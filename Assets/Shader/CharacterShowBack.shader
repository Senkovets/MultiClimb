Shader "Custom/CharacterShowBack_HurtOverlay"
{
    Properties
    {
        _Color0 ("Overlay Color", Color) = (0.35, 0.78, 1, 1)
        _Power ("Power", Range(0,5)) = 1
        _HurtValue ("_HurtValue", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+10"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "Overlay"
            Blend One One        // ADDITIVE
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color0;
                float  _Power;
                float  _HurtValue;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(v.normalOS);

                o.positionHCS = pos.positionCS;
                o.normalWS    = normalize(nrm.normalWS);
                o.viewDirWS   = normalize(GetCameraPositionWS() - pos.positionWS);
                return o;
            }

            half4 frag (Varyings i) : SV_TARGET
            {
                float fresnel = pow(1 - saturate(dot(i.normalWS, i.viewDirWS)), 2.0);
                float intensity = fresnel * _HurtValue * _Power;

                return half4(_Color0.rgb * intensity, 1);
            }
            ENDHLSL
        }
    }
}
