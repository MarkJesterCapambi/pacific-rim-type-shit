using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class UICircleOutlineGraphic : MaskableGraphic
    {
        [SerializeField] public int segments = 32;
        [SerializeField] public float thickness = 1.25f;
        [SerializeField] public Color fillColor = new Color(8f / 255f, 12f / 255f, 18f / 255f, 0.55f);
        [SerializeField] public Color borderColor = new Color(1f, 1f, 1f, 0.28f);

        public void SetColors(Color fill, Color border)
        {
            fillColor = fill;
            borderColor = border;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 1f || r.height <= 1f) return;

            int segs = Mathf.Clamp(segments, 12, 64);
            Vector2 center = r.center;
            float rx = r.width * 0.5f;
            float ry = r.height * 0.5f;
            float t = Mathf.Clamp(thickness, 0.5f, Mathf.Min(rx, ry) * 0.5f);

            Color fc = fillColor * color;
            if (fc.a > 0.001f)
            {
                vh.AddVert(center, fc, Vector2.zero);
                for (int i = 0; i < segs; i++)
                {
                    float a = (i / (float)segs) * Mathf.PI * 2f;
                    vh.AddVert(center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry), fc, Vector2.zero);
                }
                for (int i = 0; i < segs; i++)
                {
                    vh.AddTriangle(0, 1 + i, 1 + ((i + 1) % segs));
                }
            }

            Color bc = borderColor * color;
            if (bc.a > 0.001f)
            {
                int baseIdx = vh.currentVertCount;
                for (int i = 0; i < segs; i++)
                {
                    float a = (i / (float)segs) * Mathf.PI * 2f;
                    float cos = Mathf.Cos(a);
                    float sin = Mathf.Sin(a);
                    vh.AddVert(center + new Vector2(cos * rx, sin * ry), bc, Vector2.zero);
                    vh.AddVert(center + new Vector2(cos * (rx - t), sin * (ry - t)), bc, Vector2.zero);
                }
                for (int i = 0; i < segs; i++)
                {
                    int next = (i + 1) % segs;
                    int o0 = baseIdx + i * 2;
                    int i0 = o0 + 1;
                    int o1 = baseIdx + next * 2;
                    int i1 = o1 + 1;
                    vh.AddTriangle(o0, o1, i1);
                    vh.AddTriangle(o0, i1, i0);
                }
            }
        }
    }
}
