using UnityEngine;
using UnityEngine.UI;
using PacificRimAudio;

namespace PacificRimUI
{
    /// <summary>
    /// Sleek PacificRimUITheme-matched Audio Settings controller.
    /// Can be embedded directly inside a menu tab/modal via BuildEmbeddedSliders,
    /// or attached as a compact chamfered drawer if none is baked into the menu.
    /// </summary>
    public class PacificRimAudioSettingsUI : MonoBehaviour
    {
        private GameObject panelRoot;
        private Slider musicSlider;
        private Slider sfxSlider;
        private Text musicValueText;
        private Text sfxValueText;
        private PacificRimButton toggleButton;
        private bool isExpanded;

        public static void AttachToMenu(GameObject menuVisualsRoot)
        {
            if (menuVisualsRoot == null) return;
            if (menuVisualsRoot.GetComponentInChildren<PacificRimAudioSettingsUI>(true) != null) return;

            var host = new GameObject("AudioSettings_Overlay", typeof(RectTransform));
            host.transform.SetParent(menuVisualsRoot.transform, false);

            var rt = host.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-36f, 24f);
            rt.sizeDelta = new Vector2(260f, 180f);

            var comp = host.AddComponent<PacificRimAudioSettingsUI>();
            comp.BuildOverlayUI(host.transform);
        }

        /// <summary>
        /// Builds clean Music & SFX volume sliders directly into an existing menu container (tab page or modal).
        /// </summary>
        public static PacificRimAudioSettingsUI BuildEmbeddedSliders(Transform container, float topY = 0f, float rowSpacing = 66f)
        {
            if (container == null) return null;
            var comp = container.gameObject.GetComponent<PacificRimAudioSettingsUI>()
                       ?? container.gameObject.AddComponent<PacificRimAudioSettingsUI>();
            comp.BuildSliderRows(container, topY, rowSpacing);
            return comp;
        }

        private void BuildOverlayUI(Transform parent)
        {
            Font font = PacificRimUITheme.GetDefaultFont();

            // Compact chamfered toggle button
            var btnGo = new GameObject("Btn_AudioSettings", typeof(RectTransform), typeof(Button), typeof(UIChamferGraphic), typeof(PacificRimButton));
            btnGo.transform.SetParent(parent, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0f);
            btnRt.anchorMax = new Vector2(1f, 0f);
            btnRt.pivot = new Vector2(1f, 0f);
            btnRt.anchoredPosition = Vector2.zero;
            btnRt.sizeDelta = new Vector2(116f, 34f);

            var chamfer = btnGo.GetComponent<UIChamferGraphic>();
            chamfer.chamferSize = 7f;
            chamfer.borderThickness = 1f;
            chamfer.chamferTopLeft = false;
            chamfer.chamferTopRight = true;
            chamfer.chamferBottomRight = false;
            chamfer.chamferBottomLeft = true;
            chamfer.fillColor = PacificRimUITheme.Panel;
            chamfer.borderColor = PacificRimUITheme.Line;

            var btnTxtGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            btnTxtGo.transform.SetParent(btnGo.transform, false);
            var btnTxtRt = btnTxtGo.GetComponent<RectTransform>();
            btnTxtRt.anchorMin = Vector2.zero;
            btnTxtRt.anchorMax = Vector2.one;
            btnTxtRt.offsetMin = Vector2.zero;
            btnTxtRt.offsetMax = Vector2.zero;

            var btnTxt = btnTxtGo.GetComponent<Text>();
            btnTxt.font = font;
            btnTxt.fontSize = 12;
            btnTxt.fontStyle = FontStyle.Bold;
            btnTxt.alignment = TextAnchor.MiddleCenter;
            btnTxt.color = PacificRimUITheme.Text;
            btnTxt.text = "AUDIO";
            btnTxt.raycastTarget = false;

            toggleButton = btnGo.GetComponent<PacificRimButton>();
            toggleButton.unityButton = btnGo.GetComponent<Button>();
            toggleButton.chamferGraphic = chamfer;
            toggleButton.label = btnTxt;
            toggleButton.onClick = TogglePanel;

            // Expandable chamfered card above the button
            panelRoot = new GameObject("AudioSettings_Panel", typeof(RectTransform), typeof(UIChamferGraphic));
            panelRoot.transform.SetParent(parent, false);
            var panelRt = panelRoot.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0f);
            panelRt.pivot = new Vector2(1f, 0f);
            panelRt.anchoredPosition = new Vector2(0f, 42f);
            panelRt.sizeDelta = new Vector2(260f, 134f);

            var panelChamfer = panelRoot.GetComponent<UIChamferGraphic>();
            panelChamfer.chamferSize = 10f;
            panelChamfer.borderThickness = 1f;
            panelChamfer.fillColor = PacificRimUITheme.PanelOpaque;
            panelChamfer.borderColor = PacificRimUITheme.Blue;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(panelRoot.transform, false);
            var cRt = contentGo.GetComponent<RectTransform>();
            cRt.anchorMin = Vector2.zero;
            cRt.anchorMax = Vector2.one;
            cRt.offsetMin = new Vector2(12f, 10f);
            cRt.offsetMax = new Vector2(-12f, -10f);

