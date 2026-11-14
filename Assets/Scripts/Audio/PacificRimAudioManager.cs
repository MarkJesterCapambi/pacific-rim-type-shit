using System.Collections.Generic;
using UnityEngine;

namespace PacificRimAudio
{
    /// <summary>
    /// Central audio and SFX manager for Pacific Rim combat.
    /// Loads clips dynamically from Assets/SFX, handles 3D spatialized one-shots,
    /// randomized variation pools (punches, footsteps, hydraulic servos),
    /// and dedicated looped sound emitters (vortex turbine, storm ambience, EMP sparks, rocket thrusters).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PacificRimAudioManager : MonoBehaviour
    {
        private static PacificRimAudioManager instance;
        public static PacificRimAudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Object.FindAnyObjectByType<PacificRimAudioManager>();
                    if (instance == null)
                    {
                        var go = new GameObject("PacificRim_AudioManager");
                        instance = go.AddComponent<PacificRimAudioManager>();
                        if (Application.isPlaying)
                        {
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return instance;
            }
        }

        // Cached AudioClips by key/filename
        private readonly Dictionary<string, AudioClip> clipRegistry = new Dictionary<string, AudioClip>(System.StringComparer.OrdinalIgnoreCase);

        // Pre-sorted random variation pools
        private readonly List<AudioClip> jaegerLightPunchPool = new List<AudioClip>();
        private readonly List<AudioClip> jaegerHeavyPunchPool = new List<AudioClip>();
        private readonly List<AudioClip> titanFootstepPool = new List<AudioClip>();
        private readonly List<AudioClip> jaegerServoPool = new List<AudioClip>();

        // AudioSource pool for 3D positional one-shots
        private readonly List<AudioSource> oneShotPool = new List<AudioSource>();
        private const int MaxOneShotSources = 16;

        // Dedicated loop sources
        private AudioSource stormAmbienceSource;
        private AudioSource jaegerTurbineSource;
        private AudioSource jaegerRocketBurnSource;
        private AudioSource kaijuEmpSparksSource;

        // UI 2D AudioSource
        private AudioSource uiAudioSource;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            LoadClips();
            InitializeAudioSources();
        }

        private void Start()
        {
            StartStormAmbience();
            StartJaegerTurbine();
        }

        private void LoadClips()
        {
            AudioClip[] loaded = Resources.FindObjectsOfTypeAll<AudioClip>();
            // Also load from Resources if packaged, or via Resources.LoadAll
            foreach (var clip in loaded)
            {
                RegisterClip(clip);
            }

#if UNITY_EDITOR
            // In editor mode, scan Assets/SFX directly
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/SFX" });
            foreach (var guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                RegisterClip(clip);
            }
#endif

            PopulatePools();
            Debug.Log($"<color=#00e5ff><b>[PacificRimAudio]</b></color> Audio Manager initialized with {clipRegistry.Count} clips loaded.");
        }

        private void RegisterClip(AudioClip clip)
        {
            if (clip == null || string.IsNullOrEmpty(clip.name)) return;
            if (!clipRegistry.ContainsKey(clip.name))
            {
                clipRegistry[clip.name] = clip;
            }
        }

        private void PopulatePools()
        {
            jaegerLightPunchPool.Clear();
            AddIfExists(jaegerLightPunchPool, "JaegerPunchLight_01");
            AddIfExists(jaegerLightPunchPool, "JaegerPunchLight_02");
            AddIfExists(jaegerLightPunchPool, "JaegerPunchLight_03");

            jaegerHeavyPunchPool.Clear();
            AddIfExists(jaegerHeavyPunchPool, "JaegerPunchHeavy_01");
            AddIfExists(jaegerHeavyPunchPool, "JaegerPunchHeavy_02");

            titanFootstepPool.Clear();
            AddIfExists(titanFootstepPool, "OceanFootstep_Water_01");
            AddIfExists(titanFootstepPool, "OceanFootstep_Water_02");
            AddIfExists(titanFootstepPool, "OceanFootstep_Water_03");

            jaegerServoPool.Clear();
            AddIfExists(jaegerServoPool, "JaegerHydraulicServo_01");
            AddIfExists(jaegerServoPool, "JaegerHydraulicServo_02");
            AddIfExists(jaegerServoPool, "Hydraulic piston mechanical whine");
        }

        private void AddIfExists(List<AudioClip> list, string clipName)
        {
            if (clipRegistry.TryGetValue(clipName, out var c) && c != null)
            {
                if (!list.Contains(c)) list.Add(c);
            }
        }

