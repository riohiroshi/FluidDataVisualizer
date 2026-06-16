using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a dense miniature cityscape around a central sphere.
/// Buildings are tightly packed into city blocks separated by narrow streets.
/// Uses CombineMeshes per material per block for rendering performance.
/// </summary>
public class CityscapeGenerator : MonoBehaviour
{
    [Header("City Layout")]
    [Tooltip("Center point of the city (sphere position)")]
    [SerializeField] private Vector3 cityCenter = new Vector3(0f, 1f, 0f);

    [Tooltip("Total city radius from center")]
    [SerializeField] private float cityRadius = 22f;

    [Tooltip("Clear zone radius around the sphere (no buildings)")]
    [SerializeField] private float clearRadius = 3.0f;

    [Header("Block Grid")]
    [Tooltip("City block size (each block is a cluster of buildings)")]
    [SerializeField] private float blockSize = 3.0f;

    [Tooltip("Street width between blocks")]
    [SerializeField] private float streetWidth = 0.25f;

    [Tooltip("Grid cells per block side (e.g. 5 = 5x5 = 25 potential buildings per block)")]
    [SerializeField] private int cellsPerBlock = 5;

    [Tooltip("Tiny gap between buildings within a block")]
    [SerializeField] private float innerGap = 0.04f;

    [Header("Building Heights")]
    [Tooltip("Base minimum height")]
    [SerializeField] private float minHeight = 0.15f;

    [Tooltip("Base maximum height for normal buildings")]
    [SerializeField] private float maxHeight = 3.5f;

    [Tooltip("Power curve exponent — higher = more short buildings")]
    [SerializeField] private float heightExponent = 3.0f;

    [Tooltip("Chance per block to spawn one tall landmark tower")]
    [Range(0f, 0.5f)]
    [SerializeField] private float landmarkChance = 0.07f;

    [Tooltip("Landmark height range min")]
    [SerializeField] private float landmarkMinHeight = 6f;

    [Tooltip("Landmark height range max")]
    [SerializeField] private float landmarkMaxHeight = 12f;

    [Tooltip("Chance per block to spawn a medium-tall building cluster")]
    [Range(0f, 0.5f)]
    [SerializeField] private float clusterChance = 0.15f;

