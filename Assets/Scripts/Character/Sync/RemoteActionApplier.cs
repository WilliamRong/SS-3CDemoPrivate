using Mirror;
using UnityEngine;
using Character.Combat;
using Character.Controller;
using Character.StateMachine;

namespace Character.Sync
{
    /// <summary>
    /// 将离散动作、权威战斗结果和快照状态边沿汇聚到单个远端实体，统一处理去重与 Host 回环保护。
    /// </summary>
    public class RemoteActionApplier : MonoBehaviour
    {
        private CombatActor _combatActor;
        private NpcHealthBarView _healthBarView;

        private const int ServerCombatSeqBase = 100000;

        [Header("Debug")]
        [SerializeField] private bool _logApply = true;

        public int LastAppliedSeqId { get; private set; } = 0;
        public int LastAppliedTick { get; private set; } = 0;
        public ActionType CurrentRemoteAction { get; private set; } = ActionType.None;
        /// <summary>缓存进入动作时的模式，避免后续快照缺失时无法还原闪避表现。</summary>
        public byte LastDodgeMode { get; private set; }
        public int LastHitParam { get; private set; }
        public int LastHitSeqId { get; private set; }
        public int LastPostureBreakSeqId { get; private set; }
        public int LastParrySeqId { get; private set; }
        public int LastParriedSeqId { get; private set; }
        public GuardReactionType LastGuardReaction { get; private set; } = GuardReactionType.None;
        public int LastGuardReactionSeqId { get; private set; }

        private int _consumedGuardReactionSeqId;
        private int _lastServerCombatSeqId;
        private bool _postureBreakAwaitingSnapshot;
        private bool _postureBreakSnapshotObserved;
        private bool _parryAwaitingSnapshot;
        private bool _parrySnapshotObserved;
        private bool _parriedAwaitingSnapshot;
        private bool _parriedSnapshotObserved;

        private NetworkIdentity _networkIdentity;
        private PlayerController _playerController;

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            _networkIdentity = GetComponent<NetworkIdentity>();
            _playerController = GetComponent<PlayerController>();
            _combatActor = GetComponent<CombatActor>();
            _healthBarView = GetComponent<NpcHealthBarView>();
        }

        // ============ 动作入口 ============

        /// <summary>
        /// 服务器战斗序列、格挡序列和普通动作序列分别去重，避免不同发布源的编号空间互相吞掉消息。
        /// </summary>
        public void Apply(ActionEvent evt)
        {
            if (_networkIdentity != null && _networkIdentity.netId != 0
                && evt.ActorId != (int)_networkIdentity.netId)
            {
                return;
            }


            bool isServerCombatResult =
                evt.SeqId >= ServerCombatSeqBase &&
                evt.HasHealthResult != 0;

            if (evt.SeqId >= ServerCombatSeqBase && evt.Type == ActionType.Parried)
            {
                ApplyServerParried(evt);
                return;
            }

            if (isServerCombatResult)
            {
                ApplyServerCombatResult(evt);
                return;
            }


            if (evt.Type is ActionType.GuardHit or ActionType.GuardBreak)
            {
                ApplyGuardReaction(evt);
                return;
            }


            if (evt.SeqId <= LastAppliedSeqId)
            {
                if (_logApply) Debug.Log($"RemoteActionApplier: Ignore duplicate seqId {evt.SeqId}");
                return;
            }

            LastAppliedSeqId = evt.SeqId;
            LastAppliedTick = evt.Tick;

            switch (evt.Type)
            {
                case ActionType.AttackStart:
                    CurrentRemoteAction = ActionType.AttackStart;
                    break;
                case ActionType.DodgeStart:
                    CurrentRemoteAction = ActionType.DodgeStart;
                    LastDodgeMode = (byte)evt.Param;
                    break;
                case ActionType.Hit:
                    CurrentRemoteAction = ActionType.Hit;
                    LastHitParam = evt.Param;
                    LastHitSeqId = evt.SeqId;
                    break;
                case ActionType.PostureBreak:
                    CurrentRemoteAction = ActionType.PostureBreak;
                    RegisterPostureBreakEdge(evt.SeqId);
                    break;
                case ActionType.ParryStart:
                    CurrentRemoteAction = ActionType.ParryStart;
                    RegisterParryEdge(evt.SeqId);
                    break;
                case ActionType.Parried:
                    CurrentRemoteAction = ActionType.Parried;
                    RegisterParriedEdge(evt.SeqId);
                    break;
                case ActionType.Dead:
                    CurrentRemoteAction = ActionType.Dead;
                    ClearPostureBreakTracking();
                    ClearParryTracking();
                    ClearParriedTracking();
                    break;
                case ActionType.Revive:
                    CurrentRemoteAction = ActionType.Revive;
                    ClearPostureBreakTracking();
                    ClearParryTracking();
                    ClearParriedTracking();
                    break;
                default:
                    CurrentRemoteAction = ActionType.None;
                    break;
            }

            if (_logApply)
                Debug.Log($"[RemoteActionApplier] applied {evt}");
        }

