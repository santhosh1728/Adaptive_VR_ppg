using System;
using UnityEngine;

namespace UnderwaterReef
{
    public enum InteractableType
    {
        Pearl,
        Beacon,
        SurfaceGate
    }

    /// <summary>
    /// Base class for all underwater interactable reef elements
    /// </summary>
    public abstract class ReefInteractable : MonoBehaviour
    {
        public InteractableType interactableType;
        public string interactionPrompt = "Press E to Interact";
        public bool canInteract = true;

        public abstract void Interact(DiverController diver);
    }

    /// <summary>
    /// Glowing Pearl: Collectible scattered through the reef
    /// </summary>
    public class GlowingPearl : ReefInteractable
    {
        [Header("Pearl Settings")]
        public float bobbingSpeed = 2f;
        public float bobbingHeight = 0.25f;
        public Color glowColor = new Color(0.3f, 0.95f, 1f);

        private Vector3 startPos;
        private Light pointLight;
        private Renderer pearlRenderer;

        private void Awake()
        {
            interactableType = InteractableType.Pearl;
            interactionPrompt = "[E] Collect Glowing Pearl";
            startPos = transform.position;

            // Setup glowing material
            pearlRenderer = GetComponent<Renderer>();
            if (pearlRenderer == null)
            {
                pearlRenderer = gameObject.AddComponent<MeshRenderer>();
                var filter = gameObject.AddComponent<MeshFilter>();
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                filter.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(temp);
            }

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material mat = new Material(uLit);
            mat.color = glowColor;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", glowColor * 2.5f);
            }
            pearlRenderer.sharedMaterial = mat;

            // Point Light
            pointLight = gameObject.AddComponent<Light>();
            pointLight.color = glowColor;
            pointLight.intensity = 2f;
            pointLight.range = 5f;

            // Trigger Sphere
            SphereCollider sc = GetComponent<SphereCollider>();
            if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 1.2f;
        }

        private void Update()
        {
            // Gentle floating bob
            float newY = startPos.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            transform.Rotate(Vector3.up * 45f * Time.deltaTime, Space.World);
        }

        public override void Interact(DiverController diver)
        {
            if (!canInteract) return;
            canInteract = false;

            if (UnderwaterAudio.Instance != null)
            {
                UnderwaterAudio.Instance.PlayPearlChime();
            }

            if (ReefGameManager.Instance != null)
            {
                ReefGameManager.Instance.OnPearlCollected(this);
            }

            // Visual collection pop
            Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            DiverController diver = other.GetComponentInParent<DiverController>();
            if (diver != null && canInteract)
            {
                Interact(diver);
            }
        }
    }

    /// <summary>
    /// Reef Beacon: Ancient pillar that revives surrounding bleached coral when activated
    /// </summary>
    public class ReefBeacon : ReefInteractable
    {
        [Header("Beacon State")]
        public bool isActivated = false;
        public Color dormantColor = new Color(0.4f, 0.45f, 0.5f);
        public Color activeColor = new Color(0.2f, 1f, 0.6f);

        private Renderer crystalRenderer;
        private Light beaconLight;
        private Transform crystalTransform;

        private void Awake()
        {
            interactableType = InteractableType.Beacon;
            interactionPrompt = "[E] Activate Reef Beacon";

            // Find or setup crystal mesh
            Transform crystal = transform.Find("Crystal");
            if (crystal != null)
            {
                crystalTransform = crystal;
                crystalRenderer = crystal.GetComponent<Renderer>();
            }

            beaconLight = GetComponentInChildren<Light>();
            if (beaconLight == null)
            {
                beaconLight = gameObject.AddComponent<Light>();
                beaconLight.range = 14f;
                beaconLight.intensity = 1.2f;
            }

            UpdateVisuals();
        }

        public void SetupComponents(Renderer ren, Transform trans, Light lgt)
        {
            crystalRenderer = ren;
            crystalTransform = trans;
            beaconLight = lgt;
            UpdateVisuals();
        }

        private void Update()
        {
            if (crystalTransform != null)
            {
                float rotSpeed = isActivated ? 60f : 15f;
                crystalTransform.Rotate(Vector3.up * rotSpeed * Time.deltaTime, Space.World);
            }

            if (isActivated && beaconLight != null)
            {
                // Pulsing radiant light
                beaconLight.intensity = 2.5f + Mathf.Sin(Time.time * 3f) * 0.8f;
            }
        }

        public override void Interact(DiverController diver)
        {
            if (isActivated) return;
            isActivated = true;
            canInteract = false;
            interactionPrompt = "Beacon Active";

            UpdateVisuals();

            if (UnderwaterAudio.Instance != null)
            {
                UnderwaterAudio.Instance.PlayBeaconChime();
            }

            // Trigger Reef Restoration wave
            if (ReefGameManager.Instance != null)
            {
                ReefGameManager.Instance.OnBeaconActivated(this);
            }
        }

        private void UpdateVisuals()
        {
            Color targetCol = isActivated ? activeColor : dormantColor;

            if (crystalRenderer != null)
            {
                Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
                if (uLit == null) uLit = Shader.Find("Standard");

                Material mat = new Material(uLit);
                mat.color = targetCol;
                if (isActivated)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", activeColor * 3f);
                }
                crystalRenderer.material = mat;
            }

            if (beaconLight != null)
            {
                beaconLight.color = targetCol;
                beaconLight.intensity = isActivated ? 3f : 0.8f;
            }
        }
    }

    /// <summary>
    /// Surface Gate: The dive extraction exit at the water surface (Y = 0)
    /// </summary>
    public class SurfaceGate : ReefInteractable
    {
        [Header("Gate State")]
        public bool isUnlocked = false;
        public Color lockedColor = new Color(0.9f, 0.2f, 0.2f);
        public Color openColor = new Color(0.1f, 0.95f, 0.85f);

        private Renderer ringRenderer;
        private Light gateLight;

        private void Awake()
        {
            interactableType = InteractableType.SurfaceGate;
            interactionPrompt = "Surface Gate (Requires Pearls & Beacons)";
            canInteract = false;

            ringRenderer = GetComponent<Renderer>();
            gateLight = GetComponentInChildren<Light>();
        }

        public void SetupComponents(Renderer ren, Light lgt)
        {
            ringRenderer = ren;
            gateLight = lgt;
            UpdateGateVisuals();
        }

        public void UnlockGate()
        {
            isUnlocked = true;
            canInteract = true;
            interactionPrompt = "Swim through to complete dive!";
            UpdateGateVisuals();
        }

        private void Update()
        {
            transform.Rotate(Vector3.up * (isUnlocked ? 40f : 10f) * Time.deltaTime, Space.World);
        }

        private void UpdateGateVisuals()
        {
            Color col = isUnlocked ? openColor : lockedColor;

            if (ringRenderer != null)
            {
                Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
                if (uLit == null) uLit = Shader.Find("Standard");

                Material mat = new Material(uLit);
                mat.color = col;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", col * (isUnlocked ? 3.5f : 1.2f));
                ringRenderer.material = mat;
            }

            if (gateLight != null)
            {
                gateLight.color = col;
                gateLight.intensity = isUnlocked ? 4f : 1f;
            }
        }

        public override void Interact(DiverController diver)
        {
            if (!isUnlocked) return;
            if (ReefGameManager.Instance != null)
            {
                ReefGameManager.Instance.OnDiveCompleted();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            DiverController diver = other.GetComponentInParent<DiverController>();
            if (diver != null && isUnlocked)
            {
                Interact(diver);
            }
        }
    }
}

