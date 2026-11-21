using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnderwaterReef
{
    /// <summary>
    /// Realistic Technical Scuba Dive Computer & Biometric Helmet Visor HUD.
    /// Modeled after modern military / deep-sea closed-circuit rebreathers (Shearwater Petrel 3 / Garmin Descent Mk3)
    /// and Pandoran Metkayina bio-telemetry interfaces.
    /// 
    /// Features:
    /// - Diegetic curved helmet visor optical overlay with subtle glass condensation framing (F9 to toggle)
    /// - 360° Horizontal Navigational Compass Ribbon with live waypoint chevrons (Spirit Trees, Surface Gate, Pearls, Predators)
    /// - Deep-Sea Decompression Computer: Live Depth, Max Depth, Bühlmann No-Decompression Limit (NDL), and Ascent Rate Monitor (ASC RATE)
    /// - Oxygen partial pressure (PO2), tank pressure (BAR / PSI), and surface air consumption status
    /// - Cardiorespiratory Clinical Telemetry: Real-time BPM, baseline Z-score, LF/HF sympathovagal balance, RSA respiration, Russell's circumplex
    /// - Live Phosphor Oscilloscope Sweep with ECG/PPG cardiac complex
    /// - Smooth Holographic Circular Breathing Pacer (0.1 Hz resonance guide)
    /// - Situational audio-visual alerts (depth milestone chimes, rapid ascent warning alarm, critical O2 constriction)
    /// - 3D Sonar ping diamond reticles & distance rangefinders
    /// </summary>
    public class UnderwaterHUD : MonoBehaviour
    {
        public static UnderwaterHUD Instance { get; private set; }

        private DiverController diver;
        private HeartRateSystem hrSystem;
        private ReefGameManager gameManager;
        private MantaCompanionAI cachedManta;
        private ApexPredatorAI cachedPredator;
        private SpiritTreeBeacon[] cachedTrees;
        private GlowingPearl[] cachedPearls;

        [Header("Diegetic Visor & Optics")]
        public bool showVisorMaskFrame = true;     // F9 toggles optical helmet visor frame
        public bool showResearcherOverlay = true;   // F8 toggles verbose metrics in Condition C

        // Procedural Textures for AAA Graphics
        private Texture2D whiteTex;
        private Texture2D visorFrameTex;
        private Texture2D glassPanelTex;
        private Texture2D smoothRingTex;
        private Texture2D gradientBarTex;
        private Texture2D dotGridTex;

        // Typography Styles
        private GUIStyle titleStyle;
        private GUIStyle headerStyle;
        private GUIStyle subheaderStyle;
        private GUIStyle labelStyle;
        private GUIStyle labelDimStyle;
        private GUIStyle bigValueStyle;
        private GUIStyle hugeValueStyle;
        private GUIStyle pillStyle;
        private GUIStyle promptStyle;
        private GUIStyle compassLabelStyle;
        private GUIStyle victoryStyle;

        // Dynamics & Breathing Pacer State
        private float breathPhase = 0f;
        private float heartbeatVisualPulse = 1f;
        private float diveStartTime = 0f;
        private float lastDepth = 0f;
        private float ascentRateMPerMin = 0f;
        private float maxDepthLogged = 0f;
        private float lastCardinalBearingTick = 0f;
        private float rapidAscentAlarmCooldown = 0f;
        private float lowO2AlarmCooldown = 0f;

        // Depth Milestone Gates crossed
        private readonly HashSet<int> depthGatesCrossed = new HashSet<int>();

        // Scientific Log Popup
        private string activeLogTitle = "";
        private string activeLogBody = "";
        private float logDisplayTimer = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            diveStartTime = Time.time;
            GenerateProceduralTextures();
        }

        private void Start()
        {
            diver = FindAnyObjectByType<DiverController>();
            hrSystem = HeartRateSystem.Instance;
            gameManager = ReefGameManager.Instance;
            cachedManta = FindAnyObjectByType<MantaCompanionAI>();
            cachedPredator = FindAnyObjectByType<ApexPredatorAI>();
            cachedTrees = FindObjectsByType<SpiritTreeBeacon>(FindObjectsInactive.Exclude);
            cachedPearls = FindObjectsByType<GlowingPearl>(FindObjectsInactive.Exclude);

            if (hrSystem != null)
            {
                hrSystem.OnHeartbeat += (bpm) => heartbeatVisualPulse = 1.45f;
            }
        }

        private void Update()
        {
            // Resonance breathing pacer: 4s inhale, 1s hold, 4s exhale, 1s hold (~0.1 Hz)
            breathPhase += Time.deltaTime * 0.6283f; // 2 * PI / 10s
            heartbeatVisualPulse = Mathf.MoveTowards(heartbeatVisualPulse, 1f, Time.deltaTime * 3.5f);

            // Compute Ascent Rate (Meters / Minute) with exponential smoothing
            if (diver != null)
            {
                float curDepth = diver.currentDepth;
                if (curDepth > maxDepthLogged) maxDepthLogged = curDepth;

                float rawAscentSpeed = (lastDepth - curDepth) / Mathf.Max(Time.deltaTime, 0.001f); // m/s
                float targetRateMPerMin = rawAscentSpeed * 60f;
                ascentRateMPerMin = Mathf.Lerp(ascentRateMPerMin, targetRateMPerMin, Time.deltaTime * 4f);
                lastDepth = curDepth;

                // Check Depth Milestones for acoustic hydrophone ping
                int currentGate = Mathf.FloorToInt(curDepth / 10f) * 10;
                if (currentGate >= 10 && !depthGatesCrossed.Contains(currentGate))
                {
                    depthGatesCrossed.Add(currentGate);
                    if (UnderwaterAudio.Instance != null) UnderwaterAudio.Instance.PlayDepthMilestonePing();
                }

                // Rapid Ascent Alarm Check (> 9 m/min safe decompression limit)
                rapidAscentAlarmCooldown -= Time.deltaTime;
                if (curDepth > 4f && ascentRateMPerMin > 9.0f && rapidAscentAlarmCooldown <= 0f)
                {
                    rapidAscentAlarmCooldown = 1.2f;
                    if (UnderwaterAudio.Instance != null) UnderwaterAudio.Instance.PlayRapidAscentAlarm();
                }

                // Low Oxygen Alarm Check (< 20%)
                lowO2AlarmCooldown -= Time.deltaTime;
                if (hrSystem != null && hrSystem.currentOxygen < 20f && lowO2AlarmCooldown <= 0f)
                {
                    lowO2AlarmCooldown = 2.0f;
                    if (UnderwaterAudio.Instance != null) UnderwaterAudio.Instance.PlayLowOxygenPing();
                }
            }

            // Keyboard Shortcuts
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.f8Key.wasPressedThisFrame) showResearcherOverlay = !showResearcherOverlay;
                if (Keyboard.current.f9Key.wasPressedThisFrame) showVisorMaskFrame = !showVisorMaskFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F8)) showResearcherOverlay = !showResearcherOverlay;
            if (Input.GetKeyDown(KeyCode.F9)) showVisorMaskFrame = !showVisorMaskFrame;
