using System;
using UnityEngine;

namespace Character.Combat
{
    /// <summary>
    /// 封装架势钳制、恢复延迟和绝对纠正，使 Player/NPC 共享完全一致的数值规则。
    /// </summary>
    public sealed class PostureRuntime
    {
        private const float MinimumMax = 0.01f;

        public event Action<float, float> Changed;

        public float Current { get; private set; }
        public float Max { get; private set; }
        public float Ratio => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;
        public bool IsFull => Current >= Max;
        public float RecoveryDelayRemaining { get; private set; }

        public PostureRuntime(float max)
        {
            Max = SanitizeMax(max);
            Current = 0f;
            RecoveryDelayRemaining = 0f;
        }

        // ============ 配置与权威修改 ============

        public bool ConfigureMax(float max)
        {
            float nextMax = SanitizeMax(max);
            float nextCurrent = Mathf.Clamp(Current, 0f, nextMax);
            bool changed = nextMax != Max || nextCurrent != Current;

            Max = nextMax;
            Current = nextCurrent;

            if (changed)
                NotifyChanged();

            return changed;
        }

        /// <summary>
        /// 只有实际增加架势时才刷新恢复延迟，零值或已满时的重复命中不会无期限阻止恢复。
        /// </summary>
        public float Add(float amount, float recoveryDelay)
        {
            float requested = SanitizeNonNegative(amount);
            if (requested <= 0f)
                return 0f;

            float previous = Current;
            Current = Mathf.Min(Max, previous + requested);
            float applied = Current - previous;

            if (applied <= 0f)
                return 0f;

            RecoveryDelayRemaining = SanitizeNonNegative(recoveryDelay);
            NotifyChanged();
            return applied;
        }

        // ============ 恢复模拟 ============

        /// <summary>
        /// 先消费延迟再用剩余步长恢复，避免较大 deltaTime 跨过延迟时丢失本帧有效恢复时间。
        /// </summary>
        public float TickRecovery(
            float deltaTime,
            float healthRatio,
            float lowHealthRate,
            float fullHealthRate)
        {
            float remainingStep = SanitizeNonNegative(deltaTime);
            if (remainingStep <= 0f || Current <= 0f)
                return 0f;

            if (RecoveryDelayRemaining > 0f)
            {
                float consumedDelay =
                    Mathf.Min(RecoveryDelayRemaining, remainingStep);
                RecoveryDelayRemaining -= consumedDelay;
                remainingStep -= consumedDelay;

                if (remainingStep <= 0f)
                    return 0f;
            }

            float lowRate = SanitizeNonNegative(lowHealthRate);
            float fullRate = Mathf.Max(
                lowRate,
                SanitizeNonNegative(fullHealthRate));
            float clampedHealthRatio =
                Mathf.Clamp01(SanitizeNonNegative(healthRatio));
            float recoveryRate =
                Mathf.Lerp(lowRate, fullRate, clampedHealthRatio);
            float recovered =
                Mathf.Min(Current, recoveryRate * remainingStep);

            if (recovered <= 0f)
                return 0f;

            Current -= recovered;
            NotifyChanged();
            return recovered;
        }

        // ============ 重置与远端纠正 ============

        public bool Reset(bool forceNotify = false)
        {
            bool changed = Current != 0f;
            Current = 0f;
            RecoveryDelayRemaining = 0f;

            if (changed || forceNotify)
                NotifyChanged();

            return changed;
        }

        /// <summary>
        /// 绝对快照清除本地恢复延迟，客户端不保留任何可能继续模拟的权威计时状态。
        /// </summary>
        public bool ApplyAuthoritative(float current, float max)
        {
            float nextMax = SanitizeMax(max);
            float nextCurrent = Mathf.Clamp(
                SanitizeNonNegative(current),
                0f,
                nextMax);
            bool changed = nextMax != Max || nextCurrent != Current;

            Max = nextMax;
            Current = nextCurrent;
            RecoveryDelayRemaining = 0f;

            if (changed)
                NotifyChanged();

            return changed;
        }

        // ============ 通知与输入清洗 ============

        private void NotifyChanged()
        {
            Changed?.Invoke(Current, Max);
        }

        private static float SanitizeMax(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return MinimumMax;

            return Mathf.Max(MinimumMax, value);
        }

        private static float SanitizeNonNegative(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;

            return Mathf.Max(0f, value);
        }
    }
}
