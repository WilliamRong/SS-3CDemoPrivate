using Character.Intent;
using Character.Motor;

namespace Character.StateMachine.States
{
    public sealed class MoveState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterMotor _motor;
        private readonly CharacterStateRegistry _registry;

        public MoveState(CharacterStateMachine fsm, CharacterMotor motor, CharacterStateRegistry registry)
        {
            _fsm = fsm;
            _motor = motor;
            _registry = registry;
        }

        public CharacterStateId Id { get; } = CharacterStateId.Move;
        public void Enter() { }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _motor.SetSprintActive(false);
            _motor.Tick(intent, deltaTime);
            
            if (intent.IsDodgePressed)
            {
                _fsm.TryTransition(CharacterStateId.Dodge, _registry, TransitionReason.InputDodge);
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
                _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.InputMove);
                return;
            }

            if (intent.IsSprintHeld)
            {
                _fsm.TryTransition(CharacterStateId.Sprint, _registry, TransitionReason.InputSprint);
            }
        }

        public void Exit() { }
    }
}