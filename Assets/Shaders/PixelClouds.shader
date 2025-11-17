Shader "Custom/Skybox6SidedWithClouds"
{
    Properties
    {
        _FrontTex("Front (+Z)", 2D) = "white" {}
        _BackTex("Back (-Z)", 2D) = "white" {}
        _LeftTex("Left (-X)", 2D) = "white" {}
        _RightTex("Right (+X)", 2D) = "white" {}
        _UpTex("Up (+Y)", 2D) = "white" {}
        _DownTex("Down (-Y)", 2D) = "white" {}

        _CloudTex("Clouds", 2D) = "white" {}
        _CloudTint("Cloud Tint", Color) = (1,1,1,1)
        _CloudScroll("Cloud Scroll Speed", Range(0,0.1)) = 0.01
        _CloudIntensity("Cloud Opacity", Range(0,1)) = 0.5
        _PixelSize("Pixel Size", Range(1,16)) = 4
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" }
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _FrontTex;
            sampler2D _BackTex;
            sampler2D _LeftTex;
            sampler2D _RightTex;
            sampler2D _UpTex;
            sampler2D _DownTex;

            sampler2D _CloudTex;
            float4 _CloudTint;
            float _CloudScroll;
            float _CloudIntensity;
            float _PixelSize;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = normalize(v.vertex.xyz);
                return o;
            }

            fixed4 Sample6Sided(float3 dir)
            {
                float3 absDir = abs(dir);
                fixed4 col;

                if(absDir.x >= absDir.y && absDir.x >= absDir.z)
                {
                    if(dir.x > 0) col = tex2D(_RightTex, float2(-dir.z/dir.x*0.5+0.5, dir.y/dir.x*0.5+0.5));
                    else col = tex2D(_LeftTex, float2(dir.z/-dir.x*0.5+0.5, dir.y/-dir.x*0.5+0.5));
                }
                else if(absDir.y >= absDir.x && absDir.y >= absDir.z)
                {
                    if(dir.y > 0) col = tex2D(_UpTex, float2(dir.x/dir.y*0.5+0.5, -dir.z/dir.y*0.5+0.5));
                    else col = tex2D(_DownTex, float2(dir.x/-dir.y*0.5+0.5, dir.z/-dir.y*0.5+0.5));
                }
                else
                {
                    if(dir.z > 0) col = tex2D(_FrontTex, float2(dir.x/dir.z*0.5+0.5, dir.y/dir.z*0.5+0.5));
                    else col = tex2D(_BackTex, float2(-dir.x/-dir.z*0.5+0.5, dir.y/-dir.z*0.5+0.5));
                }

                return col;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Base sky
                fixed4 col = Sample6Sided(i.dir);

                // Clouds UV
                float u = (i.dir.x + 1)/2 + _Time.y * _CloudScroll;
                float v = (i.dir.y + 1)/2;

                // Pixelation
                u = floor(u * _PixelSize) / _PixelSize;
                v = floor(v * _PixelSize) / _PixelSize;

                fixed4 cloud = tex2D(_CloudTex, float2(u,v)) * _CloudTint;
                cloud.a *= _CloudIntensity;

                // Blend clouds over sky
                col.rgb = lerp(col.rgb, cloud.rgb, cloud.a);

                return col;
            }

            ENDCG
        }
    }
}
