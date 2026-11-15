using UnityEngine;

namespace PacificRimCutscene
{
    /// <summary>
    /// Controls a heavy-lift CH-47 Chinook in flight:
    /// - Counter-rotating tandem front and rear rotor blades at realistic RPM
    /// - Synchronized flashing aviation strobes (red port, green starboard, white strobe)
    /// - High-intensity downward searchlight cutting through rain and mist
    /// - Cargo hook anchor for heavy-lift tension suspension cables
    /// </summary>
    public class ChinookRotorController : MonoBehaviour
    {
        [Header("Tandem Rotors")]
        public Transform rotorBladeFront;
        public Transform rotorBladeBack;
        public float rotorRpm = 950f;
        public Vector3 frontRotorAxis = Vector3.up;
        public Vector3 backRotorAxis = Vector3.up;

        [Header("Cargo Hook & Cables")]
        public Transform cargoHook;

        [Header("Lighting & Searchlights")]
        public Light searchlight;
        public Light strobeLight;
        public float strobeInterval = 1.0f;
        public float strobeFlashDuration = 0.08f;

        private float strobeTimer;
        private float currentAngle;

        private void Start()
        {
            ResolveReferences();
        }

        public void ResolveReferences()
        {
            if (rotorBladeFront == null)
            {
                var t = transform.Find("RotorBladeFront");
                if (t != null) rotorBladeFront = t;
            }
            if (rotorBladeBack == null)
            {
                var t = transform.Find("RotorBladeBack");
                if (t != null) rotorBladeBack = t;
            }
            if (cargoHook == null)
            {
                var t = transform.Find("CargoHook");
                if (t != null) cargoHook = t;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float degrees = (rotorRpm * 360f / 60f) * dt;
            currentAngle = (currentAngle + degrees) % 360f;

            // Counter-rotating tandem rotors
            if (rotorBladeFront != null)
            {
                rotorBladeFront.Rotate(frontRotorAxis, degrees, Space.Self);
            }
            if (rotorBladeBack != null)
            {
                rotorBladeBack.Rotate(backRotorAxis, -degrees, Space.Self);
            }

            // Aviation strobe flashing
            if (strobeLight != null)
            {
                strobeTimer += dt;
                if (strobeTimer >= strobeInterval)
                {
                    strobeTimer = 0f;
                }
                strobeLight.enabled = strobeTimer < strobeFlashDuration;
            }
        }
    }
}
