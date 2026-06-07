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
    private Material _dryGrassMaterial;
    private Material _darkGrassMaterial;
    private Material _gravelMaterial;
    private Material _rockMaterial;
    private Material _woodMaterial;
    private Material _paperMaterial;
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
        EnsurePracticeSession();
    }

    private void HideDefaultGreyBox()
    {
        SetObjectActive("Plane", false);
        SetObjectActive("Cube", false);
    }

    private void CreateMaterials()
    {
        _grassMaterial = MakeMaterial("Range Grass", new Color(0.27f, 0.36f, 0.22f));
        _dirtMaterial = MakeMaterial("Packed Dirt", new Color(0.24f, 0.22f, 0.18f));
        _dryGrassMaterial = MakeMaterial("Dry Grass", new Color(0.44f, 0.41f, 0.25f));
        _darkGrassMaterial = MakeMaterial("Shadow Grass", new Color(0.12f, 0.21f, 0.16f));
        _gravelMaterial = MakeMaterial("Wet Gravel", new Color(0.22f, 0.23f, 0.22f));
        _rockMaterial = MakeMaterial("Mountain Rock", new Color(0.32f, 0.34f, 0.34f));
        _woodMaterial = MakeMaterial("Range Wood", new Color(0.42f, 0.28f, 0.17f));
        _paperMaterial = MakeMaterial("Weathered Target Paper", new Color(0.78f, 0.74f, 0.64f));
        _whiteMaterial = MakeMaterial("Target White", new Color(0.92f, 0.9f, 0.84f));
        _blackMaterial = MakeMaterial("Target Black", new Color(0.03f, 0.03f, 0.03f));
        _redMaterial = MakeMaterial("Muted Range Red", new Color(0.58f, 0.06f, 0.04f));
        _yellowMaterial = MakeMaterial("Dulled Brass Paint", new Color(0.66f, 0.5f, 0.16f));
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

        BuildGroundVariation();
        BuildRangeCenterLine();
        BuildVegetation();
    }

    private void BuildFiringLine()
    {
        CreateBox("Concrete Firing Pad", new Vector3(0f, 0.05f, -7f), new Vector3(16f, 0.1f, 8f), _gravelMaterial);
        CreateBox("Prone Shooting Mat", new Vector3(0f, 0.13f, -5.5f), new Vector3(3.2f, 0.04f, 5f), _blackMaterial);
        CreateBox("Wooden Bench Left", new Vector3(-5.2f, 0.45f, -4f), new Vector3(2.7f, 0.15f, 1.1f), _woodMaterial);
        CreateBox("Wooden Bench Right", new Vector3(5.2f, 0.45f, -4f), new Vector3(2.7f, 0.15f, 1.1f), _woodMaterial);

        for (int i = -3; i <= 3; i++)
        {
            CreateBox($"Low Firing Guide {i}", new Vector3(i * 2.6f, 0.155f, -1f), new Vector3(0.04f, 0.025f, 4.8f), _woodMaterial);
        }

        CreateSandbagStack("Left Sandbag Stack", new Vector3(-2.5f, 0.23f, -1.7f));
        CreateSandbagStack("Right Sandbag Stack", new Vector3(2.5f, 0.23f, -1.7f));
    }

    private void BuildRangeCenterLine()
    {
        for (int i = 1; i <= 10; i++)
        {
            float z = i * 50f;
            CreateBox($"Low Left Range Marker {z:0}m", new Vector3(-2.2f, 0.19f, z), new Vector3(0.055f, 0.38f, 0.055f), _woodMaterial);
            CreateBox($"Low Right Range Marker {z:0}m", new Vector3(2.2f, 0.19f, z), new Vector3(0.055f, 0.38f, 0.055f), _woodMaterial);
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
        CreateBox($"{distance:0}m Target Left Post", new Vector3(x - 0.88f, 0.96f, distance), new Vector3(0.12f, 1.92f, 0.12f), _woodMaterial);
        CreateBox($"{distance:0}m Target Right Post", new Vector3(x + 0.88f, 0.96f, distance), new Vector3(0.12f, 1.92f, 0.12f), _woodMaterial);
        CreateBox($"{distance:0}m Target Top Rail", new Vector3(x, 1.88f, distance), new Vector3(1.88f, 0.1f, 0.12f), _woodMaterial);
        GameObject board = CreateBox($"{distance:0}m Target Board", new Vector3(x, 1.48f, distance + 0.04f), new Vector3(1.45f, 1.45f, 0.065f), _paperMaterial);
        board.AddComponent<RangeTarget>().Configure(distance, 0.62f);
        CreateRing($"{distance:0}m Outer Ring", new Vector3(x, 1.48f, distance - 0.015f), 0.62f, _blackMaterial);
        CreateRing($"{distance:0}m Middle Ring", new Vector3(x, 1.48f, distance - 0.025f), 0.38f, _blackMaterial);
        CreateRing($"{distance:0}m Bull", new Vector3(x, 1.48f, distance - 0.035f), 0.16f, _redMaterial);
        CreateBox($"{distance:0}m Paper Clip Left", new Vector3(x - 0.56f, 2.12f, distance - 0.02f), new Vector3(0.12f, 0.05f, 0.025f), _blackMaterial);
        CreateBox($"{distance:0}m Paper Clip Right", new Vector3(x + 0.56f, 2.12f, distance - 0.02f), new Vector3(0.12f, 0.05f, 0.025f), _blackMaterial);
    }

    private void BuildDistanceSign(float distance, float x)
    {
        GameObject sign = CreateBox($"{distance:0}m Distance Sign", new Vector3(x, 0.7f, distance - 4f), new Vector3(1.3f, 0.55f, 0.08f), _woodMaterial);

        TextMesh text = new GameObject($"{distance:0}m Distance Text").AddComponent<TextMesh>();
        text.transform.SetParent(sign.transform, false);
        text.transform.localPosition = new Vector3(0f, 0f, -0.055f);
        text.transform.localRotation = Quaternion.identity;
        text.transform.localScale = Vector3.one * 0.18f;
        text.text = $"{distance:0}m";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = new Color(0.02f, 0.018f, 0.012f);
        text.fontSize = 64;
    }

    private void BuildBackstop()
    {
        CreateMound("Impact Berm Center", new Vector3(0f, 1.8f, 535f), new Vector3(28f, 3.6f, 8f), _dirtMaterial);
        CreateMound("Impact Berm Left", new Vector3(-24f, 1.9f, 530f), new Vector3(14f, 3.8f, 8f), _dirtMaterial);
        CreateMound("Impact Berm Right", new Vector3(24f, 1.9f, 530f), new Vector3(14f, 3.8f, 8f), _dirtMaterial);
    }

    private void BuildWindFlags()
    {
        float[] distances = { 75f, 150f, 250f, 350f, 450f };
        for (int i = 0; i < distances.Length; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Vector3 polePosition = new Vector3(side * 12f, 1.75f, distances[i]);
            CreateBox($"Wind Flag Pole {distances[i]:0}m", polePosition, new Vector3(0.08f, 3.5f, 0.08f), _blackMaterial);
            GameObject cloth = CreateBox($"Wind Flag Cloth {distances[i]:0}m", polePosition + new Vector3(side * 0.7f, 1.25f, 0f), new Vector3(1.25f, 0.42f, 0.04f), _redMaterial);
            cloth.AddComponent<WindFlagAnimator>().Configure(side, 0.8f + i * 0.13f, 7f + i * 0.8f);
        }
    }

    private void TuneLighting()
    {
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.48f, 0.56f, 0.56f);
        RenderSettings.fogDensity = 0.0018f;
        RenderSettings.ambientLight = new Color(0.36f, 0.39f, 0.38f);

        Light sun = FindAnyObjectByType<Light>();
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(34f, -42f, 0f);
            sun.intensity = 1.05f;
            sun.color = new Color(1f, 0.88f, 0.72f);
        }
    }

    private void EnsurePracticeSession()
    {
        if (FindAnyObjectByType<PracticeSessionController>() != null)
        {
            return;
        }

        gameObject.AddComponent<PracticeSessionController>();
    }

    private void BuildGroundVariation()
    {
        Random.InitState(seed);
        for (int i = 0; i < 90; i++)
        {
            float z = Random.Range(20f, rangeLength - 45f);
            float xSide = Random.value < 0.5f ? -1f : 1f;
            float x = xSide * Random.Range(12f, rangeWidth * 0.43f);
            Material material = Random.value < 0.55f ? _dryGrassMaterial : _darkGrassMaterial;
            Vector3 scale = new Vector3(Random.Range(2.5f, 8f), 0.018f, Random.Range(2.5f, 10f));
            GameObject patch = CreateMound($"Ground Tone Patch {i:00}", new Vector3(x, 0.03f, z), scale, material);
            patch.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 180f), 0f);
            DisableCollider(patch);
        }

        for (int i = 0; i < 38; i++)
        {
            float z = Random.Range(35f, rangeLength - 55f);
            float x = (Random.value < 0.5f ? -1f : 1f) * Random.Range(22f, rangeWidth * 0.46f);
            Vector3 scale = Vector3.one * Random.Range(0.35f, 1.4f);
            scale.y *= Random.Range(0.35f, 0.75f);
            GameObject rock = CreateMound($"Loose Rock {i:00}", new Vector3(x, scale.y * 0.35f, z), scale, _rockMaterial);
            rock.transform.rotation = Random.rotationUniform;
        }
    }

    private void BuildVegetation()
    {
        Random.InitState(seed + 88);
        for (int i = 0; i < 120; i++)
        {
            float z = Random.Range(8f, rangeLength - 80f);
            float x = (Random.value < 0.5f ? -1f : 1f) * Random.Range(18f, rangeWidth * 0.47f);
            CreateGrassTuft($"Range Grass Tuft {i:000}", new Vector3(x, 0.22f, z), Random.Range(0.45f, 1.25f));
        }
    }

    private void CreateGrassTuft(string objectName, Vector3 position, float height)
    {
        GameObject tuft = new GameObject(objectName);
        tuft.transform.SetParent(transform, false);
        tuft.transform.position = position;

        for (int i = 0; i < 3; i++)
        {
            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = $"{objectName} Blade {i}";
            blade.transform.SetParent(tuft.transform, false);
            blade.transform.localPosition = new Vector3((i - 1) * 0.08f, height * 0.3f, 0f);
            blade.transform.localRotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 180f), Random.Range(-18f, 18f));
            blade.transform.localScale = new Vector3(0.035f, height, 0.018f);
            blade.GetComponent<Renderer>().sharedMaterial = Random.value < 0.5f ? _dryGrassMaterial : _darkGrassMaterial;
            DisableCollider(blade);
        }

        _createdObjects.Add(tuft);
    }

    private void CreateSandbagStack(string objectName, Vector3 position)
    {
        for (int row = 0; row < 2; row++)
        {
            for (int bag = 0; bag < 3; bag++)
            {
                Vector3 offset = new Vector3((bag - 1) * 0.54f, row * 0.18f, 0f);
                GameObject sandbag = CreateMound($"{objectName} Bag {row}-{bag}", position + offset, new Vector3(0.42f, 0.14f, 0.24f), _dryGrassMaterial);
                sandbag.transform.rotation = Quaternion.Euler(0f, bag % 2 == 0 ? -4f : 5f, 0f);
            }
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

    private GameObject CreateMound(string objectName, Vector3 position, Vector3 scale, Material material)
    {
        GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        mound.name = objectName;
        mound.transform.SetParent(transform, false);
        mound.transform.position = position;
        mound.transform.localScale = scale;
        mound.GetComponent<Renderer>().sharedMaterial = material;
        _createdObjects.Add(mound);
        return mound;
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

        DisableCollider(ring);

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
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.18f);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        return material;
    }

    private static void DisableCollider(GameObject obj)
    {
        Collider collider = obj.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }
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
