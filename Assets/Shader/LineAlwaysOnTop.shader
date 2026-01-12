Shader "Custom/LineAlwaysOnTop"
{
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(1,1,0,1); // жёлтый цвет
            }
            ENDCG
        }
    }
}

