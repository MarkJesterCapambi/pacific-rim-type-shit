using UnityEngine;
using UnityEngine.Rendering;

namespace PacificRimAtmosphere
{
    /// <summary>
    /// Controls the screen-space camera lens rain droplets and downward dripping streaks.
    /// Exposes fine-grained intensity controls and only enables the overlay quad in Play Mode.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(75)]
    public class CameraLensRainController : MonoBehaviour
    {
        [Header("Intensity Controls")]
        [Range(0f, 2f)]
        [Tooltip("Master screen lens rain effect intensity.")]
        public float lensRainIntensity = 1.0f;

        [Range(0f, 2f)]
        [Tooltip("Density of beaded condensation and sliding droplets.")]
        public float dropletDensity = 1.0f;

        [Range(0f, 2f)]
        [Tooltip("Intensity of downward dripping rivulets running down the lens.")]
        public float downwardStreaksIntensity = 1.25f;

        [Range(0f, 0.05f)]
        [Tooltip("Refraction distortion strength warping the scene behind the glass.")]
        public float distortionStrength = 0.004f;

        [Range(0f, 1f)]
        [Tooltip("Base opacity of the wet sheen on the camera lens.")]
        public float baseWetOpacity = 0.08f;

        [Header("Camera Angle Dynamics")]
        [Tooltip("Increase droplet accumulation when tilting the camera upward into the storm sky.")]
        public bool lookUpDrenching = true;
        [Range(0f, 2f)]
        public float lookUpDrenchMultiplier = 1.45f;

        [Header("Components")]
        public Material rainLensMaterial;
        public Transform rainLensQuad;

        private static readonly int IdCameraLensRainOverride = Shader.PropertyToID("_CameraLensRainOverride");
        private static readonly int IdDistortionStrength = Shader.PropertyToID("_DistortionStrength");
        private static readonly int IdDropletIntensity = Shader.PropertyToID("_DropletIntensity");
        private static readonly int IdStreakIntensity = Shader.PropertyToID("_StreakIntensity");
        private static readonly int IdWetAlpha = Shader.PropertyToID("_WetAlpha");
        private static readonly int IdSplashImpactAmount = Shader.PropertyToID("_SplashImpactAmount");
        private static readonly int IdSplashImpactAge = Shader.PropertyToID("_SplashImpactAge");

        private Camera targetCamera;
        private float splashImpactPeak = 0f;
        private float splashImpactAge = 0f;
        private float splashImpactDuration = 6.5f;
        private bool hasActiveSplash = false;

        /// <summary>
        /// Triggers a burst of heavy water droplets splashing onto the camera lens
        /// that slowly bead and streak down over <paramref name="duration"/> seconds.
        /// </summary>
        public void TriggerLensWaterSplash(float intensity = 1.25f, float duration = 7.0f)
        {
            splashImpactPeak = Mathf.Clamp(intensity, 0f, 2f);
            splashImpactDuration = Mathf.Max(0.5f, duration);
            splashImpactAge = 0f;
            hasActiveSplash = splashImpactPeak > 0.01f;
            ApplyProperties();
        }

        public void ClearLensWaterSplash()
        {
            hasActiveSplash = false;
            splashImpactPeak = 0f;
            splashImpactAge = 0f;
            ApplyProperties();
        }

        void OnEnable()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (targetCamera == null) targetCamera = Camera.main;

            RebuildLensQuad();
            ApplyProperties();
        }

        void OnDisable()
        {
            Shader.SetGlobalFloat(IdCameraLensRainOverride, 0f);
        }

        void Update()
        {
            if (hasActiveSplash && Application.isPlaying)
            {
                splashImpactAge += Time.deltaTime;
                if (splashImpactAge >= splashImpactDuration)
                {
                    hasActiveSplash = false;
                    splashImpactPeak = 0f;
                }
            }
            ApplyProperties();
            PositionQuadInFrontOfCamera();
        }

        void LateUpdate()
        {
            PositionQuadInFrontOfCamera();
        }

