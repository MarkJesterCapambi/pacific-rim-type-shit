using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PacificRimUI
{
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }
        public static bool IsMenuOpen => Instance != null && Instance.isMenuOpen;

        [Header("Scene Routing")]
        [Tooltip("True if this menu is running in the dedicated Main Menu scene.")]
        public bool isMainMenuScene = false;
        [Tooltip("Scene loaded when clicking DEPLOY JAEGER or pressing Play.")]
        public string gameplaySceneName = "SampleScene";
        [Tooltip("Scene loaded when returning to the Main Menu.")]
        public string mainMenuSceneName = "MainMenuScene";

        [Header("Canvas & Paneling")]
        [SerializeField] private GameObject menuVisualsRoot;
        [SerializeField] private CanvasGroup menuCanvasGroup;
        [SerializeField] private GameObject hudRoot;

        [Header("Navigation & Feature Cards")]
        [SerializeField] private PacificRimTabBar tabBar;
        [SerializeField] private GameObject[] tabPanels;
        [SerializeField] private PacificRimCard[] featureCards;

        [Header("Buttons")]
        [SerializeField] private PacificRimButton deployButton;
        [SerializeField] private PacificRimButton controlsButton;
        [SerializeField] private PacificRimButton specsButton;
        [SerializeField] private PacificRimButton restartButton;
        [SerializeField] private PacificRimButton exitButton;
        [SerializeField] private PacificRimButton returnToMenuButton;

        [Header("Pause Telemetry")]
        [SerializeField] private UnityEngine.UI.Text pauseTitleText;
        [SerializeField] private UnityEngine.UI.Text pauseSubtitleText;
        [SerializeField] private UnityEngine.UI.Text pauseHealthText;
        [SerializeField] private UnityEngine.UI.Text pauseMissionText;
        [SerializeField] private UnityEngine.UI.Text menuBestTimeText;

        [Header("Modals")]
        [SerializeField] private PacificRimModal controlsModal;
        [SerializeField] private PacificRimModal specsModal;

        [Header("HUD Link")]
        [SerializeField] private JaegerHUD jaegerHud;

        [Header("State")]
        public bool isMenuOpen = true;
        private bool isDefeated = false;
        private bool isVictorious = false;

        private bool listenersBound;

        private void Awake()
        {
            if (Instance == null || Instance == this)
                Instance = this;

            string currentScene = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(mainMenuSceneName) && currentScene.Equals(mainMenuSceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                isMainMenuScene = true;
            }
            else if (!string.IsNullOrEmpty(gameplaySceneName) && currentScene.Equals(gameplaySceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                isMainMenuScene = false;
            }

            // Keep MainMenu_Root always active so Update() can always listen for Esc.
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
        }

        private void Start()
        {
            BindListeners();

            if (isMainMenuScene)
            {
                // Pre-warm main menu shaders, audio, and cache in the background
                PacificRimLoadingScreen.WarmupMainMenuAssets();

                isMenuOpen = true;
                SetMenuState(true);
                if (deployButton != null)
                {
                    deployButton.SetText("DEPLOY JAEGER");
                }
                if (exitButton != null)
                {
                    exitButton.SetText("DISENGAGE");
                }
                if (pauseTitleText != null)
                {
                    pauseTitleText.text = "PACIFIC RIM: OPERATION PITFALL";
                    pauseTitleText.color = PacificRimUITheme.BlueBright;
                }
                if (pauseSubtitleText != null)
                {
                    pauseSubtitleText.text = "PPDC TACTICAL COMMAND // MISSION: DEFEND STRIKER EUREKA";
                }
                if (pauseMissionText != null)
                {
                    pauseMissionText.text = "MISSION OBJECTIVE: INTERCEPT LEATHERBACK // SAVE STRIKER EUREKA";
                }
            }
            else
            {
                // In gameplay scene: start directly in combat with HUD visible
                isMenuOpen = false;
                SetMenuState(false);
                if (deployButton != null)
                {
                    deployButton.SetText("RESUME MISSION");
                }
                if (exitButton != null)
                {
                    exitButton.SetText("MAIN MENU");
                }
                if (pauseTitleText != null)
                {
                    pauseTitleText.text = "PACIFIC RIM: OPERATION PITFALL // TACTICAL PAUSE";
                }
                if (pauseSubtitleText != null)
                {
                    pauseSubtitleText.text = "MISSION DIRECTIVE // DEFEND STRIKER EUREKA";
                }
                if (jaegerHud != null)
                {
                    jaegerHud.ShowBanner("MISSION: SAVE STRIKER EUREKA // ENGAGE LEATHERBACK", 4.0f);
                }
            }

            if (tabBar != null)
            {
                OnTabSelected(tabBar.activeIndex);
            }

            if (pauseSubtitleText == null && menuVisualsRoot != null)
            {
                var t = menuVisualsRoot.transform.Find("Pause_DialogBox/Pause_Subtitle");
                if (t != null) pauseSubtitleText = t.GetComponent<UnityEngine.UI.Text>();
            }

            UpdateBestTimeDisplay();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            Time.timeScale = 1f;
        }

        private void BindListeners()
        {
            if (listenersBound) return;
            listenersBound = true;

            if (deployButton != null)
            {
                deployButton.playSuccessOnClick = true;
                deployButton.onClick = OnDeployClicked;
            }
            if (controlsButton != null)
                controlsButton.onClick = OnControlsClicked;
            if (specsButton != null)
                specsButton.onClick = OnSpecsClicked;
            if (restartButton != null)
                restartButton.onClick = OnRestartClicked;
            if (exitButton != null)
                exitButton.onClick = OnExitClicked;
            if (returnToMenuButton != null)
                returnToMenuButton.onClick = OnReturnToMenuClicked;

            if (tabBar != null)
                tabBar.onTabChanged = OnTabSelected;

            if (featureCards != null)
            {
                for (int i = 0; i < featureCards.Length; i++)
                {
                    int idx = i;
                    if (featureCards[idx] != null)
                    {
                        featureCards[idx].onClick = () => OnCardClicked(idx);
                    }
                }
            }
        }

        private void Update()
        {
            if (isDefeated || isVictorious)
            {
                // In defeat or victory screen, Esc is disabled so player must restart or return to main menu
                if (!IsAnyModalOpen() && WasConfirmPressedThisFrame())
                {
                    OnRestartClicked();
                }
                return;
            }

            if (WasEscapePressedThisFrame())
            {
                if (controlsModal != null && controlsModal.IsOpen)
                {
                    controlsModal.Close();
                    return;
                }
                if (specsModal != null && specsModal.IsOpen)
                {
                    specsModal.Close();
                    return;
                }

                // In dedicated Main Menu scene, Esc closes modals but does NOT close the menu
                if (isMainMenuScene)
                {
                    return;
                }

                PacificRimUISound.Play(isMenuOpen ? PacificRimUISound.SoundType.Back : PacificRimUISound.SoundType.Tab);
                ToggleMenu();
                return;
            }

            if (isMenuOpen && !IsAnyModalOpen() && WasConfirmPressedThisFrame())
            {
                PacificRimUISound.PlaySuccess();
                OnDeployClicked();
            }
        }

        private static bool WasEscapePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    return true;
            }
            catch
            {
                // Ignore if Legacy Input Manager is disabled
            }
            return false;
        }

        private static bool WasConfirmPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            {
                return true;
            }
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    return true;
            }
            catch
            {
                // Ignore if Legacy Input Manager is disabled
            }
            return false;
        }

        private bool IsAnyModalOpen()
        {
            return (controlsModal != null && controlsModal.IsOpen) ||
                   (specsModal != null && specsModal.IsOpen);
        }

        public void ToggleMenu()
        {
            if (isDefeated || isVictorious) return;
            SetMenuState(!isMenuOpen);
        }

        public void SetDefeatState()
        {
            if (isDefeated || isVictorious) return;
            isDefeated = true;
            SetMenuState(true);

            if (deployButton != null)
            {
                deployButton.gameObject.SetActive(false);
            }
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(true);
                restartButton.SetText("RESTART MISSION");
            }
            if (pauseTitleText != null)
            {
                pauseTitleText.text = "MISSION FAILED // CRITICAL BREACH";
                pauseTitleText.color = PacificRimUITheme.Danger;
            }
            if (pauseSubtitleText != null)
            {
                pauseSubtitleText.text = "PACIFIC DEFENSE COMMAND // DRIFT LINK COLLAPSED";
                pauseSubtitleText.color = PacificRimUITheme.Danger;
            }
            if (pauseHealthText != null)
            {
                pauseHealthText.text = "CRITICAL FAILURE // JAEGER DESTROYED";
                pauseHealthText.color = PacificRimUITheme.Danger;
            }
            if (pauseMissionText != null)
            {
                pauseMissionText.text = "GIPSY DANGER COMPROMISED // STRIKER EUREKA LOST TO LEATHERBACK";
            }
        }

        public void SetVictoryState()
        {
            if (isVictorious || isDefeated) return;
            isVictorious = true;
            SetMenuState(true);

            if (deployButton != null)
            {
                deployButton.gameObject.SetActive(false);
            }
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(true);
                restartButton.SetText("REPLAY MISSION");
            }
            if (pauseTitleText != null)
            {
                pauseTitleText.text = "MISSION ACCOMPLISHED // VICTORY";
                pauseTitleText.color = PacificRimUITheme.BlueBright;
            }
            if (pauseSubtitleText != null)
            {
                pauseSubtitleText.text = "PACIFIC DEFENSE COMMAND // LEATHERBACK ELIMINATED // STRIKER EUREKA SAVED";
                pauseSubtitleText.color = PacificRimUITheme.Success;
            }
            if (pauseHealthText != null)
            {
                var jaeger = UnityEngine.Object.FindAnyObjectByType<PacificRimWater.SimpleJaegerController>();
                int hp = jaeger != null ? Mathf.Max(0, Mathf.CeilToInt(jaeger.currentHealth)) : 1000;
                int maxHp = jaeger != null ? Mathf.CeilToInt(jaeger.maxHealth) : 1000;
                pauseHealthText.text = $"CATEGORY IV KAIJU ELIMINATED // JAEGER STANDING ({hp} / {maxHp} HP)";
                pauseHealthText.color = PacificRimUITheme.Success;
            }
            if (pauseMissionText != null)
            {
                float finishTime = JaegerHUD.Instance != null ? JaegerHUD.Instance.CurrentCombatTime : 0f;
                float bestTime = PacificRimCombatRecords.GetBestTime();
                string finishStr = PacificRimCombatRecords.FormatTime(finishTime);
                string bestStr = PacificRimCombatRecords.FormatTime(bestTime);
                pauseMissionText.text = $"MISSION CLEAR: {finishStr}  //  BEST TIME: {bestStr}";
                pauseMissionText.color = PacificRimUITheme.Amber;
            }

            UpdateBestTimeDisplay();

            PacificRimUISound.PlaySuccess();
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHUDCheatActivated();
        }

        public void SetMenuState(bool open)
        {
            isMenuOpen = open;

            // Ensure this GameObject stays active so Update() always processes Esc.
            if (Application.isPlaying && !gameObject.activeSelf)
                gameObject.SetActive(true);

            if (menuVisualsRoot != null)
            {
                menuVisualsRoot.SetActive(open);
            }

            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = open ? 1f : 0f;
                menuCanvasGroup.interactable = open;
                menuCanvasGroup.blocksRaycasts = open;
            }

            if (!open)
            {
                if (controlsModal != null && controlsModal.IsOpen) controlsModal.Close();
                if (specsModal != null && specsModal.IsOpen) specsModal.Close();
            }

            if (hudRoot != null)
            {
                hudRoot.SetActive(!open);
            }

            if (open)
            {
                if (!isDefeated && !isVictorious)
                {
                    if (pauseTitleText != null)
                    {
                        pauseTitleText.text = isMainMenuScene
                            ? "PACIFIC RIM: OPERATION PITFALL"
                            : "PACIFIC RIM: OPERATION PITFALL // TACTICAL PAUSE";
                        pauseTitleText.color = isMainMenuScene ? PacificRimUITheme.BlueBright : PacificRimUITheme.Text;
                    }
                    if (pauseSubtitleText != null)
                    {
                        pauseSubtitleText.text = isMainMenuScene
                            ? "PPDC TACTICAL COMMAND // MISSION: DEFEND STRIKER EUREKA"
                            : "MISSION DIRECTIVE // DEFEND STRIKER EUREKA";
                        pauseSubtitleText.color = PacificRimUITheme.TextDim;
                    }
                }
                UpdateTelemetry();
                UpdateBestTimeDisplay();
            }

            if (Application.isPlaying)
            {
                Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = open;

                if (!isMainMenuScene)
                {
                    Time.timeScale = open ? 0f : 1f;
                }
                else
                {
                    Time.timeScale = 1f;
                }
            }
        }

        private void OnTabSelected(int index)
        {
            if (tabPanels != null && tabPanels.Length > 0)
            {
                for (int i = 0; i < tabPanels.Length; i++)
                {
                    if (tabPanels[i] != null)
                        tabPanels[i].SetActive(i == index);
                }
            }
        }

        private void OnCardClicked(int index)
        {
            if (featureCards != null)
            {
                for (int i = 0; i < featureCards.Length; i++)
                {
                    if (featureCards[i] != null)
                        featureCards[i].SetSelected(i == index);
                }
            }

            // Card 0: Intercept / Deploy
            if (index == 0 && isMainMenuScene)
            {
                OnDeployClicked();
            }
            // Card 1 opens Combat Controls modal, Card 2 opens Jaeger Specs modal
            else if (index == 1)
                OnControlsClicked();
            else if (index == 2)
                OnSpecsClicked();
        }

        private void OnDeployClicked()
        {
            if (isMainMenuScene)
            {
                PacificRimUISound.PlaySuccess();
                Time.timeScale = 1f;
                PacificRimLoadingScreen.LoadScene(gameplaySceneName);
            }
            else
            {
                SetMenuState(false);
                if (jaegerHud != null)
                {
                    jaegerHud.ShowBanner("MISSION RESUMED // ENGAGE", 2.0f);
                }
            }
        }

        private void OnControlsClicked()
        {
            if (specsModal != null && specsModal.IsOpen) specsModal.Close();
            if (controlsModal != null)
                controlsModal.Open();
        }

        private void OnSpecsClicked()
        {
            if (controlsModal != null && controlsModal.IsOpen) controlsModal.Close();
            if (specsModal != null)
                specsModal.Open();
        }

        public void OnRestartClicked()
        {
            PacificRimUISound.Play(PacificRimUISound.SoundType.Click);
            Time.timeScale = 1f;
            PacificRimLoadingScreen.LoadScene(gameplaySceneName);
        }

        public void OnReturnToMenuClicked()
        {
            PacificRimUISound.Play(PacificRimUISound.SoundType.Back);
            Time.timeScale = 1f;
            PacificRimLoadingScreen.LoadScene(mainMenuSceneName);
        }

        private void UpdateTelemetry()
        {
            if (isMainMenuScene || isDefeated || isVictorious) return;

            var jaeger = UnityEngine.Object.FindAnyObjectByType<PacificRimWater.SimpleJaegerController>();
            if (jaeger != null && pauseHealthText != null)
            {
                int hp = Mathf.Max(0, Mathf.CeilToInt(jaeger.currentHealth));
                int maxHp = Mathf.CeilToInt(jaeger.maxHealth);
                float pct = maxHp > 0 ? (float)hp / maxHp : 1f;
                string status = pct > 0.6f ? "SYSTEM OPTIMAL" : (pct > 0.25f ? "ARMOR COMPROMISED" : "CRITICAL HULL INTEGRITY");
                pauseHealthText.text = $"HULL INTEGRITY: {hp} / {maxHp} HP // {status}";
                pauseHealthText.color = pct > 0.6f ? PacificRimUITheme.Success : (pct > 0.25f ? PacificRimUITheme.BlueBright : PacificRimUITheme.Danger);
            }

            if (jaeger != null && pauseMissionText != null)
            {
                pauseMissionText.text = jaeger.inCombatStance
                    ? "MISSION: SAVE STRIKER EUREKA // MELEE COMBAT ENGAGED"
                    : "MISSION: SAVE STRIKER EUREKA // INTERCEPT LEATHERBACK";
            }
        }

        public void UpdateBestTimeDisplay()
        {
            float bestSeconds = PacificRimCombatRecords.GetBestTime();
            string bestStr = PacificRimCombatRecords.FormatTime(bestSeconds);

            if (menuBestTimeText != null)
            {
                menuBestTimeText.text = $"BEST SPEEDRUN: {bestStr}";
                menuBestTimeText.color = PacificRimUITheme.Amber;
            }

            if (menuVisualsRoot != null)
            {
                // Sector status in top tab bar
                var sectorTxt = menuVisualsRoot.transform.Find("Top_TabBar/SectorStatus")?.GetComponent<UnityEngine.UI.Text>();
                if (sectorTxt != null)
                {
                    sectorTxt.text = $"SHATTERDOME // SECTOR 07  [BEST TIME: {bestStr}]";
                }

                // OpRow in Operation tab
                var opRowBest = menuVisualsRoot.transform.Find("SafeContentArea/Left_HeroColumn/TabPages_Container/Page_Operation/OpRow_BestRecord/Value")?.GetComponent<UnityEngine.UI.Text>();
                if (opRowBest != null)
                {
                    opRowBest.text = bestStr;
                    opRowBest.color = PacificRimUITheme.Amber;
                }
            }
        }

        private void OnExitClicked()
        {
            if (!isMainMenuScene)
            {
                // In pause menu, clicking exit returns to main menu scene!
                OnReturnToMenuClicked();
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
