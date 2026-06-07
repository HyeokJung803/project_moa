using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Runtime briefing and pause flow for the playable range session.
/// </summary>
public class MissionFlowController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BulletSpawner bulletSpawner;
    [SerializeField] private PracticeSessionController practiceSession;

    private CanvasGroup _canvasGroup;
    private Text _titleText;
    private Text _bodyText;
    private Text _footerText;
    private bool _isMenuOpen;
    private bool _hasStarted;
    private bool _restartAllowed;

    private void Awake()
    {
        if (bulletSpawner == null)
        {
            bulletSpawner = FindAnyObjectByType<BulletSpawner>();
        }

        if (practiceSession == null)
        {
            practiceSession = FindAnyObjectByType<PracticeSessionController>();
        }

        BuildOverlay();
        ShowBriefing();
    }

    private void OnEnable()
    {
        if (practiceSession != null)
        {
            practiceSession.OnSessionEnded += HandleSessionEnded;
        }
    }

    private void OnDisable()
    {
        if (practiceSession != null)
        {
            practiceSession.OnSessionEnded -= HandleSessionEnded;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (!_hasStarted && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            StartMission();
            return;
        }

        if (_isMenuOpen && _hasStarted && _restartAllowed && IsRestartPressed(keyboard))
        {
            RestartMission();
            return;
        }

        if (_hasStarted && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (_isMenuOpen)
            {
                ResumeMission();
            }
            else
            {
                ShowPause();
            }
        }
    }

    private void ShowBriefing()
    {
        _hasStarted = false;
        _titleText.text = "MOA RANGE";
        _bodyText.text =
            "MOUNTAIN PRECISION COURSE\n" +
            "10 ROUND STRING  |  STATIC AND MOVING TARGETS\n" +
            "READ WIND, HOLD BREATH, DIAL TURRETS, CONFIRM IMPACTS";
        _footerText.text = "ENTER START   ESC PAUSE";
        _restartAllowed = false;
        SetMenuOpen(true);
    }

    private void ShowPause()
    {
        _titleText.text = "PAUSED";
        _bodyText.text =
            "RANGE IS COLD\n" +
            "CHECK YOUR LAST IMPACT, RESET YOUR PLAN, THEN CONTINUE";
        _footerText.text = "ESC RESUME";
        _restartAllowed = false;
        SetMenuOpen(true);
    }

    private void StartMission()
    {
        _hasStarted = true;
        if (practiceSession != null)
        {
            practiceSession.StartSession();
        }

        SetMenuOpen(false);
    }

    private void ResumeMission()
    {
        SetMenuOpen(false);
    }

    private void RestartMission()
    {
        if (practiceSession != null)
        {
            practiceSession.StartSession();
        }

        SetMenuOpen(false);
    }

    private void HandleSessionEnded(PracticeSessionResult result)
    {
        _titleText.text = result.NewBest ? "NEW PERSONAL BEST" : "COURSE COMPLETE";
        string groupLine = result.GroupStats.HasSamples
            ? $"GROUP {result.GroupStats.SpreadMeters * 100f:0.0}cm   CORR E {result.GroupStats.CorrectionMoa.y:+0.0;-0.0;0.0} / W {result.GroupStats.CorrectionMoa.x:+0.0;-0.0;0.0} MOA"
            : "GROUP NO VALID TARGET HITS";
        _bodyText.text =
            $"GRADE {result.Grade}\n" +
            $"SCORE {result.Score:000}   HITS {result.Hits}/{result.ShotsFired}   ACC {result.Accuracy:0}%\n" +
            $"BEST {result.BestScore:000}   BEST ACC {result.BestAccuracy:0}%\n" +
            groupLine;
        _footerText.text = "ENTER OR R NEW STRING   ESC CLOSE";
        _restartAllowed = true;
        SetMenuOpen(true);
    }

    private void SetMenuOpen(bool open)
    {
        _isMenuOpen = open;
        Time.timeScale = open ? 0f : 1f;

        if (bulletSpawner != null)
        {
            bulletSpawner.InputLocked = open;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = open ? 1f : 0f;
            _canvasGroup.blocksRaycasts = open;
            _canvasGroup.interactable = open;
        }

        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
    }

    private static bool IsRestartPressed(Keyboard keyboard)
    {
        return keyboard.rKey.wasPressedThisFrame
            || keyboard.enterKey.wasPressedThisFrame
            || keyboard.numpadEnterKey.wasPressedThisFrame;
    }

    private void BuildOverlay()
    {
        GameObject canvasObject = new GameObject("Mission Flow Overlay");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        _canvasGroup = canvasObject.AddComponent<CanvasGroup>();

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Image backing = CreateImage("Mission Backing", canvasRect, new Color(0f, 0f, 0f, 0.72f));
        Stretch(backing.rectTransform);

        Image panel = CreateImage("Mission Panel", canvasRect, new Color(0.03f, 0.04f, 0.035f, 0.86f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 330f);

        _titleText = CreateText("Mission Title", panelRect, new Vector2(0f, -42f), new Vector2(690f, 70f), 42, TextAnchor.MiddleCenter);
        _bodyText = CreateText("Mission Body", panelRect, new Vector2(0f, -138f), new Vector2(690f, 120f), 23, TextAnchor.MiddleCenter);
        _footerText = CreateText("Mission Footer", panelRect, new Vector2(0f, -268f), new Vector2(690f, 34f), 19, TextAnchor.MiddleCenter);
        _footerText.color = new Color(0.92f, 0.78f, 0.48f, 0.96f);
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image image = obj.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    private static Text CreateText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor anchor)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        Text text = obj.AddComponent<Text>();
        text.raycastTarget = false;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = new Color(0.9f, 0.94f, 0.88f, 0.96f);

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
