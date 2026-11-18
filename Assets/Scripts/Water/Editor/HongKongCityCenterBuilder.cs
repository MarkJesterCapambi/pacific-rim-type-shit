using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using PacificRimEnvironment;

namespace PacificRimWater.Editor
{
    /// <summary>
    /// Builds the 500m x 500m landlocked Hong Kong City Center (Night / Rain) arena for Otachi (Level 2),
    /// reusing assets and building archetypes directly from SetupNeonCityAssets and SetupNeonCityLevel:
    /// - Slender Tong Lau tenements (with neon shop awnings, AC grids, rooftop tanks & bamboo scaffolding)
    /// - Tiered Hong Kong skyscrapers (pencil, curtain glass, twin-slit, stepped setback, rooftop helipads & strobes)
    /// - Real 3D Buildings.fbx high-rise clusters and collateral vehicles
    /// - Bank of China triangular prism X-braced supertalls, Two IFC telescoping octagonal spires & curved towers
    /// - Nathan Road double-sided Cantonese cantilevered neon signs, giant LED billboards & sky searchlights
    /// - Destructible glass pedestrian skybridges, 15m-chunk South Plaza elevated highway flyover, HK trams & tankers
    /// - Strictly landlocked (no harbor/water physics), rain-lashed, with 6 overhead urban Chinooks.
    /// </summary>
    internal static class HongKongCityCenterBuilder
    {
        private const string CityRootName = "HongKong_City_Center";

        // Strict Neon City 4-color indices inside assets.NeonTubeMats (0:Cyan, 2:Amber, 4:Jade, 5:Crimson)
        private static readonly int[] StrictNeonTubeIndices = { 5, 0, 4, 2 };
        private static readonly Color[] StrictLightColors =
        {
            new Color(1.00f, 0.14f, 0.24f), // Crimson Red
            new Color(0.10f, 0.92f, 1.00f), // Electric Cyan
            new Color(0.12f, 0.96f, 0.56f), // Jade Green
            new Color(1.00f, 0.64f, 0.14f)  // Warm Amber
        };

        private static int rngSeed;

        internal static GameObject BuildHongKongCityCenter(NeonCityAssetPack assets)
        {
            GameObject existing = GameObject.Find(CityRootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            rngSeed = 8804;
            GameObject buildingsFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Buildings.fbx");

            GameObject root = new GameObject(CityRootName);
            Transform neonCanopyRoot = CreateGroup(root.transform, "City_Cantilevered_Neon_And_Billboards");
            int signCounter = 0;

            BuildRoadGridAndCentralPlaza(root.transform, neonCanopyRoot, assets);
            BuildAllSixDenseHongKongBlocks(root.transform, neonCanopyRoot, ref signCounter, buildingsFbx, assets);
            BuildNorthEastWestSupertallPerimeters(root.transform, assets);
            BuildSouthRetainingWallInterchangeAndMidLevels(root.transform, buildingsFbx, assets);
            BuildDestructibleSkybridgesAndSouthFlyover(root.transform, assets);
            BuildStreetCollateralTraffic(root.transform, buildingsFbx, assets);
            BuildCloudsMistReflectionsLightsAndSearchlights(root.transform, assets);

            EditorUtility.SetDirty(root);
            return root;
        }

        // =====================================================================
        // 1. 500x500M WET HIGHWAY GRID & 180x180M CENTRAL TRANSIT PLAZA
        // =====================================================================
        private static void BuildRoadGridAndCentralPlaza(Transform parent, Transform neonRoot, NeonCityAssetPack assets)
        {
            Transform roadRoot = CreateGroup(parent, "City_Roads_And_Central_Plaza");

            // 1. Central 180m x 180m Combat Hub (X, Z in [-90..+90]):
            //    - Center [-30..+30] x [-30..+30]: 60m x 60m square IntersectionMat (1:1 aspect ratio, zero distortion)
            //    - Four 60m x 60m N/S/E/W HighwayMat boulevard arms meeting flush at the central intersection
            //    - Four 60m x 60m corner SidewalkMat transit concourses filling [+-30..+-90] x [+-30..+-90] edge-to-edge
            SetupNeonCityLevel.CreateRoadQuad(roadRoot, "Central_Plaza_Intersection_Core",
                new Vector3(0f, 0.022f, 0f), new Vector3(60f, 60f, 1f), assets.IntersectionMat);

            SetupNeonCityLevel.CreateRoadQuad(roadRoot, "Central_Plaza_Approach_N",
                new Vector3(0f, 0.022f, 60f), new Vector3(60f, 60f, 1f), assets.HighwayMat);
            SetupNeonCityLevel.CreateRoadQuad(roadRoot, "Central_Plaza_Approach_S",
                new Vector3(0f, 0.022f, -60f), new Vector3(60f, 60f, 1f), assets.HighwayMat);

            GameObject appW = SetupNeonCityLevel.CreateRoadQuad(roadRoot, "Central_Plaza_Approach_W",
                new Vector3(-60f, 0.022f, 0f), new Vector3(60f, 60f, 1f), assets.HighwayMat);
            appW.transform.localRotation = Quaternion.Euler(90f, 90f, 0f);

            GameObject appE = SetupNeonCityLevel.CreateRoadQuad(roadRoot, "Central_Plaza_Approach_E",
                new Vector3(60f, 0.022f, 0f), new Vector3(60f, 60f, 1f), assets.HighwayMat);
            appE.transform.localRotation = Quaternion.Euler(90f, 90f, 0f);

            float[] signs = { -1f, 1f };
            int cornerIdx = 0;
            foreach (float sx in signs)
            {
                foreach (float sz in signs)
                {
                    GameObject sw = SetupNeonCityLevel.CreateSubBox(roadRoot, $"Plaza_Concourse_{(sx > 0 ? "E" : "W")}{(sz > 0 ? "N" : "S")}",
                        new Vector3(sx * 60f, 0.08f, sz * 60f), new Vector3(60f, 0.16f, 60f), assets.SidewalkMat);
                    sw.tag = "Ground";

                    BuildDestructibleMtrPavilion(roadRoot, $"Collateral_MTR_Entrance_{cornerIdx}",
                        new Vector3(sx * 74f, 0f, sz * 74f), cornerIdx, assets);

                    cornerIdx++;
                }
            }

            // 2. Main N-S Avenue (60m wide: X in [-30..+30]):
            //    Exact non-overlapping 63m segments above North Cross-Street ([126..189], [189..252])
            //    and below South Cross-Street ([-189..-126], [-252..-189])
            float[] aveNorthZs = { 157.5f, 220.5f };
            float[] aveSouthZs = { -157.5f, -220.5f };
            for (int i = 0; i < aveNorthZs.Length; i++)
            {
                SetupNeonCityLevel.CreateRoadQuad(roadRoot, $"MainAve_North_{i}",
                    new Vector3(0f, 0.022f, aveNorthZs[i]), new Vector3(60f, 63f, 1f), assets.HighwayMat);
                SetupNeonCityLevel.CreateRoadQuad(roadRoot, $"MainAve_South_{i}",
                    new Vector3(0f, 0.022f, aveSouthZs[i]), new Vector3(60f, 63f, 1f), assets.HighwayMat);
            }

            // 3. Two 36m Secondary Cross-Streets (Z in [+90..+126] at Z=+108, and [-126..-90] at Z=-108):
            //    - Central 60m x 36m intersection at X in [-30..+30]
            //    - West arm [-252..-30] (222m) and East arm [+30..+252] (222m) each split into 4 exact 55.5m segments (zero gap, zero overlap)
            float[] crossZs = { 108.0f, -108.0f };
            float[] crossSegXs = { -224.25f, -168.75f, -113.25f, -57.75f, 57.75f, 113.25f, 168.75f, 224.25f };
            for (int c = 0; c < crossZs.Length; c++)
            {
                float cz = crossZs[c];
                string tag = c == 0 ? "North" : "South";

                SetupNeonCityLevel.CreateRoadQuad(roadRoot, $"CrossIntersection_{tag}",
                    new Vector3(0f, 0.022f, cz), new Vector3(60f, 36f, 1f), assets.IntersectionMat);

                for (int s = 0; s < crossSegXs.Length; s++)
                {
                    GameObject q = SetupNeonCityLevel.CreateRoadQuad(roadRoot, $"CrossStreet_{tag}_{s}",
                        new Vector3(crossSegXs[s], 0.022f, cz), new Vector3(36f, 55.5f, 1f), assets.HighwayMat);
                    q.transform.localRotation = Quaternion.Euler(90f, 90f, 0f);
                }
            }

            // 4. Sidewalks lining Main Avenue & Cross-Streets
            (float zMin, float zMax)[] aveSidewalkZones = { (126f, 252f), (-252f, -126f) };
            for (int i = 0; i < aveSidewalkZones.Length; i++)
            {
                float len = aveSidewalkZones[i].zMax - aveSidewalkZones[i].zMin;
                float midZ = (aveSidewalkZones[i].zMin + aveSidewalkZones[i].zMax) * 0.5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject sw = SetupNeonCityLevel.CreateSubBox(roadRoot, $"Sidewalk_Ave_{(side < 0 ? "W" : "E")}_{i}",
                        new Vector3(side * 31.4f, 0.08f, midZ), new Vector3(2.8f, 0.16f, len), assets.SidewalkMat);
                    sw.tag = "Ground";
                }
            }

            // Sleek Neon City LED Streetlights lining Main Avenue & Cross-Streets
            for (float z = 136f; z <= 240f; z += 34f)
            {
                SpawnNeonStreetlight(roadRoot, -30.8f, z, 90f, 0, assets);
                SpawnNeonStreetlight(roadRoot,  30.8f, z, 270f, 2, assets);
                SpawnNeonStreetlight(roadRoot, -30.8f, -z, 90f, 2, assets);
                SpawnNeonStreetlight(roadRoot,  30.8f, -z, 270f, 0, assets);
            }
            for (float x = -220f; x <= 220f; x += 48f)
            {
                if (Mathf.Abs(x) < 42f) continue;
                SpawnNeonStreetlight(roadRoot, x, 125.6f, 180f, 0, assets);
                SpawnNeonStreetlight(roadRoot, x, -125.6f, 0f, 2, assets);
            }
        }

