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
        private bool isGlitching;
        private float glitchRatioOffset;
        private bool hasCachedBgColor;
        private Color cachedBgColor;

        public float FillRatio => currentRatio;

        public void SetRatio(float ratio)
        {
            targetRatio = Mathf.Clamp01(ratio);
            if (!ratioInitialized)
            {
                currentRatio = targetRatio;
                ratioInitialized = true;
                ApplyRatio(isGlitching ? Mathf.Clamp01(currentRatio + glitchRatioOffset) : currentRatio);
            }
        }

        public void SetRatioImmediate(float ratio)
        {
            targetRatio = Mathf.Clamp01(ratio);
            currentRatio = targetRatio;
            ratioInitialized = true;
            ApplyRatio(isGlitching ? Mathf.Clamp01(currentRatio + glitchRatioOffset) : currentRatio);
        }

        public void SetGlitchVisual(bool active, float ratioOffset = 0f, float alphaMultiplier = 1f, Color? tintOverride = null)
        {
            if (backgroundImage != null && !hasCachedBgColor)
            {
                cachedBgColor = backgroundImage.color;
                hasCachedBgColor = true;
            }

            isGlitching = active;
            glitchRatioOffset = ratioOffset;

            if (!active)
            {
                ApplyRatio(currentRatio);
                if (backgroundImage != null && hasCachedBgColor)
                {
                    backgroundImage.color = cachedBgColor;
                }
                return;
            }

            ApplyRatio(Mathf.Clamp01(currentRatio + glitchRatioOffset));

            if (fillImage != null)
            {
                Color c = tintOverride ?? fillImage.color;
                c.a *= Mathf.Clamp01(alphaMultiplier);
                fillImage.color = c;
            }

            if (backgroundImage != null && hasCachedBgColor)
            {
                Color bg = cachedBgColor;
                bg.a *= Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(alphaMultiplier));
                backgroundImage.color = bg;
            }
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
                ApplyRatio(isGlitching ? Mathf.Clamp01(currentRatio + glitchRatioOffset) : currentRatio);
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
