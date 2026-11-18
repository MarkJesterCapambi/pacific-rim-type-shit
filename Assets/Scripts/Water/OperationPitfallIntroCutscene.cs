using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PacificRimAudio;
using PacificRimUI;
using PacificRimWater;

namespace PacificRimCutscene
{
    /// <summary>
    /// Orchestrates Level 2: Operation Pitfall's ~27.8s theatrical intro cutscene:
    /// - 0:00–0:05 | The Airlift Approach: Fade in from black + letterbox bars, locked 45° three-quarter front shot
    ///               framing Gipsy Danger's head and upper chest suspended from Chinooks with rotor wash sea spray below.
    /// - 0:05–0:11 | The Drop & Recovery: Cables release, Chinooks peel away, colossal splashdown into chest-deep water
    ///               in a heavy impact crouch (~3s stabilization & slow rise), then first heavy step as camera dollies out.
    /// - 0:11–0:18 | Wading & Submersion: 3–4 slow heavy steps down sloping seabed until water covers shoulders and Conn-Pod visor;
    ///               camera dips beneath the waterline with dense upward bubbles and turbulence rushing past the lens.
    /// - 0:18–0:26 | The Trench Descent: Fully submerged march down the steep trench slope while dimming surface light
    ///               over 8 seconds into deep murky ocean blue so Gipsy's glowing Conn-Pod visor & nuclear turbine stand out.
    /// - 0:26–0:28 | Transition to Boss Fight: Smooth fade to black directly into Operation Pitfall (Slattern encounter).
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class OperationPitfallIntroCutscene : MonoBehaviour
    {
        public static OperationPitfallIntroCutscene Instance { get; private set; }

        [Header("Scene Targets")]
        public string targetGameplayScene = "Operation Pitfall";
        public Transform jaegerTransform;
        public Terrain trenchTerrain;
        public Light mainDirectionalLight;
        public Volume postProcessVolume;
        public WaterSystem waterSystem;
        public WaterSplashManager splashManager;

        [Header("Assets")]
        public GameObject chinookPrefab;
        public Material cableMaterial;
        public Material underwaterFloodlightBeamMaterial;

        [Header("Cutscene Timeline (Seconds)")]
        public float airliftDuration = 5.0f;
        public float freefallDuration = 1.30f;
        public float crouchHoldDuration = 1.20f;
        public float standRecoveryDuration = 1.90f;
        public float firstStepDuration = 1.60f;
        public float wadingSubmersionDuration = 7.0f;
        public float trenchDescentDuration = 8.0f;
        public float fadeToBlackDuration = 1.80f;

        [Header("Movement & Coordinates")]
        public Vector3 airliftStartPos = new Vector3(0f, 7.5f, -22f);
        public Vector3 airliftReleasePos = new Vector3(0f, 7.5f, 0f);
        public float wadingForwardSpeed = 5.15f;
        public float trenchForwardSpeed = 5.75f;
        public float walkAnimatorSpeed = 0.88f;
        public float trenchAnimatorSpeed = 0.78f;

        [Header("Three-Quarter Front Camera (45 deg)")]
        public float closeAirliftDistance = 30.5f;
        public float closeAirliftHeightOffset = 22.8f;
        public float closeAirliftLookYOffset = 20.0f;
        public float closeAirliftFov = 50f;
        public float trackingDistance = 36.0f;
        public float trackingFov = 44f;
        [Tooltip("Normalized height inside visible letterbox frame where Gipsy's head top sits (0.64 = near top bar).")]
        [Range(0.25f, 0.76f)]
        public float headTopFrameRatio = 0.64f;

        [Header("UI Overlay")]
        public RectTransform letterboxRoot;
        public RectTransform topBar;
        public RectTransform bottomBar;
        public CanvasGroup letterboxCanvasGroup;
        public CanvasGroup fadeOverlayGroup;
        public Image fadeOverlayImage;
        public Text skipPromptText;

        [Header("Audio")]
        public AudioClip readyToSubmergeClip;

        [Header("State")]
        public bool playOnStart = true;
        public bool isCutsceneRunning { get; private set; }
        public bool IsInDeepDescentPhase { get; private set; }

        // Runtime state
        private Animator jaegerAnimator;
        private Rigidbody jaegerRigidbody;
        private SimpleJaegerController jaegerController;
        private JaegerVisualSockets visualSockets;
        private JaegerWaterInteraction waterInteraction;
        private PacificRimCameraShake cameraShake;
        private AudioLowPassFilter cameraLowPass;
        private Camera mainCamera;

        // Skeleton transforms for procedural impact crouch / recovery IK
        private Transform hipsBone, spine1Bone, chestBone, headBone;
        private Transform thighL, shinL, footL, thighR, shinR, footR;
        private Transform upperArmL, forearmL, handL, upperArmR, forearmR, handR;

        // Chinook convoy & cables
        private readonly List<GameObject> activeChinooks = new List<GameObject>();
        private readonly List<LineRenderer> activeCables = new List<LineRenderer>();
        private readonly List<Vector3> chinookOffsets = new List<Vector3>
        {
            new Vector3(-11.5f, 27.2f, 7.5f),
            new Vector3(11.5f, 26.2f, 6.5f),
            new Vector3(-13.5f, 29.0f, -9.5f),
            new Vector3(13.5f, 28.0f, -8.5f)
        };
        private AudioSource chinookAudioSource;
        private AudioSource underwaterAmbienceSource;
        private AudioSource readyToSubmergeSource;

        // VFX systems
        private ParticleSystem rotorWashSprayPs;
        private ParticleSystem rotorWashMistPs;
        private ParticleSystem lensBubblesPs;
        private ParticleSystem lensTurbulencePs;
        private ParticleSystem marineSnowPs;

        // Glow lights & instanced materials for Visor & Chest Turbine
        private Material visorMatInstance;
        private Material turbineMatInstance;
        private readonly List<Material> armorMatInstances = new List<Material>();
        private Material oceanMatInstance;
        private Light visorGlowLight;
        private Light turbineGlowLight;
        private readonly List<Renderer> floodlightBeamRenderers = new List<Renderer>();
        private readonly List<Light> floodlightSpots = new List<Light>();
        private readonly List<float> floodlightBaseIntensities = new List<float>();

        // Post-processing runtime overrides
        private VolumeProfile runtimeVolumeProfile;
        private ColorAdjustments volColorAdj;
        private Bloom volBloom;
        private Vignette volVignette;
        private SplitToning volSplitToning;

        // Procedural pose & camera runtime state
        private float currentCrouchWeight = 0f;
        private float currentForwardPitchDeg = 0f;
        private float currentAirliftHangWeight = 0f;
        private float currentHeadBowDeg = 0f;
        private float currentSlopeLeanDeg = 0f;

        private Vector3 desiredCamPos;
        private Quaternion desiredCamRot = Quaternion.identity;
        private float desiredCamFov = 45f;
        private bool cameraControlActive = false;
        private bool hasInitializedCamSmooth = false;
        private Vector3 camVelocity;

        private bool hasSubmergedCamera = false;
        private bool transitionTriggered = false;
        private float cutsceneElapsed = 0f;
        private Coroutine activeRoutine;

        private const float JaegerGroundRootOffset = 2.977f;
        private static readonly Color SurfaceLightColor = new Color(0.86f, 0.94f, 1.0f, 1f);
        private static readonly Color ShallowUnderwaterLightColor = new Color(0.32f, 0.68f, 0.88f, 1f);
        private static readonly Color DeepOceanLightColor = new Color(0.04f, 0.16f, 0.30f, 1f);

        private static readonly Color SurfaceFogColor = new Color(0.36f, 0.50f, 0.62f, 1f);
        private static readonly Color ShallowUnderwaterFogColor = new Color(0.045f, 0.24f, 0.36f, 1f);
        private static readonly Color DeepMurkyOceanBlue = new Color(0.012f, 0.068f, 0.148f, 1f);

        private static readonly int IdEmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int IdVisorEmission = Shader.PropertyToID("_VisorEmission");
        private static readonly int IdWaterLevel = Shader.PropertyToID("_WaterLevel");
        private static readonly int IdEdgeFogColor = Shader.PropertyToID("_EdgeFogColor");
        private static readonly int IdDeepColor = Shader.PropertyToID("_DeepColor");
        private static readonly int IdShallowColor = Shader.PropertyToID("_ShallowColor");
        private static readonly int IdGlobalRainWetness = Shader.PropertyToID("_GlobalRainWetness");
        private static readonly int IdGlobalWindVector = Shader.PropertyToID("_GlobalWindVector");
        private static readonly int IdGlobalLightningFlash = Shader.PropertyToID("_GlobalLightningFlash");

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
            ConfigureInitialSurfaceState();

            if (PacificRimLoadingScreen.Instance != null && PacificRimLoadingScreen.Instance.IsLoading)
            {
                return;
            }

            if (playOnStart)
            {
                StartCutscene();
            }
        }

        public void StartCutscene()
        {
            if (isCutsceneRunning) return;
            ResolveReferences();
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(RunCutsceneRoutine());
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
                jaegerAnimator = jaegerTransform.GetComponent<Animator>();
                jaegerRigidbody = jaegerTransform.GetComponent<Rigidbody>();
                jaegerController = jaegerTransform.GetComponent<SimpleJaegerController>();
                visualSockets = jaegerTransform.GetComponent<JaegerVisualSockets>();
                waterInteraction = jaegerTransform.GetComponent<JaegerWaterInteraction>();
                ResolveJaegerBones();
                SetupJaegerMaterialInstancesAndLights();
            }

            if (trenchTerrain == null) trenchTerrain = FindAnyObjectByType<Terrain>();
            if (waterSystem == null)
            {
                waterSystem = WaterSystem.Instance;
                if (waterSystem == null) waterSystem = FindAnyObjectByType<WaterSystem>();
            }
            if (splashManager == null)
            {
                splashManager = WaterSplashManager.Instance;
                if (splashManager == null) splashManager = FindAnyObjectByType<WaterSplashManager>();
            }
            if (postProcessVolume == null) postProcessVolume = FindAnyObjectByType<Volume>();

