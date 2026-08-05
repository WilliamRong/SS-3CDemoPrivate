using Character.Combat;
using Character.Presentation;
using Character.StateMachine;
using UnityEngine;

namespace Character.Config
{
    /// <summary>
    /// 把战斗逻辑时长和数值集中为权威数据，避免状态机、结算和表现各自维护默认值。
    /// </summary>
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

        [Header("Parry")]
        [Min(1)] public int parrySampleRate = 60;
        [Min(0)] public int parryActiveStartFrame = 8;
        [Min(1)] public int parryActiveEndFrame = 22;
        [Min(1)] public int parryTotalFrames = 34;
        [Min(0.01f)] public float parriedDuration = 64f / 60f;

        public float ParryActiveStartTime =>
         parryActiveStartFrame / (float)parrySampleRate;

        public float ParryActiveEndTime =>
            parryActiveEndFrame / (float)parrySampleRate;

        public float ParryTotalDuration =>
            parryTotalFrames / (float)parrySampleRate;


        [Header("Posture")]
        [Min(0.01f)]
        public float maxPosture = 100f;
        [Min(0f)]
        public float postureRecoveryDelay = 2f;
        [Min(0f)]
        public float postureRecoveryPerSecondAtLowHealth = 5f;
        [Min(0f)]
        public float postureRecoveryPerSecondAtFullHealth = 15f;
        [Min(0.01f)]
        public float postureBreakDuration = 1.067f;

        [Header("Hit Interrupt Windows")]
        public float hitPreHitEnd = 0.1f;
        public float hitActiveEnd = 0.2f;
        public float hitRecoveryEnd = 0.8166667f;

        // ============ 攻击数据查询 ============

        public bool TryGetAttackDefinition(AttackMoveId attackStep, out AttackDefinition definition)
        {
            definition = null;
            return attackSet != null && attackSet.TryGet(attackStep, out definition);
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

        // ============ 闪避时长查询 ============

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

        // ============ 状态窗口查询 ============

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

        // ============ 编辑器数据约束 ============

        /// <summary>
        /// 在资产写入时修正边界，使运行时和网络端都不必重复防御非法配置。
        /// </summary>
        private void OnValidate()
        {
            maxHp = Mathf.Max(1f, maxHp);
            maxPosture = Mathf.Max(0.01f, maxPosture);
            postureRecoveryDelay = Mathf.Max(0f, postureRecoveryDelay);
            postureRecoveryPerSecondAtLowHealth =
                Mathf.Max(0f, postureRecoveryPerSecondAtLowHealth);
            postureRecoveryPerSecondAtFullHealth = Mathf.Max(
                postureRecoveryPerSecondAtLowHealth,
                postureRecoveryPerSecondAtFullHealth);
            postureBreakDuration = Mathf.Max(0.01f, postureBreakDuration);

            parrySampleRate = Mathf.Max(1, parrySampleRate);
            parryTotalFrames = Mathf.Max(1, parryTotalFrames);
            parryActiveStartFrame = Mathf.Clamp(parryActiveStartFrame, 0, parryTotalFrames - 1);
            parryActiveEndFrame = Mathf.Clamp(
                parryActiveEndFrame,
                parryActiveStartFrame + 1,
                parryTotalFrames);
            parriedDuration = Mathf.Max(0.01f, parriedDuration);
        }
    }
}
