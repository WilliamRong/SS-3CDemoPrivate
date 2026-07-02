using System;
using System.Collections.Generic;
using Character.Combat;
using Character.Presentation;
using Character.StateMachine;
using UnityEngine;

namespace Character.Config
{
    [CreateAssetMenu(fileName = "CharacterCombatConfig", menuName = "SS3C/Character/Combat Config")]
    public sealed class CharacterCombatConfig : ScriptableObject
    {
        [Serializable]
        public sealed class AttackEntry
        {
            public AttackMoveId step = AttackMoveId.Combo1;
            public AttackDefinition value = new();
        }

        private const float FallbackAttackDuration = 0.8f;

        [Header("Vitals")]
        public float maxHp = 100f;

        [Header("Hit State (logic duration, seconds)")]
        public float lightHitDuration = 0.8166667f;
        public float heavyHitDuration = 0.8166667f;

        [Header("Attack Data")]
        public AttackEntry[] attackDefinitions =
        {
            CreateAttackEntry(AttackMoveId.Combo1, "Attack_Combo1", 0.8166667f, 10f, false),
            CreateAttackEntry(AttackMoveId.Combo2, "Attack_Combo2", 0.9666667f, 10f, false),
            CreateAttackEntry(AttackMoveId.Combo3, "Attack_Combo3", 0.8666667f, 10f, false),
            CreateAttackEntry(AttackMoveId.Combo4, "Attack_Combo4", 1.1500001f, 12f, false),
            CreateAttackEntry(AttackMoveId.Sprint, "Attack_Sprint", 0.9833334f, 12f, false),
            CreateAttackEntry(AttackMoveId.Dodge, "Attack_Dodge", 0.95000005f, 10f, false),
            CreateAttackEntry(AttackMoveId.Heavy1Start, "Attack_Heavy1_Start", 0.8166667f, 0f, false, false),
            CreateAttackEntry(AttackMoveId.Heavy1, "Attack_Heavy1", 0.73333335f, 18f, true),
            CreateAttackEntry(AttackMoveId.Heavy2, "Attack_Heavy2", 1.1333334f, 22f, true)
        };

        [SerializeField, HideInInspector]
        private AttackDefinitionSet attackSet;

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
        public float guardExitDuration  = 0.4f;
        
        [Header("Hit Interrupt Windows")]
        public float hitPreHitEnd = 0.1f;
        public float hitActiveEnd = 0.2f;
        public float hitRecoveryEnd = 0.8166667f;

        private readonly Dictionary<AttackMoveId, AttackDefinition> _attackLookup = new();
        private bool _attackLookupDirty = true;

        public bool TryGetAttackDefinition(AttackMoveId attackStep, out AttackDefinition definition)
        {
            RebuildAttackLookupIfNeeded();
            return _attackLookup.TryGetValue(attackStep.ClampOrDefault(), out definition);
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

        private void OnEnable()
        {
            _attackLookupDirty = true;
        }

        private void OnValidate()
        {
            _attackLookupDirty = true;
            if (attackDefinitions == null) return;

            for (int i = 0; i < attackDefinitions.Length; i++)
            {
                AttackEntry entry = attackDefinitions[i];
                if (entry == null) continue;

                entry.step = entry.step.ClampOrDefault();
                if (entry.value == null)
                {
                    entry.value = new AttackDefinition();
                }
            }
        }

        private void RebuildAttackLookupIfNeeded()
        {
            if (!_attackLookupDirty) return;

            _attackLookup.Clear();
            if (attackDefinitions != null)
            {
                for (int i = 0; i < attackDefinitions.Length; i++)
                {
                    AttackEntry entry = attackDefinitions[i];
                    if (entry == null || entry.value == null) continue;

                    _attackLookup[entry.step.ClampOrDefault()] = entry.value;
                }
            }

            if (_attackLookup.Count == 0 && attackSet != null)
            {
                ImportLegacyAttackSet();
            }

            _attackLookupDirty = false;
        }

        private void ImportLegacyAttackSet()
        {
            foreach (AttackMoveId attackStep in Enum.GetValues(typeof(AttackMoveId)))
            {
                if (attackStep == AttackMoveId.None) continue;
                if (attackSet.TryGet(attackStep, out AttackDefinition definition))
                {
                    _attackLookup[attackStep] = definition;
                }
            }
        }

        private static AttackEntry CreateAttackEntry(
            AttackMoveId step,
            string displayName,
            float duration,
            float damage,
            bool isHeavyHit,
            bool hasHitWindow = true)
        {
            return new AttackEntry
            {
                step = step,
                value = new AttackDefinition
                {
                    displayName = displayName,
                    duration = duration,
                    damage = damage,
                    isHeavyHit = isHeavyHit,
                    hitSameTargetOnce = true,
                    hitWindows = hasHitWindow
                        ? new[] { AttackHitWindow.Create(HitBoxSlot.Katana, 0.25f, 0.45f) }
                        : Array.Empty<AttackHitWindow>()
                }
            };
        }
    }
}
