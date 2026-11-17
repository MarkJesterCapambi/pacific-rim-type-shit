using UnityEngine;
using UnityEngine.UI;
using PacificRimWater;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PacificRimUI
{
    public class JaegerHUD : MonoBehaviour
    {
        public static JaegerHUD Instance { get; private set; }
        public static bool IsHudHidden { get; private set; } = false;

        [Header("Jaeger Reference")]
        [SerializeField] private SimpleJaegerController jaeger;

        [Header("Top Header Telemetry")]
        [SerializeField] private Text chassisLabelText;
        [SerializeField] private Text linkStatusText;
        [SerializeField] private Text combatTimerText;
        [SerializeField] private Text bestTimeText;
        [SerializeField] private GameObject timerContainer;

        public float CurrentCombatTime => combatElapsedTime;
        private float combatElapsedTime = 0f;
        private bool isCombatTimerRunning = false;
        private bool isCombatFinished = false;

        [Header("Hull & Overcharge Panel (Bottom-Left)")]
        [SerializeField] private Text hullCaptionText;
        [SerializeField] private Text hullNumberText;
        [SerializeField] private PacificRimBar hullBar;
        [SerializeField] private Text staminaCaptionText;
        [SerializeField] private Text staminaValueText;
        [SerializeField] private PacificRimBar staminaBar;
        [SerializeField] private GameObject overchargeContainer;
        [SerializeField] private PacificRimBar overchargeBar;
        [SerializeField] private Text overchargeStatusText;
        [SerializeField] private Text overchargeTimerText;

        [Header("Combat Matrix Panel (Bottom-Right)")]
        [SerializeField] private Text combatStanceValueText;
        [SerializeField] private Text defenseMatrixValueText;
        [SerializeField] private Text depthTelemetryText;

        [Header("Reticle & Subtle Overcharge Screen Cue")]
        [SerializeField] private UIHoloReticleGraphic holoReticle;
        [SerializeField] private CanvasGroup overchargeScreenCueGroup;

        [Header("Screen Impact & Block Flash Overlay")]
        [SerializeField] private CanvasGroup impactFlashGroup;
        [SerializeField] private UIGradientGraphic[] impactEdges;

        [Header("Footer Key Prompts")]
        [SerializeField] private PacificRimKeyPrompt stancePrompt;
        [SerializeField] private PacificRimKeyPrompt lightAttackPrompt;
        [SerializeField] private PacificRimKeyPrompt heavyAttackPrompt;
        [SerializeField] private PacificRimKeyPrompt defensePrompt;
        [SerializeField] private PacificRimKeyPrompt dashPrompt;
        [SerializeField] private PacificRimKeyPrompt bladesPrompt;
        [SerializeField] private PacificRimKeyPrompt overchargePrompt;
        [SerializeField] private PacificRimKeyPrompt plasmaPrompt;
        [SerializeField] private PacificRimKeyPrompt camPrompt;
        [SerializeField] private PacificRimKeyPrompt menuPrompt;

        [Header("Notification Banner (Top-Center)")]
        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private Text bannerText;

        [Header("Boss Health Bar (Top-Center)")]
        [SerializeField] private KaijuAI kaiju;
        [SerializeField] private GameObject bossPanelRoot;
        [SerializeField] private Text bossNameText;
        [SerializeField] private Text bossPhaseText;
        [SerializeField] private Text bossStatusText;
        [SerializeField] private Text bossHpNumberText;
        [SerializeField] private PacificRimBar bossHealthBar;
        [SerializeField] private PacificRimBar bossSubBar;

        private float powerUpTimer;
        private float bannerTimer;
        private float bannerAlpha;
        private bool wasPoweredUp;
        private bool wasInStance;
        private bool wasBlocking;
        private bool wasKaijuEmpBuffed;
        private bool wasKaijuDead;
        private bool wasJaegerDead;
        private int wasKaijuPhase = 1;
        private float impactFlashAlpha;
        private float impactFlashDecaySpeed = 2.8f;
        private float lastExhaustedBannerTime = -10f;
        private bool lastBladesActiveCached;
        private bool lastSurgeActiveCached;
        private int lastSurgeTenthsCached = -1;
        private bool lastCamFreeCached;
        private bool lastDashActiveCached;
        private KeyCode lastPlasmaKeyCodeCached = KeyCode.None;
        private string lastPlasmaKeyCached;
        private string lastPlasmaLabelCached;
        private bool lastPlasmaHighlightCached;
        private Color lastPlasmaColorCached;
        private bool stateInitialized;
        private CanvasGroup hudCanvasGroup;
        private bool isCutsceneMode;
        private Coroutine neuralBootRoutine;

        // Admin UI toggle buffer ("hesoyam")
        private static int suppressFreeCamFrame = -1;
        public static bool SuppressFreeCamThisFrame => suppressFreeCamFrame == Time.frameCount;

        private string cheatInputBuffer = "";
        private int lastCheatCharFrame = -1;
        private char lastCheatChar = '\0';

        private static readonly Color StaminaCyan = new Color(0.28f, 0.88f, 0.96f, 1f);

        private void Awake()
        {
            Instance = this;
            EnsureCanvasGroup();
            ResolveReferences();
        }

        private void OnEnable()
        {
            Instance = this;
            stateInitialized = false;
            EnsureCanvasGroup();
            ApplyHudVisibility();
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                Keyboard.current.onTextInput += OnTextInputReceived;
            }
#endif
            ResolveReferences();
            SyncImmediate();
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                Keyboard.current.onTextInput -= OnTextInputReceived;
            }
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void EnsureCanvasGroup()
        {
            if (hudCanvasGroup == null)
            {
                hudCanvasGroup = GetComponent<CanvasGroup>();
                if (hudCanvasGroup == null)
                {
                    hudCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        public void SetHudHidden(bool hidden)
        {
            IsHudHidden = hidden;
            ApplyHudVisibility();
        }

        private void ApplyHudVisibility()
        {
            if (isCutsceneMode) return;
            EnsureCanvasGroup();
            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = IsHudHidden ? 0f : 1f;
                hudCanvasGroup.blocksRaycasts = !IsHudHidden;
                hudCanvasGroup.interactable = !IsHudHidden;
            }
        }

        public void SetCutsceneHidden(bool hidden)
        {
            isCutsceneMode = hidden;
            EnsureCanvasGroup();
            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = hidden ? 0f : 1f;
                hudCanvasGroup.blocksRaycasts = !hidden;
                hudCanvasGroup.interactable = !hidden;
            }
        }

        public void PlayNeuralBootSequence(System.Action onComplete = null)
        {
            if (neuralBootRoutine != null) StopCoroutine(neuralBootRoutine);
            neuralBootRoutine = StartCoroutine(NeuralBootRoutine(onComplete));
        }

        private System.Collections.IEnumerator NeuralBootRoutine(System.Action onComplete)
        {
            EnsureCanvasGroup();
            isCutsceneMode = false;

            // Tactical boot-up flicker sequence: 0 -> 0.7 -> 0.15 -> 0.9 -> 0.35 -> 1.0
            float[] flickerKeyframes = new float[] { 0.65f, 0.12f, 0.85f, 0.30f, 0.95f, 0.45f, 1.0f };
            float[] flickerDelays = new float[]    { 0.08f, 0.06f, 0.10f, 0.07f, 0.14f, 0.09f, 0.18f };

            for (int i = 0; i < flickerKeyframes.Length; i++)
            {
                if (hudCanvasGroup != null) hudCanvasGroup.alpha = flickerKeyframes[i];
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayUiClick();
                yield return new WaitForSeconds(flickerDelays[i]);
            }

            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = 1f;
                hudCanvasGroup.blocksRaycasts = true;
                hudCanvasGroup.interactable = true;
            }

            ShowBanner("<color=#46c3ff><b>DRIFT SYNC 100%  ·  SYSTEMS ONLINE</b></color>", 2.2f);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHUDStaminaRecovered35();
            PacificRimUISound.PlaySuccess();

            onComplete?.Invoke();
            neuralBootRoutine = null;
        }

        private static bool WasTildePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
                return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.BackQuote))
                    return true;
            }
            catch
            {
            }
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        private void OnTextInputReceived(char c)
        {
            if (MainMenuUI.IsMenuOpen) return;
            AppendCheatChar(c);
        }
