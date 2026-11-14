using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    /// <summary>
    /// Battlefront UI.tabBar equivalent: top horizontal tabs with animated 2px underline indicator.
    /// </summary>
    public class PacificRimTabBar : MonoBehaviour
    {
        public List<Button> tabButtons = new List<Button>();
        public List<Text> tabLabels = new List<Text>();
        public RectTransform indicatorRect;
        public int activeIndex = 0;
        public Action<int> onTabChanged;

        private float targetIndicatorX;
        private float targetIndicatorW;
        private bool indicatorReady;

        private void Start()
        {
            for (int i = 0; i < tabButtons.Count; i++)
            {
                int idx = i;
                if (tabButtons[i] != null)
                {
                    tabButtons[i].onClick.AddListener(() => SelectTab(idx, false));
                }
            }
            SelectTab(activeIndex, true);
        }

        public void SelectTab(int index, bool silent = false)
        {
            if (tabButtons.Count == 0) return;
            index = Mathf.Clamp(index, 0, tabButtons.Count - 1);
            bool changed = index != activeIndex;
            activeIndex = index;

            for (int i = 0; i < tabLabels.Count; i++)
            {
                if (tabLabels[i] != null)
                    tabLabels[i].color = (i == activeIndex) ? PacificRimUITheme.Text : PacificRimUITheme.TextMuted;
            }

            if (indicatorRect != null && tabButtons[activeIndex] != null)
            {
                var rt = tabButtons[activeIndex].GetComponent<RectTransform>();
                targetIndicatorX = rt.anchoredPosition.x;
                targetIndicatorW = rt.sizeDelta.x;
                if (!indicatorReady || silent)
                {
                    indicatorRect.anchoredPosition = new Vector2(targetIndicatorX, indicatorRect.anchoredPosition.y);
                    indicatorRect.sizeDelta = new Vector2(targetIndicatorW, indicatorRect.sizeDelta.y);
                    indicatorReady = true;
                }
            }

            if (!silent)
            {
                PacificRimUISound.Play(PacificRimUISound.SoundType.Tab);
                if (changed || onTabChanged != null)
                    onTabChanged?.Invoke(activeIndex);
            }
        }

        private void Update()
        {
            if (!indicatorReady || indicatorRect == null) return;
            float t = Mathf.Clamp01(Time.unscaledDeltaTime * 18f);
            float curX = Mathf.Lerp(indicatorRect.anchoredPosition.x, targetIndicatorX, t);
            float curW = Mathf.Lerp(indicatorRect.sizeDelta.x, targetIndicatorW, t);
            indicatorRect.anchoredPosition = new Vector2(curX, indicatorRect.anchoredPosition.y);
            indicatorRect.sizeDelta = new Vector2(curW, indicatorRect.sizeDelta.y);
        }
    }
}
