using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Heavy mech / character controller with colossal momentum and combat strikes
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
        private float hitReactionEndTime = -10f;
        private Coroutine activeDelayedStrikeCoroutine = null;
        private float sprintAttackEndTime = -10f;

        [Header("Sprint Attack Lunge Momentum")]
        [Tooltip("Deceleration drag for the heavy 2,000-ton Jaeger forward jump lunge.")]
        public float lungeDrag = 14.5f;
        private Vector3 lungeVelocity = Vector3.zero;

        public bool IsHitStaggered => Time.time < hitAttackDisallowedUntil;
        public bool IsHitReacting => Time.time < hitReactionEndTime;
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

        [Header("Guard, Chainswords, Plasma Caster & Thruster Sidestep")]
        public KeyCode blockKey = KeyCode.F;
        public KeyCode chainswordKey = KeyCode.R;
        public KeyCode plasmaCasterKey = KeyCode.T;
        public float fistDamageMultiplier = 0.75f;
        public float chainswordDamageMultiplier = 1.25f;
        public float fistStunMultiplier = 1.0f;
        public float chainswordStunMultiplier = 0.70f;
        public float chainswordExtraRange = 2.5f;
        public float EffectiveChainswordExtraRange => Mathf.Max(chainswordExtraRange, 0f);
        [Header("I-19 Plasmacaster Ultimate")]
        public float plasmaDamage = 213f;
        public float maxUltimateCharge = 100f;
        [Tooltip("Progress toward the I-19 Plasmacaster Ultimate (always resets to a flat 0 on spawn).")]
        public float currentUltimateCharge = 0f;
        public float ultChargeLightHit = 6f;
        public float ultChargeBlockedHit = 2.5f;
        public float ultChargeHeavyHit = 12f;
        public float ultChargeGuardBreakBonus = 4f;
        public float ultChargeParry = 18f;
        public float ultChargeBlock = 4f;
        public float ultChargeDashEvade = 10f;
        public float plasmaBoltSpeed = 135f;
        [HideInInspector] public float plasmaCooldown = 15f;
        [HideInInspector, Range(0.05f, 1f)] public float plasmaStaminaFraction = 0.70f;

        [Header("Plasma Caster Charge & IK Rework")]
        [Tooltip("Base windup duration (seconds) before the I-19 Plasmacaster discharges.")]
        public float plasmaChargeDuration = 2.35f;
        [Tooltip("Extra charge-up delay (seconds) added each time Gipsy is hit while charging.")]
        public float plasmaChargeHitPenalty = 0.15f;
        [Tooltip("Incoming damage multiplier while charging the I-19 Plasmacaster.")]
        public float plasmaChargeDamageMultiplier = 2.0f;
        [Tooltip("Recoil follow-through duration (seconds) after discharge.")]
        public float plasmaRecoilDuration = 0.48f;
        [Tooltip("Smooth return-to-stance duration (seconds) after recoil.")]
        public float plasmaRecoverDuration = 0.55f;
        [Tooltip("Movement speed multiplier while charging the Plasmacaster.")]
        public float plasmaChargeMoveMultiplier = 0.42f;
        [Tooltip("Movement speed multiplier during Plasmacaster discharge recoil.")]
        public float plasmaRecoilMoveMultiplier = 0.15f;
        [Tooltip("Radial outward flare angle (degrees) for right-hand fingers around the barrel aperture.")]
        public float plasmaFingerSplayAngle = 38f;
        [Tooltip("Roll tilt (degrees) of the right palm/barrel relative to the inward brace axis.")]
        public float plasmaBarrelPalmTilt = 26f;

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

        // Finger bone chains for procedural I-19 Plasmacaster aperture & left forearm brace
        private Transform thumbR1, thumbR2;
        private Transform indexR1, indexR2, indexR3;
        private Transform middleR1, middleR2, middleR3;
        private Transform ringR1, ringR2, ringR3;
        private Transform pinkyR1, pinkyR2, pinkyR3;

        private Transform thumbL1, thumbL2;
        private Transform indexL1, indexL2, indexL3;
        private Transform middleL1, middleL2, middleL3;
        private Transform ringL1, ringL2, ringL3;
        private Transform pinkyL1, pinkyL2, pinkyL3;

        private enum PlasmaState
        {
            Idle,
            Charging,
            FiringRecoil,
            Recovering
        }

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
        private PlasmaState plasmaState = PlasmaState.Idle;
        private float lastPlasmaTime = -999f;
        private float plasmaAimStartTime = -999f;
        private float plasmaAimEndTime = -999f;
        private float plasmaFireTime = -999f;
        private float plasmaChargeElapsed = 0f;
        private float plasmaCurrentChargeDuration = 1.25f;
        private float plasmaPhaseElapsed = 0f;
        private float lastPlasmaDeniedBannerTime = -10f;
        private float lastPlasmaHitBannerTime = -10f;
        private bool plasmaSuppressedSwordL = false;
        private bool plasmaSuppressedSwordR = false;
        private bool plasmaLockedOnKaiju = false;
        private Vector3 plasmaTargetPos;
        private Vector3 plasmaSolvedBarrelPos;
        private Vector3 plasmaSolvedAimDir = Vector3.forward;
        private Coroutine activePlasmaCoroutine;
        private GameObject activePlasmaChargeRoot;
        private Transform plasmaCoreTransform;
        private Transform plasmaCoronaTransform;
        private Transform plasmaRingATransform;
        private Transform plasmaRingBTransform;
        private Light activePlasmaChargeLight;
        private static Mesh cachedPlasmaRingMesh;
        private float lastDashTime = -10f;
        private float dashEndTime = -10f;
        private Vector2 dashLocalDir = Vector2.zero;
        private Vector3 dashWorldDir = Vector3.forward;
        private float dashStartTime = -10f;
        private float lastDashEvadeChargeTime = -10f;

        // Smoothed procedural IK blend weights (prevent popping when abilities start/stop/interrupt)
        private float plasmaIKWeight = 0f;
        private float plasmaFingerOpenWeight = 0f;
        private float dashIKWeight = 0f;
        private float plasmaRecoilKick = 0f;
        private float lastTapW = -10f;
        private float lastTapS = -10f;
        private float lastTapA = -10f;
        private float lastTapD = -10f;

        public bool AreChainswordsActive => areChainswordsActive;
        public bool IsChainswordDeploying => Time.time < chainswordDeployAnimEndTime;
        public bool IsPlasmaAiming => plasmaState != PlasmaState.Idle;
        public bool IsPlasmaCharging => plasmaState == PlasmaState.Charging;
        public bool IsPlasmaRecoil => plasmaState == PlasmaState.FiringRecoil;
        public bool IsPlasmaRecovering => plasmaState == PlasmaState.Recovering;
        public bool IsPlasmaCinematicActive => plasmaState != PlasmaState.Idle && !IsDead;
        public float PlasmaCurrentChargeDuration => plasmaState == PlasmaState.Charging ? plasmaCurrentChargeDuration : plasmaChargeDuration;
        public float PlasmaChargeProgress => plasmaState == PlasmaState.Charging
            ? Mathf.Clamp01(plasmaChargeElapsed / Mathf.Max(0.01f, plasmaCurrentChargeDuration))
            : 0f;
        public float PlasmaRecoilProgress => plasmaState == PlasmaState.FiringRecoil
            ? Mathf.Clamp01(plasmaPhaseElapsed / Mathf.Max(0.01f, plasmaRecoilDuration))
            : 0f;
        public float PlasmaRecoverProgress => plasmaState == PlasmaState.Recovering
            ? Mathf.Clamp01(plasmaPhaseElapsed / Mathf.Max(0.01f, plasmaRecoverDuration))
            : 0f;
        public Transform RightUpperArmTransform => upperArmR;
        public Transform RightForearmTransform => forearmR;
        public Transform RightHandTransform => handR;
        public Vector3 JaegerChestWorldPos => chest != null ? chest.position : (transform.position + Vector3.up * 14f);
        public Vector3 PlasmaBarrelMuzzleWorldPos => plasmaSolvedBarrelPos.sqrMagnitude > 0.01f ? plasmaSolvedBarrelPos : ComputeSolvedBarrelMuzzlePos();
        public Vector3 PlasmaAimDirectionWorld => plasmaSolvedAimDir.sqrMagnitude > 0.01f ? plasmaSolvedAimDir : transform.forward;
        public float UltimateCharge => Mathf.Clamp(currentUltimateCharge, 0f, Mathf.Max(1f, maxUltimateCharge));
        public float UltimateChargeRatio => maxUltimateCharge > 0.01f ? Mathf.Clamp01(currentUltimateCharge / maxUltimateCharge) : 0f;
        public int UltimateChargePercent => IsUltimateReady ? 100 : Mathf.Clamp(Mathf.FloorToInt(UltimateChargeRatio * 100f + 0.0001f), 0, 99);
        public bool IsUltimateReady => !IsDead && currentUltimateCharge >= Mathf.Max(1f, maxUltimateCharge) - 0.001f;
        public float PlasmaCooldownRemaining => 0f;
        public float PlasmaCooldownRatio => UltimateChargeRatio;
        public float RequiredPlasmaStamina => 0f;
        public bool HasEnoughStaminaForPlasma => IsUltimateReady;
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
        public bool IsGodModeActive { get; private set; } = false;
        public bool IsInfiniteStaminaActive { get; private set; } = false;

        public void ToggleIronmanCheat()
        {
            SetIronmanCheat(!IsIronmanCheatActive);
        }

        public void SetIronmanCheat(bool active)
        {
            IsIronmanCheatActive = active;
        }

        public void SetGodMode(bool active)
        {
            IsGodModeActive = active;
        }

        public void SetInfiniteStamina(bool active)
        {
            IsInfiniteStaminaActive = active;
            if (active)
            {
                currentStamina = maxStamina;
                IsExhausted = false;
            }
        }

        public void DebugRestoreFull()
        {
            currentHealth = maxHealth;
            currentStamina = maxStamina;
            IsExhausted = false;
        }

        public void AddUltimateCharge(float amount)
        {
            if (IsDead || amount <= 0f) return;
            float maxUlt = Mathf.Max(1f, maxUltimateCharge);
            bool wasReady = currentUltimateCharge >= maxUlt - 0.001f;
            currentUltimateCharge = Mathf.Clamp(currentUltimateCharge + amount, 0f, maxUlt);
            bool isNowReady = currentUltimateCharge >= maxUlt - 0.001f;
            if (!wasReady && isNowReady)
            {
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayHUDStaminaRecovered35();
                PacificRimUI.JaegerHUD.Instance?.ShowBanner($"I-19 PLASMACASTER // ULTIMATE READY [{plasmaCasterKey.ToString().ToUpperInvariant()}]", 2.2f);
            }
        }

        public void DebugFillUltimateCharge()
        {
            currentUltimateCharge = Mathf.Max(1f, maxUltimateCharge);
            lastPlasmaTime = -999f;
        }

        public void DebugResetPlasmaCooldown() => DebugFillUltimateCharge();

        public void DebugActivateOvercharge()
        {
            powerUpEndTime = Time.time + powerUpDuration;
        }

        public void DebugToggleChainswords()
        {
            ToggleChainswords();
        }

        public void DebugApplyDamage(float amount)
        {
            if (IsDead) return;
            bool wasGod = IsGodModeActive;
            IsGodModeActive = false;
            TakeDamage(amount, Vector3.zero, amount >= 65f);
            IsGodModeActive = wasGod;
        }

        public void TriggerVictoryPose()
        {
            if (IsDead) return;
            PerformEmote();
        }

        private JaegerVisualSockets visualSockets;
        private JaegerBattleDamageVFX battleDamageVfx;

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
            maxUltimateCharge = Mathf.Max(1f, maxUltimateCharge);
            currentUltimateCharge = 0f;
            lastPlasmaTime = -999f;
            waterInteraction = GetComponent<JaegerWaterInteraction>();
            if (animator == null) animator = GetComponent<Animator>();
            cachedKaiju = FindAnyObjectByType<KaijuAI>();
            visualSockets = GetComponent<JaegerVisualSockets>() ?? GetComponentInChildren<JaegerVisualSockets>();
            battleDamageVfx = GetComponent<JaegerBattleDamageVFX>() ?? gameObject.AddComponent<JaegerBattleDamageVFX>();
            ResolveRigTransforms();
            areChainswordsActive = false;
            EnsureChainswordVisuals();
            if (chainswordBladeL != null) chainswordBladeL.SetActive(false);
            if (chainswordBladeR != null) chainswordBladeR.SetActive(false);
        }

        private void OnDestroy()
        {
            if (activePlasmaChargeRoot != null)
            {
                Destroy(activePlasmaChargeRoot);
                activePlasmaChargeRoot = null;
            }
            FreezeKaijuForPlasma(false);
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
                if (thumbL1 == null) thumbL1 = FindChildRecursive(handL, "Thumb.L");
                if (thumbL2 == null && thumbL1 != null) thumbL2 = FindChildRecursive(thumbL1, "Thumb2.L");
                if (indexL1 == null) indexL1 = FindChildRecursive(handL, "Index.L");
                if (indexL2 == null && indexL1 != null) indexL2 = FindChildRecursive(indexL1, "Index2.L");
                if (indexL3 == null && indexL2 != null) indexL3 = FindChildRecursive(indexL2, "Index3.L");
                if (middleL1 == null) middleL1 = FindChildRecursive(handL, "Middle.L");
                if (middleL2 == null && middleL1 != null) middleL2 = FindChildRecursive(middleL1, "Middle2.L");
                if (middleL3 == null && middleL2 != null) middleL3 = FindChildRecursive(middleL2, "Middle3.L");
                if (ringL1 == null) ringL1 = FindChildRecursive(handL, "Ring.L");
                if (ringL2 == null && ringL1 != null) ringL2 = FindChildRecursive(ringL1, "Ring2.L");
                if (ringL3 == null && ringL2 != null) ringL3 = FindChildRecursive(ringL2, "Ring3.L");
                if (pinkyL1 == null) pinkyL1 = FindChildRecursive(handL, "Pinky.L");
                if (pinkyL2 == null && pinkyL1 != null) pinkyL2 = FindChildRecursive(pinkyL1, "Pinky2.L");
                if (pinkyL3 == null && pinkyL2 != null) pinkyL3 = FindChildRecursive(pinkyL2, "Pinky3.L");
            }
            if (handR != null)
            {
                if (forearmR == null) forearmR = handR.parent;
                if (upperArmR == null && forearmR != null) upperArmR = forearmR.parent;
                if (thumbR1 == null) thumbR1 = FindChildRecursive(handR, "Thumb.R");
                if (thumbR2 == null && thumbR1 != null) thumbR2 = FindChildRecursive(thumbR1, "Thumb2.R");
                if (indexR1 == null) indexR1 = FindChildRecursive(handR, "Index.R");
                if (indexR2 == null && indexR1 != null) indexR2 = FindChildRecursive(indexR1, "Index2.R");
                if (indexR3 == null && indexR2 != null) indexR3 = FindChildRecursive(indexR2, "Index3.R");
                if (middleR1 == null) middleR1 = FindChildRecursive(handR, "Middle.R");
                if (middleR2 == null && middleR1 != null) middleR2 = FindChildRecursive(middleR1, "Middle2.R");
                if (middleR3 == null && middleR2 != null) middleR3 = FindChildRecursive(middleR2, "Middle3.R");
                if (ringR1 == null) ringR1 = FindChildRecursive(handR, "Ring.R");
                if (ringR2 == null && ringR1 != null) ringR2 = FindChildRecursive(ringR1, "Ring2.R");
                if (ringR3 == null && ringR2 != null) ringR3 = FindChildRecursive(ringR2, "Ring3.R");
                if (pinkyR1 == null) pinkyR1 = FindChildRecursive(handR, "Pinky.R");
                if (pinkyR2 == null && pinkyR1 != null) pinkyR2 = FindChildRecursive(pinkyR1, "Pinky2.R");
                if (pinkyR3 == null && pinkyR2 != null) pinkyR3 = FindChildRecursive(pinkyR2, "Pinky3.R");
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
            if (IsInfiniteStaminaActive)
            {
                currentStamina = maxStamina;
                IsExhausted = false;
            }

            if (PacificRimUI.MainMenuUI.IsMenuOpen || isCutsceneActive)
            {
                return;
            }

            if (PacificRimUI.PacificRimAdminUI.IsOpen)
            {
                targetVelocity = Vector3.zero;
                isBlocking = false;
                if (animator != null) animator.SetFloat("Speed", 0f);
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

            bool isEmotingNow = IsEmoting;

            // Attack status: light punch combo window or heavy attack slam
            bool isHeavyAttacking = Time.time < heavyAttackEndTime;
            bool isLightAttacking = !isHeavyAttacking && Time.time < lightAttackEndTime;
            bool isAttacking = isHeavyAttacking || isLightAttacking;

            // Emote trigger via Q (slows movement to 40% for duration, grants 15s power-up buff)
            if (emoteTriggered && !isEmotingNow && !IsPlasmaAiming && Time.time >= lastHeavyAttackTime + 0.5f && Time.time >= lastPunchTime + 0.5f)
            {
                PerformEmote();
                isEmotingNow = IsEmoting;
            }

            // Chainswords toggle (R) - locked while firing the Plasmacaster
            if (chainswordTriggered && !isEmotingNow && !IsPlasmaAiming)
            {
                ToggleChainswords();
            }

            // Plasma Caster Ultimate fire (T) - requires 100% earned Ultimate charge
            if (plasmaTriggered)
            {
                TryTriggerPlasmaCaster();
            }

            // Double-tap WASD Thruster Sidestep / Dash (disabled while charging/firing Plasmacaster)
            if (!IsPlasmaAiming)
            {
                CheckDoubleTapDash(tapW, tapS, tapA, tapD);
            }

            // Block status via F (active when holding F, not emoting, attacking, exhausted, or using Plasmacaster)
            bool wasBlockingBefore = isBlocking;
            isBlocking = blockHeld && !isAttacking && !isEmotingNow && !IsExhausted && !IsPlasmaAiming;
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

            // Toggle / Hold Shift for running mode (sprinting disabled while emoting, blocking, heavy attacking, or firing Plasmacaster)
            if (isEmotingNow || isBlocking || isHeavyAttacking || IsPlasmaAiming)
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

            // Only consider running if actually moving forward and not emoting/blocking/heavy attacking/aiming Plasmacaster
            bool effectivelyRunning = isRunning && vertical > 0.1f && !isEmotingNow && !isBlocking && !isHeavyAttacking && !IsPlasmaAiming;

            if (!bodyYawInitialized)
            {
                currentBodyYaw = transform.eulerAngles.y;
                bodyYawInitialized = true;
            }

            // Turn rotation & omnidirectional velocity
            Vector2 moveInput = IsPlasmaAiming
                ? Vector2.zero
                : Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
            if (IsPlasmaAiming)
            {
                Vector3 aimFlat = GetLockedPlasmaTargetPos() - transform.position;
                aimFlat.y = 0f;
                if (aimFlat.sqrMagnitude > 0.01f)
                {
                    float targetAimYaw = Mathf.Atan2(aimFlat.x, aimFlat.z) * Mathf.Rad2Deg;
                    currentBodyYaw = Mathf.SmoothDampAngle(currentBodyYaw, targetAimYaw, ref yawTurnVelocity, 0.14f, 360f);
                    Quaternion bodyRot = Quaternion.Euler(0f, currentBodyYaw, 0f);
                    transform.rotation = bodyRot;
                    if (rb != null) rb.rotation = bodyRot;
                }
            }
            else if (enableStrafeAndLegIK)
            {
                if (cameraFollow == null && Camera.main != null)
                {
                    cameraFollow = Camera.main.GetComponent<JaegerCameraFollow>();
                }
                bool isFreeCam = cameraFollow != null && cameraFollow.isFreeCam;

                if (!isFreeCam && alignYawToCameraWhenMoving && Camera.main != null)
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
            else if (Mathf.Abs(horizontal) > 0.05f)
            {
                currentBodyYaw += horizontal * turnSpeed * Time.deltaTime;
                Quaternion bodyRot = Quaternion.Euler(0f, currentBodyYaw, 0f);
                transform.rotation = bodyRot;
                if (rb != null) rb.rotation = bodyRot;
            }

            currentRunBlend = Mathf.MoveTowards(currentRunBlend, effectivelyRunning ? 1f : 0f, Time.deltaTime * Mathf.Max(0.2f, runTransitionSpeed));

            // Movement velocity target (40% speed while emoting, planted steady stance during Ultimate cinematic)
            Vector3 moveVec;
            if (IsPlasmaAiming)
            {
                moveVec = Vector3.zero;
            }
            else if (enableStrafeAndLegIK)
            {
                moveVec = transform.forward * moveInput.y + transform.right * (moveInput.x * strafeSpeedMultiplier);
            }
            else
            {
                moveVec = transform.forward * vertical;
            }

            float currentSpeed = Mathf.Lerp(walkSpeed, runSpeed, currentRunBlend);

            // Apply emote, plasma caster, attack, or block movement speed slowdown (combat stance has no speed debuff)
            if (isEmotingNow)
            {
                currentSpeed *= 0.40f;
            }
            else if (plasmaState == PlasmaState.Charging || plasmaState == PlasmaState.FiringRecoil)
            {
                currentSpeed = 0f;
            }
            else if (plasmaState == PlasmaState.Recovering)
            {
                float recT = Mathf.Clamp01(plasmaPhaseElapsed / Mathf.Max(0.01f, plasmaRecoverDuration));
                currentSpeed *= Mathf.Lerp(0f, 1.0f, recT);
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

            float moveMagnitude = enableStrafeAndLegIK
                ? Mathf.Clamp01(moveInput.magnitude)
                : Mathf.Clamp01(Mathf.Abs(vertical) + Mathf.Abs(horizontal) * 0.5f);

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
                    // Lower body layer (Hips down) overrides locomotion while attacking, blocking, emoting, or reacting to hits on the move
                    float targetLowerWeight = (isMoving && (isAttacking || isBlocking || isEmotingNow || IsHitReacting)) ? 1.0f : 0.0f;
                    lowerBodyLayerWeight = Mathf.MoveTowards(lowerBodyLayerWeight, targetLowerWeight, Time.deltaTime * 12.0f);
                    animator.SetLayerWeight(1, lowerBodyLayerWeight);
                }
            }

            // Attacks are allowed regardless of previous stance (engages combat stance automatically for 5s)
            float currentPunchCooldown = IsPoweredUp ? (punchCooldown * powerUpCooldownMultiplier) : punchCooldown;
            float currentHeavyCooldown = IsPoweredUp ? (heavyAttackCooldown * powerUpCooldownMultiplier) : heavyAttackCooldown;

            if (!isEmotingNow && !isBlocking && !IsHitStaggered && !IsPlasmaAiming)
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

            if (lastMoveMagnitude > 0.08f && enableStrafeAndLegIK)
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
        private static void SolveTwoBoneIKWorld(Transform root, Transform mid, Transform tip, Vector3 targetPos, Vector3 poleForward, bool keepTipLocalRotation = false, float animHintWeight = 0.65f)
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

            // Blend animated bend direction with explicit pole hint (legs keep more anim hint; arms favor pole)
            float hw = Mathf.Clamp01(animHintWeight);
            Vector3 animMidProj = a + Vector3.Project(b - a, dir);
            Vector3 animKneeDir = b - animMidProj;
            Vector3 poleProj = Vector3.ProjectOnPlane(poleForward, dir);
            Vector3 poleUnit = poleProj.sqrMagnitude > 0.0001f ? poleProj.normalized : dir;
            Vector3 bendDir = Vector3.ProjectOnPlane(animKneeDir * hw + poleUnit * (l1 * (1f - hw)), dir);
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
            if (amount <= 0f || IsInfiniteStaminaActive) return;
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

            // Wait until 90% of the previous attack animation has finished so damage always resolves before chaining
            float landTime = punchNumber == 2 ? 0.46f : (punchNumber == 3 ? 0.48f : 0.51f);
            punchComboWindowStartTime = Time.time + (effectiveClipDuration * 0.90f);

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
            currentPunchDamage *= areChainswordsActive ? chainswordDamageMultiplier : fistDamageMultiplier;
            float currentPunchRange = areChainswordsActive ? (punchRange + EffectiveChainswordExtraRange) : punchRange;
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
            currentHeavyDamage *= areChainswordsActive ? chainswordDamageMultiplier : fistDamageMultiplier;
            float currentHeavyRange = areChainswordsActive ? (heavyAttackRange + EffectiveChainswordExtraRange) : heavyAttackRange;
            if (activeDelayedStrikeCoroutine != null) StopCoroutine(activeDelayedStrikeCoroutine);
            activeDelayedStrikeCoroutine = StartCoroutine(ExecuteDelayedStrike(variant, currentHeavyDamage, currentHeavyRange, landTime / animSpeed, isHeavy: true));

            Debug.Log($"<color=#ffaa00><b>[Jaeger]</b></color> {(runningAttack ? "Running Heavy Attack (Uninterruptible)" : "Heavy Attack")} initiated! (fist impact in {landTime / animSpeed:F2}s)");
        }

        private System.Collections.IEnumerator ExecuteDelayedStrike(int attackVariant, float damage, float range, float delay, bool isHeavy)
        {
            yield return new WaitForSeconds(delay);

            if (this == null || !gameObject.activeInHierarchy || (Time.time < hitAttackDisallowedUntil && attackVariant != 5)) yield break;

            ResolveHandTransforms();
            // Right-hand strikes: Punch1 (variant 1), Light Uppercut Punch3 (variant 3), Standing Heavy (variant 4)
            Transform activeHand = (attackVariant == 1 || attackVariant == 3 || attackVariant == 4) ? handR : handL;
            Vector3 bladeForwardOffset = areChainswordsActive ? transform.forward * (EffectiveChainswordExtraRange * 0.45f) : Vector3.zero;
            Vector3 strikePos = (activeHand != null ? activeHand.position : (transform.position + transform.forward * (isHeavy ? 7.5f : 5.5f) + Vector3.up * 15f)) + bladeForwardOffset;

            bool didHit = ApplyStrikeImpact(strikePos, range, damage, isHeavy ? 35f : 20f, isHeavy ? 20f : 14f, isHeavy ? 3.0f : 2.0f, attackVariant);
            TriggerAttackCameraFeedback(attackVariant, isHeavy, didHit);

            if (WaterSplashManager.Instance != null)
            {
                WaterSplashManager.Instance.SpawnHeavyMoveWaterKickup(strikePos, transform.forward, isHeavy ? 2.65f : 1.35f, isHeavy);
            }

            if (didHit)
            {
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayJaegerPunchHit(strikePos, isHeavy, areChainswordsActive);
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

            Debug.Log($"<color=#00e5ff><b>[Jaeger Emote]</b></color> Victory/Battle roar unleashed! Movement at 40% speed for {EmoteClipLength:F1}s. Power-Up active for {powerUpDuration}s (+30% attack speed, +25% cooldown speed, glowing reactor)!");
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
            float forwardReach = (attackVariant >= 4 ? 7.5f : 6.0f) + (areChainswordsActive ? EffectiveChainswordExtraRange * 0.65f : 0f);
            Vector3 fallbackPos = transform.position + transform.forward * forwardReach + Vector3.up * 15f;

            for (int pass = 0; pass < 2 && !damagedKaiju; pass++)
            {
                Vector3 center = pass == 0 ? rawStrikePos : fallbackPos;
                Collider[] hits = Physics.OverlapSphere(center, range);
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
                            float stunMult = areChainswordsActive ? chainswordStunMultiplier : fistStunMultiplier;
                            Vector3 knockback = transform.forward * knockbackDist;
                            kaiju.TakeDamage(effectiveDamage, knockback, attackVariant, rawStrikePos, stunMult);
                            damagedKaiju = true;

                            if (!kaiju.LastHitWasParried && kaiju.LastHitDealtDamage)
                            {
                                bool isHeavyStrike = attackVariant >= 4;
                                if (kaiju.LastHitBrokeGuard)
                                {
                                    AddUltimateCharge(ultChargeHeavyHit + ultChargeGuardBreakBonus);
                                }
                                else if (kaiju.LastHitWasBlocked)
                                {
                                    AddUltimateCharge(ultChargeBlockedHit);
                                }
                                else
                                {
                                    AddUltimateCharge(isHeavyStrike ? ultChargeHeavyHit : ultChargeLightHit);
                                }
                            }
                        }
                        didHit = true;
                    }
                    else if (pass == 0 && !col.isTrigger)
                    {
                        didHit = true;
                    }
                }
            }

            return didHit;
        }

        private void PlayHitReactionAnimation(bool isHitLeft, float duration = 0.58f)
        {
            combatStanceEndTime = Mathf.Max(combatStanceEndTime, Time.time + combatStancePersistDuration);
            inCombatStance = true;
            hitReactionEndTime = Time.time + duration;

            if (animator == null) return;

            animator.ResetTrigger("HitLeft");
            animator.ResetTrigger("HitRight");
            animator.SetBool("InCombat", true);

            string targetState = isHitLeft ? "HitLeft" : "HitRight";
            animator.CrossFadeInFixedTime(targetState, 0.08f, 0, 0f);

            if (animator.layerCount > 1)
            {
                bool isMoving = lastMoveMagnitude > 0.1f;
                float targetLower = isMoving ? 1f : 0f;
                lowerBodyLayerWeight = targetLower;
                animator.SetLayerWeight(1, targetLower);
            }
        }

        public void OnParriedByKaiju(Vector3 recoilImpulse, Vector3 sparkWorldPos)
        {
            if (IsDead) return;

            hitAttackDisallowedUntil = Time.time + 0.65f;
            isPunchBuffered = false;
            heavyAttackEndTime = Time.time - 1f;
            lightAttackEndTime = Time.time - 1f;
            if (activeDelayedStrikeCoroutine != null)
            {
                StopCoroutine(activeDelayedStrikeCoroutine);
                activeDelayedStrikeCoroutine = null;
            }
            CancelPlasmaCaster(immediate: false);
            ResetAttackTriggers();

            PlayHitReactionAnimation(isHitLeft: false, duration: 0.58f);

            if (rb == null) rb = GetComponent<Rigidbody>();
            if (rb != null && recoilImpulse.sqrMagnitude > 0.01f)
            {
                rb.AddForce(recoilImpulse, ForceMode.Impulse);
            }

            Camera cam = Camera.main;
            PacificRimCameraShake shaker = cam != null ? cam.GetComponent<PacificRimCameraShake>() : null;
            if (shaker != null) shaker.TriggerBlockImpactFeedback(2.6f);

            PacificRimAudio.PacificRimAudioManager.Instance?.PlayArmorCreak(transform.position + Vector3.up * 14f, 0.95f);
            PacificRimUI.JaegerHUD.Instance?.TriggerScreenImpact(wasBlocked: true, isHeavy: true);
            WaterSplashManager.Instance?.SpawnParrySparks(sparkWorldPos, -transform.forward);
        }

        public void TakeDamage(float amount, Vector3 knockback, bool isHeavy = false, Vector3 hitPoint = default)
        {
            if (IsDead || IsGodModeActive) return;
            if (IsDashing)
            {
                if (Time.time - lastDashEvadeChargeTime >= dashDuration + 0.15f)
                {
                    lastDashEvadeChargeTime = Time.time;
                    AddUltimateCharge(ultChargeDashEvade);
                }
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
                AddUltimateCharge(ultChargeParry);

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
                AddUltimateCharge(isHeavy ? ultChargeBlock * 1.5f : ultChargeBlock);
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
            else if (IsPlasmaCharging)
            {
                // Charging the I-19 Plasmacaster grants hyper-armor (cannot be cancelled/staggered),
                // but exposes Gipsy to 2x incoming damage and lengthens remaining charge-up by +0.15s per hit.
                actualAmount = amount * Mathf.Max(1f, plasmaChargeDamageMultiplier);
                knockback *= 0.25f;

                ApplyPlasmaChargeHitPenalty();

                if (shaker != null)
                {
                    shaker.TriggerPlayerHitFeedback(isHeavy);
                }
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayArmorCreak(transform.position + Vector3.up * 14f, 0.95f);
                WaterSplashManager.Instance?.SpawnCombatSparks(impactPos, transform.forward, isBlocked: false, isHeavy: isHeavy, isKaijuTarget: false);
                PacificRimUI.JaegerHUD.Instance?.TriggerScreenImpact(wasBlocked: false, isHeavy: true);

                Debug.Log($"<color=#ff5533><b>[PLASMACASTER CHARGE HIT]</b></color> Took 2x DMG ({actualAmount:F1})! Charge-up extended +{plasmaChargeHitPenalty:F2}s (no stagger).");
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
                if (IsPlasmaAiming)
                {
                    CancelPlasmaCaster(immediate: false);
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

                PlayHitReactionAnimation(isHitLeft, duration: 0.58f);

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
            CancelPlasmaCaster(immediate: true);

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
                bool underwaterSilt = WaterSplashManager.Instance.IsDeepUnderwater;
                WaterSplashManager.Instance.SpawnHeavyMoveWaterKickup(
                    transform.position,
                    worldDashDir,
                    underwaterSilt ? 2.4f : 1.85f,
                    underwaterSilt);
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

        public bool TryTriggerPlasmaCaster()
        {
            bool isMeleeAttacking = Time.time < heavyAttackEndTime || Time.time < lightAttackEndTime;
            if (IsDead || IsEmoting || IsHitStaggered || IsPlasmaAiming || isBlocking || IsDashing || isMeleeAttacking)
            {
                return false;
            }

            if (!IsUltimateReady)
            {
                if (Time.unscaledTime - lastPlasmaDeniedBannerTime > 0.85f)
                {
                    lastPlasmaDeniedBannerTime = Time.unscaledTime;
                    PacificRimUI.JaegerHUD.Instance?.ShowBanner($"I-19 PLASMACASTER // ULTIMATE CHARGING ({UltimateChargePercent}%)", 1.35f);
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlayUiClick();
                }
                return false;
            }

            StartPlasmaCasterCharge();
            return true;
        }

        private void FreezeKaijuForPlasma(bool freeze)
        {
            if (cachedKaiju == null) cachedKaiju = FindAnyObjectByType<KaijuAI>();
            if (cachedKaiju != null)
            {
                cachedKaiju.SetUltimateTimeFreeze(freeze, Mathf.Max(2.5f, plasmaCurrentChargeDuration + 1.5f));
            }
        }

        private Vector3 GetLockedPlasmaTargetPos()
        {
            if (plasmaLockedOnKaiju && cachedKaiju != null && !cachedKaiju.IsDead)
            {
                return GetKaijuTorsoTarget(cachedKaiju);
            }
            return plasmaTargetPos;
        }

        private void StartPlasmaCasterCharge()
        {
            ResolveRigTransforms();
            plasmaState = PlasmaState.Charging;
            plasmaAimStartTime = Time.time;
            plasmaChargeElapsed = 0f;
            plasmaCurrentChargeDuration = Mathf.Max(2.35f, plasmaChargeDuration);
            plasmaPhaseElapsed = 0f;
            float effectiveRecoilDur = Mathf.Max(0.48f, plasmaRecoilDuration);
            float effectiveRecoverDur = Mathf.Max(0.55f, plasmaRecoverDuration);
            plasmaFireTime = Time.time + plasmaCurrentChargeDuration;
            plasmaAimEndTime = plasmaFireTime + effectiveRecoilDur + effectiveRecoverDur;
            combatStanceEndTime = Time.time + combatStancePersistDuration;

            // Temporarily hide both deployed Chainsword blades during the Plasmacaster Ultimate
            SuppressChainswordsForPlasma();

            plasmaTargetPos = ResolvePlasmaAimTarget();
            plasmaLockedOnKaiju = cachedKaiju != null && !cachedKaiju.IsDead &&
                Vector3.Distance(plasmaTargetPos, GetKaijuTorsoTarget(cachedKaiju)) < 28f;
            FreezeKaijuForPlasma(true);

            PacificRimAudio.PacificRimAudioManager.Instance?.PlayHydraulicServo(transform.position + Vector3.up * 13f, 0.95f);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayArmorCreak(transform.position + Vector3.up * 13.5f, 0.55f);
            PacificRimUI.JaegerHUD.Instance?.ShowBanner("I-19 PLASMACASTER // CAPACITORS CHARGING", 1.5f);

            if (activePlasmaCoroutine != null)
            {
                StopCoroutine(activePlasmaCoroutine);
            }
            activePlasmaCoroutine = StartCoroutine(FirePlasmaBoltRoutine());
        }

        private void ApplyPlasmaChargeHitPenalty()
        {
            if (plasmaState != PlasmaState.Charging) return;

            float penalty = Mathf.Max(0f, plasmaChargeHitPenalty);
            plasmaCurrentChargeDuration += penalty;
            plasmaFireTime += penalty;
            plasmaAimEndTime += penalty;

            if (Time.unscaledTime - lastPlasmaHitBannerTime > 0.65f)
            {
                lastPlasmaHitBannerTime = Time.unscaledTime;
                PacificRimUI.JaegerHUD.Instance?.ShowBanner($"CONTAINMENT HIT // 2X DMG & +{penalty:0.2}S CHARGE DELAY", 1.4f);
            }
        }

        private void CancelPlasmaCaster(bool immediate)
        {
            if (activePlasmaCoroutine != null)
            {
                StopCoroutine(activePlasmaCoroutine);
                activePlasmaCoroutine = null;
            }

            DestroyPlasmaChargeVfx();
            ClearPlasmaCameraTremor();
            FreezeKaijuForPlasma(false);
            plasmaState = PlasmaState.Idle;
            plasmaLockedOnKaiju = false;
            plasmaChargeElapsed = 0f;
            plasmaPhaseElapsed = 0f;
            plasmaRecoilKick = 0f;
            plasmaFireTime = -999f;
            plasmaAimEndTime = -999f;

            if (immediate)
            {
                plasmaIKWeight = 0f;
                plasmaFingerOpenWeight = 0f;
            }

            RestoreSuppressedChainswords();
        }

        private void SuppressChainswordsForPlasma()
        {
            if (activeChainswordAnimCoroutine != null)
            {
                StopCoroutine(activeChainswordAnimCoroutine);
                activeChainswordAnimCoroutine = null;
                chainswordDeployAnimEndTime = -10f;
            }

            plasmaSuppressedSwordL = false;
            plasmaSuppressedSwordR = false;

            if (!areChainswordsActive) return;

            EnsureChainswordVisuals();
            if (chainswordBladeL != null)
            {
                chainswordBladeL.transform.localPosition = swordDeployedPosL;
                chainswordBladeL.transform.localRotation = swordDeployedRotL;
                chainswordBladeL.transform.localScale = swordDeployedScaleL;
                chainswordBladeL.SetActive(false);
                plasmaSuppressedSwordL = true;
            }
            if (chainswordBladeR != null)
            {
                chainswordBladeR.transform.localPosition = swordDeployedPosR;
                chainswordBladeR.transform.localRotation = swordDeployedRotR;
                chainswordBladeR.transform.localScale = swordDeployedScaleR;
                chainswordBladeR.SetActive(false);
                plasmaSuppressedSwordR = true;
            }
        }

        private void RestoreSuppressedChainswords()
        {
            if (plasmaSuppressedSwordL)
            {
                plasmaSuppressedSwordL = false;
                if (!IsDead && areChainswordsActive && chainswordBladeL != null)
                {
                    chainswordBladeL.SetActive(true);
                }
            }
            if (plasmaSuppressedSwordR)
            {
                plasmaSuppressedSwordR = false;
                if (!IsDead && areChainswordsActive && chainswordBladeR != null)
                {
                    chainswordBladeR.SetActive(true);
                }
            }
        }

        private void ClearPlasmaCameraTremor()
        {
            Camera cam = Camera.main;
            PacificRimCameraShake shaker = cam != null ? cam.GetComponent<PacificRimCameraShake>() : null;
            if (shaker != null)
            {
                shaker.SetPlasmaChargeTremor(0f);
            }
        }

        private Vector3 GetKaijuTorsoTarget(KaijuAI kaiju)
        {
            if (kaiju == null) return transform.position + transform.forward * 65f + Vector3.up * 13.5f;

            Collider kaijuCol = kaiju.GetComponentInChildren<Collider>();
            if (kaijuCol != null && kaijuCol.enabled)
            {
                return kaijuCol.bounds.center + Vector3.up * (kaijuCol.bounds.extents.y * 0.12f);
            }
            return kaiju.transform.position + Vector3.up * (kaiju.isSlattern ? 16.5f : 13.5f);
        }

        private Vector3 ResolvePlasmaAimTarget()
        {
            if (cachedKaiju == null) cachedKaiju = FindAnyObjectByType<KaijuAI>();

            Camera cam = Camera.main;
            if (cam != null)
            {
                Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                RaycastHit[] hits = Physics.SphereCastAll(centerRay, 2.2f, 190f, ~0, QueryTriggerInteraction.Ignore);
                float bestDist = float.MaxValue;
                Vector3 bestPoint = Vector3.zero;
                bool foundKaijuHit = false;

                for (int i = 0; i < hits.Length; i++)
                {
                    Collider col = hits[i].collider;
                    if (col == null || col.transform.IsChildOf(transform)) continue;
                    var hitKaiju = col.GetComponentInParent<KaijuAI>();
                    if (hitKaiju != null && !hitKaiju.IsDead && hits[i].distance < bestDist)
                    {
                        bestDist = hits[i].distance;
                        bestPoint = hits[i].point;
                        foundKaijuHit = true;
                    }
                }

                if (foundKaijuHit)
                {
                    return bestPoint;
                }
            }

            if (cachedKaiju != null && !cachedKaiju.IsDead)
            {
                Vector3 kaijuPos = GetKaijuTorsoTarget(cachedKaiju);
                Vector3 toKaiju = kaijuPos - (chest != null ? chest.position : (transform.position + Vector3.up * 14f));
                if (toKaiju.sqrMagnitude > 0.1f && Vector3.Dot(toKaiju.normalized, transform.forward) > 0.25f)
                {
                    return kaijuPos;
                }
            }

            if (cam != null)
            {
                return cam.transform.position + cam.transform.forward * 90f;
            }
            return transform.position + transform.forward * 90f + Vector3.up * 14f;
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

        private static Mesh GetOrCreatePlasmaRingMesh()
        {
            if (cachedPlasmaRingMesh != null) return cachedPlasmaRingMesh;

            const int majorSegs = 32;
            const int minorSegs = 8;
            const float majorRadius = 1.0f;
            const float minorRadius = 0.055f;

            int vertCount = (majorSegs + 1) * (minorSegs + 1);
            Vector3[] verts = new Vector3[vertCount];
            Vector3[] norms = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] tris = new int[majorSegs * minorSegs * 6];

            int vIdx = 0;
            for (int i = 0; i <= majorSegs; i++)
            {
                float u = (float)i / majorSegs;
                float theta = u * Mathf.PI * 2f;
                Vector3 center = new Vector3(Mathf.Cos(theta) * majorRadius, Mathf.Sin(theta) * majorRadius, 0f);
                Vector3 radial = new Vector3(Mathf.Cos(theta), Mathf.Sin(theta), 0f);

                for (int j = 0; j <= minorSegs; j++)
                {
                    float v = (float)j / minorSegs;
                    float phi = v * Mathf.PI * 2f;
                    Vector3 offset = radial * (Mathf.Cos(phi) * minorRadius) + Vector3.forward * (Mathf.Sin(phi) * minorRadius);
                    verts[vIdx] = center + offset;
                    norms[vIdx] = offset.normalized;
                    uvs[vIdx] = new Vector2(u, v);
                    vIdx++;
                }
            }

            int tIdx = 0;
            int stride = minorSegs + 1;
            for (int i = 0; i < majorSegs; i++)
            {
                for (int j = 0; j < minorSegs; j++)
                {
                    int a = i * stride + j;
                    int b = (i + 1) * stride + j;
                    int c = (i + 1) * stride + (j + 1);
                    int d = i * stride + (j + 1);
                    tris[tIdx++] = a; tris[tIdx++] = b; tris[tIdx++] = c;
                    tris[tIdx++] = a; tris[tIdx++] = c; tris[tIdx++] = d;
                }
            }

            cachedPlasmaRingMesh = new Mesh { name = "ProceduralPlasmaRing" };
            cachedPlasmaRingMesh.vertices = verts;
            cachedPlasmaRingMesh.normals = norms;
            cachedPlasmaRingMesh.uv = uvs;
            cachedPlasmaRingMesh.triangles = tris;
            return cachedPlasmaRingMesh;
        }

        private GameObject CreatePlasmaRingObject(string objName, Transform parent, Color baseCol, Color emission)
        {
            GameObject ring = new GameObject(objName);
            ring.transform.SetParent(parent, false);
            var mf = ring.AddComponent<MeshFilter>();
            mf.sharedMesh = GetOrCreatePlasmaRingMesh();
            var mr = ring.AddComponent<MeshRenderer>();
            mr.sharedMaterial = CreatePlasmaMaterial(baseCol, emission, transparent: true);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return ring;
        }

        private void CreatePlasmaChargeVfx()
        {
            DestroyPlasmaChargeVfx();

            activePlasmaChargeRoot = new GameObject("PlasmaMuzzleCharge");
            activePlasmaChargeRoot.transform.localScale = Vector3.one;

            GameObject chargeCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            chargeCore.name = "Core";
            var coreCol = chargeCore.GetComponent<Collider>();
            if (coreCol != null) Destroy(coreCol);
            chargeCore.transform.SetParent(activePlasmaChargeRoot.transform, false);
            var coreRend = chargeCore.GetComponent<MeshRenderer>();
            if (coreRend != null)
            {
                coreRend.sharedMaterial = CreatePlasmaMaterial(new Color(0.94f, 0.99f, 1f, 1f), new Color(0.68f, 0.96f, 1f, 1f) * 6.5f);
                coreRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            plasmaCoreTransform = chargeCore.transform;

            GameObject chargeCorona = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            chargeCorona.name = "Corona";
            var coronaCol = chargeCorona.GetComponent<Collider>();
            if (coronaCol != null) Destroy(coronaCol);
            chargeCorona.transform.SetParent(activePlasmaChargeRoot.transform, false);
            var coronaRend = chargeCorona.GetComponent<MeshRenderer>();
            if (coronaRend != null)
            {
                coronaRend.sharedMaterial = CreatePlasmaMaterial(new Color(0.14f, 0.84f, 1f, 0.34f), new Color(0.12f, 0.80f, 1f, 1f) * 3.4f, transparent: true);
                coronaRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            plasmaCoronaTransform = chargeCorona.transform;

            GameObject ringA = CreatePlasmaRingObject("ContainmentRingA", activePlasmaChargeRoot.transform,
                new Color(0.35f, 0.92f, 1f, 0.65f), new Color(0.30f, 0.90f, 1f, 1f) * 4.8f);
            plasmaRingATransform = ringA.transform;

            GameObject ringB = CreatePlasmaRingObject("ContainmentRingB", activePlasmaChargeRoot.transform,
                new Color(0.65f, 0.97f, 1f, 0.55f), new Color(0.45f, 0.94f, 1f, 1f) * 5.2f);
            plasmaRingBTransform = ringB.transform;

            activePlasmaChargeLight = activePlasmaChargeRoot.AddComponent<Light>();
            activePlasmaChargeLight.type = LightType.Point;
            activePlasmaChargeLight.color = new Color(0.22f, 0.90f, 1f, 1f);
            activePlasmaChargeLight.range = 38f;
            activePlasmaChargeLight.intensity = 14f;
        }

        private void DestroyPlasmaChargeVfx()
        {
            if (activePlasmaChargeRoot != null)
            {
                Destroy(activePlasmaChargeRoot);
                activePlasmaChargeRoot = null;
            }
            plasmaCoreTransform = null;
            plasmaCoronaTransform = null;
            plasmaRingATransform = null;
            plasmaRingBTransform = null;
            activePlasmaChargeLight = null;
        }

        private Vector3 ComputeSolvedBarrelMuzzlePos()
        {
            if (handR == null)
            {
                return transform.position + transform.forward * 5f + Vector3.up * 15f;
            }

            float palmLen = middleR1 != null
                ? Vector3.Distance(handR.position, middleR1.position)
                : (forearmR != null ? Vector3.Distance(forearmR.position, handR.position) * 0.45f : 1.5f);
            // Position centered in the open I-19 palm/barrel aperture framed by the splayed fingers
            return handR.position + handR.up * (palmLen * 0.55f) + handR.right * (palmLen * 0.16f);
        }

        private void UpdatePlasmaChargeVfxTransform()
        {
            if (activePlasmaChargeRoot == null || plasmaState != PlasmaState.Charging) return;

            float u = PlasmaChargeProgress;
            Vector3 muzzleCenter = plasmaSolvedBarrelPos.sqrMagnitude > 0.01f ? plasmaSolvedBarrelPos : ComputeSolvedBarrelMuzzlePos();
            Vector3 aimDir = plasmaSolvedAimDir.sqrMagnitude > 0.01f ? plasmaSolvedAimDir : transform.forward;

            activePlasmaChargeRoot.transform.position = muzzleCenter;
            activePlasmaChargeRoot.transform.rotation = Quaternion.LookRotation(aimDir, Vector3.up);

            // Core grows during buildup, then briefly compresses at peak saturation (u > 0.86) before detonation
            float pulse = 1f + 0.14f * Mathf.Sin(plasmaChargeElapsed * Mathf.Lerp(26f, 64f, u));
            float coreScale;
            if (u < 0.86f)
            {
                float build = u / 0.86f;
                coreScale = Mathf.Lerp(0.14f, 1.32f, build * build) * pulse;
            }
            else
            {
                float compress = (u - 0.86f) / 0.14f;
                coreScale = Mathf.Lerp(1.32f, 0.82f, compress * compress) * (1f + 0.06f * Mathf.Sin(plasmaChargeElapsed * 80f));
            }
            if (plasmaCoreTransform != null)
            {
                plasmaCoreTransform.localScale = Vector3.one * coreScale;
            }

            // Outer containment corona pulls INWARD toward the palm emitter as energy condenses
            if (plasmaCoronaTransform != null)
            {
                float coronaScale = u < 0.18f
                    ? Mathf.Lerp(0.3f, 3.8f, u / 0.18f)
                    : Mathf.Lerp(3.8f, 1.35f, Mathf.SmoothStep(0f, 1f, (u - 0.18f) / 0.82f));
                plasmaCoronaTransform.localScale = Vector3.one * (coronaScale * (1f + 0.08f * Mathf.Cos(plasmaChargeElapsed * 38f)));
            }

            // Dual counter-spinning plasma containment rings tighten around the barrel mouth
            float spinRate = Mathf.Lerp(180f, 920f, u);
            float ringRadius = Mathf.Lerp(2.35f, 0.92f, Mathf.SmoothStep(0f, 1f, u));
            if (plasmaRingATransform != null)
            {
                plasmaRingATransform.localScale = Vector3.one * ringRadius;
                plasmaRingATransform.localRotation = Quaternion.Euler(22f * Mathf.Sin(plasmaChargeElapsed * 5f), 0f, plasmaChargeElapsed * spinRate);
            }
            if (plasmaRingBTransform != null)
            {
                plasmaRingBTransform.localScale = Vector3.one * (ringRadius * 0.78f);
                plasmaRingBTransform.localRotation = Quaternion.Euler(70f, 25f, -plasmaChargeElapsed * spinRate * 1.25f);
            }

            if (activePlasmaChargeLight != null)
            {
                activePlasmaChargeLight.intensity = u < 0.86f
                    ? Mathf.Lerp(14f, 460f, u * u)
                    : Mathf.Lerp(460f, 720f, (u - 0.86f) / 0.14f);
            }
        }

        private System.Collections.IEnumerator FirePlasmaBoltRoutine()
        {
            CreatePlasmaChargeVfx();

            bool empAudioStarted = false;
            bool spark1 = false, spark2 = false, spark3 = false;
            bool preSteam = false, lockCue = false;

            while (plasmaChargeElapsed < plasmaCurrentChargeDuration)
            {
                if (this == null || IsDead || plasmaState != PlasmaState.Charging)
                {
                    DestroyPlasmaChargeVfx();
                    ClearPlasmaCameraTremor();
                    FreezeKaijuForPlasma(false);
                    yield break;
                }

                plasmaChargeElapsed += Time.deltaTime;
                plasmaFireTime = plasmaAimStartTime + plasmaCurrentChargeDuration;
                plasmaAimEndTime = plasmaFireTime + Mathf.Max(0.2f, plasmaRecoilDuration) + Mathf.Max(0.2f, plasmaRecoverDuration);

                float u = PlasmaChargeProgress;

                if (!empAudioStarted && u >= 0.12f)
                {
                    empAudioStarted = true;
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlayEmpChargeUp(transform.position + Vector3.up * 15f);
                }

                Vector3 sparkPos = activePlasmaChargeRoot != null ? activePlasmaChargeRoot.transform.position : ComputeSolvedBarrelMuzzlePos();
                Vector3 aimDir = plasmaSolvedAimDir.sqrMagnitude > 0.01f ? plasmaSolvedAimDir : transform.forward;

                if (!spark1 && u >= 0.40f)
                {
                    spark1 = true;
                    WaterSplashManager.Instance?.SpawnCombatSparks(sparkPos, aimDir, isBlocked: false, isHeavy: false, isKaijuTarget: false);
                }
                if (!preSteam && u >= 0.65f)
                {
                    preSteam = true;
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlaySteamVent(transform.position + Vector3.up * 14f);
                }
                if (!spark2 && u >= 0.70f)
                {
                    spark2 = true;
                    WaterSplashManager.Instance?.SpawnCombatSparks(sparkPos, aimDir, isBlocked: false, isHeavy: false, isKaijuTarget: false);
                }
                if (!lockCue && u >= 0.86f)
                {
                    lockCue = true;
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlayUiClick();
                }
                if (!spark3 && u >= 0.90f)
                {
                    spark3 = true;
                    WaterSplashManager.Instance?.SpawnCombatSparks(sparkPos, aimDir, isBlocked: false, isHeavy: true, isKaijuTarget: false);
                }

                Camera cam = Camera.main;
                PacificRimCameraShake shaker = cam != null ? cam.GetComponent<PacificRimCameraShake>() : null;
                if (shaker != null)
                {
                    shaker.SetPlasmaChargeTremor(Mathf.InverseLerp(0.22f, 1.0f, u));
                }

                yield return null;
            }

            DestroyPlasmaChargeVfx();
            ClearPlasmaCameraTremor();
            if (this == null || IsDead)
            {
                FreezeKaijuForPlasma(false);
                plasmaState = PlasmaState.Idle;
                RestoreSuppressedChainswords();
                yield break;
            }

            // --- Stage 2: Discharge (Expend 100% Ultimate Charge ON SHOT) ---
            ResolveHandTransforms();
            plasmaTargetPos = GetLockedPlasmaTargetPos();
            Vector3 muzzleBase = ComputeSolvedBarrelMuzzlePos();
            Vector3 dir = (plasmaTargetPos - muzzleBase);
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            dir.Normalize();
            Vector3 muzzlePos = muzzleBase + dir * 0.9f;

            // Unfreeze the Kaiju right as the bolt launches so it visibly takes impact & staggers
            FreezeKaijuForPlasma(false);

            // Reset Ultimate Charge to flat 0 at the moment of discharge
            currentUltimateCharge = 0f;
            lastPlasmaTime = Time.time;

            plasmaState = PlasmaState.FiringRecoil;
            plasmaPhaseElapsed = 0f;
            plasmaRecoilKick = 1f;
            plasmaFireTime = Time.time;
            plasmaAimEndTime = Time.time + Mathf.Max(0.2f, plasmaRecoilDuration) + Mathf.Max(0.2f, plasmaRecoverDuration);

            PacificRimAudio.PacificRimAudioManager.Instance?.PlayEmpDischargeBlast(muzzlePos);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayAttackWhoosh(muzzlePos, isHeavy: true);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayLightningStrike(muzzlePos, volume: 0.72f);
            PacificRimUI.JaegerHUD.Instance?.ShowBanner("I-19 PLASMACASTER // DISCHARGED", 1.25f);

            if (rb != null) rb.AddForce(-dir * 24f, ForceMode.Impulse);

            Camera mainCam = Camera.main;
            PacificRimCameraShake dischargeShaker = mainCam != null ? mainCam.GetComponent<PacificRimCameraShake>() : null;
            if (dischargeShaker != null)
            {
                dischargeShaker.TriggerPlasmaDischargeFeedback();
            }
            else
            {
                TriggerAttackCameraFeedback(4, isHeavy: true, didHit: true);
            }

            WaterSplashManager.Instance?.SpawnHeavyMoveWaterKickup(new Vector3(muzzlePos.x, transform.position.y, muzzlePos.z), dir, 2.25f, true);
            WaterSplashManager.Instance?.SpawnParrySparks(muzzlePos, dir);
            StartCoroutine(SpawnPlasmaMuzzleBlastRoutine(muzzlePos, dir));
            LaunchPlasmaProjectile(muzzlePos, dir);

            // --- Stage 3: Recoil & Hydraulic Recovery ---
            float recoilDur = Mathf.Max(0.48f, plasmaRecoilDuration);
            bool postSteam = false;
            while (plasmaPhaseElapsed < recoilDur && plasmaState == PlasmaState.FiringRecoil)
            {
                if (this == null || IsDead) yield break;
                plasmaPhaseElapsed += Time.deltaTime;
                if (!postSteam && plasmaPhaseElapsed >= 0.16f)
                {
                    postSteam = true;
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlaySteamVent(transform.position + Vector3.up * 14f);
                }
                yield return null;
            }

            if (plasmaState == PlasmaState.FiringRecoil)
            {
                plasmaState = PlasmaState.Recovering;
                plasmaPhaseElapsed = 0f;
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayHydraulicServo(transform.position + Vector3.up * 13f, 0.75f);

                float recoverDur = Mathf.Max(0.55f, plasmaRecoverDuration);
                while (plasmaPhaseElapsed < recoverDur && plasmaState == PlasmaState.Recovering)
                {
                    if (this == null || IsDead) yield break;
                    plasmaPhaseElapsed += Time.deltaTime;
                    yield return null;
                }
            }

            if (plasmaState == PlasmaState.Recovering)
            {
                plasmaState = PlasmaState.Idle;
                RestoreSuppressedChainswords();
            }
            activePlasmaCoroutine = null;
        }

        private void LaunchPlasmaProjectile(Vector3 muzzlePos, Vector3 initialDir)
        {
            GameObject bolt = new GameObject("PlasmaCasterBolt");
            bolt.transform.position = muzzlePos;
            bolt.transform.rotation = Quaternion.LookRotation(initialDir, Vector3.up);

            GameObject boltCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            boltCore.name = "PlasmaCore";
            var boltCoreCol = boltCore.GetComponent<Collider>();
            if (boltCoreCol != null) Destroy(boltCoreCol);
            boltCore.transform.SetParent(bolt.transform, false);
            boltCore.transform.localScale = new Vector3(1.35f, 1.35f, 2.9f);
            var boltCoreRend = boltCore.GetComponent<MeshRenderer>();
            if (boltCoreRend != null)
            {
                boltCoreRend.sharedMaterial = CreatePlasmaMaterial(new Color(0.92f, 0.99f, 1f, 1f), new Color(0.75f, 0.97f, 1f, 1f) * 7.5f);
                boltCoreRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            GameObject boltShell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            boltShell.name = "PlasmaShell";
            var boltShellCol = boltShell.GetComponent<Collider>();
            if (boltShellCol != null) Destroy(boltShellCol);
            boltShell.transform.SetParent(bolt.transform, false);
            boltShell.transform.localScale = new Vector3(2.8f, 2.8f, 4.4f);
            var boltShellRend = boltShell.GetComponent<MeshRenderer>();
            if (boltShellRend != null)
            {
                boltShellRend.sharedMaterial = CreatePlasmaMaterial(new Color(0.14f, 0.76f, 1.0f, 0.45f), new Color(0.10f, 0.72f, 1.0f, 1f) * 4.2f, transparent: true);
                boltShellRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var light = bolt.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.22f, 0.88f, 1.0f, 1f);
            light.range = 55f;
            light.intensity = 450f;

            var trail = bolt.AddComponent<TrailRenderer>();
            Shader trailShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (trailShader != null)
            {
                trail.sharedMaterial = new Material(trailShader);
            }
            trail.time = 0.34f;
            trail.startWidth = 2.6f;
            trail.endWidth = 0.08f;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.90f, 0.99f, 1f), 0f),
                    new GradientColorKey(new Color(0.18f, 0.78f, 1f), 0.45f),
                    new GradientColorKey(new Color(0.04f, 0.36f, 0.90f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.98f, 0f),
                    new GradientAlphaKey(0.65f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = grad;

            StartCoroutine(TrackPlasmaBoltRoutine(bolt, boltShell.transform, initialDir));
        }

        private System.Collections.IEnumerator TrackPlasmaBoltRoutine(GameObject bolt, Transform boltShell, Vector3 dir)
        {
            float travelElapsed = 0f;
            float wakeTimer = 0f;
            bool impacted = false;
            Collider[] kaijuCols = (cachedKaiju != null && !cachedKaiju.IsDead)
                ? cachedKaiju.GetComponentsInChildren<Collider>()
                : null;

            while (travelElapsed < 2.0f && bolt != null)
            {
                if (cachedKaiju != null && !cachedKaiju.IsDead)
                {
                    Vector3 targetCenter = GetKaijuTorsoTarget(cachedKaiju);
                    Vector3 toKaiju = (targetCenter - bolt.transform.position).normalized;
                    dir = Vector3.RotateTowards(dir, toKaiju, 1.35f * Time.deltaTime, 0f);
                }

                float step = plasmaBoltSpeed * Time.deltaTime;
                Vector3 prevPos = bolt.transform.position;
                Vector3 nextPos = prevPos + dir * step;
                bolt.transform.position = nextPos;
                bolt.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                travelElapsed += Time.deltaTime;

                if (boltShell != null)
                {
                    float shellPulse = 1f + 0.16f * Mathf.Sin(travelElapsed * 42f);
                    boltShell.localScale = new Vector3(2.8f * shellPulse, 2.8f * shellPulse, 4.4f);
                }

                wakeTimer += Time.deltaTime;
                if (wakeTimer >= 0.05f)
                {
                    wakeTimer = 0f;
                    if (WaterSystem.Instance != null)
                    {
                        float wy = WaterSystem.Instance.GetWaterHeight(nextPos);
                        WaterSplashManager.Instance?.SpawnHeavyMoveWaterKickup(new Vector3(nextPos.x, wy, nextPos.z), dir, 1.35f, false);
                    }
                }

                if (cachedKaiju != null && !cachedKaiju.IsDead)
                {
                    if (Physics.SphereCast(prevPos, 2.4f, dir, out RaycastHit hit, step + 1.2f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) &&
                            hit.collider.GetComponentInParent<KaijuAI>() == cachedKaiju)
                        {
                            bolt.transform.position = hit.point;
                            impacted = true;
                            break;
                        }
                    }

                    if (kaijuCols != null)
                    {
                        for (int i = 0; i < kaijuCols.Length; i++)
                        {
                            var kc = kaijuCols[i];
                            if (kc == null || !kc.enabled || kc.isTrigger) continue;
                            if (Vector3.Distance(nextPos, kc.ClosestPoint(nextPos)) <= 3.0f)
                            {
                                impacted = true;
                                break;
                            }
                        }
                        if (impacted) break;
                    }

                    float fallbackDist = Vector3.Distance(nextPos, GetKaijuTorsoTarget(cachedKaiju));
                    if (fallbackDist <= 9.5f)
                    {
                        impacted = true;
                        break;
                    }
                }
                yield return null;
            }

            if (bolt != null)
            {
                Vector3 hitPos = bolt.transform.position;
                Destroy(bolt);

                if (impacted && cachedKaiju != null && !cachedKaiju.IsDead)
                {
                    float dmg = IsPoweredUp ? plasmaDamage * 1.25f : plasmaDamage;
                    if (IsIronmanCheatActive) dmg *= 3f;
                    cachedKaiju.TakeDamage(dmg, dir * 46f, attackVariant: 4, hitPoint: hitPos, stunMultiplier: 1.35f, unblockable: true);
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlayLightningStrike(hitPos);
                    PacificRimAudio.PacificRimAudioManager.Instance?.PlayJaegerPunchHit(hitPos, isHeavy: true);
                    WaterSplashManager.Instance?.SpawnParrySparks(hitPos, -dir);
                    WaterSplashManager.Instance?.SpawnCombatSparks(hitPos, -dir, isBlocked: false, isHeavy: true, isKaijuTarget: true);
                    WaterSplashManager.Instance?.SpawnHeavyMoveWaterKickup(hitPos, dir, 3.2f, true);
                    TriggerAttackCameraFeedback(4, isHeavy: true, didHit: true);
                }

                StartCoroutine(SpawnPlasmaDetonationRoutine(hitPos));
            }
        }

        private System.Collections.IEnumerator SpawnPlasmaMuzzleBlastRoutine(Vector3 muzzlePos, Vector3 dir)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "PlasmaMuzzleFlash";
            var col = flash.GetComponent<Collider>();
            if (col != null) Destroy(col);
            flash.transform.position = muzzlePos;
            flash.transform.localScale = Vector3.one * 2.0f;
            Material flashMat = CreatePlasmaMaterial(new Color(0.75f, 0.98f, 1f, 0.78f), new Color(0.55f, 0.95f, 1f, 1f) * 7f, transparent: true);
            var rend = flash.GetComponent<MeshRenderer>();
            if (rend != null && flashMat != null)
            {
                rend.sharedMaterial = flashMat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            GameObject ring = CreatePlasmaRingObject("PlasmaMuzzleShockwave", null,
                new Color(0.45f, 0.95f, 1f, 0.85f), new Color(0.35f, 0.92f, 1f, 1f) * 6f);
            ring.transform.position = muzzlePos + dir * 0.4f;
            ring.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            ring.transform.localScale = Vector3.one * 1.1f;
            var ringRend = ring.GetComponent<MeshRenderer>();
            Material ringMat = ringRend != null ? ringRend.sharedMaterial : null;

            float dur = 0.22f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float ease = 1f - Mathf.Pow(1f - t, 2f);

                if (flash != null)
                {
                    flash.transform.localScale = Vector3.one * Mathf.Lerp(2.0f, 6.2f, ease);
                    if (flashMat != null)
                    {
                        Color c = new Color(0.75f, 0.98f, 1f, Mathf.Lerp(0.78f, 0f, t));
                        flashMat.color = c;
                        if (flashMat.HasProperty("_BaseColor")) flashMat.SetColor("_BaseColor", c);
                    }
                }
                if (ring != null)
                {
                    ring.transform.position = muzzlePos + dir * Mathf.Lerp(0.4f, 4.5f, ease);
                    ring.transform.localScale = Vector3.one * Mathf.Lerp(1.1f, 6.8f, ease);
                    if (ringMat != null)
                    {
                        Color rc = new Color(0.45f, 0.95f, 1f, Mathf.Lerp(0.85f, 0f, t));
                        ringMat.color = rc;
                        if (ringMat.HasProperty("_BaseColor")) ringMat.SetColor("_BaseColor", rc);
                    }
                }
                yield return null;
            }

            if (flash != null) Destroy(flash);
            if (ring != null) Destroy(ring);
        }

        private System.Collections.IEnumerator SpawnPlasmaDetonationRoutine(Vector3 hitPos)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "PlasmaDetonationFlash";
            var col = flash.GetComponent<Collider>();
            if (col != null) Destroy(col);
            flash.transform.position = hitPos;
            flash.transform.localScale = Vector3.one * 3.5f;

            Material flashMat = CreatePlasmaMaterial(new Color(0.65f, 0.96f, 1f, 0.78f), new Color(0.50f, 0.94f, 1f, 1f) * 6.8f, transparent: true);
            var rend = flash.GetComponent<MeshRenderer>();
            if (rend != null && flashMat != null)
            {
                rend.sharedMaterial = flashMat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Light flashLight = flash.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = new Color(0.35f, 0.92f, 1f, 1f);
            flashLight.range = 75f;
            flashLight.intensity = 720f;

            float duration = 0.30f;
            float elapsed = 0f;
            while (elapsed < duration && flash != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(3.5f, 19f, 1f - Mathf.Pow(1f - t, 2f));
                flash.transform.localScale = Vector3.one * scale;
                flashLight.intensity = Mathf.Lerp(720f, 0f, t);

                if (flashMat != null)
                {
                    Color c = new Color(0.65f, 0.96f, 1f, Mathf.Lerp(0.78f, 0f, t));
                    flashMat.color = c;
                    if (flashMat.HasProperty("_BaseColor")) flashMat.SetColor("_BaseColor", c);
                }
                yield return null;
            }

            if (flash != null) Destroy(flash);
        }

        private static void BlendFingerChain(Transform b1, Transform b2, Transform b3, Quaternion r1, Quaternion r2, Quaternion r3, float weight)
        {
            if (weight <= 0.001f) return;
            if (b1 != null) b1.localRotation = Quaternion.Slerp(b1.localRotation, r1, weight);
            if (b2 != null) b2.localRotation = Quaternion.Slerp(b2.localRotation, r2, weight);
            if (b3 != null) b3.localRotation = Quaternion.Slerp(b3.localRotation, r3, weight);
        }

        private void ApplyPlasmaRightFingers(float openWeight, float blastKick, float chargeU)
        {
            if (openWeight <= 0.001f) return;

            float splay = Mathf.Clamp(plasmaFingerSplayAngle, 15f, 65f) * 0.55f + blastKick * 9f;
            float tremor = chargeU > 0.45f
                ? Mathf.Sin(Time.time * 68f) * 3.5f * Mathf.InverseLerp(0.45f, 1f, chargeU)
                : 0f;

            // Hand.R fingers curl inward with -Z; +Z local rotation opens/flares them radially around the palm aperture
            Quaternion phal2 = Quaternion.Euler(0f, 0f, 12f + blastKick * 6f);
            Quaternion phal3 = Quaternion.Euler(0f, 0f, 7f + blastKick * 4f);

            BlendFingerChain(indexR1, indexR2, indexR3,
                Quaternion.Euler(16f, 0f, splay + tremor), phal2, phal3, openWeight);
            BlendFingerChain(middleR1, middleR2, middleR3,
                Quaternion.Euler(5f, 0f, splay + 2f - tremor * 0.8f), phal2, phal3, openWeight);
            BlendFingerChain(ringR1, ringR2, ringR3,
                Quaternion.Euler(-6f, 0f, splay + 2f + tremor * 0.7f), phal2, phal3, openWeight);
            BlendFingerChain(pinkyR1, pinkyR2, pinkyR3,
                Quaternion.Euler(-17f, 0f, splay - tremor), phal2, phal3, openWeight);
            BlendFingerChain(thumbR1, thumbR2, null,
                Quaternion.Euler(36f, 14f, splay * 0.85f + tremor), Quaternion.Euler(0f, 0f, 12f), Quaternion.identity, openWeight);
        }

        private void ApplyPlasmaLeftBraceFingers(float braceWeight)
        {
            if (braceWeight <= 0.001f) return;

            // Hand.L curls inward with +Z; negative Z uncurls into an open forearm support cradle
            float w = braceWeight * 0.62f;
            Quaternion midL = Quaternion.Euler(0f, 0f, -14f);
            Quaternion tipL = Quaternion.Euler(0f, 0f, -10f);

            BlendFingerChain(indexL1, indexL2, indexL3, Quaternion.Euler(10f, 0f, -22f), midL, tipL, w);
            BlendFingerChain(middleL1, middleL2, middleL3, Quaternion.Euler(3f, 0f, -24f), midL, tipL, w);
            BlendFingerChain(ringL1, ringL2, ringL3, Quaternion.Euler(-4f, 0f, -22f), midL, tipL, w);
            BlendFingerChain(pinkyL1, pinkyL2, pinkyL3, Quaternion.Euler(-11f, 0f, -18f), midL, tipL, w);
            BlendFingerChain(thumbL1, thumbL2, null, Quaternion.Euler(36f, -14f, -20f), Quaternion.Euler(0f, 0f, -10f), Quaternion.identity, w);
        }

        /// <summary>
        /// Multi-phase I-19 Plasmacaster IK:
        /// - Charging: chambers right arm then extends into locked cannon barrel aim; Hand.R aligns +Y downrange
        ///   and splays all 5 fingers radially open; Hand.L cups/braces underneath Forearm.R.
        /// - FiringRecoil / Recovering: heavy arm compression, upward muzzle kick, torso rock, then fingers close back to fist.
        /// </summary>
        private void ApplyCombatArmsIK(float dt)
        {
            bool wantAim = IsPlasmaAiming && !IsEmoting && !IsDead;
            plasmaIKWeight = Mathf.MoveTowards(plasmaIKWeight, wantAim ? 1f : 0f, (wantAim ? 7.5f : 4.5f) * dt);

            float chargeU = PlasmaChargeProgress;
            bool wantFingersOpen = (plasmaState == PlasmaState.Charging && chargeU >= 0.06f) || plasmaState == PlasmaState.FiringRecoil;
            float fingerRate = wantFingersOpen ? 4.8f : 5.5f;
            plasmaFingerOpenWeight = Mathf.MoveTowards(plasmaFingerOpenWeight, wantFingersOpen ? 1f : 0f, fingerRate * dt);

            if (plasmaState == PlasmaState.FiringRecoil || plasmaState == PlasmaState.Recovering)
            {
                plasmaRecoilKick = Mathf.MoveTowards(plasmaRecoilKick, 0f, 3.2f * dt);
            }
            else
            {
                plasmaRecoilKick = 0f;
            }

            if (plasmaIKWeight > 0.001f && upperArmR != null && forearmR != null && handR != null)
            {
                if (plasmaState == PlasmaState.Charging)
                {
                    plasmaTargetPos = GetLockedPlasmaTargetPos();
                }

                float w = Mathf.SmoothStep(0f, 1f, plasmaIKWeight);
                float recoil = plasmaRecoilKick * plasmaRecoilKick;

                // 1. Torso twist leading with right shoulder + subtle charge lean + backward blast rock
                Vector3 chestOrigin = chest != null ? chest.position : (transform.position + Vector3.up * 14f);
                Vector3 toTarget = plasmaTargetPos - chestOrigin;
                Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
                float rawYaw = flat.sqrMagnitude > 0.01f ? Vector3.SignedAngle(transform.forward, flat.normalized, Vector3.up) : 0f;
                float clampedYaw = Mathf.Clamp(rawYaw, -55f, 55f);
                float twist = Mathf.Clamp(clampedYaw - 24f, -58f, 35f) * w - recoil * 11f;

                float rawPitch = flat.sqrMagnitude > 0.01f
                    ? Mathf.Clamp(-Mathf.Atan2(toTarget.y, flat.magnitude) * Mathf.Rad2Deg, -26f, 22f)
                    : 0f;
                float chargeLeanFwd = plasmaState == PlasmaState.Charging ? (5.5f * Mathf.SmoothStep(0f, 1f, chargeU)) : 0f;
                float pitch = (rawPitch * 0.45f + chargeLeanFwd) * w - recoil * 13f;

                if (spine1 != null)
                {
                    spine1.rotation = Quaternion.AngleAxis(twist * 0.52f, Vector3.up)
                                    * Quaternion.AngleAxis(pitch * 0.35f, transform.right)
                                    * spine1.rotation;
                }
                if (chest != null)
                {
                    chest.rotation = Quaternion.AngleAxis(twist * 0.48f, Vector3.up)
                                   * Quaternion.AngleAxis(pitch * 0.65f, transform.right)
                                   * chest.rotation;
                }

                // 2. Right arm trajectory: chamber draw (0..0.28) -> locked cannon extension (0.28..1) -> recoil kick
                Vector3 shoulder = upperArmR.position;
                Vector3 rawAimDir = (plasmaTargetPos - shoulder);
                if (rawAimDir.sqrMagnitude < 0.01f) rawAimDir = transform.forward;
                rawAimDir.Normalize();

                // Clamp aim vector within realistic forward torso cone so shoulder never twists backward
                Vector3 aimDir = Vector3.RotateTowards(transform.forward, rawAimDir, 62f * Mathf.Deg2Rad, 0f);
                plasmaSolvedAimDir = aimDir;

                float l1 = Vector3.Distance(upperArmR.position, forearmR.position);
                float l2 = Vector3.Distance(forearmR.position, handR.position);
                float reach = l1 + l2;

                float deployT = plasmaState == PlasmaState.Charging
                    ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(chargeU / 0.24f))
                    : 1f;

                Vector3 chamberWristPos = shoulder
                    + transform.forward * (reach * 0.54f)
                    + transform.right * (reach * 0.14f)
                    - Vector3.up * (reach * 0.08f);

                float aimExtension = Mathf.Lerp(0.93f, 0.65f, recoil);
                Vector3 recoilLift = Vector3.up * (reach * 0.19f * recoil) - aimDir * (reach * 0.08f * recoil);

                Vector3 chargeTremor = Vector3.zero;
                if (plasmaState == PlasmaState.Charging && chargeU > 0.30f)
                {
                    float tAmt = Mathf.InverseLerp(0.30f, 1.0f, chargeU);
                    chargeTremor = (transform.right * Mathf.Sin(Time.time * 58f) + Vector3.up * Mathf.Cos(Time.time * 51f))
                                 * (0.13f * tAmt);
                }

                Vector3 extendedWristPos = shoulder + aimDir * (reach * aimExtension) + recoilLift + chargeTremor;
                Vector3 targetWristPos = Vector3.Lerp(chamberWristPos, extendedWristPos, deployT);
                Vector3 blendedTarget = Vector3.Lerp(handR.position, targetWristPos, w);

                // Elbow drops down and out while chambering, then tucks into stable firing line
                Vector3 chamberPole = (-Vector3.up * 0.85f + transform.right * 0.75f - transform.forward * 0.30f).normalized;
                Vector3 aimPole = (-Vector3.up * 1.0f + transform.right * 0.35f - aimDir * 0.25f).normalized;
                Vector3 elbowPole = Vector3.Slerp(chamberPole, aimPole, deployT);

                SolveTwoBoneIKWorld(upperArmR, forearmR, handR, blendedTarget, elbowPole, keepTipLocalRotation: false, animHintWeight: 0.10f);

                // Explicit Hand.R wrist orientation: align distal +Y downrange along barrel, palm +X facing inward-up toward camera/chest
                Vector3 forearmDir = (handR.position - forearmR.position).sqrMagnitude > 0.0001f
                    ? (handR.position - forearmR.position).normalized
                    : aimDir;
                Vector3 barrelUp = Vector3.Slerp(forearmDir, aimDir, 0.55f * deployT).normalized;
                Vector3 palmBase = (-transform.right * 0.82f + Vector3.up * 0.57f).normalized;
                Vector3 palmHint = (Quaternion.AngleAxis(plasmaBarrelPalmTilt, barrelUp) * palmBase).normalized;
                Vector3 radialZ = Vector3.Cross(palmHint, barrelUp);
                if (radialZ.sqrMagnitude > 0.0001f)
                {
                    Quaternion barrelRot = Quaternion.LookRotation(radialZ.normalized, barrelUp);
                    handR.rotation = Quaternion.Slerp(handR.rotation, barrelRot, w * Mathf.Lerp(0.55f, 1.0f, deployT));
                }

                // Drive all 5 right-hand fingers into the splayed I-19 Plasmacaster barrel aperture
                ApplyPlasmaRightFingers(w * plasmaFingerOpenWeight, recoil, chargeU);

                // 3. Left arm & hand reach across to brace underneath Forearm.R
                if (upperArmL != null && forearmL != null && handL != null)
                {
                    Vector3 forearmBraceSocket = Vector3.Lerp(forearmR.position, handR.position, 0.52f)
                        - Vector3.up * (reach * 0.09f)
                        - transform.right * (reach * 0.04f);
                    float braceBlend = w * Mathf.Lerp(0.55f, 0.92f, deployT) * Mathf.Lerp(1f, 0.35f, plasmaState == PlasmaState.Recovering ? 1f : 0f);
                    Vector3 blendedBraceL = Vector3.Lerp(handL.position, forearmBraceSocket, braceBlend);
                    Vector3 poleL = (-Vector3.up * 0.95f - transform.right * 0.55f - transform.forward * 0.25f).normalized;

                    SolveTwoBoneIKWorld(upperArmL, forearmL, handL, blendedBraceL, poleL, keepTipLocalRotation: false, animHintWeight: 0.12f);

                    // Orient Hand.L so palm (-X) faces up against the underside of Forearm.R
                    Vector3 distalL = (transform.right * 0.72f + aimDir * 0.68f).normalized;
                    Vector3 worldXHintL = -Vector3.up; // palm (-X) -> +Vector3.up
                    Vector3 radialZL = Vector3.Cross(worldXHintL, distalL);
                    if (radialZL.sqrMagnitude > 0.0001f)
                    {
                        Quaternion cradleRotL = Quaternion.LookRotation(radialZL.normalized, distalL);
                        handL.rotation = Quaternion.Slerp(handL.rotation, cradleRotL, braceBlend);
                    }
                    ApplyPlasmaLeftBraceFingers(braceBlend);
                }

                plasmaSolvedBarrelPos = ComputeSolvedBarrelMuzzlePos();
                UpdatePlasmaChargeVfxTransform();
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
