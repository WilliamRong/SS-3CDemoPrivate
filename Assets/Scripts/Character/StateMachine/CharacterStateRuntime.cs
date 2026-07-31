using Character.Config;

namespace Character.StateMachine
{
    /// <summary>
    /// 独立跟踪状态经过时间和打断窗口，使状态实现与统一转换规则读取同一时间源。
    /// </summary>
    public class CharacterStateRuntime
    {
        private CharacterCombatConfig _combat;

        public CharacterStateId CurrentStateId { get; private set; } = CharacterStateId.None;
        public float StateElapsedTime { get; private set; }

        public float PreHitEnd { get; private set; }
        public float ActiveEnd { get; private set; }
        public float RecoveryEnd { get; private set; }

        // ============ 配置与状态进入 ============

        public void SetCombatConfig(CharacterCombatConfig combat)
        {
            _combat = combat;
        }

        public void OnStateEntered(CharacterStateId stateId)
        {
            CurrentStateId = stateId;
            StateElapsedTime = 0f;
            SetWindowsForState(stateId);
        }

        // ============ 时间与窗口查询 ============

        public void Tick(float dt)
        {
            StateElapsedTime += dt;
        }

        public StateWindowType GetCurrentWindowType()
        {
            if (StateElapsedTime < PreHitEnd) return StateWindowType.PreHitWindow;
            if (StateElapsedTime < ActiveEnd) return StateWindowType.ActiveWindow;
            if (StateElapsedTime < RecoveryEnd) return StateWindowType.RecoveryWindow;
            return StateWindowType.Always;
        }

        // ============ 窗口缓存 ============

        private void SetWindowsForState(CharacterStateId stateId)
        {
            if (_combat != null)
            {
                _combat.GetStateWindows(stateId, out var preHit, out var active, out var recovery);
                PreHitEnd = preHit;
                ActiveEnd = active;
                RecoveryEnd = recovery;
                return;
            }

            PreHitEnd = 0f;
            ActiveEnd = 0f;
            RecoveryEnd = 0f;
        }
    }
}
