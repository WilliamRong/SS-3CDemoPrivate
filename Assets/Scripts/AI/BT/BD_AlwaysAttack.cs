using Character.Combat;
using Character.StateMachine;
using Mirror;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.Shared.Utility;
using UnityEngine;

namespace AI.BT
{
    [Category("NPC")]
    public sealed class BD_AlwaysAttack : Action
    {
        private AttackMoveId _attackId = AttackMoveId.Combo1;
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

            if (_driver.CurrentStateId != CharacterStateId.Attack)
                _driver.ServerTryEnterAttack(_attackId);

            return TaskStatus.Running;
        }
    }
}
