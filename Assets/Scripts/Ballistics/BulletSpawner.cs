using UnityEngine;

/// <summary>
/// 플레이어 입력을 받아 탄환을 생성하고 초기 조건을 주입하는 발사 관리자.
/// 스코프 Elevation/Windage 다이얼 값을 총구 각도 오프셋으로 변환하는
/// MOA → 각도(radian) 변환 로직 포함.
/// </summary>
public class BulletSpawner : MonoBehaviour
{
    [Header("레퍼런스")]
    [SerializeField] private GameObject bulletPrefab;       // Bullet 컴포넌트 부착된 프리팹
    [SerializeField] private Transform muzzleTransform;     // 총구 위치/방향 Transform
    [SerializeField] private BallisticsData ballisticsData; // 탄환 데이터 에셋

    [Header("케스트렐 측정값 (임시 — 추후 KestrelUI에서 주입)")]
    [SerializeField] private float ambientTemperature = 15f;   // °C
    [SerializeField] private float altitude = 100f;            // m

    [Header("바람 (임시 — 추후 WindSystem에서 주입)")]
    [SerializeField] private Vector3 windVelocity = new Vector3(2f, 0f, 0f); // m/s

    [Header("스코프 다이얼 조절값")]
    [Tooltip("Elevation 터렛 누적값 (MOA 단위). 마우스 휠로 조작 예정.")]
    [SerializeField] private float elevationMOA = 0f;

    [Tooltip("Windage 터렛 누적값 (MOA 단위).")]
    [SerializeField] private float windageMOA = 0f;

    // 1 MOA = 1/60 degree = 0.000290888 radian
    private const float MOA_TO_RAD = 0.000290888f;

    private void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            Fire();
        }
    }

    private void Fire()
    {
        // ── 총구 방향에 MOA 오프셋 적용 ─────────────────────────────────
        // Elevation: 총구를 로컬 X축 기준 상방 회전
        // Windage: 총구를 로컬 Y축 기준 좌우 회전
        float elevationRad = elevationMOA * MOA_TO_RAD;
        float windageRad = windageMOA * MOA_TO_RAD;

        Quaternion muzzleOffsetRotation = Quaternion.Euler(
            -elevationRad * Mathf.Rad2Deg,  // Unity는 degree 입력
             windageRad * Mathf.Rad2Deg,
             0f
        );

        Vector3 fireDirection = muzzleOffsetRotation * muzzleTransform.forward;

        // ── 탄환 생성 및 초기 조건 주입 ─────────────────────────────────
        GameObject bulletObj = Instantiate(bulletPrefab, muzzleTransform.position, Quaternion.LookRotation(fireDirection));
        Bullet bullet = bulletObj.GetComponent<Bullet>();

        bullet.ballisticsData = ballisticsData;
        bullet.initialVelocity = fireDirection * ballisticsData.muzzleVelocity;
        bullet.ambientTemperature = ambientTemperature;
        bullet.altitude = altitude;
        bullet.windVelocity = windVelocity;

        // ── 탄착 이벤트 구독 (TOF 기반 명중음 딜레이 처리) ──────────────
        bullet.OnImpact += HandleBulletImpact;
    }

    /// <summary>
    /// 탄착 콜백: TOF(비행시간)를 기반으로 명중음 딜레이 타이밍을 계산합니다.
    /// 음속 343m/s 기준, 거리 / 343 = 소리 도달 추가 딜레이.
    /// </summary>
    private void HandleBulletImpact(Vector3 impactPoint, Vector3 normal, float timeOfFlight)
    {
        float distanceToImpact = Vector3.Distance(muzzleTransform.position, impactPoint);
        float soundDelay = distanceToImpact / 343f;  // 음속 343 m/s

        Debug.Log($"[BulletSpawner] TOF: {timeOfFlight:F3}s | 거리: {distanceToImpact:F1}m "
                + $"| 명중음 딜레이: {soundDelay:F3}s");

        // 추후 연결: StartCoroutine(PlayImpactSoundDelayed(soundDelay, impactPoint));
    }
}