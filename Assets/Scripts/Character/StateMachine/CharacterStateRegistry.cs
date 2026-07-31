using System.Collections.Generic;

namespace Character.StateMachine
{
    /// <summary>
    /// 由组合根注册具体状态实例，使状态机只依赖 ID 而不负责构造 Player/NPC 的不同实现。
    /// </summary>
    public sealed class CharacterStateRegistry
    {
        private readonly Dictionary<CharacterStateId, ICharacterState> _map = new();

        public void Register(ICharacterState state)
        {
            _map[state.Id] = state;
        }

        public ICharacterState Get(CharacterStateId id)
        {
            _map.TryGetValue(id, out var state);
            return state;
        }
    }
}
