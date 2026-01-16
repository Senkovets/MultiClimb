Shader "Custom/SkillRangeRingGlowURP"
{
    Properties
    {
        _RingColor ("Ring Color", Color) = (0.25, 0.75, 1.0, 1.0)

        _Radius ("Radius (UV space)", Range(0.0, 0.707)) = 0.45
        _LineWidth ("Line Width", Range(0.0005, 0.2)) = 0.04
        _Blur ("Edge Blur", Range(0.0005, 0.2)) = 0.02

        _GlowStrength ("Glow Strength", Range(0,3)) = 0.9
        _GlowWidth ("Glow Width", Range(0.0005, 0.25)) = 0.08

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

                half _Radius;
                half _LineWidth;
                half _Blur;

                half _GlowStrength;
                half _GlowWidth;

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

            half GlowMask(float2 uv, half radius, half width, half glowWidth, half blur)
            {
                float2 p = uv - 0.5;
                half d = (half)length(p);

                half halfW = max(width * 0.5h, 1e-5h);
                half gW = max(glowWidth, 1e-5h);
                half b = max(blur, 1e-5h);

                half outer = radius + halfW;
                half glowOuter = outer + gW;

                half m = smoothstep(outer - b, outer + b, d);              // starts at outer
                half m2 = 1.0h - smoothstep(glowOuter - b, glowOuter + b, d);
                return saturate(m * m2);
            }

            half4 frag(Varyings i) : SV_Target
            {
                half ring = RingMask(i.uv, _Radius, _LineWidth, _Blur);
                half glow = GlowMask(i.uv, _Radius, _LineWidth, _GlowWidth, _Blur);

                half4 col = half4(0,0,0,0);

                // base ring
                col.rgb = lerp(col.rgb, _RingColor.rgb, ring);
                col.a   = max(col.a, ring * _RingColor.a);

                // glow (adds light outside ring)
                col.rgb = saturate(col.rgb + glow * _GlowStrength * _RingColor.rgb);
                col.a   = max(col.a, glow * 0.65h * _RingColor.a);

                col.a = saturate(col.a * _Alpha);
                return col;
            }
            ENDHLSL
        }
    }
}
