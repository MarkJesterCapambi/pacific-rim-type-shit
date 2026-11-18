using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PacificRimAudio;
using PacificRimUI;
using PacificRimWater;

namespace PacificRimCutscene
{
    /// <summary>
    /// Part 2 (0:13 - 0:30) of the Harbor -> Hong Kong Neon Phase 3 transition cutscene,
    /// plus Phase 3 half-HP gameplay handoff and restart preservation:
    /// - Shot 3 (0:13-0:18): Dynamic low-angle airborne tracking shot (Gipsy horizontal, limbs trailing).
    /// - Shot 4 (0:18-0:24): Wide street-level heavy ground impact, rotating skid crushing containers
    ///   with orange sparks & camera shake, catching himself on one hand and knee.
    /// - Shot 5 (0:24-0:30): Orbiting 3/4 front/side push-up with shoulder steam vents while Leatherback
    ///   sprints through harbor water and leaps onto Harbour_WestPier_Deck, fading to black for Phase 3 Half-HP combat.
    /// </summary>
    [DefaultExecutionOrder(600)]
    public class NeonCityThrowCutscene : MonoBehaviour
    {
        public static NeonCityThrowCutscene Instance { get; private set; }

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

        // Jaeger bones
        private Transform jRoot, jHips, jSpine, jChest, jHead;
        private Transform jUpperArmL, jForearmL, jHandL;
        private Transform jUpperArmR, jForearmR, jHandR;
        private Transform jThighL, jShinL, jFootL;
        private Transform jThighR, jShinR, jFootR;

        // Kaiju bones
        private Transform kLowerTorso, kUpperTorso;
        private Transform kHipL, kUpperLegL, kLowerLegL;
        private Transform kHipR, kUpperLegR, kLowerLegR;

        // Environment & VFX references
        private Collider westPierCollider;
        private GameObject barrierSouthObj;
        private bool barrierSouthWasActive;
        private HarborToNeonCutsceneShared.LetterboxRig uiRig;
        private GameObject cutsceneRoot;
        private Transform deckBouncePlane;
        private ParticleSystem skidSparksPrimary;
        private ParticleSystem skidSparksSecondary;
        private ParticleSystem skidRainSpray;
        private ParticleSystem ventSteamLeft;
        private ParticleSystem ventSteamRight;
        private Light lightningLight;
        private Light skidSparkGlowLight;

        private struct CrushStack
        {
            public float TriggerZ;
            public bool Triggered;
            public Vector3 Center;
            public List<Rigidbody> Bodies;
            public List<Transform> Dents;
        }
        private readonly List<CrushStack> crushStacks = new List<CrushStack>();

        // State flags & timeline pose variables
        private bool skipRequested;
        private bool completedHandoff;
        private float timelineElapsed = 13.0f;
        private int activeShot; // 3 = Airborne, 4 = Impact/Skid/Catch, 5 = Recovery/Onslaught

        // Procedural Pose Drivers evaluated in LateUpdate
        private bool forceAirbornePose;
        private bool forceContinuousDriftPose;
        private float driftRollToKneel;    // 0 = horizontal impact, 1 = continuous kneeling drift
        private float catchCrouchWeight;   // 1 = full hand-and-knee kneeling crouch, 0 = CombatIdle standing
        private float kaijuJumpTuckWeight; // 0..1 during pier leap
        private Vector3 airborneVelWorld = Vector3.forward;
        private Vector3 jaegerCoMTargetWorld;
        private Vector3 jaegerGroundAnchorWorld;
        private Quaternion jaegerHorizontalRot = Quaternion.identity;
        private Quaternion jaegerDriftUprightRot = Quaternion.identity;

        private float customShakeStrength;
        private Vector3 baseCamPos;
        private Quaternion baseCamRot = Quaternion.identity;
        private float baseCamFov = 46f;

        // Harbour_WestPier_Deck exact constants (top Y = 8.60, seafront rim Z = -144.89)
        private const float PierTopY = 8.60f;
        private const float JaegerSoleOffsetY = 2.98f;
        private const float KaijuSoleOffsetY = 2.00f;
        private const float JaegerStandingY = PierTopY + JaegerSoleOffsetY; // 11.58f
        private const float KaijuDeckStandingY = PierTopY + KaijuSoleOffsetY; // 10.60f
        private const float PierSkidX = -101.5f;

        private static readonly Vector3 FinalJaegerCombatPos = new Vector3(-103.5f, JaegerStandingY, -10.5f);
        private static readonly Vector3 FinalKaijuCombatPos = new Vector3(-97.5f, KaijuDeckStandingY, -88.0f);

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
            HarborToNeonTransitionState.IsPart2Running = false;
            RestoreTemporaryWorldState();
            if (cutsceneRoot != null) Destroy(cutsceneRoot);
            if (uiRig != null && uiRig.Canvas != null) Destroy(uiRig.Canvas.gameObject);
        }

        private void Start()
        {
            FindSceneDependencies();
            EnsureHarborWestPierPhysics();

            // Case A: Normal Phase 3 transition cutscene continuing from Part 1 (0:13 -> 0:30)
            if (HarborToNeonTransitionState.PendingPart2Cutscene)
            {
                HarborToNeonTransitionState.PendingPart2Cutscene = false;
                BeginPart2Cutscene();
                return;
            }

            // Case B: Skipped during Part 1 -> snap cleanly to Phase 3 Half-HP combat
            if (HarborToNeonTransitionState.PendingSkipPart2ToCombat)
            {
                HarborToNeonTransitionState.PendingSkipPart2ToCombat = false;
                StartCoroutine(ImmediatePhase3HalfHpSetupRoutine(fadeFromBlack: true));
                return;
            }

            // Case C: Player restarted HK Neon Highway during the Phase 3 campaign transition
            if (HarborToNeonTransitionState.StartNeonCityAtPhase3HalfHp)
            {
                StartCoroutine(ImmediatePhase3HalfHpSetupRoutine(fadeFromBlack: false));
            }
        }

