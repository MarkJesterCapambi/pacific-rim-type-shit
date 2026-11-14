using UnityEngine;
using PacificRimWater;

namespace PacificRimAtmosphere
{
    /// <summary>
    /// Modular camera trauma & heavy impact controller.
    /// Routes impact/lightning trauma cleanly into the single PacificRimCameraShake system
    /// on the Main Camera so multiple camera shakes never stack or fight each other.
    /// Periodic random sub-bass shakes are disabled by default to avoid unprompted shaking.
    /// </summary>
    [ExecuteAlways]
    public class HeavyImpactCameraTraumaController : MonoBehaviour
    {
        public static HeavyImpactCameraTraumaController Instance { get; private set; }

        [Header("Trauma Settings")]
        [Range(0f, 1f)]
        public float currentTrauma = 0f;
        [Tooltip("Trauma recovery rate per second (linear decay).")]
        public float traumaDecayRate = 1.4f;

        [Header("Maximum Fallback Displacement Limits (Used only if PacificRimCameraShake is absent)")]
        public float maxTranslationX = 0.40f;
        public float maxTranslationY = 0.55f;
        public float maxTranslationZ = 0.35f;
        public float maxPitch = 2.0f;
        public float maxYaw = 2.2f;
        public float maxRoll = 2.4f;

        [Header("Frequency & Shake Tuning")]
        public float frequency = 16f;
        public int traumaPower = 2; // Trauma^2

        [Header("Periodic Sub-Bass Impacts (Disabled by Default)")]
        [Tooltip("Optional periodic distant Kaiju tremors. Disabled by default to prevent unprompted shakes.")]
        public bool enablePeriodicSubBassImpact = false;
        public float minSubBassInterval = 18.0f;
        public float maxSubBassInterval = 30.0f;
        [Range(0.1f, 1.0f)]
        public float subBassTrauma = 0.40f;
        public float subBassShakeDuration = 0.50f;

        [Header("Lightning Hook")]
        public bool hookLightningStrikes = true;
        [Range(0.1f, 1.0f)]
        public float closeLightningTrauma = 0.55f;
        [Range(0.05f, 0.5f)]
        public float midLightningTrauma = 0.22f;

        public Vector3 CurrentLocalPositionOffset { get; private set; }
        public Vector3 CurrentLocalRotationOffset { get; private set; }

        private float nextSubBassTime = 0f;
        private float noiseSeed;
        private Camera targetCamera;
        private PacificRimCameraShake primaryShaker;

        void Awake()
        {
            noiseSeed = Random.Range(0f, 1000f);
        }

        void OnEnable()
        {
            if (Instance == null || Instance == this)
                Instance = this;

            ResolveCameraAndShaker();
            ScheduleNextSubBass();
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;

            CurrentLocalPositionOffset = Vector3.zero;
            CurrentLocalRotationOffset = Vector3.zero;
        }

        private void ResolveCameraAndShaker()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (targetCamera == null) targetCamera = Camera.main;

            if (primaryShaker == null && targetCamera != null)
            {
                primaryShaker = targetCamera.GetComponent<PacificRimCameraShake>();
            }
        }

        public void AddTrauma(float amount, float duration = 0.40f)
        {
            ResolveCameraAndShaker();
            if (primaryShaker != null)
            {
                primaryShaker.Shake(Mathf.Clamp01(amount) * 0.75f, duration);
                CurrentLocalPositionOffset = Vector3.zero;
                CurrentLocalRotationOffset = Vector3.zero;
                return;
            }

            currentTrauma = Mathf.Clamp01(currentTrauma + amount);
        }

        private void ScheduleNextSubBass()
        {
            float minI = Mathf.Max(5f, minSubBassInterval);
            float maxI = Mathf.Max(minI, maxSubBassInterval);
            nextSubBassTime = Time.time + Random.Range(minI, maxI);
        }

        void Update()
        {
            ResolveCameraAndShaker();

            float dt = Application.isPlaying ? Time.unscaledDeltaTime : 0.016f;
            if (currentTrauma > 0f)
            {
                currentTrauma = Mathf.Max(0f, currentTrauma - traumaDecayRate * dt);
            }

            if (Application.isPlaying && enablePeriodicSubBassImpact && Time.time >= nextSubBassTime)
            {
                TriggerSubBassImpact();
                ScheduleNextSubBass();
            }

            // When the single PacificRimCameraShake system is active on Main Camera, never emit a duplicate second offset
            if (!Application.isPlaying || primaryShaker != null)
            {
                CurrentLocalPositionOffset = Vector3.zero;
                CurrentLocalRotationOffset = Vector3.zero;
                return;
            }

            float shake = Mathf.Pow(currentTrauma, traumaPower);
            if (shake > 0.0001f)
            {
                float time = (Time.time + noiseSeed) * frequency;

                float nx = (Mathf.PerlinNoise(time, 0f) * 2f - 1f) * maxTranslationX * shake;
                float ny = (Mathf.PerlinNoise(0f, time * 1.15f) * 2f - 1f) * maxTranslationY * shake;
                float nz = (Mathf.PerlinNoise(time * 0.85f, time * 0.85f) * 2f - 1f) * maxTranslationZ * shake;

                float pitch = (Mathf.PerlinNoise(time * 1.1f, 17.3f) * 2f - 1f) * maxPitch * shake;
                float yaw = (Mathf.PerlinNoise(23.7f, time * 0.95f) * 2f - 1f) * maxYaw * shake;
                float roll = (Mathf.PerlinNoise(time * 1.3f, 41.2f) * 2f - 1f) * maxRoll * shake;

                CurrentLocalPositionOffset = new Vector3(nx, ny, nz);
                CurrentLocalRotationOffset = new Vector3(pitch, yaw, roll);
            }
            else
            {
                CurrentLocalPositionOffset = Vector3.zero;
                CurrentLocalRotationOffset = Vector3.zero;
            }
        }

        [ContextMenu("Trigger Sub-Bass Impact")]
        public void TriggerSubBassImpact()
        {
            AddTrauma(subBassTrauma, subBassShakeDuration);

            if (AtmosphereAudioController.Instance != null)
            {
                AtmosphereAudioController.Instance.TriggerSubBassMuffle(0.80f, 0.55f);
            }

            if (WaterSystem.Instance != null && targetCamera != null)
            {
                Vector3 camPos = targetCamera.transform.position;
                Vector3 waveOrigin = camPos + targetCamera.transform.forward * 40f;
                waveOrigin.y = WaterSystem.Instance.GetWaterHeight(waveOrigin);
                WaterSystem.Instance.AddFootstepWave(waveOrigin, 36f, 0.35f);
            }
        }

        public void OnLightningStruck(Vector3 strikePos, float distanceToPlayer)
        {
            if (!hookLightningStrikes) return;

            if (distanceToPlayer < 65f)
            {
                float distFactor = Mathf.Clamp01(1f - distanceToPlayer / 75f);
                AddTrauma(closeLightningTrauma * Mathf.Lerp(0.55f, 1.0f, distFactor), 0.38f);
            }
            else if (distanceToPlayer < 130f)
            {
                AddTrauma(midLightningTrauma, 0.25f);
            }
        }
    }
}
