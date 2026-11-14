using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    public class PacificRimKeyPrompt : MonoBehaviour
    {
        public UIChamferGraphic keyBoxGraphic;
        public UICircleOutlineGraphic circleOutline;
        public Text keyLabel;
        public Text actionLabel;

        private bool isHighlighted;

        public void Setup(string key, string action)
        {
            if (keyLabel != null)
            {
                keyLabel.supportRichText = true;
                keyLabel.text = PacificRimUITheme.Track(key, 1, Mathf.Max(11, keyLabel.fontSize), true);
            }
            if (actionLabel != null)
            {
                actionLabel.supportRichText = true;
                actionLabel.text = PacificRimUITheme.Track(action, 2, Mathf.Max(11, actionLabel.fontSize), true);
            }
            SetHighlight(false);
        }

        public void SetHighlight(bool active, Color? highlightColor = null)
        {
            isHighlighted = active;
            Color accent = highlightColor ?? PacificRimUITheme.BlueBright;

            Color boxFill = active
                ? new Color(accent.r, accent.g, accent.b, 0.25f)
                : new Color(8f / 255f, 12f / 255f, 18f / 255f, 0.55f);
            Color boxBorder = active ? accent : PacificRimUITheme.Line;

            if (keyBoxGraphic != null)
                keyBoxGraphic.SetColors(boxFill, boxBorder);

            if (circleOutline != null)
                circleOutline.SetColors(boxFill, boxBorder);

            if (keyLabel != null)
                keyLabel.color = active ? accent : PacificRimUITheme.Text;

            if (actionLabel != null)
                actionLabel.color = active ? PacificRimUITheme.Text : PacificRimUITheme.TextDim;
        }
    }
}
