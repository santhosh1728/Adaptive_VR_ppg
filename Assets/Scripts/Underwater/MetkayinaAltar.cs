using System;
using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Metkayina Sacred Pearl Altar.
    /// An ancient coral and volcanic stone basin where the diver offers harvested glowing pearls
    /// to Eywa in exchange for permanent oceanic enhancements:
    /// - Tier 1 (1 Pearl): Metkayina Tail Surge (+35% Swim & Walk Speed)
    /// - Tier 2 (2 Pearls): Breath of Eywa (+50% Maximum Oxygen Tank)
    /// - Tier 3 (2 Pearls): Radiant Pandoran Aura (Intensified Bioluminescent Glow)
    /// </summary>
    public class MetkayinaAltar : ReefInteractable
    {
        public static MetkayinaAltar Instance { get; private set; }

        [Header("Crafting Progress")]
        public int craftingTier = 0;
        public int pearlsOfferedTotal = 0;

        [Header("Visuals")]
        public Color basinGlowColor = new Color(0.2f, 0.95f, 1f);
        private Light altarLight;
        private Renderer basinRenderer;
        private float glowPhase;

        private void Awake()
        {
            Instance = this;
            interactableType = InteractableType.Beacon;
            UpdatePrompt();
        }

        public void InitializeAltar(Vector3 spawnPos)
        {
            transform.position = spawnPos;
            BuildAltarModel();
            UpdatePrompt();
        }

        private void BuildAltarModel()
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material stoneMat = new Material(uLit);
            stoneMat.color = new Color(0.08f, 0.12f, 0.16f);
            stoneMat.SetFloat("_Smoothness", 0.4f);

            Material waterMat = new Material(uLit);
            waterMat.color = basinGlowColor;
            waterMat.EnableKeyword("_EMISSION");
            waterMat.SetColor("_EmissionColor", basinGlowColor * 2.8f);

            // 1. Pedestal Base
            GameObject baseGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseGO.name = "AltarBase";
            baseGO.transform.SetParent(transform, false);
            baseGO.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            baseGO.transform.localScale = new Vector3(3.2f, 0.5f, 3.2f);
            baseGO.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // 2. Carved Basin Bowl
            GameObject bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bowl.name = "SacredBowl";
            bowl.transform.SetParent(transform, false);
            bowl.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            bowl.transform.localScale = new Vector3(2.4f, 0.4f, 2.4f);
            bowl.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // 3. Glowing Sacred Water Pool
            GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pool.name = "SacredPool";
            pool.transform.SetParent(transform, false);
            pool.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            pool.transform.localScale = new Vector3(2.0f, 0.08f, 2.0f);
            pool.GetComponent<Renderer>().sharedMaterial = waterMat;
            basinRenderer = pool.GetComponent<Renderer>();
            Collider pc = pool.GetComponent<Collider>();
            if (pc != null) DestroyImmediate(pc);

            // 4. Four Surrounding Coral Runestone Pillars
            for (int i = 0; i < 4; i++)
            {
                float angle = (i / 4f) * Mathf.PI * 2f;
                Vector3 pPos = new Vector3(Mathf.Cos(angle) * 2.0f, 1.4f, Mathf.Sin(angle) * 2.0f);

                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"RunestonePillar_{i}";
                pillar.transform.SetParent(transform, false);
                pillar.transform.localPosition = pPos;
                pillar.transform.localScale = new Vector3(0.35f, 0.9f, 0.35f);
                pillar.GetComponent<Renderer>().sharedMaterial = stoneMat;

                // Glowing crystal tip
                GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tip.transform.SetParent(pillar.transform, false);
                tip.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                tip.transform.localScale = Vector3.one * 1.5f;
                tip.GetComponent<Renderer>().sharedMaterial = waterMat;
                Collider tc = tip.GetComponent<Collider>();
                if (tc != null) DestroyImmediate(tc);
            }

            // 5. Point Light
            altarLight = gameObject.AddComponent<Light>();
            altarLight.type = LightType.Point;
            altarLight.color = basinGlowColor;
            altarLight.range = 14f;
            altarLight.intensity = 2.4f;

            // 6. Interaction Trigger
            SphereCollider sc = GetComponent<SphereCollider>();
            if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 4.2f;
        }

        private void Update()
        {
            glowPhase += Time.deltaTime * 2.5f;
            if (altarLight != null)
            {
                altarLight.intensity = 2.4f + Mathf.Sin(glowPhase) * 0.6f;
            }

            UpdatePrompt();
        }

        private void UpdatePrompt()
        {
            int pearls = ReefGameManager.Instance != null ? ReefGameManager.Instance.collectedPearls : 0;

            if (craftingTier == 0)
            {
                interactionPrompt = pearls >= 1 ? "[E] Offer 1 Pearl: Unlock Metkayina Tail Surge (+35% Speed)" : "Metkayina Altar: Requires 1 Pearl for Speed Blessing";
                canInteract = pearls >= 1;
            }
            else if (craftingTier == 1)
            {
                interactionPrompt = pearls >= 2 ? "[E] Offer 2 Pearls: Unlock Breath of Eywa (+50% Oxygen)" : "Metkayina Altar: Requires 2 Pearls for Oxygen Blessing";
                canInteract = pearls >= 2;
            }
            else if (craftingTier == 2)
            {
                interactionPrompt = pearls >= 2 ? "[E] Offer 2 Pearls: Unlock Pandoran Radiant Aura" : "Metkayina Altar: Requires 2 Pearls for Radiant Aura";
                canInteract = pearls >= 2;
            }
            else
            {
                interactionPrompt = "Metkayina Altar: All Sacred Blessings Mastered!";
                canInteract = false;
            }
        }

        public override void Interact(DiverController diver)
        {
            if (!canInteract || ReefGameManager.Instance == null) return;

            if (craftingTier == 0 && ReefGameManager.Instance.collectedPearls >= 1)
            {
                ReefGameManager.Instance.collectedPearls -= 1;
                pearlsOfferedTotal += 1;
                craftingTier = 1;

                // Apply Speed Blessing
                if (diver != null)
                {
                    diver.forwardSwimForce *= 1.35f;
                    diver.walkForce *= 1.35f;
                    diver.maxSwimSpeed *= 1.35f;
                }

                NotifyUpgrade("METKAYINA TAIL SURGE UNLOCKED! (+35% Swim & Walk Speed)");
            }
            else if (craftingTier == 1 && ReefGameManager.Instance.collectedPearls >= 2)
            {
                ReefGameManager.Instance.collectedPearls -= 2;
                pearlsOfferedTotal += 2;
                craftingTier = 2;

                // Apply Oxygen Blessing
                if (HeartRateSystem.Instance != null)
                {
                    HeartRateSystem.Instance.maxOxygen = 150f;
                    HeartRateSystem.Instance.currentOxygen = 150f;
                }

                NotifyUpgrade("BREATH OF EYWA UNLOCKED! (Maximum Oxygen Extended to 150%)");
            }
            else if (craftingTier == 2 && ReefGameManager.Instance.collectedPearls >= 2)
            {
                ReefGameManager.Instance.collectedPearls -= 2;
                pearlsOfferedTotal += 2;
                craftingTier = 3;

                // Apply Radiant Aura Blessing
                basinGlowColor = new Color(1f, 0.85f, 0.2f); // Golden radiance
                if (altarLight != null) altarLight.color = basinGlowColor;

                NotifyUpgrade("PANDORAN RADIANT AURA UNLOCKED! (Full Oceanic Mastery Achieved)");
            }

            UpdatePrompt();
        }

        private void NotifyUpgrade(string msg)
        {
            if (UnderwaterAudio.Instance != null)
            {
                UnderwaterAudio.Instance.PlayBeaconChime();
            }

            if (UnderwaterHUD.Instance != null)
            {
                UnderwaterHUD.Instance.DisplayScientificLog("SACRED BLESSING ATTAINED", msg);
            }

            ResearchTelemetryExporter.Instance?.RecordDataPoint($"AltarUpgrade_Tier{craftingTier}");
            Debug.Log($"<color=#ffd700>[Metkayina Altar]</color> {msg}");
        }
    }
}

