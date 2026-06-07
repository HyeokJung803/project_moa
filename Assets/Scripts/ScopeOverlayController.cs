using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime scope mask and reticle overlay. Built in code to avoid extra UI assets.
/// </summary>
public class ScopeOverlayController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AimController aimController;
    [SerializeField] private BulletSpawner bulletSpawner;

    [Header("Overlay")]
    [SerializeField] private int maskTextureSize = 512;
    [SerializeField] private float visibleRadius = 0.39f;
    [SerializeField] private float edgeSoftness = 0.035f;
    [SerializeField] private Color outsideColor = new Color(0f, 0f, 0f, 0.92f);
    [SerializeField] private Color reticleColor = new Color(0f, 0f, 0f, 0.95f);
    [SerializeField] private Color glassTint = new Color(0.08f, 0.13f, 0.12f, 0.08f);
    [SerializeField] private Color scopeRingColor = new Color(0f, 0f, 0f, 0.42f);
    [SerializeField] private Color glassNoiseColor = new Color(0.75f, 0.86f, 0.82f, 0.035f);

    private CanvasGroup _canvasGroup;
    private RectTransform _reticleRoot;
    private Text _turretText;
    private Text _hitText;
    private float _hitTextTimer;

    private void Awake()
    {
        if (aimController == null)
        {
            aimController = GetComponent<AimController>();
        }

        if (bulletSpawner == null)
        {
            bulletSpawner = FindAnyObjectByType<BulletSpawner>();
        }

        BuildOverlay();
    }

    private void OnEnable()
    {
        if (bulletSpawner != null)
        {
            bulletSpawner.OnImpactFeedback += HandleImpactFeedback;
        }
    }

    private void OnDisable()
    {
        if (bulletSpawner != null)
        {
            bulletSpawner.OnImpactFeedback -= HandleImpactFeedback;
        }
    }

    private void LateUpdate()
    {
        if (_canvasGroup == null || aimController == null)
        {
            return;
        }

        float alpha = aimController.AimProgress;
        _canvasGroup.alpha = alpha;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        if (bulletSpawner != null && _reticleRoot != null)
        {
            float pixelScale = 1.8f;
            _reticleRoot.anchoredPosition = new Vector2(
                bulletSpawner.WindageMOA * pixelScale,
                bulletSpawner.ElevationMOA * pixelScale);

            if (_turretText != null)
            {
                _turretText.text =
                    $"ELEV {bulletSpawner.ElevationMOA:+0.00;-0.00;0.00} MOA   WIND {bulletSpawner.WindageMOA:+0.00;-0.00;0.00} MOA";
            }
        }

        UpdateHitText();
    }

    private void BuildOverlay()
    {
        GameObject canvasObject = new GameObject("Scope Overlay");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        _canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();

        RawImage mask = CreateRawImage("Scope Mask", canvasRect, CreateMaskTexture());
        Stretch(mask.rectTransform);

        Image tint = CreateImage("Scope Glass Tint", canvasRect, glassTint);
        Stretch(tint.rectTransform);

        RawImage glassNoise = CreateRawImage("Scope Fine Glass Noise", canvasRect, CreateGlassTexture());
        glassNoise.color = glassNoiseColor;
        Stretch(glassNoise.rectTransform);

        RawImage scopeRing = CreateRawImage("Scope Inner Edge Shadow", canvasRect, CreateRingTexture());
        Stretch(scopeRing.rectTransform);

        _reticleRoot = CreateRect("Reticle", canvasRect);
        _reticleRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _reticleRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _reticleRoot.pivot = new Vector2(0.5f, 0.5f);
        _reticleRoot.sizeDelta = new Vector2(420f, 420f);

        CreateLine("Vertical", _reticleRoot, new Vector2(2f, 420f), Vector2.zero);
        CreateLine("Horizontal", _reticleRoot, new Vector2(420f, 2f), Vector2.zero);
        CreateLine("Upper Gap", _reticleRoot, new Vector2(3f, 70f), new Vector2(0f, 140f));
        CreateLine("Lower Gap", _reticleRoot, new Vector2(3f, 70f), new Vector2(0f, -140f));

        for (int i = -4; i <= 4; i++)
        {
            if (i == 0)
            {
                continue;
            }

            CreateLine($"Wind Tick {i}", _reticleRoot, new Vector2(2f, 16f), new Vector2(i * 42f, 0f));
            CreateLine($"Elev Tick {i}", _reticleRoot, new Vector2(16f, 2f), new Vector2(0f, i * 42f));
        }

        _turretText = CreateText("Turret Readout", canvasRect, 46f, 20);
        _hitText = CreateText("Impact Readout", canvasRect, 86f, 24);
        _hitText.color = new Color(1f, 0.92f, 0.72f, 0f);
    }

    private Texture2D CreateMaskTexture()
    {
        int size = Mathf.Max(64, maskTextureSize);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * visibleRadius;
        float softness = Mathf.Max(1f, size * edgeSoftness);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.InverseLerp(radius - softness, radius + softness, distance);
                texture.SetPixel(x, y, new Color(outsideColor.r, outsideColor.g, outsideColor.b, outsideColor.a * alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    private Texture2D CreateRingTexture()
    {
        int size = Mathf.Max(64, maskTextureSize);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * visibleRadius;
        float innerFade = size * 0.045f;
        float outerFade = size * 0.02f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float outer = 1f - Mathf.InverseLerp(radius, radius + outerFade, distance);
                float inner = Mathf.InverseLerp(radius - innerFade, radius, distance);
                float alpha = Mathf.Clamp01(inner * outer) * scopeRingColor.a;
                texture.SetPixel(x, y, new Color(scopeRingColor.r, scopeRingColor.g, scopeRingColor.b, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    private Texture2D CreateGlassTexture()
    {
        int size = Mathf.Max(64, maskTextureSize);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * visibleRadius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float inside = 1f - Mathf.InverseLerp(radius - 2f, radius + 2f, distance);
                float noise = Mathf.PerlinNoise(x * 0.075f + 13.2f, y * 0.075f + 91.7f);
                float scratch = Mathf.PerlinNoise(x * 0.012f + 55.3f, y * 0.18f + 7.1f);
                float alpha = Mathf.Clamp01((noise * 0.45f + scratch * 0.55f - 0.54f) * 1.8f) * inside;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    private RawImage CreateRawImage(string name, Transform parent, Texture texture)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RawImage image = obj.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    private Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image image = obj.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private RectTransform CreateRect(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj.AddComponent<RectTransform>();
    }

    private void CreateLine(string name, Transform parent, Vector2 size, Vector2 position)
    {
        Image image = CreateImage(name, parent, reticleColor);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private Text CreateText(string name, Transform parent, float bottomOffset, int fontSize)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text text = obj.AddComponent<Text>();
        text.raycastTarget = false;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = new Color(1f, 1f, 1f, 0.82f);

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(900f, 40f);
        rect.anchoredPosition = new Vector2(0f, bottomOffset);
        return text;
    }

    private void HandleImpactFeedback(string message)
    {
        if (_hitText == null)
        {
            return;
        }

        _hitText.text = message;
        _hitTextTimer = 2.2f;
    }

    private void UpdateHitText()
    {
        if (_hitText == null)
        {
            return;
        }

        _hitTextTimer = Mathf.Max(0f, _hitTextTimer - Time.deltaTime);
        float alpha = Mathf.Clamp01(_hitTextTimer / 0.35f);
        Color color = _hitText.color;
        color.a = 0.9f * alpha;
        _hitText.color = color;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
