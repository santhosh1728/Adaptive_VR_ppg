using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Master Underwater Visual Effects & Atmospheric Particles:
    /// - Shimmering dynamic caustics and volumetric sunbeam light shifts
    /// - Fin & tail hydrodynamic micro-bubble trails while swimming
    /// - Volcanic sand silt puffs kicked up beneath the Avatar's feet while walking on the seabed
    /// - Surface breach & plunge splash droplet bursts when crossing the water line (Y = 0)
    /// - Cardiac stress vignette that pulses with physiological heartbeats
    /// </summary>
    public class UnderwaterVFXController : MonoBehaviour
    {
        public static UnderwaterVFXController Instance { get; private set; }

        private Texture2D vignetteTex;
        private Light directionalSun;
        private float causticsTimer;

        // Particle Emitters
        private ParticleSystem bubbleTrailFX;
        private ParticleSystem sandPuffFX;
        private ParticleSystem surfaceSplashFX;
        private ParticleSystem marineSnowFX;

        private DiverController diver;
        private float lastDiverY = -5f;
        private float footstepTimer = 0f;
        private float lensDropletTimer = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            GenerateVignetteTexture();
            CreateParticleSystems();
        }

        private void Start()
        {
            directionalSun = FindAnyObjectByType<Light>();
            diver = FindAnyObjectByType<DiverController>();
            if (diver != null) lastDiverY = diver.transform.position.y;
        }

        private void CreateParticleSystems()
        {
            // 1. Hydrodynamic Bubble Trail (Swim Wake)
            GameObject bubbleGO = new GameObject("VFX_BubbleTrailEmitter");
            bubbleGO.transform.SetParent(transform, false);

            bubbleTrailFX = bubbleGO.AddComponent<ParticleSystem>();
            var bMain = bubbleTrailFX.main;
            bMain.startLifetime = 1.8f;
            bMain.startSpeed = 0.4f;
            bMain.startSize = 0.08f;
            bMain.maxParticles = 120;
            bMain.gravityModifier = -0.15f; // Gently rise
            bMain.simulationSpace = ParticleSystemSimulationSpace.World;
            bMain.startColor = new Color(0.6f, 0.95f, 1f, 0.45f);

            var bEm = bubbleTrailFX.emission;
            bEm.rateOverTime = 0f; // Controlled dynamically
            UnderwaterParticleHelper.ApplyParticleMaterial(bubbleTrailFX, new Color(0.6f, 0.95f, 1f, 0.7f), true);

            // 2. Seabed Footstep Sand Puff
            GameObject sandGO = new GameObject("VFX_SeabedSandPuffEmitter");
            sandGO.transform.SetParent(transform, false);

            sandPuffFX = sandGO.AddComponent<ParticleSystem>();
            var sMain = sandPuffFX.main;
            sMain.startLifetime = 1.5f;
            sMain.startSpeed = 0.6f;
            sMain.startSize = 0.35f;
            sMain.maxParticles = 80;
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;
            sMain.startColor = new Color(0.12f, 0.16f, 0.22f, 0.4f); // Volcanic dark sand silt

            var sEm = sandPuffFX.emission;
            sEm.rateOverTime = 0f;
            UnderwaterParticleHelper.ApplyParticleMaterial(sandPuffFX, new Color(0.2f, 0.25f, 0.3f, 0.5f), false);

            // 3. Surface Breach & Plunge Splash
            GameObject splashGO = new GameObject("VFX_SurfaceSplashEmitter");
            splashGO.transform.SetParent(transform, false);

            surfaceSplashFX = splashGO.AddComponent<ParticleSystem>();
            var spMain = surfaceSplashFX.main;
            spMain.startLifetime = 1.2f;
            spMain.startSpeed = 3.5f;
            spMain.startSize = 0.18f;
            spMain.maxParticles = 60;
            spMain.gravityModifier = 1.0f; // Falls back down
            spMain.simulationSpace = ParticleSystemSimulationSpace.World;
            spMain.startColor = new Color(0.85f, 0.95f, 1f, 0.85f);

            var spEm = surfaceSplashFX.emission;
            spEm.rateOverTime = 0f;
            UnderwaterParticleHelper.ApplyParticleMaterial(surfaceSplashFX, new Color(0.85f, 0.98f, 1f, 0.85f), true);

            // 4. Suspended Planktonic Marine Snow & Bioluminescent Dinoflagellates
            GameObject snowGO = new GameObject("VFX_MarineSnowPlanktonEmitter");
            snowGO.transform.SetParent(transform, false);
            snowGO.transform.position = new Vector3(0f, -12f, 0f);

            marineSnowFX = snowGO.AddComponent<ParticleSystem>();
            var msMain = marineSnowFX.main;
            msMain.startLifetime = 10f;
            msMain.startSpeed = 0.18f;
            msMain.startSize = 0.06f;
            msMain.maxParticles = 350;
            msMain.simulationSpace = ParticleSystemSimulationSpace.World;
            msMain.startColor = new Color(0.45f, 0.95f, 1f, 0.55f);

            var msEm = marineSnowFX.emission;
            msEm.rateOverTime = 30f;

            var msShape = marineSnowFX.shape;
            msShape.shapeType = ParticleSystemShapeType.Box;
            msShape.scale = new Vector3(80f, 24f, 80f);

            UnderwaterParticleHelper.ApplyParticleMaterial(marineSnowFX, new Color(0.4f, 0.95f, 1f, 0.65f), true);
        }

        private void Update()
        {
            if (diver == null) diver = FindAnyObjectByType<DiverController>();

            UpdateCaustics();
            UpdateDiverVFX();
            UpdateChromaticLightAbsorption();
        }

        private void UpdateChromaticLightAbsorption()
        {
            float depth = diver != null ? diver.currentDepth : 12f;
            // Depth 0m to 58m (Multi-tiered biomes)
            float depthFactor = Mathf.Clamp01(depth / 55f);

            // Beer-Lambert Optical Attenuation:
            // Shallows (0m to -6m): Vibrant tropical azure/cyan
            // Mid-depth (-6m to -16m): Deep cobalt blue
            // Deep Abyss (-16m to -28m): Midnight-indigo with amplified bioluminescent contrast
            Color shallowFog = new Color(0.04f, 0.28f, 0.38f);
            Color midFog = new Color(0.02f, 0.12f, 0.22f);
            Color deepFog = new Color(0.008f, 0.035f, 0.075f);

            Color targetFog = depthFactor < 0.4f ?
                Color.Lerp(shallowFog, midFog, depthFactor / 0.4f) :
                Color.Lerp(midFog, deepFog, (depthFactor - 0.4f) / 0.6f);

            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFog, Time.deltaTime * 2.5f);
            RenderSettings.fogDensity = Mathf.Lerp(0.022f, 0.038f, depthFactor);

            // Ambient light dims with depth
            Color shallowAmbient = new Color(0.08f, 0.28f, 0.38f);
            Color deepAmbient = new Color(0.02f, 0.07f, 0.14f);
            RenderSettings.ambientLight = Color.Lerp(shallowAmbient, deepAmbient, depthFactor);

            // Sunlight intensity attenuates with water depth
            if (directionalSun != null)
            {
                float baseSun = Mathf.Lerp(1.4f, 0.75f, depthFactor);
                directionalSun.color = Color.Lerp(new Color(0.3f, 0.85f, 1f), new Color(0.12f, 0.55f, 0.85f), depthFactor);
                directionalSun.intensity = baseSun + Mathf.Sin(causticsTimer) * 0.12f;
            }
        }

        private void UpdateCaustics()
        {
            if (directionalSun != null)
            {
                causticsTimer += Time.deltaTime * 1.6f;
                float shimmer = Mathf.Sin(causticsTimer) * 0.18f + Mathf.Cos(causticsTimer * 2.3f) * 0.09f;
                directionalSun.intensity = 1.2f + shimmer;

                // Subtle light beam wobble
                float wobbleX = 60f + Mathf.Sin(causticsTimer * 0.5f) * 1.5f;
                float wobbleZ = -25f + Mathf.Cos(causticsTimer * 0.4f) * 1.5f;
                directionalSun.transform.rotation = Quaternion.Euler(wobbleX, wobbleZ, 0f);
            }
        }

        private void UpdateDiverVFX()
        {
            if (diver == null) return;

            Vector3 diverPos = diver.transform.position;
            Rigidbody rb = diver.GetComponent<Rigidbody>();
            float speed = rb != null ? rb.linearVelocity.magnitude : 0f;

            // 1. Swim Wake Bubble Trail
            if (!diver.isGrounded && speed > 1.2f)
            {
                if (bubbleTrailFX != null)
                {
                    bubbleTrailFX.transform.position = diverPos - diver.transform.forward * 0.8f + Vector3.down * 0.2f;
                    bubbleTrailFX.Emit(Mathf.RoundToInt(speed * 1.2f));
                }
            }

            // 2. Seabed Walking Footstep Silt Puffs & Resonant Impact Audio
            if (diver.isGrounded && speed > 0.4f)
            {
                footstepTimer += Time.deltaTime * speed * 3.5f;
                if (footstepTimer >= 1.0f)
                {
                    footstepTimer = 0f;
                    if (sandPuffFX != null)
                    {
                        sandPuffFX.transform.position = diverPos + Vector3.down * 1.0f;
                        sandPuffFX.Emit(6);
                    }
                    if (UnderwaterAudio.Instance != null)
                    {
                        UnderwaterAudio.Instance.PlayFootstepThud(false);
                    }
                }
            }

            // 3. Surface Breach / Plunge Splash (Crossing Y = 0)
            bool crossedSurface = (lastDiverY < -0.2f && diverPos.y >= -0.2f) || (lastDiverY > -0.2f && diverPos.y <= -0.2f);
            if (crossedSurface)
            {
                lensDropletTimer = 4.2f;

                if (surfaceSplashFX != null)
                {
                    surfaceSplashFX.transform.position = new Vector3(diverPos.x, 0.05f, diverPos.z);
                    surfaceSplashFX.Emit(35);
                }

                if (UnderwaterAudio.Instance != null)
                {
                    UnderwaterAudio.Instance.PlayHarmonicTone(260f, 0.25f);
                }
            }

            if (lensDropletTimer > 0f)
            {
                lensDropletTimer -= Time.deltaTime;
            }

            lastDiverY = diverPos.y;
        }

        private void GenerateVignetteTexture()
        {
            int size = 128;
            vignetteTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float alpha = Mathf.SmoothStep(0.2f, 1f, dist);
                    vignetteTex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }
            vignetteTex.Apply();
        }

        private void OnGUI()
        {
            if (QuestVRRigController.Instance != null && QuestVRRigController.Instance.isVRActive) return;
            if (ProcusVRManager.Instance != null && ProcusVRManager.Instance.isProcusVREnabled) return;

            // Lens Water Droplet FX when breaching or plunging through the surface
            if (lensDropletTimer > 0f)
            {
                DrawLensWaterDroplets();
            }

            HeartRateSystem hr = HeartRateSystem.Instance;
            if (hr == null) return;

            // Display stress vignette when heart rate is elevated or oxygen is low
            float stressWeight = Mathf.Clamp01((hr.currentBPM - 95f) / 45f);
            float lowO2Weight = hr.currentOxygen < 30f ? Mathf.InverseLerp(30f, 0f, hr.currentOxygen) : 0f;

            float intensity = Mathf.Max(stressWeight, lowO2Weight);

            if (intensity > 0.05f)
            {
                float pulse = 0.8f + Mathf.Sin(Time.time * (hr.currentBPM / 60f) * Mathf.PI * 2f) * 0.2f;
                Color vignetteColor = stressWeight > lowO2Weight ?
                    new Color(0.8f, 0.1f, 0.1f, intensity * pulse * 0.65f) :
                    new Color(0.1f, 0.2f, 0.3f, intensity * pulse * 0.85f);

                GUI.color = vignetteColor;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignetteTex);
                GUI.color = Color.white;
            }
        }

        private void DrawLensWaterDroplets()
        {
            float alpha = Mathf.Clamp01(lensDropletTimer / 3.0f) * 0.7f;
            Color dropCol = new Color(0.6f, 0.9f, 1f, alpha);
            GUI.color = dropCol;

            // Render procedural water beads on camera lens
            Vector2[] dropletPivots = new Vector2[]
            {
                new Vector2(0.15f, 0.2f), new Vector2(0.28f, 0.45f), new Vector2(0.42f, 0.15f),
                new Vector2(0.65f, 0.25f), new Vector2(0.78f, 0.55f), new Vector2(0.85f, 0.22f),
                new Vector2(0.2f, 0.75f), new Vector2(0.55f, 0.8f), new Vector2(0.82f, 0.82f)
            };

            for (int i = 0; i < dropletPivots.Length; i++)
            {
                float sx = dropletPivots[i].x * Screen.width;
                // Dripping down screen slowly
                float sy = (dropletPivots[i].y * Screen.height) + (4.2f - lensDropletTimer) * 22f;
                float r = 16f + (i % 3) * 8f;

                GUI.DrawTexture(new Rect(sx - r, sy - r, r * 2f, r * 2f), vignetteTex);
            }

            GUI.color = Color.white;
        }
    }
}
