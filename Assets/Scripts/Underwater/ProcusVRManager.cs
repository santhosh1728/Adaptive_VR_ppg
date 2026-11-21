using System;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnderwaterReef
{
    /// <summary>
    /// Procus VR (Smartphone / Mobile VR) Stereoscopic & Gyroscopic Manager.
    /// Supports:
    /// 1. Procus ONE, Procus PRO, and all Google Cardboard / smartphone VR headsets.
    /// 2. Pure Procedural Side-By-Side (SBS) Dual-Camera Stereoscopic Rendering (zero external SDK dependencies).
    /// 3. Mobile Phone 3DOF Gyroscope Head Tracking (Pitch, Yaw, Roll).
    /// 4. Hands-Free Gaze-Dwell Interaction: Look at Pearls, Spirit Trees, Mounts, or Questionnaire buttons for 1.2s to interact.
    /// 5. Procus Capacitive Touch Button: Pressing the physical headset button taps the phone screen to trigger instant actions.
    /// 6. Gaze-Directed Ocean Swimming: Diver swims smoothly in the direction the player is looking through the Procus lenses.
    /// 7. Floating Stereoscopic Bio-HUD: Displays live Heart Rate, Z-score, HRV, Phi Bond, Depth, and Oxygen at comfortable focal distance.
    /// 8. Editor Preview Toggle: Press F10 in Unity to test the Procus Side-By-Side split screen on PC.
    /// </summary>
    public class ProcusVRManager : MonoBehaviour
    {
        public static ProcusVRManager Instance { get; private set; }

        [Header("Procus VR State")]
        [Tooltip("Enables Side-by-Side (SBS) stereo split-screen and mobile gyro tracking for Procus headsets.")]
        public bool isProcusVREnabled = false;

        [Header("Optical Alignment & Stereoscopy")]
        [Tooltip("Interpupillary Distance in meters (Default: 0.064m / 64mm for Procus 40-42mm lenses)")]
        public float ipdMeters = 0.064f;
        [Tooltip("Field of View for each eye camera matching Procus optical lenses (85° - 95°)")]
        public float procusFOV = 92f;
        [Tooltip("Distance in meters where eyes naturally converge (Default: 2.5m). Eliminates double-vision!")]
        public float convergenceDistance = 2.5f;
        [Tooltip("True if phone is flipped 180° in headset (Landscape Right instead of Landscape Left)")]
        public bool invertLandscape = false;
        [Tooltip("Horizontal viewport inset offset (0.0 to 0.15) for wide aspect-ratio phone screens")]
        public float lensSeparationOffset = 0.02f;
        public int orientationMode = 0; // 0 = Landscape Left, 1 = Landscape Right (180), 2 = Portrait
        public float pcHeadYaw = 0f;
        public float pcHeadPitch = 0f;

        [Header("Locomotion & Swimming")]
        [Tooltip("Toggle auto-cruise forward swimming (Double-tap screen or press C)")]
        public bool autoCruiseSwim = false;
        public bool isSwimmingForward { get; private set; }
        private float lastTapTime = -1f;

        [Header("Gaze-Dwell Interaction")]
        [Tooltip("Time in seconds looking at an object before it automatically triggers interaction")]
        public float dwellDuration = 1.25f;
        public float interactionRange = 6.0f;
        public float currentDwellTimer = 0f;
        public float dwellProgress01 => Mathf.Clamp01(currentDwellTimer / Mathf.Max(0.1f, dwellDuration));

        private Camera mainCam;
        private Camera leftEyeCam;
        private Camera rightEyeCam;

        private DiverController diver;
        private ReefInteractable currentGazeInteractable;
        private VRClickableButton currentGazeButton;

        // Gyro calibration
        private bool gyroSupported = false;
        private Quaternion gyroBaseRotation = Quaternion.identity;
        private float gyroYawOffset = 0f;

        // Floating 3D Procus Bio-HUD
        private GameObject procusHudGO;
        private Text hudStatsText;
        private Image hudO2Bar;
        private Image hudBondBar;
        private Image gazeReticleFill;
        private GameObject gazeCanvasGO;

        // Procedural Textures
        private Texture2D whiteTex;
        private Texture2D ringTex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            GenerateTextures();

            // Auto-enable Procus mode on Android if not a Quest standalone headset
            if (Application.platform == RuntimePlatform.Android)
            {
                // If OpenXR headset (Quest) is NOT active, default to Procus Smartphone VR
                if (QuestVRRigController.Instance == null || !QuestVRRigController.Instance.isVRActive)
                {
                    isProcusVREnabled = true;
                }
            }
        }

        private void Start()
        {
            mainCam = Camera.main;
            diver = FindAnyObjectByType<DiverController>();

            // Initialize Smartphone Gyroscope
            if (SystemInfo.supportsGyroscope)
            {
                Input.gyro.enabled = true;
                Input.gyro.updateInterval = 0.0167f; // 60 Hz gyro sampling
                gyroSupported = true;
                gyroBaseRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            if (isProcusVREnabled)
            {
                EnableProcusStereoRig(true);
            }
        }

        private void Update()
        {
            // Calibration Hotkeys
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f10Key.wasPressedThisFrame) ToggleProcusVR();
                if (kb.rKey.wasPressedThisFrame) RecenterGyro();
                if (kb.iKey.wasPressedThisFrame) ToggleInvertLandscape();
                if (kb.tKey.wasPressedThisFrame) CycleOrientationMode();
                if (kb.leftBracketKey.wasPressedThisFrame) AdjustIPD(-0.002f);
                if (kb.rightBracketKey.wasPressedThisFrame) AdjustIPD(0.002f);
                if (kb.minusKey.wasPressedThisFrame) AdjustLensSeparation(-0.005f);
                if (kb.equalsKey.wasPressedThisFrame) AdjustLensSeparation(0.005f);
            }
#else
            if (Input.GetKeyDown(KeyCode.F10)) ToggleProcusVR();
            if (Input.GetKeyDown(KeyCode.R)) RecenterGyro();
            if (Input.GetKeyDown(KeyCode.I)) ToggleInvertLandscape();
            if (Input.GetKeyDown(KeyCode.T)) CycleOrientationMode();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) AdjustIPD(-0.002f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) AdjustIPD(0.002f);
            if (Input.GetKeyDown(KeyCode.Minus)) AdjustLensSeparation(-0.005f);
            if (Input.GetKeyDown(KeyCode.Equals)) AdjustLensSeparation(0.005f);
