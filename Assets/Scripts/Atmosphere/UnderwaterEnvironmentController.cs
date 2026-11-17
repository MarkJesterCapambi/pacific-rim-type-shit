using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PacificRimWater;

namespace PacificRimAtmosphere
{
    /// <summary>
    /// Drives the deep abyssal underwater environment for "Operation Pitfall"
    /// without using the surface WaterSystem simulation:
    /// - Deep ocean exponential fog, readable abyssal trilight ambient, and muted caustic-blue key scatter
    /// - Clears storm globals (_GlobalRainWetness = 0, _GlobalLightningFlash = 0)
    /// - Drives organic Breach godray pulsing/shimmering and multi-layered realistic underwater bubbles
    /// - Tracks the camera/Jaeger so marine snow, rising micro/macro bubbles, and seabed silt surround gameplay
    /// - Handles the underwater combat entry sequence when no Chinook deployment cutscene is present
    /// </summary>
    [ExecuteAlways]
    public class UnderwaterEnvironmentController : MonoBehaviour
    {
        public static UnderwaterEnvironmentController Instance { get; private set; }

        [Header("Targets & Particle Systems")]
        public Transform followTarget;
        public ParticleSystem marineSnowParticles;
        public ParticleSystem risingBubblesParticles;
        public ParticleSystem microBubblesParticles;
        public ParticleSystem seabedSiltParticles;
        public ParticleSystem bioluminescentJellyfishParticles;

        [Header("Abyssal Fog & Lighting")]
        public Color abyssalFogColor = new Color(0.022f, 0.095f, 0.135f, 1f);
        public float abyssalFogDensity = 0.0120f;
        public Color ambientSkyColor = new Color(0.060f, 0.155f, 0.225f, 1f);
        public Color ambientEquatorColor = new Color(0.036f, 0.098f, 0.150f, 1f);
        public Color ambientGroundColor = new Color(0.020f, 0.048f, 0.075f, 1f);
        public Light mainDirectionalLight;
        public Color underwaterLightColor = new Color(0.24f, 0.54f, 0.75f, 1f);
        public float underwaterLightIntensity = 0.55f;

        [Header("The Breach Dynamic Pulse")]
        public Light[] breachPulseLights;
        public float[] breachBaseIntensities;
        public Renderer[] breachWebbingRenderers;
        public Color breachWebbingBaseHdr = new Color(4.2f, 3.4f, 0.35f, 1f);
        public Renderer[] breachGodRayRenderers;

        [Header("Post-Processing")]
        public Volume globalVolume;

        private bool introStarted = false;
        private MaterialPropertyBlock breachWebBlock;
        private MaterialPropertyBlock breachGodRayBlock;
        private float[] godRayBaseIntensities;
        private Color[] godRayBaseColors;
        private Quaternion[] godRayBaseRotations;
        private bool floodlightsLengthened = false;
        private bool bubblesConfigured = false;
        private Texture2D runtimeBubbleTexture;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int PulsePhaseId = Shader.PropertyToID("_PulsePhase");

        private void OnEnable()
        {
            if (Instance == null || Instance == this)
                Instance = this;

            ApplyAbyssalAtmosphere();
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            ApplyAbyssalAtmosphere();
            EnsureBreachGodRayReferences();
            LengthenJaegerFloodlights();
            EnsureRealisticUnderwaterBubbles();

            if (!Application.isPlaying) return;

            DisableUnderwaterPropColliders();

            if (PacificRimAudio.PacificRimAudioManager.Instance != null)
            {
                PacificRimAudio.PacificRimAudioManager.Instance.RefreshSceneAudioMode(gameObject.scene.name);
            }

            if (PacificRimUI.PacificRimLoadingScreen.Instance != null && PacificRimUI.PacificRimLoadingScreen.Instance.IsLoading)
            {
                return;
            }

            StartCoroutine(PreloadAndStartIntro());
        }

