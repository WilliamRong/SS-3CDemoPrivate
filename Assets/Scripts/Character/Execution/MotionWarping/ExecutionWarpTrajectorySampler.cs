using System.Collections.Generic;
using Character.Config;
using UnityEngine;

namespace Character.Execution
{

    public readonly struct ExecutionWarpInputSample
    {
        public float NormalizedTime { get; }
        public Vector3 DeltaPosition { get; }
        public float DeltaYaw { get; }

        public ExecutionWarpInputSample(
            float normalizedTime,
            Vector3 deltaPosition,
            float deltaYaw)
        {
            NormalizedTime = normalizedTime;
            DeltaPosition = deltaPosition;
            DeltaYaw = deltaYaw;
        }
    }

    public readonly struct ExecutionWarpTrajectoryFrame
    {
        public int Index { get; }
        public float NormalizedTime { get; }
        public ExecutionPose PoseBefore { get; }
        public ExecutionPose PoseAfter { get; }
        public Vector3 OriginalDeltaPosition { get; }
        public float OriginalDeltaYaw { get; }
        public Vector3 CorrectedDeltaPosition { get; }
        public float CorrectedDeltaYaw { get; }
        public Vector3 PredictedRemainingPositionError { get; }
        public float PredictedRemainingYawError { get; }
        public Vector3 ActualRemainingPositionError { get; }
        public float ActualRemainingYawError { get; }
        public Vector3 RequestedTranslationCorrection { get; }
        public float RequestedYawCorrection { get; }
        public bool IsWarpWindowComplete { get; }

        public ExecutionWarpTrajectoryFrame(
            int index,
            float normalizedTime,
            in ExecutionPose poseBefore,
            in ExecutionPose poseAfter,
            Vector3 originalDeltaPosition,
            float originalDeltaYaw,
            Vector3 correctedDeltaPosition,
            float correctedDeltaYaw,
            Vector3 predictedRemainingPositionError,
            float predictedRemainingYawError,
            Vector3 actualRemainingPositionError,
            float actualRemainingYawError,
            Vector3 requestedTranslationCorrection,
            float requestedYawCorrection,
            bool isWarpWindowComplete)
        {
            Index = index;
            NormalizedTime = normalizedTime;
            PoseBefore = poseBefore;
            PoseAfter = poseAfter;
            OriginalDeltaPosition = originalDeltaPosition;
            OriginalDeltaYaw = originalDeltaYaw;
            CorrectedDeltaPosition = correctedDeltaPosition;
            CorrectedDeltaYaw = correctedDeltaYaw;
            PredictedRemainingPositionError = predictedRemainingPositionError;
            PredictedRemainingYawError = predictedRemainingYawError;
            ActualRemainingPositionError = actualRemainingPositionError;
            ActualRemainingYawError = actualRemainingYawError;
            RequestedTranslationCorrection = requestedTranslationCorrection;
            RequestedYawCorrection = requestedYawCorrection;
            IsWarpWindowComplete = isWarpWindowComplete;
        }
    }

    public sealed class ExecutionWarpTrajectory
    {
        public ExecutionPose AnchorPose { get; }
        public ExecutionPose InitialPose { get; }
        public ExecutionPose FinalPose { get; }
        public IReadOnlyList<ExecutionWarpTrajectoryFrame> Frames { get; }
        public Vector3 FinalPositionError { get; }
        public float FinalYawError { get; }

        public ExecutionWarpTrajectory(
            in ExecutionPose anchorPose,
            in ExecutionPose initialPose,
            in ExecutionPose finalPose,
            IReadOnlyList<ExecutionWarpTrajectoryFrame> frames,
            Vector3 finalPositionError,
            float finalYawError)
        {
            AnchorPose = anchorPose;
            InitialPose = initialPose;
            FinalPose = finalPose;
            Frames = frames;
            FinalPositionError = finalPositionError;
            FinalYawError = finalYawError;
        }
    }

    public static class ExecutionWarpTrajectorySampler
    {
        public static bool TrySample(
                  in ExecutionPose anchorPose,
                  in ExecutionPose initialExecutorPose,
                  CharacterCombatConfig config,
                  IReadOnlyList<ExecutionWarpInputSample> inputSamples,
                  out ExecutionWarpTrajectory trajectory)
        {
            trajectory = null;

            if (inputSamples == null)
                return false;

            if (!ExecutionWarpSolver.TryCreate(
                    anchorPose,
                    initialExecutorPose,
                    config,
                    out ExecutionWarpSolver solver))
            {
                return false;
            }

            var frames = new List<ExecutionWarpTrajectoryFrame>(
                inputSamples.Count);

            ExecutionPose currentPose = initialExecutorPose;

            for (int i = 0; i < inputSamples.Count; i++)
            {
                ExecutionWarpInputSample sample = inputSamples[i];

                if (!IsFinite(sample.NormalizedTime) ||
                    !IsFinite(sample.DeltaPosition) ||
                    !IsFinite(sample.DeltaYaw))
                {
                    return false;
                }

                ExecutionPose poseBefore = currentPose;

                if (!solver.TryWarp(
                        currentPose,
                        sample.DeltaPosition,
                        sample.DeltaYaw,
                        sample.NormalizedTime,
                        out Vector3 correctedDeltaPosition,
                        out float correctedDeltaYaw))
                {
                    return false;
                }

                Vector3 nextPosition =
                    currentPose.Position +
                    ExecutionWarpSolver.ProjectToHorizontalPlane(
                        correctedDeltaPosition);

                float nextYaw = NormalizeYaw(
                    currentPose.Yaw + correctedDeltaYaw);

                currentPose = new ExecutionPose(nextPosition, nextYaw);

                if (!solver.RefreshRemainingError(currentPose))
                    return false;

                frames.Add(new ExecutionWarpTrajectoryFrame(
                    i,
                    sample.NormalizedTime,
                    poseBefore,
                    currentPose,
                    sample.DeltaPosition,
                    sample.DeltaYaw,
                    correctedDeltaPosition,
                    correctedDeltaYaw,
                    solver.PredictedRemainingPositionError,
                    solver.PredictedRemainingYawError,
                    solver.ActualRemainingPositionError,
                    solver.ActualRemainingYawError,
                    solver.RequestedTranslationCorrection,
                    solver.RequestedYawCorrection,
                    solver.IsWarpWindowComplete));
            }

            trajectory = new ExecutionWarpTrajectory(
                anchorPose,
                initialExecutorPose,
                currentPose,
                frames,
                solver.ActualRemainingPositionError,
                solver.ActualRemainingYawError);

            return true;
        }

        private static float NormalizeYaw(float yaw)
        {
            yaw %= 360f;
            if (yaw < 0f)
                yaw += 360f;

            return yaw;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

    }
}
