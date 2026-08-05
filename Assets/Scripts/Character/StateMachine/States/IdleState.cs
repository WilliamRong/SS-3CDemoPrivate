using Character.Intent;
using Character.Motor;
using Character.Config;
using Character.LockOn;
using UnityEngine;
using Character.Presentation;

namespace Character.StateMachine.States
{
    /// <summary>
    /// 在无移动输入时仲裁动作转换与锁定转身，避免 Motor 和状态机同时控制角色朝向。
    /// </summary>
    public sealed class IdleState : ICharacterState
    {
        /// <summary>
        /// 用离散阶段同步待机转身过程，使状态逻辑、动画表现和网络快照共享同一语义。
        /// </summary>
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
        public float GetTargetTurnAngle() => _targetTurnAngle;

        public IdleState(CharacterStateMachine fsm, CharacterMotor motor, CharacterStateRegistry registry, CharacterPresentationConfig presentationConfig, ILockOnLocomotionQuery lockOnQuery)
        {
            _fsm = fsm;
            _motor = motor;
            _registry = registry;
            _presentationConfig = presentationConfig;
            _lockOnQuery = lockOnQuery;
        }

        // ============ 状态生命周期 ============

        public void Enter() { }

        /// <summary>
        /// 主动战斗输入优先于自动转身，只有真正无输入时才启动锁定面向修正。
        /// </summary>
        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _motor.SetSprintActive(false);

            if (intent.IsParryPressed)
            {
                _fsm.TryTransition(
                    CharacterStateId.Parry,
                    _registry,
                    TransitionReason.InputParry);
                return;
            }

            if (_turnCooldown > 0f) _turnCooldown -= deltaTime;

            if (_currentPhase == IdlePhase.TurnLeft || _currentPhase == IdlePhase.TurnRight)
            {
                TickTurnPhase(intent, deltaTime);

                if (_currentPhase != IdlePhase.Normal)
                {
                    _motor.Tick(intent, deltaTime);
                    return;
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
        // ============ 锁定转身判定 ============

        private bool ShouldTurnToTarget(out bool turnLeft, out float angleDelta)
        {
            turnLeft = false;
            angleDelta = 0f;

            if (_turnCooldown > 0f) return false;

            if (!TryGetFacingAngleToTarget(out turnLeft, out angleDelta, out _))
                return false;

            return CharacterTurnPlanner.ShouldTurn(_turnCooldown, angleDelta, _presentationConfig);
        }

        private bool IsWaitingForLockOnTurn()
        {
            return _lockOnQuery != null
                && _lockOnQuery.IsLockOnActive
                && _lockOnQuery.CurrentTarget != null;
        }
        // ============ 锁定转身执行 ============

        private void BeginTurn(bool turnLeft, float angleDelta)
        {
            _currentPhase = turnLeft ? IdlePhase.TurnLeft : IdlePhase.TurnRight;

            // 使用实时目标方向作为最终旋转，步进角只控制动画选择与速度。
            TryGetFacingAngleToTarget(out _, out _, out Quaternion exactTargetRotation);

            CharacterTurnPlan plan = CharacterTurnPlanner.BuildPlan(
                _motor.Root, turnLeft, angleDelta, _presentationConfig, exactTargetRotation);
            _targetTurnAngle = plan.StepAngle;
            _turnTargetRotation = plan.TargetRotation;
            _phaseTimer = 0f;
            _turnDuration = plan.Duration;
            _turnYawSpeed = plan.YawSpeed;

            _motor.SetTurnRotationOverride(true);
            _motor.SetMovementBlocked(true);

            if (_presentationConfig != null && _presentationConfig.logIdleTurnPresentation)
                Debug.Log($"[IdleState] BeginTurn phase={_currentPhase} angle={_targetTurnAngle:F1}");
        }


        /// <summary>
        /// 任意输入先结束转身并在同一 Tick 继续处理，避免多出一帧不可响应延迟。
        /// </summary>
        private void TickTurnPhase(CharacterIntent intent, float deltaTime)
        {
            bool hasInput = intent.IsAttackPressed ||
                           intent.IsDodgePressed ||
                           intent.IsGuardHeld ||
                           intent.Move.sqrMagnitude > 0.0001f;

            if (hasInput)
            {
                EndTurn();
            }
            else
            {
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
            _turnCooldown = CharacterTurnPlanner.GetCooldown(_presentationConfig);
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
            return CharacterTurnPlanner.IsAligned(_motor.Root.rotation, _turnTargetRotation, _presentationConfig);
        }

        /// <summary>
        /// 一次计算同时产出逻辑旋转与动画方向，确保本帧转身计划不会因重复读取移动目标而分叉。
        /// </summary>
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

            return CharacterTurnPlanner.TryGetFacingDelta(
                _motor.Root,
                lockTarget.position,
                out turnLeft,
                out angleDelta,
                out targetRotation);
        }
    }
}
