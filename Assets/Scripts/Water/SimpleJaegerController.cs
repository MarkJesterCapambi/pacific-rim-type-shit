using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Heavy mech / character controller with colossal momentum, stomping, and combat strikes
    /// tuned specifically for walking through ocean water and fighting Kaijus in Pacific Rim style.
    /// Fully compatible with both the New Input System and Legacy Input Manager.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SimpleJaegerController : MonoBehaviour
    {
        [Header("Health & Combat")]
        public float maxHealth = 1000f;
        public float currentHealth = 1000f;
        public float punchDamage = 50f;
        public float punchRange = 18f;
        public float punchCooldown = 1.0f;

        [Header("Locomotion")]
        [Tooltip("Walk speed in meters per second.")]
        public float walkSpeed = 8.5f;
        [Tooltip("Turn rotation speed in degrees per second.")]
        public float turnSpeed = 55f;
        [Tooltip("Acceleration responsiveness (lower = heavier feel).")]
        public float acceleration = 2.5f;

        [Header("Stomp Ability")]
        [Tooltip("Press Space to perform a heavy foot stomp into the ocean!")]
        public KeyCode stompKey = KeyCode.Space;
        public float stompCooldown = 1.2f;

        private Rigidbody rb;
        private JaegerWaterInteraction waterInteraction;
        private float lastStompTime;
        private float lastPunchTime;
        private Vector3 targetVelocity;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            currentHealth = maxHealth;
            waterInteraction = GetComponent<JaegerWaterInteraction>();
        }

        void Update()
        {
            float horizontal = 0f;
            float vertical = 0f;
            bool stompTriggered = false;
            bool punchTriggered = false;

            // 1. Try reading via Legacy Input Manager
            try
            {
                horizontal = Input.GetAxis("Horizontal");
                vertical = Input.GetAxis("Vertical");
                stompTriggered = Input.GetKeyDown(stompKey);
                punchTriggered = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F);
            }
            catch
            {
                // Legacy input disabled in Player Settings
            }

            // 2. Seamless fallback to New Input System
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (Mathf.Approximately(horizontal, 0f))
                {
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontal += 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) horizontal -= 1f;
                }
                if (Mathf.Approximately(vertical, 0f))
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) vertical += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) vertical -= 1f;
                }
                if (!stompTriggered && kb.spaceKey.wasPressedThisFrame)
                {
                    stompTriggered = true;
                }
                if (!punchTriggered && kb.fKey.wasPressedThisFrame)
                {
                    punchTriggered = true;
                }
            }

            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (!punchTriggered && mouse.leftButton.wasPressedThisFrame)
                {
                    punchTriggered = true;
                }
            }
#endif

            // Turn rotation
            if (Mathf.Abs(horizontal) > 0.05f)
            {
                float turnAngle = horizontal * turnSpeed * Time.deltaTime;
                transform.Rotate(0f, turnAngle, 0f);
            }

            // Forward/backward stride target
            Vector3 forwardMove = transform.forward * vertical;
            float currentSpeed = walkSpeed;

            // Apply water wading slowdown if deep in ocean
            if (waterInteraction != null && waterInteraction.IsWading)
            {
                currentSpeed *= waterInteraction.deepWaterSpeedMultiplier;
            }

            targetVelocity = forwardMove * currentSpeed;

            // Stomp shockwave
            if (stompTriggered && Time.time >= lastStompTime + stompCooldown)
            {
                lastStompTime = Time.time;
                PerformStomp();
            }

            // Punch Kaiju attack
            if (punchTriggered && Time.time >= lastPunchTime + punchCooldown)
            {
                lastPunchTime = Time.time;
                PerformPunch();
            }
        }

        void FixedUpdate()
        {
#if UNITY_6000_0_OR_NEWER
            Vector3 currentVel = rb.linearVelocity;
#else
            Vector3 currentVel = rb.velocity;
#endif
            Vector3 horizontalVel = new Vector3(currentVel.x, 0f, currentVel.z);
            Vector3 newHorizVel = Vector3.Lerp(horizontalVel, targetVelocity, Time.fixedDeltaTime * acceleration);

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector3(newHorizVel.x, currentVel.y, newHorizVel.z);
#else
            rb.velocity = new Vector3(newHorizVel.x, currentVel.y, newHorizVel.z);
#endif
        }

        private void PerformStomp()
        {
            if (waterInteraction != null)
            {
                Vector3 contact = transform.position;
                if (WaterSystem.Instance != null)
                {
                    contact.y = WaterSystem.Instance.GetWaterHeight(contact);
                }
                waterInteraction.TriggerFootstepImpact(contact, waterInteraction.footstepWaveRadius * 1.6f, waterInteraction.footstepWaveStrength * 2.2f);
            }
        }

        private void PerformPunch()
        {
            Vector3 punchPos = transform.position + transform.forward * 8.0f;
            if (WaterSystem.Instance != null)
            {
                punchPos.y = WaterSystem.Instance.GetWaterHeight(punchPos);
                WaterSystem.Instance.AddFootstepWave(punchPos, 14f, 2.0f);
            }

            if (WaterSplashManager.Instance != null)
            {
                WaterSplashManager.Instance.SpawnFootstepSplash(punchPos, 2.0f);
            }
            ProceduralSplash.CreateFoamRing(punchPos, 15f, 1.6f);

            // Check if hitting Kaiju
            Collider[] hits = Physics.OverlapSphere(punchPos, punchRange);
            foreach (var col in hits)
            {
                var kaiju = col.GetComponent<KaijuAI>();
                if (kaiju != null)
                {
                    Vector3 knockback = transform.forward * 20f;
                    kaiju.TakeDamage(punchDamage, knockback);
                    break;
                }
            }

            Debug.Log("<color=#00e5ff><b>[Jaeger]</b></color> Heavy Punch Strike unleashed!");
        }

        public void TakeDamage(float amount, Vector3 knockback)
        {
            currentHealth -= amount;
            Debug.Log($"<color=#ff4444><b>[Jaeger]</b></color> Taken {amount} DMG! Armor: {currentHealth}/{maxHealth}");
            rb.AddForce(knockback, ForceMode.Impulse);
        }
    }
}
