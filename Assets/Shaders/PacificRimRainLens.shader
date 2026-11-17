Shader "PacificRim/RainLens"
{
    Properties
    {
        _RainNormalMap("Rain Normal Map", 2D) = "bump" {}
        _DistortionStrength("Lens Distortion", Range(0.0, 0.15)) = 0.0075
        _DropletIntensity("Droplet Density", Range(0.0, 2.0)) = 1.0
        _StreakIntensity("Downward Streaks Intensity", Range(0.0, 2.0)) = 1.2
        _ScrollSpeed("Droplet Run Speed", Float) = 0.75
        _StreakSpeed("Streak Dripping Speed", Float) = 1.35
        _WetAlpha("Base Wet Opacity", Range(0.0, 1.0)) = 0.18
        _SplashImpactAmount("Splash Impact Amount", Range(0.0, 2.0)) = 0.0
        _SplashImpactAge("Splash Impact Age (Seconds)", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+400"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ScreenSpaceRainLens"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_RainNormalMap);
            SAMPLER(sampler_RainNormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _RainNormalMap_ST;
                float  _DistortionStrength;
                float  _DropletIntensity;
                float  _StreakIntensity;
                float  _ScrollSpeed;
                float  _StreakSpeed;
                float  _WetAlpha;
                float  _SplashImpactAmount;
                float  _SplashImpactAge;
            CBUFFER_END

            float  _GlobalRainWetness;
            float4 _GlobalWindVector;
            float  _GlobalLightningFlash;
            float  _CameraLensRainOverride;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            void EvaluateSplashBeads(float2 screenUV, float2 gridScale, float seedOffset, float age, float aspect, out float2 gradOut, out float maskOut)
            {
                // Gravity-driven slide down the glass lens after impact (starts brisk, settles into slow ooze)
                float slideDist = (1.0 - exp(-age * 0.55)) * 0.34 + age * 0.028;
                float2 uvShifted = screenUV + float2(0.0, slideDist);
                float2 st = uvShifted * gridScale;
                float2 cellId = floor(st);
                float2 cellLocal = frac(st) - 0.5;

                float r1 = Hash21(cellId + seedOffset);
                float r2 = Hash21(cellId * 1.37 + seedOffset + 19.7);
                float r3 = Hash21(cellId * 2.13 + seedOffset + 41.3);

                // Keep ~55% of grid cells populated with varied splash droplets
                float keep = step(0.45, r1);
                float2 centerOff = float2((r2 - 0.5) * 0.45, (r3 - 0.5) * 0.40);
                float2 d = cellLocal - centerOff;
                d.x *= (aspect / (gridScale.x / gridScale.y));

                // Teardrop bead head + vertical water trail directly above each sliding droplet
                float radius = lerp(0.10, 0.24, r2);
                float headDist = length(float2(d.x, d.y * 0.85));
                float headShape = smoothstep(radius, radius * 0.15, headDist) * keep;

                float trailLen = clamp(slideDist * gridScale.y * lerp(0.7, 1.5, r3), 0.02, 0.85);
                float inTrailY = smoothstep(-0.02, 0.04, d.y) * smoothstep(trailLen, 0.04, d.y);
                float trailWidth = radius * 0.42 * (1.0 - saturate(d.y / max(0.05, trailLen)) * 0.55);
                float trailShape = smoothstep(trailWidth, trailWidth * 0.15, abs(d.x)) * inTrailY * keep * 0.65;

                float combined = max(headShape, trailShape);
                float2 headNormal = -d / max(0.02, radius);
                float2 trailNormal = float2(-d.x / max(0.02, trailWidth) * 0.5, 0.25);

                gradOut = lerp(trailNormal * trailShape, headNormal * headShape, step(trailShape, headShape)) * 1.45;
                maskOut = combined;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float wetness = saturate(max(_GlobalRainWetness, _CameraLensRainOverride));
                if (wetness <= 0.001 && _SplashImpactAmount <= 0.001) return half4(0.0, 0.0, 0.0, 0.0);

                float t = _Time.y;
                float windTilt = _GlobalWindVector.x * 0.32;

                // 1. Layer 1: Medium condensation and beading droplets
                float2 dropUV1 = screenUV * float2(3.2, 2.0) + float2(-windTilt * t * 0.4, t * _ScrollSpeed * 0.35);
                float3 nDrop1 = UnpackNormal(SAMPLE_TEXTURE2D(_RainNormalMap, sampler_RainNormalMap, dropUV1));

                // 2. Layer 2: Fast-sliding fine rain droplets
                float2 dropUV2 = screenUV * float2(6.0, 3.8) + float2(-windTilt * t * 0.8 + 0.43, t * _ScrollSpeed * 0.85 + 0.17);
                float3 nDrop2 = UnpackNormal(SAMPLE_TEXTURE2D(_RainNormalMap, sampler_RainNormalMap, dropUV2));

                // 3. Layer 3: Downward dripping streaks (vertical rivulets running down the lens)
                float streakCol = floor(screenUV.x * 18.0);
                float streakRnd = Hash21(float2(streakCol, 17.3));
                float streakActive = step(0.35, streakRnd);

                float streakTime = t * _StreakSpeed * (0.65 + streakRnd * 0.7);
                float2 streakUV = float2(
                    frac(screenUV.x * 18.0) - 0.5,
                    frac(screenUV.y * 1.5 - streakTime)
                );

                float beadShape = smoothstep(0.35, 0.02, abs(streakUV.x)) * smoothstep(0.0, 0.25, streakUV.y) * smoothstep(1.0, 0.6, streakUV.y);
                float3 nStreak = float3(
                    streakUV.x * 2.8 * beadShape * streakActive,
                    (streakUV.y - 0.5) * 2.2 * beadShape * streakActive,
                    1.0
                );

                float3 nCombined = normalize(float3(
                    (nDrop1.xy * 0.7 + nDrop2.xy * 0.5) * _DropletIntensity + nStreak.xy * _StreakIntensity,
                    1.0
                ));

                float splashMask = 0.0;
                float2 splashGrad = float2(0.0, 0.0);
                if (_SplashImpactAmount > 0.001)
                {
                    float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);
                    float2 gLarge, gMed;
                    float mLarge, mMed;
                    EvaluateSplashBeads(screenUV, float2(5.5, 3.4), 7.1, _SplashImpactAge, aspect, gLarge, mLarge);
                    EvaluateSplashBeads(screenUV, float2(10.5, 6.2), 23.4, _SplashImpactAge * 1.18, aspect, gMed, mMed);
                    splashMask = saturate(max(mLarge, mMed * 0.85)) * saturate(_SplashImpactAmount);
                    splashGrad = (gLarge * mLarge + gMed * mMed * 0.75) * _SplashImpactAmount;
                    nCombined = normalize(nCombined + float3(splashGrad * 1.85, 0.0));
                }

                float distortionMag = length(nCombined.xy);
                float dropletMask = saturate(smoothstep(0.06, 0.28, distortionMag) + splashMask * 0.95);

                float2 offsetBase = nCombined.xy * (_DistortionStrength * max(wetness, saturate(_SplashImpactAmount)) + splashMask * 0.022 * _SplashImpactAmount);
                float2 uvR = saturate(screenUV + offsetBase * 1.02);
                float2 uvG = saturate(screenUV + offsetBase * 1.00);
                float2 uvB = saturate(screenUV + offsetBase * 0.98);

                half3 sceneR = SampleSceneColor(uvR);
                half3 sceneG = SampleSceneColor(uvG);
                half3 sceneB = SampleSceneColor(uvB);
                half3 refractedCol = half3(sceneR.r, sceneG.g, sceneB.b);

                float specular = pow(saturate(dot(nCombined, normalize(float3(-0.3, 0.8, 0.5)))), 8.0);
                float rimBead = pow(saturate(length(splashGrad)), 1.4) * splashMask * 0.35;
                float lightningGlint = _GlobalLightningFlash * pow(saturate(nCombined.y * 0.6 + 0.4), 3.0) * 0.25;

                half3 glintCol = half3(0.78, 0.93, 1.0) * (specular * 0.09 + rimBead + lightningGlint);
                refractedCol += glintCol;

                float2 centerDist = screenUV * 2.0 - 1.0;
                float edgeVignette = smoothstep(0.2, 1.4, dot(centerDist, centerDist));
                float rainAlpha = dropletMask * (_WetAlpha * (1.0 + edgeVignette * 0.85)) * wetness;
                float splashAlpha = splashMask * 0.62;
                float finalAlpha = saturate(max(rainAlpha, splashAlpha));

                return half4(refractedCol, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
