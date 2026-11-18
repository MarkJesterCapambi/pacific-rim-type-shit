using System.Collections.Generic;
using UnityEngine;
using PacificRimAudio;
using PacificRimWater;

namespace PacificRimEnvironment
{
    /// <summary>
    /// Smaller destructible collateral layer for Hong Kong City Center combat:
    /// - SkybridgeSegment: Enclosed glass pedestrian skybridges (16m-22m) that shatter when Gipsy or Otachi smashes through.
    /// - FlyoverChunk: ~15m elevated concrete highway flyover chunks (12m-16m) along the South side of the central plaza.
    /// - NeonFacadeAttachment: Breakable cantilevered neon signs and rooftop billboards that spark, dim, and fall when brushed or hit.
    /// Uses trigger colliders before breaking and ignores Titan collisions after breaking so mechs/Kaiju never snag.
    /// </summary>
    public class HongKongDestructibleStructure : MonoBehaviour
    {
        public enum StructureKind
        {
            SkybridgeSegment,
            FlyoverChunk,
            NeonFacadeAttachment
        }

        [Header("Destructible Behavior")]
        public StructureKind kind = StructureKind.SkybridgeSegment;
        public float debrisMass = 16f;
        public float scatterImpulse = 18f;
        public float upwardPop = 5.5f;
        public float tumbleTorque = 6.5f;
        public bool cascadeAdjacentSkybridge = true;
        public float cascadeRadius = 11.5f;

        private bool hasBroken;
        private static readonly HashSet<HongKongDestructibleStructure> ActiveStructures = new HashSet<HongKongDestructibleStructure>();
        private static readonly int IdEmissionColor = Shader.PropertyToID("_EmissionColor");

        private void OnEnable()
        {
            ActiveStructures.Add(this);
        }

        private void OnDisable()
        {
            ActiveStructures.Remove(this);
        }

        public static void TriggerBreakInRadius(Vector3 impactCenter, float radius, Vector3 pushVelocity, float forceScale = 1.0f)
        {
            float rSq = radius * radius;
            var snapshot = new List<HongKongDestructibleStructure>(ActiveStructures);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var item = snapshot[i];
                if (item == null || item.hasBroken) continue;

                Vector3 delta = item.transform.position - impactCenter;
                if (delta.sqrMagnitude <= rSq)
                {
                    Vector3 pushDir = pushVelocity.sqrMagnitude > 0.25f ? pushVelocity.normalized : delta.normalized;
                    item.BreakFromImpact(impactCenter, pushDir, forceScale);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasBroken || other == null) return;

            var jaeger = other.GetComponentInParent<SimpleJaegerController>();
            var kaiju = other.GetComponentInParent<KaijuAI>();
            if (jaeger == null && kaiju == null) return;

            Transform titan = jaeger != null ? jaeger.transform : kaiju.transform;
            Vector3 pushDir = transform.position - titan.position;
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude < 0.01f) pushDir = titan.forward;

            BreakFromImpact(titan.position, pushDir.normalized, jaeger != null && jaeger.IsDashing ? 1.45f : 1.0f);
        }

        public void BreakFromImpact(Vector3 impactOrigin, Vector3 pushDir, float forceScale = 1.0f)
        {
            if (hasBroken) return;
            hasBroken = true;

            Vector3 dir = pushDir.sqrMagnitude > 0.001f ? pushDir.normalized : Vector3.forward;
            Vector3 fxCenter = transform.position;

            // 1. Disable trigger collider(s) on root so nothing re-triggers or blocks movement
            var rootCols = GetComponents<Collider>();
            for (int i = 0; i < rootCols.Length; i++)
            {
                if (rootCols[i] != null) rootCols[i].enabled = false;
            }

            // 2. Sparks, dust, audio & camera tremor tailored to structure type
            if (WaterSplashManager.Instance != null)
            {
                bool isNeon = kind == StructureKind.NeonFacadeAttachment;
                WaterSplashManager.Instance.SpawnCombatSparks(fxCenter, Vector3.up, isBlocked: isNeon, isHeavy: !isNeon, isKaijuTarget: false);
                if (isNeon)
                {
                    WaterSplashManager.Instance.SpawnParrySparks(fxCenter, dir);
                }
                WaterSplashManager.Instance.EmitVolumetricMist(
                    fxCenter,
                    dir * 4.0f + Vector3.up * 1.5f,
                    kind == StructureKind.FlyoverChunk ? 11.5f : 7.5f,
                    kind == StructureKind.FlyoverChunk ? 0.30f : 0.20f,
                    3.2f);
            }

            if (PacificRimAudioManager.Instance != null)
            {
                PacificRimAudioManager.Instance.PlayDebrisImpact(fxCenter, kind == StructureKind.FlyoverChunk ? 1.15f : 0.90f);
                PacificRimAudioManager.Instance.PlayAtPosition("Metal Impact 7", fxCenter, kind == StructureKind.NeonFacadeAttachment ? 0.85f : 1.0f);
                if (kind == StructureKind.NeonFacadeAttachment)
                {
                    PacificRimAudioManager.Instance.PlayAtPosition("Laser Impact Light_6", fxCenter, 0.55f, 0.9f, 1.25f);
                }
            }

            if (kind != StructureKind.NeonFacadeAttachment)
            {
                Camera cam = Camera.main;
                cam?.GetComponent<PacificRimCameraShake>()?.Shake(kind == StructureKind.FlyoverChunk ? 0.55f : 0.35f, 0.30f);
            }

            // 3. Turn visual sub-pieces into falling debris (or single falling neon sign)
            if (kind == StructureKind.NeonFacadeAttachment)
            {
                DetachWholeNeonAttachment(dir, forceScale);
            }
            else
            {
                ShatterIntoDebrisChunks(dir, forceScale);
            }

            // 4. Optional local cascade for skybridge spans so hitting middle breaks neighboring glass span cleanly
            if (kind == StructureKind.SkybridgeSegment && cascadeAdjacentSkybridge && transform.parent != null)
            {
                var siblings = transform.parent.GetComponentsInChildren<HongKongDestructibleStructure>(false);
                float rSq = cascadeRadius * cascadeRadius;
                for (int i = 0; i < siblings.Length; i++)
                {
                    var sib = siblings[i];
                    if (sib == null || sib == this || sib.hasBroken) continue;
                    if ((sib.transform.position - fxCenter).sqrMagnitude <= rSq)
                    {
                        sib.cascadeAdjacentSkybridge = false;
                        sib.BreakFromImpact(impactOrigin, dir, forceScale * 0.72f);
                    }
                }
            }
        }

