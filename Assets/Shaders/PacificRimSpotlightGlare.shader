Shader "PacificRim/SpotlightGlare"
{
    Properties
    {
        _GlareColor("Glare Color", Color) = (0.82, 0.94, 1.0, 1.0)
        _Intensity("HDR Intensity", Float) = 6.0
        _BeamAlignment("Beam Toward Camera", Range(0.0, 1.0)) = 0.65
        _Aspect("Quad Aspect Ratio", Float) = 2.4
        _StreakLength("Horizontal Reflection Length", Float) = 1.8
        _Softness("Halo Falloff", Float) = 4.2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "SpotlightCameraGlare"

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
                float4 _GlareColor;
                float  _Intensity;
                float  _BeamAlignment;
                float  _Aspect;
                float  _StreakLength;
                float  _Softness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv * 2.0 - 1.0;

                // Normalize radial coordinates by quad aspect so lamp bulb & bloom halo stay spherical
                float aspect = max(1.0, _Aspect);
                float2 p = float2(uv.x * aspect, uv.y);
                float r = length(p);

                float radialMask = smoothstep(1.0, 0.35, r);
                float edgeMask = smoothstep(1.0, 0.65, abs(uv.x)) * smoothstep(1.0, 0.55, abs(uv.y));

                // Pinpoint hot spotlight bulb core + tight localized atmospheric bloom halo
                float core = exp(-r * r * 145.0) * 1.25 + exp(-r * 16.0) * 0.34;
                float halo = exp(-r * max(2.2, _Softness * 1.65)) * 0.14;

                // Subtle optical diffraction star glint
                float crossGlint = (exp(-abs(p.x) * 42.0) * exp(-abs(p.y) * 6.5)
                                  + exp(-abs(p.y) * 42.0) * exp(-abs(p.x) * 6.5)) * 0.18;

                // Crisp hairline anamorphic horizontal lens reflection streak
                float streakX = exp(-abs(uv.x) * (4.0 / max(0.35, _StreakLength)));
                float streakY = exp(-pow(abs(uv.y) * 14.0, 1.55));
                float needle  = exp(-abs(uv.y) * 92.0) * exp(-abs(uv.x) * 2.1);
                float anamorphic = (streakX * streakY * 0.28 + needle * 0.68) * edgeMask;

                float beamFocus = pow(saturate(_BeamAlignment), 1.85);
                float beaconBase = (core * 0.32 + halo * 0.18) * radialMask;
                float focusedEnergy = ((core + halo + crossGlint) * radialMask + anamorphic) * beamFocus;

                float totalEnergy = (beaconBase + focusedEnergy) * max(0.0, _Intensity);
                half3 col = _GlareColor.rgb * totalEnergy;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
