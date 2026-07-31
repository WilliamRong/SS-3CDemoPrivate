using Character.Intent;

namespace Character.StateMachine
{
    /// <summary>
    /// 统一 Player 与 NPC 状态生命周期，使共享状态机不依赖具体控制器或 AI 类型。
    /// </summary>
    public interface ICharacterState
    {
        CharacterStateId Id { get; }

        void Enter();
        void Tick(CharacterIntent intent, float deltaTime);
        void Exit();
    }
}
