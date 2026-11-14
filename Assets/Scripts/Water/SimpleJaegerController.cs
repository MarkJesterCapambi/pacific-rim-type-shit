using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Heavy mech / character controller with colossal momentum, stomping, and combat strikes
    /// tuned specifically for walking through ocean water and fighting Kaijus in Pacific Rim style.
    /// Fully compatible with both the New Input System and Legacy Input Manager.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    [RequireComponent(typeof(Rigidbody))]
    public class SimpleJaegerController : MonoBehaviour
    {
        public static SimpleJaegerController Instance { get; private set; }
        [Header("Health & Combat")]
        public float maxHealth = 1000f;
        public float currentHealth = 1000f;
        public float punchDamage = 50f;
        public float punchRange = 18f;
        public float punchCooldown = 1.16f;
        public float comboResetWindow = 2.4f;
        public float heavyAttackDamage = 110f;
        public float heavyAttackRange = 22f;
        public float heavyAttackCooldown = 1.5f;
        [Tooltip("Playback speed multiplier applied to attack states in the Animator (0.75 = 25% slower).")]
        public float attackAnimSpeed = 0.75f;
        [Tooltip("Movement speed multiplier during light attacks (0.70 = 30% slower).")]
        public float lightAttackSpeedMultiplier = 0.70f;
        [Tooltip("Movement speed multiplier during heavy attacks (0.30 = 70% slower).")]
        public float heavyAttackSpeedMultiplier = 0.30f;

        [Header("Locomotion")]
        [Tooltip("Walk speed in meters per second.")]
        public float walkSpeed = 8.5f;
        [Tooltip("Run/Sprint speed in meters per second.")]
        public float runSpeed = 22.5f;
        [Tooltip("Turn rotation speed in degrees per second.")]
        public float turnSpeed = 55f;
        [Tooltip("Acceleration responsiveness (lower = heavier feel).")]
        public float acceleration = 2.5f;

        [Header("Stamina & Exhaustion")]
        public float maxStamina = 100f;
        public float currentStamina = 100f;
        public float lightAttackStaminaCost = 16f;
        public float heavyAttackStaminaCost = 32f;
        public float blockLightStaminaCost = 20f;
        public float blockHeavyStaminaCost = 36f;
        public float staminaRegenRate = 24f;
        public float staminaRegenDelay = 0.85f;
        [Tooltip("Fraction of max stamina required to recover from exhaustion before attacks are unlocked (0.35 = 35%).")]
        [Range(0.05f, 0.95f)]
        public float exhaustionRecoveryRatio = 0.35f;
        public float exhaustionRecoveryThreshold = 35f;

        [Header("Running Mode")]
        [Tooltip("Toggle run mode on/off when pressing Shift (false = hold Shift to run).")]
        public bool toggleRunMode = true;
        public bool isRunning = false;
        [Tooltip("Blend rate (per second) between Walk and Run strides for heavy mechanical weight.")]
        public float runTransitionSpeed = 1.6f;

        [Header("Combat Stance, Block & Power-Up")]
        [Tooltip("Seconds combat stance auto-persists after the last attack before relaxing back to normal Idle.")]
        public float combatStancePersistDuration = 5.0f;
        public bool inCombatStance = false;
        private float combatStanceEndTime = -10f;
        private float punchComboWindowStartTime = -10f;
        private bool isPunchBuffered = false;
        private float punchBufferExpireTime = -10f;
        [Tooltip("Movement speed multiplier when in combat stance (1.0 = full movement speed, no debuff).")]
        public float combatStanceSpeedMultiplier = 1.0f;
        [Tooltip("Hold F to block incoming attacks.")]
        public bool isBlocking = false;
        [Tooltip("Damage reduction percentage while blocking (0.80 = take 80% less damage).")]
        public float blockDamageReduction = 0.80f;
        [Tooltip("Speed multiplier while blocking.")]
        public float blockSpeedMultiplier = 0.35f;

        [Header("Emote & Power-Up")]
        [Tooltip("Duration in seconds of the power-up buff gained from performing the Emote.")]
        public float powerUpDuration = 15f;
        [Tooltip("Attack animation speed multiplier when powered up (1.30 = 30% faster).")]
        public float powerUpAttackSpeedMultiplier = 1.30f;
        [Tooltip("Cooldown reduction multiplier when powered up (0.75 = 25% faster cooldowns).")]
        public float powerUpCooldownMultiplier = 0.75f;

        [Header("Animation")]
        public Animator animator;

        [Header("Sidestep & Procedural Leg IK")]
        [Tooltip("Enable omnidirectional movement (A/D strafe) with LateUpdate pelvis redirection and 2-bone delta leg IK.")]
        public bool enableStrafeAndLegIK = true;
        [Tooltip("Continuously align Jaeger yaw to the camera horizontal look angle while idle, moving, attacking, or blocking.")]
        public bool alignYawToCameraWhenMoving = true;
        [Tooltip("Smooth time (seconds) for Jaeger yaw to turn toward the camera angle.")]
        public float cameraTurnSmoothTime = 0.07f;
        [Tooltip("Lateral strafe movement speed multiplier relative to current walk speed.")]
        public float strafeSpeedMultiplier = 0.85f;
        [Tooltip("Maximum pelvis yaw angle (degrees) when rotating legs into a sidestep.")]
        public float maxStrafePelvisYaw = 65f;
        [Tooltip("Lateral stance spread offset (meters) applied during strafing to keep thigh armor clear.")]
        public float strafeStanceSpread = 0.75f;
        [Tooltip("Additional lateral foot reach (meters) during the swing phase of each step.")]
        public float strafeStepReach = 1.65f;
        [Tooltip("Additional vertical lift (meters) on the swinging foot during lateral steps.")]
        public float strafeStepExtraLift = 0.35f;
        [HideInInspector] public bool useDebugMoveInputOverride = false;
        [HideInInspector] public Vector2 debugMoveInputOverride = Vector2.zero;

        [Header("Stomp Ability")]
        [Tooltip("Press Space to perform a heavy foot stomp into the ocean!")]
        public KeyCode stompKey = KeyCode.Space;
        public float stompCooldown = 1.2f;

        private const float LightPunchClipLength = 0.967f;
        private const float EmoteClipLength = 3.667f;

        private Rigidbody rb;
        private JaegerWaterInteraction waterInteraction;
        private JaegerCameraFollow cameraFollow;
        private float yawTurnVelocity = 0f;
        private float currentBodyYaw = 0f;
        private bool bodyYawInitialized = false;
        private float lastStompTime = -10f;
        private float lastPunchTime = -10f;
        private float lastHeavyAttackTime = -10f;
        private float lightAttackEndTime = -10f;
        private float heavyAttackEndTime = -10f;
        private float emoteEndTime = -10f;
        private float powerUpEndTime = -10f;
        private float lowerBodyLayerWeight = 0f;
        private float currentRunBlend = 0f;
        private readonly int[] punchCycleOrder = new int[3] { 1, 2, 3 };
        private int punchCycleCursor = 3;
        private int lastPlayedPunch = 0;
        private Vector3 targetVelocity;

        private Transform handL;
        private Transform handR;
        private Transform hips;
        private Transform spine1;
        private Transform chest;
        private Transform thighL;
        private Transform shinL;
        private Transform footL;
        private Transform thighR;
        private Transform shinR;
        private Transform footR;

        private float lastHorizontalInput = 0f;
        private float lastVerticalInput = 0f;
        private float lastMoveMagnitude = 0f;
        private bool lastIsEmoting = false;
        private float currentStrafeYaw = 0f;
        private float currentStrafeWeight = 0f;
        private float currentStrafeDir = 0f;
        private float lastStaminaSpendTime = -10f;
        private KaijuAI cachedKaiju;

        public bool IsPoweredUp => Time.time < powerUpEndTime;
        public bool IsEmoting => Time.time < emoteEndTime;
        public bool IsExhausted { get; private set; }
        public bool IsDead => currentHealth <= 0f;
        public float StaminaRatio => maxStamina > 0f ? Mathf.Clamp01(currentStamina / maxStamina) : 0f;

        private JaegerVisualSockets visualSockets;

        void Awake()
        {
            Instance = this;
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            currentBodyYaw = transform.eulerAngles.y;
            bodyYawInitialized = true;
            currentHealth = maxHealth;
            currentStamina = maxStamina;
            IsExhausted = false;
            exhaustionRecoveryThreshold = maxStamina * exhaustionRecoveryRatio;
            waterInteraction = GetComponent<JaegerWaterInteraction>();
            if (animator == null) animator = GetComponent<Animator>();
            cachedKaiju = FindAnyObjectByType<KaijuAI>();
            visualSockets = GetComponent<JaegerVisualSockets>() ?? GetComponentInChildren<JaegerVisualSockets>();
            ResolveRigTransforms();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void ResolveHandTransforms()
        {
            if (handL == null) handL = FindChildRecursive(transform, "Hand.L");
            if (handR == null) handR = FindChildRecursive(transform, "Hand.R");
        }

        private void ResolveRigTransforms()
        {
            ResolveHandTransforms();
            if (hips == null) hips = FindChildRecursive(transform, "Hips");
            if (spine1 == null) spine1 = FindChildRecursive(transform, "Spine1");
            if (chest == null) chest = FindChildRecursive(transform, "Chest");
            if (thighL == null) thighL = FindChildRecursive(transform, "Thigh.L");
            if (shinL == null) shinL = FindChildRecursive(transform, "Shin.L");
            if (footL == null) footL = FindChildRecursive(transform, "Foot.L");
            if (thighR == null) thighR = FindChildRecursive(transform, "Thigh.R");
            if (shinR == null) shinR = FindChildRecursive(transform, "Shin.R");
            if (footR == null) footR = FindChildRecursive(transform, "Foot.R");
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        void Update()
        {
            if (PacificRimUI.MainMenuUI.IsMenuOpen)
            {
                return;
            }

            if (IsDead)
            {
                // When dead, lock movement and attack inputs, reset all upper body actions
                isBlocking = false;
                inCombatStance = false;
                isRunning = false;
                isPunchBuffered = false;
                targetVelocity = Vector3.zero;
                if (animator != null)
                {
                    animator.SetFloat("Speed", 0f);
                    animator.SetBool("IsRunning", false);
                    animator.SetBool("InCombat", false);
                    animator.SetBool("IsBlocking", false);
                    if (animator.layerCount > 1)
                    {
                        animator.SetLayerWeight(1, 0f);
                    }
                }
                return;
            }

            float horizontal = 0f;
            float vertical = 0f;
            bool stompTriggered = false;
            bool punchTriggered = false;
            bool heavyAttackTriggered = false;
            bool shiftPressedThisFrame = false;
            bool shiftHeld = false;
            bool blockHeld = false;
            bool emoteTriggered = false;

            // 1. Try reading via Legacy Input Manager
            try
            {
                horizontal = Input.GetAxis("Horizontal");
                vertical = Input.GetAxis("Vertical");
                stompTriggered = Input.GetKeyDown(stompKey);
                punchTriggered = Input.GetMouseButtonDown(0);
                heavyAttackTriggered = Input.GetMouseButtonDown(1);
                blockHeld = Input.GetKey(KeyCode.F);
                emoteTriggered = Input.GetKeyDown(KeyCode.Q);
                shiftPressedThisFrame = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
                shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
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
                    if (kb.dKey.isPressed) horizontal += 1f;
                    if (kb.aKey.isPressed) horizontal -= 1f;
                }
                if (Mathf.Approximately(vertical, 0f))
                {
                    if (kb.wKey.isPressed) vertical += 1f;
                    if (kb.sKey.isPressed) vertical -= 1f;
                }
                if (!shiftPressedThisFrame && (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame))
                {
                    shiftPressedThisFrame = true;
                }
                if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
                {
                    shiftHeld = true;
                }
                if (!stompTriggered && kb.spaceKey.wasPressedThisFrame)
                {
                    stompTriggered = true;
                }
                if (kb.fKey.isPressed)
                {
                    blockHeld = true;
                }
                if (!emoteTriggered && kb.qKey.wasPressedThisFrame)
                {
                    emoteTriggered = true;
                }
            }

            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (!punchTriggered && mouse.leftButton.wasPressedThisFrame)
                {
                    punchTriggered = true;
                }
                if (!heavyAttackTriggered && mouse.rightButton.wasPressedThisFrame)
                {
                    heavyAttackTriggered = true;
                }
            }
#endif

            if (useDebugMoveInputOverride)
            {
                horizontal = debugMoveInputOverride.x;
                vertical = debugMoveInputOverride.y;
            }

            // Update Global Power-up shader variable for turbine / armor glow
            float powerUpIntensity = IsPoweredUp ? Mathf.Clamp01((powerUpEndTime - Time.time) / 2.0f) : 0f;
            Shader.SetGlobalFloat("_GlobalPowerUp", powerUpIntensity);

            // Emote trigger via Q (disables movement for duration, grants 15s power-up buff)
            if (emoteTriggered && !IsEmoting && Time.time >= lastHeavyAttackTime + 0.5f && Time.time >= lastPunchTime + 0.5f)
            {
                PerformEmote();
            }

            bool isEmotingNow = IsEmoting;

            // Attack status: light punch combo window or heavy attack slam
            bool isHeavyAttacking = Time.time < heavyAttackEndTime;
            bool isLightAttacking = !isHeavyAttacking && Time.time < lightAttackEndTime;
            bool isAttacking = isHeavyAttacking || isLightAttacking;

            // Block status via F (active when holding F, not emoting or attacking or exhausted)
            isBlocking = blockHeld && !isAttacking && !isEmotingNow && !IsExhausted;
            if (isBlocking)
            {
                combatStanceEndTime = Time.time + combatStancePersistDuration;
            }

            // Auto-exhaust if stamina is depleted
            if (currentStamina <= 0.01f)
            {
                currentStamina = 0f;
                if (!IsExhausted)
                {
                    IsExhausted = true;
                    isPunchBuffered = false;
                    PacificRimUI.JaegerHUD.Instance?.NotifyStaminaExhausted();
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlayHUDStaminaDepleted();
                }
            }

            // Stamina regeneration & exhaustion recovery (must reach at least 35% to unlock attacks)
            float recoveryTarget = maxStamina * exhaustionRecoveryRatio;
            exhaustionRecoveryThreshold = recoveryTarget;
            if (Time.time >= lastStaminaSpendTime + staminaRegenDelay && currentStamina < maxStamina)
            {
                float regenMult = IsPoweredUp ? 1.4f : (isBlocking ? 0.45f : 1.0f);
                currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * regenMult * Time.deltaTime);
            }

            if (IsExhausted)
            {
                if (currentStamina >= recoveryTarget)
                {
                    IsExhausted = false;
                    PacificRimUI.JaegerHUD.Instance?.ShowBanner("STAMINA RECOVERED // COMBAT SYSTEMS ONLINE", 2.0f);
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlayHUDStaminaRecovered35();
                }
                else
                {
                    isPunchBuffered = false;
                }
            }

            // Combat stance auto-activates upon attacking/blocking and remains active for 5s
            inCombatStance = (Time.time < combatStanceEndTime) || isBlocking;

            // Toggle / Hold Shift for running mode (sprinting disabled while emoting or blocking or heavy attacking)
            if (isEmotingNow || isBlocking || isHeavyAttacking)
            {
                isRunning = false;
            }
            else
            {
                if (toggleRunMode)
                {
                    if (shiftPressedThisFrame)
                    {
                        isRunning = !isRunning;
                    }
                }
                else
                {
                    isRunning = shiftHeld;
                }

                // Prevent sprinting while moving backwards / reversing
                if (vertical < -0.05f)
                {
                    isRunning = false;
                }
            }

            // Only consider running if actually moving forward and not emoting/blocking/heavy attacking
            bool effectivelyRunning = isRunning && vertical > 0.1f && !isEmotingNow && !isBlocking && !isHeavyAttacking;

            if (!bodyYawInitialized)
            {
                currentBodyYaw = transform.eulerAngles.y;
                bodyYawInitialized = true;
            }

            // Turn rotation & omnidirectional velocity
            Vector2 moveInput = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
            if (enableStrafeAndLegIK)
            {
                if (cameraFollow == null && Camera.main != null)
                {
                    cameraFollow = Camera.main.GetComponent<JaegerCameraFollow>();
                }
                bool isFreeCam = cameraFollow != null && cameraFollow.isFreeCam;

                if (!isFreeCam && alignYawToCameraWhenMoving && !isEmotingNow && Camera.main != null)
                {
                    float targetCamYaw = cameraFollow != null ? cameraFollow.CurrentYaw : Camera.main.transform.eulerAngles.y;
                    currentBodyYaw = Mathf.SmoothDampAngle(
                        currentBodyYaw,
                        targetCamYaw,
                        ref yawTurnVelocity,
                        Mathf.Max(0.01f, cameraTurnSmoothTime),
                        540f);
                    Quaternion bodyRot = Quaternion.Euler(0f, currentBodyYaw, 0f);
                    transform.rotation = bodyRot;
                    if (rb != null) rb.rotation = bodyRot;
                }
            }
            else if (!isEmotingNow && Mathf.Abs(horizontal) > 0.05f)
            {
                currentBodyYaw += horizontal * turnSpeed * Time.deltaTime;
                Quaternion bodyRot = Quaternion.Euler(0f, currentBodyYaw, 0f);
                transform.rotation = bodyRot;
                if (rb != null) rb.rotation = bodyRot;
            }

            currentRunBlend = Mathf.MoveTowards(currentRunBlend, effectivelyRunning ? 1f : 0f, Time.deltaTime * Mathf.Max(0.2f, runTransitionSpeed));

            // Movement velocity target (zero movement while emoting)
            Vector3 moveVec;
            if (enableStrafeAndLegIK)
            {
                moveVec = isEmotingNow
                    ? Vector3.zero
                    : (transform.forward * moveInput.y + transform.right * (moveInput.x * strafeSpeedMultiplier));
            }
            else
            {
                moveVec = isEmotingNow ? Vector3.zero : (transform.forward * vertical);
            }

            float currentSpeed = Mathf.Lerp(walkSpeed, runSpeed, currentRunBlend);

            // Apply attack or block movement speed slowdown (combat stance has no speed debuff)
            if (isEmotingNow)
            {
                currentSpeed = 0f;
            }
            else
            {
                if (inCombatStance && combatStanceSpeedMultiplier < 1.0f)
                {
                    currentSpeed *= combatStanceSpeedMultiplier;
                }

                if (isBlocking)
                {
                    currentSpeed *= blockSpeedMultiplier;
                }
                else if (isHeavyAttacking)
                {
                    currentSpeed *= heavyAttackSpeedMultiplier;
                }
                else if (isLightAttacking)
                {
                    currentSpeed *= lightAttackSpeedMultiplier;
                }
            }

            // Apply water wading slowdown if deep in ocean
            if (waterInteraction != null && waterInteraction.IsWading)
            {
                currentSpeed *= waterInteraction.deepWaterSpeedMultiplier;
            }

            targetVelocity = moveVec * currentSpeed;

            float moveMagnitude = isEmotingNow
                ? 0f
                : (enableStrafeAndLegIK
                    ? Mathf.Clamp01(moveInput.magnitude)
                    : Mathf.Clamp01(Mathf.Abs(vertical) + Mathf.Abs(horizontal) * 0.5f));

            lastHorizontalInput = horizontal;
            lastVerticalInput = vertical;
            lastMoveMagnitude = moveMagnitude;
            lastIsEmoting = isEmotingNow;

            // Update Animator parameters & LowerBody locomotion override weight
            if (animator != null)
            {
                animator.SetFloat("Speed", moveMagnitude);
                animator.SetBool("IsRunning", effectivelyRunning);
                animator.SetFloat("RunBlend", currentRunBlend);
                animator.SetBool("InCombat", inCombatStance);
                animator.SetBool("IsBlocking", isBlocking);

                float walkDirection = vertical < -0.05f ? -1.0f : 1.0f;
                animator.SetFloat("WalkDirection", walkDirection);

                if (animator.layerCount > 1)
                {
                    bool isMoving = moveMagnitude > 0.1f;
                    // Lower body layer (Hips down) only overrides locomotion while attacking or blocking on the move
                    float targetLowerWeight = (isMoving && (isAttacking || isBlocking)) ? 1.0f : 0.0f;
                    lowerBodyLayerWeight = Mathf.MoveTowards(lowerBodyLayerWeight, targetLowerWeight, Time.deltaTime * 12.0f);
                    animator.SetLayerWeight(1, lowerBodyLayerWeight);
                }
            }

            // Stomp shockwave (disabled while emoting)
            if (!isEmotingNow && stompTriggered && Time.time >= lastStompTime + stompCooldown)
            {
                lastStompTime = Time.time;
                combatStanceEndTime = Time.time + combatStancePersistDuration;
                PerformStomp();
            }

            // Attacks are allowed regardless of previous stance (engages combat stance automatically for 5s)
            float currentPunchCooldown = IsPoweredUp ? (punchCooldown * powerUpCooldownMultiplier) : punchCooldown;
            float currentHeavyCooldown = IsPoweredUp ? (heavyAttackCooldown * powerUpCooldownMultiplier) : heavyAttackCooldown;

            if (!isEmotingNow && !isBlocking)
            {
                if (IsExhausted || currentStamina <= 0.01f)
                {
                    if (punchTriggered || heavyAttackTriggered)
                    {
                        PacificRimUI.JaegerHUD.Instance?.NotifyStaminaExhausted();
                    }
                    isPunchBuffered = false;
                }
                else
                {
                    // Buffered punch follow-up: executes instantly as soon as combo cancel window opens
                    if (isPunchBuffered)
                    {
                        if (Time.time >= punchComboWindowStartTime && Time.time <= punchBufferExpireTime && !isHeavyAttacking)
                        {
                            isPunchBuffered = false;
                            ExecuteNextPunch();
                        }
                        else if (Time.time > punchBufferExpireTime)
                        {
                            isPunchBuffered = false;
                        }
                    }

                    // Heavy Kaiju attack (Right Click)
                    if (heavyAttackTriggered && Time.time >= lastHeavyAttackTime + currentHeavyCooldown && !isHeavyAttacking)
                    {
                        lastHeavyAttackTime = Time.time;
                        isPunchBuffered = false;
                        PerformHeavyAttack(effectivelyRunning);
                    }
                    // Light Punch attack (Left Click) - fluid combo follow-up without dropping hands
                    else if (punchTriggered && !isHeavyAttacking)
                    {
                        if (CanStartNextLightAttack())
                        {
                            isPunchBuffered = false;
                            ExecuteNextPunch();
                        }
                        else if (Time.time < punchComboWindowStartTime && (punchComboWindowStartTime - Time.time) <= 0.32f)
                        {
                            // Buffer input during late strike arc so chaining into next punch feels snappy and responsive
                            isPunchBuffered = true;
                            punchBufferExpireTime = punchComboWindowStartTime + 0.22f;
                        }
                    }
                }
            }
        }

        void FixedUpdate()
        {
            if (IsDead)
            {
                targetVelocity = Vector3.zero;
            }

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

            if (bodyYawInitialized && rb != null)
            {
                Quaternion bodyRot = Quaternion.Euler(0f, currentBodyYaw, 0f);
                rb.rotation = bodyRot;
                rb.MoveRotation(bodyRot);
            }
        }

        void LateUpdate()
        {
            if (PacificRimUI.MainMenuUI.IsMenuOpen || IsDead) return;
            if (!enableStrafeAndLegIK && Mathf.Abs(currentStrafeYaw) <= 0.01f && currentStrafeWeight <= 0.001f) return;

            ResolveRigTransforms();

            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;

            float targetStrafeYaw = 0f;
            float targetStrafeWeight = 0f;
            float targetStrafeDir = 0f;

            if (lastMoveMagnitude > 0.08f && !lastIsEmoting && enableStrafeAndLegIK)
            {
                float dirSign = lastVerticalInput < -0.05f ? -1f : 1f;
                float effectiveFwd = Mathf.Max(0.42f, Mathf.Abs(lastVerticalInput));
                float rawAngle = Mathf.Atan2(lastHorizontalInput * dirSign, effectiveFwd) * Mathf.Rad2Deg;
                targetStrafeYaw = Mathf.Clamp(rawAngle, -maxStrafePelvisYaw, maxStrafePelvisYaw);
                targetStrafeWeight = Mathf.Clamp01(Mathf.Abs(lastHorizontalInput) * lastMoveMagnitude);
                targetStrafeDir = Mathf.Clamp(lastHorizontalInput, -1f, 1f);
            }

            currentStrafeYaw = Mathf.MoveTowards(currentStrafeYaw, targetStrafeYaw, 220f * dt);
            currentStrafeWeight = Mathf.MoveTowards(currentStrafeWeight, targetStrafeWeight, 5f * dt);
            currentStrafeDir = Mathf.MoveTowards(currentStrafeDir, targetStrafeDir, 6f * dt);

            if (Mathf.Abs(currentStrafeYaw) > 0.01f && hips != null)
            {
                hips.rotation = Quaternion.AngleAxis(currentStrafeYaw, transform.up) * hips.rotation;
                if (spine1 != null)
                {
                    spine1.rotation = Quaternion.AngleAxis(-currentStrafeYaw * 0.55f, transform.up) * spine1.rotation;
                }
                if (chest != null)
                {
                    chest.rotation = Quaternion.AngleAxis(-currentStrafeYaw * 0.45f, transform.up) * chest.rotation;
                }
            }

            if (currentStrafeWeight > 0.001f)
            {
                ApplyStrafeLegIK(thighL, shinL, footL, -1f);
                ApplyStrafeLegIK(thighR, shinR, footR, 1f);
            }
        }

        private void ApplyStrafeLegIK(Transform thigh, Transform shin, Transform foot, float sideSign)
        {
            if (thigh == null || shin == null || foot == null) return;

            float localFootY = transform.InverseTransformPoint(foot.position).y;
            float swingLift = Mathf.Clamp01((localFootY - 1.48f) / 0.85f);

            Vector3 lateralSpread = transform.right * (sideSign * strafeStanceSpread * currentStrafeWeight);
            Vector3 swingLateralReach = transform.right * (currentStrafeDir * swingLift * strafeStepReach * currentStrafeWeight);
            Vector3 swingVertLift = Vector3.up * (swingLift * strafeStepExtraLift * currentStrafeWeight);
            Vector3 targetPos = foot.position + lateralSpread + swingLateralReach + swingVertLift;

            Vector3 redirectedFwd = Quaternion.AngleAxis(currentStrafeYaw, transform.up) * transform.forward;
            Vector3 poleDir = (redirectedFwd + transform.right * (sideSign * 0.18f)).normalized;

            SolveTwoBoneIKWorld(thigh, shin, foot, targetPos, poleDir);
        }

        /// <summary>
        /// 2-bone analytical world-space delta IK solver using the Law of Cosines on top of the current animated pose.
        /// Preserves Blender bone local roll orientations while accurately placing the foot at targetPos.
        /// </summary>
        private static void SolveTwoBoneIKWorld(Transform root, Transform mid, Transform tip, Vector3 targetPos, Vector3 poleForward)
        {
            Vector3 a = root.position;
            Vector3 b = mid.position;
            Vector3 c = tip.position;
            Quaternion origTipRot = tip.rotation;

            float l1 = Vector3.Distance(a, b);
            float l2 = Vector3.Distance(b, c);
            if (l1 < 0.001f || l2 < 0.001f) return;

            Vector3 toTarget = targetPos - a;
            float dist = toTarget.magnitude;
            float minReach = Mathf.Abs(l1 - l2) + 0.05f;
            float maxReach = (l1 + l2) * 0.995f;
            float clampedDist = Mathf.Clamp(dist, minReach, maxReach);
            Vector3 dir = dist > 0.0001f ? (toTarget / dist) : Vector3.down;
            Vector3 clampedTarget = a + dir * clampedDist;

            // Blend animated knee bend direction with rotated pelvis forward pole hint
            Vector3 animMidProj = a + Vector3.Project(b - a, dir);
            Vector3 animKneeDir = b - animMidProj;
            Vector3 poleProj = Vector3.ProjectOnPlane(poleForward, dir);
            Vector3 poleUnit = poleProj.sqrMagnitude > 0.0001f ? poleProj.normalized : dir;
            Vector3 bendDir = Vector3.ProjectOnPlane(animKneeDir * 0.65f + poleUnit * (l1 * 0.35f), dir);
            if (bendDir.sqrMagnitude < 0.0001f)
            {
                bendDir = poleProj;
            }
            if (bendDir.sqrMagnitude < 0.0001f) return;
            bendDir.Normalize();

            float cosAngle0 = Mathf.Clamp((l1 * l1 + clampedDist * clampedDist - l2 * l2) / (2f * l1 * clampedDist), -1f, 1f);
            float x = l1 * cosAngle0;
            float y = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
            Vector3 solvedKnee = a + dir * x + bendDir * y;

            Vector3 curRootToMid = b - a;
            Vector3 newRootToMid = solvedKnee - a;
            if (curRootToMid.sqrMagnitude > 0.0001f && newRootToMid.sqrMagnitude > 0.0001f)
            {
                root.rotation = Quaternion.FromToRotation(curRootToMid, newRootToMid) * root.rotation;
            }

            Vector3 bAfter = mid.position;
            Vector3 cAfter = tip.position;
            Vector3 curMidToTip = cAfter - bAfter;
            Vector3 newMidToTip = clampedTarget - bAfter;
            if (curMidToTip.sqrMagnitude > 0.0001f && newMidToTip.sqrMagnitude > 0.0001f)
            {
                mid.rotation = Quaternion.FromToRotation(curMidToTip, newMidToTip) * mid.rotation;
            }

            tip.rotation = origTipRot;
        }

        private void PerformStomp()
        {
            Vector3 contact = transform.position;
            if (WaterSystem.Instance != null)
            {
                contact.y = WaterSystem.Instance.GetWaterHeight(contact);
            }

            if (waterInteraction != null)
            {
                waterInteraction.TriggerFootstepImpact(contact, waterInteraction.footstepWaveRadius * 1.8f, waterInteraction.footstepWaveStrength * 2.5f, false);
            }

            if (WaterSplashManager.Instance != null)
            {
                WaterSplashManager.Instance.SpawnHeavyMoveWaterKickup(contact, transform.forward, 2.5f, true);
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                var shaker = cam.GetComponent<PacificRimCameraShake>();
                if (shaker != null) shaker.TriggerStompFeedback(1.35f);
            }
        }

        private void ResetAttackTriggers()
        {
            if (animator == null) return;
            animator.ResetTrigger("Punch1");
            animator.ResetTrigger("Punch2");
            animator.ResetTrigger("Punch3");
            animator.ResetTrigger("HeavyAttack");
            animator.ResetTrigger("HeavyAttackRun");
            animator.ResetTrigger("Emote");
            animator.ResetTrigger("BlockHit");
        }

        private int GetNextPunchInCycle(bool resetCycle)
        {
            if (resetCycle || punchCycleCursor >= 3)
            {
                punchCycleOrder[0] = 1;
                punchCycleOrder[1] = 2;
                punchCycleOrder[2] = 3;

                for (int i = 2; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    int tmp = punchCycleOrder[i];
                    punchCycleOrder[i] = punchCycleOrder[j];
                    punchCycleOrder[j] = tmp;
                }

                // Guarantee the first punch of the new 3-cycle never repeats the last punch played
                if (punchCycleOrder[0] == lastPlayedPunch)
                {
                    int swapIdx = Random.Range(1, 3);
                    int tmp = punchCycleOrder[0];
                    punchCycleOrder[0] = punchCycleOrder[swapIdx];
                    punchCycleOrder[swapIdx] = tmp;
                }

                punchCycleCursor = 0;
            }

            int chosen = punchCycleOrder[punchCycleCursor++];
            lastPlayedPunch = chosen;
            return chosen;
        }

        private void ExecuteNextPunch()
        {
            if (IsExhausted || currentStamina <= 0.01f) return;
            int nextPunch = GetNextPunchInCycle(Time.time - lastPunchTime > comboResetWindow);
            lastPunchTime = Time.time;
            combatStanceEndTime = Time.time + combatStancePersistDuration;
            PerformPunch(nextPunch, punchCycleCursor);
        }

        private bool CanStartNextLightAttack()
        {
            if (IsExhausted || currentStamina <= 0.01f)
            {
                return false;
            }

            if (Time.time < heavyAttackEndTime)
            {
                return false;
            }

            // If no punch is active, allow immediately
            if (Time.time >= lightAttackEndTime)
            {
                return true;
            }

            // Punch combo window: allowed immediately once punch has extended & fist lands
            if (Time.time >= punchComboWindowStartTime)
            {
                return true;
            }

            return false;
        }

        private static bool IsLightPunchState(AnimatorStateInfo stateInfo)
        {
            return stateInfo.IsName("Punch1") || stateInfo.IsName("Punch2") || stateInfo.IsName("Punch3");
        }

        public void ConsumeStamina(float amount)
        {
            if (amount <= 0f) return;
            currentStamina = Mathf.Max(0f, currentStamina - amount);
            lastStaminaSpendTime = Time.time;
            if (currentStamina <= 0.01f)
            {
                currentStamina = 0f;
                IsExhausted = true;
                isBlocking = false;
                PacificRimUI.JaegerHUD.Instance?.NotifyStaminaExhausted();
            }
        }

        private void NotifyKaijuAttackStarted(int attackVariant, bool isHeavy)
        {
            if (cachedKaiju == null) cachedKaiju = FindAnyObjectByType<KaijuAI>();
            if (cachedKaiju != null)
            {
                cachedKaiju.NotifyPlayerAttackStarted(attackVariant, isHeavy);
            }
        }

        private void PerformPunch(int punchNumber, int stepInCycle)
        {
            float animSpeed = Mathf.Max(0.1f, IsPoweredUp ? (attackAnimSpeed * powerUpAttackSpeedMultiplier) : attackAnimSpeed);
            float effectiveClipDuration = LightPunchClipLength / animSpeed;
            lightAttackEndTime = Time.time + effectiveClipDuration;

            // Fist lands at ~0.46s-0.51s; combo follow-up window opens right as the strike lands
            float landTime = punchNumber == 2 ? 0.46f : (punchNumber == 3 ? 0.48f : 0.51f);
            punchComboWindowStartTime = Time.time + (landTime / animSpeed);

            ConsumeStamina(IsPoweredUp ? lightAttackStaminaCost * 0.70f : lightAttackStaminaCost);
            NotifyKaijuAttackStarted(punchNumber, isHeavy: false);

            if (animator != null)
            {
                ResetAttackTriggers();
                string triggerName = punchNumber == 2 ? "Punch2" : (punchNumber == 3 ? "Punch3" : "Punch1");
                animator.SetTrigger(triggerName);
            }

            // Calibrated time until fist actually reaches full extension and lands on target
            float currentPunchDamage = IsPoweredUp ? (punchDamage * 1.25f) : punchDamage;
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayAttackWhoosh(transform.position + transform.forward * 4f + Vector3.up * 14f, isHeavy: false);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHydraulicServo(transform.position + Vector3.up * 12f, 0.5f);
            StartCoroutine(ExecuteDelayedStrike(punchNumber, currentPunchDamage, punchRange, landTime / animSpeed, isHeavy: false));

            Debug.Log($"<color=#00e5ff><b>[Jaeger]</b></color> Punch {punchNumber} initiated! (cycle [{punchCycleOrder[0]}-{punchCycleOrder[1]}-{punchCycleOrder[2]}], step {stepInCycle}/3, fist impact in {landTime / animSpeed:F2}s, combo ready in {landTime / animSpeed:F2}s)");
        }

        private void PerformHeavyAttack(bool runningAttack)
        {
            if (IsExhausted || currentStamina <= 0.01f) return;
            punchCycleCursor = 3;
            float animSpeed = Mathf.Max(0.1f, IsPoweredUp ? (attackAnimSpeed * powerUpAttackSpeedMultiplier) : attackAnimSpeed);
            heavyAttackEndTime = Time.time + (2.30f / animSpeed) * 0.88f;
            combatStanceEndTime = Time.time + combatStancePersistDuration;
            punchComboWindowStartTime = heavyAttackEndTime;

            int variant = runningAttack ? 5 : 4;
            ConsumeStamina(IsPoweredUp ? heavyAttackStaminaCost * 0.70f : heavyAttackStaminaCost);
            NotifyKaijuAttackStarted(variant, isHeavy: true);

            if (animator != null)
            {
                ResetAttackTriggers();
                animator.SetTrigger(runningAttack ? "HeavyAttackRun" : "HeavyAttack");
            }

            // Calibrated time until heavy slam actually lands
            float landTime = runningAttack ? 1.55f : 1.38f;
            if (runningAttack)
            {
                if (visualSockets == null) visualSockets = GetComponent<JaegerVisualSockets>() ?? GetComponentInChildren<JaegerVisualSockets>();
                visualSockets?.TriggerRocketBlast(landTime / animSpeed);
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayRocketIgnition(transform.position + Vector3.up * 14f);
                StartCoroutine(RocketBurnAudioRoutine(landTime / animSpeed));
            }
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayAttackWhoosh(transform.position + transform.forward * 5f + Vector3.up * 15f, isHeavy: true);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHydraulicServo(transform.position + Vector3.up * 12f, 0.75f);
            float currentHeavyDamage = IsPoweredUp ? (heavyAttackDamage * 1.25f) : heavyAttackDamage;
            StartCoroutine(ExecuteDelayedStrike(variant, currentHeavyDamage, heavyAttackRange, landTime / animSpeed, isHeavy: true));

            Debug.Log($"<color=#ffaa00><b>[Jaeger]</b></color> {(runningAttack ? "Running Heavy Attack" : "Heavy Attack")} initiated! (fist impact in {landTime / animSpeed:F2}s)");
        }

        private System.Collections.IEnumerator ExecuteDelayedStrike(int attackVariant, float damage, float range, float delay, bool isHeavy)
        {
            yield return new WaitForSeconds(delay);

            if (this == null || !gameObject.activeInHierarchy) yield break;

            ResolveHandTransforms();
            Transform activeHand = (attackVariant == 1 || attackVariant == 4) ? handR : handL;
            Vector3 strikePos = activeHand != null ? activeHand.position : (transform.position + transform.forward * (isHeavy ? 7.5f : 5.5f) + Vector3.up * 15f);

            bool didHit = ApplyStrikeImpact(strikePos, range, damage, isHeavy ? 35f : 20f, isHeavy ? 20f : 14f, isHeavy ? 3.0f : 2.0f, attackVariant);
            TriggerAttackCameraFeedback(attackVariant, isHeavy, didHit);

            if (WaterSplashManager.Instance != null)
            {
                WaterSplashManager.Instance.SpawnHeavyMoveWaterKickup(strikePos, transform.forward, isHeavy ? 2.65f : 1.35f, isHeavy);
            }

            if (didHit)
            {
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayJaegerPunchHit(strikePos, isHeavy);
                Debug.Log($"<color=#00ffaa><b>[Combat Strike Landed!]</b></color> Attack {attackVariant} connected on target for {damage} DMG!");
            }
            else
            {
                Debug.Log($"<color=#888888><b>[Combat Strike Swung]</b></color> Attack {attackVariant} swung through air (miss).");
            }
        }

        private System.Collections.IEnumerator RocketBurnAudioRoutine(float duration)
        {
            PacificRimAudio.PacificRimAudioManager.Instance?.SetRocketBurnLoop(true, transform.position + Vector3.up * 14f);
            yield return new WaitForSeconds(duration);
            PacificRimAudio.PacificRimAudioManager.Instance?.SetRocketBurnLoop(false, transform.position + Vector3.up * 14f);
        }

        private void PerformEmote()
        {
            emoteEndTime = Time.time + EmoteClipLength;
            powerUpEndTime = Time.time + EmoteClipLength + powerUpDuration;

            if (animator != null)
            {
                ResetAttackTriggers();
                animator.SetTrigger("Emote");
            }

            if (WaterSplashManager.Instance != null)
            {
                WaterSplashManager.Instance.SpawnHeavyMoveWaterKickup(transform.position, Vector3.up, 2.2f, true);
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                var shaker = cam.GetComponent<PacificRimCameraShake>();
                if (shaker != null) shaker.TriggerTurbineOverchargeFeedback();
            }

            Debug.Log($"<color=#00e5ff><b>[Jaeger Emote]</b></color> Victory/Battle roar unleashed! Movement locked for {EmoteClipLength:F1}s. Power-Up active for {powerUpDuration}s (+30% attack speed, +25% cooldown speed, glowing reactor)!");
        }

        private void TriggerAttackCameraFeedback(int attackVariant, bool isHeavy, bool didHit)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            var shaker = cam.GetComponent<PacificRimCameraShake>();
            if (shaker == null)
            {
                shaker = cam.gameObject.AddComponent<PacificRimCameraShake>();
            }
            shaker.TriggerAttackFeedback(attackVariant, isHeavy, didHit);
        }

        private bool ApplyStrikeImpact(Vector3 strikePos, float range, float damage, float knockbackDist, float waveRadius, float waveStrength, int attackVariant = 0)
        {
            Vector3 rawStrikePos = strikePos;
            if (WaterSystem.Instance != null)
            {
                Vector3 waterPos = strikePos;
                waterPos.y = WaterSystem.Instance.GetWaterHeight(strikePos);
                float clampedRipple = Mathf.Min(waveStrength * 0.1f, 0.22f);
                WaterSystem.Instance.AddFootstepWave(waterPos, waveRadius, clampedRipple);
            }

            bool didHit = false;
            bool damagedKaiju = false;
            Collider[] hits = Physics.OverlapSphere(rawStrikePos, range);
            foreach (var col in hits)
            {
                if (col == null || col.transform.IsChildOf(transform)) continue;
                if (col.gameObject.name == "Test_Platform" || col.gameObject.name == "Ground" || col.gameObject.tag == "Ground") continue;

                var kaiju = col.GetComponentInParent<KaijuAI>();
                if (kaiju != null)
                {
                    if (!damagedKaiju)
                    {
                        Vector3 knockback = transform.forward * knockbackDist;
                        kaiju.TakeDamage(damage, knockback, attackVariant, rawStrikePos);
                        damagedKaiju = true;
                    }
                    didHit = true;
                }
                else if (!col.isTrigger)
                {
                    didHit = true;
                }
            }

            return didHit;
        }

        public void TakeDamage(float amount, Vector3 knockback, bool isHeavy = false)
        {
            if (IsDead) return;

            if (!isHeavy && amount >= 65f) isHeavy = true;

            Vector3 impactPos = transform.position + transform.forward * 3.2f + Vector3.up * 15.5f;
            float actualAmount = amount;
            bool blockedHit = isBlocking && !IsExhausted;

            Camera cam = Camera.main;
            PacificRimCameraShake shaker = cam != null ? cam.GetComponent<PacificRimCameraShake>() : null;

            if (blockedHit)
            {
                ConsumeStamina(isHeavy ? blockHeavyStaminaCost : blockLightStaminaCost);
                actualAmount = amount * (1f - Mathf.Clamp01(blockDamageReduction));
                if (animator != null)
                {
                    animator.SetTrigger("BlockHit");
                }
                if (shaker != null)
                {
                    shaker.TriggerBlockImpactFeedback(isHeavy ? 2.25f : 1.65f);
                }
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayArmorCreak(transform.position + Vector3.up * 14f, 0.75f);
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayHydraulicServo(transform.position + Vector3.up * 12f, 0.6f);
                WaterSplashManager.Instance?.SpawnCombatSparks(impactPos, transform.forward, isBlocked: true, isHeavy: isHeavy, isKaijuTarget: false);
                PacificRimUI.JaegerHUD.Instance?.TriggerScreenImpact(wasBlocked: true, isHeavy: isHeavy);
                Debug.Log($"<color=#00e5ff><b>[Jaeger BLOCK]</b></color> Guard absorbed {amount - actualAmount:F1} DMG! Took {actualAmount:F1} DMG (Stamina: {currentStamina:F0}/{maxStamina:F0}).");
                knockback *= 0.25f;
            }
            else
            {
                if (shaker != null)
                {
                    shaker.TriggerPlayerHitFeedback(isHeavy);
                }
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayArmorCreak(transform.position + Vector3.up * 14f, 0.9f);
                WaterSplashManager.Instance?.SpawnCombatSparks(impactPos, transform.forward, isBlocked: false, isHeavy: isHeavy, isKaijuTarget: false);
                PacificRimUI.JaegerHUD.Instance?.TriggerScreenImpact(wasBlocked: false, isHeavy: isHeavy);
            }

            currentHealth = Mathf.Max(0f, currentHealth - actualAmount);
            Debug.Log($"<color=#ff4444><b>[Jaeger]</b></color> Taken {actualAmount:F1} DMG! Armor: {currentHealth:F0}/{maxHealth:F0}");
            rb.AddForce(knockback, ForceMode.Impulse);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            isBlocking = false;
            inCombatStance = false;
            isRunning = false;
            isPunchBuffered = false;
            targetVelocity = Vector3.zero;

            ResetAttackTriggers();

            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
                animator.SetBool("IsRunning", false);
                animator.SetBool("InCombat", false);
                animator.SetBool("IsBlocking", false);
                if (animator.layerCount > 1)
                {
                    animator.SetLayerWeight(1, 0f);
                }
                // Play Gipsy falling down animation
                animator.CrossFade("Falldown", 0.12f, 0);
            }

            if (cachedKaiju == null) cachedKaiju = FindAnyObjectByType<KaijuAI>();
            if (cachedKaiju != null)
            {
                cachedKaiju.TriggerVictoryRoar();
            }

            Debug.Log("<color=#ff2222><b>[Jaeger DESTROYED]</b></color> Critical Hull Breach! All systems collapsed.");
            StartCoroutine(DefeatSequence());
        }

        private System.Collections.IEnumerator DefeatSequence()
        {
            // Trigger heavy defeat screen impact and camera shake
            Camera cam = Camera.main;
            if (cam != null)
            {
                var shaker = cam.GetComponent<PacificRimCameraShake>();
                if (shaker != null)
                {
                    shaker.TriggerPlayerHitFeedback(isHeavy: true);
                }
            }
            PacificRimUI.JaegerHUD.Instance?.TriggerScreenImpact(wasBlocked: false, isHeavy: true);

            // Wait for Gipsy's Falldown animation and Leatherback's EMP victory roar to play out
            yield return new WaitForSeconds(3.0f);

            PacificRimUI.MainMenuUI.Instance?.SetDefeatState();
        }
    }
}
