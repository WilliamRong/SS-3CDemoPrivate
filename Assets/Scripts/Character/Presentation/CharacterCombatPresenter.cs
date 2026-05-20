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
        private DodgeMode _lastDodgeMode = DodgeMode.None;

        public CharacterCombatPresenter(CharacterPresentationConfig config)
        {
            _config = config;
        }

        public bool TickCombat(Animator animator, CharacterStateId stateId, int actionParams = 0,
            in DodgePresentationContext dodgeCtx = default)
        {
            if (animator == null) return false;

            switch (stateId)
            {
                case CharacterStateId.Hit:
                    ResetActiveCombatLayers(animator);
                    PlayReaction(animator, AnimatorParams.HitLayerIndex, AnimatorParams.StateHit, _config.GetHitCrossFadeDuration(actionParams != 0));
                    _lastCombatState = stateId;
                    return true;
                case CharacterStateId.Dead:
                    ResetActiveCombatLayers(animator);
                    PlayReaction(animator, AnimatorParams.DeathLayerIndex, AnimatorParams.StateDeath,
                        _config.GetDeathCrossFadeDuration());
                    _lastCombatState = stateId;
                    return true;
                case CharacterStateId.Dodge:
                    ResetReactionLayers(animator);
                    TickDodge(animator, dodgeCtx);
                    _lastCombatState = stateId;
                    return true;
                case CharacterStateId.Attack:
                    ResetReactionLayers(animator);
                    _lastCombatState = stateId;
                    return true;
                default:
                    return false;
            }
        }

        public void ResetReactionLayers(Animator animator)
        {
            if (animator == null) return;
            animator.SetLayerWeight(AnimatorParams.HitLayerIndex, 0f);
            animator.SetLayerWeight(AnimatorParams.DeathLayerIndex, 0f);
            if (_lastCombatState is CharacterStateId.Hit or CharacterStateId.Dead)
            {
                _lastCombatState = CharacterStateId.None;
                _lastHash = 0;
            }
        }

        public void ResetActiveCombatLayers(Animator animator)
        {
            if (animator == null) return;
            animator.SetLayerWeight(AnimatorParams.DodgeLayerIndex, 0f);
            animator.SetLayerWeight(AnimatorParams.UpperBodyLayerIndex, 0f);
            _lastDodgeMode = DodgeMode.None;
            if (_lastCombatState is CharacterStateId.Dodge or CharacterStateId.Attack)
            {
                _lastCombatState = CharacterStateId.None;
                _lastHash = 0;
            }
        }

        public void ResetAllCombatLayers(Animator animator)
        {
            ResetReactionLayers(animator);
            ResetActiveCombatLayers(animator);
        }

        private void TickDodge(Animator animator, in DodgePresentationContext ctx)
        {
            if (!ctx.IsValid) return;

            int targetHash = ctx.Mode switch
            {
                DodgeMode.NeutralBackward => AnimatorParams.StateDodgeBackStep,
                DodgeMode.ForwardAlongMove => AnimatorParams.StateDodgeNormal,
                DodgeMode.LockOn8Way => AnimatorParams.StateDodgeDirectional,
                _ => 0,
            };
            
            if(targetHash == 0) return;

            if (ctx.Mode == DodgeMode.LockOn8Way)
            {
                animator.SetFloat(AnimatorParams.DodgeInputX, ctx.BlendLocal.x);
                animator.SetFloat(AnimatorParams.DodgeInputZ, ctx.BlendLocal.y);
            }
            
            animator.SetLayerWeight(AnimatorParams.DodgeLayerIndex, 1f);
            
            if (targetHash == _lastHash && _lastDodgeMode == ctx.Mode)
                return;
            _lastHash = targetHash;
            _lastDodgeMode = ctx.Mode;
            animator.CrossFade(targetHash, _config.dodgeCrossFadeDuration, AnimatorParams.DodgeLayerIndex, 0f);
        }

        private void PlayReaction(Animator animator, int layerIndex, int stateHash, float crossFadeDuration)
        {
            animator.SetLayerWeight(AnimatorParams.HitLayerIndex, 0f);
            animator.SetLayerWeight(AnimatorParams.DeathLayerIndex, 0f);
            animator.SetLayerWeight(layerIndex, 1f);
            if (stateHash == _lastHash && _lastCombatState != CharacterStateId.None)
                return;
            _lastHash = stateHash;
            animator.CrossFade(stateHash, crossFadeDuration, layerIndex, 0f);
        }
    }
}
