using Character.Config;
using Character.StateMachine;
using UnityEngine;

namespace Character.Presentation
{
    /// <summary>
    /// 只在 Idle、Move 与移动格挡时拥有基础层，Sprint 的离散动画可显式接管并在退出时重新建立移动树状态。
    /// </summary>
    public sealed class CharacterLocomotionPresenter
    {
        private const int HashNone = 0;

        private readonly CharacterPresentationConfig _config;
        private int _appliedStateHash = HashNone;

        public CharacterLocomotionPresenter(CharacterPresentationConfig config)
        {
            _config = config;
        }

        // ============ 基础层所有权 ============

        public void ReleaseLayerToSprint()
        {
            _appliedStateHash = HashNone;
        }

        /// <summary>
        /// 退出冲刺时强制一次完整淡入，避免缓存仍认为移动树处于激活状态而跳过重新播放。
        /// </summary>

        public void TickLeavingSprint(
            Animator animator,
            Transform presentationRoot,
            CharacterStateId stateId,
            Vector2 worldVelocityXZ,
            Vector2 moveInput,
            bool isLockOn)
        {
            if (animator == null)
                return;

            _appliedStateHash = HashNone;
            ApplyState(animator, stateId, useFullCrossFade: true);
            ApplyBlend(animator, presentationRoot, stateId, worldVelocityXZ, moveInput, isLockOn);
        }

        // ============ 常规帧更新 ============

        public void Tick(
            Animator animator,
            Transform presentationRoot,
            CharacterStateId stateId,
            Vector2 worldVelocityXZ,
            Vector2 moveInput,
            bool isLockOn)
        {
            if (animator == null)
                return;

            ApplyState(animator, stateId, useFullCrossFade: false);
            ApplyBlend(animator, presentationRoot, stateId, worldVelocityXZ, moveInput, isLockOn);
        }

        // ============ 状态选择 ============

        /// <summary>
        /// 只在目标状态变化时 CrossFade，连续帧仅更新 Blend 参数，避免动画时间被重复归零。
        /// </summary>
        private void ApplyState(Animator animator, CharacterStateId stateId, bool useFullCrossFade)
        {
            if (!IsLocomotionDrivingState(stateId))
            {
                if (stateId != CharacterStateId.None)
                    _appliedStateHash = HashNone;
                return;
            }

            int targetHash = stateId switch
            {
                CharacterStateId.Move => AnimatorParams.StateLocomotion,
                CharacterStateId.Guard => AnimatorParams.StateGuardWalk,
                _ => AnimatorParams.StateIdle,
            };

            if (targetHash == _appliedStateHash)
                return;

            bool enteringLocomotionTree = (stateId == CharacterStateId.Move
                    && _appliedStateHash != AnimatorParams.StateLocomotion)
                || (stateId == CharacterStateId.Guard
                    && _appliedStateHash != AnimatorParams.StateGuardWalk);

            float fadeDuration = useFullCrossFade || !enteringLocomotionTree
                ? _config.locomotionCrossFadeDuration
                : _config.locomotionEnterCrossFadeDuration;

            _appliedStateHash = targetHash;

            animator.CrossFade(
                targetHash,
                fadeDuration,
                AnimatorParams.LocomotionLayerIndex,
                0f);

            if (stateId == CharacterStateId.Move)
                SetForwardRunBlend(animator);
        }

        private void SetForwardRunBlend(Animator animator)
        {
            animator.SetFloat(AnimatorParams.VelocityX, 0f);
            animator.SetFloat(AnimatorParams.VelocityZ, _config.runForwardBlendZ);
        }

        // ============ Blend 参数 ============

        /// <summary>
        /// 非移动状态显式清零参数，防止 Animator 在重新进入 BlendTree 时继承上一段方向。
        /// </summary>
        private void ApplyBlend(
            Animator animator,
            Transform presentationRoot,
            CharacterStateId stateId,
            Vector2 worldVelocityXZ,
            Vector2 moveInput,
            bool isLockOn)
        {
            if (!IsLocomotionDrivingState(stateId))
            {
                animator.SetFloat(AnimatorParams.VelocityX, 0f);
                animator.SetFloat(AnimatorParams.VelocityZ, 0f);
                return;
            }

            Vector2 blendXZ = ComputeAnimatorBlendVelocity(
                presentationRoot,
                worldVelocityXZ,
                moveInput,
                isLockOn,
                stateId);

            animator.SetFloat(AnimatorParams.VelocityX, blendXZ.x);
            animator.SetFloat(AnimatorParams.VelocityZ, blendXZ.y);
        }

        private static bool IsLocomotionDrivingState(CharacterStateId stateId)
        {
            return stateId is CharacterStateId.Idle or CharacterStateId.Move or CharacterStateId.Guard;
        }

        private Vector2 ComputeLockOnBlendInput(Vector2 moveInput)
        {
            float epsilon = _config.velocityEpsilon;
            if (moveInput.sqrMagnitude <= epsilon * epsilon)
                return Vector2.zero;

            return Vector2.ClampMagnitude(moveInput, 1f);
        }

        /// <summary>
        /// 锁定使用输入意图保留侧移，非锁定使用实际世界速度并压到前向轴，匹配两套不同的 BlendTree 语义。
        /// </summary>
        private Vector2 ComputeAnimatorBlendVelocity(
            Transform presentationRoot,
            Vector2 worldVelocityXZ,
            Vector2 moveInput,
            bool isLockOn,
            CharacterStateId stateId)
        {
            if (stateId == CharacterStateId.Idle)
                return Vector2.zero;

            if (isLockOn)
                return ComputeLockOnBlendInput(moveInput);

            var worldVelocity = new Vector3(worldVelocityXZ.x, 0f, worldVelocityXZ.y);
            float refSpeed = _config.locomotionBlendReferenceSpeed;
            float runZ = _config.runForwardBlendZ;
            float axisMax = _config.blendAxisMax;
            float epsilon = _config.velocityEpsilon;

            if (presentationRoot == null)
            {
                float mag = worldVelocity.magnitude;
                if (mag < epsilon)
                    return new Vector2(0f, runZ);

                float normalized = Mathf.Clamp(mag / refSpeed * runZ, 0f, axisMax);
                return new Vector2(0f, normalized * _config.freeMoveBlendScale);
            }

            float forwardSpeed = Vector3.Dot(worldVelocity, presentationRoot.forward);
            if (Mathf.Abs(forwardSpeed) < epsilon)
                return new Vector2(0f, runZ);

            float normalizedZ = Mathf.Clamp(
                forwardSpeed / refSpeed * runZ * _config.freeMoveBlendScale,
                -axisMax,
                axisMax);

            if (forwardSpeed > epsilon)
                normalizedZ = Mathf.Max(normalizedZ, runZ);

            return new Vector2(0f, normalizedZ);
        }
    }
}
