using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// The Pandoran "Skimwing" (Tsurak) Marine & Aerial Predator Mount.
    /// In Avatar: The Way of Water, the Skimwing is the mount of Metkayina warriors:
    /// - High-speed predatory fish capable of 20 m/s underwater flight
    /// - Surface Breaching: Thrusts through the surface, leaping into the air to glide across the wave tops at 25 m/s!
    /// - Press 'E' to mount & dismount via neural Tsaheylu bond
    /// </summary>
    public class SkimwingMountAI : ReefInteractable
    {
        public static SkimwingMountAI Instance { get; private set; }

        [Header("Flight Dynamics")]
        public float swimSpeed = 19.5f;
        public float airGlideSpeed = 26.0f;
        public float turnSpeed = 6.0f;

        [Header("Mount State")]
        public bool isMounted = false;
        public bool isAirGliding = false;
        public Transform mountSeat;

        private Transform leftWing;
        private Transform rightWing;
        private Transform caudalTail;

        private DiverController diver;
        private float wingPhase = 0f;
        private float glideTimer = 0f;
        private float currentRollAngle = 0f;

        private void Awake()
        {
            Instance = this;
            interactableType = InteractableType.Beacon;
            interactionPrompt = "[E] Tsaheylu: Mount & Ride Pandoran Skimwing (Tsurak)";
            canInteract = true;
        }

        public void InitializeSkimwing(Vector3 spawnPos)
        {
            transform.position = spawnPos;
            diver = FindAnyObjectByType<DiverController>();

            BuildSkimwingModel();
        }

        private void BuildSkimwingModel()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material scalesMat = new Material(uLit);
            scalesMat.color = new Color(0.04f, 0.18f, 0.22f); // Dark teal armored scales
            scalesMat.SetFloat("_Smoothness", 0.9f);

            Material orangeBioMat = new Material(uLit);
            Color bioOrange = new Color(1f, 0.42f, 0.08f); // Metkayina warrior orange bioluminescence
            orangeBioMat.color = bioOrange;
            orangeBioMat.EnableKeyword("_EMISSION");
            orangeBioMat.SetColor("_EmissionColor", bioOrange * 3.2f);

            // 1. Long Sleek Hydrodynamic Fuselage
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "SkimwingBody";
            body.transform.SetParent(transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(1.1f, 4.6f, 0.75f);
            body.GetComponent<Renderer>().sharedMaterial = scalesMat;
            RemoveCollider(body);

            // 2. Needle Snout / Jaws
            GameObject snout = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            snout.name = "NeedleSnout";
            snout.transform.SetParent(transform, false);
            snout.transform.localPosition = new Vector3(0f, 0.1f, 3.2f);
            snout.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            snout.transform.localScale = new Vector3(0.35f, 1.8f, 0.25f);
            snout.GetComponent<Renderer>().sharedMaterial = scalesMat;
            RemoveCollider(snout);

            // 3. Saddle / Rider Seat
            GameObject seat = new GameObject("SkimwingMountSeat");
            seat.transform.SetParent(transform, false);
            seat.transform.localPosition = new Vector3(0f, 0.52f, 0.1f);
            mountSeat = seat.transform;

            // 4. Large Hydrofoil Wings
            leftWing = CreateHydrofoilWing("LeftWing", new Vector3(-0.55f, 0.15f, 0.8f), true, scalesMat, orangeBioMat);
            rightWing = CreateHydrofoilWing("RightWing", new Vector3(0.55f, 0.15f, 0.8f), false, scalesMat, orangeBioMat);

            // 5. Caudal Tail Fin
            GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tail.name = "CaudalTail";
            tail.transform.SetParent(transform, false);
            tail.transform.localPosition = new Vector3(0f, 0.35f, -3.2f);
            tail.transform.localScale = new Vector3(0.08f, 1.8f, 1.2f);
            tail.GetComponent<Renderer>().sharedMaterial = orangeBioMat;
            RemoveCollider(tail);
            caudalTail = tail.transform;
        }

        private Transform CreateHydrofoilWing(string name, Vector3 pos, bool isLeft, Material bodyMat, Material bioMat)
        {
            GameObject wingRoot = new GameObject(name);
            wingRoot.transform.SetParent(transform, false);
            wingRoot.transform.localPosition = pos;

            float dir = isLeft ? -1f : 1f;

            GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.name = "WingPlate";
            wing.transform.SetParent(wingRoot.transform, false);
            wing.transform.localPosition = new Vector3(dir * 2.2f, 0f, -0.4f);
            wing.transform.localRotation = Quaternion.Euler(0f, dir * 25f, -dir * 10f);
            wing.transform.localScale = new Vector3(4.5f, 0.08f, 1.4f);
            wing.GetComponent<Renderer>().sharedMaterial = bodyMat;
            RemoveCollider(wing);

            // Glowing edge
            GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = "GlowEdge";
            edge.transform.SetParent(wing.transform, false);
            edge.transform.localPosition = new Vector3(0f, 0f, 0.45f);
            edge.transform.localScale = new Vector3(1.02f, 1.2f, 0.15f);
            edge.GetComponent<Renderer>().sharedMaterial = bioMat;
            RemoveCollider(edge);

            return wingRoot.transform;
        }

        public override void Interact(DiverController diver)
        {
            if (isMounted)
            {
                Dismount();
            }
            else
            {
                Mount(diver);
            }
        }

        public void Mount(DiverController diverTarget)
        {
            diver = diverTarget;
            isMounted = true;
            interactionPrompt = "[E] Dismount Skimwing (Tsurak)";

            if (diver != null)
            {
                diver.StartRidingMount(mountSeat, null);
            }

            Debug.Log("<color=#ff6600>[Skimwing Tsaheylu]</color> Mounted the warrior Skimwing!");
        }

        public void Dismount()
        {
            if (!isMounted) return;
            isMounted = false;
            interactionPrompt = "[E] Tsaheylu: Mount & Ride Pandoran Skimwing (Tsurak)";

            if (diver != null)
            {
                diver.StopRidingMount();
            }

            Debug.Log("<color=#ff6600>[Skimwing Tsaheylu]</color> Dismounted Skimwing.");
        }

        private void Update()
        {
            if (diver == null) diver = FindAnyObjectByType<DiverController>();

            if (isMounted)
            {
                HandleMountedFlight();
            }
            else
            {
                // Autonomous glide near surface
                float angle = Time.time * 0.18f;
                Vector3 target = new Vector3(Mathf.Sin(angle) * 35f, -4f, Mathf.Cos(angle) * 35f);
                Vector3 dir = (target - transform.position);
                if (dir.sqrMagnitude > 0.1f)
                {
                    transform.position += dir.normalized * 5.5f * Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 3f);
                }
            }

            AnimateWings();
        }

        private void HandleMountedFlight()
        {
            if (diver == null) { Dismount(); return; }

            Vector3 input = diver.GetMovementInput();
            float pitch = diver.GetAimPitch();
            float yaw = diver.GetAimYaw();

            // Check surface breach: gliding across air
            bool isAboveSurface = transform.position.y >= 0.05f;
            if (isAboveSurface)
            {
                if (!isAirGliding)
                {
                    isAirGliding = true;
                    glideTimer = 5.0f;
                    if (UnderwaterAudio.Instance != null)
                    {
                        UnderwaterAudio.Instance.PlayHarmonicTone(440f, 0.4f);
                    }
                }

                glideTimer -= Time.deltaTime;
                // Gently descend back to water if glide timer expires or pitch down
                if (glideTimer <= 0f && input.y >= 0f) input.y = -0.5f;
            }
            else
            {
                isAirGliding = false;
            }

            // Flight propulsion (boosted by Tsaheylu Bio-Bonding Index)
            float bondMultiplier = (HRVEngine.Instance != null && HRVEngine.Instance.tsaheyluBondIndex >= 80f) ? 1.35f : 1.0f;
            float currentSpeed = (isAirGliding ? airGlideSpeed : ((input.z > 0.1f) ? swimSpeed : swimSpeed * 0.5f)) * bondMultiplier;

            // Banking roll
            float targetRoll = -input.x * 38f;
            currentRollAngle = Mathf.Lerp(currentRollAngle, targetRoll, Time.deltaTime * 8f);

            Quaternion targetRot = Quaternion.Euler(pitch * 0.8f, yaw, currentRollAngle);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * turnSpeed);

            Vector3 moveDir = (transform.forward * Mathf.Max(0.25f, input.z)) + (transform.right * input.x * 0.45f) + (Vector3.up * input.y * 0.65f);
            transform.position += moveDir * currentSpeed * Time.deltaTime;

            // Clamps: allows soaring up to +8m above water when air-gliding!
            float maxY = isAirGliding ? 8.5f : 0.05f;
            float localSeabedY = ReefWorldGenerator.Instance != null ? ReefWorldGenerator.Instance.GetSeabedHeight(transform.position.x, transform.position.z) : -22f;
            Vector3 pos = transform.position;
            pos.y = Mathf.Clamp(pos.y, localSeabedY + 1.2f, maxY);
            transform.position = pos;

            // Refill oxygen while riding
            if (HeartRateSystem.Instance != null)
            {
                HeartRateSystem.Instance.currentOxygen = Mathf.MoveTowards(
                    HeartRateSystem.Instance.currentOxygen,
                    HeartRateSystem.Instance.maxOxygen,
                    12f * Time.deltaTime
                );
            }
        }

        private void AnimateWings()
        {
            wingPhase += Time.deltaTime * (isAirGliding ? 2.5f : 8.5f);
            float flap = isAirGliding ? Mathf.Sin(wingPhase) * 6f : Mathf.Sin(wingPhase) * 24f;

            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0f, 0f, -flap);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0f, 0f, flap);
            if (caudalTail != null) caudalTail.localRotation = Quaternion.Euler(0f, Mathf.Sin(wingPhase * 1.5f) * 18f, 0f);
        }

        private void RemoveCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }
    }
}

