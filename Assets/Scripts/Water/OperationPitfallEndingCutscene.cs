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

        // Jaeger bones for procedural lean, arm IK, and sparks
        private Transform hipsBone, spine1Bone, chestBone, headBone;
        private Transform upperArmL, forearmL, handL, upperArmR, forearmR, handR;
        private Transform thighL, shinL, footL, thighR, shinR, footR;

        // Slattern bones for limp head/neck dragging
        private readonly List<Transform> slatternHeadNeckChain = new List<Transform>();
        private Transform slatternHeadBone;
        private Transform slatternTorsoDragBone;

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
        private readonly List<Light> breachLights = new List<Light>();

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
        private float jaegerDiveBlend = 0f;
        private bool ikGripHead = false;
        private bool ikHeaveForward = false;
        private float heaveAnimWeight = 0f;
        private float heaveForwardT = 0f;
        private bool headAttachedToGrip = false;
        private Vector3 gripWorldTarget;
        private Vector3 slatternDragDirection = Vector3.forward;

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
            yield return new WaitForSeconds(0.25f);

            // Show letterbox bars and start eerie finale audio
            if (letterboxCanvasGroup != null) letterboxCanvasGroup.alpha = 1f;
            StartEndingAudio();
            PacificRimMusicManager.Instance?.PlayEndingSuspenseMusic(1.5f);

            // =========================================================================
            // BEAT 1 (0:00 – 0:06): Dragging Slattern Across the Abyss
            // =========================================================================
            Vector3 breachCenter = ResolveBreachTargetPoint();
            Vector3 dragStartJaeger = new Vector3(4.5f, SampleTerrainY(4.5f, 52.0f) + 2.977f, 52.0f);
            Vector3 dragEndJaeger = new Vector3(4.5f, SampleTerrainY(4.5f, 83.5f) + 2.977f, 83.5f);
            Quaternion faceBreachRot = Quaternion.LookRotation(new Vector3(breachCenter.x - dragStartJaeger.x, 0f, breachCenter.z - dragStartJaeger.z).normalized, Vector3.up);

            SetJaegerPose(dragStartJaeger, faceBreachRot);
            if (jaegerAnimator != null)
            {
                jaegerAnimator.speed = 0.72f;
                jaegerAnimator.SetFloat("Speed", 0.85f);
                jaegerAnimator.SetBool("IsRunning", false);
                jaegerAnimator.SetBool("InCombat", false);
                jaegerAnimator.SetBool("IsBlocking", false);
                jaegerAnimator.SetFloat("WalkDirection", 1f);
                jaegerAnimator.SetFloat("RunBlend", 0f);
                if (jaegerAnimator.layerCount > 1) jaegerAnimator.SetLayerWeight(1, 0f);
                jaegerAnimator.CrossFade("Locomotion", 0.05f, 0, 0f);
            }

            if (slatternAnimator != null)
            {
                slatternAnimator.Play("DeathLoop", 0, 0.25f);
                slatternAnimator.speed = 0f;
            }

            applyLateUpdatePoses = true;
            lockCameraInLateUpdate = true;
            jaegerForwardLeanDeg = 22f;
            jaegerDiveBlend = 0f;
            ikGripHead = true;
            ikHeaveForward = false;
            headAttachedToGrip = true;
            slatternDragDirection = faceBreachRot * Vector3.forward;

            float dragTimer = 0f;
            float fadeInDur = 1.0f;
            float siltEmitAcc = 0f;
            float footstepAcc = 0f;

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

                Vector3 jPos = Vector3.Lerp(dragStartJaeger, dragEndJaeger, u);
                jPos.y = SampleTerrainY(jPos.x, jPos.z) + 2.977f;
                SetJaegerPose(jPos, faceBreachRot);

                // Orient Slattern so its massive skull sits right behind Gipsy's right hip/hand while torso & tails trail behind
                Vector3 fwd = faceBreachRot * Vector3.forward;
                Vector3 right = faceBreachRot * Vector3.right;
                Vector3 slatPos = jPos - fwd * 29.5f + right * 1.6f;
                slatPos.y = SampleTerrainY(slatPos.x, slatPos.z) + 1.85f;
                float dragWobble = Mathf.Sin(dragTimer * 2.3f) * 2.2f;
                Quaternion slatDragRot = faceBreachRot * Quaternion.Euler(0f, 180f, dragWobble);
                SetSlatternPose(slatPos, slatDragRot);

                // Emit seabed silt & dust plumes kicked up by dragging Slattern's body/tails & Gipsy's footsteps
                siltEmitAcc += dt;
                if (siltEmitAcc >= 0.045f)
                {
                    siltEmitAcc = 0f;
                    Vector3 torsoDragCenter = slatternTorsoDragBone != null ? slatternTorsoDragBone.position : (jPos - fwd * 16.5f);
                    EmitDragSiltAndBubbles(jPos, torsoDragCenter);
                }

                footstepAcc += dt;
                if (footstepAcc >= 1.15f)
                {
                    footstepAcc = 0f;
                    PacificRimAudioManager.Instance?.PlayOceanFootstep(jPos, 1.35f);
                    PacificRimAudioManager.Instance?.PlayArmorCreak(jPos + Vector3.up * 15f, 0.7f);
                    if (cameraShake != null) cameraShake.TriggerPlayerHitFeedback(isHeavy: false);
                }

                UpdateSparkEmitters();

                // Sweeping low-angle tracking shot: starts slightly ahead-3/4 wide enough to frame Gipsy head-to-toe
                // (glowing orange chest turbine, twin headlights, scarred armor, sparking joints & Gipsy hauling Slattern by the head),
                // then tracks smoothly back-alongside to reveal the glowing magenta-blue Breach ahead
                float sweepU = Mathf.SmoothStep(0f, 1f, u);
                Vector3 camStartOffset = right * 28.0f + fwd * 10.5f + Vector3.up * 4.4f;
                Vector3 camEndOffset = right * 26.0f - fwd * 11.0f + Vector3.up * 6.8f;
                camTargetPos = jPos + Vector3.Lerp(camStartOffset, camEndOffset, sweepU);

                Vector3 lookAtDrag = jPos + Vector3.up * 10.2f - fwd * 4.5f;
                Vector3 lookAtAhead = Vector3.Lerp(jPos + Vector3.up * 9.5f + fwd * 8.0f, breachCenter + Vector3.up * 4.0f, 0.38f);
                Vector3 lookTarget = Vector3.Lerp(lookAtDrag, lookAtAhead, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.16f) / 0.84f)));
                camTargetRot = Quaternion.LookRotation((lookTarget - camTargetPos).normalized, Vector3.up);
                camTargetFov = Mathf.Lerp(54f, 55f, sweepU);

                yield return null;
            }

            if (fadeOverlayGroup != null) fadeOverlayGroup.alpha = 0f;

            // =========================================================================
            // BEAT 2 (0:06 – 0:11): Triggering the Genetic Lock & The Leap
            // =========================================================================
            Vector3 edgePos = dragEndJaeger;
            Vector3 breachEntryTarget = new Vector3(breachCenter.x, -1.2f, breachCenter.z - 2.0f);
            Vector3 edgeFwd = faceBreachRot * Vector3.forward;
            Vector3 slatStartPos = slatternTransform != null ? slatternTransform.position : (edgePos - edgeFwd * 29.5f);
            Quaternion slatStartRot = slatternTransform != null ? slatternTransform.rotation : (faceBreachRot * Quaternion.Euler(0f, 180f, 0f));

            // Hard cut to wide three-quarter rear/side elevated angle looking down into the Breach chasm
            Vector3 beat2CamStart = new Vector3(-28f, 34f, 62f);
            Vector3 beat2CamEnd = new Vector3(-22f, 29f, 70f);
            Vector3 beat2LookStart = new Vector3(4.5f, 2.0f, 112f);
            Vector3 beat2LookEnd = new Vector3(5.0f, -2.5f, 117f);

            camTargetPos = beat2CamStart;
            camTargetRot = Quaternion.LookRotation((beat2LookStart - beat2CamStart).normalized, Vector3.up);
            camTargetFov = 54f;

            float b2Timer = 0f;
            bool releasedSlattern = false;
            bool triggeredGeneticLock = false;
            bool startedGipsyLeap = false;
            bool triggeredRiftRoar = false;

            Vector3 heavePivotPos = edgePos - edgeFwd * 10.5f + Vector3.up * 4.5f;
            Quaternion heavePivotRot = faceBreachRot * Quaternion.Euler(14f, 150f, 0f);

            // Phases within 5.0s Beat 2:
            //   0.0 - 0.9s : Gipsy swings/heaves Slattern up and forward toward the Breach edge by its head
            //   0.9 - 2.3s : Slattern arcs into the Breach energy field; Genetic Lock triggers at 1.65s!
            //   2.3 - 2.8s : Membrane rips wide open in blinding violet-blue flare; Gipsy steps onto rim
            //   2.8 - 5.0s : Gipsy leaps headfirst into the blinding rift right behind Slattern
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

                // Smoothly pull camera slightly forward along the elevated 3/4 rear/side angle
                float camU = Mathf.SmoothStep(0f, 1f, u);
                camTargetPos = Vector3.Lerp(beat2CamStart, beat2CamEnd, camU);
                Vector3 lookPt = Vector3.Lerp(beat2LookStart, beat2LookEnd, camU);
                camTargetRot = Quaternion.LookRotation((lookPt - camTargetPos).normalized, Vector3.up);
                camTargetFov = Mathf.Lerp(54f, 50f, camU);

                if (b2Timer < 0.90f)
                {
                    // Wind-up & heave swing
                    float hu = Mathf.Clamp01(b2Timer / 0.90f);
                    ikGripHead = hu < 0.72f;
                    ikHeaveForward = hu >= 0.35f;
                    heaveAnimWeight = Mathf.SmoothStep(0f, 1f, hu);
                    heaveForwardT = hu;
                    headAttachedToGrip = hu < 0.72f;
                    jaegerForwardLeanDeg = Mathf.Lerp(22f, 10f, hu);

                    // Advance Gipsy slightly up to rim
                    Vector3 jP = Vector3.Lerp(edgePos, edgePos + edgeFwd * 2.5f, hu);
                    jP.y = SampleTerrainY(jP.x, jP.z) + 2.977f;
                    SetJaegerPose(jP, faceBreachRot);

                    Vector3 swingArc = Vector3.Lerp(slatStartPos, heavePivotPos, Mathf.SmoothStep(0f, 1f, hu));
                    SetSlatternPose(swingArc, Quaternion.Slerp(slatStartRot, heavePivotRot, Mathf.SmoothStep(0f, 1f, hu)));
                }
                else
                {
                    if (!releasedSlattern)
                    {
                        releasedSlattern = true;
                        ikGripHead = false;
                        ikHeaveForward = true;
                        headAttachedToGrip = false;
                        PacificRimAudioManager.Instance?.PlayHydraulicServo(edgePos + Vector3.up * 12f, 1.0f);
                        PacificRimAudioManager.Instance?.PlayAttackWhoosh(edgePos + Vector3.up * 14f, true);
                    }

                    // Slattern body arcs forward and plunges into the Breach membrane (0.9s -> 2.8s)
                    float slatThrowU = Mathf.Clamp01((b2Timer - 0.90f) / 1.90f);
                    Vector3 arcPeak = new Vector3(breachEntryTarget.x, 12.0f, 100f);
                    Vector3 slatDropEnd = new Vector3(breachEntryTarget.x, -26f, breachEntryTarget.z + 14f);
                    Vector3 curSlatPos = QuadraticBezier(
                        heavePivotPos,
                        arcPeak,
                        slatDropEnd,
                        slatThrowU);
                    Quaternion curSlatRot = Quaternion.Slerp(
                        heavePivotRot,
                        faceBreachRot * Quaternion.Euler(76f, 165f, -12f),
                        slatThrowU);
                    SetSlatternPose(curSlatPos, curSlatRot);

                    // Genetic lock triggers as Slattern's body penetrates the membrane (~1.65s into Beat 2)
                    if (!triggeredGeneticLock && b2Timer >= 1.65f)
                    {
                        triggeredGeneticLock = true;
                        TriggerGeneticLockSequence();
                    }

                    if (triggeredGeneticLock)
                    {
                        float lockElapsed = b2Timer - 1.65f;
                        UpdateGeneticLockMembrane(lockElapsed);
                    }

                    // Gipsy braces at rim (0.9s -> 2.75s) then leaps headfirst into the blinding rift (2.75s -> 5.0s)
                    if (b2Timer < 2.75f)
                    {
                        float stepU = Mathf.Clamp01((b2Timer - 0.90f) / 1.85f);
                        heaveAnimWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((b2Timer - 0.90f) / 0.8f));
                        jaegerForwardLeanDeg = Mathf.Lerp(10f, 28f, stepU);
                        Vector3 rimPos = Vector3.Lerp(edgePos + (faceBreachRot * Vector3.forward) * 2.5f, edgePos + (faceBreachRot * Vector3.forward) * 7.0f, stepU);
                        rimPos.y = SampleTerrainY(rimPos.x, rimPos.z) + 2.977f;
                        SetJaegerPose(rimPos, faceBreachRot);
                    }
                    else
                    {
                        if (!startedGipsyLeap)
                        {
                            startedGipsyLeap = true;
                            ikHeaveForward = false;
                            if (jaegerAnimator != null)
                            {
                                jaegerAnimator.speed = 0.6f;
                                jaegerAnimator.CrossFade("Idle", 0.15f, 0, 0f);
                            }
                            if (visualSockets != null)
                            {
                                visualSockets.TriggerRocketBlast(1.6f);
                            }
                            PacificRimAudioManager.Instance?.PlayRocketIgnition(jaegerTransform.position + Vector3.up * 12f);
                            if (cameraShake != null) cameraShake.TriggerPlayerHitFeedback(isHeavy: true);
                        }

                        float leapU = Mathf.Clamp01((b2Timer - 2.75f) / (geneticLockAndLeapDuration - 2.75f));
                        jaegerDiveBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(leapU * 1.4f));

                        Vector3 leapStart = edgePos + (faceBreachRot * Vector3.forward) * 7.0f;
                        Vector3 leapApex = new Vector3(breachEntryTarget.x, 14.5f, 103.5f);
                        Vector3 leapEnd = new Vector3(breachEntryTarget.x, -22f, breachEntryTarget.z + 2f);
                        Vector3 divePos = QuadraticBezier(leapStart, leapApex, leapEnd, Mathf.Pow(leapU, 0.92f));

                        // Rotate swiftly headfirst into the chasm
                        Quaternion diveRot = Quaternion.Slerp(faceBreachRot, faceBreachRot * Quaternion.Euler(132f, 0f, 0f), Mathf.Pow(leapU, 0.58f));
                        SetJaegerPose(divePos, diveRot);

                        // Blinding rift surge right as Gipsy hits the chasm core (last 0.28s before hard cut)
                        if (!triggeredRiftRoar && leapU >= 0.72f)
                        {
                            triggeredRiftRoar = true;
                            PlayOneShot2D("Magic Spell_Simple Swoosh_6", 0.9f, 0.75f);
                            PlayOneShot2D("EmpDischargeBlast", 0.75f, 1.15f);
                        }

                        if (leapU >= 0.85f && fadeOverlayImage != null && fadeOverlayGroup != null)
                        {
                            fadeOverlayImage.color = new Color(0.68f, 0.85f, 1.0f, 1f);
                            fadeOverlayGroup.alpha = Mathf.SmoothStep(0f, 0.85f, Mathf.Clamp01((leapU - 0.85f) / 0.15f));
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
            breachLights.Clear();

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

        private void UpdateGeneticLockMembrane(float lockElapsed)
        {
            if (breachMembraneMat != null)
            {
                float rippleT = Mathf.Clamp01(lockElapsed / 1.15f);
                float tearT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((lockElapsed - 0.18f) / 0.95f));
                float flarePulse = 1.45f + Mathf.Sin(Mathf.Clamp01(lockElapsed / 1.35f) * Mathf.PI) * 5.2f + tearT * 1.6f;

                breachMembraneMat.SetFloat(IdRippleProgress, rippleT * 1.65f);
                breachMembraneMat.SetFloat(IdTearAmount, tearT);
                breachMembraneMat.SetFloat(IdFlareIntensity, flarePulse);
            }

            if (breachFlareLight != null)
            {
                float spike = Mathf.Sin(Mathf.Clamp01(lockElapsed / 1.5f) * Mathf.PI);
                breachFlareLight.intensity = 950f + spike * 2800f;
                breachFlareLight.color = Color.Lerp(new Color(0.58f, 0.38f, 1.0f), new Color(0.68f, 0.85f, 1.0f), spike);
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
                Vector3 rightAxis = jaegerTransform.right;
                if (jaegerForwardLeanDeg > 0.01f && jaegerDiveBlend < 0.99f)
                {
                    float w = 1f - jaegerDiveBlend;
                    if (hipsBone != null) hipsBone.rotation = Quaternion.AngleAxis(jaegerForwardLeanDeg * 0.32f * w, rightAxis) * hipsBone.rotation;
                    if (spine1Bone != null) spine1Bone.rotation = Quaternion.AngleAxis(jaegerForwardLeanDeg * 0.38f * w, rightAxis) * spine1Bone.rotation;
                    if (chestBone != null) chestBone.rotation = Quaternion.AngleAxis(jaegerForwardLeanDeg * 0.30f * w, rightAxis) * chestBone.rotation;
                }

                // Right arm reaching back/down beside right hip to grip Slattern's skull/head
                if (ikGripHead && upperArmR != null && forearmR != null && handR != null)
                {
                    Vector3 fwd = jaegerTransform.forward;
                    Vector3 rgt = jaegerTransform.right;
                    gripWorldTarget = jaegerTransform.position + rgt * 2.75f - fwd * 2.5f + Vector3.up * 7.8f;
                    Vector3 elbowPole = (-fwd * 0.55f + rgt * 0.8f + Vector3.up * 0.35f).normalized;
                    SolveTwoBoneIKWorld(upperArmR, forearmR, handR, gripWorldTarget, elbowPole);
                }

                // Heave pose swinging Slattern forward over the Breach rim
                if (ikHeaveForward && heaveAnimWeight > 0.01f && upperArmR != null && forearmR != null && handR != null)
                {
                    Vector3 fwd = jaegerTransform.forward;
                    Vector3 rgt = jaegerTransform.right;
                    Vector3 throwTarget = jaegerTransform.position + fwd * Mathf.Lerp(-2.5f, 8.2f, heaveForwardT) + rgt * Mathf.Lerp(3.2f, 0.8f, heaveForwardT) + Vector3.up * Mathf.Lerp(7.8f, 17.5f, heaveForwardT);
                    Vector3 poleR = (rgt + Vector3.down * 0.3f).normalized;
                    SolveTwoBoneIKWorld(upperArmR, forearmR, handR, Vector3.Lerp(handR.position, throwTarget, heaveAnimWeight), poleR);
                }

                // Headfirst dive pose (0:08.7 - 0:11)
                if (jaegerDiveBlend > 0.01f)
                {
                    Vector3 fwd = jaegerTransform.forward;
                    Vector3 up = jaegerTransform.up;
                    Vector3 rgt = jaegerTransform.right;
                    if (upperArmL != null && forearmL != null && handL != null)
                    {
                        Vector3 diveArmL = handL.parent.position + (up * 4.5f - rgt * 1.4f - fwd * 1.2f);
                        SolveTwoBoneIKWorld(upperArmL, forearmL, handL, Vector3.Lerp(handL.position, diveArmL, jaegerDiveBlend), -fwd);
                    }
                    if (upperArmR != null && forearmR != null && handR != null)
                    {
                        Vector3 diveArmR = handR.parent.position + (up * 4.5f + rgt * 1.4f - fwd * 1.2f);
                        SolveTwoBoneIKWorld(upperArmR, forearmR, handR, Vector3.Lerp(handR.position, diveArmR, jaegerDiveBlend), -fwd);
                    }
                }

                // Lift & align Slattern's upper torso/neck/skull cleanly into Gipsy's gripping right hand
                if (headAttachedToGrip && slatternHeadNeckChain.Count >= 2)
                {
                    AlignHeadNeckChainToTarget(gripWorldTarget);
                }
            }

            if (lockCameraInLateUpdate && mainCamera != null)
            {
                mainCamera.transform.SetPositionAndRotation(camTargetPos, camTargetRot);
                mainCamera.fieldOfView = camTargetFov;
            }
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
            Vector3 bendDir = Vector3.ProjectOnPlane((b - animMidProj) * 0.35f + poleUnit * (l1 * 0.65f), dir);
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
    }
}
