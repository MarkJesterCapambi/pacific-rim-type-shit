#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PacificRimAtmosphere;
using PacificRimCutscene;
using PacificRimEnvironment;
using PacificRimUI;
using PacificRimUI.Editor;

namespace PacificRimWater.Editor
{
    /// <summary>
    /// Builds Level 2: "Otachi Arena" using Save Striker Eureka as the staging base
    /// (without cutscenes, Striker Eureka, or the Hong Kong Neon City transition) and
    /// spawns the non-flying Category IV Kaiju Otachi with her Hit-and-Run Stalker AI,
    /// dedicated Animator Controller, Normal/Emission material, and updated 3-level campaign wiring.
    /// </summary>
    public static class SetupOtachiArenaLevel
    {
        public const string SceneName = "Otachi Arena";
        public const string ScenePath = "Assets/Scenes/Otachi Arena.unity";
        private const string BaseScenePath = "Assets/Scenes/Save Striker Eureka.unity";

        private const string OtachiFbxPath = "Assets/Models/Otachi.fbx";
        private const string FlyingOtachiFbxPath = "Assets/Models/FlyingOtachi.fbx";
        private const string OilTankerFbxPath = "Assets/Models/Oil Tanker.fbx";
        private const string OilTankerTexPath = "Assets/Models/OilTankerTexture.png";
        private const string OilTankerMatPath = "Assets/Materials/PacificRim_OilTanker.mat";
        private const string OtachiControllerPath = "Assets/Animations/Otachi_Controller.controller";
        private const string FlyingOtachiControllerPath = "Assets/Animations/FlyingOtachi_Controller.controller";
        private const string FlyingOtachiWingMaskPath = "Assets/Animations/FlyingOtachi_WingMask.mask";
        private const string OtachiMatPath = "Assets/Materials/PacificRim_Otachi.mat";
        private const string OtachiDiffusePath = "Assets/Materials/Otachi_Diffuse.png";
        private const string OtachiNormalPath = "Assets/Materials/Otachi_Normal.png";
        private const string OtachiEmissionPath = "Assets/Materials/Otachi_Emission.png";

        // HDR multiplier applied on top of Otachi_Emission.png (already painted acid-teal)
        private static readonly Color OtachiNeonHdr = new Color(3.2f, 4.4f, 4.0f, 1f);

        [MenuItem("Pacific Rim/Build Otachi Arena (Level 2)", false, 12)]
        public static void BuildOtachiArenaMenu()
        {
            BuildOtachiArenaAndSyncCampaign();
        }

        public static void BuildOtachiArenaAndSyncCampaign()
        {
            if (!File.Exists(BaseScenePath))
            {
                Debug.LogError($"[OtachiArena] Missing base scene '{BaseScenePath}'.");
                return;
            }

            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                EditorSceneManager.SaveOpenScenes();
            }

            // 1. Import & configure Otachi + FlyingOtachi FBX animations, Oil Tanker material, and Animator Controllers
            if (!ConfigureOtachiFbxImporter())
            {
                return;
            }
            ConfigureFlyingOtachiFbxImporter();
            Material otachiMat = EnsureOtachiMaterial();
            Material oilTankerMat = EnsureOilTankerMaterial();
            RuntimeAnimatorController otachiCtrl = BuildOtachiController();
            RuntimeAnimatorController flyingOtachiCtrl = BuildFlyingOtachiController();

            // 2. Duplicate Save Striker Eureka cleanly -> Otachi Arena.unity
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
                Debug.LogError($"[OtachiArena] Failed to duplicate '{BaseScenePath}' to '{ScenePath}'.");
                return;
            }
            AssetDatabase.Refresh();

            Scene arenaScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 3. Strip deployment cutscenes, Striker, Leatherback, all ocean/harbor/water objects & invisible barriers
            DestroyNamedObjects(
                "PacificRim_DeploymentCutscene",
                "HarborToNeon_Part1Cutscene",
                "HarborToNeon_Phase2CutsceneRuntime",
                "Otachi_IntroCutscene",
                "Otachi_OutroCutscene",
                "striker",
                "Cutscene_FadeOverlay",
                "Cinematic_Letterbox",
                "Kaiju_Leatherback",
                "Kaiju_Otachi",
                "Arena_Invisible_Barriers",
                "ProceduralOcean",
                "Floating_Debris_Field",
                "Victoria_Harbour_Waterfront",
                "Chinook_Patrol_Squadron");

            // 4. Reuse Hong Kong Neon City assets directly & build 500m x 500m 3x3 urban arena (zero water physics)
            NeonCityAssetPack neonAssets = SetupNeonCityAssets.EnsureAllAssets();
            HongKongCityCenterBuilder.BuildHongKongCityCenter(neonAssets);

            // 5. Configure dry city platform, Hong Kong Neon City storm-night bloom/fog, camera & lighting
            ConfigureArenaPlatformAndAtmosphere(neonAssets);

