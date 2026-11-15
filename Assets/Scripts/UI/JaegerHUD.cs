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
        [SerializeField] private PacificRimKeyPrompt overchargePrompt;
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
        private bool stateInitialized;
        private CanvasGroup hudCanvasGroup;
        private bool isCutsceneMode;
        private Coroutine neuralBootRoutine;

        // GTA-style "baguvix" cheat code state
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

            ShowBanner("<color=#00e5ff><b>NEURAL HANDSHAKE: 100% // ALL WEAPON SYSTEMS ONLINE</b></color>", 2.4f);
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

            if (cheatInputBuffer.EndsWith("baguvix", System.StringComparison.OrdinalIgnoreCase))
            {
                cheatInputBuffer = "";
                TriggerBaguvixCheat();
            }
            else if (cheatInputBuffer.EndsWith("hesoyam", System.StringComparison.OrdinalIgnoreCase))
            {
                cheatInputBuffer = "";
                TriggerHesoyamCheat();
            }
            else if (cheatInputBuffer.EndsWith("ironman", System.StringComparison.OrdinalIgnoreCase))
            {
                cheatInputBuffer = "";
                TriggerIronmanCheat();
            }
        }

        public void TriggerIronmanCheat()
        {
            if (jaeger == null)
                jaeger = FindAnyObjectByType<SimpleJaegerController>();

            bool active = true;
            if (jaeger != null)
            {
                jaeger.ToggleIronmanCheat();
                active = jaeger.IsIronmanCheatActive;
            }

            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHUDCheatActivated();
            PacificRimUISound.PlaySuccess();
            string bannerMsg = active
                ? "CHEAT ACTIVATED // IRONMAN: 3X DAMAGE & 3X RESISTANCE"
                : "CHEAT DEACTIVATED // IRONMAN: NORMAL POWER";
            ShowBanner(bannerMsg, 4.0f);
            Debug.Log($"<color=#00e5ff><b>[CHEAT]</b></color> IRONMAN {(active ? "ACTIVATED: 3x Strike Damage & 3x Attack Resistance" : "DEACTIVATED")}");
        }

        public void TriggerHesoyamCheat()
        {
            if (kaiju == null)
                kaiju = FindAnyObjectByType<KaijuAI>();

            if (kaiju != null)
            {
                kaiju.DisableAIEntirely();
            }

            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHUDCheatActivated();
            PacificRimUISound.PlaySuccess();
            ShowBanner("CHEAT ACTIVATED // HESOYAM: KAIJU AI DISABLED", 4.0f);
            Debug.Log("<color=#00e5ff><b>[CHEAT]</b></color> HESOYAM ACTIVATED: Leatherback's AI is completely disabled until level restart!");
        }

        public void TriggerBaguvixCheat()
        {
            if (kaiju == null)
                kaiju = FindAnyObjectByType<KaijuAI>();

            if (kaiju != null)
            {
                kaiju.ActivateBaguvixCheat();
            }

            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHUDCheatActivated();
            PacificRimUISound.PlaySuccess();
            ShowBanner("CHEAT ACTIVATED // BAGUVIX: PACIFIST KAIJU", 4.0f);
            Debug.Log("<color=#00e5ff><b>[CHEAT]</b></color> BAGUVIX ACTIVATED: Leatherback's hostility disabled! Fallback AI & invulnerability engaged until level restart.");
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

            // GTA-style "baguvix" cheat code detection
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
                hullCaptionText.text = jaeger.IsDead ? "HULL COMPROMISED // DESTROYED" : "HULL INTEGRITY";
                hullCaptionText.color = jaeger.IsDead ? PacificRimUITheme.Danger : PacificRimUITheme.TextDim;
            }

            if (jaeger.IsDead && !wasJaegerDead)
            {
                PacificRimUISound.PlayDeny();
                isCombatFinished = true;
                isCombatTimerRunning = false;
                ShowBanner("CRITICAL HULL BREACH // JAEGER DESTROYED", 4.0f);
            }
            wasJaegerDead = jaeger.IsDead;

            // Overcharge transition & countdown
            bool powered = jaeger.IsPoweredUp;
            if (powered && !wasPoweredUp)
            {
                powerUpTimer = Mathf.Max(0.1f, jaeger.powerUpDuration);
                PacificRimUISound.PlaySuccess();
                ShowBanner("TURBINE OVERCHARGE ENGAGED", 2.2f);
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
                    overchargeStatusText.text = PacificRimUITheme.Track("TURBINE OVERCHARGE", 2, 12, true);
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

            if (combatStanceValueText != null)
            {
                combatStanceValueText.supportRichText = true;
                combatStanceValueText.text = PacificRimUITheme.Track(
                    inStance ? "ENGAGED // COMBAT READY" : "STANDBY",
                    1, 13, true);
                combatStanceValueText.color = inStance ? PacificRimUITheme.BlueBright : PacificRimUITheme.TextDim;
            }

            if (defenseMatrixValueText != null)
            {
                defenseMatrixValueText.supportRichText = true;
                defenseMatrixValueText.text = PacificRimUITheme.Track(
                    blocking ? "MATRIX LOCKED" : "OPEN",
                    1, 13, true);
                defenseMatrixValueText.color = blocking ? PacificRimUITheme.BlueBright : PacificRimUITheme.TextDim;
            }

            if (depthTelemetryText != null)
            {
                float waterY = 0f;
                if (WaterSystem.Instance != null)
                {
                    waterY = WaterSystem.Instance.GetWaterHeight(jaeger.transform.position);
                }
                float depth = Mathf.Max(0f, waterY - jaeger.transform.position.y);
                depthTelemetryText.supportRichText = true;
                depthTelemetryText.text = PacificRimUITheme.Track($"SEA DEPTH {depth:0.0}M", 1, 13, true);
            }

            // Update tactical prompts (attacks are always armed)
            if (stancePrompt != null && stancePrompt.gameObject.activeSelf)
                stancePrompt.gameObject.SetActive(false);
            if (lightAttackPrompt != null)
                lightAttackPrompt.SetHighlight(false, PacificRimUITheme.BlueBright);
            if (heavyAttackPrompt != null)
                heavyAttackPrompt.SetHighlight(false, PacificRimUITheme.BlueBright);
            if (defensePrompt != null)
                defensePrompt.SetHighlight(blocking, PacificRimUITheme.BlueBright);
            if (overchargePrompt != null)
                overchargePrompt.SetHighlight(jaeger.IsPoweredUp, PacificRimUITheme.Amber);

            // Reticle state tint
            if (holoReticle != null)
            {
                Color targetColor;
                if (blocking)
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

            if (bossHpNumberText != null)
            {
                bossHpNumberText.text = $"{PacificRimUITheme.FormatNumber(Mathf.CeilToInt(curHp))} / {PacificRimUITheme.FormatNumber(Mathf.CeilToInt(maxHp))}";
                bossHpNumberText.color = kaiju.IsDead ? PacificRimUITheme.TextDim : PacificRimUITheme.Text;
            }

            if (bossPhaseText != null)
            {
                bossPhaseText.text = kaiju.PhaseTitleLabel;
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
                string label = kaiju.TacticalStatusLabel;
                bossStatusText.text = PacificRimUITheme.Track(label, 2, 11, true);
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
                if (kaiju.CurrentPhase == 2)
                    ShowBanner("ALERT // LEATHERBACK ENTERED PHASE II: BIO-EMP OVERDRIVE", 3.2f);
                else if (kaiju.CurrentPhase >= 3)
                    ShowBanner("CRITICAL // LEATHERBACK PHASE III: CATEGORY IV BERSERKER", 3.6f);
            }
            wasKaijuPhase = kaiju.CurrentPhase;

            if (kaiju.IsEmpBuffed && !wasKaijuEmpBuffed)
            {
                ShowBanner("WARNING // LEATHERBACK BIO-EMP OVERCHARGE", 2.8f);
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
                    ShowBanner($"NEW BEST RECORD! // TIME: {PacificRimCombatRecords.FormatTime(combatElapsedTime)}", 6.0f);
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlayHUDCheatActivated();
                }
                else
                {
                    ShowBanner($"CATEGORY IV KAIJU NEUTRALIZED // TIME: {PacificRimCombatRecords.FormatTime(combatElapsedTime)}", 5.0f);
                }
            }
            wasKaijuDead = kaiju.IsDead;
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
                staminaCaptionText.text = jaeger.IsExhausted ? "ACTUATOR EXHAUSTED (<35%)" : "ACTUATOR STAMINA";
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
            ShowBanner("ACTUATOR EXHAUSTED // RECOVER TO 35% TO ATTACK", 2.2f);
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
                combatTimerText.text = $"TIME: {PacificRimCombatRecords.FormatTime(combatElapsedTime)}";
                combatTimerText.color = isCombatTimerRunning ? PacificRimUITheme.BlueBright : PacificRimUITheme.TextDim;
            }

            if (bestTimeText != null)
            {
                float best = PacificRimCombatRecords.GetBestTime();
                bestTimeText.text = $"BEST: {PacificRimCombatRecords.FormatTime(best)}";
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
                bestTimeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                bestTimeText.fontSize = 13;
                bestTimeText.alignment = TextAnchor.MiddleRight;
                bestTimeText.color = PacificRimUITheme.Amber;
                bestTimeText.raycastTarget = false;
                bestTimeText.text = $"BEST: {PacificRimCombatRecords.FormatTime(PacificRimCombatRecords.GetBestTime())}";
                RectTransform bRt = bGo.GetComponent<RectTransform>();
                bRt.anchorMin = new Vector2(0f, 0f);
                bRt.anchorMax = new Vector2(0.52f, 1f);
                bRt.offsetMin = Vector2.zero;
                bRt.offsetMax = new Vector2(-8f, 0f);

                // Combat timer on right of row
                GameObject tGo = new GameObject("CombatTimerText", typeof(RectTransform), typeof(Text));
                tGo.transform.SetParent(row, false);
                combatTimerText = tGo.GetComponent<Text>();
                combatTimerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                combatTimerText.fontSize = 16;
                combatTimerText.fontStyle = FontStyle.Bold;
                combatTimerText.alignment = TextAnchor.MiddleRight;
                combatTimerText.color = PacificRimUITheme.BlueBright;
                combatTimerText.raycastTarget = false;
                combatTimerText.text = "TIME: 00:00";
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
