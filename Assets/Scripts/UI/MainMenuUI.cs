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

        public const string Level1SceneName = "Save Striker Eureka";
        public const string Level2SceneName = "Operation Pitfall";
        public const string Level2CutsceneSceneName = "Operation Pitfall Cutscene";

        [Header("Scene Routing")]
        [Tooltip("True if this menu is running in the dedicated Main Menu scene.")]
        public bool isMainMenuScene = false;
        [Tooltip("Scene loaded when clicking DEPLOY or pressing Play.")]
        public string gameplaySceneName = Level1SceneName;
        [Tooltip("Scene loaded when returning to the Main Menu.")]
        public string mainMenuSceneName = "MainMenuScene";
        [Tooltip("0 = Save Striker Eureka, 1 = Operation Pitfall")]
        public int selectedMissionIndex = 0;

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
        [SerializeField] private PacificRimButton audioButton;
        [SerializeField] private PacificRimButton restartButton;
        [SerializeField] private PacificRimButton exitButton;
        [SerializeField] private PacificRimButton returnToMenuButton;

        [Header("Pause Telemetry")]
        [SerializeField] private UnityEngine.UI.Text pauseTitleText;
        [SerializeField] private UnityEngine.UI.Text pauseSubtitleText;
        [SerializeField] private UnityEngine.UI.Text pauseHealthText;
        [SerializeField] private UnityEngine.UI.Text pauseMissionText;
        [SerializeField] private UnityEngine.UI.Text menuBestTimeText;

        [Header("Main Menu Operation Briefing")]
        [SerializeField] private UnityEngine.UI.Text menuMainSubtitleText;
        [SerializeField] private UnityEngine.UI.Text briefingText;
        [SerializeField] private UnityEngine.UI.Text opRowTitleValue;
        [SerializeField] private UnityEngine.UI.Text opRowThreatValue;
        [SerializeField] private UnityEngine.UI.Text opRowBestValue;

        [Header("Modals")]
        [SerializeField] private PacificRimModal controlsModal;
        [SerializeField] private PacificRimModal specsModal;
        [SerializeField] private PacificRimModal audioModal;

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
            else
            {
                isMainMenuScene = false;
                if (!string.IsNullOrEmpty(currentScene))
                {
                    gameplaySceneName = currentScene;
                }
            }

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
        }

        private void Start()
        {
            BindListeners();
            PacificRimAudioSettingsUI.AttachToMenu(menuVisualsRoot);

            if (isMainMenuScene)
            {
                PacificRimLoadingScreen.WarmupMainMenuAssets();

                isMenuOpen = true;
                SetMenuState(true);
                if (exitButton != null)
                {
                    exitButton.SetText("EXIT");
                }
                SelectMission(selectedMissionIndex, playSound: false);
            }
            else
            {
                bool isPitfall = IsOperationPitfallActive();
                isMenuOpen = false;
                SetMenuState(false);
                if (deployButton != null)
                {
                    deployButton.SetText("RESUME");
                }
                if (exitButton != null)
                {
                    exitButton.SetText("MAIN MENU");
                }
                if (pauseTitleText != null)
                {
                    pauseTitleText.text = "PAUSED";
                }
                if (pauseSubtitleText != null)
                {
                    pauseSubtitleText.text = isPitfall
                        ? "OP 02  ·  OPERATION PITFALL"
                        : "OP 01  ·  SAVE STRIKER EUREKA";
                }
                if (jaegerHud != null)
                {
                    jaegerHud.ShowBanner(
                        isPitfall
                            ? "OPERATION PITFALL // ENGAGE SLATTERN"
                            : "SAVE STRIKER EUREKA // ENGAGE LEATHERBACK",
                        3.5f);
                }
            }

            if (tabBar != null)
            {
                OnTabSelected(tabBar.activeIndex);
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
            if (audioButton != null)
                audioButton.onClick = OnAudioClicked;
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
                if (!IsAnyModalOpen() && WasConfirmPressedThisFrame())
                {
                    OnRestartClicked();
                }
                return;
            }

            if (WasEscapePressedThisFrame())
            {
                if (PacificRimAdminUI.IsOpen)
                {
                    PacificRimAdminUI.Close();
                    PacificRimUISound.PlayBack();
                    return;
                }
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
                if (audioModal != null && audioModal.IsOpen)
                {
                    audioModal.Close();
                    return;
                }

                if (isMainMenuScene)
                {
                    if (tabBar != null && tabBar.activeIndex != 0)
                    {
                        tabBar.SelectTab(0, silent: false);
                    }
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
            }
            return false;
        }

        private bool IsAnyModalOpen()
        {
            return (controlsModal != null && controlsModal.IsOpen) ||
                   (specsModal != null && specsModal.IsOpen) ||
                   (audioModal != null && audioModal.IsOpen);
        }

        public void ToggleMenu()
        {
            if (isDefeated || isVictorious) return;
            SetMenuState(!isMenuOpen);
        }

        private bool IsOperationPitfallActive()
        {
            if (isMainMenuScene)
                return selectedMissionIndex == 1;
            string sceneName = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(sceneName) && sceneName.IndexOf("Operation Pitfall", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            var kaiju = PacificRimWater.KaijuAI.Instance;
            return kaiju != null && kaiju.isSlattern;
        }

        public void SetDefeatState()
        {
            if (isDefeated || isVictorious) return;
            isDefeated = true;
            SetMenuState(true);
            bool isPitfall = IsOperationPitfallActive();

            if (deployButton != null)
            {
                deployButton.gameObject.SetActive(false);
            }
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(true);
                restartButton.SetText("RESTART");
            }
            if (pauseTitleText != null)
            {
                pauseTitleText.text = "DEFEAT";
                pauseTitleText.color = PacificRimUITheme.Danger;
            }
            if (pauseSubtitleText != null)
            {
                pauseSubtitleText.text = isPitfall ? "OP 02  ·  OPERATION PITFALL" : "OP 01  ·  SAVE STRIKER EUREKA";
                pauseSubtitleText.color = PacificRimUITheme.TextDim;
            }
            if (pauseHealthText != null)
            {
                pauseHealthText.text = "HULL DESTROYED  //  DRIFT LOST";
                pauseHealthText.color = PacificRimUITheme.Danger;
            }
            if (pauseMissionText != null)
            {
                pauseMissionText.text = isPitfall ? "LOST TO SLATTERN" : "STRIKER EUREKA LOST";
                pauseMissionText.color = PacificRimUITheme.TextDim;
            }
        }

        public void SetVictoryState()
        {
            if (isVictorious || isDefeated) return;
            isVictorious = true;
            SetMenuState(true);
            bool isPitfall = IsOperationPitfallActive();

            if (deployButton != null)
            {
                deployButton.gameObject.SetActive(false);
            }
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(true);
                restartButton.SetText("REPLAY");
            }
            if (pauseTitleText != null)
            {
                pauseTitleText.text = "VICTORY";
                pauseTitleText.color = PacificRimUITheme.BlueBright;
            }
            if (pauseSubtitleText != null)
            {
                pauseSubtitleText.text = isPitfall
                    ? "SLATTERN NEUTRALIZED  ·  BREACH SECURED"
                    : "LEATHERBACK NEUTRALIZED  ·  STRIKER SAVED";
                pauseSubtitleText.color = PacificRimUITheme.Success;
            }
            if (pauseHealthText != null)
            {
                var jaeger = Object.FindAnyObjectByType<PacificRimWater.SimpleJaegerController>();
                int hp = jaeger != null ? Mathf.Max(0, Mathf.CeilToInt(jaeger.currentHealth)) : 1000;
                int maxHp = jaeger != null ? Mathf.CeilToInt(jaeger.maxHealth) : 1000;
                pauseHealthText.text = $"HULL {hp} / {maxHp}  ·  JAEGER INTACT";
                pauseHealthText.color = PacificRimUITheme.Success;
            }
            if (pauseMissionText != null)
            {
                float finishTime = JaegerHUD.Instance != null ? JaegerHUD.Instance.CurrentCombatTime : 0f;
                float bestTime = PacificRimCombatRecords.GetBestTime();
                string finishStr = PacificRimCombatRecords.FormatTime(finishTime);
                string bestStr = PacificRimCombatRecords.FormatTime(bestTime);
                pauseMissionText.text = $"TIME {finishStr}  ·  BEST {bestStr}";
                pauseMissionText.color = PacificRimUITheme.Amber;
            }

            UpdateBestTimeDisplay();

            PacificRimUISound.PlaySuccess();
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHUDCheatActivated();
        }

        public void SetMenuState(bool open)
        {
            isMenuOpen = open;

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
                if (audioModal != null && audioModal.IsOpen) audioModal.Close();
            }

            if (hudRoot != null)
            {
                hudRoot.SetActive(!open);
            }

            if (open)
            {
                if (!isDefeated && !isVictorious)
                {
                    bool isPitfall = IsOperationPitfallActive();
                    if (pauseTitleText != null)
                    {
                        pauseTitleText.text = isMainMenuScene ? "PACIFIC RIM" : "PAUSED";
                        pauseTitleText.color = isMainMenuScene ? PacificRimUITheme.Text : PacificRimUITheme.BlueBright;
                    }
                    if (pauseSubtitleText != null)
                    {
                        pauseSubtitleText.text = isMainMenuScene
                            ? (selectedMissionIndex == 1 ? "02 · OPERATION PITFALL" : "01 · SAVE STRIKER EUREKA")
                            : (isPitfall ? "OP 02  ·  OPERATION PITFALL" : "OP 01  ·  SAVE STRIKER EUREKA");
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
            if (isMainMenuScene)
            {
                if (index == 0 || index == 1)
                {
                    bool alreadySelected = selectedMissionIndex == index;
                    SelectMission(index, playSound: true);
                    if (alreadySelected)
                    {
                        OnDeployClicked();
                    }
                    else if (tabBar != null && tabBar.activeIndex != 0)
                    {
                        tabBar.SelectTab(0, silent: false);
                    }
                    return;
                }

                if (index == 2)
                {
                    OnControlsClicked();
                    return;
                }
                if (index == 3)
                {
                    OnSpecsClicked();
                    return;
                }
            }

            if (featureCards != null)
            {
                for (int i = 0; i < featureCards.Length; i++)
                {
                    if (featureCards[i] != null)
                        featureCards[i].SetSelected(i == index);
                }
            }

            if (index == 1)
                OnControlsClicked();
            else if (index == 2)
                OnSpecsClicked();
        }

        public void SelectMission(int missionIndex, bool playSound = true)
        {
            selectedMissionIndex = Mathf.Clamp(missionIndex, 0, 1);
            gameplaySceneName = selectedMissionIndex == 1 ? Level2SceneName : Level1SceneName;

            if (playSound)
            {
                PacificRimUISound.Play(PacificRimUISound.SoundType.Tab);
            }

            if (featureCards != null)
            {
                for (int i = 0; i < featureCards.Length; i++)
                {
                    if (featureCards[i] == null) continue;
                    bool isSelectedMission = (i == selectedMissionIndex);
                    featureCards[i].SetSelected(isSelectedMission);
                    if (i == 0 || i == 1)
                    {
                        featureCards[i].SetBadge(
                            isSelectedMission ? "ACTIVE" : "SELECT",
                            isSelectedMission ? PacificRimUITheme.Success : PacificRimUITheme.TextDim);
                    }
                }
            }

            if (deployButton != null)
            {
                deployButton.SetText(selectedMissionIndex == 1 ? "DEPLOY  ·  OP 02" : "DEPLOY  ·  OP 01");
            }

            ResolveMainMenuTextReferences();

            if (menuMainSubtitleText != null)
            {
                menuMainSubtitleText.text = selectedMissionIndex == 1
                    ? "02  ·  OPERATION PITFALL"
                    : "01  ·  SAVE STRIKER EUREKA";
                menuMainSubtitleText.color = selectedMissionIndex == 1 ? PacificRimUITheme.BlueBright : PacificRimUITheme.Amber;
            }

            if (briefingText != null)
            {
                briefingText.text = selectedMissionIndex == 1
                    ? "Descend to the Mariana Trench Breach and eliminate Category V Slattern."
                    : "Protect paralyzed Striker Eureka from Category IV Leatherback in Hong Kong Bay.";
            }

            if (opRowTitleValue != null)
            {
                opRowTitleValue.text = selectedMissionIndex == 1
                    ? "MARIANA TRENCH"
                    : "HONG KONG BAY";
            }

            if (opRowThreatValue != null)
            {
                opRowThreatValue.text = selectedMissionIndex == 1
                    ? "CAT V SLATTERN"
                    : "CAT IV LEATHERBACK";
            }

            UpdateBestTimeDisplay();
        }

        private void ResolveMainMenuTextReferences()
        {
            if (menuVisualsRoot == null) return;

            if (menuMainSubtitleText == null)
                menuMainSubtitleText = menuVisualsRoot.transform.Find("SafeContentArea/Left_HeroColumn/SubTitle")?.GetComponent<UnityEngine.UI.Text>();
            if (briefingText == null)
                briefingText = menuVisualsRoot.transform.Find("SafeContentArea/Left_HeroColumn/TabPages_Container/Page_Operation/BriefingText")?.GetComponent<UnityEngine.UI.Text>();
            if (opRowTitleValue == null)
                opRowTitleValue = menuVisualsRoot.transform.Find("SafeContentArea/Left_HeroColumn/TabPages_Container/Page_Operation/OpRow_Weather/RightValue")?.GetComponent<UnityEngine.UI.Text>();
            if (opRowThreatValue == null)
                opRowThreatValue = menuVisualsRoot.transform.Find("SafeContentArea/Left_HeroColumn/TabPages_Container/Page_Operation/OpRow_Chassis/RightValue")?.GetComponent<UnityEngine.UI.Text>();
            if (opRowBestValue == null)
                opRowBestValue = menuVisualsRoot.transform.Find("SafeContentArea/Left_HeroColumn/TabPages_Container/Page_Operation/OpRow_BestRecord/RightValue")?.GetComponent<UnityEngine.UI.Text>();
        }

        private void OnDeployClicked()
        {
            if (isMainMenuScene)
            {
                PacificRimUISound.PlaySuccess();
                Time.timeScale = 1f;
                string targetScene = selectedMissionIndex == 1 ? Level2CutsceneSceneName : gameplaySceneName;
                PacificRimLoadingScreen.LoadScene(targetScene);
            }
            else
            {
                SetMenuState(false);
                if (jaegerHud != null)
                {
                    jaegerHud.ShowBanner("RESUMED", 1.8f);
                }
            }
        }

        private void OnControlsClicked()
        {
            if (isMainMenuScene && tabBar != null && tabPanels != null && tabPanels.Length >= 2)
            {
                tabBar.SelectTab(1, silent: false);
                return;
            }

            if (specsModal != null && specsModal.IsOpen) specsModal.Close();
            if (audioModal != null && audioModal.IsOpen) audioModal.Close();
            if (controlsModal != null)
                controlsModal.Open();
        }

        private void OnSpecsClicked()
        {
            if (isMainMenuScene && tabBar != null && tabPanels != null && tabPanels.Length >= 3)
            {
                tabBar.SelectTab(2, silent: false);
                return;
            }

            if (controlsModal != null && controlsModal.IsOpen) controlsModal.Close();
            if (audioModal != null && audioModal.IsOpen) audioModal.Close();
            if (specsModal != null)
                specsModal.Open();
        }

        private void OnAudioClicked()
        {
            if (isMainMenuScene && tabBar != null && tabPanels != null && tabPanels.Length >= 4)
            {
                tabBar.SelectTab(3, silent: false);
                return;
            }

            if (controlsModal != null && controlsModal.IsOpen) controlsModal.Close();
            if (specsModal != null && specsModal.IsOpen) specsModal.Close();
            if (audioModal != null)
            {
                audioModal.Open();
                audioModal.GetComponentInChildren<PacificRimAudioSettingsUI>(true)?.RefreshSliderValues();
            }
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

            var jaeger = Object.FindAnyObjectByType<PacificRimWater.SimpleJaegerController>();
            if (jaeger != null && pauseHealthText != null)
            {
                int hp = Mathf.Max(0, Mathf.CeilToInt(jaeger.currentHealth));
                int maxHp = Mathf.CeilToInt(jaeger.maxHealth);
                float pct = maxHp > 0 ? (float)hp / maxHp : 1f;
                string status = pct > 0.6f ? "OPTIMAL" : (pct > 0.25f ? "DAMAGED" : "CRITICAL");
                pauseHealthText.text = $"HULL {hp} / {maxHp}  ·  {status}";
                pauseHealthText.color = pct > 0.6f ? PacificRimUITheme.Success : (pct > 0.25f ? PacificRimUITheme.BlueBright : PacificRimUITheme.Danger);
            }

            if (pauseMissionText != null)
            {
                float curTime = JaegerHUD.Instance != null ? JaegerHUD.Instance.CurrentCombatTime : 0f;
                float bestTime = PacificRimCombatRecords.GetBestTime();
                pauseMissionText.text = $"TIME {PacificRimCombatRecords.FormatTime(curTime)}  ·  BEST {PacificRimCombatRecords.FormatTime(bestTime)}";
                pauseMissionText.color = PacificRimUITheme.TextDim;
            }
        }

        public void UpdateBestTimeDisplay()
        {
            string targetLevel = isMainMenuScene ? gameplaySceneName : SceneManager.GetActiveScene().name;
            float bestSeconds = PacificRimCombatRecords.GetBestTime(targetLevel);
            string bestStr = PacificRimCombatRecords.FormatTime(bestSeconds);

            if (menuBestTimeText != null)
            {
                string sectorLabel = selectedMissionIndex == 1 ? "SECTOR 09" : "SECTOR 04";
                menuBestTimeText.text = $"{sectorLabel}  ·  BEST {bestStr}";
                menuBestTimeText.color = PacificRimUITheme.Amber;
            }

            if (opRowBestValue != null)
            {
                opRowBestValue.text = bestStr;
                opRowBestValue.color = PacificRimUITheme.Amber;
            }

            if (menuVisualsRoot != null)
            {
                var sectorTxt = menuVisualsRoot.transform.Find("Top_TabBar/SectorStatus")?.GetComponent<UnityEngine.UI.Text>();
                if (sectorTxt != null)
                {
                    string sectorLabel = selectedMissionIndex == 1 ? "SECTOR 09" : "SECTOR 04";
                    sectorTxt.text = $"{sectorLabel}  ·  BEST {bestStr}";
                }

                var opRowBest = menuVisualsRoot.transform.Find("SafeContentArea/Left_HeroColumn/TabPages_Container/Page_Operation/OpRow_BestRecord/RightValue")?.GetComponent<UnityEngine.UI.Text>();
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
