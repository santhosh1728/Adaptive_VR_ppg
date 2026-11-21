using System;
using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Real-time physiological ECG (Electrocardiogram) waveform oscilloscope monitor.
    /// Simulates authentic clinical P-Q-R-S-T cardiac complexes synchronized with player BPM.
    /// Sweeps across the Bio-Dive Computer HUD, providing a high-tech medical-grade interface.
    /// </summary>
    public class ECGWaveformMonitor : MonoBehaviour
    {
        public static ECGWaveformMonitor Instance { get; private set; }

        public const int BUFFER_SIZE = 120;
        public float[] ecgBuffer = new float[BUFFER_SIZE];
        public int writeIndex = 0;

        private float beatPhase = -1f;
        private float sweepTimer = 0f;
        private Texture2D gridTex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            for (int i = 0; i < BUFFER_SIZE; i++) ecgBuffer[i] = 0f;
        }

        private void Start()
        {
            if (HeartRateSystem.Instance != null)
            {
                HeartRateSystem.Instance.OnHeartbeat += TriggerECGComplex;
            }
        }

        private void OnDestroy()
        {
            if (HeartRateSystem.Instance != null)
            {
                HeartRateSystem.Instance.OnHeartbeat -= TriggerECGComplex;
            }
        }

        private void TriggerECGComplex(float bpm)
        {
            beatPhase = 0f;
        }

        private void Update()
        {
            // Sample rate of oscilloscope: ~60 samples per second
            sweepTimer += Time.deltaTime;
            while (sweepTimer >= 0.016f)
            {
                sweepTimer -= 0.016f;
                AdvanceECGSample();
            }
        }

        private void AdvanceECGSample()
        {
            float voltage = 0f;

            if (beatPhase >= 0f)
            {
                beatPhase += 0.016f;

                // Clinical P-Q-R-S-T Complex duration: ~0.35 seconds
                if (beatPhase < 0.08f)
                {
                    // P-Wave (Atrial depolarization: gentle bump)
                    voltage = Mathf.Sin((beatPhase / 0.08f) * Mathf.PI) * 0.22f;
                }
                else if (beatPhase >= 0.09f && beatPhase < 0.12f)
                {
                    // Q-Wave (Slight downward deflection)
                    voltage = -0.15f;
                }
                else if (beatPhase >= 0.12f && beatPhase < 0.16f)
                {
                    // R-Spike (Ventricular depolarization: sharp upward spike)
                    float t = (beatPhase - 0.12f) / 0.04f;
                    voltage = Mathf.Sin(t * Mathf.PI) * 1.35f;
                }
                else if (beatPhase >= 0.16f && beatPhase < 0.19f)
                {
                    // S-Wave (Downward rebound)
                    voltage = -0.32f;
                }
                else if (beatPhase >= 0.23f && beatPhase < 0.35f)
                {
                    // T-Wave (Ventricular repolarization: rounded wave)
                    float t = (beatPhase - 0.23f) / 0.12f;
                    voltage = Mathf.Sin(t * Mathf.PI) * 0.38f;
                }
                else if (beatPhase >= 0.40f)
                {
                    beatPhase = -1f; // Return to baseline isoelectric line
                }
            }

            // Subtle physiological noise baseline
            voltage += (UnityEngine.Random.value - 0.5f) * 0.03f;

            ecgBuffer[writeIndex] = voltage;
            writeIndex = (writeIndex + 1) % BUFFER_SIZE;
        }

        /// <summary>
        /// Renders the oscilloscope trace inside the provided GUI rect.
        /// </summary>
        public void DrawOscilloscope(Rect rect, Color traceColor)
        {
            int n = BUFFER_SIZE;
            float stepX = rect.width / (n - 1);
            float midY = rect.y + rect.height * 0.55f;
            float scaleY = rect.height * 0.4f;

            for (int i = 0; i < n - 1; i++)
            {
                int idx1 = (writeIndex + i) % n;
                int idx2 = (writeIndex + i + 1) % n;

                float x1 = rect.x + i * stepX;
                float y1 = midY - (ecgBuffer[idx1] * scaleY);

                float x2 = rect.x + (i + 1) * stepX;
                float y2 = midY - (ecgBuffer[idx2] * scaleY);

                // Phosphor fade trail
                float alpha = Mathf.Lerp(0.2f, 1f, (float)i / n);
                Color segColor = new Color(traceColor.r, traceColor.g, traceColor.b, alpha);

                DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), segColor, 2f);
            }
        }

        private void DrawLine(Vector2 p1, Vector2 p2, Color col, float width)
        {
            Vector2 d = p2 - p1;
            float a = Mathf.Rad2Deg * Mathf.Atan2(d.y, d.x);
            GUIUtility.RotateAroundPivot(a, p1);
            GUI.color = col;
            GUI.DrawTexture(new Rect(p1.x, p1.y - width * 0.5f, d.magnitude, width), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUIUtility.RotateAroundPivot(-a, p1);
        }
    }
}

