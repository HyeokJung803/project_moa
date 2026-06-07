using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 3D vector ballistic projectile with gravity, drag, wind, impact callbacks, and visible tracer.
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class Bullet : MonoBehaviour
{
    [Header("Tracer Visuals")]
    [SerializeField] private bool showTracer = true;
    [SerializeField] private float tracerLifetime = 1.15f;
    [SerializeField] private float tracerStartWidth = 0.055f;
    [SerializeField] private float tracerEndWidth = 0.006f;
    [SerializeField] private float projectileVisualSize = 0.045f;
    [SerializeField] private Color tracerHotColor = new Color(1f, 0.82f, 0.28f, 1f);
    [SerializeField] private Color tracerFadeColor = new Color(1f, 0.22f, 0.04f, 0f);

    [HideInInspector] public BallisticsData ballisticsData;
    [HideInInspector] public Vector3 initialVelocity;
    [HideInInspector] public float ambientTemperature;
    [HideInInspector] public float altitude;
    [HideInInspector] public Vector3 windVelocity;

    /// <summary>Impact event: point, surface normal, time of flight, collider.</summary>
    public System.Action<Vector3, Vector3, float, Collider> OnImpact;

    private Vector3 _velocity;
    private float _timeOfFlight;
    private float _airDensity;
    private bool _isActive;
    private TrailRenderer _trailRenderer;
    private GameObject _projectileVisual;

    private static readonly Vector3 Gravity = new Vector3(0f, -9.8f, 0f);
    private const float MaxLifetime = 5.0f;

    private void Awake()
    {
        SetupTracerVisuals();
    }

    private void Start()
    {
        _velocity = initialVelocity;
        _timeOfFlight = 0f;
        _isActive = true;

        _airDensity = AtmosphericModel.GetAirDensity(ambientTemperature, altitude);
        AtmosphericModel.LogAtmosphericConditions(ambientTemperature, altitude);
    }

    private void FixedUpdate()
    {
        if (!_isActive)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;
        _timeOfFlight += dt;

        if (_timeOfFlight > MaxLifetime)
        {
            DestroyBullet();
            return;
        }

        Vector3 relativeVelocity = _velocity - windVelocity;
        float speed = relativeVelocity.magnitude;

        float dragForceMagnitude = 0.5f
            * _airDensity
            * speed * speed
            * ballisticsData.dragCoefficient
            * ballisticsData.bulletArea;

        Vector3 dragAcceleration = Vector3.zero;
        if (speed > 0.001f)
        {
            dragAcceleration = -(dragForceMagnitude / ballisticsData.bulletMass) * relativeVelocity.normalized;
        }

        Vector3 totalAcceleration = Gravity + dragAcceleration;
        _velocity += totalAcceleration * dt;
        transform.position += _velocity * dt;

        if (_velocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(_velocity);
        }

        float castRadius = 0.01f;
        float castDistance = speed * dt * 1.5f;

        if (Physics.SphereCast(
            transform.position,
            castRadius,
            _velocity.normalized,
            out RaycastHit hit,
            castDistance,
            ~LayerMask.GetMask("Bullet")))
        {
            HandleImpact(hit);
        }
    }

    private void HandleImpact(RaycastHit hit)
    {
        _isActive = false;
        transform.position = hit.point;

        OnImpact?.Invoke(hit.point, hit.normal, _timeOfFlight, hit.collider);

        Debug.Log($"[Bullet] Impact | Position: {hit.point} | TOF: {_timeOfFlight:F3}s "
                + $"| Collider: {hit.collider.name} | Remaining velocity: {_velocity.magnitude:F1} m/s");

        DestroyBullet();
    }

    private void DestroyBullet()
    {
        if (_projectileVisual != null)
        {
            _projectileVisual.SetActive(false);
        }

        Destroy(gameObject, Mathf.Max(0.3f, tracerLifetime));
    }

    private void SetupTracerVisuals()
    {
        _trailRenderer = GetComponent<TrailRenderer>();
        if (_trailRenderer == null || !showTracer)
        {
            return;
        }

        _trailRenderer.enabled = true;
        _trailRenderer.emitting = true;
        _trailRenderer.time = tracerLifetime;
        _trailRenderer.minVertexDistance = 0.04f;
        _trailRenderer.alignment = LineAlignment.View;
        _trailRenderer.textureMode = LineTextureMode.Stretch;
        _trailRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _trailRenderer.receiveShadows = false;
        _trailRenderer.widthCurve = AnimationCurve.Linear(0f, tracerStartWidth, 1f, tracerEndWidth);
        _trailRenderer.colorGradient = CreateTracerGradient();
        _trailRenderer.sharedMaterial = CreateTracerMaterial("Tracer Trail", tracerHotColor);

        _projectileVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _projectileVisual.name = "Visible Tracer Core";
        _projectileVisual.transform.SetParent(transform, false);
        _projectileVisual.transform.localPosition = Vector3.zero;
        _projectileVisual.transform.localScale = Vector3.one * projectileVisualSize;

        Collider visualCollider = _projectileVisual.GetComponent<Collider>();
        if (visualCollider != null)
        {
            visualCollider.enabled = false;
        }

        Renderer visualRenderer = _projectileVisual.GetComponent<Renderer>();
        if (visualRenderer != null)
        {
            visualRenderer.shadowCastingMode = ShadowCastingMode.Off;
            visualRenderer.receiveShadows = false;
            visualRenderer.sharedMaterial = CreateTracerMaterial("Tracer Core", tracerHotColor);
        }
    }

    private Gradient CreateTracerGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(tracerHotColor, 0f),
                new GradientColorKey(new Color(1f, 0.48f, 0.08f, 1f), 0.55f),
                new GradientColorKey(tracerFadeColor, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.62f, 0.65f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private static Material CreateTracerMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
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
}
