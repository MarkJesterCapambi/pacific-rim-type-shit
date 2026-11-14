Shader "PacificRim/OceanWater"
{
    Properties
    {
        _DeepColor("Deep Ocean Color", Color) = (0.015, 0.055, 0.12, 0.98)
        _ShallowColor("Shallow Water Tint", Color) = (0.04, 0.18, 0.32, 0.88)
        _FoamColor("Wave Crest Foam Color", Color) = (0.92, 0.96, 1.0, 0.95)
        _FoamThreshold("Foam Crest Height", Range(-1.0, 3.0)) = 0.5
        _FoamFalloff("Foam Softness", Range(0.01, 1.0)) = 0.2
        _Smoothness("Surface Smoothness", Range(0.0, 1.0)) = 0.93
        _SpecularColor("Specular Reflection Color", Color) = (0.85, 0.95, 1.0, 1.0)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 color      : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _ShallowColor;
                float4 _FoamColor;
                float _FoamThreshold;
                float _FoamFalloff;
                float _Smoothness;
                float4 _SpecularColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 lightDir = normalize(mainLight.direction);

                // Height gradient: deeper water is dark navy/abyssal, higher water has lighter blue tint
                float relY = input.positionWS.y;
                float depthFactor = saturate((relY - 4.0) * 0.25);
                half4 waterCol = lerp(_DeepColor, _ShallowColor, depthFactor);

                // Vertex foam (churning ripples from Jaeger feet) combined with crest elevation
                float heightFoam = smoothstep(_FoamThreshold - _FoamFalloff, _FoamThreshold + _FoamFalloff, relY - 6.0);
                float totalFoam = max(heightFoam, input.color.r);
                waterCol = lerp(waterCol, _FoamColor, saturate(totalFoam * _FoamColor.a));

                // Specular highlight / glint on ocean surface
                float3 halfDir = normalize(lightDir + viewDirWS);
                float specPower = exp2(10.0 * _Smoothness + 1.0);
                float spec = pow(max(dot(normalWS, halfDir), 0.0), specPower);
                float3 specular = spec * _SpecularColor.rgb * mainLight.color * (1.0 - totalFoam * 0.5);

                // Fresnel grazing reflection
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), 4.0);
                waterCol.rgb += fresnel * 0.2 * _SpecularColor.rgb;

                // Diffuse ocean lighting
                float NdotL = saturate(dot(normalWS, lightDir) * 0.65 + 0.35);
                float3 finalRgb = waterCol.rgb * mainLight.color * NdotL + specular;

                return half4(finalRgb, waterCol.a);
            }
            ENDHLSL
        }
    }

    // Built-in Render Pipeline Fallback
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
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
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float4 color : COLOR;
            };

            float4 _DeepColor;
            float4 _ShallowColor;
            float4 _FoamColor;
            float _FoamThreshold;
            float _FoamFalloff;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = lerp(_DeepColor, _ShallowColor, 0.4);
                float foam = smoothstep(_FoamThreshold - _FoamFalloff, _FoamThreshold + _FoamFalloff, i.worldPos.y - 6.0);
                foam = max(foam, i.color.r);
                col = lerp(col, _FoamColor, foam * 0.8);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
