using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Colossal Kaiju AI for Pacific Rim combat.
    /// Stalks and wades through deep ocean water toward the Jaeger,
    /// creating massive tidal shockwaves, water eruptions, and ocean slam attacks.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KaijuAI : MonoBehaviour
    {
        public enum KaijuState { Idle, Stalking, Attacking, Stunned }

        [Header("Target & Detection")]
        [Tooltip("The Jaeger Mech to stalk and fight.")]
        public Transform target;
        public float detectionRange = 400f;
        public float attackRange = 16f;

        [Header("Locomotion")]
        public float wadeSpeed = 7.0f;
        public float turnSpeed = 45f;
        public float strideDistance = 7.0f;

        [Header("Combat & Attacks")]
        public float maxHealth = 500f;
        public float currentHealth = 500f;
        public float attackCooldown = 3.5f;
        public float attackDamage = 25f;

        [Header("Ocean Shockwave Impact")]
        [Tooltip("Radius of Kaiju footstep waves.")]
        public float footstepWaveRadius = 12f;
        public float footstepWaveStrength = 1.8f;
        public float slamWaveRadius = 22f;
        public float slamWaveStrength = 3.0f;

        [Header("State")]
        public KaijuState currentState = KaijuState.Stalking;

        private Rigidbody rb;
        private JaegerWaterInteraction waterInteraction;
        private float lastAttackTime;
        private Vector3 lastStepPos;
        private bool isLeftFoot = true;
        private float stateTimer;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            currentHealth = maxHealth;
            waterInteraction = GetComponent<JaegerWaterInteraction>();
        }

        void Start()
        {
            if (target == null)
            {
                var jaeger = GameObject.Find("Jaeger_Mech");
                if (jaeger != null) target = jaeger.transform;
            }
            lastStepPos = transform.position;
        }

        void Update()
        {
            if (target == null)
            {
                var jaeger = GameObject.Find("Jaeger_Mech");
                if (jaeger != null) target = jaeger.transform;
                else return;
            }

            float distToTarget = Vector3.Distance(transform.position, target.position);

            switch (currentState)
            {
                case KaijuState.Idle:
                    if (distToTarget <= detectionRange)
                    {
                        currentState = KaijuState.Stalking;
                    }
                    break;

                case KaijuState.Stalking:
                    WadeTowardTarget(distToTarget);
                    if (distToTarget <= attackRange && Time.time >= lastAttackTime + attackCooldown)
                    {
                        StartAttack();
                    }
                    break;

                case KaijuState.Attacking:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        currentState = KaijuState.Stalking;
                    }
                    break;

                case KaijuState.Stunned:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        currentState = KaijuState.Stalking;
                    }
                    break;
            }

            TrackFootsteps();
        }

        private void WadeTowardTarget(float distToTarget)
        {
            Vector3 dir = (target.position - transform.position);
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }

            Vector3 moveVel = transform.forward * wadeSpeed;
            if (waterInteraction != null && waterInteraction.IsWading)
            {
                moveVel *= waterInteraction.deepWaterSpeedMultiplier;
            }

#if UNITY_6000_0_OR_NEWER
            Vector3 cur = rb.linearVelocity;
            rb.linearVelocity = new Vector3(moveVel.x, cur.y, moveVel.z);
#else
            Vector3 cur = rb.velocity;
            rb.velocity = new Vector3(moveVel.x, cur.y, moveVel.z);
#endif
        }

        private void StartAttack()
        {
            currentState = KaijuState.Attacking;
            stateTimer = 1.6f;
            lastAttackTime = Time.time;

            // Trigger Ocean Tidal Slam attack
            Invoke(nameof(ExecuteOceanSlam), 0.6f);
        }

        private void ExecuteOceanSlam()
        {
            Vector3 slamPoint = transform.position + transform.forward * 8.0f;
            if (WaterSystem.Instance != null)
            {
                slamPoint.y = WaterSystem.Instance.GetWaterHeight(slamPoint);
                WaterSystem.Instance.AddFootstepWave(slamPoint, slamWaveRadius, slamWaveStrength);
            }

            if (WaterSplashManager.Instance != null)
            {
                WaterSplashManager.Instance.SpawnFootstepSplash(slamPoint, 2.5f);
            }
            ProceduralSplash.CreateFoamRing(slamPoint, slamWaveRadius * 1.1f, 2.2f);

            // Damage player if in slam zone
            if (target != null && Vector3.Distance(slamPoint, target.position) <= slamWaveRadius * 0.8f)
            {
                var playerController = target.GetComponent<SimpleJaegerController>();
                if (playerController != null)
                {
                    playerController.TakeDamage(attackDamage, (target.position - transform.position).normalized * 15f);
                }
            }

            Debug.Log("<color=#ff2a2a><b>[Kaiju AI]</b></color> Ocean Slam Attack unleashed!");
        }

        private void TrackFootsteps()
        {
            Vector3 cur = transform.position;
            float moved = Vector3.Distance(new Vector3(cur.x, 0f, cur.z), new Vector3(lastStepPos.x, 0f, lastStepPos.z));

            if (moved >= strideDistance)
            {
                lastStepPos = cur;
                isLeftFoot = !isLeftFoot;

                Vector3 footPos = cur + (isLeftFoot ? -transform.right : transform.right) * 4.0f;
                if (WaterSystem.Instance != null)
                {
                    footPos.y = WaterSystem.Instance.GetWaterHeight(footPos);
                    WaterSystem.Instance.AddFootstepWave(footPos, footstepWaveRadius, footstepWaveStrength);
                }

                if (WaterSplashManager.Instance != null)
                {
                    WaterSplashManager.Instance.SpawnFootstepSplash(footPos, 1.8f);
                }
                ProceduralSplash.CreateFoamRing(footPos, footstepWaveRadius * 1.1f, 1.8f);
            }
        }

        public void TakeDamage(float amount, Vector3 knockback)
        {
            currentHealth -= amount;
            Debug.Log($"<color=#ff8800><b>[Kaiju]</b></color> Hit for {amount} DMG! HP: {currentHealth}/{maxHealth}");

            rb.AddForce(knockback, ForceMode.Impulse);

            if (currentHealth <= 0f)
            {
                Die();
            }
            else
            {
                currentState = KaijuState.Stunned;
                stateTimer = 0.8f;
            }
        }

        private void Die()
        {
            Debug.Log("<color=#00e5ff><b>[Kaiju DEFEATED!]</b></color> Kaiju collapses into the ocean!");
            if (WaterSystem.Instance != null)
            {
                WaterSystem.Instance.AddFootstepWave(transform.position, 28f, 3.5f);
            }
            if (WaterSplashManager.Instance != null)
            {
                WaterSplashManager.Instance.SpawnFootstepSplash(transform.position, 3.5f);
            }
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.None;
            Destroy(this);
        }
    }
}
