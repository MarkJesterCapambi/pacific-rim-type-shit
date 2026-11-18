using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PacificRimUI.Editor;
using PacificRimWater;

namespace PacificRimCutscene.Editor
{
    /// <summary>
    /// Builds and registers the dedicated ending cutscene scene ("Assets/Scenes/The Anteverse.unity")
    /// containing:
    /// - WormholeThroat_Zone (0:11–0:18): Curved organic tunnel with swirling purple/magenta/blue plasma tendrils
    /// - AnteverseCavern_Zone (0:18–0:38): Vast zero-gravity alien dimension bathed in sickly bioluminescent teal,
    ///   bruised violet, and subterranean amber fog, filled with floating jagged monoliths and towering biomechanical
    ///   Kaiju structures, plus iridescent exit membrane and thermonuclear shockwave rig.
    /// - Gipsy Danger (Jaeger_Mech) + Category V Slattern (Kaiju_Slattern)
    /// </summary>
    public static class SetupAnteverseScene
    {
        public const string ScenePath = "Assets/Scenes/The Anteverse.unity";
        private const string MeshFolder = "Assets/Meshes/Anteverse";
        private const string MatFolder = "Assets/Materials/Anteverse";
        private const string VolumeProfilePath = "Assets/Settings/TheAnteverse_VolumeProfile.asset";

        private const string JaegerFbxPath = "Assets/Models/GipsyDangerV2.fbx";
        private const string JaegerControllerPath = "Assets/Animations/GipsyDanger_Locomotion.controller";
        private const string JaegerArmorMatPath = "Assets/Materials/PacificRim_JaegerArmor.mat";
        private const string VisorMatPath = "Assets/Materials/VisorTex.mat";
        private const string TurbineMatPath = "Assets/Materials/PacificRim_NeonVisorTurbine 1.mat";

        private const string SlatternFbxPath = "Assets/Models/Slattern.fbx";
        private const string SlatternControllerPath = "Assets/Animations/Slattern_Controller.controller";
        private const string SlatternMatPath = "Assets/Materials/PacificRim_Slattern.mat";

        [MenuItem("Pacific Rim/Setup The Anteverse Ending Scene")]
        public static void Setup()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;

            EnsureDirectories();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "The Anteverse";

            ConfigureInitialRenderSettings();

            Camera cam = CreateCamera();
            Volume vol = CreatePostProcessVolume();
            Light rimLight = CreateAnteverseRimLight();

            GameObject wormholeZone = BuildWormholeZone();
            GameObject anteverseZone = BuildAnteverseCavernZone(out Transform exitMembrane);
            GameObject detonationRig = BuildThermonuclearDetonationRig(out Transform swSphere, out Transform swRing, out Light flashLight);

            GameObject jaegerGo = CreateJaeger();
            GameObject slatternGo = CreateSlattern();
            CreateCanvas();

            // Director
            var directorGo = new GameObject("Anteverse_EndingCutscene");
            var director = directorGo.AddComponent<AnteverseEndingCutscene>();
            director.wormholeThroatZone = wormholeZone;
            director.anteverseCavernZone = anteverseZone;
            director.exitMembraneTransform = exitMembrane;
            director.shockwaveSphereTransform = swSphere;
            director.shockwaveRingTransform = swRing;
            director.detonationFlashLight = flashLight;
            director.jaegerTransform = jaegerGo.transform;
            director.slatternTransform = slatternGo.transform;
            director.postProcessVolume = vol;
            director.anteverseRimLight = rimLight;

            // Default authoring view: show Anteverse Cavern active in Editor preview? Keep Wormhole ready or Anteverse visible!
            // Keeping Anteverse active in Editor lets artists inspect the Anteverse environment right away,
            // while AnteverseEndingCutscene.Start() switches Wormhole ON for 0:11-0:18 and Anteverse ON at 0:18!
            wormholeZone.SetActive(false);
            anteverseZone.SetActive(true);
            swSphere.gameObject.SetActive(false);
            swRing.gameObject.SetActive(false);

            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);

