using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PacificRimAtmosphere
{
    /// <summary>
    /// Configures the atmospheric Pacific Rim storm lighting, balanced fog, URP Volume post-processing,
    /// and anamorphic horizontal lens flare streaks without over-darkening the scene.
    /// </summary>
    [ExecuteAlways]
    public class AtmosphereVolumeController : MonoBehaviour
    {
        public static AtmosphereVolumeController Instance { get; private set; }

        [Header("Ambient Lighting (Balanced Storm Teal-Blue)")]
        public Color stormSkyAmbient = new Color(0.24f, 0.34f, 0.46f, 1f);
        public Color stormEquatorAmbient = new Color(0.18f, 0.25f, 0.35f, 1f);
        public Color stormGroundAmbient = new Color(0.10f, 0.14f, 0.20f, 1f);

        [Header("Balanced Exponential Fog")]
        public bool enableDenseFog = true;
        public float fogDensity = 0.0022f;
        public Color fogColor = new Color(0.12f, 0.18f, 0.26f, 1f);

        [Header("Main Directional Light")]
        public Light mainDirectionalLight;
        public Color mainLightColor = new Color(0.78f, 0.88f, 1.0f, 1f);
        public float mainLightIntensity = 1.65f;

        [Header("Global Volume Profile Overrides")]
        public Volume globalVolume;
        [Range(0f, 5f)] public float bloomIntensity = 0.85f;
        [Range(0.5f, 3f)] public float bloomThreshold = 0.98f;
        [Range(0f, 1f)] public float bloomScatter = 0.78f;
        [Range(0f, 1f)] public float chromaticAberrationIntensity = 0.10f;
        [Range(0f, 1f)] public float vignetteIntensity = 0.22f;
        public Color vignetteColor = new Color(0.02f, 0.04f, 0.08f, 1f);

        [Header("Color Grading Tuning")]
        public float postExposure = 0.40f;
        public float contrast = 10f;
        [Range(-100f, 100f)] public float saturation = -6f;
        [Range(0f, 1f)] public float colorLookupContribution = 0.25f;

        [Header("Anamorphic Horizontal Lens Flare")]
        public Material anamorphicFlareMaterial;
        public Light lightningFlashLight;
        public Transform flareAttachmentPoint;

        private GameObject anamorphicQuad;

        void OnEnable()
        {
            if (Instance == null || Instance == this)
                Instance = this;

            ApplyLightingAndFog();
            ApplyVolumeSettings();
            EnsureAnamorphicFlare();
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;

            if (anamorphicQuad != null)
            {
                if (Application.isPlaying) Destroy(anamorphicQuad);
                else DestroyImmediate(anamorphicQuad);
            }
        }

        void Update()
        {
            ApplyLightingAndFog();
            if (globalVolume != null && globalVolume.sharedProfile != null)
            {
                ApplyVolumeSettings();
            }
            UpdateAnamorphicFlare();
        }

        public void ApplyAtmosphericLightingAndFog() => ApplyLightingAndFog();
        public void ConfigureGlobalVolumeProfile() => ApplyVolumeSettings();
        public void EnsureAnamorphicFlareQuad() => EnsureAnamorphicFlare();

        public void ApplyLightingAndFog()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = stormSkyAmbient;
            RenderSettings.ambientEquatorColor = stormEquatorAmbient;
            RenderSettings.ambientGroundColor = stormGroundAmbient;

            RenderSettings.fog = enableDenseFog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor;

            if (mainDirectionalLight == null)
            {
                foreach (var lightComp in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                {
                    if (lightComp.type == LightType.Directional && lightComp.gameObject.name != "LightningFlashLight")
                    {
                        mainDirectionalLight = lightComp;
                        break;
                    }
                }
            }

            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.color = mainLightColor;
                mainDirectionalLight.intensity = mainLightIntensity;
            }
        }

        public void ApplyVolumeSettings()
        {
            if (globalVolume == null)
            {
                globalVolume = GetComponent<Volume>();
                if (globalVolume == null) globalVolume = Object.FindAnyObjectByType<Volume>();
            }

            if (globalVolume == null) return;
            var profile = globalVolume.sharedProfile != null ? globalVolume.sharedProfile : globalVolume.profile;
            if (profile == null) return;

            // 1. High-Threshold HDR Bloom with gentle sunray-like radial light scatter
            if (!profile.TryGet<Bloom>(out var bloom))
                bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(bloomThreshold);
            bloom.intensity.Override(bloomIntensity);
            bloom.scatter.Override(bloomScatter);
            bloom.tint.Override(new Color(0.85f, 0.94f, 1.0f, 1f));

            // 2. Subtle Chromatic Aberration
            if (!profile.TryGet<ChromaticAberration>(out var chroma))
                chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(chromaticAberrationIntensity);

            // 3. Balanced Vignette
            if (!profile.TryGet<Vignette>(out var vig))
                vig = profile.Add<Vignette>(true);
            vig.intensity.Override(vignetteIntensity);
            vig.smoothness.Override(0.38f);
            vig.rounded.Override(true);
            vig.color.Override(vignetteColor);

            // 4. Readable Cold Storm Color Grading
            if (!profile.TryGet<ColorAdjustments>(out var colorAdj))
                colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.Override(postExposure);
            colorAdj.contrast.Override(contrast);
            colorAdj.saturation.Override(saturation);
            colorAdj.colorFilter.Override(Color.white);

            // 5. Shadows / Midtones / Highlights (Neutral in URP is 1,1,1,0)
            if (!profile.TryGet<ShadowsMidtonesHighlights>(out var smh))
                smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.92f, 0.98f, 1.06f, 0.06f));
            smh.midtones.Override(new Vector4(0.96f, 1.00f, 1.04f, 0.02f));
            smh.highlights.Override(new Vector4(1.04f, 1.02f, 0.96f, 0.04f));

            // 6. Dial back Storm LUT contribution so shadows aren't crushed
            if (profile.TryGet<ColorLookup>(out var colorLookup))
            {
                colorLookup.contribution.Override(colorLookupContribution);
            }
        }

        private void EnsureAnamorphicFlare()
        {
            if (anamorphicQuad != null) return;

            if (anamorphicFlareMaterial == null)
            {
                Shader shader = Shader.Find("PacificRim/AnamorphicFlare");
                if (shader != null)
                {
                    anamorphicFlareMaterial = new Material(shader)
                    {
                        name = "PacificRim_AnamorphicFlare_Mat",
                        hideFlags = HideFlags.DontSave
                    };
                }
            }

            if (anamorphicFlareMaterial == null) return;

            anamorphicQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            anamorphicQuad.name = "AnamorphicHorizontalFlareQuad";
            anamorphicQuad.hideFlags = HideFlags.DontSave;

            var col = anamorphicQuad.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            var rend = anamorphicQuad.GetComponent<MeshRenderer>();
            rend.sharedMaterial = anamorphicFlareMaterial;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;

            if (lightningFlashLight == null)
            {
                var lightObj = GameObject.Find("LightningFlashLight");
                if (lightObj != null) lightningFlashLight = lightObj.GetComponent<Light>();
            }
        }

        private void UpdateAnamorphicFlare()
        {
            if (anamorphicQuad == null || anamorphicFlareMaterial == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Transform attach = flareAttachmentPoint;
            if (attach == null && lightningFlashLight != null) attach = lightningFlashLight.transform;
            if (attach == null) attach = cam.transform;

            Vector3 targetPos = attach.position + attach.forward * 45f;
            anamorphicQuad.transform.position = targetPos;
            anamorphicQuad.transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
            anamorphicQuad.transform.localScale = new Vector3(85f, 7.5f, 1f);

            float flashVal = Shader.GetGlobalFloat("_GlobalLightningFlash");
            anamorphicFlareMaterial.SetFloat("_FlashIntensity", Application.isPlaying ? Mathf.Max(flashVal, 0.05f) : 0f);
        }
    }
}
