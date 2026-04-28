using UnityEngine;

namespace Character.Sync
{
    public class RemoteActionApplier : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _logApply = true;

        public int LastAppliedSeqId {get; private set;} = 0;
        public int LastAppliedTick {get; private set;} = 0;
        public ActionType CurrentRemoteAction {get; private set;} = ActionType.None;



        public void Apply(ActionEvent evt)
        {
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
                    break;
                case ActionType.Hit:
                    CurrentRemoteAction = ActionType.Hit;
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

        public void ResetState(){

            LastAppliedSeqId = 0;
            LastAppliedTick = 0;
            CurrentRemoteAction = ActionType.None;
        }
    }
}
