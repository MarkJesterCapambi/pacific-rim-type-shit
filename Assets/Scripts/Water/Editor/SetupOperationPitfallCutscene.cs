using System.Collections.Generic;
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
using PacificRimWater.Editor;

namespace PacificRimCutscene.Editor
{
    /// <summary>
    /// Builds and registers the dedicated Level 2 intro cutscene scene
    /// ("Assets/Scenes/Operation Pitfall Cutscene.unity") featuring the URP WaterSystem,
    /// coastal-to-abyssal Mariana Trench terrain, Gipsy Danger, Chinook convoy, and lighting volumes.
    /// </summary>
    public static class SetupOperationPitfallCutscene
    {
        public const string ScenePath = "Assets/Scenes/Operation Pitfall Cutscene.unity";
        private const string DaylightSkyboxMatPath = "Assets/Materials/PacificRim_DaylightOceanSkybox.mat";
        private const string StormSkyboxMatPath = "Assets/Materials/PacificRim_StormSkybox.mat";
        private const string TerrainDataPath = "Assets/Settings/OperationPitfallCutscene_TerrainData.asset";
        private const string VolumeProfilePath = "Assets/Settings/OperationPitfallCutscene_VolumeProfile.asset";

        private const string JaegerFbxPath = "Assets/Models/GipsyDangerV2.fbx";
        private const string LocomotionControllerPath = "Assets/Animations/GipsyDanger_Locomotion.controller";
        private const string JaegerArmorMatPath = "Assets/Materials/PacificRim_JaegerArmor.mat";
        private const string VisorMatPath = "Assets/Materials/VisorTex.mat";
        private const string TurbineMatPath = "Assets/Materials/PacificRim_NeonVisorTurbine 1.mat";

        private const string ChinookPrefabPath = "Assets/Prefabs/PacificRim_Chinook.prefab";
        private const string CableMatPath = "Assets/Materials/PacificRim_CableWire.mat";
        private const string OceanMatPath = "Assets/Materials/PacificRim_Ocean.mat";
        private const string FloodlightBeamMatPath = "Assets/Materials/PacificRim_UnderwaterFloodlightBeam.mat";
        private const string VolumetricBeamConeMeshPath = "Assets/Meshes/PacificRim_VolumetricBeamCone.asset";

        private const string SiltLayerPath = "Assets/Textures/MarianaTrench/Layer_AbyssalSilt.terrainlayer";
        private const string BasaltLayerPath = "Assets/Textures/MarianaTrench/Layer_VolcanicBasalt.terrainlayer";
        private const string TerrainMatPath = "Assets/Materials/PacificRim_TerrainLit.mat";
        private const string BasaltBoulderPrefabPath = "Assets/Prefabs/Abyssal_Basalt_Boulder.prefab";

        // Terrain bounds: covers X = [-180..+180], Z = [-90..+230], Y = [-95..+40]
        private const float TerrainWidth = 360f;
        private const float TerrainLength = 320f;
        private const float TerrainHeightSpan = 135f;
        private static readonly Vector3 TerrainOrigin = new Vector3(-180f, -95f, -90f);

        [MenuItem("Pacific Rim/Setup Operation Pitfall Intro Cutscene Scene")]
        public static void Setup()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Operation Pitfall Cutscene";

            EnsureSettingsDirectory();
            Material skyboxMat = EnsureDaylightOceanSkybox();
            ConfigureRenderSettings(skyboxMat);

            Light mainLight = CreateDirectionalLight();
            Camera mainCam = CreateCutsceneCamera();
            Volume postVolume = CreatePostProcessVolume();
            WaterSystem waterSystem = CreateOcean(out OceanWaterMesh oceanMesh);
            WaterSplashManager splashManager = CreateSplashManager();
            Terrain trenchTerrain = CreateMarianaCutsceneTerrain();
            CreateFlankingBoulders(trenchTerrain);
            GameObject jaegerGo = CreateJaeger(waterSystem, oceanMesh);
            Canvas uiCanvas = CreateCutsceneCanvas();

