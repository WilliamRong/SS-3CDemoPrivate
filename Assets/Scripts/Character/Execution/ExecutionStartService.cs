using System;
using Character.Combat;
using Character.Config;
using Character.StateMachine;


namespace Character.Execution
{

    public enum ExecutionStartFailure : byte
    {
        None = 0,
        SessionCreationRejected = 1,
        StateEntryUnavailable = 2,
        ExecutorSuppressionRejected = 3,
        TargetSuppressionRejected = 4,
        TargetStateRejected = 5,
        ExecutorStateRejected = 6,
        RollbackFailed = 7,
        LifecycleRegistrationRejected = 8,
        AuthorityUnavailable = 9,
    }


    /// <summary>
    /// 在权威串行上下文中一次性完成：
    /// 会话预占、双方战斗抑制和双方状态进入。
    /// </summary>
    public sealed class ExecutionStartService
    {
        private readonly ExecutionSessionCoordinator _coordinator;
        private readonly ExecutionLifecycleService _lifecycle;

        public ExecutionSessionCoordinator Coordinator => _coordinator;
        public ExecutionLifecycleService Lifecycle => _lifecycle;

        public ExecutionStartService(
            ExecutionLifecycleService lifecycle)
        {
            _lifecycle = lifecycle ??
                throw new ArgumentNullException(nameof(lifecycle));

            _coordinator = lifecycle.Coordinator;
        }

        public bool TryStart(
            CombatActor executor,
            CombatActor target,
            CharacterCombatConfig config,
            double authorityStartTimeSec,
            out ExecutionSession session,
            out ExecutionEligibilityResult eligibility,
            out ExecutionSessionCreateFailure createFailure,
            out ExecutionStartFailure startFailure)
        {
            session = default;
            eligibility = default;
            createFailure = ExecutionSessionCreateFailure.None;
            startFailure = ExecutionStartFailure.None;

            CharacterStateId executorPreviousState =
                executor != null
                    ? executor.CurrentStateId
                    : CharacterStateId.None;

            CharacterStateId targetPreviousState =
                target != null
                    ? target.CurrentStateId
                    : CharacterStateId.None;

            if (!_coordinator.TryCreateSession(executor, target, config, authorityStartTimeSec,
            out ExecutionSession created, out eligibility, out createFailure))
            {
                startFailure = ExecutionStartFailure.SessionCreationRejected;
                return false;
            }

            if (!executor.CanEnterExecutionAsExecutor() ||
               !target.CanEnterExecutionAsTarget())
            {
                _coordinator.TryCancelSession(
                    created.ExecutionId,
                    out _);

                startFailure =
                    ExecutionStartFailure.StateEntryUnavailable;
                return false;
            }

            bool executorSuppressed =
                executor.BeginExecutionCombatSuppression(
                    created.ExecutionId);

            if (!executorSuppressed)
            {
                _coordinator.TryCancelSession(
                    created.ExecutionId,
                    out _);

                startFailure =
                    ExecutionStartFailure.ExecutorSuppressionRejected;
                return false;
            }

            bool targetSuppressed =
                target.BeginExecutionCombatSuppression(
                    created.ExecutionId);

            if (!targetSuppressed)
            {
                CleanupFailedStart(
                    created,
                    executor,
                    target,
                    executorSuppressed,
                    false);

                startFailure =
                    ExecutionStartFailure.TargetSuppressionRejected;
                return false;
            }

            // 先固定目标，再让处决者开始消费 Root Motion。
            if (!target.TryEnterExecuted(created))
            {
                CleanupFailedStart(
                    created,
                    executor,
                    target,
                    executorSuppressed,
                    targetSuppressed);

                startFailure =
                    ExecutionStartFailure.TargetStateRejected;
                return false;
            }


            if (!executor.TryEnterExecuting(created))
            {
                bool restored =
                    target.TryRollbackExecutionStart(
                        created.ExecutionId,
                        targetPreviousState);

                CleanupFailedStart(
                    created,
                    executor,
                    target,
                    executorSuppressed,
                    targetSuppressed);

                startFailure = restored
                    ? ExecutionStartFailure.ExecutorStateRejected
                    : ExecutionStartFailure.RollbackFailed;

                return false;
            }

            if (!_lifecycle.TryRegister(
                    created,
                    executor,
                    target,
                    executorPreviousState,
                    targetPreviousState))
            {
                bool executorRestored =
                    executor.TryRollbackExecutionStart(
                        created.ExecutionId,
                        executorPreviousState);

                bool targetRestored =
                    target.TryRollbackExecutionStart(
                        created.ExecutionId,
                        targetPreviousState);

                CleanupFailedStart(
                    created,
                    executor,
                    target,
                    executorSuppressed,
                    targetSuppressed);

                startFailure =
                    executorRestored && targetRestored
                        ? ExecutionStartFailure.LifecycleRegistrationRejected
                        : ExecutionStartFailure.RollbackFailed;

                return false;
            }

            session = created;
            return true;

        }

        private void CleanupFailedStart(
            in ExecutionSession session,
            CombatActor executor,
            CombatActor target,
            bool executorSuppressed,
            bool targetSuppressed)
        {
            if (targetSuppressed) { target.EndExecutionCombatSuppression(session.ExecutionId); }

            if (executorSuppressed) { executor.EndExecutionCombatSuppression(session.ExecutionId); }

            _coordinator.TryCancelSession(session.ExecutionId, out _);
        }
    }
}
