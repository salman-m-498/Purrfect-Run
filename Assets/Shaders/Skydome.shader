Shader "PSX/SkyDome"
{
    Properties
    {
        _CloudTex ("Cloud Texture", 2D) = "white" {}
        _SkyColorTop ("Sky Color Top", Color) = (0.4, 0.6, 0.9, 0.5)
        _SkyColorHorizon ("Sky Color Horizon", Color) = (0.8, 0.85, 0.95, 0.3)
        _CloudColor ("Cloud Color", Color) = (1, 1, 1, 1)
        _CloudSpeed ("Cloud Speed", Vector) = (0.01, 0.005, 0, 0)
        _CloudScale ("Cloud Scale", Float) = 1.0
        _CloudAlpha ("Cloud Alpha", Range(0, 1)) = 0.8
        _SkyAlpha ("Sky Alpha", Range(0, 1)) = 0.5
        _HorizonHeight ("Horizon Height", Range(0, 1)) = 0.3
        
        // PSX Style
        _VertexSnapping ("Vertex Snapping", Float) = 0.02
        _AffineMapping ("Affine Mapping", Range(0, 1)) = 1
    }
    
    SubShader
    {
        Tags { "Queue"="Geometry-100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 cloudUV : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            sampler2D _CloudTex;
            float4 _CloudTex_ST;
            float4 _SkyColorTop;
            float4 _SkyColorHorizon;
            float4 _CloudColor;
            float2 _CloudSpeed;
            float _CloudScale;
            float _CloudAlpha;
            float _SkyAlpha;
            float _HorizonHeight;
            float _VertexSnapping;
            float _AffineMapping;

            v2f vert (appdata v)
            {
                v2f o;
                
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                
                // PSX vertex snapping
                if (_VertexSnapping > 0)
                {
                    worldPos.xyz = floor(worldPos.xyz / _VertexSnapping) * _VertexSnapping;
                }
                
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = worldPos.xyz;
                o.screenPos = o.pos;
                
                // Cloud UVs - use vertex position for spherical mapping
                float3 normalizedPos = normalize(v.vertex.xyz);
                o.cloudUV = float2(
                    atan2(normalizedPos.x, normalizedPos.z) / (2 * 3.14159) + 0.5,
                    asin(normalizedPos.y) / 3.14159 + 0.5
                );
                
                o.cloudUV *= _CloudScale;
                o.cloudUV += _CloudSpeed * _Time.y;
                
                // Affine mapping
                if (_AffineMapping > 0)
                {
                    o.cloudUV *= o.pos.w;
                }
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Normalize world position for gradient
                float3 viewDir = normalize(i.worldPos);
                float heightGradient = saturate((viewDir.y + 0.2) / 1.2);
                
                // Sky gradient
                float4 skyColor = lerp(_SkyColorHorizon, _SkyColorTop, heightGradient);
                
                // Affine UV correction
                float2 cloudUV = i.cloudUV;
                if (_AffineMapping > 0)
                {
                    cloudUV /= i.screenPos.w;
                }
                
                // Sample cloud texture
                float4 clouds = tex2D(_CloudTex, cloudUV);
                
                // Only show clouds above horizon
                float cloudVisibility = saturate((heightGradient - _HorizonHeight) / (1.0 - _HorizonHeight));
                clouds.a *= cloudVisibility * _CloudAlpha;
                
                // Blend clouds with sky
                float4 finalColor = lerp(skyColor, _CloudColor, clouds.r * clouds.a);
                
                // Control overall transparency with _SkyAlpha
                finalColor.a = lerp(_SkyAlpha, 1.0, clouds.r * clouds.a);
                
                return finalColor;
            }
            ENDCG
        }
    }
}