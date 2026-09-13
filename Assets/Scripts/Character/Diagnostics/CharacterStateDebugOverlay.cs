using Character.Combat;
using Character.Config;
using Character.Controller;
using Character.Execution;
using Character.LockOn;
using Character.StateMachine;
using Character.StateMachine.States;
using Character.Sync;
using Core;
using Input;
using Mirror;
using UnityEngine;

namespace Character.Diagnostics
{
    /// <summary>
    /// 为本地 Player 提供状态、处决资格和 Motion Warping 运行时诊断。
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class CharacterStateDebugOverlay : MonoBehaviour
    {
        private const string TrueColor = "#62D96B";
        private const string FalseColor = "#FF5B5B";
        private const string DetailColor = "#B8B8B8";
        private const int CandidateBufferSize = 64;

        private enum DiagnosticTargetSource : byte
        {
            None = 0,
            Resolver = 1,
            Locked = 2,
            NearestEligibleState = 3,
            NearestActor = 4,
        }

        private struct ExecutionDiagnosticSnapshot
        {
            public CombatActor Executor;
            public CombatActor Target;
            public CombatActor ResolverTarget;
            public CharacterCombatConfig Config;
            public DiagnosticTargetSource TargetSource;
            public CharacterStateId ExecutorState;
            public CharacterStateId TargetState;
            public ExecutionEligibilityResult Spatial;
            public ExecutionEligibilityResult FullEligibility;
            public ExecutionPhysicsDiagnosticResult Physics;
            public bool RuntimeReady;
            public bool AuthorityAvailable;
            public bool LocalInputAuthority;
            public bool AttackPressedRecently;
            public bool GuardReleased;
            public bool DodgeReleased;
            public bool ParryReleased;
            public bool ConfigValid;
            public bool ExecutorIsPlayer;
            public bool ExecutorAlive;
            public bool ExecutorStateEligible;
            public bool ExecutorCanEnter;
            public bool ExecutorUnoccupied;
            public bool TargetFound;
            public bool TargetNotSelf;
            public bool TargetAlive;
            public bool TargetStateEligible;
            public bool TargetCanEnter;
            public bool TargetUnoccupied;
            public bool TargetDiscoverable;
            public bool CandidateSearchNotOverflowed;
            public bool ResolverFoundCandidate;

            public bool ReadyBeforeClick =>
                RuntimeReady &&
                AuthorityAvailable &&
                LocalInputAuthority &&
                GuardReleased &&
                DodgeReleased &&
                ParryReleased &&
                ConfigValid &&
                ExecutorIsPlayer &&
                ExecutorAlive &&
                ExecutorStateEligible &&
                ExecutorCanEnter &&
                ExecutorUnoccupied &&
                TargetFound &&
                TargetNotSelf &&
                TargetAlive &&
                TargetStateEligible &&
                TargetCanEnter &&
                TargetUnoccupied &&
                TargetDiscoverable &&
                CandidateSearchNotOverflowed &&
                FullEligibility.IsEligible &&
                ResolverFoundCandidate;
        }

        [SerializeField] private PlayerController _player;
        [SerializeField] private RemoteActionApplier _remoteActionApplier;

        [Header("Layout")]
        [Tooltip("x: distance from the right edge; y: distance from the top edge.")]
        [SerializeField] private Vector2 _screenOffset = new Vector2(12f, 12f);
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private float _minScale = 0.6f;
        [SerializeField] private float _maxScale = 2f;
        [SerializeField] private int _fontSize = 20;
        [SerializeField] private Vector2 _buttonSize = new Vector2(112f, 28f);
        [SerializeField] private float _buttonToPanelGap = 8f;
        [SerializeField] private bool _isOpen;
        [SerializeField] private bool _isExecutionOpen;
        [SerializeField] private bool _showNumericId = true;

        private readonly Collider[] _candidateHits =
            new Collider[CandidateBufferSize];

        private InputHandler _inputHandler;
        private PlayerAuthorityGate _authorityGate;
        private CombatActor _combatActor;
        private PlayerLockOnController _lockOn;
        private CharacterStateId _lastSeen;
        private CharacterStateId _previous;
        private ExecutionDiagnosticSnapshot _executionDiagnostic;
        private Vector2 _executionScrollPosition;
        private float _lastAttackPressedAt = float.NegativeInfinity;

        private void Awake()
        {
            if (_player == null)
                _player = GetComponent<PlayerController>();
            if (_remoteActionApplier == null)
                _remoteActionApplier = GetComponent<RemoteActionApplier>();

            _inputHandler = GetComponent<InputHandler>();
            _authorityGate = GetComponent<PlayerAuthorityGate>();
            _combatActor = GetComponent<CombatActor>();
            _lockOn = GetComponent<PlayerLockOnController>();
        }

        private void Update()
        {
            if (_player == null)
                return;

            CharacterStateId current = _player.CurrentStateId;
            if (_lastSeen != current)
            {
                _previous = _lastSeen;
                _lastSeen = current;
            }

            if (_inputHandler != null && _inputHandler.AttackTriggered)
                _lastAttackPressedAt = Time.unscaledTime;

            if (_isExecutionOpen)
                _executionDiagnostic = BuildExecutionDiagnostic();
        }

        /// <summary>
        /// State 与 Execution 使用同一纵向流式布局；任一面板展开都会把后续按钮下推。
        /// </summary>
        private void OnGUI()
        {
            if (_player == null)
                return;

            NetworkIdentity netId = _player.GetComponent<NetworkIdentity>();
            if (netId != null && NetworkClient.active && !netId.isLocalPlayer)
                return;

            GUIStyle stateLabel = new GUIStyle(GUI.skin.box)
            {
                fontSize = _fontSize,
                alignment = TextAnchor.UpperRight,
                richText = true,
            };

            GUIStyle button = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(10, _fontSize - 4),
                alignment = TextAnchor.MiddleCenter,
            };

            GUIStyle executionLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(11, _fontSize - 4),
                alignment = TextAnchor.UpperLeft,
                richText = true,
                wordWrap = false,
            };

