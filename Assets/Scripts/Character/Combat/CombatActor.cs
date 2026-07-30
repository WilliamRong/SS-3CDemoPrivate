using System;
using System.Collections.Generic;
using AI;
using Character.Config;
using Character.Controller;
using Character.StateMachine;
using Character.StateMachine.States;
using Character.Sync;
using Core;
using Mirror;
using UnityEngine;

namespace Character.Combat
{
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
        private AttackMoveId _trackedAttackId;
        private float _trackedAttackElapsed;
        private int _trackedAttackInstanceId;
        private int _nextAttackInstanceId = 1;

        private AttackDefinition _trackedAttackDefinition;
        private float _maxHp = 100f;
        private bool _healthInitialized;

        private bool _hasAppliedHealthRevision;

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

        public bool IsInvincible => _playerController != null && _playerController.IsInvincible;

        public bool CanReceiveHit => _canReceiveHit && !IsDead && !IsInvincible;

        public bool IsDead
        {
            get
            {
                if (CurrentHp <= 0f)
                {
                    return true;
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

        private void Reset()
        {
            EnsureReferences();
            if (_npcDriver != null) _teamId = 2;
            else _teamId = 1;
        }

        private void Awake()
        {
            EnsureReferences();
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


        public bool TryGetCurrentAttack(out AttackRuntimeInfo attack)
        {
            attack = default;

            if (!_trackingAttack || _trackedAttackDefinition == null) return false;

            attack = new AttackRuntimeInfo(
                this,
                _trackedAttackDefinition,
                _trackedAttackId,
                _trackedAttackInstanceId,
                _trackedAttackElapsed);

            return attack.IsValid;
        }


        private void CommitHealthChange(float previousHp)
        {
            if (Mathf.Approximately(previousHp, CurrentHp)) return;

            unchecked
            {
                HealthRevision++;
                if (HealthRevision == 0) HealthRevision = 1;
            }
        }

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


        public float AddPosture(float amount)
        {
            if (!HasPostureSimulationAuthority())
                return 0f;

            InitializePosture(forceNotify: false);
            CharacterCombatConfig config = ResolveCombatConfig();
            float recoveryDelay = config != null ? config.postureRecoveryDelay : 2f;

            return _posture.Add(amount, recoveryDelay);
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

        public bool ApplyHit(in HitInfo hit)
        {
            LastHitResult = default;
            LastGuardReactionType = GuardReactionType.None;

            if (!HasPostureSimulationAuthority() ||
                !CanReceiveHit)
            {
                return false;
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
                // 破势期间仍扣 HP，但不增加架势，也不播放普通 Hit。
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

        private void TickAttackRuntime(float deltaTime)
        {
            if (!TryReadActiveAttackId(out AttackMoveId attackId))
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

        private float ApplyResolvedHealthDamage(float damage)
        {
            float previousHp = CurrentHp;

            if (_playerController != null)
                _playerController.ApplyHealthDamageOnly(damage);
            else
                ApplyHealthDamage(damage);

            return Mathf.Max(0f, previousHp - CurrentHp);
        }

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


        private bool HasPostureSimulationAuthority()
        {
            if (NetworkServer.active)
                return _networkIdentity == null || _networkIdentity.isServer;

            return !NetworkClient.active;
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

    }
}
