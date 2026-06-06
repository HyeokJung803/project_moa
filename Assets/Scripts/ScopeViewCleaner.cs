using UnityEngine;

/// <summary>
/// Removes leftover first-person weapon/view blockers while the scope view is being rebuilt.
/// This is intentionally conservative: it only removes named range clutter and mesh renderers parented to the camera.
/// </summary>
public class ScopeViewCleaner : MonoBehaviour
{
    private const float ActiveCleanupSeconds = 8f;

    private float _stopTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        CleanupNow();

        GameObject cleaner = new GameObject("Scope View Cleaner");
        DontDestroyOnLoad(cleaner);
        cleaner.AddComponent<ScopeViewCleaner>();
    }

    private void Awake()
    {
        _stopTime = Time.unscaledTime + ActiveCleanupSeconds;
    }

    private void Update()
    {
        CleanupNow();

        if (Time.unscaledTime >= _stopTime)
        {
            Destroy(gameObject);
        }
    }

    public static void CleanupNow()
    {
        RemoveCameraChildMeshes();
        RemoveNamedClutter();
    }

    private static void RemoveCameraChildMeshes()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Renderer[] renderers = mainCamera.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
            {
                DestroyObject(renderer.gameObject);
            }
        }
    }

    private static void RemoveNamedClutter()
    {
        GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];
            if (obj == null)
            {
                continue;
            }

            string objectName = obj.name;
            if (objectName.Contains("R700") ||
                objectName.Contains("Sniper") ||
                objectName.Contains("M24Proxy") ||
                objectName.Contains("Central Dirt Lane") ||
                objectName.Contains("Range Stake") ||
                objectName.Contains("Firing Lane Marker"))
            {
                DestroyObject(obj);
            }
        }
    }

    private static void DestroyObject(GameObject obj)
    {
        if (obj == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(obj);
        }
        else
        {
            DestroyImmediate(obj);
        }
    }
}
