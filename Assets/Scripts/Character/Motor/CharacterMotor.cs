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

        private bool _isSprintActive;
        private bool _movementBlocked;
        private bool _isDodgeActive;
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

        public void Tick(CharacterIntent intent, float dt)
        {
            if (_isDodgeActive)
            {
                TickDodge(intent, dt);
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
