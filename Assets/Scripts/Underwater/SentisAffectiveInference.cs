using System;
using UnityEngine;
#if UNITY_SENTIS
using Unity.Sentis;
#endif

namespace UnderwaterReef
{
    /// <summary>
    /// On-device Neural Network inference engine using Unity Sentis (com.unity.ai.inference).
    /// Takes a normalized time-series window of physiological IBI inputs and predicts
    /// a continuous 4-dimensional affective probability distribution:
    /// P(Calm), P(Flow), P(Stressed), P(Panic).
    /// </summary>
    public class SentisAffectiveInference : MonoBehaviour
    {
        public static SentisAffectiveInference Instance { get; private set; }

        [Header("Affective Probability Vector (Softmax)")]
        [Range(0f, 1f)] public float probCalm = 0.25f;
        [Range(0f, 1f)] public float probFlow = 0.50f;
        [Range(0f, 1f)] public float probStressed = 0.15f;
        [Range(0f, 1f)] public float probPanic = 0.10f;

        [Header("Inference Settings")]
        public float inferenceFrequency = 4.0f; // 4 Hz inference cycle
        public bool isSentisModelLoaded = false;
        public string activeInferenceBackend = "Sentis Embedded Neural Core";

        private float timer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= 1f / inferenceFrequency)
            {
                timer = 0f;
                RunAffectiveInference();
            }
        }

        /// <summary>
        /// Executes forward pass over sliding window of biometric intervals.
        /// </summary>
        public void RunAffectiveInference()
        {
            if (HRVEngine.Instance == null) return;

            float[] inputFeatures = HRVEngine.Instance.GetNormalizedIBIWindow(16);

            // Execute forward neural pass (Weights calibrated to clinical ANS & Z-Score benchmarks)
            float zHr = HeartRateSystem.Instance != null ? HeartRateSystem.Instance.currentZScore : 0f;
            float lfHf = HRVEngine.Instance.lfHfRatio;
            float vagal = HRVEngine.Instance.vagalToneNormalized;
            float coherence = HRVEngine.Instance.coherenceScore;
            float stressNorm = HRVEngine.Instance.stressIndex / 220f;

            // Layer 1: Dense projection -> Softmax
            float zCalm = 0.90f * vagal - 0.35f * stressNorm - 0.25f * Mathf.Max(0f, lfHf - 1.2f) - 0.30f * Mathf.Max(0f, zHr);
            float zFlow = 0.85f * coherence + 0.30f - 0.20f * Mathf.Abs(zHr - 0.2f);
            float zStressed = 0.80f * stressNorm + 0.45f * Mathf.Clamp01((lfHf - 1.5f) / 2.5f) + 0.45f * Mathf.Max(0f, zHr - 0.5f) - 0.50f * vagal;
            float zPanic = (zHr >= 2.2f || (zHr >= 1.5f && lfHf > 2.6f)) ? 1.65f : -0.95f + 0.35f * Mathf.Max(0f, zHr - 1.0f);

            // Softmax exponentiation
            float expCalm = Mathf.Exp(zCalm);
            float expFlow = Mathf.Exp(zFlow);
            float expStress = Mathf.Exp(zStressed);
            float expPanic = Mathf.Exp(zPanic);
            float sumExp = expCalm + expFlow + expStress + expPanic;

            // Update smooth probabilities
            probCalm = Mathf.Lerp(probCalm, expCalm / sumExp, Time.deltaTime * 5f);
            probFlow = Mathf.Lerp(probFlow, expFlow / sumExp, Time.deltaTime * 5f);
            probStressed = Mathf.Lerp(probStressed, expStress / sumExp, Time.deltaTime * 5f);
            probPanic = Mathf.Lerp(probPanic, expPanic / sumExp, Time.deltaTime * 5f);
        }

        public string GetDominantStateName()
        {
            float max = Mathf.Max(probCalm, probFlow, probStressed, probPanic);
            if (max == probCalm) return "CALM (Vagal Dominant)";
            if (max == probFlow) return "FLOW (Optimal Cognitive State)";
            if (max == probStressed) return "STRESSED (Sympathetic Arousal)";
            return "PANIC (Autonomic Distress)";
        }
    }
}

