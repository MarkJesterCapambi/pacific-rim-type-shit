using System.Collections.Generic;
using UnityEngine;

namespace PacificRimEnvironment
{
    /// <summary>
    /// Manages the overhead combat escort squadron:
    /// - 6 heavy-lift CH-47 Chinooks orbiting above the ocean battlefield
    /// - 3 Chinooks actively track Gipsy Danger, 3 Chinooks actively track Leatherback Kaiju
    /// - Every 5 seconds, performs a tactical rotation swap: one Chinook orbiting Gipsy transitions over
    ///   to Leatherback, while another Chinook transitions from Leatherback over to Gipsy
    /// - Each Chinook is equipped with tandem counter-rotating rotors, flashing strobes, and a
    ///   volumetric searchlight beam that haphazardly illuminates the storm-swept waters and combatants
    /// - All searchlights and volumetric beams dramatically flare when lightning strikes
    /// </summary>
    [ExecuteAlways]
    public class ChinookCombatPatrolSquadron : MonoBehaviour
    {
        public static ChinookCombatPatrolSquadron Instance { get; private set; }

        [Header("Combatants")]
        public Transform gipsyTarget;
        public Transform kaijuTarget;

        [Header("Squadron Roster")]
        public List<ChinookCombatPatrolAgent> gipsySquadron = new List<ChinookCombatPatrolAgent>();
        public List<ChinookCombatPatrolAgent> kaijuSquadron = new List<ChinookCombatPatrolAgent>();

        [Header("Tactical Rotation Timing")]
        [Tooltip("Interval in seconds between target rotation swaps.")]
        public float rotationSwapInterval = 5.0f;
        public bool enableAutomaticRotation = true;

        [Header("Urban Canyon Flight")]
        public bool urbanCanyonMode = false;
        public float urbanMinClearanceY = 72f;

        [Header("Squadron Assets & Materials")]
        public GameObject chinookPrefab;
        public Material volumetricBeamMaterial;
        public Mesh volumetricConeMesh;
        public AudioClip helicopterAudioClip;

        private float swapTimer;
        private int gipsySwapPointer = 0;
        private int kaijuSwapPointer = 0;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void Start()
        {
            ResolveTargets();
            EnsureSquadronSpawned();
            swapTimer = rotationSwapInterval;
        }

