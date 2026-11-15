using UnityEngine;
using PacificRimAudio;

namespace PacificRimWater
{
    /// <summary>
    /// Controls the continuous rotation of Gipsy Danger's chest nuclear vortex turbine blade assembly ('Spinning Blade.002').
    /// Dynamically scales RPM based on Jaeger controller states (Idle, Locomotion, Sprinting, Hyper-Armor Sprint Attacks, and Nuclear Overcharge).
    /// Deforms the turbine mesh instance smoothly in LateUpdate with zero garbage collection allocations, allowing
    /// SkinnedMeshRenderer bone skinning to preserve flawless torso alignment during all combat animations and cutscenes.
    /// Synchronizes pitch and volume with the PacificRimAudioManager turbine loop.
    /// </summary>
    [DisallowMultipleComponent]
    public class JaegerTurbineSpinner : MonoBehaviour
    {
        [Header("Mesh References")]
        [Tooltip("SkinnedMeshRenderer containing the Spinning Blade mesh. Auto-resolved if null.")]
        [SerializeField] private SkinnedMeshRenderer bladeRenderer;

        [Header("RPM Parameters")]
        [Tooltip("Baseline idle RPM in combat or showcase menus.")]
        [SerializeField] private float idleRpm = 200f;

        [Tooltip("RPM during normal walking/strafing.")]
        [SerializeField] private float walkRpm = 340f;

        [Tooltip("RPM while sprinting across the ocean.")]
        [SerializeField] private float sprintRpm = 680f;

        [Tooltip("RPM during hyper-armor rocket sprint attack.")]
        [SerializeField] private float sprintAttackRpm = 1150f;

        [Tooltip("RPM during Nuclear Overcharge [Q] power-up.")]
        [SerializeField] private float overchargeRpm = 1450f;

        [Tooltip("RPM ramp acceleration (RPM per second).")]
        [SerializeField] private float acceleration = 750f;

        [Tooltip("RPM ramp deceleration (RPM per second).")]
        [SerializeField] private float deceleration = 400f;

        [Header("Rotation Settings")]
        [Tooltip("Spin clockwise when viewed from the front.")]
        [SerializeField] private bool clockwise = true;

        [Tooltip("Local spin axis in mesh coordinate space (Z is normal out of the chest).")]
        [SerializeField] private Vector3 localSpinAxis = Vector3.forward;

        [Header("Controller Reference")]
        [SerializeField] private SimpleJaegerController jaegerController;

        // Runtime buffers & instanced mesh
        private Mesh instancedMesh;
        private Vector3[] baseVertices;
        private Vector3[] baseNormals;
        private Vector3[] currentVertices;
        private Vector3[] currentNormals;

        private float currentRpm;
        private float currentAngle;
        private int cachedSpeedHash;

        public float CurrentRpm => currentRpm;
        public float TargetRpm { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            InitializeMeshInstance();
            cachedSpeedHash = Animator.StringToHash("Speed");
            currentRpm = idleRpm;
        }

        private void OnEnable()
        {
            if (instancedMesh == null)
            {
                InitializeMeshInstance();
            }
        }

        private void OnDisable()
        {
            // Do not delete mesh on disable to allow fast toggles, handled in OnDestroy
        }

        private void OnDestroy()
        {
            CleanupMeshInstance();
        }

