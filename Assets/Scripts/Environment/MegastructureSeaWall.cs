using UnityEngine;

namespace PacificRimEnvironment
{
    /// <summary>
    /// Procedurally constructs and manages the distant coastal megastructure / sea wall.
    /// Features:
    /// - Massive, dark silhouetted concrete defensive seawall and gantry crane silhouettes in the fog.
    /// - Gridded industrial floodlights piercing through storm mist.
    /// - Slow-blinking red aviation hazard beacons along the parapet crest.
    /// </summary>
    [ExecuteAlways]
    public class MegastructureSeaWall : MonoBehaviour
    {
        [Header("Structure Placement & Dimensions")]
        public float distanceFromOrigin = 280f;
        public float wallElevation = 38f;
        public float wallWidth = 520f;
        public float wallThickness = 32f;
        public float wallHeight = 85f;

        [Header("Hazard Beacons")]
        public Color beaconColor = new Color(1.0f, 0.15f, 0.18f, 1f);
        public float beaconBlinkFrequency = 0.85f;
        public float beaconIntensity = 4.5f;

        [Header("Floodlights")]
        public Color floodlightColor = new Color(0.65f, 0.82f, 1.0f, 1f);
        public float floodlightIntensity = 5.5f;
        public float floodlightRange = 160f;

        private Material concreteMat;
        private Material beaconEmissiveMat;
        private Material floodlightLensMat;
        private Light[] floodlights;
        private Light[] beaconLights;

        void OnEnable()
        {
            EnsureMaterials();
            BuildStructureIfMissing();
        }

        private void EnsureMaterials()
        {
            if (concreteMat == null)
            {
                concreteMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                {
                    name = "Megastructure_Concrete_Mat",
                    color = new Color(0.08f, 0.11f, 0.14f, 1f)
                };
                concreteMat.SetFloat("_Smoothness", 0.18f);
                concreteMat.SetFloat("_Metallic", 0.25f);
            }

            if (beaconEmissiveMat == null)
            {
                beaconEmissiveMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                {
                    name = "Megastructure_BeaconEmissive_Mat",
                    color = beaconColor
                };
                beaconEmissiveMat.EnableKeyword("_EMISSION");
                beaconEmissiveMat.SetColor("_EmissionColor", beaconColor * 4.0f);
            }

            if (floodlightLensMat == null)
            {
                floodlightLensMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                {
                    name = "Megastructure_FloodlightLens_Mat",
                    color = floodlightColor
                };
                floodlightLensMat.EnableKeyword("_EMISSION");
                floodlightLensMat.SetColor("_EmissionColor", floodlightColor * 3.5f);
            }
        }

        public void BuildSeaWall() => BuildStructureIfMissing();

        public void BuildStructureIfMissing()
        {
            EnsureMaterials();
            if (transform.childCount > 0) return;

            transform.position = new Vector3(0f, wallElevation, distanceFromOrigin);
            transform.rotation = Quaternion.identity;

            int sections = 7;
            float sectionWidth = wallWidth / sections;
            for (int i = 0; i < sections; i++)
            {
                float x = -wallWidth * 0.5f + (i + 0.5f) * sectionWidth;
                GameObject wallSegment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallSegment.name = $"SeaWall_Segment_{i}";
                wallSegment.transform.SetParent(transform, false);
                wallSegment.transform.localPosition = new Vector3(x, 0f, 0f);
                wallSegment.transform.localScale = new Vector3(sectionWidth * 0.98f, wallHeight, wallThickness);

                var rend = wallSegment.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = concreteMat;

                GameObject parapet = GameObject.CreatePrimitive(PrimitiveType.Cube);
                parapet.name = "ParapetCrest";
                parapet.transform.SetParent(wallSegment.transform, false);
                parapet.transform.localPosition = new Vector3(0f, 0.52f, 0f);
                parapet.transform.localScale = new Vector3(1.02f, 0.06f, 1.15f);
                var pRend = parapet.GetComponent<Renderer>();
                if (pRend != null) pRend.sharedMaterial = concreteMat;
            }

            int craneCount = 4;
            for (int i = 0; i < craneCount; i++)
            {
                float x = Mathf.Lerp(-wallWidth * 0.4f, wallWidth * 0.4f, (float)i / (craneCount - 1));
                GameObject crane = new GameObject($"GantryCrane_{i}");
                crane.transform.SetParent(transform, false);
                crane.transform.localPosition = new Vector3(x, wallHeight * 0.5f + 16f, 0f);

                GameObject mast = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mast.transform.SetParent(crane.transform, false);
                mast.transform.localScale = new Vector3(6f, 32f, 6f);
                mast.GetComponent<Renderer>().sharedMaterial = concreteMat;

                GameObject boom = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boom.transform.SetParent(crane.transform, false);
                boom.transform.localPosition = new Vector3(-8f, 16f, -12f);
                boom.transform.localScale = new Vector3(4f, 4f, 38f);
                boom.GetComponent<Renderer>().sharedMaterial = concreteMat;
            }

            int beaconCount = 6;
            beaconLights = new Light[beaconCount];
            for (int i = 0; i < beaconCount; i++)
            {
                float x = Mathf.Lerp(-wallWidth * 0.46f, wallWidth * 0.46f, (float)i / (beaconCount - 1));
                GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                beacon.name = $"HazardBeacon_{i}";
                beacon.transform.SetParent(transform, false);
                beacon.transform.localPosition = new Vector3(x, wallHeight * 0.5f + 42f, 0f);
                beacon.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
                beacon.GetComponent<Renderer>().sharedMaterial = beaconEmissiveMat;

                var lgt = beacon.AddComponent<Light>();
                lgt.type = LightType.Point;
                lgt.color = beaconColor;
                lgt.intensity = beaconIntensity;
                lgt.range = 75f;
                beaconLights[i] = lgt;
            }

            int floodCount = 5;
            floodlights = new Light[floodCount];
            for (int i = 0; i < floodCount; i++)
            {
                float x = Mathf.Lerp(-wallWidth * 0.38f, wallWidth * 0.38f, (float)i / (floodCount - 1));
                GameObject fl = new GameObject($"IndustrialFloodlight_{i}");
                fl.transform.SetParent(transform, false);
                fl.transform.localPosition = new Vector3(x, wallHeight * 0.35f, -wallThickness * 0.55f);
                fl.transform.localRotation = Quaternion.Euler(22f, 180f, 0f);

                var spot = fl.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.color = floodlightColor;
                spot.intensity = floodlightIntensity;
                spot.range = floodlightRange;
                spot.spotAngle = 65f;
                floodlights[i] = spot;
            }
        }

        void Update()
        {
            if (beaconEmissiveMat != null)
            {
                float pulse = Mathf.Sin(Time.time * Mathf.PI * 2f * beaconBlinkFrequency);
                float blink = pulse > 0.2f ? 1f : 0.05f;
                beaconEmissiveMat.SetColor("_EmissionColor", beaconColor * (blink * 4.5f));

                if (beaconLights != null)
                {
                    for (int i = 0; i < beaconLights.Length; i++)
                    {
                        if (beaconLights[i] != null)
                            beaconLights[i].intensity = blink * beaconIntensity;
                    }
                }
            }
        }
    }
}
