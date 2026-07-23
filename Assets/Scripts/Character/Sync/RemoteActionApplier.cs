using Mirror;
using UnityEngine;
using Character.Combat;
using Character.Controller;

namespace Character.Sync
{
    public class RemoteActionApplier : MonoBehaviour
    {
        private const int ServerCombatSeqBase = 100000;

        [Header("Debug")]
        [SerializeField] private bool _logApply = true;

        public int LastAppliedSeqId {get; private set;} = 0;
        public int LastAppliedTick {get; private set;} = 0;
        public ActionType CurrentRemoteAction {get; private set;} = ActionType.None;
        /// <summary>Last dodge mode from <see cref="ActionEvent.Param"/> on <see cref="ActionType.DodgeStart"/>.</summary>
        public byte LastDodgeMode { get; private set; }
        public int LastHitParam { get; private set; }
        public int LastHitSeqId { get; private set; }
        public GuardReactionType LastGuardReaction { get; private set; } = GuardReactionType.None;
        public int LastGuardReactionSeqId { get; private set; }

        private int _consumedGuardReactionSeqId;
        private int _lastServerCombatSeqId;

        private NetworkIdentity _networkIdentity;
        private PlayerController _playerController;

        private void Awake()
        {
            _networkIdentity = GetComponent<NetworkIdentity>();
            _playerController = GetComponent<PlayerController>();
        }

        public void Apply(ActionEvent evt)
        {
            if (_networkIdentity != null && _networkIdentity.netId != 0
                && evt.ActorId != (int)_networkIdentity.netId)
            {
                return;
            }

            if (evt.Type is ActionType.GuardHit or ActionType.GuardBreak)
            {
                ApplyGuardReaction(evt);
                return;
            }

            if (evt.SeqId >= ServerCombatSeqBase && evt.Type is ActionType.Hit or ActionType.Dead)
            {
                ApplyServerCombatReaction(evt);
                return;
            }

            if(evt.SeqId <= LastAppliedSeqId){
                if(_logApply) Debug.Log($"RemoteActionApplier: Ignore duplicate seqId {evt.SeqId}");
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
                case ActionType.Dead:
                    CurrentRemoteAction = ActionType.Dead;
                    break;
                case ActionType.Revive:
                    CurrentRemoteAction = ActionType.Revive;
                    break;
                default:
                    CurrentRemoteAction = ActionType.None;
                    break;
            }

            if (_logApply)
                Debug.Log($"[RemoteActionApplier] applied {evt}");

        }

        private void ApplyServerCombatReaction(ActionEvent evt)
        {
            if (evt.SeqId <= _lastServerCombatSeqId)
            {
                if (_logApply) Debug.Log($"RemoteActionApplier: Ignore duplicate server combat seqId {evt.SeqId}");
                return;
            }

            _lastServerCombatSeqId = evt.SeqId;
            LastAppliedTick = evt.Tick;
            CurrentRemoteAction = evt.Type;
            LastHitParam = evt.Param;
            if (evt.Type == ActionType.Hit)
                LastHitSeqId = evt.SeqId;

            if (_networkIdentity != null && _networkIdentity.isLocalPlayer && _playerController != null)
            {
                if (evt.Type == ActionType.Hit)
                    _playerController.ApplyHit(0f, evt.Param != 0);
                else if (evt.Type == ActionType.Dead)
                    _playerController.ApplyHit(float.MaxValue, true);
            }

            if (_logApply)
                Debug.Log($"[RemoteActionApplier] applied {evt}");
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

        public void ResetState(){

            LastAppliedSeqId = 0;
            LastAppliedTick = 0;
            CurrentRemoteAction = ActionType.None;
            LastDodgeMode = 0;
            LastHitParam = 0;
            LastHitSeqId = 0;
            LastGuardReaction = GuardReactionType.None;
            LastGuardReactionSeqId = 0;
            _consumedGuardReactionSeqId = 0;
            _lastServerCombatSeqId = 0;
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
