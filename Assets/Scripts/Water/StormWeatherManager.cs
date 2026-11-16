using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Central Pacific Rim thunderstorm controller driving global shader wind/wetness vectors,
    /// camera-aligned screen-space rain lens refraction quad, and follower particle containers.
    /// </summary>
    [ExecuteAlways]
    public class StormWeatherManager : MonoBehaviour
    {
        public static StormWeatherManager Instance { get; private set; }

        [Header("Wind Vector Controller")]
        public Vector2 windDirection = new Vector2(0.85f, 0.52f);
        public float baseWindSpeed = 24.0f;
        public float gustStrength = 8.0f;
        public float gustFrequency = 0.35f;

        [Header("Surface Wetness")]
        [Range(0f, 1f)]
        public float rainWetness = 0.88f;

        [Header("Weather Elements")]
        public Transform followTarget;
        public ParticleSystem rainParticles;
        public ParticleSystem fogWispParticles;
        public ParticleSystem windMistParticles;
        public Transform rainLensQuad;

        public Vector4 CurrentWindVector { get; private set; }

        private static readonly int IdGlobalWindVector = Shader.PropertyToID("_GlobalWindVector");
        private static readonly int IdGlobalRainWetness = Shader.PropertyToID("_GlobalRainWetness");

        void OnEnable()
        {
            if (Instance == null || Instance == this)
                Instance = this;

            if (followTarget == null)
            {
                var jaeger = GameObject.Find("Jaeger_Mech");
                if (jaeger != null) followTarget = jaeger.transform;
            }

            PushWeatherGlobals();
            UpdateRainLensVisibility();
            BoostThinRainVisibility();
        }

        private void BoostThinRainVisibility()
        {
            if (rainParticles == null) return;

            var main = rainParticles.main;
            main.loop = true;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.25f);
            if (main.maxParticles < 20000)
            {
                main.maxParticles = 20000;
            }
            // Thin, atmospheric storm streaks that stay visible without obscuring combat
            main.startSize = new ParticleSystem.MinMaxCurve(0.065f, 0.115f);
            main.startColor = new Color(0.86f, 0.92f, 1.0f, 0.44f);

            var emission = rainParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = 4800f;

            var shape = rainParticles.shape;
            if (shape.enabled && shape.shapeType == ParticleSystemShapeType.Box)
            {
                shape.scale = new Vector3(150f, 150f, 22f);
            }

            var rend = rainParticles.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.enableGPUInstancing = false;
                rend.minParticleSize = 0.0012f;
                rend.velocityScale = 0.055f;
                rend.lengthScale = 5.5f;
            }

            if (Application.isPlaying)
            {
                rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                rainParticles.Play(true);
            }
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            PushWeatherGlobals();
            UpdateFollowerPosition();
            UpdateRainLensVisibility();

            if (Application.isPlaying && rainParticles != null && (rainParticles.isPaused || !rainParticles.isPlaying))
            {
                rainParticles.Play(true);
            }
        }

        void LateUpdate()
        {
            PositionRainLensInPlayMode();
        }

        private void PushWeatherGlobals()
        {
            Vector2 dir = windDirection.sqrMagnitude > 0.001f
                ? windDirection.normalized
                : new Vector2(0.85f, 0.52f).normalized;

            float t = Application.isPlaying ? Time.time : 0f;
            float gust01 = Application.isPlaying ? Mathf.PerlinNoise(t * gustFrequency, 1.37f) : 0.45f;
            float currentWindSpeed = baseWindSpeed + (gust01 - 0.5f) * 2.0f * gustStrength;

            CurrentWindVector = new Vector4(dir.x, dir.y, currentWindSpeed, gust01);
            Shader.SetGlobalVector(IdGlobalWindVector, CurrentWindVector);
            Shader.SetGlobalFloat(IdGlobalRainWetness, Mathf.Clamp01(rainWetness));
        }

        private void UpdateFollowerPosition()
        {
            if (!Application.isPlaying) return;

            Camera cam = Camera.main;
            if (cam != null)
            {
                // Center rain in front of the camera so foreground & mid-ground are densely drenched
                Vector3 fwdFlat = new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z);
                if (fwdFlat.sqrMagnitude > 0.001f) fwdFlat.Normalize();
                Vector3 center = cam.transform.position + fwdFlat * 32f;
                transform.position = new Vector3(center.x, 0f, center.z);
                return;
            }

            Transform target = followTarget;
            if (target == null) return;

            transform.position = new Vector3(target.position.x, 0f, target.position.z);
        }

        private void UpdateRainLensVisibility()
        {
            if (rainLensQuad == null) return;

            bool shouldShow = Application.isPlaying && rainWetness > 0.01f;
            if (rainLensQuad.gameObject.activeSelf != shouldShow)
            {
                rainLensQuad.gameObject.SetActive(shouldShow);
            }
        }

        private void PositionRainLensInPlayMode()
        {
            if (!Application.isPlaying || rainLensQuad == null || !rainLensQuad.gameObject.activeSelf)
                return;

            Camera cam = Camera.main;
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
