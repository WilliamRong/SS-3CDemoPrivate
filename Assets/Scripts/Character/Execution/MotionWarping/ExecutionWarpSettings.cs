using Character.Config;
using UnityEngine;

namespace Character.Execution
{
    public enum ExecutionWarpParticipant : byte
    {
        Executor = 0,
        Target = 1,
    }

    /// <summary>
    /// 与参与者身份无关的 Motion Warping 参数。
    /// Solver 只消费该值对象，不直接读取某一角色专用配置字段。
    /// </summary>
    public readonly struct ExecutionWarpSettings
    {
        public float WindowStartNormalized { get; }
        public float WindowEndNormalized { get; }
        public float MaxInitialTranslationError { get; }
        public float MaxInitialYawError { get; }
        public AnimationCurve WeightCurve { get; }

        public ExecutionWarpSettings(
            float windowStartNormalized,
            float windowEndNormalized,
            float maxInitialTranslationError,
            float maxInitialYawError,
            AnimationCurve weightCurve)
        {
            WindowStartNormalized = windowStartNormalized;
            WindowEndNormalized = windowEndNormalized;
            MaxInitialTranslationError = maxInitialTranslationError;
            MaxInitialYawError = maxInitialYawError;
            WeightCurve = weightCurve;
        }

        public static bool TryFromExecutor(
            CharacterCombatConfig config,
            out ExecutionWarpSettings settings)
        {
            settings = default;

            if (config == null)
                return false;

            settings = new ExecutionWarpSettings(
                config.executionWarpWindowStartNormalized,
                config.executionWarpWindowEndNormalized,
                config.executionMaxWarpTranslation,
                config.executionMaxWarpYaw,
                config.executionWarpCurve);

            return ExecutionWarpSolver.IsValidSettings(settings);
        }

        public static bool TryFromTarget(
            CharacterCombatConfig config,
            out ExecutionWarpSettings settings)
        {
            settings = default;

            if (config == null)
                return false;

            settings = new ExecutionWarpSettings(
                config.executedWarpWindowStartNormalized,
                config.executedWarpWindowEndNormalized,
                config.executedMaxWarpTranslation,
                config.executedMaxWarpYaw,
                config.executedWarpCurve);

            return ExecutionWarpSolver.IsValidSettings(settings);
        }
    }

    public static class ExecutionWarpAnchorResolver
    {
        public static bool TryResolveTargetAnchor(
            in ExecutionPose fixedTargetPose,
            CharacterCombatConfig config,
            bool lethal,
            out ExecutionPose anchorPose)
        {
            anchorPose = default;

            if (!fixedTargetPose.IsFinite || config == null)
                return false;

            Vector3 localOffset = lethal
                ? config.executedDeathAnchorOffset
                : config.executedAnchorOffset;

            float localYawOffset = lethal
                ? config.executedDeathAnchorYawOffset
                : config.executedAnchorYawOffset;

            if (!IsFinite(localOffset) || !IsFinite(localYawOffset))
                return false;

            Quaternion referenceRotation =
                Quaternion.Euler(0f, fixedTargetPose.Yaw, 0f);

            Vector3 worldPosition =
                fixedTargetPose.Position +
                referenceRotation * localOffset;

            float worldYaw = NormalizeYaw(
                fixedTargetPose.Yaw + localYawOffset);

            anchorPose = new ExecutionPose(
                worldPosition,
                worldYaw);

            return anchorPose.IsFinite;
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
