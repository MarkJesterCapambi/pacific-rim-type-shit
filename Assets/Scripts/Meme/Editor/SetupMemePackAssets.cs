#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Video;

namespace PacificRimMeme.Editor
{
    /// <summary>
    /// Configures Assets/MemePack textures, generates URP Lit F-22 materials and prefab,
    /// and bakes Assets/Resources/MemePackCatalog.asset for Editor and standalone builds.
    /// </summary>
    public class SetupMemePackAssets : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1;

        private const string MemePackDir = "Assets/MemePack";
        private const string MaterialsDir = MemePackDir + "/Materials";
        private const string F22FbxPath = MemePackDir + "/f22.fbx";
        private const string F22PrefabPath = MemePackDir + "/F22_Raptor_Meme.prefab";
        private const string FreddyVideoPath = MemePackDir + "/FreddyJumpscare.mp4";
        private const string CatalogAssetPath = "Assets/Resources/MemePackCatalog.asset";

        private const string AlbedoPath = MemePackDir + "/f22_Avión_AlbedoTransparency.png";
        private const string NormalPath = MemePackDir + "/f22_Avión_Normal.png";
        private const string MetallicPath = MemePackDir + "/f22_Avión_MetallicSmoothness.png";
        private const string EmissionPath = MemePackDir + "/f22_Avión_Emission.png";
        private const string CabinaPath = MemePackDir + "/f22_Cabina_AlbedoTransparency.png";

        public void OnPreprocessBuild(BuildReport report)
        {
            BakeAll();
        }

        [MenuItem("Pacific Rim/Meme Pack/Setup & Bake Meme Pack Assets", false, 30)]
        public static void BakeMenu()
        {
            BakeAll();
        }

        public static MemePackCatalog BakeAll()
        {
            ConfigureTextures();
            var (bodyMat, canopyMat) = EnsureMaterials();
            GameObject f22Prefab = BuildF22Prefab(bodyMat, canopyMat);
            MemePackCatalog catalog = BakeCatalog(f22Prefab);
            Debug.Log("<color=#00e5ff><b>[MemePack]</b></color> Configured F-22 materials/prefab and baked MemePackCatalog into Assets/Resources.");
            return catalog;
        }

        private static void ConfigureTextures()
        {
            var normalImporter = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.sRGBTexture = false;
                normalImporter.SaveAndReimport();
            }