        private static void DisableUnderwaterPropColliders()
        {
            foreach (var col in FindObjectsByType<Collider>(FindObjectsInactive.Include))
            {
                if (col == null) continue;
                if (col is TerrainCollider) continue;
                if (col.GetComponentInParent<SimpleJaegerController>() != null) continue;
                if (col.GetComponentInParent<KaijuAI>() != null) continue;

                string objName = col.gameObject.name;
                if (objName.StartsWith("Canyon_Invisible_Wall")) continue;

                // Disable colliders on The_Breach, Basalt_Boulder_*, Hydrothermal vents, and other environment props
                if (objName.StartsWith("Basalt_Boulder") ||
                    objName.StartsWith("Hydrothermal") ||
                    objName.StartsWith("Breach") ||
                    objName.StartsWith("CrustalFissure") ||
                    objName.StartsWith("SeafloorLavaVein") ||
                    col.transform.root.name == "The_Breach" ||
                    col.transform.root.name.Contains("Boulder") ||
                    col.transform.root.name.Contains("Vent") ||
                    col.transform.root.name.Contains("Prop"))
                {
                    col.enabled = false;
                }
            }
        }

        private void Update()
        {
            ApplyAbyssalAtmosphere();
            UpdateBreachPulse();

            if (!bubblesConfigured)
            {
                EnsureRealisticUnderwaterBubbles();
            }

            if (Application.isPlaying)
            {
                UpdateFollowerPosition();
            }
        }

        private void UpdateBreachPulse()
        {
            float t = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartup;
            float pulse = 0.86f + 0.18f * Mathf.Sin(t * 1.9f) + 0.07f * Mathf.Sin(t * 4.7f);

            if (breachPulseLights != null && breachBaseIntensities != null)
            {
                int count = Mathf.Min(breachPulseLights.Length, breachBaseIntensities.Length);
                for (int i = 0; i < count; i++)
                {
                    if (breachPulseLights[i] != null)
                    {
                        breachPulseLights[i].intensity = breachBaseIntensities[i] * pulse;
                    }
                }
            }

            if (breachWebbingRenderers != null && breachWebbingRenderers.Length > 0)
            {
                if (breachWebBlock == null) breachWebBlock = new MaterialPropertyBlock();
                Color hdr = breachWebbingBaseHdr * pulse;
                for (int i = 0; i < breachWebbingRenderers.Length; i++)
                {
                    Renderer r = breachWebbingRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(breachWebBlock);
                    breachWebBlock.SetColor(EmissionColorId, hdr);
                    r.SetPropertyBlock(breachWebBlock);
                }
            }

            UpdateBreachGodRays(t);
        }

        private void EnsureBreachGodRayReferences()
        {
            if (breachGodRayRenderers == null || breachGodRayRenderers.Length == 0)
            {
                GameObject raysRoot = GameObject.Find("Breach_VolumetricGodRays");
                if (raysRoot != null)
                {
                    breachGodRayRenderers = raysRoot.GetComponentsInChildren<Renderer>(true);
                }
            }

            if (breachGodRayRenderers != null && breachGodRayRenderers.Length > 0 &&
                (godRayBaseIntensities == null || godRayBaseIntensities.Length != breachGodRayRenderers.Length))
            {
                godRayBaseIntensities = new float[breachGodRayRenderers.Length];
                godRayBaseColors = new Color[breachGodRayRenderers.Length];
                godRayBaseRotations = new Quaternion[breachGodRayRenderers.Length];
                for (int i = 0; i < breachGodRayRenderers.Length; i++)
                {
                    Renderer r = breachGodRayRenderers[i];
                    Material mat = r != null ? r.sharedMaterial : null;
                    godRayBaseIntensities[i] = (mat != null && mat.HasProperty(IntensityId)) ? mat.GetFloat(IntensityId) : 2.6f;
                    godRayBaseColors[i] = (mat != null && mat.HasProperty(ColorId)) ? mat.GetColor(ColorId) : new Color(1.0f, 0.55f, 0.12f, 0.36f);
                    godRayBaseRotations[i] = r != null ? r.transform.localRotation : Quaternion.identity;
                }
            }
        }

