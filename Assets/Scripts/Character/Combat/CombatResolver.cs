using System;
using System.Collections;
using System.Collections.Generic;
using Character.Sync;
using Mirror;
using UnityEngine;

namespace Character.Combat
{
    /// <summary>
    /// 把物理采样、单次命中去重和权威结果广播集中在同一帧末执行，保证所有角色使用一致的战斗结算顺序。
    /// </summary>
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

        // ============ Unity 生命周期 ============

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

        // ============ 结算权威 ============

        /// <summary>
        /// 联机时只让服务器进行物理命中判定，离线场景仍允许本地运行，避免双端各自产生不同结果。
        /// </summary>
        private bool CanResolve()
        {
            if (!_serverAuthoritative) return true;

            if (!NetworkClient.active && !NetworkServer.active) return true;

            return NetworkServer.active;
        }

        // ============ 命中采样 ============

        /// <summary>
        /// 以当前攻击定义的窗口驱动物理查询，使动画状态、配置窗口和碰撞槽位共同约束有效命中。
        /// </summary>
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

        /// <summary>
        /// 使用 NonAlloc 数组遍历当前窗口的候选 HurtBox，高频攻击帧不产生临时碰撞集合。
        /// </summary>
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

        // ============ 命中事务 ============

        /// <summary>
        /// 先写入去重键再结算，防止同一帧多个 HurtBox 或重复 Collider 让一次攻击多次命中同一目标。
        /// </summary>
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

                CombatHitResult result = target.LastHitResult;
                switch (result.FinalReaction)
                {
                    case CombatReactionType.None:
                        BroadcastHealthResult(target, result);
                        break;
                    case CombatReactionType.GuardHit:
                    case CombatReactionType.GuardBreak:
                        BroadcastGuardReaction(target, result);
                        break;
                    case CombatReactionType.PostureBreak:
                        BroadcastPostureBreak(target, result);
                        break;
                    case CombatReactionType.Hit:
                    case CombatReactionType.Dead:
                        BroadcastHitReaction(target, hit, result);
                        break;
                }
            }
        }

        // ============ 权威结果广播 ============

        /// <summary>
        /// 没有动画反应的命中仍需同步绝对生命值，否则崩防期受伤会只在服务器生效。
        /// </summary>
        private void BroadcastHealthResult(
            CombatActor target,
            in CombatHitResult result)
        {
            if (!NetworkServer.active)
                return;
            if (_transport == null)
                _transport = FindFirstObjectByType<MirrorSyncTransport>();
            if (_transport == null)
                return;

            var evt = new ActionEvent(
                _nextServerCombatSeqId++,
                Time.frameCount,
                target.ActorId,
                ActionType.HealthResult,
                param: 0,
                hasHealthResult: 1,
                appliedDamage: result.AppliedHealthDamage,
                currentHp: target.CurrentHp,
                maxHp: target.MaxHp,
                healthRevision: target.HealthRevision);

            _transport.BroadcastActionFromServer(evt);
        }

        /// <summary>
        /// 崩防使用独立动作类型，让远端能够播放不可被普通 Hit 覆盖的高优先级状态。
        /// </summary>
        private void BroadcastPostureBreak(CombatActor target,
            in CombatHitResult result)
        {
            if (!NetworkServer.active) return;

            if (_transport == null) _transport = FindFirstObjectByType<MirrorSyncTransport>();

            if (_transport == null) return;

            var evt = new ActionEvent(
                _nextServerCombatSeqId++,
                Time.frameCount,
                target.ActorId,
                ActionType.PostureBreak,
                param: 0,
                hasHealthResult: 1,
                appliedDamage: result.AppliedHealthDamage,
                currentHp: target.CurrentHp,
                maxHp: target.MaxHp,
                healthRevision: target.HealthRevision
            );

            _transport.BroadcastActionFromServer(evt);
        }

        /// <summary>
        /// 格挡结果携带权威伤害与反应类型，客户端只负责表现，避免再次执行格挡减伤。
        /// </summary>
        private void BroadcastGuardReaction(
            CombatActor target,
            in CombatHitResult result)
        {
            if (!NetworkServer.active)
                return;
            if (_transport == null)
                _transport = FindFirstObjectByType<MirrorSyncTransport>();
            if (_transport == null)
                return;
            ActionType type = result.FinalReaction == CombatReactionType.GuardBreak
                ? ActionType.GuardBreak
                : ActionType.GuardHit;
            var evt = new ActionEvent(
                _nextServerCombatSeqId++,
                Time.frameCount,
                target.ActorId,
                type,
                (int)result.GuardReaction,
                hasHealthResult: 1,
                appliedDamage: result.AppliedHealthDamage,
                currentHp: target.CurrentHp,
                maxHp: target.MaxHp,
                healthRevision: target.HealthRevision);

            _transport.BroadcastActionFromServer(evt);
        }

        /// <summary>
        /// 将死亡与普通受击共用一套生命结果载荷，确保动作到达时能同时纠正累计血量误差。
        /// </summary>
        private void BroadcastHitReaction(
            CombatActor target,
            in HitInfo hit,
            in CombatHitResult result)
        {
            if (!NetworkServer.active)
                return;
            if (_transport == null)
                _transport = FindFirstObjectByType<MirrorSyncTransport>();
            if (_transport == null)
                return;

            ActionType type = result.FinalReaction == CombatReactionType.Dead
                ? ActionType.Dead
                : ActionType.Hit;
            int param = PackHitParam(
                result.AppliedHealthDamage,
                hit.isHeavyHit,
                hit.hitVariant);
            var evt = new ActionEvent(
                _nextServerCombatSeqId++,
                Time.frameCount,
                target.ActorId,
                type,
                param,
                hasHealthResult: 1,
                appliedDamage: result.AppliedHealthDamage,
                currentHp: target.CurrentHp,
                maxHp: target.MaxHp,
                healthRevision: target.HealthRevision);
            _transport.BroadcastActionFromServer(evt);
        }

        // ============ 命中去重键 ============

        /// <summary>
        /// 将攻击者、攻击实例、目标和窗口组成值键，避免依赖 Collider 引用造成多 HurtBox 重复结算。
        /// </summary>
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

        // ============ 动作参数编解码 ============

        /// <summary>
        /// 旧动作协议只有一个整型参数，因此保持位布局稳定以兼容已录制或在途的消息。
        /// </summary>
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

        // ============ 受击方向 ============

        /// <summary>
        /// 使用受击者的局部朝向选择动画变体，使网络只需同步小型编号而不必传输额外方向向量。
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
