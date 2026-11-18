using UnityEngine;

namespace PacificRimWater
{
    /// <summary>
    /// Smooth third-person cinematic follow camera for the Jaeger mech.
    /// Operates in LateUpdate to eliminate physics jitter and avoids non-uniform scale inheritance.
    /// Fully compatible with both the New Input System and Legacy Input Manager.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class JaegerCameraFollow : MonoBehaviour
    {
        public static JaegerCameraFollow Instance { get; private set; }

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

        [Header("Field of View")]
        [Tooltip("Base field of view in degrees.")]
        public float baseFieldOfView = 50f;
        [Tooltip("Cinematic telephoto field of view during deployment cutscene.")]
        public float cutsceneFieldOfView = 40f;

        [Header("Attack Zoom Dynamics")]
        [Tooltip("Additional camera follow distance during heavy jump-sprint lunge attacks.")]
        public float attackZoomExtraDistance = 8.5f;
        [Tooltip("Additional camera FOV during heavy jump-sprint lunge attacks.")]
        public float attackZoomExtraFOV = 4.0f;
        private float currentAttackExtraDist = 0f;
        private float currentAttackExtraFov = 0f;
        private Camera cam;

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

        // Plasmacaster mini-cinematic state
        private bool wasPlasmaCinematicActive;
        private Vector3 plasmaCamStartPos;
        private Quaternion plasmaCamStartRot;
        private float plasmaCamStartFov;
        private Vector3 plasmaCamLastPos;
        private Quaternion plasmaCamLastRot;
        private float plasmaCamLastFov;
        private Vector3 plasmaRecoilStartPos;
        private Quaternion plasmaRecoilStartRot;
        private float plasmaRecoilStartFov;
        private bool plasmaRecoilCaptured;
        private Vector3 plasmaRecoverStartPos;
        private Quaternion plasmaRecoverStartRot;
        private float plasmaRecoverStartFov;
        private bool plasmaRecoverCaptured;

        public float CurrentYaw
        {
            get => currentYaw;
            set => currentYaw = value;
        }

        private void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.fieldOfView = baseFieldOfView;
            }
        }

        void Start()
        {
            if (target == null)
            {
                var jaeger = GameObject.Find("Jaeger_Mech");
                if (jaeger != null) target = jaeger.transform;
            }

            if (cam == null) cam = GetComponent<Camera>();
            if (cam != null) cam.fieldOfView = baseFieldOfView;

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
            if (PacificRimUI.MainMenuUI.IsMenuOpen || PacificRimUI.PacificRimAdminUI.IsOpen)
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

            var jaegerCtrl = SimpleJaegerController.Instance;
            bool isPlasmaCinematic = jaegerCtrl != null && jaegerCtrl.IsPlasmaCinematicActive && activeCutsceneTransition == null;

            // Freecam toggle via 'E' key (locked during Plasmacaster mini-cinematic)
            if (!isPlasmaCinematic && !PacificRimUI.MainMenuUI.IsMenuOpen && !PacificRimUI.PacificRimAdminUI.IsOpen && !PacificRimUI.JaegerHUD.SuppressFreeCamThisFrame && WasFreeCamTogglePressedThisFrame())
            {
                ToggleFreeCam();
            }

            // Read mouse input safely across New and Legacy Input Systems
            float mouseDeltaX = 0f;
            float mouseDeltaY = 0f;

            if (enableMouseLook && !isPlasmaCinematic)
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

            if (enableMouseLook && !isPlasmaCinematic)
            {
                currentYaw += mouseDeltaX * mouseSensitivity;
                currentPitch -= mouseDeltaY * mouseSensitivity;
                float activeMinPitch = isFreeCam ? freeCamMinPitch : minPitch;
                float activeMaxPitch = isFreeCam ? freeCamMaxPitch : maxPitch;
                currentPitch = Mathf.Clamp(currentPitch, activeMinPitch, activeMaxPitch);
            }

            // Distance zooming in freecam mode: Mouse ScrollWheel & Arrow Keys Up/Down
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

                // Arrow keys Up and Down zooming
                float arrowZoom = 0f;
                try
                {
                    if (Input.GetKey(KeyCode.UpArrow)) arrowZoom += 1f;
                    if (Input.GetKey(KeyCode.DownArrow)) arrowZoom -= 1f;
                }
                catch {}
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    var kb = UnityEngine.InputSystem.Keyboard.current;
                    if (kb.upArrowKey.isPressed) arrowZoom += 1f;
                    if (kb.downArrowKey.isPressed) arrowZoom -= 1f;
                }
