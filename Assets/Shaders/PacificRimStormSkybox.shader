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
        _CloudNoiseTex("Cloud Noise", 2D) = "gray" {}
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
                float4 _CloudNoiseTex_ST;
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

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.viewDir);
                float elev = saturate(dir.y);

                half3 skyBase = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(elev, 0.55));

                // Curved dome projection without severe horizon pinching
                float2 skyUV = dir.xz / (sqrt(dir.y * dir.y + 0.06) + dir.y * 0.25);
                float t = _Time.y;
                float2 windDir = normalize(_GlobalWindVector.xy + float2(0.85, 0.52));
                float windSpeed = length(_GlobalWindVector.xy) > 0.1 ? length(_GlobalWindVector.xy) : 24.0;
                
                // Active, cinematic storm cloud movement across the sky
                float cloudSpeedFactor = max(0.2, _CloudSpeed);
                float2 windPan = windDir * (t * windSpeed * 0.0032 * cloudSpeedFactor);

                // --- Layer 1: Cumulonimbus Storm Deck with Domain Warping & Rolling Churn ---
                float churn = t * 0.18 * _CloudChurnSpeed;
                float2 churnShift1 = float2(sin(churn * 0.9 + skyUV.y * 0.4), cos(churn * 0.7 + skyUV.x * 0.4)) * 0.045;
                float2 churnShift2 = float2(cos(churn * 1.1 - skyUV.x * 0.5), sin(churn * 0.8 - skyUV.y * 0.5)) * 0.045;

                float2 baseUV = skyUV * (0.16 * _CloudDetailScale);
                float warp1 = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, baseUV * 0.7 + windPan * 0.6 + churnShift1).r;
                float warp2 = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, baseUV * 0.7 + float2(0.43, 0.27) - windPan * 0.35 + churnShift2).r;
                float2 warpedUV = baseUV + float2(warp1 - 0.5, warp2 - 0.5) * 0.62 + windPan;

                float nMacro = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, warpedUV * 0.65).r;
                float nMid   = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, warpedUV * 1.55 + float2(0.31, 0.19)).r;
                float nMicro = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, warpedUV * 3.30 - float2(0.18, 0.44)).r;

                float fbm = nMacro * 0.50 + nMid * 0.35 + nMicro * 0.15;
                // Billow shaping: rounded thunderhead domes and pinched dark valleys
                float billow = 1.0 - abs(nMid * 2.0 - 1.0);
                float cloudCombined = fbm * 0.72 + billow * 0.28;

                // Threshold & density
                float threshold = 1.0 - _CloudCover;
                float cloudDensity = smoothstep(threshold - _CloudSoftness, threshold + _CloudSoftness, cloudCombined);

                // Silver-lining & 3D edge highlight
                float edgeRim = smoothstep(0.10, 0.65, cloudDensity) * (1.0 - smoothstep(0.65, 0.98, cloudDensity));
                float billowHighlight = pow(saturate(billow * 1.2), 2.0);

                // Directional ambient scattering
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float lightScatter = pow(saturate(dot(dir, -lightDir) * 0.5 + 0.5), 3.0);

                // Tonal grading of storm deck
                half3 cloudTone = lerp(_CloudDarkColor.rgb, _CloudMidColor.rgb, saturate(cloudDensity * 1.25));
                half3 highlightTone = _CloudLightColor.rgb * (half)(1.0 + lightScatter * 0.65);
                cloudTone = lerp(cloudTone, highlightTone, saturate(edgeRim * 0.85 + billowHighlight * 0.45));

                // --- Layer 2: Fast-moving Low Scud Clouds (Ragged torn shreds rushing beneath deck) ---
                float2 scudPan = windDir * (t * windSpeed * 0.0032 * cloudSpeedFactor * _ScudSpeedMultiplier);
                float2 scudUV = skyUV * (0.34 * _CloudDetailScale) + scudPan;
                float scudN1 = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, scudUV).r;
                float scudN2 = SAMPLE_TEXTURE2D(_CloudNoiseTex, sampler_CloudNoiseTex, scudUV * 2.2 + float2(0.55, 0.21) + windPan * 0.4).r;
                float scudCombined = scudN1 * 0.65 + scudN2 * 0.35;
                float scudDensity = smoothstep(0.52, 0.78, scudCombined) * smoothstep(0.04, 0.38, dir.y);

                // --- Internal Lightning Volumetric Illumination ---
                half3 lightningColor = half3(0.52h, 0.74h, 1.0h);
                half3 lightningEmissive = lightningColor * (half)(_GlobalLightningFlash * (0.35 + cloudDensity * 0.85 + edgeRim * 1.2));
                cloudTone += lightningEmissive;

                // Horizon fade
                float horizonFade = smoothstep(0.02, 0.22, dir.y);

                // Composite main deck onto sky
                half3 col = lerp(skyBase, cloudTone, saturate(cloudDensity * horizonFade * 0.95));

                // Composite low scuds in front of the illuminated cloud deck
                half3 scudTone = _ScudCloudColor.rgb + lightningEmissive * 0.4h;
                col = lerp(col, scudTone, saturate(scudDensity * 0.85));

                // Seamless blend into dark slate horizon fog
                col = lerp(_HorizonColor.rgb, col, smoothstep(-0.02, 0.12, dir.y));

                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
}
