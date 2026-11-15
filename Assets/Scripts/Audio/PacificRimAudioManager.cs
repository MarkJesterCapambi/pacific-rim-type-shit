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
        private readonly List<AudioClip> kaijuBitePool = new List<AudioClip>();
        private readonly List<AudioClip> swordImpactPool = new List<AudioClip>();
        private readonly List<AudioClip> lightningStrikePool = new List<AudioClip>();
        private readonly List<AudioClip> steamHissPool = new List<AudioClip>();
        private readonly List<AudioClip> uiClickPool = new List<AudioClip>();

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
            // 1. Primary Load for Standalone Builds & Editor:
            // Load the bundled audio bank from Resources (guaranteed to be packed into builds)
            var bank = Resources.Load<PacificRimAudioBank>("PacificRimAudioBank");
            if (bank != null && bank.allClips != null)
            {
                for (int i = 0; i < bank.allClips.Length; i++)
                {
                    RegisterClip(bank.allClips[i]);
                }
            }

            // 2. Also check any already loaded AudioClips in memory
            AudioClip[] loaded = Resources.FindObjectsOfTypeAll<AudioClip>();
            foreach (var clip in loaded)
            {
                RegisterClip(clip);
            }

#if UNITY_EDITOR
            // 3. In Editor mode, scan Assets/SFX, sound packs, and Suimono audio
            string[] searchFolders = new[] {
                "Assets/SFX",
                "Assets/Free Pack",
                "Assets/Nature Sound FX",
                "Assets/Casual Game Sounds U6",
                "Assets/SUIMONO - WATER SYSTEM 2/SOUNDS"
            };
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", searchFolders);
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

        /// <summary>
        /// Forces all cached combat audio clips to uncompress and load into memory ahead of gameplay,
        /// completely eliminating audio decompression spikes during combat.
        /// </summary>
        public void PreloadAllAudioClips()
        {
            foreach (var kvp in clipRegistry)
            {
                if (kvp.Value != null && kvp.Value.loadState != AudioDataLoadState.Loaded)
                {
                    kvp.Value.LoadAudioData();
                }
            }
        }

        private void PopulatePools()
        {
            // 1. Light Punches
            jaegerLightPunchPool.Clear();
            AddIfExists(jaegerLightPunchPool, "JaegerPunchLight_01");
            AddIfExists(jaegerLightPunchPool, "JaegerPunchLight_02");
            AddIfExists(jaegerLightPunchPool, "JaegerPunchLight_03");
            AddIfExists(jaegerLightPunchPool, "Indiana Jones Punch");
            AddIfExists(jaegerLightPunchPool, "Bloody punch");
            AddIfExists(jaegerLightPunchPool, "Metal impact 5");
            AddIfExists(jaegerLightPunchPool, "Metal Impact 7");

            // 2. Heavy Punches
            jaegerHeavyPunchPool.Clear();
            AddIfExists(jaegerHeavyPunchPool, "JaegerPunchHeavy_01");
            AddIfExists(jaegerHeavyPunchPool, "JaegerPunchHeavy_02");
            AddIfExists(jaegerHeavyPunchPool, "Heavy Object Impact 10");
            AddIfExists(jaegerHeavyPunchPool, "Heavy Object Impact 4");
            AddIfExists(jaegerHeavyPunchPool, "Cannon impact 9");

            // 3. Titan Ocean Footsteps & Big Splashes
            titanFootstepPool.Clear();
            AddIfExists(titanFootstepPool, "OceanFootstep_Water_01");
            AddIfExists(titanFootstepPool, "OceanFootstep_Water_02");
            AddIfExists(titanFootstepPool, "OceanFootstep_Water_03");
            AddIfExists(titanFootstepPool, "water_bigsplash");
            AddIfExists(titanFootstepPool, "water_splash01");
            AddIfExists(titanFootstepPool, "water_splash02");
            AddIfExists(titanFootstepPool, "water_splash03");

            // 4. Hydraulic Servos
            jaegerServoPool.Clear();
            AddIfExists(jaegerServoPool, "JaegerHydraulicServo_01");
            AddIfExists(jaegerServoPool, "JaegerHydraulicServo_02");
            AddIfExists(jaegerServoPool, "Hydraulic piston mechanical whine");

            // 5. Kaiju Bites & Claws
            kaijuBitePool.Clear();
            AddIfExists(kaijuBitePool, "Monster Bite on Armor");
            AddIfExists(kaijuBitePool, "Monster Bite");
            AddIfExists(kaijuBitePool, "KaijuAttackGrunt_01");

            // 6. Sword / Blade Clashes
            swordImpactPool.Clear();
            for (int i = 1; i <= 7; i++)
            {
                AddIfExists(swordImpactPool, $"Sword_hitting the armor {i}");
                AddIfExists(swordImpactPool, $"Sword_hitting the sword {i}");
            }

            // 7. Thunder / Lightning Strikes
            lightningStrikePool.Clear();
            for (int i = 1; i <= 7; i++)
            {
                AddIfExists(lightningStrikePool, $"Lighting {i}");
            }

            // 8. Steam Vents
            steamHissPool.Clear();
            for (int i = 1; i <= 7; i++)
            {
                AddIfExists(steamHissPool, $"Steam {i}");
            }

            // 9. UI clicks & telemetry: strictly tactile micro clicks (no melodic/retro chimes)
            uiClickPool.Clear();
            AddIfExists(uiClickPool, "DM-CGS-01");
            AddIfExists(uiClickPool, "DM-CGS-03");
            AddIfExists(uiClickPool, "DM-CGS-20");
            AddIfExists(uiClickPool, "DM-CGS-40");
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

            // Dedicated Loops (Lowered background rainfall/storm ambience by 75% to 0.05f)
            stormAmbienceSource = CreateLoopSource("StormAmbienceLoop", 0.05f);
            jaegerTurbineSource = CreateLoopSource("JaegerTurbineLoop", 0.10f);
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

        // 4b. Perfect Titan Parry Clash
        public void PlayParryClang(Vector3 impactPos)
        {
            // Explosive authentic layered titanium parry deflection using real sound packs:
            // 1. Primary sharp resonant metallic clang (Metal Impact 7 in 2D and 3D)
            PlayAtPosition("Metal Impact 7", impactPos, volume: 1.65f, pitchMin: 0.98f, pitchMax: 1.03f);
            Play2D("Metal Impact 7", volume: 1.25f, pitch: 1.02f);

            // 2. Heavy concussive sub-bass body impact shock (Heavy Object Impact 4)
            PlayAtPosition("Heavy Object Impact 4", impactPos, volume: 1.40f, pitchMin: 0.94f, pitchMax: 1.02f);

            // 3. Resonant iron ringing tail (Metal impact 5)
            PlayAtPosition("Metal impact 5", impactPos, volume: 1.10f, pitchMin: 1.06f, pitchMax: 1.14f);

            // 4. Instantaneous hydraulic piston lock whine (Gipsy bracing against colossal kinetic energy)
            PlayAtPosition("Hydraulic piston mechanical whine", impactPos, volume: 0.95f, pitchMin: 1.05f, pitchMax: 1.15f);

            // 5. Brutal Kaiju claw deflection crunch (Monster Bite on Armor)
            PlayAtPosition("Monster Bite on Armor", impactPos, volume: 0.85f, pitchMin: 0.95f, pitchMax: 1.05f);

            PlayHydraulicServo(impactPos, volume: 0.85f);
        }

        // 4c. Aerial Deployment Heavy Clamp Release
        public void PlayDeploymentClampRelease(Vector3 pos)
        {
            PlayAtPosition("Heavy Object Impact 4", pos, volume: 1.35f, pitchMin: 0.96f, pitchMax: 1.04f);
            PlayAtPosition("Hydraulic piston mechanical whine", pos, volume: 1.15f, pitchMin: 1.02f, pitchMax: 1.10f);
            PlayAtPosition("Metal Impact 7", pos, volume: 1.05f, pitchMin: 1.05f, pitchMax: 1.15f);
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

        public void PlayKaijuBite(Vector3 pos)
        {
            if (kaijuBitePool.Count > 0)
            {
                var clip = kaijuBitePool[Random.Range(0, kaijuBitePool.Count)];
                PlayAtPosition(clip, pos, volume: 1.0f, pitchMin: 0.92f, pitchMax: 1.05f);
            }
        }

        public void PlaySwordStrike(Vector3 pos)
        {
            if (swordImpactPool.Count > 0)
            {
                var clip = swordImpactPool[Random.Range(0, swordImpactPool.Count)];
                PlayAtPosition(clip, pos, volume: 1.0f, pitchMin: 0.95f, pitchMax: 1.05f);
            }
        }

        public void PlayLightningStrike(Vector3 pos)
        {
            if (lightningStrikePool.Count > 0)
            {
                var clip = lightningStrikePool[Random.Range(0, lightningStrikePool.Count)];
                PlayAtPosition(clip, pos, volume: 1.0f, pitchMin: 0.90f, pitchMax: 1.10f);
            }
        }

        public void PlaySteamVent(Vector3 pos)
        {
            if (steamHissPool.Count > 0)
            {
                var clip = steamHissPool[Random.Range(0, steamHissPool.Count)];
                PlayAtPosition(clip, pos, volume: 0.75f, pitchMin: 0.95f, pitchMax: 1.05f);
            }
        }

        public void PlayUiClick()
        {
            if (uiClickPool.Count > 0)
            {
                var clip = uiClickPool[Random.Range(0, uiClickPool.Count)];
                if (uiAudioSource != null) uiAudioSource.PlayOneShot(clip, 0.7f);
            }
            else
            {
                PlayHUDCameraToggle();
            }
        }

        // 8. HUD & Cockpit Telemetry (Authentic industrial mechanical cues - NO retro chimes)
        public void PlayHUDCameraToggle()
        {
            Play2D("DM-CGS-03", 0.70f, 1.05f);
        }

        public void PlayHUDStaminaDepleted()
        {
            // Mechanical stress groan on exhaustion
            PlayAtPosition("JaegerArmorCreak_01", Camera.main != null ? Camera.main.transform.position : Vector3.zero, 0.65f, pitchMin: 1.15f, pitchMax: 1.25f);
        }

        public void PlayHUDStaminaRecovered35()
        {
            // Tactical hydraulic repressurization hiss (replaces retro chime)
            Play2D("JaegerHydraulicServo_01", 0.65f, 1.25f);
        }

        public void PlayHUDCheatActivated()
        {
            // Colossal industrial power surge (replaces retro chime)
            Play2D("Hydraulic piston mechanical whine", 0.95f, 1.05f);
            Play2D("JaegerHydraulicServo_01", 0.85f, 1.0f);
        }

        // 8b. Gipsy Danger Iconic Foghorn Blast
        public void PlayGipsyFoghorn(Vector3 pos)
        {
            PlayAtPosition("GipsyFoghorn", pos, volume: 1.55f, pitchMin: 0.98f, pitchMax: 1.02f);
            Play2D("GipsyFoghorn", volume: 1.15f, pitch: 1.0f);
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

        public void StartJaegerTurbine()
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

        public void UpdateTurbineAudio(float normalizedSpeed)
        {
            if (jaegerTurbineSource != null)
            {
                if (!jaegerTurbineSource.isPlaying)
                {
                    StartJaegerTurbine();
                }
                jaegerTurbineSource.pitch = Mathf.Lerp(0.85f, 1.40f, normalizedSpeed);
                jaegerTurbineSource.volume = Mathf.Lerp(0.22f, 0.48f, normalizedSpeed);
            }
        }

        #endregion
    }
}
