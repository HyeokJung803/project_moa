using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Lightweight practice-game loop: ammo count, score, current distance task, and result HUD.
/// </summary>
public class PracticeSessionController : MonoBehaviour
{
    [Header("Session")]
    [SerializeField] private int shotsPerSession = 10;
    [SerializeField] private float parTimeSeconds = 180f;
    [SerializeField] private float[] taskDistances = { 100f, 200f, 300f, 500f };

    [Header("References")]
    [SerializeField] private BulletSpawner bulletSpawner;

    private CanvasGroup _canvasGroup;
    private Text _sessionText;
    private Text _objectiveText;
    private Text _lastShotText;
    private Text _environmentText;
    private Text _hintText;

    private int _shotsFired;
    private int _hits;
    private int _totalScore;
    private int _currentTaskIndex;
    private int _bestScore;
    private float _bestAccuracy;
    private float _sessionTimer;
    private bool _sessionEnded;
    private string _lastShot = "NO SHOTS FIRED";

    private const string BestScoreKey = "PracticeSession.BestScore";
    private const string BestAccuracyKey = "PracticeSession.BestAccuracy";

    private void Awake()
    {
        if (bulletSpawner == null)
        {
            bulletSpawner = FindAnyObjectByType<BulletSpawner>();
        }

        BuildHud();
        LoadBestSession();
        StartSession();
    }

    private void OnEnable()
    {
        if (bulletSpawner == null)
        {
            return;
        }

        bulletSpawner.OnFired += HandleFired;
        bulletSpawner.OnShotResolved += HandleShotResolved;
    }

    private void OnDisable()
    {
        if (bulletSpawner == null)
        {
            return;
        }

        bulletSpawner.OnFired -= HandleFired;
        bulletSpawner.OnShotResolved -= HandleShotResolved;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            StartSession();
        }

        HandleEnvironmentInput(keyboard);

        if (!_sessionEnded)
        {
            _sessionTimer = Mathf.Max(0f, _sessionTimer - Time.deltaTime);
            if (_sessionTimer <= 0f)
            {
                EndSession();
            }
        }

