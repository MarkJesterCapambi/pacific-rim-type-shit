using UnityEngine;
using UnityEditor;
using System.IO;

namespace PacificRimCutscene.Editor
{
    public static class SetupPacificRimDeploymentCutscene
    {
        [MenuItem("Pacific Rim/Setup Deployment Cutscene & Chinooks")]
        public static void Setup()
        {
            // 1. Ensure Materials Directory
            string matDir = "Assets/Materials";
            if (!Directory.Exists(matDir)) Directory.CreateDirectory(matDir);

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // Textures
            Texture2D metalNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/Metal055A_2K-JPG_NormalGL_53.jpeg");
            Texture2D metalAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/Metal055A_2K-JPG_Color_54.jpeg");

            // Material 1: Hull (Military Gunmetal / Tactical Olive Drab)
            string hullMatPath = "Assets/Materials/PacificRim_ChinookHull.mat";
            Material hullMat = AssetDatabase.LoadAssetAtPath<Material>(hullMatPath);
            if (hullMat == null)
            {
                hullMat = new Material(litShader);
                AssetDatabase.CreateAsset(hullMat, hullMatPath);
            }
            hullMat.color = new Color(0.24f, 0.28f, 0.25f, 1f); // Tactical military olive gunmetal
            hullMat.SetFloat("_Metallic", 0.72f);
            hullMat.SetFloat("_Smoothness", 0.62f);
            if (metalNormal != null)
            {
                hullMat.SetTexture("_BumpMap", metalNormal);
                hullMat.EnableKeyword("_NORMALMAP");
            }
            if (metalAlbedo != null)
            {
                hullMat.SetTexture("_BaseMap", metalAlbedo);
            }
            EditorUtility.SetDirty(hullMat);

            // Material 2: Rotors (Matte carbon composite)
            string rotorMatPath = "Assets/Materials/PacificRim_ChinookRotors.mat";
            Material rotorMat = AssetDatabase.LoadAssetAtPath<Material>(rotorMatPath);
            if (rotorMat == null)
            {
                rotorMat = new Material(litShader);
                AssetDatabase.CreateAsset(rotorMat, rotorMatPath);
            }
            rotorMat.color = new Color(0.12f, 0.13f, 0.14f, 1f);
            rotorMat.SetFloat("_Metallic", 0.35f);
            rotorMat.SetFloat("_Smoothness", 0.28f);
            EditorUtility.SetDirty(rotorMat);

            // Material 3: Engines / Mechanical Joints
            string engineMatPath = "Assets/Materials/PacificRim_ChinookEngines.mat";
            Material engineMat = AssetDatabase.LoadAssetAtPath<Material>(engineMatPath);
            if (engineMat == null)
            {
                engineMat = new Material(litShader);
                AssetDatabase.CreateAsset(engineMat, engineMatPath);
            }
            engineMat.color = new Color(0.18f, 0.19f, 0.21f, 1f);
            engineMat.SetFloat("_Metallic", 0.88f);
            engineMat.SetFloat("_Smoothness", 0.70f);
            EditorUtility.SetDirty(engineMat);

            // Material 4: Wheels / Landing Gear Rubber
            string wheelMatPath = "Assets/Materials/PacificRim_ChinookWheels.mat";
            Material wheelMat = AssetDatabase.LoadAssetAtPath<Material>(wheelMatPath);
            if (wheelMat == null)
            {
                wheelMat = new Material(litShader);
                AssetDatabase.CreateAsset(wheelMat, wheelMatPath);
            }
            wheelMat.color = new Color(0.08f, 0.08f, 0.09f, 1f);
            wheelMat.SetFloat("_Metallic", 0.1f);
            wheelMat.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(wheelMat);

            // Material 5: Steel Suspension Cables
            string cableMatPath = "Assets/Materials/PacificRim_CableWire.mat";
            Material cableMat = AssetDatabase.LoadAssetAtPath<Material>(cableMatPath);
            if (cableMat == null)
            {
                cableMat = new Material(litShader);
                AssetDatabase.CreateAsset(cableMat, cableMatPath);
            }
            cableMat.color = new Color(0.20f, 0.22f, 0.25f, 1f);
            cableMat.SetFloat("_Metallic", 0.85f);
            cableMat.SetFloat("_Smoothness", 0.65f);
            EditorUtility.SetDirty(cableMat);

            AssetDatabase.SaveAssets();

            // 2. Build Configured Chinook Prefab
            string prefabDir = "Assets/Prefabs";
            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);

            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Chinook.fbx");
            if (fbx == null)
            {
                Debug.LogError("[CutsceneSetup] Assets/Models/Chinook.fbx not found!");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            instance.name = "PacificRim_Chinook";

            // Scale to match Pacific Rim titan proportions: CH-47 is ~30m long.
            // Let's check mesh size
            var bodyRend = instance.transform.Find("Body")?.GetComponent<MeshRenderer>();
            if (bodyRend != null)
            {
                float length = bodyRend.bounds.size.z;
                if (length < 5f)
                {
                    // Scale up if imported in cm or small units
                    instance.transform.localScale = Vector3.one * 3.5f;
                }
            }

            // Assign Materials to Sub-Meshes
            foreach (var r in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name.ToLower();
                if (n.Contains("body"))
                {
                    r.sharedMaterial = hullMat;
                }
                else if (n.Contains("rotorblade"))
                {
                    r.sharedMaterial = rotorMat;
                }
                else if (n.Contains("engine") || n.Contains("rotorbase"))
                {
                    r.sharedMaterial = engineMat;
                }
                else if (n.Contains("wheel"))
                {
                    r.sharedMaterial = wheelMat;
                }
                else
                {
                    r.sharedMaterial = hullMat;
                }
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            // Add Cargo Hook Anchor
            var hookGo = new GameObject("CargoHook");
            hookGo.transform.SetParent(instance.transform, false);
            hookGo.transform.localPosition = new Vector3(0f, -0.65f, 0f);

            // Add Searchlight (front underside of cockpit at -Z, facing forward/down)
            var lightGo = new GameObject("ChinookSearchlight");
            lightGo.transform.SetParent(instance.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.5f, -2.4f);
            lightGo.transform.localRotation = Quaternion.Euler(70f, 180f, 0f); // angled down-forward towards -Z
            var spot = lightGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(0.92f, 0.96f, 1.0f);
            spot.range = 120f;
            spot.spotAngle = 48f;
            spot.innerSpotAngle = 26f;
            spot.intensity = 3200f;
            spot.shadows = LightShadows.None;

            // Add Strobe Light on rear pylon (rear at +Z)
            var strobeGo = new GameObject("AviationStrobe");
            strobeGo.transform.SetParent(instance.transform, false);
            strobeGo.transform.localPosition = new Vector3(0f, 1.3f, 2.3f);
            var strobe = strobeGo.AddComponent<Light>();
            strobe.type = LightType.Point;
            strobe.color = Color.white;
            strobe.range = 25f;
            strobe.intensity = 4500f;
            strobe.shadows = LightShadows.None;

            // Add ChinookRotorController
            var rotorCtrl = instance.AddComponent<ChinookRotorController>();
            rotorCtrl.rotorBladeFront = instance.transform.Find("RotorBladeFront");
            rotorCtrl.rotorBladeBack = instance.transform.Find("RotorBladeBack");
            rotorCtrl.cargoHook = hookGo.transform;
            rotorCtrl.searchlight = spot;
            rotorCtrl.strobeLight = strobe;
            rotorCtrl.rotorRpm = 1100f;

            Debug.Log($"<color=#00ff88>[Chinook Rotors]</color> Front Rotor LocalPos: {(rotorCtrl.rotorBladeFront != null ? rotorCtrl.rotorBladeFront.localPosition.ToString("F3") : "null")}, Back Rotor LocalPos: {(rotorCtrl.rotorBladeBack != null ? rotorCtrl.rotorBladeBack.localPosition.ToString("F3") : "null")}");

            // Save Prefab
            string prefabPath = "Assets/Prefabs/PacificRim_Chinook.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);

            // 3. Ensure Cutscene Manager exists in the Scene
            var cutsceneGo = GameObject.Find("PacificRim_DeploymentCutscene");
            if (cutsceneGo == null)
            {
                cutsceneGo = new GameObject("PacificRim_DeploymentCutscene");
            }

            var cutscene = cutsceneGo.GetComponent<PacificRimDeploymentCutscene>();
            if (cutscene == null)
            {
                cutscene = cutsceneGo.AddComponent<PacificRimDeploymentCutscene>();
            }

            cutscene.chinookPrefab = savedPrefab;
            cutscene.cableMaterial = cableMat;
            cutscene.ResolveReferences();

            EditorUtility.SetDirty(cutsceneGo);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cutsceneGo.scene);

            Debug.Log("<color=#00e5ff><b>[DeploymentCutscene]</b></color> Successfully created Chinook tactical materials, configured PacificRim_Chinook.prefab, and wired deployment cutscene!");
        }
    }
}
