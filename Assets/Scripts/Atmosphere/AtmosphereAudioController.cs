using UnityEngine;
using PacificRimWater;

namespace PacificRimAtmosphere
{
    /// <summary>
    /// Manages the heavy industrial storm audio scape and dynamic low-pass frequency filtering:
    /// - Procedural scheduling of distant coastal sirens, foghorn blasts, and groaning metal stress.
    /// - Generates synthetic procedural audio clips on-the-fly when custom audio assets are not provided.
    /// - Dynamically drives an AudioLowPassFilter on the camera to muffle high frequencies
    ///   following thunderclaps/sub-bass impacts and when near the water surface.
    /// </summary>
    [ExecuteAlways]
    public class AtmosphereAudioController : MonoBehaviour
    {
        public static AtmosphereAudioController Instance { get; private set; }

        [Header("Audio Sources")]
        public AudioSource sirenSource;
        public AudioSource foghornSource;
        public AudioSource metalCreakSource;

        [Header("Custom Audio Clips (Optional)")]
        public AudioClip customSirenClip;
        public AudioClip customFoghornClip;
        public AudioClip customMetalCreakClip;

        [Header("Scheduling Intervals")]
        public float minSirenInterval = 28f;
        public float maxSirenInterval = 55f;
        public float minFoghornInterval = 18f;
        public float maxFoghornInterval = 38f;
        public float minMetalCreakInterval = 12f;
        public float maxMetalCreakInterval = 26f;

        [Header("Dynamic Audio Low-Pass Filter")]
        public bool enableDynamicLowPass = true;
        public AudioLowPassFilter cameraLowPassFilter;
        public float normalCutoffFrequency = 22000f;
        public float thunderDeafeningCutoff = 550f;
        public float waterProximityCutoff = 800f;
        public float lowPassRecoverySpeed = 1.85f;
        public float waterSurfaceMuffleThreshold = 3.8f;

        private float nextSirenTime;
        private float nextFoghornTime;
        private float nextMetalTime;

        private float currentLowPassCutoff = 22000f;
        private float targetLowPassCutoff = 22000f;
        private float muffleHoldTimer = 0f;

        private AudioClip proceduralSirenClip;
        private AudioClip proceduralFoghornClip;
        private AudioClip proceduralMetalCreakClip;

        void Awake()
        {
            EnsureAudioSources();
            EnsureProceduralClips();
        }

        void OnEnable()
        {
            if (Instance == null || Instance == this)
                Instance = this;

            EnsureAudioSources();
            EnsureProceduralClips();
            EnsureCameraLowPassFilter();

            ScheduleEvents();
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;

            if (cameraLowPassFilter != null)
            {
                cameraLowPassFilter.cutoffFrequency = normalCutoffFrequency;
            }
        }

        private void ScheduleEvents()
        {
            nextSirenTime = Time.time + Random.Range(minSirenInterval * 0.4f, minSirenInterval);
            nextFoghornTime = Time.time + Random.Range(minFoghornInterval * 0.3f, minFoghornInterval);
            nextMetalTime = Time.time + Random.Range(minMetalCreakInterval * 0.3f, minMetalCreakInterval);
        }

        public void EnsureAudioSources()
        {
            if (sirenSource == null) sirenSource = CreateAudioSource("SirenSource", 0.45f);
            if (foghornSource == null) foghornSource = CreateAudioSource("FoghornSource", 0.65f);
            if (metalCreakSource == null) metalCreakSource = CreateAudioSource("MetalCreakSource", 0.50f);
        }

