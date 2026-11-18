#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PacificRimWater.Editor
{
    internal struct NeonCityAssetPack
    {
        public Material HighwayMat;
        public Material IntersectionMat;
        public Material SidewalkMat;
        public Material DarkConcreteMat;
        public Material TongLauConcreteMat;
        public Material DarkSteelMat;
        public Material DarkGlassMat;
        public Material WhiteLedBraceMat;
        public Material BarrierMat;
        public Material ACUnitMat;
        public Material BambooMat;
        public Material GreenNettingMat;
        public Material SkybridgeGlassMat;
        public Material GantrySignMat;
        public Material HelipadMat;
        public Material HarbourOceanMat;
        public Material CloudCeilingMat;
        public Material MistPoolMat;

        public Material[] WindowMats;
        public Material[] NeonTubeMats;      // 0:Cyan, 1:Magenta, 2:Amber, 3:Cobalt, 4:Jade, 5:Crimson
        public Material[] SignMats;          // Horizontal Traditional Chinese Neon Signs
        public Material[] VerticalSignMats;  // Vertical Cantonese Neon Calligraphy Signs
        public Material[] WaterfrontLedMats; // Giant Harbourfront LED Billboards
        public Material[] VolumetricBeamMats;// Crimson, Cyan, Amber, Magenta volumetric beams
        public Material[] CarBodyMats;
        public Material TailLightMat;
        public Material HeadLightMat;
        public Material TankerHazardMat;
        public Material TramGreenMat;

        // Custom Architectural Meshes
        public Mesh TriangularPrismMesh;
        public Mesh OctagonPrismMesh;
        public Mesh CurvedArcTowerMesh;
    }

    internal static class SetupNeonCityAssets
    {
        public const string TextureFolder = "Assets/Textures/NeonCity";
        public const string MaterialFolder = "Assets/Materials/NeonCity";
        public const string MeshFolder = "Assets/Meshes/NeonCity";

        public static readonly Color[] NeonHdrPalette = new[]
        {
            new Color(0.12f, 4.40f, 5.60f, 1f), // 0: Electric Cyan
            new Color(5.20f, 0.30f, 2.85f, 1f), // 1: Hot Magenta
            new Color(5.40f, 2.15f, 0.22f, 1f), // 2: Neon Amber
            new Color(0.28f, 2.10f, 5.60f, 1f), // 3: Midnight Cobalt Blue
            new Color(0.20f, 4.80f, 2.40f, 1f), // 4: Jade Green
            new Color(5.60f, 0.24f, 0.58f, 1f), // 5: Crimson Red
        };

        internal static readonly Dictionary<char, float[][]> HanziStrokes = BuildHanziGlyphLibrary();

        public static NeonCityAssetPack EnsureAllAssets()
        {
            EnsureFolder(TextureFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(MeshFolder);

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            var pack = new NeonCityAssetPack
            {
                TriangularPrismMesh = EnsureTriangularPrismMesh(),
                OctagonPrismMesh = EnsureOctagonPrismMesh(),
                CurvedArcTowerMesh = EnsureCurvedArcTowerMesh()
            };

            // 1. Wet Mirror-Like Highway & Intersections & Sidewalks
            GenerateHighwayTextures(out Texture2D hwAlb, out Texture2D hwSm, out Texture2D hwNorm, out Texture2D hwEmis);
            pack.HighwayMat = CreateOrUpdateMaterial("NeonCity_Highway", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.92f, 0.94f, 0.98f, 1f));
                m.SetTexture("_BaseMap", hwAlb);
                m.SetTexture("_MetallicGlossMap", hwSm);
                m.SetFloat("_Metallic", 0.08f);
                m.SetFloat("_Smoothness", 0.96f);
                m.SetTexture("_BumpMap", hwNorm);
                m.SetFloat("_BumpScale", 0.70f);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.EnableKeyword("_NORMALMAP");
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", hwEmis);
                m.SetColor("_EmissionColor", new Color(2.5f, 2.5f, 2.5f, 1f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            });

            Texture2D interTex = GenerateIntersectionTexture();
            pack.IntersectionMat = CreateOrUpdateMaterial("NeonCity_Intersection", urpLit, m =>
            {
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BaseMap", interTex);
                m.SetTexture("_MetallicGlossMap", hwSm);
                m.SetFloat("_Metallic", 0.08f);
                m.SetFloat("_Smoothness", 0.94f);
                m.SetTexture("_BumpMap", hwNorm);
                m.SetFloat("_BumpScale", 0.60f);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.EnableKeyword("_NORMALMAP");
            });

            Texture2D swTex = GenerateSidewalkTexture();
            pack.SidewalkMat = CreateOrUpdateMaterial("NeonCity_Sidewalk", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.70f, 0.74f, 0.80f, 1f));
                m.SetTexture("_BaseMap", swTex);
                m.SetTextureScale("_BaseMap", new Vector2(1f, 24f));
                m.SetFloat("_Smoothness", 0.76f);
                m.SetFloat("_Metallic", 0.10f);
            });

            // 2. Concrete, Weathered Tong Lau, Steel, Curtain-Wall Glass, White X-Brace LED
            pack.DarkConcreteMat = CreateOrUpdateMaterial("NeonCity_DarkConcrete", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.085f, 0.095f, 0.125f, 1f));
                m.SetFloat("_Smoothness", 0.52f);
                m.SetFloat("_Metallic", 0.10f);
            });

            Texture2D tongTex = GenerateTongLauConcreteTexture();
            pack.TongLauConcreteMat = CreateOrUpdateMaterial("NeonCity_TongLauConcrete", urpLit, m =>
            {
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BaseMap", tongTex);
                m.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
                m.SetFloat("_Smoothness", 0.34f);
                m.SetFloat("_Metallic", 0.05f);
            });

            pack.DarkSteelMat = CreateOrUpdateMaterial("NeonCity_DarkSteel", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.055f, 0.068f, 0.098f, 1f));
                m.SetFloat("_Smoothness", 0.74f);
                m.SetFloat("_Metallic", 0.78f);
            });

            pack.DarkGlassMat = CreateOrUpdateMaterial("NeonCity_DarkGlass", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.035f, 0.065f, 0.115f, 1f));
                m.SetFloat("_Smoothness", 0.95f);
                m.SetFloat("_Metallic", 0.62f);
            });

            pack.WhiteLedBraceMat = CreateOrUpdateMaterial("NeonCity_WhiteLedBrace", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.85f, 0.93f, 1.0f, 1f));
                m.SetFloat("_Smoothness", 0.90f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(4.6f, 5.2f, 6.2f, 1f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            });

            pack.BarrierMat = CreateOrUpdateMaterial("NeonCity_Barrier", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.20f, 0.22f, 0.26f, 1f));
                m.SetFloat("_Smoothness", 0.42f);
                m.SetFloat("_Metallic", 0.08f);
            });

            // 3. Hong Kong Collateral Materials (AC Grids, Bamboo Scaffolding, Safety Netting, Skybridges, Helipad, Trams, Tankers)
            Texture2D acTex = GenerateACUnitTexture();
            pack.ACUnitMat = CreateOrUpdateMaterial("NeonCity_ACUnit", urpLit, m =>
            {
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BaseMap", acTex);
                m.SetFloat("_Metallic", 0.45f);
                m.SetFloat("_Smoothness", 0.48f);
            });

            pack.BambooMat = CreateOrUpdateMaterial("NeonCity_Bamboo", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.46f, 0.36f, 0.20f, 1f));
                m.SetFloat("_Smoothness", 0.38f);
                m.SetFloat("_Metallic", 0.04f);
            });

            pack.GreenNettingMat = CreateOrUpdateMaterial("NeonCity_BambooNetting", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.06f, 0.28f, 0.20f, 0.62f));
                m.SetFloat("_Surface", 1f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = 3000;
                m.SetFloat("_Smoothness", 0.25f);
            });

            Texture2D bridgeTex = GenerateSkybridgeGlassTexture(out Texture2D bridgeEmis);
            pack.SkybridgeGlassMat = CreateOrUpdateMaterial("NeonCity_SkybridgeGlass", urpLit, m =>
            {
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BaseMap", bridgeTex);
                m.SetFloat("_Metallic", 0.38f);
                m.SetFloat("_Smoothness", 0.90f);
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", bridgeEmis);
                m.SetColor("_EmissionColor", new Color(1.9f, 1.9f, 1.9f, 1f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            });

            Texture2D heliTex = GenerateHelipadTexture(out Texture2D heliEmis);
            pack.HelipadMat = CreateOrUpdateMaterial("NeonCity_Helipad", urpLit, m =>
            {
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BaseMap", heliTex);
                m.SetFloat("_Smoothness", 0.55f);
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", heliEmis);
                m.SetColor("_EmissionColor", new Color(2.2f, 2.2f, 2.2f, 1f));
            });

            // 4. Window Grid Building Materials (6 HK Styles: Supertall Ice, Financial Amber/Jade, Nightlife Magenta/Cyan, Steel Cobalt, TongLau Warm/Teal, Prism Reflective)
            Color[][] windowPalettes =
            {
                new[] { new Color(0.16f, 0.88f, 1.0f), new Color(0.82f, 0.96f, 1.0f), new Color(0.10f, 0.58f, 0.92f) },
                new[] { new Color(1.00f, 0.72f, 0.25f), new Color(0.18f, 0.92f, 0.78f), new Color(1.00f, 0.36f, 0.26f) },
                new[] { new Color(1.00f, 0.20f, 0.68f), new Color(0.32f, 0.42f, 1.00f), new Color(0.16f, 0.94f, 1.00f) },
                new[] { new Color(0.20f, 0.96f, 0.56f), new Color(1.00f, 0.80f, 0.32f), new Color(0.75f, 0.96f, 0.96f) },
                new[] { new Color(1.00f, 0.78f, 0.42f), new Color(0.28f, 0.88f, 0.72f), new Color(0.95f, 0.48f, 0.32f) },
                new[] { new Color(0.42f, 0.88f, 1.00f), new Color(0.92f, 0.97f, 1.00f), new Color(0.24f, 0.62f, 0.98f) },
            };
            pack.WindowMats = new Material[6];
            for (int i = 0; i < 6; i++)
            {
                bool denseRes = (i == 4);
                bool supertallPrism = (i == 5);
                GenerateWindowGridTextures(i, windowPalettes[i], denseRes, supertallPrism, out Texture2D winAlb, out Texture2D winEmis);
                int idx = i;
                pack.WindowMats[i] = CreateOrUpdateMaterial($"NeonCity_WindowGrid_{idx}", urpLit, m =>
                {
                    m.SetColor("_BaseColor", Color.white);
                    m.SetTexture("_BaseMap", winAlb);
                    m.SetFloat("_Metallic", supertallPrism ? 0.68f : 0.46f);
                    m.SetFloat("_Smoothness", supertallPrism ? 0.94f : 0.88f);
                    m.EnableKeyword("_EMISSION");
                    m.SetTexture("_EmissionMap", winEmis);
                    m.SetColor("_EmissionColor", new Color(supertallPrism ? 1.8f : 1.55f, supertallPrism ? 1.8f : 1.55f, supertallPrism ? 1.9f : 1.55f, 1f));
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                });
            }

            // 5. HDR Neon Tubes
            pack.NeonTubeMats = new Material[NeonHdrPalette.Length];
            for (int i = 0; i < NeonHdrPalette.Length; i++)
            {
                Color hdr = NeonHdrPalette[i];
                Color baseC = new Color(Mathf.Clamp01(hdr.r * 0.22f), Mathf.Clamp01(hdr.g * 0.22f), Mathf.Clamp01(hdr.b * 0.22f), 1f);
                pack.NeonTubeMats[i] = CreateOrUpdateMaterial($"NeonCity_Tube_{i}", urpLit, m =>
                {
                    m.SetColor("_BaseColor", baseC);
                    m.SetFloat("_Smoothness", 0.92f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", hdr);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                });
            }

            // 6. Authentic Traditional Chinese Neon Calligraphy Signs (6 Horizontal + 6 Vertical + 3 Giant Waterfront LEDs + Gantry)
            (Color primary, Color accent, string labelText)[] hkSigns =
            {
                (new Color(1.00f, 0.14f, 0.34f), new Color(0.15f, 0.95f, 1.00f), "龍鳳大酒樓"), // Crimson & Cyan
                (new Color(0.12f, 0.94f, 1.00f), new Color(1.00f, 0.24f, 0.68f), "九龍夜市城"), // Cyan & Magenta
                (new Color(1.00f, 0.20f, 0.72f), new Color(1.00f, 0.76f, 0.18f), "香港大押金"), // Magenta & Amber
                (new Color(0.16f, 0.96f, 0.54f), new Color(1.00f, 0.20f, 0.38f), "東海海鮮舫"), // Jade & Crimson
                (new Color(1.00f, 0.60f, 0.14f), new Color(0.15f, 0.94f, 1.00f), "星皇金融港"), // Amber & Cyan
                (new Color(1.00f, 0.12f, 0.32f), new Color(1.00f, 0.85f, 0.26f), "金福夜總會"), // Crimson & Gold
            };

            pack.SignMats = new Material[hkSigns.Length];
            pack.VerticalSignMats = new Material[hkSigns.Length];
            for (int i = 0; i < hkSigns.Length; i++)
            {
                Texture2D hTex = GenerateChineseNeonSignTexture(i, hkSigns[i].labelText, hkSigns[i].primary, hkSigns[i].accent, vertical: false);
                pack.SignMats[i] = CreateOrUpdateMaterial($"NeonCity_Sign_{i}", urpLit, m =>
                {
                    m.SetColor("_BaseColor", Color.white);
                    m.SetTexture("_BaseMap", hTex);
                    m.SetFloat("_Smoothness", 0.76f);
                    m.EnableKeyword("_EMISSION");
                    m.SetTexture("_EmissionMap", hTex);
                    m.SetColor("_EmissionColor", new Color(2.05f, 2.05f, 2.05f, 1f));
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                });

                Texture2D vTex = GenerateChineseNeonSignTexture(i, hkSigns[i].labelText, hkSigns[i].primary, hkSigns[i].accent, vertical: true);
                pack.VerticalSignMats[i] = CreateOrUpdateMaterial($"NeonCity_VertSign_{i}", urpLit, m =>
                {
                    m.SetColor("_BaseColor", Color.white);
                    m.SetTexture("_BaseMap", vTex);
                    m.SetFloat("_Smoothness", 0.76f);
                    m.EnableKeyword("_EMISSION");
                    m.SetTexture("_EmissionMap", vTex);
                    m.SetColor("_EmissionColor", new Color(2.05f, 2.05f, 2.05f, 1f));
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                });
            }

            // Giant Waterfront Harbour LED Billboards
            (Color bg, Color prim, Color acc, string text)[] ledSpecs =
            {
                (new Color(0.02f, 0.04f, 0.14f, 1f), new Color(0.14f, 0.96f, 1.00f), new Color(1.00f, 0.18f, 0.42f), "維多利亞港"),
                (new Color(0.10f, 0.02f, 0.06f, 1f), new Color(1.00f, 0.16f, 0.36f), new Color(1.00f, 0.76f, 0.20f), "香港金融中心"),
                (new Color(0.02f, 0.08f, 0.08f, 1f), new Color(0.18f, 0.98f, 0.64f), new Color(0.20f, 0.90f, 1.00f), "九龍東海城"),
            };
            pack.WaterfrontLedMats = new Material[ledSpecs.Length];
            for (int i = 0; i < ledSpecs.Length; i++)
            {
                Texture2D ledTex = GenerateWaterfrontLedTexture(i, ledSpecs[i].text, ledSpecs[i].bg, ledSpecs[i].prim, ledSpecs[i].acc);
                pack.WaterfrontLedMats[i] = CreateOrUpdateMaterial($"NeonCity_WaterfrontLED_{i}", urpLit, m =>
                {
                    m.SetColor("_BaseColor", Color.white);
                    m.SetTexture("_BaseMap", ledTex);
                    m.SetFloat("_Smoothness", 0.85f);
                    m.EnableKeyword("_EMISSION");
                    m.SetTexture("_EmissionMap", ledTex);
                    m.SetColor("_EmissionColor", new Color(2.15f, 2.15f, 2.15f, 1f));
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                });
            }

            Texture2D gantryTex = GenerateExpresswayGantryTexture();
            pack.GantrySignMat = CreateOrUpdateMaterial("NeonCity_GantrySign", urpLit, m =>
            {
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BaseMap", gantryTex);
                m.SetFloat("_Smoothness", 0.72f);
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", gantryTex);
                m.SetColor("_EmissionColor", new Color(2.5f, 2.5f, 2.5f, 1f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            });

            // 7. Victoria Harbour Black Choppy Ocean Material
            Material baseOceanMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_Ocean.mat");
            Shader oceanShader = Shader.Find("PacificRim/OceanWater") ?? urpLit;
            pack.HarbourOceanMat = CreateOrUpdateMaterial("NeonCity_HarbourOcean", oceanShader, m =>
            {
                if (baseOceanMat != null && m.shader == baseOceanMat.shader)
                {
                    m.CopyPropertiesFromMaterial(baseOceanMat);
                }
                m.SetColor("_DeepColor", new Color(0.004f, 0.012f, 0.026f, 1.0f));
                m.SetColor("_ShallowColor", new Color(0.012f, 0.068f, 0.115f, 1.0f));
                m.SetColor("_SubsurfaceColor", new Color(0.025f, 0.160f, 0.220f, 1.0f));
                m.SetColor("_FoamColor", new Color(0.76f, 0.85f, 0.92f, 1.0f));
                m.SetColor("_EdgeFogColor", new Color(0.035f, 0.055f, 0.105f, 1.0f));
                m.SetFloat("_Smoothness", 0.96f);
                m.SetFloat("_MicroNormalStrength", 0.26f);
                m.SetFloat("_MeniscusStrength", 0.65f);
                m.SetFloat("_EdgeFadeStart", 0.72f);
                m.SetFloat("_EdgeFadeEnd", 0.99f);
            });

            // 8. Low-Altitude Typhoon Cloud Ceiling & Street Alley Mist Pool (PacificRim/FogWisp)
            Material baseFogMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_FogWisp.mat");
            Shader fogShader = Shader.Find("PacificRim/FogWisp") ?? urpLit;
            Texture mistNoiseTex = baseFogMat != null ? baseFogMat.GetTexture("_MainTex") : null;
            pack.CloudCeilingMat = CreateOrUpdateMaterial("NeonCity_StormCloudCeiling", fogShader, m =>
            {
                if (mistNoiseTex != null) m.SetTexture("_MainTex", mistNoiseTex);
                m.SetColor("_TintColor", new Color(0.06f, 0.10f, 0.19f, 0.90f));
                m.SetFloat("_InvFade", 0.025f);
                m.SetFloat("_ScrollSpeed", 0.08f);
            });

            pack.MistPoolMat = CreateOrUpdateMaterial("NeonCity_StreetMistPool", fogShader, m =>
            {
                if (mistNoiseTex != null) m.SetTexture("_MainTex", mistNoiseTex);
                m.SetColor("_TintColor", new Color(0.08f, 0.15f, 0.26f, 0.42f));
                m.SetFloat("_InvFade", 0.10f);
                m.SetFloat("_ScrollSpeed", 0.15f);
            });

            // 9. Volumetric Billboard Light Beams (PacificRim/VolumetricLightBeam)
            Shader beamShader = Shader.Find("PacificRim/VolumetricLightBeam") ?? urpLit;
            Color[] beamColors =
            {
                new Color(1.00f, 0.18f, 0.38f, 0.30f), // Crimson
                new Color(0.14f, 0.92f, 1.00f, 0.30f), // Cyan
                new Color(1.00f, 0.56f, 0.14f, 0.26f), // Amber
                new Color(0.95f, 0.20f, 0.78f, 0.28f), // Magenta
            };
            pack.VolumetricBeamMats = new Material[beamColors.Length];
            for (int i = 0; i < beamColors.Length; i++)
            {
                Color c = beamColors[i];
                pack.VolumetricBeamMats[i] = CreateOrUpdateMaterial($"NeonCity_VolBeam_{i}", beamShader, m =>
                {
                    m.SetColor("_Color", c);
                    m.SetFloat("_Intensity", 1.85f);
                    m.SetFloat("_ConeApexFade", 0.08f);
                    m.SetFloat("_ConeBaseFade", 0.24f);
                    m.SetFloat("_RimPower", 2.0f);
                    m.SetFloat("_NoiseSpeed", 1.4f);
                    m.SetFloat("_NoiseScale", 7.5f);
                    m.SetFloat("_NoiseStrength", 0.32f);
                    m.SetFloat("_WaterLevel", -50f); // Do not clip elevated city billboards at ocean level
                    m.SetFloat("_StreakDensity", 8.0f);
                    m.SetFloat("_StreakContrast", 0.45f);
                });
            }

            // 10. Collateral Traffic, Tankers, Double-Decker Trams
            pack.CarBodyMats = new[]
            {
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/City_CarSedan.mat") ?? pack.DarkSteelMat,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/City_CarSUV.mat") ?? pack.DarkSteelMat,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/City/City_CarCompact.mat") ?? pack.DarkSteelMat,
            };
            pack.TailLightMat = pack.NeonTubeMats[5];
            pack.HeadLightMat = pack.NeonTubeMats[2];
            pack.TankerHazardMat = CreateOrUpdateMaterial("NeonCity_TankerHazard", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.74f, 0.16f, 0.12f, 1f));
                m.SetFloat("_Smoothness", 0.62f);
                m.SetFloat("_Metallic", 0.48f);
            });
            pack.TramGreenMat = CreateOrUpdateMaterial("NeonCity_HKTramGreen", urpLit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.08f, 0.32f, 0.19f, 1f));
                m.SetFloat("_Smoothness", 0.58f);
                m.SetFloat("_Metallic", 0.35f);
            });

            AssetDatabase.SaveAssets();
            return pack;
        }

        #region Custom Architectural Meshes

        private static Mesh EnsureTriangularPrismMesh()
        {
            string path = $"{MeshFolder}/NeonCity_TriangularPrism.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing, true);
            }

            // Quarter-square wedge prism around (0, 0) in [-0.5, 0.5] x [0, 1] x [-0.5, 0.5]
            // Four copies rotated 0/90/180/270 around (0,0) form a watertight Bank of China square prism base!
            // Base triangle vertices: L(-0.5, -0.5), R(+0.5, -0.5), Center(0, 0)
            Mesh mesh = new Mesh { name = "NeonCity_TriangularPrism" };
            Vector3[] verts =
            {
                // Outer South Facade (L -> R)
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 1f, -0.5f), new Vector3(-0.5f, 1f, -0.5f),
                // Inner Diagonal Wall (Center -> L)
                new Vector3(0f, 0f, 0f), new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 1f, -0.5f), new Vector3(0f, 1f, 0f),
                // Inner Diagonal Wall (R -> Center)
                new Vector3(0.5f, 0f, -0.5f), new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0.5f, 1f, -0.5f),
                // Top Cap (L, Center, R)
                new Vector3(-0.5f, 1f, -0.5f), new Vector3(0f, 1f, 0f), new Vector3(0.5f, 1f, -0.5f),
                // Bottom Cap
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0f, 0f, 0f),
            };
            Vector2[] uvs =
            {
                new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
                new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
                new Vector2(0,0), new Vector2(1.4f,0), new Vector2(1.4f,1), new Vector2(0,1),
                new Vector2(0,0), new Vector2(0,1), new Vector2(1,0),
                new Vector2(0,0), new Vector2(1,0), new Vector2(0,1),
            };
            int[] tris =
            {
                0, 2, 1, 0, 3, 2,
                4, 6, 5, 4, 7, 6,
                8, 10, 9, 8, 11, 10,
                12, 14, 13,
                15, 16, 17
            };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        private static Mesh EnsureOctagonPrismMesh()
        {
            string path = $"{MeshFolder}/NeonCity_OctagonPrism.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing, true);
            }

            const int sides = 8;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float r = 0.5f;
            float angleStep = Mathf.PI * 2f / sides;
            float angleOffset = angleStep * 0.5f;

            for (int i = 0; i < sides; i++)
            {
                float a0 = i * angleStep + angleOffset;
                float a1 = (i + 1) * angleStep + angleOffset;
                Vector3 p0B = new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
                Vector3 p1B = new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
                Vector3 p1T = new Vector3(p1B.x, 1f, p1B.z);
                Vector3 p0T = new Vector3(p0B.x, 1f, p0B.z);

                int b = verts.Count;
                verts.Add(p0B); verts.Add(p1B); verts.Add(p1T); verts.Add(p0T);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0.45f, 0f));
                uvs.Add(new Vector2(0.45f, 1f)); uvs.Add(new Vector2(0f, 1f));
                tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 0); tris.Add(b + 3); tris.Add(b + 2);
            }

            // Top cap fan
            int topCenter = verts.Count;
            verts.Add(new Vector3(0f, 1f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < sides; i++)
            {
                float a = i * angleStep + angleOffset;
                verts.Add(new Vector3(Mathf.Cos(a) * r, 1f, Mathf.Sin(a) * r));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int i = 0; i < sides; i++)
            {
                int iNext = (i + 1) % sides;
                tris.Add(topCenter);
                tris.Add(topCenter + 1 + iNext);
                tris.Add(topCenter + 1 + i);
            }

            Mesh mesh = new Mesh { name = "NeonCity_OctagonPrism" };
            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        private static Mesh EnsureCurvedArcTowerMesh()
        {
            string path = $"{MeshFolder}/NeonCity_CurvedArcTower.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing, true);
            }

            // Swept elliptical cylinder / crescent-like modern HK waterfront slab normalized in [-0.5, 0.5] x [0, 1]
            const int segs = 20;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float rx = 0.5f;
            float rz = 0.36f;
            float step = Mathf.PI * 2f / segs;

            for (int i = 0; i < segs; i++)
            {
                float a0 = i * step;
                float a1 = (i + 1) * step;
                // Slightly flatter crescent modulation
                float bulge0 = 1f + 0.16f * Mathf.Cos(a0 * 2f);
                float bulge1 = 1f + 0.16f * Mathf.Cos(a1 * 2f);
                Vector3 p0B = new Vector3(Mathf.Cos(a0) * rx, 0f, Mathf.Sin(a0) * rz * bulge0);
                Vector3 p1B = new Vector3(Mathf.Cos(a1) * rx, 0f, Mathf.Sin(a1) * rz * bulge1);
                Vector3 p1T = new Vector3(p1B.x, 1f, p1B.z);
                Vector3 p0T = new Vector3(p0B.x, 1f, p0B.z);

                int b = verts.Count;
                verts.Add(p0B); verts.Add(p1B); verts.Add(p1T); verts.Add(p0T);
                float u0 = (float)i / segs * 2f;
                float u1 = (float)(i + 1) / segs * 2f;
                uvs.Add(new Vector2(u0, 0f)); uvs.Add(new Vector2(u1, 0f));
                uvs.Add(new Vector2(u1, 1f)); uvs.Add(new Vector2(u0, 1f));
                tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 0); tris.Add(b + 3); tris.Add(b + 2);
            }

            int topCenter = verts.Count;
            verts.Add(new Vector3(0f, 1f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < segs; i++)
            {
                float a = i * step;
                float bulge = 1f + 0.16f * Mathf.Cos(a * 2f);
                verts.Add(new Vector3(Mathf.Cos(a) * rx, 1f, Mathf.Sin(a) * rz * bulge));
                uvs.Add(new Vector2(0.5f, 0.5f));
            }
            for (int i = 0; i < segs; i++)
            {
                int iNext = (i + 1) % segs;
                tris.Add(topCenter);
                tris.Add(topCenter + 1 + iNext);
                tris.Add(topCenter + 1 + i);
            }

            Mesh mesh = new Mesh { name = "NeonCity_CurvedArcTower" };
            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        #endregion

        #region Procedural Textures

        private static void GenerateHighwayTextures(out Texture2D albedo, out Texture2D smoothness, out Texture2D normal, out Texture2D emission)
        {
            const int w = 1024;
            const int h = 1024;

            var albTex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            var smTex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            var emTex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            float[,] heightField = new float[h, w];

            Color baseAsphalt = new Color(0.060f, 0.066f, 0.082f, 1f);
            Color wornTrack = new Color(0.042f, 0.048f, 0.062f, 1f);
            Color whiteLine = new Color(0.84f, 0.88f, 0.92f, 1f);
            Color amberLine = new Color(0.96f, 0.68f, 0.12f, 1f);

            float leftStart = 0.025f;
            float leftEnd = 0.482f;
            float rightStart = 0.518f;
            float rightEnd = 0.975f;
            float laneSpanL = (leftEnd - leftStart) * 0.25f;
            float laneSpanR = (rightEnd - rightStart) * 0.25f;

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;

                    float grain = TileableNoise(u * 48f, v * 48f, 48, 13f);
                    float macro = TileableNoise(u * 6f, v * 6f, 6, 71f);
                    float puddleMask = Mathf.SmoothStep(0.42f, 0.64f, TileableNoise(u * 5f, v * 5f, 5, 29f));

                    float laneDist = 1f;
                    if (u > leftStart && u < leftEnd)
                    {
                        float inLane = ((u - leftStart) % laneSpanL) / laneSpanL;
                        laneDist = Mathf.Abs(inLane - 0.5f);
                    }
                    else if (u > rightStart && u < rightEnd)
                    {
                        float inLane = ((u - rightStart) % laneSpanR) / laneSpanR;
                        laneDist = Mathf.Abs(inLane - 0.5f);
                    }
                    float wear = Mathf.Exp(-laneDist * laneDist * 18f) * 0.65f;

                    Color col = Color.Lerp(baseAsphalt, wornTrack, wear * 0.55f + macro * 0.35f);
                    float speck = (grain - 0.5f) * 0.026f;
                    col.r = Mathf.Clamp01(col.r + speck);
                    col.g = Mathf.Clamp01(col.g + speck);
                    col.b = Mathf.Clamp01(col.b + speck * 1.1f);

                    float smoothVal = Mathf.Lerp(0.58f + wear * 0.16f, 0.97f, puddleMask);
                    float heightVal = Mathf.Lerp(grain * 0.40f + macro * 0.18f, 0.04f, puddleMask);
                    Color emCol = Color.black;

                    if (Mathf.Abs(u - leftStart) < 0.0045f || Mathf.Abs(u - rightEnd) < 0.0045f)
                    {
                        col = Color.Lerp(col, whiteLine, 0.92f);
                        smoothVal = Mathf.Max(smoothVal, 0.78f);
                    }

                    if (Mathf.Abs(u - 0.488f) < 0.0040f || Mathf.Abs(u - 0.512f) < 0.0040f)
                    {
                        col = Color.Lerp(col, amberLine, 0.95f);
                        smoothVal = Mathf.Max(smoothVal, 0.80f);
                    }

                    if (u > 0.491f && u < 0.509f)
                    {
                        float diag = Mathf.Repeat(v * 16f + (u - 0.5f) * 40f, 1f);
                        if (diag < 0.22f) col = Color.Lerp(col, amberLine * 0.85f, 0.75f);
                    }

                    bool dashOn = Mathf.Repeat(v * 8f, 1f) < 0.46f;
                    if (dashOn)
                    {
                        for (int l = 1; l <= 3; l++)
                        {
                            float dL = leftStart + l * laneSpanL;
                            float dR = rightStart + l * laneSpanR;
                            if (Mathf.Abs(u - dL) < 0.0032f || Mathf.Abs(u - dR) < 0.0032f)
                            {
                                col = Color.Lerp(col, whiteLine, 0.90f);
                                smoothVal = Mathf.Max(smoothVal, 0.76f);
                            }
                        }
                    }

                    // Rain-slicked street tram rails running softly along inner-outer lanes for HK detail
                    if (Mathf.Abs(u - (leftStart + 0.055f)) < 0.0018f || Mathf.Abs(u - (leftStart + 0.072f)) < 0.0018f ||
                        Mathf.Abs(u - (rightEnd - 0.055f)) < 0.0018f || Mathf.Abs(u - (rightEnd - 0.072f)) < 0.0018f)
                    {
                        col = Color.Lerp(col, new Color(0.32f, 0.38f, 0.45f, 1f), 0.75f);
                        smoothVal = 0.95f;
                    }

                    float studPhase = Mathf.Abs(Mathf.Repeat(v * 12f, 1f) - 0.5f);
                    if (studPhase < 0.025f)
                    {
                        if (Mathf.Abs(u - 0.500f) < 0.0032f)
                        {
                            emCol = new Color(1.0f, 0.62f, 0.12f, 1f);
                        }
                        else if (Mathf.Abs(u - (leftStart + 0.008f)) < 0.0028f || Mathf.Abs(u - (rightEnd - 0.008f)) < 0.0028f)
                        {
                            emCol = new Color(0.18f, 0.88f, 1.0f, 1f);
                        }
                    }

                    albTex.SetPixel(x, y, col);
                    smTex.SetPixel(x, y, new Color(0.06f, 0.06f, 0.06f, smoothVal));
                    emTex.SetPixel(x, y, emCol);
                    heightField[y, x] = heightVal;
                }
            }

            albTex.Apply();
            smTex.Apply();
            emTex.Apply();

            Texture2D normTex = BuildNormalMap(heightField, 1.6f);

            albedo = SavePngAsset(albTex, $"{TextureFolder}/NeonCity_Highway_Albedo.png", false);
            smoothness = SavePngAsset(smTex, $"{TextureFolder}/NeonCity_Highway_Smoothness.png", false);
            normal = SavePngAsset(normTex, $"{TextureFolder}/NeonCity_Highway_Normal.png", true);
            emission = SavePngAsset(emTex, $"{TextureFolder}/NeonCity_Highway_Emission.png", false);
        }

        private static Texture2D GenerateIntersectionTexture()
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            Color roadCol = new Color(0.058f, 0.065f, 0.080f, 1f);
            Color stripeCol = new Color(0.84f, 0.86f, 0.90f, 1f);
            Color boxJunction = new Color(0.82f, 0.58f, 0.10f, 1f); // HK yellow crisscross box junction!

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float n = (TileableNoise(u * 24f, v * 24f, 24, 9f) - 0.5f) * 0.022f;
                    Color c = new Color(roadCol.r + n, roadCol.g + n, roadCol.b + n, 1f);

                    bool inCrosswalkV = (v > 0.05f && v < 0.16f) || (v > 0.84f && v < 0.95f);
                    if (inCrosswalkV && u > 0.04f && u < 0.96f)
                    {
                        if (Mathf.Repeat(u * 22f, 1f) < 0.48f)
                            c = Color.Lerp(c, stripeCol, 0.88f);
                    }

                    if ((Mathf.Abs(v - 0.025f) < 0.01f || Mathf.Abs(v - 0.975f) < 0.01f) && u > 0.03f && u < 0.97f)
                        c = Color.Lerp(c, stripeCol, 0.85f);

                    // Classic Hong Kong yellow hatched box junction in center of intersection
                    if (u > 0.18f && u < 0.82f && v > 0.22f && v < 0.78f)
                    {
                        float d1 = Mathf.Repeat((u + v) * 8f, 1f);
                        float d2 = Mathf.Repeat((u - v + 2f) * 8f, 1f);
                        bool border = Mathf.Abs(u - 0.18f) < 0.01f || Mathf.Abs(u - 0.82f) < 0.01f ||
                                      Mathf.Abs(v - 0.22f) < 0.01f || Mathf.Abs(v - 0.78f) < 0.01f;
                        if (border || d1 < 0.06f || d2 < 0.06f)
                            c = Color.Lerp(c, boxJunction, 0.72f);
                    }

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return SavePngAsset(tex, $"{TextureFolder}/NeonCity_Intersection_Albedo.png", false);
        }

        private static Texture2D GenerateSidewalkTexture()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            Color slabA = new Color(0.14f, 0.15f, 0.18f, 1f);
            Color groove = new Color(0.06f, 0.07f, 0.09f, 1f);

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float n = (TileableNoise(u * 12f, v * 12f, 12, 3f) - 0.5f) * 0.03f;
                    bool seam = (Mathf.Repeat(u * 4f, 1f) < 0.05f) || (Mathf.Repeat(v * 4f, 1f) < 0.05f);
                    Color c = seam ? groove : new Color(slabA.r + n, slabA.g + n, slabA.b + n, 1f);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return SavePngAsset(tex, $"{TextureFolder}/NeonCity_Sidewalk_Albedo.png", false);
        }

        private static Texture2D GenerateTongLauConcreteTexture()
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            Color baseC = new Color(0.15f, 0.16f, 0.18f, 1f);
            Color stainC = new Color(0.08f, 0.09f, 0.10f, 1f);
            Color windowDark = new Color(0.04f, 0.05f, 0.07f, 1f);
            Color windowWarm = new Color(0.45f, 0.36f, 0.20f, 1f);

            var rng = new System.Random(3137);
            bool[,] lit = new bool[10, 6];
            for (int r = 0; r < 10; r++)
                for (int c = 0; c < 6; c++)
                    lit[r, c] = rng.NextDouble() < 0.24;

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                float ry = v * 10f;
                int row = Mathf.Clamp(Mathf.FloorToInt(ry), 0, 9);
                float cy = ry - row;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float fx = u * 6f;
                    int col = Mathf.Clamp(Mathf.FloorToInt(fx), 0, 5);
                    float cx = fx - col;

                    float grime = TileableNoise(u * 6f, v * 16f, 6, 41f);
                    float streak = TileableNoise(u * 20f, v * 3f, 20, 17f);
                    Color c = Color.Lerp(baseC, stainC, grime * 0.55f + streak * 0.45f);

                    // Window openings per apartment bay
                    if (cx > 0.18f && cx < 0.82f && cy > 0.28f && cy < 0.78f)
                    {
                        bool mullion = Mathf.Abs(cx - 0.50f) < 0.04f;
                        c = mullion ? stainC : (lit[row, col] ? windowWarm : windowDark);
                    }
                    // Balcony concrete sill band
                    else if (cy < 0.12f)
                    {
                        c *= 0.72f;
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return SavePngAsset(tex, $"{TextureFolder}/NeonCity_TongLauConcrete.png", false);
        }

        private static Texture2D GenerateACUnitTexture()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            Color casing = new Color(0.32f, 0.34f, 0.36f, 1f);
            Color grille = new Color(0.08f, 0.09f, 0.11f, 1f);

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    bool inFan = u > 0.12f && u < 0.65f && v > 0.15f && v < 0.85f;
                    bool louver = Mathf.Repeat(v * 22f, 1f) < 0.42f;
                    Color c = (inFan && louver) ? grille : casing;
                    if (inFan && !louver)
                    {
                        float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.385f, 0.5f));
                        if (d < 0.22f) c = Color.Lerp(c, grille, 0.55f);
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return SavePngAsset(tex, $"{TextureFolder}/NeonCity_ACUnit.png", false);
        }

        private static Texture2D GenerateSkybridgeGlassTexture(out Texture2D emission)
        {
            const int w = 512;
            const int h = 256;
            var alb = new Texture2D(w, h, TextureFormat.RGBA32, true);
            var em = new Texture2D(w, h, TextureFormat.RGBA32, true);

            Color steel = new Color(0.05f, 0.06f, 0.09f, 1f);
            Color glass = new Color(0.10f, 0.22f, 0.34f, 1f);
            Color glowCyan = new Color(0.20f, 0.85f, 1.00f, 1f);
            Color glowWarm = new Color(1.00f, 0.78f, 0.42f, 1f);

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    bool trussTopBot = v < 0.18f || v > 0.82f;
                    bool mullion = Mathf.Repeat(u * 12f, 1f) < 0.12f;
                    if (trussTopBot || mullion)
                    {
                        alb.SetPixel(x, y, steel);
                        em.SetPixel(x, y, Color.black);
                    }
                    else
                    {
                        Color glow = (v > 0.58f) ? glowCyan * 0.75f : glowWarm * 0.65f;
                        alb.SetPixel(x, y, Color.Lerp(glass, glow, 0.45f));
                        em.SetPixel(x, y, glow * (0.65f + 0.35f * Mathf.Sin(v * Mathf.PI)));
                    }
                }
            }
            alb.Apply();
            em.Apply();
            emission = SavePngAsset(em, $"{TextureFolder}/NeonCity_SkybridgeGlass_Emis.png", false);
            return SavePngAsset(alb, $"{TextureFolder}/NeonCity_SkybridgeGlass_Alb.png", false);
        }

        private static Texture2D GenerateHelipadTexture(out Texture2D emission)
        {
            const int size = 512;
            var alb = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var em = new Texture2D(size, size, TextureFormat.RGBA32, true);
            Color pad = new Color(0.10f, 0.11f, 0.13f, 1f);
            Color amber = new Color(1.00f, 0.72f, 0.15f, 1f);
            Color cyan = new Color(0.16f, 0.92f, 1.00f, 1f);

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                    Color a = pad;
                    Color e = Color.black;

                    if (Mathf.Abs(d - 0.39f) < 0.025f)
                    {
                        a = amber;
                        e = amber * 0.85f;
                    }
                    // Bold 'H' in center
                    bool leftBar = Mathf.Abs(u - 0.36f) < 0.032f && v > 0.30f && v < 0.70f;
                    bool rightBar = Mathf.Abs(u - 0.64f) < 0.032f && v > 0.30f && v < 0.70f;
                    bool midBar = Mathf.Abs(v - 0.50f) < 0.032f && u > 0.36f && u < 0.64f;
                    if (leftBar || rightBar || midBar)
                    {
                        a = cyan;
                        e = cyan * 0.9f;
                    }
                    alb.SetPixel(x, y, a);
                    em.SetPixel(x, y, e);
                }
            }
            alb.Apply();
            em.Apply();
            emission = SavePngAsset(em, $"{TextureFolder}/NeonCity_Helipad_Emis.png", false);
            return SavePngAsset(alb, $"{TextureFolder}/NeonCity_Helipad_Alb.png", false);
        }

        private static void GenerateWindowGridTextures(int styleIndex, Color[] palette, bool denseResidential, bool supertallPrism, out Texture2D albedo, out Texture2D emission)
        {
            const int size = 1024;
            var alb = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var em = new Texture2D(size, size, TextureFormat.RGBA32, true);

            int cols = denseResidential ? 20 : (supertallPrism ? 24 : 28);
            int rows = denseResidential ? 40 : (supertallPrism ? 48 : 34);
            Color frameCol = new Color(0.042f, 0.052f, 0.075f, 1f);
            Color spandrelCol = supertallPrism ? new Color(0.052f, 0.075f, 0.115f, 1f) : new Color(0.035f, 0.040f, 0.055f, 1f);
            Color darkGlass = supertallPrism ? new Color(0.038f, 0.068f, 0.115f, 1f) : new Color(0.024f, 0.036f, 0.060f, 1f);

            var rng = new System.Random(104729 + styleIndex * 7919);
            bool[,] lit = new bool[rows, cols];
            Color[,] winColors = new Color[rows, cols];
            float[,] winBright = new float[rows, cols];

            for (int r = 0; r < rows; r++)
            {
                bool bandLit = supertallPrism ? (r % 8 == 3) : ((r % 6 == 2) || rng.NextDouble() < 0.18);
                for (int c = 0; c < cols; c++)
                {
                    double chance = denseResidential ? 0.36 : (bandLit ? 0.68 : 0.25);
                    bool isLit = rng.NextDouble() < chance;
                    lit[r, c] = isLit;
                    winColors[r, c] = palette[rng.Next(palette.Length)];
                    winBright[r, c] = (denseResidential ? 0.50f : 0.42f) + (float)rng.NextDouble() * 0.55f;
                }
            }

            for (int y = 0; y < size; y++)
            {
                float fy = (float)y / size * rows;
                int r = Mathf.Clamp(Mathf.FloorToInt(fy), 0, rows - 1);
                float cellY = fy - r;

                for (int x = 0; x < size; x++)
                {
                    float fx = (float)x / size * cols;
                    int c = Mathf.Clamp(Mathf.FloorToInt(fx), 0, cols - 1);
                    float cellX = fx - c;

                    bool isSpandrel = cellY < 0.22f || cellY > 0.90f;
                    bool isMullion = cellX < 0.14f || cellX > 0.86f;
                    if (isSpandrel || isMullion)
                    {
                        alb.SetPixel(x, y, isSpandrel ? spandrelCol : frameCol);
                        em.SetPixel(x, y, Color.black);
                    }
                    else if (lit[r, c])
                    {
                        Color wc = winColors[r, c] * winBright[r, c];
                        float shade = Mathf.Lerp(0.62f, 1.05f, cellY);
                        Color finalEm = wc * shade;
                        finalEm.a = 1f;
                        alb.SetPixel(x, y, Color.Lerp(darkGlass, wc, 0.38f));
                        em.SetPixel(x, y, finalEm);
                    }
                    else
                    {
                        alb.SetPixel(x, y, darkGlass);
                        em.SetPixel(x, y, Color.black);
                    }
                }
            }

            alb.Apply();
            em.Apply();

            albedo = SavePngAsset(alb, $"{TextureFolder}/NeonCity_WindowGrid_{styleIndex}_Albedo.png", false);
            emission = SavePngAsset(em, $"{TextureFolder}/NeonCity_WindowGrid_{styleIndex}_Emission.png", false);
        }

        private static Texture2D GenerateChineseNeonSignTexture(int index, string text, Color primary, Color accent, bool vertical)
        {
            int w = vertical ? 256 : 512;
            int h = vertical ? 512 : 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            FillSteelSignBacking(tex);

            // Neon Outer Double Tube Frame
            DrawNeonRectOutline(tex, 12, 12, w - 13, h - 13, 6f, primary);
            DrawNeonRectOutline(tex, 26, 26, w - 27, h - 27, 3f, accent);

            int len = Mathf.Min(text.Length, 5);
            if (vertical)
            {
                // Top ornamental emblem ring (pawn shop coin / dragon crest)
                DrawNeonRing(tex, w * 0.5f, h - 72f, 34f, 4.5f, accent);
                DrawNeonRing(tex, w * 0.5f, h - 72f, 18f, 3.0f, primary);

                float charH = 66f;
                float charW = w - 96f;
                float startY = h - 136f;
                float stepY = 78f;
                for (int i = 0; i < len; i++)
                {
                    float yBottom = startY - i * stepY - charH * 0.5f;
                    DrawHanziNeonCharacter(tex, text[i], new Rect(48f, Mathf.Max(34f, yBottom), charW, charH), (i % 2 == 0) ? primary : accent, 4.2f);
                }
            }
            else
            {
                // Left crest ring
                DrawNeonRing(tex, 90f, h * 0.5f, 52f, 5.0f, accent);
                DrawNeonRing(tex, 90f, h * 0.5f, 30f, 3.5f, primary);

                float areaLeft = 166f;
                float areaW = w - 196f;
                float stepX = areaW / Mathf.Max(1, len);
                float charSize = Mathf.Min(stepX * 0.86f, h - 78f);
                float cy = (h - charSize) * 0.5f;
                for (int i = 0; i < len; i++)
                {
                    float cx = areaLeft + i * stepX + (stepX - charSize) * 0.5f;
                    DrawHanziNeonCharacter(tex, text[i], new Rect(cx, cy, charSize, charSize), (i == 0) ? accent : primary, 4.5f);
                }
            }

            tex.Apply();
            string prefix = vertical ? "NeonCity_VertSign" : "NeonCity_Sign";
            return SavePngAsset(tex, $"{TextureFolder}/{prefix}_{index}.png", false);
        }

        private static Texture2D GenerateWaterfrontLedTexture(int index, string text, Color bgTint, Color primary, Color accent)
        {
            const int w = 1024;
            const int h = 384;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    float grad = Mathf.SmoothStep(0f, 1f, u * 0.6f + v * 0.4f);
                    Color c = Color.Lerp(bgTint, bgTint * 2.2f + primary * 0.14f, grad);
                    // LED pixel matrix scanlines
                    if ((y % 4) == 0 || (x % 4) == 0) c *= 0.65f;
                    tex.SetPixel(x, y, c);
                }
            }

            DrawNeonRectOutline(tex, 14, 14, w - 15, h - 15, 7f, primary);
            DrawNeonRectOutline(tex, 32, 32, w - 33, h - 33, 3.5f, accent);

            // Futuristic graphic diagonal light slashes on left
            for (int s = 0; s < 3; s++)
            {
                float sx = 80f + s * 44f;
                DrawNeonStroke(tex, new Vector2(sx, 70f), new Vector2(sx + 58f, h - 70f), 7f, s == 1 ? accent : primary);
            }

            // Bold Traditional Chinese characters across the giant harbour screen
            int len = Mathf.Min(text.Length, 6);
            float left = 250f;
            float availW = w - 290f;
            float step = availW / Mathf.Max(1, len);
            float sz = Mathf.Min(step * 0.84f, h - 110f);
            float topBottomY = (h - sz) * 0.5f;
            for (int i = 0; i < len; i++)
            {
                float cx = left + i * step + (step - sz) * 0.5f;
                DrawHanziNeonCharacter(tex, text[i], new Rect(cx, topBottomY, sz, sz), (i % 2 == 0) ? primary : accent, 6.2f);
            }

            tex.Apply();
            return SavePngAsset(tex, $"{TextureFolder}/NeonCity_WaterfrontLED_{index}.png", false);
        }

        private static Texture2D GenerateExpresswayGantryTexture()
        {
            const int w = 512;
            const int h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            Color bg = new Color(0.018f, 0.22f, 0.10f, 1f);
            Color white = new Color(0.95f, 0.98f, 1.0f, 1f);
            Color blueBadge = new Color(0.10f, 0.45f, 0.95f, 1f);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, bg);

            DrawNeonRectOutline(tex, 10, 10, w - 11, h - 11, 5f, white);
            DrawFilledRect(tex, 38, 160, 145, 214, blueBadge);
            DrawRectOutline(tex, 38, 160, 145, 214, 3, white);

            // Hong Kong characters on expressway directional board ("香港 中環")
            string dirChars = "香港中環";
            for (int i = 0; i < 4; i++)
            {
                DrawHanziNeonCharacter(tex, dirChars[i], new Rect(170f + i * 74f, 148f, 58f, 66f), white, 3.6f);
            }

            for (int a = 0; a < 3; a++)
            {
                int cx = 120 + a * 136;
                DrawFilledRect(tex, cx - 6, 60, cx + 6, 114, white);
                for (int row = 0; row < 24; row++)
                    DrawFilledRect(tex, cx - row, 38 + row, cx + row, 39 + row, white);
            }

            tex.Apply();
            return SavePngAsset(tex, $"{TextureFolder}/NeonCity_Sign_5.png", false);
        }

        internal static void FillSteelSignBacking(Texture2D tex)
        {
            Color bg = new Color(0.018f, 0.022f, 0.042f, 1f);
            Color grid = new Color(0.045f, 0.055f, 0.085f, 1f);
            for (int y = 0; y < tex.height; y++)
            {
                bool gH = (y % 18) < 2;
                for (int x = 0; x < tex.width; x++)
                {
                    bool gV = (x % 18) < 2;
                    tex.SetPixel(x, y, (gH || gV) ? grid : bg);
                }
            }
        }

        internal static void DrawNeonRectOutline(Texture2D tex, int x0, int y0, int x1, int y1, float width, Color neonCol)
        {
            DrawNeonStroke(tex, new Vector2(x0, y0), new Vector2(x1, y0), width, neonCol);
            DrawNeonStroke(tex, new Vector2(x0, y1), new Vector2(x1, y1), width, neonCol);
            DrawNeonStroke(tex, new Vector2(x0, y0), new Vector2(x0, y1), width, neonCol);
            DrawNeonStroke(tex, new Vector2(x1, y0), new Vector2(x1, y1), width, neonCol);
        }

        internal static void DrawNeonRing(Texture2D tex, float cx, float cy, float radius, float width, Color neonCol)
        {
            int pad = Mathf.CeilToInt(radius + width * 2.5f);
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - pad));
            int maxX = Mathf.Min(tex.width - 1, Mathf.CeilToInt(cx + pad));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - pad));
            int maxY = Mathf.Min(tex.height - 1, Mathf.CeilToInt(cy + pad));

            Color coreWhite = Color.Lerp(neonCol, Color.white, 0.38f);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float d = Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - radius);
                    if (d > width * 2.2f) continue;
                    Color baseP = tex.GetPixel(x, y);
                    Color add = ComputeTubeSample(d, width, neonCol, coreWhite);
                    tex.SetPixel(x, y, BlendNeonMax(baseP, add));
                }
            }
        }

        internal static void DrawHanziNeonCharacter(Texture2D tex, char ch, Rect rect, Color neonCol, float strokeRadius)
        {
            if (!HanziStrokes.TryGetValue(ch, out float[][] strokes))
            {
                strokes = HanziStrokes['港'];
            }
            for (int i = 0; i < strokes.Length; i++)
            {
                float[] s = strokes[i];
                Vector2 a = new Vector2(rect.x + s[0] * rect.width, rect.y + s[1] * rect.height);
                Vector2 b = new Vector2(rect.x + s[2] * rect.width, rect.y + s[3] * rect.height);
                DrawNeonStroke(tex, a, b, strokeRadius * 0.85f, neonCol);
            }
        }

        internal static void DrawNeonStroke(Texture2D tex, Vector2 a, Vector2 b, float radius, Color neonCol)
        {
            float pad = radius * 2.2f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - pad));
            int maxX = Mathf.Min(tex.width - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + pad));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - pad));
            int maxY = Mathf.Min(tex.height - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + pad));

            Vector2 ab = b - a;
            float abSq = Mathf.Max(0.0001f, Vector2.Dot(ab, ab));
            Color coreWhite = Color.Lerp(neonCol, Color.white, 0.38f);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abSq);
                    float dist = Vector2.Distance(p, a + ab * t);
                    if (dist > pad) continue;
                    Color prev = tex.GetPixel(x, y);
                    Color sample = ComputeTubeSample(dist, radius, neonCol, coreWhite);
                    tex.SetPixel(x, y, BlendNeonMax(prev, sample));
                }
            }
        }

        private static Color ComputeTubeSample(float dist, float radius, Color neonCol, Color coreWhite)
        {
            if (dist <= radius * 0.48f) return coreWhite;
            if (dist <= radius) return Color.Lerp(coreWhite, neonCol, (dist - radius * 0.48f) / Mathf.Max(0.01f, radius * 0.52f));
            float halo = Mathf.Exp(-(dist - radius) / Mathf.Max(0.5f, radius * 0.55f)) * 0.65f;
            return new Color(neonCol.r * halo, neonCol.g * halo, neonCol.b * halo, 1f);
        }

        private static Color BlendNeonMax(Color prev, Color add)
        {
            return new Color(Mathf.Max(prev.r, add.r), Mathf.Max(prev.g, add.g), Mathf.Max(prev.b, add.b), 1f);
        }

        private static Dictionary<char, float[][]> BuildHanziGlyphLibrary()
        {
            // Normalized [0..1] (x right, y up) calligraphic stroke definitions for Traditional Chinese HK characters
            return new Dictionary<char, float[][]>
            {
                ['香'] = new[] {
                    new[] { 0.75f, 0.90f, 0.28f, 0.80f }, new[] { 0.14f, 0.68f, 0.86f, 0.68f }, new[] { 0.50f, 0.84f, 0.50f, 0.48f },
                    new[] { 0.50f, 0.68f, 0.16f, 0.46f }, new[] { 0.50f, 0.68f, 0.84f, 0.46f },
                    new[] { 0.28f, 0.42f, 0.28f, 0.12f }, new[] { 0.28f, 0.42f, 0.72f, 0.42f }, new[] { 0.72f, 0.42f, 0.72f, 0.12f },
                    new[] { 0.28f, 0.27f, 0.72f, 0.27f }, new[] { 0.28f, 0.12f, 0.72f, 0.12f },
                },
                ['港'] = new[] {
                    new[] { 0.12f, 0.84f, 0.24f, 0.76f }, new[] { 0.10f, 0.56f, 0.22f, 0.48f }, new[] { 0.12f, 0.14f, 0.26f, 0.34f },
                    new[] { 0.32f, 0.74f, 0.84f, 0.74f }, new[] { 0.46f, 0.88f, 0.46f, 0.58f }, new[] { 0.70f, 0.88f, 0.70f, 0.58f },
                    new[] { 0.28f, 0.58f, 0.90f, 0.58f }, new[] { 0.52f, 0.58f, 0.28f, 0.36f }, new[] { 0.66f, 0.58f, 0.90f, 0.36f },
                    new[] { 0.42f, 0.40f, 0.76f, 0.40f }, new[] { 0.76f, 0.40f, 0.76f, 0.28f }, new[] { 0.42f, 0.28f, 0.76f, 0.28f },
                    new[] { 0.42f, 0.40f, 0.42f, 0.14f }, new[] { 0.42f, 0.14f, 0.84f, 0.14f }, new[] { 0.84f, 0.14f, 0.86f, 0.24f },
                },
                ['龍'] = new[] {
                    new[] { 0.30f, 0.92f, 0.30f, 0.80f }, new[] { 0.14f, 0.80f, 0.46f, 0.80f }, new[] { 0.22f, 0.78f, 0.26f, 0.64f },
                    new[] { 0.38f, 0.78f, 0.34f, 0.64f }, new[] { 0.10f, 0.62f, 0.48f, 0.62f },
                    new[] { 0.18f, 0.52f, 0.18f, 0.14f }, new[] { 0.18f, 0.52f, 0.42f, 0.52f }, new[] { 0.42f, 0.52f, 0.42f, 0.14f },
                    new[] { 0.18f, 0.34f, 0.42f, 0.34f },
                    new[] { 0.58f, 0.88f, 0.58f, 0.64f }, new[] { 0.58f, 0.78f, 0.84f, 0.84f }, new[] { 0.58f, 0.64f, 0.86f, 0.64f },
                    new[] { 0.58f, 0.52f, 0.58f, 0.14f }, new[] { 0.58f, 0.42f, 0.84f, 0.42f }, new[] { 0.58f, 0.28f, 0.84f, 0.28f },
                    new[] { 0.58f, 0.14f, 0.88f, 0.14f },
                },
                ['鳳'] = new[] {
                    new[] { 0.20f, 0.88f, 0.16f, 0.14f }, new[] { 0.20f, 0.88f, 0.84f, 0.88f },
                    new[] { 0.84f, 0.88f, 0.82f, 0.16f }, new[] { 0.82f, 0.16f, 0.92f, 0.24f },
                    new[] { 0.34f, 0.74f, 0.70f, 0.74f }, new[] { 0.52f, 0.74f, 0.52f, 0.48f },
                    new[] { 0.34f, 0.56f, 0.70f, 0.56f }, new[] { 0.34f, 0.56f, 0.34f, 0.22f }, new[] { 0.70f, 0.56f, 0.70f, 0.22f },
                    new[] { 0.34f, 0.38f, 0.70f, 0.38f }, new[] { 0.34f, 0.24f, 0.70f, 0.24f },
                },
                ['九'] = new[] {
                    new[] { 0.46f, 0.90f, 0.20f, 0.16f },
                    new[] { 0.16f, 0.64f, 0.68f, 0.64f },
                    new[] { 0.68f, 0.64f, 0.68f, 0.18f },
                    new[] { 0.68f, 0.18f, 0.88f, 0.18f },
                    new[] { 0.88f, 0.18f, 0.88f, 0.32f },
                },
                ['大'] = new[] {
                    new[] { 0.14f, 0.62f, 0.86f, 0.62f },
                    new[] { 0.50f, 0.90f, 0.16f, 0.14f },
                    new[] { 0.50f, 0.56f, 0.86f, 0.14f },
                },
                ['酒'] = new[] {
                    new[] { 0.12f, 0.84f, 0.24f, 0.76f }, new[] { 0.10f, 0.54f, 0.22f, 0.46f }, new[] { 0.12f, 0.14f, 0.26f, 0.34f },
                    new[] { 0.34f, 0.82f, 0.88f, 0.82f }, new[] { 0.50f, 0.82f, 0.50f, 0.46f }, new[] { 0.68f, 0.82f, 0.68f, 0.46f },
                    new[] { 0.38f, 0.62f, 0.84f, 0.62f }, new[] { 0.38f, 0.62f, 0.38f, 0.16f }, new[] { 0.84f, 0.62f, 0.84f, 0.16f },
                    new[] { 0.38f, 0.34f, 0.84f, 0.34f }, new[] { 0.38f, 0.16f, 0.84f, 0.16f },
                },
                ['樓'] = new[] {
                    new[] { 0.10f, 0.66f, 0.38f, 0.66f }, new[] { 0.24f, 0.90f, 0.24f, 0.12f },
                    new[] { 0.24f, 0.64f, 0.10f, 0.28f }, new[] { 0.24f, 0.56f, 0.36f, 0.36f },
                    new[] { 0.46f, 0.84f, 0.84f, 0.84f }, new[] { 0.46f, 0.84f, 0.46f, 0.50f }, new[] { 0.84f, 0.84f, 0.84f, 0.50f },
                    new[] { 0.46f, 0.66f, 0.84f, 0.66f }, new[] { 0.42f, 0.50f, 0.88f, 0.50f },
                    new[] { 0.65f, 0.92f, 0.65f, 0.34f }, new[] { 0.42f, 0.36f, 0.88f, 0.36f },
                    new[] { 0.60f, 0.46f, 0.46f, 0.14f }, new[] { 0.72f, 0.40f, 0.84f, 0.14f },
                },
                ['金'] = new[] {
                    new[] { 0.50f, 0.90f, 0.14f, 0.56f }, new[] { 0.50f, 0.90f, 0.86f, 0.56f },
                    new[] { 0.28f, 0.58f, 0.72f, 0.58f }, new[] { 0.22f, 0.42f, 0.78f, 0.42f },
                    new[] { 0.50f, 0.58f, 0.50f, 0.16f }, new[] { 0.28f, 0.32f, 0.38f, 0.20f }, new[] { 0.72f, 0.32f, 0.62f, 0.20f },
                    new[] { 0.16f, 0.16f, 0.84f, 0.16f },
                },
                ['夜'] = new[] {
                    new[] { 0.50f, 0.92f, 0.50f, 0.80f }, new[] { 0.14f, 0.78f, 0.86f, 0.78f },
                    new[] { 0.34f, 0.70f, 0.14f, 0.36f }, new[] { 0.26f, 0.54f, 0.26f, 0.12f },
                    new[] { 0.60f, 0.72f, 0.44f, 0.44f }, new[] { 0.58f, 0.60f, 0.82f, 0.60f },
                    new[] { 0.82f, 0.60f, 0.42f, 0.14f }, new[] { 0.48f, 0.46f, 0.86f, 0.14f },
                },
                ['海'] = new[] {
                    new[] { 0.12f, 0.84f, 0.24f, 0.76f }, new[] { 0.10f, 0.54f, 0.22f, 0.46f }, new[] { 0.12f, 0.14f, 0.26f, 0.34f },
                    new[] { 0.46f, 0.90f, 0.32f, 0.64f }, new[] { 0.42f, 0.76f, 0.86f, 0.76f },
                    new[] { 0.40f, 0.58f, 0.80f, 0.58f }, new[] { 0.40f, 0.58f, 0.36f, 0.24f }, new[] { 0.80f, 0.58f, 0.76f, 0.16f },
                    new[] { 0.30f, 0.38f, 0.88f, 0.38f }, new[] { 0.36f, 0.24f, 0.80f, 0.24f }, new[] { 0.58f, 0.58f, 0.56f, 0.24f },
                },
                ['鮮'] = new[] {
                    new[] { 0.24f, 0.88f, 0.12f, 0.66f }, new[] { 0.14f, 0.66f, 0.42f, 0.66f },
                    new[] { 0.14f, 0.66f, 0.14f, 0.34f }, new[] { 0.42f, 0.66f, 0.42f, 0.34f },
                    new[] { 0.14f, 0.50f, 0.42f, 0.50f }, new[] { 0.14f, 0.34f, 0.42f, 0.34f },
                    new[] { 0.12f, 0.20f, 0.44f, 0.20f },
                    new[] { 0.58f, 0.88f, 0.64f, 0.74f }, new[] { 0.82f, 0.88f, 0.76f, 0.74f },
                    new[] { 0.52f, 0.70f, 0.88f, 0.70f }, new[] { 0.54f, 0.52f, 0.86f, 0.52f },
                    new[] { 0.50f, 0.34f, 0.90f, 0.34f }, new[] { 0.70f, 0.70f, 0.70f, 0.12f },
                },
                ['舫'] = new[] {
                    new[] { 0.28f, 0.88f, 0.20f, 0.74f }, new[] { 0.14f, 0.74f, 0.42f, 0.74f },
                    new[] { 0.14f, 0.74f, 0.12f, 0.14f }, new[] { 0.42f, 0.74f, 0.42f, 0.16f },
                    new[] { 0.08f, 0.46f, 0.46f, 0.46f }, new[] { 0.28f, 0.64f, 0.28f, 0.26f },
                    new[] { 0.68f, 0.90f, 0.68f, 0.76f }, new[] { 0.48f, 0.74f, 0.90f, 0.74f },
                    new[] { 0.68f, 0.74f, 0.48f, 0.14f }, new[] { 0.64f, 0.52f, 0.86f, 0.52f }, new[] { 0.86f, 0.52f, 0.80f, 0.16f },
                },
                ['城'] = new[] {
                    new[] { 0.10f, 0.62f, 0.36f, 0.62f }, new[] { 0.22f, 0.86f, 0.22f, 0.24f }, new[] { 0.10f, 0.20f, 0.38f, 0.34f },
                    new[] { 0.42f, 0.74f, 0.88f, 0.74f }, new[] { 0.42f, 0.74f, 0.40f, 0.20f },
                    new[] { 0.42f, 0.50f, 0.64f, 0.50f }, new[] { 0.64f, 0.50f, 0.62f, 0.20f },
                    new[] { 0.66f, 0.90f, 0.84f, 0.14f }, new[] { 0.86f, 0.54f, 0.54f, 0.16f },
                },
                ['東'] = new[] {
                    new[] { 0.18f, 0.82f, 0.82f, 0.82f }, new[] { 0.24f, 0.66f, 0.76f, 0.66f },
                    new[] { 0.24f, 0.66f, 0.24f, 0.40f }, new[] { 0.76f, 0.66f, 0.76f, 0.40f },
                    new[] { 0.24f, 0.52f, 0.76f, 0.52f }, new[] { 0.24f, 0.40f, 0.76f, 0.40f },
                    new[] { 0.50f, 0.92f, 0.50f, 0.12f }, new[] { 0.50f, 0.40f, 0.14f, 0.14f }, new[] { 0.50f, 0.40f, 0.86f, 0.14f },
                },
                ['星'] = new[] {
                    new[] { 0.24f, 0.88f, 0.76f, 0.88f }, new[] { 0.24f, 0.88f, 0.24f, 0.58f },
                    new[] { 0.76f, 0.88f, 0.76f, 0.58f }, new[] { 0.24f, 0.72f, 0.76f, 0.72f },
                    new[] { 0.24f, 0.58f, 0.76f, 0.58f }, new[] { 0.30f, 0.52f, 0.20f, 0.32f },
                    new[] { 0.24f, 0.42f, 0.80f, 0.42f }, new[] { 0.50f, 0.54f, 0.50f, 0.14f },
                    new[] { 0.24f, 0.28f, 0.76f, 0.28f }, new[] { 0.14f, 0.14f, 0.86f, 0.14f },
                },
                ['皇'] = new[] {
                    new[] { 0.50f, 0.92f, 0.42f, 0.82f },
                    new[] { 0.24f, 0.80f, 0.76f, 0.80f }, new[] { 0.24f, 0.80f, 0.24f, 0.50f },
                    new[] { 0.76f, 0.80f, 0.76f, 0.50f }, new[] { 0.24f, 0.65f, 0.76f, 0.65f },
                    new[] { 0.24f, 0.50f, 0.76f, 0.50f },
                    new[] { 0.22f, 0.40f, 0.78f, 0.40f }, new[] { 0.26f, 0.27f, 0.74f, 0.27f },
                    new[] { 0.14f, 0.14f, 0.86f, 0.14f }, new[] { 0.50f, 0.40f, 0.50f, 0.14f },
                },
                ['押'] = new[] {
                    new[] { 0.12f, 0.72f, 0.38f, 0.72f }, new[] { 0.26f, 0.90f, 0.26f, 0.14f }, new[] { 0.10f, 0.42f, 0.40f, 0.54f },
                    new[] { 0.46f, 0.84f, 0.88f, 0.84f }, new[] { 0.46f, 0.84f, 0.46f, 0.46f }, new[] { 0.88f, 0.84f, 0.88f, 0.46f },
                    new[] { 0.46f, 0.64f, 0.88f, 0.64f }, new[] { 0.46f, 0.46f, 0.88f, 0.46f }, new[] { 0.67f, 0.84f, 0.67f, 0.12f },
                },
                ['銀'] = new[] {
                    new[] { 0.26f, 0.88f, 0.10f, 0.58f }, new[] { 0.26f, 0.82f, 0.42f, 0.68f },
                    new[] { 0.16f, 0.58f, 0.38f, 0.58f }, new[] { 0.14f, 0.44f, 0.40f, 0.44f },
                    new[] { 0.27f, 0.58f, 0.27f, 0.20f }, new[] { 0.12f, 0.18f, 0.42f, 0.26f },
                    new[] { 0.52f, 0.84f, 0.84f, 0.84f }, new[] { 0.84f, 0.84f, 0.84f, 0.54f },
                    new[] { 0.52f, 0.68f, 0.84f, 0.68f }, new[] { 0.52f, 0.54f, 0.84f, 0.54f },
                    new[] { 0.52f, 0.84f, 0.52f, 0.14f }, new[] { 0.52f, 0.14f, 0.72f, 0.28f },
                    new[] { 0.62f, 0.52f, 0.88f, 0.14f },
                },
                ['行'] = new[] {
                    new[] { 0.36f, 0.88f, 0.14f, 0.62f }, new[] { 0.38f, 0.66f, 0.12f, 0.38f }, new[] { 0.26f, 0.50f, 0.26f, 0.14f },
                    new[] { 0.48f, 0.82f, 0.84f, 0.82f }, new[] { 0.44f, 0.60f, 0.90f, 0.60f },
                    new[] { 0.72f, 0.60f, 0.72f, 0.14f }, new[] { 0.72f, 0.14f, 0.56f, 0.20f },
                },
                ['維'] = new[] {
                    new[] { 0.26f, 0.88f, 0.14f, 0.64f }, new[] { 0.14f, 0.64f, 0.34f, 0.64f },
                    new[] { 0.32f, 0.72f, 0.14f, 0.42f }, new[] { 0.14f, 0.42f, 0.36f, 0.42f },
                    new[] { 0.14f, 0.20f, 0.36f, 0.30f },
                    new[] { 0.54f, 0.90f, 0.42f, 0.52f }, new[] { 0.48f, 0.74f, 0.48f, 0.14f },
                    new[] { 0.48f, 0.74f, 0.88f, 0.74f }, new[] { 0.48f, 0.54f, 0.86f, 0.54f },
                    new[] { 0.48f, 0.34f, 0.86f, 0.34f }, new[] { 0.48f, 0.16f, 0.90f, 0.16f }, new[] { 0.68f, 0.74f, 0.68f, 0.16f },
                },
                ['多'] = new[] {
                    new[] { 0.48f, 0.92f, 0.22f, 0.68f }, new[] { 0.38f, 0.82f, 0.74f, 0.82f }, new[] { 0.74f, 0.82f, 0.24f, 0.48f },
                    new[] { 0.50f, 0.56f, 0.22f, 0.34f }, new[] { 0.40f, 0.46f, 0.78f, 0.46f }, new[] { 0.78f, 0.46f, 0.20f, 0.12f },
                },
                ['利'] = new[] {
                    new[] { 0.46f, 0.86f, 0.16f, 0.78f }, new[] { 0.12f, 0.62f, 0.52f, 0.62f },
                    new[] { 0.34f, 0.82f, 0.34f, 0.14f }, new[] { 0.34f, 0.62f, 0.12f, 0.28f }, new[] { 0.34f, 0.52f, 0.50f, 0.34f },
                    new[] { 0.64f, 0.74f, 0.64f, 0.28f }, new[] { 0.82f, 0.88f, 0.82f, 0.14f },
                },
                ['亞'] = new[] {
                    new[] { 0.16f, 0.84f, 0.84f, 0.84f }, new[] { 0.20f, 0.58f, 0.80f, 0.58f }, new[] { 0.20f, 0.34f, 0.80f, 0.34f },
                    new[] { 0.20f, 0.58f, 0.20f, 0.34f }, new[] { 0.80f, 0.58f, 0.80f, 0.34f },
                    new[] { 0.40f, 0.84f, 0.40f, 0.16f }, new[] { 0.60f, 0.84f, 0.60f, 0.16f }, new[] { 0.12f, 0.16f, 0.88f, 0.16f },
                },
                ['融'] = new[] {
                    new[] { 0.12f, 0.86f, 0.46f, 0.86f }, new[] { 0.14f, 0.70f, 0.44f, 0.70f },
                    new[] { 0.14f, 0.54f, 0.44f, 0.54f }, new[] { 0.14f, 0.54f, 0.14f, 0.14f }, new[] { 0.44f, 0.54f, 0.44f, 0.14f },
                    new[] { 0.52f, 0.68f, 0.88f, 0.68f }, new[] { 0.52f, 0.68f, 0.52f, 0.42f }, new[] { 0.88f, 0.68f, 0.88f, 0.42f },
                    new[] { 0.52f, 0.42f, 0.88f, 0.42f }, new[] { 0.70f, 0.90f, 0.70f, 0.16f }, new[] { 0.52f, 0.14f, 0.90f, 0.24f },
                },
                ['中'] = new[] {
                    new[] { 0.20f, 0.72f, 0.80f, 0.72f }, new[] { 0.20f, 0.72f, 0.20f, 0.36f },
                    new[] { 0.80f, 0.72f, 0.80f, 0.36f }, new[] { 0.20f, 0.36f, 0.80f, 0.36f },
                    new[] { 0.50f, 0.92f, 0.50f, 0.10f },
                },
                ['心'] = new[] {
                    new[] { 0.20f, 0.54f, 0.14f, 0.28f }, new[] { 0.36f, 0.56f, 0.36f, 0.18f },
                    new[] { 0.36f, 0.18f, 0.78f, 0.18f }, new[] { 0.78f, 0.18f, 0.74f, 0.34f },
                    new[] { 0.52f, 0.72f, 0.56f, 0.52f }, new[] { 0.76f, 0.62f, 0.88f, 0.44f },
                },
                ['市'] = new[] {
                    new[] { 0.50f, 0.92f, 0.50f, 0.80f }, new[] { 0.14f, 0.78f, 0.86f, 0.78f },
                    new[] { 0.24f, 0.56f, 0.76f, 0.56f }, new[] { 0.24f, 0.56f, 0.24f, 0.20f }, new[] { 0.76f, 0.56f, 0.76f, 0.20f },
                    new[] { 0.50f, 0.78f, 0.50f, 0.12f },
                },
                ['福'] = new[] {
                    new[] { 0.24f, 0.88f, 0.28f, 0.78f }, new[] { 0.12f, 0.70f, 0.36f, 0.70f }, new[] { 0.26f, 0.70f, 0.12f, 0.40f },
                    new[] { 0.26f, 0.56f, 0.26f, 0.14f }, new[] { 0.46f, 0.84f, 0.88f, 0.84f },
                    new[] { 0.50f, 0.66f, 0.84f, 0.66f }, new[] { 0.48f, 0.44f, 0.86f, 0.44f },
                    new[] { 0.48f, 0.44f, 0.48f, 0.16f }, new[] { 0.86f, 0.44f, 0.86f, 0.16f },
                    new[] { 0.48f, 0.16f, 0.86f, 0.16f }, new[] { 0.67f, 0.44f, 0.67f, 0.16f },
                },
                ['總'] = new[] {
                    new[] { 0.26f, 0.88f, 0.14f, 0.64f }, new[] { 0.14f, 0.64f, 0.34f, 0.64f }, new[] { 0.14f, 0.42f, 0.36f, 0.42f },
                    new[] { 0.14f, 0.22f, 0.36f, 0.30f },
                    new[] { 0.46f, 0.78f, 0.84f, 0.78f }, new[] { 0.46f, 0.78f, 0.46f, 0.44f }, new[] { 0.84f, 0.78f, 0.84f, 0.44f },
                    new[] { 0.46f, 0.44f, 0.84f, 0.44f }, new[] { 0.44f, 0.16f, 0.84f, 0.16f }, new[] { 0.44f, 0.32f, 0.44f, 0.16f },
                },
                ['會'] = new[] {
                    new[] { 0.50f, 0.92f, 0.12f, 0.62f }, new[] { 0.50f, 0.92f, 0.88f, 0.62f },
                    new[] { 0.34f, 0.68f, 0.66f, 0.68f }, new[] { 0.20f, 0.56f, 0.80f, 0.56f },
                    new[] { 0.20f, 0.56f, 0.20f, 0.40f }, new[] { 0.80f, 0.56f, 0.80f, 0.40f }, new[] { 0.20f, 0.40f, 0.80f, 0.40f },
                    new[] { 0.50f, 0.56f, 0.50f, 0.40f },
                    new[] { 0.26f, 0.32f, 0.74f, 0.32f }, new[] { 0.26f, 0.32f, 0.26f, 0.14f }, new[] { 0.74f, 0.32f, 0.74f, 0.14f },
                    new[] { 0.26f, 0.14f, 0.74f, 0.14f },
                },
                ['環'] = new[] {
                    new[] { 0.10f, 0.82f, 0.38f, 0.82f }, new[] { 0.12f, 0.54f, 0.36f, 0.54f },
                    new[] { 0.24f, 0.82f, 0.24f, 0.22f }, new[] { 0.10f, 0.20f, 0.40f, 0.28f },
                    new[] { 0.44f, 0.86f, 0.88f, 0.86f }, new[] { 0.44f, 0.70f, 0.88f, 0.70f },
                    new[] { 0.44f, 0.86f, 0.44f, 0.70f }, new[] { 0.88f, 0.86f, 0.88f, 0.70f },
                    new[] { 0.42f, 0.56f, 0.90f, 0.56f }, new[] { 0.48f, 0.40f, 0.84f, 0.40f },
                    new[] { 0.64f, 0.40f, 0.44f, 0.14f }, new[] { 0.66f, 0.34f, 0.88f, 0.14f },
                },
            };
        }

        internal static void DrawRectOutline(Texture2D tex, int x0, int y0, int x1, int y1, int thickness, Color col)
        {
            DrawFilledRect(tex, x0, y0, x1, y0 + thickness, col);
            DrawFilledRect(tex, x0, y1 - thickness, x1, y1, col);
            DrawFilledRect(tex, x0, y0, x0 + thickness, y1, col);
            DrawFilledRect(tex, x1 - thickness, y0, x1, y1, col);
        }

        internal static void DrawFilledRect(Texture2D tex, int x0, int y0, int x1, int y1, Color col)
        {
            int minX = Mathf.Clamp(x0, 0, tex.width - 1);
            int maxX = Mathf.Clamp(x1, 0, tex.width - 1);
            int minY = Mathf.Clamp(y0, 0, tex.height - 1);
            int maxY = Mathf.Clamp(y1, 0, tex.height - 1);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    tex.SetPixel(x, y, col);
        }

        internal static float TileableNoise(float x, float y, int period, float seed)
        {
            float u = Mathf.Repeat(x / period, 1f);
            float v = Mathf.Repeat(y / period, 1f);
            float px = u * period;
            float py = v * period;
            float n00 = Mathf.PerlinNoise(px + seed, py + seed);
            float n10 = Mathf.PerlinNoise(px - period + seed, py + seed);
            float n01 = Mathf.PerlinNoise(px + seed, py - period + seed);
            float n11 = Mathf.PerlinNoise(px - period + seed, py - period + seed);
            float tx = Mathf.SmoothStep(0f, 1f, u);
            float ty = Mathf.SmoothStep(0f, 1f, v);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, tx), Mathf.Lerp(n01, n11, tx), ty);
        }

        internal static Texture2D BuildNormalMap(float[,] heights, float strength)
        {
            int h = heights.GetLength(0);
            int w = heights.GetLength(1);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            for (int y = 0; y < h; y++)
            {
                int ym = (y - 1 + h) % h;
                int yp = (y + 1) % h;
                for (int x = 0; x < w; x++)
                {
                    int xm = (x - 1 + w) % w;
                    int xp = (x + 1) % w;
                    float dx = (heights[y, xp] - heights[y, xm]) * strength;
                    float dy = (heights[yp, x] - heights[ym, x]) * strength;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    tex.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
                }
            }
            tex.Apply();
            return tex;
        }

        internal static Texture2D SavePngAsset(Texture2D tex, string assetPath, bool isNormalMap)
        {
            byte[] bytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.Combine(projectRoot, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, bytes);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.textureType = isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Material CreateOrUpdateMaterial(string matName, Shader shader, Action<Material> configure)
        {
            string path = $"{MaterialFolder}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = matName };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }
            configure?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(cur, parts[i]);
                }
                cur = next;
            }
        }

        #endregion
    }
}
#endif
