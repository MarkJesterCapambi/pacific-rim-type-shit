using System.Collections.Generic;
using UnityEngine;
using PacificRimAudio;

namespace PacificRimWater
{
    /// <summary>
    /// Adds weighty physics destruction to Harbor-level shipping containers and gantry cranes
    /// without per-frame CPU overhead when intact. Stays kinematic/trigger until struck by
    /// a Jaeger, Kaiju, sliding cutscene impact, shockwave, or collapsing neighbor, then decouples
    /// into heavy rigidbodies with steel impacts, sparks, and cascading collapse.
    /// </summary>
    public class HarborDestructibleProp : MonoBehaviour
    {
        public enum PropKind
        {
            ShippingContainer,
            HarborCrane
        }

        [Header("Destruction Weight & Forces")]
        public PropKind kind = PropKind.ShippingContainer;
        public float propMass = 38f;
        public float lateralImpulse = 15f;
        public float upwardPop = 3.8f;
        public float tumbleTorque = 2.6f;

        private bool isBroken;
        private float nextImpactFxTime;

        private static readonly HashSet<HarborDestructibleProp> activeProps = new HashSet<HarborDestructibleProp>();

        private void OnEnable()
        {
            activeProps.Add(this);
        }

        private void OnDisable()
        {
            activeProps.Remove(this);
        }

        /// <summary>
        /// Triggers weighty physics collapse on all Harbor destructibles inside a radius (used by
        /// heavy Titan slams, blasts, and Shot 4 kneeling drift collisions).
        /// </summary>
        public static void TriggerImpactInRadius(Vector3 impactCenter, float radius, Vector3 smashVelocity, float impulseMultiplier = 1f)
        {
            float sqRadius = radius * radius;
            var snapshot = new List<HarborDestructibleProp>(activeProps);
            for (int i = 0; i < snapshot.Count; i++)
            {
                HarborDestructibleProp prop = snapshot[i];
                if (prop == null) continue;

                Vector3 delta = prop.transform.position - impactCenter;
                delta.y *= 0.45f;
                if (delta.sqrMagnitude <= sqRadius)
                {
                    prop.BreakFromImpact(impactCenter, smashVelocity, impulseMultiplier);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            HandleColliderHit(other, other.ClosestPoint(transform.position));
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null || collision.collider == null) return;

            if (isBroken)
            {
                if (collision.relativeVelocity.magnitude > 4.5f && Time.time >= nextImpactFxTime)
                {
                    nextImpactFxTime = Time.time + 0.45f;
                    Vector3 pt = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                    EmitMetallicHitFx(pt, 0.55f);
                }
                return;
            }

            Vector3 contactPt = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            HandleColliderHit(collision.collider, contactPt);
        }

        private void HandleColliderHit(Collider other, Vector3 contactPoint)
        {
            var jaeger = other.GetComponentInParent<SimpleJaegerController>();
            var kaiju = other.GetComponentInParent<KaijuAI>();
            var otherProp = other.GetComponentInParent<HarborDestructibleProp>();

            Vector3 origin = contactPoint;
            Vector3 velocity = Vector3.zero;
            float forceScale = 1f;

            if (jaeger != null)
            {
                origin = jaeger.transform.position;
                var jrb = jaeger.GetComponent<Rigidbody>();
                if (jrb != null) velocity = jrb.linearVelocity;
                forceScale = 1.15f;
            }
            else if (kaiju != null)
            {
                origin = kaiju.transform.position;
                var krb = kaiju.GetComponent<Rigidbody>();
                if (krb != null) velocity = krb.linearVelocity;
                forceScale = 1.3f;
            }
            else if (otherProp != null && otherProp != this && otherProp.isBroken)
            {
                origin = other.transform.position;
                var prb = other.attachedRigidbody;
                if (prb == null || prb.linearVelocity.magnitude < 3.2f) return;
                velocity = prb.linearVelocity * 0.65f;
                forceScale = 0.72f;
            }
            else
            {
                return;
            }

            BreakFromImpact(origin, velocity, forceScale);
        }

        public void BreakFromImpact(Vector3 impactOrigin, Vector3 impactVelocity, float impulseMultiplier = 1f)
        {
            if (isBroken)
            {
                ApplyAdditionalShove(impactOrigin, impactVelocity, impulseMultiplier);
                return;
            }

            isBroken = true;

            Vector3 away = transform.position - impactOrigin;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            away.Normalize();

            Vector3 velDir = new Vector3(impactVelocity.x, 0f, impactVelocity.z);
            Vector3 pushDir = velDir.sqrMagnitude > 4f
                ? Vector3.Slerp(away, velDir.normalized, 0.55f).normalized
                : away;

            if (kind == PropKind.HarborCrane)
            {
                CollapseCraneHierarchy(pushDir, impactOrigin, impulseMultiplier);
            }
            else
            {
                CollapseSingleContainer(pushDir, impactOrigin, impulseMultiplier);
            }
        }

        private void ApplyAdditionalShove(Vector3 impactOrigin, Vector3 impactVelocity, float impulseMultiplier)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null || rb.isKinematic) return;

