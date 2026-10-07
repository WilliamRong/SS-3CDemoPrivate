using Character.Config;
using UnityEngine;

namespace Character.Execution
{
    /// <summary>
    /// Runtime 和 Editor 共用的 Motion Warping 纯计算核心。
    /// 这里不能引用 UnityEditor，也不应该作为 MonoBehaviour 挂到场景对象上。
    /// </summary>
    public sealed class ExecutionWarpSolver
    {

        private const float WeightEpsilon = 0.000001f;

        private readonly AnimationCurve _weightCurve;
        private bool _isWarpWindowFinalized;
        // 表示已经生成修正 Delta，但该 Delta 还在等待 Motor 实际消费。
        private bool _hasPendingPrediction;

        public ExecutionPose AnchorPose { get; }
        public float WindowStartNormalized { get; }
        public float WindowEndNormalized { get; }
        public float MaxInitialTranslationError { get; }
        public float MaxInitialYawError { get; }
        public bool IsActive { get; private set; }
        public float LastSampleNormalized { get; private set; }
        public float LastCumulativeWeight { get; private set; }
        public Vector3 LastOriginalDeltaPosition { get; private set; }
        public float LastOriginalDeltaYaw { get; private set; }
        public Vector3 LastCorrectedDeltaPosition { get; private set; }
        public float LastCorrectedDeltaYaw { get; private set; }
        public Vector3 RequestedTranslationCorrection { get; private set; }
        public float RequestedYawCorrection { get; private set; }
        public bool IsWarpWindowComplete => _isWarpWindowFinalized;



        /// <summary>
        /// 假设最近一次修正 Delta 被 Motor 完整执行后，预计剩余的位置误差。
        /// 它描述当前排队中的 Root Motion 结果。
        /// </summary>
        public Vector3 PredictedRemainingPositionError { get; private set; }

        /// <summary>
        /// 假设最近一次修正 Delta 被完整执行后，预计剩余的 Yaw 误差。
        /// </summary>
        public float PredictedRemainingYawError { get; private set; }

        /// <summary>
        /// CharacterController.Move 执行后，角色真实位置到锚点的剩余误差。
        /// 运行时判断和最终诊断应优先读取这个值。
        /// </summary>
        public Vector3 ActualRemainingPositionError { get; private set; }

        /// <summary>
        /// Motor 执行后，角色真实 Yaw 到锚点的剩余误差。
        /// </summary>
        public float ActualRemainingYawError { get; private set; }

        /// <summary>
        /// 最近一次 Motor 实际执行结果相比预测结果少完成的位置。
        /// 墙体阻挡时通常会出现非零值。
        /// </summary>
        public Vector3 LastMotorPositionShortfall { get; private set; }

        /// <summary>
        /// 最近一次 Motor 实际执行结果相比预测结果少完成的 Yaw。
        /// 当前 Motor 直接旋转角色，因此正常情况下应接近零。
        /// </summary>
        public float LastMotorYawShortfall { get; private set; }

        /// <summary>
        /// 是否已经取得至少一次“预测结果与实际结果”的有效对比。
        /// </summary>
        public bool HasMotorApplicationSample { get; private set; }

        /// <summary>
        /// 向后兼容的真实残差入口。
        /// </summary>
        public Vector3 RemainingPositionError =>
            ActualRemainingPositionError;

        /// <summary>
        /// 向后兼容的真实 Yaw 残差入口。
        /// </summary>
        public float RemainingYawError =>
            ActualRemainingYawError;


        private ExecutionWarpSolver(
                   in ExecutionPose anchorPose,
                   in ExecutionPose initialExecutorPose,
                   in ExecutionWarpSettings settings,
                   AnimationCurve weightCurve)
        {
            AnchorPose = anchorPose;
            WindowStartNormalized = settings.WindowStartNormalized;
            WindowEndNormalized = settings.WindowEndNormalized;
            MaxInitialTranslationError = settings.MaxInitialTranslationError;
            MaxInitialYawError = settings.MaxInitialYawError;
            _weightCurve = weightCurve;
            IsActive = true;

            RefreshRemainingError(initialExecutorPose);
            PredictedRemainingPositionError = ActualRemainingPositionError;
            PredictedRemainingYawError = ActualRemainingYawError;
        }

        public static bool TryCreate(
           in ExecutionPose anchorPose,
           in ExecutionPose initialExecutorPose,
           CharacterCombatConfig config,
           out ExecutionWarpSolver solver)
        {
            if (!ExecutionWarpSettings.TryFromExecutor(
                    config,
                    out ExecutionWarpSettings settings))
            {
                solver = null;
                return false;
            }

            return TryCreate(
                anchorPose,
                initialExecutorPose,
                settings,
                out solver);
        }