#endif

            if (!isProcusVREnabled) return;

            UpdateMobileGyroTracking();
            UpdateCameraStereoConvergence();
            UpdateLocomotion();
            UpdateGazeDwellInteraction();
            UpdateProcusFloatingHUD();
        }

        public void ToggleProcusVR()
        {
            isProcusVREnabled = !isProcusVREnabled;
            EnableProcusStereoRig(isProcusVREnabled);
            Debug.Log($"<color=#00f2fe>[Procus VR]</color> Mobile VR Mode is now: {(isProcusVREnabled ? "ACTIVE (SBS Stereo + Gyro)" : "OFF (Desktop)")}");
        }

        #region SBS Dual-Camera Stereoscopic Rig

        private void EnableProcusStereoRig(bool enable)
        {
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;

            if (enable)
            {
                // Create Left Eye Camera
                if (leftEyeCam == null)
                {
                    GameObject leftGO = new GameObject("ProcusLeftEyeCamera");
                    leftGO.transform.SetParent(mainCam.transform, false);
                    leftGO.transform.localPosition = new Vector3(-ipdMeters * 0.5f, 0f, 0f);
                    leftGO.transform.localRotation = Quaternion.identity;

                    leftEyeCam = leftGO.AddComponent<Camera>();
                    leftEyeCam.CopyFrom(mainCam);
                    leftEyeCam.rect = new Rect(0f, 0f, 0.5f, 1f); // Left half of screen
                    leftEyeCam.fieldOfView = procusFOV;
                }
                leftEyeCam.gameObject.SetActive(true);

                // Create Right Eye Camera
                if (rightEyeCam == null)
                {
                    GameObject rightGO = new GameObject("ProcusRightEyeCamera");
                    rightGO.transform.SetParent(mainCam.transform, false);
                    rightGO.transform.localPosition = new Vector3(ipdMeters * 0.5f, 0f, 0f);
                    rightGO.transform.localRotation = Quaternion.identity;

                    rightEyeCam = rightGO.AddComponent<Camera>();
                    rightEyeCam.CopyFrom(mainCam);
                    rightEyeCam.rect = new Rect(0.5f, 0f, 0.5f, 1f); // Right half of screen
                    rightEyeCam.fieldOfView = procusFOV;
                }
                rightEyeCam.gameObject.SetActive(true);

                // Disable rendering on main camera (audio listener remains active)
                mainCam.cullingMask = 0;

                // Build Floating 3D Bio-HUD & Reticle
                BuildProcusFloatingHUD();
            }
            else
            {
                if (leftEyeCam != null) leftEyeCam.gameObject.SetActive(false);
                if (rightEyeCam != null) rightEyeCam.gameObject.SetActive(false);
                mainCam.cullingMask = ~0; // Restore all layers

                if (procusHudGO != null) procusHudGO.SetActive(false);
                if (gazeCanvasGO != null) gazeCanvasGO.SetActive(false);
            }
        }

        #endregion

        #region Smartphone Gyroscope Head Tracking

        private void UpdateMobileGyroTracking()
        {
            if (mainCam == null) return;

            if (gyroSupported && Input.gyro.enabled)
            {
                // Mobile Hardware Gyroscope (Android / iOS)
                Quaternion gyroAttitude = Input.gyro.attitude;
                Quaternion unityRot = new Quaternion(gyroAttitude.x, gyroAttitude.y, -gyroAttitude.z, -gyroAttitude.w);
                Quaternion rawRot;

                if (orientationMode == 1 || invertLandscape)
                {
                    // 180° inverted landscape (Landscape Right)
                    rawRot = Quaternion.Euler(90f, 0f, 180f) * unityRot;
                }
                else if (orientationMode == 2)
                {
                    // Portrait orientation (if phone held vertically)
                    rawRot = Quaternion.Euler(0f, 0f, 90f) * unityRot;
                }
                else
                {
                    // Standard Landscape Left (default Procus Cardboard)
                    rawRot = Quaternion.Euler(90f, 0f, 0f) * unityRot;
                }

                Quaternion targetCamRot = Quaternion.Euler(0f, -gyroYawOffset, 0f) * rawRot;
                mainCam.transform.localRotation = targetCamRot;
            }
            else
            {
                // Desktop PC & Streaming Fallback (Steam Link, Moonlight, Spacedesk, Mouse, Arrows)
                // This ensures head alignment and rotation WORK EVEN ON PC STREAMING!
                Vector2 lookDelta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null)
                {
                    lookDelta = Mouse.current.delta.ReadValue() * 0.15f;
                }
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.upArrowKey.isPressed) lookDelta.y += 2.0f;
                    if (kb.downArrowKey.isPressed) lookDelta.y -= 2.0f;
                    if (kb.leftArrowKey.isPressed) lookDelta.x -= 2.5f;
                    if (kb.rightArrowKey.isPressed) lookDelta.x += 2.5f;
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                try
                {
                    lookDelta += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 3f;
                    if (Input.GetKey(KeyCode.UpArrow)) lookDelta.y += 2.0f;
                    if (Input.GetKey(KeyCode.DownArrow)) lookDelta.y -= 2.0f;
                    if (Input.GetKey(KeyCode.LeftArrow)) lookDelta.x -= 2.5f;
                    if (Input.GetKey(KeyCode.RightArrow)) lookDelta.x += 2.5f;
                }
                catch { }
