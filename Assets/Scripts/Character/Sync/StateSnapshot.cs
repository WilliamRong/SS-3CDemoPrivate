using Character.Combat;
using Character.Config;
using Character.Presentation;
using Character.StateMachine;
using Character.StateMachine.States;
using UnityEngine;

namespace Character.Sync
{
    /// <summary>
    /// 将连续运动、状态表现上下文和权威数值封装为单帧快照，远端可在不运行玩法状态机的前提下还原角色。
    /// </summary>
    public struct StateSnapshot
    {
        public int Tick;
        public float ArrivalTimeSec;
        public int ActorId;

        public Vector3 Position;
        public float Yaw;
        public Vector2 VelocityXZ;

        public CharacterStateId StateId;
        /// <summary>仅 Sprint 状态有效，其他状态保持 0，避免旧字段残留。</summary>
        public byte SprintPhase;
        /// <summary>仅 Dodge 状态有效，枚举定义见 <see cref="Presentation.DodgeMode"/>。</summary>
        public byte DodgeMode;
        /// <summary>仅 Guard 状态有效，枚举定义见 <see cref="GuardState.GuardPhase"/>。</summary>
        public byte GuardPhase;
        /// <summary>仅 Idle 状态有效，枚举定义见 <see cref="IdleState.IdlePhase"/>。</summary>
        public byte IdlePhase;
        /// <summary>仅 Attack 状态有效，以 byte 编码 <see cref="AttackMoveId"/>。</summary>
        public byte AttackComboStep;

        /// <summary>使用 byte 而非 bool，保持 Mirror 序列化布局明确。</summary>
        public byte LockOnActive;
        /// <summary>只传锁定对象 netId，0 表示未锁定，LockPoint 由接收端本地解析。</summary>
        public uint LockTargetNetId;
        /// <summary>锁定移动时直接同步 Blend 输入，避免远端从插值速度反推八向意图。</summary>
        public float MoveInputX;
        public float MoveInputY;

        public byte HasAuthoritativeHealth;
        public float CurrentHp;
        public float MaxHp;
        public uint HealthRevision;

        public byte HasAuthoritativePosture;
        public float CurrentPosture;
        public float MaxPosture;

        public StateSnapshot(
            int tick,
            int actorId,
            Vector3 position,
            float yaw,
            Vector2 velocityXZ,
            CharacterStateId stateId,
            byte sprintPhase = 0,
            byte dodgeMode = 0,
            byte guardPhase = 0,
            byte idlePhase = 0,
            byte attackComboStep = 0,
            byte lockOnActive = 0,
            uint lockTargetNetId = 0,
            float moveInputX = 0f,
            float moveInputY = 0f,
            byte hasAuthoritativeHealth = 0,
            float currentHp = 0f,
            float maxHp = 0f,
            uint healthRevision = 0,
            byte hasAuthoritativePosture = 0,
            float currentPosture = 0f,
            float maxPosture = 0f)
        {
            Tick = tick;
            ActorId = actorId;
            Position = position;
            Yaw = yaw;
            VelocityXZ = velocityXZ;
            StateId = stateId;
            SprintPhase = sprintPhase;
            DodgeMode = dodgeMode;
            GuardPhase = guardPhase;
            IdlePhase = idlePhase;
            AttackComboStep = attackComboStep;
            LockOnActive = lockOnActive;
            LockTargetNetId = lockTargetNetId;
            MoveInputX = moveInputX;
            MoveInputY = moveInputY;
            ArrivalTimeSec = 0f;
            HasAuthoritativeHealth = hasAuthoritativeHealth;
            CurrentHp = currentHp;
            MaxHp = maxHp;
            HealthRevision = healthRevision;
            HasAuthoritativePosture = hasAuthoritativePosture;
            CurrentPosture = currentPosture;
            MaxPosture = maxPosture;
        }

        public bool IsLockOnActive => LockOnActive != 0;

        public Vector2 GetMoveInputOrDefault() => new Vector2(MoveInputX, MoveInputY);

        // ============ 状态字段清洗 ============

        public SprintState.SprintPhase GetSprintPhaseOrDefault()
        {
            if (StateId != CharacterStateId.Sprint)
                return SprintState.SprintPhase.Loop;

            return System.Enum.IsDefined(typeof(SprintState.SprintPhase), (int)SprintPhase)
                ? (SprintState.SprintPhase)SprintPhase
                : SprintState.SprintPhase.Loop;
        }

