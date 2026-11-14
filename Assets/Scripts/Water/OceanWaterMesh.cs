using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Deforms a 3D ocean mesh in real-time based on WaterSystem swells and footstep shockwaves.
    /// Computes dynamic crest foam and finite-difference surface normals.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [ExecuteAlways]
    public class OceanWaterMesh : MonoBehaviour
    {
        [Header("Ocean Mesh Dimensions")]
        [Tooltip("Physical width of the ocean mesh in meters.")]
        public float meshWidth = 140f;
        [Tooltip("Physical length of the ocean mesh in meters.")]
        public float meshLength = 140f;

        [Header("Resolution")]
        [Range(32, 160)]
        [Tooltip("Vertex count along X and Z.")]
        public int resolution = 80;

        [Header("Foam Settings")]
        [Tooltip("Height above mean ocean level where wave crest foam starts appearing.")]
        public float foamHeightThreshold = 0.55f;
        [Tooltip("Turbulence ripple amplitude that generates churning white water.")]
        public float rippleFoamThreshold = 0.25f;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private WaterSystem waterSystem;

        private Vector3[] baseVertices;
        private Vector3[] deformedVertices;
        private Vector3[] normals;
        private Color[] vertexColors;
        private Vector2[] uvs;
        private int[] triangles;

        private float dx;
        private float dz;

        void OnEnable()
        {
            InitMesh();
        }

        void Start()
        {
            InitMesh();
        }

        public void InitMesh()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (waterSystem == null) waterSystem = GetComponent<WaterSystem>();
            if (waterSystem == null) waterSystem = WaterSystem.Instance;

            GenerateOceanGrid();
        }

        public void GenerateOceanGrid()
        {
            resolution = Mathf.Clamp(resolution, 16, 160);
            int vertCount = resolution * resolution;

            baseVertices = new Vector3[vertCount];
            deformedVertices = new Vector3[vertCount];
            normals = new Vector3[vertCount];
            vertexColors = new Color[vertCount];
            uvs = new Vector2[vertCount];

            dx = meshWidth / (resolution - 1);
            dz = meshLength / (resolution - 1);

            float halfW = meshWidth * 0.5f;
            float halfL = meshLength * 0.5f;

            for (int z = 0; z < resolution; z++)
            {
                float zPos = (z * dz) - halfL;
                float v = (float)z / (resolution - 1);

                for (int x = 0; x < resolution; x++)
                {
                    int idx = z * resolution + x;
                    float xPos = (x * dx) - halfW;
                    float u = (float)x / (resolution - 1);

                    baseVertices[idx] = new Vector3(xPos, 0f, zPos);
                    deformedVertices[idx] = baseVertices[idx];
                    normals[idx] = Vector3.up;
                    vertexColors[idx] = Color.clear;
                    uvs[idx] = new Vector2(u * (meshWidth / 10f), v * (meshLength / 10f));
                }
            }

            int quadCount = (resolution - 1) * (resolution - 1);
            triangles = new int[quadCount * 6];
            int triIdx = 0;

            for (int z = 0; z < resolution - 1; z++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    int root = z * resolution + x;
                    int right = root + 1;
                    int up = root + resolution;
                    int upRight = up + 1;

                    triangles[triIdx++] = root;
                    triangles[triIdx++] = up;
                    triangles[triIdx++] = right;

                    triangles[triIdx++] = right;
                    triangles[triIdx++] = up;
                    triangles[triIdx++] = upRight;
                }
            }

            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "PacificRim_OceanMesh";
                mesh.MarkDynamic();
            }
            else
            {
                mesh.Clear();
            }

            if (vertCount > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = deformedVertices;
            mesh.normals = normals;
            mesh.colors = vertexColors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            if (meshFilter != null)
            {
                meshFilter.sharedMesh = mesh;
            }
        }

        void Update()
        {
            if (waterSystem == null)
            {
                waterSystem = WaterSystem.Instance;
                if (waterSystem == null) return;
            }

            if (mesh == null || deformedVertices == null || deformedVertices.Length != resolution * resolution)
            {
                GenerateOceanGrid();
                return;
            }

            float time = Application.isPlaying ? Time.time : 0f;
            Vector3 oceanPos = transform.position;

            float invDx2 = 1.0f / (2.0f * dx);
            float invDz2 = 1.0f / (2.0f * dz);

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int idx = z * resolution + x;
                    Vector3 basePt = baseVertices[idx];

                    float worldX = oceanPos.x + basePt.x;
                    float worldZ = oceanPos.z + basePt.z;

                    // Oceanic swell displacement
                    Vector3 swell = waterSystem.EvaluateOceanSwell(worldX, worldZ, time);
                    // Interactive footstep ripple elevation
                    float rippleH = waterSystem.SampleRippleHeight(worldX, worldZ);

                    float totalElevation = swell.y + rippleH;

                    deformedVertices[idx].x = basePt.x + swell.x;
                    deformedVertices[idx].y = totalElevation;
                    deformedVertices[idx].z = basePt.z + swell.z;

                    // Compute foam from crest peaks + footstep impact churning
                    float crestFoam = Mathf.Clamp01((totalElevation - foamHeightThreshold) / 0.5f);
                    float rippleFoam = Mathf.Clamp01((Mathf.Abs(rippleH) - rippleFoamThreshold) / 0.35f);
                    float totalFoam = Mathf.Max(crestFoam, rippleFoam);
                    vertexColors[idx] = new Color(totalFoam, totalFoam, totalFoam, 1f);

                    // Surface normal via central difference
                    int xL = Mathf.Max(0, x - 1);
                    int xR = Mathf.Min(resolution - 1, x + 1);
                    int zD = Mathf.Max(0, z - 1);
                    int zU = Mathf.Min(resolution - 1, z + 1);

                    float dhdx = (deformedVertices[z * resolution + xR].y - deformedVertices[z * resolution + xL].y) * invDx2;
                    float dhdz = (deformedVertices[zU * resolution + x].y - deformedVertices[zD * resolution + x].y) * invDz2;

                    normals[idx] = new Vector3(-dhdx, 1.0f, -dhdz).normalized;
                }
            }

            mesh.vertices = deformedVertices;
            mesh.normals = normals;
            mesh.colors = vertexColors;
        }
    }
}