            if (mainDirectionalLight == null)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                {
                    if (l != null && l.type == LightType.Directional && l.gameObject.name != "LightningFlashLight")
                    {
                        mainDirectionalLight = l;
                        break;
                    }
                }
            }

            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                var camFollow = mainCamera.GetComponent<JaegerCameraFollow>();
                if (camFollow != null) camFollow.enabled = false;
                cameraShake = mainCamera.GetComponent<PacificRimCameraShake>();
                if (cameraShake == null) cameraShake = mainCamera.gameObject.AddComponent<PacificRimCameraShake>();
                cameraLowPass = mainCamera.GetComponent<AudioLowPassFilter>();
                if (cameraLowPass == null) cameraLowPass = mainCamera.gameObject.AddComponent<AudioLowPassFilter>();
            }

            EnsureUI();
            SetupRuntimeVolumeProfile();
            SetupOceanMaterialInstance();
            EnsureCutsceneParticleSystems();
        }

        private void ResolveJaegerBones()
        {
            if (jaegerTransform == null) return;
            foreach (var t in jaegerTransform.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Hips": hipsBone = t; break;
                    case "Spine1": spine1Bone = t; break;
                    case "Chest": chestBone = t; break;
                    case "Head": headBone = t; break;
                    case "Thigh.L": thighL = t; break;
                    case "Shin.L": shinL = t; break;
                    case "Foot.L": footL = t; break;
                    case "Thigh.R": thighR = t; break;
                    case "Shin.R": shinR = t; break;
                    case "Foot.R": footR = t; break;
                    case "Hand.L": handL = t; break;
                    case "Hand.R": handR = t; break;
                }
            }
            if (handL != null)
            {
                forearmL = handL.parent;
                if (forearmL != null) upperArmL = forearmL.parent;
            }
            if (handR != null)
            {
                forearmR = handR.parent;
                if (forearmR != null) upperArmR = forearmR.parent;
            }
        }

        private void SetupJaegerMaterialInstancesAndLights()
        {
            if (jaegerTransform == null) return;
            armorMatInstances.Clear();
            floodlightBeamRenderers.Clear();
            floodlightSpots.Clear();
            floodlightBaseIntensities.Clear();

            foreach (var smr in jaegerTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMaterial == null) continue;

                if (smr.name == "Visor")
                {
                    if (visorMatInstance == null)
                    {
                        visorMatInstance = new Material(smr.sharedMaterial) { name = "Visor_CutsceneRuntime" };
                        visorMatInstance.EnableKeyword("_EMISSION");
                        visorMatInstance.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        smr.sharedMaterial = visorMatInstance;
                    }
                }
                else if (smr.name.Contains("Spinning"))
                {
                    if (turbineMatInstance == null)
                    {
                        turbineMatInstance = new Material(smr.sharedMaterial) { name = "Turbine_CutsceneRuntime" };
                        turbineMatInstance.EnableKeyword("_EMISSION");
                        turbineMatInstance.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        smr.sharedMaterial = turbineMatInstance;
                    }
                }
                else if (smr.sharedMaterial.HasProperty(IdVisorEmission))
                {
                    var mats = smr.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        var inst = FindOrCloneArmorMat(mats[i]);
                        mats[i] = inst;
                    }
                    smr.sharedMaterials = mats;
                }
            }

            // Configure head & chest floodlights (also refreshed later after JaegerVisualSockets.Start)
            RefreshFloodlightSockets();

            if (headBone != null && visorGlowLight == null)
            {
                var vgGo = new GameObject("Cutscene_VisorGlowLight");
                vgGo.transform.SetParent(headBone, false);
                vgGo.transform.localPosition = new Vector3(0f, 0f, 0.002f);
                visorGlowLight = vgGo.AddComponent<Light>();
                visorGlowLight.type = LightType.Point;
                visorGlowLight.color = new Color(1.0f, 0.62f, 0.14f, 1f);
                visorGlowLight.range = 16f;
                visorGlowLight.intensity = 4f;
                visorGlowLight.shadows = LightShadows.None;
            }

            if (chestBone != null && turbineGlowLight == null)
            {
                var tgGo = new GameObject("Cutscene_TurbineGlowLight");
                tgGo.transform.SetParent(chestBone, false);
                tgGo.transform.localPosition = new Vector3(0f, 0.001f, 0.003f);
                turbineGlowLight = tgGo.AddComponent<Light>();
                turbineGlowLight.type = LightType.Point;
                turbineGlowLight.color = new Color(1.0f, 0.56f, 0.10f, 1f);
                turbineGlowLight.range = 24f;
                turbineGlowLight.intensity = 10f;
                turbineGlowLight.shadows = LightShadows.None;
            }

            ApplyGlowIntensity(1.0f, 0.0f);
        }

        private Material FindOrCloneArmorMat(Material src)
        {
            string targetName = src.name + "_CutsceneRuntime";
            for (int i = 0; i < armorMatInstances.Count; i++)
            {
                if (armorMatInstances[i].name == targetName) return armorMatInstances[i];
            }
            var clone = new Material(src) { name = targetName };
            armorMatInstances.Add(clone);
            return clone;
        }

        private void RefreshFloodlightSockets()
        {
            floodlightBeamRenderers.Clear();
            floodlightSpots.Clear();
            floodlightBaseIntensities.Clear();

            if (jaegerTransform == null) return;

            foreach (var spot in jaegerTransform.GetComponentsInChildren<Light>(true))
            {
                if (spot != null && spot.type == LightType.Spot)
                {
                    RegisterSocketSpot(spot);
                }
            }

            foreach (var t in jaegerTransform.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("VolumetricBeam") || t.name.Contains("Beam"))
                {
                    if (t.GetComponent<MeshRenderer>() != null)
                    {
                        RegisterSocketBeam(t);
                    }
                }
            }
        }

        private void RegisterSocketSpot(Light spot)
        {
            if (spot == null || floodlightSpots.Contains(spot)) return;
            floodlightSpots.Add(spot);
            floodlightBaseIntensities.Add(spot.intensity > 1f ? spot.intensity : 3200f);
        }

        private void RegisterSocketBeam(Transform beam)
        {
            if (beam == null) return;
            var mr = beam.GetComponent<MeshRenderer>();
            if (mr == null || floodlightBeamRenderers.Contains(mr)) return;

            if (underwaterFloodlightBeamMaterial != null)
            {
                mr.sharedMaterial = new Material(underwaterFloodlightBeamMaterial) { name = "CutsceneBeam_Runtime" };
            }
            else if (mr.sharedMaterial != null)
            {
                mr.sharedMaterial = new Material(mr.sharedMaterial) { name = "CutsceneBeam_Runtime" };
            }
            floodlightBeamRenderers.Add(mr);
        }

        private void SetupRuntimeVolumeProfile()
        {
            if (postProcessVolume == null) return;
            var srcProfile = postProcessVolume.sharedProfile != null ? postProcessVolume.sharedProfile : postProcessVolume.profile;
            if (srcProfile != null && runtimeVolumeProfile == null)
            {
                runtimeVolumeProfile = Instantiate(srcProfile);
                postProcessVolume.profile = runtimeVolumeProfile;
            }
            if (runtimeVolumeProfile == null) return;

            runtimeVolumeProfile.TryGet(out volColorAdj);
            runtimeVolumeProfile.TryGet(out volBloom);
            runtimeVolumeProfile.TryGet(out volVignette);
            runtimeVolumeProfile.TryGet(out volSplitToning);
        }

        private void SetupOceanMaterialInstance()
        {
            if (waterSystem == null) return;
            var mr = waterSystem.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null && oceanMatInstance == null)
            {
                oceanMatInstance = new Material(mr.sharedMaterial) { name = "Ocean_CutsceneRuntime" };
                mr.sharedMaterial = oceanMatInstance;
            }
        }

        private void EnsureUI()
        {
            var canvasGo = GameObject.Find("PacificRim_UICanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("PacificRim_UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var c = canvasGo.GetComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 500;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (letterboxRoot == null)
            {
                var existing = canvasGo.transform.Find("Cinematic_Letterbox");
                if (existing != null)
                {
                    letterboxRoot = existing.GetComponent<RectTransform>();
                    letterboxCanvasGroup = existing.GetComponent<CanvasGroup>();
                    topBar = existing.Find("TopBar") as RectTransform;
                    bottomBar = existing.Find("BottomBar") as RectTransform;
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

                    topBar = CreateLetterboxBar("TopBar", letterboxRoot, isTop: true, 128f);
                    bottomBar = CreateLetterboxBar("BottomBar", letterboxRoot, isTop: false, 128f);

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

                if (existingFade == null)
                {
                    fadeGo.transform.SetParent(canvasGo.transform, false);
                }
                fadeGo.transform.SetAsFirstSibling(); // Render behind letterbox bars so bars frame the fade cleanly!

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

        private static RectTransform CreateLetterboxBar(string barName, Transform parent, bool isTop, float height)
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

        private void EnsureCutsceneParticleSystems()
        {
            Shader particleShader = Shader.Find("PacificRim/SplashParticle");
            if (particleShader == null) particleShader = Shader.Find("Sprites/Default");

            if (rotorWashSprayPs == null)
            {
                rotorWashSprayPs = BuildCutsceneParticleSystem(
                    "Cutscene_RotorWashSpray",
                    particleShader,
                    CreateSoftCircleTexture(64, isBubble: false),
                    new Color(0.92f, 0.97f, 1.0f, 0.85f),
                    ParticleSystemRenderMode.Stretch,
                    1200,
                    gravity: 0.75f,
                    lightInfluence: 0.40f);
                var r = rotorWashSprayPs.GetComponent<ParticleSystemRenderer>();
                r.velocityScale = 0.055f;
                r.lengthScale = 2.0f;
            }

            if (rotorWashMistPs == null)
            {
                rotorWashMistPs = BuildCutsceneParticleSystem(
                    "Cutscene_RotorWashMist",
                    particleShader,
                    CreateMistPuffTexture(128),
                    new Color(0.86f, 0.93f, 0.99f, 0.38f),
                    ParticleSystemRenderMode.Billboard,
                    400,
                    gravity: -0.04f,
                    lightInfluence: 0.55f);
                ConfigureSizeAndAlphaCurve(rotorWashMistPs, 0.65f, 2.4f, 0.12f, 0.45f);
            }

            if (lensBubblesPs == null)
            {
                lensBubblesPs = BuildCutsceneParticleSystem(
                    "Cutscene_LensTransitionBubbles",
                    particleShader,
                    CreateSoftCircleTexture(64, isBubble: true),
                    new Color(0.86f, 0.97f, 1.0f, 0.92f),
                    ParticleSystemRenderMode.Billboard,
                    1600,
                    gravity: -0.35f,
                    lightInfluence: 0.35f);
                ConfigureSizeAndAlphaCurve(lensBubblesPs, 0.7f, 1.25f, 0.08f, 0.65f);
            }

            if (lensTurbulencePs == null)
            {
                lensTurbulencePs = BuildCutsceneParticleSystem(
                    "Cutscene_LensTransitionTurbulence",
                    particleShader,
                    CreateMistPuffTexture(128),
                    new Color(0.45f, 0.78f, 0.92f, 0.42f),
                    ParticleSystemRenderMode.Billboard,
                    500,
                    gravity: -0.20f,
                    lightInfluence: 0.45f);
                ConfigureSizeAndAlphaCurve(lensTurbulencePs, 0.6f, 2.6f, 0.12f, 0.40f);
            }

            if (marineSnowPs == null)
            {
                marineSnowPs = BuildCutsceneParticleSystem(
                    "Cutscene_UnderwaterMarineSnow",
                    particleShader,
                    CreateSoftCircleTexture(64, isBubble: false),
                    new Color(0.64f, 0.84f, 0.96f, 0.52f),
                    ParticleSystemRenderMode.Billboard,
                    900,
                    gravity: -0.02f,
                    lightInfluence: 0.70f);
                var main = marineSnowPs.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
                var emission = marineSnowPs.emission;
                emission.rateOverTime = 0f;
                var shape = marineSnowPs.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(90f, 52f, 90f);
                var vel = marineSnowPs.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.8f);
                vel.y = new ParticleSystem.MinMaxCurve(0.25f, 1.15f);
                vel.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            }
        }

        private ParticleSystem BuildCutsceneParticleSystem(
            string sysName,
            Shader shader,
            Texture2D tex,
            Color tint,
            ParticleSystemRenderMode renderMode,
            int maxParticles,
            float gravity,
            float lightInfluence)
        {
            Transform existing = transform.Find(sysName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(sysName);
            go.transform.SetParent(transform, false);

            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null) ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 60f;
            main.maxParticles = maxParticles;
            main.startLifetime = 1.6f;
            main.startSpeed = 0f;
            main.startSize = 0.4f;
            main.startColor = Color.white;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = false;

            var mat = new Material(shader) { name = sysName + "_Mat" };
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", tint);
            if (mat.HasProperty("_InvFade")) mat.SetFloat("_InvFade", 0.35f);
            if (mat.HasProperty("_LightInfluence")) mat.SetFloat("_LightInfluence", lightInfluence);

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = mat;
            rend.renderMode = renderMode;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.minParticleSize = 0f;
            rend.maxParticleSize = 2.8f;

            ps.Play();
            return ps;
        }

        private static void ConfigureSizeAndAlphaCurve(ParticleSystem ps, float startSize, float endSize, float fadeInEnd, float fadeOutStart)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var sizeCurve = new AnimationCurve(new Keyframe(0f, startSize), new Keyframe(1f, endSize));
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.84f, 0.95f, 1f), 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, fadeInEnd),
                    new GradientAlphaKey(0.82f, fadeOutStart),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = grad;
        }

        private void ConfigureInitialSurfaceState()
        {
            if (PacificRimAudioManager.Instance != null)
            {
                PacificRimAudioManager.Instance.isUnderwaterLevel = false;
            }
            if (splashManager != null)
            {
                splashManager.isDeepUnderwaterMode = false;
            }

            Shader.SetGlobalFloat(IdGlobalRainWetness, 0.35f);
            Shader.SetGlobalFloat(IdGlobalLightningFlash, 0f);
            Shader.SetGlobalVector(IdGlobalWindVector, new Vector4(0.85f, 0.52f, 18f, 0.35f));

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = SurfaceFogColor;
            RenderSettings.fogDensity = 0.0016f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.60f, 0.74f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.45f, 0.56f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.16f, 0.22f, 1f);

            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.enabled = true;
                mainDirectionalLight.color = SurfaceLightColor;
                mainDirectionalLight.intensity = 2.15f;
                mainDirectionalLight.shadowStrength = 0.82f;
                mainDirectionalLight.transform.rotation = Quaternion.Euler(46f, -38f, 0f);
            }

            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.Skybox;
                mainCamera.backgroundColor = SurfaceFogColor;
                mainCamera.farClipPlane = 550f;
                if (cameraLowPass != null) cameraLowPass.cutoffFrequency = 22000f;
            }

            if (PacificRimUI.JaegerHUD.Instance != null)
            {
                PacificRimUI.JaegerHUD.Instance.SetCutsceneHidden(true);
            }

            if (fadeOverlayGroup != null)
            {
                fadeOverlayGroup.alpha = 1f;
            }
            if (letterboxCanvasGroup != null)
            {
                letterboxCanvasGroup.alpha = 1f;
            }

            SetUnderwaterFloodlightVisibility(0f);
        }

        private IEnumerator RunCutsceneRoutine()
        {
            isCutsceneRunning = true;
            IsInDeepDescentPhase = false;
            hasSubmergedCamera = false;
            transitionTriggered = false;
            cutsceneElapsed = 0f;

            if (jaegerController != null) jaegerController.isCutsceneActive = true;
            if (jaegerRigidbody != null)
            {
                jaegerRigidbody.isKinematic = true;
                jaegerRigidbody.useGravity = false;
                jaegerRigidbody.position = airliftStartPos;
                jaegerRigidbody.rotation = Quaternion.identity;
            }
            if (jaegerTransform != null)
            {
                jaegerTransform.position = airliftStartPos;
                jaegerTransform.rotation = Quaternion.identity;
            }

            if (jaegerAnimator != null)
            {
                jaegerAnimator.speed = 1.0f;
                jaegerAnimator.SetFloat("Speed", 0f);
                jaegerAnimator.SetBool("IsRunning", false);
                jaegerAnimator.SetBool("InCombat", false);
                jaegerAnimator.SetBool("IsBlocking", false);
                if (jaegerAnimator.layerCount > 1) jaegerAnimator.SetLayerWeight(1, 0f);
                jaegerAnimator.Play("Idle", 0, 0f);
            }

            // Wait one frame while loading screen & JaegerVisualSockets finish initializing
            yield return null;
            while (PacificRimLoadingScreen.Instance != null && PacificRimLoadingScreen.Instance.IsLoading)
            {
                yield return null;
            }

            if (letterboxCanvasGroup != null) letterboxCanvasGroup.alpha = 0f;
            yield return StartCoroutine(PacificRimMissionBriefingOverlay.ShowOperationPitfallBriefing());
            if (letterboxCanvasGroup != null) letterboxCanvasGroup.alpha = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            RefreshFloodlightSockets();
            SetUnderwaterFloodlightVisibility(0f);

            SpawnChinookSquadron();
            StartChinookAudio();
            cameraControlActive = true;
            hasInitializedCamSmooth = false;

            // =========================================================================
            // BEAT 1 (0:00 – 0:05): The Airlift Approach
            // Fade in from black + letterbox bars; locked 45° 3/4 front framing of head & upper chest
            // with Chinook helicopters overhead and rotor wash kicking up sea spray below.
            // =========================================================================
            currentAirliftHangWeight = 1f;
            currentCrouchWeight = 0f;
            currentForwardPitchDeg = 4.5f;

            float sprayEmitAcc = 0f;
            float fadeInDuration = 1.25f;

            while (cutsceneElapsed < airliftDuration)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(SkipCutsceneRoutine()); yield break; }

                float dt = Time.deltaTime;
                cutsceneElapsed += dt;
                float u = Mathf.Clamp01(cutsceneElapsed / airliftDuration);
                float smoothU = Mathf.SmoothStep(0f, 1f, u);

                // Fade in from black during first 1.25s
                if (fadeOverlayGroup != null)
                {
                    fadeOverlayGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(cutsceneElapsed / fadeInDuration));
                }

                Vector3 currentPos = Vector3.Lerp(airliftStartPos, airliftReleasePos, smoothU);
                float swayRoll = Mathf.Sin(cutsceneElapsed * 1.15f) * 1.4f;
                float swayPitch = 4.5f + Mathf.Cos(cutsceneElapsed * 0.9f) * 1.1f;
                Quaternion hangRot = Quaternion.Euler(swayPitch, 0f, swayRoll);

                SetJaegerPose(currentPos, hangRot);
                UpdateChinookConvoy(currentPos, cutsceneElapsed);
                UpdateSuspensionCables();

                // Kick up vigorous rotor wash sea spray directly below the convoy on the ocean surface
                sprayEmitAcc += dt;
                if (sprayEmitAcc >= 0.032f)
                {
                    sprayEmitAcc = 0f;
                    EmitRotorWashSeaSpray(currentPos);
                }

                // Locked 45-degree 3/4 front camera framing head & upper chest + sea spray below
                ComputeThreeQuarterAirliftCamera(currentPos, out desiredCamPos, out desiredCamRot, out desiredCamFov, snapSmooth: true);

                yield return null;
            }

            if (fadeOverlayGroup != null) fadeOverlayGroup.alpha = 0f;

            // =========================================================================
            // BEAT 2 (0:05 – 0:11): The Drop & Recovery
            // Chinooks release cables & peel away; Gipsy drops into chest-deep shallow water
            // with massive splash, lands in heavy crouch, stabilizes 2-3s, slowly stands & takes 1st step.
            // =========================================================================
            ReleaseCablesAndDisperseChinooks();

            float seabedDropY = SampleSeabedHeight(airliftReleasePos.x, airliftReleasePos.z);
            float groundedDropRootY = seabedDropY + JaegerGroundRootOffset;
            Vector3 dropStartPos = jaegerTransform.position;
            Vector3 dropEndPos = new Vector3(airliftReleasePos.x, groundedDropRootY, airliftReleasePos.z);
            Quaternion startDropRot = jaegerTransform.rotation;

            float dropTimer = 0f;
            bool triggeredWaterEntrySplash = false;
            float surfaceLevelY = waterSystem != null ? waterSystem.MeanSurfaceY : 0f;

            // 2a. Freefall (~1.30s)
            while (dropTimer < freefallDuration)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(SkipCutsceneRoutine()); yield break; }

                float dt = Time.deltaTime;
                dropTimer += dt;
                cutsceneElapsed += dt;

                float u = Mathf.Clamp01(dropTimer / freefallDuration);
                float fallEase = u * u; // Accelerating gravity fall
                Vector3 fallPos = Vector3.Lerp(dropStartPos, dropEndPos, fallEase);
                Quaternion fallRot = Quaternion.Slerp(startDropRot, Quaternion.identity, u);
                SetJaegerPose(fallPos, fallRot);

                // Pre-brace slightly right before seabed impact
                currentAirliftHangWeight = Mathf.Lerp(1f, 0f, Mathf.Clamp01(u * 1.4f));
                currentCrouchWeight = Mathf.SmoothStep(0f, 0.35f, Mathf.Clamp01((u - 0.55f) / 0.45f));
                currentForwardPitchDeg = Mathf.Lerp(4.5f, 0f, u);

                if (!triggeredWaterEntrySplash && fallPos.y - JaegerGroundRootOffset <= surfaceLevelY)
                {
                    triggeredWaterEntrySplash = true;
                    TriggerSplashdownImpact(new Vector3(fallPos.x, surfaceLevelY, fallPos.z), isFinalSeabedHit: false);
                }

                // Follow drop smoothly keeping head framed while beginning slight pull-back
                ComputeDropAndDollyCamera(fallPos, dollyProgress: 0.08f * u);
                yield return null;
            }

            // 2b. Seabed Impact & Heavy Crouch Absorption + Stabilization + Slow Stand (3.10s total: ~6.3s -> 9.4s)
            SetJaegerPose(dropEndPos, Quaternion.identity);
            currentAirliftHangWeight = 0f;
            TriggerSplashdownImpact(new Vector3(dropEndPos.x, surfaceLevelY, dropEndPos.z), isFinalSeabedHit: true);
            PacificRimAudioManager.Instance?.PlayArmorCreak(dropEndPos + Vector3.up * 15f, 1.0f);
            PacificRimAudioManager.Instance?.PlayHydraulicServo(dropEndPos + Vector3.up * 12f, 0.9f);

            float recoveryTotal = crouchHoldDuration + standRecoveryDuration;
            float recoveryTimer = 0f;
            bool playedRiseServo = false;

            while (recoveryTimer < recoveryTotal)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(SkipCutsceneRoutine()); yield break; }

                float dt = Time.deltaTime;
                recoveryTimer += dt;
                cutsceneElapsed += dt;

                if (recoveryTimer < crouchHoldDuration)
                {
                    // Rapid compression into deep crouch (0..0.22s), then settle & stabilize in waves
                    float cU = Mathf.Clamp01(recoveryTimer / 0.22f);
                    float settleU = Mathf.Clamp01((recoveryTimer - 0.22f) / Mathf.Max(0.01f, crouchHoldDuration - 0.22f));
                    currentCrouchWeight = recoveryTimer < 0.22f
                        ? Mathf.Lerp(0.35f, 1.0f, Mathf.SmoothStep(0f, 1f, cU))
                        : Mathf.Lerp(1.0f, 0.88f, Mathf.SmoothStep(0f, 1f, settleU));
                    currentHeadBowDeg = Mathf.Lerp(16f, 6f, settleU);
                }
                else
                {
                    if (!playedRiseServo)
                    {
                        playedRiseServo = true;
                        PacificRimAudioManager.Instance?.PlayHydraulicServo(dropEndPos + Vector3.up * 14f, 0.95f);
                        PacificRimAudioManager.Instance?.PlayArmorCreak(dropEndPos + Vector3.up * 16f, 0.85f);
                    }
                    float riseU = Mathf.Clamp01((recoveryTimer - crouchHoldDuration) / standRecoveryDuration);
                    float smoothRise = Mathf.SmoothStep(0f, 1f, riseU);
                    currentCrouchWeight = Mathf.Lerp(0.88f, 0f, smoothRise);
                    currentHeadBowDeg = Mathf.Lerp(6f, 0f, Mathf.Clamp01(riseU * 1.4f));
                }

                // Dolly progress goes from 0.08 -> 0.72 during crouch stabilization & stand-up
                float beat2Dolly = Mathf.Lerp(0.08f, 0.72f, Mathf.SmoothStep(0f, 1f, recoveryTimer / recoveryTotal));
                ComputeDropAndDollyCamera(dropEndPos, beat2Dolly);
                yield return null;
            }

            currentCrouchWeight = 0f;
            currentHeadBowDeg = 0f;
            PlayReadyToSubmergeAudio();

            // 2c. First Heavy Step Forward (1.60s: ~9.4s -> 11.0s) while camera finishes pulling back
            BeginCutsceneWalk(walkAnimatorSpeed);
            float stepTimer = 0f;
            float currentSpeed = 0f;
            Vector3 marchPos = dropEndPos;

            while (stepTimer < firstStepDuration)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(SkipCutsceneRoutine()); yield break; }

                float dt = Time.deltaTime;
                stepTimer += dt;
                cutsceneElapsed += dt;

                float u = Mathf.Clamp01(stepTimer / firstStepDuration);
                currentSpeed = Mathf.Lerp(0f, wadingForwardSpeed, Mathf.SmoothStep(0f, 1f, u));
                marchPos = AdvanceJaegerAlongSeabed(marchPos, currentSpeed, dt);
                SetJaegerPose(marchPos, Quaternion.identity);

                float beat2Dolly = Mathf.Lerp(0.72f, 1.0f, Mathf.SmoothStep(0f, 1f, u));
                ComputeDropAndDollyCamera(marchPos, beat2Dolly);
                yield return null;
            }

            // =========================================================================
            // BEAT 3 (0:11 – 0:18): Wading & Submersion (7.0s)
            // 3-4 slow heavy steps down the sloping seabed; water rises past shoulders and visor;
            // camera dips beneath the waterline with dense bubbles & turbulence rushing upward past the lens.
            // =========================================================================
            float wadingTimer = 0f;
            float lensBurstTimer = 0f;

            while (wadingTimer < wadingSubmersionDuration)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(SkipCutsceneRoutine()); yield break; }

                float dt = Time.deltaTime;
                wadingTimer += dt;
                cutsceneElapsed += dt;

                float u = Mathf.Clamp01(wadingTimer / wadingSubmersionDuration);
                marchPos = AdvanceJaegerAlongSeabed(marchPos, wadingForwardSpeed, dt);
                SetJaegerPose(marchPos, Quaternion.identity);

                // Slightly increase water wetness once chest/shoulders submerge
                Shader.SetGlobalFloat(IdGlobalRainWetness, Mathf.Lerp(0.55f, 1.0f, u));

                // Track Gipsy at the same 45° 3/4 front tracking shot keeping his head near top of frame
                ComputeThreeQuarterTrackingCamera(marchPos, trackingDistance, trackingFov);

                // Check when the camera pierces the ocean surface waterline
                float camWaterY = waterSystem != null ? waterSystem.GetWaterHeight(desiredCamPos) : 0f;
                if (!hasSubmergedCamera && desiredCamPos.y < camWaterY + 0.35f)
                {
                    hasSubmergedCamera = true;
                    OnCameraSubmergedBeneathWaterline();
                }

                if (hasSubmergedCamera)
                {
                    lensBurstTimer += dt;
                    // Intense dense upward bubble & turbulence burst rushing past lens for ~2.6s on entry
                    float burstFade = 1f - Mathf.Clamp01(lensBurstTimer / 2.6f);
                    if (burstFade > 0.01f)
                    {
                        EmitLensCrossingBubblesAndTurbulence(desiredCamPos, desiredCamRot, burstFade);
                    }
                    UpdateUnderwaterParticlesFollow(marchPos, desiredCamPos);
                    UpdateSubmersionEarlyTint(Mathf.Clamp01((wadingTimer - 4.0f) / 3.0f));
                }

                yield return null;
            }

            if (!hasSubmergedCamera)
            {
                hasSubmergedCamera = true;
                OnCameraSubmergedBeneathWaterline();
            }

            // =========================================================================
            // BEAT 4 (0:18 – 0:26): The Trench Descent (8.0s)
            // Fully submerged, Gipsy marches steadily down the steep trench slope into deeper, darker water.
            // Over 8 seconds, gradually dim bright surface light into deep murky ocean blue
            // so Gipsy's glowing Conn-Pod visor and chest nuclear turbine stand out against the dark.
            // =========================================================================
            IsInDeepDescentPhase = true;
            if (jaegerAnimator != null)
            {
                jaegerAnimator.speed = trenchAnimatorSpeed;
            }

            float trenchTimer = 0f;
            float ambientBubbleAcc = 0f;

            while (trenchTimer < trenchDescentDuration)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(SkipCutsceneRoutine()); yield break; }

                float dt = Time.deltaTime;
                trenchTimer += dt;
                cutsceneElapsed += dt;

                float u = Mathf.Clamp01(trenchTimer / trenchDescentDuration);
                float dimT = Mathf.SmoothStep(0f, 1f, u);

                float stepSpeed = Mathf.Lerp(wadingForwardSpeed, trenchForwardSpeed, u);
                marchPos = AdvanceJaegerAlongSeabed(marchPos, stepSpeed, dt);
                currentSlopeLeanDeg = Mathf.Lerp(0f, 6.5f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u * 2.2f)));
                SetJaegerPose(marchPos, Quaternion.identity);

                // Gently tighten camera slightly as murk thickens so glowing visor & turbine stay crisp & dramatic
                float dist = Mathf.Lerp(trackingDistance, trackingDistance - 4.5f, dimT);
                ComputeThreeQuarterTrackingCamera(marchPos, dist, Mathf.Lerp(trackingFov, 40f, dimT));

                // Dim bright surface light into deep murky ocean blue + intensify visor/turbine contrast
                ApplyTrenchLightingAndFogRamp(dimT);
                ApplyGlowIntensity(Mathf.Lerp(1.35f, 2.45f, dimT), dimT);
                SetUnderwaterFloodlightVisibility(Mathf.Lerp(0.45f, 1.0f, dimT));

                UpdateUnderwaterParticlesFollow(marchPos, desiredCamPos);
                ambientBubbleAcc += dt;
                if (ambientBubbleAcc >= 0.08f)
                {
                    ambientBubbleAcc = 0f;
                    EmitAmbientTrenchBubbles(marchPos, desiredCamPos);
                }

                yield return null;
            }

            // =========================================================================
            // BEAT 5 (0:26 – 0:28): Transition to Boss Fight
            // Smoothly fade to black to transition directly into Operation Pitfall (Slattern encounter).
            // =========================================================================
            transitionTriggered = true;
            float fadeTimer = 0f;
            float startUnderVol = underwaterAmbienceSource != null ? underwaterAmbienceSource.volume : 0f;

            while (fadeTimer < fadeToBlackDuration)
            {
                float dt = Time.deltaTime;
                fadeTimer += dt;
                float u = Mathf.Clamp01(fadeTimer / fadeToBlackDuration);
                float smoothFade = Mathf.SmoothStep(0f, 1f, u);

                marchPos = AdvanceJaegerAlongSeabed(marchPos, trenchForwardSpeed * 0.9f, dt);
                SetJaegerPose(marchPos, Quaternion.identity);
                ComputeThreeQuarterTrackingCamera(marchPos, trackingDistance - 4.5f, 40f);
                UpdateUnderwaterParticlesFollow(marchPos, desiredCamPos);

                if (fadeOverlayGroup != null)
                {
                    fadeOverlayGroup.alpha = smoothFade;
                }
                if (underwaterAmbienceSource != null)
                {
                    underwaterAmbienceSource.volume = Mathf.Lerp(startUnderVol, 0f, smoothFade);
                }

                yield return null;
            }

            if (fadeOverlayGroup != null) fadeOverlayGroup.alpha = 1f;

            HandOffToBossEncounter(preFadeDuration: 0f);
        }

        private IEnumerator SkipCutsceneRoutine()
        {
            if (transitionTriggered) yield break;
            transitionTriggered = true;

            if (chinookAudioSource != null) chinookAudioSource.Stop();
            if (underwaterAmbienceSource != null) underwaterAmbienceSource.Stop();
            if (readyToSubmergeSource != null) readyToSubmergeSource.Stop();

            HandOffToBossEncounter(preFadeDuration: 0.25f);
            yield break;
        }

        private void HandOffToBossEncounter(float preFadeDuration)
        {
            isCutsceneRunning = false;
            var overlay = CutsceneBlackTransitionOverlay.GetOrCreate();
            if (fadeOverlayGroup != null && fadeOverlayGroup.alpha >= 0.99f)
            {
                overlay.SetBlackAlpha(1f);
            }
            overlay.TransitionToScene(targetGameplayScene, preFadeOutDuration: preFadeDuration, postFadeInDuration: 0.95f);
        }

        private void BeginCutsceneWalk(float speed)
        {
            if (jaegerAnimator == null) return;
            jaegerAnimator.speed = speed;
            jaegerAnimator.SetBool("IsRunning", false);
            jaegerAnimator.SetFloat("RunBlend", 0f);
            jaegerAnimator.SetFloat("WalkDirection", 1f);
            jaegerAnimator.SetFloat("Speed", 1.0f);
        }

        private Vector3 AdvanceJaegerAlongSeabed(Vector3 currentPos, float forwardSpeed, float dt)
        {
            Vector3 next = currentPos + Vector3.forward * (forwardSpeed * dt);
            float groundY = SampleSeabedHeight(next.x, next.z);
            next.y = groundY + JaegerGroundRootOffset;
            return next;
        }

        private void SetJaegerPose(Vector3 pos, Quaternion rot)
        {
            if (jaegerRigidbody != null)
            {
                jaegerRigidbody.position = pos;
                jaegerRigidbody.rotation = rot;
            }
            if (jaegerTransform != null)
            {
                jaegerTransform.position = pos;
                jaegerTransform.rotation = rot;
            }
        }

        private float SampleSeabedHeight(float worldX, float worldZ)
        {
            if (trenchTerrain != null)
            {
                return trenchTerrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + trenchTerrain.transform.position.y;
            }
            return -17.6f;
        }

        #region Camera Framing Choreography (3/4 Front 45-deg Angle)

        private void ComputeThreeQuarterAirliftCamera(Vector3 jaegerPos, out Vector3 camPos, out Quaternion camRot, out float fov, bool snapSmooth)
        {
            Vector3 threeQuarterDir = new Vector3(0.7071f, 0f, 0.7071f); // 45 deg front-right
            camPos = jaegerPos + threeQuarterDir * closeAirliftDistance + Vector3.up * closeAirliftHeightOffset;
            Vector3 lookTarget = jaegerPos + Vector3.up * closeAirliftLookYOffset;
            camRot = Quaternion.LookRotation((lookTarget - camPos).normalized, Vector3.up);
            fov = closeAirliftFov;

            if (snapSmooth && mainCamera != null)
            {
                desiredCamPos = camPos;
                desiredCamRot = camRot;
                desiredCamFov = fov;
                hasInitializedCamSmooth = false;
            }
        }

        private void ComputeDropAndDollyCamera(Vector3 jaegerPos, float dollyProgress)
        {
            Vector3 threeQuarterDir = new Vector3(0.7071f, 0f, 0.7071f);
            float t = Mathf.Clamp01(dollyProgress);

            float dist = Mathf.Lerp(closeAirliftDistance, trackingDistance, t);
            float fov = Mathf.Lerp(closeAirliftFov, trackingFov, t);

            // Keep Gipsy's head top near the upper portion of the visible letterbox frame as camera dollies out
            float headTopY = (headBone != null ? headBone.position.y + 1.65f : jaegerPos.y + 21.6f);
            float halfSpan = dist * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float wideAimY = headTopY - headTopFrameRatio * halfSpan;
            float wideCamY = Mathf.Max(headTopY + 1.8f, wideAimY + 7.0f);

            Vector3 closeCamPos = jaegerPos + threeQuarterDir * closeAirliftDistance + Vector3.up * closeAirliftHeightOffset;
            Vector3 closeAimPos = jaegerPos + Vector3.up * closeAirliftLookYOffset;

            Vector3 wideCamPos = new Vector3(jaegerPos.x, wideCamY, jaegerPos.z) + threeQuarterDir * trackingDistance;
            Vector3 wideAimPos = new Vector3(jaegerPos.x, wideAimY, jaegerPos.z);

            desiredCamPos = Vector3.Lerp(closeCamPos, wideCamPos, t);
            Vector3 aimPos = Vector3.Lerp(closeAimPos, wideAimPos, t);
            desiredCamRot = Quaternion.LookRotation((aimPos - desiredCamPos).normalized, Vector3.up);
            desiredCamFov = fov;
        }

        private void ComputeThreeQuarterTrackingCamera(Vector3 jaegerPos, float dist, float fov)
        {
            Vector3 threeQuarterDir = new Vector3(0.7071f, 0f, 0.7071f);
            float headTopY = headBone != null ? headBone.position.y + 1.65f : jaegerPos.y + 21.6f;
            float halfSpan = dist * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float aimY = headTopY - headTopFrameRatio * halfSpan;
            float camY = aimY + 7.2f;

            desiredCamPos = new Vector3(jaegerPos.x, camY, jaegerPos.z) + threeQuarterDir * dist;
            Vector3 aimPos = new Vector3(jaegerPos.x, aimY, jaegerPos.z);
            desiredCamRot = Quaternion.LookRotation((aimPos - desiredCamPos).normalized, Vector3.up);
            desiredCamFov = fov;
        }

        #endregion

        #region LateUpdate (Procedural Crouch IK & Smooth Camera Application)

        private void LateUpdate()
        {
            if (!isCutsceneRunning) return;

            ApplyProceduralJaegerRigPose();
            ApplyCameraTransformAndShake();
        }

        private void ApplyProceduralJaegerRigPose()
        {
            if (jaegerTransform == null) return;

            // 1. Subtle suspended hang pose while airborne under Chinook cables
            if (currentAirliftHangWeight > 0.001f && hipsBone != null && chestBone != null)
            {
                float hw = currentAirliftHangWeight;
                float sway = Mathf.Sin(cutsceneElapsed * 1.4f) * 2.2f * hw;
                if (spine1Bone != null) spine1Bone.rotation = Quaternion.AngleAxis(sway * 0.4f, jaegerTransform.forward) * spine1Bone.rotation;
                chestBone.rotation = Quaternion.AngleAxis(sway * 0.6f, jaegerTransform.forward) * chestBone.rotation;
                if (headBone != null)
                {
                    headBone.rotation = Quaternion.AngleAxis(-3.5f * hw, jaegerTransform.right) * headBone.rotation;
                }
            }

            // 2. Heavy Landing Impact Crouch & Stabilization IK
            if (currentCrouchWeight > 0.001f && hipsBone != null && thighL != null && shinL != null && footL != null && thighR != null && shinR != null && footR != null)
            {
                float cw = Mathf.Clamp01(currentCrouchWeight);
                Vector3 fwd = jaegerTransform.forward;
                Vector3 right = jaegerTransform.right;

                // Record standing feet BEFORE lowering hips, widen & slightly stagger hero landing stance
                Vector3 targetFootL = footL.position - right * (0.48f * cw) + fwd * (0.55f * cw);
                Vector3 targetFootR = footR.position + right * (0.48f * cw) - fwd * (0.55f * cw);

                // Lower hips cleanly and shift pelvis back slightly so knees drive forward heroically
                hipsBone.position += Vector3.down * (3.75f * cw) - fwd * (0.55f * cw);

                // Forward weight-absorption pitch through pelvis, spine, and chest
                hipsBone.rotation = Quaternion.AngleAxis(11f * cw, right) * hipsBone.rotation;
                if (spine1Bone != null) spine1Bone.rotation = Quaternion.AngleAxis(13f * cw, right) * spine1Bone.rotation;
                if (chestBone != null) chestBone.rotation = Quaternion.AngleAxis(11f * cw, right) * chestBone.rotation;
                if (headBone != null) headBone.rotation = Quaternion.AngleAxis(currentHeadBowDeg, right) * headBone.rotation;

                // Solve both legs back onto planted seabed feet
                Vector3 poleL = (fwd - right * 0.36f).normalized;
                Vector3 poleR = (fwd + right * 0.36f).normalized;
                SolveTwoBoneIKWorld(thighL, shinL, footL, targetFootL, poleL, keepTipLocalRotation: false);
                SolveTwoBoneIKWorld(thighR, shinR, footR, targetFootR, poleR, keepTipLocalRotation: false);

                // Brace arms wide & forward to balance 2,000 tons of momentum
                if (upperArmL != null && forearmL != null && handL != null)
                {
                    Vector3 braceL = upperArmL.position + (-right * 2.1f + fwd * 2.2f + Vector3.down * 4.2f);
                    Vector3 targetHandL = Vector3.Lerp(handL.position, braceL, cw);
                    Vector3 armPoleL = (-fwd - right * 0.85f + Vector3.down * 0.2f).normalized;
                    SolveTwoBoneIKWorld(upperArmL, forearmL, handL, targetHandL, armPoleL, keepTipLocalRotation: true);
                }
                if (upperArmR != null && forearmR != null && handR != null)
                {
                    Vector3 braceR = upperArmR.position + (right * 2.1f + fwd * 2.2f + Vector3.down * 4.2f);
                    Vector3 targetHandR = Vector3.Lerp(handR.position, braceR, cw);
                    Vector3 armPoleR = (-fwd + right * 0.85f + Vector3.down * 0.2f).normalized;
                    SolveTwoBoneIKWorld(upperArmR, forearmR, handR, targetHandR, armPoleR, keepTipLocalRotation: true);
                }
            }

            // 3. Subtle forward lean while marching down steep underwater trench slope
            if (currentSlopeLeanDeg > 0.01f && spine1Bone != null)
            {
                spine1Bone.rotation = Quaternion.AngleAxis(currentSlopeLeanDeg, jaegerTransform.right) * spine1Bone.rotation;
                if (chestBone != null)
                {
                    chestBone.rotation = Quaternion.AngleAxis(currentSlopeLeanDeg * 0.5f, jaegerTransform.right) * chestBone.rotation;
                }
            }

            // Keep rocket sockets synced if chest moved
            if (visualSockets != null)
            {
                visualSockets.UpdateRocketTransforms();
            }
        }

        private void ApplyCameraTransformAndShake()
        {
            if (mainCamera == null || !cameraControlActive) return;

            if (!hasInitializedCamSmooth)
            {
                mainCamera.transform.position = desiredCamPos;
                mainCamera.transform.rotation = desiredCamRot;
                mainCamera.fieldOfView = desiredCamFov;
                hasInitializedCamSmooth = true;
            }
            else
            {
                float dt = Time.deltaTime;
                Vector3 smoothedPos = Vector3.SmoothDamp(mainCamera.transform.position, desiredCamPos, ref camVelocity, 0.08f);
                Quaternion smoothedRot = Quaternion.Slerp(mainCamera.transform.rotation, desiredCamRot, Mathf.Clamp01(dt * 10f));

                Vector3 shakePos = cameraShake != null ? cameraShake.CurrentOffset : Vector3.zero;
                Vector3 shakeRot = cameraShake != null ? cameraShake.CurrentRotationOffset : Vector3.zero;

                mainCamera.transform.position = smoothedPos + smoothedRot * shakePos;
                mainCamera.transform.rotation = smoothedRot * Quaternion.Euler(shakeRot);
                mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, desiredCamFov, Mathf.Clamp01(dt * 8f));
            }
        }

        private static void SolveTwoBoneIKWorld(Transform root, Transform mid, Transform tip, Vector3 targetPos, Vector3 poleForward, bool keepTipLocalRotation)
        {
            Vector3 a = root.position;
            Vector3 b = mid.position;
            Vector3 c = tip.position;
            Quaternion origTipRot = tip.rotation;
            Quaternion origTipLocalRot = tip.localRotation;

            float l1 = Vector3.Distance(a, b);
            float l2 = Vector3.Distance(b, c);
            if (l1 < 0.001f || l2 < 0.001f) return;

            Vector3 toTarget = targetPos - a;
            float dist = toTarget.magnitude;
            float minReach = Mathf.Abs(l1 - l2) + 0.05f;
            float maxReach = (l1 + l2) * 0.995f;
            float clampedDist = Mathf.Clamp(dist, minReach, maxReach);
            Vector3 dir = dist > 0.0001f ? (toTarget / dist) : Vector3.down;
            Vector3 clampedTarget = a + dir * clampedDist;

            Vector3 animMidProj = a + Vector3.Project(b - a, dir);
            Vector3 animKneeDir = b - animMidProj;
            Vector3 poleProj = Vector3.ProjectOnPlane(poleForward, dir);
            Vector3 poleUnit = poleProj.sqrMagnitude > 0.0001f ? poleProj.normalized : dir;
            Vector3 bendDir = Vector3.ProjectOnPlane(animKneeDir * 0.45f + poleUnit * (l1 * 0.55f), dir);
            if (bendDir.sqrMagnitude < 0.0001f) bendDir = poleProj;
            if (bendDir.sqrMagnitude < 0.0001f) return;
            bendDir.Normalize();

            float cosAngle0 = Mathf.Clamp((l1 * l1 + clampedDist * clampedDist - l2 * l2) / (2f * l1 * clampedDist), -1f, 1f);
            float x = l1 * cosAngle0;
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

            if (keepTipLocalRotation) tip.localRotation = origTipLocalRot;
            else tip.rotation = origTipRot;
        }

        #endregion

        #region Chinook Squadron, Rotor Wash & Splashdown

        private void SpawnChinookSquadron()
        {
            activeChinooks.Clear();
            activeCables.Clear();

            for (int i = 0; i < chinookOffsets.Count; i++)
            {
                GameObject chinook = null;
                if (chinookPrefab != null)
                {
                    chinook = Instantiate(chinookPrefab);
                }
                else
                {
                    var model = Resources.Load<GameObject>("PacificRim_Chinook");
                    if (model != null) chinook = Instantiate(model);
                }

                if (chinook == null)
                {
                    chinook = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    chinook.transform.localScale = new Vector3(4f, 4f, 18f);
                }

                chinook.name = $"Cutscene_Chinook_{i + 1}";
                chinook.transform.position = jaegerTransform.position + chinookOffsets[i];
                chinook.transform.rotation = Quaternion.Euler(4f, 180f, 0f);
                activeChinooks.Add(chinook);

                // Cable LineRenderer
                var cableGo = new GameObject($"Cutscene_Cable_{i + 1}", typeof(LineRenderer));
                cableGo.transform.SetParent(chinook.transform, false);
                var lr = cableGo.GetComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.36f;
                lr.endWidth = 0.36f;
                if (cableMaterial != null)
                {
                    lr.material = cableMaterial;
                }
                else
                {
                    Shader cableShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (cableShader == null) cableShader = Shader.Find("Standard");
                    lr.material = new Material(cableShader);
                }
                lr.startColor = new Color(0.18f, 0.20f, 0.23f, 1f);
                lr.endColor = new Color(0.28f, 0.32f, 0.36f, 1f);
                lr.useWorldSpace = true;
                activeCables.Add(lr);
            }
        }

        private void UpdateChinookConvoy(Vector3 jaegerPos, float t)
        {
            for (int i = 0; i < activeChinooks.Count; i++)
            {
                if (activeChinooks[i] == null) continue;
                float bob = Mathf.Sin(t * 2.6f + i * 1.45f) * 0.45f;
                float bank = Mathf.Cos(t * 1.8f + i * 2.1f) * 2.2f;
                activeChinooks[i].transform.position = jaegerPos + chinookOffsets[i] + Vector3.up * bob;
                activeChinooks[i].transform.rotation = Quaternion.Euler(4f + bob * 1.4f, 180f, bank);
            }
        }

        private void UpdateSuspensionCables()
        {
            Transform anchor = chestBone != null ? chestBone : jaegerTransform;
            Vector3 harnessPos = anchor.position + Vector3.up * 1.4f;

            for (int i = 0; i < activeCables.Count; i++)
            {
                if (activeCables[i] == null || i >= activeChinooks.Count || activeChinooks[i] == null) continue;
                Transform hook = activeChinooks[i].transform.Find("CargoHook");
                if (hook == null) hook = activeChinooks[i].transform;
                activeCables[i].SetPosition(0, hook.position);
                activeCables[i].SetPosition(1, harnessPos);
            }
        }

        private void ReleaseCablesAndDisperseChinooks()
        {
            PacificRimAudioManager.Instance?.PlayDeploymentClampRelease(jaegerTransform.position + Vector3.up * 18f);

            for (int i = 0; i < activeCables.Count; i++)
            {
                if (activeCables[i] != null) Destroy(activeCables[i].gameObject);
            }
            activeCables.Clear();

            StartCoroutine(PeelAwayChinooksRoutine());
        }

        private IEnumerator PeelAwayChinooksRoutine()
        {
            float duration = 4.2f;
            float elapsed = 0f;
            float startVol = chinookAudioSource != null ? chinookAudioSource.volume : 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / duration);

                for (int i = 0; i < activeChinooks.Count; i++)
                {
                    if (activeChinooks[i] == null) continue;
                    Vector3 outward = activeChinooks[i].transform.position - jaegerTransform.position;
                    outward.y = 0f;
                    if (outward.sqrMagnitude < 0.01f) outward = (i % 2 == 0) ? Vector3.left : Vector3.right;
                    outward.Normalize();

                    Vector3 moveVel = outward * 22f + Vector3.forward * 14f + Vector3.up * 26f;
                    activeChinooks[i].transform.position += moveVel * Time.deltaTime;
                    float targetYaw = 180f + Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
                    activeChinooks[i].transform.rotation = Quaternion.Slerp(
                        activeChinooks[i].transform.rotation,
                        Quaternion.Euler(-16f, targetYaw, (i % 2 == 0 ? 18f : -18f)),
                        Time.deltaTime * 2.8f);
                }

                if (chinookAudioSource != null)
                {
                    chinookAudioSource.volume = Mathf.Lerp(startVol, 0f, u);
                }

                yield return null;
            }

            for (int i = 0; i < activeChinooks.Count; i++)
            {
                if (activeChinooks[i] != null) Destroy(activeChinooks[i]);
            }
            activeChinooks.Clear();

            if (chinookAudioSource != null)
            {
                Destroy(chinookAudioSource.gameObject);
                chinookAudioSource = null;
            }
        }

        private void EmitRotorWashSeaSpray(Vector3 jaegerPos)
        {
            float seaY = waterSystem != null ? waterSystem.GetWaterHeight(jaegerPos) : 0f;
            Vector3 center = new Vector3(jaegerPos.x, seaY, jaegerPos.z);

            if (waterSystem != null)
            {
                Vector2 jitter = Random.insideUnitCircle * 11f;
                waterSystem.AddFootstepWave(center + new Vector3(jitter.x, 0f, jitter.y), Random.Range(10f, 18f), Random.Range(0.45f, 0.95f));
            }

            if (splashManager != null && Random.value < 0.55f)
            {
                Vector2 off = Random.insideUnitCircle * 16f;
                Vector3 p = new Vector3(center.x + off.x, seaY, center.z + off.y);
                splashManager.SpawnCrestFoam(p, Random.Range(0.65f, 1.0f));
            }

            if (rotorWashSprayPs != null)
            {
                for (int i = 0; i < 22; i++)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    float r = Random.Range(2f, 24f);
                    Vector3 pos = center + radial * r + Vector3.up * 0.25f;
                    Vector3 vel = radial * Random.Range(9.5f, 22f) + Vector3.up * Random.Range(6.5f, 16.5f);

                    var ep = new ParticleSystem.EmitParams
                    {
                        position = pos,
                        velocity = vel,
                        startSize = Random.Range(0.45f, 1.15f),
                        startLifetime = Random.Range(0.95f, 1.75f),
                        startColor = new Color(0.94f, 0.98f, 1f, Random.Range(0.70f, 0.95f))
                    };
                    rotorWashSprayPs.Emit(ep, 1);
                }
            }

            if (rotorWashMistPs != null)
            {
                for (int i = 0; i < 7; i++)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    float r = Random.Range(4f, 22f);
                    Vector3 pos = center + radial * r + Vector3.up * Random.Range(0.6f, 2.5f);
                    Vector3 vel = radial * Random.Range(6.5f, 15.0f) + Vector3.up * Random.Range(2.8f, 6.8f);

                    var ep = new ParticleSystem.EmitParams
                    {
                        position = pos,
                        velocity = vel,
                        startSize = Random.Range(9.0f, 17.5f),
                        startLifetime = Random.Range(1.8f, 3.2f),
                        rotation = Random.Range(0f, 360f),
                        startColor = new Color(0.90f, 0.96f, 1f, Random.Range(0.28f, 0.50f))
                    };
                    rotorWashMistPs.Emit(ep, 1);
                }
            }
        }

        private void TriggerSplashdownImpact(Vector3 surfaceImpactPos, bool isFinalSeabedHit)
        {
            if (waterSystem != null)
            {
                waterSystem.AddFootstepWave(surfaceImpactPos, isFinalSeabedHit ? 46f : 34f, isFinalSeabedHit ? 3.2f : 2.2f);
            }

            if (splashManager != null)
            {
                if (!isFinalSeabedHit)
                {
                    // Main colossal entry explosion on touching the ocean surface
                    splashManager.SpawnColossalTitanDropSplash(surfaceImpactPos);
                }
                else
                {
                    // Expanding outer ring shockwave around crouch so Gipsy stays clearly visible as waves settle
                    splashManager.SpawnFootstepSplash(surfaceImpactPos, 1.85f);
                    for (int k = 0; k < 6; k++)
                    {
                        float a = (k / 6f) * Mathf.PI * 2f;
                        Vector3 ringPos = surfaceImpactPos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 8.5f;
                        splashManager.SpawnBodySplash(ringPos, Vector3.up, 0.85f, 1.25f);
                    }
                }
            }

            var audioMgr = PacificRimAudioManager.Instance;
            if (audioMgr != null)
            {
                if (!isFinalSeabedHit)
                {
                    audioMgr.PlayOceanFootstep(surfaceImpactPos, 1.8f);
                    audioMgr.PlayAtPosition("water_bigsplash", surfaceImpactPos, 1.0f, 0.85f, 0.98f);
                }
                else
                {
                    audioMgr.PlayOceanFootstep(surfaceImpactPos, 2.2f);
                    audioMgr.PlayKaijuSlamImpact(surfaceImpactPos);
                    audioMgr.PlayAtPosition("Earthquake 3 - Big", surfaceImpactPos, 0.95f, 0.92f, 1.02f);
                    audioMgr.PlayAtPosition("Tsunami hitting a Large City", surfaceImpactPos, 0.72f, 0.95f, 1.05f);
                }
            }

            if (cameraShake != null)
            {
                cameraShake.TriggerPlayerHitFeedback(isHeavy: isFinalSeabedHit);
            }
        }

        #endregion

        #region Submersion Transition, Trench Lighting & Glow

        private void OnCameraSubmergedBeneathWaterline()
        {
            if (splashManager != null)
            {
                splashManager.isDeepUnderwaterMode = true;
            }

            if (PacificRimAudioManager.Instance != null)
            {
                PacificRimAudioManager.Instance.isUnderwaterLevel = true;
                var subSplashClip = PacificRimAudioManager.Instance.GetClip("water_bigsplash");
                if (subSplashClip != null)
                {
                    PacificRimAudioManager.Instance.PlayAtPosition(subSplashClip, desiredCamPos, 0.75f, 0.70f, 0.85f, bypassLowPass: true);
                }
            }

            StartUnderwaterAmbienceLoop();

            if (cameraLowPass != null)
            {
                cameraLowPass.cutoffFrequency = 2600f;
            }

            // Refresh & switch volumetric floodlight beams into underwater mode immediately
            RefreshFloodlightSockets();
            for (int i = 0; i < floodlightBeamRenderers.Count; i++)
            {
                var r = floodlightBeamRenderers[i];
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(IdWaterLevel))
                {
                    r.sharedMaterial.SetFloat(IdWaterLevel, -500f);
                }
            }

            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = RenderSettings.fogColor;
            }

            if (marineSnowPs != null)
            {
                var emission = marineSnowPs.emission;
                emission.rateOverTime = 165f;
            }
        }

        private void EmitLensCrossingBubblesAndTurbulence(Vector3 camPos, Quaternion camRot, float intensity)
        {
            Vector3 camFwd = camRot * Vector3.forward;
            Vector3 camRight = camRot * Vector3.right;
            Vector3 camUp = camRot * Vector3.up;

            if (lensBubblesPs != null)
            {
                int count = Mathf.CeilToInt(26 * intensity);
                for (int i = 0; i < count; i++)
                {
                    Vector3 spawnPos = camPos
                                     + camFwd * Random.Range(1.6f, 7.2f)
                                     + camRight * Random.Range(-4.5f, 4.5f)
                                     + camUp * Random.Range(-3.2f, 0.8f);
                    Vector3 vel = Vector3.up * Random.Range(9.5f, 19.0f)
                                + camRight * Random.Range(-2.5f, 2.5f)
                                + camFwd * Random.Range(-1.5f, 1.5f);

                    var ep = new ParticleSystem.EmitParams
                    {
                        position = spawnPos,
                        velocity = vel,
                        startSize = Random.Range(0.05f, 0.21f),
                        startLifetime = Random.Range(0.75f, 1.40f),
                        startColor = new Color(0.84f, 0.96f, 1.0f, Random.Range(0.38f, 0.68f) * intensity)
                    };
                    lensBubblesPs.Emit(ep, 1);
                }
            }

            if (lensTurbulencePs != null)
            {
                int turbCount = Mathf.CeilToInt(7 * intensity);
                for (int i = 0; i < turbCount; i++)
                {
                    Vector3 spawnPos = camPos
                                     + camFwd * Random.Range(2.2f, 8.5f)
                                     + camRight * Random.Range(-5.0f, 5.0f)
                                     + camUp * Random.Range(-3.6f, 0.5f);
                    Vector3 vel = Vector3.up * Random.Range(7.5f, 14.5f) + camRight * Random.Range(-2.0f, 2.0f);

                    var ep = new ParticleSystem.EmitParams
                    {
                        position = spawnPos,
                        velocity = vel,
                        startSize = Random.Range(1.2f, 3.2f),
                        startLifetime = Random.Range(0.70f, 1.30f),
                        rotation = Random.Range(0f, 360f),
                        startColor = new Color(0.50f, 0.84f, 0.96f, Random.Range(0.22f, 0.42f) * intensity)
                    };
                    lensTurbulencePs.Emit(ep, 1);
                }
            }
        }

        private void EmitAmbientTrenchBubbles(Vector3 jaegerPos, Vector3 camPos)
        {
            if (lensBubblesPs == null) return;
            for (int i = 0; i < 6; i++)
            {
                Vector3 center = Vector3.Lerp(jaegerPos + Vector3.up * 12f, camPos, Random.value);
                Vector3 pos = center + Random.insideUnitSphere * 18f;
                Vector3 vel = Vector3.up * Random.Range(2.2f, 5.4f) + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
                var ep = new ParticleSystem.EmitParams
                {
                    position = pos,
                    velocity = vel,
                    startSize = Random.Range(0.08f, 0.30f),
                    startLifetime = Random.Range(2.2f, 4.2f),
                    startColor = new Color(0.74f, 0.94f, 1.0f, Random.Range(0.45f, 0.80f))
                };
                lensBubblesPs.Emit(ep, 1);
            }
        }

        private void UpdateUnderwaterParticlesFollow(Vector3 jaegerPos, Vector3 camPos)
        {
            if (marineSnowPs != null)
            {
                marineSnowPs.transform.position = Vector3.Lerp(jaegerPos + Vector3.up * 14f, camPos, 0.45f);
            }
        }

        private void UpdateSubmersionEarlyTint(float t)
        {
            t = Mathf.Clamp01(t);
            RenderSettings.fogColor = Color.Lerp(SurfaceFogColor, ShallowUnderwaterFogColor, t);
            RenderSettings.fogDensity = Mathf.Lerp(0.0022f, 0.0075f, t);

            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.42f, 0.56f, 0.70f), new Color(0.14f, 0.34f, 0.48f), t);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.28f, 0.42f, 0.54f), new Color(0.09f, 0.24f, 0.36f), t);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.10f, 0.16f, 0.22f), new Color(0.03f, 0.10f, 0.16f), t);

            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.color = Color.Lerp(SurfaceLightColor, ShallowUnderwaterLightColor, t);
                mainDirectionalLight.intensity = Mathf.Lerp(2.15f, 1.45f, t);
            }

            if (mainCamera != null && hasSubmergedCamera)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = RenderSettings.fogColor;
            }

            if (oceanMatInstance != null)
            {
                if (oceanMatInstance.HasProperty(IdEdgeFogColor))
                    oceanMatInstance.SetColor(IdEdgeFogColor, RenderSettings.fogColor);
            }

            SetUnderwaterFloodlightVisibility(Mathf.Lerp(0.0f, 0.45f, t));
            ApplyGlowIntensity(Mathf.Lerp(1.0f, 1.35f, t), 0f);
        }

        private void ApplyTrenchLightingAndFogRamp(float dimT)
        {
            dimT = Mathf.Clamp01(dimT);

            // Dim bright surface light over 8 seconds into deep, murky ocean blue
            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.color = Color.Lerp(ShallowUnderwaterLightColor, DeepOceanLightColor, dimT);
                mainDirectionalLight.intensity = Mathf.Lerp(1.45f, 0.06f, Mathf.Pow(dimT, 0.85f));
                mainDirectionalLight.shadowStrength = Mathf.Lerp(0.82f, 0.22f, dimT);
            }

            Color fogCol = Color.Lerp(ShallowUnderwaterFogColor, DeepMurkyOceanBlue, dimT);
            RenderSettings.fogColor = fogCol;
            RenderSettings.fogDensity = Mathf.Lerp(0.011f, 0.022f, dimT);

            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.14f, 0.34f, 0.48f), new Color(0.022f, 0.088f, 0.175f), dimT);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.09f, 0.24f, 0.36f), new Color(0.015f, 0.062f, 0.130f), dimT);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.03f, 0.10f, 0.16f), new Color(0.008f, 0.032f, 0.072f), dimT);

            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = fogCol;
            }

            if (oceanMatInstance != null)
            {
                if (oceanMatInstance.HasProperty(IdEdgeFogColor)) oceanMatInstance.SetColor(IdEdgeFogColor, fogCol);
                if (oceanMatInstance.HasProperty(IdShallowColor))
                {
                    oceanMatInstance.SetColor(IdShallowColor, Color.Lerp(new Color(0.025f, 0.19f, 0.28f), DeepMurkyOceanBlue, dimT));
                }
                if (oceanMatInstance.HasProperty(IdDeepColor))
                {
                    oceanMatInstance.SetColor(IdDeepColor, Color.Lerp(new Color(0.008f, 0.032f, 0.068f), DeepMurkyOceanBlue * 0.6f, dimT));
                }
            }

            if (cameraLowPass != null)
            {
                cameraLowPass.cutoffFrequency = Mathf.Lerp(2600f, 1850f, dimT);
            }

            if (underwaterAmbienceSource != null)
            {
                float sfxVol = PacificRimAudioManager.Instance != null ? PacificRimAudioManager.Instance.SfxVolume : 0.55f;
                underwaterAmbienceSource.volume = Mathf.Lerp(0.18f, 0.36f, dimT) * sfxVol;
            }

            // Tune Post-Processing Volume for moody murky blue & crisp bloom on Visor / Turbine
            if (volColorAdj != null)
            {
                volColorAdj.postExposure.Override(Mathf.Lerp(0.20f, -0.18f, dimT));
                volColorAdj.contrast.Override(Mathf.Lerp(8f, 18f, dimT));
                volColorAdj.colorFilter.Override(Color.Lerp(Color.white, new Color(0.76f, 0.90f, 1.0f, 1f), dimT));
            }
            if (volBloom != null)
            {
                volBloom.threshold.Override(Mathf.Lerp(1.05f, 0.88f, dimT));
                volBloom.intensity.Override(Mathf.Lerp(0.85f, 1.60f, dimT));
            }
            if (volVignette != null)
            {
                volVignette.intensity.Override(Mathf.Lerp(0.18f, 0.42f, dimT));
                volVignette.color.Override(DeepMurkyOceanBlue * 0.4f);
            }
            if (volSplitToning != null)
            {
                volSplitToning.shadows.Override(Color.Lerp(new Color(0.45f, 0.50f, 0.55f), new Color(0.04f, 0.18f, 0.36f), dimT));
                volSplitToning.highlights.Override(Color.Lerp(Color.white, new Color(1.0f, 0.72f, 0.32f), dimT));
            }
        }

        private void ApplyGlowIntensity(float glowScale, float darkRamp01)
        {
            if (visorMatInstance != null)
            {
                Color visorBase = new Color(1.0f, 0.65f, 0.14f, 1f);
                Color visorHdr = new Color(1.0f, 0.52f, 0.04f, 1f) * (2.6f * glowScale);
                if (visorMatInstance.HasProperty("_BaseColor")) visorMatInstance.SetColor("_BaseColor", visorBase);
                if (visorMatInstance.HasProperty("_Color")) visorMatInstance.SetColor("_Color", visorBase);
                if (visorMatInstance.HasProperty(IdEmissionColor)) visorMatInstance.SetColor(IdEmissionColor, visorHdr);
            }

            if (turbineMatInstance != null)
            {
                Color turbineHdr = new Color(1.0f, 0.45f, 0.02f, 1f) * (2.8f * glowScale);
                if (turbineMatInstance.HasProperty(IdEmissionColor)) turbineMatInstance.SetColor(IdEmissionColor, turbineHdr);
            }

            for (int i = 0; i < armorMatInstances.Count; i++)
            {
                if (armorMatInstances[i] != null && armorMatInstances[i].HasProperty(IdVisorEmission))
                {
                    armorMatInstances[i].SetFloat(IdVisorEmission, 2.2f * glowScale);
                }
            }

            if (visorGlowLight != null)
            {
                visorGlowLight.intensity = Mathf.Lerp(2f, 45f, darkRamp01);
            }
            if (turbineGlowLight != null)
            {
                turbineGlowLight.intensity = Mathf.Lerp(5f, 95f, darkRamp01);
            }
        }

        private void SetUnderwaterFloodlightVisibility(float factor)
        {
            factor = Mathf.Clamp01(factor);
            for (int i = 0; i < floodlightSpots.Count; i++)
            {
                if (floodlightSpots[i] == null) continue;
                float baseInt = i < floodlightBaseIntensities.Count ? floodlightBaseIntensities[i] : 2800f;
                floodlightSpots[i].intensity = factor <= 0.02f ? 0f : baseInt * Mathf.Lerp(0.15f, 1.25f, factor);
            }

            for (int i = 0; i < floodlightBeamRenderers.Count; i++)
            {
                var r = floodlightBeamRenderers[i];
                if (r == null || r.sharedMaterial == null) continue;
                r.enabled = factor > 0.02f;
                if (r.sharedMaterial.HasProperty("_Intensity"))
                {
                    r.sharedMaterial.SetFloat("_Intensity", Mathf.Lerp(0.0f, 1.85f, factor));
                }
            }
        }

        #endregion

        #region Audio & Input Helpers

        private void StartChinookAudio()
        {
            var go = new GameObject("Cutscene_ChinookAudio");
            go.transform.SetParent(transform, false);
            chinookAudioSource = go.AddComponent<AudioSource>();
            chinookAudioSource.spatialBlend = 0.25f;
            chinookAudioSource.loop = true;
            float sfxVol = PacificRimAudioManager.Instance != null ? PacificRimAudioManager.Instance.SfxVolume : 0.55f;
            chinookAudioSource.volume = 0.20f * sfxVol;

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

        private void StartUnderwaterAmbienceLoop()
        {
            if (underwaterAmbienceSource != null) return;
            var go = new GameObject("Cutscene_UnderwaterAmbience");
            go.transform.SetParent(transform, false);
            underwaterAmbienceSource = go.AddComponent<AudioSource>();
            underwaterAmbienceSource.spatialBlend = 0f;
            underwaterAmbienceSource.loop = true;
            float sfxVol = PacificRimAudioManager.Instance != null ? PacificRimAudioManager.Instance.SfxVolume : 0.55f;
            underwaterAmbienceSource.volume = 0.18f * sfxVol;

            AudioClip clip = PacificRimAudioManager.Instance != null ? PacificRimAudioManager.Instance.GetClip("ambientsound_underwater") : null;
            if (clip == null) clip = Resources.Load<AudioClip>("ambientsound_underwater");
#if UNITY_EDITOR
            if (clip == null) clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SUIMONO - WATER SYSTEM 2/SOUNDS/ambientsound_underwater.wav");
#endif
            if (clip != null)
            {
                underwaterAmbienceSource.clip = clip;
                underwaterAmbienceSource.Play();
            }
        }

        private void PlayReadyToSubmergeAudio()
        {
            AudioClip clip = readyToSubmergeClip;
            if (clip == null && PacificRimAudioManager.Instance != null)
            {
                clip = PacificRimAudioManager.Instance.GetClip("ReadyToSubmerge");
            }
            if (clip == null) clip = Resources.Load<AudioClip>("ReadyToSubmerge");
#if UNITY_EDITOR
            if (clip == null) clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/ReadyToSubmerge.MP3");
#endif
            if (clip == null) return;

            if (readyToSubmergeSource == null)
            {
                var go = new GameObject("Cutscene_ReadyToSubmergeAudio");
                go.transform.SetParent(transform, false);
                readyToSubmergeSource = go.AddComponent<AudioSource>();
                readyToSubmergeSource.spatialBlend = 0f;
                readyToSubmergeSource.loop = false;
                readyToSubmergeSource.priority = 0;
                readyToSubmergeSource.bypassEffects = true;
                readyToSubmergeSource.bypassListenerEffects = true;
                readyToSubmergeSource.bypassReverbZones = true;
            }

            float sfxVol = PacificRimAudioManager.Instance != null ? PacificRimAudioManager.Instance.SfxVolume : 0.55f;
            readyToSubmergeSource.volume = 1f;
            float volumeScale = sfxVol <= 0.001f ? 0f : Mathf.Clamp(1.8f * Mathf.Max(0.85f, sfxVol / 0.55f), 1.2f, 2.5f);
            readyToSubmergeSource.PlayOneShot(clip, volumeScale);
        }

        private static bool WasSkipPressed()
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

        private static Texture2D CreateSoftCircleTexture(int size, bool isBubble)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
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
                        if (isBubble)
                        {
                            float rim = Mathf.Exp(-Mathf.Pow((r - 0.84f) / 0.095f, 2f)) * 0.56f;
                            float outerSoft = SmoothRange(1.0f, 0.78f, r);
                            float interior = 0.045f * (1f - SmoothRange(0f, 0.88f, r));
                            float angle = Mathf.Atan2(v, u);
                            float crescent = Mathf.Pow(Mathf.Clamp01(Mathf.Cos(angle - 2.35f)), 4.0f) * Mathf.Exp(-Mathf.Pow((r - 0.66f) / 0.14f, 2f)) * 0.34f;
                            float hu = u + 0.34f, hv = v - 0.34f;
                            float hl = SmoothRange(0.11f, 0.02f, Mathf.Sqrt(hu * hu + hv * hv)) * 0.28f;
                            a = Mathf.Clamp01((rim + interior + crescent + hl) * outerSoft);
                        }
                        else
                        {
                            a = Mathf.Pow(SmoothRange(1f, 0.05f, r), 1.3f);
                        }
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        private static Texture2D CreateMistPuffTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
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
                        float n = Mathf.PerlinNoise(u * 2.1f + 14.3f, v * 2.1f + 8.7f) * 0.55f
                                + Mathf.PerlinNoise(u * 4.5f + 3.1f, v * 4.5f + 17.5f) * 0.30f
                                + Mathf.PerlinNoise(u * 9.1f + 9.8f, v * 9.1f + 2.4f) * 0.15f;
                        float radial = SmoothRange(1f, 0.08f, r);
                        a = Mathf.Clamp01(n * 1.4f - 0.12f) * Mathf.Pow(radial, 1.35f);
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        private static float SmoothRange(float edge0, float edge1, float x)
        {
            if (Mathf.Approximately(edge0, edge1)) return x >= edge1 ? 1f : 0f;
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        #endregion
    }
}