        private static void SpawnNeonStreetlight(Transform parent, float x, float z, float yaw, int tubeIdx, NeonCityAssetPack assets)
        {
            GameObject pole = new GameObject($"Streetlight_{x:0}_{z:0}");
            pole.transform.SetParent(parent, false);
            pole.transform.position = new Vector3(x, 0f, z);
            pole.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            SetupNeonCityLevel.CreateSubBox(pole.transform, "Shaft", new Vector3(0f, 4.6f, 0f), new Vector3(0.28f, 9.2f, 0.28f), assets.DarkSteelMat);
            SetupNeonCityLevel.CreateSubBox(pole.transform, "Arm", new Vector3(0f, 9.0f, 1.3f), new Vector3(0.22f, 0.22f, 2.8f), assets.DarkSteelMat);
            SetupNeonCityLevel.CreateSubBox(pole.transform, "LED", new Vector3(0f, 8.82f, 2.2f), new Vector3(0.70f, 0.16f, 1.25f), assets.NeonTubeMats[tubeIdx]);
        }

        private static void BuildDestructibleMtrPavilion(Transform parent, string name, Vector3 pos, int idx, NeonCityAssetPack assets)
        {
            GameObject pav = new GameObject(name);
            pav.transform.SetParent(parent, false);
            pav.transform.position = pos;

            SetupNeonCityLevel.CreateSubBox(pav.transform, "RoofGlass", new Vector3(0f, 4.4f, 0f), new Vector3(13f, 0.35f, 9f), assets.SkybridgeGlassMat);
            SetupNeonCityLevel.CreateSubBox(pav.transform, "Frame_L", new Vector3(-6.3f, 2.2f, 0f), new Vector3(0.4f, 4.4f, 9f), assets.DarkSteelMat);
            SetupNeonCityLevel.CreateSubBox(pav.transform, "Frame_R", new Vector3( 6.3f, 2.2f, 0f), new Vector3(0.4f, 4.4f, 9f), assets.DarkSteelMat);
            SetupNeonCityLevel.CreateSubBox(pav.transform, "MtrNeonRim", new Vector3(0f, 4.68f, 0f), new Vector3(13.4f, 0.32f, 9.4f),
                assets.NeonTubeMats[StrictNeonTubeIndices[idx % StrictNeonTubeIndices.Length]]);

            var col = pav.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 2.4f, 0f);
            col.size = new Vector3(13.4f, 4.8f, 9.4f);

            var prop = pav.AddComponent<UrbanCollateralProp>();
            prop.mode = UrbanCollateralProp.CollateralMode.ShatterOnTitanPass;
            prop.launchMass = 5f;
            prop.knockImpulse = 24f;
        }

        // =====================================================================
        // 2. SIX DENSE HONG KONG BLOCKS (SLENDER TONG LAU, TIERED TOWERS & FBX CLUSTERS)
        // =====================================================================
        private static void BuildAllSixDenseHongKongBlocks(
            Transform parent, Transform neonRoot, ref int signCounter, GameObject buildingsFbx, NeonCityAssetPack assets)
        {
            Transform blocksRoot = CreateGroup(parent, "City_Inner_HongKong_Blocks");

            // Block bounds strictly clear of Main Ave (|X|>=32.5), Cross-Streets (|Z| outside [89..126]), Central Plaza (|X|>=91.5)
            PopulateHongKongCityBlock(blocksRoot, neonRoot, ref signCounter, "Block_NW", -246f, -33f, 127f, 246f,
                exposeNorth: false, exposeSouth: true, exposeWest: false, exposeEast: true, buildingsFbx, assets);
            PopulateHongKongCityBlock(blocksRoot, neonRoot, ref signCounter, "Block_NE",  33f,  246f, 127f, 246f,
                exposeNorth: false, exposeSouth: true, exposeWest: true, exposeEast: false, buildingsFbx, assets);

            PopulateHongKongCityBlock(blocksRoot, neonRoot, ref signCounter, "Block_WestPodium", -246f, -92f, -88f, 88f,
                exposeNorth: true, exposeSouth: true, exposeWest: false, exposeEast: true, buildingsFbx, assets);
            PopulateHongKongCityBlock(blocksRoot, neonRoot, ref signCounter, "Block_EastPodium",  92f,  246f, -88f, 88f,
                exposeNorth: true, exposeSouth: true, exposeWest: true, exposeEast: false, buildingsFbx, assets);

            PopulateHongKongCityBlock(blocksRoot, neonRoot, ref signCounter, "Block_SW", -246f, -33f, -246f, -127f,
                exposeNorth: true, exposeSouth: false, exposeWest: false, exposeEast: true, buildingsFbx, assets);
            PopulateHongKongCityBlock(blocksRoot, neonRoot, ref signCounter, "Block_SE",  33f,  246f, -246f, -127f,
                exposeNorth: true, exposeSouth: false, exposeWest: true, exposeEast: false, buildingsFbx, assets);
        }

        private static void PopulateHongKongCityBlock(
            Transform parent, Transform neonRoot, ref int signCounter, string blockName,
            float xMin, float xMax, float zMin, float zMax,
            bool exposeNorth, bool exposeSouth, bool exposeWest, bool exposeEast,
            GameObject buildingsFbx, NeonCityAssetPack assets)
        {
            Transform blk = CreateGroup(parent, blockName);

            // Base sidewalk slab underneath this block
            GameObject plinth = SetupNeonCityLevel.CreateSubBox(blk, "Block_SidewalkPlinth",
                new Vector3((xMin + xMax) * 0.5f, 0.09f, (zMin + zMax) * 0.5f),
                new Vector3(xMax - xMin, 0.18f, zMax - zMin), assets.SidewalkMat);
            plinth.tag = "Ground";

            // Pack dense slender Hong Kong towers (16m-24m wide footprints, ~26m pitch => 4-9m dark alleys)
            // around every exposed street edge + dense interior towers!
            float pitchX = 27.5f;
            float pitchZ = 27.5f;
            int cols = Mathf.Max(2, Mathf.FloorToInt((xMax - xMin) / pitchX));
            int rows = Mathf.Max(2, Mathf.FloorToInt((zMax - zMin) / pitchZ));
            float stepX = (xMax - xMin) / cols;
            float stepZ = (zMax - zMin) / rows;

            string[] fbxPool = { "Bldg8", "Bldg5", "Bldg9", "Bldg12", "Bldg4" };
            Bounds[,] bldgBounds = new Bounds[cols, rows];
            bool[,] hasBldg = new bool[cols, rows];

            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    bool onNorthRow = (r == rows - 1);
                    bool onSouthRow = (r == 0);
                    bool onEastCol = (c == cols - 1);
                    bool onWestCol = (c == 0);
                    bool onPerimeterRing = onNorthRow || onSouthRow || onEastCol || onWestCol;

                    bool onNorthEdge = onNorthRow && exposeNorth;
                    bool onSouthEdge = onSouthRow && exposeSouth;
                    bool onEastEdge = onEastCol && exposeEast;
                    bool onWestEdge = onWestCol && exposeWest;
                    bool onStreetFront = onNorthEdge || onSouthEdge || onEastEdge || onWestEdge;

                    // Keep full perimeter ring of each block 100% populated so alley blockers never have an empty slot beside them
                    if (!onPerimeterRing && (c + r) % 4 == 1) continue;

                    float cx = xMin + stepX * (c + 0.5f);
                    float cz = zMin + stepZ * (r + 0.5f);

                    // Slender authentic HK footprints (16m-23m) leaving 5m-9m dark alleys between neighbors
                    float bldgW = Mathf.Clamp(stepX - NextRandRange(5.0f, 8.2f), 16f, 23.5f);
                    float bldgD = Mathf.Clamp(stepZ - NextRandRange(5.0f, 8.2f), 16f, 23.5f);

                    // Determine facing yaw toward open street (or plaza)
                    float yaw = 0f;
                    if (onSouthEdge) yaw = 180f;
                    else if (onNorthEdge) yaw = 0f;
                    else if (onEastEdge) yaw = 90f;
                    else if (onWestEdge) yaw = 270f;
                    else yaw = ((c + r) % 4) * 90f;

                    bool sideways = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) > 0.7f;
                    float localW = sideways ? bldgD : bldgW;
                    float localD = sideways ? bldgW : bldgD;

