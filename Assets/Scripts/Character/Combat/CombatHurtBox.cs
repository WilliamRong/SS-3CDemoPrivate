using UnityEngine;

namespace Character.Combat
{
    /// <summary>
    /// 把身体部位倍率和 CombatActor 所有权绑定到触发器，Resolver 无需猜测 Collider 层级。
    /// </summary>
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
        public bool IsCombatEnabled => _hurtCollider != null && _hurtCollider.enabled;

        // ============ Unity 生命周期 ============

        private void Reset()
        {
            EnsureReferences();
        }

        private void Awake()
        {
            EnsureReferences();
        }

        public void SetCombatEnabled(bool enabled)
        {
            EnsureReferences();

            if (_hurtCollider != null)
                _hurtCollider.enabled = enabled;
        }

        // ============ 所有权查询 ============

        public bool TryGetOwner(out CombatActor owner)
        {
            EnsureReferences();
            owner = _owner;
            return owner != null;
        }

        // ============ 引用维护 ============

        private void EnsureReferences()
        {
            if (_owner == null)
                _owner = GetComponentInParent<CombatActor>();

            if (_hurtCollider == null)
                _hurtCollider = GetComponent<Collider>();

            if (_hurtCollider != null)
                _hurtCollider.isTrigger = true;
        }
    }
}
