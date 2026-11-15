#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PacificRimEnvironment.Editor
{
    public static class SetupChinookSquadron
    {
        [MenuItem("Pacific Rim/Setup Overhead Chinook Squadron", false, 12)]
        public static void Setup()
        {
            // 1. Locate or create Chinook_Patrol_Squadron Root
            GameObject squadronRoot = GameObject.Find("Chinook_Patrol_Squadron");
            if (squadronRoot == null)
            {
                squadronRoot = new GameObject("Chinook_Patrol_Squadron");
                Undo.RegisterCreatedObjectUndo(squadronRoot, "Create Chinook Patrol Squadron");
            }

            squadronRoot.transform.position = Vector3.zero;
            squadronRoot.transform.rotation = Quaternion.identity;

            var squadron = squadronRoot.GetComponent<ChinookCombatPatrolSquadron>();
            if (squadron == null)
            {
                squadron = squadronRoot.AddComponent<ChinookCombatPatrolSquadron>();
            }

            // 2. Load Assets
            squadron.chinookPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PacificRim_Chinook.prefab");
            squadron.volumetricBeamMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_ChinookVolumetricBeam.mat");
            squadron.volumetricConeMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/PacificRim_VolumetricBeamCone.asset");
            squadron.helicopterAudioClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/HelicopterLoop.mp3");

            // 3. Resolve Targets
            squadron.ResolveTargets();

            // 4. Clear old children if re-baking
            for (int i = squadronRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = squadronRoot.transform.GetChild(i);
                Undo.DestroyObjectImmediate(child.gameObject);
            }
            squadron.gipsySquadron.Clear();
            squadron.kaijuSquadron.Clear();

            // 5. Spawn full 6-helicopter squadron
            squadron.EnsureSquadronSpawned();

            EditorUtility.SetDirty(squadronRoot);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("<color=#00e5ff><b>[ChinookSquadron]</b></color> Overhead Chinook Escort Squadron successfully deployed! 3 Chinooks tracking Gipsy, 3 tracking Leatherback with 5-second tactical rotations, volumetric searchlights, and lightning reactivity.");
        }
    }
}
#endif
