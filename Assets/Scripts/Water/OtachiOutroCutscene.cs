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
    /// Level 2 Outro Cutscene (~45 Seconds): Hong Kong Finale (Stratosphere Drop & Chain Sword).
    ///
    /// Beat Breakdown:
    /// - 0:00–0:06 | The Wing Reveal & Ambush (Hong Kong Streets): Low-angle medium shot on collapsed Otachi;
    ///               dorsal plates flare and massive leathery wings burst outward (FlyingOtachi dual-layer rig:
    ///               BaseOtachi body clips + Armature|Flying layered on UpperArm/LowerArm/WingFingers). Otachi
    ///               lunges, clamps onto Gipsy's shoulders/torso, and rips him straight into the night sky.
    /// - 0:06–0:13 | Punching Through the Storm Clouds (Vertical Ascent): Dynamic upward-tilted orbiting camera
    ///               beneath them as they rocket out of dark stormy clouds into blinding golden sunlight above
    ///               a sunlit cloud deck with wings beating furiously.
    /// - 0:13–0:20 | The Stratosphere & Frost (Edge of Space): Wide establishing shot in deep near-black indigo
    ///               upper stratosphere over Earth's glowing blue curved limb, cutting to a tight close-up on
    ///               Gipsy's Conn-Pod visor as dendritic ice crystals & frost rapidly spiderweb across the glass.
    /// - 0:20–0:25 | Right-Arm Chain Sword Deployment: Tight tracking close-up on Gipsy's right forearm as
    ///               hydraulic locks disengage and the segmented Chain Sword whips rigid into a razor-sharp blade.
    /// - 0:25–0:32 | The Slow-Motion Stratosphere Slice: Medium-wide 3/4 profile shot against Earth's curvature;
    ///               dramatic slow-motion upward right-arm slash cleanly bisecting FlyingOtachi into two baked
    ///               separating halves as bioluminescent cyan Kaiju blood sprays and freezes into glittering crystals.
    /// - 0:32–0:38 | Terminal Velocity Freefall: Full speed resumes; OTS vertical drop camera looking straight down
    ///               past plummeting Gipsy punching back through the cloud layer toward Hong Kong's neon grid below.
    /// - 0:38–0:45 | Chest Reactor Retro-Thruster & Touchdown: Dramatic ground-level low-angle shot looking up into
    ///               the storm as Gipsy pitches backward mid-air and triggers his Nuclear Vortex Turbine purge —
    ///               emitting a huge downward spotlight cone + volumetric orange-blue flame trail illuminating the
    ///               entire ground arena before slamming down in a dust/steam shockwave, then fading to Level 2 Victory.
    /// </summary>
    [DefaultExecutionOrder(600)]
    public class OtachiOutroCutscene : MonoBehaviour
    {
        public static OtachiOutroCutscene Instance { get; private set; }
        public static bool HasSceneCutscene => Instance != null;
        public static bool IsCutscenePlaying => Instance != null && Instance.isRunning;

        [Header("Scene Actors")]
        public Transform jaegerTransform;
        public Transform otachiTransform;

        [Header("Flying Otachi & Cutscene Assets")]
        public GameObject flyingOtachiPrefab;
        public RuntimeAnimatorController flyingOtachiController;
        public Material otachiMaterial;
        public Material volumetricBeamMaterial;
        public Mesh volumetricBeamConeMesh;

        [Header("Playback Settings")]
        public float totalDuration = 45.0f;

        // Spatial anchors
        private const float JaegerGroundY = 2.977f;
        private const float OtachiGroundY = 0.199f;
        private const float CloudDeckY = 268f;
        private const float StratosphereY = 950f;

        private bool isRunning;
        private bool skipRequested;
        private float elapsed;

        // Cached scene systems & saved atmosphere state
        private SimpleJaegerController jaegerCtrl;
        private JaegerVisualSockets visualSockets;
        private Animator jaegerAnim;
        private Rigidbody jaegerRb;
        private KaijuAI otachiAI;
        private Animator otachiAnim;
        private Rigidbody otachiRb;

        private Camera mainCam;
        private JaegerCameraFollow camFollow;
        private PacificRimCameraShake camShake;
        private AudioLowPassFilter camLowPass;
        private AtmosphereVolumeController atmoCtrl;
        private StormWeatherManager stormMgr;
        private LightningSystem lightningSys;
        private ChinookCombatPatrolSquadron chinookSquad;
        private Light mainDirLight;

        private bool savedCamFollowEnabled;
        private float savedCamFov = 60f;
        private float savedCamFarClip = 1200f;
        private Color savedCamBgColor;
        private bool savedFog;
        private Color savedFogColor;
        private float savedFogDensity;
        private Color savedAmbSky, savedAmbEq, savedAmbGround;
        private Color savedDirLightColor;
        private float savedDirLightIntensity;
        private Quaternion savedDirLightRot;
        private bool savedRainActive;
        private bool savedLightningEnabled;
        private bool savedChinookActive;
        private float savedLowPassCutoff = 22000f;
        private bool addedLowPass;

        // Jaeger bones & renderers
        private Transform jHips, jSpine1, jChest, jHead;
        private Transform jUpperArmL, jForearmL, jHandL;
        private Transform jUpperArmR, jForearmR, jHandR;
        private Transform jThighL, jShinL, jFootL;
        private Transform jThighR, jShinR, jFootR;
        private Transform jRightArmSword, jLeftArmSword;
        private Vector3 savedSwordPosR;
        private Quaternion savedSwordRotR;
        private Vector3 savedSwordScaleR;
        private bool hasSavedSwordPoseR;
        private Renderer turbineRenderer;
        private Material turbineMatInst;
        private Color savedTurbineEmission = Color.black;
        private bool hasSavedTurbineEmission;
        private Renderer visorRenderer;
        private Material visorMatInst;
        private Color savedVisorEmission = Color.black;
        private Texture savedVisorBaseMap;
        private Texture savedVisorEmissionMap;
        private Color savedVisorBaseColor = Color.white;
        private bool hasSavedVisorEmission;

        // FlyingOtachi runtime rig + talon bones + bisected halves
        private GameObject flyingOtachiGo;
        private Animator flyingOtachiAnim;
        private SkinnedMeshRenderer flyingOtachiSmr;
        private Transform oAnkleL, oAnkleR, oToeL, oToeR;
        private GameObject otachiUpperHalfGo;
        private GameObject otachiLowerHalfGo;
        private Vector3 upperHalfPos, lowerHalfPos;
        private Quaternion upperHalfRot, lowerHalfRot;
        private bool hasBisectedOtachi;
        private float talonClampTargetWeight;
        private float smoothedTalonClampWeight;

        // Procedural backdrop, frost, sword glow & chest retro-thruster rig
        private HarborToNeonCutsceneShared.LetterboxRig letterbox;
        private readonly List<GameObject> spawnedObjects = new List<GameObject>();
        private GameObject cloudDeckGroup;
        private GameObject goldenCloudSeaGo;
        private GameObject stratosphereBackdropGroup;
        private GameObject visorFrostQuad;
        private Material visorFrostMat;
        private Texture2D visorFrostTex;
        private ParticleSystem visorIceMotesPS;
        private ParticleSystem windStreaksPS;
        private ParticleSystem cloudPunchPS;
        private ParticleSystem swordSparksPS;
        private Light swordGlowLight;
        private GameObject swordEdgeGlowBar;
        private Material swordEdgeGlowMat;

        // Shot 7 Chest Reactor Pseudo-Rocket Rig
        private GameObject retroThrusterRoot;
        private Light retroDownSpotLight;
        private Light retroArenaBounceLight;
        private Transform retroCoreConeTr;
        private Transform retroOuterFlameConeTr;
        private Material retroCoreMat;
        private Material retroOuterMat;
        private ParticleSystem retroPlasmaJetPS;
        private ParticleSystem retroGroundWashPS;

        // LateUpdate procedural pose & camera state
        private bool driveCamera;
        private Vector3 camTargetPos;
        private Quaternion camTargetRot = Quaternion.identity;
        private float camTargetFov = 48f;

        private float rightHandIKWeight;
        private float leftHandIKWeight;
        private float smoothedRightIKWeight;
        private float smoothedLeftIKWeight;
        private Vector3 rightHandTargetWorld;
        private Vector3 smoothedRightHandTarget;
        private Vector3 rightElbowPoleWorld;
        private Vector3 leftHandTargetWorld;
        private Vector3 smoothedLeftHandTarget;
        private Vector3 leftElbowPoleWorld;
        private bool hasInitializedHandTargets;
        private float torsoYawDeg;
        private float chestPitchDeg;
        private float smoothedTorsoYaw;
        private float smoothedChestPitch;
        private float touchdownCrouchWeight;
        private float smoothedCrouchWeight;
        private Vector3 crouchFootTargetL, crouchFootTargetR;
        private Vector3 crouchKneePoleL, crouchKneePoleR;
        private float smoothedWingSpeed = 1.5f;
        private float smoothedAnimSpeed = 1.0f;

        private bool driveRightSword;
        private float rightSwordDeployT; // 0 = retracted, 1 = fully extended
        private float rightSwordWhipDeg;

        private float impactFlashAlpha;
        private Color impactFlashColor = Color.white;

        private static readonly int IdEmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int IdColor = Shader.PropertyToID("_Color");

        public static void StartFromOtachiDefeat()
        {
            if (Instance != null)
            {
                Instance.BeginOutroSequence();
                return;
            }

            var existing = FindAnyObjectByType<OtachiOutroCutscene>();
            if (existing != null)
            {
                existing.BeginOutroSequence();
                return;
            }

            // Fallback: spawn runtime controller if somehow absent
            var go = new GameObject("Otachi_OutroCutscene_Runtime");
            var comp = go.AddComponent<OtachiOutroCutscene>();
            comp.BeginOutroSequence();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            RestoreAllSystems();
            CleanupSpawnedObjects();
            if (Instance == this) Instance = null;
        }

        public void BeginOutroSequence()
        {
            if (isRunning) return;
            ResolveReferences();
            isRunning = true;
            skipRequested = false;
            StartCoroutine(RunOutroRoutine());
        }

        /// <summary>
        /// Editor/test helper to jump cleanly to any timestamp during Play Mode inspection.
        /// </summary>
        public void DebugSeekTo(float seconds)
        {
            elapsed = Mathf.Clamp(seconds, 0f, totalDuration - 0.05f);
        }

        private void Update()
        {
            if (!isRunning) return;

            if (elapsed >= 0.6f && HarborToNeonCutsceneShared.IsSkipPressed())
            {
                skipRequested = true;
            }
        }

        private void LateUpdate()
        {
            if (!isRunning) return;

            // Critically-damped runtime smoothing on all IK weights, targets, torso overrides, crouch & talon clamp
            // so animation exchanges and pose transitions flow seamlessly with ZERO abrupt snapping!
            float dt = Time.deltaTime;
            float weightRate = 1f - Mathf.Exp(-9.5f * dt);
            float targetRate = 1f - Mathf.Exp(-14f * dt);
            float crouchRate = 1f - Mathf.Exp(-11f * dt);
            float clampRate = 1f - Mathf.Exp(-7.5f * dt);

            smoothedRightIKWeight = Mathf.Lerp(smoothedRightIKWeight, rightHandIKWeight, weightRate);
            smoothedLeftIKWeight = Mathf.Lerp(smoothedLeftIKWeight, leftHandIKWeight, weightRate);
            smoothedTorsoYaw = Mathf.Lerp(smoothedTorsoYaw, torsoYawDeg, weightRate);
            smoothedChestPitch = Mathf.Lerp(smoothedChestPitch, chestPitchDeg, weightRate);
            smoothedCrouchWeight = Mathf.Lerp(smoothedCrouchWeight, touchdownCrouchWeight, crouchRate);
            smoothedTalonClampWeight = Mathf.Lerp(smoothedTalonClampWeight, talonClampTargetWeight, clampRate);

            if (!hasInitializedHandTargets)
            {
                smoothedRightHandTarget = rightHandTargetWorld;
                smoothedLeftHandTarget = leftHandTargetWorld;
                hasInitializedHandTargets = true;
            }
            else
            {
                // Snap target immediately if altitude teleported between stages (e.g., Clouds -> Stratosphere) to prevent streaking!
                if (Vector3.SqrMagnitude(smoothedRightHandTarget - rightHandTargetWorld) > 2500f)
                    smoothedRightHandTarget = rightHandTargetWorld;
                else
                    smoothedRightHandTarget = Vector3.Lerp(smoothedRightHandTarget, rightHandTargetWorld, targetRate);

                if (Vector3.SqrMagnitude(smoothedLeftHandTarget - leftHandTargetWorld) > 2500f)
                    smoothedLeftHandTarget = leftHandTargetWorld;
                else
                    smoothedLeftHandTarget = Vector3.Lerp(smoothedLeftHandTarget, leftHandTargetWorld, targetRate);
            }

            // 1. Procedural torso twist / pitch overrides (including forward lean during touchdown crouch)
            float effectivePitch = smoothedChestPitch + smoothedCrouchWeight * 28f;
            if (jSpine1 != null && (Mathf.Abs(smoothedTorsoYaw) > 0.01f || smoothedCrouchWeight > 0.01f))
            {
                Vector3 upAxis = jaegerTransform != null ? jaegerTransform.up : Vector3.up;
                Vector3 rightAxis = jaegerTransform != null ? jaegerTransform.right : Vector3.right;
                jSpine1.rotation = Quaternion.AngleAxis(smoothedTorsoYaw * 0.45f, upAxis)
                                 * Quaternion.AngleAxis(smoothedCrouchWeight * 14f, rightAxis)
                                 * jSpine1.rotation;
            }
            if (jChest != null && (Mathf.Abs(smoothedTorsoYaw) > 0.01f || Mathf.Abs(effectivePitch) > 0.01f))
            {
                Vector3 upAxis = jaegerTransform != null ? jaegerTransform.up : Vector3.up;
                Vector3 rightAxis = jaegerTransform != null ? jaegerTransform.right : Vector3.right;
                jChest.rotation = Quaternion.AngleAxis(smoothedTorsoYaw * 0.55f, upAxis)
                                * Quaternion.AngleAxis(effectivePitch, rightAxis)
                                * jChest.rotation;
            }

            // 1b. Smooth bone-driven Talon-to-Torso clamp: align FlyingOtachi's lower gripping claws directly onto
            // Gipsy's upward-facing chest/torso while Gipsy lies flat horizontally (back facing downwards)!
            if (smoothedTalonClampWeight > 0.001f && !hasBisectedOtachi && flyingOtachiGo != null && flyingOtachiGo.activeSelf && jChest != null)
            {
                Vector3 gripMid;
                if (oAnkleL != null && oAnkleR != null && oToeL != null && oToeR != null)
                {
                    Vector3 ankleMid = (oAnkleL.position + oAnkleR.position) * 0.5f;
                    Vector3 toeMid = (oToeL.position + oToeR.position) * 0.5f;
                    gripMid = Vector3.Lerp(ankleMid, toeMid, 0.85f);
                }
                else
                {
                    gripMid = flyingOtachiGo.transform.position;
                }
                // Place Otachi's gripping claws squarely on Gipsy's upward-facing chest/ribs so her belly hovers cleanly above!
                Vector3 desiredTorsoClamp = jChest.position + Vector3.up * 1.45f;
                Vector3 delta = desiredTorsoClamp - gripMid;
                flyingOtachiGo.transform.position += delta * smoothedTalonClampWeight;
            }

            // 1c. Touchdown deep superhero crouch -> slow hydraulic rise into battle pose (smoothed 2-bone leg IK!)
            if (smoothedCrouchWeight > 0.001f && jThighL != null && jShinL != null && jFootL != null && jThighR != null && jShinR != null && jFootR != null)
            {
                HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(jThighL, jShinL, jFootL, crouchFootTargetL, crouchKneePoleL, smoothedCrouchWeight);
                HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(jThighR, jShinR, jFootR, crouchFootTargetR, crouchKneePoleR, smoothedCrouchWeight);
            }

            // 2. Smoothed 2-bone IK for right and left arms
            if (smoothedRightIKWeight > 0.001f && jUpperArmR != null && jForearmR != null && jHandR != null)
            {
                HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(jUpperArmR, jForearmR, jHandR, smoothedRightHandTarget, rightElbowPoleWorld, smoothedRightIKWeight);
            }
            if (smoothedLeftIKWeight > 0.001f && jUpperArmL != null && jForearmL != null && jHandL != null)
            {
                HarborToNeonCutsceneShared.SolveTwoBoneIKWorld(jUpperArmL, jForearmL, jHandL, smoothedLeftHandTarget, leftElbowPoleWorld, smoothedLeftIKWeight);
            }

            // 3. Drive Right-Arm Chain Sword (Hand.R/LeftSword) exclusively; keep left arm sword hidden!
            if (jLeftArmSword != null && jLeftArmSword.gameObject.activeSelf)
            {
                jLeftArmSword.gameObject.SetActive(false);
            }
            if (jRightArmSword != null && hasSavedSwordPoseR)
            {
                if (!driveRightSword || rightSwordDeployT <= 0.001f)
                {
                    if (jRightArmSword.gameObject.activeSelf) jRightArmSword.gameObject.SetActive(false);
                }
                else
                {
                    if (!jRightArmSword.gameObject.activeSelf) jRightArmSword.gameObject.SetActive(true);
                    Vector3 retractOffset = new Vector3(0.014f, 0f, 0f);
                    float xScale = Mathf.Lerp(0.02f, 1.0f, rightSwordDeployT);
                    float yzScale = Mathf.Lerp(0.35f, 1.0f, Mathf.Clamp01(rightSwordDeployT * 1.4f));
                    jRightArmSword.localPosition = Vector3.Lerp(savedSwordPosR + retractOffset, savedSwordPosR, rightSwordDeployT);
                    jRightArmSword.localRotation = savedSwordRotR * Quaternion.Euler(0f, rightSwordWhipDeg * 0.6f, rightSwordWhipDeg);
                    jRightArmSword.localScale = Vector3.Scale(savedSwordScaleR, new Vector3(xScale, yzScale, yzScale));
                }
            }

            // 4. Keep visor frost quad & sword glow aligned to animated bones
            // Unity Quad front normal points along local -Z, so orient local +Z away from camera (-headForward) right over Visor glass!
            if (visorFrostQuad != null && visorFrostQuad.activeSelf && jHead != null && jaegerTransform != null)
            {
                Vector3 headForward = jaegerTransform.forward;
                Vector3 visorPos = visorRenderer != null
                    ? visorRenderer.bounds.center + headForward * 0.68f
                    : (jHead.position + headForward * 1.25f + Vector3.up * 0.55f);
                visorFrostQuad.transform.position = visorPos;
                visorFrostQuad.transform.rotation = Quaternion.LookRotation(-headForward, Vector3.up);
            }

            if (swordGlowLight != null && jRightArmSword != null && jRightArmSword.gameObject.activeSelf)
            {
                Vector3 bladeCenter = jRightArmSword.TransformPoint(new Vector3(-0.16f, 0f, 0f));
                swordGlowLight.transform.position = bladeCenter;
                if (swordSparksPS != null)
                {
                    swordSparksPS.transform.position = jRightArmSword.TransformPoint(new Vector3(-0.28f * rightSwordDeployT, 0f, 0f));
                }
            }

            // 5. Keep Chest Retro-Thruster root locked to Gipsy's downward-facing chest turbine blasting straight into Central Plaza!
            if (retroThrusterRoot != null && retroThrusterRoot.activeSelf && jChest != null && jaegerTransform != null)
            {
                // Since Gipsy pitches belly/face DOWNWARD (+80 deg around +X), jaegerTransform.forward points straight DOWN toward the ground!
                Vector3 chestDir = jaegerTransform.forward;
                Vector3 chestCenter = jChest.position + chestDir * 1.95f;
                retroThrusterRoot.transform.position = chestCenter;
                Vector3 blastDir = Vector3.Lerp(chestDir, Vector3.down, 0.55f).normalized;
                retroThrusterRoot.transform.rotation = Quaternion.LookRotation(blastDir, Vector3.forward);
            }

            // 6. Drive cinematic camera + shake
            if (driveCamera && mainCam != null)
            {
                Vector3 shakePos = camShake != null ? camShake.CurrentOffset : Vector3.zero;
                Vector3 shakeRot = camShake != null ? camShake.CurrentRotationOffset : Vector3.zero;
                mainCam.transform.position = camTargetPos + shakePos;
                mainCam.transform.rotation = camTargetRot * Quaternion.Euler(shakeRot);
                mainCam.fieldOfView = camTargetFov;
                HarborToNeonCutsceneShared.SyncCameraRain(mainCam);
            }

            // 7. Decay impact flash overlay
            if (letterbox != null && letterbox.FlashOverlay != null)
            {
                if (impactFlashAlpha > 0.001f)
                {
                    impactFlashAlpha = Mathf.MoveTowards(impactFlashAlpha, 0f, Time.deltaTime * 2.5f);
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
            }

            mainCam = Camera.main;
            if (mainCam == null)
            {
                var cgo = GameObject.FindWithTag("MainCamera");
                if (cgo != null) mainCam = cgo.GetComponent<Camera>();
            }
            if (mainCam != null)
            {
                camFollow = mainCam.GetComponent<JaegerCameraFollow>();
                camShake = mainCam.GetComponent<PacificRimCameraShake>();
                if (camShake == null) camShake = mainCam.gameObject.AddComponent<PacificRimCameraShake>();
                camLowPass = mainCam.GetComponent<AudioLowPassFilter>();
                if (camLowPass == null)
                {
                    camLowPass = mainCam.gameObject.AddComponent<AudioLowPassFilter>();
                    camLowPass.cutoffFrequency = 22000f;
                    addedLowPass = true;
                }
            }

            if (atmoCtrl == null) atmoCtrl = FindAnyObjectByType<AtmosphereVolumeController>();
            if (stormMgr == null) stormMgr = FindAnyObjectByType<StormWeatherManager>();
            if (lightningSys == null) lightningSys = FindAnyObjectByType<LightningSystem>();
            if (chinookSquad == null) chinookSquad = FindAnyObjectByType<ChinookCombatPatrolSquadron>();

            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (l != null && l.type == LightType.Directional && l.gameObject.name != "LightningFlashLight")
                {
                    mainDirLight = l;
                    break;
                }
            }

#if UNITY_EDITOR
            if (flyingOtachiPrefab == null)
                flyingOtachiPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/FlyingOtachi.fbx");
            if (flyingOtachiController == null)
                flyingOtachiController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/FlyingOtachi_Controller.controller");
            if (otachiMaterial == null)
                otachiMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_Otachi.mat");
            if (volumetricBeamMaterial == null)
                volumetricBeamMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_VolumetricBeam.mat");
            if (volumetricBeamConeMesh == null)
                volumetricBeamConeMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/PacificRim_VolumetricBeamCone.asset");
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
                    case "Thigh.L": jThighL = t; break;
                    case "Shin.L": jShinL = t; break;
                    case "Foot.L": jFootL = t; break;
                    case "Thigh.R": jThighR = t; break;
                    case "Shin.R": jShinR = t; break;
                    case "Foot.R": jFootR = t; break;
                    case "LeftSword": jRightArmSword = t; break; // Physically under Hand.R
                    case "RightSword": jLeftArmSword = t; break; // Physically under Hand.L
                }
            }

            if (jRightArmSword != null && !hasSavedSwordPoseR)
            {
                savedSwordPosR = jRightArmSword.localPosition;
                savedSwordRotR = jRightArmSword.localRotation;
                savedSwordScaleR = jRightArmSword.localScale;
                hasSavedSwordPoseR = true;
            }

            var turbTr = jaegerTransform.Find("Spinning Blade.002");
            if (turbTr != null) turbineRenderer = turbTr.GetComponent<Renderer>();
            var visorTr = jaegerTransform.Find("Visor");
            if (visorTr != null) visorRenderer = visorTr.GetComponent<Renderer>();
        }

        private void SaveAndLockSceneSystems()
        {
            if (PacificRimAdminUI.IsOpen) PacificRimAdminUI.Close();
            if (JaegerHUD.Instance != null) JaegerHUD.Instance.SetCutsceneHidden(true);

            if (jaegerCtrl != null) jaegerCtrl.isCutsceneActive = true;
            if (otachiAI != null) otachiAI.isCutsceneMode = true;

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

            if (camFollow != null)
            {
                savedCamFollowEnabled = camFollow.enabled;
                camFollow.enabled = false;
            }
            if (mainCam != null)
            {
                savedCamFov = mainCam.fieldOfView;
                savedCamFarClip = mainCam.farClipPlane;
                savedCamBgColor = mainCam.backgroundColor;
                mainCam.farClipPlane = 8500f;
            }
            if (camLowPass != null) savedLowPassCutoff = camLowPass.cutoffFrequency;

            savedFog = RenderSettings.fog;
            savedFogColor = RenderSettings.fogColor;
            savedFogDensity = RenderSettings.fogDensity;
            savedAmbSky = RenderSettings.ambientSkyColor;
            savedAmbEq = RenderSettings.ambientEquatorColor;
            savedAmbGround = RenderSettings.ambientGroundColor;

            if (mainDirLight != null)
            {
                savedDirLightColor = mainDirLight.color;
                savedDirLightIntensity = mainDirLight.intensity;
                savedDirLightRot = mainDirLight.transform.rotation;
            }
            if (stormMgr != null && stormMgr.rainParticles != null)
                savedRainActive = stormMgr.rainParticles.gameObject.activeSelf;
            if (lightningSys != null)
                savedLightningEnabled = lightningSys.enabled;
            if (chinookSquad != null)
                savedChinookActive = chinookSquad.gameObject.activeSelf;

            if (turbineRenderer != null && turbineMatInst == null)
            {
                turbineMatInst = turbineRenderer.material;
                if (turbineMatInst != null && turbineMatInst.HasProperty(IdEmissionColor))
                {
                    savedTurbineEmission = turbineMatInst.GetColor(IdEmissionColor);
                    hasSavedTurbineEmission = true;
                }
            }
            if (visorRenderer != null && visorMatInst == null)
            {
                visorMatInst = visorRenderer.material;
                if (visorMatInst != null)
                {
                    if (visorMatInst.HasProperty(IdEmissionColor))
                    {
                        savedVisorEmission = visorMatInst.GetColor(IdEmissionColor);
                        hasSavedVisorEmission = true;
                    }
                    if (visorMatInst.HasProperty("_BaseMap")) savedVisorBaseMap = visorMatInst.GetTexture("_BaseMap");
                    if (visorMatInst.HasProperty("_EmissionMap")) savedVisorEmissionMap = visorMatInst.GetTexture("_EmissionMap");
                    if (visorMatInst.HasProperty(IdBaseColor)) savedVisorBaseColor = visorMatInst.GetColor(IdBaseColor);
                }
            }
        }

        private void BuildOutroRigs()
        {
            // 1. Letterbox & Fade UI (fade in from black at 0:00)
            letterbox = HarborToNeonCutsceneShared.CreateLetterboxAndFadeUI("OtachiOutro_LetterboxUI", 0.115f, 1.0f);
            if (letterbox.Canvas != null) spawnedObjects.Add(letterbox.Canvas.gameObject);

            // 2. Spawn FlyingOtachi matched 1-to-1 with Kaiju_Otachi scale (19.98) & PacificRim_Otachi.mat
            BuildFlyingOtachiActor();

            // 3. Build Storm Cloud Deck (Y = 250..295m), Golden Cloud-Top Sea (Y = 278m), & Upper Stratosphere Curved Earth + Starfield!
            BuildSkyAndStratosphereBackdrops();

            // 4. Build Visor Dendritic Frost Overlay + High-Altitude Ice Motes
            BuildVisorFrostRig();

            // 5. Build Chain Sword Sparks + Glowing Edge Light
            BuildChainSwordFxRig();

            // 6. Build Wind Speed Streaks + Cloud-Punch Mist Ring
            BuildFlightWindAndCloudPunchVfx();

            // 7. Build Shot 7 Chest Reactor Pseudo-Rocket Huge Downward Spotlight + Volumetric Flame Trail!
            BuildChestRetroThrusterRig();
        }

        private void BuildFlyingOtachiActor()
        {
            if (flyingOtachiPrefab == null) return;

            flyingOtachiGo = Instantiate(flyingOtachiPrefab);
            flyingOtachiGo.name = "OtachiOutro_FlyingOtachi";
            spawnedObjects.Add(flyingOtachiGo);

            // Match scale 1-to-1 with scene Kaiju_Otachi (verified exact 19.980x bone ratio!)
            float matchScale = otachiTransform != null ? otachiTransform.localScale.x : 19.98f;
            flyingOtachiGo.transform.localScale = Vector3.one * matchScale;

            flyingOtachiSmr = flyingOtachiGo.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (flyingOtachiSmr != null)
            {
                RepairFlyingOtachiHeadWeights(flyingOtachiSmr);
                if (otachiMaterial != null)
                {
                    var mats = new Material[flyingOtachiSmr.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = otachiMaterial;
                    flyingOtachiSmr.sharedMaterials = mats;
                }
                flyingOtachiSmr.updateWhenOffscreen = true;
            }

            flyingOtachiAnim = flyingOtachiGo.GetComponent<Animator>();
            if (flyingOtachiAnim == null) flyingOtachiAnim = flyingOtachiGo.AddComponent<Animator>();
            if (flyingOtachiController != null)
            {
                flyingOtachiAnim.runtimeAnimatorController = flyingOtachiController;
            }
            flyingOtachiAnim.applyRootMotion = false;
            flyingOtachiAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            flyingOtachiAnim.Rebind();

            // Cache Otachi's talon bones so her feet clamp directly around Gipsy's torso with zero head clipping!
            oAnkleL = flyingOtachiGo.transform.Find("Armature/Pelvis.L/UpperLeg.L/LowerLeg.L/Ankle.L");
            oAnkleR = flyingOtachiGo.transform.Find("Armature/Pelvis.R/UpperLeg.R/LowerLeg.R/Ankle.R");
            oToeL = flyingOtachiGo.transform.Find("Armature/Pelvis.L/UpperLeg.L/LowerLeg.L/Ankle.L/Toe.L");
            oToeR = flyingOtachiGo.transform.Find("Armature/Pelvis.R/UpperLeg.R/LowerLeg.R/Ankle.R/Toe.R");

            // Start hidden until Shot 1 Wing Reveal (t = 1.45s)
            flyingOtachiGo.SetActive(false);
        }

        private void BuildSkyAndStratosphereBackdrops()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");

            // A. Billowing Storm Cloud Deck around Y = 250..290m
            cloudDeckGroup = new GameObject("OtachiOutro_StormCloudDeck");
            spawnedObjects.Add(cloudDeckGroup);
            Material stormCloudMat = CreateTransparentTexMaterial(HarborToNeonCutsceneShared.GetSoftCircleTexture(), new Color(0.14f, 0.18f, 0.28f, 0.74f), additive: false);
            for (int i = 0; i < 24; i++)
            {
                GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Quad);
                puff.name = $"StormCloudLayer_{i:00}";
                puff.transform.SetParent(cloudDeckGroup.transform, false);
                var col = puff.GetComponent<Collider>();
                if (col != null) Destroy(col);
                float angle = (i / 24f) * Mathf.PI * 2f + (i % 3) * 0.4f;
                float r = (i < 6) ? (i * 18f) : (75f + (i % 5) * 85f);
                float y = CloudDeckY - 22f + (i % 6) * 9.5f;
                puff.transform.position = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
                puff.transform.rotation = Quaternion.Euler(90f, angle * Mathf.Rad2Deg, 0f);
                float sz = 180f + (i % 4) * 95f;
                puff.transform.localScale = new Vector3(sz, sz, 1f);
                puff.GetComponent<MeshRenderer>().sharedMaterial = stormCloudMat;
            }

            // B. Sunlit Golden Cloud-Top Sea (visible when bursting above storm clouds in Shot 2!)
            goldenCloudSeaGo = new GameObject("OtachiOutro_GoldenCloudSea");
            spawnedObjects.Add(goldenCloudSeaGo);
            Texture2D cloudSeaTex = GenerateCloudSeaTexture();
            Material goldenSeaMat = new Material(unlitShader);
            if (goldenSeaMat.HasProperty("_BaseMap")) goldenSeaMat.SetTexture("_BaseMap", cloudSeaTex);
            if (goldenSeaMat.HasProperty("_MainTex")) goldenSeaMat.SetTexture("_MainTex", cloudSeaTex);
            if (goldenSeaMat.HasProperty(IdBaseColor)) goldenSeaMat.SetColor(IdBaseColor, new Color(1.0f, 0.84f, 0.54f, 0.96f));
            if (goldenSeaMat.HasProperty(IdColor)) goldenSeaMat.SetColor(IdColor, new Color(1.0f, 0.84f, 0.54f, 0.96f));
            ConfigureSurfaceTransparent(goldenSeaMat, additive: false);

            GameObject seaQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            seaQuad.transform.SetParent(goldenCloudSeaGo.transform, false);
            var sqCol = seaQuad.GetComponent<Collider>();
            if (sqCol != null) Destroy(sqCol);
            seaQuad.transform.position = new Vector3(0f, CloudDeckY + 12f, 0f);
            seaQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            seaQuad.transform.localScale = new Vector3(4800f, 4800f, 1f);
            seaQuad.GetComponent<MeshRenderer>().sharedMaterial = goldenSeaMat;

            // Golden sun disc glare high on the horizon
            Material sunGlareMat = CreateTransparentTexMaterial(HarborToNeonCutsceneShared.GetSoftCircleTexture(), new Color(1.0f, 0.88f, 0.52f, 0.95f), additive: true);
            GameObject sunDisc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sunDisc.transform.SetParent(goldenCloudSeaGo.transform, false);
            var sdCol = sunDisc.GetComponent<Collider>();
            if (sdCol != null) Destroy(sdCol);
            sunDisc.transform.position = new Vector3(950f, CloudDeckY + 240f, 1450f);
            sunDisc.transform.rotation = Quaternion.LookRotation(new Vector3(-950f, -120f, -1450f).normalized, Vector3.up);
            sunDisc.transform.localScale = new Vector3(520f, 520f, 1f);
            sunDisc.GetComponent<MeshRenderer>().sharedMaterial = sunGlareMat;
            goldenCloudSeaGo.SetActive(false);

            // C. Upper Stratosphere Backdrop: High-Resolution Smooth Curved Earth Horizon + Glowing Blue Limb Halo + Starfield!
            stratosphereBackdropGroup = new GameObject("OtachiOutro_StratosphereBackdrop");
            spawnedObjects.Add(stratosphereBackdropGroup);

            GameObject earthSphere = new GameObject("CurvedEarthHorizon");
            earthSphere.transform.SetParent(stratosphereBackdropGroup.transform, false);
            earthSphere.transform.position = new Vector3(0f, StratosphereY - 2390f, 340f);
            earthSphere.transform.localScale = Vector3.one * 2220f; // Radius = 2220m -> silky-smooth curved horizon at Y = 780m!
            earthSphere.transform.rotation = Quaternion.Euler(14f, 22f, -6f);
            earthSphere.AddComponent<MeshFilter>().sharedMesh = BuildHighResEarthSphereMesh(96, 48);

            Texture2D earthTex = GenerateEarthCurvatureTexture();
            Material earthMat = new Material(unlitShader);
            if (earthMat.HasProperty("_BaseMap")) earthMat.SetTexture("_BaseMap", earthTex);
            if (earthMat.HasProperty("_MainTex")) earthMat.SetTexture("_MainTex", earthTex);
            if (earthMat.HasProperty(IdBaseColor)) earthMat.SetColor(IdBaseColor, new Color(0.78f, 0.84f, 0.92f, 1f));
            if (earthMat.HasProperty(IdColor)) earthMat.SetColor(IdColor, new Color(0.78f, 0.84f, 0.92f, 1f));
            earthSphere.AddComponent<MeshRenderer>().sharedMaterial = earthMat;

            // Delicate smooth atmospheric blue limb shell slightly larger than Earth (zero jagged edges!)
            GameObject atmosShell = new GameObject("EarthAtmosphereLimbShell");
            atmosShell.transform.SetParent(stratosphereBackdropGroup.transform, false);
            atmosShell.transform.position = earthSphere.transform.position + new Vector3(0f, 26f, 0f);
            atmosShell.transform.localScale = Vector3.one * 2265f;
            atmosShell.AddComponent<MeshFilter>().sharedMesh = BuildHighResEarthSphereMesh(96, 48);
            Material atmosMat = CreateTransparentTexMaterial(GenerateAtmosphereLimbTexture(), new Color(0.22f, 0.62f, 1.0f, 0.42f), additive: true);
            atmosShell.AddComponent<MeshRenderer>().sharedMaterial = atmosMat;

            // Crisp starfield particle cloud in upper hemisphere around stratosphere stage
            GameObject starGo = new GameObject("Stratosphere_Stars");
            starGo.transform.SetParent(stratosphereBackdropGroup.transform, false);
            starGo.transform.position = new Vector3(0f, StratosphereY + 260f, 0f);
            var starPs = starGo.AddComponent<ParticleSystem>();
            var sm = starPs.main;
            sm.playOnAwake = false;
            sm.loop = false;
            sm.startLifetime = 999f;
            sm.startSpeed = 0f;
            sm.startSize = new ParticleSystem.MinMaxCurve(2.2f, 6.2f);
            sm.maxParticles = 450;
            sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.88f, 1f, 0.9f), new Color(1f, 0.96f, 0.88f, 0.75f));
            var sem = starPs.emission;
            sem.enabled = false;
            var sShape = starPs.shape;
            sShape.enabled = true;
            sShape.shapeType = ParticleSystemShapeType.Hemisphere;
            sShape.radius = 1800f;
            sShape.radiusThickness = 0.05f;
            var sRen = starPs.GetComponent<ParticleSystemRenderer>();
            sRen.sharedMaterial = HarborToNeonCutsceneShared.CreateParticleMaterial(HarborToNeonCutsceneShared.GetSoftCircleTexture(), true);
            starPs.Emit(420);

            stratosphereBackdropGroup.SetActive(false);
        }

        private void BuildVisorFrostRig()
        {
            visorFrostTex = GenerateDendriticFrostTexture();
            // Alpha-blended double-sided crisp crystalline frost veins so they stand out sharply over Gipsy's dimmed golden-slate visor!
            visorFrostMat = CreateTransparentTexMaterial(visorFrostTex, new Color(0.68f, 0.90f, 1.0f, 0f), additive: false);
            if (visorFrostMat.HasProperty("_Cull")) visorFrostMat.SetFloat("_Cull", 0f);

            visorFrostQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            visorFrostQuad.name = "OtachiOutro_VisorFrostOverlay";
            var col = visorFrostQuad.GetComponent<Collider>();
            if (col != null) Destroy(col);
            visorFrostQuad.transform.localScale = new Vector3(1.55f, 1.05f, 1f);
            visorFrostQuad.GetComponent<MeshRenderer>().sharedMaterial = visorFrostMat;
            visorFrostQuad.SetActive(false);
            spawnedObjects.Add(visorFrostQuad);

            // Drifting glittering high-altitude ice crystal motes
            GameObject motesGo = new GameObject("OtachiOutro_VisorIceMotes");
            spawnedObjects.Add(motesGo);
            visorIceMotesPS = motesGo.AddComponent<ParticleSystem>();
            var main = visorIceMotesPS.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.82f, 0.96f, 1f, 0.9f), new Color(1f, 1f, 1f, 0.95f));
            var em = visorIceMotesPS.emission;
            em.rateOverTime = 0f;
            var shape = visorIceMotesPS.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 4.2f;
            visorIceMotesPS.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                HarborToNeonCutsceneShared.CreateParticleMaterial(HarborToNeonCutsceneShared.GetSoftCircleTexture(), true);
        }

        private void BuildChainSwordFxRig()
        {
            // Subtle metallic sparks ONLY while interlocking steel blade teeth engage (NO lightsaber glow bar or floodlight!)
            swordSparksPS = HarborToNeonCutsceneShared.CreateOrangeSparkEmitter("OtachiOutro_SwordDeploySparks", null, null, ratePerSec: 0f, speedMin: 4f, speedMax: 12f);
            var sm = swordSparksPS.main;
            sm.gravityModifier = 0.08f;
            sm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            sm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.78f, 0.36f, 0.95f), new Color(1f, 0.92f, 0.65f, 0.9f));
            var col = swordSparksPS.collision;
            col.enabled = false;
            spawnedObjects.Add(swordSparksPS.gameObject);

            // Very tiny restrained metallic spark point light (kept at 0 except brief 0.15s spark lock flash)
            GameObject slGo = new GameObject("OtachiOutro_SwordGlowLight");
            swordGlowLight = slGo.AddComponent<Light>();
            swordGlowLight.type = LightType.Point;
            swordGlowLight.color = new Color(1f, 0.80f, 0.48f);
            swordGlowLight.range = 8f;
            swordGlowLight.intensity = 0f;
            swordGlowLight.shadows = LightShadows.None;
            spawnedObjects.Add(slGo);

            swordEdgeGlowBar = null;
        }

        private void BuildFlightWindAndCloudPunchVfx()
        {
            GameObject windGo = new GameObject("OtachiOutro_HighSpeedWindStreaks");
            spawnedObjects.Add(windGo);
            windStreaksPS = windGo.AddComponent<ParticleSystem>();
            var main = windStreaksPS.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.58f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(65f, 130f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.55f);
            main.maxParticles = 600;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.92f, 1f, 0.55f), new Color(1f, 0.96f, 0.85f, 0.42f));
            var em = windStreaksPS.emission;
            em.rateOverTime = 0f;
            var shape = windStreaksPS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 28f;
            var ren = windStreaksPS.GetComponent<ParticleSystemRenderer>();
            ren.renderMode = ParticleSystemRenderMode.Stretch;
            ren.velocityScale = 0.14f;
            ren.lengthScale = 5.5f;
            ren.sharedMaterial = HarborToNeonCutsceneShared.CreateParticleMaterial(HarborToNeonCutsceneShared.GetSparkStreakTexture(), true);

            // Cloud-punch radial mist burst
            cloudPunchPS = HarborToNeonCutsceneShared.CreateWaterSprayWallEmitter("OtachiOutro_CloudPunchMist", null, 20f, 52f);
            var cm = cloudPunchPS.main;
            cm.startSize = new ParticleSystem.MinMaxCurve(14f, 32f);
            cm.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            cm.gravityModifier = 0f;
            cm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.96f, 1f, 0.62f), new Color(1f, 0.90f, 0.68f, 0.52f));
            var cShape = cloudPunchPS.shape;
            cShape.shapeType = ParticleSystemShapeType.Sphere;
            cShape.radius = 18f;
            spawnedObjects.Add(cloudPunchPS.gameObject);
        }

        private void BuildChestRetroThrusterRig()
        {
            retroThrusterRoot = new GameObject("OtachiOutro_ChestRetroThrusterRig");
            spawnedObjects.Add(retroThrusterRoot);

            // 1. HUGE Downward Spotlight illuminating the ENTIRE Central Transit Plaza & skyline BEFORE landing!
            GameObject spotGo = new GameObject("RetroThruster_HugeArenaSpotlight");
            spotGo.transform.SetParent(retroThrusterRoot.transform, false);
            retroDownSpotLight = spotGo.AddComponent<Light>();
            retroDownSpotLight.type = LightType.Spot;
            retroDownSpotLight.spotAngle = 98f;
            retroDownSpotLight.innerSpotAngle = 48f;
            retroDownSpotLight.range = 560f;
            retroDownSpotLight.color = new Color(1.0f, 0.56f, 0.22f);
            retroDownSpotLight.intensity = 0f;
            retroDownSpotLight.shadows = LightShadows.None;

            // 2. Wide ground-level arena bounce light so wet asphalt & surrounding Hong Kong facades glow blazing orange-blue!
            GameObject bounceGo = new GameObject("RetroThruster_PlazaBounceLight");
            bounceGo.transform.position = new Vector3(0f, 14f, 0f);
            retroArenaBounceLight = bounceGo.AddComponent<Light>();
            retroArenaBounceLight.type = LightType.Point;
            retroArenaBounceLight.range = 195f;
            retroArenaBounceLight.color = new Color(1.0f, 0.52f, 0.16f);
            retroArenaBounceLight.intensity = 0f;
            retroArenaBounceLight.shadows = LightShadows.None;
            spawnedObjects.Add(bounceGo);

            // 3. Multi-layered Volumetric Flame Trail Cones (Inner White-Hot Cyan-Blue Core + Outer Superheated Orange-Amber Plume)
            // Always use our normalized unit-length +Z flame cone mesh (z=0..1, r=0.28..1.0) so meter-scale aiming is 100% exact!
            Mesh coneMesh = BuildProceduralFlameConeMesh();
            retroCoreMat = CreateFlameConeMaterial(new Color(0.52f, 0.88f, 1.0f, 0.92f));
            retroOuterMat = CreateFlameConeMaterial(new Color(1.0f, 0.44f, 0.08f, 0.72f));

            GameObject innerConeGo = new GameObject("VolumetricPlasmaCoreCone");
            innerConeGo.transform.SetParent(retroThrusterRoot.transform, false);
            innerConeGo.AddComponent<MeshFilter>().sharedMesh = coneMesh;
            innerConeGo.AddComponent<MeshRenderer>().sharedMaterial = retroCoreMat;
            retroCoreConeTr = innerConeGo.transform;

            GameObject outerConeGo = new GameObject("VolumetricNuclearFlameCone");
            outerConeGo.transform.SetParent(retroThrusterRoot.transform, false);
            outerConeGo.AddComponent<MeshFilter>().sharedMesh = coneMesh;
            outerConeGo.AddComponent<MeshRenderer>().sharedMaterial = retroOuterMat;
            retroOuterFlameConeTr = outerConeGo.transform;

            // 4. High-velocity downward nuclear plasma & ember jet particle stream
            GameObject jetGo = new GameObject("RetroThruster_PlasmaJetPS");
            jetGo.transform.SetParent(retroThrusterRoot.transform, false);
            retroPlasmaJetPS = jetGo.AddComponent<ParticleSystem>();
            var jm = retroPlasmaJetPS.main;
            jm.playOnAwake = false;
            jm.loop = true;
            jm.simulationSpace = ParticleSystemSimulationSpace.World;
            jm.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.68f);
            jm.startSpeed = new ParticleSystem.MinMaxCurve(110f, 195f);
            jm.startSize = new ParticleSystem.MinMaxCurve(3.6f, 8.8f);
            jm.maxParticles = 900;
            jm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.92f, 1.0f, 0.95f), new Color(1.0f, 0.50f, 0.12f, 0.92f));
            var jem = retroPlasmaJetPS.emission;
            jem.rateOverTime = 0f;
            var jShape = retroPlasmaJetPS.shape;
            jShape.shapeType = ParticleSystemShapeType.Cone;
            jShape.angle = 14f;
            jShape.radius = 2.0f;
            var jRen = retroPlasmaJetPS.GetComponent<ParticleSystemRenderer>();
            jRen.renderMode = ParticleSystemRenderMode.Stretch;
            jRen.velocityScale = 0.08f;
            jRen.lengthScale = 3.2f;
            jRen.sharedMaterial = HarborToNeonCutsceneShared.CreateParticleMaterial(HarborToNeonCutsceneShared.GetSoftCircleTexture(), true);

            // 5. Expanding ground-wash plasma & steam blast across the wet Central Plaza floor BEFORE landing!
            GameObject washGo = new GameObject("RetroThruster_GroundBlastWashPS");
            washGo.transform.position = new Vector3(0f, 0.6f, 0f);
            washGo.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            spawnedObjects.Add(washGo);
            retroGroundWashPS = washGo.AddComponent<ParticleSystem>();
            var wm = retroGroundWashPS.main;
            wm.playOnAwake = false;
            wm.loop = true;
            wm.simulationSpace = ParticleSystemSimulationSpace.World;
            wm.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.65f);
            wm.startSpeed = new ParticleSystem.MinMaxCurve(38f, 82f);
            wm.startSize = new ParticleSystem.MinMaxCurve(5.5f, 14.0f);
            wm.maxParticles = 700;
            wm.startColor = new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.68f, 0.30f, 0.68f), new Color(0.88f, 0.92f, 0.98f, 0.52f));
            var wem = retroGroundWashPS.emission;
            wem.rateOverTime = 0f;
            var wShape = retroGroundWashPS.shape;
            wShape.shapeType = ParticleSystemShapeType.Circle;
            wShape.radius = 5.5f;
            wShape.radiusThickness = 0.1f;
            retroGroundWashPS.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                HarborToNeonCutsceneShared.CreateParticleMaterial(HarborToNeonCutsceneShared.GetSoftCircleTexture(), false);

            retroThrusterRoot.SetActive(false);
        }

        private IEnumerator RunOutroRoutine()
        {
            // Brief fade-to-black from live defeat collapse before Shot 1 fades in
            SaveAndLockSceneSystems();
            BuildOutroRigs();

            elapsed = 0f;
            driveCamera = true;
            hasBisectedOtachi = false;

            // Initial Shot 1 street positions (Central Transit Plaza)
            Vector3 streetJaegerPos = new Vector3(0f, JaegerGroundY, -14f);
            Vector3 streetOtachiPos = new Vector3(0f, OtachiGroundY, 22f);
            Quaternion faceNorth = Quaternion.identity;
            Quaternion faceSouth = Quaternion.Euler(0f, 180f, 0f);

            if (jaegerTransform != null)
            {
                jaegerTransform.position = streetJaegerPos;
                jaegerTransform.rotation = faceNorth;
            }
            if (otachiTransform != null)
            {
                otachiTransform.gameObject.SetActive(true);
                otachiTransform.position = streetOtachiPos;
                otachiTransform.rotation = faceSouth;
            }
            if (jaegerAnim != null)
            {
                jaegerAnim.speed = 0.85f;
                jaegerAnim.SetFloat("Speed", 0f);
                jaegerAnim.SetBool("InCombat", true);
                jaegerAnim.Play("CombatIdle", 0, 0f);
                jaegerAnim.Update(0f);
            }
            if (otachiAnim != null)
            {
                otachiAnim.speed = 1.0f;
                otachiAnim.Play("DeathLoop", 0, 0.25f);
                otachiAnim.Update(0f);
            }

            // One-shot trigger flags
            bool revealedWings = false;
            bool clampedOntoGipsy = false;
            bool enteredGoldenSky = false;
            bool enteredStratosphere = false;
            bool playedFrostCue = false;
            bool playedSwordUnlock = false;
            bool playedSwordLock = false;
            bool playedSliceSound = false;
            bool snappedBackFullSpeed = false;
            bool punchedBackIntoStorm = false;
            bool ignitedRetroThruster = false;
            bool triggeredTouchdown = false;
            bool playedRiseHydraulics = false;
            float nextWingFlapSfxTime = 3.9f;

            talonClampTargetWeight = 0f;
            smoothedTalonClampWeight = 0f;
            touchdownCrouchWeight = 0f;
            smoothedCrouchWeight = 0f;
            smoothedRightIKWeight = 0f;
            smoothedLeftIKWeight = 0f;
            smoothedTorsoYaw = 0f;
            smoothedChestPitch = 0f;
            hasInitializedHandTargets = false;
            smoothedWingSpeed = 1.4f;
            smoothedAnimSpeed = 1.0f;

            while (elapsed < totalDuration && !skipRequested)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                // --- Letterbox Fade-In (0:00–0:00.9) & Final Fade-Out (0:43.8–0:45.0) ---
                if (letterbox != null && letterbox.FadeOverlay != null)
                {
                    float blackA = 0f;
                    if (elapsed < 0.9f)
                        blackA = 1f - Mathf.SmoothStep(0f, 1f, elapsed / 0.9f);
                    else if (elapsed > 43.8f)
                        blackA = Mathf.SmoothStep(0f, 1f, (elapsed - 43.8f) / 1.2f);
                    letterbox.FadeOverlay.color = new Color(0f, 0f, 0f, blackA);
                }
                if (letterbox != null && letterbox.SkipText != null)
                {
                    float skipA = (elapsed > 1.5f && elapsed < 43.0f) ? Mathf.Clamp01((elapsed - 1.5f) / 0.8f) * 0.72f : 0f;
                    letterbox.SkipText.color = new Color(0.75f, 0.88f, 0.98f, skipA);
                }

                // =========================================================================
                // SHOT 1 (0:00 – 0:06): THE WING REVEAL & AMBUSH (HONG KONG STREETS)
                // =========================================================================
                if (elapsed < 6.0f)
                {
                    driveRightSword = false;
                    rightHandIKWeight = 0f;
                    leftHandIKWeight = 0f;
                    torsoYawDeg = 0f;
                    chestPitchDeg = 0f;
                    touchdownCrouchWeight = 0f;
                    SetVisorFrost(0f);
                    SetRetroThruster(active: false, power: 0f, jaegerAltitude: 0f);
                    SetWindStreaks(active: false, pos: Vector3.zero, dir: Vector3.down, rate: 0f);

                    // 0:00–0:01.45: Collapsed Otachi seemingly defeated in the rain-slicked neon street
                    if (elapsed < 1.45f)
                    {
                        talonClampTargetWeight = 0f;
                        float u = elapsed / 1.45f;
                        Vector3 camPos = new Vector3(14.5f, 3.8f, 6.5f - u * 2.5f);
                        Vector3 lookPt = streetOtachiPos + new Vector3(0f, 6.5f, 0f);
                        camTargetPos = camPos;
                        camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                        camTargetFov = Mathf.Lerp(42f, 38f, u);
                    }
                    // 0:01.45–0:02.60: DORSAL PLATES SPLIT OPEN & MASSIVE LEATHERY WINGS BURST OUTWARD!
                    else if (elapsed < 2.60f)
                    {
                        talonClampTargetWeight = 0f;
                        float u = (elapsed - 1.45f) / 1.15f;
                        if (!revealedWings)
                        {
                            revealedWings = true;
                            // Seamless swap matching exact DeathLoop normalizedTime, then silky 0.45s crossfade into EMP!
                            float deathNormT = otachiAnim != null ? otachiAnim.GetCurrentAnimatorStateInfo(0).normalizedTime : 0.25f;
                            SwapToFlyingOtachiSeamless(streetOtachiPos, faceSouth, deathNormT, targetBodyState: "EMP", crossFadeSec: 0.45f);
                            TriggerFlash(new Color(0.25f, 0.95f, 1.0f, 1f), 0.25f);
                            camShake?.Shake(1.45f, 0.65f);
                            PacificRimAudioManager.Instance?.PlayKaijuEmpRoar(streetOtachiPos + Vector3.up * 14f);
                            PacificRimAudioManager.Instance?.PlayAttackWhoosh(streetOtachiPos + Vector3.up * 16f, isHeavy: true);
                        }

                        smoothedWingSpeed = Mathf.Lerp(smoothedWingSpeed, Mathf.Lerp(1.25f, 1.85f, u), 1f - Mathf.Exp(-6f * dt));
                        if (flyingOtachiAnim != null && flyingOtachiAnim.layerCount > 1)
                        {
                            flyingOtachiAnim.SetLayerWeight(1, Mathf.SmoothStep(0f, 1.0f, u));
                            flyingOtachiAnim.SetFloat("WingSpeed", smoothedWingSpeed);
                        }

                        // Camera pulls wider & slightly lower to reveal the colossal wingspan bursting open!
                        Vector3 camPos = Vector3.Lerp(new Vector3(14.5f, 3.8f, 4.0f), new Vector3(24.0f, 6.2f, -6.5f), Mathf.SmoothStep(0f, 1f, u));
                        Vector3 lookPt = streetOtachiPos + new Vector3(0f, Mathf.Lerp(7.5f, 14.5f, u), 0f);
                        camTargetPos = camPos;
                        camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                        camTargetFov = Mathf.Lerp(38f, 54f, u);
                    }
                    // 0:02.60–0:03.75: OTACHI SWOOPS OVER GIPSY, CLAMPS TALONS ONTO HIS TORSO & TIPS HIM BACK HORIZONTALLY!
                    else if (elapsed < 3.75f)
                    {
                        float u = (elapsed - 2.60f) / 1.15f;
                        float ease = Mathf.SmoothStep(0f, 1f, u);

                        if (flyingOtachiGo != null)
                        {
                            // Swoop high overhead facing South (180 deg) so her talons descend directly onto Gipsy's torso!
                            Vector3 swoopEnd = streetJaegerPos + new Vector3(0f, 20.0f, -14.5f);
                            Vector3 lungePos = Vector3.Lerp(streetOtachiPos, swoopEnd, ease);
                            lungePos.y += Mathf.Sin(u * Mathf.PI) * 9.0f;
                            flyingOtachiGo.transform.position = lungePos;
                            flyingOtachiGo.transform.rotation = Quaternion.Slerp(faceSouth, Quaternion.Euler(10f, 180f, 0f), ease);
                        }

                        // Smoothly begin clamping talons & tilting Gipsy backward toward flat horizontal!
                        float preClampU = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.65f) / 0.35f));
                        talonClampTargetWeight = preClampU;
                        if (jaegerTransform != null)
                        {
                            jaegerTransform.position = streetJaegerPos + Vector3.up * (preClampU * 2.5f);
                            jaegerTransform.rotation = Quaternion.Euler(Mathf.Lerp(0f, -38f, preClampU), 0f, 0f);
                        }

                        if (!clampedOntoGipsy && u >= 0.72f)
                        {
                            clampedOntoGipsy = true;
                            TriggerFlash(new Color(1f, 0.85f, 0.65f, 1f), 0.25f);
                            camShake?.Shake(2.4f, 0.55f);
                            PacificRimAudioManager.Instance?.PlayKaijuSlamImpact(streetJaegerPos + Vector3.up * 16f);
                            PacificRimAudioManager.Instance?.PlayArmorCreak(streetJaegerPos + Vector3.up * 18f);
                            if (flyingOtachiAnim != null)
                            {
                                flyingOtachiAnim.CrossFadeInFixedTime("FullFlying", 0.48f, 0);
                            }
                            if (jaegerAnim != null)
                            {
                                // Smoothly relax legs from hunched CombatIdle into straight-bodied Idle while carried horizontally!
                                jaegerAnim.SetBool("InCombat", false);
                                jaegerAnim.CrossFadeInFixedTime("Idle", 0.50f, 0);
                            }
                        }

                        smoothedWingSpeed = Mathf.Lerp(smoothedWingSpeed, 2.15f, 1f - Mathf.Exp(-5f * dt));
                        if (flyingOtachiAnim != null && flyingOtachiAnim.layerCount > 1)
                            flyingOtachiAnim.SetFloat("WingSpeed", smoothedWingSpeed);

                        Vector3 camPos = Vector3.Lerp(new Vector3(24.0f, 6.2f, -6.5f), new Vector3(32.0f, 9.5f, -29.0f), ease);
                        Vector3 lookPt = Vector3.Lerp(streetOtachiPos + Vector3.up * 14f, streetJaegerPos + Vector3.up * 20f, ease);
                        camTargetPos = camPos;
                        camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                        camTargetFov = Mathf.Lerp(54f, 54f, ease);
                    }
                    // 0:03.75–0:06.00: VIOLENT WINGBEATS RIP HORIZONTAL BACK-DOWN GIPSY STRAIGHT INTO THE NIGHT SKY!
                    else
                    {
                        talonClampTargetWeight = 1f;
                        float u = (elapsed - 3.75f) / 2.25f;
                        float liftY = Mathf.Lerp(JaegerGroundY + 2.5f, 72f, u * u * 0.65f + u * 0.35f);
                        // Smoothly finish rotating Gipsy flat horizontally, back facing downwards (-86 deg around +X!)
                        float flatPitch = Mathf.Lerp(-38f, -86f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.55f)));
                        PositionGrabbedPair(new Vector3(0f, liftY, -14f), yawDeg: u * 18f, jaegerCarryPitch: flatPitch, elapsed);

                        smoothedWingSpeed = Mathf.Lerp(smoothedWingSpeed, 2.20f, 1f - Mathf.Exp(-5f * dt));
                        if (flyingOtachiAnim != null && flyingOtachiAnim.layerCount > 1)
                            flyingOtachiAnim.SetFloat("WingSpeed", smoothedWingSpeed);

                        if (elapsed >= nextWingFlapSfxTime)
                        {
                            nextWingFlapSfxTime = elapsed + 0.82f;
                            PacificRimAudioManager.Instance?.PlayAttackWhoosh(new Vector3(0f, liftY + 18f, -14f), isHeavy: true);
                            camShake?.Shake(0.65f, 0.25f);
                        }

                        Vector3 camPos = new Vector3(32.0f, 7.0f + u * 8f, -29.0f);
                        Vector3 lookPt = new Vector3(0f, liftY + 10f, -14f);
                        camTargetPos = camPos;
                        camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                        camTargetFov = Mathf.Lerp(54f, 58f, u);
                    }
                }
                // =========================================================================
                // SHOT 2 (0:06 – 0:13): PUNCHING THROUGH THE STORM CLOUDS (VERTICAL ASCENT)
                // =========================================================================
                else if (elapsed < 13.0f)
                {
                    talonClampTargetWeight = 1f;
                    touchdownCrouchWeight = 0f;
                    float u = (elapsed - 6.0f) / 7.0f;
                    float ascentY = Mathf.Lerp(72f, 415f, u);
                    float pairYaw = 18f + u * 95f;
                    // Carried flat horizontally, back facing downwards (-86 deg around +X)!
                    PositionGrabbedPair(new Vector3(0f, ascentY, 0f), pairYaw, jaegerCarryPitch: -86f, elapsed);

                    smoothedAnimSpeed = Mathf.Lerp(smoothedAnimSpeed, 1.0f, 1f - Mathf.Exp(-6f * dt));
                    smoothedWingSpeed = Mathf.Lerp(smoothedWingSpeed, 2.25f, 1f - Mathf.Exp(-5f * dt));
                    if (flyingOtachiAnim != null)
                    {
                        flyingOtachiAnim.speed = smoothedAnimSpeed;
                        if (flyingOtachiAnim.layerCount > 1)
                        {
                            flyingOtachiAnim.SetLayerWeight(1, 1.0f);
                            flyingOtachiAnim.SetFloat("WingSpeed", smoothedWingSpeed);
                        }
                    }

                    SetWindStreaks(active: true, pos: new Vector3(0f, ascentY + 55f, 0f), dir: Vector3.down, rate: 140f);

                    if (elapsed >= nextWingFlapSfxTime)
                    {
                        nextWingFlapSfxTime = elapsed + 0.72f;
                        PacificRimAudioManager.Instance?.PlayAttackWhoosh(new Vector3(0f, ascentY + 14f, 0f), isHeavy: true);
                        camShake?.Shake(0.55f, 0.22f);
                    }

                    float cloudBreakBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((ascentY - (CloudDeckY - 26f)) / 52f));
                    ApplyGoldenCloudTopAtmosphere(cloudBreakBlend);

                    if (!enteredGoldenSky && ascentY >= CloudDeckY)
                    {
                        enteredGoldenSky = true;
                        TriggerFlash(new Color(1.0f, 0.90f, 0.62f, 1f), 0.58f);
                        camShake?.Shake(1.8f, 0.55f);
                        SpawnCloudPunchBurst(new Vector3(0f, CloudDeckY + 6f, 0f));
                        PacificRimAudioManager.Instance?.PlaySteamVent(new Vector3(0f, CloudDeckY, 0f));
                    }

                    // Dynamic upward-tilted orbiting tracking camera beneath horizontal back-down Gipsy & Otachi!
                    float orbitAng = (35f + u * 215f) * Mathf.Deg2Rad;
                    float orbitRad = Mathf.Lerp(40f, 46f, u);
                    Vector3 camPos = new Vector3(
                        Mathf.Cos(orbitAng) * orbitRad,
                        ascentY - Mathf.Lerp(18f, 12f, u),
                        Mathf.Sin(orbitAng) * orbitRad);
                    Vector3 lookPt = new Vector3(0f, ascentY + 10.0f, 0f);
                    camTargetPos = camPos;
                    camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                    camTargetFov = Mathf.Lerp(56f, 52f, u);
                }
                // =========================================================================
                // SHOT 3 (0:13 – 0:20): THE STRATOSPHERE & CONN-POD VISOR FROST (EDGE OF SPACE)
                // =========================================================================
                else if (elapsed < 20.0f)
                {
                    talonClampTargetWeight = 1f;
                    touchdownCrouchWeight = 0f;
                    if (!enteredStratosphere)
                    {
                        enteredStratosphere = true;
                        ApplyStratosphereAtmosphere();
                    }

                    SetWindStreaks(active: false, pos: Vector3.zero, dir: Vector3.down, rate: 0f);

                    float t3 = elapsed - 13.0f; // 0 .. 7.0s
                    float hoverY = StratosphereY + Mathf.Sin(t3 * 0.9f) * 3.0f;
                    PositionGrabbedPair(new Vector3(0f, hoverY, 0f), yawDeg: 18f, jaegerCarryPitch: -86f, elapsed);

                    smoothedAnimSpeed = Mathf.Lerp(smoothedAnimSpeed, 1.0f, 1f - Mathf.Exp(-6f * dt));
                    smoothedWingSpeed = Mathf.Lerp(smoothedWingSpeed, 1.25f, 1f - Mathf.Exp(-4f * dt));
                    if (flyingOtachiAnim != null)
                    {
                        flyingOtachiAnim.speed = smoothedAnimSpeed;
                        if (flyingOtachiAnim.layerCount > 1)
                        {
                            flyingOtachiAnim.SetLayerWeight(1, 1.0f);
                            flyingOtachiAnim.SetFloat("WingSpeed", smoothedWingSpeed);
                        }
                    }

                    if (camLowPass != null)
                    {
                        float muffleU = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t3 / 3.5f));
                        camLowPass.cutoffFrequency = Mathf.Lerp(6500f, 420f, muffleU);
                    }

                    // Sub-shot 3A (13.0–16.5s): Wide establishing shot high in upper stratosphere over glowing curved Earth!
                    if (t3 < 3.5f)
                    {
                        float u = t3 / 3.5f;
                        SetVisorFrost(Mathf.Lerp(0f, 0.15f, u));
                        Vector3 camPos = new Vector3(
                            Mathf.Lerp(-50f, -42f, u),
                            hoverY + Mathf.Lerp(18f, 24f, u),
                            Mathf.Lerp(-44f, -52f, u));
                        Vector3 lookPt = new Vector3(0f, hoverY + 8f, 4f);
                        camTargetPos = camPos;
                        camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                        camTargetFov = Mathf.Lerp(50f, 46f, u);
                    }
                    // Sub-shot 3B (16.5–20.0s): Tight close-up on Gipsy's upward-facing Conn-Pod visor as frost coats the glass!
                    else
                    {
                        float u = (t3 - 3.5f) / 3.5f;
                        float frostAmount = Mathf.SmoothStep(0.15f, 1.0f, u);
                        SetVisorFrost(frostAmount);

                        if (!playedFrostCue)
                        {
                            playedFrostCue = true;
                            PacificRimAudioManager.Instance?.PlayArmorCreak(jHead != null ? jHead.position : new Vector3(0f, hoverY, 0f), 0.35f);
                        }

                        Vector3 headWorld = jHead != null ? jHead.position : new Vector3(0f, hoverY, -18f);
                        Vector3 jFwd = jaegerTransform != null ? jaegerTransform.forward : Vector3.up;   // Straight UP from Gipsy's visor!
                        Vector3 jUp = jaegerTransform != null ? jaegerTransform.up : Vector3.back;       // Toward top of helmet!
                        Vector3 jRight = jaegerTransform != null ? jaegerTransform.right : Vector3.right;

                        if (visorIceMotesPS != null)
                        {
                            visorIceMotesPS.transform.position = headWorld + jFwd * 2.2f;
                            var em = visorIceMotesPS.emission;
                            em.rateOverTime = 42f;
                            if (!visorIceMotesPS.isPlaying) visorIceMotesPS.Play();
                        }

                        // Camera oriented cleanly in front of Gipsy's upward-facing Conn-Pod visor with camera Up aligned to helmet top!
                        Vector3 camPos = headWorld + jFwd * Mathf.Lerp(4.2f, 3.15f, u) + jRight * Mathf.Lerp(0.85f, -0.35f, u) + jUp * 0.55f;
                        Vector3 lookPt = headWorld + jUp * 0.45f;
                        camTargetPos = camPos;
                        camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, jUp);
                        camTargetFov = Mathf.Lerp(34f, 29f, u);
                    }
                }
                // =========================================================================
                // SHOT 4 (0:20 – 0:25): RIGHT-ARM CHAIN SWORD DEPLOYMENT (DELIBERATE REAL-TIME, RAW STEEL!)
                // =========================================================================
                else if (elapsed < 25.0f)
                {
                    talonClampTargetWeight = 1f;
                    touchdownCrouchWeight = 0f;
                    // Strictly real-time (1.0x) here — slow motion ONLY begins in Shot 5!
                    smoothedAnimSpeed = Mathf.Lerp(smoothedAnimSpeed, 1.0f, 1f - Mathf.Exp(-8f * dt));
                    smoothedWingSpeed = Mathf.Lerp(smoothedWingSpeed, 1.25f, 1f - Mathf.Exp(-5f * dt));
                    if (flyingOtachiAnim != null)
                    {
                        flyingOtachiAnim.speed = smoothedAnimSpeed;
                        if (flyingOtachiAnim.layerCount > 1) flyingOtachiAnim.SetFloat("WingSpeed", smoothedWingSpeed);
                    }
                    if (jaegerAnim != null) jaegerAnim.speed = smoothedAnimSpeed;

                    float t4 = elapsed - 20.0f; // 0 .. 5.0s
                    float u = t4 / 5.0f;
                    SetVisorFrost(0.85f);
                    if (visorIceMotesPS != null) { var em = visorIceMotesPS.emission; em.rateOverTime = 0f; }

                    if (camLowPass != null)
                    {
                        camLowPass.cutoffFrequency = Mathf.Lerp(420f, 14000f, Mathf.Clamp01(t4 / 0.8f));
                    }

                    float hoverY = StratosphereY + Mathf.Sin((elapsed - 13f) * 0.9f) * 2.4f;
                    PositionGrabbedPair(new Vector3(0f, hoverY, 0f), yawDeg: 18f, jaegerCarryPitch: -86f, elapsed);

                    // Gipsy lies flat back-down: jFwd points UP toward Otachi, jUp points toward Gipsy's head, jRight points right!
                    Vector3 chestRef = jChest != null ? jChest.position : new Vector3(0f, hoverY, -14f);
                    Vector3 jFwd = jaegerTransform != null ? jaegerTransform.forward : Vector3.up;
                    Vector3 jUp = jaegerTransform != null ? jaegerTransform.up : Vector3.back;
                    Vector3 jRight = jaegerTransform != null ? jaegerTransform.right : Vector3.right;

                    // Smoothly ramp arm IK in over 0.65s so Gipsy raises his right forearm deliberately without snapping!
                    rightHandIKWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t4 / 0.65f));
                    leftHandIKWeight = Mathf.SmoothStep(0f, 0.30f, Mathf.Clamp01(t4 / 0.65f));
                    rightHandTargetWorld = chestRef + jRight * Mathf.Lerp(5.8f, 6.8f, u) + jFwd * Mathf.Lerp(3.2f, 4.6f, u) + jUp * 0.8f;
                    rightElbowPoleWorld = chestRef + jRight * 9.8f - jFwd * 2.2f - jUp * 3.5f;
                    leftHandTargetWorld = chestRef - jRight * 5.8f + jFwd * 2.0f;
                    leftElbowPoleWorld = chestRef - jRight * 9.2f - jFwd * 2.2f - jUp * 3.0f;

                    if (!playedSwordUnlock && t4 >= 0.35f)
                    {
                        playedSwordUnlock = true;
                        Vector3 armPos = jForearmR != null ? jForearmR.position : rightHandTargetWorld;
                        PacificRimAudioManager.Instance?.PlayDeploymentClampRelease(armPos);
                        PacificRimAudioManager.Instance?.PlayHydraulicServo(armPos, 0.9f);
                        camShake?.Shake(0.30f, 0.25f);
                    }

                    // Deliberate, weighty mechanical Chain Sword extension (3.3s real time, raw dark steel!)
                    driveRightSword = t4 >= 0.65f;
                    if (t4 < 0.65f)
                    {
                        rightSwordDeployT = 0f;
                        rightSwordWhipDeg = 0f;
                        if (swordGlowLight != null) swordGlowLight.intensity = 0f;
                    }
                    else if (t4 < 3.95f)
                    {
                        float deployU = (t4 - 0.65f) / 3.30f;
                        float smooth = Mathf.SmoothStep(0f, 1f, deployU);
                        float stepped = smooth + Mathf.Sin(deployU * Mathf.PI * 10f) * 0.012f * (1f - deployU);
                        rightSwordDeployT = Mathf.Clamp01(stepped * 1.02f);
                        rightSwordWhipDeg = Mathf.Sin(deployU * 18f) * (1f - deployU) * 6.5f;

                        if (swordSparksPS != null)
                        {
                            var em = swordSparksPS.emission;
                            em.rateOverTime = (1f - deployU * 0.4f) * 32f;
                            if (!swordSparksPS.isPlaying) swordSparksPS.Play();
                        }
                        if (swordGlowLight != null) swordGlowLight.intensity = 0f;
                    }
                    else
                    {
                        float settleU = Mathf.Clamp01((t4 - 3.95f) / 0.35f);
                        rightSwordDeployT = Mathf.Lerp(1.02f, 1.0f, settleU);
                        rightSwordWhipDeg = 0f;

                        if (!playedSwordLock)
                        {
                            playedSwordLock = true;
                            Vector3 handPos = jHandR != null ? jHandR.position : rightHandTargetWorld;
                            PacificRimAudioManager.Instance?.PlayParryClang(handPos);
                            camShake?.Shake(0.55f, 0.22f);
                        }
                        if (swordSparksPS != null) { var em = swordSparksPS.emission; em.rateOverTime = 0f; }
                        if (swordGlowLight != null) swordGlowLight.intensity = (1f - settleU) * 140f;
                    }

                    // Tight tracking close-up gliding smoothly alongside Gipsy's right forearm & raw steel Chain Sword!
                    Vector3 forearmRef = jForearmR != null ? jForearmR.position : (chestRef + jRight * 4.5f + jFwd * 2.0f);
                    Vector3 handRef = jHandR != null ? jHandR.position : rightHandTargetWorld;
                    Vector3 camPos = Vector3.Lerp(
                        forearmRef + jRight * 5.2f + jFwd * 2.4f + jUp * 1.6f,
                        handRef + jRight * 6.8f + jFwd * 4.6f + jUp * 3.8f,
                        Mathf.SmoothStep(0f, 1f, u));
                    Vector3 lookPt = Vector3.Lerp(forearmRef, handRef + jFwd * (3.2f * rightSwordDeployT), Mathf.SmoothStep(0f, 1f, u));
                    camTargetPos = camPos;
                    camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                    camTargetFov = Mathf.Lerp(34f, 40f, u);
                }
                // =========================================================================
                // SHOT 5 (0:25 – 0:32): THE SLOW-MOTION STRATOSPHERE SLICE & BISECTION!
                // =========================================================================
                else if (elapsed < 32.0f)
                {
                    touchdownCrouchWeight = 0f;
                    float t5 = elapsed - 25.0f; // 0 .. 7.0s
                    float u = t5 / 7.0f;

                    // ONLY Shot 5 uses dramatic slow-motion — smoothly ramped IN (0.4–1.0s) and OUT (6.2–6.9s) with ZERO snapping!
                    float slowIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t5 - 0.4f) / 0.6f));
                    float slowOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t5 - 6.2f) / 0.7f));
                    float slowMoFactor = Mathf.Lerp(Mathf.Lerp(1.0f, 0.24f, slowIn), 1.0f, slowOut);
                    smoothedAnimSpeed = slowMoFactor;
                    if (flyingOtachiAnim != null) flyingOtachiAnim.speed = smoothedAnimSpeed;
                    if (jaegerAnim != null) jaegerAnim.speed = smoothedAnimSpeed;
                    if (camLowPass != null) camLowPass.cutoffFrequency = Mathf.Lerp(14000f, 2200f, slowIn * (1f - slowOut) * 0.72f);

                    float hoverY = StratosphereY + Mathf.Sin(t5 * 0.4f) * 1.5f;
                    Vector3 jBasePos = new Vector3(0f, hoverY, 0f);
                    // Gipsy lies flat horizontally, back facing downwards (-86 deg around +X) directly beneath Otachi!
                    Quaternion jRot = Quaternion.Euler(-86f, 18f, 0f);
                    if (jaegerTransform != null)
                    {
                        jaegerTransform.position = jBasePos;
                        jaegerTransform.rotation = jRot;
                    }

                    Vector3 chestRef = jChest != null ? jChest.position : (jBasePos + jRot * new Vector3(0f, 14.8f, 0f));
                    Vector3 jFwd = jaegerTransform != null ? jaegerTransform.forward : Vector3.up;   // Straight UP toward Otachi's belly!
                    Vector3 jUp = jaegerTransform != null ? jaegerTransform.up : Vector3.back;       // Toward Gipsy's head!
                    Vector3 jRight = jaegerTransform != null ? jaegerTransform.right : Vector3.right;

                    // Keep Otachi's talons clamped around Gipsy's horizontal torso right until the instant of bisection!
                    talonClampTargetWeight = hasBisectedOtachi ? 0f : 1f;
                    if (!hasBisectedOtachi && flyingOtachiGo != null)
                    {
                        flyingOtachiGo.transform.rotation = Quaternion.Euler(10f, 18f + 180f, 0f);
                    }

                    // Raw metallic steel Chain Sword (NO bright lightsaber glow!)
                    driveRightSword = true;
                    rightSwordDeployT = 1.0f;
                    rightSwordWhipDeg = 0f;
                    if (swordGlowLight != null) swordGlowLight.intensity = 0f;

                    rightHandIKWeight = 1.0f;
                    leftHandIKWeight = 0.45f;
                    leftHandTargetWorld = chestRef - jRight * 6.5f + jFwd * 2.2f;
                    leftElbowPoleWorld = chestRef - jRight * 10.5f - jFwd * 2.5f - jUp * 3.0f;

                    // Sweeping upward slash from horizontal Gipsy's right side straight UP (+jFwd = +Y) through Otachi's hovering belly!
                    if (t5 < 1.35f)
                    {
                        float p = Mathf.SmoothStep(0f, 1f, t5 / 1.35f);
                        torsoYawDeg = Mathf.Lerp(0f, 28f, p);
                        chestPitchDeg = Mathf.Lerp(0f, 8f, p);
                        rightHandTargetWorld = chestRef + jRight * Mathf.Lerp(6.8f, 9.2f, p) + jFwd * Mathf.Lerp(4.2f, -1.2f, p) - jUp * Mathf.Lerp(-0.8f, 2.5f, p);
                        rightElbowPoleWorld = chestRef + jRight * 11.5f - jFwd * 3.5f - jUp * 4.5f;
                    }
                    else
                    {
                        float slashProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t5 - 1.35f) / 3.15f));
                        float arcRad = Mathf.Lerp(-35f, 86f, slashProgress) * Mathf.Deg2Rad;
                        torsoYawDeg = Mathf.Lerp(28f, -34f, slashProgress);
                        chestPitchDeg = Mathf.Lerp(8f, -14f, slashProgress);

                        rightHandTargetWorld = chestRef
                            + jFwd * (4.5f + Mathf.Sin(arcRad) * 9.8f)          // Sweeps straight UP into Otachi's belly!
                            + jRight * Mathf.Lerp(9.2f, -4.5f, slashProgress)
                            + jUp * (-1.0f + Mathf.Cos(arcRad) * 5.2f);
                        rightElbowPoleWorld = chestRef + jRight * 10.0f + jFwd * 2.5f - jUp * 3.0f;
                    }

                    // Trigger clean mid-air bisection + frozen cyan Kaiju blood crystal spray at t5 = 3.45s (elapsed = 28.45s)!
                    if (!playedSliceSound && t5 >= 2.85f)
                    {
                        playedSliceSound = true;
                        PacificRimAudioManager.Instance?.PlayChainswordSlash(chestRef + Vector3.up * 12f, isHeavy: true);
                    }
                    if (!hasBisectedOtachi && t5 >= 3.45f)
                    {
                        hasBisectedOtachi = true;
                        talonClampTargetWeight = 0f;
                        smoothedTalonClampWeight = 0f;
                        Vector3 slicePt = flyingOtachiSmr != null ? flyingOtachiSmr.bounds.center : (chestRef + Vector3.up * 13.5f);
                        BakeAndSplitFlyingOtachi(slicePt, slashNormalWorld: (jUp * 0.72f + Vector3.up * 0.58f + jRight * 0.25f).normalized);
                        SpawnFrozenCyanKaijuBloodSpray(slicePt, jUp, jRight);
                        TriggerFlash(new Color(0.18f, 0.96f, 1.0f, 1f), 0.45f);
                        camShake?.Shake(2.2f, 0.65f);
                        PacificRimAudioManager.Instance?.PlaySwordStrike(slicePt);
                        PacificRimAudioManager.Instance?.PlayKaijuStaggerPain(slicePt);
                    }

                    // Drift the two severed halves cleanly apart in slow-motion!
                    if (hasBisectedOtachi)
                    {
                        float sepDt = dt * slowMoFactor;
                        upperHalfPos += (Vector3.up * 14.5f + jUp * 12.5f + jRight * 4.0f) * sepDt;
                        upperHalfRot = Quaternion.Euler(-20f * sepDt, 10f * sepDt, 14f * sepDt) * upperHalfRot;
                        lowerHalfPos += (Vector3.down * 11.5f - jUp * 11.0f - jRight * 4.5f) * sepDt;
                        lowerHalfRot = Quaternion.Euler(22f * sepDt, -12f * sepDt, -18f * sepDt) * lowerHalfRot;

                        if (otachiUpperHalfGo != null)
                        {
                            otachiUpperHalfGo.transform.position = upperHalfPos;
                            otachiUpperHalfGo.transform.rotation = upperHalfRot;
                        }
                        if (otachiLowerHalfGo != null)
                        {
                            otachiLowerHalfGo.transform.position = lowerHalfPos;
                            otachiLowerHalfGo.transform.rotation = lowerHalfRot;
                        }
                    }

                    // Crisp 3/4 side profile clear of FlyingOtachi's wingspan framing horizontal back-down Gipsy slicing Otachi above him!
                    Vector3 camPos = chestRef
                        + jRight * Mathf.Lerp(50f, 44f, u)
                        + Vector3.up * Mathf.Lerp(10f, 16f, u)
                        - jUp * Mathf.Lerp(16f, 10f, u);
                    Vector3 lookPt = chestRef + Vector3.up * 10.0f;
                    camTargetPos = camPos;
                    camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                    camTargetFov = Mathf.Lerp(48f, 44f, u);
                }
                // =========================================================================
                // SHOT 6 (0:32 – 0:38): TERMINAL VELOCITY FREEFALL (ROLLING BACK-DOWN -> FACE-DOWN!)
                // =========================================================================
                else if (elapsed < 38.0f)
                {
                    talonClampTargetWeight = 0f;
                    touchdownCrouchWeight = 0f;
                    float t6 = elapsed - 32.0f; // 0 .. 6.0s
                    float u = t6 / 6.0f;

                    if (!snappedBackFullSpeed)
                    {
                        snappedBackFullSpeed = true;
                        smoothedAnimSpeed = 1.0f;
                        if (jaegerAnim != null) jaegerAnim.speed = 1.0f;
                        if (camLowPass != null) camLowPass.cutoffFrequency = 22000f;
                        driveRightSword = false;
                        if (swordGlowLight != null) swordGlowLight.intensity = 0f;
                        SetVisorFrost(0.35f);
                        PacificRimAudioManager.Instance?.PlayAttackWhoosh(jaegerTransform.position, isHeavy: true);
                    }

                    // Accelerating terminal velocity plunge from Y = 950m -> Y = 205m!
                    float fallY = Mathf.Lerp(StratosphereY, 205f, u * u * 0.55f + u * 0.45f);
                    if (jaegerTransform != null)
                    {
                        jaegerTransform.position = new Vector3(0f, fallY, 0f);
                        // Smoothly roll/flip Gipsy mid-air from flat back-down (-86 deg) -> flat FACE-DOWN (+76 deg) toward Hong Kong!
                        Quaternion startBackDown = Quaternion.Euler(-86f, 18f, 0f);
                        Quaternion midRoll = Quaternion.Euler(0f, 100f, 78f);
                        Quaternion endFaceDown = Quaternion.Euler(76f, 180f, Mathf.Sin(t6 * 7f) * 2.5f);
                        float flipU = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.72f));
                        Quaternion curRot = flipU < 0.5f
                            ? Quaternion.Slerp(startBackDown, midRoll, flipU * 2f)
                            : Quaternion.Slerp(midRoll, endFaceDown, (flipU - 0.5f) * 2f);
                        jaegerTransform.rotation = curRot;
                    }

                    torsoYawDeg = 0f;
                    chestPitchDeg = 0f;
                    rightHandIKWeight = 0.85f;
                    leftHandIKWeight = 0.85f;
                    Vector3 jPos = new Vector3(0f, fallY, 0f);
                    Vector3 jR = jaegerTransform != null ? jaegerTransform.right : Vector3.right;
                    Vector3 jU = jaegerTransform != null ? jaegerTransform.up : Vector3.up;
                    rightHandTargetWorld = jPos + jU * 15f + jR * 11.5f;
                    rightElbowPoleWorld = jPos + jU * 13f + jR * 7.0f + Vector3.up * 4.5f;
                    leftHandTargetWorld = jPos + jU * 15f - jR * 11.5f;
                    leftElbowPoleWorld = jPos + jU * 13f - jR * 7.0f + Vector3.up * 4.5f;

                    if (hasBisectedOtachi)
                    {
                        upperHalfPos += new Vector3(4f, 6f, 3f) * dt;
                        lowerHalfPos += new Vector3(-4f, -3f, -2f) * dt;
                        if (otachiUpperHalfGo != null) otachiUpperHalfGo.transform.position = upperHalfPos;
                        if (otachiLowerHalfGo != null) otachiLowerHalfGo.transform.position = lowerHalfPos;
                    }

                    SetWindStreaks(active: true, pos: new Vector3(0f, fallY - 45f, 0f), dir: Vector3.up, rate: 220f);
                    SetVisorFrost(Mathf.Lerp(0.35f, 0f, Mathf.Clamp01(u * 1.4f)));

                    if (!punchedBackIntoStorm && fallY <= CloudDeckY + 25f)
                    {
                        punchedBackIntoStorm = true;
                        RestoreStormyHongKongNightAtmosphere();
                        TriggerFlash(new Color(0.62f, 0.78f, 1.0f, 1f), 0.36f);
                        camShake?.Shake(1.6f, 0.55f);
                        SpawnCloudPunchBurst(new Vector3(0f, CloudDeckY, 0f));
                        PacificRimAudioManager.Instance?.PlaySteamVent(new Vector3(0f, fallY, 0f));
                    }

                    Vector3 camPos = new Vector3(
                        Mathf.Lerp(5.8f, 4.4f, u) + Mathf.Sin(t6 * 14f) * 0.25f,
                        fallY + Mathf.Lerp(32f, 29f, u),
                        Mathf.Lerp(-7.5f, -5.5f, u) + Mathf.Cos(t6 * 12f) * 0.25f);
                    Vector3 lookPt = new Vector3(0f, fallY - 42f, 4.5f);
                    camTargetPos = camPos;
                    camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.forward);
                    camTargetFov = Mathf.Lerp(58f, 68f, u);
                }
                // =========================================================================
                // SHOT 7 (0:38 – 0:45): FACE-DOWN CHEST REACTOR RETRO-THRUSTER -> CROUCH -> RISE TO BATTLE POSE!
                // =========================================================================
                else
                {
                    talonClampTargetWeight = 0f;
                    driveRightSword = false;
                    if (swordGlowLight != null) swordGlowLight.intensity = 0f;
                    float t7 = elapsed - 38.0f; // 0 .. 7.0s
                    SetWindStreaks(active: false, pos: Vector3.zero, dir: Vector3.up, rate: 0f);
                    if (!punchedBackIntoStorm)
                    {
                        punchedBackIntoStorm = true;
                        RestoreStormyHongKongNightAtmosphere();
                    }

                    const float TouchdownT = 4.05f;

                    if (t7 < TouchdownT)
                    {
                        touchdownCrouchWeight = 0f;
                        float fallU = t7 / TouchdownT;
                        float decelCurve = 1f - Mathf.Pow(1f - fallU, 2.35f);
                        float jY = Mathf.Lerp(205f, JaegerGroundY, decelCurve);

                        // POSITIVE +X pitch (+80 deg) keeps Gipsy FACE & CHEST DOWNWARD toward Hong Kong
                        // firing his Nuclear Vortex Turbine purge, then swings feet underneath smoothly right before touchdown!
                        float faceDownPitch = fallU < 0.76f
                            ? Mathf.Lerp(74f, 82f, Mathf.SmoothStep(0f, 1f, fallU / 0.35f))
                            : Mathf.Lerp(82f, 8f, Mathf.SmoothStep(0f, 1f, (fallU - 0.76f) / 0.24f));

                        if (jaegerTransform != null)
                        {
                            jaegerTransform.position = new Vector3(0f, jY, 0f);
                            jaegerTransform.rotation = Quaternion.Euler(faceDownPitch, 180f, 0f);
                        }

                        // Pre-blend smoothly into CombatIdle right before landing so there is zero Animator snap at touchdown!
                        if (fallU >= 0.84f && jaegerAnim != null && !jaegerAnim.GetBool("InCombat"))
                        {
                            jaegerAnim.SetBool("InCombat", true);
                            jaegerAnim.CrossFadeInFixedTime("CombatIdle", 0.48f, 0);
                        }

                        float spreadW = fallU < 0.80f ? 1f : Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, (fallU - 0.80f) / 0.20f));
                        rightHandIKWeight = spreadW;
                        leftHandIKWeight = spreadW;
                        Vector3 jPos = new Vector3(0f, jY, 0f);
                        Vector3 jR = jaegerTransform != null ? jaegerTransform.right : Vector3.left;
                        Vector3 jU = jaegerTransform != null ? jaegerTransform.up : Vector3.back;
                        rightHandTargetWorld = jPos + jU * 14.5f + jR * 12.6f;
                        rightElbowPoleWorld = jPos + jU * 12.0f + jR * 6.5f + Vector3.up * 5.0f;
                        leftHandTargetWorld = jPos + jU * 14.5f - jR * 12.6f;
                        leftElbowPoleWorld = jPos + jU * 12.0f - jR * 6.5f + Vector3.up * 5.0f;

                        bool thrusterOn = t7 >= 0.35f;
                        if (thrusterOn && !ignitedRetroThruster)
                        {
                            ignitedRetroThruster = true;
                            PacificRimAudioManager.Instance?.PlayRocketIgnition(new Vector3(0f, jY, 0f));
                            PacificRimAudioManager.Instance?.PlaySteamVent(new Vector3(0f, jY, 0f));
                            TriggerFlash(new Color(1.0f, 0.72f, 0.36f, 1f), 0.32f);
                            camShake?.Shake(1.6f, 3.5f);
                        }

                        float thrusterPower = thrusterOn
                            ? Mathf.SmoothStep(0.35f, 1.0f, Mathf.Clamp01((t7 - 0.35f) / 0.50f)) * (fallU > 0.86f ? Mathf.Lerp(1f, 0.25f, (fallU - 0.86f) / 0.14f) : 1f)
                            : 0f;
                        SetRetroThruster(thrusterOn, thrusterPower, jY);
                        SetChestTurbineGlow(thrusterOn ? (3.2f * thrusterPower) : 0.9f);

                        Vector3 camPos = new Vector3(
                            Mathf.Lerp(-30f, -23f, fallU),
                            Mathf.Lerp(3.2f, 4.0f, fallU),
                            Mathf.Lerp(-48f, -31f, fallU));
                        Vector3 lookPt = new Vector3(0f, Mathf.Lerp(Mathf.Min(78f, jY * 0.72f), 11.5f, fallU * fallU), 0f);
                        camTargetPos = camPos;
                        camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                        camTargetFov = Mathf.Lerp(60f, 50f, fallU);
                    }
                    else
                    {
                        // TOUCHDOWN IMPACT COMPRESSION INTO DEEP SUPERHERO CROUCH -> SLOW HYDRAULIC RISE INTO BATTLE POSE!
                        if (!triggeredTouchdown)
                        {
                            triggeredTouchdown = true;
                            SetRetroThruster(active: false, power: 0f, jaegerAltitude: 0f);
                            SetChestTurbineGlow(1.45f);
                            if (jaegerAnim != null)
                            {
                                jaegerAnim.speed = 1.0f;
                                jaegerAnim.SetFloat("Speed", 0f);
                                jaegerAnim.SetBool("InCombat", true);
                                jaegerAnim.CrossFadeInFixedTime("CombatIdle", 0.42f, 0);
                            }
                            TriggerFlash(new Color(1.0f, 0.80f, 0.52f, 1f), 0.48f);
                            camShake?.Shake(4.2f, 0.90f);
                            SpawnTouchdownShockwave(new Vector3(0f, 0.6f, 0f));
                            PacificRimAudioManager.Instance?.PlayKaijuSlamImpact(Vector3.zero);
                            PacificRimAudioManager.Instance?.PlayDebrisImpact(Vector3.zero, 1.55f);
                            PacificRimAudioManager.Instance?.PlaySteamVent(new Vector3(0f, 14f, 0f));
                        }

                        float postT = t7 - TouchdownT; // 0 .. 2.95s
                        // Smooth 0.22s heavy impact compression INTO crouch -> brief 0.28s hold -> 1.75s hydraulic rise into Battle Pose!
                        float compressIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(postT / 0.22f));
                        float riseOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((postT - 0.50f) / 1.75f));
                        touchdownCrouchWeight = compressIn * (1f - riseOut);

                        if (!playedRiseHydraulics && postT >= 0.52f)
                        {
                            playedRiseHydraulics = true;
                            PacificRimAudioManager.Instance?.PlayHydraulicServo(new Vector3(0f, 12f, 0f), 0.95f);
                            PacificRimAudioManager.Instance?.PlayArmorCreak(new Vector3(0f, 10f, 0f), 0.85f);
                        }

                        if (jaegerTransform != null)
                        {
                            jaegerTransform.position = new Vector3(0f, JaegerGroundY - 4.8f * smoothedCrouchWeight, 0f);
                            jaegerTransform.rotation = Quaternion.Euler(Mathf.Lerp(8f, 0f, compressIn), 180f, 0f);
                        }

                        crouchFootTargetL = new Vector3(3.4f, 1.35f, -3.4f);
                        crouchKneePoleL = new Vector3(4.2f, 6.5f, -12.5f);
                        crouchFootTargetR = new Vector3(-3.6f, 1.35f, 3.2f);
                        crouchKneePoleR = new Vector3(-4.5f, 6.5f, -10.5f);

                        rightHandIKWeight = touchdownCrouchWeight;
                        leftHandIKWeight = touchdownCrouchWeight * 0.75f;
                        rightHandTargetWorld = new Vector3(-2.6f, 1.6f, -6.2f);
                        rightElbowPoleWorld = new Vector3(-9.5f, 8.5f, -2.5f);
                        leftHandTargetWorld = new Vector3(7.4f, 6.2f, -3.5f);
                        leftElbowPoleWorld = new Vector3(11.5f, 11.0f, 2.0f);

                        torsoYawDeg = -14f * touchdownCrouchWeight;
                        chestPitchDeg = 0f;
                        SetChestTurbineGlow(Mathf.Lerp(1.45f, 1.0f, riseOut));

                        float holdU = Mathf.Clamp01(postT / (7.0f - TouchdownT));
                        Vector3 camPos = new Vector3(
                            Mathf.Lerp(-23.0f, -17.5f, holdU),
                            Mathf.Lerp(3.2f, 5.2f, holdU),
                            Mathf.Lerp(-31.0f, -25.5f, holdU));
                        Vector3 lookPt = new Vector3(0f, Mathf.Lerp(9.8f, 15.8f, riseOut), 0f);
                        camTargetPos = camPos;
                        camTargetRot = Quaternion.LookRotation((lookPt - camPos).normalized, Vector3.up);
                        camTargetFov = Mathf.Lerp(50f, 43f, holdU);
                    }
                }

                yield return null;
            }

            yield return StartCoroutine(FinishOutroAndShowVictory());
        }

        private void SwapToFlyingOtachiSeamless(Vector3 pos, Quaternion rot, float deathNormTime, string targetBodyState, float crossFadeSec)
        {
            if (otachiTransform != null) otachiTransform.gameObject.SetActive(false);
            if (flyingOtachiGo == null) return;

            flyingOtachiGo.SetActive(true);
            flyingOtachiGo.transform.position = pos;
            flyingOtachiGo.transform.rotation = rot;

            if (flyingOtachiAnim != null)
            {
                flyingOtachiAnim.speed = 1.0f;
                // Enter at the EXACT DeathLoop normalizedTime of collapsed base Otachi with 0 wing weight,
                // then smoothly crossfade into EMP so there is ZERO abrupt frame pop on model swap!
                flyingOtachiAnim.Play("DeathLoop", 0, deathNormTime);
                if (flyingOtachiAnim.layerCount > 1)
                {
                    flyingOtachiAnim.SetLayerWeight(1, 0f);
                    flyingOtachiAnim.SetFloat("WingSpeed", 1.25f);
                    flyingOtachiAnim.Play("FlyingWings", 1, 0f);
                }
                flyingOtachiAnim.Update(0f);
                flyingOtachiAnim.CrossFadeInFixedTime(targetBodyState, crossFadeSec, 0);
            }
        }

        private void PositionGrabbedPair(Vector3 jaegerPos, float yawDeg, float jaegerCarryPitch, float time)
        {
            // Gipsy lies FLAT HORIZONTALLY, BACK FACING DOWNWARDS (jaegerCarryPitch = -86 deg around +X -> chest/visor faces straight UP!)
            Quaternion jRot = Quaternion.Euler(jaegerCarryPitch, yawDeg, Mathf.Sin(time * 2.8f) * 2.0f);
            if (jaegerTransform != null)
            {
                jaegerTransform.position = jaegerPos;
                jaegerTransform.rotation = jRot;
            }

            if (flyingOtachiGo != null && !hasBisectedOtachi)
            {
                // FlyingOtachi hovers horizontally directly ABOVE Gipsy facing the same heading Gipsy's head points (yawDeg + 180)
                // while LateUpdate smoothly clamps her gripping talons onto Gipsy's upward-facing chest/torso!
                float flapPitch = 10f + Mathf.Sin(time * 7.5f) * 3.0f;
                flyingOtachiGo.transform.rotation = Quaternion.Euler(flapPitch, yawDeg + 180f, 0f);
                flyingOtachiGo.transform.position = jaegerPos + Vector3.up * 16.5f;
            }
        }

        private void ApplyGoldenCloudTopAtmosphere(float blend)
        {
            if (goldenCloudSeaGo != null) goldenCloudSeaGo.SetActive(blend > 0.08f);
            if (stratosphereBackdropGroup != null) stratosphereBackdropGroup.SetActive(false);

            // Disable rain & Chinooks once above storm clouds!
            bool aboveClouds = blend > 0.45f;
            if (stormMgr != null && stormMgr.rainParticles != null)
                stormMgr.rainParticles.gameObject.SetActive(!aboveClouds && savedRainActive);
            if (lightningSys != null)
                lightningSys.enabled = !aboveClouds && savedLightningEnabled;
            if (chinookSquad != null)
                chinookSquad.gameObject.SetActive(!aboveClouds && savedChinookActive);

            if (mainDirLight != null)
            {
                mainDirLight.color = Color.Lerp(savedDirLightColor, new Color(1.0f, 0.80f, 0.46f, 1f), blend);
                mainDirLight.intensity = Mathf.Lerp(savedDirLightIntensity, 3.8f, blend);
                mainDirLight.transform.rotation = Quaternion.Slerp(savedDirLightRot, Quaternion.Euler(14f, 32f, 0f), blend);
            }

            if (mainCam != null)
                mainCam.backgroundColor = Color.Lerp(savedCamBgColor, new Color(0.28f, 0.44f, 0.68f, 1f), blend);

            RenderSettings.fog = true;
            RenderSettings.fogColor = Color.Lerp(savedFogColor, new Color(0.88f, 0.68f, 0.40f, 1f), blend);
            RenderSettings.fogDensity = Mathf.Lerp(savedFogDensity, 0.00055f, blend);
            RenderSettings.ambientSkyColor = Color.Lerp(savedAmbSky, new Color(0.55f, 0.66f, 0.88f, 1f), blend);
            RenderSettings.ambientEquatorColor = Color.Lerp(savedAmbEq, new Color(0.75f, 0.58f, 0.34f, 1f), blend);
            RenderSettings.ambientGroundColor = Color.Lerp(savedAmbGround, new Color(0.42f, 0.32f, 0.18f, 1f), blend);
        }

        private void ApplyStratosphereAtmosphere()
        {
            if (cloudDeckGroup != null) cloudDeckGroup.SetActive(false);
            if (goldenCloudSeaGo != null) goldenCloudSeaGo.SetActive(false);
            if (stratosphereBackdropGroup != null) stratosphereBackdropGroup.SetActive(true);

            if (stormMgr != null && stormMgr.rainParticles != null) stormMgr.rainParticles.gameObject.SetActive(false);
            if (lightningSys != null) lightningSys.enabled = false;
            if (chinookSquad != null) chinookSquad.gameObject.SetActive(false);

            if (mainCam != null)
                mainCam.backgroundColor = new Color(0.006f, 0.012f, 0.036f, 1f);

            RenderSettings.fog = false;
            RenderSettings.ambientSkyColor = new Color(0.06f, 0.10f, 0.22f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.22f, 0.42f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.34f, 0.62f, 1f); // Earthshine blue glow from below!

            if (mainDirLight != null)
            {
                mainDirLight.color = new Color(0.95f, 0.97f, 1.0f, 1f);
                mainDirLight.intensity = 2.65f;
                mainDirLight.transform.rotation = Quaternion.Euler(22f, -55f, 0f);
            }
        }

        private void RestoreStormyHongKongNightAtmosphere()
        {
            if (stratosphereBackdropGroup != null) stratosphereBackdropGroup.SetActive(false);
            if (goldenCloudSeaGo != null) goldenCloudSeaGo.SetActive(false);
            if (cloudDeckGroup != null) cloudDeckGroup.SetActive(true);

            if (stormMgr != null && stormMgr.rainParticles != null) stormMgr.rainParticles.gameObject.SetActive(savedRainActive);
            if (lightningSys != null) lightningSys.enabled = savedLightningEnabled;
            if (chinookSquad != null) chinookSquad.gameObject.SetActive(savedChinookActive);

            if (mainCam != null) mainCam.backgroundColor = savedCamBgColor;
            RenderSettings.fog = savedFog;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogDensity = savedFogDensity;
            RenderSettings.ambientSkyColor = savedAmbSky;
            RenderSettings.ambientEquatorColor = savedAmbEq;
            RenderSettings.ambientGroundColor = savedAmbGround;

            if (mainDirLight != null)
            {
                mainDirLight.color = savedDirLightColor;
                mainDirLight.intensity = savedDirLightIntensity;
                mainDirLight.transform.rotation = savedDirLightRot;
            }
        }

        private void SetVisorFrost(float amount)
        {
            float clamped = Mathf.Clamp01(amount);
            // Keep visorFrostQuad hidden — apply high-contrast dendritic frost texture directly onto Gipsy's 3D curved
            // Conn-Pod Visor mesh (_BaseMap + _EmissionMap) for a 100% seamless glass-conformal freeze with zero quad borders!
            if (visorFrostQuad != null && visorFrostQuad.activeSelf)
            {
                visorFrostQuad.SetActive(false);
            }
            if (visorMatInst != null)
            {
                if (visorFrostTex != null && visorMatInst.HasProperty("_BaseMap"))
                {
                    visorMatInst.SetTexture("_BaseMap", clamped > 0.08f ? (Texture)visorFrostTex : savedVisorBaseMap);
                }
                if (visorFrostTex != null && visorMatInst.HasProperty("_EmissionMap"))
                {
                    visorMatInst.SetTexture("_EmissionMap", clamped > 0.08f ? (Texture)visorFrostTex : savedVisorEmissionMap);
                }
                if (visorMatInst.HasProperty(IdBaseColor))
                {
                    visorMatInst.SetColor(IdBaseColor, Color.Lerp(savedVisorBaseColor, new Color(0.55f, 0.88f, 1.0f, 1f), clamped));
                }
                if (hasSavedVisorEmission && visorMatInst.HasProperty(IdEmissionColor))
                {
                    // Subtle crystalline frost veins glowing crisply across the darkened visor glass (no bloom blowout!)
                    Color frozenGlassVeins = new Color(0.45f, 0.84f, 1.0f, 1f) * 0.92f;
                    visorMatInst.SetColor(IdEmissionColor, Color.Lerp(savedVisorEmission, frozenGlassVeins, clamped));
                }
            }
        }

        private void SetChestTurbineGlow(float intensity)
        {
            if (turbineMatInst != null && turbineMatInst.HasProperty(IdEmissionColor))
            {
                Color hdr = new Color(1.0f, 0.45f, 0.08f, 1f) * Mathf.Max(0.4f, intensity * 2.4f);
                turbineMatInst.SetColor(IdEmissionColor, hdr);
            }
        }

        private void SetWindStreaks(bool active, Vector3 pos, Vector3 dir, float rate)
        {
            if (windStreaksPS == null) return;
            windStreaksPS.transform.position = pos;
            if (dir.sqrMagnitude > 0.001f) windStreaksPS.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.forward);
            var em = windStreaksPS.emission;
            em.rateOverTime = active ? rate : 0f;
            if (active && !windStreaksPS.isPlaying) windStreaksPS.Play();
        }

        private void SetRetroThruster(bool active, float power, float jaegerAltitude)
        {
            if (retroThrusterRoot == null) return;
            retroThrusterRoot.SetActive(active && power > 0.01f);

            float flicker = 0.88f + 0.12f * Mathf.PerlinNoise(Time.time * 34f, 2.7f);
            float eff = active ? (power * flicker) : 0f;

            // Huge downward spotlight illuminating ENTIRE ground arena before landing!
            if (retroDownSpotLight != null)
            {
                retroDownSpotLight.intensity = eff * 72000f;
                retroDownSpotLight.color = Color.Lerp(new Color(1.0f, 0.46f, 0.14f), new Color(0.72f, 0.90f, 1.0f), 0.25f * flicker);
            }
            if (retroArenaBounceLight != null)
            {
                float proxBoost = Mathf.Lerp(1.0f, 2.6f, Mathf.Clamp01(1f - jaegerAltitude / 160f));
                retroArenaBounceLight.intensity = eff * 18000f * proxBoost;
            }

            // Volumetric multi-layer flame cones extending from Chest down toward the wet plaza asphalt!
            float coneLen = Mathf.Clamp(jaegerAltitude + 18f, 22f, 145f);
            if (retroCoreConeTr != null)
            {
                retroCoreConeTr.localScale = new Vector3(4.2f * eff, 4.2f * eff, coneLen * 0.88f);
            }
            if (retroOuterFlameConeTr != null)
            {
                retroOuterFlameConeTr.localScale = new Vector3(9.8f * eff, 9.8f * eff, coneLen);
            }
            if (retroPlasmaJetPS != null)
            {
                var em = retroPlasmaJetPS.emission;
                em.rateOverTime = active ? (260f * power) : 0f;
                if (active && !retroPlasmaJetPS.isPlaying) retroPlasmaJetPS.Play();
            }
            if (retroGroundWashPS != null)
            {
                float groundHit = active ? Mathf.Clamp01(1f - (jaegerAltitude - 12f) / 145f) : 0f;
                var em = retroGroundWashPS.emission;
                em.rateOverTime = groundHit * 180f * power;
                if (groundHit > 0.05f && !retroGroundWashPS.isPlaying) retroGroundWashPS.Play();
            }
        }

        /// <summary>
        /// Bakes FlyingOtachi's exact mid-air posed SkinnedMeshRenderer at the instant of the Chain Sword cut
        /// and partitions its triangles cleanly along the slash plane into two real separating URP mesh halves!
        /// </summary>
        private void BakeAndSplitFlyingOtachi(Vector3 slicePointWorld, Vector3 slashNormalWorld)
        {
            if (flyingOtachiSmr == null || flyingOtachiGo == null) return;

            Mesh baked = new Mesh();
            flyingOtachiSmr.BakeMesh(baked, false);
            baked.RecalculateBounds();
            Vector3[] verts = baked.vertices;
            Vector3[] norms = baked.normals;
            Vector2[] uvs = baked.uv;
            int[] tris = baked.triangles;

            Transform smrTr = flyingOtachiSmr.transform;
            // Slice cleanly right through the true geometric center of Otachi's posed torso so BOTH Upper (wings/head)
            // and Lower (pelvis/legs/tail) halves visibly split apart with a crisp diagonal slash seam!
            Vector3 localSlicePt = baked.bounds.center;
            Vector3 localNormal = smrTr.InverseTransformDirection(slashNormalWorld).normalized;

            List<int> upperTris = new List<int>(tris.Length / 2);
            List<int> lowerTris = new List<int>(tris.Length / 2);

            for (int i = 0; i < tris.Length; i += 3)
            {
                int i0 = tris[i];
                int i1 = tris[i + 1];
                int i2 = tris[i + 2];
                Vector3 centroid = (verts[i0] + verts[i1] + verts[i2]) * (1f / 3f);
                float side = Vector3.Dot(centroid - localSlicePt, localNormal);
                if (side >= 0f)
                {
                    upperTris.Add(i0);
                    upperTris.Add(i1);
                    upperTris.Add(i2);
                }
                else
                {
                    lowerTris.Add(i0);
                    lowerTris.Add(i1);
                    lowerTris.Add(i2);
                }
            }

            otachiUpperHalfGo = CreateBakedHalfObject("OtachiOutro_UpperHalf", verts, norms, uvs, upperTris, smrTr);
            otachiLowerHalfGo = CreateBakedHalfObject("OtachiOutro_LowerHalf", verts, norms, uvs, lowerTris, smrTr);

            upperHalfPos = smrTr.position;
            upperHalfRot = smrTr.rotation;
            lowerHalfPos = smrTr.position;
            lowerHalfRot = smrTr.rotation;

            Destroy(baked);

            // Hide intact FlyingOtachi immediately so the bisected halves take over seamlessly with zero pop!
            flyingOtachiGo.SetActive(false);
        }

        private GameObject CreateBakedHalfObject(string name, Vector3[] verts, Vector3[] norms, Vector2[] uvs, List<int> tris, Transform sourceTr)
        {
            GameObject go = new GameObject(name);
            go.transform.position = sourceTr.position;
            go.transform.rotation = sourceTr.rotation;
            // BakeMesh(false) bakes world-scale dimensions relative to sourceTr without scale double-application!
            go.transform.localScale = Vector3.one;
            spawnedObjects.Add(go);

            Mesh m = new Mesh();
            m.indexFormat = verts.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.vertices = verts;
            if (norms != null && norms.Length == verts.Length) m.normals = norms;
            if (uvs != null && uvs.Length == verts.Length) m.uv = uvs;
            m.triangles = tris.ToArray();
            m.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            if (otachiMaterial != null) mr.sharedMaterial = otachiMaterial;
            return go;
        }

        private void SpawnFrozenCyanKaijuBloodSpray(Vector3 slicePos, Vector3 fwd, Vector3 right)
        {
            // 1. High-velocity cyan bioluminescent Kaiju blood spray rapidly freezing into glittering ice crystals!
            GameObject sprayGo = new GameObject("OtachiOutro_FrozenCyanBloodSpray");
            sprayGo.transform.position = slicePos;
            sprayGo.transform.rotation = Quaternion.LookRotation((right * 0.7f + Vector3.up * 0.55f + fwd * 0.35f).normalized, Vector3.up);
            spawnedObjects.Add(sprayGo);

            var ps = sprayGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 5.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 5.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(14f, 44f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 1.35f);
            main.gravityModifier = 0.03f; // Thin stratosphere micro-drift!
            main.maxParticles = 600;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.05f, 0.94f, 1.0f, 0.96f),
                new Color(0.55f, 1.0f, 1.0f, 0.96f));

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.0f, 220),
                new ParticleSystem.Burst(0.18f, 140)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 2.8f;

            // Rapidly freeze & decelerate into glittering ice crystals suspended in thin stratosphere air!
            var limVel = ps.limitVelocityOverLifetime;
            limVel.enabled = true;
            limVel.limit = 3.2f;
            limVel.dampen = 0.24f;

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.08f, 0.92f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.75f, 0.98f, 1.0f), 0.38f), // Freezes crystalline white-cyan!
                    new GradientColorKey(new Color(0.92f, 1.0f, 1.0f), 0.85f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0.0f),
                    new GradientAlphaKey(0.90f, 0.72f),
                    new GradientAlphaKey(0.0f, 1.0f)
                });
            colOverLife.color = new ParticleSystem.MinMaxGradient(g);

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            ren.sharedMaterial = HarborToNeonCutsceneShared.CreateParticleMaterial(HarborToNeonCutsceneShared.GetSoftCircleTexture(), true);
            ps.Play();
        }

        private void SpawnCloudPunchBurst(Vector3 pos)
        {
            if (cloudPunchPS == null) return;
            cloudPunchPS.transform.position = pos;
            cloudPunchPS.Emit(70);
        }

        private void SpawnTouchdownShockwave(Vector3 groundPos)
        {
            GameObject shockGo = new GameObject("OtachiOutro_TouchdownShockwave");
            shockGo.transform.position = groundPos;
            shockGo.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            spawnedObjects.Add(shockGo);

            var ps = shockGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 2.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(34f, 76f);
            main.startSize = new ParticleSystem.MinMaxCurve(6.5f, 16.5f);
            main.gravityModifier = -0.18f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.86f, 0.90f, 0.96f, 0.76f),
                new Color(0.55f, 0.58f, 0.64f, 0.65f));

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 130) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 4.5f;
            shape.radiusThickness = 0.08f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 2.1f)));

            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                HarborToNeonCutsceneShared.CreateParticleMaterial(HarborToNeonCutsceneShared.GetSoftCircleTexture(), false);
            ps.Play();
        }

        private void TriggerFlash(Color color, float alpha)
        {
            impactFlashColor = color;
            impactFlashAlpha = Mathf.Max(impactFlashAlpha, alpha);
        }

        private IEnumerator FinishOutroAndShowVictory()
        {
            if (skipRequested && letterbox != null && letterbox.FadeOverlay != null)
            {
                float t = 0f;
                float startA = letterbox.FadeOverlay.color.a;
                while (t < 0.25f)
                {
                    t += Time.unscaledDeltaTime;
                    letterbox.FadeOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(startA, 1f, t / 0.25f));
                    yield return null;
                }
                letterbox.FadeOverlay.color = Color.black;
            }

            driveCamera = false;
            driveRightSword = false;
            talonClampTargetWeight = 0f;
            smoothedTalonClampWeight = 0f;
            touchdownCrouchWeight = 0f;
            smoothedCrouchWeight = 0f;
            rightHandIKWeight = 0f;
            leftHandIKWeight = 0f;
            smoothedRightIKWeight = 0f;
            smoothedLeftIKWeight = 0f;
            torsoYawDeg = 0f;
            chestPitchDeg = 0f;
            smoothedTorsoYaw = 0f;
            smoothedChestPitch = 0f;

            RestoreAllSystems();

            // Place Gipsy cleanly standing victorious in Central Plaza
            if (jaegerTransform != null)
            {
                jaegerTransform.position = new Vector3(0f, JaegerGroundY, 0f);
                jaegerTransform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }
            if (jaegerAnim != null)
            {
                jaegerAnim.speed = 1.0f;
            }

            CleanupSpawnedObjects();
            isRunning = false;

            // Present Level 2 Victory screen ("OTACHI NEUTRALIZED · SECTOR SECURED" -> Next Level: Operation Pitfall!)
            if (MainMenuUI.Instance != null)
            {
                MainMenuUI.Instance.SetVictoryState();
            }
        }

        private void RestoreAllSystems()
        {
            RestoreStormyHongKongNightAtmosphere();

            if (mainCam != null)
            {
                mainCam.fieldOfView = savedCamFov;
                mainCam.farClipPlane = savedCamFarClip;
            }
            if (camFollow != null) camFollow.enabled = savedCamFollowEnabled;
            if (camLowPass != null)
            {
                if (addedLowPass) Destroy(camLowPass);
                else camLowPass.cutoffFrequency = savedLowPassCutoff;
                camLowPass = null;
                addedLowPass = false;
            }

            if (turbineMatInst != null && hasSavedTurbineEmission && turbineMatInst.HasProperty(IdEmissionColor))
                turbineMatInst.SetColor(IdEmissionColor, savedTurbineEmission);
            if (visorMatInst != null)
            {
                if (visorMatInst.HasProperty("_BaseMap")) visorMatInst.SetTexture("_BaseMap", savedVisorBaseMap);
                if (visorMatInst.HasProperty("_EmissionMap")) visorMatInst.SetTexture("_EmissionMap", savedVisorEmissionMap);
                if (visorMatInst.HasProperty(IdBaseColor)) visorMatInst.SetColor(IdBaseColor, savedVisorBaseColor);
                if (hasSavedVisorEmission && visorMatInst.HasProperty(IdEmissionColor))
                    visorMatInst.SetColor(IdEmissionColor, savedVisorEmission);
            }

            if (jRightArmSword != null && hasSavedSwordPoseR)
            {
                jRightArmSword.localPosition = savedSwordPosR;
                jRightArmSword.localRotation = savedSwordRotR;
                jRightArmSword.localScale = savedSwordScaleR;
                jRightArmSword.gameObject.SetActive(false);
            }

            if (jaegerRb != null) jaegerRb.isKinematic = false;
            if (jaegerCtrl != null) jaegerCtrl.isCutsceneActive = false;
        }

        private void CleanupSpawnedObjects()
        {
            for (int i = 0; i < spawnedObjects.Count; i++)
            {
                if (spawnedObjects[i] != null) Destroy(spawnedObjects[i]);
            }
            spawnedObjects.Clear();
            letterbox = null;
        }

        // =========================================================================
        // PROCEDURAL TEXTURE & MESH GENERATORS (SELF-CONTAINED, URP-SAFE)
        // =========================================================================

        private static Material CreateTransparentTexMaterial(Texture2D tex, Color tint, bool additive)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material m = new Material(s);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty(IdBaseColor)) m.SetColor(IdBaseColor, tint);
            if (m.HasProperty(IdColor)) m.SetColor(IdColor, tint);
            ConfigureSurfaceTransparent(m, additive);
            return m;
        }

        private static Material CreateFlameConeMaterial(Color tint)
        {
            Texture2D gradTex = GenerateVerticalFlameGradientTexture();
            Material m = CreateTransparentTexMaterial(gradTex, tint, additive: true);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // Double-sided volumetric cone!
            return m;
        }

        private static void ConfigureSurfaceTransparent(Material mat, bool additive)
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", additive ? 2f : 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) mat.EnableKeyword("_BLENDMODE_ADD");
            mat.renderQueue = 3100;
        }

        private static Mesh BuildProceduralFlameConeMesh()
        {
            const int segs = 28;
            Mesh mesh = new Mesh { name = "RetroThrusterFlameCone" };
            Vector3[] verts = new Vector3[(segs + 1) * 2];
            Vector2[] uvs = new Vector2[(segs + 1) * 2];
            int[] tris = new int[segs * 6];

            for (int i = 0; i <= segs; i++)
            {
                float u = i / (float)segs;
                float a = u * Mathf.PI * 2f;
                float ca = Mathf.Cos(a);
                float sa = Mathf.Sin(a);
                // Apex ring near chest turbine (z=0, r=0.28) flaring wide downward along +Z (z=1, r=1.0)
                verts[i * 2] = new Vector3(ca * 0.28f, sa * 0.28f, 0f);
                uvs[i * 2] = new Vector2(u, 0f);
                verts[i * 2 + 1] = new Vector3(ca * 1.0f, sa * 1.0f, 1.0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);

                if (i < segs)
                {
                    int b = i * 2;
                    int t = i * 6;
                    tris[t] = b;
                    tris[t + 1] = b + 2;
                    tris[t + 2] = b + 1;
                    tris[t + 3] = b + 2;
                    tris[t + 4] = b + 3;
                    tris[t + 5] = b + 1;
                }
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D GenerateVerticalFlameGradientTexture()
        {
            const int w = 64, h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1); // 0 = hot apex near chest, 1 = fading base near ground
                float alphaV = Mathf.Pow(1f - v, 1.35f) * Mathf.Clamp01(v * 14f + 0.35f);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    float streak = 0.72f + 0.28f * Mathf.PerlinNoise(u * 10f, v * 5.5f);
                    px[y * w + x] = new Color(1f, Mathf.Lerp(1f, 0.75f, v), Mathf.Lerp(1f, 0.45f, v), Mathf.Clamp01(alphaV * streak));
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateCloudSeaTexture()
        {
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);
                    float edgeFade = Mathf.Clamp01(1f - Mathf.SmoothStep(0.55f, 1.0f, dist));
                    float n = Mathf.PerlinNoise(x * 0.045f, y * 0.045f) * 0.65f +
                              Mathf.PerlinNoise(x * 0.11f + 13f, y * 0.11f + 7f) * 0.35f;
                    Color c = Color.Lerp(new Color(0.68f, 0.56f, 0.48f, 1f), new Color(1.0f, 0.94f, 0.80f, 1f), n);
                    c.a = edgeFade * Mathf.Lerp(0.75f, 1f, n);
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Repairs unweighted mouth/crest vertices on FlyingOtachi.fbx that Unity's FBX importer defaulted to bone #0 (LowerTorso),
        /// rebinding any stray bone0-only vertices located forward in the skull region cleanly to UpperHead / LowerHead!
        /// </summary>
        private static void RepairFlyingOtachiHeadWeights(SkinnedMeshRenderer smr)
        {
            if (smr == null || smr.sharedMesh == null || smr.bones == null || smr.bones.Length < 5) return;

            int upperHeadIdx = -1;
            int lowerHeadIdx = -1;
            for (int b = 0; b < smr.bones.Length; b++)
            {
                if (smr.bones[b] == null) continue;
                if (smr.bones[b].name == "UpperHead") upperHeadIdx = b;
                else if (smr.bones[b].name == "LowerHead") lowerHeadIdx = b;
            }
            if (upperHeadIdx < 0) return;
            if (lowerHeadIdx < 0) lowerHeadIdx = upperHeadIdx;

            Mesh src = smr.sharedMesh;
            if (!src.isReadable) return;
            Mesh cloned = Instantiate(src);
            Vector3[] verts = cloned.vertices;
            BoneWeight[] weights = cloned.boneWeights;
            Matrix4x4[] bindposes = cloned.bindposes;

            Vector3 lowerTorsoMeshPos = (bindposes != null && bindposes.Length > 0)
                ? bindposes[0].inverse.MultiplyPoint3x4(Vector3.zero)
                : new Vector3(0f, 0.01f, 0.37f);

            bool modified = false;
            for (int i = 0; i < weights.Length && i < verts.Length; i++)
            {
                if (weights[i].boneIndex0 == 0 && weights[i].weight1 <= 0.0001f)
                {
                    if (Vector3.Distance(verts[i], lowerTorsoMeshPos) > 0.35f)
                    {
                        BoneWeight bw = new BoneWeight
                        {
                            boneIndex0 = upperHeadIdx,
                            weight0 = 1f
                        };
                        weights[i] = bw;
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                cloned.boneWeights = weights;
                smr.sharedMesh = cloned;
            }
            else
            {
                Destroy(cloned);
            }
        }

        private static Mesh BuildHighResEarthSphereMesh(int lonSegs, int latSegs)
        {
            Mesh mesh = new Mesh { name = "HighResEarthSphere" };
            int vCount = (lonSegs + 1) * (latSegs + 1);
            Vector3[] verts = new Vector3[vCount];
            Vector3[] norms = new Vector3[vCount];
            Vector2[] uvs = new Vector2[vCount];
            int[] tris = new int[lonSegs * latSegs * 6];

            int vi = 0;
            for (int lat = 0; lat <= latSegs; lat++)
            {
                float v = lat / (float)latSegs;
                float phi = v * Mathf.PI;
                float sinPhi = Mathf.Sin(phi);
                float cosPhi = Mathf.Cos(phi);
                for (int lon = 0; lon <= lonSegs; lon++)
                {
                    float u = lon / (float)lonSegs;
                    float theta = u * Mathf.PI * 2f;
                    Vector3 n = new Vector3(sinPhi * Mathf.Cos(theta), cosPhi, sinPhi * Mathf.Sin(theta));
                    verts[vi] = n;
                    norms[vi] = n;
                    uvs[vi] = new Vector2(u, 1f - v);
                    vi++;
                }
            }

            int ti = 0;
            for (int lat = 0; lat < latSegs; lat++)
            {
                for (int lon = 0; lon < lonSegs; lon++)
                {
                    int row0 = lat * (lonSegs + 1) + lon;
                    int row1 = row0 + (lonSegs + 1);
                    tris[ti++] = row0;
                    tris[ti++] = row0 + 1;
                    tris[ti++] = row1;
                    tris[ti++] = row0 + 1;
                    tris[ti++] = row1 + 1;
                    tris[ti++] = row1;
                }
            }

            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D GenerateAtmosphereLimbTexture()
        {
            const int w = 64, h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                // Delicate limb haze band along Earth's curved upper hemisphere
                float band = Mathf.Exp(-Mathf.Pow((v - 0.86f) / 0.14f, 2f));
                for (int x = 0; x < w; x++)
                {
                    px[y * w + x] = new Color(0.28f, 0.72f, 1.0f, band * 0.65f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateEarthCurvatureTexture()
        {
            const int w = 512, h = 256;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    float landN = Mathf.PerlinNoise(u * 6f + 2.1f, v * 4f + 1.3f);
                    float cloudN = Mathf.PerlinNoise(u * 12f + 9.4f, v * 8f + 4.2f) * 0.65f +
                                   Mathf.PerlinNoise(u * 26f + 1.7f, v * 18f + 6.1f) * 0.35f;

                    // Rich deep orbital sapphire oceans & subtle clouds strictly below URP Bloom blowout threshold!
                    Color ocean = new Color(0.02f, 0.11f, 0.30f, 1f);
                    Color land = new Color(0.08f, 0.18f, 0.14f, 1f);
                    Color baseCol = Color.Lerp(ocean, land, Mathf.SmoothStep(0.55f, 0.68f, landN));
                    float cloudMask = Mathf.SmoothStep(0.48f, 0.74f, cloudN);
                    Color finalCol = Color.Lerp(baseCol, new Color(0.58f, 0.68f, 0.78f, 1f), cloudMask * 0.85f);
                    // Crisp atmospheric royal-blue horizon limb scatter near top curvature
                    finalCol = Color.Lerp(finalCol, new Color(0.16f, 0.44f, 0.76f, 1f), Mathf.SmoothStep(0.76f, 1.0f, v) * 0.65f);
                    px[y * w + x] = finalCol;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateDendriticFrostTexture()
        {
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float ang = Mathf.Atan2(ny, nx);
                    // Sharp crystalline spiderweb dendrites + frosty ice needles spiderwebbing inward from perimeter!
                    float spokesA = Mathf.Abs(Mathf.Sin(ang * 11f + Mathf.PerlinNoise(nx * 5f + 4f, ny * 5f + 4f) * 4.2f));
                    float spokesB = Mathf.Abs(Mathf.Sin(ang * 23f - Mathf.PerlinNoise(nx * 9f + 1f, ny * 9f + 7f) * 2.8f));
                    float crystal = Mathf.PerlinNoise(x * 0.14f, y * 0.14f) * 0.55f + Mathf.PerlinNoise(x * 0.34f + 9f, y * 0.34f + 3f) * 0.45f;
                    float webA = Mathf.Pow(1f - spokesA, 4.5f) * (0.45f + 0.55f * crystal);
                    float webB = Mathf.Pow(1f - spokesB, 5.5f) * crystal;
                    float rimFrost = Mathf.SmoothStep(0.32f, 0.92f, r) * (0.45f + 0.55f * crystal);
                    float veins = Mathf.Clamp01((webA * 1.75f + webB * 1.35f) * Mathf.SmoothStep(0.06f, 0.85f, r) + rimFrost * 0.75f);
                    // High-contrast dark icy sapphire glass base + razor-sharp white-cyan crystalline frost dendrites!
                    Color darkGlass = new Color(0.05f, 0.14f, 0.24f, 1f);
                    Color frostCrystal = new Color(0.82f, 0.96f, 1.0f, 1f);
                    px[y * size + x] = Color.Lerp(darkGlass, frostCrystal, veins);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