        private void InitializeAudioSources()
        {
            // UI AudioSource
            uiAudioSource = gameObject.AddComponent<AudioSource>();
            uiAudioSource.playOnAwake = false;
            uiAudioSource.spatialBlend = 0f;
            uiAudioSource.ignoreListenerPause = true;

            // Dedicated Loops
            stormAmbienceSource = CreateLoopSource("StormAmbienceLoop", 0.40f);
            jaegerTurbineSource = CreateLoopSource("JaegerTurbineLoop", 0.28f);
            jaegerRocketBurnSource = CreateLoopSource("JaegerRocketBurnLoop", 0.85f);
            kaijuEmpSparksSource = CreateLoopSource("KaijuEmpSparksLoop", 0.65f);

            // One-shot pool
            for (int i = 0; i < MaxOneShotSources; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f; // 3D
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 15f;
                src.maxDistance = 500f;
                src.dopplerLevel = 0.2f;
                oneShotPool.Add(src);
            }
        }

        private AudioSource CreateLoopSource(string name, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.volume = volume;
            src.spatialBlend = 0f;
            return src;
        }

        public AudioClip GetClip(string name)
        {
            if (clipRegistry.TryGetValue(name, out var clip))
            {
                return clip;
            }
            return null;
        }

        #region 3D One-Shot Playback
        public void PlayAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float pitchMin = 0.95f, float pitchMax = 1.05f)
        {
            if (clip == null) return;

            AudioSource available = null;
            for (int i = 0; i < oneShotPool.Count; i++)
            {
                if (!oneShotPool[i].isPlaying)
                {
                    available = oneShotPool[i];
                    break;
                }
            }

            if (available == null)
            {
                available = oneShotPool[0]; // Steal oldest
            }

            available.transform.position = position;
            available.pitch = Random.Range(pitchMin, pitchMax);
            available.volume = Mathf.Clamp01(volume);
            available.PlayOneShot(clip, volume);
        }

        public void PlayAtPosition(string clipName, Vector3 position, float volume = 1f, float pitchMin = 0.95f, float pitchMax = 1.05f)
        {
            var clip = GetClip(clipName);
            if (clip != null)
            {
                PlayAtPosition(clip, position, volume, pitchMin, pitchMax);
            }
        }

        public void Play2D(string clipName, float volume = 1f, float pitch = 1f)
        {
            var clip = GetClip(clipName);
            if (clip != null && uiAudioSource != null)
            {
                uiAudioSource.pitch = pitch;
                uiAudioSource.PlayOneShot(clip, volume);
            }
        }
        #endregion

        #region High-Level Game Audio Hooks

        // 1. Jaeger Melee Impacts
        public void PlayJaegerPunchHit(Vector3 hitPos, bool isHeavy)
        {
            if (isHeavy && jaegerHeavyPunchPool.Count > 0)
            {
                var clip = jaegerHeavyPunchPool[Random.Range(0, jaegerHeavyPunchPool.Count)];
                PlayAtPosition(clip, hitPos, volume: 1.0f, pitchMin: 0.92f, pitchMax: 1.04f);
            }
            else if (jaegerLightPunchPool.Count > 0)
            {
                var clip = jaegerLightPunchPool[Random.Range(0, jaegerLightPunchPool.Count)];
                PlayAtPosition(clip, hitPos, volume: 0.85f, pitchMin: 0.95f, pitchMax: 1.06f);
            }
        }

        // 2. Attack Air Whoosh
        public void PlayAttackWhoosh(Vector3 pos, bool isHeavy)
        {
            string clipName = isHeavy ? "JaegerAttackWhoosh_Heavy" : "JaegerAttackWhoosh_Light";
            PlayAtPosition(clipName, pos, volume: isHeavy ? 0.9f : 0.7f, pitchMin: 0.96f, pitchMax: 1.04f);
        }

        // 3. Rocket Propulsion
        public void PlayRocketIgnition(Vector3 pos)
        {
            PlayAtPosition("JaegerRocketIgnition", pos, volume: 1.0f, pitchMin: 0.97f, pitchMax: 1.03f);
        }

        public void SetRocketBurnLoop(bool active, Vector3 pos)
        {
            if (jaegerRocketBurnSource == null) return;
            if (active)
            {
                if (!jaegerRocketBurnSource.isPlaying)
                {
                    var clip = GetClip("JaegerRocketBurnLoop");
                    if (clip != null)
                    {
                        jaegerRocketBurnSource.clip = clip;
                        jaegerRocketBurnSource.transform.position = pos;
                        jaegerRocketBurnSource.spatialBlend = 0.5f;
                        jaegerRocketBurnSource.Play();
                    }
                }
            }
            else
            {
                if (jaegerRocketBurnSource.isPlaying)
                {
                    jaegerRocketBurnSource.Stop();
                    PlayAtPosition("JaegerRocketExhaustCutoff", pos, volume: 0.85f);
                }
            }
        }

        // 4. Hydraulic Servos & Armor Creak
        public void PlayHydraulicServo(Vector3 pos, float volume = 0.55f)
        {
            if (jaegerServoPool.Count > 0)
            {
                var clip = jaegerServoPool[Random.Range(0, jaegerServoPool.Count)];
                PlayAtPosition(clip, pos, volume: volume, pitchMin: 0.92f, pitchMax: 1.08f);
            }
        }

        public void PlayArmorCreak(Vector3 pos, float volume = 0.65f)
        {
            PlayAtPosition("JaegerArmorCreak_01", pos, volume: volume, pitchMin: 0.94f, pitchMax: 1.04f);
        }

        // 5. Ocean Footsteps
        public void PlayOceanFootstep(Vector3 pos, float intensity = 1f)
        {
            if (titanFootstepPool.Count > 0)
            {
                var clip = titanFootstepPool[Random.Range(0, titanFootstepPool.Count)];
                PlayAtPosition(clip, pos, volume: Mathf.Clamp01(0.75f * intensity), pitchMin: 0.92f, pitchMax: 1.05f);
            }
        }

        // 6. Floating Debris Kick
        public void PlayDebrisImpact(Vector3 pos, float volume = 0.8f)
        {
            PlayAtPosition("DebrisSplashImpact", pos, volume: volume, pitchMin: 0.93f, pitchMax: 1.07f);
        }

        // 7. Kaiju (Leatherback) Combat
        public void PlayKaijuAttackGrunt(Vector3 pos)
        {
            PlayAtPosition("KaijuAttackGrunt_01", pos, volume: 0.85f, pitchMin: 0.92f, pitchMax: 1.05f);
        }

        public void PlayKaijuSlamImpact(Vector3 pos)
        {
            PlayAtPosition("KaijuSlamImpact", pos, volume: 1.0f, pitchMin: 0.93f, pitchMax: 1.03f);
        }

        public void PlayKaijuStaggerPain(Vector3 pos)
        {
            PlayAtPosition("KaijuStaggerPain_01", pos, volume: 0.9f, pitchMin: 0.94f, pitchMax: 1.06f);
        }

        public void PlayKaijuEmpRoar(Vector3 pos)
        {
            PlayAtPosition("KaijuRoar_EmpBlast", pos, volume: 1.0f, pitchMin: 0.97f, pitchMax: 1.03f);
        }

        public void PlayEmpChargeUp(Vector3 pos)
        {
            PlayAtPosition("EmpChargeUp", pos, volume: 0.95f, pitchMin: 0.98f, pitchMax: 1.02f);
        }

        public void PlayEmpDischargeBlast(Vector3 pos)
        {
            PlayAtPosition("EmpDischargeBlast", pos, volume: 1.0f, pitchMin: 0.95f, pitchMax: 1.03f);
        }

        public void SetKaijuEmpSparksLoop(bool active, Vector3 pos)
        {
            if (kaijuEmpSparksSource == null) return;
            if (active)
            {
                if (!kaijuEmpSparksSource.isPlaying)
                {
                    var clip = GetClip("EmpSparksArcing_Loop");
                    if (clip != null)
                    {
                        kaijuEmpSparksSource.clip = clip;
                        kaijuEmpSparksSource.transform.position = pos;
                        kaijuEmpSparksSource.spatialBlend = 0.7f;
                        kaijuEmpSparksSource.Play();
                    }
                }
            }
            else
            {
                if (kaijuEmpSparksSource.isPlaying)
                {
                    kaijuEmpSparksSource.Stop();
                }
            }
        }

        public void PlayKaijuDeathCollapse(Vector3 pos)
        {
            PlayAtPosition("KaijuDeathCollapse", pos, volume: 1.0f);
        }

        // 8. HUD & Cockpit Telemetry
        public void PlayHUDCameraToggle()
        {
            Play2D("HUD_CameraToggle", 0.75f);
        }

        public void PlayHUDStaminaDepleted()
        {
            Play2D("HUD_StaminaDepleted", 0.85f);
        }

        public void PlayHUDStaminaRecovered35()
        {
            Play2D("HUD_StaminaRecovered35", 0.9f);
        }

        public void PlayHUDCheatActivated()
        {
            Play2D("HUD_CheatActivated", 0.95f);
        }

        // 9. Continuous Ambient Loops
        private void StartStormAmbience()
        {
            if (stormAmbienceSource != null && !stormAmbienceSource.isPlaying)
            {
                var clip = GetClip("OceanStormAmbience_Loop");
                if (clip != null)
                {
                    stormAmbienceSource.clip = clip;
                    stormAmbienceSource.Play();
                }
            }
        }

        private void StartJaegerTurbine()
        {
            if (jaegerTurbineSource != null && !jaegerTurbineSource.isPlaying)
            {
                var clip = GetClip("JaegerVortexTurbine_Loop");
                if (clip != null)
                {
                    jaegerTurbineSource.clip = clip;
                    jaegerTurbineSource.Play();
                }
            }
        }

        #endregion
    }
}
