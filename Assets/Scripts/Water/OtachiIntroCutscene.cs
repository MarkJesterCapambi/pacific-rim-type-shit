using System;
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
    /// Level 2 Intro Cutscene (~30 Seconds): Hong Kong Downtown (Otachi — Cargo Ship / Oil Tanker Strike).
    ///
    /// Beat Breakdown:
    /// - 0:00–0:07 | The Street Drag (Neon & Sparks): Low-angle backward tracking shot down the rain-drenched
    ///               Hong Kong neon canyon as Gipsy Danger strides forward gripping the narrow stern of the
    ///               Oil Tanker in his right hand via 2-bone IK, its steel keel gouging asphalt with bright orange sparks.
    /// - 0:07–0:13 | Otachi's Charge: Wide over-the-shoulder reverse shot past Gipsy down the misty neon avenue
    ///               as Otachi bursts from the rain and sprints straight toward Gipsy.
    /// - 0:13–0:19 | The Two-Handed Wind-Up: Medium-low 3/4 front shot on Gipsy planting both feet, hoisting
    ///               the Oil Tanker like a baseball bat, locking BOTH hands onto the narrow stern handle via IK,
    ///               chest nuclear turbine blazing orange, coiling his torso back as Otachi closes the gap.
    /// - 0:19–0:25 | The Home-Run Impact: Wide side-angle profile shot framing both combatants. Otachi lunges
    ///               jaws wide open; Gipsy whips his torso around smashing the broadside hull squarely across
    ///               Otachi's head; Otachi counters and knocks the Oil Tanker flying nose-first into a flanking skyscraper.
    /// - 0:25–0:30 | Dropping the Wreck & Combat Stance: Low-angle hero shot on Gipsy venting white shoulder steam,
    ///               raising fists into Combat Idle, sounding his deep foghorn as Otachi recovers behind him,
    ///               then fading cleanly to black and handing off into the live Central Plaza boss fight.
    /// </summary>
    [DefaultExecutionOrder(600)]
    public class OtachiIntroCutscene : MonoBehaviour
    {
        public static OtachiIntroCutscene Instance { get; private set; }
        public static bool IsCutscenePlaying => Instance != null && Instance.isRunning;

        [Header("Scene Actors")]
        public Transform jaegerTransform;
        public Transform otachiTransform;

        [Header("Cutscene Props & Materials")]
        public GameObject oilTankerPrefab;
        public Material oilTankerMaterial;

        [Header("Playback Settings")]
        public bool playOnStart = true;
        public float totalDuration = 30.0f;

        [Header("Combat Handoff Spawns (Central Transit Plaza)")]
        public Vector3 combatJaegerPos = new Vector3(0f, 2.977f, -34f);
        public Quaternion combatJaegerRot = Quaternion.identity;
        public Vector3 combatOtachiPos = new Vector3(0f, 0.199f, 38f);
        public Quaternion combatOtachiRot = Quaternion.Euler(0f, 180f, 0f);

        // Street canyon stage: South Cross-Street West (36m narrow neon canyon, zero skybridges, tall flanking skyscrapers)
        private const float StreetCenterZ = -107.5f;
        private const float JaegerGroundY = 2.977f;
        private const float OtachiGroundY = 0.199f;
        private const float JaegerDragStartX = -176f;
        private const float JaegerStandX = -136f;
        private const float OtachiChargeStartX = -22f;
        private const float OtachiImpactX = -114f;
        private static readonly Vector3 CrashBuildingTarget = new Vector3(-109f, 21.5f, -85.1f);

        // Oil Tanker local geometry calibration (Oil Tanker.fbx bounds: Z in [-1.94, +2.71], narrow tip at +Z, heavy bridge at -Z)
        private const float TankerUniformScale = 0.132f; // ~32.3m long, ~6.6m beam
        private const float TankerGripLocalZ = 2.38f;
        private const float TankerGripLocalY = -0.02f;
        private const float TankerKeelLocalZ = -1.35f;
        private const float TankerKeelLocalY = -0.145f;

        private bool isRunning;
        private bool skipRequested;
        private bool hasFinished;
        private float elapsed;

        // Cached combatant components
        private SimpleJaegerController jaegerCtrl;
        private JaegerVisualSockets visualSockets;
        private Animator jaegerAnim;
        private Rigidbody jaegerRb;

        private KaijuAI otachiAI;
        private Animator otachiAnim;
        private Rigidbody otachiRb;

        // Jaeger bones
        private Transform jHips, jSpine1, jChest, jHead;
        private Transform jUpperArmL, jForearmL, jHandL;
        private Transform jUpperArmR, jForearmR, jHandR;
        private Transform jRightArmSword, jLeftArmSword;
        private Renderer turbineRenderer;
        private Material turbineMatInst;
        private Color savedTurbineEmission = Color.black;
        private bool hasSavedTurbineEmission;

        // Otachi bones
        private Transform oUpperTorso, oNeck, oLowerHead, oUpperHead;

        // Camera & scene systems
        private Camera mainCam;
        private JaegerCameraFollow camFollow;
        private PacificRimCameraShake camShake;
        private StormWeatherManager stormMgr;
        private ChinookCombatPatrolSquadron chinookSquad;

        // Temporary objects & UI
        private HarborToNeonCutsceneShared.LetterboxRig letterbox;
        private readonly List<GameObject> spawnedRuntimeObjects = new List<GameObject>();
        private readonly List<GameObject> shot4TempBursts = new List<GameObject>();
        private readonly List<GameObject> hiddenCorridorProps = new List<GameObject>();
        private GameObject tankerRoot;
        private GameObject tankerMeshChild;
        private Transform tankerGripPrimary;   // Right hand socket
        private Transform tankerGripSecondary; // Left hand socket (lower on handle)
        private Transform tankerKeelTip;       // Dragging asphalt spark point
        private Transform tankerBroadsidePoint;

        private ParticleSystem dragSparksPS;
        private ParticleSystem dragDustPS;
        private ParticleSystem steamVentL;
        private ParticleSystem steamVentR;
        private ParticleSystem crashSmokePS;
        private Light dragSparkLight;
        private Light chestReactorLight;
        private Light impactFlashLight;
        private Transform bouncePlaneTr;

        // Pose override state driven in LateUpdate (with smoothed runtime IK/coil channels for zero snapping)
        private bool driveCamera;
        private Vector3 camTargetPos;
        private Quaternion camTargetRot = Quaternion.identity;
        private float camTargetFov = 46f;

        private bool driveTankerFromHands;
        private float rightHandIKWeight;
        private float leftHandIKWeight;
        private float smoothedRightIKWeight;
        private float smoothedLeftIKWeight;
        private Vector3 rightHandTargetWorld;
        private Vector3 rightElbowPoleWorld;
        private Vector3 leftHandTargetWorld;
        private Vector3 leftElbowPoleWorld;

        private float torsoCoilYawDeg;
        private float hipsCoilYawDeg;
        private float chestPitchDeg;
        private float smoothedTorsoCoilYaw;
        private float smoothedHipsCoilYaw;
        private float smoothedChestPitch;
        private float otachiJawOpenDeg;

        private float impactFlashAlpha;
        private Color impactFlashColor = new Color(1f, 0.72f, 0.36f, 1f);
        private float impactLightIntensity;
        private Vector3 impactLightPos;

        private bool savedCamFollowEnabled;
        private float savedCamFov = 60f;
        private static readonly int IdEmissionColor = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            CleanupRuntimeObjects(keepEmbeddedTanker: false);
            RestoreTurbineMaterial();
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            ResolveReferences();
            if (!playOnStart) return;

            // If LoadingScreen is currently active, it will call StartCutscene() when ready
            if (PacificRimLoadingScreen.Instance != null && PacificRimLoadingScreen.Instance.IsLoading)
            {
                LockActorsForPreStaging();
                return;
            }

            StartCutscene();
        }

        public void StartCutscene()
        {
            if (isRunning || hasFinished) return;
            ResolveReferences();
            isRunning = true;
            skipRequested = false;
            StartCoroutine(RunIntroRoutine());
        }

        /// <summary>
        /// Editor/test helper to jump cleanly to a specific timestamp during Play Mode inspection.
        /// </summary>
        public void DebugSeekTo(float seconds)
        {
            elapsed = Mathf.Clamp(seconds, 0f, totalDuration - 0.05f);
        }

        private void Update()
        {
            if (!isRunning) return;

            if (elapsed >= 0.45f && HarborToNeonCutsceneShared.IsSkipPressed())
            {
                skipRequested = true;
            }
        }

        private void LateUpdate()
        {
            if (!isRunning) return;

            // Smooth IK weights and torso coil channels so pose transitions never snap abruptly
            float smoothRate = 1f - Mathf.Exp(-11f * Time.deltaTime);
            smoothedRightIKWeight = Mathf.Lerp(smoothedRightIKWeight, rightHandIKWeight, smoothRate);
            smoothedLeftIKWeight = Mathf.Lerp(smoothedLeftIKWeight, leftHandIKWeight, smoothRate);
            smoothedHipsCoilYaw = Mathf.Lerp(smoothedHipsCoilYaw, hipsCoilYawDeg, smoothRate);
            smoothedTorsoCoilYaw = Mathf.Lerp(smoothedTorsoCoilYaw, torsoCoilYawDeg, smoothRate);
            smoothedChestPitch = Mathf.Lerp(smoothedChestPitch, chestPitchDeg, smoothRate);

            // 1. Apply Jaeger torso coil / swing yaw layered over Animator pose
            if (jHips != null && Mathf.Abs(smoothedHipsCoilYaw) > 0.01f)
            {
                jHips.rotation = Quaternion.AngleAxis(smoothedHipsCoilYaw, Vector3.up) * jHips.rotation;
            }
            if (jSpine1 != null && Mathf.Abs(smoothedTorsoCoilYaw) > 0.01f)
            {
                jSpine1.rotation = Quaternion.AngleAxis(smoothedTorsoCoilYaw * 0.45f, Vector3.up) * jSpine1.rotation;
            }
            if (jChest != null && (Mathf.Abs(smoothedTorsoCoilYaw) > 0.01f || Mathf.Abs(smoothedChestPitch) > 0.01f))
            {
                Quaternion yawQ = Quaternion.AngleAxis(smoothedTorsoCoilYaw * 0.55f, Vector3.up);
                Quaternion pitchQ = Quaternion.AngleAxis(smoothedChestPitch, jaegerTransform != null ? jaegerTransform.right : Vector3.right);
                jChest.rotation = yawQ * pitchQ * jChest.rotation;
            }

            // 2. Keep weapons retracted during Intro
            if (jRightArmSword != null && jRightArmSword.gameObject.activeSelf) jRightArmSword.gameObject.SetActive(false);
            if (jLeftArmSword != null && jLeftArmSword.gameObject.activeSelf) jLeftArmSword.gameObject.SetActive(false);

            // 3. Solve 2-bone arm IK onto the Oil Tanker stern sockets
            if (smoothedRightIKWeight > 0.001f && jUpperArmR != null && jForearmR != null && jHandR != null)
            {
                Vector3 tgtR = (driveTankerFromHands && tankerGripPrimary != null) ? tankerGripPrimary.position : rightHandTargetWorld;
                HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(jUpperArmR, jForearmR, jHandR, tgtR, rightElbowPoleWorld, smoothedRightIKWeight);
            }
            if (smoothedLeftIKWeight > 0.001f && jUpperArmL != null && jForearmL != null && jHandL != null)
            {
                Vector3 tgtL = (driveTankerFromHands && tankerGripSecondary != null) ? tankerGripSecondary.position : leftHandTargetWorld;
                HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(jUpperArmL, jForearmL, jHandL, tgtL, leftElbowPoleWorld, smoothedLeftIKWeight);
            }

            // 4. Otachi wide-open lunging jaw override in Shot 4
            if (oLowerHead != null && otachiJawOpenDeg > 0.01f)
            {
                oLowerHead.localRotation = oLowerHead.localRotation * Quaternion.Euler(otachiJawOpenDeg, 0f, 0f);
            }

            // 5. Update chest nuclear light & impact flash light
            if (chestReactorLight != null && jChest != null)
            {
                chestReactorLight.transform.position = jChest.position + (jaegerTransform != null ? jaegerTransform.forward * 2.4f : Vector3.forward * 2.4f);
            }
            if (impactFlashLight != null)
            {
                if (impactLightIntensity > 1f)
                {
                    impactLightIntensity = Mathf.MoveTowards(impactLightIntensity, 0f, Time.deltaTime * 42000f);
                    impactFlashLight.transform.position = impactLightPos;
                    impactFlashLight.intensity = impactLightIntensity;
                    impactFlashLight.enabled = true;
                }
                else
                {
                    impactFlashLight.enabled = false;
                }
            }

            // 6. Drive cinematic camera + shake
            if (driveCamera && mainCam != null)
            {
                Vector3 shakeOffset = Vector3.zero;
                Vector3 shakeRot = Vector3.zero;
                if (camShake != null)
                {
                    shakeOffset = camShake.CurrentOffset;
                    shakeRot = camShake.CurrentRotationOffset;
                }
                mainCam.transform.position = camTargetPos + shakeOffset;
                mainCam.transform.rotation = camTargetRot * Quaternion.Euler(shakeRot);
                mainCam.fieldOfView = camTargetFov;
                HarborToNeonCutsceneShared.SyncCameraRain(mainCam);
            }

            // 7. Decay full-screen impact flash overlay
            if (letterbox != null && letterbox.FlashOverlay != null)
            {
                if (impactFlashAlpha > 0.001f)
                {
                    impactFlashAlpha = Mathf.MoveTowards(impactFlashAlpha, 0f, Time.deltaTime * 2.8f);
                    Color c = impactFlashColor;
                    c.a = impactFlashAlpha;
                    letterbox.FlashOverlay.color = c;
                }
                else if (letterbox.FlashOverlay.color.a > 0f)
                {
                    letterbox.FlashOverlay.color = new Color(impactFlashColor.r, impactFlashColor.g, impactFlashColor.b, 0f);
                }
            }
        }

        private void ResolveReferences()
        {
            if (jaegerTransform == null)
            {
                var go = GameObject.Find("Jaeger_Mech");
                if (go != null) jaegerTransform = go.transform;
            }
            if (jaegerTransform != null)
            {
                jaegerCtrl = jaegerTransform.GetComponent<SimpleJaegerController>();
                visualSockets = jaegerTransform.GetComponent<JaegerVisualSockets>();
                jaegerAnim = jaegerTransform.GetComponent<Animator>();
                jaegerRb = jaegerTransform.GetComponent<Rigidbody>();
                ResolveJaegerBones();
            }

            if (otachiTransform == null)
            {
                var go = GameObject.Find("Kaiju_Otachi");
                if (go != null) otachiTransform = go.transform;
            }
            if (otachiTransform != null)
            {
                otachiAI = otachiTransform.GetComponent<KaijuAI>();
                otachiAnim = otachiTransform.GetComponentInChildren<Animator>();
                otachiRb = otachiTransform.GetComponent<Rigidbody>();
                ResolveOtachiBones();
            }

            mainCam = Camera.main;
            if (mainCam == null)
            {
                var camGo = GameObject.FindWithTag("MainCamera");
                if (camGo != null) mainCam = camGo.GetComponent<Camera>();
            }
            if (mainCam != null)
            {
                camFollow = mainCam.GetComponent<JaegerCameraFollow>();
                camShake = mainCam.GetComponent<PacificRimCameraShake>();
                if (camShake == null) camShake = mainCam.gameObject.AddComponent<PacificRimCameraShake>();
            }

            if (stormMgr == null) stormMgr = FindAnyObjectByType<StormWeatherManager>();
            if (chinookSquad == null) chinookSquad = FindAnyObjectByType<ChinookCombatPatrolSquadron>();

#if UNITY_EDITOR
            if (oilTankerPrefab == null)
                oilTankerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Oil Tanker.fbx");
            if (oilTankerMaterial == null)
                oilTankerMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_OilTanker.mat");
#endif
        }

        private void ResolveJaegerBones()
        {
            if (jaegerTransform == null) return;
            foreach (var t in jaegerTransform.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Hips": jHips = t; break;
                    case "Spine1": jSpine1 = t; break;
                    case "Chest": jChest = t; break;
                    case "Head": jHead = t; break;
                    case "Upperarm.L": jUpperArmL = t; break;
                    case "Forearm.L": jForearmL = t; break;
                    case "Hand.L": jHandL = t; break;
                    case "Upperarm.R": jUpperArmR = t; break;
                    case "Forearm.R": jForearmR = t; break;
                    case "Hand.R": jHandR = t; break;
                    case "LeftSword": jRightArmSword = t; break;  // Physically under Hand.R
                    case "RightSword": jLeftArmSword = t; break;  // Physically under Hand.L
                }
            }
            var turbTr = jaegerTransform.Find("Spinning Blade.002");
            if (turbTr != null) turbineRenderer = turbTr.GetComponent<Renderer>();
        }

        private void ResolveOtachiBones()
        {
            if (otachiTransform == null) return;
            foreach (var t in otachiTransform.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "UpperTorso": oUpperTorso = t; break;
                    case "Neck": oNeck = t; break;
                    case "LowerHead": oLowerHead = t; break;
                    case "UpperHead": oUpperHead = t; break;
                }
            }
        }

        private void LockActorsForPreStaging()
        {
            if (jaegerCtrl != null) jaegerCtrl.isCutsceneActive = true;
            if (otachiAI != null) otachiAI.isCutsceneMode = true;
            if (JaegerHUD.Instance != null) JaegerHUD.Instance.SetCutsceneHidden(true);
        }

        private void PrepareStage()
        {
            LockActorsForPreStaging();

            if (PacificRimAdminUI.IsOpen) PacificRimAdminUI.Close();

            if (camFollow != null)
            {
                savedCamFollowEnabled = camFollow.enabled;
                camFollow.enabled = false;
            }
            if (mainCam != null)
            {
                savedCamFov = mainCam.fieldOfView;
            }

            if (jaegerRb != null)
            {
                if (!jaegerRb.isKinematic)
                {
                    jaegerRb.linearVelocity = Vector3.zero;
                    jaegerRb.angularVelocity = Vector3.zero;
                }
                jaegerRb.isKinematic = true;
            }
            if (otachiRb != null)
            {
                if (!otachiRb.isKinematic)
                {
                    otachiRb.linearVelocity = Vector3.zero;
                    otachiRb.angularVelocity = Vector3.zero;
                }
                otachiRb.isKinematic = true;
            }

            if (visualSockets != null)
            {
                visualSockets.SetRocketBlastActive(false, immediate: true);
                visualSockets.SetVolumetricConesVisible(true);
            }

            // Temporarily hide small street vehicles inside the Intro street canyon corridor so they stay pristine for combat
            hiddenCorridorProps.Clear();
            foreach (var prop in FindObjectsByType<UrbanCollateralProp>(FindObjectsInactive.Exclude))
            {
                if (prop == null) continue;
                Vector3 p = prop.transform.position;
                if (p.z > -126f && p.z < -89f && p.x > -215f && p.x < -12f)
                {
                    prop.gameObject.SetActive(false);
                    hiddenCorridorProps.Add(prop.gameObject);
                }
            }

            // Setup turbine emissive material instance
            if (turbineRenderer != null && turbineMatInst == null)
            {
                turbineMatInst = turbineRenderer.material;
                if (turbineMatInst != null && turbineMatInst.HasProperty(IdEmissionColor))
                {
                    savedTurbineEmission = turbineMatInst.GetColor(IdEmissionColor);
                    hasSavedTurbineEmission = true;
                }
            }

            // Create letterbox & fade overlay starting solid black
            letterbox = HarborToNeonCutsceneShared.CreateLetterboxAndFadeUI("OtachiIntro_LetterboxUI", 0.115f, 1.0f);
            if (letterbox.Canvas != null) spawnedRuntimeObjects.Add(letterbox.Canvas.gameObject);

            // Create ground plane for spark bounce
            GameObject planeGo = new GameObject("OtachiIntro_SparkBouncePlane");
            planeGo.transform.position = new Vector3(-150f, 0.08f, StreetCenterZ);
            planeGo.transform.rotation = Quaternion.identity;
            bouncePlaneTr = planeGo.transform;
            spawnedRuntimeObjects.Add(planeGo);

            // Build Oil Tanker prop & sockets
            BuildOilTankerRig();

            // Build VFX emitters & dynamic lights
            BuildVfxAndLights();

            // Initial actor transforms
            Quaternion faceEast = Quaternion.Euler(0f, 90f, 0f);
            Quaternion faceWest = Quaternion.Euler(0f, 270f, 0f);

            if (jaegerTransform != null)
            {
                jaegerTransform.position = new Vector3(JaegerDragStartX, JaegerGroundY, StreetCenterZ);
                jaegerTransform.rotation = faceEast;
            }
            if (otachiTransform != null)
            {
                otachiTransform.position = new Vector3(OtachiChargeStartX, OtachiGroundY, StreetCenterZ);
                otachiTransform.rotation = faceWest;
            }

            if (jaegerAnim != null)
            {
                jaegerAnim.applyRootMotion = false;
                jaegerAnim.speed = 0.85f;
                jaegerAnim.SetFloat("Speed", 0.55f);
                jaegerAnim.SetFloat("WalkDirection", 1f);
                jaegerAnim.SetFloat("RunBlend", 0f);
                jaegerAnim.SetBool("IsRunning", false);
                jaegerAnim.SetBool("InCombat", false);
                jaegerAnim.SetBool("IsBlocking", false);
                if (jaegerAnim.layerCount > 1) jaegerAnim.SetLayerWeight(1, 0f);
                jaegerAnim.Play("Locomotion", 0, 0f);
                jaegerAnim.Update(0f);
            }

            if (otachiAnim != null)
            {
                otachiAnim.applyRootMotion = false;
                otachiAnim.speed = 1.0f;
                otachiAnim.SetFloat("LocomotionSpeed", 1.35f);
                otachiAnim.Play("Idle", 0, 0f);
                otachiAnim.Update(0f);
            }
        }

        private void BuildOilTankerRig()
        {
            tankerRoot = new GameObject("OtachiIntro_OilTanker");
            spawnedRuntimeObjects.Add(tankerRoot);

            if (oilTankerPrefab != null)
            {
                tankerMeshChild = Instantiate(oilTankerPrefab, tankerRoot.transform);
                tankerMeshChild.name = "OilTanker_Model";
                // Preserve Oil Tanker.fbx aspect ratio (100, 100, 52.67) scaled cleanly down to ~32.3m world length
                Vector3 rawScale = tankerMeshChild.transform.localScale;
                tankerMeshChild.transform.localPosition = Vector3.zero;
                tankerMeshChild.transform.localRotation = Quaternion.identity;
                tankerMeshChild.transform.localScale = rawScale * TankerUniformScale;

                foreach (var mr in tankerMeshChild.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (oilTankerMaterial != null)
                    {
                        var mats = new Material[mr.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++) mats[i] = oilTankerMaterial;
                        mr.sharedMaterials = mats;
                    }
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                foreach (var col in tankerMeshChild.GetComponentsInChildren<Collider>(true))
                {
                    col.enabled = false;
                }
            }
            else
            {
                // Fallback proxy hull if prefab missing
                tankerMeshChild = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tankerMeshChild.transform.SetParent(tankerRoot.transform, false);
                tankerMeshChild.transform.localScale = new Vector3(6.4f, 6.0f, 32f);
                var c = tankerMeshChild.GetComponent<Collider>();
                if (c != null) Destroy(c);
                if (oilTankerMaterial != null) tankerMeshChild.GetComponent<MeshRenderer>().sharedMaterial = oilTankerMaterial;
            }

            // Compute world-space offsets relative to tankerRoot (meshChild has scale ~ (13.2, 13.2, 6.95))
            float sx = tankerMeshChild.transform.localScale.x;
            float sy = tankerMeshChild.transform.localScale.y;
            float sz = tankerMeshChild.transform.localScale.z;

            // Primary right-hand grip near the narrow stern/prow handle end (+Z)
            var grip1 = new GameObject("Grip_Primary_R");
            grip1.transform.SetParent(tankerRoot.transform, false);
            grip1.transform.localPosition = new Vector3(0.06f * sx, TankerGripLocalY * sy, TankerGripLocalZ * sz);
            tankerGripPrimary = grip1.transform;

            // Secondary left-hand grip slightly closer to the very tip for two-handed baseball bat hold
            var grip2 = new GameObject("Grip_Secondary_L");
            grip2.transform.SetParent(tankerRoot.transform, false);
            grip2.transform.localPosition = new Vector3(-0.06f * sx, (TankerGripLocalY - 0.01f) * sy, (TankerGripLocalZ + 0.28f) * sz);
            tankerGripSecondary = grip2.transform;

            // Keel dragging asphalt contact point (heavy aft/hull section)
            var keel = new GameObject("Keel_DragPoint");
            keel.transform.SetParent(tankerRoot.transform, false);
            keel.transform.localPosition = new Vector3(0f, TankerKeelLocalY * sy, TankerKeelLocalZ * sz);
            tankerKeelTip = keel.transform;

            // Broadside impact point for hit VFX on Otachi
            var broad = new GameObject("Broadside_ImpactPoint");
            broad.transform.SetParent(tankerRoot.transform, false);
            broad.transform.localPosition = new Vector3(0.24f * sx, 0.05f * sy, -0.85f * sz);
            tankerBroadsidePoint = broad.transform;
        }

        private void BuildVfxAndLights()
        {
            // Continuous rooster-tail bright orange sparks under dragging Oil Tanker keel
            dragSparksPS = HarborToNeonCutsceneShared.CreateOrangeSparkEmitter(
                "OtachiIntro_KeelDragSparks", null, bouncePlaneTr, ratePerSec: 260f, speedMin: 16f, speedMax: 38f);
            var shape = dragSparksPS.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 26f;
            shape.radius = 1.6f;
            spawnedRuntimeObjects.Add(dragSparksPS.gameObject);

            // Wet asphalt gouge mist/smoke behind keel
            dragDustPS = HarborToNeonCutsceneShared.CreateWaterSprayWallEmitter("OtachiIntro_AsphaltGougeSteam", null, 8f, 18f);
            var dm = dragDustPS.main;
            dm.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.55f, 0.58f, 0.65f, 0.42f),
                new Color(0.95f, 0.62f, 0.28f, 0.28f));
            dm.gravityModifier = -0.15f;
            spawnedRuntimeObjects.Add(dragDustPS.gameObject);

            // Flickering orange drag-spark point light
            GameObject dlGo = new GameObject("OtachiIntro_DragSparkLight");
            dragSparkLight = dlGo.AddComponent<Light>();
            dragSparkLight.type = LightType.Point;
            dragSparkLight.color = new Color(1f, 0.56f, 0.14f);
            dragSparkLight.range = 42f;
            dragSparkLight.intensity = 0f;
            dragSparkLight.shadows = LightShadows.None;
            spawnedRuntimeObjects.Add(dlGo);

            // Chest nuclear turbine glow light (localized warm rim glow — never blows out Gipsy's torso!)
            GameObject clGo = new GameObject("OtachiIntro_ChestTurbineLight");
            chestReactorLight = clGo.AddComponent<Light>();
            chestReactorLight.type = LightType.Point;
            chestReactorLight.color = new Color(1f, 0.38f, 0.06f);
            chestReactorLight.range = 18f;
            chestReactorLight.intensity = 0f;
            chestReactorLight.shadows = LightShadows.None;
            spawnedRuntimeObjects.Add(clGo);

            // Impact flash light (home-run hit & skyscraper crash)
            GameObject ilGo = new GameObject("OtachiIntro_ImpactFlashLight");
            impactFlashLight = ilGo.AddComponent<Light>();
            impactFlashLight.type = LightType.Point;
            impactFlashLight.color = new Color(1f, 0.58f, 0.20f);
            impactFlashLight.range = 55f;
            impactFlashLight.intensity = 0f;
            impactFlashLight.shadows = LightShadows.None;
            impactFlashLight.enabled = false;
            spawnedRuntimeObjects.Add(ilGo);

            // Dual shoulder steam vents on Gipsy's chest for Shot 5
            Transform steamParent = jChest != null ? jChest : jaegerTransform;
            if (steamParent != null)
            {
                // Chest bone has tiny DCC scale (~0.01), so attach steam vents to world-scale followers on Jaeger root!
                GameObject steamRootL = new GameObject("OtachiIntro_ShoulderSteamL");
                steamRootL.transform.SetParent(jaegerTransform, false);
                steamRootL.transform.localPosition = new Vector3(-4.2f, 20.6f, -0.6f);
                steamVentL = HarborToNeonCutsceneShared.CreateShoulderSteamVentEmitter("SteamPS_L", steamRootL.transform, Vector3.zero, new Vector3(-0.75f, 0.65f, -0.15f));
                spawnedRuntimeObjects.Add(steamRootL);

                GameObject steamRootR = new GameObject("OtachiIntro_ShoulderSteamR");
                steamRootR.transform.SetParent(jaegerTransform, false);
                steamRootR.transform.localPosition = new Vector3(4.2f, 20.6f, -0.6f);
                steamVentR = HarborToNeonCutsceneShared.CreateShoulderSteamVentEmitter("SteamPS_R", steamRootR.transform, Vector3.zero, new Vector3(0.75f, 0.65f, -0.15f));
                spawnedRuntimeObjects.Add(steamRootR);
            }
        }

        private IEnumerator RunIntroRoutine()
        {
            PrepareStage();

            elapsed = 0f;
            driveCamera = true;

            bool playedShot2Roar = false;
            bool startedOtachiRun = false;
            bool playedShot3Hydraulics = false;
            bool enteredCombatIdleShot3 = false;
            bool startedOtachiLunge = false;
            bool playedSwingWhoosh = false;
            bool triggeredHomeRunImpact = false;
            bool triggeredOtachiCounter = false;
            bool triggeredTankerDisarm = false;
            bool triggeredBuildingCrash = false;
            bool triggeredShot5Steam = false;
            bool triggeredShot5Foghorn = false;
            float nextFootstepTime = 0.55f;
            float nextScrapeSfxTime = 0.4f;
            float nextOtachiStepTime = 8.1f;

            Quaternion faceEast = Quaternion.Euler(0f, 90f, 0f);
            Quaternion faceWest = Quaternion.Euler(0f, 270f, 0f);

            // Recorded tanker world pose at disarm moment (22.60s)
            Vector3 disarmTankerPos = Vector3.zero;
            Quaternion disarmTankerRot = Quaternion.identity;

            while (elapsed < totalDuration && !skipRequested)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                // --- Letterbox Fade-In (0:00–0:01.2) & Fade-Out (0:28.6–0:30.0) ---
                if (letterbox != null && letterbox.FadeOverlay != null)
                {
                    float blackAlpha = 0f;
                    if (elapsed < 1.2f)
                        blackAlpha = 1f - Mathf.SmoothStep(0f, 1f, elapsed / 1.2f);
                    else if (elapsed > 28.6f)
                        blackAlpha = Mathf.SmoothStep(0f, 1f, (elapsed - 28.6f) / 1.4f);
                    letterbox.FadeOverlay.color = new Color(0f, 0f, 0f, blackAlpha);
                }
                if (letterbox != null && letterbox.SkipText != null)
                {
                    float skipAlpha = elapsed > 1.5f && elapsed < 28.0f
                        ? Mathf.Clamp01((elapsed - 1.5f) / 0.8f) * 0.72f
                        : 0f;
                    letterbox.SkipText.color = new Color(0.75f, 0.88f, 0.98f, skipAlpha);
                }

                // =========================================================================
                // SHOT 1 (0:00 – 0:07): THE STREET DRAG (NEON & SPARKS)
                // =========================================================================
                if (elapsed < 7.0f)
                {
                    float u = elapsed / 7.0f;
                    float jx = Mathf.Lerp(JaegerDragStartX, JaegerStandX - 6f, u);
                    if (jaegerTransform != null)
                    {
                        jaegerTransform.position = new Vector3(jx, JaegerGroundY, StreetCenterZ);
                        jaegerTransform.rotation = faceEast;
                    }

                    // Position Oil Tanker dragged behind Gipsy's right side with keel scraping asphalt
                    PositionTankerForDrag(jx, StreetCenterZ, elapsed);

                    driveTankerFromHands = true;
                    rightHandIKWeight = 1f;
                    leftHandIKWeight = 0f;
                    rightElbowPoleWorld = new Vector3(jx - 4.5f, 16.5f, StreetCenterZ - 7.5f);
                    hipsCoilYawDeg = -8f;
                    torsoCoilYawDeg = -10f;
                    chestPitchDeg = 5f;
                    otachiJawOpenDeg = 0f;

                    // Continuous rooster-tail orange sparks & flickering ground light under dragging keel
                    UpdateKeelDragSparks(active: true, intensityScale: 1.0f);
                    SetChestTurbineGlow(0.35f);

                    // Tighter low-angle tracking shot closer behind Gipsy's back alongside the Oil Tanker:
                    // frames Gipsy from his mid-calves cleanly up to his armored upper torso (cropping feet below frame)
                    // while highlighting the upright dragging ship & sparks leading straight into his gripping right hand!
                    Vector3 camPos = new Vector3(
                        jx - Mathf.Lerp(21.5f, 18.8f, u),
                        Mathf.Lerp(7.8f, 8.8f, u) + Mathf.Sin(elapsed * 2.4f) * 0.10f,
                        StreetCenterZ - Mathf.Lerp(6.4f, 5.4f, u));
                    Vector3 lookPt = new Vector3(jx + 2.0f, 14.0f, StreetCenterZ - 1.0f);
                    camTargetPos = camPos;
                    camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                    camTargetFov = Mathf.Lerp(41f, 38f, u);

                    // Heavy mech footsteps & steel hull scraping asphalt SFX
                    if (elapsed >= nextFootstepTime)
                    {
                        nextFootstepTime = elapsed + 1.08f;
                        PacificRimAudioManager.Instance?.PlayOceanFootstep(new Vector3(jx, 0.2f, StreetCenterZ), 0.95f);
                        camShake?.Shake(0.32f, 0.22f);
                    }
                    if (elapsed >= nextScrapeSfxTime)
                    {
                        nextScrapeSfxTime = elapsed + 1.35f;
                        if (tankerKeelTip != null)
                        {
                            PacificRimAudioManager.Instance?.PlayArmorCreak(tankerKeelTip.position);
                        }
                    }
                }
                // =========================================================================
                // SHOT 2 (0:07 – 0:13): OTACHI'S CHARGE (OTS REVERSE ANGLE)
                // =========================================================================
                else if (elapsed < 13.0f)
                {
                    float u = (elapsed - 7.0f) / 6.0f;
                    // Gipsy slows smoothly into a planted halt at JaegerStandX
                    float jEase = 1f - Mathf.Pow(1f - Mathf.Clamp01(u * 1.45f), 2f);
                    float jx = Mathf.Lerp(JaegerStandX - 6f, JaegerStandX, jEase);
                    if (jaegerTransform != null)
                    {
                        jaegerTransform.position = new Vector3(jx, JaegerGroundY, StreetCenterZ);
                        jaegerTransform.rotation = faceEast;
                    }
                    if (jaegerAnim != null)
                    {
                        float spd = Mathf.Lerp(0.55f, 0f, Mathf.Clamp01(u * 1.6f));
                        jaegerAnim.SetFloat("Speed", spd);
                        jaegerAnim.speed = Mathf.Lerp(0.85f, 0.95f, u);
                    }

                    PositionTankerForDrag(jx, StreetCenterZ, elapsed);
                    driveTankerFromHands = true;
                    rightHandIKWeight = 1f;
                    leftHandIKWeight = 0f;
                    rightElbowPoleWorld = new Vector3(jx - 4.5f, 16.5f, StreetCenterZ - 7.5f);
                    hipsCoilYawDeg = Mathf.Lerp(-8f, 0f, u);
                    torsoCoilYawDeg = Mathf.Lerp(-10f, 0f, u);
                    chestPitchDeg = Mathf.Lerp(5f, 2f, u);

                    UpdateKeelDragSparks(active: u < 0.65f, intensityScale: Mathf.Clamp01(1f - u * 1.5f));
                    SetChestTurbineGlow(Mathf.Lerp(0.35f, 0.65f, u));

                    // Otachi bursts out of rain & mist at far end of avenue and charges straight toward Gipsy!
                    if (!playedShot2Roar && elapsed >= 7.35f)
                    {
                        playedShot2Roar = true;
                        PacificRimAudioManager.Instance?.PlayKaijuEmpRoar(new Vector3(OtachiChargeStartX, 12f, StreetCenterZ));
                        if (otachiAnim != null) otachiAnim.CrossFadeInFixedTime("EMP", 0.30f, 0);
                        camShake?.Shake(0.65f, 0.55f);
                    }
                    if (!startedOtachiRun && elapsed >= 8.35f)
                    {
                        startedOtachiRun = true;
                        if (otachiAnim != null)
                        {
                            otachiAnim.SetFloat("LocomotionSpeed", 1.45f);
                            otachiAnim.CrossFadeInFixedTime("Run", 0.32f, 0);
                        }
                    }

                    float chargeU = Mathf.Clamp01((elapsed - 8.1f) / 4.9f);
                    float ox = Mathf.Lerp(OtachiChargeStartX, -90f, chargeU * chargeU * 0.25f + chargeU * 0.75f);
                    if (otachiTransform != null)
                    {
                        otachiTransform.position = new Vector3(ox, OtachiGroundY, StreetCenterZ);
                        otachiTransform.rotation = faceWest;
                    }
                    if (startedOtachiRun && elapsed >= nextOtachiStepTime)
                    {
                        nextOtachiStepTime = elapsed + 0.58f;
                        PacificRimAudioManager.Instance?.PlayOceanFootstep(new Vector3(ox, 0.2f, StreetCenterZ), 1.05f);
                        camShake?.Shake(0.38f, 0.18f);
                    }

                    // Wide over-the-shoulder / reverse-angle shot looking past Gipsy's left shoulder down the neon canyon
                    Vector3 camPos = new Vector3(
                        jx - Mathf.Lerp(11.5f, 9.2f, u),
                        Mathf.Lerp(21.8f, 20.2f, u),
                        StreetCenterZ + Mathf.Lerp(7.8f, 6.4f, u));
                    Vector3 lookPt = new Vector3(Mathf.Lerp(ox, ox - 8f, u), 12.2f, StreetCenterZ - 0.5f);
                    camTargetPos = camPos;
                    camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                    camTargetFov = Mathf.Lerp(47f, 43f, u);
                }
                // =========================================================================
                // SHOT 3 (0:13 – 0:19): THE TWO-HANDED WIND-UP (BASEBALL BAT STANCE)
                // =========================================================================
                else if (elapsed < 19.0f)
                {
                    float u = (elapsed - 13.0f) / 6.0f;
                    float hoistU = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.52f));
                    float coilU = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.35f) / 0.65f));

                    if (!enteredCombatIdleShot3)
                    {
                        enteredCombatIdleShot3 = true;
                        if (jaegerAnim != null)
                        {
                            jaegerAnim.SetFloat("Speed", 0f);
                            jaegerAnim.SetBool("InCombat", true);
                            jaegerAnim.CrossFadeInFixedTime("CombatIdle", 0.38f, 0);
                        }
                    }
                    if (!playedShot3Hydraulics && elapsed >= 13.25f)
                    {
                        playedShot3Hydraulics = true;
                        PacificRimAudioManager.Instance?.PlayHydraulicServo(jaegerTransform.position + Vector3.up * 16f);
                        PacificRimAudioManager.Instance?.PlayArmorCreak(jaegerTransform.position + Vector3.up * 12f);
                    }

                    // Keep Gipsy planted solidly at JaegerStandX
                    if (jaegerTransform != null)
                    {
                        jaegerTransform.position = new Vector3(JaegerStandX, JaegerGroundY, StreetCenterZ);
                        jaegerTransform.rotation = faceEast;
                    }

                    // Otachi continues closing the gap in the misty street behind camera/off-axis
                    float ox = Mathf.Lerp(-90f, -104f, u);
                    if (otachiTransform != null)
                    {
                        otachiTransform.position = new Vector3(ox, OtachiGroundY, StreetCenterZ);
                        otachiTransform.rotation = faceWest;
                    }

                    // Smoothly hoist Oil Tanker from street drag -> cocked horizontal two-handed baseball bat load-up!
                    PositionTankerForBaseballBat(JaegerStandX, StreetCenterZ, hoistU, coilU, swingDeg: 0f);

                    driveTankerFromHands = true;
                    rightHandIKWeight = 1f;
                    leftHandIKWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.14f) / 0.34f));

                    // Natural batter elbow poles (right elbow cocked up/out behind right side, left elbow down/in front)
                    rightElbowPoleWorld = new Vector3(JaegerStandX - 6.5f, 18.5f, StreetCenterZ - 11.5f);
                    leftElbowPoleWorld = new Vector3(JaegerStandX + 5.5f, 14.5f, StreetCenterZ - 7.5f);

                    // Coil hips & torso back into loaded right-handed batter stance (positive yaw turns Gipsy's chest right/South)
                    hipsCoilYawDeg = Mathf.Lerp(0f, 26f, coilU);
                    torsoCoilYawDeg = Mathf.Lerp(0f, 42f, coilU);
                    chestPitchDeg = Mathf.Lerp(2f, -6f, coilU);

                    // Sparks die out as hull lifts cleanly off the wet asphalt
                    UpdateKeelDragSparks(active: hoistU < 0.18f, intensityScale: Mathf.Clamp01(1f - hoistU * 5f));

                    // Chest nuclear turbine glows warm fiery orange (balanced — no blown-out torso glare!)
                    SetChestTurbineGlow(Mathf.Lerp(0.55f, 1.55f, coilU));
                    if (elapsed >= 16.4f && elapsed < 16.4f + dt * 1.5f)
                    {
                        PacificRimAudioManager.Instance?.PlayHydraulicServo(jaegerTransform.position + Vector3.up * 18f);
                    }

                    // Medium-low 3/4 front angle on Gipsy with wider framing to show the UPWARD-hoisted Oil Tanker baseball bat!
                    Vector3 camPos = new Vector3(
                        JaegerStandX + Mathf.Lerp(30.5f, 25.5f, u),
                        Mathf.Lerp(7.8f, 9.2f, u),
                        StreetCenterZ - Mathf.Lerp(17.5f, 14.5f, u));
                    Vector3 lookPt = new Vector3(JaegerStandX - 0.5f, Mathf.Lerp(16.0f, 19.5f, hoistU), StreetCenterZ - 1.2f);
                    camTargetPos = camPos;
                    camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                    camTargetFov = Mathf.Lerp(50f, 46f, u);
                }
                // =========================================================================
                // SHOT 4 (0:19 – 0:25): THE HOME-RUN IMPACT & TANKER PLUNGING INTO BUILDING
                // =========================================================================
                else if (elapsed < 25.0f)
                {
                    float t4 = elapsed - 19.0f; // 0 .. 6.0s

                    if (jaegerTransform != null)
                    {
                        jaegerTransform.position = new Vector3(JaegerStandX, JaegerGroundY, StreetCenterZ);
                        jaegerTransform.rotation = faceEast;
                    }

                    // 19.0 – 20.85s: Otachi lunges straight forward with jaws wide open!
                    if (t4 < 1.85f)
                    {
                        if (!startedOtachiLunge)
                        {
                            startedOtachiLunge = true;
                            if (otachiAnim != null)
                            {
                                otachiAnim.speed = 1.15f;
                                otachiAnim.CrossFadeInFixedTime("Slam", 0.28f, 0);
                            }
                            PacificRimAudioManager.Instance?.PlayKaijuAttackGrunt(otachiTransform.position + Vector3.up * 14f);
                        }

                        float lungeU = Mathf.Clamp01(t4 / 1.85f);
                        float ox = Mathf.Lerp(-104f, OtachiImpactX, Mathf.SmoothStep(0f, 1f, lungeU));
                        if (otachiTransform != null)
                        {
                            otachiTransform.position = new Vector3(ox, OtachiGroundY, StreetCenterZ);
                            otachiTransform.rotation = faceWest;
                        }
                        otachiJawOpenDeg = Mathf.SmoothStep(0f, 42f, Mathf.Clamp01(t4 / 1.1f));

                        // Gipsy holds coil until t4=1.15s then unleashes violent horizontal baseball bat whip!
                        float swingPhase = Mathf.Clamp01((t4 - 1.15f) / 0.70f);
                        float swingEase = swingPhase * swingPhase * (3f - 2f * swingPhase);
                        float swingAngle = Mathf.Lerp(0f, 118f, swingEase);

                        if (!playedSwingWhoosh && t4 >= 1.25f)
                        {
                            playedSwingWhoosh = true;
                            PacificRimAudioManager.Instance?.PlayAttackWhoosh(jaegerTransform.position + Vector3.up * 16f, isHeavy: true);
                        }

                        hipsCoilYawDeg = Mathf.Lerp(26f, -22f, swingEase);
                        torsoCoilYawDeg = Mathf.Lerp(42f, -44f, swingEase);
                        chestPitchDeg = Mathf.Lerp(-6f, 8f, swingEase);
                        PositionTankerForBaseballBat(JaegerStandX, StreetCenterZ, 1f, 1f, swingAngle);
                        driveTankerFromHands = true;
                        rightHandIKWeight = 1f;
                        leftHandIKWeight = 1f;
                        rightElbowPoleWorld = new Vector3(JaegerStandX + 2f, 15.5f, StreetCenterZ - 9f);
                        leftElbowPoleWorld = new Vector3(JaegerStandX + 6f, 15.0f, StreetCenterZ + 8f);
                        SetChestTurbineGlow(1.65f);
                    }
                    // 20.85 – 22.60s: HOME-RUN IMPACT -> Otachi recoils then counters viciously!
                    else if (t4 < 3.60f)
                    {
                        if (!triggeredHomeRunImpact)
                        {
                            triggeredHomeRunImpact = true;
                            Vector3 hitPt = new Vector3(OtachiImpactX - 4f, 16.5f, StreetCenterZ);
                            TriggerImpactBurst(hitPt, new Color(1f, 0.70f, 0.28f, 1f), flashAlpha: 0.45f, shakeMag: 3.1f, shakeDur: 0.55f);
                            SpawnSparksBurst(hitPt, 110, isCyanBlood: false);
                            SpawnSparksBurst(hitPt + Vector3.right * 1.5f, 75, isCyanBlood: true);
                            PacificRimAudioManager.Instance?.PlayJaegerPunchHit(hitPt, isHeavy: true);
                            PacificRimAudioManager.Instance?.PlayDebrisImpact(hitPt, 1.35f);
                            PacificRimAudioManager.Instance?.PlayKaijuStaggerPain(hitPt);
                            if (otachiAnim != null)
                            {
                                otachiAnim.speed = 1.0f;
                                otachiAnim.CrossFadeInFixedTime("StaggerRight", 0.20f, 0);
                            }
                        }

                        float postU = (t4 - 1.85f) / 1.75f;
                        otachiJawOpenDeg = Mathf.Lerp(42f, 12f, Mathf.Clamp01(postU * 2.5f));

                        // Otachi recoils back then counter-swipes RIGHTWARD (PunchLeft swings from left -> right toward North building!)
                        float ox;
                        if (postU < 0.52f)
                        {
                            ox = Mathf.Lerp(OtachiImpactX, OtachiImpactX + 9.5f, Mathf.SmoothStep(0f, 1f, postU / 0.52f));
                        }
                        else
                        {
                            if (!triggeredOtachiCounter)
                            {
                                triggeredOtachiCounter = true;
                                if (otachiAnim != null)
                                {
                                    otachiAnim.speed = 1.25f;
                                    otachiAnim.CrossFadeInFixedTime("PunchLeft", 0.24f, 0);
                                }
                                PacificRimAudioManager.Instance?.PlayKaijuAttackGrunt(otachiTransform.position + Vector3.up * 15f);
                            }
                            float cU = (postU - 0.52f) / 0.48f;
                            ox = Mathf.Lerp(OtachiImpactX + 9.5f, OtachiImpactX - 1.5f, Mathf.SmoothStep(0f, 1f, cU));
                        }
                        if (otachiTransform != null)
                        {
                            otachiTransform.position = new Vector3(ox, OtachiGroundY, StreetCenterZ);
                            otachiTransform.rotation = faceWest;
                        }

                        // Gipsy follows through slightly and holds ship out across chest as Otachi counter-strikes
                        float followAngle = Mathf.Lerp(118f, 134f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(postU * 1.4f)));
                        hipsCoilYawDeg = Mathf.Lerp(-22f, -14f, postU);
                        torsoCoilYawDeg = Mathf.Lerp(-44f, -28f, postU);
                        chestPitchDeg = Mathf.Lerp(8f, 3f, postU);
                        PositionTankerForBaseballBat(JaegerStandX, StreetCenterZ, 1f, 1f, followAngle);
                        driveTankerFromHands = true;
                        rightHandIKWeight = 1f;
                        leftHandIKWeight = 1f;
                        SetChestTurbineGlow(Mathf.Lerp(1.65f, 1.15f, postU));
                    }
                    // 22.60 – 25.00s: OTACHI KNOCKS OIL TANKER OUT OF GIPSY'S HANDS -> PLUNGES INTO SKYSCRAPER!
                    else
                    {
                        if (!triggeredTankerDisarm)
                        {
                            triggeredTankerDisarm = true;
                            if (tankerRoot != null)
                            {
                                disarmTankerPos = tankerRoot.transform.position;
                                disarmTankerRot = tankerRoot.transform.rotation;
                            }
                            Vector3 disarmPt = new Vector3(JaegerStandX + 12f, 16.5f, StreetCenterZ + 3f);
                            TriggerImpactBurst(disarmPt, new Color(1f, 0.78f, 0.45f, 1f), flashAlpha: 0.30f, shakeMag: 2.2f, shakeDur: 0.38f);
                            SpawnSparksBurst(disarmPt, 80, isCyanBlood: false);
                            PacificRimAudioManager.Instance?.PlayDebrisImpact(disarmPt, 1.30f);
                            PacificRimAudioManager.Instance?.PlayArmorCreak(disarmPt);
                        }

                        float flyT = t4 - 3.60f; // 0 .. 2.40s
                        float flightDur = 0.98f;
                        float flyU = Mathf.Clamp01(flyT / flightDur);

                        // Smoothly release Gipsy's arms from IK back to Combat Idle
                        float relU = Mathf.Clamp01(flyT / 0.35f);
                        rightHandIKWeight = Mathf.Lerp(1f, 0f, relU);
                        leftHandIKWeight = Mathf.Lerp(1f, 0f, relU);
                        hipsCoilYawDeg = Mathf.Lerp(-14f, 0f, Mathf.Clamp01(flyT / 0.8f));
                        torsoCoilYawDeg = Mathf.Lerp(-28f, 0f, Mathf.Clamp01(flyT / 0.8f));
                        chestPitchDeg = Mathf.Lerp(3f, 0f, Mathf.Clamp01(flyT / 0.8f));
                        otachiJawOpenDeg = 0f;
                        SetChestTurbineGlow(Mathf.Lerp(2.0f, 1.1f, Mathf.Clamp01(flyT / 1.5f)));

                        // Parabolic tumble into the 75m tall skyscraper facade (Block_WestPodium_Tower_4_0 at Z = -85.1)
                        Quaternion embeddedRot = Quaternion.LookRotation(new Vector3(0.28f, -0.34f, 0.90f).normalized, Vector3.up);
                        if (tankerRoot != null)
                        {
                            if (flyU < 1f)
                            {
                                Vector3 pos = Vector3.Lerp(disarmTankerPos, CrashBuildingTarget, flyU);
                                pos.y += Mathf.Sin(flyU * Mathf.PI) * 9.5f;
                                tankerRoot.transform.position = pos;
                                // Dramatic spin during flight settling nose-first into facade
                                Quaternion spinRot = Quaternion.Slerp(disarmTankerRot, embeddedRot, Mathf.SmoothStep(0f, 1f, flyU))
                                                     * Quaternion.Euler(0f, 0f, (1f - flyU) * 140f);
                                tankerRoot.transform.rotation = spinRot;
                            }
                            else
                            {
                                // Embedded firmly nose-first into skyscraper wall with tiny post-impact shudder
                                float afterCrash = flyT - flightDur;
                                float shudder = Mathf.Exp(-afterCrash * 7f) * Mathf.Sin(afterCrash * 48f) * 0.45f;
                                tankerRoot.transform.position = CrashBuildingTarget + new Vector3(0f, shudder, 0f);
                                tankerRoot.transform.rotation = embeddedRot;
                            }
                        }

                        if (!triggeredBuildingCrash && flyU >= 1f)
                        {
                            triggeredBuildingCrash = true;
                            TriggerImpactBurst(CrashBuildingTarget, new Color(1f, 0.58f, 0.16f, 1f), flashAlpha: 0.36f, shakeMag: 3.4f, shakeDur: 0.72f);
                            SpawnSparksBurst(CrashBuildingTarget + new Vector3(0f, 0f, -2.5f), 150, isCyanBlood: false);
                            SpawnBuildingCrashSmoke(CrashBuildingTarget + new Vector3(0f, 1.5f, -3.5f));
                            PacificRimAudioManager.Instance?.PlayKaijuSlamImpact(CrashBuildingTarget);
                            PacificRimAudioManager.Instance?.PlayDebrisImpact(CrashBuildingTarget, 1.55f);
                        }

                        // Otachi steps back slightly after knocking the ship away
                        if (otachiTransform != null)
                        {
                            float ox = Mathf.Lerp(OtachiImpactX - 1.5f, -104f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(flyT / 1.8f)));
                            otachiTransform.position = new Vector3(ox, OtachiGroundY, StreetCenterZ);
                        }
                    }

                    UpdateKeelDragSparks(active: false, intensityScale: 0f);

                    // Wide side-angle profile action shot framing BOTH Gipsy (left) & Otachi (right) + plunging Oil Tanker crash into skyscraper!
                    float panFollow = triggeredTankerDisarm
                        ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t4 - 3.60f) / 0.85f))
                        : 0f;
                    Vector3 camPos = Vector3.Lerp(
                        new Vector3(-124.5f, 12.4f, -129.0f),
                        new Vector3(-142.0f, 15.8f, -125.5f),
                        panFollow);
                    Vector3 lookPt = Vector3.Lerp(
                        new Vector3(-125.0f, 14.2f, StreetCenterZ),
                        new Vector3(-111.0f, 20.5f, -86.5f),
                        panFollow * 0.92f);
                    camTargetPos = camPos;
                    camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                    camTargetFov = Mathf.Lerp(58f, 54f, panFollow);
                }
                // =========================================================================
                // SHOT 5 (0:25 – 0:30): DROPPING THE WRECK, STEAM VENT, COMBAT STANCE & FOGHORN
                // =========================================================================
                else
                {
                    float u = Mathf.Clamp01((elapsed - 25.0f) / 5.0f);

                    if (jaegerTransform != null)
                    {
                        jaegerTransform.position = new Vector3(JaegerStandX, JaegerGroundY, StreetCenterZ);
                        jaegerTransform.rotation = faceEast;
                    }
                    if (otachiTransform != null)
                    {
                        otachiTransform.position = new Vector3(-98f, OtachiGroundY, StreetCenterZ + 3f);
                        otachiTransform.rotation = faceWest;
                    }

                    rightHandIKWeight = 0f;
                    leftHandIKWeight = 0f;
                    hipsCoilYawDeg = 0f;
                    torsoCoilYawDeg = 0f;
                    chestPitchDeg = 0f;
                    otachiJawOpenDeg = 0f;

                    if (!triggeredShot5Steam)
                    {
                        triggeredShot5Steam = true;
                        // Ensure the Oil Tanker wreck sits embedded in the skyscraper facade during Shot 5
                        if (tankerRoot != null && !triggeredBuildingCrash)
                        {
                            tankerRoot.transform.position = CrashBuildingTarget;
                            tankerRoot.transform.rotation = Quaternion.LookRotation(new Vector3(0.28f, -0.34f, 0.90f).normalized, Vector3.up);
                        }
                        // Clear lingering Shot 4 combat burst streaks so Shot 5's hero frame is super crisp
                        for (int i = 0; i < shot4TempBursts.Count; i++)
                        {
                            if (shot4TempBursts[i] != null) Destroy(shot4TempBursts[i]);
                        }
                        shot4TempBursts.Clear();

                        if (jaegerAnim != null)
                        {
                            jaegerAnim.speed = 1.0f;
                            jaegerAnim.SetBool("InCombat", true);
                            jaegerAnim.CrossFadeInFixedTime("CombatIdle", 0.38f, 0);
                        }
                        if (otachiAnim != null)
                        {
                            otachiAnim.speed = 0.9f;
                            otachiAnim.CrossFadeInFixedTime("EMP", 0.40f, 0);
                        }
                        if (steamVentL != null)
                        {
                            var em = steamVentL.emission;
                            em.rateOverTime = 82f;
                            steamVentL.Play();
                        }
                        if (steamVentR != null)
                        {
                            var em = steamVentR.emission;
                            em.rateOverTime = 82f;
                            steamVentR.Play();
                        }
                        PacificRimAudioManager.Instance?.PlaySteamVent(jaegerTransform.position + Vector3.up * 20f);
                    }

                    // Taper steam vent after 2.2s
                    if (u > 0.44f)
                    {
                        float fadeSteam = Mathf.Clamp01(1f - (u - 0.44f) / 0.35f) * 55f;
                        if (steamVentL != null) { var em = steamVentL.emission; em.rateOverTime = fadeSteam; }
                        if (steamVentR != null) { var em = steamVentR.emission; em.rateOverTime = fadeSteam; }
                    }

                    // Deep Gipsy foghorn at 0:26.4!
                    if (!triggeredShot5Foghorn && elapsed >= 26.4f)
                    {
                        triggeredShot5Foghorn = true;
                        PacificRimAudioManager.Instance?.PlayGipsyFoghorn(jaegerTransform.position + Vector3.up * 18f);
                        camShake?.Shake(0.65f, 1.4f);
                    }

                    // Pulse chest reactor smoothly with foghorn (restrained realistic glow)
                    float pulse = triggeredShot5Foghorn ? 1.35f + 0.35f * Mathf.Sin((elapsed - 26.4f) * 5f) : 0.95f;
                    SetChestTurbineGlow(pulse);

                    // Low-angle hero shot looking up at Gipsy with Otachi recovering in the background
                    Vector3 camPos = new Vector3(
                        JaegerStandX + Mathf.Lerp(17.5f, 14.2f, u),
                        Mathf.Lerp(4.2f, 5.2f, u),
                        StreetCenterZ - Mathf.Lerp(9.5f, 7.2f, u));
                    Vector3 lookPt = new Vector3(JaegerStandX - 1.5f, 17.6f, StreetCenterZ + 0.5f);
                    camTargetPos = camPos;
                    camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                    camTargetFov = Mathf.Lerp(42f, 36f, u);
                }

                yield return null;
            }

            yield return StartCoroutine(FinishAndHandoffToCombat());
        }

        private void PositionTankerForDrag(float jaegerX, float jaegerZ, float time)
        {
            if (tankerRoot == null || tankerGripPrimary == null) return;

            // Place narrow stern right alongside Gipsy's right hand (~4.4m South of center, Y ~ 10.4m + subtle walk bob)
            float walkBob = Mathf.Sin(time * 5.2f) * 0.35f;
            Vector3 desiredGripPos = new Vector3(jaegerX - 0.8f, 10.4f + walkBob, jaegerZ - 4.4f);

            // Hull stretches upright behind Gipsy (-X) tilted smoothly downward (~19 deg pitch) so heavy keel gouges wet asphalt (Y ~ 0.15m)
            // Keep deck (+Y local) upright with minimal roll so the ship reads cleanly as an upright Oil Tanker!
            Vector3 shipFwd = new Vector3(0.935f, 0.335f, -0.115f).normalized;
            Quaternion rot = Quaternion.LookRotation(shipFwd, Vector3.up);

            Vector3 localGripOffset = rot * tankerGripPrimary.localPosition;
            tankerRoot.transform.rotation = rot;
            tankerRoot.transform.position = desiredGripPos - localGripOffset;
        }

        private void PositionTankerForBaseballBat(float jaegerX, float jaegerZ, float hoistU, float coilU, float swingDeg)
        {
            if (tankerRoot == null || tankerGripPrimary == null) return;

            // Starting drag pose
            Vector3 dragGripPos = new Vector3(jaegerX - 0.8f, 10.4f, jaegerZ - 4.4f);
            Quaternion dragRot = Quaternion.LookRotation(new Vector3(0.935f, 0.335f, -0.115f).normalized, Vector3.up);

            // Hoisted UPWARD baseball-bat pose:
            // During Shot 3 wind-up (swingDeg == 0), Gipsy holds the 32m Oil Tanker pitched sharply UPWARD (+62 deg above horizontal!)
            // over his right shoulder like a true baseball batter, gripping the narrow stern handle cleanly with both hands!
            // As swingDeg accelerates 0 -> 134 deg in Shot 4, the heavy hull drops smoothly from +62 deg upright down into
            // a level horizontal broadside smash (0 deg) across Otachi's head!
            Vector3 pivotCenter = new Vector3(jaegerX + 0.5f, 16.2f, jaegerZ);
            float baseAzimuth = Mathf.Lerp(-38f, -66f, coilU);
            float curAzimuth = baseAzimuth + swingDeg;
            float rad = curAzimuth * Mathf.Deg2Rad;

            float swingFrac = Mathf.Clamp01(swingDeg / 95f);
            Vector3 gripOffset = new Vector3(Mathf.Cos(rad) * 4.8f, Mathf.Lerp(1.6f, -0.3f, swingFrac), Mathf.Sin(rad) * 4.8f);
            Vector3 batGripPos = pivotCenter + gripOffset;

            // Compute barrel direction (pointing from hands OUT along the heavy Oil Tanker hull = local -Z):
            // Elevation starts high UPWARD (+62 deg when coiled) and whips down flat (0 deg) through the horizontal strike zone!
            float barrelElevDeg = Mathf.Lerp(Mathf.Lerp(46f, 64f, coilU), 0f, Mathf.SmoothStep(0f, 1f, swingFrac));
            float barrelAzimRad = (curAzimuth - Mathf.Lerp(26f, 48f, swingFrac)) * Mathf.Deg2Rad;
            float cosE = Mathf.Cos(barrelElevDeg * Mathf.Deg2Rad);
            float sinE = Mathf.Sin(barrelElevDeg * Mathf.Deg2Rad);
            Vector3 barrelDir = new Vector3(Mathf.Cos(barrelAzimRad) * cosE, sinE, Mathf.Sin(barrelAzimRad) * cosE).normalized;

            // Ship local +Z points toward the narrow grip handle (-barrelDir); use a stable upHint orthogonal to barrelDir
            Vector3 upHint = Mathf.Abs(Vector3.Dot(-barrelDir, Vector3.up)) > 0.92f ? Vector3.right : Vector3.up;
            Quaternion batRot = Quaternion.LookRotation(-barrelDir, upHint);

            Vector3 finalGripPos = Vector3.Lerp(dragGripPos, batGripPos, hoistU);
            Quaternion finalRot = Quaternion.Slerp(dragRot, batRot, hoistU);

            tankerRoot.transform.rotation = finalRot;
            tankerRoot.transform.position = finalGripPos - (finalRot * tankerGripPrimary.localPosition);
        }

        private void UpdateKeelDragSparks(bool active, float intensityScale)
        {
            if (tankerKeelTip == null) return;
            Vector3 keelWorld = tankerKeelTip.position;
            keelWorld.y = Mathf.Max(0.22f, keelWorld.y);

            if (dragSparksPS != null)
            {
                dragSparksPS.transform.position = keelWorld;
                // Spray rooster-tail sparks backward (-X) and upward!
                dragSparksPS.transform.rotation = Quaternion.LookRotation(new Vector3(-0.92f, 0.36f, -0.15f).normalized, Vector3.up);
                var em = dragSparksPS.emission;
                em.rateOverTime = active ? (280f * intensityScale) : 0f;
                if (active && !dragSparksPS.isPlaying) dragSparksPS.Play();
            }
            if (dragDustPS != null)
            {
                dragDustPS.transform.position = keelWorld + Vector3.up * 0.4f;
                dragDustPS.transform.rotation = Quaternion.LookRotation(new Vector3(-0.95f, 0.25f, 0f).normalized, Vector3.up);
                var em = dragDustPS.emission;
                em.rateOverTime = active ? (38f * intensityScale) : 0f;
                if (active && !dragDustPS.isPlaying) dragDustPS.Play();
            }
            if (dragSparkLight != null)
            {
                dragSparkLight.transform.position = keelWorld + Vector3.up * 1.4f;
                float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 24f, 1.3f);
                dragSparkLight.intensity = active ? (6500f * intensityScale * flicker) : 0f;
            }
        }

        private void SetChestTurbineGlow(float intensity)
        {
            if (turbineMatInst != null && turbineMatInst.HasProperty(IdEmissionColor))
            {
                // Crisp fiery orange core strictly restrained below torso-bleaching levels!
                Color hdr = new Color(1.0f, 0.36f, 0.04f, 1f) * Mathf.Clamp(intensity * 1.15f, 0.35f, 2.05f);
                turbineMatInst.SetColor(IdEmissionColor, hdr);
            }
            if (chestReactorLight != null)
            {
                chestReactorLight.intensity = Mathf.Clamp(intensity * 95f, 0f, 220f);
            }
        }

        private void TriggerImpactBurst(Vector3 pos, Color flashCol, float flashAlpha, float shakeMag, float shakeDur)
        {
            impactFlashColor = flashCol;
            impactFlashAlpha = Mathf.Max(impactFlashAlpha, flashAlpha);
            impactLightPos = pos;
            impactLightIntensity = 3600f;
            camShake?.Shake(shakeMag, shakeDur);
        }

        private void SpawnSparksBurst(Vector3 pos, int count, bool isCyanBlood)
        {
            GameObject go = new GameObject(isCyanBlood ? "Intro_KaijuBloodBurst" : "Intro_ImpactSparksBurst");
            go.transform.position = pos;
            spawnedRuntimeObjects.Add(go);
            shot4TempBursts.Add(go);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(18f, 48f);
            main.startSize = isCyanBlood
                ? new ParticleSystem.MinMaxCurve(0.55f, 1.65f)
                : new ParticleSystem.MinMaxCurve(0.24f, 0.65f);
            main.gravityModifier = 2.2f;
            main.startColor = isCyanBlood
                ? new ParticleSystem.MinMaxGradient(new Color(0.10f, 0.92f, 1.0f, 0.95f), new Color(0.28f, 1.0f, 0.85f, 0.9f))
                : new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.65f, 0.14f, 1f), new Color(1.0f, 0.95f, 0.55f, 1f));

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 2.2f;

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            if (!isCyanBlood)
            {
                ren.renderMode = ParticleSystemRenderMode.Stretch;
                ren.velocityScale = 0.08f;
                ren.lengthScale = 3.0f;
                ren.sharedMaterial = HarborToNeonCutsceneShared.CreateParticleMaterial(HarborToNeonCutsceneShared.GetSparkStreakTexture(), true);
            }
            else
            {
                ren.sharedMaterial = HarborToNeonCutsceneShared.CreateParticleMaterial(HarborToNeonCutsceneShared.GetSoftCircleTexture(), true);
            }

            ps.Play();
        }

        private void SpawnBuildingCrashSmoke(Vector3 pos)
        {
            crashSmokePS = HarborToNeonCutsceneShared.CreateWaterSprayWallEmitter("Intro_BuildingCrashSmoke", null, 6f, 22f);
            crashSmokePS.transform.position = pos;
            crashSmokePS.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.35f, -0.94f).normalized, Vector3.up);
            var main = crashSmokePS.main;
            main.startSize = new ParticleSystem.MinMaxCurve(4.2f, 9.5f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
            main.gravityModifier = -0.25f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.32f, 0.34f, 0.38f, 0.72f),
                new Color(0.85f, 0.45f, 0.18f, 0.42f));
            var em = crashSmokePS.emission;
            em.rateOverTime = 34f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });
            crashSmokePS.Play();
            spawnedRuntimeObjects.Add(crashSmokePS.gameObject);
        }

        private IEnumerator FinishAndHandoffToCombat()
        {
            // If skipped early, snap fade to black cleanly over 0.25s first
            if (skipRequested && letterbox != null && letterbox.FadeOverlay != null)
            {
                float t = 0f;
                float startA = letterbox.FadeOverlay.color.a;
                while (t < 0.22f)
                {
                    t += Time.unscaledDeltaTime;
                    letterbox.FadeOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(startA, 1f, t / 0.22f));
                    yield return null;
                }
                letterbox.FadeOverlay.color = Color.black;
            }

            driveCamera = false;
            rightHandIKWeight = 0f;
            leftHandIKWeight = 0f;
            hipsCoilYawDeg = 0f;
            torsoCoilYawDeg = 0f;
            chestPitchDeg = 0f;
            otachiJawOpenDeg = 0f;
            RestoreTurbineMaterial();

            // Re-enable hidden street vehicles
            for (int i = 0; i < hiddenCorridorProps.Count; i++)
            {
                if (hiddenCorridorProps[i] != null) hiddenCorridorProps[i].SetActive(true);
            }
            hiddenCorridorProps.Clear();

            // Place Gipsy & Otachi cleanly in Central Transit Plaza for the live boss fight
            if (jaegerTransform != null)
            {
                jaegerTransform.position = combatJaegerPos;
                jaegerTransform.rotation = combatJaegerRot;
            }
            if (jaegerRb != null)
            {
                jaegerRb.position = combatJaegerPos;
                jaegerRb.rotation = combatJaegerRot;
                jaegerRb.isKinematic = false;
                jaegerRb.linearVelocity = Vector3.zero;
                jaegerRb.angularVelocity = Vector3.zero;
            }
            if (jaegerAnim != null)
            {
                jaegerAnim.speed = 1.0f;
                jaegerAnim.SetFloat("Speed", 0f);
                jaegerAnim.SetBool("InCombat", true);
                jaegerAnim.CrossFadeInFixedTime("CombatIdle", 0.12f, 0);
            }

            if (otachiTransform != null)
            {
                otachiTransform.position = combatOtachiPos;
                otachiTransform.rotation = combatOtachiRot;
            }
            if (otachiRb != null)
            {
                otachiRb.position = combatOtachiPos;
                otachiRb.rotation = combatOtachiRot;
                otachiRb.isKinematic = false;
                otachiRb.linearVelocity = Vector3.zero;
                otachiRb.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
            if (otachiAnim != null)
            {
                otachiAnim.speed = 1.0f;
                otachiAnim.SetFloat("LocomotionSpeed", 1.0f);
                otachiAnim.CrossFadeInFixedTime("Idle", 0.12f, 0);
            }

            // Restore gameplay camera
            if (mainCam != null)
            {
                mainCam.fieldOfView = savedCamFov;
            }
            if (camFollow != null)
            {
                camFollow.CurrentYaw = 0f;
                camFollow.enabled = savedCamFollowEnabled;
            }

            // Destroy temporary emitters/lights (keep Oil Tanker wedged in building ONLY if crash actually occurred!)
            bool keepWreck = !skipRequested && tankerRoot != null && elapsed >= 23.6f;
            CleanupRuntimeObjects(keepEmbeddedTanker: keepWreck);

            // Unlock gameplay controls & show HUD
            if (jaegerCtrl != null) jaegerCtrl.isCutsceneActive = false;
            if (otachiAI != null) otachiAI.isCutsceneMode = false;
            if (JaegerHUD.Instance != null) JaegerHUD.Instance.SetCutsceneHidden(false);

            isRunning = false;
            hasFinished = true;

            // Fade black & letterbox out cleanly over 0.55s into live combat
            if (letterbox != null && letterbox.Canvas != null)
            {
                float fadeT = 0f;
                while (fadeT < 0.55f)
                {
                    fadeT += Time.unscaledDeltaTime;
                    float a = 1f - Mathf.Clamp01(fadeT / 0.55f);
                    if (letterbox.Group != null) letterbox.Group.alpha = a;
                    yield return null;
                }
                Destroy(letterbox.Canvas.gameObject);
                letterbox = null;
            }
        }

        private void RestoreTurbineMaterial()
        {
            if (turbineMatInst != null && hasSavedTurbineEmission && turbineMatInst.HasProperty(IdEmissionColor))
            {
                turbineMatInst.SetColor(IdEmissionColor, savedTurbineEmission);
            }
        }

        private void CleanupRuntimeObjects(bool keepEmbeddedTanker)
        {
            for (int i = 0; i < spawnedRuntimeObjects.Count; i++)
            {
                GameObject go = spawnedRuntimeObjects[i];
                if (go == null) continue;
                if (letterbox != null && letterbox.Canvas != null && go == letterbox.Canvas.gameObject) continue;
                if (keepEmbeddedTanker && go == tankerRoot) continue;
                Destroy(go);
            }
            spawnedRuntimeObjects.Clear();
        }
    }
}
