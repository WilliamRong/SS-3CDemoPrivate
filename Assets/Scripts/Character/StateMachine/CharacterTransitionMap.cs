using System.Collections.Generic;

namespace Character.StateMachine
{
    public static class CharacterTransitionMap
    {
        private static readonly Dictionary<CharacterStateId, HashSet<CharacterStateId>> _allowed =
            new Dictionary<CharacterStateId, HashSet<CharacterStateId>>()
            {
                { CharacterStateId.Idle,   new HashSet<CharacterStateId> { CharacterStateId.Move, CharacterStateId.Sprint, CharacterStateId.Attack, CharacterStateId.Dodge,CharacterStateId.Guard, CharacterStateId.Hit, CharacterStateId.Dead, CharacterStateId.PostureBroken} },
                { CharacterStateId.Move,   new HashSet<CharacterStateId> { CharacterStateId.Idle, CharacterStateId.Sprint, CharacterStateId.Attack, CharacterStateId.Dodge,CharacterStateId.Guard, CharacterStateId.Hit, CharacterStateId.Dead, CharacterStateId.PostureBroken } },
                { CharacterStateId.Sprint, new HashSet<CharacterStateId> { CharacterStateId.Move, CharacterStateId.Idle, CharacterStateId.Attack, CharacterStateId.Dodge,CharacterStateId.Guard, CharacterStateId.Hit, CharacterStateId.Dead, CharacterStateId.PostureBroken } },
                { CharacterStateId.Attack, new HashSet<CharacterStateId> { CharacterStateId.Idle, CharacterStateId.Move, CharacterStateId.Dodge, CharacterStateId.Hit, CharacterStateId.Dead, CharacterStateId.PostureBroken } },
                { CharacterStateId.Guard,  new HashSet<CharacterStateId>  {CharacterStateId.Idle, CharacterStateId.Move, CharacterStateId.Attack, CharacterStateId.Dodge, CharacterStateId.Hit, CharacterStateId.Dead, CharacterStateId.PostureBroken } },
                { CharacterStateId.Dodge,  new HashSet<CharacterStateId> { CharacterStateId.Idle, CharacterStateId.Move, CharacterStateId.Sprint, CharacterStateId.Attack, CharacterStateId.Hit, CharacterStateId.Dead, CharacterStateId.PostureBroken } },
                { CharacterStateId.Hit,    new HashSet<CharacterStateId> { CharacterStateId.Hit, CharacterStateId.Idle, CharacterStateId.Move, CharacterStateId.Sprint, CharacterStateId.Dead, CharacterStateId.PostureBroken } },
                { CharacterStateId.Dead,   new HashSet<CharacterStateId> { CharacterStateId.Idle } }, // revive
                { CharacterStateId.PostureBroken,new HashSet<CharacterStateId>{CharacterStateId.Idle,CharacterStateId.Move,CharacterStateId.Dead,}},
            };

        public static bool CanTransition(CharacterStateId from, CharacterStateId to)
        {
            if (!_allowed.TryGetValue(from, out var allowed)) return false;
            return allowed.Contains(to);
        }
    }
}
