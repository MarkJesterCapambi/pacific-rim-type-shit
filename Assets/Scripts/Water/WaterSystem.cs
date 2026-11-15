using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Zero-lag Pacific Rim Ocean Wave System.
    /// Synchronizes analytical 3-band Gerstner swells and a fixed 8-slot propagating wake ring
    /// buffer directly to the GPU shader while providing matching analytical CPU water height queries.
    /// </summary>
    [ExecuteAlways]
    public class WaterSystem : MonoBehaviour
    {
        public static WaterSystem Instance { get; private set; }

        [Header("Ocean Swells (Gerstner Waves)")]
        [Tooltip("Dominant ocean wave amplitude (crest height).")]
        [Range(0.05f, 3.0f)]
        public float swellAmplitude1 = 1.0f;
        [Tooltip("Dominant ocean wavelength in meters.")]
        [Range(10f, 150f)]
        public float swellWavelength1 = 52f;
        [Tooltip("Speed of dominant ocean swells.")]
        [Range(0.2f, 5.0f)]
        public float swellSpeed1 = 1.30f;
        public Vector2 swellDirection1 = new Vector2(1f, 0.32f);
        [Range(0f, 1f)]
        [Tooltip("Steepness of wave crests (0 = sine wave, 1 = sharp crest).")]
        public float swellSteepness1 = 0.34f;

        [Header("Secondary Cross Swell")]
        [Range(0f, 2.0f)]
        public float swellAmplitude2 = 0.50f;
        [Range(5f, 80f)]
        public float swellWavelength2 = 31f;
        [Range(0.2f, 5.0f)]
        public float swellSpeed2 = 1.50f;
        public Vector2 swellDirection2 = new Vector2(0.58f, 0.82f);
        [Range(0f, 1f)]
        public float swellSteepness2 = 0.28f;

        [Header("Tertiary Swell")]
        [Range(0f, 1.5f)]
        public float swellAmplitude3 = 0.30f;
        [Range(3f, 50f)]
        public float swellWavelength3 = 16f;
        [Range(0.2f, 5.0f)]
        public float swellSpeed3 = 1.80f;
        public Vector2 swellDirection3 = new Vector2(-0.78f, 0.62f);
        [Range(0f, 1f)]
        public float swellSteepness3 = 0.22f;

        [Header("Fine Wind Chop")]
        [Range(0f, 0.3f)]
        public float chopAmplitude = 0.06f;
        [Range(1f, 20f)]
        public float chopWavelength = 6.0f;
        [Range(0.5f, 6.0f)]
        public float chopSpeed = 2.3f;

        [Header("Rogue Wave Sets")]
        [Tooltip("Extra primary-swell amplitude multiplier at the peak of a rare wave set (0 = uniform swells).")]
        [Range(0f, 5f)]
        public float waveSetBoost = 3.10f;
        [Tooltip("Spatial length of one wave-set envelope along the primary swell direction (meters).")]
        [Range(60f, 600f)]
        public float waveSetWavelength = 230f;
        [Tooltip("Travel speed (m/s) of wave sets along the primary swell direction.")]
        [Range(0f, 20f)]
        public float waveSetSpeed = 7f;

        [Header("Wave Height Limits")]
        [Tooltip("Soft vertical ceiling for dynamic movement, footstep and impact wake ripples (meters).")]
        [Range(0.05f, 4.5f)]
        public float maxRippleHeight = 2.8f;
        [Tooltip("Soft vertical ceiling for total wave displacement above mean ocean level (meters).")]
        [Range(0.25f, 12.0f)]
        public float maxTotalWaveHeight = 6.60f;
        [Tooltip("Soft vertical floor for wave troughs below mean ocean level (meters) so lowest point stays at knee level.")]
        [Range(0.5f, 5.0f)]
        public float maxTroughDepth = 2.25f;

        /// <summary>Mean (undisturbed) ocean surface Y.</summary>
        public float MeanSurfaceY => transform.position.y;

        /// <summary>Horizontal travel direction of the primary swell.</summary>
        public Vector2 DominantWaveDirection => swellDirection1.sqrMagnitude > 0.001f ? swellDirection1.normalized : Vector2.right;

        /// <summary>Phase speed (m/s) of the primary swell.</summary>
        public float DominantPhaseSpeed
        {
            get
            {
                float k = 2f * Mathf.PI / Mathf.Max(1f, swellWavelength1);
                return Mathf.Sqrt(9.81f / k) * swellSpeed1;
            }
        }

        [Header("Wake Propagation Settings")]
        [Tooltip("Legacy grid settings retained for inspector compatibility (waves now evaluate analytically on GPU).")]
        [Range(32, 160)]
        public int simResolution = 112;
        public float simAreaSize = 110f;
        [Range(0.05f, 0.38f)]
        public float waveSpeed = 0.26f;
        [Range(0.92f, 0.998f)]
        public float waveDamping = 0.982f;
        public bool absorbEdges = true;

        [Tooltip("Radial expansion speed (m/s) of analytical wake rings.")]
        public float wakeExpansionSpeed = 7.0f;
        [Tooltip("Duration (seconds) before a wake ring fades completely.")]
        public float wakeLifetime = 4.2f;

        [Header("Simulation Follows Target")]
        public Transform followTarget;

        private const int MAX_WAKES = 16;
        private readonly Vector4[] wakePositions = new Vector4[MAX_WAKES]; // xyz = worldPos, w = startTime
        private readonly Vector4[] wakeParams = new Vector4[MAX_WAKES];    // x = radius, y = strength, z = speed, w = invLifetime
        private int wakeCursor;

        // Cached Shader Property IDs (zero GC allocation per frame)
        private static readonly int IdSwell1 = Shader.PropertyToID("_OceanSwell1");
        private static readonly int IdSwell1Dir = Shader.PropertyToID("_OceanSwell1Dir");
        private static readonly int IdSwell2 = Shader.PropertyToID("_OceanSwell2");
        private static readonly int IdSwell2Dir = Shader.PropertyToID("_OceanSwell2Dir");
        private static readonly int IdSwell3 = Shader.PropertyToID("_OceanSwell3");
        private static readonly int IdSwell3Dir = Shader.PropertyToID("_OceanSwell3Dir");
        private static readonly int IdHeightLimits = Shader.PropertyToID("_OceanHeightLimits");
        private static readonly int IdWakePositions = Shader.PropertyToID("_OceanWakePositions");
        private static readonly int IdWakeParams = Shader.PropertyToID("_OceanWakeParams");

        void OnEnable()
        {
            if (Instance == null || Instance == this)
                Instance = this;

            InitializeSimulation();
            PushShaderGlobals(Application.isPlaying ? Time.time : 0f);
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        void Awake()
        {
            if (Instance == null)
                Instance = this;

            InitializeSimulation();
        }

        public static float SoftLimit(float value, float limit)
        {
            if (limit <= 0.001f) return 0f;
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            float scaled = value / limit;
            float soft = (float)System.Math.Tanh(scaled) * limit;
            return Mathf.Clamp(soft, -limit, limit);
        }

        public void InitializeSimulation()
        {
            for (int i = 0; i < MAX_WAKES; i++)
            {
                wakePositions[i] = new Vector4(0f, 0f, 0f, -100f);
                wakeParams[i] = Vector4.zero;
            }
            wakeCursor = 0;
        }

        void Update()
        {
            float currentTime = Application.isPlaying ? Time.time : 0f;
            PushShaderGlobals(currentTime);
        }

        private void PushShaderGlobals(float currentTime)
        {
            Vector2 d1 = swellDirection1.sqrMagnitude > 0.001f ? swellDirection1.normalized : Vector2.right;
            Vector2 d2 = swellDirection2.sqrMagnitude > 0.001f ? swellDirection2.normalized : Vector2.up;
            Vector2 d3 = swellDirection3.sqrMagnitude > 0.001f ? swellDirection3.normalized : new Vector2(-0.78f, 0.62f).normalized;

            Shader.SetGlobalVector(IdSwell1, new Vector4(swellAmplitude1, Mathf.Max(1f, swellWavelength1), swellSpeed1, swellSteepness1));
            Shader.SetGlobalVector(IdSwell1Dir, new Vector4(d1.x, d1.y, chopAmplitude, Mathf.Max(0.5f, chopWavelength)));

            Shader.SetGlobalVector(IdSwell2, new Vector4(swellAmplitude2, Mathf.Max(1f, swellWavelength2), swellSpeed2, swellSteepness2));
            Shader.SetGlobalVector(IdSwell2Dir, new Vector4(d2.x, d2.y, chopSpeed, waveSetSpeed));

            Shader.SetGlobalVector(IdSwell3, new Vector4(swellAmplitude3, Mathf.Max(1f, swellWavelength3), swellSpeed3, swellSteepness3));
            Shader.SetGlobalVector(IdSwell3Dir, new Vector4(d3.x, d3.y, waveSetBoost, Mathf.Max(60f, waveSetWavelength)));

            Shader.SetGlobalVector(IdHeightLimits, new Vector4(
                Mathf.Max(0.05f, maxRippleHeight),
                Mathf.Max(0.25f, maxTotalWaveHeight),
                currentTime,
                Mathf.Max(0.55f, maxTroughDepth)));

            Shader.SetGlobalVectorArray(IdWakePositions, wakePositions);
            Shader.SetGlobalVectorArray(IdWakeParams, wakeParams);
        }

        #region Ocean Swell Mathematics (8-Band Gerstner Spectrum, Spatial Phase Curl & Self-Rising Swells)

        /// <summary>
        /// Continuous 2D spatial turbulence warp that bends wave ridges organically,
        /// breaking straight infinite lines and eradicating repeating cushion/diamond grid patterns.
        /// </summary>
        public static float EvaluateSpatialPhaseCurl(float worldX, float worldZ, float time)
        {
            return Mathf.Sin(0.026f * worldX + 0.038f * worldZ + 0.21f * time) * 1.55f
                 + Mathf.Cos(0.034f * worldX - 0.029f * worldZ - 0.17f * time) * 1.25f
                 + Mathf.Sin(0.052f * worldX + 0.018f * worldZ + 0.31f * time) * 0.70f;
        }

        /// <summary>
        /// Dynamic 2D constructive wave packet envelope: localized swell groups stay mostly in
        /// the normal knee-to-pelvis range (~85%) and rarely surge up to upper-torso height (~10-12%).
        /// </summary>
        public static float EvaluateSelfRisingMultiplier(float worldX, float worldZ, float time, float boost)
        {
            if (boost <= 0.001f) return 1f;

            float p1 = Mathf.Sin(0.024f * (worldX * 0.91f + worldZ * 0.41f) - 0.42f * time);
            float p2 = Mathf.Cos(0.017f * (-worldX * 0.48f + worldZ * 0.88f) - 0.31f * time + 1.6f);
            float p3 = Mathf.Sin(0.011f * (worldX * 0.65f - worldZ * 0.76f) - 0.23f * time + 0.9f);
            float group = Mathf.Clamp01(0.5f + 0.5f * (p1 * 0.55f + p2 * 0.30f + p3 * 0.15f));

            float rogue = Mathf.Clamp01((group - 0.58f) / 0.42f);
            float rareEnvelope = rogue * rogue * rogue * (rogue * (rogue * 6f - 15f) + 10f);
            return Mathf.Lerp(0.75f, 1f + boost, rareEnvelope);
        }

        private static Vector3 EvaluateGerstnerComponent(
            float amplitude,
            float wavelength,
            float speed,
            Vector2 direction,
            float steepness,
            float warp,
            float worldX,
            float worldZ,
            float time)
        {
            if (amplitude <= 0.0001f) return Vector3.zero;

            Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            float k = 2f * Mathf.PI / Mathf.Max(1f, wavelength);
            float w = Mathf.Sqrt(9.81f * k) * speed;
            float phase = k * (worldX * dir.x + worldZ * dir.y) - (w * time) + warp;

            float sinP = Mathf.Sin(phase);
            float cosP = Mathf.Cos(phase);

            return new Vector3(
                dir.x * (steepness * amplitude * cosP),
                amplitude * sinP,
                dir.y * (steepness * amplitude * cosP)
            );
        }

        /// <summary>
        /// Backward-compatible wrapper for legacy callers.
        /// </summary>
        public float EvaluateWaveSetMultiplier(float worldX, float worldZ, float time)
        {
            return EvaluateSelfRisingMultiplier(worldX, worldZ, time, waveSetBoost);
        }

        /// <summary>
        /// Computes rich 8-band oceanic Gerstner wave spectrum with curl turbulence & self-rising swell groups.
        /// </summary>
        public Vector3 EvaluateOceanSwell(float worldX, float worldZ, float time)
        {
            float warp = EvaluateSpatialPhaseCurl(worldX, worldZ, time);
            float selfRise = EvaluateSelfRisingMultiplier(worldX, worldZ, time, waveSetBoost);
            float steepScale = 1f / Mathf.Sqrt(Mathf.Max(1f, selfRise));

            Vector3 disp = Vector3.zero;

            // Wave 1: Heavy Deep Ground Swell (surges tall in rare rogue wave groups)
            disp += EvaluateGerstnerComponent(swellAmplitude1 * selfRise, swellWavelength1, swellSpeed1,
                swellDirection1, swellSteepness1 * steepScale, warp * 0.90f, worldX, worldZ, time);

            // Wave 2: Storm Crest Swell (rises with rare rogue wave sets)
            disp += EvaluateGerstnerComponent(swellAmplitude2 * selfRise * 0.85f, swellWavelength2, swellSpeed2,
                swellDirection2, swellSteepness2 * steepScale, warp * 0.75f, worldX, worldZ, time);

            // Wave 3: Secondary Cross Swell
            disp += EvaluateGerstnerComponent(swellAmplitude3, swellWavelength3, swellSpeed3,
                swellDirection3, swellSteepness3, warp * 0.60f, worldX, worldZ, time);

            // Wave 4: Oblique Counter Sea
            disp += EvaluateGerstnerComponent(0.38f, 33.0f, 1.45f,
                new Vector2(0.978f, -0.208f), 0.38f, warp * 0.50f, worldX, worldZ, time);

            // Wave 5: Intermediate Wind Sea
            disp += EvaluateGerstnerComponent(0.26f, 21.5f, 1.62f,
                new Vector2(0.743f, 0.669f), 0.34f, warp * 0.40f, worldX, worldZ, time);

            // Wave 6: Short Choppy Sea
            disp += EvaluateGerstnerComponent(0.18f, 14.8f, 1.82f,
                new Vector2(0.766f, -0.643f), 0.30f, warp * 0.30f, worldX, worldZ, time);

            // Wave 7: Fine Wind Wave
            disp += EvaluateGerstnerComponent(0.12f, 9.6f, 2.08f,
                new Vector2(0.995f, 0.105f), 0.25f, warp * 0.20f, worldX, worldZ, time);

            // Wave 8: Capillary Chop
            disp += EvaluateGerstnerComponent(0.07f, 5.4f, 2.42f,
                new Vector2(0.819f, -0.574f), 0.20f, warp * 0.15f, worldX, worldZ, time);

            // Crest sharpening & trough deepening:
            // Pinches wave crests into sharp oceanic ridges while deepening normal troughs toward knee level
            if (disp.y > 0f)
            {
                float crestFactor = Mathf.Clamp01(disp.y / 2.2f);
                disp.y *= (1.0f + 0.30f * crestFactor);
            }
            else
            {
                disp.y *= 1.30f;
            }

            return disp;
        }

        /// <summary>
        /// Returns total local displacement (swells + wake ripples) softly capped to [-maxTroughDepth, +maxTotalWaveHeight].
        /// </summary>
        public Vector3 GetClampedWaveDisplacement(float worldX, float worldZ, float time)
        {
            Vector3 swell = EvaluateOceanSwell(worldX, worldZ, time);
            float ripple = SampleRippleHeight(worldX, worldZ);

            float totalY = swell.y + ripple;
            float heightLimit = Mathf.Max(0.25f, maxTotalWaveHeight);
            float troughLimit = Mathf.Max(0.55f, maxTroughDepth);
            float clampedY = totalY >= 0f ? SoftLimit(totalY, heightLimit) : SoftLimit(totalY, troughLimit);
            float horizLimit = Mathf.Min(heightLimit * 0.45f, 2.4f);

            return new Vector3(
                Mathf.Clamp(swell.x, -horizLimit, horizLimit),
                clampedY,
                Mathf.Clamp(swell.z, -horizLimit, horizLimit)
            );
        }

        #endregion

        #region Public Wake Generation (8-Slot Ring Buffer)

        /// <summary>
        /// Records a dynamic, propagating wake ripple into the GPU-evaluated 16-slot ring buffer.
        /// </summary>
        public void AddFootstepWave(Vector3 worldPosition, float radius, float strength)
        {
            if (!Application.isPlaying) return;

            float clampedStrength = Mathf.Clamp(strength, -maxRippleHeight, maxRippleHeight);
            float clampedRadius = Mathf.Clamp(radius, 2.0f, 32f);
            float invLifetime = 1f / Mathf.Max(0.5f, wakeLifetime);

            wakePositions[wakeCursor] = new Vector4(worldPosition.x, transform.position.y, worldPosition.z, Time.time);
            wakeParams[wakeCursor] = new Vector4(clampedRadius, clampedStrength, Mathf.Max(1.0f, wakeExpansionSpeed), invLifetime);
            wakeCursor = (wakeCursor + 1) % MAX_WAKES;
        }

        /// <summary>
        /// Emits a surging bow wake in front of a moving wading leg.
        /// </summary>
        public void AddWadingWake(Vector3 legPos, Vector3 legVelocity, float legWidth, float wakeStrength)
        {
            Vector3 horizVel = new Vector3(legVelocity.x, 0f, legVelocity.z);
            if (horizVel.sqrMagnitude < 0.1f) return;

            Vector3 moveDir = horizVel.normalized;
            float clampedStrength = Mathf.Clamp(wakeStrength, -maxRippleHeight, maxRippleHeight);
            AddFootstepWave(legPos + moveDir * (legWidth * 0.75f), legWidth * 2.2f, clampedStrength);
        }

        #endregion

        #region Public Queries

        /// <summary>
        /// Returns total height-capped water surface elevation (Y) at world (X, Z).
        /// Matches the GPU vertex shader analytically.
        /// </summary>
        public float GetWaterHeight(Vector3 worldPos)
        {
            float time = Application.isPlaying ? Time.time : 0f;
            Vector3 disp = GetClampedWaveDisplacement(worldPos.x, worldPos.z, time);

            float total = transform.position.y + disp.y;
            return (float.IsNaN(total) || float.IsInfinity(total)) ? transform.position.y : total;
        }

        /// <summary>
        /// Analytically evaluates the 16 active wake slots at world (X, Z).
        /// </summary>
        public float SampleRippleHeight(float worldX, float worldZ)
        {
            if (!Application.isPlaying) return 0f;

            float time = Time.time;
            float sum = 0f;

            for (int i = 0; i < MAX_WAKES; i++)
            {
                Vector4 posTime = wakePositions[i];
                Vector4 p = wakeParams[i];
                float age = time - posTime.w;
                float normalizedAge = age * p.w;

                if (age <= 0f || normalizedAge >= 1f || Mathf.Abs(p.y) < 0.0001f)
                    continue;

                float timeDecay = 1f - normalizedAge;
                timeDecay *= timeDecay;

                float dx = worldX - posTime.x;
                float dz = worldZ - posTime.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                float radius = Mathf.Max(2.0f, p.x);
                float waveFront = age * p.z;
                float ringWidth = Mathf.Max(2.2f, radius * 0.45f);
                float dRing = (dist - waveFront) / ringWidth;

                float radialEnv = Mathf.Exp(-dRing * dRing) * Mathf.Exp(-dist / Mathf.Max(radius * 3.0f, 8f));
                sum += Mathf.Cos(dRing * 3.4f) * radialEnv * timeDecay * p.y;
            }

            return SoftLimit(sum, maxRippleHeight);
        }

        /// <summary>
        /// Returns analytical surface normal for ocean lighting, buoyancy tilting, and spray orientation.
        /// </summary>
        public Vector3 GetWaterNormal(Vector3 worldPos)
        {
            float eps = 0.4f;
            float hL = GetWaterHeight(worldPos - Vector3.right * eps);
            float hR = GetWaterHeight(worldPos + Vector3.right * eps);
            float hD = GetWaterHeight(worldPos - Vector3.forward * eps);
            float hU = GetWaterHeight(worldPos + Vector3.forward * eps);

            return new Vector3(-(hR - hL) / (2f * eps), 1f, -(hU - hD) / (2f * eps)).normalized;
        }

        public bool IsSubmerged(Vector3 worldPos, out float depth)
        {
            float surfaceY = GetWaterHeight(worldPos);
            depth = surfaceY - worldPos.y;
            return depth > 0f;
        }

        #endregion

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.35f);
            Vector3 center = followTarget != null ? followTarget.position : transform.position;
            center.y = transform.position.y;
            Gizmos.DrawWireCube(center, new Vector3(simAreaSize, maxTotalWaveHeight * 2f, simAreaSize));
        }
    }
}
