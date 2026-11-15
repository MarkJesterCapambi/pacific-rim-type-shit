Shader "PacificRim/RocketThrusterBeam"
{
    Properties
    {
        [HDR] _CoreColor("Core High-Temp Color", Color) = (0.85, 0.96, 1.0, 1.0)
        [HDR] _PlumeColor("Plume Plasma Color", Color) = (0.12, 0.78, 1.0, 1.0)
        [HDR] _TailColor("Tail Exhaust Color", Color) = (0.02, 0.20, 0.90, 1.0)
        _Intensity("Overall Intensity", Range(0.5, 15.0)) = 4.8
        _FlickerSpeed("Combustion Flicker Speed", Range(5.0, 60.0)) = 34.0
        _LengthFlickerAmount("Length Flicker Depth", Range(0.0, 0.6)) = 0.28
        _NoiseScale("Turbulence Scale", Range(1.0, 30.0)) = 14.0
        _NoiseSpeed("Turbulence Ejection Speed", Range(5.0, 60.0)) = 28.0
        _ShockFrequency("Mach Diamond Frequency", Range(1.0, 12.0)) = 5.0
        _ShockIntensity("Mach Diamond Intensity", Range(0.0, 3.0)) = 1.3
        _OriginSolidDistance("Origin Solid Zone", Range(0.05, 0.45)) = 0.22
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
            Name "RocketThrusterBeam"
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
                float4 _CoreColor;
                float4 _PlumeColor;
                float4 _TailColor;
                float  _Intensity;
                float  _FlickerSpeed;
                float  _LengthFlickerAmount;
                float  _NoiseScale;
                float  _NoiseSpeed;
                float  _ShockFrequency;
                float  _ShockIntensity;
                float  _OriginSolidDistance;
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
                float zRatio = saturate(input.uv.y);

                // 1. High-frequency Combustion Length Flicker (simulates realistic rocket thruster tail jitter)
                float t = _Time.y * _FlickerSpeed;
                float flicker = sin(t * 1.37) * 0.45 
                              + sin(t * 3.14 + input.uv.x * 6.28318) * 0.35 
                              + cos(t * 6.28) * 0.20;
                
                float dynamicLength = saturate(1.0 - _LengthFlickerAmount * (0.5 + 0.5 * flicker));

                // 2. High-speed supersonic turbulent shear along exhaust axis
                float2 noiseUV = float2(input.uv.x * 3.0, input.uv.y * _NoiseScale - _Time.y * _NoiseSpeed);
                float turbA = SmoothNoise(noiseUV);
                float turbB = SmoothNoise(noiseUV * 2.3 + float2(1.4, 2.7));
                float shear = turbA * 0.65 + turbB * 0.35;

                // Tail erosion: turbulence erodes the tail boundary dynamically
                float effectiveCutoff = dynamicLength - (1.0 - shear) * (zRatio * 0.30);

                // 3. Axial Opacity: Solid, dense core at nozzle origin; dynamic flickering cutoff at tail
                float originSolid = smoothstep(_OriginSolidDistance, 0.0, zRatio);
                float tailFade = smoothstep(effectiveCutoff, max(0.01, effectiveCutoff - 0.28), zRatio);
                float coreDecay = exp(-zRatio * 1.6);
                float axialAlpha = saturate(lerp(coreDecay * tailFade, 1.0, originSolid * 0.90));

                // 4. Supersonic Mach Diamonds (Standing Shock Discs)
                float shockPhase = zRatio * _ShockFrequency - _Time.y * 1.6;
                float shockWave = pow(saturate(cos(shockPhase * 6.28318)), 10.0);
                float shockDiamonds = shockWave * saturate(1.0 - zRatio * 1.1) * _ShockIntensity;

                // 5. Cylindrical Volumetric Softness
                float ndotv = abs(dot(input.normalWS, viewDirWS));
                float centerDensity = pow(saturate(1.0 - ndotv * 0.55), 0.75);

                // 6. Dynamic Color Gradient
                float3 colCore = _CoreColor.rgb * (1.0 + shockDiamonds * 1.2);
                float3 colPlume = _PlumeColor.rgb * (1.0 + shockDiamonds * 0.5);
                float3 colTail = _TailColor.rgb;

                float3 beamColor = lerp(colCore, colPlume, smoothstep(0.0, 0.30, zRatio));
                beamColor = lerp(beamColor, colTail, smoothstep(0.30, 0.85, zRatio));

                // Rapid micro-flicker in combustion intensity
                float combustionIntensity = 0.90 + 0.20 * sin(t * 1.85);
                float alpha = axialAlpha * centerDensity * combustionIntensity;
                float3 finalRgb = beamColor * (_Intensity * alpha * (1.0 + shockDiamonds * 0.7));

                // Apply Unity distance fog
                finalRgb = MixFog(finalRgb, input.fogCoord);

                return float4(finalRgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
