using UnityEngine;
using PacificRimWater;

namespace PacificRimEnvironment
{
    /// <summary>
    /// Buoyant floating debris (shipping containers, maritime navigation buoys, oceanic wreckage).
    /// Tracks dynamic Gerstner wave elevation and surface normal slope with hydrodynamic damping.
    /// Can be moved, pushed, and knocked around by both Gipsy Danger and Leatherback.
    /// </summary>
    [ExecuteAlways]
    public class FloatingDebris : MonoBehaviour
    {
        public enum DebrisType
        {
            ShippingContainer,
            MaritimeBuoy,
            IndustrialWreckage
        }

        public DebrisType debrisType = DebrisType.ShippingContainer;
        public Color tintColor = Color.white;

        [Header("Buoyancy Settings")]
        public float waterDamping = 4.5f;
        public float rotationDamping = 3.5f;
        public float bobbingAmplitude = 0.35f;
        public float massSubmergenceDepth = 0.5f;

        [Header("Drift Simulation")]
        public float driftSpeed = 0.65f;
        public Vector2 driftDirection = new Vector2(0.85f, 0.52f);

        [Header("Colossal Dynamic Push Reaction")]
        [Tooltip("How strongly the 200-ton Jaeger and Kaiju push/toss this debris upon contact.")]
        public float pushForceMultiplier = 16f;
        [Tooltip("Angular tumble torque added when hit by a Titan.")]
        public float pushTorque = 12f;
        [Tooltip("Horizontal water resistance to slow pushed debris back down.")]
        public float horizontalDrag = 1.4f;

        [Header("Buoy Warning Light")]
        public Light buoyLight;
        public Color buoyLightColor = new Color(1.0f, 0.65f, 0.15f, 1f);
        public float buoyBlinkRate = 1.2f;

        private Vector3 currentVelocity;
        private Vector3 smoothedNormal = Vector3.up;
        private float wavePhaseOffset;

        // Dynamic horizontal velocity from Titan impacts
        private Vector2 horizontalVelocity;
        private float angularTumbleVelocity;

        void Start()
        {
            wavePhaseOffset = Random.Range(0f, 100f);
            BuildPlaceholderIfMissing();
            EnsureTriggerColliders();
        }

        public void BuildDebris()
        {
            BuildPlaceholderIfMissing();
            EnsureTriggerColliders();
        }

        public void EnsureTriggerColliders()
        {
            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                col.isTrigger = true;
            }
        }

        public void BuildPlaceholderIfMissing()
        {
            if (transform.childCount > 0) return;

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                name = $"Debris_{debrisType}_Mat"
            };

            switch (debrisType)
            {
                case DebrisType.ShippingContainer:
                    mat.color = tintColor != Color.white ? tintColor : (Random.value > 0.5f ? new Color(0.72f, 0.22f, 0.18f, 1f) : new Color(0.18f, 0.35f, 0.55f, 1f));
                    mat.SetFloat("_Metallic", 0.75f);
                    mat.SetFloat("_Smoothness", 0.35f);

                    GameObject container = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    container.name = "ContainerHull";
                    container.transform.SetParent(transform, false);
                    container.transform.localScale = new Vector3(6.0f, 2.6f, 12.2f);
                    container.GetComponent<Renderer>().sharedMaterial = mat;
                    break;

                case DebrisType.MaritimeBuoy:
                    mat.color = new Color(0.85f, 0.45f, 0.12f, 1f);
                    mat.SetFloat("_Metallic", 0.45f);
                    mat.SetFloat("_Smoothness", 0.65f);

                    GameObject baseCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    baseCyl.name = "BuoyBase";
                    baseCyl.transform.SetParent(transform, false);
                    baseCyl.transform.localScale = new Vector3(3.5f, 1.8f, 3.5f);
                    baseCyl.GetComponent<Renderer>().sharedMaterial = mat;

                    GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tower.name = "BeaconMast";
                    tower.transform.SetParent(transform, false);
                    tower.transform.localPosition = new Vector3(0f, 2.5f, 0f);
                    tower.transform.localScale = new Vector3(0.6f, 3.5f, 0.6f);
                    tower.GetComponent<Renderer>().sharedMaterial = mat;

                    GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    lamp.name = "BeaconLamp";
                    lamp.transform.SetParent(transform, false);
                    lamp.transform.localPosition = new Vector3(0f, 4.4f, 0f);
                    lamp.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);

                    Material lampMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    lampMat.color = buoyLightColor;
                    lampMat.EnableKeyword("_EMISSION");
                    lampMat.SetColor("_EmissionColor", buoyLightColor * 4.0f);
                    lamp.GetComponent<Renderer>().sharedMaterial = lampMat;

                    buoyLight = lamp.AddComponent<Light>();
                    buoyLight.type = LightType.Point;
                    buoyLight.color = buoyLightColor;
                    buoyLight.range = 25f;
                    buoyLight.intensity = 3.5f;
                    break;

                case DebrisType.IndustrialWreckage:
                    mat.color = new Color(0.22f, 0.25f, 0.28f, 1f);
                    mat.SetFloat("_Metallic", 0.85f);
                    mat.SetFloat("_Smoothness", 0.45f);

