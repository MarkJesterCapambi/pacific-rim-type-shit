using System;
using System.Collections.Generic;
using UnityEngine;

namespace PacificRimUI
{
    /// <summary>
    /// Translated from UI.Sounds and SoundService in the Battlefront Luau kit.
    /// Provides audio feedback for Hover, Click, Tab, Deny, Success, Back, and Comm.
    /// Generates high-tech procedural audio clips on-the-fly when no external audio files are assigned.
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
        [Range(0f, 1f)] public float masterVolume = 0f; // Temporary SFX disabled by default per user request
        [Range(0f, 1f)] public float hoverVolume = 0.35f;
        [Range(0f, 1f)] public float clickVolume = 0.55f;
        [Range(0f, 1f)] public float tabVolume = 0.45f;
        [Range(0f, 1f)] public float denyVolume = 0.50f;
        [Range(0f, 1f)] public float successVolume = 0.60f;
        [Range(0f, 1f)] public float backVolume = 0.50f;
        [Range(0f, 1f)] public float commVolume = 0.65f;

        [Header("Custom Audio Clips (Optional)")]
        public AudioClip hoverClip;
        public AudioClip clickClip;
        public AudioClip tabClip;
        public AudioClip denyClip;
        public AudioClip successClip;
        public AudioClip backClip;
        public AudioClip commClip;

        private readonly Dictionary<SoundType, AudioClip> proceduralCache = new Dictionary<SoundType, AudioClip>();
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

            EnsureProceduralClips();
        }

        private void EnsureProceduralClips()
        {
            if (!proceduralCache.ContainsKey(SoundType.Hover))
                proceduralCache[SoundType.Hover] = CreateProceduralHoverClip();
            if (!proceduralCache.ContainsKey(SoundType.Click))
                proceduralCache[SoundType.Click] = CreateProceduralClickClip();
            if (!proceduralCache.ContainsKey(SoundType.Tab))
                proceduralCache[SoundType.Tab] = CreateProceduralTabClip();
            if (!proceduralCache.ContainsKey(SoundType.Deny))
                proceduralCache[SoundType.Deny] = CreateProceduralDenyClip();
            if (!proceduralCache.ContainsKey(SoundType.Success))
                proceduralCache[SoundType.Success] = CreateProceduralSuccessClip();
            if (!proceduralCache.ContainsKey(SoundType.Back))
                proceduralCache[SoundType.Back] = CreateProceduralBackClip();
            if (!proceduralCache.ContainsKey(SoundType.Comm))
                proceduralCache[SoundType.Comm] = CreateProceduralCommClip();
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
                case SoundType.Hover: return hoverClip != null ? hoverClip : proceduralCache[SoundType.Hover];
                case SoundType.Click: return clickClip != null ? clickClip : proceduralCache[SoundType.Click];
                case SoundType.Tab: return tabClip != null ? tabClip : proceduralCache[SoundType.Tab];
                case SoundType.Deny: return denyClip != null ? denyClip : proceduralCache[SoundType.Deny];
                case SoundType.Success: return successClip != null ? successClip : proceduralCache[SoundType.Success];
                case SoundType.Back: return backClip != null ? backClip : proceduralCache[SoundType.Back];
                case SoundType.Comm: return commClip != null ? commClip : proceduralCache[SoundType.Comm];
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

        #region Procedural Audio Clip Synthesizers

        private AudioClip CreateProceduralHoverClip()
        {
            // Crisp, gentle sci-fi blip (2400 Hz decaying exponential)
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * 0.035f);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 90f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * (2200f + t * 400f) * t) * env * 0.5f;
            }
            var clip = AudioClip.Create("UI_Proc_Hover", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralClickClip()
        {
            // Solid mechanical tactile snap + high resonant ping
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * 0.065f);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 70f);
                float wave = Mathf.Sin(2f * Mathf.PI * 980f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 1960f * t) * 0.4f;
                samples[i] = wave * env * 0.7f;
            }
            var clip = AudioClip.Create("UI_Proc_Click", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralTabClip()
        {
            // Two-tone rising holographic chime (880 Hz -> 1320 Hz)
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * 0.09f);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float freq = t < 0.045f ? 880f : 1320f;
                float env = Mathf.Clamp01(1f - (t / 0.09f));
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.6f;
            }
            var clip = AudioClip.Create("UI_Proc_Tab", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralDenyClip()
        {
            // Heavy warning buzz (two downward saw-ish pulses at 160 Hz)
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * 0.16f);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Abs(Mathf.Sin(t * 38f)) * Mathf.Exp(-t * 12f);
                float wave = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 180f * t));
                samples[i] = wave * env * 0.5f;
            }
            var clip = AudioClip.Create("UI_Proc_Deny", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralSuccessClip()
        {
            // Ascending triad chime (523Hz C -> 659Hz E -> 784Hz G)
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * 0.28f);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float freq = t < 0.08f ? 523.25f : (t < 0.16f ? 659.25f : 783.99f);
                float env = Mathf.Exp(-((t % 0.08f) * 22f)) * (1f - (t / 0.28f));
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.65f;
            }
            var clip = AudioClip.Create("UI_Proc_Success", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralBackClip()
        {
            // Soft downward holographic dismiss tone
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * 0.08f);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(1200f, 400f, t / 0.08f);
                float env = Mathf.Clamp01(1f - (t / 0.08f));
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.55f;
            }
            var clip = AudioClip.Create("UI_Proc_Back", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateProceduralCommClip()
        {
            // Radio static burst + high military tone ping
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * 0.18f);
            float[] samples = new float[sampleCount];
            var rng = new System.Random(42);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.25f;
                float tone = Mathf.Sin(2f * Mathf.PI * 1850f * t) * 0.6f;
                float env = Mathf.Exp(-t * 14f);
                samples[i] = (noise + tone) * env * 0.6f;
            }
            var clip = AudioClip.Create("UI_Proc_Comm", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
