using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scope turret input controller.
/// Mouse wheel adjusts elevation, and Left Shift + wheel adjusts windage.
/// </summary>
public class ScopeTurretController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BulletSpawner bulletSpawner;

    [Header("Turret Settings")]
    [Tooltip("MOA value adjusted by one mouse wheel click.")]
    [SerializeField] private float moaPerClick = 0.25f;

    [Tooltip("Absolute clamp for turret adjustment values.")]
    [SerializeField] private float maxAbsoluteMOA = 80f;

    [Header("Debug")]
    [SerializeField] private bool logChanges = true;

    private float _elevationMOA;
    private float _windageMOA;

    private void Awake()
    {
        if (bulletSpawner == null)
        {
            bulletSpawner = GetComponent<BulletSpawner>();
        }
    }

    private void Start()
    {
        if (bulletSpawner == null)
        {
            Debug.LogWarning("[ScopeTurretController] BulletSpawner reference is missing.");
            enabled = false;
            return;
        }

        _elevationMOA = bulletSpawner.ElevationMOA;
        _windageMOA = bulletSpawner.WindageMOA;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;

        if (mouse == null || keyboard == null)
        {
            return;
        }

        if (keyboard.rKey.wasPressedThisFrame)
        {
            ResetTurrets();
            return;
        }

        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollY, 0f))
        {
            return;
        }

        int clicks = scrollY > 0f ? 1 : -1;
        bool adjustWindage = keyboard.leftShiftKey.isPressed;

        if (adjustWindage)
        {
            AdjustWindage(clicks);
        }
        else
        {
            AdjustElevation(clicks);
        }
    }

    private void AdjustElevation(int clicks)
    {
        _elevationMOA = ClampMOA(_elevationMOA + clicks * moaPerClick);
        bulletSpawner.SetElevationMOA(_elevationMOA);
        LogTurretState("Elevation");
    }

    private void AdjustWindage(int clicks)
    {
        _windageMOA = ClampMOA(_windageMOA + clicks * moaPerClick);
        bulletSpawner.SetWindageMOA(_windageMOA);
        LogTurretState("Windage");
    }

    private void ResetTurrets()
    {
        _elevationMOA = 0f;
        _windageMOA = 0f;
        bulletSpawner.SetScopeAdjustment(_elevationMOA, _windageMOA);
        LogTurretState("Reset");
    }

    private float ClampMOA(float value)
    {
        return Mathf.Clamp(value, -maxAbsoluteMOA, maxAbsoluteMOA);
    }

    private void LogTurretState(string changedTurret)
    {
        if (!logChanges)
        {
            return;
        }

        Debug.Log($"[ScopeTurretController] {changedTurret} | Elevation: {_elevationMOA:+0.00;-0.00;0.00} MOA "
                + $"| Windage: {_windageMOA:+0.00;-0.00;0.00} MOA");
    }
}
