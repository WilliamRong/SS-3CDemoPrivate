using Character.Config;
using Character.Intent;
using Character.Motor;
using UnityEngine;

namespace Character.StateMachine.States
{
    public enum ParryPhase : byte
    {
        None = 0,
        Startup = 1,
        Active = 2,
        Recovery = 3,
    }
    public sealed class ParryState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly CharacterMotor _motor;
        private readonly CharacterCombatConfig _combat;
        private float _elapsed;

        public CharacterStateId Id => CharacterStateId.Parry;
        public ParryPhase CurrentPhase { get; private set; }

        public ParryState(
    CharacterStateMachine fsm,
    CharacterStateRegistry registry,
    CharacterMotor motor,
    CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combat = combat;
        }

        public void Enter()
        {
            _elapsed = 0f;
            CurrentPhase = ParryPhase.Startup;
            _motor.SetSprintActive(false);
            _motor.SetMovementBlocked(true);
            _motor.SetTurnRotationOverride(true);
        }

        public void Exit()
        {
            _elapsed = 0f;
            _motor.SetMovementBlocked(false);
            _motor.SetTurnRotationOverride(false);
            CurrentPhase = ParryPhase.None;
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _elapsed += deltaTime;
            // Parry is stationary and ignores every input until its animation ends.
            _motor.Tick(default, deltaTime);

            if (_elapsed < _combat.ParryActiveStartTime)
                CurrentPhase = ParryPhase.Startup;
            else if (_elapsed < _combat.ParryActiveEndTime)
                CurrentPhase = ParryPhase.Active;
            else
                CurrentPhase = ParryPhase.Recovery;

            if (_elapsed >= _combat.ParryTotalDuration)
            {
                _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
            }
        }
    }
}