            GUIStyle executionHeading = new GUIStyle(executionLabel)
            {
                fontSize = _fontSize,
                fontStyle = FontStyle.Bold,
            };

            Matrix4x4 oldMatrix = GUI.matrix;
            float scale = ResolveGuiScale();
            GUI.matrix = Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                new Vector3(scale, scale, 1f));

            float scaledScreenWidth = Screen.width / scale;
            float scaledScreenHeight = Screen.height / scale;
            float cursorY = _screenOffset.y;
            float buttonX = scaledScreenWidth - _buttonSize.x - _screenOffset.x;

            Rect stateButtonRect = new Rect(
                buttonX,
                cursorY,
                _buttonSize.x,
                _buttonSize.y);
            if (GUI.Button(
                    stateButtonRect,
                    _isOpen ? "State ^" : "State v",
                    button))
            {
                _isOpen = !_isOpen;
            }

            cursorY += _buttonSize.y + _buttonToPanelGap;
            if (_isOpen)
            {
                cursorY += DrawStatePanel(
                    scaledScreenWidth,
                    cursorY,
                    stateLabel);
                cursorY += _buttonToPanelGap;
            }

            Rect executionButtonRect = new Rect(
                buttonX,
                cursorY,
                _buttonSize.x,
                _buttonSize.y);
            if (GUI.Button(
                    executionButtonRect,
                    _isExecutionOpen ? "Execution ^" : "Execution v",
                    button))
            {
                _isExecutionOpen = !_isExecutionOpen;
                if (_isExecutionOpen)
                    _executionDiagnostic = BuildExecutionDiagnostic();
            }

            cursorY += _buttonSize.y + _buttonToPanelGap;
            if (_isExecutionOpen)
            {
                DrawExecutionPanel(
                    scaledScreenWidth,
                    scaledScreenHeight,
                    cursorY,
                    executionLabel,
                    executionHeading);
            }

            GUI.matrix = oldMatrix;
        }

        private float DrawStatePanel(
            float scaledScreenWidth,
            float y,
            GUIStyle label)
        {
            CharacterStateId current = _player.CurrentStateId;
            bool hasExecutingState =
                _player.TryGetActiveExecutingState(out ExecutingState executingState);
            ExecutionWarpContext warp = hasExecutingState
                ? executingState.WarpContext
                : null;

            float width = warp != null ? 720f : 420f;
            float height = warp != null ? 352f : 128f;
            float x = scaledScreenWidth - width - _screenOffset.x;

            GUILayout.BeginArea(new Rect(x, y, width, height));
            GUILayout.Label($"<b>Current</b>:  {FormatState(current)}", label);
            GUILayout.Label($"<b>Previous</b>: {FormatState(_previous)}", label);
            GUILayout.Label($"<b>Sprint Phase</b>: {FormatSprintPhase()}", label);
            GUILayout.Label($"<b>Guard Phase</b>:  {FormatGuardPhase()}", label);

            if (warp != null)
            {
                GUILayout.Label(
                    $"<b>Execution Warp</b>: #{warp.ExecutionId}  " +
                    $"time={executingState.NormalizedTime:F3}  " +
                    $"window=[{warp.WindowStartNormalized:F3}, {warp.WindowEndNormalized:F3}]",
                    label);
                GUILayout.Label(
                    $"<b>Weight</b>: {warp.LastCumulativeWeight:F3}  " +
                    $"windowComplete={warp.IsWarpWindowComplete}",
                    label);
                GUILayout.Label(
                    $"<b>Root Delta Pos</b>: {FormatVector(warp.LastOriginalDeltaPosition)}" +
                    $" -> {FormatVector(warp.LastCorrectedDeltaPosition)}",
                    label);
                GUILayout.Label(
                    $"<b>Root Delta Yaw</b>: {warp.LastOriginalDeltaYaw:F2} deg" +
                    $" -> {warp.LastCorrectedDeltaYaw:F2} deg",
                    label);
                GUILayout.Label(
                    $"<b>Requested Correction</b>: " +
                    $"pos={FormatVector(warp.RequestedTranslationCorrection)}  " +
                    $"yaw={warp.RequestedYawCorrection:F2} deg",
                    label);
                GUILayout.Label(
                    $"<b>Position Residual</b>: " +
                    $"pred={FormatVector(warp.PredictedRemainingPositionError)}  " +
                    $"actual={FormatVector(warp.ActualRemainingPositionError)}",
                    label);
                GUILayout.Label(
                    $"<b>Yaw Residual</b>: " +
                    $"pred={warp.PredictedRemainingYawError:F2} deg  " +
                    $"actual={warp.ActualRemainingYawError:F2} deg",
                    label);
                GUILayout.Label(
                    warp.HasMotorApplicationSample
                        ? $"<b>Motor Shortfall</b>: " +
                          $"pos={FormatVector(warp.LastMotorPositionShortfall)}  " +
                          $"yaw={warp.LastMotorYawShortfall:F2} deg"
                        : "<b>Motor Shortfall</b>: waiting for first applied delta",
                    label);
            }

            GUILayout.EndArea();
            return height;
        }

        private void DrawExecutionPanel(
            float scaledScreenWidth,
            float scaledScreenHeight,
            float y,
            GUIStyle label,
            GUIStyle heading)
        {
            const float width = 640f;
            float availableHeight = scaledScreenHeight - y - _screenOffset.y;
            float height = Mathf.Max(100f, Mathf.Min(720f, availableHeight));
            float x = scaledScreenWidth - width - _screenOffset.x;
            ExecutionDiagnosticSnapshot snapshot = _executionDiagnostic;

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.box);
            _executionScrollPosition = GUILayout.BeginScrollView(
                _executionScrollPosition,
                false,
                true);

            GUILayout.Label("Execution Preconditions", heading);
            DrawCondition(
                label,
                "READY BEFORE LEFT CLICK",
                snapshot.ReadyBeforeClick);
            DrawCondition(
                label,
                "Left Mouse Pressed (last 0.25s)",
                snapshot.AttackPressedRecently);

            string rejection = snapshot.FullEligibility.IsEvaluated
                ? snapshot.FullEligibility.RejectionReason.ToString()
                : "NotEvaluated";
            DrawInfo(label, $"Current rejection: {rejection}");

            GUILayout.Space(6f);
            GUILayout.Label("Request", heading);
            DrawCondition(label, "Execution Runtime", snapshot.RuntimeReady);
            DrawCondition(label, "Authority Available", snapshot.AuthorityAvailable);
            DrawCondition(label, "Local Input Authority", snapshot.LocalInputAuthority);
            DrawCondition(label, "Guard Released", snapshot.GuardReleased);
            DrawCondition(label, "Dodge Not Pressed", snapshot.DodgeReleased);
            DrawCondition(label, "Parry Not Pressed", snapshot.ParryReleased);
            DrawCondition(label, "Combat Config Valid", snapshot.ConfigValid);

            GUILayout.Space(6f);
            GUILayout.Label("Executor", heading);
            DrawInfo(label, FormatActor("Executor", snapshot.Executor, snapshot.ExecutorState));
            DrawCondition(label, "Executor Is Player", snapshot.ExecutorIsPlayer);
            DrawCondition(label, "Executor Alive", snapshot.ExecutorAlive);
            DrawCondition(
                label,
                "Executor State Is Idle / Move",
                snapshot.ExecutorStateEligible,
                snapshot.ExecutorState.ToString());
            DrawCondition(label, "Executor Can Enter Executing", snapshot.ExecutorCanEnter);
            DrawCondition(label, "Executor Unoccupied", snapshot.ExecutorUnoccupied);

            GUILayout.Space(6f);
            GUILayout.Label("Candidate / Target", heading);
            DrawInfo(label, $"Diagnostic source: {snapshot.TargetSource}");
            DrawInfo(label, FormatActor("Target", snapshot.Target, snapshot.TargetState));
            DrawInfo(
                label,
                FormatActor(
                    "Resolver target",
                    snapshot.ResolverTarget,
                    snapshot.ResolverTarget != null
                        ? snapshot.ResolverTarget.CurrentStateId
                        : CharacterStateId.None));
            DrawCondition(label, "Target Found", snapshot.TargetFound);
            DrawCondition(label, "Target Is Not Executor", snapshot.TargetNotSelf);
            DrawCondition(label, "Target Alive", snapshot.TargetAlive);
            DrawCondition(
                label,
                "Target State Is Parried / PostureBroken",
                snapshot.TargetStateEligible,
                snapshot.TargetState.ToString());
            DrawCondition(label, "Target Can Enter Executed", snapshot.TargetCanEnter);
            DrawCondition(label, "Target Unoccupied", snapshot.TargetUnoccupied);
            DrawCondition(
                label,
                "Target Discoverable By Lock-On / Overlap",
                snapshot.TargetDiscoverable);
            DrawCondition(
                label,
                "Candidate Search Not Overflowed",
                snapshot.CandidateSearchNotOverflowed);
            DrawCondition(
                label,
                "Resolver Found Eligible Candidate",
                snapshot.ResolverFoundCandidate);

            GUILayout.Space(6f);
            GUILayout.Label("Spatial", heading);
            bool hasSpatial = snapshot.Spatial.HasSpatialSolution;
            DrawCondition(label, "Spatial Solution", hasSpatial);

            CharacterCombatConfig config = snapshot.Config;
            DrawCondition(
                label,
                "Horizontal Distance",
                hasSpatial && config != null &&
                snapshot.Spatial.HorizontalDistance <= config.executionMaxDistance,
                config != null
                    ? $"{snapshot.Spatial.HorizontalDistance:F3} <= {config.executionMaxDistance:F3} m"
                    : "n/a");
            DrawCondition(
                label,
                "Target Front Angle",
                hasSpatial && config != null &&
                snapshot.Spatial.TargetFrontAngle <= config.executionFrontHalfAngle,
                config != null
                    ? $"{snapshot.Spatial.TargetFrontAngle:F2} <= {config.executionFrontHalfAngle:F2} deg"
                    : "n/a");
            DrawCondition(
                label,
                "Height Difference",
                hasSpatial && config != null &&
                snapshot.Spatial.HeightDifference <= config.executionMaxHeightDifference,
                config != null
                    ? $"{snapshot.Spatial.HeightDifference:F3} <= {config.executionMaxHeightDifference:F3} m"
                    : "n/a");
            DrawCondition(
                label,
                "Warp Translation Budget",
                hasSpatial && config != null &&
                snapshot.Spatial.WarpTranslationError <= config.executionMaxWarpTranslation,
                config != null
                    ? $"{snapshot.Spatial.WarpTranslationError:F3} <= {config.executionMaxWarpTranslation:F3} m"
                    : "n/a");
            DrawCondition(
                label,
                "Warp Yaw Budget",
                hasSpatial && config != null &&
                snapshot.Spatial.WarpYawError <= config.executionMaxWarpYaw,
                config != null
                    ? $"{snapshot.Spatial.WarpYawError:F2} <= {config.executionMaxWarpYaw:F2} deg"
                    : "n/a");

            GUILayout.Space(6f);
            GUILayout.Label("Physics", heading);
            DrawCondition(label, "Physics Evaluated", snapshot.Physics.IsEvaluated);
            DrawCondition(label, "Line Of Sight Clear", snapshot.Physics.HasClearLineOfSight);
            DrawCondition(
                label,
                "Executor Collision Shape Found",
                snapshot.Physics.HasExecutorCollisionShape);
            DrawCondition(label, "Path To Anchor Clear", snapshot.Physics.HasClearPath);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private ExecutionDiagnosticSnapshot BuildExecutionDiagnostic()
        {
            var snapshot = new ExecutionDiagnosticSnapshot
            {
                Executor = _combatActor,
                ExecutorState = _combatActor != null
                    ? _combatActor.CurrentStateId
                    : CharacterStateId.None,
                RuntimeReady = ExecutionRuntime.Instance != null,
                LocalInputAuthority =
                    _authorityGate == null ||
                    _authorityGate.CanProcessLocalInput,
                AttackPressedRecently =
                    Time.unscaledTime - _lastAttackPressedAt <= 0.25f,
                GuardReleased =
                    _inputHandler == null ||
                    !_inputHandler.IsGuardHeld,
                DodgeReleased =
                    _inputHandler == null ||
                    !_inputHandler.DodgeTriggered,
                ParryReleased =
                    _inputHandler == null ||
                    !_inputHandler.ParryTriggered,
            };

            ExecutionRuntime runtime = ExecutionRuntime.Instance;
            snapshot.AuthorityAvailable =
                runtime != null && runtime.HasAuthority;
            snapshot.Config = ResolvePlayerCombatConfig();
            snapshot.ConfigValid =
                ExecutionSpatialValidator.IsConfigurationValid(snapshot.Config);

            snapshot.ExecutorIsPlayer =
                snapshot.Executor != null &&
                snapshot.Executor.IsPlayerActor;
            snapshot.ExecutorAlive =
                snapshot.Executor != null &&
                !snapshot.Executor.IsDead;
            snapshot.ExecutorStateEligible =
                ExecutionEligibilityPrecheck.IsExecutorStateEligible(
                    snapshot.ExecutorState);
            snapshot.ExecutorCanEnter =
                snapshot.Executor != null &&
                snapshot.Executor.CanEnterExecutionAsExecutor();

            IExecutionOccupancyQuery occupancy =
                runtime != null ? runtime.OccupancyQuery : null;
            snapshot.ExecutorUnoccupied =
                snapshot.Executor != null &&
                occupancy != null &&
                !occupancy.IsActorOccupied(snapshot.Executor.ActorId);

            if (runtime != null &&
                snapshot.Executor != null &&
                snapshot.Config != null)
            {
                snapshot.ResolverFoundCandidate =
                    runtime.CandidateResolver.TrySelect(
                        snapshot.Executor,
                        _lockOn,
                        snapshot.Config,
                        out CombatActor resolverTarget,
                        out _,
                        occupancy);
                snapshot.ResolverTarget = resolverTarget;
                snapshot.CandidateSearchNotOverflowed =
                    !runtime.CandidateResolver.DidLastQueryOverflow;
            }

            snapshot.Target = ResolveDiagnosticTarget(
                snapshot.Executor,
                snapshot.ResolverTarget,
                out DiagnosticTargetSource targetSource);
            snapshot.TargetSource = targetSource;
            snapshot.TargetFound = snapshot.Target != null;
            snapshot.TargetNotSelf =
                snapshot.Target != null &&
                snapshot.Executor != null &&
                snapshot.Target != snapshot.Executor &&
                snapshot.Target.ActorId != snapshot.Executor.ActorId;
            snapshot.TargetState = snapshot.Target != null
                ? snapshot.Target.CurrentStateId
                : CharacterStateId.None;
            snapshot.TargetAlive =
                snapshot.Target != null &&
                !snapshot.Target.IsDead;
            snapshot.TargetStateEligible =
                ExecutionEligibilityPrecheck.IsTargetStateEligible(
                    snapshot.TargetState);
            snapshot.TargetCanEnter =
                snapshot.Target != null &&
                snapshot.Target.CanEnterExecutionAsTarget();
            snapshot.TargetUnoccupied =
                snapshot.Target != null &&
                occupancy != null &&
                !occupancy.IsActorOccupied(snapshot.Target.ActorId);
            bool resolverReturnedDiagnosticTarget =
                snapshot.ResolverFoundCandidate &&
                snapshot.ResolverTarget == snapshot.Target;
            bool diagnosticSearchOverflowed = false;
            snapshot.TargetDiscoverable =
                resolverReturnedDiagnosticTarget ||
                IsTargetDiscoverable(
                    snapshot.Executor,
                    snapshot.Target,
                    snapshot.Config,
                    targetSource == DiagnosticTargetSource.Locked,
                    out diagnosticSearchOverflowed);
            snapshot.CandidateSearchNotOverflowed &=
                resolverReturnedDiagnosticTarget ||
                !diagnosticSearchOverflowed;

            snapshot.Spatial = ExecutionSpatialValidator.Evaluate(
                snapshot.Executor != null ? snapshot.Executor.transform : null,
                snapshot.Target != null ? snapshot.Target.transform : null,
                snapshot.Config);

            snapshot.Physics = ExecutionPhysicsValidator.Inspect(
                snapshot.Executor,
                snapshot.Target,
                snapshot.Config,
                snapshot.Spatial);

            snapshot.FullEligibility =
                ExecutionEligibilityPrecheck.EvaluateCurrent(
                    snapshot.Executor,
                    snapshot.Target,
                    snapshot.Config,
                    occupancy);

            return snapshot;
        }

        private CombatActor ResolveDiagnosticTarget(
            CombatActor executor,
            CombatActor resolverTarget,
            out DiagnosticTargetSource source)
        {
            if (resolverTarget != null)
            {
                source = DiagnosticTargetSource.Resolver;
                return resolverTarget;
            }

            CombatActor lockedTarget = ResolveLockedActor(_lockOn);
            if (lockedTarget != null && lockedTarget != executor)
            {
                source = DiagnosticTargetSource.Locked;
                return lockedTarget;
            }

            CombatActor[] actors = Object.FindObjectsByType<CombatActor>(
                FindObjectsSortMode.None);
            CombatActor nearestEligible = null;
            CombatActor nearestActor = null;
            float nearestEligibleDistance = float.PositiveInfinity;
            float nearestActorDistance = float.PositiveInfinity;

            for (int i = 0; i < actors.Length; i++)
            {
                CombatActor actor = actors[i];
                if (actor == null ||
                    actor == executor ||
                    (executor != null && actor.ActorId == executor.ActorId))
                {
                    continue;
                }

                float distance = executor != null
                    ? (actor.transform.position - executor.transform.position).sqrMagnitude
                    : 0f;

                if (IsBetterDiagnosticTarget(
                        actor,
                        distance,
                        nearestActor,
                        nearestActorDistance))
                {
                    nearestActor = actor;
                    nearestActorDistance = distance;
                }

                if (!ExecutionEligibilityPrecheck.IsTargetStateEligible(
                        actor.CurrentStateId))
                {
                    continue;
                }

                if (IsBetterDiagnosticTarget(
                        actor,
                        distance,
                        nearestEligible,
                        nearestEligibleDistance))
                {
                    nearestEligible = actor;
                    nearestEligibleDistance = distance;
                }
            }

            if (nearestEligible != null)
            {
                source = DiagnosticTargetSource.NearestEligibleState;
                return nearestEligible;
            }

            source = nearestActor != null
                ? DiagnosticTargetSource.NearestActor
                : DiagnosticTargetSource.None;
            return nearestActor;
        }

        private bool IsTargetDiscoverable(
            CombatActor executor,
            CombatActor target,
            CharacterCombatConfig config,
            bool isLockedTarget,
            out bool overflowed)
        {
            overflowed = false;
            if (executor == null || target == null || config == null)
                return false;

            if (isLockedTarget)
                return true;

            float maxDistance = config.executionMaxDistance;
            float maxHeight = config.executionMaxHeightDifference;
            if (!IsNonNegativeFinite(maxDistance) ||
                !IsNonNegativeFinite(maxHeight))
            {
                return false;
            }

            float searchRadius = Mathf.Sqrt(
                maxDistance * maxDistance +
                maxHeight * maxHeight);
            int hitCount = Physics.OverlapSphereNonAlloc(
                executor.transform.position,
                searchRadius,
                _candidateHits,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            if (hitCount >= _candidateHits.Length)
            {
                overflowed = true;
                return false;
            }

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _candidateHits[i];
                CombatActor owner = hit != null
                    ? hit.GetComponentInParent<CombatActor>()
                    : null;
                if (owner == target ||
                    (owner != null && owner.ActorId == target.ActorId))
                {
                    return true;
                }
            }

            return false;
        }

        private static CombatActor ResolveLockedActor(
            PlayerLockOnController lockOn)
        {
            if (lockOn == null || lockOn.CurrentLockOnTarget == null)
                return null;

            Transform root = lockOn.CurrentLockOnTarget.Root;
            if (root == null)
                return null;

            CombatActor actor = root.GetComponentInParent<CombatActor>();
            return actor != null
                ? actor
                : root.GetComponentInChildren<CombatActor>();
        }

        private static bool IsBetterDiagnosticTarget(
            CombatActor actor,
            float distance,
            CombatActor current,
            float currentDistance)
        {
            if (current == null || distance < currentDistance)
                return true;

            return Mathf.Approximately(distance, currentDistance) &&
                   actor.ActorId < current.ActorId;
        }

        private static CharacterCombatConfig ResolvePlayerCombatConfig()
        {
            if (GameDataManager.Instance == null ||
                GameDataManager.Instance.Player == null)
            {
                return null;
            }

            return GameDataManager.Instance.Player.combat;
        }

        private static void DrawCondition(
            GUIStyle style,
            string label,
            bool value,
            string detail = null)
        {
            string color = value ? TrueColor : FalseColor;
            string suffix = string.IsNullOrEmpty(detail)
                ? string.Empty
                : $"  <color={DetailColor}>{detail}</color>";
            GUILayout.Label(
                $"{label}: <color={color}><b>{value.ToString().ToLowerInvariant()}</b></color>{suffix}",
                style);
        }

        private static void DrawInfo(GUIStyle style, string value)
        {
            GUILayout.Label($"<color={DetailColor}>{value}</color>", style);
        }

        private static string FormatActor(
            string label,
            CombatActor actor,
            CharacterStateId state)
        {
            return actor != null
                ? $"{label}: {actor.name}  actorId={actor.ActorId}  state={state}"
                : $"{label}: none";
        }

        private float ResolveGuiScale()
        {
            float referenceWidth = Mathf.Max(1f, _referenceResolution.x);
            float referenceHeight = Mathf.Max(1f, _referenceResolution.y);
            float scale = Mathf.Min(
                Screen.width / referenceWidth,
                Screen.height / referenceHeight);
            return Mathf.Clamp(
                scale,
                Mathf.Max(0.01f, _minScale),
                Mathf.Max(_minScale, _maxScale));
        }

        private string FormatSprintPhase()
        {
            if (_player.TryGetActiveSprintState(out SprintState sprint))
                return sprint.CurrentPhase.ToString();

            return "-";
        }

        private string FormatGuardPhase()
        {
            if (_remoteActionApplier != null)
            {
                if (_remoteActionApplier.CurrentRemoteAction == ActionType.GuardBreak)
                    return "GuardBreak";

                if (_remoteActionApplier.CurrentRemoteAction == ActionType.GuardHit)
                {
                    return FormatGuardReaction(
                        _remoteActionApplier.LastGuardReaction);
                }
            }

            if (_player.TryGetActiveGuardState(out GuardState guard))
                return guard.CurrentPhase.ToString();

            return "-";
        }

        private static string FormatGuardReaction(
            GuardReactionType reaction)
        {
            return reaction switch
            {
                GuardReactionType.Hit1 => "GuardHit1",
                GuardReactionType.Hit2 => "GuardHit2",
                GuardReactionType.Hit3 => "GuardHit3",
                GuardReactionType.Break => "GuardBreak",
                _ => "-",
            };
        }

        private string FormatState(CharacterStateId id)
        {
            string name = id.ToString();
            if (!_showNumericId)
                return name;

            return $"{name}  <color=#888888>({(byte)id})</color>";
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:F3}, {value.y:F3}, {value.z:F3})";
        }

        private static bool IsNonNegativeFinite(float value)
        {
            return value >= 0f &&
                   !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}
