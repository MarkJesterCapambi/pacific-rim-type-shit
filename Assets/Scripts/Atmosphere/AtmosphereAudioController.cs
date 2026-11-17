using UnityEngine;
using PacificRimWater;

namespace PacificRimAtmosphere
{
    /// <summary>
    /// Manages the heavy industrial storm audio scape and dynamic low-pass frequency filtering:
    /// - Realistic scheduled distant coastal sirens, foghorn blasts, and groaning metal stress
    ///   powered by authentic studio audio clips from Free Pack and SFX.
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

        [Header("Authentic Audio Clips")]
        public AudioClip sirenClip;
        public AudioClip foghornClip;
        public AudioClip metalCreakClip;

        [Header("Scheduling Intervals")]
        public float minSirenInterval = 32f;
        public float maxSirenInterval = 65f;
        public float minFoghornInterval = 22f;
        public float maxFoghornInterval = 45f;
        public float minMetalCreakInterval = 14f;
        public float maxMetalCreakInterval = 28f;

        [Header("Dynamic Audio Low-Pass Filter")]
        public bool enableDynamicLowPass = true;
        public bool isDeepUnderwater = false;
        public AudioLowPassFilter cameraLowPassFilter;
        public float normalCutoffFrequency = 22000f;
        public float deepUnderwaterCutoff = 3000f;
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

        void Awake()
        {
            EnsureAudioSources();
            EnsureRealClips();
        }

        void OnEnable()
        {
            if (Instance == null || Instance == this)
                Instance = this;

            EnsureAudioSources();
            EnsureRealClips();
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
            nextMetalTime = Time.time + Random.Range(minMetalCreakInterval * 0.3f, minMetalCreakInterval);
        }

        public void EnsureAudioSources()
        {
            if (sirenSource == null) sirenSource = CreateAudioSource("SirenSource", 0.10f);
            if (foghornSource == null) foghornSource = CreateAudioSource("FoghornSource", 0.15f);
            if (metalCreakSource == null) metalCreakSource = CreateAudioSource("MetalCreakSource", 0.11f);
        }

        public void EnsureRealClips()
        {
            var audioMgr = PacificRimAudio.PacificRimAudioManager.Instance;
            if (audioMgr != null)
            {
                if (sirenClip == null) sirenClip = audioMgr.GetClip("Computer explosion 6 - Alarm");
                if (foghornClip == null) foghornClip = audioMgr.GetClip("GipsyFoghorn");
                if (metalCreakClip == null) metalCreakClip = audioMgr.GetClip("JaegerArmorCreak_01");
            }

#if UNITY_EDITOR
            if (sirenClip == null)
            {
                sirenClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free Pack/Computer explosion 6 - Alarm.wav");
            }
            if (foghornClip == null)
            {
                foghornClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/GipsyFoghorn.mp3");
            }
            if (metalCreakClip == null)
            {
                metalCreakClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/JaegerArmorCreak_01.mp3");
            }
#endif
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
                float baseCutoff = isDeepUnderwater ? deepUnderwaterCutoff : normalCutoffFrequency;
                currentLowPassCutoff = baseCutoff;
                targetLowPassCutoff = baseCutoff;
                cameraLowPassFilter.cutoffFrequency = baseCutoff;
            }
        }

        void Update()
        {
            if (!Application.isPlaying) return;

            if (!isDeepUnderwater && Time.time >= nextSirenTime)
            {
                PlaySiren();
                nextSirenTime = Time.time + Random.Range(minSirenInterval, maxSirenInterval);
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
            if (sirenClip != null && sirenSource != null)
            {
                float sfxVol = PacificRimAudio.PacificRimAudioManager.Instance != null ? PacificRimAudio.PacificRimAudioManager.Instance.SfxVolume : 0.55f;
                sirenSource.pitch = Random.Range(0.94f, 1.04f);
                sirenSource.PlayOneShot(sirenClip, 0.12f * sfxVol);
            }
        }

        public void PlayFoghorn()
        {
            // Foghorn is exclusively triggered by the player's Q emote.
        }

        public void PlayMetalCreak()
        {
            if (metalCreakClip != null && metalCreakSource != null)
            {
                float sfxVol = PacificRimAudio.PacificRimAudioManager.Instance != null ? PacificRimAudio.PacificRimAudioManager.Instance.SfxVolume : 0.55f;
                metalCreakSource.pitch = Random.Range(0.88f, 1.12f);
                metalCreakSource.PlayOneShot(metalCreakClip, 0.13f * sfxVol);
            }
        }

        public void TriggerThunderMuffle(float muffleStrength = 0.85f, float duration = 0.75f)
        {
            float baseCutoff = isDeepUnderwater ? deepUnderwaterCutoff : normalCutoffFrequency;
            float floorCutoff = isDeepUnderwater ? 1900f : thunderDeafeningCutoff;
            targetLowPassCutoff = Mathf.Lerp(baseCutoff, floorCutoff, Mathf.Clamp01(muffleStrength));
            muffleHoldTimer = Mathf.Max(muffleHoldTimer, duration);
        }

        public void TriggerSubBassMuffle(float muffleStrength = 0.70f, float duration = 0.60f)
        {
            float baseCutoff = isDeepUnderwater ? deepUnderwaterCutoff : normalCutoffFrequency;
            float floorCutoff = isDeepUnderwater ? 1900f : (thunderDeafeningCutoff * 1.35f);
            targetLowPassCutoff = Mathf.Lerp(baseCutoff, floorCutoff, Mathf.Clamp01(muffleStrength));
            muffleHoldTimer = Mathf.Max(muffleHoldTimer, duration);
        }

        private void UpdateLowPassFilter()
        {
            if (!enableDynamicLowPass) return;
            EnsureCameraLowPassFilter();
            if (cameraLowPassFilter == null) return;

            float dt = Time.deltaTime;
            float baseCutoff = isDeepUnderwater ? deepUnderwaterCutoff : normalCutoffFrequency;

            if (muffleHoldTimer > 0f)
            {
                muffleHoldTimer -= dt;
            }
            else
            {
                targetLowPassCutoff = baseCutoff;
            }

            Camera cam = Camera.main;
            if (!isDeepUnderwater && cam != null && WaterSystem.Instance != null)
            {
                Vector3 camPos = cam.transform.position;
                float seaY = WaterSystem.Instance.GetWaterHeight(camPos);
                float distToSea = camPos.y - seaY;

                if (distToSea < 0f)
                {
                    // Fully submerged underwater: deep underwater acoustics muffle
                    float depthFactor = Mathf.Clamp01(-distToSea / 12f);
                    float underwaterCutoff = Mathf.Lerp(waterProximityCutoff, 360f, depthFactor);
                    targetLowPassCutoff = Mathf.Min(targetLowPassCutoff, underwaterCutoff);
                }
                else if (distToSea < waterSurfaceMuffleThreshold)
                {
                    float factor = Mathf.Clamp01((waterSurfaceMuffleThreshold - distToSea) / waterSurfaceMuffleThreshold);
                    float surfaceCutoff = Mathf.Lerp(normalCutoffFrequency, waterProximityCutoff, factor);
                    targetLowPassCutoff = Mathf.Min(targetLowPassCutoff, surfaceCutoff);
                }
            }

            currentLowPassCutoff = Mathf.Lerp(currentLowPassCutoff, targetLowPassCutoff, dt * lowPassRecoverySpeed);
            cameraLowPassFilter.cutoffFrequency = currentLowPassCutoff;
        }
    }
}
