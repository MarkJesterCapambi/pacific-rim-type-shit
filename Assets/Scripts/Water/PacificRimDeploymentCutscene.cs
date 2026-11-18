using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PacificRimAtmosphere;
using PacificRimAudio;
using PacificRimEnvironment;
using PacificRimUI;
using PacificRimWater;

namespace PacificRimCutscene
{
    /// <summary>
    /// Orchestrates the theatrical ~18-second Level 1 (Leatherback) intro cutscene in Save Striker Eureka:
    /// - 0:00–0:05 | Interrupted Slam & Spotlights: Slumped EMP'd Striker in waist-deep harbor water,
    ///               Leatherback mid-roar with fists raised for an overhead slam, frozen by harsh white
    ///               Chinook volumetric spotlights before squinting and turning back toward the bay.
    /// - 0:05–0:08 | Over-the-Shoulder "Deity" Reveal: Smooth orbit past Leatherback's shoulder as
    ///               blinding white godrays and Gipsy's glowing core descend from the storm clouds.
    /// - 0:08–0:11 | Sea-Level Upward Drop: Locked static low-angle camera directly under Gipsy's
    ///               right foot looking straight up as clamps release and Gipsy plummets toward camera.
    /// - 0:11–0:14 | Split-Frame Splashdown: Sea-level camera beside Gipsy's right foot catching the
    ///               harbor impact wave & physical lens droplets while Gipsy's dark silhouette frames
    ///               the left foreground against Leatherback centered across the water.
    /// - 0:14–0:18 | The Core Glow & Foghorn: Low-angle left shot angled up at Gipsy's glowing orange
    ///               chest vortex turbine as the resonant foghorn blasts and droplets streak down the lens,
    ///               followed by a crisp fade to black into playable combat.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class PacificRimDeploymentCutscene : MonoBehaviour
    {
        public static PacificRimDeploymentCutscene Instance { get; private set; }

        [Header("Scene Targets & Staging")]
        public Transform jaegerTransform;
        public Transform strikerTransform;
        public Transform kaijuTransform;
        public Vector3 targetLandingPosition = new Vector3(0f, 2.977f, 0f);
        public float harborWaterLevel = 9.48f;

        [Header("Shot Timings (~18 Seconds)")]
        public float shot1InterruptedSlamDuration = 5.0f;
        public float shot2DeityRevealDuration = 3.0f;
        public float shot3UpwardDropDuration = 3.0f;
        public float shot4SplitSplashDuration = 3.0f;
        public float shot5CoreFoghornDuration = 4.0f;

        [Header("Assets & Prefabs")]
        public GameObject chinookPrefab;
        public Material cableMaterial;
        public Material volumetricBeamMaterial;
        public Mesh volumetricBeamConeMesh;
        public Material spotlightGlareMaterial;

        [Header("Cutscene Ocean Scale")]
        public float cutsceneOceanScaleMultiplier = 2.2f;

        [Header("Cinematic Letterbox & Fade UI")]
        public RectTransform letterboxRoot;
        public RectTransform topBar;
        public RectTransform bottomBar;
        public CanvasGroup letterboxCanvasGroup;
        public CanvasGroup fadeOverlayGroup;
        public Image fadeOverlayImage;
        public Text skipPromptText;

        [Header("Cutscene State")]
        public bool playOnStart = true;
        public bool isCutsceneRunning { get; private set; }
        public bool HasTriggeredFoghorn { get; private set; }

        // Convoy & spotlight glare rigs
        private class SpotlightGlareRig
        {
            public Transform spotOrigin;
            public Light spotLight;
            public Transform glareTransform;
            public MeshRenderer glareRenderer;
            public MaterialPropertyBlock mpb;
            public Color glareColor;
            public Vector3 baseAimDir;
            public float sweepSpeed;
            public float sweepPhase;
            public float sweepYawAmp;
            public float sweepPitchAmp;
            public float baseLightIntensity;
            public float baseGlareIntensity;
            public Vector3 baseGlareScale;
            public float glareOffsetTowardCam;
            public bool isBackdropGroup;
        }

        private readonly List<GameObject> activeChinooks = new List<GameObject>();
        private readonly List<GameObject> activeBackdropChinooks = new List<GameObject>();
        private readonly List<Vector3> backdropChinookBasePositions = new List<Vector3>();
        private readonly List<Quaternion> backdropChinookBaseRotations = new List<Quaternion>();
        private readonly List<LineRenderer> activeCables = new List<LineRenderer>();
        private readonly List<GameObject> activeSpotEmitters = new List<GameObject>();
        private readonly List<Light> activeSpotLights = new List<Light>();
        private readonly List<MeshRenderer> activeBeamRenderers = new List<MeshRenderer>();
        private readonly List<SpotlightGlareRig> activeGlareRigs = new List<SpotlightGlareRig>();
        private readonly List<Vector3> chinookLocalOffsets = new List<Vector3>
        {
            new Vector3(-16f, 40f, 12f),
            new Vector3(16f, 40f, 12f),
            new Vector3(-18f, 43f, -12f),
            new Vector3(18f, 43f, -12f)
        };

        // Cached combatant & camera controllers
        private SimpleJaegerController jaegerController;
        private JaegerVisualSockets visualSockets;
        private Animator jaegerAnimator;
        private Rigidbody jaegerRigidbody;
        private Transform jaegerChest;
        private Transform hipsBone, chestBone, headBone;
        private Transform thighL, shinL, footL, thighR, shinR, footR;
        private Renderer turbineRenderer;
        private Material visorMatInstance;
        private Material turbineMatInstance;
        private Light coreGlowLight;
        private Light visorGlowLight;

        private KaijuAI kaijuAI;
        private Animator kaijuAnimator;
        private Transform kaijuTorsoBone, kaijuNeckBone, kaijuHeadBone, kaijuJawBone;
        private Transform kaijuShoulderL;

        private Camera mainCamera;
        private MonoBehaviour cameraFollowScript;
        private PacificRimCameraShake cameraShake;
        private CameraLensRainController lensRainController;
        private OceanWaterMesh oceanWaterMesh;
        private WaterSystem waterSystem;
        private ChinookCombatPatrolSquadron patrolSquadron;
        private AtmosphereVolumeController atmosphereCtrl;

        private AudioSource chinookAudioSource;
        private Coroutine activeCutsceneRoutine;

        // Saved scene state restored cleanly when combat starts
        private float savedOceanWidth;
        private float savedOceanLength;
        private bool hasExpandedOcean = false;
        private bool savedAtmosphere = false;
        private Color savedStormSkyAmbient, savedStormEqAmbient, savedStormGroundAmbient, savedFogColor;
        private float savedFogDensity, savedMainLightIntensity, savedPostExposure;
        private float savedBloomIntensity, savedBloomThreshold, savedBloomScatter;
        private float activeHarborSpotFactor = 0f;
        private float activeBackdropGlareFactor = 0f;
        private Material runtimeGlareMaterial;
        private Mesh runtimeGlareQuadMesh;
        private readonly List<Renderer> strikerEmissiveRenderers = new List<Renderer>();

        // LateUpdate procedural pose overrides
        private float cutsceneTime = 0f;
        private bool driveKaijuTurnPose = false;
        private float kaijuSquintWeight = 0f;
        private float kaijuTurnProgress = 0f;
        private float currentCrouchWeight = 0f;
        private Vector3 cameraLockPos;
        private Quaternion cameraLockRot = Quaternion.identity;
        private float cameraLockFov = 50f;
        private bool lockCameraInLateUpdate = false;

        private static readonly int IdEmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
        private static readonly int IdColor = Shader.PropertyToID("_Color");
        private static readonly int IdWaterLevel = Shader.PropertyToID("_WaterLevel");
        private static readonly int IdGlareColor = Shader.PropertyToID("_GlareColor");
        private static readonly int IdBeamAlignment = Shader.PropertyToID("_BeamAlignment");
        private static readonly int IdAspect = Shader.PropertyToID("_Aspect");
        private static readonly int IdStreakLength = Shader.PropertyToID("_StreakLength");
        private static readonly int IdSoftness = Shader.PropertyToID("_Softness");

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            ResolveReferences();

            if (playOnStart)
            {
                PrepareInitialStaging();
            }

            if (PacificRimLoadingScreen.Instance != null && PacificRimLoadingScreen.Instance.IsLoading)
            {
                return;
            }

            if (playOnStart)
            {
                StartCoroutine(PreloadAndStartCutscene());
            }
        }

        public void StartCutscene()
        {
            if (isCutsceneRunning) return;
            ResolveReferences();
            if (activeCutsceneRoutine != null) StopCoroutine(activeCutsceneRoutine);
            activeCutsceneRoutine = StartCoroutine(RunDeploymentCutscene());
        }

        private IEnumerator PreloadAndStartCutscene()
        {
            yield return PacificRimLoadingScreen.PreloadMainSceneAssetsRoutine();
            StartCutscene();
        }

        public void ResolveReferences()
        {
            if (jaegerTransform == null)
            {
                var jgo = GameObject.Find("Jaeger_Mech");
                if (jgo != null) jaegerTransform = jgo.transform;
            }
            if (jaegerTransform != null)
            {
                jaegerController = jaegerTransform.GetComponent<SimpleJaegerController>();
                visualSockets = jaegerTransform.GetComponent<JaegerVisualSockets>();
                jaegerAnimator = jaegerTransform.GetComponent<Animator>();
                jaegerRigidbody = jaegerTransform.GetComponent<Rigidbody>();
                ResolveJaegerBones();
                SetupGipsyGlowMaterialsAndLights();
            }

            if (kaijuTransform == null)
            {
                var kgo = GameObject.Find("Kaiju_Leatherback");
                if (kgo != null) kaijuTransform = kgo.transform;
            }
            if (kaijuTransform != null)
            {
                kaijuAI = kaijuTransform.GetComponent<KaijuAI>();
                kaijuAnimator = kaijuTransform.GetComponentInChildren<Animator>();
                ResolveKaijuBones();
            }

            if (strikerTransform == null)
            {
                var sgo = GameObject.Find("striker");
                if (sgo != null) strikerTransform = sgo.transform;
            }

            if (oceanWaterMesh == null) oceanWaterMesh = FindAnyObjectByType<OceanWaterMesh>();
            if (waterSystem == null)
            {
                waterSystem = WaterSystem.Instance;
                if (waterSystem == null) waterSystem = FindAnyObjectByType<WaterSystem>();
            }
            if (waterSystem != null) harborWaterLevel = waterSystem.MeanSurfaceY;

            if (patrolSquadron == null) patrolSquadron = FindAnyObjectByType<ChinookCombatPatrolSquadron>();
            if (atmosphereCtrl == null)
            {
                atmosphereCtrl = AtmosphereVolumeController.Instance;
                if (atmosphereCtrl == null) atmosphereCtrl = FindAnyObjectByType<AtmosphereVolumeController>();
            }

            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraFollowScript = mainCamera.GetComponent<JaegerCameraFollow>();
                cameraShake = mainCamera.GetComponent<PacificRimCameraShake>();
                if (cameraShake == null) cameraShake = mainCamera.gameObject.AddComponent<PacificRimCameraShake>();
                lensRainController = mainCamera.GetComponent<CameraLensRainController>();
            }