        public void ResolveReferences()
        {
            if (jaegerController == null)
            {
                jaegerController = GetComponent<SimpleJaegerController>() ?? GetComponentInParent<SimpleJaegerController>();
            }

            if (bladeRenderer == null)
            {
                foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.name.Contains("Spinning") || smr.name.Contains("Blade") ||
                        (smr.sharedMesh != null && smr.sharedMesh.name.Contains("Blade")))
                    {
                        bladeRenderer = smr;
                        break;
                    }
                }
            }
        }

        private void InitializeMeshInstance()
        {
            if (bladeRenderer == null || bladeRenderer.sharedMesh == null)
            {
                ResolveReferences();
            }

            if (bladeRenderer == null || bladeRenderer.sharedMesh == null) return;

            Mesh source = bladeRenderer.sharedMesh;
            // Prevent duplicate instantiation if already an instanced copy
            if (source.name.EndsWith("_TurbineInstance"))
            {
                instancedMesh = source;
            }
            else
            {
                instancedMesh = Instantiate(source);
                instancedMesh.name = source.name + "_TurbineInstance";
                bladeRenderer.sharedMesh = instancedMesh;
            }

            instancedMesh.MarkDynamic();

            baseVertices = source.vertices;
            baseNormals = source.normals;

            if (baseVertices != null && baseVertices.Length > 0)
            {
                currentVertices = new Vector3[baseVertices.Length];
                currentNormals = new Vector3[baseNormals != null ? baseNormals.Length : baseVertices.Length];
                System.Array.Copy(baseVertices, currentVertices, baseVertices.Length);
                if (baseNormals != null && baseNormals.Length == baseVertices.Length)
                {
                    System.Array.Copy(baseNormals, currentNormals, baseNormals.Length);
                }
            }
        }

        private void CleanupMeshInstance()
        {
            if (instancedMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(instancedMesh);
                }
                else
                {
                    DestroyImmediate(instancedMesh);
                }
                instancedMesh = null;
            }
        }

        private void LateUpdate()
        {
            if (instancedMesh == null || currentVertices == null || baseVertices == null) return;

            CalculateTargetRpm();

            // Smooth RPM ramp
            float rampRate = (currentRpm < TargetRpm) ? acceleration : deceleration;
            currentRpm = Mathf.MoveTowards(currentRpm, TargetRpm, rampRate * Time.deltaTime);

            if (currentRpm <= 0.01f) return;

            // Compute delta rotation angle: RPM * 360 / 60 = RPM * 6 deg/sec
            float degreesPerSecond = currentRpm * 6f;
            float dir = clockwise ? -1f : 1f;
            currentAngle = (currentAngle + degreesPerSecond * dir * Time.deltaTime) % 360f;

            Quaternion rot = Quaternion.AngleAxis(currentAngle, localSpinAxis);

            // Rotate vertices and normals in mesh space
            int count = baseVertices.Length;
            for (int i = 0; i < count; i++)
            {
                currentVertices[i] = rot * baseVertices[i];
            }

            if (baseNormals != null && baseNormals.Length == count)
            {
                for (int i = 0; i < count; i++)
                {
                    currentNormals[i] = rot * baseNormals[i];
                }
                instancedMesh.normals = currentNormals;
            }

            instancedMesh.vertices = currentVertices;

            // Synchronize audio feedback with PacificRimAudioManager
            if (PacificRimAudioManager.Instance != null && currentRpm > 0f)
            {
                float normalizedSpeed = Mathf.InverseLerp(idleRpm, overchargeRpm, currentRpm);
                PacificRimAudioManager.Instance.UpdateTurbineAudio(normalizedSpeed);
            }
        }

        private void CalculateTargetRpm()
        {
            if (jaegerController == null)
            {
                // Showcase / Menu Jaeger runs smoothly at idle RPM
                TargetRpm = idleRpm;
                return;
            }

            if (jaegerController.IsDead)
            {
                TargetRpm = 0f;
                return;
            }

            if (jaegerController.IsPoweredUp)
            {
                TargetRpm = overchargeRpm;
                return;
            }

            if (jaegerController.IsSprintAttacking)
            {
                TargetRpm = sprintAttackRpm;
                return;
            }

            if (jaegerController.isRunning)
            {
                TargetRpm = sprintRpm;
                return;
            }

            if (jaegerController.IsEmoting)
            {
                TargetRpm = sprintRpm * 1.2f;
                return;
            }

            if (jaegerController.IsExhausted)
            {
                TargetRpm = idleRpm * 0.35f;
                return;
            }

            bool isMoving = false;
            if (jaegerController.animator != null)
            {
                isMoving = jaegerController.animator.GetFloat(cachedSpeedHash) > 0.08f;
            }

            TargetRpm = isMoving ? walkRpm : idleRpm;
        }

        /// <summary>
        /// Explicitly force a temporary RPM surge (e.g. for cutscene splashdown or heavy parry reaction).
        /// </summary>
        public void SetRpmInstant(float rpm)
        {
            currentRpm = rpm;
        }
    }
}
