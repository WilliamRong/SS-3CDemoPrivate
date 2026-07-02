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
        public Vector3 hitPoint;
        public Vector3 hitDirection;
    }
}
