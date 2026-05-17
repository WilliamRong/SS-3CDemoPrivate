using Character.StateMachine;
using UnityEngine;

namespace Character.Presentation
{
    /// <summary>
    /// Idle + Locomotion blend tree. Not used while <see cref="CharacterStateId.Sprint"/> (sprint discrete states own layer 0).
    /// </summary>
    public sealed class CharacterLocomotionPresenter
    {
        private const float VelocityEpsilon = 0.05f;
        private const int HashNone = 0;

        private int _appliedStateHash = HashNone;

        /// <summary>Sprint discrete clips own layer 0 — invalidate so we CrossFade on return.</summary>
        public void ReleaseLayerToSprint()
        {
            _appliedStateHash = HashNone;
        }

        /// <summary>First frame after FSM left Sprint (e.g. still on SprintBrake in Animator).</summary>
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
                ? AnimatorParams.LocomotionCrossFadeDuration
                : AnimatorParams.LocomotionEnterCrossFadeDuration;

            _appliedStateHash = targetHash;

            animator.CrossFade(
                targetHash,
                fadeDuration,
                AnimatorParams.LocomotionLayerIndex,
                0f);

            if (stateId == CharacterStateId.Move)
                SetForwardRunBlend(animator);
        }

        private static void SetForwardRunBlend(Animator animator)
        {
            animator.SetFloat(AnimatorParams.VelocityX, 0f);
            animator.SetFloat(AnimatorParams.VelocityZ, AnimatorParams.RunForwardBlendZ);
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

        private static Vector2 ComputeAnimatorBlendVelocity(
            Transform presentationRoot,
            Vector2 worldVelocityXZ,
            bool isLockOn,
            CharacterStateId stateId)
        {
            if (stateId == CharacterStateId.Idle)
                return Vector2.zero;

            var worldVelocity = new Vector3(worldVelocityXZ.x, 0f, worldVelocityXZ.y);
            float refSpeed = AnimatorParams.LocomotionBlendReferenceSpeed;
            float runZ = AnimatorParams.RunForwardBlendZ;
            float axisMax = AnimatorParams.BlendAxisMax;

            if (presentationRoot == null)
            {
                float mag = worldVelocity.magnitude;
                if (mag < VelocityEpsilon)
                    return new Vector2(0f, AnimatorParams.RunForwardBlendZ);

                float normalized = Mathf.Clamp(mag / refSpeed * runZ, 0f, axisMax);
                return new Vector2(0f, normalized * AnimatorParams.FreeMoveBlendScale);
            }

            if (isLockOn)
            {
                Vector3 localVelocity = presentationRoot.InverseTransformDirection(worldVelocity);
                return new Vector2(
                    Mathf.Clamp(localVelocity.x / refSpeed * runZ, -axisMax, axisMax),
                    Mathf.Clamp(localVelocity.z / refSpeed * runZ, -axisMax, axisMax));
            }

            float forwardSpeed = Vector3.Dot(worldVelocity, presentationRoot.forward);
            if (Mathf.Abs(forwardSpeed) < VelocityEpsilon)
                return new Vector2(0f, AnimatorParams.RunForwardBlendZ);

            float normalizedZ = Mathf.Clamp(
                forwardSpeed / refSpeed * runZ * AnimatorParams.FreeMoveBlendScale,
                -axisMax,
                axisMax);

            if (forwardSpeed > VelocityEpsilon)
                normalizedZ = Mathf.Max(normalizedZ, runZ);

            return new Vector2(0f, normalizedZ);
        }
    }
}