            LoadCutsceneAssetsIfNeeded();
            EnsureLetterboxUI();
        }

        private void LoadCutsceneAssetsIfNeeded()
        {
#if UNITY_EDITOR
            if (chinookPrefab == null)
                chinookPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PacificRim_Chinook.prefab");
            if (cableMaterial == null)
                cableMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_CableWire.mat");
            if (volumetricBeamMaterial == null)
                volumetricBeamMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_VolumetricBeam.mat");
            if (volumetricBeamConeMesh == null)
                volumetricBeamConeMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/PacificRim_VolumetricBeamCone.asset");
            if (spotlightGlareMaterial == null)
                spotlightGlareMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_SpotlightGlare.mat");
#endif
        }

        private void ResolveJaegerBones()
        {
            if (jaegerTransform == null) return;
            foreach (var t in jaegerTransform.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Hips": hipsBone = t; break;
                    case "Chest": jaegerChest = t; chestBone = t; break;
                    case "Head": headBone = t; break;
                    case "Thigh.L": thighL = t; break;
                    case "Shin.L": shinL = t; break;
                    case "Foot.L": footL = t; break;
                    case "Thigh.R": thighR = t; break;
                    case "Shin.R": shinR = t; break;
                    case "Foot.R": footR = t; break;
                }
            }
            var turbTr = jaegerTransform.Find("Spinning Blade.002");
            if (turbTr != null) turbineRenderer = turbTr.GetComponent<Renderer>();
        }

        private void ResolveKaijuBones()
        {
            if (kaijuTransform == null) return;
            foreach (var t in kaijuTransform.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "UpperTorsoBone": kaijuTorsoBone = t; break;
                    case "NeckBone": kaijuNeckBone = t; break;
                    case "UpperHead": kaijuHeadBone = t; break;
                    case "LowerJaw": kaijuJawBone = t; break;
                    case "ShoulderBone.L": kaijuShoulderL = t; break;
                }
            }
        }

        private void SetupGipsyGlowMaterialsAndLights()
        {
            if (!Application.isPlaying || jaegerTransform == null) return;
            foreach (var smr in jaegerTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMaterial == null) continue;
                if (smr.name == "Visor" && visorMatInstance == null)
                {
                    visorMatInstance = new Material(smr.sharedMaterial) { name = "Visor_DeploymentCutscene" };
                    visorMatInstance.EnableKeyword("_EMISSION");
                    visorMatInstance.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    smr.sharedMaterial = visorMatInstance;
                }
                else if (smr.name.Contains("Spinning") && turbineMatInstance == null)
                {
                    turbineMatInstance = new Material(smr.sharedMaterial) { name = "Turbine_DeploymentCutscene" };
                    turbineMatInstance.EnableKeyword("_EMISSION");
                    turbineMatInstance.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    smr.sharedMaterial = turbineMatInstance;
                }
            }

            if (chestBone != null && coreGlowLight == null)
            {
                var cg = new GameObject("Cutscene_CoreGlowLight");
                cg.transform.SetParent(chestBone, false);
                cg.transform.localPosition = new Vector3(0f, 0.0005f, -0.0025f);
                coreGlowLight = cg.AddComponent<Light>();
                coreGlowLight.type = LightType.Point;
                coreGlowLight.color = new Color(1.0f, 0.50f, 0.06f, 1f);
                coreGlowLight.range = 28f;
                coreGlowLight.intensity = 18f;
                coreGlowLight.shadows = LightShadows.None;
            }

            if (headBone != null && visorGlowLight == null)
            {
                var vg = new GameObject("Cutscene_VisorGlowLight");
                vg.transform.SetParent(headBone, false);
                vg.transform.localPosition = new Vector3(0f, 0f, -0.002f);
                visorGlowLight = vg.AddComponent<Light>();
                visorGlowLight.type = LightType.Point;
                visorGlowLight.color = new Color(1.0f, 0.62f, 0.14f, 1f);
                visorGlowLight.range = 18f;
                visorGlowLight.intensity = 10f;
                visorGlowLight.shadows = LightShadows.None;
            }

            SetGipsyCoreAndVisorGlow(visorStrength: 3.4f, coreStrength: 4.2f, lightScale: 1.0f);
        }

        private void PrepareInitialStaging()
        {
            if (jaegerController != null) jaegerController.isCutsceneActive = true;
            if (JaegerHUD.Instance != null) JaegerHUD.Instance.SetCutsceneHidden(true);
            if (cameraFollowScript != null) cameraFollowScript.enabled = false;
            if (patrolSquadron != null) patrolSquadron.gameObject.SetActive(false);

            if (fadeOverlayGroup != null) fadeOverlayGroup.alpha = 1f;
            if (letterboxCanvasGroup != null) letterboxCanvasGroup.alpha = 1f;

            SlumpAndPowerDownStriker();

            if (kaijuAI != null)
            {
                kaijuAI.SetCutscenePose(0f);
            }
        }

        private void SlumpAndPowerDownStriker()
        {
            if (strikerTransform == null) return;

            // Kill all lights and emission maps on Striker Eureka (EMP disabled)
            foreach (var l in strikerTransform.GetComponentsInChildren<Light>(true))
            {
                l.enabled = false;
            }
            strikerEmissiveRenderers.Clear();
            foreach (var rend in strikerTransform.GetComponentsInChildren<Renderer>(true))
            {
                if (rend == null) continue;
                var mats = rend.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    mats[i] = new Material(mats[i]);
                    mats[i].DisableKeyword("_EMISSION");
                    if (mats[i].HasProperty(IdEmissionColor))
                    {
                        mats[i].SetColor(IdEmissionColor, Color.black);
                    }
                }
                rend.sharedMaterials = mats;
                strikerEmissiveRenderers.Add(rend);
            }

            // Compute visual bounds center (Striker FBX pivot sits offset from mesh) and slump it waist-deep in harbor water
            Bounds b = new Bounds(strikerTransform.position, Vector3.one);
            bool hasB = false;
            foreach (var r in strikerTransform.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasB) { b = r.bounds; hasB = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!hasB) return;

            // Only slump once if still upright near initial min Y (~3m)
            if (b.center.y > 12.5f)
            {
                Vector3 hipPivot = new Vector3(b.center.x, harborWaterLevel + 2.0f, b.center.z);
                strikerTransform.RotateAround(hipPivot, Vector3.left, 20f);
                strikerTransform.RotateAround(hipPivot, Vector3.forward, 9f);
                strikerTransform.position += new Vector3(1.8f, -3.6f, -0.5f);
            }
        }

        private void SetGipsyCoreAndVisorGlow(float visorStrength, float coreStrength, float lightScale)
        {
            if (visorMatInstance != null)
            {
                Color baseCol = new Color(1.0f, 0.65f, 0.14f, 1f);
                Color emCol = new Color(1.0f, 0.56f, 0.06f, 1f) * visorStrength;
                if (visorMatInstance.HasProperty("_BaseColor")) visorMatInstance.SetColor("_BaseColor", baseCol);
                if (visorMatInstance.HasProperty(IdEmissionColor)) visorMatInstance.SetColor(IdEmissionColor, emCol);
            }
            if (turbineMatInstance != null)
            {
                Color emCol = new Color(1.0f, 0.44f, 0.03f, 1f) * coreStrength;
                if (turbineMatInstance.HasProperty(IdEmissionColor)) turbineMatInstance.SetColor(IdEmissionColor, emCol);
            }
            if (coreGlowLight != null) coreGlowLight.intensity = 28f * lightScale;
            if (visorGlowLight != null) visorGlowLight.intensity = 14f * lightScale;
        }

        public void EnsureLetterboxUI()
        {
            var canvasGo = GameObject.Find("PacificRim_UICanvas");
            if (canvasGo == null) return;

            if (letterboxRoot == null)
            {
                var existing = canvasGo.transform.Find("Cinematic_Letterbox");
                if (existing != null)
                {
                    letterboxRoot = existing.GetComponent<RectTransform>();
                    letterboxCanvasGroup = existing.GetComponent<CanvasGroup>();
                    topBar = existing.Find("TopBar") as RectTransform;
                    bottomBar = existing.Find("BottomBar") as RectTransform;
                    if (topBar != null && topBar.GetComponent<Image>() != null) topBar.GetComponent<Image>().color = Color.black;
                    if (bottomBar != null && bottomBar.GetComponent<Image>() != null) bottomBar.GetComponent<Image>().color = Color.black;
                    var skip = existing.Find("BottomBar/SkipPrompt");
                    if (skip == null) skip = existing.Find("SkipPrompt");
                    if (skip != null)
                    {
                        skipPromptText = skip.GetComponent<Text>();
                        if (skipPromptText != null)
                        {
                            skipPromptText.supportRichText = true;
                            skipPromptText.font = PacificRimUITheme.GetDefaultFont();
                            skipPromptText.fontSize = 12;
                            skipPromptText.fontStyle = FontStyle.Bold;
                            skipPromptText.color = new Color(PacificRimUITheme.TextDim.r, PacificRimUITheme.TextDim.g, PacificRimUITheme.TextDim.b, 0.72f);
                            skipPromptText.text = "<color=#46c3ff>[SPACE]</color>  SKIP";
                        }
                    }
                }
                else
                {
                    var rootGo = new GameObject("Cinematic_Letterbox", typeof(RectTransform), typeof(CanvasGroup));
                    rootGo.transform.SetParent(canvasGo.transform, false);
                    letterboxRoot = rootGo.GetComponent<RectTransform>();
                    letterboxRoot.anchorMin = Vector2.zero;
                    letterboxRoot.anchorMax = Vector2.one;
                    letterboxRoot.offsetMin = Vector2.zero;
                    letterboxRoot.offsetMax = Vector2.zero;

                    letterboxCanvasGroup = rootGo.GetComponent<CanvasGroup>();
                    letterboxCanvasGroup.alpha = 1f;
                    letterboxCanvasGroup.blocksRaycasts = false;
                    letterboxCanvasGroup.interactable = false;

                    topBar = CreateBar("TopBar", letterboxRoot, true, 132f);
                    bottomBar = CreateBar("BottomBar", letterboxRoot, false, 132f);

                    var skipGo = new GameObject("SkipPrompt", typeof(RectTransform), typeof(Text));
                    skipGo.transform.SetParent(bottomBar, false);
                    var skipRt = skipGo.GetComponent<RectTransform>();
                    skipRt.anchorMin = new Vector2(0.5f, 0.5f);
                    skipRt.anchorMax = new Vector2(0.5f, 0.5f);
                    skipRt.sizeDelta = new Vector2(260f, 28f);
                    skipPromptText = skipGo.GetComponent<Text>();
                    skipPromptText.supportRichText = true;
                    skipPromptText.text = "<color=#46c3ff>[SPACE]</color>  SKIP";
                    skipPromptText.alignment = TextAnchor.MiddleCenter;
                    skipPromptText.fontSize = 12;
                    skipPromptText.fontStyle = FontStyle.Bold;
                    skipPromptText.font = PacificRimUITheme.GetDefaultFont();
                    skipPromptText.color = new Color(PacificRimUITheme.TextDim.r, PacificRimUITheme.TextDim.g, PacificRimUITheme.TextDim.b, 0.72f);
                }
            }

            if (fadeOverlayGroup == null)
            {
                var existingFade = canvasGo.transform.Find("Cutscene_FadeOverlay");
                GameObject fadeGo = existingFade != null
                    ? existingFade.gameObject
                    : new GameObject("Cutscene_FadeOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));

                if (existingFade == null) fadeGo.transform.SetParent(canvasGo.transform, false);
                fadeGo.transform.SetAsFirstSibling();

                var rt = fadeGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                fadeOverlayGroup = fadeGo.GetComponent<CanvasGroup>();
                fadeOverlayGroup.blocksRaycasts = false;
                fadeOverlayGroup.interactable = false;
                fadeOverlayImage = fadeGo.GetComponent<Image>();
                fadeOverlayImage.color = Color.black;
                fadeOverlayImage.raycastTarget = false;
            }
        }

        private static RectTransform CreateBar(string barName, Transform parent, bool isTop, float height)
        {
            var go = new GameObject(barName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = isTop ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
            rt.anchorMax = isTop ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
            rt.pivot = isTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, height);
            var img = go.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            return rt;
        }

        public IEnumerator RunDeploymentCutscene()
        {
            if (isCutsceneRunning || jaegerTransform == null) yield break;
            isCutsceneRunning = true;
            HasTriggeredFoghorn = false;
            cutsceneTime = 0f;
            driveKaijuTurnPose = false;
            kaijuSquintWeight = 0f;
            kaijuTurnProgress = 0f;
            currentCrouchWeight = 0f;

            PrepareInitialStaging();

            // Wait cleanly until loading screen fade-out completes so Shot 1's fade-in from black plays visibly
            while (PacificRimLoadingScreen.Instance != null && PacificRimLoadingScreen.Instance.IsLoading)
            {
                yield return null;
            }

            if (letterboxCanvasGroup != null) letterboxCanvasGroup.alpha = 0f;
            yield return StartCoroutine(PacificRimMissionBriefingOverlay.ShowSaveStrikerEurekaBriefing());
            if (letterboxCanvasGroup != null) letterboxCanvasGroup.alpha = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            ExpandOceanForCutscene();
            ApplyMoodyHarborAtmosphereOverride(enable: true);
            lockCameraInLateUpdate = true;

            // Place Gipsy in upper storm clouds for Beat 2 Deity Reveal (tucked until orbit turn begins)
            Vector3 deityStartPos = new Vector3(0f, 52f, 12f);
            Vector3 deityEndPos = new Vector3(0f, 34f, 10f);
            SetJaegerPose(new Vector3(0f, -200f, 12f), Quaternion.Euler(4f, 0f, 0f));
            if (jaegerAnimator != null)
            {
                jaegerAnimator.speed = 1f;
                jaegerAnimator.SetFloat("Speed", 0f);
                jaegerAnimator.SetBool("IsRunning", false);
                jaegerAnimator.SetBool("InCombat", false);
                jaegerAnimator.SetBool("IsBlocking", false);
                jaegerAnimator.Play("Idle", 0, 0f);
            }
            if (visualSockets != null)
            {
                visualSockets.SetCutsceneBeamMultiplier(1.0f);
                visualSockets.SetVolumetricConesVisible(false);
            }

            // Spawn 4-Chinook airlift convoy, Leatherback harbor spotlights, and behind-Gipsy Chinook spotlight array
            SpawnChinookSquadron(deityStartPos);
            SpawnHarborSpotlightEmitters();
            SpawnBackdropChinookArray();
            SetDeploymentFleetActive(false);
            SetHarborSpotlightsAndGodrays(spotFactor: 0f, godrayFactor: 0f);

            // =========================================================================
            // SHOT 1 (0:00 – 0:05): Interrupted Slam & Spotlights
            // Medium-wide front shot on Leatherback roaring with fists raised above slumped Striker;
            // at 2.4s Chinook rotor thumps and harsh spotlights freeze Leatherback mid-swing, making him
            // squint against the glare and slowly turn his head & shoulders toward the bay behind him.
            // =========================================================================
            Vector3 s1CamStart = new Vector3(-15.5f, 18.8f, 118.5f);
            Vector3 s1CamEnd = new Vector3(-13.8f, 19.6f, 114.2f);
            Vector3 s1LookStart = new Vector3(0.4f, 22.8f, 92.0f);
            Vector3 s1LookEnd = new Vector3(0.2f, 23.6f, 90.0f);

            SetCameraGoal(s1CamStart, Quaternion.LookRotation((s1LookStart - s1CamStart).normalized, Vector3.up), 52f, snapNow: true);

            if (kaijuAI != null) kaijuAI.SetCutscenePose(0f);
            if (kaijuAnimator != null)
            {
                kaijuAnimator.speed = 0.25f; // Wind up Slam slowly over ~2.35s cleanly to peak overhead raise
                kaijuAnimator.CrossFade("Slam", 0.05f, 0, 0f);
            }

            bool triggeredRoar = false;
            bool frozeSlamAndHitLights = false;
            float s1Elapsed = 0f;

            while (s1Elapsed < shot1InterruptedSlamDuration)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(HandleCutsceneSkipped()); yield break; }

                float dt = Time.deltaTime;
                s1Elapsed += dt;
                cutsceneTime += dt;
                float u = Mathf.Clamp01(s1Elapsed / shot1InterruptedSlamDuration);

                // Fade in from black during first 0.85s
                if (fadeOverlayGroup != null)
                {
                    fadeOverlayGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s1Elapsed / 0.85f));
                }

                // Primate EMP roar & water surge as Leatherback rears up to slam Striker
                if (!triggeredRoar && s1Elapsed >= 0.22f)
                {
                    triggeredRoar = true;
                    PacificRimAudioManager.Instance?.PlayKaijuEmpRoar(kaijuTransform.position + Vector3.up * 16f);
                    PacificRimAudioManager.Instance?.PlayKaijuAttackGrunt(kaijuTransform.position + Vector3.up * 14f);
                    WaterSystem.Instance?.AddFootstepWave(kaijuTransform.position + Vector3.forward * 6f, 22f, 1.8f);
                }

                // Freeze mid-swing at peak fist raise (~0.59 of Slam clip) when Chinook thumps & harsh white spotlights hit!
                if (!frozeSlamAndHitLights && s1Elapsed >= 2.38f)
                {
                    frozeSlamAndHitLights = true;
                    if (kaijuAnimator != null) kaijuAnimator.speed = 0f;
                    StartChinookAudio(0.24f);
                    SetHarborSpotlightsAndGodrays(spotFactor: 1.0f, godrayFactor: 0f);
                    driveKaijuTurnPose = true;
                    if (cameraShake != null) cameraShake.TriggerPlayerHitFeedback(isHeavy: false);
                }

                if (frozeSlamAndHitLights)
                {
                    float reactTime = s1Elapsed - 2.38f;
                    kaijuSquintWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(reactTime / 0.45f));
                    kaijuTurnProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((reactTime - 0.35f) / 2.25f));
                }

                // Gentle camera drift inside Shot 1 before orbiting cleanly into Shot 2
                float camEase = Mathf.SmoothStep(0f, 1f, u);
                Vector3 cPos = Vector3.Lerp(s1CamStart, s1CamEnd, camEase);
                Vector3 cAim = Vector3.Lerp(s1LookStart, s1LookEnd, camEase);
                SetCameraGoal(cPos, Quaternion.LookRotation((cAim - cPos).normalized, Vector3.up), Mathf.Lerp(52f, 49f, camEase));

                UpdateChinookConvoy(deityStartPos, cutsceneTime);
                UpdateCables();
                yield return null;
            }
            if (fadeOverlayGroup != null) fadeOverlayGroup.alpha = 0f;

            // =========================================================================
            // SHOT 2 (0:05 – 0:08): Over-the-Shoulder "Deity" Reveal
            // Smooth orbit past Leatherback's left shoulder out across the dark bay;
            // Gipsy descends silhouetted in front of a massive array of Chinooks sweeping
            // glare-blooming searchlights directly toward the camera.
            // =========================================================================
            HasTriggeredFoghorn = true; // Crossfades music into Suspense as Gipsy appears from clouds
            SetDeploymentFleetActive(true);
            SetJaegerPose(deityStartPos, Quaternion.Euler(4f, 0f, 0f));

            Vector3 s2MidPos = new Vector3(-24.0f, 23.2f, 86.0f);
            Vector3 s2EndPos = new Vector3(-12.5f, 24.5f, 66.0f);
            Vector3 s2EndAim = new Vector3(0.0f, 45.0f, 10.0f);

            float s2Elapsed = 0f;
            while (s2Elapsed < shot2DeityRevealDuration)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(HandleCutsceneSkipped()); yield break; }

                float dt = Time.deltaTime;
                s2Elapsed += dt;
                cutsceneTime += dt;
                float u = Mathf.Clamp01(s2Elapsed / shot2DeityRevealDuration);
                float smoothU = Mathf.SmoothStep(0f, 1f, u);
                float backdropOn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s2Elapsed / 0.42f));

                SetHarborSpotlightsAndGodrays(spotFactor: 0.45f, godrayFactor: backdropOn);
                SetSilhouetteDeityAtmosphere(backdropOn);

                Vector3 gipsyPos = Vector3.Lerp(deityStartPos, deityEndPos, smoothU);
                SetJaegerPose(gipsyPos, Quaternion.Euler(4f, 0f, 0f));
                UpdateChinookConvoy(gipsyPos, cutsceneTime);
                UpdateBackdropChinookArray(cutsceneTime);
                UpdateCables();

                // Quadratic Bezier orbit arc cleanly past Leatherback's left shoulder toward the bay
                float inv = 1f - smoothU;
                Vector3 orbitPos = inv * inv * s1CamEnd + 2f * inv * smoothU * s2MidPos + smoothU * smoothU * s2EndPos;
                Vector3 gipsyChestWorld = gipsyPos + Vector3.up * 16.5f;
                Vector3 orbitAim = Vector3.Lerp(s1LookEnd, Vector3.Lerp(s2EndAim, gipsyChestWorld, 0.55f), smoothU);

                SetCameraGoal(orbitPos, Quaternion.LookRotation((orbitAim - orbitPos).normalized, Vector3.up), Mathf.Lerp(49f, 46f, smoothU));
                SetGipsyCoreAndVisorGlow(visorStrength: 4.5f, coreStrength: 5.5f, lightScale: 1.15f);
                yield return null;
            }

            // =========================================================================
            // SHOT 3 (0:08 – 0:11): Sea-Level Upward Drop
            // Hard cut to locked, static camera directly beneath Gipsy's right foot looking straight up;
            // Chinooks release cables and Gipsy plummets straight down toward the sea-level lens.
            // =========================================================================
            driveKaijuTurnPose = false;
            if (kaijuAI != null) kaijuAI.SetCutscenePose(180f);
            if (kaijuAnimator != null)
            {
                kaijuAnimator.speed = 1f;
                kaijuAnimator.CrossFade("Idle", 0.25f, 0, 0f);
            }

            Vector3 dropStartPos = new Vector3(0f, 33.5f, 0f);
            Vector3 dropMidCutPos = new Vector3(0f, 10.6f, 0f);
            SetJaegerPose(dropStartPos, Quaternion.Euler(3f, 0f, 0f));
            UpdateChinookConvoy(dropStartPos, cutsceneTime);
            UpdateCables();

            Vector3 rFootWorld = footR != null ? footR.position : new Vector3(2.8f, dropStartPos.y + 1.3f, -1.5f);
            Vector3 staticUpCamPos = new Vector3(rFootWorld.x + 1.15f, harborWaterLevel + 2.25f, rFootWorld.z + 1.45f);
            Vector3 staticUpLook = new Vector3(rFootWorld.x * 0.40f, 46.0f, 0.2f);
            Quaternion staticUpRot = Quaternion.LookRotation((staticUpLook - staticUpCamPos).normalized, Vector3.up);

            // Hard cut snap (no damping across the cut!)
            SetCameraGoal(staticUpCamPos, staticUpRot, 58f, snapNow: true);
            SetHarborSpotlightsAndGodrays(spotFactor: 0.35f, godrayFactor: 0.90f);

            float s3Elapsed = 0f;
            bool releasedCables = false;

            while (s3Elapsed < shot3UpwardDropDuration)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(HandleCutsceneSkipped()); yield break; }

                float dt = Time.deltaTime;
                s3Elapsed += dt;
                cutsceneTime += dt;

                // Release clamps at 0.45s into Beat 3 -> 2.55s accelerating drop straight toward the static lens
                if (!releasedCables && s3Elapsed >= 0.45f)
                {
                    releasedCables = true;
                    ReleaseCablesAndPeelChinooks();
                }

                if (!releasedCables)
                {
                    float sway = Mathf.Sin(cutsceneTime * 2.2f) * 0.35f;
                    Vector3 holdPos = dropStartPos + Vector3.up * sway;
                    SetJaegerPose(holdPos, Quaternion.Euler(3f, 0f, 0f));
                    UpdateChinookConvoy(holdPos, cutsceneTime);
                    UpdateCables();
                }
                else
                {
                    float fallU = Mathf.Clamp01((s3Elapsed - 0.45f) / Mathf.Max(0.1f, shot3UpwardDropDuration - 0.45f));
                    float fallEase = fallU * fallU; // Accelerating gravity fall
                    Vector3 curPos = Vector3.Lerp(dropStartPos, dropMidCutPos, fallEase);
                    SetJaegerPose(curPos, Quaternion.Lerp(Quaternion.Euler(3f, 0f, 0f), Quaternion.identity, fallU));
                }

            // Keep Shot 3 camera strictly locked & stationary
                UpdateBackdropChinookArray(cutsceneTime);
                SetCameraGoal(staticUpCamPos, staticUpRot, 58f);
                yield return null;
            }

            // =========================================================================
            // SHOT 4 (0:11 – 0:14): Split-Frame Splashdown (Lens Water & Silhouette)
            // Hard cut to second sea-level camera beside Gipsy's right foot: Leatherback centered in
            // background while Gipsy plunges down on the left foreground, slamming waves onto the lens.
            // =========================================================================
            Vector3 s4CamPos = new Vector3(11.2f, harborWaterLevel + 2.0f, -13.5f);
            Vector3 s4LookTarget = new Vector3(-1.6f, 18.0f, kaijuTransform != null ? kaijuTransform.position.z : 79.43f);
            Quaternion s4CamRot = Quaternion.LookRotation((s4LookTarget - s4CamPos).normalized, Vector3.up);
            SetCameraGoal(s4CamPos, s4CamRot, 54f, snapNow: true);
            SetHarborSpotlightsAndGodrays(spotFactor: 0.15f, godrayFactor: 0f);
            SetSilhouetteDeityAtmosphere(0f);

            float s4Elapsed = 0f;
            float finishDropTime = 0.55f;
            bool triggeredImpact = false;

            while (s4Elapsed < shot4SplitSplashDuration)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(HandleCutsceneSkipped()); yield break; }

                float dt = Time.deltaTime;
                s4Elapsed += dt;
                cutsceneTime += dt;

                if (s4Elapsed < finishDropTime)
                {
                    float u = Mathf.Clamp01(s4Elapsed / finishDropTime);
                    Vector3 pos = Vector3.Lerp(dropMidCutPos, targetLandingPosition, u);
                    SetJaegerPose(pos, Quaternion.identity);
                    currentCrouchWeight = Mathf.SmoothStep(0f, 0.35f, u);
                }
                else
                {
                    if (!triggeredImpact)
                    {
                        triggeredImpact = true;
                        SetJaegerPose(targetLandingPosition, Quaternion.identity);
                        TriggerHarborSplashdownAndLensWater();
                    }

                    // Compress deeply on impact (~0.25s) then partially settle into heavy silhouetted crouch
                    float post = s4Elapsed - finishDropTime;
                    if (post < 0.28f)
                    {
                        currentCrouchWeight = Mathf.Lerp(0.35f, 0.92f, Mathf.SmoothStep(0f, 1f, post / 0.28f));
                    }
                    else
                    {
                        currentCrouchWeight = Mathf.Lerp(0.92f, 0.55f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((post - 0.28f) / 1.8f)));
                    }
                }

                // Subtle low ocean swell drift keeping Leatherback centered and Gipsy left-foreground silhouette
                Vector3 microDrift = new Vector3(0f, Mathf.Sin(s4Elapsed * 2.1f) * 0.18f, 0f);
                SetCameraGoal(s4CamPos + microDrift, s4CamRot, 54f);
                yield return null;
            }

            // =========================================================================
            // SHOT 5 (0:14 – 0:18): The Core Glow & Foghorn
            // Low-angle shot on Gipsy's left angled upward at glowing orange nuclear vortex turbine;
            // lens droplets streak down as iconic foghorn blasts across the harbor, then fade to black.
            // =========================================================================
            SetJaegerPose(targetLandingPosition, Quaternion.identity);
            SetHarborSpotlightsAndGodrays(spotFactor: 0.10f, godrayFactor: 0f);

            Vector3 s5CamStart = new Vector3(-9.2f, harborWaterLevel + 3.0f, 14.4f);
            Vector3 s5CamEnd = new Vector3(-8.4f, harborWaterLevel + 3.5f, 12.8f);

            float s5Elapsed = 0f;
            bool playedFoghorn = false;
            float fadeDuration = 0.45f;
            float fadeStartTime = Mathf.Max(2.5f, shot5CoreFoghornDuration - fadeDuration);

            while (s5Elapsed < shot5CoreFoghornDuration)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(HandleCutsceneSkipped()); yield break; }

                float dt = Time.deltaTime;
                s5Elapsed += dt;
                cutsceneTime += dt;
                float u = Mathf.Clamp01(s5Elapsed / shot5CoreFoghornDuration);

                // Finish standing tall out of Shot 4's crouch over the first 1.2s of Beat 5
                currentCrouchWeight = Mathf.Lerp(0.55f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s5Elapsed / 1.15f)));

                if (!playedFoghorn && s5Elapsed >= 0.28f)
                {
                    playedFoghorn = true;
                    Vector3 coreWorld = GetTurbineWorldCenter();
                    PacificRimAudioManager.Instance?.PlayGipsyFoghorn(coreWorld);
                    PacificRimAudioManager.Instance?.PlayHydraulicServo(coreWorld, 0.85f);
                    if (cameraShake != null) cameraShake.TriggerTurbineOverchargeFeedback();
                }

                // Pulse warm orange nuclear turbine core vividly while foghorn echoes across the dark bay
                float foghornPulse = playedFoghorn
                    ? Mathf.Exp(-Mathf.Max(0f, s5Elapsed - 0.28f) * 0.45f) * 2.4f
                    : 0f;
                SetGipsyCoreAndVisorGlow(
                    visorStrength: 4.5f + foghornPulse * 0.4f,
                    coreStrength: 5.5f + foghornPulse,
                    lightScale: 1.5f + foghornPulse * 0.45f);

                Vector3 coreCenter = GetTurbineWorldCenter();
                Vector3 cPos = Vector3.Lerp(s5CamStart, s5CamEnd, Mathf.SmoothStep(0f, 1f, u));
                Vector3 cAim = coreCenter + Vector3.up * 0.45f;
                SetCameraGoal(cPos, Quaternion.LookRotation((cAim - cPos).normalized, Vector3.up), Mathf.Lerp(42f, 39f, u), snapNow: s5Elapsed <= dt * 2f);

                if (s5Elapsed >= fadeStartTime && fadeOverlayGroup != null)
                {
                    fadeOverlayGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s5Elapsed - fadeStartTime) / fadeDuration));
                }

                yield return null;
            }

            if (fadeOverlayGroup != null) fadeOverlayGroup.alpha = 1f;
            yield return StartCoroutine(HandoffToGameplayRoutine(preBlackHold: 0.12f, fadeBackDuration: 0.55f));
        }

        private Vector3 GetTurbineWorldCenter()
        {
            if (turbineRenderer != null) return turbineRenderer.bounds.center;
            if (chestBone != null) return chestBone.position + Vector3.up * 0.65f + Vector3.forward * 1.4f;
            return targetLandingPosition + new Vector3(0.5f, 15.5f, 1.8f);
        }

        private void LateUpdate()
        {
            // Apply procedural Spine/Neck/Head reactions AFTER Animator & KaijuAI.LateUpdate
            if (driveKaijuTurnPose && kaijuTransform != null)
            {
                // Squint / recoil away from overhead Chinook glare, then crane head & torso toward -Z bay sky
                float pitchSquint = -14f * kaijuSquintWeight;
                float rollSquint = 11f * kaijuSquintWeight;
                float torsoYaw = -42f * kaijuTurnProgress;
                float neckYaw = -62f * kaijuTurnProgress;
                float headYaw = -30f * kaijuTurnProgress;

                if (kaijuTorsoBone != null && Mathf.Abs(torsoYaw) > 0.05f)
                {
                    kaijuTorsoBone.rotation = Quaternion.AngleAxis(torsoYaw, Vector3.up) * kaijuTorsoBone.rotation;
                }
                if (kaijuNeckBone != null)
                {
                    Quaternion qNeck = Quaternion.AngleAxis(neckYaw, Vector3.up) *
                                       Quaternion.AngleAxis(pitchSquint * 0.45f, kaijuTransform.right);
                    kaijuNeckBone.rotation = qNeck * kaijuNeckBone.rotation;
                }
                if (kaijuHeadBone != null)
                {
                    Quaternion qHead = Quaternion.AngleAxis(headYaw, Vector3.up) *
                                       Quaternion.AngleAxis(pitchSquint, kaijuTransform.right) *
                                       Quaternion.AngleAxis(rollSquint, kaijuTransform.forward);
                    kaijuHeadBone.rotation = qHead * kaijuHeadBone.rotation;
                }
            }

            // Heavy impact crouch compression for Beat 4 & smooth stand-up into Beat 5
            if (currentCrouchWeight > 0.005f && thighL != null && shinL != null && footL != null &&
                thighR != null && shinR != null && footR != null)
            {
                Vector3 fwd = jaegerTransform.forward;
                float dropY = 3.65f * currentCrouchWeight;

                Vector3 anchoredFootL = footL.position;
                Vector3 anchoredFootR = footR.position;

                if (hipsBone != null) hipsBone.position += Vector3.down * dropY;
                if (chestBone != null) chestBone.rotation = Quaternion.AngleAxis(16f * currentCrouchWeight, jaegerTransform.right) * chestBone.rotation;

                SolveTwoBoneIKWorld(thighL, shinL, footL, anchoredFootL, fwd);
                SolveTwoBoneIKWorld(thighR, shinR, footR, anchoredFootR, fwd);
            }

            if (lockCameraInLateUpdate && mainCamera != null)
            {
                Vector3 shakePos = cameraShake != null ? cameraShake.CurrentOffset : Vector3.zero;
                Vector3 shakeRot = cameraShake != null ? cameraShake.CurrentRotationOffset : Vector3.zero;
                mainCamera.transform.position = cameraLockPos + cameraLockRot * shakePos;
                mainCamera.transform.rotation = cameraLockRot * Quaternion.Euler(shakeRot);
                mainCamera.fieldOfView = cameraLockFov;
            }

            UpdateAllSpotlightsAndGlares();
        }

        private void SetCameraGoal(Vector3 pos, Quaternion rot, float fov, bool snapNow = false)
        {
            cameraLockPos = pos;
            cameraLockRot = rot;
            cameraLockFov = fov;
            if (snapNow && mainCamera != null)
            {
                mainCamera.transform.position = pos;
                mainCamera.transform.rotation = rot;
                mainCamera.fieldOfView = fov;
                if (lensRainController != null) lensRainController.ForceSyncNow();
            }
        }

        private void SetJaegerPose(Vector3 pos, Quaternion rot)
        {
            if (jaegerRigidbody != null)
            {
                jaegerRigidbody.isKinematic = true;
                jaegerRigidbody.useGravity = false;
                jaegerRigidbody.position = pos;
                jaegerRigidbody.rotation = rot;
            }
            if (jaegerTransform != null)
            {
                jaegerTransform.position = pos;
                jaegerTransform.rotation = rot;
            }
        }

        private void TriggerHarborSplashdownAndLensWater()
        {
            Vector3 footImpact = footR != null ? footR.position : targetLandingPosition;
            Vector3 impactSurface = new Vector3(footImpact.x, harborWaterLevel - 0.3f, footImpact.z);

            WaterSystem.Instance?.AddFootstepWave(impactSurface, 42f, 3.8f);
            if (WaterSplashManager.Instance != null)
            {
                WaterSplashManager.Instance.SpawnColossalTitanDropSplash(impactSurface);
                WaterSplashManager.Instance.SpawnBodySplash(impactSurface, Vector3.up, 3.4f, 3.2f);
                WaterSplashManager.Instance.SpawnFootstepSplash(impactSurface, 3.2f);
            }

            PacificRimAudioManager.Instance?.PlayOceanFootstep(impactSurface, 2.5f);
            PacificRimAudioManager.Instance?.PlayKaijuSlamImpact(impactSurface);
            PacificRimAudioManager.Instance?.PlayArmorCreak(targetLandingPosition + Vector3.up * 14f, 1.15f);

            if (cameraShake != null)
            {
                cameraShake.TriggerPlayerHitFeedback(isHeavy: true);
            }

            // Physical splash of sea droplets directly onto the camera lens (persists streaking through Shots 4 & 5)
            if (lensRainController != null)
            {
                lensRainController.TriggerLensWaterSplash(intensity: 1.45f, duration: 7.4f);
            }
        }

        private void SpawnChinookSquadron(Vector3 centerPos)
        {
            activeChinooks.Clear();
            activeCables.Clear();

            for (int i = 0; i < chinookLocalOffsets.Count; i++)
            {
                GameObject chinook = chinookPrefab != null
                    ? Instantiate(chinookPrefab)
                    : GameObject.CreatePrimitive(PrimitiveType.Cube);

                if (chinookPrefab == null) chinook.transform.localScale = new Vector3(4f, 4f, 18f);
                chinook.name = $"Cutscene_Chinook_{i + 1}";
                chinook.transform.position = centerPos + chinookLocalOffsets[i];
                chinook.transform.rotation = Quaternion.Euler(4f, 180f, 0f);
                activeChinooks.Add(chinook);

                // Replace volumetric cones with sweeping searchlights & camera-facing HDR glares
                var searchlight = chinook.transform.Find("ChinookSearchlight");
                if (searchlight != null)
                {
                    Vector3 aim = (new Vector3(-6f, 26f, 52f) - searchlight.position).normalized;
                    AttachSpotlightAndGlare(
                        parent: chinook.transform,
                        worldOrigin: searchlight.position,
                        aimDir: aim,
                        lightColor: new Color(0.84f, 0.94f, 1.0f, 1f),
                        spotIntensity: 4800f,
                        spotRange: 165f,
                        spotAngle: 48f,
                        glareColor: new Color(0.80f, 0.94f, 1.0f, 1f),
                        glareScale: new Vector3(9.5f, 3.2f, 1f),
                        glareIntensity: 1.45f,
                        sweepSpeed: 1.55f + i * 0.23f,
                        sweepPhase: i * 1.45f,
                        sweepYawAmp: 24f,
                        sweepPitchAmp: 12f,
                        glareOffsetTowardCam: 2.2f,
                        isBackdropGroup: true,
                        existingLight: searchlight.GetComponent<Light>());
                }

                // Suspension cable
                var cableGo = new GameObject($"Cutscene_SuspensionCable_{i + 1}", typeof(LineRenderer));
                cableGo.transform.SetParent(chinook.transform, false);
                var lr = cableGo.GetComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.42f;
                lr.endWidth = 0.42f;
                if (cableMaterial != null) lr.material = cableMaterial;
                else
                {
                    Shader s = Shader.Find("Universal Render Pipeline/Lit");
                    if (s == null) s = Shader.Find("Standard");
                    lr.material = new Material(s);
                }
                lr.startColor = new Color(0.16f, 0.18f, 0.21f, 1f);
                lr.endColor = new Color(0.28f, 0.32f, 0.36f, 1f);
                lr.useWorldSpace = true;
                activeCables.Add(lr);
            }
        }

        private void SpawnHarborSpotlightEmitters()
        {
            if (kaijuTransform == null) return;
            Vector3 kTarget = kaijuTransform.position + Vector3.up * 20f + Vector3.forward * 4f;

            // Two crisp overhead searchlight Chinooks illuminating Leatherback directly (no opaque beam cone)
            Vector3[] overLeatherback = new Vector3[]
            {
                new Vector3(-14f, 54f, 108f),
                new Vector3(14f, 56f, 106f)
            };

            for (int i = 0; i < overLeatherback.Length; i++)
            {
                GameObject emitGo = chinookPrefab != null ? Instantiate(chinookPrefab) : new GameObject();
                emitGo.name = $"Cutscene_SpotlightChinook_{i + 1}";
                emitGo.transform.position = overLeatherback[i];
                Vector3 dir = (kTarget - overLeatherback[i]).normalized;
                emitGo.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z), Vector3.up);
                activeSpotEmitters.Add(emitGo);

                DisablePrefabSearchlight(emitGo);

                AttachSpotlightAndGlare(
                    parent: emitGo.transform,
                    worldOrigin: overLeatherback[i] + Vector3.down * 1.1f,
                    aimDir: dir,
                    lightColor: new Color(0.95f, 0.98f, 1.0f, 1f),
                    spotIntensity: 6200f,
                    spotRange: 160f,
                    spotAngle: 54f,
                    glareColor: new Color(0.92f, 0.97f, 1.0f, 1f),
                    glareScale: new Vector3(6.0f, 2.2f, 1f),
                    glareIntensity: 1.25f,
                    sweepSpeed: 1.15f + i * 0.35f,
                    sweepPhase: i * 1.7f,
                    sweepYawAmp: 6.5f,
                    sweepPitchAmp: 3.5f,
                    glareOffsetTowardCam: 1.8f,
                    isBackdropGroup: false);
            }
        }

        private void SpawnBackdropChinookArray()
        {
            activeBackdropChinooks.Clear();
            backdropChinookBasePositions.Clear();
            backdropChinookBaseRotations.Clear();

            // 24-Chinook curved celestial armada behind Gipsy relative to Shot 2 & Shot 3 sightlines:
            // positioned cleanly behind Gipsy (z = -24..-62) so individual helicopter silhouettes, sweeping
            // spotlights, and HDR camera reflections frame Gipsy's dark figure cleanly in both Shots 2 & 3.
            Vector3[] backdropOffsets = new Vector3[]
            {
                // Lower-mid flanking tier
                new Vector3(-44f, 35f, -26f),
                new Vector3(-22f, 39f, -25f),
                new Vector3( 24f, 40f, -25f),
                new Vector3( 46f, 36f, -27f),
                new Vector3( -8f, 44f, -30f),
                new Vector3( 14f, 46f, -29f),

                // Mid torso/shoulder silhouette halo
                new Vector3(-54f, 54f, -32f),
                new Vector3(-32f, 56f, -26f),
                new Vector3(-16f, 60f, -25f),
                new Vector3( 18f, 61f, -25f),
                new Vector3( 36f, 57f, -27f),
                new Vector3( 56f, 54f, -33f),
                new Vector3(  2f, 56f, -38f),

                // Upper crown & storm-sky tiers
                new Vector3(-42f, 74f, -40f),
                new Vector3(-20f, 76f, -32f),
                new Vector3(  4f, 79f, -30f),
                new Vector3( 26f, 77f, -32f),
                new Vector3( 48f, 73f, -40f),
                new Vector3(-58f, 84f, -52f),
                new Vector3(-30f, 88f, -48f),
                new Vector3( -4f, 92f, -50f),
                new Vector3( 24f, 90f, -48f),
                new Vector3( 52f, 86f, -52f),
                new Vector3( 10f, 66f, -60f)
            };

            Vector3 focalTarget = new Vector3(-8f, 26f, 60f);

            for (int i = 0; i < backdropOffsets.Length; i++)
            {
                Vector3 pos = backdropOffsets[i];
                GameObject chinook = chinookPrefab != null
                    ? Instantiate(chinookPrefab)
                    : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (chinookPrefab == null) chinook.transform.localScale = new Vector3(4f, 4f, 18f);

                chinook.name = $"Cutscene_BackdropChinook_{i + 1:00}";
                Vector3 flatToCam = new Vector3(focalTarget.x - pos.x, 0f, focalTarget.z - pos.z);
                Quaternion baseRot = flatToCam.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(flatToCam.normalized, Vector3.up) * Quaternion.Euler(4f, 0f, 0f)
                    : Quaternion.identity;

                chinook.transform.SetPositionAndRotation(pos, baseRot);
                DisablePrefabSearchlight(chinook);

                activeBackdropChinooks.Add(chinook);
                backdropChinookBasePositions.Add(pos);
                backdropChinookBaseRotations.Add(baseRot);

                Vector3 aimPos = focalTarget + new Vector3(((i % 5) - 2) * 10f, ((i % 3) - 1) * 7f, (i % 4) * 5f);
                Vector3 spotPos = pos + Vector3.down * 1.4f;
                Vector3 aimDir = (aimPos - spotPos).normalized;

                float distScale = pos.z < -40f ? 1.15f : (pos.z < -26f ? 1.0f : 0.90f);
                Color glareTint = (i % 3 == 0)
                    ? new Color(0.74f, 0.90f, 1.0f, 1f)
                    : new Color(0.92f, 0.97f, 1.0f, 1f);

                AttachSpotlightAndGlare(
                    parent: chinook.transform,
                    worldOrigin: spotPos,
                    aimDir: aimDir,
                    lightColor: glareTint,
                    spotIntensity: 9000f,
                    spotRange: 175f,
                    spotAngle: 44f,
                    glareColor: glareTint,
                    glareScale: new Vector3(13.5f, 4.4f, 1f) * distScale,
                    glareIntensity: 2.15f + (i % 3) * 0.25f,
                    sweepSpeed: 1.45f + (i % 5) * 0.28f,
                    sweepPhase: i * 1.23f,
                    sweepYawAmp: 32f + (i % 3) * 5f,
                    sweepPitchAmp: 14f + (i % 2) * 3f,
                    glareOffsetTowardCam: 2.4f,
                    isBackdropGroup: true);
            }
        }

        private static void DisablePrefabSearchlight(GameObject chinook)
        {
            var pre = chinook.transform.Find("ChinookSearchlight");
            if (pre == null) return;
            var l = pre.GetComponent<Light>();
            if (l != null) l.enabled = false;
        }

        private void SetDeploymentFleetActive(bool active)
        {
            for (int i = 0; i < activeChinooks.Count; i++)
            {
                if (activeChinooks[i] != null) activeChinooks[i].SetActive(active);
            }
            for (int i = 0; i < activeBackdropChinooks.Count; i++)
            {
                if (activeBackdropChinooks[i] != null) activeBackdropChinooks[i].SetActive(active);
            }
        }

        private void AttachSpotlightAndGlare(
            Transform parent,
            Vector3 worldOrigin,
            Vector3 aimDir,
            Color lightColor,
            float spotIntensity,
            float spotRange,
            float spotAngle,
            Color glareColor,
            Vector3 glareScale,
            float glareIntensity,
            float sweepSpeed,
            float sweepPhase,
            float sweepYawAmp,
            float sweepPitchAmp,
            float glareOffsetTowardCam,
            bool isBackdropGroup,
            Light existingLight = null)
        {
            EnsureGlareResources();

            Transform spotOrigin;
            Light spot;
            if (existingLight != null)
            {
                spotOrigin = existingLight.transform;
                spot = existingLight;
            }
            else
            {
                var originGo = new GameObject("SpotOrigin");
                originGo.transform.SetParent(parent, false);
                originGo.transform.position = worldOrigin;
                spotOrigin = originGo.transform;
                spot = originGo.AddComponent<Light>();
            }

            spotOrigin.rotation = Quaternion.LookRotation(aimDir.normalized, Vector3.up);
            spot.type = LightType.Spot;
            spot.color = lightColor;
            spot.intensity = spotIntensity;
            spot.range = spotRange;
            spot.spotAngle = spotAngle;
            spot.innerSpotAngle = spotAngle * 0.52f;
            spot.shadows = LightShadows.None;
            activeSpotLights.Add(spot);

            var glareGo = new GameObject("Cutscene_SpotGlare");
            glareGo.transform.SetParent(spotOrigin, false);
            glareGo.transform.position = spotOrigin.position;
            glareGo.transform.localScale = glareScale;

            var mf = glareGo.AddComponent<MeshFilter>();
            mf.sharedMesh = runtimeGlareQuadMesh;
            var mr = glareGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = runtimeGlareMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.allowOcclusionWhenDynamic = false;

            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(IdGlareColor, glareColor);
            mpb.SetFloat(IdStreakLength, isBackdropGroup ? 1.85f : 1.15f);
            mpb.SetFloat(IdSoftness, isBackdropGroup ? 3.8f : 5.2f);
            mpb.SetFloat(IdAspect, glareScale.x / Mathf.Max(0.1f, glareScale.y));
            mr.SetPropertyBlock(mpb);

            activeGlareRigs.Add(new SpotlightGlareRig
            {
                spotOrigin = spotOrigin,
                spotLight = spot,
                glareTransform = glareGo.transform,
                glareRenderer = mr,
                mpb = mpb,
                glareColor = glareColor,
                baseAimDir = aimDir.normalized,
                sweepSpeed = sweepSpeed,
                sweepPhase = sweepPhase,
                sweepYawAmp = sweepYawAmp,
                sweepPitchAmp = sweepPitchAmp,
                baseLightIntensity = spotIntensity,
                baseGlareIntensity = glareIntensity,
                baseGlareScale = glareScale,
                glareOffsetTowardCam = glareOffsetTowardCam,
                isBackdropGroup = isBackdropGroup
            });
        }

        private void EnsureGlareResources()
        {
            if (runtimeGlareMaterial == null)
            {
                if (spotlightGlareMaterial != null)
                {
                    runtimeGlareMaterial = new Material(spotlightGlareMaterial) { name = "CutsceneSpotGlare_Runtime" };
                }
                else
                {
                    Shader s = Shader.Find("PacificRim/SpotlightGlare");
                    if (s == null) s = Shader.Find("PacificRim/AnamorphicFlare");
                    if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
                    runtimeGlareMaterial = new Material(s) { name = "CutsceneSpotGlare_FallbackRuntime" };
                }
            }

            if (runtimeGlareQuadMesh == null)
            {
                runtimeGlareQuadMesh = new Mesh { name = "CutsceneGlareQuad" };
                runtimeGlareQuadMesh.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3( 0.5f, -0.5f, 0f),
                    new Vector3(-0.5f,  0.5f, 0f),
                    new Vector3( 0.5f,  0.5f, 0f)
                };
                runtimeGlareQuadMesh.uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f)
                };
                runtimeGlareQuadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                runtimeGlareQuadMesh.RecalculateBounds();
            }
        }

        private void SetHarborSpotlightsAndGodrays(float spotFactor, float godrayFactor)
        {
            activeHarborSpotFactor = spotFactor;
            activeBackdropGlareFactor = godrayFactor;

            for (int i = 0; i < activeGlareRigs.Count; i++)
            {
                var rig = activeGlareRigs[i];
                if (rig == null) continue;
                float f = rig.isBackdropGroup ? activeBackdropGlareFactor : activeHarborSpotFactor;
                bool visible = f > 0.01f;
                if (rig.spotLight != null) rig.spotLight.enabled = visible;
                if (rig.glareRenderer != null) rig.glareRenderer.enabled = visible;
            }
        }

        private void UpdateAllSpotlightsAndGlares()
        {
            if (activeGlareRigs.Count == 0 || mainCamera == null) return;

            Vector3 camPos = mainCamera.transform.position;
            Quaternion camRot = mainCamera.transform.rotation;

            for (int i = 0; i < activeGlareRigs.Count; i++)
            {
                var rig = activeGlareRigs[i];
                if (rig == null || rig.spotOrigin == null) continue;

                float groupFactor = rig.isBackdropGroup ? activeBackdropGlareFactor : activeHarborSpotFactor;
                bool visible = groupFactor > 0.01f;

                if (rig.spotLight != null)
                {
                    rig.spotLight.enabled = visible;
                    rig.spotLight.intensity = rig.baseLightIntensity * groupFactor;
                }
                if (rig.glareRenderer != null)
                {
                    rig.glareRenderer.enabled = visible;
                }
                if (!visible) continue;

                float yaw = Mathf.Sin(cutsceneTime * rig.sweepSpeed + rig.sweepPhase) * rig.sweepYawAmp;
                float pitch = Mathf.Cos(cutsceneTime * (rig.sweepSpeed * 0.76f) + rig.sweepPhase * 1.25f) * rig.sweepPitchAmp;
                Vector3 aimDir = (Quaternion.Euler(pitch, yaw, 0f) * rig.baseAimDir).normalized;
                rig.spotOrigin.rotation = Quaternion.LookRotation(aimDir, Vector3.up);

                if (rig.glareTransform != null && rig.glareRenderer != null)
                {
                    Vector3 toCam = (camPos - rig.spotOrigin.position).normalized;
                    rig.glareTransform.position = rig.spotOrigin.position + toCam * rig.glareOffsetTowardCam;
                    rig.glareTransform.rotation = camRot;

                    float dotCam = Vector3.Dot(rig.spotOrigin.forward, toCam);
                    float align = Mathf.Clamp01((dotCam - 0.12f) / 0.88f);
                    float scaleBoost = Mathf.Lerp(0.75f, 1.30f, align);
                    rig.glareTransform.localScale = rig.baseGlareScale * scaleBoost;

                    float intensity = rig.baseGlareIntensity * groupFactor * Mathf.Lerp(0.45f, 1.38f, align);
                    rig.mpb.SetFloat(IdIntensity, intensity);
                    rig.mpb.SetFloat(IdBeamAlignment, align);
                    rig.mpb.SetFloat(IdAspect, rig.baseGlareScale.x / Mathf.Max(0.1f, rig.baseGlareScale.y));
                    rig.glareRenderer.SetPropertyBlock(rig.mpb);
                }
            }
        }

        private void UpdateBackdropChinookArray(float t)
        {
            for (int i = 0; i < activeBackdropChinooks.Count; i++)
            {
                var ch = activeBackdropChinooks[i];
                if (ch == null) continue;
                float bobY = Mathf.Sin(t * 1.8f + i * 1.1f) * 0.55f;
                float swayX = Mathf.Cos(t * 1.1f + i * 0.9f) * 0.35f;
                ch.transform.position = backdropChinookBasePositions[i] + new Vector3(swayX, bobY, 0f);
                ch.transform.rotation = backdropChinookBaseRotations[i] * Quaternion.Euler(bobY * 1.1f, 0f, swayX * 1.4f);
            }
        }

        private void UpdateChinookConvoy(Vector3 centerPos, float t)
        {
            for (int i = 0; i < activeChinooks.Count; i++)
            {
                if (activeChinooks[i] == null) continue;
                float bob = Mathf.Sin(t * 2.5f + i * 1.4f) * 0.45f;
                activeChinooks[i].transform.position = centerPos + chinookLocalOffsets[i] + Vector3.up * bob;
                activeChinooks[i].transform.rotation = Quaternion.Euler(4f + bob * 1.3f, 180f, 0f);
            }
        }

        private void UpdateCables()
        {
            Transform anchor = jaegerChest != null ? jaegerChest : jaegerTransform;
            Vector3 anchorPos = anchor.position + Vector3.up * 1.5f;
            for (int i = 0; i < activeCables.Count; i++)
            {
                if (activeCables[i] == null || i >= activeChinooks.Count || activeChinooks[i] == null) continue;
                Transform hook = activeChinooks[i].transform.Find("CargoHook");
                if (hook == null) hook = activeChinooks[i].transform;
                activeCables[i].SetPosition(0, hook.position);
                activeCables[i].SetPosition(1, anchorPos);
            }
        }

        private void ReleaseCablesAndPeelChinooks()
        {
            PacificRimAudioManager.Instance?.PlayDeploymentClampRelease(jaegerTransform.position + Vector3.up * 18f);
            for (int i = 0; i < activeCables.Count; i++)
            {
                if (activeCables[i] != null) Destroy(activeCables[i].gameObject);
            }
            activeCables.Clear();
            StartCoroutine(PeelChinooksUpRoutine());
        }

        private IEnumerator PeelChinooksUpRoutine()
        {
            float dur = 3.2f;
            float elapsed = 0f;
            float startVol = chinookAudioSource != null ? chinookAudioSource.volume : 0f;

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / dur);
                for (int i = 0; i < activeChinooks.Count; i++)
                {
                    if (activeChinooks[i] == null) continue;
                    Vector3 outDir = activeChinooks[i].transform.position - targetLandingPosition;
                    outDir.y = 0f;
                    outDir = outDir.sqrMagnitude > 0.01f ? outDir.normalized : Vector3.right;
                    activeChinooks[i].transform.position += (outDir * 16f + Vector3.up * 24f) * Time.deltaTime;
                }
                if (chinookAudioSource != null) chinookAudioSource.volume = Mathf.Lerp(startVol, 0f, u);
                yield return null;
            }

            CleanupCutsceneChinooksAndSpotlights();
        }

        private void CleanupCutsceneChinooksAndSpotlights()
        {
            for (int i = 0; i < activeCables.Count; i++)
            {
                if (activeCables[i] != null) Destroy(activeCables[i].gameObject);
            }
            activeCables.Clear();
            for (int i = 0; i < activeChinooks.Count; i++)
            {
                if (activeChinooks[i] != null) Destroy(activeChinooks[i]);
            }
            activeChinooks.Clear();
            for (int i = 0; i < activeBackdropChinooks.Count; i++)
            {
                if (activeBackdropChinooks[i] != null) Destroy(activeBackdropChinooks[i]);
            }
            activeBackdropChinooks.Clear();
            backdropChinookBasePositions.Clear();
            backdropChinookBaseRotations.Clear();
            for (int i = 0; i < activeSpotEmitters.Count; i++)
            {
                if (activeSpotEmitters[i] != null) Destroy(activeSpotEmitters[i]);
            }
            activeSpotEmitters.Clear();
            activeSpotLights.Clear();
            activeBeamRenderers.Clear();
            activeGlareRigs.Clear();
            activeHarborSpotFactor = 0f;
            activeBackdropGlareFactor = 0f;

            if (runtimeGlareMaterial != null)
            {
                Destroy(runtimeGlareMaterial);
                runtimeGlareMaterial = null;
            }
            if (runtimeGlareQuadMesh != null)
            {
                Destroy(runtimeGlareQuadMesh);
                runtimeGlareQuadMesh = null;
            }

            if (chinookAudioSource != null)
            {
                Destroy(chinookAudioSource.gameObject);
                chinookAudioSource = null;
            }
        }

        private void ApplyMoodyHarborAtmosphereOverride(bool enable)
        {
            if (atmosphereCtrl == null) return;
            if (enable)
            {
                if (!savedAtmosphere)
                {
                    savedAtmosphere = true;
                    savedStormSkyAmbient = atmosphereCtrl.stormSkyAmbient;
                    savedStormEqAmbient = atmosphereCtrl.stormEquatorAmbient;
                    savedStormGroundAmbient = atmosphereCtrl.stormGroundAmbient;
                    savedFogColor = atmosphereCtrl.fogColor;
                    savedFogDensity = atmosphereCtrl.fogDensity;
                    savedMainLightIntensity = atmosphereCtrl.mainLightIntensity;
                    savedPostExposure = atmosphereCtrl.postExposure;
                    savedBloomIntensity = atmosphereCtrl.bloomIntensity;
                    savedBloomThreshold = atmosphereCtrl.bloomThreshold;
                    savedBloomScatter = atmosphereCtrl.bloomScatter;
                }
                atmosphereCtrl.stormSkyAmbient = new Color(0.14f, 0.21f, 0.32f, 1f);
                atmosphereCtrl.stormEquatorAmbient = new Color(0.10f, 0.16f, 0.24f, 1f);
                atmosphereCtrl.stormGroundAmbient = new Color(0.05f, 0.08f, 0.13f, 1f);
                atmosphereCtrl.fogColor = new Color(0.075f, 0.125f, 0.195f, 1f);
                atmosphereCtrl.fogDensity = 0.0024f;
                atmosphereCtrl.mainLightIntensity = 1.05f;
                atmosphereCtrl.postExposure = 0.16f;
                atmosphereCtrl.bloomIntensity = 1.10f;
                atmosphereCtrl.bloomThreshold = 0.90f;
                atmosphereCtrl.bloomScatter = 0.80f;
                atmosphereCtrl.ApplyAtmosphericLightingAndFog();
                atmosphereCtrl.ConfigureGlobalVolumeProfile();
            }
            else if (savedAtmosphere)
            {
                atmosphereCtrl.stormSkyAmbient = savedStormSkyAmbient;
                atmosphereCtrl.stormEquatorAmbient = savedStormEqAmbient;
                atmosphereCtrl.stormGroundAmbient = savedStormGroundAmbient;
                atmosphereCtrl.fogColor = savedFogColor;
                atmosphereCtrl.fogDensity = savedFogDensity;
                atmosphereCtrl.mainLightIntensity = savedMainLightIntensity;
                atmosphereCtrl.postExposure = savedPostExposure;
                atmosphereCtrl.bloomIntensity = savedBloomIntensity;
                atmosphereCtrl.bloomThreshold = savedBloomThreshold;
                atmosphereCtrl.bloomScatter = savedBloomScatter;
                atmosphereCtrl.ApplyAtmosphericLightingAndFog();
                atmosphereCtrl.ConfigureGlobalVolumeProfile();
                savedAtmosphere = false;
            }
        }

        private void SetSilhouetteDeityAtmosphere(float deityBlend)
        {
            if (atmosphereCtrl == null || !savedAtmosphere) return;
            deityBlend = Mathf.Clamp01(deityBlend);
            atmosphereCtrl.mainLightIntensity = Mathf.Lerp(0.95f, 0.28f, deityBlend);
            atmosphereCtrl.stormSkyAmbient = Color.Lerp(new Color(0.14f, 0.21f, 0.32f, 1f), new Color(0.08f, 0.13f, 0.22f, 1f), deityBlend);
            atmosphereCtrl.stormEquatorAmbient = Color.Lerp(new Color(0.10f, 0.16f, 0.24f, 1f), new Color(0.06f, 0.09f, 0.16f, 1f), deityBlend);
            atmosphereCtrl.fogDensity = Mathf.Lerp(0.0024f, 0.0014f, deityBlend);
            atmosphereCtrl.bloomIntensity = Mathf.Lerp(1.05f, 0.96f, deityBlend);
            atmosphereCtrl.bloomThreshold = Mathf.Lerp(0.92f, 0.96f, deityBlend);
            atmosphereCtrl.bloomScatter = Mathf.Lerp(0.76f, 0.65f, deityBlend);
            atmosphereCtrl.ApplyAtmosphericLightingAndFog();
            atmosphereCtrl.ConfigureGlobalVolumeProfile();
        }

        private IEnumerator HandoffToGameplayRoutine(float preBlackHold, float fadeBackDuration)
        {
            if (preBlackHold > 0f) yield return new WaitForSeconds(preBlackHold);

            driveKaijuTurnPose = false;
            currentCrouchWeight = 0f;
            lockCameraInLateUpdate = false;
            CleanupCutsceneChinooksAndSpotlights();
            RestoreOceanAfterCutscene();
            ApplyMoodyHarborAtmosphereOverride(enable: false);

            if (lensRainController != null) lensRainController.ClearLensWaterSplash();
            if (visualSockets != null)
            {
                visualSockets.SetVolumetricConesVisible(true);
                visualSockets.SetCutsceneBeamMultiplier(1.0f);
            }
            SetGipsyCoreAndVisorGlow(visorStrength: 2.2f, coreStrength: 2.8f, lightScale: 0.4f);

            SetJaegerPose(targetLandingPosition, Quaternion.identity);
            if (jaegerRigidbody != null)
            {
                jaegerRigidbody.isKinematic = false;
                jaegerRigidbody.useGravity = true;
#if UNITY_6000_0_OR_NEWER
                jaegerRigidbody.linearVelocity = Vector3.zero;
#else
                jaegerRigidbody.velocity = Vector3.zero;
#endif
            }

            if (kaijuAI != null)
            {
                kaijuAI.SetCutscenePose(180f);
                if (kaijuAnimator != null)
                {
                    kaijuAnimator.speed = 1f;
                    kaijuAnimator.Play("Idle", 0, 0f);
                }
                kaijuAI.isCutsceneMode = false;
            }

            if (patrolSquadron != null) patrolSquadron.gameObject.SetActive(true);
            if (letterboxCanvasGroup != null) letterboxCanvasGroup.alpha = 0f;

            // Snap combat camera cleanly behind Gipsy while screen is black
            if (cameraFollowScript != null) cameraFollowScript.enabled = true;
            if (JaegerCameraFollow.Instance != null)
            {
                JaegerCameraFollow.Instance.CurrentYaw = 0f;
                JaegerCameraFollow.Instance.SmoothTransitionToOverTheShoulder(0.05f);
            }

            // Reveal combat HUD & fade smoothly out of black into playable battle
            bool bootDone = false;
            if (JaegerHUD.Instance != null)
            {
                JaegerHUD.Instance.SetCutsceneHidden(false);
                JaegerHUD.Instance.PlayNeuralBootSequence(() => bootDone = true);
            }
            else
            {
                bootDone = true;
            }

            float fadeOutElapsed = 0f;
            while (fadeOutElapsed < fadeBackDuration)
            {
                fadeOutElapsed += Time.deltaTime;
                if (fadeOverlayGroup != null)
                {
                    fadeOverlayGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fadeOutElapsed / fadeBackDuration));
                }
                yield return null;
            }
            if (fadeOverlayGroup != null) fadeOverlayGroup.alpha = 0f;

            while (!bootDone) yield return null;

            if (jaegerController != null) jaegerController.isCutsceneActive = false;
            if (kaijuAI != null && !kaijuAI.IsDead)
            {
                PacificRimAudioManager.Instance?.PlayKaijuAttackGrunt(kaijuAI.transform.position + Vector3.up * 14f);
            }

            isCutsceneRunning = false;
            Debug.Log("<color=#00e5ff><b>[Deployment Cutscene]</b></color> Level 1 cinematic sequence complete; combat unlocked.");
        }

        private IEnumerator HandleCutsceneSkipped()
        {
            if (fadeOverlayGroup != null) fadeOverlayGroup.alpha = 1f;
            yield return StartCoroutine(HandoffToGameplayRoutine(preBlackHold: 0.05f, fadeBackDuration: 0.30f));
        }

        private void ExpandOceanForCutscene()
        {
            if (oceanWaterMesh == null) oceanWaterMesh = FindAnyObjectByType<OceanWaterMesh>();
            if (oceanWaterMesh != null && !hasExpandedOcean && cutsceneOceanScaleMultiplier > 1.01f)
            {
                savedOceanWidth = oceanWaterMesh.meshWidth;
                savedOceanLength = oceanWaterMesh.meshLength;
                oceanWaterMesh.meshWidth = savedOceanWidth * cutsceneOceanScaleMultiplier;
                oceanWaterMesh.meshLength = savedOceanLength * cutsceneOceanScaleMultiplier;
                hasExpandedOcean = true;
            }
        }

        private void RestoreOceanAfterCutscene()
        {
            if (hasExpandedOcean && oceanWaterMesh != null)
            {
                oceanWaterMesh.meshWidth = savedOceanWidth;
                oceanWaterMesh.meshLength = savedOceanLength;
            }
            hasExpandedOcean = false;
        }

        private void StartChinookAudio(float volumeScale = 0.20f)
        {
            if (chinookAudioSource != null) return;
            var go = new GameObject("ChinookSquadronAudio");
            go.transform.SetParent(transform, false);
            chinookAudioSource = go.AddComponent<AudioSource>();
            chinookAudioSource.spatialBlend = 0.25f;
            chinookAudioSource.loop = true;
            float sfxVol = PacificRimAudioManager.Instance != null ? PacificRimAudioManager.Instance.SfxVolume : 0.55f;
            chinookAudioSource.volume = volumeScale * sfxVol;

            AudioClip clip = PacificRimAudioManager.Instance != null ? PacificRimAudioManager.Instance.GetClip("HelicopterLoop") : null;
            if (clip == null) clip = Resources.Load<AudioClip>("HelicopterLoop");
#if UNITY_EDITOR
            if (clip == null) clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/HelicopterLoop.mp3");
#endif
            if (clip != null)
            {
                chinookAudioSource.clip = clip;
                chinookAudioSource.Play();
            }
        }

        private static void SolveTwoBoneIKWorld(Transform root, Transform mid, Transform tip, Vector3 targetPos, Vector3 poleForward)
        {
            Vector3 a = root.position;
            Vector3 b = mid.position;
            Vector3 c = tip.position;
            Quaternion origTipRot = tip.rotation;

            float l1 = Vector3.Distance(a, b);
            float l2 = Vector3.Distance(b, c);
            if (l1 < 0.001f || l2 < 0.001f) return;

            Vector3 toTarget = targetPos - a;
            float dist = toTarget.magnitude;
            float clampedDist = Mathf.Clamp(dist, Mathf.Abs(l1 - l2) + 0.05f, (l1 + l2) * 0.995f);
            Vector3 dir = dist > 0.0001f ? (toTarget / dist) : Vector3.down;
            Vector3 clampedTarget = a + dir * clampedDist;

            Vector3 animMidProj = a + Vector3.Project(b - a, dir);
            Vector3 poleProj = Vector3.ProjectOnPlane(poleForward, dir);
            Vector3 poleUnit = poleProj.sqrMagnitude > 0.0001f ? poleProj.normalized : dir;
            Vector3 bendDir = Vector3.ProjectOnPlane((b - animMidProj) * 0.45f + poleUnit * (l1 * 0.55f), dir);
            if (bendDir.sqrMagnitude < 0.0001f) return;
            bendDir.Normalize();

            float cosA = Mathf.Clamp((l1 * l1 + clampedDist * clampedDist - l2 * l2) / (2f * l1 * clampedDist), -1f, 1f);
            float x = l1 * cosA;
            float y = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
            Vector3 solvedKnee = a + dir * x + bendDir * y;

            Vector3 curRootToMid = b - a;
            Vector3 newRootToMid = solvedKnee - a;
            if (curRootToMid.sqrMagnitude > 0.0001f && newRootToMid.sqrMagnitude > 0.0001f)
            {
                root.rotation = Quaternion.FromToRotation(curRootToMid, newRootToMid) * root.rotation;
            }

            Vector3 bAfter = mid.position;
            Vector3 cAfter = tip.position;
            Vector3 curMidToTip = cAfter - bAfter;
            Vector3 newMidToTip = clampedTarget - bAfter;
            if (curMidToTip.sqrMagnitude > 0.0001f && newMidToTip.sqrMagnitude > 0.0001f)
            {
                mid.rotation = Quaternion.FromToRotation(curMidToTip, newMidToTip) * mid.rotation;
            }
            tip.rotation = origTipRot;
        }

        public bool WasSkipPressed()
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame ||
                                   kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                {
                    return true;
                }
                var gp = UnityEngine.InputSystem.Gamepad.current;
                if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame))
                {
                    return true;
                }
#endif
            }
            catch { }

            try
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))
                    return true;
#endif
            }
            catch { }

            return false;
        }
    }
}
