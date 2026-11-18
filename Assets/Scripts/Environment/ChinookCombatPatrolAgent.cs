using UnityEngine;
using PacificRimWater;

namespace PacificRimEnvironment
{
    public enum ChinookTargetType
    {
        Gipsy,
        Leatherback
    }

    /// <summary>
    /// Controls an individual CH-47 Chinook in the overhead combat escort squadron:
    /// - Tracks either Gipsy Danger or Leatherback Kaiju in dynamic swirling orbit
    /// - Smoothly breaks orbit and flies across the combat arena when rotating targets every 5 seconds
    /// - Aerodynamic banking into turns, forward flight pitch, and atmospheric turbulence bobbing
    /// - Tandem counter-rotating rotors and flashing aviation beacons
    /// - High-intensity searchlight with volumetric beam cone haphazardly scanning the battleground
    /// - Dynamically flares intensity, range, and cone angle during lightning strikes to illuminate surroundings
    /// </summary>
    [ExecuteAlways]
    public class ChinookCombatPatrolAgent : MonoBehaviour
    {
        [Header("Target Tracking")]
        public Transform currentTarget;
        public ChinookTargetType targetType = ChinookTargetType.Gipsy;
        public bool isTransitioning { get; private set; }

        [Header("Orbit Dynamics")]
        public float orbitRadius = 36f;
        public float flightAltitude = 44f;
        public float orbitSpeed = 0.42f;
        public float orbitDirection = 1f; // 1 = clockwise, -1 = counter-clockwise
        public float altitudeBobSpeed = 1.4f;
        public float altitudeBobAmount = 3.2f;
        public float bankingSensitivity = 22f;
        public float forwardPitchAngle = 6.5f;

        [Header("Searchlight & Volumetric Light Beam")]
        public Transform searchlightGimbal;
        public Light searchlight;
        public Transform volumetricBeam;
        public float baseLightIntensity = 950f;
        public float lightningPeakIntensity = 2800f;
        public float baseLightRange = 110f;
        public float lightningPeakRange = 165f;
        public float baseSpotAngle = 46f;
        public float lightningSpotAngle = 58f;
        public float sweepSpeedX = 0.85f;
        public float sweepSpeedY = 1.15f;
        public float sweepAngleX = 32f;
        public float sweepAngleY = 22f;

        [Header("Audio")]
        public AudioSource rotorAudioSource;

        [Header("Urban Canyon Avoidance")]
        public bool avoidUrbanBuildings = false;
        public float minUrbanClearanceY = 68f;
        public float urbanAvoidRadius = 18f;
        public Vector2 urbanBoundsMinXZ = new Vector2(-232f, -232f);
        public Vector2 urbanBoundsMaxXZ = new Vector2(232f, 232f);

        private float currentOrbitAngle;
        private float bobPhaseOffset;
        private float noiseSeed;
        private Vector3 currentVelocity;
        private Vector3 lastPosition;
        private Quaternion lastRotation;

        // Target Transition State
        private Transform transitionTarget;
        private ChinookTargetType transitionTargetType;
        private float transitionProgress;
        private float transitionDuration = 4.2f;
        private Vector3 transitionStartPos;
        private float targetOrbitRadius;
        private float targetFlightAltitude;

        private static readonly int IdGlobalLightningFlash = Shader.PropertyToID("_GlobalLightningFlash");

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            bobPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
            noiseSeed = Random.Range(10f, 90f);
            lastPosition = transform.position;
            lastRotation = transform.rotation;
        }

