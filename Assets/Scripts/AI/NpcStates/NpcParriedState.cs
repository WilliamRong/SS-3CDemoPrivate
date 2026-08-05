using Character.Config;
using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI
{

    public sealed class NpcParriedState : ICharacterState
    {

        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combat;
        private float _elapsed;
        public CharacterStateId Id => CharacterStateId.Parried;

        public NpcParriedState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            NpcMotor motor,
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
            _motor?.Stop();
            _motor?.ResetPath();
        }

        public void Exit()
        {
            _elapsed = 0f;
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _elapsed += deltaTime;
            _motor?.Stop();

            if (_elapsed >= Mathf.Max(0.01f, _combat.parriedDuration))
            {
                _fsm.TryTransition(
                    CharacterStateId.Idle,
                    _registry,
                    TransitionReason.Timeout);
            }
        }

    }
}