        private void UpdateBreachGodRays(float t)
        {
            EnsureBreachGodRayReferences();
            if (breachGodRayRenderers == null || breachGodRayRenderers.Length == 0) return;
            if (breachGodRayBlock == null) breachGodRayBlock = new MaterialPropertyBlock();

            // Global rift surge wave that swells across the Breach periodically
            float riftSurge = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 0.42f), 2.2f);

            for (int i = 0; i < breachGodRayRenderers.Length; i++)
            {
                Renderer r = breachGodRayRenderers[i];
                if (r == null) continue;

                float phase = i * 0.78f;
                float slowWave = 0.5f + 0.5f * Mathf.Sin(t * 0.72f + phase);
                float midWave = 0.5f + 0.5f * Mathf.Sin(t * 1.45f + phase * 1.85f);
                float shimmer = 0.5f + 0.5f * Mathf.Sin(t * 2.9f + phase * 2.7f);
                float breath = Mathf.SmoothStep(0f, 1f, slowWave * 0.62f + midWave * 0.28f + shimmer * 0.10f);
                float combined = Mathf.Clamp01(breath * 0.78f + riftSurge * 0.22f);

                // Dramatic opacity swell: rays fade nearly out (0.08) and surge up (1.35)
                float alphaScale = Mathf.Lerp(0.08f, 1.35f, combined);
                float intensityScale = Mathf.Lerp(0.20f, 1.42f, combined);

                Color baseCol = godRayBaseColors[i];
                Color coolAmber = new Color(1.0f, 0.36f, 0.04f, baseCol.a);
                Color hotGold = new Color(1.0f, 0.82f, 0.34f, baseCol.a);
                Color tinted = Color.Lerp(coolAmber, hotGold, combined * 0.72f);
                tinted.a = Mathf.Clamp01(baseCol.a * alphaScale);

                r.GetPropertyBlock(breachGodRayBlock);
                breachGodRayBlock.SetColor(ColorId, tinted);
                breachGodRayBlock.SetFloat(IntensityId, godRayBaseIntensities[i] * intensityScale);
                breachGodRayBlock.SetFloat(PulsePhaseId, phase);
                r.SetPropertyBlock(breachGodRayBlock);

                // Gentle organic underwater drift on the light shafts during runtime
                if (Application.isPlaying && godRayBaseRotations != null && i < godRayBaseRotations.Length)
                {
                    float swayPitch = Mathf.Sin(t * 0.45f + phase) * 1.6f;
                    float swayYaw = Mathf.Cos(t * 0.35f + phase * 1.3f) * 2.1f;
                    r.transform.localRotation = godRayBaseRotations[i] * Quaternion.Euler(swayPitch, swayYaw, 0f);
                }
            }
        }

        private void EnsureRealisticUnderwaterBubbles()
        {
            bubblesConfigured = true;
            Texture2D bubbleTex = LoadOrCreateRealisticBubbleTexture();

            if (risingBubblesParticles == null)
            {
                Transform existing = transform.Find("Underwater_RisingBubbles");
                if (existing != null) risingBubblesParticles = existing.GetComponent<ParticleSystem>();
            }

            if (risingBubblesParticles != null)
            {
                ConfigureBubbleSystem(
                    risingBubblesParticles,
                    bubbleTex,
                    maxParticles: 2400,
                    rateOverTime: 260f,
                    boxScale: new Vector3(78f, 16f, 78f),
                    localPos: new Vector3(0f, 2.5f, 0f),
                    sizeMin: 0.06f,
                    sizeMax: 0.30f,
                    lifeMin: 5.0f,
                    lifeMax: 8.5f,
                    riseMin: 2.4f,
                    riseMax: 6.2f,
                    noiseStrength: 1.35f,
                    noiseFreq: 1.15f,
                    tint: new Color(0.76f, 0.93f, 1.0f, 0.72f));
            }

            if (microBubblesParticles == null)
            {
                Transform existingMicro = transform.Find("Underwater_MicroBubbles");
                GameObject microGo = existingMicro != null ? existingMicro.gameObject : new GameObject("Underwater_MicroBubbles");
                if (existingMicro == null)
                {
                    microGo.transform.SetParent(transform, false);
                }
                microBubblesParticles = microGo.GetComponent<ParticleSystem>() ?? microGo.AddComponent<ParticleSystem>();
            }

            if (microBubblesParticles != null)
            {
                ConfigureBubbleSystem(
                    microBubblesParticles,
                    bubbleTex,
                    maxParticles: 2800,
                    rateOverTime: 360f,
                    boxScale: new Vector3(62f, 20f, 62f),
                    localPos: new Vector3(0f, 4.0f, 0f),
                    sizeMin: 0.025f,
                    sizeMax: 0.11f,
                    lifeMin: 4.5f,
                    lifeMax: 7.5f,
                    riseMin: 1.6f,
                    riseMax: 4.5f,
                    noiseStrength: 1.10f,
                    noiseFreq: 1.45f,
                    tint: new Color(0.72f, 0.90f, 0.98f, 0.58f));
            }
        }

        private void ConfigureBubbleSystem(
            ParticleSystem ps,
            Texture2D bubbleTex,
            int maxParticles,
            float rateOverTime,
            Vector3 boxScale,
            Vector3 localPos,
            float sizeMin,
            float sizeMax,
            float lifeMin,
            float lifeMax,
            float riseMin,
            float riseMax,
            float noiseStrength,
            float noiseFreq,
            Color tint)
        {
            ps.transform.localPosition = localPos;

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.maxParticles = maxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new Color(0.86f, 0.96f, 1.0f, 0.80f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = rateOverTime;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxScale;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            vel.y = new ParticleSystem.MinMaxCurve(riseMin, riseMax);
            vel.z = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = noiseStrength;
            noise.frequency = noiseFreq;
            noise.scrollSpeed = 0.65f;
            noise.octaveCount = 2;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.72f),
                new Keyframe(1f, 1.15f)));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.78f, 0.94f, 1f), 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.88f, 0.14f),
                    new GradientAlphaKey(0.72f, 0.74f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = grad;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.renderMode = ParticleSystemRenderMode.Billboard;
                rend.shadowCastingMode = ShadowCastingMode.Off;
                rend.receiveShadows = false;
                rend.minParticleSize = 0f;
                rend.maxParticleSize = 0.45f;

                Material mat = rend.sharedMaterial;
                if (mat == null || mat.name.StartsWith("Default-Particle") || mat.mainTexture == null)
                {
                    Shader sh = Shader.Find("PacificRim/SplashParticle") ?? Shader.Find("Sprites/Default");
                    if (mat == null || mat.shader != sh)
                    {
                        mat = new Material(sh) { name = "PacificRim_UnderwaterBubbles_Runtime" };
                        rend.sharedMaterial = mat;
                    }
                }
                if (bubbleTex != null)
                {
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", bubbleTex);
                    else mat.mainTexture = bubbleTex;
                }
                if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", tint);
                if (mat.HasProperty("_InvFade")) mat.SetFloat("_InvFade", 0.30f);
                if (mat.HasProperty("_LightInfluence")) mat.SetFloat("_LightInfluence", 0.62f);
            }

            if (Application.isPlaying && !ps.isPlaying)
            {
                ps.Play();
            }
        }

        private Texture2D LoadOrCreateRealisticBubbleTexture()
        {
#if UNITY_EDITOR
            var assetTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/PacificRim_RealisticBubble.png");
            if (assetTex != null) return assetTex;
#endif
            if (runtimeBubbleTexture != null) return runtimeBubbleTexture;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "RuntimeRealisticBubble",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = 0f;
                    if (r < 1f)
                    {
                        float rim = Mathf.Exp(-Mathf.Pow((r - 0.84f) / 0.095f, 2f)) * 0.56f;
                        float outerSoft = SmoothRange(1.0f, 0.78f, r);
                        float interior = 0.045f * (1f - SmoothRange(0f, 0.88f, r));
                        float angle = Mathf.Atan2(v, u);
                        float crescent = Mathf.Pow(Mathf.Clamp01(Mathf.Cos(angle - 2.35f)), 4.0f) * Mathf.Exp(-Mathf.Pow((r - 0.66f) / 0.14f, 2f)) * 0.34f;
                        float glintDist = Mathf.Sqrt((u + 0.34f) * (u + 0.34f) + (v - 0.34f) * (v - 0.34f));
                        float glint = SmoothRange(0.11f, 0.02f, glintDist) * 0.28f;
                        a = Mathf.Clamp01((rim + interior + crescent + glint) * outerSoft);
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            runtimeBubbleTexture = tex;
            return tex;
        }

        private static float SmoothRange(float edge0, float edge1, float x)
        {
            if (Mathf.Approximately(edge0, edge1)) return x >= edge1 ? 1f : 0f;
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private void LengthenJaegerFloodlights()
        {
            if (floodlightsLengthened) return;
            var jaeger = SimpleJaegerController.Instance != null ? SimpleJaegerController.Instance : FindAnyObjectByType<SimpleJaegerController>();
            if (jaeger == null) return;

            floodlightsLengthened = true;
            foreach (var t in jaeger.GetComponentsInChildren<Transform>(true))
            {
                bool isHead = t.name == "LightBeamOriginHead";
                bool isTorso = t.name.StartsWith("LightBeamOriginUpperTorso");
                if (!isHead && !isTorso) continue;

                var spot = t.GetComponent<Light>();
                if (spot != null && spot.type == LightType.Spot)
                {
                    spot.range = isHead ? 260f : 230f;
                    spot.intensity = isHead ? 5400f : 4200f;
                }

                var beamChild = t.Find("VolumetricBeam");
                if (beamChild != null)
                {
                    Vector3 s = beamChild.localScale;
                    beamChild.localScale = new Vector3(s.x, s.y, isHead ? 2.65f : 2.45f);
                }
            }
        }

        public void ApplyAbyssalAtmosphere()
        {
            Shader.SetGlobalFloat("_GlobalRainWetness", 0f);
            Shader.SetGlobalFloat("_GlobalLightningFlash", 0f);
            Shader.SetGlobalFloat("_CameraLensRainOverride", 0f);
            Shader.SetGlobalVector("_GlobalWindVector", new Vector4(0.35f, 0.25f, 2.5f, 0.2f));

            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = abyssalFogColor;
            RenderSettings.fogDensity = abyssalFogDensity;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSkyColor;
            RenderSettings.ambientEquatorColor = ambientEquatorColor;
            RenderSettings.ambientGroundColor = ambientGroundColor;

            if (mainDirectionalLight == null)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                {
                    if (l.type == LightType.Directional && l.gameObject.name != "LightningFlashLight")
                    {
                        mainDirectionalLight = l;
                        break;
                    }
                }
            }

            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.color = underwaterLightColor;
                mainDirectionalLight.intensity = underwaterLightIntensity;
                mainDirectionalLight.shadowStrength = 0.45f;
                mainDirectionalLight.enabled = underwaterLightIntensity > 0.001f;
                mainDirectionalLight.transform.rotation = Quaternion.Euler(68f, -25f, 0f);
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = abyssalFogColor;
                cam.farClipPlane = 450f;
                var camData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (camData != null)
                {
                    camData.renderPostProcessing = true;
                }
            }

            ApplyUnderwaterVolumeTuning();
        }

        private void ApplyUnderwaterVolumeTuning()
        {
            if (globalVolume == null) globalVolume = GetComponent<Volume>();
            if (globalVolume == null) return;
            var profile = globalVolume.sharedProfile != null ? globalVolume.sharedProfile : globalVolume.profile;
            if (profile == null) return;

            if (profile.TryGet<ColorAdjustments>(out var colorAdj))
            {
                colorAdj.postExposure.Override(0.28f);
                colorAdj.contrast.Override(11f);
                colorAdj.saturation.Override(6f);
                colorAdj.colorFilter.Override(new Color(0.88f, 0.95f, 1.0f, 1f));
            }
            if (profile.TryGet<Bloom>(out var bloom))
            {
                bloom.threshold.Override(1.02f);
                bloom.intensity.Override(1.38f);
                bloom.scatter.Override(0.72f);
            }
            if (profile.TryGet<SplitToning>(out var splitToning))
            {
                splitToning.shadows.Override(new Color(13f / 255f, 56f / 255f, 76f / 255f, 1f));
                splitToning.highlights.Override(new Color(1f, 173f / 255f, 56f / 255f, 1f));
                splitToning.balance.Override(-10f);
            }
            if (profile.TryGet<ChromaticAberration>(out var chromatic))
            {
                chromatic.intensity.Override(0.12f);
            }
            if (profile.TryGet<Vignette>(out var vig))
            {
                vig.intensity.Override(0.24f);
                vig.color.Override(new Color(2f / 255f, 10f / 255f, 15f / 255f, 1f));
            }
        }

        private void UpdateFollowerPosition()
        {
            Vector3 anchor = Vector3.zero;
            if (followTarget != null)
            {
                anchor = followTarget.position;
            }
            else if (Camera.main != null)
            {
                anchor = Camera.main.transform.position;
            }
            else
            {
                var jaeger = SimpleJaegerController.Instance;
                if (jaeger != null)
                {
                    followTarget = jaeger.transform;
                    anchor = jaeger.transform.position;
                }
            }

            if (Camera.main != null && followTarget != null)
            {
                // Center particle volume between camera and Jaeger so dense bubbles fill both foreground & combat space
                anchor = Vector3.Lerp(anchor, Camera.main.transform.position, 0.35f);
            }

            transform.position = new Vector3(anchor.x, 0f, anchor.z);
        }

        private IEnumerator PreloadAndStartIntro()
        {
            yield return PacificRimUI.PacificRimLoadingScreen.PreloadMainSceneAssetsRoutine();
            StartUnderwaterIntro();
        }

        public void StartUnderwaterIntro()
        {
            if (introStarted) return;
            introStarted = true;
            StartCoroutine(UnderwaterIntroRoutine());
        }

        private IEnumerator UnderwaterIntroRoutine()
        {
            var jaeger = SimpleJaegerController.Instance != null ? SimpleJaegerController.Instance : FindAnyObjectByType<SimpleJaegerController>();
            var kaiju = KaijuAI.Instance != null ? KaijuAI.Instance : FindAnyObjectByType<KaijuAI>();

            if (jaeger != null) jaeger.isCutsceneActive = true;
            if (kaiju != null) kaiju.SetCutscenePose(180f);

            yield return new WaitForSeconds(0.25f);

            if (PacificRimCutscene.CutsceneBlackTransitionOverlay.HasPendingFadeIn &&
                PacificRimCutscene.CutsceneBlackTransitionOverlay.Instance != null)
            {
                PacificRimCutscene.CutsceneBlackTransitionOverlay.Instance.BeginFadeInAndDestroy(0.95f);
            }

            bool bootDone = false;
            if (PacificRimUI.JaegerHUD.Instance != null)
            {
                PacificRimUI.JaegerHUD.Instance.SetCutsceneHidden(false);
                PacificRimUI.JaegerHUD.Instance.PlayNeuralBootSequence(() => bootDone = true);
            }
            else
            {
                bootDone = true;
            }

            while (!bootDone)
            {
                yield return null;
            }

            if (jaeger != null) jaeger.isCutsceneActive = false;
            if (kaiju != null)
            {
                kaiju.isCutsceneMode = false;
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuAttackGrunt(kaiju.transform.position + Vector3.up * 16f);
            }

            PacificRimUI.JaegerHUD.Instance?.ShowBanner("OPERATION PITFALL // ENGAGE CATEGORY V SLATTERN", 4.0f);
        }
    }
}
