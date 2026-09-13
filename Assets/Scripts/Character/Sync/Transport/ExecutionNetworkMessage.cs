using Mirror;
using Character.Execution;
using Character.StateMachine;
using Character.Presentation;
using UnityEngine;


namespace Character.Sync
{
    /// <summary>
    /// Client只提交目标和请求序号
    /// 处决者身份必须由Server根据连接所有权确定
    /// </summary>
    public struct ExecutionRequestMsg : NetworkMessage
    {
        public uint RequestSeq;
        public int TargetActorId;
    }

    /// <summary>
    /// Server接受请求后广播的完整处决会话
    /// 字段保持扁平，便于MirrorWeaver生成稳定协议
    /// </summary>
    public struct ExecutionStartMsg : NetworkMessage
    {
        public uint RequestSeq;

        public ulong ExecutionId;
        public int ExecutorActorId;
        public int TargetActorId;

        public float FixedTargetPx;
        public float FixedTargetPy;
        public float FixedTargetPz;
        public float FixedTargetYaw;

        public float ExecutorAnchorPx;
        public float ExecutorAnchorPy;
        public float ExecutorAnchorPz;
        public float ExecutorAnchorYaw;

        public double StartTimeSec;
        public double ResultTimeSec;

        //用来在接收端重建ExecutionSession
        public float ExecutionDamage;
        public byte TargetWillDie;
        public byte DeathVariant;

        //对应ExecutionSessionFlags,允许晚加入时发送当前状态
        public byte SessionFlags;
    }


    /// <summary>
    /// Server在executionResultTime提交的绝对生命结果
    /// 重复消息由executionId和HealthRevision去重
    /// </summary>
    public struct ExecutionResultMsg : NetworkMessage
    {
        public ulong ExecutionId;
        public int ExecutorActorId;
        public int TargetActorId;

        public double AuthorityTimeSec;

        public float CurrentHp;
        public float MaxHp;
        public uint HealthRevision;

        public byte DeathVariant;
    }

    /// <summary>
    /// 广播双方独立完成，取消等累计会话边沿
    /// SessionFlags使用累计值，处理重复和乱序
    /// </summary>
    public struct ExecutionCompleteMsg : NetworkMessage
    {
        public ulong ExecutionId;
        public int ExecutorActorId;
        public int TargetActorId;

        public double AuthorityTimeSec;

        public byte SessionFlags;

        public byte DeathVariant;
    }

    /// <summary>
    /// Client 请求指定 Actor 当前关联的权威处决状态。
    /// </summary>
    public struct ExecutionStateRequestMsg : NetworkMessage
    {
        public uint RequestSeq;
        public int ActorId;
    }

    /// <summary>
    /// Server 返回活跃会话或短期缓存的终态。
    /// 使用扁平字段，保持 Mirror 协议布局明确。
    /// </summary>
    public struct ExecutionStateMsg : NetworkMessage
    {
        public uint RequestSeq;
        public int RequestedActorId;
        public byte HasState;

        public ulong ExecutionId;
        public int ExecutorActorId;
        public int TargetActorId;

        public float FixedTargetPx;
        public float FixedTargetPy;
        public float FixedTargetPz;
        public float FixedTargetYaw;

        public float ExecutorAnchorPx;
        public float ExecutorAnchorPy;
        public float ExecutorAnchorPz;
        public float ExecutorAnchorYaw;

        // 返回消息时处决者的绝对权威姿态。
        public float ExecutorPx;
        public float ExecutorPy;
        public float ExecutorPz;
        public float ExecutorYaw;

        public double StartTimeSec;
        public double ResultTimeSec;
        public double AuthorityTimeSec;

        public float ExecutionDamage;
        public byte TargetWillDie;
        public byte SessionFlags;

        public byte HasHealthResult;
        public float CurrentHp;
        public float MaxHp;
        public uint HealthRevision;

        public byte DeathVariant;
    }

    /// <summary>
    /// 负责领域对象与 Mirror 扁平消息之间的字段转换。
    /// 传输类只处理连接、校验和发送，不再同时维护序列化映射。
    /// </summary>
    internal static class SyncMessageConverter
    {
        public static SnapshotMsg ToMessage(StateSnapshot snapshot)
        {
            return new SnapshotMsg
            {
                Tick = snapshot.Tick,
                ActorId = snapshot.ActorId,
                Px = snapshot.Position.x,
                Py = snapshot.Position.y,
                Pz = snapshot.Position.z,
                Yaw = snapshot.Yaw,
                Vx = snapshot.VelocityXZ.x,
                Vz = snapshot.VelocityXZ.y,
                StateId = (int)snapshot.StateId,
                SprintPhase = snapshot.SprintPhase,
                DodgeMode = snapshot.DodgeMode,
                GuardPhase = snapshot.GuardPhase,
                IdlePhase = snapshot.IdlePhase,
                AttackComboStep = snapshot.AttackComboStep,
                LockOnActive = snapshot.LockOnActive,
                LockTargetNetId = snapshot.LockTargetNetId,
                MoveInputX = snapshot.MoveInputX,
                MoveInputY = snapshot.MoveInputY,
                HasAuthoritativeHealth = snapshot.HasAuthoritativeHealth,
                CurrentHp = snapshot.CurrentHp,
                MaxHp = snapshot.MaxHp,
                HealthRevision = snapshot.HealthRevision,
                HasAuthoritativePosture = snapshot.HasAuthoritativePosture,
                CurrentPosture = snapshot.CurrentPosture,
                MaxPosture = snapshot.MaxPosture,
                ParryPhase = snapshot.ParryPhase,
            };
        }

