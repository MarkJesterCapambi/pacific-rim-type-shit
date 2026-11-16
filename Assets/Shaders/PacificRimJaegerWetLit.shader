Shader "PacificRim/JaegerWetLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo Palette", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Metallic("Base Armor Metallic", Range(0, 1)) = 0.82
        _Smoothness("Dry Smoothness", Range(0, 1)) = 0.56
        _WetSmoothness("Wet Clearcoat Smoothness", Range(0, 1)) = 0.92
        _NormalStrength("Armor Normal & Scratch Strength", Range(0, 2)) = 1.0
        _EdgeWearStrength("Chipped Steel Edge Wear", Range(0, 2)) = 0.85
        _VisorEmission("Visor & Turbine Glow", Range(0, 5)) = 2.2
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
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
                float3 positionOS : TEXCOORD4;
                float3 normalOS   : TEXCOORD5;
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
                float  _NormalStrength;
                float  _EdgeWearStrength;
                float  _VisorEmission;
            CBUFFER_END

            // Global thunderstorm & reactor state (outside UnityPerMaterial for SRP Batcher compatibility)
            float  _GlobalRainWetness;
            float4 _GlobalWindVector;
            float  _GlobalLightningFlash;
            float  _GlobalPowerUp;

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
                output.positionOS = input.positionOS.xyz;
                output.normalOS = normalize(input.normalOS);
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

            float ArmorHash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p.x + p.y) * 43758.5453);
            }

            float ArmorValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = ArmorHash(i);
                float b = ArmorHash(i + float2(1.0, 0.0));
                float c = ArmorHash(i + float2(0.0, 1.0));
                float d = ArmorHash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Object-space triplanar armor micro-detail (brushed rolled steel, battle scratches, panel seams)
            // + screen-space normal derivative edge curvature for chipped steel wear & cavity grime
            void EvaluateArmorSurface(
                float3 posOS, float3 baseNormalOS, float3 baseNormalWS,
                out float3 outNormalWS, out float cavityAO, out float edgeWear)
            {
                float3 pOS = posOS * 180.0;
                float3 w = pow(abs(baseNormalOS), 4.0) + 0.0001;
                w /= (w.x + w.y + w.z);

                // Triplanar 2D coords
                float2 uvX = pOS.zy;
                float2 uvY = pOS.xz;
                float2 uvZ = pOS.xy;

                // Brushed anisotropic steel grain + diagonal battle scratches
                float brushX = sin(uvX.y * 42.0 + ArmorValueNoise(uvX * 3.5) * 3.0);
                float brushY = sin(uvY.x * 42.0 + ArmorValueNoise(uvY * 3.5) * 3.0);
                float brushZ = sin(uvZ.y * 42.0 + ArmorValueNoise(uvZ * 3.5) * 3.0);
                float brush = brushX * w.x + brushY * w.y + brushZ * w.z;

                float scratchX = pow(saturate(1.0 - abs(ArmorValueNoise(float2(uvX.x * 2.2 + uvX.y * 1.4, uvX.y * 8.5)) - 0.5) * 8.5), 2.0);
                float scratchY = pow(saturate(1.0 - abs(ArmorValueNoise(float2(uvY.x * 2.2 - uvY.y * 1.3, uvY.y * 8.5)) - 0.5) * 8.5), 2.0);
                float scratchZ = pow(saturate(1.0 - abs(ArmorValueNoise(float2(uvZ.x * 2.2 + uvZ.y * 1.1, uvZ.y * 8.5)) - 0.5) * 8.5), 2.0);
                float scratchNoise = saturate(scratchX * w.x + scratchY * w.y + scratchZ * w.z);

                // Panel micro-grooves along triplanar axes
                float2 seamGrid = abs(frac(uvZ * 1.15) - 0.5) * 2.0;
                float seamMask = 1.0 - smoothstep(0.92, 0.99, max(seamGrid.x, seamGrid.y));

                // Triplanar gradient perturbation in object space -> transform to world space
                float3 gradOS = float3(
                    (brushY - brushZ) * 0.06 + (scratchY - scratchZ) * 0.22,
                    (brushZ - brushX) * 0.06 + (scratchZ - scratchX) * 0.22,
                    (brushX - brushY) * 0.06 + (scratchX - scratchY) * 0.22
                );
                float3 pertWS = TransformObjectToWorldDir(gradOS * _NormalStrength, false);
                outNormalWS = normalize(baseNormalWS + pertWS);

                // Screen-space curvature from normal derivatives detects bevels & sharp armor edges
                float bevel = saturate((length(ddx(baseNormalWS)) + length(ddy(baseNormalWS))) * 3.4);
                edgeWear = saturate(bevel * (0.45 + 0.75 * scratchNoise) * _EdgeWearStrength);
                cavityAO = lerp(0.72, 1.0, saturate(seamMask * (1.0 - bevel * 0.25)));
            }

            half3 EvaluateDirectGGX(
                float3 normalWS, float3 viewDirWS, float3 lightDir, half3 radiance,
                half3 diffuseColor, half3 F0, float a2)
            {
                float NdotL = saturate(dot(normalWS, lightDir));
                if (NdotL <= 0.0) return half3(0.0h, 0.0h, 0.0h);

                float3 halfDir = normalize(lightDir + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float VdotH = saturate(dot(viewDirWS, halfDir));

                float dDenom = NdotH * NdotH * (a2 - 1.0) + 1.0;
                float D = a2 / (3.14159265 * max(dDenom * dDenom, 0.0001));
                half3 F = F0 + (1.0h - F0) * pow(1.0 - VdotH, 5.0);
                half3 specular = (half3)(D * 0.25) * F;

                return (diffuseColor * (1.0h - F) / 3.14159265h + specular * 3.14159265h) * radiance * NdotL;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 rawCol = texCol.rgb * _BaseColor.rgb;

                float3 baseNormalWS = normalize(input.normalWS);
                float3 armorNormalWS;
                float cavityAO;
                float edgeWear;
                EvaluateArmorSurface(input.positionOS, normalize(input.normalOS), baseNormalWS, armorNormalWS, cavityAO, edgeWear);

                // Palette Color-ID PBR Zone Classification
                float maxC = max(rawCol.r, max(rawCol.g, rawCol.b));
                float minC = min(rawCol.r, min(rawCol.g, rawCol.b));
                float sat = maxC > 0.001 ? (maxC - minC) / maxC : 0.0;
                float lum = dot(rawCol, half3(0.299h, 0.587h, 0.114h));

                bool isBlueHull = rawCol.b > rawCol.r * 1.15h && rawCol.b > rawCol.g * 1.03h;
                bool isBareMetal = sat < 0.15;
                bool isAmber = rawCol.r > 0.50h && rawCol.g > 0.28h && rawCol.b < 0.28h;
                bool isRed = !isAmber && rawCol.r > rawCol.g * 1.4h && rawCol.r > rawCol.b * 1.4h;

                half3 albedo = rawCol;
                half zoneMetallic = _Metallic;
                half zoneSmoothness = _Smoothness;
                half3 emissive = half3(0.0h, 0.0h, 0.0h);

                if (isBlueHull)
                {
                    // Mark-3 Alaska Steel-Navy hull plating
                    albedo = lerp(rawCol, half3(0.07h, 0.14h, 0.25h) * (half)(0.45 + lum * 1.15), 0.52h);
                    zoneMetallic = _Metallic;
                    zoneSmoothness = _Smoothness;
                }
                else if (isBareMetal)
                {
                    // Inner frame / titanium piston endoskeleton
                    albedo = rawCol * 0.85h;
                    zoneMetallic = 0.94h;
                    zoneSmoothness = _Smoothness * 0.82h;
                }
                else if (isAmber)
                {
                    // Conn-pod visor, chest vortex turbine & hazard markings
                    zoneMetallic = 0.65h;
                    emissive = rawCol * half3(1.3h, 0.78h, 0.22h) * (half)(_VisorEmission * (1.0 + _GlobalPowerUp * 1.8));
                }
                else if (isRed)
                {
                    // Painted crimson hull markings
                    zoneMetallic = 0.68h;
                }

                // Chipped bare-steel edge wear on beveled armor corners
                albedo = lerp(albedo, half3(0.58h, 0.64h, 0.72h), (half)(edgeWear * 0.65));
                half metallic = lerp(zoneMetallic, 0.96h, (half)edgeWear);
                half baseSmoothness = lerp(zoneSmoothness, 0.74h, (half)(edgeWear * 0.5));

                float wetness = saturate(_GlobalRainWetness);
                // Wet surfaces darken subtly due to internal light trapping
                albedo *= lerp(1.0h, 0.82h, (half)(wetness * (1.0h - metallic * 0.5h)));
                half smoothness = lerp(baseSmoothness, _WetSmoothness, (half)wetness);

                float3 normalWS = ApplyWetRivuletNormal(input.positionWS, armorNormalWS, wetness);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float NdotV = saturate(dot(normalWS, viewDirWS));

                half3 dielectricF0 = half3(0.04h, 0.04h, 0.04h);
                half3 F0 = lerp(dielectricF0, albedo, metallic);
                half3 diffuseColor = albedo * (1.0h - metallic) * (half)cavityAO;

                float perceptualRoughness = saturate(1.0 - smoothness);
                float roughness = max(perceptualRoughness * perceptualRoughness, 0.02);
                float a2 = roughness * roughness;

                // Main directional light
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 radiance = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation);
                half3 directLighting = EvaluateDirectGGX(normalWS, viewDirWS, normalize(mainLight.direction), radiance, diffuseColor, F0, a2);

                // Additional spotlights & point lights (Chinook searchlights, Plasmacaster, Bio-EMP)
                #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP) || defined(_FORWARD_PLUS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                uint addLightsCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(addLightsCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                    half3 addRadiance = addLight.color * (addLight.distanceAttenuation * addLight.shadowAttenuation);
                    if (dot(addRadiance, addRadiance) > 0.00001h)
                    {
                        directLighting += EvaluateDirectGGX(normalWS, viewDirWS, normalize(addLight.direction), addRadiance, diffuseColor, F0, a2);
                    }
                LIGHT_LOOP_END
                #endif

                // Ambient SH + wet skybox reflection + Fresnel wet coat sheen
                half3 sh = SampleSH(normalWS);
                float3 reflectDir = reflect(-viewDirWS, normalWS);
                half3 envReflect = GlossyEnvironmentReflection(reflectDir, perceptualRoughness, 1.0h);
                half3 envFresnel = F0 + (max(half3(smoothness, smoothness, smoothness), F0) - F0) * pow(1.0 - NdotV, 5.0);

                // Electric sky/water bounce during lightning strikes
                half3 lightningColor = half3(0.55h, 0.72h, 1.0h) * (half)(_GlobalLightningFlash * 0.55);
                half wetRim = pow(1.0 - NdotV, 3.5) * wetness * 0.35;

                half3 indirectLighting = diffuseColor * sh
                                       + envReflect * envFresnel * lerp(0.55h, 1.15h, (half)wetness) * (half)cavityAO
                                       + (half3)wetRim * (half3(0.45h, 0.58h, 0.72h) + lightningColor)
                                       + albedo * lightningColor * saturate(normalWS.y * 0.4 + 0.6) * 0.35h;

                half3 finalRgb = directLighting + indirectLighting + emissive;
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
