using System.Collections;
using UnityEngine;

/// <summary>
/// Creates bullets from player input and injects initial ballistic conditions.
/// Scope turret values are converted from MOA into local angular offsets.
/// </summary>
public class BulletSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private Transform aimTransform;
    [SerializeField] private BallisticsData ballisticsData;

    [Header("Kestrel Measurements (temporary)")]
    [SerializeField] private float ambientTemperature = 15f;
    [SerializeField] private float altitude = 100f;

    [Header("Wind (temporary)")]
    [SerializeField] private Vector3 windVelocity = new Vector3(2f, 0f, 0f);
    [SerializeField] private bool enableWindGusts = true;
    [SerializeField] private float gustStrength = 0.65f;
    [SerializeField] private float gustCycleSpeed = 0.08f;

    [Header("Scope Turrets")]
    [SerializeField] private float elevationMOA = 0f;
    [SerializeField] private float windageMOA = 0f;

    [Header("Fire Feedback")]
    [SerializeField] private bool enableProceduralFeedback = true;
    [SerializeField] private float boltCycleSeconds = 1.15f;
    [SerializeField] private float muzzleFlashDuration = 0.045f;
    [SerializeField] private float muzzleFlashIntensity = 5f;
    [SerializeField] private Color muzzleFlashColor = new Color(1f, 0.68f, 0.28f, 1f);
    [SerializeField] private Color targetHitMarkColor = new Color(0.02f, 0.018f, 0.015f, 1f);
    [SerializeField] private Color bullseyeHitMarkColor = new Color(0.85f, 0.04f, 0.025f, 1f);
    [SerializeField] private float impactMarkLifetime = 90f;

    public float ElevationMOA => elevationMOA;
    public float WindageMOA => windageMOA;
    public float AmbientTemperature => ambientTemperature;
    public float Altitude => altitude;
    public Vector3 WindVelocity => windVelocity;
    public Vector3 CurrentWindVelocity => GetCurrentWindVelocity();
    public float GustMetersPerSecond => CurrentWindVelocity.x - windVelocity.x;
    public bool IsCyclingBolt => _boltCycleTimer > 0f;
    public float BoltReady01 => boltCycleSeconds <= 0f ? 1f : 1f - Mathf.Clamp01(_boltCycleTimer / boltCycleSeconds);
    public string WeaponState => FireLocked ? "LOCKED" : IsCyclingBolt ? "CYCLING" : "READY";
    public bool InputLocked { get; set; }
    public bool FireLocked { get; set; }
    public event System.Action OnFired;
    public event System.Action<float> OnBoltCycle;
    public event System.Action<string> OnImpactFeedback;
    public event System.Action<ShotResult> OnShotResolved;

    private const float MOA_TO_RAD = 0.000290888f;

    private Light _muzzleFlashLight;
    private ParticleSystem _muzzleParticles;
    private AudioSource _audioSource;
    private AudioClip _shotClip;
    private AudioClip _impactClip;
    private AudioClip _boltClip;
    private float _boltCycleTimer;

    private void Awake()
    {
        if (aimTransform == null)
        {
            Camera mainCamera = Camera.main;
            aimTransform = mainCamera != null ? mainCamera.transform : muzzleTransform;
        }

        if (enableProceduralFeedback)
        {
            SetupProceduralFeedback();
        }
    }

    private void Update()
    {
        UpdateBoltCycle();
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;

        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            Fire();
        }
    }

    public void SetElevationMOA(float moa)
    {
        elevationMOA = moa;
    }

    public void SetWindageMOA(float moa)
    {
        windageMOA = moa;
    }

    public void SetScopeAdjustment(float elevation, float windage)
    {
        elevationMOA = elevation;
        windageMOA = windage;
    }

    public void SetAtmosphere(float temperatureCelsius, float altitudeMeters)
    {
        ambientTemperature = Mathf.Clamp(temperatureCelsius, -35f, 50f);
        altitude = Mathf.Clamp(altitudeMeters, -100f, 4500f);
    }

    public void SetWindVelocity(Vector3 velocity)
    {
        windVelocity = Vector3.ClampMagnitude(velocity, 25f);
    }

    public void SetGustStrength(float metersPerSecond)
    {
        gustStrength = Mathf.Clamp(metersPerSecond, 0f, 4f);
    }

    public void AdjustCrosswind(float deltaMetersPerSecond)
    {
        windVelocity = new Vector3(
            Mathf.Clamp(windVelocity.x + deltaMetersPerSecond, -18f, 18f),
            windVelocity.y,
            windVelocity.z);
    }

    public DopeSolution CalculateDope(float targetDistanceMeters)
    {
        if (ballisticsData == null || targetDistanceMeters <= 0f)
        {
            return DopeSolution.Invalid(targetDistanceMeters);
        }

        Vector3 position = Vector3.zero;
        Vector3 velocity = Vector3.forward * ballisticsData.muzzleVelocity;
        Vector3 wind = CurrentWindVelocity;
        float airDensity = AtmosphericModel.GetAirDensity(ambientTemperature, altitude);
        float time = 0f;
        float dt = 0.005f;
        Vector3 previousPosition = position;
        float previousTime = time;

        for (int i = 0; i < 2000; i++)
        {
            previousPosition = position;
            previousTime = time;

            Vector3 relativeVelocity = velocity - wind;
            float speed = relativeVelocity.magnitude;
            float dragForceMagnitude = 0.5f
                * airDensity
                * speed * speed
                * ballisticsData.dragCoefficient
                * ballisticsData.bulletArea;

            Vector3 dragAcceleration = Vector3.zero;
            if (speed > 0.001f)
            {
                dragAcceleration = -(dragForceMagnitude / ballisticsData.bulletMass) * relativeVelocity.normalized;
            }

            velocity += (new Vector3(0f, -9.8f, 0f) + dragAcceleration) * dt;
            position += velocity * dt;
            time += dt;

            if (position.z >= targetDistanceMeters)
            {
                float segment = position.z - previousPosition.z;
                float t = Mathf.Approximately(segment, 0f)
                    ? 1f
                    : Mathf.Clamp01((targetDistanceMeters - previousPosition.z) / segment);
                Vector3 impact = Vector3.Lerp(previousPosition, position, t);
                float impactTime = Mathf.Lerp(previousTime, time, t);
                float moaPerMeter = 3437.75f / targetDistanceMeters;
                float elevation = -impact.y * moaPerMeter;
                float windage = -impact.x * moaPerMeter;
                return new DopeSolution(true, targetDistanceMeters, elevation, windage, impact.y, impact.x, impactTime);
            }
        }

        return DopeSolution.Invalid(targetDistanceMeters);
    }

    private void Fire()
    {
        if (InputLocked)
        {
            return;
        }

        if (FireLocked)
        {
            OnImpactFeedback?.Invoke("SESSION COMPLETE   PRESS R");
            return;
        }

        if (IsCyclingBolt)
        {
            OnImpactFeedback?.Invoke($"BOLT CYCLING   {BoltReady01 * 100f:0}%");
            return;
        }

        if (bulletPrefab == null || muzzleTransform == null || ballisticsData == null)
        {
            Debug.LogWarning("[BulletSpawner] Missing bullet prefab, muzzle transform, or ballistics data.");
            return;
        }

        float elevationRad = elevationMOA * MOA_TO_RAD;
        float windageRad = windageMOA * MOA_TO_RAD;

        Transform directionTransform = aimTransform != null ? aimTransform : muzzleTransform;
        Quaternion fireRotation = directionTransform.rotation * Quaternion.Euler(
            -elevationRad * Mathf.Rad2Deg,
             windageRad * Mathf.Rad2Deg,
             0f);

        Vector3 fireDirection = fireRotation * Vector3.forward;

        GameObject bulletObj = Instantiate(
            bulletPrefab,
            muzzleTransform.position,
            Quaternion.LookRotation(fireDirection));

        Bullet bullet = bulletObj.GetComponent<Bullet>();
        bullet.ballisticsData = ballisticsData;
        bullet.initialVelocity = fireDirection * ballisticsData.muzzleVelocity;
        bullet.ambientTemperature = ambientTemperature;
        bullet.altitude = altitude;
        bullet.windVelocity = CurrentWindVelocity;
        bullet.OnImpact += HandleBulletImpact;

        OnFired?.Invoke();
        PlayFireFeedback();
        StartBoltCycle();
    }

    private Vector3 GetCurrentWindVelocity()
    {
        if (!enableWindGusts || Mathf.Approximately(gustStrength, 0f))
        {
            return windVelocity;
        }

        float noise = Mathf.PerlinNoise(Time.time * gustCycleSpeed + 19.7f, 0.37f);
        float gust = (noise - 0.5f) * 2f * gustStrength;
        float lateralPulse = Mathf.Sin(Time.time * gustCycleSpeed * 9.1f + 1.4f) * gustStrength * 0.18f;
        return windVelocity + new Vector3(gust + lateralPulse, 0f, 0f);
    }

    private void HandleBulletImpact(Vector3 impactPoint, Vector3 normal, float timeOfFlight, Collider hitCollider)
    {
        float distanceToImpact = Vector3.Distance(muzzleTransform.position, impactPoint);
        float soundDelay = distanceToImpact / 343f;
        RangeTarget target = hitCollider != null ? hitCollider.GetComponentInParent<RangeTarget>() : null;

        if (enableProceduralFeedback)
        {
            SpawnImpactParticles(impactPoint, normal, target != null);
            SpawnImpactMark(impactPoint, normal, target);
        }

        if (target != null)
        {
            int score = target.GetScore(impactPoint);
            Vector2 targetOffset = target.GetLocalImpactOffset(impactPoint);
            string feedback = $"{target.DistanceMeters:0}m HIT  SCORE {score}  TOF {timeOfFlight:F2}s";
            OnImpactFeedback?.Invoke(feedback);
            OnShotResolved?.Invoke(ShotResult.Hit(target.DistanceMeters, distanceToImpact, score, timeOfFlight, impactPoint, targetOffset));
            Debug.Log($"[RangeTarget] {feedback}");
        }
        else
        {
            OnImpactFeedback?.Invoke($"MISS  {distanceToImpact:0}m  TOF {timeOfFlight:F2}s");
            OnShotResolved?.Invoke(ShotResult.Miss(distanceToImpact, timeOfFlight, impactPoint));
        }

        Debug.Log($"[BulletSpawner] TOF: {timeOfFlight:F3}s | Distance: {distanceToImpact:F1}m "
                + $"| Impact sound delay: {soundDelay:F3}s");

        if (enableProceduralFeedback && _impactClip != null)
        {
            StartCoroutine(PlayImpactSoundDelayed(soundDelay));
        }
    }

    private void SpawnImpactParticles(Vector3 impactPoint, Vector3 normal, bool targetHit)
    {
        GameObject particlesObject = new GameObject(targetHit ? "Target Hit Chips" : "Dust Impact Puff");
        particlesObject.transform.position = impactPoint + normal * 0.025f;
        particlesObject.transform.rotation = Quaternion.LookRotation(normal);

        ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.35f;
        main.startLifetime = targetHit ? 0.32f : 0.55f;
        main.startSpeed = targetHit ? 1.7f : 2.2f;
        main.startSize = targetHit ? 0.035f : 0.12f;
        main.startColor = targetHit
            ? new Color(0.18f, 0.16f, 0.13f, 0.9f)
            : new Color(0.5f, 0.43f, 0.35f, 0.75f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = targetHit ? 34f : 26f;
        shape.radius = targetHit ? 0.02f : 0.08f;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.sharedMaterial = CreateRuntimeMaterial(
                targetHit ? "Target Chip Particle" : "Dust Impact Particle",
                targetHit ? new Color(0.18f, 0.16f, 0.13f, 0.9f) : new Color(0.5f, 0.43f, 0.35f, 0.75f));
        }

        particles.Emit(targetHit ? 16 : 26);
        Destroy(particlesObject, 2f);
    }

    private void SpawnImpactMark(Vector3 impactPoint, Vector3 normal, RangeTarget target)
    {
        if (target == null)
        {
            return;
        }

        int score = target.GetScore(impactPoint);
        GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        mark.name = $"Bullet Hole Score {score}";
        mark.transform.position = impactPoint + normal * 0.006f;
        mark.transform.rotation = Quaternion.LookRotation(normal) * Quaternion.Euler(90f, 0f, 0f);
        mark.transform.localScale = new Vector3(score >= 10 ? 0.045f : 0.035f, 0.004f, score >= 10 ? 0.045f : 0.035f);

        Collider markCollider = mark.GetComponent<Collider>();
        if (markCollider != null)
        {
            markCollider.enabled = false;
        }

        Renderer renderer = mark.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateRuntimeMaterial(
                "Runtime Bullet Hole",
                score >= 10 ? bullseyeHitMarkColor : targetHitMarkColor);
        }

        Destroy(mark, impactMarkLifetime);
    }

    private static Material CreateRuntimeMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        return material;
    }

    private void SetupProceduralFeedback()
    {
        if (muzzleTransform != null)
        {
            GameObject flashObject = new GameObject("Procedural Muzzle Flash");
            flashObject.transform.SetParent(muzzleTransform, false);

            _muzzleFlashLight = flashObject.AddComponent<Light>();
            _muzzleFlashLight.type = LightType.Point;
            _muzzleFlashLight.range = 3f;
            _muzzleFlashLight.intensity = 0f;
            _muzzleFlashLight.color = muzzleFlashColor;

            _muzzleParticles = flashObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _muzzleParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.055f;
            main.startSpeed = 2.6f;
            main.startSize = 0.18f;
            main.startColor = muzzleFlashColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = _muzzleParticles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = _muzzleParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.025f;

            ParticleSystemRenderer muzzleRenderer = _muzzleParticles.GetComponent<ParticleSystemRenderer>();
            if (muzzleRenderer != null)
            {
                muzzleRenderer.sharedMaterial = CreateRuntimeMaterial("Muzzle Flash Particle", muzzleFlashColor);
            }
        }

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0.25f;
        _audioSource.volume = 0.8f;

        _shotClip = CreateNoiseClip("Procedural Rifle Shot", 0.12f, 0.78f, 80f);
        _impactClip = CreateNoiseClip("Procedural Distant Impact", 0.07f, 0.34f, 35f);
        _boltClip = CreateBoltClip();
    }

    private void PlayFireFeedback()
    {
        if (!enableProceduralFeedback)
        {
            return;
        }

        if (_shotClip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_shotClip);
        }

        if (_muzzleParticles != null)
        {
            _muzzleParticles.Emit(14);
        }

        if (_muzzleFlashLight != null)
        {
            StopCoroutine(nameof(FlashRoutine));
            StartCoroutine(nameof(FlashRoutine));
        }
    }

    private void StartBoltCycle()
    {
        _boltCycleTimer = Mathf.Max(0.05f, boltCycleSeconds);
        OnBoltCycle?.Invoke(0f);

        if (enableProceduralFeedback && _audioSource != null && _boltClip != null)
        {
            StartCoroutine(PlayBoltSoundDelayed(0.22f));
        }
    }

    private void UpdateBoltCycle()
    {
        if (_boltCycleTimer <= 0f)
        {
            return;
        }

        _boltCycleTimer = Mathf.Max(0f, _boltCycleTimer - Time.deltaTime);
        OnBoltCycle?.Invoke(BoltReady01);
    }

    private IEnumerator FlashRoutine()
    {
        _muzzleFlashLight.intensity = muzzleFlashIntensity;
        yield return new WaitForSeconds(muzzleFlashDuration);
        _muzzleFlashLight.intensity = 0f;
    }

    private IEnumerator PlayImpactSoundDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_audioSource != null && _impactClip != null)
        {
            _audioSource.PlayOneShot(_impactClip, 0.55f);
        }
    }

    private IEnumerator PlayBoltSoundDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_audioSource != null && _boltClip != null)
        {
            _audioSource.PlayOneShot(_boltClip, 0.34f);
        }
    }

    private static AudioClip CreateNoiseClip(string clipName, float duration, float amplitude, float decay)
    {
        const int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-decay * t);
            float lowThump = Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.45f;
            float noise = Random.Range(-1f, 1f);
            data[i] = (noise * 0.55f + lowThump) * envelope * amplitude;
        }

        AudioClip clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateBoltClip()
    {
        const int sampleRate = 44100;
        float duration = 0.38f;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float clickOne = Mathf.Exp(-260f * Mathf.Abs(t - 0.05f));
            float scrape = Mathf.Sin(2f * Mathf.PI * 720f * t) * Mathf.Exp(-10f * Mathf.Max(0f, t - 0.08f));
            float clickTwo = Mathf.Exp(-240f * Mathf.Abs(t - 0.28f));
            float noise = Random.Range(-1f, 1f) * 0.16f;
            data[i] = (clickOne * 0.6f + scrape * 0.22f + clickTwo * 0.75f + noise) * 0.28f;
        }

        AudioClip clip = AudioClip.Create("Procedural Bolt Cycle", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

public readonly struct ShotResult
{
    public ShotResult(bool hitTarget, float targetDistanceMeters, float impactDistanceMeters, int score, float timeOfFlight, Vector3 impactPoint, Vector2 targetOffsetMeters)
    {
        HitTarget = hitTarget;
        TargetDistanceMeters = targetDistanceMeters;
        ImpactDistanceMeters = impactDistanceMeters;
        Score = score;
        TimeOfFlight = timeOfFlight;
        ImpactPoint = impactPoint;
        TargetOffsetMeters = targetOffsetMeters;
    }

    public bool HitTarget { get; }
    public float TargetDistanceMeters { get; }
    public float ImpactDistanceMeters { get; }
    public int Score { get; }
    public float TimeOfFlight { get; }
    public Vector3 ImpactPoint { get; }
    public Vector2 TargetOffsetMeters { get; }

    public static ShotResult Hit(float targetDistanceMeters, float impactDistanceMeters, int score, float timeOfFlight, Vector3 impactPoint, Vector2 targetOffsetMeters)
    {
        return new ShotResult(true, targetDistanceMeters, impactDistanceMeters, score, timeOfFlight, impactPoint, targetOffsetMeters);
    }

    public static ShotResult Miss(float impactDistanceMeters, float timeOfFlight, Vector3 impactPoint)
    {
        return new ShotResult(false, 0f, impactDistanceMeters, 0, timeOfFlight, impactPoint, Vector2.zero);
    }
}

public readonly struct DopeSolution
{
    public DopeSolution(bool valid, float distanceMeters, float elevationMoa, float windageMoa, float dropMeters, float driftMeters, float timeOfFlight)
    {
        IsValid = valid;
        DistanceMeters = distanceMeters;
        ElevationMoa = elevationMoa;
        WindageMoa = windageMoa;
        DropMeters = dropMeters;
        DriftMeters = driftMeters;
        TimeOfFlight = timeOfFlight;
    }

    public bool IsValid { get; }
    public float DistanceMeters { get; }
    public float ElevationMoa { get; }
    public float WindageMoa { get; }
    public float DropMeters { get; }
    public float DriftMeters { get; }
    public float TimeOfFlight { get; }

    public static DopeSolution Invalid(float distanceMeters)
    {
        return new DopeSolution(false, distanceMeters, 0f, 0f, 0f, 0f, 0f);
    }
}
