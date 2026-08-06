using Character.StateMachine;
using Mirror;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.Shared.Utility;
using UnityEngine;

namespace AI
{
    [Category("NPC")]
    public sealed class BD_AlwaysParry : Action
    {
        private NpcCharacterDriver _driver;

        public override void OnAwake()
        {
            base.OnAwake();
            _driver = GetComponent<NpcCharacterDriver>();
        }

        public override TaskStatus OnUpdate()
        {
            if (_driver == null || !NetworkServer.active || !_driver.isServer)
                return TaskStatus.Failure;

            if (_driver.CurrentStateId != CharacterStateId.Parry)
                _driver.ServerTryEnterParry();

            return TaskStatus.Running;
        }
    }
}