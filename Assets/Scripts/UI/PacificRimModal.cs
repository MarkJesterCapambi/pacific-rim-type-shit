using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    public class PacificRimModal : MonoBehaviour
    {
        public RectTransform windowRect;
        public CanvasGroup canvasGroup;
        public Text titleText;
        public Text subtitleText;
        public PacificRimButton closeButton;
        public Button dimmerButton;
        public RectTransform contentRoot;
        public Action onClosed;

        private bool isOpen;
        private float animProgress;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (closeButton != null)
                closeButton.onClick = Close;
            if (dimmerButton != null)
                dimmerButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            isOpen = true;
            animProgress = 0f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            if (windowRect != null)
            {
                windowRect.localScale = Vector3.one * 0.96f;
            }
        }

        public void Close()
        {
            if (!isOpen && !gameObject.activeSelf) return;
            isOpen = false;
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            PacificRimUISound.PlayBack();
            onClosed?.Invoke();
        }

        private void Update()
        {
            if (isOpen)
            {
                animProgress = Mathf.MoveTowards(animProgress, 1f, Time.unscaledDeltaTime * 7f);
                float ease = 1f - Mathf.Pow(1f - animProgress, 3f);
                if (canvasGroup != null) canvasGroup.alpha = ease;
                if (windowRect != null) windowRect.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, ease);
            }
            else if (gameObject.activeSelf)
            {
                animProgress = Mathf.MoveTowards(animProgress, 0f, Time.unscaledDeltaTime * 9f);
                if (canvasGroup != null) canvasGroup.alpha = animProgress;
                if (windowRect != null) windowRect.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, animProgress);
                if (animProgress <= 0.01f)
                {
                    gameObject.SetActive(false);
                }
            }
        }
    }
}