        public Presentation.DodgeMode GetDodgeModeOrDefault()
        {
            if (StateId != CharacterStateId.Dodge)
                return Presentation.DodgeMode.None;

            if (DodgeMode < (byte)Presentation.DodgeMode.NeutralBackward
                || DodgeMode > (byte)Presentation.DodgeMode.LockOn8Way)
                return Presentation.DodgeMode.None;

            return (Presentation.DodgeMode)DodgeMode;
        }

        public GuardState.GuardPhase GetGuardPhaseOrDefault()
        {
            if (StateId != CharacterStateId.Guard)
                return GuardState.GuardPhase.Start;

            if (GuardPhase > (byte)GuardState.GuardPhase.TurnRight)
                return GuardState.GuardPhase.Start;

            return (GuardState.GuardPhase)GuardPhase;
        }

        public IdleState.IdlePhase GetIdlePhaseOrDefault()
        {
            if (StateId != CharacterStateId.Idle)
                return IdleState.IdlePhase.Normal;

            if (IdlePhase > (byte)IdleState.IdlePhase.TurnRight)
                return IdleState.IdlePhase.Normal;

            return (IdleState.IdlePhase)IdlePhase;
        }

        public byte GetAttackComboStepOrDefault()
        {
            if (StateId != CharacterStateId.Attack)
                return 1;

            return AttackMoveIdExtensions.FromByte(AttackComboStep).ToByte();
        }

        // ============ 值对象变换 ============

        public StateSnapshot WithDodgeMode(byte dodgeMode)
        {
            var copy = this;
            copy.DodgeMode = dodgeMode;
            return copy;
        }

        /// <summary>
        /// 只用快照内的冻结方向重建闪避，接收端当前输入和相机变化不会改写已经发生的动作。
        /// </summary>
        public bool TryBuildDodgePresentationContext(
            CharacterCombatConfig combat,
            out DodgePresentationContext ctx)
        {
            ctx = default;
            var mode = GetDodgeModeOrDefault();
            if (mode == Presentation.DodgeMode.None)
                return false;

            var yawRot = Quaternion.Euler(0f, Yaw, 0f);
            Vector2 blendLocal = Vector2.zero;
            Vector3 worldDir;

            switch (mode)
            {
                case Presentation.DodgeMode.NeutralBackward:
                    worldDir = yawRot * Vector3.back;
                    break;
                case Presentation.DodgeMode.LockOn8Way:
                    blendLocal = VelocityXZ;
                    var local = new Vector3(blendLocal.x, 0f, blendLocal.y);
                    worldDir = local.sqrMagnitude > 0.0001f
                        ? yawRot * local.normalized
                        : yawRot * Vector3.forward;
                    break;
                default:
                    worldDir = yawRot * Vector3.forward;
                    break;
            }

            worldDir.y = 0f;
            if (worldDir.sqrMagnitude > 0.0001f)
                worldDir.Normalize();

            float duration = combat.GetDodgeDuration(mode);
            float moveDuration = combat.GetDodgeMoveDuration(mode);
            ctx = new DodgePresentationContext(mode, blendLocal, duration, moveDuration, worldDir);
            return ctx.IsValid;
        }

        // ============ 调试输出 ============

        public override string ToString()
        {
            return $"[Snapshot] tick={Tick}, actor={ActorId}, state={StateId}, " +
                   $"sprintPhase={SprintPhase}, dodgeMode={DodgeMode}, guardPhase={GuardPhase}, idlePhase={IdlePhase}, attackCombo={AttackComboStep}, " +
                   $"lockOn={LockOnActive}, lockTarget={LockTargetNetId}, moveInput=({MoveInputX:F2},{MoveInputY:F2}), " +
                   $"pos=({Position.x:F2},{Position.y:F2},{Position.z:F2}), yaw={Yaw:F1}, velXZ=({VelocityXZ.x:F2},{VelocityXZ.y:F2}), " +
                   $"posture=({CurrentPosture:F1}/{MaxPosture:F1}, authoritative={HasAuthoritativePosture})";
        }
    }
}
