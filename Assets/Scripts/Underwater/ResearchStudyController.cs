using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnderwaterReef
{
    public enum ResearchStudyCondition
    {
        ConditionA_Control,          // Baseline: Fixed lighting, constant currents, zero biofeedback
        ConditionB_ExplicitHUD,       // Standard biofeedback: Direct numeric BPM / ECG on HUD
        ConditionC_AmbientAdaptive   // Novel Ambient Bio-Adaptation: Bioluminescence, optical Jerlov extinction, and currents adapt without numeric HUD distraction
    }

    /// <summary>
    /// Master Coordinator for Experimental A/B/C Trials & Research Protocol.
    /// Manages:
    /// - Blinded / automated condition switching (F1: Control, F2: Explicit HUD, F3: Ambient Adaptive).
    /// - Participant ID & Trial numbering.
    /// - Environmental biofeedback gating per condition.
    /// - Study event marker injection into telemetry streams.
    /// </summary>
    public class ResearchStudyController : MonoBehaviour
    {
        public static ResearchStudyController Instance { get; private set; }

        [Header("Participant Metadata")]
        public string participantID = "P01";
        public int trialNumber = 1;

        [Header("Active Study Condition")]
        public ResearchStudyCondition activeCondition = ResearchStudyCondition.ConditionC_AmbientAdaptive;

        [Header("Condition Feature Toggles")]
        public bool isBiofeedbackActive => activeCondition != ResearchStudyCondition.ConditionA_Control;
        public bool isExplicitHUDVisible => activeCondition == ResearchStudyCondition.ConditionB_ExplicitHUD;
        public bool isAmbientBioAdaptiveActive => activeCondition == ResearchStudyCondition.ConditionC_AmbientAdaptive;

        public event Action<ResearchStudyCondition> OnConditionChanged;

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
            HandleConditionHotkeys();
        }

        private void HandleConditionHotkeys()
        {
            bool f1 = false, f2 = false, f3 = false, f4 = false, f5 = false, f6 = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                f1 = Keyboard.current.f1Key.wasPressedThisFrame;
                f2 = Keyboard.current.f2Key.wasPressedThisFrame;
                f3 = Keyboard.current.f3Key.wasPressedThisFrame;
                f4 = Keyboard.current.f4Key.wasPressedThisFrame;
                f5 = Keyboard.current.f5Key.wasPressedThisFrame;
                f6 = Keyboard.current.f6Key.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetKeyDown(KeyCode.F1)) f1 = true;
                if (Input.GetKeyDown(KeyCode.F2)) f2 = true;
                if (Input.GetKeyDown(KeyCode.F3)) f3 = true;
                if (Input.GetKeyDown(KeyCode.F4)) f4 = true;
                if (Input.GetKeyDown(KeyCode.F5)) f5 = true;
                if (Input.GetKeyDown(KeyCode.F6)) f6 = true;
            }
            catch { }
#endif

            if (f1) SetCondition(ResearchStudyCondition.ConditionA_Control);
            else if (f2) SetCondition(ResearchStudyCondition.ConditionB_ExplicitHUD);
            else if (f3) SetCondition(ResearchStudyCondition.ConditionC_AmbientAdaptive);
            else if (f4) CycleNextCondition();
            else if (f5) CycleNextParticipant();
            else if (f6)
            {
                // Trigger In-Situ Questionnaire checkpoint manually
                if (InSituResearchQuestionnaire.Instance != null)
                {
                    InSituResearchQuestionnaire.Instance.TriggerQuestionnaire("Manual_F6_Checkpoint");
                }
            }
        }

        public void SetCondition(ResearchStudyCondition newCondition)
        {
            activeCondition = newCondition;
            Debug.Log($"<color=#38ef7d>[Research Study]</color> Active Condition set to: <b>{newCondition}</b> (Participant: {participantID}, Trial: {trialNumber})");
            
            // Record event marker in telemetry
            if (ResearchTelemetryExporter.Instance != null)
            {
                ResearchTelemetryExporter.Instance.RecordDataPoint($"SwitchCondition_{newCondition}");
            }

            OnConditionChanged?.Invoke(activeCondition);
        }

        public void CycleNextCondition()
        {
            int next = ((int)activeCondition + 1) % 3;
            SetCondition((ResearchStudyCondition)next);
        }

        public void CycleNextParticipant()
        {
            int pNum = 1;
            if (participantID.StartsWith("P") && int.TryParse(participantID.Substring(1), out int parsed))
            {
                pNum = parsed + 1;
            }
            participantID = $"P{pNum:D2}";
            trialNumber = 1;
            Debug.Log($"<color=#38ef7d>[Research Study]</color> Advanced to Participant: <b>{participantID}</b> (Trial {trialNumber})");

            // Recalibrate baseline for the new participant
            if (HeartRateSystem.Instance != null)
            {
                HeartRateSystem.Instance.StartCalibration(30f);
            }
        }

        public string GetConditionBadgeText()
        {
            switch (activeCondition)
            {
                case ResearchStudyCondition.ConditionA_Control:
                    return "🔬 [STUDY MODE: CONDITION A - CONTROL (No Biofeedback)]";
                case ResearchStudyCondition.ConditionB_ExplicitHUD:
                    return "🔬 [STUDY MODE: CONDITION B - EXPLICIT HUD (Numeric ECG)]";
                case ResearchStudyCondition.ConditionC_AmbientAdaptive:
                    return "🔬 [STUDY MODE: CONDITION C - AMBIENT BIO-ADAPTATION]";
                default:
                    return "🔬 [STUDY MODE: ACTIVE]";
            }
        }

        public Color GetConditionColor()
        {
            switch (activeCondition)
            {
                case ResearchStudyCondition.ConditionA_Control:
                    return new Color(0.7f, 0.7f, 0.75f);
                case ResearchStudyCondition.ConditionB_ExplicitHUD:
                    return new Color(0.2f, 0.85f, 1f);
                case ResearchStudyCondition.ConditionC_AmbientAdaptive:
                    return new Color(0.25f, 1f, 0.5f);
                default:
                    return Color.white;
            }
        }
    }
}

