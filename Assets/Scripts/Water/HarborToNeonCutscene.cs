using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using PacificRimAudio;
using PacificRimUI;
using PacificRimWater;

namespace PacificRimCutscene
{
    /// <summary>
    /// Part 1 (0:00 - 0:13) of the Phase 3 Save Striker Eureka -> Hong Kong Neon transition cutscene:
    /// Shot 1 (0:00-0:06): The Charge across waist-deep storm harbor water.
    /// Shot 2 (0:06-0:13): Crushing Bear Hug, 360+ degree Hammer Spin vortex, and launch out over the bay.
    /// Preloads "Hong Kong Neon Highway" in background for a seamless hard cut at 0:13.
    /// </summary>
    [DefaultExecutionOrder(600)]
    public class HarborToNeonCutscene : MonoBehaviour
    {
        public static HarborToNeonCutscene Instance { get; private set; }

        public bool isCutsceneRunning { get; private set; }
        public float CurrentTimelineTime => timelineElapsed;

        private SimpleJaegerController jaeger;
        private KaijuAI kaiju;
        private Animator jaegerAnim;
        private Animator kaijuAnim;
        private Rigidbody jaegerRb;
        private Rigidbody kaijuRb;
        private Camera mainCam;
        private JaegerCameraFollow camFollow;
        private PacificRimCameraShake camShake;
        private WaterSystem waterSystem;
        private WaterSplashManager splashManager;

        // Bone references
        private Transform jRoot, jHips, jSpine, jChest, jHead;
        private Transform jUpperArmL, jForearmL, jHandL;
        private Transform jUpperArmR, jForearmR, jHandR;
        private Transform jThighL, jShinL, jFootL;
        private Transform jThighR, jShinR, jFootR;

        private Transform kLowerTorso, kUpperTorso, kClavicle, kNeck;
        private Transform kBicepL, kForearmL, kHandL;
        private Transform kBicepR, kForearmR, kHandR;
        private Transform kHipL, kUpperLegL, kLowerLegL, kAnkleL;
        private Transform kHipR, kUpperLegR, kLowerLegR, kAnkleR;

        // Environment overrides & runtime VFX
        private Vector3 origOceanPos;
        private bool adjustedOcean;
        private HarborToNeonCutsceneShared.LetterboxRig uiRig;
        private GameObject vfxRoot;
        private ParticleSystem bowSprayLeft;
        private ParticleSystem bowSprayRight;
        private ParticleSystem vortexSprayLeft;
        private ParticleSystem vortexSprayRight;
        private Light lightningFlashLight;

        private AsyncOperation preloadOp;
        private bool skipRequested;
        private bool hardCutCommitted;

        // Per-frame LateUpdate state driven from timeline coroutine
        private float timelineElapsed;
        private int activeShot; // 0 = pre-fade, 1 = Shot 1 (0-6s), 2 = Shot 2 (6-13s)
        private float hugIkWeight;
        private float spinNormalized;
        private float centrifugalPitchDeg;
        private float airborneBlend;
        private Vector3 throwVelocityWorld;
        private float customShakeStrength;
        private Vector3 baseCamPos;
        private Quaternion baseCamRot;
        private float baseCamFov = 44f;

        // Anchor coordinates in Save Striker Eureka
        private static readonly Vector3 GipsyHarborStartPos = new Vector3(0f, 2.98f, 0f);
        private const float GipsyHarborStartYaw = -13.2f;
        private static readonly Vector3 KaijuChargeStartPos = new Vector3(-16.0f, 3.09f, 68.0f);
        private static readonly Vector3 KaijuImpactTargetPos = new Vector3(-1.4f, 3.09f, 5.2f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            HarborToNeonTransitionState.IsPart1Running = false;
            if (adjustedOcean && waterSystem != null)
            {
                waterSystem.transform.position = origOceanPos;
            }
            if (vfxRoot != null) Destroy(vfxRoot);
            if (uiRig != null && uiRig.Canvas != null) Destroy(uiRig.Canvas.gameObject);
        }

        /// <summary>
        /// Entry point called when Leatherback hits Phase 3 HP threshold in Save Striker Eureka.
        /// </summary>
        public static void TriggerFromPhase3(KaijuAI kaijuRef)
        {
            if (HarborToNeonTransitionState.IsAnyCutsceneRunning) return;

            HarborToNeonCutscene existing = Instance != null
                ? Instance
                : FindAnyObjectByType<HarborToNeonCutscene>();

            if (existing == null)
            {
                var go = new GameObject("HarborToNeon_Part1Cutscene");
                existing = go.AddComponent<HarborToNeonCutscene>();
            }

            existing.BeginCutscene(kaijuRef);
        }

        public void BeginCutscene(KaijuAI kaijuRef)
        {
            if (isCutsceneRunning) return;
            isCutsceneRunning = true;
            HarborToNeonTransitionState.IsPart1Running = true;
            HarborToNeonTransitionState.PendingPart2Cutscene = true;
            HarborToNeonTransitionState.PendingSkipPart2ToCombat = false;
            HarborToNeonTransitionState.StartNeonCityAtPhase3HalfHp = true;
            HarborToNeonTransitionState.ElapsedAtHandoff = 0f;

            kaiju = kaijuRef != null ? kaijuRef : FindAnyObjectByType<KaijuAI>();
            jaeger = SimpleJaegerController.Instance != null ? SimpleJaegerController.Instance : FindAnyObjectByType<SimpleJaegerController>();
            mainCam = Camera.main;
            camFollow = mainCam != null ? mainCam.GetComponent<JaegerCameraFollow>() : FindAnyObjectByType<JaegerCameraFollow>();
            camShake = mainCam != null ? mainCam.GetComponent<PacificRimCameraShake>() : null;
            waterSystem = WaterSystem.Instance != null ? WaterSystem.Instance : FindAnyObjectByType<WaterSystem>();
            splashManager = WaterSplashManager.Instance != null ? WaterSplashManager.Instance : FindAnyObjectByType<WaterSplashManager>();

            CacheBones();
            StartCoroutine(RunPart1Routine());
        }

