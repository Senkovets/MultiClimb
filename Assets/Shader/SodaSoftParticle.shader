Shader "SodaSoftParticle_FixedTransparent"
{
    Properties
    {
        _BaseColor ("BaseColor", Color) = (1,1,1,1)
        _MainTex ("MainTex", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4x4 unity_ObjectToWorld;
            float4x4 unity_MatrixVP;

            float4 _MainTex_ST;
            float4 _BaseColor;

            struct Vertex_Stage_Input
            {
                float4 pos : POSITION;
                float2 uv  : TEXCOORD0;
                float4 col : COLOR;       // важно для ParticleSystem/Trail
            };

            struct Vertex_Stage_Output
            {
                float2 uv  : TEXCOORD0;
                float4 col : COLOR;
                float4 pos : SV_POSITION;
            };

            Vertex_Stage_Output vert (Vertex_Stage_Input input)
            {
                Vertex_Stage_Output o;
                o.uv  = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                o.col = input.col * _BaseColor;
                o.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
                return o;
            }

            Texture2D<float4> _MainTex;
            SamplerState sampler_MainTex;

            float4 frag (Vertex_Stage_Output input) : SV_TARGET
            {
                float4 tex = _MainTex.Sample(sampler_MainTex, input.uv);
                // применяем vertex color и BaseColor
                float4 c = tex * input.col;
                return c;
            }
            ENDHLSL
        }
    }
}
