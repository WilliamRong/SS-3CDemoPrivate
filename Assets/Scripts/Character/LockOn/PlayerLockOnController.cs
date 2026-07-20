using System.Collections.Generic;
using Character.Combat;
using Character.Presentation;
using Input;
using UnityEngine;

namespace Character.LockOn
{
    [DisallowMultipleComponent]
    public sealed class PlayerLockOnController : MonoBehaviour, ILockOnLocomotionQuery
    {
        [Header("Search")]
        [SerializeField] private float _lockRadius = 12f;
        [SerializeField] private LayerMask _targetMask = ~0;
        [SerializeField] private bool _requireCameraForward = true;
        [SerializeField] private float _cameraForwardDotMin = -0.1f;
        [SerializeField] private bool _autoSwitchOnTargetDeath = true;
        [SerializeField] private bool _autoSwitchRequiresCameraForward;

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

        private void Awake()
        {
            _input = GetComponent<InputHandler>();
            _camera = Camera.main;
            _ownerActor = GetComponent<CombatActor>();
        }

        private void Update()
        {
            if (IsOwnerDead())
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
        /// 供 LocalSyncPublisher 读取锁定同步状态。MoveInput 由 PlayerController.LastMoveInput 单独提供。
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

        private bool TryFindBestTarget(out ILockOnTarget bestTarget)
        {
            return TryFindBestTarget(null, _requireCameraForward, out bestTarget);
        }

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

        private bool IsTargetCandidate(ILockOnTarget target)
        {
            return IsTargetCandidate(target, _requireCameraForward);
        }

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

            if (requireCameraForward && _camera != null)
            {
                // Only used when acquiring a new target; locked targets stay valid when behind the player.
                var dir = toTarget.normalized;
                float dot = Vector3.Dot(_camera.transform.forward, dir);
                if (dot < _cameraForwardDotMin)
                    return false;
            }

            return true;
        }

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

        private float ScoreTarget(ILockOnTarget target)
        {
            if (!TryGetLockPoint(target, out var lockPoint))
                return float.PositiveInfinity;

            float distance = Vector3.Distance(transform.position, lockPoint.position);
            float screenCenterDistance = 0f;

            if (_camera != null)
            {
                Vector3 viewport = _camera.WorldToViewportPoint(lockPoint.position);
                var viewportDelta = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                screenCenterDistance = viewportDelta.magnitude;
            }

            return screenCenterDistance * _screenCenterWeight + distance * _distanceWeight -
                   target.LockPriority * _priorityWeight;
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
