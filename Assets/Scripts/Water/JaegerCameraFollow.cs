using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Smooth third-person cinematic follow camera for the Jaeger mech.
    /// Operates in LateUpdate to eliminate physics jitter and avoids non-uniform scale inheritance.
    /// Fully compatible with both the New Input System and Legacy Input Manager.
    /// </summary>
    public class JaegerCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform of the Jaeger Mech to track.")]
        public Transform target;
        [Tooltip("Vertical look-at target offset on the Jaeger (e.g. upper chest/shoulder level).")]
        public Vector3 targetOffset = new Vector3(0f, 16.5f, 0f);

        [Header("Camera Distance & Angle")]
        [Tooltip("Horizontal distance behind the Jaeger in meters.")]
        public float distance = 24f;
        [Tooltip("Elevation above the shoulder pivot.")]
        public float height = 4.5f;
        [Tooltip("Lateral offset to the right shoulder (third-person shooter framing).")]
        public float rightShoulderOffset = 6.5f;
        [Tooltip("Smooth position lag factor.")]
        public float followDamping = 6f;
        [Tooltip("Smooth rotation lag factor.")]
        public float rotationDamping = 5f;

        [Header("Crosshair")]
        [Tooltip("Display a simple small tactical dot crosshair in the center of the screen.")]
        public bool showCrosshair = true;
        [Tooltip("Diameter of the center crosshair dot in pixels.")]
        public float dotSize = 4f;
        [Tooltip("Color of the center crosshair dot.")]
        public Color dotColor = new Color(0.2f, 0.95f, 1f, 0.95f);

        [Header("Passive Screen Sway")]
        [Tooltip("Subtle ambient breathing and ocean swell camera sway.")]
        public bool enablePassiveSway = false;
        public float passiveSwayPositionAmplitude = 0.16f;
        public float passiveSwayRotationAmplitude = 0.35f;
        public float passiveSwayRollAmplitude = 0.20f;
        public float passiveSwayFrequency = 0.55f;

        [Header("Mouse Orbit (Optional)")]
        [Tooltip("Allow mouse to orbit the camera.")]
        public bool enableMouseLook = true;
        public float mouseSensitivity = 2.5f;
        public float minPitch = -10f;
        public float maxPitch = 60f;

        [Header("Freecam / Inspection Mode")]
        [Tooltip("Toggle with 'E' key during gameplay. When true, camera orbits freely around Jaeger without rotating him.")]
        public bool isFreeCam = false;
        public float freeCamDistance = 24f;
        public float freeCamMinDistance = 4f;
        public float freeCamMaxDistance = 80f;
        public float freeCamMinPitch = -89.5f;
        public float freeCamMaxPitch = 89.5f;
        public float freeCamMinY = 0.5f;
        public float freeCamZoomSpeed = 15f;

        private float currentYaw = 0f;
        private float currentPitch = 15f;
        private Vector3 currentVelocity;
        private Vector3 smoothedBasePos;
        private bool hasSmoothedBasePos;
        private PacificRimCameraShake cameraShake;
        private Rigidbody targetRb;
        private JaegerWaterInteraction targetWater;
        private Transform cachedTarget;

        public float CurrentYaw
        {
            get => currentYaw;
            set => currentYaw = value;
        }

        void Start()
        {
            if (target == null)
            {
                var jaeger = GameObject.Find("Jaeger_Mech");
                if (jaeger != null) target = jaeger.transform;
            }

            // Ensure camera is NOT a child of a scaled object to prevent non-uniform scale distortion
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }

            if (target != null)
            {
                currentYaw = target.eulerAngles.y;
            }

            if (!PacificRimUI.MainMenuUI.IsMenuOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void LateUpdate()
        {
            if (PacificRimUI.MainMenuUI.IsMenuOpen)
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                return;
            }
            else
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            if (target == null)
            {
                var jaeger = GameObject.Find("Jaeger_Mech");
                if (jaeger != null) target = jaeger.transform;
                else return;
            }

            // Freecam toggle via 'E' key
            if (!PacificRimUI.MainMenuUI.IsMenuOpen && WasFreeCamTogglePressedThisFrame())
            {
                ToggleFreeCam();
            }

            // Read mouse input safely across New and Legacy Input Systems
            float mouseDeltaX = 0f;
            float mouseDeltaY = 0f;

            if (enableMouseLook)
            {
                try
                {
                    mouseDeltaX = Input.GetAxis("Mouse X");
                    mouseDeltaY = Input.GetAxis("Mouse Y");
                }
                catch
                {
                    // Legacy input is disabled in Player Settings
                }

#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Mouse.current != null)
                {
                    var mouse = UnityEngine.InputSystem.Mouse.current;
                    Vector2 delta = mouse.delta.ReadValue();
                    mouseDeltaX = delta.x * 0.1f;
                    mouseDeltaY = delta.y * 0.1f;
                }
#endif
            }

            if (enableMouseLook)
            {
                currentYaw += mouseDeltaX * mouseSensitivity;
                currentPitch -= mouseDeltaY * mouseSensitivity;
                float activeMinPitch = isFreeCam ? freeCamMinPitch : minPitch;
                float activeMaxPitch = isFreeCam ? freeCamMaxPitch : maxPitch;
                currentPitch = Mathf.Clamp(currentPitch, activeMinPitch, activeMaxPitch);
            }

            // Scroll-wheel distance zooming in freecam mode
            if (isFreeCam)
            {
                float scroll = 0f;
                try { scroll = Input.GetAxis("Mouse ScrollWheel"); } catch {}
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Mouse.current != null)
                {
                    scroll += UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y * 0.005f;
                }
#endif
                if (Mathf.Abs(scroll) > 0.0001f)
                {
                    freeCamDistance = Mathf.Clamp(freeCamDistance - scroll * freeCamZoomSpeed, freeCamMinDistance, freeCamMaxDistance);
                }
            }

            Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);
            Quaternion targetRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            float effectiveShoulderOffset = isFreeCam ? 0f : rightShoulderOffset;
            float effectiveDistance = isFreeCam ? freeCamDistance : distance;
            Vector3 orbitCenter = target.position + targetOffset + (yawRotation * Vector3.right * effectiveShoulderOffset) + (isFreeCam ? Vector3.zero : Vector3.up * height);
            Vector3 desiredPosition = orbitCenter - (targetRotation * Vector3.forward * effectiveDistance);

            if (!isFreeCam)
            {
                if (WaterSystem.Instance != null)
                {
                    float minCameraY = WaterSystem.Instance.transform.position.y + 2.2f;
                    desiredPosition.y = Mathf.Max(desiredPosition.y, minCameraY);
                }
            }
            else
            {
                // In Freecam mode, allow complete underwater exploration down to seabed floor
                desiredPosition.y = Mathf.Max(desiredPosition.y, freeCamMinY);
            }

            if (!hasSmoothedBasePos)
            {
                smoothedBasePos = transform.position;
                hasSmoothedBasePos = true;
            }

            smoothedBasePos = Vector3.SmoothDamp(smoothedBasePos, desiredPosition, ref currentVelocity, 1.0f / followDamping);

            if (target != cachedTarget)
            {
                cachedTarget = target;
                targetRb = target.GetComponent<Rigidbody>();
                targetWater = target.GetComponent<JaegerWaterInteraction>();
            }

            if (cameraShake == null)
            {
                cameraShake = GetComponent<PacificRimCameraShake>();
            }
            Vector3 shakeLocalOffset = cameraShake != null ? cameraShake.CurrentOffset : Vector3.zero;
            Vector3 shakeRotOffset = cameraShake != null ? cameraShake.CurrentRotationOffset : Vector3.zero;

            if (PacificRimAtmosphere.HeavyImpactCameraTraumaController.Instance != null)
            {
                shakeLocalOffset += PacificRimAtmosphere.HeavyImpactCameraTraumaController.Instance.CurrentLocalPositionOffset;
                shakeRotOffset += PacificRimAtmosphere.HeavyImpactCameraTraumaController.Instance.CurrentLocalRotationOffset;
            }

            Vector3 swayLocalPos = Vector3.zero;
            Vector3 swayEuler = Vector3.zero;
            if (enablePassiveSway)
            {
                float speed = 0f;
                if (targetRb != null)
                {
#if UNITY_6000_0_OR_NEWER
                    Vector3 v = targetRb.linearVelocity;
#else
                    Vector3 v = targetRb.velocity;
#endif
                    speed = new Vector2(v.x, v.z).magnitude;
                }
                bool isWading = targetWater != null && targetWater.IsWading;
                float swayScale = Mathf.Lerp(1.0f, 1.25f, Mathf.Clamp01((isWading ? 0.30f : 0f) + (speed / 18f) * 0.70f));

                float t = Time.time * passiveSwayFrequency * Mathf.Lerp(1.0f, 1.15f, (swayScale - 1.0f) / 0.25f);
                float nX = (Mathf.PerlinNoise(t * 0.65f, 11.3f) - 0.5f) * 2f;
                float nY = (Mathf.PerlinNoise(t * 0.55f + 23.7f, 3.1f) - 0.5f) * 2f;

                float posAmp = passiveSwayPositionAmplitude * swayScale;
                swayLocalPos = new Vector3(
                    (Mathf.Sin(t * 1.15f) * 0.65f + nX * 0.35f) * posAmp,
                    (Mathf.Sin(t * 1.75f + 1.1f) * 0.65f + nY * 0.35f) * posAmp * 0.75f,
                    Mathf.Cos(t * 0.85f) * posAmp * 0.35f);

                float rotAmp = passiveSwayRotationAmplitude * swayScale;
                float rollAmp = passiveSwayRollAmplitude * swayScale;
                swayEuler = new Vector3(
                    (Mathf.Sin(t * 1.40f + 0.6f) * 0.65f + nY * 0.35f) * rotAmp * 0.65f,
                    (Mathf.Cos(t * 0.95f) * 0.65f + nX * 0.35f) * rotAmp,
                    (Mathf.Sin(t * 0.80f + 2.2f) * 0.70f + nX * 0.30f) * rollAmp);
            }

            Vector3 aimPoint = orbitCenter + (targetRotation * Vector3.forward * 120f);
            Vector3 lookDir = aimPoint - smoothedBasePos;
            Quaternion baseRotation = isFreeCam
                ? targetRotation
                : (lookDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(lookDir.normalized, Vector3.up) : targetRotation);

            transform.position = smoothedBasePos + baseRotation * (swayLocalPos + shakeLocalOffset);
            transform.rotation = baseRotation * Quaternion.Euler(swayEuler + shakeRotOffset);
        }

        public void ToggleFreeCam()
        {
            isFreeCam = !isFreeCam;
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHUDCameraToggle();
            if (!isFreeCam && target != null)
            {
                // Lock camera directly back in to where the Jaeger is facing
                currentYaw = target.eulerAngles.y;
                currentPitch = 15f;
                hasSmoothedBasePos = false;
                PacificRimUI.JaegerHUD.Instance?.ShowBanner("CAMERA LOCKED // COMBAT ALIGNMENT RESTORED", 2.0f);
            }
            else if (isFreeCam)
            {
                freeCamDistance = distance;
                PacificRimUI.JaegerHUD.Instance?.ShowBanner("FREECAM INSPECT // UNRESTRICTED ANGLE & UNDERWATER ACTIVE", 2.5f);
            }
        }

        private static bool WasFreeCamTogglePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.E)) return true;
            }
            catch {}
            return false;
        }

        void OnGUI()
        {
            if (PacificRimUI.MainMenuUI.IsMenuOpen || !showCrosshair || Event.current.type != EventType.Repaint) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float half = dotSize * 0.5f;

            Color prev = GUI.color;
            // Dark 1px outer outline
            GUI.color = new Color(0f, 0.05f, 0.1f, 0.85f);
            GUI.DrawTexture(new Rect(cx - half - 1f, cy - half - 1f, dotSize + 2f, dotSize + 2f), Texture2D.whiteTexture);
            // Crisp tactical center dot
            GUI.color = dotColor;
            GUI.DrawTexture(new Rect(cx - half, cy - half, dotSize, dotSize), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
