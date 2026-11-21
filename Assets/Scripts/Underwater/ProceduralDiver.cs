using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Assembles an authentic Metkayina Na'vi Avatar 3D character in code:
    /// - Cerulean/Cyan blue skin with bioluminescent dot-matrix freckles
    /// - Characteristic Na'vi facial features, pointed ears & braided neural queue
    /// - Metkayina aquatic fin-tail that sways with water currents
    /// - Forearm aquatic paddle ridges & woven Metkayina loincloth
    /// - Dual Locomotion Animation:
    ///   * Horizontal 6DOF Swimming with fin flutter & undulating tail
    ///   * Upright Ground Walking on the seabed with alternating leg stride & arm swing!
    /// </summary>
    public class ProceduralDiver : MonoBehaviour
    {
        [Header("Model Roots & Joints")]
        public Transform avatarVisualRoot;
        public Transform headTransform;
        public Transform neuralQueue;
        public Transform aquaticTail;
        public Transform leftLeg;
        public Transform rightLeg;
        public Transform leftArm;
        public Transform rightArm;

        [Header("Locomotion State")]
        public bool isGrounded = false;
        private float animationPhase = 0f;
        private float currentTiltAngle = 90f; // 90 = horizontal swim, 0 = upright stand

        public void BuildDiverModel()
        {
            // Clear only existing AvatarVisualRoot if present
            Transform existingRoot = transform.Find("AvatarVisualRoot");
            if (existingRoot != null)
            {
                DestroyImmediate(existingRoot.gameObject);
            }

            GameObject rootGO = new GameObject("AvatarVisualRoot");
            rootGO.transform.SetParent(transform, false);
            avatarVisualRoot = rootGO.transform;

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            // Metkayina Na'vi Materials
            Material skinMat = new Material(uLit);
            skinMat.color = new Color(0.18f, 0.65f, 0.88f); // Cyan/Cerulean blue skin
            skinMat.SetFloat("_Smoothness", 0.6f);

            Material freckleBioMat = new Material(uLit);
            freckleBioMat.color = new Color(0.35f, 0.95f, 1f);
            freckleBioMat.EnableKeyword("_EMISSION");
            freckleBioMat.SetColor("_EmissionColor", new Color(0.35f, 0.95f, 1f) * 2.8f);

            Material hairMat = new Material(uLit);
            hairMat.color = new Color(0.04f, 0.04f, 0.06f); // Deep midnight hair

            Material garmentMat = new Material(uLit);
            garmentMat.color = new Color(0.65f, 0.5f, 0.3f); // Woven Pandoran fiber

            // 1. Slender Athletic Torso
            GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.name = "AvatarTorso";
            torso.transform.SetParent(avatarVisualRoot, false);
            torso.transform.localPosition = new Vector3(0f, 0f, 0f);
            torso.transform.localScale = new Vector3(0.48f, 0.55f, 0.32f);
            torso.GetComponent<Renderer>().sharedMaterial = skinMat;
            RemoveCollider(torso);

            // Woven Loincloth
            GameObject loincloth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            loincloth.name = "Loincloth";
            loincloth.transform.SetParent(torso.transform, false);
            loincloth.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            loincloth.transform.localScale = new Vector3(1.15f, 0.35f, 1.15f);
            loincloth.GetComponent<Renderer>().sharedMaterial = garmentMat;
            RemoveCollider(loincloth);

            // Bioluminescent Freckle Stripes on Torso
            CreateBioFreckles(torso.transform, freckleBioMat, new Vector3(0f, 0.2f, 0.51f), new Vector3(0.35f, 0.35f, 0.05f));

            // 2. Head with Pointed Na'vi Ears & Neural Queue
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "AvatarHead";
            head.transform.SetParent(avatarVisualRoot, false);
            head.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            head.transform.localScale = new Vector3(0.32f, 0.38f, 0.34f);
            head.GetComponent<Renderer>().sharedMaterial = skinMat;
            headTransform = head.transform;
            RemoveCollider(head);

            // Pointed Ears (Left & Right)
            CreatePointedEar(head.transform, skinMat, new Vector3(-0.48f, 0.15f, -0.1f), Quaternion.Euler(15f, -35f, 40f));
            CreatePointedEar(head.transform, skinMat, new Vector3(0.48f, 0.15f, -0.1f), Quaternion.Euler(15f, 35f, -40f));

            // The Neural Queue (Braided queue for Tsaheylu)
            GameObject queueGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            queueGO.name = "NeuralQueue";
            queueGO.transform.SetParent(head.transform, false);
            queueGO.transform.localPosition = new Vector3(0f, -0.35f, -0.45f);
            queueGO.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            queueGO.transform.localScale = new Vector3(0.08f, 0.85f, 0.08f);
            queueGO.GetComponent<Renderer>().sharedMaterial = hairMat;
            neuralQueue = queueGO.transform;
            RemoveCollider(queueGO);

            // Glowing queue tip
            GameObject queueTip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            queueTip.transform.SetParent(queueGO.transform, false);
            queueTip.transform.localPosition = new Vector3(0f, -1.05f, 0f);
            queueTip.transform.localScale = Vector3.one * 1.8f;
            queueTip.GetComponent<Renderer>().sharedMaterial = freckleBioMat;
            RemoveCollider(queueTip);

            // Translucent Aquatic Visor
            Material glassMat = new Material(uLit);
            glassMat.color = new Color(0.2f, 0.9f, 1f, 0.65f);
            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "AquaticVisor";
            visor.transform.SetParent(head.transform, false);
            visor.transform.localPosition = new Vector3(0f, 0.05f, 0.42f);
            visor.transform.localScale = new Vector3(0.85f, 0.35f, 0.25f);
            visor.GetComponent<Renderer>().sharedMaterial = glassMat;
            RemoveCollider(visor);

            // 3. Metkayina Fin-Tail (Aquatic paddle tail)
            GameObject tailGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tailGO.name = "MetkayinaTail";
            tailGO.transform.SetParent(avatarVisualRoot, false);
            tailGO.transform.localPosition = new Vector3(0f, -0.55f, -0.35f);
            tailGO.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            tailGO.transform.localScale = new Vector3(0.08f, 0.9f, 0.08f);
            tailGO.GetComponent<Renderer>().sharedMaterial = skinMat;
            aquaticTail = tailGO.transform;
            RemoveCollider(tailGO);

            // Paddle Fin on Tail Tip
            GameObject finBlade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            finBlade.transform.SetParent(tailGO.transform, false);
            finBlade.transform.localPosition = new Vector3(0f, -1.1f, 0f);
            finBlade.transform.localScale = new Vector3(1.6f, 0.8f, 0.1f);
            finBlade.GetComponent<Renderer>().sharedMaterial = freckleBioMat;
            RemoveCollider(finBlade);

            // 4. Arms with Aquatic Paddle Ridges
            leftArm = CreateLimb("LeftArm", avatarVisualRoot, skinMat, freckleBioMat, new Vector3(-0.36f, 0.35f, 0f), false);
            rightArm = CreateLimb("RightArm", avatarVisualRoot, skinMat, freckleBioMat, new Vector3(0.36f, 0.35f, 0f), true);

            // 5. Legs & Metkayina Aquatic Feet
            leftLeg = CreateLeg("LeftLeg", avatarVisualRoot, skinMat, freckleBioMat, new Vector3(-0.18f, -0.55f, 0f));
            rightLeg = CreateLeg("RightLeg", avatarVisualRoot, skinMat, freckleBioMat, new Vector3(0.18f, -0.55f, 0f));
        }

        private void CreatePointedEar(Transform parent, Material mat, Vector3 pos, Quaternion rot)
        {
            GameObject ear = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ear.name = "NaViEar";
            ear.transform.SetParent(parent, false);
            ear.transform.localPosition = pos;
            ear.transform.localRotation = rot;
            ear.transform.localScale = new Vector3(0.12f, 0.35f, 0.08f);
            ear.GetComponent<Renderer>().sharedMaterial = mat;
            RemoveCollider(ear);
        }

        private void CreateBioFreckles(Transform parent, Material mat, Vector3 pos, Vector3 scale)
        {
            GameObject f = GameObject.CreatePrimitive(PrimitiveType.Cube);
            f.name = "BioFreckles";
            f.transform.SetParent(parent, false);
            f.transform.localPosition = pos;
            f.transform.localScale = scale;
            f.GetComponent<Renderer>().sharedMaterial = mat;
            RemoveCollider(f);
        }

        private Transform CreateLimb(string name, Transform parent, Material skinMat, Material bioMat, Vector3 pos, bool isRight)
        {
            GameObject pivot = new GameObject(name + "Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = pos;

            GameObject limb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            limb.transform.SetParent(pivot.transform, false);
            limb.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            limb.transform.localScale = new Vector3(0.14f, 0.42f, 0.14f);
            limb.GetComponent<Renderer>().sharedMaterial = skinMat;
            RemoveCollider(limb);

            // Metkayina paddle ridge on forearm
            GameObject paddle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paddle.transform.SetParent(limb.transform, false);
            paddle.transform.localPosition = new Vector3(isRight ? 0.6f : -0.6f, -0.2f, 0f);
            paddle.transform.localScale = new Vector3(0.35f, 0.8f, 0.08f);
            paddle.GetComponent<Renderer>().sharedMaterial = bioMat;
            RemoveCollider(paddle);

            return pivot.transform;
        }

        private Transform CreateLeg(string name, Transform parent, Material skinMat, Material bioMat, Vector3 pos)
        {
            GameObject pivot = new GameObject(name + "Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = pos;

            GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leg.transform.SetParent(pivot.transform, false);
            leg.transform.localPosition = new Vector3(0f, -0.42f, 0f);
            leg.transform.localScale = new Vector3(0.16f, 0.48f, 0.16f);
            leg.GetComponent<Renderer>().sharedMaterial = skinMat;
            RemoveCollider(leg);

            // Wide Metkayina aquatic foot
            GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            foot.transform.SetParent(leg.transform, false);
            foot.transform.localPosition = new Vector3(0f, -1.05f, 0.25f);
            foot.transform.localScale = new Vector3(0.9f, 0.15f, 1.4f);
            foot.GetComponent<Renderer>().sharedMaterial = bioMat;
            RemoveCollider(foot);

            return pivot.transform;
        }

        /// <summary>
        /// Transitions smoothly between Horizontal Swimming posture (90 deg) and Upright Walking posture (0 deg).
        /// </summary>
        public void UpdateLocomotionPosture(bool grounded, float moveSpeed)
        {
            isGrounded = grounded;

            // Target posture: 0 deg (Standing upright) when grounded, 90 deg (Prone swimming) when in water column
            float targetTilt = isGrounded ? 0f : 85f;
            currentTiltAngle = Mathf.Lerp(currentTiltAngle, targetTilt, Time.deltaTime * 6f);

            if (avatarVisualRoot != null)
            {
                avatarVisualRoot.localRotation = Quaternion.Euler(currentTiltAngle, 0f, 0f);
            }

            if (isGrounded)
            {
                AnimateWalking(moveSpeed);
            }
            else
            {
                AnimateSwimming(moveSpeed);
            }
        }

        public void AnimateWalking(float walkSpeed)
        {
            if (leftLeg == null || rightLeg == null) return;

            if (walkSpeed > 0.1f)
            {
                animationPhase += Time.deltaTime * walkSpeed * 4.5f;
                float legAngle = Mathf.Sin(animationPhase) * 32f;
                float armAngle = Mathf.Sin(animationPhase) * 28f;

                leftLeg.localRotation = Quaternion.Euler(legAngle, 0f, 0f);
                rightLeg.localRotation = Quaternion.Euler(-legAngle, 0f, 0f);

                if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-armAngle, 0f, 0f);
                if (rightArm != null) rightArm.localRotation = Quaternion.Euler(armAngle, 0f, 0f);

                // Tail sways behind while walking
                if (aquaticTail != null) aquaticTail.localRotation = Quaternion.Euler(45f, Mathf.Sin(animationPhase) * 15f, 0f);
            }
            else
            {
                // Idle breathing posture
                leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, Quaternion.identity, Time.deltaTime * 5f);
                rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, Quaternion.identity, Time.deltaTime * 5f);
                if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, Quaternion.identity, Time.deltaTime * 5f);
                if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, Quaternion.identity, Time.deltaTime * 5f);
            }
        }

        public void AnimateSwimming(float swimSpeed)
        {
            if (leftLeg == null || rightLeg == null) return;

            if (swimSpeed > 0.05f)
            {
                animationPhase += Time.deltaTime * Mathf.Clamp(swimSpeed * 6f, 4f, 14f);
                float angle = Mathf.Sin(animationPhase) * 26f;

                leftLeg.localRotation = Quaternion.Euler(angle, 0f, 0f);
                rightLeg.localRotation = Quaternion.Euler(-angle, 0f, 0f);

                // Undulating aquatic tail flutter
                if (aquaticTail != null) aquaticTail.localRotation = Quaternion.Euler(20f, 0f, Mathf.Sin(animationPhase) * 22f);
            }
            else
            {
                // Gentle floating drift
                animationPhase += Time.deltaTime * 1.5f;
                float idleAngle = Mathf.Sin(animationPhase) * 6f;
                leftLeg.localRotation = Quaternion.Euler(idleAngle, 0f, 0f);
                rightLeg.localRotation = Quaternion.Euler(-idleAngle, 0f, 0f);
            }
        }

        public void SetFirstPersonVRVisibility(bool hideHeadAndArms)
        {
            if (headTransform != null) headTransform.gameObject.SetActive(!hideHeadAndArms);
            if (leftArm != null) leftArm.gameObject.SetActive(!hideHeadAndArms);
            if (rightArm != null) rightArm.gameObject.SetActive(!hideHeadAndArms);
        }

        private void RemoveCollider(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
        }
    }
}
