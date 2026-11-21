using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace UnderwaterReef
{
    /// <summary>
    /// Clickable 3D target for Quest 3 Right-Hand laser raycasting on World-Space UI tablets.
    /// </summary>
    public class VRClickableButton : MonoBehaviour
    {
        public System.Action onClick;
        public Image backgroundImage;
        public Color baseColor = new Color(0.08f, 0.24f, 0.36f, 0.92f);
        public Color hoverColor = new Color(0.20f, 0.72f, 0.95f, 0.98f);
        public Color selectedColor = new Color(0.18f, 0.85f, 0.55f, 0.98f);
        public bool isSelected = false;
        private bool isHovered = false;

        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
            RefreshVisual();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (backgroundImage == null) return;
            if (isSelected) backgroundImage.color = selectedColor;
            else if (isHovered) backgroundImage.color = hoverColor;
            else backgroundImage.color = baseColor;
        }
    }

    /// <summary>
    /// Complete Procedural Meta Quest 3 OpenXR Rig, Wrist Dive Computer HUD, and Controller Locomotion Driver.
    /// Zero external prefabs required:
    /// - Auto-activates stereoscopic 1st-person VR when XRSettings.isDeviceActive (Quest Link, Air Link, or Standalone APK).
    /// - Attaches Unity InputSystem TrackedPoseDriver to Camera.main for 90/120 Hz 6DOF HMD tracking.
    /// - Renders tracked Left & Right Quest 3 Touch Plus hands with bioluminescent Na'vi gauntlets and a right-hand laser pointer.
    /// - Mounts a 3D World-Space Wrist Dive Computer HUD on the Left Forearm displaying PPG BPM, Z-Score, HRV (RMSSD, LF/HF), RSA Breath Rate, O2, and Depth.
    /// - Falls back seamlessly to 3rd-person desktop play when no VR headset is connected.
    /// </summary>
    public class QuestVRRigController : MonoBehaviour
    {
        public static QuestVRRigController Instance { get; private set; }

        [Header("Meta Quest 3 VR Mode")]
        [Tooltip("Enable to preview the 1st-person XR rig and Wrist HUD in Unity Editor even without a plugged-in Quest 3.")]
        public bool forceVRRigPreviewInEditor = false;
        public bool isVRActive => XRSettings.isDeviceActive || forceVRRigPreviewInEditor;

        [Header("Quest 3 Ergonomics & Comfort")]
        public float eyeHeightOffset = 0.68f;
        public float snapTurnDegrees = 30f;
        public float raycastRange = 8.0f;
        public bool enableCardiacHaptics = true;

        [Header("Tracked Rig Anchors")]
        public Transform cameraOffsetTransform;
        public Transform leftHandAnchor;
        public Transform rightHandAnchor;

        // Polled Quest 3 Controller States
        public Vector2 leftStick { get; private set; }
        public Vector2 rightStick { get; private set; }
        public bool rightTriggerJustPressed { get; private set; }
        public bool rightPrimaryJustPressed { get; private set; }   // A Button
        public bool leftPrimaryJustPressed { get; private set; }    // X Button
        public bool leftSecondaryJustPressed { get; private set; }  // Y Button
        public float consumedSnapTurnDelta { get; private set; }

        private UnityEngine.XR.InputDevice leftDevice;
        private UnityEngine.XR.InputDevice rightDevice;
        private bool prevRightTrigger;
        private bool prevRightPrimary;
        private bool prevLeftPrimary;
        private bool prevLeftSecondary;
        private bool snapTurnReady = true;

        private bool rigInitialized = false;
        private TrackedPoseDriver hmdPoseDriver;
        private LineRenderer rightRayRenderer;
        private Transform rayCursorDot;
        private VRClickableButton currentHoveredButton;

        // World-Space Left Wrist Dive Computer
        private GameObject wristCanvasGO;
        private Text wristHeaderText;
        private Text wristTelemetryText;
        private Image wristOxygenBarFill;
        private Image wristCalibrationBarFill;
        private bool showWristHUD = true;

        // World-Space Floating Interaction Prompt
        private GameObject promptCanvasGO;
        private Text promptText;

        private DiverController diver;
        private Font cachedFont;
        private Sprite cachedWhiteSprite;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            diver = FindAnyObjectByType<DiverController>();
            if (HeartRateSystem.Instance != null)
            {
                HeartRateSystem.Instance.OnHeartbeat += HandleCardiacHaptic;
            }
        }

        private void OnDestroy()
        {
            if (HeartRateSystem.Instance != null)
            {
                HeartRateSystem.Instance.OnHeartbeat -= HandleCardiacHaptic;
            }
        }

        private void Update()
        {
            if (diver == null) diver = FindAnyObjectByType<DiverController>();

            if (isVRActive)
            {
                if (!rigInitialized && diver != null)
                {
                    InitializeQuestVRRig();
                }

                PollQuestControllers();
                UpdateHandPoses();
                UpdateRightHandLaserRaycast();
                UpdateWristDiveComputer();
                UpdateFloatingInteractionPrompt();
            }
            else if (rigInitialized)
            {
                // Keep desktop graceful if headset disconnects mid-session
                SetVRVisualsVisible(false);
            }
        }

        public void InitializeQuestVRRig()
        {
            if (diver == null) return;

            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            if (cameraOffsetTransform == null)
            {
                GameObject offsetGO = new GameObject("VRCameraOffset");
                offsetGO.transform.SetParent(diver.transform, false);
                offsetGO.transform.localPosition = new Vector3(0f, eyeHeightOffset, 0.14f);
                offsetGO.transform.localRotation = Quaternion.identity;
                cameraOffsetTransform = offsetGO.transform;
            }

            mainCam.transform.SetParent(cameraOffsetTransform, false);
            mainCam.transform.localPosition = Vector3.zero;
            mainCam.transform.localRotation = Quaternion.identity;
            mainCam.nearClipPlane = 0.05f; // Crisp wrist dive computer rendering without near clipping

            // Attach Unity InputSystem TrackedPoseDriver for Quest 3 OpenXR 6DOF head tracking
            hmdPoseDriver = mainCam.GetComponent<TrackedPoseDriver>();
            if (hmdPoseDriver == null)
            {
                hmdPoseDriver = mainCam.gameObject.AddComponent<TrackedPoseDriver>();
            }
            hmdPoseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            hmdPoseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            var posAction = new InputAction("XR_HMD_Pos", InputActionType.Value, "<XRHMD>/centerEyePosition");
            posAction.AddBinding("<XRHMD>/devicePosition");
            hmdPoseDriver.positionInput = new InputActionProperty(posAction);

            var rotAction = new InputAction("XR_HMD_Rot", InputActionType.Value, "<XRHMD>/centerEyeRotation");
            rotAction.AddBinding("<XRHMD>/deviceRotation");
            hmdPoseDriver.rotationInput = new InputActionProperty(rotAction);

            var stateAction = new InputAction("XR_HMD_TrackingState", InputActionType.Value, "<XRHMD>/trackingState");
            hmdPoseDriver.trackingStateInput = new InputActionProperty(stateAction);

            // Create Left & Right Quest 3 Hand Anchors
            BuildHandAnchor(true);
            BuildHandAnchor(false);
            BuildWristDiveComputer();
            BuildFloatingInteractionPrompt();

            ProceduralDiver procDiver = diver.GetComponent<ProceduralDiver>();
            if (procDiver != null)
            {
                procDiver.SetFirstPersonVRVisibility(true);
            }

            rigInitialized = true;
            SetVRVisualsVisible(true);
            Debug.Log("<color=#00f2fe>[Quest 3 VR]</color> Initialized OpenXR TrackedPoseDriver, Touch Plus Hand Anchors & 3D Wrist Dive Computer.");
        }

        private void SetVRVisualsVisible(bool visible)
        {
            if (leftHandAnchor != null) leftHandAnchor.gameObject.SetActive(visible);
            if (rightHandAnchor != null) rightHandAnchor.gameObject.SetActive(visible);
            if (wristCanvasGO != null) wristCanvasGO.SetActive(visible && showWristHUD);
            if (promptCanvasGO != null && !visible) promptCanvasGO.SetActive(false);

            if (diver != null)
            {
                ProceduralDiver procDiver = diver.GetComponent<ProceduralDiver>();
                if (procDiver != null) procDiver.SetFirstPersonVRVisibility(visible);
            }
        }

        private void BuildHandAnchor(bool isLeft)
        {
            string anchorName = isLeft ? "LeftHandAnchor" : "RightHandAnchor";
            GameObject handGO = new GameObject(anchorName);
            handGO.transform.SetParent(cameraOffsetTransform, false);
            handGO.transform.localPosition = new Vector3(isLeft ? -0.22f : 0.22f, -0.22f, 0.35f);

            // Procedural glowing Na'vi bio-gauntlet hand proxy
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material handMat = new Material(uLit);
            handMat.color = new Color(0.14f, 0.62f, 0.85f);
            handMat.EnableKeyword("_EMISSION");
            handMat.SetColor("_EmissionColor", new Color(0.1f, 0.8f, 1f) * 0.8f);

            GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            palm.name = "BiolumGauntlet";
            palm.transform.SetParent(handGO.transform, false);
            palm.transform.localScale = new Vector3(0.065f, 0.045f, 0.11f);
            palm.GetComponent<Renderer>().sharedMaterial = handMat;
            Collider col = palm.GetComponent<Collider>();
            if (col != null) Destroy(col);

            if (isLeft)
            {
                leftHandAnchor = handGO.transform;
            }
            else
            {
                rightHandAnchor = handGO.transform;

                // Right-hand cyan laser ray + tip dot for underwater aiming and 3D Survey clicking
                rightRayRenderer = handGO.AddComponent<LineRenderer>();
                rightRayRenderer.useWorldSpace = true;
                rightRayRenderer.positionCount = 2;
                rightRayRenderer.startWidth = 0.004f;
                rightRayRenderer.endWidth = 0.002f;
                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                Material rayMat = new Material(unlit);
                rayMat.color = new Color(0.2f, 0.95f, 1f, 0.85f);
                rightRayRenderer.sharedMaterial = rayMat;

                GameObject dotGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dotGO.name = "RayCursorDot";
                dotGO.transform.localScale = Vector3.one * 0.025f;
                dotGO.GetComponent<Renderer>().sharedMaterial = rayMat;
                Collider dotCol = dotGO.GetComponent<Collider>();
                if (dotCol != null) Destroy(dotCol);
                rayCursorDot = dotGO.transform;
                rayCursorDot.gameObject.SetActive(false);
            }
        }

        private void BuildWristDiveComputer()
        {
            if (leftHandAnchor == null) return;

            // Physical Titanium Dive Watch Casing mounted to Diver's Left Wrist
            GameObject casingGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            casingGO.name = "TitaniumWatchCasing";
            casingGO.transform.SetParent(leftHandAnchor, false);
            casingGO.transform.localPosition = new Vector3(0.03f, 0.075f, -0.02f);
            casingGO.transform.localRotation = Quaternion.Euler(48f, -15f, -10f);
            casingGO.transform.localScale = new Vector3(0.20f, 0.025f, 0.15f);

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material caseMat = new Material(litShader);
            caseMat.color = new Color(0.12f, 0.16f, 0.20f); // Dark titanium
            caseMat.SetFloat("_Metallic", 0.85f);
            caseMat.SetFloat("_Smoothness", 0.75f);
            casingGO.GetComponent<Renderer>().sharedMaterial = caseMat;
            Collider caseCol = casingGO.GetComponent<Collider>();
            if (caseCol != null) Destroy(caseCol);

            // Wrist Strap Bands
            GameObject strapGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strapGO.name = "WatchStrap";
            strapGO.transform.SetParent(casingGO.transform, false);
            strapGO.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            strapGO.transform.localScale = new Vector3(1.05f, 0.5f, 1.25f);
            strapGO.GetComponent<Renderer>().sharedMaterial = caseMat;
            Collider strapCol = strapGO.GetComponent<Collider>();
            if (strapCol != null) Destroy(strapCol);

            // World-Space OLED Screen Canvas
            wristCanvasGO = new GameObject("VRWristDiveComputer");
            wristCanvasGO.transform.SetParent(casingGO.transform, false);
            wristCanvasGO.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            wristCanvasGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            wristCanvasGO.transform.localScale = new Vector3(1f / 0.20f, 1f / 0.15f, 1f / 0.025f) * 0.00048f;

            Canvas canvas = wristCanvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rt = wristCanvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(380f, 280f);

            Image bg = wristCanvasGO.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.06f, 0.10f, 0.96f);

            Font font = GetSafeUIFont();

            wristHeaderText = CreateUIText(wristCanvasGO.transform, "WristHeader", font, 15, FontStyle.Bold,
                new Color(0.35f, 0.95f, 1f), new Vector2(190, -22), new Vector2(350, 28));
            wristHeaderText.text = "TSAHEYLU DIVE-OS // VR-4K";

            wristTelemetryText = CreateUIText(wristCanvasGO.transform, "WristStats", font, 13, FontStyle.Normal,
                Color.white, new Vector2(190, -125), new Vector2(350, 165));
            wristTelemetryText.alignment = TextAnchor.UpperLeft;

            // Calibration Bar
            wristCalibrationBarFill = CreateUIBar(wristCanvasGO.transform, "CalibBar",
                new Color(1f, 0.82f, 0.2f), new Vector2(190, -220), new Vector2(340, 12));

            // Oxygen Bar
            wristOxygenBarFill = CreateUIBar(wristCanvasGO.transform, "O2Bar",
                new Color(0.2f, 0.85f, 1f), new Vector2(190, -248), new Vector2(340, 16));
        }

        private void BuildFloatingInteractionPrompt()
        {
            if (cameraOffsetTransform == null) return;

            promptCanvasGO = new GameObject("VRFloatingPrompt");
            promptCanvasGO.transform.SetParent(cameraOffsetTransform, false);
            promptCanvasGO.transform.localScale = Vector3.one * 0.0012f;

            Canvas canvas = promptCanvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rt = promptCanvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(420f, 70f);

            Image bg = promptCanvasGO.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.1f, 0.16f, 0.85f);

            promptText = CreateUIText(promptCanvasGO.transform, "PromptLabel", GetSafeUIFont(), 18, FontStyle.Bold,
                new Color(0.3f, 1f, 0.8f), Vector2.zero, new Vector2(400f, 60f));
            promptText.alignment = TextAnchor.MiddleCenter;
            promptCanvasGO.SetActive(false);
        }

        private void PollQuestControllers()
        {
            if (!leftDevice.isValid) leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!rightDevice.isValid) rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            Vector2 lStick = Vector2.zero;
            Vector2 rStick = Vector2.zero;
            bool rTrig = false, rPrim = false, lPrim = false, lSec = false;

            if (leftDevice.isValid)
            {
                leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out lStick);
                leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out lPrim);
                leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out lSec);
            }

            if (rightDevice.isValid)
            {
                rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out rStick);
                if (rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool trigBool)) rTrig = trigBool;
                if (!rTrig && rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float trigVal)) rTrig = trigVal > 0.7f;
                rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out rPrim);
            }

            leftStick = lStick.sqrMagnitude > 0.01f ? lStick : Vector2.zero;
            rightStick = rStick.sqrMagnitude > 0.01f ? rStick : Vector2.zero;

            rightTriggerJustPressed = rTrig && !prevRightTrigger;
            rightPrimaryJustPressed = rPrim && !prevRightPrimary;
            leftPrimaryJustPressed = lPrim && !prevLeftPrimary;
            leftSecondaryJustPressed = lSec && !prevLeftSecondary;

            prevRightTrigger = rTrig;
            prevRightPrimary = rPrim;
            prevLeftPrimary = lPrim;
            prevLeftSecondary = lSec;

            // Comfort Snap Turn on Right Thumbstick X
            consumedSnapTurnDelta = 0f;
            if (snapTurnReady && Mathf.Abs(rightStick.x) > 0.65f)
            {
                consumedSnapTurnDelta = Mathf.Sign(rightStick.x) * snapTurnDegrees;
                snapTurnReady = false;
            }
            else if (Mathf.Abs(rightStick.x) < 0.25f)
            {
                snapTurnReady = true;
            }

            // Y button toggles Wrist Dive Computer visibility cleanly
            if (leftSecondaryJustPressed)
            {
                showWristHUD = !showWristHUD;
                if (wristCanvasGO != null) wristCanvasGO.SetActive(showWristHUD);
            }
        }

        private void UpdateHandPoses()
        {
            if (leftHandAnchor != null && leftDevice.isValid)
            {
                if (leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 lPos))
                    leftHandAnchor.localPosition = lPos;
                if (leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion lRot))
                    leftHandAnchor.localRotation = lRot;
            }

            if (rightHandAnchor != null && rightDevice.isValid)
            {
                if (rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 rPos))
                    rightHandAnchor.localPosition = rPos;
                if (rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion rRot))
                    rightHandAnchor.localRotation = rRot;
            }
        }

        private void UpdateRightHandLaserRaycast()
        {
            if (rightHandAnchor == null || rightRayRenderer == null) return;

            bool showRay = (InSituResearchQuestionnaire.Instance != null && InSituResearchQuestionnaire.Instance.isQuestionnaireActive)
                           || (diver != null && diver.focusedInteractable != null)
                           || forceVRRigPreviewInEditor;

            Ray ray = new Ray(rightHandAnchor.position, rightHandAnchor.forward);
            VRClickableButton hitButton = null;
            Vector3 endPoint = ray.origin + ray.direction * 3.5f;

            if (Physics.Raycast(ray, out RaycastHit hit, raycastRange))
            {
                endPoint = hit.point;
                hitButton = hit.collider.GetComponent<VRClickableButton>();
                if (hitButton != null) showRay = true;
            }

            if (currentHoveredButton != hitButton)
            {
                if (currentHoveredButton != null) currentHoveredButton.SetHovered(false);
                currentHoveredButton = hitButton;
                if (currentHoveredButton != null)
                {
                    currentHoveredButton.SetHovered(true);
                    SendHapticPulse(false, 0.12f, 0.04f);
                }
            }

            if (hitButton != null && (rightTriggerJustPressed || rightPrimaryJustPressed))
            {
                hitButton.onClick?.Invoke();
                SendHapticPulse(false, 0.45f, 0.08f);
            }

            rightRayRenderer.enabled = showRay;
            if (showRay)
            {
                rightRayRenderer.SetPosition(0, ray.origin);
                rightRayRenderer.SetPosition(1, endPoint);
            }

            if (rayCursorDot != null)
            {
                rayCursorDot.gameObject.SetActive(showRay && hitButton != null);
                if (hitButton != null) rayCursorDot.position = endPoint;
            }
        }

        private void UpdateWristDiveComputer()
        {
            if (wristCanvasGO == null || !wristCanvasGO.activeSelf) return;

            HeartRateSystem hr = HeartRateSystem.Instance;
            HRVEngine hrv = HRVEngine.Instance;
            ResearchStudyController study = ResearchStudyController.Instance;

            float bpm = hr != null ? hr.currentBPM : 72f;
            float z = hr != null ? hr.currentZScore : 0f;
            float o2 = hr != null ? hr.currentOxygen : 100f;
            float depth = diver != null ? diver.currentDepth : 0f;
            float rmssd = hrv != null ? hrv.rmssd : 45f;
            float lfHf = hrv != null ? hrv.lfHfRatio : 1.05f;
            float resp = hrv != null ? hrv.derivedRespirationBPM : 14.5f;

            bool isControl = study != null && study.activeCondition == ResearchStudyCondition.ConditionA_Control;
            bool isAmbientOnly = study != null && study.activeCondition == ResearchStudyCondition.ConditionC_AmbientAdaptive &&
                                 (UnderwaterHUD.Instance == null || !UnderwaterHUD.Instance.showResearcherOverlay);

            string condLine = study != null ? $"{study.participantID} | {study.activeCondition}" : "P01 | Bio-Adaptive VR";
            string calibLine = "";
            if (hr != null && hr.calibrationState == BaselineCalibrationState.Calibrating)
            {
                float rem = Mathf.Max(0f, hr.calibrationDuration - hr.calibrationTimer);
                calibLine = $"CALIBRATING BASELINE: {rem:F0}s\n";
                if (wristCalibrationBarFill != null)
                {
                    wristCalibrationBarFill.transform.parent.gameObject.SetActive(true);
                    wristCalibrationBarFill.fillAmount = hr.calibrationProgress;
                }
            }
            else
            {
                if (wristCalibrationBarFill != null) wristCalibrationBarFill.transform.parent.gameObject.SetActive(false);
                if (hr != null) calibLine = $"Z-Score: {z:+0.00;-0.00}σ (μ={hr.baselineMeanBPM:F0})\n";
            }

            string bioLine = (!isControl && !isAmbientOnly)
                ? $"♥ {Mathf.RoundToInt(bpm)} BPM  |  RMSSD: {rmssd:F0} ms\nLF/HF: {lfHf:F2}  |  RSA: {resp:F1} BrPM\n"
                : (isControl ? "BIO: [CONTROL CONDITION]\n" : "BIO: [AMBIENT ADAPTIVE MODE]\n");

            int ndl = depth <= 10f ? 99 : (depth <= 20f ? 45 : (depth <= 30f ? 18 : 8));
            float tankBar = o2 * 2.1f;
            string driTag = (hr != null && hr.isDivingReflexActive) ? $" | DRI: +{hr.mammalianDiveReflexIndex:F0}%" : "";
            string decoLine = $"DEPTH: {depth:F1} M  |  NDL: {ndl}m{driTag}  |  {tankBar:F0}B\n";
            string bondLine = (hrv != null) ? $"Φ BOND: {hrv.tsaheyluBondIndex:F0}% [{hrv.currentBondTier}] | SpO2: {hr?.currentSpO2:F0}%\n" : "";

            wristTelemetryText.text =
                $"{condLine}\n" +
                calibLine +
                bondLine +
                bioLine +
                decoLine +
                "L-Stick: Move | R-Stick Y: Dive | Trigger/A: Act | X: Sonar";

            if (wristOxygenBarFill != null)
            {
                wristOxygenBarFill.fillAmount = Mathf.Clamp01(o2 / 100f);
                wristOxygenBarFill.color = o2 > 25f ? new Color(0.2f, 0.85f, 1f) : new Color(0.95f, 0.25f, 0.2f);
            }
        }

        private void UpdateFloatingInteractionPrompt()
        {
            if (promptCanvasGO == null || diver == null || Camera.main == null) return;

            bool surveyOpen = InSituResearchQuestionnaire.Instance != null && InSituResearchQuestionnaire.Instance.isQuestionnaireActive;
            if (!surveyOpen && diver.focusedInteractable != null && diver.focusedInteractable.canInteract)
            {
                promptCanvasGO.SetActive(true);
                Transform camT = Camera.main.transform;
                promptCanvasGO.transform.position = camT.position + camT.forward * 1.4f + camT.up * -0.22f;
                promptCanvasGO.transform.rotation = Quaternion.LookRotation(camT.forward, camT.up);
                string pTxt = diver.focusedInteractable.interactionPrompt.Replace("[E]", "[Trigger / A]");
                promptText.text = pTxt.Contains("[") ? pTxt : $"[Trigger / A]  {pTxt}";
            }
            else if (!surveyOpen && diver.isRidingMount)
            {
                promptCanvasGO.SetActive(true);
                Transform camT = Camera.main.transform;
                promptCanvasGO.transform.position = camT.position + camT.forward * 1.4f + camT.up * -0.28f;
                promptCanvasGO.transform.rotation = Quaternion.LookRotation(camT.forward, camT.up);
                promptText.text = "[Trigger / A]  Dismount Creature";
            }
            else
            {
                promptCanvasGO.SetActive(false);
            }
        }

        private void HandleCardiacHaptic(float bpm)
        {
            if (!isVRActive || !enableCardiacHaptics) return;
            if (ResearchStudyController.Instance != null && !ResearchStudyController.Instance.isBiofeedbackActive) return;

            // Dual-phase clinical cardiac haptics: Systolic "Lub" + Diastolic "Dub" (160ms apart)
            float amp = Mathf.Lerp(0.06f, 0.18f, Mathf.Clamp01((bpm - 60f) / 70f));
            StartCoroutine(DualPhaseCardiacHapticRoutine(amp));
        }

        private System.Collections.IEnumerator DualPhaseCardiacHapticRoutine(float systolicAmp)
        {
            // 1. Systolic "Lub" impulse (ventricular contraction)
            SendHapticPulse(true, systolicAmp, 0.045f);
            yield return new WaitForSeconds(0.16f);
            // 2. Diastolic "Dub" rebound (aortic/pulmonic valve closure)
            SendHapticPulse(true, systolicAmp * 0.65f, 0.035f);
        }

        public void SendHapticPulse(bool isLeftHand, float amplitude, float durationSec)
        {
            UnityEngine.XR.InputDevice dev = isLeftHand ? leftDevice : rightDevice;
            if (dev.isValid && dev.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            {
                dev.SendHapticImpulse(0, Mathf.Clamp01(amplitude), durationSec);
            }
        }

        public static Font GetSafeUIFont()
        {
            if (Instance != null && Instance.cachedFont != null) return Instance.cachedFont;
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);
            if (Instance != null) Instance.cachedFont = f;
            return f;
        }

        public static Sprite GetWhiteSprite()
        {
            if (Instance != null && Instance.cachedWhiteSprite != null) return Instance.cachedWhiteSprite;
            Texture2D tex = new Texture2D(2, 2);
            tex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            if (Instance != null) Instance.cachedWhiteSprite = sp;
            return sp;
        }

        private Text CreateUIText(Transform parent, string objName, Font font, int size, FontStyle style, Color color, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            Text txt = go.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleLeft;
            return txt;
        }

        private Image CreateUIBar(Transform parent, string objName, Color fillColor, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject bgGO = new GameObject(objName + "_BG");
            bgGO.transform.SetParent(parent, false);
            RectTransform bgRt = bgGO.AddComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 1f);
            bgRt.anchorMax = new Vector2(0f, 1f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = anchoredPos;
            bgRt.sizeDelta = sizeDelta;
            Image bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.18f, 0.25f, 0.9f);

            GameObject fillGO = new GameObject(objName + "_Fill");
            fillGO.transform.SetParent(bgGO.transform, false);
            RectTransform fillRt = fillGO.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = GetWhiteSprite();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.color = fillColor;
            fillImg.fillAmount = 1f;
            return fillImg;
        }
    }
}
