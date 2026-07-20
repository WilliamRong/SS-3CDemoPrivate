using Character.Intent;
using Character.Config;
using Character.StateMachine;
using Character.StateMachine.States;
using UnityEngine;

namespace AI
{
    public sealed class NpcIdleState : ICharacterState
    {

        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcAiIntentSource _intentSource;
        private readonly NpcMotor _motor;
        private readonly CharacterPresentationConfig _presentationConfig;

        private float _turnCooldown;
        private float _phaseTimer;
        private float _targetTurnAngle;
        private float _turnDuration;
        private float _turnYawSpeed;
        private Quaternion _turnTargetRotation = Quaternion.identity;

        public IdleState.IdlePhase CurrentPhase { get; private set; } = IdleState.IdlePhase.Normal;
        public float GetTargetTurnAngle() => _targetTurnAngle;

        private const float MinMoveDistSq = 0.0025f; // 0.05f ^2

        public CharacterStateId Id => CharacterStateId.Idle;

        public NpcIdleState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            NpcAiIntentSource intentSource,
            NpcMotor motor,
            CharacterPresentationConfig presentationConfig)
        {
            _fsm = fsm;
            _registry = registry;
            _intentSource = intentSource;
            _motor = motor;
            _presentationConfig = presentationConfig;
        }

        public void Enter()
        {
            CurrentPhase = IdleState.IdlePhase.Normal;
            _turnCooldown = 0f;
            _phaseTimer = 0f;
            _turnDuration = 0f;
            _turnYawSpeed = 0f;
            if (_motor != null)
                _turnTargetRotation = _motor.Root.rotation;
            _motor?.Stop();
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            if (_intentSource == null || _motor == null) return;

            if (_turnCooldown > 0f)
                _turnCooldown -= deltaTime;

            if (CurrentPhase is IdleState.IdlePhase.TurnLeft or IdleState.IdlePhase.TurnRight)
            {
                TickTurn(deltaTime);
                return;
            }

            if (ShouldMoveToDestination())
            {
                _fsm.TryTransition(CharacterStateId.Move, _registry, TransitionReason.InputMove);
                return;
            }

            if (ShouldTurnToTarget(out bool turnLeft, out float angleDelta))
                BeginTurn(turnLeft, angleDelta);
        }

        public void Exit()
        {
            CurrentPhase = IdleState.IdlePhase.Normal;
            _turnCooldown = 0f;
            _phaseTimer = 0f;
            _turnDuration = 0f;
            _turnYawSpeed = 0f;
            if (_motor != null)
                _turnTargetRotation = _motor.Root.rotation;
        }

        private bool ShouldMoveToDestination()
        {
            if (!_intentSource.TryGetMoveDestination(out var dest))
                return false;

            Vector3 delta = dest - _motor.Root.position;
            delta.y = 0f;
            return delta.sqrMagnitude >= MinMoveDistSq;
        }

        private bool ShouldTurnToTarget(out bool turnLeft, out float angleDelta)
        {
            turnLeft = false;
            angleDelta = 0f;

            if (_turnCooldown > 0f)
                return false;

            if (!TryGetFacingAngleToTarget(out turnLeft, out angleDelta))
                return false;

            return angleDelta >= GetTurnTriggerAngle();
        }

        private void BeginTurn(bool turnLeft, float angleDelta)
        {
            CurrentPhase = turnLeft ? IdleState.IdlePhase.TurnLeft : IdleState.IdlePhase.TurnRight;
            _targetTurnAngle = Mathf.Min(angleDelta, GetTurnStepAngle());
            _turnTargetRotation = CalculateStepTargetRotation(turnLeft, _targetTurnAngle);
            _phaseTimer = 0f;
            _turnDuration = CalculateTurnDuration(_targetTurnAngle);
            _turnYawSpeed = _turnDuration > 0.0001f ? _targetTurnAngle / _turnDuration : 0f;
            _motor.Stop();
        }

        private void TickTurn(float deltaTime)
        {
            _motor.Stop();

            _phaseTimer += deltaTime;
            float maxDegreesDelta = Mathf.Max(_turnYawSpeed, 1f) * deltaTime;
            _motor.RotateTowards(_turnTargetRotation, maxDegreesDelta);

            if (Quaternion.Angle(_motor.Root.rotation, _turnTargetRotation) <= GetTurnAngleTolerance()
                || _phaseTimer >= _turnDuration)
            {
                EndTurn();
            }
        }

        private void EndTurn()
        {
            CurrentPhase = IdleState.IdlePhase.Normal;
            _turnCooldown = GetTurnCooldown();
            _phaseTimer = 0f;
            _turnDuration = 0f;
            _turnYawSpeed = 0f;
            _turnTargetRotation = _motor.Root.rotation;
        }

        private bool TryGetFacingAngleToTarget(out bool turnLeft, out float angleDelta)
        {
            turnLeft = false;
            angleDelta = 0f;

            if (!_intentSource.TryGetFacingTarget(out Vector3 targetPos))
                return false;

            Vector3 toTarget = targetPos - _motor.Root.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f)
                return false;

            Vector3 direction = toTarget.normalized;
            float angle = Vector3.SignedAngle(_motor.Root.forward, direction, Vector3.up);
            angleDelta = Mathf.Abs(angle);
            turnLeft = angle < 0f;
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

        private float CalculateTurnDuration(float angleDelta)
        {
            float normalizedAngle = Mathf.Clamp01(angleDelta / GetTurnAnimationAngle());
            float speed = Mathf.Max(
                0.01f,
                Mathf.Lerp(GetTurnSpeedMultiplierMax(), GetTurnSpeedMultiplierMin(), normalizedAngle));

            return Mathf.Max(0.01f, GetTurnDuration()) / speed;
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
