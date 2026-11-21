using System;
using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Metkayina Ancestor Spirit Tree of Souls (Utraya Mokri of the Ocean).
    /// Features:
    /// - Sprawling banyan/mangrove stilt root base arching into volcanic seabed
    /// - Multi-branching canopy boughs
    /// - Over 28 cascading weeping willow luminous tendrils that ripple in ocean currents
    /// - Drifting bioluminescent Atokirina spores floating around canopy
    /// - Tsaheylu Neural Bond ('E'): Harmonizes cardiac rhythm with Eywa, lighting the tree in radiant cyan/magenta.
    /// </summary>
    public class SpiritTreeBeacon : ReefInteractable
    {
        [Header("Spirit Tree State")]
        public bool isBonded = false;
        public Color dormantColor = new Color(0.45f, 0.2f, 0.65f);    // Deep mystical violet
        public Color bondedColor = new Color(0.1f, 1f, 0.88f);        // Radiant electric cyan
        public Color tendrilColor = new Color(0.95f, 0.35f, 0.9f);    // Ethereal magenta

        private Transform treeRoot;
        private Transform treeCrown;
        private Light spiritLight;
        private Material trunkMat;
        private Material tendrilMat;
        private float wavePhase;

        public void InitializeSpiritTree(Vector3 position)
        {
            transform.position = position;
            interactableType = InteractableType.Beacon;
            interactionPrompt = "[E] Initiate Tsaheylu (Neural Bond with Spirit Tree)";

            BuildOrganicSpiritTreeModel();
        }

        private void BuildOrganicSpiritTreeModel()
        {
            // Clear existing model children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            treeRoot = new GameObject("OrganicTreeRoot").transform;
            treeRoot.SetParent(transform, false);

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            trunkMat = new Material(uLit);
            trunkMat.color = dormantColor;
            trunkMat.EnableKeyword("_EMISSION");
            trunkMat.SetColor("_EmissionColor", dormantColor * 1.6f);
            trunkMat.SetFloat("_Smoothness", 0.65f);

            tendrilMat = new Material(uLit);
            tendrilMat.color = tendrilColor;
            tendrilMat.EnableKeyword("_EMISSION");
            tendrilMat.SetColor("_EmissionColor", tendrilColor * 2.8f);

            // 1. Mangrove / Banyan Buttress Roots Sprawling into Seabed
            int rootCount = 7;
            for (int i = 0; i < rootCount; i++)
            {
                float angle = (i / (float)rootCount) * Mathf.PI * 2f;
                float spread = UnityEngine.Random.Range(2.2f, 3.2f);
                Vector3 basePos = new Vector3(Mathf.Cos(angle) * spread, 0f, Mathf.Sin(angle) * spread);

                GameObject rootPillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rootPillar.name = $"ButtressRoot_{i}";
                rootPillar.transform.SetParent(treeRoot, false);
                rootPillar.transform.position = transform.position + basePos * 0.5f + Vector3.up * 1.8f;

                // Angle root inward toward center trunk
                Vector3 dirToTrunk = (transform.position + Vector3.up * 3.6f) - (transform.position + basePos);
                rootPillar.transform.rotation = Quaternion.LookRotation(dirToTrunk) * Quaternion.Euler(90f, 0f, 0f);
                rootPillar.transform.localScale = new Vector3(0.45f, 2.2f, 0.45f);
                rootPillar.GetComponent<Renderer>().sharedMaterial = trunkMat;
                RemoveCollider(rootPillar);
            }

            // 2. Central Twisted Trunk
            GameObject coreTrunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coreTrunk.name = "CentralTrunk";
            coreTrunk.transform.SetParent(treeRoot, false);
            coreTrunk.transform.localPosition = new Vector3(0f, 3.8f, 0f);
            coreTrunk.transform.localScale = new Vector3(1.2f, 2.2f, 1.2f);
            coreTrunk.GetComponent<Renderer>().sharedMaterial = trunkMat;
            RemoveCollider(coreTrunk);

            // 3. Spreading Canopy Boughs
            treeCrown = new GameObject("TreeCrown").transform;
            treeCrown.SetParent(treeRoot, false);
            treeCrown.localPosition = new Vector3(0f, 6.2f, 0f);

            int boughCount = 5;
            for (int i = 0; i < boughCount; i++)
            {
                float angle = (i / (float)boughCount) * Mathf.PI * 2f;
                Vector3 boughDir = new Vector3(Mathf.Cos(angle), 0.35f, Mathf.Sin(angle)).normalized;

                GameObject bough = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bough.name = $"CanopyBough_{i}";
                bough.transform.SetParent(treeCrown, false);
                bough.transform.localPosition = boughDir * 1.8f;
                bough.transform.rotation = Quaternion.LookRotation(boughDir) * Quaternion.Euler(90f, 0f, 0f);
                bough.transform.localScale = new Vector3(0.35f, 2.0f, 0.35f);
                bough.GetComponent<Renderer>().sharedMaterial = trunkMat;
                RemoveCollider(bough);
            }

            // 4. Over 28 Weeping Willow Tendrils (With Organic Sway Animation)
            int tendrilCount = 28;
            for (int i = 0; i < tendrilCount; i++)
            {
                float angle = (i / (float)tendrilCount) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.1f, 0.1f);
                float radius = UnityEngine.Random.Range(1.8f, 3.8f);
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, UnityEngine.Random.Range(-0.2f, 0.5f), Mathf.Sin(angle) * radius);

                GameObject tendril = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tendril.name = $"WillowTendril_{i}";
                tendril.transform.SetParent(treeCrown, false);
                tendril.transform.localPosition = pos;
                tendril.transform.localScale = new Vector3(0.06f, UnityEngine.Random.Range(2.8f, 4.2f), 0.06f);
                tendril.transform.localRotation = Quaternion.Euler(UnityEngine.Random.Range(-8f, 8f), 0f, UnityEngine.Random.Range(-8f, 8f));
                tendril.GetComponent<Renderer>().sharedMaterial = tendrilMat;
                RemoveCollider(tendril);

                // Attach swaying script
                OrganicSwayFlora sway = tendril.AddComponent<OrganicSwayFlora>();
                sway.swayFrequency = UnityEngine.Random.Range(0.8f, 1.4f);
                sway.swayAngle = UnityEngine.Random.Range(6f, 14f);

                // Bioluminescent seed blossom on tendril tip
                GameObject blossom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blossom.name = "TipBlossom";
                blossom.transform.SetParent(tendril.transform, false);
                blossom.transform.localPosition = new Vector3(0f, -1.02f, 0f);
                blossom.transform.localScale = Vector3.one * 2.2f;
                blossom.GetComponent<Renderer>().sharedMaterial = tendrilMat;
                RemoveCollider(blossom);
            }

            // 5. Atokirina Drifting Spores (Tree of Souls Sacred Woodsrpites)
            GameObject sporesGO = new GameObject("AtokirinaSporeSwarm");
            sporesGO.transform.SetParent(treeCrown, false);
            sporesGO.transform.localPosition = Vector3.zero;

            ParticleSystem ps = sporesGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 8f;
            main.startSpeed = 0.25f;
            main.startSize = 0.15f;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.9f, 0.4f, 1f, 0.8f);

            var emission = ps.emission;
            emission.rateOverTime = 12f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 4.5f;

            UnderwaterParticleHelper.ApplyParticleMaterial(ps, new Color(0.9f, 0.4f, 1f, 0.8f), true);

            // 6. Central Sacred Sacred Light
            spiritLight = gameObject.AddComponent<Light>();
            spiritLight.type = LightType.Point;
            spiritLight.range = 25f;
            spiritLight.color = dormantColor;
            spiritLight.intensity = 3.2f;

            // 7. Interaction Trigger
            SphereCollider sc = GetComponent<SphereCollider>();
            if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 5.0f;
        }

        private void Update()
        {
            wavePhase += Time.deltaTime * (isBonded ? 2.8f : 1.1f);

            // Subtle organic breathing sway of tree canopy
            if (treeCrown != null)
            {
                float swayX = Mathf.Sin(wavePhase * 0.7f) * 3f;
                float swayZ = Mathf.Cos(wavePhase * 0.5f) * 3f;
                treeCrown.localRotation = Quaternion.Euler(swayX, wavePhase * 4f, swayZ);
            }

            if (spiritLight != null)
            {
                float pulse = (isBonded ? 3.8f : 2.2f) + Mathf.Sin(wavePhase * 2f) * 0.8f;
                spiritLight.intensity = pulse;
            }
        }

        public override void Interact(DiverController diver)
        {
            if (isBonded) return;
            isBonded = true;
            canInteract = false;
            interactionPrompt = "Tsaheylu Active (Neural Harmony Restored)";

            // Update materials to radiant cyan
            if (trunkMat != null)
            {
                trunkMat.color = bondedColor;
                trunkMat.SetColor("_EmissionColor", bondedColor * 3.2f);
            }
            if (tendrilMat != null)
            {
                Color radiantCyan = new Color(0.2f, 1f, 0.95f);
                tendrilMat.color = radiantCyan;
                tendrilMat.SetColor("_EmissionColor", radiantCyan * 4f);
            }
            if (spiritLight != null)
            {
                spiritLight.color = bondedColor;
                spiritLight.range = 35f;
            }

            if (UnderwaterAudio.Instance != null)
            {
                UnderwaterAudio.Instance.PlayBeaconChime();
            }

            if (ReefGameManager.Instance != null)
            {
                ReefGameManager.Instance.OnBeaconActivated(this);
            }

            Debug.Log("<color=#00f2fe>[Spirit Tree of Souls]</color> Tsaheylu initiated! Planetary neural link established.");
        }

        private void RemoveCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }
    }
}
