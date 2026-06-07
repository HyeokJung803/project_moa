using UnityEngine;

/// <summary>
/// Lightweight procedural wind motion for range flags.
/// </summary>
public class WindFlagAnimator : MonoBehaviour
{
    [SerializeField] private float side = 1f;
    [SerializeField] private float speed = 1f;
    [SerializeField] private float gustStrength = 8f;

    private Vector3 _baseScale;
    private Vector3 _basePosition;
    private float _phase;

    private void Awake()
    {
        _baseScale = transform.localScale;
        _basePosition = transform.localPosition;
        _phase = Random.Range(0f, 100f);
    }

    public void Configure(float flagSide, float animationSpeed, float gust)
    {
        side = Mathf.Sign(flagSide == 0f ? 1f : flagSide);
        speed = animationSpeed;
        gustStrength = gust;
    }

    private void Update()
    {
        float time = Time.time * speed + _phase;
        float flap = Mathf.Sin(time * 3.7f) * 0.5f + Mathf.Sin(time * 1.3f) * 0.5f;
        float lift = Mathf.Sin(time * 2.1f) * 0.035f;

        transform.localRotation = Quaternion.Euler(0f, side * (8f + flap * gustStrength), flap * 4f);
        transform.localScale = new Vector3(
            _baseScale.x * (1f + Mathf.Abs(flap) * 0.035f),
            _baseScale.y * (1f - Mathf.Abs(flap) * 0.025f),
            _baseScale.z);
        transform.localPosition = _basePosition + new Vector3(0f, lift, Mathf.Sin(time * 4.6f) * 0.025f);
    }
}