    [Header("Multi-Cell Buildings")]
    [Tooltip("Chance a building occupies 2 cells (1x2 or 2x1 footprint)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float doubleCellChance = 0.12f;

    [Tooltip("Chance a building occupies 4 cells (2x2 footprint)")]
    [Range(0f, 0.3f)]
    [SerializeField] private float quadCellChance = 0.05f;

    [Header("Ground")]
    [SerializeField] private float groundY = -0.5f;
    [SerializeField] private float groundScale = 6f;

    [Header("Visuals")]
    [Tooltip("Building base colors (mostly grays for Arknights feel)")]
    [SerializeField] private Color[] buildingColors = new Color[]
    {
        new Color(0.62f, 0.64f, 0.67f), // Light steel
        new Color(0.52f, 0.54f, 0.57f), // Mid gray
        new Color(0.42f, 0.44f, 0.47f), // Darker gray
        new Color(0.72f, 0.73f, 0.75f), // Near white
        new Color(0.32f, 0.34f, 0.37f), // Dark
        new Color(0.57f, 0.59f, 0.63f), // Blue gray
        new Color(0.48f, 0.50f, 0.54f), // Cool gray
    };

    [Tooltip("Accent colors (rare, for visual interest)")]
    [SerializeField] private Color[] accentColors = new Color[]
    {
        new Color(0.22f, 0.38f, 0.58f), // Steel blue
        new Color(0.18f, 0.42f, 0.48f), // Teal
    };

    [Tooltip("Chance for accent color")]
    [Range(0f, 0.2f)]
    [SerializeField] private float accentChance = 0.03f;

    [SerializeField] private Color groundColor = new Color(0.28f, 0.30f, 0.33f);

    [Header("Generation")]
    [SerializeField] private int seed = 42;

    [Tooltip("Chance to skip a single cell (creates air gaps/plazas)")]
    [Range(0f, 0.15f)]
    [SerializeField] private float skipChance = 0.04f;

    private Transform rootTransform;
    private readonly List<Material> materialsCache = new List<Material>();
    private Material groundMat;

    public void ApplyDemoPreset()
    {
        cityCenter = Vector3.zero;
        cityRadius = 18.5f;
        clearRadius = 3.55f;
        blockSize = 2.45f;
        streetWidth = 0.42f;
        cellsPerBlock = 5;
        innerGap = 0.035f;
        minHeight = 0.08f;
        maxHeight = 0.95f;
        heightExponent = 2.6f;
        landmarkChance = 0.045f;
        landmarkMinHeight = 1.35f;
        landmarkMaxHeight = 3.2f;
        clusterChance = 0.11f;
        doubleCellChance = 0.16f;
        quadCellChance = 0.08f;
        groundY = -0.52f;
        groundScale = 4.8f;
        buildingColors = new[]
        {
            new Color(0.09f, 0.10f, 0.12f),
            new Color(0.13f, 0.14f, 0.17f),
            new Color(0.18f, 0.19f, 0.22f),
            new Color(0.22f, 0.24f, 0.28f),
            new Color(0.10f, 0.12f, 0.15f),
            new Color(0.16f, 0.18f, 0.23f),
            new Color(0.24f, 0.25f, 0.28f),
        };
        accentColors = new[]
        {
            new Color(0.05f, 0.42f, 0.72f),
            new Color(0.04f, 0.48f, 0.42f),
            new Color(0.58f, 0.08f, 0.24f),
            new Color(0.82f, 0.55f, 0.18f),
        };
        accentChance = 0.09f;
        skipChance = 0.025f;
        groundColor = new Color(0.035f, 0.037f, 0.045f);
        seed = 42;
    }

    /// <summary>
    /// Generates the cityscape. Clears existing first.
    /// </summary>
    [ContextMenu("Generate Cityscape")]
    public void Generate()
    {
        Clear();
        InitMaterials();
        Random.InitState(seed);

        rootTransform = new GameObject("Cityscape_Root").transform;
        rootTransform.SetParent(transform, false);

        CreateGround();

        float step = blockSize + streetWidth;
        int half = Mathf.CeilToInt(cityRadius / step);
        int totalBuildings = 0;

        for (int bx = -half; bx <= half; bx++)
        {
            for (int bz = -half; bz <= half; bz++)
            {
                float cx = cityCenter.x + bx * step;
                float cz = cityCenter.z + bz * step;

                float dist = Mathf.Sqrt((cx - cityCenter.x) * (cx - cityCenter.x) +
                                        (cz - cityCenter.z) * (cz - cityCenter.z));

                if (dist > cityRadius) continue;
                if (dist < clearRadius) continue;

                totalBuildings += GenerateBlock(cx, cz, dist);
            }
        }

        Debug.Log($"[CityscapeGenerator] Generated {totalBuildings} buildings (seed={seed}).");
    }

    /// <summary>
    /// Clears all generated objects and materials.
    /// </summary>
    [ContextMenu("Clear Cityscape")]
    public void Clear()
    {
        var existing = transform.Find("Cityscape_Root");
        if (existing != null)
            SafeDestroy(existing.gameObject);

        foreach (var m in materialsCache)
            if (m != null) SafeDestroy(m);
        materialsCache.Clear();

        if (groundMat != null) { SafeDestroy(groundMat); groundMat = null; }
    }

    private void InitMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
            Debug.LogWarning("[CityscapeGenerator] URP/Lit not found, using Standard.");
        }

        foreach (var c in buildingColors)
        {
            var m = new Material(shader);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.2f);
            m.SetFloat("_Metallic", 0.05f);
            m.enableInstancing = true;
            materialsCache.Add(m);
        }

