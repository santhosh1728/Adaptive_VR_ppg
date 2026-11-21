using UnityEngine;

namespace UnderwaterReef
{
    public enum PredatorState
    {
        DormantPatrol,
        InvestigatingPulse,
        StalkingDiver,
        WarningCircle
    }

    /// <summary>
    /// Apex predator / submarine disabled and removed permanently per user preference.
    /// Safely purges any remaining GameObject or components from the scene immediately.
    /// </summary>
    public class ApexPredatorAI : MonoBehaviour
    {
        public PredatorState currentState = PredatorState.DormantPatrol;
        public float currentPerceptionRadius = 0f;

        private void Awake()
        {
            DestroyImmediate(gameObject);
        }

        private void OnEnable()
        {
            DestroyImmediate(gameObject);
        }

        private void Start()
        {
            DestroyImmediate(gameObject);
        }

        public void InitializePredator(Vector3 spawnPos, Vector3[] waypoints)
        {
            DestroyImmediate(gameObject);
        }
    }
}
