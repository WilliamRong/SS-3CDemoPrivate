using Character.Config;
using Character.StateMachine;
using UnityEngine;

namespace Character.Presentation
{
    public sealed class CharacterCombatPresenter
    {
        private readonly CharacterPresentationConfig _config;
        private CharacterStateId _lastCombatState = CharacterStateId.None;
        private int _lastHash;

        public CharacterCombatPresenter(CharacterPresentationConfig config)
        {
            _config = config;
        }

        public void Tick(Animator animator, CharacterStateId stateId, int actionParam = 0)
        {
            if (animator == null)
                return;

            switch (stateId)
            {
                case CharacterStateId.Hit:
                {
                    bool heavyHit = actionParam != 0;
                    float crossFade = _config.GetHitCrossFadeDuration(heavyHit);
                    PlayOnLayer(
                        animator,
                        AnimatorParams.HitLayerIndex,
                        AnimatorParams.StateHit,
                        crossFade,
                        suppressOtherCombatLayers: true);
                    break;
                }
                case CharacterStateId.Dead:
                    PlayOnLayer(
                        animator,
                        AnimatorParams.DeathLayerIndex,
                        AnimatorParams.StateDeath,
                        _config.GetDeathCrossFadeDuration(),
                        suppressOtherCombatLayers: true);
                    break;
                case CharacterStateId.Attack:
                case CharacterStateId.Dodge:
                    break;
                default:
                    return;
            }

            _lastCombatState = stateId;
        }

        public void ResetHitLayer(Animator animator)
        {
            if (animator == null)
                return;

            animator.SetLayerWeight(AnimatorParams.HitLayerIndex, 0f);
            if (_lastCombatState == CharacterStateId.Hit)
            {
                _lastCombatState = CharacterStateId.None;
                _lastHash = 0;
            }
        }

        public void ResetDeathLayer(Animator animator)
        {
            if (animator == null)
                return;

            animator.SetLayerWeight(AnimatorParams.DeathLayerIndex, 0f);
            if (_lastCombatState == CharacterStateId.Dead)
            {
                _lastCombatState = CharacterStateId.None;
                _lastHash = 0;
            }
        }

        public void ResetAllCombatLayers(Animator animator)
        {
            ResetHitLayer(animator);
            ResetDeathLayer(animator);
        }

        private void PlayOnLayer(
            Animator animator,
            int layerIndex,
            int stateHash,
            float crossFadeDuration,
            bool suppressOtherCombatLayers)
        {
            if (suppressOtherCombatLayers)
            {
                if (layerIndex != AnimatorParams.HitLayerIndex)
                    animator.SetLayerWeight(AnimatorParams.HitLayerIndex, 0f);
                if (layerIndex != AnimatorParams.DeathLayerIndex)
                    animator.SetLayerWeight(AnimatorParams.DeathLayerIndex, 0f);
            }

            animator.SetLayerWeight(layerIndex, 1f);

            if (stateHash == _lastHash && _lastCombatState != CharacterStateId.None)
                return;

            _lastHash = stateHash;
            animator.CrossFade(stateHash, crossFadeDuration, layerIndex, 0f);
        }
    }
}
