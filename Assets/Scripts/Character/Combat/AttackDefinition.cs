using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Character.Combat
{
    public enum AttackMoveId : byte
    {
        None = 0,
        Combo1 = 1,
        Combo2 = 2,
        Combo3 = 3,
        Combo4 = 4,
        Sprint = 5,
        Dodge = 6,
        Heavy1Start = 7,
        Heavy1 = 8,
        Heavy2 = 9
    }

    public static class AttackMoveIdExtensions
    {
        public static AttackMoveId FromByte(byte value)
        {
            AttackMoveId attackId = (AttackMoveId)value;
            return attackId.IsDefinedAttack()
                ? attackId
                : AttackMoveId.Combo1;
        }

        public static bool IsDefinedAttack(this AttackMoveId attackId)
        {
            return attackId != AttackMoveId.None && Enum.IsDefined(typeof(AttackMoveId), attackId);
        }

        public static byte ToByte(this AttackMoveId attackId)
        {
            return (byte)attackId.ClampOrDefault();
        }

        public static AttackMoveId ClampOrDefault(this AttackMoveId attackId)
        {
            return attackId.IsDefinedAttack()
                ? attackId
                : AttackMoveId.Combo1;
        }

        public static bool IsCombo(this AttackMoveId attackId)
        {
            byte value = (byte)attackId;
            return value >= (byte)AttackMoveId.Combo1 && value <= (byte)AttackMoveId.Combo4;
        }

        public static bool CanAdvanceCombo(this AttackMoveId attackId)
        {
            byte value = (byte)attackId;
            return value >= (byte)AttackMoveId.Combo1 && value < (byte)AttackMoveId.Combo4;
        }

        public static AttackMoveId NextCombo(this AttackMoveId attackId)
        {
            return attackId switch
            {
                AttackMoveId.Combo1 => AttackMoveId.Combo2,
                AttackMoveId.Combo2 => AttackMoveId.Combo3,
                AttackMoveId.Combo3 => AttackMoveId.Combo4,
                _ => attackId
            };
        }
    }

    [Serializable]
    public sealed class AttackDefinition
    {
        public string displayName;

        [Min(0.01f)]
        public float duration = 0.8f;
        [Min(0f)]
        public float damage = 10f;
        [Min(0f)]
        public float postureDamage = 15f;
        [Min(0f)]
        public float guardedPostureDamage = 22f;
        public bool isHeavyHit;

        public bool hitSameTargetOnce = true;
        public AttackHitWindow[] hitWindows =
        {
            AttackHitWindow.Create(HitBoxSlot.Katana, 0.25f, 0.45f)
        };

        public string DebugName => string.IsNullOrWhiteSpace(displayName) ? "UnnamedAttack" : displayName;

        public float NormalizedTime(float elapsedTime)
        {
            return duration > 0.0001f ? Mathf.Clamp01(elapsedTime / duration) : 0f;
        }

        public bool IsWindowActive(int windowIndex, float elapsedTime)
        {
            if (hitWindows == null || windowIndex < 0 || windowIndex >= hitWindows.Length) return false;

            return hitWindows[windowIndex].Contains(NormalizedTime(elapsedTime));
        }


        public static AttackDefinition CreateFallback(AttackMoveId attackId, float duration)
        {
            attackId = attackId.ClampOrDefault();
            bool heavyHit = attackId is AttackMoveId.Heavy1Start
              or AttackMoveId.Heavy1
              or AttackMoveId.Heavy2;
            return new AttackDefinition
            {
                displayName = $"Fallback_Attack_{attackId}",
                duration = Mathf.Max(0.01f, duration),
                damage = 10f,
                postureDamage = heavyHit ? 30f : 15f,
                guardedPostureDamage = heavyHit ? 45f : 22f,
                isHeavyHit = heavyHit,
                hitSameTargetOnce = true,
                hitWindows = new AttackHitWindow[]
                {
                    AttackHitWindow.Create(HitBoxSlot.Katana, 0.25f, 0.45f)
                }
            };
        }
    }


    public struct AttackRuntimeInfo
    {
        public CombatActor owner;
        public AttackDefinition definition;
        public AttackMoveId attackId;
        public int attackInstanceId;
        public float elapsedTime;

        public float NormalizedTime => definition != null ? definition.NormalizedTime(elapsedTime) : 0f;
        public bool IsValid => owner != null && definition != null && attackId != AttackMoveId.None;

        public AttackRuntimeInfo(CombatActor owner, AttackDefinition definition, AttackMoveId attackId, int attackInstanceId, float elapsedTime)
        {
            this.owner = owner;
            this.definition = definition;
            this.attackId = attackId.ClampOrDefault();
            this.attackInstanceId = attackInstanceId;
            this.elapsedTime = elapsedTime;
        }
    }
}
