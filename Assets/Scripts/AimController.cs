using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person aim state, weapon pose, camera FOV, and recoil controller.
/// </summary>
public class AimController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform weaponVisual;
    [SerializeField] private Material weaponMaterialOverride;
    [SerializeField] private BulletSpawner bulletSpawner;

    [Header("FOV")]
    [SerializeField] private float hipFov = 60f;
    [SerializeField] private float aimFov = 18f;
    [SerializeField] private float aimSpeed = 12f;

    [Header("Weapon Pose")]
    [SerializeField] private Vector3 hipLocalPosition = new Vector3(0.42f, -0.42f, 0.78f);
    [SerializeField] private Vector3 aimLocalPosition = new Vector3(0f, -0.2f, 0.5f);
    [SerializeField] private Vector3 hipLocalEuler = new Vector3(0f, -90f, 0f);
    [SerializeField] private Vector3 aimLocalEuler = new Vector3(0f, -90f, 0f);
    [SerializeField] private float weaponPoseSpeed = 14f;
    [SerializeField] private float scopedWeaponHideThreshold = 0.02f;

    [Header("Recoil")]
    [SerializeField] private float hipRecoilPitch = 2.4f;
    [SerializeField] private float aimRecoilPitch = 0.9f;
    [SerializeField] private float recoilRecoverySpeed = 9f;
    [SerializeField] private float maxRecoilPitch = 6f;
    [SerializeField] private float maxRecoilYaw = 1.5f;

    [Header("Sway")]
    [SerializeField] private float aimSwayPosition = 0.004f;
    [SerializeField] private float hipSwayPosition = 0.012f;
    [SerializeField] private float swaySpeed = 1.25f;

    public bool IsAiming => _isAiming;
    public float AimProgress => _aimProgress;

    private bool _isAiming;
    private float _aimProgress;
    private float _recoilPitch;
    private float _recoilYaw;
    private Renderer[] _weaponRenderers;
    private Vector3 _weaponBoundsCenterLocal;
    private bool _hasWeaponBoundsCenter;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (bulletSpawner == null)
        {
            bulletSpawner = FindAnyObjectByType<BulletSpawner>();
        }

        if (weaponVisual != null)
        {
            _weaponRenderers = weaponVisual.GetComponentsInChildren<Renderer>(true);
            ApplyWeaponMaterialOverride();
            CacheWeaponBoundsCenter();
        }
    }

    private void OnEnable()
    {
        if (bulletSpawner != null)
        {
            bulletSpawner.OnFired += HandleFired;
        }
    }

    private void OnDisable()
    {
        if (bulletSpawner != null)
        {
            bulletSpawner.OnFired -= HandleFired;
        }
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        _isAiming = mouse != null && mouse.rightButton.isPressed;

        float target = _isAiming ? 1f : 0f;
        _aimProgress = Mathf.MoveTowards(_aimProgress, target, aimSpeed * Time.deltaTime);

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = Mathf.Lerp(hipFov, aimFov, Smooth01(_aimProgress));
        }
    }

    private void LateUpdate()
    {
        ApplyWeaponPose();
        ApplyScopedWeaponVisibility();
        ApplyRecoilToCamera();
        RecoverRecoil();
    }

    public void AddRecoil(float pitchKick, float yawKick)
    {
        _recoilPitch = Mathf.Clamp(_recoilPitch + pitchKick, 0f, maxRecoilPitch);
        _recoilYaw = Mathf.Clamp(_recoilYaw + yawKick, -maxRecoilYaw, maxRecoilYaw);
    }

    private void HandleFired()
    {
        float pitchKick = Mathf.Lerp(hipRecoilPitch, aimRecoilPitch, _aimProgress);
        float yawKick = Random.Range(-0.35f, 0.35f) * Mathf.Lerp(1f, 0.35f, _aimProgress);
        AddRecoil(pitchKick, yawKick);
    }

    private void ApplyWeaponPose()
    {
        if (weaponVisual == null)
        {
            return;
        }

        float t = Smooth01(_aimProgress);
        float swayAmount = Mathf.Lerp(hipSwayPosition, aimSwayPosition, t);
        Vector3 sway = new Vector3(
            Mathf.Sin(Time.time * swaySpeed * 1.3f) * swayAmount,
            Mathf.Sin(Time.time * swaySpeed) * swayAmount,
            0f);

        Quaternion targetRotation = Quaternion.Euler(Vector3.Lerp(hipLocalEuler, aimLocalEuler, t));
        Vector3 targetCenterPosition = Vector3.Lerp(hipLocalPosition, aimLocalPosition, t) + sway;
        Vector3 targetRootPosition = GetRootPositionForBoundsCenter(targetCenterPosition, targetRotation);

        weaponVisual.localPosition = Vector3.Lerp(
            weaponVisual.localPosition,
            targetRootPosition,
            1f - Mathf.Exp(-weaponPoseSpeed * Time.deltaTime));

        weaponVisual.localRotation = Quaternion.Slerp(
            weaponVisual.localRotation,
            targetRotation,
            1f - Mathf.Exp(-weaponPoseSpeed * Time.deltaTime));
    }

    private void CacheWeaponBoundsCenter()
    {
        if (_weaponRenderers == null || _weaponRenderers.Length == 0)
        {
            return;
        }

        bool hasBounds = false;
        Bounds combinedBounds = default;

        for (int i = 0; i < _weaponRenderers.Length; i++)
        {
            Renderer weaponRenderer = _weaponRenderers[i];
            if (weaponRenderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = weaponRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(weaponRenderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return;
        }

        _weaponBoundsCenterLocal = weaponVisual.InverseTransformPoint(combinedBounds.center);
        _hasWeaponBoundsCenter = true;
    }

    private Vector3 GetRootPositionForBoundsCenter(Vector3 targetCenterPosition, Quaternion targetRotation)
    {
        if (!_hasWeaponBoundsCenter)
        {
            return targetCenterPosition;
        }

        Vector3 scaledCenterOffset = Vector3.Scale(_weaponBoundsCenterLocal, weaponVisual.localScale);
        return targetCenterPosition - targetRotation * scaledCenterOffset;
    }

    private void ApplyScopedWeaponVisibility()
    {
        if (_weaponRenderers == null)
        {
            return;
        }

        bool showWeapon = !_isAiming && _aimProgress < scopedWeaponHideThreshold;

        if (weaponVisual != null && weaponVisual.gameObject.activeSelf != showWeapon)
        {
            weaponVisual.gameObject.SetActive(showWeapon);
        }

        for (int i = 0; i < _weaponRenderers.Length; i++)
        {
            if (_weaponRenderers[i] != null)
            {
                _weaponRenderers[i].enabled = showWeapon;
            }
        }
    }

    private void ApplyWeaponMaterialOverride()
    {
        if (_weaponRenderers == null || weaponMaterialOverride == null)
        {
            return;
        }

        for (int i = 0; i < _weaponRenderers.Length; i++)
        {
            if (_weaponRenderers[i] == null)
            {
                continue;
            }

            Material[] materials = _weaponRenderers[i].sharedMaterials;
            for (int j = 0; j < materials.Length; j++)
            {
                materials[j] = weaponMaterialOverride;
            }

            _weaponRenderers[i].sharedMaterials = materials;
        }
    }

    private void ApplyRecoilToCamera()
    {
        transform.localRotation *= Quaternion.Euler(-_recoilPitch, _recoilYaw, 0f);
    }

    private void RecoverRecoil()
    {
        float recovery = recoilRecoverySpeed * Time.deltaTime;
        _recoilPitch = Mathf.MoveTowards(_recoilPitch, 0f, recovery);
        _recoilYaw = Mathf.MoveTowards(_recoilYaw, 0f, recovery * 0.45f);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