        public static bool TryCreate(
           in ExecutionPose anchorPose,
           in ExecutionPose initialParticipantPose,
           in ExecutionWarpSettings settings,
           out ExecutionWarpSolver solver)
        {
            solver = null;

            if (!initialParticipantPose.IsFinite || !anchorPose.IsFinite)
                return false;

            if (!IsValidSettings(settings))
                return false;

            if (!IsInitialPoseWithinBudget(
                    anchorPose,
                    initialParticipantPose,
                    settings))
                return false;

            var curve = new AnimationCurve(settings.WeightCurve.keys)
            {
                preWrapMode = settings.WeightCurve.preWrapMode,
                postWrapMode = settings.WeightCurve.postWrapMode,
            };

            solver = new ExecutionWarpSolver(
                anchorPose,
                initialParticipantPose,
                settings,
                curve);

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
            correctedDeltaPosition = originalDeltaPosition;
            correctedDeltaYaw = Mathf.DeltaAngle(0f, originalDeltaYaw);

            if (!IsActive ||
                !currentExecutorPose.IsFinite ||
                !IsFinite(originalDeltaPosition) ||
                !IsFinite(originalDeltaYaw) ||
                !IsFinite(normalizedTime))
            {
                return false;
            }

            float sampleNormalized = Mathf.Max(
                LastSampleNormalized,
                Mathf.Clamp01(normalizedTime));

            LastSampleNormalized = sampleNormalized;
            LastOriginalDeltaPosition = originalDeltaPosition;
            LastOriginalDeltaYaw = correctedDeltaYaw;

            float targetWeight = ResolveTargetCumulativeWeight(sampleNormalized);
            float weightDelta = targetWeight - LastCumulativeWeight;

            bool shouldApplyCorrection = false;
            float captureRatio = 0f;

            if (weightDelta > WeightEpsilon)
            {
                float unconsumedWeight = 1f - LastCumulativeWeight;
                captureRatio = unconsumedWeight > WeightEpsilon
                    ? weightDelta / unconsumedWeight
                    : 0f;

                shouldApplyCorrection = captureRatio > WeightEpsilon;
                LastCumulativeWeight = targetWeight;
            }
            else if (LastCumulativeWeight >= 1f - WeightEpsilon &&
                     sampleNormalized >= WindowStartNormalized &&
                     !_isWarpWindowFinalized)
            {
                captureRatio = 1f;
                shouldApplyCorrection = true;
            }

            if (shouldApplyCorrection)
            {
                ApplyPositionCorrection(
                    currentExecutorPose,
                    originalDeltaPosition,
                    captureRatio,
                    ref correctedDeltaPosition);

                ApplyYawCorrection(
                    currentExecutorPose,
                    correctedDeltaYaw,
                    captureRatio,
                    ref correctedDeltaYaw);
            }

            if (sampleNormalized >= WindowEndNormalized)
            {
                LastCumulativeWeight = 1f;
                _isWarpWindowFinalized = true;
            }

            LastCorrectedDeltaPosition = correctedDeltaPosition;
            LastCorrectedDeltaYaw = correctedDeltaYaw;

            UpdatePredictedRemainingError(
                currentExecutorPose,
                correctedDeltaPosition,
                correctedDeltaYaw);

            return true;
        }

        public bool RefreshRemainingError(in ExecutionPose actualExecutorPose)
        {
            if (!IsActive || !actualExecutorPose.IsFinite)
                return false;

            Vector3 actualPositionError =
                ProjectToHorizontalPlane(AnchorPose.Position - actualExecutorPose.Position);

            float actualYawError =
                Mathf.DeltaAngle(actualExecutorPose.Yaw, AnchorPose.Yaw);

            if (!IsFinite(actualPositionError) || !IsFinite(actualYawError))
                return false;

            if (_hasPendingPrediction)
            {
                LastMotorPositionShortfall =
                    actualPositionError - PredictedRemainingPositionError;

                LastMotorYawShortfall =
                    Mathf.DeltaAngle(PredictedRemainingYawError, actualYawError);

                HasMotorApplicationSample = true;
                _hasPendingPrediction = false;
            }

            ActualRemainingPositionError = actualPositionError;
            ActualRemainingYawError = actualYawError;

            return true;
        }

        public void End()
        {
            IsActive = false;
            _hasPendingPrediction = false;
        }

