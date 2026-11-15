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
        [MenuItem("Pacific Rim/Setup Pacific Rim UI (HUD & Main Menu)")]
        public static void SetupUI()
        {
            // 1. Ensure EventSystem is compatible with active Input handling
            EnsureEventSystem();

            // 2. Ensure Sound Manager exists
            EnsureSoundManager();

            // 3. Ensure Canvas exists
            Canvas canvas = EnsureCanvas();

            // 4. Clean rebuild of HUD and Main Menu roots
            CleanupOldRoots(canvas.transform);

            // 5. Build Jaeger HUD & Simplified Tactical Pause Menu
            GameObject hudGo = BuildJaegerHUD(canvas.transform);
            var hud = hudGo.GetComponent<JaegerHUD>();

            GameObject menuGo = BuildPauseMenu(canvas.transform, hudGo, hud);
            var mainMenu = menuGo.GetComponent<MainMenuUI>();

            // Wire Jaeger controller & Kaiju Boss references into HUD if present
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

            // Default on game start: In gameplay scene, HUD is active and ready for combat!
            // When user loads into SampleScene, they are in the action.
            mainMenu.isMainMenuScene = false;
            mainMenu.gameplaySceneName = "SampleScene";
            mainMenu.mainMenuSceneName = "MainMenuScene";
            mainMenu.isMenuOpen = false;
            mainMenu.SetMenuState(false);

            EditorUtility.SetDirty(canvas.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log("[PacificRimUI] Battlefront-styled Jaeger HUD & In-Game Menu built and wired (Gameplay starts active, toggles via Esc).");
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

            // Assign pure mechanical / tactical clips (strictly NO retro chimes)
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
            var oldNames = new[] { "JaegerHUD_Root", "MainMenu_Root" };
            foreach (string n in oldNames)
            {
                Transform t = parent.Find(n);
                while (t != null)
                {
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                    t = parent.Find(n);
                }
            }
        }

        // =====================================================================
        // IN-GAME JAEGER HUD
        // =====================================================================

        private static GameObject BuildJaegerHUD(Transform parent)
        {
            var root = CreateRect("JaegerHUD_Root", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hud = root.AddComponent<JaegerHUD>();

            // 1. Subtle Overcharge peripheral screen cue (low-alpha cyan edges, never darkens center)
            var cueGo = CreateRect("Overcharge_ScreenCue", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var cueCg = cueGo.AddComponent<CanvasGroup>();
            cueCg.alpha = 0f;
            cueCg.interactable = false;
            cueCg.blocksRaycasts = false;

            Color cueEdge = new Color(PacificRimUITheme.BlueBright.r, PacificRimUITheme.BlueBright.g, PacificRimUITheme.BlueBright.b, 0.36f);
            BuildGradientEdge(cueGo.transform, "CueTop", new Vector2(0f, 0.88f), new Vector2(1f, 1f), cueEdge, Color.clear);
            BuildGradientEdge(cueGo.transform, "CueBottom", new Vector2(0f, 0f), new Vector2(1f, 0.12f), Color.clear, cueEdge);
            BuildGradientEdgeHorizontal(cueGo.transform, "CueLeft", new Vector2(0f, 0f), new Vector2(0.08f, 1f), cueEdge, Color.clear);
            BuildGradientEdgeHorizontal(cueGo.transform, "CueRight", new Vector2(0.92f, 0f), new Vector2(1f, 1f), Color.clear, cueEdge);

            var impactGo = CreateRect("Impact_ScreenCue", cueGo.transform.parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            impactGo.transform.SetSiblingIndex(cueGo.transform.GetSiblingIndex() + 1);
            var impactRt = impactGo.GetComponent<RectTransform>();
            impactRt.offsetMin = Vector2.zero;
            impactRt.offsetMax = Vector2.zero;
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

            // 2. Center tactical reticle
            var reticleGo = CreateRect("HoloReticle", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(88f, 88f));
            var reticle = reticleGo.AddComponent<UIHoloReticleGraphic>();
            reticle.color = new Color(PacificRimUITheme.TextDim.r, PacificRimUITheme.TextDim.g, PacificRimUITheme.TextDim.b, 0.28f);
            reticle.raycastTarget = false;

            // 3. Top Telemetry Strip
            var topLeft = CreateRect("TopLeft_ID", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -34f), new Vector2(420f, 34f));
            SetPivot(topLeft, 0f, 1f);
            var chassisTxt = CreateStyledText("ChassisLabel", topLeft.transform, PacificRimTextStyle.Caption,
                "GIPSY DANGER // MARK-III", PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 13, 2, true);
            Stretch(chassisTxt.rectTransform);

            var topRight = CreateRect("TopRight_Sync", root.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-44f, -24f), new Vector2(460f, 58f));
            SetPivot(topRight, 1f, 1f);

            var timerRow = CreateRect("TopRight_TimerRow", topRight.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 26f));
            SetPivot(timerRow, 1f, 1f);

            var timerTxt = CreateStyledText("CombatTimerText", timerRow.transform, PacificRimTextStyle.Number,
                "TIME: 00:00", PacificRimUITheme.BlueBright, TextAnchor.MiddleRight, 16, 1, false);
            timerTxt.rectTransform.anchorMin = new Vector2(0.52f, 0f);
            timerTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            timerTxt.rectTransform.offsetMin = Vector2.zero;
            timerTxt.rectTransform.offsetMax = Vector2.zero;

            var bestTxt = CreateStyledText("BestTimeText", timerRow.transform, PacificRimTextStyle.Caption,
                $"BEST: {PacificRimCombatRecords.FormatTime(PacificRimCombatRecords.GetBestTime())}", PacificRimUITheme.Amber, TextAnchor.MiddleRight, 13, 2, true);
            bestTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
            bestTxt.rectTransform.anchorMax = new Vector2(0.52f, 1f);
            bestTxt.rectTransform.offsetMin = Vector2.zero;
            bestTxt.rectTransform.offsetMax = new Vector2(-8f, 0f);

            var linkRow = CreateRect("TopRight_LinkRow", topRight.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 24f));
            SetPivot(linkRow, 1f, 0f);
            var linkTxt = CreateStyledText("LinkStatus", linkRow.transform, PacificRimTextStyle.Caption,
                "DRIFT HANDSHAKE // SYNCHRONIZED", PacificRimUITheme.Success, TextAnchor.MiddleRight, 12, 2, true);
            Stretch(linkTxt.rectTransform);

            // 4. Top-Center Kaiju Boss Health Bar Panel
            var bossPanel = CreateRect("TopCenter_BossPanel", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(760f, 74f));
            SetPivot(bossPanel, 0.5f, 1f);
            CreateChamferPanel(bossPanel.transform, "BossPanelFrame", 10f, PacificRimUITheme.Panel, PacificRimUITheme.Danger).raycastTarget = false;
            CreateAccentBar(bossPanel.transform, "BossLeftAccent", true, 3f, PacificRimUITheme.Danger);
            CreateAccentBar(bossPanel.transform, "BossRightAccent", false, 3f, PacificRimUITheme.Amber);

            var bossNameTxt = CreateStyledText("BossNameText", bossPanel.transform, PacificRimTextStyle.Caption,
                "CATEGORY IV KAIJU // LEATHERBACK", PacificRimUITheme.Danger, TextAnchor.MiddleLeft, 13, 2, true);
            bossNameTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
            bossNameTxt.rectTransform.anchorMax = new Vector2(0.66f, 1f);
            bossNameTxt.rectTransform.pivot = new Vector2(0f, 1f);
            bossNameTxt.rectTransform.offsetMin = new Vector2(18f, -26f);
            bossNameTxt.rectTransform.offsetMax = new Vector2(0f, -6f);

            var bossHpNumTxt = CreateStyledText("BossHpNumberText", bossPanel.transform, PacificRimTextStyle.Number,
                "2,600 / 2,600", PacificRimUITheme.Text, TextAnchor.MiddleRight, 15, 0, false);
            bossHpNumTxt.rectTransform.anchorMin = new Vector2(0.66f, 1f);
            bossHpNumTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            bossHpNumTxt.rectTransform.pivot = new Vector2(1f, 1f);
            bossHpNumTxt.rectTransform.offsetMin = new Vector2(0f, -26f);
            bossHpNumTxt.rectTransform.offsetMax = new Vector2(-18f, -6f);

            CreateProgressBar("BossHealthBar", bossPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -31f), new Vector2(-36f, 12f), PacificRimUITheme.Danger, out var bossHpBar);

            var bossPhaseTxt = CreateStyledText("BossPhaseText", bossPanel.transform, PacificRimTextStyle.Caption,
                "PHASE I // BRAWLER", PacificRimUITheme.BlueBright, TextAnchor.MiddleLeft, 11, 2, true);
            bossPhaseTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
            bossPhaseTxt.rectTransform.anchorMax = new Vector2(0.34f, 0f);
            bossPhaseTxt.rectTransform.pivot = new Vector2(0f, 0f);
            bossPhaseTxt.rectTransform.offsetMin = new Vector2(18f, 8f);
            bossPhaseTxt.rectTransform.offsetMax = new Vector2(0f, 25f);

            var bossStatusTxt = CreateStyledText("BossStatusText", bossPanel.transform, PacificRimTextStyle.Caption,
                "ENGAGING", PacificRimUITheme.Amber, TextAnchor.MiddleCenter, 11, 2, true);
            bossStatusTxt.rectTransform.anchorMin = new Vector2(0.35f, 0f);
            bossStatusTxt.rectTransform.anchorMax = new Vector2(0.68f, 0f);
            bossStatusTxt.rectTransform.pivot = new Vector2(0.5f, 0f);
            bossStatusTxt.rectTransform.offsetMin = new Vector2(0f, 8f);
            bossStatusTxt.rectTransform.offsetMax = new Vector2(0f, 25f);

            CreateProgressBar("BossSubBar", bossPanel.transform, new Vector2(0.70f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 15f), new Vector2(-18f, 4f), PacificRimUITheme.Amber, out var bossSubBar);

            // 5. Top-Center Notification Banner (cleanly separated below Boss Health Bar)
            var bannerGo = CreateRect("Notification_Banner", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(540f, 42f));
            SetPivot(bannerGo, 0.5f, 1f);
            var bannerCg = bannerGo.AddComponent<CanvasGroup>();
            bannerCg.alpha = 0f;
            bannerCg.interactable = false;
            bannerCg.blocksRaycasts = false;

            var bannerChamfer = CreateChamferPanel(bannerGo.transform, "BannerBg", 8f, PacificRimUITheme.PanelOpaque, PacificRimUITheme.Blue);
            bannerChamfer.raycastTarget = false;
            CreateAccentBar(bannerGo.transform, "BannerAccent", true, 3f, PacificRimUITheme.BlueBright);
            var bannerTxt = CreateStyledText("BannerText", bannerGo.transform, PacificRimTextStyle.Caption,
                "NEURAL HANDSHAKE COMPLETE", PacificRimUITheme.Text, TextAnchor.MiddleCenter, 13, 2, true);
            Stretch(bannerTxt.rectTransform, new Vector2(16f, 0f), new Vector2(-16f, 0f));

            // 5. Bottom-Left Hull & Overcharge Panel
            var blPanel = CreateRect("BottomLeft_HullPanel", root.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(44f, 82f), new Vector2(390f, 196f));
            SetPivot(blPanel, 0f, 0f);
            CreateChamferPanel(blPanel.transform, "PanelFrame", 12f, PacificRimUITheme.Panel, PacificRimUITheme.Line).raycastTarget = false;
            CreateAccentBar(blPanel.transform, "LeftAccent", true, 3f, PacificRimUITheme.Blue);

            var hullCaptionGo = CreateRect("HullCaption", blPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -14f), new Vector2(-36f, 20f));
            SetPivot(hullCaptionGo, 0f, 1f);
            hullCaptionGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(18f, -14f);
            hullCaptionGo.GetComponent<RectTransform>().sizeDelta = new Vector2(-36f, 20f);
            var hullCaptionTxt = CreateStyledText("Text", hullCaptionGo.transform, PacificRimTextStyle.Caption,
                "HULL INTEGRITY", PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 12, 2, true);
            Stretch(hullCaptionTxt.rectTransform);

            var hullNumGo = CreateRect("HullNumber", blPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -34f), new Vector2(-36f, 40f));
            SetPivot(hullNumGo, 0f, 1f);
            hullNumGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(18f, -34f);
            hullNumGo.GetComponent<RectTransform>().sizeDelta = new Vector2(-36f, 40f);
            var hullNumTxt = CreateStyledText("NumberText", hullNumGo.transform, PacificRimTextStyle.Number,
                "1,000", PacificRimUITheme.Health, TextAnchor.MiddleLeft, 34, 0, false);
            Stretch(hullNumTxt.rectTransform);

            var hullBarGo = CreateProgressBar("HullBar", blPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -80f), new Vector2(-36f, 10f), PacificRimUITheme.Blue, out var hullBar);

            var staminaCaptionTxt = CreateStyledText("StaminaCaptionText", blPanel.transform, PacificRimTextStyle.Caption,
                "ACTUATOR STAMINA", PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 10, 2, true);
            staminaCaptionTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
            staminaCaptionTxt.rectTransform.anchorMax = new Vector2(0.68f, 1f);
            staminaCaptionTxt.rectTransform.pivot = new Vector2(0f, 1f);
            staminaCaptionTxt.rectTransform.offsetMin = new Vector2(18f, -110f);
            staminaCaptionTxt.rectTransform.offsetMax = new Vector2(0f, -94f);

            var staminaValueTxt = CreateStyledText("StaminaValueText", blPanel.transform, PacificRimTextStyle.Number,
                "100%", PacificRimUITheme.Text, TextAnchor.MiddleRight, 12, 1, false);
            staminaValueTxt.rectTransform.anchorMin = new Vector2(0.68f, 1f);
            staminaValueTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            staminaValueTxt.rectTransform.pivot = new Vector2(1f, 1f);
            staminaValueTxt.rectTransform.offsetMin = new Vector2(0f, -110f);
            staminaValueTxt.rectTransform.offsetMax = new Vector2(-18f, -94f);

            var staminaBarGo = CreateProgressBar("StaminaBar", blPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -122f), new Vector2(-36f, 8f), new Color(0.28f, 0.88f, 0.96f, 1f), out var staminaBar);
            var staminaBarRt = staminaBarGo.GetComponent<RectTransform>();
            staminaBarRt.pivot = new Vector2(0f, 1f);
            staminaBarRt.offsetMin = new Vector2(18f, -122f);
            staminaBarRt.offsetMax = new Vector2(-18f, -114f);

            // Dedicated Overcharge Container inside Bottom-Left panel
            var overchargeGo = CreateRect("Overcharge_Container", blPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 16f), new Vector2(-36f, 38f));
            SetPivot(overchargeGo, 0f, 0f);
            overchargeGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(18f, 16f);
            overchargeGo.GetComponent<RectTransform>().sizeDelta = new Vector2(-36f, 38f);

            var ocStatusTxt = CreateStyledText("OverchargeLabel", overchargeGo.transform, PacificRimTextStyle.Caption,
                "TURBINE OVERCHARGE", PacificRimUITheme.BlueBright, TextAnchor.MiddleLeft, 11, 2, true);
            SetTopStrip(ocStatusTxt.rectTransform, 0f, 0.72f, 18f);

            var ocTimerTxt = CreateStyledText("OverchargeTimer", overchargeGo.transform, PacificRimTextStyle.Number,
                "15.0S", PacificRimUITheme.Text, TextAnchor.MiddleRight, 13, 0, true);
            SetTopStrip(ocTimerTxt.rectTransform, 0.72f, 1f, 18f);

            CreateProgressBar("OverchargeBar", overchargeGo.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 7f), PacificRimUITheme.BlueBright, out var ocBar);
            overchargeGo.SetActive(false);

            // 6. Bottom-Right Combat Matrix Panel
            var brPanel = CreateRect("BottomRight_CombatPanel", root.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-44f, 82f), new Vector2(340f, 118f));
            SetPivot(brPanel, 1f, 0f);
            CreateChamferPanel(brPanel.transform, "PanelFrame", 12f, PacificRimUITheme.Panel, PacificRimUITheme.Line).raycastTarget = false;
            CreateAccentBar(brPanel.transform, "RightAccent", false, 3f, PacificRimUITheme.Blue);

            var stanceRow = BuildMatrixRow("StanceRow", brPanel.transform, -16f, "COMBAT READY", "STANDBY", out var stanceValTxt);
            var blockRow = BuildMatrixRow("DefenseRow", brPanel.transform, -48f, "DEFENSE MATRIX", "OPEN", out var blockValTxt);
            var depthRow = BuildMatrixRow("DepthRow", brPanel.transform, -80f, "BATHYMETRY", "SEA DEPTH 0.0M", out var depthValTxt);

            // 7. Bottom Footer Rule + Battlefront Tactical Prompts
            var footerRule = CreateRect("HUD_BottomRule", root.transform, new Vector2(0.03f, 0f), new Vector2(0.97f, 0f), new Vector2(0f, 54f), new Vector2(0f, 18f));
            SetPivot(footerRule, 0.5f, 0.5f);
            var brGraphic = footerRule.AddComponent<UIBottomRuleGraphic>();
            brGraphic.lineColor = PacificRimUITheme.Line;
            brGraphic.accentColor = PacificRimUITheme.Blue;
            brGraphic.raycastTarget = false;

            var promptsRow = CreateRect("HUD_PromptsStrip", root.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1180f, 32f));
            SetPivot(promptsRow, 0.5f, 0f);
            var hLayout = promptsRow.AddComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.spacing = 24f;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;

            var promptLmb    = CreateKeyPrompt("Prompt_LMB", promptsRow.transform, "LMB", "LIGHT", 44f, 110f);
            var promptRmb    = CreateKeyPrompt("Prompt_RMB", promptsRow.transform, "RMB", "HEAVY", 44f, 110f);
            var promptBlock  = CreateKeyPrompt("Prompt_Block", promptsRow.transform, "F", "BLOCK", 34f, 100f);
            var promptOver   = CreateKeyPrompt("Prompt_Overcharge", promptsRow.transform, "Q", "OVERCHARGE", 34f, 148f);
            var promptMenu   = CreateKeyPrompt("Prompt_Menu", promptsRow.transform, "ESC", "MENU", 44f, 100f);

            // Wire serialized fields into JaegerHUD
            SetSerializedField(hud, "chassisLabelText", chassisTxt);
            SetSerializedField(hud, "linkStatusText", linkTxt);
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
            SetSerializedField(hud, "combatStanceValueText", stanceValTxt);
            SetSerializedField(hud, "defenseMatrixValueText", blockValTxt);
            SetSerializedField(hud, "depthTelemetryText", depthValTxt);
            SetSerializedField(hud, "holoReticle", reticle);
            SetSerializedField(hud, "overchargeScreenCueGroup", cueCg);
            SetSerializedField(hud, "stancePrompt", null);
            SetSerializedField(hud, "lightAttackPrompt", promptLmb);
            SetSerializedField(hud, "heavyAttackPrompt", promptRmb);
            SetSerializedField(hud, "defensePrompt", promptBlock);
            SetSerializedField(hud, "overchargePrompt", promptOver);
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
        // IN-GAME PAUSE MENU (SIMPLIFIED & USEFUL)
        // =====================================================================

        public static GameObject BuildPauseMenu(Transform parent, GameObject hudRoot, JaegerHUD hud)
        {
            var menuRoot = CreateRect("MainMenu_Root", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var mainMenu = menuRoot.AddComponent<MainMenuUI>();

            // Child visuals root toggled by SetMenuState so MainMenu_Root always listens for Esc
            var visualsRoot = CreateRect("PauseVisuals_Root", menuRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var menuCg = visualsRoot.AddComponent<CanvasGroup>();

            // 1. Semi-translucent dark vignette backdrop (0.75 alpha)
            var bdGo = CreateRect("Backdrop_Dim", visualsRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bdImg = bdGo.AddComponent<Image>();
            bdImg.color = new Color(0.02f, 0.04f, 0.08f, 0.75f);

            // 2. Center Tactical Dialog Card (560px wide x 630px high)
            var dialogGo = CreateRect("Pause_DialogBox", visualsRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 630f));
            CreateChamferPanel(dialogGo.transform, "Dialog_Frame", 14f, PacificRimUITheme.PanelOpaque, PacificRimUITheme.Blue);
            CreateAccentBar(dialogGo.transform, "Dialog_LeftAccent", true, 4f, PacificRimUITheme.BlueBright);

            // Header Title
            var titleTxt = CreateStyledText("Pause_Title", dialogGo.transform, PacificRimTextStyle.Heading,
                "PACIFIC RIM: OPERATION PITFALL", PacificRimUITheme.BlueBright, TextAnchor.MiddleCenter, 18, 2, true);
            SetAnchorTop(titleTxt.rectTransform, 0f, -22f, 520f, 28f);

            var subTxt = CreateStyledText("Pause_Subtitle", dialogGo.transform, PacificRimTextStyle.Caption,
                "MISSION DIRECTIVE // DEFEND STRIKER EUREKA", PacificRimUITheme.TextDim, TextAnchor.MiddleCenter, 11, 2, true);
            SetAnchorTop(subTxt.rectTransform, 0f, -50f, 520f, 20f);

            CreateDivider("Pause_Divider", dialogGo.transform, -74f, 480f);

            // Live Telemetry Box (Making it truly useful!)
            var telemBox = CreateRect("Telemetry_Box", dialogGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -135f), new Vector2(480f, 96f));
            CreateChamferPanel(telemBox.transform, "Telem_Frame", 8f, PacificRimUITheme.Panel, PacificRimUITheme.Line);
            CreateAccentBar(telemBox.transform, "Telem_Accent", true, 3f, PacificRimUITheme.Blue);

            var chassisLabel = CreateStyledText("ChassisInfo", telemBox.transform, PacificRimTextStyle.Caption,
                "CHASSIS: GIPSY DANGER // MARK-III JAEGER", PacificRimUITheme.Text, TextAnchor.MiddleLeft, 12, 2, true);
            SetAnchorTopLeft(chassisLabel.rectTransform, 16f, -12f, 440f, 20f);

            var healthTxt = CreateStyledText("HealthInfo", telemBox.transform, PacificRimTextStyle.Caption,
                "HULL INTEGRITY: 1000 / 1000 HP // SYSTEM OPTIMAL", PacificRimUITheme.Success, TextAnchor.MiddleLeft, 12, 2, true);
            SetAnchorTopLeft(healthTxt.rectTransform, 16f, -36f, 440f, 20f);

            var stanceTxt = CreateStyledText("StanceInfo", telemBox.transform, PacificRimTextStyle.Caption,
                "MISSION: SAVE STRIKER EUREKA // INTERCEPT LEATHERBACK", PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 11, 2, true);
            SetAnchorTopLeft(stanceTxt.rectTransform, 16f, -60f, 440f, 20f);

            // Action Buttons Group (Clean vertical stack)
            var btnResume = CreateChamferButton("Btn_Resume", dialogGo.transform, Vector2.zero, new Vector2(460f, 48f),
                "RESUME OPERATION", isPrimary: true, isDanger: false);
            SetAnchorTop(btnResume.GetComponent<RectTransform>(), 0f, -210f, 460f, 48f);

            var btnControls = CreateChamferButton("Btn_Controls", dialogGo.transform, Vector2.zero, new Vector2(460f, 44f),
                "COMBAT CONTROLS", isPrimary: false, isDanger: false);
            SetAnchorTop(btnControls.GetComponent<RectTransform>(), 0f, -268f, 460f, 44f);

            var btnRestart = CreateChamferButton("Btn_Restart", dialogGo.transform, Vector2.zero, new Vector2(460f, 44f),
                "RESTART MISSION", isPrimary: false, isDanger: false);
            SetAnchorTop(btnRestart.GetComponent<RectTransform>(), 0f, -322f, 460f, 44f);

            var btnMainMenu = CreateChamferButton("Btn_MainMenu", dialogGo.transform, Vector2.zero, new Vector2(460f, 44f),
                "RETURN TO MAIN MENU", isPrimary: false, isDanger: false);
            SetAnchorTop(btnMainMenu.GetComponent<RectTransform>(), 0f, -376f, 460f, 44f);

            var btnExit = CreateChamferButton("Btn_Exit", dialogGo.transform, Vector2.zero, new Vector2(460f, 44f),
                "DISENGAGE TO DESKTOP", isPrimary: false, isDanger: true);
            SetAnchorTop(btnExit.GetComponent<RectTransform>(), 0f, -430f, 460f, 44f);

            // Footer prompt strip
            var promptsGo = CreateRect("Pause_Prompts", dialogGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(460f, 32f));
            var pLayout = promptsGo.AddComponent<HorizontalLayoutGroup>();
            pLayout.childAlignment = TextAnchor.MiddleCenter;
            pLayout.spacing = 30f;
            pLayout.childForceExpandWidth = false;
            pLayout.childForceExpandHeight = false;

            CreateKeyPrompt("Prompt_Esc", promptsGo.transform, "ESC", "RESUME", 50f, 90f);
            CreateKeyPrompt("Prompt_Enter", promptsGo.transform, "ENTER", "CONFIRM", 65f, 90f);

            // Modals: Controls Modal
            var controlsModal = BuildControlsModal(visualsRoot.transform);
            controlsModal.gameObject.SetActive(false);

            // Wire serialized fields on MainMenuUI
            SetSerializedField(mainMenu, "menuVisualsRoot", visualsRoot);
            SetSerializedField(mainMenu, "menuCanvasGroup", menuCg);
            SetSerializedField(mainMenu, "hudRoot", hudRoot);
            SetSerializedField(mainMenu, "deployButton", btnResume);
            SetSerializedField(mainMenu, "controlsButton", btnControls);
            SetSerializedField(mainMenu, "restartButton", btnRestart);
            SetSerializedField(mainMenu, "returnToMenuButton", btnMainMenu);
            SetSerializedField(mainMenu, "exitButton", btnExit);
            SetSerializedField(mainMenu, "controlsModal", controlsModal);
            SetSerializedField(mainMenu, "jaegerHud", hud);
            SetSerializedField(mainMenu, "pauseTitleText", titleTxt);
            SetSerializedField(mainMenu, "pauseSubtitleText", subTxt);
            SetSerializedField(mainMenu, "pauseHealthText", healthTxt);
            SetSerializedField(mainMenu, "pauseMissionText", stanceTxt);

            return menuRoot;
        }

        // =====================================================================
        // MAIN MENU (BATTLEFRONT UI KIT + PACIFIC RIM BLUE)
        // =====================================================================

        public static GameObject BuildMainMenu(Transform parent, GameObject hudRoot, JaegerHUD hud)
        {
            var menuRoot = CreateRect("MainMenu_Root", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var mainMenu = menuRoot.AddComponent<MainMenuUI>();

            // Child visuals root toggled by SetMenuState so MainMenu_Root always listens for Esc
            var visualsRoot = CreateRect("MenuVisuals_Root", menuRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var menuCg = visualsRoot.AddComponent<CanvasGroup>();

            // 1. UI.cinematic Backdrop at 0.88 alpha with Haze, 7 Blobs, 60 Embers, 4 Vignettes
            var backdrop = BuildCinematicBackdrop(visualsRoot.transform);

            // 2. Top Navigation Tab Bar (UI.tabBar)
            var topBar = CreateRect("Top_TabBar", visualsRoot.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(80f, -38f), new Vector2(-160f, 54f));
            SetPivot(topBar, 0.5f, 1f);
            topBar.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -38f);
            topBar.GetComponent<RectTransform>().sizeDelta = new Vector2(-160f, 54f);

            var tabBar = BuildTabBar(topBar.transform, out var tabButtons);

            // Right sector status caption on top bar
            string initBest = PacificRimCombatRecords.FormatTime(PacificRimCombatRecords.GetBestTime());
            var sectorCaption = CreateStyledText("SectorStatus", topBar.transform, PacificRimTextStyle.Caption,
                $"SHATTERDOME // SECTOR 07  [BEST TIME: {initBest}]", PacificRimUITheme.BlueBright, TextAnchor.MiddleRight, 13, 2, true);
            sectorCaption.rectTransform.anchorMin = new Vector2(0.55f, 0f);
            sectorCaption.rectTransform.anchorMax = new Vector2(1f, 1f);
            sectorCaption.rectTransform.offsetMin = Vector2.zero;
            sectorCaption.rectTransform.offsetMax = Vector2.zero;

            // 3. Safe Center Layout (Left Hero Column + Right 3x Cards Column)
            var safeArea = CreateRect("SafeContentArea", visualsRoot.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            safeArea.GetComponent<RectTransform>().offsetMin = new Vector2(96f, 128f);
            safeArea.GetComponent<RectTransform>().offsetMax = new Vector2(-96f, -120f);

            // Left Column
            var leftCol = CreateRect("Left_HeroColumn", safeArea.transform, new Vector2(0f, 0f), new Vector2(0.58f, 1f), Vector2.zero, Vector2.zero);

            // Section Title ("PPDC COMMAND INTERFACE") with 20x2 blue bar + hairline
            var sectionHeader = BuildSectionTitle("SectionHeader", leftCol.transform, -20f, "PAN PACIFIC DEFENSE CORPS // THEATER PROTOCOL");

            // Display Title ("PACIFIC RIM: OPERATION PITFALL")
            var pageTitle = CreateStyledText("PageTitle", leftCol.transform, PacificRimTextStyle.Display,
                "PACIFIC RIM: OPERATION PITFALL", PacificRimUITheme.Text, TextAnchor.UpperLeft, 40, 2, true);
            var ptRt = pageTitle.rectTransform;
            ptRt.anchorMin = new Vector2(0f, 1f);
            ptRt.anchorMax = new Vector2(1f, 1f);
            ptRt.pivot = new Vector2(0f, 1f);
            ptRt.anchoredPosition = new Vector2(0f, -64f);
            ptRt.sizeDelta = new Vector2(0f, 68f);

            var subTitle = CreateStyledText("SubTitle", leftCol.transform, PacificRimTextStyle.Caption,
                "PPDC TACTICAL COMMAND // MISSION: DEFEND STRIKER EUREKA", PacificRimUITheme.Amber, TextAnchor.MiddleLeft, 14, 2, true);
            var stRt = subTitle.rectTransform;
            stRt.anchorMin = new Vector2(0f, 1f);
            stRt.anchorMax = new Vector2(1f, 1f);
            stRt.pivot = new Vector2(0f, 1f);
            stRt.anchoredPosition = new Vector2(0f, -134f);
            stRt.sizeDelta = new Vector2(0f, 24f);

            // Horizontal divider under title
            var titleDiv = CreateDivider("TitleDivider", leftCol.transform, -170f, 480f);

            // Tab Panels Area (Operation / Doctrine / Diagnostics)
            var tabContainer = CreateRect("TabPages_Container", leftCol.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            tabContainer.GetComponent<RectTransform>().offsetMin = new Vector2(0f, 148f);
            tabContainer.GetComponent<RectTransform>().offsetMax = new Vector2(-40f, -192f);

            var panelOperation = BuildTabPageOperation(tabContainer.transform);
            var panelDoctrine = BuildTabPageDoctrine(tabContainer.transform);
            var panelDiagnostics = BuildTabPageDiagnostics(tabContainer.transform);
            panelDoctrine.SetActive(false);
            panelDiagnostics.SetActive(false);

            // Action Buttons Group (Anchored to bottom of Left Column)
            var actionsArea = CreateRect("PrimaryActions", leftCol.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(-40f, 120f));
            SetPivot(actionsArea, 0f, 0f);
            actionsArea.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
            actionsArea.GetComponent<RectTransform>().sizeDelta = new Vector2(-40f, 120f);

            var deployBtn = CreateChamferButton("Btn_Deploy", actionsArea.transform, new Vector2(0f, 64f), new Vector2(532f, 52f),
                "DEPLOY JAEGER", isPrimary: true, isDanger: false);
            SetAnchorTopLeft(deployBtn.GetComponent<RectTransform>(), 0f, 0f, 532f, 52f);

            var controlsBtn = CreateChamferButton("Btn_Controls", actionsArea.transform, Vector2.zero, new Vector2(170f, 44f),
                "CONTROLS", isPrimary: false, isDanger: false);
            SetAnchorTopLeft(controlsBtn.GetComponent<RectTransform>(), 0f, -62f, 170f, 44f);

            var specsBtn = CreateChamferButton("Btn_Specs", actionsArea.transform, Vector2.zero, new Vector2(170f, 44f),
                "SPECS", isPrimary: false, isDanger: false);
            SetAnchorTopLeft(specsBtn.GetComponent<RectTransform>(), 181f, -62f, 170f, 44f);

            var exitBtn = CreateChamferButton("Btn_Exit", actionsArea.transform, Vector2.zero, new Vector2(170f, 44f),
                "DISENGAGE", isPrimary: false, isDanger: true);
            SetAnchorTopLeft(exitBtn.GetComponent<RectTransform>(), 362f, -62f, 170f, 44f);

            // Right Column: 3 Stacked Battlefront Feature Cards
            var rightCol = CreateRect("Right_CardsColumn", safeArea.transform, new Vector2(0.60f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            BuildSectionTitle("CardsSectionTitle", rightCol.transform, -20f, "TACTICAL MODULES");

            var cardsContainer = CreateRect("CardsStack", rightCol.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var ccRt = cardsContainer.GetComponent<RectTransform>();
            ccRt.offsetMin = new Vector2(0f, 0f);
            ccRt.offsetMax = new Vector2(0f, -68f);

            var vLayout = cardsContainer.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 16f;
            vLayout.childAlignment = TextAnchor.UpperLeft;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            var cardDeploy = CreateFeatureCard("Card_Deploy", cardsContainer.transform, 0f, 0f, 114f,
                "01 // OPERATION PITFALL", "SAVE STRIKER EUREKA", "Defend paralyzed Striker Eureka against Leatherback", "READY", PacificRimUITheme.Success);
            var le1 = cardDeploy.GetComponent<LayoutElement>() ?? cardDeploy.gameObject.AddComponent<LayoutElement>();
            le1.preferredHeight = 114f;
            cardDeploy.SetSelected(true);

            var cardControls = CreateFeatureCard("Card_Controls", cardsContainer.transform, 0f, 0f, 114f,
                "02 // NEURAL DRIFT", "COMBAT CONTROLS", "View strike, defense & overcharge keys", "OPEN", PacificRimUITheme.BlueBright);
            var le2 = cardControls.GetComponent<LayoutElement>() ?? cardControls.gameObject.AddComponent<LayoutElement>();
            le2.preferredHeight = 114f;

            var cardSpecs = CreateFeatureCard("Card_Specs", cardsContainer.transform, 0f, 0f, 114f,
                "03 // CHASSIS DOSSIER", "MARK-III SPECIFICATIONS", "Turbine output, hull rating & armaments", "OPEN", PacificRimUITheme.BlueBright);
            var le3 = cardSpecs.GetComponent<LayoutElement>() ?? cardSpecs.gameObject.AddComponent<LayoutElement>();
            le3.preferredHeight = 114f;

            // 4. Bottom Rule (15 ticks + ±35 deg slashes) + Menu Prompts
            var footerRule = CreateRect("Menu_BottomRule", visualsRoot.transform, new Vector2(0.05f, 0f), new Vector2(0.95f, 0f), new Vector2(0f, 76f), new Vector2(0f, 18f));
            SetPivot(footerRule, 0.5f, 0.5f);
            var ruleGraphic = footerRule.AddComponent<UIBottomRuleGraphic>();
            ruleGraphic.lineColor = PacificRimUITheme.Line;
            ruleGraphic.accentColor = PacificRimUITheme.Blue;
            ruleGraphic.raycastTarget = false;

            var menuPrompts = CreateRect("Menu_PromptsStrip", visualsRoot.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(860f, 32f));
            SetPivot(menuPrompts, 0.5f, 0f);
            var mpLayout = menuPrompts.AddComponent<HorizontalLayoutGroup>();
            mpLayout.childAlignment = TextAnchor.MiddleCenter;
            mpLayout.spacing = 36f;
            mpLayout.childForceExpandWidth = false;
            mpLayout.childForceExpandHeight = false;

            CreateKeyPrompt("MenuPrompt_Deploy", menuPrompts.transform, "ENTER", "DEPLOY", 68f, 100f);
            CreateKeyPrompt("MenuPrompt_Esc", menuPrompts.transform, "ESC", "RESUME / CLOSE", 48f, 160f);
            CreateKeyPrompt("MenuPrompt_Click", menuPrompts.transform, "CLICK", "SELECT MODULE", 64f, 150f);

            // 5. Build 820x560 Battlefront Modals
            var controlsModal = BuildControlsModal(visualsRoot.transform);
            var specsModal = BuildSpecsModal(visualsRoot.transform);
            controlsModal.gameObject.SetActive(false);
            specsModal.gameObject.SetActive(false);

            // Wire serialized references onto MainMenuUI
            SetSerializedField(mainMenu, "menuVisualsRoot", visualsRoot);
            SetSerializedField(mainMenu, "menuCanvasGroup", menuCg);
            SetSerializedField(mainMenu, "hudRoot", hudRoot);
            SetSerializedField(mainMenu, "tabBar", tabBar);
            SetSerializedField(mainMenu, "tabPanels", new[] { panelOperation, panelDoctrine, panelDiagnostics });
            SetSerializedField(mainMenu, "featureCards", new[] { cardDeploy, cardControls, cardSpecs });
            SetSerializedField(mainMenu, "deployButton", deployBtn);
            SetSerializedField(mainMenu, "controlsButton", controlsBtn);
            SetSerializedField(mainMenu, "specsButton", specsBtn);
            SetSerializedField(mainMenu, "exitButton", exitBtn);
            SetSerializedField(mainMenu, "controlsModal", controlsModal);
            SetSerializedField(mainMenu, "specsModal", specsModal);
            SetSerializedField(mainMenu, "jaegerHud", hud);
            SetSerializedField(mainMenu, "menuBestTimeText", sectorCaption);

            return menuRoot;
        }

        private static PacificRimCinematicBackdrop BuildCinematicBackdrop(Transform parent)
        {
            var bdGo = CreateRect("Cinematic_Backdrop", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var cg = bdGo.AddComponent<CanvasGroup>();
            cg.alpha = 0.88f;

            var comp = bdGo.AddComponent<PacificRimCinematicBackdrop>();
            comp.backdropGroup = cg;

            // Deep solid fill (blocks raycasts behind menu)
            var baseGo = CreateRect("Base", bdGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var baseImg = baseGo.AddComponent<Image>();
            baseImg.color = PacificRimUITheme.Background;
            baseImg.raycastTarget = true;

            // Vertical deep ocean-blue wash
            var washGo = CreateRect("OceanGradientWash", bdGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var wash = washGo.AddComponent<UIGradientGraphic>();
            wash.SetVertical(
                new Color(10f / 255f, 34f / 255f, 64f / 255f, 0.35f),
                new Color(4f / 255f, 8f / 255f, 14f / 255f, 0.85f));
            wash.raycastTarget = false;

            // Haze horizontal atmospheric layer
            var hazeGo = CreateRect("Haze", bdGo.transform, new Vector2(0f, 0.15f), new Vector2(1f, 0.72f), Vector2.zero, Vector2.zero);
            var haze = hazeGo.AddComponent<UIGradientGraphic>();
            haze.SetHorizontal(
                new Color(PacificRimUITheme.BlueDeep.r, PacificRimUITheme.BlueDeep.g, PacificRimUITheme.BlueDeep.b, 0.28f),
                new Color(PacificRimUITheme.Blue.r, PacificRimUITheme.Blue.g, PacificRimUITheme.Blue.b, 0.08f));
            haze.raycastTarget = false;

            // 7 drifting UISoftBlobGraphic layers
            var blobsRoot = CreateRect("Blobs", bdGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var blobSpecs = new (Vector2 pos, Vector2 size, Color col)[]
            {
                (new Vector2(-620f, 240f), new Vector2(780f, 640f), new Color(18f/255f, 84f/255f, 165f/255f, 0.22f)),
                (new Vector2(540f, -180f), new Vector2(840f, 700f), new Color(12f/255f, 52f/255f, 120f/255f, 0.20f)),
                (new Vector2(-180f, -260f), new Vector2(680f, 520f), new Color(36f/255f, 148f/255f, 226f/255f, 0.14f)),
                (new Vector2(480f, 300f), new Vector2(560f, 480f), new Color(70f/255f, 195f/255f, 255f/255f, 0.10f)),
                (new Vector2(-780f, -140f), new Vector2(600f, 600f), new Color(8f/255f, 38f/255f, 110f/255f, 0.22f)),
                (new Vector2(60f, 120f), new Vector2(720f, 480f), new Color(24f/255f, 110f/255f, 190f/255f, 0.12f)),
                (new Vector2(780f, 40f), new Vector2(540f, 540f), new Color(36f/255f, 148f/255f, 226f/255f, 0.11f)),
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

            // 60 subtle drifting storm/reactor embers
            var embersRoot = CreateRect("Embers", bdGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            for (int i = 0; i < 60; i++)
            {
                float sx = Mathf.Sin((i + 1) * 19.31f);
                float sy = Mathf.Cos((i + 1) * 43.17f);
                float size = (i % 4 == 0) ? 3.5f : 2f;
                var eGo = CreateRect($"Ember_{i}", embersRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(sx * 920f, sy * 510f), new Vector2(size, size));
                var eImg = eGo.AddComponent<Image>();
                eImg.color = (i % 7 == 0)
                    ? new Color(PacificRimUITheme.Amber.r, PacificRimUITheme.Amber.g, PacificRimUITheme.Amber.b, 0.45f)
                    : new Color(PacificRimUITheme.BlueBright.r, PacificRimUITheme.BlueBright.g, PacificRimUITheme.BlueBright.b, 0.55f);
                eImg.raycastTarget = false;
                comp.embers.Add(eGo.GetComponent<RectTransform>());
            }

            // 4 Edge Vignettes (Top, Bottom, Left, Right)
            var vigRoot = CreateRect("Vignettes", bdGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Color edgeDark = new Color(0f, 0f, 0f, 0.72f);
            BuildGradientEdge(vigRoot.transform, "Vignette_Top", new Vector2(0f, 0.80f), new Vector2(1f, 1f), edgeDark, Color.clear);
            BuildGradientEdge(vigRoot.transform, "Vignette_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0.22f), Color.clear, edgeDark);
            BuildGradientEdgeHorizontal(vigRoot.transform, "Vignette_Left", new Vector2(0f, 0f), new Vector2(0.14f, 1f), edgeDark, Color.clear);
            BuildGradientEdgeHorizontal(vigRoot.transform, "Vignette_Right", new Vector2(0.86f, 0f), new Vector2(1f, 1f), Color.clear, edgeDark);

            comp.InitializeMotion();
            return comp;
        }

        private static PacificRimTabBar BuildTabBar(Transform parent, out List<Button> buttons)
        {
            var tb = parent.gameObject.AddComponent<PacificRimTabBar>();
            buttons = new List<Button>();

            // Bottom 1px hairline across entire tab bar
            var lineGo = CreateRect("BottomHairline", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            SetPivot(lineGo, 0.5f, 0f);
            lineGo.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            lineGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 1f);
            var lineDiv = lineGo.AddComponent<UIDividerGraphic>();
            lineDiv.color = PacificRimUITheme.Line;
            lineDiv.fadeEdgePercent = 0.05f;
            lineDiv.raycastTarget = false;

            string[] tabTitles = { "OPERATION", "DOCTRINE", "SYSTEMS" };
            float curX = 0f;
            float tabH = 44f;

            for (int i = 0; i < tabTitles.Length; i++)
            {
                float w = (i == 0) ? 150f : 140f;
                var tabGo = CreateRect($"Tab_{tabTitles[i]}", parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(curX, 2f), new Vector2(w, tabH));
                SetPivot(tabGo, 0f, 0.5f);
                tabGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(curX, 2f);
                tabGo.GetComponent<RectTransform>().sizeDelta = new Vector2(w, tabH);

                var hitImg = tabGo.AddComponent<Image>();
                hitImg.color = Color.clear;
                var btn = tabGo.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;

                var lbl = CreateStyledText("Label", tabGo.transform, PacificRimTextStyle.Caption,
                    tabTitles[i], i == 0 ? PacificRimUITheme.Text : PacificRimUITheme.TextMuted, TextAnchor.MiddleLeft, 14, 2, true);
                Stretch(lbl.rectTransform);

                tb.tabButtons.Add(btn);
                tb.tabLabels.Add(lbl);
                buttons.Add(btn);
                curX += w + 28f;
            }

            // 2px BlueBright active indicator bar
            var indGo = CreateRect("ActiveIndicator", parent, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(150f, 2f));
            SetPivot(indGo, 0f, 0f);
            indGo.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            indGo.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 2f);
            var indImg = indGo.AddComponent<Image>();
            indImg.color = PacificRimUITheme.BlueBright;
            indImg.raycastTarget = false;
            tb.indicatorRect = indGo.GetComponent<RectTransform>();

            return tb;
        }

        private static GameObject BuildTabPageOperation(Transform parent)
        {
            var page = CreateRect("Page_Operation", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var desc = CreateStyledText("BriefingText", page.transform, PacificRimTextStyle.Body,
                "CRITICAL MISSION DIRECTIVE: Category IV Kaiju 'LEATHERBACK' has ambushed Hong Kong Bay and detonated an organic EMP wave, causing total electrical and neural shutdown to Striker Eureka. With Striker paralyzed and defenseless, Gipsy Danger is deployed via heavy tactical airdrop to draw Leatherback's aggression, intercept the Kaiju, and save Striker Eureka from imminent destruction.",
                PacificRimUITheme.TextDim, TextAnchor.UpperLeft, 14, 0, false);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Truncate;
            desc.lineSpacing = 1.2f;
            var dRt = desc.rectTransform;
            dRt.anchorMin = new Vector2(0f, 1f);
            dRt.anchorMax = new Vector2(1f, 1f);
            dRt.pivot = new Vector2(0f, 1f);
            dRt.anchoredPosition = new Vector2(0f, -8f);
            dRt.sizeDelta = new Vector2(0f, 112f);

            BuildUIListRow("OpRow_Weather", page.transform, -126f, "PRIMARY DIRECTIVE", "SAVE STRIKER EUREKA // INTERCEPT LEATHERBACK", true);
            BuildUIListRow("OpRow_Chassis", page.transform, -186f, "ALLIED STATUS", "STRIKER EUREKA: DISABLED BY EMP // UNDER ATTACK", false);
            BuildUIListRow("OpRow_BestRecord", page.transform, -246f, "SPEEDRUN RECORD // BEST TIME", PacificRimCombatRecords.FormatTime(PacificRimCombatRecords.GetBestTime()), true);

            return page;
        }

        private static GameObject BuildTabPageDoctrine(Transform parent)
        {
            var page = CreateRect("Page_Doctrine", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BuildUIListRow("DocRow_1", page.transform, -8f,  "LMB // LIGHT STRIKES", "SEAMLESS 3-HIT PUNCH COMBO CHAIN", true);
            BuildUIListRow("DocRow_2", page.transform, -68f, "RMB // HEAVY ATTACK", "HIGH-IMPACT MELEE SLAM & SHOCKWAVE", false);
            BuildUIListRow("DocRow_3", page.transform, -128f,"F // DEFENSE MATRIX", "RAISES BRACED GUARD TO MITIGATE DAMAGE", false);
            BuildUIListRow("DocRow_4", page.transform, -188f,"Q // OVERCHARGE", "15S TURBINE SURGE (+ATTACK SPEED)", true);
            return page;
        }

        private static GameObject BuildTabPageDiagnostics(Transform parent)
        {
            var page = CreateRect("Page_Diagnostics", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BuildUIListRow("DiagRow_1", page.transform, -8f,  "ARC-9 VORTEX CORE", "100% NOMINAL // COOLANT STABLE", true);
            BuildUIListRow("DiagRow_2", page.transform, -68f, "GYRO STABILIZERS", "DOUBLE-EVENT DAMPING ONLINE", false);
            BuildUIListRow("DiagRow_3", page.transform, -128f,"HYDRO-SHOCK SEALS", "RATED FOR TYPHOON SWELL SURGE", false);
            BuildUIListRow("DiagRow_4", page.transform, -188f,"CHAIN SWORDS // PLASMA", "ARMED // STANDBY IN CON-POD", true);
            return page;
        }

        private static PacificRimModal BuildControlsModal(Transform parent)
        {
            var modal = CreateBaseModal("Controls_Modal", parent, "PILOT COMBAT CONTROLS", "NEURAL HANDSHAKE & JAEGER INPUT MATRIX");

            var rows = new (string key, string val, bool hi)[]
            {
                ("W / A / S / D", "OMNI-DIRECTIONAL STEP LOCOMOTION", false),
                ("LEFT SHIFT", "SPRINT DRIVE (ENGAGE HEAVY RUNNING GAIT)", false),
                ("SPACE", "HEAVY FOOT STOMP (TRIGGER DISPLACEMENT WAVE)", true),
                ("LEFT MOUSE [LMB]", "LIGHT STRIKE COMBO (SEAMLESS PUNCH CHAIN)", false),
                ("RIGHT MOUSE [RMB]", "HEAVY MELEE SLAM (POWERFUL FINISHER)", false),
                ("F", "HOLD TO ENGAGE DEFENSE MATRIX BLOCK", true),
                ("Q", "TURBINE OVERCHARGE EMOTE (+ATTACK SPEED // 15S)", true),
                ("ESC", "TOGGLE SHATTERDOME COMMAND MENU", false),
            };

            for (int i = 0; i < rows.Length; i++)
            {
                BuildUIListRow($"CtrlRow_{i}", modal.contentRoot, -(i * 50f), rows[i].key, rows[i].val, rows[i].hi, 46f);
            }

            return modal;
        }

        private static PacificRimModal BuildSpecsModal(Transform parent)
        {
            var modal = CreateBaseModal("Specs_Modal", parent, "GIPSY DANGER // MARK-III", "PAN PACIFIC DEFENSE CORPS CHASSIS DOSSIER");

            var rows = new (string key, string val, bool hi)[]
            {
                ("CLASSIFICATION", "MARK-III DIGITAL/ANALOG HYBRID", true),
                ("LAUNCH DATE", "JULY 10, 2017 // ANCHORAGE SHATTERDOME", false),
                ("HEIGHT / MASS", "260 FT (79.2 M) // 1,980 TONS", false),
                ("POWER PLANT", "12-KILOTON DOUBLE-CORE NUCLEAR VORTEX TURBINE", true),
                ("ARMAMENT", "I-19 PLASMACASTER // GD6 CHAIN SWORDS", false),
                ("PILOT INTERFACE", "DUAL-MIND NEURAL DRIFT HANDSHAKE", false),
                ("TACTICAL RATING", "SPEED 7  //  STRENGTH 8  //  ARMOR 6", true),
            };

            for (int i = 0; i < rows.Length; i++)
            {
                BuildUIListRow($"SpecRow_{i}", modal.contentRoot, -(i * 52f), rows[i].key, rows[i].val, rows[i].hi, 48f);
            }

            return modal;
        }

        private static PacificRimModal CreateBaseModal(string name, Transform parent, string title, string subtitle)
        {
            var modalGo = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var cg = modalGo.AddComponent<CanvasGroup>();
            var modal = modalGo.AddComponent<PacificRimModal>();
            modal.canvasGroup = cg;

            // Fullscreen dark dimmer
            var dimGo = CreateRect("Dimmer", modalGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dimImg = dimGo.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.72f);
            var dimBtn = dimGo.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            modal.dimmerButton = dimBtn;

            // 820x560 centered chamfered window
            var winGo = CreateRect("Window", modalGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 560f));
            modal.windowRect = winGo.GetComponent<RectTransform>();

            var chamfer = CreateChamferPanel(winGo.transform, "WindowFrame", 14f, PacificRimUITheme.PanelOpaque, PacificRimUITheme.Line);
            chamfer.raycastTarget = true;

            // Top 2px BlueBright header line
            var topAccent = CreateRect("TopAccent", winGo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-28f, 2f));
            SetPivot(topAccent, 0.5f, 1f);
            topAccent.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            topAccent.GetComponent<RectTransform>().sizeDelta = new Vector2(-28f, 2f);
            var topImg = topAccent.AddComponent<Image>();
            topImg.color = PacificRimUITheme.BlueBright;
            topImg.raycastTarget = false;

            // Title + Subtitle
            var titleTxt = CreateStyledText("ModalTitle", winGo.transform, PacificRimTextStyle.Title,
                title, PacificRimUITheme.Text, TextAnchor.MiddleLeft, 26, 2, true);
            SetAnchorTopLeft(titleTxt.rectTransform, 32f, -24f, 640f, 34f);

            var subTxt = CreateStyledText("ModalSubtitle", winGo.transform, PacificRimTextStyle.Caption,
                subtitle, PacificRimUITheme.BlueBright, TextAnchor.MiddleLeft, 13, 2, true);
            SetAnchorTopLeft(subTxt.rectTransform, 32f, -60f, 640f, 22f);

            modal.titleText = titleTxt;
            modal.subtitleText = subTxt;

            // Circular 40x40 Close ("X") Button
            var closeGo = CreateRect("Btn_Close", winGo.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -26f), new Vector2(40f, 40f));
            SetPivot(closeGo, 1f, 1f);
            closeGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-28f, -26f);
            closeGo.GetComponent<RectTransform>().sizeDelta = new Vector2(40f, 40f);

            var circle = closeGo.AddComponent<UICircleOutlineGraphic>();
            circle.fillColor = new Color(8f / 255f, 14f / 255f, 24f / 255f, 0.75f);
            circle.borderColor = PacificRimUITheme.Line;

            var closeTxt = CreateStyledText("X", closeGo.transform, PacificRimTextStyle.Heading, "X", PacificRimUITheme.Text, TextAnchor.MiddleCenter, 16, 0, true);
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

            // Divider under header
            CreateDivider("HeaderDivider", winGo.transform, -96f, 756f, 32f);

            // Body ContentRoot
            var contentGo = CreateRect("ContentRoot", winGo.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var cRt = contentGo.GetComponent<RectTransform>();
            cRt.offsetMin = new Vector2(32f, 24f);
            cRt.offsetMax = new Vector2(-32f, -112f);
            modal.contentRoot = cRt;

            return modal;
        }

        // =====================================================================
        // PRIMITIVE HELPERS
        // =====================================================================

        private static GameObject BuildSectionTitle(string name, Transform parent, float yPos, string text)
        {
            var go = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, yPos), new Vector2(0f, 24f));
            SetPivot(go, 0f, 1f);
            go.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, yPos);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 24f);

            var tick = CreateRect("AccentTick", go.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(20f, 2f));
            SetPivot(tick, 0f, 0.5f);
            tick.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            tick.GetComponent<RectTransform>().sizeDelta = new Vector2(20f, 2f);
            var tickImg = tick.AddComponent<Image>();
            tickImg.color = PacificRimUITheme.Blue;
            tickImg.raycastTarget = false;

            var lbl = CreateStyledText("Label", go.transform, PacificRimTextStyle.Caption, text, PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 13, 2, true);
            lbl.rectTransform.anchorMin = Vector2.zero;
            lbl.rectTransform.anchorMax = Vector2.one;
            lbl.rectTransform.offsetMin = new Vector2(30f, 0f);
            lbl.rectTransform.offsetMax = Vector2.zero;

            return go;
        }

        private static GameObject BuildUIListRow(string name, Transform parent, float yPos, string leftTitle, string rightValue, bool highlight, float height = 52f)
        {
            var rowGo = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, yPos), new Vector2(0f, height));
            SetPivot(rowGo, 0f, 1f);
            rowGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, yPos);
            rowGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, height);

            var bg = rowGo.AddComponent<Image>();
            bg.color = highlight ? PacificRimUITheme.RowSelectedFill : PacificRimUITheme.RowFill;
            bg.raycastTarget = false;

            // 1px bottom hairline
            var border = CreateRect("BottomLine", rowGo.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f));
            SetPivot(border, 0.5f, 0f);
            border.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            border.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 1f);
            var bImg = border.AddComponent<Image>();
            bImg.color = PacificRimUITheme.Line;
            bImg.raycastTarget = false;

            if (highlight)
            {
                CreateAccentBar(rowGo.transform, "RowAccent", true, 3f, PacificRimUITheme.BlueBright);
            }

            var lTxt = CreateStyledText("LeftKey", rowGo.transform, PacificRimTextStyle.Caption,
                leftTitle, highlight ? PacificRimUITheme.BlueBright : PacificRimUITheme.Text, TextAnchor.MiddleLeft, 13, 2, true);
            lTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
            lTxt.rectTransform.anchorMax = new Vector2(0.42f, 1f);
            lTxt.rectTransform.offsetMin = new Vector2(18f, 0f);
            lTxt.rectTransform.offsetMax = Vector2.zero;

            var rTxt = CreateStyledText("RightValue", rowGo.transform, PacificRimTextStyle.Small,
                rightValue, PacificRimUITheme.TextDim, TextAnchor.MiddleRight, 14, 1, true);
            rTxt.rectTransform.anchorMin = new Vector2(0.42f, 0f);
            rTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            rTxt.rectTransform.offsetMin = Vector2.zero;
            rTxt.rectTransform.offsetMax = new Vector2(-18f, 0f);

            return rowGo;
        }

        private static PacificRimCard CreateFeatureCard(string name, Transform parent, float yPos, float width, float height,
            string kicker, string title, string subtitle, string badge, Color badgeColor)
        {
            var cardGo = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, yPos), new Vector2(0f, height));
            SetPivot(cardGo, 0f, 1f);
            cardGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, yPos);
            cardGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, height);

            var cardBtn = cardGo.AddComponent<Button>();
            cardBtn.transition = Selectable.Transition.None;
            var card = cardGo.AddComponent<PacificRimCard>();
            card.unityButton = cardBtn;
            var chamfer = CreateChamferPanel(cardGo.transform, "Chamfer", 10f, PacificRimUITheme.CardFill, PacificRimUITheme.Line);
            chamfer.raycastTarget = true;
            card.chamferGraphic = chamfer;
            card.accentBar = CreateAccentBar(cardGo.transform, "LeftAccent", true, 3f, PacificRimUITheme.Blue);

            var kickerTxt = CreateStyledText("Kicker", cardGo.transform, PacificRimTextStyle.Caption,
                kicker, PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 12, 1, true);
            kickerTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
            kickerTxt.rectTransform.anchorMax = new Vector2(0.70f, 1f);
            kickerTxt.rectTransform.pivot = new Vector2(0f, 1f);
            kickerTxt.rectTransform.anchoredPosition = new Vector2(20f, -14f);
            kickerTxt.rectTransform.sizeDelta = new Vector2(0f, 18f);
            kickerTxt.raycastTarget = false;

            var badgeTxt = CreateStyledText("Badge", cardGo.transform, PacificRimTextStyle.Caption,
                badge, badgeColor, TextAnchor.MiddleRight, 12, 1, true);
            badgeTxt.rectTransform.anchorMin = new Vector2(0.70f, 1f);
            badgeTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            badgeTxt.rectTransform.pivot = new Vector2(1f, 1f);
            badgeTxt.rectTransform.anchoredPosition = new Vector2(-20f, -14f);
            badgeTxt.rectTransform.sizeDelta = new Vector2(0f, 18f);
            badgeTxt.raycastTarget = false;

            var titleTxt = CreateStyledText("Title", cardGo.transform, PacificRimTextStyle.Heading,
                title, PacificRimUITheme.Text, TextAnchor.MiddleLeft, 20, 1, true);
            titleTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleTxt.rectTransform.pivot = new Vector2(0f, 1f);
            titleTxt.rectTransform.anchoredPosition = new Vector2(20f, -40f);
            titleTxt.rectTransform.sizeDelta = new Vector2(-40f, 30f);
            titleTxt.raycastTarget = false;

            var subTxt = CreateStyledText("Subtitle", cardGo.transform, PacificRimTextStyle.Small,
                subtitle, PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 13, 0, false);
            subTxt.rectTransform.anchorMin = new Vector2(0f, 1f);
            subTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
            subTxt.rectTransform.pivot = new Vector2(0f, 1f);
            subTxt.rectTransform.anchoredPosition = new Vector2(20f, -74f);
            subTxt.rectTransform.sizeDelta = new Vector2(-40f, 24f);
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
            chamfer.chamferSize = 9f;
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
                text, PacificRimUITheme.Text, TextAnchor.MiddleCenter, isPrimary ? 16 : 14, 1, true);
            Stretch(lbl.rectTransform);
            lbl.raycastTarget = false;
            btn.label = lbl;

            return btn;
        }

        private static PacificRimKeyPrompt CreateKeyPrompt(string name, Transform parent, string key, string action, float badgeWidth, float actionWidth)
        {
            var go = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(badgeWidth + 10f + actionWidth, 28f));
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = badgeWidth + 10f + actionWidth;
            le.preferredHeight = 28f;

            var prompt = go.AddComponent<PacificRimKeyPrompt>();

            var badgeGo = CreateRect("KeyBadge", go.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(badgeWidth, 24f));
            SetPivot(badgeGo, 0f, 0.5f);
            badgeGo.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            badgeGo.GetComponent<RectTransform>().sizeDelta = new Vector2(badgeWidth, 24f);

            var badgeChamfer = badgeGo.AddComponent<UIChamferGraphic>();
            badgeChamfer.chamferSize = 4f;
            badgeChamfer.borderThickness = 1f;
            badgeChamfer.chamferTopLeft = true;
            badgeChamfer.chamferTopRight = false;
            badgeChamfer.chamferBottomRight = true;
            badgeChamfer.chamferBottomLeft = false;
            badgeChamfer.fillColor = new Color(8f / 255f, 12f / 255f, 18f / 255f, 0.55f);
            badgeChamfer.borderColor = PacificRimUITheme.Line;
            badgeChamfer.raycastTarget = false;

            var keyTxt = CreateStyledText("KeyText", badgeGo.transform, PacificRimTextStyle.Caption,
                key, PacificRimUITheme.Text, TextAnchor.MiddleCenter, 11, 1, true);
            Stretch(keyTxt.rectTransform);
            keyTxt.raycastTarget = false;

            var actGo = CreateRect("ActionLabel", go.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var aRt = actGo.GetComponent<RectTransform>();
            aRt.offsetMin = new Vector2(badgeWidth + 10f, 0f);
            aRt.offsetMax = Vector2.zero;
            var actTxt = CreateStyledText("Text", actGo.transform, PacificRimTextStyle.Caption,
                action, PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, 12, 2, true);
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
            bgGo.GetComponent<RectTransform>().anchoredPosition = anchoredPos;
            bgGo.GetComponent<RectTransform>().sizeDelta = sizeDelta;

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
            var row = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, yPos), new Vector2(-32f, 22f));
            SetPivot(row, 0f, 1f);
            row.GetComponent<RectTransform>().anchoredPosition = new Vector2(16f, yPos);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(-32f, 22f);

            var lTxt = CreateStyledText("Label", row.transform, PacificRimTextStyle.Caption, label, PacificRimUITheme.TextMuted, TextAnchor.MiddleLeft, 12, 2, true);
            lTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
            lTxt.rectTransform.anchorMax = new Vector2(0.48f, 1f);
            lTxt.rectTransform.offsetMin = Vector2.zero;
            lTxt.rectTransform.offsetMax = Vector2.zero;

            valueTxt = CreateStyledText("Value", row.transform, PacificRimTextStyle.Caption, defaultVal, PacificRimUITheme.TextDim, TextAnchor.MiddleRight, 13, 1, true);
            valueTxt.rectTransform.anchorMin = new Vector2(0.48f, 0f);
            valueTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
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
                onLeft ? new Vector2(0f, 0.12f) : new Vector2(1f, 0.12f),
                onLeft ? new Vector2(0f, 0.88f) : new Vector2(1f, 0.88f),
                Vector2.zero, new Vector2(width, 0f));
            SetPivot(go, onLeft ? 0f : 1f, 0.5f);
            go.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 0f);
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
            int tracking = trackingOverride >= 0 ? trackingOverride : spec.Tracking;

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
            txt.text = tracking > 0 ? PacificRimUITheme.Track(content, tracking, fontSize, upperOverride) : content;
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

        private static void SetTopStrip(RectTransform rt, float xMin, float xMax, float height)
        {
            rt.anchorMin = new Vector2(xMin, 1f);
            rt.anchorMax = new Vector2(xMax, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -height);
            rt.offsetMax = Vector2.zero;
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
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
