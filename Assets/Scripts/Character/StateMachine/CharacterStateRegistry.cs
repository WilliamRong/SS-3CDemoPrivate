using System.Collections.Generic;

namespace Character.StateMachine
{
    public sealed class CharacterStateRegistry
    {
        private readonly Dictionary<CharacterStateId, ICharacterState> _map = new();
        
        public void Register(ICharacterState state){
            _map[state.Id] = state;
        }

        public ICharacterState Get(CharacterStateId id){
            _map.TryGetValue(id, out var state);
            return state;
        }
       
    }
}
