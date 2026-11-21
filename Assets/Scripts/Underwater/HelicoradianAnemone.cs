using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Interactive Pandoran "Helicoradian" Spiral Fan Anemone.
    /// Inspired by the iconic spiral fan-flora from Avatar:
    /// - When Diver is Calm (Low HR): Flora remains gracefully unfurled in a vibrant spiral,
    ///   glowing with warm amber and cyan bioluminescence, revealing pearls in its center.
    /// - When Diver Panics (High HR / Stress): Senses bio-electric and acoustic shockwaves,
    ///   instantly snapping shut into the seafloor with a pneumatic whoosh!
    /// </summary>
    public class HelicoradianAnemone : MonoBehaviour
    {
        [Header("Flora Visuals")]
        public Color openGlowColor = new Color(1f, 0.55f, 0.1f); // Warm radiant amber
        public Color closedColor = new Color(0.2f, 0.1f, 0.3f);  // Dormant violet
        public float fullHeight = 3.2f;
        public float retractedHeight = 0.4f;

        private Transform stemTransform;
        private Transform spiralFan;
        private Renderer fanRenderer;
        private Light floraLight;

        private bool isRetracted = false;
        private float currentScaleY = 1f;
        private float spiralRotation = 0f;

        public void InitializeAnemone(Vector3 position, Color glowColor)
        {
            transform.position = position;
            openGlowColor = glowColor;

            BuildAnemoneModel();
        }

        private void BuildAnemoneModel()
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material stemMat = new Material(uLit);
            stemMat.color = new Color(0.08f, 0.15f, 0.12f);

            Material fanMat = new Material(uLit);
            fanMat.color = openGlowColor;
            fanMat.EnableKeyword("_EMISSION");
            fanMat.SetColor("_EmissionColor", openGlowColor * 2.5f);

            // 1. Root Stem
            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "AnemoneStem";
            stem.transform.SetParent(transform, false);
            stem.transform.localScale = new Vector3(0.35f, 1.2f, 0.35f);
            stem.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            stem.GetComponent<Renderer>().sharedMaterial = stemMat;
            stemTransform = stem.transform;
            RemoveCollider(stem);

            // 2. Spiral Fan Blossom (Concentric stepped discs creating a spiral staircase fan)
            GameObject fanRoot = new GameObject("SpiralFanBlossom");
            fanRoot.transform.SetParent(stem.transform, false);
            fanRoot.transform.localPosition = new Vector3(0f, 1f, 0f);
            spiralFan = fanRoot.transform;

            for (int i = 0; i < 6; i++)
            {
                float t = i / 6f;
                GameObject petal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                petal.name = $"Petal_{i}";
                petal.transform.SetParent(spiralFan, false);
                petal.transform.localPosition = new Vector3(0f, t * 1.4f, 0f);
                petal.transform.localRotation = Quaternion.Euler(0f, i * 45f, 15f);
                petal.transform.localScale = new Vector3(1.8f - t * 0.8f, 0.08f, 0.8f - t * 0.3f);

                Renderer r = petal.GetComponent<Renderer>();
                r.sharedMaterial = fanMat;
                if (i == 0) fanRenderer = r;
                RemoveCollider(petal);
            }

            // 3. Bioluminescent Point Light
            floraLight = gameObject.AddComponent<Light>();
            floraLight.type = LightType.Point;
            floraLight.color = openGlowColor;
            floraLight.range = 10f;
            floraLight.intensity = 2f;
        }

        private void Update()
        {
            HeartRateSystem hr = HeartRateSystem.Instance;
            bool shouldRetract = hr != null && hr.isStressed;

            // Retraction / Expansion animation
            float targetScale = shouldRetract ? 0.15f : 1f;
            currentScaleY = Mathf.Lerp(currentScaleY, targetScale, Time.deltaTime * (shouldRetract ? 8f : 2.5f));

            if (stemTransform != null)
            {
                stemTransform.localScale = new Vector3(0.35f, 1.2f * currentScaleY, 0.35f);
            }

            // Slow idle spiral rotation when open
            if (!shouldRetract && spiralFan != null)
            {
                spiralRotation += Time.deltaTime * 20f;
                spiralFan.localRotation = Quaternion.Euler(0f, spiralRotation, 0f);
            }

            // Audio & light reaction on sudden retract
            if (shouldRetract && !isRetracted)
            {
                isRetracted = true;
                if (UnderwaterAudio.Instance != null)
                {
                    UnderwaterAudio.Instance.PlayHarmonicTone(180f, 0.15f); // Soft pneumatic whoosh
                }
            }
            else if (!shouldRetract && isRetracted)
            {
                isRetracted = false;
            }

            // Dim light when retracted
            if (floraLight != null)
            {
                floraLight.intensity = Mathf.Lerp(floraLight.intensity, shouldRetract ? 0.2f : 2f, Time.deltaTime * 4f);
            }
        }

        private void RemoveCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }
    }
}

