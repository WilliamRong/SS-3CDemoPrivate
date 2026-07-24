using System;
using System.Collections;
using System.Collections.Generic;
using Character.Sync;
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


        [SerializeField] private MirrorSyncTransport _transport;
        private int _nextServerCombatSeqId = 100000;

        private Collider[] _overlapResults;
        private readonly HashSet<HitKey> _resolvedHits = new();

        private void Awake()
        {
            _overlapResults = new Collider[Mathf.Max(4, _maxOverlapResults)];
        }

        private void Update()
        {
            if (!CanResolve()) return;

            ResolveAllActiveHitBoxes();
        }

        private void OnDisable()
        {
            _resolvedHits.Clear();
        }

        private bool CanResolve()
        {
            if (!_serverAuthoritative) return true;

            if (!NetworkClient.active && !NetworkServer.active) return true;

            return NetworkServer.active;
        }

        private void ResolveAllActiveHitBoxes()
        {
            var hitBoxes = CombatHitBox.ActiveHitBoxes;

            for (int i = 0; i < hitBoxes.Count; i++)
            {
                CombatHitBox hitBox = hitBoxes[i];
                if (hitBox == null || !hitBox.IsConfigured()) continue;

                CombatActor attacker = hitBox.Owner;
                if (attacker == null || !attacker.TryGetCurrentAttack(out var attack)) continue;

                AttackDefinition definition = attack.definition;
                if (definition.hitWindows == null) continue;

                for (int windowIndex = 0; windowIndex < definition.hitWindows.Length; windowIndex++)
                {
                    AttackHitWindow window = definition.hitWindows[windowIndex];

                    if (!window.IsValid) continue;

                    if (window.slot != hitBox.Slot) continue;

                    if (!definition.IsWindowActive(windowIndex, attack.elapsedTime)) continue;

                    ResolveWindow(hitBox, attack, window, windowIndex);
                }
            }
        }

        private void ResolveWindow(CombatHitBox hitBox, AttackRuntimeInfo attack, AttackHitWindow window, int windowIndex)
        {
            int count = hitBox.OverlapHurtBoxesNonAlloc(_overlapResults);

            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapResults[i];
                if (col == null) continue;

                CombatHurtBox hurtBox = col.GetComponent<CombatHurtBox>();
                if (hurtBox == null) hurtBox = col.GetComponentInParent<CombatHurtBox>();
                if (hurtBox == null || !hurtBox.TryGetOwner(out CombatActor target)) continue;

                TryResolveHit(hitBox, hurtBox, target, attack, window, windowIndex);
            }
        }

        private void TryResolveHit(CombatHitBox hitBox, CombatHurtBox hurtBox, CombatActor target, AttackRuntimeInfo attack, AttackHitWindow window, int windowIndex)
        {
            CombatActor attacker = attack.owner;

            if (attacker == null || target == null) return;

            if (attacker == target) return;

            if (attacker.TeamId != 0 && attacker.TeamId == target.TeamId) return;

            if (!target.CanReceiveHit) return;

            int hitWindowKey = attack.definition.hitSameTargetOnce ? -1 : windowIndex;
            var key = new HitKey(attacker.ActorId, attack.attackInstanceId, target.ActorId, hitWindowKey);

            if (_resolvedHits.Contains(key)) return;

            _resolvedHits.Add(key);

            Vector3 direction = target.transform.position - attacker.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) direction.Normalize();
            else direction = attacker.transform.forward;

