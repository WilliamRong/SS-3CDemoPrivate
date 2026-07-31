using Character.Config;
using Character.Intent;
using Character.Motor;
using UnityEngine;

namespace Character.StateMachine.States
{
    /// <summary>
    /// 用显式冲刺阶段和短输入缓存识别 180 度转身，避免依赖单帧摇杆采样或动画进度。
    /// </summary>
    public sealed class SprintState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;
        private readonly CharacterStateRegistry _registry;
        private readonly SprintPhaseConfig _config;

        /// <summary>
        /// 阶段会进入网络快照，声明顺序必须保持稳定以兼容远端表现。
        /// </summary>
        public enum SprintPhase { Start, Loop, Brake, Turn180 }
        public SprintPhase CurrentPhase { get; private set; }

        private float _phaseElapsed;
        private float _turn180Cooldown;
        private Vector2 _previousMoveInput;
        private Vector2 _bufferedMoveInput;
        private float _inputBufferTimeRemaining;

        private CharacterStateId _exitTargetAfterBrake = CharacterStateId.Idle;

        public SprintState(
            CharacterStateMachine fsm,
            CharacterMotor motor,
            Character.Core.CharacterContext context,
            CharacterStateRegistry registry,
            SprintPhaseConfig config)
        {
            _fsm = fsm;
            _motor = motor;
            _registry = registry;
            _config = config;
        }

        public CharacterStateId Id { get; } = CharacterStateId.Sprint;

        // ============ 状态生命周期 ============

        public void Enter()
        {
            SetPhase(SprintPhase.Start);
            _turn180Cooldown = 0f;
            ClearInputBuffer();
            _previousMoveInput = Vector2.zero;
            _motor.SetSprintActive(true);
        }

        /// <summary>
        /// 离散阶段、180 度转身和战斗取消共用一次输入快照，避免阶段切换后重复消费同一按键。
        /// </summary>
        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _turn180Cooldown = Mathf.Max(0f, _turn180Cooldown - deltaTime);
            TickInputBufferDecay(deltaTime);

            if (TryCombatTransition(intent))
                return;

            if (intent.IsSprintHeld && TryBeginTurn180(intent.Move))
            {
                TickMotor(intent, deltaTime, sprintActive: false);
                FinishTick(intent);
                return;
            }

            if (CurrentPhase is SprintPhase.Brake or SprintPhase.Turn180)
            {
                TickMotor(intent, deltaTime, sprintActive: false);
                TryAdvanceOneShotPhase(deltaTime);
                FinishTick(intent);
                return;
            }

            if (TryAdvanceOneShotPhase(deltaTime))
            {
                FinishTick(intent);
                return;
            }

            if (!intent.IsSprintHeld)
            {
                BeginBrake(CharacterStateId.Move);
                FinishTick(intent);
                return;
            }

            if (intent.Move.sqrMagnitude <= 0.0001f)
            {
                BeginBrake(CharacterStateId.Idle);
                FinishTick(intent);
                return;
            }

