using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Sunken Scientific Research Pods scattered across the coral reef.
    /// When scanned/inspected with 'E', reveals real-world marine biology and ecological
    /// science data (coral bleaching, micro-algae symbiosis, acoustic restoration)
    /// and grants an oxygen bonus to the diver.
    /// </summary>
    public class OceanicRelic : ReefInteractable
    {
        [Header("Relic Lore Data")]
        public string relicTitle = "Marine Research Log #01";
        [TextArea(3, 6)]
        public string scientificData = "CORAL BLEACHING & SYMBIOSIS: Reef-building corals share an obligate mutualistic symbiosis with photosynthetic dinoflagellates (Symbiodiniaceae). Elevated thermal stress causes breakdown of photosynthesis, triggering expulsion of the microalgae and leaving the calcium carbonate skeleton bleached white.";

        [Header("Visuals")]
        public Color relicGlow = new Color(0.95f, 0.4f, 1f); // Radiant violet
        public bool isInspected = false;

        private Renderer tabletRenderer;
        private Light relicLight;

        private void Awake()
        {
            interactableType = InteractableType.Pearl; // Uses standard interaction
            interactionPrompt = "[E] Inspect Scientific Research Pod";

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material mat = new Material(uLit);
            mat.color = relicGlow;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", relicGlow * 2.5f);

            // Relic container (Geometric hexagonal canister)
            GameObject pod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pod.name = "PodVisual";
            pod.transform.SetParent(transform, false);
            pod.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
            pod.transform.localRotation = Quaternion.Euler(20f, 45f, 0f);
            tabletRenderer = pod.GetComponent<Renderer>();
            tabletRenderer.sharedMaterial = mat;

            Collider c = pod.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);

            relicLight = gameObject.AddComponent<Light>();
            relicLight.type = LightType.Point;
            relicLight.color = relicGlow;
            relicLight.range = 8f;
            relicLight.intensity = 2f;

            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 2.2f;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up * 25f * Time.deltaTime, Space.World);
        }

        public override void Interact(DiverController diver)
        {
            if (isInspected) return;
            isInspected = true;
            canInteract = false;

            // Replenish oxygen as research grant
            if (HeartRateSystem.Instance != null)
            {
                HeartRateSystem.Instance.ReplenishOxygen(25f);
            }

            if (UnderwaterAudio.Instance != null)
            {
                UnderwaterAudio.Instance.PlayHarmonicTone(659.25f, 0.6f); // Harmonic E chord
            }

            // Display in HUD / Log
            UnderwaterHUD hud = UnderwaterHUD.Instance;
            if (hud != null)
            {
                hud.DisplayScientificLog(relicTitle, scientificData);
            }

            // Record research marker in telemetry
            ResearchTelemetryExporter.Instance?.RecordDataPoint($"Inspected_{relicTitle}");

            Debug.Log($"<color=#ea00d9>[Scientific Relic]</color> {relicTitle}: {scientificData}");
        }
    }
}

