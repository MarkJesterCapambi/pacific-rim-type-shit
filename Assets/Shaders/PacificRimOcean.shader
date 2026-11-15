Shader "PacificRim/OceanWater"
{
    Properties
    {
        _DeepColor("Abyssal Ocean Color", Color) = (0.008, 0.032, 0.068, 1.0)
        _ShallowColor("Subsurface Scatter Tint", Color) = (0.025, 0.19, 0.28, 1.0)
        _SubsurfaceColor("Wave Crest Scatter Glow", Color) = (0.05, 0.34, 0.38, 1.0)
        _FoamColor("Meniscus & Seafoam Color", Color) = (0.86, 0.92, 0.96, 1.0)
        _AbsorptionScale("Depth Absorption Scale", Range(0.05, 2.0)) = 0.35
        _RefractionStrength("Refraction Distortion", Range(0.0, 0.1)) = 0.025
        _Smoothness("Surface Smoothness", Range(0.1, 1.0)) = 0.95
        _MicroNormalStrength("Capillary Ripple Strength", Range(0.0, 0.5)) = 0.18
        _MeniscusStrength("Shore/Leg Meniscus Strength", Range(0.0, 1.5)) = 0.45
        _SpecularColor("Specular Glint Color", Color) = (0.95, 0.98, 1.0, 1.0)
        _NormalMap("Turbulence Normal Map", 2D) = "bump" {}
        _NormalTiling("Turbulence Tiling", Float) = 0.06
        _FoamMap("Foam Mask Texture", 2D) = "white" {}
        _FoamTiling("Foam Tiling", Float) = 0.12
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float3 positionWS    : TEXCOORD0;
                float3 normalWS      : TEXCOORD1;
                float  waveElevation : TEXCOORD2;
                float  wakeFactor    : TEXCOORD3;
                float  fogCoord      : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _ShallowColor;
                float4 _SubsurfaceColor;
                float4 _FoamColor;
                float  _AbsorptionScale;
                float  _RefractionStrength;
                float  _Smoothness;
                float  _MicroNormalStrength;
                float  _MeniscusStrength;
                float4 _SpecularColor;
                float  _NormalTiling;
                float  _FoamTiling;
            CBUFFER_END

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamMap);
            SAMPLER(sampler_FoamMap);

            // Global uniforms pushed once per frame from WaterSystem & OceanWaterMesh (outside UnityPerMaterial for SRP Batcher)
            float4 _OceanSwell1;
            float4 _OceanSwell1Dir;
            float4 _OceanSwell2;
            float4 _OceanSwell2Dir;
            float4 _OceanSwell3;
            float4 _OceanSwell3Dir;
            float4 _OceanHeightLimits;
            float4 _OceanWakePositions[16];
            float4 _OceanWakeParams[16];
            float4 _JaegerLeftLegWS;
            float4 _JaegerRightLegWS;
            float4 _JaegerVelocityWS;
            float4 _KaijuPositionWS;
            float4 _KaijuVelocityWS;
            float4 _GlobalWindVector;
            float  _GlobalRainWetness;
            float  _GlobalLightningFlash;

            // Continuous 2D turbulence warp that organically bends wave ridges,
            // preventing straight parallel lines & destroying diamond cushion patterns
            float EvaluateCurlWarp(float2 p, float t)
            {
                return sin(p.x * 0.026 + p.y * 0.038 + t * 0.21) * 1.55
                     + cos(p.x * 0.034 - p.y * 0.029 - t * 0.17) * 1.25
                     + sin(p.x * 0.052 + p.y * 0.018 + t * 0.31) * 0.70;
            }

            // Dynamic 2D constructive wave packet envelope: localized swell groups stay mostly in
            // normal knee-to-pelvis wading range (~85%) and rarely surge to upper-torso height (~10-12%)
            float EvaluateSelfRisingEnvelope(float2 p, float t, float boost)
            {
                if (boost <= 0.001) return 1.0;
                float p1 = sin(0.024 * (p.x * 0.91 + p.y * 0.41) - 0.42 * t);
                float p2 = cos(0.017 * (-p.x * 0.48 + p.y * 0.88) - 0.31 * t + 1.6);
                float p3 = sin(0.011 * (p.x * 0.65 - p.y * 0.76) - 0.23 * t + 0.9);
                float group = saturate(0.5 + 0.5 * (p1 * 0.55 + p2 * 0.30 + p3 * 0.15));

                float rogue = saturate((group - 0.58) / 0.42);
                float rareEnvelope = rogue * rogue * rogue * (rogue * (rogue * 6.0 - 15.0) + 10.0);
                return lerp(0.75, 1.0 + boost, rareEnvelope);
            }

            void AccumulateGerstnerWave(
                float amp, float wavelength, float speed, float steepness, float2 dir,
                float warp, float2 worldXZ, float t,
                inout float3 disp, inout float3 tangentX, inout float3 binormalZ)
            {
                if (amp <= 0.0001) return;

                float k = 6.2831853 / max(wavelength, 1.0);
                float w = sqrt(9.81 * k) * speed;
                float phase = k * dot(dir, worldXZ) - w * t + warp;
                float sinP = sin(phase);
                float cosP = cos(phase);

                float qa = steepness * amp;
                disp.y += amp * sinP;
                disp.x += dir.x * qa * cosP;
                disp.z += dir.y * qa * cosP;

                float kA = k * amp;
                float qkA = steepness * kA;

                tangentX.x -= dir.x * dir.x * qkA * sinP;
                tangentX.y += dir.x * kA * cosP;
                tangentX.z -= dir.x * dir.y * qkA * sinP;

                binormalZ.x -= dir.x * dir.y * qkA * sinP;
                binormalZ.y += dir.y * kA * cosP;
                binormalZ.z -= dir.y * dir.y * qkA * sinP;
            }

            float EvaluateWakes(float2 worldXZ, float t, out float slopeX, out float slopeZ, out float wakeEnergy)
            {
                float sum = 0.0;
                slopeX = 0.0;
                slopeZ = 0.0;
                wakeEnergy = 0.0;

                [unroll]
                for (int i = 0; i < 16; i++)
                {
                    float4 posTime = _OceanWakePositions[i];
                    float4 p = _OceanWakeParams[i];
                    float age = t - posTime.w;
                    float normAge = age * p.w;

                    if (age > 0.0 && normAge < 1.0 && abs(p.y) > 0.0001)
                    {
                        float timeDecay = (1.0 - normAge);
                        float timeDecaySq = timeDecay * timeDecay;

                        float2 dXZ = worldXZ - posTime.xz;
                        float distSq = dot(dXZ, dXZ);
                        float dist = sqrt(max(distSq, 0.0001));

                        float radius = max(p.x, 2.0);
                        float waveFront = age * p.z;
                        float ringWidth = max(radius * 0.45, 2.2);
                        float u = (dist - waveFront) / ringWidth;

                        // Multi-harmonic propagating ripple packet with realistic spatial attenuation
                        float gauss = exp(-u * u) * exp(-dist / max(radius * 3.0, 8.0));
                        float cosTerm = cos(u * 3.4);
                        float ampTime = p.y * timeDecaySq;

                        sum += cosTerm * gauss * ampTime;
                        wakeEnergy += gauss * timeDecay * abs(p.y);

                        // Exact radial slope derivative for dynamic specular and normal glints on ripple crests
                        float dCosDu = (-3.4 * sin(u * 3.4) - 2.0 * u * cosTerm) * (gauss / ringWidth);
                        float2 dir = dXZ / dist;
                        float slopeMag = dCosDu * ampTime;
                        slopeX += dir.x * slopeMag;
                        slopeZ += dir.y * slopeMag;
                    }
                }

                return sum;
            }

            float2 BubbleHash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Animated cellular distance field: small values = bubble centers
            float BubbleCells(float2 uv, float t)
            {
                float2 g = floor(uv);
                float2 f = frac(uv);
                float m = 8.0;
                [unroll]
                for (int j = -1; j <= 1; j++)
                {
                    [unroll]
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 cell = float2(i, j);
                        float2 o = BubbleHash(g + cell);
                        o = 0.5 + 0.45 * sin(t + 6.2831853 * o);
                        float2 r = cell + o - f;
                        m = min(m, dot(r, r));
                    }
                }
                return m;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 worldBase = TransformObjectToWorld(input.positionOS.xyz);

                bool globalsActive = _OceanHeightLimits.w > 0.5;
                float t = globalsActive ? _OceanHeightLimits.z : _Time.y;
                float maxTotal = globalsActive ? max(_OceanHeightLimits.y, 0.25) : 6.60;
                float maxTrough = globalsActive ? max(_OceanHeightLimits.w, 0.55) : 2.25;
                float maxRipple = globalsActive ? max(_OceanHeightLimits.x, 0.05) : 0.22;

                float4 sw1 = globalsActive ? _OceanSwell1 : float4(1.0, 52.0, 1.30, 0.34);
                float4 sw1d = globalsActive ? _OceanSwell1Dir : float4(0.952, 0.305, 0.06, 6.0);
                float4 sw2 = globalsActive ? _OceanSwell2 : float4(0.50, 31.0, 1.50, 0.28);
                float4 sw2d = globalsActive ? _OceanSwell2Dir : float4(0.577, 0.816, 2.3, 7.0);
                float4 sw3 = globalsActive ? _OceanSwell3 : float4(0.30, 16.0, 1.80, 0.22);
                float4 sw3d = globalsActive ? _OceanSwell3Dir : float4(-0.783, 0.622, 2.3, 230.0);

                float3 disp = float3(0.0, 0.0, 0.0);
                float3 tangentX = float3(1.0, 0.0, 0.0);
                float3 binormalZ = float3(0.0, 0.0, 1.0);

                float2 swell1Dir = normalize(sw1d.xy);
                float warp = EvaluateCurlWarp(worldBase.xz, t);
                float boost = globalsActive ? sw3d.z : 3.10;
                float selfRise = EvaluateSelfRisingEnvelope(worldBase.xz, t, boost);
                float steepScale = rsqrt(max(1.0, selfRise));

                // 8 incommensurate wave trains with spatial curl turbulence & self-rising swell groups
                // Wave 1: Heavy Deep Ground Swell (surges tall in rare rogue wave groups)
                AccumulateGerstnerWave(sw1.x * selfRise, sw1.y, sw1.z, sw1.w * steepScale, swell1Dir, warp * 0.90, worldBase.xz, t, disp, tangentX, binormalZ);

                // Wave 2: Storm Crest Swell (rises with rare rogue wave sets)
                AccumulateGerstnerWave(sw2.x * selfRise * 0.85, sw2.y, sw2.z, sw2.w * steepScale, normalize(sw2d.xy), warp * 0.75, worldBase.xz, t, disp, tangentX, binormalZ);

                // Wave 3: Secondary Cross Swell
                AccumulateGerstnerWave(sw3.x, sw3.y, sw3.z, sw3.w, normalize(sw3d.xy), warp * 0.60, worldBase.xz, t, disp, tangentX, binormalZ);

                // Wave 4: Oblique Counter Sea
                AccumulateGerstnerWave(0.38, 33.0, 1.45, 0.38, float2(0.978, -0.208), warp * 0.50, worldBase.xz, t, disp, tangentX, binormalZ);

                // Wave 5: Intermediate Wind Sea
                AccumulateGerstnerWave(0.26, 21.5, 1.62, 0.34, float2(0.743, 0.669), warp * 0.40, worldBase.xz, t, disp, tangentX, binormalZ);

                // Wave 6: Short Choppy Sea
                AccumulateGerstnerWave(0.18, 14.8, 1.82, 0.30, float2(0.766, -0.643), warp * 0.30, worldBase.xz, t, disp, tangentX, binormalZ);

                // Wave 7: Fine Wind Wave
                AccumulateGerstnerWave(0.12, 9.6,  2.08, 0.25, float2(0.995, 0.105), warp * 0.20, worldBase.xz, t, disp, tangentX, binormalZ);

                // Wave 8: Capillary Chop
                AccumulateGerstnerWave(0.07, 5.4,  2.42, 0.20, float2(0.819, -0.574), warp * 0.15, worldBase.xz, t, disp, tangentX, binormalZ);

                // Crest sharpening & trough deepening:
                // Sharp pinched oceanic crests while deepening normal troughs toward knee level
                if (disp.y > 0.0)
                {
                    float crestFactor = saturate(disp.y / 2.2);
                    float sharpScale = 1.0 + 0.30 * crestFactor;
                    disp.y *= sharpScale;
                    tangentX.y *= sharpScale;
                    binormalZ.y *= sharpScale;
                }
                else
                {
                    disp.y *= 1.30;
                    tangentX.y *= 1.30;
                    binormalZ.y *= 1.30;
                }

                // Propagating wakes & ripples from movement, footsteps and impacts
                float wSlopeX, wSlopeZ, wakeEnergy;
                float wakeH = globalsActive ? EvaluateWakes(worldBase.xz, t, wSlopeX, wSlopeZ, wakeEnergy) : 0.0;
                float cappedWake = clamp(tanh(wakeH / maxRipple) * maxRipple, -maxRipple, maxRipple);
                disp.y += cappedWake;
                tangentX.y += wSlopeX * 0.75;
                binormalZ.y += wSlopeZ * 0.75;

                // Dynamic Real-time Mesh Deformation: Localized cavitation and surging bow waves under moving colossal limbs
                // 1. Jaeger Left Leg
                if (_JaegerLeftLegWS.w > 0.01)
                {
                    float2 dLeft = worldBase.xz - _JaegerLeftLegWS.xz;
                    float distL = length(dLeft);
                    float rL = max(_JaegerLeftLegWS.w, 2.2);
                    if (distL < rL * 3.4)
                    {
                        float uL = distL / rL;
                        float cavL = -exp(-uL * uL * 1.5) * 1.35;

                        float vSpdL = _JaegerVelocityWS.w;
                        float2 vDirL = normalize(_JaegerVelocityWS.xz + float2(0.0001, 0.0001));
                        float fwdDotL = dot(normalize(dLeft + float2(0.0001, 0.0001)), vDirL);
                        float bowL = 0.0;
                        if (vSpdL > 0.5 && fwdDotL > 0.12)
                        {
                            float bowDistL = length(worldBase.xz - (_JaegerLeftLegWS.xz + vDirL * rL * 1.0));
                            float bowUL = bowDistL / (rL * 1.5);
                            bowL = exp(-bowUL * bowUL * 1.9) * saturate(fwdDotL * 1.5) * min(vSpdL * 0.24, 1.85);
                        }
                        disp.y += (cavL + bowL);
                        float2 nrmDirL = dLeft / (distL + 0.01);
                        tangentX.y += nrmDirL.x * (cavL * -1.6 + bowL * 1.4);
                        binormalZ.y += nrmDirL.y * (cavL * -1.6 + bowL * 1.4);
                    }
                }

                // 2. Jaeger Right Leg
                if (_JaegerRightLegWS.w > 0.01)
                {
                    float2 dRight = worldBase.xz - _JaegerRightLegWS.xz;
                    float distR = length(dRight);
                    float rR = max(_JaegerRightLegWS.w, 2.2);
                    if (distR < rR * 3.4)
                    {
                        float uR = distR / rR;
                        float cavR = -exp(-uR * uR * 1.5) * 1.35;

                        float vSpdR = _JaegerVelocityWS.w;
                        float2 vDirR = normalize(_JaegerVelocityWS.xz + float2(0.0001, 0.0001));
                        float fwdDotR = dot(normalize(dRight + float2(0.0001, 0.0001)), vDirR);
                        float bowR = 0.0;
                        if (vSpdR > 0.5 && fwdDotR > 0.12)
                        {
                            float bowDistR = length(worldBase.xz - (_JaegerRightLegWS.xz + vDirR * rR * 1.0));
                            float bowUR = bowDistR / (rR * 1.5);
                            bowR = exp(-bowUR * bowUR * 1.9) * saturate(fwdDotR * 1.5) * min(vSpdR * 0.24, 1.85);
                        }
                        disp.y += (cavR + bowR);
                        float2 nrmDirR = dRight / (distR + 0.01);
                        tangentX.y += nrmDirR.x * (cavR * -1.6 + bowR * 1.4);
                        binormalZ.y += nrmDirR.y * (cavR * -1.6 + bowR * 1.4);
                    }
                }

                // 3. Kaiju Body / Mass Water Displacement
                if (_KaijuPositionWS.w > 0.01)
                {
                    float2 dKaiju = worldBase.xz - _KaijuPositionWS.xz;
                    float distK = length(dKaiju);
                    float rK = max(_KaijuPositionWS.w, 4.0);
                    if (distK < rK * 3.2)
                    {
                        float uK = distK / rK;
                        float cavK = -exp(-uK * uK * 1.2) * 1.6;

                        float vSpdK = _KaijuVelocityWS.w;
                        float2 vDirK = normalize(_KaijuVelocityWS.xz + float2(0.0001, 0.0001));
                        float fwdDotK = dot(normalize(dKaiju + float2(0.0001, 0.0001)), vDirK);
                        float bowK = 0.0;
                        if (vSpdK > 0.5 && fwdDotK > 0.12)
                        {
                            float bowDistK = length(worldBase.xz - (_KaijuPositionWS.xz + vDirK * rK * 1.0));
                            float bowUK = bowDistK / (rK * 1.6);
                            bowK = exp(-bowUK * bowUK * 1.7) * saturate(fwdDotK * 1.4) * min(vSpdK * 0.28, 2.3);
                        }
                        disp.y += (cavK + bowK);
                        float2 nrmDirK = dKaiju / (distK + 0.01);
                        tangentX.y += nrmDirK.x * (cavK * -1.5 + bowK * 1.3);
                        binormalZ.y += nrmDirK.y * (cavK * -1.5 + bowK * 1.3);
                    }
                }

                // Asymmetric soft-limit: deep ceiling for rare rogue crests (+maxTotal), knee-level floor for troughs (-maxTrough)
                float activeLimit = disp.y >= 0.0 ? maxTotal : maxTrough;
                float scaledY = disp.y / activeLimit;
                float softY = clamp(tanh(scaledY) * activeLimit, -maxTrough, maxTotal);
                float derivScale = max(0.05, 1.0 - (softY / activeLimit) * (softY / activeLimit));
                tangentX.y *= derivScale;
                binormalZ.y *= derivScale;

                float horizCap = min(maxTotal * 0.45, 2.4);
                disp.xz = clamp(disp.xz, -horizCap, horizCap);
                disp.y = softY;

                float3 finalWorldPos = worldBase + disp;
                float3 nrm = normalize(cross(binormalZ, tangentX));

                output.positionWS = finalWorldPos;
                output.normalWS = nrm;
                output.positionCS = TransformWorldToHClip(finalWorldPos);
                output.waveElevation = softY;
                output.wakeFactor = saturate(wakeEnergy * 3.0);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // 6 non-orthogonal, incommensurate-wavelength directional capillary wave trains (zero square grid lattices)
            float3 EvaluateCapillaryNormal(float2 xz, float t, float strength)
            {
                float2 windShift = _GlobalWindVector.xy * (t * 0.35);
                float2 p = xz + windShift;

                // Directional unit vectors spread around the primary storm swell heading (no 90-degree pairs)
                const float2 d1 = float2( 0.961,  0.276); // +16 deg
                const float2 d2 = float2( 0.848, -0.530); // -32 deg
                const float2 d3 = float2( 0.682,  0.731); // +47 deg
                const float2 d4 = float2( 0.990, -0.139); //  -8 deg
                const float2 d5 = float2( 0.788,  0.616); // +38 deg
                const float2 d6 = float2( 0.927, -0.375); // -22 deg

                // Gentle cross-crest phase warp so wave ridges curve organically instead of forming straight lines
                float warpA = sin(dot(p, float2(-0.35, 0.93)) * 0.19 + t * 0.41) * 0.65;
                float warpB = cos(dot(p, float2( 0.52, 0.85)) * 0.31 - t * 0.53) * 0.55;

                // Incommensurate spatial frequencies (wavelengths: ~19m, 11.4m, 6.8m, 4.1m, 2.5m, 1.55m)
                float w1 = cos(dot(p, d1) * 0.33 - t * 0.95 + warpA) * 0.34;
                float w2 = cos(dot(p, d2) * 0.55 - t * 1.25 + warpB) * 0.26;
                float w3 = cos(dot(p, d3) * 0.92 - t * 1.65 - warpA) * 0.19;
                float w4 = cos(dot(p, d4) * 1.53 - t * 2.15 + warpB) * 0.13;

                float rainBoost = 1.0 + saturate(_GlobalRainWetness) * 0.25;
                float w5 = cos(dot(p, d5) * 2.48 - t * 2.85 - warpB) * (0.08 * rainBoost);
                float w6 = cos(dot(p, d6) * 4.05 + t * 3.40 + warpA) * (0.05 * rainBoost);

                float2 slope = d1 * w1 + d2 * w2 + d3 * w3 + d4 * w4 + d5 * w5 + d6 * w6;
                return normalize(float3(-slope.x * strength, 1.0, -slope.y * strength));
            }

            float GetLinearSceneDepth(float rawDepth)
            {
                float persp = LinearEyeDepth(rawDepth, _ZBufferParams);
                #if UNITY_REVERSED_Z
                    float z01 = 1.0 - rawDepth;
                #else
                    float z01 = rawDepth;
                #endif
                float ortho = lerp(_ProjectionParams.y, _ProjectionParams.z, z01);
                return lerp(persp, ortho, unity_OrthoParams.w);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 cameraPos = GetCameraPositionWS();
                float3 viewVec = cameraPos - input.positionWS;
                float camDist = length(viewVec);
                float3 viewDirWS = viewVec / max(camDist, 0.0001);

                // Fade micro-ripples cleanly with distance so far waves stay smooth
                float t = _Time.y;
                float distFade = lerp(0.18, 1.0, exp(-camDist * 0.012));
                float3 macroN = normalize(input.normalWS);
                float3 microN = EvaluateCapillaryNormal(input.positionWS.xz, t, _MicroNormalStrength * distFade);

                float2 normUV1 = input.positionWS.xz * _NormalTiling + _GlobalWindVector.xy * (t * 0.04);
                float2 normUV2 = input.positionWS.xz * (_NormalTiling * 2.1) - _GlobalWindVector.xy * (t * 0.06);
                half4 nSample1 = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normUV1);
                half4 nSample2 = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normUV2);
                float3 n1 = UnpackNormal(nSample1);
                float3 n2 = UnpackNormal(nSample2);
                float3 texN = normalize(float3(n1.xy + n2.xy, n1.z));

                float3 normalWS = normalize(float3(macroN.x + microN.x + texN.x * 0.35 * distFade, macroN.y, macroN.z + microN.z + texN.y * 0.35 * distFade));
                if (dot(normalWS, viewDirWS) < 0.0)
                {
                    normalWS = -normalWS;
                }

                float NdotV = saturate(dot(normalWS, viewDirWS));

                // Screen depth & submerged geometry thickness
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float surfaceEyeDepth = GetLinearSceneDepth(input.positionCS.z);
                float rawSceneDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = GetLinearSceneDepth(rawSceneDepth);
                float waterDepth = max(0.0, sceneEyeDepth - surfaceEyeDepth);

                // Refracted opaque scene sample with foreground artifact guard
                float2 refractOffset = normalWS.xz * _RefractionStrength * saturate(waterDepth * 0.35);
                float2 refractedUV = saturate(screenUV + refractOffset);
                float refractedEyeDepth = GetLinearSceneDepth(SampleSceneDepth(refractedUV));
                if (refractedEyeDepth < surfaceEyeDepth)
                {
                    refractedUV = screenUV;
                    refractedEyeDepth = sceneEyeDepth;
                }
                float refractedThickness = max(0.0, refractedEyeDepth - surfaceEyeDepth);

                half3 underwaterCol = SampleSceneColor(refractedUV);

                // Beer-Lambert depth absorption (red absorbs fastest, blue-teal penetrates deeper)
                float3 extinction = float3(0.42, 0.16, 0.09) * _AbsorptionScale;
                float3 transmittance = exp(-refractedThickness * extinction);

                // Smoothly blend between shallow tint & deep abyssal ocean volume color
                float depthMix = saturate(1.0 - exp(-refractedThickness * _AbsorptionScale * 0.22));
                half3 waterVolume = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthMix);

                // Directional Light & Wave Crest Subsurface Scattering
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 lightDir = normalize(mainLight.direction);
                float backLight = pow(saturate(dot(viewDirWS, -lightDir + normalWS * 0.45)), 3.0);
                float crestFactor = saturate(input.waveElevation * 0.30);
                half3 sss = _SubsurfaceColor.rgb * mainLight.color * (backLight * crestFactor * 0.9 + crestFactor * 0.25);

                // Additional Spotlights / Searchlights (Chinook patrols, Jaeger visor & chest lights)
                float specPower = exp2(9.5 * _Smoothness + 1.0);
                half3 addDiffuseLit = half3(0.0, 0.0, 0.0);
                half3 addGlint = half3(0.0, 0.0, 0.0);
                half3 addUnderwaterLit = half3(0.0, 0.0, 0.0);
                half3 addFoamLit = half3(0.0, 0.0, 0.0);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = screenUV;

                #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP) || defined(_FORWARD_PLUS)
                uint addLightsCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(addLightsCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                    half3 addLightColor = addLight.color * (addLight.distanceAttenuation * addLight.shadowAttenuation);

                    if (dot(addLightColor, addLightColor) > 0.00001)
                    {
                        float3 addLightDir = normalize(addLight.direction);
                        float addNdotL = saturate(dot(normalWS, addLightDir));
                        float addBackLight = pow(saturate(dot(viewDirWS, -addLightDir + normalWS * 0.45)), 3.0);

                        // 1. Water volume subsurface scattering & diffuse body (water visibly lights up)
                        half3 addSSS = _SubsurfaceColor.rgb * addLightColor * (addBackLight * crestFactor * 1.6 + crestFactor * 0.45);
                        addDiffuseLit += waterVolume * (addLightColor * (addNdotL * 0.75 + 0.25)) + addSSS;

                        // 2. Light penetrates into ocean column, illuminating the underwater depth and seabed underneath
                        addUnderwaterLit += underwaterCol * addLightColor * (2.2 * transmittance + 0.5 * depthMix);

                        // 3. Specular glitter & broad glancing sheen across wave ripples
                        float3 addHalfDir = normalize(addLightDir + viewDirWS);
                        float addNdotH = saturate(dot(normalWS, addHalfDir));
                        float addSpec = pow(addNdotH, specPower) * 2.2;
                        float addBroad = pow(addNdotH, max(1.0, specPower * 0.12)) * 0.5;
                        addGlint += min(half3(2.5, 2.5, 2.5), (addSpec + addBroad) * _SpecularColor.rgb * addLightColor);

                        // 4. Whitecaps, seafoam, and wakes lit by the spotlight
                        addFoamLit += _FoamColor.rgb * addLightColor * (addNdotL * 0.6 + 0.4);
                    }
                LIGHT_LOOP_END
                #endif

                half3 diffuseLitVolume = waterVolume * (mainLight.color * saturate(dot(normalWS, lightDir) * 0.55 + 0.45)) + sss + addDiffuseLit;
                half3 underwaterRefracted = underwaterCol * lerp(half3(1.0, 1.0, 1.0), _ShallowColor.rgb * 1.5, depthMix * 0.65) + addUnderwaterLit;
                half3 refractedTotal = lerp(diffuseLitVolume, underwaterRefracted, transmittance);

                // Schlick Fresnel (water F0 = 0.02) & Skybox Cubemap Environment Reflection
                float fresnel = 0.02 + 0.98 * pow(1.0 - NdotV, 5.0);
                float3 reflectDir = reflect(-viewDirWS, normalWS);
                float perceptualRoughness = saturate(1.0 - _Smoothness);
                half3 skyReflection = GlossyEnvironmentReflection(reflectDir, perceptualRoughness, 1.0h);
                skyReflection += half3(0.55, 0.72, 1.0) * (_GlobalLightningFlash * 0.85);

                // GGX-style sun specular glitter across capillary ripples
                float3 halfDir = normalize(lightDir + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float spec = pow(NdotH, specPower) * 1.85;
                half3 sunGlint = min(half3(1.35, 1.35, 1.35), spec * _SpecularColor.rgb * mainLight.color * mainLight.shadowAttenuation);

                half3 surfaceColor = lerp(refractedTotal, skyReflection, saturate(fresnel * 0.92)) + sunGlint + addGlint;

                // Subtle, physically-thin meniscus right at leg/geometry intersection only
                float intersectionDist = min(waterDepth, refractedThickness);
                float legFoam = 0.0;
                if (_JaegerLeftLegWS.w > 0.5)
                {
                    float dL = length(input.positionWS.xz - _JaegerLeftLegWS.xz);
                    float dR = length(input.positionWS.xz - _JaegerRightLegWS.xz);
                    float dLeg = min(dL, dR);
                    float legRadius = max(1.0, _JaegerRightLegWS.w * 0.55);
                    float legRing = smoothstep(legRadius + 0.35, legRadius * 0.85, dLeg) * smoothstep(legRadius * 0.45, legRadius * 0.80, dLeg);
                    legFoam = legRing * 0.22;
                }

                float contactMeniscus = smoothstep(0.14, 0.0, intersectionDist) * saturate(_MeniscusStrength) * 0.45;
                float subtleFoam = saturate(contactMeniscus + legFoam);

                half3 finalRgb = lerp(surfaceColor, _FoamColor.rgb * saturate(dot(normalWS, lightDir) * 0.4 + 0.6), subtleFoam * 0.45);

                // Bubbling whitecaps: tall self-rising crests break into cellular bubble foam, moderate crests fizz lightly
                float elev = input.waveElevation;
                float2 bubbleUV = input.positionWS.xz * 0.85 + _GlobalWindVector.xy * (t * 0.06);
                float cell = BubbleCells(bubbleUV, t * 0.7);
                float cellFine = BubbleCells(bubbleUV * 2.7 + 17.3, t * 1.1);
                float bubbles = smoothstep(0.22, 0.05, cell);
                float bubbleRim = smoothstep(0.03, 0.09, cell) * smoothstep(0.20, 0.11, cell);
                float fineFizz = smoothstep(0.16, 0.03, cellFine);

                float2 foamUV = input.positionWS.xz * _FoamTiling + _GlobalWindVector.xy * (t * 0.03);
                half foamSample = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, foamUV).r;
                bubbles = lerp(bubbles, bubbles * foamSample * 1.35, 0.65);

                float bigCrest = smoothstep(1.95, 4.8, elev);
                float minorCrest = smoothstep(0.70, 1.75, elev) * (1.0 - bigCrest * 0.6);
                float whitecap = bigCrest * saturate(bubbles * 0.85 + fineFizz * 0.55 + bubbleRim * 0.35 + 0.16);
                float fizz = minorCrest * saturate(bubbles * 0.45 + fineFizz * 0.30);
                float crestFoam = saturate(whitecap * 0.92 + fizz);

                // Effervescent cellular bubble foam on water surface along Jaeger's wake path
                float wakeFoam = saturate(input.wakeFactor * (bubbles * 0.65 + fineFizz * 0.45 + 0.20));

                half3 foamLit = _FoamColor.rgb * saturate(dot(normalWS, lightDir) * 0.35 + 0.65) + addFoamLit;
                foamLit += _FoamColor.rgb * bubbleRim * bigCrest * 0.22;
                finalRgb = lerp(finalRgb, foamLit, saturate(crestFoam + wakeFoam * 0.85));
                subtleFoam = saturate(subtleFoam + crestFoam + wakeFoam);

                finalRgb = MixFog(finalRgb, input.fogCoord);
                float finalAlpha = saturate(lerp(0.94, 0.99, fresnel + subtleFoam));

                return half4(finalRgb, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Transparent/Diffuse"
}
