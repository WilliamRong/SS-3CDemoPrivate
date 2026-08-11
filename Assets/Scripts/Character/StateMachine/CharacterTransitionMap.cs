using System.Collections.Generic;

namespace Character.StateMachine
{
    /// <summary>
    /// 维护状态拓扑的硬边界，具体时机和原因仍由打断规则决定，避免两类规则混在状态代码中。
    /// </summary>
    public static class CharacterTransitionMap
    {
        private static readonly Dictionary<CharacterStateId, HashSet<CharacterStateId>> _allowed =
            new Dictionary<CharacterStateId, HashSet<CharacterStateId>>()
            {
                { CharacterStateId.Idle,   new HashSet<CharacterStateId> { CharacterStateId.Move, CharacterStateId.Sprint, CharacterStateId.Attack, CharacterStateId.Dodge,CharacterStateId.Guard, CharacterStateId.Hit, CharacterStateId.Dead, CharacterStateId.PostureBroken, CharacterStateId.Parry, CharacterStateId.Parried, CharacterStateId.Executing} },
                { CharacterStateId.Move,   new HashSet<CharacterStateId> { CharacterStateId.Idle, CharacterStateId.Sprint, CharacterStateId.Attack, CharacterStateId.Dodge,CharacterStateId.Guard, CharacterStateId.Hit, CharacterStateId.Dead, CharacterStateId.PostureBroken, CharacterStateId.Parry, CharacterStateId.Parried, CharacterStateId.Executing } },
                { CharacterStateId.Sprint, new HashSet<CharacterStateId> { CharacterStateId.Move, CharacterStateId.Idle, CharacterStateId.Attack, CharacterStateId.Dodge,CharacterStateId.Guard, CharacterStateId.Hit, CharacterStateId.Dead, CharacterStateId.PostureBroken, CharacterStateId.Parried } },
                { CharacterStateId.Attack, new HashSet<CharacterStateId> { CharacterStateId.Idle, CharacterStateId.Move, CharacterStateId.Dodge, CharacterStateId.Hit, CharacterStateId.Dead, CharacterStateId.PostureBroken, CharacterStateId.Parried } },
                { CharacterStateId.Guard,  new HashSet<CharacterStateId>  {CharacterStateId.Idle, CharacterStateId.Move, CharacterStateId.Attack, CharacterStateId.Dodge, CharacterStateId.Hit, CharacterStateId.Dead, CharacterStateId.PostureBroken, CharacterStateId.Parry, CharacterStateId.Parried } },
                { CharacterStateId.Dodge,  new HashSet<CharacterStateId> { CharacterStateId.Idle, CharacterStateId.Move, CharacterStateId.Sprint, CharacterStateId.Attack, CharacterStateId.Hit, CharacterStateId.Dead, CharacterStateId.PostureBroken, CharacterStateId.Parried } },
                { CharacterStateId.Hit,    new HashSet<CharacterStateId> { CharacterStateId.Hit, CharacterStateId.Idle, CharacterStateId.Move, CharacterStateId.Sprint, CharacterStateId.Dead, CharacterStateId.PostureBroken, CharacterStateId.Parried } },
                { CharacterStateId.Dead,   new HashSet<CharacterStateId> { CharacterStateId.Idle } }, // revive
                { CharacterStateId.PostureBroken,new HashSet<CharacterStateId>{CharacterStateId.Idle,CharacterStateId.Move,CharacterStateId.Dead, CharacterStateId.Parried, CharacterStateId.Executed}},
                { CharacterStateId.Parry,new HashSet<CharacterStateId>{CharacterStateId.Idle,CharacterStateId.Hit,CharacterStateId.Dead, CharacterStateId.PostureBroken}},
                { CharacterStateId.Parried,new HashSet<CharacterStateId>{CharacterStateId.Idle,CharacterStateId.Hit,CharacterStateId.Dead, CharacterStateId.PostureBroken, CharacterStateId.Executed}},
                {CharacterStateId.Executing, new HashSet<CharacterStateId>{CharacterStateId.Idle,CharacterStateId.Move,}},
                {CharacterStateId.Executed,new HashSet<CharacterStateId>{CharacterStateId.Dead,CharacterStateId.Parried,CharacterStateId.PostureBroken,}},
            };

        public static bool CanTransition(CharacterStateId from, CharacterStateId to)
        {
            if (!_allowed.TryGetValue(from, out var allowed)) return false;
            return allowed.Contains(to);
        }
    }
}
