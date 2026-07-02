using System;
using System.Collections.Generic;
using AI;
using Character.Config;
using Character.Controller;
using Character.StateMachine;
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

        private readonly Dictionary<AttackMoveId, AttackDefinition> _fallbackDefinitions  = new();

        private bool _trackingAttack;
        private AttackMoveId _trackedAttackId;
        private float _trackedAttackElapsed;
        private int _trackedAttackInstanceId;
        private int _nextAttackInstanceId = 1;

        private AttackDefinition _trackedAttackDefinition;
        private float _maxHp = 100f;
        private bool _healthInitialized;

        public int TeamId => _teamId;
        public float CurrentHp => _currentHp;
        public float MaxHp => _maxHp;
        public float HealthRatio => _maxHp > 0f ? Mathf.Clamp01(_currentHp / _maxHp) : 0f;

        public event Action<float, float> HealthChanged;
        public event Action<float> DamageTaken;

        public int ActorId
        {
            get
            {
                if(_networkIdentity != null && _networkIdentity.netId != 0)
                return unchecked((int)_networkIdentity.netId);

                return GetInstanceID();
            }
        }
        

        public bool CanReceiveHit => _canReceiveHit && !IsDead;

        public bool IsDead
        {
            get
            {
                if(_currentHp <= 0f)
                {
                    return true;
                }

                if(_playerController != null && _playerController.CurrentStateId == CharacterStateId.Dead)
                {
                    return true;
                }

                if(_npcDriver != null && _npcDriver.CurrentStateId == CharacterStateId.Dead)
                {
                    return true;
                }

                if(_remoteInterpolator != null
                   && _remoteInterpolator.LastAppliedSnapshot.Tick > 0)
                {
                    return _remoteInterpolator.LastAppliedSnapshot.StateId == CharacterStateId.Dead;
                }

                if(_remoteActionApplier != null
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
            if(_npcDriver != null) _teamId = 2;
            else _teamId = 1;
        }

        private void Awake()
        {
            EnsureReferences();
            InitializeHealth(force: true);
        }

        private void Start()
        {
            if (_currentHp > 0f)
                InitializeHealth(force: false);
        }

        private void Update()
        {
            TickAttackRuntime(Time.deltaTime);
        }


        public bool TryGetCurrentAttack(out AttackRuntimeInfo attack)
        {
            attack = default;

            if(!_trackingAttack || _trackedAttackDefinition == null) return false;

            attack = new AttackRuntimeInfo(
                this,
                _trackedAttackDefinition,
                _trackedAttackId,
                _trackedAttackInstanceId,
                _trackedAttackElapsed);

            return attack.IsValid;
        }

        public bool ApplyHit(in HitInfo hit)
        {
            if(!CanReceiveHit) return false;
            if(_playerController != null)
            {
                _playerController.ApplyHit(hit.damage, hit.isHeavyHit);
                return true;
            }

            if(_npcDriver != null)
            {
                if(!ApplyHealthDamage(hit.damage))
                    return false;

                if(_currentHp <= 0f)
                {
                    _npcDriver.ServerTryEnterDead();
                    return true;
                }

                _npcDriver.ServerTryEnterHit(hit.isHeavyHit);
                return true;
            }

            return ApplyHealthDamage(hit.damage);
        }

        private void TickAttackRuntime(float deltaTime)
        {
            if(!TryReadActiveAttackId(out AttackMoveId attackId))
            {
                StopTrackingAttack();
                return;
            }

            if(!_trackingAttack || _trackedAttackId != attackId)
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

            if(_nextAttackInstanceId == int.MaxValue)
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
            if(_playerController != null 
            && _playerController.CurrentStateId == CharacterStateId.Attack 
            && _playerController.TryGetActiveAttackState(out var playerAttack))
            {
                attackId = playerAttack.CurrentAttackId;
                return true;
            }

            if(_npcDriver != null 
            && _npcDriver.CurrentStateId == CharacterStateId.Attack
            && _npcDriver.TryGetActiveAttackState(out var npcAttack))
            {
                attackId = npcAttack.CurrentAttackId;
                return true;
            }

            attackId = AttackMoveId.None;
            return false;
        }

        private AttackDefinition ResolveAttackDefinition(AttackMoveId attackId)
        {
            attackId = attackId.ClampOrDefault();
            CharacterCombatConfig config = ResolveCombatConfig();
            if(config != null && config.TryGetAttackDefinition(attackId, out AttackDefinition definition))
                return definition;

            if(!_fallbackDefinitions.TryGetValue(attackId, out var fallback))
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

        private CharacterCombatConfig ResolveCombatConfig()
        {
            if(_combatConfig != null) return _combatConfig;

            if(GameDataManager.Instance == null) return null;

            if(_npcDriver != null)
            {
                return GameDataManager.Instance.Npc != null ? GameDataManager.Instance.Npc.combat : null;
            }
            return GameDataManager.Instance.Player != null ? GameDataManager.Instance.Player.combat : null;
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

            if(_currentHp <= 0f) return false;

            float previousHp = _currentHp;
            _currentHp = Mathf.Max(0f, _currentHp - Mathf.Max(0f, damage));
            float appliedDamage = previousHp - _currentHp;

            if(appliedDamage <= 0f) return false;

            DamageTaken?.Invoke(appliedDamage);
            HealthChanged?.Invoke(_currentHp, _maxHp);
            return true;
        }


        private void EnsureReferences()
        {
            if(_playerController == null)
            {
                _playerController = GetComponent<PlayerController>();
            }

            if(_npcDriver == null)
            {
                _npcDriver = GetComponent<NpcCharacterDriver>();
            }

            if(_remoteActionApplier == null)
            {
                _remoteActionApplier = GetComponent<RemoteActionApplier>();
            }

            if(_remoteInterpolator == null)
            {
                _remoteInterpolator = GetComponent<RemoteInterpolator>();
            }
            
            if(_networkIdentity == null)
            {
                _networkIdentity = GetComponent<NetworkIdentity>();
            }
        }

    }
}
