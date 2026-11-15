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
        private static readonly int IdJaegerVelocityWS = Shader.PropertyToID("_JaegerVelocityWS");
        private static readonly int IdKaijuPositionWS = Shader.PropertyToID("_KaijuPositionWS");
        private static readonly int IdKaijuVelocityWS = Shader.PropertyToID("_KaijuVelocityWS");

        private Vector3 prevJaegerPos;
        private Vector3 prevKaijuPos;
        private Transform kaijuTransform;
        private bool hasInitializedTracking = false;

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
            Vector3 jaegerVel = Vector3.zero;
            if (target != null)
            {
                if (!hasInitializedTracking)
                {
                    prevJaegerPos = target.position;
                }
                jaegerVel = (target.position - prevJaegerPos) / Mathf.Max(0.0001f, Time.deltaTime);
                prevJaegerPos = target.position;

                if (dx > 0.001f && dz > 0.001f)
                {
                    float snappedX = Mathf.Round(target.position.x / dx) * dx;
                    float snappedZ = Mathf.Round(target.position.z / dz) * dz;
                    transform.position = new Vector3(snappedX, transform.position.y, snappedZ);
                }

                if (jaegerInteraction == null)
                {
                    jaegerInteraction = target.GetComponent<JaegerWaterInteraction>();
                }
            }

            float jSpeed = new Vector2(jaegerVel.x, jaegerVel.z).magnitude;
            Shader.SetGlobalVector(IdJaegerVelocityWS, new Vector4(jaegerVel.x, 0f, jaegerVel.z, jSpeed));

            if (jaegerInteraction != null && jaegerInteraction.IsWading)
            {
                Vector3 leftLeg = jaegerInteraction.LeftLegSurfacePoint;
                Vector3 rightLeg = jaegerInteraction.RightLegSurfacePoint;
                float legRad = Mathf.Max(2.2f, jaegerInteraction.legWidth * 0.95f);
                Shader.SetGlobalVector(IdLeftLegWS, new Vector4(leftLeg.x, leftLeg.y, leftLeg.z, legRad));
                Shader.SetGlobalVector(IdRightLegWS, new Vector4(rightLeg.x, rightLeg.y, rightLeg.z, legRad));
            }
            else
            {
                Shader.SetGlobalVector(IdLeftLegWS, Vector4.zero);
                Shader.SetGlobalVector(IdRightLegWS, Vector4.zero);
            }

            // Track Kaiju position and velocity for reactive titan water displacement
            if (kaijuTransform == null)
            {
                var kaijuGo = GameObject.Find("Kaiju_Leatherback");
                if (kaijuGo != null)
                {
                    kaijuTransform = kaijuGo.transform;
                    prevKaijuPos = kaijuTransform.position;
                }
            }

            if (kaijuTransform != null)
            {
                if (!hasInitializedTracking)
                {
                    prevKaijuPos = kaijuTransform.position;
                }
                Vector3 kaijuVel = (kaijuTransform.position - prevKaijuPos) / Mathf.Max(0.0001f, Time.deltaTime);
                prevKaijuPos = kaijuTransform.position;
                Vector3 kPos = kaijuTransform.position;
                float kSpeed = new Vector2(kaijuVel.x, kaijuVel.z).magnitude;
                Shader.SetGlobalVector(IdKaijuPositionWS, new Vector4(kPos.x, kPos.y, kPos.z, 4.8f));
                Shader.SetGlobalVector(IdKaijuVelocityWS, new Vector4(kaijuVel.x, 0f, kaijuVel.z, kSpeed));
            }
            else
            {
                Shader.SetGlobalVector(IdKaijuPositionWS, Vector4.zero);
                Shader.SetGlobalVector(IdKaijuVelocityWS, Vector4.zero);
            }

            hasInitializedTracking = true;
        }
    }
}