            var metalImporter = AssetImporter.GetAtPath(MetallicPath) as TextureImporter;
            if (metalImporter != null && metalImporter.sRGBTexture)
            {
                metalImporter.sRGBTexture = false;
                metalImporter.SaveAndReimport();
            }
        }

        private static (Material bodyMat, Material canopyMat) EnsureMaterials()
        {
            if (!Directory.Exists(MaterialsDir))
            {
                Directory.CreateDirectory(MaterialsDir);
                AssetDatabase.Refresh();
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Texture2D albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            Texture2D metalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);
            Texture2D emissionTex = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);
            Texture2D cabinaTex = AssetDatabase.LoadAssetAtPath<Texture2D>(CabinaPath);

            string bodyMatPath = MaterialsDir + "/F22_Fuselage.mat";
            Material bodyMat = AssetDatabase.LoadAssetAtPath<Material>(bodyMatPath);
            if (bodyMat == null)
            {
                bodyMat = new Material(urpLit) { name = "F22_Fuselage" };
                AssetDatabase.CreateAsset(bodyMat, bodyMatPath);
            }
            else
            {
                bodyMat.shader = urpLit;
            }

            bodyMat.color = Color.white;
            if (bodyMat.HasProperty("_BaseColor")) bodyMat.SetColor("_BaseColor", Color.white);
            if (bodyMat.HasProperty("_BaseMap")) bodyMat.SetTexture("_BaseMap", albedoTex);
            bodyMat.mainTexture = albedoTex;

            if (normalTex != null && bodyMat.HasProperty("_BumpMap"))
            {
                bodyMat.SetTexture("_BumpMap", normalTex);
                if (bodyMat.HasProperty("_BumpScale")) bodyMat.SetFloat("_BumpScale", 1.0f);
                bodyMat.EnableKeyword("_NORMALMAP");
            }

            if (metalTex != null && bodyMat.HasProperty("_MetallicGlossMap"))
            {
                bodyMat.SetTexture("_MetallicGlossMap", metalTex);
                if (bodyMat.HasProperty("_Metallic")) bodyMat.SetFloat("_Metallic", 1.0f);
                if (bodyMat.HasProperty("_Smoothness")) bodyMat.SetFloat("_Smoothness", 0.72f);
                bodyMat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            if (emissionTex != null && bodyMat.HasProperty("_EmissionMap"))
            {
                bodyMat.SetTexture("_EmissionMap", emissionTex);
                bodyMat.SetColor("_EmissionColor", new Color(3.8f, 1.45f, 0.35f, 1f));
                bodyMat.EnableKeyword("_EMISSION");
                bodyMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(bodyMat);

            string canopyMatPath = MaterialsDir + "/F22_Canopy.mat";
            Material canopyMat = AssetDatabase.LoadAssetAtPath<Material>(canopyMatPath);
            if (canopyMat == null)
            {
                canopyMat = new Material(urpLit) { name = "F22_Canopy" };
                AssetDatabase.CreateAsset(canopyMat, canopyMatPath);
            }
            else
            {
                canopyMat.shader = urpLit;
            }

            canopyMat.color = new Color(1.0f, 0.88f, 0.62f, 1f);
            if (canopyMat.HasProperty("_BaseColor")) canopyMat.SetColor("_BaseColor", new Color(1.0f, 0.88f, 0.62f, 1f));
            if (canopyMat.HasProperty("_BaseMap")) canopyMat.SetTexture("_BaseMap", cabinaTex);
            canopyMat.mainTexture = cabinaTex;
            if (canopyMat.HasProperty("_Metallic")) canopyMat.SetFloat("_Metallic", 0.85f);
            if (canopyMat.HasProperty("_Smoothness")) canopyMat.SetFloat("_Smoothness", 0.92f);
            EditorUtility.SetDirty(canopyMat);

            return (bodyMat, canopyMat);
        }

        private static GameObject BuildF22Prefab(Material bodyMat, Material canopyMat)
        {
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(F22FbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[MemePack] Missing F-22 FBX at '{F22FbxPath}'");
                return null;
            }

            GameObject root = new GameObject("F22_Raptor_Meme");
            try
            {
                // Preserve the FBX root's native (270,0,0) rot & (1.31,1,1) scale under a pivot wrapper
                // so +Z is forward nose, +Y is up, +X is right, ~8.6m total length
                GameObject pivot = new GameObject("F22_Pivot");
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.localPosition = new Vector3(0f, -0.45f, -1.20f);
                pivot.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                pivot.transform.localScale = new Vector3(3.0f, 3.0f, 3.0f);

                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
                model.name = "F22_Model";
                model.transform.SetParent(pivot.transform, false);

                foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // Submesh 0 = Avion fuselage, Submesh 1 = Cabina cockpit canopy
                    mr.sharedMaterials = new[] { bodyMat, canopyMat };
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    mr.receiveShadows = true;
                }

                // Standardized combat & VFX sockets (parent local space: +Z nose, +Y up, +X right)
                CreateSocket(root.transform, "Exhaust_L", new Vector3(-0.42f, 0.03f, -3.84f));
                CreateSocket(root.transform, "Exhaust_R", new Vector3(0.42f, 0.03f, -3.84f));
                CreateSocket(root.transform, "WingTip_L", new Vector3(-3.42f, -0.09f, -1.65f));
                CreateSocket(root.transform, "WingTip_R", new Vector3(3.42f, -0.09f, -1.65f));
                CreateSocket(root.transform, "Hardpoint_L", new Vector3(-1.65f, -0.28f, -0.65f));
                CreateSocket(root.transform, "Hardpoint_R", new Vector3(1.65f, -0.28f, -0.65f));
                CreateSocket(root.transform, "GunMuzzle", new Vector3(0.32f, -0.08f, 2.25f));

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, F22PrefabPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateSocket(Transform parent, string socketName, Vector3 localPos)
        {
            var go = new GameObject(socketName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
        }

        private static MemePackCatalog BakeCatalog(GameObject f22Prefab)
        {
            const string resourcesDir = "Assets/Resources";
            if (!Directory.Exists(resourcesDir))
            {
                Directory.CreateDirectory(resourcesDir);
                AssetDatabase.Refresh();
            }

            var catalog = AssetDatabase.LoadAssetAtPath<MemePackCatalog>(CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MemePackCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            catalog.freddyJumpscareClip = AssetDatabase.LoadAssetAtPath<VideoClip>(FreddyVideoPath);
            catalog.f22RaptorPrefab = f22Prefab != null
                ? f22Prefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(F22PrefabPath);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return catalog;
        }
    }
}
#endif
