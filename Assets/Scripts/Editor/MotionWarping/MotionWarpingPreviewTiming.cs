using UnityEngine;

namespace Character.EditorTools.Execution
{
    public readonly struct MotionWarpingPreviewTimeSample
    {
        public float ElapsedTime { get; }
        public float PreviewDuration { get; }
        public float PreviewNormalizedTime { get; }
        public float ExecutorNormalizedTime { get; }
        public float TargetNormalizedTime { get; }

        public MotionWarpingPreviewTimeSample(
            float elapsedTime,
            float previewDuration,
            float previewNormalizedTime,
            float executorNormalizedTime,
            float targetNormalizedTime)
        {
            ElapsedTime = elapsedTime;
            PreviewDuration = previewDuration;
            PreviewNormalizedTime = previewNormalizedTime;
            ExecutorNormalizedTime = executorNormalizedTime;
            TargetNormalizedTime = targetNormalizedTime;
        }
    }

    public static class MotionWarpingPreviewTiming
    {
        public static bool TryEvaluate(
            float elapsedTime,
            float executorDuration,
            float targetDuration,
            out MotionWarpingPreviewTimeSample sample)
        {
            sample = default;

            if (!IsPositiveFinite(executorDuration) ||
                !IsPositiveFinite(targetDuration) ||
                !IsFinite(elapsedTime))
            {
                return false;
            }

            float previewDuration =
                Mathf.Max(executorDuration, targetDuration);

            float clampedElapsed =
                Mathf.Clamp(elapsedTime, 0f, previewDuration);

            sample = new MotionWarpingPreviewTimeSample(
                clampedElapsed,
                previewDuration,
                Mathf.Clamp01(
                    clampedElapsed / previewDuration),
                Mathf.Clamp01(
                    clampedElapsed / executorDuration),
                Mathf.Clamp01(
                    clampedElapsed / targetDuration));

            return true;
        }

        private static bool IsPositiveFinite(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}
