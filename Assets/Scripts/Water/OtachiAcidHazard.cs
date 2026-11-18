using System.Collections.Generic;
using UnityEngine;
using PacificRimAudio;

namespace PacificRimWater
{
    /// <summary>
    /// Acid projectile launched by Otachi toward Gipsy Danger that splatters into a corrosive
    /// surface pool upon hitting the Jaeger, ground, or ocean surface.
    /// </summary>
    public class OtachiAcidSpitProjectile : MonoBehaviour
    {
        private Vector3 velocity;
        private float gravity = 28f;
        private float damage = 18f;
        private float poolRadius = 8.0f;
        private float poolDuration = 8.5f;
        private float lifetime = 4.5f;
        private float elapsed;
        private SimpleJaegerController targetJaeger;
        private Transform ownerTransform;
        private bool detonated;

        private static Material sharedAcidMat;
        private static Mesh sharedSphereMesh;

        public static void Spawn(Vector3 origin, Vector3 targetPos, SimpleJaegerController jaeger, Transform owner, float dmg = 18f, float arcHeight = 6.5f, float flightTime = 0.85f, float poolRad = 8.0f, float poolDur = 8.5f)
        {
            EnsureSharedAssets();

            GameObject go = new GameObject("Otachi_AcidSpit");
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * 2.1f;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = sharedSphereMesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = sharedAcidMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var lightGo = new GameObject("AcidGlow");
            lightGo.transform.SetParent(go.transform, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.18f, 1.0f, 0.55f, 1f);
            l.intensity = 16f;
            l.range = 18f;
            l.shadows = LightShadows.None;

            var proj = go.AddComponent<OtachiAcidSpitProjectile>();
            proj.targetJaeger = jaeger;
            proj.ownerTransform = owner;
            proj.damage = dmg;
            proj.poolRadius = poolRad;
            proj.poolDuration = poolDur;
            proj.velocity = ComputeBallisticVelocity(origin, targetPos, flightTime, proj.gravity, arcHeight);
        }

        private static Vector3 ComputeBallisticVelocity(Vector3 from, Vector3 to, float flightTime, float g, float extraArc)
        {
            float t = Mathf.Max(0.35f, flightTime);
            Vector3 diff = to - from;
            Vector3 horiz = new Vector3(diff.x, 0f, diff.z) / t;
            float vy = (diff.y + 0.5f * g * t * t) / t + extraArc * 0.25f;
            return new Vector3(horiz.x, vy, horiz.z);
        }

        private void Update()
        {
            if (detonated) return;

            float dt = Time.deltaTime;
            elapsed += dt;
            if (elapsed >= lifetime)
            {
                Detonate(transform.position);
                return;
            }

            velocity.y -= gravity * dt;
            Vector3 nextPos = transform.position + velocity * dt;
            transform.position = nextPos;
            transform.Rotate(240f * dt, 310f * dt, 0f, Space.Self);

            // Check direct hit against target Jaeger
            if (targetJaeger != null && !targetJaeger.IsDead)
            {
                Vector3 jaegerBody = targetJaeger.transform.position + Vector3.up * 12.5f;
                if (Vector3.Distance(nextPos, jaegerBody) <= 8.2f)
                {
                    Vector3 flatDir = targetJaeger.transform.position - (ownerTransform != null ? ownerTransform.position : nextPos);
                    flatDir.y = 0f;
                    Vector3 kb = flatDir.sqrMagnitude > 0.01f ? flatDir.normalized * 12f : Vector3.forward * 12f;
                    targetJaeger.TakeDamage(damage, kb, isHeavy: false, hitPoint: nextPos);
                    targetJaeger.ApplyAcidCorrosion(7.5f);
                    Detonate(new Vector3(nextPos.x, GetSurfaceY(nextPos), nextPos.z));
                    return;
                }
            }

            // Check water / ground surface impact
            float surfaceY = GetSurfaceY(nextPos);
            if (nextPos.y <= surfaceY + 0.6f)
            {
                nextPos.y = surfaceY;
                Detonate(nextPos);
            }
        }

        private void Detonate(Vector3 pos)
        {
            if (detonated) return;
            detonated = true;

            pos.y = GetSurfaceY(pos);
            OtachiAcidPool.Spawn(pos, targetJaeger, radius: poolRadius, duration: poolDuration, tickDamage: 8.5f);

            if (WaterSplashManager.Instance != null)
            {
                WaterSplashManager.Instance.SpawnFootstepSplash(pos, 1.5f);
            }

            Destroy(gameObject);
        }

