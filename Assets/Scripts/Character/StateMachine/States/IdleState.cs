using Character.Intent;
using Character.Motor;
using Character.Config;
using Character.LockOn;
using UnityEngine;
using Character.Presentation;

namespace Character.StateMachine.States
{
    public sealed class IdleState : ICharacterState
    {

        public enum IdlePhase : byte
        {
            None = 0,
            Normal = 1,
            TurnLeft = 2,
            TurnRight = 3,
        }

        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;
        private readonly CharacterStateRegistry _registry;
        private readonly CharacterPresentationConfig _presentationConfig;
        private readonly ILockOnLocomotionQuery _lockOnQuery;

        private IdlePhase _currentPhase = IdlePhase.None;
        private float _phaseTimer = 0f;
        private float _turnCooldown = 0f;
        private float _targetTurnAngle = 0f;
        private float _turnDuration = 0f;
        private float _turnYawSpeed = 0f;
        private Quaternion _turnTargetRotation = Quaternion.identity;

        public IdlePhase CurrentPhase => _currentPhase;

        public CharacterStateId Id => CharacterStateId.Idle;

        public IdleState(CharacterStateMachine fsm, CharacterMotor motor, CharacterStateRegistry registry, CharacterPresentationConfig presentationConfig, ILockOnLocomotionQuery lockOnQuery)
        {
            _fsm = fsm;
            _motor = motor;
            _registry = registry;
            _presentationConfig = presentationConfig;
            _lockOnQuery = lockOnQuery;
        }

        public void Enter() { }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _motor.SetSprintActive(false);

            if (_turnCooldown > 0f) _turnCooldown -= deltaTime;

            if (_currentPhase == IdlePhase.TurnLeft || _currentPhase == IdlePhase.TurnRight)
            {
                TickTurnPhase(intent, deltaTime);

                //如果转身被输入打断，继续处理输入
                if(_currentPhase != IdlePhase.Normal)
                {
                    _motor.Tick(intent, deltaTime);
                    return;//转身未被打断，直接return
                }

            }

            if (intent.IsDodgePressed)
            {
                _fsm.TryTransition(CharacterStateId.Dodge, _registry, TransitionReason.InputDodge);
                return;
            }

            if (intent.IsGuardHeld)
            {
                _fsm.TryTransition(CharacterStateId.Guard, _registry, TransitionReason.InputGuard);
                return;
            }

            if (intent.IsAttackPressed)
            {
                _fsm.TryTransition(CharacterStateId.Attack, _registry, TransitionReason.InputAttack);
                return;
            }

            bool hasMove = intent.Move.sqrMagnitude > 0.0001f;
            if (!hasMove)
            {
                if (ShouldTurnToTarget(out bool turnleft, out float angleDelta))
                {
                    BeginTurn(turnleft, angleDelta);
                    _motor.Tick(intent, deltaTime);
                    return;
                }

                _motor.SetTurnRotationOverride(IsWaitingForLockOnTurn());
                _motor.Tick(intent, deltaTime);
                return;
            }

            _motor.SetTurnRotationOverride(false);
            _motor.Tick(intent, deltaTime);

            if (intent.IsSprintHeld)
            {
                _fsm.TryTransition(CharacterStateId.Sprint, _registry, TransitionReason.InputSprint);
                return;
            }

            _fsm.TryTransition(CharacterStateId.Move, _registry, TransitionReason.InputMove);
        }

        public void Exit()
        {
            _currentPhase = IdlePhase.Normal;
            _phaseTimer = 0f;
            _turnDuration = 0f;
            _turnYawSpeed = 0f;
            _turnTargetRotation = _motor.Root.rotation;

            _motor.SetTurnRotationOverride(false);
            _motor.SetMovementBlocked(false);
        }


        private bool ShouldTurnToTarget(out bool turnLeft, out float angleDelta)
        {
            turnLeft = false;
            angleDelta = 0f;

            //2.必须不在冷却
            if (_turnCooldown > 0f) return false;

            //3.计算角度差
            if (!TryGetFacingAngleToTarget(out turnLeft, out angleDelta, out _))
                return false;

            //4.角度差超过阈值
            return angleDelta >= GetTurnTriggerAngle();
        }

        private bool IsWaitingForLockOnTurn()
        {
            return _lockOnQuery != null
                && _lockOnQuery.IsLockOnActive
                && _lockOnQuery.CurrentTarget != null;
        }


        private void BeginTurn(bool turnLeft, float angleDelta)
        {
            _currentPhase = turnLeft ? IdlePhase.TurnLeft : IdlePhase.TurnRight;
            _targetTurnAngle = Mathf.Min(angleDelta, GetTurnStepAngle());
            _turnTargetRotation = CalculateStepTargetRotation(turnLeft, _targetTurnAngle);
            _phaseTimer = 0f;
            _turnDuration = CalculateTurnDuration(_targetTurnAngle);
            _turnYawSpeed = _turnDuration > 0.0001f
                ? _targetTurnAngle / _turnDuration
                : 0f;

            _motor.SetTurnRotationOverride(true);
            _motor.SetMovementBlocked(true);

            if (_presentationConfig != null && _presentationConfig.logIdleTurnPresentation)
                Debug.Log($"[IdleState] BeginTurn phase={_currentPhase} angle={_targetTurnAngle:F1}");
        }


        private void TickTurnPhase(CharacterIntent intent, float deltaTime)
        {
            // 检查是否有任何输入打断转身
            bool hasInput = intent.IsAttackPressed ||
                           intent.IsDodgePressed ||
                           intent.IsGuardHeld ||
                           intent.Move.sqrMagnitude > 0.0001f;

            if (hasInput)
            {
                EndTurn();
                // 不return，让Tick的后续逻辑处理输入
            }
            else
            {
                // 没有输入，继续转身
                _phaseTimer += deltaTime;
                ApplyTurnRotation(deltaTime);

                if (IsTurnAligned() || _phaseTimer >= _turnDuration)
                {
                    EndTurn();
                }
            }
        }

        private void EndTurn()
        {
            _currentPhase = IdlePhase.Normal;
            _phaseTimer = 0f;
            _turnDuration = 0f;
            _turnYawSpeed = 0f;
            _turnCooldown = GetTurnCooldown();
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
            //根据角度计算播放速度
            float normalizedAngle = Mathf.Clamp01(angleDelta / GetTurnAnimationAngle());

            //小角度快播，大角度慢播
            float speed = Mathf.Max(
                0.01f,
                Mathf.Lerp(GetTurnSpeedMultiplierMax(), GetTurnSpeedMultiplierMin(), normalizedAngle));

            return Mathf.Max(0.01f, GetIdleTurnDuration()) / speed;
        }

        public float GetTargetTurnAngle() => _targetTurnAngle;

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
            : 0.7f;

        private float GetIdleTurnDuration() => _presentationConfig != null
            ? _presentationConfig.idleTurnDuration
            : 0.5f;
    }
}
