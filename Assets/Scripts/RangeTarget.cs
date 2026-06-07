using UnityEngine;

/// <summary>
/// Runtime scoring component for simple square range target boards.
/// </summary>
public class RangeTarget : MonoBehaviour
{
    [SerializeField] private float distanceMeters;
    [SerializeField] private float scoringRadius = 0.62f;

    public float DistanceMeters => distanceMeters;

    public void Configure(float distance, float radius)
    {
        distanceMeters = distance;
        scoringRadius = Mathf.Max(0.01f, radius);
    }

    public int GetScore(Vector3 worldPoint)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        float radialDistance = new Vector2(localPoint.x, localPoint.y).magnitude;
        float normalized = radialDistance / Mathf.Max(0.01f, scoringRadius);

        if (normalized <= 0.26f)
        {
            return 10;
        }

        if (normalized <= 0.61f)
        {
            return 8;
        }

        if (normalized <= 1f)
        {
            return 6;
        }

        return 3;
    }

    public Vector2 GetLocalImpactOffset(Vector3 worldPoint)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        return new Vector2(localPoint.x, localPoint.y);
    }
}
