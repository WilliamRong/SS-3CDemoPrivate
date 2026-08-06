using Character.Config;
using AI;
using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI.NpcStates
{
    /// <summary>
    /// 破势期间只允许权威超时或死亡打断，阻�?AI 移动意图对硬直状态的干扰�?    /// </summary>
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

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            if (_timer >= Mathf.Max(0.01f, _combat.postureBreakDuration))
                _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }

        public void Exit()
        {
            _timer = 0f;
        }
    }
}
