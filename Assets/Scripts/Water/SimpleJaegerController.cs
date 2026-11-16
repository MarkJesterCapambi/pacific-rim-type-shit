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
        public float punchDamage = 62.5f;
        public float punchRange = 18f;
        public float punchCooldown = 0.93f;
        public float comboResetWindow = 2.4f;
        public float heavyAttackDamage = 137.5f;
        public float heavyAttackRange = 22f;
        public float heavyAttackCooldown = 1.20f;
        [Tooltip("Playback speed multiplier applied to attack states in the Animator (0.9375 = 1.25x faster).")]
        public float attackAnimSpeed = 0.9375f;
        [Tooltip("Movement speed multiplier during light attacks (0.70 = 30% slower).")]
        public float lightAttackSpeedMultiplier = 0.70f;
        [Tooltip("Movement speed multiplier during heavy attacks (0.30 = 70% slower).")]
        public float heavyAttackSpeedMultiplier = 0.30f;

        [Header("Locomotion")]
        [Tooltip("Walk speed in meters per second (buffed 1.5x from 8.5 m/s).")]
        public float walkSpeed = 12.75f;
        [Tooltip("Run/Sprint speed in meters per second (buffed 1.5x from 22.5 m/s).")]
        public float runSpeed = 33.75f;
        [Tooltip("Turn rotation speed in degrees per second.")]
        public float turnSpeed = 55f;
        [Tooltip("Acceleration responsiveness (lower = heavier feel).")]
        public float acceleration = 2.5f;

        [Header("Stamina & Exhaustion")]
        public float maxStamina = 125f;
        public float currentStamina = 125f;
        public float lightAttackStaminaCost = 16f;
        public float heavyAttackStaminaCost = 32f;
        public float blockLightStaminaCost = 16f;
        public float blockHeavyStaminaCost = 28.8f;
        public float staminaRegenRate = 24f;
        public float staminaRegenDelay = 0.85f;
        [Tooltip("Fraction of max stamina required to recover from exhaustion before attacks are unlocked (0.35 = 35%).")]
        [Range(0.05f, 0.95f)]
        public float exhaustionRecoveryRatio = 0.35f;
        public float exhaustionRecoveryThreshold = 43.75f;

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

        [Header("Parry System")]
        [Tooltip("Precision time window in seconds after raising block where an incoming strike is perfectly parried (tightened to 0.18s for high-skill deflection).")]
        public float parryWindow = 0.18f;
        [Tooltip("Percentage of max stamina granted on a successful parry (0.15 = +15%).")]
        public float parryStaminaRestorePercent = 0.15f;
        public float LastBlockStartTime { get; private set; } = -10f;

        [Header("Emote & Power-Up")]
        [Tooltip("Duration in seconds of the power-up buff gained from performing the Emote.")]
        public float powerUpDuration = 15f;
        [Tooltip("Attack animation speed multiplier when powered up (1.30 = 30% faster).")]
        public float powerUpAttackSpeedMultiplier = 1.30f;
        [Tooltip("Cooldown reduction multiplier when powered up (0.75 = 25% faster cooldowns).")]
        public float powerUpCooldownMultiplier = 0.75f;

        [Header("Damage Reaction & Stagger")]
        [Tooltip("Lockout duration in seconds where no attacks can be launched after taking an unblocked hit.")]
        public float hitAttackLockoutDuration = 0.15f;
        private float hitAttackDisallowedUntil = -10f;
        private Coroutine activeDelayedStrikeCoroutine = null;
        private float sprintAttackEndTime = -10f;

        [Header("Sprint Attack Lunge Momentum")]
        [Tooltip("Deceleration drag for the heavy 2,000-ton Jaeger forward jump lunge.")]
        public float lungeDrag = 14.5f;
        private Vector3 lungeVelocity = Vector3.zero;

        public bool IsHitStaggered => Time.time < hitAttackDisallowedUntil;
        public bool IsSprintAttacking => Time.time < sprintAttackEndTime;

        [Header("Cutscene & Deployment Mode")]
        [Tooltip("True while the pre-fight deployment cutscene is active; locks player input and physics locomotion.")]
        public bool isCutsceneActive = false;

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

        [Header("Guard, Chainswords, Plasma Caster & Thruster Sidestep")]
        public KeyCode blockKey = KeyCode.F;
        public KeyCode chainswordKey = KeyCode.R;
        public KeyCode plasmaCasterKey = KeyCode.T;
        public float chainswordDamageMultiplier = 1.35f;
        public float chainswordExtraRange = 4.0f;
        public float plasmaDamage = 115f;
        public float plasmaCooldown = 2.4f;
        public float plasmaStaminaCost = 24f;
        public float plasmaBoltSpeed = 120f;
        public float doubleTapWindow = 0.26f;
        public float dashSpeed = 42f;
        public float dashDuration = 0.34f;
        public float dashCooldown = 1.15f;
        public float dashStaminaCost = 20f;

        private const float LightPunchClipLength = 0.967f;
        private const float EmoteClipLength = 7.334f; // 3.667s clip slowed by 0.5x = 7.334s

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
        private Transform forearmL;
        private Transform upperArmL;
        private Transform forearmR;
        private Transform upperArmR;
        private Transform hips;
        private Transform spine1;
        private Transform chest;
        private Transform thighL;
        private Transform shinL;
        private Transform footL;
        private Transform thighR;
        private Transform shinR;
        private Transform footR;

        // Chainswords, Plasma Caster & Double-Tap Thruster Dash runtime state
        private bool areChainswordsActive = false;
        private GameObject chainswordBladeL;
        private GameObject chainswordBladeR;
        private Vector3 swordDeployedPosL;
        private Vector3 swordDeployedPosR;
        private Quaternion swordDeployedRotL;
        private Quaternion swordDeployedRotR;
        private Vector3 swordDeployedScaleL;
        private Vector3 swordDeployedScaleR;
        private Coroutine activeChainswordAnimCoroutine;
        private float chainswordDeployAnimStartTime = -10f;
        private float chainswordDeployAnimEndTime = -10f;
        private bool chainswordDeployIsOpening = false;
        private float lastPlasmaTime = -10f;
        private float plasmaAimStartTime = -10f;
        private float plasmaAimEndTime = -10f;
        private float plasmaFireTime = -10f;
        private Vector3 plasmaTargetPos;
        private float lastDashTime = -10f;
        private float dashEndTime = -10f;
        private Vector2 dashLocalDir = Vector2.zero;
        private Vector3 dashWorldDir = Vector3.forward;
        private float dashStartTime = -10f;

        // Smoothed procedural IK blend weights (prevent popping when abilities start/stop/interrupt)
        private float plasmaIKWeight = 0f;
        private float dashIKWeight = 0f;
        private float plasmaRecoilKick = 0f;
        private float lastTapW = -10f;
        private float lastTapS = -10f;
        private float lastTapA = -10f;
        private float lastTapD = -10f;

        public bool AreChainswordsActive => areChainswordsActive;
        public bool IsChainswordDeploying => Time.time < chainswordDeployAnimEndTime;
        public bool IsPlasmaAiming => Time.time < plasmaAimEndTime;
        public bool IsDashing => Time.time < dashEndTime;

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
        public bool IsIronmanCheatActive { get; private set; } = false;

        public void ToggleIronmanCheat()
        {
            IsIronmanCheatActive = !IsIronmanCheatActive;
            Debug.Log($"<color=#00e5ff><b>[CHEAT]</b></color> IRONMAN {(IsIronmanCheatActive ? "ACTIVATED: 3x Strike Damage & 3x Attack Resistance engaged!" : "DEACTIVATED")}");
        }

        public void ActivateIronmanCheat()
        {
            IsIronmanCheatActive = true;
            Debug.Log("<color=#00e5ff><b>[CHEAT]</b></color> IRONMAN ACTIVATED: 3x Strike Damage & 3x Attack Resistance engaged!");
        }

        public void TriggerVictoryPose()
        {
            if (IsDead) return;
            PerformEmote();
        }

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
            areChainswordsActive = false;
            EnsureChainswordVisuals();
            if (chainswordBladeL != null) chainswordBladeL.SetActive(false);
            if (chainswordBladeR != null) chainswordBladeR.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void ResolveHandTransforms()
        {
            if (handL == null) handL = FindChildRecursive(transform, "Hand.L");
            if (handR == null) handR = FindChildRecursive(transform, "Hand.R");
            if (handL != null)
            {
                if (forearmL == null) forearmL = handL.parent;
                if (upperArmL == null && forearmL != null) upperArmL = forearmL.parent;
            }
            if (handR != null)
            {
                if (forearmR == null) forearmR = handR.parent;
                if (upperArmR == null && forearmR != null) upperArmR = forearmR.parent;
            }
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
            if (PacificRimUI.MainMenuUI.IsMenuOpen || isCutsceneActive)
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
            bool chainswordTriggered = false;
            bool plasmaTriggered = false;
            bool tapW = false, tapS = false, tapA = false, tapD = false;

            // 1. Try reading via Legacy Input Manager
            try
            {
                horizontal = Input.GetAxis("Horizontal");
                vertical = Input.GetAxis("Vertical");
                stompTriggered = Input.GetKeyDown(stompKey);
                punchTriggered = Input.GetMouseButtonDown(0);
                heavyAttackTriggered = Input.GetMouseButtonDown(1);
                blockHeld = Input.GetKey(blockKey);
                emoteTriggered = Input.GetKeyDown(KeyCode.Q);
                chainswordTriggered = Input.GetKeyDown(chainswordKey);
                plasmaTriggered = Input.GetKeyDown(plasmaCasterKey);
                tapW = Input.GetKeyDown(KeyCode.W);
                tapS = Input.GetKeyDown(KeyCode.S);
                tapA = Input.GetKeyDown(KeyCode.A);
                tapD = Input.GetKeyDown(KeyCode.D);
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
                if (!blockHeld && kb[GetInputSystemKey(blockKey)].isPressed)
                {
                    blockHeld = true;
                }
                if (!emoteTriggered && kb.qKey.wasPressedThisFrame)
                {
                    emoteTriggered = true;
                }
                if (!chainswordTriggered && kb[GetInputSystemKey(chainswordKey)].wasPressedThisFrame)
                {
                    chainswordTriggered = true;
                }
                if (!plasmaTriggered && kb[GetInputSystemKey(plasmaCasterKey)].wasPressedThisFrame)
                {
                    plasmaTriggered = true;
                }
                if (!tapW && kb.wKey.wasPressedThisFrame) tapW = true;
                if (!tapS && kb.sKey.wasPressedThisFrame) tapS = true;
                if (!tapA && kb.aKey.wasPressedThisFrame) tapA = true;
                if (!tapD && kb.dKey.wasPressedThisFrame) tapD = true;
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

            // Chainswords toggle (R)
            if (chainswordTriggered && !IsEmoting)
            {
                ToggleChainswords();
            }

            // Plasma Caster fire (F)
            if (plasmaTriggered && !IsEmoting && !IsExhausted && !IsPlasmaAiming && Time.time >= lastPlasmaTime + plasmaCooldown)
            {
                TriggerPlasmaCaster();
            }

            // Double-tap WASD Thruster Sidestep / Dash
            CheckDoubleTapDash(tapW, tapS, tapA, tapD);

            bool isEmotingNow = IsEmoting;

            // Attack status: light punch combo window or heavy attack slam
            bool isHeavyAttacking = Time.time < heavyAttackEndTime;
            bool isLightAttacking = !isHeavyAttacking && Time.time < lightAttackEndTime;
            bool isAttacking = isHeavyAttacking || isLightAttacking;

            // Block status via F (active when holding F, not emoting or attacking or exhausted)
            bool wasBlockingBefore = isBlocking;
            isBlocking = blockHeld && !isAttacking && !isEmotingNow && !IsExhausted;
            if (isBlocking && !wasBlockingBefore)
            {
                LastBlockStartTime = Time.time;
            }
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

            // Stomp shockwave (disabled while emoting or hit staggered)
            if (!isEmotingNow && !IsHitStaggered && stompTriggered && Time.time >= lastStompTime + stompCooldown)
            {
                lastStompTime = Time.time;
                combatStanceEndTime = Time.time + combatStancePersistDuration;
                PerformStomp();
            }

            // Attacks are allowed regardless of previous stance (engages combat stance automatically for 5s)
            float currentPunchCooldown = IsPoweredUp ? (punchCooldown * powerUpCooldownMultiplier) : punchCooldown;
            float currentHeavyCooldown = IsPoweredUp ? (heavyAttackCooldown * powerUpCooldownMultiplier) : heavyAttackCooldown;

            if (!isEmotingNow && !isBlocking && !IsHitStaggered)
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
            if (isCutsceneActive) return;

            if (IsDead)
            {
                targetVelocity = Vector3.zero;
                lungeVelocity = Vector3.zero;
            }

            // Sprint-heavy jump attack lunge physics:
            // Forward leaping momentum carries the heavy 2,000-ton Jaeger through the air,
            // decaying naturally under heavy mech inertia and ocean drag.
            // Player directional input CANNOT abruptly stop or cancel this colossal forward leap momentum midair!
            if (IsDashing && !IsDead)
            {
                // Thruster burst: ease-out speed profile applied directly (bypasses heavy-mech accel smoothing)
                float t = Mathf.Clamp01((Time.time - dashStartTime) / Mathf.Max(0.01f, dashDuration));
                float speedCurve = 1f - (t * t);
                // Compensate for ocean wading drag so the burst actually carries the mech
#if UNITY_6000_0_OR_NEWER
                float dragComp = 1f + (rb.linearDamping * Time.fixedDeltaTime);
#else
                float dragComp = 1f + (rb.drag * Time.fixedDeltaTime);
#endif
                Vector3 dashVel = dashWorldDir * (dashSpeed * Mathf.Lerp(0.35f, 1f, speedCurve) * dragComp);
#if UNITY_6000_0_OR_NEWER
                Vector3 vNow = rb.linearVelocity;
                rb.linearVelocity = new Vector3(dashVel.x, vNow.y, dashVel.z);
#else
                Vector3 vNow = rb.velocity;
                rb.velocity = new Vector3(dashVel.x, vNow.y, dashVel.z);
#endif
                if (bodyYawInitialized && rb != null)
                {
                    rb.MoveRotation(Quaternion.Euler(0f, currentBodyYaw, 0f));
                }
                return;
            }

            if (IsSprintAttacking)
            {
                targetVelocity = lungeVelocity;
                lungeVelocity = Vector3.MoveTowards(lungeVelocity, Vector3.zero, lungeDrag * Time.fixedDeltaTime);
            }
            else
            {
                lungeVelocity = Vector3.zero;
            }

#if UNITY_6000_0_OR_NEWER
            Vector3 currentVel = rb.linearVelocity;
#else
            Vector3 currentVel = rb.velocity;
#endif
            Vector3 horizontalVel = new Vector3(currentVel.x, 0f, currentVel.z);
            float currentAccel = IsSprintAttacking ? 22f : acceleration;
            Vector3 newHorizVel = Vector3.Lerp(horizontalVel, targetVelocity, Time.fixedDeltaTime * currentAccel);

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

            // Smoothed dash blend weight (fast attack, softer release so the mech settles out of the burst)
            dashIKWeight = Mathf.MoveTowards(dashIKWeight, IsDashing ? 1f : 0f, (IsDashing ? 9f : 3.5f) * dt);

            if (dashIKWeight > 0.001f)
            {
                ApplyDashBraceIK(dt);
            }
            else if (currentStrafeWeight > 0.001f)
            {
                ApplyStrafeLegIK(thighL, shinL, footL, -1f);
                ApplyStrafeLegIK(thighR, shinR, footR, 1f);
            }

            ApplyCombatArmsIK(dt);
        }

        /// <summary>
        /// Thruster dash pose: capture planted feet, crouch + lean the pelvis into the burst direction
        /// (rockets push from the back), keep the upper torso braced, then re-solve both legs so the
        /// leading leg reaches out and the trailing leg extends behind like it's pushing off the seabed.
        /// </summary>
        private void ApplyDashBraceIK(float dt)
        {
            if (hips == null || thighL == null || shinL == null || footL == null || thighR == null || shinR == null || footR == null) return;

            float w = Mathf.SmoothStep(0f, 1f, dashIKWeight);
            float progress = IsDashing ? Mathf.Clamp01((Time.time - dashStartTime) / Mathf.Max(0.01f, dashDuration)) : 1f;
            float stepArc = Mathf.Sin(progress * Mathf.PI);

            // Burst direction in world, flattened
            Vector3 dir = new Vector3(dashWorldDir.x, 0f, dashWorldDir.z);
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
            dir.Normalize();

            float legLen = Vector3.Distance(thighL.position, shinL.position) + Vector3.Distance(shinL.position, footL.position);

            // 1. Capture animated (planted) foot positions BEFORE moving the pelvis
            Vector3 footPosL = footL.position;
            Vector3 footPosR = footR.position;

            // 2. Pelvis: crouch down and lean into the burst
            Vector3 leanAxis = Vector3.Cross(Vector3.up, dir);
            float leanAngle = 13f * w;
            hips.position += Vector3.down * (legLen * 0.10f * w);
            hips.rotation = Quaternion.AngleAxis(leanAngle, leanAxis) * hips.rotation;

            // 3. Brace upper body: counter-rotate part of the lean so the head/visor stays steady on target
            if (spine1 != null) spine1.rotation = Quaternion.AngleAxis(-leanAngle * 0.35f, leanAxis) * spine1.rotation;
            if (chest != null) chest.rotation = Quaternion.AngleAxis(-leanAngle * 0.25f, leanAxis) * chest.rotation;

            // 4. Leading vs trailing leg (side dashes lead with the dash-side leg; fwd/back lead with right)
            float lateral = Vector3.Dot(dir, transform.right);
            bool leftLeads = lateral < -0.3f;

            float reach = legLen * 0.22f * w;
            float lift = legLen * 0.10f * w * stepArc;

            Vector3 leadOffset = dir * reach + Vector3.up * lift;
            Vector3 trailOffset = -dir * (reach * 0.85f);

            Vector3 targetL = footPosL + (leftLeads ? leadOffset : trailOffset);
            Vector3 targetR = footPosR + (leftLeads ? trailOffset : leadOffset);

            // Knees point along the body forward, slightly splayed outward
            Vector3 poleL = (transform.forward - transform.right * 0.2f).normalized;
            Vector3 poleR = (transform.forward + transform.right * 0.2f).normalized;

            SolveTwoBoneIKWorld(thighL, shinL, footL, targetL, poleL);
            SolveTwoBoneIKWorld(thighR, shinR, footR, targetR, poleR);
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
        private static void SolveTwoBoneIKWorld(Transform root, Transform mid, Transform tip, Vector3 targetPos, Vector3 poleForward, bool keepTipLocalRotation = false)
        {
            Vector3 a = root.position;
            Vector3 b = mid.position;
            Vector3 c = tip.position;
            Quaternion origTipRot = tip.rotation;
            Quaternion origTipLocalRot = tip.localRotation;

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

            if (keepTipLocalRotation) tip.localRotation = origTipLocalRot;
            else tip.rotation = origTipRot;
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
            animator.ResetTrigger("HitLeft");
            animator.ResetTrigger("HitRight");
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
            if (Time.time < hitAttackDisallowedUntil)
            {
                return false;
            }

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
            if (areChainswordsActive) currentPunchDamage *= chainswordDamageMultiplier;
            float currentPunchRange = areChainswordsActive ? (punchRange + chainswordExtraRange) : punchRange;
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayAttackWhoosh(transform.position + transform.forward * 4f + Vector3.up * 14f, isHeavy: false);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHydraulicServo(transform.position + Vector3.up * 12f, 0.5f);
            if (activeDelayedStrikeCoroutine != null) StopCoroutine(activeDelayedStrikeCoroutine);
            activeDelayedStrikeCoroutine = StartCoroutine(ExecuteDelayedStrike(punchNumber, currentPunchDamage, currentPunchRange, landTime / animSpeed, isHeavy: false));

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

            if (runningAttack)
            {
                sprintAttackEndTime = heavyAttackEndTime;
                // Forward jump lunge momentum: decaying forward velocity carrying the 2,000-ton Jaeger
                Vector3 launchDirection = transform.forward;
                float initialLungeSpeed = Mathf.Max(runSpeed * 1.08f, 32f);
                lungeVelocity = launchDirection * initialLungeSpeed;
            }
            else
            {
                sprintAttackEndTime = -10f;
                lungeVelocity = Vector3.zero;
            }

            if (animator != null)
            {
                ResetAttackTriggers();
                animator.SetTrigger(runningAttack ? "HeavyAttackRun" : "HeavyAttack");
            }

            // Calibrated time until heavy slam actually lands
            float landTime = runningAttack ? 1.55f : 1.38f;
            float blastDuration = (runningAttack ? landTime : 1.15f) / animSpeed;

            // Rocket thrusters VFX/SFX are exclusively engaged for the jumping/sprint heavy attack
            if (runningAttack)
            {
                if (visualSockets == null) visualSockets = GetComponent<JaegerVisualSockets>() ?? GetComponentInChildren<JaegerVisualSockets>();
                visualSockets?.TriggerRocketBlast(blastDuration);
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayRocketIgnition(transform.position + Vector3.up * 14f);
                StartCoroutine(RocketBurnAudioRoutine(blastDuration));
            }

            PacificRimAudio.PacificRimAudioManager.Instance?.PlayAttackWhoosh(transform.position + transform.forward * 5f + Vector3.up * 15f, isHeavy: true);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHydraulicServo(transform.position + Vector3.up * 12f, 0.75f);
            float currentHeavyDamage = IsPoweredUp ? (heavyAttackDamage * 1.25f) : heavyAttackDamage;
            if (areChainswordsActive) currentHeavyDamage *= chainswordDamageMultiplier;
            float currentHeavyRange = areChainswordsActive ? (heavyAttackRange + chainswordExtraRange) : heavyAttackRange;
            if (activeDelayedStrikeCoroutine != null) StopCoroutine(activeDelayedStrikeCoroutine);
            activeDelayedStrikeCoroutine = StartCoroutine(ExecuteDelayedStrike(variant, currentHeavyDamage, currentHeavyRange, landTime / animSpeed, isHeavy: true));

            Debug.Log($"<color=#ffaa00><b>[Jaeger]</b></color> {(runningAttack ? "Running Heavy Attack (Uninterruptible)" : "Heavy Attack")} initiated! (fist impact in {landTime / animSpeed:F2}s)");
        }

        private System.Collections.IEnumerator ExecuteDelayedStrike(int attackVariant, float damage, float range, float delay, bool isHeavy)
        {
            yield return new WaitForSeconds(delay);

            if (this == null || !gameObject.activeInHierarchy || (Time.time < hitAttackDisallowedUntil && attackVariant != 5)) yield break;

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

            PacificRimAudio.PacificRimAudioManager.Instance?.PlaySteamVent(transform.position + Vector3.up * 14f);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayGipsyFoghorn(transform.position + Vector3.up * 18f);

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
                        float effectiveDamage = IsIronmanCheatActive ? (damage * 3f) : damage;
                        Vector3 knockback = transform.forward * knockbackDist;
                        kaiju.TakeDamage(effectiveDamage, knockback, attackVariant, rawStrikePos);
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

        public void TakeDamage(float amount, Vector3 knockback, bool isHeavy = false, Vector3 hitPoint = default)
        {
            if (IsDead) return;
            if (IsDashing)
            {
                Debug.Log("<color=#00e5ff><b>[Jaeger THRUSTER EVADE]</b></color> Dodged incoming Kaiju strike!");
                return;
            }

            if (IsIronmanCheatActive)
            {
                amount /= 3f;
                knockback /= 3f;
            }

            if (!isHeavy && amount >= 65f) isHeavy = true;

            Vector3 impactPos = hitPoint.sqrMagnitude > 0.01f ? hitPoint : (transform.position + transform.forward * 3.2f + Vector3.up * 15.5f);
            float actualAmount = amount;
            bool blockedHit = isBlocking && !IsExhausted;
            bool isParry = blockedHit && (Time.time <= LastBlockStartTime + parryWindow);

            Camera cam = Camera.main;
            PacificRimCameraShake shaker = cam != null ? cam.GetComponent<PacificRimCameraShake>() : null;

            if (isParry)
            {
                // Deflect attack completely: zero damage taken, zero knockback
                actualAmount = 0f;
                knockback = Vector3.zero;

                // Award +15% max stamina and break out of exhaustion immediately
                float staminaReward = maxStamina * parryStaminaRestorePercent;
                currentStamina = Mathf.Min(maxStamina, currentStamina + staminaReward);
                if (IsExhausted)
                {
                    IsExhausted = false;
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlayHUDStaminaRecovered35();
                }
                lastStaminaSpendTime = Time.time - staminaRegenDelay;

                // Stagger enemy without damaging enemy
                if (cachedKaiju == null) cachedKaiju = FindAnyObjectByType<KaijuAI>();
                if (cachedKaiju != null && !cachedKaiju.IsDead)
                {
                    Vector3 recoilDir = -transform.forward * 6.5f + Vector3.up * 1.5f;
                    cachedKaiju.TriggerParryStagger(recoilDir);
                }

                // Audio & visual cues (audible clang + golden vignette + radiant sparks)
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayParryClang(impactPos);
                PacificRimUI.JaegerHUD.Instance?.TriggerParryCue(parryStaminaRestorePercent);
                WaterSplashManager.Instance?.SpawnParrySparks(impactPos, transform.forward);

                if (animator != null)
                {
                    animator.SetTrigger("BlockHit");
                }
                if (shaker != null)
                {
                    shaker.TriggerBlockImpactFeedback(isHeavy ? 2.85f : 2.15f);
                }

                Debug.Log($"<color=#ffd700><b>[PERFECT PARRY]</b></color> Deflected {amount:F0} DMG! Awarded +{staminaReward:F0} Stamina ({currentStamina:F0}/{maxStamina:F0}). Kaiju STAGGERED!");
                return;
            }
            else if (blockedHit)
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
            else if (IsSprintAttacking)
            {
                // UNINTERRUPTIBLE SPRINT ATTACK:
                // Absorbs incoming strikes with hyper-armor. No animation stagger, no attack cancel, no attack lockout.
                knockback = Vector3.zero;

                if (shaker != null)
                {
                    shaker.TriggerPlayerHitFeedback(isHeavy);
                }
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayArmorCreak(transform.position + Vector3.up * 14f, 0.85f);
                WaterSplashManager.Instance?.SpawnCombatSparks(impactPos, transform.forward, isBlocked: false, isHeavy: isHeavy, isKaijuTarget: false);
                PacificRimUI.JaegerHUD.Instance?.TriggerScreenImpact(wasBlocked: false, isHeavy: isHeavy);

                Debug.Log($"<color=#ffaa00><b>[Jaeger UNINTERRUPTIBLE]</b></color> Took {actualAmount:F1} DMG during Sprint-Attack! Hyper-Armor active, strike carries through uninterrupted.");
            }
            else
            {
                // Disallow launching any attack for 0.15 seconds
                hitAttackDisallowedUntil = Time.time + hitAttackLockoutDuration;
                isPunchBuffered = false;
                heavyAttackEndTime = Time.time - 1f;
                lightAttackEndTime = Time.time - 1f;

                if (activeDelayedStrikeCoroutine != null)
                {
                    StopCoroutine(activeDelayedStrikeCoroutine);
                    activeDelayedStrikeCoroutine = null;
                }

                ResetAttackTriggers();

                if (shaker != null)
                {
                    shaker.TriggerPlayerHitFeedback(isHeavy);
                }
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayArmorCreak(transform.position + Vector3.up * 14f, 0.9f);
                WaterSplashManager.Instance?.SpawnCombatSparks(impactPos, transform.forward, isBlocked: false, isHeavy: isHeavy, isKaijuTarget: false);
                PacificRimUI.JaegerHUD.Instance?.TriggerScreenImpact(wasBlocked: false, isHeavy: isHeavy);

                // Determine directional stagger:
                // If hit on the left, Gipsy staggers left (HitLeft / Hit2).
                // If hit on the right, Gipsy staggers right (HitRight / Hit1).
                bool isHitLeft;
                if (hitPoint.sqrMagnitude > 0.01f)
                {
                    Vector3 localHit = transform.InverseTransformPoint(hitPoint);
                    isHitLeft = localHit.x < 0f;
                }
                else if (knockback.sqrMagnitude > 0.01f)
                {
                    Vector3 localKb = transform.InverseTransformDirection(knockback);
                    isHitLeft = localKb.x < 0f;
                }
                else if (cachedKaiju != null)
                {
                    Vector3 localKaiju = transform.InverseTransformPoint(cachedKaiju.transform.position);
                    isHitLeft = localKaiju.x < 0f;
                }
                else
                {
                    isHitLeft = Random.value < 0.5f;
                }

                if (animator != null)
                {
                    string targetState = isHitLeft ? "HitLeft" : "HitRight";
                    animator.CrossFade(targetState, 0.06f, 0);
                    animator.SetTrigger(targetState);

                    if (animator.layerCount > 1)
                    {
                        lowerBodyLayerWeight = 0f;
                        animator.SetLayerWeight(1, 0f);
                    }
                }

                Debug.Log($"<color=#ff4444><b>[Jaeger STAGGER]</b></color> Hit {(isHitLeft ? "LEFT" : "RIGHT")}! Playing {(isHitLeft ? "HitLeft" : "HitRight")}. Attacks locked for {hitAttackLockoutDuration:F2}s.");
            }

            currentHealth = Mathf.Max(0f, currentHealth - actualAmount);
            Debug.Log($"<color=#ff4444><b>[Jaeger]</b></color> Taken {actualAmount:F1} DMG! Armor: {currentHealth:F0}/{maxHealth:F0}");
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (rb != null) rb.AddForce(knockback, ForceMode.Impulse);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            sprintAttackEndTime = -10f;
            lungeVelocity = Vector3.zero;
            if (activeDelayedStrikeCoroutine != null)
            {
                StopCoroutine(activeDelayedStrikeCoroutine);
                activeDelayedStrikeCoroutine = null;
            }

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

#if ENABLE_INPUT_SYSTEM
        private static UnityEngine.InputSystem.Key GetInputSystemKey(KeyCode kc)
        {
            switch (kc)
            {
                case KeyCode.C: return UnityEngine.InputSystem.Key.C;
                case KeyCode.R: return UnityEngine.InputSystem.Key.R;
                case KeyCode.F: return UnityEngine.InputSystem.Key.F;
                case KeyCode.T: return UnityEngine.InputSystem.Key.T;
                case KeyCode.E: return UnityEngine.InputSystem.Key.E;
                case KeyCode.G: return UnityEngine.InputSystem.Key.G;
                case KeyCode.V: return UnityEngine.InputSystem.Key.V;
                case KeyCode.Q: return UnityEngine.InputSystem.Key.Q;
                case KeyCode.LeftControl: return UnityEngine.InputSystem.Key.LeftCtrl;
                case KeyCode.LeftAlt: return UnityEngine.InputSystem.Key.LeftAlt;
                default: return UnityEngine.InputSystem.Key.C;
            }
        }
#endif

        private void CheckDoubleTapDash(bool tapW, bool tapS, bool tapA, bool tapD)
        {
            if (IsEmoting || IsExhausted || Time.time < lastDashTime + dashCooldown) return;

            float now = Time.time;
            Vector2 dir = Vector2.zero;

            if (tapW)
            {
                if (now - lastTapW <= doubleTapWindow) dir = new Vector2(0f, 1f);
                lastTapW = now;
            }
            if (tapS)
            {
                if (now - lastTapS <= doubleTapWindow) dir = new Vector2(0f, -1f);
                lastTapS = now;
            }
            if (tapA)
            {
                if (now - lastTapA <= doubleTapWindow) dir = new Vector2(-1f, 0f);
                lastTapA = now;
            }
            if (tapD)
            {
                if (now - lastTapD <= doubleTapWindow) dir = new Vector2(1f, 0f);
                lastTapD = now;
            }

            if (dir.sqrMagnitude > 0.01f)
            {
                PerformThrusterDash(dir);
            }
        }

        private void PerformThrusterDash(Vector2 localDir)
        {
            lastDashTime = Time.time;
            dashEndTime = Time.time + dashDuration;
            dashLocalDir = localDir.normalized;
            combatStanceEndTime = Time.time + combatStancePersistDuration;
            ConsumeStamina(dashStaminaCost);

            Vector3 worldDashDir = (transform.right * dashLocalDir.x + transform.forward * dashLocalDir.y).normalized;
            dashWorldDir = worldDashDir;
            dashStartTime = Time.time;

            if (visualSockets == null) visualSockets = GetComponent<JaegerVisualSockets>() ?? GetComponentInChildren<JaegerVisualSockets>();
            visualSockets?.TriggerRocketBlast(dashDuration + 0.12f);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayRocketIgnition(transform.position + Vector3.up * 14f);
            StartCoroutine(RocketBurnAudioRoutine(dashDuration + 0.1f));

            if (WaterSplashManager.Instance != null)
            {
                WaterSplashManager.Instance.SpawnHeavyMoveWaterKickup(transform.position, worldDashDir, 1.85f, false);
            }
        }

        private void ToggleChainswords()
        {
            areChainswordsActive = !areChainswordsActive;
            combatStanceEndTime = Time.time + combatStancePersistDuration;

            chainswordDeployIsOpening = areChainswordsActive;
            chainswordDeployAnimStartTime = Time.time;
            chainswordDeployAnimEndTime = Time.time + (areChainswordsActive ? 0.72f : 0.38f);

            EnsureChainswordVisuals();
            if (activeChainswordAnimCoroutine != null) StopCoroutine(activeChainswordAnimCoroutine);
            activeChainswordAnimCoroutine = StartCoroutine(AnimateChainswordSlideRoutine(areChainswordsActive));

            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHydraulicServo(transform.position + Vector3.up * 13f, 0.85f);
            if (areChainswordsActive)
            {
                PacificRimAudio.PacificRimAudioManager.Instance?.PlaySteamVent(transform.position + Vector3.up * 14f);
            }

            PacificRimUI.JaegerHUD.Instance?.ShowBanner(
                areChainswordsActive ? "CHAIN SWORDS // DEPLOYED" : "CHAIN SWORDS // RETRACTED",
                1.6f);
        }

        private System.Collections.IEnumerator AnimateChainswordSlideRoutine(bool deploy)
        {
            float duration = deploy ? 0.68f : 0.34f;
            float elapsed = 0f;
            bool lockCuePlayed = false;

            Vector3 retractOffsetL = (chainswordBladeL != null && chainswordBladeL.transform.parent != null)
                ? chainswordBladeL.transform.parent.InverseTransformVector((chainswordBladeL.transform.parent.rotation * swordDeployedRotL) * Vector3.right * 0.55f)
                : Vector3.zero;
            Vector3 retractOffsetR = (chainswordBladeR != null && chainswordBladeR.transform.parent != null)
                ? chainswordBladeR.transform.parent.InverseTransformVector((chainswordBladeR.transform.parent.rotation * swordDeployedRotR) * Vector3.right * 0.55f)
                : Vector3.zero;

            if (deploy)
            {
                if (chainswordBladeL != null)
                {
                    chainswordBladeL.transform.localPosition = swordDeployedPosL + retractOffsetL;
                    chainswordBladeL.transform.localRotation = swordDeployedRotL;
                    chainswordBladeL.transform.localScale = Vector3.Scale(swordDeployedScaleL, new Vector3(0.02f, 0.35f, 0.35f));
                    chainswordBladeL.SetActive(true);
                }
                if (chainswordBladeR != null)
                {
                    chainswordBladeR.transform.localPosition = swordDeployedPosR + retractOffsetR;
                    chainswordBladeR.transform.localRotation = swordDeployedRotR;
                    chainswordBladeR.transform.localScale = Vector3.Scale(swordDeployedScaleR, new Vector3(0.02f, 0.35f, 0.35f));
                    chainswordBladeR.SetActive(true);
                }
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float rawT = Mathf.Clamp01(elapsed / duration);

                float slide;
                float xScaleFactor;
                float yzScaleFactor;
                float whipAngle = 0f;

                if (deploy)
                {
                    if (rawT < 0.75f)
                    {
                        float p = rawT / 0.75f;
                        float ease = 1f - Mathf.Pow(1f - p, 3f);
                        slide = ease;
                        xScaleFactor = Mathf.Lerp(0.02f, 1.06f, ease);
                        yzScaleFactor = Mathf.Lerp(0.35f, 1.0f, ease);
                        whipAngle = Mathf.Sin(rawT * 28f) * (1f - rawT) * 9f;
                    }
                    else
                    {
                        float settle = (rawT - 0.75f) / 0.25f;
                        slide = 1f;
                        xScaleFactor = Mathf.Lerp(1.06f, 1.0f, settle);
                        yzScaleFactor = 1f;
                        whipAngle = 0f;
                    }

                    if (!lockCuePlayed && rawT >= 0.72f)
                    {
                        lockCuePlayed = true;
                        PacificRimAudio.PacificRimAudioManager.Instance?.PlayParryClang(transform.position + Vector3.up * 15f);
                        ResolveHandTransforms();
                        if (handL != null)
                            WaterSplashManager.Instance?.SpawnCombatSparks(handL.position, transform.forward, isBlocked: true, isHeavy: false, isKaijuTarget: false);
                        if (handR != null)
                            WaterSplashManager.Instance?.SpawnCombatSparks(handR.position, transform.forward, isBlocked: true, isHeavy: false, isKaijuTarget: false);
                    }
                }
                else
                {
                    float ease = rawT * rawT;
                    slide = 1f - ease;
                    xScaleFactor = Mathf.Lerp(1.0f, 0.02f, ease);
                    yzScaleFactor = Mathf.Lerp(1.0f, 0.35f, ease);
                }

                Vector3 scaleMult = new Vector3(xScaleFactor, yzScaleFactor, yzScaleFactor);
                Quaternion whipRot = Quaternion.Euler(0f, whipAngle * 0.6f, whipAngle);

                if (chainswordBladeL != null)
                {
                    chainswordBladeL.transform.localPosition = Vector3.Lerp(swordDeployedPosL + retractOffsetL, swordDeployedPosL, slide);
                    chainswordBladeL.transform.localRotation = swordDeployedRotL * whipRot;
                    chainswordBladeL.transform.localScale = Vector3.Scale(swordDeployedScaleL, scaleMult);
                }
                if (chainswordBladeR != null)
                {
                    chainswordBladeR.transform.localPosition = Vector3.Lerp(swordDeployedPosR + retractOffsetR, swordDeployedPosR, slide);
                    chainswordBladeR.transform.localRotation = swordDeployedRotR * whipRot;
                    chainswordBladeR.transform.localScale = Vector3.Scale(swordDeployedScaleR, scaleMult);
                }

                yield return null;
            }

            if (chainswordBladeL != null)
            {
                chainswordBladeL.transform.localPosition = swordDeployedPosL;
                chainswordBladeL.transform.localRotation = swordDeployedRotL;
                chainswordBladeL.transform.localScale = swordDeployedScaleL;
                chainswordBladeL.SetActive(deploy);
            }
            if (chainswordBladeR != null)
            {
                chainswordBladeR.transform.localPosition = swordDeployedPosR;
                chainswordBladeR.transform.localRotation = swordDeployedRotR;
                chainswordBladeR.transform.localScale = swordDeployedScaleR;
                chainswordBladeR.SetActive(deploy);
            }
            activeChainswordAnimCoroutine = null;
        }

        private void EnsureChainswordVisuals()
        {
            ResolveHandTransforms();
            if (chainswordBladeL == null)
            {
                Transform leftSword = FindChildRecursive(transform, "LeftSword");
                if (leftSword != null) chainswordBladeL = leftSword.gameObject;
                else if (handL != null) chainswordBladeL = CreateChainswordBladeMesh("ChainswordBlade_L", handL, -1f);

                if (chainswordBladeL != null)
                {
                    swordDeployedPosL = chainswordBladeL.transform.localPosition;
                    swordDeployedRotL = chainswordBladeL.transform.localRotation;
                    swordDeployedScaleL = chainswordBladeL.transform.localScale;
                }
            }
            if (chainswordBladeR == null)
            {
                Transform rightSword = FindChildRecursive(transform, "RightSword");
                if (rightSword != null) chainswordBladeR = rightSword.gameObject;
                else if (handR != null) chainswordBladeR = CreateChainswordBladeMesh("ChainswordBlade_R", handR, 1f);

                if (chainswordBladeR != null)
                {
                    swordDeployedPosR = chainswordBladeR.transform.localPosition;
                    swordDeployedRotR = chainswordBladeR.transform.localRotation;
                    swordDeployedScaleR = chainswordBladeR.transform.localScale;
                }
            }
        }

        private static GameObject CreateChainswordBladeMesh(string objName, Transform handBone, float sideSign)
        {
            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = objName;
            var col = blade.GetComponent<Collider>();
            if (col != null) Destroy(col);

            blade.transform.SetParent(handBone, false);
            blade.transform.localPosition = new Vector3(sideSign * 0.004f, 0.018f, 0.012f);
            blade.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            blade.transform.localScale = new Vector3(0.0028f, 0.048f, 0.0085f);

            var rend = blade.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (urpLit != null)
                {
                    Material mat = new Material(urpLit);
                    mat.color = new Color(0.18f, 0.22f, 0.28f, 1f);
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.15f, 0.85f, 1.0f, 1f) * 2.2f);
                    rend.sharedMaterial = mat;
                }
            }
            blade.SetActive(false);
            return blade;
        }

        private void TriggerPlasmaCaster()
        {
            lastPlasmaTime = Time.time;
            plasmaAimStartTime = Time.time;
            plasmaFireTime = Time.time + 0.52f;
            plasmaAimEndTime = Time.time + 1.08f;
            combatStanceEndTime = Time.time + combatStancePersistDuration;
            ConsumeStamina(plasmaStaminaCost);

            if (cachedKaiju == null) cachedKaiju = FindAnyObjectByType<KaijuAI>();
            if (cachedKaiju != null && !cachedKaiju.IsDead)
            {
                plasmaTargetPos = cachedKaiju.transform.position + Vector3.up * 13.5f;
            }
            else if (Camera.main != null)
            {
                plasmaTargetPos = Camera.main.transform.position + Camera.main.transform.forward * 65f;
            }
            else
            {
                plasmaTargetPos = transform.position + transform.forward * 65f + Vector3.up * 14f;
            }

            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHydraulicServo(transform.position + Vector3.up * 13f, 0.9f);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayEmpChargeUp(transform.position + Vector3.up * 15f);
            PacificRimUI.JaegerHUD.Instance?.ShowBanner("I-19 PLASMACASTER // CHARGING", 1.1f);
            StartCoroutine(FirePlasmaBoltRoutine(0.52f));
        }

        private static Material CreatePlasmaMaterial(Color baseCol, Color hdrEmission, bool transparent = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Standard");
            if (shader == null) return null;

            Material mat = new Material(shader);
            mat.color = baseCol;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseCol);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", hdrEmission);

            if (transparent)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            return mat;
        }

        private System.Collections.IEnumerator FirePlasmaBoltRoutine(float windupDelay)
        {
            // --- Stage 1: Muzzle Charge-Up Orb ---
            GameObject chargeRoot = new GameObject("PlasmaMuzzleCharge");
            GameObject chargeCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            chargeCore.name = "Core";
            var coreCol = chargeCore.GetComponent<Collider>();
            if (coreCol != null) Destroy(coreCol);
            chargeCore.transform.SetParent(chargeRoot.transform, false);
            var coreRend = chargeCore.GetComponent<MeshRenderer>();
            if (coreRend != null)
                coreRend.sharedMaterial = CreatePlasmaMaterial(new Color(0.92f, 0.99f, 1f, 1f), new Color(0.6f, 0.95f, 1f, 1f) * 5.5f);

            GameObject chargeCorona = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            chargeCorona.name = "Corona";
            var coronaCol = chargeCorona.GetComponent<Collider>();
            if (coronaCol != null) Destroy(coronaCol);
            chargeCorona.transform.SetParent(chargeRoot.transform, false);
            chargeCorona.transform.localScale = Vector3.one * 1.65f;
            var coronaRend = chargeCorona.GetComponent<MeshRenderer>();
            if (coronaRend != null)
                coronaRend.sharedMaterial = CreatePlasmaMaterial(new Color(0.15f, 0.85f, 1f, 0.42f), new Color(0.12f, 0.80f, 1f, 1f) * 3.2f, transparent: true);

            Light chargeLight = chargeRoot.AddComponent<Light>();
            chargeLight.type = LightType.Point;
            chargeLight.color = new Color(0.2f, 0.88f, 1f, 1f);
            chargeLight.range = 28f;
            chargeLight.intensity = 10f;

            float chargeElapsed = 0f;
            bool chargeSparkSpawned = false;
            while (chargeElapsed < windupDelay)
            {
                if (this == null || IsDead)
                {
                    Destroy(chargeRoot);
                    yield break;
                }

                chargeElapsed += Time.deltaTime;
                float u = Mathf.Clamp01(chargeElapsed / Mathf.Max(0.01f, windupDelay));

                ResolveHandTransforms();
                Vector3 handPos = handR != null ? handR.position : (transform.position + transform.forward * 5f + Vector3.up * 15f);
                if (cachedKaiju != null && !cachedKaiju.IsDead)
                {
                    plasmaTargetPos = cachedKaiju.transform.position + Vector3.up * 13.5f;
                }
                Vector3 aimDir = (plasmaTargetPos - handPos).normalized;
                chargeRoot.transform.position = handPos + aimDir * 0.9f;

                float pulse = 1f + 0.18f * Mathf.Sin(chargeElapsed * 45f);
                float baseScale = Mathf.Lerp(0.15f, 2.3f, u * u) * pulse;
                chargeRoot.transform.localScale = Vector3.one * baseScale;
                chargeLight.intensity = Mathf.Lerp(10f, 420f, u);

                if (!chargeSparkSpawned && u >= 0.5f)
                {
                    chargeSparkSpawned = true;
                    WaterSplashManager.Instance?.SpawnCombatSparks(chargeRoot.transform.position, aimDir, isBlocked: false, isHeavy: false, isKaijuTarget: false);
                }

                yield return null;
            }

            Destroy(chargeRoot);
            if (this == null || IsDead) yield break;

            // --- Stage 2: Cannon Discharge, Muzzle Shockwave & Multi-Layered Projectile ---
            ResolveHandTransforms();
            if (cachedKaiju != null && !cachedKaiju.IsDead)
            {
                plasmaTargetPos = cachedKaiju.transform.position + Vector3.up * 13.5f;
            }

            Vector3 handMuzzleBase = handR != null ? handR.position : (transform.position + transform.forward * 5f + Vector3.up * 15f);
            Vector3 dir = (plasmaTargetPos - handMuzzleBase).normalized;
            Vector3 muzzlePos = handMuzzleBase + dir * 1.4f;

            PacificRimAudio.PacificRimAudioManager.Instance?.PlayEmpDischargeBlast(muzzlePos);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayAttackWhoosh(muzzlePos, isHeavy: true);

            if (rb != null) rb.AddForce(-dir * 16f, ForceMode.Impulse);
            TriggerAttackCameraFeedback(4, isHeavy: false, didHit: true);
            WaterSplashManager.Instance?.SpawnHeavyMoveWaterKickup(new Vector3(muzzlePos.x, transform.position.y, muzzlePos.z), dir, 1.75f, false);
            WaterSplashManager.Instance?.SpawnParrySparks(muzzlePos, dir);

            GameObject bolt = new GameObject("PlasmaCasterBolt");
            bolt.transform.position = muzzlePos;

            GameObject boltCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            boltCore.name = "PlasmaCore";
            var boltCoreCol = boltCore.GetComponent<Collider>();
            if (boltCoreCol != null) Destroy(boltCoreCol);
            boltCore.transform.SetParent(bolt.transform, false);
            boltCore.transform.localScale = Vector3.one * 1.5f;
            var boltCoreRend = boltCore.GetComponent<MeshRenderer>();
            if (boltCoreRend != null)
                boltCoreRend.sharedMaterial = CreatePlasmaMaterial(new Color(0.88f, 0.98f, 1f, 1f), new Color(0.75f, 0.97f, 1f, 1f) * 6.5f);

            GameObject boltShell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            boltShell.name = "PlasmaShell";
            var boltShellCol = boltShell.GetComponent<Collider>();
            if (boltShellCol != null) Destroy(boltShellCol);
            boltShell.transform.SetParent(bolt.transform, false);
            boltShell.transform.localScale = Vector3.one * 3.0f;
            var boltShellRend = boltShell.GetComponent<MeshRenderer>();
            if (boltShellRend != null)
                boltShellRend.sharedMaterial = CreatePlasmaMaterial(new Color(0.12f, 0.72f, 1.0f, 0.45f), new Color(0.10f, 0.72f, 1.0f, 1f) * 3.8f, transparent: true);

            var light = bolt.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.22f, 0.88f, 1.0f, 1f);
            light.range = 50f;
            light.intensity = 380f;

            var trail = bolt.AddComponent<TrailRenderer>();
            Shader trailShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (trailShader != null)
            {
                trail.sharedMaterial = new Material(trailShader);
            }
            trail.time = 0.28f;
            trail.startWidth = 2.1f;
            trail.endWidth = 0.05f;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.85f, 0.98f, 1f), 0f),
                    new GradientColorKey(new Color(0.15f, 0.74f, 1f), 0.45f),
                    new GradientColorKey(new Color(0.05f, 0.35f, 0.85f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.60f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = grad;

            float travelElapsed = 0f;
            float wakeTimer = 0f;
            bool steamVented = false;
            bool impacted = false;
            while (travelElapsed < 1.8f && bolt != null)
            {
                if (cachedKaiju != null && !cachedKaiju.IsDead)
                {
                    Vector3 toKaiju = ((cachedKaiju.transform.position + Vector3.up * 13.5f) - bolt.transform.position).normalized;
                    dir = Vector3.RotateTowards(dir, toKaiju, 1.6f * Time.deltaTime, 0f);
                }

                float step = plasmaBoltSpeed * Time.deltaTime;
                bolt.transform.position += dir * step;
                travelElapsed += Time.deltaTime;

                float shellPulse = 2.8f + 0.45f * Mathf.Sin(travelElapsed * 38f);
                boltShell.transform.localScale = Vector3.one * shellPulse;

                wakeTimer += Time.deltaTime;
                if (wakeTimer >= 0.06f)
                {
                    wakeTimer = 0f;
                    if (WaterSystem.Instance != null)
                    {
                        Vector3 boltPos = bolt.transform.position;
                        float wy = WaterSystem.Instance.GetWaterHeight(boltPos);
                        WaterSplashManager.Instance?.SpawnHeavyMoveWaterKickup(new Vector3(boltPos.x, wy, boltPos.z), dir, 1.15f, false);
                    }
                }

                if (!steamVented && travelElapsed >= 0.22f)
                {
                    steamVented = true;
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlaySteamVent(transform.position + Vector3.up * 14f);
                }

                if (cachedKaiju != null && !cachedKaiju.IsDead)
                {
                    float dist = Vector3.Distance(bolt.transform.position, cachedKaiju.transform.position + Vector3.up * 12f);
                    if (dist <= 10.5f)
                    {
                        impacted = true;
                        break;
                    }
                }
                yield return null;
            }

            // --- Stage 3: Plasma Detonation Fireball ---
            if (bolt != null)
            {
                Vector3 hitPos = bolt.transform.position;
                Destroy(bolt);

                if (impacted && cachedKaiju != null && !cachedKaiju.IsDead)
                {
                    float dmg = IsPoweredUp ? plasmaDamage * 1.25f : plasmaDamage;
                    cachedKaiju.TakeDamage(dmg, dir * 38f, attackVariant: 4, hitPoint: hitPos);
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlayLightningStrike(hitPos);
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlayJaegerPunchHit(hitPos, isHeavy: true);
                    WaterSplashManager.Instance?.SpawnParrySparks(hitPos, -dir);
                    WaterSplashManager.Instance?.SpawnCombatSparks(hitPos, -dir, isBlocked: false, isHeavy: true, isKaijuTarget: true);
                    WaterSplashManager.Instance?.SpawnHeavyMoveWaterKickup(hitPos, dir, 3.0f, true);
                    TriggerAttackCameraFeedback(4, isHeavy: true, didHit: true);
                }

                StartCoroutine(SpawnPlasmaDetonationRoutine(hitPos));
            }
        }

        private System.Collections.IEnumerator SpawnPlasmaDetonationRoutine(Vector3 hitPos)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "PlasmaDetonationFlash";
            var col = flash.GetComponent<Collider>();
            if (col != null) Destroy(col);
            flash.transform.position = hitPos;
            flash.transform.localScale = Vector3.one * 3f;

            Material flashMat = CreatePlasmaMaterial(new Color(0.55f, 0.94f, 1f, 0.72f), new Color(0.45f, 0.92f, 1f, 1f) * 6f, transparent: true);
            var rend = flash.GetComponent<MeshRenderer>();
            if (rend != null && flashMat != null)
            {
                rend.sharedMaterial = flashMat;
            }

            Light flashLight = flash.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = new Color(0.35f, 0.92f, 1f, 1f);
            flashLight.range = 65f;
            flashLight.intensity = 600f;

            float duration = 0.26f;
            float elapsed = 0f;
            while (elapsed < duration && flash != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(3f, 16f, 1f - Mathf.Pow(1f - t, 2f));
                flash.transform.localScale = Vector3.one * scale;
                flashLight.intensity = Mathf.Lerp(600f, 0f, t);

                if (flashMat != null)
                {
                    Color c = new Color(0.55f, 0.94f, 1f, Mathf.Lerp(0.72f, 0f, t));
                    flashMat.color = c;
                    if (flashMat.HasProperty("_BaseColor")) flashMat.SetColor("_BaseColor", c);
                }
                yield return null;
            }

            if (flash != null) Destroy(flash);
        }

        /// <summary>
        /// Plasma Caster aim pose: torso twists to lead with the right shoulder, chest pitches toward the
        /// target's elevation, the right arm extends along the shoulder->target line with a dropped elbow,
        /// and a decaying recoil kick snaps the forearm + chest back on fire. All blended by a smoothed weight.
        /// </summary>
        private void ApplyCombatArmsIK(float dt)
        {
            bool wantAim = IsPlasmaAiming && !IsEmoting && !IsDead;
            plasmaIKWeight = Mathf.MoveTowards(plasmaIKWeight, wantAim ? 1f : 0f, (wantAim ? 6.5f : 4f) * dt);

            // Recoil impulse fires once at plasmaFireTime, then decays
            if (Time.time >= plasmaFireTime && Time.time < plasmaFireTime + dt + 0.0001f)
            {
                plasmaRecoilKick = 1f;
            }
            plasmaRecoilKick = Mathf.MoveTowards(plasmaRecoilKick, 0f, 5.5f * dt);

            if (plasmaIKWeight > 0.001f && upperArmR != null && forearmR != null && handR != null)
            {
                float w = Mathf.SmoothStep(0f, 1f, plasmaIKWeight);
                float recoil = plasmaRecoilKick * plasmaRecoilKick; // sharp snap, smooth settle

                // 1. Torso twist toward target (leading with right shoulder) + elevation pitch + recoil rock
                Vector3 toTarget = plasmaTargetPos - (chest != null ? chest.position : transform.position);
                Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
                float yawToTarget = flat.sqrMagnitude > 0.01f ? Vector3.SignedAngle(transform.forward, flat.normalized, Vector3.up) : 0f;
                float twist = Mathf.Clamp(yawToTarget - 22f, -55f, 35f) * w - recoil * 8f; // -22deg brings right shoulder forward
                float pitch = flat.sqrMagnitude > 0.01f
                    ? Mathf.Clamp(-Mathf.Atan2(toTarget.y, flat.magnitude) * Mathf.Rad2Deg, -20f, 20f) * 0.5f * w
                    : 0f;

                if (spine1 != null)
                {
                    spine1.rotation = Quaternion.AngleAxis(twist * 0.55f, Vector3.up) * spine1.rotation;
                }
                if (chest != null)
                {
                    chest.rotation = Quaternion.AngleAxis(twist * 0.45f, Vector3.up)
                                   * Quaternion.AngleAxis(pitch - recoil * 11f, transform.right)
                                   * chest.rotation;
                }

                // 2. Right arm extends along shoulder -> target (re-read shoulder after torso twist)
                Vector3 shoulder = upperArmR.position;
                Vector3 aimDir = (plasmaTargetPos - shoulder).normalized;
                float l1 = Vector3.Distance(upperArmR.position, forearmR.position);
                float l2 = Vector3.Distance(forearmR.position, handR.position);
                float reach = (l1 + l2);

                // Slight elbow bend at rest (0.94), arm compresses strongly on recoil (0.70) and kicks upward
                float extension = Mathf.Lerp(0.94f, 0.70f, recoil);
                Vector3 recoilLift = Vector3.up * (reach * 0.20f * recoil) - aimDir * (reach * 0.08f * recoil);

                // Subtle high-frequency containment tremor during capacitor charge-up
                Vector3 chargeTremor = Vector3.zero;
                if (Time.time < plasmaFireTime)
                {
                    float chargeRatio = Mathf.Clamp01((Time.time - plasmaAimStartTime) / Mathf.Max(0.01f, plasmaFireTime - plasmaAimStartTime));
                    chargeTremor = (transform.right * Mathf.Sin(Time.time * 52f) + Vector3.up * Mathf.Cos(Time.time * 47f)) * (0.14f * chargeRatio);
                }

                Vector3 aimHandPos = shoulder + aimDir * (reach * extension) + recoilLift + chargeTremor;
                Vector3 blendedTarget = Vector3.Lerp(handR.position, aimHandPos, w);

                // Elbow drops down and slightly outward — natural "gun arm" posture
                Vector3 elbowPole = (-Vector3.up * 1.0f + transform.right * 0.45f - aimDir * 0.25f).normalized;
                SolveTwoBoneIKWorld(upperArmR, forearmR, handR, blendedTarget, elbowPole, keepTipLocalRotation: true);

                // 3. Left-arm counter-brace tucked across lower chest/ribs as stabilizer
                if (upperArmL != null && forearmL != null && handL != null)
                {
                    Vector3 braceAnchor = chest != null ? chest.position : (transform.position + Vector3.up * 14f);
                    float reachL = Vector3.Distance(upperArmL.position, forearmL.position) + Vector3.Distance(forearmL.position, handL.position);
                    Vector3 braceTarget = braceAnchor + transform.forward * (reachL * 0.42f) - transform.right * (reachL * 0.14f) - Vector3.up * (reachL * 0.16f);
                    Vector3 blendedBraceL = Vector3.Lerp(handL.position, braceTarget, w * 0.75f);
                    Vector3 poleL = (-Vector3.up - transform.right * 0.65f - transform.forward * 0.15f).normalized;
                    SolveTwoBoneIKWorld(upperArmL, forearmL, handL, blendedBraceL, poleL, keepTipLocalRotation: true);
                }
                return;
            }

            // 2. One-shot Chainsword Deployment / Retraction Arm Flourish (only while deploying/retracting)
            if (IsChainswordDeploying && !IsEmoting && !IsDead && !isBlocking && Time.time >= lightAttackEndTime && Time.time >= heavyAttackEndTime)
            {
                float totalDur = Mathf.Max(0.01f, chainswordDeployAnimEndTime - chainswordDeployAnimStartTime);
                float u = Mathf.Clamp01((Time.time - chainswordDeployAnimStartTime) / totalDur);
                float env = Mathf.Sin(u * Mathf.PI);

                if (upperArmL != null && forearmL != null && handL != null && upperArmR != null && forearmR != null && handR != null)
                {
                    Vector3 anchor = chest != null ? chest.position : (transform.position + Vector3.up * 14f);
                    float reachL = Vector3.Distance(upperArmL.position, forearmL.position) + Vector3.Distance(forearmL.position, handL.position);
                    float reachR = Vector3.Distance(upperArmR.position, forearmR.position) + Vector3.Distance(forearmR.position, handR.position);

                    Vector3 poseL;
                    Vector3 poseR;
                    if (chainswordDeployIsOpening)
                    {
                        if (u < 0.42f)
                        {
                            float p = Mathf.SmoothStep(0f, 1f, u / 0.42f);
                            poseL = Vector3.Lerp(handL.position, anchor + transform.forward * (reachL * 0.45f) - transform.right * (reachL * 0.18f) + Vector3.up * (reachL * 0.12f), p);
                            poseR = Vector3.Lerp(handR.position, anchor + transform.forward * (reachR * 0.45f) + transform.right * (reachR * 0.18f) + Vector3.up * (reachR * 0.12f), p);
                        }
                        else
                        {
                            float p = Mathf.SmoothStep(0f, 1f, (u - 0.42f) / 0.58f);
                            Vector3 chestGuardL = anchor + transform.forward * (reachL * 0.45f) - transform.right * (reachL * 0.18f) + Vector3.up * (reachL * 0.12f);
                            Vector3 chestGuardR = anchor + transform.forward * (reachR * 0.45f) + transform.right * (reachR * 0.18f) + Vector3.up * (reachR * 0.12f);
                            Vector3 snapOutL = anchor + transform.forward * (reachL * 0.40f) - transform.right * (reachL * 0.68f) - Vector3.up * (reachL * 0.14f);
                            Vector3 snapOutR = anchor + transform.forward * (reachR * 0.40f) + transform.right * (reachR * 0.68f) - Vector3.up * (reachR * 0.14f);
                            Vector3 snapPosL = Vector3.Lerp(chestGuardL, snapOutL, Mathf.Clamp01(p * 1.45f));
                            Vector3 snapPosR = Vector3.Lerp(chestGuardR, snapOutR, Mathf.Clamp01(p * 1.45f));
                            poseL = Vector3.Lerp(snapPosL, handL.position, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - 0.55f) / 0.45f)));
                            poseR = Vector3.Lerp(snapPosR, handR.position, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - 0.55f) / 0.45f)));
                        }
                    }
                    else
                    {
                        Vector3 tuckL = anchor + transform.forward * (reachL * 0.35f) - transform.right * (reachL * 0.28f);
                        Vector3 tuckR = anchor + transform.forward * (reachR * 0.35f) + transform.right * (reachR * 0.28f);
                        poseL = Vector3.Lerp(handL.position, tuckL, env);
                        poseR = Vector3.Lerp(handR.position, tuckR, env);
                    }

                    Vector3 poleL = (-Vector3.up - transform.right * 0.55f - transform.forward * 0.2f).normalized;
                    Vector3 poleR = (-Vector3.up + transform.right * 0.55f - transform.forward * 0.2f).normalized;
                    SolveTwoBoneIKWorld(upperArmL, forearmL, handL, poseL, poleL, keepTipLocalRotation: true);
                    SolveTwoBoneIKWorld(upperArmR, forearmR, handR, poseR, poleR, keepTipLocalRotation: true);
                }
            }
        }
    }
}
