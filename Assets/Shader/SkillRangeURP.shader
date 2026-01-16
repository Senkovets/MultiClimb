Shader "Custom/SkillRangeURP_Base"
{
    Properties
    {
        _RingColor ("Ring Color", Color) = (0.25, 0.75, 1.0, 1.0)
        _ProgressColor ("Progress Color", Color) = (0.25, 1.0, 1.0, 1.0)
        _BgColor ("Background Color (alpha=0)", Color) = (0,0,0,0)

        _Radius ("Radius (UV space)", Range(0.0, 0.707)) = 0.45
        _LineWidth ("Line Width", Range(0.0005, 0.2)) = 0.04
        _Blur ("Edge Blur", Range(0.0005, 0.2)) = 0.02

        _Progress ("Progress 0..1", Range(0,1)) = 0.75
        _ProgressStartAngle ("Start Angle (deg)", Range(-180, 180)) = -90

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

            half Angle01(float2 uv, half startDeg)
            {
                float2 p = uv - 0.5;
                float ang = atan2(p.y, p.x);          // -PI..PI
                float start = radians((float)startDeg);

                // Convert to 0..1 using frac (stable on d3d11)
                // INV_TAU = 1/(2*pi)
                const float INV_TAU = 0.15915494309189535;
                float a01 = frac((ang - start) * INV_TAU + 1.0);
                return (half)a01;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half ring = RingMask(i.uv, _Radius, _LineWidth, _Blur);

                half a01 = Angle01(i.uv, _ProgressStartAngle);
                half prog = saturate(_Progress);
                half sector = step(a01, prog);        // 1 inside the progress arc
                half progMask = ring * sector;

                half4 col = _BgColor;
                col = lerp(col, _RingColor, ring);
                col = lerp(col, _ProgressColor, progMask);

                col.a = saturate(max(ring, progMask) * _Alpha);
                return col;
            }
            ENDHLSL
        }
    }
}
