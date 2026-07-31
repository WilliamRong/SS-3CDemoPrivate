using Character.Config;
using Character.Core;
using Character.Intent;
using Character.Presentation;
using UnityEngine;

namespace Character.Motor
{
    /// <summary>
    /// 统一处理逻辑位移、根位移、闪避和锁定移动，状态只发意图而不直接改角色 Transform。
    /// </summary>
    public class CharacterMotor
    {
        private readonly CharacterContext _context;
        private readonly CharacterLocomotionConfig _config;

        public Transform Root => _context.Root;

        private Vector3 _currentHorizontalVelocity;
        private Vector3 _horizontalVelocityRef;

        private ILockOnLocomotionQuery _lockOnQuery;
        public bool IsLockOnActive => _lockOnQuery != null && _lockOnQuery.IsLockOnActive;

        private bool _isSprintActive;
        private bool _movementBlocked;
        private bool _turnRotationOverride;
        private bool _isDodgeActive;
        private bool _attackRootMotionActive;
        private bool _reactionRootMotionActive;
        private bool _hasPendingAttackRootMotion;
        private Vector3 _pendingAttackDeltaPosition;
        private float _pendingAttackDeltaYaw;
        private bool _dodgeMoveActive;
        private Vector3 _dodgeWorldDirection;
        private float _dodgeSpeed;
        private float _dodgeMoveTimer;
        private float _dodgeMoveDuration;

        public CharacterMotor(CharacterContext context, CharacterLocomotionConfig config)
        {
            _context = context;
            _config = config;
        }

        // ============ 外部绑定与主循环 ============

        public void SetLockOnQuery(ILockOnLocomotionQuery query)
        {
            _lockOnQuery = query;
        }

        /// <summary>
        /// 位移模式按闪避、根位移、普通移动互斥选择，保证同一帧 CharacterController 只消费一种水平来源。
        /// </summary>
        public void Tick(CharacterIntent intent, float dt)
        {
            if (_isDodgeActive)
            {
                TickDodge(intent, dt);
                return;
            }

            if (_attackRootMotionActive || _reactionRootMotionActive)
            {
                TickRootMotion(intent, dt);
                return;
            }

            if (_movementBlocked)
            {
                StopHorizontalMotion();
                TickVertical(intent, dt);
                _context.Controller.Move(_context.Velocity * dt);
                return;
            }

            TickHorizontal(intent, dt);
            TickVertical(intent, dt);
            _context.Controller.Move(_context.Velocity * dt);
        }

        // ============ 闪避位移 ============

        /// <summary>
        /// 进入时冻结世界方向与移动时长，闪避开始后的相机和摇杆变化不会弯折轨迹。
        /// </summary>
        public void BeginDodge(in DodgePresentationContext ctx, CharacterCombatConfig combat)
        {
            _isDodgeActive = true;
            _isSprintActive = false;
            _dodgeWorldDirection = ctx.WorldMoveDirection;

            // 前翻滚动画沿模型前向播放，因此逻辑位移开始前先对齐朝向。
            if (ctx.Mode == DodgeMode.ForwardAlongMove && ctx.WorldMoveDirection.sqrMagnitude > 0.0001f)
            {
                _context.Root.rotation = Quaternion.LookRotation(ctx.WorldMoveDirection);
            }


            float distance = ctx.Mode == DodgeMode.NeutralBackward
                ? combat.dodgeBackwardMoveDistance
                : combat.dodgeEvadeMoveDistance;
            _dodgeMoveDuration = ctx.MoveDuration > 0.0001f ? ctx.MoveDuration : ctx.Duration;
            _dodgeMoveTimer = 0f;
            _dodgeMoveActive = _dodgeMoveDuration > 0.0001f;
            _dodgeSpeed = _dodgeMoveActive ? distance / _dodgeMoveDuration : 0f;

            StopHorizontalMotion();
        }

        public void EndDodge()
        {
            _isDodgeActive = false;
            _dodgeMoveActive = false;
            _dodgeSpeed = 0f;
            _dodgeMoveTimer = 0f;
            StopHorizontalMotion();
        }

