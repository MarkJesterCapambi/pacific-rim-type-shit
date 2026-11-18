Shader "PacificRim/WormholeTunnel"
{
    Properties
    {
        [HDR] _PurpleColor("Deep Organic Purple", Color) = (0.34, 0.05, 0.68, 1.0)
        [HDR] _MagentaColor("Plasma Magenta", Color) = (1.45, 0.14, 0.96, 1.0)
        [HDR] _ElectricBlueColor("Bio-Electric Blue", Color) = (0.12, 0.85, 2.4, 1.0)
        _ScrollSpeed("Tunnel Scroll Speed", Float) = 3.8
        _SwirlAmount("Helix Swirl Twist", Float) = 2.6
        _FlashBurst("Electric Surge Flash", Range(0.0, 3.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite On

        Pass
        {
            Name "WormholeTunnelWall"
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
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _PurpleColor;
                half4 _MagentaColor;
                half4 _ElectricBlueColor;
                float _ScrollSpeed;
                float _SwirlAmount;
                float _FlashBurst;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.12);
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
                float v = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v += Noise2D(p) * amp;
                    p = p * 2.1 + float2(9.2, 5.4);
                    amp *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float t = _Time.y * 4.2;
                float3 posOS = input.positionOS.xyz;

                // Subtle organic muscular pulse along tunnel walls
                float pulse = sin(input.uv.y * 18.0 - t) * 0.45 + cos(input.uv.x * 12.566 + t * 0.7) * 0.25;
                posOS += normalize(input.normalOS) * pulse;

                VertexPositionInputs vInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs nInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vInputs.positionCS;
                output.positionWS = vInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(nInputs.normalWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float scroll = t * _ScrollSpeed;

                // Seamless cylindrical coordinates with helical twist
                float angle = input.uv.x * 6.2831853 + input.uv.y * _SwirlAmount;
                float2 cyl = float2(cos(angle), sin(angle)) * 2.4;
                float2 flowUV1 = cyl + float2(0.0, input.uv.y * 8.0 - scroll);
                float2 flowUV2 = cyl * 1.85 + float2(t * 0.4, input.uv.y * 14.0 - scroll * 1.45);

                float flesh = Fbm(flowUV1);
                float tendrils = Fbm(flowUV2 + flesh * 1.35);

                // Sharp ridged plasma tendril filaments
                float ridgeA = pow(1.0 - abs(tendrils * 2.0 - 1.0), 3.6);
                float ridgeB = pow(1.0 - abs(Fbm(flowUV2 * 2.1 - float2(0.0, scroll * 0.8)) * 2.0 - 1.0), 4.2);

                // Travelling ribbed throat rings
                float rings = 0.5 + 0.5 * sin(input.uv.y * 34.0 - scroll * 2.4 + flesh * 4.0);
                rings = pow(rings, 3.0);

                // Dark void center & glowing grazing walls
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float ndotv = abs(dot(normalize(input.normalWS), viewDir));
                float rim = pow(saturate(1.0 - ndotv), 1.6);

                half3 col = _PurpleColor.rgb * (half)(0.16 + flesh * 0.42 + rings * 0.22);
                col += _MagentaColor.rgb * (half)(pow(ridgeA, 1.45) * 0.95 + rim * 0.35);
                col += _ElectricBlueColor.rgb * (half)(pow(ridgeB, 1.55) * 1.15 + rings * ridgeA * 0.55);
                col *= (half)(1.0 + _FlashBurst * 0.55);

                // Darken far ends of the tunnel so it fades seamlessly into abyssal void
                float endFade = smoothstep(0.0, 0.08, input.uv.y) * smoothstep(1.0, 0.88, input.uv.y);
                col *= (half)endFade;

                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