byte hitVariant = CalculateHitVariant(target, attacker.transform.position);


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
                hitVariant = hitVariant,
                hitPoint = hurtBox.transform.position,
                hitDirection = direction
            };

            if (_logValidHits)
            {
                Debug.Log($"[CombatResolver] Hit resolved: {attacker.name} -> {target.name} | Damage: {damage} | Window: {windowIndex} | Attack: {attack.attackId} | Instance: {attack.attackInstanceId}");
            }

            if (_applyDamage)
            {
                bool applied = target.ApplyHit(hit);
                if (!applied)
                    return;

                if (target.LastGuardReactionType != GuardReactionType.None)
                {
                    BroadcastGuardReaction(target);
                    return;
                }

                BroadcastHitReaction(target, hit);
            }


        }



        private void BroadcastGuardReaction(CombatActor target)
        {
            if (!NetworkServer.active)
                return;
            if (_transport == null)
                _transport = FindFirstObjectByType<MirrorSyncTransport>();
            if (_transport == null)
                return;
            ActionType type = target.LastGuardReactionType == GuardReactionType.Break
                ? ActionType.GuardBreak
                : ActionType.GuardHit;
            var evt = new ActionEvent(
                _nextServerCombatSeqId++,
                Time.frameCount,
                target.ActorId,
                type,
                (int)target.LastGuardReactionType);
            _transport.BroadcastActionFromServer(evt);
        }

        private void BroadcastHitReaction(CombatActor target, in HitInfo hit)
        {
            if (!NetworkServer.active)
                return;
            if (_transport == null)
                _transport = FindFirstObjectByType<MirrorSyncTransport>();
            if (_transport == null)
                return;

            ActionType type = target.IsDead ? ActionType.Dead : ActionType.Hit;
            int param = PackHitParam(hit.damage, hit.isHeavyHit, hit.hitVariant);
            var evt = new ActionEvent(
                _nextServerCombatSeqId++,
                Time.frameCount,
                target.ActorId,
                type,
                param);
            _transport.BroadcastActionFromServer(evt);
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

        // 伤害编码：bit0=isHeavyHit, bit1-3=hitVariant(0-7), bit4+=damage*10
        public static int PackHitParam(float damage, bool isHeavyHit, byte hitVariant = 1)
        {
            int damageInt = (int)(Mathf.Clamp(damage, 0f, 9999f) * 10f);
            return (damageInt << 4) | ((hitVariant & 0x7) << 1) | (isHeavyHit ? 1 : 0);
        }

        public static void UnpackHitParam(int param, out float damage, out bool isHeavyHit, out byte hitVariant)
        {
            isHeavyHit = (param & 1) == 1;
            hitVariant = (byte)((param >> 1) & 0x7);
            if (hitVariant == 0) hitVariant = 1; // 兜底：默认 Hit1
            damage = (param >> 4) / 10f;
        }
        /// <summary>
        /// 根据攻击方向（相对受击者的朝向）选择受击动画变体
        /// 正面→Hit3,  左侧→Hit1,  右侧→Hit2,  背面上方→Hit4,  背面中间→Hit5
        /// </summary>
        private static byte CalculateHitVariant(CombatActor target, Vector3 attackerPos)
        {
            Vector3 targetForward = target.transform.forward;
            targetForward.y = 0f;
            if (targetForward.sqrMagnitude < 0.0001f) targetForward = Vector3.forward;
            targetForward.Normalize();

            Vector3 targetRight = target.transform.right;
            targetRight.y = 0f;
            targetRight.Normalize();

            // 从受击者指向攻击者
            Vector3 toAttacker = attackerPos - target.transform.position;
            Vector3 toAttackerFlat = toAttacker;
            toAttackerFlat.y = 0f;
            toAttackerFlat.Normalize();
            toAttacker.Normalize();

            float forwardDot = Vector3.Dot(targetForward, toAttackerFlat);
            float rightDot = Vector3.Dot(targetRight, toAttackerFlat);

            // 正面攻击
            if (forwardDot >= 0.3f)
                return 3; // Hit3

            // 背面攻击
            if (forwardDot <= -0.3f)
            {
                if (toAttacker.y > 0.5f)
                    return 4; // Hit4 - 背面上方
                return 5; // Hit5 - 背面中间
            }

            // 侧面攻击
            if (rightDot >= 0f)
                return 2; // Hit2 - 攻击来自右侧
            else
                return 1; // Hit1 - 攻击来自左侧
        }
    }
}