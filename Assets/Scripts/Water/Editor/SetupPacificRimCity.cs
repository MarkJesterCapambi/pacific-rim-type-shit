#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PacificRimWater.Editor
{
    public static class SetupPacificRimCity
    {
        private const string FbxPath = "Assets/Models/Buildings.fbx";
        private const string CityMatFolder = "Assets/Materials/City";
        
        // City Deck elevated to 18m: 5m above ocean base water (Y=13m).
        // Suimono max wave crests reach ~15.5m and foam ~16.5m, so Y=18.0m eliminates ALL water foam clipping!
        private const float CityDeckY = 18.0f;
        
        // Road modular parameters (scale = 0.80)
        private const float RoadScale = 0.80f;
        private const float RoadWidth = 37.92f * RoadScale; // 30.336m
        private const float RoadStraight1X = 146.30f * RoadScale; // 117.04m
        private const float BlockPitch = RoadWidth + RoadStraight1X; // 147.376m
        
        // Building uniform scale: tallest building (Bldg9, raw height 112.10m) scales to 21.0m,
        // slightly smaller than Gipsy (24.56m) and Leatherback (26.0m)
        private const float BuildingUniformScale = 0.1873f;

        private struct BuildingMeshInfo
        {
            public string name;
            public Vector3 tCenter;
            public Vector3 tSize;
            public float minY;
        }

        public static void InspectBuildingsFbx()
        {
            Debug.Log("[City Setup] InspectBuildingsFbx called");
        }

        [MenuItem("Pacific Rim/Setup Pacific Rim City & Grand Ramp", false, 12)]
        public static void BuildCity()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            Debug.Log($"<color=#00e5ff><b>[City Setup]</b></color> Starting Modular City & Ramp generation in scene: {activeScene.name}");

            // 1. Ensure all URP Lit City Materials are loaded/created
            var matCatalog = EnsureCityMaterials();

            // 2. Load Buildings FBX prefab
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[City Setup] Failed to load FBX at {FbxPath}");
                return;
            }

            // 3. Cache building mesh dimensions & centers
            var bldgInfoMap = AnalyzeBuildingMeshes(fbx);

            // 4. Create or replace root container
            string rootName = "--- Pacific Rim City ---";
            GameObject oldRoot = GameObject.Find(rootName);
            if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot);

            GameObject cityRoot = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(cityRoot, "Create Pacific Rim City");
            cityRoot.transform.position = Vector3.zero;
            cityRoot.transform.rotation = Quaternion.identity;

            // 5. Build Solid Concrete Harbor Platform Deck & Seawalls (Y = 18m)
            BuildHarborPlatformAndSeawall(cityRoot.transform, matCatalog);

            // 6. Build Grand Combat Entrance Ramp (Ocean seabed Y=0 to City deck Y=18)
            BuildGrandCombatRamp(cityRoot.transform, matCatalog);

            // 7. Build Modular Road Network (using FBX modular pieces)
            BuildModularRoadNetwork(cityRoot.transform, fbx, matCatalog);

            // 8. Build 6 City Blocks with sidewalk bases and centered modular buildings
            BuildModularCityBlocks(cityRoot.transform, fbx, bldgInfoMap, matCatalog);

            // 9. Mark scene dirty & save
            EditorSceneManager.MarkSceneDirty(activeScene);
            bool saved = EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"<color=#00ff88><b>[City Setup]</b></color> Successfully constructed Pacific Rim Modular City, Grand Ramp, and saved scene: {saved}!");
        }

        #region Material Pipeline
        public static Dictionary<string, Material> EnsureCityMaterials()
        {
            var catalog = new Dictionary<string, Material>();
            if (!AssetDatabase.IsValidFolder(CityMatFolder))
            {
                AssetDatabase.CreateFolder("Assets/Materials", "City");
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Material GetOrCreateMat(string matName, string textureSearchPattern, Color defaultColor, float smoothness = 0.25f, float metallic = 0.05f)
            {
                string matPath = $"{CityMatFolder}/{matName}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(urpLit);
                    mat.name = matName;
                    mat.color = defaultColor;
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

                    string[] guids = AssetDatabase.FindAssets(textureSearchPattern + " t:Texture2D", new[] { "Assets/Materials" });
                    if (guids.Length > 0)
                    {
                        string texPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                        if (tex != null)
                        {
                            mat.mainTexture = tex;
                            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                        }
                    }
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                catalog[matName] = mat;
                return mat;
            }

            GetOrCreateMat("City_Concrete", "simple concrete 1_base_color_4096", new Color(0.55f, 0.55f, 0.53f), 0.15f, 0.05f);
            GetOrCreateMat("City_Road", "City_Road_mat_base_color_4096", new Color(0.20f, 0.20f, 0.22f), 0.35f, 0.10f);
            GetOrCreateMat("City_Intersection", "City_Intersection_mat_base_color_4096", new Color(0.20f, 0.20f, 0.22f), 0.35f, 0.10f);
            GetOrCreateMat("City_Curb", "City_Curb_base_color_4096", new Color(0.60f, 0.60f, 0.58f), 0.20f, 0.05f);
            GetOrCreateMat("City_BuildingFacade", "Building_02_basecolor_4096", new Color(0.65f, 0.65f, 0.65f), 0.35f, 0.20f);
            GetOrCreateMat("City_LR_Facades", "LR_Facades_base_color_4096", new Color(0.70f, 0.68f, 0.65f), 0.30f, 0.10f);
            GetOrCreateMat("City_Glass", "Glass_base_color_4096", new Color(0.15f, 0.35f, 0.45f, 0.9f), 0.90f, 0.30f);
            GetOrCreateMat("City_Roof01", "roof 1_base_color_4096", new Color(0.30f, 0.30f, 0.32f), 0.20f, 0.05f);
            GetOrCreateMat("City_Roof02", "roof 2_base_color_4096", new Color(0.25f, 0.25f, 0.26f), 0.20f, 0.05f);
            GetOrCreateMat("City_Roof03", "roof 3_base_color_4096", new Color(0.35f, 0.33f, 0.30f), 0.20f, 0.05f);
            GetOrCreateMat("City_Metal", "textured metal_base_color_4096", new Color(0.35f, 0.36f, 0.38f), 0.45f, 0.70f);
            GetOrCreateMat("City_DarkMetalRough", "basic rough metal dark _base_color_4096", new Color(0.22f, 0.22f, 0.24f), 0.25f, 0.60f);
            GetOrCreateMat("City_DarkGreenMetal", "basic dark green_base_color_4096", new Color(0.18f, 0.28f, 0.22f), 0.30f, 0.40f);
            GetOrCreateMat("City_Firescape", "firescape_base_color_4096", new Color(0.28f, 0.28f, 0.30f), 0.30f, 0.50f);
            GetOrCreateMat("City_AC", "AC_base_color_4096", new Color(0.50f, 0.50f, 0.52f), 0.35f, 0.30f);
            GetOrCreateMat("City_Vent", "Vent_base_color_4096", new Color(0.38f, 0.38f, 0.40f), 0.35f, 0.50f);
            GetOrCreateMat("City_Solar", "solar_base_color_4096", new Color(0.10f, 0.15f, 0.28f), 0.80f, 0.20f);
            GetOrCreateMat("City_YellowStone", "yellow stone_base_color_4096", new Color(0.72f, 0.65f, 0.50f), 0.20f, 0.05f);
            GetOrCreateMat("City_MarbleTiles", "white marble tiles_base_color_4096", new Color(0.85f, 0.85f, 0.83f), 0.50f, 0.05f);
            GetOrCreateMat("City_RooftopTank", "rooftop_tank_base_color_4096", new Color(0.45f, 0.42f, 0.38f), 0.30f, 0.10f);
            GetOrCreateMat("City_CarSedan", "Body_06_basecolor_4096", new Color(0.60f, 0.12f, 0.12f), 0.75f, 0.40f);
            GetOrCreateMat("City_CarSUV", "Body_08_basecolor_4096", new Color(0.18f, 0.35f, 0.55f), 0.75f, 0.40f);

            return catalog;
        }

        public static Material ResolveMaterial(string origMatName, Dictionary<string, Material> catalog)
        {
            if (string.IsNullOrEmpty(origMatName)) return catalog["City_Concrete"];
            origMatName = origMatName.ToLowerInvariant();

            if (origMatName.Contains("intersection")) return catalog["City_Intersection"];
            if (origMatName.Contains("road")) return catalog["City_Road"];
            if (origMatName.Contains("curb")) return catalog["City_Curb"];
            if (origMatName.Contains("facade")) return origMatName.Contains("lr") ? catalog["City_LR_Facades"] : catalog["City_BuildingFacade"];
            if (origMatName.Contains("glass")) return catalog["City_Glass"];
            if (origMatName.Contains("roof.001") || origMatName.Contains("roof 1")) return catalog["City_Roof01"];
            if (origMatName.Contains("roof.002") || origMatName.Contains("roof 2")) return catalog["City_Roof02"];
            if (origMatName.Contains("roof")) return catalog["City_Roof03"];
            if (origMatName.Contains("dark green")) return catalog["City_DarkGreenMetal"];
            if (origMatName.Contains("firescape")) return catalog["City_Firescape"];
            if (origMatName.Contains("ac")) return catalog["City_AC"];
            if (origMatName.Contains("vent")) return catalog["City_Vent"];
            if (origMatName.Contains("solar")) return catalog["City_Solar"];
            if (origMatName.Contains("stone")) return catalog["City_YellowStone"];
            if (origMatName.Contains("marble")) return catalog["City_MarbleTiles"];
            if (origMatName.Contains("tank")) return catalog["City_RooftopTank"];
            if (origMatName.Contains("rough metal") || origMatName.Contains("black")) return catalog["City_DarkMetalRough"];
            if (origMatName.Contains("metal")) return catalog["City_Metal"];
            if (origMatName.Contains("body")) return origMatName.Contains("08") ? catalog["City_CarSUV"] : catalog["City_CarSedan"];
            if (origMatName.Contains("concrete")) return catalog["City_Concrete"];

            return catalog["City_Concrete"];
        }
        #endregion

        #region Geometry Analysis
        private static Dictionary<string, BuildingMeshInfo> AnalyzeBuildingMeshes(GameObject fbx)
        {
            var map = new Dictionary<string, BuildingMeshInfo>();
            for (int b = 1; b <= 12; b++)
            {
                string bname = $"Bldg{b}";
                Transform tr = fbx.transform.Find(bname);
                if (tr == null) continue;

                Mesh mesh = tr.GetComponent<MeshFilter>().sharedMesh;
                GameObject tempGO = new GameObject("temp_measure");
                tempGO.transform.rotation = tr.localRotation;
                tempGO.transform.localScale = tr.localScale;

                Vector3 min = mesh.bounds.min;
                Vector3 max = mesh.bounds.max;
                Vector3[] corners = new Vector3[] {
                    new Vector3(min.x, min.y, min.z),
                    new Vector3(max.x, min.y, min.z),
                    new Vector3(min.x, max.y, min.z),
                    new Vector3(max.x, max.y, min.z),
                    new Vector3(min.x, min.y, max.z),
                    new Vector3(max.x, min.y, max.z),
                    new Vector3(min.x, max.y, max.z),
                    new Vector3(max.x, max.y, max.z)
                };

                Vector3 tMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 tMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 pt = tempGO.transform.TransformPoint(corners[i]);
                    tMin = Vector3.Min(tMin, pt);
                    tMax = Vector3.Max(tMax, pt);
                }
                Object.DestroyImmediate(tempGO);

                Vector3 tCenter = (tMin + tMax) * 0.5f;
                Vector3 tSize = tMax - tMin;

                map[bname] = new BuildingMeshInfo {
                    name = bname,
                    tCenter = tCenter,
                    tSize = tSize,
                    minY = tMin.y
                };
            }
            return map;
        }
        #endregion

        #region Platform & Ramp
        private static void BuildHarborPlatformAndSeawall(Transform parent, Dictionary<string, Material> mats)
        {
            GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "City_Concrete_Harbor_Platform";
            deck.transform.SetParent(parent, false);
            deck.tag = "Ground";
            deck.layer = 0;

            // Platform: X from 145 to 505 (width 360m), Z from -95 to 405 (length 500m)
            // Center: X = 325m, Z = 155m. Top surface at Y = 18.0m, Height = 20m (sunk to Y = -2.0m)
            deck.transform.position = new Vector3(325f, CityDeckY - 10f, 155f);
            deck.transform.rotation = Quaternion.identity;
            deck.transform.localScale = new Vector3(360f, 20f, 500f);

            var mr = deck.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mats["City_Concrete"];

            // Waterfront Seawall Parapets (Ocean-facing along X = 145)
            // North of ramp: Z from 255 to 405
            CreateSeawall(parent, "Seawall_NorthWaterfront", new Vector3(145f, CityDeckY, 330f), new Vector3(4f, 2.5f, 150f), mats["City_Concrete"]);
            // South of ramp: Z from -95 to 55
            CreateSeawall(parent, "Seawall_SouthWaterfront", new Vector3(145f, CityDeckY, -20f), new Vector3(4f, 2.5f, 150f), mats["City_Concrete"]);
        }

        private static void CreateSeawall(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.tag = "Ground";
            wall.transform.position = pos;
            wall.transform.rotation = Quaternion.identity;
            wall.transform.localScale = size;
            var mr = wall.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
        }

        private static void BuildGrandCombatRamp(Transform parent, Dictionary<string, Material> mats)
        {
            // Ramp spans from X = 55 (seabed Y = 0) up to X = 155 (city deck Y = 18m)
            // Run = 100m, Rise = 18m, Angle = 10.20 deg. Width = 190m (Z from 60 to 250)
            float runX = 100f;
            float riseY = CityDeckY; // 18m
            float rampWidthZ = 190f;
            float rampSlopeAngle = Mathf.Atan2(riseY, runX) * Mathf.Rad2Deg; // 10.20 deg
            float rampLength = Mathf.Sqrt(runX * runX + riseY * riseY); // 101.61m

            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "City_Grand_Combat_Ramp";
            ramp.transform.SetParent(parent, false);
            ramp.tag = "Ground";
            ramp.layer = 0;

            ramp.transform.position = new Vector3(105f, riseY * 0.5f, 155f);
            ramp.transform.rotation = Quaternion.Euler(0f, 0f, -rampSlopeAngle);
            ramp.transform.localScale = new Vector3(rampLength, 3.5f, rampWidthZ);

            var mr = ramp.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mats["City_Concrete"];

            // Reinforced retaining side walls (North at Z = 250, South at Z = 60)
            CreateRampSideWall(parent, "Ramp_RetainingWall_North", 250f + 2f, rampLength, rampSlopeAngle, mats["City_Concrete"]);
            CreateRampSideWall(parent, "Ramp_RetainingWall_South", 60f - 2f, rampLength, rampSlopeAngle, mats["City_Concrete"]);
        }

        private static void CreateRampSideWall(Transform parent, string name, float zPos, float length, float angle, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.tag = "Ground";
            wall.transform.position = new Vector3(105f, CityDeckY * 0.5f + 1.5f, zPos);
            wall.transform.rotation = Quaternion.Euler(0f, 0f, -angle);
            wall.transform.localScale = new Vector3(length + 4f, 5.0f, 4.0f);
            var mr = wall.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
        }
        #endregion

        #region Modular Road Network
        private static void BuildModularRoadNetwork(Transform parent, GameObject fbx, Dictionary<string, Material> mats)
        {
            GameObject roadRoot = new GameObject("City_Modular_Road_Network");
            roadRoot.transform.SetParent(parent, false);

            float startX = 160f + RoadWidth * 0.5f; // Waterfront Ave center = 175.17f
            float startZ = -80f + RoadWidth * 0.5f; // South Street center = -64.83f

            float[] avenueX = new float[] {
                startX,
                startX + BlockPitch,
                startX + BlockPitch * 2f
            }; // [175.17, 322.54, 469.92]

            float[] streetZ = new float[] {
                startZ,
                startZ + BlockPitch,
                startZ + BlockPitch * 2f,
                startZ + BlockPitch * 3f
            }; // [-64.83, 82.54, 229.92, 377.30]

            // 1. Intersections (12 total: 4 L-Corners, 6 T-Perimeters, 2 X-Crossroads)
            for (int ix = 0; ix < 3; ix++)
            {
                for (int iz = 0; iz < 4; iz++)
                {
                    Vector3 pos = new Vector3(avenueX[ix], CityDeckY + 0.05f, streetZ[iz]);
                    string prefabName;
                    float yaw = 0f;

                    // Corners: L-Intersection
                    if (ix == 0 && iz == 0) { prefabName = "Road_L_Intersection"; yaw = 90f; }
                    else if (ix == 2 && iz == 0) { prefabName = "Road_L_Intersection"; yaw = 0f; }
                    else if (ix == 0 && iz == 3) { prefabName = "Road_L_Intersection"; yaw = 180f; }
                    else if (ix == 2 && iz == 3) { prefabName = "Road_L_Intersection"; yaw = 270f; }
                    // Perimeters: T-Intersection
                    else if (ix == 0) { prefabName = "Road_T_Intersection"; yaw = 90f; }  // West T
                    else if (ix == 2) { prefabName = "Road_T_Intersection"; yaw = 270f; } // East T
                    else if (iz == 0) { prefabName = "Road_T_Intersection"; yaw = 0f; }   // South T
                    else if (iz == 3) { prefabName = "Road_T_Intersection"; yaw = 180f; } // North T
                    // Center crossroads: X-Intersection
                    else { prefabName = "Road_X_Intersection"; yaw = 0f; }

                    SpawnModularRoadPiece(roadRoot.transform, fbx, prefabName, pos, yaw, mats);
                }
            }

            // 2. Horizontal Straight Road Segments along X (8 total)
            for (int iz = 0; iz < 4; iz++)
            {
                for (int bx = 0; bx < 2; bx++)
                {
                    float midX = (avenueX[bx] + avenueX[bx + 1]) * 0.5f;
                    Vector3 pos = new Vector3(midX, CityDeckY + 0.05f, streetZ[iz]);
                    SpawnModularRoadPiece(roadRoot.transform, fbx, "Road_1X_Straight", pos, 0f, mats);
                }
            }

            // 3. Vertical Straight Road Segments along Z (9 total)
            for (int ix = 0; ix < 3; ix++)
            {
                for (int bz = 0; bz < 3; bz++)
                {
                    float midZ = (streetZ[bz] + streetZ[bz + 1]) * 0.5f;
                    Vector3 pos = new Vector3(avenueX[ix], CityDeckY + 0.05f, midZ);
                    SpawnModularRoadPiece(roadRoot.transform, fbx, "Road_1X_Straight", pos, 90f, mats);
                }
            }

            Debug.Log("<color=#00ff88>[City Setup]</color> Modular Road Network instantiated with 12 intersections and 17 straight avenue pieces.");
        }

        private static void SpawnModularRoadPiece(Transform parent, GameObject fbx, string pieceName, Vector3 pos, float yaw, Dictionary<string, Material> mats)
        {
            Transform srcTrans = fbx.transform.Find(pieceName);
            if (srcTrans == null) return;

            GameObject roadPiece = new GameObject(pieceName);
            roadPiece.transform.SetParent(parent, false);
            roadPiece.transform.position = pos;
            roadPiece.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            roadPiece.transform.localScale = Vector3.one * RoadScale;
            roadPiece.tag = "Ground";
            roadPiece.layer = 0;

            var mf = roadPiece.AddComponent<MeshFilter>();
            var srcMf = srcTrans.GetComponent<MeshFilter>();
            if (srcMf != null) mf.sharedMesh = srcMf.sharedMesh;

            var mr = roadPiece.AddComponent<MeshRenderer>();
            var srcMr = srcTrans.GetComponent<MeshRenderer>();
            if (srcMr != null)
            {
                Material[] appliedMats = new Material[srcMr.sharedMaterials.Length];
                for (int i = 0; i < srcMr.sharedMaterials.Length; i++)
                {
                    string mName = srcMr.sharedMaterials[i] != null ? srcMr.sharedMaterials[i].name : "";
                    appliedMats[i] = ResolveMaterial(mName, mats);
                }
                mr.sharedMaterials = appliedMats;
            }

            // Solid box collider for Titan grounding
            var col = roadPiece.AddComponent<BoxCollider>();
            if (pieceName.Contains("Straight"))
            {
                col.center = new Vector3(0f, 0.1f, 0f);
                col.size = new Vector3(146.30f, 0.3f, 37.92f);
            }
            else
            {
                col.center = Vector3.zero;
                col.size = new Vector3(37.92f, 0.3f, 37.92f);
            }
        }
        #endregion

        #region City Blocks & Centered Buildings
        private static void BuildModularCityBlocks(Transform parent, GameObject fbx, Dictionary<string, BuildingMeshInfo> bldgInfoMap, Dictionary<string, Material> mats)
        {
            GameObject districtRoot = new GameObject("City_Blocks_District");
            districtRoot.transform.SetParent(parent, false);

            float startX = 160f + RoadWidth * 0.5f;
            float startZ = -80f + RoadWidth * 0.5f;

            float[] avenueX = new float[] {
                startX,
                startX + BlockPitch,
                startX + BlockPitch * 2f
            };

            float[] streetZ = new float[] {
                startZ,
                startZ + BlockPitch,
                startZ + BlockPitch * 2f,
                startZ + BlockPitch * 3f
            };

            // 6 City Blocks:
            // (0, 0): West-South, (0, 1): Citadel Combat Plaza, (0, 2): West-North
            // (1, 0): East-South, (1, 1): Central Financial, (1, 2): East-North
            for (int bx = 0; bx < 2; bx++)
            {
                for (int bz = 0; bz < 3; bz++)
                {
                    float minX = avenueX[bx] + RoadWidth * 0.5f;
                    float maxX = avenueX[bx + 1] - RoadWidth * 0.5f;
                    float minZ = streetZ[bz] + RoadWidth * 0.5f;
                    float maxZ = streetZ[bz + 1] - RoadWidth * 0.5f;

                    Vector3 blockCenter = new Vector3((minX + maxX) * 0.5f, CityDeckY, (minZ + maxZ) * 0.5f);
                    string blockName = (bx == 0 && bz == 1) ? "Block_CitadelCombatPlaza" : $"Block_{bx}_{bz}";

                    GameObject blockGO = new GameObject(blockName);
                    blockGO.transform.SetParent(districtRoot.transform, false);

                    // Concrete Sidewalk Slab (117m x 117m at curb level)
                    GameObject sidewalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    sidewalk.name = "Sidewalk_Base_Plaza";
                    sidewalk.transform.SetParent(blockGO.transform, false);
                    sidewalk.tag = "Ground";
                    sidewalk.transform.position = new Vector3(blockCenter.x, CityDeckY + 0.15f, blockCenter.z);
                    sidewalk.transform.rotation = Quaternion.identity;
                    sidewalk.transform.localScale = new Vector3(RoadStraight1X - 0.2f, 0.30f, RoadStraight1X - 0.2f);
                    var smr = sidewalk.GetComponent<MeshRenderer>();
                    if (smr != null) smr.sharedMaterial = (bx == 0 && bz == 1) ? mats["City_MarbleTiles"] : mats["City_Concrete"];

                    // Populate buildings inside this block
                    if (bx == 0 && bz == 1)
                    {
                        // Citadel Combat Plaza: Open central arena directly facing the Grand Ramp!
                        BuildCitadelPlazaBlock(blockGO.transform, blockCenter, fbx, bldgInfoMap, mats);
                    }
                    else
                    {
                        // Standard Dense Metropolitan City Block with perimeter buildings
                        BuildStandardCityBlock(blockGO.transform, blockCenter, bx, bz, fbx, bldgInfoMap, mats);
                    }
                }
            }

            Debug.Log("<color=#00ff88>[City Setup]</color> Populated 6 modular city blocks with centered buildings and combat plazas.");
        }

        private static void BuildCitadelPlazaBlock(Transform blockParent, Vector3 center, GameObject fbx, Dictionary<string, BuildingMeshInfo> bldgMap, Dictionary<string, Material> mats)
        {
            // Citadel Plaza has an open 65m x 65m central combat zone surrounded by 4 grand corner anchor towers
            float halfSpan = RoadStraight1X * 0.5f; // ~58.5m
            float cornerDist = halfSpan - 14f; // ~44.5m

            // 4 Massive Corner Skyscraper Anchors
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg7", new Vector3(center.x - cornerDist, CityDeckY, center.z - cornerDist), 45f, mats);
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg9", new Vector3(center.x + cornerDist, CityDeckY, center.z - cornerDist), 135f, mats);
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg2", new Vector3(center.x - cornerDist, CityDeckY, center.z + cornerDist), 315f, mats);
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg10", new Vector3(center.x + cornerDist, CityDeckY, center.z + cornerDist), 225f, mats);

            // Perimeter mid-rises along East and North/South edges
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg1", new Vector3(center.x + cornerDist + 4f, CityDeckY, center.z), 270f, mats);
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg6", new Vector3(center.x, CityDeckY, center.z + cornerDist + 3f), 180f, mats);
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg3", new Vector3(center.x, CityDeckY, center.z - cornerDist - 3f), 0f, mats);

            // Parked emergency vehicles along the plaza perimeter
            SpawnParkedVehicle(blockParent, fbx, "SUV Body", new Vector3(center.x - 25f, CityDeckY + 0.3f, center.z - 30f), 90f, mats["City_CarSUV"]);
            SpawnParkedVehicle(blockParent, fbx, "Pickup Body", new Vector3(center.x - 25f, CityDeckY + 0.3f, center.z + 30f), 90f, mats["City_CarSedan"]);
            SpawnParkedVehicle(blockParent, fbx, "Sedan Body", new Vector3(center.x + 30f, CityDeckY + 0.3f, center.z - 25f), 0f, mats["City_CarSedan"]);

            // Atmospheric Cyan & Amber combat plaza lights
            CreatePlazaLight(blockParent, "Citadel_Light_Cyan", center + new Vector3(-20f, 15f, 0f), new Color(0.1f, 0.85f, 1.0f), 350f, 65f);
            CreatePlazaLight(blockParent, "Citadel_Light_Amber", center + new Vector3(20f, 15f, 0f), new Color(1.0f, 0.65f, 0.2f), 350f, 65f);
        }

        private static void BuildStandardCityBlock(Transform blockParent, Vector3 center, int bx, int bz, GameObject fbx, Dictionary<string, BuildingMeshInfo> bldgMap, Dictionary<string, Material> mats)
        {
            float half = RoadStraight1X * 0.5f; // ~58.5m
            float curbEdge = half - 12f; // ~46.5m from center

            // 1. Four corner anchor buildings
            string[] cornerTypes = (bx + bz) % 2 == 0 ?
                new[] { "Bldg6", "Bldg10", "Bldg1", "Bldg7" } :
                new[] { "Bldg3", "Bldg9", "Bldg2", "Bldg11" };

            SpawnCenteredBuilding(blockParent, fbx, bldgMap, cornerTypes[0], new Vector3(center.x - curbEdge, CityDeckY, center.z - curbEdge), 0f, mats);
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, cornerTypes[1], new Vector3(center.x + curbEdge, CityDeckY, center.z - curbEdge), 90f, mats);
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, cornerTypes[2], new Vector3(center.x - curbEdge, CityDeckY, center.z + curbEdge), 270f, mats);
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, cornerTypes[3], new Vector3(center.x + curbEdge, CityDeckY, center.z + curbEdge), 180f, mats);

            // 2. Perimeter buildings facing the four surrounding streets
            // West facade (facing West avenue)
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg4", new Vector3(center.x - curbEdge, CityDeckY, center.z - 16f), 90f, mats);
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg5", new Vector3(center.x - curbEdge, CityDeckY, center.z + 16f), 90f, mats);

            // East facade (facing East avenue)
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg8", new Vector3(center.x + curbEdge, CityDeckY, center.z - 16f), 270f, mats);
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg12", new Vector3(center.x + curbEdge, CityDeckY, center.z + 16f), 270f, mats);

            // South facade (facing South street)
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg1", new Vector3(center.x - 16f, CityDeckY, center.z - curbEdge), 0f, mats);
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg3", new Vector3(center.x + 16f, CityDeckY, center.z - curbEdge), 0f, mats);

            // North facade (facing North street)
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg10", new Vector3(center.x - 16f, CityDeckY, center.z + curbEdge), 180f, mats);
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, "Bldg11", new Vector3(center.x + 16f, CityDeckY, center.z + curbEdge), 180f, mats);

            // 3. Central commercial core / tower inside the block
            string coreBldg = (bz == 0) ? "Bldg9" : ((bz == 2) ? "Bldg7" : "Bldg2");
            SpawnCenteredBuilding(blockParent, fbx, bldgMap, coreBldg, new Vector3(center.x, CityDeckY, center.z), 45f * (bx + bz), mats);

            // 4. Parked cars along block edges
            SpawnParkedVehicle(blockParent, fbx, "Sedan Body", new Vector3(center.x - curbEdge - 5f, CityDeckY + 0.3f, center.z + 32f), 90f, mats["City_CarSedan"]);
            SpawnParkedVehicle(blockParent, fbx, "Compact Body", new Vector3(center.x + curbEdge + 5f, CityDeckY + 0.3f, center.z - 32f), 270f, mats["City_CarSUV"]);
        }

        private static void SpawnCenteredBuilding(Transform parent, GameObject fbx, Dictionary<string, BuildingMeshInfo> bldgMap, string bldgName, Vector3 worldPos, float yaw, Dictionary<string, Material> mats)
        {
            if (!bldgMap.ContainsKey(bldgName)) return;
            var info = bldgMap[bldgName];
            Transform srcTrans = fbx.transform.Find(bldgName);
            if (srcTrans == null) return;

            GameObject bldgGO = new GameObject(bldgName);
            bldgGO.transform.SetParent(parent, false);
            bldgGO.transform.position = worldPos;
            bldgGO.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            bldgGO.transform.localScale = Vector3.one;
            bldgGO.layer = 0;
            bldgGO.tag = "Untagged";

            // Child with mesh, correctly centered so (0,0,0) is building's ground center
            GameObject meshChild = new GameObject("Mesh");
            meshChild.transform.SetParent(bldgGO.transform, false);
            meshChild.transform.localRotation = srcTrans.localRotation;
            meshChild.transform.localScale = srcTrans.localScale * BuildingUniformScale;
            meshChild.transform.localPosition = -new Vector3(info.tCenter.x, info.minY, info.tCenter.z) * BuildingUniformScale;

            var mf = meshChild.AddComponent<MeshFilter>();
            var srcMf = srcTrans.GetComponent<MeshFilter>();
            if (srcMf != null) mf.sharedMesh = srcMf.sharedMesh;

            var mr = meshChild.AddComponent<MeshRenderer>();
            var srcMr = srcTrans.GetComponent<MeshRenderer>();
            if (srcMr != null)
            {
                Material[] appliedMats = new Material[srcMr.sharedMaterials.Length];
                for (int m = 0; m < srcMr.sharedMaterials.Length; m++)
                {
                    string orig = srcMr.sharedMaterials[m] != null ? srcMr.sharedMaterials[m].name : "";
                    appliedMats[m] = ResolveMaterial(orig, mats);
                }
                mr.sharedMaterials = appliedMats;
            }

            // Accurate BoxCollider centered at (0, height/2, 0)
            var col = bldgGO.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, info.tSize.y * BuildingUniformScale * 0.5f, 0f);
            col.size = info.tSize * BuildingUniformScale;
        }

        private static void SpawnParkedVehicle(Transform parent, GameObject fbx, string carName, Vector3 worldPos, float yaw, Material mat)
        {
            Transform srcTrans = fbx.transform.Find(carName);
            if (srcTrans == null) return;

            GameObject car = new GameObject(carName);
            car.transform.SetParent(parent, false);
            car.transform.position = worldPos;
            car.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            car.transform.localScale = Vector3.one * 0.15f;

            var mf = car.AddComponent<MeshFilter>();
            var srcMf = srcTrans.GetComponent<MeshFilter>();
            if (srcMf != null) mf.sharedMesh = srcMf.sharedMesh;

            var mr = car.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            var col = car.AddComponent<BoxCollider>();
            col.size = new Vector3(20f, 15f, 45f);
        }

        private static void CreatePlazaLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            GameObject lightGO = new GameObject(name);
            lightGO.transform.SetParent(parent, false);
            lightGO.transform.position = pos;
            var l = lightGO.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }
        #endregion
    }
}
#endif
