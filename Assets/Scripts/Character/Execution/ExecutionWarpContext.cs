using Character.Config;
using UnityEngine;

namespace Character.Execution
{
    /// <summary>
    /// 保存一次处决会话的 Motion Warping 状态，根据玩法时间把 Animator Root Motion
    /// 修正到会话锚点，并在 Motor 移动后记录碰撞影响下的真实剩余误差。
    /// 创建链：ExecutingState.TryPrepare -> TryCreate。
    /// 逐帧链：AnimatorRootMotionRelay -> PlayerController -> ExecutingState -> TryWarp
    /// -> CharacterMotor -> CharacterController.Move -> RefreshRemainingError。
    /// </summary>
    public sealed class ExecutionWarpContext
    {
        private const float WeightEpsilon = 0.000001f;

        private readonly AnimationCurve _weightCurve;
        private bool _isWarpWindowFinalized;
        // 表示已经生成修正 Delta，但该 Delta 还在等待 Motor 实际消费。
        private bool _hasPendingPrediction;

        public ulong ExecutionId { get; }
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



        /// <summary>
        /// 使用已经通过校验的会话、初始姿态和配置创建会话专属状态。
        /// </summary>
        private ExecutionWarpContext(
            in ExecutionSession session,
            in ExecutionPose initialExecutorPose,
            CharacterCombatConfig config,
            AnimationCurve weightCurve)
        {
            ExecutionId = session.ExecutionId;
            AnchorPose = session.ExecutorAnchorPose;
            WindowStartNormalized =
                config.executionWarpWindowStartNormalized;
            WindowEndNormalized =
                config.executionWarpWindowEndNormalized;
            MaxInitialTranslationError =
                config.executionMaxWarpTranslation;
            MaxInitialYawError =
                config.executionMaxWarpYaw;
            _weightCurve = weightCurve;
            IsActive = true;

            // 创建时还没有待执行 Delta，因此先初始化真实残差。
            RefreshRemainingError(initialExecutorPose);

            // 在第一帧预测产生前，让预测值与初始真实值保持一致。
            PredictedRemainingPositionError =
                ActualRemainingPositionError;

            PredictedRemainingYawError =
                ActualRemainingYawError;
        }

        /// <summary>
        /// 校验会话、所有者、配置和当前姿态，并创建会话专属 Warp 上下文。
        /// </summary>
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

            if (config == null)
                return false;

            if (!initialExecutorPose.IsFinite)
                return false;

            if (!IsValidConfiguration(config))
                return false;

            if (!IsInitialPoseWithinBudget(
                    session.ExecutorAnchorPose,
                    initialExecutorPose,
                    config))
            {
                return false;
            }

            var curve = new AnimationCurve(
                config.executionWarpCurve.keys)
            {
                preWrapMode =
                    config.executionWarpCurve.preWrapMode,
                postWrapMode =
                    config.executionWarpCurve.postWrapMode,
            };

            context = new ExecutionWarpContext(
                session,
                initialExecutorPose,
                config,
                curve);

            return true;
        }

        /// <summary>
        /// 根据当前姿态、原始 Root Motion 和玩法时间计算本帧修正 Delta，不直接移动 Transform。
        /// </summary>
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

            if (!IsActive)
                return false;

            if (!currentExecutorPose.IsFinite)
                return false;

            if (!IsFinite(originalDeltaPosition))
                return false;

            if (!IsFinite(originalDeltaYaw))
                return false;

            if (!IsFinite(normalizedTime))
                return false;

            float sampleNormalized = Mathf.Max(
                LastSampleNormalized,
                Mathf.Clamp01(normalizedTime));

            LastSampleNormalized = sampleNormalized;
            LastOriginalDeltaPosition = originalDeltaPosition;
            LastOriginalDeltaYaw = correctedDeltaYaw;

            float targetWeight =
                ResolveTargetCumulativeWeight(sampleNormalized);

            float weightDelta =
                targetWeight - LastCumulativeWeight;

            bool shouldApplyCorrection = false;
            float captureRatio = 0f;

