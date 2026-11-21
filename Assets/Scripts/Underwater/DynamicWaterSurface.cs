using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Dynamic Undulating Water Surface & Volumetric Sun Shafts (God Rays).
    /// Generates:
    /// - Gerstner / sinusoidal surface wave vertex displacement visible from below
    /// - Volumetric light shafts penetrating into the abyss that sway with ocean currents
    /// </summary>
    public class DynamicWaterSurface : MonoBehaviour
    {
        [Header("Wave Motion")]
        public float waveHeight = 0.35f;
        public float waveFrequency = 0.14f;
        public float waveSpeed = 1.3f;

        private Mesh filterMesh;
        private Vector3[] baseVertices;
        private Vector3[] displacedVertices;
        private MeshFilter meshFilter;

        private List<Transform> sunShafts = new List<Transform>();
        private List<Quaternion> shaftBaseRotations = new List<Quaternion>();

        public void InitializeSurface(Transform parent, float surfaceY, float radius)
        {
            transform.SetParent(parent, false);
            transform.position = new Vector3(0f, surfaceY, 0f);

            BuildUndulatingMesh(radius * 2.5f, 36);
            CreateVolumetricSunShafts(radius);
        }

        private void BuildUndulatingMesh(float size, int resolution)
        {
            meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material surfMat = new Material(uLit);
            surfMat.color = new Color(0.06f, 0.75f, 0.88f, 0.42f);
            surfMat.EnableKeyword("_EMISSION");
            surfMat.SetColor("_EmissionColor", new Color(0.02f, 0.35f, 0.45f) * 1.5f);
            surfMat.SetFloat("_Smoothness", 0.96f);
            meshRenderer.sharedMaterial = surfMat;

            // Generate Plane Grid
            filterMesh = new Mesh();
            filterMesh.name = "UndulatingWaterMesh";

            int vCount = (resolution + 1) * (resolution + 1);
            baseVertices = new Vector3[vCount];
            displacedVertices = new Vector3[vCount];
            Vector2[] uvs = new Vector2[vCount];
            int[] triangles = new int[resolution * resolution * 6];

            float halfSize = size * 0.5f;
            float step = size / resolution;

            int vi = 0;
            for (int z = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    float wx = -halfSize + x * step;
                    float wz = -halfSize + z * step;
                    baseVertices[vi] = new Vector3(wx, 0f, wz);
                    displacedVertices[vi] = baseVertices[vi];
                    uvs[vi] = new Vector2((float)x / resolution, (float)z / resolution);
                    vi++;
                }
            }

            int ti = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int root = z * (resolution + 1) + x;
                    triangles[ti++] = root;
                    triangles[ti++] = root + resolution + 1;
                    triangles[ti++] = root + 1;

                    triangles[ti++] = root + 1;
                    triangles[ti++] = root + resolution + 1;
                    triangles[ti++] = root + resolution + 2;
                }
            }

            filterMesh.vertices = baseVertices;
            filterMesh.uv = uvs;
            filterMesh.triangles = triangles;
            filterMesh.RecalculateNormals();
            filterMesh.RecalculateBounds();
            meshFilter.mesh = filterMesh;
        }

        private void CreateVolumetricSunShafts(float radius)
        {
            GameObject shaftsRoot = new GameObject("VolumetricSunShafts");
            shaftsRoot.transform.SetParent(transform, false);

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material shaftMat = new Material(uLit);
            shaftMat.color = new Color(0.25f, 0.85f, 1f, 0.12f);
            shaftMat.EnableKeyword("_EMISSION");
            shaftMat.SetColor("_EmissionColor", new Color(0.15f, 0.7f, 0.9f) * 1.6f);
            shaftMat.SetFloat("_Smoothness", 0.1f);

            int shaftCount = 12;
            for (int i = 0; i < shaftCount; i++)
            {
                float angle = (i / (float)shaftCount) * Mathf.PI * 2f;
                float dist = UnityEngine.Random.Range(5f, radius * 0.75f);
                Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, -0.2f, Mathf.Sin(angle) * dist);

                GameObject shaftGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                shaftGO.name = $"GodRayShaft_{i + 1}";
                shaftGO.transform.SetParent(shaftsRoot.transform, false);
                shaftGO.transform.position = pos;

                // Slanted angle matching moonlight / sunlight
                float beamLength = UnityEngine.Random.Range(20f, 28f);
                Quaternion rot = Quaternion.Euler(60f + UnityEngine.Random.Range(-5f, 5f), -25f + UnityEngine.Random.Range(-8f, 8f), 0f);
                shaftGO.transform.rotation = rot;
                shaftGO.transform.localScale = new Vector3(UnityEngine.Random.Range(1.8f, 3.2f), beamLength * 0.5f, UnityEngine.Random.Range(1.8f, 3.2f));

                shaftGO.GetComponent<Renderer>().sharedMaterial = shaftMat;
                Collider c = shaftGO.GetComponent<Collider>();
                if (c != null) DestroyImmediate(c);

                sunShafts.Add(shaftGO.transform);
                shaftBaseRotations.Add(rot);
            }
        }

        private void Update()
        {
            AnimateWaterWaves();
            AnimateSunShafts();
        }

        private void AnimateWaterWaves()
        {
            if (filterMesh == null || baseVertices == null) return;

            float t = Time.time * waveSpeed;

            for (int i = 0; i < baseVertices.Length; i++)
            {
                Vector3 v = baseVertices[i];
                float y = Mathf.Sin(v.x * waveFrequency + t) * waveHeight
                        + Mathf.Cos(v.z * (waveFrequency * 1.15f) + t * 0.85f) * (waveHeight * 0.75f)
                        + Mathf.Sin((v.x + v.z) * (waveFrequency * 1.8f) + t * 1.3f) * (waveHeight * 0.35f);

                displacedVertices[i] = new Vector3(v.x, y, v.z);
            }

            filterMesh.vertices = displacedVertices;
            filterMesh.RecalculateBounds();
        }

        private void AnimateSunShafts()
        {
            float t = Time.time;
            for (int i = 0; i < sunShafts.Count; i++)
            {
                if (sunShafts[i] != null)
                {
                    float swayX = Mathf.Sin(t * 0.6f + i * 0.8f) * 2.2f;
                    float swayZ = Mathf.Cos(t * 0.45f + i * 1.1f) * 2.2f;
                    sunShafts[i].rotation = shaftBaseRotations[i] * Quaternion.Euler(swayX, 0f, swayZ);
                }
            }
        }
    }
}