            var directorGo = new GameObject("OperationPitfall_IntroCutscene");
            var cutscene = directorGo.AddComponent<OperationPitfallIntroCutscene>();
            cutscene.targetGameplayScene = "Operation Pitfall";
            cutscene.jaegerTransform = jaegerGo.transform;
            cutscene.trenchTerrain = trenchTerrain;
            cutscene.mainDirectionalLight = mainLight;
            cutscene.postProcessVolume = postVolume;
            cutscene.waterSystem = waterSystem;
            cutscene.splashManager = splashManager;
            cutscene.chinookPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChinookPrefabPath);
            cutscene.cableMaterial = AssetDatabase.LoadAssetAtPath<Material>(CableMatPath);
            cutscene.underwaterFloodlightBeamMaterial = AssetDatabase.LoadAssetAtPath<Material>(FloodlightBeamMatPath);
            cutscene.playOnStart = true;

            // Force-refresh lighting probes cleanly and save scene + assets
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);

            SetupPacificRimMainMenuScene.UpdateBuildSettings();
            AssetDatabase.Refresh();

            Debug.Log($"[PacificRimCutscene] Built & saved '{ScenePath}' with URP WaterSystem, Mariana Trench slope, and Gipsy Danger.");

            // Keep editor tidy when invoked from another scene during batch setup
            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != ScenePath && System.IO.File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        private static void EnsureSettingsDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        }

        private static Material EnsureDaylightOceanSkybox()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(DaylightSkyboxMatPath);
            if (mat == null)
            {
                Material srcStorm = AssetDatabase.LoadAssetAtPath<Material>(StormSkyboxMatPath);
                if (srcStorm != null)
                {
                    mat = new Material(srcStorm) { name = "PacificRim_DaylightOceanSkybox" };
                }
                else
                {
                    Shader skyShader = Shader.Find("PacificRim/StormSkybox");
                    if (skyShader == null) skyShader = Shader.Find("Procedural Skybox");
                    mat = new Material(skyShader) { name = "PacificRim_DaylightOceanSkybox" };
                }
                AssetDatabase.CreateAsset(mat, DaylightSkyboxMatPath);
            }

            SetColorIfPresent(mat, "_ZenithColor", new Color(0.24f, 0.42f, 0.64f, 1f));
            SetColorIfPresent(mat, "_MidSkyColor", new Color(0.42f, 0.60f, 0.78f, 1f));
            SetColorIfPresent(mat, "_HorizonColor", new Color(0.64f, 0.78f, 0.89f, 1f));
            SetColorIfPresent(mat, "_SkyZenithColor", new Color(0.24f, 0.42f, 0.64f, 1f));
            SetColorIfPresent(mat, "_SkyHorizonColor", new Color(0.64f, 0.78f, 0.89f, 1f));
            SetColorIfPresent(mat, "_CloudDarkColor", new Color(0.36f, 0.48f, 0.60f, 1f));
            SetColorIfPresent(mat, "_CloudLightColor", new Color(0.84f, 0.90f, 0.96f, 1f));
            SetFloatIfPresent(mat, "_CloudDensity", 0.52f);
            SetFloatIfPresent(mat, "_CloudCoverage", 0.52f);
            SetFloatIfPresent(mat, "_LightningFlash", 0f);
            SetFloatIfPresent(mat, "_MoonIntensity", 0f);
            SetFloatIfPresent(mat, "_Exposure", 1.15f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigureRenderSettings(Material skyboxMat)
        {
            RenderSettings.skybox = skyboxMat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.54f, 0.68f, 0.80f, 1f);
            RenderSettings.fogDensity = 0.0016f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.60f, 0.74f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.45f, 0.56f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.16f, 0.22f, 1f);
        }

        private static Light CreateDirectionalLight()
        {
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(46f, -38f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.95f, 0.97f, 1.0f, 1f);
            light.intensity = 2.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.82f;
            light.shadowBias = 0.35f;
            light.shadowNormalBias = 1.45f;
            go.AddComponent<UniversalAdditionalLightData>();
            return light;
        }

        private static Camera CreateCutsceneCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            // Default 3/4 front framing of Beat 1 airlift position (0, 7.5, -22)
            Vector3 threeQuarterDir = new Vector3(0.7071f, 0f, 0.7071f);
            Vector3 jaegerStart = new Vector3(0f, 7.5f, -22f);
            go.transform.position = jaegerStart + threeQuarterDir * 29.0f + Vector3.up * 23.0f;
            go.transform.LookAt(jaegerStart + Vector3.up * 18.5f);

            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 550f;
            cam.clearFlags = CameraClearFlags.Skybox;

            go.AddComponent<AudioListener>();
            var lowPass = go.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;

            var urpCam = go.AddComponent<UniversalAdditionalCameraData>();
            urpCam.renderPostProcessing = true;
            urpCam.renderShadows = true;
            urpCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            return cam;
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
            colorAdj.postExposure.Override(0.15f);
            colorAdj.contrast.Override(12f);
            colorAdj.saturation.Override(8f);
            colorAdj.colorFilter.Override(Color.white);

            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.72f);

            if (!profile.TryGet(out Vignette vignette)) vignette = profile.Add<Vignette>(true);
            vignette.active = true;
            vignette.color.Override(new Color(0.01f, 0.04f, 0.08f, 1f));
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);

            if (!profile.TryGet(out SplitToning split)) split = profile.Add<SplitToning>(true);
            split.active = true;
            split.shadows.Override(new Color(0.45f, 0.58f, 0.72f, 1f));
            split.highlights.Override(new Color(0.96f, 0.98f, 1.0f, 1f));

            EditorUtility.SetDirty(profile);

            var go = new GameObject("Cutscene_PostVolume");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            vol.sharedProfile = profile;
            return vol;
        }

        private static WaterSystem CreateOcean(out OceanWaterMesh oceanMesh)
        {
            var go = new GameObject("ProceduralOcean");
            go.transform.position = new Vector3(0f, 0f, 40f);

            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            Material oceanMat = AssetDatabase.LoadAssetAtPath<Material>(OceanMatPath);
            if (oceanMat != null)
            {
                mr.sharedMaterial = oceanMat;
            }
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;

            var waterSys = go.AddComponent<WaterSystem>();
            waterSys.swellAmplitude1 = 0.85f;
            waterSys.swellAmplitude2 = 0.45f;
            waterSys.swellAmplitude3 = 0.28f;
            waterSys.waveSetBoost = 0.65f;
            waterSys.maxTotalWaveHeight = 2.6f;
            waterSys.maxTroughDepth = 1.6f;

            oceanMesh = go.AddComponent<OceanWaterMesh>();
            oceanMesh.meshWidth = 540f;
            oceanMesh.meshLength = 540f;
            oceanMesh.resolution = 190;

            return waterSys;
        }

        private static WaterSplashManager CreateSplashManager()
        {
            var go = new GameObject("WaterSplashManager");
            var mgr = go.AddComponent<WaterSplashManager>();
            mgr.isDeepUnderwaterMode = false;
            return mgr;
        }

        private static Terrain CreateMarianaCutsceneTerrain()
        {
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            const int hmRes = 257;
            const int alphaRes = 256;
            data.heightmapResolution = hmRes;
            data.alphamapResolution = alphaRes;
            data.baseMapResolution = 256;
            data.size = new Vector3(TerrainWidth, TerrainHeightSpan, TerrainLength);

            var heights = new float[hmRes, hmRes];
            for (int iz = 0; iz < hmRes; iz++)
            {
                float worldZ = TerrainOrigin.z + (iz / (float)(hmRes - 1)) * TerrainLength;
                for (int ix = 0; ix < hmRes; ix++)
                {
                    float worldX = TerrainOrigin.x + (ix / (float)(hmRes - 1)) * TerrainWidth;
                    float worldY = SampleCutsceneSeabedWorldY(worldX, worldZ);
                    heights[iz, ix] = Mathf.Clamp01((worldY - TerrainOrigin.y) / TerrainHeightSpan);
                }
            }
            data.SetHeights(0, 0, heights);

            TerrainLayer siltLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(SiltLayerPath);
            TerrainLayer basaltLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(BasaltLayerPath);
            if (siltLayer != null && basaltLayer != null)
            {
                data.terrainLayers = new[] { siltLayer, basaltLayer };
                var splat = new float[alphaRes, alphaRes, 2];
                for (int iz = 0; iz < alphaRes; iz++)
                {
                    float uZ = iz / (float)(alphaRes - 1);
                    for (int ix = 0; ix < alphaRes; ix++)
                    {
                        float uX = ix / (float)(alphaRes - 1);
                        float steep = data.GetSteepness(uX, uZ);
                        float worldX = TerrainOrigin.x + uX * TerrainWidth;
                        float worldZ = TerrainOrigin.z + uZ * TerrainLength;

                        // Steep trench slopes and canyon ridges reveal dark volcanic basalt
                        float slopeWeight = SmoothRange(14f, 30f, steep);
                        float trenchDark = SmoothRange(40f, 88f, worldZ) * 0.48f;
                        float wallWeight = SmoothRange(32f, 55f, Mathf.Abs(worldX)) * 0.55f;
                        float basalt = Mathf.Clamp01(Mathf.Max(slopeWeight, wallWeight) + trenchDark * 0.5f);

                        splat[iz, ix, 0] = 1f - basalt;
                        splat[iz, ix, 1] = basalt;
                    }
                }
                data.SetAlphamaps(0, 0, splat);
            }

            EditorUtility.SetDirty(data);

            var terrainGo = new GameObject("Mariana_Cutscene_Trench_Terrain");
            terrainGo.transform.position = TerrainOrigin;
            var terrain = terrainGo.AddComponent<Terrain>();
            terrain.terrainData = data;
            terrain.heightmapPixelError = 4f;
            terrain.drawInstanced = true;
            // Disable terrain self-shadow casting to avoid cascade banding across large underwater slopes;
            // Gipsy & basalt boulders still cast crisp shadows onto the seabed (receiveShadows = true).
            terrain.shadowCastingMode = ShadowCastingMode.Off;

            Material terrainMat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath);
            if (terrainMat != null)
            {
                terrain.materialTemplate = terrainMat;
            }

            var collider = terrainGo.AddComponent<TerrainCollider>();
            collider.terrainData = data;

            return terrain;
        }

        private static float SampleCutsceneSeabedWorldY(float worldX, float worldZ)
        {
            // Centerline Z profile tuned to 28-second pacing & 3/4 camera submersion timing:
            //   Z <= 6m   : flat -17.6m coastal shelf (exact chest-deep splashdown at Z = 0)
            //   Z 6..42m  : smooth Beat 3 wading slope (-17.6m -> -26.5m); visor & cam submerge ~14.5-16s
            //   Z 42..140m: steep Beat 4 trench descent slope (-26.5m -> -72.0m)
            float baseZHeight;
            if (worldZ <= 6f)
            {
                baseZHeight = -17.6f;
            }
            else if (worldZ <= 42f)
            {
                float u = Mathf.SmoothStep(0f, 1f, (worldZ - 6f) / 36f);
                baseZHeight = Mathf.Lerp(-17.6f, -26.5f, u);
            }
            else if (worldZ <= 140f)
            {
                float u = (worldZ - 42f) / 98f;
                baseZHeight = Mathf.Lerp(-26.5f, -72.0f, u);
            }
            else
            {
                float u = Mathf.Clamp01((worldZ - 140f) / 90f);
                baseZHeight = Mathf.Lerp(-72.0f, -82.0f, Mathf.SmoothStep(0f, 1f, u));
            }

            // Asymmetric underwater canyon walls form only after Beat 3 descent (Z > 30m) and remain
            // safely submerged (<= -6.5m) so Beats 1 & 2 look out over a clean open-ocean horizon.
            float canyonZMask = SmoothRange(30f, 66f, worldZ);
            float canyonRise = 0f;
            if (worldX < -30f)
            {
                float wallT = Mathf.Clamp01((-worldX - 30f) / 68f);
                canyonRise = Mathf.Pow(wallT, 1.40f) * 34f * canyonZMask;
            }
            else if (worldX > 52f)
            {
                float wallT = Mathf.Clamp01((worldX - 52f) / 76f);
                canyonRise = Mathf.Pow(wallT, 1.50f) * 28f * canyonZMask;
            }

            // Subtle natural seabed undulation, suppressed along the X = [-8..+8] Jaeger footprint corridor
            float corridorMask = SmoothRange(5f, 20f, Mathf.Abs(worldX));
            float subNoise = (Mathf.PerlinNoise(worldX * 0.032f + 12.1f, worldZ * 0.032f + 5.7f) - 0.5f) * 3.8f
                           + (Mathf.PerlinNoise(worldX * 0.085f + 3.3f, worldZ * 0.085f + 19.4f) - 0.5f) * 1.4f;

            // Keep shallow Z <= 8m landing corridor clean & true to -17.6m
            float shallowNoiseFade = SmoothRange(4f, 18f, worldZ);
            float detail = subNoise * corridorMask * shallowNoiseFade;

            return Mathf.Min(-5.5f, baseZHeight + canyonRise + detail);
        }

        private static float SmoothRange(float edge0, float edge1, float x)
        {
            if (Mathf.Approximately(edge0, edge1)) return x >= edge1 ? 1f : 0f;
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static void CreateFlankingBoulders(Terrain terrain)
        {
            GameObject boulderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasaltBoulderPrefabPath);
            if (boulderPrefab == null || terrain == null) return;

            var parent = new GameObject("Cutscene_AbyssalOutcrops");
            var leftPlacements = new Vector3[]
            {
                new Vector3(-34f, 0f, 18f),
                new Vector3(-42f, 0f, 34f),
                new Vector3(-31f, 0f, 48f),
                new Vector3(-46f, 0f, 64f),
                new Vector3(-33f, 0f, 78f),
                new Vector3(-48f, 0f, 94f),
                new Vector3(-38f, 0f, 112f),
                new Vector3(58f, 0f, 52f),
                new Vector3(64f, 0f, 84f),
                new Vector3(56f, 0f, 110f)
            };

            for (int i = 0; i < leftPlacements.Length; i++)
            {
                Vector3 p = leftPlacements[i];
                float y = terrain.SampleHeight(new Vector3(p.x, 0f, p.z)) + terrain.transform.position.y;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(boulderPrefab);
                inst.name = $"TrenchOutcrop_{i:00}";
                inst.transform.SetParent(parent.transform, true);
                inst.transform.position = new Vector3(p.x, y - 1.5f, p.z);
                inst.transform.rotation = Quaternion.Euler((i * 19f) % 18f - 9f, (i * 67f) % 360f, (i * 13f) % 14f - 7f);
                float scale = 2.6f + (i % 4) * 1.15f;
                inst.transform.localScale = new Vector3(scale * 1.25f, scale * 1.45f, scale);

                foreach (var col in inst.GetComponentsInChildren<Collider>(true))
                {
                    col.enabled = false;
                }
            }
        }

        private static GameObject CreateJaeger(WaterSystem waterSystem, OceanWaterMesh oceanMesh)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(JaegerFbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[PacificRimCutscene] Missing Jaeger model at '{JaegerFbxPath}'!");
                return new GameObject("Jaeger_Mech");
            }

            var jaegerGo = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(jaegerGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            jaegerGo.name = "Jaeger_Mech";
            jaegerGo.transform.position = new Vector3(0f, 7.5f, -22f);
            jaegerGo.transform.rotation = Quaternion.identity;

            var animator = jaegerGo.GetComponent<Animator>();
            if (animator == null) animator = jaegerGo.AddComponent<Animator>();
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(LocomotionControllerPath);
            if (ctrl != null) animator.runtimeAnimatorController = ctrl;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Material armorMat = AssetDatabase.LoadAssetAtPath<Material>(JaegerArmorMatPath);
            Material visorMat = AssetDatabase.LoadAssetAtPath<Material>(VisorMatPath);
            Material turbineMat = AssetDatabase.LoadAssetAtPath<Material>(TurbineMatPath);

            foreach (var smr in jaegerGo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.name == "Visor" && visorMat != null)
                {
                    smr.sharedMaterial = visorMat;
                }
                else if (smr.name.Contains("Spinning") && turbineMat != null)
                {
                    smr.sharedMaterial = turbineMat;
                }
                else if (armorMat != null)
                {
                    var mats = smr.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = armorMat;
                    smr.sharedMaterials = mats;
                }
                smr.updateWhenOffscreen = true;
            }

            var rb = jaegerGo.GetComponent<Rigidbody>();
            if (rb == null) rb = jaegerGo.AddComponent<Rigidbody>();
            rb.mass = 2000f;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var cap = jaegerGo.GetComponent<CapsuleCollider>();
            if (cap == null) cap = jaegerGo.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 10.5f, 0f);
            cap.radius = 3.2f;
            cap.height = 24f;

            var controller = jaegerGo.AddComponent<SimpleJaegerController>();
            controller.isCutsceneActive = true;

            var waterInt = jaegerGo.AddComponent<JaegerWaterInteraction>();
            waterInt.footstepShakeIntensity = 0.12f;
            waterInt.footstepShakeDuration = 0.18f;
            foreach (var t in jaegerGo.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Foot.L": waterInt.leftFootTransform = t; break;
                    case "Foot.R": waterInt.rightFootTransform = t; break;
                    case "Shin.L": waterInt.leftShinTransform = t; break;
                    case "Shin.R": waterInt.rightShinTransform = t; break;
                    case "Thigh.L": waterInt.leftThighTransform = t; break;
                    case "Thigh.R": waterInt.rightThighTransform = t; break;
                }
            }

            jaegerGo.AddComponent<JaegerWaveBodyInteraction>();

            if (waterSystem != null) waterSystem.followTarget = jaegerGo.transform;
            if (oceanMesh != null)
            {
                oceanMesh.followTarget = jaegerGo.transform;
                oceanMesh.jaegerInteraction = waterInt;
            }

            // Wire visual sockets, spotlights, floodlight beam cones & turbine spinner on Jaeger_Mech
            Selection.activeGameObject = jaegerGo;
            SetupJaegerVisualSockets.Setup();
            EnsureCutsceneFloodlights(jaegerGo);

            return jaegerGo;
        }

        private static void EnsureCutsceneFloodlights(GameObject jaegerGo)
        {
            Transform headBone = null;
            Transform chestBone = null;
            foreach (var t in jaegerGo.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Head") headBone = t;
                else if (t.name == "Chest") chestBone = t;
            }

            Mesh coneMesh = AssetDatabase.LoadAssetAtPath<Mesh>(VolumetricBeamConeMeshPath);
            Material beamMat = AssetDatabase.LoadAssetAtPath<Material>(FloodlightBeamMatPath);
            Vector3 invBoneScale = new Vector3(0.0003036644f, 0.0003177507f, 0.0002324041f);

            Light headSpot = null;
            Transform headBeam = null;
            if (headBone != null)
            {
                CreateOrGetFloodlightSocket(
                    headBone, "LightBeamOriginHead",
                    new Vector3(0f, 0.0019645f, 0.0010657f),
                    new Quaternion(-0.18807186f, 0.01495750f, -0.00532757f, 0.98202693f),
                    invBoneScale, new Color(0.92f, 0.98f, 1f, 1f), 3600f, 165f, 48f, 28f,
                    coneMesh, beamMat, new Vector3(1.22f, 1.22f, 1.65f),
                    out headSpot, out headBeam);
            }

            Light torsoSpotL = null, torsoSpotR = null;
            Transform torsoBeamL = null, torsoBeamR = null;
            if (chestBone != null)
            {
                CreateOrGetFloodlightSocket(
                    chestBone, "LightBeamOriginUpperTorso.L",
                    new Vector3(-0.0036995f, 0.0077447f, 0.0007601f),
                    new Quaternion(-0.12072644f, -0.11142865f, 0.08044847f, 0.98312604f),
                    invBoneScale, new Color(0.84f, 0.94f, 1f, 1f), 2800f, 145f, 58f, 36f,
                    coneMesh, beamMat, new Vector3(1.32f, 1.32f, 1.55f),
                    out torsoSpotL, out torsoBeamL);

                CreateOrGetFloodlightSocket(
                    chestBone, "LightBeamOriginUpperTorso.R",
                    new Vector3(0.00665f, 0.00723f, 0.00015f),
                    new Quaternion(-0.10687012f, 0.11005945f, -0.07286137f, 0.985473f),
                    invBoneScale, new Color(0.84f, 0.94f, 1f, 1f), 2800f, 145f, 58f, 36f,
                    coneMesh, beamMat, new Vector3(1.32f, 1.32f, 1.55f),
                    out torsoSpotR, out torsoBeamR);
            }

            var vs = jaegerGo.GetComponent<JaegerVisualSockets>();
            if (vs != null)
            {
                var so = new SerializedObject(vs);
                AssignObjectPropIfNull(so, "headLightOrigin", headSpot != null ? headSpot.transform : null);
                AssignObjectPropIfNull(so, "torsoLightOriginL", torsoSpotL != null ? torsoSpotL.transform : null);
                AssignObjectPropIfNull(so, "torsoLightOriginR", torsoSpotR != null ? torsoSpotR.transform : null);
                AssignObjectPropIfNull(so, "headSpotLight", headSpot);
                AssignObjectPropIfNull(so, "torsoSpotLightL", torsoSpotL);
                AssignObjectPropIfNull(so, "torsoSpotLightR", torsoSpotR);
                AssignObjectPropIfNull(so, "headBeamTransform", headBeam);
                AssignObjectPropIfNull(so, "torsoBeamTransformL", torsoBeamL);
                AssignObjectPropIfNull(so, "torsoBeamTransformR", torsoBeamR);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void CreateOrGetFloodlightSocket(
            Transform parent, string socketName, Vector3 localPos, Quaternion localRot, Vector3 localScale,
            Color spotColor, float intensity, float range, float outerAngle, float innerAngle,
            Mesh coneMesh, Material beamMat, Vector3 beamScale,
            out Light spotLight, out Transform beamTransform)
        {
            Transform existing = parent.Find(socketName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(socketName);
            if (existing == null)
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;
                go.transform.localRotation = localRot;
                go.transform.localScale = localScale;
            }

            spotLight = go.GetComponent<Light>();
            if (spotLight == null) spotLight = go.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.color = spotColor;
            spotLight.intensity = intensity;
            spotLight.range = range;
            spotLight.spotAngle = outerAngle;
            spotLight.innerSpotAngle = innerAngle;
            spotLight.shadows = LightShadows.None;
            if (go.GetComponent<UniversalAdditionalLightData>() == null)
            {
                go.AddComponent<UniversalAdditionalLightData>();
            }

            Transform existingBeam = go.transform.Find("VolumetricBeam");
            if (existingBeam != null)
            {
                beamTransform = existingBeam;
                return;
            }

            var beamGo = new GameObject("VolumetricBeam");
            beamGo.transform.SetParent(go.transform, false);
            beamGo.transform.localPosition = Vector3.zero;
            beamGo.transform.localRotation = Quaternion.identity;
            beamGo.transform.localScale = beamScale;

            var mf = beamGo.AddComponent<MeshFilter>();
            if (coneMesh != null) mf.sharedMesh = coneMesh;
            var mr = beamGo.AddComponent<MeshRenderer>();
            if (beamMat != null) mr.sharedMaterial = beamMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            beamTransform = beamGo.transform;
        }

        private static void AssignObjectPropIfNull(SerializedObject so, string propName, Object value)
        {
            if (value == null) return;
            var prop = so.FindProperty(propName);
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = value;
            }
        }

        private static Canvas CreateCutsceneCanvas()
        {
            var canvasGo = new GameObject("PacificRim_UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void SetColorIfPresent(Material mat, string prop, Color val)
        {
            if (mat != null && mat.HasProperty(prop)) mat.SetColor(prop, val);
        }

        private static void SetFloatIfPresent(Material mat, string prop, float val)
        {
            if (mat != null && mat.HasProperty(prop)) mat.SetFloat(prop, val);
        }
    }
}
