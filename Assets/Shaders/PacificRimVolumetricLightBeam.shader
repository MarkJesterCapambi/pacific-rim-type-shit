Shader "PacificRim/VolumetricLightBeam"
{
    Properties
    {
        [HDR] _Color("Light Beam Color", Color) = (0.92, 0.96, 1.0, 0.65)
        _Intensity("Beam Intensity", Range(0.1, 10.0)) = 2.2
        _ConeApexFade("Apex Soft Fade", Range(0.01, 0.5)) = 0.08
        _ConeBaseFade("Base Soft Fade", Range(0.01, 0.5)) = 0.15
        _RimPower("Rim Softness", Range(0.5, 4.0)) = 1.8
        _NoiseSpeed("Mist Scroll Speed", Range(0.0, 5.0)) = 1.2
        _NoiseScale("Mist Frequency", Range(1.0, 30.0)) = 8.0
        _NoiseStrength("Mist Ripple Strength", Range(0.0, 1.0)) = 0.28
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
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
            CBUFFER_END

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
                
                // Long, soft gradient dissolve towards the base so it dissolves smoothly into the storm
                float baseFade = smoothstep(1.0, max(0.001, 1.0 - _ConeBaseFade), zRatio);
                float distanceAtten = exp(-zRatio * 2.8);
                float axialWeight = apexFade * baseFade * distanceAtten;

                // 2. Fresnel / Grazing angle softening (prevents harsh silhouette edges)
                float ndotv = abs(dot(input.normalWS, viewDirWS));
                float rimFade = pow(ndotv, _RimPower);

                // 3. Atmospheric rain mist / dust drift along the beam
                float2 noiseUV = float2(input.uv.x * 2.0, input.uv.y * _NoiseScale - _Time.y * _NoiseSpeed);
                float mistNoise = SmoothNoise(noiseUV) * 0.65 + SmoothNoise(noiseUV * 2.1) * 0.35;
                float mistMod = 1.0 - (_NoiseStrength * (1.0 - mistNoise));

                // 4. Combine volumetric scattering
                float alpha = axialWeight * rimFade * mistMod * _Color.a;
                float3 finalColor = _Color.rgb * (_Intensity * alpha);

                // Apply Unity distance fog
                finalColor = MixFog(finalColor, input.fogCoord);

                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