#endif
                if (Mathf.Abs(arrowZoom) > 0.01f)
                {
                    // Up arrow zooms IN (decreases distance), Down arrow zooms OUT (increases distance)
                    freeCamDistance = Mathf.Clamp(freeCamDistance - arrowZoom * (freeCamZoomSpeed * 1.6f) * Time.deltaTime, freeCamMinDistance, freeCamMaxDistance);
                }
            }

            // Sprint-heavy jump attack & I-19 Plasmacaster dynamic camera framing
            bool isSprintAttacking = !isFreeCam && jaegerCtrl != null && jaegerCtrl.IsSprintAttacking;
            bool isPlasmaCharging = !isFreeCam && jaegerCtrl != null && jaegerCtrl.IsPlasmaCharging;
            bool isPlasmaRecoil = !isFreeCam && jaegerCtrl != null && jaegerCtrl.IsPlasmaRecoil;
            float targetExtraDist = isSprintAttacking ? attackZoomExtraDistance : 0f;
            float targetExtraFov = isSprintAttacking ? attackZoomExtraFOV : 0f;
            float zoomRate = isSprintAttacking ? 7.5f : 3.0f;
            currentAttackExtraDist = Mathf.Lerp(currentAttackExtraDist, targetExtraDist, Time.deltaTime * zoomRate);
            currentAttackExtraFov = Mathf.Lerp(currentAttackExtraFov, targetExtraFov, Time.deltaTime * zoomRate);

            if (cam != null && activeCutsceneTransition == null)
            {
                float activeBaseFov = isCutsceneFreeCam ? cutsceneFieldOfView : baseFieldOfView;
                cam.fieldOfView = activeBaseFov + currentAttackExtraFov;
            }

            Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);
            Quaternion targetRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            float effectiveShoulderOffset = isFreeCam ? 0f : rightShoulderOffset;
            float effectiveDistance = isFreeCam ? freeCamDistance : (distance + currentAttackExtraDist);
            Vector3 orbitCenter = target.position + targetOffset + (yawRotation * Vector3.right * effectiveShoulderOffset) + (isFreeCam ? Vector3.zero : Vector3.up * height);
            Vector3 desiredPosition = orbitCenter - (targetRotation * Vector3.forward * effectiveDistance);

            if (!isFreeCam)
            {
                if (WaterSystem.Instance != null)
                {
                    float minCameraY = WaterSystem.Instance.transform.position.y + 2.2f;
                    desiredPosition.y = Mathf.Max(desiredPosition.y, minCameraY);
                }
                else
                {
                    desiredPosition.y = Mathf.Max(desiredPosition.y, 1.5f);
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

            if (isPlasmaCinematic)
            {
                if (!wasPlasmaCinematicActive)
                {
                    wasPlasmaCinematicActive = true;
                    plasmaCamStartPos = transform.position;
                    plasmaCamStartRot = transform.rotation;
                    plasmaCamStartFov = cam != null ? cam.fieldOfView : baseFieldOfView;
                    plasmaCamLastPos = plasmaCamStartPos;
                    plasmaCamLastRot = plasmaCamStartRot;
                    plasmaCamLastFov = plasmaCamStartFov;
                    plasmaRecoilCaptured = false;
                    plasmaRecoverCaptured = false;
                }

                Vector3 otsPos = desiredPosition;
                Quaternion otsLook = (aimPoint - otsPos).sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation((aimPoint - otsPos).normalized, Vector3.up)
                    : baseRotation;

                EvaluatePlasmaCinematicCamera(
                    jaegerCtrl,
                    otsPos,
                    otsLook,
                    baseFieldOfView,
                    out Vector3 cinePos,
                    out Quaternion cineRot,
                    out float cineFov);

                if (cam != null)
                {
                    cam.fieldOfView = cineFov;
                }
                transform.position = cinePos + cineRot * shakeLocalOffset;
                transform.rotation = cineRot * Quaternion.Euler(shakeRotOffset);
                smoothedBasePos = desiredPosition;
                currentVelocity = Vector3.zero;
                return;
            }
            else if (wasPlasmaCinematicActive)
            {
                wasPlasmaCinematicActive = false;
                plasmaRecoilCaptured = false;
                plasmaRecoverCaptured = false;
                smoothedBasePos = desiredPosition;
                currentVelocity = Vector3.zero;
                if (cam != null) cam.fieldOfView = baseFieldOfView;
            }

            transform.position = smoothedBasePos + baseRotation * (swayLocalPos + shakeLocalOffset);
            transform.rotation = baseRotation * Quaternion.Euler(swayEuler + shakeRotOffset);
        }

        private void EvaluatePlasmaCinematicCamera(
            SimpleJaegerController jaegerCtrl,
            Vector3 otsPos,
            Quaternion otsRot,
            float otsFov,
            out Vector3 outPos,
            out Quaternion outRot,
            out float outFov)
        {
            Vector3 aimDir = jaegerCtrl.PlasmaAimDirectionWorld;
            if (aimDir.sqrMagnitude < 0.001f) aimDir = jaegerCtrl.transform.forward;
            aimDir.Normalize();

            Vector3 sideRight = Vector3.Cross(Vector3.up, aimDir);
            if (sideRight.sqrMagnitude < 0.001f) sideRight = jaegerCtrl.transform.right;
            sideRight.Normalize();

            Vector3 chestPos = jaegerCtrl.JaegerChestWorldPos;
            Vector3 shoulderPos = jaegerCtrl.RightUpperArmTransform != null
                ? jaegerCtrl.RightUpperArmTransform.position
                : (chestPos + sideRight * 2.6f);
            Vector3 elbowPos = jaegerCtrl.RightForearmTransform != null
                ? jaegerCtrl.RightForearmTransform.position
                : Vector3.Lerp(shoulderPos, jaegerCtrl.PlasmaBarrelMuzzleWorldPos, 0.5f);
            Vector3 muzzlePos = jaegerCtrl.PlasmaBarrelMuzzleWorldPos;

            // Stage 1 end / Stage 2 start: right shoulder framing looking forward down the barrel arm
            Vector3 shoulderCamPos = shoulderPos - aimDir * 2.8f + sideRight * 3.8f + Vector3.up * 1.9f;
            Vector3 shoulderLookAt = muzzlePos + aimDir * 3.5f;
            Quaternion shoulderCamRot = Quaternion.LookRotation((shoulderLookAt - shoulderCamPos).normalized, Vector3.up)
                                      * Quaternion.Euler(0f, 0f, -4.0f);

            if (jaegerCtrl.IsPlasmaCharging)
            {
                float u = jaegerCtrl.PlasmaChargeProgress;

                if (u <= 0.24f)
                {
                    // Phase 1: Standard over-the-shoulder -> tracking onto Gipsy's right shoulder
                    float t1 = Mathf.SmoothStep(0f, 1f, u / 0.24f);
                    outPos = Vector3.Lerp(plasmaCamStartPos, shoulderCamPos, t1);
                    outRot = Quaternion.Slerp(plasmaCamStartRot, shoulderCamRot, t1);
                    outFov = Mathf.Lerp(plasmaCamStartFov, 44f, t1);
                }
                else if (u <= 0.58f)
                {
                    // Phase 2: Camera treks forward along the right arm (shoulder -> elbow -> splayed muzzle)
                    float t2 = Mathf.SmoothStep(0f, 1f, (u - 0.24f) / 0.34f);
                    Vector3 armPoint = t2 < 0.5f
                        ? Vector3.Lerp(shoulderPos, elbowPos, t2 * 2f)
                        : Vector3.Lerp(elbowPos, muzzlePos, (t2 - 0.5f) * 2f);
                    Vector3 armOffset = sideRight * Mathf.Lerp(3.8f, 3.1f, t2)
                                      + Vector3.up * Mathf.Lerp(1.9f, 1.0f, t2)
                                      - aimDir * Mathf.Lerp(2.8f, 0.0f, t2);
                    outPos = armPoint + armOffset;

                    Vector3 look2 = Vector3.Lerp(muzzlePos + aimDir * 3.5f, muzzlePos + aimDir * 0.4f, t2);
                    Quaternion rawRot2 = Quaternion.LookRotation((look2 - outPos).normalized, Vector3.up);
                    outRot = rawRot2 * Quaternion.Euler(0f, 0f, Mathf.Lerp(-4.0f, -6.5f, t2));
                    outFov = Mathf.Lerp(44f, 41f, t2);
                }
                else
                {
                    // Phase 3: Sweep around the tip of the barrel to face Gipsy head-on ("about to shoot the camera")
                    float rawT3 = Mathf.Clamp01((u - 0.58f) / 0.42f);
                    float arcT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rawT3 / 0.67f));
                    float pushT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((rawT3 - 0.62f) / 0.38f));

                    Vector3 vStart = sideRight * 3.1f + Vector3.up * 1.0f;
                    float frontDist = Mathf.Lerp(7.4f, 6.7f, pushT);
                    Vector3 vEnd = aimDir * frontDist + sideRight * 0.42f - Vector3.up * 0.52f;

                    Vector3 arcDir = Vector3.Slerp(vStart.normalized, vEnd.normalized, arcT);
                    float arcRad = Mathf.Lerp(vStart.magnitude, vEnd.magnitude, arcT);
                    outPos = muzzlePos + arcDir * arcRad;

                    Vector3 barrelSideLook = muzzlePos + aimDir * 0.4f;
                    Vector3 downBarrelLook = Vector3.Lerp(muzzlePos, chestPos + Vector3.up * 0.85f, 0.34f);
                    Vector3 look3 = Vector3.Lerp(barrelSideLook, downBarrelLook, arcT);

                    Quaternion rawRot3 = Quaternion.LookRotation((look3 - outPos).normalized, Vector3.up);
                    outRot = rawRot3 * Quaternion.Euler(0f, 0f, Mathf.Lerp(-6.5f, 2.2f, arcT));
                    outFov = Mathf.Lerp(41f, 46f, arcT);
                }
            }
            else if (jaegerCtrl.IsPlasmaRecoil)
            {
                if (!plasmaRecoilCaptured)
                {
                    plasmaRecoilCaptured = true;
                    plasmaRecoilStartPos = plasmaCamLastPos;
                    plasmaRecoilStartRot = plasmaCamLastRot;
                    plasmaRecoilStartFov = plasmaCamLastFov;
                }

                float r = jaegerCtrl.PlasmaRecoilProgress;
                float kickEnv = Mathf.Sin(Mathf.Clamp01(r) * Mathf.PI * 0.5f);
                // Punch backward & widen FOV slightly as the plasma bolt streaks past the lens
                outPos = plasmaRecoilStartPos + aimDir * (2.2f * kickEnv) + Vector3.up * (0.45f * kickEnv);
                Vector3 lookTarget = Vector3.Lerp(muzzlePos, chestPos + Vector3.up * 0.85f, 0.34f);
                Quaternion baseRecoilRot = Quaternion.LookRotation((lookTarget - outPos).normalized, Vector3.up) * Quaternion.Euler(0f, 0f, 2.2f);
                outRot = Quaternion.Slerp(plasmaRecoilStartRot, baseRecoilRot, Mathf.Clamp01(r * 2f));
                outFov = Mathf.Lerp(plasmaRecoilStartFov, 55f, kickEnv);
            }
            else
            {
                // Phase 4: Smoothly sweep back from the front-on barrel shot to standard over-the-shoulder
                if (!plasmaRecoverCaptured)
                {
                    plasmaRecoverCaptured = true;
                    plasmaRecoverStartPos = plasmaCamLastPos;
                    plasmaRecoverStartRot = plasmaCamLastRot;
                    plasmaRecoverStartFov = plasmaCamLastFov;
                }

                float recT = Mathf.SmoothStep(0f, 1f, jaegerCtrl.PlasmaRecoverProgress);
                // Swing laterally around Gipsy's right side during return so camera never passes through Gipsy's chest
                Vector3 sideApex = chestPos + sideRight * 14f + Vector3.up * 4.5f - aimDir * 4f;
                Vector3 p01 = Vector3.Lerp(plasmaRecoverStartPos, sideApex, recT);
                Vector3 p12 = Vector3.Lerp(sideApex, otsPos, recT);
                outPos = Vector3.Lerp(p01, p12, recT);

                // Pan gaze smoothly from Gipsy's muzzle/chest -> past right shoulder -> over-the-shoulder aim point (avoids 180-deg Slerp flip)
                Vector3 lookStart = Vector3.Lerp(muzzlePos, chestPos + Vector3.up * 0.85f, 0.34f);
                Vector3 lookMid = chestPos + aimDir * 16f + sideRight * 2f;
                Vector3 lookEnd = otsPos + (otsRot * Vector3.forward) * 60f;
                Vector3 l01 = Vector3.Lerp(lookStart, lookMid, recT);
                Vector3 l12 = Vector3.Lerp(lookMid, lookEnd, recT);
                Vector3 lookRec = Vector3.Lerp(l01, l12, recT);

                Vector3 recDir = (lookRec - outPos);
                Quaternion baseRecRot = recDir.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(recDir.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, Mathf.Lerp(2.2f, 0f, recT))
                    : otsRot;
                outRot = recT < 0.15f
                    ? Quaternion.Slerp(plasmaRecoverStartRot, baseRecRot, recT / 0.15f)
                    : (recT > 0.85f ? Quaternion.Slerp(baseRecRot, otsRot, (recT - 0.85f) / 0.15f) : baseRecRot);
                outFov = Mathf.Lerp(plasmaRecoverStartFov, otsFov, recT);
            }

            float minFloorY = jaegerCtrl.transform.position.y + 2.8f;
            if (outPos.y < minFloorY) outPos.y = minFloorY;

            plasmaCamLastPos = outPos;
            plasmaCamLastRot = outRot;
            plasmaCamLastFov = outFov;
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

        private Coroutine activeCutsceneTransition;
        public bool isCutsceneFreeCam = false;

        /// <summary>
        /// Activates high-altitude zoomed-out freecam during the aerial deployment cutscene,
        /// allowing the player to look around at the stormy skies, Chinooks, and Kaiju.
        /// </summary>
        public void EnableCutsceneFreeCam(float initialDistance = 72f, float initialPitch = 20f)
        {
            if (activeCutsceneTransition != null)
            {
                StopCoroutine(activeCutsceneTransition);
                activeCutsceneTransition = null;
            }

            isCutsceneFreeCam = true;
            isFreeCam = true;
            freeCamDistance = initialDistance;
            freeCamMinDistance = 12f;
            freeCamMaxDistance = 140f;
            currentPitch = initialPitch;
            if (cam != null) cam.fieldOfView = cutsceneFieldOfView;
            if (target != null) currentYaw = target.eulerAngles.y - 25f;
            hasSmoothedBasePos = false;
        }

        /// <summary>
        /// Seamlessly terminates cutscene freecam and glides the camera back to its natural
        /// over-the-shoulder combat position as the deployment cutscene concludes.
        /// </summary>
        public void SmoothTransitionToOverTheShoulder(float duration = 1.8f)
        {
            if (activeCutsceneTransition != null)
            {
                StopCoroutine(activeCutsceneTransition);
            }
            activeCutsceneTransition = StartCoroutine(CutsceneTransitionRoutine(duration));
        }

        private System.Collections.IEnumerator CutsceneTransitionRoutine(float duration)
        {
            isCutsceneFreeCam = false;
            float elapsed = 0f;

            float fromDist = freeCamDistance;
            float toDist = distance;

            float fromPitch = currentPitch;
            float toPitch = 14f;

            float fromYaw = currentYaw;
            float toYaw = target != null ? target.eulerAngles.y : currentYaw;
            float yawDiff = Mathf.DeltaAngle(fromYaw, toYaw);

            float fromFov = cutsceneFieldOfView;
            float toFov = baseFieldOfView;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

                freeCamDistance = Mathf.Lerp(fromDist, toDist, t);
                currentPitch = Mathf.Lerp(fromPitch, toPitch, t);
                currentYaw = fromYaw + yawDiff * t;
                if (cam != null)
                {
                    cam.fieldOfView = Mathf.Lerp(fromFov, toFov, t);
                }

                yield return null;
            }

            if (cam != null) cam.fieldOfView = baseFieldOfView;

            isFreeCam = false;
            freeCamDistance = distance;
            freeCamMinDistance = 4f;
            freeCamMaxDistance = 80f;
            activeCutsceneTransition = null;
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
            var jaegerCtrl = SimpleJaegerController.Instance;
            if (PacificRimUI.MainMenuUI.IsMenuOpen || PacificRimMeme.MemeCommandController.ShouldSuppressCrosshair || !showCrosshair || (jaegerCtrl != null && jaegerCtrl.IsPlasmaCinematicActive) || Event.current.type != EventType.Repaint) return;

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