        public void RebuildLensQuad()
        {
            if (rainLensQuad == null)
            {
                var found = GameObject.Find("ScreenSpaceRainLensQuad");
                if (found != null) rainLensQuad = found.transform;
            }

            if (rainLensQuad == null)
            {
                var quadObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quadObj.name = "ScreenSpaceRainLensQuad";
                var col = quadObj.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
                rainLensQuad = quadObj.transform;
            }

            if (rainLensMaterial == null && rainLensQuad != null)
            {
                var existingRend = rainLensQuad.GetComponent<Renderer>();
                if (existingRend != null && existingRend.sharedMaterial != null)
                {
                    rainLensMaterial = existingRend.sharedMaterial;
                }
            }

#if UNITY_EDITOR
            if (rainLensMaterial == null)
            {
                rainLensMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PacificRim_RainLens.mat");
            }
#endif

            if (rainLensMaterial == null)
            {
                rainLensMaterial = Resources.Load<Material>("PacificRim_RainLens");
            }

            if (rainLensMaterial == null)
            {
                Shader shader = Shader.Find("PacificRim/RainLens");
                if (shader != null)
                {
                    rainLensMaterial = new Material(shader) { name = "PacificRim_RainLens_Mat" };
                }
            }

            if (rainLensQuad != null && rainLensMaterial != null)
            {
                var rend = rainLensQuad.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.sharedMaterial = rainLensMaterial;
                    rend.shadowCastingMode = ShadowCastingMode.Off;
                    rend.receiveShadows = false;
                }
            }

            ApplyProperties();
            PositionQuadInFrontOfCamera();
        }

        public void ForceSyncNow()
        {
            ApplyProperties();
            PositionQuadInFrontOfCamera();
        }

        private void ApplyProperties()
        {
            float pitchMultiplier = 1.0f;
            if (lookUpDrenching && targetCamera != null)
            {
                float upDot = Mathf.Clamp01(Vector3.Dot(targetCamera.transform.forward, Vector3.up));
                pitchMultiplier = Mathf.Lerp(1.0f, lookUpDrenchMultiplier, upDot);
            }

            float finalIntensity = Mathf.Clamp(lensRainIntensity * pitchMultiplier, 0f, 2.5f);
            Shader.SetGlobalFloat(IdCameraLensRainOverride, Application.isPlaying ? finalIntensity : 0f);

            if (rainLensMaterial != null)
            {
                rainLensMaterial.SetFloat(IdDistortionStrength, distortionStrength * finalIntensity);
                rainLensMaterial.SetFloat(IdDropletIntensity, dropletDensity * finalIntensity);
                rainLensMaterial.SetFloat(IdStreakIntensity, downwardStreaksIntensity * finalIntensity);
                rainLensMaterial.SetFloat(IdWetAlpha, Mathf.Clamp01(baseWetOpacity * finalIntensity));

                float splashFade = hasActiveSplash && Application.isPlaying
                    ? splashImpactPeak * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(splashImpactAge / splashImpactDuration)))
                    : 0f;
                if (rainLensMaterial.HasProperty(IdSplashImpactAmount))
                    rainLensMaterial.SetFloat(IdSplashImpactAmount, splashFade);
                if (rainLensMaterial.HasProperty(IdSplashImpactAge))
                    rainLensMaterial.SetFloat(IdSplashImpactAge, splashImpactAge);
            }

            if (rainLensQuad != null)
            {
                bool shouldShow = Application.isPlaying && (finalIntensity > 0.01f || hasActiveSplash);
                if (rainLensQuad.gameObject.activeSelf != shouldShow)
                {
                    rainLensQuad.gameObject.SetActive(shouldShow);
                }
            }
        }

        private void PositionQuadInFrontOfCamera()
        {
            if (rainLensQuad == null || !rainLensQuad.gameObject.activeSelf) return;

            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            float dist = cam.nearClipPlane + 0.08f;
            float halfHeight = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.08f;
            float fullHeight = halfHeight * 2.0f;
            float fullWidth = fullHeight * cam.aspect;

            rainLensQuad.position = cam.transform.position + cam.transform.forward * dist;
            rainLensQuad.rotation = cam.transform.rotation;
            rainLensQuad.localScale = new Vector3(fullWidth, fullHeight, 1f);
        }
    }
}
