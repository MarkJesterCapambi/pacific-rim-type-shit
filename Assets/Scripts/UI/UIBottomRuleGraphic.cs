using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    /// <summary>
    /// Battlefront UI.bottomRule equivalent: full-width 1px hairline rule with 15 ticks
    /// and symmetric ±35-degree accent slashes flanking the horizontal line.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIBottomRuleGraphic : MaskableGraphic
    {
        [SerializeField] public int tickCount = 15;
        [SerializeField] public Color lineColor = new Color(1f, 1f, 1f, 0.22f);
        [SerializeField] public Color accentColor = new Color(36f / 255f, 148f / 255f, 226f / 255f, 0.85f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 10f || r.height <= 2f) return;

            Color lc = lineColor * color;
            Color lcDim = new Color(lc.r, lc.g, lc.b, lc.a * 0.65f);
            Color lcZero = new Color(lc.r, lc.g, lc.b, 0f);
            Color ac = accentColor * color;

            float cy = r.center.y;
            float fade = r.width * 0.08f;

            // Main horizontal 1px hairline with faded outer ends
            AddHSeg(vh, r.xMin, r.xMin + fade, cy - 0.5f, cy + 0.5f, lcZero, lc);
            AddHSeg(vh, r.xMin + fade, r.xMax - fade, cy - 0.5f, cy + 0.5f, lc, lc);
            AddHSeg(vh, r.xMax - fade, r.xMax, cy - 0.5f, cy + 0.5f, lc, lcZero);

            // 15 subtle vertical ticks spanning inner 84%
            int ticks = Mathf.Max(3, tickCount);
            float startX = r.xMin + r.width * 0.08f;
            float endX = r.xMax - r.width * 0.08f;
            for (int i = 0; i < ticks; i++)
            {
                float u = i / (float)(ticks - 1);
                float tx = Mathf.Lerp(startX, endX, u);
                bool isCenter = i == ticks / 2;
                float tickH = isCenter ? 8f : (i % 2 == 0 ? 5f : 3f);
                Color tc = isCenter ? ac : lcDim;
                AddHSeg(vh, tx - 0.5f, tx + 0.5f, cy - tickH * 0.5f, cy + tickH * 0.5f, tc, tc);
            }

            // Symmetric +-35 deg tactical slashes flanking center
            float slashOffset = Mathf.Min(160f, r.width * 0.18f);
            DrawSlash(vh, r.center.x - slashOffset, cy, -35f, 12f, 1.5f, ac);
            DrawSlash(vh, r.center.x + slashOffset, cy, 35f, 12f, 1.5f, ac);
        }

        private static void AddHSeg(VertexHelper vh, float x0, float x1, float y0, float y1, Color cL, Color cR)
        {
            int idx = vh.currentVertCount;
            vh.AddVert(new Vector2(x0, y0), cL, Vector2.zero);
            vh.AddVert(new Vector2(x0, y1), cL, Vector2.zero);
            vh.AddVert(new Vector2(x1, y1), cR, Vector2.zero);
            vh.AddVert(new Vector2(x1, y0), cR, Vector2.zero);
            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx + 2, idx + 3, idx);
        }

        private static void DrawSlash(VertexHelper vh, float cx, float cy, float angleDeg, float length, float width, Color c)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            Vector2 perp = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            Vector2 p0 = new Vector2(cx, cy) - dir * (length * 0.5f);
            Vector2 p1 = new Vector2(cx, cy) + dir * (length * 0.5f);

            int idx = vh.currentVertCount;
            vh.AddVert(p0 - perp, c, Vector2.zero);
            vh.AddVert(p0 + perp, c, Vector2.zero);
            vh.AddVert(p1 + perp, c, Vector2.zero);
            vh.AddVert(p1 - perp, c, Vector2.zero);
            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx + 2, idx + 3, idx);
        }
    }
}