        private void ApplyPositionCorrection(
            in ExecutionPose currentPose,
            Vector3 originalDelta,
            float captureRatio,
            ref Vector3 correctedDelta)
        {
            Vector3 predictedPosition =
                currentPose.Position + ProjectToHorizontalPlane(originalDelta);

            Vector3 remainingError =
                ProjectToHorizontalPlane(AnchorPose.Position - predictedPosition);

            Vector3 requestedCorrection = remainingError * captureRatio;

            correctedDelta += requestedCorrection;
            RequestedTranslationCorrection += requestedCorrection;
        }

        private void ApplyYawCorrection(
            in ExecutionPose currentPose,
            float originalDeltaYaw,
            float captureRatio,
            ref float correctedDeltaYaw)
        {
            float predictedYaw = currentPose.Yaw + originalDeltaYaw;
            float remainingError = Mathf.DeltaAngle(predictedYaw, AnchorPose.Yaw);
            float requestedCorrection = remainingError * captureRatio;

            correctedDeltaYaw += requestedCorrection;
            RequestedYawCorrection += requestedCorrection;
        }

        private float ResolveTargetCumulativeWeight(float normalizedTime)
        {
            if (normalizedTime < WindowStartNormalized)
                return LastCumulativeWeight;

            if (normalizedTime >= WindowEndNormalized)
                return 1f;

            float windowDuration = WindowEndNormalized - WindowStartNormalized;

            if (windowDuration <= WeightEpsilon)
                return 1f;

            float windowTime = Mathf.Clamp01(
                (normalizedTime - WindowStartNormalized) / windowDuration);

            float evaluated = _weightCurve.Evaluate(windowTime);

            if (!IsFinite(evaluated))
                return LastCumulativeWeight;

            return Mathf.Clamp(evaluated, LastCumulativeWeight, 1f);
        }

        private void UpdatePredictedRemainingError(
            in ExecutionPose currentPose,
            Vector3 correctedDeltaPosition,
            float correctedDeltaYaw)
        {
            Vector3 predictedPosition =
                currentPose.Position + ProjectToHorizontalPlane(correctedDeltaPosition);

            PredictedRemainingPositionError =
                ProjectToHorizontalPlane(AnchorPose.Position - predictedPosition);

            float predictedYaw = currentPose.Yaw + correctedDeltaYaw;

            PredictedRemainingYawError =
                Mathf.DeltaAngle(predictedYaw, AnchorPose.Yaw);

            _hasPendingPrediction = true;
        }

        public static bool IsValidConfiguration(CharacterCombatConfig config)
        {
            return ExecutionWarpSettings.TryFromExecutor(
                config,
                out _);
        }

        public static bool IsValidSettings(
            in ExecutionWarpSettings settings)
        {
            if (!IsFinite(settings.WindowStartNormalized) ||
                !IsFinite(settings.WindowEndNormalized))
            {
                return false;
            }

            if (settings.WindowStartNormalized < 0f ||
                settings.WindowStartNormalized > 1f ||
                settings.WindowEndNormalized < settings.WindowStartNormalized ||
                settings.WindowEndNormalized > 1f)
            {
                return false;
            }

            if (!IsFinite(settings.MaxInitialTranslationError) ||
                settings.MaxInitialTranslationError < 0f)
            {
                return false;
            }

            if (!IsFinite(settings.MaxInitialYawError) ||
                settings.MaxInitialYawError < 0f ||
                settings.MaxInitialYawError > 180f)
            {
                return false;
            }

            return CharacterCombatConfig.IsExecutionWarpCurveValid(
                settings.WeightCurve);
        }

        public static bool IsInitialPoseWithinBudget(
            in ExecutionPose anchorPose,
            in ExecutionPose initialExecutorPose,
            CharacterCombatConfig config)
        {
            return ExecutionWarpSettings.TryFromExecutor(
                       config,
                       out ExecutionWarpSettings settings) &&
                   IsInitialPoseWithinBudget(
                       anchorPose,
                       initialExecutorPose,
                       settings);
        }

        public static bool IsInitialPoseWithinBudget(
            in ExecutionPose anchorPose,
            in ExecutionPose initialParticipantPose,
            in ExecutionWarpSettings settings)
        {
            if (!IsValidSettings(settings))
                return false;

            Vector3 positionError =
                ProjectToHorizontalPlane(
                    anchorPose.Position -
                    initialParticipantPose.Position);

            float translationError = positionError.magnitude;

            float yawError = Mathf.Abs(Mathf.DeltaAngle(
                initialParticipantPose.Yaw,
                anchorPose.Yaw));

            return IsFinite(translationError) &&
                   translationError <=
                       settings.MaxInitialTranslationError &&
                   IsFinite(yawError) &&
                   yawError <= settings.MaxInitialYawError;
        }

        public static Vector3 ProjectToHorizontalPlane(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