                    int roll = NextRandInt(0, 100);
                    int neonIdx = StrictNeonTubeIndices[NextRandInt(0, StrictNeonTubeIndices.Length)];
                    int winIdx = NextRandInt(0, assets.WindowMats.Length);

                    GameObject spawned;
                    float finalH;

                    if (onStreetFront && roll < 34)
                    {
                        finalH = NextRandRange(20f, 33f);
                        bool hasBamboo = roll < 22;
                        spawned = SetupNeonCityLevel.SpawnTongLauBlock(
                            blk, $"{blockName}_TongLau_{c}_{r}", new Vector3(cx, 0f, cz), yaw,
                            localW, localD, finalH, hasBamboo, neonIdx, assets);
                    }
                    else if (!onPerimeterRing && buildingsFbx != null && roll < 28)
                    {
                        finalH = NextRandRange(42f, 68f);
                        spawned = SetupNeonCityLevel.SpawnScaledFbxTongLauCluster(
                            blk, buildingsFbx, fbxPool[(c + r) % fbxPool.Length],
                            $"{blockName}_FbxTower_{c}_{r}", new Vector3(cx, 0f, cz), yaw,
                            NextRandRange(0.24f, 0.36f), assets);
                        if (spawned != null) EnsureStaticBuildingCollider(spawned, bldgW, finalH, bldgD);
                    }
                    else if (!onPerimeterRing && roll >= 82)
                    {
                        finalH = NextRandRange(68f, 94f);
                        spawned = SetupNeonCityLevel.SpawnCurvedWaterfrontTower(
                            blk, $"{blockName}_Curved_{c}_{r}", new Vector3(cx, 0f, cz), yaw,
                            localW, localD, finalH, winIdx, neonIdx, assets);
                        EnsureStaticBuildingCollider(spawned, localW, finalH, localD);
                    }
                    else
                    {
                        finalH = onStreetFront ? NextRandRange(52f, 88f) : NextRandRange(64f, 95f);
                        int kind = 1 + (roll % 4); // 1:ThinPencil, 2:CurtainGlass, 3:TwinSlit, 4:SteppedSetback
                        bool hasHelipad = (finalH > 62f) && (roll % 5 == 0);
                        bool hasBamboo = onStreetFront && (roll % 7 == 0);
                        spawned = SetupNeonCityLevel.SpawnTieredSkyscraper(
                            blk, $"{blockName}_Tower_{c}_{r}", new Vector3(cx, 0f, cz), yaw,
                            localW, localD, finalH, kind, winIdx, neonIdx,
                            addCollider: true, assets, hasHelipad, hasBamboo);
                    }

                    if (spawned != null)
                    {
                        spawned.AddComponent<UrbanBuildingImpactReceiver>();
                        bldgBounds[c, r] = GetBuildingBaseFootprintBounds(spawned, new Vector3(cx, finalH * 0.5f, cz), new Vector3(bldgW, finalH, bldgD));
                        hasBldg[c, r] = true;
                    }

