using Character.Config;
using Character.Combat;
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
        private AttackMoveId _attackId = AttackMoveId.Combo1;
        
        public AttackMoveId CurrentAttackId => _attackId;
        public byte CurrentComboStep => _attackId.ToByte();
        
        public CharacterStateId Id { get; } = CharacterStateId.Attack;

        public NpcAttackState(CharacterStateMachine fsm, CharacterStateRegistry registry, NpcMotor motor,
            CharacterCombatConfig combatConfig)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combatConfig = combatConfig;
        }
        
        public void Prepare(byte comboStep) => _attackId = AttackMoveIdExtensions.FromByte(comboStep);

        public void Prepare(AttackMoveId attackId) => _attackId = attackId.ClampOrDefault();
        
        
        public void Enter()
        {
            _timer = 0f;
            _motor?.Stop();
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            if(_timer < _combatConfig.GetAttackDuration(_attackId)) return;

            _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }

        public void Exit()
        {
            _timer = 0f;
            _attackId = AttackMoveId.Combo1;
        }
    }
}
