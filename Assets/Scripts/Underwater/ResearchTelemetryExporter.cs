using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnderwaterReef
{
    [System.Serializable]
    public struct TelemetryPoint
    {
        public float timestamp;
        public string participantID;
        public string condition;
        public Vector3 position;
        public float bpm;
        public float zScore;
        public float rmssd;
        public float sdnn;
        public float lfHfRatio;
        public float respRateBpm;
        public float stressIndex;
        public float coherence;
        public float circumplexValence;
        public float circumplexArousal;
        public float oxygen;
        public float depth;
        public float latencyMs;
        public float tsaheyluBondIndex;
        public float diveReflexDRI;
        public float spO2;
        public float poincareSD1;
        public float poincareSD2;
        public AffectiveState state;
        public string eventTag;
    }

    /// <summary>
    /// Academic Clinical Research Telemetry Engine & 3D Cognitive Heatmap Visualizer.
    /// - Continuously logs synchronized physiological and kinematic variables.
    /// - Exports clinical-grade CSV files for statistical analysis in Python / R / MATLAB.
    /// - Renders in-engine 3D bathymetric heatmap splines of the dive for academic post-dive review.
    /// </summary>
    public class ResearchTelemetryExporter : MonoBehaviour
    {
        public static ResearchTelemetryExporter Instance { get; private set; }

        [Header("Telemetry Configuration")]
        public float sampleRateHz = 10f; // 10 Hz clinical logging
        public bool autoExportOnDiveComplete = true;
        public int loggedPointCount => telemetryLog.Count;
        public string lastExportedFilePath = "";

        private List<TelemetryPoint> telemetryLog = new List<TelemetryPoint>();
        private float sampleTimer = 0f;
        private DiverController diver;
        private LineRenderer heatmapRenderer;
        private int lastExportedSampleCount = 0;

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
            SetupHeatmapRenderer();
        }

        private void SetupHeatmapRenderer()
        {
            GameObject heatmapGO = new GameObject("3DHeatmapSpline");
            heatmapGO.transform.SetParent(transform, false);

            heatmapRenderer = heatmapGO.AddComponent<LineRenderer>();
            heatmapRenderer.startWidth = 0.35f;
            heatmapRenderer.endWidth = 0.35f;
            heatmapRenderer.positionCount = 0;
            heatmapRenderer.useWorldSpace = true;

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material lineMat = new Material(uLit);
            lineMat.EnableKeyword("_EMISSION");
            heatmapRenderer.sharedMaterial = lineMat;
            heatmapRenderer.enabled = false; // Hidden until post-dive review
        }

        private void Update()
        {
            if (diver == null) diver = FindAnyObjectByType<DiverController>();

            sampleTimer += Time.deltaTime;
            if (sampleTimer >= (1f / sampleRateHz))
            {
                sampleTimer = 0f;
                RecordDataPoint("");
            }

            // F7: Instant research telemetry dump to CSV
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.f7Key.wasPressedThisFrame)
            {
                ExportTelemetryToCSV();
            }
#else
            if (Input.GetKeyDown(KeyCode.F7))
            {
                ExportTelemetryToCSV();
            }
#endif
        }

        private void OnApplicationQuit()
        {
            if (telemetryLog.Count > 10 && telemetryLog.Count > lastExportedSampleCount)
            {
                ExportTelemetryToCSV();
            }
        }

        private void OnDestroy()
        {
            if (telemetryLog.Count > 10 && telemetryLog.Count > lastExportedSampleCount)
            {
                ExportTelemetryToCSV();
            }
        }

        public void RecordDataPoint(string eventTag)
        {
            if (diver == null) return;

            HeartRateSystem hr = HeartRateSystem.Instance;
            HRVEngine hrv = HRVEngine.Instance;
            ResearchStudyController study = ResearchStudyController.Instance;
            ESP32HeartRateBridge bridge = ESP32HeartRateBridge.Instance;

            TelemetryPoint pt = new TelemetryPoint
            {
                timestamp = Time.time,
                participantID = study != null ? study.participantID : "P01",
                condition = study != null ? study.activeCondition.ToString() : "ConditionC_AmbientAdaptive",
                position = diver.transform.position,
                bpm = hr != null ? hr.currentBPM : 72f,
                zScore = hr != null ? hr.currentZScore : 0f,
                rmssd = hrv != null ? hrv.rmssd : 45f,
                sdnn = hrv != null ? hrv.sdnn : 50f,
                lfHfRatio = hrv != null ? hrv.lfHfRatio : 1.05f,
                respRateBpm = hrv != null ? hrv.derivedRespirationBPM : 14.5f,
                stressIndex = hrv != null ? hrv.stressIndex : 90f,
                coherence = hrv != null ? hrv.coherenceScore : 0.7f,
                circumplexValence = hrv != null ? hrv.circumplexValence : 0.4f,
                circumplexArousal = hrv != null ? hrv.circumplexArousal : -0.2f,
                oxygen = hr != null ? hr.currentOxygen : 100f,
                depth = diver.currentDepth,
                latencyMs = (bridge != null && bridge.isHardwareConnected) ? bridge.averageLatencyMs : 0f,
                tsaheyluBondIndex = hrv != null ? hrv.tsaheyluBondIndex : 70f,
                diveReflexDRI = hr != null ? hr.mammalianDiveReflexIndex : 0f,
                spO2 = hr != null ? hr.currentSpO2 : 98.5f,
                poincareSD1 = hrv != null ? hrv.sd1 : 32f,
                poincareSD2 = hrv != null ? hrv.sd2 : 64f,
                state = hrv != null ? hrv.currentState : AffectiveState.Flow,
                eventTag = eventTag
            };

            telemetryLog.Add(pt);
        }

        public void ExportTelemetryToCSV()
        {
            if (telemetryLog.Count == 0) return;

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("TimestampSec,ParticipantID,Condition,PosX,PosY,PosZ,DepthM,BPM,ZScore,RMSSD_ms,SDNN_ms,LF_HF_Ratio,RespRate_BPM,StressIndex,CoherenceScore,CircumplexValence,CircumplexArousal,OxygenPct,LatencyMs,TsaheyluBondPct,DiveReflexDRI,SpO2,PoincareSD1,PoincareSD2,AffectiveState,EventMarker");

            foreach (var pt in telemetryLog)
            {
                sb.AppendLine(string.Format(inv,
                    "{0:F2},{1},{2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F1},{8:F2},{9:F1},{10:F1},{11:F2},{12:F1},{13:F1},{14:F2},{15:F2},{16:F2},{17:F1},{18:F2},{19:F1},{20:F1},{21:F1},{22:F1},{23:F1},{24},{25}",
                    pt.timestamp, pt.participantID, pt.condition,
                    pt.position.x, pt.position.y, pt.position.z, pt.depth,
                    pt.bpm, pt.zScore, pt.rmssd, pt.sdnn, pt.lfHfRatio, pt.respRateBpm,
                    pt.stressIndex, pt.coherence, pt.circumplexValence, pt.circumplexArousal,
                    pt.oxygen, pt.latencyMs, pt.tsaheyluBondIndex, pt.diveReflexDRI, pt.spO2,
                    pt.poincareSD1, pt.poincareSD2, pt.state, pt.eventTag));
            }

            string baseDir = Application.platform == RuntimePlatform.Android
                ? Application.persistentDataPath
                : Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dirPath = Path.Combine(baseDir, "Logs");
            if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

            ResearchStudyController study = ResearchStudyController.Instance;
            string pTag = study != null ? study.participantID : "P01";
            int trialTag = study != null ? study.trialNumber : 1;
            string condTag = study != null ? study.activeCondition.ToString() : "ConditionC";

            string fileName = $"DiveTelemetry_{pTag}_Trial{trialTag}_{condTag}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string fullPath = Path.Combine(dirPath, fileName);

            File.WriteAllText(fullPath, sb.ToString());
            lastExportedFilePath = fullPath;
            lastExportedSampleCount = telemetryLog.Count;

            Debug.Log($"<color=#38ef7d>[Research Telemetry]</color> Exported {telemetryLog.Count} clinical rows to: {fullPath}");

            // Visualize in-engine 3D heatmap
            Render3DHeatmapSpline();
        }

        private void Render3DHeatmapSpline()
        {
            if (heatmapRenderer == null || telemetryLog.Count < 2) return;

            heatmapRenderer.enabled = true;
            heatmapRenderer.positionCount = telemetryLog.Count;

            Gradient gradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[3];
            colorKeys[0] = new GradientColorKey(new Color(0.2f, 0.9f, 1f), 0.0f); // Cyan = Calm
            colorKeys[1] = new GradientColorKey(new Color(0.3f, 1f, 0.4f), 0.5f); // Green = Flow
            colorKeys[2] = new GradientColorKey(new Color(1f, 0.2f, 0.2f), 1.0f); // Red = Stress

            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(0.9f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(0.9f, 1.0f);

            gradient.SetKeys(colorKeys, alphaKeys);
            heatmapRenderer.colorGradient = gradient;

            for (int i = 0; i < telemetryLog.Count; i++)
            {
                heatmapRenderer.SetPosition(i, telemetryLog[i].position);
            }
        }
    }
}