#endif

        private void UpdateCheatCodeInput()
        {
            if (MainMenuUI.IsMenuOpen) return;

            // 1. Process legacy Input.inputString if available
            try
            {
                string inputStr = Input.inputString;
                if (!string.IsNullOrEmpty(inputStr))
                {
                    for (int i = 0; i < inputStr.Length; i++)
                    {
                        char c = inputStr[i];
                        if (char.IsLetter(c))
                        {
                            AppendCheatChar(c);
                        }
                    }
                }
            }
            catch
            {
                // Legacy input disabled
            }

            // 2. Poll New Input System keyboard keys
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.bKey.wasPressedThisFrame) AppendCheatChar('b');
                else if (kb.aKey.wasPressedThisFrame) AppendCheatChar('a');
                else if (kb.gKey.wasPressedThisFrame) AppendCheatChar('g');
                else if (kb.uKey.wasPressedThisFrame) AppendCheatChar('u');
                else if (kb.vKey.wasPressedThisFrame) AppendCheatChar('v');
                else if (kb.iKey.wasPressedThisFrame) AppendCheatChar('i');
                else if (kb.xKey.wasPressedThisFrame) AppendCheatChar('x');
                else if (kb.cKey.wasPressedThisFrame) AppendCheatChar('c');
                else if (kb.dKey.wasPressedThisFrame) AppendCheatChar('d');
                else if (kb.eKey.wasPressedThisFrame) AppendCheatChar('e');
                else if (kb.fKey.wasPressedThisFrame) AppendCheatChar('f');
                else if (kb.hKey.wasPressedThisFrame) AppendCheatChar('h');
                else if (kb.jKey.wasPressedThisFrame) AppendCheatChar('j');
                else if (kb.kKey.wasPressedThisFrame) AppendCheatChar('k');
                else if (kb.lKey.wasPressedThisFrame) AppendCheatChar('l');
                else if (kb.mKey.wasPressedThisFrame) AppendCheatChar('m');
                else if (kb.nKey.wasPressedThisFrame) AppendCheatChar('n');
                else if (kb.oKey.wasPressedThisFrame) AppendCheatChar('o');
                else if (kb.pKey.wasPressedThisFrame) AppendCheatChar('p');
                else if (kb.qKey.wasPressedThisFrame) AppendCheatChar('q');
                else if (kb.rKey.wasPressedThisFrame) AppendCheatChar('r');
                else if (kb.sKey.wasPressedThisFrame) AppendCheatChar('s');
                else if (kb.tKey.wasPressedThisFrame) AppendCheatChar('t');
                else if (kb.wKey.wasPressedThisFrame) AppendCheatChar('w');
                else if (kb.yKey.wasPressedThisFrame) AppendCheatChar('y');
                else if (kb.zKey.wasPressedThisFrame) AppendCheatChar('z');
            }
