using Character.Config;
using Character.Intent;
using Character.Motor;
using Character.Presentation;
using UnityEngine;
namespace Character.StateMachine.States
{
    /// <summary>
    /// 将防御起手、持续、退出和锁定转身收敛在同一状态，避免防御判定与表现相位脱节。
    /// </summary>
    public sealed class GuardState : ICharacterState
    {
        /// <summary>
        /// 显式保留防御阶段，便于远端表现还原起手、循环、退出与转身，而不是依赖本地计时猜测。
        /// </summary>
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
        private readonly CharacterPresentationConfig _presentationConfig;
        private readonly ILockOnLocomotionQuery _lockOnQuery;

        private float _phaseTimer;
        private float _turnCooldown;
        private float _targetTurnAngle;
        private float _turnDuration;
        private float _turnYawSpeed;
        private Quaternion _turnTargetRotation = Quaternion.identity;

        public CharacterStateId Id { get; } = CharacterStateId.Guard;
        public GuardPhase CurrentPhase { get; private set; } = GuardPhase.Start;
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

        // ============ 状态生命周期 ============

        public void Enter()
        {
            SetPhase(GuardPhase.Start);
            _motor.SetSprintActive(false);
            _motor.SetMovementBlocked(false);
            _motor.SetTurnRotationOverride(false);
        }

        /// <summary>
        /// 每个阶段独立推进但共享一个入口，输入释放、移动和锁定转身不会在同帧产生多个转换。
        /// </summary>
        public void Tick(CharacterIntent intent, float deltaTime)
        {
            intent.IsSprintHeld = false;
            intent.IsJumpPressed = false;

            if (intent.IsParryPressed)
            {
                _fsm.TryTransition(
                    CharacterStateId.Parry,
                    _registry,
                    TransitionReason.InputParry);
                return;
            }

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

        // ============ 防御阶段推进 ============

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

        /// <summary>
        /// Loop 内先处理释放与攻击，再决定移动或转身，保证主动输入优先于自动面向修正。
        /// </summary>
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

        // ============ 锁定转身 ============

        private bool ShouldTurnToTarget(out bool turnLeft, out float angleDelta)
        {
            turnLeft = false;
            angleDelta = 0f;

            if (_turnCooldown > 0f)
                return false;

            if (!TryGetFacingAngleToTarget(out turnLeft, out angleDelta, out _))
                return false;

            return CharacterTurnPlanner.ShouldTurn(_turnCooldown, angleDelta, _presentationConfig);
        }

        private void BeginTurn(bool turnLeft, float angleDelta)
        {
            SetPhase(turnLeft ? GuardPhase.TurnLeft : GuardPhase.TurnRight);

            TryGetFacingAngleToTarget(out _, out _, out Quaternion exactTargetRotation);

            CharacterTurnPlan plan = CharacterTurnPlanner.BuildPlan(
                _motor.Root, turnLeft, angleDelta, _presentationConfig, exactTargetRotation);
            _targetTurnAngle = plan.StepAngle;
            _turnTargetRotation = plan.TargetRotation;
            _turnDuration = plan.Duration;
            _turnYawSpeed = plan.YawSpeed;

            _motor.SetTurnRotationOverride(true);
            _motor.SetMovementBlocked(true);
        }

        /// <summary>
        /// 转身期间仍允许释放格挡和攻击打断，避免自动朝向把玩家锁死到完整动画结束。
        /// </summary>
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
            _turnCooldown = CharacterTurnPlanner.GetCooldown(_presentationConfig);
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
            return CharacterTurnPlanner.IsAligned(_motor.Root.rotation, _turnTargetRotation, _presentationConfig);
        }

        /// <summary>
        /// 从锁定点计算精确目标旋转，同时保留左右和角度供动画选择，避免表现层再次采样可能已经移动的目标。
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

        private bool IsWaitingForLockOnTurn()
        {
            return _lockOnQuery != null
                && _lockOnQuery.IsLockOnActive
                && _lockOnQuery.CurrentTarget != null;
        }

    }
}
