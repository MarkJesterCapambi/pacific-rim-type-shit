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
        [Tooltip("Vertical look-at target offset on the Jaeger (e.g. chest/head level).")]
        public Vector3 targetOffset = new Vector3(0f, 4.0f, 0f);

        [Header("Camera Distance & Angle")]
        [Tooltip("Horizontal distance behind the Jaeger in meters.")]
        public float distance = 26f;
        [Tooltip("Elevation above the Jaeger.")]
        public float height = 12f;
        [Tooltip("Smooth position lag factor.")]
        public float followDamping = 6f;
        [Tooltip("Smooth rotation lag factor.")]
        public float rotationDamping = 5f;

        [Header("Mouse Orbit (Optional)")]
        [Tooltip("Allow mouse to orbit the camera.")]
        public bool enableMouseLook = true;
        public float mouseSensitivity = 2.5f;
        public float minPitch = -10f;
        public float maxPitch = 60f;

        private float currentYaw = 0f;
        private float currentPitch = 15f;
        private Vector3 currentVelocity;

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
        }

        void LateUpdate()
        {
            if (target == null)
            {
                var jaeger = GameObject.Find("Jaeger_Mech");
                if (jaeger != null) target = jaeger.transform;
                else return;
            }

            // Read mouse input safely across New and Legacy Input Systems
            bool isRightClick = false;
            float mouseDeltaX = 0f;
            float mouseDeltaY = 0f;

            if (enableMouseLook)
            {
                try
                {
                    isRightClick = Input.GetMouseButton(1);
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
                    isRightClick = mouse.rightButton.isPressed;
                    Vector2 delta = mouse.delta.ReadValue();
                    mouseDeltaX = delta.x * 0.1f;
                    mouseDeltaY = delta.y * 0.1f;
                }
#endif
            }

            if (enableMouseLook && isRightClick)
            {
                currentYaw += mouseDeltaX * mouseSensitivity;
                currentPitch -= mouseDeltaY * mouseSensitivity;
                currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
            }
            else
            {
                // Smoothly follow behind the target's facing direction
                currentYaw = Mathf.LerpAngle(currentYaw, target.eulerAngles.y, Time.deltaTime * rotationDamping);
            }

            Quaternion targetRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 focusPoint = target.position + targetOffset;
            Vector3 desiredPosition = focusPoint - (targetRotation * Vector3.forward * distance) + Vector3.up * height;

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, 1.0f / followDamping);
            transform.LookAt(focusPoint);
        }
    }
}
