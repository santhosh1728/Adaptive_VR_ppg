using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// The Pandoran "Ilu" Marine Companion & Rideable Mount (Metkayina Sacred Creature).
    /// Features:
    /// - 4 undulating bioluminescent hydrofoil wings with trailing margins
    /// - Full Tsaheylu Ride System: Swim up and press 'E' to mount the Ilu and soar at 2.5x speed!
    /// - Banking flight physics, agile 3D turns, and symbiotic gill breathing (freezes oxygen drain).
    /// - Escorts and drafts ahead of the diver when unmounted in Calm/Flow states.
    /// </summary>
    public class MantaCompanionAI : ReefInteractable
    {
        public static MantaCompanionAI Instance { get; private set; }

        [Header("Ilu Flight Dynamics")]
        public float cruiseGlideSpeed = 5.2f;
        public float rideSpeed = 16.5f;
        public float turnSpeed = 3.2f;
        public float rideTurnSpeed = 5.5f;
        public float wingFlapFrequency = 1.4f;

        [Header("Bioluminescence")]
        public Color primaryBioluminescence = new Color(0.1f, 0.95f, 0.9f); // Electric Cyan
        public Color secondaryBioColor = new Color(0.9f, 0.2f, 0.85f);     // Luminous Magenta

        [Header("Mount State")]
        public bool isMounted = false;
        public Transform mountSeat;

        private Transform frontLeftWing;
        private Transform frontRightWing;
        private Transform rearLeftWing;
        private Transform rearRightWing;
        private Transform tailRibbon;

        private DiverController diver;
        private float flapPhase;
        private Vector3 targetCruisePos;
        private float currentRollAngle = 0f;

        private void Awake()
        {
            Instance = this;
            interactableType = InteractableType.Beacon;
            interactionPrompt = "[E] Tsaheylu: Mount & Ride Pandoran Ilu";
            canInteract = true;
        }

        public void InitializeManta(Vector3 startPos)
        {
            transform.position = startPos;
            targetCruisePos = startPos;
            diver = FindAnyObjectByType<DiverController>();

            BuildIluModel();
        }

        private void BuildIluModel()
        {
            // Clear previous visual children if rebuilding
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material hideMat = new Material(uLit);
            hideMat.color = new Color(0.06f, 0.12f, 0.22f);
            hideMat.SetFloat("_Smoothness", 0.9f);

            Material cyanBioMat = new Material(uLit);
            cyanBioMat.color = primaryBioluminescence;
            cyanBioMat.EnableKeyword("_EMISSION");
            cyanBioMat.SetColor("_EmissionColor", primaryBioluminescence * 2.8f);

            Material magentaBioMat = new Material(uLit);
            magentaBioMat.color = secondaryBioColor;
            magentaBioMat.EnableKeyword("_EMISSION");
            magentaBioMat.SetColor("_EmissionColor", secondaryBioColor * 2.5f);

            // 1. Ilu Torso / Sleek Aquatic Body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "IluBody";
            body.transform.SetParent(transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(1.4f, 3.4f, 0.8f);
            body.GetComponent<Renderer>().sharedMaterial = hideMat;
            RemoveCollider(body);

            // 2. Saddle / Mount Seat Pivot (Diver rests on dorsal back)
            GameObject seat = new GameObject("MountSeat");
            seat.transform.SetParent(transform, false);
            seat.transform.localPosition = new Vector3(0f, 0.55f, 0.25f);
            mountSeat = seat.transform;

            // 3. Dorsal Neural Ridge (Glowing whip connector for Tsaheylu)
            GameObject ridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ridge.name = "NeuralRidge";
            ridge.transform.SetParent(transform, false);
            ridge.transform.localPosition = new Vector3(0f, 0.5f, 0.2f);
            ridge.transform.localScale = new Vector3(0.12f, 0.35f, 2.6f);
            ridge.GetComponent<Renderer>().sharedMaterial = cyanBioMat;
            RemoveCollider(ridge);

            // 4. Front Left & Right Pectoral Wings
            frontLeftWing = CreateWing("FrontLeftWing", transform, new Vector3(-0.9f, 0f, 0.6f), new Vector3(2.8f, 0.08f, 1.8f), cyanBioMat);
            frontRightWing = CreateWing("FrontRightWing", transform, new Vector3(0.9f, 0f, 0.6f), new Vector3(2.8f, 0.08f, 1.8f), cyanBioMat);

            // 5. Rear Left & Right Pelvic Wings
            rearLeftWing = CreateWing("RearLeftWing", transform, new Vector3(-0.7f, 0f, -1.2f), new Vector3(1.8f, 0.07f, 1.2f), magentaBioMat);
            rearRightWing = CreateWing("RearRightWing", transform, new Vector3(0.7f, 0f, -1.2f), new Vector3(1.8f, 0.07f, 1.2f), magentaBioMat);

            // 6. Iridescent Tail Ribbons
            GameObject tailGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tailGO.name = "TailRibbon";
            tailGO.transform.SetParent(transform, false);
            tailGO.transform.localPosition = new Vector3(0f, 0f, -3.2f);
            tailGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tailGO.transform.localScale = new Vector3(0.08f, 2.4f, 0.08f);
            tailGO.GetComponent<Renderer>().sharedMaterial = magentaBioMat;
            tailRibbon = tailGO.transform;
            RemoveCollider(tailGO);

            // 7. Luminous Aura
            Light aura = gameObject.AddComponent<Light>();
            aura.type = LightType.Point;
            aura.color = primaryBioluminescence;
            aura.range = 16f;
            aura.intensity = 2.4f;

            // 8. Interaction Trigger Collider
            SphereCollider sc = GetComponent<SphereCollider>();
            if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 4.2f;
        }

        private Transform CreateWing(string name, Transform parent, Vector3 pivotPos, Vector3 wingScale, Material mat)
        {
            GameObject pivot = new GameObject(name + "Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = pivotPos;

            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.transform.SetParent(pivot.transform, false);
            blade.transform.localPosition = new Vector3(pivotPos.x > 0 ? wingScale.x * 0.5f : -wingScale.x * 0.5f, 0f, 0f);
            blade.transform.localScale = wingScale;
            blade.GetComponent<Renderer>().sharedMaterial = mat;
            RemoveCollider(blade);

            return pivot.transform;
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
                UpdateAutonomousEscort();
                ApplyDraftingSlipstream();
            }

            AnimateFourWings(isMounted ? 2.2f : 1.0f);
        }

        public override void Interact(DiverController playerDiver)
        {
            if (playerDiver == null) return;

            if (!isMounted)
            {
                Mount(playerDiver);
            }
            else
            {
                Dismount();
            }
        }

        public void Mount(DiverController playerDiver)
        {
            isMounted = true;
            diver = playerDiver;
            interactionPrompt = "[E] Dismount Ilu";

            diver.StartRidingMount(mountSeat, this);

            if (UnderwaterAudio.Instance != null)
            {
                UnderwaterAudio.Instance.PlayBeaconChime();
            }

            Debug.Log("<color=#00f2fe>[Tsaheylu Bond]</color> Neural link established! Riding Pandoran Ilu.");
        }

        public void Dismount()
        {
            if (!isMounted) return;
            isMounted = false;
            interactionPrompt = "[E] Tsaheylu: Mount & Ride Pandoran Ilu";

            if (diver != null)
            {
                diver.StopRidingMount();
            }

            Debug.Log("<color=#00f2fe>[Tsaheylu Bond]</color> Dismounted from Pandoran Ilu.");
        }

        private void HandleMountedFlight()
        {
            if (diver == null)
            {
                Dismount();
                return;
            }

            Vector3 input = diver.GetMovementInput();
            float targetPitch = diver.GetAimPitch();
            float targetYaw = diver.GetAimYaw();

            // Roll into turns
            float targetRoll = -input.x * 32f;
            currentRollAngle = Mathf.Lerp(currentRollAngle, targetRoll, Time.deltaTime * 6f);

            Quaternion targetRotation = Quaternion.Euler(targetPitch * 0.75f, targetYaw, currentRollAngle);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rideTurnSpeed);

            // Flight propulsion (boosted by Tsaheylu Bio-Bonding Index)
            float bondMultiplier = (HRVEngine.Instance != null && HRVEngine.Instance.tsaheyluBondIndex >= 80f) ? 1.45f : 1.0f;
            float currentSpeed = ((input.z > 0.1f) ? rideSpeed : (input.z < -0.1f ? rideSpeed * 0.4f : rideSpeed * 0.7f)) * bondMultiplier;
            Vector3 flightDir = (transform.forward * Mathf.Max(0.2f, input.z)) + (transform.right * input.x * 0.4f) + (Vector3.up * input.y * 0.6f);

            transform.position += flightDir * currentSpeed * Time.deltaTime;

            // Clamps inside world
            float worldR = ReefWorldGenerator.Instance != null ? ReefWorldGenerator.Instance.worldRadius - 2f : 46f;
            Vector3 pos = transform.position;
            float localSeabedY = ReefWorldGenerator.Instance != null ? ReefWorldGenerator.Instance.GetSeabedHeight(pos.x, pos.z) : -22f;
            pos.y = Mathf.Clamp(pos.y, localSeabedY + 1.5f, 0.05f);
            Vector2 xz = Vector2.ClampMagnitude(new Vector2(pos.x, pos.z), worldR);
            transform.position = new Vector3(xz.x, pos.y, xz.y);

            // Symbiotic breathing: refills oxygen while riding
            if (HeartRateSystem.Instance != null)
            {
                HeartRateSystem.Instance.currentOxygen = Mathf.MoveTowards(
                    HeartRateSystem.Instance.currentOxygen,
                    HeartRateSystem.Instance.maxOxygen,
                    8.0f * Time.deltaTime
                );
            }
        }

        private void UpdateAutonomousEscort()
        {
            HRVEngine hrv = HRVEngine.Instance;
            bool isDiverCalm = hrv != null && (hrv.currentState == AffectiveState.Calm || hrv.currentState == AffectiveState.Flow);

            Vector3 destination = targetCruisePos;

            if (diver != null && isDiverCalm)
            {
                // Escort diver smoothly in front and above
                destination = diver.transform.position + (diver.transform.forward * 4.2f) + (Vector3.up * 1.5f);
            }
            else
            {
                // Cruise peaceful Pandoran reef circuit
                float angle = Time.time * 0.12f;
                destination = new Vector3(Mathf.Sin(angle) * 24f, -7f, Mathf.Cos(angle) * 24f);
            }

            Vector3 moveDir = (destination - transform.position);
            if (moveDir.sqrMagnitude > 0.1f)
            {
                transform.position += moveDir.normalized * cruiseGlideSpeed * Time.deltaTime;
                Quaternion lookRot = Quaternion.LookRotation(moveDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * turnSpeed);
            }
        }

        private void AnimateFourWings(float speedMultiplier)
        {
            flapPhase += Time.deltaTime * wingFlapFrequency * 2.8f * speedMultiplier;
            float frontAngle = Mathf.Sin(flapPhase) * (isMounted ? 28f : 20f);
            float rearAngle = Mathf.Sin(flapPhase - 0.6f) * (isMounted ? 22f : 16f);

            if (frontLeftWing != null) frontLeftWing.localRotation = Quaternion.Euler(0f, 0f, -frontAngle);
            if (frontRightWing != null) frontRightWing.localRotation = Quaternion.Euler(0f, 0f, frontAngle);

            if (rearLeftWing != null) rearLeftWing.localRotation = Quaternion.Euler(0f, 0f, -rearAngle);
            if (rearRightWing != null) rearRightWing.localRotation = Quaternion.Euler(0f, 0f, rearAngle);

            if (tailRibbon != null) tailRibbon.localRotation = Quaternion.Euler(90f + Mathf.Sin(flapPhase) * 10f, 0f, 0f);
        }

        private void ApplyDraftingSlipstream()
        {
            if (diver == null) return;

            HRVEngine hrv = HRVEngine.Instance;
            float bondIndex = hrv != null ? hrv.tsaheyluBondIndex : 70f;
            float maxDraftDist = (bondIndex >= 65f) ? 8.5f : 6.8f;

            float dist = Vector3.Distance(transform.position, diver.transform.position);
            if (dist < maxDraftDist)
            {
                Rigidbody rb = diver.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    float force = (bondIndex >= 80f) ? 7.0f : 4.5f;
                    rb.AddForce(transform.forward * force, ForceMode.Acceleration);
                }

                if (HeartRateSystem.Instance != null)
                {
                    float o2RefillRate = (bondIndex >= 65f) ? 5.5f : 3.0f;
                    HeartRateSystem.Instance.currentOxygen = Mathf.MoveTowards(
                        HeartRateSystem.Instance.currentOxygen,
                        HeartRateSystem.Instance.maxOxygen,
                        o2RefillRate * Time.deltaTime
                    );
                }
            }
        }

        private void RemoveCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }
    }
}