        private void CacheBones()
        {
            if (jaeger != null)
            {
                jaegerAnim = jaeger.GetComponentInChildren<Animator>();
                jaegerRb = jaeger.GetComponent<Rigidbody>();
                Transform arm = jaeger.transform.Find("Armature");
                if (arm != null)
                {
                    jRoot = arm.Find("Root");
                    jHips = jRoot != null ? jRoot.Find("Hips") : null;
                    jSpine = jHips != null ? jHips.Find("Spine1") : null;
                    jChest = jSpine != null ? jSpine.Find("Chest") : null;
                    jHead = jChest != null ? jChest.Find("Head") : null;

                    jUpperArmL = jChest != null ? jChest.Find("Upperarm.L") : null;
                    jForearmL = jUpperArmL != null ? jUpperArmL.Find("Forearm.L") : null;
                    jHandL = jForearmL != null ? jForearmL.Find("Hand.L") : null;

                    jUpperArmR = jChest != null ? jChest.Find("Upperarm.R") : null;
                    jForearmR = jUpperArmR != null ? jUpperArmR.Find("Forearm.R") : null;
                    jHandR = jForearmR != null ? jForearmR.Find("Hand.R") : null;

                    jThighL = jHips != null ? jHips.Find("Thigh.L") : null;
                    jShinL = jThighL != null ? jThighL.Find("Shin.L") : null;
                    jFootL = jShinL != null ? jShinL.Find("Foot.L") : null;

                    jThighR = jHips != null ? jHips.Find("Thigh.R") : null;
                    jShinR = jThighR != null ? jThighR.Find("Shin.R") : null;
                    jFootR = jShinR != null ? jShinR.Find("Foot.R") : null;
                }
            }

            if (kaiju != null)
            {
                kaijuAnim = kaiju.GetComponentInChildren<Animator>();
                kaijuRb = kaiju.GetComponent<Rigidbody>();
                Transform arm = kaiju.transform.Find("Armature");
                if (arm != null)
                {
                    kLowerTorso = arm.Find("LowerTorsoBone");
                    kUpperTorso = kLowerTorso != null ? kLowerTorso.Find("UpperTorsoBone") : null;
                    kClavicle = kUpperTorso != null ? kUpperTorso.Find("ClavicleBone") : null;
                    kNeck = kClavicle != null ? kClavicle.Find("NeckBone") : null;

                    if (kUpperTorso != null)
                    {
                        Transform sL = kUpperTorso.Find("ShoulderBone.L");
                        kBicepL = sL != null ? sL.Find("BicepBone.L") : null;
                        kForearmL = kBicepL != null ? kBicepL.Find("ForearmBone.L") : null;
                        kHandL = kForearmL != null ? kForearmL.Find("HandBone.L") : null;

                        Transform sR = kUpperTorso.Find("ShoulderBone.R");
                        kBicepR = sR != null ? sR.Find("BicepBone.R") : null;
                        kForearmR = kBicepR != null ? kBicepR.Find("ForearmBone.R") : null;
                        kHandR = kForearmR != null ? kForearmR.Find("HandBone.R") : null;
                    }

                    if (kLowerTorso != null)
                    {
                        kHipL = kLowerTorso.Find("HipBone.L");
                        kUpperLegL = kHipL != null ? kHipL.Find("UpperLegBone.L") : null;
                        kLowerLegL = kUpperLegL != null ? kUpperLegL.Find("LowerLegBone.L") : null;
                        kAnkleL = kLowerLegL != null ? kLowerLegL.Find("AnkleBone.L") : null;

                        kHipR = kLowerTorso.Find("HipBone.R");
                        kUpperLegR = kHipR != null ? kHipR.Find("UpperLegBone.R") : null;
                        kLowerLegR = kUpperLegR != null ? kUpperLegR.Find("LowerLegBone.R") : null;
                        kAnkleR = kLowerLegR != null ? kLowerLegR.Find("AnkleBone.R") : null;
                    }
                }
            }
        }

        private void Update()
        {
            if (!isCutsceneRunning || hardCutCommitted) return;

            if (uiRig != null && uiRig.SkipText != null && timelineElapsed > 1.0f && !skipRequested)
            {
                Color sc = uiRig.SkipText.color;
                sc.a = Mathf.MoveTowards(sc.a, 0.72f, Time.unscaledDeltaTime * 1.2f);
                uiRig.SkipText.color = sc;
            }

            if (!skipRequested && timelineElapsed > 0.45f && HarborToNeonCutsceneShared.IsSkipPressed())
            {
                skipRequested = true;
                StartCoroutine(SkipToNeonCombatRoutine());
            }
        }

        private void LockActorsForCutscene()
        {
            if (jaeger != null)
            {
                jaeger.isCutsceneActive = true;
                jaeger.enabled = false;
            }
            if (jaegerRb != null)
            {
                jaegerRb.linearVelocity = Vector3.zero;
                jaegerRb.angularVelocity = Vector3.zero;
                jaegerRb.isKinematic = true;
                jaegerRb.useGravity = false;
            }
            if (kaiju != null)
            {
                kaiju.SetCutscenePose(kaiju.transform.eulerAngles.y);
                kaiju.enabled = false;
            }
            if (kaijuRb != null)
            {
                kaijuRb.linearVelocity = Vector3.zero;
                kaijuRb.angularVelocity = Vector3.zero;
                kaijuRb.isKinematic = true;
                kaijuRb.useGravity = false;
            }
            if (camFollow != null) camFollow.enabled = false;
            if (JaegerHUD.Instance != null) JaegerHUD.Instance.SetCutsceneHidden(true);

            // Prevent physics collider separation during close-quarters bear hug & spin
            if (jaeger != null && kaiju != null)
            {
                Collider jc = jaeger.GetComponent<Collider>();
                Collider kc = kaiju.GetComponent<Collider>();
                if (jc != null && kc != null) Physics.IgnoreCollision(jc, kc, true);
            }
        }

