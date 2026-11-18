using System.Collections;
using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Multi-phase God of War / Real Steel-inspired Boss AI for Category IV Kaiju Leatherback.
    /// Features forward/backward walking and sprinting, tactical retreating, predatory circle-strafing
    /// with procedural 2-bone leg IK and head/torso lock-on, predictive anti-spam forearm blocking,
    /// hyper-armored counters, exact directional staggers (Right/Left/Up) with combo-breaker,
    /// combat sparks feedback, and 3 escalating combat phases.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    [RequireComponent(typeof(Rigidbody))]
    public class KaijuAI : MonoBehaviour
    {
        public enum AIState
        {
            Idle,
            ChasingAndStrafing,
            SizingUp,
            Retreating,
            EmpRoar,
            PhaseTransition,
            Attacking,
            Blocking,
            Staggered,
            Dead
        }

        public enum AttackPattern
        {
            None,
            LeftHook,
            RightHook,
            DoubleHookCombo,
            TripleBrawlerCombo,
            HeavySlam,
            SprintChargeSlam,
            AbyssalDartStrike,
            DeviousBlitzLightCombo
        }

        private const float PunchClipLength = 0.97f;
        private const float SlamClipLength = 1.13f;
        private const float EmpClipLength = 2.63f;
        private const float StaggerSideClipLength = 1.97f;
        private const float StaggerUpClipLength = 2.30f;
        private const float StaggerDownClipLength = 2.30f;
        private const float DeathClipLength = 4.30f;

        // Slattern FBX animation clip lengths and exact peak extension/impact timestamps (measured from Slattern.fbx)
        private const float SlatternPunchClipLength = 1.16f;
        private const float SlatternSlamClipLength = 1.56f;
        private const float SlatternEmpClipLength = 2.76f;
        private const float SlatternStaggerClipLength = 2.76f;
        private const float SlatternDeathClipLength = 5.16f;

        private float ActivePunchClipLength => isSlattern ? SlatternPunchClipLength : PunchClipLength;
        private float ActiveSlamClipLength => isSlattern ? SlatternSlamClipLength : SlamClipLength;
        private float ActiveEmpClipLength => isSlattern ? SlatternEmpClipLength : EmpClipLength;
        private float PunchLeftImpactTime => isSlattern ? 1.01f : 0.867f;
        private float PunchRightImpactTime => isSlattern ? 0.91f : 0.767f;
        private float SlamImpactTime => isSlattern ? 1.50f : 1.033f;

        private static readonly int LocomotionSpeedHash = Animator.StringToHash("LocomotionSpeed");

        [Header("Boss Identity")]
        public string kaijuDisplayName = "LEATHERBACK";
        public string kaijuCategoryLabel = "CATEGORY IV KAIJU";
        public bool isSlattern = false;
        [Tooltip("Vertical root offset above ground raycast hit (2.0m for Leatherback, auto-calibrated for Slattern).")]
        public float groundYOffset = 2.0f;

        [Header("Target & References")]
        [Tooltip("Target Gipsy Danger Jaeger controller. Auto-resolved if left unset.")]
        public SimpleJaegerController targetJaeger;
        public Animator animator;
        public float detectionRange = 400f;
        [Tooltip("True while pre-fight cutscenes are running; freezes Kaiju AI and suppresses movement.")]
        public bool isCutsceneMode = false;

        [Header("Boss Phases, Health, Poise & Guard")]
        public float maxHealth = 4200f;
        public float currentHealth = 4200f;
        public float maxPoise = 140f;
        public float currentPoise = 140f;
        public float poiseRegenRate = 22f;
        public float poiseRegenDelay = 2.0f;
        [Range(0f, 0.95f)]
        public float blockDamageReduction = 0.88f;
        public float heavyAttackDamageThreshold = 95f;
        public float phase3DamageMultiplier = 0.65f;
        public float phase3HealAmount = 450f;

        [Header("Slattern 3-Phase & Parry Profile")]
        [Range(0f, 1f)] public float slatternPhase1ParryChance = 0.10f;
        [Range(0f, 1f)] public float slatternPhase2ParryChance = 0.42f;
        [Range(0f, 1f)] public float slatternPhase3ParryChance = 0.10f;
        public float slatternStalkMinDist = 33f;
        public float slatternStalkIdealDist = 44f;

        [Header("Locomotion & Combat Ranges")]
        [Tooltip("Allow Leatherback to sprint during normal combat/retreats. When false, he only sprints if the player runs too far away.")]
        public bool canSprint = false;
        [Tooltip("Distance (m) at which the player is considered fleeing to the far corners of the map, triggering pursuit sprint.")]
        public float chaseSprintThreshold = 450f;
        [Tooltip("Distance (m) at which pursuit sprint stops and Leatherback resumes his heavy walk.")]
        public float chaseSprintStopDistance = 252f;
        [Tooltip("Leatherback walks slower after balance tuning to give Gipsy breathing room.")]
        public float walkSpeed = 7.0f;
        [Tooltip("Leatherback charge sprint speed.")]
        public float sprintSpeed = 11.67f;
        public float empWalkSpeed = 8.13f;
        public float empSprintSpeed = 12.67f;
        public float backwardWalkSpeed = 5.87f;
        public float backwardSprintSpeed = 10.33f;
        public float strafeSpeed = 4.53f;
        public float turnSpeed = 115f;
        public float attackTurnSpeed = 46f;
        public float acceleration = 4.8f;
        public float minCombatDistance = 13.5f;
        public float meleeAttackRange = 19.5f;
        public float sprintMinRange = 20.5f;

        [Header("Combat Attacks & Bio-EMP Overcharge")]
        public float lightPunchDamage = 27.5f;
        public float heavySlamDamage = 52.5f;
        public float punchStrikeRadius = 11.5f;
        public float slamStrikeRadius = 13.5f;
        public float empBuffDuration = 18.0f;
        public float empCooldownDuration = 20.0f;
        public float initialEmpDelay = 5.0f;

        [Header("Ocean Shockwaves & Wading")]
        public float strideDistance = 7.5f;
        public float footstepWaveRadius = 18f;
        public float footstepWaveStrength = 2.4f;
        public float slamWaveRadius = 30f;
        public float slamWaveStrength = 2.8f;

        [Header("Procedural Spine & Lateral Strafe IK")]
        public bool enableProceduralIK = true;
        public float maxTorsoLookYaw = 24f;
        public float maxStrafePelvisYaw = 26f;
        public float strafeStanceSpread = 0.95f;
        public float strafeStepReach = 1.85f;
        public float strafeStepExtraLift = 0.45f;

        [Header("Runtime State")]
        public static KaijuAI Instance { get; private set; }
        public bool IsBaguvixActive { get; private set; }
        public bool IsAIDisabled { get; private set; }

        public AIState currentState = AIState.ChasingAndStrafing;
        public AttackPattern activePattern = AttackPattern.None;

        public int CurrentPhase { get; private set; } = 1;
        public bool IsDead => currentState == AIState.Dead;
        public bool IsEmpBuffed => (CurrentPhase >= 3 || Time.time < empBuffEndTime) && !IsDead;
        public float EmpBuffRemaining => CurrentPhase >= 3 ? empBuffDuration : Mathf.Max(0f, empBuffEndTime - Time.time);
        public float EmpBuffDuration => empBuffDuration;
        public bool IsMovingGuardActive => isSlattern && CurrentPhase == 3 && !IsDead && !IsAIDisabled && !IsBaguvixActive &&
            (currentState == AIState.ChasingAndStrafing || currentState == AIState.SizingUp);
        public bool IsBlocking => currentState == AIState.Blocking || IsMovingGuardActive;
        public bool IsStaggered => currentState == AIState.Staggered;
        public float PoiseRatio => maxPoise > 0f ? Mathf.Clamp01(currentPoise / maxPoise) : 0f;

        public string PhaseTitleLabel
        {
            get
            {
                if (IsDead) return "THREAT ELIMINATED";
                if (IsAIDisabled) return $"{kaijuDisplayName} // AI DISABLED";
                if (IsBaguvixActive) return "PACIFIST MODE";
                if (isSlattern)
                {
                    if (CurrentPhase == 3) return "PHASE III // MOVING-GUARD JUGGERNAUT";
                    if (CurrentPhase == 2) return "PHASE II // APEX PARRY BLITZER";
                    return "PHASE I // ABYSSAL DISTANCE FIGHTER";
                }
                if (CurrentPhase == 3) return $"PHASE III // {kaijuCategoryLabel} BERSERKER";
                if (CurrentPhase == 2) return "PHASE II // BIO-EMP OVERDRIVE";
                return "PHASE I // APEX STALKER";
            }
        }

        public string TacticalStatusLabel
        {
            get
            {
                if (IsDead) return "KAIJU NEUTRALIZED";
                if (IsAIDisabled && !isDebugForcedActionActive) return "AI OFFLINE";
                if (IsBaguvixActive) return "PACIFIST";
                if (currentState == AIState.PhaseTransition) return $"PHASE {CurrentPhase} SURGE";
                if (IsStaggered) return isGuardBrokenStagger ? "GUARD BROKEN // EXECUTE COMBO!" : "STAGGERED // GUARD BROKEN";
                if (isSlattern)
                {
                    if (isParryCountering || activePattern == AttackPattern.DeviousBlitzLightCombo) return "PARRY BLITZ // 4-HIT LIGHT COMBO";
                    if (IsMovingGuardActive) return "MOVING GUARD // HEAVY TO BREAK";
                    if (CurrentPhase == 1 && (currentState == AIState.Retreating || currentState == AIState.SizingUp || isStrafingInRange))
                        return "DISTANCE ZONER // STALKING IN FOG";
                    if (CurrentPhase == 2 && (currentState == AIState.Retreating || currentState == AIState.SizingUp || isStrafingInRange))
                        return "BLITZER // READY TO PARRY";
                }
                if (IsBlocking) return "ARMORED FORE-GUARD";
                if (currentState == AIState.Retreating) return retreatWithSprint ? "EVASIVE BACK-SPRINT" : "TACTICAL RETREAT";
                if (currentState == AIState.SizingUp) return "SIZING UP // STALKING";
                if (IsEmpBuffed && currentAnimState == "Run") return isSlattern ? "ABYSSAL SURGE // CHARGING" : "BIO-EMP OVERCHARGE // CHARGING";
                if (IsEmpBuffed || currentState == AIState.EmpRoar) return isSlattern ? "ABYSSAL SURGE" : "BIO-EMP OVERCHARGE";
                if (currentState == AIState.Attacking) return hasHyperArmor ? "ARMORED COUNTER-ASSAULT" : (isSlattern && CurrentPhase == 3 ? "ATTACKING // CATCH OFF-GUARD!" : "HEAVY ASSAULT");
                if (currentState == AIState.ChasingAndStrafing && isStrafingInRange) return "CIRCLE STALKING";
                return "ENGAGING";
            }
        }

        private Rigidbody rb;
        private Coroutine activeSequenceCoroutine;

        private float currentBodyYaw;
        private bool bodyYawInitialized;
        private Vector3 desiredVelocity;
        private string currentAnimState = "";
        private bool hasLocomotionSpeedParam;
        private float currentLocomotionSpeed = 1f;

        private float stateTimer;
        private float postAttackPacingTimer = 0.7f;
        private float sizingUpCooldown = 5.0f;
        private float empCooldownTimer;
        private float empBuffEndTime = -100f;
        private float lastDamageTime = -100f;
        private float lastPlayerAttackNotifyTime = -100f;
        private int playerSpamCount;
        private int consecutiveStaggers;
        private float lastStaggerTime = -100f;

        private bool counterQueued;
        private bool blockEnterFast;
        private bool hasHyperArmor;
        private bool retreatWithSprint;
        private float retreatTargetDist = 28f;
        private bool deathLoopEntered;
        private bool hasCelebratedPlayerDefeat;

        private float slatternBlitzCooldown = 2.0f;
        private bool isParryCountering;
        private bool isGuardBrokenStagger;
        private bool isDebugForcedActionActive;
        private float upperBodyGuardWeight;
        private int movingGuardLightHits;

        private float circleStrafeDir = 1f;
        private float strafeSwitchTimer = 3.0f;
        private bool isStrafingInRange;

        private Vector3 lastStepPos;
        private bool isLeftFoot = true;

        // Cached Rig Bones
        private Transform upperTorsoBone;
        private Transform neckBone;
        private Transform hipL;
        private Transform upperLegL;
        private Transform lowerLegL;
        private Transform ankleL;
        private Transform hipR;
        private Transform upperLegR;
        private Transform lowerLegR;
        private Transform ankleR;
        private Transform handL;
        private Transform handR;

        // Procedural IK smoothing state
        private float currentStrafeYaw;
        private float currentStrafeWeight;
        private float currentStrafeDir;
        private float currentTorsoYaw;
        private float currentNeckYaw;

        private void Awake()
        {
            Instance = this;
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            currentBodyYaw = transform.eulerAngles.y;
            bodyYawInitialized = true;
            currentHealth = maxHealth;
            currentPoise = maxPoise;
            CurrentPhase = 1;
            empCooldownTimer = initialEmpDelay;
            circleStrafeDir = Random.value > 0.5f ? 1f : -1f;

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var p in animator.parameters)
                {
                    if (p.nameHash == LocomotionSpeedHash)
                    {
                        hasLocomotionSpeedParam = true;
                        break;
                    }
                }
            }

            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
            }

            CacheRigBones();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetPacifistMode(bool active)
        {
            IsBaguvixActive = active;
            CancelActiveSequence();
            isDebugForcedActionActive = false;
            hasHyperArmor = false;
            counterQueued = false;
            blockEnterFast = false;
            if (active)
            {
                currentState = AIState.SizingUp;
                stateTimer = Random.Range(3.0f, 5.0f);
                circleStrafeDir = Random.value > 0.5f ? 1f : -1f;
            }
            else if (!IsAIDisabled && !IsDead)
            {
                currentState = AIState.ChasingAndStrafing;
                postAttackPacingTimer = 0.25f;
            }
        }

        public void SetAIDisabled(bool disabled)
        {
            IsAIDisabled = disabled;
            CancelActiveSequence();
            isDebugForcedActionActive = false;
            hasHyperArmor = false;
            counterQueued = false;
            blockEnterFast = false;
            isStrafingInRange = false;
            desiredVelocity = Vector3.zero;

            if (disabled)
            {
                currentState = AIState.Idle;
                SetLocomotionSpeed(1f);
                CrossFadeState("Idle", 0.2f);
            }
            else if (!IsDead)
            {
                currentState = IsBaguvixActive ? AIState.SizingUp : AIState.ChasingAndStrafing;
                postAttackPacingTimer = 0.25f;
            }
        }

        public void DisableAIEntirely() => SetAIDisabled(true);

        public void DebugForceAttack(AttackPattern pattern)
        {
            if (IsDead || pattern == AttackPattern.None) return;
            ResolveTarget();
            CancelActiveSequence();
            isDebugForcedActionActive = true;
            isParryCountering = false;
            currentState = AIState.Attacking;
            activePattern = pattern;
            hasHyperArmor = (pattern == AttackPattern.HeavySlam || pattern == AttackPattern.SprintChargeSlam);

            if (targetJaeger != null)
            {
                Vector3 toTarget = targetJaeger.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    currentBodyYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0f, currentBodyYaw, 0f);
                }
            }

            SetLocomotionSpeed(1f);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuAttackGrunt(transform.position + Vector3.up * 14f);
            activeSequenceCoroutine = StartCoroutine(ExecuteAttackSequence(pattern));
        }

        public void DebugForceEmpRoar()
        {
            if (IsDead) return;
            CancelActiveSequence();
            isDebugForcedActionActive = IsAIDisabled;
            currentState = AIState.EmpRoar;
            stateTimer = EmpClipLength;
            empCooldownTimer = empCooldownDuration;
            desiredVelocity = Vector3.zero;
            hasHyperArmor = true;
            SetLocomotionSpeed(1f);
            CrossFadeState("EMP", 0.16f, true);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuEmpRoar(transform.position + Vector3.up * 14f);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayEmpChargeUp(transform.position + Vector3.up * 12f);
            activeSequenceCoroutine = StartCoroutine(EmpShockwaveRoutine());
        }

        public void DebugForceBlock()
        {
            if (IsDead) return;
            isDebugForcedActionActive = IsAIDisabled;
            StartBlock(fastReaction: false);
        }

        public void DebugForceStagger()
        {
            if (IsDead) return;
            isDebugForcedActionActive = IsAIDisabled;
            TriggerParryStagger(-transform.forward * 10f);
        }

        public void DebugSetPhase(int phase)
        {
            if (IsDead) DebugResetBoss();
            phase = Mathf.Clamp(phase, 1, 3);
            CancelActiveSequence();
            isDebugForcedActionActive = false;
            CurrentPhase = phase;
            currentPoise = maxPoise;
            consecutiveStaggers = 0;

            if (phase == 1)
            {
                currentHealth = maxHealth;
                empBuffEndTime = -100f;
            }
            else if (phase == 2)
            {
                currentHealth = maxHealth * 0.60f;
                empBuffEndTime = Time.time + empBuffDuration;
            }
            else
            {
                currentHealth = maxHealth * 0.25f;
                empBuffEndTime = Time.time + 999f;
            }

            currentState = IsAIDisabled ? AIState.Idle : AIState.ChasingAndStrafing;
        }

        public void DebugResetBoss()
        {
            CancelActiveSequence();
            StopAllCoroutines();
            isDebugForcedActionActive = false;
            hasHyperArmor = false;
            counterQueued = false;
            blockEnterFast = false;
            isParryCountering = false;
            isGuardBrokenStagger = false;
            deathLoopEntered = false;
            hasCelebratedPlayerDefeat = false;
            consecutiveStaggers = 0;
            CurrentPhase = 1;
            currentHealth = maxHealth;
            currentPoise = maxPoise;
            empBuffEndTime = -100f;
            empCooldownTimer = initialEmpDelay;
            desiredVelocity = Vector3.zero;
            currentState = IsAIDisabled ? AIState.Idle : AIState.ChasingAndStrafing;
            SetLocomotionSpeed(1f);
            CrossFadeState("Idle", 0.1f, true);
        }

        public void DebugKillBoss()
        {
            if (IsDead) return;
            Die();
        }

        private void Start()
        {
            ResolveTarget();
            IgnorePlatformCollisions();
            lastStepPos = transform.position;
            SetLocomotionSpeed(1f);
            CrossFadeState("Idle", 0.05f, true);
        }

        private void ResolveTarget()
        {
            if (targetJaeger != null) return;
            targetJaeger = FindAnyObjectByType<SimpleJaegerController>();
        }

        private void IgnorePlatformCollisions()
        {
            var myCols = GetComponentsInChildren<Collider>(true);
            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include))
            {
                if (col == null || col.transform.IsChildOf(transform)) continue;
                if (col.CompareTag("Ground") || col.gameObject.name.Contains("Platform") || col.gameObject.name.Contains("Ramp") || col.gameObject.name.Contains("Deck"))
                {
                    foreach (var myCol in myCols)
                    {
                        if (myCol != null) Physics.IgnoreCollision(myCol, col, true);
                    }
                }
            }
        }

        private void CacheRigBones()
        {
            if (upperTorsoBone != null && ankleL != null && handR != null) return;

            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "UpperTorsoBone":
                    case "UpperTorso":
                        upperTorsoBone = t;
                        break;
                    case "NeckBone":
                    case "Neck":
                        neckBone = t;
                        break;
                    case "HipBone.L":
                    case "Pelvis.L":
                        hipL = t;
                        break;
                    case "UpperLegBone.L":
                    case "UpperLeg.L":
                        upperLegL = t;
                        break;
                    case "LowerLegBone.L":
                    case "LowerLeg.L":
                        lowerLegL = t;
                        break;
                    case "AnkleBone.L":
                    case "Ankle.L":
                        ankleL = t;
                        break;
                    case "HipBone.R":
                    case "Pelvis.R":
                        hipR = t;
                        break;
                    case "UpperLegBone.R":
                    case "UpperLeg.R":
                        upperLegR = t;
                        break;
                    case "LowerLegBone.R":
                    case "LowerLeg.R":
                        lowerLegR = t;
                        break;
                    case "AnkleBone.R":
                    case "Ankle.R":
                        ankleR = t;
                        break;
                    case "HandBone.L":
                    case "PalmBone.L":
                    case "Wrist.L":
                        if (handL == null || t.name == "HandBone.L" || t.name == "PalmBone.L")
                            handL = t;
                        break;
                    case "HandBone.R":
                    case "PalmBone.R":
                    case "Wrist.R":
                        if (handR == null || t.name == "HandBone.R" || t.name == "PalmBone.R")
                            handR = t;
                        break;
                }
            }
        }

        private void Update()
        {
            ResolveTarget();

            if (PacificRimUI.MainMenuUI.IsMenuOpen)
            {
                desiredVelocity = Vector3.zero;
                return;
            }

            UpdateUpperBodyGuardLayer(Time.deltaTime);

            if (IsAIDisabled && !isDebugForcedActionActive)
            {
                desiredVelocity = Vector3.zero;
                SetLocomotionSpeed(1f);
                if (currentState != AIState.Idle)
                {
                    currentState = AIState.Idle;
                    CrossFadeState("Idle", 0.2f);
                }
                return;
            }

            if (IsDead)
            {
                desiredVelocity = Vector3.zero;
                SetLocomotionSpeed(1f);
                stateTimer -= Time.deltaTime;
                if (!deathLoopEntered && stateTimer <= 0f)
                {
                    deathLoopEntered = true;
                    CrossFadeState("DeathLoop", 0.35f);
                }
                return;
            }

            if (isCutsceneMode && attentionTurnTimer <= 0f)
            {
                desiredVelocity = Vector3.zero;
                SetLocomotionSpeed(0f);
                CrossFadeState("Idle", 0.25f);
                return;
            }

            if (targetJaeger == null || targetJaeger.IsDead)
            {
                if (targetJaeger != null && targetJaeger.IsDead && !hasCelebratedPlayerDefeat)
                {
                    TriggerVictoryRoar();
                    return;
                }

                if (currentState == AIState.EmpRoar)
                {
                    // Allow victory EMP blast animation to finish playing before dropping to idle
                    stateTimer -= Time.deltaTime;
                    desiredVelocity = Vector3.zero;
                    SetLocomotionSpeed(1f);
                    if (stateTimer <= 0f)
                    {
                        currentState = AIState.Idle;
                        CrossFadeState("Idle", 0.3f);
                    }
                    return;
                }

                desiredVelocity = Vector3.zero;
                SetLocomotionSpeed(1f);
                CrossFadeState("Idle", 0.2f);
                return;
            }

            float dt = Time.deltaTime;

            if (Time.time - lastPlayerAttackNotifyTime > 2.6f)
            {
                playerSpamCount = 0;
            }

            if (Time.time - lastStaggerTime > 2.2f)
            {
                consecutiveStaggers = 0;
            }

            // Regenerate poise smoothly out of guard/stagger
            if (currentState != AIState.Staggered && currentState != AIState.Blocking &&
                Time.time >= lastDamageTime + poiseRegenDelay)
            {
                currentPoise = Mathf.Min(maxPoise, currentPoise + poiseRegenRate * dt);
            }

            if (empCooldownTimer > 0f && !IsEmpBuffed && currentState != AIState.EmpRoar)
            {
                empCooldownTimer -= dt;
                if (!IsEmpBuffed)
                {
                    PacificRimAudio.PacificRimAudioManager.Instance?.SetKaijuEmpSparksLoop(false, transform.position);
                }
            }

            if (postAttackPacingTimer > 0f && currentState == AIState.ChasingAndStrafing)
            {
                postAttackPacingTimer -= dt;
            }

            if (sizingUpCooldown > 0f)
            {
                sizingUpCooldown -= dt;
            }

            if (slatternBlitzCooldown > 0f && currentState != AIState.Attacking)
            {
                slatternBlitzCooldown -= dt;
            }

            strafeSwitchTimer -= dt;
            if (strafeSwitchTimer <= 0f)
            {
                strafeSwitchTimer = Random.Range(2.4f, 4.2f);
                circleStrafeDir = -circleStrafeDir;
            }

            if (attentionTurnTimer > 0f)
            {
                attentionTurnTimer -= dt;
                float t = 1f - Mathf.Clamp01(attentionTurnTimer / Mathf.Max(0.01f, attentionTurnDuration));
                float signedAngle = 0f;
                float targetYaw = currentBodyYaw;
                bool hasTarget = false;

                if (attentionOverrideYaw.HasValue)
                {
                    targetYaw = attentionOverrideYaw.Value;
                    Vector3 targetDir = Quaternion.Euler(0f, targetYaw, 0f) * Vector3.forward;
                    signedAngle = Vector3.SignedAngle(transform.forward, targetDir, Vector3.up);
                    hasTarget = true;
                }
                else if (targetJaeger != null)
                {
                    Vector3 toTarget = targetJaeger.transform.position - transform.position;
                    toTarget.y = 0f;
                    if (toTarget.sqrMagnitude > 0.01f)
                    {
                        Vector3 dirToTarget = toTarget.normalized;
                        targetYaw = Mathf.Atan2(dirToTarget.x, dirToTarget.z) * Mathf.Rad2Deg;
                        signedAngle = Vector3.SignedAngle(transform.forward, dirToTarget, Vector3.up);
                        hasTarget = true;
                    }
                }

                if (hasTarget)
                {
                    if (Mathf.Abs(signedAngle) > 165f)
                    {
                        signedAngle = Mathf.Abs(signedAngle) * attentionTurnSign;
                    }

                    // Delay root body rotation for the first ~0.45s (t <= 0.14) so head & upper torso turn over the shoulder first
                    if (t > 0.14f)
                    {
                        float easedSpeed = turnSpeed * 0.62f * Mathf.Sin(Mathf.Clamp01((t - 0.14f) / 0.86f) * Mathf.PI);
                        float step = Mathf.Min(Mathf.Abs(signedAngle), Mathf.Max(0f, easedSpeed) * dt);
                        currentBodyYaw = Mathf.Repeat(currentBodyYaw + Mathf.Sign(signedAngle) * step, 360f);
                        transform.rotation = Quaternion.Euler(0f, currentBodyYaw, 0f);
                    }
                }

                if (attentionTurnTimer <= 0f)
                {
                    attentionOverrideYaw = null;
                }

                desiredVelocity = Vector3.zero;
                if (t > 0.14f && Mathf.Abs(signedAngle) > 10f)
                {
                    SetLocomotionSpeed(0.55f);
                    CrossFadeState("Walk", 0.22f);
                }
                else
                {
                    SetLocomotionSpeed(1f);
                    CrossFadeState("Idle", 0.25f);
                }

                UpdateAnimatorPlaybackSpeed();
                TrackFootsteps();
                return;
            }

            UpdateAnimatorPlaybackSpeed();
            UpdateStateMachine(dt);
            TrackFootsteps();
        }

        private float attentionTurnTimer = 0f;
        private float attentionTurnDuration = 2.6f;
        private float? attentionOverrideYaw = null;
        private float attentionTurnSign = 1f;
        private bool isPursuitSprinting = false;
        public bool IsAttentionTurning => attentionTurnTimer > 0f;

        public void SetCutscenePose(float yawDegrees)
        {
            isCutsceneMode = true;
            attentionTurnTimer = 0f;
            attentionOverrideYaw = null;
            desiredVelocity = Vector3.zero;
            currentBodyYaw = yawDegrees;
            bodyYawInitialized = true;
            currentTorsoYaw = 0f;
            currentNeckYaw = 0f;
            currentStrafeYaw = 0f;
            currentStrafeWeight = 0f;
            Quaternion rot = Quaternion.Euler(0f, yawDegrees, 0f);
            transform.rotation = rot;
            if (rb != null)
            {
                rb.rotation = rot;
                rb.MoveRotation(rot);
            }
        }

        /// <summary>
        /// Temporarily animates Leatherback using procedural IK (head/neck lead, upper torso follow,
        /// and 2-bone leg pivot) to slowly turn his attention toward Gipsy (or an explicit yaw like 180)
        /// after the foghorn sounds.
        /// </summary>
        public void TriggerFoghornAttentionTurn(float duration = 2.6f, float? overrideTargetYaw = null)
        {
            if (IsDead) return;
            attentionTurnDuration = Mathf.Max(0.5f, duration);
            attentionTurnTimer = attentionTurnDuration;
            attentionOverrideYaw = overrideTargetYaw;
            currentBodyYaw = transform.eulerAngles.y;

            Vector3 targetDir = Vector3.zero;
            if (overrideTargetYaw.HasValue)
            {
                targetDir = Quaternion.Euler(0f, overrideTargetYaw.Value, 0f) * Vector3.forward;
            }
            else if (targetJaeger != null)
            {
                Vector3 toTarget = targetJaeger.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f) targetDir = toTarget.normalized;
            }

            if (targetDir.sqrMagnitude > 0.01f)
            {
                float initialSigned = Vector3.SignedAngle(transform.forward, targetDir, Vector3.up);
                attentionTurnSign = Mathf.Abs(initialSigned) > 165f ? 1f : (initialSigned >= 0f ? 1f : -1f);
            }
            else
            {
                attentionTurnSign = 1f;
            }

            desiredVelocity = Vector3.zero;
        }

        private void UpdateUpperBodyGuardLayer(float dt)
        {
            if (animator == null || animator.layerCount <= 1) return;

            float targetWeight = IsMovingGuardActive ? 1.0f : 0.0f;
            float speed = currentState == AIState.Attacking || currentState == AIState.Staggered || IsDead ? 18.0f : 7.5f;
            upperBodyGuardWeight = Mathf.MoveTowards(upperBodyGuardWeight, targetWeight, dt * speed);
            animator.SetLayerWeight(1, upperBodyGuardWeight);
        }

        private void UpdateAnimatorPlaybackSpeed()
        {
            if (animator == null) return;

            if (currentState == AIState.Attacking && activePattern == AttackPattern.DeviousBlitzLightCombo)
            {
                animator.speed = IsEmpBuffed ? 1.55f : 1.45f;
            }
            else if (currentState == AIState.Blocking && blockEnterFast)
            {
                animator.speed = 1.75f;
            }
            else if (currentState == AIState.Staggered)
            {
                animator.speed = (currentAnimState == "StaggerUp" || currentAnimState == "StaggerDown") ? 1.15f : 1.35f;
            }
            else if (currentState == AIState.PhaseTransition)
            {
                animator.speed = 1.25f;
            }
            else if (IsEmpBuffed)
            {
                animator.speed = currentState == AIState.Attacking
                    ? (CurrentPhase == 3 ? 1.28f : 1.20f)
                    : 1.15f;
            }
            else
            {
                animator.speed = 1.0f;
            }
        }

        private void FixedUpdate()
        {
            if (rb == null) return;

            if (IsAIDisabled && !isDebugForcedActionActive)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
#endif
                return;
            }

            // Ground elevation tracking across seabed, ramps, and elevated city decks
            float groundY = 0f;
            if (Physics.Raycast(transform.position + Vector3.up * 40f, Vector3.down, out RaycastHit groundHit, 90f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
            {
                if (!groundHit.transform.IsChildOf(transform))
                {
                    groundY = groundHit.point.y;
                }
            }
            float targetY = groundY + groundYOffset;
            float vertVel = Mathf.Clamp((targetY - rb.position.y) * 8f, -20f, 20f);

#if UNITY_6000_0_OR_NEWER
            Vector3 currentVel = rb.linearVelocity;
            Vector3 horiz = new Vector3(currentVel.x, 0f, currentVel.z);
            Vector3 nextHoriz = Vector3.Lerp(horiz, desiredVelocity, Time.fixedDeltaTime * acceleration);
            rb.linearVelocity = new Vector3(nextHoriz.x, vertVel, nextHoriz.z);
#else
            Vector3 currentVel = rb.velocity;
            Vector3 horiz = new Vector3(currentVel.x, 0f, currentVel.z);
            Vector3 nextHoriz = Vector3.Lerp(horiz, desiredVelocity, Time.fixedDeltaTime * acceleration);
            rb.velocity = new Vector3(nextHoriz.x, vertVel, nextHoriz.z);
#endif

            if (bodyYawInitialized)
            {
                Quaternion bodyRot = Quaternion.Euler(0f, currentBodyYaw, 0f);
                rb.rotation = bodyRot;
                rb.MoveRotation(bodyRot);
            }
        }

        private void LateUpdate()
        {
            if (!enableProceduralIK || IsDead || IsAIDisabled || PacificRimUI.MainMenuUI.IsMenuOpen) return;

            CacheRigBones();
            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;

            bool isAttentionTurn = IsAttentionTurning;
            bool isLocoState = !isCutsceneMode && (currentState == AIState.ChasingAndStrafing ||
                               currentState == AIState.SizingUp ||
                               currentState == AIState.Retreating);

            // 1. Upper-torso & head tracking toward Gipsy
            float targetTorsoYaw = 0f;
            float targetNeckYaw = 0f;
            float attentionSignedAngle = 0f;
            if (targetJaeger != null && (isLocoState || currentState == AIState.Blocking || isAttentionTurn))
            {
                Vector3 lookDir = Vector3.zero;
                if (isAttentionTurn && attentionOverrideYaw.HasValue)
                {
                    lookDir = Quaternion.Euler(0f, attentionOverrideYaw.Value, 0f) * Vector3.forward;
                }
                else
                {
                    Vector3 toTarget = targetJaeger.transform.position - transform.position;
                    toTarget.y = 0f;
                    if (toTarget.sqrMagnitude > 0.01f) lookDir = toTarget.normalized;
                }

                if (lookDir.sqrMagnitude > 0.01f)
                {
                    float signedAngle = Vector3.SignedAngle(transform.forward, lookDir, Vector3.up);
                    if (isAttentionTurn && Mathf.Abs(signedAngle) > 165f)
                    {
                        signedAngle = Mathf.Abs(signedAngle) * attentionTurnSign;
                    }
                    attentionSignedAngle = signedAngle;
                    if (isAttentionTurn)
                    {
                        float t = 1f - Mathf.Clamp01(attentionTurnTimer / Mathf.Max(0.01f, attentionTurnDuration));
                        float attentionBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 3.0f));
                        targetNeckYaw = Mathf.Clamp(signedAngle * 0.78f, -maxTorsoLookYaw * 1.85f, maxTorsoLookYaw * 1.85f) * attentionBlend;
                        targetTorsoYaw = Mathf.Clamp(signedAngle * 0.55f, -maxTorsoLookYaw * 1.30f, maxTorsoLookYaw * 1.30f) * attentionBlend;
                    }
                    else
                    {
                        targetTorsoYaw = Mathf.Clamp(signedAngle * 0.45f, -maxTorsoLookYaw, maxTorsoLookYaw);
                        targetNeckYaw = Mathf.Clamp(signedAngle * 0.60f, -maxTorsoLookYaw * 1.2f, maxTorsoLookYaw * 1.2f);
                    }
                }
            }

            currentTorsoYaw = Mathf.MoveTowards(currentTorsoYaw, targetTorsoYaw, (isAttentionTurn ? 65f : 100f) * dt);
            currentNeckYaw = Mathf.MoveTowards(currentNeckYaw, targetNeckYaw, (isAttentionTurn ? 95f : 150f) * dt);

            if (Mathf.Abs(currentTorsoYaw) > 0.05f && upperTorsoBone != null)
            {
                upperTorsoBone.rotation = Quaternion.AngleAxis(currentTorsoYaw, transform.up) * upperTorsoBone.rotation;
            }
            if (IsMovingGuardActive && (animator == null || animator.layerCount <= 1) && upperTorsoBone != null)
            {
                upperTorsoBone.rotation = Quaternion.AngleAxis(14f, transform.right) * upperTorsoBone.rotation;
            }
            if (Mathf.Abs(currentNeckYaw) > 0.05f && neckBone != null)
            {
                neckBone.rotation = Quaternion.AngleAxis(currentNeckYaw, transform.up) * neckBone.rotation;
            }

            // 2. Lateral Strafe Pelvis Steering & 2-Bone Analytical Leg IK
            float desiredStrafeWeight = 0f;
            float desiredStrafeYaw = 0f;
            float desiredStrafeDir = 0f;

            if (isAttentionTurn && Mathf.Abs(attentionSignedAngle) > 6f)
            {
                desiredStrafeWeight = Mathf.Clamp01(Mathf.Abs(attentionSignedAngle) / 45f) * 0.65f;
                desiredStrafeYaw = Mathf.Clamp(attentionSignedAngle * 0.45f, -maxStrafePelvisYaw, maxStrafePelvisYaw);
                desiredStrafeDir = Mathf.Sign(attentionSignedAngle) * 0.5f;
            }
            else if (isLocoState && desiredVelocity.sqrMagnitude > 0.5f)
            {
                Vector3 flatVel = new Vector3(desiredVelocity.x, 0f, desiredVelocity.z);
                float latSpeed = Vector3.Dot(flatVel, transform.right);
                float fwdSpeed = Vector3.Dot(flatVel, transform.forward);
                float speedMag = flatVel.magnitude;

                if (Mathf.Abs(latSpeed) > 0.35f && speedMag > 0.5f)
                {
                    float dirSign = fwdSpeed < -0.5f ? -1f : 1f;
                    float latNorm = Mathf.Clamp(latSpeed / Mathf.Max(0.1f, strafeSpeed), -1f, 1f);
                    float effFwd = Mathf.Max(0.42f, Mathf.Abs(fwdSpeed) / Mathf.Max(0.1f, walkSpeed));
                    float rawAngle = Mathf.Atan2(latNorm * dirSign, effFwd) * Mathf.Rad2Deg;

                    desiredStrafeYaw = Mathf.Clamp(rawAngle, -maxStrafePelvisYaw, maxStrafePelvisYaw);
                    desiredStrafeWeight = Mathf.Clamp01(Mathf.Abs(latNorm));
                    desiredStrafeDir = latNorm;
                }
            }

            currentStrafeYaw = Mathf.MoveTowards(currentStrafeYaw, desiredStrafeYaw, 180f * dt);
            currentStrafeWeight = Mathf.MoveTowards(currentStrafeWeight, desiredStrafeWeight, 5.0f * dt);
            currentStrafeDir = Mathf.MoveTowards(currentStrafeDir, desiredStrafeDir, 6.0f * dt);

            if (currentStrafeWeight > 0.001f)
            {
                if (Mathf.Abs(currentStrafeYaw) > 0.05f)
                {
                    if (hipL != null)
                        hipL.rotation = Quaternion.AngleAxis(currentStrafeYaw * 0.6f, transform.up) * hipL.rotation;
                    if (hipR != null)
                        hipR.rotation = Quaternion.AngleAxis(currentStrafeYaw * 0.6f, transform.up) * hipR.rotation;
                }

                ApplyStrafeLegIK(upperLegL, lowerLegL, ankleL, -1f);
                ApplyStrafeLegIK(upperLegR, lowerLegR, ankleR, 1f);
            }
        }

        private void ApplyStrafeLegIK(Transform thigh, Transform shin, Transform ankle, float sideSign)
        {
            if (thigh == null || shin == null || ankle == null) return;

            // Measured Walk AnkleBone height offset above root: min=4.49m, max=9.58m
            float relAnkleY = ankle.position.y - transform.position.y;
            float swingLift = Mathf.Clamp01((relAnkleY - 4.8f) / 4.2f);

            Vector3 lateralSpread = transform.right * (sideSign * strafeStanceSpread * currentStrafeWeight);
            Vector3 swingLateralReach = transform.right * (currentStrafeDir * swingLift * strafeStepReach * currentStrafeWeight);
            Vector3 swingVertLift = Vector3.up * (swingLift * strafeStepExtraLift * currentStrafeWeight);
            Vector3 targetPos = ankle.position + lateralSpread + swingLateralReach + swingVertLift;

            Vector3 redirectedFwd = Quaternion.AngleAxis(currentStrafeYaw, transform.up) * transform.forward;
            Vector3 poleDir = (redirectedFwd + transform.right * (sideSign * 0.22f)).normalized;

            SolveTwoBoneIKWorld(thigh, shin, ankle, targetPos, poleDir);
        }

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
            if (dist < 0.001f) return;

            Vector3 dir = toTarget / dist;
            float minReach = Mathf.Abs(l1 - l2) + 0.01f;
            float maxReach = (l1 + l2) - 0.01f;
            float clampedDist = Mathf.Clamp(dist, minReach, maxReach);
            Vector3 clampedTarget = a + dir * clampedDist;

            Vector3 poleProj = Vector3.ProjectOnPlane(poleForward, dir);
            Vector3 poleUnit = poleProj.sqrMagnitude > 0.0001f ? poleProj.normalized : Vector3.Cross(dir, Vector3.right).normalized;
            Vector3 animKneeDir = Vector3.ProjectOnPlane(b - a, dir);
            Vector3 bendDir = Vector3.ProjectOnPlane(animKneeDir * 0.65f + poleUnit * (l1 * 0.35f), dir);
            if (bendDir.sqrMagnitude < 0.0001f) bendDir = poleProj;
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

        private void UpdateStateMachine(float dt)
        {
            Vector3 toTarget = targetJaeger.transform.position - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            Vector3 dirToTarget = dist > 0.01f ? (toTarget / dist) : transform.forward;
            float targetYaw = Mathf.Atan2(dirToTarget.x, dirToTarget.z) * Mathf.Rad2Deg;

            isStrafingInRange = false;

            switch (currentState)
            {
                case AIState.Idle:
                    isPursuitSprinting = false;
                    desiredVelocity = Vector3.zero;
                    SetLocomotionSpeed(1f);
                    CrossFadeState("Idle", 0.2f);
                    if (dist <= detectionRange)
                    {
                        if (IsBaguvixActive)
                            StartSizingUp(Random.Range(3.0f, 5.0f));
                        else
                            currentState = AIState.ChasingAndStrafing;
                    }
                    break;

                case AIState.ChasingAndStrafing:
                    RotateYawToward(targetYaw, turnSpeed, dt);

                    if (IsBaguvixActive)
                    {
                        if (dist < 19.5f)
                        {
                            StartRetreat(useSprint: dist < 15.5f, duration: Random.Range(1.8f, 2.8f));
                        }
                        else
                        {
                            StartSizingUp(Random.Range(3.0f, 5.0f));
                        }
                        break;
                    }

                    if (isSlattern)
                    {
                        UpdateSlatternChasingState(dt, dist, dirToTarget);
                        break;
                    }

                    if (empCooldownTimer <= 0f && !IsEmpBuffed && dist >= 18.0f && dist <= 140f)
                    {
                        StartEmpRoar();
                        break;
                    }

                    // Anti-overlap retreat step if Gipsy crowds into chest
                    if (dist < 15.8f)
                    {
                        desiredVelocity = (-dirToTarget * backwardWalkSpeed) + transform.right * (circleStrafeDir * strafeSpeed * 0.55f);
                        SetLocomotionSpeed(-1.05f);
                        CrossFadeState("Walk", 0.14f);
                        isStrafingInRange = true;
                        break;
                    }

                    // Occasionally back off & circle-stalk to size up the player like a God of War duel
                    if (dist <= 27f && dist >= 16.5f && sizingUpCooldown <= 0f && postAttackPacingTimer <= 0.1f && Random.value < 0.40f)
                    {
                        StartSizingUp();
                        break;
                    }

                    if (dist >= chaseSprintThreshold)
                    {
                        isPursuitSprinting = true;
                    }
                    else if (dist <= chaseSprintStopDistance)
                    {
                        isPursuitSprinting = false;
                    }

                    if (dist > meleeAttackRange)
                    {
                        bool shouldSprint = isPursuitSprinting || (canSprint && dist > sprintMinRange && (IsEmpBuffed || dist > 36f));
                        if (shouldSprint)
                        {
                            float spd = IsEmpBuffed ? empSprintSpeed : sprintSpeed;
                            desiredVelocity = transform.forward * spd;
                            SetLocomotionSpeed(IsEmpBuffed ? 1.12f : 1.0f);
                            CrossFadeState("Run", 0.16f);
                        }
                        else
                        {
                            float spd = IsEmpBuffed ? empWalkSpeed : walkSpeed;
                            desiredVelocity = (transform.forward + transform.right * (circleStrafeDir * 0.28f)).normalized * spd;
                            SetLocomotionSpeed(IsEmpBuffed ? 1.15f : 1.0f);
                            CrossFadeState("Walk", 0.16f);
                        }
                    }
                    else
                    {
                        if (postAttackPacingTimer > 0f)
                        {
                            float spd = strafeSpeed * (IsEmpBuffed ? 1.20f : 1.0f);
                            float radialAdjust = dist < minCombatDistance ? -0.60f : (dist > meleeAttackRange - 1.5f ? 0.40f : 0f);
                            desiredVelocity = (transform.right * circleStrafeDir + transform.forward * radialAdjust).normalized * spd;
                            SetLocomotionSpeed(radialAdjust < -0.1f ? -0.9f : 1.0f);
                            CrossFadeState("Walk", 0.16f);
                            isStrafingInRange = true;
                        }
                        else
                        {
                            StartMeleeAttack(dist);
                        }
                    }
                    break;

                case AIState.SizingUp:
                    RotateYawToward(targetYaw, turnSpeed * 1.1f, dt);
                    stateTimer -= dt;
                    isStrafingInRange = true;

                    float targetCircleDist = Mathf.Clamp(dist, 21f, 29f);
                    float radialBias = 0f;
                    if (dist < 20.5f) radialBias = -0.65f;
                    else if (dist > 30f) radialBias = 0.55f;

                    float circleSpd = strafeSpeed * (IsEmpBuffed ? 1.25f : 1.05f);
                    desiredVelocity = (transform.right * circleStrafeDir + transform.forward * radialBias).normalized * circleSpd;
                    SetLocomotionSpeed(radialBias < -0.15f ? -1.0f : 1.05f);
                    CrossFadeState("Walk", 0.16f);

                    if (IsBaguvixActive)
                    {
                        // Pacifist fallback: if player gets close, retreat instead of attacking
                        if (dist < 18.5f)
                        {
                            StartRetreat(useSprint: dist < 15.0f, duration: Random.Range(1.8f, 2.8f));
                        }
                        else if (stateTimer <= 0f)
                        {
                            circleStrafeDir = -circleStrafeDir;
                            stateTimer = Random.Range(2.5f, 4.5f);
                        }
                        break;
                    }

                    if (isSlattern)
                    {
                        if (CurrentPhase == 3 && dist <= 44f)
                        {
                            currentState = AIState.ChasingAndStrafing;
                            postAttackPacingTimer = 0.15f;
                            break;
                        }
                        if (dist < 24f && slatternBlitzCooldown > 0f)
                        {
                            StartRetreat(useSprint: true, duration: Random.Range(1.2f, 1.8f), customTargetDist: Random.Range(36f, 44f));
                            break;
                        }
                        if (stateTimer <= 0f || (dist <= 52f && slatternBlitzCooldown <= 0f))
                        {
                            if (CurrentPhase == 3)
                            {
                                currentState = AIState.ChasingAndStrafing;
                            }
                            else if (CurrentPhase == 2)
                            {
                                StartAttackPattern(AttackPattern.DeviousBlitzLightCombo);
                            }
                            else
                            {
                                StartAttackPattern(AttackPattern.AbyssalDartStrike);
                            }
                        }
                        break;
                    }

                    // Punish reckless rush or explode out of the stalk into a charging Slam
                    if (dist < 17.5f)
                    {
                        StartAttackPattern(Random.value < 0.55f ? AttackPattern.HeavySlam : AttackPattern.DoubleHookCombo);
                    }
                    else if (stateTimer <= 0f)
                    {
                        sizingUpCooldown = Random.Range(4.5f, 7.5f);
                        if (dist > 22.5f)
                        {
                            StartAttackPattern(AttackPattern.SprintChargeSlam);
                        }
                        else
                        {
                            currentState = AIState.ChasingAndStrafing;
                            postAttackPacingTimer = 0f;
                        }
                    }
                    break;

                case AIState.Retreating:
                    RotateYawToward(targetYaw, turnSpeed * 1.15f, dt);
                    stateTimer -= dt;

                    float backSpd = isSlattern
                        ? (backwardWalkSpeed * 1.35f)
                        : (retreatWithSprint ? backwardSprintSpeed : backwardWalkSpeed);
                    desiredVelocity = (-dirToTarget * backSpd) + transform.right * (circleStrafeDir * strafeSpeed * 0.35f);
                    SetLocomotionSpeed(isSlattern ? -1.12f : (retreatWithSprint ? -1.18f : -1.02f));
                    CrossFadeState(isSlattern ? "Walk" : (retreatWithSprint ? "Run" : "Walk"), 0.14f);

                    if (stateTimer <= 0f || dist >= retreatTargetDist)
                    {
                        if (IsBaguvixActive)
                        {
                            StartSizingUp(Random.Range(2.5f, 4.0f));
                        }
                        else if (isSlattern)
                        {
                            if (CurrentPhase == 3)
                            {
                                currentState = AIState.ChasingAndStrafing;
                                postAttackPacingTimer = 0.25f;
                            }
                            else
                            {
                                currentState = AIState.ChasingAndStrafing;
                                postAttackPacingTimer = Random.Range(0.4f, 0.8f);
                            }
                        }
                        else if (CurrentPhase >= 2 && Random.value < 0.50f)
                        {
                            StartAttackPattern(AttackPattern.SprintChargeSlam);
                        }
                        else
                        {
                            StartSizingUp(Random.Range(1.4f, 2.3f));
                        }
                    }
                    break;

                case AIState.EmpRoar:
                    RotateYawToward(targetYaw, attackTurnSpeed * 0.6f, dt);
                    desiredVelocity = Vector3.zero;
                    SetLocomotionSpeed(1f);
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        if (IsAIDisabled)
                        {
                            isDebugForcedActionActive = false;
                            currentState = AIState.Idle;
                            CrossFadeState("Idle", 0.2f);
                        }
                        else
                        {
                            currentState = AIState.ChasingAndStrafing;
                        }
                    }
                    break;

                case AIState.PhaseTransition:
                    RotateYawToward(targetYaw, turnSpeed, dt);
                    stateTimer -= dt;
                    break;

                case AIState.Attacking:
                    if (IsBaguvixActive && !isDebugForcedActionActive)
                    {
                        CancelActiveSequence();
                        StartSizingUp(Random.Range(2.5f, 4.0f));
                        break;
                    }

                    bool isInChargeRun = activePattern == AttackPattern.SprintChargeSlam ||
                                         activePattern == AttackPattern.AbyssalDartStrike ||
                                         activePattern == AttackPattern.DeviousBlitzLightCombo;
                    if (!isInChargeRun)
                    {
                        if (isSlattern) RotateYawToward(targetYaw, attackTurnSpeed, dt);
                        float lungeSpeed = (dist > minCombatDistance + 0.5f)
                            ? (isSlattern ? Mathf.Clamp((dist - minCombatDistance) * 1.8f, walkSpeed * 0.45f, walkSpeed * 1.05f) : walkSpeed * 0.32f)
                            : 0f;
                        desiredVelocity = transform.forward * lungeSpeed;
                    }
                    break;

                case AIState.Blocking:
                    RotateYawToward(targetYaw, turnSpeed * 0.85f, dt);
                    desiredVelocity = dist < minCombatDistance ? (-dirToTarget * 3.0f) : Vector3.zero;
                    SetLocomotionSpeed(dist < minCombatDistance ? -0.8f : 1f);
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        blockEnterFast = false;
                        if (IsAIDisabled)
                        {
                            isDebugForcedActionActive = false;
                            counterQueued = false;
                            currentState = AIState.Idle;
                            CrossFadeState("Idle", 0.2f);
                        }
                        else if (IsBaguvixActive)
                        {
                            counterQueued = false;
                            StartRetreat(false, 1.5f);
                        }
                        else if (counterQueued)
                        {
                            counterQueued = false;
                            StartCounterAttack();
                        }
                        else
                        {
                            currentState = AIState.ChasingAndStrafing;
                            postAttackPacingTimer = 0.25f;
                        }
                    }
                    break;

                case AIState.Staggered:
                    desiredVelocity = Vector3.zero;
                    SetLocomotionSpeed(1f);
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        currentPoise = maxPoise;
                        isGuardBrokenStagger = false;
                        movingGuardLightHits = 0;
                        if (IsAIDisabled)
                        {
                            isDebugForcedActionActive = false;
                            currentState = AIState.Idle;
                            CrossFadeState("Idle", 0.2f);
                        }
                        else if (IsBaguvixActive)
                        {
                            StartSizingUp(Random.Range(2.5f, 4.0f));
                        }
                        else if (isSlattern && CurrentPhase == 3)
                        {
                            consecutiveStaggers = 0;
                            currentState = AIState.ChasingAndStrafing;
                            postAttackPacingTimer = 0.25f;
                        }
                        else if (isSlattern && CurrentPhase < 3)
                        {
                            consecutiveStaggers = 0;
                            StartRetreat(useSprint: true, duration: Random.Range(1.25f, 1.85f), customTargetDist: Random.Range(35f, 43f));
                        }
                        else if (consecutiveStaggers >= 2 && Random.value < 0.55f)
                        {
                            consecutiveStaggers = 0;
                            StartRetreat(useSprint: true, duration: Random.Range(1.0f, 1.4f));
                        }
                        else
                        {
                            currentState = AIState.ChasingAndStrafing;
                            postAttackPacingTimer = 0.35f;
                        }
                    }
                    break;
            }
        }

        private void UpdateSlatternChasingState(float dt, float dist, Vector3 dirToTarget)
        {
            if (CurrentPhase == 1)
            {
                // Phase 1: Distance fighter - stalks and circles Gipsy from afar or in the murk (33-46m), darting in on cooldown
                if (dist < 29f && slatternBlitzCooldown > 0f)
                {
                    StartRetreat(useSprint: dist < 24f, duration: Random.Range(1.35f, 2.1f), customTargetDist: Random.Range(38f, 46f));
                    return;
                }

                if (slatternBlitzCooldown <= 0f && dist <= 54f)
                {
                    StartAttackPattern(AttackPattern.AbyssalDartStrike);
                    return;
                }

                float radial = 0f;
                if (dist < slatternStalkMinDist) radial = -0.70f;
                else if (dist > 75f) radial = 0.45f;
                else if (dist > 54f) radial = 0.25f;
                else if (dist > slatternStalkIdealDist) radial = 0.65f;

                float spd = strafeSpeed * (dist > 54f ? 1.25f : (radial > 0.3f ? 1.20f : 1.05f));
                desiredVelocity = (transform.right * circleStrafeDir + transform.forward * radial).normalized * spd;
                bool isMovingBackward = radial < -0.05f || Vector3.Dot(desiredVelocity, transform.forward) < -0.05f;
                SetLocomotionSpeed(isMovingBackward ? -1.05f : 1.05f);
                CrossFadeState("Walk", 0.16f);
                isStrafingInRange = true;
            }
            else if (CurrentPhase == 2)
            {
                // Phase 2: Parry Blitzer - circles from afar or keeps distance (28-42m), blitzes in with a 4-hit light combo
                if (dist < 25.5f && slatternBlitzCooldown > 0f)
                {
                    StartRetreat(useSprint: false, duration: Random.Range(1.1f, 1.65f), customTargetDist: Random.Range(34f, 42f));
                    return;
                }

                if (slatternBlitzCooldown <= 0f && dist <= 48f)
                {
                    StartAttackPattern(AttackPattern.DeviousBlitzLightCombo);
                    return;
                }

                float radial = 0f;
                if (dist < 30f) radial = -0.65f;
                else if (dist > 70f) radial = 0.50f;
                else if (dist > 48f) radial = 0.28f;
                else if (dist > 40f) radial = 0.70f;

                float spd = strafeSpeed * 1.38f;
                desiredVelocity = (transform.right * circleStrafeDir + transform.forward * radial).normalized * spd;
                bool isMovingBackward = radial < -0.05f || Vector3.Dot(desiredVelocity, transform.forward) < -0.05f;
                SetLocomotionSpeed(isMovingBackward ? -1.12f : 1.18f);
                CrossFadeState("Walk", 0.14f);
                isStrafingInRange = true;
            }
            else
            {
                // Phase 3: Moving-Guard Juggernaut - circles from afar while closing in, then relentlessly advances
                float slatternStrikeTriggerDist = Mathf.Min(meleeAttackRange - 3.8f, minCombatDistance + 2.0f);
                if (dist <= slatternStrikeTriggerDist && postAttackPacingTimer <= 0f)
                {
                    StartSlatternPhase3Attack();
                    return;
                }

                if (dist < minCombatDistance - 2.2f)
                {
                    desiredVelocity = (-dirToTarget * (backwardWalkSpeed * 0.75f)) + transform.right * (circleStrafeDir * strafeSpeed * 0.5f);
                    SetLocomotionSpeed(-1.05f);
                    CrossFadeState("Walk", 0.14f);
                    isStrafingInRange = true;
                    return;
                }

                float marchSpeed = walkSpeed * 1.22f;
                if (dist > 44f)
                {
                    desiredVelocity = (transform.right * (circleStrafeDir * 0.85f) + transform.forward * 0.55f).normalized * marchSpeed;
                    isStrafingInRange = true;
                }
                else
                {
                    desiredVelocity = (transform.forward + transform.right * (circleStrafeDir * 0.20f)).normalized * marchSpeed;
                }
                SetLocomotionSpeed(1.18f);
                CrossFadeState("Walk", 0.14f);
            }
        }

        private void SetLocomotionSpeed(float spd)
        {
            if (Mathf.Approximately(currentLocomotionSpeed, spd)) return;
            currentLocomotionSpeed = spd;
            if (animator != null && hasLocomotionSpeedParam)
            {
                animator.SetFloat(LocomotionSpeedHash, spd);
            }
        }

        private void RotateYawToward(float targetYaw, float maxDegPerSec, float dt)
        {
            currentBodyYaw = Mathf.MoveTowardsAngle(currentBodyYaw, targetYaw, maxDegPerSec * dt);
            transform.rotation = Quaternion.Euler(0f, currentBodyYaw, 0f);
        }

        private void CrossFadeState(string stateName, float fadeDuration = 0.16f, bool forceRestart = false)
        {
            if (animator == null) return;
            if (!forceRestart && currentAnimState == stateName) return;

            currentAnimState = stateName;
            // Start near the end of Walk when playing backwards so reverse locomotion begins without a seam hitch
            float fixedTimeOffset = (stateName == "Walk" && currentLocomotionSpeed < 0f)
                ? (isSlattern ? 4.75f : 3.95f)
                : 0f;
            animator.CrossFadeInFixedTime(stateName, fadeDuration, 0, fixedTimeOffset);
        }

        private void CancelActiveSequence()
        {
            if (activeSequenceCoroutine != null)
            {
                StopCoroutine(activeSequenceCoroutine);
                activeSequenceCoroutine = null;
            }
            activePattern = AttackPattern.None;
            hasHyperArmor = false;
        }

        private void StartSizingUp(float customDuration = -1f)
        {
            CancelActiveSequence();
            currentState = AIState.SizingUp;
            stateTimer = customDuration > 0f ? customDuration : Random.Range(1.6f, 2.8f);
            circleStrafeDir = Random.value > 0.5f ? 1f : -1f;
        }

        private void StartRetreat(bool useSprint, float duration, float customTargetDist = -1f)
        {
            CancelActiveSequence();
            currentState = AIState.Retreating;
            retreatWithSprint = !isSlattern && canSprint && useSprint;
            stateTimer = duration;
            retreatTargetDist = customTargetDist > 0f
                ? customTargetDist
                : (retreatWithSprint ? Random.Range(28f, 35f) : Random.Range(24f, 29f));
        }

        public void TriggerVictoryRoar()
        {
            if (IsDead) return;
            hasCelebratedPlayerDefeat = true;
            CancelActiveSequence();
            currentState = AIState.EmpRoar;
            stateTimer = EmpClipLength + 1.5f;
            desiredVelocity = Vector3.zero;
            hasHyperArmor = true;

            SetLocomotionSpeed(1f);
            CrossFadeState("EMP", 0.16f, true);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuEmpRoar(transform.position + Vector3.up * 14f);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayEmpChargeUp(transform.position + Vector3.up * 12f);
            activeSequenceCoroutine = StartCoroutine(EmpShockwaveRoutine());
            Debug.Log("<color=#00e5ff><b>[Leatherback Boss]</b></color> JAEGER DESTROYED! Unleashing Victory BIO-EMP Blast!");
        }

        private void StartEmpRoar()
        {
            if (IsBaguvixActive)
            {
                StartSizingUp(Random.Range(2.5f, 4.0f));
                return;
            }

            CancelActiveSequence();
            currentState = AIState.EmpRoar;
            stateTimer = EmpClipLength;
            empCooldownTimer = empCooldownDuration;
            desiredVelocity = Vector3.zero;
            hasHyperArmor = true;

            SetLocomotionSpeed(1f);
            CrossFadeState("EMP", 0.16f, true);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuEmpRoar(transform.position + Vector3.up * 14f);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayEmpChargeUp(transform.position + Vector3.up * 12f);
            activeSequenceCoroutine = StartCoroutine(EmpShockwaveRoutine());
            Debug.Log("<color=#00e5ff><b>[Leatherback Boss]</b></color> Initiating BIO-EMP Roar & Overcharge!");
        }

        private IEnumerator EmpShockwaveRoutine()
        {
            yield return new WaitForSeconds(0.95f);
            if (IsDead) yield break;

            empBuffEndTime = Time.time + empBuffDuration;
            Vector3 center = transform.position + transform.forward * 4f;
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayEmpDischargeBlast(center + Vector3.up * 10f);
            PacificRimAudio.PacificRimAudioManager.Instance?.SetKaijuEmpSparksLoop(true, transform.position + Vector3.up * 14f);
            EmitOceanImpact(center, transform.forward, 32f, 2.8f, 3.2f, true, 1.4f);

            Camera cam = Camera.main;
            if (cam != null)
            {
                var shaker = cam.GetComponent<PacificRimCameraShake>();
                if (shaker != null) shaker.TriggerTurbineOverchargeFeedback();
            }
        }

        private void StartPhaseTransition(int targetPhase)
        {
            CancelActiveSequence();
            CurrentPhase = targetPhase;
            currentState = AIState.PhaseTransition;
            hasHyperArmor = true;
            consecutiveStaggers = 0;
            currentPoise = maxPoise;

            if (targetPhase == 3)
            {
                currentHealth = Mathf.Min(maxHealth, currentHealth + phase3HealAmount);
                empBuffEndTime = Time.time + 999f;
            }
            else
            {
                empBuffEndTime = Time.time + empBuffDuration;
            }

            stateTimer = 1.0f + (EmpClipLength * 0.8f);
            activeSequenceCoroutine = StartCoroutine(PhaseTransitionRoutine(targetPhase));
            if (isSlattern)
            {
                string msg = targetPhase == 2
                    ? "SLATTERN PHASE II // BLITZER PARRY MODE ENGAGED!"
                    : "SLATTERN PHASE III // MOVING-GUARD JUGGERNAUT (HEAVY TO BREAK!)";
                PacificRimUI.JaegerHUD.Instance?.ShowBanner(msg, 3.0f);
            }
            Debug.Log($"<color=#ff3333><b>[{kaijuDisplayName} PHASE {targetPhase}]</b></color> Phase transition triggered!");
        }

        private IEnumerator PhaseTransitionRoutine(int phase)
        {
            // Phase disengage: rapid backward disengage to reset range (skip backward retreat for Slattern P3 Tank)
            if (!(isSlattern && phase == 3))
            {
                retreatWithSprint = !isSlattern && canSprint;
                SetLocomotionSpeed(isSlattern ? -1.15f : (retreatWithSprint ? -1.22f : -1.05f));
                CrossFadeState(isSlattern ? "Walk" : (retreatWithSprint ? "Run" : "Walk"), 0.12f, true);

                float backElapsed = 0f;
                float backSpeed = isSlattern
                    ? (backwardWalkSpeed * 1.35f)
                    : (retreatWithSprint ? backwardSprintSpeed * 1.1f : backwardWalkSpeed * 1.15f);
                while (backElapsed < 0.95f && currentState == AIState.PhaseTransition)
                {
                    desiredVelocity = -transform.forward * backSpeed;
                    backElapsed += Time.deltaTime;
                    yield return null;
                }
            }

            if (IsDead || currentState != AIState.PhaseTransition) yield break;

            desiredVelocity = Vector3.zero;
            SetLocomotionSpeed(1f);
            CrossFadeState("EMP", 0.14f, true);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuEmpRoar(transform.position + Vector3.up * 14f);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayEmpChargeUp(transform.position + Vector3.up * 12f);

            yield return new WaitForSeconds(0.75f);
            if (IsDead || currentState != AIState.PhaseTransition) yield break;

            Vector3 pulsePos = transform.position + transform.forward * 4f;
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayEmpDischargeBlast(pulsePos + Vector3.up * 10f);
            PacificRimAudio.PacificRimAudioManager.Instance?.SetKaijuEmpSparksLoop(true, transform.position + Vector3.up * 14f);
            EmitOceanImpact(pulsePos, Vector3.up, 36f, 3.2f, 3.4f, true, 1.8f);
            WaterSplashManager.Instance?.SpawnCombatSparks(pulsePos + Vector3.up * 12f, Vector3.up, false, true, true);

            yield return new WaitForSeconds(EmpClipLength * 0.55f);
            if (IsDead || currentState != AIState.PhaseTransition) yield break;

            hasHyperArmor = false;
            if (isSlattern)
            {
                if (phase == 2)
                {
                    slatternBlitzCooldown = 0.5f;
                    StartAttackPattern(AttackPattern.DeviousBlitzLightCombo);
                }
                else
                {
                    movingGuardLightHits = 0;
                    currentState = AIState.ChasingAndStrafing;
                    postAttackPacingTimer = 0.15f;
                }
            }
            else if (phase >= 2)
            {
                StartAttackPattern(canSprint ? AttackPattern.SprintChargeSlam : AttackPattern.HeavySlam);
            }
            else
            {
                currentState = AIState.ChasingAndStrafing;
            }
        }

        /// <summary>
        /// Called by SimpleJaegerController as soon as the player starts an attack swing so Leatherback
        /// can anticipate, raise an armored fore-guard, and punish button-mashing.
        /// </summary>
        public void NotifyPlayerAttackStarted(int attackVariant, bool isHeavy)
        {
            if (IsDead || IsAIDisabled || currentState == AIState.PhaseTransition || currentState == AIState.EmpRoar) return;
            if (currentState == AIState.Blocking || currentState == AIState.Staggered) return;
            if (isCutsceneMode)
            {
                desiredVelocity = Vector3.zero;
                SetLocomotionSpeed(0f);
                CrossFadeState("Idle", 0.25f);
                return;
            }

            if (targetJaeger == null || targetJaeger.IsDead) return;

            if (IsBaguvixActive)
            {
                // When player attacks in baguvix mode, kaiju simply retreats or steps back peacefully
                if (currentState != AIState.Retreating)
                {
                    StartRetreat(useSprint: true, duration: Random.Range(1.5f, 2.5f));
                }
                return;
            }

            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(targetJaeger.transform.position.x, 0f, targetJaeger.transform.position.z));

            if (Time.time - lastPlayerAttackNotifyTime <= 2.6f)
            {
                playerSpamCount++;
            }
            else
            {
                playerSpamCount = 1;
            }
            lastPlayerAttackNotifyTime = Time.time;

            if (dist > 26.5f) return;

            if (isSlattern)
            {
                // Slattern relies on on-hit parries in P1/P2 and Moving Guard in P3 rather than pre-hit dodging
                return;
            }

            bool canAnticipate = currentState == AIState.ChasingAndStrafing ||
                                 currentState == AIState.SizingUp ||
                                 currentState == AIState.Retreating;
            if (!canAnticipate) return;

            float baseChance = CurrentPhase == 1 ? 0.38f : (CurrentPhase == 2 ? 0.52f : 0.68f);
            float spamBonus = Mathf.Max(0, playerSpamCount - 1) * 0.26f;
            float blockChance = Mathf.Min(0.95f, baseChance + spamBonus);

            if (playerSpamCount >= 4 || Random.value < blockChance)
            {
                StartBlock(fastReaction: true);
            }
        }

        private void StartMeleeAttack(float distToTarget)
        {
            if (IsBaguvixActive)
            {
                StartSizingUp(Random.Range(2.5f, 4.0f));
                return;
            }

            AttackPattern chosen;
            float roll = Random.value;

            if (targetJaeger != null && targetJaeger.isBlocking)
            {
                chosen = roll < 0.60f ? AttackPattern.HeavySlam : (CurrentPhase >= 2 ? AttackPattern.TripleBrawlerCombo : AttackPattern.DoubleHookCombo);
            }
            else if (CurrentPhase >= 2)
            {
                if (roll < 0.22f) chosen = AttackPattern.RightHook;
                else if (roll < 0.45f) chosen = AttackPattern.DoubleHookCombo;
                else if (roll < 0.78f) chosen = AttackPattern.TripleBrawlerCombo;
                else chosen = AttackPattern.HeavySlam;
            }
            else
            {
                if (roll < 0.22f) chosen = AttackPattern.LeftHook;
                else if (roll < 0.44f) chosen = AttackPattern.RightHook;
                else if (roll < 0.76f) chosen = AttackPattern.DoubleHookCombo;
                else chosen = AttackPattern.HeavySlam;
            }

            StartAttackPattern(chosen);
        }

        private void StartSlatternPhase3Attack()
        {
            if (IsBaguvixActive)
            {
                StartSizingUp(Random.Range(2.5f, 4.0f));
                return;
            }

            // Phase 3 Juggernaut drops Moving Guard to swing: catches the player if careless,
            // or leaves Slattern vulnerable to being caught off-guard mid-swing (hasHyperArmor = false on combos).
            float roll = Random.value;
            AttackPattern chosen = roll < 0.38f
                ? AttackPattern.DoubleHookCombo
                : (roll < 0.72f ? AttackPattern.TripleBrawlerCombo : AttackPattern.HeavySlam);

            CancelActiveSequence();
            currentState = AIState.Attacking;
            activePattern = chosen;
            hasHyperArmor = false;
            SetLocomotionSpeed(1f);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuAttackGrunt(transform.position + Vector3.up * 14f);
            activeSequenceCoroutine = StartCoroutine(ExecuteAttackSequence(chosen));
        }

        private void StartCounterAttack()
        {
            if (IsBaguvixActive)
            {
                StartRetreat(false, 1.5f);
                return;
            }

            AttackPattern counter;
            float r = Random.value;
            if (CurrentPhase >= 2 && r < 0.40f) counter = AttackPattern.TripleBrawlerCombo;
            else if (r < 0.72f) counter = AttackPattern.HeavySlam;
            else counter = AttackPattern.RightHook;

            StartAttackPattern(counter, hyperArmored: true);
        }

        private void StartAttackPattern(AttackPattern pattern, bool hyperArmored = false)
        {
            if (IsBaguvixActive)
            {
                StartSizingUp(Random.Range(2.5f, 4.0f));
                return;
            }

            CancelActiveSequence();
            currentState = AIState.Attacking;
            activePattern = pattern;
            hasHyperArmor = isSlattern
                ? isParryCountering
                : (hyperArmored || pattern == AttackPattern.HeavySlam || pattern == AttackPattern.SprintChargeSlam);
            SetLocomotionSpeed(1f);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuAttackGrunt(transform.position + Vector3.up * 14f);
            activeSequenceCoroutine = StartCoroutine(ExecuteAttackSequence(pattern));
        }

        private IEnumerator ExecuteAttackSequence(AttackPattern pattern)
        {
            float spdScale = IsEmpBuffed ? (CurrentPhase == 3 ? 1.28f : 1.20f) : 1.0f;
            float closeInTargetDist = isSlattern ? Mathf.Min(meleeAttackRange - 4.0f, minCombatDistance + 1.8f) : (meleeAttackRange - 1.0f);

            float FlatDistToTarget()
            {
                if (targetJaeger == null) return 999f;
                Vector3 d = targetJaeger.transform.position - transform.position;
                d.y = 0f;
                return d.magnitude;
            }

            float leftHitTime = PunchLeftImpactTime;
            float rightHitTime = PunchRightImpactTime;
            float slamHitTime = SlamImpactTime;
            float punchClipLen = ActivePunchClipLength;
            float slamClipLen = ActiveSlamClipLength;

            switch (pattern)
            {
                case AttackPattern.LeftHook:
                    CrossFadeState("PunchLeft", 0.12f, true);
                    yield return new WaitForSeconds(leftHitTime / spdScale);
                    if (currentState != AIState.Attacking) yield break;
                    ExecuteStrikeImpact(handL, transform.position + transform.forward * 12f + Vector3.up * 12f, lightPunchDamage, punchStrikeRadius, false, 20f);
                    yield return new WaitForSeconds(Mathf.Max(0.05f, (punchClipLen - leftHitTime) / spdScale));
                    break;

                case AttackPattern.RightHook:
                    CrossFadeState("PunchRight", 0.12f, true);
                    yield return new WaitForSeconds(rightHitTime / spdScale);
                    if (currentState != AIState.Attacking) yield break;
                    ExecuteStrikeImpact(handR, transform.position + transform.forward * 12f + Vector3.up * 12f, lightPunchDamage, punchStrikeRadius, false, 20f);
                    yield return new WaitForSeconds(Mathf.Max(0.05f, (punchClipLen - rightHitTime) / spdScale));
                    break;

                case AttackPattern.DoubleHookCombo:
                    CrossFadeState("PunchLeft", 0.12f, true);
                    yield return new WaitForSeconds(leftHitTime / spdScale);
                    if (currentState != AIState.Attacking) yield break;
                    ExecuteStrikeImpact(handL, transform.position + transform.forward * 12f + Vector3.up * 12f, lightPunchDamage * 0.85f, punchStrikeRadius, false, 14f);

                    yield return new WaitForSeconds(0.12f / spdScale);
                    if (currentState != AIState.Attacking) yield break;

                    CrossFadeState("PunchRight", 0.10f, true);
                    yield return new WaitForSeconds(rightHitTime / spdScale);
                    if (currentState != AIState.Attacking) yield break;
                    ExecuteStrikeImpact(handR, transform.position + transform.forward * 12f + Vector3.up * 12f, lightPunchDamage, punchStrikeRadius, false, 24f);
                    yield return new WaitForSeconds(Mathf.Max(0.05f, (punchClipLen - rightHitTime) / spdScale));
                    break;

                case AttackPattern.TripleBrawlerCombo:
                    CrossFadeState("PunchLeft", 0.12f, true);
                    yield return new WaitForSeconds(leftHitTime / spdScale);
                    if (currentState != AIState.Attacking) yield break;
                    ExecuteStrikeImpact(handL, transform.position + transform.forward * 12f + Vector3.up * 12f, lightPunchDamage * 0.80f, punchStrikeRadius, false, 14f);

                    yield return new WaitForSeconds(0.12f / spdScale);
                    if (currentState != AIState.Attacking) yield break;
                    CrossFadeState("PunchRight", 0.10f, true);
                    yield return new WaitForSeconds(rightHitTime / spdScale);
                    if (currentState != AIState.Attacking) yield break;
                    ExecuteStrikeImpact(handR, transform.position + transform.forward * 12f + Vector3.up * 12f, lightPunchDamage * 0.90f, punchStrikeRadius, false, 18f);

                    yield return new WaitForSeconds(0.14f / spdScale);
                    if (currentState != AIState.Attacking) yield break;
                    if (!isSlattern) hasHyperArmor = true;
                    CrossFadeState("Slam", 0.12f, true);
                    yield return new WaitForSeconds(slamHitTime / spdScale);
                    if (currentState != AIState.Attacking) yield break;
                    ExecuteStrikeImpact(null, GetSlamCenter(), heavySlamDamage, slamStrikeRadius, true, 34f);
                    yield return new WaitForSeconds(Mathf.Max(0.05f, (slamClipLen - slamHitTime) / spdScale));
                    break;

                case AttackPattern.HeavySlam:
                    CrossFadeState("Slam", 0.14f, true);
                    yield return new WaitForSeconds(slamHitTime / spdScale);
                    if (currentState != AIState.Attacking) yield break;
                    ExecuteStrikeImpact(null, GetSlamCenter(), heavySlamDamage, slamStrikeRadius, true, 34f);
                    yield return new WaitForSeconds(Mathf.Max(0.05f, (slamClipLen - slamHitTime) / spdScale));
                    break;

                case AttackPattern.SprintChargeSlam:
                    SetLocomotionSpeed(canSprint ? (IsEmpBuffed ? 1.15f : 1.05f) : 1.0f);
                    CrossFadeState(canSprint ? "Run" : "Walk", 0.14f, true);
                    float chargeElapsed = 0f;
                    float chargeStopDist = isSlattern ? closeInTargetDist : 20.5f;
                    while (chargeElapsed < 3.8f && currentState == AIState.Attacking && targetJaeger != null)
                    {
                        Vector3 toT = targetJaeger.transform.position - transform.position;
                        toT.y = 0f;
                        float d = toT.magnitude;
                        if (isSlattern && d > 0.1f)
                        {
                            RotateYawToward(Mathf.Atan2(toT.x, toT.z) * Mathf.Rad2Deg, turnSpeed * 1.25f, Time.deltaTime);
                        }
                        desiredVelocity = transform.forward * (canSprint ? (IsEmpBuffed ? empSprintSpeed : sprintSpeed) : (IsEmpBuffed ? empWalkSpeed : walkSpeed));
                        if (d <= chargeStopDist) break;
                        chargeElapsed += Time.deltaTime;
                        yield return null;
                    }
                    if (currentState != AIState.Attacking) yield break;
                    if (FlatDistToTarget() > meleeAttackRange + 2.0f)
                    {
                        activePattern = AttackPattern.None;
                        hasHyperArmor = false;
                        currentState = AIState.ChasingAndStrafing;
                        yield break;
                    }

                    SetLocomotionSpeed(1f);
                    desiredVelocity = transform.forward * (walkSpeed * 0.45f);
                    CrossFadeState("Slam", 0.12f, true);
                    yield return new WaitForSeconds(slamHitTime / spdScale);
                    if (currentState != AIState.Attacking) yield break;
                    ExecuteStrikeImpact(null, GetSlamCenter(), heavySlamDamage * 1.10f, slamStrikeRadius, true, 38f);
                    yield return new WaitForSeconds(Mathf.Max(0.05f, (slamClipLen - slamHitTime) / spdScale));
                    break;

                case AttackPattern.AbyssalDartStrike:
                    SetLocomotionSpeed(1.12f);
                    CrossFadeState("Run", 0.12f, true);
                    float dartElapsed = 0f;
                    while (dartElapsed < 4.2f && currentState == AIState.Attacking && targetJaeger != null)
                    {
                        Vector3 toT = targetJaeger.transform.position - transform.position;
                        toT.y = 0f;
                        float d = toT.magnitude;
                        if (d > 0.1f) RotateYawToward(Mathf.Atan2(toT.x, toT.z) * Mathf.Rad2Deg, turnSpeed * 1.4f, Time.deltaTime);
                        desiredVelocity = transform.forward * (empSprintSpeed * 1.28f);
                        if (d <= closeInTargetDist) break;
                        dartElapsed += Time.deltaTime;
                        yield return null;
                    }
                    if (currentState != AIState.Attacking) yield break;
                    float dNow = FlatDistToTarget();
                    if (dNow > meleeAttackRange + 1.0f)
                    {
                        activePattern = AttackPattern.None;
                        hasHyperArmor = false;
                        currentState = AIState.ChasingAndStrafing;
                        slatternBlitzCooldown = 1.0f;
                        yield break;
                    }

                    SetLocomotionSpeed(1f);
                    desiredVelocity = transform.forward * (dNow > minCombatDistance ? walkSpeed * 0.85f : walkSpeed * 0.25f);
                    if (targetJaeger != null && targetJaeger.isBlocking)
                    {
                        CrossFadeState("Slam", 0.10f, true);
                        yield return new WaitForSeconds(slamHitTime / spdScale);
                        if (currentState != AIState.Attacking) yield break;
                        ExecuteStrikeImpact(null, GetSlamCenter(), heavySlamDamage * 0.95f, slamStrikeRadius, true, 32f);
                        yield return new WaitForSeconds(Mathf.Max(0.05f, (slamClipLen - slamHitTime) / spdScale));
                    }
                    else
                    {
                        CrossFadeState("PunchRight", 0.10f, true);
                        yield return new WaitForSeconds(rightHitTime / spdScale);
                        if (currentState != AIState.Attacking) yield break;
                        ExecuteStrikeImpact(handR, transform.position + transform.forward * 13f + Vector3.up * 12f, lightPunchDamage, punchStrikeRadius, false, 22f);

                        yield return new WaitForSeconds(0.10f / spdScale);
                        if (currentState != AIState.Attacking) yield break;
                        desiredVelocity = transform.forward * (FlatDistToTarget() > minCombatDistance ? walkSpeed * 0.85f : walkSpeed * 0.25f);

                        CrossFadeState("PunchLeft", 0.10f, true);
                        yield return new WaitForSeconds(leftHitTime / spdScale);
                        if (currentState != AIState.Attacking) yield break;
                        ExecuteStrikeImpact(handL, transform.position + transform.forward * 13f + Vector3.up * 12f, lightPunchDamage * 0.95f, punchStrikeRadius, false, 24f);
                        yield return new WaitForSeconds(Mathf.Max(0.05f, (punchClipLen - leftHitTime) / spdScale));
                    }
                    slatternBlitzCooldown = Random.Range(3.0f, 4.4f);
                    break;

                case AttackPattern.DeviousBlitzLightCombo:
                    if (!isParryCountering)
                    {
                        SetLocomotionSpeed(1.22f);
                        CrossFadeState("Run", 0.10f, true);
                        float blitzRunElapsed = 0f;
                        while (blitzRunElapsed < 4.2f && currentState == AIState.Attacking && targetJaeger != null)
                        {
                            Vector3 toT = targetJaeger.transform.position - transform.position;
                            toT.y = 0f;
                            float d = toT.magnitude;
                            if (d > 0.1f) RotateYawToward(Mathf.Atan2(toT.x, toT.z) * Mathf.Rad2Deg, turnSpeed * 1.6f, Time.deltaTime);
                            desiredVelocity = transform.forward * (empSprintSpeed * 1.32f);
                            if (d <= closeInTargetDist) break;
                            blitzRunElapsed += Time.deltaTime;
                            yield return null;
                        }
                        if (currentState != AIState.Attacking) yield break;
                        if (FlatDistToTarget() > meleeAttackRange + 1.0f)
                        {
                            activePattern = AttackPattern.None;
                            hasHyperArmor = false;
                            isParryCountering = false;
                            currentState = AIState.ChasingAndStrafing;
                            slatternBlitzCooldown = 1.0f;
                            yield break;
                        }
                    }

                    float blitzSpd = IsEmpBuffed ? 1.55f : 1.45f;
                    SetLocomotionSpeed(1f);
                    for (int hitIdx = 0; hitIdx < 4; hitIdx++)
                    {
                        if (currentState != AIState.Attacking) yield break;
                        float dHit = FlatDistToTarget();
                        if (hitIdx > 0 && dHit > meleeAttackRange + 3.0f) break;

                        if (targetJaeger != null)
                        {
                            Vector3 toT = targetJaeger.transform.position - transform.position;
                            toT.y = 0f;
                            if (toT.sqrMagnitude > 0.01f) RotateYawToward(Mathf.Atan2(toT.x, toT.z) * Mathf.Rad2Deg, turnSpeed * 2.0f, 0.08f);
                        }
                        desiredVelocity = transform.forward * (dHit > minCombatDistance ? walkSpeed * 0.90f : walkSpeed * 0.25f);
                        bool useLeft = (hitIdx % 2) == 0;
                        CrossFadeState(useLeft ? "PunchLeft" : "PunchRight", 0.07f, true);
                        float windup = useLeft ? leftHitTime : rightHitTime;
                        yield return new WaitForSeconds(windup / blitzSpd);
                        if (currentState != AIState.Attacking) yield break;

                        float hitDmg = lightPunchDamage * (hitIdx == 3 ? 0.90f : 0.70f);
                        ExecuteStrikeImpact(
                            useLeft ? handL : handR,
                            transform.position + transform.forward * 13f + Vector3.up * 12f,
                            hitDmg,
                            punchStrikeRadius * 1.08f,
                            false,
                            hitIdx == 3 ? 22f : 12f);
                        yield return new WaitForSeconds(Mathf.Max(0.06f, (punchClipLen - windup) * 0.65f / blitzSpd));
                    }

                    isParryCountering = false;
                    slatternBlitzCooldown = Random.Range(2.2f, 3.4f);
                    break;
            }

            if (currentState == AIState.Attacking)
            {
                activePattern = AttackPattern.None;
                hasHyperArmor = false;
                isParryCountering = false;

                if (isDebugForcedActionActive && IsAIDisabled)
                {
                    isDebugForcedActionActive = false;
                    currentState = AIState.Idle;
                    desiredVelocity = Vector3.zero;
                    CrossFadeState("Idle", 0.2f);
                }
                else
                {
                    isDebugForcedActionActive = false;
                    if (isSlattern)
                {
                    if (CurrentPhase == 3)
                    {
                        // Phase 3 tank never retreats: immediately re-engages forward Moving Guard
                        currentState = AIState.ChasingAndStrafing;
                        postAttackPacingTimer = Random.Range(0.45f, 0.75f);
                        circleStrafeDir = Random.value > 0.5f ? 1f : -1f;
                    }
                    else
                    {
                        // Phase 1/2 Distance Fighter & Blitzer back-sprint into the fog after attacking
                        StartRetreat(useSprint: true, duration: Random.Range(1.45f, 2.15f), customTargetDist: CurrentPhase == 1 ? 42f : 36f);
                    }
                }
                else if (Random.value < 0.45f)
                {
                    StartRetreat(useSprint: CurrentPhase >= 2 && Random.value < 0.55f, duration: Random.Range(1.0f, 1.55f));
                }
                    else
                    {
                        currentState = AIState.ChasingAndStrafing;
                        postAttackPacingTimer = IsEmpBuffed ? Random.Range(0.55f, 0.95f) : Random.Range(0.95f, 1.55f);
                        circleStrafeDir = Random.value > 0.5f ? 1f : -1f;
                    }
                }
            }
        }

        private Vector3 GetSlamCenter()
        {
            if (handL != null && handR != null)
            {
                return (handL.position + handR.position) * 0.5f;
            }
            return transform.position + transform.forward * 12.5f + Vector3.up * 8f;
        }

        private void ExecuteStrikeImpact(Transform handBone, Vector3 fallbackPos, float damage, float strikeRadius, bool isHeavy, float knockbackImpulse)
        {
            Vector3 impactPos = handBone != null ? handBone.position : fallbackPos;
            if (isHeavy)
            {
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuSlamImpact(impactPos);
            }
            else
            {
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuBite(impactPos);
            }
            EmitOceanImpact(impactPos, transform.forward, isHeavy ? slamWaveRadius : 18f, isHeavy ? slamWaveStrength : 1.8f, isHeavy ? 2.6f : 1.4f, isHeavy, isHeavy ? 1.5f : 0.8f);

            if (isCutsceneMode)
            {
                desiredVelocity = Vector3.zero;
                SetLocomotionSpeed(0f);
                CrossFadeState("Idle", 0.25f);
                return;
            }

            if (targetJaeger == null || targetJaeger.IsDead) return;

            Vector3 jaegerChest = targetJaeger.transform.position + Vector3.up * 14f;
            float handDist = Vector3.Distance(impactPos, jaegerChest);
            float handHorizDist = Vector2.Distance(
                new Vector2(impactPos.x, impactPos.z),
                new Vector2(jaegerChest.x, jaegerChest.z));

            Vector3 flatToJaeger = targetJaeger.transform.position - transform.position;
            flatToJaeger.y = 0f;
            float rootDist = flatToJaeger.magnitude;
            float fwdDot = rootDist > 0.01f ? Vector3.Dot(transform.forward, flatToJaeger / rootDist) : 1f;

            bool inStrikeZone = (handDist <= strikeRadius || handHorizDist <= strikeRadius || rootDist <= meleeAttackRange - 1.0f) && fwdDot >= 0.68f;
            if (inStrikeZone)
            {
                Vector3 kbDir = rootDist > 0.01f ? (flatToJaeger / rootDist) : transform.forward;
                float phaseDamageMult = CurrentPhase == 3 ? 1.15f : 1.0f;
                targetJaeger.TakeDamage(damage * phaseDamageMult, kbDir * knockbackImpulse, isHeavy, impactPos);
            }
        }

        private void StartBlock(bool fastReaction)
        {
            CancelActiveSequence();
            currentState = AIState.Blocking;
            blockEnterFast = fastReaction;
            stateTimer = Random.Range(1.35f, 1.95f);
            counterQueued = false;
            desiredVelocity = Vector3.zero;
            SetLocomotionSpeed(1f);
            CrossFadeState("BlockEnter", fastReaction ? 0.06f : 0.12f, true);
        }

        private void TriggerStagger(string staggerState, float duration)
        {
            isPursuitSprinting = false;
            CancelActiveSequence();
            currentState = AIState.Staggered;
            stateTimer = duration;
            counterQueued = false;
            desiredVelocity = Vector3.zero;
            upperBodyGuardWeight = 0f;
            if (animator != null && animator.layerCount > 1)
            {
                animator.SetLayerWeight(1, 0f);
            }
            SetLocomotionSpeed(1f);
            CrossFadeState(staggerState, 0.08f, true);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuStaggerPain(transform.position + Vector3.up * 14f);
        }

        /// <summary>
        /// Instantly staggers the Kaiju upon being parried by Gipsy Danger:
        /// cancels any active attack sequence, pushes the Kaiju backward with recoil, and opens a damage window without dealing damage.
        /// </summary>
        public void TriggerParryStagger(Vector3 recoilForce)
        {
            if (IsDead) return;

            CancelActiveSequence();
            currentState = AIState.Staggered;
            stateTimer = 2.2f;
            counterQueued = false;
            consecutiveStaggers = 0;
            desiredVelocity = Vector3.zero;
            SetLocomotionSpeed(1f);

            if (rb != null && recoilForce.sqrMagnitude > 0.01f)
            {
                rb.AddForce(recoilForce, ForceMode.Impulse);
            }

            CrossFadeState("StaggerUp", 0.06f, true);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuStaggerPain(transform.position + Vector3.up * 14f);
            Debug.Log("<color=#ffd700><b>[Kaiju AI]</b></color> Deflected by Gipsy's PERFECT PARRY! Staggered for 2.2s without taking direct parry damage.");
        }

        private void TriggerSlatternParryCounter(Vector3 sparkPos)
        {
            CancelActiveSequence();
            isParryCountering = true;
            isGuardBrokenStagger = false;
            consecutiveStaggers = 0;

            PacificRimAudio.PacificRimAudioManager.Instance?.PlayParryClang(sparkPos);
            WaterSplashManager.Instance?.SpawnParrySparks(sparkPos, transform.forward);
            GetComponent<KaijuBioluminescenceController>()?.TriggerSurge(2.2f);
            PacificRimUI.JaegerHUD.Instance?.ShowBanner("SLATTERN PARRIED // DEVIANT BLITZ COUNTER!", 1.8f);

            if (targetJaeger != null && !targetJaeger.IsDead)
            {
                targetJaeger.OnParriedByKaiju(-targetJaeger.transform.forward * 14f, sparkPos);
            }

            currentState = AIState.Blocking;
            blockEnterFast = true;
            stateTimer = 0.15f;
            desiredVelocity = Vector3.zero;
            SetLocomotionSpeed(1f);
            CrossFadeState("BlockEnter", 0.04f, true);
            activeSequenceCoroutine = StartCoroutine(HoldParryBlockThenCounterRoutine());
            Debug.Log($"<color=#00e5ff><b>[Slattern P{CurrentPhase} PARRY!]</b></color> Deflected Gipsy's strike, holding block 0.15s before Devious Light Blitz Counter!");
        }

        private IEnumerator HoldParryBlockThenCounterRoutine()
        {
            yield return new WaitForSeconds(0.15f);
            activeSequenceCoroutine = null;
            if (!IsDead && !IsAIDisabled)
            {
                StartAttackPattern(AttackPattern.DeviousBlitzLightCombo, hyperArmored: true);
            }
        }

        public void TakeDamage(float amount, Vector3 knockback, int attackVariant = 0, Vector3 hitPoint = default, float stunMultiplier = 1.0f)
        {
            if (IsDead) return;

            if (IsBaguvixActive)
            {
                // Baguvix pacifist & invulnerability: spawn defensive sparks feedback, but zero damage taken and never staggered
                Vector3 sparkPoint = hitPoint.sqrMagnitude > 0.01f
                    ? hitPoint
                    : (transform.position + transform.forward * 10.5f + Vector3.up * 15.5f);
                WaterSplashManager.Instance?.SpawnCombatSparks(sparkPoint, -transform.forward, isBlocked: true, isHeavy: false, isKaijuTarget: true);
                return;
            }

            bool isLightVariant = attackVariant == 1 || attackVariant == 2 || attackVariant == 3;
            bool isHeavyHit = !isLightVariant && (attackVariant == 4 || attackVariant == 5 || amount >= heavyAttackDamageThreshold);
            bool isCurrentlyGuarding = currentState == AIState.Blocking || IsMovingGuardActive;
            bool isBlocked = isCurrentlyGuarding && !isHeavyHit;

            Vector3 sparkPos = hitPoint.sqrMagnitude > 0.01f
                ? hitPoint
                : (transform.position + transform.forward * 10.5f + Vector3.up * 15.5f);

            // Slattern Parry Check (10% in P1, heavy parry rate in P2 Blitzer, 10% vs light hits in P3;
            // vulnerable during AIState.Attacking so Gipsy can catch him off-guard, and never parries Heavy Guard-Break!).
            if (isSlattern && !IsDead && currentState != AIState.Staggered && currentState != AIState.PhaseTransition &&
                currentState != AIState.EmpRoar && currentState != AIState.Attacking && !(IsMovingGuardActive && isHeavyHit))
            {
                float parryChance;
                if (CurrentPhase == 1) parryChance = slatternPhase1ParryChance;
                else if (CurrentPhase == 2) parryChance = Mathf.Min(0.70f, slatternPhase2ParryChance + (playerSpamCount >= 2 ? 0.15f : 0f));
                else parryChance = isHeavyHit ? 0f : slatternPhase3ParryChance;

                if (Random.value < parryChance)
                {
                    TriggerSlatternParryCounter(sparkPos);
                    return;
                }
            }

            WaterSplashManager.Instance?.SpawnCombatSparks(sparkPos, -transform.forward, isBlocked, isHeavyHit, isKaijuTarget: true);
            lastDamageTime = Time.time;

            if (isCurrentlyGuarding)
            {
                if (isHeavyHit)
                {
                    Debug.Log($"<color=#ffaa00><b>[{kaijuDisplayName} GUARD BROKEN!]</b></color> Heavy strike shattered guard for {amount:F0} DMG!");
                    ApplyHealthReduction(amount * (isSlattern ? 1.20f : 1.0f), knockback * 0.85f);
                    PacificRimUI.JaegerHUD.Instance?.ShowBanner($"GUARD BROKEN // {kaijuDisplayName} STAGGERED!", 1.6f);
                    EmitOceanImpact(transform.position, -transform.forward, 28f, 2.8f, 2.8f, true, 1.6f);
                    if (!IsDead && currentState != AIState.PhaseTransition)
                    {
                        isGuardBrokenStagger = true;
                        movingGuardLightHits = 0;
                        consecutiveStaggers = 0;
                        currentPoise = 0f;
                        float clampedStun = Mathf.Clamp(stunMultiplier, 0.2f, 2.0f);
                        TriggerStagger("StaggerDown", (isSlattern ? 2.85f : (StaggerDownClipLength / 1.15f)) * clampedStun);
                    }
                    return;
                }

                float blockedDmg = amount * (1f - Mathf.Clamp01(blockDamageReduction));
                ApplyHealthReduction(blockedDmg, knockback * 0.25f);
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayArmorCreak(transform.position + Vector3.up * 14f, 0.85f);

                if (IsMovingGuardActive)
                {
                    movingGuardLightHits++;
                    if (movingGuardLightHits >= 3)
                    {
                        movingGuardLightHits = 0;
                        StartAttackPattern(Random.value < 0.55f ? AttackPattern.DeviousBlitzLightCombo : AttackPattern.HeavySlam);
                    }
                    Debug.Log($"<color=#ffaa00><b>[SLATTERN MOVING GUARD]</b></color> Light attack deflected ({movingGuardLightHits}/3)! Use Heavy Attack to guard-break or catch during swing!");
                    return;
                }

                counterQueued = true;
                stateTimer = Mathf.Min(stateTimer, 0.32f);

                Debug.Log($"<color=#ffaa00><b>[{kaijuDisplayName} BLOCKED]</b></color> Absorbed {amount - blockedDmg:F0} DMG! Armored counter incoming!");
                return;
            }

            float finalDmg = CurrentPhase == 3 ? (amount * phase3DamageMultiplier) : amount;
            ApplyHealthReduction(finalDmg, knockback * 0.45f);

            if (IsDead || currentState == AIState.PhaseTransition) return;

            // Hyper-armor attacks cannot be interrupted mid-swing unless broken by a Heavy Attack
            if (hasHyperArmor && !isHeavyHit)
            {
                return;
            }

            // Directional Stagger Resolution:
            // Variant 1 (Right Hook) -> StaggerRight
            // Variant 2 (Left Hook) -> StaggerLeft
            // Variant 3 (Uppercut) -> StaggerUp
            // Variant 4 / 5 (Heavy Attack) -> StaggerDown
            string targetStagger;
            float staggerDuration;
            if (attackVariant == 4 || attackVariant == 5 || isHeavyHit)
            {
                targetStagger = "StaggerDown";
                staggerDuration = (isSlattern ? SlatternStaggerClipLength : StaggerDownClipLength) / 1.15f;
                if (isSlattern) EmitOceanImpact(transform.position, -transform.forward, 26f, 2.4f, 2.5f, true, 1.2f);
            }
            else if (attackVariant == 3)
            {
                targetStagger = "StaggerUp";
                staggerDuration = (isSlattern ? SlatternStaggerClipLength : StaggerUpClipLength) / 1.15f;
            }
            else if (attackVariant == 1)
            {
                targetStagger = "StaggerRight";
                staggerDuration = 1.05f;
            }
            else if (attackVariant == 2)
            {
                targetStagger = "StaggerLeft";
                staggerDuration = 1.05f;
            }
            else
            {
                float sideDot = knockback.sqrMagnitude > 0.01f ? Vector3.Dot(knockback.normalized, transform.right) : 0f;
                targetStagger = sideDot >= 0f ? "StaggerRight" : "StaggerLeft";
                staggerDuration = StaggerSideClipLength / 1.35f;
            }

            if (Time.time - lastStaggerTime > 2.2f)
            {
                consecutiveStaggers = 0;
                isGuardBrokenStagger = false;
            }

            bool isUppercutFinisher = targetStagger == "StaggerUp" && consecutiveStaggers == 2;
            consecutiveStaggers++;
            lastStaggerTime = Time.time;

            int staggerCap = isGuardBrokenStagger ? 5 : 4;
            // Combo-Breaker: allow full 3-hit light + heavy finisher (or 5th-hit break after Guard Break), then punish excessive stunlocking
            if (consecutiveStaggers >= staggerCap && !isUppercutFinisher)
            {
                consecutiveStaggers = 0;
                isGuardBrokenStagger = false;
                if (isSlattern && CurrentPhase < 3)
                {
                    StartRetreat(useSprint: true, duration: 1.45f, customTargetDist: 38f);
                }
                else if (isSlattern && CurrentPhase == 3)
                {
                    StartAttackPattern(AttackPattern.DeviousBlitzLightCombo);
                }
                else if (Random.value < 0.65f)
                {
                    StartBlock(fastReaction: true);
                }
                else
                {
                    StartRetreat(useSprint: true, duration: 1.25f);
                }
                return;
            }

            staggerDuration *= Mathf.Clamp(stunMultiplier, 0.2f, 2.0f);
            TriggerStagger(targetStagger, staggerDuration);
        }

        private void ApplyHealthReduction(float dmg, Vector3 impulse)
        {
            currentHealth = Mathf.Max(0f, currentHealth - dmg);
            if (rb != null) rb.AddForce(impulse, ForceMode.Impulse);

            Debug.Log($"<color=#ff8800><b>[Leatherback Boss P{CurrentPhase}]</b></color> Hit for {dmg:F0} DMG! HP: {currentHealth:F0}/{maxHealth:F0}");

            if (currentHealth <= 0f)
            {
                Die();
                return;
            }

            if (CurrentPhase == 1 && currentHealth <= maxHealth * 0.65f)
            {
                currentHealth = Mathf.Max(currentHealth, maxHealth * 0.65f);
                StartPhaseTransition(2);
            }
            else if (CurrentPhase == 2 && currentHealth <= maxHealth * 0.30f)
            {
                currentHealth = Mathf.Max(currentHealth, maxHealth * 0.28f);
                StartPhaseTransition(3);
            }
        }

        private void Die()
        {
            CancelActiveSequence();
            currentHealth = 0f;
            currentState = AIState.Dead;
            deathLoopEntered = false;
            stateTimer = isSlattern ? SlatternDeathClipLength : DeathClipLength;
            desiredVelocity = Vector3.zero;
            SetLocomotionSpeed(1f);
            if (animator != null) animator.speed = 1.0f;

            CrossFadeState("Death", 0.18f, true);
            PacificRimAudio.PacificRimAudioManager.Instance?.PlayKaijuDeathCollapse(transform.position + Vector3.up * 10f);
            StartCoroutine(DeathSplashRoutine());
            StartCoroutine(VictorySequenceRoutine());
            Debug.Log("<color=#00e5ff><b>[KAIJU NEUTRALIZED]</b></color> Leatherback collapses into the Pacific!");
        }

        private IEnumerator DeathSplashRoutine()
        {
            yield return new WaitForSeconds(1.8f);
            EmitOceanImpact(transform.position + transform.forward * 4f, Vector3.up, 36f, 3.0f, 3.5f, true, 1.8f);
        }

        private IEnumerator VictorySequenceRoutine()
        {
            if (isSlattern)
            {
                // Allow Slattern's death collapse into the trench floor before starting the 38s Breach finale
                yield return new WaitForSeconds(2.8f);
                PacificRimCutscene.OperationPitfallEndingCutscene.StartFromSlatternDefeat();
                yield break;
            }

            // Give time for Leatherback's collapse into the Pacific waves
            yield return new WaitForSeconds(1.2f);
            if (targetJaeger != null && !targetJaeger.IsDead)
            {
                targetJaeger.TriggerVictoryPose();
            }
            else if (SimpleJaegerController.Instance != null && !SimpleJaegerController.Instance.IsDead)
            {
                SimpleJaegerController.Instance.TriggerVictoryPose();
            }

            // Allow Gipsy Danger's battle victory roar to ring out across the storm
            yield return new WaitForSeconds(2.8f);
            PacificRimUI.MainMenuUI.Instance?.SetVictoryState();
        }

        private void TrackFootsteps()
        {
            if (currentState != AIState.ChasingAndStrafing &&
                currentState != AIState.SizingUp &&
                currentState != AIState.Retreating)
            {
                return;
            }

            Vector3 cur = transform.position;
            float moved = Vector3.Distance(new Vector3(cur.x, 0f, cur.z), new Vector3(lastStepPos.x, 0f, lastStepPos.z));
            if (moved >= strideDistance)
            {
                lastStepPos = cur;
                isLeftFoot = !isLeftFoot;

                Vector3 footPos = cur + (isLeftFoot ? -transform.right : transform.right) * 4.5f;
                PacificRimAudio.PacificRimAudioManager.Instance?.PlayOceanFootstep(footPos, (currentState == AIState.Attacking || CurrentPhase >= 2) ? 1.4f : 1.1f);
                EmitOceanImpact(footPos, transform.forward, footstepWaveRadius, footstepWaveStrength, isSlattern ? 2.5f : 1.8f, false, 0f);
            }
        }

        private static void EmitOceanImpact(Vector3 worldPos, Vector3 dir, float waveRadius, float waveStrength, float splashIntensity, bool isHeavy, float shakeIntensity)
        {
            if (WaterSystem.Instance != null)
            {
                worldPos.y = WaterSystem.Instance.GetWaterHeight(worldPos);
                WaterSystem.Instance.AddFootstepWave(worldPos, waveRadius, waveStrength);
            }

            if (WaterSplashManager.Instance != null)
            {
                if (isHeavy)
                    WaterSplashManager.Instance.SpawnHeavyMoveWaterKickup(worldPos, dir, splashIntensity, true);
                else
                    WaterSplashManager.Instance.SpawnFootstepSplash(worldPos, splashIntensity);
            }

            if (isHeavy && WaterSystem.Instance != null)
            {
                ProceduralSplash.CreateFoamRing(worldPos, waveRadius * 1.05f, 2.2f);
            }

            if (shakeIntensity > 0f)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    var shaker = cam.GetComponent<PacificRimCameraShake>();
                    if (shaker != null) shaker.TriggerStompFeedback(shakeIntensity);
                }
            }
        }
    }
}
