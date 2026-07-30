using UnityEngine;

namespace Character.Config
{
    [CreateAssetMenu(fileName = "CharacterPresentationConfig", menuName = "SS3C/Character/Presentation Config")]
    public sealed class CharacterPresentationConfig : ScriptableObject
    {
        [Header("Locomotion Animator")]
        public float locomotionCrossFadeDuration = 0.15f;
        public float locomotionEnterCrossFadeDuration = 0.08f;
        [Tooltip("World speed mapped to full run on blend tree forward axis.")]
        public float locomotionBlendReferenceSpeed = 5f;
        public float velocityEpsilon = 0.05f;

        [Header("Blend Tree")]
        public float freeMoveBlendScale = 1f;
        public float blendAxisMax = 1f;
        public float runForwardBlendZ = 1f;

        [Header("Sprint Animator")]
        public float sprintCrossFadeDuration = 0.15f;

        [Header("Combat Animator (CrossFade duration, seconds)")]
        public float lightHitCrossFadeDuration = 0.08f;
        public float heavyHitCrossFadeDuration = 0.08f;
        public float deathCrossFadeDuration = 0.08f;
        [Header("Dodge Animator")]
        public float dodgeCrossFadeDuration = 0.08f;
        [Tooltip("Eight-way dodge blend tree axis scale (matches DirectionalDodge tree positions, often 1).")]
        public float dodgeBlendAxisMax = 1f;
        
        [Header("Guard Animator")]
        public float guardCrossFadeDuration = 0.1f;

        [Header("Turn Animator")]
        [Tooltip("Minimum facing angle delta, in degrees, required to start an idle/guard turn animation.")]
        [Range(1f, 90f)]
        public float turnTriggerAngle = 20f;

        [Tooltip("Maximum yaw degrees applied by one idle/guard turn step. Larger values let one turn phase face the current target direction.")]
        [Range(1f, 180f)]
        public float turnStepAngle = 180f;

        [Tooltip("Designed angle for the Turn90 clip, used to normalize playback speed.")]
        public float turnAnimationAngle = 90f;

        [Tooltip("Playback speed for smaller turns.")]
        [Range(0.8f, 2f)]
        public float turnSpeedMultiplierMax = 1.3f;

        [Tooltip("Playback speed for larger turns.")]
        [Range(0.3f, 1f)]
        public float turnSpeedMultiplierMin = 1f;

        [Tooltip("Cooldown after a turn animation completes. Keep this short so lock-on idle can keep following a moving target.")]
        public float turnCooldown = 0.2f;

        [Tooltip("Angle tolerance where the current turn can be considered aligned.")]
        [Range(1f, 30f)]
        public float turnAngleTolerance = 3f;

        [Tooltip("Base clip duration for Idle Turn90 before TurnSpeed is applied.")]
        public float idleTurnDuration = 0.5f;

        [Tooltip("Log idle turn phase and animator CrossFade calls for debugging.")]
        public bool logIdleTurnPresentation = false;

        [Header("Attack Animator")]
        public float attackCrossFadeDuration = 0.1f;

        [Header("PostureBroken")]
        public float postureBrokenCrossFadeDuration = 0.08f;

        public float GetHitCrossFadeDuration(bool heavyHit)
        {
            return heavyHit ? heavyHitCrossFadeDuration : lightHitCrossFadeDuration;
        }

        public float GetDeathCrossFadeDuration() => deathCrossFadeDuration;
    }
}
