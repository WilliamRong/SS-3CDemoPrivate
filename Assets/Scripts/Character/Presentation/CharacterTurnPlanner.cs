using Character.Config;
using UnityEngine;

namespace Character.Presentation
{
    public readonly struct CharacterTurnPlan
    {
        public readonly float StepAngle;
        public readonly Quaternion TargetRotation;
        public readonly float Duration;
        public readonly float YawSpeed;

        public CharacterTurnPlan(float stepAngle, Quaternion targetRotation, float duration, float yawSpeed)
        {
            StepAngle = stepAngle;
            TargetRotation = targetRotation;
            Duration = duration;
            YawSpeed = yawSpeed;
        }
    }

    public static class CharacterTurnPlanner
    {
        public static bool TryGetFacingDelta(
            Transform root,
            Vector3 targetPosition,
            out bool turnLeft,
            out float angleDelta,
            out Quaternion targetRotation)
        {
            turnLeft = false;
            angleDelta = 0f;
            targetRotation = root != null ? root.rotation : Quaternion.identity;

            if (root == null)
                return false;

            Vector3 toTarget = targetPosition - root.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.01f)
                return false;

            Vector3 direction = toTarget.normalized;
            float angle = Vector3.SignedAngle(root.forward, direction, Vector3.up);
            angleDelta = Mathf.Abs(angle);
            turnLeft = angle < 0f;
            targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            return true;
        }

        public static bool ShouldTurn(float turnCooldown, float angleDelta, CharacterPresentationConfig config)
        {
            return turnCooldown <= 0f && angleDelta >= GetTriggerAngle(config);
        }

        public static CharacterTurnPlan BuildPlan(
            Transform root,
            bool turnLeft,
            float angleDelta,
            CharacterPresentationConfig config,
            Quaternion exactTargetRotation = default)
        {
            float stepAngle = Mathf.Min(angleDelta, GetStepAngle(config));
            Quaternion targetRotation;

            // 最后一步（剩余角度不超过单步上限）：直接用精确面向目标的旋转，消除累积误差
            if (angleDelta <= GetStepAngle(config) && exactTargetRotation != default)
            {
                stepAngle = angleDelta;
                targetRotation = exactTargetRotation;
            }
            else
            {
                targetRotation = CalculateStepTargetRotation(root, turnLeft, stepAngle);
            }

            float duration = CalculateDuration(stepAngle, config);
            float yawSpeed = duration > 0.0001f ? stepAngle / duration : 0f;

            return new CharacterTurnPlan(stepAngle, targetRotation, duration, yawSpeed);
        }

        public static bool IsAligned(Quaternion currentRotation, Quaternion targetRotation, CharacterPresentationConfig config)
        {
            return Quaternion.Angle(currentRotation, targetRotation) <= GetAngleTolerance(config);
        }

        /// <summary>统一的动画播放倍率，不随角度变化。</summary>
        public static float CalculateSpeed(float angleDelta, CharacterPresentationConfig config)
        {
            return GetSpeedMultiplier(config);
        }

        public static float GetCooldown(CharacterPresentationConfig config) => config != null
            ? Mathf.Max(0f, config.turnCooldown)
            : 0.2f;

        private static Quaternion CalculateStepTargetRotation(Transform root, bool turnLeft, float stepAngle)
        {
            if (root == null)
                return Quaternion.identity;

            float signedStep = turnLeft ? -stepAngle : stepAngle;
            Vector3 targetForward = Quaternion.AngleAxis(signedStep, Vector3.up) * root.forward;
            targetForward.y = 0f;

            return targetForward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(targetForward.normalized, Vector3.up)
                : root.rotation;
        }

        private static float CalculateDuration(float angleDelta, CharacterPresentationConfig config)
        {
            float baseDuration = GetDuration(config);
            float animAngle = GetAnimationAngle(config);
            float speed = GetSpeedMultiplier(config);
            if (animAngle <= 0.001f) return baseDuration / speed;
            return Mathf.Max(0.01f, (angleDelta / animAngle) * baseDuration / speed);
        }

        private static float GetTriggerAngle(CharacterPresentationConfig config) => config != null
            ? Mathf.Max(1f, config.turnTriggerAngle)
            : 20f;

        private static float GetStepAngle(CharacterPresentationConfig config) => config != null
            ? Mathf.Max(1f, config.turnStepAngle)
            : 180f;

        private static float GetAngleTolerance(CharacterPresentationConfig config) => config != null
            ? Mathf.Max(0.1f, config.turnAngleTolerance)
            : 3f;

        private static float GetAnimationAngle(CharacterPresentationConfig config) => config != null
            ? Mathf.Max(1f, config.turnAnimationAngle)
            : 90f;

        private static float GetSpeedMultiplier(CharacterPresentationConfig config) => config != null
            ? Mathf.Max(0.5f, config.turnSpeedMultiplierMax)
            : 1.5f;

        private static float GetDuration(CharacterPresentationConfig config) => config != null
            ? config.idleTurnDuration
            : 0.5f;
    }
}
