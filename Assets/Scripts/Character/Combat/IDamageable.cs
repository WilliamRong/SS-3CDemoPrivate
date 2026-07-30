using UnityEngine;

namespace Character.Combat
{
    public interface IDamageable 
    {
       bool CanReceiveHit {get;}
       bool ApplyHit(in HitInfo hit);
    }

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

    public enum CombatReactionType: byte
    {
        None = 0,
        Hit = 1,
        GuardHit = 2,
        GuardBreak = 3,
        PostureBreak = 4,
        Dead = 5,
    }

    public struct CombatHitResult
    {
        public bool Applied;
        public float AppliedHealthDamage;
        public float AppliedPostureDamage;
        public bool WasGuarded;
        public bool WasGuardBreak;
        public bool WasPostureBroken;
        public bool CausedPostureBreak;
        public bool IsDead;
        public GuardReactionType GuardReaction;
        public CombatReactionType FinalReaction;
    }
}
