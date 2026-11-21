using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnderwaterReef
{
    [System.Serializable]
    public struct SonarPingTarget
    {
        public Vector3 worldPosition;
        public string label;
        public Color pingColor;
        public float remainingLifetime;
    }

    /// <summary>
    /// Metkayina "Way of Water" Echolocation Bio-Sonar System.
    /// Tap 'Tab' or 'V' to emit an expansive bioluminescent acoustic wave.
    /// - Highlights all distant pearls, Spirit Trees, and sunken shrines through the dark water.
    /// - Recharges faster when the diver is in Calm / Flow physiological state.
    /// </summary>
    public class MetkayinaSonarSystem : MonoBehaviour
    {
        public static MetkayinaSonarSystem Instance { get; private set; }

        [Header("Sonar Tuning")]
        public float sonarCooldown = 5.0f;
        public float sonarRange = 80.0f;
        public Color sonarWaveColor = new Color(0.2f, 1f, 0.9f, 0.75f);

        public float currentCooldownTimer { get; private set; } = 0f;
        public bool isReady => currentCooldownTimer <= 0f;

        public List<SonarPingTarget> activePings = new List<SonarPingTarget>();

        private ParticleSystem sonarRingFX;
        private DiverController diver;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreateSonarParticleFX();
        }

        private void Start()
        {
            diver = FindAnyObjectByType<DiverController>();
        }

        private void CreateSonarParticleFX()
        {
            GameObject fxGO = new GameObject("SonarWaveRingEmitter");
            fxGO.transform.SetParent(transform, false);

            sonarRingFX = fxGO.AddComponent<ParticleSystem>();
            var main = sonarRingFX.main;
            main.startLifetime = 2.4f;
            main.startSpeed = 22f;
            main.startSize = 0.35f;
            main.maxParticles = 180;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.startColor = sonarWaveColor;

            var emission = sonarRingFX.emission;
            emission.rateOverTime = 0; // Triggered via burst

            var shape = sonarRingFX.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;

            UnderwaterParticleHelper.ApplyParticleMaterial(sonarRingFX, sonarWaveColor, true);
        }

        private void Update()
        {
            if (diver == null) diver = FindAnyObjectByType<DiverController>();

            // Cooldown countdown
            if (currentCooldownTimer > 0f)
            {
                float rate = (HeartRateSystem.Instance != null && HeartRateSystem.Instance.isCalm) ? 1.6f : 1.0f;
                currentCooldownTimer -= Time.deltaTime * rate;
            }

            HandleInput();
            UpdateActivePings();
        }

        private void HandleInput()
        {
            bool triggerSonar = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.vKey.wasPressedThisFrame)
                {
                    triggerSonar = true;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.V))
                {
                    triggerSonar = true;
                }
            }
            catch { }
