using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Oceanic Jet Stream / High-Speed Slipstream Current.
    /// Fast-flowing oceanic current channels that accelerate the diver, Ilu, or Skimwing across the map:
    /// - Continuous bubble vortex particles flowing along the stream vector
    /// - Accelerates rigidbodies inside the channel to 26 m/s
    /// </summary>
    public class OceanJetStream : MonoBehaviour
    {
        [Header("Jet Stream Tuning")]
        public Vector3 flowDirection = Vector3.forward;
        public float streamSpeed = 24.0f;
        public float length = 45.0f;
        public float radius = 4.0f;

        private ParticleSystem vortexFX;

        public void InitializeJetStream(Vector3 startPos, Vector3 endPos)
        {
            transform.position = (startPos + endPos) * 0.5f;
            Vector3 diff = (endPos - startPos);
            length = diff.magnitude;
            flowDirection = diff.normalized;
            transform.rotation = Quaternion.LookRotation(flowDirection);

            BuildStreamTriggerAndParticles();
        }

        private void BuildStreamTriggerAndParticles()
        {
            // 1. Trigger Capsule Collider
            CapsuleCollider trigger = gameObject.AddComponent<CapsuleCollider>();
            trigger.isTrigger = true;
            trigger.direction = 2; // Z-axis (forward)
            trigger.radius = radius;
            trigger.height = length;

            // 2. Bubble Vortex Particle Emitter
            GameObject fxGO = new GameObject("StreamVortexFX");
            fxGO.transform.SetParent(transform, false);
            fxGO.transform.localPosition = new Vector3(0f, 0f, -length * 0.5f);

            vortexFX = fxGO.AddComponent<ParticleSystem>();
            var main = vortexFX.main;
            main.startLifetime = length / streamSpeed;
            main.startSpeed = streamSpeed;
            main.startSize = 0.22f;
            main.maxParticles = 180;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.35f, 0.95f, 1f, 0.5f);

            var em = vortexFX.emission;
            em.rateOverTime = 45f;

            var shape = vortexFX.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.75f;

            UnderwaterParticleHelper.ApplyParticleMaterial(vortexFX, new Color(0.35f, 0.95f, 1f, 0.65f), true);
        }

        private void OnTriggerStay(Collider other)
        {
            Rigidbody rb = other.GetComponentInParent<Rigidbody>();
            if (rb != null)
            {
                // Accelerate through current
                rb.AddForce(flowDirection * streamSpeed * 2.5f, ForceMode.Acceleration);

                if (UnderwaterHUD.Instance != null && UnityEngine.Random.value < 0.04f)
                {
                    UnderwaterHUD.Instance.DisplayScientificLog("OCEANIC JET STREAM", "⚡ Caught in high-speed slipstream current (+24 m/s)!");
                }
            }
        }
    }
}