        public void Initialize(Transform target, ChinookTargetType type, float radius, float altitude, float speed, float initialAngle, float direction)
        {
            currentTarget = target;
            targetType = type;
            orbitRadius = radius;
            flightAltitude = altitude;
            orbitSpeed = speed;
            currentOrbitAngle = initialAngle;
            orbitDirection = direction;
            bobPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
            noiseSeed = Random.Range(10f, 90f);
            isTransitioning = false;

            ResolveReferences();

            // Set initial position
            if (currentTarget != null)
            {
                Vector3 center = currentTarget.position;
                Vector3 offset = new Vector3(Mathf.Cos(currentOrbitAngle), 0f, Mathf.Sin(currentOrbitAngle)) * orbitRadius;
                transform.position = new Vector3(center.x + offset.x, flightAltitude, center.z + offset.z);
            }
            lastPosition = transform.position;
        }

        public void ResolveReferences()
        {
            if (searchlightGimbal == null)
            {
                var sl = transform.Find("ChinookSearchlight");
                if (sl != null) searchlightGimbal = sl;
            }

            if (searchlightGimbal != null && searchlight == null)
            {
                searchlight = searchlightGimbal.GetComponent<Light>();
            }

            if (searchlightGimbal != null && volumetricBeam == null)
            {
                volumetricBeam = searchlightGimbal.Find("VolumetricBeam");
            }

            if (rotorAudioSource == null)
            {
                rotorAudioSource = GetComponent<AudioSource>();
            }
            if (Application.isPlaying && rotorAudioSource != null)
            {
                float sfxVol = PacificRimAudio.PacificRimAudioManager.Instance != null ? PacificRimAudio.PacificRimAudioManager.Instance.SfxVolume : 0.55f;
                rotorAudioSource.volume = 0.16f * sfxVol;
            }
        }

        public void BeginTransitionTo(Transform newTarget, ChinookTargetType newType, float newRadius, float newAltitude)
        {
            if (newTarget == null) return;
            isTransitioning = true;
            transitionTarget = newTarget;
            transitionTargetType = newType;
            targetOrbitRadius = newRadius;
            targetFlightAltitude = newAltitude;
            transitionStartPos = transform.position;
            transitionProgress = 0f;
            transitionDuration = Random.Range(3.8f, 4.8f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (isTransitioning)
            {
                UpdateTransitionFlight(dt);
            }
            else
            {
                UpdateOrbitFlight(dt);
            }

            UpdateSearchlightGimbal(dt);
            UpdateLightningReaction();
        }

        private void UpdateOrbitFlight(float dt)
        {
            if (currentTarget == null) return;

            currentOrbitAngle += orbitSpeed * orbitDirection * dt;
            if (currentOrbitAngle > Mathf.PI * 2f) currentOrbitAngle -= Mathf.PI * 2f;
            if (currentOrbitAngle < 0f) currentOrbitAngle += Mathf.PI * 2f;

            Vector3 center = currentTarget.position;
            float bob = Mathf.Sin(Time.time * altitudeBobSpeed + bobPhaseOffset) * altitudeBobAmount;
            float currentY = flightAltitude + bob;

            Vector3 desiredPos = new Vector3(
                center.x + Mathf.Cos(currentOrbitAngle) * orbitRadius,
                currentY,
                center.z + Mathf.Sin(currentOrbitAngle) * orbitRadius
            );
            desiredPos = ApplyUrbanAvoidance(desiredPos);

            // Smooth position update
            transform.position = Vector3.Lerp(transform.position, desiredPos, dt * 7.5f);

            // Compute actual horizontal velocity vector
            Vector3 horizVel = (transform.position - lastPosition);
            horizVel.y = 0f;
            lastPosition = transform.position;

            if (horizVel.sqrMagnitude > 0.0001f)
            {
                Vector3 forwardDir = horizVel.normalized;
                // Model faces -Z in source FBX, so yaw offset is 180 degrees
                Quaternion lookRot = Quaternion.LookRotation(forwardDir) * Quaternion.Euler(0f, 180f, 0f);

                // Realistic banking into turns proportional to angular rate
                float bankRoll = -orbitDirection * bankingSensitivity;
                // Pitch down slightly in forward flight
                float pitch = forwardPitchAngle;

                Quaternion targetRot = lookRot * Quaternion.Euler(pitch, 0f, bankRoll);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * 6.5f);
            }
        }

