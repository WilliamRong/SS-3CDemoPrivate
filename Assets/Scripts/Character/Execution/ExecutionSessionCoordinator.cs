using System.Collections.Generic;
using Character.Combat;
using Character.Config;

namespace Character.Execution
{
    public enum ExecutionSessionCreateFailure : byte
    {
        None = 0,
        EligibilityRejected = 1,
        InvalidAuthorityTime = 2,
        InvalidSessionData = 3,
        ExecutionIdExhausted = 4,
        ReservationConflict = 5,
    }

    /// <summary>
    /// 权威处决会话的唯一存储，并维护
    /// ActorId 到 executionId 的占用关系。
    /// 仅允许在 Unity 主线程或同一权威串行上下文中调用。
    /// </summary>
    public sealed class ExecutionSessionCoordinator :
        IExecutionOccupancyQuery
    {
        private readonly Dictionary<ulong, ExecutionSession>
            _sessionsById = new();

        private readonly Dictionary<int, ulong>
            _sessionIdByActor = new();

        private ulong _nextExecutionId = 1;

        public int ActiveSessionCount => _sessionsById.Count;

        public bool IsActorOccupied(int actorId)
        {
            return actorId != 0 &&
                   _sessionIdByActor.ContainsKey(actorId);
        }

        public bool TryGetSession(
            ulong executionId,
            out ExecutionSession session)
        {
            if (executionId == 0)
            {
                session = default;
                return false;
            }

            return _sessionsById.TryGetValue(
                executionId,
                out session);
        }

        public bool TryGetSessionForActor(
            int actorId,
            out ExecutionSession session)
        {
            session = default;

            if (actorId == 0 ||
                !_sessionIdByActor.TryGetValue(
                    actorId,
                    out ulong executionId))
            {
                return false;
            }

            return _sessionsById.TryGetValue(
                executionId,
                out session);
        }

        /// <summary>
        /// 使用当前权威状态重新校验资格，随后一次性创建会话并占用双方。
        /// 不信任 CandidateResolver 先前计算出的锚点或资格结果。
        /// </summary>
        public bool TryCreateSession(
            CombatActor executor,
            CombatActor target,
            CharacterCombatConfig config,
            double authorityStartTimeSec,
            out ExecutionSession session,
            out ExecutionEligibilityResult eligibility,
            out ExecutionSessionCreateFailure failure)
        {
            session = default;
            eligibility = default;
            failure = ExecutionSessionCreateFailure.None;

            if (!IsFinite(authorityStartTimeSec) ||
                authorityStartTimeSec < 0d)
            {
                failure =
                    ExecutionSessionCreateFailure.InvalidAuthorityTime;
                return false;
            }

            eligibility =
                ExecutionEligibilityService.EvaluateCurrent(
                    executor,
                    target,
                    config,
                    this);

            if (!eligibility.IsEligible)
            {
                failure =
                    ExecutionSessionCreateFailure.EligibilityRejected;
                return false;
            }

            if (!TryPeekNextExecutionId(out ulong executionId))
            {
                failure =
                    ExecutionSessionCreateFailure.ExecutionIdExhausted;
                return false;
            }

            double resultTimeSec =
                authorityStartTimeSec +
                config.executionResultTime;

            float executionDamage = config.executionDamage;
            bool targetWillDie =
                executionDamage > 0f &&
                target.CurrentHp <= executionDamage;

            var proposedSession = new ExecutionSession(
                executionId,
                executor.ActorId,
                target.ActorId,
                eligibility.FixedTargetPose,
                eligibility.ExecutorAnchorPose,
                authorityStartTimeSec,
                resultTimeSec,
                executionDamage,
                targetWillDie);

            if (!proposedSession.TryValidate(out _))
            {
                failure =
                    ExecutionSessionCreateFailure.InvalidSessionData;
                return false;
            }

            if (!TryReserve(proposedSession))
            {
                failure =
                    ExecutionSessionCreateFailure.ReservationConflict;
                return false;
            }

            AdvanceExecutionId(executionId);
            session = proposedSession;
            return true;
        }


        public bool TryMarkResultCommitted(
            ulong executionId,
            out ExecutionSession session)
        {
            if (!TryGetSession(executionId, out ExecutionSession existing))
            {
                session = default;
                return false;
            }

            // 重复结果消息不会再次改变会话。
            if (existing.IsResultCommitted)
            {
                session = existing;
                return true;
            }

            return TryStoreUpdatedSession(
                existing.MarkResultCommitted(),
                out session,
                out _);
        }

        public bool TryMarkExecutorCompleted(
            ulong executionId,
            out ExecutionSession session,
            out bool released)
        {
            if (!TryGetSession(executionId, out ExecutionSession existing))
            {
                session = default;
                released = false;
                return false;
            }

            if (existing.IsExecutorCompleted)
            {
                session = existing;
                released = false;
                return true;
            }

            return TryStoreUpdatedSession(
                existing.MarkExecutorCompleted(),
                out session,
                out released);
        }

        public bool TryMarkTargetCompleted(
            ulong executionId,
            out ExecutionSession session,
            out bool released)
        {
            if (!TryGetSession(executionId, out ExecutionSession existing))
            {
                session = default;
                released = false;
                return false;
            }

            // 目标不能在权威致死结果提交前完成处决。
            if (!existing.IsResultCommitted)
            {
                session = existing;
                released = false;
                return false;
            }

            if (existing.IsTargetCompleted)
            {
                session = existing;
                released = false;
                return true;
            }

            return TryStoreUpdatedSession(
                existing.MarkTargetCompleted(),
                out session,
                out released);
        }

        /// <summary>
        /// 生命周期异常时取消会话并立即释放双方占用。
        /// </summary>
        public bool TryCancelSession(
            ulong executionId,
            out ExecutionSession cancelledSession)
        {
            if (!TryGetSession(executionId, out ExecutionSession existing))
            {
                cancelledSession = default;
                return false;
            }

            return TryStoreUpdatedSession(
                existing.MarkCancelled(),
                out cancelledSession,
                out _);
        }


        /// <summary>
        /// 只移除仍由该 executionId 持有的占用。
        /// 重复释放不会改变任何状态。
        /// </summary>
        public bool TryReleaseSession(
            ulong executionId,
            out ExecutionSession releasedSession)
        {
            releasedSession = default;

            if (!_sessionsById.TryGetValue(
                    executionId,
                    out ExecutionSession existing))
            {
                return false;
            }

            _sessionsById.Remove(executionId);

            ReleaseActorReservation(
                existing.ExecutorActorId,
                executionId);

            ReleaseActorReservation(
                existing.TargetActorId,
                executionId);

            releasedSession = existing;
            return true;
        }


        private bool TryStoreUpdatedSession(
    in ExecutionSession updated,
    out ExecutionSession session,
    out bool released)
        {
            session = default;
            released = false;

            if (!updated.TryValidate(out _) ||
                !_sessionsById.ContainsKey(updated.ExecutionId))
            {
                return false;
            }

            _sessionsById[updated.ExecutionId] = updated;
            session = updated;

            // ResultCommitted 本身不结束会话。
            // 只有取消，或者双方都完成，才释放全局占用。
            if (!updated.IsComplete)
                return true;

            if (!TryReleaseSession(updated.ExecutionId, out _))
                return false;

            released = true;
            return true;
        }

        private bool TryReserve(in ExecutionSession session)
        {
            if (IsActorOccupied(session.ExecutorActorId) ||
                IsActorOccupied(session.TargetActorId) ||
                _sessionsById.ContainsKey(session.ExecutionId))
            {
                return false;
            }

            // 检查和三次写入都发生在同一个权威串行调用中。
            _sessionsById.Add(
                session.ExecutionId,
                session);

            _sessionIdByActor.Add(
                session.ExecutorActorId,
                session.ExecutionId);

            _sessionIdByActor.Add(
                session.TargetActorId,
                session.ExecutionId);

            return true;
        }

        private void ReleaseActorReservation(
            int actorId,
            ulong executionId)
        {
            if (_sessionIdByActor.TryGetValue(
                    actorId,
                    out ulong ownerExecutionId) &&
                ownerExecutionId == executionId)
            {
                _sessionIdByActor.Remove(actorId);
            }
        }

        private bool TryPeekNextExecutionId(
            out ulong executionId)
        {
            executionId = _nextExecutionId;

            return executionId != 0 &&
                   !_sessionsById.ContainsKey(executionId);
        }

        private void AdvanceExecutionId(ulong allocatedId)
        {
            _nextExecutionId =
                allocatedId == ulong.MaxValue
                    ? 0
                    : allocatedId + 1;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }
    }
}
