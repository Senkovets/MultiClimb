Shader "Custom/SkillRangeFillURP"
{
    Properties
    {
        _FillColor ("Fill Color (RGBA)", Color) = (0.10, 0.45, 0.65, 1.0)
        _Radius ("Radius (UV space)", Range(0.0, 0.707)) = 0.45
        _Feather ("Edge Feather", Range(0.0005, 0.2)) = 0.04
        _Alpha ("Global Alpha", Range(0,1)) = 0.25
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
                half4 _FillColor;
                half _Radius;
                half _Feather;
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

            half FillMask(float2 uv, half radius, half feather)
            {
                float2 p = uv - 0.5;
                half d = (half)length(p);

                half f = max(feather, 1e-5h);
                // 1 inside, smooth fade near edge
                return 1.0h - smoothstep(radius - f, radius + f, d);
            }

            half4 frag(Varyings i) : SV_Target
            {
                half m = FillMask(i.uv, _Radius, _Feather);

                half4 col = _FillColor;
                col.a = saturate(_FillColor.a * _Alpha * m);

                return col;
            }
            ENDHLSL
        }
    }
}
