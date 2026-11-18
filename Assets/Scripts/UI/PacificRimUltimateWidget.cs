using UnityEngine;
using UnityEngine.UI;

namespace PacificRimUI
{
    /// <summary>
    /// Overwatch-style circular Ultimate charge widget for Gipsy Danger's I-19 Plasmacaster.
    /// Displays radial segmented charge (0% on spawn -> 99%) and transitions to a glowing
    /// cyan/white icon emblem when 100% Ready.
    /// </summary>
    public class PacificRimUltimateWidget : MonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField] private Sprite outerRingSprite;
        [SerializeField] private Sprite segmentedRingSprite;
        [SerializeField] private Sprite dashedRingSprite;
        [SerializeField] private Sprite readyRingSprite;
        [SerializeField] private Sprite readyAuraSprite;
        [SerializeField] private Sprite iconSprite;
        [SerializeField] private Sprite badgeOutlineSprite;
        [SerializeField] private Sprite badgeSolidSprite;

        [Header("UI Elements")]
        [SerializeField] private Image outerRingImage;
        [SerializeField] private Image readyAuraBackImage;
        [SerializeField] private Image readyAuraFrontImage;
        [SerializeField] private Image segmentTrackImage;
        [SerializeField] private Image segmentFillImage;
        [SerializeField] private Image dashedRingImage;
        [SerializeField] private GameObject percentGroup;
        [SerializeField] private Text percentText;
        [SerializeField] private Image readyRingImage;
        [SerializeField] private Image readyIconImage;
        [SerializeField] private Image badgeOutlineImage;
        [SerializeField] private Image badgeSolidImage;
        [SerializeField] private Text keyText;

        private static readonly Color ChargeAmber = new Color(1.0f, 0.78f, 0.14f, 1.0f);
        private static readonly Color ChargeTrack = new Color(0.45f, 0.50f, 0.54f, 0.42f);
        private static readonly Color BadgeDarkText = new Color(0.08f, 0.11f, 0.16f, 1.0f);
        private static readonly Color ReadyAuraBackTint = new Color(0.12f, 0.55f, 1.0f, 0.88f);
        private static readonly Color ReadyAuraFrontTint = new Color(0.28f, 0.90f, 1.0f, 0.72f);

        private float targetRatio;
        private float displayedRatio;
        private int displayedPercent = -1;
        private bool isReady;
        private bool isFiringActive;
        private string cachedKey = "";
        private float readyPopTimer;
        private bool initialized;

        public void EnsureBuiltAndWired(
            Sprite outerRing = null,
            Sprite segmentedRing = null,
            Sprite dashedRing = null,
            Sprite readyRing = null,
            Sprite readyAura = null,
            Sprite icon = null,
            Sprite badgeOutline = null,
            Sprite badgeSolid = null)
        {
            outerRingSprite = outerRing != null ? outerRing : (outerRingSprite != null ? outerRingSprite : Resources.Load<Sprite>("UI/PlasmaUlt_OuterRing"));
            segmentedRingSprite = segmentedRing != null ? segmentedRing : (segmentedRingSprite != null ? segmentedRingSprite : Resources.Load<Sprite>("UI/PlasmaUlt_SegmentedRing"));
            dashedRingSprite = dashedRing != null ? dashedRing : (dashedRingSprite != null ? dashedRingSprite : Resources.Load<Sprite>("UI/PlasmaUlt_DashedRing"));
            readyRingSprite = readyRing != null ? readyRing : (readyRingSprite != null ? readyRingSprite : Resources.Load<Sprite>("UI/PlasmaUlt_ReadyRing"));
            readyAuraSprite = readyAura != null ? readyAura : (readyAuraSprite != null ? readyAuraSprite : Resources.Load<Sprite>("UI/PlasmaUlt_ReadyAura"));
            iconSprite = icon != null ? icon : (iconSprite != null ? iconSprite : Resources.Load<Sprite>("UI/PlasmaUlt_Icon"));
            badgeOutlineSprite = badgeOutline != null ? badgeOutline : (badgeOutlineSprite != null ? badgeOutlineSprite : Resources.Load<Sprite>("UI/PlasmaUlt_BadgeOutline"));
            badgeSolidSprite = badgeSolid != null ? badgeSolid : (badgeSolidSprite != null ? badgeSolidSprite : Resources.Load<Sprite>("UI/PlasmaUlt_BadgeSolid"));

            if (outerRingImage == null)
            {
                BuildHierarchy();
            }
            ApplySprites();
        }

        private void BuildHierarchy()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 70f);
            rt.sizeDelta = new Vector2(124f, 124f);

            readyAuraBackImage = CreateChildImage("ReadyAuraBack", new Vector2(160f, 160f), ReadyAuraBackTint);
            readyAuraFrontImage = CreateChildImage("ReadyAuraFront", new Vector2(148f, 148f), ReadyAuraFrontTint);
            outerRingImage = CreateStretchedImage("OuterRing", Color.white);
            segmentTrackImage = CreateStretchedImage("SegmentTrack", ChargeTrack);

            segmentFillImage = CreateStretchedImage("SegmentFill", ChargeAmber);
            segmentFillImage.type = Image.Type.Filled;
            segmentFillImage.fillMethod = Image.FillMethod.Radial360;
            segmentFillImage.fillOrigin = (int)Image.Origin360.Top;
            segmentFillImage.fillClockwise = true;
            segmentFillImage.fillAmount = 0f;

            dashedRingImage = CreateStretchedImage("DashedRing", ChargeAmber);

            percentGroup = new GameObject("PercentGroup", typeof(RectTransform));
            percentGroup.transform.SetParent(transform, false);
            var pgRt = percentGroup.GetComponent<RectTransform>();
            pgRt.anchorMin = Vector2.zero;
            pgRt.anchorMax = Vector2.one;
            pgRt.offsetMin = Vector2.zero;
            pgRt.offsetMax = Vector2.zero;

            var txtGo = new GameObject("PercentText", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(percentGroup.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            percentText = txtGo.GetComponent<Text>();
            percentText.font = PacificRimUITheme.GetDefaultFont();
            percentText.fontSize = 27;
            percentText.fontStyle = FontStyle.BoldAndItalic;
            percentText.alignment = TextAnchor.MiddleCenter;
            percentText.supportRichText = true;
            percentText.color = PacificRimUITheme.Text;
            percentText.raycastTarget = false;
            var pctShadow = txtGo.AddComponent<Shadow>();
            pctShadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            pctShadow.effectDistance = new Vector2(1f, -1f);

            readyRingImage = CreateStretchedImage("ReadyRing", Color.white);
            readyIconImage = CreateChildImage("ReadyIcon", new Vector2(56f, 56f), Color.white);

            // Keybind badge seated cleanly at the bottom edge of the outer ring
            var badgeGo = new GameObject("KeyBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(transform, false);
            var badgeRt = badgeGo.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0.5f, 0f);
            badgeRt.anchorMax = new Vector2(0.5f, 0f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(0f, 7f);
            badgeRt.sizeDelta = new Vector2(26f, 26f);

            badgeOutlineImage = CreateBadgeImage(badgeGo.transform, "BadgeOutline", Color.white);
            badgeSolidImage = CreateBadgeImage(badgeGo.transform, "BadgeSolid", Color.white);

            var keyGo = new GameObject("KeyText", typeof(RectTransform), typeof(Text));
            keyGo.transform.SetParent(badgeGo.transform, false);
            var kRt = keyGo.GetComponent<RectTransform>();
            kRt.anchorMin = Vector2.zero;
            kRt.anchorMax = Vector2.one;
            kRt.offsetMin = Vector2.zero;
            kRt.offsetMax = Vector2.zero;
            keyText = keyGo.GetComponent<Text>();
            keyText.font = PacificRimUITheme.GetDefaultFont();
            keyText.fontSize = 13;
            keyText.fontStyle = FontStyle.BoldAndItalic;
            keyText.alignment = TextAnchor.MiddleCenter;
            keyText.color = PacificRimUITheme.Text;
            keyText.raycastTarget = false;
            keyText.text = "T";
        }

        private Image CreateStretchedImage(string objName, Color tint)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = tint;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        private Image CreateChildImage(string objName, Vector2 size, Color tint)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = tint;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        private static Image CreateBadgeImage(Transform parent, string objName, Color tint)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = tint;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        private void ApplySprites()
        {
            if (outerRingImage != null) outerRingImage.sprite = outerRingSprite;
            if (readyAuraBackImage != null) readyAuraBackImage.sprite = readyAuraSprite;
            if (readyAuraFrontImage != null) readyAuraFrontImage.sprite = readyAuraSprite;
            if (segmentTrackImage != null) segmentTrackImage.sprite = segmentedRingSprite;
            if (segmentFillImage != null) segmentFillImage.sprite = segmentedRingSprite;
            if (dashedRingImage != null) dashedRingImage.sprite = dashedRingSprite;
            if (readyRingImage != null) readyRingImage.sprite = readyRingSprite;
            if (readyIconImage != null) readyIconImage.sprite = iconSprite;
            if (badgeOutlineImage != null) badgeOutlineImage.sprite = badgeOutlineSprite;
            if (badgeSolidImage != null) badgeSolidImage.sprite = badgeSolidSprite;
        }

        public void SetImmediate(float chargeRatio, int percent, bool ready, bool firingActive, string keyLabel)
        {
            EnsureBuiltAndWired();
            initialized = true;
            targetRatio = Mathf.Clamp01(chargeRatio);
            displayedRatio = targetRatio;
            isReady = ready;
            isFiringActive = firingActive;
            displayedPercent = -1;
            readyPopTimer = 0f;
            ApplyKey(keyLabel);
            ApplyVisualState(percent);
        }

        public void UpdateState(float chargeRatio, int percent, bool ready, bool firingActive, string keyLabel, float dt)
        {
            EnsureBuiltAndWired();
            if (!initialized)
            {
                SetImmediate(chargeRatio, percent, ready, firingActive, keyLabel);
                return;
            }

            if (ready && !isReady)
            {
                readyPopTimer = 0.35f;
            }

            targetRatio = Mathf.Clamp01(chargeRatio);
            displayedRatio = Mathf.MoveTowards(displayedRatio, targetRatio, dt * 1.85f);
            if (targetRatio <= 0.001f && displayedRatio < 0.02f)
            {
                displayedRatio = 0f;
            }

            isReady = ready;
            isFiringActive = firingActive;
            ApplyKey(keyLabel);
            ApplyVisualState(percent);
            AnimateReadyEffects(dt);
        }

        private void ApplyKey(string keyLabel)
        {
            string normalized = string.IsNullOrEmpty(keyLabel) ? "T" : keyLabel.ToUpperInvariant();
            if (keyText != null && normalized != cachedKey)
            {
                cachedKey = normalized;
                keyText.text = normalized;
            }
        }

        private void ApplyVisualState(int percent)
        {
            bool showReady = isReady || isFiringActive;

            if (segmentTrackImage != null && segmentTrackImage.gameObject.activeSelf == showReady)
                segmentTrackImage.gameObject.SetActive(!showReady);
            if (segmentFillImage != null)
            {
                if (segmentFillImage.gameObject.activeSelf == showReady)
                    segmentFillImage.gameObject.SetActive(!showReady);
                if (!showReady)
                    segmentFillImage.fillAmount = displayedRatio;
            }
            if (dashedRingImage != null && dashedRingImage.gameObject.activeSelf == showReady)
                dashedRingImage.gameObject.SetActive(!showReady);
            if (percentGroup != null && percentGroup.activeSelf == showReady)
                percentGroup.SetActive(!showReady);

            if (!showReady && percentText != null)
            {
                int clampedPct = Mathf.Clamp(percent, 0, 99);
                if (clampedPct != displayedPercent)
                {
                    displayedPercent = clampedPct;
                    percentText.text = $"{clampedPct}<size=15><color=#FFBE28>%</color></size>";
                }
            }

            if (readyAuraBackImage != null && readyAuraBackImage.gameObject.activeSelf != showReady)
                readyAuraBackImage.gameObject.SetActive(showReady);
            if (readyAuraFrontImage != null && readyAuraFrontImage.gameObject.activeSelf != showReady)
                readyAuraFrontImage.gameObject.SetActive(showReady);
            if (readyRingImage != null && readyRingImage.gameObject.activeSelf != showReady)
                readyRingImage.gameObject.SetActive(showReady);
            if (readyIconImage != null && readyIconImage.gameObject.activeSelf != showReady)
                readyIconImage.gameObject.SetActive(showReady);

            if (badgeOutlineImage != null && badgeOutlineImage.gameObject.activeSelf == showReady)
                badgeOutlineImage.gameObject.SetActive(!showReady);
            if (badgeSolidImage != null && badgeSolidImage.gameObject.activeSelf != showReady)
                badgeSolidImage.gameObject.SetActive(showReady);
            if (keyText != null)
                keyText.color = showReady ? BadgeDarkText : PacificRimUITheme.Text;
        }

        private void AnimateReadyEffects(float dt)
        {
            if (!(isReady || isFiringActive)) return;

            float time = Time.unscaledTime;
            if (readyPopTimer > 0f)
            {
                readyPopTimer = Mathf.Max(0f, readyPopTimer - dt);
            }
            float pop = readyPopTimer > 0f ? (1f + 0.14f * Mathf.Sin((readyPopTimer / 0.35f) * Mathf.PI)) : 1f;

            if (readyAuraBackImage != null)
            {
                readyAuraBackImage.rectTransform.Rotate(0f, 0f, -14f * dt);
                float pulse = isFiringActive ? (0.95f + 0.05f * Mathf.Sin(time * 16f)) : (0.78f + 0.18f * Mathf.Sin(time * 3.4f));
                Color c = ReadyAuraBackTint;
                c.a = pulse;
                readyAuraBackImage.color = c;
                readyAuraBackImage.rectTransform.localScale = Vector3.one * (pop * (1f + 0.03f * Mathf.Sin(time * 3.4f)));
            }

            if (readyAuraFrontImage != null)
            {
                readyAuraFrontImage.rectTransform.Rotate(0f, 0f, 21f * dt);
                float pulse = isFiringActive ? 0.92f : (0.58f + 0.20f * Mathf.Cos(time * 4.2f));
                Color c = ReadyAuraFrontTint;
                c.a = pulse;
                readyAuraFrontImage.color = c;
            }

            if (readyIconImage != null)
            {
                float iconPulse = isFiringActive
                    ? (1.04f + 0.05f * Mathf.Sin(time * 18f))
                    : (1.0f + 0.025f * Mathf.Sin(time * 3.2f));
                readyIconImage.rectTransform.localScale = Vector3.one * (pop * iconPulse);
                readyIconImage.color = isFiringActive
                    ? new Color(0.55f, 0.96f, 1f, 1f)
                    : Color.white;
            }
        }
    }
}
