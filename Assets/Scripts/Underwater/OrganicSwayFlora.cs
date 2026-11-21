using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Animates organic marine flora (kelp fronds, willow tendrils, soft coral branches)
    /// with fluid, natural sinusoidal swaying driven by ocean water currents.
    /// </summary>
    public class OrganicSwayFlora : MonoBehaviour
    {
        public float swayFrequency = 1.2f;
        public float swayAngle = 8f;
        public Vector3 swayAxis = Vector3.forward;

        private float randomPhase;
        private Quaternion baseRotation;

        private Renderer cachedRenderer;
        private Color baseEmissionColor = Color.clear;
        private bool hasEmission = false;

        private void Start()
        {
            baseRotation = transform.localRotation;
            randomPhase = (transform.position.x * 0.35f) + (transform.position.z * 0.45f);

            cachedRenderer = GetComponent<Renderer>();
            if (cachedRenderer != null && cachedRenderer.sharedMaterial != null)
            {
                if (cachedRenderer.sharedMaterial.HasProperty("_EmissionColor"))
                {
                    baseEmissionColor = cachedRenderer.sharedMaterial.GetColor("_EmissionColor");
                    hasEmission = baseEmissionColor.maxColorComponent > 0.05f;
                }
            }
        }

        private void Update()
        {
            // Biological resonance: sway frequency entrains to player's derived breathing rate (RSA)
            float freq = swayFrequency;
            HRVEngine hrv = HRVEngine.Instance;
            if (hrv != null)
            {
                float rsaHz = hrv.derivedRespirationBPM / 60f;
                freq = Mathf.Lerp(swayFrequency, rsaHz * 4.2f, 0.4f);
            }

            float wave = Mathf.Sin((Time.time * freq) + randomPhase);
            float wave2 = Mathf.Cos((Time.time * freq * 0.7f) + randomPhase * 1.3f) * 0.4f;
            transform.localRotation = baseRotation * Quaternion.AngleAxis((wave + wave2) * swayAngle, swayAxis);

            // Bioluminescent blooming when Tsaheylu Bio-Bond is active (Phi >= 65%)
            if (hasEmission && cachedRenderer != null && hrv != null)
            {
                float bondBonus = Mathf.Clamp01((hrv.tsaheyluBondIndex - 40f) / 50f);
                float pulse = 0.8f + Mathf.Sin(Time.time * 2.0f + randomPhase) * 0.35f * bondBonus;
                Color glow = Color.Lerp(baseEmissionColor, hrv.tsaheyluBondColor * 2.2f, bondBonus * 0.6f);
                cachedRenderer.sharedMaterial.SetColor("_EmissionColor", glow * pulse);
            }
        }
    }
}

