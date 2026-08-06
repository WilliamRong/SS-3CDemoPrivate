using Character.Intent;
using AI;
using Character.StateMachine;
using UnityEngine;

namespace AI.NpcStates
{
    /// <summary>
    /// 姝讳骸鎬佷笉鑷閫€鍑猴紝澶嶆椿蹇呴』鐢辨湇鍔″櫒鏄惧紡鍙戣捣锛岄伩鍏嶈鏃舵垨 AI 鎰忓浘鎰忓澶嶆椿瑙掕壊銆?    /// </summary>
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
            // Dead only accepts an external revive transition.
        }

        public void Exit()
        {
           
        }
    }
}
