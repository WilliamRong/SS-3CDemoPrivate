using System;
using System.Collections.Generic;
using AI;
using AI.NpcStates;
using Character.Config;
using Character.Controller;
using Character.Execution;
using Character.StateMachine;
using Character.StateMachine.States;
using Character.Sync;
using Core;
using Mirror;
using UnityEngine;

namespace Character.Combat
{
    /// <summary>
    /// 统一承接 Player 与 NPC 的战斗事务，避免两套角色驱动各自计算生命、架势和网络纠正而产生分歧。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatActor : MonoBehaviour, IDamageable
    {
        [SerializeField]
        private int _teamId = 1;

        [SerializeField]
        private CharacterCombatConfig _combatConfig;

        [SerializeField]
        private PlayerController _playerController;
        [SerializeField]
        private NpcCharacterDriver _npcDriver;
        [SerializeField]
        private RemoteActionApplier _remoteActionApplier;
        [SerializeField]
        private RemoteInterpolator _remoteInterpolator;
        [SerializeField]
        private NetworkIdentity _networkIdentity;
        [SerializeField]
        private bool _canReceiveHit = true;

        [SerializeField, Min(0f)]
        private float _currentHp = 100f;

        private readonly Dictionary<AttackMoveId, AttackDefinition> _fallbackDefinitions = new();

        private bool _trackingAttack;
        private bool _suppressCurrentAttackUntilStateExit;
        private AttackMoveId _trackedAttackId;
        private float _trackedAttackElapsed;
        private int _trackedAttackInstanceId;
        private int _nextAttackInstanceId = 1;

        private AttackDefinition _trackedAttackDefinition;
        private float _maxHp = 100f;
        private bool _healthInitialized;

        private bool _hasAppliedHealthRevision;
        private ulong _lastCommittedExecutionDamageId;
        private bool _lastExecutionDamageWasLethal;

        private PostureRuntime _posture;
        private bool _wasDead;

        public uint HealthRevision { get; private set; }

        public float CurrentHp => _playerController != null ? _playerController.CurrentHp : _currentHp;

        public int TeamId => _teamId;

        public float MaxHp => _playerController != null ? _playerController.MaxHp : _maxHp;
        public float HealthRatio => MaxHp > 0f ? Mathf.Clamp01(CurrentHp / MaxHp) : 0f;

        public float CurrentPosture => _posture?.Current ?? 0f;
        public float MaxPosture => _posture?.Max ?? 0f;
        public float PostureRatio => _posture?.Ratio ?? 0f;
        public bool IsPostureFull => _posture?.IsFull ?? false;
        public float PostureRecoveryDelayRemaining => _posture?.RecoveryDelayRemaining ?? 0f;

        public GuardReactionType LastGuardReactionType { get; private set; }
        public CombatHitResult LastHitResult { get; private set; }

        public event Action<float, float> HealthChanged;
        public event Action<float, float> PostureChanged;
        public event Action<float> DamageTaken;

        public event Action<HitInfo> HitBlocked;

        public int ActorId
        {
            get
            {
                if (_networkIdentity != null && _networkIdentity.netId != 0)
                    return unchecked((int)_networkIdentity.netId);

                return GetInstanceID();
            }
        }

        public bool IsPlayerActor
        {
            get
            {
                EnsureReferences();
                return _playerController != null &&
                       _npcDriver == null;
            }
        }

        public CharacterStateId CurrentStateId
        {
            get
            {
                EnsureReferences();
                return ResolveCurrentStateId();
            }
        }

        private readonly InvulnerabilityRuntime _invulnerability = new();
        private readonly AttackSuppressionRuntime _attackSuppression = new();

        private CombatHurtBox[] _hurtBoxes = Array.Empty<CombatHurtBox>();

        public bool IsInvincible => _invulnerability.IsActive;

        public int InvulnerabilityOwnerCount => _invulnerability.ActiveOwnerCount;

        public bool IsAttackSuppressed => _attackSuppression.IsActive;

        public int AttackSuppressionOwnerCount => _attackSuppression.ActiveOwnerCount;

        public bool CanProduceCombatHit => !IsDead && !IsAttackSuppressed;

        public bool CanReceiveHit => _canReceiveHit && !IsDead && !IsInvincible;

        public bool IsDead
        {
            get
            {
                if (CurrentHp <= 0f)
                {
                    return true;
                }

                // Remote NPCs do not advance their local authoritative FSM. After a GM
                // revive, the local driver can still be in Dead until the next snapshot
                // arrives, so the latest remote snapshot must take precedence here.
                if (!NetworkServer.active &&
                    _npcDriver != null &&
                    _remoteInterpolator != null &&
                    _remoteInterpolator.LastAppliedSnapshot.Tick > 0)
                {
                    return _remoteInterpolator.LastAppliedSnapshot.StateId ==
                           CharacterStateId.Dead;
                }

                if (_playerController != null && _playerController.CurrentStateId == CharacterStateId.Dead)
                {
                    return true;
                }

                if (_npcDriver != null && _npcDriver.CurrentStateId == CharacterStateId.Dead)
                {
                    return true;
                }

                if (_remoteInterpolator != null
                   && _remoteInterpolator.LastAppliedSnapshot.Tick > 0)
                {
                    return _remoteInterpolator.LastAppliedSnapshot.StateId == CharacterStateId.Dead;
                }

                if (_remoteActionApplier != null
                   && _remoteActionApplier.CurrentRemoteAction == ActionType.Dead)
                {
                    return true;
                }

                return false;
            }
        }

        public bool AcquireInvulnerability(InvulnerabilitySource source, ulong ownerId)
        {
            return _invulnerability.Acquire(source, ownerId);
        }

        public bool ReleaseInvulnerability(InvulnerabilitySource source, ulong ownerId)
        {
            return _invulnerability.Release(source, ownerId);
        }


        public bool AcquireAttackSuppression(ulong executionId)
        {
            if (executionId == 0) return false;

            CancelCurrentAttack();

            return _attackSuppression.Acquire(executionId);
        }

        public bool ReleaseAttackSuppression(ulong executionId)
        {
            return _attackSuppression.Release(executionId);
        }

        public bool BeginExecutionCombatSuppression(ulong executionId)
        {
            if (executionId == 0) return false;

            bool attackAcquired = AcquireAttackSuppression(executionId);

            bool invulnerabilityAcquired = AcquireInvulnerability(InvulnerabilitySource.Execution, executionId);

            if (attackAcquired && invulnerabilityAcquired) return true;

            if (invulnerabilityAcquired)
                ReleaseInvulnerability(InvulnerabilitySource.Execution, executionId);

            if (attackAcquired)
                ReleaseAttackSuppression(executionId);

            return false;
        }

        public bool EndExecutionCombatSuppression(ulong executionId)
        {
            if (executionId == 0) return false;

            // Keep attacks suppressed while HurtBoxes are being restored.
            bool invulnerabilityReleased = ReleaseInvulnerability(
                InvulnerabilitySource.Execution,
                executionId);
            bool attackSuppressionReleased =
                ReleaseAttackSuppression(executionId);

            return invulnerabilityReleased ||
                   attackSuppressionReleased;
        }


        private void HandleInvulnerabilityChanged(bool isInvincible)
        {
            bool hurtBoxEnabled = !isInvincible;

            for (int i = 0; i < _hurtBoxes.Length; i++)
            {
                CombatHurtBox hurtBox = _hurtBoxes[i];
                if (hurtBox != null)
                    hurtBox.SetCombatEnabled(hurtBoxEnabled);
            }
        }

        private void OnDestroy()
        {
            _invulnerability.Changed -= HandleInvulnerabilityChanged;
        }

        // ============ Unity 生命周期 ============

        private void Reset()
        {
            EnsureReferences();
            if (_npcDriver != null) _teamId = 2;
            else _teamId = 1;
        }

        private void Awake()
        {
            EnsureReferences();
            _hurtBoxes = GetComponentsInChildren<CombatHurtBox>(includeInactive: true);

            _invulnerability.Changed += HandleInvulnerabilityChanged;
            HandleInvulnerabilityChanged(_invulnerability.IsActive);


            InitializeHealth(force: true);
            InitializePosture(forceNotify: true);
            _wasDead = IsDead;
        }

        private void Start()
        {
            if (_currentHp > 0f)
                InitializeHealth(force: false);

            InitializePosture(forceNotify: false);
        }

        private void Update()
        {
            TickAttackRuntime(Time.deltaTime);
            TickPosture(Time.deltaTime);
        }

        // ============ 权威生命状态 ============

        private void CommitHealthChange(float previousHp)
        {
            if (Mathf.Approximately(previousHp, CurrentHp)) return;

            unchecked
            {
                HealthRevision++;
                if (HealthRevision == 0) HealthRevision = 1;
            }
        }

        /// <summary>
        /// revision 只允许权威状态单向前进，防止乱序到达的旧快照覆盖客户端已经看到的新生命值。
        /// </summary>
        public bool ApplyAuthoritativeHealth(float currentHp, float maxHp, uint revision)
        {
            if (_hasAppliedHealthRevision && revision <= HealthRevision) return false;

            float previousHp = CurrentHp;

            if (_playerController != null)
            {
                _playerController.ApplyAuthoritativeHealth(currentHp, maxHp);
            }
            else
            {
                _maxHp = Mathf.Max(1f, maxHp);
                _currentHp = Mathf.Clamp(currentHp, 0f, _maxHp);
                _healthInitialized = true;
            }

            HealthRevision = revision;
            _hasAppliedHealthRevision = true;

            float appliedDamage = Mathf.Max(0f, previousHp - CurrentHp);
            if (appliedDamage > 0f)
                DamageTaken?.Invoke(appliedDamage);

            HealthChanged?.Invoke(CurrentHp, MaxHp);
            return true;
        }

        /// <summary>
        /// 复活同时恢复生命与架势，确保新的战斗生命周期不会继承死亡前尚未恢复的架势。
        /// </summary>
        public bool RestoreFullHealthForRevive()
        {
            if (!HasPostureSimulationAuthority())
                return false;

            float previousHp = CurrentHp;

            if (_playerController != null)
            {
                _playerController.Revive(MaxHp);
            }
            else
            {
                InitializeHealth(force: false);
                _currentHp = _maxHp;
                HealthChanged?.Invoke(_currentHp, _maxHp);
            }

            CommitHealthChange(previousHp);
            ResetPosture(forceNotify: true);
            _wasDead = false;

            return CurrentHp > 0f;
        }


        /// <summary>
        /// Applies the execution's authoritative damage without going through HurtBox.
        /// Repeating the same execution id returns the original result without damaging again.
        /// </summary>
        public bool TryCommitExecutionDamage(
            ulong executionId,
            float damage,
            out bool targetDied)
        {
            EnsureReferences();

            targetDied = false;

            if (!HasPostureSimulationAuthority() || executionId == 0 ||
                float.IsNaN(damage) || float.IsInfinity(damage) || damage < 0f ||
                !IsBoundToExecutedSession(executionId))
            {
                return false;
            }

            if (_lastCommittedExecutionDamageId == executionId)
            {
                targetDied = _lastExecutionDamageWasLethal;
                return true;
            }

            float previousHp = CurrentHp;
            if (_playerController != null)
            {
                _playerController.ApplyAuthoritativeHealth(
                    Mathf.Max(0f, previousHp - damage),
                    MaxHp);
            }
            else
            {
                ApplyHealthDamage(damage);
            }

            CommitHealthChange(previousHp);
            ResetPosture(forceNotify: true);
            targetDied = CurrentHp <= 0f;
            _wasDead = targetDied;

            _lastCommittedExecutionDamageId = executionId;
            _lastExecutionDamageWasLethal = targetDied;

            HealthChanged?.Invoke(CurrentHp, MaxHp);
            return true;
        }

        // ============ 权威架势状态 ============

        public float AddPosture(float amount)
        {
            if (!HasPostureSimulationAuthority())
                return 0f;

            InitializePosture(forceNotify: false);
            CharacterCombatConfig config = ResolveCombatConfig();
            float recoveryDelay = config != null ? config.postureRecoveryDelay : 2f;

            return _posture.Add(amount, recoveryDelay);
        }

        /// <summary>
        /// 权威端只把架势提高到目标值，并允许调试或玩法入口指定恢复延迟。
        /// 不会降低已经更高的架势，也不会直接触发崩防状态。
        /// </summary>
        public float RaisePostureTo(
            float targetPosture,
            float recoveryDelay)
        {
            if (!HasPostureSimulationAuthority())
                return 0f;

            InitializePosture(forceNotify: false);
            float amount = targetPosture - _posture.Current;

            return amount > 0f
                ? _posture.Add(amount, recoveryDelay)
                : 0f;
        }

        public bool ResetPosture(bool forceNotify = false)
        {
            if (!HasPostureSimulationAuthority())
                return false;

            InitializePosture(forceNotify: false);
            return _posture.Reset(forceNotify);
        }

        public bool ApplyAuthoritativePosture(
           float currentPosture,
           float maxPosture)
        {
            if (_posture == null)
                InitializePosture(forceNotify: false);

            return _posture.ApplyAuthoritative(
                currentPosture,
                maxPosture);
        }

        // ============ 命中事务 ============

        /// <summary>
        /// 在一次事务内先完成数值结算，再按 Dead、PostureBreak、Hit/Guard 的优先级选择唯一反应，避免同一命中触发互相冲突的状态。
        /// </summary>
        public bool ApplyHit(in HitInfo hit)
        {
            LastHitResult = default;
            LastGuardReactionType = GuardReactionType.None;

            if (!HasPostureSimulationAuthority() ||
                !CanReceiveHit)
            {
                return false;
            }

            if (GetCurrentParryPhase() == ParryPhase.Active)
            {
                LastHitResult = new CombatHitResult
                {
                    Applied = true,
                    AppliedHealthDamage = 0f,
                    AppliedPostureDamage = 0f,
                    WasParried = true,
                    FinalReaction = CombatReactionType.Parry
                };
                return true;
            }

            InitializePosture(forceNotify: false);

            bool wasPostureBroken = IsPostureBroken();

            bool wasGuardBreak = TryResolveGuardBreak(
                hit,
                out float healthDamageMultiplier);
            bool wasGuarded = wasGuardBreak;

            if (!wasGuardBreak &&
                TryResolveGuard(
                    hit,
                    out float guardDamageMultiplier))
            {
                wasGuarded = true;
                healthDamageMultiplier = guardDamageMultiplier;
            }

            float requestedHealthDamage = Mathf.Max(
                0f,
                hit.damage * healthDamageMultiplier);
            float previousHp = CurrentHp;
            float appliedHealthDamage =
                ApplyResolvedHealthDamage(requestedHealthDamage);

            CommitHealthChange(previousHp);

            if (wasGuarded && !wasGuardBreak)
                HitBlocked?.Invoke(hit);

            bool isDead = CurrentHp <= 0f;
            float appliedPostureDamage = 0f;
            bool causedPostureBreak = false;

            if (isDead)
            {
                ResetPosture(forceNotify: true);
                _wasDead = true;
                TryEnterDeadState();
            }
            else if (!wasPostureBroken)
            {
                AttackDefinition attackDefinition =
                    hit.attack ??
                    ResolveAttackDefinition(hit.attackId);

                float requestedPostureDamage = wasGuarded
                    ? attackDefinition.guardedPostureDamage
                    : attackDefinition.postureDamage;

                appliedPostureDamage =
                    AddPosture(requestedPostureDamage);

                if (IsPostureFull &&
                    TryEnterPostureBrokenState())
                {
                    ResetPosture();
                    causedPostureBreak = true;
                }
            }

            GuardReactionType guardReaction =
                GuardReactionType.None;
            CombatReactionType finalReaction;

            if (isDead)
            {
                finalReaction = CombatReactionType.Dead;
            }
            else if (causedPostureBreak)
            {
                finalReaction = CombatReactionType.PostureBreak;
            }
            else if (wasPostureBroken || IsPostureBroken())
            {
                // 崩防期继续接受生命伤害，但保持不可被普通受击动画打断。
                finalReaction = CombatReactionType.None;
            }
            else if (wasGuardBreak)
            {
                TryEnterHitReaction(
                    isHeavyHit: true,
                    hit.hitVariant);

                guardReaction = GuardReactionType.Break;
                finalReaction = CombatReactionType.GuardBreak;
            }
            else if (wasGuarded)
            {
                guardReaction = SelectGuardReaction(
                    hit.attackId,
                    hit.isHeavyHit);
                finalReaction = CombatReactionType.GuardHit;
            }
            else
            {
                TryEnterHitReaction(
                    hit.isHeavyHit,
                    hit.hitVariant);

                finalReaction = CombatReactionType.Hit;
            }

            LastGuardReactionType = guardReaction;
            LastHitResult = new CombatHitResult
            {
                Applied = true,
                AppliedHealthDamage = appliedHealthDamage,
                AppliedPostureDamage = appliedPostureDamage,
                WasGuarded = wasGuarded,
                WasGuardBreak = wasGuardBreak,
                WasPostureBroken = wasPostureBroken,
                CausedPostureBreak = causedPostureBreak,
                IsDead = isDead,
                GuardReaction = guardReaction,
                FinalReaction = finalReaction
            };

            return true;
        }

        private float ApplyResolvedHealthDamage(float damage)
        {
            float previousHp = CurrentHp;

            if (_playerController != null)
                _playerController.ApplyHealthDamageOnly(damage);
            else
                ApplyHealthDamage(damage);

            return Mathf.Max(0f, previousHp - CurrentHp);
        }

        // ============ 攻击运行时跟踪 ============

        public bool TryGetCurrentAttack(out AttackRuntimeInfo attack)
        {
            attack = default;

            if (!CanProduceCombatHit || !_trackingAttack || _trackedAttackDefinition == null) return false;

            attack = new AttackRuntimeInfo(
                this,
                _trackedAttackDefinition,
                _trackedAttackId,
                _trackedAttackInstanceId,
                _trackedAttackElapsed);

            return attack.IsValid;
        }

        /// <summary>
        /// 招式变化立即开启新攻击实例，未处于攻击状态则清空跟踪，防止上一段命中窗口跨状态残留。
        /// </summary>
        private void TickAttackRuntime(float deltaTime)
        {
            if (!TryReadActiveAttackId(out AttackMoveId attackId))
            {
                StopTrackingAttack();
                _suppressCurrentAttackUntilStateExit = false;
                return;
            }

            if (_suppressCurrentAttackUntilStateExit)
            {
                StopTrackingAttack();
                return;
            }

            if (!_trackingAttack || _trackedAttackId != attackId)
            {
                BeginTrackingAttack(attackId);
                return;
            }

            _trackedAttackElapsed += deltaTime;
        }

        private void BeginTrackingAttack(AttackMoveId attackId)
        {
            // 实例 ID 区分连续使用同一招式的两次攻击，让命中箱能够按攻击实例去重。
            _trackingAttack = true;
            _trackedAttackId = attackId.ClampOrDefault();
            _trackedAttackElapsed = 0f;
            _trackedAttackInstanceId = _nextAttackInstanceId++;
            _trackedAttackDefinition = ResolveAttackDefinition(_trackedAttackId);

            if (_nextAttackInstanceId == int.MaxValue)
            {
                _nextAttackInstanceId = 1;
            }
        }

        private void StopTrackingAttack()
        {
            _trackingAttack = false;
            _trackedAttackId = AttackMoveId.None;
            _trackedAttackElapsed = 0f;
            _trackedAttackDefinition = null;
        }

        public void CancelCurrentAttack()
        {
            _suppressCurrentAttackUntilStateExit = true;
            StopTrackingAttack();
        }

        public bool TryEnterParriedReaction()
        {
            if (_playerController != null)
                return _playerController.TryEnterParried();

            if (_npcDriver == null)
                return false;

            return _npcDriver.ServerTryEnterParried();
        }

        /// <summary>
        /// 按本地 Player、服务器 NPC、远端快照三种所有权来源读取同一招式语义，让命中箱不依赖具体驱动类型。
        /// </summary>
        private bool TryReadActiveAttackId(out AttackMoveId attackId)
        {
            if (_playerController != null
            && _playerController.CurrentStateId == CharacterStateId.Attack
            && _playerController.TryGetActiveAttackState(out var playerAttack))
            {
                attackId = playerAttack.CurrentAttackId;
                return true;
            }

            if (_npcDriver != null
            && _npcDriver.CurrentStateId == CharacterStateId.Attack
            && _npcDriver.TryGetActiveAttackState(out var npcAttack))
            {
                attackId = npcAttack.CurrentAttackId;
                return true;
            }

            if (_remoteInterpolator != null)
            {
                StateSnapshot snapshot = _remoteInterpolator.LastAppliedSnapshot;
                if (snapshot.Tick > 0 && snapshot.StateId == CharacterStateId.Attack)
                {
                    attackId = AttackMoveIdExtensions.FromByte(snapshot.GetAttackComboStepOrDefault());
                    return true;
                }
            }

            attackId = AttackMoveId.None;
            return false;
        }

        // ============ 格挡判定 ============

        private bool TryResolveGuard(in HitInfo hit, out float damageMultiplier)
        {
            damageMultiplier = 1f;

            CharacterCombatConfig config = ResolveCombatConfig();
            if (config == null) return false;

            if (!IsGuardLoopActive()) return false;

            if (hit.isHeavyHit && !config.guardCanBlockHeavy)
                return false;

            if (!IsHitInsideGuardArc(hit, config.guardBlockAngle))
                return false;

            damageMultiplier = hit.isHeavyHit ? config.guardHeavyDamageMultiplier : config.guardDamageMultiplier;

            damageMultiplier = Mathf.Clamp01(damageMultiplier);

            return true;
        }

        /// <summary>
        /// 破防仍复用正常格挡的阶段与方向约束，重击不会从角色背后错误触发正面破防表现。
        /// </summary>
        private bool TryResolveGuardBreak(in HitInfo hit, out float damageMultiplier)
        {
            damageMultiplier = 1f;

            CharacterCombatConfig config = ResolveCombatConfig();
            if (config == null) return false;

            if (!config.guardBreakOnHeavyHit || !hit.isHeavyHit)
                return false;

            if (!IsGuardLoopActive())
                return false;

            if (!IsHitInsideGuardArc(hit, config.guardBlockAngle))
                return false;

            damageMultiplier = Mathf.Clamp01(config.guardBreakDamageMultiplier);
            return true;
        }

        /// <summary>
        /// 本地状态和远端快照共用相同的可格挡阶段集合，避免 Host 与纯 Client 对同一命中得出不同结果。
        /// </summary>
        private bool IsGuardLoopActive()
        {
            if (_playerController != null && _playerController.TryGetActiveGuardState(out var playerGuard))
            {
                return IsBlockingGuardPhase(playerGuard.CurrentPhase);
            }

            if (_npcDriver != null && _npcDriver.TryGetActiveGuardState(out var npcGuard))
            {
                return IsBlockingGuardPhase(npcGuard.CurrentPhase);
            }

            if (_remoteInterpolator != null)
            {
                StateSnapshot snapshot = _remoteInterpolator.LastAppliedSnapshot;
                return snapshot.Tick > 0
                    && snapshot.StateId == CharacterStateId.Guard
                    && IsBlockingGuardPhase(snapshot.GetGuardPhaseOrDefault());
            }


            return false;
        }

        private static bool IsBlockingGuardPhase(GuardState.GuardPhase phase)
        {
            return phase is GuardState.GuardPhase.Loop
                or GuardState.GuardPhase.TurnLeft
                or GuardState.GuardPhase.TurnRight;
        }

        /// <summary>
        /// 优先使用命中携带的方向，缺失时才回退到攻击者位置，使网络重放不依赖双方稍后变化的 Transform。
        /// </summary>
        private bool IsHitInsideGuardArc(in HitInfo hit, float guardBlockAngle)
        {
            Vector3 incoming = -hit.hitDirection;
            incoming.y = 0f;
            if (incoming.sqrMagnitude <= 0.0001f && hit.attacker != null)
            {
                incoming = hit.attacker.transform.position - transform.position;
                incoming.y = 0f;
            }
            if (incoming.sqrMagnitude <= 0.0001f)
                return true;
            incoming.Normalize();
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                return false;
            forward.Normalize();
            float halfAngle = Mathf.Clamp(guardBlockAngle, 0f, 360f) * 0.5f;
            float threshold = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            float dot = Vector3.Dot(forward, incoming);
            return dot >= threshold;
        }

        // ============ 状态反应路由 ============

        private bool TryEnterHitReaction(bool isHeavyHit, byte hitVariant)
        {
            if (_playerController != null)
                return _playerController.TryEnterHitReaction(isHeavyHit, hitVariant);

            if (_npcDriver != null)
                return _npcDriver.ServerTryEnterHit(isHeavyHit, hitVariant);

            return false;
        }

        private bool TryEnterDeadState()
        {
            if (_playerController != null)
                return _playerController.TryEnterDead();

            if (_npcDriver != null)
                return _npcDriver.ServerTryEnterDead();

            return false;
        }

        private bool TryEnterPostureBrokenState()
        {
            if (_playerController != null)
                return _playerController.TryEnterPostureBroken();

            if (_npcDriver != null)
                return _npcDriver.ServerTryEnterPostureBroken();

            return false;
        }

        private static GuardReactionType SelectGuardReaction(AttackMoveId attackId, bool isHeavyHit)
        {
            if (isHeavyHit) return GuardReactionType.Hit3;

            return attackId switch
            {
                AttackMoveId.Combo1 or AttackMoveId.Combo2 => GuardReactionType.Hit1,
                AttackMoveId.Combo3 or AttackMoveId.Combo4 => GuardReactionType.Hit2,
                AttackMoveId.Sprint or AttackMoveId.Dodge => GuardReactionType.Hit2,
                AttackMoveId.Heavy1Start or AttackMoveId.Heavy1 or AttackMoveId.Heavy2 => GuardReactionType.Hit3,
                _ => GuardReactionType.Hit1,
            };
        }

        // ============ 攻击数据解析 ============

        /// <summary>
        /// 配置缺失时仍生成稳定的招式定义，使编辑器临时场景和不完整数据不会改变命中窗口的确定性。
        /// </summary>
        private AttackDefinition ResolveAttackDefinition(AttackMoveId attackId)
        {
            attackId = attackId.ClampOrDefault();
            CharacterCombatConfig config = ResolveCombatConfig();
            if (config != null && config.TryGetAttackDefinition(attackId, out AttackDefinition definition))
                return definition;

            if (!_fallbackDefinitions.TryGetValue(attackId, out var fallback))
            {
                fallback = AttackDefinition.CreateFallback(attackId, ResolveFallbackDuration(attackId));
                _fallbackDefinitions.Add(attackId, fallback);
            }

            fallback.duration = ResolveFallbackDuration(attackId);
            return fallback;
        }

        private float ResolveFallbackDuration(AttackMoveId attackId)
        {
            CharacterCombatConfig config = ResolveCombatConfig();
            return config != null ? config.GetAttackDuration(attackId) : 0.8f;
        }

        // ============ 权威与状态查询 ============

        /// <summary>
        /// Player 的普通动作读取所属端快照；Server 强制反应和处决状态
        /// 由本地状态机优先，避免旧快照覆盖权威状态。
        /// </summary>
        private CharacterStateId ResolveCurrentStateId()
        {
            if (_npcDriver != null)
                return _npcDriver.CurrentStateId;

            CharacterStateId controllerState =
                _playerController != null
                    ? _playerController.CurrentStateId
                    : CharacterStateId.None;

            // Server 强制进入的非 locomotion 状态优先于远端快照。
            if (_playerController != null &&
                controllerState is not (
                    CharacterStateId.None or
                    CharacterStateId.Idle or
                    CharacterStateId.Move))
            {
                return controllerState;
            }

            if (TryGetRemoteSnapshotState(out CharacterStateId remoteState))
                return remoteState;

            return controllerState;
        }

        private bool TryGetRemoteSnapshotState(
            out CharacterStateId stateId)
        {
            stateId = CharacterStateId.None;

            if (_remoteInterpolator == null)
                return false;

            // 所属 Player 始终以自己的状态机为准。
            if (_networkIdentity != null &&
                _networkIdentity.isLocalPlayer)
            {
                return false;
            }

            StateSnapshot snapshot =
                _remoteInterpolator.LastAppliedSnapshot;

            if (snapshot.Tick == 0)
                return false;

            stateId = snapshot.StateId;
            return true;
        }

        /// <summary>
        /// 在线模式只允许服务器推进架势，离线模式则由本地实例接管，避免客户端预测与权威恢复同时写入。
        /// </summary>
        private bool HasPostureSimulationAuthority()
        {
            if (NetworkServer.active)
                return _networkIdentity == null || _networkIdentity.isServer;

            return !NetworkClient.active;
        }

        public ParryPhase GetCurrentParryPhase()
        {
            if (_npcDriver != null)
            {
                return _npcDriver.TryGetActiveParryState(out NpcParryState npcParry)
                    ? npcParry.CurrentPhase
                    : ParryPhase.None;
            }

            bool isRemotePlayerOnServer =
                _playerController != null &&
                NetworkServer.active &&
                _networkIdentity != null &&
                !_networkIdentity.isLocalPlayer;

            if (_playerController != null && !isRemotePlayerOnServer)
            {
                return _playerController.TryGetActiveParryState(out ParryState playerParry)
                    ? playerParry.CurrentPhase
                    : ParryPhase.None;
            }

            if (_remoteInterpolator != null)
            {
                StateSnapshot snapshot = _remoteInterpolator.LastAppliedSnapshot;
                if (snapshot.Tick > 0 && snapshot.StateId == CharacterStateId.Parry)
                    return snapshot.GetParryPhaseOrDefault();
            }

            return ParryPhase.None;
        }

        private bool IsPostureBroken()
        {
            if (_playerController != null)
            {
                return _playerController.CurrentStateId ==
                    CharacterStateId.PostureBroken;
            }

            if (_npcDriver != null)
            {
                return _npcDriver.CurrentStateId ==
                    CharacterStateId.PostureBroken;
            }

            return false;
        }

        private CharacterCombatConfig ResolveCombatConfig()
        {
            if (_combatConfig != null) return _combatConfig;

            if (GameDataManager.Instance == null) return null;

            if (_npcDriver != null)
            {
                return GameDataManager.Instance.Npc != null ? GameDataManager.Instance.Npc.combat : null;
            }
            return GameDataManager.Instance.Player != null ? GameDataManager.Instance.Player.combat : null;
        }

        // ============ 架势模拟 ============

        private void InitializePosture(bool forceNotify)
        {
            CharacterCombatConfig config = ResolveCombatConfig();
            float maxPosture =
                config != null ? config.maxPosture : 100f;

            if (_posture == null)
            {
                _posture = new PostureRuntime(maxPosture);
                _posture.Changed += OnPostureRuntimeChanged;

                if (forceNotify)
                    OnPostureRuntimeChanged(_posture.Current, _posture.Max);

                return;
            }

            bool changed = _posture.ConfigureMax(maxPosture);
            if (forceNotify && !changed)
                OnPostureRuntimeChanged(_posture.Current, _posture.Max);
        }

        /// <summary>
        /// 客户端只消费权威架势；死亡边沿仅重置一次，避免每帧重复广播相同 UI 事件。
        /// </summary>
        private void TickPosture(float deltaTime)
        {
            if (!HasPostureSimulationAuthority())
                return;

            InitializePosture(forceNotify: false);

            bool isDead = IsDead;
            if (isDead)
            {
                if (!_wasDead)
                    _posture.Reset(forceNotify: true);

                _wasDead = true;
                return;
            }

            if (_wasDead)
            {
                _posture.Reset(forceNotify: true);
                _wasDead = false;
            }

            if (IsPostureBroken())
                return;

            CharacterCombatConfig config = ResolveCombatConfig();
            float lowHealthRate = config != null
                ? config.postureRecoveryPerSecondAtLowHealth
                : 5f;
            float fullHealthRate = config != null
                ? config.postureRecoveryPerSecondAtFullHealth
                : 15f;

            _posture.TickRecovery(
                deltaTime,
                HealthRatio,
                lowHealthRate,
                fullHealthRate);
        }


        private void OnPostureRuntimeChanged(
                   float currentPosture,
                   float maxPosture)
        {
            PostureChanged?.Invoke(currentPosture, maxPosture);
        }

        // ============ 本地生命存储 ============

        private void InitializeHealth(bool force)
        {
            if (_healthInitialized && !force) return;

            CharacterCombatConfig config = ResolveCombatConfig();
            _maxHp = Mathf.Max(1f, config != null ? config.maxHp : _maxHp);
            _currentHp = _maxHp;
            _healthInitialized = true;
            HealthChanged?.Invoke(_currentHp, _maxHp);
        }

        private bool ApplyHealthDamage(float damage)
        {
            InitializeHealth(force: false);

            if (_currentHp <= 0f) return false;

            float previousHp = _currentHp;
            _currentHp = Mathf.Max(0f, _currentHp - Mathf.Max(0f, damage));
            float appliedDamage = previousHp - _currentHp;

            if (appliedDamage <= 0f) return false;

            DamageTaken?.Invoke(appliedDamage);
            HealthChanged?.Invoke(_currentHp, _maxHp);
            return true;
        }

        // ============ 引用维护 ============

        private void EnsureReferences()
        {
            if (_playerController == null)
            {
                _playerController = GetComponent<PlayerController>();
            }

            if (_npcDriver == null)
            {
                _npcDriver = GetComponent<NpcCharacterDriver>();
            }

            if (_remoteActionApplier == null)
            {
                _remoteActionApplier = GetComponent<RemoteActionApplier>();
            }

            if (_remoteInterpolator == null)
            {
                _remoteInterpolator = GetComponent<RemoteInterpolator>();
            }

            if (_networkIdentity == null)
            {
                _networkIdentity = GetComponent<NetworkIdentity>();
            }
        }


        // ============== 状态路由 ============

        public bool CanEnterExecutionAsExecutor()
        {
            EnsureReferences();

            return _playerController != null &&
                   _npcDriver == null &&
                   _playerController.CanEnterExecuting();
        }

        public bool CanEnterExecutionAsTarget()
        {
            EnsureReferences();

            if (_playerController != null)
                return _playerController.CanEnterExecuted();

            return _npcDriver != null &&
                   _npcDriver.CanEnterExecuted();
        }

        public bool TryEnterExecuting(
            in ExecutionSession session)
        {
            EnsureReferences();

            return _playerController != null &&
                   _npcDriver == null &&
                   _playerController.TryEnterExecuting(session);
        }

        public bool TryEnterExecuted(
            in ExecutionSession session)
        {
            EnsureReferences();

            if (_playerController != null)
                return _playerController.TryEnterExecuted(session);

            return _npcDriver != null &&
                   _npcDriver.ServerTryEnterExecuted(session);
        }

        public bool TryRollbackExecutionStart(
            ulong executionId,
            CharacterStateId previousState)
        {
            EnsureReferences();

            if (_playerController != null)
            {
                return _playerController.TryRollbackExecutionStart(
                    executionId,
                    previousState);
            }

            return _npcDriver != null &&
                   _npcDriver.ServerTryRollbackExecutionStart(
                       executionId,
                       previousState);
        }

        public bool HasExecutionDurationElapsedAsExecutor(
    ulong executionId)
        {
            EnsureReferences();

            return _playerController != null &&
                   _npcDriver == null &&
                   _playerController.TryGetActiveExecutingState(
                       out ExecutingState state) &&
                   state.IsBoundTo(executionId) &&
                   state.HasReachedDuration;
        }

        public bool HasExecutionDurationElapsedAsTarget(
            ulong executionId)
        {
            EnsureReferences();

            if (_playerController != null)
            {
                return _playerController.TryGetActiveExecutedState(
                           out ExecutedState state) &&
                       state.IsBoundTo(executionId) &&
                       state.HasReachedDuration;
            }

            return _npcDriver != null &&
                   _npcDriver.TryGetActiveExecutedState(
                       out NpcExecutedState npcState) &&
                   npcState.IsBoundTo(executionId) &&
                   npcState.HasReachedDuration;
        }

        public bool TryCompleteExecutionAsExecutor(
            ulong executionId)
        {
            EnsureReferences();

            return _playerController != null &&
                   _npcDriver == null &&
                   _playerController.TryCompleteExecuting(
                       executionId);
        }

        public bool TryCompleteExecutionAsTarget(
            ulong executionId)
        {
            EnsureReferences();

            if (_playerController != null)
            {
                return _playerController.TryCompleteExecuted(
                    executionId);
            }

            return _npcDriver != null &&
                   _npcDriver.ServerTryCompleteExecuted(
                       executionId);
        }

        private bool IsBoundToExecutedSession(
            ulong executionId)
        {
            if (_playerController != null)
            {
                return _playerController.TryGetActiveExecutedState(
                           out ExecutedState state) &&
                       state.IsBoundTo(executionId);
            }

            return _npcDriver != null &&
                   _npcDriver.TryGetActiveExecutedState(
                       out NpcExecutedState npcState) &&
                   npcState.IsBoundTo(executionId);
        }
    }
}