            BuildSliderRows(contentGo.transform, 0f, 56f);
            panelRoot.SetActive(false);
        }

        private void BuildSliderRows(Transform parent, float topY, float rowSpacing)
        {
            Font font = PacificRimUITheme.GetDefaultFont();

            float initialMusic = PacificRimMusicManager.Instance != null
                ? PacificRimMusicManager.Instance.MusicVolume
                : PlayerPrefs.GetFloat("PR_MusicVolume", 0.85f);

            float initialSfx = PacificRimAudioManager.Instance != null
                ? PacificRimAudioManager.Instance.SfxVolume
                : PlayerPrefs.GetFloat("PR_SFXVolume", 0.55f);

            (musicSlider, musicValueText) = CreateSliderRow(
                parent, "Music", "MUSIC", font, topY, initialMusic, OnMusicSliderChanged);

            (sfxSlider, sfxValueText) = CreateSliderRow(
                parent, "SFX", "SFX", font, topY - rowSpacing, initialSfx, OnSfxSliderChanged);
        }

        private void OnEnable()
        {
            RefreshSliderValues();
        }

        public void RefreshSliderValues()
        {
            if (musicSlider != null)
            {
                float v = PacificRimMusicManager.Instance != null
                    ? PacificRimMusicManager.Instance.MusicVolume
                    : PlayerPrefs.GetFloat("PR_MusicVolume", 0.85f);
                musicSlider.SetValueWithoutNotify(v);
                if (musicValueText != null)
                    musicValueText.text = $"{Mathf.RoundToInt(v * 100f)}%";
            }

            if (sfxSlider != null)
            {
                float v = PacificRimAudioManager.Instance != null
                    ? PacificRimAudioManager.Instance.SfxVolume
                    : PlayerPrefs.GetFloat("PR_SFXVolume", 0.55f);
                sfxSlider.SetValueWithoutNotify(v);
                if (sfxValueText != null)
                    sfxValueText.text = $"{Mathf.RoundToInt(v * 100f)}%";
            }
        }

        private void TogglePanel()
        {
            isExpanded = !isExpanded;
            if (panelRoot != null) panelRoot.SetActive(isExpanded);
            toggleButton?.SetText(isExpanded ? "CLOSE" : "AUDIO");
            if (isExpanded) RefreshSliderValues();
        }

        private void OnMusicSliderChanged(float value)
        {
            PacificRimMusicManager.Instance?.SetMusicVolume(value);
            PlayerPrefs.SetFloat("PR_MusicVolume", value);
            if (musicValueText != null)
                musicValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        private void OnSfxSliderChanged(float value)
        {
            PacificRimAudioManager.Instance?.SetSfxVolume(value);
            PlayerPrefs.SetFloat("PR_SFXVolume", value);
            if (sfxValueText != null)
                sfxValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        private static Text CreateText(Transform parent, string name, string content, Font font, int size, FontStyle style,
            Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var txt = go.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = align;
            txt.text = content;
            txt.raycastTarget = false;
            return txt;
        }

        private static (Slider, Text) CreateSliderRow(Transform parent, string key, string label, Font font,
            float yOffset, float initialValue, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var rowGo = new GameObject($"AudioRow_{key}", typeof(RectTransform), typeof(Image));
            rowGo.transform.SetParent(parent, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, yOffset);
            rowRt.sizeDelta = new Vector2(0f, 52f);

            var rowBg = rowGo.GetComponent<Image>();
            rowBg.color = PacificRimUITheme.RowFill;
            rowBg.raycastTarget = false;

            // Left accent bar
            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accentGo.transform.SetParent(rowGo.transform, false);
            var aRt = accentGo.GetComponent<RectTransform>();
            aRt.anchorMin = new Vector2(0f, 0.15f);
            aRt.anchorMax = new Vector2(0f, 0.85f);
            aRt.pivot = new Vector2(0f, 0.5f);
            aRt.sizeDelta = new Vector2(2.5f, 0f);
            accentGo.GetComponent<Image>().color = PacificRimUITheme.Blue;
            accentGo.GetComponent<Image>().raycastTarget = false;

            CreateText(rowGo.transform, $"Label_{key}", label, font, 12, FontStyle.Bold,
                PacificRimUITheme.Text, new Vector2(0f, 0.50f), new Vector2(0.65f, 1f),
                new Vector2(14f, 0f), new Vector2(0f, -4f), TextAnchor.MiddleLeft);

            var valTxt = CreateText(rowGo.transform, $"Value_{key}", $"{Mathf.RoundToInt(initialValue * 100f)}%", font, 12, FontStyle.Bold,
                PacificRimUITheme.BlueBright, new Vector2(0.65f, 0.50f), new Vector2(1f, 1f),
                Vector2.zero, new Vector2(-14f, -4f), TextAnchor.MiddleRight);

            var sliderGo = new GameObject($"Slider_{key}", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(rowGo.transform, false);
            var sRt = sliderGo.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0f, 0f);
            sRt.anchorMax = new Vector2(1f, 0.44f);
            sRt.offsetMin = new Vector2(14f, 4f);
            sRt.offsetMax = new Vector2(-14f, -2f);

            // Background track
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(sliderGo.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.35f);
            bgRt.anchorMax = new Vector2(1f, 0.65f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = PacificRimUITheme.BarTrack;

            // Fill area
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.35f);
            faRt.anchorMax = new Vector2(1f, 0.65f);
            faRt.offsetMin = Vector2.zero;
            faRt.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillArea.transform, false);
            var fRt = fillGo.GetComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            fillGo.GetComponent<Image>().color = PacificRimUITheme.BlueBright;

            // Handle
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            var haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(5f, 0f);
            haRt.offsetMax = new Vector2(-5f, 0f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleArea.transform, false);
            var hRt = handleGo.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(8f, 12f);
            handleGo.GetComponent<Image>().color = PacificRimUITheme.Text;

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = initialValue;
            slider.fillRect = fRt;
            slider.handleRect = hRt;
            slider.targetGraphic = handleGo.GetComponent<Image>();
            slider.onValueChanged.AddListener(onChanged);

            return (slider, valTxt);
        }
    }
}
