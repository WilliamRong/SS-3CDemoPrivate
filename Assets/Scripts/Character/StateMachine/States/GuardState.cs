using Character.Config;
using Character.Intent;
using Character.Motor;
using Character.Presentation;
using UnityEngine;
namespace Character.StateMachine.States
{

    public sealed class GuardState : ICharacterState
    {
        public enum GuardPhase : byte
        {
            None = 0,
            Start = 1,
            Loop = 2,
            Exit = 3,
            TurnLeft = 4,
            TurnRight = 5,
        }

        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;
        private readonly CharacterStateRegistry _registry;
        private readonly CharacterCombatConfig _combatConfig;

        private float _phaseTimer;

        public CharacterStateId Id { get; } = CharacterStateId.Guard;
        public GuardPhase CurrentPhase { get; private set; } = GuardPhase.Start;

        private readonly CharacterPresentationConfig _presentationConfig;
        private readonly ILockOnLocomotionQuery _lockOnQuery;

        private float _turnCooldown;
        private float _targetTurnAngle;
        private float _turnDuration;
        private float _turnYawSpeed;
        private Quaternion _turnTargetRotation = Quaternion.identity;

        public float GetTargetTurnAngle() => _targetTurnAngle;
        public GuardState(CharacterStateMachine fsm, CharacterMotor motor, CharacterStateRegistry registry,
            CharacterCombatConfig combatConfig, CharacterPresentationConfig presentationConfig, ILockOnLocomotionQuery lockOnQuery)
        {
            _fsm = fsm;
            _motor = motor;
            _registry = registry;
            _combatConfig = combatConfig;
            _presentationConfig = presentationConfig;
            _lockOnQuery = lockOnQuery;
        }

        public void Enter()
        {
            SetPhase(GuardPhase.Start);
            _motor.SetSprintActive(false);
            _motor.SetMovementBlocked(false);
            _motor.SetTurnRotationOverride(false);
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            intent.IsSprintHeld = false;
            intent.IsJumpPressed = false;

            switch (CurrentPhase)
            {
                case GuardPhase.Start:
                    TickStart(intent, deltaTime);
                    break;
                case GuardPhase.Loop:
                    TickLoop(intent, deltaTime);
                    break;
                case GuardPhase.Exit:
                    TickExit(intent, deltaTime);
                    break;
                case GuardPhase.TurnLeft:
                case GuardPhase.TurnRight:
                    TickTurnPhase(intent, deltaTime);
                    break;

            }
        }

        public void Exit()
        {
            _motor.SetMovementBlocked(false);
            _motor.SetSprintActive(false);
            _motor.SetTurnRotationOverride(false);
            _turnCooldown = 0f;
            _turnDuration = 0f;
            _turnYawSpeed = 0f;
            _turnTargetRotation = _motor.Root.rotation;
            SetPhase(GuardPhase.Start);
        }

        private void TickStart(CharacterIntent intent, float deltaTime)
        {
            intent.IsDodgePressed = false;
            _motor.Tick(intent, deltaTime);

            _phaseTimer += deltaTime;
            if (_phaseTimer >= _combatConfig.guardStartDuration)
            {
                SetPhase(GuardPhase.Loop);
            }
        }

        private void TickLoop(CharacterIntent intent, float deltaTime)
        {
            _motor.SetSprintActive(false);

            if (intent.IsDodgePressed)
            {
                _fsm.TryTransition(CharacterStateId.Dodge, _registry, TransitionReason.InputDodge);
                return;
            }

            if (intent.IsAttackPressed)
            {
                var attackState = _registry.Get(CharacterStateId.Attack) as AttackState;
                attackState?.PrepareHeavyAttack();

                bool transitioned = _fsm.TryTransition(CharacterStateId.Attack, _registry, TransitionReason.InputAttack);
                if (!transitioned)
                    attackState?.PrepareComboAttack();

                return;
            }

            if (!intent.IsGuardHeld)
            {
                _motor.SetTurnRotationOverride(false);
                _motor.Tick(intent, deltaTime);
                SetPhase(GuardPhase.Exit);
                return;
            }

            if (_turnCooldown > 0f)
                _turnCooldown -= deltaTime;

            bool hasMove = intent.Move.sqrMagnitude > 0.0001f;
            if (!hasMove && ShouldTurnToTarget(out bool turnLeft, out float angleDelta))
            {
                BeginTurn(turnLeft, angleDelta);
                _motor.Tick(intent, deltaTime);
                return;
            }

            _motor.SetTurnRotationOverride(!hasMove && IsWaitingForLockOnTurn());
            _motor.Tick(intent, deltaTime);
        }

        private void TickExit(CharacterIntent intent, float deltaTime)
        {
            intent.IsDodgePressed = false;
            _motor.Tick(intent, deltaTime);

            _phaseTimer += deltaTime;
            if (_phaseTimer < _combatConfig.guardExitDuration) return;

            bool hasMove = intent.Move.sqrMagnitude > 0.0001f;
            _fsm.TryTransition(hasMove ? CharacterStateId.Move : CharacterStateId.Idle, _registry,
                TransitionReason.Timeout);
        }

        private void SetPhase(GuardPhase phase)
        {
            CurrentPhase = phase;
            _phaseTimer = 0f;
        }

        private bool ShouldTurnToTarget(out bool turnLeft, out float angleDelta)
        {
            turnLeft = false;
            angleDelta = 0f;

            if (_turnCooldown > 0f)
                return false;

            if (!TryGetFacingAngleToTarget(out turnLeft, out angleDelta, out _))
                return false;

            return angleDelta >= GetTurnTriggerAngle();
        }

