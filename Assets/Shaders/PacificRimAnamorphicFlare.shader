Shader "PacificRim/AnamorphicFlare"
{
    Properties
    {
        _FlareColor("Flare Tint", Color) = (0.25, 0.75, 1.0, 1.0)
        _Intensity("Intensity", Range(0.0, 10.0)) = 2.5
        _HorizontalStretch("Horizontal Stretch", Float) = 14.0
        _CoreSharpness("Core Sharpness", Float) = 4.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+500"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One One // Additive blend for HDR anamorphic glow
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "AnamorphicStreak"

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
                float4 _FlareColor;
                float  _Intensity;
                float  _HorizontalStretch;
                float  _CoreSharpness;
            CBUFFER_END

            float _GlobalLightningFlash;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv * 2.0 - 1.0; // Center at (0,0)

                // Anamorphic horizontal streak falloff: extreme compression along Y, wide along X
                float xFalloff = exp(-abs(uv.x) * (2.2 / max(0.1, _HorizontalStretch)));
                float yFalloff = exp(-pow(abs(uv.y), 2.0) * _CoreSharpness * 32.0);

                // Bright central needle
                float coreNeedle = exp(-abs(uv.y) * 90.0) * exp(-abs(uv.x) * 0.85);

                float streak = (xFalloff * yFalloff * 0.85 + coreNeedle * 0.95);

                // Boost intensity during lightning flashes
                float flashBoost = 1.0 + saturate(_GlobalLightningFlash) * 2.5;
                half3 col = _FlareColor.rgb * (_Intensity * flashBoost * streak);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
