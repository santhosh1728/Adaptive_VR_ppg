using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Fractal Gorgonian Sea Fan Coral with Organic Sway and Bioluminescent Ripple Waves.
    /// Features:
    /// - Multi-tiered fractal branching lattice structure
    /// - Hydrodynamic current sway physics
    /// - Reactive touch: sending a glowing bio-electric ripple wave through its branches
    /// </summary>
    public class GorgonianSeaFan : MonoBehaviour
    {
        [Header("Coral Visuals")]
        public Color primaryGlowColor = new Color(0.95f, 0.25f, 0.85f); // Vibrant Pandoran Violet/Magenta
        public Color pulseColor = new Color(0.2f, 1f, 0.95f);          // Electric Cyan Ripple

        private List<Renderer> branchRenderers = new List<Renderer>();
        private Material fanMaterial;
        private Quaternion baseRotation;
        private float swayPhase;
        private float pulseTimer = 0f;

        public void InitializeSeaFan(Vector3 position, Quaternion rotation, Color color, float scale = 1.0f)
        {
            transform.position = position;
            transform.rotation = rotation;
            baseRotation = rotation;
            primaryGlowColor = color;
            swayPhase = UnityEngine.Random.Range(0f, 10f);

            BuildFractalLattice(scale);
        }

        private void BuildFractalLattice(float masterScale)
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            fanMaterial = new Material(uLit);
            fanMaterial.color = primaryGlowColor;
            fanMaterial.EnableKeyword("_EMISSION");
            fanMaterial.SetColor("_EmissionColor", primaryGlowColor * 2.2f);
            fanMaterial.SetFloat("_Smoothness", 0.85f);

            // Holdfast Base
            GameObject holdfast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            holdfast.name = "Holdfast";
            holdfast.transform.SetParent(transform, false);
            holdfast.transform.localPosition = Vector3.zero;
            holdfast.transform.localScale = new Vector3(0.6f, 0.3f, 0.6f) * masterScale;
            holdfast.GetComponent<Renderer>().sharedMaterial = fanMaterial;
            branchRenderers.Add(holdfast.GetComponent<Renderer>());
            RemoveCollider(holdfast);

            // Level 0: Main Stipe Trunk
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "TrunkStipe";
            trunk.transform.SetParent(transform, false);
            trunk.transform.localPosition = new Vector3(0f, 0.6f * masterScale, 0f);
            trunk.transform.localScale = new Vector3(0.18f, 0.6f, 0.18f) * masterScale;
            trunk.GetComponent<Renderer>().sharedMaterial = fanMaterial;
            branchRenderers.Add(trunk.GetComponent<Renderer>());
            RemoveCollider(trunk);

            // Level 1: Primary Fan Bifurcations (-30 deg, 0 deg, +30 deg)
            float[] angles = new float[] { -28f, -8f, 12f, 32f };
            for (int i = 0; i < angles.Length; i++)
            {
                float ang = angles[i];
                GameObject primaryBranch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                primaryBranch.name = $"PrimaryBranch_{i}";
                primaryBranch.transform.SetParent(trunk.transform, false);
                primaryBranch.transform.localPosition = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * 0.4f, 1.0f + (i * 0.15f), 0f);
                primaryBranch.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
                primaryBranch.transform.localScale = new Vector3(0.12f, 0.8f, 0.12f);
                primaryBranch.GetComponent<Renderer>().sharedMaterial = fanMaterial;
                branchRenderers.Add(primaryBranch.GetComponent<Renderer>());
                RemoveCollider(primaryBranch);

                // Level 2: Secondary Lace Twigs
                for (int t = 0; t < 3; t++)
                {
                    float twigAng = (t % 2 == 0 ? 1 : -1) * UnityEngine.Random.Range(20f, 40f);
                    GameObject twig = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    twig.name = $"Twig_{i}_{t}";
                    twig.transform.SetParent(primaryBranch.transform, false);
                    twig.transform.localPosition = new Vector3(0f, 0.3f + t * 0.25f, 0f);
                    twig.transform.localRotation = Quaternion.Euler(0f, 0f, twigAng);
                    twig.transform.localScale = new Vector3(0.08f, 0.45f, 0.08f);
                    twig.GetComponent<Renderer>().sharedMaterial = fanMaterial;
                    branchRenderers.Add(twig.GetComponent<Renderer>());
                    RemoveCollider(twig);
                }
            }
        }

        private void Update()
        {
            // Hydrodynamic ocean current swaying
            float t = Time.time * 1.3f + swayPhase;
            float swayAngle = Mathf.Sin(t) * 7.5f;
            float swayTwist = Mathf.Cos(t * 0.7f) * 4.0f;
            transform.rotation = baseRotation * Quaternion.Euler(swayAngle, swayTwist, 0f);

            // Bioluminescent pulse wave animation
            if (pulseTimer > 0f)
            {
                pulseTimer -= Time.deltaTime;
                float progress = Mathf.Clamp01(pulseTimer / 1.8f);
                Color currentColor = Color.Lerp(primaryGlowColor, pulseColor, Mathf.Sin(progress * Mathf.PI));
                if (fanMaterial != null)
                {
                    fanMaterial.color = currentColor;
                    fanMaterial.SetColor("_EmissionColor", currentColor * (2.2f + progress * 3f));
                }
            }
        }

        public void TriggerBioluminescentPulse()
        {
            if (pulseTimer > 0f) return;
            pulseTimer = 1.8f;

            if (UnderwaterAudio.Instance != null)
            {
                UnderwaterAudio.Instance.PlayHarmonicTone(660f, 0.2f);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<DiverController>() != null || other.GetComponentInParent<MantaCompanionAI>() != null)
            {
                TriggerBioluminescentPulse();
            }
        }

        private void RemoveCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }
    }
}

