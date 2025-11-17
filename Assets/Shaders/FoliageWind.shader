Shader "PSX/FoliageWind"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _WindSpeed ("Wind Speed", Float) = 1.0
        _WindStrength ("Wind Strength", Float) = 0.1
        _WindDirection ("Wind Direction", Vector) = (1, 0, 1, 0)
        
        // PSX Style properties
        _AffineMapping ("Affine Mapping", Range(0, 1)) = 1
        _VertexSnapping ("Vertex Snapping", Float) = 0.01
        _DrawDistance ("Draw Distance", Float) = 50
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; // Use vertex color R channel for sway amount
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float distance : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _WindSpeed;
            float _WindStrength;
            float4 _WindDirection;
            float _AffineMapping;
            float _VertexSnapping;
            float _DrawDistance;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                
                // Wind calculation using world position for variation
                float windTime = _Time.y * _WindSpeed;
                float windPhase = (worldPos.x + worldPos.z) * 0.5;
                float wind = sin(windTime + windPhase) * cos(windTime * 0.7 + windPhase * 1.3);
                
                // Apply wind using vertex color red channel (0 = no sway, 1 = full sway)
                float swayAmount = v.color.r * _WindStrength;
                float3 windOffset = normalize(_WindDirection.xyz) * wind * swayAmount;
                worldPos.xyz += windOffset;
                
                // PSX vertex snapping
                if (_VertexSnapping > 0)
                {
                    worldPos.xyz = floor(worldPos.xyz / _VertexSnapping) * _VertexSnapping;
                }
                
                float4 viewPos = mul(UNITY_MATRIX_V, worldPos);
                o.pos = mul(UNITY_MATRIX_P, viewPos);
                
                // Calculate distance for fade
                o.distance = length(_WorldSpaceCameraPos - worldPos.xyz);
                
                // Affine texture mapping (PSX style)
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                if (_AffineMapping > 0)
                {
                    o.uv *= o.pos.w; // Classic PSX affine mapping trick
                }
                
                o.color = v.color * _Color;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                // Distance fade
                float fade = saturate((_DrawDistance - i.distance) / 5.0);
                if (fade <= 0) discard;
                
                // Affine UV correction
                float2 uv = i.uv;
                if (_AffineMapping > 0)
                {
                    uv /= i.pos.w;
                }
                
                fixed4 col = tex2D(_MainTex, uv) * i.color;
                
                // Alpha cutoff for vegetation
                if (col.a < 0.5) discard;
                
                col.rgb *= fade;
                
                return col;
            }
            ENDCG
        }
    }
}