            // 6. Configure Gipsy Danger inside the 180x180m Central Transit Plaza facing North
            GameObject jaegerGo = ConfigureGipsyFighter();

            // 7. Spawn & configure non-flying Otachi facing South inside Central Transit Plaza
            GameObject otachiGo = CreateOtachiBoss(jaegerGo, otachiMat, otachiCtrl);

            // 8. Switch SplashManager to dry urban dust/sparks & rebuild urban Chinook squadron
            ConfigureCityRainAndSplashes(jaegerGo);
            RebuildUrbanChinookSquadron(jaegerGo, otachiGo);

            // 8b. Wire Level 2 Intro (~30s Oil Tanker Strike) & Outro (~45s Stratosphere Drop & Chain Sword) cutscenes
            WireOtachiCutscenesInScene(jaegerGo, otachiGo, oilTankerMat, otachiMat, flyingOtachiCtrl);

            // 9. Rebuild & wire HUD + Pause Menu
            SetupPacificRimUIScene.SetupUI();
            var hud = UnityEngine.Object.FindAnyObjectByType<JaegerHUD>();
            if (hud != null)
            {
                if (jaegerGo != null) SetupPacificRimUIScene.SetSerializedField(hud, "jaeger", jaegerGo.GetComponent<SimpleJaegerController>());
                if (otachiGo != null) SetupPacificRimUIScene.SetSerializedField(hud, "kaiju", otachiGo.GetComponent<KaijuAI>());
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

            EditorSceneManager.MarkSceneDirty(arenaScene);
            EditorSceneManager.SaveScene(arenaScene);

            // 10. Sync Main Menu (3-level campaign: L1 Leatherback -> L2 Otachi Arena -> L3 Operation Pitfall) & Build Settings
            SyncCampaignMenusAndBuildSettings();

            // 11. Re-open Otachi Arena so user can immediately inspect/play it
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00ffaa><b>[OtachiArena]</b></color> Built Level 2 '{ScenePath}' with non-flying Otachi (Hit-and-Run Stalker) and synced 3-level campaign (L1 Save Striker Eureka -> L2 Otachi Arena -> L3 Operation Pitfall).");
        }

