using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a grey-box mountain shooting range at runtime.
/// It replaces the default plane/cube test view with a readable practice range.
/// </summary>
public class MountainRangeBuilder : MonoBehaviour
{
    [Header("Range")]
    [SerializeField] private float rangeLength = 650f;
    [SerializeField] private float rangeWidth = 260f;
    [SerializeField] private int terrainResolution = 65;
    [SerializeField] private int seed = 247;

    [Header("Targets")]
    [SerializeField] private float[] targetDistances = { 100f, 200f, 300f, 500f };

    private readonly List<GameObject> _createdObjects = new List<GameObject>();

    private Material _grassMaterial;
    private Material _dirtMaterial;
    private Material _rockMaterial;
    private Material _woodMaterial;
    private Material _whiteMaterial;
    private Material _blackMaterial;
    private Material _redMaterial;
    private Material _yellowMaterial;

    private void Awake()
    {
        HideDefaultGreyBox();
        CreateMaterials();
        BuildTerrain();
        BuildFiringLine();
        BuildTargetLanes();
        BuildBackstop();
        BuildWindFlags();
        TuneLighting();
    }

    private void HideDefaultGreyBox()
    {
        SetObjectActive("Plane", false);
        SetObjectActive("Cube", false);
    }

    private void CreateMaterials()
    {
        _grassMaterial = MakeMaterial("Range Grass", new Color(0.27f, 0.36f, 0.22f));
        _dirtMaterial = MakeMaterial("Packed Dirt", new Color(0.45f, 0.39f, 0.32f));
        _rockMaterial = MakeMaterial("Mountain Rock", new Color(0.32f, 0.34f, 0.34f));
        _woodMaterial = MakeMaterial("Range Wood", new Color(0.42f, 0.28f, 0.17f));
        _whiteMaterial = MakeMaterial("Target White", new Color(0.92f, 0.9f, 0.84f));
        _blackMaterial = MakeMaterial("Target Black", new Color(0.03f, 0.03f, 0.03f));
        _redMaterial = MakeMaterial("Range Red", new Color(0.8f, 0.08f, 0.04f));
        _yellowMaterial = MakeMaterial("Safety Yellow", new Color(0.92f, 0.72f, 0.18f));
    }

    private void BuildTerrain()
    {
        Mesh mesh = new Mesh { name = "Procedural Mountain Range Mesh" };
        int resolution = Mathf.Max(8, terrainResolution);
        Vector3[] vertices = new Vector3[resolution * resolution];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];

        for (int z = 0; z < resolution; z++)
        {
            float z01 = z / (float)(resolution - 1);
            float worldZ = Mathf.Lerp(-40f, rangeLength, z01);

            for (int x = 0; x < resolution; x++)
            {
                float x01 = x / (float)(resolution - 1);
                float worldX = Mathf.Lerp(-rangeWidth * 0.5f, rangeWidth * 0.5f, x01);
                float laneDistance = Mathf.Abs(worldX);
                float side = Mathf.InverseLerp(26f, rangeWidth * 0.5f, laneDistance);
                float farRise = Mathf.InverseLerp(120f, rangeLength, worldZ);
                float noise = Mathf.PerlinNoise(seed + worldX * 0.018f, seed + worldZ * 0.012f);
                float ridgeNoise = Mathf.PerlinNoise(seed + worldX * 0.035f, seed + worldZ * 0.026f);
                float height = side * side * Mathf.Lerp(7f, 45f, farRise) + ridgeNoise * 18f * side;

                if (laneDistance < 18f && worldZ > -5f && worldZ < 560f)
                {
                    height *= 0.12f;
                }

                if (worldZ < 20f)
                {
                    height *= Mathf.InverseLerp(-40f, 20f, worldZ);
                }

                vertices[z * resolution + x] = new Vector3(worldX, height + noise * 0.35f, worldZ);
                uvs[z * resolution + x] = new Vector2(x01 * 10f, z01 * 24f);
            }
        }

        int tri = 0;
        for (int z = 0; z < resolution - 1; z++)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                int i = z * resolution + x;
                triangles[tri++] = i;
                triangles[tri++] = i + resolution;
                triangles[tri++] = i + 1;
                triangles[tri++] = i + 1;
                triangles[tri++] = i + resolution;
                triangles[tri++] = i + resolution + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject terrain = new GameObject("Mountain Training Range Terrain");
        terrain.transform.SetParent(transform, false);
        MeshFilter meshFilter = terrain.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = terrain.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = _grassMaterial;

        MeshCollider meshCollider = terrain.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        _createdObjects.Add(terrain);

