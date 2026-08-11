
namespace Character.Execution
{

    /// <summary>
    /// 隔离资格判定与会话存储；后续由
    /// ExecutionSessionCoordinator 实现。
    /// </summary>
    public interface IExecutionOccupancyQuery
    {
        bool IsActorOccupied(int actorId);
    }
}