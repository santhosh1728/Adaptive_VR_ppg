using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnderwaterReef
{
    /// <summary>
    /// Master coordinator for Underwater Reef Rescue.
// Updated: Biometric PPG integration & 6DOF fluid diving active.
    /// Handles:
    /// - Automatic runtime scene initialization (auto-spawns diver, world, HUD, audio)
    /// - Objectives tracking (Pearls, Beacons, Surface Gate)
    /// - Win condition & dive restart
    /// - Editor menu item to build the scene in Edit Mode
    /// </summary>
    public class ReefGameManager : MonoBehaviour
    {
        public static ReefGameManager Instance { get; private set; }

        [Header("Dive Objectives")]
        public int totalPearlsRequired = 5;
        public int totalBeaconsRequired = 2;
        public int collectedPearls = 0;
        public int activatedBeacons = 0;
        public bool isDiveComplete = false;

        [Header("References")]
        public DiverController diver;
        public ReefWorldGenerator worldGenerator;
        public HeartRateSystem heartRateSystem;
        public UnderwaterHUD hud;
        public UnderwaterAudio audioSystem;
        public ESP32HeartRateBridge esp32Bridge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeGame()
        {
            if (FindAnyObjectByType<ReefGameManager>() == null)
            {
                Debug.Log("<color=#00e5ff>[Underwater Reef Rescue]</color> Auto-bootstrapping game environment and diver...");
                GameObject managerGO = new GameObject("--- REEF GAME MANAGER ---");
                managerGO.AddComponent<ReefGameManager>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeCoreSystems();
        }

        private void Start()
        {
            // Build the procedural world and diver
            if (worldGenerator != null)
            {
                worldGenerator.GenerateWorld();
            }

            SpawnOrFindDiver();
        }

        private void InitializeCoreSystems()
        {
            // 1. Heart Rate Biofeedback System
            heartRateSystem = GetComponent<HeartRateSystem>();
            if (heartRateSystem == null) heartRateSystem = gameObject.AddComponent<HeartRateSystem>();

            // 2. Procedural Audio Synthesizer
            audioSystem = GetComponent<UnderwaterAudio>();
            if (audioSystem == null) audioSystem = gameObject.AddComponent<UnderwaterAudio>();

            // 3. Reef World Generator
            worldGenerator = GetComponent<ReefWorldGenerator>();
            if (worldGenerator == null) worldGenerator = gameObject.AddComponent<ReefWorldGenerator>();

            // 4. Underwater HUD
            hud = GetComponent<UnderwaterHUD>();
            if (hud == null) hud = gameObject.AddComponent<UnderwaterHUD>();

            // 5. ESP32 / PPG Hardware Bridge
            esp32Bridge = GetComponent<ESP32HeartRateBridge>();
            if (esp32Bridge == null) esp32Bridge = gameObject.AddComponent<ESP32HeartRateBridge>();

            // 6. Underwater Visual FX & Stress Vignette
            if (GetComponent<UnderwaterVFXController>() == null) gameObject.AddComponent<UnderwaterVFXController>();

            // 7. Academic Mathematical HRV Engine (RMSSD + Vagal Tone)
            if (GetComponent<HRVEngine>() == null) gameObject.AddComponent<HRVEngine>();

            // 8. Unity Sentis Affective Inference Engine
            if (GetComponent<SentisAffectiveInference>() == null) gameObject.AddComponent<SentisAffectiveInference>();

            // 9. The "Abyssal Director" (Biometric DDA)
            if (GetComponent<AbyssalDirector>() == null) gameObject.AddComponent<AbyssalDirector>();

            // 10. Research Telemetry Exporter & 3D Cognitive Heatmap
            if (GetComponent<ResearchTelemetryExporter>() == null) gameObject.AddComponent<ResearchTelemetryExporter>();

            // 11. In-Game Pause Menu & Bio-Sensor Config
            if (GetComponent<UnderwaterPauseMenu>() == null) gameObject.AddComponent<UnderwaterPauseMenu>();

            // 12. Real-Time Physiological ECG Oscilloscope Monitor
            if (GetComponent<ECGWaveformMonitor>() == null) gameObject.AddComponent<ECGWaveformMonitor>();

            // 13. Metkayina Sonar Bio-Acoustic Pulse System
            if (GetComponent<MetkayinaSonarSystem>() == null) gameObject.AddComponent<MetkayinaSonarSystem>();

            // 14. Pandoran Day / Poly-Eclipse Atmospheric Cycle
            if (GetComponent<PandoranDayNightCycle>() == null) gameObject.AddComponent<PandoranDayNightCycle>();

            // 15. Automated A/B/C Research Study Controller
            if (GetComponent<ResearchStudyController>() == null) gameObject.AddComponent<ResearchStudyController>();

            // 16. In-Situ In-VR Research Assessment Engine (SAM + NASA-TLX)
            if (GetComponent<InSituResearchQuestionnaire>() == null) gameObject.AddComponent<InSituResearchQuestionnaire>();

            // 17. Meta Quest 3 OpenXR Rig, Wrist Dive Computer & Touch Plus Controller Driver
            if (GetComponent<QuestVRRigController>() == null) gameObject.AddComponent<QuestVRRigController>();

            // 18. Procus VR Smartphone Stereoscopic Rig & Gyroscope Controller (F10 Preview)
            if (GetComponent<ProcusVRManager>() == null) gameObject.AddComponent<ProcusVRManager>();
        }

        private void SpawnOrFindDiver()
        {
            diver = FindAnyObjectByType<DiverController>();
            if (diver == null)
            {
                GameObject diverGO = new GameObject("PlayerDiver");
                diverGO.transform.position = new Vector3(0f, -4f, -4f); // Spawn near surface

                diver = diverGO.AddComponent<DiverController>();
            }

            if (diver.GetComponent<DiverFlashlight>() == null)
            {
                diver.gameObject.AddComponent<DiverFlashlight>();
            }
        }

        public void OnPearlCollected(GlowingPearl pearl)
        {
            collectedPearls++;
            Debug.Log($"<color=#38ef7d>[Reef Rescue]</color> Pearl collected! ({collectedPearls}/{totalPearlsRequired})");
            ResearchTelemetryExporter.Instance?.RecordDataPoint("PearlCollected");
            CheckObjectives();
        }

        public void AddPearl()
        {
            OnPearlCollected(null);
        }

        public void OnBeaconActivated(ReefInteractable beacon = null)
        {
            activatedBeacons++;
            Debug.Log($"<color=#00f2fe>[Tsaheylu Harmony]</color> Spirit Tree bonded! ({activatedBeacons}/{totalBeaconsRequired})");
            ResearchTelemetryExporter.Instance?.RecordDataPoint($"Beacon_{activatedBeacons}_Activated");

            // Restore vibrant color to bleached coral reefs
            if (worldGenerator != null)
            {
                worldGenerator.RestoreCoralReefColor();
            }

            CheckObjectives();

            // Prompt In-Situ Psychological Assessment Checkpoint (SAM + NASA-TLX)
            if (InSituResearchQuestionnaire.Instance != null)
            {
                InSituResearchQuestionnaire.Instance.TriggerQuestionnaire($"Beacon_{activatedBeacons}_Bonded");
            }
        }

        private void CheckObjectives()
        {
            if (collectedPearls >= totalPearlsRequired && activatedBeacons >= totalBeaconsRequired)
            {
                if (worldGenerator != null && worldGenerator.surfaceGate != null)
                {
                    worldGenerator.surfaceGate.UnlockGate();
                    Debug.Log("<color=#ffff00>[Reef Rescue]</color> All objectives met! Surface Gate is now OPEN!");
                }
            }
        }

        public void OnDiveCompleted()
        {
            if (isDiveComplete) return;
            isDiveComplete = true;

            Debug.Log("<color=#00ff88>[Reef Rescue]</color> DIVE COMPLETED! Marine ecosystem restored.");

            if (UnderwaterAudio.Instance != null)
            {
                UnderwaterAudio.Instance.PlayWinFanfare();
            }

            // Export Clinical Research Telemetry to CSV & Render 3D Heatmap
            ResearchTelemetryExporter.Instance?.ExportTelemetryToCSV();

            // Prompt Final Post-Dive In-Situ Assessment
            if (InSituResearchQuestionnaire.Instance != null)
            {
                InSituResearchQuestionnaire.Instance.TriggerQuestionnaire("Dive_Completed_SurfaceDock");
            }
        }

        public void RestartDive()
        {
            isDiveComplete = false;
            collectedPearls = 0;
            activatedBeacons = 0;

            if (diver != null)
            {
                diver.transform.position = new Vector3(0f, -4f, -4f);
                Rigidbody rb = diver.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;
            }

            if (heartRateSystem != null)
            {
                heartRateSystem.currentOxygen = heartRateSystem.maxOxygen;
                heartRateSystem.currentBPM = heartRateSystem.restingBPM;
            }

            if (worldGenerator != null)
            {
                worldGenerator.GenerateWorld();
            }
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Reef Rescue/Build Scene in Editor")]
        public static void EditorBuildScene()
        {
            ReefGameManager mgr = FindAnyObjectByType<ReefGameManager>();
            if (mgr == null)
            {
                GameObject go = new GameObject("--- REEF GAME MANAGER ---");
                mgr = go.AddComponent<ReefGameManager>();
            }

            mgr.InitializeCoreSystems();
            mgr.worldGenerator.GenerateWorld();
            mgr.SpawnOrFindDiver();

            Debug.Log("<color=#00e5ff>[Reef Rescue]</color> Scene successfully generated in Editor!");
        }
#endif
    }
}

