using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIChamferGraphic : MaskableGraphic
    {
        [SerializeField] public float chamferSize = 10f;
        [SerializeField] public float borderThickness = 1f;
        [SerializeField] public Color fillColor = new Color(8f / 255f, 12f / 255f, 18f / 255f, 0.45f);
        [SerializeField] public Color borderColor = new Color(1f, 1f, 1f, 0.22f);
        [SerializeField] public bool chamferTopLeft = false;
        [SerializeField] public bool chamferTopRight = true;
        [SerializeField] public bool chamferBottomRight = false;
        [SerializeField] public bool chamferBottomLeft = true;
        [SerializeField] public bool drawFill = true;
        [SerializeField] public bool drawBorder = true;

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

            float maxCut = Mathf.Min(r.width * 0.45f, r.height * 0.45f);
            float c = Mathf.Clamp(chamferSize, 0f, maxCut);

            var outerPts = BuildChamferPolygon(r, c);
            if (outerPts.Count < 3) return;

            if (drawFill && fillColor.a * color.a > 0.001f)
            {
                Color32 fc = MultiplyColor(fillColor, color);
                int startIdx = vh.currentVertCount;
                for (int i = 0; i < outerPts.Count; i++)
                {
                    vh.AddVert(outerPts[i], fc, Vector2.zero);
                }
                for (int i = 1; i < outerPts.Count - 1; i++)
                {
                    vh.AddTriangle(startIdx, startIdx + i, startIdx + i + 1);
                }
            }

            if (drawBorder && borderThickness > 0.05f && borderColor.a * color.a > 0.001f)
            {
                float t = Mathf.Min(borderThickness, Mathf.Min(r.width, r.height) * 0.4f);
                Rect innerRect = new Rect(r.x + t, r.y + t, Mathf.Max(0.5f, r.width - t * 2f), Mathf.Max(0.5f, r.height - t * 2f));
                float innerCut = Mathf.Max(0f, c - t * 0.414f);
                var innerPts = BuildChamferPolygon(innerRect, Mathf.Min(innerCut, Mathf.Min(innerRect.width, innerRect.height) * 0.45f));

                if (innerPts.Count == outerPts.Count)
                {
                    Color32 bc = MultiplyColor(borderColor, color);
                    int baseIdx = vh.currentVertCount;
                    int n = outerPts.Count;
                    for (int i = 0; i < n; i++)
                    {
                        vh.AddVert(outerPts[i], bc, Vector2.zero);
                        vh.AddVert(innerPts[i], bc, Vector2.zero);
                    }
                    for (int i = 0; i < n; i++)
                    {
                        int next = (i + 1) % n;
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

        private List<Vector2> BuildChamferPolygon(Rect r, float c)
        {
            var pts = new List<Vector2>(8);
            float l = r.xMin;
            float ri = r.xMax;
            float b = r.yMin;
            float t = r.yMax;

            // Top-Left corner
            if (chamferTopLeft && c > 0.1f)
            {
                pts.Add(new Vector2(l, t - c));
                pts.Add(new Vector2(l + c, t));
            }
            else
            {
                pts.Add(new Vector2(l, t));
            }

            // Top-Right corner
            if (chamferTopRight && c > 0.1f)
            {
                pts.Add(new Vector2(ri - c, t));
                pts.Add(new Vector2(ri, t - c));
            }
            else
            {
                pts.Add(new Vector2(ri, t));
            }

            // Bottom-Right corner
            if (chamferBottomRight && c > 0.1f)
            {
                pts.Add(new Vector2(ri, b + c));
                pts.Add(new Vector2(ri - c, b));
            }
            else
            {
                pts.Add(new Vector2(ri, b));
            }

            // Bottom-Left corner
            if (chamferBottomLeft && c > 0.1f)
            {
                pts.Add(new Vector2(l + c, b));
                pts.Add(new Vector2(l, b + c));
            }
            else
            {
                pts.Add(new Vector2(l, b));
            }

            return pts;
        }

        private static Color32 MultiplyColor(Color c1, Color c2)
        {
            return new Color(c1.r * c2.r, c1.g * c2.g, c1.b * c2.b, c1.a * c2.a);
        }
    }
}
