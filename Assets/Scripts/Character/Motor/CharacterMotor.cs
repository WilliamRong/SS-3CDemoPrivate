using Character.Config;
using Character.Core;
using Character.Intent;
using Character.Presentation;
using UnityEngine;

namespace Character.Motor
{
    public class CharacterMotor
    {
        private readonly CharacterContext _context;
        private readonly CharacterLocomotionConfig _config;

        private Vector3 _currentHorizontalVelocity;
        private Vector3 _horizontalVelocityRef;

        private ILockOnLocomotionQuery _lockOnQuery;
        public bool IsLockOnActive => _lockOnQuery != null && _lockOnQuery.IsLockOnActive;

        private bool _isSprintActive;
        private bool _movementBlocked;
        private bool _isDodgeActive;
        private bool _attackRootMotionActive;
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

        public void SetLockOnQuery(ILockOnLocomotionQuery query)
        {
            _lockOnQuery = query;
        }
        
        public void Tick(CharacterIntent intent, float dt)
        {
            if (_isDodgeActive)
            {
                TickDodge(intent, dt);
                return;
            }

            if (_attackRootMotionActive)
            {
                TickAttackRootMotion(intent, dt);
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

        public void BeginDodge(in DodgePresentationContext ctx, CharacterCombatConfig combat)
        {
            _isDodgeActive = true;
            _isSprintActive = false;
            _dodgeWorldDirection = ctx.WorldMoveDirection;

            // 前翻滚动画是前向播放，需要先把角色转向位移方向
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

        public void BeginAttackRootMotion()
        {
            if (_attackRootMotionActive)
                return;

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

        public void SnapAttackDirection(CharacterIntent intent)
        {
            if (!TryGetInputWorldDirection(intent, out var inputDir))
                return;

            _context.Root.rotation = Quaternion.LookRotation(inputDir);
        }

        public void SetAttackRootMotionDelta(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            if (!_attackRootMotionActive)
                return;

            _hasPendingAttackRootMotion = true;
            _pendingAttackDeltaPosition += deltaPosition;
            _pendingAttackDeltaYaw += Mathf.DeltaAngle(0f, deltaRotation.eulerAngles.y);
        }

        private void TickAttackRootMotion(CharacterIntent intent, float dt)
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

        private bool TryGetInputWorldDirection(CharacterIntent intent, out Vector3 inputDir)
        {
            inputDir = Vector3.zero;

            if (intent.Move.sqrMagnitude <= 0.0001f)
                return false;

            //角色自身局部空间
            // Vector3 rootForward = _context.Root.forward;
            // Vector3 rootRight = _context.Root.right;
            // rootForward.y = 0f;
            // rootRight.y = 0f;
            //
            // if (rootForward.sqrMagnitude > 0.0001f)
            //     rootForward.Normalize();
            // if (rootRight.sqrMagnitude > 0.0001f)
            //     rootRight.Normalize();
            //
            // inputDir = rootRight * intent.Move.x + rootForward * intent.Move.y;
            
            //相机空间
            _context.GetCameraBasis(out var camForward, out var camRight);
            inputDir = camRight * intent.Move.x + camForward * intent.Move.y;
            inputDir.y = 0f;

            if (inputDir.sqrMagnitude <= 0.0001f)
                return false;

            inputDir.Normalize();
            return true;
        }

        public void SetMovementBlocked(bool blocked)
        {
            _movementBlocked = blocked;
            if (blocked)
                StopHorizontalMotion();
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
            if (_lockOnQuery != null && _lockOnQuery.IsLockOnActive && _lockOnQuery.CurrentTarget != null)
            {
                TickLockOnHorizontal(intent, dt, _lockOnQuery.CurrentTarget);
                return;
            }
            
            TickFreeHorizontal(intent, dt);
        }

        private void TickLockOnHorizontal(CharacterIntent intent, float dt, Transform target)
        {
            _context.GetCameraBasis(out var camForward, out var camRight);

            var inputDir = camRight * intent.Move.x + camForward * intent.Move.y;
            inputDir.y = 0f;
            
            if(inputDir.sqrMagnitude > 0.0001f) inputDir.Normalize();

            var toTarget = target.position - _context.Root.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                var targetRot = Quaternion.LookRotation(toTarget.normalized);
                _context.Root.rotation = Quaternion.Slerp(
                    _context.Root.rotation,
                    targetRot,
                    _config.rotationSlerpSpeed * dt);
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
        
        private void TickFreeHorizontal(CharacterIntent intent, float dt)
        {
            _context.GetCameraBasis(out var camForward, out var camRight);

            var inputDir = camRight * intent.Move.x + camForward * intent.Move.y;
            var hasMoveInput = intent.Move.sqrMagnitude > 0.0001f && inputDir.sqrMagnitude > 0.0001f;

            if (hasMoveInput)
            {
                inputDir.Normalize();
                var targetRot = Quaternion.LookRotation(inputDir);
                _context.Root.rotation = Quaternion.Slerp(
                    _context.Root.rotation,
                    targetRot,
                    _config.rotationSlerpSpeed * dt);
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

        public void SetSprintActive(bool active) => _isSprintActive = active;
    }
}
