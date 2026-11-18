using UnityEngine;

namespace PacificRimCutscene
{
    /// <summary>
    /// Drives eerie, weightless zero-gravity drifting and slow rotational tumble
    /// for floating jagged monoliths and biomechanical structures in The Anteverse.
    /// </summary>
    public class AnteverseFloatProp : MonoBehaviour
    {
        public Vector3 bobAmplitude = new Vector3(0.45f, 1.25f, 0.45f);
        public float bobFrequency = 0.22f;
        public Vector3 spinDegreesPerSecond = new Vector3(1.2f, 2.4f, -0.9f);
        public float phaseOffset = 0f;

        private Vector3 startPosition;
        private Quaternion startRotation;
        private Vector3 startScale;
        private bool initialized;

        private void Awake()
        {
            CacheInitialTransform();
        }

        private void OnEnable()
        {
            CacheInitialTransform();
        }

        public void CacheInitialTransform()
        {
            if (initialized) return;
            startPosition = transform.position;
            startRotation = transform.rotation;
            startScale = transform.localScale;
            initialized = true;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            float t = Time.time + phaseOffset;
            Vector3 offset = new Vector3(
                Mathf.Sin(t * bobFrequency) * bobAmplitude.x,
                Mathf.Sin(t * bobFrequency * 1.17f + 1.1f) * bobAmplitude.y,
                Mathf.Cos(t * bobFrequency * 0.88f + 0.6f) * bobAmplitude.z);
            transform.position = startPosition + offset;
            transform.rotation = startRotation * Quaternion.Euler(spinDegreesPerSecond * Time.time);
        }

        public void SetVaporizeFade(float fadeOut01)
        {
            if (!initialized) CacheInitialTransform();
            float s = Mathf.Lerp(1f, 0.01f, Mathf.Clamp01(fadeOut01));
            transform.localScale = startScale * s;
            if (fadeOut01 >= 0.98f && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
