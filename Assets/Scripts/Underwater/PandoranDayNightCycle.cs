using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnderwaterReef
{
    /// <summary>
    /// Pandoran Day / Poly-Eclipse Atmospheric Cycle.
    /// Cycles between crystalline tropical daylight and deep alien bioluminescent Poly-Eclipse:
    /// - Day: Radiant azure sunlight with caustics and volumetric beams
    /// - Poly-Eclipse: Sunlight vanishes into velvet darkness; bioluminescent corals and flora ignite into glowing neon!
    /// - Press 'L' to toggle between Day and Eclipse anytime!
    /// </summary>
    public class PandoranDayNightCycle : MonoBehaviour
    {
        public static PandoranDayNightCycle Instance { get; private set; }

        public bool isEclipse = false;
        public float cycleDurationMinutes = 6.0f;

        private Light celestialLight;
        private float transitionProgress = 0f;

        private Color dayFog = new Color(0.03f, 0.22f, 0.32f);
        private Color eclipseFog = new Color(0.003f, 0.008f, 0.018f);

        private Color dayAmbient = new Color(0.06f, 0.25f, 0.35f);
        private Color eclipseAmbient = new Color(0.01f, 0.03f, 0.07f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            celestialLight = FindAnyObjectByType<Light>();
        }

        private void Update()
        {
            if (celestialLight == null) celestialLight = FindAnyObjectByType<Light>();

            HandleInput();

            // Smooth transition
            float targetT = isEclipse ? 1.0f : 0.0f;
            transitionProgress = Mathf.MoveTowards(transitionProgress, targetT, Time.deltaTime * 0.85f);

            ApplyAtmosphericLighting();
        }

        private void HandleInput()
        {
            bool togglePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetKeyDown(KeyCode.L)) togglePressed = true;
            }
            catch { }
#endif

            if (togglePressed)
            {
                isEclipse = !isEclipse;
                string stateText = isEclipse ? "POLY-ECLIPSE FALLEN (Bioluminescence Activated)" : "PANDORAN DAYLIGHT RESTORED";
                Debug.Log($"<color=#00e5ff>[Pandoran Atmosphere]</color> {stateText}");

                if (UnderwaterHUD.Instance != null)
                {
                    UnderwaterHUD.Instance.DisplayScientificLog("CELESTIAL TRANSITION", stateText);
                }
            }
        }

        private void ApplyAtmosphericLighting()
        {
            if (celestialLight != null)
            {
                celestialLight.intensity = Mathf.Lerp(1.35f, 0.18f, transitionProgress);
                celestialLight.color = Color.Lerp(new Color(0.25f, 0.85f, 1f), new Color(0.6f, 0.2f, 0.9f), transitionProgress);
            }

            RenderSettings.fogColor = Color.Lerp(dayFog, eclipseFog, transitionProgress);
            RenderSettings.ambientLight = Color.Lerp(dayAmbient, eclipseAmbient, transitionProgress);
        }
    }
}

