using Character.Combat;
using Character.Config;
using Character.StateMachine;
using Character.StateMachine.States;
using UnityEngine;

namespace Character.Presentation
{
    public sealed class CharacterCombatPresenter
    {
        private readonly CharacterPresentationConfig _config;
        private CharacterStateId _lastCombatState = CharacterStateId.None;
        private int _lastHash;
        private DodgeMode _lastDodgeMode = DodgeMode.None;
        private int _lastGuardHash;
        private int _lastGuardLayerIndex = -1;
        private GuardState.GuardPhase _lastGuardPhase = GuardState.GuardPhase.None;
        private bool _lastGuardIsFullBody;

        public CharacterCombatPresenter(CharacterPresentationConfig config)
        {
            _config = config;
        }

        public bool TickCombat(
            Animator animator,
            CharacterStateId stateId,
            int actionParams = 0,
            in DodgePresentationContext dodgeCtx = default,
            GuardState.GuardPhase guardPhase = GuardState.GuardPhase.Start,
            bool guardHasMove = false,
            byte attackComboStep = 1)
        {
            if (animator == null) return false;

            switch (stateId)
            {
                case CharacterStateId.Hit:
                    ResetActiveCombatLayers(animator);
                    PlayReaction(animator, AnimatorParams.StateHit, _config.GetHitCrossFadeDuration(actionParams != 0));
                    _lastCombatState = stateId;
                    return true;
                case CharacterStateId.Dead:
                    ResetActiveCombatLayers(animator);
                    PlayReaction(animator, AnimatorParams.StateDeath, _config.GetDeathCrossFadeDuration());
                    _lastCombatState = stateId;
                    return true;
                case CharacterStateId.Dodge:
                    ResetReactionLayers(animator);
                    ResetGuardLayers(animator);
                    TickDodge(animator, dodgeCtx);
                    _lastCombatState = stateId;
                    return true;
                case CharacterStateId.Guard:
                    _lastCombatState = stateId;
                    return TickGuard(animator, guardPhase, guardHasMove);
                case CharacterStateId.Attack:
                    ResetReactionLayers(animator);
                    TickAttack(animator, attackComboStep);
                    _lastCombatState = stateId;
                    return true;
                default:
                    return false;
            }
        }

        public void ResetReactionLayers(Animator animator)
        {
            if (animator == null) return;
            animator.SetLayerWeight(AnimatorParams.ReactionLayerIndex, 0f);
            if (_lastCombatState is CharacterStateId.Hit or CharacterStateId.Dead)
            {
                _lastCombatState = CharacterStateId.None;
                _lastHash = 0;
            }
        }

        public void ResetActiveCombatLayers(Animator animator)
        {
            if (animator == null) return;
            animator.SetLayerWeight(AnimatorParams.CombatLayerIndex, 0f);
            animator.SetLayerWeight(AnimatorParams.UpperBodyLayerIndex, 0f);
            _lastDodgeMode = DodgeMode.None;
            ResetGuardCache();
            if (_lastCombatState is CharacterStateId.Dodge or CharacterStateId.Guard or CharacterStateId.Attack)
            {
                _lastCombatState = CharacterStateId.None;
                _lastHash = 0;
            }
        }

