using System;
using System.Collections.Generic;
using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Makes the ocean react to the Jaeger's body. When tall waves rise into the torso, hips, arms, hands or
    /// thighs, spray erupts from the armor surface (spawned outside the body and clip-guarded). Also makes the
    /// biggest wave crests around the Jaeger bubble and break into foam.
    /// </summary>
    public class JaegerWaveBodyInteraction : MonoBehaviour
    {
        [Serializable]
        public class BodyProbe
        {
            public string label;
            public Transform bone;
            [Tooltip("Approximate armor radius at this bone (meters).")]
            public float bodyRadius = 2f;
            [Tooltip("Splash size multiplier for this body part.")]
            public float sizeScale = 1f;
            [Tooltip("Scales how strongly this part reacts (thighs react less than the chest).")]
            public float sensitivity = 1f;

            [NonSerialized] public bool initialized;
            [NonSerialized] public float prevWaterY;
            [NonSerialized] public Vector3 prevBonePos;
            [NonSerialized] public float lastSplashTime = -10f;
        }

        [Header("Wave vs. Body Splashes")]
        public bool enableBodySplashes = true;
        public List<BodyProbe> probes = new List<BodyProbe>();
        [Tooltip("Minimum seconds between splashes on the same body part.")]
        public float hitCooldown = 0.38f;
        [Tooltip("Minimum relative water speed (m/s) hitting a body part to trigger spray.")]
        public float minImpactSpeed = 0.85f;
        [Tooltip("Relative speed (m/s) that produces a maximum-intensity splash.")]
        public float maxImpactSpeed = 4.5f;
        [Tooltip("Wave height above the mean sea level required before waves count as 'big' (meters).")]
        public float bigWaveStartHeight = 1.95f;
        [Tooltip("Wave height above the mean sea level at which splashes reach full strength (meters).")]
        public float bigWaveFullHeight = 5.40f;

        [Header("Bubbling Wave Crests")]
        public bool enableCrestBubbles = true;
        [Range(2, 40)] public int crestSamplesPerFrame = 14;
        public float crestMinRadius = 6f;
        public float crestMaxRadius = 75f;
        [Tooltip("Wave height above mean sea level where crests start to bubble (meters).")]
        public float crestBubbleStartHeight = 1.6f;
        [Tooltip("Wave height above mean sea level where crests bubble at full intensity (meters).")]
        public float crestBubbleFullHeight = 5.2f;
        [Range(0f, 1f)] public float crestSpawnChance = 0.55f;

        void Awake()
        {
            if (probes == null || probes.Count == 0) AutoBuildProbes();
        }

        [ContextMenu("Auto Build Probes")]
        public void AutoBuildProbes()
        {
            probes = new List<BodyProbe>();

            if (FindBone("Chest") != null || FindBone("Hips") != null)
            {
                AddProbe("UpperTorso.L", "Upperarm.L", 2.4f, 1.40f, 1.25f);
                AddProbe("UpperTorso.R", "Upperarm.R", 2.4f, 1.40f, 1.25f);
                AddProbe("Chest", "Chest", 3.2f, 1.55f, 1.30f);
                AddProbe("Arm.L", "Upperarm.L", 1.8f, 1.10f, 1.0f);
                AddProbe("Arm.R", "Upperarm.R", 1.8f, 1.10f, 1.0f);
                AddProbe("Hand.L", "Hand.L", 1.7f, 1.0f, 0.95f);
                AddProbe("Hand.R", "Hand.R", 1.7f, 1.0f, 0.95f);
                AddProbe("Hips", "Hips", 2.8f, 1.15f, 0.85f);
                return;
            }

            if (FindBone("UpperTorsoBone") != null || FindBone("LowerTorsoBone") != null)
            {
                AddProbe("KaijuUpperTorso", "UpperTorsoBone", 4.8f, 1.70f, 1.30f);
                AddProbe("KaijuClavicle", "ClavicleBone", 4.2f, 1.55f, 1.20f);
                AddProbe("KaijuBicep.L", "BicepBone.L", 3.2f, 1.40f, 1.15f);
                AddProbe("KaijuBicep.R", "BicepBone.R", 3.2f, 1.40f, 1.15f);
                AddProbe("KaijuForearm.L", "ForearmBone.L", 2.8f, 1.45f, 1.15f);
                AddProbe("KaijuForearm.R", "ForearmBone.R", 2.8f, 1.45f, 1.15f);
                AddProbe("KaijuHand.L", "HandBone.L", 2.5f, 1.25f, 1.05f);
                AddProbe("KaijuHand.R", "HandBone.R", 2.5f, 1.25f, 1.05f);
                AddProbe("KaijuUpperLeg.L", "UpperLegBone.L", 3.2f, 1.35f, 1.10f);
                AddProbe("KaijuUpperLeg.R", "UpperLegBone.R", 3.2f, 1.35f, 1.10f);
                AddProbe("KaijuLowerTorso", "LowerTorsoBone", 4.0f, 1.45f, 0.95f);
            }
        }

        private Transform FindBone(string boneName)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == boneName) return t;
            }
            return null;
        }

        private void AddProbe(string label, string boneName, float radius, float size, float sensitivity)
        {
            Transform bone = FindBone(boneName);
            if (bone == null) return;
            probes.Add(new BodyProbe { label = label, bone = bone, bodyRadius = radius, sizeScale = size, sensitivity = sensitivity });
        }

        void LateUpdate()
        {
            WaterSystem water = WaterSystem.Instance;
            WaterSplashManager splash = WaterSplashManager.Instance;
            if (water == null || splash == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;

            if (enableBodySplashes) UpdateBodyProbes(water, splash, dt);
            if (enableCrestBubbles) UpdateCrestBubbles(water, splash, dt);
        }

        private void UpdateBodyProbes(WaterSystem water, WaterSplashManager splash, float dt)
        {
            Vector2 waveDir2 = water.DominantWaveDirection;
            Vector3 waveVel = new Vector3(waveDir2.x, 0f, waveDir2.y) * (water.DominantPhaseSpeed * 0.55f);
            float meanY = water.MeanSurfaceY;
            float now = Time.time;

            for (int i = 0; i < probes.Count; i++)
            {
                BodyProbe p = probes[i];
                if (p == null || p.bone == null) continue;

                Vector3 bonePos = p.bone.position;
                float waterY = water.GetWaterHeight(bonePos);

                if (!p.initialized)
                {
                    p.initialized = true;
                    p.prevWaterY = waterY;
                    p.prevBonePos = bonePos;
                    continue;
                }

                float waterVy = (waterY - p.prevWaterY) / dt;
                Vector3 boneVel = (bonePos - p.prevBonePos) / dt;
                p.prevWaterY = waterY;
                p.prevBonePos = bonePos;

                // Skip teleports / animation resets
                if (boneVel.sqrMagnitude > 60f * 60f) continue;

                // Water must be touching this part of the armor (surface within its vertical span)
                float r = p.bodyRadius;
                bool touching = waterY > bonePos.y - r * 0.95f && waterY < bonePos.y + r * 0.95f;
                if (!touching) continue;
                if (now - p.lastSplashTime < hitCooldown) continue;

                float waveHeight = waterY - meanY;
                float bigWave = Mathf.InverseLerp(bigWaveStartHeight, bigWaveFullHeight, waveHeight);
                if (bigWave <= 0.02f) continue;

                // Relative water motion against the body: rising crests + horizontal sweep into the armor
                Vector3 boneHoriz = new Vector3(boneVel.x, 0f, boneVel.z);
                Vector3 relHoriz = waveVel - boneHoriz;
                float relRise = Mathf.Max(0f, waterVy - boneVel.y);
                float impact = relRise + relHoriz.magnitude * 0.22f + bigWave * 0.65f;

                float strength = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, impact);
                if (impact < minImpactSpeed) continue;

                float intensity = Mathf.Clamp01((0.25f + strength * 0.75f) * Mathf.Lerp(0.45f, 1f, bigWave) * p.sensitivity);
                if (intensity < 0.12f) continue;

                // Contact face: the side of the body the water is flowing into
                Vector3 normal = -relHoriz;
                normal.y = 0f;
                if (normal.sqrMagnitude < 0.04f) normal = new Vector3(-waveDir2.x, 0f, -waveDir2.y);
                normal.Normalize();

                Vector3 contact = new Vector3(bonePos.x, waterY, bonePos.z) + normal * (r + 0.3f);

                splash.SpawnBodySplash(contact, normal, intensity, p.sizeScale);
                if (bigWave >= 0.45f)
                {
                    splash.SpawnFootstepSplash(contact, Mathf.Clamp(p.sizeScale * Mathf.Lerp(0.65f, 1.25f, bigWave), 0.5f, 1.75f));
                }
                p.lastSplashTime = now;
            }
        }

        private void UpdateCrestBubbles(WaterSystem water, WaterSplashManager splash, float dt)
        {
            float meanY = water.MeanSurfaceY;
            Vector3 center = transform.position;
            float frameScale = Mathf.Clamp(dt * 60f, 0.25f, 3f);

            for (int i = 0; i < crestSamplesPerFrame; i++)
            {
                float ang = UnityEngine.Random.value * Mathf.PI * 2f;
                float radius = Mathf.Lerp(crestMinRadius, crestMaxRadius, Mathf.Sqrt(UnityEngine.Random.value));
                Vector3 p = new Vector3(center.x + Mathf.Cos(ang) * radius, 0f, center.z + Mathf.Sin(ang) * radius);

                float h = water.GetWaterHeight(p);
                float waveHeight = h - meanY;
                if (waveHeight < crestBubbleStartHeight) continue;

                float intensity = Mathf.InverseLerp(crestBubbleStartHeight, crestBubbleFullHeight, waveHeight);
                if (UnityEngine.Random.value > intensity * crestSpawnChance * frameScale) continue;

                splash.SpawnCrestFoam(new Vector3(p.x, h, p.z), intensity);
            }
        }
    }
}