            if (weightDelta > WeightEpsilon)
            {
                float unconsumedWeight =
                    1f - LastCumulativeWeight;

                captureRatio = unconsumedWeight > WeightEpsilon
                    ? weightDelta / unconsumedWeight
                    : 0f;

                shouldApplyCorrection =
                    captureRatio > WeightEpsilon;

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

            LastCorrectedDeltaPosition =
                correctedDeltaPosition;
            LastCorrectedDeltaYaw =
                correctedDeltaYaw;

            UpdatePredictedRemainingError(
                currentExecutorPose,
                correctedDeltaPosition,
                correctedDeltaYaw);

            return true;
        }

        /// <summary>
        /// 在 Motor 移动后使用碰撞处理过的真实姿态刷新剩余位置和 Yaw 误差。
        /// </summary>
        public bool RefreshRemainingError(
            in ExecutionPose actualExecutorPose)
        {
            if (!IsActive)
                return false;

            if (!actualExecutorPose.IsFinite)
                return false;

            Vector3 actualPositionError = ProjectToHorizontalPlane(AnchorPose.Position - actualExecutorPose.Position);

            float actualYawError = Mathf.DeltaAngle(actualExecutorPose.Yaw, AnchorPose.Yaw);

            // 极端坐标运算仍可能溢出，因此写入前再次检查。
            if (!IsFinite(actualPositionError) ||
                !IsFinite(actualYawError))
            {
                return false;
            }


            // 如果存在等待执行的预测，这次真实姿态就是它的执行结果。
            if (_hasPendingPrediction)
            {
                // 实际残差减预测残差，得到没有完成的位移。
                LastMotorPositionShortfall = actualPositionError - PredictedRemainingPositionError;

                // 使用最短角度计算旋转没有完成的部分。
                LastMotorYawShortfall = Mathf.DeltaAngle(PredictedRemainingYawError, actualYawError);

                HasMotorApplicationSample = true;

                _hasPendingPrediction = false;

            }


            ActualRemainingPositionError =
                actualPositionError;

            ActualRemainingYawError =
                actualYawError;

            return true;
        }

        /// <summary>
        /// 结束指定会话的 Warp 上下文，拒绝不匹配的会话编号。
        /// </summary>
        public bool TryEnd(ulong executionId)
        {
            if (executionId == 0 || executionId != ExecutionId)
                return false;

            IsActive = false;

            _hasPendingPrediction = false;
            
            return true;
        }

        /// <summary>
        /// 根据原始动画预测落点与锚点的误差，为本帧位移 Delta 追加平移修正。
        /// </summary>
        private void ApplyPositionCorrection(
            in ExecutionPose currentPose,
            Vector3 originalDelta,
            float captureRatio,
            ref Vector3 correctedDelta)
        {
            Vector3 predictedPosition =
                currentPose.Position +
                ProjectToHorizontalPlane(originalDelta);

            Vector3 remainingError = ProjectToHorizontalPlane(
                AnchorPose.Position - predictedPosition);

            Vector3 requestedCorrection =
                remainingError * captureRatio;

            correctedDelta += requestedCorrection;
            RequestedTranslationCorrection +=
                requestedCorrection;
        }

        /// <summary>
        /// 根据原始动画预测朝向与锚点的最短角度误差，为本帧 Yaw Delta 追加旋转修正。
        /// </summary>
        private void ApplyYawCorrection(
            in ExecutionPose currentPose,
            float originalDeltaYaw,
            float captureRatio,
            ref float correctedDeltaYaw)
        {
            float predictedYaw =
                currentPose.Yaw + originalDeltaYaw;

            float remainingError = Mathf.DeltaAngle(
                predictedYaw,
                AnchorPose.Yaw);

            float requestedCorrection =
                remainingError * captureRatio;

            correctedDeltaYaw += requestedCorrection;
            RequestedYawCorrection += requestedCorrection;
        }

