Shader "PacificRim/VolumetricLightBeam"
{
    Properties
    {
        [HDR] _Color("Light Beam Color", Color) = (0.92, 0.96, 1.0, 0.65)
        _Intensity("Beam Intensity", Range(0.1, 10.0)) = 2.2
        _ConeApexFade("Apex Soft Fade", Range(0.01, 0.5)) = 0.08
        _ConeBaseFade("Base Soft Fade", Range(0.01, 0.85)) = 0.15
        _RimPower("Rim Softness", Range(0.5, 6.0)) = 1.8
        _NoiseSpeed("Mist Scroll Speed", Range(0.0, 5.0)) = 1.2
        _NoiseScale("Mist Frequency", Range(1.0, 30.0)) = 8.0
        _NoiseStrength("Mist Ripple Strength", Range(0.0, 1.0)) = 0.28
        _WaterLevel("Ocean Water Level", Float) = 2.977
        _StreakDensity("Volumetric Streak Density", Range(1.0, 32.0)) = 6.0
        _StreakContrast("Volumetric Streak Contrast", Range(0.0, 1.0)) = 0.0
        _PulseSpeed("Opacity Pulse Speed", Range(0.0, 5.0)) = 0.0
        _PulseAmount("Opacity Pulse Amount", Range(0.0, 1.0)) = 0.0
        _PulsePhase("Opacity Pulse Phase", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VolumetricBeam"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _ConeApexFade;
                float _ConeBaseFade;
                float _RimPower;
                float _NoiseSpeed;
                float _NoiseScale;
                float _NoiseStrength;
                float _WaterLevel;
                float _StreakDensity;
                float _StreakContrast;
                float _PulseSpeed;
                float _PulseAmount;
                float _PulsePhase;
            CBUFFER_END

            float _GlobalLightningFlash;

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.normalWS   = normalize(normalInputs.normalWS);
                output.uv         = input.uv;
                output.fogCoord   = ComputeFogFactor(vertexInputs.positionCS.z);
                return output;
            }

            float SimpleNoise(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float SmoothNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = SimpleNoise(i);
                float b = SimpleNoise(i + float2(1.0, 0.0));
                float c = SimpleNoise(i + float2(0.0, 1.0));
                float d = SimpleNoise(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                // 1. Axial Falloff (uv.y from 0 at apex to 1 at base)
                float zRatio = saturate(input.uv.y);
                float apexFade = smoothstep(0.0, max(0.001, _ConeApexFade), zRatio);
                float baseFade = smoothstep(1.0, max(0.001, 1.0 - _ConeBaseFade), zRatio);
                float distanceAtten = exp(-zRatio * 2.4);
                float axialWeight = apexFade * baseFade * distanceAtten;

                // 2. Smooth Fresnel / grazing-angle edge dissolution (prevents cone silhouette lines)
                float ndotv = abs(dot(normalize(input.normalWS), viewDirWS));
                float rimFade = pow(saturate(ndotv), _RimPower) * smoothstep(0.03, 0.42, ndotv);

                // 3. Seamless cylindrical mist & drifting caustic light-shaft streaks
                float angle = input.uv.x * 6.2831853;
                float2 ringCoord = float2(cos(angle), sin(angle));
                float2 mistUV = ringCoord * 1.45 + float2(0.0, input.uv.y * _NoiseScale - _Time.y * _NoiseSpeed);
                float mistNoise = SmoothNoise(mistUV) * 0.65 + SmoothNoise(mistUV * 2.15 + 3.7) * 0.35;
                float mistMod = 1.0 - (_NoiseStrength * (1.0 - mistNoise));

                // Multi-layered radial light shafts when _StreakContrast > 0 (godrays)
                float streakWaveA = 0.5 + 0.5 * sin(angle * _StreakDensity + _Time.y * (_NoiseSpeed * 0.35) + _PulsePhase);
                float streakWaveB = 0.5 + 0.5 * sin(angle * (_StreakDensity * 1.7 + 1.0) - _Time.y * (_NoiseSpeed * 0.25) + input.uv.y * 2.2);
                float shafts = pow(saturate(streakWaveA * 0.62 + streakWaveB * 0.38), 1.65);
                float shaftMod = lerp(1.0, lerp(0.22, 1.45, shafts), _StreakContrast);

                // Travelling luminous caustic shimmer along the beam axis
                float shimmer = lerp(1.0, 0.72 + 0.56 * SmoothNoise(ringCoord * 2.5 + float2(_Time.y * 0.22, input.uv.y * 3.5 - _Time.y * _NoiseSpeed * 0.85)), _StreakContrast);

                // Breathing opacity pulse in shader (for autonomous editor & runtime shimmer)
                float pulseWave = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed + _PulsePhase);
                float pulseMod = lerp(1.0, lerp(1.0 - _PulseAmount, 1.0 + _PulseAmount * 0.35, smoothstep(0.0, 1.0, pulseWave)), step(0.001, _PulseSpeed));

                // 4. Soft termination at ocean water level
                float waterFade = saturate((input.positionWS.y - (_WaterLevel - 0.5)) * 0.75);

                // 5. Soft depth fade against solid geometry
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float vertexEyeDepth = input.positionCS.w;
                float depthFade = saturate((sceneEyeDepth - vertexEyeDepth) * 0.22);

                // 6. Combine volumetric scattering (additive: fade fog toward BLACK, never fog color)
                float alpha = axialWeight * rimFade * mistMod * shaftMod * shimmer * pulseMod * waterFade * depthFade * _Color.a;
                float flashBoost = 1.0 + _GlobalLightningFlash * 1.6;
                float3 finalColor = _Color.rgb * (_Intensity * alpha * flashBoost);

                // Additive blend MUST attenuate toward black with distance fog, not add fog color
                finalColor = MixFogColor(finalColor, half3(0.0, 0.0, 0.0), input.fogCoord);

                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
