using Character.Presentation;
using Cinemachine;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Drives FreeLook / LockOn Cinemachine rigs for the local player.
    /// All tunable values live in <see cref="PlayerCameraRigConfig"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCameraRigController : MonoBehaviour
    {
        [SerializeField] private PlayerCameraRigConfig _config;

        private CinemachineFreeLook _freeLook;
        private CinemachineVirtualCamera _lockOnVcam;
        private CinemachineTransposer _lockTransposer;
        private CinemachineGroupComposer _lockGroupComposer;
        private CinemachineTargetGroup _lockTargetGroup;
        private Transform _lockPivot;
        private ILockOnLocomotionQuery _lockOnQuery;
        private Transform _playerFollowTp;
        private Transform _playerFollowLockOn;
        private Transform _playerLookAt;

        private Quaternion _pivotYaw = Quaternion.identity;
        private bool _pivotYawInitialized;
        private float _savedXAxisMaxSpeed;
        private float _savedYAxisMaxSpeed;
        private bool _freeLookInputSaved;
        private bool _wasLockOnActive;
        private bool _initialized;

        public bool IsInitialized => _initialized;

        public bool TryInitialize(Transform playerFollowTp, Transform playerFollowLockOn, Transform playerLookAt)
        {
            if (playerFollowTp == null && playerFollowLockOn == null && playerLookAt == null)
                return false;

            var config = ResolveConfig();
            if (config == null)
            {
                Debug.LogWarning("[PlayerCameraRigController] Missing PlayerCameraRigConfig.");
                return false;
            }

            _playerFollowTp = playerFollowTp ?? playerFollowLockOn ?? playerLookAt;
            _playerFollowLockOn = playerFollowLockOn ?? playerFollowTp ?? playerLookAt;
            _playerLookAt = playerLookAt ?? _playerFollowTp;
            _lockOnQuery = GetComponent<ILockOnLocomotionQuery>();

            var freeLookGo = FindSceneObject(config.freeLookPath);
            var lockOnGo = FindSceneObject(config.lockOnPath);
            if (freeLookGo == null || lockOnGo == null)
                return false;

            _freeLook = freeLookGo.GetComponent<CinemachineFreeLook>();
            _lockOnVcam = lockOnGo.GetComponent<CinemachineVirtualCamera>();
            if (_freeLook == null || _lockOnVcam == null)
                return false;

            if (!EnsureLockOnPipeline())
                return false;

            EnsureLockHelpers();
            ApplyConfig(config);

            _freeLook.Follow = _playerFollowTp;
            _freeLook.LookAt = _playerLookAt;
            _freeLook.Priority = config.freeLookPriority;

            _lockOnVcam.Follow = _lockPivot;
            _lockOnVcam.LookAt = _lockTargetGroup.transform;
            _lockOnVcam.Priority = config.lockOnInactivePriority;

            _wasLockOnActive = _lockOnQuery != null && _lockOnQuery.IsLockOnActive;
            if (_wasLockOnActive && _lockOnQuery.CurrentTarget != null)
                ApplyLockOnState(_lockOnQuery.CurrentTarget, config);
            else
                ApplyLockOffState(config);

            _initialized = true;
            return true;
        }

        private PlayerCameraRigConfig ResolveConfig()
        {
            if (_config != null)
                return _config;

            return GameDataManager.Instance != null
                ? GameDataManager.Instance.PlayerCameraRig
                : null;
        }

        private void ApplyConfig(PlayerCameraRigConfig config)
        {
            config.ApplyToTransposer(_lockTransposer);
            config.ApplyToGroupComposer(_lockGroupComposer);
        }

        private bool EnsureLockOnPipeline()
        {
            var owner = _lockOnVcam.GetComponentOwner();
            if (owner == null)
                return false;

            foreach (var c in owner.GetComponents<CinemachineComponentBase>())
            {
                if (c is CinemachineTransposer || c is CinemachineGroupComposer)
                    continue;
                Destroy(c);
            }

            _lockTransposer = owner.GetComponent<CinemachineTransposer>();
            if (_lockTransposer == null)
                _lockTransposer = owner.gameObject.AddComponent<CinemachineTransposer>();

            _lockGroupComposer = owner.GetComponent<CinemachineGroupComposer>();
            if (_lockGroupComposer == null)
                _lockGroupComposer = owner.gameObject.AddComponent<CinemachineGroupComposer>();

            return true;
        }

        private void EnsureLockHelpers()
        {
            if (_lockPivot == null)
            {
                var pivotGo = new GameObject("LockOnPivot");
                pivotGo.hideFlags = HideFlags.HideAndDontSave;
                _lockPivot = pivotGo.transform;
            }

            if (_lockTargetGroup == null)
            {
                var groupGo = new GameObject("LockOnTargetGroup");
                groupGo.hideFlags = HideFlags.HideAndDontSave;
                _lockTargetGroup = groupGo.AddComponent<CinemachineTargetGroup>();
                _lockTargetGroup.m_PositionMode = CinemachineTargetGroup.PositionMode.GroupCenter;
                _lockTargetGroup.m_RotationMode = CinemachineTargetGroup.RotationMode.Manual;
                _lockTargetGroup.m_UpdateMethod = CinemachineTargetGroup.UpdateMethod.LateUpdate;
            }
        }

        private void OnDestroy()
        {
            if (_lockPivot != null)
                Destroy(_lockPivot.gameObject);
            if (_lockTargetGroup != null)
                Destroy(_lockTargetGroup.gameObject);
        }

        private void LateUpdate()
        {
            if (!_initialized || _lockOnQuery == null)
                return;

            var config = ResolveConfig();
            if (config == null)
                return;

            bool active = _lockOnQuery.IsLockOnActive;
            Transform target = _lockOnQuery.CurrentTarget;

            if (active != _wasLockOnActive)
            {
                if (active && target != null)
                    ApplyLockOnState(target, config);
                else
                    ApplyLockOffState(config);

                _wasLockOnActive = active;
            }

            if (active && target != null)
            {
                UpdateLockTargetGroup(target, config);
                UpdateLockPivot(target, config);
            }
        }

        private void ApplyLockOnState(Transform target, PlayerCameraRigConfig config)
        {
            ApplyConfig(config);
            CapturePivotYaw(target);
            UpdateLockTargetGroup(target, config);
            UpdateLockPivot(target, config);
            SetFreeLookInputEnabled(false);
            _lockOnVcam.Priority = config.lockOnActivePriority;
        }

        private void ApplyLockOffState(PlayerCameraRigConfig config)
        {
            ClearLockTargetGroup();
            _lockOnVcam.Priority = config.lockOnInactivePriority;
            SetFreeLookInputEnabled(true);
        }

        private void UpdateLockPivot(Transform target, PlayerCameraRigConfig config)
        {
            if (_lockPivot == null || _playerFollowLockOn == null)
                return;

            _lockPivot.position = _playerFollowLockOn.position;

            var desiredYaw = ComputePivotYaw(target);
            if (config.pivotYawDamping <= 0f)
                _pivotYaw = desiredYaw;
            else
                _pivotYaw = Quaternion.Slerp(
                    _pivotYaw,
                    desiredYaw,
                    1f - Mathf.Exp(-Time.deltaTime / config.pivotYawDamping));

            _lockPivot.rotation = _pivotYaw;
        }

        private void CapturePivotYaw(Transform target)
        {
            _pivotYaw = ComputePivotYaw(target);
            _pivotYawInitialized = true;
        }

        private Quaternion ComputePivotYaw(Transform target)
        {
            var toTarget = target.position - _playerFollowLockOn.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= 0.0001f)
                return _pivotYawInitialized ? _pivotYaw : Quaternion.identity;

            return Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        }

        private void UpdateLockTargetGroup(Transform target, PlayerCameraRigConfig config)
        {
            if (_lockTargetGroup == null || target == null)
                return;

            _lockTargetGroup.m_Targets = new CinemachineTargetGroup.Target[]
            {
                new CinemachineTargetGroup.Target
                {
                    target = _playerFollowLockOn,
                    weight = config.playerFramingWeight,
                    radius = config.playerFramingRadius
                },
                new CinemachineTargetGroup.Target
                {
                    target = target,
                    weight = config.targetFramingWeight,
                    radius = config.targetFramingRadius
                }
            };
        }

        private void ClearLockTargetGroup()
        {
            if (_lockTargetGroup == null)
                return;

            _lockTargetGroup.m_Targets = System.Array.Empty<CinemachineTargetGroup.Target>();
        }

        private void SetFreeLookInputEnabled(bool enabled)
        {
            if (_freeLook == null)
                return;

            if (!enabled)
            {
                if (!_freeLookInputSaved)
                {
                    _savedXAxisMaxSpeed = _freeLook.m_XAxis.m_MaxSpeed;
                    _savedYAxisMaxSpeed = _freeLook.m_YAxis.m_MaxSpeed;
                    _freeLookInputSaved = true;
                }

                _freeLook.m_XAxis.m_MaxSpeed = 0f;
                _freeLook.m_YAxis.m_MaxSpeed = 0f;
                return;
            }

            if (_freeLookInputSaved)
            {
                _freeLook.m_XAxis.m_MaxSpeed = _savedXAxisMaxSpeed;
                _freeLook.m_YAxis.m_MaxSpeed = _savedYAxisMaxSpeed;
            }
        }

        private static GameObject FindSceneObject(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var segments = path.Split('/');
            if (segments.Length == 0)
                return null;

            GameObject current = null;
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == segments[0])
                {
                    current = roots[i];
                    break;
                }
            }

            if (current == null)
                return null;

            for (int i = 1; i < segments.Length; i++)
            {
                var child = current.transform.Find(segments[i]);
                if (child == null)
                    return null;

                current = child.gameObject;
            }

            return current;
        }
    }
}
