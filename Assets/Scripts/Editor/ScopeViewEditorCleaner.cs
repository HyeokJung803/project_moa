using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ScopeViewEditorCleaner
{
    private static double _nextCleanupTime;
    private static int _remainingCleanupPasses = 120;

    static ScopeViewEditorCleaner()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now < _nextCleanupTime)
        {
            return;
        }

        _nextCleanupTime = now + 0.1d;
        _remainingCleanupPasses--;
        CleanupNow();

        if (_remainingCleanupPasses <= 0)
        {
            EditorApplication.update -= Tick;
        }
    }

    private static void CleanupNow()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Renderer[] renderers = mainCamera.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                {
                    Object.Destroy(renderer.gameObject);
                }
            }
        }

        GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
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
                Object.Destroy(obj);
            }
        }
    }
}
