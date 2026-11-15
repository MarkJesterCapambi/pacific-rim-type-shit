Shader "PacificRim/RainStreak"
{
    Properties
    {
        _TintColor("Streak Tint", Color) = (0.85, 0.92, 1.0, 0.48)
        _InvFade("Soft Particle Fade", Range(0.05, 3.0)) = 0.65
        _NeedleCoreIntensity("Needle Core Intensity", Range(1.0, 5.0)) = 2.4
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
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

                // High-contrast razor-thin specular core (sharp thin needle, never chunky)
                float needleCore = pow(lat, max(2.0, _NeedleSharpness)); // Razor-thin intense glint line down center
                float needleHalo = pow(lat, 2.0) * 0.30;                 // Soft translucent boundary
                float mask = (needleCore * max(1.0, _NeedleCoreIntensity) + needleHalo) * pow(lon, 0.65);

                // Highly visible, crisp raindrop refraction glint
                half3 needleGlint = lerp(_TintColor.rgb, half3(0.96h, 0.98h, 1.0h), (half)(needleCore * 0.95));
                half3 flashBoost = needleGlint * (half)(1.0 + _GlobalLightningFlash * 1.15);
                half3 rgb = flashBoost * input.color.rgb;
                rgb = MixFog(rgb, input.fogCoord);

                half alpha = saturate(mask * _TintColor.a * input.color.a * (half)softFade);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
