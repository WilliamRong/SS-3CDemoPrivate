using Character.Intent;
using Character.Motor;

namespace Character.StateMachine.States
{
    public sealed class DeadState : ICharacterState
    {
        private readonly CharacterMotor _motor;

        public CharacterStateId Id { get; } = CharacterStateId.Dead;

        public DeadState(CharacterMotor motor)
        {
            _motor = motor;
        }

        public void Enter()
        {
            _motor.SetSprintActive(false);
            _motor.BeginReactionRootMotion();
            _motor.SetMovementBlocked(true);
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _motor.Tick(intent, deltaTime);
        }

        public void Exit()
        {
            _motor.EndReactionRootMotion();
            _motor.SetMovementBlocked(false);
        }
    }
}
