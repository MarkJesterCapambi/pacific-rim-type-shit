using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PacificRimAtmosphere;
using PacificRimAudio;
using PacificRimUI;
using PacificRimWater;

namespace PacificRimCutscene
{
    /// <summary>
    /// Orchestrates Beats 1 & 2 (0:00–0:11) of the 38-second movie-accurate Operation Pitfall Ending Cutscene:
    /// - 0:00–0:06 | Dragging Slattern Across the Abyss: Fade in from black with letterbox bars on the
    ///               pitch-black floor of the Mariana Trench, illuminated only by Gipsy's headlights,
    ///               glowing orange chest turbine, and the distant magenta-blue glow of the Breach.
    ///               Low-angle tracking shot moving alongside battle-damaged, sparking Gipsy Danger
    ///               leaning forward and dragging Slattern's limp Category V carcass by the tail
    ///               through kicked-up seabed silt.
    /// - 0:06–0:11 | Triggering the Genetic Lock & The Leap: Wide 3/4 rear/side angle looking down into
    ///               the jagged pulsing Breach chasm. Gipsy heaves Slattern into the Breach energy field,
    ///               causing the violet-and-blue bio-membrane to flare violently as it reads Kaiju DNA
    ///               and rips wide open. Without hesitating, Gipsy dives headfirst into the blinding rift.
    /// At 0:11, activates the preloaded "The Anteverse" scene for a seamless hard cut into Beat 3 (Wormhole Throat).
    /// </summary>
    [DefaultExecutionOrder(75)]
    public class OperationPitfallEndingCutscene : MonoBehaviour
    {
        public static OperationPitfallEndingCutscene Instance { get; private set; }

        public const string AnteverseSceneName = "The Anteverse";

        [Header("Scene References (Auto-Resolved if Null)")]
        public Transform jaegerTransform;
        public Transform slatternTransform;
        public Transform breachTransform;
        public Terrain trenchTerrain;

        [Header("Timeline (Seconds)")]
        public float fadeToStageDuration = 0.85f;
        public float dragDuration = 6.0f;
        public float geneticLockAndLeapDuration = 5.0f;

        public bool isCutsceneRunning { get; private set; }

        // Runtime cached references
        private SimpleJaegerController jaegerController;
        private JaegerVisualSockets visualSockets;
        private Animator jaegerAnimator;
        private Rigidbody jaegerRigidbody;
        private KaijuAI kaijuAI;
        private Animator slatternAnimator;
        private Rigidbody slatternRigidbody;
        private KaijuBioluminescenceController slatternBio;
        private UnderwaterEnvironmentController underwaterEnv;
        private AtmosphereAudioController atmosphereAudio;
        private WaterSplashManager splashManager;
        private Camera mainCamera;
        private PacificRimCameraShake cameraShake;
        private AudioLowPassFilter cameraLowPass;
        private Light mainDirectionalLight;

        // Jaeger bones for procedural lean, arm/leg IK, finger curl, and sparks
        private Transform hipsBone, spine1Bone, chestBone, headBone;
        private Transform upperArmL, forearmL, handL, upperArmR, forearmR, handR;
        private Transform thighL, shinL, footL, thighR, shinR, footR;
        private Transform thumbR1, thumbR2, indexR1, indexR2, indexR3, middleR1, middleR2, middleR3, ringR1, ringR2, ringR3, pinkyR1, pinkyR2, pinkyR3;
        private Transform thumbL1, thumbL2, indexL1, indexL2, indexL3, middleL1, middleL2, middleL3, ringL1, ringL2, ringL3, pinkyL1, pinkyL2, pinkyL3;

        // Slattern bones & articulated world-space physics ragdoll
        private readonly List<Transform> slatternHeadNeckChain = new List<Transform>();
        private Transform slatternHeadBone;
        private Transform slatternTorsoDragBone;
        private SlatternCutsceneRagdoll slatternRagdoll;
        private TerrainCollider trenchTerrainCollider;

        // Runtime visual rigs
        private Material visorMatInstance;
        private Material turbineMatInstance;
        private readonly List<Material> armorMatInstances = new List<Material>();
        private Light coreGlowLight;
        private Light visorGlowLight;

        private readonly List<ParticleSystem> jointSparkSystems = new List<ParticleSystem>();
        private readonly List<Transform> jointSparkAnchors = new List<Transform>();
        private Light jointSparkFlashLight;
        private float nextSparkBurstTime = 0f;

        private ParticleSystem dragSiltPs;
        private ParticleSystem dragBubblesPs;

        // Breach magenta-blue recolor + bio-membrane
        private Material breachMembraneMat;
        private GameObject breachMembraneObj;
        private Light breachFlareLight;
        private readonly List<Material> runtimeBreachMaterials = new List<Material>();
        private readonly List<Color> breachMatBaseColors = new List<Color>();
        private readonly List<Color> breachMatEmColors = new List<Color>();
        private readonly List<Transform> breachGodRayTransforms = new List<Transform>();
        private readonly List<Light> breachLights = new List<Light>();
        private readonly List<float> breachLightBaseIntensities = new List<float>();

        // UI & overlays
        private RectTransform letterboxRoot;
        private CanvasGroup letterboxCanvasGroup;
        private CanvasGroup fadeOverlayGroup;
        private Image fadeOverlayImage;
        private Text skipPromptText;

        // Audio
        private AudioSource underwaterRumbleSource;
        private AudioSource dragAudioSource;
        private AudioSource sparkAudioSource;
        private AudioSource cutsceneSfxSource;

        // Background async preload of The Anteverse
        private AsyncOperation anteversePreloadOp;
        private float recordedCombatTime;

        // Procedural LateUpdate control state
        private bool applyLateUpdatePoses = false;
        private bool lockCameraInLateUpdate = false;
        private Vector3 camTargetPos;
        private Quaternion camTargetRot = Quaternion.identity;
        private float camTargetFov = 50f;

        private float jaegerForwardLeanDeg = 0f;
        private float jaegerTorsoTwistDeg = 0f;
        private float jaegerCrouchWeight = 0f;
        private float jaegerCrouchDropMeters = 2.4f;
        private float jaegerHeadTiltDeg = 0f;
        private float jaegerDiveBlend = 0f;
        private float jaegerPlungeTime = 0f;
        private float braceLeftArmWeight = 0f;
        private float gripRightFistWeight = 0f;
        private bool ikGripHead = false;
        private bool ikHeaveForward = false;
        private float heaveAnimWeight = 0f;
        private float heaveForwardT = 0f;
        private bool headAttachedToGrip = false;
        private Vector3 gripWorldTarget;
        private Vector3 slatternDragDirection = Vector3.forward;
        private bool useRagdollPhysics = false;
        private bool isHeavingRagdoll = false;
        private float heaveAssistT = 0f;
        private Vector3 currentHeaveVelocityTarget;

        private static readonly int IdEmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int IdColor = Shader.PropertyToID("_Color");
        private static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
        private static readonly int IdSmoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int IdWetSmoothness = Shader.PropertyToID("_WetSmoothness");
        private static readonly int IdEdgeWearStrength = Shader.PropertyToID("_EdgeWearStrength");
        private static readonly int IdNormalStrength = Shader.PropertyToID("_NormalStrength");
        private static readonly int IdRippleProgress = Shader.PropertyToID("_RippleProgress");
        private static readonly int IdHitUV = Shader.PropertyToID("_HitUV");
        private static readonly int IdTearAmount = Shader.PropertyToID("_TearAmount");
        private static readonly int IdCloseAmount = Shader.PropertyToID("_CloseAmount");
        private static readonly int IdFlareIntensity = Shader.PropertyToID("_FlareIntensity");

        public static void StartFromSlatternDefeat()
        {
            if (Instance != null && Instance.isCutsceneRunning) return;

            var inst = Instance;
            if (inst == null)
            {
                inst = FindAnyObjectByType<OperationPitfallEndingCutscene>();
            }
            if (inst == null)
            {
                var go = new GameObject("OperationPitfall_EndingCutscene");
                inst = go.AddComponent<OperationPitfallEndingCutscene>();
            }
            inst.BeginEndingSequence();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (slatternRagdoll != null)
            {
                slatternRagdoll.Dispose();
                slatternRagdoll = null;
            }
            if (Instance == this) Instance = null;
        }

        public void BeginEndingSequence()
        {
            if (isCutsceneRunning) return;
            isCutsceneRunning = true;
            recordedCombatTime = JaegerHUD.Instance != null ? JaegerHUD.Instance.CurrentCombatTime : 0f;
            StartCoroutine(RunEndingCutsceneRoutine());
        }

        private void ResolveReferences()
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
            }

            if (slatternTransform == null)
            {
                var sgo = GameObject.Find("Kaiju_Slattern");
                if (sgo != null) slatternTransform = sgo.transform;
            }
            if (slatternTransform != null)
            {
                kaijuAI = slatternTransform.GetComponent<KaijuAI>();
                slatternAnimator = slatternTransform.GetComponentInChildren<Animator>();
                slatternRigidbody = slatternTransform.GetComponent<Rigidbody>();
                slatternBio = slatternTransform.GetComponent<KaijuBioluminescenceController>();
                ResolveSlatternBones();
            }

            if (breachTransform == null)
            {
                var bgo = GameObject.Find("The_Breach");
                if (bgo != null) breachTransform = bgo.transform;
            }

