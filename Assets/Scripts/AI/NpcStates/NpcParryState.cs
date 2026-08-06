using Character.Config;
using AI;
using Character.Intent;
using Character.StateMachine;
using Character.StateMachine.States;

namespace AI.NpcStates
{
    public sealed class NpcParryState : ICharacterState
    {

        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combat;

        private float _elapsed;
        public CharacterStateId Id => CharacterStateId.Parry;
        public ParryPhase CurrentPhase { get; private set; }

        public NpcParryState(
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
            _elapsed = 0f;
            CurrentPhase = ParryPhase.Startup;
            _motor?.Stop();
            _motor?.ResetPath();
        }

        public void Exit()
        {
            _elapsed = 0f;
            CurrentPhase = ParryPhase.None;
            _motor?.Stop();
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            // NPC Parry 期间忽略全部 AI 意图�?
            _motor?.Stop();
            _elapsed += deltaTime;


            if (_elapsed < _combat.ParryActiveStartTime)
            {
                CurrentPhase = ParryPhase.Startup;
            }
            else if (_elapsed < _combat.ParryActiveEndTime)
            {
                CurrentPhase = ParryPhase.Active;
            }
            else
            {
                CurrentPhase = ParryPhase.Recovery;
            }

            if (_elapsed >= _combat.ParryTotalDuration)
            {
                _fsm.TryTransition(
                    CharacterStateId.Idle,
                    _registry,
                    TransitionReason.Timeout);
            }
        }
    }
}
