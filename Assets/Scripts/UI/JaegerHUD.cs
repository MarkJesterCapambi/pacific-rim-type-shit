using System.Collections.Generic;
using System.Text;
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

        [Header("Plasmacaster Ultimate (Bottom-Center)")]
        [SerializeField] private PacificRimUltimateWidget ultimateWidget;

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

        // Kaiju EMP / Slattern Emote HUD Glitch State
        private bool wasHudGlitching;
        private float glitchTickTimer;
        private int glitchTickCounter;
        private readonly Dictionary<RectTransform, Vector2> cachedBaseAnchoredPos = new Dictionary<RectTransform, Vector2>(24);
        private bool hasCachedChassisBase;
        private string cachedChassisBaseText;
        private Color cachedChassisBaseColor;
        private Image[] glitchScanBars;
        private readonly StringBuilder glitchStringBuilder = new StringBuilder(32);

        // Admin UI toggle & meme command buffer ("hesoyam", "freddy", "america")
        private static int suppressFreeCamFrame = -1;
        public static bool SuppressFreeCamThisFrame => suppressFreeCamFrame == Time.frameCount;

        private string cheatInputBuffer = "";
        private int lastCheatCharFrame = -1;
        private uint textInputMaskThisFrame = 0u;
        private uint legacyInputMaskThisFrame = 0u;
        private uint keyPollMaskThisFrame = 0u;
#if ENABLE_INPUT_SYSTEM
        private Keyboard subscribedKeyboard;
#endif

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
            SubscribeKeyboardTextInput();
