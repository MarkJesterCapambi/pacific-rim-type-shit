using System.Collections;
using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Manages Gipsy Danger's rigged visual sockets:
    /// - Head visor searchlight beam
    /// - Dual chest searchlight beams with atmospheric volumetric light cones
    /// - Twin back rocket engines with realistic cyan-blue supersonic thruster blast VFX
    ///   activated during sprint-heavy jump/leap attacks.
    /// </summary>
    [ExecuteAlways]
    public class JaegerVisualSockets : MonoBehaviour
    {
        [Header("Rig Bones")]
        public Transform headBone;
        public Transform chestBone;

        [Header("Light Beam Origins")]
        public Transform headLightOrigin;
        public Transform torsoLightOriginL;
        public Transform torsoLightOriginR;

        [Header("Rocket Engine Origins")]
        public Transform rocketOriginL;
        public Transform rocketOriginR;

        [Header("Runtime References")]
        [SerializeField] private ParticleSystem[] rocketSystemsL;
        [SerializeField] private ParticleSystem[] rocketSystemsR;
        [SerializeField] private Light rocketLightL;
        [SerializeField] private Light rocketLightR;

        [Header("Volumetric Light Components")]
        [SerializeField] private Light headSpotLight;
        [SerializeField] private Light torsoSpotLightL;
        [SerializeField] private Light torsoSpotLightR;

        public bool IsRocketBlasting { get; private set; }

        private Coroutine activeBlastRoutine;
        private float originalLightIntensityL = 22000f;
        private float originalLightIntensityR = 22000f;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            SetRocketBlastActive(false, immediate: true);
        }

        public void ResolveReferences()
        {
            if (chestBone == null || headBone == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Head" && t.parent != null && t.parent.name == "Chest") headBone = t;
                    else if (t.name == "Chest" && t.parent != null && t.parent.name == "Spine1") chestBone = t;
                }
            }

            if (headLightOrigin == null || torsoLightOriginL == null || torsoLightOriginR == null ||
                rocketOriginL == null || rocketOriginR == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "LightBeamOriginHead") headLightOrigin = t;
                    else if (t.name == "LightBeamOriginUpperTorso.L" || (t.name == "LightBeamOriginUpperTorso" && t.localPosition.x < 0)) torsoLightOriginL = t;
                    else if (t.name == "LightBeamOriginUpperTorso.R" || (t.name == "LightBeamOriginUpperTorso" && t.localPosition.x >= 0)) torsoLightOriginR = t;
                    else if (t.name == "RocketEngineOrigin.L" || (t.name.StartsWith("RocketEngineOrigin") && t.localPosition.x < 0)) rocketOriginL = t;
                    else if (t.name == "RocketEngineOrigin.R" || (t.name.StartsWith("RocketEngineOrigin") && t.localPosition.x >= 0)) rocketOriginR = t;
                }
            }

            if (rocketOriginL != null)
            {
                rocketSystemsL = rocketOriginL.GetComponentsInChildren<ParticleSystem>(true);
                rocketLightL = rocketOriginL.GetComponentInChildren<Light>(true);
                if (rocketLightL != null) originalLightIntensityL = rocketLightL.intensity;
            }

            if (rocketOriginR != null)
            {
                rocketSystemsR = rocketOriginR.GetComponentsInChildren<ParticleSystem>(true);
                rocketLightR = rocketOriginR.GetComponentInChildren<Light>(true);
                if (rocketLightR != null) originalLightIntensityR = rocketLightR.intensity;
            }
        }

        /// <summary>
        /// Ignites the twin back rocket thrusters for the specified duration (e.g. during a sprint-heavy jump attack).
        /// </summary>
        public void TriggerRocketBlast(float duration)
        {
            if (!gameObject.activeInHierarchy) return;

            if (activeBlastRoutine != null)
            {
                StopCoroutine(activeBlastRoutine);
            }
            activeBlastRoutine = StartCoroutine(RocketBlastRoutine(Mathf.Max(0.2f, duration)));
        }

        private IEnumerator RocketBlastRoutine(float duration)
        {
            IsRocketBlasting = true;
            SetRocketBlastActive(true);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                // Combustion flicker on thruster lights
                float flicker = 0.82f + 0.36f * Mathf.PerlinNoise(Time.time * 38f, 0.5f);
                if (rocketLightL != null) rocketLightL.intensity = originalLightIntensityL * flicker;
                if (rocketLightR != null) rocketLightR.intensity = originalLightIntensityR * flicker;

                yield return null;
            }

            SetRocketBlastActive(false);
            IsRocketBlasting = false;
            activeBlastRoutine = null;
        }

        public void SetRocketBlastActive(bool active, bool immediate = false)
        {
            if (rocketSystemsL != null)
            {
                for (int i = 0; i < rocketSystemsL.Length; i++)
                {
                    if (rocketSystemsL[i] == null) continue;
                    if (active)
                    {
                        rocketSystemsL[i].Play(true);
                    }
                    else
                    {
                        if (immediate) rocketSystemsL[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        else rocketSystemsL[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }

            if (rocketSystemsR != null)
            {
                for (int i = 0; i < rocketSystemsR.Length; i++)
                {
                    if (rocketSystemsR[i] == null) continue;
                    if (active)
                    {
                        rocketSystemsR[i].Play(true);
                    }
                    else
                    {
                        if (immediate) rocketSystemsR[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        else rocketSystemsR[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }

            if (rocketLightL != null)
            {
                rocketLightL.enabled = active;
                if (!active) rocketLightL.intensity = originalLightIntensityL;
            }

            if (rocketLightR != null)
            {
                rocketLightR.enabled = active;
                if (!active) rocketLightR.intensity = originalLightIntensityR;
            }
        }
    }
}
