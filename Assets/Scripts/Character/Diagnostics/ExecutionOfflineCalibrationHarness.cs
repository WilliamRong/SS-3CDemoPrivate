using Character.Combat;
using Character.Config;
using Character.Execution;
using Core;
using Mirror;
using UnityEngine;

namespace Character.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class ExecutionOfflineCalibrationHarness : MonoBehaviour
    {
        [Header("Optional References")]
        [SerializeField] private CombatActor _executor;
        [SerializeField] private CombatActor _target;
        [SerializeField] private CharacterCombatConfig _config;

        [Header("Runtime")]
        [SerializeField] private bool _autoResolveActors = true;
        [SerializeField] private bool _logResidualEveryFrame;
        [SerializeField, Min(0.01f)] private float _residualLogInterval = 0.1f;

        private ExecutionRuntime _runtime;
        private bool _eventsBound;

        private ulong _trackedExecutionId;
        private CombatActor _trackedExecutor;
        private CombatActor _trackedTarget;

        private float _nextResidualLogTime;

        private float _maxExecutorPositionError;
        private float _maxExecutorYawError;
        private float _maxTargetPositionError;
        private float _maxTargetYawError;

        private void Update()
        {
            RefreshRuntimeBinding();

            if (UnityEngine.Input.GetKeyDown(KeyCode.F7))
                EvaluateCurrent();

            if (UnityEngine.Input.GetKeyDown(KeyCode.F8))
                StartExecution();

            if (UnityEngine.Input.GetKeyDown(KeyCode.F9))
                CancelExecution();

            TickResidualMeasurement();
        }

        private void OnDisable()
        {
            UnbindLifecycleEvents();
        }

        private void RefreshRuntimeBinding()
        {
            ExecutionRuntime current = ExecutionRuntime.Instance;

            if (current != _runtime)
            {
                UnbindLifecycleEvents();
                _runtime = current;

                if (_trackedExecutionId != 0)
                    ResetTracking();
            }

            if (_runtime == null ||
                _eventsBound ||
                _runtime.Lifecycle == null)
            {
                return;
            }

            _runtime.Lifecycle.ResultCommitted += HandleResultCommitted;
            _runtime.Lifecycle.CompletionChanged += HandleCompletionChanged;
            _eventsBound = true;
        }

        private void UnbindLifecycleEvents()
        {
            if (!_eventsBound || _runtime == null || _runtime.Lifecycle == null)
            {
                _eventsBound = false;
                return;
            }

            _runtime.Lifecycle.ResultCommitted -= HandleResultCommitted;
            _runtime.Lifecycle.CompletionChanged -= HandleCompletionChanged;
            _eventsBound = false;
        }

        private void EvaluateCurrent()
        {
            if (!TryResolveContext(
                    out CombatActor executor,
                    out CombatActor target,
                    out CharacterCombatConfig config))
            {
                return;
            }

            IExecutionOccupancyQuery occupancy =
                _runtime != null
                    ? _runtime.OccupancyQuery
                    : null;

            ExecutionEligibilityResult result =
                ExecutionEligibilityService.EvaluateCurrent(
                    executor,
                    target,
                    config,
                    occupancy);

            Debug.Log(
                "[ExecutionCalibration] " +
                $"eligible={result.IsEligible}; " +
                $"reason={result.RejectionReason}; " +
                $"executor={executor.name}({executor.ActorId}); " +
                $"target={target.name}({target.ActorId}); " +
                $"distance={result.HorizontalDistance:F3}m; " +
                $"height={result.HeightDifference:F3}m; " +
                $"frontAngle={result.TargetFrontAngle:F2}deg; " +
                $"warpPosition={result.WarpTranslationError:F3}m; " +
                $"warpYaw={result.WarpYawError:F2}deg",
                this);
        }

        private void StartExecution()
        {
            if (_trackedExecutionId != 0)
            {
                Debug.LogWarning(
                    "[ExecutionCalibration] An execution is already active.",
                    this);
                return;
            }

            if (_runtime == null)
            {
                Debug.LogWarning(
                    "[ExecutionCalibration] ExecutionRuntime is not ready.",
                    this);
                return;
            }

            if (!TryResolveContext(
                    out CombatActor executor,
                    out CombatActor target,
                    out CharacterCombatConfig config))
            {
                return;
            }

            bool started = _runtime.TryStart(
                executor,
                target,
                config,
                out ExecutionSession session,
                out ExecutionEligibilityResult eligibility,
                out ExecutionSessionCreateFailure createFailure,
                out ExecutionStartFailure startFailure);

            if (!started)
            {
                Debug.LogWarning(
                    "[ExecutionCalibration] Start rejected. " +
                    $"reason={eligibility.RejectionReason}; " +
                    $"createFailure={createFailure}; " +
                    $"startFailure={startFailure}",
                    this);
                return;
            }

            _trackedExecutionId = session.ExecutionId;
            _trackedExecutor = executor;
            _trackedTarget = target;

            _maxExecutorPositionError = 0f;
            _maxExecutorYawError = 0f;
            _maxTargetPositionError = 0f;
            _maxTargetYawError = 0f;
            _nextResidualLogTime = 0f;

            Debug.Log(
                "[ExecutionCalibration] Started. " +
                $"executionId={session.ExecutionId}; " +
                $"executor={executor.name}; " +
                $"target={target.name}; " +
                $"resultTime={session.ResultTimeSec:F3}",
                this);
        }

        private void CancelExecution()
        {
            if (_trackedExecutionId == 0)
            {
                Debug.Log(
                    "[ExecutionCalibration] No active execution.",
                    this);
                return;
            }

            ulong executionId = _trackedExecutionId;

            if (_runtime == null ||
                !_runtime.TryCancel(executionId))
            {
                Debug.LogWarning(
                    "[ExecutionCalibration] Cancel failed. " +
                    $"executionId={executionId}",
                    this);
                return;
            }

            Debug.Log(
                "[ExecutionCalibration] Cancel requested. " +
                $"executionId={executionId}",
                this);
        }

        private void TickResidualMeasurement()
        {
            if (_trackedExecutionId == 0 ||
                _runtime == null ||
                _runtime.Coordinator == null ||
                _trackedExecutor == null ||
                _trackedTarget == null)
            {
                return;
            }

            if (!_runtime.Coordinator.TryGetSession(
                    _trackedExecutionId,
                    out ExecutionSession session))
            {
                return;
            }

            MeasureResidual(session);

            if (!_logResidualEveryFrame ||
                Time.unscaledTime < _nextResidualLogTime)
            {
                return;
            }

            _nextResidualLogTime =
                Time.unscaledTime +
                Mathf.Max(0.01f, _residualLogInterval);

            Debug.Log(
                "[ExecutionCalibration] Residual. " +
                $"executorPos={_maxExecutorPositionError:F3}m; " +
                $"executorYaw={_maxExecutorYawError:F2}deg; " +
                $"targetPos={_maxTargetPositionError:F3}m; " +
                $"targetYaw={_maxTargetYawError:F2}deg",
                this);
        }

        private void MeasureResidual(in ExecutionSession session)
        {
            if (_trackedExecutor == null ||
                _trackedTarget == null)
            {
                return;
            }

            float executorPositionError =
                Vector3.Distance(
                    _trackedExecutor.transform.position,
                    session.ExecutorAnchorPose.Position);

            float executorYawError =
                Mathf.Abs(
                    Mathf.DeltaAngle(
                        _trackedExecutor.transform.eulerAngles.y,
                        session.ExecutorAnchorPose.Yaw));

            float targetPositionError =
                Vector3.Distance(
                    _trackedTarget.transform.position,
                    session.FixedTargetPose.Position);

            float targetYawError =
                Mathf.Abs(
                    Mathf.DeltaAngle(
                        _trackedTarget.transform.eulerAngles.y,
                        session.FixedTargetPose.Yaw));

            _maxExecutorPositionError =
                Mathf.Max(
                    _maxExecutorPositionError,
                    executorPositionError);

            _maxExecutorYawError =
                Mathf.Max(
                    _maxExecutorYawError,
                    executorYawError);

            _maxTargetPositionError =
                Mathf.Max(
                    _maxTargetPositionError,
                    targetPositionError);

            _maxTargetYawError =
                Mathf.Max(
                    _maxTargetYawError,
                    targetYawError);
        }

        private void HandleResultCommitted(
            ExecutionSession session,
            CombatActor target,
            double authorityNowSec)
        {
            if (session.ExecutionId != _trackedExecutionId)
                return;

            Debug.Log(
                "[ExecutionCalibration] Result committed. " +
                $"executionId={session.ExecutionId}; " +
                $"targetWillDie={session.TargetWillDie}; " +
                $"authorityTime={authorityNowSec:F3}",
                this);
        }

        private void HandleCompletionChanged(
            ExecutionSession session)
        {
            if (session.ExecutionId != _trackedExecutionId)
                return;

            MeasureResidual(session);

            Debug.Log(
                "[ExecutionCalibration] Completed. " +
                $"executionId={session.ExecutionId}; " +
                $"flags={session.Flags}; " +
                $"maxExecutorPos={_maxExecutorPositionError:F3}m; " +
                $"maxExecutorYaw={_maxExecutorYawError:F2}deg; " +
                $"maxTargetPos={_maxTargetPositionError:F3}m; " +
                $"maxTargetYaw={_maxTargetYawError:F2}deg",
                this);

            ResetTracking();
        }

        private bool TryResolveContext(
            out CombatActor executor,
            out CombatActor target,
            out CharacterCombatConfig config)
        {
            executor = ResolveExecutor();
            target = ResolveTarget(executor);
            config = ResolveConfig();

            if (executor == null)
            {
                Debug.LogWarning(
                    "[ExecutionCalibration] Player CombatActor not found.",
                    this);
                return false;
            }

            if (target == null)
            {
                Debug.LogWarning(
                    "[ExecutionCalibration] Target CombatActor not found.",
                    this);
                return false;
            }

            if (config == null)
            {
                Debug.LogWarning(
                    "[ExecutionCalibration] Combat config not found.",
                    this);
                return false;
            }

            return true;
        }

        private CombatActor ResolveExecutor()
        {
            if (IsRuntimeActor(_executor) &&
                _executor.IsPlayerActor)
            {
                return _executor;
            }

            if (NetworkClient.active &&
                NetworkClient.localPlayer != null)
            {
                CombatActor local =
                    NetworkClient.localPlayer
                        .GetComponentInChildren<CombatActor>();

                if (IsRuntimeActor(local) &&
                    local.IsPlayerActor)
                {
                    return local;
                }
            }

            CombatActor self =
                GetComponent<CombatActor>();

            if (IsRuntimeActor(self) &&
                self.IsPlayerActor)
            {
                return self;
            }

            if (!_autoResolveActors)
                return null;

            CombatActor[] actors =
                Object.FindObjectsByType<CombatActor>(
                    FindObjectsSortMode.None);

            CombatActor result = null;

            for (int i = 0; i < actors.Length; i++)
            {
                CombatActor actor = actors[i];

                if (!IsRuntimeActor(actor) ||
                    !actor.IsPlayerActor)
                {
                    continue;
                }

                if (result == null ||
                    actor.ActorId < result.ActorId)
                {
                    result = actor;
                }
            }

            return result;
        }

        private CombatActor ResolveTarget(
            CombatActor executor)
        {
            if (IsRuntimeActor(_target) &&
                _target != executor)
            {
                return _target;
            }

            if (!_autoResolveActors ||
                executor == null)
            {
                return null;
            }

            CombatActor[] actors =
                Object.FindObjectsByType<CombatActor>(
                    FindObjectsSortMode.None);

            CombatActor nearest = null;
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < actors.Length; i++)
            {
                CombatActor actor = actors[i];

                if (!IsRuntimeActor(actor) ||
                    actor == executor)
                {
                    continue;
                }

                float distance =
                    (actor.transform.position -
                     executor.transform.position).sqrMagnitude;

                if (nearest == null ||
                    distance < nearestDistance)
                {
                    nearest = actor;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private CharacterCombatConfig ResolveConfig()
        {
            if (_config != null)
                return _config;

            GameDataManager data =
                GameDataManager.Instance;

            if (data == null ||
                data.Player == null)
            {
                return null;
            }

            return data.Player.combat;
        }

        private static bool IsRuntimeActor(
            CombatActor actor)
        {
            return actor != null &&
                   actor.gameObject.activeInHierarchy &&
                   actor.gameObject.scene.IsValid();
        }

        private void ResetTracking()
        {
            _trackedExecutionId = 0;
            _trackedExecutor = null;
            _trackedTarget = null;
        }

        private static string FormatVector(
            Vector3 value)
        {
            return
                $"({value.x:F3}, {value.y:F3}, {value.z:F3})";
        }
    }
}