        /// <summary>
        /// 把处决玩法时间映射到 Warp Window，并返回单调递增的累计修正权重。
        /// </summary>
        private float ResolveTargetCumulativeWeight(float normalizedTime)
        {
            if (normalizedTime < WindowStartNormalized)
                return LastCumulativeWeight;

            if (normalizedTime >= WindowEndNormalized)
                return 1f;

            float windowDuration =
                WindowEndNormalized - WindowStartNormalized;

            if (windowDuration <= WeightEpsilon)
                return 1f;

            float windowTime = Mathf.Clamp01(
                (normalizedTime - WindowStartNormalized) /
                windowDuration);

            float evaluated =
                _weightCurve.Evaluate(windowTime);

            if (!IsFinite(evaluated))
                return LastCumulativeWeight;

            return Mathf.Clamp(
                evaluated,
                LastCumulativeWeight,
                1f);
        }

        /// <summary>
        /// 记录修正 Delta 被完整执行时的预测残差，之后由真实移动结果覆盖。
        /// </summary>
        private void UpdatePredictedRemainingError(
            in ExecutionPose currentPose,
            Vector3 correctedDeltaPosition,
            float correctedDeltaYaw)
        {
            // 假设修正 Delta 会被 Motor 完整执行。
            Vector3 predictedPosition =
                currentPose.Position +
                ProjectToHorizontalPlane(correctedDeltaPosition);

            // 记录排队中 Delta 执行后的预测位置残差。
            PredictedRemainingPositionError = ProjectToHorizontalPlane(
                AnchorPose.Position - predictedPosition);

            // 预测修正 Delta 执行后的角色朝向。
            float predictedYaw =
                currentPose.Yaw + correctedDeltaYaw;

            // 记录预测 Yaw 残差。
            PredictedRemainingYawError = Mathf.DeltaAngle(
                predictedYaw,
                AnchorPose.Yaw);

            // 下一次 RefreshRemainingError 应把真实结果与本预测配对。
            _hasPendingPrediction = true;

        }

        /// <summary>
        /// 验证 Warp Window、初始误差预算和累计权重曲线是否合法。
        /// </summary>
        private static bool IsValidConfiguration(
            CharacterCombatConfig config)
        {
            if (config == null)
                return false;

            if (!IsFinite(config.executionWarpWindowStartNormalized))
                return false;

            if (!IsFinite(config.executionWarpWindowEndNormalized))
                return false;

            if (config.executionWarpWindowStartNormalized < 0f)
                return false;

            if (config.executionWarpWindowStartNormalized > 1f)
                return false;

            if (config.executionWarpWindowEndNormalized <
                config.executionWarpWindowStartNormalized)
            {
                return false;
            }

            if (config.executionWarpWindowEndNormalized > 1f)
                return false;

            if (!IsFinite(config.executionMaxWarpTranslation))
                return false;

            if (config.executionMaxWarpTranslation < 0f)
                return false;

            if (!IsFinite(config.executionMaxWarpYaw))
                return false;

            if (config.executionMaxWarpYaw < 0f)
                return false;

            if (config.executionMaxWarpYaw > 180f)
                return false;

            if (!CharacterCombatConfig.IsExecutionWarpCurveValid(
                    config.executionWarpCurve))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 确认创建瞬间的实际位置和 Yaw 仍处于配置允许的初始 Warp 预算内。
        /// </summary>
        private static bool IsInitialPoseWithinBudget(
            in ExecutionPose anchorPose,
            in ExecutionPose initialExecutorPose,
            CharacterCombatConfig config)
        {
            Vector3 positionError = ProjectToHorizontalPlane(
                anchorPose.Position - initialExecutorPose.Position);

            float translationError =
                positionError.magnitude;

            float yawError = Mathf.Abs(Mathf.DeltaAngle(
                initialExecutorPose.Yaw,
                anchorPose.Yaw));

            if (!IsFinite(translationError))
                return false;

            if (translationError > config.executionMaxWarpTranslation)
                return false;

            if (!IsFinite(yawError))
                return false;

            if (yawError > config.executionMaxWarpYaw)
                return false;

            return true;
        }

        /// <summary>
        /// 把世界空间向量投影到由 CharacterMotor 管理的水平 XZ 平面。
        /// </summary>
        private static Vector3 ProjectToHorizontalPlane(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        /// <summary>
        /// 检查向量全部分量是否为有限值。
        /// </summary>
        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        /// <summary>
        /// 检查浮点数是否既不是 NaN，也不是正负 Infinity。
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}