#endif

                pcHeadYaw += lookDelta.x;
                pcHeadPitch -= lookDelta.y;
                pcHeadPitch = Mathf.Clamp(pcHeadPitch, -82f, 82f);

                mainCam.transform.localRotation = Quaternion.Euler(pcHeadPitch, pcHeadYaw - gyroYawOffset, 0f);
            }

            // Single tap on empty space / Procus capacitive touch button re-centers forward orientation
            if (Input.touchCount == 1 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began)
            {
                if (currentGazeInteractable == null && currentGazeButton == null)
                {
                    RecenterGyro();
                }
            }

            // 3-finger tap flips landscape orientation 180° (if phone is inserted in reverse)
            if (Input.touchCount == 3 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began)
            {
                ToggleInvertLandscape();
            }
        }

        public void RecenterGyro()
        {
            if (mainCam != null)
            {
                if (gyroSupported && Input.gyro.enabled)
                {
                    Quaternion gyroAttitude = Input.gyro.attitude;
                    Quaternion unityRot = new Quaternion(gyroAttitude.x, gyroAttitude.y, -gyroAttitude.z, -gyroAttitude.w);
                    Quaternion rawRot = Quaternion.Euler(90f, 0f, 0f) * unityRot;
                    gyroYawOffset = rawRot.eulerAngles.y;
                }
                else
                {
                    gyroYawOffset = pcHeadYaw;
                    pcHeadPitch = 0f;
                }
                Debug.Log($"<color=#00f2fe>[Procus VR]</color> Head alignment re-centered! Current heading is now Forward. (Yaw Offset: {gyroYawOffset:F1}°)");
            }
        }

        public void CycleOrientationMode()
        {
            orientationMode = (orientationMode + 1) % 3;
            RecenterGyro();
            string modeName = orientationMode == 0 ? "Landscape Left (Standard)" :
                             (orientationMode == 1 ? "Landscape Right (180° Inverted)" : "Portrait / 90° Stream");
            Debug.Log($"<color=#00f2fe>[Procus VR]</color> Phone Orientation Mode: {modeName}");
        }

        public void ToggleInvertLandscape()
        {
            invertLandscape = !invertLandscape;
            RecenterGyro();
            Debug.Log($"<color=#00f2fe>[Procus VR]</color> Landscape Inversion: {(invertLandscape ? "180° FLIPPED (Landscape Right)" : "STANDARD (Landscape Left)")}");
        }

        public void AdjustIPD(float deltaMeters)
        {
            ipdMeters = Mathf.Clamp(ipdMeters + deltaMeters, 0.050f, 0.078f);
            Debug.Log($"<color=#00f2fe>[Procus VR]</color> IPD Adjusted: {ipdMeters * 1000f:F1} mm");
        }

        public void AdjustLensSeparation(float delta)
        {
            lensSeparationOffset = Mathf.Clamp(lensSeparationOffset + delta, -0.06f, 0.10f);
            Debug.Log($"<color=#00f2fe>[Procus VR]</color> Lens Separation Offset: {lensSeparationOffset:F3} (Adjusted for phone screen width)");
        }

        public void AdjustConvergence(float deltaMeters)
        {
            convergenceDistance = Mathf.Clamp(convergenceDistance + deltaMeters, 1.0f, 10.0f);
            Debug.Log($"<color=#00f2fe>[Procus VR]</color> Stereo Convergence Distance: {convergenceDistance:F2} m");
        }

        private void UpdateCameraStereoConvergence()
        {
            if (mainCam == null || leftEyeCam == null || rightEyeCam == null) return;

            // Wide-screen smartphone optical centering:
            // Shifts viewports inward so optical centers match Procus 62-66mm lenses!
            float leftOffset = Mathf.Max(0f, lensSeparationOffset);
            float eyeW = 0.5f - leftOffset;

            leftEyeCam.rect = new Rect(leftOffset, 0f, eyeW, 1f);
            rightEyeCam.rect = new Rect(0.5f, 0f, eyeW, 1f);

            // Parallel Optical Axes to strictly prevent vertical parallax & double vision
            leftEyeCam.transform.localPosition = new Vector3(-ipdMeters * 0.5f, 0f, 0f);
            rightEyeCam.transform.localPosition = new Vector3(ipdMeters * 0.5f, 0f, 0f);
            leftEyeCam.transform.localRotation = Quaternion.identity;
            rightEyeCam.transform.localRotation = Quaternion.identity;

            leftEyeCam.fieldOfView = procusFOV;
            rightEyeCam.fieldOfView = procusFOV;
        }

        private void UpdateLocomotion()
        {
            // Double-tap screen or 'C' key toggles Auto-Cruise forward swimming
            bool tapBegan = (Input.touchCount > 0 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began) ||
                            Input.GetMouseButtonDown(0);
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.cKey.wasPressedThisFrame) autoCruiseSwim = !autoCruiseSwim;
#else
            if (Input.GetKeyDown(KeyCode.C)) autoCruiseSwim = !autoCruiseSwim;