                    // Mount Nathan Road double-sided Cantonese cantilevered neon signs directly onto actual MeshRenderer wall bounds!
                    if (onStreetFront && spawned != null)
                    {
                        if (onNorthEdge)
                            MountNeonCantileversOnBuildingMesh(neonRoot, ref signCounter, spawned, jutAlongX: false, dirSign: +1f, assets);
                        if (onSouthEdge)
                            MountNeonCantileversOnBuildingMesh(neonRoot, ref signCounter, spawned, jutAlongX: false, dirSign: -1f, assets);
                        if (onEastEdge)
                            MountNeonCantileversOnBuildingMesh(neonRoot, ref signCounter, spawned, jutAlongX: true, dirSign: +1f, assets);
                        if (onWestEdge)
                            MountNeonCantileversOnBuildingMesh(neonRoot, ref signCounter, spawned, jutAlongX: true, dirSign: -1f, assets);

                        // Mount giant Hong Kong LED Rooftop Billboards directly onto corner building roofs facing Central Plaza
                        bool isPlazaCornerBldg =
                            (onEastEdge && (onNorthEdge || onSouthEdge)) ||
                            (onWestEdge && (onNorthEdge || onSouthEdge));
                        if (isPlazaCornerBldg)
                        {
                            Bounds roofB = GetSolidWallBoundsForSign(spawned);
                            float roofTopY = roofB.max.y;
                            float yawToOrigin = Mathf.Atan2(-cx, -cz) * Mathf.Rad2Deg;
                            int bIdx = signCounter % assets.WaterfrontLedMats.Length;
                            GameObject board = SetupNeonCityLevel.BuildWaterfrontLedBillboard(
                                neonRoot, $"Billboard_RoofCorner_{blockName}_{c}_{r}",
                                new Vector3(roofB.center.x, roofTopY + 5.2f, roofB.center.z),
                                yawToOrigin, new Vector3(Mathf.Min(18f, roofB.size.x * 0.85f), 8.5f, 0.55f),
                                assets.WaterfrontLedMats[bIdx],
                                assets.VolumetricBeamMats[bIdx % assets.VolumetricBeamMats.Length],
                                StrictLightColors[bIdx % StrictLightColors.Length],
                                assets, groundBaseY: roofTopY - 0.5f);
                            MakeNeonAttachmentDestructible(board, new Vector3(18f, 10f, 3.5f), Vector3.zero, debrisMass: 12f);
                        }
                    }
                }
            }

            // Seal EVERY alley gap along all 4 sides of the block using exact building mesh bounds + 3.4m overlap
            // plus outer block-to-perimeter gaps so zero open space exists beside any alley blocker!
            int[] zEdgeRows = { 0, rows - 1 };
            for (int ri = 0; ri < zEdgeRows.Length; ri++)
            {
                int r = zEdgeRows[ri];
                float yaw = (r == 0) ? 180f : 0f;
                string edgeTag = (r == 0) ? "S" : "N";

                for (int c = 0; c < cols - 1; c++)
                {
                    if (!hasBldg[c, r] || !hasBldg[c + 1, r]) continue;
                    Bounds bL = bldgBounds[c, r];
                    Bounds bR = bldgBounds[c + 1, r];
                    float gapMinX = bL.max.x;
                    float gapMaxX = bR.min.x;
                    float spanW = Mathf.Max(3.0f, (gapMaxX - gapMinX) + 3.6f);
                    float midX = (gapMinX + gapMaxX) * 0.5f;
                    float zLine = (r == 0)
                        ? Mathf.Max(bL.min.z, bR.min.z) + 1.5f
                        : Mathf.Min(bL.max.z, bR.max.z) - 1.5f;
                    SealAlleyCurb(blk, $"{blockName}_Alley{edgeTag}_{c}", new Vector3(midX, 0f, zLine), spanW, yaw, assets);
                }

                // Seal outer gap between West-most building of this row and West Perimeter (X = -252)
                if (xMin < -200f && hasBldg[0, r])
                {
                    Bounds b0 = bldgBounds[0, r];
                    float gapMinX = -252.5f;
                    float gapMaxX = b0.min.x;
                    float spanW = Mathf.Max(3.0f, (gapMaxX - gapMinX) + 3.6f);
                    float midX = (gapMinX + gapMaxX) * 0.5f;
                    float zLine = (r == 0) ? (b0.min.z + 1.5f) : (b0.max.z - 1.5f);
                    SealAlleyCurb(blk, $"{blockName}_Alley{edgeTag}_WestOuter", new Vector3(midX, 0f, zLine), spanW, yaw, assets);
                }

                // Seal outer gap between East-most building of this row and East Perimeter (X = +252)
                if (xMax > 200f && hasBldg[cols - 1, r])
                {
                    Bounds bLast = bldgBounds[cols - 1, r];
                    float gapMinX = bLast.max.x;
                    float gapMaxX = 252.5f;
                    float spanW = Mathf.Max(3.0f, (gapMaxX - gapMinX) + 3.6f);
                    float midX = (gapMinX + gapMaxX) * 0.5f;
                    float zLine = (r == 0) ? (bLast.min.z + 1.5f) : (bLast.max.z - 1.5f);
                    SealAlleyCurb(blk, $"{blockName}_Alley{edgeTag}_EastOuter", new Vector3(midX, 0f, zLine), spanW, yaw, assets);
                }
            }

            int[] xEdgeCols = { 0, cols - 1 };
            for (int ci = 0; ci < xEdgeCols.Length; ci++)
            {
                int c = xEdgeCols[ci];
                float yaw = (c == 0) ? 270f : 90f;
                string edgeTag = (c == 0) ? "W" : "E";

                for (int r = 0; r < rows - 1; r++)
                {
                    if (!hasBldg[c, r] || !hasBldg[c, r + 1]) continue;
                    Bounds bS = bldgBounds[c, r];
                    Bounds bN = bldgBounds[c, r + 1];
                    float gapMinZ = bS.max.z;
                    float gapMaxZ = bN.min.z;
                    float spanD = Mathf.Max(3.0f, (gapMaxZ - gapMinZ) + 3.6f);
                    float midZ = (gapMinZ + gapMaxZ) * 0.5f;
                    float xLine = (c == 0)
                        ? Mathf.Max(bS.min.x, bN.min.x) + 1.5f
                        : Mathf.Min(bS.max.x, bN.max.x) - 1.5f;
                    SealAlleyCurb(blk, $"{blockName}_Alley{edgeTag}_{r}", new Vector3(xLine, 0f, midZ), spanD, yaw, assets);
                }

                // Seal outer gap between North-most building of this col and North Perimeter (Z = +252)
                if (zMax > 200f && hasBldg[c, rows - 1])
                {
                    Bounds bTop = bldgBounds[c, rows - 1];
                    float gapMinZ = bTop.max.z;
                    float gapMaxZ = 252.5f;
                    float spanD = Mathf.Max(3.0f, (gapMaxZ - gapMinZ) + 3.6f);
                    float midZ = (gapMinZ + gapMaxZ) * 0.5f;
                    float xLine = (c == 0) ? (bTop.min.x + 1.5f) : (bTop.max.x - 1.5f);
                    SealAlleyCurb(blk, $"{blockName}_Alley{edgeTag}_NorthOuter", new Vector3(xLine, 0f, midZ), spanD, yaw, assets);
                }

                // Seal outer gap between South-most building of this col and South Retaining Wall (Z = -252)
                if (zMin < -200f && hasBldg[c, 0])
                {
                    Bounds bBot = bldgBounds[c, 0];
                    float gapMinZ = -252.5f;
                    float gapMaxZ = bBot.min.z;
                    float spanD = Mathf.Max(3.0f, (gapMaxZ - gapMinZ) + 3.6f);
                    float midZ = (gapMinZ + gapMaxZ) * 0.5f;
                    float xLine = (c == 0) ? (bBot.min.x + 1.5f) : (bBot.max.x - 1.5f);
                    SealAlleyCurb(blk, $"{blockName}_Alley{edgeTag}_SouthOuter", new Vector3(xLine, 0f, midZ), spanD, yaw, assets);
                }
            }
        }

        private static Bounds GetSolidWallBoundsForSign(GameObject bldg)
        {
            // Prefer the lower continuous shaft or TongLau body so signs never mount above a setback or inside a twin-slit gap
            string[] preferredNames = { "TongLau_Body", "WindowShaft", "Shaft_Lower", "Shaft_L", "Curved_Body", "Mesh" };
            for (int i = 0; i < preferredNames.Length; i++)
            {
                Transform child = bldg.transform.Find(preferredNames[i]);
                if (child != null)
                {
                    MeshRenderer mr = child.GetComponent<MeshRenderer>();
                    if (mr != null) return mr.bounds;
                }
            }
            return GetCombinedMeshBounds(bldg);
        }

        private static Bounds GetBuildingBaseFootprintBounds(GameObject bldg, Vector3 fallbackCenter, Vector3 fallbackSize)
        {
            string[] baseNames = { "Podium", "Shop_Ground", "TongLau_Body", "WindowShaft", "Shaft_Lower" };
            for (int i = 0; i < baseNames.Length; i++)
            {
                Transform child = bldg.transform.Find(baseNames[i]);
                if (child != null)
                {
                    MeshRenderer mr = child.GetComponent<MeshRenderer>();
                    if (mr != null) return mr.bounds;
                }
            }
            MeshRenderer[] all = bldg.GetComponentsInChildren<MeshRenderer>();
            if (all.Length > 0)
            {
                Bounds b = all[0].bounds;
                for (int i = 1; i < all.Length; i++) b.Encapsulate(all[i].bounds);
                return b;
            }
            return new Bounds(fallbackCenter, fallbackSize);
        }

        private static void MountNeonCantileversOnBuildingMesh(
            Transform neonRoot, ref int signCounter, GameObject bldg,
            bool jutAlongX, float dirSign, NeonCityAssetPack assets)
        {
            Bounds wb = GetSolidWallBoundsForSign(bldg);
            float minElev = Mathf.Max(13.5f, wb.min.y + 4.5f);
            float maxElev = Mathf.Min(wb.max.y - 4.5f, 52.0f);
            if (maxElev <= minElev + 1.0f) return;

            float wallSpan = jutAlongX ? wb.size.z : wb.size.x;
            if (wallSpan < 5.5f) return;

            int count = (maxElev - minElev > 18f) ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                float elevY = count == 1
                    ? NextRandRange(minElev, maxElev)
                    : (i == 0 ? NextRandRange(minElev, Mathf.Min(minElev + 11f, maxElev))
                              : NextRandRange(Mathf.Max(minElev + 13f, maxElev - 14f), maxElev));

                bool isVertical = (signCounter % 2) == 0;
                float signW = isVertical ? NextRandRange(3.6f, 4.4f) : NextRandRange(5.6f, 7.4f);
                float signH = isVertical ? NextRandRange(9.5f, 13.0f) : NextRandRange(3.5f, 4.4f);
                float jutDist = signW + NextRandRange(0.95f, 1.55f); // Total projection 4.6m - 8.9m, never overshot

                int sIdx = signCounter % (isVertical ? assets.VerticalSignMats.Length : assets.SignMats.Length);
                int tubeIdx = StrictNeonTubeIndices[signCounter % StrictNeonTubeIndices.Length];
                Material faceMat = isVertical ? assets.VerticalSignMats[sIdx] : assets.SignMats[sIdx];

                Vector3 pos;
                if (jutAlongX)
                {
                    float wallFaceX = dirSign > 0f ? wb.max.x : wb.min.x;
                    float padZ = Mathf.Min(2.4f, wallSpan * 0.30f);
                    float latZ = NextRandRange(wb.min.z + padZ, wb.max.z - padZ);
                    pos = new Vector3(wallFaceX, elevY, latZ);
                }
                else
                {
                    float wallFaceZ = dirSign > 0f ? wb.max.z : wb.min.z;
                    float padX = Mathf.Min(2.4f, wallSpan * 0.30f);
                    float latX = NextRandRange(wb.min.x + padX, wb.max.x - padX);
                    pos = new Vector3(latX, elevY, wallFaceZ);
                }

                float jutDir = jutAlongX ? dirSign : 1f;
                GameObject sign = SetupNeonCityLevel.SpawnCantileveredNeonSign(
                    neonRoot, $"NeonSign_Cantilever_{signCounter++}", pos,
                    jutDir, jutDist, signW, signH, isVertical, faceMat, assets.NeonTubeMats[tubeIdx], assets.DarkSteelMat);

                if (!jutAlongX)
                {
                    sign.transform.rotation = Quaternion.Euler(0f, dirSign > 0f ? -90f : 90f, 0f);
                }

                float boardCenterX = jutDir * ((jutDist - signW) + signW * 0.5f);
                MakeNeonAttachmentDestructible(sign, new Vector3(signW + 1.8f, signH + 1.5f, 2.8f), new Vector3(boardCenterX, 0f, 0f), debrisMass: 5.5f);
            }
        }

        private static void SealAlleyCurb(Transform parent, string name, Vector3 pos, float spanW, float yawDeg, NeonCityAssetPack assets)
        {
            GameObject barrier = new GameObject(name);
            barrier.transform.SetParent(parent, false);
            barrier.transform.position = pos;
            barrier.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            GameObject wall = SetupNeonCityLevel.CreateSubBox(barrier.transform, "AlleyBlocker_Jersey",
                new Vector3(0f, 3.0f, 0f), new Vector3(spanW + 0.4f, 6.0f, 4.2f), assets.BarrierMat);
            wall.AddComponent<BoxCollider>();
            wall.AddComponent<UrbanBuildingImpactReceiver>();

            SetupNeonCityLevel.CreateSubBox(barrier.transform, "AlleyBlocker_Beacon",
                new Vector3(0f, 6.15f, 0f), new Vector3(spanW, 0.38f, 0.55f), assets.NeonTubeMats[2]);

            SetupNeonCityLevel.BuildBambooScaffoldingLattice(barrier.transform, spanW, 12.0f, 0.4f, assets);
        }

        // =====================================================================
        // 3. WALL-TO-WALL SUPERTALL PERIMETERS (250m-420m+) & 450m+ NORTH BANK-OF-CHINA HERO
        // =====================================================================
        private static void BuildNorthEastWestSupertallPerimeters(Transform parent, NeonCityAssetPack assets)
        {
            Transform wallRoot = CreateGroup(parent, "City_Perimeter_Supertalls");
            var northTowers = new List<Bounds>();

            // 1. 450m+ HERO LANDMARK SKYSCRAPER centered at North end of Main Avenue (Bank of China X-Braced Prism!)
            GameObject hero = SetupNeonCityLevel.SpawnTriangularPrismSupertall(
                wallRoot, "Hero_Supertall_BankOfChina_455m",
                new Vector3(0f, 0f, 283f), yawDeg: 0f, baseSize: 62f, totalHeight: 428f,
                winIdx: 5, neonIdx: 0, assets);
            Bounds heroB = AlignPerimeterTowerFrontFace(hero, isNorthWall: true, sideSign: 1f, targetLine: 252.0f, h: 400f);
            hero.AddComponent<UrbanBuildingImpactReceiver>();
            northTowers.Add(heroB);

            // 2. North Border wall-to-wall supertalls (Z = +252.0f front face)
            float[] northSlotsX = { -251f, -189.5f, -128f, -66.5f, 66.5f, 128f, 189.5f, 251f };
            for (int i = 0; i < northSlotsX.Length; i++)
            {
                Bounds tb = SpawnPerimeterSupertallArchetype(wallRoot, $"Supertall_North_{i}",
                    new Vector3(northSlotsX[i], 0f, 284f), 54f, 64f, NextRandRange(255f, 405f), i, 180f,
                    isNorthWall: true, sideSign: 1f, forceBoxArchetype: false, assets);
                northTowers.Add(tb);
            }

            // Sort all North supertalls by X and seal every gap between consecutive supertalls with +4.4m overlap
            northTowers.Sort((a, b) => a.center.x.CompareTo(b.center.x));
            for (int i = 0; i < northTowers.Count - 1; i++)
            {
                float gapMinX = northTowers[i].max.x;
                float gapMaxX = northTowers[i + 1].min.x;
                float spanW = Mathf.Max(3.5f, (gapMaxX - gapMinX) + 4.4f);
                float midX = (gapMinX + gapMaxX) * 0.5f;
                SealAlleyCurb(wallRoot, $"Perimeter_North_Alley_{i}", new Vector3(midX, 0f, 253.8f), spanW, 0f, assets);
            }

            // 3. West & East Borders (|X| = 252.0f front face)
            float[] midBandZ = { -58f, 0f, 58f };
            float[] outerBandZ = { -223f, -158f, 158f, 223f };
            for (int side = -1; side <= 1; side += 2)
            {
                string sideName = side < 0 ? "West" : "East";
                float xCenter = side * 284f;
                float yaw = side < 0 ? 90f : 270f;
                var sideTowers = new List<Bounds>();

                for (int i = 0; i < midBandZ.Length; i++)
                {
                    sideTowers.Add(SpawnPerimeterSupertallArchetype(wallRoot, $"Supertall_{sideName}_Mid_{i}",
                        new Vector3(xCenter, 0f, midBandZ[i]), 54f, 64f, NextRandRange(265f, 415f), i + (side > 0 ? 1 : 0), yaw,
                        isNorthWall: false, sideSign: side, forceBoxArchetype: false, assets));
                }
                for (int i = 0; i < outerBandZ.Length; i++)
                {
                    sideTowers.Add(SpawnPerimeterSupertallArchetype(wallRoot, $"Supertall_{sideName}_Outer_{i}",
                        new Vector3(xCenter, 0f, outerBandZ[i]), 56f, 64f, NextRandRange(255f, 405f), i + (side > 0 ? 2 : 3), yaw,
                        isNorthWall: false, sideSign: side, forceBoxArchetype: false, assets));
                }

                // Pinch both 35m Cross-Street mouths at |X| = 252 down to sealed ~10m neon alleys using solid rectangular gate towers
                float[] crossMouths = { -107.5f, 107.5f };
                for (int m = 0; m < crossMouths.Length; m++)
                {
                    float cz = crossMouths[m];
                    sideTowers.Add(SpawnPerimeterSupertallArchetype(wallRoot, $"Supertall_{sideName}_MouthN_{m}",
                        new Vector3(side * 276f, 0f, cz + 15.0f), 20f, 52f, NextRandRange(275f, 355f), m + 1, yaw,
                        isNorthWall: false, sideSign: side, forceBoxArchetype: true, assets));
                    sideTowers.Add(SpawnPerimeterSupertallArchetype(wallRoot, $"Supertall_{sideName}_MouthS_{m}",
                        new Vector3(side * 276f, 0f, cz - 15.0f), 20f, 52f, NextRandRange(275f, 355f), m + 2, yaw,
                        isNorthWall: false, sideSign: side, forceBoxArchetype: true, assets));
                }

                // Sort all side supertalls by Z and seal every consecutive gap + South/North corners with +4.4m overlap
                sideTowers.Sort((a, b) => a.center.z.CompareTo(b.center.z));
                for (int i = 0; i < sideTowers.Count - 1; i++)
                {
                    float gapMinZ = sideTowers[i].max.z;
                    float gapMaxZ = sideTowers[i + 1].min.z;
                    float spanD = Mathf.Max(3.5f, (gapMaxZ - gapMinZ) + 4.4f);
                    float midZ = (gapMinZ + gapMaxZ) * 0.5f;
                    SealAlleyCurb(wallRoot, $"Perimeter_{sideName}_SideSeal_{i}", new Vector3(side * 253.8f, 0f, midZ), spanD, yaw, assets);
                }

                // Seal South corner (between South Retaining Wall at Z=-252 and first side supertall)
                if (sideTowers.Count > 0 && sideTowers[0].min.z > -253f)
                {
                    float gapMinZ = -253.5f;
                    float gapMaxZ = sideTowers[0].min.z;
                    float spanD = Mathf.Max(3.5f, (gapMaxZ - gapMinZ) + 4.0f);
                    SealAlleyCurb(wallRoot, $"Perimeter_{sideName}_CornerSouth",
                        new Vector3(side * 253.8f, 0f, (gapMinZ + gapMaxZ) * 0.5f), spanD, yaw, assets);
                }

                // Seal North corner (between last side supertall and North supertall wall at Z=+252)
                if (sideTowers.Count > 0 && sideTowers[sideTowers.Count - 1].max.z < 253f)
                {
                    float gapMinZ = sideTowers[sideTowers.Count - 1].max.z;
                    float gapMaxZ = 253.5f;
                    float spanD = Mathf.Max(3.5f, (gapMaxZ - gapMinZ) + 4.0f);
                    SealAlleyCurb(wallRoot, $"Perimeter_{sideName}_CornerNorth",
                        new Vector3(side * 253.8f, 0f, (gapMinZ + gapMaxZ) * 0.5f), spanD, yaw, assets);
                }
            }
        }

        private static Bounds SpawnPerimeterSupertallArchetype(
            Transform parent, string name, Vector3 basePos,
            float w, float d, float h, int seed, float yawDeg,
            bool isNorthWall, float sideSign, bool forceBoxArchetype, NeonCityAssetPack assets)
        {
            int winIdx = seed % assets.WindowMats.Length;
            int neonIdx = StrictNeonTubeIndices[seed % StrictNeonTubeIndices.Length];
            int archetype = forceBoxArchetype ? 3 : (seed % 4);
            GameObject tower;

            if (archetype == 0)
            {
                float sz = Mathf.Min(w, d);
                tower = SetupNeonCityLevel.SpawnTriangularPrismSupertall(parent, name, basePos, yawDeg, sz, h, 5, neonIdx, assets);
            }
            else if (archetype == 1)
            {
                tower = SetupNeonCityLevel.SpawnTieredNeoFuturisticSpire(parent, name, basePos, Mathf.Min(w, d), h, winIdx, neonIdx, assets);
                tower.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            }
            else if (archetype == 2)
            {
                tower = SetupNeonCityLevel.SpawnCurvedWaterfrontTower(parent, name, basePos, yawDeg, w, d, h, winIdx, neonIdx, assets);
            }
            else
            {
                int kind = (seed % 2 == 0) ? 3 : 4;
                tower = SetupNeonCityLevel.SpawnTieredSkyscraper(parent, name, basePos, yawDeg, w, d, h, kind, winIdx, neonIdx, addCollider: false, assets);
            }

            Bounds aligned = AlignPerimeterTowerFrontFace(tower, isNorthWall, sideSign, targetLine: 252.0f, h);
            tower.AddComponent<UrbanBuildingImpactReceiver>();
            return aligned;
        }

        private static Bounds AlignPerimeterTowerFrontFace(GameObject tower, bool isNorthWall, float sideSign, float targetLine, float h)
        {
            Bounds mb = GetCombinedMeshBounds(tower);
            if (isNorthWall)
            {
                float shiftZ = targetLine - mb.min.z;
                tower.transform.position += new Vector3(0f, 0f, shiftZ);
            }
            else if (sideSign < 0f)
            {
                float shiftX = -targetLine - mb.max.x;
                tower.transform.position += new Vector3(shiftX, 0f, 0f);
            }
            else
            {
                float shiftX = targetLine - mb.min.x;
                tower.transform.position += new Vector3(shiftX, 0f, 0f);
            }

            mb = GetCombinedMeshBounds(tower);
            var box = tower.GetComponent<BoxCollider>();
            if (box == null) box = tower.AddComponent<BoxCollider>();
            box.isTrigger = false;
            Vector3 localCenter = tower.transform.InverseTransformPoint(new Vector3(mb.center.x, h * 0.5f, mb.center.z));
            Vector3 localSize = tower.transform.InverseTransformVector(new Vector3(mb.size.x, h, mb.size.z));
            box.center = localCenter;
            box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            return mb;
        }

        private static Bounds GetCombinedMeshBounds(GameObject go)
        {
            MeshRenderer[] mrs = go.GetComponentsInChildren<MeshRenderer>();
            if (mrs.Length == 0)
            {
                return new Bounds(go.transform.position, new Vector3(20f, 50f, 20f));
            }
            Bounds b = mrs[0].bounds;
            for (int i = 1; i < mrs.Length; i++)
            {
                b.Encapsulate(mrs[i].bounds);
            }
            return b;
        }

        // =====================================================================
        // 4. SOUTH BORDER: 100M RETAINING WALL + 3-TIER EXPRESSWAY INTERCHANGE + MID-LEVELS HILLSIDE
        // =====================================================================
        private static void BuildSouthRetainingWallInterchangeAndMidLevels(Transform parent, GameObject buildingsFbx, NeonCityAssetPack assets)
        {
            Transform southRoot = CreateGroup(parent, "City_South_RetainingWall_Interchange");

            // 1. Steep 100m Concrete Retaining Wall sealing Z <= -252 across X in [-300..+300]
            GameObject wall = SetupNeonCityLevel.CreateSubBox(southRoot, "South_100m_Retaining_Wall",
                new Vector3(0f, 50f, -274f), new Vector3(600f, 100f, 44f), assets.DarkConcreteMat);
            wall.AddComponent<BoxCollider>();
            wall.AddComponent<UrbanBuildingImpactReceiver>();

            // Structural retaining wall pilasters & concrete coping
            for (float bx = -256f; bx <= 256f; bx += 32f)
            {
                if (Mathf.Abs(bx) < 28f) continue;
                SetupNeonCityLevel.CreateSubBox(southRoot, $"Wall_Buttress_{bx:0}",
                    new Vector3(bx, 48f, -251.2f), new Vector3(4.2f, 96f, 4.0f), assets.BarrierMat, Quaternion.Euler(-3f, 0f, 0f));
            }
            SetupNeonCityLevel.CreateSubBox(southRoot, "Wall_Top_Cornice",
                new Vector3(0f, 101f, -252.5f), new Vector3(600f, 3.0f, 6.5f), assets.BarrierMat);

            // Twin Highway Tunnel Portals at South end of Main Avenue + Neon City Expressway Gantry
            for (int side = -1; side <= 1; side += 2)
            {
                float tx = side * 14.5f;
                SetupNeonCityLevel.CreateSubBox(southRoot, $"Tunnel_Surround_{(side < 0 ? "W" : "E")}",
                    new Vector3(tx, 9.5f, -251.2f), new Vector3(22f, 19f, 3.5f), assets.BarrierMat);
                SetupNeonCityLevel.CreateSubBox(southRoot, $"Tunnel_Void_{(side < 0 ? "W" : "E")}",
                    new Vector3(tx, 7.5f, -250.5f), new Vector3(16.5f, 14f, 1.4f), assets.DarkGlassMat);
                SetupNeonCityLevel.CreateSubBox(southRoot, $"Tunnel_AmberStrip_{(side < 0 ? "W" : "E")}",
                    new Vector3(tx, 15.2f, -249.6f), new Vector3(16.5f, 0.55f, 0.55f), assets.NeonTubeMats[2]);
            }
            SetupNeonCityLevel.BuildExpresswayGantry(southRoot, "South_Tunnel_Gantry", -246f, 0f, assets, baseY: 0f);

            // 2. 3-Tier Elevated Expressway Interchange decks sweeping across the retaining wall
            float[] tierYs = { 19f, 44f, 72f };
            float[] tierZs = { -237f, -243f, -248f };
            float[] tierYaws = { 0f, -2.0f, 2.5f };
            for (int t = 0; t < tierYs.Length; t++)
            {
                GameObject deck = new GameObject($"South_Interchange_Tier_{t + 1}");
                deck.transform.SetParent(southRoot, false);
                deck.transform.position = new Vector3(0f, tierYs[t], tierZs[t]);
                deck.transform.rotation = Quaternion.Euler(0f, tierYaws[t], 0f);

                SetupNeonCityLevel.CreateSubBox(deck.transform, "Girder_Slab", Vector3.zero, new Vector3(540f, 2.2f, 12.5f), assets.DarkConcreteMat);
                SetupNeonCityLevel.CreateSubBox(deck.transform, "Parapet_N", new Vector3(0f, 1.55f, 6.0f), new Vector3(540f, 1.1f, 0.6f), assets.BarrierMat);
                SetupNeonCityLevel.CreateSubBox(deck.transform, "Amber_GuardStrip", new Vector3(0f, 0.4f, 6.35f), new Vector3(540f, 0.30f, 0.25f), assets.NeonTubeMats[2]);

                // Cylindrical concrete viaduct support piers
                if (t == 0)
                {
                    for (float px = -220f; px <= 220f; px += 44f)
                    {
                        if (Mathf.Abs(px) < 34f) continue;
                        GameObject pier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        pier.name = $"Interchange_Pier_{px:0}";
                        pier.transform.SetParent(deck.transform, false);
                        pier.transform.localPosition = new Vector3(px, -tierYs[t] * 0.5f, 0f);
                        pier.transform.localScale = new Vector3(2.4f, tierYs[t] * 0.5f, 2.4f);
                        Object.DestroyImmediate(pier.GetComponent<Collider>());
                        pier.GetComponent<MeshRenderer>().sharedMaterial = assets.BarrierMat;
                    }
                }
            }

            // 3. Mid-Levels Terraced Hillside Slope (rising behind Y = 100m into low storm clouds)
            //    Built cleanly from stepped dark slope terraces + lush dark green slope berms + Neon City Mid-Levels high-rises!
            float[] slopeZ = { -282f, -326f, -372f, -422f };
            float[] slopeTopY = { 112f, 138f, 166f, 198f };
            for (int s = 0; s < slopeZ.Length; s++)
            {
                SetupNeonCityLevel.CreateSubBox(southRoot, $"MidLevels_Terrace_{s}",
                    new Vector3(0f, slopeTopY[s] - 32f, slopeZ[s]), new Vector3(640f, 64f, 62f), assets.DarkConcreteMat);
                // Stepped dark subtropical hillside greenery band along front of each terrace (using TramGreenMat matte-dark tone)
                SetupNeonCityLevel.CreateSubBox(southRoot, $"MidLevels_GreenSlope_{s}",
                    new Vector3(0f, slopeTopY[s] - 2.5f, slopeZ[s] + 22f), new Vector3(620f, 16f, 24f), assets.TramGreenMat, Quaternion.Euler(28f, 0f, 0f));
            }

            // Iconic Hong Kong Mid-Levels residential skyline rising out of the stepped hillside!
            float[] midTowerXs = { -225f, -162f, -102f, -42f, 18f, 82f, 144f, 206f };
            for (int i = 0; i < midTowerXs.Length; i++)
            {
                int tier = i % 3;
                float baseElev = slopeTopY[tier] - 4f;
                float tz = slopeZ[tier] - 4f;
                float h = NextRandRange(95f, 165f);
                int neonIdx = StrictNeonTubeIndices[i % StrictNeonTubeIndices.Length];

                if (i % 3 == 0)
                {
                    SetupNeonCityLevel.SpawnTieredNeoFuturisticSpire(southRoot, $"MidLevels_Spire_{i}",
                        new Vector3(midTowerXs[i], baseElev, tz), 26f, h, i % assets.WindowMats.Length, neonIdx, assets);
                }
                else if (i % 3 == 1)
                {
                    SetupNeonCityLevel.SpawnCurvedWaterfrontTower(southRoot, $"MidLevels_Curved_{i}",
                        new Vector3(midTowerXs[i], baseElev, tz), 0f, 28f, 20f, h, i % assets.WindowMats.Length, neonIdx, assets);
                }
                else
                {
                    SetupNeonCityLevel.SpawnTieredSkyscraper(southRoot, $"MidLevels_Tower_{i}",
                        new Vector3(midTowerXs[i], baseElev, tz), 0f, 24f, 22f, h, 3 + (i % 2),
                        i % assets.WindowMats.Length, neonIdx, addCollider: false, assets);
                }
            }
        }

        // =====================================================================
        // 5. DESTRUCTIBLE SKYBRIDGES (8 OVERHEAD SPANS 16-22M) & SOUTH PLAZA FLYOVER (15M CHUNKS)
        // =====================================================================
        private static void BuildDestructibleSkybridgesAndSouthFlyover(Transform parent, NeonCityAssetPack assets)
        {
            Transform destRoot = CreateGroup(parent, "City_Destructible_Spans");

            // A. 8 Enclosed Glass Pedestrian Skybridges (17m-21m clearance) built in Neon City glass-tube style
            (string name, Vector3 pos, float yaw, float spanLen, int chunks)[] skybridgeSpecs =
            {
                ("Skybridge_NorthAve_1",     new Vector3(0f, 18.2f, 165f),     0f, 62f, 4),
                ("Skybridge_NorthAve_2",     new Vector3(0f, 20.6f, 220f),     0f, 62f, 4),
                ("Skybridge_SouthAve_1",     new Vector3(0f, 17.8f, -165f),    0f, 62f, 4),
                ("Skybridge_SouthAve_2",     new Vector3(0f, 21.0f, -216f),    0f, 62f, 4),
                ("Skybridge_CrossNW_Inner",  new Vector3(-135f, 17.5f, 107.5f), 90f, 36.5f, 3),
                ("Skybridge_CrossNW_Outer",  new Vector3(-206f, 19.6f, 107.5f), 90f, 36.5f, 3),
                ("Skybridge_CrossNE",        new Vector3( 148f, 19.8f, 107.5f), 90f, 36.5f, 3),
                ("Skybridge_CrossSE",        new Vector3( 138f, 17.6f, -107.5f),90f, 36.5f, 3)
            };

            for (int i = 0; i < skybridgeSpecs.Length; i++)
            {
                var spec = skybridgeSpecs[i];
                BuildDestructibleNeonSkybridge(destRoot, spec.name, spec.pos, spec.yaw, spec.spanLen, 5.4f, 4.8f, spec.chunks, assets);
            }

            // B. Elevated Concrete Highway Flyover (14m high) along South edge of Central Plaza (Z = -91.5),
            //    broken into 14 independent ~15m destructible chunks spanning X in [-105..+105]
            Transform flyoverRoot = CreateGroup(destRoot, "South_Plaza_Elevated_Flyover");
            const int chunkCount = 14;
            const float chunkLen = 15.0f;
            float startX = -((chunkCount - 1) * chunkLen) * 0.5f;

            for (int i = 0; i < chunkCount; i++)
            {
                float cx = startX + i * chunkLen;
                BuildFlyoverDestructibleChunk(flyoverRoot, $"Flyover_Chunk_{i:00}", new Vector3(cx, 14.0f, -91.5f), chunkLen - 0.15f, 12.8f, assets);
            }
        }

        private static void BuildDestructibleNeonSkybridge(
            Transform parent, string bridgeName, Vector3 centerPos, float yawDeg,
            float totalSpanX, float widthZ, float heightY, int chunkCount, NeonCityAssetPack assets)
        {
            GameObject bridge = new GameObject(bridgeName);
            bridge.transform.SetParent(parent, false);
            bridge.transform.position = centerPos;
            bridge.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            float segLen = totalSpanX / chunkCount;
            for (int s = 0; s < chunkCount; s++)
            {
                float lx = -totalSpanX * 0.5f + segLen * (s + 0.5f);
                GameObject seg = SetupNeonCityLevel.BuildGlassSkybridge(
                    bridge.transform, $"City_Skybridge_{bridgeName}_Seg_{s}",
                    Vector3.zero, 0f, segLen - 0.16f, widthZ, heightY, assets);
                seg.transform.localPosition = new Vector3(lx, 0f, 0f);
                seg.transform.localRotation = Quaternion.identity;

                var col = seg.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(segLen, heightY + 0.8f, widthZ + 0.6f);

                var dest = seg.AddComponent<HongKongDestructibleStructure>();
                dest.kind = HongKongDestructibleStructure.StructureKind.SkybridgeSegment;
                dest.debrisMass = 9f;
                dest.scatterImpulse = 24f;
                dest.upwardPop = 6.5f;
                dest.cascadeAdjacentSkybridge = true;
                dest.cascadeRadius = segLen * 1.15f;
            }
        }

        private static void BuildFlyoverDestructibleChunk(Transform parent, string name, Vector3 deckCenter, float lenX, float widthZ, NeonCityAssetPack assets)
        {
            GameObject chunk = new GameObject(name);
            chunk.transform.SetParent(parent, false);
            chunk.transform.position = deckCenter;

            // Concrete box-girder expressway span styled straight from SetupNeonCityLevel viaducts
            SetupNeonCityLevel.CreateSubBox(chunk.transform, "GirderDeck", Vector3.zero, new Vector3(lenX, 1.8f, widthZ), assets.DarkConcreteMat);
            SetupNeonCityLevel.CreateSubBox(chunk.transform, "Parapet_N", new Vector3(0f, 1.35f,  widthZ * 0.47f), new Vector3(lenX, 1.0f, 0.65f), assets.BarrierMat);
            SetupNeonCityLevel.CreateSubBox(chunk.transform, "Parapet_S", new Vector3(0f, 1.35f, -widthZ * 0.47f), new Vector3(lenX, 1.0f, 0.65f), assets.BarrierMat);
            SetupNeonCityLevel.CreateSubBox(chunk.transform, "Neon_GuardStrip", new Vector3(0f, 0.15f, widthZ * 0.51f), new Vector3(lenX, 0.25f, 0.22f), assets.NeonTubeMats[2]);

            // Cylindrical breakaway support pier under this ~15m chunk
            GameObject pier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pier.name = "SupportPier";
            pier.transform.SetParent(chunk.transform, false);
            pier.transform.localPosition = new Vector3(0f, -7.2f, 0f);
            pier.transform.localScale = new Vector3(2.2f, 6.4f, 2.2f);
            Object.DestroyImmediate(pier.GetComponent<Collider>());
            pier.GetComponent<MeshRenderer>().sharedMaterial = assets.BarrierMat;

            var trig = chunk.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.center = new Vector3(0f, -4.2f, 0f);
            trig.size = new Vector3(lenX + 0.5f, 15.5f, widthZ + 1.0f);

            var dest = chunk.AddComponent<HongKongDestructibleStructure>();
            dest.kind = HongKongDestructibleStructure.StructureKind.FlyoverChunk;
            dest.debrisMass = 22f;
            dest.scatterImpulse = 19f;
            dest.upwardPop = 5.0f;
            dest.tumbleTorque = 5.5f;
            dest.cascadeAdjacentSkybridge = false;
        }

        // =====================================================================
        // 6. STREET COLLATERAL TRAFFIC (UNIFIED 4-WHEELED 3D BUILDINGS.FBX VEHICLES ALIGNED TO LANES)
        // =====================================================================
        private static void BuildStreetCollateralTraffic(Transform parent, GameObject buildingsFbx, NeonCityAssetPack assets)
        {
            Transform colRoot = CreateGroup(parent, "City_Street_Collateral_Traffic");
            if (buildingsFbx == null) return;

            // Consistent 3D vehicle models from Buildings.fbx (each spawned with its 4 matching 3D wheels, DarkGlassMat windows & optics)
            string[] carTypes =
            {
                "Sedan Body", "SUV Body", "Pickup Body", "Compact Body",
                "Sport body", "Hatchback Body", "Wagon Body", "minivan body", "Offroad Body"
            };

            // Lane-aligned traffic across Main N-S Avenue, East-West Plaza Boulevard, and both 36m Cross-Streets
            (Vector3 pos, float yaw)[] vehiclePlacements =
            {
                // North Main Avenue northbound (+Z, yaw=0) & southbound (-Z, yaw=180) lanes
                (new Vector3(-16f, 0.03f,  144f), 180f), (new Vector3( 16f, 0.03f,  156f),   0f),
                (new Vector3( -9f, 0.03f,  182f), 180f), (new Vector3(  9f, 0.03f,  198f),   0f),
                (new Vector3(-21f, 0.03f,  222f), 180f), (new Vector3( 21f, 0.03f,  234f),   0f),

                // South Main Avenue northbound (+Z, yaw=0) & southbound (-Z, yaw=180) lanes
                (new Vector3(-16f, 0.03f, -144f), 180f), (new Vector3( 16f, 0.03f, -156f),   0f),
                (new Vector3( -9f, 0.03f, -182f), 180f), (new Vector3(  9f, 0.03f, -198f),   0f),
                (new Vector3(-21f, 0.03f, -222f), 180f), (new Vector3( 21f, 0.03f, -234f),   0f),

                // Central Plaza E-W Boulevard outer approach lanes (clear of central brawl spawn)
                (new Vector3(-68f, 0.03f,  -12f),  90f), (new Vector3(-54f, 0.03f,   12f), 270f),
                (new Vector3( 54f, 0.03f,  -12f),  90f), (new Vector3( 68f, 0.03f,   12f), 270f),

                // North Cross-Street (Z = +108) eastbound (+X, yaw=90) & westbound (-X, yaw=270) lanes
                (new Vector3(-210f, 0.03f, 101f),  90f), (new Vector3(-165f, 0.03f, 115f), 270f),
                (new Vector3(-115f, 0.03f, 101f),  90f), (new Vector3( -65f, 0.03f, 115f), 270f),
                (new Vector3(  65f, 0.03f, 101f),  90f), (new Vector3( 115f, 0.03f, 115f), 270f),
                (new Vector3( 165f, 0.03f, 101f),  90f), (new Vector3( 210f, 0.03f, 115f), 270f),

                // South Cross-Street (Z = -108) eastbound (+X, yaw=90) & westbound (-X, yaw=270) lanes
                (new Vector3(-210f, 0.03f,-115f),  90f), (new Vector3(-165f, 0.03f,-101f), 270f),
                (new Vector3(-115f, 0.03f,-115f),  90f), (new Vector3( -65f, 0.03f,-101f), 270f),
                (new Vector3(  65f, 0.03f,-115f),  90f), (new Vector3( 115f, 0.03f,-101f), 270f),
                (new Vector3( 165f, 0.03f,-115f),  90f), (new Vector3( 210f, 0.03f,-101f), 270f)
            };

            for (int i = 0; i < vehiclePlacements.Length; i++)
            {
                var vp = vehiclePlacements[i];
                string modelName = carTypes[i % carTypes.Length];
                Material bodyMat = assets.CarBodyMats[i % assets.CarBodyMats.Length];
                SetupNeonCityLevel.SpawnCollateralCar(
                    colRoot, buildingsFbx, modelName, vp.pos, vp.yaw,
                    bodyMat, assets.TailLightMat, assets.DarkGlassMat, assets.DarkSteelMat);
            }
        }

        // =====================================================================
        // 7. TYPHOON CLOUDS, MIST RIBBONS, REFLECTION PROBES, NEON SPILL LIGHTS & SKY SEARCHLIGHTS
        // =====================================================================
        private static void BuildCloudsMistReflectionsLightsAndSearchlights(Transform parent, NeonCityAssetPack assets)
        {
            Transform fxRoot = CreateGroup(parent, "City_Clouds_Mist_Reflections_And_Lights");

            // 1. Heavy Storm Cloud Ceiling above ~220m partially swallowing the upper thirds of 250m-455m supertalls
            for (int layer = 0; layer < 32; layer++)
            {
                float cz = -260f + (layer / 4) * 76f + (layer % 3) * 18f;
                float cx = ((layer % 4) - 1.5f) * 165f;
                float cy = 222f + (layer % 5) * 18f;
                float w = 320f + (layer % 3) * 85f;
                float tilt = (layer % 2 == 0) ? 84f : 96f;

                GameObject cloud = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cloud.name = $"Cloud_StormCeiling_{layer}";
                cloud.transform.SetParent(fxRoot, false);
                cloud.transform.position = new Vector3(cx, cy, cz);
                cloud.transform.rotation = Quaternion.Euler(tilt, (layer * 37f) % 360f, 0f);
                cloud.transform.localScale = new Vector3(w, w * 0.85f, 1f);
                Object.DestroyImmediate(cloud.GetComponent<Collider>());
                var mr = cloud.GetComponent<MeshRenderer>();
                mr.sharedMaterial = assets.CloudCeilingMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            // 2. Street-Level Rain Mist Ribbons (0m - 30m)
            Vector3[] mistPositions =
            {
                new Vector3(  0f,  7f,   0f),   new Vector3(-48f,  9f,  48f),
                new Vector3( 48f,  9f, -48f),   new Vector3(  0f, 14f, 140f),
                new Vector3(  0f, 14f,-140f),   new Vector3(-135f, 11f, 107.5f),
                new Vector3(135f, 11f, 107.5f), new Vector3(-135f, 11f,-107.5f),
                new Vector3(135f, 11f,-107.5f), new Vector3(  0f, 20f, 215f),
                new Vector3(  0f, 20f,-215f)
            };
            for (int i = 0; i < mistPositions.Length; i++)
            {
                GameObject mist = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mist.name = $"Cloud_StreetMist_{i}";
                mist.transform.SetParent(fxRoot, false);
                mist.transform.position = mistPositions[i];
                mist.transform.rotation = Quaternion.Euler(14f, i * 38f, 0f);
                mist.transform.localScale = new Vector3(68f, 18f, 1f);
                Object.DestroyImmediate(mist.GetComponent<Collider>());
                var mr = mist.GetComponent<MeshRenderer>();
                mr.sharedMaterial = assets.MistPoolMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            // 3. Box-Projected Realtime (OnAwake) Reflection Probes so wet asphalt mirrors neon signage
            Vector3[] probePositions =
            {
                new Vector3(   0f, 18f,    0f),
                new Vector3(   0f, 18f,  155f),
                new Vector3(   0f, 18f, -155f),
                new Vector3(-135f, 18f,  107.5f),
                new Vector3( 135f, 18f, -107.5f)
            };
            Vector3[] probeSizes =
            {
                new Vector3(195f, 90f, 195f),
                new Vector3(145f, 95f, 180f),
                new Vector3(145f, 95f, 180f),
                new Vector3(240f, 85f,  90f),
                new Vector3(240f, 85f,  90f)
            };
            for (int i = 0; i < probePositions.Length; i++)
            {
                GameObject pGo = new GameObject($"ReflectionProbe_NeonCity_{i}");
                pGo.transform.SetParent(fxRoot, false);
                pGo.transform.position = probePositions[i];
                var probe = pGo.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                probe.resolution = 256;
                probe.boxProjection = true;
                probe.size = probeSizes[i];
                probe.intensity = 1.35f;
                probe.farClipPlane = 650f;
            }

            // 4. Forward+ Non-Shadow Neon Spill Point Lights (Crimson, Cyan, Jade, Amber)
            Vector3[] spillPositions =
            {
                new Vector3(-56f, 20f,  46f), new Vector3( 56f, 20f,  46f),
                new Vector3(-56f, 20f, -46f), new Vector3( 56f, 20f, -46f),
                new Vector3(-24f, 22f, 148f), new Vector3( 24f, 22f, 188f),
                new Vector3(-24f, 22f, 228f), new Vector3( 24f, 22f,-148f),
                new Vector3(-24f, 22f,-188f), new Vector3( 24f, 22f,-228f),
                new Vector3(-86f, 24f, 107.5f), new Vector3(-164f, 24f, 107.5f),
                new Vector3( 86f, 24f, 107.5f), new Vector3( 164f, 24f, 107.5f),
                new Vector3(-86f, 24f,-107.5f), new Vector3(-164f, 24f,-107.5f),
                new Vector3( 86f, 24f,-107.5f), new Vector3( 164f, 24f,-107.5f),
                new Vector3(  0f, 34f, 242f),   new Vector3(  0f, 40f,-234f)
            };
            for (int i = 0; i < spillPositions.Length; i++)
            {
                GameObject lGo = new GameObject($"NeonSpill_Light_{i}");
                lGo.transform.SetParent(fxRoot, false);
                lGo.transform.position = spillPositions[i];
                var l = lGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = StrictLightColors[i % StrictLightColors.Length];
                l.intensity = 240f;
                l.range = 68f;
                l.shadows = LightShadows.None;
            }

            // 5. Iconic Neon City Volumetric Sky Searchlight Beams piercing upward into typhoon rain clouds!
            (Vector3 pos, Vector3 tilt, int beamIdx)[] searchlights =
            {
                (new Vector3(-86f,  2f,  86f), new Vector3(-22f,  28f,  12f), 0),
                (new Vector3( 86f,  2f,  86f), new Vector3(-20f, -32f, -14f), 1),
                (new Vector3(-86f,  2f, -86f), new Vector3( 18f,  35f,  10f), 2),
                (new Vector3( 86f,  2f, -86f), new Vector3( 20f, -26f, -12f), 0),
                (new Vector3(-34f, 26f, 242f), new Vector3(-12f,  18f,   8f), 1),
                (new Vector3( 34f, 26f, 242f), new Vector3(-12f, -18f,  -8f), 2)
            };
            for (int i = 0; i < searchlights.Length; i++)
            {
                var sl = searchlights[i];
                SetupNeonCityLevel.BuildVolumetricSearchlight(fxRoot, $"Searchlight_Sky_{i}",
                    sl.pos, sl.tilt, assets.VolumetricBeamMats[sl.beamIdx % assets.VolumetricBeamMats.Length], assets.DarkSteelMat);
            }
        }

        // =====================================================================
        // HELPERS
        // =====================================================================
        private static void EnsureStaticBuildingCollider(GameObject go, float w, float h, float d)
        {
            if (go == null) return;
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, h * 0.5f, 0f);
            box.size = new Vector3(w, h, d);
            box.isTrigger = false;
        }

        private static void MakeNeonAttachmentDestructible(GameObject go, Vector3 triggerSize, Vector3 triggerCenter, float debrisMass)
        {
            if (go == null) return;
            var trig = go.GetComponent<BoxCollider>();
            if (trig == null) trig = go.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.center = triggerCenter;
            trig.size = triggerSize;

            var dest = go.GetComponent<HongKongDestructibleStructure>();
            if (dest == null) dest = go.AddComponent<HongKongDestructibleStructure>();
            dest.kind = HongKongDestructibleStructure.StructureKind.NeonFacadeAttachment;
            dest.debrisMass = debrisMass;
            dest.scatterImpulse = 16f;
            dest.tumbleTorque = 7.5f;
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static float NextRand01()
        {
            rngSeed = unchecked(rngSeed * 1664525 + 1013904223);
            uint v = (uint)rngSeed >> 8;
            return (v % 10000) / 10000f;
        }

        private static float NextRandRange(float min, float max) => Mathf.Lerp(min, max, NextRand01());

        private static int NextRandInt(int minInc, int maxExc)
        {
            if (maxExc <= minInc) return minInc;
            return minInc + Mathf.FloorToInt(NextRand01() * (maxExc - minInc - 0.0001f));
        }
    }
}
