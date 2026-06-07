using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a short picture-in-picture view of the last impact for shot correction.
/// </summary>
public class ImpactSpotterCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BulletSpawner bulletSpawner;

    [Header("View")]
    [SerializeField] private float displaySeconds = 4.5f;
    [SerializeField] private float cameraBackOffset = 8f;
    [SerializeField] private float cameraHeightOffset = 1.4f;
    [SerializeField] private float fieldOfView = 15f;

    private Camera _spotterCamera;
    private RenderTexture _renderTexture;
    private CanvasGroup _canvasGroup;
    private RawImage _previewImage;
    private Text _labelText;
    private float _timer;

    private void Awake()
    {
        if (bulletSpawner == null)
        {
            bulletSpawner = FindAnyObjectByType<BulletSpawner>();
        }

        BuildCamera();
        BuildUi();
        HidePreview();
    }

    private void OnEnable()
    {
        if (bulletSpawner != null)
        {
            bulletSpawner.OnShotResolved += HandleShotResolved;
        }
    }

    private void OnDisable()
    {
        if (bulletSpawner != null)
        {
            bulletSpawner.OnShotResolved -= HandleShotResolved;
        }
    }

    private void Update()
    {
        if (_timer <= 0f)
        {
            HidePreview();
            return;
        }

        _timer -= Time.deltaTime;
        float fade = Mathf.Clamp01(_timer / 0.35f);
        _canvasGroup.alpha = Mathf.Min(1f, fade * 1.35f);
    }

    private void HandleShotResolved(ShotResult result)
    {
        if (_spotterCamera == null)
        {
            return;
        }

        Vector3 impactPoint = result.ImpactPoint;
        Vector3 viewPosition = impactPoint + new Vector3(0f, cameraHeightOffset, -cameraBackOffset);
        if (viewPosition.y < 0.8f)
        {
            viewPosition.y = 0.8f;
        }

        _spotterCamera.transform.position = viewPosition;
        _spotterCamera.transform.rotation = Quaternion.LookRotation((impactPoint - viewPosition).normalized, Vector3.up);
        _spotterCamera.enabled = true;
        _timer = displaySeconds;

        if (_labelText != null)
        {
            _labelText.text = result.HitTarget
                ? $"SPOTTER   {result.TargetDistanceMeters:0}m HIT   SCORE {result.Score}"
                : $"SPOTTER   MISS   {result.ImpactDistanceMeters:0}m";
        }
    }

    private void BuildCamera()
    {
        GameObject cameraObject = new GameObject("Impact Spotter Camera");
        cameraObject.transform.SetParent(transform, false);

        _spotterCamera = cameraObject.AddComponent<Camera>();
        _spotterCamera.enabled = false;
        _spotterCamera.fieldOfView = fieldOfView;
        _spotterCamera.nearClipPlane = 0.05f;
        _spotterCamera.farClipPlane = 120f;
        _spotterCamera.clearFlags = CameraClearFlags.Skybox;
        _spotterCamera.depth = -5f;

        _renderTexture = new RenderTexture(512, 288, 16, RenderTextureFormat.ARGB32);
        _renderTexture.name = "Impact Spotter Render Texture";
        _spotterCamera.targetTexture = _renderTexture;
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("Impact Spotter HUD");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        _canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Image frame = CreateImage("Spotter Frame", canvasRect, new Color(0f, 0f, 0f, 0.55f));
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = new Vector2(1f, 1f);
        frameRect.anchorMax = new Vector2(1f, 1f);
        frameRect.pivot = new Vector2(1f, 1f);
        frameRect.anchoredPosition = new Vector2(-26f, -24f);
        frameRect.sizeDelta = new Vector2(370f, 238f);

        GameObject previewObject = new GameObject("Spotter Preview");
        previewObject.transform.SetParent(frameRect, false);
        _previewImage = previewObject.AddComponent<RawImage>();
        _previewImage.texture = _renderTexture;
        _previewImage.raycastTarget = false;

        RectTransform previewRect = _previewImage.rectTransform;
        previewRect.anchorMin = new Vector2(0.5f, 1f);
        previewRect.anchorMax = new Vector2(0.5f, 1f);
        previewRect.pivot = new Vector2(0.5f, 1f);
        previewRect.anchoredPosition = new Vector2(0f, -14f);
        previewRect.sizeDelta = new Vector2(338f, 190f);

        _labelText = CreateText("Spotter Label", frameRect, new Vector2(16f, -210f), 17, TextAnchor.UpperLeft);
    }

    private void HidePreview()
    {
        _timer = 0f;
        if (_spotterCamera != null)
        {
            _spotterCamera.enabled = false;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
        }
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image image = obj.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(string name, Transform parent, Vector2 anchoredPosition, int fontSize, TextAnchor anchor)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text text = obj.AddComponent<Text>();
        text.raycastTarget = false;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = new Color(0.92f, 0.95f, 0.9f, 0.9f);

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(338f, 24f);
        return text;
    }
}
