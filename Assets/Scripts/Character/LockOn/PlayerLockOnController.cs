using System.Collections.Generic;
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
        private ILockOnTarget _currentTarget;

        public bool IsLockOnActive => _currentTarget != null && _currentTarget.CanBeLocked;
        public Transform CurrentTarget => IsLockOnActive ? _currentTarget.LockPoint : null;

        private void Awake()
        {
            _input = GetComponent<InputHandler>();
            _camera = Camera.main;
        }

        private void Update()
        {
            if (_input != null && _input.LockOnTriggered)
                ToggleLockOn();

            if (_input != null && _input.IsSprinting && IsLockOnActive)
                ClearLockOn();

            if (_currentTarget != null && !IsTargetStillValid(_currentTarget))
                ClearLockOn();

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
                if (target?.LockPoint == null) continue;
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, target.LockPoint.position);
                Gizmos.DrawWireSphere(target.LockPoint.position, 0.25f);
            }

            foreach (var target in _debugRejectedCandidates)
            {
                if (target?.LockPoint == null) continue;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, target.LockPoint.position);
            }

            if (IsLockOnActive && CurrentTarget != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, CurrentTarget.position);
                Gizmos.DrawWireSphere(CurrentTarget.position, 0.35f);
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

            if (!IsLockOnActive || _currentTarget == null) return false;

            if (_currentTarget is LockOnTarget lockOnTarget)
                return lockOnTarget.TryGetNetworkId(out lockTargetNetId);

            if (_currentTarget.Root != null)
            {
                var fallback = _currentTarget.Root.GetComponentInParent<LockOnTarget>();
                if (fallback != null) return fallback.TryGetNetworkId(out lockTargetNetId);
            }

            return false;
        }

        private bool TryFindBestTarget(out ILockOnTarget bestTarget)
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
                if (target == null || !IsTargetCandidate(target)) continue;

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
            if (target == null || !target.CanBeLocked || target.Root == transform)
                return false;

            var toTarget = target.LockPoint.position - transform.position;
            if (toTarget.sqrMagnitude > _lockRadius * _lockRadius)
                return false;

            if (_requireCameraForward && _camera != null)
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
            if (target == null || !target.CanBeLocked || target.Root == transform)
                return false;

            var toTarget = target.LockPoint.position - transform.position;
            return toTarget.sqrMagnitude <= _lockRadius * _lockRadius;
        }

        private float ScoreTarget(ILockOnTarget target)
        {
            float distance = Vector3.Distance(transform.position, target.LockPoint.position);
            float screenCenterDistance = 0f;

            if (_camera != null)
            {
                Vector3 viewport = _camera.WorldToViewportPoint(target.LockPoint.position);
                var viewportDelta = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                screenCenterDistance = viewportDelta.magnitude;
            }

            return screenCenterDistance * _screenCenterWeight + distance * _distanceWeight -
                   target.LockPriority * _priorityWeight;
        }
    }
}
