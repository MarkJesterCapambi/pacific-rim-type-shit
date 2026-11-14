using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    public static class PacificRimUIElements
    {
        public static void SetStretch(RectTransform rt, Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin ?? Vector2.zero;
            rt.offsetMax = offsetMax ?? Vector2.zero;
        }
    }

    // Individual UI MonoBehaviours and MaskableGraphics live in their own matching .cs files
    // under Assets/Scripts/UI/ so Unity serializes MonoScript references cleanly.
}