        public static StateSnapshot ToSnapshot(SnapshotMsg message)
        {
            return new StateSnapshot(
                message.Tick,
                message.ActorId,
                new Vector3(message.Px, message.Py, message.Pz),
                message.Yaw,
                new Vector2(message.Vx, message.Vz),
                (CharacterStateId)message.StateId,
                message.SprintPhase,
                message.DodgeMode,
                message.GuardPhase,
                message.IdlePhase,
                message.AttackComboStep,
                message.LockOnActive,
                message.LockTargetNetId,
                message.MoveInputX,
                message.MoveInputY,
                message.HasAuthoritativeHealth,
                message.CurrentHp,
                message.MaxHp,
                message.HealthRevision,
                message.HasAuthoritativePosture,
                message.CurrentPosture,
                message.MaxPosture,
                message.ParryPhase);
        }

        public static ActionMsg ToMessage(ActionEvent actionEvent)
        {
            return new ActionMsg
            {
                SeqId = actionEvent.SeqId,
                Tick = actionEvent.Tick,
                ActorId = actionEvent.ActorId,
                Type = (int)actionEvent.Type,
                Param = actionEvent.Param,
                HasHealthResult = actionEvent.HasHealthResult,
                AppliedDamage = actionEvent.AppliedDamage,
                CurrentHp = actionEvent.CurrentHp,
                MaxHp = actionEvent.MaxHp,
                HealthRevision = actionEvent.HealthRevision,
            };
        }

        public static ActionEvent ToActionEvent(ActionMsg message)
        {
            return new ActionEvent(
                message.SeqId,
                message.Tick,
                message.ActorId,
                (ActionType)message.Type,
                message.Param,
                message.HasHealthResult,
                message.AppliedDamage,
                message.CurrentHp,
                message.MaxHp,
                message.HealthRevision);
        }
    }

    internal static class ExecutionMessageValidator
    {
        public static ExecutionSession MergeFlags(in ExecutionSession session, ExecutionSessionFlags flags)
        {
            ExecutionSession merged = session;
            if ((flags & ExecutionSessionFlags.ResultCommitted) != 0) merged = merged.MarkResultCommitted();
            if ((flags & ExecutionSessionFlags.ExecutorCompleted) != 0) merged = merged.MarkExecutorCompleted();
            if ((flags & ExecutionSessionFlags.TargetCompleted) != 0) merged = merged.MarkTargetCompleted();
            if ((flags & ExecutionSessionFlags.Cancelled) != 0) merged = merged.MarkCancelled();
            return merged;
        }

        public static bool TryValidateComplete(in ExecutionCompleteMsg message, out ExecutionSessionFlags flags, out DeathPresentationVariant deathVariant)
        {
            flags = (ExecutionSessionFlags)message.SessionFlags;
            deathVariant = (DeathPresentationVariant)message.DeathVariant;
            const ExecutionSessionFlags knownFlags = ExecutionSessionFlags.ResultCommitted | ExecutionSessionFlags.ExecutorCompleted | ExecutionSessionFlags.TargetCompleted | ExecutionSessionFlags.Cancelled;
            const ExecutionSessionFlags completionFlags = ExecutionSessionFlags.ExecutorCompleted | ExecutionSessionFlags.TargetCompleted | ExecutionSessionFlags.Cancelled;
            return message.ExecutionId != 0 && message.ExecutorActorId > 0 && message.TargetActorId > 0 && message.ExecutorActorId != message.TargetActorId &&
                   !double.IsNaN(message.AuthorityTimeSec) && !double.IsInfinity(message.AuthorityTimeSec) && message.AuthorityTimeSec >= 0d &&
                   (flags & ~knownFlags) == ExecutionSessionFlags.None && (flags & completionFlags) != ExecutionSessionFlags.None &&
                   ((flags & ExecutionSessionFlags.TargetCompleted) == 0 || (flags & ExecutionSessionFlags.ResultCommitted) != 0) &&
                   deathVariant is DeathPresentationVariant.Default or DeathPresentationVariant.Executed;
        }

        public static bool TryBuildSession(in ExecutionStartMsg message, out ExecutionSession session, out DeathPresentationVariant deathVariant)
        {
            session = default;
            deathVariant = (DeathPresentationVariant)message.DeathVariant;
            if (message.TargetWillDie > 1 || deathVariant is not (DeathPresentationVariant.Default or DeathPresentationVariant.Executed)) return false;
            bool targetWillDie = message.TargetWillDie != 0;
            if (targetWillDie != (deathVariant == DeathPresentationVariant.Executed)) return false;
            session = new ExecutionSession(
                message.ExecutionId, message.ExecutorActorId, message.TargetActorId,
                new ExecutionPose(new Vector3(message.FixedTargetPx, message.FixedTargetPy, message.FixedTargetPz), message.FixedTargetYaw),
                new ExecutionPose(new Vector3(message.ExecutorAnchorPx, message.ExecutorAnchorPy, message.ExecutorAnchorPz), message.ExecutorAnchorYaw),
                message.StartTimeSec, message.ResultTimeSec, message.ExecutionDamage, targetWillDie,
                (ExecutionSessionFlags)message.SessionFlags);
            if (!session.TryValidate(out _) || !session.IsActive)
            {
                session = default;
                deathVariant = DeathPresentationVariant.Default;
                return false;
            }
            return true;
        }
    }
}