        private AudioSource CreateAudioSource(string childName, float volume)
        {
            Transform existing = transform.Find(childName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(childName);
            go.transform.SetParent(transform, false);

            var src = go.GetComponent<AudioSource>();
            if (src == null) src = go.AddComponent<AudioSource>();

            src.playOnAwake = false;
            src.spatialBlend = 0.65f;
            src.volume = volume;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 35f;
            src.maxDistance = 500f;
            return src;
        }

        private void EnsureCameraLowPassFilter()
        {
            if (cameraLowPassFilter != null) return;

            Camera cam = Camera.main;
            if (cam != null)
            {
                cameraLowPassFilter = cam.GetComponent<AudioLowPassFilter>();
                if (cameraLowPassFilter == null)
                {
                    cameraLowPassFilter = cam.gameObject.AddComponent<AudioLowPassFilter>();
                }
                cameraLowPassFilter.cutoffFrequency = normalCutoffFrequency;
            }
        }

        void Update()
        {
            if (!Application.isPlaying) return;

            if (Time.time >= nextSirenTime)
            {
                PlaySiren();
                nextSirenTime = Time.time + Random.Range(minSirenInterval, maxSirenInterval);
            }

            if (Time.time >= nextFoghornTime)
            {
                PlayFoghorn();
                nextFoghornTime = Time.time + Random.Range(minFoghornInterval, maxFoghornInterval);
            }

            if (Time.time >= nextMetalTime)
            {
                PlayMetalCreak();
                nextMetalTime = Time.time + Random.Range(minMetalCreakInterval, maxMetalCreakInterval);
            }

            UpdateLowPassFilter();
        }

        public void PlaySiren()
        {
            if (customSirenClip == null) return; // Do not play procedural temporary placeholder SFX
            AudioClip clip = customSirenClip;
            if (clip != null && sirenSource != null)
            {
                sirenSource.pitch = Random.Range(0.92f, 1.05f);
                sirenSource.PlayOneShot(clip, sirenSource.volume);
            }
        }

        public void PlayFoghorn()
        {
            if (customFoghornClip == null) return; // Do not play procedural temporary placeholder SFX
            AudioClip clip = customFoghornClip;
            if (clip != null && foghornSource != null)
            {
                foghornSource.pitch = Random.Range(0.88f, 1.04f);
                foghornSource.PlayOneShot(clip, foghornSource.volume);
            }
        }

        public void PlayMetalCreak()
        {
            if (customMetalCreakClip == null) return; // Do not play procedural temporary placeholder SFX
            AudioClip clip = customMetalCreakClip;
            if (clip != null && metalCreakSource != null)
            {
                metalCreakSource.pitch = Random.Range(0.85f, 1.15f);
                metalCreakSource.PlayOneShot(clip, metalCreakSource.volume);
            }
        }

        public void TriggerThunderMuffle(float muffleStrength = 0.85f, float duration = 0.75f)
        {
            targetLowPassCutoff = Mathf.Lerp(normalCutoffFrequency, thunderDeafeningCutoff, Mathf.Clamp01(muffleStrength));
            muffleHoldTimer = Mathf.Max(muffleHoldTimer, duration);
        }

        public void TriggerSubBassMuffle(float muffleStrength = 0.70f, float duration = 0.60f)
        {
            targetLowPassCutoff = Mathf.Lerp(normalCutoffFrequency, thunderDeafeningCutoff * 1.35f, Mathf.Clamp01(muffleStrength));
            muffleHoldTimer = Mathf.Max(muffleHoldTimer, duration);
        }

        private void UpdateLowPassFilter()
        {
            if (!enableDynamicLowPass) return;
            EnsureCameraLowPassFilter();
            if (cameraLowPassFilter == null) return;

            float dt = Time.deltaTime;

            if (muffleHoldTimer > 0f)
            {
                muffleHoldTimer -= dt;
            }
            else
            {
                targetLowPassCutoff = normalCutoffFrequency;
            }

            Camera cam = Camera.main;
            if (cam != null && WaterSystem.Instance != null)
            {
                Vector3 camPos = cam.transform.position;
                float seaY = WaterSystem.Instance.GetWaterHeight(camPos);
                float distToSea = camPos.y - seaY;

                if (distToSea < waterSurfaceMuffleThreshold)
                {
                    float factor = Mathf.Clamp01((waterSurfaceMuffleThreshold - distToSea) / waterSurfaceMuffleThreshold);
                    float surfaceCutoff = Mathf.Lerp(normalCutoffFrequency, waterProximityCutoff, factor);
                    targetLowPassCutoff = Mathf.Min(targetLowPassCutoff, surfaceCutoff);
                }
            }

            currentLowPassCutoff = Mathf.Lerp(currentLowPassCutoff, targetLowPassCutoff, dt * lowPassRecoverySpeed);
            cameraLowPassFilter.cutoffFrequency = currentLowPassCutoff;
        }

        #region Procedural Audio Synthesizers

        private void EnsureProceduralClips()
        {
            if (proceduralSirenClip == null) proceduralSirenClip = GenerateProceduralSirenClip();
            if (proceduralFoghornClip == null) proceduralFoghornClip = GenerateProceduralFoghornClip();
            if (proceduralMetalCreakClip == null) proceduralMetalCreakClip = GenerateProceduralMetalCreakClip();
        }

        private AudioClip GenerateProceduralSirenClip()
        {
            int sampleRate = 44100;
            float duration = 4.2f;
            int count = (int)(sampleRate * duration);
            float[] samples = new float[count];

            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(440f, 720f, 0.5f + 0.5f * Mathf.Sin(t * 1.8f));
                phase += 2f * Mathf.PI * freq / sampleRate;

                float envelope = Mathf.Sin(Mathf.PI * (t / duration));
                float wave = Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 2f) * 0.25f + Mathf.Sin(phase * 3f) * 0.15f;
                samples[i] = wave * envelope * 0.45f;
            }

            var clip = AudioClip.Create("Proc_CoastalSiren", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateProceduralFoghornClip()
        {
            int sampleRate = 44100;
            float duration = 3.5f;
            int count = (int)(sampleRate * duration);
            float[] samples = new float[count];

            float phase = 0f;
            float f0 = 74.0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                phase += 2f * Mathf.PI * f0 / sampleRate;

                float attack = Mathf.Clamp01(t / 0.4f);
                float decay = Mathf.Clamp01((duration - t) / 0.8f);
                float envelope = attack * decay;

                float wave = Mathf.Sin(phase) * 0.55f
                           + Mathf.Sin(phase * 2f) * 0.30f
                           + Mathf.Sin(phase * 3f) * 0.15f
                           + Mathf.Sin(phase * 4f) * 0.08f;

                samples[i] = wave * envelope * 0.65f;
            }

            var clip = AudioClip.Create("Proc_OceanFoghorn", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateProceduralMetalCreakClip()
        {
            int sampleRate = 44100;
            float duration = 2.4f;
            int count = (int)(sampleRate * duration);
            float[] samples = new float[count];

            var rng = new System.Random(1337);
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float freqMod = 95f + Mathf.Sin(t * 8.5f) * 35f;
                phase += 2f * Mathf.PI * freqMod / sampleRate;

                float envelope = Mathf.Sin(Mathf.PI * (t / duration));
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.12f;
                float wave = (Mathf.Sin(phase) * 0.5f + Mathf.Sin(phase * 1.5f) * 0.3f + noise);

                samples[i] = wave * envelope * 0.45f;
            }

            var clip = AudioClip.Create("Proc_MetalCreak", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
