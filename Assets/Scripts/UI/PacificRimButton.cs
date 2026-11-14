using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    [RequireComponent(typeof(Button))]
    public class PacificRimButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public UIChamferGraphic chamferGraphic;
        public UICircleOutlineGraphic circleGraphic;
        public Text label;
        public Image accentBar;
        public Button unityButton;
        public bool isPrimary = false;
        public bool isDanger = false;
        public bool isDisabled = false;
        public bool playSuccessOnClick = false;
        public bool playBackOnClick = false;
        public Action onClick;

        private bool isHovered;
        private bool isPressed;
        private Color targetFill;
        private Color targetBorder;
        private Color currentFill;
        private Color currentBorder;
        private bool initialized;

        public bool IsDisabled => isDisabled;

        private void Awake()
        {
            if (unityButton == null) unityButton = GetComponent<Button>();
            if (unityButton != null)
            {
                unityButton.transition = Selectable.Transition.None;
                unityButton.onClick.RemoveListener(HandleUnityButtonClick);
                unityButton.onClick.AddListener(HandleUnityButtonClick);
            }
            InitializeColors();
        }

        private void OnEnable()
        {
            isHovered = false;
            isPressed = false;
            InitializeColors();
            ApplyInstant();
        }

        private void HandleUnityButtonClick()
        {
            if (isDisabled)
            {
                PacificRimUISound.PlayDeny();
                return;
            }

            if (playSuccessOnClick) PacificRimUISound.PlaySuccess();
            else if (playBackOnClick) PacificRimUISound.PlayBack();
            else PacificRimUISound.PlayClick();

            onClick?.Invoke();
        }

        private void InitializeColors()
        {
            ComputeTargets();
            if (!initialized)
            {
                currentFill = targetFill;
                currentBorder = targetBorder;
                initialized = true;
            }
        }

        private void Update()
        {
            ComputeTargets();
            float t = Mathf.Clamp01(Time.unscaledDeltaTime * 16f);
            currentFill = Color.Lerp(currentFill, targetFill, t);
            currentBorder = Color.Lerp(currentBorder, targetBorder, t);

            if (chamferGraphic != null)
            {
                chamferGraphic.SetColors(currentFill, currentBorder);
            }
            if (circleGraphic != null)
            {
                circleGraphic.SetColors(currentFill, currentBorder);
            }
        }

        public void SetText(string text)
        {
            if (label != null)
            {
                label.supportRichText = true;
                label.text = PacificRimUITheme.Track(text, 2, Mathf.Max(12, label.fontSize), true);
            }
        }

        public void SetDisabled(bool disabled)
        {
            isDisabled = disabled;
            if (unityButton != null) unityButton.interactable = !disabled;
            ComputeTargets();
            if (label != null)
            {
                label.color = disabled ? PacificRimUITheme.TextMuted : PacificRimUITheme.Text;
            }
            ApplyInstant();
        }

        private void ComputeTargets()
        {
            if (isDisabled)
            {
                targetFill = new Color(12f / 255f, 16f / 255f, 22f / 255f, 0.30f);
                targetBorder = new Color(1f, 1f, 1f, 0.10f);
                return;
            }

            if (isPrimary)
            {
                if (isPressed)
                {
                    targetFill = new Color(PacificRimUITheme.Blue.r, PacificRimUITheme.Blue.g, PacificRimUITheme.Blue.b, 0.42f);
                    targetBorder = Color.white;
                }
                else if (isHovered)
                {
                    targetFill = new Color(PacificRimUITheme.Blue.r, PacificRimUITheme.Blue.g, PacificRimUITheme.Blue.b, 0.34f);
                    targetBorder = PacificRimUITheme.BlueBright;
                }
                else
                {
                    targetFill = PacificRimUITheme.CardFill;
                    targetBorder = PacificRimUITheme.Blue;
                }
            }
            else if (isDanger)
            {
                if (isPressed)
                {
                    targetFill = new Color(PacificRimUITheme.Danger.r, PacificRimUITheme.Danger.g, PacificRimUITheme.Danger.b, 0.30f);
                    targetBorder = PacificRimUITheme.Danger;
                }
                else if (isHovered)
                {
                    targetFill = new Color(PacificRimUITheme.Danger.r, PacificRimUITheme.Danger.g, PacificRimUITheme.Danger.b, 0.18f);
                    targetBorder = PacificRimUITheme.Danger;
                }
                else
                {
                    targetFill = PacificRimUITheme.Panel;
                    targetBorder = PacificRimUITheme.Line;
                }
            }
            else
            {
                if (isPressed)
                {
                    targetFill = PacificRimUITheme.RowSelectedFill;
                    targetBorder = Color.white;
                }
                else if (isHovered)
                {
                    targetFill = new Color(PacificRimUITheme.BlueDeep.r, PacificRimUITheme.BlueDeep.g, PacificRimUITheme.BlueDeep.b, 0.58f);
                    targetBorder = PacificRimUITheme.BlueBright;
                }
                else
                {
                    targetFill = PacificRimUITheme.Panel;
                    targetBorder = PacificRimUITheme.Line;
                }
            }
        }

        private void ApplyInstant()
        {
            currentFill = targetFill;
            currentBorder = targetBorder;
            if (chamferGraphic != null)
                chamferGraphic.SetColors(currentFill, currentBorder);
            if (circleGraphic != null)
                circleGraphic.SetColors(currentFill, currentBorder);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            if (!isDisabled)
                PacificRimUISound.PlayHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Unity's Button.onClick will also trigger, but we handle it through HandleUnityButtonClick.
            // If unityButton is absent for any reason, trigger directly:
            if (unityButton == null)
            {
                HandleUnityButtonClick();
            }
        }
    }
}
