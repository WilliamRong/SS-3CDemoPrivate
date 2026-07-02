using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Character.Combat
{
    [DefaultExecutionOrder(100)]
    public class CombatResolver : MonoBehaviour
    {
        [SerializeField] private bool _serverAuthoritative = true;
        [SerializeField] private bool _applyDamage = false;
        [SerializeField] private bool _logValidHits = true;
        [SerializeField] private int _maxOverlapResults = 32;

        private Collider[] _overlapResults;
        private readonly HashSet<HitKey> _resolvedHits = new();

        private void Awake()
        {
            _overlapResults = new Collider[Mathf.Max(4, _maxOverlapResults)];
        }

        private void Update()
        {
            if(!CanResolve()) return;

            ResolveAllActiveHitBoxes();
        }

        private void OnDisable()
        {
            _resolvedHits.Clear();
        }

        private bool CanResolve()
        {
            if(!_serverAuthoritative) return true;

            if(!NetworkClient.active && !NetworkServer.active) return true;

            return NetworkServer.active;
        }

        private void ResolveAllActiveHitBoxes()
        {
            var hitBoxes = CombatHitBox.ActiveHitBoxes;

            for(int i = 0; i < hitBoxes.Count; i++)
            {
                CombatHitBox hitBox = hitBoxes[i];
                if(hitBox == null || !hitBox.IsConfigured()) continue;

                CombatActor attacker = hitBox.Owner;
                if(attacker == null || !attacker.TryGetCurrentAttack(out var attack)) continue;

                AttackDefinition definition = attack.definition;
                if(definition.hitWindows == null) continue;

                for(int windowIndex = 0; windowIndex < definition.hitWindows.Length; windowIndex++)
                {
                    AttackHitWindow window = definition.hitWindows[windowIndex];

                    if(!window.IsValid) continue;

                    if(window.slot != hitBox.Slot) continue;

                    if(!definition.IsWindowActive(windowIndex, attack.elapsedTime)) continue;

                    ResolveWindow(hitBox, attack, window, windowIndex);
                }
            }
        }

        private void ResolveWindow(CombatHitBox hitBox, AttackRuntimeInfo attack, AttackHitWindow window, int windowIndex)
        {
            int count = hitBox.OverlapHurtBoxesNonAlloc(_overlapResults);

            for(int i = 0; i < count; i++)
            {
                Collider col = _overlapResults[i];
                if(col == null) continue;

                CombatHurtBox hurtBox = col.GetComponent<CombatHurtBox>();
                if(hurtBox == null) hurtBox = col.GetComponentInParent<CombatHurtBox>();
                if(hurtBox == null || !hurtBox.TryGetOwner(out CombatActor target)) continue;

                TryResolveHit(hitBox, hurtBox, target, attack, window, windowIndex);
            }
        }

        private void TryResolveHit(CombatHitBox hitBox, CombatHurtBox hurtBox, CombatActor target, AttackRuntimeInfo attack, AttackHitWindow window, int windowIndex)
        {
            CombatActor attacker = attack.owner;

            if(attacker == null || target == null) return;

            if(attacker == target) return;

            if(attacker.TeamId != 0 && attacker.TeamId == target.TeamId) return;

            if(!target.CanReceiveHit) return;

            int hitWindowKey = attack.definition.hitSameTargetOnce ? -1: windowIndex;
            var key = new HitKey(attacker.ActorId, attack.attackInstanceId, target.ActorId, hitWindowKey);

            if(_resolvedHits.Contains(key)) return;

            _resolvedHits.Add(key);

            Vector3 direction = target.transform.position - attacker.transform.position;
            direction.y = 0f;
            if(direction.sqrMagnitude > 0.0001f) direction.Normalize();
            else direction = attacker.transform.forward;

            float damage = attack.definition.damage * window.DamageMultiplierOrDefault * hurtBox.DamageMultiplier;

            var hit = new HitInfo
            {
                attacker = attacker,
                target = target,
                attack = attack.definition,
                attackId = attack.attackId,
                attackInstanceId = attack.attackInstanceId,
                windowIndex = windowIndex,
                slot = window.slot,
                damage = damage,
                isHeavyHit = attack.definition.isHeavyHit,
                hitPoint = hurtBox.transform.position,
                hitDirection = direction
            };

            if(_logValidHits)
            {
                Debug.Log($"[CombatResolver] Hit resolved: {attacker.name} -> {target.name} | Damage: {damage} | Window: {windowIndex} | Attack: {attack.attackId} | Instance: {attack.attackInstanceId}");
            }

            if(_applyDamage)
            {
                target.ApplyHit(hit);
            }


        }



        private readonly struct HitKey : IEquatable<HitKey>
        {

            private readonly int _attackerId;
            private readonly int _attackInstanceId;
            private readonly int _targetId;
            private readonly int _windowKey;

            public HitKey(int attackerId, int attackInstanceId, int targetId, int windowKey)
            {
                _attackerId = attackerId;
                _attackInstanceId = attackInstanceId;
                _targetId = targetId;
                _windowKey = windowKey;
            }


            public bool Equals(HitKey other)
            {
                return _attackerId == other._attackerId
                    && _attackInstanceId == other._attackInstanceId
                    && _targetId == other._targetId
                    && _windowKey == other._windowKey;
            }

            public override bool Equals(object obj)
            {
                return obj is HitKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + _attackerId;
                    hash = hash * 31 + _attackInstanceId;
                    hash = hash * 31 + _targetId;
                    hash = hash * 31 + _windowKey;
                    return hash;
                }
            }
        }
    }


}
