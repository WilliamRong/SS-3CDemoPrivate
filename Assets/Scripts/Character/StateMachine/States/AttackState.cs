using Character.Config;
using Character.Intent;
using Character.Motor;

namespace Character.StateMachine.States
{
    public sealed class AttackState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;
        private readonly CharacterStateRegistry _registry;
        private readonly CharacterCombatConfig _combat;

        private float _timer;

        public CharacterStateId Id { get; } = CharacterStateId.Attack;

        public AttackState(
            CharacterStateMachine fsm,
            CharacterMotor motor,
            CharacterStateRegistry registry,
            CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _motor = motor;
            _registry = registry;
            _combat = combat;
        }

        public void Enter()
        {
            _timer = 0f;
            _motor.SetSprintActive(false);
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            _motor.Tick(intent, deltaTime);
            if (intent.IsDodgePressed)
            {
                if (_fsm.TryTransition(CharacterStateId.Dodge, _registry, TransitionReason.InputDodge))
                    return;
            }
            if (_timer >= _combat.attackDuration)
            {
                bool hasMove = intent.Move.sqrMagnitude > 0.0001f;
                _fsm.TryTransition(hasMove ? CharacterStateId.Move : CharacterStateId.Idle, _registry, TransitionReason.Timeout);
            }
        }

        public void Exit()
        {
        }
    }
}