                    GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    plate.name = "WreckagePlate";
                    plate.transform.SetParent(transform, false);
                    plate.transform.localScale = new Vector3(8.5f, 0.8f, 9.5f);
                    plate.transform.localRotation = Quaternion.Euler(12f, 25f, -8f);
                    plate.GetComponent<Renderer>().sharedMaterial = mat;
                    break;
            }
        }

        void Update()
        {
            Vector3 pos = transform.position;
            float waterHeight = 0f;
            Vector3 waterNormal = Vector3.up;

            if (WaterSystem.Instance != null)
            {
                waterHeight = WaterSystem.Instance.GetWaterHeight(pos);
                waterNormal = WaterSystem.Instance.GetWaterNormal(pos);
            }
            else
            {
                waterHeight = Mathf.Sin((pos.x + pos.z) * 0.15f + Time.time * 1.5f + wavePhaseOffset) * bobbingAmplitude;
            }

            if (Application.isPlaying)
            {
                // Drift + dynamic push velocity
                pos.x += (driftDirection.x * driftSpeed + horizontalVelocity.x) * Time.deltaTime;
                pos.z += (driftDirection.y * driftSpeed + horizontalVelocity.y) * Time.deltaTime;

                // Dampen push velocity back to neutral ocean drift
                horizontalVelocity = Vector2.MoveTowards(horizontalVelocity, Vector2.zero, horizontalDrag * Time.deltaTime);
                angularTumbleVelocity = Mathf.MoveTowards(angularTumbleVelocity, 0f, 45f * Time.deltaTime);

                // Proximity push check for Jaeger & Kaiju (covers cases where character controllers or triggers sweep near)
                ApplyProximityDisplacement(ref pos);
            }

            float targetY = waterHeight - massSubmergenceDepth;
            pos.y = Mathf.SmoothDamp(pos.y, targetY, ref currentVelocity.y, 1f / waterDamping);
            transform.position = pos;

            smoothedNormal = Vector3.Slerp(smoothedNormal, waterNormal, Time.deltaTime * rotationDamping);
            Quaternion waveRot = Quaternion.FromToRotation(Vector3.up, smoothedNormal);

            if (Mathf.Abs(angularTumbleVelocity) > 0.01f)
            {
                transform.Rotate(Vector3.up, angularTumbleVelocity * Time.deltaTime, Space.World);
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, waveRot, Time.deltaTime * rotationDamping);

            if (buoyLight != null)
            {
                float blink = Mathf.Sin(Time.time * Mathf.PI * 2f * buoyBlinkRate) > 0.15f ? 1f : 0.05f;
                buoyLight.intensity = blink * 4.0f;
            }
        }

        private void ApplyProximityDisplacement(ref Vector3 pos)
        {
            // Check Gipsy Danger & Kaiju
            var jaeger = SimpleJaegerController.Instance != null ? SimpleJaegerController.Instance : FindAnyObjectByType<SimpleJaegerController>();
            if (jaeger != null)
            {
                CheckAndPushFromTitan(jaeger.transform.position, 7.5f, 18f, ref pos);
            }

            var kaiju = KaijuAI.Instance != null ? KaijuAI.Instance : FindAnyObjectByType<KaijuAI>();
            if (kaiju != null)
            {
                CheckAndPushFromTitan(kaiju.transform.position, 9.0f, 20f, ref pos);
            }
        }

        private void CheckAndPushFromTitan(Vector3 titanPos, float titanRadius, float pushImpulse, ref Vector3 pos)
        {
            Vector3 diff = pos - titanPos;
            diff.y = 0f;
            float dist = diff.magnitude;
            float combinedRadius = titanRadius + 4.5f; // debris approximate footprint

            if (dist < combinedRadius && dist > 0.01f)
            {
                Vector3 pushDir = diff / dist;
                float penetration = combinedRadius - dist;

                // Push position out immediately so the colossal titan never gets stopped
                pos += pushDir * (penetration * 0.85f);

                // Add sustained outward glide velocity & rotation tumble
                horizontalVelocity += new Vector2(pushDir.x, pushDir.z) * (pushImpulse * Time.deltaTime * 60f);
                if (horizontalVelocity.magnitude > 25f)
                {
                    horizontalVelocity = horizontalVelocity.normalized * 25f;
                }

                angularTumbleVelocity += Random.Range(-pushTorque, pushTorque);

                // Ocean splash kickup on push
                if (WaterSplashManager.Instance != null && penetration > 0.4f)
                {
                    WaterSplashManager.Instance.SpawnFootstepSplash(pos, 1.2f);
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlayDebrisImpact(pos, 0.7f);
                }
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!Application.isPlaying) return;

            // When a Jaeger/Kaiju collider penetrates the debris trigger
            var titan = other.GetComponentInParent<SimpleJaegerController>() != null ||
                        other.GetComponentInParent<KaijuAI>() != null;

            if (titan)
            {
                Vector3 toDebris = transform.position - other.transform.position;
                toDebris.y = 0f;
                Vector3 dir = toDebris.sqrMagnitude > 0.01f ? toDebris.normalized : transform.forward;

                horizontalVelocity += new Vector2(dir.x, dir.z) * pushForceMultiplier;
                if (horizontalVelocity.magnitude > 28f)
                {
                    horizontalVelocity = horizontalVelocity.normalized * 28f;
                }
                angularTumbleVelocity = Random.Range(-40f, 40f);
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayDebrisImpact(transform.position, 0.85f);
            }
        }
    }
}
