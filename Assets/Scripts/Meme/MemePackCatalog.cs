using UnityEngine;
using UnityEngine.Video;

namespace PacificRimMeme
{
    /// <summary>
    /// Bundles serialized references to Assets/MemePack assets inside Assets/Resources
    /// so standalone builds include them alongside Editor play mode.
    /// </summary>
    public class MemePackCatalog : ScriptableObject
    {
        public VideoClip freddyJumpscareClip;
        public GameObject f22RaptorPrefab;
    }
}