        /// <summary>
        /// 水平速度按逻辑时长推进，垂直速度继续走统一重力链路，避免闪避离地时悬空。
        /// </summary>
        private void TickDodge(CharacterIntent intent, float dt)
        {
            if (_dodgeMoveActive)
            {
                _dodgeMoveTimer += dt;
                if (_dodgeMoveTimer >= _dodgeMoveDuration)
                {
                    _dodgeMoveActive = false;
                    StopHorizontalMotion();
                }
            }

            if (_dodgeMoveActive)
            {
                var horizontal = _dodgeWorldDirection * _dodgeSpeed;
                var v = _context.Velocity;
                v.x = horizontal.x;
                v.z = horizontal.z;
                _context.Velocity = v;
            }

            TickVertical(intent, dt);
            _context.Controller.Move(_context.Velocity * dt);
        }

        // ============ 根位移模式 ============

        public void BeginAttackRootMotion()
        {
            if (_attackRootMotionActive)
                return;

            _reactionRootMotionActive = false;
            _attackRootMotionActive = true;
            _hasPendingAttackRootMotion = false;
            _pendingAttackDeltaPosition = Vector3.zero;
            _pendingAttackDeltaYaw = 0f;
            _isSprintActive = false;
            StopHorizontalMotion();
        }

        public void EndAttackRootMotion()
        {
            _attackRootMotionActive = false;
            _hasPendingAttackRootMotion = false;
            _pendingAttackDeltaPosition = Vector3.zero;
            _pendingAttackDeltaYaw = 0f;
            StopHorizontalMotion();
        }

        public void BeginReactionRootMotion()
        {
            if (_reactionRootMotionActive)
                return;

            _attackRootMotionActive = false;
            _reactionRootMotionActive = true;
            _hasPendingAttackRootMotion = false;
            _pendingAttackDeltaPosition = Vector3.zero;
            _pendingAttackDeltaYaw = 0f;
            _isSprintActive = false;
            StopHorizontalMotion();
        }

        public void EndReactionRootMotion()
        {
            _reactionRootMotionActive = false;
            _hasPendingAttackRootMotion = false;
            _pendingAttackDeltaPosition = Vector3.zero;
            _pendingAttackDeltaYaw = 0f;
            StopHorizontalMotion();
        }

        public void SnapAttackDirection(CharacterIntent intent)
        {
            if (!TryGetInputWorldDirection(intent, out var inputDir))
                return;

            _context.Root.rotation = Quaternion.LookRotation(inputDir);
        }

        public void SetAttackRootMotionDelta(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            // Animator 回调可能在状态 Tick 之后到达，先累积到下一次 Motor Tick 再消费。
            if (!_attackRootMotionActive && !_reactionRootMotionActive)
                return;

            _hasPendingAttackRootMotion = true;
            _pendingAttackDeltaPosition += deltaPosition;
            _pendingAttackDeltaYaw += Mathf.DeltaAngle(0f, deltaRotation.eulerAngles.y);
        }

        /// <summary>
        /// 攻击与反应根位移使用完全相同的 Animator 增量消费路径，只由进入/退出接口区分所有权。
        /// </summary>
        private void TickRootMotion(CharacterIntent intent, float dt)
        {
            Vector3 deltaPosition = _hasPendingAttackRootMotion ? _pendingAttackDeltaPosition : Vector3.zero;
            float deltaYaw = _hasPendingAttackRootMotion ? _pendingAttackDeltaYaw : 0f;

            _hasPendingAttackRootMotion = false;
            _pendingAttackDeltaPosition = Vector3.zero;
            _pendingAttackDeltaYaw = 0f;

            deltaPosition.y = 0f;
            if (Mathf.Abs(deltaYaw) > 0.0001f)
                _context.Root.Rotate(0f, deltaYaw, 0f, Space.World);

            float invDt = dt > 0.0001f ? 1f / dt : 0f;
            var v = _context.Velocity;
            v.x = deltaPosition.x * invDt;
            v.z = deltaPosition.z * invDt;
            _context.Velocity = v;

            TickVertical(intent, dt);
            var move = new Vector3(deltaPosition.x, _context.Velocity.y * dt, deltaPosition.z);
            _context.Controller.Move(move);
        }

        // ============ 输入方向 ============

        private bool TryGetInputWorldDirection(CharacterIntent intent, out Vector3 inputDir)
        {
            inputDir = Vector3.zero;

            if (intent.Move.sqrMagnitude <= 0.0001f)
                return false;

            // 玩家输入使用相机水平基，才能保证背对目标时仍按镜头方向闪避。
            _context.GetCameraBasis(out var camForward, out var camRight);
            inputDir = camRight * intent.Move.x + camForward * intent.Move.y;
            inputDir.y = 0f;

            if (inputDir.sqrMagnitude <= 0.0001f)
                return false;

            inputDir.Normalize();
            return true;
        }