        internal static float GetDryGroundY(Vector3 pos)
        {
            RaycastHit[] hits = Physics.RaycastAll(pos + Vector3.up * 40f, Vector3.down, 90f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);
            float bestY = float.NegativeInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider != null && hits[i].collider.CompareTag("Ground"))
                {
                    if (hits[i].point.y > bestY)
                    {
                        bestY = hits[i].point.y;
                    }
                }
            }
            return bestY > float.NegativeInfinity ? bestY : 0f;
        }

        private static float GetSurfaceY(Vector3 pos)
        {
            if (WaterSystem.IsDryLandAt(pos, out float dryY))
            {
                return dryY + 0.08f;
            }
            if (WaterSystem.Instance != null)
            {
                return WaterSystem.Instance.GetWaterHeight(pos) + 0.08f;
            }
            return GetDryGroundY(pos) + 0.08f;
        }

        private static void EnsureSharedAssets()
        {
            if (sharedSphereMesh == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sharedSphereMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(tmp);
            }

            if (sharedAcidMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Standard");
                sharedAcidMat = new Material(shader)
                {
                    name = "Otachi_AcidGlobule_Mat"
                };
                Color baseCol = new Color(0.18f, 0.95f, 0.45f, 1f);
                sharedAcidMat.color = baseCol;
                if (sharedAcidMat.HasProperty("_BaseColor")) sharedAcidMat.SetColor("_BaseColor", baseCol);
                sharedAcidMat.EnableKeyword("_EMISSION");
                if (sharedAcidMat.HasProperty("_EmissionColor"))
                    sharedAcidMat.SetColor("_EmissionColor", new Color(0.45f, 4.2f, 1.6f, 1f));
            }
        }
    }

    /// <summary>
    /// Lingering corrosive acid pool spawned on the water or ground during Otachi's retreats
    /// and acid spit impacts. Deals periodic corrosive damage to Gipsy Danger if standing inside.
    /// </summary>
    public class OtachiAcidPool : MonoBehaviour
    {
        private static readonly List<OtachiAcidPool> ActivePools = new List<OtachiAcidPool>();
        private const int MaxConcurrentPools = 14;

        private float radius = 8.0f;
        private float duration = 7.5f;
        private float tickDamage = 9.0f;
        private float tickInterval = 0.65f;

        private float elapsed;
        private float tickTimer;
        private SimpleJaegerController targetJaeger;
        private Material instanceMat;
        private Light poolLight;
        private Vector3 baseScale;

        private static Mesh sharedDiscMesh;
        private static Material sharedPoolMat;
        private static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int IdEmissionColor = Shader.PropertyToID("_EmissionColor");

        public static void Spawn(Vector3 worldPos, SimpleJaegerController jaeger, float radius = 8.0f, float duration = 7.5f, float tickDamage = 9.0f)
        {
            EnsureSharedAssets();

            while (ActivePools.Count >= MaxConcurrentPools)
            {
                var oldest = ActivePools[0];
                ActivePools.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }

            float surfaceY = WaterSystem.IsDryLandAt(worldPos, out float dryY)
                ? dryY + 0.12f
                : (WaterSystem.Instance != null
                    ? WaterSystem.Instance.GetWaterHeight(worldPos) + 0.12f
                    : OtachiAcidSpitProjectile.GetDryGroundY(worldPos) + 0.12f);

            GameObject go = new GameObject("Otachi_AcidPool");
            go.transform.position = new Vector3(worldPos.x, surfaceY, worldPos.z);
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float diameter = radius * 2f;
            go.transform.localScale = new Vector3(diameter, 0.14f, diameter);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = sharedDiscMesh;

            var mr = go.AddComponent<MeshRenderer>();
            Material instMat = new Material(sharedPoolMat);
            mr.sharedMaterial = instMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var lightGo = new GameObject("AcidPoolLight");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 12f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.15f, 1.0f, 0.50f, 1f);
            l.intensity = 22f;
            l.range = radius * 2.4f;
            l.shadows = LightShadows.None;

            // Rising acid vapor particles
            var psGo = new GameObject("AcidVapor");
            psGo.transform.SetParent(go.transform, false);
            psGo.transform.localPosition = Vector3.up * 0.5f;
            var ps = psGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.4f;
            main.startSpeed = 3.2f;
            main.startSize = 1.6f;
            main.startColor = new Color(0.28f, 1.0f, 0.48f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.45f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var psRend = psGo.GetComponent<ParticleSystemRenderer>();
            if (psRend != null)
            {
                psRend.sharedMaterial = instMat;
            }

            var pool = go.AddComponent<OtachiAcidPool>();
            pool.targetJaeger = jaeger;
            pool.radius = radius;
            pool.duration = duration;
            pool.tickDamage = tickDamage;
            pool.instanceMat = instMat;
            pool.poolLight = l;
            pool.baseScale = go.transform.localScale;
            ActivePools.Add(pool);
        }

        private void OnDestroy()
        {
            ActivePools.Remove(this);
            if (instanceMat != null) Destroy(instanceMat);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            if (elapsed >= duration)
            {
                Destroy(gameObject);
                return;
            }

            // Track water wave height so pool floats cleanly on harbor swells
            Vector3 p = transform.position;
            if (!WaterSystem.IsDryLandAt(p, out _) && WaterSystem.Instance != null)
            {
                p.y = WaterSystem.Instance.GetWaterHeight(p) + 0.10f;
                transform.position = p;
            }

            float fadeIn = Mathf.Clamp01(elapsed / 0.35f);
            float fadeOut = Mathf.Clamp01((duration - elapsed) / 1.4f);
            float alpha = fadeIn * fadeOut;

            float pulse = 0.85f + 0.15f * Mathf.Sin(elapsed * 5.5f);
            transform.localScale = new Vector3(
                baseScale.x * (0.35f + 0.65f * fadeIn) * (0.96f + 0.04f * pulse),
                baseScale.y,
                baseScale.z * (0.35f + 0.65f * fadeIn) * (0.96f + 0.04f * pulse));

            if (instanceMat != null)
            {
                Color c = new Color(0.14f, 0.96f, 0.42f, 0.68f * alpha);
                instanceMat.color = c;
                if (instanceMat.HasProperty(IdBaseColor)) instanceMat.SetColor(IdBaseColor, c);
                if (instanceMat.HasProperty(IdEmissionColor))
                    instanceMat.SetColor(IdEmissionColor, new Color(0.28f, 3.4f, 1.15f, 1f) * (alpha * pulse));
            }

            if (poolLight != null)
            {
                poolLight.intensity = 20f * alpha * pulse;
            }

            // Periodic corrosive DoT if Gipsy stands inside the acid pool
            tickTimer -= dt;
            if (tickTimer <= 0f)
            {
                tickTimer = tickInterval;
                if (targetJaeger == null) targetJaeger = SimpleJaegerController.Instance;
                if (targetJaeger != null && !targetJaeger.IsDead && !PacificRimUI.MainMenuUI.IsMenuOpen)
                {
                    Vector2 delta = new Vector2(
                        targetJaeger.transform.position.x - p.x,
                        targetJaeger.transform.position.z - p.z);
                    if (delta.magnitude <= radius)
                    {
                        Vector3 hitPt = targetJaeger.transform.position + Vector3.up * 3.5f;
                        targetJaeger.TakeCorrosivePoolDamage(tickDamage, 6.5f);
                        PacificRimAudioManager.Instance?.PlayArmorCreak(hitPt, 0.38f);
                    }
                }
            }
        }

        private static void EnsureSharedAssets()
        {
            if (sharedDiscMesh == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                sharedDiscMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(tmp);
            }

            if (sharedPoolMat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Standard");
                sharedPoolMat = new Material(shader)
                {
                    name = "Otachi_AcidPool_SharedMat"
                };
                Color c = new Color(0.14f, 0.96f, 0.42f, 0.68f);
                sharedPoolMat.color = c;
                if (sharedPoolMat.HasProperty("_BaseColor")) sharedPoolMat.SetColor("_BaseColor", c);
                if (sharedPoolMat.HasProperty("_Surface")) sharedPoolMat.SetFloat("_Surface", 1f);
                sharedPoolMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                sharedPoolMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                sharedPoolMat.SetInt("_ZWrite", 0);
                sharedPoolMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                sharedPoolMat.EnableKeyword("_EMISSION");
                sharedPoolMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                if (sharedPoolMat.HasProperty("_EmissionColor"))
                    sharedPoolMat.SetColor("_EmissionColor", new Color(0.28f, 3.4f, 1.15f, 1f));
            }
        }
    }
}
