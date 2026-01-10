Shader "SodaCutout_Fixed_ParticleColor"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}
        _BaseColor ("BaseColor", Color) = (1,1,1,1)
        _ClipThreshold ("ClipThreshold", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZWrite On
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4x4 unity_ObjectToWorld;
            float4x4 unity_MatrixVP;
            float4 _MainTex_ST;

            float4 _BaseColor;
            float _ClipThreshold;

            struct appdata
            {
                float4 pos   : POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;      // <- важно: цвет из ParticleSystem
            };

            struct v2f
            {
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;      // <- протаскиваем дальше
                float4 pos   : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.uv    = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                o.color = v.color;
                o.pos   = mul(unity_MatrixVP, mul(unity_ObjectToWorld, v.pos));
                return o;
            }

            Texture2D<float4> _MainTex;
            SamplerState sampler_MainTex;

            float4 frag(v2f i) : SV_TARGET
            {
                float4 tex = _MainTex.Sample(sampler_MainTex, i.uv);

                // Material tint + ParticleSystem color
                float4 c = tex * _BaseColor * i.color;

                // Cutout по итоговой альфе (так цвет/альфа из системы тоже влияет)
                clip(c.a - _ClipThreshold);

                return c;
            }
            ENDHLSL
        }
    }
}