#endif
            if (QuestVRRigController.Instance != null && QuestVRRigController.Instance.isVRActive && QuestVRRigController.Instance.leftPrimaryJustPressed)
            {
                triggerSonar = true;
            }

            if (triggerSonar && isReady)
            {
                TriggerSonarPulse();
            }
        }

        public void TriggerSonarPulse()
        {
            currentCooldownTimer = sonarCooldown;
            activePings.Clear();

            Vector3 origin = diver != null ? diver.transform.position : transform.position;

            // Emit particle shockwave
            if (sonarRingFX != null)
            {
                sonarRingFX.transform.position = origin;
                sonarRingFX.Emit(120);
            }

            // Audio resonance
            if (UnderwaterAudio.Instance != null)
            {
                UnderwaterAudio.Instance.PlayHarmonicTone(528f, 0.75f); // 528 Hz transformation frequency tone
            }

            // 1. Ping Pearls
            var pearls = FindObjectsByType<GlowingPearl>();
            foreach (var p in pearls)
            {
                if (p != null && p.canInteract && Vector3.Distance(origin, p.transform.position) <= sonarRange)
                {
                    activePings.Add(new SonarPingTarget
                    {
                        worldPosition = p.transform.position,
                        label = "GLOWING PEARL",
                        pingColor = new Color(0.2f, 1f, 0.9f),
                        remainingLifetime = 6.5f
                    });
                }
            }

            // 2. Ping Spirit Trees
            var trees = FindObjectsByType<SpiritTreeBeacon>();
            foreach (var t in trees)
            {
                if (t != null && Vector3.Distance(origin, t.transform.position) <= sonarRange)
                {
                    activePings.Add(new SonarPingTarget
                    {
                        worldPosition = t.transform.position + Vector3.up * 4f,
                        label = "SPIRIT TREE (TSAHEYLU)",
                        pingColor = new Color(0.9f, 0.3f, 1f),
                        remainingLifetime = 7.5f
                    });
                }
            }

            // 3. Ping Altar
            var altar = FindAnyObjectByType<MetkayinaAltar>();
            if (altar != null && Vector3.Distance(origin, altar.transform.position) <= sonarRange)
            {
                activePings.Add(new SonarPingTarget
                {
                    worldPosition = altar.transform.position + Vector3.up * 1.5f,
                    label = "METKAYINA ALTAR",
                    pingColor = new Color(1f, 0.8f, 0.2f),
                    remainingLifetime = 7.5f
                });
            }

            // 4. Ping Giant Clams
            var clams = FindObjectsByType<SeabedGiantClam>();
            foreach (var c in clams)
            {
                if (c != null && c.hasPearl && Vector3.Distance(origin, c.transform.position) <= sonarRange)
                {
                    activePings.Add(new SonarPingTarget
                    {
                        worldPosition = c.transform.position + Vector3.up * 0.5f,
                        label = "GIANT TRIDACNA CLAM",
                        pingColor = new Color(0.1f, 0.95f, 0.9f),
                        remainingLifetime = 7.0f
                    });
                }
            }

            // 5. Ping Tulkun Spirit Whale
            var tulkun = FindAnyObjectByType<TulkunCompanionAI>();
            if (tulkun != null && Vector3.Distance(origin, tulkun.transform.position) <= sonarRange * 1.5f)
            {
                activePings.Add(new SonarPingTarget
                {
                    worldPosition = tulkun.transform.position + Vector3.up * 2f,
                    label = "TULKUN SPIRIT WHALE",
                    pingColor = new Color(1f, 0.85f, 0.2f),
                    remainingLifetime = 9.0f
                });
            }

            // 6. Ping Skimwing Mount
            var skimwing = FindAnyObjectByType<SkimwingMountAI>();
            if (skimwing != null && Vector3.Distance(origin, skimwing.transform.position) <= sonarRange)
            {
                activePings.Add(new SonarPingTarget
                {
                    worldPosition = skimwing.transform.position,
                    label = "SKIMWING (TSURAK)",
                    pingColor = new Color(1f, 0.5f, 0.1f),
                    remainingLifetime = 8.0f
                });
            }

            // 7. Ping Surface Gate
            var gate = FindAnyObjectByType<SurfaceGate>();
            if (gate != null)
            {
                activePings.Add(new SonarPingTarget
                {
                    worldPosition = gate.transform.position,
                    label = "SURFACE ESCAPE GATE",
                    pingColor = new Color(0.3f, 0.8f, 1f),
                    remainingLifetime = 6.5f
                });
            }

            Debug.Log($"<color=#00e5ff>[Way of Water Sonar]</color> Pulse emitted! Located {activePings.Count} oceanic objectives.");
        }

        private void UpdateActivePings()
        {
            for (int i = activePings.Count - 1; i >= 0; i--)
            {
                var ping = activePings[i];
                ping.remainingLifetime -= Time.deltaTime;
                if (ping.remainingLifetime <= 0f)
                {
                    activePings.RemoveAt(i);
                }
                else
                {
                    activePings[i] = ping;
                }
            }
        }
    }
}