        CreateBox("Central Dirt Lane", new Vector3(0f, 0.035f, 280f), new Vector3(18f, 0.05f, 620f), _dirtMaterial);
    }

    private void BuildFiringLine()
    {
        CreateBox("Concrete Firing Pad", new Vector3(0f, 0.05f, -7f), new Vector3(16f, 0.1f, 8f), _dirtMaterial);
        CreateBox("Prone Shooting Mat", new Vector3(0f, 0.13f, -5.5f), new Vector3(3.2f, 0.04f, 5f), _blackMaterial);
        CreateBox("Wooden Bench Left", new Vector3(-5.2f, 0.45f, -4f), new Vector3(2.7f, 0.15f, 1.1f), _woodMaterial);
        CreateBox("Wooden Bench Right", new Vector3(5.2f, 0.45f, -4f), new Vector3(2.7f, 0.15f, 1.1f), _woodMaterial);

        for (int i = -3; i <= 3; i++)
        {
            CreateBox($"Firing Lane Marker {i}", new Vector3(i * 2.6f, 0.16f, -1f), new Vector3(0.05f, 0.04f, 5.5f), _yellowMaterial);
        }
    }

    private void BuildTargetLanes()
    {
        for (int i = 0; i < targetDistances.Length; i++)
        {
            float distance = targetDistances[i];
            float x = (i - (targetDistances.Length - 1) * 0.5f) * 4.2f;
            BuildTarget(distance, x);
            BuildDistanceSign(distance, x - 2.3f);
        }
    }

    private void BuildTarget(float distance, float x)
    {
        CreateBox($"{distance:0}m Target Stand", new Vector3(x, 1.1f, distance), new Vector3(0.18f, 2.1f, 0.18f), _woodMaterial);
        CreateBox($"{distance:0}m Target Board", new Vector3(x, 1.55f, distance + 0.04f), new Vector3(1.55f, 1.55f, 0.08f), _whiteMaterial);
        CreateRing($"{distance:0}m Outer Ring", new Vector3(x, 1.55f, distance - 0.015f), 0.62f, _blackMaterial);
        CreateRing($"{distance:0}m Middle Ring", new Vector3(x, 1.55f, distance - 0.025f), 0.38f, _blackMaterial);
        CreateRing($"{distance:0}m Bull", new Vector3(x, 1.55f, distance - 0.035f), 0.16f, _redMaterial);
    }

    private void BuildDistanceSign(float distance, float x)
    {
        GameObject sign = CreateBox($"{distance:0}m Distance Sign", new Vector3(x, 0.7f, distance - 4f), new Vector3(1.3f, 0.55f, 0.08f), _yellowMaterial);

        TextMesh text = new GameObject($"{distance:0}m Distance Text").AddComponent<TextMesh>();
        text.transform.SetParent(sign.transform, false);
        text.transform.localPosition = new Vector3(0f, 0f, -0.055f);
        text.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        text.transform.localScale = Vector3.one * 0.18f;
        text.text = $"{distance:0}m";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.black;
        text.fontSize = 64;
    }

    private void BuildBackstop()
    {
        CreateBox("Impact Berm Center", new Vector3(0f, 4f, 535f), new Vector3(40f, 8f, 10f), _dirtMaterial);
        CreateBox("Impact Berm Left", new Vector3(-28f, 5f, 530f), new Vector3(18f, 10f, 12f), _dirtMaterial);
        CreateBox("Impact Berm Right", new Vector3(28f, 5f, 530f), new Vector3(18f, 10f, 12f), _dirtMaterial);
        CreateBox("Rock Face Backdrop", new Vector3(0f, 22f, 610f), new Vector3(180f, 45f, 18f), _rockMaterial);
    }

    private void BuildWindFlags()
    {
        float[] distances = { 75f, 150f, 250f, 350f, 450f };
        for (int i = 0; i < distances.Length; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Vector3 polePosition = new Vector3(side * 12f, 1.75f, distances[i]);
            CreateBox($"Wind Flag Pole {distances[i]:0}m", polePosition, new Vector3(0.08f, 3.5f, 0.08f), _blackMaterial);
            CreateBox($"Wind Flag Cloth {distances[i]:0}m", polePosition + new Vector3(side * 0.7f, 1.25f, 0f), new Vector3(1.25f, 0.42f, 0.04f), _redMaterial);
        }
    }

    private void TuneLighting()
    {
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.58f, 0.66f, 0.68f);
        RenderSettings.fogDensity = 0.0022f;
        RenderSettings.ambientLight = new Color(0.52f, 0.55f, 0.58f);

        Light sun = FindAnyObjectByType<Light>();
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.94f, 0.84f);
        }
    }

    private GameObject CreateBox(string objectName, Vector3 position, Vector3 scale, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(transform, false);
        box.transform.position = position;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = material;
        _createdObjects.Add(box);
        return box;
    }

    private void CreateRing(string objectName, Vector3 position, float radius, Material material)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = objectName;
        ring.transform.SetParent(transform, false);
        ring.transform.position = position;
        ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        ring.transform.localScale = new Vector3(radius, 0.015f, radius);
        ring.GetComponent<Renderer>().sharedMaterial = material;

        Collider collider = ring.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        _createdObjects.Add(ring);
    }

    private Material MakeMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;
        return material;
    }

    private static void SetObjectActive(string objectName, bool active)
    {
        GameObject found = GameObject.Find(objectName);
        if (found != null)
        {
            found.SetActive(active);
        }
    }
}
