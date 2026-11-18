using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Interactive Jaeger-scale micro-collateral for urban environments:
    /// - KnockableVehicles (Hong Kong double-decker trams, fuel tankers, sedans): remain lightweight kinematic
    ///   props until bumped/punched/stomped by a Titan, then launch across the wet asphalt with sparks & crunch SFX.
    /// - ShatterOnTitanPass (low concrete overpass spans, bamboo scaffolding frames): keep trigger colliders so
    ///   Titans never snag mid-stride, breaking dynamically into scattered rigidbody debris on contact.
    /// </summary>
    public class UrbanCollateralProp : MonoBehaviour
    {
        public enum CollateralMode
        {
            KnockableVehicle,
            ShatterOnTitanPass
        }

        [Header("Collateral Behavior")]
        public CollateralMode mode = CollateralMode.KnockableVehicle;
        public bool isFuelTanker = false;
        public float launchMass = 18f;
        public float knockImpulse = 34f;
        public float upwardPop = 11f;
        public float spinTorque = 9f;

        private bool hasTriggered;
        private float nextBumpSfxTime;

        private void OnTriggerEnter(Collider other)
        {
            HandleTitanContact(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision != null && collision.collider != null)
            {
                HandleTitanContact(collision.collider);
            }
        }

        public void TriggerImpact(Vector3 impactOrigin)
        {
            HandleTitanOrigin(impactOrigin, transform.forward);
        }

        private void HandleTitanContact(Collider other)
        {
            if (other == null) return;

            var jaeger = other.GetComponentInParent<SimpleJaegerController>();
            var kaiju = other.GetComponentInParent<KaijuAI>();
            if (jaeger == null && kaiju == null) return;

            Transform titanRoot = jaeger != null ? jaeger.transform : kaiju.transform;
            HandleTitanOrigin(titanRoot.position, titanRoot.forward);
        }

        private void HandleTitanOrigin(Vector3 originPos, Vector3 fallbackForward)
        {
            Vector3 away = transform.position - originPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = fallbackForward;
            away.Normalize();

            if (mode == CollateralMode.ShatterOnTitanPass)
            {
                if (hasTriggered) return;
                hasTriggered = true;
                ShatterHierarchy(away);
                return;
            }

            ActivateVehiclePhysics(away);
        }

        private void ActivateVehiclePhysics(Vector3 pushDir)
        {
            if (!hasTriggered)
            {
                hasTriggered = true;

                Collider col = GetComponent<Collider>();
                if (col != null)
                {
                    col.isTrigger = false;
                }

                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
                rb.mass = Mathf.Max(4f, launchMass);
                rb.useGravity = true;
                rb.isKinematic = false;
#if UNITY_6000_0_OR_NEWER
                rb.linearDamping = 0.25f;
                rb.angularDamping = 0.35f;
#else
                rb.drag = 0.25f;
                rb.angularDrag = 0.35f;
#endif
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                Vector3 impulse = (pushDir * knockImpulse + Vector3.up * upwardPop) * Random.Range(0.85f, 1.25f);
                body.AddForce(impulse, ForceMode.VelocityChange);
                body.AddTorque(Random.onUnitSphere * spinTorque, ForceMode.VelocityChange);
            }

            if (Time.time >= nextBumpSfxTime)
            {
                nextBumpSfxTime = Time.time + 0.45f;
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayDebrisImpact(transform.position, isFuelTanker ? 1.15f : 0.78f);

                if (WaterSplashManager.Instance != null)
                {
                    WaterSplashManager.Instance.SpawnHeavyMoveWaterKickup(
                        transform.position,
                        pushDir,
                        isFuelTanker ? 1.85f : 0.85f,
                        isFuelTanker);
                }
            }
        }

        private void ShatterHierarchy(Vector3 pushDir)
        {
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayDebrisImpact(transform.position, 1.0f);
            if (WaterSplashManager.Instance != null)
            {
                WaterSplashManager.Instance.SpawnFootstepSplash(transform.position, 1.15f);
            }

            Collider rootCol = GetComponent<Collider>();
            if (rootCol != null) rootCol.enabled = false;

            MeshRenderer[] parts = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                GameObject part = parts[i].gameObject;
                Collider partCol = part.GetComponent<Collider>();
                if (partCol == null)
                {
                    partCol = part.AddComponent<BoxCollider>();
                }
                partCol.isTrigger = false;
                partCol.enabled = true;

                Rigidbody rb = part.GetComponent<Rigidbody>();
                if (rb == null) rb = part.AddComponent<Rigidbody>();
                rb.mass = Mathf.Max(2f, launchMass * 0.35f);
                rb.useGravity = true;
                rb.isKinematic = false;

                Vector3 jitter = (part.transform.position - transform.position);
                jitter.y = 0f;
                Vector3 dir = (pushDir + jitter.normalized * 0.45f + Random.insideUnitSphere * 0.2f).normalized;
                rb.AddForce(dir * knockImpulse * 0.55f + Vector3.up * Random.Range(3f, 8f), ForceMode.VelocityChange);
                rb.AddTorque(Random.onUnitSphere * spinTorque * 0.65f, ForceMode.VelocityChange);
            }
        }
    }
}