#endif
            ResolveReferences();
            SyncImmediate();
        }

        private void OnDisable()
        {
            ClearEmpGlitchEffect();
#if ENABLE_INPUT_SYSTEM
            UnsubscribeKeyboardTextInput();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void SubscribeKeyboardTextInput()
        {
            var kb = Keyboard.current;
            if (subscribedKeyboard == kb) return;
            UnsubscribeKeyboardTextInput();
            if (kb != null)
            {
                kb.onTextInput += OnTextInputReceived;
                subscribedKeyboard = kb;
            }
        }

        private void UnsubscribeKeyboardTextInput()
        {
            if (subscribedKeyboard != null)
            {
                subscribedKeyboard.onTextInput -= OnTextInputReceived;
                subscribedKeyboard = null;
            }
        }
#endif

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
            if (!hidden && PacificRimMeme.MemeCommandController.IsAmericaActive)
            {
                hidden = true;
            }
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

        private enum CheatInputSource
        {
            TextInput,
            LegacyString,
            KeyPoll
        }

#if ENABLE_INPUT_SYSTEM
        private void OnTextInputReceived(char c)
        {
            if (MainMenuUI.IsMenuOpen) return;
            AppendCheatChar(c, CheatInputSource.TextInput);
        }
#endif

        private void UpdateCheatCodeInput()
        {
            if (MainMenuUI.IsMenuOpen) return;

#if ENABLE_INPUT_SYSTEM
            SubscribeKeyboardTextInput();
#endif

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
                            AppendCheatChar(c, CheatInputSource.LegacyString);
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
                if (kb.aKey.wasPressedThisFrame) AppendCheatChar('a', CheatInputSource.KeyPoll);
                if (kb.bKey.wasPressedThisFrame) AppendCheatChar('b', CheatInputSource.KeyPoll);
                if (kb.cKey.wasPressedThisFrame) AppendCheatChar('c', CheatInputSource.KeyPoll);
                if (kb.dKey.wasPressedThisFrame) AppendCheatChar('d', CheatInputSource.KeyPoll);
                if (kb.eKey.wasPressedThisFrame) AppendCheatChar('e', CheatInputSource.KeyPoll);
                if (kb.fKey.wasPressedThisFrame) AppendCheatChar('f', CheatInputSource.KeyPoll);
                if (kb.gKey.wasPressedThisFrame) AppendCheatChar('g', CheatInputSource.KeyPoll);
                if (kb.hKey.wasPressedThisFrame) AppendCheatChar('h', CheatInputSource.KeyPoll);
                if (kb.iKey.wasPressedThisFrame) AppendCheatChar('i', CheatInputSource.KeyPoll);
                if (kb.jKey.wasPressedThisFrame) AppendCheatChar('j', CheatInputSource.KeyPoll);
                if (kb.kKey.wasPressedThisFrame) AppendCheatChar('k', CheatInputSource.KeyPoll);
                if (kb.lKey.wasPressedThisFrame) AppendCheatChar('l', CheatInputSource.KeyPoll);
                if (kb.mKey.wasPressedThisFrame) AppendCheatChar('m', CheatInputSource.KeyPoll);
                if (kb.nKey.wasPressedThisFrame) AppendCheatChar('n', CheatInputSource.KeyPoll);
                if (kb.oKey.wasPressedThisFrame) AppendCheatChar('o', CheatInputSource.KeyPoll);
                if (kb.pKey.wasPressedThisFrame) AppendCheatChar('p', CheatInputSource.KeyPoll);
                if (kb.qKey.wasPressedThisFrame) AppendCheatChar('q', CheatInputSource.KeyPoll);
                if (kb.rKey.wasPressedThisFrame) AppendCheatChar('r', CheatInputSource.KeyPoll);
                if (kb.sKey.wasPressedThisFrame) AppendCheatChar('s', CheatInputSource.KeyPoll);
                if (kb.tKey.wasPressedThisFrame) AppendCheatChar('t', CheatInputSource.KeyPoll);
                if (kb.uKey.wasPressedThisFrame) AppendCheatChar('u', CheatInputSource.KeyPoll);
                if (kb.vKey.wasPressedThisFrame) AppendCheatChar('v', CheatInputSource.KeyPoll);
                if (kb.wKey.wasPressedThisFrame) AppendCheatChar('w', CheatInputSource.KeyPoll);
                if (kb.xKey.wasPressedThisFrame) AppendCheatChar('x', CheatInputSource.KeyPoll);
                if (kb.yKey.wasPressedThisFrame) AppendCheatChar('y', CheatInputSource.KeyPoll);
                if (kb.zKey.wasPressedThisFrame) AppendCheatChar('z', CheatInputSource.KeyPoll);
            }
#endif
        }

        private void AppendCheatChar(char c, CheatInputSource source)
        {
            char lower = char.ToLowerInvariant(c);
            if (lower < 'a' || lower > 'z') return;

            if (Time.frameCount != lastCheatCharFrame)
            {
                lastCheatCharFrame = Time.frameCount;
                textInputMaskThisFrame = 0u;
                legacyInputMaskThisFrame = 0u;
                keyPollMaskThisFrame = 0u;
            }

            uint charBit = 1u << (lower - 'a');
            switch (source)
            {
                case CheatInputSource.TextInput:
                    // onTextInput fires once per actual key event (allows fast same-frame double taps like "dd")
                    textInputMaskThisFrame |= charBit;
                    break;
                case CheatInputSource.LegacyString:
                    if ((textInputMaskThisFrame & charBit) != 0u) return;
                    legacyInputMaskThisFrame |= charBit;
                    break;
                case CheatInputSource.KeyPoll:
                    if (((textInputMaskThisFrame | legacyInputMaskThisFrame | keyPollMaskThisFrame) & charBit) != 0u) return;
                    keyPollMaskThisFrame |= charBit;
                    break;
            }

            cheatInputBuffer += lower;
            if (cheatInputBuffer.Length > 24)
            {
                cheatInputBuffer = cheatInputBuffer.Substring(cheatInputBuffer.Length - 24);
            }

            if (cheatInputBuffer.EndsWith("he", System.StringComparison.OrdinalIgnoreCase) ||
                cheatInputBuffer.EndsWith("fre", System.StringComparison.OrdinalIgnoreCase) ||
                cheatInputBuffer.EndsWith("ame", System.StringComparison.OrdinalIgnoreCase))
            {
                suppressFreeCamFrame = Time.frameCount;
            }

            if (cheatInputBuffer.EndsWith("hesoyam", System.StringComparison.OrdinalIgnoreCase))
            {
                cheatInputBuffer = "";
                TriggerAdminUIToggle();
            }
            else if (cheatInputBuffer.EndsWith("freddy", System.StringComparison.OrdinalIgnoreCase))
            {
                cheatInputBuffer = "";
                PacificRimMeme.MemeCommandController.TriggerFreddyJumpscare();
            }
            else if (cheatInputBuffer.EndsWith("america", System.StringComparison.OrdinalIgnoreCase))
            {
                cheatInputBuffer = "";
                PacificRimMeme.MemeCommandController.TriggerAmericaAirstrike();
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
            UpdateEmpGlitchEffect();
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
            EnsureUltimateWidgetUI();
            if (ultimateWidget != null)
            {
                string keyStr = jaeger.plasmaCasterKey.ToString().ToUpperInvariant();
                ultimateWidget.SetImmediate(
                    jaeger.UltimateChargeRatio,
                    jaeger.UltimateChargePercent,
                    jaeger.IsUltimateReady,
                    jaeger.IsPlasmaAiming,
                    keyStr);
            }
            UpdateCombatAndTelemetry();
            UpdateBossHealthBar();
            ClearEmpGlitchEffect();
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

            // Reticle state tint (fade out during reverse Plasmacaster barrel cinematic)
            if (holoReticle != null)
            {
                Color targetColor;
                if (jaeger.IsPlasmaCinematicActive)
                    targetColor = new Color(0.40f, 0.98f, 1f, 0f);
                else if (jaeger.IsPlasmaCharging)
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

        private void EnsureUltimateWidgetUI()
        {
            if (ultimateWidget == null)
            {
                Transform existing = transform.Find("BottomCenter_Ultimate");
                if (existing != null)
                {
                    ultimateWidget = existing.GetComponent<PacificRimUltimateWidget>();
                    if (ultimateWidget == null)
                    {
                        ultimateWidget = existing.gameObject.AddComponent<PacificRimUltimateWidget>();
                    }
                }
                else
                {
                    var go = new GameObject("BottomCenter_Ultimate", typeof(RectTransform));
                    go.transform.SetParent(transform, false);
                    ultimateWidget = go.AddComponent<PacificRimUltimateWidget>();
                }
            }
            ultimateWidget.EnsureBuiltAndWired();
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
            EnsureUltimateWidgetUI();
            EnsurePlasmaPromptUI();
            if (jaeger == null) return;

            bool keyChanged = jaeger.plasmaCasterKey != lastPlasmaKeyCodeCached || string.IsNullOrEmpty(lastPlasmaKeyCached);
            if (keyChanged)
            {
                lastPlasmaKeyCodeCached = jaeger.plasmaCasterKey;
                lastPlasmaKeyCached = lastPlasmaKeyCodeCached.ToString().ToUpperInvariant();
            }
            string keyStr = lastPlasmaKeyCached;

            if (ultimateWidget != null)
            {
                ultimateWidget.UpdateState(
                    jaeger.UltimateChargeRatio,
                    jaeger.UltimateChargePercent,
                    jaeger.IsUltimateReady,
                    jaeger.IsPlasmaAiming,
                    keyStr,
                    Time.deltaTime);
            }

            if (plasmaPrompt == null) return;

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
            else if (jaeger.IsUltimateReady)
            {
                label = "ULT READY";
                highlight = true;
                accent = PacificRimUITheme.BlueBright;
            }
            else
            {
                label = $"{jaeger.UltimateChargePercent}%";
                highlight = false;
                accent = PacificRimUITheme.Amber;
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

        private void UpdateEmpGlitchEffect()
        {
            bool shouldGlitch = !MainMenuUI.IsMenuOpen
                && !isCutsceneMode
                && !IsHudHidden
                && kaiju != null
                && kaiju.IsHudDisruptionActive
                && jaeger != null
                && !jaeger.IsDead;

            if (!shouldGlitch)
            {
                if (wasHudGlitching)
                {
                    ClearEmpGlitchEffect();
                }
                return;
            }

            if (!wasHudGlitching)
            {
                wasHudGlitching = true;
                glitchTickTimer = 0f;
                glitchTickCounter = 1;
                CacheGlitchBaselines();

                string alertMsg = kaiju.isSlattern
                    ? "<color=#ff645a><b>ABYSSAL RESONANCE  ·  HUD TELEMETRY DISRUPTED</b></color>"
                    : "<color=#46c3ff><b>BIO-EMP INTERFERENCE  ·  HUD TELEMETRY DISRUPTED</b></color>";
                ShowBanner(alertMsg, 2.3f);
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayUiClick();
            }

            glitchTickTimer -= Time.deltaTime;
            if (glitchTickTimer <= 0f)
            {
                glitchTickTimer = Random.Range(0.038f, 0.068f);
                glitchTickCounter++;
            }

            float tickSeed = glitchTickCounter * 17.31f;
            Color accentGlitch = kaiju.isSlattern ? PacificRimUITheme.Danger : PacificRimUITheme.BlueBright;

            // Flicker player & boss numeric readouts
            GlitchNumberText(hullNumberText, tickSeed + 1.1f, 7f, 2f, accentGlitch, scrambleChance: 0.42f);
            GlitchNumberText(staminaValueText, tickSeed + 2.3f, 5f, 1.5f, accentGlitch, scrambleChance: 0.40f);
            GlitchNumberText(bossHpNumberText, tickSeed + 3.7f, 6f, 1.5f, accentGlitch, scrambleChance: 0.38f);
            GlitchNumberText(overchargeTimerText, tickSeed + 4.9f, 4f, 1f, accentGlitch, scrambleChance: 0.35f);
            GlitchNumberText(combatTimerText, tickSeed + 6.1f, 5f, 1f, accentGlitch, scrambleChance: 0.35f);
            GlitchNumberText(bestTimeText, tickSeed + 7.3f, 3.5f, 1f, accentGlitch, scrambleChance: 0.25f);
            GlitchNumberText(depthTelemetryText, tickSeed + 8.5f, 4.5f, 1f, accentGlitch, scrambleChance: 0.38f);

            // Flicker HP, Stamina, Boss, Poise & Overcharge bars
            GlitchBar(hullBar, tickSeed + 11.1f, accentGlitch);
            GlitchBar(staminaBar, tickSeed + 12.7f, accentGlitch);
            GlitchBar(bossHealthBar, tickSeed + 14.3f, accentGlitch);
            GlitchBar(bossSubBar, tickSeed + 15.9f, accentGlitch);
            if (overchargeContainer != null && overchargeContainer.activeSelf)
            {
                GlitchBar(overchargeBar, tickSeed + 17.5f, accentGlitch);
            }

            // Flicker status captions & combat matrix labels
            GlitchLabelText(hullCaptionText, tickSeed + 21.1f, "SIGNAL ERR", 0.24f, accentGlitch);
            GlitchLabelText(staminaCaptionText, tickSeed + 22.3f, "SYNC LOSS", 0.22f, accentGlitch);
            GlitchLabelText(combatStanceValueText, tickSeed + 23.5f, "ERR//0xF", 0.28f, accentGlitch);
            GlitchLabelText(defenseMatrixValueText, tickSeed + 24.7f, "STATIC", 0.28f, accentGlitch);
            GlitchLabelText(bossPhaseText, tickSeed + 25.9f, "INTERFERENCE", 0.20f, accentGlitch);
            GlitchLabelText(bossStatusText, tickSeed + 27.1f, kaiju.isSlattern ? "RESONANCE" : "EMP DISCHARGE", 0.25f, accentGlitch);
            GlitchLabelText(chassisLabelText, tickSeed + 28.3f, "DRIFT LINK UNSTABLE", 0.26f, accentGlitch);

            // Flicker holographic center reticle
            if (holoReticle != null)
            {
                float rHash = Hash01(tickSeed + 31.7f);
                RectTransform rRt = holoReticle.rectTransform;
                CacheRectBasePos(rRt);
                if (cachedBaseAnchoredPos.TryGetValue(rRt, out Vector2 basePos))
                {
                    float jx = (Hash01(tickSeed + 32.1f) - 0.5f) * 10f;
                    float jy = (Hash01(tickSeed + 33.4f) - 0.5f) * 6f;
                    rRt.anchoredPosition = basePos + new Vector2(jx, jy);
                }
                rRt.localEulerAngles = new Vector3(0f, 0f, (Hash01(tickSeed + 34.9f) - 0.5f) * 22f);

                Color c = rHash < 0.22f
                    ? new Color(PacificRimUITheme.Danger.r, PacificRimUITheme.Danger.g, PacificRimUITheme.Danger.b, 0.85f)
                    : (rHash < 0.50f
                        ? new Color(accentGlitch.r, accentGlitch.g, accentGlitch.b, 0.22f)
                        : new Color(0.85f, 0.98f, 1f, 0.88f));
                holoReticle.color = c;
            }

            UpdateGlitchScanBars(tickSeed, accentGlitch);
        }

        private void ClearEmpGlitchEffect()
        {
            if (!wasHudGlitching && glitchScanBars == null) return;
            wasHudGlitching = false;

            foreach (var kvp in cachedBaseAnchoredPos)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.anchoredPosition = kvp.Value;
                }
            }

            if (holoReticle != null)
            {
                holoReticle.rectTransform.localEulerAngles = Vector3.zero;
            }

            if (chassisLabelText != null && hasCachedChassisBase)
            {
                chassisLabelText.text = cachedChassisBaseText;
                chassisLabelText.color = cachedChassisBaseColor;
            }

            hullBar?.SetGlitchVisual(false);
            staminaBar?.SetGlitchVisual(false);
            bossHealthBar?.SetGlitchVisual(false);
            bossSubBar?.SetGlitchVisual(false);
            overchargeBar?.SetGlitchVisual(false);

            if (glitchScanBars != null)
            {
                for (int i = 0; i < glitchScanBars.Length; i++)
                {
                    if (glitchScanBars[i] != null && glitchScanBars[i].gameObject.activeSelf)
                    {
                        glitchScanBars[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void CacheGlitchBaselines()
        {
            CacheRectBasePos(hullNumberText != null ? hullNumberText.rectTransform : null);
            CacheRectBasePos(staminaValueText != null ? staminaValueText.rectTransform : null);
            CacheRectBasePos(bossHpNumberText != null ? bossHpNumberText.rectTransform : null);
            CacheRectBasePos(overchargeTimerText != null ? overchargeTimerText.rectTransform : null);
            CacheRectBasePos(combatTimerText != null ? combatTimerText.rectTransform : null);
            CacheRectBasePos(bestTimeText != null ? bestTimeText.rectTransform : null);
            CacheRectBasePos(depthTelemetryText != null ? depthTelemetryText.rectTransform : null);
            CacheRectBasePos(hullCaptionText != null ? hullCaptionText.rectTransform : null);
            CacheRectBasePos(staminaCaptionText != null ? staminaCaptionText.rectTransform : null);
            CacheRectBasePos(combatStanceValueText != null ? combatStanceValueText.rectTransform : null);
            CacheRectBasePos(defenseMatrixValueText != null ? defenseMatrixValueText.rectTransform : null);
            CacheRectBasePos(bossPhaseText != null ? bossPhaseText.rectTransform : null);
            CacheRectBasePos(bossStatusText != null ? bossStatusText.rectTransform : null);
            CacheRectBasePos(chassisLabelText != null ? chassisLabelText.rectTransform : null);
            CacheRectBasePos(holoReticle != null ? holoReticle.rectTransform : null);

            CacheRectBasePos(hullBar != null ? hullBar.GetComponent<RectTransform>() : null);
            CacheRectBasePos(staminaBar != null ? staminaBar.GetComponent<RectTransform>() : null);
            CacheRectBasePos(bossHealthBar != null ? bossHealthBar.GetComponent<RectTransform>() : null);
            CacheRectBasePos(bossSubBar != null ? bossSubBar.GetComponent<RectTransform>() : null);
            CacheRectBasePos(overchargeBar != null ? overchargeBar.GetComponent<RectTransform>() : null);

            if (chassisLabelText != null && !hasCachedChassisBase)
            {
                cachedChassisBaseText = chassisLabelText.text;
                cachedChassisBaseColor = chassisLabelText.color;
                hasCachedChassisBase = true;
            }
        }

        private void CacheRectBasePos(RectTransform rt)
        {
            if (rt == null || cachedBaseAnchoredPos.ContainsKey(rt)) return;
            cachedBaseAnchoredPos[rt] = rt.anchoredPosition;
        }

        private void GlitchNumberText(Text txt, float seed, float maxOffsetX, float maxOffsetY, Color accentColor, float scrambleChance)
        {
            if (txt == null) return;
            CacheRectBasePos(txt.rectTransform);

            float h1 = Hash01(seed);
            float h2 = Hash01(seed + 5.3f);
            float h3 = Hash01(seed + 9.7f);

            if (cachedBaseAnchoredPos.TryGetValue(txt.rectTransform, out Vector2 basePos))
            {
                Vector2 offset = h1 < 0.65f
                    ? new Vector2((h2 - 0.5f) * 2f * maxOffsetX, (h3 - 0.5f) * 2f * maxOffsetY)
                    : Vector2.zero;
                txt.rectTransform.anchoredPosition = basePos + offset;
            }

            if (h1 < scrambleChance && !string.IsNullOrEmpty(txt.text))
            {
                txt.text = ScrambleNumericString(txt.text, seed + 13.1f);
            }

            Color c = txt.color;
            if (h2 < 0.14f)
            {
                c.a = 0.06f; // Brief digital dropout blink
            }
            else if (h2 < 0.42f)
            {
                c = h3 < 0.5f ? PacificRimUITheme.Danger : accentColor;
                c.a = Mathf.Lerp(0.55f, 1.0f, Hash01(seed + 17.7f));
            }
            else if (h2 < 0.68f)
            {
                c.a = Mathf.Lerp(0.28f, 0.72f, Hash01(seed + 23.9f));
            }
            txt.color = c;
        }

        private void GlitchLabelText(Text txt, float seed, string errorAlias, float corruptChance, Color accentColor)
        {
            if (txt == null) return;
            CacheRectBasePos(txt.rectTransform);

            float h1 = Hash01(seed);
            float h2 = Hash01(seed + 3.9f);

            if (cachedBaseAnchoredPos.TryGetValue(txt.rectTransform, out Vector2 basePos))
            {
                Vector2 offset = h1 < 0.5f
                    ? new Vector2((h2 - 0.5f) * 6f, 0f)
                    : Vector2.zero;
                txt.rectTransform.anchoredPosition = basePos + offset;
            }

            if (h1 < corruptChance)
            {
                txt.text = errorAlias;
                txt.color = h2 < 0.5f ? PacificRimUITheme.Danger : accentColor;
            }
            else if (h2 < 0.25f)
            {
                Color c = txt.color;
                c.a *= 0.25f;
                txt.color = c;
            }
        }

        private void GlitchBar(PacificRimBar bar, float seed, Color accentColor)
        {
            if (bar == null) return;

            RectTransform rt = bar.GetComponent<RectTransform>();
            CacheRectBasePos(rt);

            float h1 = Hash01(seed);
            float h2 = Hash01(seed + 4.1f);
            float h3 = Hash01(seed + 8.9f);

            if (rt != null && cachedBaseAnchoredPos.TryGetValue(rt, out Vector2 basePos))
            {
                Vector2 offset = h1 < 0.6f
                    ? new Vector2((h2 - 0.5f) * 8f, (h3 - 0.5f) * 2f)
                    : Vector2.zero;
                rt.anchoredPosition = basePos + offset;
            }

            float ratioOffset = h1 < 0.55f ? Mathf.Lerp(-0.26f, 0.22f, h2) : 0f;
            float alphaMult = h3 < 0.16f ? 0.12f : (h3 < 0.45f ? 0.48f : 1.0f);

            Color? tint = null;
            if (h2 < 0.25f) tint = PacificRimUITheme.Danger;
            else if (h2 < 0.48f) tint = accentColor;
            else if (h2 > 0.88f) tint = Color.white;

            bar.SetGlitchVisual(true, ratioOffset, alphaMult, tint);
        }

        private string ScrambleNumericString(string src, float seed)
        {
            const string glitchChars = "08#?-!";
            glitchStringBuilder.Length = 0;
            for (int i = 0; i < src.Length; i++)
            {
                char ch = src[i];
                if (char.IsDigit(ch))
                {
                    float h = Hash01(seed + i * 3.71f);
                    if (h < 0.65f)
                    {
                        int idx = Mathf.Clamp(Mathf.FloorToInt(Hash01(seed + i * 7.13f) * glitchChars.Length), 0, glitchChars.Length - 1);
                        glitchStringBuilder.Append(glitchChars[idx]);
                    }
                    else
                    {
                        glitchStringBuilder.Append(ch);
                    }
                }
                else
                {
                    glitchStringBuilder.Append(ch);
                }
            }
            return glitchStringBuilder.ToString();
        }

        private void EnsureGlitchScanBars()
        {
            if (glitchScanBars != null) return;

            const int barCount = 3;
            glitchScanBars = new Image[barCount];
            for (int i = 0; i < barCount; i++)
            {
                GameObject go = new GameObject($"HUD_GlitchScanBar_{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = Color.clear;
                go.SetActive(false);
                glitchScanBars[i] = img;
            }
        }

        private void UpdateGlitchScanBars(float seed, Color accentColor)
        {
            EnsureGlitchScanBars();
            if (glitchScanBars == null) return;

            for (int i = 0; i < glitchScanBars.Length; i++)
            {
                Image img = glitchScanBars[i];
                if (img == null) continue;

                float hVisible = Hash01(seed + i * 19.3f);
                if (hVisible > 0.58f)
                {
                    if (img.gameObject.activeSelf) img.gameObject.SetActive(false);
                    continue;
                }

                if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);

                // Place thin scanline bursts in peripheral HUD regions (top/bottom edges, never blocking center view)
                float hY = Hash01(seed + i * 29.1f);
                float hX = Hash01(seed + i * 41.7f);
                float hW = Hash01(seed + i * 53.3f);
                float bandY = (i == 0)
                    ? Mathf.Lerp(0.84f, 0.96f, hY)
                    : (i == 1 ? Mathf.Lerp(0.05f, 0.22f, hY) : Mathf.Lerp(0.10f, 0.90f, hY));

                float widthFrac = Mathf.Lerp(0.14f, 0.42f, hW);
                float minX = (i == 2 && bandY > 0.28f && bandY < 0.72f)
                    ? (hX < 0.5f ? 0.02f : 0.78f) // side margins only if near mid-screen height
                    : Mathf.Lerp(0.02f, 0.96f - widthFrac, hX);

                RectTransform rt = img.rectTransform;
                rt.anchorMin = new Vector2(minX, bandY);
                rt.anchorMax = new Vector2(Mathf.Min(0.98f, minX + widthFrac), bandY);
                rt.offsetMin = new Vector2(0f, -1.5f);
                rt.offsetMax = new Vector2(0f, 1.5f);

                Color c = Hash01(seed + i * 71.9f) < 0.38f ? PacificRimUITheme.Danger : accentColor;
                c.a = Mathf.Lerp(0.09f, 0.24f, Hash01(seed + i * 83.1f));
                img.color = c;
            }
        }

        private static float Hash01(float x)
        {
            float s = Mathf.Sin(x * 12.9898f + 78.233f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }
    }
}
