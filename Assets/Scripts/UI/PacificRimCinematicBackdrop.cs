using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PacificRimUI
{
    /// <summary>
    /// Battlefront UI.cinematic equivalent:
    /// Deep backdrop at 0.88 alpha with slow atmospheric blobs (UISoftBlobGraphic),
    /// 60 drifting embers, and edge vignettes animated via unscaledDeltaTime.
    /// </summary>
    public class PacificRimCinematicBackdrop : MonoBehaviour
    {
        public CanvasGroup backdropGroup;
        public List<RectTransform> blobs = new List<RectTransform>();
        public List<RectTransform> embers = new List<RectTransform>();

        private Vector2[] blobBasePos;
        private float[] blobFreq;
        private float[] blobPhase;
        private Vector2[] emberVel;
        private float[] emberPhase;
        private Image[] emberImages;
        private float elapsed;

        private void Awake()
        {
            InitializeMotion();
        }

        public void InitializeMotion()
        {
            int bCount = blobs.Count;
            blobBasePos = new Vector2[bCount];
            blobFreq = new float[bCount];
            blobPhase = new float[bCount];
            for (int i = 0; i < bCount; i++)
            {
                if (blobs[i] != null)
                    blobBasePos[i] = blobs[i].anchoredPosition;
                blobFreq[i] = 0.22f + (i % 4) * 0.08f;
                blobPhase[i] = i * 1.17f;
            }

            int eCount = embers.Count;
            emberVel = new Vector2[eCount];
            emberPhase = new float[eCount];
            emberImages = new Image[eCount];
            for (int i = 0; i < eCount; i++)
            {
                // Deterministic pseudo-random motion for consistent feel across reloads
                float seedA = Mathf.Abs(Mathf.Sin((i + 1) * 12.9898f));
                float seedB = Mathf.Abs(Mathf.Cos((i + 1) * 78.233f));
                emberVel[i] = new Vector2(Mathf.Lerp(-12f, 22f, seedA), Mathf.Lerp(14f, 44f, seedB));
                emberPhase[i] = i * 0.63f;
                if (embers[i] != null)
                    emberImages[i] = embers[i].GetComponent<Image>();
            }
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            elapsed += dt;

            if (blobBasePos == null || blobBasePos.Length != blobs.Count ||
                emberVel == null || emberVel.Length != embers.Count)
            {
                InitializeMotion();
            }

            // Gentle drift on atmospheric light blobs
            for (int i = 0; i < blobs.Count; i++)
            {
                RectTransform rt = blobs[i];
                if (rt == null) continue;
                float t = elapsed * blobFreq[i] + blobPhase[i];
                Vector2 offset = new Vector2(Mathf.Sin(t) * 28f, Mathf.Cos(t * 0.85f) * 18f);
                rt.anchoredPosition = blobBasePos[i] + offset;
            }

            // Upward drifting cyan-blue / subtle ember sparks
            for (int i = 0; i < embers.Count; i++)
            {
                RectTransform rt = embers[i];
                if (rt == null) continue;

                Vector2 p = rt.anchoredPosition + emberVel[i] * dt;
                float sway = Mathf.Sin(elapsed * 1.4f + emberPhase[i]) * 8f * dt;
                p.x += sway;

                if (p.y > 560f) p.y = -560f;
                if (p.y < -560f) p.y = 560f;
                if (p.x > 1000f) p.x = -1000f;
                if (p.x < -1000f) p.x = 1000f;

                rt.anchoredPosition = p;

                if (emberImages[i] != null)
                {
                    Color c = emberImages[i].color;
                    float twinkle = 0.25f + 0.55f * (0.5f + 0.5f * Mathf.Sin(elapsed * 2.6f + emberPhase[i] * 2f));
                    c.a = twinkle;
                    emberImages[i].color = c;
                }
            }
        }
    }
}
