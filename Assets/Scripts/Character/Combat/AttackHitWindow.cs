using System;
using UnityEngine;

namespace Character.Combat
{
    /// <summary>
    /// 使用归一化区间描述命中窗，使动画替换或播放倍率变化时数据仍可复用。
    /// </summary>
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

        public static AttackHitWindow Create(HitBoxSlot slot, float start, float end, float multiplier = 1f)
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

    /// <summary>
    /// 槽位而非 Collider 引用进入攻击数据，使同一攻击定义可以绑定不同角色预制体。
    /// </summary>
    public enum HitBoxSlot : byte
    {
        None = 0,
        Katana = 1,
        Body = 2,
        Kick = 3
    }
}
