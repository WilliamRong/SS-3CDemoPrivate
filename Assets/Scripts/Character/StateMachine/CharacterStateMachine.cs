using Character.Config;
using Character.Intent;

namespace Character.StateMachine
{
    public sealed class CharacterStateMachine
    {
        public ICharacterState CurrentState { get; private set; }
        public CharacterStateId CurrentId => CurrentState?.Id ?? CharacterStateId.None;

        private readonly CharacterStateRuntime _runtime = new CharacterStateRuntime();

        public CharacterStateMachine(CharacterCombatConfig combatConfig = null)
        {
            _runtime.SetCombatConfig(combatConfig);
        }

        private static readonly CharacterInterruptRule[] _interruptRules =
        {
            // 攻击生效窗内：轻击不可打断，重击可打断
            new CharacterInterruptRule(CharacterStateId.Attack, CharacterStateId.Hit, StateWindowType.ActiveWindow, TransitionReason.HitLight, false),
            new CharacterInterruptRule(CharacterStateId.Attack, CharacterStateId.Hit, StateWindowType.ActiveWindow, TransitionReason.HitHeavy, true),

            //翻滚
            new CharacterInterruptRule(CharacterStateId.Attack, CharacterStateId.Dodge, StateWindowType.PreHitWindow, TransitionReason.InputDodge, false),
            new CharacterInterruptRule(CharacterStateId.Attack, CharacterStateId.Dodge, StateWindowType.ActiveWindow, TransitionReason.InputDodge, false),
            new CharacterInterruptRule(CharacterStateId.Attack, CharacterStateId.Dodge, StateWindowType.RecoveryWindow, TransitionReason.InputDodge, true),
            new CharacterInterruptRule(CharacterStateId.Attack, CharacterStateId.Dodge, StateWindowType.Always, TransitionReason.InputDodge, true),
            new CharacterInterruptRule(CharacterStateId.Dodge, CharacterStateId.Attack, StateWindowType.Always, TransitionReason.InputAttack, true),
            
            //防御
            new CharacterInterruptRule(CharacterStateId.Guard, CharacterStateId.Dodge, StateWindowType.Always, TransitionReason.InputDodge,  true),
            new CharacterInterruptRule(CharacterStateId.Guard, CharacterStateId.Hit,   StateWindowType.Always, TransitionReason.HitLight,    true),
            new CharacterInterruptRule(CharacterStateId.Guard, CharacterStateId.Hit,   StateWindowType.Always, TransitionReason.HitHeavy,    true),
            new CharacterInterruptRule(CharacterStateId.Guard, CharacterStateId.Dead,  StateWindowType.Always, TransitionReason.Death,       true),
            
            // 死亡永远可抢占
            new CharacterInterruptRule(CharacterStateId.Attack, CharacterStateId.Dead, StateWindowType.Always, TransitionReason.Death, true),
            new CharacterInterruptRule(CharacterStateId.Dodge, CharacterStateId.Dead, StateWindowType.Always, TransitionReason.Death, true),
            new CharacterInterruptRule(CharacterStateId.Hit, CharacterStateId.Dead, StateWindowType.Always, TransitionReason.Death, true),
        };

        public void Initialize(ICharacterState initialState)
        {
            CurrentState = initialState;
            CurrentState.Enter();
            _runtime.OnStateEntered(initialState.Id);
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _runtime.Tick(deltaTime);
            CurrentState.Tick(intent, deltaTime);
        }

        public bool TryTransition(CharacterStateId targetId, CharacterStateRegistry registry, TransitionReason reason = TransitionReason.Any)
        {
            if (targetId == CurrentId) return false;
            if (!CharacterTransitionMap.CanTransition(CurrentId, targetId)) return false;
            if (!CanInterrupt(CurrentId, targetId, _runtime.GetCurrentWindowType(), reason)) return false;

            var target = registry.Get(targetId);
            if (target == null) return false;

            ChangeState(target, targetId);
            return true;
        }

        private void ChangeState(ICharacterState newState, CharacterStateId newStateId)
        {
            CurrentState.Exit();
            CurrentState = newState;
            CurrentState.Enter();
            _runtime.OnStateEntered(newStateId);
        }

        private static bool CanInterrupt(CharacterStateId from, CharacterStateId incoming, StateWindowType window, TransitionReason reason)
        {
            for (int i = 0; i < _interruptRules.Length; i++)
            {
                var rule = _interruptRules[i];
                if (rule.FromState != from || rule.IncomingState != incoming) continue;
                if (rule.WindowType != StateWindowType.Always && rule.WindowType != window) continue;
                if (rule.Reason != TransitionReason.Any && rule.Reason != reason) continue;
                return rule.IsAllowed;
            }

            // Default: allow unless an explicit rule rejects.
            return true;
        }
    }
}
