using UnityEngine;
using UnityEngine.Events;

namespace PacificRimWater
{
    /// <summary>
    /// Pacific Rim Heavy Ocean Wading Interaction.
    /// Simulates footstep shockwaves, dual-leg ocean parting bow wakes, water eruptions,
    /// and viscous hydrodynamic drag for giant mechs wading through thigh-deep ocean water.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class JaegerWaterInteraction : MonoBehaviour
    {
        [Header("Animation-Synced Footsteps")]
        [Tooltip("Sync footstep shockwaves and splashes to actual Foot.L / Foot.R bone touchdowns.")]
        public bool useAnimationSyncedFootsteps = true;
        [Tooltip("Minimum seconds between impacts on the same foot.")]
        public float minFootstepInterval = 0.55f;
        [Tooltip("Minimum seconds between alternating Left and Right foot impacts.")]
        public float crossFootMinInterval = 0.32f;

        [Header("Footstep Shockwave Settings")]
        [Tooltip("Radius of the tidal shockwave expanding from each footstep (meters).")]
        public float footstepWaveRadius = 18.0f;
        [Tooltip("Wave height impulse of each footstep.")]
        public float footstepWaveStrength = 2.4f;
        [Tooltip("Fallback distance in meters between consecutive footsteps if foot bones are missing.")]
        public float strideDistance = 6.0f;
        [Tooltip("Lateral spacing between left and right foot (meters).")]
        public float footSpacing = 2.4f;
        [Tooltip("Physical width of the wading leg for bow wave calculations.")]
        public float legWidth = 2.8f;

        [Header("Continuous Wake Bubbles & Foam")]
        [Tooltip("Distance in meters moved before spawning another cross-section of churning wake bubbles & foam.")]
        public float wakeTrailStepDistance = 0.45f;

        [Header("Water Eruptions & Splashes")]
        [Tooltip("Spawn vertical water eruptions and spray plumes on footsteps.")]
        public bool spawnSplashes = true;
        public float splashScale = 0.55f;

        [Header("Impact Camera Shake")]
        [Tooltip("Intensity of screen shake on heavy foot impact.")]
        public float footstepShakeIntensity = 0.38f;
        public float footstepShakeDuration = 0.32f;

        [Header("Ocean Wading Physics")]
        [Tooltip("Viscous hydrodynamic drag resisting horizontal movement through ocean water.")]
        [Range(0.1f, 10f)]
        public float oceanWadingDrag = 3.5f;
        [Tooltip("Speed multiplier applied when wading through ocean water (0.78 = 22% slower).")]
        [Range(0.2f, 1.0f)]
        public float deepWaterSpeedMultiplier = 0.78f;
        [Tooltip("Upward buoyant support when submerged deeper than buoyancyThresholdDepth (0 = stay grounded on seabed).")]
        public float oceanBuoyancy = 0f;
        [Tooltip("Submersion depth required before upward buoyancy starts lifting the character.")]
        public float buoyancyThresholdDepth = 22.0f;

        [Header("Limb Transforms (Auto-bound if unassigned)")]
        public Transform leftFootTransform;
        public Transform rightFootTransform;
        public Transform leftShinTransform;
        public Transform rightShinTransform;
        public Transform leftThighTransform;
        public Transform rightThighTransform;

        [Header("Events")]
        public UnityEvent<Vector3> OnFootstepWaterImpact;
        public UnityEvent OnEnterOcean;
        public UnityEvent OnExitOcean;

        private struct FootStepTracker
        {
            public bool initialized;
            public Vector3 prevLocalPos;
            public float smoothedVelY;
            public float smoothedSignedVelZ;
            public float minY;
            public float maxY;
            public float minZ;
            public float maxZ;
            public float peakDescentSpeed;
            public bool isAirborne;
            public float lastStrikeTime;

            public void ResetAirborne()
            {
                initialized = false;
                smoothedVelY = 0f;
                smoothedSignedVelZ = 0f;
                peakDescentSpeed = 0f;
                isAirborne = false;
            }
        }

        // Internal State
        private Rigidbody rb;
        private Animator animator;
        private WaterSystem water;
        private Vector3 lastStepPos;
        private Vector3 prevPos;
        private bool isLeftFoot = true;
        private bool wasInWater;
        private float initialDrag;
        private float nextWakeTime;
        private float wakeTrailAccumulator;
        private FootStepTracker leftTracker;
        private FootStepTracker rightTracker;
        private float lastAnyFootstepTime = -10f;
        private SimpleJaegerController controller;

        public bool IsWading { get; private set; }
        public float SubmergedDepth { get; private set; }
        public Vector3 LeftLegSurfacePoint { get; private set; }
        public Vector3 RightLegSurfacePoint { get; private set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            animator = GetComponent<Animator>();
            controller = GetComponent<SimpleJaegerController>();
            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                initialDrag = rb.linearDamping;
#else
                initialDrag = rb.drag;
#endif
            }
            ResolveLimbTransformsIfMissing();
        }