        private void FindSceneDependencies()
        {
            jaeger = SimpleJaegerController.Instance != null ? SimpleJaegerController.Instance : FindAnyObjectByType<SimpleJaegerController>();
            kaiju = FindAnyObjectByType<KaijuAI>();
            mainCam = Camera.main;
            camFollow = mainCam != null ? mainCam.GetComponent<JaegerCameraFollow>() : FindAnyObjectByType<JaegerCameraFollow>();
            camShake = mainCam != null ? mainCam.GetComponent<PacificRimCameraShake>() : null;
            waterSystem = WaterSystem.Instance != null ? WaterSystem.Instance : FindAnyObjectByType<WaterSystem>();
            splashManager = WaterSplashManager.Instance != null ? WaterSplashManager.Instance : FindAnyObjectByType<WaterSplashManager>();
            CacheBones();
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
                    if (kLowerTorso != null)
                    {
                        kHipL = kLowerTorso.Find("HipBone.L");
                        kUpperLegL = kHipL != null ? kHipL.Find("UpperLegBone.L") : null;
                        kLowerLegL = kUpperLegL != null ? kUpperLegL.Find("LowerLegBone.L") : null;

                        kHipR = kLowerTorso.Find("HipBone.R");
                        kUpperLegR = kHipR != null ? kHipR.Find("UpperLegBone.R") : null;
                        kLowerLegR = kUpperLegR != null ? kUpperLegR.Find("LowerLegBone.R") : null;
                    }
                }
            }
        }

        /// <summary>
        /// Ensures Harbour_WestPier_Deck has a solid Ground BoxCollider and is registered as dry concrete
        /// in WaterSystem so neither Gipsy nor Leatherback falls through the pier or emits water splashes on land.
        /// </summary>
        private void EnsureHarborWestPierPhysics()
        {
            GameObject deck = GameObject.Find("Harbour_WestPier_Deck");
            if (deck == null) return;

            westPierCollider = deck.GetComponent<BoxCollider>();
            if (westPierCollider == null)
            {
                westPierCollider = deck.AddComponent<BoxCollider>();
            }
            if (!deck.CompareTag("Ground"))
            {
                deck.tag = "Ground";
            }

            if (waterSystem != null && westPierCollider != null)
            {
                var current = waterSystem.dryGroundColliders;
                bool exists = false;
                if (current != null)
                {
                    for (int i = 0; i < current.Length; i++)
                    {
                        if (current[i] == westPierCollider)
                        {
                            exists = true;
                            break;
                        }
                    }
                }
                if (!exists)
                {
                    int len = current != null ? current.Length : 0;
                    Collider[] expanded = new Collider[len + 1];
                    for (int i = 0; i < len; i++) expanded[i] = current[i];
                    expanded[len] = westPierCollider;
                    waterSystem.dryGroundColliders = expanded;
                }
            }
        }

        private void BeginPart2Cutscene()
        {
            isCutsceneRunning = true;
            HarborToNeonTransitionState.IsPart2Running = true;

            // Clear any black overlay from earlier transitions so 0:13 is a crisp HARD CUT
            if (CutsceneBlackTransitionOverlay.Instance != null)
            {
                CutsceneBlackTransitionOverlay.Instance.SetBlackAlpha(0f);
                Destroy(CutsceneBlackTransitionOverlay.Instance.gameObject);
            }

            LockActorsForCutscene();
            TemporarilyDisableSouthBarrier();
            uiRig = HarborToNeonCutsceneShared.CreateLetterboxAndFadeUI("HarborToNeon_Part2UI", 0.115f, 0f);
            if (uiRig.SkipText != null)
            {
                uiRig.SkipText.color = new Color(0.75f, 0.88f, 0.98f, 0.72f);
            }

            BuildCutsceneVFXAndCrushProps();
            StartCoroutine(RunPart2Routine());
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
        }

        private void TemporarilyDisableSouthBarrier()
        {
            GameObject barriers = GameObject.Find("Arena_Invisible_Barriers");
            if (barriers != null)
            {
                Transform south = barriers.transform.Find("Barrier_South");
                if (south != null)
                {
                    barrierSouthObj = south.gameObject;
                    barrierSouthWasActive = barrierSouthObj.activeSelf;
                    barrierSouthObj.SetActive(false);
                }
            }
        }

        private void RestoreTemporaryWorldState()
        {
            if (barrierSouthObj != null)
            {
                barrierSouthObj.SetActive(barrierSouthWasActive);
                barrierSouthObj = null;
            }
        }

        private void BuildCutsceneVFXAndCrushProps()
        {
            cutsceneRoot = new GameObject("NeonCityCutscene_RuntimeVFX");

            GameObject planeObj = new GameObject("DeckBouncePlane");
            planeObj.transform.SetParent(cutsceneRoot.transform, false);
            planeObj.transform.position = new Vector3(PierSkidX, PierTopY + 0.05f, -20f);
            deckBouncePlane = planeObj.transform;

            // Continuous skid orange metal spark emitters + rainwater mist spray
            skidSparksPrimary = HarborToNeonCutsceneShared.CreateOrangeSparkEmitter(
                "SkidSparks_Primary", cutsceneRoot.transform, deckBouncePlane, 220f, 16f, 38f);
            skidSparksSecondary = HarborToNeonCutsceneShared.CreateOrangeSparkEmitter(
                "SkidSparks_Secondary", cutsceneRoot.transform, deckBouncePlane, 140f, 10f, 26f);
            SetEmission(skidSparksPrimary, 0f);
            SetEmission(skidSparksSecondary, 0f);

            skidRainSpray = HarborToNeonCutsceneShared.CreateWaterSprayWallEmitter(
                "SkidRainwaterSpray", cutsceneRoot.transform, 10f, 22f);
            var sMain = skidRainSpray.main;
            sMain.startSize = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
            SetEmission(skidRainSpray, 0f);

            // Dynamic warm orange point light illuminating concrete & containers during spark shower
            GameObject glowObj = new GameObject("SkidSparksPointLight");
            glowObj.transform.SetParent(cutsceneRoot.transform, false);
            skidSparkGlowLight = glowObj.AddComponent<Light>();
            skidSparkGlowLight.type = LightType.Point;
            skidSparkGlowLight.range = 42f;
            skidSparkGlowLight.color = new Color(1f, 0.56f, 0.14f);
            skidSparkGlowLight.intensity = 0f;

            // Lightning flash overhead light
            GameObject lightGo = new GameObject("NeonStormLightningLight");
            lightGo.transform.SetParent(cutsceneRoot.transform, false);
            lightGo.transform.position = new Vector3(PierSkidX - 15f, 110f, -65f);
            lightningLight = lightGo.AddComponent<Light>();
            lightningLight.type = LightType.Point;
            lightningLight.range = 480f;
            lightningLight.color = new Color(0.65f, 0.85f, 1f);
            lightningLight.intensity = 0f;

            // Twin Shoulder Vent Steam Emitters anchored directly onto Gipsy's Chest shoulder armor vents
            Transform ventMount = jChest != null ? jChest : (jaeger != null ? jaeger.transform : cutsceneRoot.transform);
            ventSteamLeft = HarborToNeonCutsceneShared.CreateShoulderSteamVentEmitter(
                "ShoulderVentSteam_L", ventMount, new Vector3(-2.4f, 3.2f, -0.5f), new Vector3(-0.48f, 1.0f, -0.25f));
            ventSteamRight = HarborToNeonCutsceneShared.CreateShoulderSteamVentEmitter(
                "ShoulderVentSteam_R", ventMount, new Vector3(2.4f, 3.2f, -0.5f), new Vector3(0.48f, 1.0f, -0.25f));

            // Crushable shipping container stacks & dock gantry structures directly on Gipsy's skid path
            BuildCrushableDockStructures();
        }

        private void BuildCrushableDockStructures()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material MakeContainerMat(Color albedo, Color emissionCol, float metallic = 0.78f, float smooth = 0.42f)
            {
                Material m = new Material(litShader);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", albedo);
                m.color = albedo;
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                if (emissionCol.maxColorComponent > 0.01f)
                {
                    m.EnableKeyword("_EMISSION");
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emissionCol);
                }
                return m;
            }

            Material matRustRed = MakeContainerMat(new Color(0.56f, 0.14f, 0.11f), Color.black);
            Material matSteelTeal = MakeContainerMat(new Color(0.11f, 0.34f, 0.42f), Color.black);
            Material matHazardAmber = MakeContainerMat(new Color(0.65f, 0.44f, 0.09f), Color.black);
            Material matDarkSteel = MakeContainerMat(new Color(0.16f, 0.19f, 0.23f), Color.black, 0.85f, 0.55f);
            Material matNeonCyan = MakeContainerMat(new Color(0.12f, 0.88f, 1f), new Color(0.18f, 1.85f, 2.4f), 0.1f, 0.9f);
            Material matNeonMagenta = MakeContainerMat(new Color(1f, 0.22f, 0.64f), new Color(2.1f, 0.28f, 1.35f), 0.1f, 0.9f);

            // Stack 1 (Z = -32): Double-stacked dock containers + neon dock caution arch right where Gipsy touches down & begins skidding
            CreateCrushStackCluster("CrushCluster_Touchdown", -32.0f, new[]
            {
                (new Vector3(PierSkidX - 3.6f, PierTopY + 1.4f, -32.0f), new Vector3(3.1f, 2.8f, 10.6f), 8f, matRustRed),
                (new Vector3(PierSkidX + 3.1f, PierTopY + 1.4f, -31.2f), new Vector3(3.1f, 2.8f, 10.6f), -6f, matSteelTeal),
                (new Vector3(PierSkidX - 3.5f, PierTopY + 4.2f, -32.1f), new Vector3(3.1f, 2.8f, 10.6f), 11f, matHazardAmber),
                (new Vector3(PierSkidX + 0.0f, PierTopY + 6.9f, -32.0f), new Vector3(11.4f, 0.85f, 1.2f), 0f, matDarkSteel),
                (new Vector3(PierSkidX + 0.0f, PierTopY + 7.45f, -31.4f), new Vector3(9.6f, 0.38f, 0.24f), 0f, matNeonCyan),
            });

            // Stack 2 (Z = -21): Heavy container cluster + steel gantry frame mid-skid
            CreateCrushStackCluster("CrushCluster_MidSkid", -21.0f, new[]
            {
                (new Vector3(PierSkidX - 2.4f, PierTopY + 1.4f, -21.5f), new Vector3(3.2f, 2.8f, 10.2f), 15f, matSteelTeal),
                (new Vector3(PierSkidX + 3.4f, PierTopY + 1.4f, -20.2f), new Vector3(3.2f, 2.8f, 10.2f), -12f, matRustRed),
                (new Vector3(PierSkidX + 3.2f, PierTopY + 4.2f, -20.4f), new Vector3(3.1f, 2.8f, 10.0f), -9f, matHazardAmber),
                (new Vector3(PierSkidX - 6.4f, PierTopY + 3.8f, -21.0f), new Vector3(0.95f, 7.6f, 0.95f), 0f, matDarkSteel),
                (new Vector3(PierSkidX + 6.4f, PierTopY + 3.8f, -21.0f), new Vector3(0.95f, 7.6f, 0.95f), 0f, matDarkSteel),
                (new Vector3(PierSkidX + 0.0f, PierTopY + 7.75f, -21.0f), new Vector3(13.8f, 0.9f, 1.1f), 0f, matDarkSteel),
                (new Vector3(PierSkidX + 0.0f, PierTopY + 7.35f, -20.3f), new Vector3(11.0f, 0.4f, 0.22f), 0f, matNeonMagenta),
            });

            // Stack 3 (Z = -11): Final dockside containers & concrete harbor bollards before Gipsy's catch
            CreateCrushStackCluster("CrushCluster_EndSkid", -11.5f, new[]
            {
                (new Vector3(PierSkidX - 4.0f, PierTopY + 1.4f, -11.5f), new Vector3(3.1f, 2.8f, 9.8f), -18f, matRustRed),
                (new Vector3(PierSkidX + 2.6f, PierTopY + 1.4f, -12.0f), new Vector3(3.1f, 2.8f, 9.8f), 22f, matSteelTeal),
                (new Vector3(PierSkidX - 1.0f, PierTopY + 0.75f, -10.0f), new Vector3(4.8f, 1.5f, 1.2f), 4f, matDarkSteel),
            });
        }

        private void CreateCrushStackCluster(string clusterName, float triggerZ, (Vector3 pos, Vector3 scale, float yaw, Material mat)[] pieces)
        {
            var stack = new CrushStack
            {
                TriggerZ = triggerZ,
                Triggered = false,
                Center = new Vector3(PierSkidX, PierTopY + 1.8f, triggerZ),
                Bodies = new List<Rigidbody>(),
                Dents = new List<Transform>()
            };

            GameObject parent = new GameObject(clusterName);
            parent.transform.SetParent(cutsceneRoot.transform, false);

            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"{clusterName}_Part{i}";
                cube.transform.SetParent(parent.transform, false);
                cube.transform.position = p.pos;
                cube.transform.rotation = Quaternion.Euler(0f, p.yaw, 0f);
                cube.transform.localScale = p.scale;

                var mr = cube.GetComponent<MeshRenderer>();
                if (mr != null && p.mat != null) mr.sharedMaterial = p.mat;

                var col = cube.GetComponent<BoxCollider>();
                var rb = cube.AddComponent<Rigidbody>();
                rb.mass = 25f;
                rb.isKinematic = true;
                rb.useGravity = true;

                // Ensure crushed debris won't snag Gipsy or Kaiju capsules
                if (col != null)
                {
                    if (jaeger != null && jaeger.GetComponent<Collider>() != null)
                        Physics.IgnoreCollision(col, jaeger.GetComponent<Collider>(), true);
                    if (kaiju != null && kaiju.GetComponent<Collider>() != null)
                        Physics.IgnoreCollision(col, kaiju.GetComponent<Collider>(), true);
                }

                stack.Bodies.Add(rb);
                stack.Dents.Add(cube.transform);
            }

            crushStacks.Add(stack);
        }

        private void Update()
        {
            if (!isCutsceneRunning || completedHandoff) return;

            if (!skipRequested && timelineElapsed > 13.45f && HarborToNeonCutsceneShared.IsSkipPressed())
            {
                skipRequested = true;
                StartCoroutine(SkipToCombatRoutine());
            }
        }

        private IEnumerator RunPart2Routine()
        {
            var audio = PacificRimAudioManager.Instance;

            // =========================================================================
            // SHOT 3 (0:13 - 0:18): AIRBORNE TRAJECTORY OVER HONG KONG NEON CITY
            // =========================================================================
            activeShot = 3;
            forceAirbornePose = true;
            forceContinuousDriftPose = false;
            catchCrouchWeight = 0f;

            if (jaegerAnim != null)
            {
                jaegerAnim.enabled = true;
                jaegerAnim.Play("CombatIdle", 0, 0f);
                jaegerAnim.Update(0f);
                jaegerAnim.speed = 0f;
            }
            if (kaiju != null) kaiju.transform.position = new Vector3(-97.5f, 3.00f, -198.0f);

            audio?.PlayAttackWhoosh(new Vector3(PierSkidX, 42f, -135f), true);
            audio?.PlayArmorCreak(new Vector3(PierSkidX, 42f, -135f), 0.7f);
            bool airLightningFired = false;

            // Hurtling horizontally across the Harbor sky toward Harbour_WestPier_Deck landing strip
            Vector3 airStart = new Vector3(-124.0f, 54.0f, -158.0f);
            Vector3 airEnd = new Vector3(PierSkidX - 1.0f, 16.5f, -45.0f);
            timelineElapsed = 13.00f;

            while (timelineElapsed < 18.00f && !skipRequested)
            {
                float dt = Mathf.Min(Time.deltaTime, 0.04f);
                timelineElapsed = Mathf.Min(18.00f, timelineElapsed + dt);
                float u = Mathf.Clamp01((timelineElapsed - 13.00f) / 5.00f);

                Vector3 pos = Vector3.Lerp(airStart, airEnd, u);
                pos.y += Mathf.Sin(u * Mathf.PI) * 14.5f;
                jaegerCoMTargetWorld = pos;

                Vector3 nextPos = Vector3.Lerp(airStart, airEnd, Mathf.Min(1f, u + 0.02f));
                nextPos.y += Mathf.Sin(Mathf.Min(1f, u + 0.02f) * Mathf.PI) * 14.5f;
                Vector3 velDir = (nextPos - pos).sqrMagnitude > 0.001f ? (nextPos - pos).normalized : Vector3.forward;
                airborneVelWorld = velDir;

                float bankRoll = Mathf.Lerp(-24f, 18f, u);
                jaegerHorizontalRot = Quaternion.LookRotation(velDir, Vector3.up) * Quaternion.Euler(90f, 0f, bankRoll);

                if (!airLightningFired && timelineElapsed >= 15.1f)
                {
                    airLightningFired = true;
                    StartCoroutine(PulseLightning(0.45f, 5200f));
                    audio?.PlayLightningStrike(pos + Vector3.up * 25f, 0.85f);
                }

                if (splashManager != null && UnityEngine.Random.value < 0.35f)
                {
                    splashManager.EmitVolumetricMist(pos - velDir * 3f, -velDir * 8f, 7.5f, 0.22f, 1.1f);
                }

                // Dynamic BROADSIDE low-angle tracking camera looking UPWARD so Gipsy's full torso, outstretched arms & trailing legs silhouette cleanly against neon skyline & rain!
                Vector3 camOffset = Vector3.Lerp(new Vector3(22.5f, -9.2f, -4.5f), new Vector3(19.5f, -7.2f, -2.5f), u);
                baseCamPos = pos + camOffset;
                baseCamPos.y = Mathf.Max(baseCamPos.y, PierTopY + 3.0f);
                Vector3 lookTarget = pos - velDir * 2.5f + Vector3.up * 2.2f;
                baseCamRot = Quaternion.LookRotation((lookTarget - baseCamPos).normalized, Vector3.up);
                baseCamFov = Mathf.Lerp(53f, 50f, u);
                customShakeStrength = 0.06f;

                yield return null;
            }
            if (skipRequested) yield break;

            // =========================================================================
            // SHOT 4 (0:18 - 0:24): IMPACT -> CONTINUOUS ROLL INTO KNEELING DRIFT -> CATCH STOP
            // Zero popping! Gipsy slams horizontally, catches fist/knee cleanly, drifts kneeling
            // ~30m across the pavement smashing destructible containers & crane, facing Leatherback!
            // =========================================================================
            activeShot = 4;
            forceAirbornePose = true;

            Vector3 impactPos = new Vector3(PierSkidX - 0.5f, PierTopY + 2.4f, -43.0f);
            Vector3 skidEndPos = FinalJaegerCombatPos;

            baseCamPos = new Vector3(-86.5f, PierTopY + 4.0f, 20.0f);
            baseCamRot = Quaternion.LookRotation((impactPos - baseCamPos).normalized, Vector3.up);
            baseCamFov = 50f;

            // 18.00 -> 18.18: Final split-second plunge onto the pavement still horizontal with full trailing legs
            while (timelineElapsed < 18.18f && !skipRequested)
            {
                float dt = Mathf.Min(Time.deltaTime, 0.04f);
                timelineElapsed = Mathf.Min(18.18f, timelineElapsed + dt);
                float plungeU = Mathf.Clamp01((timelineElapsed - 18.00f) / 0.18f);
                jaegerCoMTargetWorld = Vector3.Lerp(airEnd, impactPos, plungeU);
                airborneVelWorld = Vector3.forward;
                jaegerHorizontalRot = Quaternion.LookRotation(Vector3.forward, Vector3.up) * Quaternion.Euler(90f, 0f, 18f);
                baseCamRot = Quaternion.LookRotation((jaegerCoMTargetWorld - baseCamPos).normalized, Vector3.up);
                yield return null;
            }
            if (skipRequested) yield break;

            // Heavy Touchdown Slam!
            audio?.PlayKaijuSlamImpact(impactPos);
            audio?.PlayAtPosition("Heavy Object Impact 4", impactPos, 1.0f);
            audio?.PlayAtPosition("Metal Impact 7", impactPos, 1.0f);
            audio?.PlayDebrisImpact(impactPos, 1.0f);
            camShake?.Shake(1.35f, 0.58f);
            StartCoroutine(PulseImpactFlash(new Color(1f, 0.65f, 0.25f, 0.28f), 0.18f));
            splashManager?.SpawnColossalTitanDropSplash(new Vector3(impactPos.x, PierTopY + 0.15f, impactPos.z));
            splashManager?.SpawnCombatSparks(impactPos, Vector3.up, false, true, true);
            HarborDestructibleProp.TriggerImpactInRadius(impactPos, 8.5f, Vector3.forward * 20f, 1.15f);
            mainCam?.GetComponent<PacificRimAtmosphere.CameraLensRainController>()?.TriggerLensWaterSplash(0.95f);

            SetEmission(skidSparksPrimary, 260f);
            SetEmission(skidSparksSecondary, 170f);
            SetEmission(skidRainSpray, 85f);
            skidSparksPrimary?.Play();
            skidSparksSecondary?.Play();
            skidRainSpray?.Play();

            forceAirbornePose = false;
            forceContinuousDriftPose = true;

            float nextScrapeAudioTime = 18.45f;
            float nextGroundScuffDecalZ = -41.0f;

            // 18.18 -> 24.00: Seamless impact roll (18.18->19.35) directly into prolonged heroic KNEELING PAVEMENT DRIFT (19.35->22.90) -> settled cool kneel (22.90->24.00)
            while (timelineElapsed < 24.00f && !skipRequested)
            {
                float dt = Mathf.Min(Time.deltaTime, 0.04f);
                timelineElapsed = Mathf.Min(24.00f, timelineElapsed + dt);
                float t = timelineElapsed;

                // Continuous deceleration across entire Shot 4 so Gipsy drifts into a smooth heavy stop by 23.0s
                float slideU = Mathf.Clamp01((t - 18.18f) / (23.05f - 18.18f));
                float easeDecel = 1f - Mathf.Pow(1f - slideU, 2.15f);

                float curX = Mathf.Lerp(impactPos.x, skidEndPos.x, easeDecel);
                float curZ = Mathf.Lerp(impactPos.z, skidEndPos.z, easeDecel);
                float rollToKneel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 18.18f) / 1.15f));
                driftRollToKneel = rollToKneel;
                catchCrouchWeight = rollToKneel;

                // Smooth yaw whip around into facing oncoming Leatherback (~160 -> 176.13 deg) while kneeling & drifting!
                float targetFaceYaw = Mathf.LerpAngle(20f, 176.13f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 18.22f) / 2.25f)));
                jaegerDriftUprightRot = Quaternion.Euler(0f, targetFaceYaw, 0f);
                jaegerHorizontalRot = Quaternion.Euler(0f, Mathf.LerpAngle(20f, 145f, rollToKneel), 0f) *
                                      Quaternion.Euler(Mathf.Lerp(88f, 0f, rollToKneel), 0f, Mathf.Lerp(18f, 0f, rollToKneel));

                jaegerCoMTargetWorld = new Vector3(curX, PierTopY + 2.4f, curZ);
                jaegerGroundAnchorWorld = new Vector3(curX, JaegerStandingY, curZ);

                // Continuous orange metal sparks & scorch decals while drifting (< 22.95s)
                float driftFade = t < 22.95f ? Mathf.Clamp01(1f - Mathf.Clamp01((t - 21.8f) / 1.15f)) : 0f;
                Vector3 fwd = Quaternion.Euler(0f, targetFaceYaw, 0f) * Vector3.forward;
                Vector3 right = Quaternion.Euler(0f, targetFaceYaw, 0f) * Vector3.right;
                Vector3 fistSpot = new Vector3(curX, PierTopY + 0.45f, curZ) + right * 2.5f + fwd * 2.6f;
                Vector3 kneeSpot = new Vector3(curX, PierTopY + 0.45f, curZ) - right * 2.0f - fwd * 2.2f;

                if (skidSparksPrimary != null) skidSparksPrimary.transform.position = fistSpot;
                if (skidSparksSecondary != null) skidSparksSecondary.transform.position = kneeSpot;
                if (skidRainSpray != null) skidRainSpray.transform.position = new Vector3(curX, PierTopY + 0.6f, curZ);
                if (skidSparkGlowLight != null)
                {
                    skidSparkGlowLight.transform.position = new Vector3(curX, PierTopY + 2.8f, curZ);
                    skidSparkGlowLight.intensity = driftFade * (24f + Mathf.PerlinNoise(t * 26f, 0f) * 16f);
                }

                SetEmission(skidSparksPrimary, 265f * driftFade);
                SetEmission(skidSparksSecondary, 175f * driftFade);
                SetEmission(skidRainSpray, 85f * driftFade);

                if (driftFade > 0.08f)
                {
                    EvaluateCrushTriggers(curZ, new Vector3(curX, PierTopY, curZ));
                    HarborDestructibleProp.TriggerImpactInRadius(new Vector3(curX, PierTopY + 2f, curZ), 5.8f, Vector3.forward * (18f * driftFade), 1.0f);

                    if (curZ >= nextGroundScuffDecalZ)
                    {
                        SpawnSkidScorchMark(new Vector3(curX + UnityEngine.Random.Range(-1.2f, 1.2f), PierTopY + 0.04f, curZ - 1.5f));
                        nextGroundScuffDecalZ = curZ + 3.2f;
                    }

                    if (t >= nextScrapeAudioTime)
                    {
                        nextScrapeAudioTime = t + 0.48f;
                        audio?.PlayAtPosition("Metal Impact 7", fistSpot, 0.78f * driftFade);
                        audio?.PlayArmorCreak(fistSpot, 0.68f * driftFade);
                    }
                }

                customShakeStrength = 0.36f * driftFade;

                // Street-level camera glides smoothly from wide side/front corridor angle into framing cool kneeling pose!
                Vector3 camStart = new Vector3(-86.5f, PierTopY + 4.0f, 20.0f);
                Vector3 camMid = new Vector3(-88.2f, PierTopY + 4.8f, 4.5f);
                Vector3 camEnd = new Vector3(-90.5f, PierTopY + 5.2f, -24.0f);
                float shot4CamU = Mathf.Clamp01((t - 18.18f) / (24.00f - 18.18f));
                Vector3 desiredCam = shot4CamU < 0.55f
                    ? Vector3.Lerp(camStart, camMid, Mathf.SmoothStep(0f, 1f, shot4CamU / 0.55f))
                    : Vector3.Lerp(camMid, camEnd, Mathf.SmoothStep(0f, 1f, (shot4CamU - 0.55f) / 0.45f));
                baseCamPos = Vector3.Lerp(baseCamPos, desiredCam, 1f - Mathf.Exp(-dt * 4.8f));

                Vector3 lookFocus = new Vector3(curX, PierTopY + Mathf.Lerp(4.2f, 9.2f, rollToKneel), curZ);
                baseCamRot = Quaternion.LookRotation((lookFocus - baseCamPos).normalized, Vector3.up);
                baseCamFov = Mathf.Lerp(50f, 47f, shot4CamU);

                yield return null;
            }
            if (skipRequested) yield break;

            // =========================================================================
            // SHOT 5 (0:24 - 0:30): SLOW STAND-UP FACING LEATHERBACK + CALM WALK, WALL LEAP & ROAR
            // =========================================================================
            activeShot = 5;
            forceAirbornePose = false;
            forceContinuousDriftPose = false;
            SetEmission(skidSparksPrimary, 0f);
            SetEmission(skidSparksSecondary, 0f);
            SetEmission(skidRainSpray, 0f);
            if (skidSparkGlowLight != null) skidSparkGlowLight.intensity = 0f;

            if (jaeger != null)
            {
                jaeger.transform.position = FinalJaegerCombatPos;
                jaeger.transform.rotation = Quaternion.Euler(0f, 176.13f, 0f);
            }
            if (jaegerAnim != null)
            {
                jaegerAnim.enabled = true;
                jaegerAnim.Play("CombatIdle", 0, 0f);
                jaegerAnim.Update(0f);
                jaegerAnim.speed = 1.0f;
                jaegerAnim.SetBool("InCombat", true);
            }

            Vector3 kBayStart = new Vector3(-97.5f, 2.9f, -172.0f);
            Vector3 kSeawallLip = new Vector3(-97.5f, 3.1f, -148.5f);
            Vector3 kDeckLanding = new Vector3(-97.5f, KaijuDeckStandingY, -112.0f);
            Vector3 kRoarSpot = new Vector3(-97.5f, KaijuDeckStandingY, -102.0f);

            KaijuBioluminescenceController biolum = kaiju != null ? kaiju.GetComponentInChildren<KaijuBioluminescenceController>() : null;
            if (kaiju != null)
            {
                kaiju.transform.position = kBayStart;
                kaiju.transform.rotation = Quaternion.Euler(0f, 356.13f, 0f);
                biolum?.TriggerSurge(2.2f);
                audio?.SetKaijuEmpSparksLoop(true, kBayStart);
            }
            if (kaijuAnim != null)
            {
                // Calm steady Walk animation onto Harbor (NOT sprint!)
                kaijuAnim.speed = 1.0f;
                kaijuAnim.SetBool("IsMoving", true);
                kaijuAnim.SetFloat("LocomotionSpeed", 1.05f);
                kaijuAnim.CrossFade("Walk", 0.14f);
            }

            bool ventBurst1 = false;
            bool ventBurst2 = false;
            bool kaijuLeapStarted = false;
            bool kaijuLandedRoar = false;
            bool shot5Lightning = false;
            bool cutToHarborReveal = false;
            float nextBayStepTime = 24.30f;
            float nextBioSurgeTime = 24.80f;
            float nextSteamPuffTime = 24.22f;

            while (timelineElapsed < 30.00f && !skipRequested)
            {
                float dt = Mathf.Min(Time.deltaTime, 0.04f);
                timelineElapsed = Mathf.Min(30.00f, timelineElapsed + dt);
                float t = timelineElapsed;

                // Gipsy stands up SLOWLY (24.0 -> 28.5s) while continuously facing Leatherback (176.13 deg)
                float standU = Mathf.Clamp01((t - 24.00f) / 4.45f);
                float smoothStand = Mathf.SmoothStep(0f, 1f, standU);
                catchCrouchWeight = 1f - smoothStand;

                if (!ventBurst1 && t >= 24.20f)
                {
                    ventBurst1 = true;
                    audio?.PlayAtPosition("Hydraulic piston mechanical whine", FinalJaegerCombatPos, 0.88f);
                    audio?.PlaySteamVent(FinalJaegerCombatPos + Vector3.up * 12f);
                    SetEmission(ventSteamLeft, 185f);
                    SetEmission(ventSteamRight, 185f);
                    ventSteamLeft?.Play();
                    ventSteamRight?.Play();
                }
                if (t >= 25.90f && t < 26.35f)
                {
                    SetEmission(ventSteamLeft, 40f);
                    SetEmission(ventSteamRight, 40f);
                }
                if (!ventBurst2 && t >= 26.35f)
                {
                    ventBurst2 = true;
                    audio?.PlayHydraulicServo(FinalJaegerCombatPos, 0.90f);
                    audio?.PlayArmorCreak(FinalJaegerCombatPos, 0.80f);
                    audio?.PlaySteamVent(FinalJaegerCombatPos + Vector3.up * 16f);
                    SetEmission(ventSteamLeft, 195f);
                    SetEmission(ventSteamRight, 195f);
                }
                if (t >= 28.35f)
                {
                    SetEmission(ventSteamLeft, 0f);
                    SetEmission(ventSteamRight, 0f);
                }

                if (splashManager != null && t >= 24.20f && t <= 28.15f && t >= nextSteamPuffTime)
                {
                    nextSteamPuffTime = t + 0.08f;
                    Transform anchor = jChest != null ? jChest : (jaeger != null ? jaeger.transform : null);
                    if (anchor != null)
                    {
                        Vector3 lPos = anchor.TransformPoint(new Vector3(-2.3f, 2.9f, -0.4f));
                        Vector3 rPos = anchor.TransformPoint(new Vector3(2.3f, 2.9f, -0.4f));
                        Vector3 lDir = anchor.TransformDirection(new Vector3(-0.48f, 1.0f, -0.25f)).normalized;
                        Vector3 rDir = anchor.TransformDirection(new Vector3(0.48f, 1.0f, -0.25f)).normalized;
                        splashManager.EmitVolumetricMist(lPos, lDir * 13f, 5.2f, 0.65f, 1.05f);
                        splashManager.EmitVolumetricMist(rPos, rDir * 13f, 5.2f, 0.65f, 1.05f);
                    }
                }

                if (t >= nextBioSurgeTime)
                {
                    nextBioSurgeTime = t + 0.85f;
                    biolum?.TriggerSurge(2.0f);
                }

                // Leatherback entrance: 24.0->26.55 Calm Walk over to Harbor -> 26.55->28.15 Calm Jump over seawall -> 28.15->30.0 Roaring EMP!
                if (kaiju != null)
                {
                    if (t < 26.55f)
                    {
                        float walkU = Mathf.Clamp01((t - 24.00f) / 2.55f);
                        Vector3 kPos = Vector3.Lerp(kBayStart, kSeawallLip, walkU);
                        kaiju.transform.position = kPos;

                        if (t >= nextBayStepTime)
                        {
                            nextBayStepTime = t + 0.55f;
                            Vector3 waterHit = new Vector3(kPos.x, 5.05f, kPos.z);
                            splashManager?.SpawnFootstepSplash(waterHit, 1.6f);
                            waterSystem?.AddFootstepWave(waterHit, 20f, 2.8f);
                            audio?.PlayOceanFootstep(kPos, 0.78f);
                        }
                    }
                    else if (t < 28.15f)
                    {
                        if (!kaijuLeapStarted)
                        {
                            kaijuLeapStarted = true;
                            if (kaijuAnim != null)
                            {
                                kaijuAnim.speed = 0.75f;
                                kaijuAnim.SetBool("IsMoving", false);
                                kaijuAnim.SetFloat("LocomotionSpeed", 0f);
                                kaijuAnim.CrossFade("Idle", 0.16f);
                            }
                            audio?.PlayKaijuAttackGrunt(kSeawallLip);
                            splashManager?.SpawnFootstepSplash(new Vector3(kSeawallLip.x, 5.05f, kSeawallLip.z), 2.2f);
                        }

                        float leapU = Mathf.Clamp01((t - 26.55f) / (28.15f - 26.55f));
                        kaijuJumpTuckWeight = Mathf.Sin(leapU * Mathf.PI) * 0.65f;
                        Vector3 arcPos = Vector3.Lerp(kSeawallLip, kDeckLanding, leapU);
                        arcPos.y += Mathf.Sin(leapU * Mathf.PI) * 16.5f;
                        kaiju.transform.position = arcPos;
                    }
                    else
                    {
                        kaijuJumpTuckWeight = 0f;
                        if (!kaijuLandedRoar)
                        {
                            kaijuLandedRoar = true;
                            audio?.PlayKaijuSlamImpact(kDeckLanding);
                            audio?.PlayAtPosition("Heavy Object Impact 4", kDeckLanding, 0.95f);
                            audio?.PlayKaijuEmpRoar(kDeckLanding);
                            audio?.PlayEmpDischargeBlast(kDeckLanding);
                            camShake?.Shake(0.85f, 0.42f);
                            customShakeStrength = 0.34f;
                            biolum?.TriggerSurge(3.2f);
                            splashManager?.SpawnCombatSparks(new Vector3(kDeckLanding.x, PierTopY + 0.4f, kDeckLanding.z), Vector3.up, false, true, true);
                            if (kaijuAnim != null)
                            {
                                kaijuAnim.speed = 1.0f;
                                kaijuAnim.SetBool("IsMoving", false);
                                kaijuAnim.CrossFade("EMP", 0.10f);
                            }
                        }

                        // Slight forward menacing weight settle while roaring
                        float roarU = Mathf.Clamp01((t - 28.15f) / 1.85f);
                        kaiju.transform.position = Vector3.Lerp(kDeckLanding, kRoarSpot, Mathf.SmoothStep(0f, 1f, roarU));
                    }
                }

                if (!shot5Lightning && t >= 28.15f)
                {
                    shot5Lightning = true;
                    StartCoroutine(PulseLightning(0.50f, 5400f));
                    audio?.PlayLightningStrike(new Vector3(PierSkidX, 65f, -125f), 0.85f);
                }

                // Two clean heroic beats in Shot 5:
                // 24.0->26.15: Low 3/4 Front/Side framing Gipsy slowly standing up with visor, reactor & hissing shoulder steam facing Leatherback!
                // 26.15->30.00: Cuts/completes orbit past Gipsy's flank framing towering Gipsy in foreground + Leatherback calmly jumping wall & roaring!
                if (t < 26.15f)
                {
                    float fU = Mathf.Clamp01((t - 24.00f) / 2.15f);
                    Vector3 desired = Vector3.Lerp(new Vector3(-90.5f, PierTopY + 5.2f, -24.5f), new Vector3(-88.5f, PierTopY + 6.4f, -15.5f), fU);
                    baseCamPos = Vector3.Lerp(baseCamPos, desired, 1f - Mathf.Exp(-dt * 5.2f));
                    Vector3 faceFocus = FinalJaegerCombatPos + Vector3.up * Mathf.Lerp(9.2f, 13.5f, smoothStand);
                    baseCamRot = Quaternion.LookRotation((faceFocus - baseCamPos).normalized, Vector3.up);
                    baseCamFov = Mathf.Lerp(47f, 49f, fU);
                }
                else
                {
                    float rU = Mathf.Clamp01((t - 26.15f) / (30.00f - 26.15f));
                    Vector3 revealStart = new Vector3(-91.4f, PierTopY + 6.8f, 15.5f);
                    Vector3 revealEnd = new Vector3(-91.2f, PierTopY + 7.4f, 18.8f);
                    if (!cutToHarborReveal)
                    {
                        cutToHarborReveal = true;
                        baseCamPos = revealStart;
                    }
                    else
                    {
                        baseCamPos = Vector3.Lerp(baseCamPos, Vector3.Lerp(revealStart, revealEnd, rU), 1f - Mathf.Exp(-dt * 4.5f));
                    }
                    Vector3 aim = Vector3.Lerp(new Vector3(-99.5f, PierTopY + 14.5f, -38.0f), new Vector3(-98.5f, PierTopY + 13.5f, -55.0f), rU);
                    baseCamRot = Quaternion.LookRotation((aim - baseCamPos).normalized, Vector3.up);
                    baseCamFov = Mathf.Lerp(52f, 54f, rU);
                }

                customShakeStrength = Mathf.MoveTowards(customShakeStrength, 0.04f, dt * 0.6f);

                if (t >= 29.30f)
                {
                    float blackU = Mathf.Clamp01((t - 29.30f) / 0.70f);
                    SetBlackAlpha(Mathf.SmoothStep(0f, 1f, blackU));
                }

                yield return null;
            }
            if (skipRequested) yield break;

            SetBlackAlpha(1f);
            FinishCutsceneAndStartPhase3Combat();
        }

        private IEnumerator SkipToCombatRoutine()
        {
            float fadeT = 0f;
            float startA = uiRig != null && uiRig.FadeOverlay != null ? uiRig.FadeOverlay.color.a : 0f;
            while (fadeT < 0.32f)
            {
                fadeT += Time.unscaledDeltaTime;
                SetBlackAlpha(Mathf.Lerp(startA, 1f, fadeT / 0.32f));
                yield return null;
            }
            SetBlackAlpha(1f);
            FinishCutsceneAndStartPhase3Combat();
        }

        private IEnumerator ImmediatePhase3HalfHpSetupRoutine(bool fadeFromBlack)
        {
            // Wait one frame so Awake() and Start() on KaijuAI/Jaeger/HUD finish cleanly
            yield return null;
            FindSceneDependencies();
            EnsureHarborWestPierPhysics();

            ApplyCombatPositionsAndPhase3HalfHp();

            if (fadeFromBlack)
            {
                var black = CutsceneBlackTransitionOverlay.GetOrCreate();
                black.SetBlackAlpha(1f);
                black.BeginFadeInAndDestroy(0.85f);
            }
            else if (CutsceneBlackTransitionOverlay.Instance != null)
            {
                CutsceneBlackTransitionOverlay.Instance.BeginFadeInAndDestroy(0.55f);
            }
        }

        private void FinishCutsceneAndStartPhase3Combat()
        {
            if (completedHandoff) return;
            completedHandoff = true;

            activeShot = 0;
            forceAirbornePose = false;
            forceContinuousDriftPose = false;
            catchCrouchWeight = 0f;
            kaijuJumpTuckWeight = 0f;
            isCutsceneRunning = false;
            HarborToNeonTransitionState.IsPart2Running = false;

            SetEmission(skidSparksPrimary, 0f);
            SetEmission(skidSparksSecondary, 0f);
            SetEmission(skidRainSpray, 0f);
            SetEmission(ventSteamLeft, 0f);
            SetEmission(ventSteamRight, 0f);

            // Destroy temporary crushable stacks & VFX cleanly so combat boulevard is clear and crisp
            if (cutsceneRoot != null)
            {
                Destroy(cutsceneRoot);
                cutsceneRoot = null;
            }
            RestoreTemporaryWorldState();

            ApplyCombatPositionsAndPhase3HalfHp();

            // Hand black curtain over to persistent overlay for crisp fade-in while destroying cutscene UI/letterbox
            var blackOverlay = CutsceneBlackTransitionOverlay.GetOrCreate();
            blackOverlay.SetBlackAlpha(1f);
            if (uiRig != null && uiRig.Canvas != null)
            {
                Destroy(uiRig.Canvas.gameObject);
                uiRig = null;
            }
            blackOverlay.BeginFadeInAndDestroy(0.85f);

            Destroy(gameObject);
        }

        private void ApplyCombatPositionsAndPhase3HalfHp()
        {
            if (jaeger != null)
            {
                jaeger.transform.position = FinalJaegerCombatPos;
                jaeger.transform.rotation = Quaternion.Euler(0f, 176.13f, 0f);
                if (jaegerAnim != null)
                {
                    jaegerAnim.enabled = true;
                    jaegerAnim.speed = 1f;
                    jaegerAnim.SetBool("InCombat", true);
                    jaegerAnim.CrossFade("CombatIdle", 0.05f);
                }
                if (jaegerRb != null)
                {
                    jaegerRb.isKinematic = false;
                    jaegerRb.useGravity = true;
                    jaegerRb.linearVelocity = Vector3.zero;
                    jaegerRb.angularVelocity = Vector3.zero;
                }
                jaeger.enabled = true;
                jaeger.isCutsceneActive = false;
            }

            if (kaiju != null)
            {
                kaiju.transform.position = FinalKaijuCombatPos;
                kaiju.transform.rotation = Quaternion.Euler(0f, 356.13f, 0f);
                if (kaijuAnim != null)
                {
                    kaijuAnim.speed = 1f;
                    kaijuAnim.CrossFade("Idle", 0.08f);
                }
                if (kaijuRb != null)
                {
                    kaijuRb.isKinematic = false;
                    kaijuRb.useGravity = true;
                    kaijuRb.linearVelocity = Vector3.zero;
                    kaijuRb.angularVelocity = Vector3.zero;
                }
                kaiju.enabled = true;
                kaiju.InitializePhase3HalfHealth(true);
            }

            if (camFollow != null)
            {
                camFollow.enabled = true;
                camFollow.SmoothTransitionToOverTheShoulder(0.01f);
            }
            if (mainCam != null)
            {
                mainCam.fieldOfView = 60f;
                HarborToNeonCutsceneShared.SyncCameraRain(mainCam, clearSplashes: true);
            }

            if (JaegerHUD.Instance != null)
            {
                JaegerHUD.Instance.SyncStateImmediate();
                JaegerHUD.Instance.SetCutsceneHidden(false);
                JaegerHUD.Instance.PlayNeuralBootSequence(() =>
                {
                    JaegerHUD.Instance?.ShowBanner("LEATHERBACK // PHASE III BERSERK — 50% INTEGRITY", 3.4f);
                });
            }
        }

        private void LateUpdate()
        {
            if (!isCutsceneRunning || activeShot == 0) return;

            if (jaeger != null && forceAirbornePose && jHips != null)
            {
                // Keep base CombatIdle skeletal rest offsets intact, then trail FULL legs & arms cleanly behind velocity!
                if (jaegerAnim != null)
                {
                    jaegerAnim.Play("CombatIdle", 0, 0f);
                    jaegerAnim.Update(0f);
                }
                jaeger.transform.rotation = jaegerHorizontalRot;
                jaeger.transform.position = jaegerCoMTargetWorld - (jaegerHorizontalRot * (Vector3.up * 11.4f));

                Vector3 trailDir = airborneVelWorld.sqrMagnitude > 0.01f ? -airborneVelWorld.normalized : Vector3.back;
                Vector3 sideAxis = jaeger.transform.right;
                Vector3 downAxis = -jaeger.transform.forward;
                float fl = Mathf.Sin(timelineElapsed * 12f) * 0.85f;

                Vector3 hipPos = jHips.position;
                HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(
                    jThighL, jShinL, jFootL,
                    hipPos + trailDir * 10.6f - sideAxis * 1.6f + downAxis * (0.9f + fl),
                    hipPos + trailDir * 5.0f - sideAxis * 2.0f - downAxis * 2.4f, 1f);
                HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(
                    jThighR, jShinR, jFootR,
                    hipPos + trailDir * 10.4f + sideAxis * 1.6f + downAxis * (0.9f - fl),
                    hipPos + trailDir * 5.0f + sideAxis * 2.0f - downAxis * 2.4f, 1f);

                Vector3 chestPos = jChest != null ? jChest.position : hipPos + Vector3.up * 3f;
                HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(
                    jUpperArmL, jForearmL, jHandL,
                    chestPos + trailDir * 6.0f - sideAxis * 3.5f - downAxis * fl,
                    chestPos + trailDir * 3.0f - sideAxis * 6.2f, 1f);
                HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(
                    jUpperArmR, jForearmR, jHandR,
                    chestPos + trailDir * 6.0f + sideAxis * 3.5f + downAxis * fl,
                    chestPos + trailDir * 3.0f + sideAxis * 6.2f, 1f);
            }
            else if (jaeger != null && forceContinuousDriftPose && jHips != null)
            {
                // Seamless blend from horizontal impact (roll=0) directly into kneeling pavement drift (roll=1 -> no sudden pop!)
                if (jaegerAnim != null)
                {
                    jaegerAnim.Play("CombatIdle", 0, 0f);
                    jaegerAnim.Update(0f);
                }

                float roll = driftRollToKneel;
                Quaternion rot = Quaternion.Slerp(jaegerHorizontalRot, jaegerDriftUprightRot, roll);
                jaeger.transform.rotation = rot;

                Vector3 flatOrigin = jaegerCoMTargetWorld - (jaegerHorizontalRot * (Vector3.up * 11.4f));
                jaeger.transform.position = Vector3.Lerp(flatOrigin, jaegerGroundAnchorWorld, roll);

                if (roll > 0.01f)
                {
                    ApplyThreePointKneelPose(roll);
                }
            }
            else if (catchCrouchWeight > 0.005f && jaeger != null && jHips != null)
            {
                ApplyThreePointKneelPose(catchCrouchWeight);
            }

            if (kaijuJumpTuckWeight > 0.01f && kaiju != null)
            {
                if (kUpperLegL != null) kUpperLegL.localRotation *= Quaternion.Euler(-38f * kaijuJumpTuckWeight, 0f, 0f);
                if (kLowerLegL != null) kLowerLegL.localRotation *= Quaternion.Euler(52f * kaijuJumpTuckWeight, 0f, 0f);
                if (kUpperLegR != null) kUpperLegR.localRotation *= Quaternion.Euler(-32f * kaijuJumpTuckWeight, 0f, 0f);
                if (kLowerLegR != null) kLowerLegR.localRotation *= Quaternion.Euler(46f * kaijuJumpTuckWeight, 0f, 0f);
            }

            if (mainCam != null)
            {
                Vector3 handShake = Vector3.zero;
                if (customShakeStrength > 0.005f)
                {
                    float nx = (Mathf.PerlinNoise(Time.time * 28f, 4.1f) - 0.5f) * 2f;
                    float ny = (Mathf.PerlinNoise(7.3f, Time.time * 28f) - 0.5f) * 2f;
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

        private void ApplyThreePointKneelPose(float weight)
        {
            Vector3 fwd = jaeger.transform.forward;
            Vector3 right = jaeger.transform.right;
            Vector3 basePos = jaeger.transform.position;

            // Drop hips ~6.8m so Left Knee rests cleanly at PierTopY + 1.2m while Chest stays high above pier concrete
            jHips.position += new Vector3(0f, -6.8f * weight, 0f) - fwd * (1.2f * weight);

            if (jSpine != null)
                jSpine.rotation = Quaternion.AngleAxis(34f * weight, right) *
                                  Quaternion.AngleAxis(10f * weight, Vector3.up) * jSpine.rotation;
            if (jChest != null)
                jChest.rotation = Quaternion.AngleAxis(10f * weight, right) * jChest.rotation;
            if (jHead != null)
                jHead.rotation = Quaternion.AngleAxis(-28f * weight, right) * jHead.rotation;

            // Right fist slams down bracing forward-right across concrete dock
            Vector3 palmTarget = new Vector3(basePos.x, PierTopY + 0.70f, basePos.z) + right * 2.8f + fwd * 3.2f;
            Vector3 palmPole = (jChest != null ? jChest.position : basePos + Vector3.up * 15f) + right * 6.5f - fwd * 3.0f;
            HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(jUpperArmR, jForearmR, jHandR, palmTarget, palmPole, weight);

            // Left knee braced down behind/left; right foot planted wide forward-right
            Vector3 footLTarget = new Vector3(basePos.x, PierTopY + 0.65f, basePos.z) - right * 2.2f - fwd * 4.8f;
            Vector3 kneeLPole = new Vector3(basePos.x, PierTopY + 1.4f, basePos.z) - right * 2.4f + fwd * 2.2f;
            HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(jThighL, jShinL, jFootL, footLTarget, kneeLPole, weight);

            Vector3 footRTarget = new Vector3(basePos.x, PierTopY + 0.55f, basePos.z) + right * 2.4f + fwd * 2.6f;
            Vector3 kneeRPole = new Vector3(basePos.x, PierTopY + 7.5f, basePos.z) + right * 3.0f + fwd * 6.5f;
            HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(jThighR, jShinR, jFootR, footRTarget, kneeRPole, weight);
        }

        private void EvaluateCrushTriggers(float gipsyZ, Vector3 gipsyPos)
        {
            var audio = PacificRimAudioManager.Instance;
            for (int i = 0; i < crushStacks.Count; i++)
            {
                CrushStack stack = crushStacks[i];
                if (stack.Triggered || gipsyZ < stack.TriggerZ - 2.0f) continue;

                stack.Triggered = true;
                crushStacks[i] = stack;

                audio?.PlayDebrisImpact(stack.Center, 1.0f);
                audio?.PlayAtPosition("Metal Impact 7", stack.Center, 1.0f);
                audio?.PlayAtPosition("Heavy Object Impact 4", stack.Center, 0.9f);
                camShake?.Shake(0.75f, 0.35f);
                splashManager?.SpawnCombatSparks(stack.Center, Vector3.up, false, true, true);
                splashManager?.EmitVolumetricMist(stack.Center, Vector3.up * 5f, 11f, 0.4f, 1.8f);

                for (int b = 0; b < stack.Bodies.Count; b++)
                {
                    Rigidbody rb = stack.Bodies[b];
                    if (rb == null) continue;
                    rb.isKinematic = false;
                    Vector3 sideDir = rb.position.x <= PierSkidX ? Vector3.left : Vector3.right;
                    Vector3 launch = (sideDir * UnityEngine.Random.Range(16f, 29f)) +
                                     Vector3.up * UnityEngine.Random.Range(9f, 19f) +
                                     Vector3.forward * UnityEngine.Random.Range(12f, 24f);
                    rb.linearVelocity = launch;
                    rb.angularVelocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(2.8f, 6.5f);

                    // Crush/crumple metal deformation right on collision
                    Transform t = stack.Dents[b];
                    if (t != null)
                    {
                        Vector3 s = t.localScale;
                        t.localScale = new Vector3(s.x * 0.78f, s.y * 0.76f, s.z * 0.88f);
                    }
                }
            }
        }

        private void SpawnSkidScorchMark(Vector3 worldPos)
        {
            if (cutsceneRoot == null) return;
            GameObject scar = GameObject.CreatePrimitive(PrimitiveType.Quad);
            scar.name = "SkidScorchScar";
            scar.transform.SetParent(cutsceneRoot.transform, false);
            scar.transform.position = worldPos;
            scar.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(-14f, 14f), 0f);
            scar.transform.localScale = new Vector3(UnityEngine.Random.Range(3.2f, 4.8f), 4.6f, 1f);
            var col = scar.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var mr = scar.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material m = new Material(sh);
                Color c = new Color(0.06f, 0.06f, 0.07f, 1f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                m.color = c;
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.75f);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
                mr.sharedMaterial = m;
            }
        }

        private void SetBlackAlpha(float alpha)
        {
            if (uiRig == null || uiRig.FadeOverlay == null) return;
            Color c = uiRig.FadeOverlay.color;
            c.a = Mathf.Clamp01(alpha);
            uiRig.FadeOverlay.color = c;
        }

        private void SetEmission(ParticleSystem ps, float rate)
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
            if (lightningLight == null) yield break;
            float e = 0f;
            while (e < duration)
            {
                e += Time.deltaTime;
                float u = e / duration;
                float env = u < 0.25f ? Mathf.Sin((u / 0.25f) * Mathf.PI) : Mathf.Pow(1f - u, 2f) * (UnityEngine.Random.value > 0.35f ? 1f : 0.25f);
                lightningLight.intensity = peakIntensity * Mathf.Clamp01(env);
                yield return null;
            }
            lightningLight.intensity = 0f;
        }
    }
}
