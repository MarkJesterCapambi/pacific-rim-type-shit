using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class UISoftBlobGraphic : MaskableGraphic
    {
        [SerializeField] public int segments = 28;
        [SerializeField] public Color blobColor = new Color(36f / 255f, 148f / 255f, 226f / 255f, 0.16f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 1f || r.height <= 1f) return;

            int segs = Mathf.Clamp(segments, 12, 64);
            Vector2 center = r.center;
            float rx = r.width * 0.5f;
            float ry = r.height * 0.5f;

            Color cCenter = blobColor * color;
            Color cMid = new Color(cCenter.r, cCenter.g, cCenter.b, cCenter.a * 0.40f);
            Color cEdge = new Color(cCenter.r, cCenter.g, cCenter.b, 0f);

            vh.AddVert(center, cCenter, new Vector2(0.5f, 0.5f));

            // Mid ring at r = 0.52 for smooth Gaussian falloff
            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(center + new Vector2(dir.x * rx * 0.52f, dir.y * ry * 0.52f), cMid, Vector2.zero);
            }

            // Outer ring at r = 1.0 -> 0 alpha
            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(center + new Vector2(dir.x * rx, dir.y * ry), cEdge, Vector2.zero);
            }

            // Center fan -> mid ring
            for (int i = 0; i < segs; i++)
            {
                int m0 = 1 + i;
                int m1 = 1 + ((i + 1) % segs);
                vh.AddTriangle(0, m0, m1);
            }

            // Mid ring -> outer ring
            int outerOffset = 1 + segs;
            for (int i = 0; i < segs; i++)
            {
                int next = (i + 1) % segs;
                int m0 = 1 + i;
                int m1 = 1 + next;
                int o0 = outerOffset + i;
                int o1 = outerOffset + next;
                vh.AddTriangle(m0, o0, o1);
                vh.AddTriangle(m0, o1, m1);
            }
        }
    }
}
