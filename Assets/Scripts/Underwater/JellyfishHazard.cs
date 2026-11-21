using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Bioluminescent deep-sea jellyfish that float in coral trenches.
    /// Interacts directly with player biofeedback:
    /// - If Diver is Calm (Low HR): Jellyfish glow with a gentle cyan hue and allow the diver to swim past safely.
    /// - If Diver is Stressed (High HR): Jellyfish become agitated, pulse bright warning crimson/violet, and zap the diver with a knockback shock!
    /// </summary>
    public class JellyfishHazard : MonoBehaviour
    {
        [Header("Movement")]
        public float pulseFrequency = 1.2f;
        public float swimSpeed = 0.8f;
        public float verticalRange = 3.5f;

        [Header("Visuals")]
        public Color calmColor = new Color(0.2f, 0.9f, 1f, 0.75f);
        public Color agitatedColor = new Color(1f, 0.2f, 0.4f, 0.95f);

        private Vector3 startPos;
        private Transform bellTransform;
        private Renderer bellRenderer;
        private Light jLight;
        private float pulsePhase;

        public void Initialize(Vector3 position)
        {
            transform.position = position;
            startPos = position;

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material jellyMat = new Material(uLit);
            jellyMat.color = calmColor;
            jellyMat.EnableKeyword("_EMISSION");
            jellyMat.SetColor("_EmissionColor", calmColor * 2f);

            // 1. Jellyfish Bell (Hemisphere/Squashed Sphere)
            GameObject bell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bell.name = "JellyBell";
            bell.transform.SetParent(transform, false);
            bell.transform.localScale = new Vector3(1.2f, 0.75f, 1.2f);
            bellRenderer = bell.GetComponent<Renderer>();
            bellRenderer.sharedMaterial = jellyMat;
            bellTransform = bell.transform;

            Collider bellCol = bell.GetComponent<Collider>();
            if (bellCol != null) DestroyImmediate(bellCol);

            // 2. Trailing Tentacles (Thin cylinders)
            for (int i = 0; i < 5; i++)
            {
                float angle = (i / 5f) * Mathf.PI * 2f;
                Vector3 tentacleOffset = new Vector3(Mathf.Cos(angle) * 0.4f, -0.6f, Mathf.Sin(angle) * 0.4f);

                GameObject tentacle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tentacle.name = $"Tentacle_{i}";
                tentacle.transform.SetParent(transform, false);
                tentacle.transform.localPosition = tentacleOffset;
                tentacle.transform.localScale = new Vector3(0.04f, 1.2f, 0.04f);
                tentacle.GetComponent<Renderer>().sharedMaterial = jellyMat;

                Collider col = tentacle.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);
            }

            // 3. Bioluminescent Point Light
            jLight = gameObject.AddComponent<Light>();
            jLight.type = LightType.Point;
            jLight.range = 8f;
            jLight.color = calmColor;
            jLight.intensity = 1.5f;

            // 4. Trigger Sphere
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 1.8f;
        }

        private void Update()
        {
            pulsePhase += Time.deltaTime * pulseFrequency;

            // Gentle pulsating locomotion
            float bellScale = 1.2f + Mathf.Sin(pulsePhase * 3f) * 0.2f;
            if (bellTransform != null)
            {
                bellTransform.localScale = new Vector3(bellScale, 0.75f - Mathf.Sin(pulsePhase * 3f) * 0.1f, bellScale);
            }

            // Bob up and down in trench
            float newY = startPos.y + Mathf.Sin(pulsePhase) * verticalRange;
            transform.position = new Vector3(startPos.x, newY, startPos.z);

            // React to player stress
            UpdateBioReaction();
        }

        private void UpdateBioReaction()
        {
            HeartRateSystem hr = HeartRateSystem.Instance;
            bool isStressed = hr != null && hr.isStressed;

            Color targetColor = isStressed ? agitatedColor : calmColor;

            if (bellRenderer != null && bellRenderer.material != null)
            {
                bellRenderer.material.color = Color.Lerp(bellRenderer.material.color, targetColor, Time.deltaTime * 3f);
                bellRenderer.material.SetColor("_EmissionColor", targetColor * (isStressed ? 3.5f : 1.8f));
            }

            if (jLight != null)
            {
                jLight.color = Color.Lerp(jLight.color, targetColor, Time.deltaTime * 3f);
                jLight.intensity = isStressed ? 3.5f + Mathf.PingPong(Time.time * 6f, 1.5f) : 1.5f;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            DiverController diver = other.GetComponentInParent<DiverController>();
            if (diver != null)
            {
                HeartRateSystem hr = HeartRateSystem.Instance;
                if (hr != null && hr.isStressed)
                {
                    // Stressed player gets stung / pushed back
                    Rigidbody rb = diver.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 pushDir = (diver.transform.position - transform.position).normalized + Vector3.up * 0.5f;
                        rb.AddForce(pushDir * 18f, ForceMode.Impulse);
                    }

                    // Spike heart rate slightly from the shock
                    hr.FeedSensorBPM(hr.currentBPM + 15f);

                    if (UnderwaterAudio.Instance != null)
                    {
                        UnderwaterAudio.Instance.PlayHarmonicTone(220f, 0.4f); // Low shock buzz
                    }
                }
            }
        }
    }
}

