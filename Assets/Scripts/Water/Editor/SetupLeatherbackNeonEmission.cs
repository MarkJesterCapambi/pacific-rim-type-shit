#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace PacificRimWater.Editor
{
    public static class SetupLeatherbackNeonEmission
    {
        [MenuItem("Pacific Rim/Setup Leatherback Neon Emission")]
        public static void ApplyNeonEmission()
        {
            Texture2D emissionTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/leatherback_emission.png");
            if (emissionTex == null)
            {
                Debug.LogWarning("[NeonEmission] Could not load Assets/Materials/leatherback_emission.png");
                return;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_Leatherback.mat");
            Color neonHdr = new Color(0.3f, 3.8f, 4.8f, 1f); // Balanced electric cyan glow

            if (mat != null)
            {
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", emissionTex);
                mat.SetColor("_EmissionColor", neonHdr);
                EditorUtility.SetDirty(mat);
            }

            GameObject kaijuObj = GameObject.Find("Kaiju_Leatherback");
            if (kaijuObj != null)
            {
                var bioCtrl = kaijuObj.GetComponent<KaijuBioluminescenceController>();
                if (bioCtrl == null)
                {
                    bioCtrl = kaijuObj.AddComponent<KaijuBioluminescenceController>();
                }
                bioCtrl.baseEmissionColor = neonHdr;
                bioCtrl.minIntensity = 0.85f;
                bioCtrl.maxIntensity = 1.15f;
                bioCtrl.maxLightIntensity = 1.8f;
                bioCtrl.lightRange = 18f;

                SkinnedMeshRenderer smr = kaijuObj.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    bioCtrl.targetRenderer = smr;
                    if (mat != null)
                    {
                        smr.sharedMaterial = mat;
                    }
                    if (smr.sharedMaterial != null)
                    {
                        smr.sharedMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        smr.sharedMaterial.EnableKeyword("_EMISSION");
                        smr.sharedMaterial.SetTexture("_EmissionMap", emissionTex);
                        smr.sharedMaterial.SetColor("_EmissionColor", neonHdr);
                        EditorUtility.SetDirty(smr.sharedMaterial);
                    }
                    EditorUtility.SetDirty(smr);
                }

                EditorUtility.SetDirty(kaijuObj);
                EditorSceneManager.MarkSceneDirty(kaijuObj.scene);
                Debug.Log("<color=#00FFFF><b>[Pacific Rim] Leatherback Neon Cyan Veins re-tuned to balanced HDR intensity (3.2, 4.2)!</b></color>");
            }

            AssetDatabase.SaveAssets();
        }
    }
}
#endif
