using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace PacificRimUI.Editor
{
    public static class SetupPacificRimUIScene
    {
        private struct ControlEntry
        {
            public string Key;
            public string Action;
            public string Note;
            public bool Highlight;

            public ControlEntry(string key, string action, string note, bool highlight = false)
            {
                Key = key;
                Action = action;
                Note = note;
                Highlight = highlight;
            }
        }

        private static readonly ControlEntry[] MovementAndMeleeControls =
        {
            new ControlEntry("W A S D", "MOVE / STRAFE", "Omnidirectional step"),
            new ControlEntry("SHIFT", "SPRINT", "Toggle run gait"),
            new ControlEntry("2x WASD", "THRUSTER DASH", "Burst evade · 20 STM", true),
            new ControlEntry("LMB", "LIGHT STRIKE", "3-hit combo chain"),
            new ControlEntry("RMB", "HEAVY SLAM", "Breaks Kaiju guard"),
            new ControlEntry("SHIFT+RMB", "ROCKET LUNGE", "Uninterruptible heavy", true),
        };

        private static readonly ControlEntry[] DefenseAndSystemControls =
        {
            new ControlEntry("HOLD F", "BLOCK", "Mitigates 80% DMG"),
            new ControlEntry("TAP F", "PERFECT PARRY", "0 DMG · +15% STM · Stagger", true),
            new ControlEntry("R", "CHAIN SWORDS", "+Damage & reach"),
            new ControlEntry("T", "PLASMACASTER", "Ultimate · Charge in combat", true),
            new ControlEntry("Q", "OVERCHARGE", "+30% Speed / +25% DMG · 15s", true),
            new ControlEntry("E", "FREECAM", "Inspect · Scroll to zoom"),
            new ControlEntry("~ / ESC", "HUD / PAUSE", "Hide HUD · Pause menu"),
        };

        [MenuItem("Pacific Rim/Setup Pacific Rim UI (HUD & Main Menu)")]
        public static void SetupUI()
        {
            EnsureEventSystem();
            EnsureSoundManager();
            Canvas canvas = EnsureCanvas();
            CleanupOldRoots(canvas.transform);

            // Clean up any leftover letterbox in scenes without the aerial deployment cutscene
            var deploymentCutscene = UnityEngine.Object.FindAnyObjectByType<PacificRimCutscene.PacificRimDeploymentCutscene>();
            if (deploymentCutscene == null)
            {
                CleanupNamedChild(canvas.transform, "Cinematic_Letterbox");
                CleanupNamedChild(canvas.transform, "Cutscene_FadeOverlay");
            }
            else
            {
                var skipText = canvas.transform.Find("Cinematic_Letterbox/BottomBar/SkipPrompt")?.GetComponent<Text>();
                if (skipText != null)
                {
                    skipText.supportRichText = true;
                    skipText.font = PacificRimUITheme.GetDefaultFont();
                    skipText.fontSize = 12;
                    skipText.fontStyle = FontStyle.Bold;
                    skipText.color = new Color(PacificRimUITheme.TextDim.r, PacificRimUITheme.TextDim.g, PacificRimUITheme.TextDim.b, 0.72f);
                    skipText.text = "<color=#46c3ff>[SPACE]</color>  SKIP";
                }
            }

            GameObject hudGo = BuildJaegerHUD(canvas.transform);
            var hud = hudGo.GetComponent<JaegerHUD>();

            GameObject menuGo = BuildPauseMenu(canvas.transform, hudGo, hud);
            var mainMenu = menuGo.GetComponent<MainMenuUI>();

            var jaeger = UnityEngine.Object.FindAnyObjectByType<PacificRimWater.SimpleJaegerController>();
            if (jaeger != null)
            {
                SetSerializedField(hud, "jaeger", jaeger);
            }
            var kaiju = UnityEngine.Object.FindAnyObjectByType<PacificRimWater.KaijuAI>();
            if (kaiju != null)
            {
                SetSerializedField(hud, "kaiju", kaiju);
            }

            string activeSceneName = SceneManager.GetActiveScene().name;
            mainMenu.isMainMenuScene = false;
            mainMenu.gameplaySceneName = string.IsNullOrEmpty(activeSceneName) ? MainMenuUI.Level1SceneName : activeSceneName;
            mainMenu.mainMenuSceneName = "MainMenuScene";
            mainMenu.isMenuOpen = false;
            mainMenu.SetMenuState(false);

            EditorUtility.SetDirty(canvas.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log("[PacificRimUI] Minimalist Jaeger HUD & Pause Menu built and wired.");
        }

        public static void EnsureEventSystem()
        {
            var es = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                es = esGo.AddComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
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

        public static void EnsureSoundManager()
        {
            var sound = UnityEngine.Object.FindAnyObjectByType<PacificRimUISound>();
            if (sound == null)
            {
                var go = new GameObject("PacificRim_UISoundManager", typeof(AudioSource), typeof(PacificRimUISound));
                sound = go.GetComponent<PacificRimUISound>();
            }

            sound.hoverClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Casual Game Sounds U6/CasualGameSounds/DM-CGS-01.wav");
            sound.clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Casual Game Sounds U6/CasualGameSounds/DM-CGS-03.wav");
            sound.tabClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Casual Game Sounds U6/CasualGameSounds/DM-CGS-03.wav");
            sound.denyClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free Pack/Laser Impact Light_6.wav");
            sound.successClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/Hydraulic piston mechanical whine.mp3");
            sound.backClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Casual Game Sounds U6/CasualGameSounds/DM-CGS-03.wav");
            sound.commClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free Pack/Laser Impact Light_6.wav");
            sound.masterVolume = 0.85f;

            var src = sound.GetComponent<AudioSource>();
            if (src != null)
            {
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.ignoreListenerPause = true;
            }
            EditorUtility.SetDirty(sound);
            EditorUtility.SetDirty(sound.gameObject);
        }

        public static Canvas EnsureCanvas()
        {
            var existingGo = GameObject.Find("PacificRim_UICanvas");
            Canvas canvas = existingGo != null ? existingGo.GetComponent<Canvas>() : null;
            if (canvas == null)
            {
                var go = new GameObject("PacificRim_UICanvas");
                canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                go.AddComponent<GraphicRaycaster>();
            }
            return canvas;
        }

        public static void CleanupOldRoots(Transform parent)
        {
            CleanupNamedChild(parent, "JaegerHUD_Root");
            CleanupNamedChild(parent, "MainMenu_Root");
        }

        private static void CleanupNamedChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            while (t != null)
            {
                UnityEngine.Object.DestroyImmediate(t.gameObject);
                t = parent.Find(name);
            }
        }

        // =====================================================================
        // MINIMALIST IN-GAME JAEGER HUD
        // =====================================================================

        private static GameObject BuildJaegerHUD(Transform parent)
        {
            var root = CreateRect("JaegerHUD_Root", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hud = root.AddComponent<JaegerHUD>();

            // 1. Peripheral Overcharge & Impact Edge Vignettes
            var cueGo = CreateRect("Overcharge_ScreenCue", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var cueCg = cueGo.AddComponent<CanvasGroup>();
            cueCg.alpha = 0f;
            cueCg.interactable = false;
            cueCg.blocksRaycasts = false;

            Color cueEdge = new Color(PacificRimUITheme.BlueBright.r, PacificRimUITheme.BlueBright.g, PacificRimUITheme.BlueBright.b, 0.34f);
            BuildGradientEdge(cueGo.transform, "CueTop", new Vector2(0f, 0.88f), new Vector2(1f, 1f), cueEdge, Color.clear);
            BuildGradientEdge(cueGo.transform, "CueBottom", new Vector2(0f, 0f), new Vector2(1f, 0.12f), Color.clear, cueEdge);
            BuildGradientEdgeHorizontal(cueGo.transform, "CueLeft", new Vector2(0f, 0f), new Vector2(0.08f, 1f), cueEdge, Color.clear);
            BuildGradientEdgeHorizontal(cueGo.transform, "CueRight", new Vector2(0.92f, 0f), new Vector2(1f, 1f), Color.clear, cueEdge);

            var impactGo = CreateRect("Impact_ScreenCue", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var impactCg = impactGo.AddComponent<CanvasGroup>();
            impactCg.alpha = 0f;
            impactCg.blocksRaycasts = false;
            impactCg.interactable = false;
            Color impactEdge = new Color(PacificRimUITheme.Danger.r, PacificRimUITheme.Danger.g, PacificRimUITheme.Danger.b, 0.48f);
            BuildGradientEdge(impactGo.transform, "ImpactTop", new Vector2(0f, 0.84f), new Vector2(1f, 1f), impactEdge, Color.clear);
            BuildGradientEdge(impactGo.transform, "ImpactBottom", new Vector2(0f, 0f), new Vector2(1f, 0.16f), Color.clear, impactEdge);
            BuildGradientEdgeHorizontal(impactGo.transform, "ImpactLeft", new Vector2(0f, 0f), new Vector2(0.11f, 1f), impactEdge, Color.clear);
            BuildGradientEdgeHorizontal(impactGo.transform, "ImpactRight", new Vector2(0.89f, 0f), new Vector2(1f, 1f), Color.clear, impactEdge);
            var impactEdges = impactGo.GetComponentsInChildren<UIGradientGraphic>(true);

            // 2. Delicate Center Tactical Reticle
            var reticleGo = CreateRect("HoloReticle", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
            var reticle = reticleGo.AddComponent<UIHoloReticleGraphic>();
            reticle.ringThickness = 1.25f;
            reticle.color = new Color(PacificRimUITheme.TextDim.r, PacificRimUITheme.TextDim.g, PacificRimUITheme.TextDim.b, 0.24f);
            reticle.raycastTarget = false;

            // 3. Top-Left Minimal Unit Identifier
            var topLeft = CreateRect("TopLeft_ID", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -26f), new Vector2(280f, 24f));
            SetPivot(topLeft, 0f, 1f);
            var chassisTxt = CreateStyledText("ChassisLabel", topLeft.transform, PacificRimTextStyle.Caption,
                "MARK-III  ·  GIPSY DANGER", PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 11, 0, true);
            Stretch(chassisTxt.rectTransform);

            // 4. Top-Right Clean Combat Timer + Best Record
            var topRight = CreateRect("TopRight_Sync", root.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -24f), new Vector2(260f, 28f));
            SetPivot(topRight, 1f, 1f);

            var timerRow = CreateRect("TopRight_TimerRow", topRight.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Stretch(timerRow.GetComponent<RectTransform>());

            var bestTxt = CreateStyledText("BestTimeText", timerRow.transform, PacificRimTextStyle.Caption,
                $"BEST  {PacificRimCombatRecords.FormatTime(PacificRimCombatRecords.GetBestTime())}", PacificRimUITheme.Amber, TextAnchor.MiddleRight, 11, 0, true);
            bestTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
            bestTxt.rectTransform.anchorMax = new Vector2(0.58f, 1f);
            bestTxt.rectTransform.offsetMin = Vector2.zero;
            bestTxt.rectTransform.offsetMax = new Vector2(-12f, 0f);

            var timerTxt = CreateStyledText("CombatTimerText", timerRow.transform, PacificRimTextStyle.Number,
                "00:00", PacificRimUITheme.BlueBright, TextAnchor.MiddleRight, 17, 0, false);
            timerTxt.rectTransform.anchorMin = new Vector2(0.58f, 0f);
            timerTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            timerTxt.rectTransform.offsetMin = Vector2.zero;
            timerTxt.rectTransform.offsetMax = Vector2.zero;

            // 5. Top-Center Slim Kaiju Boss Panel (620x58)
            var bossPanel = CreateRect("TopCenter_BossPanel", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(620f, 58f));
            SetPivot(bossPanel, 0.5f, 1f);
            CreateChamferPanel(bossPanel.transform, "BossPanelFrame", 8f, PacificRimUITheme.Panel, new Color(PacificRimUITheme.Danger.r, PacificRimUITheme.Danger.g, PacificRimUITheme.Danger.b, 0.55f)).raycastTarget = false;
            CreateAccentBar(bossPanel.transform, "BossLeftAccent", true, 3f, PacificRimUITheme.Danger);

            bool isPitfall = SceneManager.GetActiveScene().name.IndexOf("Operation Pitfall", StringComparison.OrdinalIgnoreCase) >= 0;
            var activeKaiju = UnityEngine.Object.FindAnyObjectByType<PacificRimWater.KaijuAI>();
            string bossHeader = activeKaiju != null
                ? $"{activeKaiju.kaijuDisplayName}  <color=#8C96A6><size=10>{activeKaiju.kaijuCategoryLabel.Replace("CATEGORY ", "CAT ").Replace(" KAIJU", string.Empty)}</size></color>"
                : (isPitfall ? "SLATTERN  <color=#8C96A6><size=10>CAT V</size></color>" : "LEATHERBACK  <color=#8C96A6><size=10>CAT IV</size></color>");

            var bossNameTxt = CreateStyledText("BossNameText", bossPanel.transform, PacificRimTextStyle.Caption,
                bossHeader, PacificRimUITheme.Text, TextAnchor.MiddleLeft, 13, 0, false);
            bossNameTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
            bossNameTxt.rectTransform.anchorMax = new Vector2(0.65f, 1f);
            bossNameTxt.rectTransform.pivot = new Vector2(0f, 1f);
            bossNameTxt.rectTransform.offsetMin = new Vector2(14f, -24f);
            bossNameTxt.rectTransform.offsetMax = new Vector2(0f, -5f);

            var bossHpNumTxt = CreateStyledText("BossHpNumberText", bossPanel.transform, PacificRimTextStyle.Number,
                "4,200 / 4,200", PacificRimUITheme.Text, TextAnchor.MiddleRight, 12, 0, false);
            bossHpNumTxt.rectTransform.anchorMin = new Vector2(0.65f, 1f);
            bossHpNumTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            bossHpNumTxt.rectTransform.pivot = new Vector2(1f, 1f);
            bossHpNumTxt.rectTransform.offsetMin = new Vector2(0f, -24f);
            bossHpNumTxt.rectTransform.offsetMax = new Vector2(-14f, -5f);

            CreateProgressBar("BossHealthBar", bossPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(14f, -27f), new Vector2(-28f, 7f), PacificRimUITheme.Danger, out var bossHpBar);

            CreateProgressBar("BossSubBar", bossPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(14f, -37f), new Vector2(-28f, 3f), PacificRimUITheme.Amber, out var bossSubBar);

            var bossPhaseTxt = CreateStyledText("BossPhaseText", bossPanel.transform, PacificRimTextStyle.Caption,
                "PHASE I", PacificRimUITheme.BlueBright, TextAnchor.MiddleLeft, 10, 0, true);
            bossPhaseTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
            bossPhaseTxt.rectTransform.anchorMax = new Vector2(0.45f, 0f);
            bossPhaseTxt.rectTransform.pivot = new Vector2(0f, 0f);
            bossPhaseTxt.rectTransform.offsetMin = new Vector2(14f, 4f);
            bossPhaseTxt.rectTransform.offsetMax = new Vector2(0f, 17f);

            var bossStatusTxt = CreateStyledText("BossStatusText", bossPanel.transform, PacificRimTextStyle.Caption,
                "ENGAGING", PacificRimUITheme.Amber, TextAnchor.MiddleRight, 10, 0, true);
            bossStatusTxt.rectTransform.anchorMin = new Vector2(0.45f, 0f);
            bossStatusTxt.rectTransform.anchorMax = new Vector2(1f, 0f);
            bossStatusTxt.rectTransform.pivot = new Vector2(1f, 0f);
            bossStatusTxt.rectTransform.offsetMin = new Vector2(0f, 4f);
            bossStatusTxt.rectTransform.offsetMax = new Vector2(-14f, 17f);

            // 6. Top-Center Compact Notification Banner
            var bannerGo = CreateRect("Notification_Banner", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(480f, 32f));
            SetPivot(bannerGo, 0.5f, 1f);
            var bannerCg = bannerGo.AddComponent<CanvasGroup>();
            bannerCg.alpha = 0f;
            bannerCg.interactable = false;
            bannerCg.blocksRaycasts = false;

            var bannerChamfer = CreateChamferPanel(bannerGo.transform, "BannerBg", 6f, PacificRimUITheme.PanelOpaque, PacificRimUITheme.Blue);
            bannerChamfer.raycastTarget = false;
            CreateAccentBar(bannerGo.transform, "BannerAccent", true, 3f, PacificRimUITheme.BlueBright);
            var bannerTxt = CreateStyledText("BannerText", bannerGo.transform, PacificRimTextStyle.Caption,
                "SYSTEMS ONLINE", PacificRimUITheme.Text, TextAnchor.MiddleCenter, 11, 0, true);
            Stretch(bannerTxt.rectTransform, new Vector2(14f, 0f), new Vector2(-14f, 0f));

            // 7. Bottom-Left Compact Hull & Stamina Panel (310x102) + Docked Overcharge Bar
            var blPanel = CreateRect("BottomLeft_HullPanel", root.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 62f), new Vector2(310f, 102f));
            SetPivot(blPanel, 0f, 0f);
            CreateChamferPanel(blPanel.transform, "PanelFrame", 10f, PacificRimUITheme.Panel, PacificRimUITheme.Line).raycastTarget = false;
            CreateAccentBar(blPanel.transform, "LeftAccent", true, 3f, PacificRimUITheme.Blue);

            var hullCaptionTxt = CreateStyledText("HullCaption", blPanel.transform, PacificRimTextStyle.Caption,
                "HULL", PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 11, 0, true);
            hullCaptionTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
            hullCaptionTxt.rectTransform.anchorMax = new Vector2(0.45f, 1f);
            hullCaptionTxt.rectTransform.pivot = new Vector2(0f, 1f);
            hullCaptionTxt.rectTransform.offsetMin = new Vector2(14f, -34f);
            hullCaptionTxt.rectTransform.offsetMax = new Vector2(0f, -10f);

            var hullNumTxt = CreateStyledText("NumberText", blPanel.transform, PacificRimTextStyle.Number,
                "1,000", PacificRimUITheme.Health, TextAnchor.MiddleRight, 24, 0, false);
            hullNumTxt.rectTransform.anchorMin = new Vector2(0.45f, 1f);
            hullNumTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            hullNumTxt.rectTransform.pivot = new Vector2(1f, 1f);
            hullNumTxt.rectTransform.offsetMin = new Vector2(0f, -36f);
            hullNumTxt.rectTransform.offsetMax = new Vector2(-14f, -8f);

            CreateProgressBar("HullBar", blPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(14f, -40f), new Vector2(-28f, 8f), PacificRimUITheme.Blue, out var hullBar);

            var staminaCaptionTxt = CreateStyledText("StaminaCaptionText", blPanel.transform, PacificRimTextStyle.Caption,
                "STAMINA", PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 10, 0, true);
            staminaCaptionTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
            staminaCaptionTxt.rectTransform.anchorMax = new Vector2(0.68f, 1f);
            staminaCaptionTxt.rectTransform.pivot = new Vector2(0f, 1f);
            staminaCaptionTxt.rectTransform.offsetMin = new Vector2(14f, -76f);
            staminaCaptionTxt.rectTransform.offsetMax = new Vector2(0f, -60f);

            var staminaValueTxt = CreateStyledText("StaminaValueText", blPanel.transform, PacificRimTextStyle.Number,
                "100%", PacificRimUITheme.Text, TextAnchor.MiddleRight, 11, 0, false);
            staminaValueTxt.rectTransform.anchorMin = new Vector2(0.68f, 1f);
            staminaValueTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            staminaValueTxt.rectTransform.pivot = new Vector2(1f, 1f);
            staminaValueTxt.rectTransform.offsetMin = new Vector2(0f, -76f);
            staminaValueTxt.rectTransform.offsetMax = new Vector2(-14f, -60f);

            CreateProgressBar("StaminaBar", blPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(14f, -80f), new Vector2(-28f, 6f), new Color(0.28f, 0.88f, 0.96f, 1f), out var staminaBar);

            // Docked Overcharge Bar cleanly floating right above BottomLeft_HullPanel when active
            var overchargeGo = CreateRect("Overcharge_Container", blPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 8f), new Vector2(0f, 30f));
            SetPivot(overchargeGo, 0.5f, 0f);
            overchargeGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 8f);
            overchargeGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 30f);
            CreateChamferPanel(overchargeGo.transform, "OcFrame", 6f, PacificRimUITheme.PanelOpaque, PacificRimUITheme.BlueBright).raycastTarget = false;

            var ocStatusTxt = CreateStyledText("OverchargeLabel", overchargeGo.transform, PacificRimTextStyle.Caption,
                "OVERCHARGE", PacificRimUITheme.BlueBright, TextAnchor.MiddleLeft, 10, 0, true);
            ocStatusTxt.rectTransform.anchorMin = new Vector2(0f, 0.35f);
            ocStatusTxt.rectTransform.anchorMax = new Vector2(0.72f, 1f);
            ocStatusTxt.rectTransform.offsetMin = new Vector2(12f, 0f);
            ocStatusTxt.rectTransform.offsetMax = Vector2.zero;

            var ocTimerTxt = CreateStyledText("OverchargeTimer", overchargeGo.transform, PacificRimTextStyle.Number,
                "15.0S", PacificRimUITheme.Text, TextAnchor.MiddleRight, 11, 0, true);
            ocTimerTxt.rectTransform.anchorMin = new Vector2(0.72f, 0.35f);
            ocTimerTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            ocTimerTxt.rectTransform.offsetMin = Vector2.zero;
            ocTimerTxt.rectTransform.offsetMax = new Vector2(-12f, 0f);

            CreateProgressBar("OverchargeBar", overchargeGo.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(12f, 8f), new Vector2(-24f, 4f), PacificRimUITheme.BlueBright, out var ocBar);
            overchargeGo.SetActive(false);

            // 8. Bottom-Right Compact Systems Card (220x82)
            var brPanel = CreateRect("BottomRight_CombatPanel", root.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 62f), new Vector2(220f, 82f));
            SetPivot(brPanel, 1f, 0f);
            CreateChamferPanel(brPanel.transform, "PanelFrame", 10f, PacificRimUITheme.Panel, PacificRimUITheme.Line).raycastTarget = false;
            CreateAccentBar(brPanel.transform, "RightAccent", false, 3f, PacificRimUITheme.Blue);

            BuildMatrixRow("BladesRow", brPanel.transform, -10f, "BLADES [R]", "STOWED", out var bladesValTxt);
            BuildMatrixRow("GuardRow", brPanel.transform, -31f, "GUARD [F]", "READY", out var guardValTxt);
            BuildMatrixRow("CamRow", brPanel.transform, -52f, "CAM [E]", "0M", out var camValTxt);

            // 8b. Bottom-Center Plasmacaster Ultimate Icon (Overwatch-style circular gauge)
            var ultGo = CreateRect("BottomCenter_Ultimate", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(124f, 124f));
            SetPivot(ultGo, 0.5f, 0f);
            var ultWidget = ultGo.AddComponent<PacificRimUltimateWidget>();
            ultWidget.EnsureBuiltAndWired(
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/PlasmaUlt_OuterRing.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/PlasmaUlt_SegmentedRing.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/PlasmaUlt_DashedRing.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/PlasmaUlt_ReadyRing.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/PlasmaUlt_ReadyAura.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/PlasmaUlt_Icon.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/PlasmaUlt_BadgeOutline.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/PlasmaUlt_BadgeSolid.png"));
            ultWidget.SetImmediate(0f, 0, false, false, "T");

            // 9. Bottom Footer Rule + Compact Action Prompt Strip
            var footerRule = CreateRect("HUD_BottomRule", root.transform, new Vector2(0.03f, 0f), new Vector2(0.97f, 0f), new Vector2(0f, 52f), new Vector2(0f, 14f));
            SetPivot(footerRule, 0.5f, 0.5f);
            var brGraphic = footerRule.AddComponent<UIBottomRuleGraphic>();
            brGraphic.lineColor = PacificRimUITheme.Line;
            brGraphic.accentColor = PacificRimUITheme.Blue;
            brGraphic.raycastTarget = false;

            var promptsRow = CreateRect("HUD_PromptsStrip", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1300f, 28f));
            SetPivot(promptsRow, 0.5f, 0f);
            var hLayout = promptsRow.AddComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.spacing = 14f;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;

            var promptLmb    = CreateKeyPrompt("Prompt_LMB", promptsRow.transform, "LMB", "LIGHT", 36f, 44f);
            var promptRmb    = CreateKeyPrompt("Prompt_RMB", promptsRow.transform, "RMB", "HEAVY", 36f, 48f);
            var promptBlock  = CreateKeyPrompt("Prompt_Block", promptsRow.transform, "F", "PARRY", 24f, 46f);
            var promptDash   = CreateKeyPrompt("Prompt_Dash", promptsRow.transform, "2x WASD", "DASH", 64f, 42f);
            var promptBlades = CreateKeyPrompt("Prompt_Blades", promptsRow.transform, "R", "SWORDS", 24f, 56f);
            var promptPlasma = CreateKeyPrompt("Prompt_Plasma", promptsRow.transform, "T", "PLASMA", 24f, 66f);
            var promptOver   = CreateKeyPrompt("Prompt_Overcharge", promptsRow.transform, "Q", "SURGE", 24f, 48f);
            var promptCam    = CreateKeyPrompt("Prompt_Cam", promptsRow.transform, "E", "CAM", 24f, 34f);
            var promptMenu   = CreateKeyPrompt("Prompt_Menu", promptsRow.transform, "ESC", "MENU", 36f, 44f);

            // Wire serialized fields into JaegerHUD
            SetSerializedField(hud, "chassisLabelText", chassisTxt);
            SetSerializedField(hud, "linkStatusText", null);
            SetSerializedField(hud, "combatTimerText", timerTxt);
            SetSerializedField(hud, "bestTimeText", bestTxt);
            SetSerializedField(hud, "timerContainer", timerRow);
            SetSerializedField(hud, "hullCaptionText", hullCaptionTxt);
            SetSerializedField(hud, "hullNumberText", hullNumTxt);
            SetSerializedField(hud, "hullBar", hullBar);
            SetSerializedField(hud, "overchargeContainer", overchargeGo);
            SetSerializedField(hud, "overchargeBar", ocBar);
            SetSerializedField(hud, "overchargeStatusText", ocStatusTxt);
            SetSerializedField(hud, "overchargeTimerText", ocTimerTxt);
            SetSerializedField(hud, "combatStanceValueText", bladesValTxt);
            SetSerializedField(hud, "defenseMatrixValueText", guardValTxt);
            SetSerializedField(hud, "depthTelemetryText", camValTxt);
            SetSerializedField(hud, "holoReticle", reticle);
            SetSerializedField(hud, "overchargeScreenCueGroup", cueCg);
            SetSerializedField(hud, "stancePrompt", null);
            SetSerializedField(hud, "lightAttackPrompt", promptLmb);
            SetSerializedField(hud, "heavyAttackPrompt", promptRmb);
            SetSerializedField(hud, "defensePrompt", promptBlock);
            SetSerializedField(hud, "dashPrompt", promptDash);
            SetSerializedField(hud, "bladesPrompt", promptBlades);
            SetSerializedField(hud, "overchargePrompt", promptOver);
            SetSerializedField(hud, "plasmaPrompt", promptPlasma);
            SetSerializedField(hud, "ultimateWidget", ultWidget);
            SetSerializedField(hud, "camPrompt", promptCam);
            SetSerializedField(hud, "menuPrompt", promptMenu);
            SetSerializedField(hud, "staminaCaptionText", staminaCaptionTxt);
            SetSerializedField(hud, "staminaValueText", staminaValueTxt);
            SetSerializedField(hud, "staminaBar", staminaBar);
            SetSerializedField(hud, "impactFlashGroup", impactCg);
            SetSerializedField(hud, "impactEdges", impactEdges);
            SetSerializedField(hud, "bannerGroup", bannerCg);
            SetSerializedField(hud, "bannerText", bannerTxt);
            SetSerializedField(hud, "bossPanelRoot", bossPanel);
            SetSerializedField(hud, "bossNameText", bossNameTxt);
            SetSerializedField(hud, "bossPhaseText", bossPhaseTxt);
            SetSerializedField(hud, "bossStatusText", bossStatusTxt);
            SetSerializedField(hud, "bossHpNumberText", bossHpNumTxt);
            SetSerializedField(hud, "bossHealthBar", bossHpBar);
            SetSerializedField(hud, "bossSubBar", bossSubBar);

            return root;
        }

        // =====================================================================
        // MINIMALIST IN-GAME PAUSE / OUTCOME MENU
        // =====================================================================

        public static GameObject BuildPauseMenu(Transform parent, GameObject hudRoot, JaegerHUD hud)
        {
            var menuRoot = CreateRect("MainMenu_Root", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var mainMenu = menuRoot.AddComponent<MainMenuUI>();

            var visualsRoot = CreateRect("PauseVisuals_Root", menuRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var menuCg = visualsRoot.AddComponent<CanvasGroup>();

            // Translucent dark backdrop
            var bdGo = CreateRect("Backdrop_Dim", visualsRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bdImg = bdGo.AddComponent<Image>();
            bdImg.color = new Color(0.02f, 0.03f, 0.06f, 0.72f);

            // Sleek centered dialog (440x462)
            var dialogGo = CreateRect("Pause_DialogBox", visualsRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 462f));
            CreateChamferPanel(dialogGo.transform, "Dialog_Frame", 12f, PacificRimUITheme.PanelOpaque, PacificRimUITheme.Blue);
            CreateAccentBar(dialogGo.transform, "Dialog_LeftAccent", true, 3.5f, PacificRimUITheme.BlueBright);

            bool isPitfall = SceneManager.GetActiveScene().name.IndexOf("Operation Pitfall", StringComparison.OrdinalIgnoreCase) >= 0;

            var subTxt = CreateStyledText("Pause_Subtitle", dialogGo.transform, PacificRimTextStyle.Caption,
                isPitfall ? "OP 02  ·  OPERATION PITFALL" : "OP 01  ·  SAVE STRIKER EUREKA",
                PacificRimUITheme.TextDim, TextAnchor.MiddleCenter, 11, 0, true);
            SetAnchorTop(subTxt.rectTransform, 0f, -20f, 380f, 18f);

            var titleTxt = CreateStyledText("Pause_Title", dialogGo.transform, PacificRimTextStyle.Heading,
                "PAUSED", PacificRimUITheme.BlueBright, TextAnchor.MiddleCenter, 24, 0, true);
            SetAnchorTop(titleTxt.rectTransform, 0f, -40f, 380f, 30f);

            CreateDivider("Pause_Divider", dialogGo.transform, -76f, 376f, 32f);

            // Compact 2-line Telemetry Strip
            var telemBox = CreateRect("Telemetry_Box", dialogGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(376f, 54f));
            SetPivot(telemBox, 0.5f, 1f);
            CreateChamferPanel(telemBox.transform, "Telem_Frame", 7f, PacificRimUITheme.Panel, PacificRimUITheme.Line);
            CreateAccentBar(telemBox.transform, "Telem_Accent", true, 2.5f, PacificRimUITheme.Blue);

            var healthTxt = CreateStyledText("HealthInfo", telemBox.transform, PacificRimTextStyle.Caption,
                "HULL 1,000 / 1,000  ·  OPTIMAL", PacificRimUITheme.Success, TextAnchor.MiddleCenter, 12, 0, true);
            SetAnchorTop(healthTxt.rectTransform, 0f, -8f, 350f, 18f);

            var stanceTxt = CreateStyledText("StanceInfo", telemBox.transform, PacificRimTextStyle.Caption,
                "TIME 00:00  ·  BEST --:--", PacificRimUITheme.TextDim, TextAnchor.MiddleCenter, 11, 0, true);
            SetAnchorTop(stanceTxt.rectTransform, 0f, -28f, 350f, 18f);

            // Clean Action Buttons Stack (5 buttons, no duplicates)
            var btnResume = CreateChamferButton("Btn_Resume", dialogGo.transform, Vector2.zero, new Vector2(376f, 44f),
                "RESUME", isPrimary: true, isDanger: false);
            SetAnchorTop(btnResume.GetComponent<RectTransform>(), 0f, -158f, 376f, 44f);

            var btnControls = CreateChamferButton("Btn_Controls", dialogGo.transform, Vector2.zero, new Vector2(376f, 40f),
                "CONTROLS", isPrimary: false, isDanger: false);
            SetAnchorTop(btnControls.GetComponent<RectTransform>(), 0f, -212f, 376f, 40f);

            var btnAudio = CreateChamferButton("Btn_Audio", dialogGo.transform, Vector2.zero, new Vector2(376f, 40f),
                "AUDIO", isPrimary: false, isDanger: false);
            SetAnchorTop(btnAudio.GetComponent<RectTransform>(), 0f, -260f, 376f, 40f);

            var btnRestart = CreateChamferButton("Btn_Restart", dialogGo.transform, Vector2.zero, new Vector2(376f, 40f),
                "RESTART", isPrimary: false, isDanger: false);
            SetAnchorTop(btnRestart.GetComponent<RectTransform>(), 0f, -308f, 376f, 40f);

            var btnMainMenu = CreateChamferButton("Btn_MainMenu", dialogGo.transform, Vector2.zero, new Vector2(376f, 40f),
                "MAIN MENU", isPrimary: false, isDanger: true);
            SetAnchorTop(btnMainMenu.GetComponent<RectTransform>(), 0f, -356f, 376f, 40f);

            // Footer prompt strip
            var promptsGo = CreateRect("Pause_Prompts", dialogGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(376f, 28f));
            SetPivot(promptsGo, 0.5f, 0f);
            var pLayout = promptsGo.AddComponent<HorizontalLayoutGroup>();
            pLayout.childAlignment = TextAnchor.MiddleCenter;
            pLayout.spacing = 24f;
            pLayout.childForceExpandWidth = false;
            pLayout.childForceExpandHeight = false;

            CreateKeyPrompt("Prompt_Esc", promptsGo.transform, "ESC", "RESUME", 36f, 56f);
            CreateKeyPrompt("Prompt_Enter", promptsGo.transform, "ENTER", "CONFIRM", 48f, 64f);

            // Modals: Controls & Audio
            var controlsModal = BuildControlsModal(visualsRoot.transform);
            controlsModal.gameObject.SetActive(false);

            var audioModal = BuildAudioModal(visualsRoot.transform);
            audioModal.gameObject.SetActive(false);

            // Wire serialized fields on MainMenuUI
            SetSerializedField(mainMenu, "menuVisualsRoot", visualsRoot);
            SetSerializedField(mainMenu, "menuCanvasGroup", menuCg);
            SetSerializedField(mainMenu, "hudRoot", hudRoot);
            SetSerializedField(mainMenu, "deployButton", btnResume);
            SetSerializedField(mainMenu, "controlsButton", btnControls);
            SetSerializedField(mainMenu, "audioButton", btnAudio);
            SetSerializedField(mainMenu, "restartButton", btnRestart);
            SetSerializedField(mainMenu, "returnToMenuButton", btnMainMenu);
            SetSerializedField(mainMenu, "controlsModal", controlsModal);
            SetSerializedField(mainMenu, "audioModal", audioModal);
            SetSerializedField(mainMenu, "jaegerHud", hud);
            SetSerializedField(mainMenu, "pauseTitleText", titleTxt);
            SetSerializedField(mainMenu, "pauseSubtitleText", subTxt);
            SetSerializedField(mainMenu, "pauseHealthText", healthTxt);
            SetSerializedField(mainMenu, "pauseMissionText", stanceTxt);

            return menuRoot;
        }

        // =====================================================================
        // MINIMALIST MAIN MENU (PACIFIC RIM BLUE THEME)
        // =====================================================================

        public static GameObject BuildMainMenu(Transform parent, GameObject hudRoot, JaegerHUD hud)
        {
            var menuRoot = CreateRect("MainMenu_Root", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var mainMenu = menuRoot.AddComponent<MainMenuUI>();

            var visualsRoot = CreateRect("MenuVisuals_Root", menuRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var menuCg = visualsRoot.AddComponent<CanvasGroup>();

            // 1. Sleek Atmospheric Backdrop (left-weighted scrim so 3D Jaeger shines on the right)
            BuildCinematicBackdrop(visualsRoot.transform);

            // 2. Slim Top Navigation Tab Bar
            var topBar = CreateRect("Top_TabBar", visualsRoot.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -32f), new Vector2(-160f, 44f));
            SetPivot(topBar, 0.5f, 1f);

            var tabBar = BuildTabBar(topBar.transform, out _);

            string initBest = PacificRimCombatRecords.FormatTime(PacificRimCombatRecords.GetBestTime(MainMenuUI.Level1SceneName));
            var sectorCaption = CreateStyledText("SectorStatus", topBar.transform, PacificRimTextStyle.Caption,
                $"SECTOR 04  ·  BEST {initBest}", PacificRimUITheme.Amber, TextAnchor.MiddleRight, 12, 0, true);
            sectorCaption.rectTransform.anchorMin = new Vector2(0.55f, 0f);
            sectorCaption.rectTransform.anchorMax = new Vector2(1f, 1f);
            sectorCaption.rectTransform.offsetMin = Vector2.zero;
            sectorCaption.rectTransform.offsetMax = Vector2.zero;

            // 3. Safe Content Area
            var safeArea = CreateRect("SafeContentArea", visualsRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeArea.GetComponent<RectTransform>().offsetMin = new Vector2(80f, 96f);
            safeArea.GetComponent<RectTransform>().offsetMax = new Vector2(-80f, -96f);

            // Left Column: Hero Title + Active Tab Content + Primary Actions (kept cleanly to the left of center Jaeger)
            var leftCol = CreateRect("Left_HeroColumn", safeArea.transform, new Vector2(0f, 0f), new Vector2(0.42f, 1f), Vector2.zero, Vector2.zero);

            BuildSectionTitle("SectionHeader", leftCol.transform, -14f, "MARK-III  ·  GIPSY DANGER");

            var pageTitle = CreateStyledText("PageTitle", leftCol.transform, PacificRimTextStyle.Display,
                "Pacific Rim: Operation Pitfall", PacificRimUITheme.Text, TextAnchor.UpperLeft, 42, 0, true);
            var ptRt = pageTitle.rectTransform;
            ptRt.anchorMin = new Vector2(0f, 1f);
            ptRt.anchorMax = new Vector2(1f, 1f);
            ptRt.pivot = new Vector2(0f, 1f);
            ptRt.anchoredPosition = new Vector2(0f, -44f);
            ptRt.sizeDelta = new Vector2(0f, 48f);

            var subTitle = CreateStyledText("SubTitle", leftCol.transform, PacificRimTextStyle.Caption,
                "01  ·  SAVE STRIKER EUREKA", PacificRimUITheme.Amber, TextAnchor.MiddleLeft, 13, 0, true);
            var stRt = subTitle.rectTransform;
            stRt.anchorMin = new Vector2(0f, 1f);
            stRt.anchorMax = new Vector2(1f, 1f);
            stRt.pivot = new Vector2(0f, 1f);
            stRt.anchoredPosition = new Vector2(0f, -94f);
            stRt.sizeDelta = new Vector2(0f, 22f);

            CreateDivider("TitleDivider", leftCol.transform, -124f, 460f);

            // Tab Pages Area (Deploy / Controls / Chassis / Audio)
            var tabContainer = CreateRect("TabPages_Container", leftCol.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            tabContainer.GetComponent<RectTransform>().offsetMin = new Vector2(0f, 120f);
            tabContainer.GetComponent<RectTransform>().offsetMax = new Vector2(0f, -138f);

            var panelOperation = BuildTabPageOperation(tabContainer.transform, out var briefingTxt, out var opTitleVal, out var opThreatVal, out var opBestVal);
            var panelControls = BuildTabPageControls(tabContainer.transform);
            var panelChassis = BuildTabPageChassis(tabContainer.transform);
            var panelAudio = BuildTabPageAudio(tabContainer.transform);
            panelControls.SetActive(false);
            panelChassis.SetActive(false);
            panelAudio.SetActive(false);

            // Primary Actions Area (Clean 3-button cluster)
            var actionsArea = CreateRect("PrimaryActions", leftCol.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 102f));
            SetPivot(actionsArea, 0f, 0f);

            var deployBtn = CreateChamferButton("Btn_Deploy", actionsArea.transform, Vector2.zero, new Vector2(440f, 48f),
                "DEPLOY  ·  OP 01", isPrimary: true, isDanger: false);
            SetAnchorTopLeft(deployBtn.GetComponent<RectTransform>(), 0f, 0f, 440f, 48f);

            var controlsBtn = CreateChamferButton("Btn_Controls", actionsArea.transform, Vector2.zero, new Vector2(214f, 40f),
                "CONTROLS", isPrimary: false, isDanger: false);
            SetAnchorTopLeft(controlsBtn.GetComponent<RectTransform>(), 0f, -58f, 214f, 40f);

            var exitBtn = CreateChamferButton("Btn_Exit", actionsArea.transform, Vector2.zero, new Vector2(214f, 40f),
                "EXIT", isPrimary: false, isDanger: true);
            SetAnchorTopLeft(exitBtn.GetComponent<RectTransform>(), 226f, -58f, 214f, 40f);

            // Right Column: Only the 2 Mission Cards (leaves center 3D mech view completely clear!)
            var rightCol = CreateRect("Right_CardsColumn", safeArea.transform, new Vector2(0.64f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            BuildSectionTitle("CardsSectionTitle", rightCol.transform, -14f, "OPERATIONS");

            var cardsContainer = CreateRect("CardsStack", rightCol.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var ccRt = cardsContainer.GetComponent<RectTransform>();
            ccRt.offsetMin = Vector2.zero;
            ccRt.offsetMax = new Vector2(0f, -48f);

            var vLayout = cardsContainer.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 14f;
            vLayout.childAlignment = TextAnchor.UpperLeft;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            var cardLevel1 = CreateFeatureCard("Card_Level1", cardsContainer.transform, 0f, 0f, 96f,
                "01  ·  HONG KONG BAY", "SAVE STRIKER EUREKA", "Category IV Leatherback", "ACTIVE", PacificRimUITheme.Success);
            var le1 = cardLevel1.GetComponent<LayoutElement>() ?? cardLevel1.gameObject.AddComponent<LayoutElement>();
            le1.preferredHeight = 96f;
            cardLevel1.SetSelected(true);

            var cardLevel2 = CreateFeatureCard("Card_Level2", cardsContainer.transform, 0f, 0f, 96f,
                "02  ·  MARIANA TRENCH", "OPERATION PITFALL", "Solo Mission · Cat V Slattern", "SELECT", PacificRimUITheme.TextDim);
            var le2 = cardLevel2.GetComponent<LayoutElement>() ?? cardLevel2.gameObject.AddComponent<LayoutElement>();
            le2.preferredHeight = 96f;

            // 4. Footer Rule & Minimal Prompts
            var footerRule = CreateRect("Menu_BottomRule", visualsRoot.transform, new Vector2(0.04f, 0f), new Vector2(0.96f, 0f), new Vector2(0f, 60f), new Vector2(0f, 14f));
            SetPivot(footerRule, 0.5f, 0.5f);
            var ruleGraphic = footerRule.AddComponent<UIBottomRuleGraphic>();
            ruleGraphic.lineColor = PacificRimUITheme.Line;
            ruleGraphic.accentColor = PacificRimUITheme.Blue;
            ruleGraphic.raycastTarget = false;

            var menuPrompts = CreateRect("Menu_PromptsStrip", visualsRoot.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(700f, 28f));
            SetPivot(menuPrompts, 0.5f, 0f);
            var mpLayout = menuPrompts.AddComponent<HorizontalLayoutGroup>();
            mpLayout.childAlignment = TextAnchor.MiddleCenter;
            mpLayout.spacing = 28f;
            mpLayout.childForceExpandWidth = false;
            mpLayout.childForceExpandHeight = false;

            CreateKeyPrompt("MenuPrompt_Deploy", menuPrompts.transform, "ENTER", "DEPLOY", 48f, 64f);
            CreateKeyPrompt("MenuPrompt_Click", menuPrompts.transform, "CLICK", "SELECT MISSION", 48f, 114f);
            CreateKeyPrompt("MenuPrompt_Esc", menuPrompts.transform, "ESC", "BACK", 36f, 48f);

            // 5. Controls Modal (kept for quick access if ever invoked)
            var controlsModal = BuildControlsModal(visualsRoot.transform);
            controlsModal.gameObject.SetActive(false);

            // Wire serialized references onto MainMenuUI
            SetSerializedField(mainMenu, "menuVisualsRoot", visualsRoot);
            SetSerializedField(mainMenu, "menuCanvasGroup", menuCg);
            SetSerializedField(mainMenu, "hudRoot", hudRoot);
            SetSerializedField(mainMenu, "tabBar", tabBar);
            SetSerializedField(mainMenu, "tabPanels", new[] { panelOperation, panelControls, panelChassis, panelAudio });
            SetSerializedField(mainMenu, "featureCards", new[] { cardLevel1, cardLevel2 });
            SetSerializedField(mainMenu, "deployButton", deployBtn);
            SetSerializedField(mainMenu, "controlsButton", controlsBtn);
            SetSerializedField(mainMenu, "exitButton", exitBtn);
            SetSerializedField(mainMenu, "controlsModal", controlsModal);
            SetSerializedField(mainMenu, "jaegerHud", hud);
            SetSerializedField(mainMenu, "menuBestTimeText", sectorCaption);
            SetSerializedField(mainMenu, "menuMainSubtitleText", subTitle);
            SetSerializedField(mainMenu, "briefingText", briefingTxt);
            SetSerializedField(mainMenu, "opRowTitleValue", opTitleVal);
            SetSerializedField(mainMenu, "opRowThreatValue", opThreatVal);
            SetSerializedField(mainMenu, "opRowBestValue", opBestVal);

            return menuRoot;
        }

        private static PacificRimCinematicBackdrop BuildCinematicBackdrop(Transform parent)
        {
            var bdGo = CreateRect("Cinematic_Backdrop", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var cg = bdGo.AddComponent<CanvasGroup>();
            cg.alpha = 0.84f;

            var comp = bdGo.AddComponent<PacificRimCinematicBackdrop>();
            comp.backdropGroup = cg;

            // Subtle base scrim (lets the 3D Jaeger showcase stay visible)
            var baseGo = CreateRect("Base", bdGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var baseImg = baseGo.AddComponent<Image>();
            baseImg.color = new Color(PacificRimUITheme.Background.r, PacificRimUITheme.Background.g, PacificRimUITheme.Background.b, 0.32f);
            baseImg.raycastTarget = true;

            // Left-side directional readability scrim (fades cleanly toward center-right)
            BuildGradientEdgeHorizontal(bdGo.transform, "LeftReadabilityScrim",
                new Vector2(0f, 0f), new Vector2(0.72f, 1f),
                new Color(PacificRimUITheme.Background.r, PacificRimUITheme.Background.g, PacificRimUITheme.Background.b, 0.90f),
                Color.clear);

            // Subtle atmospheric ocean blobs
            var blobsRoot = CreateRect("Blobs", bdGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var blobSpecs = new (Vector2 pos, Vector2 size, Color col)[]
            {
                (new Vector2(-620f, 240f), new Vector2(760f, 620f), new Color(18f/255f, 84f/255f, 165f/255f, 0.16f)),
                (new Vector2(540f, -180f), new Vector2(780f, 640f), new Color(12f/255f, 52f/255f, 120f/255f, 0.14f)),
                (new Vector2(-180f, -260f), new Vector2(640f, 500f), new Color(36f/255f, 148f/255f, 226f/255f, 0.11f)),
                (new Vector2(480f, 300f), new Vector2(540f, 460f), new Color(70f/255f, 195f/255f, 255f/255f, 0.08f)),
                (new Vector2(-780f, -140f), new Vector2(580f, 580f), new Color(8f/255f, 38f/255f, 110f/255f, 0.18f)),
            };

            for (int i = 0; i < blobSpecs.Length; i++)
            {
                var spec = blobSpecs[i];
                var bGo = CreateRect($"Blob_{i}", blobsRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), spec.pos, spec.size);
                var blob = bGo.AddComponent<UISoftBlobGraphic>();
                blob.blobColor = spec.col;
                blob.raycastTarget = false;
                comp.blobs.Add(bGo.GetComponent<RectTransform>());
            }

            // 40 subtle drifting sparks
            var embersRoot = CreateRect("Embers", bdGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            for (int i = 0; i < 40; i++)
            {
                float sx = Mathf.Sin((i + 1) * 19.31f);
                float sy = Mathf.Cos((i + 1) * 43.17f);
                float size = (i % 4 == 0) ? 3f : 2f;
                var eGo = CreateRect($"Ember_{i}", embersRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(sx * 920f, sy * 510f), new Vector2(size, size));
                var eImg = eGo.AddComponent<Image>();
                eImg.color = (i % 7 == 0)
                    ? new Color(PacificRimUITheme.Amber.r, PacificRimUITheme.Amber.g, PacificRimUITheme.Amber.b, 0.40f)
                    : new Color(PacificRimUITheme.BlueBright.r, PacificRimUITheme.BlueBright.g, PacificRimUITheme.BlueBright.b, 0.48f);
                eImg.raycastTarget = false;
                comp.embers.Add(eGo.GetComponent<RectTransform>());
            }

            // Soft Edge Vignettes
            var vigRoot = CreateRect("Vignettes", bdGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Color edgeDark = new Color(0f, 0f, 0f, 0.62f);
            BuildGradientEdge(vigRoot.transform, "Vignette_Top", new Vector2(0f, 0.82f), new Vector2(1f, 1f), edgeDark, Color.clear);
            BuildGradientEdge(vigRoot.transform, "Vignette_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0.20f), Color.clear, edgeDark);
            BuildGradientEdgeHorizontal(vigRoot.transform, "Vignette_Left", new Vector2(0f, 0f), new Vector2(0.10f, 1f), edgeDark, Color.clear);
            BuildGradientEdgeHorizontal(vigRoot.transform, "Vignette_Right", new Vector2(0.88f, 0f), new Vector2(1f, 1f), Color.clear, edgeDark);

            comp.InitializeMotion();
            return comp;
        }

        private static PacificRimTabBar BuildTabBar(Transform parent, out List<Button> buttons)
        {
            var tb = parent.gameObject.AddComponent<PacificRimTabBar>();
            buttons = new List<Button>();

            var lineGo = CreateRect("BottomHairline", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            SetPivot(lineGo, 0.5f, 0f);
            var lineDiv = lineGo.AddComponent<UIDividerGraphic>();
            lineDiv.color = PacificRimUITheme.Line;
            lineDiv.fadeEdgePercent = 0.05f;
            lineDiv.raycastTarget = false;

            string[] tabTitles = { "DEPLOY", "CONTROLS", "CHASSIS", "AUDIO" };
            float[] tabWidths = { 82f, 100f, 88f, 72f };
            float curX = 0f;
            float tabH = 40f;

            for (int i = 0; i < tabTitles.Length; i++)
            {
                float w = tabWidths[i];
                var tabGo = CreateRect($"Tab_{tabTitles[i]}", parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(curX, 2f), new Vector2(w, tabH));
                SetPivot(tabGo, 0f, 0.5f);

                var hitImg = tabGo.AddComponent<Image>();
                hitImg.color = Color.clear;
                var btn = tabGo.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;

                var lbl = CreateStyledText("Label", tabGo.transform, PacificRimTextStyle.Caption,
                    tabTitles[i], i == 0 ? PacificRimUITheme.Text : PacificRimUITheme.TextMuted, TextAnchor.MiddleLeft, 13, 0, true);
                Stretch(lbl.rectTransform);

                tb.tabButtons.Add(btn);
                tb.tabLabels.Add(lbl);
                buttons.Add(btn);
                curX += w + 24f;
            }

            var indGo = CreateRect("ActiveIndicator", parent, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(tabWidths[0], 2f));
            SetPivot(indGo, 0f, 0f);
            var indImg = indGo.AddComponent<Image>();
            indImg.color = PacificRimUITheme.BlueBright;
            indImg.raycastTarget = false;
            tb.indicatorRect = indGo.GetComponent<RectTransform>();

            return tb;
        }

        private static GameObject BuildTabPageOperation(Transform parent, out Text briefingTxt, out Text opTitleVal, out Text opThreatVal, out Text opBestVal)
        {
            var page = CreateRect("Page_Operation", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            briefingTxt = CreateStyledText("BriefingText", page.transform, PacificRimTextStyle.Body,
                "Protect paralyzed Striker Eureka from Category IV Leatherback in Hong Kong Bay.",
                PacificRimUITheme.TextDim, TextAnchor.UpperLeft, 14, 0, false);
            briefingTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            briefingTxt.verticalOverflow = VerticalWrapMode.Truncate;
            briefingTxt.lineSpacing = 1.2f;
            var dRt = briefingTxt.rectTransform;
            dRt.anchorMin = new Vector2(0f, 1f);
            dRt.anchorMax = new Vector2(1f, 1f);
            dRt.pivot = new Vector2(0f, 1f);
            dRt.anchoredPosition = new Vector2(0f, -4f);
            dRt.sizeDelta = new Vector2(0f, 46f);

            var row1 = BuildUIListRow("OpRow_Weather", page.transform, -58f, "ZONE", "HONG KONG BAY", false, 44f);
            var row2 = BuildUIListRow("OpRow_Chassis", page.transform, -106f, "THREAT", "CAT IV LEATHERBACK", true, 44f);
            var row3 = BuildUIListRow("OpRow_BestRecord", page.transform, -154f, "BEST TIME", PacificRimCombatRecords.FormatTime(PacificRimCombatRecords.GetBestTime(MainMenuUI.Level1SceneName)), false, 44f);

            // Quick Controls Preview Strip right under operation metadata
            var quickStrip = CreateRect("QuickControlsHint", page.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -216f), new Vector2(0f, 28f));
            SetPivot(quickStrip, 0f, 1f);
            var qLayout = quickStrip.AddComponent<HorizontalLayoutGroup>();
            qLayout.childAlignment = TextAnchor.MiddleLeft;
            qLayout.spacing = 18f;
            qLayout.childForceExpandWidth = false;
            qLayout.childForceExpandHeight = false;

            CreateKeyPrompt("Quick_LMB", quickStrip.transform, "LMB/RMB", "STRIKE", 64f, 48f);
            CreateKeyPrompt("Quick_F", quickStrip.transform, "F", "PARRY", 24f, 44f);
            CreateKeyPrompt("Quick_Dash", quickStrip.transform, "2x WASD", "DASH", 64f, 40f);
            CreateKeyPrompt("Quick_T", quickStrip.transform, "T", "PLASMA", 24f, 54f);

            opTitleVal = row1.transform.Find("RightValue")?.GetComponent<Text>();
            opThreatVal = row2.transform.Find("RightValue")?.GetComponent<Text>();
            opBestVal = row3.transform.Find("RightValue")?.GetComponent<Text>();

            return page;
        }

        private static GameObject BuildTabPageControls(Transform parent)
        {
            var page = CreateRect("Page_Controls", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BuildTwoColumnControlsGrid(page.transform);
            return page;
        }

        private static GameObject BuildTabPageChassis(Transform parent)
        {
            var page = CreateRect("Page_Chassis", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BuildUIListRow("DiagRow_1", page.transform, -4f,   "CLASSIFICATION", "MARK-III HYBRID", true, 46f);
            BuildUIListRow("DiagRow_2", page.transform, -54f,  "DIMENSIONS", "79.2 M  ·  1,980 TONS", false, 46f);
            BuildUIListRow("DiagRow_3", page.transform, -104f, "POWER PLANT", "DUAL NUCLEAR VORTEX", false, 46f);
            BuildUIListRow("DiagRow_4", page.transform, -154f, "ARMAMENTS", "GD6 SWORDS  ·  I-19 PLASMA", true, 46f);
            BuildUIListRow("DiagRow_5", page.transform, -204f, "COMBAT RATING", "SPD 7  ·  STR 8  ·  ARM 6", false, 46f);
            return page;
        }

        private static GameObject BuildTabPageAudio(Transform parent)
        {
            var page = CreateRect("Page_Audio", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            PacificRimAudioSettingsUI.BuildEmbeddedSliders(page.transform, -4f, 66f);
            return page;
        }

        private static PacificRimModal BuildControlsModal(Transform parent)
        {
            var modal = CreateBaseModal("Controls_Modal", parent, "CONTROLS", "GIPSY DANGER INPUTS", 780f, 490f);
            BuildTwoColumnControlsGrid(modal.contentRoot);
            return modal;
        }

        private static PacificRimModal BuildAudioModal(Transform parent)
        {
            var modal = CreateBaseModal("Audio_Modal", parent, "AUDIO", "VOLUME LEVELS", 440f, 286f);
            PacificRimAudioSettingsUI.BuildEmbeddedSliders(modal.contentRoot, 0f, 66f);
            return modal;
        }

        private static void BuildTwoColumnControlsGrid(Transform container)
        {
            var leftCol = CreateRect("Col_Left", container, new Vector2(0f, 0f), new Vector2(0.485f, 1f), Vector2.zero, Vector2.zero);
            var rightCol = CreateRect("Col_Right", container, new Vector2(0.515f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            var leftHeader = CreateStyledText("Header_Left", leftCol.transform, PacificRimTextStyle.Caption,
                "MOVEMENT & MELEE", PacificRimUITheme.BlueBright, TextAnchor.MiddleLeft, 10, 0, true);
            SetAnchorTopLeft(leftHeader.rectTransform, 0f, 0f, 260f, 18f);

            var rightHeader = CreateStyledText("Header_Right", rightCol.transform, PacificRimTextStyle.Caption,
                "DEFENSE & SYSTEMS", PacificRimUITheme.BlueBright, TextAnchor.MiddleLeft, 10, 0, true);
            SetAnchorTopLeft(rightHeader.rectTransform, 0f, 0f, 260f, 18f);

            float rowH = 40f;
            float rowStep = 44f;
            float startY = -22f;

            for (int i = 0; i < MovementAndMeleeControls.Length; i++)
            {
                BuildControlGridRow($"CtrlL_{i}", leftCol.transform, startY - i * rowStep, rowH, MovementAndMeleeControls[i]);
            }

            for (int i = 0; i < DefenseAndSystemControls.Length; i++)
            {
                BuildControlGridRow($"CtrlR_{i}", rightCol.transform, startY - i * rowStep, rowH, DefenseAndSystemControls[i]);
            }
        }

        private static void BuildControlGridRow(string name, Transform parent, float yPos, float height, ControlEntry entry)
        {
            var rowGo = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, yPos), new Vector2(0f, height));
            SetPivot(rowGo, 0f, 1f);

            var bg = rowGo.AddComponent<Image>();
            bg.color = entry.Highlight ? PacificRimUITheme.RowSelectedFill : PacificRimUITheme.RowFill;
            bg.raycastTarget = false;

            if (entry.Highlight)
            {
                CreateAccentBar(rowGo.transform, "Accent", true, 2.5f, PacificRimUITheme.BlueBright);
            }

            // Keycap badge on left
            var keyBox = CreateRect("KeyBox", rowGo.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(88f, 24f));
            SetPivot(keyBox, 0f, 0.5f);
            var keyChamfer = keyBox.AddComponent<UIChamferGraphic>();
            keyChamfer.chamferSize = 4f;
            keyChamfer.borderThickness = 1f;
            keyChamfer.chamferTopLeft = true;
            keyChamfer.chamferTopRight = false;
            keyChamfer.chamferBottomRight = true;
            keyChamfer.chamferBottomLeft = false;
            keyChamfer.fillColor = entry.Highlight
                ? new Color(PacificRimUITheme.Blue.r, PacificRimUITheme.Blue.g, PacificRimUITheme.Blue.b, 0.24f)
                : new Color(8f / 255f, 12f / 255f, 18f / 255f, 0.72f);
            keyChamfer.borderColor = entry.Highlight ? PacificRimUITheme.BlueBright : PacificRimUITheme.Line;
            keyChamfer.raycastTarget = false;

            var keyTxt = CreateStyledText("Key", keyBox.transform, PacificRimTextStyle.Caption,
                entry.Key, entry.Highlight ? PacificRimUITheme.BlueBright : PacificRimUITheme.Text, TextAnchor.MiddleCenter, 10, 0, false);
            Stretch(keyTxt.rectTransform);
            keyTxt.raycastTarget = false;

            // Action & Short Note on right
            var actionTxt = CreateStyledText("Action", rowGo.transform, PacificRimTextStyle.Caption,
                entry.Action, PacificRimUITheme.Text, TextAnchor.MiddleLeft, 11, 0, true);
            actionTxt.rectTransform.anchorMin = new Vector2(0f, 0.46f);
            actionTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            actionTxt.rectTransform.offsetMin = new Vector2(106f, 0f);
            actionTxt.rectTransform.offsetMax = new Vector2(-8f, -2f);
            actionTxt.raycastTarget = false;

            var noteTxt = CreateStyledText("Note", rowGo.transform, PacificRimTextStyle.Small,
                entry.Note, PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 10, 0, false);
            noteTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
            noteTxt.rectTransform.anchorMax = new Vector2(1f, 0.50f);
            noteTxt.rectTransform.offsetMin = new Vector2(106f, 2f);
            noteTxt.rectTransform.offsetMax = new Vector2(-8f, 0f);
            noteTxt.raycastTarget = false;
        }

        private static PacificRimModal CreateBaseModal(string name, Transform parent, string title, string subtitle, float width = 780f, float height = 490f)
        {
            var modalGo = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var cg = modalGo.AddComponent<CanvasGroup>();
            var modal = modalGo.AddComponent<PacificRimModal>();
            modal.canvasGroup = cg;

            var dimGo = CreateRect("Dimmer", modalGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dimImg = dimGo.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.84f);
            var dimBtn = dimGo.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            modal.dimmerButton = dimBtn;

            var winGo = CreateRect("Window", modalGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            modal.windowRect = winGo.GetComponent<RectTransform>();

            var chamfer = CreateChamferPanel(winGo.transform, "WindowFrame", 12f, PacificRimUITheme.PanelOpaque, PacificRimUITheme.Line);
            chamfer.raycastTarget = true;

            var topAccent = CreateRect("TopAccent", winGo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-24f, 2f));
            SetPivot(topAccent, 0.5f, 1f);
            var topImg = topAccent.AddComponent<Image>();
            topImg.color = PacificRimUITheme.BlueBright;
            topImg.raycastTarget = false;

            var titleTxt = CreateStyledText("ModalTitle", winGo.transform, PacificRimTextStyle.Title,
                title, PacificRimUITheme.Text, TextAnchor.MiddleLeft, 22, 0, true);
            SetAnchorTopLeft(titleTxt.rectTransform, 28f, -20f, width - 100f, 28f);

            var subTxt = CreateStyledText("ModalSubtitle", winGo.transform, PacificRimTextStyle.Caption,
                subtitle, PacificRimUITheme.BlueBright, TextAnchor.MiddleLeft, 11, 0, true);
            SetAnchorTopLeft(subTxt.rectTransform, 28f, -50f, width - 100f, 18f);

            modal.titleText = titleTxt;
            modal.subtitleText = subTxt;

            var closeGo = CreateRect("Btn_Close", winGo.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(36f, 36f));
            SetPivot(closeGo, 1f, 1f);

            var circle = closeGo.AddComponent<UICircleOutlineGraphic>();
            circle.fillColor = new Color(8f / 255f, 14f / 255f, 24f / 255f, 0.75f);
            circle.borderColor = PacificRimUITheme.Line;

            var closeTxt = CreateStyledText("X", closeGo.transform, PacificRimTextStyle.Heading, "X", PacificRimUITheme.Text, TextAnchor.MiddleCenter, 14, 0, true);
            Stretch(closeTxt.rectTransform);
            closeTxt.raycastTarget = false;

            var closeUnityBtn = closeGo.AddComponent<Button>();
            closeUnityBtn.transition = Selectable.Transition.None;
            var closeBtn = closeGo.AddComponent<PacificRimButton>();
            closeBtn.unityButton = closeUnityBtn;
            closeBtn.circleGraphic = circle;
            closeBtn.label = closeTxt;
            closeBtn.isDanger = true;
            closeBtn.playBackOnClick = true;
            modal.closeButton = closeBtn;

            CreateDivider("HeaderDivider", winGo.transform, -80f, width - 56f, 28f);

            var contentGo = CreateRect("ContentRoot", winGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var cRt = contentGo.GetComponent<RectTransform>();
            cRt.offsetMin = new Vector2(28f, 22f);
            cRt.offsetMax = new Vector2(-28f, -94f);
            modal.contentRoot = cRt;

            return modal;
        }

        // =====================================================================
        // PRIMITIVE HELPERS
        // =====================================================================

        private static GameObject BuildSectionTitle(string name, Transform parent, float yPos, string text)
        {
            var go = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, yPos), new Vector2(0f, 20f));
            SetPivot(go, 0f, 1f);

            var tick = CreateRect("AccentTick", go.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(16f, 2f));
            SetPivot(tick, 0f, 0.5f);
            var tickImg = tick.AddComponent<Image>();
            tickImg.color = PacificRimUITheme.BlueBright;
            tickImg.raycastTarget = false;

            var lbl = CreateStyledText("Label", go.transform, PacificRimTextStyle.Caption, text, PacificRimUITheme.BlueBright, TextAnchor.MiddleLeft, 11, 0, true);
            lbl.rectTransform.anchorMin = Vector2.zero;
            lbl.rectTransform.anchorMax = Vector2.one;
            lbl.rectTransform.offsetMin = new Vector2(24f, 0f);
            lbl.rectTransform.offsetMax = Vector2.zero;

            return go;
        }

        private static GameObject BuildUIListRow(string name, Transform parent, float yPos, string leftTitle, string rightValue, bool highlight, float height = 46f)
        {
            var rowGo = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, yPos), new Vector2(0f, height));
            SetPivot(rowGo, 0f, 1f);

            var bg = rowGo.AddComponent<Image>();
            bg.color = highlight ? PacificRimUITheme.RowSelectedFill : PacificRimUITheme.RowFill;
            bg.raycastTarget = false;

            var border = CreateRect("BottomLine", rowGo.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            SetPivot(border, 0.5f, 0f);
            var bImg = border.AddComponent<Image>();
            bImg.color = PacificRimUITheme.Line;
            bImg.raycastTarget = false;

            if (highlight)
            {
                CreateAccentBar(rowGo.transform, "RowAccent", true, 3f, PacificRimUITheme.BlueBright);
            }

            var lTxt = CreateStyledText("LeftKey", rowGo.transform, PacificRimTextStyle.Caption,
                leftTitle, highlight ? PacificRimUITheme.BlueBright : PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 11, 0, true);
            lTxt.rectTransform.anchorMin = Vector2.zero;
            lTxt.rectTransform.anchorMax = new Vector2(0.42f, 1f);
            lTxt.rectTransform.offsetMin = new Vector2(16f, 0f);
            lTxt.rectTransform.offsetMax = Vector2.zero;

            var rTxt = CreateStyledText("RightValue", rowGo.transform, PacificRimTextStyle.Small,
                rightValue, PacificRimUITheme.Text, TextAnchor.MiddleRight, 13, 0, true);
            rTxt.rectTransform.anchorMin = new Vector2(0.42f, 0f);
            rTxt.rectTransform.anchorMax = Vector2.one;
            rTxt.rectTransform.offsetMin = Vector2.zero;
            rTxt.rectTransform.offsetMax = new Vector2(-16f, 0f);

            return rowGo;
        }

        private static PacificRimCard CreateFeatureCard(string name, Transform parent, float yPos, float width, float height,
            string kicker, string title, string subtitle, string badge, Color badgeColor)
        {
            var cardGo = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, yPos), new Vector2(0f, height));
            SetPivot(cardGo, 0f, 1f);

            var cardBtn = cardGo.AddComponent<Button>();
            cardBtn.transition = Selectable.Transition.None;
            var card = cardGo.AddComponent<PacificRimCard>();
            card.unityButton = cardBtn;
            var chamfer = CreateChamferPanel(cardGo.transform, "Chamfer", 10f, PacificRimUITheme.CardFill, PacificRimUITheme.Line);
            chamfer.raycastTarget = true;
            card.chamferGraphic = chamfer;
            card.accentBar = CreateAccentBar(cardGo.transform, "LeftAccent", true, 3f, PacificRimUITheme.Blue);

            var kickerTxt = CreateStyledText("Kicker", cardGo.transform, PacificRimTextStyle.Caption,
                kicker, PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 11, 0, true);
            kickerTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
            kickerTxt.rectTransform.anchorMax = new Vector2(0.70f, 1f);
            kickerTxt.rectTransform.pivot = new Vector2(0f, 1f);
            kickerTxt.rectTransform.anchoredPosition = new Vector2(18f, -12f);
            kickerTxt.rectTransform.sizeDelta = new Vector2(0f, 16f);
            kickerTxt.raycastTarget = false;

            var badgeTxt = CreateStyledText("Badge", cardGo.transform, PacificRimTextStyle.Caption,
                badge, badgeColor, TextAnchor.MiddleRight, 11, 0, true);
            badgeTxt.rectTransform.anchorMin = new Vector2(0.70f, 1f);
            badgeTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            badgeTxt.rectTransform.pivot = new Vector2(1f, 1f);
            badgeTxt.rectTransform.anchoredPosition = new Vector2(-18f, -12f);
            badgeTxt.rectTransform.sizeDelta = new Vector2(0f, 16f);
            badgeTxt.raycastTarget = false;

            var titleTxt = CreateStyledText("Title", cardGo.transform, PacificRimTextStyle.Heading,
                title, PacificRimUITheme.Text, TextAnchor.MiddleLeft, 19, 0, true);
            titleTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleTxt.rectTransform.pivot = new Vector2(0f, 1f);
            titleTxt.rectTransform.anchoredPosition = new Vector2(18f, -34f);
            titleTxt.rectTransform.sizeDelta = new Vector2(-36f, 26f);
            titleTxt.raycastTarget = false;

            var subTxt = CreateStyledText("Subtitle", cardGo.transform, PacificRimTextStyle.Small,
                subtitle, PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 12, 0, false);
            subTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
            subTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            subTxt.rectTransform.pivot = new Vector2(0f, 1f);
            subTxt.rectTransform.anchoredPosition = new Vector2(18f, -64f);
            subTxt.rectTransform.sizeDelta = new Vector2(-36f, 20f);
            subTxt.raycastTarget = false;

            card.kickerText = kickerTxt;
            card.titleText = titleTxt;
            card.subtitleText = subTxt;
            card.badgeText = badgeTxt;

            return card;
        }

        private static PacificRimButton CreateChamferButton(string name, Transform parent, Vector2 pos, Vector2 size,
            string text, bool isPrimary, bool isDanger)
        {
            var go = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            SetPivot(go, 0f, 1f);
            var unityBtn = go.AddComponent<Button>();
            unityBtn.transition = Selectable.Transition.None;
            var btn = go.AddComponent<PacificRimButton>();
            btn.unityButton = unityBtn;
            btn.isPrimary = isPrimary;
            btn.isDanger = isDanger;

            var chamfer = go.AddComponent<UIChamferGraphic>();
            chamfer.chamferSize = 8f;
            chamfer.borderThickness = 1f;
            chamfer.chamferTopLeft = false;
            chamfer.chamferTopRight = true;
            chamfer.chamferBottomRight = false;
            chamfer.chamferBottomLeft = true;
            chamfer.fillColor = isPrimary ? PacificRimUITheme.CardFill : PacificRimUITheme.Panel;
            chamfer.borderColor = isPrimary ? PacificRimUITheme.Blue : PacificRimUITheme.Line;
            chamfer.raycastTarget = true;
            btn.chamferGraphic = chamfer;

            if (isPrimary)
            {
                btn.accentBar = CreateAccentBar(go.transform, "Accent", true, 3f, PacificRimUITheme.BlueBright);
            }

            var lbl = CreateStyledText("Label", go.transform, PacificRimTextStyle.Caption,
                text, PacificRimUITheme.Text, TextAnchor.MiddleCenter, isPrimary ? 15 : 13, 0, true);
            Stretch(lbl.rectTransform);
            lbl.raycastTarget = false;
            btn.label = lbl;

            return btn;
        }

        private static PacificRimKeyPrompt CreateKeyPrompt(string name, Transform parent, string key, string action, float badgeWidth, float actionWidth)
        {
            var go = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(badgeWidth + 6f + actionWidth, 24f));
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = badgeWidth + 6f + actionWidth;
            le.preferredHeight = 24f;

            var prompt = go.AddComponent<PacificRimKeyPrompt>();

            var badgeGo = CreateRect("KeyBadge", go.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(badgeWidth, 20f));
            SetPivot(badgeGo, 0f, 0.5f);

            var badgeChamfer = badgeGo.AddComponent<UIChamferGraphic>();
            badgeChamfer.chamferSize = 3f;
            badgeChamfer.borderThickness = 1f;
            badgeChamfer.chamferTopLeft = true;
            badgeChamfer.chamferTopRight = false;
            badgeChamfer.chamferBottomRight = true;
            badgeChamfer.chamferBottomLeft = false;
            badgeChamfer.fillColor = new Color(8f / 255f, 12f / 255f, 18f / 255f, 0.55f);
            badgeChamfer.borderColor = PacificRimUITheme.Line;
            badgeChamfer.raycastTarget = false;

            var keyTxt = CreateStyledText("KeyText", badgeGo.transform, PacificRimTextStyle.Caption,
                key, PacificRimUITheme.Text, TextAnchor.MiddleCenter, 10, 0, false);
            Stretch(keyTxt.rectTransform);
            keyTxt.raycastTarget = false;

            var actGo = CreateRect("ActionLabel", go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var aRt = actGo.GetComponent<RectTransform>();
            aRt.offsetMin = new Vector2(badgeWidth + 6f, 0f);
            aRt.offsetMax = Vector2.zero;
            var actTxt = CreateStyledText("Text", actGo.transform, PacificRimTextStyle.Caption,
                action, PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 10, 0, true);
            Stretch(actTxt.rectTransform);
            actTxt.raycastTarget = false;

            prompt.keyBoxGraphic = badgeChamfer;
            prompt.keyLabel = keyTxt;
            prompt.actionLabel = actTxt;
            prompt.Setup(key, action);

            return prompt;
        }

        private static GameObject CreateProgressBar(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 sizeDelta, Color fillColor, out PacificRimBar bar)
        {
            var bgGo = CreateRect(name, parent, anchorMin, anchorMax, anchoredPos, sizeDelta);
            SetPivot(bgGo, 0f, 1f);

            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = PacificRimUITheme.BarTrack;
            bgImg.raycastTarget = false;

            var fillGo = CreateRect("Fill", bgGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.raycastTarget = false;

            bar = bgGo.AddComponent<PacificRimBar>();
            bar.backgroundImage = bgImg;
            bar.fillImage = fillImg;
            bar.fillTransform = fillGo.GetComponent<RectTransform>();

            return bgGo;
        }

        private static GameObject BuildMatrixRow(string name, Transform parent, float yPos, string label, string defaultVal, out Text valueTxt)
        {
            var row = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, yPos), new Vector2(-28f, 18f));
            SetPivot(row, 0f, 1f);

            var lTxt = CreateStyledText("Label", row.transform, PacificRimTextStyle.Caption, label, PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 10, 0, true);
            lTxt.rectTransform.anchorMin = Vector2.zero;
            lTxt.rectTransform.anchorMax = new Vector2(0.50f, 1f);
            lTxt.rectTransform.offsetMin = Vector2.zero;
            lTxt.rectTransform.offsetMax = Vector2.zero;

            valueTxt = CreateStyledText("Value", row.transform, PacificRimTextStyle.Caption, defaultVal, PacificRimUITheme.TextDim, TextAnchor.MiddleRight, 11, 0, true);
            valueTxt.rectTransform.anchorMin = new Vector2(0.50f, 0f);
            valueTxt.rectTransform.anchorMax = Vector2.one;
            valueTxt.rectTransform.offsetMin = Vector2.zero;
            valueTxt.rectTransform.offsetMax = Vector2.zero;

            return row;
        }

        private static UIChamferGraphic CreateChamferPanel(Transform parent, string name, float chamferSize, Color fill, Color border)
        {
            var go = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var g = go.AddComponent<UIChamferGraphic>();
            g.chamferSize = chamferSize;
            g.borderThickness = 1f;
            g.fillColor = fill;
            g.borderColor = border;
            return g;
        }

        private static Image CreateAccentBar(Transform parent, string name, bool onLeft, float width, Color color)
        {
            var go = CreateRect(name, parent,
                onLeft ? new Vector2(0f, 0.14f) : new Vector2(1f, 0.14f),
                onLeft ? new Vector2(0f, 0.86f) : new Vector2(1f, 0.86f),
                Vector2.zero, new Vector2(width, 0f));
            SetPivot(go, onLeft ? 0f : 1f, 0.5f);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static GameObject CreateDivider(string name, Transform parent, float yPos, float width, float xOffset = 0f)
        {
            var go = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(xOffset, yPos), new Vector2(width, 1f));
            SetPivot(go, 0f, 1f);
            var div = go.AddComponent<UIDividerGraphic>();
            div.color = PacificRimUITheme.Line;
            div.fadeEdgePercent = 0.15f;
            div.raycastTarget = false;
            return go;
        }

        private static void BuildGradientEdge(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color top, Color bottom)
        {
            var go = CreateRect(name, parent, aMin, aMax, Vector2.zero, Vector2.zero);
            var grad = go.AddComponent<UIGradientGraphic>();
            grad.SetVertical(top, bottom);
            grad.raycastTarget = false;
        }

        private static void BuildGradientEdgeHorizontal(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color left, Color right)
        {
            var go = CreateRect(name, parent, aMin, aMax, Vector2.zero, Vector2.zero);
            var grad = go.AddComponent<UIGradientGraphic>();
            grad.SetHorizontal(left, right);
            grad.raycastTarget = false;
        }

        private static Text CreateStyledText(string name, Transform parent, PacificRimTextStyle style,
            string content, Color color, TextAnchor alignment, int sizeOverride, int trackingOverride, bool upperOverride)
        {
            var spec = PacificRimUITheme.GetStyle(style);
            int fontSize = sizeOverride > 0 ? sizeOverride : spec.Size;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.font = PacificRimUITheme.GetDefaultFont();
            txt.fontSize = fontSize;
            txt.fontStyle = spec.FontStyle;
            txt.color = color;
            txt.alignment = alignment;
            txt.supportRichText = true;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.text = upperOverride ? content.ToUpperInvariant() : content;
            return txt;
        }

        private static GameObject CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return go;
        }

        private static void SetPivot(GameObject go, float px, float py)
        {
            go.GetComponent<RectTransform>().pivot = new Vector2(px, py);
        }

        private static void Stretch(RectTransform rt, Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin ?? Vector2.zero;
            rt.offsetMax = offsetMax ?? Vector2.zero;
        }

        private static void SetAnchorTop(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void SetAnchorTopLeft(RectTransform rt, float x, float y, float w = -1f, float h = -1f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            if (w >= 0f && h >= 0f)
                rt.sizeDelta = new Vector2(w, h);
        }

        public static void SetSerializedField(UnityEngine.Object target, string fieldName, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[SetupPacificRimUIScene] Missing property '{fieldName}' on {target.GetType().Name}");
                return;
            }

            if (prop.isArray && value is UnityEngine.Object[] arr)
            {
                prop.arraySize = arr.Length;
                for (int i = 0; i < arr.Length; i++)
                {
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
                }
            }
            else if (value is UnityEngine.Object objVal)
            {
                prop.objectReferenceValue = objVal;
            }
            else if (value == null)
            {
                prop.objectReferenceValue = null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
