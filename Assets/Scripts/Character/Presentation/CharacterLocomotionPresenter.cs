using Character.Config;
using Character.StateMachine;
using UnityEngine;

namespace Character.Presentation
{
    /// <summary>
    /// Idle + Locomotion blend tree. Not used while <see cref="CharacterStateId.Sprint"/> (sprint discrete states own layer 0).
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

        public void ReleaseLayerToSprint()
        {
            _appliedStateHash = HashNone;
        }

        public void TickLeavingSprint(
            Animator animator,
            Transform presentationRoot,
            CharacterStateId stateId,
            Vector2 worldVelocityXZ,
            bool isLockOn)
        {
            if (animator == null)
                return;

            _appliedStateHash = HashNone;
            ApplyState(animator, stateId, useFullCrossFade: true);
            ApplyBlend(animator, presentationRoot, stateId, worldVelocityXZ, isLockOn);
        }

        public void Tick(
            Animator animator,
            Transform presentationRoot,
            CharacterStateId stateId,
            Vector2 worldVelocityXZ,
            bool isLockOn)
        {
            if (animator == null)
                return;

            ApplyState(animator, stateId, useFullCrossFade: false);
            ApplyBlend(animator, presentationRoot, stateId, worldVelocityXZ, isLockOn);
        }

        private void ApplyState(Animator animator, CharacterStateId stateId, bool useFullCrossFade)
        {
            if (!IsLocomotionDrivingState(stateId))
            {
                if (stateId != CharacterStateId.None)
                    _appliedStateHash = HashNone;
                return;
            }

            int targetHash = stateId == CharacterStateId.Move
                ? AnimatorParams.StateLocomotion
                : AnimatorParams.StateIdle;

            if (targetHash == _appliedStateHash)
                return;

            bool enteringLocomotionTree = stateId == CharacterStateId.Move
                && _appliedStateHash != AnimatorParams.StateLocomotion;

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

        private void ApplyBlend(
            Animator animator,
            Transform presentationRoot,
            CharacterStateId stateId,
            Vector2 worldVelocityXZ,
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
                isLockOn,
                stateId);

            animator.SetFloat(AnimatorParams.VelocityX, blendXZ.x);
            animator.SetFloat(AnimatorParams.VelocityZ, blendXZ.y);
        }

        private static bool IsLocomotionDrivingState(CharacterStateId stateId)
        {
            return stateId is CharacterStateId.Idle or CharacterStateId.Move;
        }

        private Vector2 ComputeAnimatorBlendVelocity(
            Transform presentationRoot,
            Vector2 worldVelocityXZ,
            bool isLockOn,
            CharacterStateId stateId)
        {
            if (stateId == CharacterStateId.Idle)
                return Vector2.zero;

            var worldVelocity = new Vector3(worldVelocityXZ.x, 0f, worldVelocityXZ.y);
            float refSpeed = _config.locomotionBlendReferenceSpeed;
            float runZ = _config.runForwardBlendZ;
            float axisMax = _config.blendAxisMax;
            float epsilon = _config.velocityEpsilon;

            if (presentationRoot == null)
            {
                float mag = worldVelocity.magnitude;
                if (mag < epsilon)
                    return new Vector2(0f, _config.runForwardBlendZ);

                float normalized = Mathf.Clamp(mag / refSpeed * runZ, 0f, axisMax);
                return new Vector2(0f, normalized * _config.freeMoveBlendScale);
            }

            if (isLockOn)
            {
                Vector3 localVelocity = presentationRoot.InverseTransformDirection(worldVelocity);
                return new Vector2(
                    Mathf.Clamp(localVelocity.x / refSpeed * runZ, -axisMax, axisMax),
                    Mathf.Clamp(localVelocity.z / refSpeed * runZ, -axisMax, axisMax));
            }

            float forwardSpeed = Vector3.Dot(worldVelocity, presentationRoot.forward);
            if (Mathf.Abs(forwardSpeed) < epsilon)
                return new Vector2(0f, _config.runForwardBlendZ);

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
