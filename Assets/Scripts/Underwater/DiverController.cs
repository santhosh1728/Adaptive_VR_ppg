using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnderwaterReef
{
    /// <summary>
    /// Dual-Locomotion Metkayina Na'vi Avatar Controller:
    /// - 3D Underwater Swimming: 6DOF fluid propulsion aligned directly with camera view,
    ///   effortless surface diving, heart-rate-driven buoyancy, fin/tail flutter.
    /// - Seabed & Surface Platform Walking: Seamless transition when touching the ocean floor
    ///   or walking on the Metkayina stilt village boardwalks, upright posture, walking strides.
    /// - Effortless Surface Penetration: Diving back down into the ocean from the surface is instant
    ///   and responsive via camera aim + forward (W) or direct descend (Q/C/Ctrl).
    /// - Hard Safety Boundaries: Impenetrable seabed clamp & radial cove perimeter.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(ProceduralDiver))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class DiverController : MonoBehaviour
    {
        [Header("Locomotion Tuning")]
        public float forwardSwimForce = 16f;
        public float strafeSwimForce = 12f;
        public float verticalSwimForce = 14f;
        public float maxSwimSpeed = 9.0f;
        public float waterDrag = 2.2f;

        [Header("Seabed Walking")]
        public float walkForce = 22f;
        public float maxWalkSpeed = 5.5f;
        public float jumpThrustForce = 12f;
        public float seabedGroundFriction = 8f;

        [Header("Aim & Look Settings")]
        public float mouseSensitivity = 2.5f;
        public Transform cameraFollowTarget;
        public float cameraDistance = 4.0f;
        public float cameraHeight = 1.3f;
        public float cameraSmoothSpeed = 14f;

        [Header("Interaction")]
        public float interactRange = 4.8f;
        public LayerMask interactLayer = ~0;
        public ReefInteractable focusedInteractable { get; private set; }

        [Header("Locomotion & Safety State")]
        public bool isGrounded { get; private set; }
        public bool isGroundedPlatform { get; private set; }
        public float currentDepth { get; private set; }
        public float seaBedY { get; private set; } = -22f;
        public float worldRadius { get; private set; } = 85f;

        [Header("Tsaheylu Mount Riding")]
        public bool isRidingMount { get; private set; } = false;
        public Transform currentMountSeat { get; private set; }
        public ReefInteractable currentMount { get; private set; }

        public Vector3 GetMovementInput() => inputDirection;
        public float GetAimPitch() => pitch;
        public float GetAimYaw() => yaw;

        public void StartRidingMount(Transform seat, ReefInteractable mount)
        {
            isRidingMount = true;
            currentMountSeat = seat;
            currentMount = mount;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        public void StopRidingMount()
        {
            isRidingMount = false;
            currentMountSeat = null;
            currentMount = null;

            rb.isKinematic = false;
        }

        private Rigidbody rb;
        private CapsuleCollider playerCollider;
        private ProceduralDiver diverVisual;
        private Camera trailingCamera;

        private float pitch;
        private float yaw;
        private Vector3 inputDirection = Vector3.zero;
        private bool jumpOrAscendPressed = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false; // Custom underwater / seabed buoyancy physics
            rb.linearDamping = waterDrag;
            rb.angularDamping = 4f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            playerCollider = GetComponent<CapsuleCollider>();
            if (playerCollider == null) playerCollider = gameObject.AddComponent<CapsuleCollider>();
            playerCollider.height = 2.1f;
            playerCollider.radius = 0.45f;
            playerCollider.center = new Vector3(0f, 0f, 0f);

            diverVisual = GetComponent<ProceduralDiver>();

            // Setup camera target pivot
            if (cameraFollowTarget == null)
            {
                GameObject pivot = new GameObject("CameraFollowPivot");
                pivot.transform.SetParent(transform, false);
                pivot.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                cameraFollowTarget = pivot.transform;
            }

            SetupTrailingCamera();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (diverVisual != null)
            {
                diverVisual.BuildDiverModel();
            }

            if (ReefWorldGenerator.Instance != null)
            {
                seaBedY = ReefWorldGenerator.Instance.seaBedDepth;
                worldRadius = ReefWorldGenerator.Instance.worldRadius;
            }
        }

        private void SetupTrailingCamera()
        {
            trailingCamera = Camera.main;
            if (trailingCamera == null)
            {
                GameObject camGO = new GameObject("UnderwaterTrailingCamera");
                trailingCamera = camGO.AddComponent<Camera>();
                camGO.tag = "MainCamera";
                camGO.AddComponent<AudioListener>();
            }

            trailingCamera.clearFlags = CameraClearFlags.SolidColor;
            trailingCamera.backgroundColor = new Color(0.015f, 0.05f, 0.10f);
            trailingCamera.nearClipPlane = 0.15f;
            trailingCamera.fieldOfView = 65f;

            yaw = transform.eulerAngles.y;
            pitch = 10f;
        }

        private void Update()
        {
            if (ReefWorldGenerator.Instance != null)
            {
                seaBedY = ReefWorldGenerator.Instance.seaBedDepth;
                worldRadius = ReefWorldGenerator.Instance.worldRadius;
            }

            HandleLookInput();
            PollMovementInputs();
            HandleInteractionInput();
            CheckAimInteraction();

            // When riding mount, position and rotation are anchored to the saddle
            if (isRidingMount && currentMountSeat != null)
            {
                transform.position = currentMountSeat.position;
                transform.rotation = currentMountSeat.rotation;
                currentDepth = Mathf.Max(0f, -transform.position.y);
                if (diverVisual != null) diverVisual.UpdateLocomotionPosture(false, 16f);
                return;
            }

            // Calculate current depth below water surface (Y = 0)
            currentDepth = Mathf.Max(0f, -transform.position.y);

            // Dynamic Bathymetric Seabed Height
            float localSeabedY = ReefWorldGenerator.Instance != null ?
                ReefWorldGenerator.Instance.GetSeabedHeight(transform.position.x, transform.position.z) : seaBedY;

            // Ground checks:
            // 1. Seabed ground (bottom of the ocean floor)
            float minStandY = localSeabedY + 1.05f;
            bool depthGrounded = transform.position.y <= (localSeabedY + 1.35f);
            bool rayGrounded = Physics.Raycast(transform.position, Vector3.down, out RaycastHit groundHit, 1.45f);
            bool isGroundedSeabed = depthGrounded || (rayGrounded && groundHit.point.y <= (localSeabedY + 1.8f) && !groundHit.collider.isTrigger);

            // 2. Surface platform ground (stilt village docks, ramps, land)
            isGroundedPlatform = rayGrounded && groundHit.point.y > -0.6f && !groundHit.collider.isTrigger;

            isGrounded = isGroundedSeabed || isGroundedPlatform;

            // Hard Safety Clamping: never fall below seabed floor or exit the cove
            ApplyHardSafetyClamps(minStandY);

            // Update Avatar posture (Horizontal swimming vs upright walking)
            if (diverVisual != null)
            {
                diverVisual.UpdateLocomotionPosture(isGrounded, rb.linearVelocity.magnitude);
            }
        }

        private void LateUpdate()
        {
            UpdateTrailingCamera();
        }

        private void FixedUpdate()
        {
            if (isRidingMount) return;

            if (isGrounded)
            {
                HandleSeabedWalkPhysics();
            }
            else
            {
                if (transform.position.y > 0.45f)
                {
                    // High above water (e.g. jumped off dock) -> apply gravity back down towards surface
                    rb.AddForce(Vector3.down * 18f, ForceMode.Acceleration);
                }
                else
                {
                    HandleAbyssalSwimPhysics();
                }
            }
        }

        private void ApplyHardSafetyClamps(float minStandY)
        {
            Vector3 pos = transform.position;

            // 1. Bottom Floor Clamp: Strictly prevent sinking beneath seabed
            if (pos.y < minStandY)
            {
                pos.y = minStandY;
                if (rb.linearVelocity.y < 0f)
                {
                    Vector3 vel = rb.linearVelocity;
                    vel.y = 0f;
                    rb.linearVelocity = vel;
                }
            }

            // 2. Open Water Surface Ceiling Clamp
            // Allow natural breathing and breaching up to +0.25m, but clamp infinite sky ascent
            if (!isGrounded)
            {
                if (pos.y > 0.25f)
                {
                    pos.y = 0.25f;
                    if (rb.linearVelocity.y > 0f)
                    {
                        Vector3 vel = rb.linearVelocity;
                        vel.y = 0f;
                        rb.linearVelocity = vel;
                    }
                }
            }

            // 3. Radial Perimeter Clamp: Keep diver inside the reef world radius
            Vector2 xz = new Vector2(pos.x, pos.z);
            float maxR = worldRadius - 2f;
            if (xz.magnitude > maxR)
            {
                xz = xz.normalized * maxR;
                pos.x = xz.x;
                pos.z = xz.y;
            }

            transform.position = pos;
        }

        private void PollMovementInputs()
        {
            inputDirection = Vector3.zero;
            jumpOrAscendPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) inputDirection += Vector3.forward;
                if (Keyboard.current.sKey.isPressed) inputDirection += Vector3.back;
                if (Keyboard.current.aKey.isPressed) inputDirection += Vector3.left;
                if (Keyboard.current.dKey.isPressed) inputDirection += Vector3.right;
                if (Keyboard.current.qKey.isPressed || Keyboard.current.cKey.isPressed || Keyboard.current.leftCtrlKey.isPressed) inputDirection += Vector3.down;
                if (Keyboard.current.rKey.isPressed || Keyboard.current.spaceKey.isPressed)
                {
                    jumpOrAscendPressed = true;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetKey(KeyCode.W)) inputDirection += Vector3.forward;
                if (Input.GetKey(KeyCode.S)) inputDirection += Vector3.back;
                if (Input.GetKey(KeyCode.A)) inputDirection += Vector3.left;
                if (Input.GetKey(KeyCode.D)) inputDirection += Vector3.right;
                if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl)) inputDirection += Vector3.down;
                if (Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.Space))
                {
                    jumpOrAscendPressed = true;
                }
            }
            catch { }
