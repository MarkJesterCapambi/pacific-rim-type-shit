#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using PacificRimAtmosphere;
using PacificRimAudio.Editor;
using PacificRimUI;
using PacificRimUI.Editor;

namespace PacificRimWater.Editor
{
    public static class SetupCampaignLevelsAndSlattern
    {
        public const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        public const string Level1ScenePath = "Assets/Scenes/Save Striker Eureka.unity";
        public const string Level2ScenePath = "Assets/Scenes/Operation Pitfall.unity";
        public const string MainMenuScenePath = "Assets/Scenes/MainMenuScene.unity";

        private const string Leatherback1FbxPath = "Assets/Models/Leatherback 1.fbx";
        private const string LeatherbackControllerPath = "Assets/Animations/Leatherback_Showcase.controller";
        private const string SlatternFbxPath = "Assets/Models/Slattern.fbx";
        private const string SlatternControllerPath = "Assets/Animations/Slattern_Controller.controller";
        private const string SlatternUpperMaskPath = "Assets/Animations/Slattern_UpperBody.mask";
        private const string SlatternTexturePath = "Assets/Materials/Slattern_D.png";
        private const string SlatternEmissionPath = "Assets/Materials/Slattern_Emission.png";
        private const string SlatternMatPath = "Assets/Materials/PacificRim_Slattern.mat";
        private static readonly Color SlatternNeonHdr = new Color(0.25f, 3.4f, 4.4f, 1f);
        private const string SandySeabedMatPath = "Assets/Materials/PacificRim_SandySeabed.mat";
        private const string SandyDunesMeshPath = "Assets/Meshes/Underwater_SandyDunesRing.asset";
        private const string TrenchTerrainDataPath = "Assets/Settings/MarianaTrench_TerrainData.asset";
        private const string TrenchTextureFolder = "Assets/Textures/MarianaTrench";
        private const string TrenchSiltLayerPath = "Assets/Textures/MarianaTrench/Layer_AbyssalSilt.terrainlayer";
        private const string TrenchBasaltLayerPath = "Assets/Textures/MarianaTrench/Layer_VolcanicBasalt.terrainlayer";
        private const string TrenchTerrainMatPath = "Assets/Materials/PacificRim_TerrainLit.mat";
        private const string TrenchBasaltMatPath = "Assets/Materials/PacificRim_VolcanicBasalt.mat";
        private const string TrenchBoulderPrefabPath = "Assets/Prefabs/Abyssal_Basalt_Boulder.prefab";
        private const string AbyssalVolumeProfilePath = "Assets/Settings/PacificRim_AbyssalVolumeProfile.asset";
        private const string BreachCoreGradientPath = "Assets/Materials/Breach_CoreGradient.png";

        [MenuItem("Pacific Rim/Setup Full Campaign (Save Striker Eureka + Operation Pitfall + Main Menu)", false, 2)]
        public static void SetupFullCampaign()
        {
            // Save dirty scene changes if needed
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                EditorSceneManager.SaveOpenScenes();
            }

            // 1. Configure FBX importers & Animator Controllers + Materials
            ConfigureKaijuFbxImporter(
                Leatherback1FbxPath,
                loopTargets: new[] { "Idle", "Walk", "Run", "BlockLoop", "DeathLoop" });
            UpdateLeatherbackControllerWithStaggerDown();

            ConfigureKaijuFbxImporter(
                SlatternFbxPath,
                loopTargets: new[] { "Idle", "Walk", "Run", "DeathLoop" });
            Material slatternMat = EnsureSlatternMaterial();
            RuntimeAnimatorController slatternCtrl = BuildSlatternController();

            // 2. Bake Audio Bank (includes JaegerFootstep1-3)
            PacificRimAudioBankBaker.Bake();

            // 3. Rename SampleScene -> Save Striker Eureka (if SampleScene still exists) and configure Level 1
            EnsureLevel1SceneExists();
            ConfigureLevel1Scene();

            // 4. Create/Duplicate Operation Pitfall from Level 1 and configure underwater Slattern battle
            CreateAndConfigureLevel2Scene(slatternMat, slatternCtrl);

            // 5. Rebuild Main Menu scene with level selector & update Build Settings
            SetupPacificRimMainMenuScene.CreateAndSetupMainMenuScene();
            SetupPacificRimMainMenuScene.UpdateBuildSettings();

            // 6. Re-open MainMenuScene as the starting scene in Editor
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00ffaa><b>[Campaign Setup]</b></color> Full 2-level campaign setup completed: MainMenuScene, Save Striker Eureka, and Operation Pitfall!");
        }

        [MenuItem("Pacific Rim/Setup Animations Only (Leatherback StaggerDown + Slattern)", false, 3)]
        public static void SetupAnimationsOnly()
        {
            ConfigureKaijuFbxImporter(
                Leatherback1FbxPath,
                loopTargets: new[] { "Idle", "Walk", "Run", "BlockLoop", "DeathLoop" });
            UpdateLeatherbackControllerWithStaggerDown();

            ConfigureKaijuFbxImporter(
                SlatternFbxPath,
                loopTargets: new[] { "Idle", "Walk", "Run", "DeathLoop" });
            EnsureSlatternMaterial();
            BuildSlatternController();
            Debug.Log("<color=#00ffaa><b>[Campaign Setup]</b></color> Leatherback StaggerDown & Slattern animations configured.");
        }

        private static bool ConfigureKaijuFbxImporter(string fbxPath, string[] loopTargets)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[Kaiju Import] ModelImporter not found at '{fbxPath}'");
                return false;
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.useFileUnits = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;

            TakeInfo[] takeInfos = importer.importedTakeInfos;
            ModelImporterClipAnimation[] defaultClips = importer.defaultClipAnimations;

            bool hasTakes = takeInfos != null && takeInfos.Length > 0;
            bool hasDefaults = defaultClips != null && defaultClips.Length > 0;
            if (!hasTakes && !hasDefaults)
            {
                Debug.LogError($"[Kaiju Import] No animation takes found in '{fbxPath}'");
                return false;
            }

            var takeLookup = new Dictionary<string, TakeInfo>(StringComparer.OrdinalIgnoreCase);
            if (takeInfos != null)
            {
                foreach (var t in takeInfos)
                {
                    if (!string.IsNullOrEmpty(t.name) && !takeLookup.ContainsKey(t.name)) takeLookup[t.name] = t;
                    if (!string.IsNullOrEmpty(t.defaultClipName) && !takeLookup.ContainsKey(t.defaultClipName)) takeLookup[t.defaultClipName] = t;
                }
            }

            ModelImporterClipAnimation[] clips;
            if (hasTakes)
            {
                clips = new ModelImporterClipAnimation[takeInfos.Length];
                for (int i = 0; i < takeInfos.Length; i++)
                {
                    TakeInfo t = takeInfos[i];
                    float sr = t.sampleRate > 0f ? t.sampleRate : 30f;
                    float startT = t.stopTime > t.startTime ? t.startTime : t.bakeStartTime;
                    float stopT = t.stopTime > t.startTime ? t.stopTime : t.bakeStopTime;
                    clips[i] = new ModelImporterClipAnimation
                    {
                        name = string.IsNullOrEmpty(t.defaultClipName) ? t.name : t.defaultClipName,
                        takeName = t.name,
                        firstFrame = Mathf.Round(startT * sr),
                        lastFrame = Mathf.Round(stopT * sr)
                    };
                }
            }
            else
            {
                clips = defaultClips;
            }

            foreach (var c in clips)
            {
                if (c.lastFrame <= c.firstFrame && takeLookup.TryGetValue(c.takeName ?? c.name ?? "", out var t))
                {
                    float sr = t.sampleRate > 0f ? t.sampleRate : 30f;
                    float startT = t.stopTime > t.startTime ? t.startTime : t.bakeStartTime;
                    float stopT = t.stopTime > t.startTime ? t.stopTime : t.bakeStopTime;
                    c.firstFrame = Mathf.Round(startT * sr);
                    c.lastFrame = Mathf.Round(stopT * sr);
                }

                bool shouldLoop = loopTargets.Any(suffix =>
                    (c.name != null && c.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) ||
                    (c.takeName != null && c.takeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));

                c.loopTime = shouldLoop;
                c.loop = shouldLoop;
                c.keepOriginalOrientation = false;
                c.keepOriginalPositionXZ = false;
                c.keepOriginalPositionY = true;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            AssetDatabase.Refresh();
            Debug.Log($"<color=#00e5ff><b>[Kaiju Import]</b></color> Configured '{fbxPath}' with {clips.Length} clips.");
            return true;
        }

