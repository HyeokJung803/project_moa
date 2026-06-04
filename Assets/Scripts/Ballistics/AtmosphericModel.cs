using UnityEngine;

/// <summary>
/// 국제표준대기(ISA) 단순화 모델 기반 공기밀도 계산.
/// 케스트렐 측정값(기온, 고도)을 받아 ρ를 반환하는 순수 정적 클래스.
/// </summary>
public static class AtmosphericModel
{
    // 해수면 15°C 표준 공기밀도 (kg/m³)
    private const float RHO_STANDARD = 1.225f;
    // 고도별 밀도 감쇠 지수 (ISA 단순화 상수)
    private const float ALTITUDE_DECAY = 0.0001184f;

    /// <summary>
    /// 현재 환경의 공기밀도를 계산합니다.
    /// </summary>
    /// <param name="temperatureCelsius">케스트렐 측정 기온 (°C)</param>
    /// <param name="altitudeMeters">케스트렐 측정 고도 (m)</param>
    /// <returns>공기밀도 ρ (kg/m³)</returns>
    public static float GetAirDensity(float temperatureCelsius, float altitudeMeters)
    {
        float tempKelvin = temperatureCelsius + 273.15f;

        // ISA 단순화 공식: ρ = ρ₀ × (288.15 / T) × e^(-0.0001184 × h)
        float tempRatio = 288.15f / tempKelvin;
        float altitudeFactor = Mathf.Exp(-ALTITUDE_DECAY * altitudeMeters);

        float density = RHO_STANDARD * tempRatio * altitudeFactor;

        return density;
    }

    /// <summary>
    /// 디버그용: 현재 대기 조건을 로그로 출력합니다.
    /// </summary>
    public static void LogAtmosphericConditions(float temp, float altitude)
    {
        float rho = GetAirDensity(temp, altitude);
        Debug.Log($"[AtmosphericModel] 기온: {temp}°C | 고도: {altitude}m | ρ = {rho:F4} kg/m³");
    }
}