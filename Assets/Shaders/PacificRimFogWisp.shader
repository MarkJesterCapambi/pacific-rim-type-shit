Shader "PacificRim/FogWisp"
{
    Properties
    {
        _MainTex("Mist Noise", 2D) = "white" {}
        _TintColor("Mist Tint", Color) = (0.14, 0.18, 0.24, 0.22)
        _InvFade("Soft Particle Fade", Range(0.05, 2.0)) = 0.25
        _ScrollSpeed("UV Pan Speed", Float) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+80"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "FogWispForward"
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
                float  _ScrollSpeed;
            CBUFFER_END

            float4 _GlobalWindVector;
            float  _GlobalLightningFlash;

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

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float surfaceEyeDepth = GetLinearSceneDepth(input.positionCS.z);
                float sceneEyeDepth = GetLinearSceneDepth(SampleSceneDepth(screenUV));
                float softFade = saturate((sceneEyeDepth - surfaceEyeDepth) * _InvFade);

                float2 pan = _GlobalWindVector.xy * (_Time.y * _ScrollSpeed);
                float n1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + pan).r;
                float n2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv * 1.7 - pan * 0.45 + float2(0.27, 0.61)).r;
                float noise = saturate(n1 * 0.65 + n2 * 0.55 - 0.15);

                float radial = smoothstep(0.5, 0.08, length(input.uv - 0.5));
                half alpha = saturate((half)(noise * radial * softFade) * _TintColor.a * input.color.a);

                half3 flashGlow = half3(0.32h, 0.44h, 0.68h) * (half)_GlobalLightningFlash;
                half3 rgb = (_TintColor.rgb * input.color.rgb) + flashGlow * 0.45h;
                rgb = MixFog(rgb, input.fogCoord);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
