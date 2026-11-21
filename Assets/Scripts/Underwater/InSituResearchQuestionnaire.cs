using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnderwaterReef
{
    /// <summary>
    /// In-Situ In-VR Psychological Assessment Engine.
    /// Eliminates the need to take off the headset or alt-tab during research trials:
    /// - Self-Assessment Manikin (SAM): Valence (1-5) and Arousal (1-5).
    /// - NASA-TLX Short Form: Mental Demand (1-10), Physical Demand (1-10), Frustration (1-10).
    /// - Automatically logs timestamped responses to Logs/QuestionnaireResponses.csv.
    /// </summary>
    public class InSituResearchQuestionnaire : MonoBehaviour
    {
        public static InSituResearchQuestionnaire Instance { get; private set; }

        [Header("State")]
        public bool isQuestionnaireActive = false;
        public string currentMilestoneTag = "Checkpoint";

        [Header("Current Responses")]
        public int samValence = 3;       // 1 = Very Unpleasant, 5 = Very Pleasant
        public int samArousal = 2;       // 1 = Deeply Calm, 5 = Highly Aroused/Stressed
        public int tlxMentalDemand = 3;  // 1 = Very Low, 10 = Very High
        public int tlxPhysicalDemand = 3;// 1 = Very Low, 10 = Very High
        public int tlxFrustration = 2;   // 1 = Very Low, 10 = Very High

        private GUIStyle headerStyle;
        private GUIStyle promptStyle;
        private GUIStyle buttonStyle;
        private GUIStyle activeButtonStyle;
        private GUIStyle submitStyle;
        private Texture2D panelBackground;
        private Texture2D buttonNormalTex;
        private Texture2D buttonActiveTex;
        private Texture2D submitTex;

        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;

        // 3D World-Space VR Tablet UI (Meta Quest 3)
        private GameObject vrTabletGO;
        private Text vrTabletSubtitle;
        private List<VRClickableButton> valButtons = new List<VRClickableButton>();
        private List<VRClickableButton> aroButtons = new List<VRClickableButton>();
        private List<VRClickableButton> mentalButtons = new List<VRClickableButton>();
        private List<VRClickableButton> physButtons = new List<VRClickableButton>();
        private List<VRClickableButton> frustButtons = new List<VRClickableButton>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildProceduralTextures();
        }

        private void BuildProceduralTextures()
        {
            panelBackground = CreateSolidTex(new Color(0.02f, 0.08f, 0.12f, 0.94f));
            buttonNormalTex = CreateSolidTex(new Color(0.08f, 0.22f, 0.32f, 0.85f));
            buttonActiveTex = CreateSolidTex(new Color(0.2f, 0.75f, 0.9f, 0.95f));
            submitTex = CreateSolidTex(new Color(0.18f, 0.75f, 0.45f, 0.95f));
        }

        private Texture2D CreateSolidTex(Color col)
        {
            Texture2D tex = new Texture2D(2, 2);
            Color[] cols = new Color[] { col, col, col, col };
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private void Update()
        {
            if (isQuestionnaireActive)
            {
                // Quick submission via Enter, Escape, or F6 key
                bool submitOrClose = false;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || 
                                                 Keyboard.current.numpadEnterKey.wasPressedThisFrame || 
                                                 Keyboard.current.escapeKey.wasPressedThisFrame || 
                                                 Keyboard.current.f6Key.wasPressedThisFrame))
                {
                    submitOrClose = true;
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                try 
                { 
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || 
                        Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F6)) 
                    {
                        submitOrClose = true; 
                    }
                } 
                catch { }
#endif
                if (submitOrClose)
                {
                    SubmitAndResume();
                }
            }
        }

        public void TriggerQuestionnaire(string milestoneTag)
        {
            if (isQuestionnaireActive) return;

            currentMilestoneTag = milestoneTag;
            isQuestionnaireActive = true;

            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (QuestVRRigController.Instance != null && QuestVRRigController.Instance.isVRActive)
            {
                ShowVRTablet();
            }

            Debug.Log($"<color=#38ef7d>[InSitu Questionnaire]</color> Opened assessment for milestone: <b>{milestoneTag}</b>");

            if (ResearchTelemetryExporter.Instance != null)
            {
                ResearchTelemetryExporter.Instance.RecordDataPoint($"SurveyOpened_{milestoneTag}");
            }
        }

        public void SubmitAndResume()
        {
            SaveResponsesToCSV();

            isQuestionnaireActive = false;
            if (vrTabletGO != null) vrTabletGO.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log($"<color=#38ef7d>[InSitu Questionnaire]</color> Submitted: Valence={samValence}, Arousal={samArousal}, Mental={tlxMentalDemand}, Physical={tlxPhysicalDemand}, Frustration={tlxFrustration}. Resuming dive!");

            if (ResearchTelemetryExporter.Instance != null)
            {
                ResearchTelemetryExporter.Instance.RecordDataPoint($"SurveySubmitted_{currentMilestoneTag}_V{samValence}_A{samArousal}");
            }
        }

        private void ShowVRTablet()
        {
            if (vrTabletGO == null) BuildVRTablet();

            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 fwdFlat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
                if (fwdFlat.sqrMagnitude < 0.01f) fwdFlat = cam.transform.forward;
                fwdFlat.Normalize();
                vrTabletGO.transform.position = cam.transform.position + fwdFlat * 1.25f + Vector3.up * -0.05f;
                vrTabletGO.transform.rotation = Quaternion.LookRotation(fwdFlat, Vector3.up);
            }

            if (vrTabletSubtitle != null)
            {
                string pId = ResearchStudyController.Instance != null ? ResearchStudyController.Instance.participantID : "P01";
                vrTabletSubtitle.text = $"Milestone: {currentMilestoneTag}   |   Participant: {pId}   (Point Right Laser + Trigger)";
            }

            RefreshVRButtonSelections();
            vrTabletGO.SetActive(true);
        }

        private void BuildVRTablet()
        {
            vrTabletGO = new GameObject("VR_InSitu_Questionnaire_Tablet");
            Canvas canvas = vrTabletGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rootRt = vrTabletGO.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(760f, 560f);
            vrTabletGO.transform.localScale = Vector3.one * 0.0014f;

            Image bg = vrTabletGO.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.08f, 0.13f, 0.96f);

            Font font = QuestVRRigController.GetSafeUIFont();
            CreateVRLabel(vrTabletGO.transform, "Header", "IN-VR RESEARCH ASSESSMENT (SAM + NASA-TLX)", font, 22, FontStyle.Bold, new Color(0.3f, 0.95f, 1f), new Vector2(0, 245), new Vector2(700, 36), TextAnchor.MiddleCenter);
            vrTabletSubtitle = CreateVRLabel(vrTabletGO.transform, "Sub", "Milestone", font, 14, FontStyle.Normal, Color.white, new Vector2(0, 214), new Vector2(700, 26), TextAnchor.MiddleCenter);

            // Row 1: SAM Valence (1-5)
            CreateVRLabel(vrTabletGO.transform, "L1", "1. Emotional Valence (1=Unpleasant, 5=Serene)", font, 16, FontStyle.Bold, Color.white, new Vector2(-120, 168), new Vector2(460, 26), TextAnchor.MiddleLeft);
            BuildButtonRow(vrTabletGO.transform, 5, 126f, 118f, font, valButtons, v => { samValence = v; RefreshVRButtonSelections(); });

            // Row 2: SAM Arousal (1-5)
            CreateVRLabel(vrTabletGO.transform, "L2", "2. Physiological Arousal (1=Deep Calm, 5=Panic)", font, 16, FontStyle.Bold, Color.white, new Vector2(-120, 72), new Vector2(460, 26), TextAnchor.MiddleLeft);
            BuildButtonRow(vrTabletGO.transform, 5, 30f, 118f, font, aroButtons, v => { samArousal = v; RefreshVRButtonSelections(); });

            // Row 3: NASA Mental Demand (1-10)
            CreateVRLabel(vrTabletGO.transform, "L3", "3. NASA-TLX Mental Demand (1-10)", font, 15, FontStyle.Bold, Color.white, new Vector2(-120, -20), new Vector2(460, 24), TextAnchor.MiddleLeft);
            BuildButtonRow(vrTabletGO.transform, 10, -56f, 58f, font, mentalButtons, v => { tlxMentalDemand = v; RefreshVRButtonSelections(); });

            // Row 4: NASA Physical Demand (1-10)
            CreateVRLabel(vrTabletGO.transform, "L4", "4. NASA-TLX Physical Demand (1-10)", font, 15, FontStyle.Bold, Color.white, new Vector2(-120, -100), new Vector2(460, 24), TextAnchor.MiddleLeft);
            BuildButtonRow(vrTabletGO.transform, 10, -136f, 58f, font, physButtons, v => { tlxPhysicalDemand = v; RefreshVRButtonSelections(); });

            // Row 5: NASA Frustration (1-10)
            CreateVRLabel(vrTabletGO.transform, "L5", "5. NASA-TLX Frustration Level (1-10)", font, 15, FontStyle.Bold, Color.white, new Vector2(-120, -180), new Vector2(460, 24), TextAnchor.MiddleLeft);
            BuildButtonRow(vrTabletGO.transform, 10, -216f, 58f, font, frustButtons, v => { tlxFrustration = v; RefreshVRButtonSelections(); });

            // Submit Button
            CreateVRButton(vrTabletGO.transform, "SubmitBtn", "SUBMIT & RESUME DIVE", font, 18,
                new Vector2(0, -264f), new Vector2(340, 44), new Color(0.15f, 0.72f, 0.42f, 0.96f), SubmitAndResume);
        }

        private void BuildButtonRow(Transform parent, int count, float yPos, float btnWidth, Font font, List<VRClickableButton> cache, System.Action<int> onSelect)
        {
            cache.Clear();
            float spacing = 8f;
            float totalW = count * btnWidth + (count - 1) * spacing;
            float startX = -totalW * 0.5f + btnWidth * 0.5f;

            for (int i = 1; i <= count; i++)
            {
                int captured = i;
                float xPos = startX + (i - 1) * (btnWidth + spacing);
                var btn = CreateVRButton(parent, $"Btn_{count}_{i}", i.ToString(), font, 16,
                    new Vector2(xPos, yPos), new Vector2(btnWidth, 34), new Color(0.08f, 0.24f, 0.36f, 0.92f),
                    () => onSelect(captured));
                cache.Add(btn);
            }
        }

        private VRClickableButton CreateVRButton(Transform parent, string name, string label, Font font, int fontSize, Vector2 pos, Vector2 size, Color bgCol, System.Action action)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = go.AddComponent<Image>();
            img.color = bgCol;

            BoxCollider col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(size.x, size.y, 12f);

            VRClickableButton vrBtn = go.AddComponent<VRClickableButton>();
            vrBtn.backgroundImage = img;
            vrBtn.baseColor = bgCol;
            vrBtn.onClick = action;

            CreateVRLabel(go.transform, "Label", label, font, fontSize, FontStyle.Bold, Color.white, Vector2.zero, size, TextAnchor.MiddleCenter);
            return vrBtn;
        }

        private Text CreateVRLabel(Transform parent, string name, string content, Font font, int fontSize, FontStyle style, Color col, Vector2 pos, Vector2 size, TextAnchor align)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Text txt = go.AddComponent<Text>();
            txt.text = content;
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = col;
            txt.alignment = align;
            return txt;
        }

        private void RefreshVRButtonSelections()
        {
            for (int i = 0; i < valButtons.Count; i++) valButtons[i].SetSelected(i + 1 == samValence);
            for (int i = 0; i < aroButtons.Count; i++) aroButtons[i].SetSelected(i + 1 == samArousal);
            for (int i = 0; i < mentalButtons.Count; i++) mentalButtons[i].SetSelected(i + 1 == tlxMentalDemand);
            for (int i = 0; i < physButtons.Count; i++) physButtons[i].SetSelected(i + 1 == tlxPhysicalDemand);
            for (int i = 0; i < frustButtons.Count; i++) frustButtons[i].SetSelected(i + 1 == tlxFrustration);
        }

        private void SaveResponsesToCSV()
        {
            string baseDir = Application.platform == RuntimePlatform.Android
                ? Application.persistentDataPath
                : Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dirPath = Path.Combine(baseDir, "Logs");
            if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

            string fullPath = Path.Combine(dirPath, "QuestionnaireResponses.csv");
            bool exists = File.Exists(fullPath);

            StringBuilder sb = new StringBuilder();
            if (!exists)
            {
                sb.AppendLine("TimestampSec,DateTime,ParticipantID,Condition,MilestoneTag,SAM_Valence_1to5,SAM_Arousal_1to5,NASA_MentalDemand_1to10,NASA_PhysicalDemand_1to10,NASA_Frustration_1to10");
            }

            string pId = ResearchStudyController.Instance != null ? ResearchStudyController.Instance.participantID : "P01";
            string cond = ResearchStudyController.Instance != null ? ResearchStudyController.Instance.activeCondition.ToString() : "ConditionC";

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            sb.AppendLine(string.Format(inv, "{0:F2},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                Time.time, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), pId, cond, currentMilestoneTag,
                samValence, samArousal, tlxMentalDemand, tlxPhysicalDemand, tlxFrustration));

            File.AppendAllText(fullPath, sb.ToString());
        }

        private void OnGUI()
        {
            if (!isQuestionnaireActive) return;
            if (QuestVRRigController.Instance != null && QuestVRRigController.Instance.isVRActive) return; // Rendered via 3D VR Tablet instead

            InitStyles();

            // Center Modal Panel
            float panelW = 620;
            float panelH = 500;
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = (Screen.height - panelH) * 0.5f;

            Rect panelRect = new Rect(panelX, panelY, panelW, panelH);
            GUI.DrawTexture(panelRect, panelBackground);

            // Sleek border
            DrawBorder(panelRect, new Color(0.25f, 0.75f, 0.95f, 0.85f), 2);

            GUILayout.BeginArea(new Rect(panelX + 24, panelY + 18, panelW - 48, panelH - 36));

            GUILayout.Label("IN-SITU RESEARCH ASSESSMENT", headerStyle);
            GUILayout.Label($"Milestone: {currentMilestoneTag}  |  Participant: {(ResearchStudyController.Instance != null ? ResearchStudyController.Instance.participantID : "P01")}", promptStyle);
            GUILayout.Space(12);

            // 1. SAM Valence (1 to 5)
            GUILayout.Label("1. Emotional Valence (How pleasant or positive did you feel?)", promptStyle);
            GUILayout.BeginHorizontal();
            string[] valLabels = { "1 (Very Unpleasant)", "2 (Unpleasant)", "3 (Neutral)", "4 (Pleasant)", "5 (Very Serene)" };
            for (int i = 1; i <= 5; i++)
            {
                bool active = samValence == i;
                if (GUILayout.Button(valLabels[i - 1], active ? activeButtonStyle : buttonStyle, GUILayout.Height(30)))
                {
                    samValence = i;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            // 2. SAM Arousal (1 to 5)
            GUILayout.Label("2. Physiological Arousal (How agitated, excited, or relaxed were you?)", promptStyle);
            GUILayout.BeginHorizontal();
            string[] arousLabels = { "1 (Deeply Calm)", "2 (Calm)", "3 (Alert)", "4 (Elevated)", "5 (Intense Stress)" };
            for (int i = 1; i <= 5; i++)
            {
                bool active = samArousal == i;
                if (GUILayout.Button(arousLabels[i - 1], active ? activeButtonStyle : buttonStyle, GUILayout.Height(30)))
                {
                    samArousal = i;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(12);

            // 3. NASA-TLX Mental Demand (1 to 10)
            GUILayout.Label($"3. NASA-TLX: Mental Demand ({tlxMentalDemand}/10 - Cognitive effort required)", promptStyle);
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 10; i++)
            {
                bool active = tlxMentalDemand == i;
                if (GUILayout.Button(i.ToString(), active ? activeButtonStyle : buttonStyle, GUILayout.Height(28)))
                {
                    tlxMentalDemand = i;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            // 4. NASA-TLX Physical Demand (1 to 10)
            GUILayout.Label($"4. NASA-TLX: Physical / Locomotion Demand ({tlxPhysicalDemand}/10 - Effort to swim/navigate)", promptStyle);
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 10; i++)
            {
                bool active = tlxPhysicalDemand == i;
                if (GUILayout.Button(i.ToString(), active ? activeButtonStyle : buttonStyle, GUILayout.Height(28)))
                {
                    tlxPhysicalDemand = i;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            // 5. NASA-TLX Frustration (1 to 10)
            GUILayout.Label($"5. NASA-TLX: Frustration Level ({tlxFrustration}/10 - Insecure, stressed, or annoyed)", promptStyle);
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 10; i++)
            {
                bool active = tlxFrustration == i;
                if (GUILayout.Button(i.ToString(), active ? activeButtonStyle : buttonStyle, GUILayout.Height(28)))
                {
                    tlxFrustration = i;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(16);

            // Submit Button
            if (GUILayout.Button("✔  SUBMIT & RESUME DIVE [Enter]", submitStyle, GUILayout.Height(40)))
            {
                SubmitAndResume();
            }

            GUILayout.EndArea();
        }

        private void InitStyles()
        {
            if (headerStyle != null) return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = new Color(0.3f, 0.95f, 1f);

            promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal
            };
            promptStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            buttonStyle.normal.background = buttonNormalTex;
            buttonStyle.normal.textColor = new Color(0.8f, 0.9f, 1f);

            activeButtonStyle = new GUIStyle(buttonStyle);
            activeButtonStyle.normal.background = buttonActiveTex;
            activeButtonStyle.normal.textColor = new Color(0.01f, 0.08f, 0.15f);

            submitStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            submitStyle.normal.background = submitTex;
            submitStyle.normal.textColor = Color.white;
        }

        private void DrawBorder(Rect r, Color color, int thickness)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}

