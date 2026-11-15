using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace PacificRimAudio.Editor
{
    /// <summary>
    /// Scans the project for all AudioClips and bakes them into Assets/Resources/PacificRimAudioBank.asset.
    /// Because the asset is stored in Resources, Unity's build pipeline is forced to include every single
    /// audio file into standalone builds, ensuring zero missing sounds during gameplay.
    /// </summary>
    public class PacificRimAudioBankBaker : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("<color=#00e5ff><b>[PacificRimAudio]</b></color> Build preprocessor triggered: Baking audio bank for standalone player...");
            Bake();
        }

        [MenuItem("Pacific Rim/Bake Audio Bank for Build")]
        public static void BakeMenu()
        {
            Bake();
        }

        public static PacificRimAudioBank Bake()
        {
            const string resourcesDir = "Assets/Resources";
            const string assetPath = resourcesDir + "/PacificRimAudioBank.asset";

            if (!Directory.Exists(resourcesDir))
            {
                Directory.CreateDirectory(resourcesDir);
                AssetDatabase.Refresh();
            }

            // Load existing or create new asset
            var bank = AssetDatabase.LoadAssetAtPath<PacificRimAudioBank>(assetPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<PacificRimAudioBank>();
                AssetDatabase.CreateAsset(bank, assetPath);
            }

            string[] searchFolders = new[] {
                "Assets/SFX",
                "Assets/Free Pack",
                "Assets/Nature Sound FX",
                "Assets/Casual Game Sounds U6",
                "Assets/SUIMONO - WATER SYSTEM 2/SOUNDS"
            };

            // Filter to existing directories only
            var validFolders = new List<string>();
            foreach (var f in searchFolders)
            {
                if (Directory.Exists(f)) validFolders.Add(f);
            }

            string[] guids = validFolders.Count > 0 
                ? AssetDatabase.FindAssets("t:AudioClip", validFolders.ToArray())
                : AssetDatabase.FindAssets("t:AudioClip");

            var clips = new List<AudioClip>();
            var seenNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null && !clips.Contains(clip))
                {
                    clips.Add(clip);
                    seenNames.Add(clip.name);
                }
            }

            bank.allClips = clips.ToArray();
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00e5ff><b>[PacificRimAudio]</b></color> Successfully baked <b>{bank.allClips.Length} AudioClips</b> into '{assetPath}'. All sounds are guaranteed to carry over into standalone builds!");
            return bank;
        }
    }
}