        void Start()
        {
            water = WaterSystem.Instance;
            if (animator == null) animator = GetComponent<Animator>();
            lastStepPos = transform.position;
            prevPos = transform.position;
            ResolveLimbTransformsIfMissing();
            if (water != null)
            {
                wasInWater = (water.GetWaterHeight(transform.position) - transform.position.y) > 0.1f;
                IsWading = wasInWater;
            }
            else
            {
                wasInWater = true;
            }
        }

        private void ResolveLimbTransformsIfMissing()
        {
            if (leftFootTransform == null) leftFootTransform = FindChildByName(transform, "Foot.L");
            if (rightFootTransform == null) rightFootTransform = FindChildByName(transform, "Foot.R");
            if (leftShinTransform == null) leftShinTransform = FindChildByName(transform, "Shin.L");
            if (rightShinTransform == null) rightShinTransform = FindChildByName(transform, "Shin.R");
            if (leftThighTransform == null) leftThighTransform = FindChildByName(transform, "Thigh.L");
            if (rightThighTransform == null) rightThighTransform = FindChildByName(transform, "Thigh.R");
        }

        private static Transform FindChildByName(Transform parent, string targetName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == targetName) return child;
                Transform found = FindChildByName(child, targetName);
                if (found != null) return found;
            }
            return null;
        }

        private Vector3 ComputeLegWaterlinePoint(Transform shin, Transform thigh, float sideSign, float oceanSurfaceY)
        {
            if (shin != null && thigh != null)
            {
                Vector3 a = shin.position;
                Vector3 b = thigh.position;
                float dy = b.y - a.y;
                if (Mathf.Abs(dy) > 0.05f)
                {
                    float t = Mathf.Clamp01((oceanSurfaceY - a.y) / dy);
                    return Vector3.Lerp(a, b, t);
                }
            }

            Vector3 fallback = transform.position + transform.right * (sideSign * footSpacing);
            fallback.y = oceanSurfaceY;
            return fallback;
        }

        void Update()
        {
            Vector3 currentPos = transform.position;
            Vector3 vel = GetMovementVelocity();
            float horizontalSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;

            if (water == null)
            {
                water = WaterSystem.Instance;
            }

            bool isDeepUnderwaterMode = (water == null) || (WaterSplashManager.Instance != null && WaterSplashManager.Instance.IsDeepUnderwater);
            if (water == null)
            {
                IsWading = false;
                SubmergedDepth = isDeepUnderwaterMode ? 60f : 0f;

                if (horizontalSpeed > 0.45f && WaterSplashManager.Instance != null)
                {
                    wakeTrailAccumulator += horizontalSpeed * Time.deltaTime;
                    if (wakeTrailAccumulator >= Mathf.Max(0.35f, wakeTrailStepDistance * 1.15f))
                    {
                        wakeTrailAccumulator = 0f;
                        Vector3 lPos = leftFootTransform != null ? leftFootTransform.position : (currentPos - transform.right * footSpacing);
                        Vector3 rPos = rightFootTransform != null ? rightFootTransform.position : (currentPos + transform.right * footSpacing);
                        WaterSplashManager.Instance.SpawnUnderwaterWakeBubbles(lPos, rPos, vel, horizontalSpeed);
                    }
                }

                prevPos = currentPos;
                return;
            }

            float oceanSurfaceY = water.GetWaterHeight(currentPos);
            SubmergedDepth = oceanSurfaceY - currentPos.y;

            LeftLegSurfacePoint = ComputeLegWaterlinePoint(leftShinTransform, leftThighTransform, -1f, oceanSurfaceY);
            RightLegSurfacePoint = ComputeLegWaterlinePoint(rightShinTransform, rightThighTransform, 1f, oceanSurfaceY);

            bool inWater = SubmergedDepth > 0.1f;
            bool isFullySubmerged = SubmergedDepth > 24.5f || (WaterSplashManager.Instance != null && WaterSplashManager.Instance.IsDeepUnderwater);

            if (inWater && !wasInWater)
            {
                OnEnterOcean?.Invoke();
                float entryStrength = Mathf.Min(footstepWaveStrength * 1.25f, water.maxRippleHeight);
                TriggerFootstepImpact(currentPos, footstepWaveRadius * 1.15f, entryStrength);
            }
            else if (!inWater && wasInWater)
            {
                OnExitOcean?.Invoke();
            }

            IsWading = inWater && !isFullySubmerged;

            if (inWater && !isFullySubmerged)
            {
                // Distance-based fallback footstep shockwave only if animator is missing
                if (!useAnimationSyncedFootsteps || animator == null)
                {
                    float distMoved = Vector3.Distance(
                        new Vector3(currentPos.x, 0f, currentPos.z),
                        new Vector3(lastStepPos.x, 0f, lastStepPos.z));

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

                        float dynamicStrength = footstepWaveStrength * Mathf.Clamp(horizontalSpeed / 8.5f, 0.7f, 1.35f);
                        TriggerFootstepImpact(footContact, footstepWaveRadius, dynamicStrength);
                    }
                }

                // Dual leg bow wakes and dynamic ripples plowing through the ocean surface
                if (horizontalSpeed > 0.8f && Time.time >= nextWakeTime)
                {
                    nextWakeTime = Time.time + 0.14f;
                    float wakeAmp = Mathf.Clamp(horizontalSpeed * 0.18f, 0.75f, 1.8f);
                    water.AddWadingWake(LeftLegSurfacePoint, vel, legWidth, wakeAmp);
                    water.AddWadingWake(RightLegSurfacePoint, vel, legWidth, wakeAmp);
                }

                // Continuous churning bubble & foam wake trail streaming behind wading legs
                if (horizontalSpeed > 0.35f)
                {
                    wakeTrailAccumulator += horizontalSpeed * Time.deltaTime;
                    if (wakeTrailAccumulator >= Mathf.Max(0.15f, wakeTrailStepDistance))
                    {
                        wakeTrailAccumulator = 0f;
                        if (WaterSplashManager.Instance != null)
                        {
                            WaterSplashManager.Instance.SpawnWadingWakeTrail(
                                LeftLegSurfacePoint,
                                RightLegSurfacePoint,
                                vel,
                                legWidth,
                                horizontalSpeed);
                        }
                    }
                }
            }

            wasInWater = inWater;
            prevPos = currentPos;
        }

        void LateUpdate()
        {
            if (!useAnimationSyncedFootsteps) return;
            ResolveLimbTransformsIfMissing();

            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null || leftFootTransform == null || rightFootTransform == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;

            Vector3 vel = GetMovementVelocity();
            float horizontalSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
            float animSpeed = 0f;
            float walkDir = 1f;
            float runBlend = 0f;
            try { animSpeed = animator.GetFloat("Speed"); } catch { }
            try { walkDir = animator.GetFloat("WalkDirection"); } catch { }
            try { runBlend = animator.GetFloat("RunBlend"); } catch { }
            if (Mathf.Abs(walkDir) < 0.1f) walkDir = 1f;

            bool isDashing = controller != null && controller.IsDashing;
            bool isLocomoting = !isDashing && (animSpeed > 0.08f || horizontalSpeed > 0.35f);

            if (!isLocomoting)
            {
                leftTracker.ResetAirborne();
                rightTracker.ResetAirborne();
                return;
            }

            UpdateFootStepTracker(ref leftTracker, leftFootTransform, true, walkDir, runBlend, horizontalSpeed, dt);
            UpdateFootStepTracker(ref rightTracker, rightFootTransform, false, walkDir, runBlend, horizontalSpeed, dt);
        }

        private void TriggerFootPlant(Transform footBone, bool isLeft, float horizontalSpeed)
        {
            Vector3 footContact;
            if (footBone != null)
            {
                footContact = footBone.position;
            }
            else
            {
                Vector3 footOffset = (isLeft ? -transform.right : transform.right) * footSpacing;
                footContact = transform.position + footOffset;
            }

            bool isDeepUnderwater = SubmergedDepth > 24.5f || (WaterSplashManager.Instance != null && WaterSplashManager.Instance.IsDeepUnderwater);
            if (water != null && !isDeepUnderwater)
            {
                footContact.y = water.GetWaterHeight(footContact);
            }

            float speedFactor = Mathf.Clamp(horizontalSpeed / 8.5f, 0.8f, 1.4f);
            float dynamicStrength = footstepWaveStrength * speedFactor;

            TriggerFootstepImpact(footContact, footstepWaveRadius, dynamicStrength, true);
        }

        private void UpdateFootStepTracker(
            ref FootStepTracker tracker,
            Transform footTransform,
            bool isLeft,
            float walkDir,
            float runBlend,
            float horizontalSpeed,
            float dt)
        {
            Vector3 localPos = transform.InverseTransformPoint(footTransform.position);

            if (!tracker.initialized)
            {
                tracker.prevLocalPos = localPos;
                tracker.minY = localPos.y;
                tracker.maxY = localPos.y + 0.25f;
                tracker.minZ = localPos.z - 0.20f;
                tracker.maxZ = localPos.z + 0.20f;
                tracker.smoothedVelY = 0f;
                tracker.smoothedSignedVelZ = 0f;
                tracker.peakDescentSpeed = 0f;
                tracker.isAirborne = false;
                tracker.initialized = true;
                return;
            }

            float rawVelY = (localPos.y - tracker.prevLocalPos.y) / dt;
            float rawSignedVelZ = ((localPos.z - tracker.prevLocalPos.z) / dt) * walkDir;
            float prevVelY = tracker.smoothedVelY;

            float smoothAlpha = 1f - Mathf.Exp(-24f * dt);
            tracker.smoothedVelY = Mathf.Lerp(tracker.smoothedVelY, rawVelY, smoothAlpha);
            tracker.smoothedSignedVelZ = Mathf.Lerp(tracker.smoothedSignedVelZ, rawSignedVelZ, smoothAlpha);

            // Fast adaptation to actual foot trajectory extremes (walk vs run vs strafe)
            float relaxRate = dt * 1.5f;
            tracker.minY = Mathf.Min(Mathf.Lerp(tracker.minY, localPos.y, relaxRate), localPos.y);
            tracker.maxY = Mathf.Max(Mathf.Lerp(tracker.maxY, localPos.y, relaxRate), localPos.y);
            tracker.minZ = Mathf.Min(Mathf.Lerp(tracker.minZ, localPos.z, relaxRate), localPos.z);
            tracker.maxZ = Mathf.Max(Mathf.Lerp(tracker.maxZ, localPos.z, relaxRate), localPos.z);

            float yRange = Mathf.Max(0.20f, tracker.maxY - tracker.minY);
            float normY = Mathf.Clamp01((localPos.y - tracker.minY) / yRange);

            // Arm lift phase: foot clearly lifted off baseline into swing
            if (!tracker.isAirborne)
            {
                if (localPos.y >= 1.82f || (yRange > 0.45f && normY >= 0.38f))
                {
                    tracker.isAirborne = true;
                    tracker.peakDescentSpeed = 0f;
                }
            }

            if (tracker.isAirborne)
            {
                if (tracker.smoothedVelY < 0f)
                {
                    tracker.peakDescentSpeed = Mathf.Max(tracker.peakDescentSpeed, -tracker.smoothedVelY);
                }

                float now = Time.time;
                float groundContactY = Mathf.Lerp(1.38f, 1.48f, Mathf.Clamp01(runBlend));
                bool isAtBottom = localPos.y <= groundContactY || (yRange > 0.45f && normY <= 0.14f && localPos.y <= 1.54f);
                // Exclude mid-air forward swing dips (e.g., Gipsy.Run left leg passing forward at +velZ)
                bool isPlanting = tracker.smoothedSignedVelZ <= 0.85f;
                bool decelerationTouchdown = (prevVelY < -0.15f && tracker.smoothedVelY >= -0.95f) || localPos.y <= 1.35f;

                float effectiveMinSame = Mathf.Max(0.28f, minFootstepInterval);
                float effectiveCrossMin = Mathf.Max(0.16f, crossFootMinInterval);
                bool canTrigger = (now - tracker.lastStrikeTime >= effectiveMinSame) &&
                                  (now - lastAnyFootstepTime >= effectiveCrossMin);

                if (isAtBottom && isPlanting && decelerationTouchdown && canTrigger)
                {
                    tracker.isAirborne = false;
                    tracker.lastStrikeTime = now;
                    lastAnyFootstepTime = now;

                    TriggerFootPlant(footTransform, isLeft, horizontalSpeed);
                    tracker.peakDescentSpeed = 0f;
                }
            }

            tracker.prevLocalPos = localPos;
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
            float submersionFactor = Mathf.Clamp01(SubmergedDepth / 10.0f);

#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = Mathf.Lerp(initialDrag, oceanWadingDrag, submersionFactor);
#else
            rb.drag = Mathf.Lerp(initialDrag, oceanWadingDrag, submersionFactor);
#endif

            // Deep water buoyant assist (only activates past buoyancyThresholdDepth so thigh-deep Jaeger stays on seabed)
            if (oceanBuoyancy > 0f && SubmergedDepth > buoyancyThresholdDepth)
            {
                float floatPower = Mathf.Clamp01((SubmergedDepth - buoyancyThresholdDepth) / 4.0f);
                rb.AddForce(Vector3.up * (oceanBuoyancy * floatPower), ForceMode.Acceleration);
            }
        }

        /// <summary>
        /// Triggers a footstep impact shockwave, bounded spray plume, and expanding foam ring.
        /// </summary>
        public void TriggerFootstepImpact(Vector3 contactPoint, float radius, float strength, bool triggerCameraShake = true)
        {
            bool isDeepUnderwater = SubmergedDepth > 24.5f || (WaterSplashManager.Instance != null && WaterSplashManager.Instance.IsDeepUnderwater);
            if (water != null && !isDeepUnderwater)
            {
                bool isHeavyLunge = controller != null && controller.IsSprintAttacking;
                float finalRadius = isHeavyLunge ? Mathf.Max(radius, 26f) : radius;
                float finalStrength = isHeavyLunge ? Mathf.Max(strength, 2.8f) : strength;
                water.AddFootstepWave(contactPoint, finalRadius, finalStrength);
            }

            if (spawnSplashes)
            {
                if (WaterSplashManager.Instance != null)
                {
                    WaterSplashManager.Instance.SpawnFootstepSplash(contactPoint, splashScale);
                }
            }

            if (triggerCameraShake && footstepShakeIntensity > 0f)
            {
                float multiplier = (controller != null && controller.isRunning) ? 1.45f : 1.0f;
                float shakeDuration = footstepShakeDuration * ((controller != null && controller.isRunning) ? 1.25f : 1.0f);

                // Keep Operation Pitfall intro cutscene tracking camera subtle so footsteps feel weighty without jarring close-up shots
                if (PacificRimCutscene.OperationPitfallIntroCutscene.Instance != null &&
                    PacificRimCutscene.OperationPitfallIntroCutscene.Instance.isCutsceneRunning)
                {
                    multiplier *= 0.22f;
                    shakeDuration *= 0.70f;
                }

                DoCameraShake(footstepShakeIntensity * multiplier, shakeDuration);
            }

            PacificRimAudio.PacificRimAudioManager.Instance?.PlayOceanFootstep(contactPoint, (controller != null && controller.isRunning) ? 1.35f : 1.0f);
            if (Random.value < 0.45f)
            {
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayHydraulicServo(contactPoint + Vector3.up * 8f, 0.4f);
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
    /// Multi-channel non-destructive camera feedback controller for Pacific Rim footsteps,
    /// directional attack swing weight, and hit-vs-miss impact vibrations.
    /// Runs in LateUpdate after JaegerWaterInteraction (-10) and before JaegerCameraFollow (50)
    /// so every footstep shake applies on the exact frame of touchdown with zero frame lag.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class PacificRimCameraShake : MonoBehaviour
    {
        // Channel 1: Low-frequency footstep / ground rumble
        private float rumbleTime;
        private float rumbleDuration;
        private float rumbleMagnitude;
        private float rumbleElapsed;
        private float rumbleSeed;

        // Channel 2: Smooth directional attack follow-through (punch / heavy arc weight)
        private float swingElapsed;
        private float swingDuration;
        private Vector3 swingPeakPos;
        private Vector3 swingPeakRot;

        // Channel 3: High-frequency impact vibration burst (light micro-tremor on miss, strong shudder on hit)
        private float vibTime;
        private float vibDuration;
        private float vibPosAmplitude;
        private float vibRotAmplitude;
        private float vibFrequency = 32f;
        private float vibSeed;

        // Channel 4: Continuous capacitor charge-up rumble for the I-19 Plasmacaster
        private float chargeTremorIntensity;
        private float chargeTremorSeed;

        public Vector3 CurrentOffset { get; private set; }
        public Vector3 CurrentRotationOffset { get; private set; }

        /// <summary>
        /// Triggers a damped low-frequency footstep or environmental rumble.
        /// </summary>
        public void Shake(float magnitude, float duration)
        {
            if (magnitude <= 0f) return;
            float clampedDur = Mathf.Max(0.01f, duration);
            if (magnitude >= rumbleMagnitude * (rumbleDuration > 0f ? rumbleTime / rumbleDuration : 0f))
            {
                rumbleMagnitude = magnitude;
                rumbleDuration = clampedDur;
                rumbleTime = clampedDur;
                rumbleElapsed = 0f;
                rumbleSeed = Random.Range(0f, 100f);
            }
        }

        /// <summary>
        /// Triggers seismic earth-shattering camera feedback when the Jaeger stomps the ocean floor.
        /// </summary>
        public void TriggerStompFeedback(float intensity = 1.0f)
        {
            float mag = Mathf.Clamp(intensity, 0.5f, 2.5f);
            Shake(0.65f * mag, 0.45f * mag);

            // Immediate sharp downward vertical drop and recovery
            swingDuration = 0.42f;
            swingElapsed = 0f;
            swingPeakPos = new Vector3(0f, -0.62f * mag, 0.35f * mag);
            swingPeakRot = new Vector3(-1.85f * mag, Random.Range(-0.4f, 0.4f) * mag, 0f);

            vibSeed = Random.Range(0f, 100f);
            vibDuration = 0.35f * mag;
            vibTime = vibDuration;
            vibPosAmplitude = 0.38f * mag;
            vibRotAmplitude = 1.55f * mag;
            vibFrequency = 22f;
        }

        /// <summary>
        /// High-frequency mechanical turbine vibration pulsing through the cockpit on reactor overcharge.
        /// </summary>
        public void TriggerTurbineOverchargeFeedback()
        {
            vibSeed = Random.Range(0f, 100f);
            vibDuration = 0.65f;
            vibTime = vibDuration;
            vibPosAmplitude = 0.22f;
            vibRotAmplitude = 0.95f;
            vibFrequency = 45f; // High-frequency turbine hum

            swingDuration = 0.55f;
            swingElapsed = 0f;
            swingPeakPos = new Vector3(0f, 0.18f, -0.25f);
            swingPeakRot = new Vector3(0.85f, 0f, 0f);
        }

        /// <summary>
        /// Sets continuous high-frequency cockpit rumble while charging the I-19 Plasmacaster (0 = off, 1 = peak saturation).
        /// </summary>
        public void SetPlasmaChargeTremor(float intensity)
        {
            float clamped = Mathf.Clamp01(intensity);
            if (clamped > 0.001f && chargeTremorIntensity <= 0.001f)
            {
                chargeTremorSeed = Random.Range(0f, 100f);
            }
            chargeTremorIntensity = clamped;
        }

        /// <summary>
        /// Heavy backward cannon kick and sonic shockwave vibration when the I-19 Plasmacaster fires.
        /// </summary>
        public void TriggerPlasmaDischargeFeedback()
        {
            chargeTremorIntensity = 0f;
            Shake(0.95f, 0.48f);

            swingDuration = 0.56f;
            swingElapsed = 0f;
            swingPeakPos = new Vector3(0.45f, -0.28f, -0.92f);
            swingPeakRot = new Vector3(-2.60f, 1.85f, -1.65f);

            vibSeed = Random.Range(0f, 100f);
            vibDuration = 0.46f;
            vibTime = vibDuration;
            vibPosAmplitude = 0.58f;
            vibRotAmplitude = 2.45f;
            vibFrequency = 36f;
        }

        /// <summary>
        /// Rigid mechanical shudder when blocking incoming attacks with the defense matrix.
        /// </summary>
        public void TriggerBlockImpactFeedback(float intensity = 1.0f)
        {
            float scale = Mathf.Clamp(intensity, 0.7f, 2.8f);
            Shake(0.45f * scale, 0.25f);

            swingDuration = 0.30f;
            swingElapsed = 0f;
            swingPeakPos = new Vector3(0f, -0.26f * scale, -0.52f * scale);
            swingPeakRot = new Vector3(1.35f * scale, Random.Range(-0.55f, 0.55f) * scale, 0f);

            vibSeed = Random.Range(0f, 100f);
            vibDuration = 0.28f;
            vibTime = vibDuration;
            vibPosAmplitude = 0.42f * scale;
            vibRotAmplitude = 1.65f * scale;
            vibFrequency = 40f;
        }

        /// <summary>
        /// Sharp cockpit trauma punch when the Jaeger suffers an unblocked strike from the Kaiju.
        /// </summary>
        public void TriggerPlayerHitFeedback(bool isHeavy)
        {
            Shake(isHeavy ? 1.15f : 0.72f, isHeavy ? 0.50f : 0.35f);

            float sideSign = Random.value > 0.5f ? 1f : -1f;
            swingDuration = isHeavy ? 0.48f : 0.34f;
            swingElapsed = 0f;
            swingPeakPos = isHeavy
                ? new Vector3(sideSign * 0.55f, -0.62f, -0.85f)
                : new Vector3(sideSign * 0.35f, -0.34f, -0.55f);
            swingPeakRot = isHeavy
                ? new Vector3(2.40f, sideSign * 1.90f, sideSign * 2.10f)
                : new Vector3(1.45f, sideSign * 1.25f, sideSign * 1.35f);

            vibSeed = Random.Range(0f, 100f);
            vibDuration = isHeavy ? 0.42f : 0.28f;
            vibTime = vibDuration;
            vibPosAmplitude = isHeavy ? 0.65f : 0.38f;
            vibRotAmplitude = isHeavy ? 2.80f : 1.75f;
            vibFrequency = isHeavy ? 32f : 38f;
        }

        /// <summary>
        /// Triggers weighty, noticeably varied camera movement for an attack swing,
        /// with a calm directional follow-through on misses and sharper tactile vibration on hits.
        /// </summary>
        public void TriggerAttackFeedback(int attackVariant, bool isHeavy, bool didHit)
        {
            Vector3 basePos;
            Vector3 baseRot;

            switch (attackVariant)
            {
                case 1:
                    // Punch 1 (right hook / cross) - snappy right-to-left yaw recoil + forward thrust
                    basePos = new Vector3(0.28f, -0.16f, 0.48f);
                    baseRot = new Vector3(-0.75f, 1.25f, -0.72f);
                    break;
                case 2:
                    // Punch 2 (left cross / hook) - leftward torque + upward lift
                    basePos = new Vector3(-0.30f, -0.14f, 0.48f);
                    baseRot = new Vector3(-0.70f, -1.30f, 0.78f);
                    break;
                case 3:
                    // Punch 3 (forward drive / uppercut finisher) - heavy vertical surge and chin drop
                    basePos = new Vector3(0.14f, -0.32f, 0.65f);
                    baseRot = new Vector3(-1.45f, 0.75f, -0.55f);
                    break;
                case 5:
                    // Running heavy lunge - colossal forward charge momentum
                    basePos = new Vector3(-0.38f, -0.55f, 1.35f);
                    baseRot = new Vector3(-2.60f, -1.45f, 1.35f);
                    break;
                default:
                    // Variant 4: Standing Heavy Attack (Elbow Rocket power blow) - massive kinetic release
                    basePos = new Vector3(0.42f, -0.48f, 1.15f);
                    baseRot = new Vector3(-2.40f, 1.60f, -1.40f);
                    break;
            }

            float randomScale = Random.Range(0.92f, 1.18f);
            float hitBoost = didHit ? 1.35f : 1.0f;
            float scale = randomScale * hitBoost;

            swingDuration = isHeavy ? 0.54f : 0.38f;
            swingElapsed = 0f;
            swingPeakPos = basePos * scale;
            swingPeakRot = new Vector3(
                baseRot.x * scale + Random.Range(-0.15f, 0.15f),
                baseRot.y * scale + Random.Range(-0.20f, 0.20f),
                baseRot.z * scale + Random.Range(-0.15f, 0.15f));

            vibSeed = Random.Range(0f, 100f);
            if (didHit)
            {
                vibDuration = isHeavy ? 0.38f : 0.26f;
                vibTime = vibDuration;
                vibPosAmplitude = (isHeavy ? 0.55f : 0.32f) * Random.Range(0.95f, 1.20f);
                vibRotAmplitude = (isHeavy ? 2.40f : 1.45f) * Random.Range(0.95f, 1.20f);
                vibFrequency = isHeavy ? 28f : 34f;
            }
            else
            {
                // Miss: weighty mechanical servo tremor so the swing feels colossal
                vibDuration = isHeavy ? 0.22f : 0.15f;
                vibTime = vibDuration;
                vibPosAmplitude = isHeavy ? 0.14f : 0.08f;
                vibRotAmplitude = isHeavy ? 0.55f : 0.28f;
                vibFrequency = 24f;
            }
        }

        void LateUpdate()
        {
            Vector3 totalPos = Vector3.zero;
            Vector3 totalRot = Vector3.zero;

            // 1. Footstep rumble channel (Phase-locked downward thud at impact followed by damped seismic decay)
            if (rumbleTime > 0f && rumbleDuration > 0f)
            {
                float dt = Time.deltaTime;
                rumbleTime = Mathf.Max(0f, rumbleTime - dt);
                rumbleElapsed += dt;
                float damper = rumbleTime / rumbleDuration;
                float decay = damper * damper;

                float impactWindow = Mathf.Min(0.14f, rumbleDuration * 0.65f);
                float impactEnv = impactWindow > 0.001f && rumbleElapsed < impactWindow
                    ? Mathf.Sin((rumbleElapsed / impactWindow) * Mathf.PI)
                    : 0f;
                float t = rumbleElapsed * 28f;

                Vector3 rumbleDir = new Vector3(
                    Mathf.Sin(t * 1.13f + rumbleSeed) * 0.42f * decay,
                    -impactEnv * 1.15f + Mathf.Sin(t * 1.47f) * 0.55f * decay,
                    Mathf.Sin(t * 0.79f + rumbleSeed * 0.5f) * 0.35f * decay);

                totalPos += rumbleDir * rumbleMagnitude;
                totalRot.x += (-impactEnv * 2.4f + Mathf.Sin(t * 1.25f) * 0.8f * decay) * rumbleMagnitude;
                totalRot.y += Mathf.Sin(t * 1.05f + rumbleSeed) * rumbleMagnitude * 0.9f * decay;
                totalRot.z += Mathf.Cos(t * 0.95f + rumbleSeed) * rumbleMagnitude * 1.1f * decay;
            }

            // 2. Smooth directional swing weight envelope
            if (swingDuration > 0f && swingElapsed < swingDuration)
            {
                swingElapsed = Mathf.Min(swingDuration, swingElapsed + Time.deltaTime);
                float progress = swingElapsed / swingDuration;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Pow(progress, 0.68f));
                totalPos += swingPeakPos * envelope;
                totalRot += swingPeakRot * envelope;
            }

            // 3. Tactile vibration burst (pronounced on landed strikes, faint on misses)
            if (vibTime > 0f && vibDuration > 0f)
            {
                vibTime = Mathf.Max(0f, vibTime - Time.deltaTime);
                float damper = vibTime / vibDuration;
                float decay = damper * damper;
                float w = (Time.time + vibSeed) * vibFrequency * Mathf.PI * 2f;

                Vector3 vibDir = new Vector3(
                    Mathf.Sin(w),
                    Mathf.Cos(w * 1.19f + 0.7f),
                    Mathf.Sin(w * 0.87f + 1.9f) * 0.6f);
                Vector3 vibRot = new Vector3(
                    Mathf.Sin(w * 1.07f + 0.4f),
                    Mathf.Cos(w * 0.93f + 1.3f) * 0.8f,
                    Mathf.Sin(w * 1.23f + 2.1f) * 0.9f);

                totalPos += vibDir * (vibPosAmplitude * decay);
                totalRot += vibRot * (vibRotAmplitude * decay);
            }

            // 4. Continuous Plasmacaster capacitor charge tremor
            if (chargeTremorIntensity > 0.001f)
            {
                float sq = chargeTremorIntensity * chargeTremorIntensity;
                float cw = (Time.time + chargeTremorSeed) * Mathf.Lerp(26f, 56f, chargeTremorIntensity) * Mathf.PI * 2f;
                Vector3 cPos = new Vector3(
                    Mathf.Sin(cw * 1.11f),
                    Mathf.Cos(cw * 1.37f + 0.5f),
                    Mathf.Sin(cw * 0.79f + 1.4f) * 0.55f) * (0.16f * sq);
                Vector3 cRot = new Vector3(
                    Mathf.Sin(cw * 1.23f + 0.8f),
                    Mathf.Cos(cw * 0.91f + 1.7f),
                    Mathf.Sin(cw * 1.41f + 2.3f)) * (0.72f * sq);

                totalPos += cPos;
                totalRot += cRot;
            }

            CurrentOffset = totalPos;
            CurrentRotationOffset = totalRot;
        }
    }
}