            if (trenchTerrain == null) trenchTerrain = FindAnyObjectByType<Terrain>();
            if (trenchTerrain != null) trenchTerrainCollider = trenchTerrain.GetComponent<TerrainCollider>();
            if (underwaterEnv == null) underwaterEnv = UnderwaterEnvironmentController.Instance ?? FindAnyObjectByType<UnderwaterEnvironmentController>();
            if (atmosphereAudio == null) atmosphereAudio = FindAnyObjectByType<AtmosphereAudioController>();
            if (splashManager == null) splashManager = WaterSplashManager.Instance ?? FindAnyObjectByType<WaterSplashManager>();

            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraShake = mainCamera.GetComponent<PacificRimCameraShake>() ?? mainCamera.gameObject.AddComponent<PacificRimCameraShake>();
                cameraLowPass = mainCamera.GetComponent<AudioLowPassFilter>() ?? mainCamera.gameObject.AddComponent<AudioLowPassFilter>();
            }

            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (l != null && l.type == LightType.Directional && l.gameObject.name != "LightningFlashLight")
                {
                    mainDirectionalLight = l;
                    break;
                }
            }

            EnsureCutsceneUI();
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
                    case "Upperarm.L": upperArmL = t; break;
                    case "Forearm.L": forearmL = t; break;
                    case "Hand.L": handL = t; break;
                    case "Upperarm.R": upperArmR = t; break;
                    case "Forearm.R": forearmR = t; break;
                    case "Hand.R": handR = t; break;
                    case "Thigh.L": thighL = t; break;
                    case "Shin.L": shinL = t; break;
                    case "Foot.L": footL = t; break;
                    case "Thigh.R": thighR = t; break;
                    case "Shin.R": shinR = t; break;
                    case "Foot.R": footR = t; break;
                    case "Thumb.R": thumbR1 = t; break;
                    case "Thumb2.R": thumbR2 = t; break;
                    case "Index.R": indexR1 = t; break;
                    case "Index2.R": indexR2 = t; break;
                    case "Index3.R": indexR3 = t; break;
                    case "Middle.R": middleR1 = t; break;
                    case "Middle2.R": middleR2 = t; break;
                    case "Middle3.R": middleR3 = t; break;
                    case "Ring.R": ringR1 = t; break;
                    case "Ring2.R": ringR2 = t; break;
                    case "Ring3.R": ringR3 = t; break;
                    case "Pinky.R": pinkyR1 = t; break;
                    case "Pinky2.R": pinkyR2 = t; break;
                    case "Pinky3.R": pinkyR3 = t; break;
                    case "Thumb.L": thumbL1 = t; break;
                    case "Thumb2.L": thumbL2 = t; break;
                    case "Index.L": indexL1 = t; break;
                    case "Index2.L": indexL2 = t; break;
                    case "Index3.L": indexL3 = t; break;
                    case "Middle.L": middleL1 = t; break;
                    case "Middle2.L": middleL2 = t; break;
                    case "Middle3.L": middleL3 = t; break;
                    case "Ring.L": ringL1 = t; break;
                    case "Ring2.L": ringL2 = t; break;
                    case "Ring3.L": ringL3 = t; break;
                    case "Pinky.L": pinkyL1 = t; break;
                    case "Pinky2.L": pinkyL2 = t; break;
                    case "Pinky3.L": pinkyL3 = t; break;
                }
            }
        }

        private void ResolveSlatternBones()
        {
            slatternHeadNeckChain.Clear();
            slatternHeadBone = null;
            slatternTorsoDragBone = null;
            if (slatternTransform == null) return;

            // Articulated chain from chest through neck into skull/head so Gipsy grips Slattern's head cleanly
            string[] headNeckNames =
            {
                "UpperTorso",
                "Neck",
                "UpperHead"
            };
            var map = new Dictionary<string, Transform>();
            foreach (var t in slatternTransform.GetComponentsInChildren<Transform>(true))
            {
                map[t.name] = t;
                if (t.name == "UpperHead") slatternHeadBone = t;
                if (t.name == "LowerTorso") slatternTorsoDragBone = t;
            }
            foreach (var n in headNeckNames)
            {
                if (map.TryGetValue(n, out var tr))
                {
                    slatternHeadNeckChain.Add(tr);
                }
            }
        }

        private IEnumerator RunEndingCutsceneRoutine()
        {
            ResolveReferences();

            // Close any open menus or HUD before fading to black
            if (PacificRimAdminUI.IsOpen) PacificRimAdminUI.Close();
            if (JaegerHUD.Instance != null) JaegerHUD.Instance.SetCutsceneHidden(true);

            // Fade smoothly to black after Slattern's collapse
            if (letterboxCanvasGroup != null) letterboxCanvasGroup.alpha = 0f;
            if (fadeOverlayGroup != null)
            {
                fadeOverlayGroup.alpha = 0f;
                float elapsed = 0f;
                while (elapsed < fadeToStageDuration)
                {
                    elapsed += Time.deltaTime;
                    fadeOverlayGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeToStageDuration));
                    yield return null;
                }
                fadeOverlayGroup.alpha = 1f;
            }

            // Lock player, AI, camera follow, audio loops, and begin preloading The Anteverse in the background!
            PrepareEndingStage();
            StartAnteverseBackgroundPreload();

            // Stage initial positions & build Slattern's world-scale ragdoll BEFORE fade-in so
            // PhysX settles Slattern's 3,200-ton body & tails naturally onto the seabed behind Gipsy.
            Vector3 breachCenter = ResolveBreachTargetPoint();
            Vector3 dragStartJaeger = new Vector3(4.5f, SampleTerrainY(4.5f, 52.0f) + 2.977f, 52.0f);
            Vector3 dragEndJaeger = new Vector3(4.5f, SampleTerrainY(4.5f, 83.5f) + 2.977f, 83.5f);
            Quaternion faceBreachRot = Quaternion.LookRotation(new Vector3(breachCenter.x - dragStartJaeger.x, 0f, breachCenter.z - dragStartJaeger.z).normalized, Vector3.up);
            Vector3 startFwd = faceBreachRot * Vector3.forward;
            Vector3 startRight = faceBreachRot * Vector3.right;

            SetJaegerPose(dragStartJaeger, faceBreachRot);
            if (jaegerAnimator != null)
            {
                jaegerAnimator.speed = 0.78f;
                jaegerAnimator.SetFloat("Speed", 0.55f);
                jaegerAnimator.SetBool("IsRunning", false);
                jaegerAnimator.SetBool("InCombat", false);
                jaegerAnimator.SetBool("IsBlocking", false);
                jaegerAnimator.SetFloat("WalkDirection", 1f);
                jaegerAnimator.SetFloat("RunBlend", 0f);
                if (jaegerAnimator.layerCount > 1) jaegerAnimator.SetLayerWeight(1, 0f);
                jaegerAnimator.Play("Locomotion", 0, 0f);
                jaegerAnimator.Update(0f);
            }

            Vector3 initialGripTarget = ComputeDragGripTarget(dragStartJaeger, startFwd, startRight);
            gripWorldTarget = initialGripTarget;

            if (slatternTransform != null)
            {
                Vector3 slatInitPos = dragStartJaeger - startFwd * 28.0f + startRight * 1.9f;
                slatInitPos.y = SampleTerrainY(slatInitPos.x, slatInitPos.z) + 0.10f;
                Quaternion slatInitRot = faceBreachRot * Quaternion.Euler(0f, 180f, 0f);
                SetSlatternPose(slatInitPos, slatInitRot);

                if (slatternAnimator != null)
                {
                    slatternAnimator.Play("DeathLoop", 0, 0.25f);
                    slatternAnimator.Update(0f);
                    slatternAnimator.speed = 0f;
                    slatternAnimator.enabled = false;
                }

                UncurlSlatternTailsForDrag(slatInitRot);
                AlignSlatternSkullToInitialGrip(initialGripTarget);
                slatternRagdoll = new SlatternCutsceneRagdoll(slatternTransform, trenchTerrainCollider);
                slatternRagdoll.AttachGripToSkull(initialGripTarget);
                useRagdollPhysics = true;
                isHeavingRagdoll = false;
            }

            // Let PhysX settle Slattern's limp limbs, torso & triple tails onto the seabed while screen is black
            yield return new WaitForSeconds(0.28f);

            // Show letterbox bars and start eerie finale audio
            if (letterboxCanvasGroup != null) letterboxCanvasGroup.alpha = 1f;
            StartEndingAudio();
            PacificRimMusicManager.Instance?.PlayEndingSuspenseMusic(1.5f);

            // =========================================================================
            // BEAT 1 (0:00 – 0:06): Dragging Slattern Across the Abyss
            // =========================================================================
            applyLateUpdatePoses = true;
            lockCameraInLateUpdate = true;
            jaegerForwardLeanDeg = 29f;
            jaegerTorsoTwistDeg = 14f;
            jaegerCrouchWeight = 0.78f;
            jaegerCrouchDropMeters = 2.55f;
            jaegerHeadTiltDeg = 7f;
            jaegerDiveBlend = 0f;
            braceLeftArmWeight = 0.72f;
            gripRightFistWeight = 1f;
            ikGripHead = true;
            ikHeaveForward = false;
            headAttachedToGrip = false; // Handled directly by SlatternCutsceneRagdoll
            slatternDragDirection = startFwd;

            float dragTimer = 0f;
            float fadeInDur = 1.0f;
            float siltEmitAcc = 0f;
            int lastFootstepHalfCycle = -1;
            const float walkClipLength = 3.30f;
            const float dragWalkAnimSpeed = 0.78f;

            while (dragTimer < dragDuration)
            {
                if (WasSkipPressed())
                {
                    yield return StartCoroutine(SkipEndingCutsceneRoutine());
                    yield break;
                }

                float dt = Time.deltaTime;
                dragTimer += dt;
                float u = Mathf.Clamp01(dragTimer / dragDuration);

                // Fade in from black during first 1.0s
                if (fadeOverlayGroup != null)
                {
                    fadeOverlayGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dragTimer / fadeInDur));
                }

                // Stride-modulated progress so Gipsy surges during leg push and plants heavily on footfall
                float cycleProgress = (dragTimer * dragWalkAnimSpeed) / walkClipLength;
                float stepAngle = cycleProgress * Mathf.PI * 4f; // 2 steps per cycle
                float uWeighted = Mathf.Clamp01(u - Mathf.Sin(stepAngle) * 0.016f);

                Vector3 jPos = Vector3.Lerp(dragStartJaeger, dragEndJaeger, uWeighted);
                // Heavy vertical sink on foot plant (~0.35m) & strain rise on push
                float stepBob = -Mathf.Cos(stepAngle) * 0.24f;
                jPos.y = SampleTerrainY(jPos.x, jPos.z) + 2.977f + stepBob;
                SetJaegerPose(jPos, faceBreachRot);

                jaegerForwardLeanDeg = 26.5f + Mathf.Sin(stepAngle) * 2.2f;
                jaegerTorsoTwistDeg = 12f + Mathf.Sin(stepAngle * 0.5f) * 3.5f;

                Vector3 fwd = faceBreachRot * Vector3.forward;
                Vector3 right = faceBreachRot * Vector3.right;

                // Trigger seismic footstep thud + localized seabed silt burst on exact plant phase
                int halfCycle = Mathf.FloorToInt(cycleProgress * 2f + 0.84f);
                if (halfCycle != lastFootstepHalfCycle)
                {
                    lastFootstepHalfCycle = halfCycle;
                    Transform plantedFoot = (halfCycle % 2 == 0) ? footR : footL;
                    Vector3 footImpactPos = plantedFoot != null ? new Vector3(plantedFoot.position.x, SampleTerrainY(plantedFoot.position.x, plantedFoot.position.z), plantedFoot.position.z) : jPos;
                    PacificRimAudioManager.Instance?.PlayOceanFootstep(footImpactPos, 1.5f);
                    PacificRimAudioManager.Instance?.PlayArmorCreak(jPos + Vector3.up * 15f, 0.78f);
                    if (cameraShake != null) cameraShake.TriggerStompFeedback(0.62f);
                    EmitFootstepSiltBurst(footImpactPos);
                }

                // Emit seabed silt & dust plumes kicked up by dragging Slattern's grounded carcass & tails
                siltEmitAcc += dt;
                if (siltEmitAcc >= 0.04f)
                {
                    siltEmitAcc = 0f;
                    Vector3 torsoDragCenter = slatternRagdoll != null ? slatternRagdoll.TorsoWorldCenter : (jPos - fwd * 17f);
                    Vector3 tailDragCenter = slatternRagdoll != null ? slatternRagdoll.TailWorldCenter : (jPos - fwd * 27f);
                    EmitDragSiltAndBubbles(jPos, torsoDragCenter);
                    EmitDragSiltAndBubbles(jPos, tailDragCenter);
                }

                UpdateSparkEmitters();

                // Sweeping low-angle tracking shot: frames Gipsy head-to-toe leaning into the heavy haul
                // alongside Slattern's grounded carcass scraping through seabed silt, sweeping to reveal the Breach ahead
                float sweepU = Mathf.SmoothStep(0f, 1f, u);
                Vector3 camStartOffset = right * 28.0f + fwd * 10.5f + Vector3.up * 4.4f;
                Vector3 camEndOffset = right * 26.0f - fwd * 11.0f + Vector3.up * 6.8f;
                camTargetPos = jPos + Vector3.Lerp(camStartOffset, camEndOffset, sweepU);

                Vector3 lookAtDrag = jPos + Vector3.up * 9.5f - fwd * 5.0f;
                Vector3 lookAtAhead = Vector3.Lerp(jPos + Vector3.up * 9.5f + fwd * 8.0f, breachCenter + Vector3.up * 4.0f, 0.38f);
                Vector3 lookTarget = Vector3.Lerp(lookAtDrag, lookAtAhead, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.16f) / 0.84f)));
                camTargetRot = Quaternion.LookRotation((lookTarget - camTargetPos).normalized, Vector3.up);
                camTargetFov = Mathf.Lerp(54f, 55f, sweepU);

                yield return null;
            }

            if (fadeOverlayGroup != null) fadeOverlayGroup.alpha = 0f;

            // =========================================================================
            // BEAT 2 (0:06 – 0:11): Heave, Deep Ragdoll Throw, Genetic Lock & Sacrificial Plunge
            // =========================================================================
            Vector3 edgePos = dragEndJaeger;
            Vector3 edgeFwd = faceBreachRot * Vector3.forward;
            Vector3 edgeRight = faceBreachRot * Vector3.right;
            // Rim stand spot sits cleanly before the obsidian lip teeth (Z = 89.0m)
            Vector3 rimBracePos = new Vector3(edgePos.x, SampleTerrainY(edgePos.x, 85.8f) + 2.977f, 85.8f);
            Vector3 breachEntryTarget = new Vector3(breachCenter.x, -1.2f, breachCenter.z - 2.0f);

            // Switch Gipsy out of Walk into heavy planted CombatIdle for the heave & sacrificial leap
            if (jaegerAnimator != null)
            {
                jaegerAnimator.speed = 0.75f;
                jaegerAnimator.SetFloat("Speed", 0f);
                jaegerAnimator.SetBool("InCombat", true);
                jaegerAnimator.CrossFade("CombatIdle", 0.18f, 0, 0f);
            }

            // Wide three-quarter rear/side elevated angle looking down into the Breach chasm
            Vector3 beat2CamStart = new Vector3(-28f, 34f, 62f);
            Vector3 beat2CamEnd = new Vector3(-22f, 28f, 71f);
            Vector3 beat2LookStart = new Vector3(4.5f, 2.0f, 112f);
            Vector3 beat2LookEnd = new Vector3(5.2f, -6.5f, 117f);

            camTargetPos = beat2CamStart;
            camTargetRot = Quaternion.LookRotation((beat2LookStart - beat2CamStart).normalized, Vector3.up);
            camTargetFov = 54f;

            float b2Timer = 0f;
            float geneticLockTriggerTime = 1.50f;
            float portalCloseTriggerTime = 3.85f;
            bool releasedSlattern = false;
            bool triggeredGeneticLock = false;
            bool triggeredCrouchRumble = false;
            bool startedGipsyLeap = false;
            bool triggeredPortalClose = false;

            const float heaveEndTime = 0.85f;
            const float crouchStartTime = 1.85f;
            const float leapStartTime = 2.38f;
            const float leapDuration = 1.62f; // Upright jump & drop into Breach completes cleanly around ~4.00s, leaving ~1.0s for pulse + close shut!

            // Phases within 5.0s Beat 2:
            //   0.00 - 0.85s : Planted coil + violent hydraulic heave swinging Slattern's ragdoll over the rim
            //   0.85 - 1.85s : Slattern enters free-fall ragdoll & plunges DEEP into Breach -> Portal Pulses (stays OPEN & glowing!)
            //   1.85 - 2.38s : Gipsy coils down into a heavy 2,000-ton crouch at the rim overlooking the open portal
            //   2.38 - 4.00s : Gipsy jumps upright WITHOUT rotating his body (jumping IK + ballistic arc into the open glowing Breach)
            //   3.85 - 5.00s : Portal emits closing Pulse #2 and collapses/closes shut behind Gipsy!
            while (b2Timer < geneticLockAndLeapDuration)
            {
                if (WasSkipPressed())
                {
                    yield return StartCoroutine(SkipEndingCutsceneRoutine());
                    yield break;
                }

                float dt = Time.deltaTime;
                b2Timer += dt;
                float u = Mathf.Clamp01(b2Timer / geneticLockAndLeapDuration);

                // Smoothly pull camera slightly forward & track downward into the Breach glow as Gipsy plunges
                float camU = Mathf.SmoothStep(0f, 1f, u);
                camTargetPos = Vector3.Lerp(beat2CamStart, beat2CamEnd, camU);
                float plungeLookBias = b2Timer >= leapStartTime ? Mathf.SmoothStep(0f, 1f, (b2Timer - leapStartTime) / (geneticLockAndLeapDuration - leapStartTime)) : 0f;
                Vector3 lookPt = Vector3.Lerp(beat2LookStart, beat2LookEnd, Mathf.Clamp01(camU * 0.72f + plungeLookBias * 0.48f));
                camTargetRot = Quaternion.LookRotation((lookPt - camTargetPos).normalized, Vector3.up);
                camTargetFov = Mathf.Lerp(54f, 49f, camU);

                if (b2Timer < heaveEndTime)
                {
                    // Phase 2a: Two-stage wide right-flank hammer heave (never swings across Gipsy's body!)
                    float hu = Mathf.Clamp01(b2Timer / heaveEndTime);
                    float coilU = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(hu / 0.34f));
                    float driveU = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((hu - 0.30f) / 0.70f));

                    ikGripHead = hu < 0.28f;
                    ikHeaveForward = hu >= 0.26f;
                    heaveAnimWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((hu - 0.24f) / 0.30f));
                    heaveForwardT = driveU;
                    gripRightFistWeight = 1f;
                    braceLeftArmWeight = Mathf.Lerp(0.72f, 0.88f, hu);

                    jaegerCrouchWeight = Mathf.Lerp(0.65f, 0.90f, coilU) * (1f - driveU * 0.62f);
                    jaegerCrouchDropMeters = 2.35f;
                    jaegerForwardLeanDeg = Mathf.Lerp(26f, 14f, coilU) + Mathf.Lerp(0f, 10f, driveU);
                    jaegerTorsoTwistDeg = Mathf.Lerp(14f, 36f, coilU) - driveU * 38f; // +36° right windup -> -2° forward release (clears Gipsy's flank)
                    jaegerHeadTiltDeg = Mathf.Lerp(4f, 10f, driveU);

                    isHeavingRagdoll = true;
                    heaveAssistT = driveU;

                    // Swing Slattern's multi-ton torso UP and OUTWARD along Gipsy's right flank (strictly +edgeRight clearance!)
                    Vector3 heaveArcVel = edgeFwd * Mathf.Lerp(8f, 26f, driveU)
                        + Vector3.up * Mathf.Lerp(5f, 20f, Mathf.Sin(driveU * Mathf.PI * 0.85f))
                        + edgeRight * Mathf.Lerp(11f, 3.5f, driveU);
                    currentHeaveVelocityTarget = heaveArcVel;

                    Vector3 jP = Vector3.Lerp(edgePos, rimBracePos, Mathf.SmoothStep(0f, 1f, hu * 0.65f));
                    jP.y = SampleTerrainY(jP.x, jP.z) + 2.977f;
                    SetJaegerPose(jP, faceBreachRot);

                    if (hu >= 0.32f && hu <= 0.40f && cameraShake != null)
                    {
                        cameraShake.Shake(0.45f, 0.35f);
                    }
                }
                else
                {
                    if (!releasedSlattern)
                    {
                        releasedSlattern = true;
                        isHeavingRagdoll = false;
                        ikGripHead = false;
                        ikHeaveForward = true;
                        gripRightFistWeight = 0.25f;

                        // Release ragdoll grip and hurl Slattern slightly right of Gipsy straight into the 88m Breach opening (zero overlap!)
                        if (slatternRagdoll != null)
                        {
                            Vector3 slatternBreachTarget = new Vector3(breachEntryTarget.x + 4.8f, breachEntryTarget.y, breachEntryTarget.z + 2f);
                            Vector3 throwOrigin = slatternRagdoll.TorsoWorldCenter;
                            Vector3 toBreach = new Vector3(slatternBreachTarget.x - throwOrigin.x, 0f, slatternBreachTarget.z - throwOrigin.z);
                            Vector3 throwDir = toBreach.sqrMagnitude > 0.1f ? toBreach.normalized : edgeFwd;
                            Vector3 throwVel = throwDir * 27.0f + Vector3.up * 8.5f;
                            Vector3 tumbleTorque = edgeRight * 1.45f - edgeFwd * 0.35f;
                            slatternRagdoll.ReleaseAndThrowIntoBreach(throwVel, tumbleTorque, slatternBreachTarget);
                        }

                        PacificRimAudioManager.Instance?.PlayHydraulicServo(rimBracePos + Vector3.up * 12f, 1.0f);
                        PacificRimAudioManager.Instance?.PlayAttackWhoosh(rimBracePos + Vector3.up * 14f, true);
                        if (cameraShake != null) cameraShake.TriggerPlayerHitFeedback(isHeavy: false);
                    }

                    // Dynamic Genetic Lock impact trigger: fires when Slattern's ragdoll pierces the Breach membrane
                    // (with safety fallback at 1.65s so timeline stays rock-solid)
                    if (!triggeredGeneticLock)
                    {
                        bool ragdollPierced = slatternRagdoll != null && slatternRagdoll.HasPiercedBreachPlane(1.8f);
                        if (ragdollPierced || b2Timer >= 1.50f)
                        {
                            triggeredGeneticLock = true;
                            geneticLockTriggerTime = b2Timer;
                            if (breachMembraneMat != null && slatternRagdoll != null)
                            {
                                Vector3 hitPos = slatternRagdoll.HeadWorldPos;
                                Vector3 memOrigin = breachMembraneObj != null ? breachMembraneObj.transform.position : breachCenter;
                                float uHit = Mathf.Clamp01(0.5f + (hitPos.x - memOrigin.x) / 88f);
                                float vHit = Mathf.Clamp01(0.5f + (hitPos.z - memOrigin.z) / 56f);
                                breachMembraneMat.SetVector(IdHitUV, new Vector4(uHit, vHit, 0f, 0f));
                            }
                            TriggerGeneticLockSequence();
                        }
                    }

                    if (triggeredGeneticLock && !triggeredPortalClose)
                    {
                        UpdateGeneticLockMembrane(Mathf.Max(0f, b2Timer - geneticLockTriggerTime), -1f);
                    }
                    else if (triggeredPortalClose)
                    {
                        UpdateGeneticLockMembrane(Mathf.Max(0f, b2Timer - geneticLockTriggerTime), Mathf.Max(0f, b2Timer - portalCloseTriggerTime));
                    }

                    if (b2Timer < crouchStartTime)
                    {
                        // Phase 2c: Follow-through settle into heavy braced watch on the rim (no clipping into obsidian teeth!)
                        float settleT = Mathf.Clamp01((b2Timer - heaveEndTime) / (crouchStartTime - heaveEndTime));
                        float smoothSettle = Mathf.SmoothStep(0f, 1f, settleT);
                        heaveAnimWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((b2Timer - heaveEndTime) / 0.52f));
                        ikHeaveForward = heaveAnimWeight > 0.02f;
                        braceLeftArmWeight = Mathf.Lerp(0.85f, 0.35f, smoothSettle);
                        gripRightFistWeight = Mathf.Lerp(0.25f, 0.5f, smoothSettle);

                        jaegerCrouchWeight = Mathf.Lerp(0.32f, 0.25f, smoothSettle);
                        jaegerCrouchDropMeters = 2.2f;
                        jaegerForwardLeanDeg = Mathf.Lerp(26f, 15f, smoothSettle);
                        jaegerTorsoTwistDeg = Mathf.Lerp(-24f, 0f, smoothSettle);
                        jaegerHeadTiltDeg = Mathf.Lerp(10f, 18f, smoothSettle);

                        Vector3 watchPos = Vector3.Lerp(edgePos + edgeFwd * 1.6f, rimBracePos, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(settleT * 1.4f)));
                        watchPos.y = SampleTerrainY(watchPos.x, watchPos.z) + 2.977f;
                        SetJaegerPose(watchPos, faceBreachRot);
                    }
                    else if (b2Timer < leapStartTime)
                    {
                        // Phase 2d: Heavy 2,000-ton Anticipation Crouch on the rim peering down into the tearing abyss
                        if (!triggeredCrouchRumble)
                        {
                            triggeredCrouchRumble = true;
                            PacificRimAudioManager.Instance?.PlayHydraulicServo(rimBracePos + Vector3.up * 10f, 0.9f);
                            PacificRimAudioManager.Instance?.PlayArmorCreak(rimBracePos + Vector3.up * 12f, 0.85f);
                            if (cameraShake != null) cameraShake.TriggerStompFeedback(0.50f);
                            EmitFootstepSiltBurst(rimBracePos);
                        }

                        float cu = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((b2Timer - crouchStartTime) / (leapStartTime - crouchStartTime)));
                        ikHeaveForward = false;
                        heaveAnimWeight = 0f;
                        braceLeftArmWeight = Mathf.Lerp(0.35f, 0.85f, cu);
                        gripRightFistWeight = 0.85f;
                        jaegerCrouchWeight = Mathf.Lerp(0.25f, 1.0f, cu);
                        jaegerCrouchDropMeters = 2.65f;
                        jaegerForwardLeanDeg = Mathf.Lerp(15f, 29f, cu);
                        jaegerTorsoTwistDeg = 0f;
                        jaegerHeadTiltDeg = Mathf.Lerp(18f, 22f, cu);

                        SetJaegerPose(rimBracePos, faceBreachRot);
                    }
                    else
                    {
                        // Phase 2e: Gipsy jumps WITHOUT rotating his body (pure jumping IK + upright arc) -> Portal Pulses then Closes Shut
                        if (!startedGipsyLeap)
                        {
                            startedGipsyLeap = true;
                            ikHeaveForward = false;
                            braceLeftArmWeight = 0f;
                            gripRightFistWeight = 1f;

                            PacificRimAudioManager.Instance?.PlayOceanFootstep(rimBracePos, 1.85f);
                            PacificRimAudioManager.Instance?.PlayHydraulicServo(rimBracePos + Vector3.up * 11f, 1.0f);
                            if (cameraShake != null) cameraShake.TriggerStompFeedback(0.85f);
                            EmitFootstepSiltBurst(rimBracePos);
                        }

                        float leapElapsed = b2Timer - leapStartTime;
                        float leapU = Mathf.Clamp01(leapElapsed / leapDuration);

                        jaegerPlungeTime = leapElapsed;
                        jaegerDiveBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(leapU * 4.2f));
                        jaegerCrouchWeight = Mathf.Lerp(1.0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(leapU * 4.8f)));
                        jaegerForwardLeanDeg = Mathf.Lerp(29f, 8f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(leapU * 3.2f)));
                        jaegerTorsoTwistDeg = 0f;
                        jaegerHeadTiltDeg = Mathf.Lerp(22f, 14f, leapU);

                        // Heavy upright vault: pushes UP & FORWARD cleanly over rim teeth (Z=89), then drops feet-first into the glowing Breach center
                        Vector3 jumpApexPos = new Vector3(
                            Mathf.Lerp(rimBracePos.x, breachEntryTarget.x, 0.52f),
                            rimBracePos.y + 5.8f,
                            101.5f);
                        Vector3 jumpEndPos = new Vector3(breachEntryTarget.x, -26.5f, breachEntryTarget.z - 2.0f);

                        float pathT = Mathf.Pow(leapU, 1.12f);
                        Vector3 curRootPos = QuadraticBezier(rimBracePos, jumpApexPos, jumpEndPos, pathT);
                        SetJaegerPose(curRootPos, faceBreachRot); // Never rotate body into the ground!

                        if (leapU < 0.92f && dragBubblesPs != null && Random.value < dt * 28f)
                        {
                            Vector3 chestApprox = curRootPos + Vector3.up * 14f;
                            EmitDragSiltAndBubbles(chestApprox, chestApprox);
                        }

                        // Trigger Portal Pulse #2 + Collapse Shut right as Gipsy vanishes beneath the glowing portal surface (~3.85s)
                        if (!triggeredPortalClose && b2Timer >= 3.85f)
                        {
                            triggeredPortalClose = true;
                            portalCloseTriggerTime = b2Timer;
                            if (breachMembraneMat != null)
                            {
                                breachMembraneMat.SetVector(IdHitUV, new Vector4(0.5f, 0.5f, 0f, 0f));
                            }
                            TriggerGeneticLockSequence();
                            PlayOneShot2D("Magic Spell_Simple Swoosh_6", 0.9f, 0.72f);
                        }

                        // Brief dark abyssal cut fade only during the final 0.22s AFTER the portal has visibly pulsed & snapped shut
                        if (b2Timer >= 4.78f && fadeOverlayImage != null && fadeOverlayGroup != null)
                        {
                            fadeOverlayImage.color = new Color(0.02f, 0.04f, 0.10f, 1f);
                            fadeOverlayGroup.alpha = Mathf.SmoothStep(0f, 0.95f, Mathf.Clamp01((b2Timer - 4.78f) / 0.22f));
                        }
                    }
                }

                UpdateSparkEmitters();
                yield return null;
            }

            // =========================================================================
            // 0:11 Hard Cut -> Activate preloaded The Anteverse scene!
            // =========================================================================
            HandOffToAnteverseScene();
        }

        private void FixedUpdate()
        {
            if (!isCutsceneRunning || !useRagdollPhysics || slatternRagdoll == null) return;

            if (jaegerTransform != null)
            {
                bool shieldActive = jaegerDiveBlend < 0.05f;
                float shieldRadius = isHeavingRagdoll ? 4.25f : (ikGripHead ? 2.85f : 4.05f);
                slatternRagdoll.UpdateJaegerShield(jaegerTransform.position - slatternDragDirection * 0.35f, shieldRadius, shieldActive);
            }

            if (ikGripHead)
            {
                slatternRagdoll.MoveGripAnchor(gripWorldTarget);
                slatternRagdoll.ApplyDragDownwardWeight(18f);
            }
            else if (isHeavingRagdoll)
            {
                Vector3 fwd = slatternDragDirection;
                Vector3 rgt = Vector3.Cross(Vector3.up, fwd).normalized;
                Vector3 heaveGripTarget = jaegerTransform != null
                    ? jaegerTransform.position + fwd * Mathf.Lerp(-2.2f, 8.2f, heaveAssistT) + rgt * (5.2f + Mathf.Sin(heaveAssistT * Mathf.PI) * 2.2f) + Vector3.up * Mathf.Lerp(9.8f, 20.5f, heaveAssistT)
                    : gripWorldTarget;
                gripWorldTarget = heaveGripTarget;
                slatternRagdoll.MoveGripAnchor(heaveGripTarget);
                if (heaveAssistT > 0.02f)
                {
                    slatternRagdoll.ApplyHeaveAssist(currentHeaveVelocityTarget, heaveAssistT);
                }
            }
            else
            {
                slatternRagdoll.StepFreeFallIntoBreach(Time.fixedDeltaTime);
            }
        }

        private Vector3 ComputeDragGripTarget(Vector3 jaegerPos, Vector3 fwd, Vector3 rgt)
        {
            return jaegerPos + rgt * 2.55f - fwd * 1.9f + Vector3.up * 8.6f;
        }

        private void UncurlSlatternTailsForDrag(Quaternion slatRot)
        {
            if (slatternTransform == null) return;
            Transform b025 = null, tC = null, tL = null, tR = null;
            foreach (var t in slatternTransform.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Bone.025": b025 = t; break;
                    case "TailBone": tC = t; break;
                    case "TailBone.L": tL = t; break;
                    case "TailBone.R": tR = t; break;
                }
            }
            // Note: slatRot has 180 deg yaw so Slattern faces +Z (Gipsy). Tail trail direction behind Slattern is -Z (-(slatRot * forward)? Wait: at rot=0 head is -Z, so at rot=180 head is +Z and behind hips is -Z = slatRot * Vector3.forward!)
            Vector3 behindHipsDir = slatRot * Vector3.forward;
            Vector3 rightDir = slatRot * Vector3.right;
            Vector3 tailTrailDir = (behindHipsDir + Vector3.down * 0.10f).normalized;

            if (b025 != null && tC != null)
            {
                Vector3 curTailFwd = tC.position - b025.position;
                if (curTailFwd.sqrMagnitude > 0.01f)
                {
                    Quaternion swingBack = Quaternion.FromToRotation(curTailFwd.normalized, (behindHipsDir + Vector3.down * 0.35f).normalized);
                    b025.rotation = Quaternion.Slerp(Quaternion.identity, swingBack, 0.72f) * b025.rotation;
                }
            }
            if (tC != null)
            {
                Transform tCNext = tC.Find("TailBone.002");
                if (tCNext != null)
                {
                    Vector3 curDir = tCNext.position - tC.position;
                    if (curDir.sqrMagnitude > 0.01f)
                        tC.rotation = Quaternion.FromToRotation(curDir.normalized, tailTrailDir) * tC.rotation;
                }
            }
            if (tL != null)
            {
                Transform tLNext = tL.Find("TailBone.L.001");
                if (tLNext != null)
                {
                    Vector3 curDir = tLNext.position - tL.position;
                    if (curDir.sqrMagnitude > 0.01f)
                        tL.rotation = Quaternion.FromToRotation(curDir.normalized, (tailTrailDir - rightDir * 0.38f).normalized) * tL.rotation;
                }
            }
            if (tR != null)
            {
                Transform tRNext = tR.Find("TailBone.R.001");
                if (tRNext != null)
                {
                    Vector3 curDir = tRNext.position - tR.position;
                    if (curDir.sqrMagnitude > 0.01f)
                        tR.rotation = Quaternion.FromToRotation(curDir.normalized, (tailTrailDir + rightDir * 0.38f).normalized) * tR.rotation;
                }
            }
        }

        private void AlignSlatternSkullToInitialGrip(Vector3 targetSkullWorldPos)
        {
            if (slatternTransform == null) return;
            Transform upperHead = slatternHeadBone;
            Transform upperHeadEnd = upperHead != null ? upperHead.Find("UpperHead_end") : null;
            Vector3 curSkull = upperHead != null
                ? (upperHeadEnd != null ? Vector3.Lerp(upperHead.position, upperHeadEnd.position, 0.42f) : upperHead.position)
                : slatternTransform.position;
            Vector3 horizShift = new Vector3(targetSkullWorldPos.x - curSkull.x, 0f, targetSkullWorldPos.z - curSkull.z);
            slatternTransform.position += horizShift;
            AlignHeadNeckChainToTarget(targetSkullWorldPos);
        }

        private void EmitFootstepSiltBurst(Vector3 footPos)
        {
            if (dragSiltPs != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    float ang = (i / 10f) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                    Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    dragSiltPs.Emit(new ParticleSystem.EmitParams
                    {
                        position = footPos + radial * Random.Range(1.0f, 3.2f) + Vector3.up * 0.4f,
                        velocity = radial * Random.Range(2.4f, 5.2f) + Vector3.up * Random.Range(0.8f, 2.4f),
                        startSize = Random.Range(4.2f, 8.8f),
                        startLifetime = Random.Range(1.8f, 3.4f),
                        startColor = new Color(0.16f, 0.18f, 0.24f, 0.40f)
                    }, 1);
                }
            }
            if (dragBubblesPs != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    dragBubblesPs.Emit(new ParticleSystem.EmitParams
                    {
                        position = footPos + Random.insideUnitSphere * 2.2f + Vector3.up * 0.8f,
                        velocity = Vector3.up * Random.Range(3.2f, 6.8f) + Random.insideUnitSphere * 1.1f,
                        startSize = Random.Range(0.10f, 0.28f),
                        startLifetime = Random.Range(1.2f, 2.4f),
                        startColor = new Color(0.78f, 0.92f, 1.0f, 0.70f)
                    }, 1);
                }
            }
        }

        private void PrepareEndingStage()
        {
            if (jaegerController != null)
            {
                jaegerController.isCutsceneActive = true;
                jaegerController.enabled = false;
            }
            if (jaegerRigidbody != null)
            {
                jaegerRigidbody.isKinematic = true;
                jaegerRigidbody.useGravity = false;
            }

            if (kaijuAI != null)
            {
                kaijuAI.isCutsceneMode = true;
                kaijuAI.enabled = false;
            }
            if (slatternRigidbody != null)
            {
                slatternRigidbody.isKinematic = true;
                slatternRigidbody.useGravity = false;
            }
            if (slatternBio != null)
            {
                slatternBio.minIntensity = 0.04f;
                slatternBio.maxIntensity = 0.06f;
                slatternBio.maxLightIntensity = 0f;
            }

            // Disable canyon walls and camera follow
            var camFollow = JaegerCameraFollow.Instance ?? (mainCamera != null ? mainCamera.GetComponent<JaegerCameraFollow>() : null);
            if (camFollow != null) camFollow.enabled = false;

            foreach (var col in FindObjectsByType<Collider>(FindObjectsInactive.Include))
            {
                if (col == null || col is TerrainCollider) continue;
                col.enabled = false;
            }

            // Override UnderwaterEnvironmentController so abyss turns pitch-black except headlights, orange turbine & Breach
            if (underwaterEnv != null)
            {
                underwaterEnv.enabled = false;
            }

            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.008f, 0.009f, 0.024f, 1f);
            RenderSettings.fogDensity = 0.0135f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.012f, 0.015f, 0.030f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.008f, 0.010f, 0.020f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.004f, 0.005f, 0.010f, 1f);

            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.color = new Color(0.08f, 0.14f, 0.28f, 1f);
                mainDirectionalLight.intensity = 0.03f;
                mainDirectionalLight.shadowStrength = 0.25f;
            }

            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = RenderSettings.fogColor;
                if (cameraLowPass != null) cameraLowPass.cutoffFrequency = 2200f;
            }

            // Dim hydrothermal vents & blood pools so only Gipsy's headlights, turbine & Breach dominate
            DimSceneEnvironmentLights();

            // Setup Gipsy battle-damaged scorched armor, glowing chest turbine, strong headlights & joint sparks
            SetupBattleDamagedGipsy();

            // Recolor The Breach to magenta-blue and spawn the shimmering Bio-Membrane
            SetupBreachMagentaBlueAndMembrane();

            // Create seabed silt & bubble particle systems for dragging
            EnsureDragParticles();

            // Suppress gameplay storm/turbine loops so ending cutscene controls audio cleanly
            PacificRimAudioManager.Instance?.SetEndingCutsceneLoopsSuppressed(true);
        }

        private void DimSceneEnvironmentLights()
        {
            var ventsRoot = GameObject.Find("Underwater_HydrothermalVents");
            if (ventsRoot != null)
            {
                foreach (var l in ventsRoot.GetComponentsInChildren<Light>(true)) l.enabled = false;
                foreach (var ps in ventsRoot.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (ps.name.Contains("Ember")) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            var poolsRoot = GameObject.Find("Underwater_KaijuBloodPools");
            if (poolsRoot != null)
            {
                foreach (var l in poolsRoot.GetComponentsInChildren<Light>(true)) l.intensity *= 0.12f;
            }
            var fissuresRoot = GameObject.Find("Mariana_CrustalFissures");
            if (fissuresRoot != null)
            {
                fissuresRoot.SetActive(false);
            }
        }

        private void SetupBattleDamagedGipsy()
        {
            if (jaegerTransform == null) return;

            foreach (var smr in jaegerTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMaterial == null) continue;
                if (smr.name == "Visor")
                {
                    visorMatInstance = new Material(smr.sharedMaterial) { name = "Visor_EndingCutscene" };
                    visorMatInstance.EnableKeyword("_EMISSION");
                    visorMatInstance.SetColor(IdBaseColor, new Color(1.0f, 0.60f, 0.12f, 1f));
                    visorMatInstance.SetColor(IdEmissionColor, new Color(1.0f, 0.50f, 0.05f, 1f) * 3.4f);
                    smr.sharedMaterial = visorMatInstance;
                }
                else if (smr.name.Contains("Spinning"))
                {
                    turbineMatInstance = new Material(smr.sharedMaterial) { name = "Turbine_EndingCutscene" };
                    turbineMatInstance.EnableKeyword("_EMISSION");
                    turbineMatInstance.SetColor(IdEmissionColor, new Color(1.0f, 0.45f, 0.03f, 1f) * 5.2f);
                    smr.sharedMaterial = turbineMatInstance;
                }
                else
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

            // Chest orange turbine glow light & head visor glow light
            if (chestBone != null && coreGlowLight == null)
            {
                var cg = new GameObject("Ending_CoreGlowLight");
                coreGlowLight = cg.AddComponent<Light>();
                coreGlowLight.type = LightType.Point;
                coreGlowLight.color = new Color(1.0f, 0.46f, 0.04f, 1f);
                coreGlowLight.range = 42f;
                coreGlowLight.intensity = 52f;
                coreGlowLight.shadows = LightShadows.None;
            }
            if (headBone != null && visorGlowLight == null)
            {
                var vg = new GameObject("Ending_VisorGlowLight");
                visorGlowLight = vg.AddComponent<Light>();
                visorGlowLight.type = LightType.Point;
                visorGlowLight.color = new Color(1.0f, 0.58f, 0.12f, 1f);
                visorGlowLight.range = 20f;
                visorGlowLight.intensity = 16f;
                visorGlowLight.shadows = LightShadows.None;
            }

            // Ensure Gipsy's headlights & volumetric beams slice strongly through the dark trench
            foreach (var spot in jaegerTransform.GetComponentsInChildren<Light>(true))
            {
                if (spot != null && spot.type == LightType.Spot)
                {
                    spot.enabled = true;
                    spot.intensity = spot.gameObject.name.Contains("Head") ? 5500f : 3800f;
                    spot.range = 240f;
                    spot.color = new Color(0.88f, 0.96f, 1.0f, 1f);
                }
            }
            if (visualSockets != null)
            {
                visualSockets.SetVolumetricConesVisible(true);
                visualSockets.SetCutsceneBeamMultiplier(0.45f);
            }

            // Joint spark emitters tracking key damaged joints at 1:1 world scale
            jointSparkSystems.Clear();
            jointSparkAnchors.Clear();
            Transform[] sparkJoints = { upperArmL, chestBone, thighR, headBone };
            foreach (var anchor in sparkJoints)
            {
                if (anchor == null) continue;
                var ps = CreateSparkParticleSystem($"EndingJointSparks_{anchor.name}");
                jointSparkSystems.Add(ps);
                jointSparkAnchors.Add(anchor);
            }

            var flashGo = new GameObject("Ending_JointSparkLight");
            jointSparkFlashLight = flashGo.AddComponent<Light>();
            jointSparkFlashLight.type = LightType.Point;
            jointSparkFlashLight.color = new Color(0.75f, 0.88f, 1.0f, 1f);
            jointSparkFlashLight.range = 22f;
            jointSparkFlashLight.intensity = 0f;
            jointSparkFlashLight.shadows = LightShadows.None;
        }

        private Material FindOrCloneArmorMat(Material src)
        {
            string targetName = src.name + "_ScorchedBattleDamage";
            for (int i = 0; i < armorMatInstances.Count; i++)
            {
                if (armorMatInstances[i].name == targetName) return armorMatInstances[i];
            }
            var m = new Material(src) { name = targetName };
            // Scorched charred armor look
            if (m.HasProperty(IdBaseColor)) m.SetColor(IdBaseColor, new Color(0.54f, 0.56f, 0.62f, 1f));
            if (m.HasProperty(IdSmoothness)) m.SetFloat(IdSmoothness, 0.22f);
            if (m.HasProperty(IdWetSmoothness)) m.SetFloat(IdWetSmoothness, 0.42f);
            if (m.HasProperty(IdEdgeWearStrength)) m.SetFloat(IdEdgeWearStrength, 1.75f);
            if (m.HasProperty(IdNormalStrength)) m.SetFloat(IdNormalStrength, 1.65f);
            armorMatInstances.Add(m);
            return m;
        }

        private ParticleSystem CreateSparkParticleSystem(string sysName)
        {
            var go = new GameObject(sysName);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 240;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.72f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 16f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.gravityModifier = 0.65f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.0f, 0.78f, 0.28f, 1f),
                new Color(0.55f, 0.88f, 1f, 1f));

            var emission = ps.emission;
            emission.enabled = false;

            Texture2D softTex = CreateSoftGlowParticleTexture();
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.06f;
            rend.lengthScale = 2.0f;
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh) { name = sysName + "_Mat" };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", softTex);
            mat.mainTexture = softTex;
            if (mat.HasProperty(IdBaseColor)) mat.SetColor(IdBaseColor, new Color(2.4f, 1.8f, 1.0f, 1f));
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.One);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ADD");
            mat.renderQueue = (int)RenderQueue.Transparent;
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = ShadowCastingMode.Off;

            ps.Play();
            return ps;
        }

        private void UpdateSparkEmitters()
        {
            if (coreGlowLight != null && chestBone != null)
            {
                coreGlowLight.transform.position = chestBone.position + (jaegerTransform != null ? jaegerTransform.forward * 1.4f : Vector3.forward);
                coreGlowLight.intensity = 48f + Mathf.Sin(Time.time * 18f) * 4f;
            }
            if (visorGlowLight != null && headBone != null)
            {
                visorGlowLight.transform.position = headBone.position + (jaegerTransform != null ? jaegerTransform.forward * 1.0f : Vector3.forward);
            }

            for (int i = 0; i < jointSparkSystems.Count; i++)
            {
                if (jointSparkSystems[i] != null && i < jointSparkAnchors.Count && jointSparkAnchors[i] != null)
                {
                    jointSparkSystems[i].transform.position = jointSparkAnchors[i].position;
                }
            }

            if (jointSparkFlashLight != null)
            {
                jointSparkFlashLight.intensity = Mathf.MoveTowards(jointSparkFlashLight.intensity, 0f, Time.deltaTime * 180f);
            }

            if (Time.time >= nextSparkBurstTime && jointSparkSystems.Count > 0)
            {
                nextSparkBurstTime = Time.time + Random.Range(0.18f, 0.55f);
                int idx = Random.Range(0, jointSparkSystems.Count);
                var ps = jointSparkSystems[idx];
                if (ps != null)
                {
                    int count = Random.Range(10, 24);
                    for (int k = 0; k < count; k++)
                    {
                        var ep = new ParticleSystem.EmitParams
                        {
                            position = ps.transform.position + Random.insideUnitSphere * 0.45f,
                            velocity = Random.onUnitSphere * Random.Range(5f, 15f) + Vector3.up * Random.Range(1f, 6f),
                            startSize = Random.Range(0.08f, 0.24f),
                            startLifetime = Random.Range(0.25f, 0.65f),
                            startColor = k % 3 == 0 ? new Color(0.55f, 0.90f, 1f, 1f) : new Color(1.0f, 0.72f, 0.22f, 1f)
                        };
                        ps.Emit(ep, 1);
                    }
                    if (jointSparkFlashLight != null)
                    {
                        jointSparkFlashLight.transform.position = ps.transform.position;
                        jointSparkFlashLight.intensity = Random.Range(24f, 48f);
                    }
                    if (Random.value < 0.45f)
                    {
                        PlayOneShot2D("Magic Spell_Electricity Spell_1", 0.22f, Random.Range(1.25f, 1.55f));
                    }
                }
            }
        }

        private void SetupBreachMagentaBlueAndMembrane()
        {
            if (breachTransform == null) return;

            runtimeBreachMaterials.Clear();
            breachMatBaseColors.Clear();
            breachMatEmColors.Clear();
            breachGodRayTransforms.Clear();
            breachLights.Clear();
            breachLightBaseIntensities.Clear();

            Color magentaHdr = new Color(1.65f, 0.22f, 3.4f, 1f);
            Color blueHdr = new Color(0.14f, 1.05f, 3.2f, 1f);

            int breachIdx = 0;
            foreach (var r in breachTransform.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.sharedMaterial == null) continue;
                r.SetPropertyBlock(null); // Clear UnderwaterEnvironmentController's cached orange/yellow PropertyBlock overrides
                string n = r.gameObject.name;

                if (n.Contains("Tooth"))
                {
                    // Keep Breach ObsidianTooth / SeafloorLipTooth as dark basalt rock framing the chasm
                    continue;
                }
                else if (n.Contains("GodRay"))
                {
                    var m = new Material(r.sharedMaterial) { name = n + "_MagentaBlueRay" };
                    bool even = (breachIdx++ % 2) == 0;
                    Color rayCol = even ? new Color(0.55f, 0.16f, 0.96f, 0.22f) : new Color(0.10f, 0.56f, 0.96f, 0.20f);
                    if (m.HasProperty(IdColor)) m.SetColor(IdColor, rayCol);
                    if (m.HasProperty(IdBaseColor)) m.SetColor(IdBaseColor, rayCol);
                    if (m.HasProperty(IdIntensity)) m.SetFloat(IdIntensity, 1.15f);
                    r.sharedMaterial = m;
                    runtimeBreachMaterials.Add(m);
                    breachMatBaseColors.Add(rayCol);
                    breachMatEmColors.Add(Color.black);
                    breachGodRayTransforms.Add(r.transform);
                }
                else
                {
                    var mats = r.sharedMaterials;
                    bool modified = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        var m = new Material(mats[i]) { name = n + "_MagentaBlueRuntime_" + i };
                        bool isCore = n.Contains("InnerCore") || n.Contains("Core") || n.Contains("Energy");
                        bool even = (breachIdx++ % 2) == 0;
                        Color c = isCore
                            ? new Color(0.28f, 0.14f, 0.85f, 1f)
                            : (even ? new Color(0.32f, 0.10f, 0.65f, 1f) : new Color(0.10f, 0.26f, 0.72f, 1f));
                        Color em = isCore
                            ? blueHdr * 1.1f
                            : (even ? magentaHdr * 0.75f : blueHdr * 0.65f);
                        if (m.HasProperty(IdBaseColor)) m.SetColor(IdBaseColor, c);
                        if (m.HasProperty(IdColor)) m.SetColor(IdColor, c);
                        if (m.HasProperty(IdEmissionColor))
                        {
                            m.EnableKeyword("_EMISSION");
                            m.SetColor(IdEmissionColor, em);
                        }
                        mats[i] = m;
                        runtimeBreachMaterials.Add(m);
                        breachMatBaseColors.Add(c);
                        breachMatEmColors.Add(em);
                        modified = true;
                    }
                    if (modified) r.sharedMaterials = mats;
                }
            }

            foreach (var l in breachTransform.GetComponentsInChildren<Light>(true))
            {
                if (l == null) continue;
                l.color = l.gameObject.name.Contains("Rim") ? new Color(0.68f, 0.22f, 1.0f, 1f) : new Color(0.18f, 0.52f, 1.0f, 1f);
                l.intensity *= 0.85f;
                breachLights.Add(l);
                breachLightBaseIntensities.Add(l.intensity);
            }

            // Build shimmering violet-and-blue Breach Bio-Membrane spanning the chasm opening
            Vector3 center = ResolveBreachTargetPoint();
            breachMembraneObj = new GameObject("Breach_GeneticLockMembrane");
            breachMembraneObj.transform.position = new Vector3(center.x, 0.85f, center.z);
            breachMembraneObj.transform.rotation = Quaternion.identity;

            var mf = breachMembraneObj.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildSubdividedPlaneMesh(88f, 56f, 24, 24);
            var mr = breachMembraneObj.AddComponent<MeshRenderer>();

            Shader memShader = Shader.Find("PacificRim/BreachMembrane") ?? Shader.Find("Universal Render Pipeline/Unlit");
            breachMembraneMat = new Material(memShader) { name = "BreachGeneticMembrane_Runtime" };
            breachMembraneMat.SetColor("_ColorA", new Color(0.58f, 0.14f, 1.35f, 0.9f));
            breachMembraneMat.SetColor("_ColorB", new Color(0.10f, 0.72f, 1.65f, 0.9f));
            breachMembraneMat.SetColor("_TearEdgeColor", new Color(2.0f, 1.7f, 2.9f, 1f));
            breachMembraneMat.SetFloat("_Iridescence", 0.22f);
            breachMembraneMat.SetFloat(IdRippleProgress, 0f);
            breachMembraneMat.SetVector(IdHitUV, new Vector4(0.5f, 0.42f, 0f, 0f));
            breachMembraneMat.SetFloat(IdTearAmount, 0f);
            breachMembraneMat.SetFloat(IdCloseAmount, 0f);
            breachMembraneMat.SetFloat(IdFlareIntensity, 1.45f);
            mr.sharedMaterial = breachMembraneMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            var flareLightGo = new GameObject("Breach_GeneticFlareLight");
            flareLightGo.transform.position = new Vector3(center.x, 6f, center.z - 4f);
            breachFlareLight = flareLightGo.AddComponent<Light>();
            breachFlareLight.type = LightType.Point;
            breachFlareLight.color = new Color(0.58f, 0.38f, 1.0f, 1f);
            breachFlareLight.range = 190f;
            breachFlareLight.intensity = 950f;
            breachFlareLight.shadows = LightShadows.None;
        }

        private void TriggerGeneticLockSequence()
        {
            PlayOneShot2D("Magic Spell_Electricity Spell_1", 0.95f, 0.85f);
            PlayOneShot2D("EmpChargeUp", 0.90f, 1.10f);
            PlayOneShot2D("EmpDischargeBlast", 0.85f, 0.92f);
            if (cameraShake != null) cameraShake.TriggerPlayerHitFeedback(isHeavy: true);
        }

        private void UpdateGeneticLockMembrane(float lockElapsed, float closeElapsed)
        {
            float closePhase = closeElapsed >= 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((closeElapsed - 0.24f) / 0.62f)) : 0f;
            float envFade = 1f - closePhase;

            if (breachMembraneMat != null)
            {
                if (closeElapsed < 0f)
                {
                    // Pulse #1 (Slattern enters): strong shockwave rings + HDR flare pulse, portal stays WIDE OPEN & GLOWING!
                    float rippleT = Mathf.Clamp01(lockElapsed / 1.15f);
                    float pulseSin = Mathf.Sin(Mathf.Clamp01(lockElapsed / 1.15f) * Mathf.PI);
                    float flarePulse = 1.85f + pulseSin * 4.2f;

                    breachMembraneMat.SetFloat(IdRippleProgress, rippleT * 1.70f);
                    breachMembraneMat.SetFloat(IdTearAmount, 0f);
                    breachMembraneMat.SetFloat(IdCloseAmount, 0f);
                    breachMembraneMat.SetFloat(IdFlareIntensity, flarePulse);
                }
                else
                {
                    // Pulse #2 + Closes Shut (after Gipsy plunges in): bright cyan-white pulse then iris-collapse shut to 0
                    float pulseT = Mathf.Clamp01(closeElapsed / 0.36f);
                    float pulseSin = Mathf.Sin(pulseT * Mathf.PI);
                    float flarePulse = Mathf.Lerp(2.1f + pulseSin * 5.2f, 0.4f, Mathf.Pow(closePhase, 1.5f));

                    breachMembraneMat.SetFloat(IdRippleProgress, pulseT * 1.60f);
                    breachMembraneMat.SetFloat(IdTearAmount, 0f);
                    breachMembraneMat.SetFloat(IdCloseAmount, closePhase);
                    breachMembraneMat.SetFloat(IdFlareIntensity, flarePulse);
                }
            }

            if (breachFlareLight != null)
            {
                if (closeElapsed < 0f)
                {
                    float spike = Mathf.Sin(Mathf.Clamp01(lockElapsed / 1.25f) * Mathf.PI);
                    breachFlareLight.intensity = 1250f + spike * 2600f;
                    breachFlareLight.color = Color.Lerp(new Color(0.58f, 0.38f, 1.0f), new Color(0.68f, 0.85f, 1.0f), spike);
                }
                else
                {
                    float spike = Mathf.Sin(Mathf.Clamp01(closeElapsed / 0.38f) * Mathf.PI);
                    breachFlareLight.intensity = (1350f + spike * 3200f) * envFade;
                    breachFlareLight.color = Color.Lerp(new Color(0.58f, 0.38f, 1.0f), new Color(0.75f, 0.90f, 1.0f), spike);
                }
            }

            if (closeElapsed >= 0f)
            {
                for (int i = 0; i < breachLights.Count && i < breachLightBaseIntensities.Count; i++)
                {
                    if (breachLights[i] != null)
                        breachLights[i].intensity = breachLightBaseIntensities[i] * envFade;
                }
                for (int i = 0; i < runtimeBreachMaterials.Count; i++)
                {
                    var m = runtimeBreachMaterials[i];
                    if (m == null) continue;
                    if (i < breachMatBaseColors.Count)
                    {
                        Color bc = breachMatBaseColors[i] * envFade;
                        if (m.HasProperty(IdBaseColor)) m.SetColor(IdBaseColor, bc);
                        if (m.HasProperty(IdColor)) m.SetColor(IdColor, bc);
                    }
                    if (i < breachMatEmColors.Count && m.HasProperty(IdEmissionColor))
                    {
                        m.SetColor(IdEmissionColor, breachMatEmColors[i] * envFade);
                    }
                    if (m.HasProperty(IdIntensity))
                    {
                        m.SetFloat(IdIntensity, 1.15f * envFade);
                    }
                }
                for (int i = 0; i < breachGodRayTransforms.Count; i++)
                {
                    if (breachGodRayTransforms[i] != null)
                        breachGodRayTransforms[i].localScale = new Vector3(1f - closePhase * 0.92f, 1f, 1f - closePhase * 0.92f);
                }
            }
        }

        private void EnsureDragParticles()
        {
            Shader particleShader = Shader.Find("PacificRim/SplashParticle") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            Texture2D softTex = CreateSoftGlowParticleTexture();

            var siltGo = new GameObject("Ending_SeabedDragSilt");
            dragSiltPs = siltGo.AddComponent<ParticleSystem>();
            var sMain = dragSiltPs.main;
            sMain.loop = true;
            sMain.playOnAwake = false;
            sMain.maxParticles = 300;
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 4.2f);
            sMain.startSpeed = 0f;
            sMain.startSize = new ParticleSystem.MinMaxCurve(5.5f, 12.5f);
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;
            dragSiltPs.emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());
            var sEm = dragSiltPs.emission; sEm.enabled = false;
            var sRend = siltGo.GetComponent<ParticleSystemRenderer>();
            var siltMat = new Material(particleShader) { name = "EndingDragSilt_Mat" };
            siltMat.mainTexture = softTex;
            if (siltMat.HasProperty("_MainTex")) siltMat.SetTexture("_MainTex", softTex);
            if (siltMat.HasProperty("_BaseMap")) siltMat.SetTexture("_BaseMap", softTex);
            if (siltMat.HasProperty("_TintColor")) siltMat.SetColor("_TintColor", new Color(0.08f, 0.10f, 0.14f, 0.38f));
            if (siltMat.HasProperty("_InvFade")) siltMat.SetFloat("_InvFade", 0.35f);
            if (siltMat.HasProperty("_LightInfluence")) siltMat.SetFloat("_LightInfluence", 0.85f);
            sRend.sharedMaterial = siltMat;
            sRend.shadowCastingMode = ShadowCastingMode.Off;
            dragSiltPs.Play();

            var bubGo = new GameObject("Ending_SeabedDragBubbles");
            dragBubblesPs = bubGo.AddComponent<ParticleSystem>();
            var bMain = dragBubblesPs.main;
            bMain.loop = true;
            bMain.playOnAwake = false;
            bMain.maxParticles = 400;
            bMain.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
            bMain.startSpeed = 0f;
            bMain.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.34f);
            bMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var bEm = dragBubblesPs.emission; bEm.enabled = false;
            var bRend = bubGo.GetComponent<ParticleSystemRenderer>();
            var bubMat = new Material(particleShader) { name = "EndingDragBubbles_Mat" };
            bubMat.mainTexture = softTex;
            if (bubMat.HasProperty("_MainTex")) bubMat.SetTexture("_MainTex", softTex);
            if (bubMat.HasProperty("_BaseMap")) bubMat.SetTexture("_BaseMap", softTex);
            if (bubMat.HasProperty("_TintColor")) bubMat.SetColor("_TintColor", new Color(0.75f, 0.90f, 1.0f, 0.55f));
            if (bubMat.HasProperty("_InvFade")) bubMat.SetFloat("_InvFade", 0.3f);
            if (bubMat.HasProperty("_LightInfluence")) bubMat.SetFloat("_LightInfluence", 0.7f);
            bRend.sharedMaterial = bubMat;
            bRend.shadowCastingMode = ShadowCastingMode.Off;
            dragBubblesPs.Play();
        }

        private static Texture2D cachedSoftGlowTex;

        private static Texture2D CreateSoftGlowParticleTexture()
        {
            if (cachedSoftGlowTex != null) return cachedSoftGlowTex;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "EndingCutscene_SoftParticleTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float half = (size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float falloff = Mathf.Clamp01(1f - r);
                    float a = Mathf.Pow(falloff, 2.0f);
                    float rgb = Mathf.Clamp01(0.5f * falloff + 0.65f * Mathf.Exp(-r * r * 4.5f));
                    pixels[y * size + x] = new Color(rgb, rgb, rgb, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            cachedSoftGlowTex = tex;
            return tex;
        }

        private void EmitDragSiltAndBubbles(Vector3 jaegerPos, Vector3 slatternDragCenter)
        {
            if (dragSiltPs != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 p = slatternDragCenter + new Vector3(Random.Range(-7.5f, 7.5f), Random.Range(0.4f, 1.8f), Random.Range(-10f, 10f));
                    dragSiltPs.Emit(new ParticleSystem.EmitParams
                    {
                        position = p,
                        velocity = new Vector3(Random.Range(-1.8f, 1.8f), Random.Range(0.6f, 2.2f), Random.Range(-1.5f, 0.5f)),
                        startSize = Random.Range(4.8f, 10.5f),
                        startLifetime = Random.Range(2.0f, 3.8f),
                        startColor = new Color(0.14f, 0.16f, 0.22f, 0.34f)
                    }, 1);
                }
            }
            if (dragBubblesPs != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    Vector3 p = jaegerPos + new Vector3(Random.Range(-3.5f, 3.5f), Random.Range(0.5f, 3.0f), Random.Range(-3.5f, 3.5f));
                    dragBubblesPs.Emit(new ParticleSystem.EmitParams
                    {
                        position = p,
                        velocity = Vector3.up * Random.Range(2.8f, 6.2f) + Random.insideUnitSphere * 0.8f,
                        startSize = Random.Range(0.10f, 0.32f),
                        startLifetime = Random.Range(1.4f, 2.6f),
                        startColor = new Color(0.78f, 0.92f, 1.0f, 0.65f)
                    }, 1);
                }
            }
        }

        private void LateUpdate()
        {
            if (!isCutsceneRunning) return;

            if (applyLateUpdatePoses && jaegerTransform != null)
            {
                Vector3 fwd = jaegerTransform.forward;
                Vector3 up = jaegerTransform.up;
                Vector3 rgt = jaegerTransform.right;

                // 1. Capture animated foot plant targets BEFORE dropping pelvis for heavy crouch/haul
                float crouchW = Mathf.Clamp01(jaegerCrouchWeight) * (1f - Mathf.Clamp01(jaegerDiveBlend));
                Vector3 plantedFootL = footL != null ? footL.position : Vector3.zero;
                Vector3 plantedFootR = footR != null ? footR.position : Vector3.zero;

                if (crouchW > 0.005f && hipsBone != null)
                {
                    hipsBone.position += Vector3.down * (jaegerCrouchDropMeters * crouchW) - fwd * (0.45f * crouchW);
                }

                // 2. Heavy forward lean & hauling shoulder/torso twist
                if (jaegerDiveBlend < 0.99f)
                {
                    float w = 1f - jaegerDiveBlend;
                    if (Mathf.Abs(jaegerForwardLeanDeg) > 0.01f)
                    {
                        if (hipsBone != null) hipsBone.rotation = Quaternion.AngleAxis(jaegerForwardLeanDeg * 0.30f * w, rgt) * hipsBone.rotation;
                        if (spine1Bone != null) spine1Bone.rotation = Quaternion.AngleAxis(jaegerForwardLeanDeg * 0.38f * w, rgt) * spine1Bone.rotation;
                        if (chestBone != null) chestBone.rotation = Quaternion.AngleAxis(jaegerForwardLeanDeg * 0.32f * w, rgt) * chestBone.rotation;
                    }
                    if (Mathf.Abs(jaegerTorsoTwistDeg) > 0.01f)
                    {
                        if (spine1Bone != null) spine1Bone.rotation = Quaternion.AngleAxis(jaegerTorsoTwistDeg * 0.45f * w, Vector3.up) * spine1Bone.rotation;
                        if (chestBone != null) chestBone.rotation = Quaternion.AngleAxis(jaegerTorsoTwistDeg * 0.55f * w, Vector3.up) * chestBone.rotation;
                    }
                    if (Mathf.Abs(jaegerHeadTiltDeg) > 0.01f && headBone != null)
                    {
                        headBone.rotation = Quaternion.AngleAxis(jaegerHeadTiltDeg * w, rgt) * headBone.rotation;
                    }
                }

                // 3. Re-solve grounded legs onto planted feet during heavy crouch/haul
                if (crouchW > 0.005f && thighL != null && shinL != null && footL != null && thighR != null && shinR != null && footR != null)
                {
                    Vector3 poleL = (fwd - rgt * 0.34f).normalized;
                    Vector3 poleR = (fwd + rgt * 0.34f).normalized;
                    SolveTwoBoneIKWorld(thighL, shinL, footL, Vector3.Lerp(footL.position, plantedFootL, crouchW), poleL, keepTipLocalRotation: false, animHintWeight: 0.42f);
                    SolveTwoBoneIKWorld(thighR, shinR, footR, Vector3.Lerp(footR.position, plantedFootR, crouchW), poleR, keepTipLocalRotation: false, animHintWeight: 0.42f);
                }

                // 4. Right arm reaching down/back within true arm reach (~6.0m) to grip Slattern's skull low behind hip
                if (ikGripHead && upperArmR != null && forearmR != null && handR != null)
                {
                    Vector3 anchorOffset = -up * 5.25f - fwd * 2.85f + rgt * 0.55f;
                    gripWorldTarget = upperArmR.position + anchorOffset;
                    Vector3 ikTarget = gripWorldTarget;
                    if (slatternRagdoll != null)
                    {
                        Vector3 skullPos = slatternRagdoll.SkullGripWorldPos;
                        Vector3 delta = skullPos - upperArmR.position;
                        if (delta.magnitude > 6.45f) delta = delta.normalized * 6.45f;
                        ikTarget = Vector3.Lerp(gripWorldTarget, upperArmR.position + delta, 0.72f);
                    }
                    Vector3 elbowPole = (-fwd * 0.45f + rgt * 0.85f + up * 0.25f).normalized;
                    SolveTwoBoneIKWorld(upperArmR, forearmR, handR, ikTarget, elbowPole, keepTipLocalRotation: true, animHintWeight: 0.15f);
                }

                // 5. Heavy outer-right heave follow-through swinging Slattern cleanly past Gipsy's right flank (no crossing body)
                if (ikHeaveForward && heaveAnimWeight > 0.01f && upperArmR != null && forearmR != null && handR != null)
                {
                    Vector3 throwTarget = upperArmR.position
                        + fwd * Mathf.Lerp(-2.6f, 5.2f, heaveForwardT)
                        + rgt * (2.2f + Mathf.Sin(heaveForwardT * Mathf.PI) * 1.8f)
                        + up * Mathf.Lerp(-4.5f, 2.4f, heaveForwardT);
                    Vector3 poleR = (rgt * 0.92f - up * 0.25f).normalized;
                    SolveTwoBoneIKWorld(upperArmR, forearmR, handR, Vector3.Lerp(handR.position, throwTarget, heaveAnimWeight), poleR, keepTipLocalRotation: true, animHintWeight: 0.15f);
                }

                // 6. Heavy braced left-arm counterweight during drag & crouch (prevents casual walk arm swing)
                if (braceLeftArmWeight > 0.01f && jaegerDiveBlend < 0.05f && upperArmL != null && forearmL != null && handL != null)
                {
                    Vector3 braceL = upperArmL.position + (-rgt * 1.45f + fwd * 2.15f - up * 4.35f);
                    Vector3 poleL = (-fwd * 0.55f - rgt * 0.80f - up * 0.15f).normalized;
                    SolveTwoBoneIKWorld(upperArmL, forearmL, handL, Vector3.Lerp(handL.position, braceL, braceLeftArmWeight), poleL, keepTipLocalRotation: true, animHintWeight: 0.2f);
                }

                // 7. Upright Jumping IK Animation (heavy staggered jump knees + chest-guard arms, ZERO root rotation)
                if (jaegerDiveBlend > 0.01f)
                {
                    float dw = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(jaegerDiveBlend));
                    float rushDrift = Mathf.Sin(jaegerPlungeTime * 4.2f) * 0.35f;

                    if (spine1Bone != null)
                        spine1Bone.rotation = Quaternion.AngleAxis(6f * dw, rgt) * spine1Bone.rotation;
                    if (chestBone != null)
                        chestBone.rotation = Quaternion.AngleAxis(5f * dw, rgt) * chestBone.rotation;
                    if (headBone != null)
                        headBone.rotation = Quaternion.AngleAxis(-14f * dw, rgt) * headBone.rotation;

                    if (upperArmL != null && forearmL != null && handL != null)
                    {
                        Vector3 jumpArmL = upperArmL.position + (-up * 3.15f + fwd * 2.55f - rgt * 2.35f + up * rushDrift * 0.45f);
                        Vector3 armPoleL = (-rgt * 0.78f - fwd * 0.55f - up * 0.25f).normalized;
                        SolveTwoBoneIKWorld(upperArmL, forearmL, handL, Vector3.Lerp(handL.position, jumpArmL, dw), armPoleL, keepTipLocalRotation: true, animHintWeight: 0.15f);
                    }
                    if (upperArmR != null && forearmR != null && handR != null)
                    {
                        Vector3 jumpArmR = upperArmR.position + (-up * 3.55f + fwd * 1.85f + rgt * 2.45f - up * rushDrift * 0.45f);
                        Vector3 armPoleR = (rgt * 0.78f - fwd * 0.55f - up * 0.25f).normalized;
                        SolveTwoBoneIKWorld(upperArmR, forearmR, handR, Vector3.Lerp(handR.position, jumpArmR, dw), armPoleR, keepTipLocalRotation: true, animHintWeight: 0.15f);
                    }

                    if (thighL != null && shinL != null && footL != null)
                    {
                        Vector3 jumpFootL = thighL.position + (-up * 7.25f + fwd * 2.35f - rgt * 0.48f + up * rushDrift * 0.55f);
                        Vector3 legPoleL = (fwd * 0.95f - rgt * 0.25f).normalized;
                        SolveTwoBoneIKWorld(thighL, shinL, footL, Vector3.Lerp(footL.position, jumpFootL, dw), legPoleL, keepTipLocalRotation: true, animHintWeight: 0.15f);
                    }
                    if (thighR != null && shinR != null && footR != null)
                    {
                        Vector3 jumpFootR = thighR.position + (-up * 9.55f - fwd * 1.15f + rgt * 0.48f - up * rushDrift * 0.55f);
                        Vector3 legPoleR = (fwd * 0.88f + rgt * 0.28f).normalized;
                        SolveTwoBoneIKWorld(thighR, shinR, footR, Vector3.Lerp(footR.position, jumpFootR, dw), legPoleR, keepTipLocalRotation: true, animHintWeight: 0.15f);
                    }
                }

                // 8. Curl Gipsy's right fist around Slattern's skull (and left fist during braced crouch/plunge)
                if (gripRightFistWeight > 0.01f)
                {
                    ApplyHandFistCurl(isRightHand: true, gripRightFistWeight);
                }
                if (braceLeftArmWeight > 0.01f || jaegerDiveBlend > 0.01f)
                {
                    ApplyHandFistCurl(isRightHand: false, Mathf.Max(braceLeftArmWeight, jaegerDiveBlend) * 0.85f);
                }

                // 9. Sync Slattern's articulated PhysX ragdoll bodies back onto the skinned skeleton
                if (useRagdollPhysics && slatternRagdoll != null)
                {
                    slatternRagdoll.SyncToSkeleton();
                }
                else if (headAttachedToGrip && slatternHeadNeckChain.Count >= 2)
                {
                    AlignHeadNeckChainToTarget(gripWorldTarget);
                }
            }

            if (lockCameraInLateUpdate && mainCamera != null)
            {
                Vector3 shakePos = cameraShake != null ? cameraShake.CurrentOffset : Vector3.zero;
                Vector3 shakeRot = cameraShake != null ? cameraShake.CurrentRotationOffset : Vector3.zero;
                mainCamera.transform.SetPositionAndRotation(camTargetPos + camTargetRot * shakePos, camTargetRot * Quaternion.Euler(shakeRot));
                mainCamera.fieldOfView = camTargetFov;
            }
        }

        private void ApplyHandFistCurl(bool isRightHand, float weight)
        {
            float w = Mathf.Clamp01(weight);
            float sign = isRightHand ? -1f : 1f; // Hand.R curls inward with -Z; Hand.L curls inward with +Z
            Quaternion p1 = Quaternion.Euler(0f, 0f, 48f * sign);
            Quaternion p2 = Quaternion.Euler(0f, 0f, 62f * sign);
            Quaternion p3 = Quaternion.Euler(0f, 0f, 48f * sign);

            if (isRightHand)
            {
                BlendFingerChain(indexR1, indexR2, indexR3, p1, p2, p3, w);
                BlendFingerChain(middleR1, middleR2, middleR3, p1, p2, p3, w);
                BlendFingerChain(ringR1, ringR2, ringR3, p1, p2, p3, w);
                BlendFingerChain(pinkyR1, pinkyR2, pinkyR3, p1, p2, p3, w);
                BlendFingerChain(thumbR1, thumbR2, null, Quaternion.Euler(28f, -18f, -38f), Quaternion.Euler(0f, 0f, -36f), Quaternion.identity, w);
            }
            else
            {
                BlendFingerChain(indexL1, indexL2, indexL3, p1, p2, p3, w);
                BlendFingerChain(middleL1, middleL2, middleL3, p1, p2, p3, w);
                BlendFingerChain(ringL1, ringL2, ringL3, p1, p2, p3, w);
                BlendFingerChain(pinkyL1, pinkyL2, pinkyL3, p1, p2, p3, w);
                BlendFingerChain(thumbL1, thumbL2, null, Quaternion.Euler(28f, 18f, 38f), Quaternion.Euler(0f, 0f, 36f), Quaternion.identity, w);
            }
        }

        private static void BlendFingerChain(Transform b1, Transform b2, Transform b3, Quaternion r1, Quaternion r2, Quaternion r3, float w)
        {
            if (b1 != null) b1.localRotation = Quaternion.Slerp(b1.localRotation, r1, w);
            if (b2 != null) b2.localRotation = Quaternion.Slerp(b2.localRotation, r2, w);
            if (b3 != null) b3.localRotation = Quaternion.Slerp(b3.localRotation, r3, w);
        }

        private void AlignHeadNeckChainToTarget(Vector3 targetWorldPos)
        {
            // Gently articulate UpperTorso -> Neck so UpperHead (and attached jaw) sits squarely inside Gipsy's right hand
            Transform headTip = slatternHeadBone != null ? slatternHeadBone : slatternHeadNeckChain[slatternHeadNeckChain.Count - 1];
            float[] weights = { 0.38f, 0.78f };
            for (int iter = 0; iter < 2; iter++)
            {
                int chainEnd = Mathf.Min(slatternHeadNeckChain.Count - 2, weights.Length - 1);
                for (int i = chainEnd; i >= 0; i--)
                {
                    Transform bone = slatternHeadNeckChain[i];
                    if (bone == null || headTip == null) continue;
                    Vector3 toHead = headTip.position - bone.position;
                    Vector3 toTarget = targetWorldPos - bone.position;
                    if (toHead.sqrMagnitude > 0.0001f && toTarget.sqrMagnitude > 0.0001f)
                    {
                        Quaternion delta = Quaternion.FromToRotation(toHead.normalized, toTarget.normalized);
                        bone.rotation = Quaternion.Slerp(Quaternion.identity, delta, weights[i]) * bone.rotation;
                    }
                }
            }
        }

        private void StartAnteverseBackgroundPreload()
        {
            if (anteversePreloadOp != null) return;
            anteversePreloadOp = SceneManager.LoadSceneAsync(AnteverseSceneName, LoadSceneMode.Single);
            if (anteversePreloadOp != null)
            {
                anteversePreloadOp.allowSceneActivation = false;
            }
        }

        private void HandOffToAnteverseScene()
        {
            isCutsceneRunning = false;
            applyLateUpdatePoses = false;
            lockCameraInLateUpdate = false;
            useRagdollPhysics = false;
            if (slatternRagdoll != null)
            {
                slatternRagdoll.Dispose();
                slatternRagdoll = null;
            }

            AnteverseEndingCutscene.HandoffFromOperationPitfall = true;
            AnteverseEndingCutscene.RecordedCombatTime = recordedCombatTime;

            if (anteversePreloadOp != null)
            {
                anteversePreloadOp.allowSceneActivation = true;
            }
            else
            {
                SceneManager.LoadScene(AnteverseSceneName);
            }
        }

        private IEnumerator SkipEndingCutsceneRoutine()
        {
            isCutsceneRunning = false;
            applyLateUpdatePoses = false;
            lockCameraInLateUpdate = false;
            useRagdollPhysics = false;
            if (slatternRagdoll != null)
            {
                slatternRagdoll.Dispose();
                slatternRagdoll = null;
            }

            StopEndingAudio();
            PacificRimMusicManager.Instance?.StopMusicImmediate();

            if (fadeOverlayImage != null) fadeOverlayImage.color = Color.black;
            if (fadeOverlayGroup != null) fadeOverlayGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(0.2f);

            AnteverseEndingCutscene.HandoffFromOperationPitfall = false;
            AnteverseEndingCutscene.SkipStraightToVictory = true;
            AnteverseEndingCutscene.RecordedCombatTime = recordedCombatTime;

            if (anteversePreloadOp != null)
            {
                anteversePreloadOp.allowSceneActivation = true;
            }
            else
            {
                SceneManager.LoadScene(AnteverseSceneName);
            }
        }

        private void StartEndingAudio()
        {
            var audioMgr = PacificRimAudioManager.Instance;
            if (audioMgr == null) return;

            var underClip = audioMgr.GetClip("ambientsound_underwater");
            if (underClip != null)
            {
                underwaterRumbleSource = CreateLocalLoopAudio("Ending_UnderwaterPressure", underClip, 0.35f, 0.78f);
            }
            var dragClip = audioMgr.GetClip("Earthquake 3 - Big");
            if (dragClip != null)
            {
                dragAudioSource = CreateLocalLoopAudio("Ending_DragRumble", dragClip, 0.22f, 0.65f);
            }
            var sparkClip = audioMgr.GetClip("EmpSparksArcing_Loop");
            if (sparkClip != null)
            {
                sparkAudioSource = CreateLocalLoopAudio("Ending_JointSparks", sparkClip, 0.18f, 1.05f);
            }

            var sfxGo = new GameObject("Ending_CutsceneSfx");
            sfxGo.transform.SetParent(transform, false);
            cutsceneSfxSource = sfxGo.AddComponent<AudioSource>();
            cutsceneSfxSource.spatialBlend = 0f;
            cutsceneSfxSource.bypassListenerEffects = true;
        }

        private void StopEndingAudio()
        {
            if (underwaterRumbleSource != null) underwaterRumbleSource.Stop();
            if (dragAudioSource != null) dragAudioSource.Stop();
            if (sparkAudioSource != null) sparkAudioSource.Stop();
        }

        private AudioSource CreateLocalLoopAudio(string objName, AudioClip clip, float volume, float pitch)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.spatialBlend = 0f;
            src.pitch = pitch;
            float sfxVol = PacificRimAudioManager.Instance != null ? PacificRimAudioManager.Instance.SfxVolume : 0.55f;
            src.volume = volume * sfxVol;
            src.Play();
            return src;
        }

        private void PlayOneShot2D(string clipName, float volume, float pitch)
        {
            var audioMgr = PacificRimAudioManager.Instance;
            var clip = audioMgr != null ? audioMgr.GetClip(clipName) : null;
            if (clip != null && cutsceneSfxSource != null)
            {
                float sfxVol = audioMgr.SfxVolume;
                cutsceneSfxSource.pitch = pitch;
                cutsceneSfxSource.PlayOneShot(clip, Mathf.Clamp01(volume * sfxVol));
            }
        }

        private Vector3 ResolveBreachTargetPoint()
        {
            if (breachTransform != null)
            {
                var core = breachTransform.Find("Breach_InnerCoreGlow");
                if (core != null)
                {
                    var r = core.GetComponent<Renderer>();
                    if (r != null) return r.bounds.center;
                }
                return breachTransform.position;
            }
            return new Vector3(5.5f, -0.9f, 120.2f);
        }

        private float SampleTerrainY(float worldX, float worldZ)
        {
            if (trenchTerrain != null)
            {
                return trenchTerrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + trenchTerrain.transform.position.y;
            }
            return 0f;
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
                jaegerTransform.SetPositionAndRotation(pos, rot);
            }
        }

        private void SetSlatternPose(Vector3 pos, Quaternion rot)
        {
            if (slatternRigidbody != null)
            {
                slatternRigidbody.position = pos;
                slatternRigidbody.rotation = rot;
            }
            if (slatternTransform != null)
            {
                slatternTransform.SetPositionAndRotation(pos, rot);
            }
        }

        private void EnsureCutsceneUI()
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
                scaler.matchWidthOrHeight = 0.5f;
            }

            var existingLB = canvasGo.transform.Find("Cinematic_Letterbox");
            if (existingLB != null)
            {
                letterboxRoot = existingLB.GetComponent<RectTransform>();
                letterboxCanvasGroup = existingLB.GetComponent<CanvasGroup>();
            }
            else
            {
                var lbGo = new GameObject("Cinematic_Letterbox", typeof(RectTransform), typeof(CanvasGroup));
                lbGo.transform.SetParent(canvasGo.transform, false);
                letterboxRoot = lbGo.GetComponent<RectTransform>();
                letterboxRoot.anchorMin = Vector2.zero;
                letterboxRoot.anchorMax = Vector2.one;
                letterboxRoot.offsetMin = Vector2.zero;
                letterboxRoot.offsetMax = Vector2.zero;
                letterboxCanvasGroup = lbGo.GetComponent<CanvasGroup>();
                letterboxCanvasGroup.alpha = 0f;
                letterboxCanvasGroup.blocksRaycasts = false;
                letterboxCanvasGroup.interactable = false;

                CreateBar("TopBar", letterboxRoot, true, 132f);
                var bottomBar = CreateBar("BottomBar", letterboxRoot, false, 132f);

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

        private static Mesh BuildSubdividedPlaneMesh(float width, float length, int segX, int segZ)
        {
            int cols = segX + 1;
            int rows = segZ + 1;
            var verts = new Vector3[cols * rows];
            var uvs = new Vector2[verts.Length];
            var tris = new int[segX * segZ * 6];

            for (int r = 0; r < rows; r++)
            {
                float v = (float)r / segZ;
                float z = (v - 0.5f) * length;
                for (int c = 0; c < cols; c++)
                {
                    float u = (float)c / segX;
                    float x = (u - 0.5f) * width;
                    int idx = r * cols + c;
                    verts[idx] = new Vector3(x, 0f, z);
                    uvs[idx] = new Vector2(u, v);
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

            var m = new Mesh { name = "BreachMembraneGrid" };
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            float inv = 1f - t;
            return inv * inv * p0 + 2f * inv * t * p1 + t * t * p2;
        }

        private static void SolveTwoBoneIKWorld(
            Transform root, Transform mid, Transform tip, Vector3 targetPos, Vector3 poleForward,
            bool keepTipLocalRotation = false, float animHintWeight = 0.35f)
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
            float clampedDist = Mathf.Clamp(dist, Mathf.Abs(l1 - l2) + 0.05f, (l1 + l2) * 0.995f);
            Vector3 dir = dist > 0.0001f ? (toTarget / dist) : Vector3.down;
            Vector3 clampedTarget = a + dir * clampedDist;

            float hw = Mathf.Clamp01(animHintWeight);
            Vector3 animMidProj = a + Vector3.Project(b - a, dir);
            Vector3 poleProj = Vector3.ProjectOnPlane(poleForward, dir);
            Vector3 poleUnit = poleProj.sqrMagnitude > 0.0001f ? poleProj.normalized : dir;
            Vector3 bendDir = Vector3.ProjectOnPlane((b - animMidProj) * hw + poleUnit * (l1 * (1f - hw)), dir);
            if (bendDir.sqrMagnitude < 0.0001f) bendDir = poleProj;
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
            if (keepTipLocalRotation) tip.localRotation = origTipLocalRot;
            else tip.rotation = origTipRot;
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

        /// <summary>
        /// 1:1 world-scale articulated PhysX ragdoll proxy for Category V Slattern.
        /// Bypasses the 2567x FBX Armature scale cleanly by simulating unscaled meter-scale rigidbodies,
        /// colliders, and projection-locked ConfigurableJoints, then syncing bone transforms in LateUpdate.
        /// </summary>
        private sealed class SlatternCutsceneRagdoll
        {
            private sealed class RagdollNode
            {
                public Transform bone;
                public GameObject proxyGo;
                public Rigidbody rb;
                public Collider col;
            }

            private sealed class RigidFollowBone
            {
                public Transform bone;
                public Transform leaderBone;
                public Vector3 relOffsetInLeaderFrame;
                public Quaternion relRotToLeader;
            }

            private readonly GameObject rootGo;
            private readonly TerrainCollider terrainCollider;
            private readonly List<RagdollNode> nodes = new List<RagdollNode>();
            private readonly List<RigidFollowBone> rigidFollowers = new List<RigidFollowBone>();
            private readonly Dictionary<string, Transform> boneMap = new Dictionary<string, Transform>();

            private RagdollNode lowerTorsoNode;
            private RagdollNode middleTorsoNode;
            private RagdollNode upperTorsoNode;
            private RagdollNode neckNode;
            private RagdollNode upperHeadNode;
            private RagdollNode tailCenterNode;

            private Transform lowerHeadBone;
            private Quaternion lowerHeadRelToUpperHead = Quaternion.identity;

            private Vector3 skullGripLocalOffset = Vector3.zero;
            private GameObject gripAnchorGo;
            private Rigidbody gripAnchorRb;
            private ConfigurableJoint gripJoint;

            private GameObject jaegerShieldGo;
            private Rigidbody jaegerShieldRb;
            private CapsuleCollider jaegerShieldCol;

            private bool isReleasedIntoBreach = false;
            private Vector3 breachCoreTarget;

            public Vector3 SkullGripWorldPos => upperHeadNode != null
                ? upperHeadNode.proxyGo.transform.TransformPoint(skullGripLocalOffset)
                : Vector3.zero;

            public Vector3 HeadWorldPos => upperHeadNode != null ? upperHeadNode.proxyGo.transform.position : Vector3.zero;
            public Vector3 TorsoWorldCenter => upperTorsoNode != null ? upperTorsoNode.proxyGo.transform.position : Vector3.zero;
            public Vector3 TailWorldCenter => tailCenterNode != null ? tailCenterNode.proxyGo.transform.position : Vector3.zero;

            public SlatternCutsceneRagdoll(Transform slatternTransform, TerrainCollider terrainCol)
            {
                terrainCollider = terrainCol;
                rootGo = new GameObject("_SlatternCutsceneRagdoll");

                foreach (var t in slatternTransform.GetComponentsInChildren<Transform>(true))
                {
                    if (!boneMap.ContainsKey(t.name)) boneMap[t.name] = t;
                }

                var dragPhysMat = new PhysicsMaterial("SlatternDragPhysMat")
                {
                    dynamicFriction = 0.52f,
                    staticFriction = 0.58f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };

                // Core spine & skull
                lowerTorsoNode = CreateNode("LowerTorso", "MiddleTorso", 850f, 2.35f, dragPhysMat);
                middleTorsoNode = CreateJointedNode("MiddleTorso", lowerTorsoNode, "UpperTorso", 620f, 2.45f, 24f, 20f, 16f, 65f, 450f, dragPhysMat);
                upperTorsoNode = CreateJointedNode("UpperTorso", middleTorsoNode, "Neck", 720f, 2.60f, 24f, 22f, 18f, 65f, 480f, dragPhysMat);
                neckNode = CreateJointedNode("Neck", upperTorsoNode, "UpperHead", 210f, 1.50f, 36f, 32f, 25f, 22f, 220f, dragPhysMat);
                upperHeadNode = CreateJointedNode("UpperHead", neckNode, "UpperHead_end", 240f, 1.75f, 42f, 36f, 28f, 16f, 180f, dragPhysMat);

                if (upperHeadNode != null && boneMap.TryGetValue("UpperHead_end", out var headEnd))
                {
                    skullGripLocalOffset = upperHeadNode.proxyGo.transform.InverseTransformPoint(headEnd.position) * 0.44f;
                }
                if (upperHeadNode != null && boneMap.TryGetValue("LowerHead", out lowerHeadBone))
                {
                    lowerHeadRelToUpperHead = Quaternion.Inverse(upperHeadNode.bone.rotation) * lowerHeadBone.rotation;
                }

                // Sibling pelvic/shoulder/tail roots that sit outside parent bone chain in FBX
                RegisterRigidFollower("Bone.024", lowerTorsoNode?.bone);
                RegisterRigidFollower("Pelvis.L", lowerTorsoNode?.bone);
                RegisterRigidFollower("Pelvis.R", lowerTorsoNode?.bone);
                RegisterRigidFollower("Shoulder.L", upperTorsoNode?.bone);
                RegisterRigidFollower("Shoulder.R", upperTorsoNode?.bone);

                // Arms (connected to UpperTorso)
                var uArmL = CreateJointedNode("UpperArm.L", upperTorsoNode, "LowerArm.L", 150f, 1.45f, 62f, 52f, 40f, 12f, 130f, dragPhysMat);
                CreateJointedNode("LowerArm.L", uArmL, "Wrist.L", 120f, 1.25f, 68f, 32f, 26f, 8f, 110f, dragPhysMat);
                var uArmR = CreateJointedNode("UpperArm.R", upperTorsoNode, "LowerArm.R", 150f, 1.45f, 62f, 52f, 40f, 12f, 130f, dragPhysMat);
                CreateJointedNode("LowerArm.R", uArmR, "Wrist.R", 120f, 1.25f, 68f, 32f, 26f, 8f, 110f, dragPhysMat);

                // Legs (connected to LowerTorso)
                var uLegL = CreateJointedNode("UpperLeg.L", lowerTorsoNode, "LowerLeg.L", 190f, 1.60f, 58f, 42f, 32f, 15f, 150f, dragPhysMat);
                var lLegL = CreateJointedNode("LowerLeg.L", uLegL, "Ankle.L", 145f, 1.35f, 68f, 28f, 22f, 10f, 120f, dragPhysMat);
                CreateJointedNode("Ankle.L", lLegL, "Foot.L_end", 95f, 1.15f, 48f, 24f, 20f, 8f, 85f, dragPhysMat);

                var uLegR = CreateJointedNode("UpperLeg.R", lowerTorsoNode, "LowerLeg.R", 190f, 1.60f, 58f, 42f, 32f, 15f, 150f, dragPhysMat);
                var lLegR = CreateJointedNode("LowerLeg.R", uLegR, "Ankle.R", 145f, 1.35f, 68f, 28f, 22f, 10f, 120f, dragPhysMat);
                CreateJointedNode("Ankle.R", lLegR, "Foot.R_end", 95f, 1.15f, 48f, 24f, 20f, 8f, 85f, dragPhysMat);

                // Triple Tails (connected to LowerTorso via Bone.025)
                var tailRoot = CreateJointedNode("Bone.025", lowerTorsoNode, "TailBone", 140f, 1.40f, 40f, 36f, 22f, 18f, 130f, dragPhysMat);
                var tc1 = CreateJointedNode("TailBone", tailRoot, "TailBone.002", 95f, 1.10f, 44f, 44f, 25f, 10f, 95f, dragPhysMat);
                var tc2 = CreateJointedNode("TailBone.002", tc1, "TailBone.001", 80f, 0.95f, 46f, 46f, 25f, 8f, 80f, dragPhysMat);
                tailCenterNode = tc2 ?? tc1;
                var tc3 = CreateJointedNode("TailBone.001", tc2, "TailBone.003", 65f, 0.85f, 48f, 48f, 25f, 6f, 65f, dragPhysMat);
                CreateJointedNode("TailBone.003", tc3, "TailBone.004_end", 50f, 0.72f, 48f, 48f, 25f, 5f, 55f, dragPhysMat);

                var tl1 = CreateJointedNode("TailBone.L", tailRoot, "TailBone.L.001", 85f, 1.00f, 44f, 44f, 25f, 10f, 85f, dragPhysMat);
                var tl2 = CreateJointedNode("TailBone.L.001", tl1, "TailBone.L.002", 70f, 0.85f, 46f, 46f, 25f, 8f, 70f, dragPhysMat);
                CreateJointedNode("TailBone.L.002", tl2, "TailBone.L.004_end", 55f, 0.72f, 48f, 48f, 25f, 5f, 55f, dragPhysMat);

                var tr1 = CreateJointedNode("TailBone.R", tailRoot, "TailBone.R.001", 85f, 1.00f, 44f, 44f, 25f, 10f, 85f, dragPhysMat);
                var tr2 = CreateJointedNode("TailBone.R.001", tr1, "TailBone.R.002", 70f, 0.85f, 46f, 46f, 25f, 8f, 70f, dragPhysMat);
                CreateJointedNode("TailBone.R.002", tr2, "TailBone.R.004_end", 55f, 0.72f, 48f, 48f, 25f, 5f, 55f, dragPhysMat);

                // Ignore all internal self-collisions and non-Terrain scene colliders so only seabed TerrainCollider acts on limbs
                var allSceneColliders = FindObjectsByType<Collider>(FindObjectsInactive.Include);
                for (int i = 0; i < nodes.Count; i++)
                {
                    var ci = nodes[i].col;
                    if (ci == null) continue;
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        if (nodes[j].col != null) Physics.IgnoreCollision(ci, nodes[j].col, true);
                    }
                    for (int k = 0; k < allSceneColliders.Length; k++)
                    {
                        var sc = allSceneColliders[k];
                        if (sc == null || sc == terrainCollider) continue;
                        bool isOwn = false;
                        for (int m = 0; m < nodes.Count; m++)
                        {
                            if (nodes[m].col == sc) { isOwn = true; break; }
                        }
                        if (!isOwn) Physics.IgnoreCollision(ci, sc, true);
                    }
                }
            }

            private void RegisterRigidFollower(string followerName, Transform leaderBone)
            {
                if (leaderBone == null || !boneMap.TryGetValue(followerName, out var fBone)) return;
                rigidFollowers.Add(new RigidFollowBone
                {
                    bone = fBone,
                    leaderBone = leaderBone,
                    relOffsetInLeaderFrame = Quaternion.Inverse(leaderBone.rotation) * (fBone.position - leaderBone.position),
                    relRotToLeader = Quaternion.Inverse(leaderBone.rotation) * fBone.rotation
                });
            }

            private RagdollNode CreateNode(string boneName, string tipName, float mass, float radius, PhysicsMaterial physMat)
            {
                if (!boneMap.TryGetValue(boneName, out var bone)) return null;
                var proxyGo = new GameObject($"Proxy_{boneName}");
                proxyGo.transform.SetParent(rootGo.transform, true);
                proxyGo.transform.SetPositionAndRotation(bone.position, bone.rotation);
                proxyGo.transform.localScale = Vector3.one;

                var rb = proxyGo.AddComponent<Rigidbody>();
                rb.mass = mass;
                rb.linearDamping = 1.65f;
                rb.angularDamping = 3.20f;
                rb.useGravity = false; // We apply calibrated underwater gravity & Breach suction manually
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.solverIterations = 16;
                rb.solverVelocityIterations = 6;

                Collider col = null;
                if (!string.IsNullOrEmpty(tipName) && boneMap.TryGetValue(tipName, out var tip))
                {
                    Vector3 localTip = proxyGo.transform.InverseTransformPoint(tip.position);
                    float len = localTip.magnitude;
                    if (len > 0.25f)
                    {
                        var colGo = new GameObject("Col");
                        colGo.transform.SetParent(proxyGo.transform, false);
                        colGo.transform.localPosition = localTip * 0.5f;
                        colGo.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localTip.normalized);
                        var cap = colGo.AddComponent<CapsuleCollider>();
                        cap.direction = 1;
                        cap.radius = radius;
                        cap.height = Mathf.Max(radius * 2.1f, len + radius * 0.45f);
                        cap.sharedMaterial = physMat;
                        col = cap;
                    }
                }
                if (col == null)
                {
                    var sph = proxyGo.AddComponent<SphereCollider>();
                    sph.radius = radius;
                    sph.sharedMaterial = physMat;
                    col = sph;
                }

                var node = new RagdollNode { bone = bone, proxyGo = proxyGo, rb = rb, col = col };
                nodes.Add(node);
                return node;
            }

            private RagdollNode CreateJointedNode(
                string boneName, RagdollNode parentNode, string tipName,
                float mass, float radius, float swingX, float swingY, float twistZ,
                float spring, float damper, PhysicsMaterial physMat)
            {
                var node = CreateNode(boneName, tipName, mass, radius, physMat);
                if (node == null || parentNode == null) return node;

                var joint = node.proxyGo.AddComponent<ConfigurableJoint>();
                joint.connectedBody = parentNode.rb;
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector3.zero;
                joint.connectedAnchor = parentNode.proxyGo.transform.InverseTransformPoint(node.proxyGo.transform.position);
                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Locked;
                joint.angularXMotion = ConfigurableJointMotion.Limited;
                joint.angularYMotion = ConfigurableJointMotion.Limited;
                joint.angularZMotion = ConfigurableJointMotion.Limited;
                joint.lowAngularXLimit = new SoftJointLimit { limit = -swingX };
                joint.highAngularXLimit = new SoftJointLimit { limit = swingX };
                joint.angularYLimit = new SoftJointLimit { limit = swingY };
                joint.angularZLimit = new SoftJointLimit { limit = twistZ };
                joint.rotationDriveMode = RotationDriveMode.Slerp;
                joint.slerpDrive = new JointDrive { positionSpring = spring, positionDamper = damper, maximumForce = float.MaxValue };
                joint.projectionMode = JointProjectionMode.PositionAndRotation;
                joint.projectionDistance = 0.04f;
                joint.projectionAngle = 4f;
                joint.enableCollision = false;
                joint.enablePreprocessing = false;
                return node;
            }

            public void AttachGripToSkull(Vector3 worldPos)
            {
                if (upperHeadNode == null) return;
                gripAnchorGo = new GameObject("Slattern_SkullGripAnchor");
                gripAnchorGo.transform.SetParent(rootGo.transform, true);
                gripAnchorGo.transform.position = worldPos;
                gripAnchorRb = gripAnchorGo.AddComponent<Rigidbody>();
                gripAnchorRb.isKinematic = true;
                gripAnchorRb.useGravity = false;

                gripJoint = upperHeadNode.proxyGo.AddComponent<ConfigurableJoint>();
                gripJoint.connectedBody = gripAnchorRb;
                gripJoint.autoConfigureConnectedAnchor = false;
                gripJoint.anchor = skullGripLocalOffset;
                gripJoint.connectedAnchor = Vector3.zero;
                gripJoint.xMotion = ConfigurableJointMotion.Locked;
                gripJoint.yMotion = ConfigurableJointMotion.Locked;
                gripJoint.zMotion = ConfigurableJointMotion.Locked;
                gripJoint.angularXMotion = ConfigurableJointMotion.Free;
                gripJoint.angularYMotion = ConfigurableJointMotion.Free;
                gripJoint.angularZMotion = ConfigurableJointMotion.Free;
                gripJoint.rotationDriveMode = RotationDriveMode.Slerp;
                gripJoint.slerpDrive = new JointDrive { positionSpring = 0f, positionDamper = 180f, maximumForce = float.MaxValue };
                gripJoint.projectionMode = JointProjectionMode.PositionAndRotation;
                gripJoint.projectionDistance = 0.05f;
                gripJoint.projectionAngle = 180f;
                gripJoint.enableCollision = false;
            }

            public void MoveGripAnchor(Vector3 targetWorldPos)
            {
                if (gripAnchorRb != null)
                {
                    gripAnchorRb.MovePosition(targetWorldPos);
                }
            }

            public void UpdateJaegerShield(Vector3 jaegerRootWorldPos, float radius, bool active)
            {
                if (!active)
                {
                    if (jaegerShieldCol != null) jaegerShieldCol.enabled = false;
                    return;
                }
                if (jaegerShieldGo == null)
                {
                    jaegerShieldGo = new GameObject("__JaegerBodyShield");
                    jaegerShieldGo.transform.SetParent(rootGo.transform, true);
                    jaegerShieldGo.transform.position = jaegerRootWorldPos + Vector3.up * 10.5f;
                    jaegerShieldRb = jaegerShieldGo.AddComponent<Rigidbody>();
                    jaegerShieldRb.isKinematic = true;
                    jaegerShieldRb.useGravity = false;
                    jaegerShieldCol = jaegerShieldGo.AddComponent<CapsuleCollider>();
                    jaegerShieldCol.direction = 1;
                    jaegerShieldCol.height = 24f;
                    jaegerShieldCol.radius = radius;
                    if (terrainCollider != null)
                    {
                        Physics.IgnoreCollision(jaegerShieldCol, terrainCollider, true);
                    }
                }
                jaegerShieldCol.enabled = true;
                jaegerShieldCol.radius = radius;
                jaegerShieldRb.MovePosition(jaegerRootWorldPos + Vector3.up * 10.5f);
            }

            public void ApplyDragDownwardWeight(float gravityAccel)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    var rb = nodes[i].rb;
                    if (rb != null)
                    {
                        rb.AddForce(Vector3.down * gravityAccel, ForceMode.Acceleration);
                    }
                }
            }

            public void ApplyHeaveAssist(Vector3 targetVel, float weight)
            {
                float w = Mathf.Clamp01(weight);
                void Guide(RagdollNode n, float strength)
                {
                    if (n == null || n.rb == null) return;
#if UNITY_6000_0_OR_NEWER
                    Vector3 dv = targetVel - n.rb.linearVelocity;
#else
                    Vector3 dv = targetVel - n.rb.velocity;
#endif
                    n.rb.AddForce(dv * (strength * w), ForceMode.Acceleration);
                }
                Guide(upperTorsoNode, 18f);
                Guide(middleTorsoNode, 15f);
                Guide(lowerTorsoNode, 13f);
                ApplyDragDownwardWeight(12f);
            }

            public void ReleaseAndThrowIntoBreach(Vector3 throwVelocity, Vector3 tumbleAngularVel, Vector3 breachTarget)
            {
                isReleasedIntoBreach = true;
                breachCoreTarget = breachTarget;

                if (gripJoint != null)
                {
                    Destroy(gripJoint);
                    gripJoint = null;
                }
                if (gripAnchorGo != null)
                {
                    Destroy(gripAnchorGo);
                    gripAnchorGo = null;
                    gripAnchorRb = null;
                }

                // Bypass flat seabed TerrainCollider inside the Breach so Slattern plunges deep into the rift
                for (int i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i];
                    if (n.col != null && terrainCollider != null)
                    {
                        Physics.IgnoreCollision(n.col, terrainCollider, true);
                    }
                    if (n.rb != null)
                    {
                        n.rb.linearDamping = 0.42f;
                        n.rb.angularDamping = 0.90f;
#if UNITY_6000_0_OR_NEWER
                        n.rb.linearVelocity = throwVelocity;
#else
                        n.rb.velocity = throwVelocity;
#endif
                        if (n == lowerTorsoNode || n == middleTorsoNode || n == upperTorsoNode || n == upperHeadNode)
                        {
                            n.rb.angularVelocity = tumbleAngularVel;
                        }
                    }
                }
            }

            public bool HasPiercedBreachPlane(float planeY)
            {
                if (!isReleasedIntoBreach) return false;
                if (upperHeadNode != null && upperHeadNode.proxyGo.transform.position.y <= planeY) return true;
                if (upperTorsoNode != null && upperTorsoNode.proxyGo.transform.position.y <= planeY) return true;
                return false;
            }

            public void StepFreeFallIntoBreach(float dt)
            {
                if (!isReleasedIntoBreach) return;

                Vector3 deepTarget = new Vector3(breachCoreTarget.x, -110f, breachCoreTarget.z + 2f);
                for (int i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i];
                    if (n.rb == null) continue;

                    // Heavy gravity + Breach abyssal suction funneling Slattern deep down below the membrane
                    Vector3 p = n.proxyGo.transform.position;
                    float suctionGain = p.z > 94f ? 1f : Mathf.InverseLerp(86f, 96f, p.z);
                    Vector3 toDeep = new Vector3(deepTarget.x - p.x, 0f, deepTarget.z - p.z);
                    Vector3 accel = Vector3.down * Mathf.Lerp(26f, 52f, suctionGain) + toDeep * (1.25f * suctionGain);
                    n.rb.AddForce(accel, ForceMode.Acceleration);
                }
            }

            public void SyncToSkeleton()
            {
                if (lowerTorsoNode == null) return;

                // Sync root pelvis position & rotation first
                lowerTorsoNode.bone.SetPositionAndRotation(
                    lowerTorsoNode.proxyGo.transform.position,
                    lowerTorsoNode.proxyGo.transform.rotation);

                // Keep sibling pelvis/tail roots glued to LowerTorso in world meters
                for (int i = 0; i < rigidFollowers.Count; i++)
                {
                    var rf = rigidFollowers[i];
                    if (rf.bone == null || rf.leaderBone == null) continue;
                    rf.bone.SetPositionAndRotation(
                        rf.leaderBone.position + (rf.leaderBone.rotation * rf.relOffsetInLeaderFrame),
                        rf.leaderBone.rotation * rf.relRotToLeader);
                }

                // Copy world rotations in strict parent-before-child creation order
                for (int i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i];
                    if (n == lowerTorsoNode || n.bone == null || n.proxyGo == null) continue;
                    n.bone.rotation = n.proxyGo.transform.rotation;

                    // Refresh Shoulder.L/R after UpperTorso rotates (since Shoulder.L/R are children of MiddleTorso in FBX)
                    if (n == upperTorsoNode)
                    {
                        for (int r = 0; r < rigidFollowers.Count; r++)
                        {
                            var rf = rigidFollowers[r];
                            if (rf.leaderBone == upperTorsoNode.bone && rf.bone != null)
                            {
                                rf.bone.SetPositionAndRotation(
                                    rf.leaderBone.position + (rf.leaderBone.rotation * rf.relOffsetInLeaderFrame),
                                    rf.leaderBone.rotation * rf.relRotToLeader);
                            }
                        }
                    }
                }

                // Keep jaw (LowerHead) aligned with slack open droop relative to UpperHead
                if (lowerHeadBone != null && upperHeadNode != null)
                {
                    lowerHeadBone.rotation = upperHeadNode.bone.rotation * lowerHeadRelToUpperHead * Quaternion.Euler(14f, 0f, 0f);
                }
            }

            public void Dispose()
            {
                if (rootGo != null) Destroy(rootGo);
                nodes.Clear();
                rigidFollowers.Clear();
            }
        }
    }
}
