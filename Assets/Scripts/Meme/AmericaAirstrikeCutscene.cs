using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using PacificRimAtmosphere;
using PacificRimAudio;
using PacificRimUI;
using PacificRimWater;

namespace PacificRimMeme
{
    /// <summary>
    /// Orchestrates the ~12.6-second "america" F-22 Raptor Squadron airstrike meme-cutscene:
    /// spawns 10 F-22 fighters with locked cinematic cameras, red/white/blue wingtip smoke trails,
    /// missile & JDAM barrages, 20mm Vulcan tracer fire, massive explosions, and kills the Kaiju on completion.
    /// </summary>
    [DefaultExecutionOrder(65)]
    public class AmericaAirstrikeCutscene : MonoBehaviour
    {
        public static bool IsRunning { get; private set; }

        private const int JetCount = 10;
        private const float TotalDuration = 12.6f;
        private const float Beat1End = 2.8f;
        private const float Beat2End = 6.2f;
        private const float Beat3End = 10.2f;
        private const float FinaleImpactTime = 11.72f;

        private class JetUnit
        {
            public int index;
            public GameObject root;
            public Transform transform;
            public Transform hardpointL;
            public Transform hardpointR;
            public Transform gunMuzzle;
            public Transform[] burnerCones;
            public Light engineLight;
            public Vector3 prevPos;
            public Vector3 velocity;
            public float smoothedBankDeg;
            public int nextHardpointToggle;
            public float flareCooldown;
        }

        private class ActiveProjectile
        {
            public GameObject go;
            public Transform transform;
            public Vector3 position;
            public Vector3 velocity;
            public Vector3 targetOffset;
            public float age;
            public float maxLife;
            public float impactRadius;
            public bool isHeavyBomb;
            public bool isTracer;
            public float turnRate;
        }

        private class TransientEffect
        {
            public GameObject go;
            public Transform transform;
            public Material mat;
            public Light light;
            public float age;
            public float duration;
            public float startScale;
            public float endScale;
            public float startLightIntensity;
            public Color baseColor;
            public bool isRing;
        }

        private readonly List<JetUnit> jets = new List<JetUnit>(JetCount);
        private readonly List<ActiveProjectile> projectiles = new List<ActiveProjectile>(64);
        private readonly List<TransientEffect> effects = new List<TransientEffect>(48);
        private readonly List<AudioSource> sfxPool = new List<AudioSource>(16);

        private KaijuAI targetKaiju;
        private SimpleJaegerController jaeger;
        private Camera mainCamera;
        private JaegerCameraFollow cameraFollow;
        private PacificRimCameraShake cameraShake;
        private AudioLowPassFilter cameraLowPass;
        private WaterSystem waterSystem;
        private WaterSplashManager splashManager;
        private GameObject f22Prefab;

        // Saved scene state restored cleanly on completion or destroy
        private bool savedStateCaptured;
        private bool savedJaegerCutscene;
        private bool savedJaegerGodMode;
        private bool savedCamFollowEnabled;
        private float savedCameraFov;
        private bool savedKaijuAiDisabled;
        private float savedFogDensity;
        private bool modifiedFogDensity;
        private float savedLowPassCutoff;
        private bool modifiedLowPass;

        // Combat progression tracking
        private float kaijuStartHealth;
        private float currentTime;
        private bool finaleDetonated;
        private bool cleanedUp;
        private Vector3 kaijuAnchorPos;
        private Vector3 attackDir;
        private Vector3 rightDir;
        private float combatFloorY;

        // Camera state
        private Vector3 camSmoothedPos;
        private Quaternion camSmoothedRot = Quaternion.identity;
        private float camCurrentFov = 56f;
        private bool camInitialized;
        private int activeBeat = -1;

        // Weapon timers
        private float nextMissileWaveTime;
        private float nextCannonBurstTime;
        private float nextBombDropTime;
        private float nextStaggerTime;
        private int missileWaveIndex;
        private AudioSource jetEngineLoopSource;

        // Procedural shared assets for cutscene VFX
        private Texture2D softGlowTex;
        private Texture2D ribbonTrailTex;
        private Material fireballAdditiveMat;
        private Material smokeAlphaMat;
        private Material sparkAdditiveMat;
        private Material shockwaveRingMat;
        private Material missileBodyMat;
        private Material missileTrailMat;
        private Material wingtipTrailMat;
        private Material tracerMat;
        private Material burnerOuterMat;
        private Material burnerInnerMat;
        private Mesh coneMesh;
        private Mesh ringMesh;

        // Letterbox & tactical meme HUD overlay
        private Canvas overlayCanvas;
        private RectTransform topLetterboxBar;
        private RectTransform bottomLetterboxBar;
        private CanvasGroup overlayGroup;
        private Text radioCallsignText;
        private Text beatCaptionText;
        private Image screenFlashImage;
        private float screenFlashAlpha;

        public void BeginAirstrike(KaijuAI kaiju, GameObject jetPrefab)
        {
            if (IsRunning || kaiju == null)
            {
                Destroy(gameObject);
                return;
            }

            IsRunning = true;
            targetKaiju = kaiju;
            f22Prefab = jetPrefab;

            ResolveSceneReferences();
            CaptureAndLockState();
            BuildProceduralVfxResources();
            BuildOverlayUI();
            BuildAudioPool();
            ComputeStageFrame();
            SpawnSquadron();

            StartCoroutine(RunAirstrikeRoutine());
        }

        private void OnDestroy()
        {
            CleanupAndRestore(triggerKill: false);
            IsRunning = false;
        }

        private void ResolveSceneReferences()
        {
            jaeger = SimpleJaegerController.Instance ?? FindAnyObjectByType<SimpleJaegerController>();
            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraFollow = mainCamera.GetComponent<JaegerCameraFollow>();
                cameraShake = mainCamera.GetComponent<PacificRimCameraShake>() ?? mainCamera.gameObject.AddComponent<PacificRimCameraShake>();
                cameraLowPass = mainCamera.GetComponent<AudioLowPassFilter>();
            }
            waterSystem = WaterSystem.Instance ?? FindAnyObjectByType<WaterSystem>();
            splashManager = WaterSplashManager.Instance ?? FindAnyObjectByType<WaterSplashManager>();
        }

        private void CaptureAndLockState()
        {
            savedStateCaptured = true;

            if (jaeger != null)
            {
                savedJaegerCutscene = jaeger.isCutsceneActive;
                savedJaegerGodMode = jaeger.IsGodModeActive;
                jaeger.isCutsceneActive = true;
                jaeger.isBlocking = false;
                jaeger.SetGodMode(true);
                if (jaeger.animator != null)
                {
                    jaeger.animator.SetFloat("Speed", 0f);
                    jaeger.animator.SetBool("IsRunning", false);
                    jaeger.animator.SetBool("IsBlocking", false);
                }
                var jRb = jaeger.GetComponent<Rigidbody>();
                if (jRb != null)
                {
#if UNITY_6000_0_OR_NEWER
                    jRb.linearVelocity = Vector3.zero;
#else
                    jRb.velocity = Vector3.zero;
#endif
                }
            }

            if (cameraFollow != null)
            {
                savedCamFollowEnabled = cameraFollow.enabled;
                cameraFollow.enabled = false;
            }

            if (mainCamera != null)
            {
                savedCameraFov = mainCamera.fieldOfView;
            }

            if (targetKaiju != null)
            {
                savedKaijuAiDisabled = targetKaiju.IsAIDisabled;
                kaijuStartHealth = Mathf.Max(1f, targetKaiju.currentHealth);
                targetKaiju.SetAIDisabled(true);
            }

            // Ensure deep underwater fog doesn't obscure the jets in Operation Pitfall
            savedFogDensity = RenderSettings.fogDensity;
            if (RenderSettings.fog && savedFogDensity > 0.0055f)
            {
                RenderSettings.fogDensity = 0.0048f;
                modifiedFogDensity = true;
            }

            // Keep explosions and jet roar punchy even underwater
            if (cameraLowPass != null && cameraLowPass.enabled)
            {
                savedLowPassCutoff = cameraLowPass.cutoffFrequency;
                cameraLowPass.cutoffFrequency = Mathf.Max(savedLowPassCutoff, 11000f);
                modifiedLowPass = true;
            }

            JaegerHUD.Instance?.SetCutsceneHidden(true);
        }

