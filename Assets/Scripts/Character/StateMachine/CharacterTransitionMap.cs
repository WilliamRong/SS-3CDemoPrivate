using System.Collections.Generic;

namespace Character.StateMachine
{
    public static class CharacterTransitionMap
    {
        private static readonly Dictionary<CharacterStateId, HashSet<CharacterStateId>> _allowed =
            new Dictionary<CharacterStateId, HashSet<CharacterStateId>>()
            {
                { CharacterStateId.Idle,   new HashSet<CharacterStateId> { CharacterStateId.Move, CharacterStateId.Sprint, CharacterStateId.Attack, CharacterStateId.Dodge, CharacterStateId.Hit, CharacterStateId.Dead } },
                { CharacterStateId.Move,   new HashSet<CharacterStateId> { CharacterStateId.Idle, CharacterStateId.Sprint, CharacterStateId.Attack, CharacterStateId.Dodge, CharacterStateId.Hit, CharacterStateId.Dead } },
                { CharacterStateId.Sprint, new HashSet<CharacterStateId> { CharacterStateId.Move, CharacterStateId.Idle, CharacterStateId.Attack, CharacterStateId.Dodge, CharacterStateId.Hit, CharacterStateId.Dead } },
                { CharacterStateId.Attack, new HashSet<CharacterStateId> { CharacterStateId.Idle, CharacterStateId.Move, CharacterStateId.Hit, CharacterStateId.Dead } },
                { CharacterStateId.Dodge,  new HashSet<CharacterStateId> { CharacterStateId.Idle, CharacterStateId.Move, CharacterStateId.Hit, CharacterStateId.Dead } },
                { CharacterStateId.Hit,    new HashSet<CharacterStateId> { CharacterStateId.Idle, CharacterStateId.Move, CharacterStateId.Dead } },
                { CharacterStateId.Dead,   new HashSet<CharacterStateId> { CharacterStateId.Idle } }, // revive
            };

        public static bool CanTransition(CharacterStateId from, CharacterStateId to){
            if(!_allowed.TryGetValue(from, out var allowed)) return false;
            return allowed.Contains(to);
        }
    }
}
