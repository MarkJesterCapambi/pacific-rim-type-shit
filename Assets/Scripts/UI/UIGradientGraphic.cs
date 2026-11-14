using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIGradientGraphic : MaskableGraphic
    {
        [SerializeField] public Color colorTopLeft = Color.clear;
        [SerializeField] public Color colorTopRight = Color.clear;
        [SerializeField] public Color colorBottomRight = Color.black;
        [SerializeField] public Color colorBottomLeft = Color.black;

        public void SetVertical(Color top, Color bottom)
        {
            colorTopLeft = top;
            colorTopRight = top;
            colorBottomLeft = bottom;
            colorBottomRight = bottom;
            SetVerticesDirty();
        }

        public void SetHorizontal(Color left, Color right)
        {
            colorTopLeft = left;
            colorBottomLeft = left;
            colorTopRight = right;
            colorBottomRight = right;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0.5f || r.height <= 0.5f) return;

            vh.AddVert(new Vector2(r.xMin, r.yMin), colorBottomLeft * color, new Vector2(0f, 0f));
            vh.AddVert(new Vector2(r.xMin, r.yMax), colorTopLeft * color, new Vector2(0f, 1f));
            vh.AddVert(new Vector2(r.xMax, r.yMax), colorTopRight * color, new Vector2(1f, 1f));
            vh.AddVert(new Vector2(r.xMax, r.yMin), colorBottomRight * color, new Vector2(1f, 0f));

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
