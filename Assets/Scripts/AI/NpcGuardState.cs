using Character.Config;
using Character.Intent;
using Character.StateMachine;
using Character.StateMachine.States;
using UnityEngine;

namespace AI
{
    public sealed class NpcGuardState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combatConfig;
        private readonly CharacterPresentationConfig _presentationConfig;
        private readonly NpcAiIntentSource _intentSource;

        private float _phaseTimer;
        private float _loopHoldDuration = 2f;
        private float _turnCooldown;
        private float _targetTurnAngle;
        private float _turnDuration;
        private float _turnYawSpeed;
        private Quaternion _turnTargetRotation = Quaternion.identity;

        public GuardState.GuardPhase CurrentPhase { get; private set; } = GuardState.GuardPhase.Start;
        public float GetTargetTurnAngle() => _targetTurnAngle;
        
        public CharacterStateId Id { get; } = CharacterStateId.Guard;
        
        public NpcGuardState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            NpcMotor motor,
            CharacterCombatConfig combat,
            CharacterPresentationConfig presentationConfig,
            NpcAiIntentSource intentSource)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combatConfig = combat;
            _presentationConfig = presentationConfig;
            _intentSource = intentSource;
        }
        
        public void Prepare(float loopHoldDuration = 2f) => _loopHoldDuration = Mathf.Max(0.1f, loopHoldDuration);
        
        
        public void Enter()
        {
            SetPhase(GuardState.GuardPhase.Start);
            _motor?.Stop();
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            switch (CurrentPhase)
            {
                case GuardState.GuardPhase.Start:
                    TickStart(deltaTime);
                    break;
                case GuardState.GuardPhase.Loop:
                    TickLoop(deltaTime);
                    break;
                case GuardState.GuardPhase.Exit:
                    TickExit(deltaTime);
                    break;
                case GuardState.GuardPhase.TurnLeft:
                case GuardState.GuardPhase.TurnRight:
                    TickTurn(deltaTime);
                    break;
            }
        }

        public void Exit()
        {
            _turnCooldown = 0f;
            _turnDuration = 0f;
            _turnYawSpeed = 0f;
            if (_motor != null)
                _turnTargetRotation = _motor.Root.rotation;
            SetPhase(GuardState.GuardPhase.Start);
        }

        private void TickStart(float deltaTime)
        {
            _phaseTimer += deltaTime;
            if(_phaseTimer >= _combatConfig.guardStartDuration)
                SetPhase(GuardState.GuardPhase.Loop);
        }

        private void TickLoop(float deltaTime)
        {
            if (_turnCooldown > 0f)
                _turnCooldown -= deltaTime;

            if (ShouldTurnToTarget(out bool turnLeft, out float angleDelta))
            {
                BeginTurn(turnLeft, angleDelta);
                return;
            }

            _phaseTimer += deltaTime;
            if(_phaseTimer >= _loopHoldDuration)
                SetPhase(GuardState.GuardPhase.Exit);
        }

        private void TickExit(float deltaTime)
        {
            _phaseTimer += deltaTime;
            if(_phaseTimer < _combatConfig.guardExitDuration) return;

            _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }

        private void SetPhase(GuardState.GuardPhase phase)
        {
            CurrentPhase = phase;
            _phaseTimer = 0f;
        }

        private bool ShouldTurnToTarget(out bool turnLeft, out float angleDelta)
        {
            turnLeft = false;
            angleDelta = 0f;

            if (_motor == null || _intentSource == null || _turnCooldown > 0f)
                return false;

            if (!TryGetFacingAngleToTarget(out turnLeft, out angleDelta))
                return false;

            return angleDelta >= GetTurnTriggerAngle();
        }

        private void BeginTurn(bool turnLeft, float angleDelta)
        {
            SetPhase(turnLeft ? GuardState.GuardPhase.TurnLeft : GuardState.GuardPhase.TurnRight);

            _targetTurnAngle = Mathf.Min(angleDelta, GetTurnStepAngle());
            _turnTargetRotation = CalculateStepTargetRotation(turnLeft, _targetTurnAngle);
            _turnDuration = CalculateTurnDuration(_targetTurnAngle);
            _turnYawSpeed = _turnDuration > 0.0001f ? _targetTurnAngle / _turnDuration : 0f;
            _motor.Stop();
        }

        private void TickTurn(float deltaTime)
        {
            if (_motor == null)
            {
                EndTurn();
                return;
            }

            _phaseTimer += deltaTime;
            _motor.Stop();

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
            SetPhase(GuardState.GuardPhase.Loop);
            _turnCooldown = GetTurnCooldown();
            _turnDuration = 0f;
            _turnYawSpeed = 0f;
            _turnTargetRotation = _motor != null ? _motor.Root.rotation : Quaternion.identity;
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
