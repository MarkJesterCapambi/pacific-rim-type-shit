using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Static high-density GPU-displaced ocean grid.
    /// Builds the mesh once on initialization and performs zero per-frame CPU vertex updates,
    /// horizontally snapping around followTarget and uploading leg intersection points to the shader.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [ExecuteAlways]
    public class OceanWaterMesh : MonoBehaviour
    {
        [Header("Ocean Mesh Dimensions")]
        [Tooltip("Physical width of the ocean mesh in meters.")]
        public float meshWidth = 360f;
        [Tooltip("Physical length of the ocean mesh in meters.")]
        public float meshLength = 360f;

        [Header("Resolution")]
        [Range(32, 200)]
        [Tooltip("Vertex count along X and Z.")]
        public int resolution = 140;

        [Header("Foam Settings")]
        public float foamHeightThreshold = 0.76f;
        public float rippleFoamThreshold = 0.20f;

        [Header("Follow & Leg Contact Interaction")]
        [Tooltip("Optional target for horizontal grid snapping so the ocean follows the Jaeger seamlessly.")]
        public Transform followTarget;
        [Tooltip("JaegerWaterInteraction used to pass submerged thigh surface positions to the GPU shader.")]
        public JaegerWaterInteraction jaegerInteraction;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private WaterSystem waterSystem;

        private float dx;
        private float dz;
        private float builtWidth;
        private float builtLength;
        private int builtResolution;

        private static readonly int IdLeftLegWS = Shader.PropertyToID("_JaegerLeftLegWS");
        private static readonly int IdRightLegWS = Shader.PropertyToID("_JaegerRightLegWS");

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
            if (waterSystem == null) waterSystem = GetComponent<WaterSystem>() ?? WaterSystem.Instance;

            GenerateOceanGrid();
        }

        public void GenerateOceanGrid()
        {
            resolution = Mathf.Clamp(resolution, 32, 200);
            int vertCount = resolution * resolution;

            Vector3[] vertices = new Vector3[vertCount];
            Vector3[] normals = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];

            dx = meshWidth / (resolution - 1);
            dz = meshLength / (resolution - 1);
            builtWidth = meshWidth;
            builtLength = meshLength;
            builtResolution = resolution;

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

                    vertices[idx] = new Vector3(xPos, 0f, zPos);
                    normals[idx] = Vector3.up;
                    uvs[idx] = new Vector2(u, v);
                }
            }

            int quadCount = (resolution - 1) * (resolution - 1);
            int[] triangles = new int[quadCount * 6];
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
            }
            else
            {
                mesh.Clear();
            }

            mesh.indexFormat = vertCount > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            // Generous vertical bounds prevent frustum culling when vertices displace on GPU
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(meshWidth, 18f, meshLength));

            if (meshFilter != null)
            {
                meshFilter.sharedMesh = mesh;
            }
        }

        void Update()
        {
            if (waterSystem == null)
            {
                waterSystem = GetComponent<WaterSystem>() ?? WaterSystem.Instance;
            }

            bool gridDirty = mesh == null
                          || builtResolution != resolution
                          || !Mathf.Approximately(builtWidth, meshWidth)
                          || !Mathf.Approximately(builtLength, meshLength);

            if (gridDirty)
            {
                GenerateOceanGrid();
            }

            Transform target = followTarget != null ? followTarget : (waterSystem != null ? waterSystem.followTarget : null);
            if (target != null && dx > 0.001f && dz > 0.001f)
            {
                float snappedX = Mathf.Round(target.position.x / dx) * dx;
                float snappedZ = Mathf.Round(target.position.z / dz) * dz;
                transform.position = new Vector3(snappedX, transform.position.y, snappedZ);

                if (jaegerInteraction == null)
                {
                    jaegerInteraction = target.GetComponent<JaegerWaterInteraction>();
                }
            }

            if (jaegerInteraction != null && jaegerInteraction.IsWading)
            {
                Vector3 leftLeg = jaegerInteraction.LeftLegSurfacePoint;
                Vector3 rightLeg = jaegerInteraction.RightLegSurfacePoint;
                Shader.SetGlobalVector(IdLeftLegWS, new Vector4(leftLeg.x, leftLeg.y, leftLeg.z, 1f));
                Shader.SetGlobalVector(IdRightLegWS, new Vector4(rightLeg.x, rightLeg.y, rightLeg.z, Mathf.Max(1.0f, jaegerInteraction.legWidth)));
            }
            else
            {
                Shader.SetGlobalVector(IdLeftLegWS, Vector4.zero);
                Shader.SetGlobalVector(IdRightLegWS, Vector4.zero);
            }
        }
    }
}
