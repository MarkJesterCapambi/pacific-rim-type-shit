using UnityEngine;
using UnityEngine.Events;

namespace PacificRimWater
{
    /// <summary>
    /// Pacific Rim Heavy Ocean Wading Interaction.
    /// Simulates colossal footstep shockwaves, ocean parting bow wakes, water eruptions,
    /// and viscous deep-water resistance for giant mechs or characters wading through the ocean.
    /// </summary>
    public class JaegerWaterInteraction : MonoBehaviour
    {
        [Header("Footstep Shockwave Settings")]
        [Tooltip("Radius of the tidal shockwave expanding from each footstep (meters).")]
        public float footstepWaveRadius = 6.0f;
        [Tooltip("Wave height impulse of each footstep.")]
        public float footstepWaveStrength = 1.2f;
        [Tooltip("Distance in meters between consecutive footsteps.")]
        public float strideDistance = 3.5f;
        [Tooltip("Lateral spacing between left and right foot (meters).")]
        public float footSpacing = 1.8f;
        [Tooltip("Physical width of the wading leg for bow wave calculations.")]
        public float legWidth = 2.2f;

        [Header("Water Eruptions & Splashes")]
        [Tooltip("Spawn vertical water eruptions and spray plumes on footsteps.")]
        public bool spawnSplashes = true;
        public float splashScale = 1.4f;

        [Header("Impact Camera Shake")]
        [Tooltip("Intensity of screen shake on heavy foot impact.")]
        public float footstepShakeIntensity = 0.35f;
        public float footstepShakeDuration = 0.25f;

        [Header("Ocean Wading Physics")]
        [Tooltip("Viscous hydrodynamic drag resisting horizontal movement through ocean water.")]
        [Range(0.1f, 10f)]
        public float oceanWadingDrag = 3.5f;
        [Tooltip("Speed penalty applied when wading through deep ocean water.")]
        [Range(0.2f, 1.0f)]
        public float deepWaterSpeedMultiplier = 0.55f;
        [Tooltip("Upward buoyant support when submerged deeply.")]
        public float oceanBuoyancy = 8f;

        [Header("Custom Foot Transforms (Optional)")]
        [Tooltip("If unassigned, footsteps are automatically generated based on stride distance.")]
        public Transform leftFootTransform;
        public Transform rightFootTransform;

        [Header("Events")]
        public UnityEvent<Vector3> OnFootstepWaterImpact;
        public UnityEvent OnEnterOcean;
        public UnityEvent OnExitOcean;

        // Internal State
        private Rigidbody rb;
        private WaterSystem water;
        private Vector3 lastStepPos;
        private Vector3 prevPos;
        private bool isLeftFoot = true;
        private bool wasInWater;
        private float initialDrag;
        private float nextWakeTime;

        public bool IsWading { get; private set; }
        public float SubmergedDepth { get; private set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                initialDrag = rb.linearDamping;
#else
                initialDrag = rb.drag;
#endif
            }
        }

        void Start()
        {
            water = WaterSystem.Instance;
            lastStepPos = transform.position;
            prevPos = transform.position;
        }

        void Update()
        {
            if (water == null)
            {
                water = WaterSystem.Instance;
                if (water == null) return;
            }

            Vector3 currentPos = transform.position;
            float oceanSurfaceY = water.GetWaterHeight(currentPos);
            SubmergedDepth = oceanSurfaceY - currentPos.y;

            bool inWater = SubmergedDepth > 0.1f;

            if (inWater && !wasInWater)
            {
                OnEnterOcean?.Invoke();
                TriggerFootstepImpact(currentPos, footstepWaveRadius * 1.3f, footstepWaveStrength * 1.5f);
            }
            else if (!inWater && wasInWater)
            {
                OnExitOcean?.Invoke();
            }

            IsWading = inWater;

            Vector3 vel = GetMovementVelocity();
            float horizontalSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;

            if (inWater)
            {
                // Footstep shockwave generation
                float distMoved = Vector3.Distance(new Vector3(currentPos.x, 0f, currentPos.z), new Vector3(lastStepPos.x, 0f, lastStepPos.z));
                if (distMoved >= strideDistance && horizontalSpeed > 0.5f)
                {
                    lastStepPos = currentPos;
                    isLeftFoot = !isLeftFoot;

                    Vector3 footContact;
                    if (isLeftFoot && leftFootTransform != null)
                    {
                        footContact = leftFootTransform.position;
                    }
                    else if (!isLeftFoot && rightFootTransform != null)
                    {
                        footContact = rightFootTransform.position;
                    }
                    else
                    {
                        Vector3 footOffset = (isLeftFoot ? -transform.right : transform.right) * footSpacing;
                        footContact = currentPos + footOffset;
                    }

                    footContact.y = water.GetWaterHeight(footContact);

                    float dynamicStrength = footstepWaveStrength * Mathf.Clamp(horizontalSpeed / 3.5f, 0.7f, 2.0f);
                    TriggerFootstepImpact(footContact, footstepWaveRadius, dynamicStrength);
                }

                // Continuous leg bow wake plowing through the sea
                if (horizontalSpeed > 1.0f && Time.time >= nextWakeTime)
                {
                    nextWakeTime = Time.time + 0.12f;
                    Vector3 legCenter = new Vector3(currentPos.x, oceanSurfaceY, currentPos.z);
                    water.AddWadingWake(legCenter, vel, legWidth, 0.45f);
                }
            }

            wasInWater = inWater;
            prevPos = currentPos;
        }

