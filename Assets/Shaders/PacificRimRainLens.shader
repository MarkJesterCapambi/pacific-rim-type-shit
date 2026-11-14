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

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float wetness = saturate(max(_GlobalRainWetness, _CameraLensRainOverride));
                if (wetness <= 0.001) return half4(0.0, 0.0, 0.0, 0.0);

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

                float distortionMag = length(nCombined.xy);
                float dropletMask = smoothstep(0.06, 0.28, distortionMag);

                float2 offsetBase = nCombined.xy * (_DistortionStrength * wetness);
                float2 uvR = saturate(screenUV + offsetBase * 1.02);
                float2 uvG = saturate(screenUV + offsetBase * 1.00);
                float2 uvB = saturate(screenUV + offsetBase * 0.98);

                half3 sceneR = SampleSceneColor(uvR);
                half3 sceneG = SampleSceneColor(uvG);
                half3 sceneB = SampleSceneColor(uvB);
                half3 refractedCol = half3(sceneR.r, sceneG.g, sceneB.b);

                float specular = pow(saturate(dot(nCombined, normalize(float3(-0.3, 0.8, 0.5)))), 8.0);
                float lightningGlint = _GlobalLightningFlash * pow(saturate(nCombined.y * 0.6 + 0.4), 3.0) * 0.25;

                half3 glintCol = half3(0.75, 0.92, 1.0) * (specular * 0.08 + lightningGlint);
                refractedCol += glintCol;

                float2 centerDist = screenUV * 2.0 - 1.0;
                float edgeVignette = smoothstep(0.2, 1.4, dot(centerDist, centerDist));
                float finalAlpha = saturate(dropletMask * (_WetAlpha * (1.0 + edgeVignette * 0.85)) * wetness);

                return half4(refractedCol, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
