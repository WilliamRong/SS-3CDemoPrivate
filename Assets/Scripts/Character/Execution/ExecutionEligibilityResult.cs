namespace Character.Execution
{

    public readonly struct ExecutionEligibilityResult
    {
        public bool IsEvaluated { get; }

        public bool IsEligible => IsEvaluated && RejectionReason == ExecutionRejectionReason.None;

        public bool HasSpatialSolution { get; }

        public ExecutionRejectionReason RejectionReason { get; }

        public ExecutionPose FixedTargetPose { get; }
        public ExecutionPose ExecutorAnchorPose { get; }

        public float HorizontalDistance { get; }
        public float HeightDifference { get; }
        public float TargetFrontAngle { get; }
        public float WarpTranslationError { get; }
        public float WarpYawError { get; }

        private ExecutionEligibilityResult(
            ExecutionRejectionReason rejectionReason,
            bool hasSpatialSolution,
            ExecutionPose fixedTargetPose,
            ExecutionPose executorAnchorPose,
            float horizontalDistance,
            float heightDifference,
            float targetFrontAngle,
            float warpTranslationError,
            float warpYawError)
        {
            IsEvaluated = true;
            RejectionReason = rejectionReason;
            HasSpatialSolution = hasSpatialSolution;
            FixedTargetPose = fixedTargetPose;
            ExecutorAnchorPose = executorAnchorPose;
            HorizontalDistance = horizontalDistance;
            HeightDifference = heightDifference;
            TargetFrontAngle = targetFrontAngle;
            WarpTranslationError = warpTranslationError;
            WarpYawError = warpYawError;
        }


        internal static ExecutionEligibilityResult RejectBeforeSpatial(ExecutionRejectionReason reason)
        {
            return new ExecutionEligibilityResult(reason, false, default, default, 0f, 0f, 0f, 0f, 0f);
        }

        internal static ExecutionEligibilityResult FromSpatial(
            ExecutionRejectionReason reason,
            ExecutionPose fixedTargetPose,
            ExecutionPose executorAnchorPose,
            float horizontalDistance,
            float heightDifference,
            float targetFrontAngle,
            float warpTranslationError,
            float warpYawError)
        {
            return new ExecutionEligibilityResult(
                reason,
                true,
                fixedTargetPose,
                executorAnchorPose,
                horizontalDistance,
                heightDifference,
                targetFrontAngle,
                warpTranslationError,
                warpYawError);
        }

        internal ExecutionEligibilityResult WithRejection(
            ExecutionRejectionReason reason)
        {
            return new ExecutionEligibilityResult(
                reason,
                HasSpatialSolution,
                FixedTargetPose,
                ExecutorAnchorPose,
                HorizontalDistance,
                HeightDifference,
                TargetFrontAngle,
                WarpTranslationError,
                WarpYawError);
        }

    }
}