        public void ResetGuardLayers(Animator animator)
        {
            if (animator == null) return;
            animator.SetLayerWeight(AnimatorParams.CombatLayerIndex, 0f);
            animator.SetLayerWeight(AnimatorParams.UpperBodyLayerIndex, 0f);
            ResetGuardCache();
            if (_lastCombatState == CharacterStateId.Guard)
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


        private void TickAttack(Animator animator, byte comboStep)
        {
            int targetHash = AttackIdToHash(AttackMoveIdExtensions.FromByte(comboStep));
            if (targetHash == 0) return;
            
            animator.SetLayerWeight(AnimatorParams.UpperBodyLayerIndex, 0f);
            animator.SetLayerWeight(AnimatorParams.CombatLayerIndex, 1f);

            if (_lastCombatState == CharacterStateId.Attack && _lastHash == targetHash) return;

            _lastHash = targetHash;
            animator.CrossFade(targetHash, _config.attackCrossFadeDuration, AnimatorParams.CombatLayerIndex, 0f);
        }
        
        
        
        /// <summary>
        /// Returns true when Guard is full-body and should block locomotion presentation.
        /// Moving Guard is a temporary split: upper-body guard over layer-0 walk.
        /// </summary>
        public bool TickGuard(Animator animator, GuardState.GuardPhase phase, bool hasMove)
        {
            if (animator == null) return false;
            
            ResetReactionLayers(animator); 
            animator.SetLayerWeight(AnimatorParams.CombatLayerIndex, 0f); 
            _lastDodgeMode = DodgeMode.None;

            // Any guard phase can use upper-body overlay while moving, so layer-0 walk stays visible.
            bool isFullBody = !hasMove;
            int layerIndex = isFullBody
                ? AnimatorParams.CombatLayerIndex
                : AnimatorParams.UpperBodyLayerIndex;

            int targetHash = PhaseToGuardHash(phase);
            if (targetHash == 0)
                return isFullBody;

            if (isFullBody)
            {
                animator.SetLayerWeight(AnimatorParams.UpperBodyLayerIndex, 0f);
                animator.SetLayerWeight(AnimatorParams.CombatLayerIndex, 1f);
            }
            else
            {
                animator.SetLayerWeight(AnimatorParams.CombatLayerIndex, 0f);
                animator.SetLayerWeight(AnimatorParams.UpperBodyLayerIndex, 1f);
            }

            if (phase == _lastGuardPhase
                && targetHash == _lastGuardHash
                && layerIndex == _lastGuardLayerIndex
                && isFullBody == _lastGuardIsFullBody)
            {
                return isFullBody;
            }

            _lastCombatState = CharacterStateId.Guard;
            _lastGuardPhase = phase;
            _lastGuardHash = targetHash;
            _lastGuardLayerIndex = layerIndex;
            _lastGuardIsFullBody = isFullBody;

            animator.CrossFade(targetHash, _config.guardCrossFadeDuration, layerIndex, 0f);
            return isFullBody;
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
            
            animator.SetLayerWeight(AnimatorParams.CombatLayerIndex, 1f);
            
            if (targetHash == _lastHash && _lastDodgeMode == ctx.Mode)
                return;
            _lastHash = targetHash;
            _lastDodgeMode = ctx.Mode;
            animator.CrossFade(targetHash, _config.dodgeCrossFadeDuration, AnimatorParams.CombatLayerIndex, 0f);
        }

        private void PlayReaction(Animator animator, int stateHash, float crossFadeDuration)
        {
            animator.SetLayerWeight(AnimatorParams.ReactionLayerIndex, 1f);
            if (stateHash == _lastHash && _lastCombatState != CharacterStateId.None)
                return;
            _lastHash = stateHash;
            animator.CrossFade(stateHash, crossFadeDuration, AnimatorParams.ReactionLayerIndex, 0f);
        }

        private static int PhaseToGuardHash(GuardState.GuardPhase phase)
        {
            return phase switch
            {
                GuardState.GuardPhase.Start => AnimatorParams.StateGuardStart,
                GuardState.GuardPhase.Loop => AnimatorParams.StateGuardLoop,
                GuardState.GuardPhase.Exit => AnimatorParams.StateGuardExit,
                _ => 0,
            };
        }

        private static int AttackIdToHash(AttackMoveId attackId)
        {
            return attackId switch
            {
                AttackMoveId.Combo1 => AnimatorParams.AttackCombo1,
                AttackMoveId.Combo2 => AnimatorParams.AttackCombo2,
                AttackMoveId.Combo3 => AnimatorParams.AttackCombo3,
                AttackMoveId.Combo4 => AnimatorParams.AttackCombo4,
                AttackMoveId.Sprint => AnimatorParams.AttackSprint,
                AttackMoveId.Dodge => AnimatorParams.AttackDodge,
                AttackMoveId.Heavy1Start => AnimatorParams.AttackHeavy1Start,
                AttackMoveId.Heavy1 => AnimatorParams.AttackHeavy1,
                AttackMoveId.Heavy2 => AnimatorParams.AttackHeavy2,
                _ => AnimatorParams.AttackCombo1,
            };
        }

        private void ResetGuardCache()
        {
            _lastGuardHash = 0;
            _lastGuardLayerIndex = -1;
            _lastGuardPhase = GuardState.GuardPhase.None;
            _lastGuardIsFullBody = false;
        }
    }
}
