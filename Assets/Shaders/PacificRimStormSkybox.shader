Shader "PacificRim/StormSkybox"
{
    Properties
    {
        _HorizonColor("Horizon Slate Haze", Color) = (0.07, 0.09, 0.13, 1.0)
        _ZenithColor("Zenith Dark Storm", Color) = (0.015, 0.025, 0.045, 1.0)
        _CloudDarkColor("Cloud Dark Underbelly", Color) = (0.045, 0.065, 0.095, 1.0)
        _CloudMidColor("Cloud Midtone Scatter", Color) = (0.12, 0.16, 0.22, 1.0)
        _CloudLightColor("Cloud Highlight / Rim", Color) = (0.24, 0.32, 0.42, 1.0)
        _ScudCloudColor("Fast Low Scud Clouds", Color) = (0.035, 0.050, 0.075, 1.0)
        _CloudCover("Cloud Coverage", Range(0.2, 0.95)) = 0.62
        _CloudSoftness("Cloud Edge Softness", Range(0.05, 0.6)) = 0.28
        _CloudDetailScale("Cloud Detail Scale", Range(0.5, 3.0)) = 1.35
        _CloudSpeed("Cloud Wind Speed", Range(0.5, 8.0)) = 3.2
        _CloudChurnSpeed("Cloud Churn / Boil Speed", Range(0.0, 3.0)) = 1.1
        _ScudSpeedMultiplier("Low Scud Wind Speed", Range(1.0, 6.0)) = 3.4
        _CloudParallaxDepth("Volumetric Cloud Depth", Range(0.05, 0.8)) = 0.34
        _CloudNoiseTex("Cloud Noise", 2D) = "gray" {}
        _MoonDirection("Moon Direction (XYZ)", Vector) = (0.36, 0.40, 0.84, 0.0)
        _MoonSize("Moon Angular Size", Range(0.006, 0.08)) = 0.026
        _MoonColor("Moon Disc Color", Color) = (0.88, 0.94, 1.0, 1.0)
        _MoonIntensity("Moon Brightness", Range(0.2, 6.0)) = 2.6
        _MoonHaloIntensity("Moon Cloud Scatter / Halo", Range(0.0, 3.0)) = 1.35
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            Name "StormSkybox"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir    : TEXCOORD0;
            };

            TEXTURE2D(_CloudNoiseTex);
            SAMPLER(sampler_CloudNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor;
                half4 _ZenithColor;
                half4 _CloudDarkColor;
                half4 _CloudMidColor;
                half4 _CloudLightColor;
                half4 _ScudCloudColor;
                float _CloudCover;
                float _CloudSoftness;
                float _CloudDetailScale;
                float _CloudSpeed;
                float _CloudChurnSpeed;
                float _ScudSpeedMultiplier;
                float _CloudParallaxDepth;
                float4 _CloudNoiseTex_ST;
                float4 _MoonDirection;
                float _MoonSize;
                half4 _MoonColor;
                float _MoonIntensity;
                float _MoonHaloIntensity;
            CBUFFER_END

            float4 _GlobalWindVector;
            float  _GlobalLightningFlash;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.viewDir = input.positionOS.xyz;
                return output;
            }

            float SampleCloudDensity(float2 uv, float2 windPan, float churn, out float billowOut)
            {
                float2 churnShift1 = float2(sin(churn * 0.9 + uv.y * 2.2), cos(churn * 0.7 + uv.x * 2.2)) * 0.032;
                float2 churnShift2 = float2(cos(churn * 1.1 - uv.x * 2.6), sin(churn * 0.8 - uv.y * 2.6)) * 0.032;

                float warp1 = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, uv * 0.7 + windPan * 0.6 + churnShift1).r;
                float warp2 = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, uv * 0.7 + float2(0.43, 0.27) - windPan * 0.35 + churnShift2).r;
                float2 warpedUV = uv + float2(warp1 - 0.5, warp2 - 0.5) * 0.55 + windPan;

                float nMacro = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, warpedUV * 0.65).r;
                float nMid   = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, warpedUV * 1.55 + float2(0.31, 0.19)).r;
                float nMicro = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, warpedUV * 3.60 - float2(0.18, 0.44)).r;

                float fbm = nMacro * 0.50 + nMid * 0.35 + nMicro * 0.15;
                float billow = 1.0 - abs(nMid * 2.0 - 1.0);
                billowOut = billow;

                // Fibrous edge erosion for realistic ragged cloud wisps
                float combined = fbm * 0.74 + billow * 0.26 - (1.0 - nMicro) * 0.08;
                float threshold = 1.0 - _CloudCover;
                return smoothstep(threshold - _CloudSoftness, threshold + _CloudSoftness, combined);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.viewDir);
                float elev = saturate(dir.y);

                half3 skyBase = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(elev, 0.55));

                // Moon setup (behind the cloud deck)
                float3 moonDir = normalize(_MoonDirection.xyz);
                float moonDot = dot(dir, moonDir);
                float moonAngle = acos(clamp(moonDot, -1.0, 1.0));
                float moonDisc = smoothstep(_MoonSize + 0.0022, _MoonSize - 0.0022, moonAngle);

                // Project subtle lunar maria detail onto the moon disc
                float3 moonRight = normalize(cross(float3(0.0, 1.0, 0.0), moonDir));
                float3 moonUp = cross(moonDir, moonRight);
                float2 moonLocalUV = float2(dot(dir - moonDir, moonRight), dot(dir - moonDir, moonUp)) / max(_MoonSize, 0.001) * 0.5 + 0.5;
                float lunarMare = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, moonLocalUV * 1.35 + float2(0.17, 0.31)).r;
                half3 moonSurface = _MoonColor.rgb * (half)(_MoonIntensity * lerp(0.72, 1.08, lunarMare));
                float moonHorizonMask = smoothstep(-0.02, 0.08, dir.y);

                // Clear-sky atmospheric moon halo
                float clearHalo = pow(saturate(moonDot), 28.0) * 0.25 + pow(saturate(moonDot), 180.0) * 0.55;
                skyBase += _MoonColor.rgb * (half)(clearHalo * _MoonHaloIntensity * moonHorizonMask);
                skyBase = lerp(skyBase, moonSurface, saturate(moonDisc * moonHorizonMask));

                // Curved dome projection without severe horizon pinching
                float2 skyUV = dir.xz / (sqrt(dir.y * dir.y + 0.06) + dir.y * 0.25);
                float t = _Time.y;
                float2 windDir = normalize(_GlobalWindVector.xy + float2(0.85, 0.52));
                float windSpeed = length(_GlobalWindVector.xy) > 0.1 ? length(_GlobalWindVector.xy) : 24.0;

                float cloudSpeedFactor = max(0.2, _CloudSpeed);
                float2 windPan = windDir * (t * windSpeed * 0.0032 * cloudSpeedFactor);
                float churn = t * 0.18 * _CloudChurnSpeed;

                // --- Layer 1: Volumetric Multi-Slice Ray-Marched Storm Cloud Deck ---
                const int SLICE_COUNT = 5;
                float2 baseUV = skyUV * (0.16 * _CloudDetailScale);
                float2 moonLightStep = normalize(moonDir.xz + float2(0.0001, 0.0001)) * 0.038;

                float accumDensity = 0.0;
                half3 accumColor = half3(0.0, 0.0, 0.0);
                float accumBillow = 0.0;

                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float lightScatter = pow(saturate(dot(dir, -lightDir) * 0.5 + 0.5), 3.0);
                float moonScatter = pow(saturate(moonDot), 6.0) * 0.65 + pow(saturate(moonDot), 36.0) * 1.45;

                [unroll]
                for (int i = 0; i < SLICE_COUNT; i++)
                {
                    float h = (float)i / (SLICE_COUNT - 1); // 0 = dark storm base, 1 = upper anvil/top
                    float parallaxScale = 1.0 + (0.5 - h) * _CloudParallaxDepth;
                    float2 sliceUV = baseUV * parallaxScale + windDir * (h * 0.012);

                    float sliceBillow;
                    float d = SampleCloudDensity(sliceUV, windPan, churn, sliceBillow);

                    // Directional self-shadowing toward the moon
                    float shadowBillow;
                    float dShadow = SampleCloudDensity(sliceUV + moonLightStep * (1.0 - h * 0.5), windPan, churn, shadowBillow);
                    float lightTrans = exp(-dShadow * 1.85);

                    // Height-graded cloud lighting: dark heavy underbelly -> silver-lined upper ridges
                    float heightLit = lerp(0.18, 1.0, h);
                    half3 sliceTone = lerp(_CloudDarkColor.rgb, _CloudMidColor.rgb, saturate(heightLit * 0.85 + d * 0.35));

                    float silverRim = saturate((1.0 - d * 0.65) * lightTrans * (0.45 + h * 0.55));
                    half3 highlightTone = _CloudLightColor.rgb * (half)(1.0 + lightScatter * 0.55);
                    sliceTone = lerp(sliceTone, highlightTone, saturate(silverRim * 0.78 + sliceBillow * h * 0.35));

                    // Moonlight forward-scattering / silver lining through thin cloud edges
                    half3 moonCloudGlow = _MoonColor.rgb * (half)(moonScatter * lightTrans * (1.0 - d * 0.55) * _MoonHaloIntensity * 0.65);
                    sliceTone += moonCloudGlow;

                    float sliceWeight = d * (1.0 - accumDensity) * 0.52;
                    accumColor += sliceTone * (half)sliceWeight;
                    accumDensity = saturate(accumDensity + sliceWeight);
                    accumBillow += sliceBillow * sliceWeight;
                }

                float cloudDensity = accumDensity;
                half3 cloudTone = accumColor / (half)max(cloudDensity, 0.001);

                // --- Internal Lightning Volumetric Illumination ---
                half3 lightningColor = half3(0.52h, 0.74h, 1.0h);
                half3 lightningEmissive = lightningColor * (half)(_GlobalLightningFlash * (0.35 + cloudDensity * 0.85 + accumBillow * 1.1));
                cloudTone += lightningEmissive;

                // --- Layer 2: Fast-moving Low Scud Clouds (Ragged torn shreds rushing beneath deck) ---
                float2 scudPan = windDir * (t * windSpeed * 0.0032 * cloudSpeedFactor * _ScudSpeedMultiplier);
                float2 scudUV = skyUV * (0.34 * _CloudDetailScale) + scudPan;
                float scudN1 = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, scudUV).r;
                float scudN2 = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, scudUV * 2.2 + float2(0.55, 0.21) + windPan * 0.4).r;
                float scudCombined = scudN1 * 0.65 + scudN2 * 0.35;
                float scudDensity = smoothstep(0.52, 0.78, scudCombined) * smoothstep(0.04, 0.38, dir.y);

                // Horizon fade
                float horizonFade = smoothstep(0.02, 0.22, dir.y);

                // Composite volumetric cloud deck onto sky (hiding moon behind thick clouds)
                half3 col = lerp(skyBase, cloudTone, saturate(cloudDensity * horizonFade * 0.96));

                // Diffused moon corona bleeding through moderately thin cloud cover
                float cloudDiffusedGlow = pow(saturate(moonDot), 18.0) * saturate(1.0 - cloudDensity * 0.45) * cloudDensity * _MoonHaloIntensity * 0.55 * moonHorizonMask;
                col += _MoonColor.rgb * (half)cloudDiffusedGlow;

                // Composite low scuds in front of the cloud deck & moon
                half3 scudRim = _MoonColor.rgb * (half)(pow(saturate(moonDot), 10.0) * (1.0 - scudDensity * 0.6) * _MoonHaloIntensity * 0.35);
                half3 scudTone = _ScudCloudColor.rgb + lightningEmissive * 0.4h + scudRim;
                col = lerp(col, scudTone, saturate(scudDensity * 0.88));

                // Seamless blend into dark slate horizon fog
                col = lerp(_HorizonColor.rgb, col, smoothstep(-0.02, 0.12, dir.y));

                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
}
