#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using PacificRimAtmosphere;
using PacificRimUI;
using PacificRimUI.Editor;

namespace PacificRimWater.Editor
{
    /// <summary>
    /// Builds the upgraded "Hong Kong Neon Highway" Harbour-to-Canyon level:
    /// - Spawns Gipsy Danger in Victoria Harbour facing Leatherback on the wet waterfront shoreline.
    /// - Grand Harbour Causeway Ramp leads straight into a narrow (~71m = ~2.9x Gipsy), dense Hong Kong neon canyon.
    /// - Full 3-tier Jaeger-Scale Vertical Hierarchy:
    ///   1) 0-18m Ankle-to-Waist: Multi-tier expressway flyovers, breakable overpass spans, knockable HK trams & fuel tankers, tong lau blocks with AC grids, rooftop water tanks & bamboo scaffolding.
    ///   2) 20-52m Chest-to-Eye: Thin HK pencil towers of wildly varied width/height, enclosed glass pedestrian skybridges, and 3-4 deep cantilevered Traditional Chinese neon calligraphy signboards.
    ///   3) 65-160m+ Overhead Supertall Peaks: Triangular glass prisms w/ white LED X-bracing, tiered neo-futuristic neon spires, curved waterfront towers, helipads, and low typhoon storm clouds.
    /// </summary>
    public static class SetupNeonCityLevel
    {
        public const string SceneName = "Hong Kong Neon Highway";
        public const string ScenePath = "Assets/Scenes/Hong Kong Neon Highway.unity";
        private const string BaseScenePath = "Assets/Scenes/Save Striker Eureka.unity";

        // Core Spatial Constants (Gipsy SMR height = 24.56m, Leatherback = 33.87m H x 32.41m W)
        public const float GipsyHeight = 24.56f;
        public const float HarbourSeabedY = 0.0f;
        public const float HarbourWaterMeanY = 5.0f;  // Shin-to-knee deep ocean in Victoria Harbour; crests peak <= 7.4m
        public const float CityDeckY = 9.0f;          // Raised dry downtown street deck (1.6m clear above max harbour crest)

        public const float CanyonHalfWidth = 36.2f;   // 72.4m visual canyon floor; smooth colliders at ±35.8m (71.6m = 2.92x Gipsy)
        public const float HighwayAsphaltWidth = 66.8f;
        public const float SidewalkWidth = 2.8f;
        public const float CrossStreetWidth = 34.0f;

        [MenuItem("Pacific Rim/Build Hong Kong Neon Highway Level", false, 15)]
        public static void BuildNeonCityLevelMenu()
        {
            BuildCompleteNeonCityLevel();
        }

        public static void BuildCompleteNeonCityLevel()
        {
            if (!File.Exists(BaseScenePath))
            {
                Debug.LogError($"[NeonCity] Missing base scene '{BaseScenePath}'.");
                return;
            }

            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                EditorSceneManager.SaveOpenScenes();
            }

            // 1. Generate & bake all HK calligraphy signs, meshes, ocean, cloud, & beam assets
            NeonCityAssetPack assets = SetupNeonCityAssets.EnsureAllAssets();

            // 2. Duplicate base scene cleanly -> Hong Kong Neon Highway.unity
            if (SceneManager.GetActiveScene().path == ScenePath)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            if (File.Exists(ScenePath))
            {
                AssetDatabase.DeleteAsset(ScenePath);
            }
            if (!AssetDatabase.CopyAsset(BaseScenePath, ScenePath))
            {
                Debug.LogError($"[NeonCity] Failed to duplicate '{BaseScenePath}' to '{ScenePath}'.");
                return;
            }
            AssetDatabase.Refresh();

            Scene cityScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 3. Strip harbor ocean/debris/cutscene/Striker from base template so we build cleanly
            DestroyNamedObjects(
                "ProceduralOcean",
                "ProceduralOceanSplashParticles",
                "Floating_Debris_Field",
                "Chinook_Patrol_Squadron",
                "PacificRim_DeploymentCutscene",
                "striker",
                "Cutscene_FadeOverlay",
                "Cinematic_Letterbox",
                "--- Hong Kong Neon City ---",
                "Arena_Invisible_Barriers");

            // 4. Configure fighters in Harbour Spawn Zone -> facing Hong Kong Neon Canyon
            GameObject jaegerGo = ConfigureGipsyFighter();
            GameObject kaijuGo = ConfigureLeatherbackFighter(jaegerGo);

            // 5. Build Victoria Harbour, Causeway Ramp, Waterfront Promenade, Tiered HK Cityscape, Supertalls & Weather
            List<Collider> dryGroundColliders = new List<Collider>();
            BuildEnvironment(assets, jaegerGo, dryGroundColliders);

            // 6. Create ProceduralOcean (Victoria Harbour) with dryGroundColliders hybrid land detection
            ConfigureVictoriaHarbourOcean(assets, jaegerGo, kaijuGo, dryGroundColliders.ToArray());

            // 7. Configure night typhoon atmosphere, camera skyward tilt & clipping guard
            ConfigureAtmosphereWeatherAndCamera(jaegerGo);

            // 8. Build smooth funnel + canyon invisible arena barriers (zero facade snagging)
            BuildSmoothArenaBarriers();

            // 9. Rebuild & wire HUD + Pause Menu
            SetupPacificRimUIScene.SetupUI();
            var hud = UnityEngine.Object.FindAnyObjectByType<JaegerHUD>();
            if (hud != null)
            {
                if (jaegerGo != null) SetupPacificRimUIScene.SetSerializedField(hud, "jaeger", jaegerGo.GetComponent<SimpleJaegerController>());
                if (kaijuGo != null) SetupPacificRimUIScene.SetSerializedField(hud, "kaiju", kaijuGo.GetComponent<KaijuAI>());
            }

            var mainMenu = UnityEngine.Object.FindAnyObjectByType<MainMenuUI>();
            if (mainMenu != null)
            {
                mainMenu.isMainMenuScene = false;
                mainMenu.gameplaySceneName = SceneName;
                mainMenu.mainMenuSceneName = "MainMenuScene";
                mainMenu.isMenuOpen = false;
                mainMenu.SetMenuState(false);
                EditorUtility.SetDirty(mainMenu.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(cityScene);
            EditorSceneManager.SaveScene(cityScene);

            // 10. Sync Main Menu & Build Settings
            UpdateMainMenuAndBuildSettings();

            // 11. Re-open Hong Kong Neon Highway
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00ffaa><b>[NeonCity]</b></color> Rebuilt '{ScenePath}' with Victoria Harbour Spawn, Curved Waterfront & LEDs, Breakable Overpasses & Collateral Trams/Tankers, 3-Tier HK Neon Canyon & Supertall Storm Cloud Peaks!");
        }

        #region Fighter & Ocean Setup

        private static GameObject ConfigureGipsyFighter()
        {
            GameObject jaeger = GameObject.Find("Jaeger_Mech");
            if (jaeger == null)
            {
                Debug.LogWarning("[NeonCity] Jaeger_Mech not found.");
                return null;
            }

            // Spawn knee-deep in Victoria Harbour facing +Z toward Leatherback & Neon Skyline Canyon
            jaeger.transform.position = new Vector3(-4.0f, 2.977f, -72.0f);
            jaeger.transform.rotation = Quaternion.identity;

            var ctrl = jaeger.GetComponent<SimpleJaegerController>();
            if (ctrl != null)
            {
                ctrl.isCutsceneActive = false;
                ctrl.currentHealth = ctrl.maxHealth;
                ctrl.currentStamina = ctrl.maxStamina;
                ctrl.currentUltimateCharge = 0f;
                EditorUtility.SetDirty(ctrl);
            }

            var waveBody = jaeger.GetComponent<JaegerWaveBodyInteraction>();
            if (waveBody != null)
            {
                waveBody.enableBodySplashes = true;
                waveBody.enableCrestBubbles = true;
                EditorUtility.SetDirty(waveBody);
            }

            EditorUtility.SetDirty(jaeger);
            return jaeger;
        }

        private static GameObject ConfigureLeatherbackFighter(GameObject jaegerGo)
        {
            GameObject kaiju = GameObject.Find("Kaiju_Leatherback");
            if (kaiju == null)
            {
                Debug.LogWarning("[NeonCity] Kaiju_Leatherback not found.");
                return null;
            }

            // Spawn right at the Harbour shoreline / foot of Grand Causeway Ramp facing Gipsy
            kaiju.transform.position = new Vector3(4.0f, 2.0f, -16.0f);
            kaiju.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var ai = kaiju.GetComponent<KaijuAI>();
            if (ai != null)
            {
                ai.kaijuDisplayName = "LEATHERBACK";
                ai.kaijuCategoryLabel = "CATEGORY IV KAIJU";
                ai.isSlattern = false;
                ai.isCutsceneMode = false;
                ai.groundYOffset = 2.0f;
                ai.targetJaeger = jaegerGo != null ? jaegerGo.GetComponent<SimpleJaegerController>() : null;
                EditorUtility.SetDirty(ai);
            }

            var waveBody = kaiju.GetComponent<JaegerWaveBodyInteraction>();
            if (waveBody != null)
            {
                waveBody.enableBodySplashes = true;
                waveBody.enableCrestBubbles = false;
                EditorUtility.SetDirty(waveBody);
            }

            SetupLeatherbackNeonEmission.ApplyNeonEmission();
            EditorUtility.SetDirty(kaiju);
            return kaiju;
        }

        private static void ConfigureVictoriaHarbourOcean(NeonCityAssetPack assets, GameObject jaegerGo, GameObject kaijuGo, Collider[] dryColliders)
        {
            GameObject oceanObj = new GameObject("ProceduralOcean");
            Undo.RegisterCreatedObjectUndo(oceanObj, "Setup Victoria Harbour Ocean");
            oceanObj.transform.position = new Vector3(0f, HarbourWaterMeanY, 0f);
            oceanObj.transform.rotation = Quaternion.identity;
            oceanObj.transform.localScale = Vector3.one;

            var water = oceanObj.AddComponent<WaterSystem>();
            // Choppy dark Victoria Harbour storm waves (peaks stay well below CityDeckY = 9.0m)
            water.swellAmplitude1 = 0.55f;
            water.swellWavelength1 = 44f;
            water.swellSpeed1 = 1.45f;
            water.swellDirection1 = new Vector2(0.92f, 0.39f);
            water.swellSteepness1 = 0.42f;

            water.swellAmplitude2 = 0.36f;
            water.swellWavelength2 = 26f;
            water.swellSpeed2 = 1.65f;
            water.swellDirection2 = new Vector2(0.55f, -0.83f);
            water.swellSteepness2 = 0.38f;

            water.swellAmplitude3 = 0.24f;
            water.swellWavelength3 = 14f;
            water.swellSpeed3 = 1.90f;
            water.swellDirection3 = new Vector2(-0.68f, 0.73f);
            water.swellSteepness3 = 0.30f;

            water.chopAmplitude = 0.14f;
            water.chopWavelength = 4.2f;
            water.chopSpeed = 2.65f;
            water.waveSetBoost = 0.85f;
            water.maxRippleHeight = 1.6f;
            water.maxTotalWaveHeight = 2.4f;
            water.maxTroughDepth = 1.35f;
            water.wakeExpansionSpeed = 7.5f;
            water.wakeLifetime = 3.8f;
            water.followTarget = jaegerGo != null ? jaegerGo.transform : null;
            water.dryGroundColliders = dryColliders;

            oceanObj.AddComponent<MeshFilter>();
            var mr = oceanObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = assets.HarbourOceanMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;

            var oceanMesh = oceanObj.AddComponent<OceanWaterMesh>();
            oceanMesh.meshWidth = 860f;
            oceanMesh.meshLength = 860f;
            oceanMesh.resolution = 200;
            oceanMesh.foamHeightThreshold = 0.72f;
            oceanMesh.rippleFoamThreshold = 0.18f;
            oceanMesh.followTarget = jaegerGo != null ? jaegerGo.transform : null;
            oceanMesh.jaegerInteraction = jaegerGo != null ? jaegerGo.GetComponent<JaegerWaterInteraction>() : null;

            water.InitializeSimulation();
            oceanMesh.InitMesh();

            var splashMgr = UnityEngine.Object.FindAnyObjectByType<WaterSplashManager>();
            if (splashMgr != null)
            {
                // False = allows real Victoria Harbour splashes in water, while WaterSystem.IsDryLandAt swaps to asphalt dust up on City Deck!
                splashMgr.isDryLandMode = false;
                splashMgr.isDeepUnderwaterMode = false;
                EditorUtility.SetDirty(splashMgr);
            }
        }

        private static void ConfigureAtmosphereWeatherAndCamera(GameObject jaegerGo)
        {
            var lightning = UnityEngine.Object.FindAnyObjectByType<LightningSystem>();
            if (lightning != null)
            {
                lightning.sheetLightningOnly = true;
                lightning.flashPeakIntensity = 3.4f;
                lightning.minStrikeInterval = 4.0f;
                lightning.maxStrikeInterval = 8.0f;
                EditorUtility.SetDirty(lightning);
            }

            var weather = UnityEngine.Object.FindAnyObjectByType<StormWeatherManager>();
            if (weather != null)
            {
                weather.windDirection = new Vector2(0.88f, -0.46f);
                weather.baseWindSpeed = 28.0f;
                weather.gustStrength = 9.5f;
                weather.rainWetness = 0.96f;
                weather.followTarget = jaegerGo != null ? jaegerGo.transform : weather.followTarget;
                EditorUtility.SetDirty(weather);
            }

            var atmoSystems = GameObject.Find("Atmosphere_Systems");
            if (atmoSystems != null)
            {
                var atmoAudio = atmoSystems.GetComponent<AtmosphereAudioController>();
                if (atmoAudio != null)
                {
                    atmoAudio.isDeepUnderwater = false;
                    EditorUtility.SetDirty(atmoAudio);
                }

                var volCtrl = atmoSystems.GetComponent<AtmosphereVolumeController>();
                if (volCtrl != null)
                {
                    // Deep midnight-blue storm shadows contrasting against saturated crimson & cyan neon
                    volCtrl.stormSkyAmbient = new Color(0.08f, 0.13f, 0.25f, 1f);
                    volCtrl.stormEquatorAmbient = new Color(0.06f, 0.09f, 0.18f, 1f);
                    volCtrl.stormGroundAmbient = new Color(0.03f, 0.04f, 0.09f, 1f);
                    volCtrl.enableDenseFog = true;
                    volCtrl.fogDensity = 0.0028f;
                    volCtrl.fogColor = new Color(0.028f, 0.048f, 0.098f, 1f);
                    volCtrl.mainLightColor = new Color(0.42f, 0.56f, 0.86f, 1f);
                    volCtrl.mainLightIntensity = 0.62f;
                    volCtrl.bloomIntensity = 1.25f;
                    volCtrl.bloomThreshold = 0.88f;
                    volCtrl.bloomScatter = 0.78f;
                    volCtrl.postExposure = 0.26f;
                    volCtrl.contrast = 16f;
                    volCtrl.saturation = 8f;
                    volCtrl.ApplyLightingAndFog();
                    volCtrl.ApplyVolumeSettings();
                    EditorUtility.SetDirty(volCtrl);
                }
            }

            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (l.type == LightType.Directional && l.gameObject.name != "LightningFlashLight")
                {
                    l.transform.rotation = Quaternion.Euler(32f, -42f, 0f);
                    l.color = new Color(0.42f, 0.56f, 0.86f, 1f);
                    l.intensity = 0.62f;
                    l.shadows = LightShadows.Soft;
                    l.shadowStrength = 0.72f;
                    EditorUtility.SetDirty(l.gameObject);
                    break;
                }
            }

            Camera mainCam = Camera.main ?? GameObject.FindWithTag("MainCamera")?.GetComponent<Camera>();
            if (mainCam != null)
            {
                mainCam.farClipPlane = 850f;
                mainCam.backgroundColor = new Color(0.032f, 0.052f, 0.105f, 1f);

                var lowPass = mainCam.GetComponent<AudioLowPassFilter>();
                if (lowPass != null) lowPass.cutoffFrequency = 22000f;

                var camFollow = mainCam.GetComponent<JaegerCameraFollow>();
                if (camFollow != null)
                {
                    camFollow.target = jaegerGo != null ? jaegerGo.transform : camFollow.target;
                    camFollow.preventEnvironmentClipping = true;
                    camFollow.isFreeCam = false;
                    camFollow.minPitch = -42f; // Allow tilting skyward toward 150m supertall peaks!
                    camFollow.maxPitch = 60f;
                    camFollow.CurrentYaw = 0f;

                    Vector3 targetPos = jaegerGo != null ? jaegerGo.transform.position : new Vector3(-4f, 2.977f, -72f);
                    Quaternion initRot = Quaternion.Euler(8f, 0f, 0f);
                    Vector3 pivot = targetPos + camFollow.targetOffset + Vector3.right * camFollow.rightShoulderOffset + Vector3.up * camFollow.height;
                    mainCam.transform.position = pivot - (initRot * Vector3.forward * camFollow.distance);
                    mainCam.transform.LookAt(pivot + (initRot * Vector3.forward * 140f));
                    EditorUtility.SetDirty(camFollow);
                }

                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.AlignViewToObject(mainCam.transform);
                }
                EditorUtility.SetDirty(mainCam.gameObject);
            }
        }

