Shader "UI/CircularTransition"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0, 0, 0, 1)
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Range(0, 2)) = 1
        _Smoothness ("Edge Smoothness", Range(0, 0.5)) = 0.1
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue"="Overlay" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            sampler2D _MainTex;
            float4 _Color;
            float2 _Center;
            float _Radius;
            float _Smoothness;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate distance from center
                float2 delta = i.uv - _Center;
                float dist = length(delta);
                
                // Create circular mask with smooth edge
                float circle = smoothstep(_Radius - _Smoothness, _Radius, dist);
                
                // Return color with alpha based on circle mask
                fixed4 col = _Color;
                col.a *= circle;
                
                return col;
            }
            ENDCG
        }
    }
}