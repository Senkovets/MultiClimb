Shader "Qjjxk/RoundCorner"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _RoundType("RoundType", Int) = 0
        _CornerType("CornerType", Int) = 0
        _Radius("Radius", Range(0, 1)) = 0.2
        _FreeRadius("FreeRadius", Vector) = (0.2, 0.2, 0.2, 0.2)
        _N("N", Float) = 5
        _FreeN("FreeN", Vector) = (5, 5, 5, 5)
        _Width ("Width", Float) = 1
        _Height ("Height", Float) = 1
    }
    SubShader
    {
        Cull Off
        ZWrite Off
        Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha

        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            int _UIVertexColorAlwaysGammaSpace;
            sampler2D _MainTex;
            int _RoundType;
            int _CornerType;
            float _Radius;
            float4 _FreeRadius;
            float _N;
            float4 _FreeN;
            float _Width;
            float _Height;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                if (_UIVertexColorAlwaysGammaSpace)
                {
                    if (!IsGammaSpace()) v.color.rgb = UIGammaToLinear(v.color.rgb);
                }
                o.color = v.color;

                return o;
            }

            float EllipseDistance(float2 pos)
            {
                float n = _CornerType == 0 ? _N : _FreeN[int(step(0, pos.x) * 2 + step(0, pos.y))];

                pos = abs(pos);
                float2 ratioN = float2(n, n);
                ratioN *= float2(max(_Width, _Height) / _Height, max(_Height, _Width) / _Width);

                return pow(pow(pos.x, ratioN.x) + pow(pos.y, ratioN.y), 10 / n) - 1;
            }

            float RoundDistance(float2 pos)
            {
                float radius = _CornerType == 0 ? _Radius : _FreeRadius[int(step(0, pos.x) * 2 + step(0, pos.y))];

                float2 ratio = float2(max(_Width, _Height) / _Height, max(_Height, _Width) / _Width);
                pos = abs(pos) * ratio;
                float2 center = ratio - radius;
                if (pos.x < center.x || pos.y < center.y)
                {
                    return max(pos.x - ratio.x, pos.y - ratio.y);
                }
                return length(pos - center) - radius;
            }

            fixed4 frag(const v2f i) : SV_Target
            {
                fixed4 color = i.color * tex2D(_MainTex, i.uv);
                float2 pos = (i.uv - 0.5) * 2.0;

                float dist = _RoundType == 0 ? RoundDistance(pos) : EllipseDistance(pos);
                float pwidth = fwidth(dist);
                float alpha = smoothstep(dist + 0.5 * pwidth, dist + 2 * pwidth, pwidth);
                color.a *= alpha;
                return color;
            }
            ENDCG
        }
    }
}