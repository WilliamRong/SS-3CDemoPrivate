using Character.Config;
using Character.StateMachine;
using Character.StateMachine.States;
using UnityEngine;

namespace Character.Presentation
{
    /// <summary>
    /// 冲刺阶段由状态层计时，表现层只映射离散动画并缓存最后结果，避免 Animator 反向主导玩法状态。
    /// </summary>
    public sealed class CharacterSprintPresenter
    {
        private readonly CharacterPresentationConfig _config;
        private int _lastAnimatorHash;
        private SprintState.SprintPhase _lastPhase = (SprintState.SprintPhase)(-1);

        public CharacterSprintPresenter(CharacterPresentationConfig config)
        {
            _config = config;
        }

        // ============ 阶段入口 ============

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

        // ============ Animator 映射 ============

        /// <summary>
        /// 本地与远端阶段最终进入同一映射路径，保证两种同步来源不会使用不同淡入规则。
        /// </summary>
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
                _config.sprintCrossFadeDuration,
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
