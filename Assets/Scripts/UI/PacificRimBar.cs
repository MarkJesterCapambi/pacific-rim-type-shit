using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    public class PacificRimBar : MonoBehaviour
    {
        public RectTransform fillTransform;
        public Image fillImage;
        public Image backgroundImage;
        public Text valueText;

        private float targetRatio = 1f;
        private float currentRatio = 1f;
        private bool ratioInitialized;

        public float FillRatio => currentRatio;

        public void SetRatio(float ratio)
        {
            targetRatio = Mathf.Clamp01(ratio);
            if (!ratioInitialized)
            {
                currentRatio = targetRatio;
                ratioInitialized = true;
                ApplyRatio(currentRatio);
            }
        }

        public void SetRatioImmediate(float ratio)
        {
            targetRatio = Mathf.Clamp01(ratio);
            currentRatio = targetRatio;
            ratioInitialized = true;
            ApplyRatio(currentRatio);
        }

        public void SetValueText(string text)
        {
            if (valueText != null)
                valueText.text = text;
        }

        private void Update()
        {
            if (!ratioInitialized) return;
            if (Mathf.Abs(currentRatio - targetRatio) > 0.0005f)
            {
                currentRatio = Mathf.Lerp(currentRatio, targetRatio, Mathf.Clamp01(Time.unscaledDeltaTime * 14f));
                ApplyRatio(currentRatio);
            }
        }

        private void ApplyRatio(float ratio)
        {
            if (fillTransform != null)
            {
                fillTransform.anchorMax = new Vector2(ratio, fillTransform.anchorMax.y);
            }
        }
    }
}
