using System;
using UnityEngine;

namespace Character.Combat
{
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

        public bool Reset(bool forceNotify = false)
        {
            bool changed = Current != 0f;
            Current = 0f;
            RecoveryDelayRemaining = 0f;

            if (changed || forceNotify)
                NotifyChanged();

            return changed;
        }

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