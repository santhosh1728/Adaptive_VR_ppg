using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Avatar: Pandora - Metkayina Bioluminescent Ocean World Generator.
    /// Transforms the underwater environment into a self-illuminating alien abyss:
    /// - Deep Midnight-Indigo Ocean Abyss with Pandoran Moonlight
    /// - Glowing Coral Arches & Translucent Mushroom Corals
    /// - Sacred Oceanic "Atokirina" Spores (Woodsprites of the Sea)
    /// - Reactive "Helicoradian" Spiral Fan Anemones (Retract on stress)
    /// - Metkayina Ancestor Spirit Trees of Souls
    /// - Fluorescent Emerald & Magenta Kelp Forests
    /// </summary>
    public class ReefWorldGenerator : MonoBehaviour
    {
        public static ReefWorldGenerator Instance { get; private set; }

        [Header("World Dimensions")]
        public float worldRadius = 85f;
        public float seaBedDepth = -30f;
        public float surfaceY = 0f;

        [Header("Spawning")]
        public int pearlCount = 5;
        public List<GlowingPearl> spawnedPearls = new List<GlowingPearl>();
        public List<ReefInteractable> spawnedBeacons = new List<ReefInteractable>();
        public SurfaceGate surfaceGate { get; private set; }

        private List<Renderer> bioluminescentCorals = new List<Renderer>();
        private Transform worldContainer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void GenerateWorld()
        {
            if (worldContainer != null) DestroyImmediate(worldContainer.gameObject);
            GameObject oldEnv = GameObject.Find("PandoranReefEnvironment");
            if (oldEnv != null) DestroyImmediate(oldEnv);

            // Completely purge any submarine or predator remnants from previous scenes or runs
            foreach (var oldAI in FindObjectsByType<ApexPredatorAI>())
            {
                if (oldAI != null && oldAI.gameObject != null) DestroyImmediate(oldAI.gameObject);
            }
            string[] purgeNames = new string[] { "PandoranAkulaLeviathan", "AkulaBody", "CranialArmor", "LateralStripeL", "LateralStripeR", "DorsalSpine", "TailPivot", "Submarine" };
            foreach (var pName in purgeNames)
            {
                GameObject obj = GameObject.Find(pName);
                while (obj != null)
                {
                    DestroyImmediate(obj);
                    obj = GameObject.Find(pName);
                }
            }

            GameObject container = new GameObject("PandoranReefEnvironment");
            worldContainer = container.transform;

            SetupPandoranAtmosphere();
            CreateIridescentSurface(worldContainer);
            CreateObsidianSeaBed(worldContainer);
            CreateSurfaceVillageAndSeaStacks(worldContainer);
            CreateSunkenAncestralTemple(worldContainer);
            CreateLeviathanRibcage(worldContainer);
            CreateHydrothermalBlackSmokers(worldContainer);
            CreateOceanJetStreams(worldContainer);
            CreateVolcanicCratersAndHydrothermalVents(worldContainer);
            CreateOrganicReefFormations(worldContainer);
            CreateGlowingMushroomCorals(worldContainer);
            CreateRealisticKelpForest(worldContainer);
            CreateAbyssalGrottoCave(worldContainer);
            CreateAtokirinaSpores(worldContainer);
            SpawnDeepSeaLanternSquidAndSiphonophores(worldContainer);
            SpawnHelicoradianAnemones(worldContainer);
            SpawnGorgonianSeaFans(worldContainer);
            SpawnSpiritTreeBeacons(worldContainer);
            SpawnMetkayinaAltar(worldContainer);
            SpawnGiantClams(worldContainer);
            SpawnPearls(worldContainer);
            SpawnFishSchools(worldContainer);
            SpawnSeaTurtles(worldContainer);
            SpawnJellyfishHazards(worldContainer);
            SpawnMantaCompanion(worldContainer);
            SpawnSkimwingMount(worldContainer);
            SpawnTulkunWhale(worldContainer);
            SpawnOceanicRelics(worldContainer);
            SpawnSurfaceGate(worldContainer);
        }

        public float GetSeabedHeight(float x, float z)
        {
            float dist = Mathf.Sqrt(x * x + z * z);
            float baseDepth = -20f;

            // Zone 3: Hadal Deep Trench in South-West quad (down to -56m)
            float trenchCenterDist = Vector2.Distance(new Vector2(x, z), new Vector2(-36f, -34f));
            float trenchFactor = Mathf.Clamp01((38f - trenchCenterDist) / 38f);
            float trenchDepth = -Mathf.SmoothStep(0f, 32f, trenchFactor);

            // Zone 1: Shallows Atoll in North-East (rising to -8m)
            float shallowsDist = Vector2.Distance(new Vector2(x, z), new Vector2(26f, 26f));
            float shallowsFactor = Mathf.Clamp01((32f - shallowsDist) / 32f);
            float shallowsRise = Mathf.SmoothStep(0f, 13f, shallowsFactor);

            // Rolling sand dunes and geological ridges
            float dune = Mathf.Sin(x * 0.075f) * Mathf.Cos(z * 0.065f) * 2.8f;
            float shelfNoise = (Mathf.PerlinNoise(x * 0.025f + 140f, z * 0.025f + 140f) - 0.5f) * 5.0f;

            // Subterranean grotto entrance dip near (-26, -22)
            float grottoDist = Vector2.Distance(new Vector2(x, z), new Vector2(-26f, -22f));
            float grottoDip = (grottoDist < 12f) ? Mathf.SmoothStep(-5.5f, 0f, grottoDist / 12f) : 0f;

            // Outer perimeter barrier reef / sea stack roots rising up toward surface
            float rimCurve = (dist > 62f) ? Mathf.SmoothStep(0f, 18f, (dist - 62f) / 22f) : 0f;

            return baseDepth + trenchDepth + shallowsRise + dune + shelfNoise + grottoDip + rimCurve;
        }

        private void SetupPandoranAtmosphere()
        {
            // Deep mystical midnight-indigo ocean abyss
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.015f, 0.05f, 0.10f); // Deep Ocean Midnight Indigo
            RenderSettings.fogDensity = 0.030f;
            RenderSettings.ambientLight = new Color(0.04f, 0.14f, 0.25f); // Ethereal bio-blue ambient

            // Pandoran Moonlight filtered through ocean surface
            Light moon = FindAnyObjectByType<Light>();
            if (moon == null)
            {
                GameObject moonGO = new GameObject("PandoranMoonlight");
                moon = moonGO.AddComponent<Light>();
                moon.type = LightType.Directional;
            }
            moon.transform.rotation = Quaternion.Euler(60f, -25f, 0f);
            moon.color = new Color(0.18f, 0.7f, 0.95f);
            moon.intensity = 1.25f;
        }

        private void CreateIridescentSurface(Transform parent)
        {
            GameObject surface = new GameObject("PandoranWaterSurface");
            surface.transform.SetParent(parent, false);

            DynamicWaterSurface dws = surface.AddComponent<DynamicWaterSurface>();
            dws.InitializeSurface(parent, surfaceY, worldRadius);
        }

        private void CreateObsidianSeaBed(Transform parent)
        {
            GameObject seabed = new GameObject("BathymetricVolcanicSeaBed");
            seabed.transform.SetParent(parent, false);

            MeshFilter mf = seabed.AddComponent<MeshFilter>();
            MeshRenderer mr = seabed.AddComponent<MeshRenderer>();

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material bedMat = new Material(uLit);
            bedMat.color = new Color(0.06f, 0.12f, 0.18f); // Base dark oceanic seabed
            bedMat.EnableKeyword("_EMISSION");
            bedMat.SetColor("_EmissionColor", new Color(0.01f, 0.08f, 0.12f));
            bedMat.SetFloat("_Smoothness", 0.35f);
            mr.sharedMaterial = bedMat;

            // Generate Bathymetric Procedural Heightmap Grid
            int resolution = 42;
            float size = worldRadius * 2.4f;
            float halfSize = size * 0.5f;
            float step = size / resolution;

            int vCount = (resolution + 1) * (resolution + 1);
            Vector3[] vertices = new Vector3[vCount];
            Vector2[] uvs = new Vector2[vCount];
            Color[] colors = new Color[vCount];
            int[] triangles = new int[resolution * resolution * 6];

            int vi = 0;
            for (int z = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    float wx = -halfSize + x * step;
                    float wz = -halfSize + z * step;
                    float wy = GetSeabedHeight(wx, wz);

                    vertices[vi] = new Vector3(wx, wy, wz);
                    uvs[vi] = new Vector2((float)x / resolution, (float)z / resolution);

                    // Procedural Layered Seabed Colors:
                    // Shallows / Sand Dune peaks: Fine Pandoran gold-cyan sand
                    // Mid-slopes: Turquoise coral sediment
                    // Deep trenches: Volcanic basalt obsidian
                    float heightNorm = Mathf.InverseLerp(seaBedDepth - 6f, seaBedDepth + 4f, wy);
                    Color sandCol = new Color(0.18f, 0.45f, 0.55f);
                    Color sedimentCol = new Color(0.08f, 0.22f, 0.28f);
                    Color basaltCol = new Color(0.03f, 0.05f, 0.08f);

                    colors[vi] = heightNorm > 0.55f ?
                        Color.Lerp(sedimentCol, sandCol, (heightNorm - 0.55f) / 0.45f) :
                        Color.Lerp(basaltCol, sedimentCol, heightNorm / 0.55f);

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

            Mesh bedMesh = new Mesh();
            bedMesh.name = "BathymetricSeaBedMesh";
            bedMesh.vertices = vertices;
            bedMesh.uv = uvs;
            bedMesh.colors = colors;
            bedMesh.triangles = triangles;
            bedMesh.RecalculateNormals();
            bedMesh.RecalculateBounds();
            mf.mesh = bedMesh;

            // Accurate physical MeshCollider adhering to all dunes, trenches, and slopes!
            MeshCollider mc = seabed.AddComponent<MeshCollider>();
            mc.sharedMesh = bedMesh;

            // 1. Massive 6-meter-thick solid bedrock floor collider underneath to guarantee player never falls through
            GameObject solidFloor = new GameObject("SolidBedrockFloorCollider");
            solidFloor.transform.SetParent(parent, false);
            solidFloor.transform.position = new Vector3(0f, seaBedDepth - 9f, 0f);
            BoxCollider floorBox = solidFloor.AddComponent<BoxCollider>();
            floorBox.size = new Vector3(300f, 6f, 300f);

            // 2. Invisible perimeter boundary walls around the reef cove
            CreatePerimeterBoundary(parent);
        }

        private void CreatePerimeterBoundary(Transform parent)
        {
            GameObject boundaries = new GameObject("CovePerimeterBoundaries");
            boundaries.transform.SetParent(parent, false);

            float wallThickness = 6f;
            float wallHeight = 110f;
            float wallCenterY = -15f;
            float span = worldRadius * 2.3f;

            // North
            CreateBoundaryWall(boundaries.transform, "NorthBoundary", new Vector3(0f, wallCenterY, worldRadius), new Vector3(span, wallHeight, wallThickness));
            // South
            CreateBoundaryWall(boundaries.transform, "SouthBoundary", new Vector3(0f, wallCenterY, -worldRadius), new Vector3(span, wallHeight, wallThickness));
            // East
            CreateBoundaryWall(boundaries.transform, "EastBoundary", new Vector3(worldRadius, wallCenterY, 0f), new Vector3(wallThickness, wallHeight, span));
            // West
            CreateBoundaryWall(boundaries.transform, "WestBoundary", new Vector3(-worldRadius, wallCenterY, 0f), new Vector3(wallThickness, wallHeight, span));
        }

        private void CreateBoundaryWall(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            BoxCollider col = wall.AddComponent<BoxCollider>();
            col.size = size;
        }

        private void CreateSurfaceVillageAndSeaStacks(Transform parent)
        {
            GameObject surfaceWorld = new GameObject("PandoranSurfaceWorld");
            surfaceWorld.transform.SetParent(parent, false);

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material stoneMat = new Material(uLit);
            stoneMat.color = new Color(0.12f, 0.16f, 0.18f); // High sea stack basalt
            stoneMat.SetFloat("_Smoothness", 0.35f);

            Material woodMat = new Material(uLit);
            woodMat.color = new Color(0.36f, 0.24f, 0.14f); // Woven mangrove timber
            woodMat.SetFloat("_Smoothness", 0.45f);

            Material thatchMat = new Material(uLit);
            thatchMat.color = new Color(0.55f, 0.45f, 0.22f); // Dried golden palm thatch

            Material lanternMat = new Material(uLit);
            Color lanternCol = new Color(1f, 0.72f, 0.2f);
            lanternMat.color = lanternCol;
            lanternMat.EnableKeyword("_EMISSION");
            lanternMat.SetColor("_EmissionColor", lanternCol * 3.2f);

            // 1. Towering Pandoran Sea Stack Spires (Breaking the surface up to +20m)
            Vector3[] stackCoords = new Vector3[]
            {
                new Vector3(42f, 0f, 38f),
                new Vector3(-24f, 0f, 48f),
                new Vector3(56f, 0f, -14f),
                new Vector3(-55f, 0f, 22f)
            };

            for (int i = 0; i < stackCoords.Length; i++)
            {
                Vector3 basePos = stackCoords[i];
                float seabedY = GetSeabedHeight(basePos.x, basePos.z);
                float stackHeight = 22f + (i * 4f);

                GameObject spire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spire.name = $"SeaStackSpire_{i + 1}";
                spire.transform.SetParent(surfaceWorld.transform, false);
                spire.transform.position = new Vector3(basePos.x, (seabedY + stackHeight) * 0.5f, basePos.z);
                spire.transform.localScale = new Vector3(8.5f - i * 0.8f, (stackHeight - seabedY) * 0.5f, 8.5f - i * 0.8f);
                spire.transform.localRotation = Quaternion.Euler(UnityEngine.Random.Range(-4f, 4f), i * 45f, UnityEngine.Random.Range(-4f, 4f));
                spire.GetComponent<Renderer>().sharedMaterial = stoneMat;
            }

            // 2. Metkayina Woven Stilt Village (Surface Docks at Y = +0.45m)
            Vector3 villageCenter = new Vector3(22f, 0.45f, 24f);
            GameObject villageRoot = new GameObject("MetkayinaStiltVillage");
            villageRoot.transform.SetParent(surfaceWorld.transform, false);
            villageRoot.transform.position = villageCenter;

            // Mangrove Stilt Support Pillars
            for (int p = 0; p < 8; p++)
            {
                float px = (p % 4 - 1.5f) * 6.5f;
                float pz = (p / 4 - 0.5f) * 12.0f;
                float seabedAtStilt = GetSeabedHeight(villageCenter.x + px, villageCenter.z + pz);
                float legLen = 0.45f - seabedAtStilt;

                GameObject stilt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stilt.name = $"MangroveStilt_{p}";
                stilt.transform.SetParent(villageRoot.transform, false);
                stilt.transform.localPosition = new Vector3(px, -legLen * 0.5f, pz);
                stilt.transform.localScale = new Vector3(0.45f, legLen * 0.5f, 0.45f);
                stilt.GetComponent<Renderer>().sharedMaterial = woodMat;
            }

            // Solid Wooden Boardwalk Platforms (Avatars can walk, jump, and stand on them!)
            Vector3[] dockPositions = new Vector3[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(-6f, 0f, 6f),
                new Vector3(7f, 0f, 5f),
                new Vector3(0f, 0f, 12f)
            };

            for (int d = 0; d < dockPositions.Length; d++)
            {
                GameObject dock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dock.name = $"VillageBoardwalkDock_{d + 1}";
                dock.transform.SetParent(villageRoot.transform, false);
                dock.transform.localPosition = dockPositions[d];
                dock.transform.localScale = new Vector3(7.5f, 0.35f, 7.5f);
                dock.GetComponent<Renderer>().sharedMaterial = woodMat;

                // Thatched Marai Pavilion Huts on docks 0 and 3
                if (d == 0 || d == 3)
                {
                    GameObject hut = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    hut.name = "ThatchedMaraiHut";
                    hut.transform.SetParent(dock.transform, false);
                    hut.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                    hut.transform.localScale = new Vector3(0.9f, 1.8f, 0.9f);
                    hut.GetComponent<Renderer>().sharedMaterial = thatchMat;
                    Collider hc = hut.GetComponent<Collider>();
                    if (hc != null) DestroyImmediate(hc);

                    // Hanging lantern
                    GameObject lantern = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    lantern.name = "MaraiLantern";
                    lantern.transform.SetParent(dock.transform, false);
                    lantern.transform.localPosition = new Vector3(0f, 2.8f, 0f);
                    lantern.transform.localScale = Vector3.one * 0.4f;
                    lantern.GetComponent<Renderer>().sharedMaterial = lanternMat;
                    Collider lc = lantern.GetComponent<Collider>();
                    if (lc != null) DestroyImmediate(lc);

                    Light lLight = lantern.AddComponent<Light>();
                    lLight.type = LightType.Point;
                    lLight.color = lanternCol;
                    lLight.range = 14f;
                    lLight.intensity = 2.2f;
                }
            }

            // Sloping Entry Ramp (Dipping from docks into ocean so player walks effortlessly out of water)
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "OceanEntryRamp";
            ramp.transform.SetParent(villageRoot.transform, false);
            ramp.transform.localPosition = new Vector3(0f, -0.65f, -6.5f);
            ramp.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            ramp.transform.localScale = new Vector3(5.5f, 0.25f, 8.5f);
            ramp.GetComponent<Renderer>().sharedMaterial = woodMat;
        }

        private void CreateSunkenAncestralTemple(Transform parent)
        {
            float tx = -16f;
            float tz = 18f;
            float ty = GetSeabedHeight(tx, tz);

            GameObject templeGO = new GameObject("SunkenAncestralTemple");
            templeGO.transform.SetParent(parent, false);
            templeGO.transform.position = new Vector3(tx, ty, tz);

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material stoneMat = new Material(uLit);
            stoneMat.color = new Color(0.08f, 0.12f, 0.15f); // Ancient submerged megalithic stone
            stoneMat.SetFloat("_Smoothness", 0.4f);

            Material runeMat = new Material(uLit);
            Color runeCol = new Color(0.1f, 0.95f, 0.85f);
            runeMat.color = runeCol;
            runeMat.EnableKeyword("_EMISSION");
            runeMat.SetColor("_EmissionColor", runeCol * 3.5f);

            // Concentric stepped dais foundation
            for (int t = 0; t < 2; t++)
            {
                GameObject tier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tier.name = $"TempleDaisTier_{t}";
                tier.transform.SetParent(templeGO.transform, false);
                tier.transform.localPosition = new Vector3(0f, t * 0.4f, 0f);
                tier.transform.localScale = new Vector3(14.0f - (t * 2.8f), 0.4f, 14.0f - (t * 2.8f));
                tier.GetComponent<Renderer>().sharedMaterial = stoneMat;
            }

            // 6 Megalithic Cyclopean Pillars with glowing rune bands
            for (int p = 0; p < 6; p++)
            {
                float ang = (p / 6f) * Mathf.PI * 2f;
                Vector3 pPos = new Vector3(Mathf.Cos(ang) * 4.8f, 3.2f, Mathf.Sin(ang) * 4.8f);

                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"TemplePillar_{p}";
                pillar.transform.SetParent(templeGO.transform, false);
                pillar.transform.localPosition = pPos;
                pillar.transform.localScale = new Vector3(0.9f, 3.2f, 0.9f);
                pillar.GetComponent<Renderer>().sharedMaterial = stoneMat;

                // Glowing glyph ring
                GameObject glyph = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                glyph.name = "GlyphRing";
                glyph.transform.SetParent(pillar.transform, false);
                glyph.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                glyph.transform.localScale = new Vector3(1.08f, 0.25f, 1.08f);
                glyph.GetComponent<Renderer>().sharedMaterial = runeMat;
                Collider gc = glyph.GetComponent<Collider>();
                if (gc != null) DestroyImmediate(gc);
            }

            // Central Altar with Sacred Temple Pearl
            GameObject altar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            altar.name = "TempleCenterAltar";
            altar.transform.SetParent(templeGO.transform, false);
            altar.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            altar.transform.localScale = new Vector3(2.2f, 0.6f, 2.2f);
            altar.GetComponent<Renderer>().sharedMaterial = stoneMat;

            GameObject templePearl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            templePearl.name = "GlowingPearl_AncestralTemple";
            templePearl.transform.SetParent(templeGO.transform, false);
            templePearl.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            templePearl.transform.localScale = Vector3.one * 0.9f;
            GlowingPearl gp = templePearl.AddComponent<GlowingPearl>();
            spawnedPearls.Add(gp);

            // Ambient turquoise temple illumination
            GameObject tLightGO = new GameObject("TempleAtmosphericLight");
            tLightGO.transform.SetParent(templeGO.transform, false);
            tLightGO.transform.localPosition = new Vector3(0f, 3.5f, 0f);
            Light tl = tLightGO.AddComponent<Light>();
            tl.type = LightType.Point;
            tl.color = runeCol;
            tl.range = 24f;
            tl.intensity = 2.4f;
        }

        private void CreateLeviathanRibcage(Transform parent)
        {
            float rx = 14f;
            float rz = -16f;
            float ry = GetSeabedHeight(rx, rz);

            GameObject cageGO = new GameObject("LeviathanSkeletalRibcage");
            cageGO.transform.SetParent(parent, false);
            cageGO.transform.position = new Vector3(rx, ry, rz);

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material boneMat = new Material(uLit);
            boneMat.color = new Color(0.72f, 0.76f, 0.72f); // Fossilized marine bone
            boneMat.EnableKeyword("_EMISSION");
            boneMat.SetColor("_EmissionColor", new Color(0.08f, 0.25f, 0.22f)); // Algae patina glow
            boneMat.SetFloat("_Smoothness", 0.45f);

            int ribPairs = 8;
            float spacing = 3.8f;

            for (int r = 0; r < ribPairs; r++)
            {
                float zOffset = (r - ribPairs * 0.5f) * spacing;
                float ribSpan = 7.5f - Mathf.Abs(r - ribPairs * 0.5f) * 0.4f;
                float ribHeight = 7.0f - Mathf.Abs(r - ribPairs * 0.5f) * 0.35f;

                // Left Rib
                GameObject leftRib = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leftRib.name = $"Rib_L_{r}";
                leftRib.transform.SetParent(cageGO.transform, false);
                leftRib.transform.localPosition = new Vector3(-ribSpan * 0.5f, ribHeight * 0.5f, zOffset);
                leftRib.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
                leftRib.transform.localScale = new Vector3(0.45f, ribHeight * 0.55f, 0.45f);
                leftRib.GetComponent<Renderer>().sharedMaterial = boneMat;

                // Right Rib
                GameObject rightRib = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rightRib.name = $"Rib_R_{r}";
                rightRib.transform.SetParent(cageGO.transform, false);
                rightRib.transform.localPosition = new Vector3(ribSpan * 0.5f, ribHeight * 0.5f, zOffset);
                rightRib.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
                rightRib.transform.localScale = new Vector3(0.45f, ribHeight * 0.55f, 0.45f);
                rightRib.GetComponent<Renderer>().sharedMaterial = boneMat;
            }
        }

        private void CreateHydrothermalBlackSmokers(Transform parent)
        {
            Vector2[] ventCoords = new Vector2[]
            {
                new Vector2(-34f, -32f),
                new Vector2(-28f, -44f),
                new Vector2(-44f, -28f)
            };

            for (int i = 0; i < ventCoords.Length; i++)
            {
                float x = ventCoords[i].x;
                float z = ventCoords[i].y;
                float y = GetSeabedHeight(x, z);

                GameObject smokerGO = new GameObject($"BlackSmokerChimney_{i + 1}");
                smokerGO.transform.SetParent(parent, false);

                BlackSmokerChimney chimney = smokerGO.AddComponent<BlackSmokerChimney>();
                chimney.chimneyHeight = 11.0f + (i * 2.5f);
                chimney.InitializeChimney(new Vector3(x, y, z));
            }
        }

        private void CreateOceanJetStreams(Transform parent)
        {
            // Stream 1: Shallows to Abyss Highway
            GameObject stream1GO = new GameObject("OceanJetStream_ShallowsToAbyss");
            stream1GO.transform.SetParent(parent, false);
            OceanJetStream s1 = stream1GO.AddComponent<OceanJetStream>();
            s1.InitializeJetStream(new Vector3(22f, -8f, 22f), new Vector3(-26f, -36f, -24f));

            // Stream 2: Deep Abyss Return Highway
            GameObject stream2GO = new GameObject("OceanJetStream_AbyssReturn");
            stream2GO.transform.SetParent(parent, false);
            OceanJetStream s2 = stream2GO.AddComponent<OceanJetStream>();
            s2.InitializeJetStream(new Vector3(-36f, -46f, -36f), new Vector3(8f, -16f, -8f));
        }

        private void SpawnDeepSeaLanternSquidAndSiphonophores(Transform parent)
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material siphMat = new Material(uLit);
            Color siphCol = new Color(0.2f, 0.95f, 1f, 0.7f);
            siphMat.color = siphCol;
            siphMat.EnableKeyword("_EMISSION");
            siphMat.SetColor("_EmissionColor", siphCol * 3.2f);

            // 6 Glowing Siphonophore Chains in the Hadal Deep Trench
            Vector3[] siphPositions = new Vector3[]
            {
                new Vector3(-32f, -38f, -30f),
                new Vector3(-36f, -45f, -38f),
                new Vector3(-28f, -34f, -42f),
                new Vector3(-42f, -42f, -32f),
                new Vector3(-25f, -24f, -22f),
                new Vector3(-30f, -28f, -26f)
            };

            for (int s = 0; s < siphPositions.Length; s++)
            {
                GameObject siphChain = new GameObject($"SiphonophoreChain_{s + 1}");
                siphChain.transform.SetParent(parent, false);
                siphChain.transform.position = siphPositions[s];

                int nodes = 5;
                for (int n = 0; n < nodes; n++)
                {
                    GameObject bell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bell.name = $"Bell_{n}";
                    bell.transform.SetParent(siphChain.transform, false);
                    bell.transform.localPosition = new Vector3(0f, -n * 1.4f, 0f);
                    bell.transform.localScale = Vector3.one * (0.6f - n * 0.08f);
                    bell.GetComponent<Renderer>().sharedMaterial = siphMat;
                    Collider c = bell.GetComponent<Collider>();
                    if (c != null) DestroyImmediate(c);
                }

                OrganicSwayFlora sway = siphChain.AddComponent<OrganicSwayFlora>();
                sway.swayAngle = 12f;
                sway.swayFrequency = 0.8f;
            }
        }

        private void SpawnSkimwingMount(Transform parent)
        {
            GameObject skimwingGO = new GameObject("PandoranSkimwingMount");
            skimwingGO.transform.SetParent(parent, false);

            SkimwingMountAI skimwing = skimwingGO.AddComponent<SkimwingMountAI>();
            skimwing.InitializeSkimwing(new Vector3(16f, -3.5f, 18f));
        }

        private void SpawnTulkunWhale(Transform parent)
        {
            GameObject tulkunGO = new GameObject("PandoranTulkunSpiritWhale");
            tulkunGO.transform.SetParent(parent, false);

            TulkunCompanionAI tulkun = tulkunGO.AddComponent<TulkunCompanionAI>();
            tulkun.InitializeTulkun(new Vector3(-34f, -44f, -32f));
        }

        private void CreateVolcanicCratersAndHydrothermalVents(Transform parent)
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material basaltMat = new Material(uLit);
            basaltMat.color = new Color(0.06f, 0.08f, 0.11f); // Volcanic basalt rock
            basaltMat.SetFloat("_Smoothness", 0.15f);

            Material magmaMat = new Material(uLit);
            magmaMat.color = new Color(1f, 0.35f, 0.05f); // Molten fissure core
            magmaMat.EnableKeyword("_EMISSION");
            magmaMat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.05f) * 3.5f);

            Material cyanVentMat = new Material(uLit);
            cyanVentMat.color = new Color(0.05f, 0.95f, 0.85f); // Ethereal thermal fissure
            cyanVentMat.EnableKeyword("_EMISSION");
            cyanVentMat.SetColor("_EmissionColor", new Color(0.05f, 0.95f, 0.85f) * 3.5f);

            // 1. Sunken Caldera Craters with Rocky Basin Rims
            Vector3[] craterPositions = new Vector3[]
            {
                new Vector3(-14f, seaBedDepth, -10f),
                new Vector3(16f, seaBedDepth, 12f),
                new Vector3(-6f, seaBedDepth, 20f),
                new Vector3(20f, seaBedDepth, -14f)
            };

            for (int c = 0; c < craterPositions.Length; c++)
            {
                Vector3 center = craterPositions[c];
                center.y = GetSeabedHeight(center.x, center.z);
                GameObject craterGO = new GameObject($"VolcanicCrater_{c + 1}");
                craterGO.transform.SetParent(parent, false);
                craterGO.transform.position = center;

                // Center glowing thermal pool
                GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pool.name = "ThermalFissurePool";
                pool.transform.SetParent(craterGO.transform, false);
                pool.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                pool.transform.localScale = new Vector3(3.8f, 0.05f, 3.8f);
                pool.GetComponent<Renderer>().sharedMaterial = (c % 2 == 0) ? magmaMat : cyanVentMat;
                Collider pc = pool.GetComponent<Collider>();
                if (pc != null) DestroyImmediate(pc);

                // Rugged boulder rim forming the crater wall
                int rockCount = 12;
                float craterRadius = 3.6f;
                for (int r = 0; r < rockCount; r++)
                {
                    float angle = (r / (float)rockCount) * Mathf.PI * 2f;
                    Vector3 rockPos = new Vector3(Mathf.Cos(angle) * craterRadius, 0.35f, Mathf.Sin(angle) * craterRadius);

                    GameObject boulder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    boulder.name = $"BasaltRim_{r}";
                    boulder.transform.SetParent(craterGO.transform, false);
                    boulder.transform.localPosition = rockPos;
                    boulder.transform.localScale = new Vector3(UnityEngine.Random.Range(1.4f, 2.2f), UnityEngine.Random.Range(0.9f, 1.6f), UnityEngine.Random.Range(1.4f, 2.2f));
                    boulder.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(-25f, 25f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-25f, 25f));
                    boulder.GetComponent<Renderer>().sharedMaterial = basaltMat;
                }

                // Rising hydrothermal thermal bubble vent
                GameObject ventFX = new GameObject("ThermalBubbleVent");
                ventFX.transform.SetParent(craterGO.transform, false);
                ventFX.transform.localPosition = new Vector3(0f, 0.2f, 0f);

                ParticleSystem ps = ventFX.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.startLifetime = 6.5f;
                main.startSpeed = 1.6f;
                main.startSize = 0.2f;
                main.maxParticles = 45;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startColor = (c % 2 == 0) ? new Color(1f, 0.65f, 0.3f, 0.65f) : new Color(0.3f, 0.95f, 1f, 0.65f);

                var emission = ps.emission;
                emission.rateOverTime = 12f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 1.4f;

                UnderwaterParticleHelper.ApplyParticleMaterial(ps, main.startColor.color, true);
            }

            // 2. Hydrothermal Smoker Mineral Spire Chimneys
            Vector3[] spirePositions = new Vector3[]
            {
                new Vector3(-20f, seaBedDepth, 14f),
                new Vector3(9f, seaBedDepth, -20f),
                new Vector3(24f, seaBedDepth, 2f)
            };

            for (int s = 0; s < spirePositions.Length; s++)
            {
                Vector3 spireBase = spirePositions[s];
                GameObject spireGO = new GameObject($"HydrothermalChimney_{s + 1}");
                spireGO.transform.SetParent(parent, false);
                spireGO.transform.position = spireBase;

                float height = UnityEngine.Random.Range(5.5f, 8.5f);
                int segments = 4;
                for (int seg = 0; seg < segments; seg++)
                {
                    float t = seg / (float)segments;
                    float segY = t * height + (height / segments * 0.5f);
                    float segWidth = Mathf.Lerp(2.4f, 0.9f, t);

                    GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    chunk.name = $"ChimneySegment_{seg}";
                    chunk.transform.SetParent(spireGO.transform, false);
                    chunk.transform.localPosition = new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), segY, UnityEngine.Random.Range(-0.2f, 0.2f));
                    chunk.transform.localScale = new Vector3(segWidth, height / segments * 0.52f, segWidth);
                    chunk.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-6f, 6f));
                    chunk.GetComponent<Renderer>().sharedMaterial = basaltMat;
                }

                // Molten venting crown
                GameObject ventCrown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ventCrown.transform.SetParent(spireGO.transform, false);
                ventCrown.transform.localPosition = new Vector3(0f, height + 0.1f, 0f);
                ventCrown.transform.localScale = new Vector3(0.7f, 0.08f, 0.7f);
                ventCrown.GetComponent<Renderer>().sharedMaterial = magmaMat;
                Collider vc = ventCrown.GetComponent<Collider>();
                if (vc != null) DestroyImmediate(vc);
            }
        }

        private void CreateOrganicReefFormations(Transform parent)
        {
            bioluminescentCorals.Clear();

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Color[] pandoraColors = new Color[]
            {
                new Color(0.05f, 0.95f, 0.85f), // Electric Cyan
                new Color(0.95f, 0.2f, 0.85f),  // Fluorescent Magenta
                new Color(0.2f, 0.95f, 0.45f),  // Alien Emerald
                new Color(1f, 0.65f, 0.12f)     // Luminous Amber
            };

            // 1. True Curved Coral Archways (The player can swim and walk underneath!)
            Vector3[] archPositions = new Vector3[]
            {
                new Vector3(-10f, seaBedDepth, 8f),
                new Vector3(14f, seaBedDepth, -6f),
                new Vector3(-16f, seaBedDepth, -18f),
                new Vector3(18f, seaBedDepth, 16f)
            };

            float[] archYaws = new float[] { 25f, -40f, 65f, -15f };

            for (int i = 0; i < archPositions.Length; i++)
            {
                GameObject archRoot = new GameObject($"OrganicCoralArch_{i + 1}");
                archRoot.transform.SetParent(parent, false);
                archRoot.transform.position = archPositions[i];
                archRoot.transform.rotation = Quaternion.Euler(0f, archYaws[i], 0f);

                Color col = pandoraColors[i % pandoraColors.Length];
                Material coralMat = new Material(uLit);
                coralMat.color = col;
                coralMat.EnableKeyword("_EMISSION");
                coralMat.SetColor("_EmissionColor", col * 2.2f);

                float archSpan = 4.2f;
                float archHeight = 5.5f;

                // Left Pillar Arching Inward
                GameObject leftPillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leftPillar.name = "LeftArchPillar";
                leftPillar.transform.SetParent(archRoot.transform, false);
                leftPillar.transform.localPosition = new Vector3(-archSpan * 0.5f, archHeight * 0.5f, 0f);
                leftPillar.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
                leftPillar.transform.localScale = new Vector3(1.1f, archHeight * 0.52f, 1.1f);
                Renderer lr = leftPillar.GetComponent<Renderer>();
                lr.sharedMaterial = coralMat;
                bioluminescentCorals.Add(lr);

                // Right Pillar Arching Inward
                GameObject rightPillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rightPillar.name = "RightArchPillar";
                rightPillar.transform.SetParent(archRoot.transform, false);
                rightPillar.transform.localPosition = new Vector3(archSpan * 0.5f, archHeight * 0.5f, 0f);
                rightPillar.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
                rightPillar.transform.localScale = new Vector3(1.1f, archHeight * 0.52f, 1.1f);
                Renderer rr = rightPillar.GetComponent<Renderer>();
                rr.sharedMaterial = coralMat;
                bioluminescentCorals.Add(rr);

                // Overhead Keystone Span
                GameObject keystone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                keystone.name = "ArchKeystone";
                keystone.transform.SetParent(archRoot.transform, false);
                keystone.transform.localPosition = new Vector3(0f, archHeight, 0f);
                keystone.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                keystone.transform.localScale = new Vector3(1.2f, archSpan * 0.55f, 1.2f);
                Renderer kr = keystone.GetComponent<Renderer>();
                kr.sharedMaterial = coralMat;
                bioluminescentCorals.Add(kr);

                // Antler Coral Polyps Clusters on Arch
                for (int b = 0; b < 4; b++)
                {
                    GameObject branch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    branch.name = $"CoralNodule_{b}";
                    branch.transform.SetParent(keystone.transform, false);
                    branch.transform.localPosition = new Vector3(0f, UnityEngine.Random.Range(-0.8f, 0.8f), 0.6f);
                    branch.transform.localRotation = Quaternion.Euler(UnityEngine.Random.Range(-25f, 25f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-25f, 25f));
                    branch.transform.localScale = new Vector3(0.25f, 0.8f, 0.25f);
                    Renderer br = branch.GetComponent<Renderer>();
                    br.sharedMaterial = coralMat;
                    bioluminescentCorals.Add(br);
                    Collider bc = branch.GetComponent<Collider>();
                    if (bc != null) DestroyImmediate(bc);
                }
            }

            // 2. Branching Staghorn Corals
            for (int s = 0; s < 8; s++)
            {
                float angle = (s / 8f) * Mathf.PI * 2f + 0.3f;
                float dist = UnityEngine.Random.Range(12f, worldRadius - 10f);
                Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, seaBedDepth, Mathf.Sin(angle) * dist);

                GameObject staghornGO = new GameObject($"StaghornCoral_{s + 1}");
                staghornGO.transform.SetParent(parent, false);
                staghornGO.transform.position = pos;

                Color sCol = pandoraColors[(s + 1) % pandoraColors.Length];
                Material sMat = new Material(uLit);
                sMat.color = sCol;
                sMat.EnableKeyword("_EMISSION");
                sMat.SetColor("_EmissionColor", sCol * 2.4f);

                // Base trunk
                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.transform.SetParent(staghornGO.transform, false);
                trunk.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                trunk.transform.localScale = new Vector3(0.55f, 1.2f, 0.55f);
                Renderer tr = trunk.GetComponent<Renderer>();
                tr.sharedMaterial = sMat;
                bioluminescentCorals.Add(tr);

                // Branching antlers
                for (int a = 0; a < 3; a++)
                {
                    float bAngle = (a / 3f) * Mathf.PI * 2f;
                    Vector3 bDir = new Vector3(Mathf.Cos(bAngle) * 0.4f, 0.8f, Mathf.Sin(bAngle) * 0.4f).normalized;

                    GameObject antler = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    antler.transform.SetParent(staghornGO.transform, false);
                    antler.transform.localPosition = new Vector3(0f, 2.0f, 0f) + bDir * 0.7f;
                    antler.transform.rotation = Quaternion.LookRotation(bDir) * Quaternion.Euler(90f, 0f, 0f);
                    antler.transform.localScale = new Vector3(0.22f, 0.9f, 0.22f);
                    Renderer ar = antler.GetComponent<Renderer>();
                    ar.sharedMaterial = sMat;
                    bioluminescentCorals.Add(ar);
                    Collider ac = antler.GetComponent<Collider>();
                    if (ac != null) DestroyImmediate(ac);
                }
            }

            // 3. Layered Table / Shelf Corals (Acropora plates)
            for (int t = 0; t < 8; t++)
            {
                float tAngle = (t / 8f) * Mathf.PI * 2f + 0.6f;
                float tDist = UnityEngine.Random.Range(8f, worldRadius - 14f);
                float tx = Mathf.Cos(tAngle) * tDist;
                float tz = Mathf.Sin(tAngle) * tDist;
                Vector3 tPos = new Vector3(tx, GetSeabedHeight(tx, tz), tz);

                GameObject tableGO = new GameObject($"TableCoral_{t + 1}");
                tableGO.transform.SetParent(parent, false);
                tableGO.transform.position = tPos;

                Color tCol = pandoraColors[(t + 2) % pandoraColors.Length];
                Material tMat = new Material(uLit);
                tMat.color = tCol;
                tMat.EnableKeyword("_EMISSION");
                tMat.SetColor("_EmissionColor", tCol * 2.0f);

                // Central pedestal
                GameObject ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ped.transform.SetParent(tableGO.transform, false);
                ped.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                ped.transform.localScale = new Vector3(0.6f, 1.4f, 0.6f);
                ped.GetComponent<Renderer>().sharedMaterial = tMat;

                // Two tiered horizontal plates
                for (int p = 0; p < 2; p++)
                {
                    GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    plate.transform.SetParent(tableGO.transform, false);
                    plate.transform.localPosition = new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 1.8f + (p * 1.1f), UnityEngine.Random.Range(-0.3f, 0.3f));
                    plate.transform.localScale = new Vector3(2.6f - (p * 0.6f), 0.12f, 2.6f - (p * 0.6f));
                    Renderer pr = plate.GetComponent<Renderer>();
                    pr.sharedMaterial = tMat;
                    bioluminescentCorals.Add(pr);
                }
            }
        }

        private void CreateGlowingMushroomCorals(Transform parent)
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Color shroomColor = new Color(0.1f, 0.85f, 1f);
            Material shroomMat = new Material(uLit);
            shroomMat.color = shroomColor;
            shroomMat.EnableKeyword("_EMISSION");
            shroomMat.SetColor("_EmissionColor", shroomColor * 3.0f);

            Material stalkMat = new Material(uLit);
            stalkMat.color = new Color(0.08f, 0.14f, 0.22f);

            for (int i = 0; i < 16; i++)
            {
                float x = UnityEngine.Random.Range(-worldRadius * 0.6f, worldRadius * 0.6f);
                float z = UnityEngine.Random.Range(-worldRadius * 0.6f, worldRadius * 0.6f);

                GameObject shroomGO = new GameObject($"GlowingShroom_{i}");
                shroomGO.transform.SetParent(parent, false);
                shroomGO.transform.position = new Vector3(x, GetSeabedHeight(x, z), z);

                // Textured stalk
                GameObject stalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stalk.transform.SetParent(shroomGO.transform, false);
                stalk.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                stalk.transform.localScale = new Vector3(0.35f, 0.65f, 0.35f);
                stalk.GetComponent<Renderer>().sharedMaterial = stalkMat;
                Collider sc = stalk.GetComponent<Collider>();
                if (sc != null) DestroyImmediate(sc);

                // Bioluminescent Cap
                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = $"GlowingMushroomCap_{i}";
                cap.transform.SetParent(shroomGO.transform, false);
                cap.transform.localPosition = new Vector3(0f, 1.35f, 0f);
                cap.transform.localScale = new Vector3(2.2f, 0.45f, 2.2f);
                cap.GetComponent<Renderer>().sharedMaterial = shroomMat;

                Collider c = cap.GetComponent<Collider>();
                if (c != null) DestroyImmediate(c);
            }
        }

        private void CreateRealisticKelpForest(Transform parent)
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material kelpMat = new Material(uLit);
            kelpMat.color = new Color(0.06f, 0.55f, 0.38f); // Translucent deep sea emerald
            kelpMat.EnableKeyword("_EMISSION");
            kelpMat.SetColor("_EmissionColor", new Color(0.04f, 0.45f, 0.32f) * 1.8f);
            kelpMat.SetFloat("_Smoothness", 0.7f);

            Material rockMat = new Material(uLit);
            rockMat.color = new Color(0.08f, 0.12f, 0.14f);

            int clusterCount = 18;
            for (int i = 0; i < clusterCount; i++)
            {
                float x = UnityEngine.Random.Range(-worldRadius * 0.75f, worldRadius * 0.75f);
                float z = UnityEngine.Random.Range(-worldRadius * 0.75f, worldRadius * 0.75f);

                GameObject kelpPlant = new GameObject($"KelpPlant_{i + 1}");
                kelpPlant.transform.SetParent(parent, false);
                kelpPlant.transform.position = new Vector3(x, GetSeabedHeight(x, z), z);

                // Holdfast rock anchor
                GameObject holdfast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                holdfast.name = "HoldfastRock";
                holdfast.transform.SetParent(kelpPlant.transform, false);
                holdfast.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                holdfast.transform.localScale = new Vector3(1.2f, 0.6f, 1.2f);
                holdfast.GetComponent<Renderer>().sharedMaterial = rockMat;

                // 2 Curving stalks per plant
                for (int s = 0; s < 2; s++)
                {
                    float stalkHeight = UnityEngine.Random.Range(8f, 14f);
                    GameObject stalkRoot = new GameObject($"Stalk_{s}");
                    stalkRoot.transform.SetParent(kelpPlant.transform, false);
                    stalkRoot.transform.localPosition = new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), 0.5f, UnityEngine.Random.Range(-0.4f, 0.4f));

                    // Segmented stalk
                    int segs = 3;
                    Transform prevParent = stalkRoot.transform;
                    for (int seg = 0; seg < segs; seg++)
                    {
                        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        stem.name = $"StemSeg_{seg}";
                        stem.transform.SetParent(prevParent, false);
                        stem.transform.localPosition = new Vector3(0f, stalkHeight / segs * 0.5f, 0f);
                        stem.transform.localRotation = Quaternion.Euler(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-6f, 6f));
                        stem.transform.localScale = new Vector3(0.14f, stalkHeight / segs * 0.5f, 0.14f);
                        stem.GetComponent<Renderer>().sharedMaterial = kelpMat;
                        Collider c = stem.GetComponent<Collider>();
                        if (c != null) DestroyImmediate(c);

                        // Attach swaying blade leaves
                        GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        blade.name = "KelpBlade";
                        blade.transform.SetParent(stem.transform, false);
                        blade.transform.localPosition = new Vector3(0.5f, 0.2f, 0f);
                        blade.transform.localRotation = Quaternion.Euler(15f, 0f, 25f);
                        blade.transform.localScale = new Vector3(2.4f, 0.05f, 0.75f);
                        blade.GetComponent<Renderer>().sharedMaterial = kelpMat;
                        Collider bc = blade.GetComponent<Collider>();
                        if (bc != null) DestroyImmediate(bc);

                        OrganicSwayFlora sway = blade.AddComponent<OrganicSwayFlora>();
                        sway.swayFrequency = UnityEngine.Random.Range(1.0f, 1.5f);
                        sway.swayAngle = UnityEngine.Random.Range(8f, 16f);

                        prevParent = stem.transform;
                    }
                }
            }
        }

        private void CreateAbyssalGrottoCave(Transform parent)
        {
            GameObject caveRoot = new GameObject("AbyssalBioluminescentGrotto");
            caveRoot.transform.SetParent(parent, false);

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material basaltMat = new Material(uLit);
            basaltMat.color = new Color(0.04f, 0.06f, 0.10f);
            basaltMat.SetFloat("_Smoothness", 0.25f);

            Material crystalMat = new Material(uLit);
            Color crystalCol = new Color(0.82f, 0.25f, 1f); // Radiant Amethyst / Violet
            crystalMat.color = crystalCol;
            crystalMat.EnableKeyword("_EMISSION");
            crystalMat.SetColor("_EmissionColor", crystalCol * 3.8f);
            crystalMat.SetFloat("_Smoothness", 0.95f);

            // Grotto Tunnel: Descending curve from (-20, seabed, -16) into (-32, seabed - 5.5m, -28)
            Vector3[] tunnelWaypoints = new Vector3[]
            {
                new Vector3(-20f, seaBedDepth + 0.5f, -16f),
                new Vector3(-23f, seaBedDepth - 0.8f, -19f),
                new Vector3(-26f, seaBedDepth - 2.2f, -22f),
                new Vector3(-29f, seaBedDepth - 3.8f, -25f),
                new Vector3(-33f, seaBedDepth - 5.2f, -29f)
            };

            for (int i = 0; i < tunnelWaypoints.Length; i++)
            {
                Vector3 center = tunnelWaypoints[i];
                float archRadius = 4.2f + (i == tunnelWaypoints.Length - 1 ? 2.5f : 0f); // Expands into inner sanctum chamber

                // Cavern Archway Ribs (Left & Right basalt pillars)
                GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leftWall.name = $"GrottoArch_L_{i}";
                leftWall.transform.SetParent(caveRoot.transform, false);
                leftWall.transform.position = center + new Vector3(-archRadius * 0.7f, 1.8f, 0f);
                leftWall.transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
                leftWall.transform.localScale = new Vector3(1.8f, 3.2f, 2.2f);
                leftWall.GetComponent<Renderer>().sharedMaterial = basaltMat;

                GameObject rightWall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rightWall.name = $"GrottoArch_R_{i}";
                rightWall.transform.SetParent(caveRoot.transform, false);
                rightWall.transform.position = center + new Vector3(archRadius * 0.7f, 1.8f, 0f);
                rightWall.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
                rightWall.transform.localScale = new Vector3(1.8f, 3.2f, 2.2f);
                rightWall.GetComponent<Renderer>().sharedMaterial = basaltMat;

                // Overhead Cavern Ceiling Dome
                GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ceiling.name = $"GrottoCeiling_{i}";
                ceiling.transform.SetParent(caveRoot.transform, false);
                ceiling.transform.position = center + new Vector3(0f, 4.2f, 0f);
                ceiling.transform.localScale = new Vector3(archRadius * 1.8f, 2.2f, 4.5f);
                ceiling.GetComponent<Renderer>().sharedMaterial = basaltMat;

                // Cavern Floor Trench Slab
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = $"GrottoFloor_{i}";
                floor.transform.SetParent(caveRoot.transform, false);
                floor.transform.position = center - new Vector3(0f, 0.4f, 0f);
                floor.transform.localScale = new Vector3(archRadius * 1.6f, 0.8f, 5.0f);
                floor.GetComponent<Renderer>().sharedMaterial = basaltMat;

                // Hanging Violet Crystal Stalactites from ceiling
                for (int c = 0; c < 3; c++)
                {
                    GameObject stalactite = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    stalactite.name = $"CrystalStalactite_{i}_{c}";
                    stalactite.transform.SetParent(caveRoot.transform, false);
                    stalactite.transform.position = center + new Vector3(UnityEngine.Random.Range(-2f, 2f), 3.4f, UnityEngine.Random.Range(-1.5f, 1.5f));
                    stalactite.transform.localRotation = Quaternion.Euler(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-10f, 10f));
                    stalactite.transform.localScale = new Vector3(0.18f, UnityEngine.Random.Range(1.2f, 2.2f), 0.18f);
                    stalactite.GetComponent<Renderer>().sharedMaterial = crystalMat;
                    Collider col = stalactite.GetComponent<Collider>();
                    if (col != null) DestroyImmediate(col);
                }

                // Rising Crystal Stalagmites from floor
                GameObject stalagmite = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stalagmite.name = $"CrystalStalagmite_{i}";
                stalagmite.transform.SetParent(caveRoot.transform, false);
                stalagmite.transform.position = center + new Vector3(UnityEngine.Random.Range(-2.2f, 2.2f), 0.8f, UnityEngine.Random.Range(-1f, 1f));
                stalagmite.transform.localScale = new Vector3(0.22f, UnityEngine.Random.Range(0.8f, 1.5f), 0.22f);
                stalagmite.GetComponent<Renderer>().sharedMaterial = crystalMat;
                Collider scol = stalagmite.GetComponent<Collider>();
                if (scol != null) DestroyImmediate(scol);
            }

            // Inner Sanctum Chamber: at tunnel end (-33, seabed - 5.2m, -29)
            Vector3 sanctumPos = tunnelWaypoints[tunnelWaypoints.Length - 1];

            // Deep Amethyst Cavern Point Light
            GameObject lightGO = new GameObject("GrottoAmethystLight");
            lightGO.transform.SetParent(caveRoot.transform, false);
            lightGO.transform.position = sanctumPos + new Vector3(0f, 2.5f, 0f);
            Light grottoLight = lightGO.AddComponent<Light>();
            grottoLight.type = LightType.Point;
            grottoLight.color = crystalCol;
            grottoLight.range = 28f;
            grottoLight.intensity = 2.4f;

            // Ancient Altar Pedestal in Sanctum
            GameObject altarBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            altarBase.name = "AncientShrinePedestal";
            altarBase.transform.SetParent(caveRoot.transform, false);
            altarBase.transform.position = sanctumPos + new Vector3(0f, 0.4f, 0f);
            altarBase.transform.localScale = new Vector3(2.0f, 0.4f, 2.0f);
            altarBase.GetComponent<Renderer>().sharedMaterial = basaltMat;

            // Hidden Ancient Pearl of the Deep Grotto
            GameObject hiddenPearl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hiddenPearl.name = "GlowingPearl_AncientGrotto";
            hiddenPearl.transform.SetParent(caveRoot.transform, false);
            hiddenPearl.transform.position = sanctumPos + new Vector3(0f, 1.25f, 0f);
            hiddenPearl.transform.localScale = Vector3.one * 0.9f;

            GlowingPearl pearlComp = hiddenPearl.AddComponent<GlowingPearl>();
            spawnedPearls.Add(pearlComp);
        }

        private void CreateAtokirinaSpores(Transform parent)
        {
            GameObject sporesGO = new GameObject("AtokirinaOceanicSpores");
            sporesGO.transform.SetParent(parent, false);
            sporesGO.transform.position = new Vector3(0f, -10f, 0f);

            ParticleSystem ps = sporesGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 12f;
            main.startSpeed = 0.35f;
            main.startSize = 0.12f;
            main.maxParticles = 350;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.4f, 0.95f, 1f, 0.65f); // Ethereal cyan glow

            var emission = ps.emission;
            emission.rateOverTime = 35f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(worldRadius * 1.6f, 22f, worldRadius * 1.6f);

            UnderwaterParticleHelper.ApplyParticleMaterial(ps, new Color(0.4f, 0.95f, 1f, 0.65f), true);
        }

        private void SpawnHelicoradianAnemones(Transform parent)
        {
            Vector3[] anemonePositions = new Vector3[]
            {
                new Vector3(-7f, seaBedDepth + 0.1f, 8f),
                new Vector3(12f, seaBedDepth + 0.1f, -6f),
                new Vector3(-15f, seaBedDepth + 0.1f, -12f),
                new Vector3(6f, seaBedDepth + 0.1f, 14f)
            };

            Color[] colors = new Color[]
            {
                new Color(1f, 0.55f, 0.1f),   // Amber
                new Color(0.1f, 0.95f, 0.9f),  // Cyan
                new Color(0.95f, 0.2f, 0.85f), // Magenta
                new Color(0.3f, 1f, 0.45f)    // Emerald
            };

            for (int i = 0; i < anemonePositions.Length; i++)
            {
                GameObject anemoneGO = new GameObject($"HelicoradianAnemone_{i + 1}");
                anemoneGO.transform.SetParent(parent, false);
                HelicoradianAnemone anemone = anemoneGO.AddComponent<HelicoradianAnemone>();
                anemone.InitializeAnemone(anemonePositions[i], colors[i % colors.Length]);
            }
        }

        private void SpawnGorgonianSeaFans(Transform parent)
        {
            Color[] fanColors = new Color[]
            {
                new Color(0.95f, 0.22f, 0.82f), // Radiant Magenta
                new Color(0.12f, 0.95f, 0.9f),  // Electric Cyan
                new Color(1f, 0.65f, 0.15f),    // Sunken Gold
                new Color(0.35f, 0.35f, 1f)     // Bioluminescent Periwinkle
            };

            Vector2[] fanCoords = new Vector2[]
            {
                new Vector2(-12f, 16f), new Vector2(-6f, -14f), new Vector2(14f, 8f),
                new Vector2(8f, -18f), new Vector2(-22f, 4f), new Vector2(22f, -6f),
                new Vector2(-15f, -8f), new Vector2(5f, 20f), new Vector2(-5f, 24f),
                new Vector2(18f, 16f)
            };

            for (int i = 0; i < fanCoords.Length; i++)
            {
                float x = fanCoords[i].x;
                float z = fanCoords[i].y;
                float y = GetSeabedHeight(x, z);

                GameObject fanGO = new GameObject($"GorgonianSeaFan_{i + 1}");
                fanGO.transform.SetParent(parent, false);

                Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                GorgonianSeaFan fan = fanGO.AddComponent<GorgonianSeaFan>();
                fan.InitializeSeaFan(new Vector3(x, y + 0.15f, z), rot, fanColors[i % fanColors.Length], UnityEngine.Random.Range(0.85f, 1.35f));
            }
        }

        private void SpawnSpiritTreeBeacons(Transform parent)
        {
            spawnedBeacons.Clear();
            Vector2[] treeCoords = new Vector2[]
            {
                new Vector2(-16f, 14f),
                new Vector2(18f, -12f)
            };

            for (int i = 0; i < treeCoords.Length; i++)
            {
                Vector3 treePos = new Vector3(treeCoords[i].x, GetSeabedHeight(treeCoords[i].x, treeCoords[i].y) + 0.1f, treeCoords[i].y);
                GameObject treeGO = new GameObject($"MetkayinaSpiritTree_{i + 1}");
                treeGO.transform.SetParent(parent, false);

                SpiritTreeBeacon tree = treeGO.AddComponent<SpiritTreeBeacon>();
                tree.InitializeSpiritTree(treePos);
                spawnedBeacons.Add(tree);
            }
        }

        private void SpawnMetkayinaAltar(Transform parent)
        {
            GameObject altarGO = new GameObject("MetkayinaPearlAltar");
            altarGO.transform.SetParent(parent, false);

            MetkayinaAltar altar = altarGO.AddComponent<MetkayinaAltar>();
            float altarY = GetSeabedHeight(10f, -8f);
            altar.InitializeAltar(new Vector3(10f, altarY, -8f));
        }

        private void SpawnGiantClams(Transform parent)
        {
            Vector2[] clamCoords = new Vector2[]
            {
                new Vector2(-8f, 7f),
                new Vector2(15f, -14f),
                new Vector2(-18f, -9f)
            };

            for (int i = 0; i < clamCoords.Length; i++)
            {
                float x = clamCoords[i].x;
                float z = clamCoords[i].y;
                float y = GetSeabedHeight(x, z);

                GameObject clamGO = new GameObject($"GiantTridacnaClam_{i + 1}");
                clamGO.transform.SetParent(parent, false);

                Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                SeabedGiantClam clam = clamGO.AddComponent<SeabedGiantClam>();
                clam.InitializeClam(new Vector3(x, y + 0.15f, z), rot);
            }
        }

        private void SpawnPearls(Transform parent)
        {
            spawnedPearls.Clear();

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Material oysterMat = new Material(uLit);
            oysterMat.color = new Color(0.12f, 0.22f, 0.32f);
            oysterMat.EnableKeyword("_EMISSION");
            oysterMat.SetColor("_EmissionColor", new Color(0.1f, 0.6f, 0.8f) * 1.5f);

            // Combination of Seabed Pearls (for walking exploration) and Reef Column Pearls (for swimming)
            Vector3[] pearlPositions = new Vector3[]
            {
                // Seabed pearls resting on the ocean floor in oyster nests
                new Vector3(14f, seaBedDepth + 0.85f, 12f),
                new Vector3(-16f, seaBedDepth + 0.85f, 8f),
                new Vector3(6f, seaBedDepth + 0.85f, -16f),
                new Vector3(-12f, seaBedDepth + 0.85f, -14f),
                new Vector3(0f, seaBedDepth + 0.85f, 18f),
                // Mid-water column pearls
                new Vector3(4f, -7f, 7f),
                new Vector3(-9f, -12f, -6f)
            };

            for (int i = 0; i < pearlPositions.Length; i++)
            {
                Vector3 pos = pearlPositions[i];
                bool isSeabedPearl = i < 5;

                // For seabed pearls, adapt dynamically to the undulating bathymetric dunes
                if (isSeabedPearl)
                {
                    float duneY = GetSeabedHeight(pos.x, pos.z);
                    pos.y = duneY + 0.85f;

                    GameObject oyster = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    oyster.name = $"BioluminescentOyster_{i + 1}";
                    oyster.transform.SetParent(parent, false);
                    oyster.transform.position = new Vector3(pos.x, duneY + 0.12f, pos.z);
                    oyster.transform.localScale = new Vector3(1.6f, 0.12f, 1.6f);
                    oyster.GetComponent<Renderer>().sharedMaterial = oysterMat;
                    Collider c = oyster.GetComponent<Collider>();
                    if (c != null) DestroyImmediate(c);
                }

                GameObject pearlGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pearlGO.name = $"GlowingPearl_{i + 1}";
                pearlGO.transform.SetParent(parent, false);
                pearlGO.transform.position = pos;
                pearlGO.transform.localScale = Vector3.one * 0.75f;

                GlowingPearl pearl = pearlGO.AddComponent<GlowingPearl>();
                spawnedPearls.Add(pearl);
            }
        }

        private void SpawnFishSchools(Transform parent)
        {
            Vector3[] schoolLocations = new Vector3[]
            {
                new Vector3(-6f, -8f, 5f),
                new Vector3(10f, -12f, -8f),
                new Vector3(-12f, seaBedDepth + 5f, 10f)
            };

            Color[] schoolColors = new Color[]
            {
                new Color(0.1f, 0.95f, 0.9f),  // Pandoran Neon Cyan
                new Color(0.95f, 0.25f, 0.85f),// Fluorescent Magenta
                new Color(1f, 0.7f, 0.1f)      // Glowing Gold
            };

            for (int i = 0; i < schoolLocations.Length; i++)
            {
                GameObject schoolGO = new GameObject($"SchoolOfFish_{i + 1}");
                schoolGO.transform.SetParent(parent, false);
                SchoolingFish school = schoolGO.AddComponent<SchoolingFish>();
                school.InitializeSchool(schoolLocations[i], schoolColors[i % schoolColors.Length], 18);
            }
        }

        private void SpawnSeaTurtles(Transform parent)
        {
            Vector3[] turtleSpawns = new Vector3[]
            {
                new Vector3(12f, seaBedDepth + 6f, 10f),
                new Vector3(-14f, seaBedDepth + 8f, -12f),
                new Vector3(4f, seaBedDepth + 12f, -16f)
            };

            for (int i = 0; i < turtleSpawns.Length; i++)
            {
                GameObject turtleGO = new GameObject($"PandoranSeaTurtle_{i + 1}");
                turtleGO.transform.SetParent(parent, false);

                PandoranSeaTurtleAI turtle = turtleGO.AddComponent<PandoranSeaTurtleAI>();
                turtle.InitializeTurtle(turtleSpawns[i]);
            }
        }

        private void SpawnJellyfishHazards(Transform parent)
        {
            Vector3[] jellyPositions = new Vector3[]
            {
                new Vector3(-16f, seaBedDepth + 4f, 8f),
                new Vector3(10f, seaBedDepth + 3.5f, 13f),
                new Vector3(2f, -12f, -14f)
            };

            for (int i = 0; i < jellyPositions.Length; i++)
            {
                GameObject jellyGO = new GameObject($"BioluminescentJellyfish_{i + 1}");
                jellyGO.transform.SetParent(parent, false);
                JellyfishHazard hazard = jellyGO.AddComponent<JellyfishHazard>();
                hazard.Initialize(jellyPositions[i]);
            }
        }

        private void SpawnApexPredator(Transform parent)
        {
            // Submarine predator disabled per user preference
        }

        private void SpawnMantaCompanion(Transform parent)
        {
            GameObject mantaGO = new GameObject("PandoranIluCompanion");
            mantaGO.transform.SetParent(parent, false);

            MantaCompanionAI manta = mantaGO.AddComponent<MantaCompanionAI>();
            manta.InitializeManta(new Vector3(12f, -7f, 10f));
        }

        private void SpawnOceanicRelics(Transform parent)
        {
            Vector3[] relicPositions = new Vector3[]
            {
                new Vector3(-8f, seaBedDepth + 1.2f, 16f),
                new Vector3(14f, seaBedDepth + 1.2f, -16f)
            };

            string[] titles = new string[]
            {
                "Pandoran Neural Symbiosis",
                "Metkayina Way of Water"
            };

            string[] dataLogs = new string[]
            {
                "TSAHEYLU COGNITION: Pandoran marine flora exchange electrical potentials through root mycorrhizae, mimicking an oceanic neural synapse.",
                "METKAYINA BREATH CONTROL: To bond with the ocean, Na'vi slow their heart rate to mimic the heartbeat of Eywa, achieving neutral buoyancy and infinite calm."
            };

            for (int i = 0; i < relicPositions.Length; i++)
            {
                GameObject relicGO = new GameObject($"PandoranRelic_{i + 1}");
                relicGO.transform.SetParent(parent, false);
                relicGO.transform.position = relicPositions[i];

                OceanicRelic relic = relicGO.AddComponent<OceanicRelic>();
                relic.relicTitle = titles[i % titles.Length];
                relic.scientificData = dataLogs[i % dataLogs.Length];
            }
        }

        private void SpawnSurfaceGate(Transform parent)
        {
            GameObject gateGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gateGO.name = "SurfaceGate";
            gateGO.transform.SetParent(parent, false);
            gateGO.transform.position = new Vector3(0f, -0.6f, 0f);
            gateGO.transform.localScale = new Vector3(6.5f, 0.2f, 6.5f);

            Light gateLight = gateGO.AddComponent<Light>();
            gateLight.type = LightType.Point;
            gateLight.range = 22f;

            surfaceGate = gateGO.AddComponent<SurfaceGate>();
            surfaceGate.SetupComponents(gateGO.GetComponent<Renderer>(), gateLight);

            Collider col = gateGO.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        public void RestoreCoralReefColor()
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Color[] radiantColors = new Color[]
            {
                new Color(0.1f, 1f, 0.9f),   // Ultra Cyan
                new Color(1f, 0.2f, 0.9f),   // Radiant Magenta
                new Color(0.3f, 1f, 0.4f),   // Bio Emerald
                new Color(1f, 0.7f, 0.15f)   // Luminous Gold
            };

            for (int i = 0; i < bioluminescentCorals.Count; i++)
            {
                if (bioluminescentCorals[i] != null)
                {
                    Color col = radiantColors[i % radiantColors.Length];
                    Material mat = new Material(uLit);
                    mat.color = col;
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", col * 3.5f);
                    bioluminescentCorals[i].material = mat;
                }
            }
        }
    }
}
