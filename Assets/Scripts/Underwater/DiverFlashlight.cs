using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnderwaterReef
{
    /// <summary>
    /// Tactical scuba diving flashlight attached to the diver's mask/shoulder.
    /// - Toggles with 'F' key.
    /// - Illuminates deep dark trenches, reveals hidden pearls, and casts volumetric ocean light.
    /// - Auto-powers on when descending below 10m depth.
    /// </summary>
    public class DiverFlashlight : MonoBehaviour
    {
        [Header("Light Settings")]
        public bool isOn = true;
        public float lightRange = 24f;
        public float spotAngle = 55f;
        public float lightIntensity = 2.8f;
        public Color beamColor = new Color(0.85f, 0.95f, 1f);

        private Light spotLight;
        private Transform diverHead;

        private void Start()
        {
            SetupFlashlight();
        }

        private void SetupFlashlight()
        {
            ProceduralDiver diverVisual = GetComponent<ProceduralDiver>();
            diverHead = diverVisual != null ? diverVisual.headTransform : transform;

            GameObject lightGO = new GameObject("DiverMaskFlashlight");
            lightGO.transform.SetParent(diverHead, false);
            lightGO.transform.localPosition = new Vector3(0.2f, 0.1f, 0.4f); // Shoulder/mask mount
            lightGO.transform.localRotation = Quaternion.Euler(5f, 0f, 0f); // Slight forward-down pitch

            spotLight = lightGO.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.range = lightRange;
            spotLight.spotAngle = spotAngle;
            spotLight.innerSpotAngle = spotAngle * 0.65f;
            spotLight.intensity = lightIntensity;
            spotLight.color = beamColor;
            spotLight.shadows = LightShadows.Soft;

            // Halo glow effect
            GameObject bulbGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulbGO.transform.SetParent(lightGO.transform, false);
            bulbGO.transform.localScale = Vector3.one * 0.08f;

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material bulbMat = new Material(uLit);
            bulbMat.color = beamColor;
            bulbMat.EnableKeyword("_EMISSION");
            bulbMat.SetColor("_EmissionColor", beamColor * 3f);
            bulbGO.GetComponent<Renderer>().sharedMaterial = bulbMat;

            Collider c = bulbGO.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }

        private void Update()
        {
            HandleInput();
            AutoDepthActivation();
        }

        private void HandleInput()
        {
            bool fPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                fPressed = Keyboard.current.fKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetKeyDown(KeyCode.F)) fPressed = true;
            }
            catch { }
#endif

            if (fPressed)
            {
                ToggleFlashlight();
            }
        }

        private void AutoDepthActivation()
        {
            DiverController diver = GetComponent<DiverController>();
            if (diver != null && diver.currentDepth > 10f && !isOn)
            {
                SetLightState(true);
            }
        }

        public void ToggleFlashlight()
        {
            SetLightState(!isOn);
        }

        public void SetLightState(bool state)
        {
            isOn = state;
            if (spotLight != null) spotLight.enabled = isOn;

            if (UnderwaterAudio.Instance != null)
            {
                UnderwaterAudio.Instance.PlayHarmonicTone(isOn ? 1200f : 800f, 0.08f); // Click sound
            }
        }
    }
}

