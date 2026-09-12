using System;
using System.Collections.Generic;
using Character.Combat;
using Character.StateMachine;


namespace Character.Execution
{
    /// <summary>
    /// 在权威串行上下文中推进处决结果、双方独立完成边沿和异常清理。
    /// </summary>
    public sealed class ExecutionLifecycleService
    {
        private sealed class ActiveExecution
        {
            public ExecutionSession Session { get; }
            public CombatActor Executor { get; }
            public CombatActor Target { get; }

            public CharacterStateId ExecutorPreviousState { get; }
            public CharacterStateId TargetPreviousState { get; }

            public ActiveExecution(
               in ExecutionSession session,
               CombatActor executor,
               CombatActor target,
               CharacterStateId executorPreviousState,
               CharacterStateId targetPreviousState)
            {
                Session = session;
                Executor = executor;
                Target = target;
                ExecutorPreviousState = executorPreviousState;
                TargetPreviousState = targetPreviousState;
            }
        }

        private readonly ExecutionSessionCoordinator _coordinator;

        private readonly Dictionary<ulong, ActiveExecution> _activeById = new();

        // Tick 时可能移除运行记录，因此先复用列表复制 key。
        private readonly List<ulong> _iterationIds = new();

        public ExecutionSessionCoordinator Coordinator => _coordinator;
        public int ActiveExecutionCount => _activeById.Count;

        public event Action<ExecutionSession, CombatActor, double> ResultCommitted;

        public event Action<ExecutionSession> CompletionChanged;

        public ExecutionLifecycleService(ExecutionSessionCoordinator coordinator)
        {
            _coordinator = coordinator ??
                throw new ArgumentNullException(nameof(coordinator));
        }

        public bool TryRegister(
            in ExecutionSession session,
            CombatActor executor,
            CombatActor target,
            CharacterStateId executorPreviousState,
            CharacterStateId targetPreviousState)
        {
            if (!session.TryValidate(out _) ||
                !session.IsActive ||
                session.Flags != ExecutionSessionFlags.None ||
                executor == null ||
                target == null ||
                executor.ActorId != session.ExecutorActorId ||
                target.ActorId != session.TargetActorId ||
                executor.CurrentStateId != CharacterStateId.Executing ||
                target.CurrentStateId != CharacterStateId.Executed ||
                executorPreviousState is not (
                    CharacterStateId.Idle or
                    CharacterStateId.Move) ||
                targetPreviousState is not (
                    CharacterStateId.Parried or
                    CharacterStateId.PostureBroken) ||
                _activeById.ContainsKey(session.ExecutionId))
            {
                return false;
            }

            if (!_coordinator.TryGetSession(
                    session.ExecutionId,
                    out ExecutionSession coordinated) ||
                !IsSameSession(session, coordinated))
            {
                return false;
            }

            _activeById.Add(
                session.ExecutionId,
                new ActiveExecution(
                    session,
                    executor,
                    target,
                    executorPreviousState,
                    targetPreviousState));

            return true;
        }

        /// <summary>
        /// authorityNowSec 必须与创建会话时使用同一个时钟域。
        /// </summary>
        public bool Tick(double authorityNowSec)
        {
            if (!IsFinite(authorityNowSec) ||
                authorityNowSec < 0d)
            {
                return false;
            }

            _iterationIds.Clear();

            foreach (ulong executionId in _activeById.Keys)
                _iterationIds.Add(executionId);

            for (int i = 0; i < _iterationIds.Count; i++)
            {
                ulong executionId = _iterationIds[i];

                if (!_activeById.TryGetValue(
                        executionId,
                        out ActiveExecution active))
                {
                    continue;
                }

                if (TickActiveExecution(active, authorityNowSec))
                    _activeById.Remove(executionId);
            }

            return true;
        }

        public bool TryCancel(ulong executionId)
        {
            if (executionId == 0)
                return false;

            if (!_activeById.TryGetValue(
                    executionId,
                    out ActiveExecution active))
            {
                return TryCancelCoordinatedSession(executionId);
            }

            CleanupActorsAndSession(active);
            _activeById.Remove(executionId);
            return true;
        }

        public void CancelAll()
        {
            _iterationIds.Clear();

            foreach (ulong executionId in _activeById.Keys)
                _iterationIds.Add(executionId);

            for (int i = 0; i < _iterationIds.Count; i++)
                TryCancel(_iterationIds[i]);

            _iterationIds.Clear();
        }

