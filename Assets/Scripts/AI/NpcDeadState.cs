using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// 死亡态不自行退出，复活必须由服务器显式发起，避免计时或 AI 意图意外复活角色。
    /// </summary>
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
            // 有意留空：死亡只接受外部 Revive 转换。
        }

        public void Exit()
        {
           
        }
    }
}
