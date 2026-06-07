using UnityEngine;

/// <summary>
/// Moves a target carrier sideways to create a wind-and-lead practice lane.
/// </summary>
public class MovingTarget : MonoBehaviour
{
    [SerializeField] private float travelWidthMeters = 7f;
    [SerializeField] private float speedMetersPerSecond = 1.35f;
    [SerializeField] private float pauseAtEdgesSeconds = 0.45f;

    private Vector3 _startPosition;
    private float _direction = 1f;
    private float _pauseTimer;

    private void Awake()
    {
        _startPosition = transform.position;
    }

    public void Configure(float travelWidth, float speed, float edgePause)
    {
        travelWidthMeters = Mathf.Max(0.5f, travelWidth);
        speedMetersPerSecond = Mathf.Max(0.05f, speed);
        pauseAtEdgesSeconds = Mathf.Max(0f, edgePause);
    }

    private void Update()
    {
        if (_pauseTimer > 0f)
        {
            _pauseTimer -= Time.deltaTime;
            return;
        }

        Vector3 position = transform.position;
        position.x += _direction * speedMetersPerSecond * Time.deltaTime;

        float halfTravel = travelWidthMeters * 0.5f;
        float localX = position.x - _startPosition.x;
        if (Mathf.Abs(localX) >= halfTravel)
        {
            position.x = _startPosition.x + Mathf.Sign(localX) * halfTravel;
            _direction *= -1f;
            _pauseTimer = pauseAtEdgesSeconds;
        }

        transform.position = position;
    }
}
