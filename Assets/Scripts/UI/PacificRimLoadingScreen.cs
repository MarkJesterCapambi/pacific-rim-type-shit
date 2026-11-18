using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PacificRimUI
{
    /// <summary>
    /// Cinema-grade tactical Loading Screen & Asset Preloader for Pacific Rim.
    /// Ensures instantaneous display with zero lag before loading, uses non-blocking
    /// async scene loading with DontDestroyOnLoad framing, and performs targeted asset
    /// warmup without freezing the thread or throwing DX12 keyword errors.
    /// </summary>
    public class PacificRimLoadingScreen : MonoBehaviour
    {
        public static PacificRimLoadingScreen Instance { get; private set; }
        public bool IsLoading { get; private set; }

        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private RectTransform barFill;
        private Text percentText;
        private Text statusText;
        private Text tipText;
        private Text sectorSubText;
        private RectTransform reticleTransform;

        private float targetProgress = 0f;
        private float currentDisplayProgress = 0f;
        private string targetStatus = "INITIALIZING";
        private float tipTimer = 0f;
        private int currentTipIndex = 0;

        private static readonly string[] TacticalTips = new string[]
        {
            "TAP [F] ON IMPACT TO PARRY  ·  0 DMG  ·  +15% STAMINA  ·  STAGGERS KAIJU",
            "DOUBLE-TAP [W A S D] FOR THRUSTER DASH",
            "SPRINT + [RMB] FOR UNINTERRUPTIBLE ROCKET LUNGE",
            "[R] DEPLOYS GD6 CHAIN SWORDS  ·  +DAMAGE & REACH",
            "[T] FIRES I-19 PLASMACASTER  ·  EARN 100% ULTIMATE CHARGE IN COMBAT",
            "[Q] OVERCHARGE  ·  +30% ATTACK SPEED  ·  +25% DMG (15S)",
            "[E] TOGGLES FREECAM  ·  [~] HIDES HUD"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            GetOrCreateInstance();
        }

        public static PacificRimLoadingScreen GetOrCreateInstance()
        {
            if (Instance != null) return Instance;

            var existing = FindAnyObjectByType<PacificRimLoadingScreen>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var go = new GameObject("PacificRim_LoadingScreen");
            Instance = go.AddComponent<PacificRimLoadingScreen>();
            DontDestroyOnLoad(go);
            return Instance;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                if (canvas == null) BuildUI();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (!IsLoading && (canvasGroup == null || canvasGroup.alpha <= 0.001f)) return;

            // Smooth progress bar interpolation
            currentDisplayProgress = Mathf.MoveTowards(currentDisplayProgress, targetProgress, Time.unscaledDeltaTime * 1.5f);
            float clamped = Mathf.Clamp01(currentDisplayProgress);
            if (barFill != null)
            {
                barFill.anchorMax = new Vector2(clamped, 1f);
            }
            if (percentText != null)
            {
                percentText.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
            }
            if (statusText != null)
            {
                statusText.text = targetStatus;
            }

            // Smooth tactical reticle rotation
            if (reticleTransform != null)
            {
                reticleTransform.Rotate(0f, 0f, -38f * Time.unscaledDeltaTime);
            }

            // Cycling tactical tips
            tipTimer += Time.unscaledDeltaTime;
            if (tipTimer >= 3.6f)
            {
                tipTimer = 0f;
                currentTipIndex = (currentTipIndex + 1) % TacticalTips.Length;
                if (tipText != null)
                {
                    tipText.text = TacticalTips[currentTipIndex];
                }
            }
        }

        /// <summary>
        /// Pre-warms essential UI audio clips in the background on Main Menu startup without blocking the engine.
        /// </summary>
        public static void WarmupMainMenuAssets()
        {
            var loader = GetOrCreateInstance();
            loader.StartCoroutine(loader.WarmupMainMenuRoutine());
        }

        private IEnumerator WarmupMainMenuRoutine()
        {
            yield return null;
            // Preload audio clips in small batches to avoid any frame hitch
            AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].loadState != AudioDataLoadState.Loaded)
                {
                    clips[i].LoadAudioData();
                }
                if (i % 25 == 0) yield return null;
            }
            Debug.Log("<color=#00e5ff><b>[LoadingScreen]</b></color> Main Menu assets pre-warmed smoothly.");
        }

        /// <summary>
        /// Immediately displays the loading screen on this frame, then begins asynchronous loading.
        /// </summary>
        public static void LoadScene(string sceneName)
        {
            var loader = GetOrCreateInstance();
            loader.StartLoadingSequence(sceneName);
        }

        public void StartLoadingSequence(string sceneName)
        {
            StopAllCoroutines();
            if (sectorSubText != null)
            {
                if (sceneName.IndexOf("Anteverse", StringComparison.OrdinalIgnoreCase) >= 0)
                    sectorSubText.text = "SECTOR 09 // THE ANTEVERSE";
                else if (sceneName.IndexOf("Operation Pitfall", StringComparison.OrdinalIgnoreCase) >= 0)
                    sectorSubText.text = "SECTOR 09 // MARIANA TRENCH";
                else if (sceneName.IndexOf("Otachi", StringComparison.OrdinalIgnoreCase) >= 0)
                    sectorSubText.text = "SECTOR 05 // HONG KONG CITY CENTER";
                else if (sceneName.IndexOf("Hong Kong", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         sceneName.IndexOf("Neon", StringComparison.OrdinalIgnoreCase) >= 0)
                    sectorSubText.text = "SECTOR 05 // KOWLOON EXPRESSWAY";
                else if (sceneName.IndexOf("Save Striker Eureka", StringComparison.OrdinalIgnoreCase) >= 0)
                    sectorSubText.text = "SECTOR 04 // HONG KONG BAY";
                else
                    sectorSubText.text = "SHATTERDOME // COMMAND";
            }
            ShowImmediate("CONNECTING");
            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        public void ShowImmediate(string initialStatus)
        {
            IsLoading = true;
            targetProgress = 0.08f;
            currentDisplayProgress = 0.05f;
            targetStatus = initialStatus;

            // Make sure the GameObject is ALWAYS active so coroutines run reliably
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (canvas != null)
            {
                canvas.enabled = true;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }

            if (statusText != null) statusText.text = targetStatus;
            if (percentText != null) percentText.text = "5%";
            if (barFill != null) barFill.anchorMax = new Vector2(0.05f, 1f);

            Canvas.ForceUpdateCanvases();
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            // Give Unity 2 rendered frames to paint the loading screen onto the screen BEFORE loading starts!
            yield return null;
            yield return new WaitForEndOfFrame();

            targetStatus = $"LOADING  {sceneName.ToUpperInvariant()}";
            targetProgress = 0.15f;

            // Non-blocking asynchronous scene load with staged activation
            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
            asyncOp.allowSceneActivation = false;

            while (asyncOp.progress < 0.9f)
            {
                float opProgress = Mathf.Clamp01(asyncOp.progress / 0.9f);
                targetProgress = Mathf.Lerp(0.15f, 0.58f, opProgress);
                targetStatus = $"LOADING SECTOR";
                yield return null;
            }

            targetProgress = 0.60f;
            targetStatus = "INITIALIZING";
            asyncOp.allowSceneActivation = true;

            while (!asyncOp.isDone)
            {
                yield return null;
            }

            // Once the new scene is active, perform targeted preloading if it's a combat gameplay scene
            bool isGameplay = !sceneName.Equals("MainMenuScene", StringComparison.OrdinalIgnoreCase);
            if (isGameplay)
            {
                yield return StartCoroutine(PreloadMainSceneAssetsRoutine((progress, status) =>
                {
                    targetProgress = progress;
                    targetStatus = status;
                }));

                // Signal aerial deployment cutscene (Level 1), Otachi intro cutscene (Level 2), Operation Pitfall intro (Level 3), or underwater intro
                var cutscene = FindAnyObjectByType<PacificRimCutscene.PacificRimDeploymentCutscene>();
                var otachiIntro = FindAnyObjectByType<PacificRimCutscene.OtachiIntroCutscene>();
                var pitfallCutscene = FindAnyObjectByType<PacificRimCutscene.OperationPitfallIntroCutscene>();
                if (cutscene != null)
                {
                    cutscene.StartCutscene();
                }
                else if (otachiIntro == null && pitfallCutscene == null)
                {
                    var underwaterCtrl = FindAnyObjectByType<PacificRimAtmosphere.UnderwaterEnvironmentController>();
                    if (underwaterCtrl != null)
                    {
                        underwaterCtrl.StartUnderwaterIntro();
                    }
                }
            }
            else
            {
                targetProgress = 1.0f;
                targetStatus = "READY";
                yield return new WaitForSecondsRealtime(0.2f);
            }

            // Wait for display progress to smoothly catch up to 100%
            while (currentDisplayProgress < 0.98f)
            {
                yield return null;
            }
            currentDisplayProgress = 1.0f;
            targetStatus = "READY";
            yield return new WaitForSecondsRealtime(0.35f);

            // Smooth fade-out of the loading screen
            float fadeElapsed = 0f;
            while (fadeElapsed < 0.45f)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                if (canvasGroup != null) canvasGroup.alpha = 1f - Mathf.Clamp01(fadeElapsed / 0.45f);
                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            if (canvas != null) canvas.enabled = false;
            IsLoading = false;

            if (isGameplay)
            {
                var otachiIntro = FindAnyObjectByType<PacificRimCutscene.OtachiIntroCutscene>();
                if (otachiIntro != null)
                {
                    otachiIntro.StartCutscene();
                }

                var pitfallCutscene = FindAnyObjectByType<PacificRimCutscene.OperationPitfallIntroCutscene>();
                if (pitfallCutscene != null)
                {
                    pitfallCutscene.StartCutscene();
                }
            }
        }

        /// <summary>
        /// Targeted, non-blocking preloading of scene materials, audio clips, particle systems,
        /// and memory buffers to prevent any gameplay frame hitching.
        /// </summary>
        public static IEnumerator PreloadMainSceneAssetsRoutine(Action<float, string> onProgress = null)
        {
            // 1. Scene Material & Shader Touch (60% -> 72%)
            onProgress?.Invoke(0.72f, "WARMING SHADERS");
            yield return null;

            Renderer[] renderers = FindObjectsByType<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    var mats = renderers[i].sharedMaterials;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] != null && mats[m].shader != null)
                        {
                            string dummy = mats[m].shader.name;
                        }
                    }
                }
                if (i > 0 && i % 40 == 0) yield return null;
            }
            yield return null;

            // 2. Audio Buffers & Servo Samples (72% -> 82%)
            onProgress?.Invoke(0.82f, "LOADING AUDIO");
            yield return null;
            if (PacificRimAudio.PacificRimAudioManager.Instance != null)
            {
                PacificRimAudio.PacificRimAudioManager.Instance.PreloadAllAudioClips();
            }
            AudioSource[] audioSources = FindObjectsByType<AudioSource>();
            for (int i = 0; i < audioSources.Length; i++)
            {
                if (audioSources[i] != null && audioSources[i].clip != null && audioSources[i].clip.loadState != AudioDataLoadState.Loaded)
                {
                    audioSources[i].clip.LoadAudioData();
                }
                if (i > 0 && i % 15 == 0) yield return null;
            }
            yield return null;

            // 3. Particle Systems & VFX Buffers (82% -> 92%)
            onProgress?.Invoke(0.92f, "WARMING VFX");
            yield return null;
            ParticleSystem[] particleSystems = FindObjectsByType<ParticleSystem>();
            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (particleSystems[i] != null)
                {
                    particleSystems[i].Simulate(0.02f, true, true);
                    particleSystems[i].Clear(true);
                }
                if (i > 0 && i % 10 == 0) yield return null;
            }
            yield return null;

            // 4. Memory Purge, Physics Sync & GC (92% -> 98%)
            onProgress?.Invoke(0.98f, "SYNCING DRIFT");
            yield return null;
            Physics.SyncTransforms();
            Resources.UnloadUnusedAssets();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            yield return null;
            yield return new WaitForEndOfFrame();

            // 5. Ready (100%)
            onProgress?.Invoke(1.0f, "READY");
            yield return null;
        }

        private void BuildUI()
        {
            canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            Font font = PacificRimUITheme.GetDefaultFont();

            // 1. Dark Backdrop
            var bgGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.color = PacificRimUITheme.Background;
            bgImg.raycastTarget = true;

            // 2. Top and Bottom Ambient Vignettes
            var topVignette = new GameObject("TopVignette", typeof(RectTransform), typeof(UIGradientGraphic));
            topVignette.transform.SetParent(bgGo.transform, false);
            var topRt = topVignette.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 0.78f);
            topRt.anchorMax = Vector2.one;
            topRt.sizeDelta = Vector2.zero;
            var topGrad = topVignette.GetComponent<UIGradientGraphic>();
            topGrad.SetVertical(new Color(0.02f, 0.05f, 0.12f, 0.55f), Color.clear);
            topGrad.raycastTarget = false;

            var botVignette = new GameObject("BottomVignette", typeof(RectTransform), typeof(UIGradientGraphic));
            botVignette.transform.SetParent(bgGo.transform, false);
            var botRt = botVignette.GetComponent<RectTransform>();
            botRt.anchorMin = Vector2.zero;
            botRt.anchorMax = new Vector2(1f, 0.24f);
            botRt.sizeDelta = Vector2.zero;
            var botGrad = botVignette.GetComponent<UIGradientGraphic>();
            botGrad.SetVertical(Color.clear, new Color(0.02f, 0.05f, 0.12f, 0.65f));
            botGrad.raycastTarget = false;

            // 3. Slim Top Header Bar
            var headerGo = new GameObject("TopHeader", typeof(RectTransform));
            headerGo.transform.SetParent(transform, false);
            var headerRt = headerGo.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -34f);
            headerRt.sizeDelta = new Vector2(-140f, 34f);

            var topDivGo = new GameObject("HeaderLine", typeof(RectTransform), typeof(UIDividerGraphic));
            topDivGo.transform.SetParent(headerGo.transform, false);
            var topDivRt = topDivGo.GetComponent<RectTransform>();
            topDivRt.anchorMin = new Vector2(0f, 0f);
            topDivRt.anchorMax = new Vector2(1f, 0f);
            topDivRt.sizeDelta = new Vector2(0f, 1f);
            var topDiv = topDivGo.GetComponent<UIDividerGraphic>();
            topDiv.color = PacificRimUITheme.Line;
            topDiv.raycastTarget = false;

            var ppdcSubGo = new GameObject("PPDCSector", typeof(RectTransform), typeof(Text));
            ppdcSubGo.transform.SetParent(headerGo.transform, false);
            var ppdcSubRt = ppdcSubGo.GetComponent<RectTransform>();
            ppdcSubRt.anchorMin = new Vector2(0f, 0f);
            ppdcSubRt.anchorMax = new Vector2(0.75f, 1f);
            ppdcSubRt.sizeDelta = Vector2.zero;
            var ppdcSub = ppdcSubGo.GetComponent<Text>();
            ppdcSub.font = font;
            ppdcSub.fontSize = 12;
            ppdcSub.fontStyle = FontStyle.Bold;
            ppdcSub.color = PacificRimUITheme.BlueBright;
            ppdcSub.alignment = TextAnchor.MiddleLeft;
            ppdcSub.text = "SHATTERDOME // SECTOR";
            ppdcSub.raycastTarget = false;
            sectorSubText = ppdcSub;

            var ppdcTitleGo = new GameObject("PPDCTitle", typeof(RectTransform), typeof(Text));
            ppdcTitleGo.transform.SetParent(headerGo.transform, false);
            var ppdcTitleRt = ppdcTitleGo.GetComponent<RectTransform>();
            ppdcTitleRt.anchorMin = new Vector2(0.75f, 0f);
            ppdcTitleRt.anchorMax = new Vector2(1f, 1f);
            ppdcTitleRt.sizeDelta = Vector2.zero;
            var ppdcTitle = ppdcTitleGo.GetComponent<Text>();
            ppdcTitle.font = font;
            ppdcTitle.fontSize = 11;
            ppdcTitle.fontStyle = FontStyle.Bold;
            ppdcTitle.color = PacificRimUITheme.TextMuted;
            ppdcTitle.alignment = TextAnchor.MiddleRight;
            ppdcTitle.text = "MARK-III";
            ppdcTitle.raycastTarget = false;

            // 4. Center Hologram Blueprint Reticle
            var reticleGo = new GameObject("CenterReticle", typeof(RectTransform), typeof(UIHoloReticleGraphic));
            reticleGo.transform.SetParent(transform, false);
            reticleTransform = reticleGo.GetComponent<RectTransform>();
            reticleTransform.anchorMin = new Vector2(0.5f, 0.54f);
            reticleTransform.anchorMax = new Vector2(0.5f, 0.54f);
            reticleTransform.pivot = new Vector2(0.5f, 0.5f);
            reticleTransform.sizeDelta = new Vector2(184f, 184f);
            var holoRet = reticleGo.GetComponent<UIHoloReticleGraphic>();
            holoRet.color = new Color(PacificRimUITheme.BlueBright.r, PacificRimUITheme.BlueBright.g, PacificRimUITheme.BlueBright.b, 0.65f);
            holoRet.ringThickness = 1.6f;
            holoRet.segments = 56;
            holoRet.raycastTarget = false;

            var innerCircleGo = new GameObject("InnerCircle", typeof(RectTransform), typeof(UICircleOutlineGraphic));
            innerCircleGo.transform.SetParent(reticleGo.transform, false);
            var innerCircleRt = innerCircleGo.GetComponent<RectTransform>();
            innerCircleRt.anchorMin = new Vector2(0.5f, 0.5f);
            innerCircleRt.anchorMax = new Vector2(0.5f, 0.5f);
            innerCircleRt.sizeDelta = new Vector2(138f, 138f);
            var innerCircle = innerCircleGo.GetComponent<UICircleOutlineGraphic>();
            innerCircle.fillColor = new Color(0.04f, 0.08f, 0.16f, 0.30f);
            innerCircle.borderColor = new Color(PacificRimUITheme.Blue.r, PacificRimUITheme.Blue.g, PacificRimUITheme.Blue.b, 0.32f);
            innerCircle.thickness = 1f;
            innerCircle.raycastTarget = false;

            var jaegerLabelGo = new GameObject("CenterJaegerLabel", typeof(RectTransform), typeof(Text));
            jaegerLabelGo.transform.SetParent(transform, false);
            var jaegerLabelRt = jaegerLabelGo.GetComponent<RectTransform>();
            jaegerLabelRt.anchorMin = new Vector2(0.5f, 0.54f);
            jaegerLabelRt.anchorMax = new Vector2(0.5f, 0.54f);
            jaegerLabelRt.pivot = new Vector2(0.5f, 0.5f);
            jaegerLabelRt.sizeDelta = new Vector2(160f, 44f);
            var jaegerLabel = jaegerLabelGo.GetComponent<Text>();
            jaegerLabel.font = font;
            jaegerLabel.fontSize = 12;
            jaegerLabel.fontStyle = FontStyle.Bold;
            jaegerLabel.alignment = TextAnchor.MiddleCenter;
            jaegerLabel.color = PacificRimUITheme.Text;
            jaegerLabel.text = "GIPSY DANGER";
            jaegerLabel.raycastTarget = false;

            // 5. Bottom Loading Container
            var bottomContainer = new GameObject("BottomContainer", typeof(RectTransform));
            bottomContainer.transform.SetParent(transform, false);
            var botContainerRt = bottomContainer.GetComponent<RectTransform>();
            botContainerRt.anchorMin = new Vector2(0.5f, 0f);
            botContainerRt.anchorMax = new Vector2(0.5f, 0f);
            botContainerRt.pivot = new Vector2(0.5f, 0f);
            botContainerRt.anchoredPosition = new Vector2(0f, 64f);
            botContainerRt.sizeDelta = new Vector2(640f, 108f);

            // Status Text (Left)
            var statusGo = new GameObject("StatusText", typeof(RectTransform), typeof(Text));
            statusGo.transform.SetParent(bottomContainer.transform, false);
            var statusRt = statusGo.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0f, 1f);
            statusRt.anchorMax = new Vector2(0.75f, 1f);
            statusRt.pivot = new Vector2(0f, 1f);
            statusRt.anchoredPosition = new Vector2(0f, -6f);
            statusRt.sizeDelta = new Vector2(0f, 22f);
            statusText = statusGo.GetComponent<Text>();
            statusText.font = font;
            statusText.fontSize = 11;
            statusText.fontStyle = FontStyle.Bold;
            statusText.color = PacificRimUITheme.BlueBright;
            statusText.alignment = TextAnchor.MiddleLeft;
            statusText.text = targetStatus;
            statusText.raycastTarget = false;

            // Percent Text (Right)
            var percentGo = new GameObject("PercentText", typeof(RectTransform), typeof(Text));
            percentGo.transform.SetParent(bottomContainer.transform, false);
            var percentRt = percentGo.GetComponent<RectTransform>();
            percentRt.anchorMin = new Vector2(0.75f, 1f);
            percentRt.anchorMax = new Vector2(1f, 1f);
            percentRt.pivot = new Vector2(1f, 1f);
            percentRt.anchoredPosition = new Vector2(0f, -6f);
            percentRt.sizeDelta = new Vector2(0f, 22f);
            percentText = percentGo.GetComponent<Text>();
            percentText.font = font;
            percentText.fontSize = 14;
            percentText.fontStyle = FontStyle.Bold;
            percentText.color = PacificRimUITheme.Text;
            percentText.alignment = TextAnchor.MiddleRight;
            percentText.text = "0%";
            percentText.raycastTarget = false;

            // Sleek Progress Bar Track
            var barFrameGo = new GameObject("ProgressBarFrame", typeof(RectTransform), typeof(Image));
            barFrameGo.transform.SetParent(bottomContainer.transform, false);
            var barFrameRt = barFrameGo.GetComponent<RectTransform>();
            barFrameRt.anchorMin = new Vector2(0f, 1f);
            barFrameRt.anchorMax = new Vector2(1f, 1f);
            barFrameRt.pivot = new Vector2(0.5f, 1f);
            barFrameRt.anchoredPosition = new Vector2(0f, -34f);
            barFrameRt.sizeDelta = new Vector2(0f, 6f);
            var barTrack = barFrameGo.GetComponent<Image>();
            barTrack.color = PacificRimUITheme.BarTrack;
            barTrack.raycastTarget = false;

            // Progress Bar Fill (anchor-driven 0..1)
            var fillGo = new GameObject("ProgressBarFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barFrameGo.transform, false);
            barFill = fillGo.GetComponent<RectTransform>();
            barFill.anchorMin = new Vector2(0f, 0f);
            barFill.anchorMax = new Vector2(0f, 1f);
            barFill.offsetMin = Vector2.zero;
            barFill.offsetMax = Vector2.zero;
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = PacificRimUITheme.BlueBright;
            fillImg.raycastTarget = false;

            // Tactical Tip Display
            var tipGo = new GameObject("TacticalTip", typeof(RectTransform), typeof(Text));
            tipGo.transform.SetParent(bottomContainer.transform, false);
            var tipRt = tipGo.GetComponent<RectTransform>();
            tipRt.anchorMin = new Vector2(0f, 0f);
            tipRt.anchorMax = new Vector2(1f, 0f);
            tipRt.pivot = new Vector2(0.5f, 0f);
            tipRt.anchoredPosition = new Vector2(0f, 8f);
            tipRt.sizeDelta = new Vector2(0f, 36f);
            tipText = tipGo.GetComponent<Text>();
            tipText.font = font;
            tipText.fontSize = 11;
            tipText.color = PacificRimUITheme.TextDim;
            tipText.alignment = TextAnchor.MiddleCenter;
            tipText.text = TacticalTips[0];
            tipText.raycastTarget = false;

            // Default state: canvas disabled so it doesn't render until LoadScene or ShowImmediate is called,
            // while the GameObject stays active to safely run coroutines.
            canvas.enabled = false;
        }
    }
}
