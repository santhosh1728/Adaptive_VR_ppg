using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnderwaterReef
{
    /// <summary>
    /// In-game Pause & Bio-Sensor Configuration Menu.
    /// Opens with 'Esc' or 'P'.
    /// Features:
    /// - Resting Heart Rate (RHR) 10-Second Baseline Calibration
    /// - Sensor Mode Toggle (Live ESP32 vs Keyboard Demo)
    /// - COM Port selector & Mouse Sensitivity adjustments
    /// - Resume & Restart dive options
    /// </summary>
    public class UnderwaterPauseMenu : MonoBehaviour
    {
        public static UnderwaterPauseMenu Instance { get; private set; }

        public bool isPaused = false;

        [Header("Calibration")]
        public bool isCalibrating = false;
        public float calibrationTimer = 0f;
        private float calibrationSum = 0f;
        private int calibrationSamples = 0;

        private Texture2D bgTex;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.04f, 0.08f, 0.14f, 0.94f));
            bgTex.Apply();
        }

        private void Update()
        {
            HandlePauseInput();
            UpdateCalibration();
        }

        private void HandlePauseInput()
        {
            bool escPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                escPressed = Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) escPressed = true;
            }
            catch { }
#endif

            if (escPressed)
            {
                TogglePause();
            }
        }

        public void TogglePause()
        {
            isPaused = !isPaused;
            Time.timeScale = isPaused ? 0f : 1f;

            Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isPaused;
        }

        private void UpdateCalibration()
        {
            if (isCalibrating)
            {
                calibrationTimer -= Time.unscaledDeltaTime;
                HeartRateSystem hr = HeartRateSystem.Instance;
                if (hr != null)
                {
                    calibrationSum += hr.currentBPM;
                    calibrationSamples++;
                }

                if (calibrationTimer <= 0f)
                {
                    isCalibrating = false;
                    if (calibrationSamples > 0 && hr != null)
                    {
                        hr.restingBPM = Mathf.Round(calibrationSum / calibrationSamples);
                        Debug.Log($"<color=#00ff88>[Calibration Complete]</color> New Resting Baseline: {hr.restingBPM} BPM");
                    }
                }
            }
        }

        public void StartCalibration()
        {
            isCalibrating = true;
            calibrationTimer = 10f;
            calibrationSum = 0f;
            calibrationSamples = 0;
        }

        private void OnGUI()
        {
            if (!isPaused) return;

            InitStyles();

            // Dim background
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bgTex);

            Rect panelRect = new Rect((Screen.width - 480) * 0.5f, (Screen.height - 520) * 0.5f, 480, 520);
            DrawBorder(panelRect, new Color(0.2f, 0.8f, 0.95f));

            GUILayout.BeginArea(new Rect(panelRect.x + 25, panelRect.y + 25, panelRect.width - 50, panelRect.height - 50));

            GUILayout.Label("REEF RESCUE - SETTINGS & BIO-CONFIG", titleStyle);
            GUILayout.Space(12);

            // 1. Calibration Section
            GUILayout.Label("1. PHYSIOLOGICAL CALIBRATION", labelStyle);
            HeartRateSystem hr = HeartRateSystem.Instance;
            if (isCalibrating)
            {
                GUILayout.Label($"Calibrating Baseline... {calibrationTimer:F1}s remaining (Breathe normally)", labelStyle);
            }
            else
            {
                GUILayout.Label($"Current Resting Baseline: {(hr != null ? hr.restingBPM : 68)} BPM", labelStyle);
                if (GUILayout.Button("Calibrate Personal Resting Heart Rate (10s)", buttonStyle, GUILayout.Height(30)))
                {
                    StartCalibration();
                }
            }

            GUILayout.Space(14);

            // 2. Hardware Mode
            GUILayout.Label("2. SENSOR INPUT MODE", labelStyle);
            ESP32HeartRateBridge bridge = ESP32HeartRateBridge.Instance;
            if (bridge != null)
            {
                bool isHW = bridge.isHardwareConnected;
                GUILayout.Label($"Status: {(isHW ? "● ESP32 Hardware Active" : "○ Demo Mode Active (Keyboard Space/Shift)")}", labelStyle);
                GUILayout.Label($"Serial Port: {bridge.portName} ({bridge.baudRate} baud)", labelStyle);
            }

            GUILayout.Space(14);

            // 3. Controls & Flashlight info
            GUILayout.Label("3. CONTROLS GUIDE", labelStyle);
            GUILayout.Label("WASD: Swim  |  Mouse: Look  |  F: Flashlight", labelStyle);
            GUILayout.Label("E: Interact  |  Q/R: Depth  |  Shift: Calm  |  Space: Stress", labelStyle);

            GUILayout.Space(20);

            // Buttons
            if (GUILayout.Button("Resume Dive", buttonStyle, GUILayout.Height(36)))
            {
                TogglePause();
            }

            GUILayout.Space(6);

            if (GUILayout.Button("Restart Dive", buttonStyle, GUILayout.Height(32)))
            {
                TogglePause();
                ReefGameManager.Instance?.RestartDive();
            }

            GUILayout.EndArea();
        }

        private void InitStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0.3f, 0.95f, 1f);

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12
            };
            labelStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
        }

        private void DrawBorder(Rect rect, Color borderCol)
        {
            GUI.color = borderCol;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - 2, rect.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width - 2, rect.y, 2, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}

