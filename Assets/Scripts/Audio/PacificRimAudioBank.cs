using UnityEngine;

namespace PacificRimAudio
{
    /// <summary>
    /// Bundles and serializes all project AudioClips into a Resources asset so that Unity's build
    /// linker includes 100% of the project's combat SFX, ambient loops, and UI audio into standalone builds.
    /// Loaded at runtime via Resources.Load&lt;PacificRimAudioBank&gt;("PacificRimAudioBank").
    /// </summary>
    public class PacificRimAudioBank : ScriptableObject
    {
        [Tooltip("All project audio clips bundled for standalone player builds.")]
        public AudioClip[] allClips;
    }
}
