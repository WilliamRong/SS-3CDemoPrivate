using Character.Intent;
using Character.Motor;

namespace Character.StateMachine.States
{
    /// <summary>
    /// 将有移动输入时的动作优先级集中处理，Motor 只负责落实本帧位移。
    /// </summary>
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

        /// <summary>
        /// 先仲裁战斗转换再推进移动，确保按键边沿不会被同帧 Motor 位移延后一个状态 Tick。
        /// </summary>
        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _motor.SetSprintActive(false);
            
            if (intent.IsDodgePressed)
            {
                _fsm.TryTransition(CharacterStateId.Dodge, _registry, TransitionReason.InputDodge);
                return;
            }
            
            if (intent.IsGuardHeld)
            {
                _fsm.TryTransition(CharacterStateId.Guard, _registry, TransitionReason.InputGuard);
                return;
            }

            if (intent.IsAttackPressed)
            {
                _fsm.TryTransition(CharacterStateId.Attack, _registry, TransitionReason.InputAttack);
                return;
            }

            _motor.Tick(intent, deltaTime);

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
