using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI
{
    public sealed class NpcDeadState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        
        public CharacterStateId Id { get; } = CharacterStateId.Dead;
        
        public NpcDeadState(CharacterStateMachine fsm, CharacterStateRegistry registry, NpcMotor motor)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
        }

        
        public void Enter()
        {
           _motor?.Stop();
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            // 保持死亡直到 ServerTryRevive / BT 触发 Revive
        }

        public void Exit()
        {
           
        }
    }
}
