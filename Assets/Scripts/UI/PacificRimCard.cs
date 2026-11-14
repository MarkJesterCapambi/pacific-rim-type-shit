using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    [RequireComponent(typeof(Button))]
    public class PacificRimCard : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public UIChamferGraphic chamferGraphic;
        public Image accentBar;
        public Text kickerText;
        public Text titleText;
        public Text subtitleText;
        public Text badgeText;
        public Button unityButton;
        public bool isSelected;
        public Action onClick;

        private bool isHovered;
        private Color currentFill;
        private Color currentBorder;
        private bool initialized;

        private void Awake()
        {
            if (unityButton == null) unityButton = GetComponent<Button>();
            if (unityButton != null)
            {
                unityButton.transition = Selectable.Transition.None;
                unityButton.onClick.RemoveListener(HandleCardClick);
                unityButton.onClick.AddListener(HandleCardClick);
            }
            SyncInstant();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            SyncInstant();
        }

        private void HandleCardClick()
        {
            PacificRimUISound.PlayClick();
            onClick?.Invoke();
        }

        private void SyncInstant()
        {
            ComputeColors(out var f, out var b);
            currentFill = f;
            currentBorder = b;
            if (chamferGraphic != null)
                chamferGraphic.SetColors(currentFill, currentBorder);
            if (accentBar != null)
                accentBar.color = isSelected ? PacificRimUITheme.BlueBright : PacificRimUITheme.Blue;
        }

        private void Update()
        {
            ComputeColors(out var targetFill, out var targetBorder);
            float t = Mathf.Clamp01(Time.unscaledDeltaTime * 14f);
            currentFill = Color.Lerp(currentFill, targetFill, t);
            currentBorder = Color.Lerp(currentBorder, targetBorder, t);

            if (chamferGraphic != null)
                chamferGraphic.SetColors(currentFill, currentBorder);
            if (accentBar != null)
                accentBar.color = isSelected ? PacificRimUITheme.BlueBright : (isHovered ? PacificRimUITheme.BlueBright : PacificRimUITheme.Blue);
        }

        private void ComputeColors(out Color fill, out Color border)
        {
            if (isSelected)
            {
                fill = PacificRimUITheme.RowSelectedFill;
                border = PacificRimUITheme.BlueBright;
            }
            else if (isHovered)
            {
                fill = new Color(PacificRimUITheme.BlueDeep.r, PacificRimUITheme.BlueDeep.g, PacificRimUITheme.BlueDeep.b, 0.58f);
                border = PacificRimUITheme.Blue;
            }
            else
            {
                fill = PacificRimUITheme.CardFill;
                border = PacificRimUITheme.Line;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            PacificRimUISound.PlayHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (unityButton == null)
            {
                HandleCardClick();
            }
        }
    }
}
