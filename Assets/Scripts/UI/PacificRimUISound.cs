using System;
using UnityEngine;

namespace PacificRimUI
{
    /// <summary>
    /// Provides tactile UI audio feedback for Hover, Click, Tab, Deny, Success, Back, and Comm.
    /// Uses real, authentic industrial and tactical sound effects from user packs (no retro chimes).
    /// </summary>
    public class PacificRimUISound : MonoBehaviour
    {
        private static PacificRimUISound instance;
        public static PacificRimUISound Instance
        {
            get
            {
                if (instance == null)
                {
                    var found = UnityEngine.Object.FindAnyObjectByType<PacificRimUISound>();
                    if (found != null)
                    {
                        instance = found;
                    }
                    else
                    {
                        var go = new GameObject("PacificRim_UISoundManager");
                        instance = go.AddComponent<PacificRimUISound>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        public enum SoundType
        {
            Hover,
            Click,
            Tab,
            Deny,
            Success,
            Back,
            Comm
        }

        [Header("Audio Source")]
        [SerializeField] private AudioSource audioSource;

        [Header("Volume Overrides")]
        [Range(0f, 1f)] public float masterVolume = 0.85f;
        [Range(0f, 1f)] public float hoverVolume = 0.35f;
        [Range(0f, 1f)] public float clickVolume = 0.70f;
        [Range(0f, 1f)] public float tabVolume = 0.65f;
        [Range(0f, 1f)] public float denyVolume = 0.65f;
        [Range(0f, 1f)] public float successVolume = 0.80f;
        [Range(0f, 1f)] public float backVolume = 0.60f;
        [Range(0f, 1f)] public float commVolume = 0.75f;

        [Header("Authentic Audio Clips (No Retro Chimes)")]
        public AudioClip hoverClip;
        public AudioClip clickClip;
        public AudioClip tabClip;
        public AudioClip denyClip;
        public AudioClip successClip;
        public AudioClip backClip;
        public AudioClip commClip;

        private float lastHoverTime = -1f;
        private const float HoverCooldown = 0.04f;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D UI Audio
            audioSource.ignoreListenerPause = true;

            EnsureRealClips();
        }

        public void EnsureRealClips()
        {
            var audioMgr = PacificRimAudio.PacificRimAudioManager.Instance;
            if (audioMgr != null)
            {
                if (hoverClip == null) hoverClip = audioMgr.GetClip("DM-CGS-01");
                if (clickClip == null) clickClip = audioMgr.GetClip("DM-CGS-03");
                if (tabClip == null) tabClip = audioMgr.GetClip("DM-CGS-03");
                if (denyClip == null) denyClip = audioMgr.GetClip("Laser Impact Light_6") ?? audioMgr.GetClip("DM-CGS-31");
                if (successClip == null) successClip = audioMgr.GetClip("Hydraulic piston mechanical whine") ?? audioMgr.GetClip("JaegerHydraulicServo_01");
                if (backClip == null) backClip = audioMgr.GetClip("DM-CGS-03");
                if (commClip == null) commClip = audioMgr.GetClip("Laser Impact Light_6") ?? audioMgr.GetClip("JaegerHydraulicServo_01");
            }

#if UNITY_EDITOR
            string cgsDir = "Assets/Casual Game Sounds U6/CasualGameSounds/";
            string freeDir = "Assets/Free Pack/";
            string sfxDir = "Assets/SFX/";

            // Pure tactile micro clicks (no melodic/retro chimes)
            if (hoverClip == null || hoverClip.name.Contains("26") || hoverClip.name.Contains("28"))
                hoverClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(cgsDir + "DM-CGS-01.wav");

            if (clickClip == null || clickClip.name.Contains("26") || clickClip.name.Contains("28"))
                clickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(cgsDir + "DM-CGS-03.wav");

            if (tabClip == null || tabClip.name.Contains("26") || tabClip.name.Contains("28"))
                tabClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(cgsDir + "DM-CGS-03.wav");

            if (denyClip == null || denyClip.name.Contains("31"))
                denyClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(freeDir + "Laser Impact Light_6.wav") ??
                           UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(cgsDir + "DM-CGS-31.wav");
            
            // Heavy mechanical hydraulic lock / surge for success / overcharge (NO CHIMES)
            if (successClip == null || successClip.name.Contains("26") || successClip.name.Contains("28") || successClip.name.Contains("CGS"))
                successClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(sfxDir + "Hydraulic piston mechanical whine.mp3") ??
                              UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(sfxDir + "JaegerHydraulicServo_01.mp3");
            
            if (backClip == null)
                backClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(cgsDir + "DM-CGS-03.wav");

            if (commClip == null || commClip.name.Contains("47") || commClip.name.Contains("CGS"))
                commClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(freeDir + "Laser Impact Light_6.wav") ??
                           UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(sfxDir + "JaegerHydraulicServo_01.mp3");
#endif
        }

        public static void Play(SoundType type, float volumeMultiplier = 1f)
        {
            Instance?.PlayInternal(type, volumeMultiplier);
        }

        public static void PlayHover() => Play(SoundType.Hover);
        public static void PlayClick() => Play(SoundType.Click);
        public static void PlayTab() => Play(SoundType.Tab);
        public static void PlayDeny() => Play(SoundType.Deny);
        public static void PlaySuccess() => Play(SoundType.Success);
        public static void PlayBack() => Play(SoundType.Back);
        public static void PlayComm() => Play(SoundType.Comm);

        private void PlayInternal(SoundType type, float volumeMultiplier = 1f)
        {
            if (masterVolume <= 0.001f) return;
            if (type == SoundType.Hover)
            {
                float now = Time.unscaledTime;
                if (now - lastHoverTime < HoverCooldown)
                    return;
                lastHoverTime = now;
            }

            AudioClip clip = GetClip(type);
            if (clip == null) return;

            float baseVol = GetDefaultVolume(type);
            float finalVol = Mathf.Clamp01(baseVol * masterVolume * volumeMultiplier);

            audioSource.PlayOneShot(clip, finalVol);
        }

        private AudioClip GetClip(SoundType type)
        {
            switch (type)
            {
                case SoundType.Hover: return hoverClip;
                case SoundType.Click: return clickClip;
                case SoundType.Tab: return tabClip;
                case SoundType.Deny: return denyClip;
                case SoundType.Success: return successClip;
                case SoundType.Back: return backClip;
                case SoundType.Comm: return commClip;
                default: return null;
            }
        }

        private float GetDefaultVolume(SoundType type)
        {
            switch (type)
            {
                case SoundType.Hover: return hoverVolume;
                case SoundType.Click: return clickVolume;
                case SoundType.Tab: return tabVolume;
                case SoundType.Deny: return denyVolume;
                case SoundType.Success: return successVolume;
                case SoundType.Back: return backVolume;
                case SoundType.Comm: return commVolume;
                default: return 0.5f;
            }
        }
    }
}
