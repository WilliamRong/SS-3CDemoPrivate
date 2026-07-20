using Character.Config;
using Character.StateMachine.States;
using UnityEngine;

namespace Character.Presentation
{
    public sealed class CharacterTurnPresenter
    {
        private readonly CharacterPresentationConfig _config;

        private IdleState.IdlePhase _lastIdlePhase = IdleState.IdlePhase.None;
        private GuardState.GuardPhase _lastGuardPhase = GuardState.GuardPhase.None;
        private int _lastHash;

        public CharacterTurnPresenter(CharacterPresentationConfig config)
        {
            _config = config;
        }

        public bool TickIdleTurn(Animator animator, IdleState.IdlePhase phase, float targetAngle)
        {
            if (animator == null) return false;

            int targetHash = phase switch
            {
                IdleState.IdlePhase.TurnLeft => AnimatorParams.StateIdleTurnLeft,
                IdleState.IdlePhase.TurnRight => AnimatorParams.StateIdleTurnRight,
                _ => 0,
            };

            if (targetHash == 0)
            {
                Reset();
                return false;
            }

            float speed = CharacterTurnPlanner.CalculateSpeed(targetAngle, _config);
            animator.SetFloat(AnimatorParams.TurnSpeed, speed);

            if (phase == _lastIdlePhase && targetHash == _lastHash)
                return true;

            _lastIdlePhase = phase;
            _lastHash = targetHash;

            if (_config != null && _config.logIdleTurnPresentation)
                Debug.Log($"[TurnPresenter] IdleTurn phase={phase} angle={targetAngle:F1} speed={speed:F2} hash={targetHash}");

            animator.CrossFade(
                targetHash,
                0.1f,
                AnimatorParams.LocomotionLayerIndex,
                0f);

            return true;
        }

        public bool TickGuardTurn(Animator animator, GuardState.GuardPhase phase, float targetAngle)
        {
            if (animator == null) return false;

            int targetHash = phase switch
            {
                GuardState.GuardPhase.TurnLeft => AnimatorParams.StateGuardTurnLeft,
                GuardState.GuardPhase.TurnRight => AnimatorParams.StateGuardTurnRight,
                _ => 0,
            };

            if (targetHash == 0)
                return false;

            float speed = CharacterTurnPlanner.CalculateSpeed(targetAngle, _config);
            animator.SetFloat(AnimatorParams.TurnSpeed, speed);

            animator.SetLayerWeight(AnimatorParams.UpperBodyLayerIndex, 0f);
            animator.SetLayerWeight(AnimatorParams.CombatLayerIndex, 1f);

            if (phase == _lastGuardPhase && targetHash == _lastHash)
                return true;

            _lastGuardPhase = phase;
            _lastHash = targetHash;

            animator.CrossFade(
                targetHash,
                _config != null ? _config.guardCrossFadeDuration : 0.1f,
                AnimatorParams.CombatLayerIndex,
                0f);

            return true;
        }


        public void Reset()
        {
            _lastIdlePhase = IdleState.IdlePhase.None;
            _lastGuardPhase = GuardState.GuardPhase.None;
            _lastHash = 0;
        }
    }
}
