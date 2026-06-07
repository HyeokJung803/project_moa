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

    [Header("Scope Turrets")]
    [SerializeField] private float elevationMOA = 0f;
    [SerializeField] private float windageMOA = 0f;

    [Header("Fire Feedback")]
    [SerializeField] private bool enableProceduralFeedback = true;
    [SerializeField] private float muzzleFlashDuration = 0.045f;
    [SerializeField] private float muzzleFlashIntensity = 5f;
    [SerializeField] private Color muzzleFlashColor = new Color(1f, 0.68f, 0.28f, 1f);
    [SerializeField] private Color targetHitMarkColor = new Color(0.02f, 0.018f, 0.015f, 1f);
    [SerializeField] private Color bullseyeHitMarkColor = new Color(0.85f, 0.04f, 0.025f, 1f);
    [SerializeField] private float impactMarkLifetime = 90f;

    public float ElevationMOA => elevationMOA;
    public float WindageMOA => windageMOA;
    public bool FireLocked { get; set; }
    public event System.Action OnFired;
    public event System.Action<string> OnImpactFeedback;
    public event System.Action<ShotResult> OnShotResolved;

    private const float MOA_TO_RAD = 0.000290888f;

    private Light _muzzleFlashLight;
    private ParticleSystem _muzzleParticles;
    private AudioSource _audioSource;
    private AudioClip _shotClip;
    private AudioClip _impactClip;

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

    private void Fire()
    {
        if (FireLocked)
        {
            OnImpactFeedback?.Invoke("SESSION COMPLETE   PRESS R");
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
        bullet.windVelocity = windVelocity;
        bullet.OnImpact += HandleBulletImpact;

        OnFired?.Invoke();
        PlayFireFeedback();
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
            string feedback = $"{target.DistanceMeters:0}m HIT  SCORE {score}  TOF {timeOfFlight:F2}s";
            OnImpactFeedback?.Invoke(feedback);
            OnShotResolved?.Invoke(ShotResult.Hit(target.DistanceMeters, distanceToImpact, score, timeOfFlight, impactPoint));
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
}

public readonly struct ShotResult
{
    public ShotResult(bool hitTarget, float targetDistanceMeters, float impactDistanceMeters, int score, float timeOfFlight, Vector3 impactPoint)
    {
        HitTarget = hitTarget;
        TargetDistanceMeters = targetDistanceMeters;
        ImpactDistanceMeters = impactDistanceMeters;
        Score = score;
        TimeOfFlight = timeOfFlight;
        ImpactPoint = impactPoint;
    }

    public bool HitTarget { get; }
    public float TargetDistanceMeters { get; }
    public float ImpactDistanceMeters { get; }
    public int Score { get; }
    public float TimeOfFlight { get; }
    public Vector3 ImpactPoint { get; }

    public static ShotResult Hit(float targetDistanceMeters, float impactDistanceMeters, int score, float timeOfFlight, Vector3 impactPoint)
    {
        return new ShotResult(true, targetDistanceMeters, impactDistanceMeters, score, timeOfFlight, impactPoint);
    }

    public static ShotResult Miss(float impactDistanceMeters, float timeOfFlight, Vector3 impactPoint)
    {
        return new ShotResult(false, 0f, impactDistanceMeters, 0, timeOfFlight, impactPoint);
    }
}
