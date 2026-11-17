Shader "PacificRim/AbyssalSeabed"
{
    Properties
    {
        _SandMap("Abyssal Silt Albedo", 2D) = "white" {}
        _SandNormalMap("Abyssal Silt Normal", 2D) = "bump" {}
        _SandScale("Abyssal Silt UV Scale", Float) = 0.08
        _SandColor("Abyssal Silt Tint", Color) = (0.09, 0.11, 0.13, 1.0)

        _RockMap("Basalt Cliff Albedo", 2D) = "white" {}
        _RockNormalMap("Basalt Cliff Normal", 2D) = "bump" {}
        _RockScale("Basalt Cliff UV Scale", Float) = 0.045
        _RockColor("Volcanic Basalt Tint", Color) = (0.05, 0.06, 0.08, 1.0)

        _SlopeThreshold("Rock Slope Start", Range(0.05, 0.8)) = 0.18
        _SlopeSharpness("Slope Blend Sharpness", Range(1.0, 12.0)) = 5.0
        _Smoothness("Wet Benthic Smoothness", Range(0.0, 1.0)) = 0.34

        [HDR] _FissureColorA("Molten Amber Core", Color) = (4.8, 1.45, 0.15, 1.0)
        [HDR] _FissureColorB("Sickly Yellow Vein", Color) = (3.8, 3.2, 0.35, 1.0)
        _BreachCenterWS("Breach World Pos (xyz=pos, w=radius)", Vector) = (0.0, 0.0, 112.0, 85.0)
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
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_SandMap);
            SAMPLER(sampler_SandMap);
            TEXTURE2D(_SandNormalMap);
            SAMPLER(sampler_SandNormalMap);

            TEXTURE2D(_RockMap);
            SAMPLER(sampler_RockMap);
            TEXTURE2D(_RockNormalMap);
            SAMPLER(sampler_RockNormalMap);

            CBUFFER_START(UnityPerMaterial)
                float  _SandScale;
                half4  _SandColor;
                float  _RockScale;
                half4  _RockColor;
                float  _SlopeThreshold;
                float  _SlopeSharpness;
                half   _Smoothness;
                half4  _FissureColorA;
                half4  _FissureColorB;
                float4 _BreachCenterWS;
            CBUFFER_END

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
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(posInputs.positionCS.z);
                output.color = input.color;
                return output;
            }

            float2 SeabedHash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            // Returns (F1, F2 - F1) for 2D cellular Voronoi rock fractures & lava fissures
            float2 SeabedVoronoiEdge(float2 x)
            {
                float2 n = floor(x);
                float2 f = frac(x);

                float md1 = 8.0;
                float md2 = 8.0;

                [unroll]
                for (int j = -1; j <= 1; j++)
                {
                    [unroll]
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2((float)i, (float)j);
                        float2 o = SeabedHash22(n + g);
                        float2 r = g + o - f;
                        float d = dot(r, r);
                        if (d < md1)
                        {
                            md2 = md1;
                            md1 = d;
                        }
                        else if (d < md2)
                        {
                            md2 = d;
                        }
                    }
                }
                return float2(sqrt(md1), sqrt(md2) - sqrt(md1));
            }

            half3 SampleTriplanarAlbedo(TEXTURE2D_PARAM(texMap, texSamp), float3 posWS, float scale, float3 triW)
            {
                half3 cx = SAMPLE_TEXTURE2D(texMap, texSamp, posWS.zy * scale).rgb;
                half3 cy = SAMPLE_TEXTURE2D(texMap, texSamp, posWS.xz * scale).rgb;
                half3 cz = SAMPLE_TEXTURE2D(texMap, texSamp, posWS.xy * scale).rgb;
                return cx * triW.x + cy * triW.y + cz * triW.z;
            }

            float3 SampleTriplanarPerturb(TEXTURE2D_PARAM(nrmMap, nrmSamp), float3 posWS, float scale, float3 triW, float strength)
            {
                float3 nx = UnpackNormal(SAMPLE_TEXTURE2D(nrmMap, nrmSamp, posWS.zy * scale));
                float3 ny = UnpackNormal(SAMPLE_TEXTURE2D(nrmMap, nrmSamp, posWS.xz * scale));
                float3 nz = UnpackNormal(SAMPLE_TEXTURE2D(nrmMap, nrmSamp, posWS.xy * scale));

                float3 wx = float3(0.0, nx.y, nx.x);
                float3 wy = float3(ny.x, 0.0, ny.y);
                float3 wz = float3(nz.x, nz.y, 0.0);
                return (wx * triW.x + wy * triW.y + wz * triW.z) * strength;
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

                float3 baseNormalWS = normalize(input.normalWS);
                float3 posWS = input.positionWS;

                // Triplanar weights so steep vertical canyon walls never stretch UVs
                float3 triW = pow(abs(baseNormalWS), 4.0) + 0.0001;
                triW /= (triW.x + triW.y + triW.z);

                // Slope-based rock exposure + vertex ridge mask (color.g)
                float slope = 1.0 - saturate(baseNormalWS.y);
                float slopeRockMask = saturate((slope - _SlopeThreshold) * _SlopeSharpness);
                float ridgeMask = saturate(input.color.g);
                float rockFactor = saturate(max(slopeRockMask, ridgeMask * 0.85));

                // Sample abyssal silt (flat floor) and fractured volcanic basalt (cliffs & boulders)
                half3 sandTex = SAMPLE_TEXTURE2D(_SandMap, sampler_SandMap, posWS.xz * _SandScale).rgb;
                half3 sandAlbedo = sandTex * _SandColor.rgb;
                float3 sandNorm = UnpackNormal(SAMPLE_TEXTURE2D(_SandNormalMap, sampler_SandNormalMap, posWS.xz * _SandScale));
                float3 sandPert = float3(sandNorm.x, 0.0, sandNorm.y) * 0.55;

                // Subtle abyssal current ripples on flat silt floor
                float ripplePhase = posWS.x * 0.28 + posWS.z * 0.14 + sin(posWS.z * 0.07) * 2.2;
                sandPert += float3(cos(ripplePhase) * 0.07, 0.0, sin(ripplePhase) * 0.04);

                half3 rockTex = SampleTriplanarAlbedo(TEXTURE2D_ARGS(_RockMap, sampler_RockMap), posWS, _RockScale, triW);
                half3 rockAlbedo = rockTex * _RockColor.rgb;
                float3 rockPert = SampleTriplanarPerturb(TEXTURE2D_ARGS(_RockNormalMap, sampler_RockNormalMap), posWS, _RockScale, triW, 0.95);

                // Voronoi cliff crack micro-facets
                float2 rockVoro = SeabedVoronoiEdge(posWS.xz * 0.095 + posWS.yy * 0.065);
                float cliffCrevice = 1.0 - smoothstep(0.0, 0.14, rockVoro.y);
                rockAlbedo *= lerp(1.0h, 0.45h, (half)cliffCrevice);

                half3 albedo = lerp(sandAlbedo, rockAlbedo, (half)rockFactor);
                float3 normalWS = normalize(baseNormalWS + lerp(sandPert, rockPert, rockFactor));

                // Tectonic magma fissures near the Breach (color.r + Breach proximity + Voronoi cracks)
                float distToBreach = length(posWS.xz - _BreachCenterWS.xz);
                float breachProximity = saturate(1.0 - distToBreach / max(15.0, _BreachCenterWS.w));
                float2 fissureVoro = SeabedVoronoiEdge(posWS.xz * 0.12 + float2(sin(posWS.z * 0.05), cos(posWS.x * 0.05)) * 0.6);
                float voroCrack = 1.0 - smoothstep(0.01, 0.085, fissureVoro.y);

                float bakedFissure = saturate(input.color.r);
                float fissureStrength = saturate(max(bakedFissure * 1.15, voroCrack * breachProximity * 0.92));

                float pulseWave = 0.78 + 0.28 * sin(_Time.y * 2.6 + distToBreach * 0.12 + fissureVoro.x * 4.0);
                float coreHeat = smoothstep(0.32, 0.92, fissureStrength);
                half3 fissureCol = lerp(_FissureColorA.rgb, _FissureColorB.rgb, (half)coreHeat);
                half3 emissive = fissureCol * (half3)(fissureStrength * pulseWave);

                // Direct & additional volumetric lights (Gipsy floodlights + Breach underglow lights)
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(posWS);
                half3 diffuseColor = albedo * 0.96h;
                half3 F0 = half3(0.04h, 0.04h, 0.04h);
                half smoothness = lerp(_Smoothness * 0.75h, _Smoothness, (half)rockFactor);
                float perceptualRoughness = saturate(1.0 - smoothness);
                float roughness = max(perceptualRoughness * perceptualRoughness, 0.04);
                float a2 = roughness * roughness;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(posWS));
                half3 mainRadiance = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation);
                half3 directLighting = EvaluateDirectGGX(normalWS, viewDirWS, normalize(mainLight.direction), mainRadiance, diffuseColor, F0, a2);

                #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP) || defined(_FORWARD_PLUS)
                InputData inputData = (InputData)0;
                inputData.positionWS = posWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                uint addLightsCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(addLightsCount)
                    Light addLight = GetAdditionalLight(lightIndex, posWS);
                    half3 addRadiance = addLight.color * (addLight.distanceAttenuation * addLight.shadowAttenuation);
                    if (dot(addRadiance, addRadiance) > 0.00001h)
                    {
                        directLighting += EvaluateDirectGGX(normalWS, viewDirWS, normalize(addLight.direction), addRadiance, diffuseColor, F0, a2);
                    }
                LIGHT_LOOP_END
                #endif

                half3 sh = SampleSH(normalWS);
                half3 indirectLighting = diffuseColor * sh;

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