        public void ResolveTargets()
        {
            if (gipsyTarget == null)
            {
                var jaeger = GameObject.Find("Jaeger_Mech");
                if (jaeger != null) gipsyTarget = jaeger.transform;
            }

            if (kaijuTarget == null)
            {
                var kaiju = GameObject.Find("Kaiju_Leatherback")
                            ?? GameObject.Find("Leatherback")
                            ?? GameObject.Find("Kaiju_Otachi")
                            ?? (PacificRimWater.KaijuAI.Instance != null ? PacificRimWater.KaijuAI.Instance.gameObject : null)
                            ?? FindAnyObjectByType<PacificRimWater.KaijuAI>()?.gameObject;
                if (kaiju != null) kaijuTarget = kaiju.transform;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            ResolveTargets();

            if (!enableAutomaticRotation) return;

            swapTimer -= Time.deltaTime;
            if (swapTimer <= 0f)
            {
                swapTimer = rotationSwapInterval;
                ExecuteTacticalRotationSwap();
            }
        }

        /// <summary>
        /// Executes a synchronized tactical target swap every 5 seconds:
        /// - 1 Chinook swirling around Gipsy departs for Leatherback
        /// - 1 Chinook swirling around Leatherback departs for Gipsy
        /// </summary>
        public void ExecuteTacticalRotationSwap()
        {
            if (gipsyTarget == null || kaijuTarget == null) return;
            CleanSquadronLists();

            if (gipsySquadron.Count == 0 || kaijuSquadron.Count == 0) return;

            // Pick eligible non-transitioning agent from Gipsy squadron
            ChinookCombatPatrolAgent gipsyCandidate = null;
            for (int i = 0; i < gipsySquadron.Count; i++)
            {
                int idx = (gipsySwapPointer + i) % gipsySquadron.Count;
                if (!gipsySquadron[idx].isTransitioning)
                {
                    gipsyCandidate = gipsySquadron[idx];
                    gipsySwapPointer = (idx + 1) % gipsySquadron.Count;
                    break;
                }
            }

            // Pick eligible non-transitioning agent from Kaiju squadron
            ChinookCombatPatrolAgent kaijuCandidate = null;
            for (int i = 0; i < kaijuSquadron.Count; i++)
            {
                int idx = (kaijuSwapPointer + i) % kaijuSquadron.Count;
                if (!kaijuSquadron[idx].isTransitioning)
                {
                    kaijuCandidate = kaijuSquadron[idx];
                    kaijuSwapPointer = (idx + 1) % kaijuSquadron.Count;
                    break;
                }
            }

            if (gipsyCandidate != null && kaijuCandidate != null)
            {
                // Reassign in lists
                gipsySquadron.Remove(gipsyCandidate);
                kaijuSquadron.Add(gipsyCandidate);

                kaijuSquadron.Remove(kaijuCandidate);
                gipsySquadron.Add(kaijuCandidate);

                // Command Gipsy Chinook to fly over and watch Kaiju
                float newRadiusForKaiju = urbanCanyonMode ? Random.Range(24f, 38f) : Random.Range(32f, 48f);
                float newAltitudeForKaiju = urbanCanyonMode ? Random.Range(74f, 94f) : Random.Range(40f, 52f);
                gipsyCandidate.BeginTransitionTo(kaijuTarget, ChinookTargetType.Leatherback, newRadiusForKaiju, newAltitudeForKaiju);

                // Command Kaiju Chinook to fly over and watch Gipsy
                float newRadiusForGipsy = urbanCanyonMode ? Random.Range(24f, 38f) : Random.Range(28f, 44f);
                float newAltitudeForGipsy = urbanCanyonMode ? Random.Range(72f, 92f) : Random.Range(38f, 50f);
                kaijuCandidate.BeginTransitionTo(gipsyTarget, ChinookTargetType.Gipsy, newRadiusForGipsy, newAltitudeForGipsy);
            }
        }

        private void CleanSquadronLists()
        {
            gipsySquadron.RemoveAll(a => a == null);
            kaijuSquadron.RemoveAll(a => a == null);
        }

        /// <summary>
        /// Builds or locates the 6-Chinook squadron in the scene.
        /// </summary>
        public void EnsureSquadronSpawned()
        {
            CleanSquadronLists();

            if (gipsySquadron.Count >= 3 && kaijuSquadron.Count >= 3)
            {
                for (int i = 0; i < gipsySquadron.Count; i++) ApplyModeToAgent(gipsySquadron[i]);
                for (int i = 0; i < kaijuSquadron.Count; i++) ApplyModeToAgent(kaijuSquadron[i]);
                return;
            }

            // Look for existing children
            var existingAgents = GetComponentsInChildren<ChinookCombatPatrolAgent>(true);
            foreach (var a in existingAgents)
            {
                ApplyModeToAgent(a);
                if (a.targetType == ChinookTargetType.Gipsy && !gipsySquadron.Contains(a))
                    gipsySquadron.Add(a);
                else if (a.targetType == ChinookTargetType.Leatherback && !kaijuSquadron.Contains(a))
                    kaijuSquadron.Add(a);
            }

            if (gipsySquadron.Count >= 3 && kaijuSquadron.Count >= 3)
            {
                for (int i = 0; i < gipsySquadron.Count; i++) ApplyModeToAgent(gipsySquadron[i]);
                for (int i = 0; i < kaijuSquadron.Count; i++) ApplyModeToAgent(kaijuSquadron[i]);
                return;
            }

            LoadAssetsIfMissing();
            ResolveTargets();

            // Spawn Gipsy patrol (3 Chinooks)
            float[] gipsyRadii = urbanCanyonMode ? new float[] { 24f, 31f, 38f } : new float[] { 28f, 38f, 48f };
            float[] gipsyAltitudes = urbanCanyonMode ? new float[] { 72f, 80f, 90f } : new float[] { 40f, 45f, 50f };
            float[] gipsySpeeds = new float[] { 0.44f, 0.38f, 0.48f };

            while (gipsySquadron.Count < 3)
            {
                int i = gipsySquadron.Count;
                float angle = i * (Mathf.PI * 2f / 3f);
                var agent = SpawnPatrolChinook($"Chinook_Patrol_Gipsy_{i + 1}", gipsyTarget, ChinookTargetType.Gipsy, gipsyRadii[i], gipsyAltitudes[i], gipsySpeeds[i], angle, 1f);
                gipsySquadron.Add(agent);
            }

            // Spawn Kaiju patrol (3 Chinooks)
            float[] kaijuRadii = urbanCanyonMode ? new float[] { 25f, 32f, 38f } : new float[] { 32f, 42f, 52f };
            float[] kaijuAltitudes = urbanCanyonMode ? new float[] { 75f, 84f, 94f } : new float[] { 42f, 48f, 54f };
            float[] kaijuSpeeds = new float[] { 0.42f, 0.36f, 0.46f };

            while (kaijuSquadron.Count < 3)
            {
                int i = kaijuSquadron.Count;
                float angle = i * (Mathf.PI * 2f / 3f) + 0.8f;
                var agent = SpawnPatrolChinook($"Chinook_Patrol_Kaiju_{i + 1}", kaijuTarget, ChinookTargetType.Leatherback, kaijuRadii[i], kaijuAltitudes[i], kaijuSpeeds[i], angle, -1f);
                kaijuSquadron.Add(agent);
            }
        }

        private void LoadAssetsIfMissing()
        {
#if UNITY_EDITOR
            if (chinookPrefab == null)
            {
                chinookPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PacificRim_Chinook.prefab");
            }
            if (volumetricBeamMaterial == null)
            {
                volumetricBeamMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_ChinookVolumetricBeam.mat");
            }
            if (volumetricConeMesh == null)
            {
                volumetricConeMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/PacificRim_VolumetricBeamCone.asset");
            }
            if (helicopterAudioClip == null)
            {
                helicopterAudioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/HelicopterLoop.mp3");
            }
#endif
        }

        private ChinookCombatPatrolAgent SpawnPatrolChinook(string chinookName, Transform target, ChinookTargetType type, float radius, float altitude, float speed, float angle, float direction)
        {
            GameObject go = null;
            if (chinookPrefab != null)
            {
                go = Instantiate(chinookPrefab, transform);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform);
                go.transform.localScale = new Vector3(3.5f, 3.5f, 16f);
            }

            go.name = chinookName;

            // Ensure ChinookRotorController is present
            var rotorCtrl = go.GetComponent<PacificRimCutscene.ChinookRotorController>();
            if (rotorCtrl == null)
            {
                rotorCtrl = go.AddComponent<PacificRimCutscene.ChinookRotorController>();
                rotorCtrl.ResolveReferences();
            }

            // Ensure Searchlight and Volumetric Light Beam
            SetupChinookSearchlightAndBeam(go);

            // Ensure 3D Spatial Audio
            SetupChinookAudio(go);

            // Add Patrol Agent
            var agent = go.GetComponent<ChinookCombatPatrolAgent>();
            if (agent == null)
            {
                agent = go.AddComponent<ChinookCombatPatrolAgent>();
            }

            ApplyModeToAgent(agent);

            agent.Initialize(target, type, radius, altitude, speed, angle, direction);
            return agent;
        }

