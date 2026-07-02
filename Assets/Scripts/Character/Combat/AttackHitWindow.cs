using System;
using UnityEngine;

namespace Character.Combat
{
    [Serializable]
    public struct AttackHitWindow
    {
        [Range(0f, 1f)]
        public float startNormalized;

        [Range(0f, 1f)]
        public float endNormalized;

        public HitBoxSlot slot;
        public float damageMultiplier;
        
        public bool IsValid => slot != HitBoxSlot.None && endNormalized > startNormalized;
        public float DamageMultiplierOrDefault => damageMultiplier > 0f ? damageMultiplier : 1f;

        public bool Contains(float normalizedTime)
        {
            return IsValid && normalizedTime >= startNormalized && normalizedTime <= endNormalized;
        }

        public static AttackHitWindow Create( HitBoxSlot slot, float start, float end,float multiplier = 1f)
        {
            return new AttackHitWindow
            {
                slot = slot,
                startNormalized = start,
                endNormalized = end,
                damageMultiplier = multiplier
            };
        }
    }

    public enum HitBoxSlot: byte
    {
        None = 0,
        Katana = 1,
        Body = 2,
        Kick = 3
    }
}
