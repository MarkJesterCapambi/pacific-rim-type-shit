#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PacificRimEnvironment.Editor
{
    public static class SetupFloatingPropsField
    {
        private struct PropSpawnDef
        {
            public string name;
            public FloatingDebris.DebrisType type;
            public Vector3 position;
            public Quaternion rotation;
            public Color tintColor;
            public Color lightColor;
            public float blinkRate;
        }

        [MenuItem("Pacific Rim/Setup Floating Debris Field", false, 13)]
        public static void Setup()
        {
            GameObject fieldRoot = GameObject.Find("Floating_Debris_Field");
            if (fieldRoot == null)
            {
                fieldRoot = new GameObject("Floating_Debris_Field");
                Undo.RegisterCreatedObjectUndo(fieldRoot, "Create Floating Debris Field");
            }

            fieldRoot.transform.position = Vector3.zero;
            fieldRoot.transform.rotation = Quaternion.identity;

            // Definition of additional props spread around the arena
            var propDefs = new PropSpawnDef[]
            {
                new PropSpawnDef
                {
                    name = "ShippingContainer_03_Orange",
                    type = FloatingDebris.DebrisType.ShippingContainer,
                    position = new Vector3(-85f, 0f, 70f),
                    rotation = Quaternion.Euler(0f, 32f, 0f),
                    tintColor = new Color(0.88f, 0.42f, 0.12f, 1f)
                },
                new PropSpawnDef
                {
                    name = "ShippingContainer_04_Green",
                    type = FloatingDebris.DebrisType.ShippingContainer,
                    position = new Vector3(80f, 0f, -40f),
                    rotation = Quaternion.Euler(0f, -45f, 0f),
                    tintColor = new Color(0.12f, 0.55f, 0.32f, 1f)
                },
                new PropSpawnDef
                {
                    name = "ShippingContainer_05_Navy",
                    type = FloatingDebris.DebrisType.ShippingContainer,
                    position = new Vector3(-70f, 0f, -65f),
                    rotation = Quaternion.Euler(0f, 15f, 0f),
                    tintColor = new Color(0.12f, 0.22f, 0.48f, 1f)
                },
                new PropSpawnDef
                {
                    name = "ShippingContainer_06_Yellow",
                    type = FloatingDebris.DebrisType.ShippingContainer,
                    position = new Vector3(65f, 0f, 105f),
                    rotation = Quaternion.Euler(0f, -20f, 0f),
                    tintColor = new Color(0.85f, 0.72f, 0.18f, 1f)
                },
                new PropSpawnDef
                {
                    name = "MaritimeBuoy_03_Amber",
                    type = FloatingDebris.DebrisType.MaritimeBuoy,
                    position = new Vector3(-110f, 0f, -15f),
                    rotation = Quaternion.identity,
                    lightColor = new Color(1.0f, 0.70f, 0.15f, 1f),
                    blinkRate = 1.1f
                },
                new PropSpawnDef
                {
                    name = "MaritimeBuoy_04_Crimson",
                    type = FloatingDebris.DebrisType.MaritimeBuoy,
                    position = new Vector3(110f, 0f, 35f),
                    rotation = Quaternion.identity,
                    lightColor = new Color(1.0f, 0.18f, 0.15f, 1f),
                    blinkRate = 1.5f
                },
                new PropSpawnDef
                {
                    name = "MaritimeBuoy_05_Cyan",
                    type = FloatingDebris.DebrisType.MaritimeBuoy,
                    position = new Vector3(-35f, 0f, 125f),
                    rotation = Quaternion.identity,
                    lightColor = new Color(0.15f, 0.90f, 1.0f, 1f),
                    blinkRate = 0.9f
                },
                new PropSpawnDef
                {
                    name = "IndustrialHullPlate_02",
                    type = FloatingDebris.DebrisType.IndustrialWreckage,
                    position = new Vector3(45f, 0f, -85f),
                    rotation = Quaternion.Euler(8f, 65f, -5f)
                },
                new PropSpawnDef
                {
                    name = "CargoCrateCluster_01",
                    type = FloatingDebris.DebrisType.CargoCrateCluster,
                    position = new Vector3(25f, 0f, -50f),
                    rotation = Quaternion.Euler(0f, 40f, 0f),
                    tintColor = new Color(0.48f, 0.36f, 0.24f, 1f)
                },
                new PropSpawnDef
                {
                    name = "CargoCrateCluster_02",
                    type = FloatingDebris.DebrisType.CargoCrateCluster,
                    position = new Vector3(-85f, 0f, -25f),
                    rotation = Quaternion.Euler(0f, -15f, 0f),
                    tintColor = new Color(0.44f, 0.32f, 0.22f, 1f)
                }
            };

            int addedCount = 0;
            foreach (var def in propDefs)
            {
                // Check if already exists under root
                Transform existing = fieldRoot.transform.Find(def.name);
                GameObject propObj;
                if (existing != null)
                {
                    propObj = existing.gameObject;
                }
                else
                {
                    propObj = new GameObject(def.name);
                    propObj.transform.SetParent(fieldRoot.transform, false);
                    Undo.RegisterCreatedObjectUndo(propObj, $"Create {def.name}");
                    addedCount++;
                }

                propObj.transform.position = def.position;
                propObj.transform.rotation = def.rotation;

                var debris = propObj.GetComponent<FloatingDebris>();
                if (debris == null)
                {
                    debris = propObj.AddComponent<FloatingDebris>();
                }

                debris.debrisType = def.type;
                debris.tintColor = def.tintColor != default ? def.tintColor : Color.white;
                debris.waterDamping = 4.5f;
                debris.rotationDamping = 3.5f;
                debris.bobbingAmplitude = 0.38f;
                debris.massSubmergenceDepth = def.type == FloatingDebris.DebrisType.ShippingContainer ? 0.75f : 0.45f;
                debris.driftSpeed = Random.Range(0.45f, 0.75f);
                debris.driftDirection = new Vector2(Random.Range(0.6f, 0.9f), Random.Range(0.3f, 0.6f)).normalized;

                if (def.type == FloatingDebris.DebrisType.MaritimeBuoy)
                {
                    debris.buoyLightColor = def.lightColor;
                    debris.buoyBlinkRate = def.blinkRate;
                }

                debris.BuildDebris();
                EditorUtility.SetDirty(propObj);
            }

            // Also ensure all existing props have colliders configured
            foreach (var debris in fieldRoot.GetComponentsInChildren<FloatingDebris>(true))
            {
                debris.EnsureTriggerColliders();
                EditorUtility.SetDirty(debris.gameObject);
            }

            EditorUtility.SetDirty(fieldRoot);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log($"<color=#00e5ff><b>[FloatingDebrisField]</b></color> Successfully configured floating debris field! Added {addedCount} new floating props (containers, beacon buoys, hull wreckage, cargo crates) spread across the 360-degree oceanic arena.");
        }
    }
}
#endif
