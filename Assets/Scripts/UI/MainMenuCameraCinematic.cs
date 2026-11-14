using UnityEngine;

namespace PacificRimUI
{
    /// <summary>
    /// Subtle, atmospheric cinematic camera motion for the Pacific Rim Main Menu.
    /// Slowly drifts and breathes around the focal subject (the Jaeger mech) with stormy momentum.
    /// </summary>
    public class MainMenuCameraCinematic : MonoBehaviour
    {
        [Header("Target & Framing")]
        public Transform lookAtTarget;
        public Vector3 lookAtOffset = new Vector3(0f, 16f, 0f);

        [Header("Motion Dynamics")]
        public float swaySpeed = 0.28f;
        public float swayAmplitudeX = 0.75f;
        public float swayAmplitudeY = 0.40f;
        public float swayAmplitudeZ = 0.60f;

        private Vector3 initialPosition;
        private Quaternion initialRotation;

        private void Start()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        private void LateUpdate()
        {
            float t = Time.unscaledTime * swaySpeed;
            float offsetX = Mathf.Sin(t) * swayAmplitudeX;
            float offsetY = Mathf.Sin(t * 0.73f) * swayAmplitudeY;
            float offsetZ = Mathf.Cos(t * 0.51f) * swayAmplitudeZ;

            transform.position = initialPosition + new Vector3(offsetX, offsetY, offsetZ);

            if (lookAtTarget != null)
            {
                Vector3 targetPos = lookAtTarget.position + lookAtOffset;
                Quaternion targetRot = Quaternion.LookRotation(targetPos - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.unscaledDeltaTime * 1.5f);
            }
        }
    }
}