        #endregion

        #region Environment Orchestration

        private static void BuildEnvironment(NeonCityAssetPack assets, GameObject jaegerGo, List<Collider> dryColliders)
        {
            // Configure Harbour Seabed ground collider at Y = 0
            GameObject platform = GameObject.Find("Test_Platform");
            if (platform != null)
            {
                platform.transform.position = new Vector3(-40f, -0.5f, 40f);
                platform.transform.rotation = Quaternion.identity;
                platform.transform.localScale = new Vector3(1100f, 1f, 1100f);
                platform.tag = "Ground";
                platform.layer = 0;

                var mr = platform.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = assets.DarkConcreteMat;

                var col = platform.GetComponent<BoxCollider>() ?? platform.AddComponent<BoxCollider>();
                col.center = Vector3.zero;
                col.size = Vector3.one;
                col.enabled = true;
                EditorUtility.SetDirty(platform);
            }

            GameObject root = new GameObject("--- Hong Kong Neon City ---");
            Undo.RegisterCreatedObjectUndo(root, "Create Hong Kong Neon City");

            BuildDecksAndHarbourRamp(root.transform, assets, dryColliders);
            BuildVictoriaHarbourZone(root.transform, assets);
            BuildHighwayDeckAndCrossStreets(root.transform, assets);
            BuildTier1FlyoversAndMicroCollateral(root.transform, assets);
            BuildTier2And3CitySkyline(root.transform, assets);
            BuildSkybridgesAndCantileveredCanyonSigns(root.transform, assets);
            BuildTyphoonCloudsMistAndReflections(root.transform, assets);

            EditorUtility.SetDirty(root);
        }

        private static void BuildDecksAndHarbourRamp(Transform parent, NeonCityAssetPack assets, List<Collider> dryColliders)
        {
            GameObject deckGroup = new GameObject("City_Harbour_Decks_And_Ramp");
            deckGroup.transform.SetParent(parent, false);

            // 1. East Waterfront Concrete Promenade & Downtown Base Deck (X in [+36.5, +380], Z in [-240, +24])
            //    Creates crisp Harbour seawall directly facing West onto Victoria Harbour!
            BoxCollider eastQuay = CreateGroundSlab(deckGroup.transform, "Deck_EastWaterfront",
                new Vector3(36.5f, -2.0f, -240f), new Vector3(380f, CityDeckY, 24f), assets.DarkConcreteMat);
            dryColliders.Add(eastQuay);

            // 2. Central-to-East Waterfront Causeway Apron (X in [-36.5, +380], Z in [+18, +64])
            //    Lets highway reach downtown while leaving X < -36.5 open choppy Harbour water up to Z = +64!
            BoxCollider midApron = CreateGroundSlab(deckGroup.transform, "Deck_WaterfrontTransition",
                new Vector3(-36.5f, -2.0f, 18f), new Vector3(380f, CityDeckY, 64f), assets.DarkConcreteMat);
            dryColliders.Add(midApron);

            // 3. Full Downtown North Cityscape Deck (X in [-380, +380], Z in [+64, +390])
            BoxCollider northDeck = CreateGroundSlab(deckGroup.transform, "Deck_DowntownNorth",
                new Vector3(-380f, -2.0f, 64f), new Vector3(380f, CityDeckY, 390f), assets.DarkConcreteMat);
            dryColliders.Add(northDeck);

            // 4. Grand Harbour Causeway Ramp rising gently from Harbour Seabed (Z=-42, Y=0) up to CityDeck (Z=+18, Y=9)
            float rampZ0 = -42f;
            float rampZ1 = 18f;
            float run = rampZ1 - rampZ0; // 60m
            float rise = CityDeckY;      // 9m
            float slopeDeg = Mathf.Atan2(rise, run) * Mathf.Rad2Deg; // 8.53 deg
            float hyp = Mathf.Sqrt(run * run + rise * rise);
            float slabThick = 2.0f;

            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Harbour_Grand_Ramp";
            ramp.transform.SetParent(deckGroup.transform, false);
            ramp.tag = "Ground";
            // Offset downward perpendicular to slope so TOP face passes smoothly through (Z=-42, Y=0) -> (Z=+18, Y=9)
            Vector3 slopeNormal = Quaternion.Euler(-slopeDeg, 0f, 0f) * Vector3.up;
            Vector3 topMid = new Vector3(0f, rise * 0.5f, (rampZ0 + rampZ1) * 0.5f);
            ramp.transform.position = topMid - slopeNormal * (slabThick * 0.5f);
            ramp.transform.rotation = Quaternion.Euler(-slopeDeg, 0f, 0f);
            ramp.transform.localScale = new Vector3(72.0f, slabThick, hyp + 3.0f);
            ramp.GetComponent<MeshRenderer>().sharedMaterial = assets.HighwayMat;
            BoxCollider rampCol = ramp.GetComponent<BoxCollider>();
            dryColliders.Add(rampCol);

            // Flanked Seawall Embankments & Parapet Curbs along East Quay (X = +36.6) & Harbour Ramp Sidewalls
            CreateSubBox(deckGroup.transform, "Seawall_EastQuay_Wall",
                new Vector3(36.7f, (CityDeckY + 0.55f) * 0.5f - 1.0f, -108f),
                new Vector3(1.8f, CityDeckY + 2.6f, 252f), assets.BarrierMat);
            CreateSubBox(deckGroup.transform, "Seawall_EastQuay_NeonTrim",
                new Vector3(35.8f, CityDeckY + 0.45f, -108f),
                new Vector3(0.32f, 0.22f, 250f), assets.NeonTubeMats[0]);

            // West Retaining Seawall along Ramp -> North Waterfront transition (X = -36.7, Z in [-42..+64])
            CreateSubBox(deckGroup.transform, "Seawall_WestCauseway_Wall",
                new Vector3(-36.7f, (CityDeckY + 0.55f) * 0.5f - 1.0f, 11f),
                new Vector3(1.8f, CityDeckY + 2.6f, 106f), assets.BarrierMat);
            CreateSubBox(deckGroup.transform, "Seawall_WestCauseway_NeonTrim",
                new Vector3(-35.8f, CityDeckY + 0.45f, 11f),
                new Vector3(0.32f, 0.22f, 104f), assets.NeonTubeMats[5]);

            // North Bay Promenade Seawall along Z = +64 across West Harbour (X in [-195 .. -36.5])
            CreateSubBox(deckGroup.transform, "Seawall_NorthBay_Wall",
                new Vector3(-116f, (CityDeckY + 0.55f) * 0.5f - 1.0f, 64f),
                new Vector3(160f, CityDeckY + 2.6f, 1.8f), assets.BarrierMat);
            CreateSubBox(deckGroup.transform, "Seawall_NorthBay_NeonTrim",
                new Vector3(-116f, CityDeckY + 0.45f, 63.1f),
                new Vector3(158f, 0.24f, 0.34f), assets.NeonTubeMats[0]);
        }

        private static BoxCollider CreateGroundSlab(Transform parent, string name, Vector3 minCorner, Vector3 maxCorner, Material mat)
        {
            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.SetParent(parent, false);
            slab.tag = "Ground";
            slab.layer = 0;
            Vector3 size = maxCorner - minCorner;
            slab.transform.position = (minCorner + maxCorner) * 0.5f;
            slab.transform.localScale = size;
            var mr = slab.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
            return slab.GetComponent<BoxCollider>();
        }

        private static void BuildVictoriaHarbourZone(Transform parent, NeonCityAssetPack assets)
        {
            GameObject harbourRoot = new GameObject("Victoria_Harbour_Waterfront");
            harbourRoot.transform.SetParent(parent, false);

            // 1. West Breakwater Industrial Dock Pier (outside playable bay at X = -96..-64, Z = -140..-10)
            CreateSubBox(harbourRoot.transform, "Harbour_WestPier_Deck",
                new Vector3(-86f, 3.8f, -72f), new Vector3(36f, 9.6f, 145f), assets.DarkConcreteMat);
            CreateSubBox(harbourRoot.transform, "Harbour_WestPier_NeonRim",
                new Vector3(-68.1f, 8.6f, -72f), new Vector3(0.35f, 0.25f, 144f), assets.NeonTubeMats[2]);

            // Red-Orange Harbour Container Cranes looming above the breakwater dock
            float[] craneZs = { -102f, -48f };
            for (int i = 0; i < craneZs.Length; i++)
            {
                BuildHarbourContainerCrane(harbourRoot.transform, $"Harbour_Crane_{i}", new Vector3(-80f, 8.6f, craneZs[i]), 90f, assets);
            }

            // Stacked Intermodal Cargo Containers on West Pier
            for (int i = 0; i < 6; i++)
            {
                float cz = -120f + i * 16f;
                Material cMat = i % 3 == 0 ? assets.TankerHazardMat : (i % 3 == 1 ? assets.TramGreenMat : assets.DarkSteelMat);
                CreateSubBox(harbourRoot.transform, $"Harbour_Container_{i}",
                    new Vector3(-76f + (i % 2) * 3.5f, 9.9f, cz), new Vector3(2.8f, 2.6f, 11.5f), cMat);
            }

            // 2. Moored Victoria Harbour Cargo Ship & Star Ferry Silhouette in open West Bay
            BuildHarbourCargoShip(harbourRoot.transform, "Harbour_CargoShip_W", new Vector3(-78f, 4.4f, 12f), 12f, assets);
            BuildHarbourCargoShip(harbourRoot.transform, "Harbour_CargoShip_S", new Vector3(-92f, 4.4f, -155f), 74f, assets);

            // Channel Navigation Buoys in Victoria Harbour
            Vector3[] buoyPos = { new Vector3(-48f, 4.9f, -92f), new Vector3(-50f, 4.9f, -38f), new Vector3(-42f, 4.9f, 4f) };
            for (int i = 0; i < buoyPos.Length; i++)
            {
                GameObject buoy = CreateSubBox(harbourRoot.transform, $"Harbour_Buoy_{i}", buoyPos[i], new Vector3(1.6f, 2.8f, 1.6f), assets.TankerHazardMat);
                CreateSubBox(buoy.transform, "Beacon", new Vector3(0f, 0.72f, 0f), new Vector3(0.45f, 0.45f, 0.45f), assets.NeonTubeMats[i % 2 == 0 ? 5 : 4]);
            }

            // 3. East Quay Waterfront Promenade & Curved Gull-Wing Harbour Convention Pavilion
            //    Sits right along X = +42..+110 overlooking Victoria Harbour at Z = -145..+18
            BuildCurvedWaterfrontPavilion(harbourRoot.transform, new Vector3(62f, CityDeckY, -82f), assets);

            // Giant Blazing Waterfront LED Billboards on East Quay facing the Harbour waves + Volumetric Light Beams!
            BuildWaterfrontLedBillboard(harbourRoot.transform, "Waterfront_LED_South",
                new Vector3(45f, CityDeckY + 21.5f, -108f), -72f, new Vector3(28f, 13f, 0.8f),
                assets.WaterfrontLedMats[0], assets.VolumetricBeamMats[1], new Color(0.14f, 0.92f, 1.0f), assets);

            BuildWaterfrontLedBillboard(harbourRoot.transform, "Waterfront_LED_Shoreline",
                new Vector3(45f, CityDeckY + 22.5f, -46f), -85f, new Vector3(30f, 14f, 0.8f),
                assets.WaterfrontLedMats[1], assets.VolumetricBeamMats[0], new Color(1.0f, 0.18f, 0.42f), assets);

            // West-side Waterfront LED Billboard right where Victoria Harbour meets the Downtown Canyon
            BuildWaterfrontLedBillboard(harbourRoot.transform, "Waterfront_LED_EsplanadeWest",
                new Vector3(-43f, CityDeckY + 20.5f, 28f), 62f, new Vector3(26f, 12.5f, 0.8f),
                assets.WaterfrontLedMats[2], assets.VolumetricBeamMats[3], new Color(0.95f, 0.22f, 0.82f), assets);

            // Upward Harbour Searchlights on East Seawall slicing through the typhoon storm
            float[] searchZs = { -126f, -66f, -8f };
            for (int i = 0; i < searchZs.Length; i++)
            {
                BuildVolumetricSearchlight(harbourRoot.transform, $"Harbour_Searchlight_{i}",
                    new Vector3(40f, CityDeckY + 1.0f, searchZs[i]),
                    new Vector3(-16f, 0f, 18f),
                    assets.VolumetricBeamMats[i % 2 == 0 ? 1 : 0], assets.DarkSteelMat);
            }

            // 4. Distant Kowloon Peninsula Seawall & Dense Harbourfront Skyline across Victoria Harbour (X = -195 .. -285)
            CreateSubBox(harbourRoot.transform, "Kowloon_BaySeawall_Quay",
                new Vector3(-245f, HarbourWaterMeanY - 1.5f, 0f), new Vector3(110f, 9.0f, 560f), assets.DarkConcreteMat);
            CreateSubBox(harbourRoot.transform, "Kowloon_BaySeawall_NeonRim",
                new Vector3(-189.8f, HarbourWaterMeanY + 3.1f, 0f), new Vector3(0.45f, 0.35f, 550f), assets.NeonTubeMats[0]);

            for (int i = 0; i < 16; i++)
            {
                float kz = -230f + i * 32f;
                float kh = Mathf.Lerp(46f, 126f, ((i * 7 + 3) % 11) / 10f);
                float kw = Mathf.Lerp(16f, 30f, ((i * 5 + 1) % 7) / 6f);
                Vector3 pos = new Vector3(-206f - (i % 3) * 26f, HarbourWaterMeanY + 3.0f, kz);
                SpawnTieredSkyscraper(harbourRoot.transform, $"Kowloon_BaySkyline_{i}", pos, 90f,
                    kw, kw * 0.9f, kh, i % 4, i % assets.WindowMats.Length, i % assets.NeonTubeMats.Length,
                    addCollider: false, assets);
            }
            // Giant Glowing Harbourfront LED signs on Distant Kowloon Bay side
            BuildWaterfrontLedBillboard(harbourRoot.transform, "Kowloon_FarBay_LED_A",
                new Vector3(-192f, 24f, -35f), 90f, new Vector3(52f, 18f, 1.2f),
                assets.WaterfrontLedMats[0], assets.VolumetricBeamMats[1], new Color(0.15f, 0.95f, 1.0f), assets);
            BuildWaterfrontLedBillboard(harbourRoot.transform, "Kowloon_FarBay_LED_B",
                new Vector3(-192f, 26f, 52f), 90f, new Vector3(48f, 16f, 1.2f),
                assets.WaterfrontLedMats[1], assets.VolumetricBeamMats[0], new Color(1.0f, 0.22f, 0.45f), assets);
        }