        // ============ 权威战斗结果 ============

        /// <summary>
        /// 观察端统一显示受击者世界血条；Host 已直接结算，只记录表现边沿而不再次应用生命伤害。
        /// </summary>
        private void ApplyServerCombatResult(ActionEvent evt)
        {
            if (evt.SeqId <= _lastServerCombatSeqId)
            {
                if (_logApply) Debug.Log($"RemoteActionApplier: Ignore duplicate server combat seqId {evt.SeqId}");
                return;
            }

            _lastServerCombatSeqId = evt.SeqId;
            LastAppliedTick = evt.Tick;
            CurrentRemoteAction = evt.Type;

            bool isHealthOnly = evt.Type == ActionType.HealthResult;
            bool isPostureBreak = evt.Type == ActionType.PostureBreak;
            bool isGuard =
                evt.Type == ActionType.GuardHit ||
                evt.Type == ActionType.GuardBreak;

            if (isPostureBreak)
            {
                RegisterPostureBreakEdge(evt.SeqId);
            }
            else if (isGuard)
            {
                LastGuardReaction = evt.Type == ActionType.GuardBreak
                    ? GuardReactionType.Break
                    : ToGuardReaction(evt.Param);
                LastGuardReactionSeqId = evt.SeqId;
            }
            else if (!isHealthOnly)
            {
                LastHitParam = evt.Param;
                LastHitSeqId = evt.SeqId;
            }

            if (evt.Type == ActionType.Dead)
                ClearPostureBreakTracking();

            // 即使格挡伤害为零，观察者也应看到被命中者的世界血条。
            _healthBarView?.ShowForHit();

            // Host/Server 端 CombatResolver 已直接应用伤害，跳过以防二次扣血。
            if (NetworkServer.active)
                return;

            // 使用绝对生命值纠正累计误差，revision 会拒绝乱序旧结果。
            if (_combatActor != null && evt.HasHealthResult != 0)
            {
                _combatActor.ApplyAuthoritativeHealth(
                    evt.CurrentHp,
                    evt.MaxHp,
                    evt.HealthRevision);
            }

            if (isPostureBreak &&
                _playerController != null &&
                _networkIdentity != null &&
                _networkIdentity.isLocalPlayer)
            {
                _playerController.TryEnterPostureBroken();
            }
            else if (!isGuard && !isHealthOnly &&
                _playerController != null &&
                _networkIdentity != null &&
                _networkIdentity.isLocalPlayer)
            {
                CombatResolver.UnpackHitParam(
                    evt.Param,
                    out _,
                    out bool isHeavyHit,
                    out byte hitVariant);

                _playerController.ApplyRemoteHitReaction(
                    isHeavyHit,
                    hitVariant,
                    evt.Type == ActionType.Dead);
            }

            if (_logApply)
            {
                Debug.Log(
                    $"[RemoteActionApplier] combat result type={evt.Type}, " +
                    $"damage={evt.AppliedDamage:F1}, " +
                    $"hp={evt.CurrentHp:F1}/{evt.MaxHp:F1}, " +
                    $"revision={evt.HealthRevision}");
            }
        }

        // ============ 快照状态协调 ============

        /// <summary>
        /// 崩防动作可能先于快照到达，在观察到对应快照前锁存状态，避免中间普通快照让动画提前退出。
        /// </summary>
        private void ApplyServerParried(ActionEvent evt)
        {
            if (evt.SeqId <= _lastServerCombatSeqId)
                return;

            _lastServerCombatSeqId = evt.SeqId;
            LastAppliedTick = evt.Tick;
            CurrentRemoteAction = ActionType.Parried;
            RegisterParriedEdge(evt.SeqId);

            // The server event is authoritative for an owned Player as well.  Enter
            // the local FSM immediately so stale Attack input/snapshots cannot win.
            _combatActor?.CancelCurrentAttack();

            if (_playerController != null &&
                _networkIdentity != null &&
                _networkIdentity.isLocalPlayer &&
                _playerController.CurrentStateId != CharacterStateId.Parried)
            {
                _playerController.TryEnterParried();
            }
        }

