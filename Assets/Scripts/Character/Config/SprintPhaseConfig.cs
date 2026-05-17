using Character.StateMachine.States;
using UnityEngine;

namespace Character.Config
{
    [CreateAssetMenu(fileName = "SprintPhaseConfig", menuName = "SS3C/Character/Sprint Phase Config")]
    public class SprintPhaseConfig : ScriptableObject
    {
        [Header("Presentation")]
        [Tooltip("Extra time after clip length before the FSM advances the phase (not applied to Turn180).")]
        public float crossfadePadding = 0.15f;

        [Header("One-Shot Durations (seconds)")]
        public float startDuration = 0.5f;
        public float brakeDuration = 0.15f;
        public float turn180Duration = 0.2f;

        [Header("Turn180 Input (raw stick)")]
        public float turn180Cooldown = 0.5f;
        [Tooltip("How long the last stick direction is kept after input goes neutral.")]
        public float inputBufferDuration = 0.5f;
        [Tooltip("Min magnitude to write buffer or to count as opposite input.")]
        public float minOppositeInputMagnitude = 0.2f;
        [Range(0f, 1f)]
        [Tooltip("Dot below -threshold counts as opposite (0.2 ≈ >101°).")]
        public float oppositeInputDotThreshold = 0.2f;

        public float GetOneShotDuration(SprintState.SprintPhase phase)
        {
            switch (phase)
            {
                case SprintState.SprintPhase.Start:
                    return startDuration;
                case SprintState.SprintPhase.Brake:
                    return brakeDuration;
                case SprintState.SprintPhase.Turn180:
                    return turn180Duration;
                default:
                    return 0f;
            }
        }

        public bool IsOneShot(SprintState.SprintPhase phase)
        {
            return phase == SprintState.SprintPhase.Start
                || phase == SprintState.SprintPhase.Brake
                || phase == SprintState.SprintPhase.Turn180;
        }

        public float GetPhaseCompleteDuration(SprintState.SprintPhase phase)
        {
            float padding = phase == SprintState.SprintPhase.Turn180 ? 0f : crossfadePadding;
            return GetOneShotDuration(phase) + padding;
        }
    }
}
