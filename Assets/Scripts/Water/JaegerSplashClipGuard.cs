using System;
using System.Collections.Generic;
using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Approximates a colossal mech or Kaiju body as a set of bone-driven capsules so water particles
    /// (droplets, mist, foam, bubbles) can be pushed back out to the body surface instead of
    /// clipping into the torso, arms, legs, or head. Capsules follow the live animated pose.
    /// </summary>
    public class JaegerSplashClipGuard : MonoBehaviour
    {
        public static JaegerSplashClipGuard Instance { get; private set; }
        public static readonly List<JaegerSplashClipGuard> ActiveGuards = new List<JaegerSplashClipGuard>();

        [Serializable]
        public class BodyCapsule
        {
            public string label;
            public Transform a;
            public Transform b;
            public float radius = 1.5f;
        }

        [Tooltip("Extra gap kept between particles and the body surface (meters).")]
        public float clearance = 0.35f;
        [Tooltip("Rebuild body capsules from the skeleton on Awake when the list is empty.")]
        public bool autoBuild = true;
        public List<BodyCapsule> capsules = new List<BodyCapsule>();

        private Vector3[] segA = new Vector3[0];
        private Vector3[] segB = new Vector3[0];
        private float[] segR = new float[0];
        private int segCount;
        private int refreshedFrame = -1;

        void Awake()
        {
            if (Instance == null) Instance = this;
            if (!ActiveGuards.Contains(this)) ActiveGuards.Add(this);
            if (autoBuild && (capsules == null || capsules.Count == 0))
            {
                AutoBuild();
            }
        }

        void OnEnable()
        {
            if (Instance == null) Instance = this;
            if (!ActiveGuards.Contains(this)) ActiveGuards.Add(this);
        }

        void OnDisable()
        {
            ActiveGuards.Remove(this);
            if (Instance == this)
            {
                Instance = ActiveGuards.Count > 0 ? ActiveGuards[0] : null;
            }
        }

        /// <summary>Pushes a point out of every active Jaeger and Kaiju clip guard.</summary>
        public static bool PushOutAll(ref Vector3 p, ref Vector3 v, bool horizontalOnly)
        {
            bool anyMoved = false;
            for (int i = ActiveGuards.Count - 1; i >= 0; i--)
            {
                JaegerSplashClipGuard g = ActiveGuards[i];
                if (g == null)
                {
                    ActiveGuards.RemoveAt(i);
                    continue;
                }
                if (!g.isActiveAndEnabled) continue;
                if (g.PushOut(ref p, ref v, horizontalOnly))
                {
                    anyMoved = true;
                }
            }
            return anyMoved;
        }

        [ContextMenu("Auto Build Body Capsules")]
        public void AutoBuild()
        {
            capsules = new List<BodyCapsule>();

            Transform hips = FindBone("Hips");
            if (hips != null)
            {
                Transform chest = FindBone("Chest");
                Transform head = FindBone("Head");
                Transform armL = FindBone("Upperarm.L");
                Transform armR = FindBone("Upperarm.R");
                Transform handL = FindBone("Hand.L");
                Transform handR = FindBone("Hand.R");
                Transform thighL = FindBone("Thigh.L");
                Transform thighR = FindBone("Thigh.R");
                Transform shinL = FindBone("Shin.L");
                Transform shinR = FindBone("Shin.R");
                Transform footL = FindBone("Foot.L");
                Transform footR = FindBone("Foot.R");

                Add("Torso", hips, chest, 3.0f);
                Add("UpperChest", chest, head, 2.7f);
                Add("Head", head, head, 1.9f);
                Add("ShoulderBar", armL, armR, 1.6f);
                Add("Arm.L", armL, handL, 1.6f);
                Add("Arm.R", armR, handR, 1.6f);
                Add("Fist.L", handL, handL, 1.5f);
                Add("Fist.R", handR, handR, 1.5f);
                Add("Thigh.L", thighL, shinL, 1.9f);
                Add("Thigh.R", thighR, shinR, 1.9f);
                Add("Shin.L", shinL, footL, 1.7f);
                Add("Shin.R", shinR, footR, 1.7f);
                Add("Foot.L", footL, footL, 2.0f);
                Add("Foot.R", footR, footR, 2.0f);
                return;
            }

            // Leatherback Kaiju rig fallback
            Transform lowerTorso = FindBone("LowerTorsoBone");
            Transform upperTorso = FindBone("UpperTorsoBone");
            Transform clavicle = FindBone("ClavicleBone");
            Transform kaijuHead = FindBone("UpperHead");
            Transform shoulderL = FindBone("ShoulderBone.L");
            Transform shoulderR = FindBone("ShoulderBone.R");
            Transform bicepL = FindBone("BicepBone.L");
            Transform bicepR = FindBone("BicepBone.R");
            Transform forearmL = FindBone("ForearmBone.L");
            Transform forearmR = FindBone("ForearmBone.R");
            Transform handL_K = FindBone("HandBone.L");
            Transform handR_K = FindBone("HandBone.R");
            Transform upperLegL = FindBone("UpperLegBone.L");
            Transform upperLegR = FindBone("UpperLegBone.R");
            Transform lowerLegL = FindBone("LowerLegBone.L");
            Transform lowerLegR = FindBone("LowerLegBone.R");
            Transform footL_K = FindBone("FootBone.L");
            Transform footR_K = FindBone("FootBone.R");

            Add("LowerTorso", lowerTorso, upperTorso, 3.8f);
            Add("UpperTorso", upperTorso, clavicle, 4.2f);
            Add("Head", clavicle, kaijuHead, 2.8f);
            Add("ShoulderBar", shoulderL, shoulderR, 2.6f);
            Add("Bicep.L", bicepL, forearmL, 2.6f);
            Add("Bicep.R", bicepR, forearmR, 2.6f);
            Add("Forearm.L", forearmL, handL_K, 2.6f);
            Add("Forearm.R", forearmR, handR_K, 2.6f);
            Add("UpperLeg.L", upperLegL, lowerLegL, 2.5f);
            Add("UpperLeg.R", upperLegR, lowerLegR, 2.5f);
            Add("LowerLeg.L", lowerLegL, footL_K, 2.5f);
            Add("LowerLeg.R", lowerLegR, footR_K, 2.5f);
        }

        private void Add(string label, Transform a, Transform b, float radius)
        {
            if (a == null || b == null) return;
            capsules.Add(new BodyCapsule { label = label, a = a, b = b, radius = radius });
        }

        private Transform FindBone(string boneName)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == boneName) return t;
            }
            return null;
        }

        /// <summary>Caches this frame's capsule end points. Cheap; safe to call repeatedly.</summary>
        public void Refresh()
        {
            if (refreshedFrame == Time.frameCount) return;
            refreshedFrame = Time.frameCount;

            int n = capsules != null ? capsules.Count : 0;
            if (segA.Length < n)
            {
                segA = new Vector3[n];
                segB = new Vector3[n];
                segR = new float[n];
            }

            segCount = 0;
            for (int i = 0; i < n; i++)
            {
                var c = capsules[i];
                if (c == null || c.a == null || c.b == null) continue;
                segA[segCount] = c.a.position;
                segB[segCount] = c.b.position;
                segR[segCount] = c.radius;
                segCount++;
            }
        }

        /// <summary>
        /// Pushes a point out of every body capsule and removes inward velocity.
        /// Returns true if the point was moved.
        /// </summary>
        public bool PushOut(ref Vector3 p, ref Vector3 v, bool horizontalOnly)
        {
            Refresh();
            bool moved = false;

            // Two passes resolve overlaps where neighboring capsules meet (e.g. shoulder + torso).
            for (int pass = 0; pass < 2; pass++)
            {
                bool movedThisPass = false;
                for (int i = 0; i < segCount; i++)
                {
                    Vector3 a = segA[i];
                    Vector3 ab = segB[i] - a;
                    float abLenSq = ab.sqrMagnitude;
                    float s = abLenSq > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / abLenSq) : 0f;
                    Vector3 closest = a + ab * s;

                    Vector3 d = p - closest;
                    if (horizontalOnly) d.y = 0f;

                    float minDist = segR[i] + clearance;
                    float distSq = d.sqrMagnitude;
                    if (distSq >= minDist * minDist) continue;

                    float dist = Mathf.Sqrt(distSq);
                    Vector3 n;
                    if (dist > 0.0001f)
                    {
                        n = d / dist;
                    }
                    else
                    {
                        Vector3 fallback = horizontalOnly ? new Vector3(p.x - transform.position.x, 0f, p.z - transform.position.z) : (p - transform.position);
                        n = fallback.sqrMagnitude > 0.0001f ? fallback.normalized : transform.forward;
                    }

                    Vector3 pushed = closest + n * minDist;
                    if (horizontalOnly) pushed.y = p.y;
                    p = pushed;

                    float inward = Vector3.Dot(v, n);
                    if (inward < 0f)
                    {
                        v -= n * inward;           // slide along the body instead of passing through it
                        v += n * (-inward * 0.15f); // tiny rebound so spray visibly peels off the armor
                    }

                    moved = true;
                    movedThisPass = true;
                }

                if (!movedThisPass) break;
            }

            return moved;
        }

        /// <summary>Returns true when a point lies inside any (clearance-inflated) body capsule.</summary>
        public bool IsInsideBody(Vector3 p)
        {
            Refresh();
            for (int i = 0; i < segCount; i++)
            {
                Vector3 a = segA[i];
                Vector3 ab = segB[i] - a;
                float abLenSq = ab.sqrMagnitude;
                float s = abLenSq > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / abLenSq) : 0f;
                float minDist = segR[i] + clearance;
                if ((p - (a + ab * s)).sqrMagnitude < minDist * minDist) return true;
            }
            return false;
        }

        void OnDrawGizmosSelected()
        {
            if (capsules == null) return;
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.5f);
            foreach (var c in capsules)
            {
                if (c == null || c.a == null || c.b == null) continue;
                Gizmos.DrawWireSphere(c.a.position, c.radius);
                Gizmos.DrawWireSphere(c.b.position, c.radius);
                Gizmos.DrawLine(c.a.position, c.b.position);
            }
        }
    }
}