        /// <returns>是否应移除本地运行记录。</returns>
        private bool TickActiveExecution(
            ActiveExecution active,
            double authorityNowSec)
        {
            ulong executionId = active.Session.ExecutionId;

            if (active.Executor == null ||
                active.Target == null ||
                !_coordinator.TryGetSession(
                    executionId,
                    out ExecutionSession session))
            {
                CleanupActorsAndSession(active);
                return true;
            }

            // 结果由绝对权威时间决定，不依赖 Animator。
            if (!session.IsResultCommitted &&
                authorityNowSec >= session.ResultTimeSec)
            {
                if (!active.Target.TryCommitExecutionDamage(
                        executionId,
                        session.ExecutionDamage,
                        out bool targetDied) ||
                    targetDied != session.TargetWillDie)
                {
                    CleanupActorsAndSession(active);
                    return true;
                }

                if (!_coordinator.TryMarkResultCommitted(
                        executionId,
                        out session))
                {
                    CleanupActorsAndSession(active);
                    return true;
                }

                ResultCommitted?.Invoke(
                    session,
                    active.Target,
                    authorityNowSec);
            }

            // 处决者按自己的玩法时长独立完成。
            if (!session.IsExecutorCompleted &&
                active.Executor.HasExecutionDurationElapsedAsExecutor(
                    executionId))
            {
                if (!active.Executor.TryCompleteExecutionAsExecutor(
                        executionId))
                {
                    CleanupActorsAndSession(active);
                    return true;
                }

                active.Executor.EndExecutionCombatSuppression(executionId);

                if (!_coordinator.TryMarkExecutorCompleted(
                        executionId,
                        out session,
                        out bool released))
                {
                    CleanupActorsAndSession(active);
                    return true;
                }

                CompletionChanged?.Invoke(session);

                if (released)
                    return true;
            }

            // 目标必须等致死结果已经提交，才能进入处决死亡表现。
            if (session.IsResultCommitted &&
                !session.IsTargetCompleted &&
                active.Target.HasExecutionDurationElapsedAsTarget(
                    executionId))
            {
                if (!active.Target.TryCompleteExecutionAsTarget(
                        executionId))
                {
                    CleanupActorsAndSession(active);
                    return true;
                }

                active.Target.EndExecutionCombatSuppression(executionId);

                if (!_coordinator.TryMarkTargetCompleted(
                        executionId,
                        out session,
                        out bool released))
                {
                    CleanupActorsAndSession(active);
                    return true;
                }

                CompletionChanged?.Invoke(session);

                if (released)
                    return true;
            }

            return false;
        }

        private bool TryCancelCoordinatedSession(ulong executionId)
        {
            if (!_coordinator.TryCancelSession(
                    executionId,
                    out ExecutionSession cancelled))
            {
                return false;
            }

            CompletionChanged?.Invoke(cancelled);
            return true;
        }

        private void CleanupActorsAndSession(
            ActiveExecution active)
        {
            ulong executionId = active.Session.ExecutionId;

            if (active.Executor != null &&
                !active.Executor.IsDead)
            {
                active.Executor.TryRollbackExecutionStart(
                    executionId,
                    active.ExecutorPreviousState);
            }

            if (active.Target != null)
            {
                if (active.Target.IsDead)
                {
                    // 结果已经发生时不能恢复成活着的受创状态。
                    active.Target.TryCompleteExecutionAsTarget(
                        executionId);
                }
                else
                {
                    active.Target.TryRollbackExecutionStart(
                        executionId,
                        active.TargetPreviousState);
                }
            }

            if (active.Executor != null)
                active.Executor.EndExecutionCombatSuppression(executionId);

            if (active.Target != null)
                active.Target.EndExecutionCombatSuppression(executionId);

            TryCancelCoordinatedSession(executionId);
        }

        private static bool IsSameSession(
            in ExecutionSession left,
            in ExecutionSession right)
        {
            return left.ExecutionId == right.ExecutionId &&
                   left.ExecutorActorId == right.ExecutorActorId &&
                   left.TargetActorId == right.TargetActorId &&
                   left.FixedTargetPose.Position.Equals(
                       right.FixedTargetPose.Position) &&
                   left.FixedTargetPose.Yaw.Equals(
                       right.FixedTargetPose.Yaw) &&
                   left.ExecutorAnchorPose.Position.Equals(
                       right.ExecutorAnchorPose.Position) &&
                    left.ExecutorAnchorPose.Yaw.Equals(
                        right.ExecutorAnchorPose.Yaw) &&
                    left.StartTimeSec.Equals(right.StartTimeSec) &&
                    left.ResultTimeSec.Equals(right.ResultTimeSec) &&
                    left.ExecutionDamage.Equals(right.ExecutionDamage) &&
                    left.TargetWillDie == right.TargetWillDie &&
                    left.Flags == right.Flags;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }

    }
}
