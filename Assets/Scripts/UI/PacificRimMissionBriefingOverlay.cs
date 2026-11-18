using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace PacificRimUI
{
    /// <summary>
    /// Pre-cutscene PPDC tactical briefing overlay displaying mission lore, threat assessment,
    /// and a "CLICK TO CONTINUE" prompt before launching a level's intro cutscene.
    /// </summary>
    public class PacificRimMissionBriefingOverlay : MonoBehaviour
    {
        public struct BriefingData
        {
            public string Eyebrow;
            public string Title;
            public string Sector;
            public string Threat;
            public string UnitStatus;
            public string Body;
            public Color AccentColor;
            public string ButtonLabel;
        }

        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Text eyebrowText;
        private Text titleText;
        private Text sectorValueText;
        private Text threatValueText;
        private Text unitValueText;
        private Text bodyText;
        private Text pulsePromptText;
        private Image leftAccentBar;
        private UIChamferGraphic panelGraphic;
        private PacificRimButton continueButton;

        private bool confirmed;

        public static IEnumerator ShowSaveStrikerEurekaBriefing()
        {
            var data = new BriefingData
            {
                Eyebrow = "SHATTERDOME TACTICAL BRIEFING  //  OPERATION 01",
                Title = "SAVE STRIKER EUREKA",
                Sector = "HONG KONG BAY · SECTOR 04",
                Threat = "CAT IV  ·  LEATHERBACK",
                UnitStatus = "GIPSY DANGER (MARK-III)",
                Body =
                    "Category IV Kaiju <color=#ff963c><b>LEATHERBACK</b></color> has surfaced in Hong Kong Harbor and unleashed a devastating Bio-EMP shockwave—paralyzing Mark-V Jaeger <color=#46c3ff><b>STRIKER EUREKA</b></color> in waist-deep water.\n\n" +
                    "Only <color=#46c3ff><b>GIPSY DANGER</b></color>'s analog nuclear reactor survived the pulse. Four CH-47 Chinooks are airlifting you directly into the storm.\n\n" +
                    "<color=#f2f2f4><b>MISSION:</b></color> Drop into the bay, intercept Leatherback before it destroys Striker Eureka, and neutralize the Category IV threat.",
                AccentColor = PacificRimUITheme.Amber,
                ButtonLabel = "CLICK TO CONTINUE"
            };
            yield return ShowAndWait(data);
        }

        public static IEnumerator ShowOperationPitfallBriefing()
        {
            var data = new BriefingData
            {
                Eyebrow = "PPDC CLASSIFIED BRIEFING  //  OPERATION 02",
                Title = "OPERATION PITFALL",
                Sector = "MARIANA TRENCH · 11,000M",
                Threat = "CAT V  ·  SLATTERN",
                UnitStatus = "SOLO DEPLOYMENT · GIPSY DANGER",
                Body =
                    "Although Leatherback was neutralized in Hong Kong Bay, its Bio-EMP discharge severely damaged Striker Eureka's digital core—leaving no other Jaeger operational for the final assault.\n\n" +
                    "<color=#46c3ff><b>GIPSY DANGER</b></color> must descend <color=#ff963c><b>ALONE</b></color> into the crushing blackness of the Mariana Trench. Guarding the tectonic rift is <color=#ff5050><b>SLATTERN</b></color>, the first-ever Category V Kaiju.\n\n" +
                    "<color=#f2f2f4><b>MISSION:</b></color> Defeat Slattern on the trench floor, drag its Kaiju carcass into the Breach to trigger the genetic DNA lock, and detonate your nuclear core inside the Anteverse to seal the Breach forever.",
                AccentColor = PacificRimUITheme.BlueBright,
                ButtonLabel = "CLICK TO CONTINUE"
            };
            yield return ShowAndWait(data);
        }

        public static IEnumerator ShowAndWait(BriefingData data)
        {
            EnsureEventSystem();

            var go = new GameObject("PacificRim_MissionBriefingOverlay");
            var overlay = go.AddComponent<PacificRimMissionBriefingOverlay>();
            overlay.BuildUI(data);
            yield return overlay.RunRoutine();
            Destroy(go);
        }

        private static void EnsureEventSystem()
        {
            var es = FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                es = esGo.AddComponent<EventSystem>();
            }
#if ENABLE_INPUT_SYSTEM
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null) Destroy(legacy);
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

        private IEnumerator RunRoutine()
        {
            CursorLockMode prevLock = Cursor.lockState;
            bool prevVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            canvasGroup.alpha = 0f;
            float fadeIn = 0.28f;
            float elapsed = 0f;
            while (elapsed < fadeIn)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeIn));
                yield return null;
            }
            canvasGroup.alpha = 1f;

            // Small grace frame so stray clicks from the loading transition don't skip the briefing instantly
            yield return new WaitForSecondsRealtime(0.18f);
            confirmed = false;

            while (!confirmed)
            {
                if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }

                if (pulsePromptText != null)
                {
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.4f);
                    Color c = PacificRimUITheme.BlueBright;
                    pulsePromptText.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.42f, 0.95f, pulse));
                }

                if (WasConfirmInputPressed())
                {
                    Confirm();
                }

                yield return null;
            }

            PacificRimUISound.PlaySuccess();

            float fadeOut = 0.28f;
            elapsed = 0f;
            while (elapsed < fadeOut)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeOut));
                yield return null;
            }
            canvasGroup.alpha = 0f;

            Cursor.lockState = prevLock;
            Cursor.visible = prevVisible;
        }

        private void Confirm()
        {
            if (confirmed) return;
            confirmed = true;
        }

        private static bool WasConfirmInputPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    return true;
            }
            catch
            {
            }
            return false;
        }

        private void BuildUI(BriefingData data)
        {
            Font font = PacificRimUITheme.GetDefaultFont();

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 650;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            // Fullscreen dark scrim & click-anywhere trigger
            var bgGo = new GameObject("BriefingBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            bgGo.transform.SetParent(transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(4f / 255f, 6f / 255f, 10f / 255f, 0.94f);
            var bgBtn = bgGo.GetComponent<Button>();
            bgBtn.transition = Selectable.Transition.None;
            bgBtn.onClick.AddListener(Confirm);

            // Subtle top & bottom gradient vignettes
            var topVig = new GameObject("TopGlow", typeof(RectTransform), typeof(UIGradientGraphic));
            topVig.transform.SetParent(bgGo.transform, false);
            var topVigRt = topVig.GetComponent<RectTransform>();
            topVigRt.anchorMin = new Vector2(0f, 0.72f);
            topVigRt.anchorMax = Vector2.one;
            topVigRt.offsetMin = Vector2.zero;
            topVigRt.offsetMax = Vector2.zero;
            var topGrad = topVig.GetComponent<UIGradientGraphic>();
            topGrad.SetVertical(new Color(data.AccentColor.r, data.AccentColor.g, data.AccentColor.b, 0.14f), Color.clear);
            topGrad.raycastTarget = false;

            // Centered tactical briefing card (860 x 540)
            var panelGo = new GameObject("BriefingCard", typeof(RectTransform));
            panelGo.transform.SetParent(transform, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(860f, 540f);

            panelGraphic = panelGo.AddComponent<UIChamferGraphic>();
            panelGraphic.chamferSize = 14f;
            panelGraphic.borderThickness = 1.25f;
            panelGraphic.fillColor = PacificRimUITheme.PanelOpaque;
            panelGraphic.borderColor = new Color(data.AccentColor.r, data.AccentColor.g, data.AccentColor.b, 0.65f);
            panelGraphic.raycastTarget = true;

            // Left vertical accent bar
            var accentGo = new GameObject("LeftAccent", typeof(RectTransform), typeof(Image));
            accentGo.transform.SetParent(panelGo.transform, false);
            var accentRt = accentGo.GetComponent<RectTransform>();
            accentRt.anchorMin = new Vector2(0f, 0.10f);
            accentRt.anchorMax = new Vector2(0f, 0.90f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.sizeDelta = new Vector2(4f, 0f);
            leftAccentBar = accentGo.GetComponent<Image>();
            leftAccentBar.color = data.AccentColor;
            leftAccentBar.raycastTarget = false;

            // Decorative subtle hologram reticle on top right corner
            var retGo = new GameObject("HeaderReticle", typeof(RectTransform), typeof(UIHoloReticleGraphic));
            retGo.transform.SetParent(panelGo.transform, false);
            var retRt = retGo.GetComponent<RectTransform>();
            retRt.anchorMin = new Vector2(1f, 1f);
            retRt.anchorMax = new Vector2(1f, 1f);
            retRt.pivot = new Vector2(1f, 1f);
            retRt.anchoredPosition = new Vector2(-34f, -26f);
            retRt.sizeDelta = new Vector2(56f, 56f);
            var ret = retGo.GetComponent<UIHoloReticleGraphic>();
            ret.color = new Color(data.AccentColor.r, data.AccentColor.g, data.AccentColor.b, 0.45f);
            ret.ringThickness = 1.2f;
            ret.raycastTarget = false;

            // Eyebrow & Title
            eyebrowText = CreateText("Eyebrow", panelGo.transform, font, 12, FontStyle.Bold, data.AccentColor, TextAnchor.MiddleLeft, data.Eyebrow);
            SetTopLeft(eyebrowText.rectTransform, 44f, -30f, 700f, 20f);

            titleText = CreateText("Title", panelGo.transform, font, 34, FontStyle.Bold, PacificRimUITheme.Text, TextAnchor.MiddleLeft, data.Title);
            SetTopLeft(titleText.rectTransform, 44f, -54f, 720f, 44f);

            // Metadata Strip (3 cells)
            var metaRow = new GameObject("MetadataStrip", typeof(RectTransform));
            metaRow.transform.SetParent(panelGo.transform, false);
            var metaRt = metaRow.GetComponent<RectTransform>();
            SetTopLeft(metaRt, 44f, -112f, 772f, 54f);

            var metaBg = metaRow.AddComponent<Image>();
            metaBg.color = PacificRimUITheme.RowFill;
            metaBg.raycastTarget = false;

            sectorValueText = BuildMetaCell(metaRow.transform, font, 0f, 0.34f, "ZONE", data.Sector, PacificRimUITheme.Text);
            threatValueText = BuildMetaCell(metaRow.transform, font, 0.34f, 0.66f, "THREAT", data.Threat, PacificRimUITheme.Danger);
            unitValueText = BuildMetaCell(metaRow.transform, font, 0.66f, 1.0f, "DEPLOYMENT", data.UnitStatus, data.AccentColor);

            // Divider
            var divGo = new GameObject("Divider", typeof(RectTransform), typeof(UIDividerGraphic));
            divGo.transform.SetParent(panelGo.transform, false);
            var divRt = divGo.GetComponent<RectTransform>();
            SetTopLeft(divRt, 44f, -182f, 772f, 1f);
            var div = divGo.GetComponent<UIDividerGraphic>();
            div.color = PacificRimUITheme.Line;
            div.fadeEdgePercent = 0.08f;
            div.raycastTarget = false;

            // Body Lore & Mission Narrative
            bodyText = CreateText("BodyText", panelGo.transform, font, 17, FontStyle.Normal, PacificRimUITheme.TextDim, TextAnchor.UpperLeft, data.Body);
            bodyText.supportRichText = true;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.lineSpacing = 1.28f;
            SetTopLeft(bodyText.rectTransform, 44f, -202f, 772f, 218f);

            // Continue Button
            var btnGo = new GameObject("Btn_Continue", typeof(RectTransform), typeof(Button), typeof(PacificRimButton));
            btnGo.transform.SetParent(panelGo.transform, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0f);
            btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.anchoredPosition = new Vector2(0f, 44f);
            btnRt.sizeDelta = new Vector2(360f, 50f);

            var unityBtn = btnGo.GetComponent<Button>();
            unityBtn.transition = Selectable.Transition.None;

            var btnChamfer = btnGo.AddComponent<UIChamferGraphic>();
            btnChamfer.chamferSize = 9f;
            btnChamfer.borderThickness = 1.25f;
            btnChamfer.chamferTopLeft = false;
            btnChamfer.chamferTopRight = true;
            btnChamfer.chamferBottomLeft = true;
            btnChamfer.chamferBottomRight = false;
            btnChamfer.fillColor = PacificRimUITheme.CardFill;
            btnChamfer.borderColor = PacificRimUITheme.Blue;
            btnChamfer.raycastTarget = true;

            var btnAccentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            btnAccentGo.transform.SetParent(btnGo.transform, false);
            var baRt = btnAccentGo.GetComponent<RectTransform>();
            baRt.anchorMin = new Vector2(0f, 0.14f);
            baRt.anchorMax = new Vector2(0f, 0.86f);
            baRt.pivot = new Vector2(0f, 0.5f);
            baRt.sizeDelta = new Vector2(3.5f, 0f);
            var baImg = btnAccentGo.GetComponent<Image>();
            baImg.color = PacificRimUITheme.BlueBright;
            baImg.raycastTarget = false;

            var btnLbl = CreateText("Label", btnGo.transform, font, 15, FontStyle.Bold, PacificRimUITheme.Text, TextAnchor.MiddleCenter, data.ButtonLabel);
            btnLbl.rectTransform.anchorMin = Vector2.zero;
            btnLbl.rectTransform.anchorMax = Vector2.one;
            btnLbl.rectTransform.offsetMin = Vector2.zero;
            btnLbl.rectTransform.offsetMax = Vector2.zero;

            continueButton = btnGo.GetComponent<PacificRimButton>();
            continueButton.unityButton = unityBtn;
            continueButton.chamferGraphic = btnChamfer;
            continueButton.accentBar = baImg;
            continueButton.label = btnLbl;
            continueButton.isPrimary = true;
            continueButton.playSuccessOnClick = true;
            continueButton.onClick = Confirm;

            // Pulsing sub-prompt under button
            pulsePromptText = CreateText("ClickPrompt", panelGo.transform, font, 11, FontStyle.Bold, PacificRimUITheme.BlueBright, TextAnchor.MiddleCenter,
                "CLICK ANYWHERE OR PRESS [SPACE] TO CONTINUE");
            var ppRt = pulsePromptText.rectTransform;
            ppRt.anchorMin = new Vector2(0.5f, 0f);
            ppRt.anchorMax = new Vector2(0.5f, 0f);
            ppRt.pivot = new Vector2(0.5f, 0f);
            ppRt.anchoredPosition = new Vector2(0f, 14f);
            ppRt.sizeDelta = new Vector2(600f, 20f);
        }

        private static Text BuildMetaCell(Transform parent, Font font, float minX, float maxX, string keyLabel, string valLabel, Color valColor)
        {
            var cell = new GameObject($"Cell_{keyLabel}", typeof(RectTransform));
            cell.transform.SetParent(parent, false);
            var rt = cell.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(minX, 0f);
            rt.anchorMax = new Vector2(maxX, 1f);
            rt.offsetMin = new Vector2(14f, 6f);
            rt.offsetMax = new Vector2(-10f, -6f);

            var k = CreateText("Key", cell.transform, font, 10, FontStyle.Bold, PacificRimUITheme.TextMuted, TextAnchor.UpperLeft, keyLabel);
            k.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            k.rectTransform.anchorMax = new Vector2(1f, 1f);
            k.rectTransform.offsetMin = Vector2.zero;
            k.rectTransform.offsetMax = Vector2.zero;

            var v = CreateText("Value", cell.transform, font, 13, FontStyle.Bold, valColor, TextAnchor.LowerLeft, valLabel);
            v.rectTransform.anchorMin = new Vector2(0f, 0f);
            v.rectTransform.anchorMax = new Vector2(1f, 0.58f);
            v.rectTransform.offsetMin = Vector2.zero;
            v.rectTransform.offsetMax = Vector2.zero;
            return v;
        }

        private static Text CreateText(string name, Transform parent, Font font, int size, FontStyle style, Color color, TextAnchor align, string content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var txt = go.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = align;
            txt.supportRichText = true;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            txt.text = content;
            return txt;
        }

        private static void SetTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
