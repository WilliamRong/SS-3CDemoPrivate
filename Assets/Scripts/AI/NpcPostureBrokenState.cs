using Character.Config;
using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI
{
    public sealed class NpcPostureBrokenState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combat;
        private float _timer;

        public CharacterStateId Id => CharacterStateId.PostureBroken;

        public NpcPostureBrokenState(
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
            _timer = 0f;
            _motor?.Stop();
        }

        public void Exit()
        {
            _timer = 0f;
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            if (_timer >= Mathf.Max(0.01f, _combat.postureBreakDuration))
                _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }
    }
}
