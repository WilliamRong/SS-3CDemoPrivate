using UnityEngine;

namespace Character.Combat
{
    /// <summary>
    /// 让 Resolver 只依赖命中接收契约，具体生命、格挡和状态所有权由目标 Actor 决定。
    /// </summary>
    public interface IDamageable
    {
        bool CanReceiveHit { get; }
        bool ApplyHit(in HitInfo hit);
    }

    /// <summary>
    /// 携带一次已通过碰撞筛选的完整命中输入，使目标可以原子计算 HP、架势和最终反应。
    /// </summary>
    public struct HitInfo
    {
        public CombatActor attacker;
        public CombatActor target;
        public AttackDefinition attack;
        public AttackMoveId attackId;
        public int attackInstanceId;
        public int windowIndex;
        public HitBoxSlot slot;
        public float damage;
        public bool isHeavyHit;
        public byte hitVariant;
        public Vector3 hitPoint;
        public Vector3 hitDirection;
    }

    /// <summary>
    /// 明确结算后的唯一表现优先级，防止同一击同时发布受击、破势和死亡反应。
    /// </summary>
    public enum CombatReactionType : byte
    {
        None = 0,
        Hit = 1,
        GuardHit = 2,
        GuardBreak = 3,
        PostureBreak = 4,
        Dead = 5,
        Parry = 6,
    }

    /// <summary>
    /// 记录权威事务的实际结果，发布层无需根据请求伤害重新推导发生了什么。
    /// </summary>
    public struct CombatHitResult
    {
        public bool Applied;
        public float AppliedHealthDamage;
        public float AppliedPostureDamage;
        public bool WasParried;
        public bool WasGuarded;
        public bool WasGuardBreak;
        public bool WasPostureBroken;
        public bool CausedPostureBreak;
        public bool IsDead;
        public GuardReactionType GuardReaction;
        public CombatReactionType FinalReaction;
    }
}
