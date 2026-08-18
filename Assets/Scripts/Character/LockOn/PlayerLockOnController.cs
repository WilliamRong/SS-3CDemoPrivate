using System.Collections.Generic;
using Character.Combat;
using Character.Presentation;
using Character.StateMachine;
using Input;
using UnityEngine;

namespace Character.LockOn
{
    /// <summary>
    /// 以相机视野而非角色朝向筛选锁定目标，使观察方向与玩家意图保持一致，并向移动和同步层提供同一目标状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLockOnController : MonoBehaviour, ILockOnLocomotionQuery
    {
        [Header("Search")]
        [SerializeField] private float _lockRadius = 12f;
        [SerializeField] private LayerMask _targetMask = ~0;
        [SerializeField] private bool _requireCameraForward = true;
        [SerializeField, Range(0f, 1f)] private float _cameraForwardDotMin = 0f;
        [SerializeField] private bool _autoSwitchOnTargetDeath = true;
        [SerializeField] private bool _autoSwitchRequiresCameraForward = true;

        [Header("Score")]
        [SerializeField] private float _screenCenterWeight = 2f;
        [SerializeField] private float _distanceWeight = 0.2f;
        [SerializeField] private float _priorityWeight = 1f;

        [Header("Debug")]
        [SerializeField] private bool _showLockOnDebug;
        [SerializeField] private bool _debugScanEveryFrame = true;

        private readonly Collider[] _hits = new Collider[32];
        private readonly List<ILockOnTarget> _debugValidCandidates = new();
        private readonly List<ILockOnTarget> _debugRejectedCandidates = new();

        private InputHandler _input;
        private Camera _camera;
        private CombatActor _ownerActor;
        private ILockOnTarget _currentTarget;

        public bool IsLockOnActive => IsTargetReferenceAlive(_currentTarget)
            && _currentTarget.CanBeLocked
            && _currentTarget.LockPoint != null;

        public Transform CurrentTarget
        {
            get
            {
                if (!IsTargetReferenceAlive(_currentTarget) || !_currentTarget.CanBeLocked)
                    return null;

                return _currentTarget.LockPoint;
            }
        }

        public ILockOnTarget CurrentLockOnTarget =>
            IsLockOnActive ? _currentTarget : null;

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            _input = GetComponent<InputHandler>();
            _camera = Camera.main;
            _ownerActor = GetComponent<CombatActor>();
        }

        /// <summary>
        /// 死亡和冲刺先解除锁定，再维护目标有效性，避免失效目标在同帧触发自动切换。
        /// </summary>
        private void Update()
        {
            ResolveCamera();

            if (IsOwnerDead())
            {
                ClearLockOn();
                return;
            }

            if (IsExecutionControlLocked())
            {
                ClearLockOn();
                return;
            }

            if (_input != null && _input.LockOnTriggered)
                ToggleLockOn();

            if (_input != null && _input.IsSprinting && IsLockOnActive)
                ClearLockOn();

            if (_currentTarget != null && !IsTargetStillValid(_currentTarget))
                HandleInvalidCurrentTarget();

            if (_showLockOnDebug && _debugScanEveryFrame)
                RefreshDebugScan();


        }


        private bool IsExecutionControlLocked()
        {
            return _ownerActor != null &&
                   _ownerActor.CurrentStateId is
                       CharacterStateId.Executing or
                       CharacterStateId.Executed;
        }

        // ============ 调试可视化 ============

        private void OnDrawGizmos()
        {
            if (!_showLockOnDebug)
                return;

            Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _lockRadius);

