using System.Collections.Generic;
using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Thunderstorm lightning controller managing a pre-baked pool of low-poly 3D bolt meshes,
    /// cloud-embedded Voronoi sheet lightning billboards, a frame-counted directional GI flash light,
    /// and the global _GlobalLightningFlash shader parameter.
    /// </summary>
    [ExecuteAlways]
    public class LightningSystem : MonoBehaviour
    {
        public static LightningSystem Instance { get; private set; }
        public static System.Action OnLightningStrike;

        [Header("Global Illumination Flash Light")]
        [Tooltip("Dedicated directional light enabled only for 1-2 frames per lightning pulse.")]
        public Light flashDirectionalLight;
        public float flashPeakIntensity = 5.8f;

        [Header("Pooled Geometry & Billboards")]
        [Tooltip("Pre-baked pool of 3-5 low-poly 3D lightning bolt meshes.")]
        public Renderer[] boltMeshPool;
        [Tooltip("Cloud-embedded Voronoi noise billboards for ambient sheet lightning.")]
        public Renderer[] sheetLightningQuads;

        [Header("Placement & Timing")]
        public Transform followCenter;
        public bool sheetLightningOnly = false;
        public float minStrikeInterval = 2.2f;
        public float maxStrikeInterval = 4.8f;
        public float boltDuration = 0.18f;
        public float flashDecayDuration = 0.36f;

        public float CurrentFlashNormalized { get; private set; }

        private static readonly int IdGlobalLightningFlash = Shader.PropertyToID("_GlobalLightningFlash");
        private static readonly int IdFlickerIntensity = Shader.PropertyToID("_FlickerIntensity");
        private static readonly int IdFlashAlpha = Shader.PropertyToID("_FlashAlpha");

        private MaterialPropertyBlock propBlock;
        private float nextStrikeTime;
        private float boltHideTime;
        private float flashDecayTimer;
        private int flashFrameStage;
        private int flashStageFramesLeft;

        void Awake()
        {
            Instance = this;
            InitializePool();
        }

        void OnEnable()
        {
            Instance = this;
            InitializePool();
            if (!Application.isPlaying)
            {
                ApplyEditModeState();
            }
            else
            {
                ScheduleNextStrike();
            }
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (flashDirectionalLight != null)
            {
                flashDirectionalLight.enabled = false;
            }
            CurrentFlashNormalized = 0f;
            Shader.SetGlobalFloat(IdGlobalLightningFlash, 0f);
        }

        private void InitializePool()
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            if (followCenter == null)
            {
                var jaeger = GameObject.Find("Jaeger_Mech");
                if (jaeger != null) followCenter = jaeger.transform;
            }
        }

        private void ApplyEditModeState()
        {
            if (flashDirectionalLight != null)
            {
                flashDirectionalLight.enabled = false;
            }

            if (boltMeshPool != null)
            {
                for (int i = 0; i < boltMeshPool.Length; i++)
                {
                    if (boltMeshPool[i] != null) boltMeshPool[i].enabled = false;
                }
            }

            if (sheetLightningQuads != null)
            {
                for (int i = 0; i < sheetLightningQuads.Length; i++)
                {
                    if (sheetLightningQuads[i] == null) continue;
                    sheetLightningQuads[i].enabled = false;
                    propBlock.Clear();
                    propBlock.SetFloat(IdFlickerIntensity, 0f);
                    sheetLightningQuads[i].SetPropertyBlock(propBlock);
                }
            }

            CurrentFlashNormalized = 0f;
            Shader.SetGlobalFloat(IdGlobalLightningFlash, 0f);
        }

        private void ScheduleNextStrike()
        {
            if (Random.value < 0.28f)
            {
                // Rapid second bolt / multi-pulse lightning burst
                nextStrikeTime = Time.time + Random.Range(0.22f, 0.65f);
            }
            else
            {
                float minT = Mathf.Max(0.5f, minStrikeInterval);
                float maxT = Mathf.Max(minT, maxStrikeInterval);
                nextStrikeTime = Time.time + Random.Range(minT, maxT);
            }
        }

        void Update()
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            if (!Application.isPlaying)
            {
                ApplyEditModeState();
                return;
            }

            if (followCenter == null)
            {
                var jaeger = GameObject.Find("Jaeger_Mech");
                if (jaeger != null) followCenter = jaeger.transform;
            }

            if (Time.time >= nextStrikeTime)
            {
                TriggerLightningStrike();
                ScheduleNextStrike();
            }

            UpdateFlashLightStateMachine();
            UpdateBolts();
            UpdateSheetLightning();

            if (flashDecayTimer > 0f)
            {
                flashDecayTimer = Mathf.Max(0f, flashDecayTimer - Time.deltaTime);
                CurrentFlashNormalized = Mathf.Clamp01(flashDecayTimer / Mathf.Max(0.05f, flashDecayDuration));
            }
            else
            {
                CurrentFlashNormalized = 0f;
            }

            Shader.SetGlobalFloat(IdGlobalLightningFlash, CurrentFlashNormalized);
        }

        /// <summary>
        /// Triggers an immediate thunderstorm lightning strike, pooled 3D bolts, and 1-2 frame GI flash.
        /// </summary>
        public void TriggerLightningStrike()
        {
            Vector3 center = followCenter != null ? followCenter.position : Vector3.zero;
            float baseAngle = Random.Range(0f, 360f);

            if (!sheetLightningOnly && boltMeshPool != null && boltMeshPool.Length > 0)
            {
                for (int i = 0; i < boltMeshPool.Length; i++)
                {
                    if (boltMeshPool[i] != null) boltMeshPool[i].enabled = false;
                }

                int boltCount = (boltMeshPool.Length > 1 && Random.value > 0.45f) ? 2 : 1;
                int firstIdx = Random.Range(0, boltMeshPool.Length);

                float closestDist = float.MaxValue;
                for (int b = 0; b < boltCount; b++)
                {
                    int idx = (firstIdx + b) % boltMeshPool.Length;
                    Renderer bolt = boltMeshPool[idx];
                    if (bolt == null) continue;

                    float angle = baseAngle + b * Random.Range(18f, 42f);
                    // Frequent close-range lightning hits (24m - 65m) and dramatic mid strikes (65m - 130m)
                    bool isClose = Random.value > 0.45f;
                    float dist = isClose ? Random.Range(24f, 65f) : Random.Range(65f, 130f);
                    closestDist = Mathf.Min(closestDist, dist);
                    Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist;
                    Vector3 strikePos = new Vector3(center.x + offset.x, 0f, center.z + offset.z);

                    float waterY = 0f;
                    if (WaterSystem.Instance != null)
                    {
                        waterY = WaterSystem.Instance.GetWaterHeight(strikePos);
                    }
                    strikePos.y = waterY;

                    bolt.transform.position = strikePos;
                    bolt.transform.rotation = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0f, 360f), Random.Range(-5f, 5f));
                    float scaleXZ = Random.Range(0.95f, 1.45f);
                    bolt.transform.localScale = new Vector3(scaleXZ, Random.Range(1.05f, 1.40f), scaleXZ);
                    bolt.enabled = true;

                    // Close strikes trigger ocean splashes and radial waves
                    if (dist < 75f)
                    {
                        if (WaterSplashManager.Instance != null && !WaterSplashManager.Instance.isDryLandMode)
                        {
                            WaterSplashManager.Instance.SpawnFootstepSplash(strikePos, 2.2f);
                            WaterSplashManager.Instance.SpawnHeavyMoveWaterKickup(strikePos, Vector3.up, 2.2f, true);
                        }
                        if (WaterSystem.Instance != null)
                        {
                            WaterSystem.Instance.AddFootstepWave(strikePos, 32f, 0.45f);
                        }
                    }
                }

                // Trigger a single consolidated camera rumble & audio muffle per lightning strike event
                if (closestDist < 65f)
                {
                    if (PacificRimAtmosphere.HeavyImpactCameraTraumaController.Instance != null)
                    {
                        PacificRimAtmosphere.HeavyImpactCameraTraumaController.Instance.OnLightningStruck(center, closestDist);
                    }
                    else
                    {
                        Camera cam = Camera.main;
                        if (cam != null)
                        {
                            var shaker = cam.GetComponent<PacificRimCameraShake>();
                            if (shaker != null) shaker.Shake(0.45f * (1f - closestDist / 75f), 0.38f);
                        }
                    }

                    if (PacificRimAtmosphere.AtmosphereAudioController.Instance != null)
                    {
                        PacificRimAtmosphere.AtmosphereAudioController.Instance.TriggerThunderMuffle(0.92f, 0.85f);
                    }

                    if (PacificRimAudio.PacificRimAudioManager.Instance != null)
                    {
                        // Nature Ambience (Thunderstorm): lowest tier in volume hierarchy
                        PacificRimAudio.PacificRimAudioManager.Instance.PlayLightningStrike(center, 0.10f);
                    }
                }

                boltHideTime = Time.time + Mathf.Max(0.05f, boltDuration);
            }

            // Aim GI flash directional light roughly from strike azimuth downward
            if (flashDirectionalLight != null)
            {
                flashDirectionalLight.transform.rotation = Quaternion.Euler(Random.Range(32f, 52f), baseAngle + 180f, 0f);
                flashDirectionalLight.intensity = flashPeakIntensity;
                flashDirectionalLight.enabled = true;
            }

            // Stage 1: 2 frames ON -> Stage 2: 1 frame OFF -> Stage 3: 1 frame ON restrike -> OFF
            flashFrameStage = 1;
            flashStageFramesLeft = 2;
            flashDecayTimer = Mathf.Max(0.05f, flashDecayDuration);
            CurrentFlashNormalized = 1f;
            OnLightningStrike?.Invoke();
        }

        private void UpdateFlashLightStateMachine()
        {
            if (flashDirectionalLight == null) return;

            switch (flashFrameStage)
            {
                case 1:
                    flashDirectionalLight.enabled = true;
                    flashDirectionalLight.intensity = flashPeakIntensity;
                    flashStageFramesLeft--;
                    if (flashStageFramesLeft <= 0)
                    {
                        flashFrameStage = 2;
                        flashStageFramesLeft = 1;
                    }
                    break;

                case 2:
                    flashDirectionalLight.enabled = false;
                    flashStageFramesLeft--;
                    if (flashStageFramesLeft <= 0)
                    {
                        flashFrameStage = 3;
                        flashStageFramesLeft = 1;
                        flashDecayTimer = Mathf.Max(flashDecayTimer, flashDecayDuration * 0.6f);
                    }
                    break;

                case 3:
                    flashDirectionalLight.enabled = true;
                    flashDirectionalLight.intensity = flashPeakIntensity * 0.85f;
                    flashStageFramesLeft--;
                    if (flashStageFramesLeft <= 0)
                    {
                        flashFrameStage = 0;
                        flashDirectionalLight.enabled = false;
                    }
                    break;

                default:
                    if (flashDirectionalLight.enabled)
                    {
                        flashDirectionalLight.enabled = false;
                    }
                    break;
            }
        }

        private void UpdateBolts()
        {
            if (boltMeshPool == null) return;

            bool active = Time.time < boltHideTime;
            float remainNorm = active
                ? Mathf.Clamp01((boltHideTime - Time.time) / Mathf.Max(0.05f, boltDuration))
                : 0f;

            for (int i = 0; i < boltMeshPool.Length; i++)
            {
                Renderer bolt = boltMeshPool[i];
                if (bolt == null) continue;

                if (!active)
                {
                    if (bolt.enabled) bolt.enabled = false;
                }
                else if (bolt.enabled)
                {
                    propBlock.Clear();
                    propBlock.SetFloat(IdFlashAlpha, remainNorm);
                    bolt.SetPropertyBlock(propBlock);
                }
            }
        }

        private void UpdateSheetLightning()
        {
            if (sheetLightningQuads == null) return;

            float t = Time.time;
            for (int i = 0; i < sheetLightningQuads.Length; i++)
            {
                Renderer quad = sheetLightningQuads[i];
                if (quad == null) continue;

                float p1 = Mathf.PerlinNoise(t * 2.1f + i * 19.3f, 0.7f);
                float p2 = Mathf.PerlinNoise(t * 14.5f + i * 7.1f, 3.1f);
                float burst = p1 > 0.72f ? Mathf.Pow(Mathf.InverseLerp(0.72f, 1.0f, p1), 1.4f) * (0.4f + p2 * 1.4f) : 0f;
                float flicker = Mathf.Clamp(burst + CurrentFlashNormalized * 0.75f, 0f, 2.5f);

                bool visible = flicker > 0.001f;
                if (quad.enabled != visible) quad.enabled = visible;
                if (visible)
                {
                    propBlock.Clear();
                    propBlock.SetFloat(IdFlickerIntensity, flicker);
                    quad.SetPropertyBlock(propBlock);
                }
            }
        }

        /// <summary>
        /// Procedurally builds a forked low-poly 3D crossed-ribbon lightning bolt mesh (~56 vertices).
        /// uv.y spans [0..1] vertically for animated vertex jitter in shader.
        /// </summary>
        public static Mesh BuildLowPolyLightningBoltMesh(int seed, int segments = 14)
        {
            System.Random rng = new System.Random(seed);
            segments = Mathf.Clamp(segments, 6, 20);

            List<Vector3> verts = new List<Vector3>(64);
            List<Vector2> uvs = new List<Vector2>(64);
            List<int> tris = new List<int>(180);

            Vector3[] spine = new Vector3[segments + 1];
            float height = 64f;
            float offsetX = 0f;
            float offsetZ = 0f;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float y = t * height;
                if (i > 0 && i < segments)
                {
                    offsetX += (float)(rng.NextDouble() * 2.0 - 1.0) * 3.8f;
                    offsetZ += (float)(rng.NextDouble() * 2.0 - 1.0) * 3.8f;
                }
                spine[i] = new Vector3(offsetX, y, offsetZ);
            }

            void AddRibbon(Vector3[] points, float baseHalfWidth, float startUvY, float endUvY, Vector3 sideAxis)
            {
                int startVert = verts.Count;
                for (int i = 0; i < points.Length; i++)
                {
                    float frac = (float)i / (points.Length - 1);
                    float w = baseHalfWidth * Mathf.Lerp(0.75f, 1.25f, frac);
                    float uvy = Mathf.Lerp(startUvY, endUvY, frac);

                    verts.Add(points[i] - sideAxis * w);
                    verts.Add(points[i] + sideAxis * w);
                    uvs.Add(new Vector2(0f, uvy));
                    uvs.Add(new Vector2(1f, uvy));
                }

                for (int i = 0; i < points.Length - 1; i++)
                {
                    int r0 = startVert + i * 2;
                    int r1 = r0 + 2;

                    tris.Add(r0);
                    tris.Add(r1);
                    tris.Add(r0 + 1);

                    tris.Add(r0 + 1);
                    tris.Add(r1);
                    tris.Add(r1 + 1);
                }
            }

            // Two crossed low-poly ribbons so the trunk has volumetric readability from every angle
            AddRibbon(spine, 1.05f, 0f, 1f, Vector3.right);
            AddRibbon(spine, 1.05f, 0f, 1f, Vector3.forward);

            // Small low-poly fork branch near upper third
            int forkIdx = Mathf.Clamp((segments * 2) / 3, 2, segments - 2);
            int forkSegs = 4;
            Vector3[] fork = new Vector3[forkSegs + 1];
            Vector3 fp = spine[forkIdx];
            Vector3 forkDir = new Vector3((float)(rng.NextDouble() * 2.0 - 1.0), -1.1f, (float)(rng.NextDouble() * 2.0 - 1.0)).normalized;
            for (int i = 0; i <= forkSegs; i++)
            {
                float step = i * 4.5f;
                Vector3 jitter = i > 0
                    ? new Vector3((float)(rng.NextDouble() - 0.5) * 1.6f, 0f, (float)(rng.NextDouble() - 0.5) * 1.6f)
                    : Vector3.zero;
                fork[i] = fp + forkDir * step + jitter;
            }
            AddRibbon(fork, 0.55f, (float)forkIdx / segments, Mathf.Clamp01((float)(forkIdx - 2) / segments), Vector3.right);

            Mesh mesh = new Mesh();
            mesh.name = $"PacificRim_LightningBolt_{seed}";
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
