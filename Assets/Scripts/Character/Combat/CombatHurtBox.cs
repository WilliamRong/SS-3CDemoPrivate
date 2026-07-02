using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Character.Combat
{
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class CombatHurtBox : MonoBehaviour
{
    [SerializeField] private CombatActor _owner;
    [SerializeField] private Collider _hurtCollider;
    [SerializeField] private string _partName = "Body";
    [SerializeField] private float _damageMultiplier = 1f;

    public CombatActor Owner => _owner;
    public string PartName => _partName;
    public float DamageMultiplier => _damageMultiplier <= 0f ? 1f : _damageMultiplier;

    private void Reset()
    {
        EnsureReferences();
    }

    private void Awake()
    {
        EnsureReferences();
    }

    public bool TryGetOwner(out CombatActor owner)
    {
        EnsureReferences();
        owner = _owner;
        return owner != null;
    }

    private void EnsureReferences()
    {
        if(_owner == null)
        {
            _owner = GetComponentInParent<CombatActor>();
        }

        if(_hurtCollider == null)
        {
            _hurtCollider = GetComponent<Collider>();
        }

        if(_hurtCollider != null)
        {
            _hurtCollider.isTrigger = true;
        }
    }
}
}