            Vector3 away = transform.position - impactOrigin;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            Vector3 dir = (away.normalized + new Vector3(impactVelocity.x, 0f, impactVelocity.z).normalized * 0.5f).normalized;

            rb.AddForce(dir * (lateralImpulse * 0.65f * impulseMultiplier) + Vector3.up * (upwardPop * 0.45f), ForceMode.VelocityChange);
            rb.AddTorque(Random.onUnitSphere * (tumbleTorque * 0.6f), ForceMode.VelocityChange);
        }

        private void CollapseSingleContainer(Vector3 pushDir, Vector3 impactOrigin, float impulseMultiplier)
        {
            BoxCollider col = GetComponent<BoxCollider>();
            if (col == null) col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = false;
            IgnoreTitanCollisions(col);

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(15f, propMass);
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.linearDamping = 0.55f;
            rb.angularDamping = 0.72f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            float rand = Random.Range(0.82f, 1.22f);
            Vector3 vel = pushDir * (lateralImpulse * impulseMultiplier * rand) + Vector3.up * (upwardPop * Mathf.Clamp(impulseMultiplier, 0.6f, 1.5f));
            rb.AddForce(vel, ForceMode.VelocityChange);
            rb.AddTorque(Random.onUnitSphere * (tumbleTorque * rand), ForceMode.VelocityChange);

            EmitMetallicHitFx(transform.position, 0.9f);

            // Wake up any containers stacked above so vertical stacks collapse naturally under weight
            Collider[] above = Physics.OverlapBox(
                transform.position + Vector3.up * 3.2f,
                new Vector3(2.8f, 3.0f, 6.4f),
                transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < above.Length; i++)
            {
                if (above[i] == null || above[i].gameObject == gameObject) continue;
                var neighbor = above[i].GetComponentInParent<HarborDestructibleProp>();
                if (neighbor != null && neighbor != this && !neighbor.isBroken)
                {
                    neighbor.BreakFromImpact(impactOrigin, pushDir * 5f, 0.55f);
                }
            }
        }

        private void CollapseCraneHierarchy(Vector3 pushDir, Vector3 impactOrigin, float impulseMultiplier)
        {
            Collider rootCol = GetComponent<Collider>();
            if (rootCol != null) rootCol.enabled = false;

            EmitMetallicHitFx(transform.position + Vector3.up * 8f, 1.35f);
            var cam = Camera.main;
            cam?.GetComponent<PacificRimCameraShake>()?.Shake(0.62f, 0.35f);

            Transform[] children = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
                children[i] = transform.GetChild(i);

            Vector3 toppleAxis = Vector3.Cross(Vector3.up, pushDir).normalized;

            for (int i = 0; i < children.Length; i++)
            {
                Transform part = children[i];
                if (part == null) continue;

                Collider partCol = part.GetComponent<Collider>();
                if (partCol == null) partCol = part.gameObject.AddComponent<BoxCollider>();
                partCol.isTrigger = false;
                partCol.enabled = true;
                IgnoreTitanCollisions(partCol);

                Rigidbody rb = part.GetComponent<Rigidbody>();
                if (rb == null) rb = part.gameObject.AddComponent<Rigidbody>();

                bool isBoom = part.name.Contains("Boom") || part.name.Contains("Cab");
                rb.mass = isBoom ? 115f : 60f;
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.linearDamping = 0.45f;
                rb.angularDamping = 0.62f;

                Vector3 jitter = part.position - transform.position;
                jitter.y = 0f;
                Vector3 partDir = (pushDir + jitter.normalized * 0.35f).normalized;
                float speedScale = isBoom ? 0.55f : 0.85f;
                rb.AddForce(
                    partDir * (lateralImpulse * speedScale * impulseMultiplier) + Vector3.up * Random.Range(1.2f, 4.2f),
                    ForceMode.VelocityChange);
                rb.AddTorque(
                    (toppleAxis * 2.2f + Random.onUnitSphere * 1.1f),
                    ForceMode.VelocityChange);
            }

            // Crush stacked containers directly beneath falling crane gantry
            TriggerImpactInRadius(transform.position, 14f, pushDir * 10f, 0.75f);
        }

        /// <summary>
        /// Disables physical collisions between broken debris pieces and both Gipsy Danger and
        /// Kaiju colliders while keeping debris grounded against the pier deck and other wreckage.
        /// </summary>
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

        private void EmitMetallicHitFx(Vector3 worldPos, float volume)
        {
            var audio = PacificRimAudioManager.Instance;
            if (audio != null)
            {
                audio.PlayDebrisImpact(worldPos, volume);
                audio.PlayAtPosition("Metal Impact 7", worldPos, Mathf.Clamp01(volume * 0.9f));
                if (kind == PropKind.HarborCrane)
                    audio.PlayArmorCreak(worldPos, 1.0f);
            }

            if (WaterSplashManager.Instance != null)
            {
                WaterSplashManager.Instance.SpawnCombatSparks(worldPos, Vector3.up, true, true, false);
                WaterSplashManager.Instance.EmitVolumetricMist(worldPos, Vector3.up * 3.5f, kind == PropKind.HarborCrane ? 10f : 5.5f, 0.35f, 1.4f);
            }
        }
    }
}
