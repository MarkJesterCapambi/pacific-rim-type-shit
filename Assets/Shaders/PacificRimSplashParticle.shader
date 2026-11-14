Shader "PacificRim/SplashParticle"
{
    Properties
    {
        _MainTex("Particle Shape (Alpha)", 2D) = "white" {}
        _TintColor("Tint", Color) = (0.90, 0.95, 1.0, 1.0)
        _InvFade("Soft Particle Fade", Range(0.05, 3.0)) = 0.45
        _LightInfluence("Scene Light Influence", Range(0.0, 1.0)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+60"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SplashParticleForward"
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _TintColor;
                float  _InvFade;
                float  _LightInfluence;
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
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float surfaceEyeDepth = GetLinearSceneDepth(input.positionCS.z);
                float sceneEyeDepth = GetLinearSceneDepth(SampleSceneDepth(screenUV));
                float softFade = saturate((sceneEyeDepth - surfaceEyeDepth) * _InvFade);

                half lightLum = dot(_MainLightColor.rgb, half3(0.333h, 0.333h, 0.333h));
                half lit = lerp(1.0h, saturate(0.40h + lightLum * 0.60h), _LightInfluence);

                half3 rgb = tex.rgb * _TintColor.rgb * input.color.rgb * lit;
                rgb *= (half)(1.0 + _GlobalLightningFlash * 0.75);
                rgb = MixFog(rgb, input.fogCoord);

                half alpha = saturate(tex.a * _TintColor.a * input.color.a * (half)softFade);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
