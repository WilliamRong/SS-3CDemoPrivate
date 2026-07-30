using Mirror;
using UnityEngine;
using Character.Combat;
using Character.Controller;
using Character.StateMachine;

namespace Character.Sync
{
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
        /// <summary>Last dodge mode from <see cref="ActionEvent.Param"/> on <see cref="ActionType.DodgeStart"/>.</summary>
        public byte LastDodgeMode { get; private set; }
        public int LastHitParam { get; private set; }
        public int LastHitSeqId { get; private set; }
        public int LastPostureBreakSeqId { get; private set; }
        public GuardReactionType LastGuardReaction { get; private set; } = GuardReactionType.None;
        public int LastGuardReactionSeqId { get; private set; }

        private int _consumedGuardReactionSeqId;
        private int _lastServerCombatSeqId;
        private bool _postureBreakAwaitingSnapshot;
        private bool _postureBreakSnapshotObserved;

        private NetworkIdentity _networkIdentity;
        private PlayerController _playerController;

        private void Awake()
        {
            _networkIdentity = GetComponent<NetworkIdentity>();
            _playerController = GetComponent<PlayerController>();
            _combatActor = GetComponent<CombatActor>();
            _healthBarView = GetComponent<NpcHealthBarView>();
        }

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

            // 最小版：只记录动作状态
            // 后续可在这里驱动 Animator / VFX / SFX
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
                case ActionType.Dead:
                    CurrentRemoteAction = ActionType.Dead;
                    ClearPostureBreakTracking();
                    break;
                case ActionType.Revive:
                    CurrentRemoteAction = ActionType.Revive;
                    ClearPostureBreakTracking();
                    break;
                default:
                    CurrentRemoteAction = ActionType.None;
                    break;
            }

            if (_logApply)
                Debug.Log($"[RemoteActionApplier] applied {evt}");

        }

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

            // Every observer shows the victim's world health bar, even for a zero-damage block.
            _healthBarView?.ShowForHit();

            // Host/Server 端 CombatResolver 已直接应用伤害，跳过以防二次扣血
            if (NetworkServer.active)
                return;

            // Apply the server's absolute HP result; revision rejects stale delivery.
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

        public CharacterStateId ResolveSnapshotState(CharacterStateId snapshotState)
        {
            if (snapshotState == CharacterStateId.Dead)
            {
                ClearPostureBreakTracking();
                return CharacterStateId.Dead;
            }

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

            return snapshotState;
        }

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

        public void ResetState()
        {

            LastAppliedSeqId = 0;
            LastAppliedTick = 0;
            CurrentRemoteAction = ActionType.None;
            LastDodgeMode = 0;
            LastHitParam = 0;
            LastHitSeqId = 0;
            LastPostureBreakSeqId = 0;
            LastGuardReaction = GuardReactionType.None;
            LastGuardReactionSeqId = 0;
            _consumedGuardReactionSeqId = 0;
            _lastServerCombatSeqId = 0;
            _postureBreakAwaitingSnapshot = false;
            _postureBreakSnapshotObserved = false;
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