#endif

            if (tapBegan)
            {
                if (Time.time - lastTapTime < 0.35f)
                {
                    autoCruiseSwim = !autoCruiseSwim;
                    Debug.Log($"<color=#00f2fe>[Procus VR]</color> Auto-Cruise Swim: {(autoCruiseSwim ? "ENABLED" : "DISABLED")}");
                }
                lastTapTime = Time.time;
            }

            // Holding screen or Procus capacitive button (or holding mouse button in editor)
            bool isHoldingTrigger = (Input.touchCount > 0 && (Input.GetTouch(0).phase == UnityEngine.TouchPhase.Stationary || Input.GetTouch(0).phase == UnityEngine.TouchPhase.Moved)) ||
                                    Input.GetMouseButton(0);

            // Swimming forward if holding trigger or auto-cruise is active
            isSwimmingForward = autoCruiseSwim || isHoldingTrigger;

            // Pause swimming if actively filling a questionnaire
            if (InSituResearchQuestionnaire.Instance != null && InSituResearchQuestionnaire.Instance.isQuestionnaireActive)
            {
                isSwimmingForward = false;
            }
        }

        #endregion

        #region Hands-Free Gaze-Dwell Interaction

        private void UpdateGazeDwellInteraction()
        {
            if (mainCam == null) return;

            Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
            ReefInteractable hitInteractable = null;
            VRClickableButton hitButton = null;

            if (Physics.Raycast(ray, out RaycastHit hit, interactionRange))
            {
                hitInteractable = hit.collider.GetComponentInParent<ReefInteractable>();
                hitButton = hit.collider.GetComponent<VRClickableButton>();
            }

            bool isLookingAtTarget = (hitInteractable != null && hitInteractable.canInteract) || hitButton != null;

            if (isLookingAtTarget)
            {
                currentDwellTimer += Time.deltaTime;

                // Update Hover on Button
                if (hitButton != currentGazeButton)
                {
                    if (currentGazeButton != null) currentGazeButton.SetHovered(false);
                    currentGazeButton = hitButton;
                    if (currentGazeButton != null) currentGazeButton.SetHovered(true);
                }

                // Check Procus Capacitive Button Tap OR Gaze-Dwell Complete (1.25s)
                bool triggerPressed = (Input.touchCount > 0 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began) ||
                                      Input.GetMouseButtonDown(0);

                if (currentDwellTimer >= dwellDuration || triggerPressed)
                {
                    ExecuteGazeInteraction(hitInteractable, hitButton);
                    currentDwellTimer = 0f;
                }
            }
            else
            {
                currentDwellTimer = Mathf.MoveTowards(currentDwellTimer, 0f, Time.deltaTime * 3f);
                if (currentGazeButton != null)
                {
                    currentGazeButton.SetHovered(false);
                    currentGazeButton = null;
                }
            }

            currentGazeInteractable = hitInteractable;

            // Update Gaze Reticle Fill Animation
            if (gazeReticleFill != null)
            {
                gazeReticleFill.fillAmount = dwellProgress01;
                gazeReticleFill.color = isLookingAtTarget ? new Color(0.2f, 1f, 0.75f) : new Color(0.4f, 0.85f, 1f, 0.5f);
            }
        }

        private void ExecuteGazeInteraction(ReefInteractable interactable, VRClickableButton button)
        {
            if (diver == null) diver = FindAnyObjectByType<DiverController>();

            if (interactable != null && interactable.canInteract)
            {
                interactable.Interact(diver);
                if (UnderwaterAudio.Instance != null) UnderwaterAudio.Instance.PlayPearlChime();
                Debug.Log($"<color=#00f2fe>[Procus VR]</color> Gaze activated: {interactable.interactionPrompt}");
            }
            else if (button != null)
            {
                button.onClick?.Invoke();
                if (UnderwaterAudio.Instance != null) UnderwaterAudio.Instance.PlayCompassTick();
                Debug.Log("<color=#00f2fe>[Procus VR]</color> Gaze clicked survey tablet button.");
            }
        }

        #endregion

        #region Floating Stereoscopic 3D Bio-HUD

        private void BuildProcusFloatingHUD()
        {
            if (mainCam == null) return;

            // 1. Gaze Center Reticle Canvas (pinned at 1.4m in front of eyes)
            if (gazeCanvasGO == null)
            {
                gazeCanvasGO = new GameObject("ProcusGazeReticleCanvas");
                gazeCanvasGO.transform.SetParent(mainCam.transform, false);
                gazeCanvasGO.transform.localPosition = new Vector3(0f, 0f, 1.4f);
                gazeCanvasGO.transform.localRotation = Quaternion.identity;
                gazeCanvasGO.transform.localScale = Vector3.one * 0.0018f;

                Canvas canvas = gazeCanvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                RectTransform rt = gazeCanvasGO.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(100f, 100f);

                // Background ring
                GameObject ringGO = new GameObject("ReticleRing");
                ringGO.transform.SetParent(gazeCanvasGO.transform, false);
                Image ringImg = ringGO.AddComponent<Image>();
                ringImg.sprite = Sprite.Create(ringTex, new Rect(0, 0, ringTex.width, ringTex.height), new Vector2(0.5f, 0.5f));
                ringImg.color = new Color(0.3f, 0.7f, 0.9f, 0.45f);
                ringGO.GetComponent<RectTransform>().sizeDelta = new Vector2(48f, 48f);

                // Filling progress ring
                GameObject fillGO = new GameObject("ReticleFill");
                fillGO.transform.SetParent(gazeCanvasGO.transform, false);
                gazeReticleFill = fillGO.AddComponent<Image>();
                gazeReticleFill.sprite = Sprite.Create(ringTex, new Rect(0, 0, ringTex.width, ringTex.height), new Vector2(0.5f, 0.5f));
                gazeReticleFill.type = Image.Type.Filled;
                gazeReticleFill.fillMethod = Image.FillMethod.Radial360;
                gazeReticleFill.fillOrigin = (int)Image.Origin360.Top;
                gazeReticleFill.fillAmount = 0f;
                gazeReticleFill.color = new Color(0.2f, 1f, 0.85f);
                fillGO.GetComponent<RectTransform>().sizeDelta = new Vector2(48f, 48f);
            }
            gazeCanvasGO.SetActive(true);

            // 2. Lower Floating Bio-HUD Panel (pinned at 1.6m down in diver's lower periphery)
            if (procusHudGO == null)
            {
                procusHudGO = new GameObject("ProcusFloatingBioHUD");
                procusHudGO.transform.SetParent(mainCam.transform, false);
                procusHudGO.transform.localPosition = new Vector3(0f, -0.42f, 1.65f);
                procusHudGO.transform.localRotation = Quaternion.Euler(15f, 0f, 0f); // Tilted up towards eyes
                procusHudGO.transform.localScale = Vector3.one * 0.0014f;

                Canvas hCanvas = procusHudGO.AddComponent<Canvas>();
                hCanvas.renderMode = RenderMode.WorldSpace;
                RectTransform hRt = procusHudGO.GetComponent<RectTransform>();
                hRt.sizeDelta = new Vector2(440f, 140f);

                Image bg = procusHudGO.AddComponent<Image>();
                bg.color = new Color(0.03f, 0.08f, 0.14f, 0.88f);

                Font font = GetSafeUIFont();

                // Title
                CreateUIText(procusHudGO.transform, "Title", font, 13, FontStyle.Bold, new Color(0.3f, 0.95f, 1f),
                    new Vector2(0f, 48f), new Vector2(420f, 26f), TextAnchor.MiddleCenter);

                // Telemetry Text
                hudStatsText = CreateUIText(procusHudGO.transform, "Stats", font, 12, FontStyle.Normal, Color.white,
                    new Vector2(0f, 4f), new Vector2(410f, 60f), TextAnchor.MiddleCenter);

                // Oxygen Bar
                hudO2Bar = CreateUIBar(procusHudGO.transform, "O2Bar", new Color(0.2f, 0.85f, 1f),
                    new Vector2(0f, -42f), new Vector2(380f, 10f));

                // Tsaheylu Bio-Bond Bar
                hudBondBar = CreateUIBar(procusHudGO.transform, "BondBar", new Color(0.1f, 1f, 0.8f),
                    new Vector2(0f, -56f), new Vector2(380f, 8f));
            }
            procusHudGO.SetActive(true);
        }

        private void UpdateProcusFloatingHUD()
        {
            if (hudStatsText == null) return;

            HeartRateSystem hr = HeartRateSystem.Instance;
            HRVEngine hrv = HRVEngine.Instance;
            ESP32HeartRateBridge bridge = ESP32HeartRateBridge.Instance;

            bool hwActive = bridge != null && bridge.isHardwareConnected;
            bool fingerPlaced = bridge != null && bridge.isFingerPlaced;

            float bpm = hr != null ? hr.currentBPM : 72f;
            float z = hr != null ? hr.currentZScore : 0f;
            float o2 = hr != null ? hr.currentOxygen : 100f;
            float depth = diver != null ? diver.currentDepth : 0f;
            float phi = hrv != null ? hrv.tsaheyluBondIndex : 70f;
            string tier = hrv != null ? hrv.currentBondTier.ToString().ToUpper() : "HARMONIZING";

            string swimState = autoCruiseSwim ? "CRUISE: ON" : (isSwimmingForward ? "SWIMMING" : "HOLD BUTTON TO SWIM");
            string promptHint = currentGazeInteractable != null ?
                $"LOOK TO ACT: {currentGazeInteractable.interactionPrompt} ({Mathf.RoundToInt(dwellProgress01 * 100f)}%)" :
                $"GAZE TO INTERACT | {swimState} | 2x-TAP: AUTO-CRUISE";

            // If hardware is active but finger is not placed, alert user directly in headset
            string sensorStatusTag = "○ PPG: DEMO";
            string hrDisplay = $"♥ {Mathf.RoundToInt(bpm)}";

            if (hwActive)
            {
                if (fingerPlaced)
                {
                    sensorStatusTag = $"● PPG: {bridge.hardwareLabel} [LOCKED]";
                    if (hr != null && hr.calibrationState == BaselineCalibrationState.Calibrating)
                    {
                        promptHint = $"⏳ CALIBRATING BASELINE ({Mathf.RoundToInt(hr.calibrationProgress * 100f)}%) — Keep finger still & breathe gently";
                    }
                }
                else
                {
                    sensorStatusTag = "⚠️ PPG: NO FINGER DETECTED";
                    hrDisplay = "♡ --";
                    promptHint = "⚠️ FINGER OFF SENSOR! Please place your finger pad on the optical LED.";
                }
            }

            hudStatsText.text =
                $"{hrDisplay} BPM  |  Z: {z:+0.00;-0.00}σ  |  DEPTH: {depth:F1} M  |  O2: {Mathf.RoundToInt(o2)}%\n" +
                $"Φ BOND: {phi:F0}% [{tier}]  |  {sensorStatusTag}\n" +
                $"{promptHint}";

            if (hudO2Bar != null)
            {
                hudO2Bar.fillAmount = Mathf.Clamp01(o2 / 100f);
                hudO2Bar.color = o2 > 25f ? new Color(0.2f, 0.85f, 1f) : Color.red;
            }

            if (hudBondBar != null && hrv != null)
            {
                hudBondBar.fillAmount = Mathf.Clamp01(phi / 100f);
                hudBondBar.color = hrv.tsaheyluBondColor;
            }
        }

        private void OnGUI()
        {
            if (!isProcusVREnabled) return;

            // Draw central optical alignment notch & line for positioning phone in Procus tray
            float cx = Screen.width * 0.5f;
            if (whiteTex == null) GenerateTextures();

            // Subtle vertical dividing line (matches headset physical plastic septum)
            GUI.color = new Color(0.1f, 0.7f, 1.0f, 0.35f);
            GUI.DrawTexture(new Rect(cx - 1f, 0f, 2f, Screen.height), whiteTex);

            // Top alignment notch
            GUI.color = new Color(0.2f, 1.0f, 0.7f, 0.75f);
            GUI.DrawTexture(new Rect(cx - 12f, 10f, 24f, 4f), whiteTex);

            // Bottom alignment notch
            GUI.DrawTexture(new Rect(cx - 12f, Screen.height - 14f, 24f, 4f), whiteTex);

            // Stereo Optical Alignment Reticles in center of both eyes
            // When user looks through Procus lenses, these two cyan crosshairs fuse into ONE!
            float leftEyeCenterX = (Screen.width * 0.25f) + (lensSeparationOffset * Screen.width * 0.5f);
            float rightEyeCenterX = (Screen.width * 0.75f) - (lensSeparationOffset * Screen.width * 0.5f);
            float eyeCenterY = Screen.height * 0.5f;

            GUI.color = new Color(0.2f, 0.9f, 1.0f, 0.45f);
            DrawCrosshairReticle(leftEyeCenterX, eyeCenterY);
            DrawCrosshairReticle(rightEyeCenterX, eyeCenterY);
            GUI.color = Color.white;
        }

        private void DrawCrosshairReticle(float x, float y)
        {
            GUI.DrawTexture(new Rect(x - 7f, y - 1f, 14f, 2f), whiteTex);
            GUI.DrawTexture(new Rect(x - 1f, y - 7f, 2f, 14f), whiteTex);
        }

        #endregion

        #region Procedural Helpers

        private void GenerateTextures()
        {
            whiteTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            whiteTex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
            whiteTex.Apply();

            int rSize = 64;
            ringTex = new Texture2D(rSize, rSize, TextureFormat.RGBA32, false);
            Vector2 rCenter = new Vector2(rSize * 0.5f, rSize * 0.5f);
            float rOuter = rSize * 0.46f;
            float rInner = rSize * 0.36f;

            for (int y = 0; y < rSize; y++)
            {
                for (int x = 0; x < rSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), rCenter);
                    if (dist >= rInner && dist <= rOuter)
                    {
                        ringTex.SetPixel(x, y, Color.white);
                    }
                    else
                    {
                        ringTex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            ringTex.Apply();
        }

        private Text CreateUIText(Transform parent, string objName, Font font, int size, FontStyle style, Color color, Vector2 anchoredPos, Vector2 sizeDelta, TextAnchor alignment)
        {
            GameObject go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            Text txt = go.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            return txt;
        }

        private Image CreateUIBar(Transform parent, string objName, Color fillColor, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject bgGO = new GameObject(objName + "_BG");
            bgGO.transform.SetParent(parent, false);
            RectTransform bgRt = bgGO.AddComponent<RectTransform>();
            bgRt.anchoredPosition = anchoredPos;
            bgRt.sizeDelta = sizeDelta;
            Image bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.15f, 0.22f, 0.85f);

            GameObject fillGO = new GameObject(objName + "_Fill");
            fillGO.transform.SetParent(bgGO.transform, false);
            RectTransform fillRt = fillGO.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = Sprite.Create(whiteTex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.color = fillColor;
            fillImg.fillAmount = 1f;
            return fillImg;
        }

        private Font GetSafeUIFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 14);
            return f;
        }

        #endregion
    }
}
