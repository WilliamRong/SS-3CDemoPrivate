using Mirror;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.Shared.Utility;

namespace AI.BT
{
    [Category("NPC")]
    public sealed class BD_AlwaysCombo : Action
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

            _driver.ServerRequestComboAttack();
            return TaskStatus.Running;
        }
    }
}