#endif

            // Merge Meta Quest 3 Touch Plus thumbstick input
            if (QuestVRRigController.Instance != null && QuestVRRigController.Instance.isVRActive)
            {
                Vector2 ls = QuestVRRigController.Instance.leftStick;
                Vector2 rs = QuestVRRigController.Instance.rightStick;
                inputDirection += new Vector3(ls.x, 0f, ls.y);
                if (Mathf.Abs(rs.y) > 0.15f)
                {
                    if (rs.y > 0f && isGrounded) jumpOrAscendPressed = true;
                    else inputDirection += new Vector3(0f, rs.y, 0f);
                }
            }

            // Merge Procus VR Gaze / Capacitive Touch locomotion
            if (ProcusVRManager.Instance != null && ProcusVRManager.Instance.isProcusVREnabled)
            {
                if (ProcusVRManager.Instance.isSwimmingForward)
                {
                    inputDirection += Vector3.forward;
                }
            }

            if (inputDirection.sqrMagnitude > 1f) inputDirection.Normalize();
        }

        private void HandleSeabedWalkPhysics()
        {
            // Walking on Seabed or Stilt Village Boardwalks
            bool isVR = ((QuestVRRigController.Instance != null && QuestVRRigController.Instance.isVRActive) ||
                         (ProcusVRManager.Instance != null && ProcusVRManager.Instance.isProcusVREnabled)) && trailingCamera != null;
            Vector3 camForward;
            Vector3 camRight;
            if (isVR)
            {
                camForward = Vector3.ProjectOnPlane(trailingCamera.transform.forward, Vector3.up).normalized;
                camRight = Vector3.ProjectOnPlane(trailingCamera.transform.right, Vector3.up).normalized;
            }
            else
            {
                Quaternion camYaw = Quaternion.Euler(0f, yaw, 0f);
                camForward = camYaw * Vector3.forward;
                camRight = camYaw * Vector3.right;
            }
            Vector3 moveDir = (camForward * inputDirection.z + camRight * inputDirection.x).normalized;

            // Project along natural sand dune slope normal if hit
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit slopeHit, 2.0f))
            {
                moveDir = Vector3.ProjectOnPlane(moveDir, slopeHit.normal).normalized;
            }

            Vector3 moveForce = moveDir * (inputDirection.magnitude * walkForce);
            rb.AddForce(moveForce, ForceMode.Acceleration);

            // Ground anchoring force to keep feet planted while walking
            rb.AddForce(Vector3.down * seabedGroundFriction, ForceMode.Acceleration);

            // Jump / Push off seabed into swimming mode
            if (jumpOrAscendPressed)
            {
                rb.AddForce(Vector3.up * jumpThrustForce, ForceMode.Impulse);
            }

            // Clamp horizontal walk velocity
            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (horizontalVel.magnitude > maxWalkSpeed)
            {
                horizontalVel = horizontalVel.normalized * maxWalkSpeed;
                rb.linearVelocity = new Vector3(horizontalVel.x, rb.linearVelocity.y, horizontalVel.z);
            }
        }

        private void HandleAbyssalSwimPhysics()
        {
            // 3D Underwater Swimming aligned with Camera / Quest 3 / Procus HMD Aim
            bool isVR = ((QuestVRRigController.Instance != null && QuestVRRigController.Instance.isVRActive) ||
                         (ProcusVRManager.Instance != null && ProcusVRManager.Instance.isProcusVREnabled)) && trailingCamera != null;
            Vector3 camFwd;
            Vector3 camRight;
            if (isVR)
            {
                camFwd = trailingCamera.transform.forward;
                camRight = trailingCamera.transform.right;
            }
            else
            {
                Quaternion camRot = Quaternion.Euler(pitch, yaw, 0f);
                camFwd = camRot * Vector3.forward;
                camRight = camRot * Vector3.right;
            }

            // Pause swim drift while completing an in-VR Research Tablet survey
            if (InSituResearchQuestionnaire.Instance != null && InSituResearchQuestionnaire.Instance.isQuestionnaireActive && isVR)
            {
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 6f);
                return;
            }

            // 6DOF directional propulsion
            Vector3 swimVector = (camFwd * inputDirection.z * forwardSwimForce) +
                                 (camRight * inputDirection.x * strafeSwimForce) +
                                 (Vector3.up * inputDirection.y * verticalSwimForce);

            if (jumpOrAscendPressed)
            {
                swimVector += Vector3.up * verticalSwimForce;
            }

            // Check if player is commanding a dive
            bool isCommandingDive = (inputDirection.y < -0.1f) ||
                                    (inputDirection.z > 0.1f && camFwd.y < -0.05f) ||
                                    (inputDirection.z < -0.1f && camFwd.y > 0.05f);

            // Surface Diving Assist: instantly cuts under surface waves when commanding a dive
            if (transform.position.y >= -1.0f && isCommandingDive)
            {
                Vector3 currentVel = rb.linearVelocity;
                if (currentVel.y > -3.0f)
                {
                    currentVel.y = -4.5f;
                    rb.linearVelocity = currentVel;
                }
                swimVector += Vector3.down * (verticalSwimForce * 2.5f);
            }

            rb.AddForce(swimVector, ForceMode.Acceleration);

            // Cardiac Buoyancy: only active when NOT commanding a dive down
            ApplyHeartRateBuoyancy(isCommandingDive);

            // Clamp max swimming speed
            if (rb.linearVelocity.magnitude > maxSwimSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSwimSpeed;
            }
        }

        private void HandleLookInput()
        {
            bool isQuestVR = QuestVRRigController.Instance != null && QuestVRRigController.Instance.isVRActive;
            bool isProcusVR = ProcusVRManager.Instance != null && ProcusVRManager.Instance.isProcusVREnabled;

            if (isQuestVR)
            {
                // Quest 3 OpenXR comfort snap-turning; keep PlayerDiver upright (zero X/Z tilt) to prevent VR sickness
                yaw += QuestVRRigController.Instance.consumedSnapTurnDelta;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                return;
            }

            if (isProcusVR)
            {
                // In Procus VR, camera rotation is driven by smartphone gyroscope; align body yaw with gaze
                if (trailingCamera != null)
                {
                    Vector3 fwd = Vector3.ProjectOnPlane(trailingCamera.transform.forward, Vector3.up);
                    if (fwd.sqrMagnitude > 0.01f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwd), Time.deltaTime * 8f);
                    }
                }
                return;
            }

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
#endif

            Vector2 mouseDelta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                mouseDelta = Mouse.current.delta.ReadValue() * 0.1f;
            }
            if (Keyboard.current != null)
            {
                if (Keyboard.current.leftArrowKey.isPressed) mouseDelta.x -= 2.0f;
                if (Keyboard.current.rightArrowKey.isPressed) mouseDelta.x += 2.0f;
                if (Keyboard.current.upArrowKey.isPressed) mouseDelta.y += 1.8f;
                if (Keyboard.current.downArrowKey.isPressed) mouseDelta.y -= 1.8f;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                if (Input.GetKey(KeyCode.LeftArrow)) mouseDelta.x -= 2.0f;
                if (Input.GetKey(KeyCode.RightArrow)) mouseDelta.x += 2.0f;
                if (Input.GetKey(KeyCode.UpArrow)) mouseDelta.y += 1.8f;
                if (Input.GetKey(KeyCode.DownArrow)) mouseDelta.y -= 1.8f;
            }
            catch { }
