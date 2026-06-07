using System.Collections.Generic;
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
    [SerializeField] private float[] taskDistances = { 100f, 200f, 300f, 350f, 500f };

    [Header("References")]
    [SerializeField] private BulletSpawner bulletSpawner;

    public string CourseName => _courseName;
    public int ShotsPerSession => shotsPerSession;
    public float ParTimeSeconds => parTimeSeconds;

    private CanvasGroup _canvasGroup;
    private Text _sessionText;
    private Text _objectiveText;
    private Text _lastShotText;
    private Text _environmentText;
    private Text _shotLogText;
    private Text _hintText;

    private int _shotsFired;
    private int _hits;
    private int _totalScore;
    private int _currentTaskIndex;
    private int _bestScore;
    private float _bestAccuracy;
    private readonly List<ShotGroupSample> _groupSamples = new List<ShotGroupSample>();
    private readonly List<string> _shotLog = new List<string>();
    private ShotGroupStats _groupStats;
    private float _sessionTimer;
    private bool _sessionEnded;
    private string _courseName = "STANDARD";
    private string _lastShot = "NO SHOTS FIRED";

    private const string BestScorePrefix = "PracticeSession.BestScore.";
    private const string BestAccuracyPrefix = "PracticeSession.BestAccuracy.";

    public event System.Action<PracticeSessionResult> OnSessionEnded;

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
        if (bulletSpawner != null && bulletSpawner.InputLocked)
        {
            RefreshHud();
            return;
        }

        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            StartSession();
            return;
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

    public void StartSession()
    {
        _shotsFired = 0;
        _hits = 0;
        _totalScore = 0;
        _currentTaskIndex = 0;
        _groupSamples.Clear();
        _shotLog.Clear();
        _groupStats = ShotGroupStats.Empty;
        _sessionTimer = parTimeSeconds;
        _sessionEnded = false;
        _lastShot = "NO SHOTS FIRED";

        if (bulletSpawner != null)
        {
            bulletSpawner.FireLocked = false;
        }

        RefreshHud();
    }

    public void ConfigureCourse(string courseName, int shotCount, float parSeconds, float[] distances, Vector3 windVelocity, float temperatureCelsius, float altitudeMeters)
    {
        _courseName = string.IsNullOrWhiteSpace(courseName) ? "STANDARD" : courseName.ToUpperInvariant();
        shotsPerSession = Mathf.Clamp(shotCount, 3, 30);
        parTimeSeconds = Mathf.Clamp(parSeconds, 30f, 600f);
        taskDistances = distances != null && distances.Length > 0
            ? (float[])distances.Clone()
            : new[] { 100f, 200f, 300f, 350f, 500f };

        if (bulletSpawner != null)
        {
            bulletSpawner.SetWindVelocity(windVelocity);
            bulletSpawner.SetAtmosphere(temperatureCelsius, altitudeMeters);
        }

        LoadBestSession();
        StartSession();
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
            _groupSamples.Add(new ShotGroupSample(result.TargetOffsetMeters, result.TargetDistanceMeters));
            _groupStats = CalculateGroupStats();
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
        AddShotLogEntry(result, bonus, taskHit);

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
        if (_sessionEnded)
        {
            return;
        }

        _sessionEnded = true;
        if (bulletSpawner != null)
        {
            bulletSpawner.FireLocked = true;
        }

        bool newBest = SaveBestSession();
        float accuracy = _shotsFired > 0 ? _hits / (float)_shotsFired * 100f : 0f;
        OnSessionEnded?.Invoke(new PracticeSessionResult(
            _totalScore,
            _shotsFired,
            _hits,
            accuracy,
            _bestScore,
            _bestAccuracy,
            newBest,
            _groupStats));
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

        Image logBacking = CreateImage("Shot Log Backing", canvasRect, new Color(0f, 0f, 0f, 0.26f));
        RectTransform logRect = logBacking.rectTransform;
        logRect.anchorMin = new Vector2(1f, 1f);
        logRect.anchorMax = new Vector2(1f, 1f);
        logRect.pivot = new Vector2(1f, 1f);
        logRect.anchoredPosition = new Vector2(-24f, -278f);
        logRect.sizeDelta = new Vector2(380f, 168f);
        _shotLogText = CreateText("Shot Log Text", logRect, new Vector2(16f, -14f), 18, TextAnchor.UpperLeft);
        _shotLogText.rectTransform.sizeDelta = new Vector2(348f, 140f);

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
        string status = _sessionEnded ? "SESSION COMPLETE" : _courseName;

        string weapon = bulletSpawner != null ? bulletSpawner.WeaponState : "READY";
        _sessionText.text = $"{status}   SCORE {_totalScore:000}   AMMO {remaining}/{shotsPerSession}   {weapon}";
        _objectiveText.text = $"TASK: HIT {CurrentTaskDistance:0}m TARGET   TIME {FormatTime(_sessionTimer)}   ACC {accuracy:0}%";
        string group = _groupStats.SampleCount >= 2 ? $"   GRP {_groupStats.SpreadMeters * 100f:0.0}cm" : string.Empty;
        _lastShotText.text = $"{_lastShot}{group}   PB {_bestScore:000}/{_bestAccuracy:0}%";
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

        if (_shotLogText != null)
        {
            _shotLogText.text = BuildShotLogText();
        }
    }

    private void AddShotLogEntry(ShotResult result, int bonus, bool taskHit)
    {
        string entry;
        if (result.HitTarget)
        {
            string task = taskHit ? " TASK" : string.Empty;
            string bonusText = bonus > 0 ? $"+{bonus}" : string.Empty;
            entry = $"{_shotsFired:00}  {result.TargetDistanceMeters:000}m  {result.Score}{bonusText}{task}  {FormatOffset(result.TargetOffsetMeters)}";
        }
        else
        {
            entry = $"{_shotsFired:00}  MISS  {result.ImpactDistanceMeters:000}m";
        }

        _shotLog.Insert(0, entry);
        while (_shotLog.Count > 5)
        {
            _shotLog.RemoveAt(_shotLog.Count - 1);
        }
    }

    private string BuildShotLogText()
    {
        if (_shotLog.Count == 0)
        {
            return "SHOT LOG\n--";
        }

        string text = "SHOT LOG";
        for (int i = 0; i < _shotLog.Count; i++)
        {
            text += "\n" + _shotLog[i];
        }

        return text;
    }

    private static string FormatOffset(Vector2 offsetMeters)
    {
        string horizontal = offsetMeters.x >= 0f ? "R" : "L";
        string vertical = offsetMeters.y >= 0f ? "H" : "L";
        return $"{horizontal}{Mathf.Abs(offsetMeters.x) * 100f:0} {vertical}{Mathf.Abs(offsetMeters.y) * 100f:0}cm";
    }

    private ShotGroupStats CalculateGroupStats()
    {
        if (_groupSamples.Count == 0)
        {
            return ShotGroupStats.Empty;
        }

        Vector2 averageOffset = Vector2.zero;
        float averageDistance = 0f;
        for (int i = 0; i < _groupSamples.Count; i++)
        {
            averageOffset += _groupSamples[i].OffsetMeters;
            averageDistance += _groupSamples[i].DistanceMeters;
        }

        averageOffset /= _groupSamples.Count;
        averageDistance /= _groupSamples.Count;

        float maxSpread = 0f;
        for (int i = 0; i < _groupSamples.Count; i++)
        {
            for (int j = i + 1; j < _groupSamples.Count; j++)
            {
                float spread = Vector2.Distance(_groupSamples[i].OffsetMeters, _groupSamples[j].OffsetMeters);
                maxSpread = Mathf.Max(maxSpread, spread);
            }
        }

        if (_groupSamples.Count == 1)
        {
            maxSpread = 0f;
        }

        float moaPerMeter = averageDistance <= 0.01f ? 0f : 3437.75f / averageDistance;
        Vector2 correctionMoa = new Vector2(-averageOffset.x * moaPerMeter, -averageOffset.y * moaPerMeter);
        return new ShotGroupStats(_groupSamples.Count, averageOffset, maxSpread, averageDistance, correctionMoa);
    }

    private void LoadBestSession()
    {
        _bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        _bestAccuracy = PlayerPrefs.GetFloat(BestAccuracyKey, 0f);
    }

    private bool SaveBestSession()
    {
        float accuracy = _shotsFired > 0 ? _hits / (float)_shotsFired * 100f : 0f;
        bool newBest = _totalScore > _bestScore
            || (_totalScore == _bestScore && accuracy > _bestAccuracy);

        if (!newBest)
        {
            return false;
        }

        _bestScore = _totalScore;
        _bestAccuracy = accuracy;
        PlayerPrefs.SetInt(BestScoreKey, _bestScore);
        PlayerPrefs.SetFloat(BestAccuracyKey, _bestAccuracy);
        PlayerPrefs.Save();
        return true;
    }

    private string BestScoreKey => BestScorePrefix + _courseName;
    private string BestAccuracyKey => BestAccuracyPrefix + _courseName;

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

