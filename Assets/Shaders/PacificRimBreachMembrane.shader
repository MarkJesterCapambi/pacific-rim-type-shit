Shader "PacificRim/BreachMembrane"
{
    Properties
    {
        [HDR] _ColorA("Primary Violet Color", Color) = (0.55, 0.12, 1.4, 0.85)
        [HDR] _ColorB("Secondary Electric Blue", Color) = (0.08, 0.78, 1.8, 0.85)
        [HDR] _TearEdgeColor("Tear Rim HDR Color", Color) = (1.8, 1.4, 2.8, 1.0)
        _Iridescence("Iridescence Blend", Range(0.0, 1.0)) = 0.0
        _RippleProgress("Genetic Ripple Progress", Range(0.0, 2.0)) = 0.0
        _HitUV("Contact UV", Vector) = (0.5, 0.5, 0.0, 0.0)
        _TearAmount("Tear Opening Amount", Range(0.0, 1.0)) = 0.0
        _FlareIntensity("HDR Flare Multiplier", Range(0.1, 15.0)) = 1.6
        _PulseSpeed("Organic Pulse Speed", Float) = 2.2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+120"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "BreachBioMembrane"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

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
                float3 positionWS : TEXCOORD1;
                float  fogCoord   : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                half4 _TearEdgeColor;
                float _Iridescence;
                float _RippleProgress;
                float4 _HitUV;
                float _TearAmount;
                float _FlareIntensity;
                float _PulseSpeed;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float s = 0.0;
                float a = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    s += Noise2D(p) * a;
                    p = p * 2.07 + float2(13.1, 7.7);
                    a *= 0.5;
                }
                return s;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posOS = input.positionOS.xyz;

                float2 centered = input.uv - _HitUV.xy;
                float dist = length(centered);
                float wave = sin(dist * 24.0 - _Time.y * 4.5) * 0.08;
                float shock = exp(-abs(dist - _RippleProgress * 0.85) * 14.0) * saturate(1.5 - _RippleProgress) * 0.55;
                posOS.y += (wave + shock);

                VertexPositionInputs vInputs = GetVertexPositionInputs(posOS);
                output.positionCS = vInputs.positionCS;
                output.positionWS = vInputs.positionWS;
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(vInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y * _PulseSpeed;
                float2 uv = input.uv;
                float2 hitDelta = uv - _HitUV.xy;
                float dist = length(hitDelta);
                float ang = atan2(hitDelta.y, hitDelta.x);

                // Outer silhouette ellipse so the membrane fades smoothly into the jagged chasm walls
                float2 edgeDelta = (uv - 0.5) * 2.0;
                float radialEdge = length(edgeDelta);
                float edgeMask = 1.0 - smoothstep(0.62, 0.98, radialEdge);

                // Organic bio-electric plasma veins & cellular web
                float n1 = Fbm(uv * 6.5 + float2(t * 0.14, -t * 0.11));
                float n2 = Fbm(uv * 14.0 + float2(-t * 0.24, t * 0.18) + n1 * 1.4);
                float veins = pow(1.0 - abs(n2 * 2.0 - 1.0), 3.0);

                // Expanding concentric DNA-lock scan shockwave rings
                float ringMask = 0.0;
                if (_RippleProgress > 0.001)
                {
                    float r1 = exp(-pow((dist - _RippleProgress * 0.65) * 18.0, 2.0));
                    float r2 = exp(-pow((dist - _RippleProgress * 0.42) * 22.0, 2.0)) * 0.7;
                    ringMask = saturate(r1 + r2) * saturate(1.8 - _RippleProgress);
                }

                // Ripping aperture open from _HitUV as Slattern triggers the genetic lock
                float jaggedAngleNoise = Fbm(float2(cos(ang) * 3.0 + 4.1, sin(ang) * 3.0 + t * 0.25)) * 0.18;
                float tearRadius = _TearAmount * 0.58 + jaggedAngleNoise * saturate(_TearAmount * 2.2);
                float openAlpha = smoothstep(tearRadius - 0.07, tearRadius + 0.05, dist);
                float tearRim = exp(-pow((dist - tearRadius) / 0.055, 2.0)) * saturate(_TearAmount * 3.5);

                // Color blending (+ optional rainbow iridescence for the 0:18 inner exit membrane)
                half3 baseGrad = lerp(_ColorA.rgb, _ColorB.rgb, saturate(n1 * 1.25 + veins * 0.5));
                if (_Iridescence > 0.01)
                {
                    float phase = n1 * 6.283 + dist * 9.0 - t * 1.2;
                    half3 irid = 0.55h + 0.45h * cos(half3(phase, phase + 2.094, phase + 4.188));
                    baseGrad = lerp(baseGrad, irid * 1.6h, (half)_Iridescence);
                }

                half3 hdrCol = baseGrad * (half)(0.75 + veins * 1.45) * (half)_FlareIntensity;
                hdrCol += _TearEdgeColor.rgb * (half)(tearRim * 2.8 + ringMask * 2.4 * _FlareIntensity);

                // Bright blinding rift glow deep inside the ripped opening before fading into open tear
                float innerBlaze = (1.0 - openAlpha) * saturate(_TearAmount * 2.0) * (1.0 - smoothstep(0.0, max(0.05, tearRadius * 0.65), dist));
                hdrCol += half3(1.6, 1.8, 2.6) * (half)(innerBlaze * 1.4 * _FlareIntensity);

                float alpha = saturate((0.42 + veins * 0.48 + ringMask * 0.65 + tearRim * 0.85) * edgeMask * lerp(1.0, openAlpha + tearRim * 0.9, saturate(_TearAmount * 1.6)));
                hdrCol = MixFog(hdrCol, input.fogCoord);

                return half4(hdrCol, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