        RefreshHud();
    }

    private void StartSession()
    {
        _shotsFired = 0;
        _hits = 0;
        _totalScore = 0;
        _currentTaskIndex = 0;
        _sessionTimer = parTimeSeconds;
        _sessionEnded = false;
        _lastShot = "NO SHOTS FIRED";

        if (bulletSpawner != null)
        {
            bulletSpawner.FireLocked = false;
        }
    }

    private void HandleFired()
    {
        if (_sessionEnded)
        {
            return;
        }

        _shotsFired++;
        if (_shotsFired >= shotsPerSession)
        {
            if (bulletSpawner != null)
            {
                bulletSpawner.FireLocked = true;
            }
        }
    }

    private void HandleShotResolved(ShotResult result)
    {
        if (_sessionEnded)
        {
            return;
        }

        int bonus = 0;
        float targetDistance = CurrentTaskDistance;
        bool taskHit = result.HitTarget && Mathf.Abs(result.TargetDistanceMeters - targetDistance) < 0.5f;

        if (result.HitTarget)
        {
            _hits++;
            bonus = taskHit ? 2 : 0;
            _totalScore += result.Score + bonus;
            _lastShot = $"{result.TargetDistanceMeters:0}m HIT   SCORE {result.Score}";
            if (bonus > 0)
            {
                _lastShot += " + TASK";
                AdvanceTask();
            }
        }
        else
        {
            _lastShot = $"MISS   {result.ImpactDistanceMeters:0}m";
        }

        _lastShot += $"   TOF {result.TimeOfFlight:F2}s";

        if (_shotsFired >= shotsPerSession)
        {
            EndSession();
        }
    }

    private void AdvanceTask()
    {
        if (taskDistances == null || taskDistances.Length == 0)
        {
            return;
        }

        _currentTaskIndex = (_currentTaskIndex + 1) % taskDistances.Length;
    }

    private void EndSession()
    {
        _sessionEnded = true;
        if (bulletSpawner != null)
        {
            bulletSpawner.FireLocked = true;
        }

        SaveBestSession();
    }

    private float CurrentTaskDistance
    {
        get
        {
            if (taskDistances == null || taskDistances.Length == 0)
            {
                return 100f;
            }

            int index = Mathf.Clamp(_currentTaskIndex, 0, taskDistances.Length - 1);
            return taskDistances[index];
        }
    }

    private void BuildHud()
    {
        GameObject canvasObject = new GameObject("Practice Session HUD");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 650;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        _canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();

        Image backing = CreateImage("HUD Backing", canvasRect, new Color(0f, 0f, 0f, 0.32f));
        RectTransform backingRect = backing.rectTransform;
        backingRect.anchorMin = new Vector2(0f, 1f);
        backingRect.anchorMax = new Vector2(0f, 1f);
        backingRect.pivot = new Vector2(0f, 1f);
        backingRect.anchoredPosition = new Vector2(24f, -22f);
        backingRect.sizeDelta = new Vector2(460f, 158f);

        _sessionText = CreateText("Session Text", backingRect, new Vector2(18f, -16f), 24, TextAnchor.UpperLeft);
        _objectiveText = CreateText("Objective Text", backingRect, new Vector2(18f, -56f), 22, TextAnchor.UpperLeft);
        _lastShotText = CreateText("Last Shot Text", backingRect, new Vector2(18f, -94f), 20, TextAnchor.UpperLeft);
        _environmentText = CreateText("Environment Text", backingRect, new Vector2(18f, -126f), 18, TextAnchor.UpperLeft);
        _hintText = CreateText("Hint Text", canvasRect, new Vector2(0f, 26f), 18, TextAnchor.MiddleCenter);

        RectTransform hintRect = _hintText.rectTransform;
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(980f, 36f);
    }

    private void RefreshHud()
    {
        if (_canvasGroup == null)
        {
            return;
        }

        int remaining = Mathf.Max(0, shotsPerSession - _shotsFired);
        float accuracy = _shotsFired > 0 ? _hits / (float)_shotsFired * 100f : 0f;
        string status = _sessionEnded ? "SESSION COMPLETE" : "LIVE PRACTICE";

        _sessionText.text = $"{status}   SCORE {_totalScore:000}   AMMO {remaining}/{shotsPerSession}";
        _objectiveText.text = $"TASK: HIT {CurrentTaskDistance:0}m TARGET   TIME {FormatTime(_sessionTimer)}   ACC {accuracy:0}%";
        _lastShotText.text = $"{_lastShot}   PB {_bestScore:000}/{_bestAccuracy:0}%";
        if (bulletSpawner != null)
        {
            float wind = bulletSpawner.WindVelocity.x;
            string windDirection = wind >= 0f ? "L->R" : "R->L";
            _environmentText.text =
                $"KESTREL  {bulletSpawner.AmbientTemperature:+0;-0;0}C   ALT {bulletSpawner.Altitude:0}m   WIND {windDirection} {Mathf.Abs(wind):0.0}m/s";
        }

        _hintText.text = _sessionEnded
            ? "PRESS R TO START A NEW STRING"
            : "SPACE FIRE   RMB SCOPE   SHIFT HOLD BREATH   [ ] WIND   - = TEMP   , . ALT";
    }

    private void LoadBestSession()
    {
        _bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        _bestAccuracy = PlayerPrefs.GetFloat(BestAccuracyKey, 0f);
    }

    private void SaveBestSession()
    {
        float accuracy = _shotsFired > 0 ? _hits / (float)_shotsFired * 100f : 0f;
        bool newBest = _totalScore > _bestScore
            || (_totalScore == _bestScore && accuracy > _bestAccuracy);

        if (!newBest)
        {
            return;
        }

        _bestScore = _totalScore;
        _bestAccuracy = accuracy;
        PlayerPrefs.SetInt(BestScoreKey, _bestScore);
        PlayerPrefs.SetFloat(BestAccuracyKey, _bestAccuracy);
        PlayerPrefs.Save();
    }

    private void HandleEnvironmentInput(Keyboard keyboard)
    {
        if (keyboard == null || bulletSpawner == null)
        {
            return;
        }

        if (keyboard.leftBracketKey.wasPressedThisFrame)
        {
            bulletSpawner.AdjustCrosswind(-0.5f);
        }
        else if (keyboard.rightBracketKey.wasPressedThisFrame)
        {
            bulletSpawner.AdjustCrosswind(0.5f);
        }

        if (keyboard.minusKey.wasPressedThisFrame)
        {
            bulletSpawner.SetAtmosphere(bulletSpawner.AmbientTemperature - 1f, bulletSpawner.Altitude);
        }
        else if (keyboard.equalsKey.wasPressedThisFrame)
        {
            bulletSpawner.SetAtmosphere(bulletSpawner.AmbientTemperature + 1f, bulletSpawner.Altitude);
        }

        if (keyboard.commaKey.wasPressedThisFrame)
        {
            bulletSpawner.SetAtmosphere(bulletSpawner.AmbientTemperature, bulletSpawner.Altitude - 50f);
        }
        else if (keyboard.periodKey.wasPressedThisFrame)
        {
            bulletSpawner.SetAtmosphere(bulletSpawner.AmbientTemperature, bulletSpawner.Altitude + 50f);
        }
    }

    private static string FormatTime(float seconds)
    {
        int wholeSeconds = Mathf.CeilToInt(seconds);
        int minutes = wholeSeconds / 60;
        int remainder = wholeSeconds % 60;
        return $"{minutes:0}:{remainder:00}";
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
        rect.sizeDelta = new Vector2(430f, 32f);
        return text;
    }
}
