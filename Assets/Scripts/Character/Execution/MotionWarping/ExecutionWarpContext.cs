using Character.Config;
using UnityEngine;

namespace Character.Execution
{
    public sealed class ExecutionWarpContext
    {
        private readonly ExecutionWarpSolver _solver;

        public ulong ExecutionId { get; }

        public ExecutionPose AnchorPose => _solver.AnchorPose;
        public float WindowStartNormalized => _solver.WindowStartNormalized;
        public float WindowEndNormalized => _solver.WindowEndNormalized;
        public float MaxInitialTranslationError => _solver.MaxInitialTranslationError;
        public float MaxInitialYawError => _solver.MaxInitialYawError;
        public bool IsActive => _solver.IsActive;
        public float LastSampleNormalized => _solver.LastSampleNormalized;
        public float LastCumulativeWeight => _solver.LastCumulativeWeight;
        public Vector3 LastOriginalDeltaPosition => _solver.LastOriginalDeltaPosition;
        public float LastOriginalDeltaYaw => _solver.LastOriginalDeltaYaw;
        public Vector3 LastCorrectedDeltaPosition => _solver.LastCorrectedDeltaPosition;
        public float LastCorrectedDeltaYaw => _solver.LastCorrectedDeltaYaw;
        public Vector3 RequestedTranslationCorrection => _solver.RequestedTranslationCorrection;
        public float RequestedYawCorrection => _solver.RequestedYawCorrection;
        public bool IsWarpWindowComplete => _solver.IsWarpWindowComplete;

        public Vector3 PredictedRemainingPositionError =>
            _solver.PredictedRemainingPositionError;

        public float PredictedRemainingYawError =>
            _solver.PredictedRemainingYawError;

        public Vector3 ActualRemainingPositionError =>
            _solver.ActualRemainingPositionError;

        public float ActualRemainingYawError =>
            _solver.ActualRemainingYawError;

        public Vector3 LastMotorPositionShortfall =>
            _solver.LastMotorPositionShortfall;

        public float LastMotorYawShortfall =>
            _solver.LastMotorYawShortfall;

        public bool HasMotorApplicationSample =>
            _solver.HasMotorApplicationSample;

        public Vector3 RemainingPositionError =>
            _solver.RemainingPositionError;

        public float RemainingYawError =>
            _solver.RemainingYawError;

        private ExecutionWarpContext(
            ulong executionId,
            ExecutionWarpSolver solver)
        {
            ExecutionId = executionId;
            _solver = solver;
        }

        public static bool TryCreate(
            in ExecutionSession session,
            int ownerActorId,
            CharacterCombatConfig config,
            in ExecutionPose initialExecutorPose,
            out ExecutionWarpContext context)
        {
            context = null;

            if (!session.TryValidate(out _))
                return false;

            if (!session.IsActive)
                return false;

            if (session.ExecutorActorId != ownerActorId)
                return false;

            if (!ExecutionWarpSolver.TryCreate(
                    session.ExecutorAnchorPose,
                    initialExecutorPose,
                    config,
                    out ExecutionWarpSolver solver))
            {
                return false;
            }

            context = new ExecutionWarpContext(
                session.ExecutionId,
                solver);

            return true;
        }

        public bool TryWarp(
            in ExecutionPose currentExecutorPose,
            Vector3 originalDeltaPosition,
            float originalDeltaYaw,
            float normalizedTime,
            out Vector3 correctedDeltaPosition,
            out float correctedDeltaYaw)
        {
            return _solver.TryWarp(
                currentExecutorPose,
                originalDeltaPosition,
                originalDeltaYaw,
                normalizedTime,
                out correctedDeltaPosition,
                out correctedDeltaYaw);
        }

        public bool RefreshRemainingError(
            in ExecutionPose actualExecutorPose)
        {
            return _solver.RefreshRemainingError(actualExecutorPose);
        }

        public bool TryEnd(ulong executionId)
        {
            if (executionId == 0 || executionId != ExecutionId)
                return false;

            _solver.End();
            return true;
        }
    }
}