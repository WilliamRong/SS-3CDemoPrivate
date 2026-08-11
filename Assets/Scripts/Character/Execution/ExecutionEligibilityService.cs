using Character.Combat;
using Character.Config;

namespace Character.Execution
{
    /// <summary>
    /// Offline 与 Server 共用的完整资格计算入口。
    /// 所有位置、状态和锚点都从当前 Actor 重新读取。
    /// </summary>
    public static class ExecutionEligibilityService
    {
        public static ExecutionEligibilityResult EvaluateCurrent(
            CombatActor executor,
            CombatActor target,
            CharacterCombatConfig config,
            IExecutionOccupancyQuery occupancyQuery = null)
        {
            ExecutionEligibilityResult precheck =
                ExecutionEligibilityPrecheck.Evaluate(
                    executor,
                    target,
                    config,
                    occupancyQuery);

            if (!precheck.IsEligible)
                return precheck;

            return ExecutionPhysicsValidator.Evaluate(
                executor,
                target,
                config,
                precheck);
        }
    }
}