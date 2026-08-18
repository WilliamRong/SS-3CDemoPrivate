using Character.Config;
using UnityEngine;

namespace Character.Presentation
{
    /// <summary>
    /// 将一次分段转身所需旋转和时长冻结，避免动画播放期间目标移动导致步长持续漂移。
    /// </summary>
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

    /// <summary>
    /// 统一 Idle 与 Guard 的分段转身几何计算，使状态层只负责决定何时执行计划。
    /// </summary>
    public static class CharacterTurnPlanner
    {
        // ============ 朝向几何 ============

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

        // ============ 转身计划 ============

        /// <summary>
        /// 大角度按固定步长拆分，最后一步使用精确目标旋转，避免多次 Quaternion 累积留下朝向误差。
        /// </summary>
        public static CharacterTurnPlan BuildPlan(
            Transform root,
            bool turnLeft,
            float angleDelta,
            CharacterPresentationConfig config,
            Quaternion exactTargetRotation = default)
        {
            float stepAngle = Mathf.Min(angleDelta, GetStepAngle(config));
            Quaternion targetRotation;
            bool hasExactTargetRotation =
                Quaternion.Dot(exactTargetRotation, exactTargetRotation) > 0.000001f;

            if (angleDelta <= GetStepAngle(config) && hasExactTargetRotation)
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

        public static float CalculateSpeed(float angleDelta, CharacterPresentationConfig config)
        {
            if (config == null || config.turnAnimationAngle <= 0.001f)
                return 1f;

            float normalizedAngle = Mathf.Clamp01(angleDelta / GetAnimationAngle(config));
            return Mathf.Max(
                0.01f,
                Mathf.Lerp(GetSpeedMultiplierMax(config), GetSpeedMultiplierMin(config), normalizedAngle));
        }

        // ============ 配置回退 ============

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
            return Mathf.Max(0.01f, GetDuration(config)) / CalculateSpeed(angleDelta, config);
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

        private static float GetSpeedMultiplierMax(CharacterPresentationConfig config) => config != null
            ? Mathf.Max(0.01f, config.turnSpeedMultiplierMax)
            : 1.3f;

        private static float GetSpeedMultiplierMin(CharacterPresentationConfig config) => config != null
            ? Mathf.Max(0.01f, config.turnSpeedMultiplierMin)
            : 1f;

        private static float GetDuration(CharacterPresentationConfig config) => config != null
            ? config.idleTurnDuration
            : 0.5f;
    }
}
