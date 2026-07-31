using Character.Intent;
using Character.Motor;

namespace Character.StateMachine.States
{
    /// <summary>
    /// 死亡态持续阻断移动并允许死亡动画根位移，退出权只保留给显式复活转换。
    /// </summary>
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
