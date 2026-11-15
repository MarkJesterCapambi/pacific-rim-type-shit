#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PacificRimWater;

namespace PacificRimWater.Editor
{
    public static class SetupArenaBarriers
    {
        public static void CreateArenaBarriers()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            Debug.Log($"[Arena Setup] Starting barrier generation in scene: {activeScene.name}");

            // 1. Locate Test_Platform to establish exact boundary coordinates
            GameObject platform = GameObject.Find("Test_Platform");
            Vector3 platCenter = Vector3.zero;
            Vector3 platSize = new Vector3(1000f, 1f, 1000f);

            if (platform != null)
            {
                var col = platform.GetComponent<BoxCollider>();
                if (col != null)
                {
                    platSize = Vector3.Scale(col.size, platform.transform.lossyScale);
                    platCenter = platform.transform.TransformPoint(col.center);
                }
                else
                {
                    platSize = platform.transform.lossyScale;
                    platCenter = platform.transform.position;
                }
            }
            else
            {
                Debug.LogWarning("[Arena Setup] 'Test_Platform' not found, defaulting to 1000x1000 arena.");
            }

            // Floor top level and extents
            float minX = platCenter.x - platSize.x * 0.5f;
            float maxX = platCenter.x + platSize.x * 0.5f;
            float minZ = platCenter.z - platSize.z * 0.5f;
            float maxZ = platCenter.z + platSize.z * 0.5f;
            float floorY = platCenter.y + platSize.y * 0.5f;

            // Ensure bounds enclose any active monsters if spawned farther
            GameObject kaijuObj = GameObject.Find("Leatherback");
            if (kaijuObj != null)
            {
                minX = Mathf.Min(minX, kaijuObj.transform.position.x - 50f);
                maxX = Mathf.Max(maxX, kaijuObj.transform.position.x + 50f);
                minZ = Mathf.Min(minZ, kaijuObj.transform.position.z - 50f);
                maxZ = Mathf.Max(maxZ, kaijuObj.transform.position.z + 50f);
            }

            Debug.Log($"[Arena Setup] Arena Extents: X=[{minX:F1}, {maxX:F1}], Z=[{minZ:F1}, {maxZ:F1}], Floor Y={floorY:F1}");

            // 2. Create or find parent container
            string rootName = "Arena_Invisible_Barriers";
            GameObject root = GameObject.Find(rootName);
            if (root == null)
            {
                root = new GameObject(rootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Arena Barriers Root");
            }
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            // Clean up old children if re-running
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);
            }

            float wallHeight = 120f;
            float wallThickness = 25f;
            float wallCenterY = floorY + (wallHeight * 0.5f) - 10f; // Sunk 10m below floor, 110m above floor

            float arenaWidthX = maxX - minX;
            float arenaLengthZ = maxZ - minZ;
            float centerX = (minX + maxX) * 0.5f;
            float centerZ = (minZ + maxZ) * 0.5f;

            // Barrier North (+Z)
            CreateSingleBarrier(root.transform, "Barrier_North",
                new Vector3(centerX, wallCenterY, maxZ + wallThickness * 0.5f),
                new Vector3(arenaWidthX + wallThickness * 2f, wallHeight, wallThickness));

            // Barrier South (-Z)
            CreateSingleBarrier(root.transform, "Barrier_South",
                new Vector3(centerX, wallCenterY, minZ - wallThickness * 0.5f),
                new Vector3(arenaWidthX + wallThickness * 2f, wallHeight, wallThickness));

            // Barrier East (+X)
            CreateSingleBarrier(root.transform, "Barrier_East",
                new Vector3(maxX + wallThickness * 0.5f, wallCenterY, centerZ),
                new Vector3(wallThickness, wallHeight, arenaLengthZ + wallThickness * 2f));

            // Barrier West (-X)
            CreateSingleBarrier(root.transform, "Barrier_West",
                new Vector3(minX - wallThickness * 0.5f, wallCenterY, centerZ),
                new Vector3(wallThickness, wallHeight, arenaLengthZ + wallThickness * 2f));

            // 3. Update SimpleJaegerController Stamina in scene
            var jaeger = Object.FindAnyObjectByType<SimpleJaegerController>();
            if (jaeger != null)
            {
                Undo.RecordObject(jaeger, "Buff Stamina 1.25x");
                jaeger.maxStamina = 125f;
                jaeger.currentStamina = 125f;
                jaeger.exhaustionRecoveryRatio = 0.35f;
                jaeger.exhaustionRecoveryThreshold = 125f * 0.35f;
                EditorUtility.SetDirty(jaeger);
                Debug.Log($"<color=#00e5ff>[Arena Setup]</color> Gipsy Danger stamina updated in scene: Max={jaeger.maxStamina}, Current={jaeger.currentStamina}, Threshold={jaeger.exhaustionRecoveryThreshold}");
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            bool saved = EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"<color=#00ff88>[Arena Setup] Successfully created 4 invisible barriers and saved scene: {saved}</color>");
        }

        private static void CreateSingleBarrier(Transform parent, string name, Vector3 worldPos, Vector3 boxSize)
        {
            GameObject wall = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(wall, $"Create {name}");
            wall.transform.SetParent(parent, false);
            wall.transform.position = worldPos;
            wall.transform.rotation = Quaternion.identity;
            wall.transform.localScale = Vector3.one;

            var col = wall.AddComponent<BoxCollider>();
            col.isTrigger = false;
            col.size = boxSize;
            col.center = Vector3.zero;

            wall.layer = 0; // Default layer
            wall.tag = "Untagged";

            Debug.Log($"[Arena Setup] Created {name} at {worldPos} with size {boxSize}");
        }
    }
}
#endif