        private void SetupRuntimeVFX()
        {
            vfxRoot = new GameObject("HarborCutscene_VFX");
            if (kaiju != null)
            {
                bowSprayLeft = HarborToNeonCutsceneShared.CreateWaterSprayWallEmitter("BowSpray_L", kaiju.transform);
                bowSprayLeft.transform.localPosition = new Vector3(-0.22f, 0.25f, 0.26f);
                bowSprayLeft.transform.localRotation = Quaternion.Euler(-48f, -52f, 0f);

                bowSprayRight = HarborToNeonCutsceneShared.CreateWaterSprayWallEmitter("BowSpray_R", kaiju.transform);
                bowSprayRight.transform.localPosition = new Vector3(0.22f, 0.25f, 0.26f);
                bowSprayRight.transform.localRotation = Quaternion.Euler(-48f, 52f, 0f);

                vortexSprayLeft = HarborToNeonCutsceneShared.CreateWaterSprayWallEmitter("VortexSpray_L", vfxRoot.transform, 20f, 44f);
                vortexSprayRight = HarborToNeonCutsceneShared.CreateWaterSprayWallEmitter("VortexSpray_R", vfxRoot.transform, 20f, 44f);
            }

            GameObject lightGo = new GameObject("StormLightningFlashLight");
            lightGo.transform.SetParent(vfxRoot.transform, false);
            lightGo.transform.position = new Vector3(-18f, 95f, 35f);
            lightningFlashLight = lightGo.AddComponent<Light>();
            lightningFlashLight.type = LightType.Point;
            lightningFlashLight.range = 420f;
            lightningFlashLight.color = new Color(0.68f, 0.85f, 1f);
            lightningFlashLight.intensity = 0f;
        }

