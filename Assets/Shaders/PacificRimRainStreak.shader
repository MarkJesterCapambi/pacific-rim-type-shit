Shader "PacificRim/RainStreak"
{
    Properties
    {
        _TintColor("Streak Tint", Color) = (0.88, 0.94, 1.0, 0.85)
        _InvFade("Soft Particle Fade", Range(0.05, 3.0)) = 0.65
        _NeedleCoreIntensity("Needle Core Intensity", Range(1.0, 8.0)) = 4.2
        _NeedleSharpness("Needle Sharpness (Thinness)", Range(2.0, 8.0)) = 5.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "RainStreakForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                float  fogCoord   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                float _InvFade;
                float _NeedleCoreIntensity;
                float _NeedleSharpness;
            CBUFFER_END

            float _GlobalLightningFlash;

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

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float surfaceEyeDepth = GetLinearSceneDepth(input.positionCS.z);
                float sceneEyeDepth = GetLinearSceneDepth(SampleSceneDepth(screenUV));
                float softFade = saturate((sceneEyeDepth - surfaceEyeDepth) * _InvFade);

                // Smooth lateral needle falloff & longitudinal streak taper
                float lat = saturate(1.0 - abs(input.uv.x * 2.0 - 1.0));
                float lon = saturate(sin(saturate(input.uv.y) * 3.14159265));

                // High-contrast razor-thin specular core (sharp thin needle, never sub-pixel invisible)
                float needleCore = pow(lat, clamp(_NeedleSharpness, 1.8, 3.2));
                float needleHalo = smoothstep(0.0, 0.45, lat) * 0.16;
                float mask = (needleCore * max(1.4, _NeedleCoreIntensity) + needleHalo) * pow(lon, 0.65);

                // Near-camera fade so giant streaks right on the lens don't obscure gameplay
                float nearCamFade = smoothstep(2.5, 9.0, surfaceEyeDepth);

                // Subtle unlit storm ambient baseline (rain shouldn't self-illuminate in the dark)
                half3 ambientStorm = _TintColor.rgb * half3(0.24h, 0.31h, 0.42h);
                half3 lightningLit = half3(0.75h, 0.88h, 1.0h) * (half)(_GlobalLightningFlash * 1.15);

                // Real-time spotlight & point light transmission (Chinook searchlights, Gipsy chest/visor lights)
                half3 spotLit = half3(0.0h, 0.0h, 0.0h);
                float spotAlphaBoost = 0.0;

                #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP) || defined(_FORWARD_PLUS)
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = screenUV;

                uint addLightsCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(addLightsCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                    half3 addCol = addLight.color * (addLight.distanceAttenuation * addLight.shadowAttenuation);
                    if (dot(addCol, addCol) > 0.0001h)
                    {
                        // Forward-scattering glint when spotlight passes through the raindrop toward the camera
                        float scatter = pow(saturate(dot(viewDirWS, -normalize(addLight.direction))), 2.0) * 0.70 + 0.30;
                        half3 contrib = addCol * (half)(scatter * (1.2 + needleCore * 1.6));
                        spotLit += contrib;
                        spotAlphaBoost += saturate(dot(contrib, half3(0.33h, 0.33h, 0.33h)));
                    }
                LIGHT_LOOP_END
                #endif

                half3 rgb = (ambientStorm + lightningLit + spotLit) * input.color.rgb;
                half3 foggedRgb = MixFog(rgb, input.fogCoord);
                rgb = lerp(rgb, foggedRgb, 0.55h);

                // Low baseline transparency in darkness; pops brightly where spotlight beams pass through
                half baseAlpha = _TintColor.a * 0.42h;
                half litAlpha = saturate(baseAlpha + (half)(spotAlphaBoost * 0.65) + (half)(_GlobalLightningFlash * 0.35));
                half alpha = saturate(mask * litAlpha * input.color.a * (half)(softFade * nearCamFade));
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
