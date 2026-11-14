#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PacificRimWater.Editor
{
    public static class SetupPacificRimOceanScene
    {
        [MenuItem("Pacific Rim/Setup Ocean Wading Scene", false, 10)]
        public static void CreatePacificRimOceanScene()
        {
            // Remove previous setup if existing
            GameObject existing = GameObject.Find("--- Pacific Rim Ocean Scene ---");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject root = new GameObject("--- Pacific Rim Ocean Scene ---");
            Undo.RegisterCreatedObjectUndo(root, "Setup Pacific Rim Ocean Scene");

            // 1. Create and verify all Materials (fixes all pink shader issues)
            Material oceanMat = EnsureOceanMaterial();
            Material seabedMat = EnsureSeabedMaterial();
            Material jaegerMat = EnsureJaegerMaterial();
            Material kaijuMat = EnsureKaijuMaterial();

            // 2. Splash Manager
            GameObject splashMgrObj = new GameObject("WaterSplashManager");
            splashMgrObj.transform.SetParent(root.transform);
            splashMgrObj.AddComponent<WaterSplashManager>();

            // 3. Jaeger Mech Character (Height = 16m)
            GameObject jaeger = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            jaeger.name = "Jaeger_Mech";
            jaeger.transform.SetParent(root.transform);
            jaeger.transform.localScale = new Vector3(4f, 8f, 4f); // 16m tall mech
            jaeger.transform.position = new Vector3(0f, 6.0f, 0f);

            Renderer jaegerRend = jaeger.GetComponent<Renderer>();
            if (jaegerRend != null && jaegerMat != null)
            {
                jaegerRend.sharedMaterial = jaegerMat;
            }

            Rigidbody jaegerRb = jaeger.GetComponent<Rigidbody>();
            if (jaegerRb == null) jaegerRb = jaeger.AddComponent<Rigidbody>();
            jaegerRb.mass = 2000f; // 2,000 tons
            jaegerRb.freezeRotation = true;

            // Character controller
            SimpleJaegerController controller = jaeger.AddComponent<SimpleJaegerController>();
            controller.walkSpeed = 8.5f;
            controller.turnSpeed = 55f;
            controller.acceleration = 2.5f;

            // Wading interaction
            JaegerWaterInteraction interaction = jaeger.AddComponent<JaegerWaterInteraction>();
            interaction.footstepWaveRadius = 10.0f;
            interaction.footstepWaveStrength = 1.6f;
            interaction.strideDistance = 6.0f;
            interaction.footSpacing = 3.5f;
            interaction.legWidth = 3.0f;
            interaction.footstepShakeIntensity = 0.45f;
            interaction.oceanWadingDrag = 4.0f;

            // 4. Kaiju Capsule Enemy (Height = 20m, 60m ahead in the ocean)
            GameObject kaiju = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            kaiju.name = "Kaiju_Category_IV";
            kaiju.transform.SetParent(root.transform);
            kaiju.transform.localScale = new Vector3(5.5f, 10f, 5.5f); // 20m tall titan
            kaiju.transform.position = new Vector3(0f, 6.5f, 75f);
            kaiju.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Facing the Jaeger

            Renderer kaijuRend = kaiju.GetComponent<Renderer>();
            if (kaijuRend != null && kaijuMat != null)
            {
                kaijuRend.sharedMaterial = kaijuMat;
            }

            Rigidbody kaijuRb = kaiju.GetComponent<Rigidbody>();
            if (kaijuRb == null) kaijuRb = kaiju.AddComponent<Rigidbody>();
            kaijuRb.mass = 3500f; // 3,500 tons
            kaijuRb.freezeRotation = true;

            // Kaiju AI
            KaijuAI kaijuAI = kaiju.AddComponent<KaijuAI>();
            kaijuAI.target = jaeger.transform;
            kaijuAI.wadeSpeed = 7.0f;
            kaijuAI.turnSpeed = 45f;
            kaijuAI.attackRange = 22f;

            // Kaiju Water Interaction (creates giant tidal waves as it stalks toward you)
            JaegerWaterInteraction kaijuWater = kaiju.AddComponent<JaegerWaterInteraction>();
            kaijuWater.footstepWaveRadius = 14.0f;
            kaijuWater.footstepWaveStrength = 2.2f;
            kaijuWater.strideDistance = 7.5f;
            kaijuWater.footSpacing = 4.5f;
            kaijuWater.legWidth = 4.0f;
            kaijuWater.footstepShakeIntensity = 0.35f;

            // 5. Ocean Surface (6x BIGGER: 960m x 960m expansive ocean!)
            GameObject oceanObj = new GameObject("Pacific Rim Ocean Surface");
            oceanObj.transform.SetParent(root.transform);
            oceanObj.transform.position = new Vector3(0f, 6.0f, 0f); // Water level at Y = 6 (waist deep)

            WaterSystem waterSystem = oceanObj.AddComponent<WaterSystem>();
            waterSystem.followTarget = jaeger.transform;
            waterSystem.simAreaSize = 260f;
            waterSystem.simResolution = 110;
            waterSystem.swellAmplitude1 = 1.35f;
            waterSystem.swellWavelength1 = 55f;
            waterSystem.swellSpeed1 = 1.6f;
            waterSystem.swellSteepness1 = 0.55f;

            OceanWaterMesh oceanMesh = oceanObj.AddComponent<OceanWaterMesh>();
            oceanMesh.meshWidth = 960f;
            oceanMesh.meshLength = 960f;
            oceanMesh.resolution = 110;

            MeshRenderer mr = oceanObj.GetComponent<MeshRenderer>();
            if (mr != null && oceanMat != null)
            {
                mr.sharedMaterial = oceanMat;
            }

            oceanMesh.InitMesh();

            // 6. Seabed / Floor (6x BIGGER: 1500m x 1500m vast seabed)
            GameObject seabed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seabed.name = "Seabed_Floor";
            seabed.transform.SetParent(root.transform);
            seabed.transform.position = new Vector3(0f, -0.5f, 0f);
            seabed.transform.localScale = new Vector3(1500f, 1f, 1500f);
            seabed.tag = "Ground";

            Renderer sRend = seabed.GetComponent<Renderer>();
            if (sRend != null && seabedMat != null)
            {
                sRend.sharedMaterial = seabedMat;
            }

            // 7. Camera: Connected to the Mech via smooth JaegerCameraFollow
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                mainCam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
                camObj.tag = "MainCamera";
            }

            mainCam.transform.SetParent(null);
            mainCam.transform.localScale = Vector3.one;

            JaegerCameraFollow camFollow = mainCam.GetComponent<JaegerCameraFollow>();
            if (camFollow == null)
            {
                camFollow = mainCam.gameObject.AddComponent<JaegerCameraFollow>();
            }
            camFollow.target = jaeger.transform;
            camFollow.targetOffset = new Vector3(0f, 4.0f, 0f);
            camFollow.distance = 32f;
            camFollow.height = 15f;
            camFollow.followDamping = 6f;

            Vector3 initCamPos = jaeger.transform.position - (jaeger.transform.forward * 32f) + Vector3.up * 15f;
            mainCam.transform.position = initCamPos;
            mainCam.transform.LookAt(jaeger.transform.position + new Vector3(0f, 4f, 0f));

            // 8. Directional Light (Dramatic stormy ocean lighting)
            Light dirLight = Object.FindAnyObjectByType<Light>();
            if (dirLight != null && dirLight.type == LightType.Directional)
            {
                dirLight.transform.rotation = Quaternion.Euler(38f, -40f, 0f);
                dirLight.color = new Color(0.72f, 0.84f, 0.96f);
                dirLight.intensity = 1.2f;
            }

            Selection.activeGameObject = jaeger;
            Debug.Log("<color=#00e5ff><b>[Pacific Rim Water]</b></color> 6x Expansive Ocean Arena & Kaiju AI spawned! Stride into battle!");
        }

        public static Material EnsureOceanMaterial()
        {
            EnsureFolder("Assets/Materials");
            string path = "Assets/Materials/PacificRim_Ocean.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader oceanShader = Shader.Find("PacificRim/OceanWater");
            if (oceanShader == null)
            {
                oceanShader = Shader.Find("Universal Render Pipeline/Lit");
            }
            if (oceanShader == null)
            {
                oceanShader = Shader.Find("Standard");
            }

            if (mat == null)
            {
                mat = new Material(oceanShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = oceanShader;
            }

            mat.name = "PacificRim_Ocean";

            if (mat.HasProperty("_DeepColor"))
                mat.SetColor("_DeepColor", new Color(0.015f, 0.055f, 0.12f, 0.98f));
            if (mat.HasProperty("_ShallowColor"))
                mat.SetColor("_ShallowColor", new Color(0.04f, 0.18f, 0.32f, 0.88f));
            if (mat.HasProperty("_FoamColor"))
                mat.SetColor("_FoamColor", new Color(0.92f, 0.96f, 1.0f, 0.95f));
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.93f);

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", new Color(0.03f, 0.14f, 0.28f, 0.92f));
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Smoothness", 0.92f);
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        public static Material EnsureSeabedMaterial()
        {
            EnsureFolder("Assets/Materials");
            string path = "Assets/Materials/PacificRim_Seabed.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null)
            {
                mat = new Material(s);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = s;
            }

            mat.name = "PacificRim_Seabed";
            Color darkSeabed = new Color(0.07f, 0.10f, 0.13f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", darkSeabed);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", darkSeabed);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        public static Material EnsureJaegerMaterial()
        {
            EnsureFolder("Assets/Materials");
            string path = "Assets/Materials/PacificRim_JaegerArmor.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null)
            {
                mat = new Material(s);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = s;
            }

            mat.name = "PacificRim_JaegerArmor";
            Color armorColor = new Color(0.22f, 0.26f, 0.30f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", armorColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", armorColor);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.88f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.70f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        public static Material EnsureKaijuMaterial()
        {
            EnsureFolder("Assets/Materials");
            string path = "Assets/Materials/PacificRim_Kaiju.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (mat == null)
            {
                mat = new Material(s);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = s;
            }

            mat.name = "PacificRim_Kaiju";
            // Dark obsidian scaly skin with glowing bioluminescent blue emission
            Color kaijuSkin = new Color(0.08f, 0.09f, 0.11f);
            Color bioluminescentCyan = new Color(0.0f, 0.75f, 1.0f);

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", kaijuSkin);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", kaijuSkin);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", bioluminescentCyan * 1.5f);
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }
    }
}
#endif
