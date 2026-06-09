using Character.Config;
using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI
{
    public sealed class NpcAttackState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combatConfig;

        private float _timer;
        private byte _comboStep = 1;
        
        public byte CurrentComboStep => _comboStep;
        
        public CharacterStateId Id { get; } = CharacterStateId.Attack;

        public NpcAttackState(CharacterStateMachine fsm, CharacterStateRegistry registry, NpcMotor motor,
            CharacterCombatConfig combatConfig)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combatConfig = combatConfig;
        }
        
        public void Prepare(byte comboStep) => _comboStep = comboStep is >= 1 and <= 9 ? comboStep: (byte)1;
        
        
        public void Enter()
        {
            _timer = 0f;
            _motor?.Stop();
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            if(_timer < _combatConfig.GetAttackDuration(_comboStep)) return;

            _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }

        public void Exit()
        {
            _timer = 0f;
            _comboStep = 1;
        }
    }
}