        private static void BuildHarbourContainerCrane(Transform parent, string name, Vector3 pos, float yaw, NeonCityAssetPack assets)
        {
            GameObject crane = new GameObject(name);
            crane.transform.SetParent(parent, false);
            crane.transform.position = pos;
            crane.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // 4 tall portal legs
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iz = -1; iz <= 1; iz += 2)
                    CreateSubBox(crane.transform, $"Leg_{ix}_{iz}", new Vector3(ix * 4.5f, 14f, iz * 4.5f), new Vector3(1.2f, 28f, 1.2f), assets.TankerHazardMat);

            // Horizontal gantry beam & jutting waterward boom
            CreateSubBox(crane.transform, "Boom_Beam", new Vector3(0f, 25f, 10f), new Vector3(2.4f, 2.2f, 44f), assets.TankerHazardMat);
            CreateSubBox(crane.transform, "Machinery_Cab", new Vector3(0f, 27.5f, -4f), new Vector3(5.5f, 3.6f, 8.5f), assets.DarkSteelMat);
            CreateSubBox(crane.transform, "Apex_Tower", new Vector3(0f, 34f, -2f), new Vector3(1.6f, 14f, 1.6f), assets.TankerHazardMat);
            CreateSubBox(crane.transform, "Aviation_Beacon", new Vector3(0f, 41.2f, -2f), new Vector3(0.9f, 0.9f, 0.9f), assets.NeonTubeMats[5]);
        }

        private static void BuildHarbourCargoShip(Transform parent, string name, Vector3 pos, float yaw, NeonCityAssetPack assets)
        {
            GameObject ship = new GameObject(name);
            ship.transform.SetParent(parent, false);
            ship.transform.position = pos;
            ship.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            CreateSubBox(ship.transform, "Hull", new Vector3(0f, 1.2f, 0f), new Vector3(12f, 5.5f, 48f), assets.DarkSteelMat);
            CreateSubBox(ship.transform, "Hull_RedWaterline", new Vector3(0f, -0.5f, 0f), new Vector3(12.2f, 2.2f, 48.2f), assets.TankerHazardMat);
            CreateSubBox(ship.transform, "Bridge", new Vector3(0f, 6.2f, -15f), new Vector3(10.5f, 7.5f, 9f), assets.TongLauConcreteMat);
            CreateSubBox(ship.transform, "Bridge_Windows", new Vector3(0f, 8.2f, -10.4f), new Vector3(9.2f, 1.4f, 0.4f), assets.NeonTubeMats[2]);
            CreateSubBox(ship.transform, "Deck_Containers_A", new Vector3(-2.6f, 4.8f, 5f), new Vector3(4.2f, 3.2f, 24f), assets.TramGreenMat);
            CreateSubBox(ship.transform, "Deck_Containers_B", new Vector3(2.6f, 5.4f, 4f), new Vector3(4.2f, 4.4f, 22f), assets.TankerHazardMat);
        }

        private static void BuildCurvedWaterfrontPavilion(Transform parent, Vector3 basePos, NeonCityAssetPack assets)
        {
            GameObject pav = new GameObject("Harbour_CurvedWaterfrontPavilion");
            pav.transform.SetParent(parent, false);
            pav.transform.position = basePos;
            pav.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

            // Sweeping curved glass concourse overlooking Victoria Harbour + undulating gull-wing roof slabs
            GameObject arc = CreateCustomMeshGO(pav.transform, "Curved_Glass_Hall", assets.CurvedArcTowerMesh,
                new Vector3(0f, 0f, 0f), new Vector3(72f, 22f, 42f), assets.WindowMats[5]);
            CreateCustomMeshGO(pav.transform, "Curved_Neon_Collar", assets.CurvedArcTowerMesh,
                new Vector3(0f, 22f, 0f), new Vector3(74f, 0.65f, 43.5f), assets.NeonTubeMats[0]);
            CreateSubBox(pav.transform, "GullWing_Roof_A", new Vector3(-12f, 25f, 0f), new Vector3(44f, 1.4f, 38f), assets.DarkSteelMat, Quaternion.Euler(0f, 0f, -9f));
            CreateSubBox(pav.transform, "GullWing_Roof_B", new Vector3(14f, 24.5f, 0f), new Vector3(44f, 1.4f, 38f), assets.DarkSteelMat, Quaternion.Euler(0f, 0f, 11f));
        }

        internal static GameObject BuildWaterfrontLedBillboard(
            Transform parent, string name, Vector3 pos, float yaw, Vector3 size,
            Material ledMat, Material beamMat, Color lightCol, NeonCityAssetPack assets, float groundBaseY = CityDeckY)
        {
            GameObject board = new GameObject(name);
            board.transform.SetParent(parent, false);
            board.transform.position = pos;
            board.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Support steel stanchions down to ground
            float stanchionH = Mathf.Max(2f, pos.y - groundBaseY);
            CreateSubBox(board.transform, "Stanchion_L", new Vector3(-size.x * 0.34f, -stanchionH * 0.5f, 0f), new Vector3(1.2f, stanchionH, 1.2f), assets.DarkSteelMat);
            CreateSubBox(board.transform, "Stanchion_R", new Vector3(size.x * 0.34f, -stanchionH * 0.5f, 0f), new Vector3(1.2f, stanchionH, 1.2f), assets.DarkSteelMat);

            CreateNeonBillboard(board.transform, "LED_Screen", Vector3.zero, size, ledMat, assets.DarkSteelMat, doubleSided: true);

            // Volumetric Billboard Light Beam fanning forward out into the typhoon rain (soft cylinder, zero cube corners)
            if (beamMat != null)
            {
                GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beam.name = "Beam_BillboardCone";
                beam.transform.SetParent(board.transform, false);
                beam.transform.localPosition = new Vector3(0f, -3.2f, size.x * 0.42f);
                beam.transform.localRotation = Quaternion.Euler(78f, 0f, 0f);
                beam.transform.localScale = new Vector3(size.x * 0.62f, size.x * 0.45f, size.y * 0.78f);
                var col = beam.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
                var mr = beam.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = beamMat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
            }

            // Spill point light illuminating nearby harbour water / wet quay
            GameObject lightGo = new GameObject("LED_WaterSpillLight");
            lightGo.transform.SetParent(board.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -2f, 8f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = lightCol;
            l.intensity = 240f;
            l.range = 75f;
            l.shadows = LightShadows.None;
            return board;
        }

        internal static GameObject BuildVolumetricSearchlight(Transform parent, string name, Vector3 basePos, Vector3 tiltEuler, Material beamMat, Material steelMat)
        {
            GameObject housing = new GameObject(name);
            housing.transform.SetParent(parent, false);
            housing.transform.position = basePos;
            CreateSubBox(housing.transform, "Projector", Vector3.zero, new Vector3(2.2f, 1.8f, 2.2f), steelMat);

            GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cone.name = "Beam_SkySearchlight";
            cone.transform.SetParent(housing.transform, false);
            cone.transform.localRotation = Quaternion.Euler(tiltEuler);
            cone.transform.localPosition = Quaternion.Euler(tiltEuler) * new Vector3(0f, 48f, 0f);
            cone.transform.localScale = new Vector3(7.5f, 48f, 7.5f);
            var col = cone.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            var mr = cone.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = beamMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            return housing;
        }

        #endregion

        #region Highways, Tier 1 Flyovers & Micro Collateral

        private static void BuildHighwayDeckAndCrossStreets(Transform parent, NeonCityAssetPack assets)
        {
            GameObject roadGroup = new GameObject("City_Highway_Roadway");
            roadGroup.transform.SetParent(parent, false);

            // Flat 8-lane downtown highway from ramp crest Z=+18 to Z=+298 at Y = CityDeckY + 0.02m (4 segments x 70m)
            const float segLen = 70f;
            for (int i = 0; i < 4; i++)
            {
                float zCenter = 18f + segLen * (i + 0.5f);
                CreateRoadQuad(roadGroup.transform, $"Highway_8Lane_Segment_{i}",
                    new Vector3(0f, CityDeckY + 0.02f, zCenter),
                    new Vector3(HighwayAsphaltWidth, segLen, 1f), assets.HighwayMat);
            }

            // Two downtown Cross-Street Intersections (Z = +58m and Z = +154m)
            float[] crossZs = { 58f, 154f };
            for (int i = 0; i < crossZs.Length; i++)
            {
                float cz = crossZs[i];
                CreateRoadQuad(roadGroup.transform, $"Highway_Intersection_{i}",
                    new Vector3(0f, CityDeckY + 0.03f, cz),
                    new Vector3(HighwayAsphaltWidth, CrossStreetWidth, 1f), assets.IntersectionMat);

                CreateRoadQuad(roadGroup.transform, $"CrossStreet_East_{i}",
                    new Vector3(135f, CityDeckY + 0.018f, cz),
                    new Vector3(200f, CrossStreetWidth, 1f), assets.DarkConcreteMat);

                if (cz > 70f)
                {
                    CreateRoadQuad(roadGroup.transform, $"CrossStreet_West_{i}",
                        new Vector3(-135f, CityDeckY + 0.018f, cz),
                        new Vector3(200f, CrossStreetWidth, 1f), assets.DarkConcreteMat);
                }
                else
                {
                    // At Z=+58, West cross-street opens straight into a Victoria Harbour Waterfront Esplanade Pier
                    CreateRoadQuad(roadGroup.transform, "CrossStreet_West_Pier",
                        new Vector3(-48f, CityDeckY + 0.018f, cz),
                        new Vector3(32f, CrossStreetWidth, 1f), assets.SidewalkMat);
                }
            }

            // Downtown Sidewalks lining main avenue
            GameObject swGroup = new GameObject("City_Sidewalks");
            swGroup.transform.SetParent(parent, false);
            (float minZ, float maxZ, bool hasWestSidewalk)[] swZones =
            {
                (18f, 41f, true),
                (75f, 137f, true),
                (171f, 298f, true),
            };
            float swX = HighwayAsphaltWidth * 0.5f + SidewalkWidth * 0.5f;
            for (int z = 0; z < swZones.Length; z++)
            {
                float lenZ = swZones[z].maxZ - swZones[z].minZ;
                float midZ = (swZones[z].minZ + swZones[z].maxZ) * 0.5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    if (side < 0 && !swZones[z].hasWestSidewalk) continue;
                    GameObject sw = CreateSubBox(swGroup.transform, $"Sidewalk_{(side < 0 ? "W" : "E")}_{z}",
                        new Vector3(side * swX, CityDeckY + 0.09f, midZ),
                        new Vector3(SidewalkWidth, 0.18f, lenZ), assets.SidewalkMat);
                    sw.tag = "Ground";
                }
            }
        }

        internal static GameObject CreateRoadQuad(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            q.transform.SetParent(parent, false);
            q.transform.localPosition = localPos;
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            q.transform.localScale = scale;
            q.tag = "Ground";
            var col = q.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            var mr = q.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
            return q;
        }

        private static void BuildTier1FlyoversAndMicroCollateral(Transform parent, NeonCityAssetPack assets)
        {
            GameObject t1Root = new GameObject("City_Tier1_Flyovers_And_Collateral");
            t1Root.transform.SetParent(parent, false);

            // 1. Sleek Elevated Concrete Highway Flyovers along waterfront curb edge (leaving ground-floor Tong Lau & waterfront LEDs unobstructed!)
            BuildFlankingExpresswayViaduct(t1Root.transform, "Flyover_East_WaterfrontToCanyon",
                xCenter: 35.4f, zStart: -95f, zEnd: 245f, deckRelY: 4.5f, upperTierRelY: -1f, assets);
            BuildFlankingExpresswayViaduct(t1Root.transform, "Flyover_West_Canyon",
                xCenter: -35.4f, zStart: 36f, zEnd: 245f, deckRelY: 4.5f, upperTierRelY: -1f, assets);

            // 2. Breakable Low Concrete Overpass Spans across main fighting boulevard (knee level: CityDeckY + 4.4m)
            //    Uses UrbanCollateralProp (ShatterOnTitanPass) so Jaeger/Leatherback step straight through without snagging!
            BuildBreakableOverpassBridge(t1Root.transform, "Overpass_Breakable_WaterfrontEntry",
                new Vector3(0f, CityDeckY + 4.4f, 44f), widthX: 66f, assets);
            BuildBreakableOverpassBridge(t1Root.transform, "Overpass_Breakable_MidCanyon",
                new Vector3(0f, CityDeckY + 4.6f, 134f), widthX: 66f, assets);

            // 3. Jersey Curbs + Roadblocks closing side-streets visually cleanly at canyon walls
            float[] crossZs = { 58f, 154f };
            for (int c = 0; c < crossZs.Length; c++)
            {
                float cz = crossZs[c];
                for (int side = -1; side <= 1; side += 2)
                {
                    if (c == 0 && side < 0) continue; // Z=+58 West stays open pier view of Victoria Harbour!
                    float bx = side * (CanyonHalfWidth + 0.6f);
                    CreateSubBox(t1Root.transform, $"Roadblock_Cross_{(side < 0 ? "W" : "E")}_{c}",
                        new Vector3(bx, CityDeckY + 0.55f, cz), new Vector3(1.1f, 1.1f, 32f), assets.BarrierMat);
                    CreateSubBox(t1Root.transform, $"Roadblock_Beacon_{(side < 0 ? "W" : "E")}_{c}",
                        new Vector3(bx, CityDeckY + 1.22f, cz), new Vector3(0.35f, 0.22f, 30f), assets.NeonTubeMats[2]);
                }
            }

            // North End-of-Arena Expressway Gantry + Roadblock at Z = +238m
            BuildExpresswayGantry(t1Root.transform, "Gantry_Expressway_NorthEnd", 238f, 180f, assets);

            // 4. Hong Kong Double-Decker Trams ("Ding Ding"), Fuel Tankers & Kicked Street Traffic!
            Vector3[] tramPositions =
            {
                new Vector3(-18.5f, CityDeckY + 0.04f, 26f),
                new Vector3(19.2f,  CityDeckY + 0.04f, 52f),
                new Vector3(-17.8f, CityDeckY + 0.04f, 88f),
                new Vector3(18.6f,  CityDeckY + 0.04f, 118f),
                new Vector3(-19.0f, CityDeckY + 0.04f, 168f),
                new Vector3(17.5f,  CityDeckY + 0.04f, 202f),
            };
            for (int i = 0; i < tramPositions.Length; i++)
            {
                SpawnHKDoubleDeckerTram(t1Root.transform, $"Collateral_HKTram_{i}", tramPositions[i], i % 2 == 0 ? 0f : 180f, assets);
            }

            Vector3[] tankerPositions =
            {
                new Vector3(-8.5f,  CityDeckY + 0.04f, 34f),
                new Vector3(9.5f,   CityDeckY + 0.04f, 76f),
                new Vector3(-7.0f,  CityDeckY + 0.04f, 126f),
                new Vector3(11.0f,  CityDeckY + 0.04f, 182f),
            };
            for (int i = 0; i < tankerPositions.Length; i++)
            {
                SpawnFuelTanker(t1Root.transform, $"Collateral_FuelTanker_{i}", tankerPositions[i], (i % 2 == 0 ? 12f : 168f), assets);
            }

            // Knockable Taxis & Sedans along highway lanes
            GameObject buildingsFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Buildings.fbx");
            if (buildingsFbx != null)
            {
                string[] carTypes = { "Sedan Body", "SUV Body", "Pickup Body", "Compact Body", "Sport body" };
                float[] carZs = { 22f, 40f, 64f, 82f, 102f, 116f, 146f, 174f, 196f, 214f };
                for (int i = 0; i < carZs.Length; i++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float xOff = side * (12f + ((i * 3 + (side > 0 ? 1 : 0)) % 14));
                        SpawnCollateralCar(t1Root.transform, buildingsFbx, carTypes[i % carTypes.Length],
                            new Vector3(xOff, CityDeckY + 0.16f, carZs[i]), side < 0 ? 176f + i * 3f : -6f + i * 2f,
                            assets.CarBodyMats[i % assets.CarBodyMats.Length], assets.TailLightMat);
                    }
                }
            }

            // 5. Sleek Expressway Streetlights lining sidewalks
            for (float z = 24f; z <= 230f; z += 36f)
            {
                if (Mathf.Abs(z - 58f) < 19f || Mathf.Abs(z - 154f) < 19f) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject pole = new GameObject($"Streetlight_{(side < 0 ? "W" : "E")}_{z:F0}");
                    pole.transform.SetParent(t1Root.transform, false);
                    pole.transform.position = new Vector3(side * (HighwayAsphaltWidth * 0.5f + 0.8f), CityDeckY, z);
                    pole.transform.rotation = Quaternion.Euler(0f, side < 0 ? 90f : 270f, 0f);
                    CreateSubBox(pole.transform, "Shaft", new Vector3(0f, 4.6f, 0f), new Vector3(0.28f, 9.2f, 0.28f), assets.DarkSteelMat);
                    CreateSubBox(pole.transform, "Arm", new Vector3(0f, 9.0f, 1.3f), new Vector3(0.22f, 0.22f, 2.8f), assets.DarkSteelMat);
                    CreateSubBox(pole.transform, "LED", new Vector3(0f, 8.82f, 2.2f), new Vector3(0.70f, 0.16f, 1.25f), assets.NeonTubeMats[0]);
                }
            }
        }

        private static void BuildFlankingExpresswayViaduct(Transform parent, string name, float xCenter, float zStart, float zEnd, float deckRelY, float upperTierRelY, NeonCityAssetPack assets)
        {
            GameObject viaduct = new GameObject(name);
            viaduct.transform.SetParent(parent, false);
            float len = zEnd - zStart;
            float midZ = (zStart + zEnd) * 0.5f;
            float baseY = CityDeckY;

            // Main concrete box-girder expressway deck + parapet rails
            CreateSubBox(viaduct.transform, "Girder_Deck_Lower", new Vector3(xCenter, baseY + deckRelY, midZ), new Vector3(6.8f, 1.15f, len), assets.DarkConcreteMat);
            CreateSubBox(viaduct.transform, "Parapet_Inner", new Vector3(xCenter - Mathf.Sign(xCenter) * 3.1f, baseY + deckRelY + 0.9f, midZ), new Vector3(0.45f, 0.85f, len), assets.BarrierMat);
            CreateSubBox(viaduct.transform, "Neon_GuardStrip", new Vector3(xCenter - Mathf.Sign(xCenter) * 3.4f, baseY + deckRelY + 0.15f, midZ), new Vector3(0.22f, 0.18f, len), assets.NeonTubeMats[2]);

            // Optional 2nd upper elevated tier for true multi-tier Hong Kong interchange layering!
            if (upperTierRelY > 0f)
            {
                float upperX = xCenter + Mathf.Sign(xCenter) * 4.4f;
                CreateSubBox(viaduct.transform, "Girder_Deck_Upper", new Vector3(upperX, baseY + upperTierRelY, midZ), new Vector3(6.2f, 1.05f, len), assets.DarkConcreteMat);
                CreateSubBox(viaduct.transform, "Parapet_Upper", new Vector3(upperX - Mathf.Sign(xCenter) * 2.8f, baseY + upperTierRelY + 0.85f, midZ), new Vector3(0.45f, 0.80f, len), assets.BarrierMat);
            }

            // Support cylindrical piers down to City Deck / Harbour seabed
            for (float z = zStart + 14f; z <= zEnd - 10f; z += 32f)
            {
                bool overHarbour = xCenter < 0f && z < 18f;
                float pierBottom = overHarbour ? 0f : baseY;
                float pierTop = baseY + deckRelY;
                float pierH = pierTop - pierBottom;
                GameObject col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                col.name = $"Viaduct_Pier_{z:F0}";
                col.transform.SetParent(viaduct.transform, false);
                col.transform.position = new Vector3(xCenter, pierBottom + pierH * 0.5f, z);
                col.transform.localScale = new Vector3(1.8f, pierH * 0.5f, 1.8f);
                var pc = col.GetComponent<Collider>();
                if (pc != null) UnityEngine.Object.DestroyImmediate(pc);
                col.GetComponent<MeshRenderer>().sharedMaterial = assets.BarrierMat;
            }
        }

        private static void BuildBreakableOverpassBridge(Transform parent, string name, Vector3 centerPos, float widthX, NeonCityAssetPack assets)
        {
            GameObject bridge = new GameObject(name);
            bridge.transform.SetParent(parent, false);
            bridge.transform.position = centerPos;

            // Flanking concrete abutment piers outside the main road
            for (int side = -1; side <= 1; side += 2)
            {
                CreateSubBox(bridge.transform, $"Abutment_{(side < 0 ? "L" : "R")}",
                    new Vector3(side * (widthX * 0.5f + 1.8f), -2.4f, 0f), new Vector3(3.2f, 5.4f, 7.2f), assets.BarrierMat);
            }

            // 6 shatterable deck span modules with Trigger colliders (Titans pass straight through cleanly!)
            const int segCount = 6;
            float segW = widthX / segCount;
            for (int i = 0; i < segCount; i++)
            {
                float lx = -widthX * 0.5f + segW * (i + 0.5f);
                GameObject seg = new GameObject($"Overpass_BreakableSeg_{i}");
                seg.transform.SetParent(bridge.transform, false);
                seg.transform.localPosition = new Vector3(lx, 0f, 0f);

                var col = seg.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(segW - 0.1f, 2.2f, 6.2f);

                var prop = seg.AddComponent<UrbanCollateralProp>();
                prop.mode = UrbanCollateralProp.CollateralMode.ShatterOnTitanPass;
                prop.launchMass = 22f;
                prop.knockImpulse = 26f;

                CreateSubBox(seg.transform, "DeckSlab", new Vector3(0f, 0f, 0f), new Vector3(segW - 0.15f, 1.05f, 6.0f), assets.BarrierMat);
                CreateSubBox(seg.transform, "Rail_S", new Vector3(0f, 0.85f, -2.8f), new Vector3(segW - 0.15f, 0.75f, 0.35f), assets.DarkConcreteMat);
                CreateSubBox(seg.transform, "Rail_N", new Vector3(0f, 0.85f, 2.8f), new Vector3(segW - 0.15f, 0.75f, 0.35f), assets.DarkConcreteMat);
                CreateSubBox(seg.transform, "HazardStrip", new Vector3(0f, 0.12f, -3.05f), new Vector3(segW * 0.8f, 0.16f, 0.14f), assets.NeonTubeMats[2]);
            }
        }

        internal static GameObject SpawnHKDoubleDeckerTram(Transform parent, string name, Vector3 pos, float yaw, NeonCityAssetPack assets)
        {
            GameObject tram = new GameObject(name);
            tram.transform.SetParent(parent, false);
            tram.transform.position = pos;
            tram.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var col = tram.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 1.95f, 0f);
            col.size = new Vector3(2.3f, 3.9f, 8.6f);

            var prop = tram.AddComponent<UrbanCollateralProp>();
            prop.mode = UrbanCollateralProp.CollateralMode.KnockableVehicle;
            prop.launchMass = 14f;
            prop.knockImpulse = 36f;

            CreateSubBox(tram.transform, "Body_Green", new Vector3(0f, 1.85f, 0f), new Vector3(2.15f, 3.55f, 8.4f), assets.TramGreenMat);
            CreateSubBox(tram.transform, "Cream_WaistBand", new Vector3(0f, 2.02f, 0f), new Vector3(2.22f, 0.34f, 8.45f), assets.TongLauConcreteMat);
            CreateSubBox(tram.transform, "Cream_Roof", new Vector3(0f, 3.72f, 0f), new Vector3(2.05f, 0.25f, 8.2f), assets.TongLauConcreteMat);
            CreateSubBox(tram.transform, "Upper_Windows", new Vector3(0f, 2.82f, 0f), new Vector3(2.20f, 0.78f, 7.6f), assets.WindowMats[4]);
            CreateSubBox(tram.transform, "Lower_Windows", new Vector3(0f, 1.22f, 0f), new Vector3(2.20f, 0.75f, 7.6f), assets.WindowMats[1]);
            CreateSubBox(tram.transform, "Front_RouteBoard", new Vector3(0f, 3.25f, 4.24f), new Vector3(1.1f, 0.36f, 0.12f), assets.NeonTubeMats[2]);
            CreateSubBox(tram.transform, "Headlight", new Vector3(0f, 0.72f, 4.24f), new Vector3(0.75f, 0.28f, 0.12f), assets.HeadLightMat);
            CreateSubBox(tram.transform, "Taillight", new Vector3(0f, 0.72f, -4.24f), new Vector3(0.75f, 0.22f, 0.12f), assets.TailLightMat);
            return tram;
        }

        internal static GameObject SpawnFuelTanker(Transform parent, string name, Vector3 pos, float yaw, NeonCityAssetPack assets)
        {
            GameObject tanker = new GameObject(name);
            tanker.transform.SetParent(parent, false);
            tanker.transform.position = pos;
            tanker.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var col = tanker.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 1.5f, 0f);
            col.size = new Vector3(2.6f, 3.0f, 10.6f);

            var prop = tanker.AddComponent<UrbanCollateralProp>();
            prop.mode = UrbanCollateralProp.CollateralMode.KnockableVehicle;
            prop.isFuelTanker = true;
            prop.launchMass = 16f;
            prop.knockImpulse = 38f;
            prop.upwardPop = 13f;

            CreateSubBox(tanker.transform, "Chassis", new Vector3(0f, 0.45f, 0f), new Vector3(2.25f, 0.55f, 10.2f), assets.DarkSteelMat);
            CreateSubBox(tanker.transform, "Cab", new Vector3(0f, 1.45f, 3.65f), new Vector3(2.35f, 2.05f, 2.4f), assets.TankerHazardMat);
            GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "Fuel_Tank_Cylinder";
            cyl.transform.SetParent(tanker.transform, false);
            cyl.transform.localPosition = new Vector3(0f, 1.72f, -1.2f);
            cyl.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cyl.transform.localScale = new Vector3(2.25f, 3.4f, 2.25f);
            UnityEngine.Object.DestroyImmediate(cyl.GetComponent<Collider>());
            cyl.GetComponent<MeshRenderer>().sharedMaterial = assets.BarrierMat;
            CreateSubBox(tanker.transform, "HazardBand", new Vector3(0f, 1.72f, -1.2f), new Vector3(2.32f, 0.35f, 5.8f), assets.NeonTubeMats[2]);
            CreateSubBox(tanker.transform, "Headlights", new Vector3(0f, 0.85f, 4.90f), new Vector3(1.8f, 0.24f, 0.1f), assets.HeadLightMat);
            return tanker;
        }

        internal static GameObject SpawnCollateralCar(
            Transform parent, GameObject fbx, string carName, Vector3 pos, float yaw,
            Material bodyMat, Material tailMat, Material glassMat = null, Material wheelMat = null)
        {
            Transform src = fbx.transform.Find(carName);
            if (src == null) return null;

            if (glassMat == null)
                glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/NeonCity/NeonCity_DarkGlass.mat");
            if (wheelMat == null)
                wheelMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/NeonCity/NeonCity_DarkSteel.mat");

            GameObject wrapper = new GameObject($"Collateral_{carName}");
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.position = pos;
            wrapper.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var col = wrapper.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.95f, 0f);
            col.size = new Vector3(2.4f, 1.9f, 5.2f);

            var prop = wrapper.AddComponent<UrbanCollateralProp>();
            prop.mode = UrbanCollateralProp.CollateralMode.KnockableVehicle;
            prop.launchMass = 8f;
            prop.knockImpulse = 40f;

            const float carScale = 0.125f;
            GameObject meshChild = new GameObject("CarMesh");
            meshChild.transform.SetParent(wrapper.transform, false);
            meshChild.transform.localPosition = Vector3.zero;
            meshChild.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            meshChild.transform.localScale = Vector3.one * carScale;

            var mf = meshChild.AddComponent<MeshFilter>();
            var srcMf = src.GetComponent<MeshFilter>();
            if (srcMf != null) mf.sharedMesh = srcMf.sharedMesh;

            var mr = meshChild.AddComponent<MeshRenderer>();
            var srcMr = src.GetComponent<MeshRenderer>();
            int matLen = srcMr != null ? srcMr.sharedMaterials.Length : 1;
            Material[] mats = new Material[matLen];
            for (int m = 0; m < matLen; m++)
            {
                string slotName = (srcMr != null && srcMr.sharedMaterials[m] != null)
                    ? srcMr.sharedMaterials[m].name.ToLowerInvariant()
                    : "";
                if (m == 1 || slotName.Contains("glass"))
                    mats[m] = glassMat != null ? glassMat : bodyMat;
                else if (m == 2 || slotName.Contains("optic"))
                    mats[m] = tailMat != null ? tailMat : bodyMat;
                else
                    mats[m] = bodyMat;
            }
            mr.sharedMaterials = mats;

            // Attach the 4 matching 3D Wheel_* meshes from Buildings.fbx in src's local mesh space
            Quaternion invSrcRot = Quaternion.Inverse(src.localRotation);
            float invSrcScale = 1f / Mathf.Max(0.001f, src.localScale.x);
            int wheelCount = 0;
            foreach (Transform child in fbx.transform)
            {
                if (!child.name.StartsWith("Wheel")) continue;
                float distInFbx = Vector3.Distance(child.localPosition, src.localPosition);
                if (distInFbx > 6.5f) continue;
                if (Quaternion.Angle(child.localRotation, src.localRotation) > 8f) continue;

                var wSrcMf = child.GetComponent<MeshFilter>();
                if (wSrcMf == null || wSrcMf.sharedMesh == null) continue;

                GameObject wGo = new GameObject($"Wheel_{wheelCount++}");
                wGo.transform.SetParent(meshChild.transform, false);
                wGo.transform.localPosition = (invSrcRot * (child.localPosition - src.localPosition)) * invSrcScale;
                wGo.transform.localRotation = invSrcRot * child.localRotation;
                wGo.transform.localScale = Vector3.one;

                wGo.AddComponent<MeshFilter>().sharedMesh = wSrcMf.sharedMesh;
                var wMr = wGo.AddComponent<MeshRenderer>();
                int wSubCount = child.GetComponent<MeshRenderer>() != null
                    ? child.GetComponent<MeshRenderer>().sharedMaterials.Length
                    : 1;
                Material[] wMats = new Material[wSubCount];
                for (int wm = 0; wm < wSubCount; wm++) wMats[wm] = wheelMat != null ? wheelMat : bodyMat;
                wMr.sharedMaterials = wMats;
            }

            // Ground the bottom of the tires/chassis flush onto pos.y
            MeshRenderer[] allRenderers = wrapper.GetComponentsInChildren<MeshRenderer>();
            if (allRenderers.Length > 0)
            {
                Bounds b = allRenderers[0].bounds;
                for (int i = 1; i < allRenderers.Length; i++) b.Encapsulate(allRenderers[i].bounds);
                float liftY = pos.y - b.min.y;
                meshChild.transform.position += new Vector3(0f, liftY, 0f);
            }

            return wrapper;
        }

        internal static GameObject BuildExpresswayGantry(Transform parent, string name, float gz, float yaw, NeonCityAssetPack assets, float baseY = CityDeckY)
        {
            GameObject gantry = new GameObject(name);
            gantry.transform.SetParent(parent, false);
            gantry.transform.position = new Vector3(0f, baseY, gz);
            gantry.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            for (int side = -1; side <= 1; side += 2)
                CreateSubBox(gantry.transform, $"Pillar_{(side < 0 ? "L" : "R")}", new Vector3(side * 34.8f, 10.2f, 0f), new Vector3(1.5f, 20.4f, 1.5f), assets.DarkSteelMat);

            CreateSubBox(gantry.transform, "Overhead_Truss_Top", new Vector3(0f, 20.2f, 0f), new Vector3(71.2f, 0.8f, 1.8f), assets.DarkSteelMat);
            CreateSubBox(gantry.transform, "Overhead_Truss_Bot", new Vector3(0f, 18.0f, 0f), new Vector3(71.2f, 0.8f, 1.8f), assets.DarkSteelMat);
            CreateNeonBillboard(gantry.transform, "Gantry_Sign_Left", new Vector3(-13.5f, 19.1f, 1.05f), new Vector3(18f, 4.4f, 0.35f), assets.GantrySignMat, assets.DarkSteelMat, doubleSided: false);
            CreateNeonBillboard(gantry.transform, "Gantry_Sign_Right", new Vector3(13.5f, 19.1f, 1.05f), new Vector3(18f, 4.4f, 0.35f), assets.SignMats[0], assets.DarkSteelMat, doubleSided: false);

            CreateSubBox(gantry.transform, "Roadblock_EndWall", new Vector3(0f, 0.65f, 1.5f), new Vector3(HighwayAsphaltWidth, 1.3f, 1.4f), assets.BarrierMat);
            CreateSubBox(gantry.transform, "Roadblock_EndBeacon", new Vector3(0f, 1.42f, 1.5f), new Vector3(HighwayAsphaltWidth - 2f, 0.22f, 0.45f), assets.NeonTubeMats[2]);
            return gantry;
        }

        #endregion

        #region Tier 2 Neon Canyon & Tier 3 Supertall Peaks

        private struct CanyonSlot
        {
            public float ZCenter;
            public float WidthZ;
            public float DepthX;
            public float Height;
            public float CurbSetback;
            public int Kind;      // 0:TongLau+AC+Bamboo, 1:ThinPencil, 2:CurtainGlass, 3:TwinSlit, 4:SteppedSetback
            public bool HasBamboo;
            public bool HasHelipad;
            public int StyleIdx;
            public int NeonIdx;
        }

        private static void BuildTier2And3CitySkyline(Transform parent, NeonCityAssetPack assets)
        {
            GameObject buildingsRoot = new GameObject("City_HongKong_Skyline");
            buildingsRoot.transform.SetParent(parent, false);

            // 1. FRONT CANYON WALLS — Thin skyscrapers & tong lau clusters of dramatically varied widths (8.5-21m) & heights (11.5-54m)!
            //    South of Z=+18 is Harbour on West side; Z=+18..+64 has East Waterfront + Lower East Tong Lau;
            //    Z >= +74 closes BOTH West & East walls into tight HK street canyon with 2-4m dark alleys & cross-streets (Z=+58, Z=+154).
            CanyonSlot[] eastFrontSlots =
            {
                // Harbour Waterfront Row (Z = -135 .. +39)
                new CanyonSlot { ZCenter = -128f, WidthZ = 16f, DepthX = 18f, Height = 38f, CurbSetback = 5.5f, Kind = 2, StyleIdx = 5, NeonIdx = 0 },
                new CanyonSlot { ZCenter = -102f, WidthZ = 12f, DepthX = 14f, Height = 16f, CurbSetback = 7.0f, Kind = 0, HasBamboo = true, StyleIdx = 4, NeonIdx = 5 },
                new CanyonSlot { ZCenter = -66f,  WidthZ = 18f, DepthX = 20f, Height = 44f, CurbSetback = 6.5f, Kind = 1, HasHelipad = true, StyleIdx = 0, NeonIdx = 1 },
                new CanyonSlot { ZCenter = -30f,  WidthZ = 11f, DepthX = 15f, Height = 26f, CurbSetback = 5.0f, Kind = 0, StyleIdx = 4, NeonIdx = 2 },
                new CanyonSlot { ZCenter = -8f,   WidthZ = 15f, DepthX = 18f, Height = 52f, CurbSetback = 3.5f, Kind = 3, StyleIdx = 2, NeonIdx = 0 },
                new CanyonSlot { ZCenter = 12f,   WidthZ = 11f, DepthX = 13f, Height = 15f, CurbSetback = 2.0f, Kind = 0, HasBamboo = true, StyleIdx = 4, NeonIdx = 5 },
                new CanyonSlot { ZCenter = 28f,   WidthZ = 13f, DepthX = 16f, Height = 34f, CurbSetback = 1.0f, Kind = 1, StyleIdx = 1, NeonIdx = 4 },

                // Mid Block (Z = +76 .. +137)
                new CanyonSlot { ZCenter = 81f,   WidthZ = 11f, DepthX = 14f, Height = 14f, CurbSetback = 0.8f, Kind = 0, StyleIdx = 4, NeonIdx = 5 },
                new CanyonSlot { ZCenter = 95f,   WidthZ = 11f, DepthX = 14f, Height = 46f, CurbSetback = 2.2f, Kind = 1, StyleIdx = 0, NeonIdx = 0 },
                new CanyonSlot { ZCenter = 110f,  WidthZ = 14f, DepthX = 18f, Height = 28f, CurbSetback = 0.5f, Kind = 4, HasHelipad = true, StyleIdx = 2, NeonIdx = 1 },
                new CanyonSlot { ZCenter = 127f,  WidthZ = 12f, DepthX = 14f, Height = 17f, CurbSetback = 1.2f, Kind = 0, HasBamboo = true, StyleIdx = 4, NeonIdx = 2 },

                // North Block (Z = +172 .. +280)
                new CanyonSlot { ZCenter = 177f,  WidthZ = 12f, DepthX = 15f, Height = 39f, CurbSetback = 1.0f, Kind = 1, StyleIdx = 3, NeonIdx = 4 },
                new CanyonSlot { ZCenter = 193f,  WidthZ = 15f, DepthX = 19f, Height = 54f, CurbSetback = 2.4f, Kind = 3, HasHelipad = true, StyleIdx = 5, NeonIdx = 0 },
                new CanyonSlot { ZCenter = 211f,  WidthZ = 10f, DepthX = 13f, Height = 14f, CurbSetback = 0.6f, Kind = 0, HasBamboo = true, StyleIdx = 4, NeonIdx = 5 },
                new CanyonSlot { ZCenter = 225f,  WidthZ = 13f, DepthX = 16f, Height = 42f, CurbSetback = 1.6f, Kind = 2, StyleIdx = 1, NeonIdx = 2 },
                new CanyonSlot { ZCenter = 244f,  WidthZ = 17f, DepthX = 20f, Height = 32f, CurbSetback = 2.0f, Kind = 4, StyleIdx = 0, NeonIdx = 1 },
                new CanyonSlot { ZCenter = 266f,  WidthZ = 14f, DepthX = 18f, Height = 48f, CurbSetback = 1.5f, Kind = 1, StyleIdx = 2, NeonIdx = 0 },
            };

            CanyonSlot[] westFrontSlots =
            {
                // West Side starts at Z = +76 after Victoria Harbour Promenade -> immediate dense canyon closure!
                new CanyonSlot { ZCenter = 76f,   WidthZ = 12f, DepthX = 14f, Height = 15f, CurbSetback = 1.2f, Kind = 0, HasBamboo = true, StyleIdx = 4, NeonIdx = 5 },
                new CanyonSlot { ZCenter = 91f,   WidthZ = 12f, DepthX = 16f, Height = 48f, CurbSetback = 0.8f, Kind = 1, StyleIdx = 2, NeonIdx = 1 },
                new CanyonSlot { ZCenter = 107f,  WidthZ = 14f, DepthX = 17f, Height = 29f, CurbSetback = 2.2f, Kind = 3, StyleIdx = 0, NeonIdx = 0 },
                new CanyonSlot { ZCenter = 124f,  WidthZ = 11f, DepthX = 13f, Height = 13f, CurbSetback = 0.6f, Kind = 0, StyleIdx = 4, NeonIdx = 2 },
                new CanyonSlot { ZCenter = 136f,  WidthZ = 10f, DepthX = 15f, Height = 38f, CurbSetback = 1.8f, Kind = 1, StyleIdx = 3, NeonIdx = 4 },

                // North Block West (Z = +172 .. +280)
                new CanyonSlot { ZCenter = 176f,  WidthZ = 11f, DepthX = 14f, Height = 16f, CurbSetback = 0.9f, Kind = 0, HasBamboo = true, StyleIdx = 4, NeonIdx = 5 },
                new CanyonSlot { ZCenter = 191f,  WidthZ = 14f, DepthX = 18f, Height = 50f, CurbSetback = 2.0f, Kind = 2, HasHelipad = true, StyleIdx = 5, NeonIdx = 0 },
                new CanyonSlot { ZCenter = 209f,  WidthZ = 11f, DepthX = 14f, Height = 33f, CurbSetback = 1.0f, Kind = 1, StyleIdx = 1, NeonIdx = 2 },
                new CanyonSlot { ZCenter = 224f,  WidthZ = 13f, DepthX = 17f, Height = 44f, CurbSetback = 2.2f, Kind = 4, StyleIdx = 0, NeonIdx = 1 },
                new CanyonSlot { ZCenter = 242f,  WidthZ = 12f, DepthX = 15f, Height = 18f, CurbSetback = 0.8f, Kind = 0, StyleIdx = 4, NeonIdx = 4 },
                new CanyonSlot { ZCenter = 262f,  WidthZ = 16f, DepthX = 20f, Height = 52f, CurbSetback = 1.5f, Kind = 3, StyleIdx = 2, NeonIdx = 5 },
            };

            for (int i = 0; i < eastFrontSlots.Length; i++)
            {
                var s = eastFrontSlots[i];
                float xPos = CanyonHalfWidth + s.CurbSetback + s.DepthX * 0.5f;
                SpawnFrontCanyonTower(buildingsRoot.transform, $"Building_Front_E_{i}",
                    new Vector3(xPos, CityDeckY, s.ZCenter), 270f, s, assets);
            }
            for (int i = 0; i < westFrontSlots.Length; i++)
            {
                var s = westFrontSlots[i];
                float xPos = -(CanyonHalfWidth + s.CurbSetback + s.DepthX * 0.5f);
                SpawnFrontCanyonTower(buildingsRoot.transform, $"Building_Front_W_{i}",
                    new Vector3(xPos, CityDeckY, s.ZCenter), 90f, s, assets);
            }

            // 2. TIER 3 SUPERTALL PEAKS (120m - 160m above CityDeck = ~5x-6.5x Gipsy height!)
            //    A) Sharp Triangular Glass Prisms w/ White LED Geometric X-Bracing (Bank of China silhouette!)
            SpawnTriangularPrismSupertall(buildingsRoot.transform, "Supertall_TriPrism_HarbourEast",
                new Vector3(78f, CityDeckY, 6f), -25f, 34f, 142f, 5, 0, assets);
            SpawnTriangularPrismSupertall(buildingsRoot.transform, "Supertall_TriPrism_WestCanyon",
                new Vector3(-84f, CityDeckY, 122f), 115f, 36f, 152f, 5, 1, assets);
            SpawnTriangularPrismSupertall(buildingsRoot.transform, "Supertall_TriPrism_NorthVista",
                new Vector3(30f, CityDeckY, 286f), -140f, 38f, 160f, 5, 5, assets);

            //    B) Tiered Neo-Futuristic Spires w/ Glowing Neon Rings (Central Plaza / Two IFC silhouette!)
            SpawnTieredNeoFuturisticSpire(buildingsRoot.transform, "Supertall_NeoSpire_EastCanyon",
                new Vector3(74f, CityDeckY, 106f), 28f, 134f, 0, 0, assets);
            SpawnTieredNeoFuturisticSpire(buildingsRoot.transform, "Supertall_NeoSpire_WestNorth",
                new Vector3(-82f, CityDeckY, 206f), 30f, 144f, 2, 1, assets);
            SpawnTieredNeoFuturisticSpire(buildingsRoot.transform, "Supertall_NeoSpire_NorthVistaL",
                new Vector3(-28f, CityDeckY, 296f), 30f, 148f, 5, 5, assets);

            //    C) Curved Waterfront Skyscrapers overlooking Victoria Harbour!
            SpawnCurvedWaterfrontTower(buildingsRoot.transform, "Supertall_CurvedWaterfront_1",
                new Vector3(68f, CityDeckY, -44f), -78f, 34f, 22f, 92f, 0, 1, assets);
            SpawnCurvedWaterfrontTower(buildingsRoot.transform, "Supertall_CurvedWaterfront_2",
                new Vector3(76f, CityDeckY, -124f), -65f, 38f, 24f, 114f, 5, 0, assets);

            // 3. DENSE 2ND & 3RD ROW THIN HIGH-RISES & PENCIL CLUSTERS (52m - 108m above deck) + Weathered Buildings.fbx Variants!
            GameObject buildingsFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Buildings.fbx");
            float[] backEastZs = { -145f, -95f, -55f, -18f, 32f, 74f, 114f, 154f, 188f, 226f, 266f };
            float[] backWestZs = { 82f, 108f, 144f, 182f, 218f, 256f, 290f };

            for (int row = 0; row < 2; row++)
            {
                float eastX = 68f + row * 46f;
                for (int i = 0; i < backEastZs.Length; i++)
                {
                    int seed = row * 41 + i * 13 + 7;
                    float h = Mathf.Lerp(54f, 106f, (seed % 11) / 10f);
                    float w = Mathf.Lerp(14f, 26f, (seed % 7) / 6f);
                    float d = Mathf.Lerp(14f, 24f, (seed % 5) / 4f);
                    SpawnTieredSkyscraper(buildingsRoot.transform, $"Backdrop_E_R{row}_{i}",
                        new Vector3(eastX + (seed % 9) - 4f, CityDeckY, backEastZs[i] + (row * 14f)),
                        270f, w, d, h, seed % 4, seed % assets.WindowMats.Length, seed % assets.NeonTubeMats.Length,
                        addCollider: false, assets);
                }

                float westX = -(68f + row * 46f);
                for (int i = 0; i < backWestZs.Length; i++)
                {
                    int seed = row * 37 + i * 17 + 19;
                    float h = Mathf.Lerp(56f, 110f, (seed % 11) / 10f);
                    float w = Mathf.Lerp(13f, 25f, (seed % 7) / 6f);
                    float d = Mathf.Lerp(13f, 23f, (seed % 5) / 4f);
                    SpawnTieredSkyscraper(buildingsRoot.transform, $"Backdrop_W_R{row}_{i}",
                        new Vector3(westX - (seed % 9) + 4f, CityDeckY, backWestZs[i] + (row * 12f)),
                        90f, w, d, h, seed % 4, seed % assets.WindowMats.Length, seed % assets.NeonTubeMats.Length,
                        addCollider: false, assets);
                }
            }

            // Cross-street canyon depth towers & weathered FBX high-rises framing intersections at Z=+58 and Z=+154
            if (buildingsFbx != null)
            {
                string[] fbxPool = { "Bldg8", "Bldg5", "Bldg9", "Bldg12", "Bldg4" };
                for (int i = 0; i < 6; i++)
                {
                    float zPos = (i < 3 ? 58f : 154f) + ((i % 2 == 0) ? -22f : 22f);
                    float xPos = (i % 2 == 0 ? 1f : -1f) * (92f + (i % 3) * 34f);
                    if (zPos < 65f && xPos < 0f) continue; // Keep West harbour side open
                    SpawnScaledFbxTongLauCluster(buildingsRoot.transform, buildingsFbx, fbxPool[i % fbxPool.Length],
                        $"FBX_Cluster_{i}", new Vector3(xPos, CityDeckY, zPos), (i * 90f) % 360f, 0.24f + (i % 3) * 0.08f, assets);
                }
            }
        }

        private static void SpawnFrontCanyonTower(Transform parent, string name, Vector3 basePos, float yawDeg, CanyonSlot s, NeonCityAssetPack assets)
        {
            if (s.Kind == 0)
            {
                SpawnTongLauBlock(parent, name, basePos, yawDeg, s.WidthZ, s.DepthX, s.Height, s.HasBamboo, s.NeonIdx, assets);
                return;
            }
            SpawnTieredSkyscraper(parent, name, basePos, yawDeg, s.WidthZ, s.DepthX, s.Height, s.Kind, s.StyleIdx, s.NeonIdx, addCollider: true, assets, s.HasHelipad, s.HasBamboo);
        }

        /// <summary>
        /// Builds a dense older 12-story Hong Kong apartment tenement (Tong Lau) with weathered concrete,
        /// roller-shuttered shops, exterior 3D AC unit grids, rooftop water tanks, and optional bamboo scaffolding.
        /// </summary>
        internal static GameObject SpawnTongLauBlock(Transform parent, string name, Vector3 basePos, float yawDeg, float w, float d, float h, bool hasBamboo, int neonIdx, NeonCityAssetPack assets)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = basePos;
            go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, h * 0.5f, 0f);
            box.size = new Vector3(w, h, d);

            Material neonMat = assets.NeonTubeMats[neonIdx % assets.NeonTubeMats.Length];

            // Concrete podium shopfront + weathered apartment body
            CreateSubBox(go.transform, "Shop_Ground", new Vector3(0f, 1.8f, 0f), new Vector3(w, 3.6f, d), assets.DarkConcreteMat);
            CreateSubBox(go.transform, "Shop_NeonAwning", new Vector3(0f, 3.7f, d * 0.5f + 0.35f), new Vector3(w * 0.92f, 0.32f, 0.9f), neonMat);
            CreateSubBox(go.transform, "TongLau_Body", new Vector3(0f, 3.6f + (h - 3.6f) * 0.5f, 0f), new Vector3(w * 0.96f, h - 3.6f, d * 0.96f), assets.TongLauConcreteMat);
            CreateSubBox(go.transform, "Parapet_Cap", new Vector3(0f, h - 0.15f, 0f), new Vector3(w * 0.98f, 0.35f, d * 0.98f), assets.DarkConcreteMat);

            // Exterior AC Unit Grids bolted across the street-facing concrete facade
            int acCols = Mathf.Clamp(Mathf.FloorToInt(w / 2.6f), 2, 5);
            int acRows = Mathf.Clamp(Mathf.FloorToInt((h - 4.5f) / 2.4f), 2, 5);
            for (int r = 0; r < acRows; r++)
            {
                float ay = 4.8f + r * 2.3f;
                for (int c = 0; c < acCols; c++)
                {
                    if ((r + c) % 4 == 3) continue; // Organic irregularity
                    float ax = ((float)c / Mathf.Max(1, acCols - 1) - 0.5f) * (w * 0.72f);
                    CreateSubBox(go.transform, $"AC_Unit_{r}_{c}",
                        new Vector3(ax, ay, d * 0.48f + 0.32f), new Vector3(0.95f, 0.65f, 0.58f), assets.ACUnitMat);
                }
            }

            // Rooftop Water Tanks & cage ladder at Y = h
            for (int t = 0; t < 2; t++)
            {
                GameObject tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tank.name = $"Rooftop_WaterTank_{t}";
                tank.transform.SetParent(go.transform, false);
                tank.transform.localPosition = new Vector3((t == 0 ? -0.22f : 0.24f) * w, h + 1.05f, (t == 0 ? 0.15f : -0.18f) * d);
                tank.transform.localScale = new Vector3(1.9f, 1.05f, 1.9f);
                UnityEngine.Object.DestroyImmediate(tank.GetComponent<Collider>());
                tank.GetComponent<MeshRenderer>().sharedMaterial = assets.BarrierMat;
            }
            CreateSubBox(go.transform, "Rooftop_Shed", new Vector3(0f, h + 1.1f, -d * 0.18f), new Vector3(w * 0.34f, 2.2f, d * 0.30f), assets.DarkSteelMat);

            // Bamboo Construction Scaffolding wrapping the front/corner facade!
            if (hasBamboo)
            {
                BuildBambooScaffoldingLattice(go.transform, w * 0.92f, h * 0.85f, d * 0.48f + 0.65f, assets);
            }
            return go;
        }

        internal static GameObject BuildBambooScaffoldingLattice(Transform parent, float spanW, float height, float localZ, NeonCityAssetPack assets)
        {
            GameObject scaf = new GameObject("Bamboo_Scaffolding");
            scaf.transform.SetParent(parent, false);
            scaf.transform.localPosition = new Vector3(0f, 3.6f, localZ);

            // Vertical bamboo poles
            int poles = Mathf.Clamp(Mathf.FloorToInt(spanW / 1.6f), 5, 9);
            for (int i = 0; i < poles; i++)
            {
                float px = ((float)i / (poles - 1) - 0.5f) * spanW;
                CreateSubBox(scaf.transform, $"Bamboo_V_{i}", new Vector3(px, height * 0.5f, 0.30f), new Vector3(0.22f, height, 0.22f), assets.BambooMat);
            }

            // Horizontal ledger bamboo poles + diagonal bracings
            int tiers = Mathf.Clamp(Mathf.FloorToInt(height / 2.0f), 4, 9);
            for (int r = 0; r < tiers; r++)
            {
                float ry = (r + 0.5f) * (height / tiers);
                CreateSubBox(scaf.transform, $"Bamboo_H_{r}", new Vector3(0f, ry, 0.42f), new Vector3(spanW + 0.5f, 0.19f, 0.19f), assets.BambooMat);
            }

            // Iconic Hong Kong green safety netting sheet draped behind poles
            CreateSubBox(scaf.transform, "Safety_Netting", new Vector3(0f, height * 0.48f, 0.12f), new Vector3(spanW * 0.92f, height * 0.84f, 0.06f), assets.GreenNettingMat);
            return scaf;
        }

        internal static GameObject SpawnTieredSkyscraper(
            Transform parent, string name, Vector3 basePos, float yawDeg,
            float w, float d, float totalH, int kind, int winIdx, int neonIdx,
            bool addCollider, NeonCityAssetPack assets, bool hasHelipad = false, bool hasBamboo = false)
        {
            GameObject bldg = new GameObject(name);
            bldg.transform.SetParent(parent, false);
            bldg.transform.position = basePos;
            bldg.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            if (addCollider)
            {
                var box = bldg.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, totalH * 0.5f, 0f);
                box.size = new Vector3(w, totalH, d);
            }

            Material winMat = assets.WindowMats[winIdx % assets.WindowMats.Length];
            Material neonMat = assets.NeonTubeMats[neonIdx % assets.NeonTubeMats.Length];
            Material secNeon = assets.NeonTubeMats[(neonIdx + 2) % assets.NeonTubeMats.Length];

            float podiumH = Mathf.Min(6.0f, totalH * 0.18f);
            float crownH = Mathf.Min(4.2f, totalH * 0.10f);
            float shaftH = Mathf.Max(6.0f, totalH - podiumH - crownH);

            CreateSubBox(bldg.transform, "Podium", new Vector3(0f, podiumH * 0.5f, 0f), new Vector3(w, podiumH, d), assets.DarkConcreteMat);
            CreateSubBox(bldg.transform, "Neon_PodiumCornice", new Vector3(0f, podiumH - 0.15f, 0f), new Vector3(w + 0.3f, 0.24f, d + 0.3f), neonMat);

            float sW = w * 0.90f;
            float sD = d * 0.90f;
            float sY = podiumH + shaftH * 0.5f;

            if (kind == 3)
            {
                float wingW = sW * 0.42f;
                float off = sW * 0.28f;
                CreateSubBox(bldg.transform, "Shaft_L", new Vector3(-off, sY, 0f), new Vector3(wingW, shaftH, sD), winMat);
                CreateSubBox(bldg.transform, "Shaft_R", new Vector3(off, sY, 0f), new Vector3(wingW, shaftH, sD), winMat);
                CreateSubBox(bldg.transform, "Core_Bridge", new Vector3(0f, sY, -sD * 0.12f), new Vector3(sW * 0.30f, shaftH * 0.90f, sD * 0.62f), assets.DarkSteelMat);
                CreateSubBox(bldg.transform, "Neon_CoreSpineL", new Vector3(-off + wingW * 0.5f, sY, sD * 0.5f + 0.1f), new Vector3(0.28f, shaftH, 0.22f), secNeon);
                CreateSubBox(bldg.transform, "Neon_CoreSpineR", new Vector3(off - wingW * 0.5f, sY, sD * 0.5f + 0.1f), new Vector3(0.28f, shaftH, 0.22f), secNeon);
            }
            else if (kind == 4)
            {
                // Stepped Setback Tower (lower broad tier + upper slender stepped tower)
                float hLower = shaftH * 0.56f;
                float hUpper = shaftH * 0.44f;
                CreateSubBox(bldg.transform, "Shaft_Lower", new Vector3(0f, podiumH + hLower * 0.5f, 0f), new Vector3(sW, hLower, sD), winMat);
                CreateSubBox(bldg.transform, "Setback_Collar", new Vector3(0f, podiumH + hLower, 0f), new Vector3(sW + 0.35f, 0.35f, sD + 0.35f), secNeon);
                sW *= 0.72f;
                sD *= 0.74f;
                CreateSubBox(bldg.transform, "Shaft_Upper", new Vector3(0f, podiumH + hLower + hUpper * 0.5f, 0f), new Vector3(sW, hUpper, sD), winMat);
            }
            else
            {
                CreateSubBox(bldg.transform, "WindowShaft", new Vector3(0f, sY, 0f), new Vector3(sW, shaftH, sD), winMat);
                if (kind == 1)
                {
                    // Thin Hong Kong Pencil Tower ribs & street-facing AC bay recesses
                    CreateSubBox(bldg.transform, "Pencil_SpineRib", new Vector3(0f, sY, sD * 0.5f + 0.14f), new Vector3(0.65f, shaftH, 0.35f), assets.DarkSteelMat);
                    CreateSubBox(bldg.transform, "Pencil_NeonLine", new Vector3(0f, sY, sD * 0.5f + 0.34f), new Vector3(0.22f, shaftH * 0.96f, 0.16f), secNeon);
                }
            }

            // Corner vertical neon edge tubes
            float cornerZ = sD * 0.5f + 0.12f;
            float cornerTopY = podiumH + shaftH;
            CreateSubBox(bldg.transform, "Neon_EdgeL", new Vector3(-sW * 0.5f, podiumH + shaftH * 0.5f, cornerZ), new Vector3(0.28f, shaftH, 0.24f), neonMat);
            CreateSubBox(bldg.transform, "Neon_EdgeR", new Vector3(sW * 0.5f, podiumH + shaftH * 0.5f, cornerZ), new Vector3(0.28f, shaftH, 0.24f), neonMat);

            // Dark rooftop deck + thin perimeter rim neon + rooftop details (Helipad OR Water Tanks + Comms Mast!)
            float crownBase = cornerTopY;
            CreateSubBox(bldg.transform, "Crown_Parapet", new Vector3(0f, crownBase + crownH * 0.4f, 0f), new Vector3(sW * 0.96f, crownH * 0.8f, sD * 0.96f), assets.DarkSteelMat);
            float rimY = crownBase + crownH * 0.82f;
            float rW = sW * 0.96f;
            float rD = sD * 0.96f;
            CreateSubBox(bldg.transform, "RoofDeck", new Vector3(0f, rimY, 0f), new Vector3(rW * 0.94f, 0.2f, rD * 0.94f), assets.DarkConcreteMat);
            CreateSubBox(bldg.transform, "Rim_F", new Vector3(0f, rimY, rD * 0.5f), new Vector3(rW + 0.3f, 0.24f, 0.28f), neonMat);
            CreateSubBox(bldg.transform, "Rim_B", new Vector3(0f, rimY, -rD * 0.5f), new Vector3(rW + 0.3f, 0.24f, 0.28f), neonMat);
            CreateSubBox(bldg.transform, "Rim_L", new Vector3(-rW * 0.5f, rimY, 0f), new Vector3(0.28f, 0.24f, rD + 0.3f), neonMat);
            CreateSubBox(bldg.transform, "Rim_R", new Vector3(rW * 0.5f, rimY, 0f), new Vector3(0.28f, 0.24f, rD + 0.3f), neonMat);

            if (hasHelipad)
            {
                float padSz = Mathf.Min(rW, rD) * 0.82f;
                GameObject pad = CreateRoadQuad(bldg.transform, "Rooftop_Helipad",
                    new Vector3(0f, rimY + 0.14f, 0f), new Vector3(padSz, padSz, 1f), assets.HelipadMat);
                pad.tag = "Untagged";
            }
            else
            {
                CreateSubBox(bldg.transform, "Rooftop_Penthouse", new Vector3(-sW * 0.14f, rimY + 1.2f, 0f), new Vector3(sW * 0.38f, 2.4f, sD * 0.36f), assets.DarkSteelMat);
                CreateSubBox(bldg.transform, "Rooftop_CommsMast", new Vector3(sW * 0.22f, rimY + 4.5f, 0f), new Vector3(0.35f, 9.0f, 0.35f), assets.DarkSteelMat);
                CreateSubBox(bldg.transform, "Aviation_Strobe", new Vector3(sW * 0.22f, rimY + 9.2f, 0f), new Vector3(0.55f, 0.55f, 0.55f), assets.NeonTubeMats[5]);
            }

            if (hasBamboo)
            {
                BuildBambooScaffoldingLattice(bldg.transform, sW * 0.78f, Mathf.Min(16f, shaftH * 0.55f), sD * 0.5f + 0.28f, assets);
            }
            return bldg;
        }

        /// <summary>
        /// Tier 3 Hero Archetype: Sharp Triangular Glass Prism Supertall inspired by Hong Kong's Bank of China Tower,
        /// with staggered sliced triangular quadrant shafts and glowing white LED geometric X-bracing.
        /// </summary>
        internal static GameObject SpawnTriangularPrismSupertall(
            Transform parent, string name, Vector3 basePos, float yawDeg, float baseSize, float totalHeight,
            int winIdx, int neonIdx, NeonCityAssetPack assets)
        {
            GameObject tower = new GameObject(name);
            tower.transform.SetParent(parent, false);
            tower.transform.position = basePos;
            tower.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            float[] heights = { totalHeight * 0.44f, totalHeight * 0.66f, totalHeight * 0.84f, totalHeight * 1.00f };
            float[] qYaws = { 0f, 90f, 180f, 270f };
            float halfW = baseSize * 0.5f;

            for (int q = 0; q < 4; q++)
            {
                float qh = heights[q];
                Quaternion wedgeRot = Quaternion.Euler(0f, qYaws[q], 0f);
                GameObject prism = CreateCustomMeshGO(tower.transform, $"Prism_Quadrant_{q}", assets.TriangularPrismMesh,
                    Vector3.zero, new Vector3(baseSize, qh, baseSize), assets.WindowMats[winIdx]);
                prism.transform.localRotation = wedgeRot;

                // White LED Geometric X-Bracing built in uniform-scaled tower space (zero non-uniform scale skew!)
                Vector3 localLBot = wedgeRot * new Vector3(-halfW, 0f, -halfW - 0.18f);
                Vector3 localRBot = wedgeRot * new Vector3(halfW, 0f, -halfW - 0.18f);
                Vector3 localLTop = localLBot + Vector3.up * qh;
                Vector3 localRTop = localRBot + Vector3.up * qh;

                CreateBeamBetween(tower.transform, $"Edge_L_{q}", localLBot, localLTop, 0.55f, assets.WhiteLedBraceMat);
                CreateBeamBetween(tower.transform, $"Edge_R_{q}", localRBot, localRTop, 0.55f, assets.WhiteLedBraceMat);

                int tiers = 3 + q;
                float stepH = qh / tiers;
                for (int t = 0; t < tiers; t++)
                {
                    Vector3 bL = localLBot + Vector3.up * (t * stepH);
                    Vector3 bR = localRBot + Vector3.up * (t * stepH);
                    Vector3 tL = bL + Vector3.up * stepH;
                    Vector3 tR = bR + Vector3.up * stepH;

                    CreateBeamBetween(tower.transform, $"XBrace_H_{q}_{t}", tL, tR, 0.46f, assets.WhiteLedBraceMat);
                    CreateBeamBetween(tower.transform, $"XBrace_D1_{q}_{t}", bL, tR, 0.52f, assets.WhiteLedBraceMat);
                    CreateBeamBetween(tower.transform, $"XBrace_D2_{q}_{t}", bR, tL, 0.52f, assets.WhiteLedBraceMat);
                }
            }

            // Twin soaring spire antennas from highest wedge crown into low-altitude storm clouds
            CreateSubBox(tower.transform, "Twin_Spire_Main", new Vector3(halfW * 0.45f, totalHeight + 13f, -halfW * 0.25f), new Vector3(0.75f, 26f, 0.75f), assets.WhiteLedBraceMat);
            CreateSubBox(tower.transform, "Twin_Spire_Sub", new Vector3(-halfW * 0.35f, heights[2] + 10f, halfW * 0.25f), new Vector3(0.60f, 20f, 0.60f), assets.NeonTubeMats[neonIdx]);
            CreateSubBox(tower.transform, "Aviation_Beacon_Top", new Vector3(halfW * 0.45f, totalHeight + 26.5f, -halfW * 0.25f), new Vector3(1.1f, 1.1f, 1.1f), assets.NeonTubeMats[5]);
            return tower;
        }

        private static void CreateBeamBetween(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            Vector3 delta = b - a;
            float len = delta.magnitude;
            if (len < 0.01f) return;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta / len);
            go.transform.localScale = new Vector3(thickness, len, thickness);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>
        /// Tier 3 Hero Archetype: Tiered Neo-Futuristic Spire (Central Plaza / Two IFC silhouette),
        /// with telescoping octagonal shafts, HDR neon floor collars, lit crown pyramid, and needle spire.
        /// </summary>
        internal static GameObject SpawnTieredNeoFuturisticSpire(
            Transform parent, string name, Vector3 basePos, float width, float totalHeight,
            int winIdx, int neonIdx, NeonCityAssetPack assets)
        {
            GameObject tower = new GameObject(name);
            tower.transform.SetParent(parent, false);
            tower.transform.position = basePos;

            Material winMat = assets.WindowMats[winIdx % assets.WindowMats.Length];
            Material neonMat = assets.NeonTubeMats[neonIdx % assets.NeonTubeMats.Length];
            Material secNeon = assets.NeonTubeMats[(neonIdx + 1) % assets.NeonTubeMats.Length];

            float[] fracH = { 0.42f, 0.30f, 0.18f, 0.10f };
            float[] fracW = { 1.00f, 0.78f, 0.56f, 0.32f };
            float curY = 0f;

            for (int t = 0; t < 4; t++)
            {
                float h = totalHeight * fracH[t];
                float w = width * fracW[t];
                CreateCustomMeshGO(tower.transform, $"Tier_Octagon_{t}", assets.OctagonPrismMesh,
                    new Vector3(0f, curY, 0f), new Vector3(w, h, w), t == 3 ? assets.WindowMats[5] : winMat);
                // Glowing neon ring collar at top of each setback tier
                CreateCustomMeshGO(tower.transform, $"Tier_NeonRing_{t}", assets.OctagonPrismMesh,
                    new Vector3(0f, curY + h - 0.35f, 0f), new Vector3(w * 1.04f, 0.65f, w * 1.04f), t % 2 == 0 ? neonMat : secNeon);
                curY += h;
            }

            CreateSubBox(tower.transform, "Spire_Needle", new Vector3(0f, curY + 12f, 0f), new Vector3(0.75f, 24f, 0.75f), assets.WhiteLedBraceMat);
            CreateSubBox(tower.transform, "Spire_Beacon", new Vector3(0f, curY + 24.4f, 0f), new Vector3(1.1f, 1.1f, 1.1f), assets.NeonTubeMats[5]);
            return tower;
        }

        internal static GameObject SpawnCurvedWaterfrontTower(
            Transform parent, string name, Vector3 basePos, float yawDeg, float w, float d, float h,
            int winIdx, int neonIdx, NeonCityAssetPack assets)
        {
            GameObject tower = new GameObject(name);
            tower.transform.SetParent(parent, false);
            tower.transform.position = basePos;
            tower.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            Material winMat = assets.WindowMats[winIdx % assets.WindowMats.Length];
            Material neonMat = assets.NeonTubeMats[neonIdx % assets.NeonTubeMats.Length];

            CreateCustomMeshGO(tower.transform, "Curved_Body", assets.CurvedArcTowerMesh, Vector3.zero, new Vector3(w, h * 0.78f, d), winMat);
            CreateCustomMeshGO(tower.transform, "Curved_Step", assets.CurvedArcTowerMesh, new Vector3(0f, h * 0.78f, 0f), new Vector3(w * 0.76f, h * 0.22f, d * 0.76f), winMat);
            CreateCustomMeshGO(tower.transform, "Curved_CrownNeon", assets.CurvedArcTowerMesh, new Vector3(0f, h - 0.5f, 0f), new Vector3(w * 0.79f, 0.65f, d * 0.79f), neonMat);
            CreateSubBox(tower.transform, "Waterfront_Fin", new Vector3(0f, h * 0.5f, d * 0.38f), new Vector3(0.45f, h * 0.92f, 0.8f), assets.WhiteLedBraceMat);
            return tower;
        }

        internal static GameObject SpawnScaledFbxTongLauCluster(Transform parent, GameObject fbx, string bldgName, string outName, Vector3 pos, float yaw, float scale, NeonCityAssetPack assets)
        {
            Transform src = fbx.transform.Find(bldgName);
            if (src == null) return null;

            GameObject go = new GameObject(outName);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            GameObject meshChild = new GameObject("Mesh");
            meshChild.transform.SetParent(go.transform, false);
            meshChild.transform.localRotation = src.localRotation;
            meshChild.transform.localScale = src.localScale * scale;

            var mf = meshChild.AddComponent<MeshFilter>();
            var srcMf = src.GetComponent<MeshFilter>();
            if (srcMf != null && srcMf.sharedMesh != null)
            {
                mf.sharedMesh = srcMf.sharedMesh;
                Bounds b = srcMf.sharedMesh.bounds;
                Matrix4x4 m = Matrix4x4.TRS(Vector3.zero, src.localRotation, src.localScale * scale);
                float minY = float.MaxValue;
                Vector3 centerXZ = Vector3.zero;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 tp = m.MultiplyPoint3x4(corner);
                    if (tp.y < minY) minY = tp.y;
                    centerXZ += tp;
                }
                centerXZ /= 8f;
                meshChild.transform.localPosition = new Vector3(-centerXZ.x, -minY, -centerXZ.z);
            }

            var mr = meshChild.AddComponent<MeshRenderer>();
            var srcMr = src.GetComponent<MeshRenderer>();
            if (srcMr != null)
            {
                Material[] mats = new Material[srcMr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    string mName = srcMr.sharedMaterials[i] != null ? srcMr.sharedMaterials[i].name.ToLowerInvariant() : "";
                    mats[i] = mName.Contains("glass") ? assets.WindowMats[4] :
                              mName.Contains("ac") ? assets.ACUnitMat :
                              mName.Contains("tank") || mName.Contains("vent") ? assets.BarrierMat : assets.TongLauConcreteMat;
                }
                mr.sharedMaterials = mats;
            }
            return go;
        }

        #endregion

        #region Skybridges & Cantilevered Hong Kong Neon Signs

        private static void BuildSkybridgesAndCantileveredCanyonSigns(Transform parent, NeonCityAssetPack assets)
        {
            GameObject canyonRoot = new GameObject("City_Skybridges_And_CantileveredNeon");
            canyonRoot.transform.SetParent(parent, false);

            // 1. Enclosed Glass Pedestrian Skybridges at Chest/Eye-Level across intersecting side-streets (Y = CityDeckY + 18m = 27m)
            BuildGlassSkybridge(canyonRoot.transform, "Skybridge_EastCross_1",
                new Vector3(52f, CityDeckY + 18.0f, 58f), 90f, spanLen: 40f, width: 4.6f, height: 4.4f, assets);
            BuildGlassSkybridge(canyonRoot.transform, "Skybridge_WestCross_2",
                new Vector3(-52f, CityDeckY + 19.0f, 154f), 90f, spanLen: 40f, width: 4.8f, height: 4.4f, assets);
            BuildGlassSkybridge(canyonRoot.transform, "Skybridge_EastCross_2",
                new Vector3(54f, CityDeckY + 17.5f, 154f), 90f, spanLen: 42f, width: 4.8f, height: 4.4f, assets);

            // High Overhead Enclosed Glass Pedestrian Skybridges linking canyon walls above Leatherback (33.87m -> Y_rel = 40.5m = 49.5m!)
            BuildGlassSkybridge(canyonRoot.transform, "Skybridge_MainCanyon_HighA",
                new Vector3(0f, CityDeckY + 40.5f, 92f), 0f, spanLen: 76f, width: 5.5f, height: 5.2f, assets);
            BuildGlassSkybridge(canyonRoot.transform, "Skybridge_MainCanyon_HighB",
                new Vector3(0f, CityDeckY + 41.5f, 192f), 0f, spanLen: 76f, width: 5.5f, height: 5.2f, assets);

            // 2. Massive Cantilevered Vertical & Horizontal Cantonese Neon Signboards jutting out over the narrow street!
            //    Layered 3-4 deep down Nathan Road style avenues at Y_rel = 10.5m .. 25.0m (brushing right past Gipsy's shoulders!)
            //    Zero physics colliders so Gipsy & Leatherback can brush right past glowing signs seamlessly!
            (float z, float relY, float jutDist, float signW, float signH, bool isVertical, int signIdx, int neonIdx)[] eastCantilevers =
            {
                (26f,  15.5f, 9.5f,  4.4f, 12.5f, true,  0, 5),
                (34f,  12.2f, 8.2f,  10.8f, 4.6f, false, 1, 0),
                (82f,  16.8f, 10.5f, 4.6f, 13.5f, true,  2, 1),
                (89f,  12.5f, 8.8f,  11.5f, 4.8f, false, 3, 4),
                (97f,  21.0f, 11.8f, 4.8f, 14.0f, true,  4, 2),
                (106f, 14.8f, 9.2f,  10.5f, 4.4f, false, 5, 5),
                (118f, 18.2f, 11.2f, 4.5f, 13.0f, true,  0, 0),
                (128f, 11.5f, 8.4f,  11.2f, 4.6f, false, 2, 1),
                (176f, 16.5f, 10.8f, 4.8f, 14.2f, true,  3, 4),
                (185f, 12.8f, 9.0f,  11.0f, 4.8f, false, 4, 2),
                (195f, 20.5f, 11.5f, 4.6f, 13.6f, true,  5, 5),
                (206f, 14.2f, 9.5f,  10.6f, 4.5f, false, 1, 0),
                (218f, 17.5f, 10.4f, 4.6f, 13.2f, true,  0, 1),
                (230f, 13.0f, 8.6f,  10.4f, 4.4f, false, 2, 5),
            };

            (float z, float relY, float jutDist, float signW, float signH, bool isVertical, int signIdx, int neonIdx)[] westCantilevers =
            {
                (79f,  15.2f, 10.2f, 4.6f, 13.2f, true,  1, 0),
                (87f,  11.8f, 8.5f,  10.8f, 4.6f, false, 5, 5),
                (96f,  19.8f, 11.4f, 4.8f, 13.8f, true,  3, 4),
                (104f, 13.5f, 9.2f,  11.2f, 4.5f, false, 0, 1),
                (115f, 17.0f, 10.6f, 4.5f, 12.8f, true,  4, 2),
                (126f, 12.2f, 8.6f,  10.6f, 4.4f, false, 2, 0),
                (175f, 15.8f, 10.4f, 4.6f, 13.4f, true,  5, 5),
                (184f, 21.0f, 11.6f, 4.8f, 14.0f, true,  0, 0),
                (192f, 12.6f, 8.8f,  11.0f, 4.6f, false, 1, 1),
                (203f, 17.4f, 10.8f, 4.5f, 13.2f, true,  3, 4),
                (215f, 13.8f, 9.2f,  10.8f, 4.5f, false, 4, 2),
                (226f, 18.6f, 10.2f, 4.6f, 13.0f, true,  2, 5),
            };

            for (int i = 0; i < eastCantilevers.Length; i++)
            {
                var c = eastCantilevers[i];
                SpawnCantileveredNeonSign(canyonRoot.transform, $"Cantilever_E_{i}",
                    new Vector3(CanyonHalfWidth + 0.8f, CityDeckY + c.relY, c.z),
                    jutDirectionX: -1f, c.jutDist, c.signW, c.signH, c.isVertical,
                    c.isVertical ? assets.VerticalSignMats[c.signIdx] : assets.SignMats[c.signIdx],
                    assets.NeonTubeMats[c.neonIdx], assets.DarkSteelMat);
            }

            for (int i = 0; i < westCantilevers.Length; i++)
            {
                var c = westCantilevers[i];
                SpawnCantileveredNeonSign(canyonRoot.transform, $"Cantilever_W_{i}",
                    new Vector3(-CanyonHalfWidth - 0.8f, CityDeckY + c.relY, c.z),
                    jutDirectionX: 1f, c.jutDist, c.signW, c.signH, c.isVertical,
                    c.isVertical ? assets.VerticalSignMats[c.signIdx] : assets.SignMats[c.signIdx],
                    assets.NeonTubeMats[c.neonIdx], assets.DarkSteelMat);
            }
        }

        internal static GameObject BuildGlassSkybridge(Transform parent, string name, Vector3 pos, float yawDeg, float spanLen, float width, float height, NeonCityAssetPack assets)
        {
            GameObject bridge = new GameObject(name);
            bridge.transform.SetParent(parent, false);
            bridge.transform.position = pos;
            bridge.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            // Illuminated enclosed pedestrian glass tube
            CreateSubBox(bridge.transform, "Glass_Tube", Vector3.zero, new Vector3(spanLen, height, width), assets.SkybridgeGlassMat);
            CreateSubBox(bridge.transform, "Roof_SteelDeck", new Vector3(0f, height * 0.5f + 0.18f, 0f), new Vector3(spanLen, 0.36f, width + 0.4f), assets.DarkSteelMat);
            CreateSubBox(bridge.transform, "Bottom_Truss", new Vector3(0f, -height * 0.5f - 0.18f, 0f), new Vector3(spanLen, 0.36f, width + 0.4f), assets.DarkSteelMat);
            CreateSubBox(bridge.transform, "Neon_UnderRail_S", new Vector3(0f, -height * 0.5f - 0.38f, -width * 0.5f), new Vector3(spanLen, 0.22f, 0.22f), assets.NeonTubeMats[0]);
            CreateSubBox(bridge.transform, "Neon_UnderRail_N", new Vector3(0f, -height * 0.5f - 0.38f, width * 0.5f), new Vector3(spanLen, 0.22f, 0.22f), assets.NeonTubeMats[1]);
            return bridge;
        }

        internal static GameObject SpawnCantileveredNeonSign(
            Transform parent, string name, Vector3 wallAnchorPos, float jutDirectionX,
            float jutDist, float signWidth, float signHeight, bool isVertical,
            Material faceMat, Material tubeMat, Material steelMat)
        {
            GameObject sign = new GameObject(name);
            sign.transform.SetParent(parent, false);
            sign.transform.position = wallAnchorPos;

            // Keep signboard 0.85m-1.8m clear of facade and embed steel truss 2.2m inside building wall so it never floats or overshoots
            float wallGap = Mathf.Clamp(jutDist - signWidth, 0.85f, 1.85f);
            float wallEmbed = 2.2f;
            float boardCenterDist = wallGap + signWidth * 0.5f;
            float trussOuter = wallGap + signWidth * 0.95f;
            float trussInner = -wallEmbed;
            float trussLen = trussOuter - trussInner;
            float trussCenterX = jutDirectionX * ((trussOuter + trussInner) * 0.5f);
            float armY = signHeight * 0.34f;

            CreateSubBox(sign.transform, "Wall_Bracket", new Vector3(0f, 0f, 0f), new Vector3(0.32f, signHeight * 0.82f, 0.55f), steelMat);
            CreateSubBox(sign.transform, "Truss_TopArm", new Vector3(trussCenterX, armY, 0f), new Vector3(trussLen, 0.28f, 0.28f), steelMat);
            CreateSubBox(sign.transform, "Truss_BotArm", new Vector3(trussCenterX, -armY, 0f), new Vector3(trussLen, 0.28f, 0.28f), steelMat);

            float diagSpan = wallGap + signWidth * 0.55f;
            CreateSubBox(sign.transform, "Truss_DiagStrut", new Vector3(jutDirectionX * (diagSpan * 0.45f), 0f, 0f),
                new Vector3(diagSpan, 0.20f, 0.20f), steelMat, Quaternion.Euler(0f, 0f, jutDirectionX * 26f));

            // Double-sided signboard oriented facing DOWN THE STREET (+Z / -Z) at boardCenterDist
            float tipX = jutDirectionX * boardCenterDist;
            GameObject panel = new GameObject("Cantilevered_Board");
            panel.transform.SetParent(sign.transform, false);
            panel.transform.localPosition = new Vector3(tipX, 0f, 0f);
            panel.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            // Board frame & double-sided lit calligraphy quads facing both -Z (forward players) and +Z (looking back)
            CreateNeonBillboard(panel.transform, "SignBox", Vector3.zero, new Vector3(signWidth, signHeight, 0.42f), faceMat, steelMat, doubleSided: true);

            // Glowing vertical neon edge accent strips along outer edge of cantilevered sign
            CreateSubBox(panel.transform, "OuterEdge_Neon", new Vector3(jutDirectionX * (signWidth * 0.52f), 0f, 0f),
                new Vector3(0.22f, signHeight + 0.3f, 0.48f), tubeMat);
            return sign;
        }

        #endregion

        #region Typhoon Clouds, Mist Pools, Reflection Probes & Neon Spill Lights

        private static void BuildTyphoonCloudsMistAndReflections(Transform parent, NeonCityAssetPack assets)
        {
            GameObject fxRoot = new GameObject("City_Clouds_Mist_Reflections_And_Lights");
            fxRoot.transform.SetParent(parent, false);

            // 1. Low-Altitude Typhoon Storm Cloud Ceiling (Y_world = 70m .. 124m) partially swallowing upper thirds of supertall peaks!
            for (int layer = 0; layer < 28; layer++)
            {
                float cz = -80f + (layer / 4) * 58f + (layer % 3) * 16f;
                float cx = ((layer % 4) - 1.5f) * 85f;
                float cy = 74f + (layer % 5) * 11.5f;
                float w = 240f + (layer % 3) * 65f;
                float tilt = (layer % 2 == 0) ? 84f : 96f;

                GameObject cloud = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cloud.name = $"Cloud_StormCeiling_{layer}";
                cloud.transform.SetParent(fxRoot.transform, false);
                cloud.transform.position = new Vector3(cx, cy, cz);
                cloud.transform.rotation = Quaternion.Euler(tilt, (layer * 37f) % 360f, 0f);
                cloud.transform.localScale = new Vector3(w, w * 0.85f, 1f);
                UnityEngine.Object.DestroyImmediate(cloud.GetComponent<Collider>());
                var mr = cloud.GetComponent<MeshRenderer>();
                mr.sharedMaterial = assets.CloudCeilingMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            // 2. Street-Level & Harbour Mist Ribbons pooling between buildings & skimming Victoria Harbour
            Vector3[] mistPositions =
            {
                new Vector3(-18f, HarbourWaterMeanY + 2.2f, -55f),
                new Vector3(16f,  HarbourWaterMeanY + 2.4f, -24f),
                new Vector3(-22f, CityDeckY + 4.5f, 52f),
                new Vector3(24f,  CityDeckY + 4.8f, 96f),
                new Vector3(-24f, CityDeckY + 4.5f, 148f),
                new Vector3(20f,  CityDeckY + 5.2f, 198f),
            };
            for (int i = 0; i < mistPositions.Length; i++)
            {
                GameObject mist = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mist.name = $"Cloud_StreetMist_{i}";
                mist.transform.SetParent(fxRoot.transform, false);
                mist.transform.position = mistPositions[i];
                mist.transform.rotation = Quaternion.Euler(14f, i * 42f, 0f);
                mist.transform.localScale = new Vector3(48f, 14f, 1f);
                UnityEngine.Object.DestroyImmediate(mist.GetComponent<Collider>());
                var mr = mist.GetComponent<MeshRenderer>();
                mr.sharedMaterial = assets.MistPoolMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            // 3. Reflection Probes down Victoria Harbour & Wet Highway Canyon so wet puddles mirror crimson/cyan neon!
            Vector3[] probePos =
            {
                new Vector3(0f, HarbourWaterMeanY + 12f, -45f),
                new Vector3(0f, CityDeckY + 14f, 75f),
                new Vector3(0f, CityDeckY + 14f, 175f),
            };
            for (int i = 0; i < probePos.Length; i++)
            {
                GameObject pGo = new GameObject($"ReflectionProbe_NeonCanyon_{i}");
                pGo.transform.SetParent(fxRoot.transform, false);
                pGo.transform.position = probePos[i];
                var probe = pGo.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                probe.resolution = 256;
                probe.boxProjection = true;
                probe.size = new Vector3(145f, 85f, 150f);
                probe.intensity = 1.25f;
            }

            // 4. Forward+ Non-Shadow Neon Spill Point Lights for saturated wet road & facade reflections
            float[] spillZs = { -55f, -5f, 35f, 75f, 115f, 155f, 195f, 232f };
            Color[] spillCols =
            {
                new Color(0.14f, 0.90f, 1.00f), // Electric Cyan
                new Color(1.00f, 0.16f, 0.36f), // Hyper Crimson
                new Color(0.98f, 0.22f, 0.72f), // Hot Magenta
                new Color(1.00f, 0.58f, 0.16f), // Amber Gold
                new Color(0.18f, 0.96f, 0.56f), // Jade Green
            };

            for (int i = 0; i < spillZs.Length; i++)
            {
                float yBase = spillZs[i] < 0f ? (HarbourWaterMeanY + 9f) : (CityDeckY + 14f);
                for (int side = -1; side <= 1; side += 2)
                {
                    if (spillZs[i] < 10f && side < 0) continue; // Only East Quay in South Harbour (West has Harbour Waterfront LEDs)
                    int cIdx = (i + (side > 0 ? 1 : 0)) % spillCols.Length;
                    GameObject lGo = new GameObject($"NeonSpill_{(side < 0 ? "W" : "E")}_{i}");
                    lGo.transform.SetParent(fxRoot.transform, false);
                    lGo.transform.position = new Vector3(side * (CanyonHalfWidth - 5.5f), yBase, spillZs[i]);

                    var l = lGo.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = spillCols[cIdx];
                    l.intensity = 145f;
                    l.range = 56f;
                    l.shadows = LightShadows.None;
                }
            }
        }

        private static void BuildSmoothArenaBarriers()
        {
            GameObject root = new GameObject("Arena_Invisible_Barriers");
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;

            float wallHeight = 140f;
            float wallThickness = 24f;
            float wallCenterY = wallHeight * 0.5f - 10f;

            // North Wall (end of street canyon at Z = +232)
            CreateInvisibleWall(root.transform, "Barrier_North",
                new Vector3(0f, wallCenterY, 232f + wallThickness * 0.5f),
                new Vector3(160f, wallHeight, wallThickness), 0f);

            // South Wall (Harbour Bay end at Z = -114)
            CreateInvisibleWall(root.transform, "Barrier_South",
                new Vector3(-10f, wallCenterY, -114f - wallThickness * 0.5f),
                new Vector3(170f, wallHeight, wallThickness), 0f);

            // East Wall continuous along X = +35.8m (Z = -125 .. +245)
            CreateInvisibleWall(root.transform, "Barrier_East",
                new Vector3(35.8f + wallThickness * 0.5f, wallCenterY, 60f),
                new Vector3(wallThickness, wallHeight, 390f), 0f);

            // West Wall Section 1: Harbour Bay boundary at X = -56m (Z = -125 .. -60)
            CreateInvisibleWall(root.transform, "Barrier_West_Harbour",
                new Vector3(-56.0f - wallThickness * 0.5f, wallCenterY, -92.5f),
                new Vector3(wallThickness, wallHeight, 66f), 0f);

            // West Wall Section 2: Smooth funnel from Harbour (-56, -60) cleanly onto Causeway Ramp (-35.6, -34)
            Vector2 p0 = new Vector2(-56.0f, -60f);
            Vector2 p1 = new Vector2(-35.6f, -34f);
            Vector2 mid = (p0 + p1) * 0.5f;
            float funnelLen = Vector2.Distance(p0, p1) + 4f;
            float funnelYaw = Mathf.Atan2(p1.x - p0.x, p1.y - p0.y) * Mathf.Rad2Deg;
            Vector2 outerNormal = new Vector2(-(p1.y - p0.y), p1.x - p0.x).normalized;
            CreateInvisibleWall(root.transform, "Barrier_West_Funnel",
                new Vector3(mid.x + outerNormal.x * (wallThickness * 0.5f), wallCenterY, mid.y + outerNormal.y * (wallThickness * 0.5f)),
                new Vector3(wallThickness, wallHeight, funnelLen), funnelYaw);

            // West Wall Section 3: Ramp -> Downtown Neon Canyon continuous at X = -35.6m (Z = -34 .. +245)
            CreateInvisibleWall(root.transform, "Barrier_West_Canyon",
                new Vector3(-35.6f - wallThickness * 0.5f, wallCenterY, 105.5f),
                new Vector3(wallThickness, wallHeight, 282f), 0f);
        }

        private static void CreateInvisibleWall(Transform parent, string name, Vector3 pos, Vector3 size, float yawDeg)
        {
            GameObject w = new GameObject(name);
            w.transform.SetParent(parent, false);
            w.transform.position = pos;
            w.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            var col = w.AddComponent<BoxCollider>();
            col.size = size;
            col.isTrigger = false;
            w.layer = 0;
            w.tag = "Untagged";
        }

        #endregion

        #region Geometry Primitive Helpers & Build Sync

        internal static GameObject CreateSubBox(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat, Quaternion? localRot = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;

            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
            }
            return go;
        }

        internal static GameObject CreateCustomMeshGO(Transform parent, string name, Mesh mesh, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            return go;
        }

        internal static void CreateNeonBillboard(Transform parent, string name, Vector3 localPos, Vector3 size, Material faceMat, Material frameMat, bool doubleSided)
        {
            GameObject backing = CreateSubBox(parent, name + "_Frame", localPos, size, frameMat);
            backing.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            CreateSignQuad(parent, name + "_FaceFront", localPos + new Vector3(0f, 0f, size.z * 0.5f + 0.04f), Quaternion.Euler(0f, 180f, 0f), size, faceMat);
            if (doubleSided)
            {
                CreateSignQuad(parent, name + "_FaceBack", localPos - new Vector3(0f, 0f, size.z * 0.5f + 0.04f), Quaternion.identity, size, faceMat);
            }
        }

        private static void CreateSignQuad(Transform parent, string name, Vector3 localPos, Quaternion localRot, Vector3 size, Material mat)
        {
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = name;
            face.transform.SetParent(parent, false);
            face.transform.localPosition = localPos;
            face.transform.localRotation = localRot;
            face.transform.localScale = new Vector3(size.x * 0.94f, size.y * 0.92f, 1f);

            var col = face.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);

            var mr = face.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        private static void UpdateMainMenuAndBuildSettings()
        {
            if (File.Exists(SetupPacificRimMainMenuScene.MainMenuScenePath))
            {
                EditorSceneManager.OpenScene(SetupPacificRimMainMenuScene.MainMenuScenePath, OpenSceneMode.Single);
                SetupPacificRimMainMenuScene.SetupMainMenuCanvasUI();
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
            SetupPacificRimMainMenuScene.UpdateBuildSettings();
        }

        private static void DestroyNamedObjects(params string[] names)
        {
            var set = new HashSet<string>(names, StringComparer.Ordinal);
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (go != null && set.Contains(go.name))
                {
                    Undo.DestroyObjectImmediate(go);
                }
            }
        }

        #endregion
    }
}
#endif