#endif
        }

        public void DisplayScientificLog(string title, string body)
        {
            activeLogTitle = title;
            activeLogBody = body;
            logDisplayTimer = 8f;
        }

        #region Procedural Graphics Generation

        private void GenerateProceduralTextures()
        {
            // 1. Solid White 1x1
            whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();

            // 2. Diegetic Mask Visor Frame Texture (512x512)
            int vSize = 512;
            visorFrameTex = new Texture2D(vSize, vSize, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(vSize * 0.5f, vSize * 0.5f);
            float radius = vSize * 0.5f;

            for (int y = 0; y < vSize; y++)
            {
                for (int x = 0; x < vSize; x++)
                {
                    // Elliptical mask frame curvature
                    float nx = (x - center.x) / (radius * 0.96f);
                    float ny = (y - center.y) / (radius * 0.88f);
                    float d = Mathf.Sqrt(nx * nx + ny * ny);

                    if (d > 1f)
                    {
                        // Solid dark carbon-fiber bezel
                        float rimDist = d - 1f;
                        float alpha = Mathf.Clamp01(rimDist * 8f);
                        visorFrameTex.SetPixel(x, y, new Color(0.02f, 0.04f, 0.06f, alpha * 0.95f));
                    }
                    else if (d > 0.82f)
                    {
                        // Inner glass optical bevel & lens reflection
                        float bevelT = Mathf.InverseLerp(0.82f, 1f, d);
                        float glassAlpha = Mathf.SmoothStep(0f, 0.45f, bevelT);
                        // Subtle cyan anti-reflective lens sheen
                        Color glassColor = Color.Lerp(new Color(0f, 0.08f, 0.12f, 0f), new Color(0.05f, 0.25f, 0.35f, 0.4f), glassAlpha);
                        visorFrameTex.SetPixel(x, y, glassColor);
                    }
                    else
                    {
                        visorFrameTex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            visorFrameTex.Apply();

            // 3. Modern Titanium Glass Panel Texture (128x128)
            int pSize = 128;
            glassPanelTex = new Texture2D(pSize, pSize, TextureFormat.RGBA32, false);
            for (int y = 0; y < pSize; y++)
            {
                for (int x = 0; x < pSize; x++)
                {
                    bool isBorder = (x == 0 || x == pSize - 1 || y == 0 || y == pSize - 1);
                    bool isCornerBracket = ((x < 12 || x > pSize - 13) && (y < 4 || y > pSize - 5)) ||
                                           ((y < 12 || y > pSize - 13) && (x < 4 || x > pSize - 5));

                    if (isCornerBracket)
                    {
                        glassPanelTex.SetPixel(x, y, new Color(0.2f, 0.85f, 1f, 0.95f)); // Luminous cyan bracket
                    }
                    else if (isBorder)
                    {
                        glassPanelTex.SetPixel(x, y, new Color(0.12f, 0.38f, 0.52f, 0.65f)); // Subtle border
                    }
                    else
                    {
                        // Frosted dark ocean glass
                        float grad = 1f - (float)y / pSize * 0.25f;
                        glassPanelTex.SetPixel(x, y, new Color(0.03f, 0.07f, 0.11f, 0.82f * grad));
                    }
                }
            }
            glassPanelTex.Apply();

            // 4. Smooth Anti-Aliased Ring Texture (128x128)
            int rSize = 128;
            smoothRingTex = new Texture2D(rSize, rSize, TextureFormat.RGBA32, false);
            Vector2 rCenter = new Vector2(rSize * 0.5f, rSize * 0.5f);
            float rOuter = rSize * 0.46f;
            float rInner = rSize * 0.38f;

            for (int y = 0; y < rSize; y++)
            {
                for (int x = 0; x < rSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), rCenter);
                    if (dist >= rInner && dist <= rOuter)
                    {
                        float edgeDist = Mathf.Min(dist - rInner, rOuter - dist);
                        float alpha = Mathf.Clamp01(edgeDist * 0.8f);
                        smoothRingTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        smoothRingTex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            smoothRingTex.Apply();

            // 5. Horizontal Gradient LED Bar Texture (64x16)
            gradientBarTex = new Texture2D(64, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float t = (float)x / 63f;
                    // Segmented LED micro-grooves
                    float seg = (x % 4 == 3) ? 0.4f : 1f;
                    gradientBarTex.SetPixel(x, y, new Color(1f, 1f, 1f, seg * (0.85f + t * 0.15f)));
                }
            }
            gradientBarTex.Apply();

            // 6. Dot-Matrix Grid Texture (32x32)
            dotGridTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    bool isDot = (x % 8 == 0 && y % 8 == 0);
                    dotGridTex.SetPixel(x, y, isDot ? new Color(0.2f, 0.7f, 0.9f, 0.25f) : Color.clear);
                }
            }
            dotGridTex.Apply();
        }

        #endregion

        #region GUI Rendering Loop

        private void OnGUI()
        {
            // Stereo Meta Quest 3 VR & Procus Mobile VR use 3D World-Space Floating HUD instead of flat OnGUI
            if (QuestVRRigController.Instance != null && QuestVRRigController.Instance.isVRActive) return;
            if (ProcusVRManager.Instance != null && ProcusVRManager.Instance.isProcusVREnabled) return;

            InitStyles();

            // 1. Diegetic Helmet Visor Rim
            if (showVisorMaskFrame && visorFrameTex != null)
            {
                DrawVisorFrame();
            }

            // 2. Victory / Dive Complete Screen
            if (gameManager != null && gameManager.isDiveComplete)
            {
                DrawVictoryScreen();
                return;
            }

            // 3. Top Curved Navigational Compass Ribbon
            DrawNavigationalCompassRibbon();

            // 4. Left Instrument Cluster: Scuba Dive Computer & Biometrics
            DrawDiveComputerPanel();

            // 5. Right Instrument Cluster: Expedition Objectives & Biosphere Tracking
            DrawMissionObjectives();

            // 6. Resonance Breathing Hologram Pacer (Bottom-Left)
            DrawBreathingPacer();

            // 7. Tactical Interaction Reticle & Rangefinder
            DrawCrosshairAndPrompts();

            // 8. 3D Sonar Target Diamond Pings
            DrawSonarPings();

            // 9. Audio-Visual Warning Alerts
            DrawRapidAscentWarning();
            DrawOxygenWarning();
            DrawPredatorWarning();

            // 10. Scientific Discovery Popup
            DrawScientificLogPopup();

            // 11. Minimal Footer Controls Hint
            DrawMinimalControlsHint();
        }

        private void InitStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = new Color(0.35f, 0.95f, 1f);

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = new Color(0.6f, 0.85f, 1f);

            subheaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            subheaderStyle.normal.textColor = new Color(0.65f, 0.8f, 0.9f);

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            labelStyle.normal.textColor = Color.white;

            labelDimStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft
            };
            labelDimStyle.normal.textColor = new Color(0.55f, 0.7f, 0.82f);

            bigValueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            bigValueStyle.normal.textColor = Color.white;

            hugeValueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            hugeValueStyle.normal.textColor = Color.white;

            pillStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            pillStyle.normal.textColor = Color.white;

            promptStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            promptStyle.normal.textColor = new Color(0.2f, 1f, 0.85f);

            compassLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            compassLabelStyle.normal.textColor = new Color(0.4f, 0.9f, 1f);

            victoryStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            victoryStyle.normal.textColor = new Color(0.3f, 1f, 0.75f);
        }

        #endregion

        #region Diegetic Mask Visor Frame

        private void DrawVisorFrame()
        {
            // Subtle carbon-fiber dive helmet mask rim with edge glass optical tint
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), visorFrameTex, ScaleMode.StretchToFill);
            GUI.color = Color.white;
        }

        #endregion

        #region 360° Navigational Compass Ribbon

        private void DrawNavigationalCompassRibbon()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float heading = cam.transform.eulerAngles.y;
            float ribbonW = Mathf.Min(500f, Screen.width * 0.45f);
            float ribbonH = 38f;
            float ribbonX = (Screen.width - ribbonW) * 0.5f;
            float ribbonY = 16f;

            // Draw curved glass backdrop
            Rect ribbonRect = new Rect(ribbonX, ribbonY, ribbonW, ribbonH);
            DrawModernGlassPanel(ribbonRect, new Color(0.04f, 0.08f, 0.14f, 0.88f), new Color(0.25f, 0.65f, 0.9f, 0.7f));

            // Center target indicator [ 042° ]
            float centerTargetW = 60f;
            Rect centerBox = new Rect(ribbonX + (ribbonW - centerTargetW) * 0.5f, ribbonY + 2f, centerTargetW, 16f);
            DrawFilledBox(centerBox, new Color(0.08f, 0.22f, 0.35f, 0.95f), new Color(0.3f, 0.95f, 1f, 0.9f));
            GUI.Label(centerBox, $"{Mathf.RoundToInt(heading):D3}°", compassLabelStyle);

            // Tick graduation tape (FOV ~70 degrees across the ribbon)
            float degRange = 70f;
            float pxPerDeg = ribbonW / degRange;

            float minDeg = heading - degRange * 0.5f;
            float maxDeg = heading + degRange * 0.5f;

            int startTick = Mathf.FloorToInt(minDeg / 5f) * 5;
            int endTick = Mathf.CeilToInt(maxDeg / 5f) * 5;

            // Play acoustic tick on passing cardinal points (0, 90, 180, 270)
            int nearestCardinal = Mathf.RoundToInt(heading / 90f) * 90;
            if (Mathf.Abs(heading - nearestCardinal) < 1.5f && Mathf.Abs(lastCardinalBearingTick - nearestCardinal) > 5f)
            {
                lastCardinalBearingTick = nearestCardinal;
                if (UnderwaterAudio.Instance != null) UnderwaterAudio.Instance.PlayCompassTick();
            }

            for (int tick = startTick; tick <= endTick; tick += 5)
            {
                float xPos = ribbonX + (ribbonW * 0.5f) + (tick - heading) * pxPerDeg;
                if (xPos < ribbonX + 10 || xPos > ribbonX + ribbonW - 10) continue;

                int normDeg = (tick % 360 + 360) % 360;
                bool isMajor = (normDeg % 15 == 0);
                bool isCardinal = (normDeg % 45 == 0);

                float tickH = isCardinal ? 14f : (isMajor ? 9f : 5f);
                Color tickCol = isCardinal ? new Color(0.4f, 1f, 0.95f) : (isMajor ? new Color(0.6f, 0.85f, 1f, 0.7f) : new Color(0.3f, 0.5f, 0.7f, 0.45f));

                // Draw tick line
                DrawFilledBox(new Rect(xPos - 0.5f, ribbonY + ribbonH - tickH - 3, 1f, tickH), tickCol, Color.clear);

                // Cardinal & Major Numerals
                if (isCardinal)
                {
                    string cardName = GetCardinalName(normDeg);
                    GUIStyle cStyle = new GUIStyle(compassLabelStyle);
                    cStyle.normal.textColor = normDeg == 0 ? new Color(1f, 0.35f, 0.35f) : new Color(0.3f, 0.95f, 1f);
                    GUI.Label(new Rect(xPos - 15, ribbonY + 16, 30, 16), cardName, cStyle);
                }
                else if (isMajor)
                {
                    GUIStyle mStyle = new GUIStyle(compassLabelStyle) { fontSize = 8 };
                    mStyle.normal.textColor = new Color(0.6f, 0.75f, 0.85f, 0.75f);
                    GUI.Label(new Rect(xPos - 12, ribbonY + 18, 24, 14), $"{normDeg}", mStyle);
                }
            }

            // Waypoint Chevrons on Compass Tape
            DrawCompassWaypoint(cam, ribbonRect, heading, pxPerDeg);
        }

        private string GetCardinalName(int deg)
        {
            switch (deg)
            {
                case 0: return "N";
                case 45: return "NE";
                case 90: return "E";
                case 135: return "SE";
                case 180: return "S";
                case 225: return "SW";
                case 270: return "W";
                case 315: return "NW";
                default: return $"{deg}°";
            }
        }

        private void DrawCompassWaypoint(Camera cam, Rect ribbonRect, float heading, float pxPerDeg)
        {
            Vector3 camPos = cam.transform.position;

            // 1. Spirit Trees
            if (cachedTrees != null && cachedTrees.Length > 0)
            {
                for (int i = 0; i < cachedTrees.Length; i++)
                {
                    if (cachedTrees[i] == null) continue;
                    Vector3 toTarget = cachedTrees[i].transform.position - camPos;
                    float dist = toTarget.magnitude;
                    float targetBearing = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                    float deltaDeg = Mathf.DeltaAngle(heading, targetBearing);

                    if (Mathf.Abs(deltaDeg) < 35f)
                    {
                        float x = ribbonRect.x + (ribbonRect.width * 0.5f) + deltaDeg * pxPerDeg;
                        DrawCaretIcon(new Vector2(x, ribbonRect.y + 3), new Color(0.2f, 1f, 0.85f), $"TREE {dist:F0}m");
                    }
                }
            }

            // 2. Nearest Pearl
            if (cachedPearls != null && cachedPearls.Length > 0)
            {
                GlowingPearl nearestPearl = null;
                float nearestDist = float.MaxValue;
                for (int i = 0; i < cachedPearls.Length; i++)
                {
                    if (cachedPearls[i] == null || !cachedPearls[i].gameObject.activeInHierarchy) continue;
                    float d = Vector3.Distance(camPos, cachedPearls[i].transform.position);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearestPearl = cachedPearls[i];
                    }
                }

                if (nearestPearl != null && nearestDist < 80f)
                {
                    Vector3 toPearl = nearestPearl.transform.position - camPos;
                    float targetBearing = Mathf.Atan2(toPearl.x, toPearl.z) * Mathf.Rad2Deg;
                    float deltaDeg = Mathf.DeltaAngle(heading, targetBearing);
                    if (Mathf.Abs(deltaDeg) < 35f)
                    {
                        float x = ribbonRect.x + (ribbonRect.width * 0.5f) + deltaDeg * pxPerDeg;
                        DrawCaretIcon(new Vector2(x, ribbonRect.y + 3), new Color(1f, 0.85f, 0.2f), $"PEARL {nearestDist:F0}m");
                    }
                }
            }

            // 3. Apex Predator Danger Caret
            if (cachedPredator == null) cachedPredator = FindAnyObjectByType<ApexPredatorAI>();
            if (cachedPredator != null)
            {
                Vector3 toPred = cachedPredator.transform.position - camPos;
                float predDist = toPred.magnitude;
                if (predDist < 75f)
                {
                    float targetBearing = Mathf.Atan2(toPred.x, toPred.z) * Mathf.Rad2Deg;
                    float deltaDeg = Mathf.DeltaAngle(heading, targetBearing);
                    float flash = Mathf.PingPong(Time.time * 5f, 1f);
                    Color predCol = Color.Lerp(new Color(1f, 0.3f, 0.1f), Color.red, flash);

                    if (Mathf.Abs(deltaDeg) < 35f)
                    {
                        float x = ribbonRect.x + (ribbonRect.width * 0.5f) + deltaDeg * pxPerDeg;
                        DrawCaretIcon(new Vector2(x, ribbonRect.y + 3), predCol, $"⚠ PREDATOR {predDist:F0}m");
                    }
                }
            }
        }

        private void DrawCaretIcon(Vector2 pos, Color col, string label)
        {
            // Mini Diamond Caret
            DrawFilledBox(new Rect(pos.x - 3, pos.y, 6, 6), col, Color.white);
            GUIStyle st = new GUIStyle(compassLabelStyle) { fontSize = 8, fontStyle = FontStyle.Bold };
            st.normal.textColor = col;
            GUI.Label(new Rect(pos.x - 45, pos.y + 7, 90, 12), label, st);
        }

        #endregion

        #region Left Scuba Dive Computer Panel

        private void DrawDiveComputerPanel()
        {
            float panelW = 325f;
            float panelH = 515f;
            Rect panelRect = new Rect(24, 24, panelW, panelH);

            DrawModernGlassPanel(panelRect, new Color(0.04f, 0.09f, 0.15f, 0.90f), new Color(0.2f, 0.65f, 0.88f, 0.85f));

            GUILayout.BeginArea(new Rect(panelRect.x + 14, panelRect.y + 12, panelRect.width - 28, panelRect.height - 24));

            // Header: Model & Firmware
            GUILayout.BeginHorizontal();
            GUILayout.Label("TSAHEYLU DIVE-OS // CCR-4K", titleStyle);
            float diveSeconds = Time.time - diveStartTime;
            int mins = (int)(diveSeconds / 60f);
            int secs = (int)(diveSeconds % 60f);
            GUILayout.Label($"{mins:D2}:{secs:D2}", new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.4f, 0.9f, 1f) } });
            GUILayout.EndHorizontal();

            // Study Protocol Banner & Participant Tag
            if (ResearchStudyController.Instance != null)
            {
                Color condCol = ResearchStudyController.Instance.GetConditionColor();
                string condTag = ResearchStudyController.Instance.GetConditionBadgeText();
                string subTag = $"SUBJ: {ResearchStudyController.Instance.participantID} (TRIAL {ResearchStudyController.Instance.trialNumber})";

                GUILayout.BeginHorizontal();
                DrawPillLabel(condTag, condCol, 160f);
                GUILayout.FlexibleSpace();
                GUILayout.Label(subTag, subheaderStyle);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(2);

            // Hardware Sensor Status Pill
            bool hwActive = ESP32HeartRateBridge.Instance != null && ESP32HeartRateBridge.Instance.isHardwareConnected;
            string hwStatus = ESP32HeartRateBridge.Instance != null ? ESP32HeartRateBridge.Instance.sensorStatusText : "DEMO";
            string hwSrc = ESP32HeartRateBridge.Instance != null ? ESP32HeartRateBridge.Instance.hardwareLabel : "COM3";
            string hwTag = hwActive ?
                $"● SENSOR: {hwSrc} [{hwStatus}]" :
                "○ SENSOR: SYNTHETIC DEMO [SPACE/SHIFT]";

            Color hwCol = hwActive ?
                (hwStatus.Contains("FINGER") ? new Color(1f, 0.75f, 0.2f) : new Color(0.25f, 1f, 0.45f)) :
                new Color(0.45f, 0.85f, 1f);
            labelDimStyle.normal.textColor = hwCol;
            GUILayout.Label(hwTag, labelDimStyle);
            labelDimStyle.normal.textColor = new Color(0.55f, 0.7f, 0.82f);

            // Clinical DSP Signal Processing & Signal Quality Index (SQI)
            if (hwActive && PPGSignalFilter.Instance != null)
            {
                string sqiTag = $"● DSP: [KALMAN+MEDIAN] | SQI: {Mathf.RoundToInt(PPGSignalFilter.Instance.currentSQI)}% [{PPGSignalFilter.Instance.sqiRating}]";
                Color sqiCol = PPGSignalFilter.Instance.currentSQI >= 80f ? new Color(0.3f, 1f, 0.7f) : (PPGSignalFilter.Instance.currentSQI >= 55f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.4f));
                GUIStyle sqiSt = new GUIStyle(labelDimStyle) { normal = { textColor = sqiCol }, fontSize = 8 };
                GUILayout.Label(sqiTag, sqiSt);
            }

            // Baseline Calibration Progress or Z-Score Readout
            if (hrSystem != null)
            {
                if (hrSystem.calibrationState == BaselineCalibrationState.Calibrating)
                {
                    float p = hrSystem.calibrationProgress;
                    float rem = Mathf.Max(0f, hrSystem.calibrationDuration - hrSystem.calibrationTimer);
                    GUILayout.Label($"⏳ BASELINE CALIBRATION: {rem:F0}s ({Mathf.RoundToInt(p * 100f)}%)", headerStyle);
                    DrawGradientBar(p, new Color(1f, 0.82f, 0.2f));
                }
                else if (hrSystem.calibrationState == BaselineCalibrationState.Calibrated)
                {
                    string zSign = hrSystem.currentZScore >= 0 ? "+" : "";
                    Color zCol = Mathf.Abs(hrSystem.currentZScore) < 1.0f ? new Color(0.3f, 1f, 0.65f) : (hrSystem.currentZScore > 2.0f ? Color.red : new Color(1f, 0.8f, 0.2f));
                    GUILayout.Label($"BASELINE: μ={hrSystem.baselineMeanBPM:F0} BPM, σ={hrSystem.baselineStdDevBPM:F1} | Z={zSign}{hrSystem.currentZScore:F2}σ", new GUIStyle(labelDimStyle) { normal = { textColor = zCol } });
                }
            }
            GUILayout.Space(4);

            // CELL 2: Biometric Cardiorespiratory Telemetry
            float bpm = hrSystem != null ? hrSystem.currentBPM : 72f;
            Color hrColor = GetHeartRateColor(bpm);
            HRVEngine hrv = HRVEngine.Instance;
            float rmssd = hrv != null ? hrv.rmssd : 45f;
            float lfHf = hrv != null ? hrv.lfHfRatio : 1.05f;
            float respBpm = hrv != null ? hrv.derivedRespirationBPM : 14.2f;
            string stateName = SentisAffectiveInference.Instance != null ?
                SentisAffectiveInference.Instance.GetDominantStateName() : "[CALM FLOW]";

            bool isControl = ResearchStudyController.Instance != null && ResearchStudyController.Instance.activeCondition == ResearchStudyCondition.ConditionA_Control;
            bool isAmbientOnly = ResearchStudyController.Instance != null && ResearchStudyController.Instance.activeCondition == ResearchStudyCondition.ConditionC_AmbientAdaptive && !showResearcherOverlay;

            bool fingerMissing = hwActive && (ESP32HeartRateBridge.Instance != null && !ESP32HeartRateBridge.Instance.isFingerPlaced);
            bool isAcquiring = hwActive && !fingerMissing && (hrSystem != null && hrSystem.useHardwareSensor && !hrSystem.hasHardwareBPM);

            if (!isControl && !isAmbientOnly)
            {
                if (fingerMissing)
                {
                    GUILayout.BeginHorizontal();
                    DrawPillLabel("⚠️ PLACE FINGER ON SENSOR", new Color(1f, 0.7f, 0.1f), panelW - 40);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(2);
                }
                else if (isAcquiring)
                {
                    GUILayout.BeginHorizontal();
                    DrawPillLabel("⏳ ACQUIRING OPTICAL PULSE...", new Color(0.2f, 0.85f, 1f), panelW - 40);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(2);
                }

                GUILayout.BeginHorizontal();
                hugeValueStyle.normal.textColor = fingerMissing ? new Color(0.65f, 0.65f, 0.65f) : (isAcquiring ? new Color(0.4f, 0.8f, 1f) : hrColor);
                string beatHeartIcon = fingerMissing ? "♡" : (heartbeatVisualPulse > 1.15f ? "♥" : "♡");
                string hrText = fingerMissing ? "♡ --" : (isAcquiring ? "♡ ..." : $"{beatHeartIcon} {Mathf.RoundToInt(bpm)}");
                GUILayout.Label(hrText, hugeValueStyle, GUILayout.Width(130));
                GUILayout.BeginVertical();
                GUILayout.Label("BEATS / MIN", headerStyle);
                string conciseState = fingerMissing ? "NO CONTACT" : (isAcquiring ? "ACQUIRING" : stateName);
                if (conciseState.IndexOf("STRESSED", StringComparison.OrdinalIgnoreCase) >= 0) conciseState = "STRESSED";
                else if (conciseState.IndexOf("CALM", StringComparison.OrdinalIgnoreCase) >= 0) conciseState = "CALM FLOW";
                else if (conciseState.IndexOf("FLOW", StringComparison.OrdinalIgnoreCase) >= 0) conciseState = "FLOW";
                else if (conciseState.IndexOf("PANIC", StringComparison.OrdinalIgnoreCase) >= 0) conciseState = "PANIC";
                DrawPillLabel(conciseState, fingerMissing ? new Color(1f, 0.7f, 0.1f) : (isAcquiring ? new Color(0.2f, 0.85f, 1f) : hrColor), 110f);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                // Dual Visual Biofeedback: Live Oscilloscope Sweep (Left) + Poincaré Nonlinear Scatter Plot (Right)
                if (ECGWaveformMonitor.Instance != null && (ResearchStudyController.Instance == null || ResearchStudyController.Instance.isExplicitHUDVisible))
                {
                    GUILayout.BeginHorizontal();
                    Rect scopeRect = GUILayoutUtility.GetRect(panelW - 110, 42);
                    DrawModernGlassPanel(scopeRect, new Color(0.01f, 0.04f, 0.08f, 0.95f), new Color(0.18f, 0.45f, 0.6f, 0.7f));
                    ECGWaveformMonitor.Instance.DrawOscilloscope(scopeRect, hrColor);

                    Rect poincareRect = GUILayoutUtility.GetRect(66, 42);
                    DrawPoincarePlot(poincareRect);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(2);
                }

                // Tsaheylu Bio-Bonding Index (Φ)
                if (hrv != null)
                {
                    GUILayout.BeginHorizontal();
                    labelStyle.normal.textColor = hrv.tsaheyluBondColor;
                    GUILayout.Label($"TSAHEYLU BOND (Φ): {hrv.tsaheyluBondIndex:F0}%", new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold, normal = { textColor = hrv.tsaheyluBondColor } });
                    GUILayout.FlexibleSpace();
                    DrawPillLabel(hrv.currentBondTier.ToString().ToUpper(), hrv.tsaheyluBondColor, 105f);
                    GUILayout.EndHorizontal();
                    DrawGradientBar(hrv.tsaheyluBondIndex / 100f, hrv.tsaheyluBondColor);
                    GUILayout.Space(2);
                }

                // Frequency-Domain HRV Spectrum & Sympathovagal Balance Bar
                GUILayout.BeginHorizontal();
                GUILayout.Label($"HRV (RMSSD): {rmssd:F1} ms", subheaderStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"LF/HF: {lfHf:F2} | SD1/2: {hrv?.sd1Sd2Ratio:F2}", subheaderStyle);
                GUILayout.EndHorizontal();

                // Bipolar sympathovagal bar: 0.5 (Vagal/Parasympathetic) to 3.5 (Sympathetic Surge)
                float sympRatio01 = Mathf.InverseLerp(0.5f, 3.0f, lfHf);
                DrawBipolarBalanceBar(sympRatio01);

                GUILayout.BeginHorizontal();
                labelDimStyle.normal.textColor = new Color(0.3f, 0.95f, 0.7f);
                GUILayout.Label("◄ VAGAL / RELAX", labelDimStyle);
                GUILayout.FlexibleSpace();
                labelDimStyle.normal.textColor = new Color(1f, 0.45f, 0.3f);
                GUILayout.Label("SYMPATHETIC / STRESS ►", labelDimStyle);
                labelDimStyle.normal.textColor = new Color(0.55f, 0.7f, 0.82f);
                GUILayout.EndHorizontal();

                // Mammalian Diving Reflex (DRI) & SpO2
                if (hrSystem != null)
                {
                    GUILayout.BeginHorizontal();
                    string driTag = hrSystem.isDivingReflexActive ?
                        $"DRI: +{hrSystem.mammalianDiveReflexIndex:F0}% [METKAYINA BRADYCARDIAC O2 -45%]" :
                        $"DRI: {hrSystem.mammalianDiveReflexIndex:+0.0;-0.0;0.0}%";
                    Color driCol = hrSystem.isDivingReflexActive ? new Color(0.25f, 1f, 0.6f) : new Color(0.6f, 0.75f, 0.85f);
                    GUILayout.Label(driTag, new GUIStyle(subheaderStyle) { normal = { textColor = driCol } });
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"SpO2: {hrSystem.currentSpO2:F1}%", subheaderStyle);
                    GUILayout.EndHorizontal();
                }

                // Respiratory Sinus Arrhythmia
                GUILayout.Label($"RSA Respiration: {respBpm:F1} BrPM  |  {hrv?.circumplexQuadrant.ToString().Replace("_", " / ")}", labelDimStyle);
            }
            else
            {
                GUILayout.Label(isControl ? "BIO-HUD: [CONTROL CONDITION HIDDEN]" : "BIO-HUD: [AMBIENT ADAPTIVE — F8 VIEW]", headerStyle);
            }
            GUILayout.Space(6);

            // CELL 3: Decompression & Hyperbaric Telemetry
            float depth = diver != null ? diver.currentDepth : 0f;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("DEPTH", headerStyle);
            bigValueStyle.normal.textColor = new Color(0.3f, 0.95f, 1f);
            GUILayout.Label($"{depth:F1} M", bigValueStyle);
            GUILayout.Label($"MAX: {maxDepthLogged:F1} M", labelDimStyle);
            GUILayout.EndVertical();

            // Ascent Rate Monitor (ASC RATE)
            GUILayout.BeginVertical(GUILayout.Width(130));
            GUILayout.Label("ASCENT RATE", headerStyle);
            Color ascCol = ascentRateMPerMin > 9.0f ? Color.red : (ascentRateMPerMin > 6.0f ? Color.yellow : new Color(0.2f, 1f, 0.5f));
            GUILayout.Label($"{ascentRateMPerMin:+0.0;-0.0;0.0} M/MIN", new GUIStyle(bigValueStyle) { fontSize = 16, normal = { textColor = ascCol } });
            DrawAscentRateMeter(ascentRateMPerMin);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            // Bühlmann No-Decompression Limit (NDL)
            int ndlMinutes = CalculateSimulatedNDL(depth);
            string ndlText = ndlMinutes >= 99 ? "NDL: 99+ MIN" : $"NDL: {ndlMinutes} MIN (DECO SAFE)";
            GUILayout.Label(ndlText, new GUIStyle(subheaderStyle) { normal = { textColor = ndlMinutes < 15 ? new Color(1f, 0.5f, 0.2f) : new Color(0.4f, 0.9f, 0.7f) } });

            // Oxygen Tank & Partial Pressure (PO2)
            float o2 = hrSystem != null ? hrSystem.currentOxygen : 100f;
            bool isSurfaced = diver != null && diver.transform.position.y >= -1.5f;
            float tankBar = o2 * 2.1f; // 210 BAR full tank
            float po2 = Mathf.Clamp(1.25f + (depth / 35f) * 0.15f, 0.21f, 1.45f);

            string o2Header = isSurfaced ? $"O2 TANK: {Mathf.RoundToInt(o2)}% [SURFACED: VENT OPEN]" : $"O2 TANK: {Mathf.RoundToInt(o2)}% ({tankBar:F0} BAR | PO2: {po2:F2} ATA)";
            GUILayout.Label(o2Header, headerStyle);
            DrawGradientBar(o2 / 100f, o2 > 25f ? new Color(0.2f, 0.85f, 1f) : new Color(0.95f, 0.25f, 0.2f));

            GUILayout.Space(6);

            // CELL 4: Locomotion & Creature Symbiosis
            DrawLocomotionAndSymbiosisStatus();

            GUILayout.EndArea();
        }

        private int CalculateSimulatedNDL(float depth)
        {
            // Standard Bühlmann ZHL-16C air curve approximation
            if (depth <= 10f) return 99;
            if (depth <= 15f) return 72;
            if (depth <= 20f) return 45;
            if (depth <= 25f) return 29;
            if (depth <= 30f) return 18;
            if (depth <= 35f) return 12;
            if (depth <= 40f) return 8;
            return 5;
        }

        private void DrawAscentRateMeter(float rateMPerMin)
        {
            // 6-segment LED vertical/horizontal ladder
            int totalSegs = 6;
            int activeSegs = Mathf.Clamp(Mathf.RoundToInt((rateMPerMin / 12f) * totalSegs), 0, totalSegs);

            Rect barRect = GUILayoutUtility.GetRect(120, 8);
            float segW = (barRect.width - (totalSegs - 1) * 2f) / totalSegs;

            for (int i = 0; i < totalSegs; i++)
            {
                Rect segRect = new Rect(barRect.x + i * (segW + 2f), barRect.y, segW, barRect.height);
                Color segColor = new Color(0.1f, 0.18f, 0.25f, 0.6f); // Inactive dim

                if (i < activeSegs)
                {
                    segColor = (i < 3) ? new Color(0.2f, 1f, 0.5f) : ((i < 5) ? new Color(1f, 0.85f, 0.2f) : Color.red);
                }
                DrawFilledBox(segRect, segColor, Color.clear);
            }
        }

        private void DrawLocomotionAndSymbiosisStatus()
        {
            if (diver != null && diver.isRidingMount)
            {
                bool isSkim = diver.currentMount is SkimwingMountAI || (SkimwingMountAI.Instance != null && SkimwingMountAI.Instance.isMounted);
                if (isSkim)
                {
                    bool airGliding = SkimwingMountAI.Instance != null && SkimwingMountAI.Instance.isAirGliding;
                    labelStyle.normal.textColor = new Color(1f, 0.6f, 0.15f);
                    GUILayout.Label(airGliding ? "🦅 SKIMWING: AIR GLIDE (26 m/s)" : "🦅 MOUNT: PANDORAN SKIMWING", labelStyle);
                    labelStyle.normal.textColor = new Color(0.3f, 1f, 0.6f);
                    GUILayout.Label("🫁 GILL SYMBIOSIS: O2 DRAIN FROZEN", labelStyle);
                }
                else
                {
                    labelStyle.normal.textColor = new Color(0.2f, 0.95f, 1f);
                    GUILayout.Label("🐎 MOUNT: PANDORAN ILU (2.5x GLIDE)", labelStyle);
                    labelStyle.normal.textColor = new Color(0.3f, 1f, 0.6f);
                    GUILayout.Label("🫁 GILL SYMBIOSIS: O2 DRAIN FROZEN", labelStyle);
                }
            }
            else
            {
                bool isGrounded = diver != null && diver.isGrounded;
                bool isAboveWater = diver != null && diver.transform.position.y > 0.1f;
                string locoText = isAboveWater ? "🏝️ SURFACE STILT DOCK" : (isGrounded ? "👣 SEABED STRIDING" : "🏊 3D HYDRO-SWIMMING");
                Color locoCol = isAboveWater ? new Color(1f, 0.85f, 0.2f) : (isGrounded ? new Color(0.3f, 1f, 0.6f) : new Color(0.3f, 0.85f, 1f));
                labelStyle.normal.textColor = locoCol;
                GUILayout.Label(locoText, labelStyle);

                if (cachedManta == null) cachedManta = FindAnyObjectByType<MantaCompanionAI>();
                if (cachedManta != null && diver != null && Vector3.Distance(cachedManta.transform.position, diver.transform.position) < 6.5f)
                {
                    labelStyle.normal.textColor = new Color(0.2f, 1f, 0.85f);
                    GUILayout.Label("⚡ ILU DRAFTING: +50% SPEED BOOST", labelStyle);
                }
            }
            labelStyle.normal.textColor = Color.white;

            // Sonar & Eclipse State
            if (MetkayinaSonarSystem.Instance != null)
            {
                bool ready = MetkayinaSonarSystem.Instance.isReady;
                string sText = ready ? "📡 SONAR [TAB/V]: READY (PRESS)" : $"📡 SONAR: {MetkayinaSonarSystem.Instance.currentCooldownTimer:F1}s";
                Color sCol = ready ? new Color(0.3f, 1f, 0.9f) : new Color(0.6f, 0.7f, 0.8f);
                labelDimStyle.normal.textColor = sCol;
                GUILayout.Label(sText, labelDimStyle);
            }

            if (PandoranDayNightCycle.Instance != null)
            {
                bool eclipse = PandoranDayNightCycle.Instance.isEclipse;
                string cText = eclipse ? "🌘 POLY-ECLIPSE [L]: ACTIVE" : "☀️ PANDORAN DAY [L]: ACTIVE";
                labelDimStyle.normal.textColor = eclipse ? new Color(0.85f, 0.45f, 1f) : new Color(0.5f, 0.85f, 1f);
                GUILayout.Label(cText, labelDimStyle);
                labelDimStyle.normal.textColor = new Color(0.55f, 0.7f, 0.82f);
            }
        }

        #endregion

        #region Right Objectives Panel

        private void DrawMissionObjectives()
        {
            float panelW = 285f;
            float panelH = 175f;
            Rect panelRect = new Rect(Screen.width - panelW - 24, 24, panelW, panelH);

            DrawModernGlassPanel(panelRect, new Color(0.04f, 0.09f, 0.15f, 0.86f), new Color(0.2f, 0.65f, 0.88f, 0.80f));

            GUILayout.BeginArea(new Rect(panelRect.x + 14, panelRect.y + 12, panelRect.width - 28, panelRect.height - 24));

            GUILayout.Label("METKAYINA REEF OBJECTIVES", titleStyle);
            GUILayout.Space(6);

            int pearls = gameManager != null ? gameManager.collectedPearls : 0;
            int totalPearls = gameManager != null ? gameManager.totalPearlsRequired : 5;
            bool pearlsDone = pearls >= totalPearls;

            DrawObjectiveRow("Glowing Pearls", $"{pearls} / {totalPearls}", pearlsDone, new Color(1f, 0.85f, 0.2f));

            int beacons = gameManager != null ? gameManager.activatedBeacons : 0;
            int totalBeacons = gameManager != null ? gameManager.totalBeaconsRequired : 2;
            bool beaconsDone = beacons >= totalBeacons;

            DrawObjectiveRow("Spirit Trees", $"{beacons} / {totalBeacons}", beaconsDone, new Color(0.2f, 1f, 0.85f));

            GUILayout.Space(4);
            bool gateUnlocked = pearlsDone && beaconsDone;
            Color gateColor = gateUnlocked ? new Color(0.2f, 1f, 0.5f) : new Color(0.85f, 0.4f, 0.4f);
            string gateStatus = gateUnlocked ? "UNLOCKED (SWIM UP)" : "LOCKED (COLLECT PEARLS)";
            DrawObjectiveRow("Surface Gate", gateStatus, gateUnlocked, gateColor);

            GUILayout.EndArea();
        }

        private void DrawObjectiveRow(string label, string status, bool isComplete, Color glowColor)
        {
            GUILayout.BeginHorizontal();
            string icon = isComplete ? "✓" : "○";
            Color col = isComplete ? new Color(0.25f, 1f, 0.55f) : glowColor;

            labelStyle.normal.textColor = col;
            GUILayout.Label($"{icon} {label}:", labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(status, new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold, normal = { textColor = col } });
            GUILayout.EndHorizontal();
            labelStyle.normal.textColor = Color.white;
        }

        #endregion

        #region Resonance Breathing Hologram Pacer

        private void DrawBreathingPacer()
        {
            // Bottom-Left Circular Breathing Guide (~0.1 Hz)
            float baseRadius = 38f;
            float pulseOffset = Mathf.Sin(breathPhase) * 12f;
            float currentRadius = baseRadius + pulseOffset;

            Vector2 center = new Vector2(105, Screen.height - 105);

            // Determine Breath Phase
            float sinVal = Mathf.Sin(breathPhase);
            float cosVal = Mathf.Cos(breathPhase);

            string breathText;
            Color ringColor;

            if (sinVal > 0.3f)
            {
                breathText = "INHALE ▲";
                ringColor = new Color(0.25f, 0.95f, 1f, 0.85f);
            }
            else if (sinVal < -0.3f)
            {
                breathText = "EXHALE ▼";
                ringColor = new Color(0.2f, 1f, 0.65f, 0.85f);
            }
            else
            {
                breathText = cosVal > 0 ? "HOLD ⏸" : "HOLD ⏸";
                ringColor = new Color(1f, 0.85f, 0.3f, 0.75f);
            }

            // Draw outer frosted circular glass backing
            Rect backingRect = new Rect(center.x - 55, center.y - 55, 110, 110);
            DrawModernGlassPanel(backingRect, new Color(0.03f, 0.08f, 0.14f, 0.82f), new Color(0.2f, 0.6f, 0.8f, 0.6f));

            // Draw Anti-Aliased Breathing Ring
            Rect ringRect = new Rect(center.x - currentRadius, center.y - currentRadius, currentRadius * 2f, currentRadius * 2f);
            GUI.color = ringColor;
            GUI.DrawTexture(ringRect, smoothRingTex);
            GUI.color = Color.white;

            // In-Ring Breath Text
            GUIStyle pacerStyle = new GUIStyle(compassLabelStyle)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ringColor }
            };
            GUI.Label(new Rect(center.x - 50, center.y - 8, 100, 16), breathText, pacerStyle);

            GUIStyle subPacer = new GUIStyle(compassLabelStyle) { fontSize = 8, normal = { textColor = new Color(0.6f, 0.8f, 0.95f, 0.7f) } };
            GUI.Label(new Rect(center.x - 50, center.y + 8, 100, 12), "0.1 Hz FLOW", subPacer);
        }

        #endregion

        #region Crosshair & Tactical Reticle

        private void DrawCrosshairAndPrompts()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            Color reticleColor = new Color(0.4f, 0.9f, 1f, 0.75f);

            if (diver != null && diver.focusedInteractable != null && diver.focusedInteractable.canInteract)
            {
                reticleColor = new Color(0.2f, 1f, 0.75f, 0.95f);

                // Show Contextual Interaction Prompt
                string prompt = diver.focusedInteractable.interactionPrompt;
                Rect promptRect = new Rect(cx - 160, cy + 42, 320, 38);
                DrawModernGlassPanel(promptRect, new Color(0.04f, 0.14f, 0.18f, 0.95f), new Color(0.2f, 1f, 0.8f, 0.9f));
                GUI.Label(promptRect, prompt, promptStyle);
            }

            // Draw Tactical Rangefinder Reticle
            float reticleR = 14f;
            Rect ringRect = new Rect(cx - reticleR, cy - reticleR, reticleR * 2, reticleR * 2);
            GUI.color = new Color(reticleColor.r, reticleColor.g, reticleColor.b, 0.35f);
            GUI.DrawTexture(ringRect, smoothRingTex);

            // Center targeting crosshairs
            DrawFilledBox(new Rect(cx - 2, cy - 2, 4, 4), reticleColor, Color.clear);
            DrawFilledBox(new Rect(cx - 16, cy - 0.5f, 8, 1), reticleColor, Color.clear);
            DrawFilledBox(new Rect(cx + 8, cy - 0.5f, 8, 1), reticleColor, Color.clear);
            DrawFilledBox(new Rect(cx - 0.5f, cy - 16, 1, 8), reticleColor, Color.clear);
            DrawFilledBox(new Rect(cx - 0.5f, cy + 8, 1, 8), reticleColor, Color.clear);
            GUI.color = Color.white;
        }

        #endregion

        #region 3D Sonar Target Diamond Pings

        private void DrawSonarPings()
        {
            if (MetkayinaSonarSystem.Instance == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            var pings = MetkayinaSonarSystem.Instance.activePings;
            if (pings == null || pings.Count == 0) return;

            for (int i = 0; i < pings.Count; i++)
            {
                var ping = pings[i];
                Vector3 screenPoint = cam.WorldToScreenPoint(ping.worldPosition);

                if (screenPoint.z > 0.5f)
                {
                    float guiY = Screen.height - screenPoint.y;
                    float dist = Vector3.Distance(cam.transform.position, ping.worldPosition);
                    float alpha = Mathf.Clamp01(ping.remainingLifetime / 2.5f);
                    Color col = ping.pingColor;
                    col.a = alpha;

                    // Diamond reticle cross
                    DrawFilledBox(new Rect(screenPoint.x - 8, guiY - 1, 16, 2), col, Color.clear);
                    DrawFilledBox(new Rect(screenPoint.x - 1, guiY - 8, 2, 16), col, Color.clear);

                    // Diamond ring brackets
                    DrawFilledBox(new Rect(screenPoint.x - 6, guiY - 6, 12, 1), col, Color.clear);
                    DrawFilledBox(new Rect(screenPoint.x - 6, guiY + 6, 12, 1), col, Color.clear);
                    DrawFilledBox(new Rect(screenPoint.x - 6, guiY - 6, 1, 12), col, Color.clear);
                    DrawFilledBox(new Rect(screenPoint.x + 6, guiY - 6, 1, 12), col, Color.clear);

                    // On-screen Ping Tag
                    string tagText = $"{ping.label} [{Mathf.RoundToInt(dist)}m]";
                    GUIStyle pingStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 11,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = col }
                    };
                    GUI.Label(new Rect(screenPoint.x - 90, guiY + 9, 180, 20), tagText, pingStyle);
                }
            }
        }

        #endregion

        #region Situational Alerts & Alarms

        private void DrawRapidAscentWarning()
        {
            if (diver != null && diver.currentDepth > 4f && ascentRateMPerMin > 9.0f)
            {
                float flash = Mathf.PingPong(Time.time * 6f, 1f);
                Color warnCol = Color.Lerp(new Color(1f, 0.4f, 0.1f), Color.red, flash);

                Rect warnRect = new Rect((Screen.width - 440) * 0.5f, 68, 440, 34);
                DrawModernGlassPanel(warnRect, new Color(0.15f, 0.02f, 0.02f, 0.95f), warnCol);

                GUI.Label(warnRect, "⚠ SLOW DOWN! ASCENT TOO RAPID (>9m/min) ⚠", new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = warnCol }
                });
            }
        }

        private void DrawOxygenWarning()
        {
            if (hrSystem != null && hrSystem.currentOxygen < 20f)
            {
                float flash = Mathf.PingPong(Time.time * 4f, 1f);
                Color warnCol = new Color(0.95f, 0.1f, 0.1f, flash * 0.35f);
                DrawFilledBox(new Rect(0, 0, Screen.width, Screen.height), warnCol, Color.clear);

                Rect warnRect = new Rect((Screen.width - 320) * 0.5f, 108, 320, 36);
                DrawModernGlassPanel(warnRect, new Color(0.18f, 0.02f, 0.02f, 0.95f), Color.red);

                GUI.Label(warnRect, "⚠ CRITICAL OXYGEN DEPLETION ⚠", new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.red }
                });
            }
        }

        private void DrawPredatorWarning()
        {
            if (cachedPredator == null) cachedPredator = FindAnyObjectByType<ApexPredatorAI>();
            if (cachedPredator != null && cachedPredator.currentState != PredatorState.DormantPatrol)
            {
                float flash = Mathf.PingPong(Time.time * 4f, 1f);
                Color warnCol = Color.Lerp(new Color(1f, 0.45f, 0.1f), Color.red, flash);

                Rect warnRect = new Rect((Screen.width - 480) * 0.5f, 148, 480, 34);
                DrawModernGlassPanel(warnRect, new Color(0.12f, 0.02f, 0.02f, 0.92f), warnCol);

                GUI.Label(warnRect, "⚠ ACOUSTIC CARDIAC LOCK: SLOW HEART RATE TO EVADE APEX PREDATOR ⚠", new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = warnCol }
                });
            }
        }

        private void DrawScientificLogPopup()
        {
            if (logDisplayTimer <= 0f) return;
            logDisplayTimer -= Time.deltaTime;

            Rect logRect = new Rect((Screen.width - 520) * 0.5f, 110, 520, 95);
            DrawModernGlassPanel(logRect, new Color(0.06f, 0.04f, 0.14f, 0.96f), new Color(0.85f, 0.4f, 1f, 0.9f));

            GUILayout.BeginArea(new Rect(logRect.x + 16, logRect.y + 10, logRect.width - 32, logRect.height - 20));
            GUILayout.Label($"🔬 {activeLogTitle.ToUpper()}", titleStyle);
            GUILayout.Space(3);
            labelStyle.normal.textColor = new Color(0.95f, 0.9f, 1f);
            GUILayout.Label(activeLogBody, labelStyle);
            labelStyle.normal.textColor = Color.white;
            GUILayout.EndArea();
        }

        private void DrawMinimalControlsHint()
        {
            float w = 980f;
            Rect hintRect = new Rect((Screen.width - w) * 0.5f, Screen.height - 34, w, 24);
            DrawModernGlassPanel(hintRect, new Color(0.03f, 0.06f, 0.10f, 0.75f), new Color(0.15f, 0.4f, 0.55f, 0.4f));

            string controls = "WASD: Swim | Space: Ascend | Q/C: Dive | E: Mount/Act | B: Calibrate | 2: Harmonic Mode | F8: DSP Filter | Tab/V: Sonar | F6: Survey | F7: CSV | F10: Procus SBS | R: Recenter";
            GUI.Label(hintRect, controls, new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = new Color(0.75f, 0.88f, 1f, 0.85f) }
            });
        }

        private void DrawVictoryScreen()
        {
            DrawFilledBox(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.08f, 0.14f, 0.88f), Color.clear);

            Rect boxRect = new Rect((Screen.width - 580) * 0.5f, (Screen.height - 320) * 0.5f, 580, 320);
            DrawModernGlassPanel(boxRect, new Color(0.04f, 0.12f, 0.18f, 0.98f), new Color(0.2f, 1f, 0.75f, 0.9f));

            GUILayout.BeginArea(new Rect(boxRect.x + 24, boxRect.y + 18, boxRect.width - 48, boxRect.height - 36));

            GUILayout.Label("PANDORAN COVE HARMONY RESTORED!", victoryStyle);
            GUILayout.Space(6);

            float avgRMSSD = HRVEngine.Instance != null ? HRVEngine.Instance.rmssd : 45f;
            string grade = avgRMSSD > 52f ? "GRADE S: BIO-ZEN MASTER" :
                          (avgRMSSD > 38f ? "GRADE A: CALM EXPLORER" :
                          (avgRMSSD > 25f ? "GRADE B: FLOW DIVER" : "GRADE C: ADRENALINE SURVIVOR"));
            Color gradeColor = avgRMSSD > 52f ? new Color(0.2f, 1f, 0.8f) : (avgRMSSD > 38f ? Color.green : Color.yellow);

            GUILayout.Label($"★ {grade} ★", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = gradeColor } });
            GUILayout.Space(4);

            labelStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("You bonded with the Metkayina Spirit Trees and unified planetary harmony.", labelStyle);
            GUILayout.Label("Heart rate equilibrium maintained throughout the dive.", labelStyle);
            GUILayout.Space(6);

            string csvPath = ResearchTelemetryExporter.Instance != null ?
                ResearchTelemetryExporter.Instance.lastExportedFilePath : "Logs/DiveTelemetry.csv";
            int count = ResearchTelemetryExporter.Instance != null ?
                ResearchTelemetryExporter.Instance.loggedPointCount : 0;

            labelStyle.normal.textColor = new Color(0.35f, 0.95f, 1f);
            GUILayout.Label("[ACADEMIC DIVE TELEMETRY EXPORTED]", labelStyle);
            GUILayout.Label($"Points Logged: {count} @ 10 Hz | Saved: {System.IO.Path.GetFileName(csvPath)}", labelStyle);
            labelStyle.normal.textColor = Color.white;
            GUILayout.Space(12);

            if (GUILayout.Button("Dive Again", GUILayout.Height(38)))
            {
                gameManager.RestartDive();
            }

            labelStyle.alignment = TextAnchor.MiddleLeft;
            GUILayout.EndArea();
        }

        #endregion

        #region Drawing Helpers

        private void DrawModernGlassPanel(Rect rect, Color bodyColor, Color rimColor)
        {
            // Panel Body
            GUI.color = bodyColor;
            GUI.DrawTexture(rect, glassPanelTex);

            // Subtle interior dot matrix grid
            GUI.color = new Color(1f, 1f, 1f, 0.08f);
            GUI.DrawTextureWithTexCoords(rect, dotGridTex, new Rect(0f, 0f, rect.width / 32f, rect.height / 32f));

            // Glowing Luminous Rim & Corner Brackets
            GUI.color = rimColor;
            // 4 Chamfered corner brackets
            float cLen = 10f;
            GUI.DrawTexture(new Rect(rect.x, rect.y, cLen, 2), whiteTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2, cLen), whiteTex);

            GUI.DrawTexture(new Rect(rect.x + rect.width - cLen, rect.y, cLen, 2), whiteTex);
            GUI.DrawTexture(new Rect(rect.x + rect.width - 2, rect.y, 2, cLen), whiteTex);

            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - 2, cLen, 2), whiteTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - cLen, 2, cLen), whiteTex);

            GUI.DrawTexture(new Rect(rect.x + rect.width - cLen, rect.y + rect.height - 2, cLen, 2), whiteTex);
            GUI.DrawTexture(new Rect(rect.x + rect.width - 2, rect.y + rect.height - cLen, 2, cLen), whiteTex);

            GUI.color = Color.white;
        }

        private void DrawFilledBox(Rect rect, Color fill, Color border)
        {
            GUI.color = fill;
            GUI.DrawTexture(rect, whiteTex);

            if (border != Color.clear)
            {
                GUI.color = border;
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1), whiteTex);
                GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), whiteTex);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 1, rect.height), whiteTex);
                GUI.DrawTexture(new Rect(rect.x + rect.width - 1, rect.y, 1, rect.height), whiteTex);
            }
            GUI.color = Color.white;
        }

        private void DrawGradientBar(float fill01, Color barColor)
        {
            Rect barRect = GUILayoutUtility.GetRect(200, 10);
            DrawFilledBox(barRect, new Color(0.05f, 0.12f, 0.18f, 0.9f), new Color(0.15f, 0.35f, 0.5f, 0.5f));

            float fillWidth = Mathf.Clamp01(fill01) * (barRect.width - 2);
            if (fillWidth > 0)
            {
                GUI.color = barColor;
                GUI.DrawTexture(new Rect(barRect.x + 1, barRect.y + 1, fillWidth, barRect.height - 2), gradientBarTex);
                GUI.color = Color.white;
            }
        }

        private void DrawBipolarBalanceBar(float ratio01)
        {
            Rect barRect = GUILayoutUtility.GetRect(200, 8);
            DrawFilledBox(barRect, new Color(0.04f, 0.1f, 0.15f, 0.9f), new Color(0.15f, 0.35f, 0.45f, 0.5f));

            // Center marker line
            float midX = barRect.x + barRect.width * 0.5f;
            DrawFilledBox(new Rect(midX - 1, barRect.y, 2, barRect.height), new Color(0.5f, 0.7f, 0.9f, 0.8f), Color.clear);

            // Active balance marker
            float markerX = barRect.x + Mathf.Clamp01(ratio01) * barRect.width;
            Color markCol = ratio01 < 0.45f ? new Color(0.2f, 1f, 0.7f) : (ratio01 > 0.65f ? Color.red : new Color(1f, 0.85f, 0.2f));
            DrawFilledBox(new Rect(markerX - 3, barRect.y - 1, 6, barRect.height + 2), markCol, Color.white);
        }

        private void DrawPillLabel(string text, Color col, float width)
        {
            Rect pillRect = GUILayoutUtility.GetRect(width, 18);
            Color body = new Color(col.r * 0.2f, col.g * 0.2f, col.b * 0.2f, 0.85f);
            DrawFilledBox(pillRect, body, col);

            GUIStyle st = new GUIStyle(pillStyle);
            st.normal.textColor = col;
            GUI.Label(pillRect, text, st);
        }

        private void DrawPoincarePlot(Rect rect)
        {
            DrawModernGlassPanel(rect, new Color(0.01f, 0.04f, 0.08f, 0.95f), new Color(0.2f, 0.5f, 0.7f, 0.7f));

            // Identity diagonal line (RR_n = RR_{n+1})
            DrawFilledBox(new Rect(rect.x + 2, rect.y + rect.height * 0.5f, rect.width - 4, 1), new Color(0.2f, 0.4f, 0.5f, 0.35f), Color.clear);

            HRVEngine hrv = HRVEngine.Instance;
            if (hrv == null || hrv.poincarePoints.Count < 2) return;

            // Plot range: 450ms to 1250ms
            float minRR = 450f;
            float maxRR = 1250f;

            for (int i = 0; i < hrv.poincarePoints.Count; i++)
            {
                Vector2 pt = hrv.poincarePoints[i];
                float nx = Mathf.Clamp01((pt.x - minRR) / (maxRR - minRR));
                float ny = Mathf.Clamp01((pt.y - minRR) / (maxRR - minRR));

                float px = rect.x + 4 + nx * (rect.width - 8);
                float py = rect.y + rect.height - 4 - ny * (rect.height - 8);

                float alpha = Mathf.Lerp(0.35f, 1.0f, (float)i / hrv.poincarePoints.Count);
                Color dotCol = (i == hrv.poincarePoints.Count - 1) ? new Color(1f, 0.9f, 0.2f, 1f) : new Color(0.2f, 0.95f, 1f, alpha);
                DrawFilledBox(new Rect(px - 1.5f, py - 1.5f, 3, 3), dotCol, Color.clear);
            }

            // Scatter label
            GUIStyle st = new GUIStyle(compassLabelStyle) { fontSize = 7, normal = { textColor = new Color(0.5f, 0.85f, 1f, 0.8f) } };
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, 12), $"POINCARÉ", st);
        }

        private Color GetHeartRateColor(float bpm)
        {
            if (bpm < 65f) return new Color(0.2f, 0.95f, 1f); // Calm cyan
            if (bpm < 95f) return new Color(0.3f, 1f, 0.55f); // Normal emerald
            if (bpm < 118f) return new Color(1f, 0.85f, 0.2f); // Elevated amber
            return new Color(1f, 0.25f, 0.25f); // Stress red
        }

        #endregion
    }
}
