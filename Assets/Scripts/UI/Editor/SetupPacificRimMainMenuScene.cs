#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PacificRimUI.Editor
{
    public static class SetupPacificRimMainMenuScene
    {
        public const string MainMenuScenePath = "Assets/Scenes/MainMenuScene.unity";
        public const string Level1ScenePath = "Assets/Scenes/Save Striker Eureka.unity";
        public const string Level2ScenePath = "Assets/Scenes/Otachi Arena.unity";
        public const string Level3ScenePath = "Assets/Scenes/Operation Pitfall.unity";
        public const string Level3CutsceneScenePath = "Assets/Scenes/Operation Pitfall Cutscene.unity";
        public const string AnteverseScenePath = "Assets/Scenes/The Anteverse.unity";
        public const string CityHighwayScenePath = "Assets/Scenes/Hong Kong Neon Highway.unity";
        public const string GameplayScenePath = Level1ScenePath;
        public const string GameplaySceneName = MainMenuUI.Level1SceneName;
        public const string MainMenuSceneName = "MainMenuScene";

        [MenuItem("Pacific Rim/Setup Dedicated Main Menu Scene", false, 1)]
        public static void CreateAndSetupMainMenuScene()
        {
            // 1. Save any open scene changes before switching
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                EditorSceneManager.SaveOpenScenes();
            }

            // 2. Create a clean new scene for the Main Menu
            Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. Setup Stormy Environment & Skybox
            SetupEnvironmentLighting();

            // 4. Setup Seabed & Ocean Platform
            SetupSeabedAndOcean();

            // 5. Setup Showcase Jaeger Mech
            GameObject jaegerGo = SetupShowcaseJaeger();

            // 6. Setup Cinematic Main Camera with Slow Drift
            SetupCinematicCamera(jaegerGo);

            // 7. Setup Pacific Rim Audio & EventSystem & Main Menu UI
            SetupMainMenuCanvasUI();

            // 8. Save the newly created Main Menu Scene
            EditorSceneManager.SaveScene(menuScene, MainMenuScenePath);
            Debug.Log($"[PacificRimUI] Dedicated Main Menu scene saved successfully at '{MainMenuScenePath}'.");

            // 9. Register both scenes in EditorBuildSettings (MainMenuScene as 0, SampleScene as 1)
            UpdateBuildSettings();

            // 10. Update SampleScene UI configuration (starts directly in combat, hud open, menu closed)
            ConfigureSampleSceneUI();

            // 11. Finally, re-open MainMenuScene so it is the active scene in Editor
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Debug.Log("[PacificRimUI] Complete Main Menu setup finished! Press Play in Editor to test Main Menu and transition into Gameplay Scene.");
        }

        private static void SetupEnvironmentLighting()
        {
            // Key stormy directional light
            var keyLightGo = new GameObject("Directional Light (Key Storm)", typeof(Light));
            keyLightGo.transform.position = new Vector3(0f, 40f, 0f);
            keyLightGo.transform.rotation = Quaternion.Euler(24f, 135f, 0f);
            var keyLight = keyLightGo.GetComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.38f, 0.52f, 0.74f, 1f);
            keyLight.intensity = 1.35f;
            keyLight.shadows = LightShadows.Soft;

            // Fill/Rim back light
            var rimLightGo = new GameObject("Directional Light (Back Rim)", typeof(Light));
            rimLightGo.transform.position = new Vector3(0f, 40f, 0f);
            rimLightGo.transform.rotation = Quaternion.Euler(16f, -45f, 0f);
            var rimLight = rimLightGo.GetComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(0.22f, 0.44f, 0.70f, 1f);
            rimLight.intensity = 0.65f;
            rimLight.shadows = LightShadows.None;

            // Skybox & Ambient Lighting
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_StormSkybox.mat");
            if (skyMat != null)
            {
                RenderSettings.skybox = skyMat;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.12f, 0.18f, 0.28f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.12f, 0.18f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.04f, 0.06f);

            // Storm Fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0035f;
            RenderSettings.fogColor = new Color(0.05f, 0.08f, 0.14f);

            // Global Post-Processing Volume
            var volProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/PacificRim_StormVolumeProfile.asset");
            if (volProfile != null)
            {
                var volGo = new GameObject("Global_Storm_Volume", typeof(Volume));
                var volume = volGo.GetComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
                volume.profile = volProfile;
            }
        }

        private static void SetupSeabedAndOcean()
        {
            // Seabed Floor Platform
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Test_Platform";
            platform.transform.position = new Vector3(0f, -0.5f, 0f);
            platform.transform.rotation = Quaternion.identity;
            platform.transform.localScale = new Vector3(400f, 1f, 400f);
            platform.tag = "Ground";

            var seabedMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_Seabed.mat");
            if (seabedMat != null)
            {
                var rend = platform.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = seabedMat;
            }

            // Visual Ocean Plane
            var oceanPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            oceanPlane.name = "Ocean_Display_Plane";
            oceanPlane.transform.position = new Vector3(0f, 4.0f, 0f);
            oceanPlane.transform.rotation = Quaternion.identity;
            oceanPlane.transform.localScale = new Vector3(40f, 1f, 40f);

            var oceanMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_Ocean.mat");
            if (oceanMat != null)
            {
                var oRend = oceanPlane.GetComponent<Renderer>();
                if (oRend != null) oRend.sharedMaterial = oceanMat;
            }
            var col = oceanPlane.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
        }

        private static GameObject SetupShowcaseJaeger()
        {
            string fbxPath = "Assets/Models/GipsyDangerV2.fbx";
            var fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxPrefab == null)
            {
                Debug.LogWarning("[PacificRimUI] GipsyDangerV2.fbx not found at " + fbxPath);
                return null;
            }

            var jaeger = UnityEngine.Object.Instantiate(fbxPrefab, Vector3.zero, Quaternion.Euler(0f, 195f, 0f));
            jaeger.name = "Showcase_Jaeger_Mech";

            var armorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_JaegerArmor.mat");
            if (armorMat != null)
            {
                foreach (var r in jaeger.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = armorMat;
                    if (r is SkinnedMeshRenderer smr)
                    {
                        smr.updateWhenOffscreen = true;
                    }
                }
            }

            var anim = jaeger.GetComponent<Animator>() ?? jaeger.AddComponent<Animator>();
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (a is Avatar av)
                {
                    anim.avatar = av;
                    break;
                }
            }

            var animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/GipsyDanger_Locomotion.controller");
            if (animController != null)
            {
                anim.runtimeAnimatorController = animController;
            }
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            return jaeger;
        }

        private static void SetupCinematicCamera(GameObject jaegerTarget)
        {
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(UniversalAdditionalCameraData), typeof(MainMenuCameraCinematic));
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(-13.5f, 15.5f, 32f);
            camGo.transform.rotation = Quaternion.Euler(11f, 158f, 0f);

            var cam = camGo.GetComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1200f;

            var uCam = camGo.GetComponent<UniversalAdditionalCameraData>();
            if (uCam != null)
            {
                uCam.renderPostProcessing = true;
            }

            var cinematic = camGo.GetComponent<MainMenuCameraCinematic>();
            if (cinematic != null && jaegerTarget != null)
            {
                cinematic.lookAtTarget = jaegerTarget.transform;
                cinematic.lookAtOffset = new Vector3(0f, 16.5f, 0f);
                cinematic.swaySpeed = 0.22f;
                cinematic.swayAmplitudeX = 0.9f;
                cinematic.swayAmplitudeY = 0.4f;
                cinematic.swayAmplitudeZ = 0.7f;
            }
        }

        [MenuItem("Pacific Rim/Rebuild Main Menu UI Only", false, 2)]
        public static void SetupMainMenuCanvasUI()
        {
            SetupPacificRimUIScene.EnsureEventSystem();
            SetupPacificRimUIScene.EnsureSoundManager();
            Canvas canvas = SetupPacificRimUIScene.EnsureCanvas();
            SetupPacificRimUIScene.CleanupOldRoots(canvas.transform);

            GameObject menuGo = SetupPacificRimUIScene.BuildMainMenu(canvas.transform, null, null);
            var mainMenu = menuGo.GetComponent<MainMenuUI>();

            mainMenu.isMainMenuScene = true;
            mainMenu.gameplaySceneName = GameplaySceneName;
            mainMenu.mainMenuSceneName = MainMenuSceneName;
            mainMenu.isMenuOpen = true;
            mainMenu.SetMenuState(true);

            EditorUtility.SetDirty(canvas.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        public static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(Level1ScenePath, true)
            };
            if (System.IO.File.Exists(Level2ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(Level2ScenePath, true));
            }
            if (System.IO.File.Exists(Level3CutsceneScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(Level3CutsceneScenePath, true));
            }
            if (System.IO.File.Exists(Level3ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(Level3ScenePath, true));
            }
            if (System.IO.File.Exists(AnteverseScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(AnteverseScenePath, true));
            }
            if (System.IO.File.Exists(CityHighwayScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(CityHighwayScenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[PacificRimUI] Build Settings updated: MainMenuScene, Save Striker Eureka (L1), Otachi Arena (L2), Operation Pitfall Cutscene, Operation Pitfall (L3), The Anteverse, Hong Kong Neon Highway.");
        }

        public static void ConfigureSampleSceneUI()
        {
            if (!System.IO.File.Exists(GameplayScenePath))
            {
                Debug.LogWarning("[PacificRimUI] Gameplay scene not found at " + GameplayScenePath);
                return;
            }

            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            SetupPacificRimUIScene.SetupUI();

            var sampleMainMenu = UnityEngine.Object.FindAnyObjectByType<MainMenuUI>();
            if (sampleMainMenu != null)
            {
                sampleMainMenu.isMainMenuScene = false;
                sampleMainMenu.gameplaySceneName = GameplaySceneName;
                sampleMainMenu.mainMenuSceneName = MainMenuSceneName;
                sampleMainMenu.isMenuOpen = false;
                sampleMainMenu.SetMenuState(false);
                EditorUtility.SetDirty(sampleMainMenu.gameObject);
            }

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[PacificRimUI] SampleScene configured for direct gameplay with in-game tactical pause menu.");
        }
    }
}
#endif
