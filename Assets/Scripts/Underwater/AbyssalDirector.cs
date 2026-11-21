using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// The "Abyssal Director" — Physiology-Driven Dynamic Difficulty Adjustment (DDA).
    /// Dynamically orchestrates ocean pacing, water turbidity, cross-currents,
    /// and spawns emergency oxygen bubble vents when the player enters autonomic distress.
    /// </summary>
    public class AbyssalDirector : MonoBehaviour
    {
        public static AbyssalDirector Instance { get; private set; }

        [Header("Director Pacing Metrics")]
        [Tooltip("0 = Pacified Sanctuary, 1 = Hostile Abyssal Surge")]
        [Range(0f, 1f)] public float challengeIntensity = 0.5f;
        public float dynamicFogDensity = 0.038f;
        public Vector3 oceanCurrentForce = Vector3.zero;

        [Header("Emergency Respite")]
        public bool isAssistingPlayer = false;
        private float assistTimer = 0f;

        private DiverController diver;
        private ParticleSystem rescueBubbleSystem;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreateRescueBubbleVent();
        }

        private void Start()
        {
            diver = FindAnyObjectByType<DiverController>();
        }

        private void Update()
        {
            UpdateBiometricPacing();
            ApplyEnvironmentalModulations();
            UpdateEmergencyRespite();
        }

        private void UpdateEmergencyRespite()
        {
            if (!isAssistingPlayer) return;

            assistTimer -= Time.deltaTime;
            if (assistTimer <= 0f)
            {
                isAssistingPlayer = false;
                if (rescueBubbleSystem != null)
                {
                    rescueBubbleSystem.Stop();
                }
            }
        }

        private void UpdateBiometricPacing()
        {
            if (HRVEngine.Instance == null || HeartRateSystem.Instance == null) return;

            AffectiveState state = HRVEngine.Instance.currentState;
            float stress = HeartRateSystem.Instance.stressFactor;

            switch (state)
            {
                case AffectiveState.Calm:
                    // Player has mastered composure -> gently increase challenge
                    challengeIntensity = Mathf.MoveTowards(challengeIntensity, 0.75f, Time.deltaTime * 0.05f);
                    isAssistingPlayer = false;
                    break;

                case AffectiveState.Flow:
                    // Optimal performance equilibrium
                    challengeIntensity = Mathf.MoveTowards(challengeIntensity, 0.5f, Time.deltaTime * 0.03f);
                    isAssistingPlayer = false;
                    break;

                case AffectiveState.Stressed:
                    // Sympathetic arousal -> stabilize environment
                    challengeIntensity = Mathf.MoveTowards(challengeIntensity, 0.35f, Time.deltaTime * 0.08f);
                    break;

                case AffectiveState.Panic:
                    // Player in distress -> Director triggers calming intervention
                    challengeIntensity = Mathf.MoveTowards(challengeIntensity, 0.15f, Time.deltaTime * 0.15f);
                    TriggerCalmingIntervention();
                    break;
            }
        }

        private void ApplyEnvironmentalModulations()
        {
            // 1. Modulate water visibility & turbidity
            // Calm = crystalline clear (fog 0.022), Panic = murky sediment (fog 0.055)
            float targetFog = Mathf.Lerp(0.022f, 0.058f, challengeIntensity);
            dynamicFogDensity = Mathf.Lerp(dynamicFogDensity, targetFog, Time.deltaTime * 1.5f);
            RenderSettings.fogDensity = dynamicFogDensity;

            // 2. Modulate ocean currents (Flow/Calm creates cross-currents that require navigational planning)
            if (challengeIntensity > 0.6f)
            {
                float currentAngle = Time.time * 0.15f;
                oceanCurrentForce = new Vector3(Mathf.Sin(currentAngle), 0f, Mathf.Cos(currentAngle)) * 2.2f;
            }
            else
            {
                oceanCurrentForce = Vector3.zero;
            }

            if (diver != null && oceanCurrentForce != Vector3.zero)
            {
                Rigidbody rb = diver.GetComponent<Rigidbody>();
                if (rb != null) rb.AddForce(oceanCurrentForce, ForceMode.Acceleration);
            }
        }

        private void TriggerCalmingIntervention()
        {
            if (isAssistingPlayer || diver == null) return;
            isAssistingPlayer = true;
            assistTimer = 8f;

            // Spawn soothing oxygen bubble conduit right in front of the diver
            if (rescueBubbleSystem != null)
            {
                rescueBubbleSystem.transform.position = diver.transform.position + diver.transform.forward * 3.5f + Vector3.down * 1.5f;
                rescueBubbleSystem.Play();
            }

            // Gently replenish some emergency air
            if (HeartRateSystem.Instance != null)
            {
                HeartRateSystem.Instance.ReplenishOxygen(15f);
            }

            Debug.Log("<color=#00ffc8>[Abyssal Director]</color> Deployed Emergency Oxygen Vent to stabilize player autonomic distress.");
        }

        private void CreateRescueBubbleVent()
        {
            GameObject ventGO = new GameObject("AbyssalRescueBubbleVent");
            ventGO.transform.SetParent(transform, false);

            rescueBubbleSystem = ventGO.AddComponent<ParticleSystem>();
            var main = rescueBubbleSystem.main;
            main.startLifetime = 4f;
            main.startSpeed = 2f;
            main.startSize = 0.25f;
            main.maxParticles = 60;
            main.gravityModifier = -0.6f; // Rise swiftly
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = rescueBubbleSystem.emission;
            emission.rateOverTime = 20f;

            var shape = rescueBubbleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.8f;

            UnderwaterParticleHelper.ApplyParticleMaterial(rescueBubbleSystem, new Color(0.3f, 0.95f, 1f, 0.75f), true);
        }
    }
}

