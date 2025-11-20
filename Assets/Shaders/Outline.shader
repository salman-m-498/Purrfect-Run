Shader "Custom/Outline"
{
    Properties
    {
        _Color("Color", Color) = (0,0,0,1)
        _Thickness("Thickness", Float) = 0.03
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Cull Front   // draw backfaces
        ZWrite On
        ZTest Less

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            fixed4 _Color;
            float _Thickness;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 norm = normalize(v.normal) * _Thickness;
                o.pos = UnityObjectToClipPos(v.vertex + float4(norm,0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