public readonly struct ShotGroupSample
{
    public ShotGroupSample(Vector2 offsetMeters, float distanceMeters)
    {
        OffsetMeters = offsetMeters;
        DistanceMeters = distanceMeters;
    }

    public Vector2 OffsetMeters { get; }
    public float DistanceMeters { get; }
}

public readonly struct ShotGroupStats
{
    public ShotGroupStats(int sampleCount, Vector2 averageOffsetMeters, float spreadMeters, float averageDistanceMeters, Vector2 correctionMoa)
    {
        SampleCount = sampleCount;
        AverageOffsetMeters = averageOffsetMeters;
        SpreadMeters = spreadMeters;
        AverageDistanceMeters = averageDistanceMeters;
        CorrectionMoa = correctionMoa;
    }

    public int SampleCount { get; }
    public Vector2 AverageOffsetMeters { get; }
    public float SpreadMeters { get; }
    public float AverageDistanceMeters { get; }
    public Vector2 CorrectionMoa { get; }
    public bool HasSamples => SampleCount > 0;

    public static ShotGroupStats Empty => new ShotGroupStats(0, Vector2.zero, 0f, 0f, Vector2.zero);
}

public readonly struct PracticeSessionResult
{
    public PracticeSessionResult(int score, int shotsFired, int hits, float accuracy, int bestScore, float bestAccuracy, bool newBest, ShotGroupStats groupStats)
    {
        Score = score;
        ShotsFired = shotsFired;
        Hits = hits;
        Accuracy = accuracy;
        BestScore = bestScore;
        BestAccuracy = bestAccuracy;
        NewBest = newBest;
        GroupStats = groupStats;
    }

    public int Score { get; }
    public int ShotsFired { get; }
    public int Hits { get; }
    public float Accuracy { get; }
    public int BestScore { get; }
    public float BestAccuracy { get; }
    public bool NewBest { get; }
    public ShotGroupStats GroupStats { get; }

    public string Grade
    {
        get
        {
            if (Score >= 96 && Accuracy >= 90f)
            {
                return "EXPERT";
            }

            if (Score >= 78 && Accuracy >= 70f)
            {
                return "MARKSMAN";
            }

            if (Score >= 55)
            {
                return "QUALIFIED";
            }

            return "RETRAIN";
        }
    }
}
