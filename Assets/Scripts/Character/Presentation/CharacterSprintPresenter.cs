using Character.StateMachine;
using Character.StateMachine.States;
using UnityEngine;

namespace Character.Presentation
{
    /// <summary>
    /// Sprint discrete states on layer 0. Phase advances in <see cref="SprintState"/> (timed).
    /// </summary>
    public sealed class CharacterSprintPresenter
    {
        private int _lastAnimatorHash;
        private SprintState.SprintPhase _lastPhase = (SprintState.SprintPhase)(-1);

        public void Reset()
        {
            _lastAnimatorHash = 0;
            _lastPhase = (SprintState.SprintPhase)(-1);
        }

        public void TickRemotePhase(Animator animator, SprintState.SprintPhase phase)
        {
            if (animator == null)
                return;

            CrossFadeToPhase(animator, phase);
        }

        public void Tick(Animator animator, SprintState sprintState)
        {
            if (animator == null || sprintState == null)
                return;

            CrossFadeToPhase(animator, sprintState.CurrentPhase);
        }

        private void CrossFadeToPhase(Animator animator, SprintState.SprintPhase phase)
        {
            int targetHash = PhaseToHash(phase);
            if (targetHash == 0)
                return;

            if (phase == _lastPhase && targetHash == _lastAnimatorHash)
                return;

            _lastPhase = phase;
            _lastAnimatorHash = targetHash;

            animator.CrossFade(
                targetHash,
                AnimatorParams.SprintCrossFadeDuration,
                AnimatorParams.LocomotionLayerIndex,
                0f);
        }

        private static int PhaseToHash(SprintState.SprintPhase phase)
        {
            return phase switch
            {
                SprintState.SprintPhase.Start => AnimatorParams.StateSprintStart,
                SprintState.SprintPhase.Loop => AnimatorParams.StateSprintLoop,
                SprintState.SprintPhase.Brake => AnimatorParams.StateSprintBrake,
                SprintState.SprintPhase.Turn180 => AnimatorParams.StateSprintTurn180,
                _ => 0
            };
        }
    }
}
