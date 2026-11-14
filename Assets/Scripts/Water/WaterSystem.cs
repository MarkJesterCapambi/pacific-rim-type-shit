using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Pacific Rim Ocean Wave System.
    /// Combines deep oceanic Gerstner swells with a real-time 2D wave equation heightfield
    /// for massive tidal ripples, footstep shockwaves, and plowing wakes from giant mechs.
    /// </summary>
    [ExecuteAlways]
    public class WaterSystem : MonoBehaviour
    {
        public static WaterSystem Instance { get; private set; }

        [Header("Ocean Swells (Gerstner Waves)")]
        [Tooltip("Dominant ocean wave amplitude (crest height).")]
        [Range(0.1f, 3.0f)]
        public float swellAmplitude1 = 0.85f;
        [Tooltip("Dominant ocean wavelength in meters.")]
        [Range(10f, 150f)]
        public float swellWavelength1 = 36f;
        [Tooltip("Speed of dominant ocean swells.")]
        [Range(0.2f, 5.0f)]
        public float swellSpeed1 = 1.4f;
        public Vector2 swellDirection1 = new Vector2(1f, 0.25f);
        [Range(0f, 1f)]
        [Tooltip("Steepness of wave crests (0 = sine wave, 1 = sharp crest).")]
        public float swellSteepness1 = 0.5f;

        [Header("Secondary Cross Swell")]
        [Range(0f, 2.0f)]
        public float swellAmplitude2 = 0.45f;
        [Range(5f, 80f)]
        public float swellWavelength2 = 18f;
        [Range(0.2f, 5.0f)]
        public float swellSpeed2 = 1.8f;
        public Vector2 swellDirection2 = new Vector2(0.3f, 1f);
        [Range(0f, 1f)]
        public float swellSteepness2 = 0.4f;

        [Header("Fine Wind Chop")]
        [Range(0f, 0.5f)]
        public float chopAmplitude = 0.15f;
        [Range(1f, 20f)]
        public float chopWavelength = 6f;
        [Range(0.5f, 6.0f)]
        public float chopSpeed = 2.5f;

        [Header("Interactive Wave Simulation (2D Wave Equation)")]
        [Tooltip("Grid resolution for real-time interactive footstep waves.")]
        [Range(32, 160)]
        public int simResolution = 96;
        [Tooltip("Physical dimension (meters) of the interactive wave simulation zone.")]
        public float simAreaSize = 80f;
        [Tooltip("Speed at which footstep ripples propagate across the ocean surface.")]
        [Range(0.1f, 0.45f)]
        public float waveSpeed = 0.32f;
        [Tooltip("Damping factor for ripples (closer to 1.0 = ripples travel further).")]
        [Range(0.92f, 0.998f)]
        public float waveDamping = 0.985f;
        [Tooltip("Dampen waves at borders of the simulation zone.")]
        public bool absorbEdges = true;

        [Header("Simulation Follows Target")]
        [Tooltip("Optional transform (e.g. Jaeger) that the interactive wave grid follows in the world.")]
        public Transform followTarget;

        // Wave simulation heightfield buffers
        private float[,] currentHeights;
        private float[,] previousHeights;
        private Vector3 simCenter;
        private float cellSpacing;
        private float simStepAccumulator;
        private const float FIXED_SIM_DT = 0.02f;

        void OnEnable()
        {
            if (Instance == null || Instance == this)
                Instance = this;

            InitializeSimulation();
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

        public void InitializeSimulation()
        {
            simResolution = Mathf.Clamp(simResolution, 32, 160);
            currentHeights = new float[simResolution, simResolution];
            previousHeights = new float[simResolution, simResolution];
            cellSpacing = simAreaSize / (simResolution - 1);
            simCenter = followTarget != null ? followTarget.position : transform.position;
            simCenter.y = transform.position.y;
        }

        void Update()
        {
            if (currentHeights == null || currentHeights.GetLength(0) != simResolution)
            {
                InitializeSimulation();
            }

            // Follow target in world space (snapped to cell spacing to prevent vertex shimmering)
            if (followTarget != null)
            {
                Vector3 targetPos = followTarget.position;
                float snappedX = Mathf.Round(targetPos.x / cellSpacing) * cellSpacing;
                float snappedZ = Mathf.Round(targetPos.z / cellSpacing) * cellSpacing;
                simCenter = new Vector3(snappedX, transform.position.y, snappedZ);
            }

            float dt = Application.isPlaying ? Time.deltaTime : 0.02f;
            simStepAccumulator += dt;

            while (simStepAccumulator >= FIXED_SIM_DT)
            {
                StepWaveEquation();
                simStepAccumulator -= FIXED_SIM_DT;
            }
        }

        /// <summary>
        /// Solves discrete 2D Laplacian wave equation across the simulation heightfield.
        /// </summary>
        private void StepWaveEquation()
        {
            if (currentHeights == null || previousHeights == null) return;

            float d = waveDamping;

            for (int z = 1; z < simResolution - 1; z++)
            {
                for (int x = 1; x < simResolution - 1; x++)
                {
                    float current = currentHeights[x, z];
                    float neighbors = currentHeights[x - 1, z]
                                    + currentHeights[x + 1, z]
                                    + currentHeights[x, z - 1]
                                    + currentHeights[x, z + 1];

                    float newHeight = (neighbors * 0.5f - previousHeights[x, z]) * d;
                    previousHeights[x, z] = current;
                    currentHeights[x, z] = newHeight;
                }
            }

            if (absorbEdges)
            {
                for (int i = 0; i < simResolution; i++)
                {
                    currentHeights[i, 0] *= 0.6f;
                    currentHeights[i, simResolution - 1] *= 0.6f;
                    currentHeights[0, i] *= 0.6f;
                    currentHeights[simResolution - 1, i] *= 0.6f;
                }
            }
        }

        #region Ocean Swell Mathematics (Gerstner Waves)

        /// <summary>
        /// Computes oceanic swell displacement at world coordinate (X, Z) at time T.
        /// </summary>
        public Vector3 EvaluateOceanSwell(float worldX, float worldZ, float time)
        {
            Vector3 disp = Vector3.zero;

            // Wave 1: Primary Swell
            if (swellAmplitude1 > 0f)
            {
                Vector2 dir = swellDirection1.sqrMagnitude > 0.001f ? swellDirection1.normalized : Vector2.right;
                float k = 2f * Mathf.PI / Mathf.Max(1f, swellWavelength1);
                float w = Mathf.Sqrt(9.81f * k) * swellSpeed1;
                float phase = k * (worldX * dir.x + worldZ * dir.y) - (w * time);

                float sinP = Mathf.Sin(phase);
                float cosP = Mathf.Cos(phase);

                disp.y += swellAmplitude1 * sinP;
                disp.x += dir.x * (swellSteepness1 * swellAmplitude1 * cosP);
                disp.z += dir.y * (swellSteepness1 * swellAmplitude1 * cosP);
            }

            // Wave 2: Secondary Cross Swell
            if (swellAmplitude2 > 0f)
            {
                Vector2 dir = swellDirection2.sqrMagnitude > 0.001f ? swellDirection2.normalized : Vector2.up;
                float k = 2f * Mathf.PI / Mathf.Max(1f, swellWavelength2);
                float w = Mathf.Sqrt(9.81f * k) * swellSpeed2;
                float phase = k * (worldX * dir.x + worldZ * dir.y) - (w * time);

                float sinP = Mathf.Sin(phase);
                float cosP = Mathf.Cos(phase);

                disp.y += swellAmplitude2 * sinP;
                disp.x += dir.x * (swellSteepness2 * swellAmplitude2 * cosP);
                disp.z += dir.y * (swellSteepness2 * swellAmplitude2 * cosP);
            }

            // Wave 3: Fine Wind Chop
            if (chopAmplitude > 0f)
            {
                float k = 2f * Mathf.PI / Mathf.Max(0.5f, chopWavelength);
                float phase = k * (worldX * 0.707f + worldZ * 0.707f) - (chopSpeed * time);
                disp.y += Mathf.Sin(phase) * chopAmplitude;
            }

            return disp;
        }

        #endregion

        #region Public Wave Generation (Footsteps, Splashes, Wakes)

        /// <summary>
        /// Injects a heavy footstep impact wave into the ocean surface.
        /// Produces a deep crater depression and an expanding tidal crest ring.
        /// </summary>
        /// <param name="worldPosition">Contact location of the foot</param>
        /// <param name="radius">Radius of the shockwave in meters (e.g. 3m - 8m for a Jaeger)</param>
        /// <param name="strength">Strength of the displacement</param>
        public void AddFootstepWave(Vector3 worldPosition, float radius, float strength)
        {
            if (currentHeights == null) return;

            float halfSize = simAreaSize * 0.5f;
            float localX = worldPosition.x - (simCenter.x - halfSize);
            float localZ = worldPosition.z - (simCenter.z - halfSize);

            if (localX < -radius || localX > simAreaSize + radius ||
                localZ < -radius || localZ > simAreaSize + radius)
                return;

            int centerIdxX = Mathf.RoundToInt((localX / simAreaSize) * (simResolution - 1));
            int centerIdxZ = Mathf.RoundToInt((localZ / simAreaSize) * (simResolution - 1));

            int cellR = Mathf.CeilToInt((radius / simAreaSize) * (simResolution - 1));
            int minX = Mathf.Clamp(centerIdxX - cellR, 1, simResolution - 2);
            int maxX = Mathf.Clamp(centerIdxX + cellR, 1, simResolution - 2);
            int minZ = Mathf.Clamp(centerIdxZ - cellR, 1, simResolution - 2);
            int maxZ = Mathf.Clamp(centerIdxZ + cellR, 1, simResolution - 2);

            float rSq = radius * radius;

            for (int z = minZ; z <= maxZ; z++)
            {
                float worldPosZ = (simCenter.z - halfSize) + (z * cellSpacing);
                float dz = worldPosZ - worldPosition.z;

                for (int x = minX; x <= maxX; x++)
                {
                    float worldPosX = (simCenter.x - halfSize) + (x * cellSpacing);
                    float dx = worldPosX - worldPosition.x;
                    float distSq = dx * dx + dz * dz;

                    if (distSq < rSq)
                    {
                        float dist = Mathf.Sqrt(distSq);
                        // Dome-and-crest footstep profile: center depression + surrounding surge ring
                        float normDist = dist / radius;
                        float profile = -Mathf.Cos(normDist * Mathf.PI); // -1 at center, +1 at edge
                        float falloff = 1f - normDist;

                        currentHeights[x, z] += strength * profile * falloff;
                    }
                }
            }
        }

        /// <summary>
        /// Injects an advancing bow wake in front of a giant wading leg.
        /// </summary>
        public void AddWadingWake(Vector3 legPos, Vector3 legVelocity, float legWidth, float wakeStrength)
        {
            if (legVelocity.sqrMagnitude < 0.1f) return;

            Vector3 moveDir = legVelocity.normalized;
            Vector3 bowWavePos = legPos + moveDir * (legWidth * 0.8f);
            Vector3 lateralLeft = legPos - Vector3.Cross(moveDir, Vector3.up) * (legWidth * 0.7f);
            Vector3 lateralRight = legPos + Vector3.Cross(moveDir, Vector3.up) * (legWidth * 0.7f);

            // Forward surge crest
            AddFootstepWave(bowWavePos, legWidth * 1.2f, wakeStrength * 0.7f);
            // Lateral parting wave crests
            AddFootstepWave(lateralLeft, legWidth * 0.9f, wakeStrength * 0.5f);
            AddFootstepWave(lateralRight, legWidth * 0.9f, wakeStrength * 0.5f);
            // Trailing depression wake
            AddFootstepWave(legPos - moveDir * legWidth, legWidth, -wakeStrength * 0.4f);
        }

        #endregion

        #region Public Queries

        /// <summary>
        /// Returns total water surface height (swells + dynamic footstep ripples) at world (X, Z).
        /// </summary>
        public float GetWaterHeight(Vector3 worldPos)
        {
            float time = Application.isPlaying ? Time.time : 0f;
            Vector3 swell = EvaluateOceanSwell(worldPos.x, worldPos.z, time);
            float ripple = SampleRippleHeight(worldPos.x, worldPos.z);

            return transform.position.y + swell.y + ripple;
        }

        /// <summary>
        /// Samples dynamic ripple elevation using bilinear interpolation.
        /// </summary>
        public float SampleRippleHeight(float worldX, float worldZ)
        {
            if (currentHeights == null) return 0f;

            float halfSize = simAreaSize * 0.5f;
            float localX = worldX - (simCenter.x - halfSize);
            float localZ = worldZ - (simCenter.z - halfSize);

            if (localX < 0f || localX >= simAreaSize || localZ < 0f || localZ >= simAreaSize)
                return 0f;

            float normX = (localX / simAreaSize) * (simResolution - 1);
            float normZ = (localZ / simAreaSize) * (simResolution - 1);

            int x0 = Mathf.Clamp(Mathf.FloorToInt(normX), 0, simResolution - 2);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(normZ), 0, simResolution - 2);
            int x1 = x0 + 1;
            int z1 = z0 + 1;

            float tx = normX - x0;
            float tz = normZ - z0;

            float h00 = currentHeights[x0, z0];
            float h10 = currentHeights[x1, z0];
            float h01 = currentHeights[x0, z1];
            float h11 = currentHeights[x1, z1];

            float top = Mathf.Lerp(h00, h10, tx);
            float bottom = Mathf.Lerp(h01, h11, tx);

            return Mathf.Lerp(top, bottom, tz);
        }

        /// <summary>
        /// Returns surface normal for ocean lighting, buoyancy tilting, and spray orientation.
        /// </summary>
        public Vector3 GetWaterNormal(Vector3 worldPos)
        {
            float eps = 0.35f;
            float hL = GetWaterHeight(worldPos - Vector3.right * eps);
            float hR = GetWaterHeight(worldPos + Vector3.right * eps);
            float hD = GetWaterHeight(worldPos - Vector3.forward * eps);
            float hU = GetWaterHeight(worldPos + Vector3.forward * eps);

            return new Vector3(-(hR - hL) / (2f * eps), 1f, -(hU - hD) / (2f * eps)).normalized;
        }

        /// <summary>
        /// Checks if a position is submerged under water.
        /// </summary>
        public bool IsSubmerged(Vector3 worldPos, out float depth)
        {
            float surfaceY = GetWaterHeight(worldPos);
            depth = surfaceY - worldPos.y;
            return depth > 0f;
        }

        #endregion

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
            Vector3 center = simCenter;
            center.y = transform.position.y;
            Gizmos.DrawWireCube(center, new Vector3(simAreaSize, 4f, simAreaSize));
        }
    }
}
