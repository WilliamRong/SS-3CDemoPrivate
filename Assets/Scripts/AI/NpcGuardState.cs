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

        private float _phaseTimer;
        private float _loopHoldDuration = 2f;

        public GuardState.GuardPhase CurrentPhase { get; private set; } = GuardState.GuardPhase.Start;
        
        public CharacterStateId Id { get; } = CharacterStateId.Guard;
        
        public NpcGuardState(
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
            }
        }

        public void Exit()
        {
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
    }
}
