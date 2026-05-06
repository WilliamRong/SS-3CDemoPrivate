using System;
using Character.Intent;
using Character.StateMachine;
using Mirror;
using UnityEngine;

namespace AI
{
    [RequireComponent(typeof(NetworkIdentity))]
    public class NpcCharacterDriver : NetworkBehaviour
    {
       [SerializeField] private NpcAiIntentSource _intentSource;
       [SerializeField] private NPCMotor _motor;

       private CharacterStateMachine _fsm;
       private CharacterStateRegistry _registry;
       private NpcIdleState _idle;
       private NpcMoveState _move;

       public CharacterStateId CurrentStateId => _fsm?.CurrentState?.Id ?? CharacterStateId.None;


       private void Awake()
       {
           if (_intentSource == null) _intentSource = GetComponent<NpcAiIntentSource>();
           if (_motor == null) _motor = GetComponent<NPCMotor>();
       }

       public override void OnStartServer()
       {
           base.OnStartServer();
           _fsm = new CharacterStateMachine();
           _registry = new CharacterStateRegistry();
           _idle = new NpcIdleState(_fsm, _registry, _intentSource, _motor);
           _move = new NpcMoveState(_fsm, _registry, _intentSource, _motor);
           
           _registry.Register(_idle);
           _registry.Register(_move);
           
           _fsm.Initialize(_idle);
       }

       /// <summary>
       /// 需要保证在行为树之后执行，放在LateUpdate中
       /// </summary>
       private void LateUpdate()
       {
           if (!isServer || _fsm == null) return;

           CharacterIntent intent = _intentSource != null ? _intentSource.BuildIntent() : default;
           
           _fsm.Tick(intent, Time.deltaTime);
       }
    }
}
