Shader "Custom/SmokeFireFX_URP"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}
        _Alpha ("Alpha", Range(0,1)) = 0.42
        _AlphaTint ("AlphaTint", Color) = (1,1,1,1)
        _AlphaRemap ("AlphaRemap", Vector) = (0,1,0,1)
        [HDR] _EmissionColor ("EmissionColor", Color) = (0,0,0,1)

        _SoftDistance ("SoftDistance", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _AlphaTint;
                float4 _AlphaRemap;
                float4 _EmissionColor;
                float  _Alpha;
                float  _SoftDistance;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                float4 screenPos   : TEXCOORD1;
            };

            float Remap01(float x, float2 inMinMax, float2 outMinMax)
            {
                float t = saturate((x - inMinMax.x) / max(1e-5, (inMinMax.y - inMinMax.x)));
                return lerp(outMinMax.x, outMinMax.y, t);
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionHCS = pos.positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.screenPos = ComputeScreenPos(o.positionHCS);
                return o;
            }

            half4 frag (Varyings i) : SV_TARGET
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 col = tex * i.color;

                float a = Remap01(col.a, _AlphaRemap.xy, _AlphaRemap.zw);
                a *= saturate(_Alpha) * saturate(_AlphaTint.a);

                // Soft particles (только если включено)
                if (_SoftDistance > 1e-4)
                {
                    float2 uvSS = i.screenPos.xy / i.screenPos.w;

                    // глубина сцены (raw -> eye)
                    float sceneRaw = SampleSceneDepth(uvSS);
                    float sceneEye = LinearEyeDepth(sceneRaw, _ZBufferParams);

                    // глубина частицы (raw -> eye)
                    float particleRaw = i.positionHCS.z / i.positionHCS.w;
                    float particleEye = LinearEyeDepth(particleRaw, _ZBufferParams);

                    float fade = saturate((sceneEye - particleEye) / _SoftDistance);
                    a *= fade;
                }

                col.a = a;
                col.rgb *= _AlphaTint.rgb;
                col.rgb += _EmissionColor.rgb * col.a;

                return col;
            }
            ENDHLSL
        }
    }
}
