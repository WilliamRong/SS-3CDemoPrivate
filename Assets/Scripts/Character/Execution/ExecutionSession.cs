using System;
using UnityEngine;


namespace Character.Execution
{
    [Flags]
    public enum ExecutionSessionFlags : byte
    {
        None = 0,
        ResultCommitted = 1 << 0,
        ExecutorCompleted = 1 << 1,
        TargetCompleted = 1 << 2,
        Cancelled = 1 << 3,
    }

    public readonly struct ExecutionPose
    {
        public Vector3 Position { get; }
        public float Yaw { get; }

        public ExecutionPose(Vector3 position, float yaw)
        {
            Position = position;
            Yaw = yaw;
        }

        public bool IsFinite =>
           IsFiniteValue(Position.x) &&
           IsFiniteValue(Position.y) &&
           IsFiniteValue(Position.z) &&
           IsFiniteValue(Yaw);

        private static bool IsFiniteValue(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct ExecutionSession
    {
        private const ExecutionSessionFlags KnownFlags =
           ExecutionSessionFlags.ResultCommitted |
           ExecutionSessionFlags.ExecutorCompleted |
           ExecutionSessionFlags.TargetCompleted |
           ExecutionSessionFlags.Cancelled;

        public ulong ExecutionId { get; }
        public int ExecutorActorId { get; }
        public int TargetActorId { get; }

        public ExecutionPose FixedTargetPose { get; }
        public ExecutionPose ExecutorAnchorPose { get; }

        public double StartTimeSec { get; }
        public double ResultTimeSec { get; }

        public float ExecutionDamage { get; }
        public bool TargetWillDie { get; }

        public ExecutionSessionFlags Flags { get; }

        public bool IsResultCommitted =>
            HasFlag(ExecutionSessionFlags.ResultCommitted);

        public bool IsExecutorCompleted =>
            HasFlag(ExecutionSessionFlags.ExecutorCompleted);

        public bool IsTargetCompleted =>
            HasFlag(ExecutionSessionFlags.TargetCompleted);

        public bool IsCancelled =>
            HasFlag(ExecutionSessionFlags.Cancelled);

        public bool IsComplete =>
            IsCancelled || (IsExecutorCompleted && IsTargetCompleted);

        public bool IsCreated => ExecutionId != 0;

        public bool IsActive => IsCreated && !IsComplete;

        public ExecutionSession(
            ulong executionId,
            int executorActorId,
            int targetActorId,
            ExecutionPose fixedTargetPose,
            ExecutionPose executorAnchorPose,
            double startTimeSec,
            double resultTimeSec,
            float executionDamage,
            bool targetWillDie,
            ExecutionSessionFlags flags = ExecutionSessionFlags.None)
        {
            ExecutionId = executionId;
            ExecutorActorId = executorActorId;
            TargetActorId = targetActorId;
            FixedTargetPose = fixedTargetPose;
            ExecutorAnchorPose = executorAnchorPose;
            StartTimeSec = startTimeSec;
            ResultTimeSec = resultTimeSec;
            ExecutionDamage = executionDamage;
            TargetWillDie = targetWillDie;
            Flags = flags;
        }

        public bool TryValidate(out string error)
        {
            if (ExecutionId == 0)
                return Fail("executionId must be non-zero.", out error);

            if (ExecutorActorId == 0 || TargetActorId == 0)
                return Fail("Actor ids must be non-zero.", out error);

            if (ExecutorActorId == TargetActorId)
                return Fail("Executor and target must be different actors.", out error);

            if (!FixedTargetPose.IsFinite || !ExecutorAnchorPose.IsFinite)
                return Fail("Target or anchor pose contains a non-finite value.", out error);

            if (!IsFiniteValue(StartTimeSec) || StartTimeSec < 0d)
                return Fail("Start time must be finite and non-negative.", out error);

            if (!IsFiniteValue(ResultTimeSec) ||
                ResultTimeSec < StartTimeSec)
            {
                return Fail(
                    "Result time must be finite and not precede start time.",
                    out error);
            }

            if (!IsFiniteValue(ExecutionDamage) || ExecutionDamage < 0f)
                return Fail("Execution damage must be finite and non-negative.", out error);

            if ((Flags & ~KnownFlags) != ExecutionSessionFlags.None)
                return Fail("Session contains unknown flags.", out error);

            if (IsTargetCompleted && !IsResultCommitted)
            {
                return Fail(
                    "Target cannot complete before the result is committed.",
                    out error);
            }

            error = null;
            return true;
        }

        public ExecutionSession MarkResultCommitted() =>
            WithFlags(Flags | ExecutionSessionFlags.ResultCommitted);

        public ExecutionSession MarkExecutorCompleted() =>
            WithFlags(Flags | ExecutionSessionFlags.ExecutorCompleted);

        public ExecutionSession MarkTargetCompleted() =>
            WithFlags(Flags | ExecutionSessionFlags.TargetCompleted);

        public ExecutionSession MarkCancelled() =>
            WithFlags(Flags | ExecutionSessionFlags.Cancelled);

        private bool HasFlag(ExecutionSessionFlags flag) =>
            (Flags & flag) != 0;

        private ExecutionSession WithFlags(ExecutionSessionFlags flags) =>
            new ExecutionSession(
                ExecutionId,
                ExecutorActorId,
                TargetActorId,
                FixedTargetPose,
                ExecutorAnchorPose,
                StartTimeSec,
                ResultTimeSec,
                ExecutionDamage,
                TargetWillDie,
                flags);

        private static bool IsFiniteValue(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsFiniteValue(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
