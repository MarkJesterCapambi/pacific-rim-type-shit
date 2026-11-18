using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PacificRimAudio;
using PacificRimUI;
using PacificRimWater;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace PacificRimCutscene
{
    /// <summary>
    /// Orchestrates Beats 3–6 (0:11–0:38) of the movie-accurate Operation Pitfall Ending Cutscene
    /// inside the dedicated "The Anteverse" scene:
    /// - 0:11–0:18 | The Wormhole Throat (Bio-Electric Turbulence): Tumbling disorienting camera
    ///               as battle-damaged Gipsy and limp Slattern plummet through swirling purple,
    ///               magenta, and electric-blue plasma tendrils with jagged blue-white electric arcs
    ///               crackling over Gipsy's armor and spatial distortion ripples past the lens.
    /// - 0:18–0:24 | Breach into the Anteverse (Zero-Gravity Reveal): Burst through iridescent inner
    ///               membrane -> AUDIO DROP #1 into hollow zero-G silence -> wide establishing shot of
    ///               the vast alien Anteverse (teal/violet/amber fog, floating jagged monoliths,
    ///               towering biomechanical Kaiju structures).
    /// - 0:24–0:33 | The Cruciform T-Pose & Core Meltdown: Medium-wide front shot as Slattern drifts
    ///               into the background. Gipsy spreads arms into floating cruciform T-pose with head
    ///               tilted upward. Smooth dolly-in toward chest Nuclear Vortex Turbine as it spins
    ///               into critical overdrive, ramping deep amber-orange -> blinding unstable white-blue
    ///               solar glare with heavy bloom, anamorphic lens flare, superheated plasma vents,
    ///               and jagged electric arcs.
    /// - 0:33–0:38 | Critical Mass & Whiteout Detonation: 0.50s AUDIO DROP #2 (absolute silence) at
    ///               peak brightness -> colossal thermonuclear flash & expanding white-gold shockwave
    ///               sphere vaporizing Gipsy, Slattern & Anteverse cavern -> pure white wash held 1.5s
    ///               -> fade to black & Campaign Complete Victory overlay.
    /// </summary>
    [DefaultExecutionOrder(75)]
    public class AnteverseEndingCutscene : MonoBehaviour
    {
        public static AnteverseEndingCutscene Instance { get; private set; }

        // Static handoff flags set by OperationPitfallEndingCutscene at 0:11 (or skip)
        public static bool HandoffFromOperationPitfall = false;
        public static bool SkipStraightToVictory = false;
        public static float RecordedCombatTime = 0f;

        [Header("Scene Zone Roots")]
        public GameObject wormholeThroatZone;
        public GameObject anteverseCavernZone;
        public Transform exitMembraneTransform;
        public Transform shockwaveSphereTransform;
        public Transform shockwaveRingTransform;
        public Light detonationFlashLight;

        [Header("Combatant References")]
        public Transform jaegerTransform;
        public Transform slatternTransform;
        public Volume postProcessVolume;
        public Light anteverseRimLight;

        [Header("Timeline Durations (Seconds)")]
        public float wormholeDuration = 7.0f;       // 0:11 - 0:18
        public float zeroGRevealDuration = 6.0f;    // 0:18 - 0:24
        public float tPoseMeltdownDuration = 9.0f;  // 0:24 - 0:33
        public float silenceBeatDuration = 0.5f;    // 0:33.0 - 0:33.5
        public float detonationExpandDuration = 1.5f; // 0:33.5 - 0:35.0
        public float whiteHoldDuration = 1.5f;      // 0:35.0 - 0:36.5
        public float whiteToBlackDuration = 1.5f;   // 0:36.5 - 0:38.0

        public bool isCutsceneRunning { get; private set; }

        // Runtime component caches
        private Animator jaegerAnimator;
        private JaegerTurbineSpinner turbineSpinner;
        private JaegerVisualSockets visualSockets;
        private Animator slatternAnimator;
        private Camera mainCamera;
        private PacificRimCameraShake cameraShake;

        // Jaeger bones
        private Transform hipsBone, spine1Bone, chestBone, headBone;
        private Transform upperArmL, forearmL, handL, upperArmR, forearmR, handR;
        private Transform thighL, shinL, footL, thighR, shinR, footR;

        // Materials & lights
        private Material turbineMatInstance;
        private Material visorMatInstance;
        private readonly List<Material> armorMatInstances = new List<Material>();
        private Material exitMembraneMat;
        private Material wormholeMat;
        private Material shockwaveSphereMat;
        private Material shockwaveRingMat;
        private Material chestAnamorphicFlareMat;
        private GameObject chestAnamorphicFlareQuad;
        private Light corePointLight;
        private Light visorPointLight;
        private Light wormholeArcFlashLight;

        // Volume overrides
        private VolumeProfile runtimeVolumeProfile;
        private Bloom volBloom;
        private ColorAdjustments volColorAdj;
        private ChromaticAberration volChromatic;
        private Vignette volVignette;
        private SplitToning volSplitToning;

        // VFX
        private ParticleSystem wormholeSparksPs;
        private ParticleSystem wormholeStreaksPs;
        private ParticleSystem corePlasmaVentPs;
        private ParticleSystem zeroGSporesPs;
        private readonly List<LineRenderer> arcRenderers = new List<LineRenderer>();
        private float nextArcRefreshTime = 0f;

        // Floating props for detonation vaporization
        private AnteverseFloatProp[] floatingProps;

        // Audio
        private AudioSource wormholeWindSource;
        private AudioSource wormholeRumbleSource;
        private AudioSource wormholeSparksSource;
        private AudioSource vacuumDroneSource;
        private AudioSource turbineOverloadSource;
        private AudioSource alarmPulseSource;
        private AudioSource sfxOneShotSource;

        // UI
        private Canvas uiCanvas;
        private RectTransform letterboxRoot;
        private CanvasGroup letterboxCanvasGroup;
        private CanvasGroup whiteoutOverlayGroup;
        private Image whiteoutOverlayImage;
        private GameObject victoryPanelRoot;
        private CanvasGroup victoryPanelGroup;

        // LateUpdate pose & camera control
        private bool applyLateUpdatePoses = false;
        private bool applyTPose = false;
        private float tPoseWeight = 0f;
        private float divePoseWeight = 0f;
        private float zeroGFloatWeight = 0f;
        private float headTiltUpDeg = 15f;
        private bool lockCameraInLateUpdate = false;
        private Vector3 camTargetPos;
        private Quaternion camTargetRot = Quaternion.identity;
        private float camTargetFov = 54f;

        private bool skipRequested = false;
        private bool audioMutedForSilence = false;

        private static readonly int IdEmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
        private static readonly int IdOpacity = Shader.PropertyToID("_Opacity");
        private static readonly int IdFlareColor = Shader.PropertyToID("_FlareColor");
        private static readonly int IdHorizontalStretch = Shader.PropertyToID("_HorizontalStretch");
        private static readonly int IdFlashBurst = Shader.PropertyToID("_FlashBurst");
        private static readonly int IdTearAmount = Shader.PropertyToID("_TearAmount");
        private static readonly int IdFlareIntensity = Shader.PropertyToID("_FlareIntensity");
        private static readonly int IdRippleProgress = Shader.PropertyToID("_RippleProgress");
        private static readonly int IdSmoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int IdEdgeWearStrength = Shader.PropertyToID("_EdgeWearStrength");

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            RestoreGlobalAudioSafely();
            if (Instance == this) Instance = null;
        }

        private void OnDisable()
        {
            RestoreGlobalAudioSafely();
        }

        private void RestoreGlobalAudioSafely()
        {
            if (audioMutedForSilence)
            {
                AudioListener.volume = 1f;
                audioMutedForSilence = false;
            }
        }

        private void Start()
        {
            ResolveReferences();
            PacificRimAudioManager.Instance?.SetEndingCutsceneLoopsSuppressed(true);
            PacificRimMusicManager.Instance?.StopMusicImmediate();

            bool skipNow = SkipStraightToVictory;
            SkipStraightToVictory = false;
            HandoffFromOperationPitfall = false;

            if (skipNow)
            {
                StartCoroutine(ShowVictoryImmediatelyRoutine());
                return;
            }

            StartCoroutine(RunAnteverseCutsceneRoutine());
        }

        private void ResolveReferences()
        {
            if (wormholeThroatZone == null) wormholeThroatZone = GameObject.Find("WormholeThroat_Zone");
            if (anteverseCavernZone == null) anteverseCavernZone = GameObject.Find("AnteverseCavern_Zone");

            if (exitMembraneTransform == null && anteverseCavernZone != null)
            {
                var em = anteverseCavernZone.transform.Find("Anteverse_ExitMembrane");
                if (em != null) exitMembraneTransform = em;
            }
            if (shockwaveSphereTransform == null)
            {
                var sw = GameObject.Find("Thermonuclear_ShockwaveSphere");
                if (sw != null) shockwaveSphereTransform = sw.transform;
            }
            if (shockwaveRingTransform == null)
            {
                var sr = GameObject.Find("Thermonuclear_ShockwaveRing");
                if (sr != null) shockwaveRingTransform = sr.transform;
            }
            if (detonationFlashLight == null)
            {
                var dl = GameObject.Find("Thermonuclear_FlashLight");
                if (dl != null) detonationFlashLight = dl.GetComponent<Light>();
            }

            if (jaegerTransform == null)
            {
                var j = GameObject.Find("Jaeger_Mech");
                if (j != null) jaegerTransform = j.transform;
            }
            if (jaegerTransform != null)
            {
                jaegerAnimator = jaegerTransform.GetComponent<Animator>();
                turbineSpinner = jaegerTransform.GetComponent<JaegerTurbineSpinner>();
                visualSockets = jaegerTransform.GetComponent<JaegerVisualSockets>();
                var jCtrl = jaegerTransform.GetComponent<SimpleJaegerController>();
                if (jCtrl != null) { jCtrl.isCutsceneActive = true; jCtrl.enabled = false; }
                var jRb = jaegerTransform.GetComponent<Rigidbody>();
                if (jRb != null) { jRb.isKinematic = true; jRb.useGravity = false; }
                ResolveJaegerBones();
            }

            if (slatternTransform == null)
            {
                var s = GameObject.Find("Kaiju_Slattern");
                if (s != null) slatternTransform = s.transform;
            }
            if (slatternTransform != null)
            {
                slatternAnimator = slatternTransform.GetComponentInChildren<Animator>();
                var kAi = slatternTransform.GetComponent<KaijuAI>();
                if (kAi != null) { kAi.isCutsceneMode = true; kAi.enabled = false; }
                var sRb = slatternTransform.GetComponent<Rigidbody>();
                if (sRb != null) { sRb.isKinematic = true; sRb.useGravity = false; }
                var sBio = slatternTransform.GetComponent<KaijuBioluminescenceController>();
                if (sBio != null)
                {
                    sBio.minIntensity = 0.03f;
                    sBio.maxIntensity = 0.05f;
                    sBio.maxLightIntensity = 0f;
                }
            }

            if (postProcessVolume == null) postProcessVolume = FindAnyObjectByType<Volume>();
            if (postProcessVolume != null)
            {
                var src = postProcessVolume.sharedProfile != null ? postProcessVolume.sharedProfile : postProcessVolume.profile;
                if (src != null)
                {
                    runtimeVolumeProfile = Instantiate(src);
                    postProcessVolume.profile = runtimeVolumeProfile;
                    runtimeVolumeProfile.TryGet(out volBloom);
                    runtimeVolumeProfile.TryGet(out volColorAdj);
                    runtimeVolumeProfile.TryGet(out volChromatic);
                    runtimeVolumeProfile.TryGet(out volVignette);
                    runtimeVolumeProfile.TryGet(out volSplitToning);
                }
            }

            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                var follow = mainCamera.GetComponent<JaegerCameraFollow>();
                if (follow != null) follow.enabled = false;
                cameraShake = mainCamera.GetComponent<PacificRimCameraShake>() ?? mainCamera.gameObject.AddComponent<PacificRimCameraShake>();
            }

            floatingProps = FindObjectsByType<AnteverseFloatProp>(FindObjectsInactive.Include);

            SetupMaterialsAndVfx();
            EnsureUI();
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

        private void SetupMaterialsAndVfx()
        {
            armorMatInstances.Clear();
            if (jaegerTransform != null)
            {
                foreach (var smr in jaegerTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr == null || smr.sharedMaterial == null) continue;
                    if (smr.name == "Visor")
                    {
                        visorMatInstance = new Material(smr.sharedMaterial) { name = "Visor_AnteverseRuntime" };
                        visorMatInstance.EnableKeyword("_EMISSION");
                        smr.sharedMaterial = visorMatInstance;
                    }
                    else if (smr.name.Contains("Spinning"))
                    {
                        turbineMatInstance = new Material(smr.sharedMaterial) { name = "Turbine_AnteverseRuntime" };
                        turbineMatInstance.EnableKeyword("_EMISSION");
                        smr.sharedMaterial = turbineMatInstance;
                    }
                    else
                    {
                        var mats = smr.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] == null) continue;
                            mats[i] = CloneBattleDamagedArmor(mats[i]);
                        }
                        smr.sharedMaterials = mats;
                    }
                }

                // Disable surface floodlight cones inside wormhole/Anteverse; chest core & sparks lead the lighting
                if (visualSockets != null)
                {
                    visualSockets.SetVolumetricConesVisible(false);
                }
                foreach (var l in jaegerTransform.GetComponentsInChildren<Light>(true))
                {
                    if (l != null && l.type == LightType.Spot) l.intensity *= 0.25f;
                }
            }

            // Chest Core Glow & Visor Glow lights
            var coreGo = new GameObject("Anteverse_CorePointLight");
            corePointLight = coreGo.AddComponent<Light>();
            corePointLight.type = LightType.Point;
            corePointLight.color = new Color(1.0f, 0.48f, 0.06f, 1f);
            corePointLight.range = 42f;
            corePointLight.intensity = 45f;
            corePointLight.shadows = LightShadows.None;

            var visorGo = new GameObject("Anteverse_VisorPointLight");
            visorPointLight = visorGo.AddComponent<Light>();
            visorPointLight.type = LightType.Point;
            visorPointLight.color = new Color(1.0f, 0.56f, 0.10f, 1f);
            visorPointLight.range = 16f;
            visorPointLight.intensity = 12f;
            visorPointLight.shadows = LightShadows.None;

            var arcLightGo = new GameObject("Anteverse_ElectricArcFlashLight");
            wormholeArcFlashLight = arcLightGo.AddComponent<Light>();
            wormholeArcFlashLight.type = LightType.Point;
            wormholeArcFlashLight.color = new Color(0.55f, 0.85f, 1.0f, 1f);
            wormholeArcFlashLight.range = 55f;
            wormholeArcFlashLight.intensity = 0f;
            wormholeArcFlashLight.shadows = LightShadows.None;

            // Anamorphic Lens Flare quad anchored at Gipsy's chest turbine
            Shader flareShader = Shader.Find("PacificRim/AnamorphicFlare");
            if (flareShader != null)
            {
                chestAnamorphicFlareMat = new Material(flareShader) { name = "CoreMeltdown_AnamorphicFlare" };
                chestAnamorphicFlareMat.SetColor(IdFlareColor, new Color(1.0f, 0.52f, 0.12f, 1f));
                chestAnamorphicFlareMat.SetFloat(IdIntensity, 0f);
                chestAnamorphicFlareMat.SetFloat(IdHorizontalStretch, 12f);
                chestAnamorphicFlareMat.SetFloat("_CoreSharpness", 5.5f);
            }
            else
            {
                Shader fallbackFlare = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                chestAnamorphicFlareMat = new Material(fallbackFlare) { name = "CoreMeltdown_AnamorphicFlare_Fallback" };
                Texture2D softGlow = CreateSoftGlowParticleTexture();
                if (chestAnamorphicFlareMat.HasProperty("_BaseMap")) chestAnamorphicFlareMat.SetTexture("_BaseMap", softGlow);
                chestAnamorphicFlareMat.mainTexture = softGlow;
                if (chestAnamorphicFlareMat.HasProperty("_Surface")) chestAnamorphicFlareMat.SetFloat("_Surface", 1f);
                if (chestAnamorphicFlareMat.HasProperty("_Blend")) chestAnamorphicFlareMat.SetFloat("_Blend", 2f);
                if (chestAnamorphicFlareMat.HasProperty("_ZWrite")) chestAnamorphicFlareMat.SetFloat("_ZWrite", 0f);
                if (chestAnamorphicFlareMat.HasProperty(IdBaseColor)) chestAnamorphicFlareMat.SetColor(IdBaseColor, Color.clear);
                chestAnamorphicFlareMat.renderQueue = 3500;
            }

            chestAnamorphicFlareQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            chestAnamorphicFlareQuad.name = "Chest_AnamorphicLensFlare";
            var qCol = chestAnamorphicFlareQuad.GetComponent<Collider>();
            if (qCol != null) Destroy(qCol);
            var qMr = chestAnamorphicFlareQuad.GetComponent<MeshRenderer>();
            qMr.sharedMaterial = chestAnamorphicFlareMat;
            qMr.shadowCastingMode = ShadowCastingMode.Off;
            chestAnamorphicFlareQuad.transform.localScale = new Vector3(18f, 3.2f, 1f);
            chestAnamorphicFlareQuad.SetActive(false);

            // Cache Wormhole, Exit Membrane, and Shockwave materials
            if (wormholeThroatZone != null)
            {
                var tunnelMr = wormholeThroatZone.transform.Find("WormholeTunnelMesh")?.GetComponent<MeshRenderer>();
                if (tunnelMr != null && tunnelMr.sharedMaterial != null)
                {
                    wormholeMat = new Material(tunnelMr.sharedMaterial);
                    tunnelMr.sharedMaterial = wormholeMat;
                }
            }

            if (exitMembraneTransform != null)
            {
                var mr = exitMembraneTransform.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    exitMembraneMat = new Material(mr.sharedMaterial);
                    mr.sharedMaterial = exitMembraneMat;
                }
            }

            if (shockwaveSphereTransform != null)
            {
                var mr = shockwaveSphereTransform.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    shockwaveSphereMat = new Material(mr.sharedMaterial);
                    mr.sharedMaterial = shockwaveSphereMat;
                }
                shockwaveSphereTransform.gameObject.SetActive(false);
            }
            if (shockwaveRingTransform != null)
            {
                var mr = shockwaveRingTransform.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    shockwaveRingMat = new Material(mr.sharedMaterial);
                    mr.sharedMaterial = shockwaveRingMat;
                }
                shockwaveRingTransform.gameObject.SetActive(false);
            }
            if (detonationFlashLight != null)
            {
                detonationFlashLight.intensity = 0f;
                detonationFlashLight.enabled = false;
            }

            BuildElectricArcRenderers();
            BuildCutsceneParticles();
        }

        private Material CloneBattleDamagedArmor(Material src)
        {
            string name = src.name + "_AnteverseBattleDamage";
            for (int i = 0; i < armorMatInstances.Count; i++)
            {
                if (armorMatInstances[i].name == name) return armorMatInstances[i];
            }
            var m = new Material(src) { name = name };
            if (m.HasProperty(IdBaseColor)) m.SetColor(IdBaseColor, new Color(0.56f, 0.58f, 0.64f, 1f));
            if (m.HasProperty(IdSmoothness)) m.SetFloat(IdSmoothness, 0.24f);
            if (m.HasProperty(IdEdgeWearStrength)) m.SetFloat(IdEdgeWearStrength, 1.75f);
            armorMatInstances.Add(m);
            return m;
        }

        private void BuildElectricArcRenderers()
        {
            arcRenderers.Clear();
            Shader arcShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var arcMat = new Material(arcShader) { name = "BioElectricArc_Runtime" };
            if (arcMat.HasProperty(IdBaseColor)) arcMat.SetColor(IdBaseColor, new Color(1.6f, 2.4f, 4.0f, 1f));
            if (arcMat.HasProperty("_Surface")) arcMat.SetFloat("_Surface", 1f);
            if (arcMat.HasProperty("_Blend")) arcMat.SetFloat("_Blend", 2f);

            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject($"BioElectricArc_{i}");
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 14;
                lr.startWidth = 0.28f;
                lr.endWidth = 0.14f;
                lr.sharedMaterial = arcMat;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.startColor = new Color(0.75f, 0.94f, 1.0f, 1f);
                lr.endColor = new Color(0.35f, 0.72f, 1.0f, 0.65f);
                lr.enabled = false;
                arcRenderers.Add(lr);
            }
        }

        private void BuildCutsceneParticles()
        {
            Shader unlitParticle = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            // 0. Ensure a soft radial glow texture so stretched sparks/streaks/plasma have feathered glow instead of solid quads
            Texture2D softParticleTex = CreateSoftGlowParticleTexture();

            // 1. Wormhole sparks shedding off Gipsy
            Material addMat = CreateAdditiveParticleMaterial("AnteverseAdditiveSparkMat", unlitParticle, softParticleTex, new Color(2.4f, 2.0f, 1.4f, 1f));
            wormholeSparksPs = CreateEmitterPS("Wormhole_ArmorSparks", addMat, 360, ParticleSystemRenderMode.Stretch, 0.10f, 2.6f, 0f);

            // 2. Wormhole distortion plasma streaks whipping past camera (sleek, translucent, non-occluding)
            Material streakMat = CreateAdditiveParticleMaterial("WormholeStreaksMat", unlitParticle, softParticleTex, new Color(1.1f, 0.38f, 1.85f, 0.36f));
            wormholeStreaksPs = CreateEmitterPS("Wormhole_LensRipplesAndStreaks", streakMat, 180, ParticleSystemRenderMode.Stretch, 0.14f, 4.5f, 0f);

            // 3. Superheated plasma vents erupting from Gipsy's chest turbine during meltdown (feathered tapered jets)
            Material plasmaMat = CreateAdditiveParticleMaterial("CorePlasmaVentMat", unlitParticle, softParticleTex, new Color(2.2f, 1.75f, 1.0f, 0.85f));
            corePlasmaVentPs = CreateEmitterPS("CoreMeltdown_PlasmaVents", plasmaMat, 360, ParticleSystemRenderMode.Stretch, 0.045f, 2.4f, 0f);

            // 4. Weightless zero-G Anteverse bioluminescent spores
            Material sporeMat = CreateAdditiveParticleMaterial("ZeroGSporesMat", unlitParticle, softParticleTex, new Color(0.35f, 1.85f, 1.75f, 0.85f));
            zeroGSporesPs = CreateEmitterPS("Anteverse_ZeroGSpores", sporeMat, 320, ParticleSystemRenderMode.Billboard, 0f, 1f, 0f);
            var zMain = zeroGSporesPs.main;
            zMain.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            zMain.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.55f);
            var zEm = zeroGSporesPs.emission;
            zEm.enabled = true;
            zEm.rateOverTime = 26f;
            var zShape = zeroGSporesPs.shape;
            zShape.enabled = true;
            zShape.shapeType = ParticleSystemShapeType.Box;
            zShape.scale = new Vector3(130f, 90f, 130f);
            var zVel = zeroGSporesPs.velocityOverLifetime;
            zVel.enabled = true;
            zVel.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            zVel.y = new ParticleSystem.MinMaxCurve(-0.25f, 0.45f);
            zVel.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            zeroGSporesPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static ParticleSystem CreateEmitterPS(
            string name, Material mat, int maxParticles, ParticleSystemRenderMode mode,
            float velScale, float lenScale, float gravity)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = maxParticles;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.enabled = false;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = mat;
            rend.renderMode = mode;
            rend.velocityScale = velScale;
            rend.lengthScale = lenScale;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        private static Material CreateAdditiveParticleMaterial(string matName, Shader shader, Texture2D tex, Color baseColor)
        {
            var mat = new Material(shader) { name = matName };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            if (mat.HasProperty(IdBaseColor)) mat.SetColor(IdBaseColor, baseColor);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.One);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ADD");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return mat;
        }

        private static Texture2D cachedSoftGlowTex;

        private static Texture2D CreateSoftGlowParticleTexture()
        {
            if (cachedSoftGlowTex != null) return cachedSoftGlowTex;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "EndingCutscene_SoftGlowTex",
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
                    float a = Mathf.Pow(falloff, 2.2f);
                    float core = Mathf.Exp(-r * r * 5.2f);
                    float rgb = Mathf.Clamp01(0.45f * falloff + 0.75f * core);
                    pixels[y * size + x] = new Color(rgb, rgb, rgb, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            cachedSoftGlowTex = tex;
            return tex;
        }

        private IEnumerator RunAnteverseCutsceneRoutine()
        {
            isCutsceneRunning = true;
            skipRequested = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (letterboxCanvasGroup != null) letterboxCanvasGroup.alpha = 1f;
            if (whiteoutOverlayGroup != null) whiteoutOverlayGroup.alpha = 0f;
            if (victoryPanelRoot != null) victoryPanelRoot.SetActive(false);

            // =========================================================================
            // BEAT 3 (0:11 – 0:18): The Wormhole Throat (Bio-Electric Turbulence)
            // =========================================================================
            if (wormholeThroatZone != null) wormholeThroatZone.SetActive(true);
            if (anteverseCavernZone != null) anteverseCavernZone.SetActive(false);

            ConfigureWormholeAtmosphere();
            StartWormholeAudio();

            if (jaegerAnimator != null)
            {
                jaegerAnimator.speed = 0.45f;
                jaegerAnimator.SetFloat("Speed", 0f);
                jaegerAnimator.Play("Idle", 0, 0f);
            }
            if (slatternAnimator != null)
            {
                slatternAnimator.Play("DeathLoop", 0, 0.35f);
                slatternAnimator.speed = 0f;
            }
            if (turbineSpinner != null) turbineSpinner.SetRpmInstant(260f);

            applyLateUpdatePoses = true;
            lockCameraInLateUpdate = true;
            divePoseWeight = 1f;
            zeroGFloatWeight = 0f;
            applyTPose = false;
            tPoseWeight = 0f;

            float wTimer = 0f;
            float sparkAcc = 0f;

            while (wTimer < wormholeDuration)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(SkipToVictoryRoutine()); yield break; }

                float dt = Time.deltaTime;
                wTimer += dt;
                float u = Mathf.Clamp01(wTimer / wormholeDuration);

                // Tumble Gipsy & Slattern inside the claustrophobic swirling tunnel
                float jPitch = 102f + Mathf.Sin(wTimer * 2.1f) * 16f;
                float jYaw = wTimer * 28f + Mathf.Cos(wTimer * 1.6f) * 20f;
                float jRoll = Mathf.Sin(wTimer * 2.8f) * 26f;
                Vector3 jPos = new Vector3(Mathf.Sin(wTimer * 1.9f) * 2.4f, Mathf.Cos(wTimer * 1.4f) * 1.8f, Mathf.Sin(wTimer * 1.2f) * 2.0f);
                SetJaegerPose(jPos, Quaternion.Euler(jPitch, jYaw, jRoll));

                Vector3 sPos = new Vector3(Mathf.Cos(wTimer * 1.3f) * 8.5f, 16f + Mathf.Sin(wTimer * 1.1f) * 4f, -15f + Mathf.Sin(wTimer * 0.9f) * 3f);
                SetSlatternPose(sPos, Quaternion.Euler(65f + wTimer * 14f, 140f - wTimer * 18f, wTimer * 22f));

                // Dynamic, disorienting tumbling camera inside the wormhole throat
                float orbitAngle = wTimer * 0.85f + 0.55f;
                float camDist = Mathf.Lerp(17.5f, 14.5f, Mathf.Sin(u * Mathf.PI));
                Vector3 camOffset = new Vector3(
                    Mathf.Sin(orbitAngle) * camDist,
                    Mathf.Lerp(-13f, -6f, Mathf.Sin(wTimer * 1.4f) * 0.5f + 0.5f),
                    Mathf.Cos(orbitAngle) * camDist);
                camTargetPos = jPos + new Vector3(0f, 8f, 0f) + camOffset;
                Vector3 lookAtTarget = jPos + (jaegerTransform.up * 6f);
                Quaternion baseLook = Quaternion.LookRotation((lookAtTarget - camTargetPos).normalized, Vector3.up);
                float dutchTilt = Mathf.Sin(wTimer * 1.75f) * 28f + wTimer * 14f;
                camTargetRot = baseLook * Quaternion.Euler(0f, 0f, dutchTilt);
                camTargetFov = Mathf.Lerp(68f, 76f, Mathf.Sin(wTimer * 2.4f) * 0.5f + 0.5f);

                // Crackling blue-white electric arcs across Gipsy's armor & camera turbulence
                UpdateWormholeElectricity(wTimer, jPos);
                if (cameraShake != null && Random.value < dt * 4.5f)
                {
                    cameraShake.TriggerPlayerHitFeedback(isHeavy: Random.value < 0.35f);
                }

                // Shed sparks and warped spatial distortion plasma streaks past camera lens
                sparkAcc += dt;
                if (sparkAcc >= 0.03f)
                {
                    sparkAcc = 0f;
                    EmitWormholeSparksAndLensRipples(jPos, camTargetPos, camTargetRot);
                }

                // Flash iridescent exit membrane during final 0.55s of Wormhole before burst-through
                if (u > 0.90f && whiteoutOverlayImage != null && whiteoutOverlayGroup != null)
                {
                    float flashU = Mathf.Clamp01((u - 0.90f) / 0.10f);
                    whiteoutOverlayImage.color = new Color(0.65f, 0.92f, 1.0f, 1f);
                    whiteoutOverlayGroup.alpha = Mathf.Sin(flashU * Mathf.PI * 0.5f) * 0.65f;
                }

                UpdateCoreAndVisorAppearance(0f, 0f);
                yield return null;
            }

            // =========================================================================
            // BEAT 4 (0:18 – 0:24): Breach into the Anteverse (Zero-Gravity Reveal)
            // =========================================================================
            // AUDIO DROP #1: Instant hard cut of all wormhole turbulence/roar into eerie zero-G vacuum!
            StopWormholeAudio();
            DisableElectricArcs();
            if (wormholeArcFlashLight != null) wormholeArcFlashLight.intensity = 0f;

            if (wormholeThroatZone != null) wormholeThroatZone.SetActive(false);
            if (anteverseCavernZone != null) anteverseCavernZone.SetActive(true);
            ConfigureAnteverseCavernAtmosphere();
            StartZeroGravityVacuumAudio();

            // Clear brief burst flash quickly over 0.35s and ease smoothly from plunge pose into relaxed zero-G float
            divePoseWeight = 1f;
            zeroGFloatWeight = 0f;
            if (zeroGSporesPs != null) zeroGSporesPs.Play();
            if (exitMembraneMat != null)
            {
                exitMembraneMat.SetFloat(IdTearAmount, 0.55f);
                exitMembraneMat.SetFloat(IdFlareIntensity, 6.0f);
                exitMembraneMat.SetFloat(IdRippleProgress, 0.6f);
            }

            // Wide establishing shot as Gipsy & Slattern drift out from the iridescent inner membrane
            Vector3 gipsyZeroGStart = new Vector3(4f, 20f, -12f);
            Vector3 gipsyZeroGEnd = new Vector3(4f, 8f, 4f);
            Quaternion gipsyRotStart = Quaternion.Euler(46f, 165f, -18f);
            Quaternion gipsyRotEnd = Quaternion.Euler(4f, 180f, 0f);

            // Offset Slattern cleanly into upper-left/midground so both silhouettes read separately in Zero-G
            Vector3 slatZeroGStart = new Vector3(-20f, 28f, 4f);
            Vector3 slatZeroGEnd = new Vector3(-34f, 16f, 36f);
            Quaternion slatRotStart = Quaternion.Euler(22f, 130f, -14f);
            Quaternion slatRotEnd = Quaternion.Euler(42f, 95f, -34f);

            Vector3 b4CamStart = new Vector3(42f, 8f, -46f);
            Vector3 b4CamEnd = new Vector3(34f, 12f, -38f);

            float b4Timer = 0f;
            while (b4Timer < zeroGRevealDuration)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(SkipToVictoryRoutine()); yield break; }

                float dt = Time.deltaTime;
                b4Timer += dt;
                float u = Mathf.Clamp01(b4Timer / zeroGRevealDuration);
                float ease = 1f - Mathf.Pow(1f - u, 2.2f); // Rapid deceleration into eerie zero-G drift
                divePoseWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(b4Timer / 2.2f));
                zeroGFloatWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(b4Timer / 1.8f));

                if (whiteoutOverlayGroup != null && b4Timer < 0.45f)
                {
                    whiteoutOverlayGroup.alpha = Mathf.Lerp(0.65f, 0f, Mathf.Clamp01(b4Timer / 0.45f));
                }
                else if (whiteoutOverlayGroup != null)
                {
                    whiteoutOverlayGroup.alpha = 0f;
                }

                if (exitMembraneMat != null)
                {
                    exitMembraneMat.SetFloat(IdTearAmount, Mathf.Lerp(0.55f, 0.95f, Mathf.Clamp01(u * 2f)));
                    exitMembraneMat.SetFloat(IdFlareIntensity, Mathf.Lerp(6.0f, 1.1f, Mathf.Clamp01(u * 1.5f)));
                    exitMembraneMat.SetFloat(IdRippleProgress, 0.6f + u * 1.2f);
                }

                Vector3 gp = Vector3.Lerp(gipsyZeroGStart, gipsyZeroGEnd, ease);
                gp.y += Mathf.Sin(b4Timer * 0.9f) * 0.45f;
                SetJaegerPose(gp, Quaternion.Slerp(gipsyRotStart, gipsyRotEnd, ease));

                Vector3 sp = Vector3.Lerp(slatZeroGStart, slatZeroGEnd, ease);
                SetSlatternPose(sp, Quaternion.Slerp(slatRotStart, slatRotEnd, ease));

                camTargetPos = Vector3.Lerp(b4CamStart, b4CamEnd, u);
                Vector3 wideLook = Vector3.Lerp(gp + Vector3.up * 12f, sp * 0.25f + gp * 0.75f + Vector3.up * 12f, u * 0.4f);
                camTargetRot = Quaternion.LookRotation((wideLook - camTargetPos).normalized, Vector3.up);
                camTargetFov = Mathf.Lerp(58f, 54f, u);

                UpdateCoreAndVisorAppearance(0f, 0f);
                yield return null;
            }

            // =========================================================================
            // BEAT 5 (0:24 – 0:33): The Cruciform T-Pose & Core Meltdown
            // =========================================================================
            if (exitMembraneTransform != null) exitMembraneTransform.gameObject.SetActive(false);

            applyTPose = true;
            tPoseWeight = 0f;
            if (chestAnamorphicFlareQuad != null) chestAnamorphicFlareQuad.SetActive(true);
            StartMeltdownAudio();

            // Medium-wide front angle framing Gipsy floating upright facing camera (-Z) as Slattern drifts into background
            Vector3 gipsyTPoseBase = gipsyZeroGEnd;
            Vector3 b5CamStart = new Vector3(0f, gipsyTPoseBase.y + 13.5f, gipsyTPoseBase.z - 27.5f);
            Vector3 b5CamEnd = new Vector3(0f, gipsyTPoseBase.y + 15.2f, gipsyTPoseBase.z - 11.2f);

            float b5Timer = 0f;
            float plasmaAcc = 0f;
            bool playedWarningAlarm = false;

            while (b5Timer < tPoseMeltdownDuration)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(SkipToVictoryRoutine()); yield break; }

                float dt = Time.deltaTime;
                b5Timer += dt;
                float u = Mathf.Clamp01(b5Timer / tPoseMeltdownDuration);

                // Smoothly spread both arms outward into the iconic cruciform T-pose while easing out zero-G drift
                tPoseWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(b5Timer / 1.85f));
                zeroGFloatWeight = (1f - tPoseWeight) * 0.65f;
                divePoseWeight = 0f;
                headTiltUpDeg = Mathf.Lerp(4f, 18f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(b5Timer / 2.2f)));

                // Gentle weightless zero-G float upright while Slattern drifts slowly further into the background
                Vector3 gp = gipsyTPoseBase + new Vector3(0f, Mathf.Sin(b5Timer * 0.85f) * 0.42f, 0f);
                Quaternion gRot = Quaternion.Euler(Mathf.Sin(b5Timer * 0.65f) * 1.8f, 180f + Mathf.Cos(b5Timer * 0.5f) * 2.2f, Mathf.Sin(b5Timer * 0.7f) * 1.5f);
                SetJaegerPose(gp, gRot);

                Vector3 sp = Vector3.Lerp(slatZeroGEnd, slatZeroGEnd + new Vector3(-26f, 6f, 44f), u);
                SetSlatternPose(sp, slatRotEnd * Quaternion.Euler(u * 12f, u * 16f, -u * 10f));

                // Smooth slow dolly-in toward Gipsy's chest turbine
                float dollyU = Mathf.SmoothStep(0f, 1f, u);
                camTargetPos = Vector3.Lerp(b5CamStart, b5CamEnd, dollyU);
                Vector3 chestCenter = GetTurbineWorldCenter();
                Vector3 camAim = Vector3.Lerp(gp + Vector3.up * 14.5f, chestCenter, dollyU);
                camTargetRot = Quaternion.LookRotation((camAim - camTargetPos).normalized, Vector3.up);
                camTargetFov = Mathf.Lerp(46f, 38f, dollyU);

                // Nuclear Vortex Turbine critical overdrive & color escalation (amber-orange -> blinding white-blue)
                float meltdownT = Mathf.Pow(u, 1.15f);
                if (turbineSpinner != null)
                {
                    turbineSpinner.SetRpmInstant(Mathf.Lerp(320f, 3800f, meltdownT));
                }

                UpdateCoreAndVisorAppearance(meltdownT, b5Timer);
                UpdateMeltdownAudio(meltdownT);

                if (!playedWarningAlarm && u >= 0.45f)
                {
                    playedWarningAlarm = true;
                    PlayOneShot2D("Computer explosion 6 - Alarm", 0.55f, 1.0f);
                }

                // Superheated plasma vents & jagged electric arcs erupting from the turbine housing
                if (u > 0.18f)
                {
                    float ventRate = Mathf.Lerp(0.08f, 0.015f, meltdownT);
                    plasmaAcc += dt;
                    if (plasmaAcc >= ventRate)
                    {
                        plasmaAcc = 0f;
                        EmitCoreMeltdownPlasmaVents(chestCenter, meltdownT);
                    }
                    UpdateMeltdownChestArcs(chestCenter, meltdownT);
                }

                // Ramp micro camera tremble as core reaches critical instability
                if (u > 0.55f && cameraShake != null && Random.value < dt * Mathf.Lerp(3f, 14f, (u - 0.55f) / 0.45f))
                {
                    cameraShake.TriggerTurbineOverchargeFeedback();
                }

                yield return null;
            }

            // =========================================================================
            // BEAT 6a (0:33.0 – 0:33.5): AUDIO DROP #2 (Half-Second Absolute Silence)
            // =========================================================================
            CutAllAudioForAbsoluteSilence();
            UpdateCoreAndVisorAppearance(1.0f, tPoseMeltdownDuration);

            float silenceTimer = 0f;
            while (silenceTimer < silenceBeatDuration)
            {
                if (WasSkipPressed()) { yield return StartCoroutine(SkipToVictoryRoutine()); yield break; }
                silenceTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            // =========================================================================
            // BEAT 6b (0:33.5 – 0:38.0): Thermonuclear Detonation & Whiteout
            // =========================================================================
            RestoreAudioAndTriggerNuclearBlast(GetTurbineWorldCenter());

            Vector3 blastOrigin = GetTurbineWorldCenter();
            if (shockwaveSphereTransform != null)
            {
                shockwaveSphereTransform.position = blastOrigin;
                shockwaveSphereTransform.localScale = Vector3.one * 2f;
                shockwaveSphereTransform.gameObject.SetActive(true);
            }
            if (shockwaveRingTransform != null)
            {
                shockwaveRingTransform.position = blastOrigin;
                shockwaveRingTransform.localScale = Vector3.one * 3f;
                shockwaveRingTransform.gameObject.SetActive(true);
            }
            if (detonationFlashLight != null)
            {
                detonationFlashLight.transform.position = blastOrigin;
                detonationFlashLight.enabled = true;
                detonationFlashLight.intensity = 12000f;
            }

            // Place whiteout overlay above letterbox bars for pure borderless white detonation wash
            if (whiteoutOverlayGroup != null)
            {
                whiteoutOverlayGroup.transform.SetAsLastSibling();
            }
            if (victoryPanelRoot != null)
            {
                victoryPanelRoot.transform.SetAsLastSibling();
            }

            if (cameraShake != null) cameraShake.TriggerPlayerHitFeedback(isHeavy: true);

            // Expand shockwave sphere & vaporize Gipsy, Slattern, and surrounding Anteverse cavern until pure whiteout
            float detTimer = 0f;
            while (detTimer < detonationExpandDuration)
            {
                detTimer += Time.deltaTime;
                float u = Mathf.Clamp01(detTimer / detonationExpandDuration);
                float sphereScale = Mathf.Lerp(2.5f, 360f, Mathf.Pow(u, 1.35f));

                if (shockwaveSphereTransform != null)
                {
                    shockwaveSphereTransform.localScale = Vector3.one * sphereScale;
                }
                if (shockwaveRingTransform != null)
                {
                    shockwaveRingTransform.localScale = new Vector3(sphereScale * 1.45f, sphereScale * 0.28f, sphereScale * 1.45f);
                }
                if (shockwaveSphereMat != null)
                {
                    shockwaveSphereMat.SetFloat(IdIntensity, Mathf.Lerp(5.5f, 16f, u));
                }

                // Vaporize Gipsy, Slattern & surrounding monoliths/structures as the shockwave engulfs them
                if (u > 0.14f && jaegerTransform != null && jaegerTransform.gameObject.activeSelf)
                {
                    float jFade = Mathf.Clamp01((u - 0.14f) / 0.24f);
                    jaegerTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.01f, jFade);
                    if (jFade >= 0.98f) jaegerTransform.gameObject.SetActive(false);
                }
                if (u > 0.28f && slatternTransform != null && slatternTransform.gameObject.activeSelf)
                {
                    float sFade = Mathf.Clamp01((u - 0.28f) / 0.34f);
                    if (sFade >= 0.98f) slatternTransform.gameObject.SetActive(false);
                }
                if (u > 0.36f && floatingProps != null)
                {
                    float pFade = Mathf.Clamp01((u - 0.36f) / 0.45f);
                    for (int i = 0; i < floatingProps.Length; i++)
                    {
                        if (floatingProps[i] != null) floatingProps[i].SetVaporizeFade(pFade);
                    }
                }

                // Hold clear view of expanding white-gold shockwave dome briefly, then wash entire screen pure white
                if (whiteoutOverlayImage != null && whiteoutOverlayGroup != null)
                {
                    whiteoutOverlayImage.color = Color.Lerp(new Color(1f, 0.97f, 0.88f, 1f), Color.white, u);
                    whiteoutOverlayGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.40f) / 0.50f));
                }

                if (volBloom != null) volBloom.intensity.Override(Mathf.Lerp(5.5f, 12f, u));
                if (volColorAdj != null) volColorAdj.postExposure.Override(Mathf.Lerp(0.5f, 6.5f, u));

                yield return null;
            }

            if (whiteoutOverlayImage != null) whiteoutOverlayImage.color = Color.white;
            if (whiteoutOverlayGroup != null) whiteoutOverlayGroup.alpha = 1f;

            // Hold on pure white for 1.5 seconds
            yield return new WaitForSecondsRealtime(whiteHoldDuration);

            // Fade smoothly from pure white to pure black
            float fadeBlackTimer = 0f;
            while (fadeBlackTimer < whiteToBlackDuration)
            {
                fadeBlackTimer += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fadeBlackTimer / whiteToBlackDuration));
                if (whiteoutOverlayImage != null)
                {
                    whiteoutOverlayImage.color = Color.Lerp(Color.white, Color.black, u);
                }
                yield return null;
            }
            if (whiteoutOverlayImage != null) whiteoutOverlayImage.color = Color.black;

            // Present Campaign Victory / Breach Sealed screen
            ShowCampaignVictoryScreen();
        }

        private IEnumerator ShowVictoryImmediatelyRoutine()
        {
            isCutsceneRunning = false;
            EnsureUI();
            if (letterboxCanvasGroup != null) letterboxCanvasGroup.alpha = 0f;
            if (whiteoutOverlayImage != null) whiteoutOverlayImage.color = Color.black;
            if (whiteoutOverlayGroup != null) whiteoutOverlayGroup.alpha = 1f;
            yield return null;
            ShowCampaignVictoryScreen();
        }

        private IEnumerator SkipToVictoryRoutine()
        {
            if (skipRequested) yield break;
            skipRequested = true;
            isCutsceneRunning = false;
            applyLateUpdatePoses = false;
            lockCameraInLateUpdate = false;

            StopWormholeAudio();
            StopMeltdownAndVacuumAudio();
            RestoreGlobalAudioSafely();

            if (whiteoutOverlayImage != null) whiteoutOverlayImage.color = Color.black;
            if (whiteoutOverlayGroup != null) whiteoutOverlayGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(0.18f);

            ShowCampaignVictoryScreen();
        }

        private void ShowCampaignVictoryScreen()
        {
            isCutsceneRunning = false;
            applyLateUpdatePoses = false;
            lockCameraInLateUpdate = false;
            if (letterboxCanvasGroup != null) letterboxCanvasGroup.alpha = 0f;

            if (victoryPanelRoot != null)
            {
                victoryPanelRoot.SetActive(true);
                if (victoryPanelGroup != null) victoryPanelGroup.alpha = 1f;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            PacificRimUISound.PlaySuccess();
            PacificRimMusicManager.Instance?.PlayEndingVictoryTheme(2.2f);
        }

        private void ConfigureWormholeAtmosphere()
        {
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.12f, 0.02f, 0.24f, 1f);
            RenderSettings.fogDensity = 0.011f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.08f, 0.42f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.14f, 0.22f, 0.54f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.02f, 0.14f, 1f);

            if (anteverseRimLight != null) anteverseRimLight.enabled = false;
            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = RenderSettings.fogColor;
            }

            if (volBloom != null) { volBloom.intensity.Override(1.65f); volBloom.threshold.Override(0.85f); }
            if (volColorAdj != null) { volColorAdj.postExposure.Override(0.18f); volColorAdj.contrast.Override(16f); }
            if (volChromatic != null) { volChromatic.intensity.Override(0.58f); }
            if (volVignette != null) { volVignette.intensity.Override(0.36f); }
        }

        private void ConfigureAnteverseCavernAtmosphere()
        {
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.032f, 0.048f, 0.088f, 1f);
            RenderSettings.fogDensity = 0.0046f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.14f, 0.08f, 0.24f, 1f);     // Bruised violet
            RenderSettings.ambientEquatorColor = new Color(0.06f, 0.20f, 0.22f, 1f); // Sickly teal
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.09f, 0.03f, 1f);  // Subterranean amber

            if (anteverseRimLight != null)
            {
                anteverseRimLight.enabled = true;
                anteverseRimLight.color = new Color(0.16f, 0.88f, 0.78f, 1f);
                anteverseRimLight.intensity = 1.15f;
            }
            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = RenderSettings.fogColor;
                mainCamera.farClipPlane = 800f;
            }

            if (volBloom != null) { volBloom.intensity.Override(1.35f); volBloom.threshold.Override(0.90f); volBloom.scatter.Override(0.75f); }
            if (volColorAdj != null) { volColorAdj.postExposure.Override(0.22f); volColorAdj.contrast.Override(14f); }
            if (volChromatic != null) { volChromatic.intensity.Override(0.14f); }
            if (volVignette != null) { volVignette.intensity.Override(0.26f); }
            if (volSplitToning != null)
            {
                volSplitToning.shadows.Override(new Color(0.14f, 0.06f, 0.22f, 1f));
                volSplitToning.highlights.Override(new Color(0.18f, 0.90f, 0.76f, 1f));
            }
        }

        private void UpdateWormholeElectricity(float t, Vector3 jaegerPos)
        {
            if (Time.time >= nextArcRefreshTime)
            {
                nextArcRefreshTime = Time.time + Random.Range(0.045f, 0.11f);

                Transform[] bodyPoints = { headBone, chestBone, upperArmL, handL, upperArmR, handR, thighL, thighR };
                for (int i = 0; i < arcRenderers.Count; i++)
                {
                    var lr = arcRenderers[i];
                    if (lr == null) continue;
                    bool show = Random.value < 0.78f;
                    lr.enabled = show;
                    if (!show) continue;

                    Vector3 pA = GetRandomValidPoint(bodyPoints, jaegerPos + Vector3.up * 14f);
                    Vector3 pB = (i < 4)
                        ? GetRandomValidPoint(bodyPoints, jaegerPos + Vector3.up * 8f)
                        : jaegerPos + Vector3.up * 12f + Random.onUnitSphere * 24f;
                    PopulateJaggedArcPoints(lr, pA, pB, jitterAmplitude: 1.45f);
                }

                if (wormholeArcFlashLight != null)
                {
                    wormholeArcFlashLight.transform.position = jaegerPos + Vector3.up * 14f + Random.insideUnitSphere * 6f;
                    wormholeArcFlashLight.intensity = Random.Range(40f, 160f);
                }
                if (wormholeMat != null)
                {
                    wormholeMat.SetFloat(IdFlashBurst, Random.Range(0.2f, 1.4f));
                }
                if (Random.value < 0.28f)
                {
                    PlayOneShot2D($"Lighting ({Random.Range(1, 7)})", 0.42f, Random.Range(0.95f, 1.25f));
                }
            }
        }

        private void UpdateMeltdownChestArcs(Vector3 chestCenter, float meltdownT)
        {
            if (Time.time < nextArcRefreshTime) return;
            nextArcRefreshTime = Time.time + Mathf.Lerp(0.14f, 0.04f, meltdownT);

            Transform[] targets = { headBone, upperArmL, handL, upperArmR, handR, hipsBone };
            int activeCount = Mathf.RoundToInt(Mathf.Lerp(2f, arcRenderers.Count, meltdownT));

            for (int i = 0; i < arcRenderers.Count; i++)
            {
                var lr = arcRenderers[i];
                if (lr == null) continue;
                bool show = i < activeCount && Random.value < (0.45f + 0.50f * meltdownT);
                lr.enabled = show;
                if (!show) continue;

                Vector3 pB = GetRandomValidPoint(targets, chestCenter + Random.onUnitSphere * 8f);
                PopulateJaggedArcPoints(lr, chestCenter, pB, jitterAmplitude: Mathf.Lerp(0.6f, 1.6f, meltdownT));
            }
        }

        private void DisableElectricArcs()
        {
            for (int i = 0; i < arcRenderers.Count; i++)
            {
                if (arcRenderers[i] != null) arcRenderers[i].enabled = false;
            }
        }

        private static Vector3 GetRandomValidPoint(Transform[] candidates, Vector3 fallback)
        {
            for (int tries = 0; tries < 4; tries++)
            {
                var t = candidates[Random.Range(0, candidates.Length)];
                if (t != null) return t.position;
            }
            return fallback;
        }

        private static void PopulateJaggedArcPoints(LineRenderer lr, Vector3 a, Vector3 b, float jitterAmplitude)
        {
            int count = lr.positionCount;
            Vector3 span = b - a;
            Vector3 side1 = Vector3.Cross(span.normalized, Vector3.up);
            if (side1.sqrMagnitude < 0.01f) side1 = Vector3.Cross(span.normalized, Vector3.right);
            side1.Normalize();
            Vector3 side2 = Vector3.Cross(span.normalized, side1).normalized;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                float env = Mathf.Sin(t * Mathf.PI);
                Vector3 p = Vector3.Lerp(a, b, t)
                          + side1 * Random.Range(-jitterAmplitude, jitterAmplitude) * env
                          + side2 * Random.Range(-jitterAmplitude, jitterAmplitude) * env;
                lr.SetPosition(i, p);
            }
        }

        private void EmitWormholeSparksAndLensRipples(Vector3 jaegerPos, Vector3 camPos, Quaternion camRot)
        {
            if (wormholeSparksPs != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector3 p = jaegerPos + Vector3.up * Random.Range(6f, 18f) + Random.insideUnitSphere * 3.5f;
                    wormholeSparksPs.Emit(new ParticleSystem.EmitParams
                    {
                        position = p,
                        velocity = Vector3.up * Random.Range(28f, 52f) + Random.insideUnitSphere * 12f,
                        startSize = Random.Range(0.12f, 0.34f),
                        startLifetime = Random.Range(0.35f, 0.75f),
                        startColor = i % 2 == 0 ? new Color(0.5f, 0.9f, 1f, 1f) : new Color(1f, 0.75f, 0.25f, 1f)
                    }, 1);
                }
            }

            if (wormholeStreaksPs != null)
            {
                Vector3 fwd = camRot * Vector3.forward;
                Vector3 right = camRot * Vector3.right;
                Vector3 up = camRot * Vector3.up;
                for (int i = 0; i < 3; i++)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    float radial = Random.Range(4.5f, 10.5f);
                    Vector3 p = camPos + fwd * Random.Range(4f, 14f) + (right * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * radial;
                    wormholeStreaksPs.Emit(new ParticleSystem.EmitParams
                    {
                        position = p,
                        velocity = Vector3.up * Random.Range(32f, 58f) + fwd * Random.Range(-8f, 8f),
                        startSize = Random.Range(0.08f, 0.22f),
                        startLifetime = Random.Range(0.22f, 0.45f),
                        startColor = i % 2 == 0 ? new Color(0.90f, 0.24f, 1f, 0.42f) : new Color(0.18f, 0.80f, 1f, 0.42f)
                    }, 1);
                }
            }
        }

        private void EmitCoreMeltdownPlasmaVents(Vector3 chestCenter, float meltdownT)
        {
            if (corePlasmaVentPs == null) return;
            int count = Mathf.RoundToInt(Mathf.Lerp(2f, 8f, meltdownT));
            Color hotCol = Color.Lerp(new Color(1.0f, 0.65f, 0.15f, 0.85f), new Color(0.88f, 0.96f, 1.0f, 0.9f), meltdownT);

            Vector3 rgt = jaegerTransform != null ? jaegerTransform.right : Vector3.right;
            Vector3 up = jaegerTransform != null ? jaegerTransform.up : Vector3.up;
            Vector3 fwd = jaegerTransform != null ? jaegerTransform.forward : Vector3.forward;

            for (int i = 0; i < count; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                // Vent mostly sideways & diagonally off the turbine housing rather than dead-center into camera
                Vector3 dir = (rgt * Mathf.Cos(ang) + up * Mathf.Sin(ang) * 0.85f + fwd * Random.Range(0.10f, 0.45f)).normalized;
                corePlasmaVentPs.Emit(new ParticleSystem.EmitParams
                {
                    position = chestCenter + dir * 0.95f,
                    velocity = dir * Random.Range(10f, 28f),
                    startSize = Random.Range(0.06f, 0.16f),
                    startLifetime = Random.Range(0.22f, 0.48f),
                    startColor = hotCol
                }, 1);
            }
        }

        private void UpdateCoreAndVisorAppearance(float meltdownT, float timeInBeat)
        {
            Vector3 chestPos = GetTurbineWorldCenter();
            if (corePointLight != null) corePointLight.transform.position = chestPos + (jaegerTransform != null ? jaegerTransform.forward * 1.4f : Vector3.forward);
            if (visorPointLight != null && headBone != null) visorPointLight.transform.position = headBone.position + (jaegerTransform != null ? jaegerTransform.forward * 1.0f : Vector3.forward);

            // Color progression: deep amber-orange -> hot gold -> unstable blinding white-blue solar glare
            Color deepAmber = new Color(1.0f, 0.40f, 0.04f, 1f);
            Color hotGold = new Color(1.0f, 0.80f, 0.36f, 1f);
            Color solarWhiteBlue = new Color(0.90f, 0.96f, 1.0f, 1f);

            Color curCol = meltdownT < 0.5f
                ? Color.Lerp(deepAmber, hotGold, meltdownT * 2f)
                : Color.Lerp(hotGold, solarWhiteBlue, (meltdownT - 0.5f) * 2f);

            float instability = meltdownT > 0.35f
                ? 1.0f + (Mathf.PerlinNoise(timeInBeat * 24f, 3.1f) - 0.3f) * 0.45f * meltdownT
                : 1.0f;

            float emissiveStrength = Mathf.Lerp(4.2f, 58f, Mathf.Pow(meltdownT, 1.6f)) * instability;

            if (turbineMatInstance != null && turbineMatInstance.HasProperty(IdEmissionColor))
            {
                turbineMatInstance.SetColor(IdEmissionColor, curCol * emissiveStrength);
            }
            if (visorMatInstance != null && visorMatInstance.HasProperty(IdEmissionColor))
            {
                visorMatInstance.SetColor(IdEmissionColor, Color.Lerp(deepAmber, hotGold, meltdownT * 0.5f) * Mathf.Lerp(3.2f, 6.5f, meltdownT));
            }
            if (corePointLight != null)
            {
                corePointLight.color = curCol;
                corePointLight.intensity = Mathf.Lerp(42f, 680f, Mathf.Pow(meltdownT, 1.5f)) * instability;
                corePointLight.range = Mathf.Lerp(36f, 130f, meltdownT);
            }
            if (visorPointLight != null)
            {
                visorPointLight.intensity = Mathf.Lerp(14f, 45f, meltdownT);
            }

            if (chestAnamorphicFlareQuad != null && chestAnamorphicFlareQuad.activeSelf && mainCamera != null)
            {
                chestAnamorphicFlareQuad.transform.position = chestPos + (mainCamera.transform.position - chestPos).normalized * 1.4f;
                chestAnamorphicFlareQuad.transform.rotation = mainCamera.transform.rotation;
                float flareScaleX = Mathf.Lerp(14f, 44f, meltdownT) * instability;
                float flareScaleY = Mathf.Lerp(1.1f, 3.6f, meltdownT) * instability;
                chestAnamorphicFlareQuad.transform.localScale = new Vector3(flareScaleX, flareScaleY, 1f);

                if (chestAnamorphicFlareMat != null)
                {
                    float flareIntensity = Mathf.Lerp(0.35f, 5.2f, Mathf.Pow(meltdownT, 1.4f)) * instability;
                    if (chestAnamorphicFlareMat.HasProperty(IdFlareColor))
                    {
                        chestAnamorphicFlareMat.SetColor(IdFlareColor, curCol);
                        chestAnamorphicFlareMat.SetFloat(IdIntensity, flareIntensity);
                        chestAnamorphicFlareMat.SetFloat(IdHorizontalStretch, Mathf.Lerp(10f, 24f, meltdownT));
                    }
                    else if (chestAnamorphicFlareMat.HasProperty(IdBaseColor))
                    {
                        chestAnamorphicFlareMat.SetColor(IdBaseColor, curCol * Mathf.Clamp(flareIntensity, 0f, 3f));
                    }
                }
            }

            if (meltdownT > 0.01f)
            {
                if (volBloom != null)
                {
                    volBloom.intensity.Override(Mathf.Lerp(1.35f, 5.4f, meltdownT) * instability);
                    volBloom.threshold.Override(Mathf.Lerp(0.90f, 0.52f, meltdownT));
                    volBloom.scatter.Override(Mathf.Lerp(0.75f, 0.90f, meltdownT));
                }
                if (volColorAdj != null)
                {
                    volColorAdj.postExposure.Override(Mathf.Lerp(0.22f, 0.65f, meltdownT));
                }
            }
        }

        private void LateUpdate()
        {
            if (!isCutsceneRunning) return;

            if (applyLateUpdatePoses && jaegerTransform != null)
            {
                Vector3 up = jaegerTransform.up;
                Vector3 rgt = jaegerTransform.right;
                Vector3 fwd = jaegerTransform.forward;

                // Wormhole heavy braced tumble pose (matches Beat 2 sacrificial plunge — bent staggered knees & braced arms)
                if (divePoseWeight > 0.01f)
                {
                    float dw = Mathf.Clamp01(divePoseWeight);
                    float turb = Mathf.Sin(Time.time * 4.5f) * 0.45f;
                    if (upperArmL != null && forearmL != null && handL != null)
                    {
                        Vector3 tL = upperArmL.position + (-up * 2.5f + fwd * 3.1f - rgt * 2.3f + up * turb);
                        SolveTwoBoneIKWorld(upperArmL, forearmL, handL, Vector3.Lerp(handL.position, tL, dw), (-rgt * 0.85f - up * 0.35f - fwd * 0.25f).normalized, keepTipLocalRotation: true);
                    }
                    if (upperArmR != null && forearmR != null && handR != null)
                    {
                        Vector3 tR = upperArmR.position + (-up * 2.5f + fwd * 3.1f + rgt * 2.3f - up * turb);
                        SolveTwoBoneIKWorld(upperArmR, forearmR, handR, Vector3.Lerp(handR.position, tR, dw), (rgt * 0.85f - up * 0.35f - fwd * 0.25f).normalized, keepTipLocalRotation: true);
                    }
                    if (thighL != null && shinL != null && footL != null)
                    {
                        Vector3 fL = thighL.position + (-up * 8.4f + fwd * 3.0f - rgt * 0.45f + fwd * turb * 0.8f);
                        SolveTwoBoneIKWorld(thighL, shinL, footL, Vector3.Lerp(footL.position, fL, dw), (fwd * 0.9f - rgt * 0.25f).normalized, keepTipLocalRotation: true);
                    }
                    if (thighR != null && shinR != null && footR != null)
                    {
                        Vector3 fR = thighR.position + (-up * 9.8f - fwd * 1.4f + rgt * 0.45f - fwd * turb * 0.8f);
                        SolveTwoBoneIKWorld(thighR, shinR, footR, Vector3.Lerp(footR.position, fR, dw), (fwd * 0.85f + rgt * 0.28f).normalized, keepTipLocalRotation: true);
                    }
                }

                // Weightless zero-G relaxed drift pose before spreading into Beat 5 Cruciform T-pose
                if (zeroGFloatWeight > 0.01f && tPoseWeight < 0.99f)
                {
                    float zw = Mathf.Clamp01(zeroGFloatWeight) * (1f - Mathf.Clamp01(tPoseWeight));
                    float sway = Mathf.Sin(Time.time * 1.15f) * 0.35f;
                    if (thighL != null && shinL != null && footL != null)
                    {
                        Vector3 fL = thighL.position + (-up * 10.2f + fwd * 1.85f - rgt * 0.55f);
                        SolveTwoBoneIKWorld(thighL, shinL, footL, Vector3.Lerp(footL.position, fL, zw), (fwd * 0.9f - rgt * 0.25f).normalized, keepTipLocalRotation: true);
                    }
                    if (thighR != null && shinR != null && footR != null)
                    {
                        Vector3 fR = thighR.position + (-up * 10.7f + fwd * 0.45f + rgt * 0.55f);
                        SolveTwoBoneIKWorld(thighR, shinR, footR, Vector3.Lerp(footR.position, fR, zw), (fwd * 0.85f + rgt * 0.25f).normalized, keepTipLocalRotation: true);
                    }
                    if (upperArmL != null && forearmL != null && handL != null)
                    {
                        Vector3 aL = upperArmL.position + (-up * 3.8f + fwd * 1.6f - rgt * 2.8f + up * sway);
                        SolveTwoBoneIKWorld(upperArmL, forearmL, handL, Vector3.Lerp(handL.position, aL, zw), (-rgt * 0.85f - fwd * 0.3f).normalized, keepTipLocalRotation: true);
                    }
                    if (upperArmR != null && forearmR != null && handR != null)
                    {
                        Vector3 aR = upperArmR.position + (-up * 3.8f + fwd * 1.6f + rgt * 2.8f - up * sway);
                        SolveTwoBoneIKWorld(upperArmR, forearmR, handR, Vector3.Lerp(handR.position, aR, zw), (rgt * 0.85f - fwd * 0.3f).normalized, keepTipLocalRotation: true);
                    }
                }

                // Cruciform floating T-pose with upward-tilted head (0:24–0:33)
                if (applyTPose && tPoseWeight > 0.005f)
                {
                    if (upperArmL != null && forearmL != null)
                    {
                        AlignBoneChainDirection(upperArmL, forearmL, (-rgt + up * 0.08f).normalized, tPoseWeight);
                    }
                    if (forearmL != null && handL != null)
                    {
                        AlignBoneChainDirection(forearmL, handL, (-rgt - up * 0.03f + fwd * 0.05f).normalized, tPoseWeight);
                    }
                    if (upperArmR != null && forearmR != null)
                    {
                        AlignBoneChainDirection(upperArmR, forearmR, (rgt + up * 0.08f).normalized, tPoseWeight);
                    }
                    if (forearmR != null && handR != null)
                    {
                        AlignBoneChainDirection(forearmR, handR, (rgt - up * 0.03f + fwd * 0.05f).normalized, tPoseWeight);
                    }

                    if (headBone != null)
                    {
                        headBone.rotation = Quaternion.AngleAxis(-headTiltUpDeg * tPoseWeight, rgt) * headBone.rotation;
                    }
                }
            }

            if (lockCameraInLateUpdate && mainCamera != null)
            {
                Vector3 shakePos = cameraShake != null ? cameraShake.CurrentOffset : Vector3.zero;
                Vector3 shakeRot = cameraShake != null ? cameraShake.CurrentRotationOffset : Vector3.zero;
                mainCamera.transform.position = camTargetPos + camTargetRot * shakePos;
                mainCamera.transform.rotation = camTargetRot * Quaternion.Euler(shakeRot);
                mainCamera.fieldOfView = camTargetFov;
            }
        }

        private static void AlignBoneChainDirection(Transform bone, Transform childBone, Vector3 targetWorldDir, float weight)
        {
            Vector3 curDir = childBone.position - bone.position;
            if (curDir.sqrMagnitude < 0.00001f) return;
            Quaternion delta = Quaternion.FromToRotation(curDir.normalized, targetWorldDir.normalized);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, delta, Mathf.Clamp01(weight)) * bone.rotation;
        }

        private static void SolveTwoBoneIKWorld(Transform root, Transform mid, Transform tip, Vector3 targetPos, Vector3 poleForward, bool keepTipLocalRotation = false)
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
            if (keepTipLocalRotation) tip.localRotation = origTipLocalRot;
            else tip.rotation = origTipRot;
        }

        private Vector3 GetTurbineWorldCenter()
        {
            if (chestBone != null) return chestBone.position + (jaegerTransform != null ? jaegerTransform.forward * 1.4f : Vector3.forward) + Vector3.up * 0.35f;
            return jaegerTransform != null ? jaegerTransform.position + Vector3.up * 15f : Vector3.zero;
        }

        private void SetJaegerPose(Vector3 pos, Quaternion rot)
        {
            if (jaegerTransform != null) jaegerTransform.SetPositionAndRotation(pos, rot);
        }

        private void SetSlatternPose(Vector3 pos, Quaternion rot)
        {
            if (slatternTransform != null) slatternTransform.SetPositionAndRotation(pos, rot);
        }

        #region Audio Sequence Helpers

        private void StartWormholeAudio()
        {
            var audioMgr = PacificRimAudioManager.Instance;
            if (audioMgr == null) return;

            sfxOneShotSource = CreateSfxSource();

            wormholeWindSource = CreateLoop("Wormhole_WindRush", audioMgr.GetClip("Wind (7)"), 0.72f, 1.15f);
            wormholeRumbleSource = CreateLoop("Wormhole_SubRumble", audioMgr.GetClip("Earthquake 3 - Big"), 0.75f, 0.85f);
            wormholeSparksSource = CreateLoop("Wormhole_ElectricArcs", audioMgr.GetClip("EmpSparksArcing_Loop"), 0.55f, 1.10f);
            PlayOneShot2D("Spaceship passing by", 0.75f, 0.85f);
        }

        private void StopWormholeAudio()
        {
            if (wormholeWindSource != null) wormholeWindSource.Stop();
            if (wormholeRumbleSource != null) wormholeRumbleSource.Stop();
            if (wormholeSparksSource != null) wormholeSparksSource.Stop();
        }

        private void StartZeroGravityVacuumAudio()
        {
            var audioMgr = PacificRimAudioManager.Instance;
            if (audioMgr == null) return;

            // Instant drop from deafening wormhole into eerie, hollow zero-G vacuum!
            PacificRimMusicManager.Instance?.StopMusicImmediate();
            AudioClip cavern = audioMgr.GetClip("Cavern Atmosphere - Loop") ?? audioMgr.GetClip("Ice Cavern-Loop");
            vacuumDroneSource = CreateLoop("ZeroG_VacuumAtmosphere", cavern, 0.14f, 0.72f);
            PlayOneShot2D("Ghost Manifestation 2", 0.30f, 0.65f);
        }

        private void StartMeltdownAudio()
        {
            var audioMgr = PacificRimAudioManager.Instance;
            if (audioMgr == null) return;

            turbineOverloadSource = CreateLoop("CoreMeltdown_Turbine", audioMgr.GetClip("JaegerVortexTurbine_Loop"), 0.22f, 0.90f);
            alarmPulseSource = CreateLoop("CoreMeltdown_Sparks", audioMgr.GetClip("EmpSparksArcing_Loop"), 0.05f, 0.95f);
            PlayOneShot2D("EmpChargeUp", 0.65f, 0.90f);
        }

        private void UpdateMeltdownAudio(float meltdownT)
        {
            float sfxVol = PacificRimAudioManager.Instance != null ? PacificRimAudioManager.Instance.SfxVolume : 0.55f;
            if (turbineOverloadSource != null)
            {
                turbineOverloadSource.pitch = Mathf.Lerp(0.90f, 2.35f, meltdownT);
                turbineOverloadSource.volume = Mathf.Lerp(0.22f, 0.95f, meltdownT) * sfxVol;
            }
            if (alarmPulseSource != null)
            {
                alarmPulseSource.pitch = Mathf.Lerp(0.95f, 1.45f, meltdownT);
                alarmPulseSource.volume = Mathf.Lerp(0.05f, 0.55f, meltdownT) * sfxVol;
            }
            if (vacuumDroneSource != null)
            {
                vacuumDroneSource.volume = Mathf.Lerp(0.14f, 0.04f, meltdownT) * sfxVol;
            }
        }

        private void StopMeltdownAndVacuumAudio()
        {
            if (vacuumDroneSource != null) vacuumDroneSource.Stop();
            if (turbineOverloadSource != null) turbineOverloadSource.Stop();
            if (alarmPulseSource != null) alarmPulseSource.Stop();
        }

        private void CutAllAudioForAbsoluteSilence()
        {
            StopWormholeAudio();
            StopMeltdownAndVacuumAudio();
            PacificRimMusicManager.Instance?.StopMusicImmediate();
            audioMutedForSilence = true;
            AudioListener.volume = 0f;
        }

        private void RestoreAudioAndTriggerNuclearBlast(Vector3 blastPos)
        {
            RestoreGlobalAudioSafely();

            PlayOneShot2D("Explosion 1", 1.0f, 0.85f);
            PlayOneShot2D("Explosion 2", 1.0f, 0.72f);
            PlayOneShot2D("Explosion 8", 1.0f, 0.78f);
            PlayOneShot2D("Earthquake 3 - Big", 1.0f, 0.70f);
            PlayOneShot2D("Cannon impact 9", 0.95f, 0.80f);
            PlayOneShot2D("EmpDischargeBlast", 0.90f, 0.85f);
        }

        private AudioSource CreateSfxSource()
        {
            if (sfxOneShotSource != null) return sfxOneShotSource;
            var go = new GameObject("Anteverse_OneShotSfx");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.bypassListenerEffects = true;
            return src;
        }

        private AudioSource CreateLoop(string objName, AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return null;
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.spatialBlend = 0f;
            src.bypassListenerEffects = true;
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
            if (clip == null) return;
            if (sfxOneShotSource == null) CreateSfxSource();
            sfxOneShotSource.pitch = pitch;
            sfxOneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume * audioMgr.SfxVolume));
        }

        #endregion

        #region UI & Campaign Complete Screen

        private void EnsureUI()
        {
            EnsureEventSystem();

            var canvasGo = GameObject.Find("PacificRim_UICanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("PacificRim_UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            }
            uiCanvas = canvasGo.GetComponent<Canvas>() ?? canvasGo.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiCanvas.sortingOrder = 500;
            var scaler = canvasGo.GetComponent<CanvasScaler>() ?? canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (canvasGo.GetComponent<GraphicRaycaster>() == null) canvasGo.AddComponent<GraphicRaycaster>();

            // Letterbox bars
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
                letterboxCanvasGroup.alpha = 1f;
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
                var skipText = skipGo.GetComponent<Text>();
                skipText.supportRichText = true;
                skipText.text = "<color=#46c3ff>[SPACE]</color>  SKIP";
                skipText.alignment = TextAnchor.MiddleCenter;
                skipText.fontSize = 12;
                skipText.fontStyle = FontStyle.Bold;
                skipText.font = PacificRimUITheme.GetDefaultFont();
                skipText.color = new Color(PacificRimUITheme.TextDim.r, PacificRimUITheme.TextDim.g, PacificRimUITheme.TextDim.b, 0.72f);
            }

            // Full-screen whiteout / fade overlay
            var existingWhite = canvasGo.transform.Find("Whiteout_Overlay");
            GameObject whiteGo = existingWhite != null
                ? existingWhite.gameObject
                : new GameObject("Whiteout_Overlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            if (existingWhite == null) whiteGo.transform.SetParent(canvasGo.transform, false);
            var wrt = whiteGo.GetComponent<RectTransform>();
            wrt.anchorMin = Vector2.zero;
            wrt.anchorMax = Vector2.one;
            wrt.offsetMin = Vector2.zero;
            wrt.offsetMax = Vector2.zero;
            whiteoutOverlayGroup = whiteGo.GetComponent<CanvasGroup>();
            whiteoutOverlayGroup.alpha = 0f;
            whiteoutOverlayGroup.blocksRaycasts = false;
            whiteoutOverlayGroup.interactable = false;
            whiteoutOverlayImage = whiteGo.GetComponent<Image>();
            whiteoutOverlayImage.color = Color.white;
            whiteoutOverlayImage.raycastTarget = false;

            BuildVictoryPanel(canvasGo.transform);
        }

        private void BuildVictoryPanel(Transform canvasParent)
        {
            var existing = canvasParent.Find("CampaignVictory_Panel");
            if (existing != null) Destroy(existing.gameObject);

            Font font = PacificRimUITheme.GetDefaultFont();
            victoryPanelRoot = new GameObject("CampaignVictory_Panel", typeof(RectTransform), typeof(CanvasGroup));
            victoryPanelRoot.transform.SetParent(canvasParent, false);
            var rootRt = victoryPanelRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            victoryPanelGroup = victoryPanelRoot.GetComponent<CanvasGroup>();
            victoryPanelGroup.alpha = 0f;

            var cardGo = new GameObject("VictoryCard", typeof(RectTransform));
            cardGo.transform.SetParent(victoryPanelRoot.transform, false);
            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(680f, 440f);

            var chamfer = cardGo.AddComponent<UIChamferGraphic>();
            chamfer.chamferSize = 14f;
            chamfer.borderThickness = 1.25f;
            chamfer.fillColor = PacificRimUITheme.PanelOpaque;
            chamfer.borderColor = PacificRimUITheme.Blue;

            var accentGo = new GameObject("LeftAccent", typeof(RectTransform), typeof(Image));
            accentGo.transform.SetParent(cardGo.transform, false);
            var aRt = accentGo.GetComponent<RectTransform>();
            aRt.anchorMin = new Vector2(0f, 0.12f);
            aRt.anchorMax = new Vector2(0f, 0.88f);
            aRt.pivot = new Vector2(0f, 0.5f);
            aRt.sizeDelta = new Vector2(4f, 0f);
            accentGo.GetComponent<Image>().color = PacificRimUITheme.BlueBright;

            var eyebrow = CreateLabel("Eyebrow", cardGo.transform, font, 12, FontStyle.Bold, PacificRimUITheme.Success, TextAnchor.MiddleCenter,
                "OPERATION PITFALL COMPLETE  //  THE BREACH IS SEALED");
            SetTopCenter(eyebrow.rectTransform, -34f, 600f, 20f);

            var title = CreateLabel("Title", cardGo.transform, font, 36, FontStyle.Bold, PacificRimUITheme.Text, TextAnchor.MiddleCenter,
                "VICTORY");
            SetTopCenter(title.rectTransform, -64f, 600f, 46f);

            var epilogue = CreateLabel("Epilogue", cardGo.transform, font, 15, FontStyle.Normal, PacificRimUITheme.TextDim, TextAnchor.MiddleCenter,
                "Gipsy Danger's nuclear vortex core detonated inside the Anteverse, vaporizing Category V Slattern and collapsing the interdimensional Breach forever.");
            epilogue.horizontalOverflow = HorizontalWrapMode.Wrap;
            epilogue.verticalOverflow = VerticalWrapMode.Overflow;
            epilogue.lineSpacing = 1.25f;
            SetTopCenter(epilogue.rectTransform, -130f, 560f, 74f);

            float bestTime = PacificRimCombatRecords.GetBestTime(MainMenuUI.Level3SceneName);
            string timeLine = RecordedCombatTime > 0.5f
                ? $"SLATTERN TIME {PacificRimCombatRecords.FormatTime(RecordedCombatTime)}  ·  BEST {PacificRimCombatRecords.FormatTime(bestTime)}"
                : $"BEST TIME {PacificRimCombatRecords.FormatTime(bestTime)}";

            var recordTxt = CreateLabel("RecordText", cardGo.transform, font, 13, FontStyle.Bold, PacificRimUITheme.Amber, TextAnchor.MiddleCenter, timeLine);
            SetTopCenter(recordTxt.rectTransform, -220f, 560f, 26f);

            // Buttons: MAIN MENU (Primary) + REPLAY OPERATION
            var btnMainMenu = CreateVictoryButton("Btn_MainMenu", cardGo.transform, font, new Vector2(0f, -274f), new Vector2(360f, 48f),
                "RETURN TO MAIN MENU", isPrimary: true, onClick: () =>
                {
                    PacificRimUISound.PlaySuccess();
                    PacificRimLoadingScreen.LoadScene("MainMenuScene");
                });

            var btnReplay = CreateVictoryButton("Btn_ReplayPitfall", cardGo.transform, font, new Vector2(0f, -338f), new Vector2(360f, 42f),
                "REPLAY OPERATION PITFALL", isPrimary: false, onClick: () =>
                {
                    PacificRimUISound.PlayClick();
                    PacificRimLoadingScreen.LoadScene(MainMenuUI.Level3SceneName);
                });

            _ = btnMainMenu;
            _ = btnReplay;

            victoryPanelRoot.SetActive(false);
        }

        private static PacificRimButton CreateVictoryButton(
            string name, Transform parent, Font font, Vector2 topPos, Vector2 size,
            string text, bool isPrimary, System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(PacificRimButton));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = topPos;
            rt.sizeDelta = size;

            var uBtn = go.GetComponent<Button>();
            uBtn.transition = Selectable.Transition.None;

            var chamfer = go.AddComponent<UIChamferGraphic>();
            chamfer.chamferSize = 8f;
            chamfer.borderThickness = 1f;
            chamfer.chamferTopLeft = false;
            chamfer.chamferTopRight = true;
            chamfer.chamferBottomLeft = true;
            chamfer.chamferBottomRight = false;
            chamfer.fillColor = isPrimary ? PacificRimUITheme.CardFill : PacificRimUITheme.Panel;
            chamfer.borderColor = isPrimary ? PacificRimUITheme.Blue : PacificRimUITheme.Line;

            var lbl = CreateLabel("Label", go.transform, font, isPrimary ? 14 : 12, FontStyle.Bold, PacificRimUITheme.Text, TextAnchor.MiddleCenter, text);
            lbl.rectTransform.anchorMin = Vector2.zero;
            lbl.rectTransform.anchorMax = Vector2.one;
            lbl.rectTransform.offsetMin = Vector2.zero;
            lbl.rectTransform.offsetMax = Vector2.zero;

            var prBtn = go.GetComponent<PacificRimButton>();
            prBtn.unityButton = uBtn;
            prBtn.chamferGraphic = chamfer;
            prBtn.label = lbl;
            prBtn.isPrimary = isPrimary;
            prBtn.playSuccessOnClick = isPrimary;
            prBtn.onClick = onClick;
            return prBtn;
        }

        private static Text CreateLabel(string name, Transform parent, Font font, int size, FontStyle style, Color col, TextAnchor align, string content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = col;
            t.alignment = align;
            t.text = content;
            t.raycastTarget = false;
            return t;
        }

        private static void SetTopCenter(RectTransform rt, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(w, h);
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

        private static void EnsureEventSystem()
        {
            var es = FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                es = esGo.AddComponent<EventSystem>();
            }
#if ENABLE_INPUT_SYSTEM
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null) Destroy(legacy);
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
#else
            if (es.GetComponent<StandaloneInputModule>() == null)
            {
                es.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }

        private static bool WasSkipPressed()
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame ||
                                   kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                    return true;
#endif
            }
            catch { }
            try
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))
                    return true;
            }
            catch { }
            return false;
        }

        #endregion
    }
}
