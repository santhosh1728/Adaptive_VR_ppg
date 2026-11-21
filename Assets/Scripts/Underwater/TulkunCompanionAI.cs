using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// The Pandoran "Tulkun" Oceanic Cetacean / Spirit Whale.
    /// In Avatar: The Way of Water, Tulkun are highly intelligent spiritual brethren of the Metkayina.
    /// Features:
    /// - 35-meter colossal body with armored cranial plates, 4 giant hydrofoil flippers, and twin fluke tails
    /// - Glides gracefully through the Hadal Deep Trench in a serene circuit
    /// - Emits resonant low-frequency acoustic whale songs that vibrate the ocean
    /// - Symbiotic Draft Slipstream: Swimming alongside the Tulkun gives +80% speed boost and replenishes oxygen
    /// </summary>
    public class TulkunCompanionAI : MonoBehaviour
    {
        [Header("Tulkun Dynamics")]
        public float cruiseSpeed = 6.5f;
        public float turnSpeed = 1.2f;
        public float songInterval = 9.0f;

        private Transform leftFrontPectoral;
        private Transform rightFrontPectoral;
        private Transform leftRearPectoral;
        private Transform rightRearPectoral;
        private Transform flukeTail;

        private DiverController diver;
        private float flipperPhase = 0f;
        private float songTimer = 4.0f;
        private float circuitAngle = 0f;

        // Trench circuit center: South-West trench at (-34, -42, -32)
        private Vector3 trenchCenter = new Vector3(-34f, -42f, -32f);

        public void InitializeTulkun(Vector3 startPos)
        {
            transform.position = startPos;
            diver = FindAnyObjectByType<DiverController>();

            BuildTulkunModel();
        }

        private void BuildTulkunModel()
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material whaleMat = new Material(uLit);
            whaleMat.color = new Color(0.08f, 0.16f, 0.26f); // Deep oceanic slate blue
            whaleMat.SetFloat("_Smoothness", 0.75f);

            Material bellyMat = new Material(uLit);
            bellyMat.color = new Color(0.75f, 0.88f, 0.95f); // Creamy ventral pleats
            bellyMat.SetFloat("_Smoothness", 0.6f);

            Material goldBioMat = new Material(uLit);
            Color bioGold = new Color(1f, 0.82f, 0.2f);
            goldBioMat.color = bioGold;
            goldBioMat.EnableKeyword("_EMISSION");
            goldBioMat.SetColor("_EmissionColor", bioGold * 2.8f);

            // 1. Massive 32m Torso Body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "TulkunTorso";
            body.transform.SetParent(transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(5.2f, 16.0f, 4.2f);
            body.GetComponent<Renderer>().sharedMaterial = whaleMat;
            RemoveCollider(body);

            // 2. Armored Rostrum Cranial Crest
            GameObject headCrest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            headCrest.name = "RostrumPlate";
            headCrest.transform.SetParent(transform, false);
            headCrest.transform.localPosition = new Vector3(0f, 0.8f, 12.0f);
            headCrest.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            headCrest.transform.localScale = new Vector3(4.2f, 1.8f, 8.5f);
            headCrest.GetComponent<Renderer>().sharedMaterial = whaleMat;
            RemoveCollider(headCrest);

            // 3. Ventral Throat Pleats (Belly)
            GameObject belly = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            belly.name = "VentralPleats";
            belly.transform.SetParent(transform, false);
            belly.transform.localPosition = new Vector3(0f, -1.2f, 4.0f);
            belly.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            belly.transform.localScale = new Vector3(4.2f, 12.0f, 2.4f);
            belly.GetComponent<Renderer>().sharedMaterial = bellyMat;
            RemoveCollider(belly);

            // 4. Bioluminescent Lateral Stripe Runes
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = $"BioStripe_{s}";
                stripe.transform.SetParent(transform, false);
                stripe.transform.localPosition = new Vector3(s * 2.6f, 0.3f, 2.0f);
                stripe.transform.localScale = new Vector3(0.12f, 0.6f, 18.0f);
                stripe.GetComponent<Renderer>().sharedMaterial = goldBioMat;
                RemoveCollider(stripe);
            }

            // 5. Four Enormous Hydrofoil Pectoral Flippers
            leftFrontPectoral = CreatePectoralFlipper("LeftFrontFlipper", new Vector3(-3.2f, -0.6f, 6.0f), true, 1.2f, whaleMat, goldBioMat);
            rightFrontPectoral = CreatePectoralFlipper("RightFrontFlipper", new Vector3(3.2f, -0.6f, 6.0f), false, 1.2f, whaleMat, goldBioMat);
            leftRearPectoral = CreatePectoralFlipper("LeftRearFlipper", new Vector3(-2.8f, -0.4f, -4.0f), true, 0.85f, whaleMat, goldBioMat);
            rightRearPectoral = CreatePectoralFlipper("RightRearFlipper", new Vector3(2.8f, -0.4f, -4.0f), false, 0.85f, whaleMat, goldBioMat);

            // 6. Caudal Tail Stock & Twin Flukes
            GameObject tailStock = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            tailStock.name = "TailStock";
            tailStock.transform.SetParent(transform, false);
            tailStock.transform.localPosition = new Vector3(0f, 0f, -14.0f);
            tailStock.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tailStock.transform.localScale = new Vector3(2.2f, 8.0f, 1.8f);
            tailStock.GetComponent<Renderer>().sharedMaterial = whaleMat;
            RemoveCollider(tailStock);

            GameObject flukes = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flukes.name = "TwinFlukes";
            flukes.transform.SetParent(tailStock.transform, false);
            flukes.transform.localPosition = new Vector3(0f, -4.2f, 0f);
            flukes.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            flukes.transform.localScale = new Vector3(12.0f, 0.4f, 2.8f);
            flukes.GetComponent<Renderer>().sharedMaterial = whaleMat;
            RemoveCollider(flukes);
            flukeTail = flukes.transform;
        }

        private Transform CreatePectoralFlipper(string name, Vector3 pos, bool isLeft, float scale, Material skinMat, Material bioMat)
        {
            GameObject flipperRoot = new GameObject(name);
            flipperRoot.transform.SetParent(transform, false);
            flipperRoot.transform.localPosition = pos;

            float dir = isLeft ? -1f : 1f;

            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Blade";
            blade.transform.SetParent(flipperRoot.transform, false);
            blade.transform.localPosition = new Vector3(dir * 3.5f * scale, 0f, -0.5f);
            blade.transform.localRotation = Quaternion.Euler(0f, dir * 18f, -dir * 12f);
            blade.transform.localScale = new Vector3(7.0f * scale, 0.35f, 2.4f * scale);
            blade.GetComponent<Renderer>().sharedMaterial = skinMat;
            RemoveCollider(blade);

            // Bioluminescent margin tip
            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.name = "BioTip";
            tip.transform.SetParent(blade.transform, false);
            tip.transform.localPosition = new Vector3(dir * 0.45f, 0f, 0f);
            tip.transform.localScale = new Vector3(0.2f, 1.05f, 0.85f);
            tip.GetComponent<Renderer>().sharedMaterial = bioMat;
            RemoveCollider(tip);

            return flipperRoot.transform;
        }

        private void Update()
        {
            if (diver == null) diver = FindAnyObjectByType<DiverController>();

            UpdateTrenchCircuitFlight();
            AnimateWhaleKinematics();
            HandleAcousticSongs();
            ApplySymbioticDraftingSlipstream();
        }

        private void UpdateTrenchCircuitFlight()
        {
            // Circuit around the deep trench: radius 32m, depth -42m to -48m
            circuitAngle += Time.deltaTime * (cruiseSpeed / 32f);
            float targetX = trenchCenter.x + Mathf.Cos(circuitAngle) * 32f;
            float targetZ = trenchCenter.z + Mathf.Sin(circuitAngle * 2f) * 22f; // Figure-8 motion
            float targetY = trenchCenter.y + Mathf.Sin(circuitAngle * 1.5f) * 4.5f;

            Vector3 targetPos = new Vector3(targetX, targetY, targetZ);
            Vector3 moveDir = (targetPos - transform.position);

            if (moveDir.sqrMagnitude > 0.1f)
            {
                transform.position += transform.forward * cruiseSpeed * Time.deltaTime;
                Quaternion lookRot = Quaternion.LookRotation(moveDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * turnSpeed);
            }
        }

        private void AnimateWhaleKinematics()
        {
            flipperPhase += Time.deltaTime * 0.85f;
            float flipperRoll = Mathf.Sin(flipperPhase) * 14f;
            float flukeAngle = Mathf.Sin(flipperPhase - 0.8f) * 16f;

            if (leftFrontPectoral != null) leftFrontPectoral.localRotation = Quaternion.Euler(0f, 0f, -flipperRoll);
            if (rightFrontPectoral != null) rightFrontPectoral.localRotation = Quaternion.Euler(0f, 0f, flipperRoll);
            if (leftRearPectoral != null) leftRearPectoral.localRotation = Quaternion.Euler(0f, 0f, -flipperRoll * 0.7f);
            if (rightRearPectoral != null) rightRearPectoral.localRotation = Quaternion.Euler(0f, 0f, flipperRoll * 0.7f);

            if (flukeTail != null) flukeTail.localRotation = Quaternion.Euler(90f + flukeAngle, 0f, 0f);
        }

        private void HandleAcousticSongs()
        {
            songTimer -= Time.deltaTime;
            if (songTimer <= 0f)
            {
                songTimer = songInterval + UnityEngine.Random.Range(-1.5f, 2.5f);

                // Play resonant low-frequency cetacean whale song (68 Hz deep harmonic)
                if (UnderwaterAudio.Instance != null)
                {
                    UnderwaterAudio.Instance.PlayHarmonicTone(68f, 2.2f);
                }

                // If diver is nearby, log spiritual communion and give heart rate coherence bonus
                if (diver != null && Vector3.Distance(transform.position, diver.transform.position) < 45f)
                {
                    if (UnderwaterHUD.Instance != null)
                    {
                        UnderwaterHUD.Instance.DisplayScientificLog("TULKUN SPIRIT SONG", "Resonant low-frequency acoustic transmission harmonizes with your neural link.");
                    }

                    if (HeartRateSystem.Instance != null)
                    {
                        HeartRateSystem.Instance.currentOxygen = Mathf.MoveTowards(
                            HeartRateSystem.Instance.currentOxygen,
                            HeartRateSystem.Instance.maxOxygen,
                            15f
                        );
                    }
                }
            }
        }

        private void ApplySymbioticDraftingSlipstream()
        {
            if (diver == null) return;

            float dist = Vector3.Distance(transform.position, diver.transform.position);
            if (dist < 18f)
            {
                Rigidbody rb = diver.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForce(transform.forward * 8.5f, ForceMode.Acceleration);
                }

                if (HeartRateSystem.Instance != null)
                {
                    HeartRateSystem.Instance.currentOxygen = Mathf.MoveTowards(
                        HeartRateSystem.Instance.currentOxygen,
                        HeartRateSystem.Instance.maxOxygen,
                        6f * Time.deltaTime
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

