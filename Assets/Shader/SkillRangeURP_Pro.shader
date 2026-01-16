Shader "Custom/SkillRangeURP_Pro"
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

            // A softer outer glow around the ring
            half GlowMask(float2 uv, half radius, half width, half glowWidth, half blur)
            {
                float2 p = uv - 0.5;
                half d = (half)length(p);

                half halfW = max(width * 0.5h, 1e-5h);
                half gW = max(glowWidth, 1e-5h);
                half b = max(blur, 1e-5h);

                // Glow band outside outer edge
                half outer = radius + halfW;
                half glowOuter = outer + gW;

                half m = smoothstep(outer - b, outer + b, d);              // starts at outer
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

            // Grid lines in UV, returns 0..1
            half GridMask(float2 uv, half tiles, half lineWidth, half blur)
            {
                float2 g = uv * (float)tiles;
                float2 f = frac(g);

                // distance to nearest grid line in cell space
                float2 dist = min(f, 1.0 - f);
                half d = (half)min(dist.x, dist.y);

                half lw = max(lineWidth, 1e-5h);
                half b = max(blur, 1e-5h);

                // line if close to border
                half m = 1.0h - smoothstep(lw - b, lw + b, d);
                return saturate(m);
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Optional pulse: modulates brightness a bit (kept subtle)
                half pulse = 1.0h;
                if (_PulseStrength > 1e-5h)
                {
                    // _Time.y is seconds
                    half s = (half)sin(_Time.y * (float)_PulseSpeed);
                    pulse = 1.0h + s * _PulseStrength * 0.15h;
                }

                half ring = RingMask(i.uv, _Radius, _LineWidth, _Blur);

                half a01 = Angle01(i.uv, _ProgressStartAngle);
                half prog = saturate(_Progress);
                half sector = step(a01, prog);
                half progMask = ring * sector;

                // Glow
                half glow = GlowMask(i.uv, _Radius, _LineWidth, _GlowWidth, _Blur);

                // Grid only where ring exists (inside ring band)
                half grid = GridMask(i.uv, _GridTiles, _GridLineWidth, _Blur);
                half gridMask = ring * grid * _GridStrength;

                // Color build
                half4 col = _BgColor;

                // Base ring
                half4 ringCol = _RingColor;
                ringCol.rgb *= pulse;

                // Progress overlay
                half4 progCol = _ProgressColor;
                progCol.rgb *= pulse;

                // Mix
                col = lerp(col, ringCol, ring);
                col = lerp(col, progCol, progMask);

                // Grid: slight brighten (not additive blend)
                col.rgb = lerp(col.rgb, saturate(col.rgb + gridMask * 0.35h), gridMask);

                // Glow: adds outside ring
                col.rgb = saturate(col.rgb + glow * _GlowStrength * ringCol.rgb);

                // Alpha: ring/prog plus glow contribution
                half a = max(ring, progMask);
                a = saturate(a + glow * 0.65h);
                col.a = saturate(a * _Alpha);

                return col;
            }
            ENDHLSL
        }
    }
}
