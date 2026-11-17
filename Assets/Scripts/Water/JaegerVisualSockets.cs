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
    /// - Cutscene volumetric searchlight beam extension and smooth splashdown tweening.
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
        [SerializeField] private MeshRenderer exhaustMeshRendererL;
        [SerializeField] private MeshRenderer exhaustMeshRendererR;

        private Vector3 rocketBoneOffsetL = new Vector3(-0.003062f, 0.001459f, -0.002963f);
        private Quaternion rocketBoneRotL = new Quaternion(0.198929f, 0.806624f, -0.486027f, 0.271224f);
        private Vector3 rocketBoneOffsetR = new Vector3(0.003046f, 0.001429f, -0.003046f);
        private Quaternion rocketBoneRotR = new Quaternion(-0.230915f, 0.797251f, -0.490104f, -0.266208f);

        [Header("Volumetric Light Components")]
        [SerializeField] private Light headSpotLight;
        [SerializeField] private Light torsoSpotLightL;
        [SerializeField] private Light torsoSpotLightR;
        [SerializeField] private Transform headBeamTransform;
        [SerializeField] private Transform torsoBeamTransformL;
        [SerializeField] private Transform torsoBeamTransformR;

        [Header("Cutscene Visual Overrides")]
        private Coroutine activeCutsceneBeamTween;
        private float baseHeadLightRange = 85f;
        private float baseTorsoLightRangeL = 75f;
        private float baseTorsoLightRangeR = 75f;
        private Vector3 baseHeadBeamScale = new Vector3(0.85f, 0.85f, 1.05f);
        private Vector3 baseTorsoBeamScaleL = Vector3.one;
        private Vector3 baseTorsoBeamScaleR = Vector3.one;

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

        private void LateUpdate()
        {
            UpdateRocketTransforms();
        }

        public void UpdateRocketTransforms()
        {
            if (chestBone == null) return;

            if (rocketOriginL != null && rocketOriginL.parent != chestBone)
            {
                rocketOriginL.position = chestBone.TransformPoint(rocketBoneOffsetL);
                rocketOriginL.rotation = chestBone.rotation * rocketBoneRotL;
            }

            if (rocketOriginR != null && rocketOriginR.parent != chestBone)
            {
                rocketOriginR.position = chestBone.TransformPoint(rocketBoneOffsetR);
                rocketOriginR.rotation = chestBone.rotation * rocketBoneRotR;
            }
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

            // Bone scale isolation: unparent rockets from the shrunken DCC bone hierarchy to the Jaeger root
            // so particle systems, light ranges, and thruster meshes operate at true 1-meter world scale.
            if (chestBone != null)
            {
                if (rocketOriginL != null && rocketOriginL.parent == chestBone)
                {
                    rocketBoneOffsetL = rocketOriginL.localPosition;
                    rocketBoneRotL = rocketOriginL.localRotation;
                    rocketOriginL.SetParent(transform, true);
                    rocketOriginL.localScale = Vector3.one;
                }
                if (rocketOriginR != null && rocketOriginR.parent == chestBone)
                {
                    rocketBoneOffsetR = rocketOriginR.localPosition;
                    rocketBoneRotR = rocketOriginR.localRotation;
                    rocketOriginR.SetParent(transform, true);
                    rocketOriginR.localScale = Vector3.one;
                }
                UpdateRocketTransforms();
            }

            if (rocketOriginL != null)
            {
                rocketSystemsL = rocketOriginL.GetComponentsInChildren<ParticleSystem>(true);
                rocketLightL = rocketOriginL.GetComponentInChildren<Light>(true);
                if (rocketLightL != null)
                {
                    if (rocketLightL.intensity > 100f) originalLightIntensityL = rocketLightL.intensity;
                    else originalLightIntensityL = 4800f;
                }
                var emL = rocketOriginL.Find("ExhaustBeamMesh");
                if (emL != null) exhaustMeshRendererL = emL.GetComponent<MeshRenderer>();
            }

            if (rocketOriginR != null)
            {
                rocketSystemsR = rocketOriginR.GetComponentsInChildren<ParticleSystem>(true);
                rocketLightR = rocketOriginR.GetComponentInChildren<Light>(true);
                if (rocketLightR != null)
                {
                    if (rocketLightR.intensity > 100f) originalLightIntensityR = rocketLightR.intensity;
                    else originalLightIntensityR = 4800f;
                }
                var emR = rocketOriginR.Find("ExhaustBeamMesh");
                if (emR != null) exhaustMeshRendererR = emR.GetComponent<MeshRenderer>();
            }

            if (headLightOrigin != null)
            {
                headSpotLight = headLightOrigin.GetComponent<Light>();
                if (headSpotLight != null) baseHeadLightRange = headSpotLight.range;
                headBeamTransform = headLightOrigin.Find("VolumetricBeam");
                if (headBeamTransform != null) baseHeadBeamScale = headBeamTransform.localScale;
            }

            if (torsoLightOriginL != null)
            {
                torsoSpotLightL = torsoLightOriginL.GetComponent<Light>();
                if (torsoSpotLightL != null) baseTorsoLightRangeL = torsoSpotLightL.range;
                torsoBeamTransformL = torsoLightOriginL.Find("VolumetricBeam");
                if (torsoBeamTransformL != null) baseTorsoBeamScaleL = torsoBeamTransformL.localScale;
            }

            if (torsoLightOriginR != null)
            {
                torsoSpotLightR = torsoLightOriginR.GetComponent<Light>();
                if (torsoSpotLightR != null) baseTorsoLightRangeR = torsoSpotLightR.range;
                torsoBeamTransformR = torsoLightOriginR.Find("VolumetricBeam");
                if (torsoBeamTransformR != null) baseTorsoBeamScaleR = torsoBeamTransformR.localScale;
            }
        }

        /// <summary>
        /// Scales Gipsy's volumetric searchlight beams and spotlight ranges.
        /// Used during the aerial convoy cutscene for colossal beam distance piercing the stormy skies,
        /// then smoothly tweened back to standard values on splashdown.
        /// </summary>
        public void SetCutsceneBeamMultiplier(float multiplier)
        {
            multiplier = Mathf.Max(0.1f, multiplier);

            if (headBeamTransform != null)
            {
                headBeamTransform.localScale = new Vector3(
                    baseHeadBeamScale.x * Mathf.Lerp(1f, 1.4f, (multiplier - 1f) / 2.5f),
                    baseHeadBeamScale.y * Mathf.Lerp(1f, 1.4f, (multiplier - 1f) / 2.5f),
                    baseHeadBeamScale.z * multiplier
                );
            }
            if (torsoBeamTransformL != null)
            {
                torsoBeamTransformL.localScale = new Vector3(
                    baseTorsoBeamScaleL.x * Mathf.Lerp(1f, 1.4f, (multiplier - 1f) / 2.5f),
                    baseTorsoBeamScaleL.y * Mathf.Lerp(1f, 1.4f, (multiplier - 1f) / 2.5f),
                    baseTorsoBeamScaleL.z * multiplier
                );
            }
            if (torsoBeamTransformR != null)
            {
                torsoBeamTransformR.localScale = new Vector3(
                    baseTorsoBeamScaleR.x * Mathf.Lerp(1f, 1.4f, (multiplier - 1f) / 2.5f),
                    baseTorsoBeamScaleR.y * Mathf.Lerp(1f, 1.4f, (multiplier - 1f) / 2.5f),
                    baseTorsoBeamScaleR.z * multiplier
                );
            }

            if (headSpotLight != null) headSpotLight.range = baseHeadLightRange * multiplier;
            if (torsoSpotLightL != null) torsoSpotLightL.range = baseTorsoLightRangeL * multiplier;
            if (torsoSpotLightR != null) torsoSpotLightR.range = baseTorsoLightRangeR * multiplier;
        }

        /// <summary>
        /// Toggles visibility of Gipsy's 3D volumetric light cone meshes while leaving lights intact.
        /// Used during silhouette-focused cutscene shots so opaque beam meshes do not obscure armor framing.
        /// </summary>
        public void SetVolumetricConesVisible(bool visible)
        {
            SetConeTransformVisible(headBeamTransform, visible);
            SetConeTransformVisible(torsoBeamTransformL, visible);
            SetConeTransformVisible(torsoBeamTransformR, visible);
        }

        private static void SetConeTransformVisible(Transform coneTransform, bool visible)
        {
            if (coneTransform == null) return;
            var renderers = coneTransform.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = visible;
            }
        }

        public Coroutine TweenBeamMultiplier(float from, float to, float duration)
        {
            if (activeCutsceneBeamTween != null)
            {
                StopCoroutine(activeCutsceneBeamTween);
            }
            if (gameObject.activeInHierarchy)
            {
                activeCutsceneBeamTween = StartCoroutine(CutsceneBeamTweenRoutine(from, to, duration));
                return activeCutsceneBeamTween;
            }
            else
            {
                SetCutsceneBeamMultiplier(to);
                return null;
            }
        }

        private IEnumerator CutsceneBeamTweenRoutine(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                float current = Mathf.Lerp(from, to, smoothT);
                SetCutsceneBeamMultiplier(current);
                yield return null;
            }
            SetCutsceneBeamMultiplier(to);
            activeCutsceneBeamTween = null;
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
            if (exhaustMeshRendererL != null) exhaustMeshRendererL.enabled = active;
            if (exhaustMeshRendererR != null) exhaustMeshRendererR.enabled = active;

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