#endif

            if (mouseDelta.sqrMagnitude > 0.0001f)
            {
                yaw += mouseDelta.x * mouseSensitivity;
                pitch -= mouseDelta.y * mouseSensitivity;
                pitch = Mathf.Clamp(pitch, -80f, 80f);
            }

            // In walking mode: body stays upright, rotating only along Y (yaw)
            // In swimming mode: body smoothly tilts with pitch for aerodynamic gliding
            if (isGrounded)
            {
                Quaternion targetRot = Quaternion.Euler(0f, yaw, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 12f);
            }
            else
            {
                Quaternion targetRot = Quaternion.Euler(pitch * 0.85f, yaw, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
            }
        }

        private void UpdateTrailingCamera()
        {
            if (QuestVRRigController.Instance != null && QuestVRRigController.Instance.isVRActive)
            {
                // HMD 6DOF tracking is driven by TrackedPoseDriver inside VRCameraOffset
                return;
            }

            if (ProcusVRManager.Instance != null && ProcusVRManager.Instance.isProcusVREnabled)
            {
                // First-Person VR View for Procus Smartphone VR (camera anchored at diver mask level)
                if (cameraFollowTarget != null && trailingCamera != null)
                {
                    trailingCamera.transform.position = cameraFollowTarget.position + Vector3.up * 0.45f;
                }
                return;
            }

            if (trailingCamera == null || cameraFollowTarget == null) return;

            Vector3 targetPos = cameraFollowTarget.position;

            // In walking mode, slightly higher camera offset; in mount mode, wider cinematic view
            float effectiveHeight = isRidingMount ? cameraHeight + 0.85f : (isGrounded ? cameraHeight + 0.35f : cameraHeight);
            float effectiveDist = isRidingMount ? cameraDistance + 2.2f : (isGrounded ? cameraDistance + 0.2f : cameraDistance);

            Quaternion camRot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desiredCamPos = targetPos - (camRot * Vector3.forward * effectiveDist) + (Vector3.up * effectiveHeight);

            // Keep camera above bathymetric seabed as well
            float localCamSeabedY = ReefWorldGenerator.Instance != null ?
                ReefWorldGenerator.Instance.GetSeabedHeight(desiredCamPos.x, desiredCamPos.z) : seaBedY;
            float minCamY = localCamSeabedY + 0.6f;
            if (desiredCamPos.y < minCamY) desiredCamPos.y = minCamY;

            trailingCamera.transform.position = Vector3.Lerp(trailingCamera.transform.position, desiredCamPos, Time.deltaTime * cameraSmoothSpeed);
            trailingCamera.transform.rotation = camRot;
        }

        private void ApplyHeartRateBuoyancy(bool isCommandingDive)
        {
            if (HeartRateSystem.Instance == null) return;

            // When user is actively commanding a dive, NEVER resist with upward buoyancy
            if (isCommandingDive) return;

            // High HR (stress/panic) increases buoyancy, gently pushing diver toward surface
            // Low HR (calm dive reflex) allows effortless descent into the abyss
            float hrBuoyancy = HeartRateSystem.Instance.buoyancyModifier;
            rb.AddForce(Vector3.up * hrBuoyancy * 3.2f, ForceMode.Acceleration);
        }

        private void CheckAimInteraction()
        {
            if (trailingCamera == null) return;

            bool isVR = QuestVRRigController.Instance != null && QuestVRRigController.Instance.isVRActive;
            Ray ray;
            if (isVR && QuestVRRigController.Instance.rightHandAnchor != null)
            {
                Transform hand = QuestVRRigController.Instance.rightHandAnchor;
                ray = new Ray(hand.position, hand.forward);
            }
            else
            {
                ray = trailingCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            }
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, isVR ? interactRange + 2.0f : interactRange, interactLayer))
            {
                ReefInteractable interactable = hit.collider.GetComponentInParent<ReefInteractable>();
                if (interactable != null && interactable.canInteract)
                {
                    focusedInteractable = interactable;
                    return;
                }
            }

            // Proximity check fallback
            Collider[] overlaps = Physics.OverlapSphere(transform.position, 2.5f);
            foreach (var col in overlaps)
            {
                ReefInteractable nearby = col.GetComponentInParent<ReefInteractable>();
                if (nearby != null && nearby.canInteract)
                {
                    focusedInteractable = nearby;
                    return;
                }
            }

            focusedInteractable = null;
        }

        private void HandleInteractionInput()
        {
            bool ePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                ePressed = Keyboard.current.eKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetKeyDown(KeyCode.E)) ePressed = true;
            }
            catch { }
