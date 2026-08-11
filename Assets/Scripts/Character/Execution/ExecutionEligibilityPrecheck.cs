using Character.Combat;
using Character.Config;
using Character.StateMachine;

namespace Character.Execution
{
    /// <summary>
    /// 执行不涉及 Physics 的低成本资格预检。
    /// 可由本地候选查询和权威资格服务共同调用。
    /// </summary>
    public static class ExecutionEligibilityPrecheck
    {
        public static ExecutionEligibilityResult Evaluate(
            CombatActor executor,
            CombatActor target,
            CharacterCombatConfig config,
            IExecutionOccupancyQuery occupancyQuery = null)
        {
            if (executor == null)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.ExecutorMissing);
            }

            if (target == null)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.TargetMissing);
            }

            if (executor == target ||
                executor.ActorId == target.ActorId)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.SameActor);
            }

            if (!executor.IsPlayerActor)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.ExecutorNotPlayer);
            }

            if (executor.IsDead)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.ExecutorDead);
            }

            if (target.IsDead)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.TargetDead);
            }

            if (!IsExecutorStateEligible(
                    executor.CurrentStateId))
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.ExecutorStateInvalid);
            }

            if (!IsTargetStateEligible(
                    target.CurrentStateId))
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.TargetStateInvalid);
            }

            if (occupancyQuery != null &&
                occupancyQuery.IsActorOccupied(executor.ActorId))
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.ExecutorOccupied);
            }

            if (occupancyQuery != null &&
                occupancyQuery.IsActorOccupied(target.ActorId))
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.TargetOccupied);
            }

            return ExecutionSpatialValidator.Evaluate(
                executor.transform,
                target.transform,
                config);
        }

        public static bool IsExecutorStateEligible(
            CharacterStateId stateId)
        {
            return stateId is
                CharacterStateId.Idle or
                CharacterStateId.Move;
        }

        public static bool IsTargetStateEligible(
            CharacterStateId stateId)
        {
            return stateId is
                CharacterStateId.Parried or
                CharacterStateId.PostureBroken;
        }
    }
}