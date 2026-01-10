Shader "Custom/SodaCharacter_HurtURP"
{
    Properties
    {
        _MainTex ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        _HurtColor ("Hurt Color", Color) = (1,0.3,0.3,1)
        _HurtIntensity ("Hurt Intensity", Range(0,5)) = 1.5

        _HurtValue ("_HurtValue", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP core utilities (camera position, transforms, etc.)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _HurtColor;
                float  _HurtIntensity;
                float  _HurtValue;
                float4 _MainTex_ST;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;

                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(v.normalOS);

                o.positionHCS = posInputs.positionCS;
                o.positionWS  = posInputs.positionWS;
                o.normalWS    = normalize(nrmInputs.normalWS);
                o.uv          = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            half4 frag (Varyings i) : SV_TARGET
            {
                half4 baseTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 col = baseTex * (half4)_BaseColor;

                // View dir from camera (URP-safe)
                float3 camPosWS = GetCameraPositionWS();
                float3 viewDirWS = normalize(camPosWS - i.positionWS);

                // Simple rim (fresnel)
                float ndv = saturate(dot(i.normalWS, viewDirWS));
                float fresnel = pow(1.0 - ndv, 2.5);

                // Hurt glow driven by HurtVisual.cs via MaterialPropertyBlock
                float hurt = saturate(_HurtValue) * _HurtIntensity;
                col.rgb += (half3)_HurtColor.rgb * (hurt + fresnel * hurt);

                return col;
            }
            ENDHLSL
        }
    }
}
