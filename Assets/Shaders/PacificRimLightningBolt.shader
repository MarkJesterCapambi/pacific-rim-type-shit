Shader "PacificRim/LightningBolt"
{
    Properties
    {
        [HDR] _CoreColor("Core HDR Color", Color) = (2.2, 2.8, 4.0, 1.0)
        [HDR] _EdgeColor("Corona Color", Color) = (0.35, 0.55, 1.5, 1.0)
        _DisplacementStrength("Vertex Jitter", Range(0.0, 4.0)) = 1.35
        _JitterSpeed("Jitter Speed", Float) = 38.0
        _FlashAlpha("Flash Alpha", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+120"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "LightningBoltForward"
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

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _EdgeColor;
                float _DisplacementStrength;
                float _JitterSpeed;
                float _FlashAlpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posOS = input.positionOS.xyz;

                float t = _Time.y * _JitterSpeed;
                float envelope = sin(saturate(input.uv.y) * 3.14159265);
                float jx = sin(input.uv.y * 28.0 + t) + 0.5 * cos(input.uv.y * 63.0 - t * 1.4);
                float jz = cos(input.uv.y * 31.0 - t * 1.15) + 0.5 * sin(input.uv.y * 57.0 + t * 0.9);

                posOS.x += jx * _DisplacementStrength * envelope;
                posOS.z += jz * _DisplacementStrength * envelope;

                output.positionCS = TransformObjectToHClip(posOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float radial = saturate(1.0 - abs(input.uv.x * 2.0 - 1.0));
                float core = pow(radial, 3.0);
                float corona = pow(radial, 1.1);

                half3 hdrCol = _CoreColor.rgb * (half)core + _EdgeColor.rgb * (half)(corona * 0.65);
                hdrCol *= (half)saturate(_FlashAlpha);

                return half4(hdrCol, saturate(corona * _FlashAlpha));
            }
            ENDHLSL
        }
    }
}
