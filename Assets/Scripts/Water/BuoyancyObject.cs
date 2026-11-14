using System.Collections.Generic;
using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Archimedes buoyancy for floating ocean objects (debris, buoys, hulls, wreckage).
    /// Samples WaterSystem swells and footstep shockwaves to tilt and bob realistically.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BuoyancyObject : MonoBehaviour
    {
        [Header("Buoyancy Settings")]
        public float buoyancyFactor = 1.3f;
        public float submersionDepth = 2.0f;
        public float waterDrag = 2.5f;
        public float waterAngularDrag = 2.0f;
        public float waveTiltTorque = 4.0f;

        [Header("Sample Points")]
        public List<Vector3> floaterPoints = new List<Vector3>();

        private Rigidbody rb;
        private WaterSystem water;
        private float initialDrag;
        private float initialAngularDrag;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
#if UNITY_6000_0_OR_NEWER
            initialDrag = rb.linearDamping;
            initialAngularDrag = rb.angularDamping;
#else
            initialDrag = rb.drag;
            initialAngularDrag = rb.angularDrag;
#endif

            if (floaterPoints == null || floaterPoints.Count == 0)
            {
                Collider col = GetComponent<Collider>();
                if (col != null)
                {
                    Vector3 ext = col.bounds.extents;
                    Vector3 lExt = new Vector3(
                        ext.x / transform.lossyScale.x,
                        ext.y / transform.lossyScale.y,
                        ext.z / transform.lossyScale.z
                    );
                    float by = -lExt.y * 0.7f;
                    floaterPoints.Add(new Vector3(-lExt.x * 0.7f, by, -lExt.z * 0.7f));
                    floaterPoints.Add(new Vector3(lExt.x * 0.7f, by, -lExt.z * 0.7f));
                    floaterPoints.Add(new Vector3(-lExt.x * 0.7f, by, lExt.z * 0.7f));
                    floaterPoints.Add(new Vector3(lExt.x * 0.7f, by, lExt.z * 0.7f));
                }
                else
                {
                    floaterPoints.Add(Vector3.zero);
                }
            }
        }

        void Start()
        {
            water = WaterSystem.Instance;
        }

        void FixedUpdate()
        {
            if (water == null)
            {
                water = WaterSystem.Instance;
                if (water == null) return;
            }

            int count = floaterPoints.Count;
            if (count == 0) return;

            int submergedCount = 0;
            float totalRatio = 0f;
            Vector3 avgNormal = Vector3.up;
            float forcePerPoint = (rb.mass * Mathf.Abs(Physics.gravity.y) * buoyancyFactor) / count;

            for (int i = 0; i < count; i++)
            {
                Vector3 worldPt = transform.TransformPoint(floaterPoints[i]);
                float waterY = water.GetWaterHeight(worldPt);
                float depth = waterY - worldPt.y;

                if (depth > 0f)
                {
                    submergedCount++;
                    float subRatio = Mathf.Clamp01(depth / submersionDepth);
                    totalRatio += subRatio;

                    // Buoyancy force
                    rb.AddForceAtPosition(Vector3.up * (forcePerPoint * subRatio), worldPt, ForceMode.Force);

                    // Point hydrodynamic damping
                    Vector3 ptVel = rb.GetPointVelocity(worldPt);
                    rb.AddForceAtPosition(-ptVel * (waterDrag * subRatio * 0.5f), worldPt, ForceMode.Force);

                    avgNormal += water.GetWaterNormal(worldPt);
                }
            }

            if (submergedCount > 0)
            {
                float avgSub = totalRatio / count;
#if UNITY_6000_0_OR_NEWER
                rb.linearDamping = Mathf.Lerp(initialDrag, waterDrag, avgSub);
                rb.angularDamping = Mathf.Lerp(initialAngularDrag, waterAngularDrag, avgSub);
#else
                rb.drag = Mathf.Lerp(initialDrag, waterDrag, avgSub);
                rb.angularDrag = Mathf.Lerp(initialAngularDrag, waterAngularDrag, avgSub);
#endif
                if (waveTiltTorque > 0f)
                {
                    avgNormal = (avgNormal / (submergedCount + 1)).normalized;
                    Vector3 torque = Vector3.Cross(transform.up, avgNormal);
                    rb.AddTorque(torque * (waveTiltTorque * avgSub), ForceMode.Acceleration);
                }
            }
            else
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearDamping = initialDrag;
                rb.angularDamping = initialAngularDrag;
#else
                rb.drag = initialDrag;
                rb.angularDrag = initialAngularDrag;
#endif
            }
        }
    }
}