        void FixedUpdate()
        {
            if (!IsWading || rb == null)
            {
                if (rb != null)
                {
#if UNITY_6000_0_OR_NEWER
                    rb.linearDamping = initialDrag;
#else
                    rb.drag = initialDrag;
#endif
                }
                return;
            }

            // Viscous ocean wading drag
            float submersionFactor = Mathf.Clamp01(SubmergedDepth / 6.0f);

#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = Mathf.Lerp(initialDrag, oceanWadingDrag, submersionFactor);
#else
            rb.drag = Mathf.Lerp(initialDrag, oceanWadingDrag, submersionFactor);
#endif

            // Deep water buoyant assist
            if (SubmergedDepth > 3.0f)
            {
                float floatPower = Mathf.Clamp01((SubmergedDepth - 3.0f) / 4.0f);
                rb.AddForce(Vector3.up * (oceanBuoyancy * floatPower), ForceMode.Acceleration);
            }
        }

        /// <summary>
        /// Triggers a colossal footstep impact shockwave, spray plume, and foam ring.
        /// </summary>
        public void TriggerFootstepImpact(Vector3 contactPoint, float radius, float strength)
        {
            if (water != null)
            {
                water.AddFootstepWave(contactPoint, radius, strength);
            }

            if (spawnSplashes)
            {
                if (WaterSplashManager.Instance != null)
                {
                    WaterSplashManager.Instance.SpawnFootstepSplash(contactPoint, splashScale);
                }
                ProceduralSplash.CreateFoamRing(contactPoint, radius * 1.1f, 1.6f);
            }

            // Footstep camera thump
            if (footstepShakeIntensity > 0f)
            {
                DoCameraShake(footstepShakeIntensity, footstepShakeDuration);
            }

            OnFootstepWaterImpact?.Invoke(contactPoint);
        }

        private void DoCameraShake(float intensity, float duration)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                var shaker = cam.GetComponent<PacificRimCameraShake>();
                if (shaker == null)
                {
                    shaker = cam.gameObject.AddComponent<PacificRimCameraShake>();
                }
                shaker.Shake(intensity, duration);
            }
        }

        private Vector3 GetMovementVelocity()
        {
            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                return rb.linearVelocity;
#else
                return rb.velocity;
#endif
            }

            float dt = Time.deltaTime;
            return dt > 0f ? (transform.position - prevPos) / dt : Vector3.zero;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 l = transform.position - transform.right * footSpacing;
            Vector3 r = transform.position + transform.right * footSpacing;
            Gizmos.DrawWireSphere(l, 0.4f);
            Gizmos.DrawWireSphere(r, 0.4f);
        }
    }

    /// <summary>
    /// Screen rumble effect when a Jaeger stomps in the ocean.
    /// </summary>
    public class PacificRimCameraShake : MonoBehaviour
    {
        private float shakeTime;
        private float shakeMagnitude;
        private Vector3 initialLocalPos;

        void Awake()
        {
            initialLocalPos = transform.localPosition;
        }

        public void Shake(float magnitude, float duration)
        {
            shakeMagnitude = magnitude;
            shakeTime = duration;
        }

        void LateUpdate()
        {
            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                Vector3 randOffset = Random.insideUnitSphere * shakeMagnitude;
                transform.localPosition = initialLocalPos + randOffset;

                if (shakeTime <= 0f)
                {
                    transform.localPosition = initialLocalPos;
                }
            }
        }
    }
}
