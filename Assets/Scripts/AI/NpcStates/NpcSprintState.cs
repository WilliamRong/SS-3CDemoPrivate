using Character.Config;
using AI;
using Character.Intent;
using Character.StateMachine;
using Character.StateMachine.States;
using UnityEngine;

namespace AI.NpcStates
{
    /// <summary>
    /// 用显式阶段同�?NPC 冲刺表现，使远端不必从速度反推 Start/Loop/Brake�?    /// </summary>
    public sealed class NpcSprintState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combatConfig;
        
        private float _phaseTimer;
        private float _sprintHoldDuration = 1.5f;

        public SprintState.SprintPhase CurrentPhase { get; private set; } = SprintState.SprintPhase.Start;

        public CharacterStateId Id { get; } = CharacterStateId.Sprint;
        
        public NpcSprintState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            NpcMotor motor,
            CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combatConfig = combat;
        }

        public void Prepare(float holdDuration = 1.5f) => _sprintHoldDuration = Mathf.Max(0.2f, holdDuration);

        // ============ 状态生命周�?============

        public void Enter()
        {
            SetPhase(SprintState.SprintPhase.Start);
        }
        
        /// <summary>
        /// NPC 冲刺沿用 Player 的阶段枚举，使同一套网络快照和 Presenter 无需区分控制来源�?        /// </summary>
        public void Tick(CharacterIntent intent, float deltaTime)
        {
            switch (CurrentPhase)
            {
                case SprintState.SprintPhase.Start:
                    TickStart(deltaTime);
                    break;
                case SprintState.SprintPhase.Loop:
                    TickLoop(deltaTime);
                    break;
                case SprintState.SprintPhase.Brake:
                    TickBrake(deltaTime);
                    break;
                default:
                    SetPhase(SprintState.SprintPhase.Loop);
                    break;
            }
        }
        
        public void Exit() => SetPhase(SprintState.SprintPhase.Start);

        // ============ 阶段推进 ============

        private void TickStart(float deltaTime)
        {
            _phaseTimer += deltaTime;
            if (_phaseTimer >= 0.75f) // 对齐 SprintPhaseConfig.startDuration 默认�?
                SetPhase(SprintState.SprintPhase.Loop);
        }
        
        private void TickLoop(float deltaTime)
        {
            _phaseTimer += deltaTime;
            if (_phaseTimer >= _sprintHoldDuration)
                SetPhase(SprintState.SprintPhase.Brake);
        }
        
        private void TickBrake(float deltaTime)
        {
            _phaseTimer += deltaTime;
            if (_phaseTimer >= 0.2f) // brakeDuration 默认
            {
                _motor?.Stop();
                _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
            }
        }
        
        private void SetPhase(SprintState.SprintPhase phase)
        {
            CurrentPhase = phase;
            _phaseTimer = 0f;
        }
    }
}