        private void UpdateTransitionFlight(float dt)
        {
            if (transitionTarget == null)
            {
                isTransitioning = false;
                return;
            }

            transitionProgress += dt / Mathf.Max(0.1f, transitionDuration);
            float t = Mathf.Clamp01(transitionProgress);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Target destination: merge smoothly into the other titan's orbit
            Vector3 targetCenter = transitionTarget.position;
            // Angle around destination
            float approachAngle = Mathf.Atan2(transform.position.z - targetCenter.z, transform.position.x - targetCenter.x);
            Vector3 targetEntryPos = new Vector3(
                targetCenter.x + Mathf.Cos(approachAngle) * targetOrbitRadius,
                targetFlightAltitude,
                targetCenter.z + Mathf.Sin(approachAngle) * targetOrbitRadius
            );

            // Flight curve: arc slightly outward and dip slightly in mid-flight for dynamic cinematic path
            Vector3 midPoint = (transitionStartPos + targetEntryPos) * 0.5f;
            Vector3 cross = Vector3.Cross(Vector3.up, (targetEntryPos - transitionStartPos).normalized);
            midPoint += cross * (18f * orbitDirection) + Vector3.up * 4.5f;

            // Quadratic bezier arc across the stormy arena
            Vector3 desiredPos = (1f - smoothT) * (1f - smoothT) * transitionStartPos
                               + 2f * (1f - smoothT) * smoothT * midPoint
                               + smoothT * smoothT * targetEntryPos;
            desiredPos = ApplyUrbanAvoidance(desiredPos);

            transform.position = Vector3.Lerp(transform.position, desiredPos, dt * 8.5f);

            Vector3 moveDelta = (transform.position - lastPosition);
            moveDelta.y = 0f;
            lastPosition = transform.position;

            if (moveDelta.sqrMagnitude > 0.0001f)
            {
                Vector3 fwd = moveDelta.normalized;
                Quaternion lookRot = Quaternion.LookRotation(fwd) * Quaternion.Euler(0f, 180f, 0f);
                float bank = -Mathf.Clamp(Vector3.Dot(cross, fwd) * 26f, -28f, 28f);
                Quaternion targetRot = lookRot * Quaternion.Euler(forwardPitchAngle * 1.4f, 0f, bank);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * 7f);
            }

