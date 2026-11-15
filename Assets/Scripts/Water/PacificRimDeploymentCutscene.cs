using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PacificRimCutscene
{
    /// <summary>
    /// Orchestrates the Pacific Rim cinematic deployment intro:
    /// - Spawns Gipsy Danger airborne suspended by heavy-lift Chinook helicopters via tension cables
    /// - Theatrical anamorphic 2.39:1 letterbox bars with combat HUD disabled
    /// - Extended volumetric searchlight beams cutting through the storm
    /// - Dynamic cinematic camera choreography tracking the aerial convoy through driving rain
    /// - Leatherback Kaiju idle facing away, turning around when the iconic foghorn sounds
    /// - Mechanical clamp release, freefall, and colossal ocean splashdown with water displacement waves
    /// - Smooth letterbox exit and tactical neural handshake HUD boot-up flicker before combat starts
    /// - Robust skip detection compatible with both New Input System and Legacy Input
    /// </summary>
    public class PacificRimDeploymentCutscene : MonoBehaviour
    {
        public static PacificRimDeploymentCutscene Instance { get; private set; }

        [Header("Convoy & Target Coordinates")]
        public Transform jaegerTransform;
        public Vector3 targetLandingPosition = new Vector3(0f, 2.977f, 0f);
        public Vector3 airborneStartPosition = new Vector3(0f, 36f, -70f);
        public Vector3 airborneReleasePosition = new Vector3(0f, 27.5f, -4f);

        [Header("Timing")]
        public float transportDuration = 5.2f;
        public float freefallTimeout = 3.5f;

        [Header("Assets & Prefabs")]
        public GameObject chinookPrefab;
        public Material cableMaterial;

        [Header("Cinematic Letterbox UI")]
        public RectTransform letterboxRoot;
        public RectTransform topBar;
        public RectTransform bottomBar;
        public CanvasGroup letterboxCanvasGroup;
        public Text skipPromptText;

        [Header("Cutscene State")]
        public bool playOnStart = true;
        public bool isCutsceneRunning { get; private set; }

        // Runtime convoy references: Chinooks fly well ABOVE Gipsy Danger (Gipsy is 24.5m tall)
        // High heavy-lift box formation so Gipsy is suspended hanging below the helicopters
        private readonly List<GameObject> activeChinooks = new List<GameObject>();
        private readonly List<LineRenderer> activeCables = new List<LineRenderer>();
        private readonly List<Vector3> chinookLocalOffsets = new List<Vector3>()
        {
            new Vector3(-26f, 48f, 22f),  // Front Left (48m above Gipsy's feet, ~24m above Gipsy's head)
            new Vector3(26f, 48f, 22f),   // Front Right
            new Vector3(-28f, 50f, -22f), // Rear Left
            new Vector3(28f, 50f, -22f)   // Rear Right
        };

        private AudioSource chinookAudioSource;
        private PacificRimWater.SimpleJaegerController jaegerController;
        private PacificRimWater.JaegerVisualSockets visualSockets;
        private PacificRimWater.KaijuAI kaijuAI;
        private Camera mainCamera;
        private MonoBehaviour cameraFollowScript;
        private Rigidbody jaegerRigidbody;
        private Transform jaegerChest;
        private Coroutine activeCutsceneRoutine;
        private Coroutine activeKaijuRotateRoutine;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            ResolveReferences();

            // If the Loading Screen is actively managing the transition and deep preloading,
            // wait for it to complete; it will call StartCutscene() when all scene assets are 100% pre-warmed.
            if (PacificRimUI.PacificRimLoadingScreen.Instance != null && PacificRimUI.PacificRimLoadingScreen.Instance.IsLoading)
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
            yield return PacificRimUI.PacificRimLoadingScreen.PreloadMainSceneAssetsRoutine();
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
                jaegerController = jaegerTransform.GetComponent<PacificRimWater.SimpleJaegerController>();
                visualSockets = jaegerTransform.GetComponent<PacificRimWater.JaegerVisualSockets>();
                jaegerRigidbody = jaegerTransform.GetComponent<Rigidbody>();

                // Locate Chest bone for cable harness
                foreach (var t in jaegerTransform.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Chest")
                    {
                        jaegerChest = t;
                        break;
                    }
                }
            }

            var kgo = GameObject.Find("Kaiju_Leatherback");
            if (kgo != null) kaijuAI = kgo.GetComponent<PacificRimWater.KaijuAI>();

            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraFollowScript = mainCamera.GetComponent<PacificRimWater.JaegerCameraFollow>();
            }

            EnsureLetterboxUI();
        }

        public void EnsureLetterboxUI()
        {
            if (letterboxRoot != null) return;

            var canvasGo = GameObject.Find("PacificRim_UICanvas");
            if (canvasGo == null) return;

            var existing = canvasGo.transform.Find("Cinematic_Letterbox");
            if (existing != null)
            {
                letterboxRoot = existing.GetComponent<RectTransform>();
                letterboxCanvasGroup = existing.GetComponent<CanvasGroup>();
                topBar = existing.Find("TopBar") as RectTransform;
                bottomBar = existing.Find("BottomBar") as RectTransform;
                if (topBar != null)
                {
                    var topImage = topBar.GetComponent<Image>();
                    if (topImage != null) topImage.color = Color.black;
                }
                if (bottomBar != null)
                {
                    var botImage = bottomBar.GetComponent<Image>();
                    if (botImage != null) botImage.color = Color.black;
                }
                var skip = existing.Find("SkipPrompt");
                if (skip != null) skipPromptText = skip.GetComponent<Text>();
                return;
            }

            var rootGo = new GameObject("Cinematic_Letterbox", typeof(RectTransform), typeof(CanvasGroup));
            rootGo.transform.SetParent(canvasGo.transform, false);
            letterboxRoot = rootGo.GetComponent<RectTransform>();
            letterboxRoot.anchorMin = Vector2.zero;
            letterboxRoot.anchorMax = Vector2.one;
            letterboxRoot.offsetMin = Vector2.zero;
            letterboxRoot.offsetMax = Vector2.zero;

            letterboxCanvasGroup = rootGo.GetComponent<CanvasGroup>();
            letterboxCanvasGroup.alpha = 0f;
            letterboxCanvasGroup.blocksRaycasts = false;
            letterboxCanvasGroup.interactable = false;

            // Top Bar
            var topGo = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
            topGo.transform.SetParent(letterboxRoot, false);
            topBar = topGo.GetComponent<RectTransform>();
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.sizeDelta = new Vector2(0f, 135f);
            var topImg = topGo.GetComponent<Image>();
            topImg.color = Color.black;

            // Bottom Bar
            var botGo = new GameObject("BottomBar", typeof(RectTransform), typeof(Image));
            botGo.transform.SetParent(letterboxRoot, false);
            bottomBar = botGo.GetComponent<RectTransform>();
            bottomBar.anchorMin = new Vector2(0f, 0f);
            bottomBar.anchorMax = new Vector2(1f, 0f);
            bottomBar.pivot = new Vector2(0.5f, 0f);
            bottomBar.sizeDelta = new Vector2(0f, 135f);
            var botImg = botGo.GetComponent<Image>();
            botImg.color = Color.black;

            // Skip Prompt
            var skipGo = new GameObject("SkipPrompt", typeof(RectTransform), typeof(Text));
            skipGo.transform.SetParent(bottomBar, false);
            var skipRt = skipGo.GetComponent<RectTransform>();
            skipRt.anchorMin = new Vector2(0.5f, 0.5f);
            skipRt.anchorMax = new Vector2(0.5f, 0.5f);
            skipRt.sizeDelta = new Vector2(400f, 30f);
            skipPromptText = skipGo.GetComponent<Text>();
            skipPromptText.text = "PRESS [SPACE] TO SKIP CONVOY CUTSCENE";
            skipPromptText.alignment = TextAnchor.MiddleCenter;
            skipPromptText.fontSize = 14;
            skipPromptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            skipPromptText.color = new Color(0.6f, 0.8f, 0.9f, 0.45f);
        }

        /// <summary>
        /// Robust input polling supporting both Unity's New Input System and legacy manager.
        /// Prevents InvalidOperationException when New Input System is exclusively active.
        /// </summary>
        public bool WasSkipPressed()
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame ||
                        kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
                    {
                        return true;
                    }
                }
                var gp = UnityEngine.InputSystem.Gamepad.current;
                if (gp != null)
                {
                    if (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame)
                    {
                        return true;
                    }
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

        public IEnumerator RunDeploymentCutscene()
        {
            if (isCutsceneRunning || jaegerTransform == null) yield break;
            isCutsceneRunning = true;

            // 1. Lock player control & hide combat HUD
            if (jaegerController != null)
            {
                jaegerController.isCutsceneActive = true;
            }
            if (PacificRimUI.JaegerHUD.Instance != null)
            {
                PacificRimUI.JaegerHUD.Instance.SetCutsceneHidden(true);
            }

            // 2. Set Gipsy initial airborne transform
            if (jaegerRigidbody != null)
            {
                jaegerRigidbody.isKinematic = true;
                jaegerRigidbody.position = airborneStartPosition;
                jaegerRigidbody.rotation = Quaternion.Euler(6f, 0f, 0f); // slight forward tilt from cable tension
            }
            jaegerTransform.position = airborneStartPosition;
            jaegerTransform.rotation = Quaternion.Euler(6f, 0f, 0f);

            // Extend volumetric searchlight beams during the cutscene
            if (visualSockets != null)
            {
                visualSockets.SetCutsceneBeamMultiplier(3.5f);
            }

            // 3. Freeze Kaiju AI and face away initially (+Z)
            if (kaijuAI != null)
            {
                kaijuAI.isCutsceneMode = true;
                kaijuAI.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }

            // 4. Show Cinematic Letterbox Bars
            EnsureLetterboxUI();
            if (letterboxCanvasGroup != null)
            {
                letterboxCanvasGroup.alpha = 1f;
            }

            // 5. Spawn 4-Chinook Convoy Squadron & Tension Cables
            SpawnChinookSquadron();
            StartChinookAudio();

            // 6. Enable zoomed-out Cutscene FreeCam so the player sees the full stormy environment,
            // with mouse look orbit and Arrow Up / Arrow Down distance zoom
            if (cameraFollowScript != null)
            {
                cameraFollowScript.enabled = true;
            }
            if (PacificRimWater.JaegerCameraFollow.Instance != null)
            {
                PacificRimWater.JaegerCameraFollow.Instance.EnableCutsceneFreeCam(initialDistance: 72f, initialPitch: 20f);
            }

            // Stage 1: Convoy Approach (glide smoothly from airborneStartPosition to airborneReleasePosition)
            float elapsed = 0f;
            bool skipped = false;
            bool foghornTriggered = false;
            float foghornLeadTime = 1.4f;

            while (elapsed < transportDuration)
            {
                if (WasSkipPressed())
                {
                    skipped = true;
                    break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / transportDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 currentPos = Vector3.Lerp(airborneStartPosition, airborneReleasePosition, smoothT);
                if (jaegerRigidbody != null) jaegerRigidbody.position = currentPos;
                jaegerTransform.position = currentPos;

                UpdateChinookPositions(currentPos);
                UpdateCables();

                // Dynamic Cinematic Camera Choreography
                UpdateCinematicCamera(currentPos, t);

                // Trigger Foghorn and Kaiju turnaround before release
                if (!foghornTriggered && elapsed >= (transportDuration - foghornLeadTime))
                {
                    foghornTriggered = true;
                    TriggerFoghornAndKaijuTurn();
                }

                yield return null;
            }

            if (skipped)
            {
                yield return StartCoroutine(HandleCutsceneSkipped());
                yield break;
            }

            // Ensure foghorn triggered if not yet
            if (!foghornTriggered)
            {
                TriggerFoghornAndKaijuTurn();
            }

            // Stage 2: Mechanical Clamp Release & Cable Whiplash
            yield return StartCoroutine(ReleaseCablesAndFreefall());

            // Stage 3: Colossal Ocean Splashdown
            yield return StartCoroutine(ExecuteTitanSplashdown());

            // Stage 4: Cinematic Exit & HUD Neural Boot Flicker
            yield return StartCoroutine(ExitCutsceneAndBootHUD());

            isCutsceneRunning = false;
        }

        private void TriggerFoghornAndKaijuTurn()
        {
            // Play Iconic Gipsy Danger Foghorn
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayGipsyFoghorn(jaegerTransform.position + Vector3.up * 18f);

            // Turn Leatherback 180 degrees smoothly to face Gipsy
            if (kaijuAI != null)
            {
                if (activeKaijuRotateRoutine != null) StopCoroutine(activeKaijuRotateRoutine);
                activeKaijuRotateRoutine = StartCoroutine(SmoothRotateKaiju(1.4f));
            }
        }

        private IEnumerator SmoothRotateKaiju(float duration)
        {
            if (kaijuAI == null) yield break;

            Quaternion startRot = kaijuAI.transform.rotation;
            // Target is facing towards Gipsy (towards negative Z / 180 degrees yaw)
            Quaternion targetRot = Quaternion.Euler(0f, 180f, 0f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                if (kaijuAI != null)
                {
                    kaijuAI.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                }
                yield return null;
            }

            if (kaijuAI != null)
            {
                kaijuAI.transform.rotation = targetRot;
            }
            activeKaijuRotateRoutine = null;
        }

        private void SpawnChinookSquadron()
        {
            activeChinooks.Clear();
            activeCables.Clear();

            for (int i = 0; i < chinookLocalOffsets.Count; i++)
            {
                GameObject chinook = null;
                if (chinookPrefab != null)
                {
                    chinook = Instantiate(chinookPrefab);
                }
                else
                {
                    // Fallback to loading model directly
                    var model = Resources.Load<GameObject>("PacificRim_Chinook") ?? Resources.Load<GameObject>("Models/Chinook");
                    if (model != null) chinook = Instantiate(model);
                }

                if (chinook == null)
                {
                    chinook = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    chinook.transform.localScale = new Vector3(4f, 4f, 18f);
                }

                chinook.name = $"Chinook_Escort_{i + 1}";
                Vector3 worldPos = jaegerTransform.position + chinookLocalOffsets[i];
                chinook.transform.position = worldPos;
                // Chinooks face forward (+Z) by having 180 yaw
                chinook.transform.rotation = Quaternion.Euler(4f, 180f, 0f);
                activeChinooks.Add(chinook);

                // Add cable LineRenderer
                var cableGo = new GameObject($"SuspensionCable_{i + 1}", typeof(LineRenderer));
                cableGo.transform.SetParent(chinook.transform, false);
                var lr = cableGo.GetComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.45f;
                lr.endWidth = 0.45f;
                lr.material = cableMaterial != null ? cableMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                lr.startColor = new Color(0.18f, 0.20f, 0.22f, 1f);
                lr.endColor = new Color(0.28f, 0.32f, 0.36f, 1f);
                lr.useWorldSpace = true;
                activeCables.Add(lr);
            }
        }

        private void UpdateChinookPositions(Vector3 jaegerPos)
        {
            for (int i = 0; i < activeChinooks.Count; i++)
            {
                if (activeChinooks[i] == null) continue;
                // Atmospheric rotor bob
                float bob = Mathf.Sin(Time.time * 2.8f + i * 1.5f) * 0.45f;
                activeChinooks[i].transform.position = jaegerPos + chinookLocalOffsets[i] + Vector3.up * bob;
                activeChinooks[i].transform.rotation = Quaternion.Euler(4f + bob * 1.5f, 180f, 0f);
            }
        }

        private void UpdateCables()
        {
            Transform harnessAnchor = jaegerChest != null ? jaegerChest : jaegerTransform;
            Vector3 anchorPos = harnessAnchor.position + Vector3.up * 1.5f; // Clavicle / shoulder harness

            for (int i = 0; i < activeCables.Count; i++)
            {
                if (activeCables[i] == null || i >= activeChinooks.Count || activeChinooks[i] == null) continue;
                var chinook = activeChinooks[i].transform;
                var hook = chinook.Find("CargoHook") ?? chinook;
                activeCables[i].SetPosition(0, hook.position);
                activeCables[i].SetPosition(1, anchorPos);
            }
        }

        private void UpdateCinematicCamera(Vector3 jaegerPos, float progress)
        {
            if (mainCamera == null) return;
            // When cutscene freecam is active, player has full mouse orbit and arrow zoom control
            if (PacificRimWater.JaegerCameraFollow.Instance != null && PacificRimWater.JaegerCameraFollow.Instance.isCutsceneFreeCam) return;

            // Phase 1: Low-angle dramatic convoy shot framing Gipsy hanging suspended under the soaring Chinooks
            // Phase 2: Orbiting forward chase camera aligning with the ocean drop target
            if (progress < 0.55f)
            {
                float tPhase = progress / 0.55f;
                Vector3 camOffset = Vector3.Lerp(new Vector3(-34f, 6f, -48f), new Vector3(36f, 12f, -44f), tPhase);
                mainCamera.transform.position = jaegerPos + camOffset;
                mainCamera.transform.LookAt(jaegerPos + Vector3.up * 20f);
            }
            else
            {
                float tPhase = (progress - 0.55f) / 0.45f;
                Vector3 camOffset = Vector3.Lerp(new Vector3(36f, 12f, -44f), new Vector3(20f, 18f, -50f), tPhase);
                mainCamera.transform.position = jaegerPos + camOffset;
                mainCamera.transform.LookAt(jaegerPos + Vector3.up * 18f);
            }
        }

        private IEnumerator ReleaseCablesAndFreefall()
        {
            // Play heavy pneumatic clamp release using authentic real pack SFX
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayDeploymentClampRelease(jaegerTransform.position + Vector3.up * 20f);

            // Detach and retract cables
            for (int i = 0; i < activeCables.Count; i++)
            {
                if (activeCables[i] != null) Destroy(activeCables[i].gameObject);
            }
            activeCables.Clear();

            // Smoothly tween volumetric beams back to standard values while dropping
            if (visualSockets != null)
            {
                visualSockets.TweenBeamMultiplier(3.5f, 1.0f, 1.2f);
            }

            // Chinooks bank away and climb into the storm clouds
            StartCoroutine(AscendAndDisperseChinooks());

            // Gipsy plunges into freefall under realistic gravity
            if (jaegerRigidbody != null)
            {
                jaegerRigidbody.isKinematic = false;
                jaegerRigidbody.useGravity = true;
            }

            float fallTimer = 0f;
            float seaLevel = targetLandingPosition.y;

            while (jaegerTransform.position.y > seaLevel + 0.3f && fallTimer < freefallTimeout)
            {
                if (WasSkipPressed())
                {
                    break;
                }

                fallTimer += Time.deltaTime;

                // Cinematic tracking shot following Gipsy's drop (if freecam not active)
                if (mainCamera != null && (PacificRimWater.JaegerCameraFollow.Instance == null || !PacificRimWater.JaegerCameraFollow.Instance.isCutsceneFreeCam))
                {
                    Vector3 camPos = new Vector3(jaegerTransform.position.x + 18f, Mathf.Max(seaLevel + 10f, jaegerTransform.position.y + 4f), jaegerTransform.position.z - 32f);
                    mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, camPos, Time.deltaTime * 7f);
                    mainCamera.transform.LookAt(jaegerTransform.position + Vector3.up * 6f);
                }

                yield return null;
            }

            // Snap to sea level position
            jaegerTransform.position = new Vector3(targetLandingPosition.x, seaLevel, targetLandingPosition.z);
            if (jaegerRigidbody != null)
            {
                jaegerRigidbody.position = jaegerTransform.position;
#if UNITY_6000_0_OR_NEWER
                jaegerRigidbody.linearVelocity = Vector3.zero;
#else
                jaegerRigidbody.velocity = Vector3.zero;
#endif
            }
        }

        private IEnumerator AscendAndDisperseChinooks()
        {
            float duration = 4.0f;
            float elapsed = 0f;

            float startVol = chinookAudioSource != null ? chinookAudioSource.volume : 1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                for (int i = 0; i < activeChinooks.Count; i++)
                {
                    if (activeChinooks[i] == null) continue;
                    // Peel outward and climb into clouds
                    Vector3 outward = (activeChinooks[i].transform.position - jaegerTransform.position).normalized;
                    outward.y = 0f;
                    activeChinooks[i].transform.position += (outward * 18f + Vector3.up * 26f) * Time.deltaTime;
                    activeChinooks[i].transform.rotation = Quaternion.Slerp(activeChinooks[i].transform.rotation, Quaternion.Euler(-18f, 180f + Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg, 0f), Time.deltaTime * 3f);
                }

                if (chinookAudioSource != null)
                {
                    chinookAudioSource.volume = Mathf.Lerp(startVol, 0f, t);
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

        private IEnumerator ExecuteTitanSplashdown()
        {
            Vector3 impactPoint = targetLandingPosition + Vector3.down * 1.5f;

            // 1. Colossal ocean impact splash: massive radial geyser droplets, mist, foam, and displacement waves
            PacificRimWater.WaterSystem.Instance?.AddFootstepWave(impactPoint, 36f, 3.2f);
            PacificRimWater.WaterSplashManager.Instance?.SpawnColossalTitanDropSplash(impactPoint);
            PacificRimWater.WaterSplashManager.Instance?.SpawnBodySplash(impactPoint, Vector3.up, 3.8f, 3.5f);
            PacificRimWater.WaterSplashManager.Instance?.SpawnFootstepSplash(impactPoint, 3.5f);

            // 2. Colossal audio impact
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayOceanFootstep(impactPoint, 2.5f);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuSlamImpact(impactPoint);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayArmorCreak(targetLandingPosition + Vector3.up * 14f, 1.2f);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHydraulicServo(targetLandingPosition + Vector3.up * 10f, 1.0f);

            // 3. Heavy Camera Shake on impact
            if (mainCamera != null)
            {
                var shaker = mainCamera.GetComponent<PacificRimWater.PacificRimCameraShake>();
                if (shaker != null)
                {
                    shaker.TriggerPlayerHitFeedback(isHeavy: true);
                }
            }

            yield return new WaitForSeconds(0.45f);
        }

        private IEnumerator ExitCutsceneAndBootHUD()
        {
            // 1. Cut freecam and slowly move camera back to its over-the-shoulder natural position
            if (cameraFollowScript != null)
            {
                cameraFollowScript.enabled = true;
            }
            if (PacificRimWater.JaegerCameraFollow.Instance != null)
            {
                PacificRimWater.JaegerCameraFollow.Instance.SmoothTransitionToOverTheShoulder(1.8f);
            }

            // 2. Slide letterbox bars offscreen
            float slideDuration = 0.85f;
            float slideElapsed = 0f;

            Vector2 topStart = topBar != null ? topBar.anchoredPosition : Vector2.zero;
            Vector2 botStart = bottomBar != null ? bottomBar.anchoredPosition : Vector2.zero;
            Vector2 topTarget = new Vector2(topStart.x, topStart.y + 160f);
            Vector2 botTarget = new Vector2(botStart.x, botStart.y - 160f);

            while (slideElapsed < slideDuration)
            {
                slideElapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, slideElapsed / slideDuration);

                if (topBar != null) topBar.anchoredPosition = Vector2.Lerp(topStart, topTarget, t);
                if (bottomBar != null) bottomBar.anchoredPosition = Vector2.Lerp(botStart, botTarget, t);
                if (letterboxCanvasGroup != null) letterboxCanvasGroup.alpha = 1f - t;

                yield return null;
            }

            if (letterboxCanvasGroup != null)
            {
                letterboxCanvasGroup.alpha = 0f;
            }

            // 3. Neural Handshake Boot Sequence (HUD flicker back & forth until stabilized)
            bool bootComplete = false;
            if (PacificRimUI.JaegerHUD.Instance != null)
            {
                PacificRimUI.JaegerHUD.Instance.PlayNeuralBootSequence(() => bootComplete = true);
            }
            else
            {
                bootComplete = true;
            }

            while (!bootComplete)
            {
                yield return null;
            }

            // 4. Unlock player control and Kaiju AI
            if (jaegerController != null)
            {
                jaegerController.isCutsceneActive = false;
            }

            if (kaijuAI != null)
            {
                kaijuAI.isCutsceneMode = false;
                if (!kaijuAI.IsDead)
                {
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuAttackGrunt(kaijuAI.transform.position + Vector3.up * 14f);
                }
            }

            Debug.Log("<color=#00e5ff><b>[Deployment Cutscene]</b></color> Gipsy Danger deployed into combat! Neural handshake stabilized.");
        }

        private IEnumerator HandleCutsceneSkipped()
        {
            // Immediate cleanup for skip
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

            if (chinookAudioSource != null)
            {
                Destroy(chinookAudioSource.gameObject);
                chinookAudioSource = null;
            }

            if (activeKaijuRotateRoutine != null)
            {
                StopCoroutine(activeKaijuRotateRoutine);
                activeKaijuRotateRoutine = null;
            }

            // Snap Gipsy to combat landing position
            jaegerTransform.position = targetLandingPosition;
            if (jaegerRigidbody != null)
            {
                jaegerRigidbody.isKinematic = false;
                jaegerRigidbody.useGravity = true;
                jaegerRigidbody.position = targetLandingPosition;
#if UNITY_6000_0_OR_NEWER
                jaegerRigidbody.linearVelocity = Vector3.zero;
#else
                jaegerRigidbody.velocity = Vector3.zero;
#endif
            }

            // Reset visual beams to normal
            if (visualSockets != null)
            {
                visualSockets.SetCutsceneBeamMultiplier(1.0f);
            }

            // Face Leatherback toward Gipsy and unfreeze
            if (kaijuAI != null)
            {
                kaijuAI.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                kaijuAI.isCutsceneMode = false;
            }

            // Unlock player control
            if (jaegerController != null)
            {
                jaegerController.isCutsceneActive = false;
            }

            // Hide letterbox instantly
            if (letterboxCanvasGroup != null)
            {
                letterboxCanvasGroup.alpha = 0f;
            }

            // Restore HUD
            if (PacificRimUI.JaegerHUD.Instance != null)
            {
                PacificRimUI.JaegerHUD.Instance.SetCutsceneHidden(false);
            }

            // Restore combat camera instantly
            if (cameraFollowScript != null)
            {
                cameraFollowScript.enabled = true;
            }
            if (PacificRimWater.JaegerCameraFollow.Instance != null)
            {
                PacificRimWater.JaegerCameraFollow.Instance.SmoothTransitionToOverTheShoulder(0.2f);
            }

            isCutsceneRunning = false;
            Debug.Log("<color=#00e5ff><b>[Deployment Cutscene]</b></color> Cutscene skipped by player.");
            yield break;
        }

        private void StartChinookAudio()
        {
            var go = new GameObject("ChinookSquadronAudio");
            go.transform.SetParent(transform, false);
            chinookAudioSource = go.AddComponent<AudioSource>();
            chinookAudioSource.spatialBlend = 0.35f;
            chinookAudioSource.loop = true;
            chinookAudioSource.volume = 0.15f; // reduced volume for pleasant atmospheric convoy flight
            chinookAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            chinookAudioSource.minDistance = 30f;
            chinookAudioSource.maxDistance = 250f;

            var clip = PacificRimAudio.PacificRimAudioManager.Instance?.GetClip("HelicopterLoop") ??
                       Resources.Load<AudioClip>("HelicopterLoop") ??
#if UNITY_EDITOR
                       UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/HelicopterLoop.mp3") ??
#endif
                       null;

            if (clip != null)
            {
                chinookAudioSource.clip = clip;
                chinookAudioSource.Play();
            }
        }
    }
}