        private void DetachWholeNeonAttachment(Vector3 dir, float forceScale)
        {
            // Dim emissive signage via MaterialPropertyBlock to simulate severed neon power
            var propBlock = new MaterialPropertyBlock();
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var mr = renderers[i];
                if (mr == null) continue;
                mr.GetPropertyBlock(propBlock);
                propBlock.SetColor(IdEmissionColor, new Color(0.08f, 0.08f, 0.10f, 1f));
                mr.SetPropertyBlock(propBlock);
            }

            // Convert root or main board into falling physics body that ignores Titans
            BoxCollider fallCol = gameObject.AddComponent<BoxCollider>();
            fallCol.isTrigger = false;
            fallCol.size = new Vector3(3.5f, 3.5f, 1.5f);
            IgnoreTitanCollisions(fallCol);

            Rigidbody rb = gameObject.GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(4f, debrisMass);
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.linearDamping = 0.25f;
            rb.angularDamping = 0.35f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            Vector3 launch = dir * (scatterImpulse * 0.55f * forceScale) + Vector3.up * Random.Range(1.0f, 3.5f);
            rb.AddForce(launch, ForceMode.VelocityChange);
            rb.AddTorque(Random.onUnitSphere * tumbleTorque, ForceMode.VelocityChange);
        }

        private void ShatterIntoDebrisChunks(Vector3 dir, float forceScale)
        {
            MeshRenderer[] parts = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                GameObject part = parts[i].gameObject;
                if (part == null) continue;

                // Detach from parent so each piece tumbles independently
                part.transform.SetParent(null, true);

                Collider partCol = part.GetComponent<Collider>();
                if (partCol == null) partCol = part.AddComponent<BoxCollider>();
                partCol.isTrigger = false;
                partCol.enabled = true;
                IgnoreTitanCollisions(partCol);

                Rigidbody rb = part.GetComponent<Rigidbody>();
                if (rb == null) rb = part.AddComponent<Rigidbody>();
                rb.mass = Mathf.Max(3f, debrisMass * 0.45f);
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.linearDamping = 0.32f;
                rb.angularDamping = 0.48f;

                Vector3 spread = (part.transform.position - transform.position);
                spread.y = 0f;
                Vector3 launchDir = (dir + spread.normalized * 0.45f + Random.insideUnitSphere * 0.28f).normalized;
                Vector3 vel = launchDir * (scatterImpulse * Random.Range(0.75f, 1.25f) * forceScale)
                            + Vector3.up * Random.Range(upwardPop * 0.4f, upwardPop * 1.2f);
                rb.AddForce(vel, ForceMode.VelocityChange);
                rb.AddTorque(Random.onUnitSphere * tumbleTorque, ForceMode.VelocityChange);
            }
        }

        private static void IgnoreTitanCollisions(Collider debrisCol)
        {
            if (debrisCol == null) return;

            var jaegers = FindObjectsByType<SimpleJaegerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < jaegers.Length; i++)
            {
                if (jaegers[i] == null) continue;
                Collider[] cols = jaegers[i].GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < cols.Length; c++)
                {
                    if (cols[c] != null && cols[c] != debrisCol)
                        Physics.IgnoreCollision(debrisCol, cols[c], true);
                }
            }

            var kaijus = FindObjectsByType<KaijuAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < kaijus.Length; i++)
            {
                if (kaijus[i] == null) continue;
                Collider[] cols = kaijus[i].GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < cols.Length; c++)
                {
                    if (cols[c] != null && cols[c] != debrisCol)
                        Physics.IgnoreCollision(debrisCol, cols[c], true);
                }
            }
        }
    }
}
