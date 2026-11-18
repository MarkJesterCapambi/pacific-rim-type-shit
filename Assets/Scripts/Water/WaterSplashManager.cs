using System.Collections.Generic;
using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Realistic ocean spray for a colossal wading Jaeger.
    /// Uses four soft-textured particle layers (stretched round droplets, rising mist, surface foam patches,
    /// and floating bubbles) instead of hard-edged quads. Foam and bubbles ride the live wave surface,
    /// droplets re-enter the water as tiny bubble bursts, and everything is pushed out of the Jaeger's body
    /// via <see cref="JaegerSplashClipGuard"/> so particles never clip into the armor.
    /// </summary>
    [ExecuteAlways]
    public class WaterSplashManager : MonoBehaviour
    {
        public static WaterSplashManager Instance { get; private set; }

        [Header("Procedural Splash Settings")]
        [Tooltip("Base number of droplets per footstep impact (scaled by impact size).")]
        public int splashParticleCount = 34;
        [Tooltip("Vertical launch velocity of water spray.")]
        public float splashUpwardForce = 9.0f;
        [Tooltip("Horizontal radius of a footstep splash crown.")]
        public float splashSpread = 3.2f;

        [Header("Realism")]
        [Tooltip("Gravity multiplier for droplets.")]
        public float dropletGravity = 1.0f;
        [Tooltip("Chance a falling droplet leaves a bubble/foam speck where it re-enters the sea.")]
        [Range(0f, 1f)]
        public float dropletImpactFoamChance = 0.35f;
        [Tooltip("When true (or when WaterSystem is absent), emits sandy seabed silt plumes and rising bubbles instead of surface splashes.")]
        public bool isDeepUnderwaterMode = false;
        [Tooltip("When true (e.g. City Highway combat), suppresses water droplets/foam/bubbles and emits urban asphalt dust & sparks instead.")]
        public bool isDryLandMode = false;

        public bool IsDeepUnderwater => !isDryLandMode && (isDeepUnderwaterMode || WaterSystem.Instance == null);
        private bool appliedUnderwaterSiltTint;

        private ParticleSystem droplets;
        private ParticleSystem mist;
        private ParticleSystem foam;
        private ParticleSystem bubbles;
        private ParticleSystem sparks;
        private ParticleSystem volumetricMist;

        private ParticleSystem.Particle[] dropletBuf;
        private ParticleSystem.Particle[] mistBuf;
        private ParticleSystem.Particle[] foamBuf;
        private ParticleSystem.Particle[] bubbleBuf;
        private ParticleSystem.Particle[] volumetricMistBuf;
        private readonly List<Vector3> impactPoints = new List<Vector3>(32);

        private ParticleSystem.EmitParams emit;
        private JaegerSplashClipGuard guard;
        private bool built;

        void Awake()
        {
            if (Instance == null || Instance == this)
                Instance = this;

            EnsureSystems();
        }

        void OnEnable()
        {
            if (Instance == null || Instance == this)
                Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        #region Particle System Construction

        private void EnsureSystems()
        {
            if (built && droplets != null && mist != null && foam != null && bubbles != null && sparks != null && volumetricMist != null)
            {
                if (IsDeepUnderwater && !appliedUnderwaterSiltTint)
                {
                    UpdateUnderwaterSiltTint();
                }
                return;
            }

            // Remove any legacy single-system child created by older versions
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (c.name == "ProceduralOceanSplashParticles")
                {
                    if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
                }
            }

            Material dropMat = MakeMaterial("Splash_Droplet", SplashTextures.SoftDroplet(), new Color(0.92f, 0.97f, 1.0f, 0.85f), 0.35f, 0.45f);
            Material mistMat = MakeMaterial("Splash_Mist", SplashTextures.Puff(), new Color(0.80f, 0.87f, 0.93f, 0.55f), 0.25f, 0.55f);
            Material foamMat = MakeMaterial("Splash_Foam", SplashTextures.FoamPatch(), new Color(0.93f, 0.97f, 1.0f, 0.95f), 0.10f, 0.50f);
            Material bubbleMat = MakeMaterial("Splash_Bubble", SplashTextures.Bubble(), new Color(0.95f, 0.99f, 1.0f, 0.95f), 0.10f, 0.40f);

            droplets = BuildSystem("SplashDroplets", dropMat, ParticleSystemRenderMode.Stretch, 1600, dropletGravity);
            var dr = droplets.GetComponent<ParticleSystemRenderer>();
            dr.velocityScale = 0.045f;
            dr.lengthScale = 1.7f;
            dr.cameraVelocityScale = 0f;
            AddAlphaFade(droplets, 0.04f, 0.65f);
            AddSizeCurve(droplets, 1.0f, 0.55f);

            mist = BuildSystem("SplashMist", mistMat, ParticleSystemRenderMode.Billboard, 400, -0.03f);
            AddAlphaFade(mist, 0.18f, 0.45f);
            AddSizeCurve(mist, 0.55f, 1.7f);
            var mistVel = mist.limitVelocityOverLifetime;
            mistVel.enabled = true;
            mistVel.limit = 2.5f;
            mistVel.dampen = 0.18f;

            foam = BuildSystem("SplashFoam", foamMat, ParticleSystemRenderMode.HorizontalBillboard, 3500, 0f);
            AddAlphaFade(foam, 0.08f, 0.65f);
            AddSizeCurve(foam, 0.6f, 1.45f);

            bubbles = BuildSystem("SplashBubbles", bubbleMat, ParticleSystemRenderMode.Billboard, 5000, 0f);
            AddAlphaFade(bubbles, 0.06f, 0.75f);
            AddSizeCurve(bubbles, 0.85f, 0.95f);

            Material sparkMat = MakeMaterial("Splash_Sparks", SplashTextures.SoftDroplet(), Color.white, 0.2f, 0f);
            sparks = BuildSystem("CombatSparks", sparkMat, ParticleSystemRenderMode.Stretch, 1200, 1.6f);
            var sr = sparks.GetComponent<ParticleSystemRenderer>();
            sr.velocityScale = 0.065f;
            sr.lengthScale = 2.6f;
            sr.cameraVelocityScale = 0f;
            AddAlphaFade(sparks, 0.02f, 0.45f);
            AddSizeCurve(sparks, 1.0f, 0.25f);

            // Volumetric Mist Cloud Layer for giant titan impacts (footsteps & cutscene landings)
            // Soft, airy, non-intrusive volumetric atmospheric mist
            Material volMistMat = MakeMaterial("Splash_VolumetricMist", SplashTextures.VolumetricMistPuff(), new Color(0.86f, 0.92f, 0.98f, 0.16f), 0.28f, 0.85f);
            volumetricMist = BuildSystem("SplashVolumetricMist", volMistMat, ParticleSystemRenderMode.Billboard, 1500, -0.015f);
            AddAlphaFade(volumetricMist, 0.03f, 0.16f);
            AddSizeCurve(volumetricMist, 0.65f, 3.2f);
            var vVol = volumetricMist.limitVelocityOverLifetime;
            vVol.enabled = true;
            vVol.limit = 1.8f;
            vVol.dampen = 0.28f;
            var rotOverLife = volumetricMist.rotationOverLifetime;
            rotOverLife.enabled = true;
            rotOverLife.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

            built = true;
            if (IsDeepUnderwater)
            {
                UpdateUnderwaterSiltTint();
            }
        }

        private void UpdateUnderwaterSiltTint()
        {
            appliedUnderwaterSiltTint = true;
            ApplyParticleMaterialTint(mist, new Color(0.24f, 0.27f, 0.30f, 0.58f), 0.75f);
            ApplyParticleMaterialTint(volumetricMist, new Color(0.26f, 0.29f, 0.32f, 0.28f), 0.90f);
        }

        private static void ApplyParticleMaterialTint(ParticleSystem ps, Color tint, float lightInfluence)
        {
            if (ps == null) return;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null || r.sharedMaterial == null) return;
            if (r.sharedMaterial.HasProperty("_TintColor")) r.sharedMaterial.SetColor("_TintColor", tint);
            else r.sharedMaterial.color = tint;
            if (r.sharedMaterial.HasProperty("_LightInfluence")) r.sharedMaterial.SetFloat("_LightInfluence", lightInfluence);
        }

        private Material MakeMaterial(string matName, Texture2D tex, Color tint, float invFade, float lightInfluence)
        {
            Shader shader = Shader.Find("PacificRim/SplashParticle");
            Material mat;
            if (shader != null)
            {
                mat = new Material(shader) { name = matName };
                mat.SetTexture("_MainTex", tex);
                mat.SetColor("_TintColor", tint);
                mat.SetFloat("_InvFade", invFade);
                mat.SetFloat("_LightInfluence", lightInfluence);
            }
            else
            {
                shader = Shader.Find("Sprites/Default");
                mat = new Material(shader) { name = matName };
                mat.mainTexture = tex;
                mat.color = tint;
            }
            return mat;
        }

        private ParticleSystem BuildSystem(string systemName, Material mat, ParticleSystemRenderMode mode, int maxParticles, float gravity)
        {
            GameObject go = new GameObject(systemName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 60f;
            main.maxParticles = maxParticles;
            main.startLifetime = 1.5f;
            main.startSpeed = 0f;
            main.startSize = 0.4f;
            main.startColor = Color.white;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = false;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = mode;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.minParticleSize = 0f;
            r.maxParticleSize = 3f;
            r.alignment = ParticleSystemRenderSpace.View;

            ps.Play();
            return ps;
        }

        private static void AddAlphaFade(ParticleSystem ps, float fadeInEnd, float fadeOutStart)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.86f, 0.93f, 1f), 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, Mathf.Clamp(fadeInEnd, 0.01f, 0.5f)),
                    new GradientAlphaKey(0.8f, Mathf.Clamp(fadeOutStart, 0.3f, 0.95f)),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = g;
        }

        private static void AddSizeCurve(ParticleSystem ps, float start, float end)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, start);
            curve.AddKey(1f, end);
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        #endregion

        #region Per-Frame Simulation (surface riding, water re-entry, body clip guard)

        void LateUpdate()
        {
            if (!built) return;

            if (guard == null) guard = JaegerSplashClipGuard.Instance;
            if (guard == null) guard = FindAnyObjectByType<JaegerSplashClipGuard>();

            WaterSystem water = WaterSystem.Instance;

            UpdateDroplets(water);
            UpdateAirborne(mist, ref mistBuf);
            UpdateAirborne(volumetricMist, ref volumetricMistBuf);
            UpdateSurfaceRiders(foam, ref foamBuf, water, 0.07f, 1.6f);
            UpdateSurfaceRiders(bubbles, ref bubbleBuf, water, 0.04f, 1.2f);

            if (impactPoints.Count > 0)
            {
                for (int i = 0; i < impactPoints.Count; i++)
                {
                    EmitBubble(impactPoints[i], Random.Range(0.12f, 0.32f), Vector3.zero);
                }
                impactPoints.Clear();
            }
        }

        private void UpdateDroplets(WaterSystem water)
        {
            int count = droplets.particleCount;
            if (count == 0) return;

            EnsureBuffer(ref dropletBuf, droplets);
            int n = droplets.GetParticles(dropletBuf);

            float meanY = water != null ? water.MeanSurfaceY : float.NegativeInfinity;
            float ceilingY = water != null ? meanY + water.maxTotalWaveHeight + 1.0f : float.PositiveInfinity;

            for (int i = 0; i < n; i++)
            {
                Vector3 pos = dropletBuf[i].position;
                Vector3 vel = dropletBuf[i].velocity;

                if (JaegerSplashClipGuard.PushOutAll(ref pos, ref vel, false))
                {
                    dropletBuf[i].position = pos;
                    dropletBuf[i].velocity = vel;
                }

                if (water != null && vel.y < 0f && pos.y < ceilingY)
                {
                    float h = water.GetWaterHeight(pos);
                    if (pos.y <= h)
                    {
                        dropletBuf[i].remainingLifetime = 0f;
                        if (impactPoints.Count < 28 && Random.value < dropletImpactFoamChance)
                        {
                            impactPoints.Add(new Vector3(pos.x, h, pos.z));
                        }
                    }
                }
            }

            droplets.SetParticles(dropletBuf, n);
        }

        private void UpdateAirborne(ParticleSystem ps, ref ParticleSystem.Particle[] buf)
        {
            if (JaegerSplashClipGuard.ActiveGuards.Count == 0 || ps.particleCount == 0) return;

            EnsureBuffer(ref buf, ps);
            int n = ps.GetParticles(buf);
            bool changed = false;

            for (int i = 0; i < n; i++)
            {
                Vector3 pos = buf[i].position;
                Vector3 vel = buf[i].velocity;
                if (JaegerSplashClipGuard.PushOutAll(ref pos, ref vel, false))
                {
                    buf[i].position = pos;
                    buf[i].velocity = vel;
                    changed = true;
                }
            }

            if (changed) ps.SetParticles(buf, n);
        }

        private void UpdateSurfaceRiders(ParticleSystem ps, ref ParticleSystem.Particle[] buf, WaterSystem water, float yOffset, float drag)
        {
            if (ps.particleCount == 0) return;

            EnsureBuffer(ref buf, ps);
            int n = ps.GetParticles(buf);
            float damp = Mathf.Exp(-drag * Time.deltaTime);

            for (int i = 0; i < n; i++)
            {
                Vector3 pos = buf[i].position;
                Vector3 vel = buf[i].velocity;

                if (water != null)
                {
                    vel.y = 0f;
                    pos.y = water.GetWaterHeight(pos) + yOffset;
                }
                else if (ps == bubbles && IsDeepUnderwater)
                {
                    float sway = Mathf.Sin(Time.time * 3.8f + pos.x * 0.7f + pos.z * 0.5f);
                    float swayZ = Mathf.Cos(Time.time * 3.2f + pos.z * 0.8f - pos.x * 0.4f);
                    pos.y += (2.8f + (i % 5) * 0.55f) * Time.deltaTime;
                    pos.x += sway * 0.55f * Time.deltaTime;
                    pos.z += swayZ * 0.55f * Time.deltaTime;
                }
                else
                {
                    vel.y = 0f;
                }
                vel.x *= damp;
                vel.z *= damp;

                JaegerSplashClipGuard.PushOutAll(ref pos, ref vel, water != null);

                buf[i].position = pos;
                buf[i].velocity = vel;
            }

            ps.SetParticles(buf, n);
        }

        private static void EnsureBuffer(ref ParticleSystem.Particle[] buf, ParticleSystem ps)
        {
            int needed = ps.main.maxParticles;
            if (buf == null || buf.Length < needed) buf = new ParticleSystem.Particle[needed];
        }

        #endregion

        #region Emit Helpers

        private void EmitDroplet(Vector3 pos, Vector3 vel, float size)
        {
            JaegerSplashClipGuard.PushOutAll(ref pos, ref vel, false);

            emit.position = pos;
            emit.velocity = vel;
            emit.startSize = size;
            emit.startLifetime = Random.Range(1.1f, 2.0f);
            emit.startColor = new Color32(255, 255, 255, (byte)Random.Range(165, 255));
            emit.rotation = 0f;
            droplets.Emit(emit, 1);
        }

        private void EmitMist(Vector3 pos, Vector3 vel, float size, float alpha)
        {
            JaegerSplashClipGuard.PushOutAll(ref pos, ref vel, false);

            emit.position = pos;
            emit.velocity = vel;
            emit.startSize = size;
            emit.startLifetime = IsDeepUnderwater ? Random.Range(4.2f, 6.8f) : Random.Range(1.5f, 2.6f);
            emit.startColor = new Color(1f, 1f, 1f, alpha);
            emit.rotation = Random.Range(0f, 360f);
            mist.Emit(emit, 1);
        }

        public void EmitVolumetricMist(Vector3 pos, Vector3 vel, float size, float alpha, float lifetime = 3.6f)
        {
            if (volumetricMist == null) return;
            JaegerSplashClipGuard.PushOutAll(ref pos, ref vel, false);

            emit.position = pos;
            emit.velocity = vel;
            emit.startSize = size;
            emit.startLifetime = lifetime;
            emit.startColor = new Color(1f, 1f, 1f, alpha);
            emit.rotation = Random.Range(0f, 360f);
            emit.angularVelocity = Random.Range(-18f, 18f);
            volumetricMist.Emit(emit, 1);
        }

        private void EmitFoam(Vector3 pos, Vector3 vel, float size)
        {
            vel.y = 0f;
            JaegerSplashClipGuard.PushOutAll(ref pos, ref vel, true);

            emit.position = pos;
            emit.velocity = vel;
            emit.startSize = size;
            emit.startLifetime = Random.Range(4.5f, 8.5f);
            emit.startColor = new Color(1f, 1f, 1f, Random.Range(0.60f, 0.98f));
            emit.rotation = Random.Range(0f, 360f);
            foam.Emit(emit, 1);
        }

        private void EmitBubble(Vector3 pos, float size, Vector3 vel)
        {
            if (!IsDeepUnderwater) vel.y = 0f;
            JaegerSplashClipGuard.PushOutAll(ref pos, ref vel, !IsDeepUnderwater);

            emit.position = pos;
            emit.velocity = vel;
            emit.startSize = size;
            emit.startLifetime = Random.Range(4.0f, 7.5f);
            emit.startColor = new Color(1f, 1f, 1f, Random.Range(0.70f, 1f));
            emit.rotation = 0f;
            bubbles.Emit(emit, 1);
        }

        private static Vector3 Flat(Vector2 v) { return new Vector3(v.x, 0f, v.y); }

        #endregion

        #region Public Spawn API

        /// <summary>
        /// Continuously called while moving deep underwater (e.g. Operation Pitfall)
        /// to emit realistic micro-to-medium cavitation bubble trails behind both legs.
        /// </summary>
        public void SpawnUnderwaterWakeBubbles(Vector3 leftFootPos, Vector3 rightFootPos, Vector3 moveVelocity, float speed)
        {
            if (float.IsNaN(leftFootPos.x) || float.IsNaN(rightFootPos.x)) return;
            EnsureSystems();

            Vector3 moveDir = new Vector3(moveVelocity.x, 0f, moveVelocity.z);
            float hSpeed = moveDir.magnitude;
            if (hSpeed > 0.05f) moveDir /= hSpeed;
            else moveDir = transform.forward;

            Vector3 backDir = -moveDir;
            Vector3 sideDir = Vector3.Cross(Vector3.up, moveDir);
            float speedFactor = Mathf.Clamp(speed / 10.0f, 0.6f, 1.8f);

            int perLegCount = Mathf.RoundToInt(Random.Range(7, 12) * speedFactor);
            for (int leg = 0; leg < 2; leg++)
            {
                Vector3 basePos = leg == 0 ? leftFootPos : rightFootPos;
                for (int i = 0; i < perLegCount; i++)
                {
                    bool isMicro = Random.value < 0.72f;
                    Vector3 pos = basePos
                        + backDir * Random.Range(0.2f, 3.2f)
                        + sideDir * Random.Range(-1.8f, 1.8f)
                        + Vector3.up * Random.Range(0.2f, 5.5f);
                    Vector3 vel = backDir * Random.Range(0.5f, 2.4f)
                        + sideDir * Random.Range(-1.2f, 1.2f)
                        + Vector3.up * Random.Range(1.8f, 5.2f);
                    float sz = isMicro ? Random.Range(0.05f, 0.16f) : Random.Range(0.17f, 0.34f);
                    EmitBubble(pos, sz * Mathf.Lerp(0.85f, 1.25f, speedFactor * 0.5f), vel);
                }
            }
        }

        /// <summary>
        /// Continuously called while the Jaeger wades through ocean water to leave a wide,
        /// dense, persistent trail of frothing bubbles and floating seafoam behind both legs.
        /// </summary>
        public void SpawnWadingWakeTrail(Vector3 leftWaterline, Vector3 rightWaterline, Vector3 moveVelocity, float legWidth, float speed)
        {
            if (float.IsNaN(leftWaterline.x) || float.IsNaN(rightWaterline.x)) return;
            EnsureSystems();

            Vector3 moveDir = new Vector3(moveVelocity.x, 0f, moveVelocity.z);
            float hSpeed = moveDir.magnitude;
            if (hSpeed > 0.05f) moveDir /= hSpeed;
            else moveDir = transform.forward;

            Vector3 backDir = -moveDir;
            Vector3 sideDir = Vector3.Cross(Vector3.up, moveDir); // lateral spread vector
            float speedFactor = Mathf.Clamp(speed / 7.0f, 0.65f, 1.45f);

            // 1. Left leg trailing wake bubbles
            int leftBubbleCount = Random.Range(5, 9);
            for (int i = 0; i < leftBubbleCount; i++)
            {
                float lateral = Random.Range(-legWidth * 0.55f, legWidth * 0.55f);
                float distBack = Random.Range(0.4f, 2.4f);
                Vector3 pos = leftWaterline + backDir * distBack + sideDir * lateral;
                Vector3 vel = backDir * Random.Range(0.6f, 2.2f) + sideDir * (lateral * 0.4f);
                EmitBubble(pos, Random.Range(0.18f, 0.58f) * speedFactor, vel);
            }

            // 2. Right leg trailing wake bubbles
            int rightBubbleCount = Random.Range(5, 9);
            for (int i = 0; i < rightBubbleCount; i++)
            {
                float lateral = Random.Range(-legWidth * 0.55f, legWidth * 0.55f);
                float distBack = Random.Range(0.4f, 2.4f);
                Vector3 pos = rightWaterline + backDir * distBack + sideDir * lateral;
                Vector3 vel = backDir * Random.Range(0.6f, 2.2f) + sideDir * (lateral * 0.4f);
                EmitBubble(pos, Random.Range(0.18f, 0.58f) * speedFactor, vel);
            }

            // 3. Central turbulent churn wake between the two legs (vortex cavitation)
            Vector3 centerPos = (leftWaterline + rightWaterline) * 0.5f;
            int centerBubbleCount = Random.Range(7, 14);
            for (int i = 0; i < centerBubbleCount; i++)
            {
                float lateral = Random.Range(-legWidth * 0.75f, legWidth * 0.75f);
                float distBack = Random.Range(0.8f, 4.0f);
                Vector3 pos = centerPos + backDir * distBack + sideDir * lateral;
                Vector3 vel = backDir * Random.Range(0.4f, 1.8f) + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
                EmitBubble(pos, Random.Range(0.22f, 0.68f) * speedFactor, vel);
            }

            // 4. Persistent floating surface foam patches along the wake trail
            Vector3 foamPosL = leftWaterline + backDir * Random.Range(0.6f, 2.6f) + sideDir * Random.Range(-legWidth * 0.45f, legWidth * 0.45f);
            EmitFoam(foamPosL, backDir * Random.Range(0.4f, 1.5f), Random.Range(1.8f, 3.8f) * speedFactor);

            Vector3 foamPosR = rightWaterline + backDir * Random.Range(0.6f, 2.6f) + sideDir * Random.Range(-legWidth * 0.45f, legWidth * 0.45f);
            EmitFoam(foamPosR, backDir * Random.Range(0.4f, 1.5f), Random.Range(1.8f, 3.8f) * speedFactor);

            if (Random.value < 0.85f)
            {
                Vector3 foamPosC = centerPos + backDir * Random.Range(1.2f, 4.2f) + sideDir * Random.Range(-legWidth * 0.65f, legWidth * 0.65f);
                EmitFoam(foamPosC, backDir * Random.Range(0.3f, 1.2f), Random.Range(2.2f, 4.8f) * speedFactor);
            }
        }

        /// <summary>
        /// Footstep / strike splash: a crown of round, stretched droplets, a central jet, rising mist,
        /// and foam + bubbles that spread across the surface.
        /// </summary>
        private static bool IsDryLandAtPoint(Vector3 pos, WaterSystem water, out Vector3 surfacePos)
        {
            surfacePos = pos;
            if (Instance != null && Instance.isDryLandMode)
                return true;

            if (WaterSystem.IsDryLandAt(pos, out float dryGroundY))
            {
                surfacePos.y = dryGroundY + 0.05f;
                return true;
            }

            if (water != null)
            {
                surfacePos.y = water.GetWaterHeight(pos);
            }
            return false;
        }

        public void SpawnFootstepSplash(Vector3 position, float scale = 1.0f)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z)) return;
            EnsureSystems();

            scale = Mathf.Clamp(scale, 0.35f, 2.0f);
            float sizeBias = Mathf.Lerp(0.85f, 1.35f, Mathf.Clamp01(scale / 2f));

            if (IsDryLandAtPoint(position, WaterSystem.Instance, out Vector3 dryPos))
            {
                position = dryPos;
                int dustCount = 8 + Mathf.RoundToInt(6f * scale);
                for (int i = 0; i < dustCount; i++)
                {
                    Vector3 off = Flat(Random.insideUnitCircle * splashSpread * 0.65f * scale);
                    Vector3 vel = new Vector3(off.x, 0f, off.z) * 1.1f + Vector3.up * Random.Range(0.8f, 2.4f) * scale;
                    EmitMist(position + off + Vector3.up * 0.25f, vel, Random.Range(4.2f, 8.5f) * scale, Random.Range(0.24f, 0.42f));
                }

                int volDustCount = 5 + Mathf.RoundToInt(4f * scale);
                for (int i = 0; i < volDustCount; i++)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    Vector3 pos = position + radial * Random.Range(0.4f, splashSpread * 0.9f * scale) + Vector3.up * Random.Range(0.2f, 1.0f);
                    Vector3 vel = radial * (Random.Range(2.0f, 4.8f) * scale) + Vector3.up * (Random.Range(0.9f, 2.6f) * scale);
                    EmitVolumetricMist(pos, vel, Random.Range(5.5f, 10.5f) * scale, Random.Range(0.10f, 0.18f), Random.Range(2.4f, 3.8f));
                }
                return;
            }

            if (IsDeepUnderwater)
            {
                int siltCount = 14 + Mathf.RoundToInt(10f * scale);
                for (int i = 0; i < siltCount; i++)
                {
                    Vector3 off = Flat(Random.insideUnitCircle * splashSpread * 0.85f * scale);
                    Vector3 vel = new Vector3(off.x, 0f, off.z) * 1.15f + Vector3.up * Random.Range(1.6f, 4.2f) * scale;
                    EmitMist(position + off + Vector3.up * 0.35f, vel, Random.Range(7.5f, 15.0f) * scale, Random.Range(0.42f, 0.66f));
                }

                int underwaterVolCount = 16 + Mathf.RoundToInt(12f * scale);
                float underwaterSpread = splashSpread * 1.1f * scale;
                for (int i = 0; i < underwaterVolCount; i++)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    float dist = Random.Range(0.2f, 1.35f) * underwaterSpread;
                    Vector3 pos = position + radial * dist + Vector3.up * Random.Range(0.2f, 1.6f);
                    Vector3 vel = radial * (Random.Range(3.0f, 7.2f) * scale) + Vector3.up * (Random.Range(1.8f, 4.6f) * scale);
                    EmitVolumetricMist(pos, vel, Random.Range(11.0f, 22.0f) * scale, Random.Range(0.24f, 0.42f), Random.Range(5.0f, 8.2f));
                }

                int underwaterBubbleCount = 28 + Mathf.RoundToInt(24f * scale);
                for (int i = 0; i < underwaterBubbleCount; i++)
                {
                    bool isMicro = i % 3 != 0;
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    float r0 = Random.Range(0.2f, 2.8f) * splashSpread * 0.65f * scale;
                    Vector3 bubbleVel = radial * Random.Range(0.9f, 3.4f) + Vector3.up * Random.Range(1.8f, 6.4f);
                    float sz = isMicro ? Random.Range(0.05f, 0.16f) : Random.Range(0.18f, 0.40f);
                    EmitBubble(position + radial * r0 + Vector3.up * Random.Range(0.15f, 1.8f), sz * sizeBias, bubbleVel);
                }
                return;
            }

            int crown = Mathf.RoundToInt(splashParticleCount * scale);
            for (int i = 0; i < crown; i++)
            {
                bool jet = (i % 4) == 0;
                float ang = Random.Range(0f, Mathf.PI * 2f);
                Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));

                float r0 = jet ? Random.Range(0f, 0.35f) * splashSpread : Random.Range(0.55f, 1.0f) * splashSpread * 0.6f * scale;
                Vector3 pos = position + radial * r0 + Vector3.up * 0.06f;

                float up = splashUpwardForce * scale * (jet ? Random.Range(1.0f, 1.45f) : Random.Range(0.45f, 0.95f));
                float outward = jet ? Random.Range(0.3f, 1.2f) : Random.Range(2.5f, 5.8f) * scale;
                Vector3 vel = radial * outward + Vector3.up * up;

                EmitDroplet(pos, vel, Random.Range(0.22f, 0.62f) * sizeBias);
            }

            int mistCount = 2 + Mathf.RoundToInt(3f * scale);
            for (int i = 0; i < mistCount; i++)
            {
                Vector3 off = Flat(Random.insideUnitCircle * splashSpread * 0.4f * scale);
                Vector3 vel = new Vector3(off.x, 0f, off.z) * 0.8f + Vector3.up * Random.Range(1.2f, 3.2f) * scale;
                EmitMist(position + off + Vector3.up * 0.4f, vel, Random.Range(2.2f, 4.5f) * scale, Random.Range(0.35f, 0.6f));
            }

            // Colossal Volumetric Mist Cloud eruption for titan footstep impact (soft, airy, non-obscuring)
            int volMistCount = 6 + Mathf.RoundToInt(6f * scale);
            float baseMistSpread = splashSpread * 0.95f * scale;
            for (int i = 0; i < volMistCount; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                float dist = Random.Range(0.2f, 1.25f) * baseMistSpread;
                Vector3 pos = position + radial * dist + Vector3.up * Random.Range(0.2f, 1.5f);

                float outSpd = Random.Range(2.5f, 6.8f) * scale;
                float upSpd = Random.Range(1.8f, 4.8f) * scale;
                Vector3 vel = radial * outSpd + Vector3.up * upSpd;

                float pSize = Random.Range(5.5f, 9.8f) * scale;
                float pAlpha = Random.Range(0.10f, 0.20f);
                float pLife = Random.Range(2.8f, 4.2f);

                EmitVolumetricMist(pos, vel, pSize, pAlpha, pLife);
            }

            int foamCount = 3 + Mathf.RoundToInt(5f * scale);
            for (int i = 0; i < foamCount; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                float r0 = Random.Range(0.8f, 1.6f) * splashSpread * 0.5f * scale;
                EmitFoam(position + radial * r0, radial * Random.Range(1.2f, 3.2f) * scale, Random.Range(1.4f, 3.4f) * scale);
            }

            int bubbleCount = 5 + Mathf.RoundToInt(9f * scale);
            for (int i = 0; i < bubbleCount; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                float r0 = Random.Range(0.3f, 1.9f) * splashSpread * 0.5f * scale;
                EmitBubble(position + radial * r0, Random.Range(0.14f, 0.5f) * sizeBias, radial * Random.Range(0.4f, 2.2f));
            }
        }

        /// <summary>
        /// Erupts violent, high-velocity water droplets and spray kicking up into the air
        /// when the Jaeger unleashes heavy attacks, colossal punches, elbow rockets, or seismic stomps.
        /// </summary>
        public void SpawnHeavyMoveWaterKickup(Vector3 origin, Vector3 moveDirection, float intensity = 1.0f, bool isHeavy = false)
        {
            if (float.IsNaN(origin.x) || float.IsNaN(origin.y) || float.IsNaN(origin.z)) return;
            EnsureSystems();

            float clampedIntensity = Mathf.Clamp(intensity, 0.5f, 3.5f);
            Vector3 forward = new Vector3(moveDirection.x, 0f, moveDirection.z);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            else forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            bool dryAtPoint = IsDryLandAtPoint(origin, WaterSystem.Instance, out Vector3 surfacePos);

            if (dryAtPoint)
            {
                int dustPuffs = isHeavy ? 14 : 7;
                for (int i = 0; i < dustPuffs; i++)
                {
                    Vector3 off = forward * Random.Range(0.5f, 6.0f) + right * Random.Range(-3.5f, 3.5f) + Vector3.up * Random.Range(0.2f, 1.4f);
                    Vector3 vel = forward * Random.Range(2.5f, 7.5f) + right * Random.Range(-2.0f, 2.0f) + Vector3.up * Random.Range(1.2f, 3.8f);
                    EmitMist(surfacePos + off, vel, Random.Range(5.0f, 11.0f) * clampedIntensity, Random.Range(0.25f, 0.45f));
                }

                int volDustCount = isHeavy ? 12 : 6;
                for (int i = 0; i < volDustCount; i++)
                {
                    Vector3 off = forward * Random.Range(0.5f, 7.5f) + right * Random.Range(-4.0f, 4.0f) + Vector3.up * Random.Range(0.2f, 1.6f);
                    Vector3 vel = forward * Random.Range(3.0f, 9.0f) + right * Random.Range(-2.4f, 2.4f) + Vector3.up * Random.Range(1.2f, 4.0f);
                    EmitVolumetricMist(surfacePos + off, vel, Random.Range(7.0f, 14.0f) * clampedIntensity, Random.Range(0.12f, 0.22f), Random.Range(2.8f, 4.4f));
                }
                return;
            }

            if (IsDeepUnderwater)
            {
                int siltPuffs = isHeavy ? 28 : 16;
                for (int i = 0; i < siltPuffs; i++)
                {
                    Vector3 off = forward * Random.Range(0.5f, 7.5f) + right * Random.Range(-4.5f, 4.5f) + Vector3.up * Random.Range(0.4f, 2.0f);
                    Vector3 vel = forward * Random.Range(3.5f, 9.5f) + right * Random.Range(-2.5f, 2.5f) + Vector3.up * Random.Range(2.5f, 6.2f);
                    EmitMist(surfacePos + off, vel, Random.Range(8.5f, 18.0f) * clampedIntensity, Random.Range(0.46f, 0.74f));
                }

                int volCount = isHeavy ? 30 : 18;
                for (int i = 0; i < volCount; i++)
                {
                    Vector3 off = forward * Random.Range(0.5f, 10.5f) + right * Random.Range(-5.5f, 5.5f) + Vector3.up * Random.Range(0.3f, 2.2f);
                    Vector3 vel = forward * Random.Range(4.5f, 13.0f) + right * Random.Range(-3.0f, 3.0f) + Vector3.up * Random.Range(2.6f, 6.8f);
                    EmitVolumetricMist(surfacePos + off, vel, Random.Range(14.0f, 28.0f) * clampedIntensity, Random.Range(0.28f, 0.46f), Random.Range(5.8f, 9.2f));
                }

                int cavBubbles = isHeavy ? 78 : 42;
                for (int i = 0; i < cavBubbles; i++)
                {
                    bool isMicro = i % 3 != 0;
                    Vector3 off = forward * Random.Range(-1.5f, 9f) + right * Random.Range(-5f, 5f) + Vector3.up * Random.Range(0.2f, 4.5f);
                    Vector3 vel = forward * Random.Range(2f, 8.5f) + right * Random.Range(-3.5f, 3.5f) + Vector3.up * Random.Range(2.4f, 8.2f);
                    float sz = isMicro ? Random.Range(0.05f, 0.18f) : Random.Range(0.18f, 0.44f);
                    EmitBubble(surfacePos + off, sz * clampedIntensity, vel);
                }
                return;
            }

            int dropCount = isHeavy ? Mathf.RoundToInt(95 * clampedIntensity) : Mathf.RoundToInt(50 * clampedIntensity);
            float baseUp = isHeavy ? (splashUpwardForce * 2.1f) : (splashUpwardForce * 1.45f);
            float baseForward = isHeavy ? 18.0f : 12.0f;

            for (int i = 0; i < dropCount; i++)
            {
                // Disperse droplets in a driving forward cone + radial blowout
                float lateral = Random.Range(-3.5f, 3.5f) * (isHeavy ? 1.4f : 1.0f);
                float fwdOffset = Random.Range(-1.5f, 5.0f) * (isHeavy ? 1.4f : 1.0f);
                Vector3 spawnPos = surfacePos + forward * fwdOffset + right * lateral + Vector3.up * Random.Range(0.1f, 0.8f);

                float upVel = baseUp * Random.Range(0.75f, 1.55f) * clampedIntensity;
                float fwdVel = baseForward * Random.Range(0.6f, 1.45f) * clampedIntensity;
                float latVel = Random.Range(-7.0f, 7.0f) * (isHeavy ? 1.35f : 1.0f);

                Vector3 vel = forward * fwdVel + right * latVel + Vector3.up * upVel;
                float dropSize = Random.Range(0.35f, 0.95f) * (isHeavy ? 1.35f : 1.0f);

                EmitDroplet(spawnPos, vel, dropSize);
            }

            // Rising water mist clouds from kinetic ocean displacement
            int mistPuffs = isHeavy ? 10 : 5;
            for (int i = 0; i < mistPuffs; i++)
            {
                Vector3 off = forward * Random.Range(1f, 6f) + right * Random.Range(-3.5f, 3.5f) + Vector3.up * Random.Range(0.5f, 1.8f);
                Vector3 vel = forward * Random.Range(3f, 8f) + Vector3.up * Random.Range(2.5f, 6.0f);
                EmitMist(surfacePos + off, vel, Random.Range(3.5f, 7.5f) * clampedIntensity, Random.Range(0.4f, 0.75f));
            }

            int volMistHeavyCount = isHeavy ? 12 : 6;
            for (int i = 0; i < volMistHeavyCount; i++)
            {
                Vector3 off = forward * Random.Range(1f, 8f) + right * Random.Range(-4.5f, 4.5f) + Vector3.up * Random.Range(0.4f, 2.0f);
                Vector3 vel = forward * Random.Range(4f, 12f) + Vector3.up * Random.Range(2.5f, 6.5f);
                EmitVolumetricMist(surfacePos + off, vel, Random.Range(6.5f, 12.5f) * clampedIntensity, Random.Range(0.12f, 0.22f), Random.Range(2.8f, 4.4f));
            }

            // Boiling frothing ocean foam bursting at the waterline
            int foamPatches = isHeavy ? 14 : 7;
            for (int i = 0; i < foamPatches; i++)
            {
                Vector3 foamOff = forward * Random.Range(-2f, 7f) + right * Random.Range(-4.5f, 4.5f);
                Vector3 foamVel = (foamOff.normalized + forward * 0.5f) * Random.Range(2.5f, 6.5f);
                EmitFoam(surfacePos + foamOff, foamVel, Random.Range(2.5f, 5.5f) * clampedIntensity);
            }

            // Also send dynamic wave ripples into the ocean
            if (WaterSystem.Instance != null)
            {
                float waveRad = isHeavy ? 24f : 16f;
                float waveStr = isHeavy ? 0.35f : 0.22f;
                WaterSystem.Instance.AddFootstepWave(surfacePos, waveRad, waveStr);
            }
        }

        /// <summary>
        /// Cataclysmic ocean impact splash when Gipsy Danger (2,000 tons) is released from aerial suspension
        /// and crashes down into the ocean: creates a massive 360-degree crown of hundreds of soaring droplets,
        /// violent rising mist geysers, turbulent foam rings expanding outward, and water displacement shockwaves.
        /// </summary>
        public void SpawnColossalTitanDropSplash(Vector3 impactPoint)
        {
            if (float.IsNaN(impactPoint.x) || float.IsNaN(impactPoint.y) || float.IsNaN(impactPoint.z)) return;
            EnsureSystems();

            bool dryAtDrop = IsDryLandAtPoint(impactPoint, WaterSystem.Instance, out Vector3 surfacePos);

            if (dryAtDrop)
            {
                for (int i = 0; i < 24; i++)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    Vector3 off = radial * Random.Range(1f, 9.0f) + Vector3.up * Random.Range(0.3f, 2.0f);
                    Vector3 vel = radial * Random.Range(3.0f, 9.0f) + Vector3.up * Random.Range(2.0f, 6.0f);
                    EmitMist(surfacePos + off, vel, Random.Range(6.5f, 14.0f), Random.Range(0.28f, 0.48f));
                }
                return;
            }

            if (IsDeepUnderwater)
            {
                for (int i = 0; i < 36; i++)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    Vector3 off = radial * Random.Range(1f, 10.5f) + Vector3.up * Random.Range(0.4f, 2.5f);
                    Vector3 vel = radial * Random.Range(3.5f, 10.5f) + Vector3.up * Random.Range(3.5f, 8.5f);
                    EmitMist(surfacePos + off, vel, Random.Range(8.5f, 18.0f), Random.Range(0.48f, 0.78f));
                }

                for (int i = 0; i < 48; i++)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    float r = Random.Range(0.8f, 22.0f);
                    Vector3 pos = surfacePos + radial * r + Vector3.up * Random.Range(0.3f, 3.2f);
                    Vector3 vel = radial * Random.Range(6.5f, 16.5f) + Vector3.up * Random.Range(3.5f, 10.5f);
                    EmitVolumetricMist(pos, vel, Random.Range(12.0f, 24.0f), Random.Range(0.26f, 0.45f), Random.Range(4.4f, 7.0f));
                }

                for (int i = 0; i < 135; i++)
                {
                    bool isMicro = i % 3 != 0;
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    float r0 = Random.Range(0.4f, 11.5f);
                    Vector3 bubbleVel = radial * Random.Range(1.4f, 6.2f) + Vector3.up * Random.Range(2.8f, 9.2f);
                    float sz = isMicro ? Random.Range(0.06f, 0.22f) : Random.Range(0.24f, 0.58f);
                    EmitBubble(surfacePos + radial * r0 + Vector3.up * Random.Range(0.1f, 2.2f), sz, bubbleVel);
                }
                return;
            }

            // 1. Massive 360-degree droplet eruption (240 droplets flying up to 28m high and radially outward)
            int dropletCount = 240;
            for (int i = 0; i < dropletCount; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));

                bool highJet = (i % 3) == 0;
                float r0 = Random.Range(0.4f, 4.5f);
                Vector3 spawnPos = surfacePos + radial * r0 + Vector3.up * Random.Range(0.2f, 1.2f);

                float upVel = highJet ? Random.Range(20f, 32f) : Random.Range(10f, 22f);
                float radialVel = highJet ? Random.Range(4f, 12f) : Random.Range(12f, 26f);
                Vector3 vel = radial * radialVel + Vector3.up * upVel;

                float dropSize = Random.Range(0.45f, 1.35f);
                EmitDroplet(spawnPos, vel, dropSize);
            }

            // 2. Rising geyser mist clouds (26 billowing mist puffs)
            int mistCount = 26;
            for (int i = 0; i < mistCount; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 off = radial * Random.Range(1f, 8.5f) + Vector3.up * Random.Range(0.5f, 2.5f);
                Vector3 vel = radial * Random.Range(2.5f, 8.0f) + Vector3.up * Random.Range(4.0f, 9.5f);
                EmitMist(surfacePos + off, vel, Random.Range(6.5f, 14.0f), Random.Range(0.55f, 0.85f));
            }

            // 2b. Atmospheric 360-degree volumetric mist eruption for cutscene titan landing (soft, airy vapor clouds)
            int volMistDropCount = 42;
            for (int i = 0; i < volMistDropCount; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                float r = Random.Range(0.8f, 18.0f);
                Vector3 pos = surfacePos + radial * r + Vector3.up * Random.Range(0.3f, 3.2f);

                float outSpd = Random.Range(6.0f, 16.0f);
                float upSpd = Random.Range(4.0f, 12.0f);
                Vector3 vel = radial * outSpd + Vector3.up * upSpd;

                float pSize = Random.Range(9.0f, 16.0f);
                float pAlpha = Random.Range(0.14f, 0.25f);
                float pLife = Random.Range(4.0f, 6.5f);

                EmitVolumetricMist(pos, vel, pSize, pAlpha, pLife);
            }

            // 3. Boiling frothing ocean foam ring (38 expanding foam patches)
            int foamCount = 38;
            for (int i = 0; i < foamCount; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                float r0 = Random.Range(1.5f, 7.5f);
                Vector3 foamVel = radial * Random.Range(4.0f, 9.5f);
                EmitFoam(surfacePos + radial * r0, foamVel, Random.Range(3.5f, 7.5f));
            }

            // 4. Heavy cavitation bubbles (45 bubbles churning in the impact wake)
            int bubbleCount = 45;
            for (int i = 0; i < bubbleCount; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                float r0 = Random.Range(0.5f, 5.5f);
                Vector3 bubbleVel = radial * Random.Range(1.2f, 4.5f);
                EmitBubble(surfacePos + radial * r0, Random.Range(0.35f, 0.85f), bubbleVel);
            }

            // 5. Huge kinetic ocean surface wave ripple
            if (WaterSystem.Instance != null)
            {
                WaterSystem.Instance.AddFootstepWave(surfacePos, 50f, 0.95f);
            }
        }

        /// <summary>
        /// Splash of a big wave slapping against armor (torso, arms, hips...). The contact point should
        /// lie on/near the armor surface; spray peels off in the outward direction and is clip-guarded.
        /// </summary>
        public void SpawnBodySplash(Vector3 contactPoint, Vector3 outwardNormal, float intensity, float sizeScale = 1f)
        {
            if (float.IsNaN(contactPoint.x) || float.IsNaN(contactPoint.y) || float.IsNaN(contactPoint.z)) return;
            EnsureSystems();

            intensity = Mathf.Clamp01(intensity);
            sizeScale = Mathf.Clamp(sizeScale, 0.4f, 2.0f);

            Vector3 n = new Vector3(outwardNormal.x, 0f, outwardNormal.z);
            n = n.sqrMagnitude > 0.0001f ? n.normalized : Vector3.forward;
            Vector3 tangent = Vector3.Cross(Vector3.up, n);

            int drops = Mathf.RoundToInt(Mathf.Lerp(26f, 78f, intensity) * sizeScale);
            float speed = Mathf.Lerp(6.0f, 15.5f, intensity);
            Vector3 axis = (n * 0.8f + Vector3.up * 1.0f).normalized;

            for (int i = 0; i < drops; i++)
            {
                Vector3 dir = Vector3.Slerp(axis, Random.onUnitSphere, Random.Range(0.05f, 0.38f));
                if (dir.y < 0.15f) dir.y = 0.15f + Mathf.Abs(dir.y);
                dir.Normalize();

                Vector3 pos = contactPoint + tangent * Random.Range(-1.4f, 1.4f) * sizeScale + n * Random.Range(0f, 0.35f) + Vector3.up * Random.Range(0f, 0.5f);
                float dropSize = Random.Range(0.30f, 0.78f) * Mathf.Lerp(0.95f, 1.45f, intensity) * Mathf.Lerp(0.85f, 1.25f, sizeScale * 0.5f);
                EmitDroplet(pos, dir * (speed * Random.Range(0.45f, 1.15f)), dropSize);
            }

            int puffs = 3 + Mathf.RoundToInt(6f * intensity);
            for (int i = 0; i < puffs; i++)
            {
                Vector3 pos = contactPoint + n * Random.Range(0.3f, 0.9f) + tangent * Random.Range(-1.0f, 1.0f) + Vector3.up * Random.Range(0.2f, 1.0f);
                Vector3 vel = n * Random.Range(1.0f, 2.2f) + Vector3.up * Random.Range(1.5f, 3.4f);
                EmitMist(pos, vel, Random.Range(2.8f, 5.8f) * sizeScale, Random.Range(0.3f, 0.55f));
            }

            int foamCount = 4 + Mathf.RoundToInt(8f * intensity);
            for (int i = 0; i < foamCount; i++)
            {
                Vector3 pos = contactPoint + n * Random.Range(0.5f, 2.6f) + tangent * Random.Range(-2.2f, 2.2f);
                EmitFoam(pos, n * Random.Range(0.8f, 2.4f) + tangent * Random.Range(-0.6f, 0.6f), Random.Range(1.3f, 3.0f) * sizeScale);
            }

            int bubbleCount = 8 + Mathf.RoundToInt(14f * intensity);
            for (int i = 0; i < bubbleCount; i++)
            {
                Vector3 pos = contactPoint + n * Random.Range(0.4f, 3.0f) + tangent * Random.Range(-2.4f, 2.4f);
                EmitBubble(pos, Random.Range(0.14f, 0.5f), n * Random.Range(0.3f, 1.6f));
            }
        }

        /// <summary>
        /// Small bubbling/breaking-crest effect on a tall wave: foam patches, bubbles and occasional fine spray.
        /// </summary>
        public void SpawnCrestFoam(Vector3 surfacePos, float intensity)
        {
            if (float.IsNaN(surfacePos.x) || float.IsNaN(surfacePos.y) || float.IsNaN(surfacePos.z)) return;
            EnsureSystems();

            intensity = Mathf.Clamp01(intensity);

            Vector3 drift = Vector3.zero;
            WaterSystem water = WaterSystem.Instance;
            if (water != null)
            {
                Vector2 d = water.DominantWaveDirection;
                drift = new Vector3(d.x, 0f, d.y) * (water.DominantPhaseSpeed * 0.32f);
            }

            int patches = 1 + (intensity > 0.55f ? 1 : 0);
            for (int i = 0; i < patches; i++)
            {
                Vector3 jitter = Flat(Random.insideUnitCircle * 1.6f);
                EmitFoam(surfacePos + jitter, drift + Flat(Random.insideUnitCircle * 0.6f), Random.Range(1.6f, 3.8f) * (0.7f + intensity * 0.7f));
            }

            int bubbleCount = 3 + Mathf.RoundToInt(5f * intensity);
            for (int i = 0; i < bubbleCount; i++)
            {
                Vector3 jitter = Flat(Random.insideUnitCircle * 2.2f);
                EmitBubble(surfacePos + jitter, Random.Range(0.12f, 0.46f), drift * Random.Range(0.6f, 1.0f) + Flat(Random.insideUnitCircle * 0.5f));
            }

            if (intensity > 0.55f && Random.value < 0.5f)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector3 pos = surfacePos + Flat(Random.insideUnitCircle * 1.2f) + Vector3.up * 0.2f;
                    EmitDroplet(pos, drift * 0.5f + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(1.8f, 4.5f), Random.Range(-0.8f, 0.8f)), Random.Range(0.16f, 0.34f));
                }
            }

            if (intensity > 0.75f && Random.value < 0.25f)
            {
                EmitMist(surfacePos + Vector3.up * 0.4f, drift * 0.4f + Vector3.up * 1.2f, Random.Range(2.0f, 3.2f), Random.Range(0.25f, 0.4f));
            }
        }

        /// <summary>
        /// Emits high-velocity glowing metal/plasma sparks for blocked parries and unblocked melee impacts.
        /// </summary>
        public void SpawnCombatSparks(Vector3 impactPoint, Vector3 outwardNormal, bool isBlocked, bool isHeavy, bool isKaijuTarget = false)
        {
            if (float.IsNaN(impactPoint.x) || float.IsNaN(impactPoint.y) || float.IsNaN(impactPoint.z)) return;
            EnsureSystems();
            if (sparks == null) return;

            Vector3 n = outwardNormal.sqrMagnitude > 0.001f ? outwardNormal.normalized : Vector3.up;
            int count = isHeavy ? (isBlocked ? 110 : 95) : (isBlocked ? 80 : 62);

            Color cPrimary = isBlocked
                ? new Color(0.35f, 0.92f, 1.0f, 1f)
                : new Color(1.0f, 0.58f, 0.12f, 1f);
            Color cCore = isBlocked
                ? new Color(1.0f, 0.92f, 0.55f, 1f)
                : new Color(1.0f, 0.95f, 0.75f, 1f);
            Color cAccent = isBlocked
                ? new Color(0.15f, 0.58f, 1.0f, 1f)
                : (isKaijuTarget ? new Color(0.25f, 0.95f, 1.0f, 1f) : new Color(1.0f, 0.28f, 0.08f, 1f));

            float spread = isBlocked ? 0.92f : 0.72f;
            float speedMin = isHeavy ? 22f : 18f;
            float speedMax = isHeavy ? 42f : 34f;

            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Vector3.Slerp(n, Random.onUnitSphere, Random.Range(0.18f, spread));
                if (dir.y < -0.25f) dir.y = Mathf.Abs(dir.y) * 0.5f;
                dir.Normalize();

                float speed = Random.Range(speedMin, speedMax);
                Vector3 pos = impactPoint + Random.insideUnitSphere * (isHeavy ? 1.4f : 0.9f);

                float roll = Random.value;
                Color32 col = roll < 0.35f ? cCore : (roll < 0.75f ? cPrimary : cAccent);

                emit = new ParticleSystem.EmitParams();
                emit.position = pos;
                emit.velocity = dir * speed + Vector3.up * Random.Range(1.5f, 6.0f);
                emit.startLifetime = Random.Range(0.18f, 0.48f);
                emit.startSize = Random.Range(0.28f, isHeavy ? 0.75f : 0.58f);
                emit.startColor = col;
                emit.applyShapeToPosition = false;
                sparks.Emit(emit, 1);
            }
        }

        /// <summary>
        /// Emits a brilliant radial golden/white plasma spark burst when Gipsy Danger perfectly parries an incoming Kaiju strike.
        /// </summary>
        public void SpawnParrySparks(Vector3 impactPoint, Vector3 outwardNormal)
        {
            if (float.IsNaN(impactPoint.x) || float.IsNaN(impactPoint.y) || float.IsNaN(impactPoint.z)) return;
            EnsureSystems();
            if (sparks == null) return;

            Vector3 n = outwardNormal.sqrMagnitude > 0.001f ? outwardNormal.normalized : Vector3.up;
            int count = 145;

            Color cGold = new Color(1.0f, 0.85f, 0.20f, 1f);
            Color cWhite = new Color(1.0f, 1.0f, 0.95f, 1f);
            Color cAmber = new Color(1.0f, 0.65f, 0.10f, 1f);

            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Vector3.Slerp(n, Random.onUnitSphere, Random.Range(0.12f, 0.95f));
                if (dir.y < -0.2f) dir.y = Mathf.Abs(dir.y) * 0.5f;
                dir.Normalize();

                float speed = Random.Range(26f, 48f);
                Vector3 pos = impactPoint + Random.insideUnitSphere * 1.2f;

                float roll = Random.value;
                Color32 col = roll < 0.45f ? cWhite : (roll < 0.85f ? cGold : cAmber);

                emit = new ParticleSystem.EmitParams();
                emit.position = pos;
                emit.velocity = dir * speed + Vector3.up * Random.Range(2.0f, 7.0f);
                emit.startLifetime = Random.Range(0.20f, 0.55f);
                emit.startSize = Random.Range(0.40f, 0.95f);
                emit.startColor = col;
                emit.applyShapeToPosition = false;
                sparks.Emit(emit, 1);
            }
        }

        #endregion
    }

    /// <summary>Procedurally generated soft particle shapes (no hard square edges).</summary>
    internal static class SplashTextures
    {
        private static Texture2D droplet, puff, foamPatch, bubble, volumetricMistPuff;

        private static Texture2D Build(string texName, int size, System.Func<float, float, float, float> alphaFn)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = texName,
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
                    float a = r >= 1f ? 0f : Mathf.Clamp01(alphaFn(u, v, r));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        public static Texture2D SoftDroplet()
        {
            if (droplet == null)
            {
                droplet = Build("SplashDroplet", 64, (u, v, r) => Mathf.Pow(Smooth(1f, 0.05f, r), 1.2f));
            }
            return droplet;
        }

        public static Texture2D Puff()
        {
            if (puff == null)
            {
                puff = Build("SplashPuff", 128, (u, v, r) =>
                {
                    float n = Mathf.PerlinNoise(u * 1.7f + 11.3f, v * 1.7f + 4.1f) * 0.6f
                            + Mathf.PerlinNoise(u * 3.9f + 2.7f, v * 3.9f + 9.4f) * 0.4f;
                    float radial = Smooth(1f, 0.15f, r);
                    return Mathf.Clamp01(n * 1.5f - 0.15f) * Mathf.Pow(radial, 1.3f);
                });
            }
            return puff;
        }

        public static Texture2D FoamPatch()
        {
            if (foamPatch == null)
            {
                foamPatch = Build("SplashFoamPatch", 128, (u, v, r) =>
                {
                    float n = Mathf.PerlinNoise(u * 2.6f + 31.7f, v * 2.6f + 7.9f) * 0.55f
                            + Mathf.PerlinNoise(u * 5.7f + 3.3f, v * 5.7f + 19.1f) * 0.30f
                            + Mathf.PerlinNoise(u * 11.5f + 8.8f, v * 11.5f + 1.2f) * 0.15f;
                    float broken = Smooth(0.40f, 0.62f, n);
                    float radial = Smooth(1f, 0.40f, r);
                    return (broken * 0.95f + 0.14f) * radial;
                });
            }
            return foamPatch;
        }

        public static Texture2D Bubble()
        {
            if (bubble == null)
            {
                // Realistic thin-rim refractive deep-sea bubble (no chunky cartoon ring or giant polka-dot)
                bubble = Build("SplashBubble", 128, (u, v, r) =>
                {
                    float rim = Mathf.Exp(-Mathf.Pow((r - 0.84f) / 0.095f, 2f)) * 0.56f;
                    float outerSoft = Smooth(1.0f, 0.78f, r);
                    float interior = 0.045f * (1f - Smooth(0.0f, 0.88f, r));
                    // Subtle upper-left crescent caustic refraction instead of a solid dot
                    float angle = Mathf.Atan2(v, u);
                    float crescent = Mathf.Pow(Mathf.Clamp01(Mathf.Cos(angle - 2.35f)), 4.0f) * Mathf.Exp(-Mathf.Pow((r - 0.66f) / 0.14f, 2f)) * 0.34f;
                    float glintDist = Mathf.Sqrt((u + 0.34f) * (u + 0.34f) + (v - 0.34f) * (v - 0.34f));
                    float glint = Smooth(0.11f, 0.02f, glintDist) * 0.28f;
                    return (rim + interior + crescent + glint) * outerSoft;
                });
            }
            return bubble;
        }

        public static Texture2D VolumetricMistPuff()
        {
            if (volumetricMistPuff == null)
            {
                volumetricMistPuff = Build("SplashVolumetricMistPuff", 128, (u, v, r) =>
                {
                    float n = Mathf.PerlinNoise(u * 2.1f + 14.3f, v * 2.1f + 8.7f) * 0.5f
                            + Mathf.PerlinNoise(u * 4.6f + 3.1f, v * 4.6f + 17.5f) * 0.35f
                            + Mathf.PerlinNoise(u * 9.2f + 9.8f, v * 9.2f + 2.4f) * 0.15f;
                    float radial = Smooth(1.0f, 0.08f, r);
                    float core = Smooth(1.0f, 0.35f, r);
                    return Mathf.Clamp01(n * 1.35f - 0.1f) * Mathf.Pow(radial, 1.4f) * core;
                });
            }
            return volumetricMistPuff;
        }
    }
}