        public CharacterStateId ResolveSnapshotState(CharacterStateId snapshotState)
        {
            if (snapshotState == CharacterStateId.Dead)
            {
                ClearPostureBreakTracking();
                ClearParryTracking();
                ClearParriedTracking();
                return CharacterStateId.Dead;
            }

            if (snapshotState == CharacterStateId.Parried)
            {
                ClearParryTracking();
                _parriedAwaitingSnapshot = false;
                _parriedSnapshotObserved = true;
                return CharacterStateId.Parried;
            }

            if (_parriedAwaitingSnapshot && !_parriedSnapshotObserved)
                return CharacterStateId.Parried;

            if (_parriedSnapshotObserved)
                ClearParriedTracking();

            if (snapshotState == CharacterStateId.PostureBroken)
            {
                _postureBreakAwaitingSnapshot = false;
                _postureBreakSnapshotObserved = true;
                return CharacterStateId.PostureBroken;
            }

            if (_postureBreakAwaitingSnapshot && !_postureBreakSnapshotObserved)
                return CharacterStateId.PostureBroken;

            if (_postureBreakSnapshotObserved)
                ClearPostureBreakTracking();

            if (snapshotState == CharacterStateId.Parry)
            {
                _parryAwaitingSnapshot = false;
                _parrySnapshotObserved = true;
                return CharacterStateId.Parry;
            }

            if (_parryAwaitingSnapshot && !_parrySnapshotObserved)
                return CharacterStateId.Parry;

            if (_parrySnapshotObserved)
                ClearParryTracking();

            return snapshotState;
        }

        // ============ 格挡反应 ============

        /// <summary>
        /// 非权威旧格式格挡事件使用独立序列去重，兼容仍未携带生命结果的发送路径。
        /// </summary>
        private void ApplyGuardReaction(ActionEvent evt)
        {
            if (evt.SeqId <= LastGuardReactionSeqId)
            {
                if (_logApply) Debug.Log($"RemoteActionApplier: Ignore duplicate guard seqId {evt.SeqId}");
                return;
            }

            LastAppliedTick = evt.Tick;
            CurrentRemoteAction = evt.Type;
            LastGuardReaction = evt.Type == ActionType.GuardBreak
                ? GuardReactionType.Break
                : ToGuardReaction(evt.Param);
            LastGuardReactionSeqId = evt.SeqId;

            if (_logApply)
                Debug.Log($"[RemoteActionApplier] applied {evt}");
        }

        public bool TryConsumeGuardReaction(out GuardReactionType reaction)
        {
            reaction = LastGuardReaction;
            if (reaction == GuardReactionType.None)
                return false;

            if (LastGuardReactionSeqId <= _consumedGuardReactionSeqId)
                return false;

            _consumedGuardReactionSeqId = LastGuardReactionSeqId;
            return true;
        }

        // ============ 状态重置与边沿跟踪 ============

        /// <summary>
        /// 网络对象复用或重新绑定时必须清空全部序列水位，否则新生命周期的小序号会被当作旧消息丢弃。
        /// </summary>
        public void ResetState()
        {
            LastAppliedSeqId = 0;
            LastAppliedTick = 0;
            CurrentRemoteAction = ActionType.None;
            LastDodgeMode = 0;
            LastHitParam = 0;
            LastHitSeqId = 0;
            LastPostureBreakSeqId = 0;
            LastParrySeqId = 0;
            LastParriedSeqId = 0;
            LastGuardReaction = GuardReactionType.None;
            LastGuardReactionSeqId = 0;
            _consumedGuardReactionSeqId = 0;
            _lastServerCombatSeqId = 0;
            _postureBreakAwaitingSnapshot = false;
            _postureBreakSnapshotObserved = false;
            _parryAwaitingSnapshot = false;
            _parrySnapshotObserved = false;
            _parriedAwaitingSnapshot = false;
            _parriedSnapshotObserved = false;
        }

        private void RegisterPostureBreakEdge(int seqId)
        {
            // CombatResolver 与 NPC publisher 可能描述同一次进入，只锁存第一个边沿。
            if (_postureBreakAwaitingSnapshot || _postureBreakSnapshotObserved)
                return;

            LastPostureBreakSeqId = seqId;
            _postureBreakAwaitingSnapshot = true;
            _postureBreakSnapshotObserved = false;
        }

        private void ClearPostureBreakTracking()
        {
            _postureBreakAwaitingSnapshot = false;
            _postureBreakSnapshotObserved = false;
        }

        private void RegisterParryEdge(int seqId)
        {
            if (seqId <= LastParrySeqId)
                return;

            LastParrySeqId = seqId;
            _parryAwaitingSnapshot = true;
            _parrySnapshotObserved = false;
        }

        private void ClearParryTracking()
        {
            _parryAwaitingSnapshot = false;
            _parrySnapshotObserved = false;
        }

        private void RegisterParriedEdge(int seqId)
        {
            if (seqId <= LastParriedSeqId)
                return;

            LastParriedSeqId = seqId;
            _parriedAwaitingSnapshot = true;
            _parriedSnapshotObserved = false;
        }

        private void ClearParriedTracking()
        {
            _parriedAwaitingSnapshot = false;
            _parriedSnapshotObserved = false;
        }

        private static GuardReactionType ToGuardReaction(int param)
        {
            GuardReactionType reaction = (GuardReactionType)param;
            return reaction is GuardReactionType.Hit1
                or GuardReactionType.Hit2
                or GuardReactionType.Hit3
                or GuardReactionType.Break
                ? reaction
                : GuardReactionType.Hit1;
        }
    }
}