        private void ComputeStageFrame()
        {
            kaijuAnchorPos = targetKaiju != null ? targetKaiju.transform.position : Vector3.zero;
            float terrainFloor = kaijuAnchorPos.y;
            float waterSurface = waterSystem != null ? waterSystem.MeanSurfaceY : terrainFloor;
            combatFloorY = Mathf.Max(terrainFloor, waterSurface);

            if (jaeger != null)
            {
                Vector3 delta = kaijuAnchorPos - jaeger.transform.position;
                delta.y = 0f;
                attackDir = delta.sqrMagnitude > 1f ? delta.normalized : jaeger.transform.forward;
            }
            else
            {
                attackDir = targetKaiju != null ? -targetKaiju.transform.forward : Vector3.forward;
            }
            attackDir.y = 0f;
            if (attackDir.sqrMagnitude < 0.01f) attackDir = Vector3.forward;
            attackDir.Normalize();

            rightDir = Vector3.Cross(Vector3.up, attackDir).normalized;
        }

        private Vector3 GetKaijuCenter()
        {
            if (targetKaiju != null)
            {
                kaijuAnchorPos = targetKaiju.transform.position;
            }
            return kaijuAnchorPos + Vector3.up * 13.5f;
        }

        private void SpawnSquadron()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            for (int i = 0; i < JetCount; i++)
            {
                GameObject jetGo = f22Prefab != null
                    ? Instantiate(f22Prefab, transform)
                    : CreateFallbackJetMesh(urpLit);

                jetGo.name = $"F22_Freedom_{i + 1:00}";

                var unit = new JetUnit
                {
                    index = i,
                    root = jetGo,
                    transform = jetGo.transform,
                    hardpointL = jetGo.transform.Find("Hardpoint_L"),
                    hardpointR = jetGo.transform.Find("Hardpoint_R"),
                    gunMuzzle = jetGo.transform.Find("GunMuzzle"),
                    flareCooldown = 0.4f + i * 0.17f
                };

                AttachJetVfx(unit, urpLit);

                EvaluateJetTrajectory(i, 0f, out Vector3 initPos, out Vector3 initFwd, out float initRoll);
                unit.transform.position = initPos;
                unit.transform.rotation = BuildJetRotation(initFwd, initRoll);
                unit.prevPos = initPos;
                unit.velocity = initFwd * 36f;
                unit.smoothedBankDeg = initRoll;

                jets.Add(unit);
            }
        }

        private static GameObject CreateFallbackJetMesh(Shader shader)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.localScale = new Vector3(5.5f, 1.4f, 8.2f);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = new Material(shader) { color = new Color(0.32f, 0.35f, 0.38f) };
            return go;
        }

        private void AttachJetVfx(JetUnit unit, Shader urpLit)
        {
            Transform exhL = unit.transform.Find("Exhaust_L");
            Transform exhR = unit.transform.Find("Exhaust_R");
            Vector3 posL = exhL != null ? exhL.localPosition : new Vector3(-0.42f, 0.03f, -3.54f);
            Vector3 posR = exhR != null ? exhR.localPosition : new Vector3(0.42f, 0.03f, -3.54f);

            // Twin afterburner flame cones (outer orange-gold sheath + inner bright cyan/white core)
            unit.burnerCones = new Transform[4];
            unit.burnerCones[0] = CreateBurnerCone(unit.transform, "AfterburnerOuter_L", posL, new Vector3(0.54f, 0.54f, 3.6f), burnerOuterMat);
            unit.burnerCones[1] = CreateBurnerCone(unit.transform, "AfterburnerOuter_R", posR, new Vector3(0.54f, 0.54f, 3.6f), burnerOuterMat);
            unit.burnerCones[2] = CreateBurnerCone(unit.transform, "AfterburnerCore_L", posL, new Vector3(0.28f, 0.28f, 2.3f), burnerInnerMat);
            unit.burnerCones[3] = CreateBurnerCone(unit.transform, "AfterburnerCore_R", posR, new Vector3(0.28f, 0.28f, 2.3f), burnerInnerMat);

            // Engine warm afterburner point light on lead/inner jets (keeps light count clean & fast)
            if (unit.index < 4)
            {
                var lightGo = new GameObject("AfterburnerLight");
                lightGo.transform.SetParent(unit.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.1f, -4.6f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1.0f, 0.58f, 0.18f);
                light.range = 24f;
                light.intensity = 14f;
                light.shadows = LightShadows.None;
                unit.engineLight = light;
            }

            // Red / White / Blue patriotic wingtip smoke trails across the 10-jet formation
            Color trailColor = GetPatrioticTrailColor(unit.index);
            Transform tipL = unit.transform.Find("WingTip_L");
            Transform tipR = unit.transform.Find("WingTip_R");
            AddWingtipTrail(tipL != null ? tipL : CreateAnchor(unit.transform, "TipL", new Vector3(-3.4f, -0.09f, -1.35f)), trailColor);
            AddWingtipTrail(tipR != null ? tipR : CreateAnchor(unit.transform, "TipR", new Vector3(3.4f, -0.09f, -1.35f)), trailColor);
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private Transform CreateBurnerCone(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = scale;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = coneMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        private static Color GetPatrioticTrailColor(int jetIndex)
        {
            switch (jetIndex % 3)
            {
                case 0: return new Color(1.0f, 0.18f, 0.16f, 0.72f); // Freedom Red
                case 1: return new Color(0.95f, 0.97f, 1.0f, 0.72f); // Crisp White
                default: return new Color(0.18f, 0.56f, 1.0f, 0.72f); // Liberty Blue
            }
        }

        private void AddWingtipTrail(Transform anchor, Color color)
        {
            var tr = anchor.gameObject.AddComponent<TrailRenderer>();
            tr.time = 1.65f;
            tr.minVertexDistance = 0.40f;
            tr.startWidth = 0.56f;
            tr.endWidth = 0.08f;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.sharedMaterial = wingtipTrailMat;

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.Lerp(color, Color.white, 0.25f), 0.5f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.55f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            tr.colorGradient = grad;
        }

        private IEnumerator RunAirstrikeRoutine()
        {
            PlaySound("Computer explosion 6 - Alarm", 0.55f, 1.15f);
            PlaySound("Spaceship passing by", 0.95f, 0.95f);
            StartJetEngineLoop();

            nextMissileWaveTime = 2.85f;
            nextCannonBurstTime = 3.10f;
            nextBombDropTime = 6.35f;
            nextStaggerTime = 3.6f;

            while (currentTime < TotalDuration)
            {
                float dt = Time.deltaTime;
                currentTime += dt;

                UpdateBeatState(currentTime);
                UpdateSquadronFlight(currentTime, dt);
                UpdateWeaponsSchedule(currentTime);
                UpdateProjectiles(dt);
                UpdateTransientEffects(dt);
                UpdateKaijuDamageAndReaction(currentTime);
                UpdateUI(dt);

                yield return null;
            }

            // Final fade out of letterbox and clean handoff before instant Kaiju death
            CleanupAndRestore(triggerKill: true);
            Destroy(gameObject);
        }

        private void UpdateBeatState(float t)
        {
            int beat = t < Beat1End ? 0 : (t < Beat2End ? 1 : (t < Beat3End ? 2 : 3));
            if (beat == activeBeat) return;

            activeBeat = beat;
            switch (beat)
            {
                case 0:
                    SetCaption("AIR SUPPORT INBOUND  ·  10x F-22 RAPTOR SQUADRON");
                    break;
                case 1:
                    SetCaption("WEAPONS FREE  ·  WHAT THE FUCK IS A KILOMETER?!");
                    PlaySound("Computer explosion 6 - Alarm", 0.50f, 1.25f);
                    break;
                case 2:
                    SetCaption("FULL CARPET BOMBARDMENT  ·  100% PURE DEMOCRACY");
                    PlaySound("Spaceship passing by", 0.90f, 1.10f);
                    break;
                case 3:
                    SetCaption("FINAL PAYLOAD INCOMING  ·  FREEDOM DELIVERED");
                    PlaySound("Spaceship passing by", 1.0f, 1.22f);
                    break;
            }
        }

        private void UpdateSquadronFlight(float t, float dt)
        {
            const float sampleEps = 0.04f;
            for (int i = 0; i < jets.Count; i++)
            {
                var jet = jets[i];
                EvaluateJetTrajectory(i, t, out Vector3 pos, out Vector3 fwd, out float choreoBank);
                EvaluateJetTrajectory(i, t + sampleEps, out Vector3 nextPos, out _, out _);

                Vector3 vel = (nextPos - pos) / sampleEps;
                if (vel.sqrMagnitude > 0.01f) fwd = vel.normalized;

                // Blend subtle aerodynamic turn-bank with explicit choreographed rolls
                jet.smoothedBankDeg = Mathf.LerpAngle(jet.smoothedBankDeg, choreoBank, Mathf.Clamp01(dt * 6.5f));
                jet.transform.position = pos;
                jet.transform.rotation = BuildJetRotation(fwd, jet.smoothedBankDeg);
                jet.velocity = vel;
                jet.prevPos = pos;

                // Animate high-frequency afterburner flicker
                float flicker = 0.88f + 0.24f * Mathf.PerlinNoise(t * 24f + i * 7.3f, i * 3.1f);
                float climbBoost = (t > Beat3End + 1.1f) ? 1.45f : 1.0f;
                for (int b = 0; b < jet.burnerCones.Length; b++)
                {
                    if (jet.burnerCones[b] == null) continue;
                    bool isCore = b >= 2;
                    float baseZ = isCore ? 2.3f : 3.6f;
                    float baseXY = isCore ? 0.28f : 0.54f;
                    jet.burnerCones[b].localScale = new Vector3(
                        baseXY * Mathf.Lerp(0.95f, 1.12f, flicker - 0.8f),
                        baseXY * Mathf.Lerp(0.95f, 1.12f, flicker - 0.8f),
                        baseZ * flicker * climbBoost);
                }

                if (jet.engineLight != null)
                {
                    jet.engineLight.intensity = 14f * flicker * climbBoost;
                }

                // Periodic countermeasure flare pops during combat maneuvers
                jet.flareCooldown -= dt;
                if (jet.flareCooldown <= 0f && t > 1.2f && t < 12.1f)
                {
                    jet.flareCooldown = (t > Beat3End) ? 0.28f : Random.Range(0.75f, 1.35f);
                    SpawnFlareBurst(pos - fwd * 2.5f - Vector3.up * 0.5f, vel * 0.35f + Random.insideUnitSphere * 7f - Vector3.up * 6f);
                }
            }
        }

        private void EvaluateJetTrajectory(int index, float t, out Vector3 pos, out Vector3 forward, out float bankDeg)
        {
            Vector3 center = GetKaijuCenter();
            Vector3 formationLocal = GetDeltaFormationOffset(index);

            if (t < Beat1End)
            {
                // Beat 1: Tight 10-jet Delta Echelon sweeping directly in toward Kaiju
                float u = Mathf.Clamp01(t / Beat1End);
                float eased = u; // steady high-speed approach
                Vector3 leadStart = center - attackDir * 112f + Vector3.up * (combatFloorY - center.y + 30f);
                Vector3 leadEnd = center - attackDir * 30f + Vector3.up * (combatFloorY - center.y + 24f);
                Vector3 leadPos = Vector3.Lerp(leadStart, leadEnd, eased);
                leadPos.y += Mathf.Sin(u * Mathf.PI) * 2.5f;

                pos = leadPos + rightDir * formationLocal.x + Vector3.up * formationLocal.y + attackDir * formationLocal.z;
                forward = (leadEnd - leadStart).normalized;
                // Dramatic synchronized wing-level roll on arrival
                float rollEnv = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(u / 0.55f));
                bankDeg = (index % 2 == 0 ? -36f : 36f) * rollEnv;
                return;
            }

            if (t < Beat2End)
            {
                // Beat 2: Diving pass directly over the Kaiju, fanning into split banking climbs
                float u = Mathf.Clamp01((t - Beat1End) / (Beat2End - Beat1End));
                float passDist = Mathf.Lerp(-30f, 68f, u);
                float passHeight = combatFloorY + 24f - Mathf.Sin(u * Mathf.PI) * 5.5f + u * 11f;

                // Fan outward laterally after passing over Kaiju (u > 0.45)
                float splitSign = (index % 2 == 0) ? -1f : 1f;
                float splitFactor = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.38f) / 0.62f));
                float lateralFan = splitSign * (14f + (index / 2) * 5.5f) * splitFactor;

                Vector3 basePos = kaijuAnchorPos + attackDir * passDist + rightDir * lateralFan;
                basePos.y = passHeight;

                Vector3 offset = rightDir * (formationLocal.x * (1f - 0.45f * splitFactor))
                               + Vector3.up * formationLocal.y
                               + attackDir * (formationLocal.z * 0.8f);
                pos = basePos + offset;

                forward = (attackDir + rightDir * (splitSign * 0.65f * splitFactor) + Vector3.up * (u > 0.5f ? 0.25f : -0.14f)).normalized;
                bankDeg = -splitSign * 44f * splitFactor;
                return;
            }

            if (t < Beat3End)
            {
                // Beat 3: Two counter-rotating 5-jet high-G banked orbital circles strafing & carpet-bombing Kaiju
                float u = Mathf.Clamp01((t - Beat2End) / (Beat3End - Beat2End));
                bool isLeftOrbit = (index % 2 == 0);
                int slot = index / 2; // 0..4
                float orbitDir = isLeftOrbit ? 1f : -1f;
                float radius = isLeftOrbit ? (34f + slot * 3.2f) : (39f + slot * 3.2f);
                float altitude = combatFloorY + (isLeftOrbit ? 25f : 31f) + slot * 2.2f + Mathf.Sin((u * 2f + slot * 0.4f) * Mathf.PI) * 3.0f;

                float baseAngle = (isLeftOrbit ? 0.35f : Mathf.PI - 0.35f) + slot * (Mathf.PI * 2f / 5f) * 0.42f;
                float sweepAngle = baseAngle + orbitDir * u * Mathf.PI * 1.45f;

                Vector3 radial = new Vector3(Mathf.Cos(sweepAngle), 0f, Mathf.Sin(sweepAngle));
                Vector3 tangent = new Vector3(-Mathf.Sin(sweepAngle), 0f, Mathf.Cos(sweepAngle)) * orbitDir;

                pos = new Vector3(center.x, altitude, center.z) + radial * radius;
                forward = tangent.normalized;
                // Steep 52-degree inward bank toward the Kaiju!
                bankDeg = orbitDir * 52f;
                return;
            }

            // Beat 4: All 10 F-22s align into a massive horizontal attack wall, dive for final JDAM salvo, then pull straight skyward!
            {
                float u = Mathf.Clamp01((t - Beat3End) / (TotalDuration - Beat3End));
                float wallLateral = (index - 4.5f) * 7.8f;
                float wallDepth = -Mathf.Abs(index - 4.5f) * 1.6f;

                if (u < 0.58f)
                {
                    float diveU = u / 0.58f;
                    float distAlong = Mathf.Lerp(-68f, 4f, diveU);
                    float alt = combatFloorY + Mathf.Lerp(36f, 21f, Mathf.SmoothStep(0f, 1f, diveU));
                    pos = kaijuAnchorPos + attackDir * distAlong + rightDir * wallLateral + attackDir * wallDepth;
                    pos.y = alt;
                    forward = (attackDir * 0.96f - Vector3.up * 0.28f).normalized;
                    bankDeg = 0f;
                }
                else
                {
                    float climbU = (u - 0.58f) / 0.42f;
                    float easedClimb = climbU * climbU;
                    float distAlong = Mathf.Lerp(4f, 64f, climbU);
                    float alt = combatFloorY + 21f + easedClimb * 82f;
                    float spread = wallLateral * (1f + climbU * 0.55f);
                    pos = kaijuAnchorPos + attackDir * distAlong + rightDir * spread + attackDir * wallDepth;
                    pos.y = alt;
                    forward = Vector3.Slerp(attackDir, (attackDir * 0.45f + Vector3.up * 0.89f).normalized, Mathf.Clamp01(climbU * 1.4f));
                    bankDeg = (index - 4.5f) * 5.5f * climbU;
                }
            }
        }

        private static Vector3 GetDeltaFormationOffset(int index)
        {
            switch (index)
            {
                case 0: return new Vector3(0f, 0f, 0f);
                case 1: return new Vector3(-8.5f, 1.0f, -7.5f);
                case 2: return new Vector3(8.5f, 1.0f, -7.5f);
                case 3: return new Vector3(-17f, 2.2f, -15f);
                case 4: return new Vector3(0f, 3.8f, -14f);
                case 5: return new Vector3(17f, 2.2f, -15f);
                case 6: return new Vector3(-25.5f, 3.5f, -22.5f);
                case 7: return new Vector3(-8.5f, 5.2f, -21.5f);
                case 8: return new Vector3(8.5f, 5.2f, -21.5f);
                default: return new Vector3(25.5f, 3.5f, -22.5f);
            }
        }

        private static Quaternion BuildJetRotation(Vector3 forward, float bankDeg)
        {
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            Quaternion look = Quaternion.LookRotation(forward.normalized, Vector3.up);
            return look * Quaternion.Euler(0f, 0f, bankDeg);
        }

        private void LateUpdate()
        {
            if (!IsRunning || mainCamera == null) return;

            if (jaeger != null && !jaeger.isCutsceneActive)
            {
                jaeger.isCutsceneActive = true;
            }
            EvaluateCinematicCamera(currentTime, Time.deltaTime);
        }

        private void EvaluateCinematicCamera(float t, float dt)
        {
            Vector3 kCenter = GetKaijuCenter();
            Vector3 desiredPos;
            Vector3 desiredLook;
            float desiredFov;
            float desiredRoll = 0f;
            bool hardCut = false;

            if (t < Beat1End)
            {
                // Shot 1: Locked Wingman Chase Cam behind Lead F-22 looking through the 10-jet V toward Kaiju
                var lead = jets[0];
                Vector3 fwd = lead.velocity.sqrMagnitude > 1f ? lead.velocity.normalized : lead.transform.forward;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                desiredPos = lead.transform.position - fwd * 18.0f + right * 7.2f + Vector3.up * 4.0f;
                desiredLook = lead.transform.position + fwd * 22f + Vector3.up * 0.2f;
                desiredFov = Mathf.Lerp(56f, 62f, t / Beat1End);
                desiredRoll = lead.smoothedBankDeg * 0.22f;
                if (!camInitialized) hardCut = true;
            }
            else if (t < 4.35f)
            {
                // Shot 2A: Locked Wing-Pod Missile Launch Cam beside Jet 1 watching salvo ignite into Kaiju
                if (t - dt < Beat1End) hardCut = true;
                var wingman = jets.Count > 1 ? jets[1] : jets[0];
                Vector3 fwd = wingman.velocity.sqrMagnitude > 1f ? wingman.velocity.normalized : wingman.transform.forward;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                desiredPos = wingman.transform.position - fwd * 9.0f - right * 6.8f + Vector3.up * 1.5f;
                desiredLook = Vector3.Lerp(wingman.transform.position + fwd * 14f, kCenter, 0.52f);
                desiredFov = 58f;
                desiredRoll = wingman.smoothedBankDeg * 0.30f;
            }
            else if (t < Beat2End)
            {
                // Shot 2B: Front-Quarter Kaiju Impact Shot as missile waves explode and jets thunder overhead
                if (t - dt < 4.35f) hardCut = true;
                float u = (t - 4.35f) / (Beat2End - 4.35f);
                desiredPos = kCenter - attackDir * Mathf.Lerp(37f, 41f, u) + rightDir * Mathf.Lerp(21f, 16f, u) + Vector3.up * 5f;
                desiredPos.y = Mathf.Max(desiredPos.y, combatFloorY + 6.5f);
                desiredLook = kCenter + Vector3.up * 3.5f;
                desiredFov = 52f;
            }
            else if (t < 8.25f)
            {
                // Shot 3A: Locked Orbital Wing-Cam riding outside Jet 2 in steep 52-degree banked turn around burning Kaiju
                if (t - dt < Beat2End) hardCut = true;
                var orbiter = jets.Count > 2 ? jets[2] : jets[0];
                Vector3 fwd = orbiter.velocity.sqrMagnitude > 1f ? orbiter.velocity.normalized : orbiter.transform.forward;
                Vector3 outward = (orbiter.transform.position - kCenter);
                outward.y = 0f;
                outward = outward.sqrMagnitude > 0.1f ? outward.normalized : rightDir;

                desiredPos = orbiter.transform.position - fwd * 11.5f + outward * 7.5f + Vector3.up * 2.6f;
                desiredLook = Vector3.Lerp(orbiter.transform.position + fwd * 8f, kCenter + Vector3.up * 2f, 0.62f);
                desiredFov = 57f;
                desiredRoll = orbiter.smoothedBankDeg * 0.25f;
            }
            else if (t < Beat3End)
            {
                // Shot 3B: Low-Angle Titan-Scale Chaos Shot looking up at Kaiju under continuous bombardment
                if (t - dt < 8.25f) hardCut = true;
                float u = (t - 8.25f) / (Beat3End - 8.25f);
                desiredPos = kCenter - attackDir * 44f - rightDir * Mathf.Lerp(17f, 11f, u);
                desiredPos.y = combatFloorY + 6.5f;
                desiredLook = kCenter + Vector3.up * 6f;
                desiredFov = 54f;
            }
            else
            {
                // Shot 4: Grand Finale Converging Wall Dive -> Vertical Pull-Up & Colossal Detonation
                if (t - dt < Beat3End) hardCut = true;
                float u = Mathf.Clamp01((t - Beat3End) / (TotalDuration - Beat3End));
                desiredPos = kCenter - attackDir * Mathf.Lerp(48f, 58f, u) + rightDir * Mathf.Lerp(24f, 19f, u);
                desiredPos.y = combatFloorY + Mathf.Lerp(9f, 14f, u);
                desiredLook = kCenter + Vector3.up * Mathf.Lerp(8f, 12f, u);
                desiredFov = u > 0.55f ? Mathf.Lerp(54f, 64f, (u - 0.55f) / 0.45f) : 54f;
            }

            desiredPos.y = Mathf.Max(desiredPos.y, combatFloorY + 4.0f);
            Vector3 lookVec = desiredLook - desiredPos;
            Quaternion targetRot = lookVec.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(lookVec.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, desiredRoll)
                : mainCamera.transform.rotation;

            if (hardCut || !camInitialized)
            {
                camSmoothedPos = desiredPos;
                camSmoothedRot = targetRot;
                camCurrentFov = desiredFov;
                camInitialized = true;
            }
            else
            {
                float posRate = t < Beat1End || (t >= Beat1End && t < 4.35f) || (t >= Beat2End && t < 8.25f) ? 16f : 7.5f;
                camSmoothedPos = Vector3.Lerp(camSmoothedPos, desiredPos, Mathf.Clamp01(dt * posRate));
                camSmoothedRot = Quaternion.Slerp(camSmoothedRot, targetRot, Mathf.Clamp01(dt * posRate));
                camCurrentFov = Mathf.Lerp(camCurrentFov, desiredFov, Mathf.Clamp01(dt * 6f));
            }

            // Apply aerodynamic jet rumble + PacificRimCameraShake impact tremor
            Vector3 jetBuffet = Vector3.zero;
            if (t < Beat1End || (t >= Beat2End && t < 8.25f))
            {
                jetBuffet = new Vector3(
                    (Mathf.PerlinNoise(t * 14f, 1.3f) - 0.5f) * 0.24f,
                    (Mathf.PerlinNoise(5.7f, t * 16f) - 0.5f) * 0.24f,
                    0f);
            }

            Vector3 shakePos = cameraShake != null ? cameraShake.CurrentOffset : Vector3.zero;
            Vector3 shakeRot = cameraShake != null ? cameraShake.CurrentRotationOffset : Vector3.zero;

            mainCamera.fieldOfView = camCurrentFov;
            mainCamera.transform.position = camSmoothedPos + camSmoothedRot * (jetBuffet + shakePos);
            mainCamera.transform.rotation = camSmoothedRot * Quaternion.Euler(shakeRot);
        }

        private void UpdateWeaponsSchedule(float t)
        {
            // Beat 2 & Beat 3: Guided Missile Waves
            if (t >= 2.85f && t < 10.0f && t >= nextMissileWaveTime)
            {
                nextMissileWaveTime = t + (t < Beat2End ? 0.52f : 0.68f);
                int salvoJets = t < Beat2End ? JetCount : 5;
                int offset = (missileWaveIndex % 2) * 5;
                for (int k = 0; k < salvoJets; k++)
                {
                    int jIdx = (k + offset) % jets.Count;
                    SpawnMissileFromJet(jets[jIdx], isHeavyBomb: false);
                }
                missileWaveIndex++;
                PlaySound("Missile - Shot 4", 0.72f, Random.Range(0.94f, 1.12f));
            }

            // Beat 2 & Beat 3: Rapid 20mm Vulcan Cannon Tracer Streams
            if (t >= 3.0f && t < 10.1f && t >= nextCannonBurstTime)
            {
                nextCannonBurstTime = t + 0.085f;
                int shooterCount = t < Beat2End ? 3 : 4;
                for (int s = 0; s < shooterCount; s++)
                {
                    var jet = jets[(missileWaveIndex + s * 3) % jets.Count];
                    SpawnCannonTracer(jet);
                }
                if (Random.value < 0.55f)
                {
                    PlaySound(Random.value < 0.5f ? "Autocannon - Shot 1" : "Machine Gun 1", 0.55f, Random.Range(0.95f, 1.12f));
                }
            }

            // Beat 3: Heavy JDAM Carpet Bombs
            if (t >= 6.3f && t < 9.9f && t >= nextBombDropTime)
            {
                nextBombDropTime = t + 0.42f;
                int jIdx = Random.Range(0, jets.Count);
                SpawnMissileFromJet(jets[jIdx], isHeavyBomb: true);
                SpawnMissileFromJet(jets[(jIdx + 5) % jets.Count], isHeavyBomb: true);
            }

            // Beat 4: Synchronized 10-Jet Grand Finale Mega-Salvo converging at FinaleImpactTime
            if (t >= 10.85f && t <= 11.35f && t >= nextMissileWaveTime)
            {
                nextMissileWaveTime = 999f; // Single massive all-jet salvo
                for (int i = 0; i < jets.Count; i++)
                {
                    SpawnMissileFromJet(jets[i], isHeavyBomb: true, convergeTimeOverride: Mathf.Max(0.35f, FinaleImpactTime - t));
                    SpawnMissileFromJet(jets[i], isHeavyBomb: false, convergeTimeOverride: Mathf.Max(0.35f, FinaleImpactTime - t - 0.12f));
                }
                PlaySound("Missile - Shot 4", 0.95f, 0.88f);
                PlaySound("Flare gun 5-2", 0.70f, 1.0f);
            }

            // Grand Finale Detonation!
            if (!finaleDetonated && t >= FinaleImpactTime)
            {
                finaleDetonated = true;
                TriggerGrandFinaleDetonation();
            }
        }

        private void SpawnMissileFromJet(JetUnit jet, bool isHeavyBomb, float convergeTimeOverride = -1f)
        {
            bool useLeft = (jet.nextHardpointToggle++ % 2) == 0;
            Transform rail = useLeft ? jet.hardpointL : jet.hardpointR;
            Vector3 spawnPos = rail != null ? rail.position : (jet.transform.position - Vector3.up * 0.6f);

            Vector3 targetOff = new Vector3(
                Random.Range(-5.5f, 5.5f),
                Random.Range(-5.0f, 11.0f),
                Random.Range(-5.5f, 5.5f));
            Vector3 aimPos = GetKaijuCenter() + targetOff;
            Vector3 toTarget = (aimPos - spawnPos);
            float dist = Mathf.Max(8f, toTarget.magnitude);

            float speed = convergeTimeOverride > 0f
                ? dist / convergeTimeOverride
                : (isHeavyBomb ? Random.Range(56f, 68f) : Random.Range(74f, 92f));

            Vector3 launchDir = Vector3.Lerp(jet.transform.forward, toTarget.normalized, 0.65f).normalized;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.name = isHeavyBomb ? "JDAM_Bomb" : "AGM_Missile";
            go.transform.position = spawnPos;
            go.transform.localScale = isHeavyBomb ? new Vector3(0.42f, 1.45f, 0.42f) : new Vector3(0.24f, 0.95f, 0.24f);
            go.transform.rotation = Quaternion.LookRotation(launchDir) * Quaternion.Euler(90f, 0f, 0f);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = missileBodyMat;
                rend.shadowCastingMode = ShadowCastingMode.Off;
            }

            // Glowing rocket motor tail sphere + smoke trail
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var gCol = glow.GetComponent<Collider>();
            if (gCol != null) Destroy(gCol);
            glow.transform.SetParent(go.transform, false);
            glow.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            glow.transform.localScale = new Vector3(1.2f, 0.55f, 1.2f);
            var gRend = glow.GetComponent<Renderer>();
            if (gRend != null)
            {
                gRend.sharedMaterial = burnerInnerMat;
                gRend.shadowCastingMode = ShadowCastingMode.Off;
            }

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = isHeavyBomb ? 0.85f : 0.65f;
            trail.startWidth = isHeavyBomb ? 0.55f : 0.34f;
            trail.endWidth = 0.08f;
            trail.sharedMaterial = missileTrailMat;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.72f, 0.3f), 0f), new GradientColorKey(new Color(0.75f, 0.78f, 0.82f), 0.35f), new GradientColorKey(new Color(0.4f, 0.42f, 0.45f), 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.50f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            trail.colorGradient = grad;

            projectiles.Add(new ActiveProjectile
            {
                go = go,
                transform = go.transform,
                position = spawnPos,
                velocity = launchDir * speed,
                targetOffset = targetOff,
                age = 0f,
                maxLife = 2.4f,
                impactRadius = isHeavyBomb ? 5.2f : 4.2f,
                isHeavyBomb = isHeavyBomb,
                isTracer = false,
                turnRate = isHeavyBomb ? 7.5f : 11f
            });
        }

        private void SpawnCannonTracer(JetUnit jet)
        {
            Vector3 muzzle = jet.gunMuzzle != null ? jet.gunMuzzle.position : (jet.transform.position + jet.transform.forward * 2.5f);
            Vector3 targetOff = new Vector3(Random.Range(-4.5f, 4.5f), Random.Range(-3.5f, 9.5f), Random.Range(-4.5f, 4.5f));
            Vector3 dir = (GetKaijuCenter() + targetOff - muzzle).normalized;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.name = "VulcanTracer";
            go.transform.position = muzzle;
            go.transform.localScale = new Vector3(0.14f, 2.2f, 0.14f);
            go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = tracerMat;
                rend.shadowCastingMode = ShadowCastingMode.Off;
            }

            projectiles.Add(new ActiveProjectile
            {
                go = go,
                transform = go.transform,
                position = muzzle,
                velocity = dir * 185f,
                targetOffset = targetOff,
                age = 0f,
                maxLife = 1.2f,
                impactRadius = 4.0f,
                isHeavyBomb = false,
                isTracer = true,
                turnRate = 0f
            });
        }

        private void UpdateProjectiles(float dt)
        {
            Vector3 kCenter = GetKaijuCenter();

            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var p = projectiles[i];
                p.age += dt;
                Vector3 target = kCenter + p.targetOffset;
                Vector3 toTarget = target - p.position;
                float dist = toTarget.magnitude;

                if (!p.isTracer && p.turnRate > 0f && dist > 0.5f)
                {
                    float speed = p.velocity.magnitude;
                    Vector3 desiredVel = toTarget.normalized * speed;
                    p.velocity = Vector3.Slerp(p.velocity, desiredVel, Mathf.Clamp01(dt * p.turnRate));
                }

                Vector3 step = p.velocity * dt;
                p.position += step;

                if (p.transform != null)
                {
                    p.transform.position = p.position;
                    if (p.velocity.sqrMagnitude > 0.01f)
                    {
                        p.transform.rotation = Quaternion.LookRotation(p.velocity.normalized) * Quaternion.Euler(90f, 0f, 0f);
                    }
                }

                if (dist <= p.impactRadius || dist <= step.magnitude * 1.15f || p.age >= p.maxLife)
                {
                    if (p.isTracer)
                    {
                        SpawnTracerImpactSparks(p.position);
                    }
                    else
                    {
                        SpawnExplosion(p.position, p.isHeavyBomb ? 1.45f : 0.92f, playAudio: true);
                    }

                    if (p.go != null) Destroy(p.go);
                    projectiles.RemoveAt(i);
                }
            }
        }

        private void SpawnTracerImpactSparks(Vector3 pos)
        {
            if (Random.value < 0.45f)
            {
                PlaySound("Bullet Impacts - Multiple 1", 0.40f, Random.Range(0.92f, 1.15f));
            }
            SpawnSparkBurst(pos, 10, 18f, 0.35f);
            if (Random.value < 0.35f)
            {
                SpawnFlashSphere(pos, 0.8f, 3.5f, 0.12f, new Color(1.0f, 0.72f, 0.25f, 0.9f), withLight: false);
            }
        }

        private void SpawnExplosion(Vector3 pos, float scale, bool playAudio)
        {
            // 1. Hot fireball core flash sphere + light
            SpawnFlashSphere(pos, 1.4f * scale, 9.5f * scale, 0.32f, new Color(1.0f, 0.52f, 0.12f, 0.92f), withLight: effects.Count < 14);

            // 2. Expanding shockwave ring
            SpawnShockwaveRing(pos, 1.5f * scale, 18f * scale, 0.45f, new Color(1.0f, 0.78f, 0.35f, 0.72f));

            // 3. Fireball & smoke particle burst + hot flying sparks
            SpawnExplosionParticles(pos, scale);
            SpawnSparkBurst(pos, Mathf.RoundToInt(18 * scale), 26f * scale, 0.55f);

            // 4. Water / seabed reaction if ocean is present
            if (waterSystem != null)
            {
                Vector3 wavePos = new Vector3(pos.x, waterSystem.MeanSurfaceY, pos.z);
                waterSystem.AddFootstepWave(wavePos, 18f * scale, 1.4f * scale);
                splashManager?.SpawnBodySplash(wavePos, Vector3.up, 0.85f * scale, scale);
            }

            // 5. Camera shake & audio
            cameraShake?.Shake(0.32f * scale, 0.24f);
            if (playAudio && effects.Count < 18)
            {
                string clipName = Random.value < 0.34f ? "Explosion 1" : (Random.value < 0.5f ? "Explosion 2" : "Explosion 8");
                PlaySound(clipName, Mathf.Clamp01(0.55f * scale), Random.Range(0.88f, 1.08f));
            }
        }

        private void TriggerGrandFinaleDetonation()
        {
            Vector3 kCenter = GetKaijuCenter();

            // Screen flash punch + peak camera shake
            screenFlashAlpha = 0.42f;
            cameraShake?.TriggerPlasmaDischargeFeedback();
            cameraShake?.Shake(1.65f, 0.95f);

            // Layered massive explosion audio
            PlaySound("Explosion 8", 1.0f, 0.82f);
            PlaySound("Cannon impact 9", 0.95f, 0.86f);
            PlaySound("Earthquake 3 - Big", 1.0f, 0.95f);
            PlaySound("KaijuRoar_EmpBlast", 0.85f, 0.92f);

            // Huge concentric fireballs + shockwave spheres + horizontal shockwave rings
            SpawnFlashSphere(kCenter, 4f, 38f, 0.75f, new Color(1.0f, 0.85f, 0.45f, 0.95f), withLight: true);
            SpawnFlashSphere(kCenter + Vector3.up * 4f, 3f, 28f, 0.95f, new Color(1.0f, 0.42f, 0.08f, 0.85f), withLight: false);
            SpawnShockwaveRing(kCenter, 3f, 56f, 0.78f, new Color(1.0f, 0.88f, 0.55f, 0.90f));
            SpawnShockwaveRing(new Vector3(kCenter.x, combatFloorY + 2f, kCenter.z), 4f, 68f, 0.92f, new Color(1.0f, 0.58f, 0.18f, 0.78f));

            SpawnExplosionParticles(kCenter, 3.2f);
            SpawnExplosionParticles(kCenter + Vector3.up * 6f, 2.5f);
            SpawnSparkBurst(kCenter, 85, 48f, 1.05f);

            if (waterSystem != null)
            {
                Vector3 wavePos = new Vector3(kCenter.x, waterSystem.MeanSurfaceY, kCenter.z);
                waterSystem.AddFootstepWave(wavePos, 48f, 4.2f);
                splashManager?.SpawnColossalTitanDropSplash(wavePos);
            }

            if (targetKaiju != null && !targetKaiju.IsDead)
            {
                targetKaiju.currentHealth = 1f;
                targetKaiju.DebugForceStagger();
            }
        }

        private void SpawnFlashSphere(Vector3 pos, float startScale, float endScale, float duration, Color color, bool withLight)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.name = "Airstrike_FireballFlash";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * startScale;

            var mat = new Material(fireballAdditiveMat);
            SetMaterialColor(mat, color);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = ShadowCastingMode.Off;
            }

            Light l = null;
            if (withLight)
            {
                l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1.0f, 0.62f, 0.22f);
                l.range = endScale * 3.8f;
                l.intensity = 28f;
                l.shadows = LightShadows.None;
            }

            effects.Add(new TransientEffect
            {
                go = go,
                transform = go.transform,
                mat = mat,
                light = l,
                age = 0f,
                duration = duration,
                startScale = startScale,
                endScale = endScale,
                startLightIntensity = l != null ? l.intensity : 0f,
                baseColor = color,
                isRing = false
            });
        }

        private void SpawnShockwaveRing(Vector3 pos, float startScale, float endScale, float duration, Color color)
        {
            var go = new GameObject("Airstrike_ShockwaveRing");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(Random.Range(-18f, 18f), Random.Range(0f, 360f), Random.Range(-18f, 18f));
            go.transform.localScale = Vector3.one * startScale;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = ringMesh;
            var mat = new Material(shockwaveRingMat);
            SetMaterialColor(mat, color);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            effects.Add(new TransientEffect
            {
                go = go,
                transform = go.transform,
                mat = mat,
                age = 0f,
                duration = duration,
                startScale = startScale,
                endScale = endScale,
                baseColor = color,
                isRing = true
            });
        }

        private ParticleSystem CreateOneShotParticleSystem(
            string goName,
            Vector3 pos,
            float duration,
            float gravity,
            short burstCount,
            Material mat,
            ParticleSystemRenderMode renderMode)
        {
            var go = new GameObject(goName);
            go.transform.position = pos;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = duration;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, burstCount) });

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = mat;
            rend.renderMode = renderMode;
            rend.shadowCastingMode = ShadowCastingMode.Off;

            return ps;
        }

        private void SpawnExplosionParticles(Vector3 pos, float scale)
        {
            short burst = (short)Mathf.Clamp(Mathf.RoundToInt(18 * scale), 10, 48);
            var ps = CreateOneShotParticleSystem("Airstrike_SmokeFirePS", pos, 1.6f, -0.35f, burst, smokeAlphaMat, ParticleSystemRenderMode.Billboard);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f * scale, 22f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(3.2f * scale, 7.2f * scale);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.14f, 0.13f, 0.85f), new Color(1.0f, 0.45f, 0.12f, 0.85f));

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.8f * scale;

            var sizeLife = ps.sizeOverLifetime;
            sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.45f),
                new Keyframe(0.35f, 1.15f),
                new Keyframe(1f, 1.75f)));

            var colLife = ps.colorOverLifetime;
            colLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.65f, 0.25f), 0f), new GradientColorKey(new Color(0.22f, 0.18f, 0.16f), 0.35f), new GradientColorKey(new Color(0.12f, 0.12f, 0.14f), 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.68f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colLife.color = new ParticleSystem.MinMaxGradient(grad);

            ps.Play();
        }

        private void SpawnSparkBurst(Vector3 pos, int count, float speed, float life)
        {
            short burst = (short)Mathf.Clamp(count, 4, 96);
            var ps = CreateOneShotParticleSystem("Airstrike_SparksPS", pos, life + 0.1f, 1.1f, burst, sparkAdditiveMat, ParticleSystemRenderMode.Stretch);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.86f, 0.42f, 1f), new Color(1.0f, 0.52f, 0.15f, 1f));

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.0f;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.velocityScale = 0.08f;
            rend.lengthScale = 2.6f;

            ps.Play();
        }

        private void SpawnFlareBurst(Vector3 pos, Vector3 inheritVel)
        {
            var ps = CreateOneShotParticleSystem("Airstrike_FlarePS", pos, 0.9f, 0.7f, 2, sparkAdditiveMat, ParticleSystemRenderMode.Billboard);

            var main = ps.main;
            main.startLifetime = 0.85f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = 1.35f;
            main.startColor = new Color(1.0f, 0.92f, 0.55f, 1f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = inheritVel.x;
            vel.y = inheritVel.y;
            vel.z = inheritVel.z;

            ps.Play();
        }

        private void UpdateTransientEffects(float dt)
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var fx = effects[i];
                fx.age += dt;
                float u = Mathf.Clamp01(fx.age / Mathf.Max(0.01f, fx.duration));

                if (fx.transform != null)
                {
                    float ease = 1f - Mathf.Pow(1f - u, 2.2f);
                    float s = Mathf.Lerp(fx.startScale, fx.endScale, ease);
                    fx.transform.localScale = Vector3.one * s;
                }

                if (fx.mat != null)
                {
                    Color c = fx.baseColor;
                    c.a = fx.baseColor.a * (1f - u) * (1f - u);
                    SetMaterialColor(fx.mat, c);
                }

                if (fx.light != null)
                {
                    fx.light.intensity = fx.startLightIntensity * Mathf.Pow(1f - u, 1.8f);
                }

                if (fx.age >= fx.duration)
                {
                    if (fx.mat != null) Destroy(fx.mat);
                    if (fx.go != null) Destroy(fx.go);
                    effects.RemoveAt(i);
                }
            }
        }

        private void UpdateKaijuDamageAndReaction(float t)
        {
            if (targetKaiju == null || targetKaiju.IsDead) return;

            if (t >= 3.3f && t < FinaleImpactTime)
            {
                // Visibly shred Kaiju health bar down to 1 HP during bombardment without prematurely triggering PhaseTransition/Die
                float dmgProgress = Mathf.Clamp01((t - 3.3f) / (FinaleImpactTime - 3.3f));
                targetKaiju.currentHealth = Mathf.Max(1f, Mathf.Lerp(kaijuStartHealth, 1f, dmgProgress));

                if (t >= nextStaggerTime)
                {
                    nextStaggerTime = t + 1.45f;
                    targetKaiju.DebugForceStagger();
                    PlaySound("KaijuStaggerPain_01", 0.72f, Random.Range(0.92f, 1.06f));
                }
            }
        }

        private void BuildProceduralVfxResources()
        {
            softGlowTex = CreateSoftGlowTexture();
            ribbonTrailTex = CreateRibbonTrailTexture();
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                 ?? Shader.Find("Universal Render Pipeline/Unlit")
                                 ?? Shader.Find("Sprites/Default");
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? particleShader;

            fireballAdditiveMat = CreateParticleMat("Airstrike_FireballMat", particleShader, softGlowTex, Color.white, additive: true);
            smokeAlphaMat = CreateParticleMat("Airstrike_SmokeMat", particleShader, softGlowTex, Color.white, additive: false);
            sparkAdditiveMat = CreateParticleMat("Airstrike_SparkMat", particleShader, softGlowTex, new Color(2.2f, 1.6f, 0.8f, 1f), additive: true);
            shockwaveRingMat = CreateParticleMat("Airstrike_RingMat", particleShader, softGlowTex, Color.white, additive: true);
            missileTrailMat = CreateParticleMat("Airstrike_TrailMat", particleShader, ribbonTrailTex, Color.white, additive: false);
            wingtipTrailMat = CreateParticleMat("Airstrike_WingtipTrailMat", particleShader, ribbonTrailTex, Color.white, additive: true);

            burnerOuterMat = CreateParticleMat("Airstrike_BurnerOuter", unlitShader, null, new Color(2.8f, 1.15f, 0.22f, 0.72f), additive: true);
            burnerInnerMat = CreateParticleMat("Airstrike_BurnerInner", unlitShader, null, new Color(1.6f, 2.6f, 3.8f, 0.92f), additive: true);
            tracerMat = CreateParticleMat("Airstrike_TracerMat", unlitShader, null, new Color(3.2f, 2.2f, 0.55f, 1f), additive: true);

            missileBodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? unlitShader)
            {
                name = "Airstrike_MissileBody",
                color = new Color(0.88f, 0.90f, 0.92f)
            };

            coneMesh = CreateTaperedConeMesh(14);
            ringMesh = CreateShockwaveRingMesh(32);
        }

        private static Material CreateParticleMat(string name, Shader shader, Texture2D tex, Color color, bool additive)
        {
            var mat = new Material(shader) { name = name };
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                mat.mainTexture = tex;
            }
            SetMaterialColor(mat, color);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", additive ? 2f : 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) mat.EnableKeyword("_BLENDMODE_ADD");
            mat.renderQueue = (int)RenderQueue.Transparent + 40;
            return mat;
        }

        private static void SetMaterialColor(Material mat, Color color)
        {
            if (mat == null) return;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }

        private static Texture2D CreateRibbonTrailTexture()
        {
            const int w = 8;
            const int h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Airstrike_RibbonTrail",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y / (float)(h - 1)) * 2f - 1f;
                float edge = Mathf.Clamp01(1f - Mathf.Abs(v));
                float alpha = Mathf.Pow(edge, 1.35f);
                for (int x = 0; x < w; x++)
                {
                    pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D CreateSoftGlowTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Airstrike_SoftGlow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float half = (size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float falloff = Mathf.Clamp01(1f - r);
                    float a = Mathf.Pow(falloff, 2.0f);
                    float core = Mathf.Exp(-r * r * 4.5f);
                    float rgb = Mathf.Clamp01(0.55f * falloff + 0.65f * core);
                    pixels[y * size + x] = new Color(rgb, rgb, rgb, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static Mesh CreateTaperedConeMesh(int segments)
        {
            var mesh = new Mesh { name = "Airstrike_BurnerCone" };
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 3];

            verts[0] = Vector3.zero;
            verts[1] = Vector3.forward; // Tapered exhaust tip

            for (int i = 0; i < segments; i++)
            {
                float ang = (i / (float)segments) * Mathf.PI * 2f;
                verts[i + 2] = new Vector3(Mathf.Cos(ang) * 0.5f, Mathf.Sin(ang) * 0.5f, 0f);
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                tris[i * 3 + 0] = 1;
                tris[i * 3 + 1] = i + 2;
                tris[i * 3 + 2] = next + 2;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh CreateShockwaveRingMesh(int segments)
        {
            var mesh = new Mesh { name = "Airstrike_ShockwaveRing" };
            var verts = new Vector3[segments * 2];
            var uvs = new Vector2[segments * 2];
            var tris = new int[segments * 6];

            const float innerR = 0.72f;
            const float outerR = 1.0f;

            for (int i = 0; i < segments; i++)
            {
                float ang = (i / (float)segments) * Mathf.PI * 2f;
                float c = Mathf.Cos(ang);
                float s = Mathf.Sin(ang);
                verts[i * 2] = new Vector3(c * innerR, 0f, s * innerR);
                verts[i * 2 + 1] = new Vector3(c * outerR, 0f, s * outerR);
                uvs[i * 2] = new Vector2(i / (float)segments, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = i * 2;
                int i1 = ((i + 1) % segments) * 2;
                int b = i * 6;
                tris[b + 0] = i0;
                tris[b + 1] = i0 + 1;
                tris[b + 2] = i1 + 1;
                tris[b + 3] = i0;
                tris[b + 4] = i1 + 1;
                tris[b + 5] = i1;
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        private void BuildOverlayUI()
        {
            var canvasGo = new GameObject("AmericaAirstrike_OverlayCanvas");
            canvasGo.transform.SetParent(transform, false);

            overlayCanvas = canvasGo.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 520;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            overlayGroup = canvasGo.AddComponent<CanvasGroup>();
            overlayGroup.alpha = 1f;
            overlayGroup.blocksRaycasts = false;
            overlayGroup.interactable = false;

            // Screen flash panel
            var flashGo = new GameObject("FinaleFlash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(canvasGo.transform, false);
            var flashRt = flashGo.GetComponent<RectTransform>();
            flashRt.anchorMin = Vector2.zero;
            flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = Vector2.zero;
            flashRt.offsetMax = Vector2.zero;
            screenFlashImage = flashGo.GetComponent<Image>();
            screenFlashImage.color = new Color(1f, 0.95f, 0.82f, 0f);
            screenFlashImage.raycastTarget = false;

            // Top & Bottom 2.39:1 cinematic letterbox bars
            topLetterboxBar = CreateBar("TopBar", canvasGo.transform, isTop: true, height: 108f);
            bottomLetterboxBar = CreateBar("BottomBar", canvasGo.transform, isTop: false, height: 108f);

            Font uiFont = PacificRimUITheme.GetDefaultFont();

            // Tactical USAF callsign tag inside top bar
            var callGo = new GameObject("RadioCallsign", typeof(RectTransform), typeof(Text));
            callGo.transform.SetParent(topLetterboxBar, false);
            var callRt = callGo.GetComponent<RectTransform>();
            callRt.anchorMin = new Vector2(0f, 0f);
            callRt.anchorMax = new Vector2(1f, 1f);
            callRt.offsetMin = new Vector2(36f, 0f);
            callRt.offsetMax = new Vector2(-36f, 0f);
            radioCallsignText = callGo.GetComponent<Text>();
            radioCallsignText.font = uiFont;
            radioCallsignText.fontSize = 15;
            radioCallsignText.fontStyle = FontStyle.Bold;
            radioCallsignText.alignment = TextAnchor.MiddleLeft;
            radioCallsignText.color = PacificRimUITheme.BlueBright;
            radioCallsignText.text = "USAF // FREEDOM-1 SQUADRON   [10x F-22 RAPTOR AIR SUPERIORITY]";
            radioCallsignText.raycastTarget = false;

            // Dynamic meme tactical caption centered in bottom bar
            var capGo = new GameObject("BeatCaption", typeof(RectTransform), typeof(Text));
            capGo.transform.SetParent(bottomLetterboxBar, false);
            var capRt = capGo.GetComponent<RectTransform>();
            capRt.anchorMin = Vector2.zero;
            capRt.anchorMax = Vector2.one;
            capRt.offsetMin = new Vector2(36f, 0f);
            capRt.offsetMax = new Vector2(-36f, 0f);
            beatCaptionText = capGo.GetComponent<Text>();
            beatCaptionText.font = uiFont;
            beatCaptionText.fontSize = 20;
            beatCaptionText.fontStyle = FontStyle.Bold;
            beatCaptionText.alignment = TextAnchor.MiddleCenter;
            beatCaptionText.color = PacificRimUITheme.Amber;
            beatCaptionText.text = "";
            beatCaptionText.raycastTarget = false;
        }

        private static RectTransform CreateBar(string name, Transform parent, bool isTop, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = isTop ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
            rt.anchorMax = isTop ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
            rt.pivot = isTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);

            var img = go.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            return rt;
        }

        private void SetCaption(string msg)
        {
            if (beatCaptionText != null)
            {
                beatCaptionText.text = msg;
            }
        }

        private void UpdateUI(float dt)
        {
            if (screenFlashImage != null && screenFlashAlpha > 0f)
            {
                screenFlashAlpha = Mathf.MoveTowards(screenFlashAlpha, 0f, dt * 2.5f);
                screenFlashImage.color = new Color(1f, 0.94f, 0.78f, screenFlashAlpha);
            }

            float barHeight = 108f;
            if (currentTime < 0.35f)
            {
                barHeight = Mathf.Lerp(0f, 108f, currentTime / 0.35f);
            }
            else if (currentTime > TotalDuration - 0.45f)
            {
                barHeight = Mathf.Lerp(108f, 0f, (currentTime - (TotalDuration - 0.45f)) / 0.45f);
            }

            if (topLetterboxBar != null) topLetterboxBar.sizeDelta = new Vector2(0f, barHeight);
            if (bottomLetterboxBar != null) bottomLetterboxBar.sizeDelta = new Vector2(0f, barHeight);
        }

        private void BuildAudioPool()
        {
            for (int i = 0; i < 14; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.bypassListenerEffects = true;
                src.ignoreListenerPause = false;
                sfxPool.Add(src);
            }

            jetEngineLoopSource = gameObject.AddComponent<AudioSource>();
            jetEngineLoopSource.playOnAwake = false;
            jetEngineLoopSource.loop = true;
            jetEngineLoopSource.spatialBlend = 0f;
            jetEngineLoopSource.bypassListenerEffects = true;
        }

        private void StartJetEngineLoop()
        {
            var audioMgr = PacificRimAudioManager.Instance;
            if (audioMgr == null || jetEngineLoopSource == null) return;

            AudioClip loopClip = audioMgr.GetClip("JaegerRocketBurnLoop") ?? audioMgr.GetClip("Spaceship passing by");
            if (loopClip != null)
            {
                jetEngineLoopSource.clip = loopClip;
                jetEngineLoopSource.volume = 0.42f * audioMgr.SfxVolume;
                jetEngineLoopSource.pitch = 1.15f;
                jetEngineLoopSource.Play();
            }
        }

        private void PlaySound(string clipName, float volume = 0.8f, float pitch = 1.0f)
        {
            var audioMgr = PacificRimAudioManager.Instance;
            AudioClip clip = audioMgr != null ? audioMgr.GetClip(clipName) : null;
            if (clip == null) return;

            float masterSfx = audioMgr != null ? audioMgr.SfxVolume : 0.6f;
            AudioSource target = null;
            for (int i = 0; i < sfxPool.Count; i++)
            {
                if (!sfxPool[i].isPlaying)
                {
                    target = sfxPool[i];
                    break;
                }
            }
            if (target == null && sfxPool.Count > 0) target = sfxPool[0];
            if (target == null) return;

            target.pitch = pitch;
            target.volume = Mathf.Clamp01(volume * masterSfx);
            target.PlayOneShot(clip, 1f);
        }

        private void CleanupAndRestore(bool triggerKill)
        {
            if (cleanedUp) return;
            cleanedUp = true;
            IsRunning = false;

            if (jetEngineLoopSource != null && jetEngineLoopSource.isPlaying)
            {
                jetEngineLoopSource.Stop();
            }

            // Destroy active jets, projectiles, and temporary effect meshes cleanly
            for (int i = 0; i < projectiles.Count; i++)
            {
                if (projectiles[i].go != null) Destroy(projectiles[i].go);
            }
            projectiles.Clear();

            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].mat != null) Destroy(effects[i].mat);
                if (effects[i].go != null) Destroy(effects[i].go);
            }
            effects.Clear();

            for (int i = 0; i < jets.Count; i++)
            {
                if (jets[i].root != null) Destroy(jets[i].root);
            }
            jets.Clear();

            // Clean up shared procedural materials/meshes/textures
            DestroyAssetSafe(fireballAdditiveMat);
            DestroyAssetSafe(smokeAlphaMat);
            DestroyAssetSafe(sparkAdditiveMat);
            DestroyAssetSafe(shockwaveRingMat);
            DestroyAssetSafe(missileBodyMat);
            DestroyAssetSafe(missileTrailMat);
            DestroyAssetSafe(wingtipTrailMat);
            DestroyAssetSafe(tracerMat);
            DestroyAssetSafe(burnerOuterMat);
            DestroyAssetSafe(burnerInnerMat);
            DestroyAssetSafe(coneMesh);
            DestroyAssetSafe(ringMesh);
            DestroyAssetSafe(softGlowTex);
            DestroyAssetSafe(ribbonTrailTex);

            if (savedStateCaptured)
            {
                if (modifiedFogDensity)
                {
                    RenderSettings.fogDensity = savedFogDensity;
                }

                if (modifiedLowPass && cameraLowPass != null)
                {
                    cameraLowPass.cutoffFrequency = savedLowPassCutoff;
                }

                if (mainCamera != null)
                {
                    mainCamera.fieldOfView = savedCameraFov;
                }

                if (cameraFollow != null)
                {
                    cameraFollow.enabled = savedCamFollowEnabled;
                }

                if (jaeger != null)
                {
                    jaeger.isCutsceneActive = savedJaegerCutscene;
                    jaeger.SetGodMode(savedJaegerGodMode);
                }

                if (targetKaiju != null)
                {
                    targetKaiju.SetAIDisabled(savedKaijuAiDisabled);
                }

                JaegerHUD.Instance?.SetCutsceneHidden(false);
            }

            if (triggerKill && targetKaiju != null && !targetKaiju.IsDead)
            {
                targetKaiju.SetAIDisabled(false);
                targetKaiju.DebugKillBoss();
            }
        }

        private static void DestroyAssetSafe(Object obj)
        {
            if (obj != null) Destroy(obj);
        }
    }
}
