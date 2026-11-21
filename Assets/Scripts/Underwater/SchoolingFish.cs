using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Procedural marine life that schools together across the coral reef.
    /// Responds directly to player biofeedback:
    /// - Calm (Low HR): Fish become curious and school peacefully around the diver.
    /// - Stress (High HR): Fish get startled and scatter away into coral crevices.
    /// </summary>
    public class SchoolingFish : MonoBehaviour
    {
        [Header("School Settings")]
        public int fishCount = 18;
        public float schoolRadius = 6f;
        public float swimSpeed = 3.5f;
        public float scatterSpeed = 9f;
        public Color fishColor = new Color(1f, 0.6f, 0.1f); // Vibrant orange/yellow

        private struct FishAgent
        {
            public Transform transform;
            public Vector3 velocity;
            public float tailPhase;
        }

        private List<FishAgent> fishList = new List<FishAgent>();
        private DiverController diver;
        private HeartRateSystem hrSystem;
        private Vector3 schoolCenter;
        private float scatterTimer = 0f;

        public void InitializeSchool(Vector3 centerPos, Color color, int count)
        {
            schoolCenter = centerPos;
            fishColor = color;
            fishCount = count;
            transform.position = centerPos;

            diver = FindAnyObjectByType<DiverController>();
            hrSystem = HeartRateSystem.Instance;

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material fishMat = new Material(uLit);
            fishMat.color = fishColor;
            fishMat.SetFloat("_Smoothness", 0.7f);

            // Spawn fish agents
            for (int i = 0; i < fishCount; i++)
            {
                GameObject fishGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fishGO.name = $"ReefFish_{i}";
                fishGO.transform.SetParent(transform, false);

                Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * schoolRadius;
                fishGO.transform.localPosition = randomOffset;
                fishGO.transform.localScale = new Vector3(0.12f, 0.14f, 0.45f);

                Renderer ren = fishGO.GetComponent<Renderer>();
                ren.sharedMaterial = fishMat;

                Collider col = fishGO.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);

                // Tail fin blade
                GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tail.name = "FishTail";
                tail.transform.SetParent(fishGO.transform, false);
                tail.transform.localPosition = new Vector3(0f, 0f, -0.26f);
                tail.transform.localScale = new Vector3(0.02f, 0.28f, 0.22f);
                tail.GetComponent<Renderer>().sharedMaterial = fishMat;
                Collider tc = tail.GetComponent<Collider>();
                if (tc != null) DestroyImmediate(tc);

                FishAgent agent = new FishAgent
                {
                    transform = fishGO.transform,
                    velocity = UnityEngine.Random.onUnitSphere * swimSpeed,
                    tailPhase = UnityEngine.Random.Range(0f, 10f)
                };
                fishList.Add(agent);
            }
        }

        private void Update()
        {
            if (diver == null) diver = FindAnyObjectByType<DiverController>();
            if (hrSystem == null) hrSystem = HeartRateSystem.Instance;

            float diverDist = diver != null ? Vector3.Distance(transform.position, diver.transform.position) : 999f;
            Rigidbody diverRb = diver != null ? diver.GetComponent<Rigidbody>() : null;
            float diverSpeed = diverRb != null ? diverRb.linearVelocity.magnitude : 0f;

            // Trigger explosive scatter if diver rushes close (< 4.8m with speed > 2.5m/s) or diver is stressed
            bool shouldScatter = diverDist < 4.8f && (diverSpeed > 2.5f || (hrSystem != null && hrSystem.isStressed));
            if (shouldScatter)
            {
                scatterTimer = 3.2f;
            }
            else if (scatterTimer > 0f)
            {
                scatterTimer -= Time.deltaTime;
            }

            bool isScattering = scatterTimer > 0f;
            bool isDiverNear = diverDist < 12f;
            bool isDiverCalm = hrSystem != null && hrSystem.isCalm;

            // Target for school center: drift slowly or orbit diver if calm and not scattering
            Vector3 targetCenter = schoolCenter;
            if (isDiverNear && isDiverCalm && !isScattering)
            {
                targetCenter = diver.transform.position + new Vector3(0f, 0.5f, 0f);
            }

            for (int i = 0; i < fishList.Count; i++)
            {
                FishAgent fish = fishList[i];
                Vector3 toCenter = (targetCenter - fish.transform.position);

                // Flocking force towards center
                Vector3 desiredVel = toCenter.normalized * swimSpeed;

                // Explosive radial scatter burst away from diver
                if (isScattering && diver != null)
                {
                    Vector3 awayFromDiver = (fish.transform.position - diver.transform.position).normalized;
                    // Add upward and lateral panic dispersion
                    awayFromDiver.y += 0.35f;
                    desiredVel = awayFromDiver.normalized * (scatterSpeed + (i % 5) * 1.2f);
                }

                float lerpRate = isScattering ? 7f : 2.5f;
                fish.velocity = Vector3.Lerp(fish.velocity, desiredVel, Time.deltaTime * lerpRate);
                fish.transform.position += fish.velocity * Time.deltaTime;

                // Rotate fish facing swimming direction
                if (fish.velocity.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(fish.velocity.normalized);
                    fish.transform.rotation = Quaternion.Slerp(fish.transform.rotation, targetRot, Time.deltaTime * 8f);
                }

                // Rapid tail flutter when scattering, gentle swimming tail when cruising
                float tailRate = isScattering ? 18f : 7f;
                fish.tailPhase += Time.deltaTime * tailRate;
                fish.transform.Rotate(Vector3.up * Mathf.Sin(fish.tailPhase) * (isScattering ? 22f : 12f), Space.Self);

                fishList[i] = fish;
            }
        }
    }
}

