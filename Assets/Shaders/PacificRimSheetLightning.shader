Shader "PacificRim/SheetLightning"
{
    Properties
    {
        _MainTex("Voronoi Noise", 2D) = "white" {}
        [HDR] _Color("Sheet Color", Color) = (0.45, 0.65, 1.0, 1.0)
        _FlickerIntensity("Flicker Intensity", Range(0.0, 3.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+90"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SheetLightningForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                float  _FlickerIntensity;
            CBUFFER_END

            float4 _GlobalWindVector;
            float  _GlobalLightningFlash;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 drift = _GlobalWindVector.xy * (_Time.y * 0.04);
                float v1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv * 1.4 + drift).r;
                float v2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv * 2.3 - drift * 0.65 + float2(0.31, 0.47)).r;

                float voronoi = pow(saturate(v1 * v2 * 1.4), 2.5);
                float edgeMask = smoothstep(0.5, 0.1, length(input.uv - 0.5));
                float intensity = max(0.0, _FlickerIntensity + _GlobalLightningFlash * 0.85);

                half3 rgb = _Color.rgb * (half)(voronoi * edgeMask * intensity);
                return half4(rgb, saturate(edgeMask * intensity));
            }
            ENDHLSL
        }
    }
}