        private void BeginTurn(bool turnLeft, float angleDelta)
        {
            SetPhase(turnLeft ? GuardPhase.TurnLeft : GuardPhase.TurnRight);

            _targetTurnAngle = Mathf.Min(angleDelta, GetTurnStepAngle());
            _turnTargetRotation = CalculateStepTargetRotation(turnLeft, _targetTurnAngle);
            _turnDuration = CalculateTurnDuration(_targetTurnAngle);
            _turnYawSpeed = _turnDuration > 0.0001f ? _targetTurnAngle / _turnDuration : 0f;

            _motor.SetTurnRotationOverride(true);
            _motor.SetMovementBlocked(true);
        }

        private void TickTurnPhase(CharacterIntent intent, float deltaTime)
        {
            if (!intent.IsGuardHeld)
            {
                EndTurn();
                SetPhase(GuardPhase.Exit);
                return;
            }

            if (intent.IsDodgePressed)
            {
                EndTurn();
                _fsm.TryTransition(CharacterStateId.Dodge, _registry, TransitionReason.InputDodge);
                return;
            }

            if (intent.IsAttackPressed || intent.Move.sqrMagnitude > 0.0001f)
            {
                EndTurn();
                SetPhase(GuardPhase.Loop);
                return;
            }

            _phaseTimer += deltaTime;
            ApplyTurnRotation(deltaTime);
            _motor.Tick(intent, deltaTime);

            if (IsTurnAligned() || _phaseTimer >= _turnDuration)
                EndTurn();
        }

        private void EndTurn()
        {
            SetPhase(GuardPhase.Loop);
            _turnCooldown = GetTurnCooldown();
            _turnDuration = 0f;
            _turnYawSpeed = 0f;
            _turnTargetRotation = _motor.Root.rotation;

            _motor.SetTurnRotationOverride(false);
            _motor.SetMovementBlocked(false);
        }

        private void ApplyTurnRotation(float deltaTime)
        {
            float maxDegreesDelta = Mathf.Max(_turnYawSpeed, 1f) * deltaTime;
            _motor.Root.rotation = Quaternion.RotateTowards(_motor.Root.rotation, _turnTargetRotation, maxDegreesDelta);
        }

        private bool IsTurnAligned()
        {
            return Quaternion.Angle(_motor.Root.rotation, _turnTargetRotation) <= GetTurnAngleTolerance();
        }

        private float CalculateTurnDuration(float angleDelta)
        {
            float normalizedAngle = Mathf.Clamp01(angleDelta / GetTurnAnimationAngle());
            float speed = Mathf.Max(
                0.01f,
                Mathf.Lerp(GetTurnSpeedMultiplierMax(), GetTurnSpeedMultiplierMin(), normalizedAngle));

            return Mathf.Max(0.01f, GetTurnDuration()) / speed;
        }

        private bool TryGetFacingAngleToTarget(out bool turnLeft, out float angleDelta, out Quaternion targetRotation)
        {
            turnLeft = false;
            angleDelta = 0f;
            targetRotation = _motor.Root.rotation;

            if (_lockOnQuery == null || !_lockOnQuery.IsLockOnActive)
                return false;

            Transform lockTarget = _lockOnQuery.CurrentTarget;
            if (lockTarget == null)
                return false;

            Vector3 toTarget = lockTarget.position - _motor.Root.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.01f)
                return false;

            Vector3 direction = toTarget.normalized;
            float angle = Vector3.SignedAngle(_motor.Root.forward, direction, Vector3.up);
            angleDelta = Mathf.Abs(angle);
            turnLeft = angle < 0f;
            targetRotation = Quaternion.LookRotation(direction);
            return true;
        }

        private Quaternion CalculateStepTargetRotation(bool turnLeft, float stepAngle)
        {
            float signedStep = turnLeft ? -stepAngle : stepAngle;
            Vector3 targetForward = Quaternion.AngleAxis(signedStep, Vector3.up) * _motor.Root.forward;
            targetForward.y = 0f;

            return targetForward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(targetForward.normalized, Vector3.up)
                : _motor.Root.rotation;
        }

        private bool IsWaitingForLockOnTurn()
        {
            return _lockOnQuery != null
                && _lockOnQuery.IsLockOnActive
                && _lockOnQuery.CurrentTarget != null;
        }

        private float GetTurnTriggerAngle() => _presentationConfig != null
            ? Mathf.Max(1f, _presentationConfig.turnTriggerAngle)
            : 20f;

        private float GetTurnStepAngle() => _presentationConfig != null
            ? Mathf.Max(1f, _presentationConfig.turnStepAngle)
            : 180f;

        private float GetTurnCooldown() => _presentationConfig != null
            ? Mathf.Max(0f, _presentationConfig.turnCooldown)
            : 0.2f;

        private float GetTurnAngleTolerance() => _presentationConfig != null
            ? Mathf.Max(0.1f, _presentationConfig.turnAngleTolerance)
            : 3f;

        private float GetTurnAnimationAngle() => _presentationConfig != null
            ? Mathf.Max(1f, _presentationConfig.turnAnimationAngle)
            : 90f;

        private float GetTurnSpeedMultiplierMax() => _presentationConfig != null
            ? Mathf.Max(0.01f, _presentationConfig.turnSpeedMultiplierMax)
            : 1.3f;

        private float GetTurnSpeedMultiplierMin() => _presentationConfig != null
            ? Mathf.Max(0.01f, _presentationConfig.turnSpeedMultiplierMin)
            : 1f;

        private float GetTurnDuration() => _presentationConfig != null
            ? _presentationConfig.idleTurnDuration
            : 0.5f;
    }
}
