using Character.Intent;

namespace Character.StateMachine
{
    public interface ICharacterState
    {
        CharacterStateId Id { get; }

        void Enter();
        void Tick(CharacterIntent intent, float deltaTime);
        void Exit();
    }
}