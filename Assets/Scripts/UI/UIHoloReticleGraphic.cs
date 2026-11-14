using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIHoloReticleGraphic : MaskableGraphic
    {
        [SerializeField] public int segments = 48;
        [SerializeField] public float ringThickness = 1.5f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 4f || r.height <= 4f) return;

            Vector2 c = r.center;
            float outerR = Mathf.Min(r.width, r.height) * 0.5f;
            float innerR = Mathf.Max(1f, outerR - ringThickness);

            Color main = color;
            Color dim = new Color(main.r, main.g, main.b, main.a * 0.35f);

            // Outer broken tactical arcs (4 quadrants with small gaps)
            for (int q = 0; q < 4; q++)
            {
                float aStart = (q * 90f + 12f) * Mathf.Deg2Rad;
                float aEnd = (q * 90f + 78f) * Mathf.Deg2Rad;
                int steps = Mathf.Max(6, segments / 4);
                int baseIdx = vh.currentVertCount;

                for (int i = 0; i <= steps; i++)
                {
                    float a = Mathf.Lerp(aStart, aEnd, i / (float)steps);
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    vh.AddVert(c + dir * outerR, main, Vector2.zero);
                    vh.AddVert(c + dir * innerR, main, Vector2.zero);
                }

                for (int i = 0; i < steps; i++)
                {
                    int v0 = baseIdx + i * 2;
                    int v1 = v0 + 1;
                    int v2 = v0 + 2;
                    int v3 = v0 + 3;
                    vh.AddTriangle(v0, v2, v3);
                    vh.AddTriangle(v0, v3, v1);
                }
            }

            // Inner subtle guide ring
            float midR = outerR * 0.58f;
            float midIn = Mathf.Max(1f, midR - 1f);
            int ringSteps = Mathf.Max(16, segments);
            int rBase = vh.currentVertCount;
            for (int i = 0; i < ringSteps; i++)
            {
                float a = (i / (float)ringSteps) * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(c + dir * midR, dim, Vector2.zero);
                vh.AddVert(c + dir * midIn, dim, Vector2.zero);
            }
            for (int i = 0; i < ringSteps; i++)
            {
                int next = (i + 1) % ringSteps;
                int o0 = rBase + i * 2;
                int i0 = o0 + 1;
                int o1 = rBase + next * 2;
                int i1 = o1 + 1;
                vh.AddTriangle(o0, o1, i1);
                vh.AddTriangle(o0, i1, i0);
            }
        }
    }
}
