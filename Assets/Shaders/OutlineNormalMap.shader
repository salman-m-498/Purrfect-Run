Shader "Custom/OutlineNormalMap"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
        _NormalTex("Normal Map", 2D) = "bump" {}
        _LightDir("Fake Light Direction", Vector) = (0.4, 0.5, 0.7, 0)
        _Brightness("Light Strength", Range(0, 2)) = 1
        _AlphaCut("Alpha Cutoff", Range(0,1)) = 0.1

        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness("Outline Thickness", Range(0, 0.05)) = 0.015
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        // --- PASS 1: OUTLINE EXPANSION ---
        Pass
        {
            Name "OUTLINE"
            Cull Front   // Expand outward

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float _OutlineThickness;
            float4 _OutlineColor;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;

                // push vertices outward (screen-space outline)
                float3 norm = normalize(v.vertex.xyz);
                float3 offset = norm * _OutlineThickness;

                o.pos = UnityObjectToClipPos(v.vertex + float4(offset,0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        // --- PASS 2: SPRITE + FAKE NORMAL LIGHTING ---
        Pass
        {
            Name "MAIN"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _NormalTex;

            float4 _LightDir;
            float _Brightness;
            float _AlphaCut;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float3 UnpackNormal(float4 n)
            {
                float3 normal = n.xyz * 2 - 1;
                normal.z = sqrt(1 - saturate(dot(normal.xy, normal.xy)));
                return normal;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                if (c.a < _AlphaCut) discard;

                float3 normal = UnpackNormal(tex2D(_NormalTex, i.uv));
                float3 L = normalize(_LightDir.xyz);

                float NdotL = saturate(dot(normal, L));
                float lit = (0.5 + NdotL * 0.5) * _Brightness;

                c.rgb *= lit;
                return c;
            }
            ENDCG
        }
    }

    FallBack Off
}
