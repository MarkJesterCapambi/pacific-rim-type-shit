using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIDividerGraphic : MaskableGraphic
    {
        [SerializeField] public bool horizontal = true;
        [SerializeField] public float fadeEdgePercent = 0.15f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0.5f || r.height <= 0.5f) return;

            Color cFull = color;
            Color cZero = new Color(cFull.r, cFull.g, cFull.b, 0f);
            float f = Mathf.Clamp(fadeEdgePercent, 0f, 0.45f);

            if (horizontal)
            {
                float x0 = r.xMin;
                float x1 = Mathf.Lerp(r.xMin, r.xMax, f);
                float x2 = Mathf.Lerp(r.xMin, r.xMax, 1f - f);
                float x3 = r.xMax;
                float y0 = r.yMin;
                float y1 = r.yMax;

                AddQuad(vh, x0, y0, x1, y1, f > 0.001f ? cZero : cFull, cFull);
                AddQuad(vh, x1, y0, x2, y1, cFull, cFull);
                AddQuad(vh, x2, y0, x3, y1, cFull, f > 0.001f ? cZero : cFull);
            }
            else
            {
                float y0 = r.yMin;
                float y1 = Mathf.Lerp(r.yMin, r.yMax, f);
                float y2 = Mathf.Lerp(r.yMin, r.yMax, 1f - f);
                float y3 = r.yMax;

                AddVerticalQuad(vh, r.xMin, y0, r.xMax, y1, f > 0.001f ? cZero : cFull, cFull);
                AddVerticalQuad(vh, r.xMin, y1, r.xMax, y2, cFull, cFull);
                AddVerticalQuad(vh, r.xMin, y2, r.xMax, y3, cFull, f > 0.001f ? cZero : cFull);
            }
        }

        private static void AddQuad(VertexHelper vh, float x0, float y0, float x1, float y1, Color left, Color right)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector2(x0, y0), left, Vector2.zero);
            vh.AddVert(new Vector2(x0, y1), left, Vector2.zero);
            vh.AddVert(new Vector2(x1, y1), right, Vector2.zero);
            vh.AddVert(new Vector2(x1, y0), right, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        private static void AddVerticalQuad(VertexHelper vh, float x0, float y0, float x1, float y1, Color bottom, Color top)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector2(x0, y0), bottom, Vector2.zero);
            vh.AddVert(new Vector2(x0, y1), top, Vector2.zero);
            vh.AddVert(new Vector2(x1, y1), top, Vector2.zero);
            vh.AddVert(new Vector2(x1, y0), bottom, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}
