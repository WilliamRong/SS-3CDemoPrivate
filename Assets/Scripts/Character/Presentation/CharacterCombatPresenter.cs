using Character.Combat;
using Character.Config;
using Character.StateMachine;
using Character.StateMachine.States;
using UnityEngine;

namespace Character.Presentation
{
    /// <summary>
    /// 集中协调战斗层、上半身层和反应层的所有权，保证同一帧只有最高优先级状态控制相关 Animator Layer。
    /// </summary>
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
        private float _guardReactionTimer;
        private GuardReactionType _activeGuardReaction = GuardReactionType.None;

        public CharacterCombatPresenter(CharacterPresentationConfig config)
        {
            _config = config;
        }

        // ============ 战斗状态分派 ============

        /// <summary>
        /// 所有战斗状态从单一入口选择动画层，避免各状态分别设置 LayerWeight 后互相残留。
        /// </summary>
        public bool TickCombat(
            Animator animator,
            CharacterStateId stateId,
            int actionParams = 0,
            in DodgePresentationContext dodgeCtx = default,
            GuardState.GuardPhase guardPhase = GuardState.GuardPhase.Start,
            bool guardHasMove = false,
            byte attackComboStep = 1,
            bool forceRestart = false)
        {
            if (animator == null) return false;

            switch (stateId)
            {
                case CharacterStateId.PostureBroken:
                    ResetActiveCombatLayers(animator);
                    PlayReaction(
                        animator,
                        AnimatorParams.StatePostureBroken,
                        _config.postureBrokenCrossFadeDuration,
                        forceRestart);
                    _lastCombatState = stateId;
                    return true;
                case CharacterStateId.Hit:
                    ResetActiveCombatLayers(animator);
                    PlayReaction(animator, HitVariantToHash(actionParams), _config.lightHitCrossFadeDuration, forceRestart);
                    _lastCombatState = stateId;
                    return true;
                case CharacterStateId.Dead:
                    ResetActiveCombatLayers(animator);
                    PlayReaction(animator, AnimatorParams.StateDeath, _config.GetDeathCrossFadeDuration(), forceRestart);
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

        // ============ 动画层重置 ============

        public void ResetReactionLayers(Animator animator)
        {
            if (animator == null) return;
            animator.SetLayerWeight(AnimatorParams.ReactionLayerIndex, 0f);
            if (_lastCombatState is CharacterStateId.Hit or CharacterStateId.Dead or CharacterStateId.PostureBroken)
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
            _guardReactionTimer = 0f;
            _activeGuardReaction = GuardReactionType.None;
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

        // ============ 格挡受击反应 ============

        /// <summary>
        /// 格挡受击在战斗层短暂独占播放，计时结束后再交还普通 Guard，防止移动格挡的上半身层覆盖反馈。
        /// </summary>
        public bool PlayGuardReaction(
            Animator animator,
            GuardReactionType reaction,
            float hitDuration,
            float breakDuration)
        {
            if (animator == null || reaction == GuardReactionType.None)
                return false;

            int targetHash = GuardReactionToHash(reaction);
            if (targetHash == 0)
                return false;

            ResetReactionLayers(animator);
            animator.SetLayerWeight(AnimatorParams.UpperBodyLayerIndex, 0f);
            animator.SetLayerWeight(AnimatorParams.CombatLayerIndex, 1f);

            _activeGuardReaction = reaction;
            _guardReactionTimer = reaction == GuardReactionType.Break
                ? Mathf.Max(0.01f, breakDuration)
                : Mathf.Max(0.01f, hitDuration);
            _lastCombatState = CharacterStateId.Guard;
            _lastHash = targetHash;

            animator.CrossFade(targetHash, _config.guardCrossFadeDuration, AnimatorParams.CombatLayerIndex, 0f);
            return true;
        }

        public bool TickGuardReaction(Animator animator)
        {
            if (animator == null || _guardReactionTimer <= 0f)
                return false;

            _guardReactionTimer = Mathf.Max(0f, _guardReactionTimer - Time.deltaTime);
            animator.SetLayerWeight(AnimatorParams.UpperBodyLayerIndex, 0f);
            animator.SetLayerWeight(AnimatorParams.CombatLayerIndex, 1f);

            if (_guardReactionTimer <= 0f)
                _activeGuardReaction = GuardReactionType.None;

            return true;
        }

        // ============ 攻击表现 ============

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

        // ============ 格挡表现 ============

        /// <summary>
        /// 静止格挡独占全身战斗层，移动格挡改用上半身层叠加基础移动，返回值让外层决定是否继续刷新 Locomotion。
        /// </summary>
        public bool TickGuard(Animator animator, GuardState.GuardPhase phase, bool hasMove)
        {
            if (animator == null) return false;

            if (TickGuardReaction(animator))
                return true;

            ResetReactionLayers(animator);
            animator.SetLayerWeight(AnimatorParams.CombatLayerIndex, 0f);
            _lastDodgeMode = DodgeMode.None;

            // 移动期间所有格挡阶段都让出基础层，保持脚步动画连续。
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

        // ============ 闪避表现 ============

        /// <summary>
        /// 八向闪避在 CrossFade 前写入冻结 Blend 值，同一动作后续帧不因输入变化而改方向。
        /// </summary>
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

            if (targetHash == 0) return;

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

        // ============ 高优先级反应 ============

        private void PlayReaction(Animator animator, int stateHash, float crossFadeDuration, bool forceRestart)
        {
            animator.SetLayerWeight(AnimatorParams.ReactionLayerIndex, 1f);
            if (!forceRestart && stateHash == _lastHash && _lastCombatState != CharacterStateId.None)
                return;
            _lastHash = stateHash;
            animator.CrossFade(stateHash, crossFadeDuration, AnimatorParams.ReactionLayerIndex, 0f);
        }

        // ============ Animator 状态映射 ============

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

        private static int GuardReactionToHash(GuardReactionType reaction)
        {
            return reaction switch
            {
                GuardReactionType.Hit1 => AnimatorParams.StateGuardHit1,
                GuardReactionType.Hit2 => AnimatorParams.StateGuardHit2,
                GuardReactionType.Hit3 => AnimatorParams.StateGuardHit3,
                GuardReactionType.Break => AnimatorParams.StateGuardBreak,
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

        private static int HitVariantToHash(int variant)
        {
            return variant switch
            {
                2 => AnimatorParams.StateHit2,
                3 => AnimatorParams.StateHit3,
                4 => AnimatorParams.StateHit4,
                5 => AnimatorParams.StateHit5,
                _ => AnimatorParams.StateHit1,
            };
        }

        // ============ 播放缓存 ============

        private void ResetGuardCache()
        {
            _lastGuardHash = 0;
            _lastGuardLayerIndex = -1;
            _lastGuardPhase = GuardState.GuardPhase.None;
            _lastGuardIsFullBody = false;
            _guardReactionTimer = 0f;
            _activeGuardReaction = GuardReactionType.None;
        }
    }
}