            if (t >= 1f)
            {
                // Completed transition: seamlessly become orbiting agent of new target!
                isTransitioning = false;
                currentTarget = transitionTarget;
                targetType = transitionTargetType;
                orbitRadius = targetOrbitRadius;
                flightAltitude = targetFlightAltitude;
                currentOrbitAngle = approachAngle;
            }
        }

        private Vector3 ApplyUrbanAvoidance(Vector3 candidatePos)
        {
            if (!avoidUrbanBuildings) return candidatePos;

            candidatePos.x = Mathf.Clamp(candidatePos.x, urbanBoundsMinXZ.x, urbanBoundsMaxXZ.x);
            candidatePos.z = Mathf.Clamp(candidatePos.z, urbanBoundsMinXZ.y, urbanBoundsMaxXZ.y);
            candidatePos.y = Mathf.Max(candidatePos.y, minUrbanClearanceY);

            Collider[] nearby = Physics.OverlapSphere(candidatePos, urbanAvoidRadius, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < nearby.Length; i++)
            {
                Collider col = nearby[i];
                if (col == null || col.CompareTag("Ground")) continue;
                if (col.GetComponentInParent<SimpleJaegerController>() != null ||
                    col.GetComponentInParent<KaijuAI>() != null ||
                    col.GetComponentInParent<ChinookCombatPatrolAgent>() != null) continue;

                Vector3 closest = col.ClosestPoint(candidatePos);
                Vector3 away = candidatePos - closest;
                float dist = away.magnitude;
                if (dist >= urbanAvoidRadius) continue;

                float roofY = col.bounds.max.y;
                if (roofY <= candidatePos.y + 18f)
                {
                    candidatePos.y = Mathf.Max(candidatePos.y, roofY + 10f);
                }

                Vector3 horiz = new Vector3(away.x, 0f, away.z);
                if (horiz.sqrMagnitude < 0.01f)
                {
                    horiz = new Vector3(candidatePos.x - col.bounds.center.x, 0f, candidatePos.z - col.bounds.center.z);
                }
                if (horiz.sqrMagnitude < 0.01f) horiz = Vector3.forward;

                candidatePos += horiz.normalized * (urbanAvoidRadius - dist) * 1.35f;
            }

            candidatePos.x = Mathf.Clamp(candidatePos.x, urbanBoundsMinXZ.x, urbanBoundsMaxXZ.x);
            candidatePos.z = Mathf.Clamp(candidatePos.z, urbanBoundsMinXZ.y, urbanBoundsMaxXZ.y);
            candidatePos.y = Mathf.Max(candidatePos.y, minUrbanClearanceY);
            return candidatePos;
        }

        private void UpdateSearchlightGimbal(float dt)
        {
            if (searchlightGimbal == null) return;

            // Haphazardly sweep searchlight across the combat zone:
            // Combines a general aim toward the combatants with chaotic operator sweeps
            Vector3 targetAimPoint = Vector3.zero;
            if (currentTarget != null)
            {
                targetAimPoint = currentTarget.position + Vector3.up * 7f;
            }

            Vector3 aimDir = (targetAimPoint - searchlightGimbal.position).normalized;
            if (aimDir.sqrMagnitude > 0.001f)
            {
                // Base forward alignment with Chinook model (-Z is helicopter forward)
                Quaternion baseLook = Quaternion.LookRotation(aimDir, Vector3.up);

                // Multi-frequency Perlin noise and sinusoidal sweeps for erratic, haphazard search motion
                float timeX = Time.time * sweepSpeedX + noiseSeed;
                float timeY = Time.time * sweepSpeedY + noiseSeed * 1.5f;

                float yawJitter = (Mathf.PerlinNoise(timeX, 0f) * 2f - 1f) * sweepAngleX
                                + Mathf.Sin(Time.time * 1.7f) * 8f;
                float pitchJitter = (Mathf.PerlinNoise(0f, timeY) * 2f - 1f) * sweepAngleY
                                  + Mathf.Cos(Time.time * 2.3f) * 6f;

                Quaternion sweepRot = Quaternion.Euler(pitchJitter, yawJitter, 0f);
                Quaternion desiredWorldRot = baseLook * sweepRot;

                searchlightGimbal.rotation = Quaternion.Slerp(searchlightGimbal.rotation, desiredWorldRot, dt * 5.5f);
            }
        }

        private void UpdateLightningReaction()
        {
            float flash = Shader.GetGlobalFloat(IdGlobalLightningFlash);
            if (LightningSystem.Instance != null && LightningSystem.Instance.CurrentFlashNormalized > flash)
            {
                flash = LightningSystem.Instance.CurrentFlashNormalized;
            }

            if (searchlight != null)
            {
                searchlight.intensity = Mathf.Lerp(baseLightIntensity, lightningPeakIntensity, flash);
                searchlight.range = Mathf.Lerp(baseLightRange, lightningPeakRange, flash);
                searchlight.spotAngle = Mathf.Lerp(baseSpotAngle, lightningSpotAngle, flash);
            }

            if (volumetricBeam != null)
            {
                // Volumetric beam scales up slightly in storm flare
                float beamScaleBoost = 1.0f + flash * 0.35f;
                volumetricBeam.localScale = new Vector3(0.9f * beamScaleBoost, 0.9f * beamScaleBoost, 1.15f * beamScaleBoost);
            }
        }
    }
}
