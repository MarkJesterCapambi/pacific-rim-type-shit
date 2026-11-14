Shader "PacificRim/JaegerWetLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo Palette", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Metallic("Metallic", Range(0, 1)) = 0.18
        _Smoothness("Dry Smoothness", Range(0, 1)) = 0.48
        _WetSmoothness("Wet Smoothness", Range(0, 1)) = 0.88
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _Metallic;
                half   _Smoothness;
                half   _WetSmoothness;
            CBUFFER_END

            // Global thunderstorm weather state (outside UnityPerMaterial for SRP Batcher compatibility)
            float  _GlobalRainWetness;
            float4 _GlobalWindVector;
            float  _GlobalLightningFlash;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(nrmInputs.normalWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogCoord = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            // Analytical tri-planar rain rivulet perturbation tilted by wind (zero extra texture fetches)
            float3 ApplyWetRivuletNormal(float3 worldPos, float3 baseNormal, float wetness)
            {
                if (wetness <= 0.001) return baseNormal;

                float t = _Time.y;
                float2 windDrift = _GlobalWindVector.xy * 0.25;

                // Blend horizontal/vertical projection weights for continuous streaks along vertical plates
                float3 absN = abs(baseNormal);
                float sideMask = saturate(1.0 - absN.y * 0.65);

                // Downward streaming rivulets
                float verticalFlowY = worldPos.y * 2.4 + t * 5.8;
                float horizCoord = (absN.x > absN.z ? worldPos.z : worldPos.x) * 3.8
                                 + (absN.x > absN.z ? windDrift.y : windDrift.x) * worldPos.y * 0.35;

                float r1 = sin(horizCoord + sin(verticalFlowY * 0.45) * 1.15);
                float r2 = cos(horizCoord * 2.1 - verticalFlowY * 0.85);
                float streakGrad = (cos(horizCoord + sin(verticalFlowY * 0.45) * 1.15) * 0.6
                                 - sin(horizCoord * 2.1 - verticalFlowY * 0.85) * 0.4);

                // Top-facing rain impact dimples
                float topPuddle = cos(worldPos.x * 7.5 + t * 8.2) * sin(worldPos.z * 7.5 - t * 6.9);

                float3 perpXZ = absN.x > absN.z ? float3(0.0, 0.0, 1.0) : float3(1.0, 0.0, 0.0);
                float3 pert = perpXZ * (streakGrad * sideMask * 0.14 * wetness)
                            + float3(r1 * topPuddle * 0.06, 0.0, r2 * topPuddle * 0.06) * ((1.0 - sideMask) * wetness);

                return normalize(baseNormal + pert);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 baseAlbedo = texCol.rgb * _BaseColor.rgb;

                float wetness = saturate(_GlobalRainWetness);
                // Wet surfaces darken subtly due to internal light trapping
                half3 albedo = baseAlbedo * lerp(1.0h, 0.82h, (half)(wetness * (1.0h - _Metallic * 0.5h)));
                half smoothness = lerp(_Smoothness, _WetSmoothness, (half)wetness);

                float3 baseNormalWS = normalize(input.normalWS);
                float3 normalWS = ApplyWetRivuletNormal(input.positionWS, baseNormalWS, wetness);

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float NdotV = saturate(dot(normalWS, viewDirWS));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 lightDir = normalize(mainLight.direction);
                float NdotL = saturate(dot(normalWS, lightDir));
                half3 radiance = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation);

                // Simplified URP metallic PBR + analytical wet specular lobe
                half3 dielectricF0 = half3(0.04h, 0.04h, 0.04h);
                half3 F0 = lerp(dielectricF0, albedo, _Metallic);
                half3 diffuseColor = albedo * (1.0h - _Metallic);

                float3 halfDir = normalize(lightDir + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float VdotH = saturate(dot(viewDirWS, halfDir));

                float perceptualRoughness = saturate(1.0 - smoothness);
                float roughness = max(perceptualRoughness * perceptualRoughness, 0.02);
                float a2 = roughness * roughness;
                float dDenom = NdotH * NdotH * (a2 - 1.0) + 1.0;
                float D = a2 / (3.14159265 * max(dDenom * dDenom, 0.0001));
                half3 F = F0 + (1.0h - F0) * pow(1.0 - VdotH, 5.0);
                half3 specular = (half3)(D * 0.25) * F;

                half3 directLighting = (diffuseColor * (1.0h - F) / 3.14159265h + specular * 3.14159265h) * radiance * NdotL;

                // Ambient SH + wet skybox reflection + Fresnel wet coat sheen
                half3 sh = SampleSH(normalWS);
                float3 reflectDir = reflect(-viewDirWS, normalWS);
                half3 envReflect = GlossyEnvironmentReflection(reflectDir, perceptualRoughness, 1.0h);
                half3 envFresnel = F0 + (max(half3(smoothness, smoothness, smoothness), F0) - F0) * pow(1.0 - NdotV, 5.0);

                // Electric sky/water bounce during lightning strikes
                half3 lightningColor = half3(0.55h, 0.72h, 1.0h) * (half)(_GlobalLightningFlash * 0.55);
                half wetRim = pow(1.0 - NdotV, 3.5) * wetness * 0.35;

                half3 indirectLighting = diffuseColor * sh
                                       + envReflect * envFresnel * lerp(0.55h, 1.15h, (half)wetness)
                                       + (half3)wetRim * (half3(0.45h, 0.58h, 0.72h) + lightningColor)
                                       + albedo * lightningColor * saturate(normalWS.y * 0.4 + 0.6) * 0.35h;

                half3 finalRgb = directLighting + indirectLighting;
                finalRgb = MixFog(finalRgb, input.fogCoord);

                return half4(finalRgb, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
