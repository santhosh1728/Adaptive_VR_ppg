using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Pandoran Oceanic Glide Turtle (Metkayina Reef Creature).
    /// Drifts peacefully along the coral reef and volcanic craters.
    /// Features:
    /// - Detailed domed carapace with bioluminescent margin lines
    /// - Contoured head and neck that turns towards the diver
    /// - 4 flapping flippers with organic swimming stroke cycles
    /// - Responds to heart rate: gently circles the diver when the diver is Calm / Flow.
    /// </summary>
    public class PandoranSeaTurtleAI : MonoBehaviour
    {
        [Header("Flight Dynamics")]
        public float swimSpeed = 2.4f;
        public float turnSpeed = 1.8f;
        public float flipperCycleSpeed = 1.6f;
        public Color shellGlowColor = new Color(0.1f, 0.95f, 0.75f); // Bio Emerald / Turquoise

        private Transform frontLeftFlipper;
        private Transform frontRightFlipper;
        private Transform rearLeftFlipper;
        private Transform rearRightFlipper;
        private Transform turtleHead;

        private DiverController diver;
        private Vector3 swimTarget;
        private float strokePhase;
        private float wanderTimer = 0f;

        public void InitializeTurtle(Vector3 spawnPos)
        {
            transform.position = spawnPos;
            swimTarget = spawnPos + UnityEngine.Random.insideUnitSphere * 15f;
            swimTarget.y = Mathf.Clamp(swimTarget.y, -20f, -6f);

            diver = FindAnyObjectByType<DiverController>();
            BuildTurtleModel();
        }

        private void BuildTurtleModel()
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material shellMat = new Material(uLit);
            shellMat.color = new Color(0.08f, 0.16f, 0.18f); // Deep oceanic obsidian carapace
            shellMat.SetFloat("_Smoothness", 0.8f);

            Material bioTrimMat = new Material(uLit);
            bioTrimMat.color = shellGlowColor;
            bioTrimMat.EnableKeyword("_EMISSION");
            bioTrimMat.SetColor("_EmissionColor", shellGlowColor * 2.5f);

            Material fleshMat = new Material(uLit);
            fleshMat.color = new Color(0.12f, 0.45f, 0.55f); // Cerulean reptile flesh
            fleshMat.SetFloat("_Smoothness", 0.6f);

            // 1. Domed Shell Carapace
            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = "TurtleCarapace";
            shell.transform.SetParent(transform, false);
            shell.transform.localScale = new Vector3(1.8f, 0.75f, 2.4f);
            shell.GetComponent<Renderer>().sharedMaterial = shellMat;
            RemoveCollider(shell);

            // Bioluminescent shell margin trim
            GameObject shellTrim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shellTrim.name = "ShellBioTrim";
            shellTrim.transform.SetParent(shell.transform, false);
            shellTrim.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            shellTrim.transform.localScale = new Vector3(1.08f, 0.08f, 1.08f);
            shellTrim.GetComponent<Renderer>().sharedMaterial = bioTrimMat;
            RemoveCollider(shellTrim);

            // 2. Head and Neck
            GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            neck.name = "Neck";
            neck.transform.SetParent(transform, false);
            neck.transform.localPosition = new Vector3(0f, 0.1f, 1.4f);
            neck.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            neck.transform.localScale = new Vector3(0.35f, 0.5f, 0.35f);
            neck.GetComponent<Renderer>().sharedMaterial = fleshMat;
            RemoveCollider(neck);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(neck.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            head.transform.localScale = new Vector3(1.3f, 1.2f, 1.6f);
            head.GetComponent<Renderer>().sharedMaterial = fleshMat;
            turtleHead = head.transform;
            RemoveCollider(head);

            // Bioluminescent Eyes
            CreateEye(head.transform, bioTrimMat, new Vector3(-0.45f, 0.2f, 0.35f));
            CreateEye(head.transform, bioTrimMat, new Vector3(0.45f, 0.2f, 0.35f));

            // 3. Front Swept Flippers (Main Propulsion Hydrofoils)
            frontLeftFlipper = CreateFlipper("FrontLeftFlipper", transform, new Vector3(-0.95f, -0.1f, 0.8f), new Vector3(2.2f, 0.08f, 0.75f), fleshMat, bioTrimMat);
            frontRightFlipper = CreateFlipper("FrontRightFlipper", transform, new Vector3(0.95f, -0.1f, 0.8f), new Vector3(2.2f, 0.08f, 0.75f), fleshMat, bioTrimMat);

            // 4. Rear Rudders
            rearLeftFlipper = CreateFlipper("RearLeftFlipper", transform, new Vector3(-0.75f, -0.1f, -1.1f), new Vector3(1.1f, 0.06f, 0.55f), fleshMat, bioTrimMat);
            rearRightFlipper = CreateFlipper("RearRightFlipper", transform, new Vector3(0.75f, -0.1f, -1.1f), new Vector3(1.1f, 0.06f, 0.55f), fleshMat, bioTrimMat);

            // 5. Tail
            GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tail.transform.SetParent(transform, false);
            tail.transform.localPosition = new Vector3(0f, -0.15f, -1.5f);
            tail.transform.localRotation = Quaternion.Euler(-75f, 0f, 0f);
            tail.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
            tail.GetComponent<Renderer>().sharedMaterial = fleshMat;
            RemoveCollider(tail);
        }

        private void CreateEye(Transform parent, Material mat, Vector3 pos)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.transform.SetParent(parent, false);
            eye.transform.localPosition = pos;
            eye.transform.localScale = Vector3.one * 0.25f;
            eye.GetComponent<Renderer>().sharedMaterial = mat;
            RemoveCollider(eye);
        }

        private Transform CreateFlipper(string name, Transform parent, Vector3 pos, Vector3 scale, Material fleshMat, Material trimMat)
        {
            GameObject pivot = new GameObject(name + "Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = pos;

            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.transform.SetParent(pivot.transform, false);
            blade.transform.localPosition = new Vector3(pos.x < 0 ? -scale.x * 0.45f : scale.x * 0.45f, 0f, 0f);
            blade.transform.localScale = scale;
            blade.GetComponent<Renderer>().sharedMaterial = fleshMat;
            RemoveCollider(blade);

            // Glowing edge
            GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.transform.SetParent(blade.transform, false);
            edge.transform.localPosition = new Vector3(0f, 0f, 0.48f);
            edge.transform.localScale = new Vector3(1.02f, 1.1f, 0.15f);
            edge.GetComponent<Renderer>().sharedMaterial = trimMat;
            RemoveCollider(edge);

            return pivot.transform;
        }

        private void Update()
        {
            if (diver == null) diver = FindAnyObjectByType<DiverController>();

            wanderTimer -= Time.deltaTime;
            if (wanderTimer <= 0f)
            {
                wanderTimer = UnityEngine.Random.Range(8f, 16f);
                PickNewCruiseTarget();
            }

            // Check diver heart rate: if calm, turtle escorts the diver
            bool diverCalm = HeartRateSystem.Instance != null && HeartRateSystem.Instance.isCalm;
            if (diver != null && diverCalm && Vector3.Distance(transform.position, diver.transform.position) < 20f)
            {
                swimTarget = diver.transform.position + new Vector3(Mathf.Sin(Time.time * 0.4f) * 6f, 1.2f, Mathf.Cos(Time.time * 0.4f) * 6f);
            }

            // Move towards target smoothly
            Vector3 targetDir = (swimTarget - transform.position).normalized;
            if (targetDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(targetDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * turnSpeed);
            }

            transform.position += transform.forward * swimSpeed * Time.deltaTime;

            // Animate graceful flipper strokes
            strokePhase += Time.deltaTime * flipperCycleSpeed;
            float wingAngle = Mathf.Sin(strokePhase) * 28f;
            float wingRoll = Mathf.Cos(strokePhase) * 14f;

            if (frontLeftFlipper != null) frontLeftFlipper.localRotation = Quaternion.Euler(wingRoll, 0f, wingAngle);
            if (frontRightFlipper != null) frontRightFlipper.localRotation = Quaternion.Euler(-wingRoll, 0f, -wingAngle);

            if (rearLeftFlipper != null) rearLeftFlipper.localRotation = Quaternion.Euler(0f, Mathf.Sin(strokePhase * 0.8f) * 12f, 0f);
            if (rearRightFlipper != null) rearRightFlipper.localRotation = Quaternion.Euler(0f, -Mathf.Sin(strokePhase * 0.8f) * 12f, 0f);
        }

        private void PickNewCruiseTarget()
        {
            float radius = ReefWorldGenerator.Instance != null ? ReefWorldGenerator.Instance.worldRadius * 0.7f : 35f;
            float seabedY = ReefWorldGenerator.Instance != null ? ReefWorldGenerator.Instance.seaBedDepth : -22f;

            Vector2 circle = UnityEngine.Random.insideUnitCircle * radius;
            swimTarget = new Vector3(circle.x, UnityEngine.Random.Range(seabedY + 3f, -4f), circle.y);
        }

        private void RemoveCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }
    }
}