            TickMotor(intent, deltaTime, sprintActive: true);
            FinishTick(intent);
        }

        public void Exit()
        {
            _motor.SetSprintActive(false);
            CurrentPhase = SprintPhase.Start;
            _phaseElapsed = 0f;
            ClearInputBuffer();
        }

        // ============ 战斗转换 ============

        /// <summary>
        /// 攻击和闪避从冲刺派生专用动作上下文，必须在普通刹车判定前取得优先级。
        /// </summary>
        private bool TryCombatTransition(CharacterIntent intent)
        {
            if (intent.IsDodgePressed)
            {
                FinishTick(intent);
                return _fsm.TryTransition(CharacterStateId.Dodge, _registry, TransitionReason.InputDodge);
            }
            
            
            if (intent.IsGuardHeld)
            {
                FinishTick(intent);
                return _fsm.TryTransition(CharacterStateId.Guard, _registry, TransitionReason.InputGuard);
            }
            
            if (intent.IsAttackPressed)
            {
                FinishTick(intent);
                var attackState = _registry.Get(CharacterStateId.Attack) as AttackState;
                attackState?.PrepareSprintAttack();

                bool transitioned = _fsm.TryTransition(CharacterStateId.Attack, _registry, TransitionReason.InputAttack);
                if (!transitioned)
                    attackState?.PrepareComboAttack();

                return transitioned;
            }

            return false;
        }

        private void TickMotor(CharacterIntent intent, float deltaTime, bool sprintActive)
        {
            _motor.SetSprintActive(sprintActive);
            _motor.Tick(intent, deltaTime);
        }

        private void FinishTick(CharacterIntent intent)
        {
            _previousMoveInput = intent.Move;
            CommitInputBuffer(intent.Move);
        }

        // ============ 输入缓存 ============

        private void TickInputBufferDecay(float deltaTime)
        {
            if (_inputBufferTimeRemaining <= 0f)
                return;

            _inputBufferTimeRemaining -= deltaTime;
            if (_inputBufferTimeRemaining <= 0f)
                ClearInputBuffer();
        }

        private void CommitInputBuffer(Vector2 move)
        {
            float minSqr = _config.minOppositeInputMagnitude * _config.minOppositeInputMagnitude;
            if (move.sqrMagnitude < minSqr)
                return;

            _bufferedMoveInput = move;
            _inputBufferTimeRemaining = _config.inputBufferDuration;
        }

        private void ClearInputBuffer()
        {
            _bufferedMoveInput = Vector2.zero;
            _inputBufferTimeRemaining = 0f;
        }

        private bool HasActiveInputBuffer()
        {
            float minSqr = _config.minOppositeInputMagnitude * _config.minOppositeInputMagnitude;
            return _inputBufferTimeRemaining > 0f && _bufferedMoveInput.sqrMagnitude >= minSqr;
        }

        // ============ 阶段推进 ============

        private void SetPhase(SprintPhase phase)
        {
            CurrentPhase = phase;
            _phaseElapsed = 0f;
        }

        /// <summary>
        /// 单次阶段用剩余时间循环推进，低帧率跨越多个边界时仍能落到正确最终阶段。
        /// </summary>
        private bool TryAdvanceOneShotPhase(float deltaTime)
        {
            if (!_config.IsOneShot(CurrentPhase))
                return false;

            _phaseElapsed += deltaTime;
            if (_phaseElapsed < _config.GetPhaseCompleteDuration(CurrentPhase))
                return false;

            switch (CurrentPhase)
            {
                case SprintPhase.Start:
                    SetPhase(SprintPhase.Loop);
                    _motor.SetSprintActive(true);
                    return false;
                case SprintPhase.Turn180:
                    SetPhase(SprintPhase.Loop);
                    _motor.SetSprintActive(true);
                    _turn180Cooldown = _config.turn180Cooldown;
                    ClearInputBuffer();
                    return false;
                case SprintPhase.Brake:
                    return _fsm.TryTransition(_exitTargetAfterBrake, _registry, TransitionReason.InputMove);
                default:
                    return false;
            }
        }

        private void BeginBrake(CharacterStateId exitTarget)
        {
            if (CurrentPhase == SprintPhase.Brake)
                return;

            SetPhase(SprintPhase.Brake);
            _exitTargetAfterBrake = exitTarget;
            _motor.SetSprintActive(false);
        }

        // ============ 180 度转身 ============

        private bool CanAttemptTurn180()
        {
            return CurrentPhase is SprintPhase.Start or SprintPhase.Loop or SprintPhase.Brake;
        }

        private bool TryBeginTurn180(Vector2 currentMove)
        {
            if (_turn180Cooldown > 0f || !CanAttemptTurn180())
                return false;

            if (!IsOppositeStick(currentMove))
                return false;

            SetPhase(SprintPhase.Turn180);
            ClearInputBuffer();
            return true;
        }

        /// <summary>
        /// 同时约束当前输入与上一帧输入，避免从静止突然反向时误触 180 度转身。
        /// </summary>
        private bool IsOppositeStick(Vector2 currentMove)
        {
            float minSqr = _config.minOppositeInputMagnitude * _config.minOppositeInputMagnitude;
            if (currentMove.sqrMagnitude < minSqr)
                return false;

            var current = currentMove.normalized;
            float oppositeDot = -_config.oppositeInputDotThreshold;

            if (HasActiveInputBuffer()
                && Vector2.Dot(current, _bufferedMoveInput.normalized) < oppositeDot)
                return true;

            if (_previousMoveInput.sqrMagnitude >= minSqr
                && Vector2.Dot(current, _previousMoveInput.normalized) < oppositeDot)
                return true;

            return false;
        }
    }
}