            foreach (var target in _debugValidCandidates)
            {
                if (!TryGetLockPoint(target, out var lockPoint)) continue;
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, lockPoint.position);
                Gizmos.DrawWireSphere(lockPoint.position, 0.25f);
            }

            foreach (var target in _debugRejectedCandidates)
            {
                if (!TryGetLockPoint(target, out var lockPoint)) continue;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, lockPoint.position);
            }

            Transform currentTarget = CurrentTarget;
            if (IsLockOnActive && currentTarget != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, currentTarget.position);
                Gizmos.DrawWireSphere(currentTarget.position, 0.35f);
            }
        }

        /// <summary>
        /// 调试扫描复用正式候选规则但单独保存结果，避免 Gizmos 为了可视化改写当前锁定目标。
        /// </summary>
        private void RefreshDebugScan()
        {
            _debugValidCandidates.Clear();
            _debugRejectedCandidates.Clear();

            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                _lockRadius,
                _hits,
                _targetMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit == null) continue;

                var target = hit.GetComponentInParent<ILockOnTarget>();
                if (target == null) continue;

                if (IsTargetCandidate(target))
                    _debugValidCandidates.Add(target);
                else
                    _debugRejectedCandidates.Add(target);
            }
        }

        // ============ 锁定控制 ============

        private void ToggleLockOn()
        {
            if (IsLockOnActive)
            {
                ClearLockOn();
                return;
            }

            if (TryFindBestTarget(out var target))
                _currentTarget = target;
        }

        public void ClearLockOn()
        {
            _currentTarget = null;
        }

        /// <summary>
        /// 只同步 NetworkIdentity，远端自行解析 LockPoint，避免把运行时骨骼 Transform 写进协议。
        /// </summary>
        public bool TryGetSyncState(out uint lockTargetNetId)
        {
            lockTargetNetId = 0;

            if (!IsLockOnActive || !IsTargetReferenceAlive(_currentTarget)) return false;

            if (_currentTarget is LockOnTarget lockOnTarget)
                return lockOnTarget.TryGetNetworkId(out lockTargetNetId);

            Transform root = _currentTarget.Root;
            if (root != null)
            {
                var fallback = root.GetComponentInParent<LockOnTarget>();
                if (fallback != null) return fallback.TryGetNetworkId(out lockTargetNetId);
            }

            return false;
        }

        // ============ 目标搜索与评分 ============

        private bool TryFindBestTarget(out ILockOnTarget bestTarget)
        {
            return TryFindBestTarget(null, _requireCameraForward, out bestTarget);
        }

        /// <summary>
        /// 使用无分配物理查询并在同一批候选中完成过滤与评分，避免锁定按键产生 GC 或跨帧候选漂移。
        /// </summary>
        private bool TryFindBestTarget(
            ILockOnTarget excludedTarget,
            bool requireCameraForward,
            out ILockOnTarget bestTarget)
        {
            bestTarget = null;
            float bestScore = float.PositiveInfinity;

            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                _lockRadius,
                _hits,
                _targetMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit == null) continue;

                var target = hit.GetComponentInParent<ILockOnTarget>();
                if (target == null || ReferenceEquals(target, excludedTarget)) continue;
                if (!IsTargetCandidate(target, requireCameraForward)) continue;

                float score = ScoreTarget(target);
                if (score >= bestScore) continue;

                bestScore = score;
                bestTarget = target;
            }

            if (_showLockOnDebug)
                RefreshDebugScan();

            return bestTarget != null;
        }

        /// <summary>
        /// 初次锁定可要求目标位于相机前方，自动切换则可独立放宽条件，避免角色朝向影响相机选择。
        /// </summary>

        private bool IsTargetCandidate(ILockOnTarget target)
        {
            return IsTargetCandidate(target, _requireCameraForward);
        }

        /// <summary>
        /// 相机前向限制只约束首次选择；距离、存活和可锁定性始终校验，保证自动切换不会选到失效实体。
        /// </summary>
        private bool IsTargetCandidate(ILockOnTarget target, bool requireCameraForward)
        {
            if (!IsTargetReferenceAlive(target))
                return false;

            Transform root = target.Root;
            Transform lockPoint = target.LockPoint;
            if (root == null || lockPoint == null || !target.CanBeLocked || root == transform)
                return false;

            var toTarget = lockPoint.position - transform.position;
            if (toTarget.sqrMagnitude > _lockRadius * _lockRadius)
                return false;

            if (requireCameraForward)
            {
                Camera viewCamera = ResolveCamera();
                if (viewCamera == null)
                    return false;

                Vector3 cameraToTarget = lockPoint.position - viewCamera.transform.position;
                if (cameraToTarget.sqrMagnitude <= 0.0001f)
                    return false;

                float dot = Vector3.Dot(
                    viewCamera.transform.forward,
                    cameraToTarget.normalized);
                if (dot < _cameraForwardDotMin)
                    return false;

                if (viewCamera.WorldToViewportPoint(lockPoint.position).z <= 0f)
                    return false;
            }

            return true;
        }

        // ============ 当前目标维护 ============

        private bool IsTargetStillValid(ILockOnTarget target)
        {
            if (!IsTargetReferenceAlive(target))
                return false;

            Transform root = target.Root;
            Transform lockPoint = target.LockPoint;
            if (root == null || lockPoint == null || !target.CanBeLocked || root == transform)
                return false;

            var toTarget = lockPoint.position - transform.position;
            return toTarget.sqrMagnitude <= _lockRadius * _lockRadius;
        }

        private void HandleInvalidCurrentTarget()
        {
            var previousTarget = _currentTarget;
            ClearLockOn();

            if (!_autoSwitchOnTargetDeath || !IsTargetReferenceAlive(previousTarget) || !previousTarget.IsDead)
                return;

            bool requireCameraForward = _autoSwitchRequiresCameraForward && _requireCameraForward;
            if (TryFindBestTarget(previousTarget, requireCameraForward, out var nextTarget))
            {
                _currentTarget = nextTarget;
            }
        }

        private bool IsOwnerDead()
        {
            return _ownerActor != null && _ownerActor.IsDead;
        }

        // ============ 目标评分 ============

        /// <summary>
        /// 屏幕中心优先于距离，并保留显式优先级修正，使相机所见目标成为主要选择依据。
        /// </summary>
        private float ScoreTarget(ILockOnTarget target)
        {
            if (!TryGetLockPoint(target, out var lockPoint))
                return float.PositiveInfinity;

            float distance = Vector3.Distance(transform.position, lockPoint.position);
            float screenCenterDistance = 0f;

            Camera viewCamera = ResolveCamera();
            if (viewCamera != null)
            {
                Vector3 viewport = viewCamera.WorldToViewportPoint(lockPoint.position);
                var viewportDelta = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                screenCenterDistance = viewportDelta.magnitude;
            }

            return screenCenterDistance * _screenCenterWeight + distance * _distanceWeight -
                   target.LockPriority * _priorityWeight;
        }

        // ============ 引用安全 ============

        private Camera ResolveCamera()
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
                _camera = Camera.main;

            return _camera;
        }

        private static bool IsTargetReferenceAlive(ILockOnTarget target)
        {
            if (target == null)
                return false;

            return target is not UnityEngine.Object unityObject || unityObject != null;
        }

        private static bool TryGetLockPoint(ILockOnTarget target, out Transform lockPoint)
        {
            lockPoint = null;

            if (!IsTargetReferenceAlive(target))
                return false;

            lockPoint = target.LockPoint;
            return lockPoint != null;
        }
    }
}
