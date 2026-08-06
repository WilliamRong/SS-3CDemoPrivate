using Opsive.BehaviorDesigner.Runtime.Tasks;
using AI;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.Shared.Utility;

namespace AI.BT
{
    [Category("NPC")]
    public sealed class BD_Parry : Action
    {
        private NpcCharacterDriver _driver;

        public override void OnAwake()
        {
            base.OnAwake();
            _driver = GetComponent<NpcCharacterDriver>();
        }

        public override TaskStatus OnUpdate()
        {
            return _driver != null && _driver.ServerTryEnterParry()
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }
}