        foreach (var c in accentColors)
        {
            var m = new Material(shader);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.3f);
            m.SetFloat("_Metallic", 0.1f);
            m.enableInstancing = true;
            materialsCache.Add(m);
        }

        groundMat = new Material(shader);
        groundMat.SetColor("_BaseColor", groundColor);
        groundMat.SetFloat("_Smoothness", 0.1f);
        groundMat.SetFloat("_Metallic", 0f);
    }

    private void CreateGround()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "Ground";
        SafeDestroy(go.GetComponent<Collider>());
        go.transform.SetParent(rootTransform, false);
        go.transform.localPosition = new Vector3(cityCenter.x, groundY, cityCenter.z);
        go.transform.localScale = new Vector3(groundScale, 1f, groundScale);
        go.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
    }

    private int GenerateBlock(float blockX, float blockZ, float distFromCenter)
    {
        float cellSize = blockSize / cellsPerBlock;
        float halfBlock = blockSize * 0.5f;

        // Distance-based height modulation
        float distNorm = Mathf.Clamp01((distFromCenter - clearRadius) / (cityRadius - clearRadius));
        float heightScale = Mathf.Lerp(1.0f, 0.4f, distNorm);

        // Per-block features
        bool hasLandmark = Random.value < landmarkChance;
        int lmX = Random.Range(0, cellsPerBlock);
        int lmZ = Random.Range(0, cellsPerBlock);

        bool hasCluster = Random.value < clusterChance;
        int clX = Random.Range(0, cellsPerBlock);
        int clZ = Random.Range(0, cellsPerBlock);

        // Track occupied cells for multi-cell buildings
        bool[,] occupied = new bool[cellsPerBlock, cellsPerBlock];
        int buildingCount = 0;

        // Block parent
        var blockGO = new GameObject($"Blk_{blockX:F0}_{blockZ:F0}");
        blockGO.transform.SetParent(rootTransform, false);

        // Collect cubes grouped by material index for combining
        var meshGroups = new Dictionary<int, List<CombineInstance>>();

        // First pass: place multi-cell buildings
        for (int cx = 0; cx < cellsPerBlock; cx++)
        {
            for (int cz = 0; cz < cellsPerBlock; cz++)
            {
                if (occupied[cx, cz]) continue;

                // Try quad cell (2x2)
                if (Random.value < quadCellChance &&
                    cx + 1 < cellsPerBlock && cz + 1 < cellsPerBlock &&
                    !occupied[cx + 1, cz] && !occupied[cx, cz + 1] && !occupied[cx + 1, cz + 1])
                {
                    occupied[cx, cz] = true;
                    occupied[cx + 1, cz] = true;
                    occupied[cx, cz + 1] = true;
                    occupied[cx + 1, cz + 1] = true;

                    float bx = blockX - halfBlock + (cx + 1.0f) * cellSize;
                    float bz = blockZ - halfBlock + (cz + 1.0f) * cellSize;
                    float w = cellSize * 2f - innerGap;
                    float d = cellSize * 2f - innerGap;
                    float h = RandomHeight(heightScale) * 1.3f;

                    AddCube(meshGroups, bx, bz, w, h, d);
                    buildingCount++;
                    continue;
                }

                // Try double cell (1x2 or 2x1)
                if (Random.value < doubleCellChance)
                {
                    bool horizontal = Random.value > 0.5f;
                    int nx = cx + (horizontal ? 1 : 0);
                    int nz = cz + (horizontal ? 0 : 1);

                    if (nx < cellsPerBlock && nz < cellsPerBlock && !occupied[nx, nz])
                    {
                        occupied[cx, cz] = true;
                        occupied[nx, nz] = true;

                        float bx = blockX - halfBlock + (cx + (horizontal ? 1.0f : 0.5f)) * cellSize;
                        float bz = blockZ - halfBlock + (cz + (horizontal ? 0.5f : 1.0f)) * cellSize;
                        float w = cellSize * (horizontal ? 2f : 1f) - innerGap;
                        float d = cellSize * (horizontal ? 1f : 2f) - innerGap;
                        float h = RandomHeight(heightScale) * 1.1f;

                        AddCube(meshGroups, bx, bz, w, h, d);
                        buildingCount++;
                        continue;
                    }
                }
            }
        }

        // Second pass: fill remaining cells with single buildings
        for (int cx = 0; cx < cellsPerBlock; cx++)
        {
            for (int cz = 0; cz < cellsPerBlock; cz++)
            {
                if (occupied[cx, cz]) continue;
                if (Random.value < skipChance) continue;

                float bx = blockX - halfBlock + (cx + 0.5f) * cellSize;
                float bz = blockZ - halfBlock + (cz + 0.5f) * cellSize;

                // Per-cell distance check to sphere
                float cellDist = Mathf.Sqrt((bx - cityCenter.x) * (bx - cityCenter.x) +
                                            (bz - cityCenter.z) * (bz - cityCenter.z));
                if (cellDist < clearRadius * 0.85f) continue;

                float baseSize = cellSize - innerGap;
                float w = baseSize * Random.Range(0.75f, 1.0f);
                float d = baseSize * Random.Range(0.75f, 1.0f);
                float h;

                // Landmark
                if (hasLandmark && cx == lmX && cz == lmZ)
                {
                    h = Random.Range(landmarkMinHeight, landmarkMaxHeight) * heightScale;
                    w = baseSize * 0.55f;
                    d = baseSize * 0.55f;
                }
                // Cluster (medium-tall group)
                else if (hasCluster && Mathf.Abs(cx - clX) <= 1 && Mathf.Abs(cz - clZ) <= 1)
                {
                    h = Random.Range(maxHeight * 0.5f, maxHeight * 0.9f) * heightScale;
                }
                else
                {
                    h = RandomHeight(heightScale);
                }

                AddCube(meshGroups, bx, bz, w, h, d);
                buildingCount++;
            }
        }

        // Combine meshes per material and create renderers
        BuildCombinedMeshes(blockGO, meshGroups);

        return buildingCount;
    }

    private float RandomHeight(float scale)
    {
        float t = Mathf.Pow(Random.value, heightExponent);
        return Mathf.Lerp(minHeight, maxHeight, t) * scale;
    }

    private int PickMaterialIndex()
    {
        if (Random.value < accentChance && accentColors.Length > 0)
            return buildingColors.Length + Random.Range(0, accentColors.Length);
        return Random.Range(0, buildingColors.Length);
    }

    private void AddCube(Dictionary<int, List<CombineInstance>> groups,
                         float x, float z, float width, float height, float depth)
    {
        int matIdx = PickMaterialIndex();
        if (!groups.ContainsKey(matIdx))
            groups[matIdx] = new List<CombineInstance>();

        float posY = groundY + height * 0.5f;

        var ci = new CombineInstance
        {
            mesh = GetCubeMesh(),
            transform = Matrix4x4.TRS(
                new Vector3(x, posY, z),
                Quaternion.identity,
                new Vector3(width, height, depth))
        };
        groups[matIdx].Add(ci);
    }

    private void BuildCombinedMeshes(GameObject parent, Dictionary<int, List<CombineInstance>> groups)
    {
        foreach (var kvp in groups)
        {
            int matIdx = kvp.Key;
            var combines = kvp.Value;

            // Split into batches to stay under 65k vertex limit (cube = 24 verts)
            const int maxPerBatch = 2500;
            int batchCount = Mathf.CeilToInt((float)combines.Count / maxPerBatch);

            for (int b = 0; b < batchCount; b++)
            {
                int start = b * maxPerBatch;
                int count = Mathf.Min(maxPerBatch, combines.Count - start);
                var batchArray = new CombineInstance[count];
                for (int i = 0; i < count; i++)
                    batchArray[i] = combines[start + i];

                var mesh = new Mesh();
                mesh.CombineMeshes(batchArray, true, true);
                mesh.RecalculateBounds();

                var go = new GameObject($"M{matIdx}_B{b}");
                go.transform.SetParent(parent.transform, false);

                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = materialsCache[matIdx];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;
            }
        }
    }

    private Mesh _cubeMesh;

    private Mesh GetCubeMesh()
    {
        if (_cubeMesh == null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            SafeDestroy(temp);
        }
        return _cubeMesh;
    }

    private void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private void OnDestroy()
    {
        foreach (var m in materialsCache)
            if (m != null) SafeDestroy(m);
        materialsCache.Clear();

        if (groundMat != null) { SafeDestroy(groundMat); groundMat = null; }
    }
}