            SetupPacificRimMainMenuScene.UpdateBuildSettings();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00e5ff><b>[Anteverse]</b></color> Built & saved '{ScenePath}' with Wormhole Throat, Anteverse Cavern, Monoliths, Biomechanical Kaiju Structures, Gipsy & Slattern.");

            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != ScenePath && File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            if (!AssetDatabase.IsValidFolder("Assets/Meshes")) AssetDatabase.CreateFolder("Assets", "Meshes");
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder("Assets/Meshes", "Anteverse");
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Anteverse");
        }

        private static void ConfigureInitialRenderSettings()
        {
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.032f, 0.048f, 0.088f, 1f);
            RenderSettings.fogDensity = 0.0046f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.14f, 0.08f, 0.24f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.06f, 0.20f, 0.22f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.09f, 0.03f, 1f);
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(38f, 14f, -38f);
            go.transform.LookAt(new Vector3(0f, 15f, 4f));

            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 54f;
            cam.nearClipPlane = 0.4f;
            cam.farClipPlane = 850f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.032f, 0.048f, 0.088f, 1f);

            go.AddComponent<AudioListener>();
            var urpCam = go.AddComponent<UniversalAdditionalCameraData>();
            urpCam.renderPostProcessing = true;
            urpCam.renderShadows = true;
            urpCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            return cam;
        }

        private static Light CreateAnteverseRimLight()
        {
            var go = new GameObject("Anteverse_BioluminescentRimLight");
            go.transform.rotation = Quaternion.Euler(28f, 142f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(0.16f, 0.88f, 0.78f, 1f);
            l.intensity = 1.15f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.55f;
            go.AddComponent<UniversalAdditionalLightData>();
            return l;
        }

        private static Volume CreatePostProcessVolume()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            if (!profile.TryGet(out ColorAdjustments colorAdj)) colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.active = true;
            colorAdj.postExposure.Override(0.22f);
            colorAdj.contrast.Override(14f);
            colorAdj.saturation.Override(12f);

            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(0.88f);
            bloom.intensity.Override(1.38f);
            bloom.scatter.Override(0.76f);

            if (!profile.TryGet(out Vignette vig)) vig = profile.Add<Vignette>(true);
            vig.active = true;
            vig.color.Override(new Color(0.06f, 0.02f, 0.12f, 1f));
            vig.intensity.Override(0.28f);

            if (!profile.TryGet(out ChromaticAberration chrom)) chrom = profile.Add<ChromaticAberration>(true);
            chrom.active = true;
            chrom.intensity.Override(0.16f);

            if (!profile.TryGet(out SplitToning split)) split = profile.Add<SplitToning>(true);
            split.active = true;
            split.shadows.Override(new Color(0.14f, 0.06f, 0.22f, 1f));
            split.highlights.Override(new Color(0.18f, 0.90f, 0.76f, 1f));

            EditorUtility.SetDirty(profile);

            var go = new GameObject("Anteverse_PostVolume");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            vol.sharedProfile = profile;
            return vol;
        }

        private static GameObject BuildWormholeZone()
        {
            var root = new GameObject("WormholeThroat_Zone");

            Shader tunnelShader = Shader.Find("PacificRim/WormholeTunnel") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material tunnelMat = LoadOrCreateMaterial($"{MatFolder}/Anteverse_WormholeTunnel.mat", tunnelShader, m =>
            {
                m.SetColor("_PurpleColor", new Color(0.32f, 0.04f, 0.65f, 1f));
                m.SetColor("_MagentaColor", new Color(1.45f, 0.14f, 0.96f, 1f));
                m.SetColor("_ElectricBlueColor", new Color(0.12f, 0.85f, 2.4f, 1f));
                m.SetFloat("_ScrollSpeed", 3.8f);
                m.SetFloat("_SwirlAmount", 2.8f);
                m.SetFloat("_FlashBurst", 0f);
            });

            Mesh tunnelMesh = SaveMeshAsset(BuildCurvedWormholeTunnelMesh(radius: 30f, length: 280f, radialSegs: 40, lengthSegs: 56), $"{MeshFolder}/WormholeTunnelMesh.asset");
            var tunnelGo = new GameObject("WormholeTunnelMesh");
            tunnelGo.transform.SetParent(root.transform, false);
            tunnelGo.AddComponent<MeshFilter>().sharedMesh = tunnelMesh;
            var tMr = tunnelGo.AddComponent<MeshRenderer>();
            tMr.sharedMaterial = tunnelMat;
            tMr.shadowCastingMode = ShadowCastingMode.Off;

            // Twisted swirling plasma tendril ribbons lining the inner throat
            Shader ribbonShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material magentaTendrilMat = LoadOrCreateMaterial($"{MatFolder}/Anteverse_WormholeTendrilMagenta.mat", ribbonShader, m =>
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(1.15f, 0.14f, 0.92f, 0.24f));
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
            });
            Material blueTendrilMat = LoadOrCreateMaterial($"{MatFolder}/Anteverse_WormholeTendrilBlue.mat", ribbonShader, m =>
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.10f, 0.65f, 1.45f, 0.24f));
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
            });

            Mesh tendrilMesh = SaveMeshAsset(BuildPlasmaTendrilRibbonMesh(length: 250f, width: 3.4f, segs: 48), $"{MeshFolder}/WormholeTendrilRibbon.asset");
            for (int i = 0; i < 16; i++)
            {
                float ang = (i / 16f) * Mathf.PI * 2f;
                float r = 21f + (i % 3) * 2.2f;
                var tendril = new GameObject($"PlasmaTendril_{i + 1:00}");
                tendril.transform.SetParent(root.transform, false);
                tendril.transform.position = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                tendril.transform.rotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f);
                tendril.AddComponent<MeshFilter>().sharedMesh = tendrilMesh;
                var mr = tendril.AddComponent<MeshRenderer>();
                mr.sharedMaterial = (i % 2 == 0) ? magentaTendrilMat : blueTendrilMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            // Dynamic bio-electric lights inside the throat
            AddZonePointLight(root.transform, "WormholeLight_MagentaTop", new Vector3(0f, 38f, 0f), new Color(0.95f, 0.20f, 1.0f), 140f, 2200f);
            AddZonePointLight(root.transform, "WormholeLight_BlueMid", new Vector3(8f, 10f, -12f), new Color(0.12f, 0.72f, 1.0f), 130f, 2400f);
            AddZonePointLight(root.transform, "WormholeLight_VioletBot", new Vector3(-8f, -35f, 10f), new Color(0.55f, 0.16f, 1.0f), 140f, 1800f);

            return root;
        }

        private static GameObject BuildAnteverseCavernZone(out Transform exitMembrane)
        {
            var root = new GameObject("AnteverseCavern_Zone");

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? litShader;

            // Materials for jagged monoliths, alien biomechanical structures, & subterranean amber fog
            Material monolithMat = LoadOrCreateMaterial($"{MatFolder}/Anteverse_ObsidianMonolith.mat", litShader, m =>
            {
                m.SetColor("_BaseColor", new Color(0.055f, 0.048f, 0.095f, 1f));
                m.SetFloat("_Metallic", 0.35f);
                m.SetFloat("_Smoothness", 0.68f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.06f, 0.24f, 0.30f, 1f));
            });

            Material bioChitinMat = LoadOrCreateMaterial($"{MatFolder}/Anteverse_BiomechanicalChitin.mat", litShader, m =>
            {
                m.SetColor("_BaseColor", new Color(0.07f, 0.08f, 0.11f, 1f));
                m.SetFloat("_Metallic", 0.22f);
                m.SetFloat("_Smoothness", 0.62f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.05f, 0.55f, 0.52f, 1f));
            });

            Material bioTealSacMat = LoadOrCreateMaterial($"{MatFolder}/Anteverse_BioluminescentTealSac.mat", litShader, m =>
            {
                m.SetColor("_BaseColor", new Color(0.10f, 0.75f, 0.68f, 1f));
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.15f, 2.2f, 2.0f, 1f));
            });

            Material subAmberGlowMat = LoadOrCreateMaterial($"{MatFolder}/Anteverse_SubterraneanAmberMist.mat", unlitShader, m =>
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(1.4f, 0.65f, 0.14f, 0.32f));
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
            });

            // 1. Subterranean Amber Cavern Depth & Bioluminescent Teal / Bruised Violet Lights
            AddZonePointLight(root.transform, "Anteverse_SubAmberGlowL", new Vector3(-85f, -110f, 70f), new Color(1.0f, 0.52f, 0.10f), 280f, 4800f);
            AddZonePointLight(root.transform, "Anteverse_SubAmberGlowR", new Vector3(95f, -115f, 95f), new Color(1.0f, 0.46f, 0.08f), 280f, 4400f);
            AddZonePointLight(root.transform, "Anteverse_TealCavernGlowA", new Vector3(-90f, 45f, 65f), new Color(0.10f, 0.92f, 0.82f), 240f, 3600f);
            AddZonePointLight(root.transform, "Anteverse_VioletCavernGlowB", new Vector3(105f, 55f, 40f), new Color(0.55f, 0.18f, 0.96f), 250f, 3800f);
            AddZonePointLight(root.transform, "Anteverse_TealAbyssGlowC", new Vector3(0f, 80f, 145f), new Color(0.14f, 0.88f, 0.78f), 320f, 4200f);

            // Subterranean amber haze layer far below
            var amberFloor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            amberFloor.name = "Anteverse_SubterraneanAmberHaze";
            Object.DestroyImmediate(amberFloor.GetComponent<Collider>());
            amberFloor.transform.SetParent(root.transform, false);
            amberFloor.transform.position = new Vector3(0f, -125f, 40f);
            amberFloor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            amberFloor.transform.localScale = new Vector3(720f, 720f, 1f);
            var afMr = amberFloor.GetComponent<MeshRenderer>();
            afMr.sharedMaterial = subAmberGlowMat;
            afMr.shadowCastingMode = ShadowCastingMode.Off;

            // 2. 36 Floating Jagged Monoliths suspended in zero gravity
            Mesh monoMeshA = SaveMeshAsset(BuildJaggedMonolithMesh(17), $"{MeshFolder}/AnteverseMonolith_A.asset");
            Mesh monoMeshB = SaveMeshAsset(BuildJaggedMonolithMesh(43), $"{MeshFolder}/AnteverseMonolith_B.asset");
            var monoParent = new GameObject("Anteverse_FloatingMonoliths");
            monoParent.transform.SetParent(root.transform, false);

            var rng = new System.Random(201307);
            for (int i = 0; i < 36; i++)
            {
                float ang = ((float)i / 36f) * Mathf.PI * 2f + (float)(rng.NextDouble() - 0.5) * 0.25f;
                float dist = 68f + (float)rng.NextDouble() * 195f;
                float y = -65f + (float)rng.NextDouble() * 165f;
                // Keep immediate center camera corridor clear
                if (dist < 85f && Mathf.Abs(Mathf.Sin(ang)) < 0.35f) dist += 42f;

                var mGo = new GameObject($"FloatingMonolith_{i + 1:00}");
                mGo.transform.SetParent(monoParent.transform, false);
                mGo.transform.position = new Vector3(Mathf.Cos(ang) * dist, y, Mathf.Sin(ang) * dist + 25f);
                mGo.transform.rotation = Quaternion.Euler(
                    (float)rng.NextDouble() * 48f - 24f,
                    (float)rng.NextDouble() * 360f,
                    (float)rng.NextDouble() * 48f - 24f);
                float sx = 5.5f + (float)rng.NextDouble() * 12f;
                float sy = 14f + (float)rng.NextDouble() * 36f;
                mGo.transform.localScale = new Vector3(sx, sy, sx * (0.7f + (float)rng.NextDouble() * 0.5f));

                mGo.AddComponent<MeshFilter>().sharedMesh = (i % 2 == 0) ? monoMeshA : monoMeshB;
                var mr = mGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = monolithMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;

                var fp = mGo.AddComponent<AnteverseFloatProp>();
                fp.bobAmplitude = new Vector3(0.3f, 0.8f + (i % 3) * 0.4f, 0.3f);
                fp.bobFrequency = 0.14f + (i % 5) * 0.03f;
                fp.spinDegreesPerSecond = new Vector3((i % 3 - 1) * 1.1f, (i % 2 == 0 ? 1.8f : -1.5f), (i % 4 - 1.5f) * 0.8f);
                fp.phaseOffset = i * 0.7f;
            }

            // 3. 14 Towering Biomechanical Kaiju Structures suspended around the cavernous abyss
            Mesh ribSpireMesh = SaveMeshAsset(BuildBiomechanicalSpireMesh(11), $"{MeshFolder}/BiomechanicalKaijuSpire.asset");
            Mesh podMesh = SaveMeshAsset(BuildBiomechanicalPodMesh(), $"{MeshFolder}/BiomechanicalKaijuPod.asset");
            var bioParent = new GameObject("Anteverse_BiomechanicalStructures");
            bioParent.transform.SetParent(root.transform, false);

            for (int i = 0; i < 14; i++)
            {
                float ang = Mathf.Lerp(-0.75f * Mathf.PI, 0.75f * Mathf.PI, (float)i / 13f) + Mathf.PI * 0.5f;
                float dist = 125f + (i % 3) * 48f;
                float y = -95f + (i % 4) * 32f;

                var structRoot = new GameObject($"BiomechanicalStructure_{i + 1:00}");
                structRoot.transform.SetParent(bioParent.transform, false);
                structRoot.transform.position = new Vector3(Mathf.Cos(ang) * dist, y, Mathf.Sin(ang) * dist + 40f);
                structRoot.transform.rotation = Quaternion.Euler((i % 3 - 1) * 14f, ang * Mathf.Rad2Deg + 90f, (i % 2 == 0 ? 8f : -8f));
                float scaleH = 85f + (i % 4) * 42f;
                float scaleW = 18f + (i % 3) * 7f;
                structRoot.transform.localScale = new Vector3(scaleW, scaleH, scaleW);

                structRoot.AddComponent<MeshFilter>().sharedMesh = ribSpireMesh;
                var smr = structRoot.AddComponent<MeshRenderer>();
                smr.sharedMaterial = bioChitinMat;
                smr.shadowCastingMode = ShadowCastingMode.Off;

                // Glowing bioluminescent gestation pod suspended inside the rib structure
                var podGo = new GameObject("BioluminescentPod");
                podGo.transform.SetParent(structRoot.transform, false);
                podGo.transform.localPosition = new Vector3(0f, 0.55f, 0.15f);
                podGo.transform.localScale = new Vector3(0.45f, 0.24f, 0.45f);
                podGo.AddComponent<MeshFilter>().sharedMesh = podMesh;
                var pmr = podGo.AddComponent<MeshRenderer>();
                pmr.sharedMaterial = bioTealSacMat;
                pmr.shadowCastingMode = ShadowCastingMode.Off;

                var fp = structRoot.AddComponent<AnteverseFloatProp>();
                fp.bobAmplitude = new Vector3(0.25f, 0.65f, 0.25f);
                fp.bobFrequency = 0.09f;
                fp.spinDegreesPerSecond = new Vector3(0f, (i % 2 == 0 ? 0.45f : -0.45f), 0f);
                fp.phaseOffset = i * 1.15f;
            }

            // 4. Iridescent Inner Exit Membrane (0:18 Burst-Through Portal)
            Shader memShader = Shader.Find("PacificRim/BreachMembrane") ?? unlitShader;
            Material exitMat = LoadOrCreateMaterial($"{MatFolder}/Anteverse_ExitMembrane.mat", memShader, m =>
            {
                m.SetColor("_ColorA", new Color(0.55f, 0.25f, 1.3f, 0.9f));
                m.SetColor("_ColorB", new Color(0.15f, 1.0f, 1.4f, 0.9f));
                m.SetColor("_TearEdgeColor", new Color(2.0f, 2.2f, 2.8f, 1f));
                m.SetFloat("_Iridescence", 1.0f);
                m.SetFloat("_RippleProgress", 0.4f);
                m.SetFloat("_TearAmount", 0.35f);
                m.SetFloat("_FlareIntensity", 2.8f);
            });

            var memGo = new GameObject("Anteverse_ExitMembrane");
            memGo.transform.SetParent(root.transform, false);
            memGo.transform.position = new Vector3(-4f, 34f, -18f);
            memGo.transform.rotation = Quaternion.Euler(52f, 12f, 0f);
            memGo.AddComponent<MeshFilter>().sharedMesh = SaveMeshAsset(BuildFlatDiscGridMesh(42f, 24, 24), $"{MeshFolder}/AnteverseExitMembrane.asset");
            var emr = memGo.AddComponent<MeshRenderer>();
            emr.sharedMaterial = exitMat;
            emr.shadowCastingMode = ShadowCastingMode.Off;
            exitMembrane = memGo.transform;

            return root;
        }

        private static GameObject BuildThermonuclearDetonationRig(out Transform swSphere, out Transform swRing, out Light flashLight)
        {
            var root = new GameObject("Thermonuclear_Detonation_Rig");

            Shader swShader = Shader.Find("PacificRim/ShockwaveSphere") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material swMat = LoadOrCreateMaterial($"{MatFolder}/Anteverse_ShockwaveSphere.mat", swShader, m =>
            {
                m.SetColor("_CoreColor", new Color(4.8f, 4.4f, 3.6f, 1f));
                m.SetColor("_RimColor", new Color(3.6f, 2.4f, 1.0f, 1f));
                m.SetFloat("_Intensity", 6.0f);
                m.SetFloat("_Opacity", 1.0f);
            });

            var sphereGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereGo.name = "Thermonuclear_ShockwaveSphere";
            Object.DestroyImmediate(sphereGo.GetComponent<Collider>());
            sphereGo.transform.SetParent(root.transform, false);
            var smr = sphereGo.GetComponent<MeshRenderer>();
            smr.sharedMaterial = swMat;
            smr.shadowCastingMode = ShadowCastingMode.Off;
            swSphere = sphereGo.transform;

            var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGo.name = "Thermonuclear_ShockwaveRing";
            Object.DestroyImmediate(ringGo.GetComponent<Collider>());
            ringGo.transform.SetParent(root.transform, false);
            var rmr = ringGo.GetComponent<MeshRenderer>();
            rmr.sharedMaterial = swMat;
            rmr.shadowCastingMode = ShadowCastingMode.Off;
            swRing = ringGo.transform;

            var lightGo = new GameObject("Thermonuclear_FlashLight");
            lightGo.transform.SetParent(root.transform, false);
            flashLight = lightGo.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = new Color(1.0f, 0.94f, 0.80f, 1f);
            flashLight.range = 600f;
            flashLight.intensity = 0f;
            flashLight.shadows = LightShadows.None;
            flashLight.enabled = false;

            return root;
        }

        private static GameObject CreateJaeger()
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(JaegerFbxPath);
            if (fbx == null) return new GameObject("Jaeger_Mech");

            var jaegerGo = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(jaegerGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            jaegerGo.name = "Jaeger_Mech";
            jaegerGo.transform.position = new Vector3(0f, 8f, 4f);
            jaegerGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var anim = jaegerGo.GetComponent<Animator>() ?? jaegerGo.AddComponent<Animator>();
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(JaegerControllerPath);
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Material armorMat = AssetDatabase.LoadAssetAtPath<Material>(JaegerArmorMatPath);
            Material visorMat = AssetDatabase.LoadAssetAtPath<Material>(VisorMatPath);
            Material turbineMat = AssetDatabase.LoadAssetAtPath<Material>(TurbineMatPath);

            foreach (var smr in jaegerGo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.name == "Visor" && visorMat != null) smr.sharedMaterial = visorMat;
                else if (smr.name.Contains("Spinning") && turbineMat != null) smr.sharedMaterial = turbineMat;
                else if (armorMat != null)
                {
                    var mats = smr.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = armorMat;
                    smr.sharedMaterials = mats;
                }
                smr.updateWhenOffscreen = true;
            }

            jaegerGo.AddComponent<JaegerTurbineSpinner>();
            return jaegerGo;
        }

        private static GameObject CreateSlattern()
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(SlatternFbxPath);
            if (fbx == null) return new GameObject("Kaiju_Slattern");

            var slatGo = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(slatGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            slatGo.name = "Kaiju_Slattern";
            slatGo.transform.position = new Vector3(-34f, 14f, 42f);
            slatGo.transform.rotation = Quaternion.Euler(42f, 95f, -34f);
            slatGo.transform.localScale = Vector3.one * 25.67f;

            Material slatMat = AssetDatabase.LoadAssetAtPath<Material>(SlatternMatPath);
            foreach (var r in slatGo.GetComponentsInChildren<Renderer>(true))
            {
                if (slatMat != null)
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = slatMat;
                    r.sharedMaterials = mats;
                }
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }

            var anim = slatGo.GetComponent<Animator>() ?? slatGo.AddComponent<Animator>();
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SlatternControllerPath);
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            return slatGo;
        }

        private static void CreateCanvas()
        {
            var canvasGo = new GameObject("PacificRim_UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = canvasGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 500;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        #region Procedural Mesh Builders

        private static Mesh BuildCurvedWormholeTunnelMesh(float radius, float length, int radialSegs, int lengthSegs)
        {
            int cols = radialSegs + 1;
            int rows = lengthSegs + 1;
            var verts = new Vector3[cols * rows];
            var uvs = new Vector2[verts.Length];
            var tris = new int[radialSegs * lengthSegs * 6];

            for (int r = 0; r < rows; r++)
            {
                float v = (float)r / lengthSegs;
                float y = Mathf.Lerp(length * 0.5f, -length * 0.5f, v);
                float cx = Mathf.Sin(v * Mathf.PI * 2.5f) * 9.0f;
                float cz = Mathf.Cos(v * Mathf.PI * 1.8f) * 7.5f;
                float ringRad = radius * (0.88f + 0.16f * Mathf.Sin(v * 14f));

                for (int c = 0; c < cols; c++)
                {
                    float u = (float)c / radialSegs;
                    float ang = u * Mathf.PI * 2f;
                    int idx = r * cols + c;
                    verts[idx] = new Vector3(cx + Mathf.Cos(ang) * ringRad, y, cz + Mathf.Sin(ang) * ringRad);
                    uvs[idx] = new Vector2(u, v);
                }
            }

            int ti = 0;
            for (int r = 0; r < lengthSegs; r++)
            {
                for (int c = 0; c < radialSegs; c++)
                {
                    int a = r * cols + c;
                    int b = a + 1;
                    int d = a + cols;
                    int e = d + 1;
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = d;
                    tris[ti++] = b; tris[ti++] = e; tris[ti++] = d;
                }
            }

            var m = new Mesh { name = "WormholeTunnelMesh" };
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildPlasmaTendrilRibbonMesh(float length, float width, int segs)
        {
            var verts = new Vector3[(segs + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var tris = new int[segs * 6];

            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                float y = Mathf.Lerp(length * 0.5f, -length * 0.5f, t);
                float waveX = Mathf.Sin(t * 16f) * 2.2f;
                float waveZ = Mathf.Cos(t * 12f) * 1.8f;
                float w = width * Mathf.Sin(t * Mathf.PI);

                verts[i * 2] = new Vector3(waveX - w * 0.5f, y, waveZ);
                verts[i * 2 + 1] = new Vector3(waveX + w * 0.5f, y, waveZ);
                uvs[i * 2] = new Vector2(0f, t);
                uvs[i * 2 + 1] = new Vector2(1f, t);
            }

            int ti = 0;
            for (int i = 0; i < segs; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
            }

            var m = new Mesh { name = "WormholeTendrilRibbon" };
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildJaggedMonolithMesh(int seed)
        {
            const int rings = 10;
            const int segs = 12;
            var verts = new Vector3[(rings + 1) * segs];
            var uvs = new Vector2[verts.Length];
            var tris = new int[rings * segs * 6];

            for (int r = 0; r <= rings; r++)
            {
                float t = (float)r / rings;
                float y = t - 0.5f;
                float taper = Mathf.Sin(Mathf.Pow(t, 0.72f) * Mathf.PI);
                taper = Mathf.Max(0.08f, taper);
                for (int s = 0; s < segs; s++)
                {
                    float ang = ((float)s / segs) * Mathf.PI * 2f + t * 0.45f;
                    float jag = 0.72f + 0.48f * Mathf.PerlinNoise(Mathf.Cos(ang) * 1.4f + seed, t * 3.2f + Mathf.Sin(ang) * 1.4f);
                    float rad = taper * jag;
                    int idx = r * segs + s;
                    verts[idx] = new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad);
                    uvs[idx] = new Vector2((float)s / segs, t);
                }
            }

            int ti = 0;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segs; s++)
                {
                    int sn = (s + 1) % segs;
                    int a = r * segs + s;
                    int b = r * segs + sn;
                    int c = (r + 1) * segs + s;
                    int d = (r + 1) * segs + sn;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
            }

            var m = new Mesh { name = $"AnteverseMonolith_{seed}" };
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildBiomechanicalSpireMesh(int seed)
        {
            const int rings = 24;
            const int segs = 20;
            var verts = new Vector3[(rings + 1) * segs];
            var uvs = new Vector2[verts.Length];
            var tris = new int[rings * segs * 6];

            for (int r = 0; r <= rings; r++)
            {
                float t = (float)r / rings;
                float y = t;
                float archBend = Mathf.Sin(t * Mathf.PI) * 0.58f;
                float ribPulse = 0.84f + 0.16f * Mathf.Sin(t * Mathf.PI * 10f);
                float taper = Mathf.Lerp(1.0f, 0.18f, Mathf.Pow(t, 1.05f));

                for (int s = 0; s < segs; s++)
                {
                    float ang = ((float)s / segs) * Mathf.PI * 2f;
                    float flute = 1.0f + 0.22f * Mathf.Cos(ang * 5f + t * 2.2f);
                    float rad = taper * ribPulse * flute;
                    int idx = r * segs + s;
                    verts[idx] = new Vector3(Mathf.Cos(ang) * rad, y, archBend + Mathf.Sin(ang) * rad * 0.78f);
                    uvs[idx] = new Vector2((float)s / segs, t);
                }
            }

            int ti = 0;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segs; s++)
                {
                    int sn = (s + 1) % segs;
                    int a = r * segs + s;
                    int b = r * segs + sn;
                    int c = (r + 1) * segs + s;
                    int d = (r + 1) * segs + sn;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
            }

            var m = new Mesh { name = $"BiomechanicalKaijuSpire_{seed}" };
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildBiomechanicalPodMesh()
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh src = temp.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] bv = src.vertices;
            int[] bt = src.triangles;
            Object.DestroyImmediate(temp);

            var verts = new Vector3[bv.Length];
            for (int i = 0; i < bv.Length; i++)
            {
                Vector3 v = bv[i];
                float bulge = 1f + 0.18f * Mathf.Sin(v.y * 8f);
                verts[i] = new Vector3(v.x * bulge, v.y * 1.45f, v.z * bulge);
            }
            var m = new Mesh { name = "BiomechanicalKaijuPod" };
            m.vertices = verts;
            m.triangles = bt;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildFlatDiscGridMesh(float size, int segX, int segZ)
        {
            int cols = segX + 1;
            int rows = segZ + 1;
            var verts = new Vector3[cols * rows];
            var uvs = new Vector2[verts.Length];
            var tris = new int[segX * segZ * 6];
            for (int r = 0; r < rows; r++)
            {
                float v = (float)r / segZ;
                for (int c = 0; c < cols; c++)
                {
                    float u = (float)c / segX;
                    verts[r * cols + c] = new Vector3((u - 0.5f) * size, 0f, (v - 0.5f) * size);
                    uvs[r * cols + c] = new Vector2(u, v);
                }
            }
            int ti = 0;
            for (int r = 0; r < segZ; r++)
            {
                for (int c = 0; c < segX; c++)
                {
                    int a = r * cols + c;
                    int b = a + 1;
                    int d = a + cols;
                    int e = d + 1;
                    tris[ti++] = a; tris[ti++] = d; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = d; tris[ti++] = e;
                }
            }
            var m = new Mesh { name = "AnteverseExitMembrane" };
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        #endregion

        #region Helpers

        private static void AddZonePointLight(Transform parent, string name, Vector3 pos, Color col, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = col;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }

        private static Mesh SaveMeshAsset(Mesh mesh, string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            AssetDatabase.CreateAsset(mesh, assetPath);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material LoadOrCreateMaterial(string assetPath, Shader shader, System.Action<Material> configure)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }
            configure?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        #endregion
    }
}
