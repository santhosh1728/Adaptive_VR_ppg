using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnderwaterReef
{
    public enum BaselineCalibrationState
    {
        Uncalibrated,
        Calibrating,
        Calibrated
    }

    /// <summary>
    /// Manages player biofeedback (Heart Rate in BPM) and Personalized Z-Score Calibration.
    /// Supports:
    /// 1. Clinical Baseline Normalization (Z-Score): User-specific resting HR calibration.
    /// 2. Demo Mode: Space increases HR (stress), Shift decreases HR (calm).
    /// 3. ESP32 / PPG Serial connection framework.
    /// 4. Diving physiology: Depth buoyancy, oxygen consumption rate, and calm diving reflex.
    /// </summary>
    public class HeartRateSystem : MonoBehaviour
    {
        public static HeartRateSystem Instance { get; private set; }

        [Header("Heart Rate Parameters")]
        public float currentBPM = 72f;
        public float restingBPM = 68f;
        public float minBPM = 48f;
        public float maxBPM = 160f;

        [Header("Clinical Baseline Normalization (Z-Score)")]
        [Tooltip("Resting calibration status: captures personal mu and sigma to eliminate individual baseline bias.")]
        public BaselineCalibrationState calibrationState = BaselineCalibrationState.Calibrating;
        public float calibrationDuration = 30f; // 30 seconds resting baseline window
        public float calibrationTimer = 0f;
        public float baselineMeanBPM = 70f;
        public float baselineStdDevBPM = 5.5f;
        public float currentZScore = 0f;
        public float calmZScoreThreshold = -0.4f;
        public float stressZScoreThreshold = 1.25f;
        public float calibrationProgress => calibrationDuration > 0 ? Mathf.Clamp01(calibrationTimer / calibrationDuration) : 1f;

        private List<float> calibrationSamples = new List<float>();

        [Header("Demo Simulation Speeds")]
        public float stressIncreaseRate = 35f; // BPM per sec holding Space
        public float calmDecreaseRate = 25f;   // BPM per sec holding Shift
        public float naturalReturnRate = 12f;  // BPM drift towards resting

        [Header("Physiological States")]
        public bool isCalm;
        public bool isStressed;
        public float stressFactor; // 0 (calm) to 1 (max stress)

        [Header("Diving Physiology Impact")]
        [Tooltip("Buoyancy offset: high HR causes upward floating/struggle, low HR allows controlled deep diving")]
        public float buoyancyModifier = 0f;
        [Tooltip("Rate at which oxygen is consumed multiplier (1.0 = normal, 2.5 = high stress)")]
        public float oxygenBurnRate = 1.0f;

        [Header("Mammalian Diving Reflex (DRI) & Real SpO2")]
        [Tooltip("Peripheral capillary oxygen saturation (92% - 100%)")]
        public float currentSpO2 = 98.4f;
        [Tooltip("Mammalian Diving Reflex Index: % cardiac deceleration from resting baseline upon submersion")]
        public float mammalianDiveReflexIndex = 0f;
        [Tooltip("True when diving reflex bradycardia is active (DRI >= 12%), extending oxygen reserve by 45%")]
        public bool isDivingReflexActive = false;
        public float diveReflexO2Discount => isDivingReflexActive ? 0.55f : 1.0f;

        [Header("Oxygen Tank")]
        public float maxOxygen = 100f;
        public float currentOxygen = 100f;
        public bool isOutOfOxygen;

        [Header("ESP32 / PPG Sensor Settings")]
        public bool useHardwareSensor = false;
        public string serialPortName = "COM3";
        public int baudRate = 115200;
        public float targetHardwareBPM = 72f;
        public bool hasHardwareBPM = false;
        [Tooltip("Harmonic mode: 0 = Auto DSP De-Aliasing, 1 = Force 2x Pulse, 2 = Force 0.5x Pulse. Toggle anytime with '2' key.")]
        public int manualHarmonicMode = 0; // 0=Auto, 1=Force 2x, 2=Force 0.5x
        public bool harmonicDoubleRate => manualHarmonicMode == 1;
        private float lastValidBPM = 70f;
        private List<float> recentBpmWindow = new List<float>();

        public event Action<float> OnHeartbeat;
        private float lastBeatTime;
        private float beatInterval => 60f / Mathf.Clamp(currentBPM, 40f, 200f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            currentOxygen = maxOxygen;
        }

        private void Update()
        {
            // B Key restarts 30-second personalized resting baseline calibration anytime
            // 2 Key cycles Harmonic Mode: 0 = Auto DSP De-Aliasing | 1 = Force 2x | 2 = Force 0.5x
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.bKey.wasPressedThisFrame) StartCalibration(30f);
                if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
                {
                    manualHarmonicMode = (manualHarmonicMode + 1) % 3;
                    string modeStr = manualHarmonicMode == 0 ? "AUTO DSP (De-Aliasing On)" : (manualHarmonicMode == 1 ? "FORCE 2x (Double ~46 to ~92)" : "FORCE 0.5x (Halve ~124 to ~62)");
                    Debug.Log($"<color=#38ef7d>[HeartRateSystem]</color> Harmonic Pulse Mode: {modeStr}");
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.B)) StartCalibration(30f);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                manualHarmonicMode = (manualHarmonicMode + 1) % 3;
                string modeStr = manualHarmonicMode == 0 ? "AUTO DSP (De-Aliasing On)" : (manualHarmonicMode == 1 ? "FORCE 2x (Double ~46 to ~92)" : "FORCE 0.5x (Halve ~124 to ~62)");
                Debug.Log($"<color=#38ef7d>[HeartRateSystem]</color> Harmonic Pulse Mode: {modeStr}");
            }
