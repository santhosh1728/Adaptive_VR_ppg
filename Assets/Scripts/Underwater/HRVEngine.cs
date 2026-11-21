using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterReef
{
    public enum AffectiveState
    {
        Calm,       // High parasympathetic tone (Vagal recovery)
        Flow,       // Balanced sympathetic-parasympathetic harmony (Optimal task focus)
        Stressed,   // Elevated sympathetic arousal
        Panic       // Severe autonomic distress (Tachycardia + low HRV)
    }

    public enum CircumplexQuadrant
    {
        Serenity_Calm,       // Low Arousal, High Valence
        Flow_Exhilaration,   // High Arousal, High Valence
        Panic_Distress,      // High Arousal, Low Valence
        Fatigue_Hypoarousal  // Low Arousal, Low Valence
    }

    public enum TsaheyluTier
    {
        Disconnected,  // Φ < 40%: No autonomic synchrony
        Harmonizing,   // 40% <= Φ < 65%: Baseline autonomic calibration
        Bonded,        // 65% <= Φ < 85%: Symbiotic synchrony with reef ecology
        Transcendent   // Φ >= 85%: Hyper-flow, full planetary harmony
    }

    /// <summary>
    /// Mathematical Heart Rate Variability (HRV) Signal Processing Engine.
    /// Computes clinical time-domain & frequency-domain metrics over a rolling beat window:
    /// - Time-Domain: RMSSD (vagal tone), SDNN (total ANS power), Baevsky Stress Index
    /// - Frequency-Domain (4 Hz uniform DFT): LF (0.04-0.15 Hz), HF (0.15-0.40 Hz), LF/HF Ratio
    /// - Respiratory Sinus Arrhythmia (RSA): Derived Respiration Rate (Breaths/Min)
    /// - Affective Mapping: Russell's 2D Circumplex Valence-Arousal Coordinates
    /// </summary>
    public class HRVEngine : MonoBehaviour
    {
        public static HRVEngine Instance { get; private set; }

        [Header("Clinical Time-Domain HRV Metrics")]
        [Tooltip("Root Mean Square of Successive Differences in ms (Higher = Calmer/Healthier vagal tone)")]
        public float rmssd = 45f;
        [Tooltip("Standard Deviation of NN intervals in ms")]
        public float sdnn = 50f;
        [Tooltip("Baevsky Stress Index (Normal: 50 - 150, Stress: > 250)")]
        public float stressIndex = 90f;
        [Tooltip("Cardiac Coherence Score (0 to 1)")]
        public float coherenceScore = 0.7f;

        [Header("Frequency-Domain HRV & RSA Respiration")]
        [Tooltip("Low Frequency power (0.04 - 0.15 Hz): Sympathetic + Baroreflex modulation")]
        public float lfPower = 420f;
        [Tooltip("High Frequency power (0.15 - 0.40 Hz): Pure Vagal / Parasympathetic power")]
        public float hfPower = 400f;
        [Tooltip("Sympathovagal Balance (LF/HF Ratio): > 2.0 indicates sympathetic dominance/stress")]
        public float lfHfRatio = 1.05f;
        [Tooltip("Derived Respiratory Rate via Respiratory Sinus Arrhythmia (Breaths Per Minute)")]
        public float derivedRespirationBPM = 14.5f;

        [Header("Russell's 2D Circumplex Affective Coordinates")]
        [Range(-1f, 1f)] public float circumplexValence = 0.45f;
        [Range(-1f, 1f)] public float circumplexArousal = -0.20f;
        public CircumplexQuadrant circumplexQuadrant = CircumplexQuadrant.Serenity_Calm;

        [Header("Predicted Affective State")]
        public AffectiveState currentState = AffectiveState.Flow;
        public float vagalToneNormalized = 0.6f; // 0 = panic, 1 = deep zen calm

        [Header("Nonlinear Dynamics (Poincaré Plot)")]
        [Tooltip("Minor axis SD1 (ms): Instantaneous short-term beat-to-beat variability (vagal tone)")]
        public float sd1 = 31.8f;
        [Tooltip("Major axis SD2 (ms): Continuous long-term autonomic variability")]
        public float sd2 = 63.2f;
        [Tooltip("Axis ratio SD1/SD2: Autonomic flexibility / sympathovagal balance")]
        public float sd1Sd2Ratio = 0.50f;
        public List<Vector2> poincarePoints = new List<Vector2>();
        public const int MAX_POINCARE_POINTS = 32;

        [Header("Tsaheylu Bio-Bonding Index (Φ)")]
        [Range(0f, 100f)] public float tsaheyluBondIndex = 72f;
        public TsaheyluTier currentBondTier = TsaheyluTier.Harmonizing;
        public Color tsaheyluBondColor = new Color(0.1f, 1f, 0.85f);
        public event Action<TsaheyluTier> OnTsaheyluBondTierChanged;

        private Queue<float> ibiHistory = new Queue<float>();
        private const int WINDOW_SIZE = 40; // Rolling 40 beats (~30-40 seconds for PSD resolution)
        private float lastBeatTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Seed with healthy resting baseline with subtle 0.24 Hz (~14.4 BrPM) RSA modulation
            float simTime = 0f;
            for (int i = 0; i < WINDOW_SIZE; i++)
            {
                float rsaWave = Mathf.Sin(2f * Mathf.PI * 0.24f * simTime) * 24f;
                float seedIbi = 760f + rsaWave + UnityEngine.Random.Range(-10f, 10f);
                ibiHistory.Enqueue(seedIbi);
                simTime += seedIbi / 1000f;
            }
        }

        private void Start()
        {
            if (HeartRateSystem.Instance != null)
            {
                HeartRateSystem.Instance.OnHeartbeat += HandleHeartbeatEvent;
            }
        }

        private void OnDestroy()
        {
            if (HeartRateSystem.Instance != null)
            {
                HeartRateSystem.Instance.OnHeartbeat -= HandleHeartbeatEvent;
            }
        }

        private void HandleHeartbeatEvent(float bpm)
        {
            float now = Time.time;
            if (lastBeatTime > 0)
            {
                float measuredIBI = (now - lastBeatTime) * 1000f; // in milliseconds
                measuredIBI = Mathf.Clamp(measuredIBI, 300f, 1500f);
                RegisterIBI(measuredIBI);
            }
            lastBeatTime = now;
        }

        /// <summary>
        /// Registers a new Inter-Beat Interval (IBI) in milliseconds and recalculates clinical metrics.
        /// </summary>
        public void RegisterIBI(float ibiMs)
        {
            // Clinical Ectopic Beat & Motion Artifact Rejection (Malik et al., 1996)
            float filteredIBI = FilterArtifacts(ibiMs);

            if (ibiHistory.Count > 0)
            {
                float[] arr = ibiHistory.ToArray();
                float prev = arr[arr.Length - 1];
                poincarePoints.Add(new Vector2(prev, filteredIBI));
                if (poincarePoints.Count > MAX_POINCARE_POINTS) poincarePoints.RemoveAt(0);
            }

            ibiHistory.Enqueue(filteredIBI);
            if (ibiHistory.Count > WINDOW_SIZE)
            {
                ibiHistory.Dequeue();
            }

            ComputeClinicalMetrics();
        }

        private float FilterArtifacts(float rawIBI)
        {
            if (ibiHistory.Count < 5) return Mathf.Clamp(rawIBI, 300f, 1500f);

            float[] recent = new float[5];
            float[] all = ibiHistory.ToArray();
            for (int i = 0; i < 5; i++) recent[i] = all[all.Length - 5 + i];
            Array.Sort(recent);
            float median = recent[2];

            // Rejection threshold: >20% deviation from 5-beat running median
            if (Mathf.Abs(rawIBI - median) / median > 0.20f)
            {
                return median; // Interpolate with median
            }
            return Mathf.Clamp(rawIBI, 300f, 1500f);
        }

        private void ComputeClinicalMetrics()
        {
            float[] ibis = ibiHistory.ToArray();
            int n = ibis.Length;
            if (n < 4) return;

            // 1. Mean & SDNN
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += ibis[i];
            float meanIBI = sum / n;

            float varianceSum = 0f;
            for (int i = 0; i < n; i++)
            {
                float diff = ibis[i] - meanIBI;
                varianceSum += diff * diff;
            }
            sdnn = Mathf.Sqrt(varianceSum / (n - 1));

            // 2. RMSSD (Root Mean Square of Successive Differences)
            float successiveDiffsSqSum = 0f;
            for (int i = 0; i < n - 1; i++)
            {
                float d = ibis[i + 1] - ibis[i];
                successiveDiffsSqSum += d * d;
            }
            rmssd = Mathf.Sqrt(successiveDiffsSqSum / (n - 1));

            // 3. Baevsky Stress Index: SI = AMo / (2 * Mo * MxDMn)
            float modalInterval = meanIBI / 1000f; // in seconds
            float minIBI = float.MaxValue;
            float maxIBI = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                if (ibis[i] < minIBI) minIBI = ibis[i];
                if (ibis[i] > maxIBI) maxIBI = ibis[i];
            }
            float rangeSec = Mathf.Max(0.04f, (maxIBI - minIBI) / 1000f);
            float amo = (sdnn < 15f) ? 0.85f : Mathf.Clamp01(30f / (sdnn + 0.01f)); // Amplitude of mode approximation
            stressIndex = (amo * 100f) / (2f * modalInterval * rangeSec);
            stressIndex = Mathf.Clamp(stressIndex, 20f, 600f);

            // 4. Frequency-Domain HRV (4 Hz Resampled DFT: LF vs HF) & RSA Breathing Rate
            ComputeFrequencyDomainAndRSA(ibis, meanIBI);

            // 5. Cardiac Coherence & Vagal Tone Normalization
            vagalToneNormalized = Mathf.Clamp01((rmssd - 12f) / 60f);
            float spectralHarmony = 1f / (1f + Mathf.Max(0f, lfHfRatio - 0.8f));
            coherenceScore = Mathf.Clamp01(((sdnn / 75f) * 0.6f + spectralHarmony * 0.4f) * (1f - (stressIndex / 550f)));

            // 6. Russell's 2D Circumplex Valence-Arousal Classification
            UpdateRussellCircumplex();

            // 7. Affective State Classification (incorporating Z-Score & LF/HF Sympathovagal Balance)
            float zScore = HeartRateSystem.Instance != null ? HeartRateSystem.Instance.currentZScore : 0f;
            if (stressIndex > 320f || rmssd < 18f || (zScore > 2.2f && lfHfRatio > 2.8f))
            {
                currentState = AffectiveState.Panic;
            }
            else if (stressIndex > 180f || rmssd < 28f || lfHfRatio > 2.2f || zScore > 1.25f)
            {
                currentState = AffectiveState.Stressed;
            }
            else if ((rmssd > 50f || vagalToneNormalized > 0.6f) && coherenceScore > 0.60f && lfHfRatio < 1.3f)
            {
                currentState = AffectiveState.Calm;
            }
            else
            {
                currentState = AffectiveState.Flow;
            }

            // 8. Nonlinear Poincaré Plot Geometry (SD1, SD2, SD1/SD2)
            sd1 = rmssd / 1.41421356f; // sqrt(0.5 * RMSSD^2)
            float sd2Radicand = 2f * sdnn * sdnn - 0.5f * rmssd * rmssd;
            sd2 = Mathf.Sqrt(Mathf.Max(0.01f, sd2Radicand));
            sd1Sd2Ratio = sd1 / Mathf.Max(0.01f, sd2);

            // 9. Tsaheylu Bio-Bonding Index (Φ)
            UpdateTsaheyluBioBond();
        }

        private void UpdateTsaheyluBioBond()
        {
            float zScore = HeartRateSystem.Instance != null ? HeartRateSystem.Instance.currentZScore : 0f;
            float zPenalty = Mathf.Clamp01(Mathf.Abs(zScore) / 2.0f);
            float zHarmony = 1f - zPenalty; // 1 when at baseline, 0 when heavily deviating

            // Weighted combination: Vagal tone (35%) + Baseline alignment (35%) + Coherence score (30%)
            float rawPhi = (0.35f * vagalToneNormalized + 0.35f * zHarmony + 0.30f * coherenceScore) * 100f;
            tsaheyluBondIndex = Mathf.Lerp(tsaheyluBondIndex, Mathf.Clamp(rawPhi, 0f, 100f), 0.25f);

            TsaheyluTier oldTier = currentBondTier;
            if (tsaheyluBondIndex >= 85f)
            {
                currentBondTier = TsaheyluTier.Transcendent;
                tsaheyluBondColor = new Color(1f, 0.85f, 0.2f); // Transcendent Gold
            }
            else if (tsaheyluBondIndex >= 65f)
            {
                currentBondTier = TsaheyluTier.Bonded;
                tsaheyluBondColor = new Color(0.1f, 1f, 0.85f); // Resonant Electric Cyan
            }
            else if (tsaheyluBondIndex >= 40f)
            {
                currentBondTier = TsaheyluTier.Harmonizing;
                tsaheyluBondColor = new Color(0.35f, 0.75f, 1f); // Soft Cyan
            }
            else
            {
                currentBondTier = TsaheyluTier.Disconnected;
                tsaheyluBondColor = new Color(0.55f, 0.6f, 0.68f); // Muted Slate
            }

            if (oldTier != currentBondTier)
            {
                OnTsaheyluBondTierChanged?.Invoke(currentBondTier);
            }
        }

        private void ComputeFrequencyDomainAndRSA(float[] ibis, float meanIBI)
        {
            int n = ibis.Length;
            if (n < 8) return;

            // Build cumulative beat arrival timestamps (seconds)
            float[] beatTimes = new float[n];
            beatTimes[0] = 0f;
            for (int i = 1; i < n; i++)
            {
                beatTimes[i] = beatTimes[i - 1] + (ibis[i - 1] / 1000f);
            }

            float totalDuration = beatTimes[n - 1];
            if (totalDuration < 4f) return;

            // Task Force standard: uniform 4 Hz cubic/linear tachogram resampling
            const float fs = 4.0f;
            float dt = 1.0f / fs;
            int m = Mathf.Clamp(Mathf.FloorToInt(totalDuration * fs), 16, 256);
            float[] uniform = new float[m];

            int cursor = 0;
            for (int k = 0; k < m; k++)
            {
                float t = k * dt;
                while (cursor < n - 2 && beatTimes[cursor + 1] < t)
                {
                    cursor++;
                }
                float span = Mathf.Max(0.001f, beatTimes[cursor + 1] - beatTimes[cursor]);
                float frac = Mathf.Clamp01((t - beatTimes[cursor]) / span);
                float val = Mathf.Lerp(ibis[cursor], ibis[cursor + 1], frac) - meanIBI; // Detrended
                // Hann window to suppress spectral leakage
                float hann = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * k / (m - 1)));
                uniform[k] = val * hann;
            }

            // Evaluate PSD across LF (0.04 - 0.15 Hz) & HF (0.15 - 0.40 Hz)
            float lfSum = 0f;
            float hfSum = 0f;
            float peakHfPower = -1f;
            float peakHfFreq = 0.24f;
            float df = 0.01f;

            for (float f = 0.04f; f <= 0.40f + 0.001f; f += df)
            {
                float cosAcc = 0f;
                float sinAcc = 0f;
                float twoPiF = 2f * Mathf.PI * f;

                for (int k = 0; k < m; k++)
                {
                    float angle = twoPiF * (k * dt);
                    cosAcc += uniform[k] * Mathf.Cos(angle);
                    sinAcc -= uniform[k] * Mathf.Sin(angle);
                }

                float psd = (cosAcc * cosAcc + sinAcc * sinAcc) / (m * fs);

                if (f < 0.15f)
                {
                    lfSum += psd;
                }
                else
                {
                    hfSum += psd;
                }

                // RSA breathing rate peak inside normal respiratory band [0.10 Hz = 6 BrPM, 0.40 Hz = 24 BrPM]
                if (f >= 0.10f && f <= 0.40f && psd > peakHfPower)
                {
                    peakHfPower = psd;
                    peakHfFreq = f;
                }
            }

            lfPower = Mathf.Lerp(lfPower, lfSum * df * 100f, 0.4f);
            hfPower = Mathf.Lerp(hfPower, hfSum * df * 100f, 0.4f);
            float instantRatio = lfSum / Mathf.Max(hfSum, 0.001f);
            lfHfRatio = Mathf.Lerp(lfHfRatio, Mathf.Clamp(instantRatio, 0.15f, 8.0f), 0.35f);

            // Convert peak RSA frequency to Breaths Per Minute (BrPM)
            float instantBrpm = Mathf.Clamp(peakHfFreq * 60f, 5.5f, 24.0f);
            derivedRespirationBPM = Mathf.Lerp(derivedRespirationBPM, instantBrpm, 0.3f);
        }

        private void UpdateRussellCircumplex()
        {
            float zScore = HeartRateSystem.Instance != null ? HeartRateSystem.Instance.currentZScore : 0f;

            // Arousal axis (-1 = Deep Calm, +1 = High Sympathetic Activation)
            float rawArousal = 0.55f * (float)Math.Tanh(zScore / 1.5f) + 0.45f * (float)Math.Tanh((lfHfRatio - 1.4f) / 1.4f);
            circumplexArousal = Mathf.Lerp(circumplexArousal, Mathf.Clamp(rawArousal, -1f, 1f), 0.35f);

            // Valence axis (-1 = Unpleasant Distress, +1 = Pleasant Serenity/Flow)
            float stressPenalty = Mathf.Clamp01(stressIndex / 320f);
            float rawValence = (1.15f * vagalToneNormalized) + (0.65f * coherenceScore) - (0.85f * stressPenalty) - 0.22f;
            circumplexValence = Mathf.Lerp(circumplexValence, Mathf.Clamp(rawValence, -1f, 1f), 0.35f);

            if (circumplexValence >= 0f)
            {
                circumplexQuadrant = circumplexArousal >= 0f ? CircumplexQuadrant.Flow_Exhilaration : CircumplexQuadrant.Serenity_Calm;
            }
            else
            {
                circumplexQuadrant = circumplexArousal >= 0f ? CircumplexQuadrant.Panic_Distress : CircumplexQuadrant.Fatigue_Hypoarousal;
            }
        }

        /// <summary>
        /// Returns sliding window of normalized IBIs for neural network inference.
        /// </summary>
        public float[] GetNormalizedIBIWindow(int size)
        {
            float[] result = new float[size];
            float[] current = ibiHistory.ToArray();
            for (int i = 0; i < size; i++)
            {
                if (i < current.Length)
                {
                    result[i] = (current[i] - 700f) / 300f; // Normalized z-score approximation
                }
                else
                {
                    result[i] = 0f;
                }
            }
            return result;
        }
    }
}

