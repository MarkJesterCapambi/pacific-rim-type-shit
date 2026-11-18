using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PacificRimWater;

namespace PacificRimUI
{
    /// <summary>
    /// Minimalist tactical Admin & Debug Console toggled via typing "hesoyam".
    /// Consolidates all legacy cheat overrides alongside live Enemy AI controls,
    /// attack pattern forcing, Jaeger overrides, and simulation utilities.
    /// Docked to the right screen edge so center-frame combat remains unobscured.
    /// </summary>
    public class PacificRimAdminUI : MonoBehaviour
    {
        private static PacificRimAdminUI instance;
        public static PacificRimAdminUI Instance => instance;
        public static bool IsOpen => instance != null && instance.isOpen;

        private const float DrawerWidth = 396f;
        private const float DrawerMargin = 16f;

        private readonly List<Action> refreshCallbacks = new List<Action>();

        private Canvas adminCanvas;
        private CanvasGroup drawerGroup;
        private RectTransform drawerRect;
        private ScrollRect scrollRect;
        private Text statusSummaryText;
        private Font uiFont;

        private bool isOpen;
        private bool prevCursorVisible;
        private CursorLockMode prevCursorLockState = CursorLockMode.Locked;
        private float lastTelemetryRefresh;

        public static void Toggle()
        {
            var inst = EnsureInstance();
            if (inst.isOpen) inst.HideInternal();
            else inst.ShowInternal();
        }

        public static void Open() => EnsureInstance().ShowInternal();

        public static void Close()
        {
            if (instance != null && instance.isOpen)
            {
                instance.HideInternal();
            }
        }

        private static PacificRimAdminUI EnsureInstance()
        {
            if (instance != null) return instance;

            var existing = FindAnyObjectByType<PacificRimAdminUI>();
            if (existing != null)
            {
                instance = existing;
                return instance;
            }

            var go = new GameObject("PacificRim_AdminConsole");
            instance = go.AddComponent<PacificRimAdminUI>();
            DontDestroyOnLoad(go);
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            BuildUI();
            ApplyClosedImmediate();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (instance == this)
            {
                instance = null;
            }
            Time.timeScale = 1f;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Mathf.Abs(Time.timeScale - 1f) > 0.001f)
            {
                Time.timeScale = 1f;
            }
            if (isOpen)
            {
                HideInternal(restoreCursor: false);
            }
            RefreshBindingsAndTelemetry();
        }

