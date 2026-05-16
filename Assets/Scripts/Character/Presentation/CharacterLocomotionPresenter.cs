using Character.StateMachine;
using UnityEngine;

namespace Character.Presentation
{
    /// <summary>
    /// Locomotion layer: Idle state + 2D run blend tree (VelocityX/Z).
    /// Free move: forward axis targets run at (0, <see cref="AnimatorParams.RunForwardBlendZ"/>).
    /// Lock-on strafe (walk/defense) will use full X/Z later; upper-body overlay TBD.
    /// </summary>
    public sealed class CharacterLocomotionPresenter
    {
        private const float VelocityEpsilon = 0.05f;

        private int _lastLocomotionAnimatorHash;

        public void Tick(Animator animator, Transform presentationRoot, CharacterStateId stateId, Vector2 worldVelocityXZ, bool isLockOn)
        {
            if (animator == null) return;

            ApplyLocomotionState(animator, stateId);
            ApplyLocomotionBlend(animator, presentationRoot, stateId, worldVelocityXZ, isLockOn);
        }

        private void ApplyLocomotionState(Animator animator, CharacterStateId stateId)
        {
            if (!IsLocomotionDrivingState(stateId))
            {
                if (stateId != CharacterStateId.None)
                    _lastLocomotionAnimatorHash = 0;
                return;
            }

            bool useLocomotionTree = stateId == CharacterStateId.Move || stateId == CharacterStateId.Sprint;
            int targetHash = useLocomotionTree ? AnimatorParams.StateLocomotion : AnimatorParams.StateIdle;

            if (targetHash == _lastLocomotionAnimatorHash)
                return;

            bool enteringLocomotionTree = useLocomotionTree
                && _lastLocomotionAnimatorHash != AnimatorParams.StateLocomotion;
            float fadeDuration = enteringLocomotionTree
                ? AnimatorParams.LocomotionEnterCrossFadeDuration
                : AnimatorParams.LocomotionCrossFadeDuration;

            _lastLocomotionAnimatorHash = targetHash;

            animator.CrossFade(
                targetHash,
                fadeDuration,
                AnimatorParams.LocomotionLayerIndex,
                0f);

            if (enteringLocomotionTree)
                SetForwardRunBlend(animator);
        }

        private static void SetForwardRunBlend(Animator animator)
        {
            animator.SetFloat(AnimatorParams.VelocityX, 0f);
            animator.SetFloat(AnimatorParams.VelocityZ, AnimatorParams.RunForwardBlendZ);
        }

        private void ApplyLocomotionBlend(
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
            return stateId is CharacterStateId.Idle or CharacterStateId.Move or CharacterStateId.Sprint;
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
                    return ResolveFallbackBlend(stateId);

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
                return ResolveFallbackBlend(stateId);

            float normalizedZ = Mathf.Clamp(
                forwardSpeed / refSpeed * runZ * AnimatorParams.FreeMoveBlendScale,
                -axisMax,
                axisMax);

            if (forwardSpeed > VelocityEpsilon)
                normalizedZ = Mathf.Max(normalizedZ, runZ);

            return new Vector2(0f, normalizedZ);
        }

        /// <summary>
        /// Move/Sprint with input but negligible velocity — hold forward run in blend tree.
        /// </summary>
        private static Vector2 ResolveFallbackBlend(CharacterStateId stateId)
        {
            if (stateId is CharacterStateId.Move or CharacterStateId.Sprint)
                return new Vector2(0f, AnimatorParams.RunForwardBlendZ);

            return Vector2.zero;
        }
    }
}