#endif
        }

        private void AppendCheatChar(char c)
        {
            char lower = char.ToLowerInvariant(c);
            if (!char.IsLetter(lower)) return;

            // Deduplicate if multiple input providers fired on the same frame
            if (Time.frameCount == lastCheatCharFrame && lastCheatChar == lower)
            {
                return;
            }
            lastCheatCharFrame = Time.frameCount;
            lastCheatChar = lower;

            cheatInputBuffer += lower;
            if (cheatInputBuffer.Length > 24)
            {
                cheatInputBuffer = cheatInputBuffer.Substring(cheatInputBuffer.Length - 24);
            }

            if (cheatInputBuffer.EndsWith("he", System.StringComparison.OrdinalIgnoreCase))
            {
                suppressFreeCamFrame = Time.frameCount;
            }

            if (cheatInputBuffer.EndsWith("hesoyam", System.StringComparison.OrdinalIgnoreCase))
            {
                cheatInputBuffer = "";
                TriggerAdminUIToggle();
            }
        }

        public void TriggerAdminUIToggle()
        {
            PacificRimAdminUI.Toggle();
            PacificRimUISound.Play(PacificRimAdminUI.IsOpen
                ? PacificRimUISound.SoundType.Click
                : PacificRimUISound.SoundType.Back);
        }

        private void ResolveReferences()
        {
            if (jaeger == null)
                jaeger = FindAnyObjectByType<SimpleJaegerController>();
            if (kaiju == null)
                kaiju = FindAnyObjectByType<KaijuAI>();
        }

        private void Update()
        {
            // In-game HUD toggle via Tilde key (~)
            if (!MainMenuUI.IsMenuOpen && WasTildePressedThisFrame())
            {
                SetHudHidden(!IsHudHidden);
            }

            // Admin console key sequence detection ("hesoyam")
            UpdateCheatCodeInput();

            ResolveReferences();
            if (jaeger == null) return;

            if (!stateInitialized)
            {
                SyncImmediate();
                return;
            }

            UpdateHullAndOvercharge();
            UpdateStamina();
            UpdateCombatAndTelemetry();
            UpdateBossHealthBar();
            UpdateCombatTimer();
            UpdateScreenImpact();
            UpdateBanner();
        }

        private void SyncImmediate()
        {
            if (jaeger == null) return;
            wasPoweredUp = jaeger.IsPoweredUp;
            wasInStance = jaeger.inCombatStance;
            wasBlocking = jaeger.isBlocking;
            powerUpTimer = jaeger.IsPoweredUp ? Mathf.Max(0.01f, jaeger.powerUpDuration) : 0f;
            wasKaijuEmpBuffed = kaiju != null && kaiju.IsEmpBuffed;
            wasKaijuDead = kaiju != null && kaiju.IsDead;
            wasJaegerDead = jaeger != null && jaeger.IsDead;
            wasKaijuPhase = kaiju != null ? kaiju.CurrentPhase : 1;
            impactFlashAlpha = 0f;
            if (impactFlashGroup != null)
                impactFlashGroup.alpha = 0f;
            stateInitialized = true;

            if (hullBar != null && jaeger.maxHealth > 0f)
                hullBar.SetRatioImmediate(jaeger.currentHealth / jaeger.maxHealth);

            if (staminaBar != null)
                staminaBar.SetRatioImmediate(jaeger.StaminaRatio);

            if (kaiju != null && bossHealthBar != null && kaiju.maxHealth > 0f)
                bossHealthBar.SetRatioImmediate(kaiju.currentHealth / kaiju.maxHealth);

            UpdateHullAndOvercharge();
            UpdateStamina();
            UpdateCombatAndTelemetry();
            UpdateBossHealthBar();
        }

        public void ShowBanner(string message, float duration = 2.5f)
        {
            if (bannerText != null)
            {
                bannerText.supportRichText = true;
                bannerText.text = PacificRimUITheme.Track(message, 2, Mathf.Max(12, bannerText.fontSize), true);
            }
            bannerTimer = Mathf.Max(0.25f, duration);
        }

        private void UpdateHullAndOvercharge()
        {
            float maxHp = Mathf.Max(1f, jaeger.maxHealth);
            float curHp = Mathf.Clamp(jaeger.currentHealth, 0f, maxHp);
            float ratio = curHp / maxHp;

            if (hullNumberText != null)
            {
                hullNumberText.text = PacificRimUITheme.FormatNumber(Mathf.CeilToInt(curHp));
                hullNumberText.color = ratio < 0.25f
                    ? PacificRimUITheme.Danger
                    : (ratio < 0.50f ? PacificRimUITheme.Amber : PacificRimUITheme.Health);
            }

            if (hullBar != null)
            {
                hullBar.SetRatio(ratio);
                if (hullBar.fillImage != null)
                {
                    hullBar.fillImage.color = ratio < 0.25f
                        ? PacificRimUITheme.Danger
                        : (ratio < 0.50f ? PacificRimUITheme.Amber : PacificRimUITheme.Blue);
                }
            }

            if (hullCaptionText != null)
            {
                hullCaptionText.text = jaeger.IsDead ? "HULL DESTROYED" : "HULL";
                hullCaptionText.color = jaeger.IsDead ? PacificRimUITheme.Danger : PacificRimUITheme.TextDim;
            }

            if (jaeger.IsDead && !wasJaegerDead)
            {
                PacificRimUISound.PlayDeny();
                isCombatFinished = true;
                isCombatTimerRunning = false;
                ShowBanner("CRITICAL HULL BREACH  ·  JAEGER LOST", 4.0f);
            }
            wasJaegerDead = jaeger.IsDead;

            // Overcharge transition & countdown
            bool powered = jaeger.IsPoweredUp;
            if (powered && !wasPoweredUp)
            {
                powerUpTimer = Mathf.Max(0.1f, jaeger.powerUpDuration);
                PacificRimUISound.PlaySuccess();
                ShowBanner("TURBINE OVERCHARGE", 2.0f);
            }
            wasPoweredUp = powered;

            if (powered)
            {
                powerUpTimer = Mathf.Max(0f, powerUpTimer - Time.deltaTime);
                float duration = Mathf.Max(0.1f, jaeger.powerUpDuration);
                float powerRatio = Mathf.Clamp01(powerUpTimer / duration);

                if (overchargeContainer != null && !overchargeContainer.activeSelf)
                    overchargeContainer.SetActive(true);

                if (overchargeBar != null)
                {
                    overchargeBar.SetRatio(powerRatio);
                    if (overchargeBar.fillImage != null)
                        overchargeBar.fillImage.color = PacificRimUITheme.BlueBright;
                }

                if (overchargeStatusText != null)
                {
                    overchargeStatusText.supportRichText = true;
                    overchargeStatusText.text = "OVERCHARGE";
                    overchargeStatusText.color = PacificRimUITheme.BlueBright;
                }

                if (overchargeTimerText != null)
                {
                    overchargeTimerText.text = $"{powerUpTimer:0.0}S";
                    overchargeTimerText.color = PacificRimUITheme.Text;
                }
            }
            else
            {
                if (overchargeContainer != null && overchargeContainer.activeSelf)
                    overchargeContainer.SetActive(false);

                if (overchargeBar != null)
                    overchargeBar.SetRatio(0f);
            }

            // Subtle non-obstructive peripheral screen cue while Overcharged
            if (overchargeScreenCueGroup != null)
            {
                float targetAlpha = powered ? (0.14f + 0.06f * Mathf.Sin(Time.time * 4.5f)) : 0f;
                overchargeScreenCueGroup.alpha = Mathf.Lerp(overchargeScreenCueGroup.alpha, targetAlpha, Time.deltaTime * 10f);
            }
        }

        private void UpdateCombatAndTelemetry()
        {
            bool inStance = jaeger.inCombatStance;
            if (inStance != wasInStance)
            {
                wasInStance = inStance;
                PacificRimUISound.Play(inStance ? PacificRimUISound.SoundType.Tab : PacificRimUISound.SoundType.Back);
            }

            bool blocking = jaeger.isBlocking;
            if (blocking && !wasBlocking)
            {
                PacificRimUISound.PlayClick();
            }
            wasBlocking = blocking;

            bool bladesActive = jaeger.AreChainswordsActive;
            if (combatStanceValueText != null)
            {
                combatStanceValueText.supportRichText = true;
                combatStanceValueText.text = bladesActive ? "DEPLOYED" : "STOWED";
                combatStanceValueText.color = bladesActive ? PacificRimUITheme.BlueBright : PacificRimUITheme.TextMuted;
            }

            if (defenseMatrixValueText != null)
            {
                defenseMatrixValueText.supportRichText = true;
                if (jaeger.IsExhausted)
                {
                    defenseMatrixValueText.text = "LOCKED";
                    defenseMatrixValueText.color = PacificRimUITheme.Danger;
                }
                else
                {
                    defenseMatrixValueText.text = blocking ? "BLOCKING" : "READY";
                    defenseMatrixValueText.color = blocking ? PacificRimUITheme.BlueBright : PacificRimUITheme.TextDim;
                }
            }

            bool isFreeCam = JaegerCameraFollow.Instance != null && JaegerCameraFollow.Instance.isFreeCam;
            if (depthTelemetryText != null)
            {
                depthTelemetryText.supportRichText = true;
                if (isFreeCam)
                {
                    depthTelemetryText.text = "FREECAM";
                    depthTelemetryText.color = PacificRimUITheme.Amber;
                }
                else if (WaterSystem.Instance != null)
                {
                    float waterY = WaterSystem.Instance.GetWaterHeight(jaeger.transform.position);
                    float depth = Mathf.Max(0f, waterY - jaeger.transform.position.y);
                    depthTelemetryText.text = $"{depth:0}M";
                    depthTelemetryText.color = PacificRimUITheme.TextDim;
                }
                else
                {
                    depthTelemetryText.text = "10,890M";
                    depthTelemetryText.color = PacificRimUITheme.TextDim;
                }
            }

            // Update tactical footer prompts
            if (stancePrompt != null && stancePrompt.gameObject.activeSelf)
                stancePrompt.gameObject.SetActive(false);
            if (lightAttackPrompt != null)
                lightAttackPrompt.SetHighlight(false, PacificRimUITheme.BlueBright);
            if (heavyAttackPrompt != null)
                heavyAttackPrompt.SetHighlight(false, PacificRimUITheme.BlueBright);
            if (defensePrompt != null)
                defensePrompt.SetHighlight(blocking, PacificRimUITheme.BlueBright);

            bool isDashing = jaeger.IsDashing;
            if (dashPrompt != null && isDashing != lastDashActiveCached)
            {
                lastDashActiveCached = isDashing;
                dashPrompt.SetHighlight(isDashing, PacificRimUITheme.BlueBright);
            }

            if (bladesPrompt != null && bladesActive != lastBladesActiveCached)
            {
                lastBladesActiveCached = bladesActive;
                bladesPrompt.SetHighlight(bladesActive, PacificRimUITheme.BlueBright);
            }

            bool powered = jaeger.IsPoweredUp;
            if (overchargePrompt != null)
            {
                int tenths = powered ? Mathf.CeilToInt(powerUpTimer * 10f) : -1;
                if (powered != lastSurgeActiveCached || tenths != lastSurgeTenthsCached)
                {
                    lastSurgeActiveCached = powered;
                    lastSurgeTenthsCached = tenths;
                    if (powered)
                    {
                        overchargePrompt.Setup("Q", $"{tenths / 10}.{tenths % 10}S");
                        overchargePrompt.SetHighlight(true, PacificRimUITheme.Amber);
                    }
                    else
                    {
                        overchargePrompt.Setup("Q", "SURGE");
                        overchargePrompt.SetHighlight(false, PacificRimUITheme.Amber);
                    }
                }
            }

            if (camPrompt != null && isFreeCam != lastCamFreeCached)
            {
                lastCamFreeCached = isFreeCam;
                camPrompt.SetHighlight(isFreeCam, PacificRimUITheme.Amber);
            }

            UpdatePlasmaPrompt();

            // Reticle state tint
            if (holoReticle != null)
            {
                Color targetColor;
                if (jaeger.IsPlasmaCharging)
                    targetColor = new Color(0.40f, 0.98f, 1f, 0.95f);
                else if (blocking)
                    targetColor = new Color(PacificRimUITheme.BlueBright.r, PacificRimUITheme.BlueBright.g, PacificRimUITheme.BlueBright.b, 0.85f);
                else if (inStance)
                    targetColor = jaeger.IsPoweredUp
                        ? new Color(PacificRimUITheme.BlueBright.r, PacificRimUITheme.BlueBright.g, 1f, 0.78f)
                        : new Color(PacificRimUITheme.Blue.r, PacificRimUITheme.Blue.g, PacificRimUITheme.Blue.b, 0.65f);
                else
                    targetColor = new Color(PacificRimUITheme.TextDim.r, PacificRimUITheme.TextDim.g, PacificRimUITheme.TextDim.b, 0.28f);

                holoReticle.color = Color.Lerp(holoReticle.color, targetColor, Time.deltaTime * 12f);
            }
        }

        private void EnsurePlasmaPromptUI()
        {
            if (plasmaPrompt != null || overchargePrompt == null) return;

            Transform parent = overchargePrompt.transform.parent;
            if (parent == null) return;

            Transform existing = parent.Find("Prompt_Plasma");
            if (existing != null)
            {
                plasmaPrompt = existing.GetComponent<PacificRimKeyPrompt>();
                if (plasmaPrompt != null) return;
            }

            GameObject clone = Instantiate(overchargePrompt.gameObject, parent, false);
            clone.name = "Prompt_Plasma";
            if (menuPrompt != null && menuPrompt.transform.parent == parent)
            {
                clone.transform.SetSiblingIndex(menuPrompt.transform.GetSiblingIndex());
            }

            var le = clone.GetComponent<LayoutElement>();
            if (le != null) le.preferredWidth = 178f;

            plasmaPrompt = clone.GetComponent<PacificRimKeyPrompt>();
            if (plasmaPrompt != null)
            {
                plasmaPrompt.Setup("T", "PLASMA READY");
            }
        }

        private void UpdatePlasmaPrompt()
        {
            EnsurePlasmaPromptUI();
            if (plasmaPrompt == null || jaeger == null) return;

            bool keyChanged = jaeger.plasmaCasterKey != lastPlasmaKeyCodeCached || string.IsNullOrEmpty(lastPlasmaKeyCached);
            if (keyChanged)
            {
                lastPlasmaKeyCodeCached = jaeger.plasmaCasterKey;
                lastPlasmaKeyCached = lastPlasmaKeyCodeCached.ToString().ToUpperInvariant();
            }
            string keyStr = lastPlasmaKeyCached;

            string label;
            bool highlight;
            Color accent;

            if (jaeger.IsPlasmaCharging)
            {
                label = "CHARGING";
                highlight = true;
                accent = PacificRimUITheme.BlueBright;
            }
            else if (jaeger.IsPlasmaAiming)
            {
                label = "FIRED";
                highlight = true;
                accent = PacificRimUITheme.Amber;
            }
            else if (jaeger.PlasmaCooldownRemaining > 0.05f)
            {
                int tenths = Mathf.CeilToInt(jaeger.PlasmaCooldownRemaining * 10f);
                label = $"{tenths / 10}.{tenths % 10}S";
                highlight = false;
                accent = PacificRimUITheme.Amber;
            }
            else if (!jaeger.HasEnoughStaminaForPlasma)
            {
                label = "<70% STM";
                highlight = false;
                accent = PacificRimUITheme.Danger;
            }
            else
            {
                label = "PLASMA";
                highlight = true;
                accent = PacificRimUITheme.BlueBright;
            }

            if (keyChanged || label != lastPlasmaLabelCached)
            {
                lastPlasmaLabelCached = label;
                plasmaPrompt.Setup(keyStr, label);
                plasmaPrompt.SetHighlight(highlight, accent);
                lastPlasmaHighlightCached = highlight;
                lastPlasmaColorCached = accent;
            }
            else if (highlight != lastPlasmaHighlightCached || accent != lastPlasmaColorCached)
            {
                lastPlasmaHighlightCached = highlight;
                lastPlasmaColorCached = accent;
                plasmaPrompt.SetHighlight(highlight, accent);
            }
        }

        private void UpdateBossHealthBar()
        {
            if (bossPanelRoot == null) return;

            bool hasKaiju = kaiju != null;
            if (bossPanelRoot.activeSelf != hasKaiju)
                bossPanelRoot.SetActive(hasKaiju);

            if (!hasKaiju) return;

            float maxHp = Mathf.Max(1f, kaiju.maxHealth);
            float curHp = Mathf.Clamp(kaiju.currentHealth, 0f, maxHp);
            float hpRatio = curHp / maxHp;

            if (bossNameText != null)
            {
                bossNameText.supportRichText = true;
                string catShort = string.IsNullOrEmpty(kaiju.kaijuCategoryLabel)
                    ? string.Empty
                    : kaiju.kaijuCategoryLabel.Replace("CATEGORY ", "CAT ").Replace(" KAIJU", string.Empty).Trim();
                bossNameText.text = string.IsNullOrEmpty(catShort)
                    ? kaiju.kaijuDisplayName
                    : $"{kaiju.kaijuDisplayName}  <color=#8C96A6><size=10>{catShort}</size></color>";
            }

            if (bossHpNumberText != null)
            {
                bossHpNumberText.text = $"{PacificRimUITheme.FormatNumber(Mathf.CeilToInt(curHp))} / {PacificRimUITheme.FormatNumber(Mathf.CeilToInt(maxHp))}";
                bossHpNumberText.color = kaiju.IsDead ? PacificRimUITheme.TextDim : PacificRimUITheme.Text;
            }

            if (bossPhaseText != null)
            {
                bossPhaseText.text = FormatConcisePhase(kaiju);
                bossPhaseText.color = kaiju.CurrentPhase >= 3
                    ? PacificRimUITheme.Danger
                    : (kaiju.CurrentPhase == 2 ? PacificRimUITheme.Amber : PacificRimUITheme.BlueBright);
            }

            if (bossHealthBar != null)
            {
                bossHealthBar.SetRatio(hpRatio);
                if (bossHealthBar.fillImage != null)
                {
                    if (kaiju.IsEmpBuffed)
                        bossHealthBar.fillImage.color = PacificRimUITheme.BlueBright;
                    else if (kaiju.IsBlocking || kaiju.IsStaggered)
                        bossHealthBar.fillImage.color = PacificRimUITheme.Amber;
                    else
                        bossHealthBar.fillImage.color = PacificRimUITheme.Danger;
                }
            }

            if (bossStatusText != null)
            {
                bossStatusText.supportRichText = true;
                bossStatusText.text = FormatConciseBossStatus(kaiju);
                if (kaiju.IsDead)
                    bossStatusText.color = PacificRimUITheme.Success;
                else if (kaiju.IsEmpBuffed)
                    bossStatusText.color = PacificRimUITheme.BlueBright;
                else if (kaiju.IsBlocking || kaiju.IsStaggered)
                    bossStatusText.color = PacificRimUITheme.Amber;
                else
                    bossStatusText.color = PacificRimUITheme.Danger;
            }

            if (bossSubBar != null)
            {
                float subRatio = kaiju.IsEmpBuffed && kaiju.EmpBuffDuration > 0.01f
                    ? Mathf.Clamp01(kaiju.EmpBuffRemaining / kaiju.EmpBuffDuration)
                    : kaiju.PoiseRatio;
                bossSubBar.SetRatio(kaiju.IsDead ? 0f : subRatio);
                if (bossSubBar.fillImage != null)
                {
                    bossSubBar.fillImage.color = kaiju.IsEmpBuffed
                        ? PacificRimUITheme.BlueBright
                        : PacificRimUITheme.Amber;
                }
            }

            if (kaiju.CurrentPhase > wasKaijuPhase)
            {
                string overdriveName = kaiju.isSlattern ? "ABYSSAL OVERDRIVE" : "EMP OVERDRIVE";
                if (kaiju.CurrentPhase == 2)
                    ShowBanner($"{kaiju.kaijuDisplayName}  ·  PHASE II {overdriveName}", 3.0f);
                else if (kaiju.CurrentPhase >= 3)
                    ShowBanner($"{kaiju.kaijuDisplayName}  ·  PHASE III BERSERK", 3.2f);
            }
            wasKaijuPhase = kaiju.CurrentPhase;

            if (kaiju.IsEmpBuffed && !wasKaijuEmpBuffed)
            {
                string surgeLabel = kaiju.isSlattern ? "ABYSSAL SURGE" : "EMP SURGE";
                ShowBanner($"{kaiju.kaijuDisplayName}  ·  {surgeLabel}", 2.5f);
            }
            wasKaijuEmpBuffed = kaiju.IsEmpBuffed;

            if (kaiju.IsDead && !wasKaijuDead)
            {
                PacificRimUISound.PlaySuccess();
                isCombatFinished = true;
                isCombatTimerRunning = false;

                bool isNewBest = false;
                if (combatElapsedTime > 0.5f)
                {
                    PacificRimCombatRecords.TryRecordVictoryTime(combatElapsedTime, out isNewBest);
                }

                if (isNewBest)
                {
                    ShowBanner($"NEW BEST  ·  {PacificRimCombatRecords.FormatTime(combatElapsedTime)}", 5.0f);
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlayHUDCheatActivated();
                }
                else
                {
                    ShowBanner($"{kaiju.kaijuDisplayName} NEUTRALIZED  ·  {PacificRimCombatRecords.FormatTime(combatElapsedTime)}", 4.5f);
                }
            }
            wasKaijuDead = kaiju.IsDead;
        }

        private static string FormatConcisePhase(KaijuAI k)
        {
            if (k.IsDead) return "NEUTRALIZED";
            if (k.IsAIDisabled) return "AI OFF";
            if (k.IsBaguvixActive) return "PACIFIST";
            if (k.isSlattern)
            {
                if (k.CurrentPhase == 3) return "PHASE III · JUGGERNAUT";
                if (k.CurrentPhase == 2) return "PHASE II · BLITZER";
                return "PHASE I · STALKER";
            }
            if (k.CurrentPhase == 3) return "PHASE III · BERSERK";
            if (k.CurrentPhase == 2) return "PHASE II · OVERDRIVE";
            return "PHASE I";
        }

        private static string FormatConciseBossStatus(KaijuAI k)
        {
            if (k.IsDead) return "ELIMINATED";
            if (k.IsAIDisabled) return "OFFLINE";
            if (k.IsBaguvixActive) return "PASSIVE";
            if (k.currentState == KaijuAI.AIState.PhaseTransition) return $"PHASE {k.CurrentPhase}";
            if (k.IsStaggered) return "STAGGERED";
            if (k.IsMovingGuardActive) return "MOVING GUARD · HEAVY BREAKS";
            if (k.IsBlocking) return "GUARDING";
            if (k.IsEmpBuffed) return k.isSlattern ? "ABYSSAL SURGE" : "EMP SURGE";
            if (k.currentState == KaijuAI.AIState.Retreating) return "RETREATING";
            if (k.currentState == KaijuAI.AIState.Attacking) return "ATTACKING";
            return "ENGAGING";
        }

        private void UpdateStamina()
        {
            if (jaeger == null) return;

            float ratio = Mathf.Clamp01(jaeger.StaminaRatio);
            if (staminaBar != null)
            {
                staminaBar.SetRatio(ratio);
                if (staminaBar.fillImage != null)
                {
                    if (jaeger.IsExhausted)
                    {
                        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 12f);
                        staminaBar.fillImage.color = Color.Lerp(PacificRimUITheme.Danger, PacificRimUITheme.Amber, pulse * 0.45f);
                    }
                    else if (ratio < 0.35f)
                    {
                        staminaBar.fillImage.color = PacificRimUITheme.Amber;
                    }
                    else
                    {
                        staminaBar.fillImage.color = StaminaCyan;
                    }
                }
            }

            if (staminaCaptionText != null)
            {
                staminaCaptionText.text = jaeger.IsExhausted ? "EXHAUSTED <35%" : "STAMINA";
                staminaCaptionText.color = jaeger.IsExhausted ? PacificRimUITheme.Danger : PacificRimUITheme.TextDim;
            }

            if (staminaValueText != null)
            {
                staminaValueText.text = $"{Mathf.RoundToInt(ratio * 100f)}%";
                staminaValueText.color = jaeger.IsExhausted
                    ? PacificRimUITheme.Danger
                    : (ratio < 0.35f ? PacificRimUITheme.Amber : PacificRimUITheme.Text);
            }
        }

        private void UpdateScreenImpact()
        {
            if (impactFlashGroup == null) return;

            impactFlashAlpha = Mathf.MoveTowards(impactFlashAlpha, 0f, Time.unscaledDeltaTime * impactFlashDecaySpeed);
            impactFlashGroup.alpha = impactFlashAlpha;
        }

        public void TriggerScreenImpact(bool wasBlocked, bool isHeavy)
        {
            impactFlashDecaySpeed = 2.8f;
            impactFlashAlpha = Mathf.Max(
                impactFlashAlpha,
                wasBlocked ? (isHeavy ? 0.72f : 0.50f) : (isHeavy ? 0.92f : 0.68f));

            if (impactFlashGroup != null)
                impactFlashGroup.alpha = impactFlashAlpha;

            if (impactEdges == null) return;

            Color baseColor = wasBlocked
                ? PacificRimUITheme.BlueBright
                : (isHeavy ? PacificRimUITheme.Danger : PacificRimUITheme.Amber);
            float edgeAlpha = wasBlocked ? 0.46f : (isHeavy ? 0.64f : 0.48f);
            Color edgeTint = new Color(baseColor.r, baseColor.g, baseColor.b, edgeAlpha);

            for (int i = 0; i < impactEdges.Length; i++)
            {
                if (impactEdges[i] == null) continue;
                string edgeName = impactEdges[i].gameObject.name;
                if (edgeName.EndsWith("Top"))
                    impactEdges[i].SetVertical(edgeTint, Color.clear);
                else if (edgeName.EndsWith("Bottom"))
                    impactEdges[i].SetVertical(Color.clear, edgeTint);
                else if (edgeName.EndsWith("Left"))
                    impactEdges[i].SetHorizontal(edgeTint, Color.clear);
                else
                    impactEdges[i].SetHorizontal(Color.clear, edgeTint);
            }
        }

        /// <summary>
        /// Triggers the golden parry vignette peripheral cue and tactical banner.
        /// Visually mirrors the red hurt vignette style, but in rich metallic gold and tuned to be clean and non-intrusive.
        /// </summary>
        public void TriggerParryCue(float staminaPercent = 0.15f)
        {
            // Golden parry vignette: smoother, less harsh than damage flash (0.52 alpha vs 0.92) but immediately unmistakable
            impactFlashDecaySpeed = 1.45f;
            impactFlashAlpha = 0.54f;

            if (impactFlashGroup != null)
                impactFlashGroup.alpha = impactFlashAlpha;

            if (impactEdges != null)
            {
                // Radiant metallic gold
                Color goldColor = new Color(1.0f, 0.82f, 0.16f);
                float edgeAlpha = 0.52f;
                Color edgeTint = new Color(goldColor.r, goldColor.g, goldColor.b, edgeAlpha);

                for (int i = 0; i < impactEdges.Length; i++)
                {
                    if (impactEdges[i] == null) continue;
                    string edgeName = impactEdges[i].gameObject.name;
                    if (edgeName.EndsWith("Top"))
                        impactEdges[i].SetVertical(edgeTint, Color.clear);
                    else if (edgeName.EndsWith("Bottom"))
                        impactEdges[i].SetVertical(Color.clear, edgeTint);
                    else if (edgeName.EndsWith("Left"))
                        impactEdges[i].SetHorizontal(edgeTint, Color.clear);
                    else
                        impactEdges[i].SetHorizontal(Color.clear, edgeTint);
                }
            }
        }

        public void NotifyStaminaExhausted()
        {
            if (Time.unscaledTime - lastExhaustedBannerTime < 2.0f) return;
            lastExhaustedBannerTime = Time.unscaledTime;
            ShowBanner("EXHAUSTED  ·  RECOVER TO 35% STAMINA", 2.0f);
        }

        private void UpdateBanner()
        {
            if (bannerGroup == null) return;

            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.deltaTime;
                bannerAlpha = Mathf.MoveTowards(bannerAlpha, 1f, Time.deltaTime * 6f);
            }
            else
            {
                bannerAlpha = Mathf.MoveTowards(bannerAlpha, 0f, Time.deltaTime * 3.5f);
            }

            bannerGroup.alpha = bannerAlpha;
        }

        private void UpdateCombatTimer()
        {
            EnsureTimerUI();

            bool isCutscene = isCutsceneMode;
            if (PacificRimCutscene.PacificRimDeploymentCutscene.Instance != null &&
                PacificRimCutscene.PacificRimDeploymentCutscene.Instance.isCutsceneRunning)
            {
                isCutscene = true;
            }

            if (!isCutscene && !isCombatFinished && !MainMenuUI.IsMenuOpen)
            {
                isCombatTimerRunning = true;
                combatElapsedTime += Time.deltaTime;
            }
            else if (isCutscene)
            {
                isCombatTimerRunning = false;
            }

            if (combatTimerText != null)
            {
                combatTimerText.text = PacificRimCombatRecords.FormatTime(combatElapsedTime);
                combatTimerText.color = isCombatTimerRunning ? PacificRimUITheme.BlueBright : PacificRimUITheme.TextDim;
            }

            if (bestTimeText != null)
            {
                float best = PacificRimCombatRecords.GetBestTime();
                bestTimeText.text = $"BEST  {PacificRimCombatRecords.FormatTime(best)}";
                bestTimeText.color = PacificRimUITheme.Amber;
            }
        }

        private void EnsureTimerUI()
        {
            if (combatTimerText != null && bestTimeText != null) return;

            Transform topRight = transform.Find("TopRight_Sync");
            if (topRight == null && linkStatusText != null)
            {
                topRight = linkStatusText.transform.parent;
            }
            if (topRight == null) return;

            // Expand TopRight_Sync rect to accommodate timer row
            RectTransform rt = topRight.GetComponent<RectTransform>();
            if (rt != null && rt.sizeDelta.y < 50f)
            {
                rt.sizeDelta = new Vector2(Mathf.Max(420f, rt.sizeDelta.x), 58f);
            }

            Transform row = topRight.Find("TopRight_TimerRow");
            if (row == null)
            {
                GameObject rowGo = new GameObject("TopRight_TimerRow", typeof(RectTransform));
                rowGo.transform.SetParent(topRight, false);
                row = rowGo.transform;

                RectTransform rRt = rowGo.GetComponent<RectTransform>();
                rRt.anchorMin = new Vector2(0f, 1f);
                rRt.anchorMax = new Vector2(1f, 1f);
                rRt.pivot = new Vector2(1f, 1f);
                rRt.anchoredPosition = new Vector2(0f, 0f);
                rRt.sizeDelta = new Vector2(0f, 26f);

                // Best time text on left of row
                GameObject bGo = new GameObject("BestTimeText", typeof(RectTransform), typeof(Text));
                bGo.transform.SetParent(row, false);
                bestTimeText = bGo.GetComponent<Text>();
                bestTimeText.font = PacificRimUITheme.GetDefaultFont();
                bestTimeText.fontSize = 12;
                bestTimeText.alignment = TextAnchor.MiddleRight;
                bestTimeText.color = PacificRimUITheme.Amber;
                bestTimeText.raycastTarget = false;
                bestTimeText.text = $"BEST  {PacificRimCombatRecords.FormatTime(PacificRimCombatRecords.GetBestTime())}";
                RectTransform bRt = bGo.GetComponent<RectTransform>();
                bRt.anchorMin = new Vector2(0f, 0f);
                bRt.anchorMax = new Vector2(0.52f, 1f);
                bRt.offsetMin = Vector2.zero;
                bRt.offsetMax = new Vector2(-8f, 0f);

                // Combat timer on right of row
                GameObject tGo = new GameObject("CombatTimerText", typeof(RectTransform), typeof(Text));
                tGo.transform.SetParent(row, false);
                combatTimerText = tGo.GetComponent<Text>();
                combatTimerText.font = PacificRimUITheme.GetDefaultFont();
                combatTimerText.fontSize = 16;
                combatTimerText.fontStyle = FontStyle.Bold;
                combatTimerText.alignment = TextAnchor.MiddleRight;
                combatTimerText.color = PacificRimUITheme.BlueBright;
                combatTimerText.raycastTarget = false;
                combatTimerText.text = "00:00";
                RectTransform tRt = tGo.GetComponent<RectTransform>();
                tRt.anchorMin = new Vector2(0.52f, 0f);
                tRt.anchorMax = new Vector2(1f, 1f);
                tRt.offsetMin = Vector2.zero;
                tRt.offsetMax = Vector2.zero;

                timerContainer = rowGo;

                // Adjust linkStatusText to sit on bottom row
                if (linkStatusText != null)
                {
                    RectTransform lRt = linkStatusText.rectTransform;
                    lRt.anchorMin = new Vector2(0f, 0f);
                    lRt.anchorMax = new Vector2(1f, 0f);
                    lRt.pivot = new Vector2(1f, 0f);
                    lRt.anchoredPosition = new Vector2(0f, 0f);
                    lRt.sizeDelta = new Vector2(0f, 22f);
                }
            }
            else
            {
                if (combatTimerText == null)
                {
                    Transform t = row.Find("CombatTimerText");
                    if (t != null) combatTimerText = t.GetComponent<Text>();
                }
                if (bestTimeText == null)
                {
                    Transform b = row.Find("BestTimeText");
                    if (b != null) bestTimeText = b.GetComponent<Text>();
                }
            }
        }
    }
}
