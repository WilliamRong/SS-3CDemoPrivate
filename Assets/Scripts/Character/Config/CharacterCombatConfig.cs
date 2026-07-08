using Character.Combat;
using Character.Presentation;
using Character.StateMachine;
using UnityEngine;

namespace Character.Config
{
    [CreateAssetMenu(fileName = "CharacterCombatConfig", menuName = "SS3C/Character/Combat Config")]
    public sealed class CharacterCombatConfig : ScriptableObject
    {
        private const float FallbackAttackDuration = 0.8f;

        [Header("Vitals")]
        public float maxHp = 100f;

        [Header("Hit State (logic duration, seconds)")]
        public float lightHitDuration = 0.8166667f;
        public float heavyHitDuration = 0.8166667f;

        [Header("Attack Data")]
        public AttackDefinitionSet attackSet;

        [Header("Attack State")]
        [Range(0f, 1f)]
        public float attackComboCancelStartRatio = 0.55f;
        public float attackDirectionSampleTime = 0.2f;
        public float attackPreHitEnd = 0.1f;
        public float attackActiveEnd = 0.25f;
        public float attackRecoveryEnd = 0.45f;

        [Header("Dodge State")]
        public float dodgeDuration = 0.2f;
        public float dodgeInvincibleStart = 0.05f;
        public float dodgeInvincibleEnd = 0.18f;
        public float dodgePreHitEnd = 0.05f;
        public float dodgeActiveEnd = 0.18f;
        public float dodgeRecoveryEnd = 0.25f;
        public float dodgeBackwardDuration = 0.7f;
        public float dodgeEvadeDuration = 0.9f;
        [Range(0.2f, 1f)]
        [Tooltip("Share of BackStep clip used for logic displacement (stops near back contact).")]
        public float dodgeBackwardMoveDurationRatio = 0.5f;
        [Range(0.2f, 1f)]
        [Tooltip("Share of Evade clip used for logic displacement (stops near back contact).")]
        public float dodgeEvadeMoveDurationRatio = 0.58f;
        [Tooltip("Horizontal travel distance over move window (BackStep).")]
        public float dodgeBackwardMoveDistance = 3f;
        [Tooltip("Horizontal travel distance over move window (Evade / eight-way).")]
        public float dodgeEvadeMoveDistance = 4f;
        [Range(0f, 1f)]
        public float dodgeAttackCancelStartRatio = 0.5f;


        [Header("Guard State")]
        public float guardStartDuration = 0.3f;
        public float guardExitDuration = 0.4f;

        [Header("Guard Block")]
        [Range(0, 360f)]
        public float guardBlockAngle = 220f;

        [Range(0, 1f)]
        public float guardDamageMultiplier = 0f;

        [Range(0f, 1f)]
        public float guardHeavyDamageMultiplier = 0.25f;

        public bool guardCanBlockHeavy = true;


        [Header("Guard Break")]
        public bool guardBreakOnHeavyHit = false;
        [Range(0f, 1f)]
        public float guardBreakDamageMultiplier = 1f;
        public float guardHitReactionDuration = 0.28f;
        public float guardBreakReactionDuration = 0.65f;

        [Header("Hit Interrupt Windows")]
        public float hitPreHitEnd = 0.1f;
        public float hitActiveEnd = 0.2f;
        public float hitRecoveryEnd = 0.8166667f;

        public bool TryGetAttackDefinition(AttackMoveId attackStep, out AttackDefinition definition)
        {
            definition = null;
            return attackSet != null && attackSet.TryGet(attackStep, out definition);
        }

        public float GetDodgeDuration(DodgeMode mode)
        {
            return mode == DodgeMode.NeutralBackward ? dodgeBackwardDuration : dodgeEvadeDuration;
        }

        public float GetDodgeMoveDuration(DodgeMode mode)
        {
            float anim = GetDodgeDuration(mode);
            float ratio = mode == DodgeMode.NeutralBackward
                ? dodgeBackwardMoveDurationRatio
                : dodgeEvadeMoveDurationRatio;
            return anim * ratio;
        }

        public float GetAttackDuration(byte attackStep)
        {
            return GetAttackDuration(AttackMoveIdExtensions.FromByte(attackStep));
        }

        public float GetAttackDuration(AttackMoveId attackId)
        {
            return TryGetAttackDefinition(attackId, out AttackDefinition definition)
                ? Mathf.Max(0.01f, definition.duration)
                : FallbackAttackDuration;
        }

        public void GetStateWindows(CharacterStateId stateId, out float preHitEnd, out float activeEnd, out float recoveryEnd)
        {
            switch (stateId)
            {
                case CharacterStateId.Attack:
                    preHitEnd = attackPreHitEnd;
                    activeEnd = attackActiveEnd;
                    recoveryEnd = attackRecoveryEnd;
                    break;
                case CharacterStateId.Dodge:
                    preHitEnd = dodgePreHitEnd;
                    activeEnd = dodgeActiveEnd;
                    recoveryEnd = dodgeRecoveryEnd;
                    break;
                case CharacterStateId.Hit:
                    preHitEnd = hitPreHitEnd;
                    activeEnd = hitActiveEnd;
                    recoveryEnd = hitRecoveryEnd;
                    break;
                default:
                    preHitEnd = 0f;
                    activeEnd = 0f;
                    recoveryEnd = 0f;
                    break;
            }
        }

    }
}
