using Character.Config;
using UnityEngine;

namespace Character.Execution
{
    /// <summary>
    /// 只负责无副作用的空间计算，不执行 Physics 查询或状态判断。
    /// </summary>
    public static class ExecutionSpatialValidator
    {
        private const float DirectionEpsilonSqr = 0.000001f;

        public static ExecutionEligibilityResult Evaluate(
            Transform executorRoot,
            Transform targetRoot,
            CharacterCombatConfig config)
        {
            if (executorRoot == null)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.ExecutorMissing);
            }

            if (targetRoot == null)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.TargetMissing);
            }

            if (executorRoot == targetRoot)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.SameActor);
            }

            var executorPose = new ExecutionPose(
                executorRoot.position,
                executorRoot.eulerAngles.y);

            var targetPose = new ExecutionPose(
                targetRoot.position,
                targetRoot.eulerAngles.y);

            return Evaluate(executorPose, targetPose, config);
        }

        public static ExecutionEligibilityResult Evaluate(
            in ExecutionPose executorPose,
            in ExecutionPose targetPose,
            CharacterCombatConfig config)
        {
            if (!IsConfigurationValid(config))
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.InvalidConfiguration);
            }

            if (!executorPose.IsFinite)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.InvalidExecutorPose);
            }

            if (!targetPose.IsFinite)
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.InvalidTargetPose);
            }

            var fixedTargetPose = new ExecutionPose(
                targetPose.Position,
                NormalizeYaw(targetPose.Yaw));

            Quaternion targetYawRotation = Quaternion.Euler(
                0f,
                fixedTargetPose.Yaw,
                0f);

            Vector3 targetToExecutor =
                executorPose.Position - fixedTargetPose.Position;

            float heightDifference = Mathf.Abs(targetToExecutor.y);

            targetToExecutor.y = 0f;
            float horizontalDistance = targetToExecutor.magnitude;

            Vector3 targetForward =
                targetYawRotation * Vector3.forward;

            float targetFrontAngle =
                targetToExecutor.sqrMagnitude > DirectionEpsilonSqr
                    ? Vector3.Angle(targetForward, targetToExecutor)
                    : 0f;

            Vector3 anchorPosition =
                fixedTargetPose.Position +
                targetYawRotation * config.executorAnchorOffset;

            Vector3 anchorToTarget =
                fixedTargetPose.Position - anchorPosition;
            anchorToTarget.y = 0f;

            float anchorYaw =
                anchorToTarget.sqrMagnitude > DirectionEpsilonSqr
                    ? YawFromDirection(anchorToTarget)
                    : NormalizeYaw(fixedTargetPose.Yaw + 180f);

            var executorAnchorPose = new ExecutionPose(
                anchorPosition,
                anchorYaw);

            Vector3 executorToAnchor =
                anchorPosition - executorPose.Position;

            // 当前 CharacterMotor 丢弃 Root Motion 的 Y，
            // 因此 Warp 平移预算只计算水平误差。
            executorToAnchor.y = 0f;
            float warpTranslationError =
                executorToAnchor.magnitude;

            float warpYawError = Mathf.Abs(
                Mathf.DeltaAngle(executorPose.Yaw, anchorYaw));

            if (!executorAnchorPose.IsFinite ||
                !IsFinite(horizontalDistance) ||
                !IsFinite(heightDifference) ||
                !IsFinite(targetFrontAngle) ||
                !IsFinite(warpTranslationError) ||
                !IsFinite(warpYawError))
            {
                return ExecutionEligibilityResult.RejectBeforeSpatial(
                    ExecutionRejectionReason.InvalidSpatialComputation);
            }

            ExecutionRejectionReason rejectionReason =
                ResolveRejectionReason(
                    horizontalDistance,
                    heightDifference,
                    targetFrontAngle,
                    warpTranslationError,
                    warpYawError,
                    config);

            return ExecutionEligibilityResult.FromSpatial(
                rejectionReason,
                fixedTargetPose,
                executorAnchorPose,
                horizontalDistance,
                heightDifference,
                targetFrontAngle,
                warpTranslationError,
                warpYawError);
        }

        private static ExecutionRejectionReason ResolveRejectionReason(
            float horizontalDistance,
            float heightDifference,
            float targetFrontAngle,
            float warpTranslationError,
            float warpYawError,
            CharacterCombatConfig config)
        {
            if (horizontalDistance > config.executionMaxDistance)
                return ExecutionRejectionReason.DistanceExceeded;

            if (targetFrontAngle > config.executionFrontHalfAngle)
                return ExecutionRejectionReason.FrontAngleExceeded;

            if (heightDifference > config.executionMaxHeightDifference)
                return ExecutionRejectionReason.HeightDifferenceExceeded;

            if (warpTranslationError >
                config.executionMaxWarpTranslation)
            {
                return ExecutionRejectionReason.WarpTranslationExceeded;
            }

            if (warpYawError > config.executionMaxWarpYaw)
                return ExecutionRejectionReason.WarpYawExceeded;

            return ExecutionRejectionReason.None;
        }

        private static bool IsConfigurationValid(
            CharacterCombatConfig config)
        {
            if (config == null)
                return false;

            return
                IsNonNegativeFinite(config.executionMaxDistance) &&
                IsInAngleRange(config.executionFrontHalfAngle) &&
                IsNonNegativeFinite(
                    config.executionMaxHeightDifference) &&
                IsFinite(config.executorAnchorOffset) &&
                IsNonNegativeFinite(
                    config.executionMaxWarpTranslation) &&
                IsInAngleRange(config.executionMaxWarpYaw);
        }

        private static bool IsInAngleRange(float value)
        {
            return IsFinite(value) &&
                   value >= 0f &&
                   value <= 180f;
        }

        private static bool IsNonNegativeFinite(float value)
        {
            return IsFinite(value) && value >= 0f;
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

        private static float NormalizeYaw(float yaw)
        {
            return Mathf.DeltaAngle(0f, yaw);
        }

        private static float YawFromDirection(Vector3 direction)
        {
            return NormalizeYaw(
                Mathf.Atan2(direction.x, direction.z) *
                Mathf.Rad2Deg);
        }
    }
}