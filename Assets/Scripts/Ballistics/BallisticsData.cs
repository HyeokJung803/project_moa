using UnityEngine;

/// <summary>
/// 총기/탄환 물리 상수 데이터 컨테이너.
/// Unity 에디터에서 [Create > Ballistics > BallisticsData]로 에셋 생성.
/// </summary>
[CreateAssetMenu(menuName = "Ballistics/BallisticsData")]
public class BallisticsData : ScriptableObject
{
    [Header("탄환 물리 속성")]
    [Tooltip("탄환 질량 (kg). 7.62x51mm NATO 기준 ≈ 0.00994")]
    public float bulletMass = 0.00994f;

    [Tooltip("탄환 단면적 (m²). 7.62mm 구경 기준 ≈ 0.0000456")]
    public float bulletArea = 0.0000456f;

    [Tooltip("항력 계수 Cd. 보트테일 탄 기준 ≈ 0.295")]
    public float dragCoefficient = 0.295f;

    [Header("발사 속도")]
    [Tooltip("초구 속도 (m/s). 7.62 NATO 일반탄 기준 ≈ 850")]
    public float muzzleVelocity = 850f;
}