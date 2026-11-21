using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Interactive Pandoran Giant Tridacna Clam.
    /// Features:
    /// - Two articulating scalloped shells hinged along the posterior axis
    /// - Iridescent bioluminescent inner mantle
    /// - Protects a sacred giant glowing pearl
    /// - Defensively snaps shut with a silt puff when approached recklessly or in panic
    /// - Stays gracefully open when approached with calm cardiac coherence
    /// </summary>
    public class SeabedGiantClam : ReefInteractable
    {
        [Header("Clam State")]
        public bool isOpen = true;
        public bool hasPearl = true;
        public float openAngle = 42f;

        private Transform topShell;
        private Transform bottomShell;
        private Renderer mantleRenderer;
        private GameObject pearlGO;
        private float currentAngle = 42f;
        private DiverController diver;
        private HeartRateSystem hrSystem;

        private Color mantleGlowColor = new Color(0.1f, 0.95f, 0.9f);

        public void InitializeClam(Vector3 spawnPos, Quaternion rotation)
        {
            transform.position = spawnPos;
            transform.rotation = rotation;

            interactableType = InteractableType.Pearl;
            interactionPrompt = "[E] Harvest Sacred Giant Pearl";
            canInteract = true;

            BuildClamModel();
        }

        private void BuildClamModel()
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material shellMat = new Material(uLit);
            shellMat.color = new Color(0.12f, 0.16f, 0.22f); // Deep oceanic chitin
            shellMat.SetFloat("_Smoothness", 0.5f);

            Material mantleMat = new Material(uLit);
            mantleMat.color = mantleGlowColor;
            mantleMat.EnableKeyword("_EMISSION");
            mantleMat.SetColor("_EmissionColor", mantleGlowColor * 2.8f);

            // 1. Bottom Shell
            GameObject bShell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bShell.name = "BottomShell";
            bShell.transform.SetParent(transform, false);
            bShell.transform.localPosition = new Vector3(0f, 0.15f, 0.35f);
            bShell.transform.localScale = new Vector3(2.4f, 0.25f, 2.0f);
            bShell.GetComponent<Renderer>().sharedMaterial = shellMat;
            RemoveCollider(bShell);
            bottomShell = bShell.transform;

            // 2. Inner Bioluminescent Mantle
            GameObject mantle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mantle.name = "InnerMantle";
            mantle.transform.SetParent(bottomShell, false);
            mantle.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            mantle.transform.localScale = new Vector3(0.85f, 0.3f, 0.85f);
            mantleRenderer = mantle.GetComponent<Renderer>();
            mantleRenderer.sharedMaterial = mantleMat;
            RemoveCollider(mantle);

            // 3. Top Shell (Hinged at back)
            GameObject hinge = new GameObject("TopShellHinge");
            hinge.transform.SetParent(transform, false);
            hinge.transform.localPosition = new Vector3(0f, 0.25f, -0.6f);
            topShell = hinge.transform;

            GameObject tShell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tShell.name = "TopShellLid";
            tShell.transform.SetParent(hinge.transform, false);
            tShell.transform.localPosition = new Vector3(0f, 0.1f, 0.95f);
            tShell.transform.localScale = new Vector3(2.4f, 0.25f, 2.0f);
            tShell.GetComponent<Renderer>().sharedMaterial = shellMat;
            RemoveCollider(tShell);

            // 4. Inner Giant Pearl
            pearlGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pearlGO.name = "GiantPearl";
            pearlGO.transform.SetParent(transform, false);
            pearlGO.transform.localPosition = new Vector3(0f, 0.45f, 0.25f);
            pearlGO.transform.localScale = Vector3.one * 0.65f;

            Material pearlMat = new Material(uLit);
            pearlMat.color = new Color(0.95f, 0.95f, 1f);
            pearlMat.EnableKeyword("_EMISSION");
            pearlMat.SetColor("_EmissionColor", new Color(0.3f, 0.9f, 1f) * 3f);
            pearlGO.GetComponent<Renderer>().sharedMaterial = pearlMat;
            RemoveCollider(pearlGO);

            // 5. Interaction Sphere Trigger
            SphereCollider sc = GetComponent<SphereCollider>();
            if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 3.2f;
        }

        private void Update()
        {
            if (diver == null) diver = FindAnyObjectByType<DiverController>();
            if (hrSystem == null) hrSystem = HeartRateSystem.Instance;

            bool isNear = diver != null && Vector3.Distance(transform.position, diver.transform.position) < 4.2f;
            Rigidbody rb = diver != null ? diver.GetComponent<Rigidbody>() : null;
            float diverSpeed = rb != null ? rb.linearVelocity.magnitude : 0f;

            // Tsaheylu Bio-Bonding & Panic Response:
            // Stays open and radiates luminous mantle light when diver maintains cardiac coherence (Phi >= 50%)
            // Snaps shut defensively if diver panics, rushes recklessly, or exhibits sympathetic stress (Phi < 35%)
            HRVEngine hrv = HRVEngine.Instance;
            float bondIndex = hrv != null ? hrv.tsaheyluBondIndex : 70f;
            bool highCoherence = bondIndex >= 50f && (hrSystem == null || !hrSystem.isStressed);

            bool threatDetected = isNear && (!highCoherence || diverSpeed > 4.2f || (hrSystem != null && hrSystem.isStressed));

            if (threatDetected && isOpen)
            {
                // Snap shut defensively!
                isOpen = false;
                canInteract = false;

                if (UnderwaterAudio.Instance != null)
                {
                    UnderwaterAudio.Instance.PlayShockBuzz();
                }
            }
            else if (!threatDetected && !isOpen && highCoherence)
            {
                // Slowly reopen when safe and player achieves Tsaheylu autonomic coherence
                isOpen = true;
                canInteract = hasPearl;
            }

            // Mantle bioluminescence pulses with player's heart rate when bonded
            if (mantleRenderer != null && isOpen)
            {
                Color glow = hrv != null ? hrv.tsaheyluBondColor : mantleGlowColor;
                float pulse = 1.8f + Mathf.Sin(Time.time * 2.5f) * 0.8f;
                mantleRenderer.sharedMaterial.SetColor("_EmissionColor", glow * pulse);
            }

            // Animate shell hinge rotation
            float targetAngle = isOpen ? openAngle : 3f;
            float lerpSpeed = isOpen ? 2.5f : 12f; // Snaps shut rapidly, opens cautiously
            currentAngle = Mathf.Lerp(currentAngle, targetAngle, Time.deltaTime * lerpSpeed);

            if (topShell != null)
            {
                topShell.localRotation = Quaternion.Euler(-currentAngle, 0f, 0f);
            }
        }

        public override void Interact(DiverController diver)
        {
            if (!canInteract || !hasPearl || !isOpen) return;

            hasPearl = false;
            canInteract = false;
            interactionPrompt = "Giant Clam (Pearl Harvested)";

            if (pearlGO != null) Destroy(pearlGO);

            if (ReefGameManager.Instance != null)
            {
                ReefGameManager.Instance.OnPearlCollected(null);
            }

            if (UnderwaterAudio.Instance != null)
            {
                UnderwaterAudio.Instance.PlayPearlChime();
            }

            Debug.Log("<color=#00e5ff>[Giant Clam]</color> Harvested ancient giant pearl!");
        }

        private void RemoveCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }
    }
}

