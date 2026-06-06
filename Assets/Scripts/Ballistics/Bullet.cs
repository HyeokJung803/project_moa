using UnityEngine;

/// <summary>
/// 3D 벡터 기반 물리 탄도 발사체.
/// - 중력 낙차 (9.8 m/s²)
/// - 공기 저항 (Drag: ρ, Cd, A, v² 적용)
/// - 대기 밀도 실시간 반영 (기온/고도 → AtmosphericModel)
/// - 바람 벡터 외부 주입 지원
/// - 지형 충돌 및 탄착 이벤트 콜백
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class Bullet : MonoBehaviour
{
    // ── 외부 주입 데이터 (BulletSpawner에서 발사 시 설정) ──────────────────
    [HideInInspector] public BallisticsData ballisticsData;
    [HideInInspector] public Vector3 initialVelocity;   // 총구 방향 × 초구속도
    [HideInInspector] public float ambientTemperature;  // 케스트렐 기온 (°C)
    [HideInInspector] public float altitude;            // 케스트렐 고도 (m)
    [HideInInspector] public Vector3 windVelocity;      // 바람 벡터 (m/s, 월드 공간)

    // ── 이벤트 ──────────────────────────────────────────────────────────────
    /// <summary>탄착 이벤트: 충돌 위치, 충돌 표면 법선, 비행 시간(TOF), 충돌체 전달</summary>
    public System.Action<Vector3, Vector3, float, Collider> OnImpact;

    // ── 내부 상태 ────────────────────────────────────────────────────────────
    private Vector3 _velocity;          // 현재 속도 벡터 (m/s)
    private float _timeOfFlight;        // 누적 비행 시간 (초)
    private float _airDensity;          // 현재 공기 밀도 ρ
    private bool _isActive;

    // ── 물리 상수 ────────────────────────────────────────────────────────────
    private static readonly Vector3 GRAVITY = new Vector3(0f, -9.8f, 0f);

    // ── 수명 제한 (1km 사거리 기준 여유) ────────────────────────────────────
    private const float MAX_LIFETIME = 5.0f;  // 초

    // ────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Start()
    {
        _velocity = initialVelocity;
        _timeOfFlight = 0f;
        _isActive = true;

        // 발사 시점에 공기밀도 한 번 계산 (고도는 발사 지점 기준 고정값)
        // 추후 고도변화 지형에서는 매 프레임 갱신으로 전환 가능
        _airDensity = AtmosphericModel.GetAirDensity(ambientTemperature, altitude);

        AtmosphericModel.LogAtmosphericConditions(ambientTemperature, altitude);
    }

    /// <summary>
    /// FixedUpdate: 물리 연산은 반드시 고정 타임스텝에서 수행.
    /// Unity 기본 FixedUpdate = 0.02s (50Hz). 저격 시뮬레이터는 0.005s(200Hz) 권장.
    /// Edit > Project Settings > Time > Fixed Timestep = 0.005 으로 변경할 것.
    /// </summary>
    private void FixedUpdate()
    {
        if (!_isActive) return;

        float dt = Time.fixedDeltaTime;
        _timeOfFlight += dt;

        // 수명 초과 시 자동 소멸
        if (_timeOfFlight > MAX_LIFETIME)
        {
            DestroyBullet();
            return;
        }

        // ── 1. 항력 가속도 계산 ──────────────────────────────────────────
        // 상대 속도 = 탄환 속도 - 바람 속도 (바람이 불어오는 방향이 저항)
        Vector3 relativeVelocity = _velocity - windVelocity;
        float speed = relativeVelocity.magnitude;

        // Fd = 0.5 × ρ × v² × Cd × A
        float dragForceMagnitude = 0.5f
            * _airDensity
            * (speed * speed)
            * ballisticsData.dragCoefficient
            * ballisticsData.bulletArea;

        // 항력 방향은 진행 방향의 반대 (속도 정규화 벡터 × 음수)
        Vector3 dragAcceleration = Vector3.zero;
        if (speed > 0.001f)  // 분모 0 방지
        {
            dragAcceleration = -(dragForceMagnitude / ballisticsData.bulletMass)
                               * relativeVelocity.normalized;
        }

        // ── 2. 총 가속도 = 중력 + 항력 ──────────────────────────────────
        Vector3 totalAcceleration = GRAVITY + dragAcceleration;

        // ── 3. Euler 적분으로 속도 및 위치 갱신 ─────────────────────────
        _velocity += totalAcceleration * dt;
        transform.position += _velocity * dt;

        // ── 4. 탄환 회전: 진행 방향으로 자동 정렬 ───────────────────────
        if (_velocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(_velocity);
        }

        // ── 5. 충돌 감지 (SphereCast: 탄환 크기 보정, Raycast보다 안정적) ─
        float castRadius = 0.01f;   // 탄환 반지름 (m) — 약 10mm
        float castDistance = speed * dt * 1.5f;  // 이번 프레임 이동 거리 + 여유

        if (Physics.SphereCast(
            transform.position,
            castRadius,
            _velocity.normalized,
            out RaycastHit hit,
            castDistance,
            ~LayerMask.GetMask("Bullet")))  // 탄환 레이어 자신 제외
        {
            HandleImpact(hit);
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────────
    #region Impact & Destruction

    private void HandleImpact(RaycastHit hit)
    {
        _isActive = false;

        // 탄착 위치로 즉시 이동
        transform.position = hit.point;

        // 이벤트 발행 → BulletSpawner 또는 ImpactManager에서 수신
        // (먼지 파티클, TOF 기반 명중음 딜레이, 점수 처리 등)
        OnImpact?.Invoke(hit.point, hit.normal, _timeOfFlight, hit.collider);

        Debug.Log($"[Bullet] 탄착 | 위치: {hit.point} | TOF: {_timeOfFlight:F3}s "
                + $"| 충돌체: {hit.collider.name} | 잔여속도: {_velocity.magnitude:F1} m/s");

        DestroyBullet();
    }

    private void DestroyBullet()
    {
        // TrailRenderer가 사라지는 경우 잔상이 끊기므로 0.3초 딜레이 후 소멸
        Destroy(gameObject, 0.3f);
    }

    #endregion
}