        private void Update()
        {
            if (!isOpen) return;

            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Time.unscaledTime - lastTelemetryRefresh >= 0.1f)
            {
                lastTelemetryRefresh = Time.unscaledTime;
                RefreshBindingsAndTelemetry();
            }
        }

        private void ShowInternal()
        {
            if (adminCanvas == null) BuildUI();
            if (isOpen) return;

            prevCursorVisible = Cursor.visible;
            prevCursorLockState = Cursor.lockState;

            isOpen = true;
            drawerGroup.alpha = 1f;
            drawerGroup.interactable = true;
            drawerGroup.blocksRaycasts = true;
            drawerRect.gameObject.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            RefreshBindingsAndTelemetry();
        }

        private void HideInternal(bool restoreCursor = true)
        {
            isOpen = false;
            if (drawerGroup != null)
            {
                drawerGroup.alpha = 0f;
                drawerGroup.interactable = false;
                drawerGroup.blocksRaycasts = false;
            }
            if (drawerRect != null)
            {
                drawerRect.gameObject.SetActive(false);
            }

            if (restoreCursor && !MainMenuUI.IsMenuOpen)
            {
                Cursor.lockState = prevCursorLockState == CursorLockMode.None ? CursorLockMode.Locked : prevCursorLockState;
                Cursor.visible = false;
            }
        }

        private void ApplyClosedImmediate()
        {
            isOpen = false;
            if (drawerGroup != null)
            {
                drawerGroup.alpha = 0f;
                drawerGroup.interactable = false;
                drawerGroup.blocksRaycasts = false;
            }
            if (drawerRect != null)
            {
                drawerRect.gameObject.SetActive(false);
            }
        }

        private void BuildUI()
        {
            refreshCallbacks.Clear();
            uiFont = ResolveFont();

            adminCanvas = gameObject.AddComponent<Canvas>();
            adminCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            adminCanvas.sortingOrder = 600;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // Docked right-hand drawer container
            var drawerGo = new GameObject("AdminDrawer", typeof(RectTransform), typeof(CanvasGroup), typeof(UIChamferGraphic));
            drawerGo.transform.SetParent(transform, false);
            drawerRect = drawerGo.GetComponent<RectTransform>();
            drawerRect.anchorMin = new Vector2(1f, 0f);
            drawerRect.anchorMax = new Vector2(1f, 1f);
            drawerRect.pivot = new Vector2(1f, 0.5f);
            drawerRect.anchoredPosition = new Vector2(-DrawerMargin, 0f);
            drawerRect.sizeDelta = new Vector2(DrawerWidth, -(DrawerMargin * 2f));

            drawerGroup = drawerGo.GetComponent<CanvasGroup>();
            var bg = drawerGo.GetComponent<UIChamferGraphic>();
            bg.chamferSize = 12f;
            bg.borderThickness = 1.25f;
            bg.chamferTopLeft = false;
            bg.chamferTopRight = true;
            bg.chamferBottomLeft = true;
            bg.chamferBottomRight = false;
            bg.SetColors(new Color(7f / 255f, 10f / 255f, 16f / 255f, 1f), new Color(PacificRimUITheme.Blue.r, PacificRimUITheme.Blue.g, PacificRimUITheme.Blue.b, 0.45f));

            // Top accent rule
            var topAccent = CreateGraphic("TopAccent", drawerRect, new Color(PacificRimUITheme.Blue.r, PacificRimUITheme.Blue.g, PacificRimUITheme.Blue.b, 0.90f));
            topAccent.anchorMin = new Vector2(0f, 1f);
            topAccent.anchorMax = new Vector2(1f, 1f);
            topAccent.pivot = new Vector2(0.5f, 1f);
            topAccent.anchoredPosition = Vector2.zero;
            topAccent.sizeDelta = new Vector2(-12f, 2f);

            // Header strip
            var header = CreateRect("Header", drawerRect);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = new Vector2(0f, -12f);
            header.sizeDelta = new Vector2(-28f, 44f);

            CreateLabel("Eyebrow", header, "SYS // OVERRIDE", 10, FontStyle.Bold, PacificRimUITheme.BlueBright, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0.65f, 1f), Vector2.zero, new Vector2(0f, 14f));
            CreateLabel("Title", header, "ADMIN DEBUG CONSOLE", 17, FontStyle.Bold, PacificRimUITheme.Text, TextAnchor.LowerLeft,
                new Vector2(0f, 0f), new Vector2(0.68f, 1f), Vector2.zero, new Vector2(0f, 26f));

            var closeBtn = CreateStyledButton(header, "CLOSE [ESC]", () =>
            {
                PacificRimUISound.PlayBack();
                HideInternal();
            }, height: 28f, fontSize: 11);
            var closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 0.5f);
            closeRect.anchorMax = new Vector2(1f, 0.5f);
            closeRect.pivot = new Vector2(1f, 0.5f);
            closeRect.anchoredPosition = Vector2.zero;
            closeRect.sizeDelta = new Vector2(96f, 28f);

            // Telemetry summary card
            var telemetryGo = new GameObject("TelemetryCard", typeof(RectTransform), typeof(UIChamferGraphic));
            telemetryGo.transform.SetParent(drawerRect, false);
            var telemetryRect = telemetryGo.GetComponent<RectTransform>();
            telemetryRect.anchorMin = new Vector2(0f, 1f);
            telemetryRect.anchorMax = new Vector2(1f, 1f);
            telemetryRect.pivot = new Vector2(0.5f, 1f);
            telemetryRect.anchoredPosition = new Vector2(0f, -64f);
            telemetryRect.sizeDelta = new Vector2(-28f, 68f);

            var telemetryBg = telemetryGo.GetComponent<UIChamferGraphic>();
            telemetryBg.chamferSize = 7f;
            telemetryBg.borderThickness = 1f;
            telemetryBg.chamferTopLeft = true;
            telemetryBg.chamferTopRight = false;
            telemetryBg.chamferBottomLeft = false;
            telemetryBg.chamferBottomRight = true;
            telemetryBg.SetColors(PacificRimUITheme.RowFill, new Color(1f, 1f, 1f, 0.14f));

            statusSummaryText = CreateLabel("TelemetryText", telemetryRect, "INITIALIZING TELEMETRY...", 11, FontStyle.Normal,
                PacificRimUITheme.TextDim, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20f, -10f));
            statusSummaryText.lineSpacing = 1.15f;

            // Scrollable control surface
            var scrollGo = new GameObject("ControlsScroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            scrollGo.transform.SetParent(drawerRect, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(14f, 14f);
            scrollRt.offsetMax = new Vector2(-14f, -140f);

            var maskImg = scrollGo.GetComponent<Image>();
            maskImg.color = new Color(1f, 1f, 1f, 0.01f);
            scrollGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollRt, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = scrollRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;

            PopulateSections(contentRect);

            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        private void PopulateSections(RectTransform content)
        {
            // 1. KAIJU AI & STATE
            CreateSectionHeader(content, "KAIJU AI & COMBAT STATE");

            var aiRow = CreateHorizontalRow(content, 30f, 6f);
            CreateToggleButton(aiRow,
                () =>
                {
                    var k = FindKaiju();
                    if (k == null) return "AI: NO KAIJU";
                    return k.IsAIDisabled ? "AI: OFFLINE" : "AI: ACTIVE";
                },
                () =>
                {
                    var k = FindKaiju();
                    if (k == null) return;
                    k.SetAIDisabled(!k.IsAIDisabled);
                    ShowToast(k.IsAIDisabled ? "ADMIN // KAIJU AI DEACTIVATED" : "ADMIN // KAIJU AI REACTIVATED");
                },
                () =>
                {
                    var k = FindKaiju();
                    return k != null && !k.IsAIDisabled;
                },
                activeTint: PacificRimUITheme.Success,
                inactiveTint: PacificRimUITheme.Danger);

            CreateToggleButton(aiRow,
                () =>
                {
                    var k = FindKaiju();
                    return (k != null && k.IsBaguvixActive) ? "PACIFIST: ON" : "PACIFIST: OFF";
                },
                () =>
                {
                    var k = FindKaiju();
                    if (k == null) return;
                    k.SetPacifistMode(!k.IsBaguvixActive);
                    ShowToast(k.IsBaguvixActive ? "ADMIN // KAIJU PACIFIST ENGAGED" : "ADMIN // KAIJU PACIFIST CLEARED");
                },
                () =>
                {
                    var k = FindKaiju();
                    return k != null && k.IsBaguvixActive;
                },
                activeTint: PacificRimUITheme.Amber);

            var phaseRow = CreateHorizontalRow(content, 28f, 6f);
            for (int p = 1; p <= 3; p++)
            {
                int phaseTarget = p;
                CreateToggleButton(phaseRow,
                    () => $"PHASE {phaseTarget}",
                    () =>
                    {
                        var k = FindKaiju();
                        if (k == null) return;
                        k.DebugSetPhase(phaseTarget);
                        ShowToast($"ADMIN // BOSS PHASE {phaseTarget}");
                    },
                    () =>
                    {
                        var k = FindKaiju();
                        return k != null && k.CurrentPhase == phaseTarget && !k.IsDead;
                    });
            }
            CreateActionButton(phaseRow, "WARP 18M", WarpKaijuInFrontOfJaeger, 28f, 11);

            var reactRow = CreateHorizontalRow(content, 28f, 6f);
            CreateActionButton(reactRow, "EMP ROAR", () => InvokeKaiju(k => k.DebugForceEmpRoar(), "EMP ROAR"), 28f, 11);
            CreateActionButton(reactRow, "BLOCK", () => InvokeKaiju(k => k.DebugForceBlock(), "BLOCK"), 28f, 11);
            CreateActionButton(reactRow, "STAGGER", () => InvokeKaiju(k => k.DebugForceStagger(), "STAGGER"), 28f, 11);

            var lifeRow = CreateHorizontalRow(content, 28f, 6f);
            CreateActionButton(lifeRow, "RESET BOSS", () => InvokeKaiju(k => k.DebugResetBoss(), "BOSS RESET"), 28f, 11);
            CreateActionButton(lifeRow, "KILL BOSS", () => InvokeKaiju(k => k.DebugKillBoss(), "KILL BOSS"), 28f, 11, PacificRimUITheme.Danger);

            // 2. FORCE KAIJU ATTACK PATTERN
            CreateSectionHeader(content, "FORCE KAIJU ATTACK");

            AddAttackPair(content,
                ("LEFT HOOK", KaijuAI.AttackPattern.LeftHook),
                ("RIGHT HOOK", KaijuAI.AttackPattern.RightHook));
            AddAttackPair(content,
                ("DOUBLE HOOK", KaijuAI.AttackPattern.DoubleHookCombo),
                ("TRIPLE BRAWL", KaijuAI.AttackPattern.TripleBrawlerCombo));
            AddAttackPair(content,
                ("HEAVY SLAM", KaijuAI.AttackPattern.HeavySlam),
                ("SPRINT SLAM", KaijuAI.AttackPattern.SprintChargeSlam));
            AddAttackPair(content,
                ("ABYSSAL DART", KaijuAI.AttackPattern.AbyssalDartStrike),
                ("BLITZ 4-HIT", KaijuAI.AttackPattern.DeviousBlitzLightCombo));

            // 3. JAEGER OVERRIDES
            CreateSectionHeader(content, "JAEGER SYSTEMS");

            var jaegerToggles = CreateHorizontalRow(content, 30f, 6f);
            CreateToggleButton(jaegerToggles,
                () => IsJaegerFlag(j => j.IsGodModeActive) ? "GOD: ON" : "GOD: OFF",
                () => InvokeJaeger(j =>
                {
                    j.SetGodMode(!j.IsGodModeActive);
                    ShowToast(j.IsGodModeActive ? "ADMIN // GOD MODE ON" : "ADMIN // GOD MODE OFF");
                }),
                () => IsJaegerFlag(j => j.IsGodModeActive),
                activeTint: PacificRimUITheme.Success);

            CreateToggleButton(jaegerToggles,
                () => IsJaegerFlag(j => j.IsIronmanCheatActive) ? "3X DMG: ON" : "3X DMG: OFF",
                () => InvokeJaeger(j =>
                {
                    j.SetIronmanCheat(!j.IsIronmanCheatActive);
                    ShowToast(j.IsIronmanCheatActive ? "ADMIN // IRONMAN 3X ON" : "ADMIN // IRONMAN OFF");
                }),
                () => IsJaegerFlag(j => j.IsIronmanCheatActive),
                activeTint: PacificRimUITheme.BlueBright);

            CreateToggleButton(jaegerToggles,
                () => IsJaegerFlag(j => j.IsInfiniteStaminaActive) ? "INF STM: ON" : "INF STM: OFF",
                () => InvokeJaeger(j =>
                {
                    j.SetInfiniteStamina(!j.IsInfiniteStaminaActive);
                    ShowToast(j.IsInfiniteStaminaActive ? "ADMIN // INFINITE STAMINA ON" : "ADMIN // INFINITE STAMINA OFF");
                }),
                () => IsJaegerFlag(j => j.IsInfiniteStaminaActive),
                activeTint: PacificRimUITheme.BlueBright);

            var jaegerActs1 = CreateHorizontalRow(content, 28f, 6f);
            CreateActionButton(jaegerActs1, "HEAL FULL", () => InvokeJaeger(j => { j.DebugRestoreFull(); ShowToast("ADMIN // JAEGER RESTORED"); }), 28f, 11);
            CreateActionButton(jaegerActs1, "OVERCHARGE", () => InvokeJaeger(j => { j.DebugActivateOvercharge(); ShowToast("ADMIN // OVERCHARGE ACTIVE"); }), 28f, 11);
            CreateActionButton(jaegerActs1, "SWORDS", () => InvokeJaeger(j => j.DebugToggleChainswords()), 28f, 11);

            var jaegerActs2 = CreateHorizontalRow(content, 28f, 6f);
            CreateActionButton(jaegerActs2, "FILL PLASMA ULT", () => InvokeJaeger(j => { j.DebugFillUltimateCharge(); ShowToast("ADMIN // PLASMA ULT 100%"); }), 28f, 11);
            CreateActionButton(jaegerActs2, "TEST DMG -200", () => InvokeJaeger(j => j.DebugApplyDamage(200f)), 28f, 11, PacificRimUITheme.Amber);

            // 4. SIMULATION & VIEW
            CreateSectionHeader(content, "SIMULATION & VIEW");

            var timeRow = CreateHorizontalRow(content, 28f, 5f);
            float[] scales = { 0f, 0.25f, 0.5f, 1f, 2f };
            string[] scaleLabels = { "0X", ".25X", ".5X", "1X", "2X" };
            for (int i = 0; i < scales.Length; i++)
            {
                float s = scales[i];
                string lbl = scaleLabels[i];
                CreateToggleButton(timeRow,
                    () => lbl,
                    () =>
                    {
                        Time.timeScale = s;
                        RefreshBindingsAndTelemetry();
                    },
                    () => Mathf.Abs(Time.timeScale - s) < 0.02f);
            }

            var viewRow = CreateHorizontalRow(content, 28f, 6f);
            CreateToggleButton(viewRow,
                () => (JaegerCameraFollow.Instance != null && JaegerCameraFollow.Instance.isFreeCam) ? "FREECAM: ON" : "FREECAM: OFF",
                () => JaegerCameraFollow.Instance?.ToggleFreeCam(),
                () => JaegerCameraFollow.Instance != null && JaegerCameraFollow.Instance.isFreeCam);

            CreateToggleButton(viewRow,
                () => JaegerHUD.IsHudHidden ? "HUD: HIDDEN" : "HUD: VISIBLE",
                () => JaegerHUD.Instance?.SetHudHidden(!JaegerHUD.IsHudHidden),
                () => !JaegerHUD.IsHudHidden);

            CreateActionButton(viewRow, "RESET ALL", ResetAllOverrides, 28f, 11, PacificRimUITheme.Amber);
        }

        private void AddAttackPair(RectTransform content, (string label, KaijuAI.AttackPattern pattern) left, (string label, KaijuAI.AttackPattern pattern) right)
        {
            var row = CreateHorizontalRow(content, 28f, 6f);
            CreateToggleButton(row,
                () => left.label,
                () => ForceAttack(left.pattern),
                () => IsKaijuExecuting(left.pattern));
            CreateToggleButton(row,
                () => right.label,
                () => ForceAttack(right.pattern),
                () => IsKaijuExecuting(right.pattern));
        }

        private void ForceAttack(KaijuAI.AttackPattern pattern)
        {
            var k = FindKaiju();
            if (k == null) return;
            k.DebugForceAttack(pattern);
            RefreshBindingsAndTelemetry();
        }

        private void InvokeKaiju(Action<KaijuAI> action, string toast = null)
        {
            var k = FindKaiju();
            if (k == null) return;
            action?.Invoke(k);
            if (!string.IsNullOrEmpty(toast)) ShowToast($"ADMIN // {toast}");
            RefreshBindingsAndTelemetry();
        }

        private void InvokeJaeger(Action<SimpleJaegerController> action)
        {
            var j = FindJaeger();
            if (j == null) return;
            action?.Invoke(j);
            RefreshBindingsAndTelemetry();
        }

        private void WarpKaijuInFrontOfJaeger()
        {
            var j = FindJaeger();
            var k = FindKaiju();
            if (j == null || k == null) return;

            Vector3 forward = j.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            float targetDist = Mathf.Clamp(k.meleeAttackRange > 1f ? k.meleeAttackRange : 18f, 16f, 24f);
            Vector3 dest = j.transform.position + forward * targetDist;
            dest.y = k.transform.position.y;

            Quaternion lookRot = Quaternion.LookRotation(-forward, Vector3.up);
            var rb = k.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = dest;
                rb.rotation = lookRot;
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
#endif
            }
            k.transform.SetPositionAndRotation(dest, lookRot);
            ShowToast($"ADMIN // KAIJU WARPED {targetDist:0}M");
            RefreshBindingsAndTelemetry();
        }

        private void ResetAllOverrides()
        {
            Time.timeScale = 1f;

            var k = FindKaiju();
            if (k != null)
            {
                k.SetPacifistMode(false);
                k.SetAIDisabled(false);
            }

            var j = FindJaeger();
            if (j != null)
            {
                j.SetGodMode(false);
                j.SetIronmanCheat(false);
                j.SetInfiniteStamina(false);
            }

            if (JaegerCameraFollow.Instance != null && JaegerCameraFollow.Instance.isFreeCam)
            {
                JaegerCameraFollow.Instance.ToggleFreeCam();
            }

            JaegerHUD.Instance?.SetHudHidden(false);
            ShowToast("ADMIN // ALL OVERRIDES RESET");
            RefreshBindingsAndTelemetry();
        }

        private void RefreshBindingsAndTelemetry()
        {
            for (int i = 0; i < refreshCallbacks.Count; i++)
            {
                refreshCallbacks[i]?.Invoke();
            }

            if (statusSummaryText == null) return;

            var j = FindJaeger();
            var k = FindKaiju();

            string jLine = j != null
                ? $"JAEGER  HP {Mathf.CeilToInt(j.currentHealth)}/{Mathf.CeilToInt(j.maxHealth)}  |  STM {Mathf.CeilToInt(j.currentStamina)}/{Mathf.CeilToInt(j.maxStamina)}"
                : "JAEGER  NOT FOUND";

            string kLine;
            if (k != null)
            {
                string aiMode = k.IsDead ? "DEAD" : (k.IsAIDisabled ? "AI OFF" : (k.IsBaguvixActive ? "PACIFIST" : "AI ON"));
                string distText = j != null ? $" | {Vector3.Distance(j.transform.position, k.transform.position):0.0}M" : "";
                kLine = $"{k.kaijuDisplayName}  P{k.CurrentPhase} {Mathf.CeilToInt(k.currentHealth)}/{Mathf.CeilToInt(k.maxHealth)}  [{aiMode}]";
                string stateLine = $"STATE: {k.currentState.ToString().ToUpperInvariant()} ({k.activePattern}){distText}  |  {Time.timeScale:0.##}X";
                statusSummaryText.text = $"{jLine}\n{kLine}\n{stateLine}";
            }
            else
            {
                statusSummaryText.text = $"{jLine}\nKAIJU   NONE IN SCENE\nSIM     TIMESCALE {Time.timeScale:0.##}X";
            }
        }

        private bool IsJaegerFlag(Func<SimpleJaegerController, bool> predicate)
        {
            var j = FindJaeger();
            return j != null && predicate(j);
        }

        private bool IsKaijuExecuting(KaijuAI.AttackPattern pattern)
        {
            var k = FindKaiju();
            return k != null && k.currentState == KaijuAI.AIState.Attacking && k.activePattern == pattern;
        }

        private static SimpleJaegerController FindJaeger()
        {
            return SimpleJaegerController.Instance != null
                ? SimpleJaegerController.Instance
                : FindAnyObjectByType<SimpleJaegerController>();
        }

        private static KaijuAI FindKaiju()
        {
            return KaijuAI.Instance != null
                ? KaijuAI.Instance
                : FindAnyObjectByType<KaijuAI>();
        }

        private static void ShowToast(string msg)
        {
            JaegerHUD.Instance?.ShowBanner(msg, 2.2f);
        }

        private Font ResolveFont()
        {
            var f = PacificRimUITheme.GetDefaultFont();
            if (f != null) return f;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void CreateSectionHeader(RectTransform parent, string title)
        {
            var holder = new GameObject("Sec_" + title, typeof(RectTransform), typeof(LayoutElement));
            holder.transform.SetParent(parent, false);
            var le = holder.GetComponent<LayoutElement>();
            le.preferredHeight = 20f;

            var rt = holder.GetComponent<RectTransform>();
            CreateLabel("Label", rt, title, 10, FontStyle.Bold, PacificRimUITheme.BlueBright, TextAnchor.LowerLeft,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var line = CreateGraphic("Rule", rt, new Color(1f, 1f, 1f, 0.14f));
            line.anchorMin = new Vector2(0f, 0f);
            line.anchorMax = new Vector2(1f, 0f);
            line.pivot = new Vector2(0.5f, 0f);
            line.sizeDelta = new Vector2(0f, 1f);
        }

        private RectTransform CreateHorizontalRow(RectTransform parent, float height, float spacing)
        {
            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowGo.transform.SetParent(parent, false);
            var le = rowGo.GetComponent<LayoutElement>();
            le.preferredHeight = height;

            var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            return rowGo.GetComponent<RectTransform>();
        }

        private Button CreateActionButton(RectTransform parent, string label, Action onClick, float height = 28f, int fontSize = 11, Color? accentBorder = null)
        {
            var btn = CreateStyledButton(parent, label, () =>
            {
                PacificRimUISound.PlayClick();
                onClick?.Invoke();
            }, height, fontSize);

            if (accentBorder.HasValue)
            {
                var bg = btn.GetComponent<UIChamferGraphic>();
                var c = accentBorder.Value;
                bg.SetColors(new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 0.65f), new Color(c.r, c.g, c.b, 0.72f));
            }
            return btn;
        }

        private void CreateToggleButton(
            RectTransform parent,
            Func<string> labelProvider,
            Action onClick,
            Func<bool> isActiveProvider,
            Color? activeTint = null,
            Color? inactiveTint = null)
        {
            var btn = CreateStyledButton(parent, labelProvider(), () =>
            {
                PacificRimUISound.PlayClick();
                onClick?.Invoke();
                RefreshBindingsAndTelemetry();
            }, height: 28f, fontSize: 11);

            var bg = btn.GetComponent<UIChamferGraphic>();
            var txt = btn.GetComponentInChildren<Text>();
            Color activeCol = activeTint ?? PacificRimUITheme.Blue;

            refreshCallbacks.Add(() =>
            {
                if (btn == null || bg == null || txt == null) return;
                txt.text = labelProvider();
                bool active = isActiveProvider != null && isActiveProvider();
                if (active)
                {
                    bg.SetColors(
                        new Color(activeCol.r * 0.22f, activeCol.g * 0.30f, activeCol.b * 0.42f, 0.85f),
                        new Color(activeCol.r, activeCol.g, activeCol.b, 0.90f));
                    txt.color = PacificRimUITheme.Text;
                }
                else if (inactiveTint.HasValue)
                {
                    Color inc = inactiveTint.Value;
                    bg.SetColors(
                        new Color(inc.r * 0.22f, inc.g * 0.14f, inc.b * 0.16f, 0.65f),
                        new Color(inc.r, inc.g, inc.b, 0.70f));
                    txt.color = PacificRimUITheme.Text;
                }
                else
                {
                    bg.SetColors(PacificRimUITheme.RowFill, new Color(1f, 1f, 1f, 0.16f));
                    txt.color = PacificRimUITheme.TextDim;
                }
            });
        }

        private Button CreateStyledButton(RectTransform parent, string label, Action onClick, float height, int fontSize)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(UIChamferGraphic), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = height;

            var chamfer = go.GetComponent<UIChamferGraphic>();
            chamfer.chamferSize = 5f;
            chamfer.borderThickness = 1f;
            chamfer.chamferTopLeft = false;
            chamfer.chamferTopRight = true;
            chamfer.chamferBottomLeft = true;
            chamfer.chamferBottomRight = false;
            chamfer.SetColors(PacificRimUITheme.RowFill, new Color(1f, 1f, 1f, 0.18f));
            chamfer.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = chamfer;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.22f, 1.28f, 1.35f, 1f);
            colors.pressedColor = new Color(0.8f, 0.9f, 1f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            CreateLabel("Label", go.GetComponent<RectTransform>(), label, fontSize, FontStyle.Bold,
                PacificRimUITheme.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8f, 0f));

            return btn;
        }

        private Text CreateLabel(
            string name,
            RectTransform parent,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor align,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var t = go.GetComponent<Text>();
            t.font = uiFont;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            t.text = content;
            return t;
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static RectTransform CreateGraphic(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return go.GetComponent<RectTransform>();
        }
    }
}