        private void ApplyModeToAgent(ChinookCombatPatrolAgent agent)
        {
            if (agent == null) return;
            agent.avoidUrbanBuildings = urbanCanyonMode;
            agent.minUrbanClearanceY = urbanMinClearanceY;
            if (urbanCanyonMode)
            {
                agent.baseLightIntensity = 1450f;
                agent.lightningPeakIntensity = 3600f;
                agent.baseLightRange = 165f;
                agent.lightningPeakRange = 230f;
            }
            else
            {
                agent.baseLightIntensity = 950f;
                agent.lightningPeakIntensity = 2800f;
                agent.baseLightRange = 110f;
                agent.lightningPeakRange = 165f;
            }
        }

        private void SetupChinookSearchlightAndBeam(GameObject go)
        {
            Transform lightNode = go.transform.Find("ChinookSearchlight");
            if (lightNode == null)
            {
                var lightGo = new GameObject("ChinookSearchlight");
                lightGo.transform.SetParent(go.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, -0.5f, -2.4f);
                lightGo.transform.localRotation = Quaternion.Euler(70f, 180f, 0f);
                lightNode = lightGo.transform;
            }

            var spot = lightNode.GetComponent<Light>();
            if (spot == null) spot = lightNode.gameObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(0.88f, 0.94f, 1.0f);
            spot.range = urbanCanyonMode ? 165f : 110f;
            spot.spotAngle = 46f;
            spot.innerSpotAngle = 26f;
            spot.intensity = urbanCanyonMode ? 1450f : 950f;
            spot.shadows = LightShadows.None;

            // Child Volumetric Light Beam Cone
            Transform beamChild = lightNode.Find("VolumetricBeam");
            if (beamChild == null)
            {
                var bgo = new GameObject("VolumetricBeam");
                bgo.transform.SetParent(lightNode, false);
                beamChild = bgo.transform;
            }

            beamChild.localPosition = Vector3.zero;
            beamChild.localRotation = Quaternion.identity;
            beamChild.localScale = new Vector3(0.9f, 0.9f, 1.15f);

            var mf = beamChild.GetComponent<MeshFilter>();
            if (mf == null) mf = beamChild.gameObject.AddComponent<MeshFilter>();
            if (volumetricConeMesh != null) mf.sharedMesh = volumetricConeMesh;

            var mr = beamChild.GetComponent<MeshRenderer>();
            if (mr == null) mr = beamChild.gameObject.AddComponent<MeshRenderer>();
            if (volumetricBeamMaterial != null) mr.sharedMaterial = volumetricBeamMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private void SetupChinookAudio(GameObject go)
        {
            var audio = go.GetComponent<AudioSource>();
            if (audio == null) audio = go.AddComponent<AudioSource>();
            if (helicopterAudioClip != null) audio.clip = helicopterAudioClip;
            audio.loop = true;
            audio.playOnAwake = true;
            audio.spatialBlend = 0.85f; // 3D spatialized
            audio.minDistance = 20f;
            audio.maxDistance = 220f;
            float sfxVol = PacificRimAudio.PacificRimAudioManager.Instance != null ? PacificRimAudio.PacificRimAudioManager.Instance.SfxVolume : 0.55f;
            audio.volume = 0.16f * sfxVol; // Artificial Ambience: below SFX/Music, above Nature Ambience
            audio.pitch = Random.Range(0.96f, 1.04f); // subtle individual variance
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            if (Application.isPlaying && !audio.isPlaying)
            {
                audio.Play();
            }
        }
    }
}