        private static bool ConfigureOtachiFbxImporter()
        {
            ModelImporter importer = AssetImporter.GetAtPath(OtachiFbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[Otachi Import] ModelImporter not found at '{OtachiFbxPath}'");
                return false;
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.useFileUnits = true;
            importer.isReadable = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;

            TakeInfo[] takeInfos = importer.importedTakeInfos;
            if (takeInfos == null || takeInfos.Length == 0)
            {
                Debug.LogError($"[Otachi Import] No animation takes found in '{OtachiFbxPath}'");
                return false;
            }

            string[] loopTargets = { "Idle", "Walk", "Run", "BlockLoop", "DeathLoop" };
            var clips = new ModelImporterClipAnimation[takeInfos.Length];
            for (int i = 0; i < takeInfos.Length; i++)
            {
                TakeInfo t = takeInfos[i];
                float sr = t.sampleRate > 0f ? t.sampleRate : 30f;
                float startT = t.stopTime > t.startTime ? t.startTime : t.bakeStartTime;
                float stopT = t.stopTime > t.startTime ? t.stopTime : t.bakeStopTime;
                string clipName = string.IsNullOrEmpty(t.defaultClipName) ? t.name : t.defaultClipName;

                bool shouldLoop = loopTargets.Any(suffix =>
                    (clipName != null && clipName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) ||
                    (t.name != null && t.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));

                clips[i] = new ModelImporterClipAnimation
                {
                    name = clipName,
                    takeName = t.name,
                    firstFrame = Mathf.Round(startT * sr),
                    lastFrame = Mathf.Round(stopT * sr),
                    loopTime = shouldLoop,
                    loop = shouldLoop,
                    keepOriginalOrientation = false,
                    keepOriginalPositionXZ = false,
                    keepOriginalPositionY = true
                };
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            AssetDatabase.Refresh();
            Debug.Log($"<color=#00e5ff><b>[Otachi Import]</b></color> Configured '{OtachiFbxPath}' with {clips.Length} clips.");
            return true;
        }

        private static Material EnsureOtachiMaterial()
        {
            if (File.Exists(OtachiNormalPath))
            {
                var normImporter = AssetImporter.GetAtPath(OtachiNormalPath) as TextureImporter;
                if (normImporter != null && normImporter.textureType != TextureImporterType.NormalMap)
                {
                    normImporter.textureType = TextureImporterType.NormalMap;
                    normImporter.SaveAndReimport();
                }
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(OtachiMatPath);
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null)
            {
                mat = new Material(litShader);
                AssetDatabase.CreateAsset(mat, OtachiMatPath);
            }
            else if (litShader != null && mat.shader != litShader)
            {
                mat.shader = litShader;
            }

            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(OtachiDiffusePath);
            if (diffuse != null)
            {
                mat.mainTexture = diffuse;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", diffuse);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", diffuse);
            }

            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(OtachiNormalPath);
            if (normal != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normal);
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1.0f);
            }

            Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(OtachiEmissionPath);
            if (emission != null)
            {
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", emission);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", OtachiNeonHdr);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.08f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.42f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 2f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static AnimationClip FindClipInOtachiFbx(params string[] suffixes)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(OtachiFbxPath);
            foreach (string suffix in suffixes)
            {
                foreach (var asset in assets)
                {
                    if (asset is AnimationClip clip &&
                        !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase) &&
                        clip.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        return clip;
                    }
                }
            }
            return null;
        }

        private static RuntimeAnimatorController BuildOtachiController()
        {
            if (!Directory.Exists("Assets/Animations"))
            {
                AssetDatabase.CreateFolder("Assets", "Animations");
            }
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(OtachiControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(OtachiControllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(OtachiControllerPath);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "LocomotionSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1.0f
            });

            var sm = controller.layers[0].stateMachine;

            AnimationClip idleClip = FindClipInOtachiFbx("OtachiIdle");
            AnimationClip walkClip = FindClipInOtachiFbx("OtachiWalk");
            AnimationClip runClip = FindClipInOtachiFbx("OtachiRun");
            AnimationClip punchLClip = FindClipInOtachiFbx("OtachiPunchLeft");
            AnimationClip punchRClip = FindClipInOtachiFbx("OtachiPunchRight");
            AnimationClip slamClip = FindClipInOtachiFbx("OtachiSlam");
            AnimationClip emoteClip = FindClipInOtachiFbx("OtachiEmote", "OtachiEMP");
            AnimationClip blockEnterClip = FindClipInOtachiFbx("OtachiBlockEnter");
            AnimationClip blockLoopClip = FindClipInOtachiFbx("OtachiBlockLoop") ?? blockEnterClip;
            AnimationClip staggerLClip = FindClipInOtachiFbx("OtachiStaggerLeft");
            AnimationClip staggerRClip = FindClipInOtachiFbx("OtachiStaggerRight");
            AnimationClip staggerUClip = FindClipInOtachiFbx("OtachiStaggerUp");
            AnimationClip staggerDClip = FindClipInOtachiFbx("OtachiStaggerDown", "OtachiStagerDown");
            AnimationClip deathClip = FindClipInOtachiFbx("OtachiDeath");
            AnimationClip deathLoopClip = FindClipInOtachiFbx("OtachiDeathLoop");

            AnimatorState AddState(string stateName, AnimationClip clip, Vector3 pos, bool useLocoSpeed = false)
            {
                var st = sm.AddState(stateName, pos);
                st.motion = clip;
                st.speed = 1f;
                if (useLocoSpeed)
                {
                    st.speedParameterActive = true;
                    st.speedParameter = "LocomotionSpeed";
                }
                return st;
            }

            var idleState = AddState("Idle", idleClip, new Vector3(200, 0, 0));
            AddState("Walk", walkClip, new Vector3(200, 65, 0), useLocoSpeed: true);
            AddState("Run", runClip, new Vector3(200, 130, 0), useLocoSpeed: true);
            AddState("PunchLeft", punchLClip, new Vector3(200, 195, 0));
            AddState("PunchRight", punchRClip, new Vector3(200, 260, 0));
            AddState("Slam", slamClip, new Vector3(200, 325, 0));
            AddState("EMP", emoteClip, new Vector3(200, 390, 0));
            var blockEnterState = AddState("BlockEnter", blockEnterClip, new Vector3(200, 455, 0));
            var blockLoopState = AddState("BlockLoop", blockLoopClip, new Vector3(200, 520, 0));
            AddState("StaggerLeft", staggerLClip, new Vector3(200, 585, 0));
            AddState("StaggerRight", staggerRClip, new Vector3(200, 650, 0));
            AddState("StaggerUp", staggerUClip, new Vector3(200, 715, 0));
            AddState("StaggerDown", staggerDClip, new Vector3(200, 780, 0));
            var deathState = AddState("Death", deathClip, new Vector3(200, 845, 0));
            var deathLoopState = AddState("DeathLoop", deathLoopClip, new Vector3(200, 910, 0));

            sm.defaultState = idleState;

            if (blockEnterState != null && blockLoopState != null)
            {
                var blockTr = blockEnterState.AddTransition(blockLoopState);
                blockTr.hasExitTime = true;
                blockTr.exitTime = 0.95f;
                blockTr.duration = 0.1f;
            }

            if (deathState != null && deathLoopState != null)
            {
                var deathTr = deathState.AddTransition(deathLoopState);
                deathTr.hasExitTime = true;
                deathTr.exitTime = 0.95f;
                deathTr.duration = 0.15f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=#00ffaa><b>[Otachi Controller]</b></color> Created '{OtachiControllerPath}' with all 15 Otachi animation states.");
            return controller;
        }

        private static GameObject ConfigureGipsyFighter()
        {
            GameObject jaeger = GameObject.Find("Jaeger_Mech");
            if (jaeger == null)
            {
                Debug.LogWarning("[OtachiArena] Jaeger_Mech not found.");
                return null;
            }

            jaeger.transform.position = new Vector3(0f, 2.977f, -34f);
            jaeger.transform.rotation = Quaternion.identity;

            var ctrl = jaeger.GetComponent<SimpleJaegerController>();
            if (ctrl != null)
            {
                ctrl.isCutsceneActive = false;
                ctrl.currentHealth = ctrl.maxHealth;
                ctrl.maxStamina = 125f;
                ctrl.currentStamina = 125f;
                ctrl.exhaustionRecoveryRatio = 0.35f;
                ctrl.exhaustionRecoveryThreshold = 125f * 0.35f;
                ctrl.currentUltimateCharge = 0f;
                EditorUtility.SetDirty(ctrl);
            }

            var waveBody = jaeger.GetComponent<JaegerWaveBodyInteraction>();
            if (waveBody != null)
            {
                waveBody.enableBodySplashes = false;
                waveBody.enableCrestBubbles = false;
                EditorUtility.SetDirty(waveBody);
            }

            var waterInter = jaeger.GetComponent<JaegerWaterInteraction>();
            if (waterInter != null)
            {
                waterInter.enabled = false;
                EditorUtility.SetDirty(waterInter);
            }

            EditorUtility.SetDirty(jaeger);
            return jaeger;
        }

        private static GameObject CreateOtachiBoss(GameObject jaegerGo, Material otachiMat, RuntimeAnimatorController otachiCtrl)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(OtachiFbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[OtachiArena] FBX not found at '{OtachiFbxPath}'");
                return null;
            }

            GameObject otachi = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            otachi.name = "Kaiju_Otachi";
            otachi.transform.position = new Vector3(0f, 0f, 82f);
            otachi.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            otachi.transform.localScale = Vector3.one;

            foreach (var rend in otachi.GetComponentsInChildren<Renderer>(true))
            {
                if (otachiMat != null)
                {
                    var mats = new Material[rend.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = otachiMat;
                    rend.sharedMaterials = mats;
                }
                if (rend is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true;
                }
            }

            Avatar avatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(OtachiFbxPath))
            {
                if (asset is Avatar av)
                {
                    avatar = av;
                    break;
                }
            }

            var anim = GetOrAdd<Animator>(otachi);
            anim.avatar = avatar;
            anim.runtimeAnimatorController = otachiCtrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Rebind();

            // Sample Idle pose to measure exact native height & scale Otachi to menacing Cat IV proportions (~21.8m hunched height)
            AnimationClip idleClip = FindClipInOtachiFbx("OtachiIdle");
            if (idleClip != null)
            {
                idleClip.SampleAnimation(otachi, 0f);
            }

            float nativeHeight = 1.17f;
            if (ComputeCombinedSkinnedBounds(otachi, out Bounds nativeBounds) && nativeBounds.size.y > 0.01f)
            {
                nativeHeight = nativeBounds.size.y;
            }

            float targetHeight = 23.3f;
            float scaleFactor = targetHeight / Mathf.Max(0.01f, nativeHeight);
            otachi.transform.localScale = Vector3.one * scaleFactor;

            if (idleClip != null)
            {
                idleClip.SampleAnimation(otachi, 0f);
            }

            float computedGroundOffset = 0f;
            if (ComputeCombinedSkinnedBounds(otachi, out Bounds scaledBounds))
            {
                computedGroundOffset = Mathf.Max(0f, otachi.transform.position.y - scaledBounds.min.y);
                Debug.Log($"<color=#00ffaa><b>[Otachi Scale]</b></color> NativeH={nativeHeight:F3}m, TargetH={targetHeight:F2}m, Scale={scaleFactor:F2}, Bounds={scaledBounds.size:F2}, GroundYOffset={computedGroundOffset:F3}m");
            }
            otachi.transform.position = new Vector3(0f, computedGroundOffset, 38f);
            otachi.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // Configure CapsuleCollider in local space
            var col = GetOrAdd<CapsuleCollider>(otachi);
            col.direction = 1;
            float invScale = 1f / Mathf.Max(0.001f, scaleFactor);
            col.height = 24.5f * invScale;
            col.radius = 7.2f * invScale;
            col.center = new Vector3(0f, (12.2f - computedGroundOffset) * invScale, 0f);
            col.isTrigger = false;

            PhysicsMaterial physMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Materials/Jaeger_Physics.physicMaterial");
            if (physMat != null) col.sharedMaterial = physMat;

            // Configure Rigidbody (lighter, agile stalker mass)
            var rb = GetOrAdd<Rigidbody>(otachi);
            rb.mass = 2400f;
            rb.linearDamping = 3f;
            rb.angularDamping = 5f;
            rb.useGravity = false;
            rb.freezeRotation = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Clip guard (dry land urban combat — zero ocean body splashes)
            var clipGuard = GetOrAdd<JaegerSplashClipGuard>(otachi);
            clipGuard.AutoBuild();

            var waveBody = GetOrAdd<JaegerWaveBodyInteraction>(otachi);
            waveBody.enableBodySplashes = false;
            waveBody.enableCrestBubbles = false;

            // Bioluminescence controller (acid-teal glow)
            var bioCtrl = GetOrAdd<KaijuBioluminescenceController>(otachi);
            bioCtrl.baseEmissionColor = OtachiNeonHdr;
            bioCtrl.minIntensity = 0.85f;
            bioCtrl.maxIntensity = 1.15f;
            bioCtrl.maxLightIntensity = 2.0f;
            bioCtrl.lightRange = 20f;
            var smrTarget = otachi.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smrTarget != null) bioCtrl.targetRenderer = smrTarget;

            // Configure KaijuAI (Hit-and-Run Stalker Otachi)
            var ai = GetOrAdd<KaijuAI>(otachi);
            ai.kaijuDisplayName = "OTACHI";
            ai.kaijuCategoryLabel = "CATEGORY IV KAIJU";
            ai.isOtachi = true;
            ai.isSlattern = false;
            ai.isCutsceneMode = false;
            ai.groundYOffset = computedGroundOffset;
            ai.targetJaeger = jaegerGo != null ? jaegerGo.GetComponent<SimpleJaegerController>() : null;
            ai.animator = anim;
            bioCtrl.kaijuAI = ai;

            ai.maxHealth = 2850f;
            ai.currentHealth = 2850f;
            ai.maxPoise = 90f;
            ai.currentPoise = 90f;
            ai.poiseRegenRate = 24f;
            ai.poiseRegenDelay = 1.6f;
            ai.blockDamageReduction = 0.80f;
            ai.phase3DamageMultiplier = 0.90f;
            ai.phase3HealAmount = 220f;

            ai.canSprint = true;
            ai.walkSpeed = 13.0f;
            ai.sprintSpeed = 22.0f;
            ai.empWalkSpeed = 14.8f;
            ai.empSprintSpeed = 24.5f;
            ai.backwardWalkSpeed = 11.2f;
            ai.backwardSprintSpeed = 19.8f;
            ai.strafeSpeed = 10.2f;
            ai.turnSpeed = 155f;
            ai.attackTurnSpeed = 130f;
            ai.acceleration = 7.6f;

            ai.minCombatDistance = 14.5f;
            ai.meleeAttackRange = 21.5f;
            ai.sprintMinRange = 22.5f;

            ai.lightPunchDamage = 19.0f;
            ai.heavySlamDamage = 38.0f;
            ai.punchStrikeRadius = 12.5f;
            ai.slamStrikeRadius = 14.5f;
            ai.empBuffDuration = 12.0f;
            ai.empCooldownDuration = 15.0f;
            ai.initialEmpDelay = 4.0f;

            ai.otachiStalkMinDist = 25f;
            ai.otachiStalkIdealDist = 34f;
            ai.otachiAcidPoolInterval = 0.95f;
            ai.otachiRetreatAcidEnabled = true;

            EditorUtility.SetDirty(otachi);
            return otachi;
        }

        private static void ConfigureArenaPlatformAndAtmosphere(NeonCityAssetPack neonAssets)
        {
            GameObject platform = GameObject.Find("Test_Platform");
            if (platform != null)
            {
                platform.transform.position = new Vector3(0f, -0.5f, 0f);
                platform.transform.localScale = new Vector3(1400f, 1f, 1400f);
                platform.tag = "Ground";
                platform.layer = 0;
                var mr = platform.GetComponent<MeshRenderer>();
                if (mr != null && neonAssets.DarkConcreteMat != null)
                {
                    mr.sharedMaterial = neonAssets.DarkConcreteMat;
                }
                EditorUtility.SetDirty(platform);
            }

            // Match Hong Kong Neon City midnight-blue storm shadows & vivid HDR neon bloom
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
                    volCtrl.stormSkyAmbient = new Color(0.08f, 0.13f, 0.25f, 1f);
                    volCtrl.stormEquatorAmbient = new Color(0.06f, 0.09f, 0.18f, 1f);
                    volCtrl.stormGroundAmbient = new Color(0.03f, 0.04f, 0.09f, 1f);
                    volCtrl.enableDenseFog = true;
                    volCtrl.fogDensity = 0.0024f;
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
                mainCam.farClipPlane = 1200f;
                mainCam.backgroundColor = new Color(0.032f, 0.052f, 0.105f, 1f);
                var camFollow = mainCam.GetComponent<JaegerCameraFollow>();
                if (camFollow != null)
                {
                    var jaeger = GameObject.Find("Jaeger_Mech");
                    if (jaeger != null) camFollow.target = jaeger.transform;
                    camFollow.isFreeCam = false;
                    camFollow.preventEnvironmentClipping = true;
                    camFollow.minPitch = -42f;
                    camFollow.maxPitch = 60f;
                    camFollow.CurrentYaw = 0f;
                    EditorUtility.SetDirty(camFollow);
                }
                EditorUtility.SetDirty(mainCam);
            }
        }

        private static void ConfigureCityRainAndSplashes(GameObject jaegerGo)
        {
            var splashMgr = UnityEngine.Object.FindAnyObjectByType<WaterSplashManager>();
            if (splashMgr != null)
            {
                splashMgr.isDryLandMode = true;
                splashMgr.isDeepUnderwaterMode = false;
                for (int i = splashMgr.transform.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(splashMgr.transform.GetChild(i).gameObject);
                }
                EditorUtility.SetDirty(splashMgr);
            }

            var stormMgr = UnityEngine.Object.FindAnyObjectByType<StormWeatherManager>();
            if (stormMgr != null)
            {
                Camera mainCam = Camera.main ?? GameObject.FindWithTag("MainCamera")?.GetComponent<Camera>();
                stormMgr.followTarget = mainCam != null ? mainCam.transform : (jaegerGo != null ? jaegerGo.transform : null);
                if (stormMgr.rainParticles != null)
                {
                    stormMgr.rainParticles.gameObject.SetActive(true);
                }
                EditorUtility.SetDirty(stormMgr);
            }

            var lightning = UnityEngine.Object.FindAnyObjectByType<LightningSystem>();
            if (lightning != null)
            {
                lightning.sheetLightningOnly = true;
                lightning.followCenter = jaegerGo != null ? jaegerGo.transform : null;
                EditorUtility.SetDirty(lightning);
            }
        }

        private static void RebuildUrbanChinookSquadron(GameObject jaegerGo, GameObject otachiGo)
        {
            GameObject sqGo = new GameObject("Chinook_Patrol_Squadron");
            var sq = sqGo.AddComponent<ChinookCombatPatrolSquadron>();
            sq.urbanCanyonMode = true;
            sq.urbanMinClearanceY = 74f;
            sq.gipsyTarget = jaegerGo != null ? jaegerGo.transform : null;
            sq.kaijuTarget = otachiGo != null ? otachiGo.transform : null;
            sq.EnsureSquadronSpawned();
            EditorUtility.SetDirty(sqGo);
        }

        [MenuItem("Pacific Rim/Wire Otachi Cutscenes Only (Open Scene)", false, 13)]
        public static void WireOtachiCutscenesInOpenSceneMenu()
        {
            ConfigureFlyingOtachiFbxImporter();
            Material otachiMat = EnsureOtachiMaterial();
            Material oilTankerMat = EnsureOilTankerMaterial();
            RuntimeAnimatorController flyingCtrl = BuildFlyingOtachiController();

            DestroyNamedObjects("Otachi_IntroCutscene", "Otachi_OutroCutscene");
            GameObject jaegerGo = GameObject.Find("Jaeger_Mech");
            GameObject otachiGo = GameObject.Find("Kaiju_Otachi");
            WireOtachiCutscenesInScene(jaegerGo, otachiGo, oilTankerMat, otachiMat, flyingCtrl);

            Scene active = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=#00ffaa><b>[OtachiCutscenes]</b></color> Wired Intro & Outro cutscenes + OilTanker material + FlyingOtachi dual-layer controller in open scene.");
        }

        private static void ConfigureFlyingOtachiFbxImporter()
        {
            if (!File.Exists(FlyingOtachiFbxPath)) return;
            ModelImporter importer = AssetImporter.GetAtPath(FlyingOtachiFbxPath) as ModelImporter;
            if (importer == null) return;

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.useFileUnits = true;
            importer.isReadable = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;

            TakeInfo[] takeInfos = importer.importedTakeInfos;
            if (takeInfos != null && takeInfos.Length > 0)
            {
                var clips = new ModelImporterClipAnimation[takeInfos.Length];
                for (int i = 0; i < takeInfos.Length; i++)
                {
                    TakeInfo t = takeInfos[i];
                    float sr = t.sampleRate > 0f ? t.sampleRate : 30f;
                    float startT = t.stopTime > t.startTime ? t.startTime : t.bakeStartTime;
                    float stopT = t.stopTime > t.startTime ? t.stopTime : t.bakeStopTime;
                    string clipName = string.IsNullOrEmpty(t.defaultClipName) ? t.name : t.defaultClipName;

                    clips[i] = new ModelImporterClipAnimation
                    {
                        name = clipName,
                        takeName = t.name,
                        firstFrame = Mathf.Round(startT * sr),
                        lastFrame = Mathf.Round(stopT * sr),
                        loopTime = true,
                        loop = true,
                        keepOriginalOrientation = false,
                        keepOriginalPositionXZ = false,
                        keepOriginalPositionY = true
                    };
                }
                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();
            AssetDatabase.Refresh();
            Debug.Log($"<color=#00e5ff><b>[FlyingOtachi Import]</b></color> Configured '{FlyingOtachiFbxPath}' as Generic with looping Armature|Flying clip.");
        }

        private static Material EnsureOilTankerMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(OilTankerMatPath);
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null)
            {
                mat = new Material(litShader);
                AssetDatabase.CreateAsset(mat, OilTankerMatPath);
            }
            else if (litShader != null && mat.shader != litShader)
            {
                mat.shader = litShader;
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(OilTankerTexPath);
            if (tex != null)
            {
                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.36f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.62f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static AnimationClip FindFlyingClip()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(FlyingOtachiFbxPath);
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip &&
                    !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }
            }
            return null;
        }

        private static AvatarMask BuildFlyingOtachiWingMask()
        {
            if (!Directory.Exists("Assets/Animations"))
            {
                AssetDatabase.CreateFolder("Assets", "Animations");
            }
            if (AssetDatabase.LoadAssetAtPath<AvatarMask>(FlyingOtachiWingMaskPath) != null)
            {
                AssetDatabase.DeleteAsset(FlyingOtachiWingMaskPath);
            }

            AvatarMask mask = new AvatarMask();
            GameObject flyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FlyingOtachiFbxPath);
            if (flyAsset != null)
            {
                var paths = new List<string>();
                foreach (var t in flyAsset.GetComponentsInChildren<Transform>(true))
                {
                    if (t == flyAsset.transform) continue;
                    string p = t.name;
                    var cur = t.parent;
                    while (cur != null && cur != flyAsset.transform)
                    {
                        p = cur.name + "/" + p;
                        cur = cur.parent;
                    }
                    paths.Add(p);
                }

                mask.transformCount = paths.Count;
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    mask.SetTransformPath(i, path);
                    // Enable ONLY the wing arm & finger branches so BaseOtachi animations drive body/head/legs/tail
                    // while Armature|Flying drives UpperArm, LowerArm, and wing fingers cleanly!
                    bool isWingBranch = path.Contains("/Shoulder.L") || path.Contains("/Shoulder.R") ||
                                        path.Contains("/UpperArm.") || path.Contains("/LowerArm.") ||
                                        path.Contains("/Finger.");
                    mask.SetTransformActive(i, isWingBranch);
                }
            }

            AssetDatabase.CreateAsset(mask, FlyingOtachiWingMaskPath);
            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssets();
            return mask;
        }

        private static RuntimeAnimatorController BuildFlyingOtachiController()
        {
            if (!Directory.Exists("Assets/Animations"))
            {
                AssetDatabase.CreateFolder("Assets", "Animations");
            }
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FlyingOtachiControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(FlyingOtachiControllerPath);
            }

            AvatarMask wingMask = BuildFlyingOtachiWingMask();
            AnimationClip flyingClip = FindFlyingClip();

            var controller = AnimatorController.CreateAnimatorControllerAtPath(FlyingOtachiControllerPath);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "WingSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1.6f
            });

            // Layer 0: Base Otachi body/legs/tail/head animations from Otachi.fbx
            var baseSm = controller.layers[0].stateMachine;
            AnimatorState AddBaseState(string stateName, AnimationClip clip, Vector3 pos)
            {
                var st = baseSm.AddState(stateName, pos);
                st.motion = clip;
                st.speed = 1f;
                return st;
            }

            var idleSt = AddBaseState("Idle", FindClipInOtachiFbx("OtachiIdle") ?? flyingClip, new Vector3(200, 0, 0));
            AddBaseState("EMP", FindClipInOtachiFbx("OtachiEmote", "OtachiEMP") ?? flyingClip, new Vector3(200, 65, 0));
            AddBaseState("Slam", FindClipInOtachiFbx("OtachiSlam") ?? flyingClip, new Vector3(200, 130, 0));
            AddBaseState("DeathLoop", FindClipInOtachiFbx("OtachiDeathLoop") ?? flyingClip, new Vector3(200, 195, 0));
            AddBaseState("StaggerUp", FindClipInOtachiFbx("OtachiStaggerUp") ?? flyingClip, new Vector3(200, 260, 0));
            AddBaseState("FullFlying", flyingClip, new Vector3(200, 325, 0));
            baseSm.defaultState = idleSt;

            // Layer 1: Wing Flight Override masked exclusively onto UpperArm / LowerArm / Wing Fingers playing Armature|Flying!
            var wingSm = new AnimatorStateMachine { name = "WingFlightLayer", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(wingSm, controller);
            var wingState = wingSm.AddState("FlyingWings", new Vector3(220, 0, 0));
            wingState.motion = flyingClip;
            wingState.speed = 1.0f;
            wingState.speedParameterActive = true;
            wingState.speedParameter = "WingSpeed";
            wingSm.defaultState = wingState;

            var layers = controller.layers;
            var newLayers = new AnimatorControllerLayer[layers.Length + 1];
            Array.Copy(layers, newLayers, layers.Length);
            newLayers[layers.Length] = new AnimatorControllerLayer
            {
                name = "WingFlightOverride",
                stateMachine = wingSm,
                avatarMask = wingMask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1.0f
            };
            controller.layers = newLayers;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=#00ffaa><b>[FlyingOtachi Controller]</b></color> Created dual-layer '{FlyingOtachiControllerPath}' (Layer 0 = BaseOtachi body, Layer 1 = WingMask Armature|Flying).");
            return controller;
        }

        private static void WireOtachiCutscenesInScene(
            GameObject jaegerGo,
            GameObject otachiGo,
            Material oilTankerMat,
            Material otachiMat,
            RuntimeAnimatorController flyingOtachiCtrl)
        {
            GameObject oilTankerFbx = AssetDatabase.LoadAssetAtPath<GameObject>(OilTankerFbxPath);
            GameObject flyingOtachiFbx = AssetDatabase.LoadAssetAtPath<GameObject>(FlyingOtachiFbxPath);
            Material beamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_VolumetricBeam.mat");
            Mesh beamMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/PacificRim_VolumetricBeamCone.asset");

            // Intro Cutscene (~30s Oil Tanker Strike)
            GameObject introGo = new GameObject("Otachi_IntroCutscene");
            var intro = introGo.AddComponent<OtachiIntroCutscene>();
            intro.jaegerTransform = jaegerGo != null ? jaegerGo.transform : null;
            intro.otachiTransform = otachiGo != null ? otachiGo.transform : null;
            intro.oilTankerPrefab = oilTankerFbx;
            intro.oilTankerMaterial = oilTankerMat;
            intro.playOnStart = true;
            EditorUtility.SetDirty(introGo);

            // Outro Cutscene (~45s Stratosphere Drop & Chain Sword)
            GameObject outroGo = new GameObject("Otachi_OutroCutscene");
            var outro = outroGo.AddComponent<OtachiOutroCutscene>();
            outro.jaegerTransform = jaegerGo != null ? jaegerGo.transform : null;
            outro.otachiTransform = otachiGo != null ? otachiGo.transform : null;
            outro.flyingOtachiPrefab = flyingOtachiFbx;
            outro.flyingOtachiController = flyingOtachiCtrl;
            outro.otachiMaterial = otachiMat;
            outro.volumetricBeamMaterial = beamMat;
            outro.volumetricBeamConeMesh = beamMesh;
            EditorUtility.SetDirty(outroGo);
        }

        private static void SyncCampaignMenusAndBuildSettings()
        {
            // Rebuild Main Menu scene with updated 3-level order
            SetupPacificRimMainMenuScene.CreateAndSetupMainMenuScene();
            SetupPacificRimMainMenuScene.UpdateBuildSettings();

            // Refresh UI on Operation Pitfall (now OP 03) if present
            if (File.Exists(SetupPacificRimMainMenuScene.Level3ScenePath))
            {
                EditorSceneManager.OpenScene(SetupPacificRimMainMenuScene.Level3ScenePath, OpenSceneMode.Single);
                SetupPacificRimUIScene.SetupUI();
                var pitfallMenu = UnityEngine.Object.FindAnyObjectByType<MainMenuUI>();
                if (pitfallMenu != null)
                {
                    pitfallMenu.isMainMenuScene = false;
                    pitfallMenu.gameplaySceneName = MainMenuUI.Level3SceneName;
                    pitfallMenu.mainMenuSceneName = "MainMenuScene";
                    pitfallMenu.isMenuOpen = false;
                    pitfallMenu.SetMenuState(false);
                    EditorUtility.SetDirty(pitfallMenu.gameObject);
                }
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
        }

        private static bool ComputeCombinedSkinnedBounds(GameObject root, out Bounds combined)
        {
            combined = new Bounds();
            bool hasBounds = false;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                var baked = new UnityEngine.Mesh();
                smr.BakeMesh(baked, false);
                baked.RecalculateBounds();
                Bounds localB = baked.bounds;
                UnityEngine.Object.DestroyImmediate(baked);

                // BakeMesh(false) outputs vertices already scaled into world units relative to smr.transform
                Vector3 c = smr.transform.position + (smr.transform.rotation * localB.center);
                Bounds worldB = new Bounds(c, localB.size);
                if (!hasBounds)
                {
                    combined = worldB;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(worldB);
                }
            }
            return hasBounds;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void DestroyNamedObjects(params string[] names)
        {
            foreach (string n in names)
            {
                GameObject go;
                while ((go = GameObject.Find(n)) != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }
    }
}
#endif