#endif
            if (QuestVRRigController.Instance != null && QuestVRRigController.Instance.isVRActive)
            {
                if (InSituResearchQuestionnaire.Instance != null && InSituResearchQuestionnaire.Instance.isQuestionnaireActive)
                {
                    // Laser clicks handle 3D questionnaire while active
                    return;
                }
                if (QuestVRRigController.Instance.rightTriggerJustPressed || QuestVRRigController.Instance.rightPrimaryJustPressed)
                {
                    ePressed = true;
                }
            }

            if (ePressed)
            {
                if (isRidingMount)
                {
                    if (currentMount is MantaCompanionAI manta) manta.Dismount();
                    else if (currentMount is SkimwingMountAI skim) skim.Dismount();
                    else if (MantaCompanionAI.Instance != null && MantaCompanionAI.Instance.isMounted) MantaCompanionAI.Instance.Dismount();
                    else if (SkimwingMountAI.Instance != null && SkimwingMountAI.Instance.isMounted) SkimwingMountAI.Instance.Dismount();
                    else StopRidingMount();
                    return;
                }

                if (focusedInteractable != null)
                {
                    focusedInteractable.Interact(this);
                    if (QuestVRRigController.Instance != null) QuestVRRigController.Instance.SendHapticPulse(false, 0.35f, 0.08f);
                }
            }
        }
    }
}
