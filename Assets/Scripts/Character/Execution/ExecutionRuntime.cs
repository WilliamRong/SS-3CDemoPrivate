using Character.Combat;
using Character.Config;
using Character.StateMachine;
using Mirror;
using UnityEngine;


namespace Character.Execution
{
    public enum ExecutionStartFailure : byte
    {
        None = 0,
        SessionCreationRejected = 1,
        StateEntryUnavailable = 2,
        ExecutorSuppressionRejected = 3,
        TargetSuppressionRejected = 4,
        TargetStateRejected = 5,
        ExecutorStateRejected = 6,
        RollbackFailed = 7,
        LifecycleRegistrationRejected = 8,
        AuthorityUnavailable = 9,
    }

    /// <summary>
    /// 处决系统的权威组合根。
    /// 统一持有服务、选择时钟并推进活动会话。
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class ExecutionRuntime : MonoBehaviour
    {
        private enum AuthorityDomain : byte
        {
            None = 0,
            Offline = 1,
            Server = 2,
        }

        public static ExecutionRuntime Instance
        {
            get;
            private set;
        }

        private ExecutionSessionCoordinator _coordinator;
        private ExecutionLifecycleService _lifecycle;
        private ExecutionCandidateResolver _candidateResolver;

        private AuthorityDomain _authorityDomain;

        public ExecutionSessionCoordinator Coordinator =>
            _coordinator;

        public ExecutionLifecycleService Lifecycle =>
           _lifecycle;

        public ExecutionCandidateResolver CandidateResolver =>
            _candidateResolver;

        public IExecutionOccupancyQuery OccupancyQuery =>
            _coordinator;

        public bool HasAuthority => ResolveAuthorityDomain() != AuthorityDomain.None;

        public double AuthorityNowSec
        {
            get
            {
                AuthorityDomain domain = ResolveAuthorityDomain();

                return domain == AuthorityDomain.Server
                    ? NetworkTime.localTime
                    : Time.timeAsDouble;
            }
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnLoad()
        {
            if (FindFirstObjectByType<ExecutionRuntime>() != null) return;

            var runtimeObject = new GameObject("Execution Runtime");

            runtimeObject.AddComponent<ExecutionRuntime>();
        }


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _coordinator =
                new ExecutionSessionCoordinator();

            _lifecycle =
                new ExecutionLifecycleService(_coordinator);

            _candidateResolver =
                new ExecutionCandidateResolver();
        }

        private void LateUpdate()
        {
            AuthorityDomain domain = SynchronizeAuthorityDomain();

            if (domain == AuthorityDomain.None) return;

            _lifecycle.Tick(GetAuthorityTimeSec(domain));
        }

        private void OnDisable()
        {
            if (Instance != this) return;

            _lifecycle?.CancelAll();
            _authorityDomain = AuthorityDomain.None;
        }


        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// 处决必须通过这个入口启动，保证创建和 Tick
        /// 使用相同的权威时钟。
        /// </summary>
        public bool TryStart(CombatActor executor, CombatActor target, CharacterCombatConfig config,
        out ExecutionSession session,
        out ExecutionEligibilityResult eligibility,
        out ExecutionSessionCreateFailure createFailure,
        out ExecutionStartFailure startFailure)
        {
            session = default;
            eligibility = default;
            createFailure = ExecutionSessionCreateFailure.None;
            startFailure = ExecutionStartFailure.None;

            AuthorityDomain domain = SynchronizeAuthorityDomain();

            if (domain == AuthorityDomain.None)
            {
                startFailure = ExecutionStartFailure.AuthorityUnavailable;

                return false;
            }

            CharacterStateId executorPreviousState = executor != null
                ? executor.CurrentStateId
                : CharacterStateId.None;
            CharacterStateId targetPreviousState = target != null
                ? target.CurrentStateId
                : CharacterStateId.None;

            if (!_coordinator.TryCreateSession(executor, target, config,
                    GetAuthorityTimeSec(domain), out ExecutionSession created,
                    out eligibility, out createFailure))
            {
                startFailure = ExecutionStartFailure.SessionCreationRejected;
                return false;
            }

            if (!executor.CanEnterExecutionAsExecutor() ||
                !target.CanEnterExecutionAsTarget())
            {
                _coordinator.TryCancelSession(created.ExecutionId, out _);
                startFailure = ExecutionStartFailure.StateEntryUnavailable;
                return false;
            }

            bool executorSuppressed = executor.BeginExecutionCombatSuppression(created.ExecutionId);
            if (!executorSuppressed)
            {
                _coordinator.TryCancelSession(created.ExecutionId, out _);
                startFailure = ExecutionStartFailure.ExecutorSuppressionRejected;
                return false;
            }

            bool targetSuppressed = target.BeginExecutionCombatSuppression(created.ExecutionId);
            if (!targetSuppressed)
            {
                CleanupFailedStart(created, executor, target, executorSuppressed, false);
                startFailure = ExecutionStartFailure.TargetSuppressionRejected;
                return false;
            }

            if (!target.TryEnterExecuted(created))
            {
                CleanupFailedStart(created, executor, target, executorSuppressed, targetSuppressed);
                startFailure = ExecutionStartFailure.TargetStateRejected;
                return false;
            }

            if (!executor.TryEnterExecuting(created))
            {
                bool restored = target.TryRollbackExecutionStart(created.ExecutionId, targetPreviousState);
                CleanupFailedStart(created, executor, target, executorSuppressed, targetSuppressed);
                startFailure = restored ? ExecutionStartFailure.ExecutorStateRejected : ExecutionStartFailure.RollbackFailed;
                return false;
            }

            if (!_lifecycle.TryRegister(created, executor, target,
                    executorPreviousState, targetPreviousState))
            {
                bool executorRestored = executor.TryRollbackExecutionStart(created.ExecutionId, executorPreviousState);
                bool targetRestored = target.TryRollbackExecutionStart(created.ExecutionId, targetPreviousState);
                CleanupFailedStart(created, executor, target, executorSuppressed, targetSuppressed);
                startFailure = executorRestored && targetRestored
                    ? ExecutionStartFailure.LifecycleRegistrationRejected
                    : ExecutionStartFailure.RollbackFailed;
                return false;
            }

            session = created;
            return true;

        }

        private void CleanupFailedStart(in ExecutionSession session,
            CombatActor executor, CombatActor target,
            bool executorSuppressed, bool targetSuppressed)
        {
            if (targetSuppressed)
                target.EndExecutionCombatSuppression(session.ExecutionId);
            if (executorSuppressed)
                executor.EndExecutionCombatSuppression(session.ExecutionId);
            _coordinator.TryCancelSession(session.ExecutionId, out _);
        }

        public bool TryCancel(ulong executionId)
        {
            return executionId != 0 && _lifecycle.TryCancel(executionId);
        }

        private AuthorityDomain SynchronizeAuthorityDomain()
        {
            AuthorityDomain current = ResolveAuthorityDomain();

            if (current == _authorityDomain) return current;

            // Host/Server 与 Offline 的时钟域不同。
            // 切换运行模式时不能继续旧会话。
            if (_authorityDomain != AuthorityDomain.None) _lifecycle.CancelAll();

            _authorityDomain = current;
            return current;
        }

        private static AuthorityDomain ResolveAuthorityDomain()
        {
            if (NetworkServer.active) return AuthorityDomain.Server;

            if (!NetworkClient.active) return AuthorityDomain.Offline;

            return AuthorityDomain.None;
        }

        private static double GetAuthorityTimeSec(AuthorityDomain domain)
        {
            return domain == AuthorityDomain.Server ? NetworkTime.localTime : Time.timeAsDouble;
        }
    }
}