        private IEnumerator RunPart1Routine()
        {
            LockActorsForCutscene();
            uiRig = HarborToNeonCutsceneShared.CreateLetterboxAndFadeUI("HarborToNeon_Part1UI", 0.115f, 0f);
            SetupRuntimeVFX();

            var audio = PacificRimAudioManager.Instance;
            if (audio != null)
            {
                audio.PlayKaijuStaggerPain(kaiju != null ? kaiju.transform.position : Vector3.zero);
                audio.PlayAtPosition("Heavy Object Impact 4", kaiju != null ? kaiju.transform.position : Vector3.zero, 0.85f);
            }

            // Short dramatic combat-to-black beat (0.42s) before 0:00 fade in
            float preFade = 0f;
            while (preFade < 0.42f && !skipRequested)
            {
                preFade += Time.unscaledDeltaTime;
                SetBlackAlpha(Mathf.Clamp01(preFade / 0.42f));
                yield return null;
            }
            if (skipRequested) yield break;
            SetBlackAlpha(1f);

            // Start asynchronous background preload of Hong Kong Neon Highway right at 0:00
            preloadOp = SceneManager.LoadSceneAsync(HarborToNeonTransitionState.NeonSceneName, LoadSceneMode.Single);
            if (preloadOp != null)
            {
                preloadOp.allowSceneActivation = false;
                preloadOp.priority = 1;
            }

            // Stage Waist-Deep Harbor Water & Actors
            if (waterSystem != null)
            {
                origOceanPos = waterSystem.transform.position;
                adjustedOcean = true;
                waterSystem.transform.position = new Vector3(origOceanPos.x, 11.45f, origOceanPos.z);
            }

            if (jaeger != null)
            {
                jaeger.transform.position = GipsyHarborStartPos;
                jaeger.transform.rotation = Quaternion.Euler(0f, GipsyHarborStartYaw, 0f);
            }
            if (jaegerAnim != null)
            {
                jaegerAnim.speed = 1f;
                jaegerAnim.SetBool("InCombat", true);
                jaegerAnim.SetBool("IsRunning", false);
                jaegerAnim.SetFloat("Speed", 0f);
                jaegerAnim.CrossFade("CombatIdle", 0.05f);
            }

            if (kaiju != null)
            {
                kaiju.transform.position = KaijuChargeStartPos;
                Vector3 toGipsy = (GipsyHarborStartPos - KaijuChargeStartPos);
                toGipsy.y = 0f;
                kaiju.transform.rotation = Quaternion.LookRotation(toGipsy.normalized, Vector3.up);
            }
            if (kaijuAnim != null)
            {
                kaijuAnim.speed = 1.05f;
                kaijuAnim.SetBool("IsMoving", false);
                kaijuAnim.SetFloat("LocomotionSpeed", 0f);
                kaijuAnim.CrossFade("EMP", 0.05f);
            }

            // =========================================================================
            // SHOT 1 (0:00 - 0:06): THE CHARGE
            // =========================================================================
            activeShot = 1;
            timelineElapsed = 0f;
            float nextStepSprayTime = 1.45f;
            bool startedRun = false;
            bool roarTriggered = false;
            bool lightningShot1 = false;

            while (timelineElapsed < 6.00f && !skipRequested)
            {
                float dt = Mathf.Min(Time.deltaTime, 0.04f);
                timelineElapsed = Mathf.Min(6.00f, timelineElapsed + dt);
                float t = timelineElapsed;

                // Fade in cleanly 0:00 -> 0:85
                float blackAlpha = t < 0.85f ? Mathf.SmoothStep(1f, 0f, t / 0.85f) : 0f;
                SetBlackAlpha(blackAlpha);

                if (!roarTriggered && t >= 0.12f)
                {
                    roarTriggered = true;
                    audio?.PlayKaijuEmpRoar(kaiju != null ? kaiju.transform.position : KaijuChargeStartPos);
                    audio?.PlayEmpChargeUp(kaiju != null ? kaiju.transform.position : KaijuChargeStartPos);
                    customShakeStrength = 0.25f;
                }

                // Transition from EMP Roar into menacing fast-Walk charge (avoids goofy Sprint animation)
                if (!startedRun && t >= 1.35f)
                {
                    startedRun = true;
                    if (kaijuAnim != null)
                    {
                        kaijuAnim.speed = 1.32f;
                        kaijuAnim.SetBool("IsMoving", true);
                        kaijuAnim.SetFloat("LocomotionSpeed", 1.75f);
                        kaijuAnim.CrossFade("Walk", 0.14f);
                    }
                    audio?.PlayKaijuAttackGrunt(kaiju != null ? kaiju.transform.position : KaijuChargeStartPos);
                    SetSprayEmission(bowSprayLeft, 95f);
                    SetSprayEmission(bowSprayRight, 95f);
                    bowSprayLeft?.Play();
                    bowSprayRight?.Play();
                }

                // Sprint forward through deep harbor water
                if (kaiju != null && t >= 1.35f)
                {
                    float sprintU = Mathf.Clamp01((t - 1.35f) / (6.00f - 1.35f));
                    // Accelerate menacingly into collision
                    float curvedU = Mathf.Lerp(sprintU, sprintU * sprintU * 0.35f + sprintU * 0.65f, 0.55f);
                    Vector3 kPos = Vector3.Lerp(KaijuChargeStartPos, KaijuImpactTargetPos, curvedU);
                    float strideBob = Mathf.Abs(Mathf.Sin(t * 9.5f)) * 0.45f;
                    kaiju.transform.position = kPos + Vector3.up * strideBob;

                    Vector3 lookDir = GipsyHarborStartPos - kPos;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.01f)
                        kaiju.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

                    if (t >= nextStepSprayTime)
                    {
                        nextStepSprayTime = t + 0.31f;
                        Vector3 waterPos = new Vector3(kPos.x, 11.45f, kPos.z);
                        Vector3 fwd = kaiju.transform.forward;
                        Vector3 right = kaiju.transform.right;
                        splashManager?.SpawnHeavyMoveWaterKickup(waterPos + right * 4.5f, fwd + right * 0.6f, 1.85f, true);
                        splashManager?.SpawnHeavyMoveWaterKickup(waterPos - right * 4.5f, fwd - right * 0.6f, 1.85f, true);
                        splashManager?.SpawnFootstepSplash(waterPos + fwd * 4.2f, 2.2f);
                        waterSystem?.AddFootstepWave(waterPos, 24f, 3.6f);
                        audio?.PlayOceanFootstep(kPos, 1.35f);
                        customShakeStrength = Mathf.Max(customShakeStrength, 0.14f + sprintU * 0.24f);
                    }
                }

                // Storm lightning flash mid-charge
                if (!lightningShot1 && t >= 3.35f)
                {
                    lightningShot1 = true;
                    StartCoroutine(PulseLightning(0.45f, 4500f));
                    audio?.PlayLightningStrike(new Vector3(-12f, 35f, 40f), 0.75f);
                }

                // Camera: Gipsy left-third (~0.31), three-quarter rear/side waist-deep, Leatherback center-frame charging in!
                float camU = Mathf.Clamp01(t / 6.0f);
                baseCamPos = Vector3.Lerp(new Vector3(15.5f, 15.2f, -21.0f), new Vector3(14.2f, 14.8f, -18.2f), camU);
                Vector3 camTarget = Vector3.Lerp(new Vector3(-5.0f, 14.5f, 32.0f), new Vector3(-2.6f, 14.6f, 20.0f), camU);
                baseCamRot = Quaternion.LookRotation((camTarget - baseCamPos).normalized, Vector3.up);
                baseCamFov = Mathf.Lerp(44f, 40.5f, camU);

                customShakeStrength = Mathf.MoveTowards(customShakeStrength, 0.05f, dt * 0.6f);
                yield return null;
            }
            if (skipRequested) yield break;

            // =========================================================================
            // SHOT 2 (0:06 - 0:13): BEAR HUG & 360-DEGREE HAMMER THROW
            // =========================================================================
            activeShot = 2;
            SetSprayEmission(bowSprayLeft, 0f);
            SetSprayEmission(bowSprayRight, 0f);

            if (kaijuAnim != null)
            {
                kaijuAnim.speed = 0f;
                kaijuAnim.Play("Idle", 0, 0f);
                kaijuAnim.Update(0f);
            }
            if (jaegerAnim != null)
            {
                jaegerAnim.speed = 0.75f;
                jaegerAnim.SetTrigger("HitLeft");
            }

            // Bear hug collision impact beat: spins >360 deg and releases aiming +90 (+X = RIGHT toward Victoria Harbour!)
            Vector3 hugPivot = new Vector3(-0.6f, 2.98f, 1.5f);
            float initialKaijuYaw = kaiju != null ? kaiju.transform.eulerAngles.y : 168f;
            float targetReleaseYaw = 90f; // +X = RIGHT side of bay = Victoria Harbour waterfront!
            float cwDelta = Mathf.DeltaAngle(initialKaijuYaw, targetReleaseYaw);
            float releaseSpinDeltaDeg = cwDelta <= 0f ? cwDelta - 360f : cwDelta - 360f; // Full 360+ spin ending exactly at +90 (+X)!
            bool impactPlayed = false;
            bool gruntPlayed = false;
            bool vortexStarted = false;
            bool releaseTriggered = false;
            bool lensSplashTriggered = false;
            float nextVortexRingTime = 7.05f;

