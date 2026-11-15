using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Controls the bioluminescent glowing cyan veins of the Kaiju.
    /// Manages balanced HDR emission intensity, organic breathing pulsations,
    /// EMP/Roar flare surges, ambient bioluminescent chest illumination, and death dimming.
    /// Works in both Edit mode and Play mode via [ExecuteAlways].
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class KaijuBioluminescenceController : MonoBehaviour
    {
        [Header("Emission Color & Intensity (Balanced Neon)")]
        [Tooltip("Base bioluminescent electric cyan color (HDR). Balanced for an obvious glow without blinding.")]
        [ColorUsage(true, true)]
        public Color baseEmissionColor = new Color(0.3f, 3.8f, 4.8f, 1f);

        [Tooltip("Minimum emission multiplier during idle breathing.")]
        public float minIntensity = 0.85f;

        [Tooltip("Maximum emission multiplier during idle breathing.")]
        public float maxIntensity = 1.15f;

        [Tooltip("Speed of the breathing pulse in radians/second.")]
        public float pulseFrequency = 1.35f;

        [Header("Combat & Surge Reactions")]
        [Tooltip("Emission multiplier surge when roaring or executing EMP blasts.")]
        public float surgeMultiplier = 1.6f;

        [Tooltip("Decay speed for surge flare back to normal pulse.")]
        public float surgeDecayRate = 2.0f;

        [Header("Ambient Water & Environment Glow")]
        [Tooltip("Optional real-time point light that casts soft cyan reflections on ocean water.")]
        public Light chestBioluminescentLight;
        public float maxLightIntensity = 1.8f;
        public float lightRange = 18f;

        [Header("References")]
        public Renderer targetRenderer;
        public KaijuAI kaijuAI;

        private MaterialPropertyBlock _propBlock;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private float _currentSurge = 0f;
        private bool _isDead = false;
        private float _deathFade = 1f;

        private void OnEnable()
        {
            InitializeReferences();
        }

        private void Awake()
        {
            InitializeReferences();
        }

        private void InitializeReferences()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
                if (targetRenderer == null)
                {
                    targetRenderer = GetComponentInChildren<MeshRenderer>();
                }
            }

            if (kaijuAI == null)
            {
                kaijuAI = GetComponent<KaijuAI>();
            }

            if (targetRenderer != null && targetRenderer.sharedMaterial != null)
            {
                targetRenderer.sharedMaterial.EnableKeyword("_EMISSION");
                targetRenderer.sharedMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (_propBlock == null)
            {
                _propBlock = new MaterialPropertyBlock();
            }

            // Setup or auto-create subtle cyan chest light if missing
            if (chestBioluminescentLight == null)
            {
                Transform existing = transform.Find("Kaiju_BioGlow_Light");
                if (existing != null)
                {
                    chestBioluminescentLight = existing.GetComponent<Light>();
                }
                else
                {
                    GameObject lightGo = new GameObject("Kaiju_BioGlow_Light");
                    lightGo.transform.SetParent(transform, false);
                    lightGo.transform.localPosition = new Vector3(0f, 14f, 3f); // Upper chest/throat height

                    chestBioluminescentLight = lightGo.AddComponent<Light>();
                    chestBioluminescentLight.type = LightType.Point;
                    chestBioluminescentLight.color = new Color(0.05f, 0.85f, 1f, 1f);
                    chestBioluminescentLight.shadows = LightShadows.None;
                }
            }

            if (chestBioluminescentLight != null)
            {
                chestBioluminescentLight.range = lightRange;
                chestBioluminescentLight.intensity = maxLightIntensity;
            }
        }

        private void Update()
        {
            if (targetRenderer == null) return;

            // Check if Kaiju has died
            if (kaijuAI != null && kaijuAI.currentHealth <= 0f)
            {
                _isDead = true;
            }

            if (_isDead)
            {
                _deathFade = Mathf.MoveTowards(_deathFade, 0f, Time.deltaTime * 0.35f);
            }

            // Calculate organic breathing pulse
            float timeVal = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartup;
            float pulse = Mathf.Sin(timeVal * pulseFrequency);
            float normalizedPulse = (pulse + 1f) * 0.5f; // 0 to 1
            float intensity = Mathf.Lerp(minIntensity, maxIntensity, normalizedPulse);

            // Handle surge flare (e.g. during EMP attacks or roars)
            if (_currentSurge > 0.01f)
            {
                intensity += _currentSurge * surgeMultiplier;
                _currentSurge = Mathf.MoveTowards(_currentSurge, 0f, Time.deltaTime * surgeDecayRate);
            }

            // Apply death fade
            intensity *= _deathFade;

            Color finalColor = baseEmissionColor * intensity;

            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(EmissionColorId, finalColor);
            targetRenderer.SetPropertyBlock(_propBlock);

            if (chestBioluminescentLight != null)
            {
                chestBioluminescentLight.intensity = (intensity / maxIntensity) * maxLightIntensity * _deathFade;
            }
        }

        /// <summary>
        /// Triggers a temporary bioluminescent brightness flare (e.g. during roars, heavy slams, or EMP blasts).
        /// </summary>
        public void TriggerSurge(float amount = 1f)
        {
            _currentSurge = Mathf.Max(_currentSurge, amount);
        }

        /// <summary>
        /// Manually sets whether the Kaiju is dead to fade out the bioluminescence.
        /// </summary>
        public void SetDead(bool dead)
        {
            _isDead = dead;
        }
    }
}
