using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Hydrothermal Black Smoker Mineral Chimney.
    /// In Avatar's deep ocean trenches, geothermal vents spew superheated mineral plumes:
    /// - Towering 12-meter volcanic basalt chimney spire
    /// - Plume of black/cyan thermal particles
    /// - Updraft Physics: Entering the thermal plume launches the diver or Ilu upward like a geyser!
    /// </summary>
    public class BlackSmokerChimney : MonoBehaviour
    {
        [Header("Chimney Tuning")]
        public float chimneyHeight = 12.0f;
        public float updraftThrust = 32.0f;

        private ParticleSystem plumeFX;

        public void InitializeChimney(Vector3 basePos)
        {
            transform.position = basePos;
            BuildChimneyModel();
        }

        private void BuildChimneyModel()
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material rockMat = new Material(uLit);
            rockMat.color = new Color(0.04f, 0.05f, 0.07f); // Volcanic basalt
            rockMat.SetFloat("_Smoothness", 0.15f);

            Material fissureMat = new Material(uLit);
            Color ventCol = new Color(0.1f, 0.95f, 0.85f);
            fissureMat.color = ventCol;
            fissureMat.EnableKeyword("_EMISSION");
            fissureMat.SetColor("_EmissionColor", ventCol * 3.5f);

            // 1. Segmented chimney spire
            int segs = 4;
            float segH = chimneyHeight / segs;
            for (int i = 0; i < segs; i++)
            {
                float radius = Mathf.Lerp(2.2f, 0.9f, (float)i / segs);
                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                seg.name = $"ChimneySeg_{i}";
                seg.transform.SetParent(transform, false);
                seg.transform.localPosition = new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), (i + 0.5f) * segH, UnityEngine.Random.Range(-0.2f, 0.2f));
                seg.transform.localRotation = Quaternion.Euler(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-4f, 4f));
                seg.transform.localScale = new Vector3(radius, segH * 0.5f, radius);
                seg.GetComponent<Renderer>().sharedMaterial = rockMat;
            }

            // 2. Glowing hydrothermal rim lip
            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "VentMouthRim";
            rim.transform.SetParent(transform, false);
            rim.transform.localPosition = new Vector3(0f, chimneyHeight + 0.1f, 0f);
            rim.transform.localScale = new Vector3(1.1f, 0.15f, 1.1f);
            rim.GetComponent<Renderer>().sharedMaterial = fissureMat;

            // 3. Thermal Plume Particle Emitter
            GameObject plumeGO = new GameObject("ThermalPlumeFX");
            plumeGO.transform.SetParent(transform, false);
            plumeGO.transform.localPosition = new Vector3(0f, chimneyHeight + 0.3f, 0f);

            plumeFX = plumeGO.AddComponent<ParticleSystem>();
            var main = plumeFX.main;
            main.startLifetime = 3.5f;
            main.startSpeed = 7.5f;
            main.startSize = 0.45f;
            main.maxParticles = 140;
            main.gravityModifier = -0.4f; // Strongly buoyant
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.12f, 0.85f, 0.95f, 0.65f);

            var em = plumeFX.emission;
            em.rateOverTime = 32f;

            var shape = plumeFX.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.6f;

            UnderwaterParticleHelper.ApplyParticleMaterial(plumeFX, new Color(0.12f, 0.85f, 0.95f, 0.65f), true);

            // 4. Updraft Trigger Volume
            CapsuleCollider updraftCol = gameObject.AddComponent<CapsuleCollider>();
            updraftCol.isTrigger = true;
            updraftCol.radius = 2.8f;
            updraftCol.height = 20.0f;
            updraftCol.center = new Vector3(0f, chimneyHeight + 8f, 0f);
        }

        private void OnTriggerStay(Collider other)
        {
            Rigidbody rb = other.GetComponentInParent<Rigidbody>();
            if (rb != null)
            {
                // Launch soaring upward in thermal current
                rb.AddForce(Vector3.up * updraftThrust, ForceMode.Acceleration);

                if (UnderwaterAudio.Instance != null && UnityEngine.Random.value < 0.05f)
                {
                    UnderwaterAudio.Instance.PlayHarmonicTone(120f, 0.3f);
                }
            }
        }
    }
}
