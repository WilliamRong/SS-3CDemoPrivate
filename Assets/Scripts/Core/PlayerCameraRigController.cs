using Character.Presentation;
using Cinemachine;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 在不复制相机配置的前提下切换 FreeLook 与锁定 Rig，并集中维护运行时辅助目标的生命周期。
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

        // ============ 初始化与管线构建 ============

        /// <summary>
        /// 由本地玩家绑定器显式传入跟随点，避免控制器通过全局搜索绑定到远端玩家。
        /// </summary>
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
            Transform initialTarget = _wasLockOnActive ? _lockOnQuery.CurrentTarget : null;
            if (_wasLockOnActive && initialTarget != null)
                ApplyLockOnState(initialTarget, config);
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

        /// <summary>
        /// 锁定管线允许优先复用场景对象，缺失时再运行时创建，以兼容现有场景同时避免重复 Rig。
        /// </summary>
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

        /// <summary>
        /// 辅助目标脱离玩家层级，防止玩家旋转再次叠加到相机瞄准方向。
        /// </summary>
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

        // ============ Unity 生命周期 ============

        private void OnDestroy()
        {
            // 辅助对象使用 HideAndDontSave，必须由所有者主动销毁，避免退出场景后残留。
            if (_lockPivot != null)
                Destroy(_lockPivot.gameObject);
            if (_lockTargetGroup != null)
                Destroy(_lockTargetGroup.gameObject);
        }

        /// <summary>
        /// 跟随目标在角色和锁定状态更新后再刷新，保证 Cinemachine 本帧读取最终位置。
        /// </summary>
        private void LateUpdate()
        {
            if (!_initialized || _lockOnQuery == null)
                return;

            var config = ResolveConfig();
            if (config == null)
                return;

            bool active = _lockOnQuery.IsLockOnActive;
            Transform target = active ? _lockOnQuery.CurrentTarget : null;
            active = active && target != null;

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

        // ============ 锁定模式切换 ============

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

        // ============ 锁定轴向 ============

        /// <summary>
        /// Pivot 只跟随位置并按水平目标方向旋转，使相机可锁定角色背后的相机可见目标。
        /// </summary>
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

        // ============ 目标组取景 ============

        /// <summary>
        /// 将玩家与目标同时放入 TargetGroup，由 Composer 统一取景，避免手工推导屏幕边界。
        /// </summary>
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

        // ============ FreeLook 输入切换 ============

        /// <summary>
        /// 保存而不是写死恢复速度，确保退出锁定后回到场景原有的输入调参。
        /// </summary>
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

        // ============ 场景对象查询 ============

        /// <summary>
        /// 逐段查找包含未激活节点的场景路径，弥补 GameObject.Find 无法发现禁用相机 Rig 的限制。
        /// </summary>
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
