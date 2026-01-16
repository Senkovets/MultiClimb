Shader "Custom/SkillRangeURP_ProFill"
{
    Properties
    {
        [Header(Colors)]
        _RingColor ("Ring Color", Color) = (0.25, 0.75, 1.0, 1.0)
        _ProgressColor ("Progress Color", Color) = (0.25, 1.0, 1.0, 1.0)
        _BgColor ("Background (alpha=0)", Color) = (0,0,0,0)

        [Header(Shape)]
        _Radius ("Radius (UV space)", Range(0.0, 0.707)) = 0.45
        _LineWidth ("Line Width", Range(0.0005, 0.2)) = 0.04
        _Blur ("Edge Blur", Range(0.0005, 0.2)) = 0.02

        [Header(Progress)]
        _Progress ("Progress 0..1", Range(0,1)) = 0.75
        _ProgressStartAngle ("Start Angle (deg)", Range(-180, 180)) = -90

        [Header(Fill)]
        _FillStrength ("Fill Strength", Range(0,1)) = 0.22
        _FillColor ("Fill Color", Color) = (0.10, 0.45, 0.65, 1.0)
        _FillUseProgress ("Fill Uses Progress (0/1)", Range(0,1)) = 0
        _FillFeather ("Fill Feather", Range(0.0005, 0.2)) = 0.03

        [Header(Grid)]
        _GridStrength ("Grid Strength", Range(0,1)) = 0.35
        _GridTiles ("Grid Tiles", Range(1, 64)) = 16
        _GridLineWidth ("Grid Line Width", Range(0.0005, 0.2)) = 0.02

        [Header(Glow)]
        _GlowStrength ("Glow Strength", Range(0,3)) = 0.9
        _GlowWidth ("Glow Width", Range(0.0005, 0.25)) = 0.08

        [Header(Pulse)]
        _PulseStrength ("Pulse Strength", Range(0,1)) = 0.0
        _PulseSpeed ("Pulse Speed", Range(0,10)) = 2.0

        [Header(Final)]
        _Alpha ("Global Alpha", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RingColor;
                half4 _ProgressColor;
                half4 _BgColor;

                half _Radius;
                half _LineWidth;
                half _Blur;

                half _Progress;
                half _ProgressStartAngle;

                half _FillStrength;
                half4 _FillColor;
                half _FillUseProgress;
                half _FillFeather;

                half _GridStrength;
                half _GridTiles;
                half _GridLineWidth;

                half _GlowStrength;
                half _GlowWidth;

                half _PulseStrength;
                half _PulseSpeed;

                half _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv;
                return o;
            }

            half RingMask(float2 uv, half radius, half width, half blur)
            {
                float2 p = uv - 0.5;
                half d = (half)length(p);

                half halfW = max(width * 0.5h, 1e-5h);
                half b = max(blur, 1e-5h);

                half inner = radius - halfW;
                half outer = radius + halfW;

                half m1 = smoothstep(inner - b, inner + b, d);
                half m2 = 1.0h - smoothstep(outer - b, outer + b, d);
                return saturate(m1 * m2);
            }

            // Disc fill inside the ring radius (soft edge near radius)
            half FillMask(float2 uv, half radius, half feather)
            {
                float2 p = uv - 0.5;
                half d = (half)length(p);

                half f = max(feather, 1e-5h);
                // 1 inside, fades to 0 near edge
                return 1.0h - smoothstep(radius - f, radius + f, d);
            }

            half GlowMask(float2 uv, half radius, half width, half glowWidth, half blur)
            {
                float2 p = uv - 0.5;
                half d = (half)length(p);

                half halfW = max(width * 0.5h, 1e-5h);
                half gW = max(glowWidth, 1e-5h);
                half b = max(blur, 1e-5h);

                half outer = radius + halfW;
                half glowOuter = outer + gW;

                half m = smoothstep(outer - b, outer + b, d);
                half m2 = 1.0h - smoothstep(glowOuter - b, glowOuter + b, d);
                return saturate(m * m2);
            }

            half Angle01(float2 uv, half startDeg)
            {
                float2 p = uv - 0.5;
                float ang = atan2(p.y, p.x);
                float start = radians((float)startDeg);

                const float INV_TAU = 0.15915494309189535; // 1/(2*pi)
                float a01 = frac((ang - start) * INV_TAU + 1.0);
                return (half)a01;
            }

            half GridMask(float2 uv, half tiles, half lineWidth, half blur)
            {
                float2 g = uv * (float)tiles;
                float2 f = frac(g);

                float2 dist = min(f, 1.0 - f);
                half d = (half)min(dist.x, dist.y);

                half lw = max(lineWidth, 1e-5h);
                half b = max(blur, 1e-5h);

                half m = 1.0h - smoothstep(lw - b, lw + b, d);
                return saturate(m);
            }

            half4 frag(Varyings i) : SV_Target
            {
                // pulse multiplier (very subtle)
                half pulse = 1.0h;
                if (_PulseStrength > 1e-5h)
                {
                    half s = (half)sin(_Time.y * (float)_PulseSpeed);
                    pulse = 1.0h + s * _PulseStrength * 0.15h;
                }

                half ring = RingMask(i.uv, _Radius, _LineWidth, _Blur);

                half a01 = Angle01(i.uv, _ProgressStartAngle);
                half prog = saturate(_Progress);
                half sector = step(a01, prog);
                half progMask = ring * sector;

                // Fill
                half fill = FillMask(i.uv, _Radius, _FillFeather);
                // Option: fill only in progress sector
                half fillMask = (_FillUseProgress >= 0.5h) ? (fill * sector) : fill;

                // Glow
                half glow = GlowMask(i.uv, _Radius, _LineWidth, _GlowWidth, _Blur);

                // Grid inside ring band
                half grid = GridMask(i.uv, _GridTiles, _GridLineWidth, _Blur);
                half gridMask = ring * grid * _GridStrength;

                // Color composition
                half4 col = _BgColor;

                // Fill first (under ring)
                half4 fillCol = _FillColor;
                fillCol.rgb *= pulse;

                col.rgb = lerp(col.rgb, fillCol.rgb, fillMask * _FillStrength);
                col.a   = max(col.a, fillMask * _FillStrength);

                // Ring + progress
                half4 ringCol = _RingColor; ringCol.rgb *= pulse;
                half4 progCol = _ProgressColor; progCol.rgb *= pulse;

                col = lerp(col, ringCol, ring);
                col = lerp(col, progCol, progMask);

                // Grid brighten
                col.rgb = lerp(col.rgb, saturate(col.rgb + gridMask * 0.35h), gridMask);

                // Glow add
                col.rgb = saturate(col.rgb + glow * _GlowStrength * ringCol.rgb);

                // Final alpha: combine ring/prog + fill + glow, apply global alpha
                half a = max(max(ring, progMask), fillMask * _FillStrength);
                a = saturate(a + glow * 0.65h);
                col.a = saturate(a * _Alpha);

                return col;
            }
            ENDHLSL
        }
    }
}
