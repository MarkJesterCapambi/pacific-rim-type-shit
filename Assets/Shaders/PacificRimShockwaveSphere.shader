Shader "PacificRim/ShockwaveSphere"
{
    Properties
    {
        [HDR] _CoreColor("White-Gold Core Color", Color) = (4.5, 4.1, 3.2, 1.0)
        [HDR] _RimColor("Solar Rim Color", Color) = (3.4, 2.2, 0.9, 1.0)
        _Intensity("Intensity", Range(0.0, 20.0)) = 4.0
        _Opacity("Opacity", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+300"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "NuclearShockwave"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

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
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _RimColor;
                float _Intensity;
                float _Opacity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = vInputs.positionCS;
                output.positionWS = vInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(nInputs.normalWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float ndotv = abs(dot(normalize(input.normalWS), viewDir));
                float rim = pow(saturate(1.0 - ndotv), 2.1);

                float t = _Time.y * 12.0;
                float plasma = 0.75 + 0.25 * sin(input.uv.x * 28.0 + t) * cos(input.uv.y * 22.0 - t * 1.3);

                half3 col = (_CoreColor.rgb * (half)(0.65 + 0.35 * ndotv) + _RimColor.rgb * (half)(rim * 2.2)) * (half)(_Intensity * _Opacity * plasma);
                return half4(col, saturate(_Opacity));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