        private static AnimationClip FindClipInFbx(string fbxPath, string suffix)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                {
                    if (clip.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        return clip;
                }
            }
            return null;
        }

        private static void UpdateLeatherbackControllerWithStaggerDown()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(LeatherbackControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[Leatherback Controller] Missing controller at '{LeatherbackControllerPath}'");
                return;
            }

            AnimationClip staggerDownClip = FindClipInFbx(Leatherback1FbxPath, "LeatherbackStaggerDown");
            if (staggerDownClip == null)
            {
                Debug.LogError($"[Leatherback Controller] Could not find Armature|LeatherbackStaggerDown in '{Leatherback1FbxPath}'");
                return;
            }

            var sm = controller.layers[0].stateMachine;
            AnimatorState staggerDownState = null;
            foreach (var child in sm.states)
            {
                if (child.state.name == "StaggerDown")
                {
                    staggerDownState = child.state;
                    break;
                }
            }

            if (staggerDownState == null)
            {
                staggerDownState = sm.AddState("StaggerDown", new Vector3(600, 745, 0));
            }
            staggerDownState.motion = staggerDownClip;
            staggerDownState.speed = 1f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=#00ffaa><b>[Leatherback Controller]</b></color> StaggerDown wired to '{staggerDownClip.name}' ({staggerDownClip.length:F2}s).");
        }

        [MenuItem("Pacific Rim/Setup Slattern Cyan Emission", false, 4)]
        public static void GenerateAndApplySlatternEmission()
        {
            Material mat = EnsureSlatternMaterial();

            string prevScenePath = SceneManager.GetActiveScene().path;
            bool needOpenLevel2 = string.IsNullOrEmpty(prevScenePath) || prevScenePath != Level2ScenePath;

            if (needOpenLevel2 && File.Exists(Level2ScenePath))
            {
                if (EditorSceneManager.GetActiveScene().isDirty)
                {
                    EditorSceneManager.SaveOpenScenes();
                }
                EditorSceneManager.OpenScene(Level2ScenePath, OpenSceneMode.Single);
            }

            GameObject slatternGo = GameObject.Find("Kaiju_Slattern");
            if (slatternGo != null)
            {
                ConfigureSlatternBioluminescence(slatternGo, mat);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                Debug.Log("<color=#00FFFF><b>[Slattern Emission]</b></color> Applied cyan-only emission map & KaijuBioluminescenceController to Kaiju_Slattern in Operation Pitfall.");
            }
            else
            {
                Debug.LogWarning("[Slattern Emission] Kaiju_Slattern not found in active scene; material updated cleanly.");
            }

            if (needOpenLevel2 && !string.IsNullOrEmpty(prevScenePath) && File.Exists(prevScenePath))
            {
                EditorSceneManager.OpenScene(prevScenePath, OpenSceneMode.Single);
            }
        }

        private static Texture2D EnsureSlatternEmissionTexture()
        {
            EnsureFolder("Assets/Materials");
            if (!File.Exists(SlatternTexturePath))
            {
                Debug.LogWarning($"[Slattern Emission] Source diffuse texture not found at '{SlatternTexturePath}'");
                return null;
            }

            Texture2D src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(SlatternTexturePath));
            int w = src.width;
            int h = src.height;

            Color[] srcPixels = src.GetPixels();
            Color[] dstPixels = new Color[srcPixels.Length];
            int glowingPixels = 0;

            for (int i = 0; i < srcPixels.Length; i++)
            {
                Color c = srcPixels[i];
                float cyanDelta = Mathf.Min(c.g, c.b) - c.r;
                float cyanBright = 0.55f * c.b + 0.45f * c.g;
                float mask = 0f;

                if (cyanDelta > 0.08f && c.g > 0.45f && c.b > 0.55f)
                {
                    float tDelta = Mathf.InverseLerp(0.08f, 0.30f, cyanDelta);
                    float tBright = Mathf.InverseLerp(0.48f, 0.78f, cyanBright);
                    float coreBoost = (c.g > 0.82f && c.b > 0.88f) ? Mathf.InverseLerp(0.08f, 0.14f, cyanDelta) : 0f;
                    mask = Mathf.Clamp01(Mathf.Max(tDelta * tBright, coreBoost));
                    mask = Mathf.SmoothStep(0f, 1f, mask);
                }

                if (mask > 0.02f) glowingPixels++;
                dstPixels[i] = new Color(mask * 0.25f, mask * 0.92f, mask * 1.0f, 1f);
            }

            Texture2D emTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            emTex.SetPixels(dstPixels);
            emTex.Apply();
            File.WriteAllBytes(SlatternEmissionPath, emTex.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(src);
            UnityEngine.Object.DestroyImmediate(emTex);

            AssetDatabase.ImportAsset(SlatternEmissionPath, ImportAssetOptions.ForceUpdate);
            Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(SlatternEmissionPath);
            float pct = (100f * glowingPixels) / Mathf.Max(1, dstPixels.Length);
            Debug.Log($"<color=#00FFFF><b>[Slattern Emission]</b></color> Generated '{SlatternEmissionPath}' ({w}x{h}, {pct:F2}% glowing cyan pixels).");
            return imported;
        }

        private static void ConfigureSlatternBioluminescence(GameObject slattern, Material slatternMat)
        {
            var bioCtrl = GetOrAdd<KaijuBioluminescenceController>(slattern);
            bioCtrl.baseEmissionColor = SlatternNeonHdr;
            bioCtrl.minIntensity = 0.85f;
            bioCtrl.maxIntensity = 1.15f;
            bioCtrl.maxLightIntensity = 2.0f;
            bioCtrl.lightRange = 22f;
            bioCtrl.kaijuAI = slattern.GetComponent<KaijuAI>();

            var smr = slattern.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null)
            {
                bioCtrl.targetRenderer = smr;
                if (slatternMat != null)
                {
                    var mats = new Material[smr.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = slatternMat;
                    smr.sharedMaterials = mats;
                }
                EditorUtility.SetDirty(smr);
            }

            EditorUtility.SetDirty(bioCtrl);
            EditorUtility.SetDirty(slattern);
        }

        private static Material EnsureSlatternMaterial()
        {
            EnsureFolder("Assets/Materials");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(SlatternMatPath);
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(litShader);
                AssetDatabase.CreateAsset(mat, SlatternMatPath);
            }
            else if (litShader != null && mat.shader != litShader)
            {
                mat.shader = litShader;
            }

            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(SlatternTexturePath);
            if (diffuse != null)
            {
                mat.mainTexture = diffuse;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", diffuse);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", diffuse);
            }
            else
            {
                Debug.LogWarning($"[Slattern Material] Diffuse texture not found at '{SlatternTexturePath}'");
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.08f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.44f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 2f);

            Texture2D emissionTex = EnsureSlatternEmissionTexture();
            if (emissionTex != null)
            {
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", emissionTex);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", SlatternNeonHdr);
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static RuntimeAnimatorController BuildSlatternController()
        {
            EnsureFolder("Assets/Animations");
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SlatternControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(SlatternControllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(SlatternControllerPath);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "LocomotionSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1.0f
            });

            var sm = controller.layers[0].stateMachine;

            AnimationClip idleClip = FindClipInFbx(SlatternFbxPath, "SlatternIdle");
            AnimationClip walkClip = FindClipInFbx(SlatternFbxPath, "SlatternWalk");
            AnimationClip runClip = FindClipInFbx(SlatternFbxPath, "SlatternRun");
            AnimationClip punchLClip = FindClipInFbx(SlatternFbxPath, "SlatternPunchLeft");
            AnimationClip punchRClip = FindClipInFbx(SlatternFbxPath, "SlatternPunchRight");
            AnimationClip slamClip = FindClipInFbx(SlatternFbxPath, "SlatternSlam");
            AnimationClip emoteClip = FindClipInFbx(SlatternFbxPath, "SlatternEmote");
            AnimationClip blockEnterClip = FindClipInFbx(SlatternFbxPath, "SlatternBlockEnter");
            AnimationClip staggerLClip = FindClipInFbx(SlatternFbxPath, "SlatternStaggerLeft");
            AnimationClip staggerRClip = FindClipInFbx(SlatternFbxPath, "SlatternStaggerRight");
            AnimationClip staggerUClip = FindClipInFbx(SlatternFbxPath, "SlatternStaggerUp");
            AnimationClip staggerDClip = FindClipInFbx(SlatternFbxPath, "SlatternStaggerDown");
            AnimationClip deathClip = FindClipInFbx(SlatternFbxPath, "SlatternDeath");
            AnimationClip deathLoopClip = FindClipInFbx(SlatternFbxPath, "SlatternDeathLoop");

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
            AddState("BlockLoop", blockEnterClip, new Vector3(200, 520, 0));
            AddState("StaggerLeft", staggerLClip, new Vector3(200, 585, 0));
            AddState("StaggerRight", staggerRClip, new Vector3(200, 650, 0));
            AddState("StaggerUp", staggerUClip, new Vector3(200, 715, 0));
            AddState("StaggerDown", staggerDClip, new Vector3(200, 780, 0));
            var deathState = AddState("Death", deathClip, new Vector3(200, 845, 0));
            var deathLoopState = AddState("DeathLoop", deathLoopClip, new Vector3(200, 910, 0));

            sm.defaultState = idleState;

            if (deathState != null && deathLoopState != null)
            {
                var deathTr = deathState.AddTransition(deathLoopState);
                deathTr.hasExitTime = true;
                deathTr.exitTime = 0.95f;
                deathTr.duration = 0.15f;
            }

            // Note: Slattern has no separate BlockLoop clip; BlockEnter holds its final guard pose naturally.
            _ = blockEnterState;

            // Layer 1: UpperBodyGuard (used in Phase 3 Moving Guard while Layer 0 drives Walk/Run locomotion)
            AvatarMask upperMask = BuildSlatternUpperBodyMask();
            if (upperMask != null && blockEnterClip != null)
            {
                var guardSm = new AnimatorStateMachine
                {
                    name = "UpperBodyGuard",
                    hideFlags = HideFlags.HideInHierarchy
                };
                AssetDatabase.AddObjectToAsset(guardSm, controller);
                var guardHold = guardSm.AddState("GuardHold", new Vector3(200, 0, 0));
                guardHold.motion = blockEnterClip;
                guardHold.speed = 0.25f;
                guardSm.defaultState = guardHold;

                var guardLayer = new AnimatorControllerLayer
                {
                    name = "UpperBodyGuard",
                    defaultWeight = 0f,
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    avatarMask = upperMask,
                    stateMachine = guardSm
                };
                controller.AddLayer(guardLayer);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=#00ffaa><b>[Slattern Controller]</b></color> Created '{SlatternControllerPath}' with UpperBodyGuard Layer + all Slattern animation states.");
            return controller;
        }

        private static AvatarMask BuildSlatternUpperBodyMask()
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(SlatternFbxPath);
            if (fbx == null) return null;

            if (AssetDatabase.LoadAssetAtPath<AvatarMask>(SlatternUpperMaskPath) != null)
            {
                AssetDatabase.DeleteAsset(SlatternUpperMaskPath);
            }

            AvatarMask mask = new AvatarMask();
            mask.AddTransformPath(fbx.transform, true);
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                bool isUpper = path.Contains("UpperTorso");
                mask.SetTransformActive(i, isUpper);
            }

            AssetDatabase.CreateAsset(mask, SlatternUpperMaskPath);
            AssetDatabase.SaveAssets();
            return mask;
        }

        private static void EnsureLevel1SceneExists()
        {
            if (File.Exists(Level1ScenePath))
            {
                return;
            }

            if (File.Exists(SampleScenePath))
            {
                // Make sure MainMenuScene or an empty scene is opened if SampleScene is currently open before moving
                if (SceneManager.GetActiveScene().path == SampleScenePath)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }

                string moveError = AssetDatabase.MoveAsset(SampleScenePath, Level1ScenePath);
                if (!string.IsNullOrEmpty(moveError))
                {
                    Debug.LogWarning($"[Campaign Setup] MoveAsset failed ({moveError}), falling back to CopyAsset.");
                    AssetDatabase.CopyAsset(SampleScenePath, Level1ScenePath);
                }
                AssetDatabase.Refresh();
                Debug.Log($"<color=#00e5ff><b>[Campaign Setup]</b></color> Renamed '{SampleScenePath}' -> '{Level1ScenePath}'.");
            }
            else
            {
                Debug.LogError($"[Campaign Setup] Neither '{Level1ScenePath}' nor '{SampleScenePath}' was found!");
            }
        }

        private static void ConfigureLevel1Scene()
        {
            if (!File.Exists(Level1ScenePath)) return;

            EditorSceneManager.OpenScene(Level1ScenePath, OpenSceneMode.Single);

            var leatherback = GameObject.Find("Kaiju_Leatherback");
            if (leatherback != null)
            {
                var kaijuAi = leatherback.GetComponent<KaijuAI>();
                if (kaijuAi != null)
                {
                    kaijuAi.kaijuDisplayName = "LEATHERBACK";
                    kaijuAi.kaijuCategoryLabel = "CATEGORY IV KAIJU";
                    kaijuAi.isSlattern = false;
                    kaijuAi.groundYOffset = 2.0f;
                    EditorUtility.SetDirty(kaijuAi);
                }

                var anim = leatherback.GetComponentInChildren<Animator>();
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LeatherbackControllerPath);
                if (anim != null && ctrl != null)
                {
                    anim.runtimeAnimatorController = ctrl;
                    EditorUtility.SetDirty(anim);
                }
            }

            SetupPacificRimUIScene.SetupUI();
            var mainMenu = UnityEngine.Object.FindAnyObjectByType<MainMenuUI>();
            if (mainMenu != null)
            {
                mainMenu.isMainMenuScene = false;
                mainMenu.gameplaySceneName = MainMenuUI.Level1SceneName;
                mainMenu.mainMenuSceneName = "MainMenuScene";
                mainMenu.isMenuOpen = false;
                mainMenu.SetMenuState(false);
                EditorUtility.SetDirty(mainMenu.gameObject);
            }

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"<color=#00ffaa><b>[Level 1]</b></color> Configured and saved '{Level1ScenePath}'.");
        }

        private static void CreateAndConfigureLevel2Scene(Material slatternMat, RuntimeAnimatorController slatternCtrl)
        {
            if (!File.Exists(Level1ScenePath))
            {
                Debug.LogError($"[Level 2] Missing base scene '{Level1ScenePath}' to duplicate.");
                return;
            }

            if (SceneManager.GetActiveScene().path == Level2ScenePath)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            if (File.Exists(Level2ScenePath))
            {
                AssetDatabase.DeleteAsset(Level2ScenePath);
            }

            if (!AssetDatabase.CopyAsset(Level1ScenePath, Level2ScenePath))
            {
                Debug.LogError($"[Level 2] Failed to copy '{Level1ScenePath}' to '{Level2ScenePath}'.");
                return;
            }
            AssetDatabase.Refresh();

            EditorSceneManager.OpenScene(Level2ScenePath, OpenSceneMode.Single);

            // 1. Destroy non-underwater assets: ocean simulation, floating debris, Chinooks, cutscene, storm weather, Striker Eureka
            DestroyAllNamed(
                "ProceduralOcean",
                "Floating_Debris_Field",
                "Chinook_Patrol_Squadron",
                "PacificRim_DeploymentCutscene",
                "StormWeatherSystem",
                "ScreenSpaceRainLensQuad",
                "AnamorphicHorizontalFlareQuad",
                "striker");

            // Soft deep abyssal ocean scatter at 11,000m depth (dark & moody yet readable)
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l != null && l.type == LightType.Directional && l.gameObject.name != "LightningFlashLight")
                {
                    l.color = new Color(0.24f, 0.54f, 0.75f, 1f);
                    l.intensity = 0.55f;
                    l.shadowStrength = 0.45f;
                    l.enabled = true;
                    EditorUtility.SetDirty(l.gameObject);
                }
            }

            // Remove CameraLensRainController from Main Camera and keep AudioLowPassFilter ready
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camGo = GameObject.FindWithTag("MainCamera");
                if (camGo != null) mainCam = camGo.GetComponent<Camera>();
            }
            if (mainCam != null)
            {
                var lensRain = mainCam.GetComponent<CameraLensRainController>();
                if (lensRain != null) UnityEngine.Object.DestroyImmediate(lensRain);

                var lowPass = GetOrAdd<AudioLowPassFilter>(mainCam.gameObject);
                lowPass.cutoffFrequency = 3000f;
                lowPass.lowpassResonanceQ = 1.0f;
                EditorUtility.SetDirty(mainCam.gameObject);
            }

            // 2. Configure Atmosphere_Systems for underwater audio muffling & disable storm overrides
            var atmoSystems = GameObject.Find("Atmosphere_Systems");
            if (atmoSystems != null)
            {
                var volCtrl = atmoSystems.GetComponent<AtmosphereVolumeController>();
                if (volCtrl != null) UnityEngine.Object.DestroyImmediate(volCtrl);

                var atmoAudio = atmoSystems.GetComponent<AtmosphereAudioController>();
                if (atmoAudio != null)
                {
                    atmoAudio.isDeepUnderwater = true;
                    atmoAudio.deepUnderwaterCutoff = 3000f;
                    atmoAudio.enableDynamicLowPass = true;
                    atmoAudio.cameraLowPassFilter = mainCam != null ? mainCam.GetComponent<AudioLowPassFilter>() : null;
                    EditorUtility.SetDirty(atmoAudio);
                }
            }

            // 3. Configure WaterSplashManager for deep underwater silt & rising bubbles
            var splashMgr = UnityEngine.Object.FindAnyObjectByType<WaterSplashManager>();
            if (splashMgr != null)
            {
                splashMgr.isDeepUnderwaterMode = true;
                EditorUtility.SetDirty(splashMgr);
            }

            // 4. Configure Jaeger_Mech position & measure height for 1.15x Slattern scaling
            GameObject jaegerGo = GameObject.Find("Jaeger_Mech");
            float jaegerHeight = 24.56f;
            if (jaegerGo != null)
            {
                var jController = jaegerGo.GetComponent<SimpleJaegerController>();
                if (jController != null) jController.isCutsceneActive = false;

                if (ComputeCombinedSkinnedBounds(jaegerGo, out Bounds jBounds) && jBounds.size.y > 5f)
                {
                    jaegerHeight = jBounds.size.y;
                }

                // Clear any WaveBodyInteraction crest bubbles
                var waveBody = jaegerGo.GetComponent<JaegerWaveBodyInteraction>();
                if (waveBody != null)
                {
                    waveBody.enableBodySplashes = false;
                    waveBody.enableCrestBubbles = false;
                }
                EditorUtility.SetDirty(jaegerGo);
            }

            // 5. Configure Mariana Trench Canyon Seabed (Unity Terrain 1000x1000x250 + 30-deg slope splatmap + Prefab Boulders + Vents)
            Material sandyMat = EnsureSandySeabedMaterial();
            GameObject platform = GameObject.Find("Test_Platform");
            if (platform != null)
            {
                platform.transform.position = new Vector3(0f, -0.5f, 0f);
                platform.transform.localScale = new Vector3(1000f, 1f, 1000f);
                var platRend = platform.GetComponent<Renderer>();
                if (platRend != null)
                {
                    if (sandyMat != null) platRend.sharedMaterial = sandyMat;
                    platRend.enabled = false;
                }
                var platCol = platform.GetComponent<Collider>();
                if (platCol != null) platCol.enabled = false; // Mariana_Trench_Terrain TerrainCollider handles ground physics
                EditorUtility.SetDirty(platform);
            }
            Terrain trenchTerrain = BuildMarianaTrenchUnityTerrain();
            BuildAbyssalBasaltBoulders(trenchTerrain);
            BuildHydrothermalVents(trenchTerrain);
            BuildKaijuBloodPools(trenchTerrain);

            // 6. Replace Kaiju_Leatherback with Kaiju_Slattern (1.15x Gipsy height)
            DestroyAllNamed("Kaiju_Leatherback", "Kaiju_Slattern");
            GameObject slatternGo = CreateSlatternBoss(jaegerGo, jaegerHeight, slatternMat, slatternCtrl);

            // 7. Build UnderwaterAtmosphereSystem, THE BREACH Focal Landmark & Volumetric Floodlights
            UnderwaterEnvironmentController underwaterCtrl = BuildUnderwaterAtmosphereSystem(
                jaegerGo != null ? jaegerGo.transform : null, mainCam);
            BuildTheBreachLandmark(underwaterCtrl, sandyMat);
            BoostJaegerUnderwaterFloodlights(jaegerGo);

            // 8. Rebuild & wire UI (HUD + Pause Menu)
            SetupPacificRimUIScene.SetupUI();
            var hud = UnityEngine.Object.FindAnyObjectByType<JaegerHUD>();
            if (hud != null && slatternGo != null)
            {
                SetupPacificRimUIScene.SetSerializedField(hud, "kaiju", slatternGo.GetComponent<KaijuAI>());
            }

            var mainMenu = UnityEngine.Object.FindAnyObjectByType<MainMenuUI>();
            if (mainMenu != null)
            {
                mainMenu.isMainMenuScene = false;
                mainMenu.gameplaySceneName = MainMenuUI.Level2SceneName;
                mainMenu.mainMenuSceneName = "MainMenuScene";
                mainMenu.isMenuOpen = false;
                mainMenu.SetMenuState(false);
                EditorUtility.SetDirty(mainMenu.gameObject);
            }

            // 9. Update Arena Barriers to enclose Slattern cleanly
            SetupArenaBarriers.CreateArenaBarriers();

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"<color=#00ffaa><b>[Level 2]</b></color> Built and saved '{Level2ScenePath}' (Slattern height={jaegerHeight * 1.15f:F2}m, 1.15x Jaeger={jaegerHeight:F2}m).");
        }

        private static GameObject CreateSlatternBoss(GameObject jaegerGo, float jaegerHeight, Material slatternMat, RuntimeAnimatorController slatternCtrl)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(SlatternFbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[Slattern] FBX not found at '{SlatternFbxPath}'");
                return null;
            }

            GameObject slattern = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            slattern.name = "Kaiju_Slattern";
            slattern.transform.position = new Vector3(0f, 0f, 82f);
            slattern.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            slattern.transform.localScale = Vector3.one;

            // Assign Slattern material and disable offscreen culling
            foreach (var rend in slattern.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[rend.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = slatternMat;
                rend.sharedMaterials = mats;

                if (rend is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true;
                }
            }

            // Assign Avatar & Animator Controller
            Avatar avatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(SlatternFbxPath))
            {
                if (asset is Avatar av)
                {
                    avatar = av;
                    break;
                }
            }

            var anim = GetOrAdd<Animator>(slattern);
            anim.avatar = avatar;
            anim.runtimeAnimatorController = slatternCtrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Rebind();

            // Sample Idle pose to measure exact native height & calculate 1.15x Jaeger scale
            AnimationClip idleClip = FindClipInFbx(SlatternFbxPath, "SlatternIdle");
            if (idleClip != null)
            {
                idleClip.SampleAnimation(slattern, 0f);
            }

            float nativeHeight = 1f;
            if (ComputeCombinedSkinnedBounds(slattern, out Bounds nativeBounds) && nativeBounds.size.y > 0.01f)
            {
                nativeHeight = nativeBounds.size.y;
            }

            float targetHeight = jaegerHeight * 1.15f;
            float scaleFactor = targetHeight / Mathf.Max(0.01f, nativeHeight);
            slattern.transform.localScale = Vector3.one * scaleFactor;

            // Check orientation: compare head vs tail Z in world space when rotation is (0, 180, 0)
            Transform headBone = FindChildRecursive(slattern.transform, "UpperHead");
            Transform tailBone = FindChildRecursive(slattern.transform, "TailBone.004");
            if (headBone != null && tailBone != null)
            {
                Debug.Log($"<color=#00e5ff>[Slattern Orientation Check]</color> Head.z={headBone.position.z:F2}, Tail.z={tailBone.position.z:F2}, Forward={slattern.transform.forward}");
            }

            // Re-sample Idle after scaling so groundYOffset places feet flush on seabed (Y = 0)
            if (idleClip != null)
            {
                idleClip.SampleAnimation(slattern, 0f);
            }
            float computedGroundOffset = 0f;
            if (ComputeCombinedSkinnedBounds(slattern, out Bounds scaledBounds))
            {
                computedGroundOffset = Mathf.Max(0f, slattern.transform.position.y - scaledBounds.min.y);
                Debug.Log($"<color=#00ffaa><b>[Slattern Scale]</b></color> NativeH={nativeHeight:F3}m, TargetH={targetHeight:F2}m (1.15x Jaeger {jaegerHeight:F2}m), ScaleFactor={scaleFactor:F3}, ScaledSize={scaledBounds.size:F2}, GroundYOffset={computedGroundOffset:F3}m");
            }
            slattern.transform.position = new Vector3(0f, computedGroundOffset, 82f);

            // Configure CapsuleCollider in local space so world bounds enclose Slattern's torso & limbs
            var col = GetOrAdd<CapsuleCollider>(slattern);
            col.direction = 1;
            float invScale = 1f / Mathf.Max(0.001f, scaleFactor);
            col.height = targetHeight * invScale;
            col.radius = 7.2f * invScale;
            col.center = new Vector3(0f, (targetHeight * 0.5f - computedGroundOffset) * invScale, 0f);
            col.isTrigger = false;

            PhysicsMaterial physMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Materials/Jaeger_Physics.physicMaterial");
            if (physMat != null) col.sharedMaterial = physMat;

            // Configure Rigidbody
            var rb = GetOrAdd<Rigidbody>(slattern);
            rb.mass = 3200f;
            rb.linearDamping = 3f;
            rb.angularDamping = 5f;
            rb.useGravity = false;
            rb.freezeRotation = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Configure ClipGuard
            var clipGuard = GetOrAdd<JaegerSplashClipGuard>(slattern);
            clipGuard.AutoBuild();

            // Configure KaijuAI (Category V Slattern)
            var ai = GetOrAdd<KaijuAI>(slattern);
            ai.kaijuDisplayName = "SLATTERN";
            ai.kaijuCategoryLabel = "CATEGORY V KAIJU";
            ai.isSlattern = true;
            ai.groundYOffset = computedGroundOffset;
            ai.targetJaeger = jaegerGo != null ? jaegerGo.GetComponent<SimpleJaegerController>() : null;
            ai.animator = anim;
            ai.maxHealth = 5200f;
            ai.currentHealth = 5200f;
            ai.maxPoise = 165f;
            ai.currentPoise = 165f;
            ai.poiseRegenRate = 20f;
            ai.poiseRegenDelay = 2.4f;
            ai.canSprint = true;
            ai.walkSpeed = 10.5f;
            ai.sprintSpeed = 17.5f;
            ai.empWalkSpeed = 12.2f;
            ai.empSprintSpeed = 19.0f;
            ai.backwardWalkSpeed = 9.5f;
            ai.backwardSprintSpeed = 17.2f;
            ai.strafeSpeed = 7.4f;
            ai.slatternPhase1ParryChance = 0.10f;
            ai.slatternPhase2ParryChance = 0.42f;
            ai.slatternPhase3ParryChance = 0.10f;
            ai.slatternStalkMinDist = 33f;
            ai.slatternStalkIdealDist = 44f;
            ai.turnSpeed = 95f;
            ai.attackTurnSpeed = 38f;
            ai.acceleration = 4f;
            ai.minCombatDistance = 17.5f;
            ai.meleeAttackRange = 24.5f;
            ai.sprintMinRange = 26f;
            ai.lightPunchDamage = 32f;
            ai.heavySlamDamage = 60f;
            ai.punchStrikeRadius = 15f;
            ai.slamStrikeRadius = 17.5f;
            ai.empBuffDuration = 16f;
            ai.empCooldownDuration = 22f;
            ai.initialEmpDelay = 5.5f;
            ai.isCutsceneMode = false;

            ConfigureSlatternBioluminescence(slattern, slatternMat);

            EditorUtility.SetDirty(slattern);
            return slattern;
        }

        private static Material EnsureSandySeabedMaterial()
        {
            EnsureFolder("Assets/Materials");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(SandySeabedMatPath);
            Shader abyssShader = Shader.Find("PacificRim/AbyssalSeabed")
                              ?? Shader.Find("Universal Render Pipeline/Lit")
                              ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(abyssShader);
                AssetDatabase.CreateAsset(mat, SandySeabedMatPath);
            }
            else if (abyssShader != null && mat.shader != abyssShader)
            {
                mat.shader = abyssShader;
            }

            Texture2D sandDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SUIMONO - WATER SYSTEM 2/_DEMO/TERRAIN/textures_terrain/tex_sandwet_diff.psd");
            Texture2D sandNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SUIMONO - WATER SYSTEM 2/_DEMO/TERRAIN/textures_terrain/tex_sandwet_norm.psd");
            Texture2D rockDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SUIMONO - WATER SYSTEM 2/_DEMO/TERRAIN/textures_terrain/tex_rock_diff.psd");
            Texture2D rockNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SUIMONO - WATER SYSTEM 2/_DEMO/TERRAIN/textures_terrain/tex_rock_norm.psd");

            if (sandDiff != null && mat.HasProperty("_SandMap")) mat.SetTexture("_SandMap", sandDiff);
            if (sandNorm != null && mat.HasProperty("_SandNormalMap")) mat.SetTexture("_SandNormalMap", sandNorm);
            if (rockDiff != null && mat.HasProperty("_RockMap")) mat.SetTexture("_RockMap", rockDiff);
            if (rockNorm != null && mat.HasProperty("_RockNormalMap")) mat.SetTexture("_RockNormalMap", rockNorm);

            if (mat.HasProperty("_SandScale")) mat.SetFloat("_SandScale", 0.08f);
            if (mat.HasProperty("_RockScale")) mat.SetFloat("_RockScale", 0.045f);
            if (mat.HasProperty("_SandColor")) mat.SetColor("_SandColor", new Color(0.11f, 0.13f, 0.15f, 1f));
            if (mat.HasProperty("_RockColor")) mat.SetColor("_RockColor", new Color(0.06f, 0.07f, 0.09f, 1f));
            if (mat.HasProperty("_SlopeThreshold")) mat.SetFloat("_SlopeThreshold", 0.16f);
            if (mat.HasProperty("_SlopeSharpness")) mat.SetFloat("_SlopeSharpness", 5.5f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.34f);
            if (mat.HasProperty("_FissureColorA")) mat.SetColor("_FissureColorA", new Color(4.8f, 1.45f, 0.15f, 1f));
            if (mat.HasProperty("_FissureColorB")) mat.SetColor("_FissureColorB", new Color(3.8f, 3.2f, 0.35f, 1f));
            if (mat.HasProperty("_BreachCenterWS")) mat.SetVector("_BreachCenterWS", new Vector4(0f, 0f, 112f, 82f));

            // Fallback compatibility if URP Lit is ever used
            if (sandDiff != null) mat.mainTexture = sandDiff;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.12f, 0.14f, 0.16f, 1f));

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Mesh SaveOrUpdateMeshAsset(Mesh mesh, string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir)) EnsureFolder(dir);
            if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        private static float Fbm2D(float x, float z, int octaves = 4)
        {
            float sum = 0f;
            float amp = 0.5f;
            float freq = 1f;
            float norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * freq + 17.3f * (i + 1), z * freq + 31.7f * (i + 1)) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.05f;
            }
            return sum / Mathf.Max(0.0001f, norm);
        }

        private static float RidgedMultifractal2D(float x, float z, int octaves = 4)
        {
            float sum = 0f;
            float amp = 0.5f;
            float freq = 1f;
            float weight = 1f;
            float norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float n = Mathf.PerlinNoise(x * freq + 71.1f + i * 19.3f, z * freq + 43.9f + i * 23.7f);
                float signal = 1f - Mathf.Abs(2f * n - 1f);
                signal = signal * signal * weight;
                weight = Mathf.Clamp01(signal * 1.85f);
                sum += signal * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.2f; // High lacunarity ~2.2 for fractured volcanic ridges
            }
            return sum / Mathf.Max(0.0001f, norm);
        }

        private static Vector2 Hash22(float ix, float iz)
        {
            float dx = Mathf.Sin(ix * 127.1f + iz * 311.7f) * 43758.5453f;
            float dz = Mathf.Sin(ix * 269.5f + iz * 183.3f) * 43758.5453f;
            return new Vector2(dx - Mathf.Floor(dx), dz - Mathf.Floor(dz));
        }

        private static float VoronoiEdge2D(float x, float z)
        {
            float nx = Mathf.Floor(x);
            float nz = Mathf.Floor(z);
            float fx = x - nx;
            float fz = z - nz;
            float md1 = 8f;
            float md2 = 8f;

            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    Vector2 h = Hash22(nx + i, nz + j);
                    float rx = i + h.x - fx;
                    float rz = j + h.y - fz;
                    float d = rx * rx + rz * rz;
                    if (d < md1)
                    {
                        md2 = md1;
                        md1 = d;
                    }
                    else if (d < md2)
                    {
                        md2 = d;
                    }
                }
            }
            return Mathf.Sqrt(md2) - Mathf.Sqrt(md1);
        }

        private static float TileableFbm(float u, float v, float scale, float offsetSeed, int octaves = 4)
        {
            float sum = 0f;
            float amp = 0.5f;
            float norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                float period = Mathf.Max(1f, scale * (1 << o));
                float px = u * period;
                float pz = v * period;
                float n00 = Mathf.PerlinNoise(px + offsetSeed + o * 17.3f, pz + offsetSeed + o * 31.1f);
                float n10 = Mathf.PerlinNoise((px - period) + offsetSeed + o * 17.3f, pz + offsetSeed + o * 31.1f);
                float n01 = Mathf.PerlinNoise(px + offsetSeed + o * 17.3f, (pz - period) + offsetSeed + o * 31.1f);
                float n11 = Mathf.PerlinNoise((px - period) + offsetSeed + o * 17.3f, (pz - period) + offsetSeed + o * 31.1f);
                float tx = Mathf.SmoothStep(0f, 1f, u);
                float tz = Mathf.SmoothStep(0f, 1f, v);
                float nx0 = Mathf.Lerp(n00, n10, tx);
                float nx1 = Mathf.Lerp(n01, n11, tx);
                sum += Mathf.Lerp(nx0, nx1, tz) * amp;
                norm += amp;
                amp *= 0.5f;
            }
            return sum / Mathf.Max(0.0001f, norm);
        }

        private static Texture2D SavePngTextureAsset(Texture2D tex, string assetPath, bool isNormalMap)
        {
            EnsureFolder(TrenchTextureFolder);
            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.Combine(projectRoot, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, png);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.textureType = isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Texture2D BuildHeightToNormalMap(float[,] heightField, float normalStrength)
        {
            int size = heightField.GetLength(0);
            Texture2D normalTex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            Color[] cols = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                int yPrev = (y - 1 + size) % size;
                int yNext = (y + 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int xPrev = (x - 1 + size) % size;
                    int xNext = (x + 1) % size;

                    float dhdx = (heightField[xNext, y] - heightField[xPrev, y]) * normalStrength;
                    float dhdy = (heightField[x, yNext] - heightField[x, yPrev]) * normalStrength;
                    Vector3 n = new Vector3(-dhdx, -dhdy, 1f).normalized;
                    cols[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            normalTex.SetPixels(cols);
            normalTex.Apply(false, false);
            return normalTex;
        }

        private static void EnsureMarianaTerrainTextures(
            out Texture2D siltAlbedo,
            out Texture2D siltNormal,
            out Texture2D basaltAlbedo,
            out Texture2D basaltNormal)
        {
            const int res = 256;
            Color siltBase = new Color(30f / 255f, 34f / 255f, 38f / 255f, 1f);     // #1E2226 abyssal silt/sand
            Color basaltBase = new Color(20f / 255f, 24f / 255f, 29f / 255f, 1f);   // #14181D dark wet volcanic basalt

            Texture2D siltAlbedoTex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            Texture2D basaltAlbedoTex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            float[,] siltHeight = new float[res, res];
            float[,] basaltHeight = new float[res, res];

            for (int y = 0; y < res; y++)
            {
                float v = (float)y / res;
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / res;

                    // Abyssal silt: directional sine + fBM current ripples + fine dark-grey grain
                    float fbmLow = TileableFbm(u, v, 4f, 11.7f, 4);
                    float fbmFine = TileableFbm(u, v, 16f, 43.3f, 3);
                    float directionalRipple = Mathf.Sin((u * 12f + v * 3f) * Mathf.PI * 2f + fbmLow * 4.5f) * 0.5f + 0.5f;
                    siltHeight[x, y] = directionalRipple * 0.62f + fbmFine * 0.38f;

                    float siltShade = Mathf.Lerp(0.84f, 1.18f, fbmFine * 0.65f + directionalRipple * 0.35f);
                    Color sc = siltBase * siltShade;
                    sc.a = 1f;
                    siltAlbedoTex.SetPixel(x, y, sc);

                    // Striated volcanic basalt: ridged fractures + micro pitting
                    float ridge = 1f - Mathf.Abs(2f * TileableFbm(u, v, 6f, 77.1f, 4) - 1f);
                    float stria = Mathf.Sin((u * 5f - v * 11f) * Mathf.PI * 2f + ridge * 5.0f) * 0.5f + 0.5f;
                    float micro = TileableFbm(u, v, 20f, 93.4f, 3);
                    basaltHeight[x, y] = ridge * 0.50f + stria * 0.32f + micro * 0.18f;

                    float basaltShade = Mathf.Lerp(0.72f, 1.24f, ridge * 0.55f + micro * 0.45f);
                    Color bc = basaltBase * basaltShade;
                    bc.a = 1f;
                    basaltAlbedoTex.SetPixel(x, y, bc);
                }
            }

            siltAlbedoTex.Apply(false, false);
            basaltAlbedoTex.Apply(false, false);

            siltAlbedo = SavePngTextureAsset(siltAlbedoTex, $"{TrenchTextureFolder}/AbyssalSilt_Albedo.png", false);
            siltNormal = SavePngTextureAsset(BuildHeightToNormalMap(siltHeight, 2.8f), $"{TrenchTextureFolder}/AbyssalSilt_RippleNormal.png", true);
            basaltAlbedo = SavePngTextureAsset(basaltAlbedoTex, $"{TrenchTextureFolder}/VolcanicBasalt_Albedo.png", false);
            basaltNormal = SavePngTextureAsset(BuildHeightToNormalMap(basaltHeight, 4.4f), $"{TrenchTextureFolder}/VolcanicBasalt_Normal.png", true);
        }

        private static TerrainLayer LoadOrCreateTerrainLayer(
            string path, Texture2D albedo, Texture2D normal, float normalScale, Vector2 tileSize, float smoothness, float metallic)
        {
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = albedo;
            layer.normalMapTexture = normal;
            layer.normalScale = normalScale;
            layer.tileSize = tileSize;
            layer.tileOffset = Vector2.zero;
            layer.smoothness = smoothness;
            layer.metallic = metallic;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static Material EnsureTerrainLitMaterial()
        {
            Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit")
                ?? Shader.Find("Nature/Terrain/Standard")
                ?? Shader.Find("Universal Render Pipeline/Lit");
            return EnsureWeatherMaterial(TrenchTerrainMatPath, terrainShader, m =>
            {
                if (m.HasProperty("_EnableInstancedPerPixelNormal"))
                    m.SetFloat("_EnableInstancedPerPixelNormal", 1f);
            });
        }

        private static Material EnsureVolcanicBasaltMaterial(Texture2D basaltNormal)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return EnsureWeatherMaterial(TrenchBasaltMatPath, urpLit, m =>
            {
                Color darkWetBasalt = new Color(20f / 255f, 24f / 255f, 29f / 255f, 1f); // #14181D
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", darkWetBasalt);
                if (m.HasProperty("_Color")) m.SetColor("_Color", darkWetBasalt);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.65f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.08f);
                if (basaltNormal != null && m.HasProperty("_BumpMap"))
                {
                    m.SetTexture("_BumpMap", basaltNormal);
                    m.SetFloat("_BumpScale", 1.15f);
                    m.EnableKeyword("_NORMALMAP");
                }
            });
        }

        private static float SmoothstepRange(float edge0, float edge1, float x)
        {
            if (Mathf.Approximately(edge0, edge1)) return x < edge0 ? 0f : 1f;
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static Terrain BuildMarianaTrenchUnityTerrain()
        {
            DestroyAllNamed("Underwater_SandyTerrain_Dunes", "Mariana_Trench_Terrain", "Mariana_CrustalFissures");
            EnsureFolder("Assets/Settings");
            EnsureFolder(TrenchTextureFolder);

            EnsureMarianaTerrainTextures(
                out Texture2D siltAlbedo,
                out Texture2D siltNormal,
                out Texture2D basaltAlbedo,
                out Texture2D basaltNormal);

            // Slope < 30°: Fine dark-grey abyssal silt/sand (#1E2226), low smoothness 0.15
            TerrainLayer siltLayer = LoadOrCreateTerrainLayer(
                TrenchSiltLayerPath, siltAlbedo, siltNormal, 0.90f, new Vector2(22f, 22f), 0.15f, 0.0f);
            // Slope > 30°: Dark, wet, striated volcanic basalt (#14181D), high smoothness 0.65
            TerrainLayer basaltLayer = LoadOrCreateTerrainLayer(
                TrenchBasaltLayerPath, basaltAlbedo, basaltNormal, 1.35f, new Vector2(28f, 28f), 0.65f, 0.08f);

            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TrenchTerrainDataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TrenchTerrainDataPath);
            }

            const int hmRes = 513;
            const int alphaRes = 512;
            const float worldSpan = 1000f;
            const float maxHeight = 250f;
            const float baseFloorOffset = 4f; // Terrain sits at Y=-4 so flat combat floor is right at world Y=0

            data.heightmapResolution = hmRes;
            data.alphamapResolution = alphaRes;
            data.baseMapResolution = 512;
            data.size = new Vector3(worldSpan, maxHeight, worldSpan);
            data.terrainLayers = new[] { siltLayer, basaltLayer };

            float[,] heights = new float[hmRes, hmRes];
            float half = worldSpan * 0.5f;

            for (int z = 0; z < hmRes; z++)
            {
                float pz = -half + ((float)z / (hmRes - 1)) * worldSpan;
                for (int x = 0; x < hmRes; x++)
                {
                    float px = -half + ((float)x / (hmRes - 1)) * worldSpan;

                    // Wide, open abyssal seafloor basin (~300m wide across arena, closing behind Breach caldera at Z > 104)
                    float openBasinRadiusX = 148f + (Fbm2D(pz * 0.009f, 3.7f, 3) - 0.5f) * 38f;
                    float sideNorm = Mathf.Abs(px) / openBasinRadiusX;
                    float northNorm = pz > 104f ? (pz - 104f) / 38f : 0f;
                    float southNorm = pz < -155f ? (-155f - pz) / 55f : 0f;
                    float basinDist = Mathf.Max(sideNorm, Mathf.Max(northNorm, southNorm));

                    // Amphitheater seafloor notch torn open in front of & behind the Breach (Z = 94..128)
                    float breachPocketMask = Mathf.Exp(-Mathf.Pow(px / 32f, 2f)) * SmoothstepRange(94f, 128f, pz);

                    float outerRidgeRamp = SmoothstepRange(0.88f, 1.45f, basinDist);
                    float highRidgeMask = SmoothstepRange(1.05f, 2.35f, basinDist);

                    // 30% Flat, sweeping abyssal silt & rippled volcanic sand stretches
                    float fbmDune = (Fbm2D(px * 0.016f, pz * 0.016f, 4) - 0.5f) * 2.2f;
                    float spawnLaneCalm = SmoothstepRange(16f, 52f, Mathf.Abs(px));
                    float flatFloorY = fbmDune * Mathf.Lerp(0.10f, 0.58f, spawnLaneCalm);

                    // 60% Jagged volcanic basalt ridges & stepped rocky plateaus (5-octave Ridged Multifractal + terraced steps)
                    float ridged = RidgedMultifractal2D(px * 0.008f, pz * 0.008f, 5);
                    float voroEdge = VoronoiEdge2D(px * 0.012f, pz * 0.012f);
                    float plateauRaw = Fbm2D(px * 0.0065f, pz * 0.0065f, 3) * 0.65f + (1f - voroEdge) * 0.35f;
                    float pScaled = plateauRaw * 5.0f;
                    float pStepFrac = pScaled - Mathf.Floor(pScaled);
                    float steppedPlateau = Mathf.Floor(pScaled) / 5.0f + SmoothstepRange(0.75f, 0.98f, pStepFrac) * 0.2f;

                    float plateauPatch = SmoothstepRange(0.56f, 0.72f, Fbm2D(px * 0.009f + 19.3f, pz * 0.009f + 41.7f, 3));
                    float flankClear = SmoothstepRange(68f, 110f, Mathf.Abs(px)) * (1f - breachPocketMask * 0.85f);
                    float innerPlateauMask = Mathf.Clamp01(plateauPatch * flankClear);

                    float steppedHeight = (12f + steppedPlateau * 36f) * innerPlateauMask;
                    float outerRidgeHeight = (14f + steppedPlateau * 34f) * outerRidgeRamp
                        + (42f + ridged * 95f + steppedPlateau * 32f) * highRidgeMask;

                    float rockyHeight = Mathf.Max(steppedHeight, outerRidgeHeight);
                    float rockyBlend = Mathf.Clamp01(Mathf.Max(outerRidgeRamp, innerPlateauMask));

                    float worldY = Mathf.Lerp(flatFloorY, rockyHeight, rockyBlend);
                    worldY *= (1f - breachPocketMask * 0.68f);

                    // 10% Glowing crustal tectonic fissure channels near the Breach & central seafloor
                    float dBreach = Vector2.Distance(new Vector2(px, pz), new Vector2(0f, 110f));
                    float nearBreach = Mathf.Clamp01(1f - dBreach / 78f);
                    float crackNoise = VoronoiEdge2D(px * 0.08f + Fbm2D(px * 0.04f, pz * 0.04f, 2), pz * 0.08f);
                    float crackMask = 1f - SmoothstepRange(0.01f, 0.085f, crackNoise);
                    float laneSafety = (pz > 92f || Mathf.Abs(px) > 20f) ? 1f : 0.14f;
                    float carvedDepth = crackMask * nearBreach * 2.4f * laneSafety;

                    heights[z, x] = Mathf.Clamp01((baseFloorOffset + worldY - carvedDepth) / maxHeight);
                }
            }

            data.SetHeights(0, 0, heights);

            // Automatic 30-degree slope-based splatmap blending
            float[,,] alphamaps = new float[alphaRes, alphaRes, 2];
            for (int az = 0; az < alphaRes; az++)
            {
                float nz = (float)az / (alphaRes - 1);
                for (int ax = 0; ax < alphaRes; ax++)
                {
                    float nx = (float)ax / (alphaRes - 1);
                    float steepnessDeg = data.GetSteepness(nx, nz);
                    float basaltWeight = SmoothstepRange(28f, 32f, steepnessDeg);
                    alphamaps[az, ax, 0] = 1f - basaltWeight; // < 30 deg: abyssal silt (#1E2226)
                    alphamaps[az, ax, 1] = basaltWeight;      // > 30 deg: dark wet basalt (#14181D)
                }
            }
            data.SetAlphamaps(0, 0, alphamaps);
            EditorUtility.SetDirty(data);

            GameObject terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "Mariana_Trench_Terrain";
            terrainGo.tag = "Ground";
            terrainGo.transform.position = new Vector3(-half, -baseFloorOffset, -half);

            Terrain terrain = terrainGo.GetComponent<Terrain>();
            terrain.materialTemplate = EnsureTerrainLitMaterial();
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 450f;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            EditorUtility.SetDirty(terrainGo);

            BuildGlowingCrustalFissures(terrain);
            AssetDatabase.SaveAssets();
            return terrain;
        }

        private static void BuildGlowingCrustalFissures(Terrain terrain)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material crackMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_CrustalFissure.mat", urpLit, m =>
            {
                Color c = new Color(1.0f, 0.42f, 0.0f, 1f);
                Color hdr = new Color(1.0f, 0.48f, 0.02f, 1f) * 7.0f;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", hdr);
            });

            GameObject root = new GameObject("Mariana_CrustalFissures");
            root.transform.position = Vector3.zero;
            System.Random rng = new System.Random(7731);
            float terrainBaseY = terrain != null ? terrain.transform.position.y : -4f;

            for (int i = 0; i < 22; i++)
            {
                float px = Mathf.Lerp(-95f, 95f, (float)i / 21f) + (float)(rng.NextDouble() - 0.5) * 14f;
                float pz = 42f + (float)rng.NextDouble() * 66f;
                float len = 7f + (float)rng.NextDouble() * 16f;
                float yaw = Mathf.Atan2(-px, 112f - pz) * Mathf.Rad2Deg + (float)(rng.NextDouble() - 0.5) * 38f;
                float y = terrain != null ? terrain.SampleHeight(new Vector3(px, 0f, pz)) + terrainBaseY : 0f;

                GameObject crack = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crack.name = $"CrustalFissure_{i + 1:00}";
                var col = crack.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
                crack.transform.SetParent(root.transform, false);
                crack.transform.position = new Vector3(px, y + 0.06f, pz);
                crack.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                crack.transform.localScale = new Vector3(0.32f + (i % 3) * 0.16f, 0.12f, len);

                var mr = crack.GetComponent<MeshRenderer>();
                mr.sharedMaterial = crackMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            EditorUtility.SetDirty(root);
        }

        private static Mesh BuildFracturedBasaltBoulderMesh(int seed)
        {
            // Sculpt a jagged icosphere-like faceted volcanic basalt boulder
            GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh srcMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] baseVerts = srcMesh.vertices;
            int[] baseTris = srcMesh.triangles;
            UnityEngine.Object.DestroyImmediate(tempSphere);

            Vector3[] verts = new Vector3[baseVerts.Length];
            Color[] cols = new Color[baseVerts.Length];
            for (int i = 0; i < baseVerts.Length; i++)
            {
                Vector3 dir = baseVerts[i].normalized;
                float n1 = Mathf.PerlinNoise(dir.x * 2.4f + seed * 11.7f, dir.z * 2.4f + dir.y * 1.9f);
                float n2 = Mathf.Abs(2f * Mathf.PerlinNoise(dir.y * 4.5f + seed * 5.3f, dir.x * 4.5f) - 1f);
                float disp = 0.68f + n1 * 0.42f + (1f - n2) * 0.25f;
                Vector3 v = new Vector3(dir.x * disp * 1.25f, dir.y * disp * 0.85f, dir.z * disp * 1.1f);
                verts[i] = v;
                cols[i] = new Color(0f, 1f, 0f, 1f);
            }

            Mesh m = new Mesh { name = $"BasaltBoulder_{seed}" };
            m.vertices = verts;
            m.triangles = baseTris;
            m.colors = cols;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static void BuildAbyssalBasaltBoulders(Terrain terrain)
        {
            DestroyAllNamed("Underwater_BasaltBoulders");

            GameObject root = new GameObject("Underwater_BasaltBoulders");
            root.transform.position = Vector3.zero;

            Texture2D basaltNormal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TrenchTextureFolder}/VolcanicBasalt_Normal.png");
            Material basaltMat = EnsureVolcanicBasaltMaterial(basaltNormal);

            EnsureFolder("Assets/Meshes/MarianaTrench");
            EnsureFolder("Assets/Prefabs");
            Mesh boulderMeshA = SaveOrUpdateMeshAsset(BuildFracturedBasaltBoulderMesh(1), "Assets/Meshes/MarianaTrench/BasaltBoulder_A.asset");
            Mesh boulderMeshB = SaveOrUpdateMeshAsset(BuildFracturedBasaltBoulderMesh(7), "Assets/Meshes/MarianaTrench/BasaltBoulder_B.asset");

            // Create / update reusable Abyssal Basalt Boulder prefab asset
            GameObject tempPrefabSrc = new GameObject("Abyssal_Basalt_Boulder");
            tempPrefabSrc.AddComponent<MeshFilter>().sharedMesh = boulderMeshA;
            var pMr = tempPrefabSrc.AddComponent<MeshRenderer>();
            pMr.sharedMaterial = basaltMat;
            pMr.shadowCastingMode = ShadowCastingMode.On;
            pMr.receiveShadows = true;
            GameObject boulderPrefab = PrefabUtility.SaveAsPrefabAsset(tempPrefabSrc, TrenchBoulderPrefabPath);
            UnityEngine.Object.DestroyImmediate(tempPrefabSrc);

            float terrainBaseY = terrain != null ? terrain.transform.position.y : -4f;
            System.Random rng = new System.Random(10890);

            // Scatter 40 loose basalt boulders across wider ridge bases (|X|=76-152m) & Breach approach (|X|=28-96m, Z=52-108m)
            for (int i = 0; i < 40; i++)
            {
                bool nearBreach = i < 14;
                float px, pz;
                if (nearBreach)
                {
                    float sideSign = (i % 2 == 0) ? -1f : 1f;
                    px = sideSign * (28f + (float)rng.NextDouble() * 68f);
                    pz = 52f + (float)rng.NextDouble() * 56f;
                }
                else
                {
                    float sideSign = (i % 2 == 0) ? -1f : 1f;
                    px = sideSign * (76f + (float)rng.NextDouble() * 76f);
                    pz = -125f + ((float)(i - 14) / 26f) * 228f + (float)(rng.NextDouble() - 0.5) * 16f;
                }

                float sx = 3.8f + (float)rng.NextDouble() * 7.2f;
                float sy = 2.4f + (float)rng.NextDouble() * 5.8f;
                float sz = 3.6f + (float)rng.NextDouble() * 6.8f;
                float groundY = terrain != null ? terrain.SampleHeight(new Vector3(px, 0f, pz)) + terrainBaseY : 0f;

                GameObject bGo = boulderPrefab != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(boulderPrefab, root.transform)
                    : new GameObject($"Basalt_Boulder_{i + 1:00}");
                bGo.name = $"Basalt_Boulder_{i + 1:00}";
                bGo.transform.position = new Vector3(px, groundY + sy * 0.24f, pz);
                bGo.transform.rotation = Quaternion.Euler(
                    (float)rng.NextDouble() * 18f - 9f,
                    (float)rng.NextDouble() * 360f,
                    (float)rng.NextDouble() * 18f - 9f);
                bGo.transform.localScale = new Vector3(sx, sy, sz);

                var mf = GetOrAdd<MeshFilter>(bGo);
                mf.sharedMesh = (i % 2 == 0) ? boulderMeshA : boulderMeshB;
                var mr = GetOrAdd<MeshRenderer>(bGo);
                mr.sharedMaterial = basaltMat;
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
            }

            EditorUtility.SetDirty(root);
        }

        private static Mesh BuildVentChimneyMesh()
        {
            const int segs = 12;
            const int rings = 6;
            Vector3[] verts = new Vector3[(rings + 1) * segs];
            Vector2[] uvs = new Vector2[verts.Length];
            Color[] cols = new Color[verts.Length];
            int[] tris = new int[rings * segs * 6];

            for (int r = 0; r <= rings; r++)
            {
                float t = (float)r / rings;
                float y = t * 1.0f;
                float rad = Mathf.Lerp(1.0f, 0.34f, Mathf.Pow(t, 0.72f));
                for (int s = 0; s < segs; s++)
                {
                    float ang = ((float)s / segs) * Mathf.PI * 2f;
                    float jagged = 0.82f + 0.36f * Mathf.PerlinNoise(Mathf.Cos(ang) * 2.5f + t * 3.1f, Mathf.Sin(ang) * 2.5f + 5.7f);
                    float cr = rad * jagged;
                    int idx = r * segs + s;
                    verts[idx] = new Vector3(Mathf.Cos(ang) * cr, y, Mathf.Sin(ang) * cr);
                    uvs[idx] = new Vector2((float)s / segs, t);
                    float rimEmber = t >= 0.85f ? 0.55f : 0f;
                    cols[idx] = new Color(rimEmber, 1f, 0f, 1f);
                }
            }

            int ti = 0;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segs; s++)
                {
                    int sNext = (s + 1) % segs;
                    int a = r * segs + s;
                    int b = r * segs + sNext;
                    int c = (r + 1) * segs + s;
                    int d = (r + 1) * segs + sNext;

                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
            }

            Mesh m = new Mesh { name = "HydrothermalVentChimney" };
            m.vertices = verts;
            m.uv = uvs;
            m.colors = cols;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static void BuildHydrothermalVents(Terrain terrain)
        {
            DestroyAllNamed("Underwater_HydrothermalVents");

            GameObject root = new GameObject("Underwater_HydrothermalVents");
            root.transform.position = Vector3.zero;

            Texture2D basaltNormal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TrenchTextureFolder}/VolcanicBasalt_Normal.png");
            Material basaltMat = EnsureVolcanicBasaltMaterial(basaltNormal);

            Mesh ventMesh = SaveOrUpdateMeshAsset(BuildVentChimneyMesh(), "Assets/Meshes/MarianaTrench/HydrothermalVentChimney.asset");
            Shader particleShader = Shader.Find("PacificRim/SplashParticle") ?? Shader.Find("Sprites/Default");

            Material rocketSmokeRef = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_RocketSmoke.mat");
            Material fogWispRef = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_FogWisp.mat");
            Texture puffTex = rocketSmokeRef != null && rocketSmokeRef.mainTexture != null
                ? rocketSmokeRef.mainTexture
                : (fogWispRef != null ? fogWispRef.mainTexture : null);

            Material smokeMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_HydrothermalSmoke.mat", particleShader, m =>
            {
                if (puffTex != null) m.SetTexture("_MainTex", puffTex);
                m.SetColor("_TintColor", new Color(0.04f, 0.05f, 0.06f, 0.85f));
                m.SetFloat("_InvFade", 0.30f);
                m.SetFloat("_LightInfluence", 0.72f);
            });

            Material emberMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_VentBaseEmbers.mat",
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? particleShader, m =>
            {
                if (puffTex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", puffTex);
                if (puffTex != null && m.HasProperty("_MainTex")) m.SetTexture("_MainTex", puffTex);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(1.0f, 0.42f, 0.0f, 1f) * 3.5f);
                if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(1.0f, 0.42f, 0.0f, 1f) * 3.5f);
                if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(1.0f, 0.50f, 0.06f, 0.9f));
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f); // Additive
            });

            Vector3[] ventPositions =
            {
                new Vector3(-62f, 0f, 84f),
                new Vector3(64f, 0f, 86f),
                new Vector3(-58f, 0f, 96f),
                new Vector3(60f, 0f, 95f),
                new Vector3(-92f, 0f, 62f),
                new Vector3(94f, 0f, 58f),
                new Vector3(-118f, 0f, 36f),
                new Vector3(114f, 0f, 30f),
                new Vector3(-86f, 0f, 12f),
                new Vector3(88f, 0f, 14f),
                new Vector3(-124f, 0f, -18f),
                new Vector3(122f, 0f, -24f),
                new Vector3(-78f, 0f, -46f),
                new Vector3(82f, 0f, -48f),
                new Vector3(-108f, 0f, -76f),
                new Vector3(110f, 0f, -74f)
            };

            float terrainBaseY = terrain != null ? terrain.transform.position.y : -4f;
            System.Random rng = new System.Random(4207);
            for (int i = 0; i < ventPositions.Length; i++)
            {
                float height = 5.2f + (float)rng.NextDouble() * 4.4f;
                float radiusScale = 2.6f + (float)rng.NextDouble() * 1.8f;
                Vector3 groundPos = ventPositions[i];
                if (terrain != null) groundPos.y = terrain.SampleHeight(groundPos) + terrainBaseY - 0.35f;

                GameObject ventGo = new GameObject($"HydrothermalVent_{i + 1:00}");
                ventGo.transform.SetParent(root.transform, false);
                ventGo.transform.position = groundPos;
                ventGo.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                ventGo.transform.localScale = new Vector3(radiusScale, height, radiusScale);

                var mf = ventGo.AddComponent<MeshFilter>();
                mf.sharedMesh = ventMesh;
                var mr = ventGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = basaltMat;
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;

                // Black Smoker rising mineral smoke plume
                GameObject plumeGo = new GameObject("BlackSmokerPlume");
                plumeGo.transform.SetParent(root.transform, false);
                plumeGo.transform.position = groundPos + Vector3.up * (height * 0.94f);

                var ps = plumeGo.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.prewarm = true;
                main.maxParticles = 45;
                main.startLifetime = new ParticleSystem.MinMaxCurve(4.8f, 7.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3.2f, 5.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(3.2f, 6.2f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.05f, 0.06f, 0.07f, 0.85f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 6.0f;

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 14f;
                shape.radius = 0.85f;
                shape.rotation = new Vector3(-90f, 0f, 0f);

                var sizeOL = ps.sizeOverLifetime;
                sizeOL.enabled = true;
                AnimationCurve sc = new AnimationCurve();
                sc.AddKey(0f, 0.45f);
                sc.AddKey(0.4f, 1.35f);
                sc.AddKey(1f, 2.8f);
                sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sc);

                var colOL = ps.colorOverLifetime;
                colOL.enabled = true;
                Gradient g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(new Color(0.24f, 0.14f, 0.08f), 0f), new GradientColorKey(new Color(0.03f, 0.04f, 0.05f), 0.25f), new GradientColorKey(new Color(0.02f, 0.03f, 0.04f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.88f, 0.12f), new GradientAlphaKey(0.65f, 0.55f), new GradientAlphaKey(0f, 1f) });
                colOL.color = g;

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 1.6f;
                noise.frequency = 0.42f;
                noise.scrollSpeed = 0.65f;

                var rend = plumeGo.GetComponent<ParticleSystemRenderer>();
                rend.renderMode = ParticleSystemRenderMode.Billboard;
                rend.sharedMaterial = smokeMat;
                rend.shadowCastingMode = ShadowCastingMode.Off;
                rend.receiveShadows = false;

                // Faint orange base & chimney throat embers rising inside the mineral plume
                GameObject emberGo = new GameObject("VentBaseEmbers");
                emberGo.transform.SetParent(root.transform, false);
                emberGo.transform.position = groundPos + Vector3.up * (height * 0.35f);

                var eps = emberGo.AddComponent<ParticleSystem>();
                var eMain = eps.main;
                eMain.loop = true;
                eMain.playOnAwake = true;
                eMain.prewarm = true;
                eMain.maxParticles = 24;
                eMain.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
                eMain.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
                eMain.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.52f);
                eMain.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1.0f, 0.42f, 0.0f, 0.9f),
                    new Color(1.0f, 0.72f, 0.0f, 0.85f));
                eMain.simulationSpace = ParticleSystemSimulationSpace.World;

                var eEmit = eps.emission;
                eEmit.enabled = true;
                eEmit.rateOverTime = 5.5f;

                var eShape = eps.shape;
                eShape.enabled = true;
                eShape.shapeType = ParticleSystemShapeType.Cone;
                eShape.angle = 18f;
                eShape.radius = radiusScale * 0.55f;
                eShape.rotation = new Vector3(-90f, 0f, 0f);

                var eColOL = eps.colorOverLifetime;
                eColOL.enabled = true;
                Gradient eg = new Gradient();
                eg.SetKeys(
                    new[] { new GradientColorKey(new Color(1.0f, 0.55f, 0.08f), 0f), new GradientColorKey(new Color(1.0f, 0.35f, 0.02f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0f, 1f) });
                eColOL.color = eg;

                var eRend = emberGo.GetComponent<ParticleSystemRenderer>();
                eRend.renderMode = ParticleSystemRenderMode.Billboard;
                eRend.sharedMaterial = emberMat;
                eRend.shadowCastingMode = ShadowCastingMode.Off;
                eRend.receiveShadows = false;

                if (i < 6)
                {
                    Light emberLight = emberGo.AddComponent<Light>();
                    emberLight.type = LightType.Point;
                    emberLight.color = new Color(1.0f, 0.42f, 0.02f, 1f);
                    emberLight.range = 16f;
                    emberLight.intensity = 28f;
                    emberLight.shadows = LightShadows.None;
                }
            }

            EditorUtility.SetDirty(root);
        }

        private static Mesh BuildBreachScarMesh()
        {
            // Vertical-to-angled jagged tectonic scar (~56m wide max, 106m tall within 80-120m spec)
            const int rows = 30;
            const int cols = 10;
            Vector3[] verts = new Vector3[(rows + 1) * (cols + 1)];
            Vector2[] uvs = new Vector2[verts.Length];
            int[] tris = new int[rows * cols * 6];

            for (int r = 0; r <= rows; r++)
            {
                float v = (float)r / rows;
                float y = Mathf.Lerp(-2f, 104f, v);
                // S-curve tectonic scar center X (tilts diagonally as it rips upward through canyon wall)
                float centerX = Mathf.Lerp(-8f, 14f, v) + Mathf.Sin(v * Mathf.PI * 2.4f) * 6.2f;
                float halfWidth = Mathf.Sin(Mathf.Pow(v, 0.78f) * Mathf.PI) * 28f;
                halfWidth *= (0.82f + 0.36f * Mathf.PerlinNoise(v * 6.5f, 12.1f));
                halfWidth = Mathf.Max(2.0f, halfWidth);

                for (int c = 0; c <= cols; c++)
                {
                    float u = (float)c / cols;
                    float xOff = (u - 0.5f) * 2f * halfWidth;
                    float centerBell = 1f - Mathf.Abs(u - 0.5f) * 2f;
                    float zOff = centerBell * 4.2f + Mathf.Sin(v * 11f + u * 7f) * 1.3f;

                    int idx = r * (cols + 1) + c;
                    verts[idx] = new Vector3(centerX + xOff, y, zOff);
                    uvs[idx] = new Vector2(u, v);
                }
            }

            int ti = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int a = r * (cols + 1) + c;
                    int b = a + 1;
                    int d0 = a + (cols + 1);
                    int d1 = d0 + 1;

                    tris[ti++] = a; tris[ti++] = d0; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = d0; tris[ti++] = d1;
                }
            }

            Mesh m = new Mesh { name = "BreachCoreRiftScar" };
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildObsidianToothMesh()
        {
            const int rings = 6;
            const int segs = 6;
            Vector3[] verts = new Vector3[(rings + 1) * segs];
            Vector2[] uvs = new Vector2[verts.Length];
            int[] tris = new int[rings * segs * 6];

            for (int r = 0; r <= rings; r++)
            {
                float t = (float)r / rings;
                float y = t;
                float rad = Mathf.Lerp(1.0f, 0.04f, Mathf.Pow(t, 0.65f));
                float twist = t * 0.65f;
                float curveX = t * t * 0.28f;
                for (int s = 0; s < segs; s++)
                {
                    float ang = ((float)s / segs) * Mathf.PI * 2f + twist;
                    float ridge = (s % 2 == 0) ? 1.15f : 0.72f;
                    int idx = r * segs + s;
                    verts[idx] = new Vector3(curveX + Mathf.Cos(ang) * rad * ridge, y, Mathf.Sin(ang) * rad * ridge);
                    uvs[idx] = new Vector2((float)s / segs, t);
                }
            }

            int ti = 0;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segs; s++)
                {
                    int sNext = (s + 1) % segs;
                    int a = r * segs + s;
                    int b = r * segs + sNext;
                    int c = (r + 1) * segs + s;
                    int d = (r + 1) * segs + sNext;

                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
            }

            Mesh m = new Mesh { name = "BreachObsidianTooth" };
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Material EnsureKaijuBloodPoolMaterial()
        {
            const int res = 256;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            for (int y = 0; y < res; y++)
            {
                float ny = ((float)y / (res - 1)) * 2f - 1f;
                for (int x = 0; x < res; x++)
                {
                    float nx = ((float)x / (res - 1)) * 2f - 1f;
                    float ang = Mathf.Atan2(ny, nx);
                    float warp = 0.78f + 0.34f * Mathf.PerlinNoise(Mathf.Cos(ang) * 2.2f + 13.7f, Mathf.Sin(ang) * 2.2f + 29.1f)
                                       + 0.14f * Mathf.PerlinNoise(nx * 4.5f + 4.2f, ny * 4.5f + 9.8f);
                    float d = Mathf.Sqrt(nx * nx + ny * ny) / Mathf.Max(0.25f, warp);

                    float alpha = 1f - SmoothstepRange(0.68f, 0.96f, d);
                    float centerCore = 1f - SmoothstepRange(0.05f, 0.72f, d);
                    Color coreCol = new Color(0.0f, 0.90f, 1.0f, alpha);   // #00E5FF electric-blue core
                    Color edgeCol = new Color(0.0f, 0.40f, 1.0f, alpha);   // #0066FF bioluminescent rim
                    Color col = Color.Lerp(edgeCol, coreCol, centerCore);
                    col.a = alpha;
                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply(false, false);

            const string maskPath = "Assets/Materials/KaijuBloodPool_Mask.png";
            EnsureFolder("Assets/Materials");
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath), maskPath), png);
            AssetDatabase.ImportAsset(maskPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(maskPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            Texture2D maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return EnsureWeatherMaterial("Assets/Materials/PacificRim_KaijuBloodPool.mat", urpLit, m =>
            {
                if (maskTex != null)
                {
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", maskTex);
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", maskTex);
                    if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", maskTex);
                }
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0f, 0.78f, 1f, 0.92f));
                if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0f, 0.78f, 1f, 0.92f));
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.88f);
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", new Color(0.08f, 2.9f, 4.8f, 1f));
                m.renderQueue = (int)RenderQueue.Transparent;
            });
        }

        private static Mesh BuildKaijuBloodPoolMesh()
        {
            const int rings = 6;
            const int segs = 32;
            Vector3[] verts = new Vector3[1 + rings * segs];
            Vector2[] uvs = new Vector2[verts.Length];
            int[] tris = new int[segs * 3 + (rings - 1) * segs * 6];

            verts[0] = new Vector3(0f, -0.02f, 0f);
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int r = 1; r <= rings; r++)
            {
                float t = (float)r / rings;
                for (int s = 0; s < segs; s++)
                {
                    float ang = ((float)s / segs) * Mathf.PI * 2f;
                    float lobe = 0.72f + 0.36f * Mathf.PerlinNoise(Mathf.Cos(ang) * 2.1f + 5.7f, Mathf.Sin(ang) * 2.1f + 11.4f);
                    float rad = t * lobe;
                    float y = t >= 0.85f ? 0.03f : Mathf.Lerp(-0.02f, 0.015f, t);
                    int idx = 1 + (r - 1) * segs + s;
                    verts[idx] = new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad * 0.78f);
                    uvs[idx] = new Vector2(0.5f + verts[idx].x * 0.45f, 0.5f + verts[idx].z * 0.45f);
                }
            }

            int ti = 0;
            for (int s = 0; s < segs; s++)
            {
                int sNext = (s + 1) % segs;
                tris[ti++] = 0;
                tris[ti++] = 1 + sNext;
                tris[ti++] = 1 + s;
            }
            for (int r = 1; r < rings; r++)
            {
                int rowA = 1 + (r - 1) * segs;
                int rowB = 1 + r * segs;
                for (int s = 0; s < segs; s++)
                {
                    int sNext = (s + 1) % segs;
                    int a = rowA + s;
                    int b = rowA + sNext;
                    int c = rowB + s;
                    int d = rowB + sNext;
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                    tris[ti++] = b; tris[ti++] = d; tris[ti++] = c;
                }
            }

            Mesh m = new Mesh { name = "KaijuBloodCrevicePool" };
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static void BuildKaijuBloodPools(Terrain terrain)
        {
            DestroyAllNamed("Underwater_KaijuBloodPools");

            GameObject root = new GameObject("Underwater_KaijuBloodPools");
            root.transform.position = Vector3.zero;

            EnsureFolder("Assets/Meshes/MarianaTrench");
            Material bloodMat = EnsureKaijuBloodPoolMaterial();
            Mesh poolMesh = SaveOrUpdateMeshAsset(BuildKaijuBloodPoolMesh(), "Assets/Meshes/MarianaTrench/KaijuBloodCrevicePool.asset");
            Mesh rimRockMesh = SaveOrUpdateMeshAsset(BuildFracturedBasaltBoulderMesh(7), "Assets/Meshes/MarianaTrench/BasaltBoulder_B.asset");
            Texture2D basaltNormal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TrenchTextureFolder}/VolcanicBasalt_Normal.png");
            Material basaltMat = EnsureVolcanicBasaltMaterial(basaltNormal);

            Vector2[] poolCoords =
            {
                new Vector2(-28f, 64f), new Vector2(32f, 58f),
                new Vector2(-54f, 44f), new Vector2(56f, 38f),
                new Vector2(-38f, 18f), new Vector2(42f, 12f),
                new Vector2(-78f, 24f), new Vector2(82f, 28f),
                new Vector2(-46f, -16f), new Vector2(52f, -22f),
                new Vector2(-92f, -8f), new Vector2(96f, -12f),
                new Vector2(-34f, -52f), new Vector2(40f, -58f),
                new Vector2(-72f, 82f), new Vector2(76f, 80f)
            };

            float terrainBaseY = terrain != null ? terrain.transform.position.y : -4f;
            System.Random rng = new System.Random(8842);

            for (int i = 0; i < poolCoords.Length; i++)
            {
                float px = poolCoords[i].x + (float)(rng.NextDouble() - 0.5) * 6f;
                float pz = poolCoords[i].y + (float)(rng.NextDouble() - 0.5) * 6f;
                float groundY = terrain != null ? terrain.SampleHeight(new Vector3(px, 0f, pz)) + terrainBaseY : 0f;

                float radiusX = 4.2f + (float)rng.NextDouble() * 4.8f;
                float radiusZ = 3.8f + (float)rng.NextDouble() * 4.4f;
                float yaw = (float)rng.NextDouble() * 360f;

                GameObject poolGo = new GameObject($"KaijuBloodPool_{i + 1:00}");
                poolGo.transform.SetParent(root.transform, false);
                poolGo.transform.position = new Vector3(px, groundY + 0.05f, pz);
                poolGo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                poolGo.transform.localScale = new Vector3(radiusX, 1f, radiusZ);

                poolGo.AddComponent<MeshFilter>().sharedMesh = poolMesh;
                var mr = poolGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = bloodMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = true;

                // Frame each blood pool inside rock crevices with 2-3 small dark basalt rim rocks
                int rimRocks = 2 + (i % 2);
                for (int r = 0; r < rimRocks; r++)
                {
                    float rAng = ((float)r / rimRocks) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.8f;
                    float rx = px + Mathf.Cos(rAng) * (radiusX * 0.88f);
                    float rz = pz + Mathf.Sin(rAng) * (radiusZ * 0.88f);
                    float ry = terrain != null ? terrain.SampleHeight(new Vector3(rx, 0f, rz)) + terrainBaseY : groundY;

                    GameObject rock = new GameObject($"CreviceRock_{i + 1:00}_{r + 1}");
                    rock.transform.SetParent(root.transform, false);
                    rock.transform.position = new Vector3(rx, ry + 0.25f, rz);
                    rock.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    rock.transform.localScale = new Vector3(1.4f + (float)rng.NextDouble() * 1.4f, 0.55f + (float)rng.NextDouble() * 0.5f, 1.3f + (float)rng.NextDouble() * 1.2f);
                    rock.AddComponent<MeshFilter>().sharedMesh = rimRockMesh;
                    var rMr = rock.AddComponent<MeshRenderer>();
                    rMr.sharedMaterial = basaltMat;
                    rMr.shadowCastingMode = ShadowCastingMode.On;
                    rMr.receiveShadows = true;
                }

                if (i < 6)
                {
                    GameObject lightGo = new GameObject($"BloodPoolGlow_{i + 1:00}");
                    lightGo.transform.SetParent(root.transform, false);
                    lightGo.transform.position = new Vector3(px, groundY + 1.25f, pz);
                    Light pt = lightGo.AddComponent<Light>();
                    pt.type = LightType.Point;
                    pt.color = new Color(0f, 216f / 255f, 1f, 1f); // #00D8FF
                    pt.range = 18f;
                    pt.intensity = 42f;
                    pt.shadows = LightShadows.None;
                }
            }

            EditorUtility.SetDirty(root);
        }

        private static Mesh BuildBreachSeafloorChasmMesh()
        {
            // Horizontal jagged seafloor rift torn directly into the abyssal basalt basin (64m wide x 42m long)
            const int rows = 24;
            const int cols = 10;
            Vector3[] verts = new Vector3[(rows + 1) * (cols + 1)];
            Vector2[] uvs = new Vector2[verts.Length];
            int[] tris = new int[rows * cols * 6];

            for (int r = 0; r <= rows; r++)
            {
                float v = (float)r / rows;
                float z = Mathf.Lerp(-21f, 21f, v);
                float centerX = Mathf.Sin(v * Mathf.PI * 2.2f) * 6.5f;
                float halfW = Mathf.Sin(Mathf.Pow(v, 0.72f) * Mathf.PI) * 30f;
                halfW *= 0.82f + 0.36f * Mathf.PerlinNoise(v * 7.1f, 19.3f);
                halfW = Mathf.Max(2.2f, halfW);

                for (int c = 0; c <= cols; c++)
                {
                    float u = (float)c / cols;
                    float xOff = (u - 0.5f) * 2f * halfW;
                    float bell = 1f - Mathf.Abs(u - 0.5f) * 2f;
                    float yOff = -bell * 1.4f + Mathf.Sin(v * 13f + u * 6f) * 0.22f;
                    int idx = r * (cols + 1) + c;
                    verts[idx] = new Vector3(centerX + xOff, yOff, z);
                    uvs[idx] = new Vector2(u, v);
                }
            }

            int ti = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int a = r * (cols + 1) + c;
                    int b = a + 1;
                    int d0 = a + (cols + 1);
                    int d1 = d0 + 1;
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = d0;
                    tris[ti++] = b; tris[ti++] = d1; tris[ti++] = d0;
                }
            }

            Mesh m = new Mesh { name = "BreachSeafloorChasm" };
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Texture2D EnsureBreachCoreGradientTexture()
        {
            const int w = 256;
            const int h = 256;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color coreColor = new Color(1.0f, 107f / 255f, 0f, 1f); // #FF6B00 blazing core
            Color edgeColor = new Color(1.0f, 184f / 255f, 0f, 1f); // #FFB800 molten edge

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1);
                    float radial = Mathf.Clamp01(Mathf.Abs(u - 0.5f) * 2f);
                    float organicWarp = 0.88f + 0.24f * Mathf.PerlinNoise(u * 6f + 3.1f, v * 12f + 7.4f);
                    float t = Mathf.Clamp01(radial * organicWarp);
                    Color c = Color.Lerp(coreColor, edgeColor, SmoothstepRange(0.08f, 0.92f, t));
                    float vFade = SmoothstepRange(0f, 0.06f, v) * SmoothstepRange(1f, 0.90f, v);
                    c *= Mathf.Lerp(0.75f, 1.0f, vFade);
                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply(false, false);

            EnsureFolder("Assets/Materials");
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            string fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), BreachCoreGradientPath);
            File.WriteAllBytes(fullPath, png);
            AssetDatabase.ImportAsset(BreachCoreGradientPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(BreachCoreGradientPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(BreachCoreGradientPath);
        }

        private static void BuildTheBreachLandmark(UnderwaterEnvironmentController ctrl, Material seabedMat)
        {
            DestroyAllNamed("The_Breach", "The_Breach_Tectonic_Rift");

            Vector3 breachPos = new Vector3(0f, 0f, 112f);
            GameObject root = new GameObject("The_Breach");
            root.transform.position = breachPos;
            root.transform.rotation = Quaternion.identity;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit") ?? urpLit;
            Texture2D breachGradTex = EnsureBreachCoreGradientTexture();

            // HDR Emissive Core (#FF6B00 core fading to #FFB800 edges, intensity 8.5 within 6-10)
            Material breachCoreMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_BreachCore.mat", urpLit, m =>
            {
                Color baseCore = new Color(1.0f, 107f / 255f, 0f, 1f);
                Color hdrEm = Color.white * 8.5f;
                if (breachGradTex != null)
                {
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", breachGradTex);
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", breachGradTex);
                    if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", breachGradTex);
                }
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCore);
                if (m.HasProperty("_Color")) m.SetColor("_Color", baseCore);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", hdrEm);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            });

            Material breachRimMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_BreachRim.mat", urpLit, m =>
            {
                Color baseC = new Color(1.0f, 107f / 255f, 0f, 1f);
                Color hdrEm = new Color(1.0f, 130f / 255f, 0f, 1f) * 6.8f;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseC);
                if (m.HasProperty("_Color")) m.SetColor("_Color", baseC);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", hdrEm);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            });

            Material obsidianMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_BreachObsidian.mat", urpLit, m =>
            {
                Color obs = new Color(0.03f, 0.035f, 0.045f, 1f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", obs);
                if (m.HasProperty("_Color")) m.SetColor("_Color", obs);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.25f);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.72f);
            });

            Material webbingMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_BreachWebbing.mat", urpLit, m =>
            {
                Color webBase = new Color(0.42f, 0.26f, 0.04f, 1f);
                Color webHdr = new Color(1.0f, 0.72f, 0.08f, 1f) * 4.2f;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", webBase);
                if (m.HasProperty("_Color")) m.SetColor("_Color", webBase);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", webHdr);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.55f);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            });

            // 1. Core Tectonic Scar (106m rising scar + jagged horizontal Seafloor Chasm torn into seabed)
            Mesh scarMesh = SaveOrUpdateMeshAsset(BuildBreachScarMesh(), "Assets/Meshes/MarianaTrench/BreachCoreRiftScar.asset");
            Mesh seafloorChasmMesh = SaveOrUpdateMeshAsset(BuildBreachSeafloorChasmMesh(), "Assets/Meshes/MarianaTrench/BreachSeafloorChasm.asset");

            GameObject outerScar = new GameObject("Breach_OuterRimEmber");
            outerScar.transform.SetParent(root.transform, false);
            outerScar.transform.localPosition = new Vector3(0f, 0f, 1.8f);
            outerScar.transform.localScale = new Vector3(1.16f, 1.05f, 1.1f);
            outerScar.AddComponent<MeshFilter>().sharedMesh = scarMesh;
            var outerMr = outerScar.AddComponent<MeshRenderer>();
            outerMr.sharedMaterial = breachRimMat;
            outerMr.shadowCastingMode = ShadowCastingMode.Off;

            GameObject innerScar = new GameObject("Breach_InnerCoreGlow");
            innerScar.transform.SetParent(root.transform, false);
            innerScar.transform.localPosition = Vector3.zero;
            innerScar.transform.localScale = new Vector3(0.86f, 0.94f, 1f);
            innerScar.AddComponent<MeshFilter>().sharedMesh = scarMesh;
            var innerMr = innerScar.AddComponent<MeshRenderer>();
            innerMr.sharedMaterial = breachCoreMat;
            innerMr.shadowCastingMode = ShadowCastingMode.Off;

            GameObject floorRimScar = new GameObject("Breach_SeafloorScarRim");
            floorRimScar.transform.SetParent(root.transform, false);
            floorRimScar.transform.localPosition = new Vector3(0f, 0.14f, -12f);
            floorRimScar.transform.localScale = new Vector3(1.18f, 1f, 1.12f);
            floorRimScar.AddComponent<MeshFilter>().sharedMesh = seafloorChasmMesh;
            var floorRimMr = floorRimScar.AddComponent<MeshRenderer>();
            floorRimMr.sharedMaterial = breachRimMat;
            floorRimMr.shadowCastingMode = ShadowCastingMode.Off;

            GameObject floorCoreScar = new GameObject("Breach_SeafloorScarCore");
            floorCoreScar.transform.SetParent(root.transform, false);
            floorCoreScar.transform.localPosition = new Vector3(0f, 0.22f, -12f);
            floorCoreScar.transform.localScale = Vector3.one;
            floorCoreScar.AddComponent<MeshFilter>().sharedMesh = seafloorChasmMesh;
            var floorCoreMr = floorCoreScar.AddComponent<MeshRenderer>();
            floorCoreMr.sharedMaterial = breachCoreMat;
            floorCoreMr.shadowCastingMode = ShadowCastingMode.Off;

            // 2. Twisted Jagged Obsidian Rock Teeth framing rising scar + torn seafloor lip (32 rising + 12 seafloor lip)
            Mesh toothMesh = SaveOrUpdateMeshAsset(BuildObsidianToothMesh(), "Assets/Meshes/MarianaTrench/BreachObsidianTooth.asset");
            GameObject teethRoot = new GameObject("Breach_ObsidianRockTeeth");
            teethRoot.transform.SetParent(root.transform, false);

            for (int i = 0; i < 32; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float v = (i < 26) ? ((float)(i / 2) / 12f) : (0.85f + ((float)(i - 26) / 6f) * 0.15f);
                float y = Mathf.Lerp(-1f, 100f, v);
                float centerX = Mathf.Lerp(-8f, 14f, v) + Mathf.Sin(v * Mathf.PI * 2.4f) * 6.2f;
                float edgeWidth = Mathf.Sin(Mathf.Pow(v, 0.78f) * Mathf.PI) * 28.5f;
                float px = centerX + side * (edgeWidth + 1.5f);
                if (i >= 26) px = centerX + Mathf.Lerp(-16f, 16f, (float)(i - 26) / 5f);

                GameObject tooth = new GameObject($"ObsidianTooth_{i + 1:00}");
                tooth.transform.SetParent(teethRoot.transform, false);
                tooth.transform.localPosition = new Vector3(px, y, -1.5f + ((i % 3) - 1) * 1.4f);

                float tiltZ = i >= 26 ? Mathf.Lerp(145f, 215f, (float)(i - 26) / 5f) : (side > 0 ? 58f + (i % 3) * 8f : -58f - (i % 3) * 8f);
                float tiltX = -18f - (i % 4) * 6f;
                tooth.transform.localRotation = Quaternion.Euler(tiltX, side * 18f, tiltZ);
                float fangLen = 13f + (i % 4) * 4.2f;
                float fangThick = 3.6f + (i % 3) * 1.2f;
                tooth.transform.localScale = new Vector3(fangThick, fangLen, fangThick * 1.25f);

                tooth.AddComponent<MeshFilter>().sharedMesh = toothMesh;
                var tMr = tooth.AddComponent<MeshRenderer>();
                tMr.sharedMaterial = obsidianMat;
                tMr.shadowCastingMode = ShadowCastingMode.On;
                tMr.receiveShadows = true;
            }

            // 12 Seafloor-lip obsidian fangs jutting inward over the torn seafloor chasm
            for (int i = 0; i < 12; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float t = (float)(i / 2) / 5f;
                float zLoc = Mathf.Lerp(-28f, 2f, t);
                float xSpan = Mathf.Lerp(10f, 28f, Mathf.Sin(t * Mathf.PI));

                GameObject tooth = new GameObject($"SeafloorLipTooth_{i + 1:00}");
                tooth.transform.SetParent(teethRoot.transform, false);
                tooth.transform.localPosition = new Vector3(side * xSpan, 0.2f, zLoc);
                tooth.transform.localRotation = Quaternion.Euler(-12f + (i % 3) * 8f, side * 24f, side * (46f + (i % 3) * 10f));
                float fangLen = 8.5f + (i % 3) * 2.8f;
                float fangThick = 2.8f + (i % 3) * 0.9f;
                tooth.transform.localScale = new Vector3(fangThick, fangLen, fangThick * 1.2f);

                tooth.AddComponent<MeshFilter>().sharedMesh = toothMesh;
                var tMr = tooth.AddComponent<MeshRenderer>();
                tMr.sharedMaterial = obsidianMat;
                tMr.shadowCastingMode = ShadowCastingMode.On;
                tMr.receiveShadows = true;
            }

            // 3. Pulsing Bioluminescent Cartilage-Like Webbing spanning rising scar & seafloor chasm edges
            GameObject webRoot = new GameObject("Breach_OrganicWebbing");
            webRoot.transform.SetParent(root.transform, false);
            var webRenderers = new List<Renderer>(26);
            for (int i = 0; i < 20; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float v = 0.06f + ((float)(i / 2) / 9f) * 0.86f;
                float y = Mathf.Lerp(2f, 96f, v);
                float centerX = Mathf.Lerp(-8f, 14f, v) + Mathf.Sin(v * Mathf.PI * 2.4f) * 6.2f;
                float edgeWidth = Mathf.Sin(Mathf.Pow(v, 0.78f) * Mathf.PI) * 23.0f;

                GameObject web = new GameObject($"OrganicWebbing_{i + 1:00}");
                web.transform.SetParent(webRoot.transform, false);
                web.transform.localPosition = new Vector3(centerX + side * edgeWidth * 0.78f, y, -0.8f);
                web.transform.localRotation = Quaternion.Euler(-12f, side * 24f, side * (32f + (i % 3) * 18f));
                web.transform.localScale = new Vector3(1.2f + (i % 3) * 0.45f, 12.5f + (i % 4) * 2.5f, 1.9f);

                web.AddComponent<MeshFilter>().sharedMesh = toothMesh;
                var wMr = web.AddComponent<MeshRenderer>();
                wMr.sharedMaterial = webbingMat;
                wMr.shadowCastingMode = ShadowCastingMode.Off;
                webRenderers.Add(wMr);
            }

            for (int i = 0; i < 6; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float t = 0.18f + (float)(i / 2) * 0.30f;
                float zLoc = Mathf.Lerp(-26f, -4f, t);
                float xSpan = Mathf.Lerp(10f, 24f, Mathf.Sin(t * Mathf.PI));

                GameObject web = new GameObject($"SeafloorWebbing_{i + 1:00}");
                web.transform.SetParent(webRoot.transform, false);
                web.transform.localPosition = new Vector3(side * xSpan * 0.72f, 0.3f, zLoc);
                web.transform.localRotation = Quaternion.Euler(78f, side * 25f, side * 38f);
                web.transform.localScale = new Vector3(1.1f, 9.5f + (i % 3) * 2.2f, 1.4f);

                web.AddComponent<MeshFilter>().sharedMesh = toothMesh;
                var wMr = web.AddComponent<MeshRenderer>();
                wMr.sharedMaterial = webbingMat;
                wMr.shadowCastingMode = ShadowCastingMode.Off;
                webRenderers.Add(wMr);
            }

            // 4. Branching Seafloor Glowing Magma/Energy Cracks radiating outward toward Slattern (Z=112 -> Z=48)
            GameObject veinsRoot = new GameObject("Breach_SeafloorFissureVeins");
            veinsRoot.transform.SetParent(root.transform, false);
            Terrain activeTerrain = Terrain.activeTerrain;
            float terrainBaseY = activeTerrain != null ? activeTerrain.transform.position.y : -4f;
            for (int i = 0; i < 16; i++)
            {
                float yaw = Mathf.Lerp(-52f, 52f, (float)i / 15f) + ((i % 2 == 0) ? 3.5f : -3.5f);
                float veinLen = 38f + (i % 5) * 9f;
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
                Vector3 worldMid = breachPos + dir * (veinLen * 0.5f);
                float groundY = activeTerrain != null ? activeTerrain.SampleHeight(worldMid) + terrainBaseY : 0f;

                GameObject vein = GameObject.CreatePrimitive(PrimitiveType.Cube);
                vein.name = $"SeafloorLavaVein_{i + 1:00}";
                var vc = vein.GetComponent<Collider>();
                if (vc != null) UnityEngine.Object.DestroyImmediate(vc);

                vein.transform.SetParent(veinsRoot.transform, false);
                vein.transform.localPosition = dir * (veinLen * 0.5f) + Vector3.up * (groundY + 0.08f);
                vein.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                vein.transform.localScale = new Vector3(0.45f + (i % 3) * 0.25f, 0.16f, veinLen);

                var vMr = vein.GetComponent<MeshRenderer>();
                vMr.sharedMaterial = (i % 2 == 0) ? breachCoreMat : breachRimMat;
                vMr.shadowCastingMode = ShadowCastingMode.Off;
            }

            // 5. Intense Volumetric God Rays erupting upward and outward from the interior & seafloor scar of the Breach
            Mesh coneMesh = EnsureSmoothBreachGodRayConeMesh();
            if (coneMesh != null)
            {
                Shader beamShader = Shader.Find("PacificRim/VolumetricLightBeam") ?? urpUnlit;
                Material amberRayMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_BreachGodRayAmber.mat", beamShader, m =>
                {
                    m.SetColor("_Color", new Color(1.0f, 0.44f, 0.04f, 0.40f));
                    m.SetFloat("_Intensity", 2.9f);
                    m.SetFloat("_ConeApexFade", 0.12f);
                    m.SetFloat("_ConeBaseFade", 0.85f);
                    m.SetFloat("_RimPower", 2.9f);
                    m.SetFloat("_NoiseSpeed", 1.15f);
                    m.SetFloat("_NoiseScale", 7.0f);
                    m.SetFloat("_NoiseStrength", 0.42f);
                    m.SetFloat("_WaterLevel", -500f);
                    if (m.HasProperty("_StreakDensity")) m.SetFloat("_StreakDensity", 8.0f);
                    if (m.HasProperty("_StreakContrast")) m.SetFloat("_StreakContrast", 0.74f);
                    if (m.HasProperty("_PulseSpeed")) m.SetFloat("_PulseSpeed", 0.85f);
                    if (m.HasProperty("_PulseAmount")) m.SetFloat("_PulseAmount", 0.45f);
                });
                Material yellowRayMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_BreachGodRayYellow.mat", beamShader, m =>
                {
                    m.SetColor("_Color", new Color(1.0f, 0.74f, 0.14f, 0.34f));
                    m.SetFloat("_Intensity", 2.6f);
                    m.SetFloat("_ConeApexFade", 0.14f);
                    m.SetFloat("_ConeBaseFade", 0.86f);
                    m.SetFloat("_RimPower", 3.1f);
                    m.SetFloat("_NoiseSpeed", 1.40f);
                    m.SetFloat("_NoiseScale", 8.5f);
                    m.SetFloat("_NoiseStrength", 0.38f);
                    m.SetFloat("_WaterLevel", -500f);
                    if (m.HasProperty("_StreakDensity")) m.SetFloat("_StreakDensity", 11.0f);
                    if (m.HasProperty("_StreakContrast")) m.SetFloat("_StreakContrast", 0.70f);
                    if (m.HasProperty("_PulseSpeed")) m.SetFloat("_PulseSpeed", 1.15f);
                    if (m.HasProperty("_PulseAmount")) m.SetFloat("_PulseAmount", 0.42f);
                });

                GameObject raysRoot = new GameObject("Breach_VolumetricGodRays");
                raysRoot.transform.SetParent(root.transform, false);

                Vector3[] rayOrigins =
                {
                    new Vector3(0f, 34f, -1f),
                    new Vector3(-11f, 22f, -1f),
                    new Vector3(13f, 44f, -1f),
                    new Vector3(-8f, 58f, -1f),
                    new Vector3(10f, 18f, -1f),
                    new Vector3(3f, 74f, -1f),
                    new Vector3(-14f, 38f, -1f),
                    new Vector3(15f, 28f, -1f),
                    new Vector3(0f, 12f, -1f),
                    new Vector3(-6f, 82f, -1f),
                    new Vector3(9f, 66f, -1f),
                    new Vector3(-5f, 2f, -14f), // Seafloor scar upward beams
                    new Vector3(7f, 2f, -10f),
                    new Vector3(0f, 2f, -18f)
                };
                Vector3[] rayEulerAngles =
                {
                    new Vector3(10f, 180f, 0f),
                    new Vector3(16f, 198f, 0f),
                    new Vector3(8f, 162f, 0f),
                    new Vector3(-28f, 188f, 0f),
                    new Vector3(20f, 168f, 0f),
                    new Vector3(-42f, 174f, 0f),
                    new Vector3(-18f, 206f, 0f),
                    new Vector3(-12f, 152f, 0f),
                    new Vector3(24f, 180f, 0f),
                    new Vector3(-48f, 184f, 0f),
                    new Vector3(-34f, 166f, 0f),
                    new Vector3(-58f, 190f, 0f),
                    new Vector3(-52f, 168f, 0f),
                    new Vector3(-64f, 180f, 0f)
                };

                for (int i = 0; i < rayOrigins.Length; i++)
                {
                    GameObject ray = new GameObject($"BreachGodRay_{i + 1:00}");
                    ray.transform.SetParent(raysRoot.transform, false);
                    ray.transform.localPosition = rayOrigins[i];
                    ray.transform.localRotation = Quaternion.Euler(rayEulerAngles[i]);
                    float wScale = (i == 0 || i == 8 || i >= 11) ? 4.4f : (2.7f + (i % 3) * 0.7f);
                    float lScale = (i == 0) ? 2.05f : (1.55f + (i % 3) * 0.25f);
                    ray.transform.localScale = new Vector3(wScale, wScale, lScale);

                    ray.AddComponent<MeshFilter>().sharedMesh = coneMesh;
                    var rMr = ray.AddComponent<MeshRenderer>();
                    rMr.sharedMaterial = (i % 2 == 0) ? amberRayMat : yellowRayMat;
                    rMr.shadowCastingMode = ShadowCastingMode.Off;
                    rMr.receiveShadows = false;
                }
            }

            // 6. High-Radius Real-Time Breach Lights (fiery underglow, upward god rays & Slattern/Gipsy rim-lighting)
            var pulseLights = new List<Light>(4);
            var baseIntensities = new List<float>(4);

            GameObject coreLightGo = new GameObject("Breach_CorePointLight");
            coreLightGo.transform.SetParent(root.transform, false);
            coreLightGo.transform.localPosition = new Vector3(0f, 36f, -6f);
            Light corePt = coreLightGo.AddComponent<Light>();
            corePt.type = LightType.Point;
            corePt.color = new Color(1.0f, 0.42f, 0.0f, 1f);
            corePt.range = 215f;
            corePt.intensity = 5200f;
            corePt.shadows = LightShadows.None;
            pulseLights.Add(corePt);
            baseIntensities.Add(5200f);

            GameObject rimSpotGo = new GameObject("Breach_OutwardRimSpotLight");
            rimSpotGo.transform.SetParent(root.transform, false);
            rimSpotGo.transform.localPosition = new Vector3(2f, 44f, -8f);
            rimSpotGo.transform.LookAt(new Vector3(0f, 12f, 20f));
            Light rimSpot = rimSpotGo.AddComponent<Light>();
            rimSpot.type = LightType.Spot;
            rimSpot.color = new Color(1.0f, 0.58f, 0.08f, 1f);
            rimSpot.range = 260f;
            rimSpot.spotAngle = 86f;
            rimSpot.innerSpotAngle = 48f;
            rimSpot.intensity = 8200f;
            rimSpot.shadows = LightShadows.None;
            pulseLights.Add(rimSpot);
            baseIntensities.Add(8200f);

            GameObject upperSpotGo = new GameObject("Breach_UpperGodRaySpotLight");
            upperSpotGo.transform.SetParent(root.transform, false);
            upperSpotGo.transform.localPosition = new Vector3(-2f, 68f, -6f);
            upperSpotGo.transform.LookAt(new Vector3(0f, 110f, -15f));
            Light upperSpot = upperSpotGo.AddComponent<Light>();
            upperSpot.type = LightType.Spot;
            upperSpot.color = new Color(1.0f, 0.72f, 0.08f, 1f);
            upperSpot.range = 280f;
            upperSpot.spotAngle = 92f;
            upperSpot.innerSpotAngle = 52f;
            upperSpot.intensity = 6800f;
            upperSpot.shadows = LightShadows.None;
            pulseLights.Add(upperSpot);
            baseIntensities.Add(6800f);

            GameObject underLightGo = new GameObject("Breach_UnderglowPointLight");
            underLightGo.transform.SetParent(root.transform, false);
            underLightGo.transform.localPosition = new Vector3(0f, 4f, -24f); // Z = 88 world, beneath/behind Slattern
            Light underPt = underLightGo.AddComponent<Light>();
            underPt.type = LightType.Point;
            underPt.color = new Color(1.0f, 0.56f, 0.08f, 1f);
            underPt.range = 125f;
            underPt.intensity = 2800f;
            underPt.shadows = LightShadows.None;
            pulseLights.Add(underPt);
            baseIntensities.Add(2800f);

            if (ctrl != null)
            {
                ctrl.breachPulseLights = pulseLights.ToArray();
                ctrl.breachBaseIntensities = baseIntensities.ToArray();
                ctrl.breachWebbingRenderers = webRenderers.ToArray();
                EditorUtility.SetDirty(ctrl);
            }

            // 7. Breach model & barrier kept non-collidable so Slattern never gets stuck in it
            GameObject breachBarrier = new GameObject("Breach_Invisible_Barrier");
            breachBarrier.transform.SetParent(root.transform, false);
            breachBarrier.transform.localPosition = new Vector3(0f, 60f, -8f);
            var mainWallCol = breachBarrier.AddComponent<BoxCollider>();
            mainWallCol.size = new Vector3(340f, 140f, 18f);
            mainWallCol.isTrigger = false;
            mainWallCol.enabled = false;

            // Wide open abyssal basin boundary colliders (X = ±155m)
            GameObject canyonWallL = new GameObject("Canyon_Invisible_Wall_L");
            canyonWallL.transform.SetParent(root.transform, false);
            canyonWallL.transform.position = new Vector3(-155f, 55f, -25f);
            var colL = canyonWallL.AddComponent<BoxCollider>();
            colL.size = new Vector3(16f, 130f, 320f);

            GameObject canyonWallR = new GameObject("Canyon_Invisible_Wall_R");
            canyonWallR.transform.SetParent(root.transform, false);
            canyonWallR.transform.position = new Vector3(155f, 55f, -25f);
            var colR = canyonWallR.AddComponent<BoxCollider>();
            colR.size = new Vector3(16f, 130f, 320f);

            EditorUtility.SetDirty(root);
            Debug.Log("<color=#ff7700><b>[The_Breach]</b></color> Built 106m Breach + torn Seafloor Chasm (#FF6B00->#FFB800 HDR 8.5), 44 obsidian teeth, 26 organic webbing strands, 16 floor veins, 14 volumetric god rays, and wide X=±155m / Z=95m barriers.");
        }

        private static void BoostJaegerUnderwaterFloodlights(GameObject jaegerGo)
        {
            if (jaegerGo == null) return;

            Shader beamShader = Shader.Find("PacificRim/VolumetricLightBeam");
            Material underwaterBeamMat = beamShader != null
                ? EnsureWeatherMaterial("Assets/Materials/PacificRim_UnderwaterFloodlightBeam.mat", beamShader, m =>
                {
                    m.SetColor("_Color", new Color(0.84f, 0.96f, 1.0f, 0.52f));
                    m.SetFloat("_Intensity", 3.45f);
                    m.SetFloat("_ConeApexFade", 0.08f);
                    m.SetFloat("_ConeBaseFade", 0.72f);
                    m.SetFloat("_RimPower", 1.9f);
                    m.SetFloat("_NoiseSpeed", 1.4f);
                    m.SetFloat("_NoiseScale", 8.5f);
                    m.SetFloat("_NoiseStrength", 0.36f);
                    m.SetFloat("_WaterLevel", -500f);
                })
                : null;

            foreach (var t in jaegerGo.GetComponentsInChildren<Transform>(true))
            {
                bool isHead = t.name == "LightBeamOriginHead";
                bool isTorso = t.name.StartsWith("LightBeamOriginUpperTorso");
                if (!isHead && !isTorso) continue;

                var spot = t.GetComponent<Light>();
                if (spot != null && spot.type == LightType.Spot)
                {
                    spot.range = isHead ? 260f : 230f;
                    spot.spotAngle = isHead ? 48f : 58f;
                    spot.innerSpotAngle = isHead ? 28f : 36f;
                    spot.intensity = isHead ? 3600f : 2800f;
                    spot.color = isHead ? new Color(0.92f, 0.98f, 1.0f) : new Color(0.84f, 0.94f, 1.0f);
                    EditorUtility.SetDirty(spot);
                }

                var beamChild = t.Find("VolumetricBeam");
                if (beamChild != null)
                {
                    beamChild.localScale = isHead
                        ? new Vector3(1.22f, 1.22f, 2.65f)
                        : new Vector3(1.32f, 1.32f, 2.45f);
                    var mr = beamChild.GetComponent<MeshRenderer>();
                    if (mr != null && underwaterBeamMat != null)
                    {
                        mr.sharedMaterial = underwaterBeamMat;
                        EditorUtility.SetDirty(mr);
                    }
                }
            }

            EditorUtility.SetDirty(jaegerGo);
        }

        private static T EnsureVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var comp))
            {
                comp = profile.Add<T>(true);
            }
            comp.name = typeof(T).Name;
            comp.active = true;
            if (!AssetDatabase.Contains(comp))
            {
                AssetDatabase.AddObjectToAsset(comp, profile);
            }
            return comp;
        }

        private static VolumeProfile EnsureAbyssalVolumeProfile()
        {
            EnsureFolder("Assets/Settings");
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AbyssalVolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, AbyssalVolumeProfilePath);
            }

            var bloom = EnsureVolumeComponent<Bloom>(profile);
            bloom.threshold.Override(1.02f);
            bloom.intensity.Override(1.38f);
            bloom.scatter.Override(0.72f);

            var splitToning = EnsureVolumeComponent<SplitToning>(profile);
            splitToning.shadows.Override(new Color(13f / 255f, 56f / 255f, 76f / 255f, 1f)); // Cool teal shadows
            splitToning.highlights.Override(new Color(1f, 173f / 255f, 56f / 255f, 1f));    // Warm amber highlights
            splitToning.balance.Override(-10f);

            var colorAdj = EnsureVolumeComponent<ColorAdjustments>(profile);
            colorAdj.postExposure.Override(0.28f);
            colorAdj.contrast.Override(11f);
            colorAdj.saturation.Override(6f);
            colorAdj.colorFilter.Override(new Color(0.88f, 0.95f, 1.0f, 1f));

            var chromatic = EnsureVolumeComponent<ChromaticAberration>(profile);
            chromatic.intensity.Override(0.12f);

            var vignette = EnsureVolumeComponent<Vignette>(profile);
            vignette.intensity.Override(0.24f);
            vignette.color.Override(new Color(2f / 255f, 10f / 255f, 15f / 255f, 1f));

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static Material EnsureBioluminescentJellyfishMaterial()
        {
            const int res = 128;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            for (int y = 0; y < res; y++)
            {
                float v = (float)y / (res - 1); // 1 = top of bell, 0 = bottom tentacles
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / (res - 1);
                    float dx = (u - 0.5f) * 2f;

                    // Translucent umbrella dome in upper half (v in 0.48..0.88)
                    float dyDome = (v - 0.68f) / 0.22f;
                    float domeDist = Mathf.Sqrt((dx / 0.42f) * (dx / 0.42f) + dyDome * dyDome);
                    float domeAlpha = v >= 0.56f ? (1f - SmoothstepRange(0.62f, 1.0f, domeDist)) : 0f;
                    float domeRim = v >= 0.56f ? Mathf.Exp(-Mathf.Pow((domeDist - 0.65f) / 0.24f, 2f)) * 0.65f : 0f;

                    // Delicate trailing bioluminescent tentacles below bell (v < 0.60)
                    float waveX = Mathf.Sin(v * 14f) * 0.045f;
                    float tentaclePattern = 0.5f + 0.5f * Mathf.Cos((dx + waveX) * 26f);
                    float envX = Mathf.Exp(-Mathf.Pow(dx / 0.34f, 2f));
                    float envY = v < 0.62f ? SmoothstepRange(0.04f, 0.56f, v) * (1f - SmoothstepRange(0.56f, 0.64f, v)) : 0f;
                    float tentacleAlpha = Mathf.Pow(tentaclePattern, 3.0f) * envX * envY * 0.85f;

                    float alpha = Mathf.Clamp01(domeAlpha * 0.85f + domeRim + tentacleAlpha);
                    Color bellColor = new Color(0.20f, 0.96f, 1.0f, alpha);
                    Color tentColor = new Color(0.05f, 0.68f, 1.0f, alpha);
                    Color col = Color.Lerp(tentColor, bellColor, Mathf.Clamp01(domeAlpha + domeRim));
                    col.a = alpha;
                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply(false, false);

            const string jellyTexPath = "Assets/Materials/BioluminescentJellyfish.png";
            EnsureFolder("Assets/Materials");
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath), jellyTexPath), png);
            AssetDatabase.ImportAsset(jellyTexPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(jellyTexPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            Texture2D jellyTex = AssetDatabase.LoadAssetAtPath<Texture2D>(jellyTexPath);

            Shader jellyShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("PacificRim/SplashParticle")
                ?? Shader.Find("Sprites/Default");
            return EnsureWeatherMaterial("Assets/Materials/PacificRim_BioluminescentJellyfish.mat", jellyShader, m =>
            {
                if (jellyTex != null)
                {
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", jellyTex);
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", jellyTex);
                }
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.25f, 2.2f, 2.8f, 0.95f));
                if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.25f, 2.2f, 2.8f, 0.95f));
                if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.25f, 0.95f, 1.0f, 0.95f));
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f); // Additive glow
            });
        }

        private static UnderwaterEnvironmentController BuildUnderwaterAtmosphereSystem(Transform followTarget, Camera mainCam)
        {
            DestroyAllNamed("UnderwaterAtmosphereSystem");

            GameObject root = new GameObject("UnderwaterAtmosphereSystem");
            root.transform.position = Vector3.zero;

            Color abyssFog = new Color(0.022f, 0.095f, 0.135f, 1f);
            Color abyssSky = new Color(0.060f, 0.155f, 0.225f, 1f);
            Color abyssEq = new Color(0.036f, 0.098f, 0.150f, 1f);
            Color abyssGnd = new Color(0.020f, 0.048f, 0.075f, 1f);

            var ctrl = root.AddComponent<UnderwaterEnvironmentController>();
            ctrl.followTarget = followTarget;
            ctrl.abyssalFogColor = abyssFog;
            ctrl.abyssalFogDensity = 0.0120f;
            ctrl.ambientSkyColor = abyssSky;
            ctrl.ambientEquatorColor = abyssEq;
            ctrl.ambientGroundColor = abyssGnd;
            ctrl.underwaterLightColor = new Color(0.24f, 0.54f, 0.75f, 1f);
            ctrl.underwaterLightIntensity = 0.55f;

            // Apply deep abyssal ocean fog & ambient immediately in Editor Scene settings
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = abyssFog;
            RenderSettings.fogDensity = ctrl.abyssalFogDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = abyssSky;
            RenderSettings.ambientEquatorColor = abyssEq;
            RenderSettings.ambientGroundColor = abyssGnd;

            // Dedicated Post-Processing Volume (Bloom 1.1, SplitToning teal/amber, Chromatic Aberration)
            VolumeProfile volProfile = EnsureAbyssalVolumeProfile();
            if (volProfile != null)
            {
                var vol = root.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.priority = 10f;
                vol.sharedProfile = volProfile;
                ctrl.globalVolume = vol;
            }

            Shader particleShader = Shader.Find("PacificRim/SplashParticle") ?? Shader.Find("Sprites/Default");

            // 1. Marine Snow & Suspended Abyssal Silt Specks (lit dynamically by floodlights & Breach!)
            Material snowMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_UnderwaterMarineSnow.mat", particleShader, m =>
            {
                m.SetColor("_TintColor", new Color(0.62f, 0.72f, 0.78f, 0.65f));
                m.SetFloat("_InvFade", 0.35f);
                m.SetFloat("_LightInfluence", 0.90f);
            });
            var snowGo = new GameObject("Underwater_MarineSnow");
            snowGo.transform.SetParent(root.transform, false);
            snowGo.transform.localPosition = new Vector3(0f, 24f, 0f);
            var snowPs = snowGo.AddComponent<ParticleSystem>();
            var snowMain = snowPs.main;
            snowMain.loop = true;
            snowMain.playOnAwake = true;
            snowMain.prewarm = true;
            snowMain.maxParticles = 950;
            snowMain.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 8.0f);
            snowMain.startSpeed = 0f;
            snowMain.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.42f);
            snowMain.startColor = new Color(0.75f, 0.85f, 0.92f, 0.58f);
            snowMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var snowEmission = snowPs.emission;
            snowEmission.enabled = true;
            snowEmission.rateOverTime = 135f;

            var snowShape = snowPs.shape;
            snowShape.enabled = true;
            snowShape.shapeType = ParticleSystemShapeType.Box;
            snowShape.scale = new Vector3(145f, 54f, 145f);

            var snowVel = snowPs.velocityOverLifetime;
            snowVel.enabled = true;
            snowVel.space = ParticleSystemSimulationSpace.World;
            snowVel.x = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            snowVel.y = new ParticleSystem.MinMaxCurve(-0.55f, 0.35f);
            snowVel.z = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);

            var snowNoise = snowPs.noise;
            snowNoise.enabled = true;
            snowNoise.strength = 0.75f;
            snowNoise.frequency = 0.35f;
            snowNoise.scrollSpeed = 0.45f;

            var snowRend = snowGo.GetComponent<ParticleSystemRenderer>();
            snowRend.renderMode = ParticleSystemRenderMode.Billboard;
            snowRend.sharedMaterial = snowMat;
            snowRend.shadowCastingMode = ShadowCastingMode.Off;
            snowRend.receiveShadows = false;
            ctrl.marineSnowParticles = snowPs;

            // 2. Rising Deep-Sea Bubbles (realistic thin-rim texture + dense multi-size micro/macro streams)
            Texture2D bubbleTex = EnsureRealisticBubbleTextureAsset();
            Material bubbleMat = EnsureWeatherMaterial("Assets/Materials/PacificRim_UnderwaterBubbles.mat", particleShader, m =>
            {
                if (bubbleTex != null)
                {
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", bubbleTex);
                    else m.mainTexture = bubbleTex;
                }
                m.SetColor("_TintColor", new Color(0.76f, 0.93f, 1.0f, 0.72f));
                m.SetFloat("_InvFade", 0.30f);
                m.SetFloat("_LightInfluence", 0.62f);
            });
            var bubbleGo = new GameObject("Underwater_RisingBubbles");
            bubbleGo.transform.SetParent(root.transform, false);
            bubbleGo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            var bubblePs = bubbleGo.AddComponent<ParticleSystem>();
            var bubbleMain = bubblePs.main;
            bubbleMain.loop = true;
            bubbleMain.playOnAwake = true;
            bubbleMain.prewarm = true;
            bubbleMain.maxParticles = 2400;
            bubbleMain.startLifetime = new ParticleSystem.MinMaxCurve(5.0f, 8.5f);
            bubbleMain.startSpeed = 0f;
            bubbleMain.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.30f);
            bubbleMain.startColor = new Color(0.86f, 0.96f, 1.0f, 0.80f);
            bubbleMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var bubbleEmission = bubblePs.emission;
            bubbleEmission.enabled = true;
            bubbleEmission.rateOverTime = 260f;

            var bubbleShape = bubblePs.shape;
            bubbleShape.enabled = true;
            bubbleShape.shapeType = ParticleSystemShapeType.Box;
            bubbleShape.scale = new Vector3(78f, 16f, 78f);

            var bubbleVel = bubblePs.velocityOverLifetime;
            bubbleVel.enabled = true;
            bubbleVel.space = ParticleSystemSimulationSpace.World;
            bubbleVel.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            bubbleVel.y = new ParticleSystem.MinMaxCurve(2.4f, 6.2f);
            bubbleVel.z = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);

            var bubbleNoise = bubblePs.noise;
            bubbleNoise.enabled = true;
            bubbleNoise.strength = 1.35f;
            bubbleNoise.frequency = 1.15f;
            bubbleNoise.scrollSpeed = 0.65f;
            bubbleNoise.octaveCount = 2;

            var bubbleRend = bubbleGo.GetComponent<ParticleSystemRenderer>();
            bubbleRend.renderMode = ParticleSystemRenderMode.Billboard;
            bubbleRend.sharedMaterial = bubbleMat;
            bubbleRend.shadowCastingMode = ShadowCastingMode.Off;
            bubbleRend.receiveShadows = false;
            bubbleRend.maxParticleSize = 0.45f;
            ctrl.risingBubblesParticles = bubblePs;

            // 3. Heavy Choking Teal-Black Seabed Silt & Fog Clouds
            Material siltMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_FogWisp.mat");
            var siltGo = new GameObject("Underwater_SeabedSiltWisps");
            siltGo.transform.SetParent(root.transform, false);
            siltGo.transform.localPosition = new Vector3(0f, 8f, 0f);
            var siltPs = siltGo.AddComponent<ParticleSystem>();
            var siltMain = siltPs.main;
            siltMain.loop = true;
            siltMain.playOnAwake = true;
            siltMain.prewarm = true;
            siltMain.maxParticles = 76;
            siltMain.startLifetime = new ParticleSystem.MinMaxCurve(6.0f, 10.5f);
            siltMain.startSpeed = 0f;
            siltMain.startSize = new ParticleSystem.MinMaxCurve(26f, 48f);
            siltMain.startColor = new Color(0.04f, 0.11f, 0.14f, 0.42f);
            siltMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var siltEmission = siltPs.emission;
            siltEmission.enabled = true;
            siltEmission.rateOverTime = 9.5f;

            var siltShape = siltPs.shape;
            siltShape.enabled = true;
            siltShape.shapeType = ParticleSystemShapeType.Box;
            siltShape.scale = new Vector3(145f, 16f, 145f);

            var siltVel = siltPs.velocityOverLifetime;
            siltVel.enabled = true;
            siltVel.space = ParticleSystemSimulationSpace.World;
            siltVel.x = 2.2f;
            siltVel.y = 0.18f;
            siltVel.z = 1.6f;

            var siltCol = siltPs.colorOverLifetime;
            siltCol.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.05f, 0.16f, 0.19f), 0f), new GradientColorKey(new Color(0.02f, 0.08f, 0.10f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.78f, 0.25f), new GradientAlphaKey(0.78f, 0.7f), new GradientAlphaKey(0f, 1f) });
            siltCol.color = g;

            var siltRend = siltGo.GetComponent<ParticleSystemRenderer>();
            siltRend.renderMode = ParticleSystemRenderMode.Billboard;
            if (siltMat != null) siltRend.sharedMaterial = siltMat;
            siltRend.shadowCastingMode = ShadowCastingMode.Off;
            siltRend.receiveShadows = false;
            ctrl.seabedSiltParticles = siltPs;

            // 4. Sparse, Tiny Bioluminescent Jellyfish drifting through the choking abyssal murk
            Material jellyMat = EnsureBioluminescentJellyfishMaterial();
            var jellyGo = new GameObject("Underwater_BioluminescentJellyfish");
            jellyGo.transform.SetParent(root.transform, false);
            jellyGo.transform.localPosition = new Vector3(0f, 20f, 0f);
            var jellyPs = jellyGo.AddComponent<ParticleSystem>();
            var jellyMain = jellyPs.main;
            jellyMain.loop = true;
            jellyMain.playOnAwake = true;
            jellyMain.prewarm = true;
            jellyMain.maxParticles = 55;
            jellyMain.startLifetime = new ParticleSystem.MinMaxCurve(7.5f, 12.0f);
            jellyMain.startSpeed = 0f;
            jellyMain.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.78f);
            jellyMain.startColor = new Color(0.28f, 0.92f, 1.0f, 0.90f);
            jellyMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var jellyEmit = jellyPs.emission;
            jellyEmit.enabled = true;
            jellyEmit.rateOverTime = 6.5f;

            var jellyShape = jellyPs.shape;
            jellyShape.enabled = true;
            jellyShape.shapeType = ParticleSystemShapeType.Box;
            jellyShape.scale = new Vector3(155f, 42f, 155f);

            var jellyVel = jellyPs.velocityOverLifetime;
            jellyVel.enabled = true;
            jellyVel.space = ParticleSystemSimulationSpace.World;
            jellyVel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            jellyVel.y = new ParticleSystem.MinMaxCurve(0.45f, 1.25f);
            jellyVel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

            var jellyNoise = jellyPs.noise;
            jellyNoise.enabled = true;
            jellyNoise.strength = 0.9f;
            jellyNoise.frequency = 0.25f;
            jellyNoise.scrollSpeed = 0.35f;

            var jellyCol = jellyPs.colorOverLifetime;
            jellyCol.enabled = true;
            Gradient jg = new Gradient();
            jg.SetKeys(
                new[] { new GradientColorKey(new Color(0.15f, 0.85f, 1.0f), 0f), new GradientColorKey(new Color(0.35f, 1.0f, 0.92f), 0.5f), new GradientColorKey(new Color(0.10f, 0.65f, 1.0f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.2f), new GradientAlphaKey(0.85f, 0.75f), new GradientAlphaKey(0f, 1f) });
            jellyCol.color = jg;

            var jellyRend = jellyGo.GetComponent<ParticleSystemRenderer>();
            jellyRend.renderMode = ParticleSystemRenderMode.Billboard;
            jellyRend.sharedMaterial = jellyMat;
            jellyRend.shadowCastingMode = ShadowCastingMode.Off;
            jellyRend.receiveShadows = false;
            ctrl.bioluminescentJellyfishParticles = jellyPs;

            if (mainCam != null)
            {
                ctrl.ApplyAbyssalAtmosphere();
            }

            EditorUtility.SetDirty(root);
            return ctrl;
        }

        private static Material EnsureWeatherMaterial(string assetPath, Shader shader, Action<Material> configure)
        {
            EnsureFolder("Assets/Materials");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                mat = new Material(shader ?? Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }
            configure?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Mesh EnsureSmoothBreachGodRayConeMesh()
        {
            const string meshPath = "Assets/Meshes/MarianaTrench/BreachGodRayCone.asset";
            const int radialSegs = 48;
            const int lengthSegs = 10;
            const float length = 75f;
            const float apexRadius = 0.55f;
            const float baseRadius = 11.2f;

            int cols = radialSegs + 1;
            int rows = lengthSegs + 1;
            Vector3[] verts = new Vector3[cols * rows];
            Vector3[] normals = new Vector3[verts.Length];
            Vector2[] uvs = new Vector2[verts.Length];
            int[] tris = new int[radialSegs * lengthSegs * 6];

            float slope = (baseRadius - apexRadius) / length;
            for (int r = 0; r < rows; r++)
            {
                float v = r / (float)lengthSegs;
                float z = v * length;
                float rad = Mathf.Lerp(apexRadius, baseRadius, v);
                for (int c = 0; c < cols; c++)
                {
                    float u = c / (float)radialSegs;
                    float ang = u * Mathf.PI * 2f;
                    float cos = Mathf.Cos(ang);
                    float sin = Mathf.Sin(ang);
                    int idx = r * cols + c;
                    verts[idx] = new Vector3(cos * rad, sin * rad, z);
                    normals[idx] = new Vector3(cos, sin, -slope).normalized;
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
                    int d = (r + 1) * cols + c;
                    int e = d + 1;
                    tris[ti++] = a; tris[ti++] = d; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = d; tris[ti++] = e;
                }
            }

            Mesh m = new Mesh { name = "BreachGodRayCone" };
            m.vertices = verts;
            m.normals = normals;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateBounds();
            return SaveOrUpdateMeshAsset(m, meshPath);
        }

        private static Texture2D EnsureRealisticBubbleTextureAsset()
        {
            const string texPath = "Assets/Materials/PacificRim_RealisticBubble.png";
            EnsureFolder("Assets/Materials");
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = 0f;
                    if (r < 1f)
                    {
                        float rim = Mathf.Exp(-Mathf.Pow((r - 0.84f) / 0.095f, 2f)) * 0.56f;
                        float outerSoft = SmoothstepRange(1.0f, 0.78f, r);
                        float interior = 0.045f * (1f - SmoothstepRange(0.0f, 0.88f, r));
                        float angle = Mathf.Atan2(v, u);
                        float crescent = Mathf.Pow(Mathf.Clamp01(Mathf.Cos(angle - 2.35f)), 4.0f) * Mathf.Exp(-Mathf.Pow((r - 0.66f) / 0.14f, 2f)) * 0.34f;
                        float glintDist = Mathf.Sqrt((u + 0.34f) * (u + 0.34f) + (v - 0.34f) * (v - 0.34f));
                        float glint = SmoothstepRange(0.11f, 0.02f, glintDist) * 0.28f;
                        a = Mathf.Clamp01((rim + interior + crescent + glint) * outerSoft);
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath), texPath), png);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        private static void DestroyAllNamed(params string[] names)
        {
            var set = new HashSet<string>(names, StringComparer.Ordinal);
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (go != null && set.Contains(go.name))
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        private static bool ComputeCombinedSkinnedBounds(GameObject root, out Bounds totalBounds)
        {
            totalBounds = new Bounds();
            bool hasBounds = false;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!hasBounds)
                {
                    totalBounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    totalBounds.Encapsulate(r.bounds);
                }
            }
            return hasBounds;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }
    }
}
#endif
