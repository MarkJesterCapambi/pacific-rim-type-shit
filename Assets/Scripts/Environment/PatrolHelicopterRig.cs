using UnityEngine;

namespace PacificRimEnvironment
{
    /// <summary>
    /// Patrol Helicopter Rig orbiting the mid-ground.
    /// Features:
    /// - Orbiting circular/elliptical flight pattern around scene center.
    /// - Sweeping high-intensity spotlight searching the churning ocean.
    /// - Blinking navigation strobe beacons (red port, green starboard, white tail).
    /// </summary>
    [ExecuteAlways]
    public class PatrolHelicopterRig : MonoBehaviour
    {
        [Header("Flight & Orbit Dynamics")]
        public Transform orbitCenter;
        public float orbitRadius = 95f;
        public float flightAltitude = 38f;
        public float orbitSpeed = 0.32f;
        public float altitudeSwayAmount = 4.2f;
        public float altitudeSwaySpeed = 0.65f;
        public float bankingAngle = 18f;

        [Header("Searchlight")]
        public Light searchlight;
        public Color searchlightColor = new Color(0.85f, 0.95f, 1.0f, 1f);
        public float searchlightIntensity = 12.0f;
        public float searchlightRange = 120f;
        public float sweepSpeed = 1.4f;
        public float sweepAngle = 28f;

        [Header("Navigation Strobes")]
        public Light portStrobe;
        public Light starboardStrobe;
        public Light tailStrobe;
        public float strobeBlinkInterval = 1.1f;

        private float orbitAngle = 0f;
        private Transform rotorTransform;

        void Start()
        {
            if (orbitCenter == null)
            {
                var jaeger = GameObject.Find("Jaeger_Mech");
                if (jaeger != null) orbitCenter = jaeger.transform;
            }

            BuildHelicopterPlaceholderIfMissing();
        }

        public void BuildHelicopter() => BuildHelicopterPlaceholderIfMissing();

        public void BuildHelicopterPlaceholderIfMissing()
        {
            if (transform.childCount > 0) return;

            Material darkChassisMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                name = "Heli_DarkChassis_Mat",
                color = new Color(0.10f, 0.14f, 0.18f, 1f)
            };
            darkChassisMat.SetFloat("_Metallic", 0.65f);
            darkChassisMat.SetFloat("_Smoothness", 0.35f);

            GameObject fuselage = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fuselage.name = "Fuselage";
            fuselage.transform.SetParent(transform, false);
            fuselage.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            fuselage.transform.localScale = new Vector3(3.2f, 7.5f, 3.2f);
            fuselage.GetComponent<Renderer>().sharedMaterial = darkChassisMat;

            GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tail.name = "TailBoom";
            tail.transform.SetParent(transform, false);
            tail.transform.localPosition = new Vector3(0f, 0.6f, -7.5f);
            tail.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tail.transform.localScale = new Vector3(1.1f, 5.5f, 1.1f);
            tail.GetComponent<Renderer>().sharedMaterial = darkChassisMat;

            GameObject rotorHub = new GameObject("RotorHub");
            rotorHub.transform.SetParent(transform, false);
            rotorHub.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            rotorTransform = rotorHub.transform;

            GameObject blades = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blades.name = "RotorBlades";
            blades.transform.SetParent(rotorHub.transform, false);
            blades.transform.localScale = new Vector3(19f, 0.12f, 1.2f);
            blades.GetComponent<Renderer>().sharedMaterial = darkChassisMat;

            GameObject slObj = new GameObject("NoseSearchlight");
            slObj.transform.SetParent(transform, false);
            slObj.transform.localPosition = new Vector3(0f, -1.2f, 5.2f);
            searchlight = slObj.AddComponent<Light>();
            searchlight.type = LightType.Spot;
            searchlight.color = searchlightColor;
            searchlight.intensity = searchlightIntensity;
            searchlight.range = searchlightRange;
            searchlight.spotAngle = 42f;

            portStrobe = CreateStrobeLight("PortStrobe_Red", new Vector3(-2.8f, 0f, 0f), Color.red);
            starboardStrobe = CreateStrobeLight("StarboardStrobe_Green", new Vector3(2.8f, 0f, 0f), Color.green);
            tailStrobe = CreateStrobeLight("TailStrobe_White", new Vector3(0f, 2.2f, -12.5f), Color.white);
        }

        private Light CreateStrobeLight(string lightName, Vector3 localPos, Color color)
        {
            GameObject obj = new GameObject(lightName);
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = localPos;
            var l = obj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = 25f;
            l.intensity = 3.5f;
            return l;
        }

        void Update()
        {
            if (!Application.isPlaying) return;

            if (rotorTransform == null)
            {
                rotorTransform = transform.Find("RotorHub");
            }

            orbitAngle += orbitSpeed * Time.deltaTime;
            Vector3 center = orbitCenter != null ? orbitCenter.position : Vector3.zero;

            float x = center.x + Mathf.Cos(orbitAngle) * orbitRadius;
            float z = center.z + Mathf.Sin(orbitAngle) * orbitRadius;
            float y = flightAltitude + Mathf.Sin(Time.time * altitudeSwaySpeed) * altitudeSwayAmount;

            Vector3 newPos = new Vector3(x, y, z);
            Vector3 forwardDir = new Vector3(-Mathf.Sin(orbitAngle), 0f, Mathf.Cos(orbitAngle));

            transform.position = newPos;
            Quaternion targetRot = Quaternion.LookRotation(forwardDir, Vector3.up);
            targetRot *= Quaternion.Euler(0f, 0f, -bankingAngle);
            transform.rotation = targetRot;

            if (rotorTransform != null)
            {
                rotorTransform.Rotate(Vector3.up, 1440f * Time.deltaTime, Space.Self);
            }

            if (searchlight != null)
            {
                float sweepPitch = 38f + Mathf.Sin(Time.time * sweepSpeed * 0.8f) * 12f;
                float sweepYaw = Mathf.Sin(Time.time * sweepSpeed) * sweepAngle;
                searchlight.transform.localRotation = Quaternion.Euler(sweepPitch, sweepYaw, 0f);
            }

            bool strobeOn = (Time.time % strobeBlinkInterval) < 0.12f;
            if (portStrobe != null) portStrobe.enabled = strobeOn;
            if (starboardStrobe != null) starboardStrobe.enabled = strobeOn;
            if (tailStrobe != null) tailStrobe.enabled = strobeOn;
        }
    }
}