#endif

            HandleHeartRateInput();
            UpdatePhysiology();
            UpdateOxygen();
            TriggerHeartbeatPulse();
        }

        private void HandleHeartRateInput()
        {
            if (useHardwareSensor && hasHardwareBPM)
            {
                // Smooth biological damping: smoothly glide currentBPM to targetHardwareBPM
                // 5.0f * Time.deltaTime provides natural, stable cardiac dynamics without violent jitter
                currentBPM = Mathf.MoveTowards(currentBPM, targetHardwareBPM, 5.0f * Time.deltaTime);
            }
            else
            {
                bool spaceHeld = false;
                bool shiftHeld = false;

#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null)
                {
                    spaceHeld = Keyboard.current.spaceKey.isPressed;
                    shiftHeld = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                try
                {
                    if (Input.GetKey(KeyCode.Space)) spaceHeld = true;
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) shiftHeld = true;
                }
                catch { }
#endif

                if (spaceHeld)
                {
                    // Stress simulation
                    currentBPM = Mathf.MoveTowards(currentBPM, maxBPM, stressIncreaseRate * Time.deltaTime);
                }
                else if (shiftHeld)
                {
                    // Calm simulation
                    currentBPM = Mathf.MoveTowards(currentBPM, minBPM, calmDecreaseRate * Time.deltaTime);
                }
                else
                {
                    // Slowly normalize towards resting rate with subtle biological respiratory sinus arrhythmia
                    float rsa = Mathf.Sin(Time.time * 1.5f) * 0.8f;
                    float target = restingBPM + rsa;
                    currentBPM = Mathf.MoveTowards(currentBPM, target, naturalReturnRate * Time.deltaTime);
                }
            }
        }

        private void UpdatePhysiology()
        {
            // Active Baseline Calibration Logic
            if (calibrationState == BaselineCalibrationState.Calibrating)
            {
                calibrationTimer += Time.deltaTime;
                if (currentBPM >= 40f && currentBPM <= 190f)
                {
                    if (calibrationSamples.Count == 0 || Time.frameCount % 4 == 0)
                    {
                        calibrationSamples.Add(currentBPM);
                    }
                }

                if (calibrationTimer >= calibrationDuration && calibrationSamples.Count >= 10)
                {
                    ComputeBaselineStatistics();
                }
            }

            if (calibrationState == BaselineCalibrationState.Calibrated)
            {
                // Clinical Z-Score Normalization with physiological lower variance guard
                float safeStdDev = Mathf.Max(baselineStdDevBPM, 4.5f);
                currentZScore = Mathf.Clamp((currentBPM - baselineMeanBPM) / safeStdDev, -3.5f, 3.5f);
                stressFactor = Mathf.Clamp01(Mathf.InverseLerp(calmZScoreThreshold, stressZScoreThreshold, currentZScore));
                isCalm = currentZScore <= calmZScoreThreshold;
                isStressed = currentZScore >= stressZScoreThreshold;
            }
            else
            {
                // Provisional calibration fallback
                stressFactor = Mathf.InverseLerp(restingBPM - 10f, maxBPM, currentBPM);
                isCalm = currentBPM <= restingBPM - 5f;
                isStressed = currentBPM >= restingBPM + 22f;
                currentZScore = Mathf.Clamp((currentBPM - restingBPM) / 10f, -3.5f, 3.5f);
            }

            // Physiology affects diving buoyancy:
            // Stress causes buoyant struggle upward (buoyancy > 0)
            // Calm enables effortless neutral/deep dive gliding (buoyancy < 0)
            float targetBuoyancy = Mathf.Clamp(currentZScore * 0.85f, -1.8f, 2.5f);
            buoyancyModifier = targetBuoyancy;

            // Mammalian Diving Reflex (DRI) & Metkayina Diving Bradycardia
            DiverController diver = FindAnyObjectByType<DiverController>();
            bool isSubmerged = diver != null && diver.transform.position.y < -2.0f;
            if (isSubmerged && baselineMeanBPM > 40f)
            {
                // DRI = % deceleration from resting baseline upon submersion
                mammalianDiveReflexIndex = Mathf.Clamp(((baselineMeanBPM - currentBPM) / baselineMeanBPM) * 100f, -50f, 60f);
                isDivingReflexActive = mammalianDiveReflexIndex >= 12.0f; // Significant parasympathetic bradycardia
            }
            else
            {
                mammalianDiveReflexIndex = Mathf.MoveTowards(mammalianDiveReflexIndex, 0f, Time.deltaTime * 10f);
                isDivingReflexActive = false;
            }

            // Continuous SpO2 Peripheral Pulse Oximetry
            // Resting: 98.8% | Under deep submersion + hypoxic breath-holding: safely dips to ~92.5%, recovering on surface
            float targetSpO2 = isSubmerged ? Mathf.Lerp(98.8f, 92.5f, stressFactor * (1f - (currentOxygen / 100f))) : 99.1f;
            currentSpO2 = Mathf.Lerp(currentSpO2, targetSpO2, Time.deltaTime * 0.4f);

            // Oxygen burn rate scales with cardiac exertion, discounted by 45% if Mammalian Diving Reflex is active!
            float rawBurnRate = Mathf.Lerp(0.6f, 3.2f, stressFactor);
            oxygenBurnRate = rawBurnRate * diveReflexO2Discount;
        }

        public void StartCalibration(float duration = 30f)
        {
            calibrationDuration = duration;
            calibrationTimer = 0f;
            calibrationSamples.Clear();
            calibrationState = BaselineCalibrationState.Calibrating;
            Debug.Log($"<color=#38ef7d>[HeartRateSystem]</color> Started personalized resting baseline calibration ({duration:F0}s)...");
        }

        public void SkipCalibration(float defaultMean = 70f, float defaultStd = 6f)
        {
            baselineMeanBPM = defaultMean;
            baselineStdDevBPM = defaultStd;
            restingBPM = defaultMean;
            calibrationState = BaselineCalibrationState.Calibrated;
            Debug.Log($"<color=#38ef7d>[HeartRateSystem]</color> Calibration skipped. Using baseline {defaultMean:F1} ± {defaultStd:F1} BPM.");
        }

        private void ComputeBaselineStatistics()
        {
            if (calibrationSamples.Count == 0) return;
            float sum = 0f;
            for (int i = 0; i < calibrationSamples.Count; i++) sum += calibrationSamples[i];
            baselineMeanBPM = sum / calibrationSamples.Count;
            restingBPM = baselineMeanBPM;

            float sumSq = 0f;
            for (int i = 0; i < calibrationSamples.Count; i++)
            {
                float diff = calibrationSamples[i] - baselineMeanBPM;
                sumSq += diff * diff;
            }
            baselineStdDevBPM = Mathf.Max(Mathf.Sqrt(sumSq / calibrationSamples.Count), 4.5f);
            calibrationState = BaselineCalibrationState.Calibrated;
            Debug.Log($"<color=#38ef7d>[HeartRateSystem]</color> Personalized Baseline Complete! Mean: {baselineMeanBPM:F1} BPM, StdDev: {baselineStdDevBPM:F1} BPM.");
        }

        private void UpdateOxygen()
        {
            var diver = FindAnyObjectByType<DiverController>();
            bool isAtSurface = diver != null && diver.transform.position.y >= -1.5f;

            if (isAtSurface)
            {
                // Surfacing breathes fresh surface air and refills the tank
                currentOxygen = Mathf.MoveTowards(currentOxygen, maxOxygen, 35f * Time.deltaTime);
                isOutOfOxygen = false;
            }
            else if (currentOxygen > 0)
            {
                // Base consumption: 1 unit per second scaled by heart rate
                currentOxygen -= oxygenBurnRate * 0.8f * Time.deltaTime;
                if (currentOxygen <= 0)
                {
                    currentOxygen = 0;
                    isOutOfOxygen = true;
                }
            }
        }

        public void ReplenishOxygen(float amount)
        {
            currentOxygen = Mathf.Clamp(currentOxygen + amount, 0f, maxOxygen);
            if (currentOxygen > 0) isOutOfOxygen = false;
        }

        private void TriggerHeartbeatPulse()
        {
            if (Time.time - lastBeatTime >= beatInterval)
            {
                lastBeatTime = Time.time;
                OnHeartbeat?.Invoke(currentBPM);
            }
        }

        /// <summary>
        /// Call this to inject real raw readings from ESP32 / Bluetooth PPG
        /// </summary>
        public void FeedSensorBPM(float measuredBPM)
        {
            // Auto-instantiate clinical PPG signal filter if not yet attached
            if (PPGSignalFilter.Instance == null)
            {
                GameObject filterGO = new GameObject("PPGSignalFilter");
                filterGO.AddComponent<PPGSignalFilter>();
            }

            // Manual user override mode (toggled via '2' key)
            if (manualHarmonicMode == 1) measuredBPM *= 2.0f;       // Force 2x
            else if (manualHarmonicMode == 2) measuredBPM *= 0.5f;  // Force 0.5x

            // Run sample through the 5-stage clinical DSP pipeline (Kalman, Median, De-Aliasing, Slew-Rate, SQI)
            float filteredBPM = PPGSignalFilter.Instance.FilterSample(measuredBPM, baselineMeanBPM);

            targetHardwareBPM = filteredBPM;
            lastValidBPM = filteredBPM;
            useHardwareSensor = true;

            // Snap immediately if uninitialized or if starting from default
            if (!hasHardwareBPM || Mathf.Abs(currentBPM - restingBPM) < 0.2f)
            {
                currentBPM = filteredBPM;
            }
            hasHardwareBPM = true;
        }
    }
}