        // ============ 移动开关与水平移动 ============

        public void SetMovementBlocked(bool blocked)
        {
            _movementBlocked = blocked;
            if (blocked)
                StopHorizontalMotion();
        }

        public void SetTurnRotationOverride(bool enable)
        {
            _turnRotationOverride = enable;
        }

        public void StopHorizontalMotion()
        {
            _currentHorizontalVelocity = Vector3.zero;
            _horizontalVelocityRef = Vector3.zero;

            var v = _context.Velocity;
            v.x = 0f;
            v.z = 0f;
            _context.Velocity = v;
        }

        private void TickHorizontal(CharacterIntent intent, float dt)
        {
            Transform lockOnTarget = _lockOnQuery != null && _lockOnQuery.IsLockOnActive
                ? _lockOnQuery.CurrentTarget
                : null;

            if (lockOnTarget != null)
            {
                TickLockOnHorizontal(intent, dt, lockOnTarget);
                return;
            }

            TickFreeHorizontal(intent, dt);
        }

        /// <summary>
        /// 锁定移动保留角色朝向目标并允许相机系侧移，速度方向与模型前向不再强制一致。
        /// </summary>
        private void TickLockOnHorizontal(CharacterIntent intent, float dt, Transform target)
        {
            _context.GetCameraBasis(out var camForward, out var camRight);

            var inputDir = camRight * intent.Move.x + camForward * intent.Move.y;
            inputDir.y = 0f;

            if (inputDir.sqrMagnitude > 0.0001f) inputDir.Normalize();

            var toTarget = target.position - _context.Root.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                if (!_turnRotationOverride)
                {
                    var targetRot = Quaternion.LookRotation(toTarget.normalized);
                    _context.Root.rotation = Quaternion.Slerp(
                        _context.Root.rotation,
                        targetRot,
                        _config.rotationSlerpSpeed * dt);
                }
            }

            var speed = _config.moveSpeed;
            var targetHorizontal = inputDir * speed;

            _currentHorizontalVelocity = Vector3.SmoothDamp(
                _currentHorizontalVelocity,
                targetHorizontal,
                ref _horizontalVelocityRef,
                _config.smoothTime);

            var v = _context.Velocity;
            v.x = _currentHorizontalVelocity.x;
            v.z = _currentHorizontalVelocity.z;
            _context.Velocity = v;
        }

        /// <summary>
        /// 非锁定模式让角色朝实际移动方向旋转，保持前向 BlendTree 与世界速度语义一致。
        /// </summary>
        private void TickFreeHorizontal(CharacterIntent intent, float dt)
        {
            _context.GetCameraBasis(out var camForward, out var camRight);

            var inputDir = camRight * intent.Move.x + camForward * intent.Move.y;
            var hasMoveInput = intent.Move.sqrMagnitude > 0.0001f && inputDir.sqrMagnitude > 0.0001f;

            if (hasMoveInput)
            {
                inputDir.Normalize();
                if (!_turnRotationOverride)
                {
                    var targetRot = Quaternion.LookRotation(inputDir);
                    _context.Root.rotation = Quaternion.Slerp(
                        _context.Root.rotation,
                        targetRot,
                        _config.rotationSlerpSpeed * dt);
                }
            }

            var speed = _isSprintActive ? _config.sprintSpeed : _config.moveSpeed;
            var targetHorizontal = inputDir * speed;

            _currentHorizontalVelocity = Vector3.SmoothDamp(
                _currentHorizontalVelocity,
                targetHorizontal,
                ref _horizontalVelocityRef,
                _config.smoothTime);
            var v = _context.Velocity;
            v.x = _currentHorizontalVelocity.x;
            v.z = _currentHorizontalVelocity.z;
            _context.Velocity = v;
        }

        // ============ 垂直移动 ============

        private void TickVertical(CharacterIntent intent, float dt)
        {
            var v = _context.Velocity;

            if (_context.IsGrounded && v.y < 0f)
                v.y = -2f;

            if (_context.IsGrounded && intent.IsJumpPressed)
                v.y = Mathf.Sqrt(_config.jumpHeight * -2f * _config.gravity);

            v.y += _config.gravity * dt;
            _context.Velocity = v;
        }

        // ============ 状态标志 ============

        public void SetSprintActive(bool active) => _isSprintActive = active;
    }
}