            Vector3 throwOrigin = hugPivot + Vector3.up * 2.4f;

            while (timelineElapsed < 11.20f && !skipRequested)
            {
                float dt = Mathf.Min(Time.deltaTime, 0.04f);
                timelineElapsed = Mathf.Min(11.20f, timelineElapsed + dt);
                float t = timelineElapsed;

                // Impact punch + flash right as chest locks
                if (!impactPlayed && t >= 6.22f)
                {
                    impactPlayed = true;
                    audio?.PlayKaijuSlamImpact(hugPivot);
                    audio?.PlayAtPosition("Heavy Object Impact 4", hugPivot, 1.0f);
                    audio?.PlayArmorCreak(hugPivot, 0.9f);
                    audio?.PlayHydraulicServo(hugPivot, 0.75f);
                    splashManager?.SpawnColossalTitanDropSplash(new Vector3(hugPivot.x, 11.45f, hugPivot.z));
                    splashManager?.SpawnCombatSparks(hugPivot + Vector3.up * 13.5f, Vector3.up, false, true, false);
                    camShake?.Shake(1.05f, 0.45f);
                    StartCoroutine(PulseImpactFlash(new Color(1f, 0.72f, 0.45f, 0.28f), 0.16f));
                    StartCoroutine(PulseLightning(0.32f, 3800f));
                }

                // Ramp Bear-Hug 2-bone IK lock (6.05 -> 6.55)
                hugIkWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 6.05f) / 0.50f));

                float grabU = Mathf.Clamp01((t - 6.00f) / 0.85f);
                spinNormalized = Mathf.Clamp01((t - 6.85f) / (11.20f - 6.85f));
                float easedSpin = Mathf.Pow(spinNormalized, 1.55f);

                if (!gruntPlayed && t >= 7.05f)
                {
                    gruntPlayed = true;
                    audio?.PlayKaijuAttackGrunt(hugPivot);
                    audio?.PlayAtPosition("Hydraulic piston mechanical whine", hugPivot, 0.8f);
                    audio?.PlayArmorCreak(hugPivot, 0.85f);
                }

                if (!vortexStarted && t >= 6.95f)
                {
                    vortexStarted = true;
                    SetSprayEmission(vortexSprayLeft, 150f);
                    SetSprayEmission(vortexSprayRight, 150f);
                    vortexSprayLeft?.Play();
                    vortexSprayRight?.Play();
                }

                float currentYaw = initialKaijuYaw + easedSpin * releaseSpinDeltaDeg;
                float kaijuDrop = Mathf.Lerp(0f, 0.45f, spinNormalized);
                if (kaiju != null)
                {
                    kaiju.transform.position = new Vector3(hugPivot.x, 3.09f - kaijuDrop, hugPivot.z);
                    kaiju.transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
                }

                // Keep Gipsy pinned inside Leatherback's chest bear hug (3.6 -> 4.8m) so giant arms stay coiled snugly around Gipsy's waist!
                float hoistY = Mathf.Lerp(1.6f, 3.2f, grabU) + Mathf.Lerp(0f, 1.8f, spinNormalized);
                float swingRadius = Mathf.Lerp(4.2f, 5.2f, easedSpin);
                centrifugalPitchDeg = Mathf.Lerp(8f, 44f, Mathf.SmoothStep(0f, 1f, spinNormalized));

                if (jaeger != null && kaiju != null)
                {
                    Vector3 fwd = kaiju.transform.forward;
                    Vector3 gPos = kaiju.transform.position + fwd * swingRadius + Vector3.up * hoistY;
                    jaeger.transform.position = gPos;
                    jaeger.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up) *
                                               Quaternion.Euler(Mathf.Lerp(-4f, -centrifugalPitchDeg, Mathf.Clamp01(spinNormalized * 1.1f)), 0f, 0f);
                }

                Vector3 tangL = Quaternion.Euler(0f, currentYaw - 90f, 0f) * Vector3.forward;
                Vector3 tangR = -tangL;
                if (vortexSprayLeft != null)
                {
                    vortexSprayLeft.transform.position = new Vector3(hugPivot.x, 11.45f, hugPivot.z) + tangL * 5.2f;
                    vortexSprayLeft.transform.rotation = Quaternion.LookRotation((tangL * 0.7f + Vector3.up * 0.6f).normalized, Vector3.up);
                }
                if (vortexSprayRight != null)
                {
                    vortexSprayRight.transform.position = new Vector3(hugPivot.x, 11.45f, hugPivot.z) + tangR * 5.2f;
                    vortexSprayRight.transform.rotation = Quaternion.LookRotation((tangR * 0.7f + Vector3.up * 0.6f).normalized, Vector3.up);
                }

                if (vortexStarted && t >= nextVortexRingTime)
                {
                    nextVortexRingTime = t + Mathf.Lerp(0.32f, 0.14f, spinNormalized);
                    float ringAng = currentYaw + UnityEngine.Random.Range(0f, 360f);
                    Vector3 outDir = Quaternion.Euler(0f, ringAng, 0f) * Vector3.forward;
                    Vector3 wPos = new Vector3(hugPivot.x, 11.45f, hugPivot.z) + outDir * UnityEngine.Random.Range(5.5f, 9.5f);
                    splashManager?.SpawnHeavyMoveWaterKickup(wPos, outDir, 1.55f + spinNormalized * 0.9f, true);
                    waterSystem?.AddFootstepWave(wPos, 22f, 3.2f);
                    audio?.PlayOceanFootstep(wPos, 0.85f + spinNormalized * 0.5f);
                    customShakeStrength = Mathf.Max(customShakeStrength, 0.15f + spinNormalized * 0.35f);
                }

                if (!lensSplashTriggered && t >= 9.4f)
                {
                    lensSplashTriggered = true;
                    mainCam?.GetComponent<PacificRimAtmosphere.CameraLensRainController>()?.TriggerLensWaterSplash(1.4f);
                }

                throwOrigin = jaeger != null ? jaeger.transform.position : hugPivot + Vector3.up * 5f;

                // Camera from South bay (-Z) looking North so Harbor skyline (+X) sits on the RIGHT side of frame!
                Vector3 gCenter = jaeger != null ? jaeger.transform.position + Vector3.up * 11.0f : hugPivot + Vector3.up * 11f;
                Vector3 kCenter = kaiju != null ? kaiju.transform.position + Vector3.up * 12.0f : hugPivot + Vector3.up * 12f;
                Vector3 mid = (gCenter + kCenter) * 0.5f;

                Vector3 desiredPos = Vector3.Lerp(new Vector3(-28f, 20.5f, -44f), new Vector3(14f, 21.5f, -48f), spinNormalized);
                Vector3 lookSpot = mid + new Vector3(8f * spinNormalized, 1.5f, 3f);
                baseCamPos = t < 6.05f ? desiredPos : Vector3.Lerp(baseCamPos, desiredPos, 1f - Mathf.Exp(-dt * 6.5f));
                baseCamRot = Quaternion.LookRotation((lookSpot - baseCamPos).normalized, Vector3.up);
                baseCamFov = Mathf.Lerp(50f, 54f, spinNormalized);
                customShakeStrength = Mathf.MoveTowards(customShakeStrength, 0.04f, dt * 0.5f);
                yield return null;
            }
            if (skipRequested) yield break;

            // Unconditional release burst at 11.20 -> 13.00 throwing Gipsy RIGHTWARD (+X) toward the Harbor!
            while (timelineElapsed < 13.00f && !skipRequested)
            {
                float dt = Mathf.Min(Time.deltaTime, 0.04f);
                timelineElapsed = Mathf.Min(13.00f, timelineElapsed + dt);
                float t = timelineElapsed;
                float airT = t - 11.20f;

                if (!releaseTriggered)
                {
                    releaseTriggered = true;
                    hugIkWeight = 0f;
                    SetSprayEmission(vortexSprayLeft, 30f);
                    SetSprayEmission(vortexSprayRight, 30f);

                    audio?.PlayKaijuEmpRoar(hugPivot);
                    audio?.PlayAttackWhoosh(throwOrigin, true);
                    audio?.PlayAtPosition("Heavy Object Impact 4", hugPivot, 0.95f);
                    splashManager?.SpawnColossalTitanDropSplash(new Vector3(hugPivot.x, 11.45f, hugPivot.z));
                    splashManager?.SpawnColossalTitanDropSplash(new Vector3(hugPivot.x, 11.45f, hugPivot.z) + Vector3.right * 8f);
                    camShake?.Shake(0.9f, 0.4f);
                    StartCoroutine(PulseImpactFlash(new Color(0.7f, 0.9f, 1f, 0.22f), 0.14f));

                    if (kaijuAnim != null)
                    {
                        kaijuAnim.speed = 1.0f;
                        kaijuAnim.CrossFade("EMP", 0.08f);
                    }
                    if (jaegerAnim != null)
                    {
                        jaegerAnim.enabled = true;
                        jaegerAnim.Play("CombatIdle", 0, 0f);
                        jaegerAnim.Update(0f);
                        jaegerAnim.speed = 0f;
                    }

                    // Throw directly RIGHTWARD (+X) over the Harbor bay!
                    Vector3 launchDir = new Vector3(0.97f, 0f, 0.24f).normalized;
                    throwVelocityWorld = launchDir * 66f + Vector3.up * 22f;
                }

                airborneBlend = Mathf.Clamp01(airT / 0.35f);
                Vector3 gravity = new Vector3(0f, -12.5f, 0f);
                Vector3 curPos = throwOrigin + throwVelocityWorld * airT + 0.5f * gravity * (airT * airT);
                Vector3 curVel = throwVelocityWorld + gravity * airT;
                if (jaeger != null)
                {
                    Vector3 rollUp = Quaternion.AngleAxis(airT * 24f, curVel.normalized) * Vector3.up;
                    Quaternion hRot = Quaternion.LookRotation(curVel.normalized, rollUp) * Quaternion.Euler(90f, 0f, 0f);
                    jaeger.transform.rotation = hRot;
                    jaeger.transform.position = curPos - (hRot * (Vector3.up * 11.2f));
                }

                if (airT < 0.75f && splashManager != null && UnityEngine.Random.value < 0.35f)
                {
                    splashManager.EmitVolumetricMist(curPos, -curVel * 0.12f, 9f, 0.38f, 1.5f);
                }

                float airU = Mathf.Clamp01(airT / 1.80f);
                Vector3 kCenter = kaiju != null ? kaiju.transform.position + Vector3.up * 12f : hugPivot + Vector3.up * 12f;
                Vector3 gCenter = jaeger != null ? curPos : hugPivot + new Vector3(50f, 22f, 12f);
                Vector3 lookFocus = Vector3.Lerp(kCenter, gCenter, Mathf.SmoothStep(0.18f, 0.92f, airU));
                baseCamPos = Vector3.Lerp(baseCamPos, new Vector3(12f, 20.5f, -52f), 1f - Mathf.Exp(-dt * 4.0f));
                baseCamRot = Quaternion.LookRotation((lookFocus - baseCamPos).normalized, Vector3.up);
                baseCamFov = Mathf.Lerp(54f, 52f, airU);
                customShakeStrength = Mathf.MoveTowards(customShakeStrength, 0.04f, dt * 0.5f);
                yield return null;
            }
            if (skipRequested) yield break;

            // =========================================================================
            // 0:13 HARD CUT INTO HONG KONG NEON HIGHWAY
            // =========================================================================
            hardCutCommitted = true;
            HarborToNeonTransitionState.ElapsedAtHandoff = timelineElapsed;

            if (preloadOp != null)
            {
                if (preloadOp.progress < 0.9f)
                {
                    // Brief black safeguard ONLY if slow disk still loading HK scene
                    SetBlackAlpha(1f);
                    while (preloadOp.progress < 0.9f) yield return null;
                }
                HarborToNeonTransitionState.IsPart1Running = false;
                preloadOp.allowSceneActivation = true;
                while (!preloadOp.isDone) yield return null;
            }
            else
            {
                HarborToNeonTransitionState.IsPart1Running = false;
                SceneManager.LoadScene(HarborToNeonTransitionState.NeonSceneName, LoadSceneMode.Single);
            }
        }

        private IEnumerator SkipToNeonCombatRoutine()
        {
            hardCutCommitted = true;
            HarborToNeonTransitionState.PendingPart2Cutscene = false;
            HarborToNeonTransitionState.PendingSkipPart2ToCombat = true;
            HarborToNeonTransitionState.StartNeonCityAtPhase3HalfHp = true;

            var blackOverlay = CutsceneBlackTransitionOverlay.GetOrCreate();
            blackOverlay.SetBlackAlpha(0f);

            float fadeT = 0f;
            while (fadeT < 0.35f)
            {
                fadeT += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(fadeT / 0.35f);
                SetBlackAlpha(a);
                blackOverlay.SetBlackAlpha(a);
                yield return null;
            }
            blackOverlay.SetBlackAlpha(1f);
            SetBlackAlpha(1f);

            HarborToNeonTransitionState.IsPart1Running = false;
            if (preloadOp != null)
            {
                while (preloadOp.progress < 0.9f) yield return null;
                preloadOp.allowSceneActivation = true;
                while (!preloadOp.isDone) yield return null;
            }
            else
            {
                SceneManager.LoadScene(HarborToNeonTransitionState.NeonSceneName, LoadSceneMode.Single);
            }
        }

        private void LateUpdate()
        {
            if (!isCutsceneRunning || activeShot == 0) return;

            // Shot 1 slight head track by Gipsy toward oncoming Leatherback
            if (activeShot == 1 && jHead != null && kaiju != null)
            {
                Vector3 toK = (kaiju.transform.position + Vector3.up * 10f) - jHead.position;
                if (toK.sqrMagnitude > 1f)
                {
                    Quaternion aim = Quaternion.LookRotation(toK.normalized, Vector3.up);
                    jHead.rotation = Quaternion.Slerp(jHead.rotation, aim, 0.45f);
                }
            }

            // Shot 2 Bear-Hug 2-Bone IK + Spin Lean + Release Airborne Limb Trail
            if (activeShot == 2)
            {
                if (hugIkWeight > 0.005f && kaiju != null && jaeger != null && jHips != null && jChest != null)
                {
                    if (kaijuAnim != null)
                    {
                        kaijuAnim.Play("Idle", 0, 0f);
                        kaijuAnim.Update(0f);
                    }
                    // Bend Leatherback knees into low hammer-throw anchor stance
                    float bend = Mathf.Lerp(8f, 34f, spinNormalized);
                    if (kUpperLegL != null) kUpperLegL.localRotation *= Quaternion.Euler(-bend, 0f, -8f);
                    if (kLowerLegL != null) kLowerLegL.localRotation *= Quaternion.Euler(bend * 1.65f, 0f, 0f);
                    if (kUpperLegR != null) kUpperLegR.localRotation *= Quaternion.Euler(-bend, 0f, 8f);
                    if (kLowerLegR != null) kLowerLegR.localRotation *= Quaternion.Euler(bend * 1.65f, 0f, 0f);
                    if (kUpperTorso != null) kUpperTorso.localRotation *= Quaternion.Euler(-bend * 0.45f, 0f, 0f);

                    Vector3 kCenter = kaiju.transform.position + Vector3.up * 10f;
                    Vector3 gRight = jHips.right;
                    Vector3 gFwd = jHips.forward;
                    Vector3 waistPos = jHips.position;

                    // Leatherback wraps BOTH colossal arms completely around Gipsy's waist behind Gipsy's back!
                    Vector3 targetHandL = waistPos - gFwd * 2.2f + gRight * 0.65f;
                    Vector3 targetHandR = waistPos - gFwd * 2.2f - gRight * 0.65f;
                    Vector3 poleL = (kBicepL != null ? kBicepL.position : kCenter) - kaiju.transform.right * 11f + kaiju.transform.forward * 6f;
                    Vector3 poleR = (kBicepR != null ? kBicepR.position : kCenter) + kaiju.transform.right * 11f + kaiju.transform.forward * 6f;

                    HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(kBicepL, kForearmL, kHandL, targetHandL, poleL, hugIkWeight);
                    HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(kBicepR, kForearmR, kHandR, targetHandR, poleR, hugIkWeight);

                    // Gipsy pushes down/clamps onto Leatherback's shoulders
                    Vector3 kShoulderL = kBicepL != null ? kBicepL.position : kCenter - kaiju.transform.right * 4.5f;
                    Vector3 kShoulderR = kBicepR != null ? kBicepR.position : kCenter + kaiju.transform.right * 4.5f;
                    HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(
                        jUpperArmL, jForearmL, jHandL, kShoulderR + Vector3.up * 1.4f,
                        jChest.position - jaeger.transform.right * 7f, hugIkWeight * 0.88f);
                    HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(
                        jUpperArmR, jForearmR, jHandR, kShoulderL + Vector3.up * 1.4f,
                        jChest.position + jaeger.transform.right * 7f, hugIkWeight * 0.88f);

                    // Gipsy legs kick & swing outward dynamically via world 2-bone IK during bear-hug spin
                    float kickWave = Mathf.Sin(timelineElapsed * 9.5f) * (1.2f + spinNormalized * 1.4f);
                    Vector3 outSpinDir = kaiju.transform.forward;
                    Vector3 jUp = jaeger.transform.up;
                    Vector3 footTargetL = waistPos - jUp * 9.2f + outSpinDir * (1.5f + spinNormalized * 4.5f) - jaeger.transform.right * (1.8f + kickWave * 0.5f);
                    Vector3 footTargetR = waistPos - jUp * 9.2f + outSpinDir * (1.5f + spinNormalized * 4.5f) + jaeger.transform.right * (1.8f - kickWave * 0.5f);
                    HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(
                        jThighL, jShinL, jFootL, footTargetL,
                        waistPos - jUp * 4.5f + jaeger.transform.forward * 4f - jaeger.transform.right * 2f, hugIkWeight);
                    HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(
                        jThighR, jShinR, jFootR, footTargetR,
                        waistPos - jUp * 4.5f + jaeger.transform.forward * 4f + jaeger.transform.right * 2f, hugIkWeight);
                }
                else if (airborneBlend > 0.01f && jaeger != null && jHips != null)
                {
                    // Keep base CombatIdle leg/arm geometry intact and trail limbs naturally via 2-bone IK (no folded bones!)
                    if (jaegerAnim != null)
                    {
                        jaegerAnim.Play("CombatIdle", 0, 0f);
                        jaegerAnim.Update(0f);
                    }
                    Vector3 trailDir = throwVelocityWorld.sqrMagnitude > 1f ? -throwVelocityWorld.normalized : Vector3.left;
                    Vector3 sideAxis = jaeger.transform.right;
                    Vector3 downAxis = -jaeger.transform.forward;
                    float fl = Mathf.Sin(timelineElapsed * 12f) * 1.1f;

                    Vector3 hipPos = jHips.position;
                    HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(
                        jThighL, jShinL, jFootL,
                        hipPos + trailDir * 10.6f - sideAxis * 1.6f + downAxis * (0.9f + fl),
                        hipPos + trailDir * 5.0f - sideAxis * 2.0f - downAxis * 2.5f, 1f);
                    HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(
                        jThighR, jShinR, jFootR,
                        hipPos + trailDir * 10.4f + sideAxis * 1.6f + downAxis * (0.9f - fl),
                        hipPos + trailDir * 5.0f + sideAxis * 2.0f - downAxis * 2.5f, 1f);

                    Vector3 chestPos = jChest != null ? jChest.position : hipPos + Vector3.up * 3f;
                    HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(
                        jUpperArmL, jForearmL, jHandL,
                        chestPos + trailDir * 6.0f - sideAxis * 3.6f - downAxis * fl,
                        chestPos + trailDir * 3.0f - sideAxis * 6.5f, 1f);
                    HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(
                        jUpperArmR, jForearmR, jHandR,
                        chestPos + trailDir * 6.0f + sideAxis * 3.6f + downAxis * fl,
                        chestPos + trailDir * 3.0f + sideAxis * 6.5f, 1f);
                }
            }

            if (mainCam != null)
            {
                Vector3 handShake = Vector3.zero;
                if (customShakeStrength > 0.005f)
                {
                    float nx = (Mathf.PerlinNoise(Time.time * 24f, 1.7f) - 0.5f) * 2f;
                    float ny = (Mathf.PerlinNoise(3.9f, Time.time * 24f) - 0.5f) * 2f;
                    handShake = (mainCam.transform.right * nx + mainCam.transform.up * ny) * customShakeStrength;
                }
                Vector3 shakeOffset = (camShake != null ? camShake.CurrentOffset : Vector3.zero) + handShake;
                Quaternion shakeRot = camShake != null ? Quaternion.Euler(camShake.CurrentRotationOffset) : Quaternion.identity;

                mainCam.transform.position = baseCamPos + shakeOffset;
                mainCam.transform.rotation = baseCamRot * shakeRot;
                mainCam.fieldOfView = baseCamFov;
                HarborToNeonCutsceneShared.SyncCameraRain(mainCam);
            }
        }

        private void SetBlackAlpha(float alpha)
        {
            if (uiRig == null || uiRig.FadeOverlay == null) return;
            Color c = uiRig.FadeOverlay.color;
            c.a = Mathf.Clamp01(alpha);
            uiRig.FadeOverlay.color = c;
        }

        private void SetSprayEmission(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.rateOverTime = rate;
        }

        private IEnumerator PulseImpactFlash(Color baseCol, float duration)
        {
            if (uiRig == null || uiRig.FlashOverlay == null) yield break;
            float e = 0f;
            while (e < duration)
            {
                e += Time.unscaledDeltaTime;
                Color c = baseCol;
                c.a = Mathf.Lerp(baseCol.a, 0f, e / duration);
                uiRig.FlashOverlay.color = c;
                yield return null;
            }
            uiRig.FlashOverlay.color = new Color(baseCol.r, baseCol.g, baseCol.b, 0f);
        }

        private IEnumerator PulseLightning(float duration, float peakIntensity)
        {
            if (lightningFlashLight == null) yield break;
            float e = 0f;
            while (e < duration)
            {
                e += Time.deltaTime;
                float u = e / duration;
                float env = u < 0.25f ? Mathf.Sin((u / 0.25f) * Mathf.PI) : Mathf.Pow(1f - u, 2f) * (UnityEngine.Random.value > 0.35f ? 1f : 0.25f);
                lightningFlashLight.intensity = peakIntensity * Mathf.Clamp01(env);
                yield return null;
            }
            lightningFlashLight.intensity = 0f;
        }
    }
}
