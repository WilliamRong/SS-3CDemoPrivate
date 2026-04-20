namespace Character.StateMachine
{
    public class CharacterStateRuntime
    {
       public CharacterStateId CurrentStateId { get; private set; } = CharacterStateId.None;
       public float StateElapsedTime { get; private set; } = 0f;

       //可按状态刷新的窗口
        public float PreHitEnd = 0.1f;     // 前摇结束
        public float ActiveEnd = 0.25f;    // 生效结束
        public float RecoveryEnd = 0.45f;  // 后摇结束

        public void OnStateEntered(CharacterStateId stateId){
            CurrentStateId = stateId;
            StateElapsedTime = 0f;
            SetWindowsForState(stateId);
        }

        public void Tick(float dt){
            StateElapsedTime += dt;
        }

        public StateWindowType GetCurrentWindowType(){
            if(StateElapsedTime < PreHitEnd) return StateWindowType.PreHitWindow;
            if(StateElapsedTime < ActiveEnd) return StateWindowType.ActiveWindow;
            if(StateElapsedTime < RecoveryEnd) return StateWindowType.RecoveryWindow;
            return StateWindowType.Always;
        }

        private void SetWindowsForState(CharacterStateId stateId)
        {
            switch (stateId)
            {
                case CharacterStateId.Attack:
                    PreHitEnd = 0.1f;
                    ActiveEnd = 0.25f;
                    RecoveryEnd = 0.45f;
                    break;
                case CharacterStateId.Dodge:
                    PreHitEnd = 0.05f;
                    ActiveEnd = 0.18f;
                    RecoveryEnd = 0.25f;
                    break;
                case CharacterStateId.Hit:
                    PreHitEnd = 0.1f;
                    ActiveEnd = 0.2f;
                    RecoveryEnd = 0.5f;
                    break;
                default:
                    PreHitEnd = 0f;
                    ActiveEnd = 0f;
                    RecoveryEnd = 0f;
                    break;
            }
        }
    }
}
