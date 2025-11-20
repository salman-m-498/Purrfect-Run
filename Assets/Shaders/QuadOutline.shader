Shader "Custom/QuadOutline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _Thickness ("Outline Thickness", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            float4 _OutlineColor;
            float _Thickness;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 t = float2(_Thickness / 1024.0, _Thickness / 1024.0);

                float a = tex2D(_MainTex, i.uv).a;

                // sample in four directions
                a += tex2D(_MainTex, i.uv + float2(t.x, 0)).a;
                a += tex2D(_MainTex, i.uv + float2(-t.x, 0)).a;
                a += tex2D(_MainTex, i.uv + float2(0, t.y)).a;
                a += tex2D(_MainTex, i.uv + float2(0, -t.y)).a;

                if (a > 0.01) {
                    // return outline if surrounding pixel is transparent
                    if (tex2D(_MainTex, i.uv).a < 0.01)
                        return _OutlineColor;
                }

                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}
