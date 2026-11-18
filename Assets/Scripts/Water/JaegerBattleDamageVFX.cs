using System.Collections.Generic;
using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Progressive hull battle-damage VFX for Gipsy Danger:
    /// - >= 70% HP: Clean hull (no battle-damage sparks or arcs).
    /// - < 70% HP: Sparse sparks and rare subtle electric crackles ("barely").
    /// - < 40% HP: Increased sparks across torso/limbs with the same electricity crackle rate.
    /// - < 10% HP: Increased electricity crackles and continuous sparking across all rig joints.
    /// </summary>
    public class JaegerBattleDamageVFX : MonoBehaviour
    {
        private enum DamageTier
        {
            None = 0,
            Light = 1,    // < 70% HP
            Moderate = 2, // < 40% HP
            Critical = 3  // < 10% HP
        }

        private struct JointSocket
        {
            public Transform bone;
            public float surfaceOffset;
            public bool isUpperPrimary;
            public bool isMidBody;
        }

        private struct ActiveArc
        {
            public LineRenderer line;
            public Light flashLight;
            public float endTime;
            public float nextJitterTime;
            public JointSocket startJoint;
            public JointSocket endJoint;
            public Vector3 localOffsetA;
            public Vector3 localOffsetB;
        }

        private SimpleJaegerController jaeger;
        private Transform vfxRoot;
        private ParticleSystem sparkSystem;
        private Material sparkMaterial;
        private Material arcMaterial;
        private Texture2D sparkTexture;

        private readonly List<JointSocket> allJoints = new List<JointSocket>(16);
        private readonly List<JointSocket> primaryJoints = new List<JointSocket>(8);
        private readonly List<JointSocket> moderateJoints = new List<JointSocket>(12);

        private readonly List<ActiveArc> arcPool = new List<ActiveArc>(6);
        private readonly Vector3[] arcPointBuffer = new Vector3[9];

        private Transform spineAnchor;
        private Transform chestAnchor;
        private Transform hipsAnchor;

        private float nextSparkBurstTime;
        private float nextAllJointSparkTime;
        private float nextArcTime;
        private float nextSoftArcAudioTime;
        private int jointRoundRobinIndex;
        private DamageTier lastTier = DamageTier.None;
        private bool initialized;

        private static readonly Color SparkCore = new Color(1.0f, 0.94f, 0.72f, 1.0f);
        private static readonly Color SparkAmber = new Color(1.0f, 0.56f, 0.14f, 1.0f);
        private static readonly Color SparkHotOrange = new Color(1.0f, 0.28f, 0.06f, 1.0f);
        private static readonly Color SparkElectricCyan = new Color(0.45f, 0.92f, 1.0f, 1.0f);

        private void Awake()
        {
            jaeger = GetComponent<SimpleJaegerController>();
            if (Application.isPlaying)
            {
                EnsureInitialized();
            }
        }

        private void OnDestroy()
        {
            if (sparkMaterial != null) Destroy(sparkMaterial);
            if (arcMaterial != null) Destroy(arcMaterial);
            if (sparkTexture != null) Destroy(sparkTexture);
            if (vfxRoot != null) Destroy(vfxRoot.gameObject);
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            if (jaeger == null) jaeger = GetComponent<SimpleJaegerController>();

            ResolveJointSockets();
            BuildVfxRootAndSystems();
            initialized = true;
        }

        private void ResolveJointSockets()
        {
            allJoints.Clear();
            primaryJoints.Clear();
            moderateJoints.Clear();

            AddJoint("Chest", 1.55f, isUpperPrimary: true, isMidBody: true);
            AddJoint("Spine1", 1.35f, isUpperPrimary: true, isMidBody: true);
            AddJoint("Hips", 1.25f, isUpperPrimary: false, isMidBody: true);
            AddJoint("Head", 0.85f, isUpperPrimary: false, isMidBody: true);

            AddJoint("Upperarm.L", 0.95f, isUpperPrimary: true, isMidBody: true);
            AddJoint("Upperarm.R", 0.95f, isUpperPrimary: true, isMidBody: true);
            AddJoint("Forearm.L", 0.65f, isUpperPrimary: true, isMidBody: true);
            AddJoint("Forearm.R", 0.65f, isUpperPrimary: true, isMidBody: true);
            AddJoint("Hand.L", 0.50f, isUpperPrimary: false, isMidBody: false);
            AddJoint("Hand.R", 0.50f, isUpperPrimary: false, isMidBody: false);

            AddJoint("Thigh.L", 0.90f, isUpperPrimary: false, isMidBody: true);
            AddJoint("Thigh.R", 0.90f, isUpperPrimary: false, isMidBody: true);
            AddJoint("Shin.L", 0.68f, isUpperPrimary: false, isMidBody: false);
            AddJoint("Shin.R", 0.68f, isUpperPrimary: false, isMidBody: false);
            AddJoint("Foot.L", 0.58f, isUpperPrimary: false, isMidBody: false);
            AddJoint("Foot.R", 0.58f, isUpperPrimary: false, isMidBody: false);

            spineAnchor = FindChildRecursive(transform, "Spine1");
            chestAnchor = FindChildRecursive(transform, "Chest");
            hipsAnchor = FindChildRecursive(transform, "Hips");
        }

        private void AddJoint(string boneName, float offset, bool isUpperPrimary, bool isMidBody)
        {
            Transform bone = FindChildRecursive(transform, boneName);
            if (bone == null) return;

            var socket = new JointSocket
            {
                bone = bone,
                surfaceOffset = offset,
                isUpperPrimary = isUpperPrimary,
                isMidBody = isMidBody
            };
            allJoints.Add(socket);
            if (isUpperPrimary) primaryJoints.Add(socket);
            if (isMidBody) moderateJoints.Add(socket);
        }

        private static Transform FindChildRecursive(Transform parent, string targetName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == targetName) return child;
                Transform found = FindChildRecursive(child, targetName);
                if (found != null) return found;
            }
            return null;
        }

        private void BuildVfxRootAndSystems()
        {
            Transform existing = transform.Find("BattleDamageVFX_Root");
            if (existing != null) Destroy(existing.gameObject);

            GameObject rootGo = new GameObject("BattleDamageVFX_Root");
            rootGo.transform.SetParent(transform, false);
            rootGo.transform.localPosition = Vector3.zero;
            rootGo.transform.localRotation = Quaternion.identity;
            rootGo.transform.localScale = Vector3.one;
            vfxRoot = rootGo.transform;

            sparkTexture = CreateSoftSparkTexture();
            sparkMaterial = CreateSparkMaterial(sparkTexture);
            arcMaterial = CreateArcMaterial();

            // Spark particle system operating in World space so DCC bone scaling never distorts particles
            GameObject psGo = new GameObject("BattleDamageSparks");
            psGo.transform.SetParent(vfxRoot, false);
            sparkSystem = psGo.AddComponent<ParticleSystem>();
            sparkSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = sparkSystem.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 60f;
            main.maxParticles = 480;
            main.startLifetime = 0.4f;
            main.startSpeed = 0f;
            main.startSize = 0.28f;
            main.gravityModifier = 0.95f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = sparkSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = sparkSystem.shape;
            shape.enabled = false;

            var col = sparkSystem.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.6f), 0.45f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.15f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.06f),
                    new GradientAlphaKey(0.85f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = g;

            var sol = sparkSystem.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(1f, 0.2f);
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var psr = psGo.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = sparkMaterial;
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.velocityScale = 0.055f;
            psr.lengthScale = 2.4f;
            psr.cameraVelocityScale = 0f;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;

            sparkSystem.Play();

            // Pooled 3D electric arc renderers
            arcPool.Clear();
            const int poolCount = 6;
            for (int i = 0; i < poolCount; i++)
            {
                GameObject arcGo = new GameObject($"ElectricCrackleArc_{i}");
                arcGo.transform.SetParent(vfxRoot, false);

                var lr = arcGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = arcPointBuffer.Length;
                lr.startWidth = 0.16f;
                lr.endWidth = 0.07f;
                lr.numCapVertices = 2;
                lr.numCornerVertices = 1;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.sharedMaterial = arcMaterial;
                lr.enabled = false;

                Gradient arcGrad = new Gradient();
                arcGrad.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(0.85f, 0.98f, 1.0f), 0f),
                        new GradientColorKey(new Color(0.28f, 0.88f, 1.0f), 0.5f),
                        new GradientColorKey(new Color(0.85f, 0.98f, 1.0f), 1f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(0.35f, 0f),
                        new GradientAlphaKey(1.0f, 0.25f),
                        new GradientAlphaKey(1.0f, 0.75f),
                        new GradientAlphaKey(0.25f, 1f)
                    });
                lr.colorGradient = arcGrad;

                Light flash = null;
                if (i < 2)
                {
                    flash = arcGo.AddComponent<Light>();
                    flash.type = LightType.Point;
                    flash.color = new Color(0.35f, 0.88f, 1.0f, 1f);
                    flash.range = 10.5f;
                    flash.intensity = 42f;
                    flash.shadows = LightShadows.None;
                    flash.enabled = false;
                }

                arcPool.Add(new ActiveArc
                {
                    line = lr,
                    flashLight = flash,
                    endTime = -10f
                });
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            EnsureInitialized();
            if (jaeger == null) return;

            // Keep simulation space clean if underwater
            if (sparkSystem != null)
            {
                bool deepUnderwater = WaterSplashManager.Instance != null && WaterSplashManager.Instance.IsDeepUnderwater;
                var main = sparkSystem.main;
                main.gravityModifier = deepUnderwater ? 0.30f : 1.05f;
            }

            UpdateActiveArcs();

            if (PacificRimUI.MainMenuUI.IsMenuOpen || jaeger.isCutsceneActive || jaeger.IsDead)
            {
                if (lastTier != DamageTier.None)
                {
                    DisableAllArcs();
                    lastTier = DamageTier.None;
                }
                return;
            }

            float maxHp = Mathf.Max(1f, jaeger.maxHealth);
            float hpRatio = Mathf.Clamp01(jaeger.currentHealth / maxHp);

            DamageTier tier = DamageTier.None;
            if (hpRatio < 0.10f) tier = DamageTier.Critical;
            else if (hpRatio < 0.40f) tier = DamageTier.Moderate;
            else if (hpRatio < 0.70f) tier = DamageTier.Light;

            if (tier == DamageTier.None)
            {
                if (lastTier != DamageTier.None)
                {
                    DisableAllArcs();
                    lastTier = DamageTier.None;
                }
                return;
            }

            // Reset timers cleanly on entering a new damage tier so transitions feel immediate
            if (tier != lastTier)
            {
                if (lastTier == DamageTier.None)
                {
                    nextSparkBurstTime = Time.time + 0.08f;
                    nextArcTime = Time.time + 0.35f;
                    nextAllJointSparkTime = Time.time + 0.05f;
                }
                else if (tier > lastTier)
                {
                    nextSparkBurstTime = Mathf.Min(nextSparkBurstTime, Time.time + 0.10f);
                    if (tier == DamageTier.Critical)
                    {
                        nextArcTime = Mathf.Min(nextArcTime, Time.time + 0.15f);
                        nextAllJointSparkTime = Time.time + 0.03f;
                    }
                }
                lastTier = tier;
            }

            UpdateSparkEmissions(tier);
            UpdateElectricCrackles(tier);
        }

        private void UpdateSparkEmissions(DamageTier tier)
        {
            if (sparkSystem == null || allJoints.Count == 0) return;

            float now = Time.time;

            if (tier == DamageTier.Light)
            {
                // < 70% HP: barely sparking (sparse occasional small burst from primary joints)
                if (now >= nextSparkBurstTime)
                {
                    nextSparkBurstTime = now + Random.Range(1.15f, 2.25f);
                    JointSocket socket = PickRandomSocket(primaryJoints);
                    EmitJointSparks(socket, Random.Range(4, 8), speedMin: 4.5f, speedMax: 10.5f, sizeMin: 0.14f, sizeMax: 0.28f, includeElectricTint: false);
                }
            }
            else if (tier == DamageTier.Moderate)
            {
                // < 40% HP: noticeable increase in sparks across torso, arms & thighs
                if (now >= nextSparkBurstTime)
                {
                    nextSparkBurstTime = now + Random.Range(0.34f, 0.68f);
                    JointSocket socket = PickRandomSocket(moderateJoints);
                    EmitJointSparks(socket, Random.Range(8, 15), speedMin: 6.0f, speedMax: 14.5f, sizeMin: 0.16f, sizeMax: 0.34f, includeElectricTint: false);

                    if (Random.value < 0.35f)
                    {
                        JointSocket second = PickRandomSocket(moderateJoints);
                        EmitJointSparks(second, Random.Range(4, 8), speedMin: 5.0f, speedMax: 11.5f, sizeMin: 0.14f, sizeMax: 0.28f, includeElectricTint: false);
                    }
                }
            }
            else if (tier == DamageTier.Critical)
            {
                // < 10% HP: sparking on ALL joints continuously (round-robin + multi-joint surges)
                if (now >= nextAllJointSparkTime)
                {
                    nextAllJointSparkTime = now + Random.Range(0.06f, 0.11f);

                    int jointsPerTick = Mathf.Min(3, allJoints.Count);
                    for (int i = 0; i < jointsPerTick; i++)
                    {
                        JointSocket socket = allJoints[jointRoundRobinIndex % allJoints.Count];
                        jointRoundRobinIndex++;
                        EmitJointSparks(socket, Random.Range(4, 9), speedMin: 6.5f, speedMax: 16.5f, sizeMin: 0.16f, sizeMax: 0.36f, includeElectricTint: true);
                    }
                }

                if (now >= nextSparkBurstTime)
                {
                    nextSparkBurstTime = now + Random.Range(0.28f, 0.55f);
                    JointSocket hotJoint = PickRandomSocket(allJoints);
                    EmitJointSparks(hotJoint, Random.Range(10, 16), speedMin: 8.5f, speedMax: 19.5f, sizeMin: 0.20f, sizeMax: 0.42f, includeElectricTint: true);
                }
            }
        }

        private void UpdateElectricCrackles(DamageTier tier)
        {
            if (allJoints.Count == 0) return;

            float now = Time.time;
            if (now < nextArcTime) return;

            if (tier == DamageTier.Light || tier == DamageTier.Moderate)
            {
                // < 70% and < 40% share the SAME electricity crackle rate ("barely" / "same amount of electricity crackles")
                nextArcTime = now + Random.Range(2.50f, 4.20f);
                List<JointSocket> pool = tier == DamageTier.Light ? primaryJoints : moderateJoints;
                SpawnElectricCrackle(pool, arcDuration: Random.Range(0.08f, 0.13f), widthScale: 0.85f, emitCyanSparks: true);
            }
            else if (tier == DamageTier.Critical)
            {
                // < 10% HP: a bit more electricity crackles across all joints
                nextArcTime = now + Random.Range(0.95f, 1.85f);
                SpawnElectricCrackle(allJoints, arcDuration: Random.Range(0.10f, 0.17f), widthScale: 1.15f, emitCyanSparks: true);

                if (Random.value < 0.40f)
                {
                    SpawnElectricCrackle(allJoints, arcDuration: Random.Range(0.08f, 0.14f), widthScale: 1.0f, emitCyanSparks: false);
                }
            }
        }

        private void SpawnElectricCrackle(List<JointSocket> pool, float arcDuration, float widthScale, bool emitCyanSparks)
        {
            if (pool == null || pool.Count == 0) return;

            int slot = -1;
            for (int i = 0; i < arcPool.Count; i++)
            {
                if (arcPool[i].line != null && !arcPool[i].line.enabled)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0) return;

            JointSocket a = PickRandomSocket(pool);
            JointSocket b = PickNearbyJoint(a, pool);

            Vector3 outA = ComputeOutwardNormal(a.bone != null ? a.bone.position : transform.position);
            Vector3 outB = ComputeOutwardNormal(b.bone != null ? b.bone.position : transform.position);
            Vector3 offA = outA * a.surfaceOffset + Random.insideUnitSphere * 0.35f;
            Vector3 offB = outB * b.surfaceOffset + Random.insideUnitSphere * 0.45f;

            if (a.bone == b.bone)
            {
                offB = Quaternion.Euler(Random.Range(-45f, 45f), Random.Range(-55f, 55f), 0f) * outA * (a.surfaceOffset + 0.35f) + Vector3.down * Random.Range(0.6f, 1.8f);
            }

            ActiveArc arc = arcPool[slot];
            arc.startJoint = a;
            arc.endJoint = b;
            arc.localOffsetA = a.bone != null ? a.bone.InverseTransformVector(offA) : offA;
            arc.localOffsetB = b.bone != null ? b.bone.InverseTransformVector(offB) : offB;
            arc.endTime = Time.time + arcDuration;
            arc.nextJitterTime = 0f;
            arc.line.startWidth = 0.15f * widthScale;
            arc.line.endWidth = 0.06f * widthScale;
            arc.line.enabled = true;

            if (arc.flashLight != null)
            {
                arc.flashLight.enabled = true;
                arc.flashLight.intensity = 34f * widthScale;
            }

            arcPool[slot] = arc;
            RefreshArcGeometry(ref arc);
            arcPool[slot] = arc;

            if (emitCyanSparks && a.bone != null)
            {
                EmitJointSparks(a, Random.Range(2, 5), speedMin: 5f, speedMax: 11f, sizeMin: 0.12f, sizeMax: 0.24f, includeElectricTint: true, forceElectricOnly: true);
            }

            if (Time.time >= nextSoftArcAudioTime)
            {
                nextSoftArcAudioTime = Time.time + 2.2f;
                Vector3 pos = a.bone != null ? a.bone.position : (transform.position + Vector3.up * 14f);
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayAtPosition("Laser Impact Light_6", pos, volume: 0.12f, pitchMin: 1.25f, pitchMax: 1.65f);
            }
        }

        private void UpdateActiveArcs()
        {
            float now = Time.time;
            for (int i = 0; i < arcPool.Count; i++)
            {
                ActiveArc arc = arcPool[i];
                if (arc.line == null || !arc.line.enabled) continue;

                if (now >= arc.endTime)
                {
                    arc.line.enabled = false;
                    if (arc.flashLight != null) arc.flashLight.enabled = false;
                    arcPool[i] = arc;
                    continue;
                }

                if (now >= arc.nextJitterTime)
                {
                    arc.nextJitterTime = now + 0.032f;
                    RefreshArcGeometry(ref arc);
                    arcPool[i] = arc;
                }
            }
        }

        private void RefreshArcGeometry(ref ActiveArc arc)
        {
            if (arc.line == null) return;

            Vector3 pA = arc.startJoint.bone != null
                ? arc.startJoint.bone.position + arc.startJoint.bone.TransformVector(arc.localOffsetA)
                : transform.position + Vector3.up * 12f;

            Vector3 pB = arc.endJoint.bone != null
                ? arc.endJoint.bone.position + arc.endJoint.bone.TransformVector(arc.localOffsetB)
                : pA + Vector3.down * 1.5f;

            Vector3 mid = (pA + pB) * 0.5f;
            Vector3 outward = ComputeOutwardNormal(mid);
            float span = Vector3.Distance(pA, pB);
            float jitterAmp = Mathf.Clamp(span * 0.18f, 0.22f, 0.72f);

            int count = arcPointBuffer.Length;
            for (int k = 0; k < count; k++)
            {
                float u = (float)k / (count - 1);
                Vector3 basePt = Vector3.Lerp(pA, pB, u);
                float env = Mathf.Sin(u * Mathf.PI);
                // Bow outward slightly so arcs trace the exterior armor rather than cutting straight through limbs
                Vector3 disp = outward * (env * jitterAmp * 0.65f) + Random.insideUnitSphere * (env * jitterAmp);
                arcPointBuffer[k] = basePt + disp;
            }

            arc.line.SetPositions(arcPointBuffer);

            if (arc.flashLight != null)
            {
                arc.flashLight.transform.position = mid + outward * 0.8f;
            }
        }

        private void DisableAllArcs()
        {
            for (int i = 0; i < arcPool.Count; i++)
            {
                ActiveArc arc = arcPool[i];
                if (arc.line != null) arc.line.enabled = false;
                if (arc.flashLight != null) arc.flashLight.enabled = false;
                arc.endTime = -10f;
                arcPool[i] = arc;
            }
        }

        private void EmitJointSparks(
            JointSocket socket,
            int count,
            float speedMin,
            float speedMax,
            float sizeMin,
            float sizeMax,
            bool includeElectricTint,
            bool forceElectricOnly = false)
        {
            if (sparkSystem == null || socket.bone == null) return;

            Vector3 bonePos = socket.bone.position;
            Vector3 outward = ComputeOutwardNormal(bonePos);
            Vector3 emitOrigin = bonePos + outward * socket.surfaceOffset + Random.insideUnitSphere * 0.22f;

            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Vector3.Slerp(outward, Random.onUnitSphere, Random.Range(0.25f, 0.78f));
                if (dir.y < -0.35f) dir.y = -dir.y * 0.35f;
                dir.Normalize();

                float speed = Random.Range(speedMin, speedMax);

                Color32 col;
                if (forceElectricOnly)
                {
                    col = Random.value < 0.4f ? Color.white : SparkElectricCyan;
                }
                else
                {
                    float r = Random.value;
                    if (includeElectricTint && r < 0.16f) col = SparkElectricCyan;
                    else if (r < 0.42f) col = SparkCore;
                    else if (r < 0.78f) col = SparkAmber;
                    else col = SparkHotOrange;
                }

                var ep = new ParticleSystem.EmitParams
                {
                    position = emitOrigin + Random.insideUnitSphere * 0.18f,
                    velocity = dir * speed + Vector3.up * Random.Range(0.5f, 3.2f),
                    startLifetime = Random.Range(0.18f, 0.46f),
                    startSize = Random.Range(sizeMin, sizeMax),
                    startColor = col,
                    applyShapeToPosition = false
                };
                sparkSystem.Emit(ep, 1);
            }
        }

        private Vector3 ComputeOutwardNormal(Vector3 worldPos)
        {
            Vector3 anchor;
            if (chestAnchor != null && spineAnchor != null && hipsAnchor != null)
            {
                float dChest = (worldPos - chestAnchor.position).sqrMagnitude;
                float dHips = (worldPos - hipsAnchor.position).sqrMagnitude;
                anchor = dChest < dHips ? chestAnchor.position : hipsAnchor.position;
            }
            else
            {
                anchor = transform.position + Vector3.up * 14f;
            }

            Vector3 dir = worldPos - anchor;
            if (dir.sqrMagnitude < 0.04f)
            {
                dir = transform.forward + Vector3.up * 0.2f + transform.right * Random.Range(-0.5f, 0.5f);
            }
            return dir.normalized;
        }

        private static JointSocket PickRandomSocket(List<JointSocket> pool)
        {
            if (pool == null || pool.Count == 0) return default;
            return pool[Random.Range(0, pool.Count)];
        }

        private static JointSocket PickNearbyJoint(JointSocket from, List<JointSocket> pool)
        {
            if (from.bone == null || pool == null || pool.Count <= 1) return from;

            JointSocket best = from;
            float bestDist = float.MaxValue;
            // Pick among close neighbors (with slight randomness so arcs don't always hit the same pair)
            for (int i = 0; i < pool.Count; i++)
            {
                JointSocket candidate = pool[i];
                if (candidate.bone == null || candidate.bone == from.bone) continue;
                float d = Vector3.Distance(from.bone.position, candidate.bone.position) * Random.Range(0.75f, 1.45f);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = candidate;
                }
            }
            return best;
        }

        private static Texture2D CreateSoftSparkTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "JaegerDamageSparkTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float falloff = r >= 1f ? 0f : Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(falloff * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        private static Material CreateSparkMaterial(Texture2D tex)
        {
            Shader urpParticles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpParticles != null)
            {
                Material mat = new Material(urpParticles) { name = "JaegerDamageSparkMat" };
                mat.SetTexture("_BaseMap", tex);
                mat.mainTexture = tex;
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 2f); // Additive
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 80;
                return mat;
            }

            Shader splashShader = Shader.Find("PacificRim/SplashParticle") ?? Shader.Find("Sprites/Default");
            Material fallback = new Material(splashShader) { name = "JaegerDamageSparkMat" };
            fallback.mainTexture = tex;
            if (fallback.HasProperty("_TintColor")) fallback.SetColor("_TintColor", Color.white);
            if (fallback.HasProperty("_InvFade")) fallback.SetFloat("_InvFade", 0.15f);
            if (fallback.HasProperty("_LightInfluence")) fallback.SetFloat("_LightInfluence", 0f);
            return fallback;
        }

        private static Material CreateArcMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default");
            Material mat = new Material(shader) { name = "JaegerDamageArcMat" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1.6f, 2.4f, 3.6f, 1f));
            mat.color = Color.white;
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 90;
            return mat;
        }
    }
}
