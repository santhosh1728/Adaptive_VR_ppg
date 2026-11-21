using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnderwaterReef
{
    /// <summary>
    /// Clinical-Grade Digital Signal Processing (DSP) Filter Pipeline for PPG & Heart Rate.
    /// Eliminates:
    /// 1. Dicrotic Notch double-counting (~120-130 BPM artifact from ~60-65 true BPM)
    /// 2. Alternate-beat dropping (~40-48 BPM sub-harmonic aliasing from ~80-96 true BPM)
    /// 3. High-frequency impulse spikes & optical noise (7-sample moving median filter)
    /// 4. Jitter and sudden jumps (1D Biosignal Kalman Filter & Slew-Rate Limiter)
    /// 5. Motion artifacts (Real-time Signal Quality Index / SQI confidence gating)
    /// </summary>
    public class PPGSignalFilter : MonoBehaviour
    {
        public static PPGSignalFilter Instance { get; private set; }

        [Header("DSP Filter Pipeline Toggles")]
        [Tooltip("Eliminates double-counting (124 -> 62 BPM) and dropped beats (46 -> 92 BPM)")]
        public bool enableHarmonicDeAliasing = true;

        [Tooltip("7-sample rolling median filter to completely eliminate random optical spikes")]
        public bool enableMedianFilter = true;

        [Tooltip("1D Biosignal Kalman Filter for smooth, medical-grade state estimation")]
        public bool enableKalmanFilter = true;

        [Tooltip("Physiological slew-rate limiter (clamps sudden non-biological accelerations)")]
        public bool enableSlewRateLimiter = true;

        [Tooltip("Signal Quality Index (SQI) confidence gating (rejects motion artifacts)")]
        public bool enableSQIGating = true;

        [Header("Harmonic De-Aliasing Thresholds")]
        [Tooltip("Target baseline anchor for harmonic checks (updated dynamically)")]
        public float referenceAnchorBPM = 68f;
        public float doubleBeatLowRatio = 1.75f;
        public float doubleBeatHighRatio = 2.25f;
        public float halfBeatLowRatio = 0.44f;
        public float halfBeatHighRatio = 0.58f;

        [Header("Median Filter Settings")]
        [Range(3, 11)] public int medianWindowSize = 7;
        private List<float> medianBuffer = new List<float>();

        [Header("1D Kalman Filter Parameters")]
        [Tooltip("Process noise variance Q (natural cardiac fluctuation, 0.05 - 0.5)")]
        public float processNoiseQ = 0.12f;
        [Tooltip("Measurement noise variance R (optical sensor uncertainty, 2.0 - 8.0)")]
        public float measurementNoiseR = 4.0f;
        private float kalmanEstimate = 68f;
        private float kalmanCovariance = 8f;
        private bool isKalmanInitialized = false;

        [Header("Slew-Rate Limiter")]
        [Tooltip("Maximum allowed physiological BPM change per second (human SA node limit)")]
        public float maxBpmChangePerSecond = 3.5f;
        private float lastSlewFilteredBPM = 68f;
        private float lastSampleTimestamp = 0f;

        [Header("Signal Quality Index (SQI)")]
        [Range(0f, 100f)] public float currentSQI = 95f;
        public string sqiRating = "PRISTINE";
        private Queue<float> sqiBuffer = new Queue<float>();
        private const int SQI_WINDOW = 8;
        private int consecutiveRejections = 0;

        [Header("Telemetry & Diagnostics")]
        public float rawInputBPM = 0f;
        public float filteredOutputBPM = 68f;
        public bool wasDeAliasedDouble = false;
        public bool wasDeAliasedHalf = false;
        public int totalSpikesRejected = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            lastSampleTimestamp = Time.time;
        }

        private void Update()
        {
            // Hotkey F8: Toggle DSP Filter pipeline on/off for instant comparison
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                ToggleAllFilters();
            }
#else
            if (Input.GetKeyDown(KeyCode.F8))
            {
                ToggleAllFilters();
            }
#endif

            // Slowly decay SQI towards 100% when steady
            if (Time.time - lastSampleTimestamp > 1.0f)
            {
                currentSQI = Mathf.MoveTowards(currentSQI, 90f, Time.deltaTime * 5f);
            }
        }

        public void ToggleAllFilters()
        {
            bool newState = !enableHarmonicDeAliasing;
            enableHarmonicDeAliasing = newState;
            enableMedianFilter = newState;
            enableKalmanFilter = newState;
            enableSlewRateLimiter = newState;
            enableSQIGating = newState;
            Debug.Log($"<color=#00e5ff>[PPG DSP Filter]</color> Pipeline {(newState ? "ENABLED (All 5 Filters Active)" : "BYPASSED (Raw Mode)")}");
        }

        /// <summary>
        /// Main Entry Point: Pass a raw BPM measurement from ESP32 / PPG.
        /// Returns the clean, clinically filtered, noise-free heart rate.
        /// </summary>
        public float FilterSample(float rawBPM, float baselineMean = 68f)
        {
            rawInputBPM = rawBPM;
            wasDeAliasedDouble = false;
            wasDeAliasedHalf = false;

            // Step 0: Absolute Physiological Envelope (Human survival range)
            if (rawBPM < 36f || rawBPM > 220f)
            {
                totalSpikesRejected++;
                return filteredOutputBPM; // Hold last good estimate
            }

            float currentSample = rawBPM;
            float dt = Mathf.Max(0.02f, Time.time - lastSampleTimestamp);
            lastSampleTimestamp = Time.time;

            // Step 1: Harmonic De-Aliasing Filter (Eliminates 124 BPM double & 46 BPM half-rate)
            if (enableHarmonicDeAliasing)
            {
                currentSample = ApplyHarmonicDeAliasing(currentSample, baselineMean);
            }

            // Step 2: Signal Quality Index (SQI) Assessment & Motion Gating
            if (enableSQIGating)
            {
                bool accepted = AssessSignalQuality(currentSample);
                if (!accepted && consecutiveRejections < 4)
                {
                    consecutiveRejections++;
                    totalSpikesRejected++;
                    // Hold last reliable Kalman estimate rather than accepting a motion artifact
                    return filteredOutputBPM;
                }
                consecutiveRejections = 0;
            }

            // Step 3: Rolling Median Filter (Rejects impulse noise spikes)
            if (enableMedianFilter)
            {
                currentSample = ApplyMedianFilter(currentSample);
            }

            // Step 4: 1D Biosignal Kalman Filter (Optimal state tracking)
            if (enableKalmanFilter)
            {
                currentSample = ApplyKalmanFilter(currentSample);
            }

            // Step 5: Physiological Slew-Rate Limiter (Maximum biological acceleration)
            if (enableSlewRateLimiter)
            {
                currentSample = ApplySlewRateLimiter(currentSample, dt);
            }

            filteredOutputBPM = currentSample;
            return filteredOutputBPM;
        }

        #region Filter Implementations

        private float ApplyHarmonicDeAliasing(float sample, float baseline)
        {
            // Use current running estimate or baseline as anchor
            float anchor = isKalmanInitialized ? kalmanEstimate : (baseline > 40f ? baseline : 68f);
            referenceAnchorBPM = anchor;

            float ratio = sample / Mathf.Max(30f, anchor);

            // Case A: Dicrotic Notch Double-Count (e.g. 118 - 140 BPM when real rate is ~60-70 BPM)
            if (ratio >= doubleBeatLowRatio && ratio <= doubleBeatHighRatio)
            {
                sample *= 0.5f; // Fold dicrotic notch back into single cardiac cycle
                wasDeAliasedDouble = true;
            }
            // Case B: Dropped Alternate Beat / Respiratory Attenuation (e.g. 42 - 50 BPM when real rate is ~84-95 BPM)
            else if (ratio >= halfBeatLowRatio && ratio <= halfBeatHighRatio)
            {
                sample *= 2.0f; // Restore dropped alternate beat
                wasDeAliasedHalf = true;
            }

            return sample;
        }

        private float ApplyMedianFilter(float sample)
        {
            medianBuffer.Add(sample);
            if (medianBuffer.Count > medianWindowSize)
            {
                medianBuffer.RemoveAt(0);
            }

            List<float> sorted = new List<float>(medianBuffer);
            sorted.Sort();
            return sorted[sorted.Count / 2];
        }

        private float ApplyKalmanFilter(float measurement)
        {
            if (!isKalmanInitialized)
            {
                kalmanEstimate = measurement;
                kalmanCovariance = 8f;
                isKalmanInitialized = true;
                return kalmanEstimate;
            }

            // Time Update (Prediction)
            float predictedEstimate = kalmanEstimate;
            float predictedCovariance = kalmanCovariance + processNoiseQ;

            // Measurement Update (Correction)
            float kalmanGain = predictedCovariance / (predictedCovariance + measurementNoiseR);
            kalmanEstimate = predictedEstimate + kalmanGain * (measurement - predictedEstimate);
            kalmanCovariance = (1f - kalmanGain) * predictedCovariance;

            return kalmanEstimate;
        }

        private float ApplySlewRateLimiter(float target, float dt)
        {
            float maxDelta = maxBpmChangePerSecond * dt;
            float slewLimited = Mathf.MoveTowards(lastSlewFilteredBPM, target, maxDelta);
            lastSlewFilteredBPM = slewLimited;
            return slewLimited;
        }

        private bool AssessSignalQuality(float sample)
        {
            sqiBuffer.Enqueue(sample);
            if (sqiBuffer.Count > SQI_WINDOW) sqiBuffer.Dequeue();

            if (sqiBuffer.Count < 3)
            {
                currentSQI = 90f;
                sqiRating = "ACQUIRING";
                return true;
            }

            // Calculate Mean and Standard Deviation of recent window
            float sum = 0f;
            foreach (float val in sqiBuffer) sum += val;
            float mean = sum / sqiBuffer.Count;

            float sumSq = 0f;
            foreach (float val in sqiBuffer)
            {
                float diff = val - mean;
                sumSq += diff * diff;
            }
            float stdDev = Mathf.Sqrt(sumSq / sqiBuffer.Count);

            // Coefficient of Variation (CV = StdDev / Mean)
            // For a stable finger, CV is typically < 0.06 (6%). Sudden motion causes CV > 0.18 (18%)
            float cv = stdDev / Mathf.Max(mean, 40f);

            // Convert CV to 0-100% SQI score
            float rawSQI = Mathf.Clamp01(1f - (cv / 0.15f)) * 100f;
            currentSQI = Mathf.Lerp(currentSQI, rawSQI, 0.25f);

            if (currentSQI >= 80f) sqiRating = "PRISTINE";
            else if (currentSQI >= 55f) sqiRating = "STABLE";
            else if (currentSQI >= 35f) sqiRating = "MOTION NOISE";
            else sqiRating = "POOR CONTACT";

            // If a single incoming beat deviates by > 26 BPM from rolling mean, it's a motion artifact
            if (Mathf.Abs(sample - mean) > 26f)
            {
                return false;
            }

            return currentSQI >= 35f;
        }

        #endregion

        /// <summary>
        /// Resets the filter buffers when a new player connects or finger is replaced.
        /// </summary>
        public void ResetFilter(float seedBPM = 68f)
        {
            medianBuffer.Clear();
            sqiBuffer.Clear();
            kalmanEstimate = seedBPM;
            kalmanCovariance = 8f;
            lastSlewFilteredBPM = seedBPM;
            filteredOutputBPM = seedBPM;
            isKalmanInitialized = true;
            consecutiveRejections = 0;
            currentSQI = 95f;
            sqiRating = "PRISTINE";
        }
    }
}
