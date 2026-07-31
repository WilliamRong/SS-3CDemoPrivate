using System;
using Character.Combat;
using Mirror;
using UnityEngine;

namespace Character.LockOn
{
    /// <summary>
    /// 将角色根节点、网络身份和稳定的胸口瞄准点组合为一个目标契约，隔离不同角色骨骼层级的差异。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LockOnTarget : MonoBehaviour, ILockOnTarget
    {
        [SerializeField] private Transform _lockPoint;
        [SerializeField] private bool _useHumanoidChest = true;
        [SerializeField] private Vector3 _chestOffset = new Vector3(0f, 0.05f, 0f);
        [SerializeField] private CombatActor _combatActor;
        [SerializeField] private Transform _root;
        [SerializeField] private int _lockPriority;
        [SerializeField] private bool _canBeLocked = true;

        private Transform _resolvedLockPoint;
        private Animator _animator;
        private bool _referencesResolved;

        public Transform LockPoint => ResolveLockPoint();
        public Transform Root => _root != null ? _root : transform;
        public bool CanBeLocked => _canBeLocked && isActiveAndEnabled && !IsDead;
        public int LockPriority => _lockPriority;

        public bool IsDead
        {
            get
            {
                EnsureReferences();
                return _combatActor != null && _combatActor.IsDead;
            }
        }

        // ============ Unity 生命周期 ============

        private void Reset()
        {
            EnsureReferences();
        }

        private void Awake()
        {
            EnsureReferences();
            ResolveLockPoint();
        }

        // ============ 网络身份 ============

        public bool TryGetNetworkId(out uint netId)
        {
            netId = 0;

            Transform root = Root;
            if (root == null) return false;

            var identity = root.GetComponentInParent<NetworkIdentity>();
            if (identity == null || identity.netId == 0) return false;

            netId = identity.netId;
            return true;
        }

        // ============ 引用维护 ============

        private void EnsureReferences()
        {
            if (_referencesResolved) return;

            if (_combatActor == null)
            {
                _combatActor = GetComponentInParent<CombatActor>();
            }

            _referencesResolved = true;
        }

        // ============ 锁定点解析 ============

        /// <summary>
        /// 运行时锁定点挂在实际胸骨上，使模型缩放和动画都能自然带动瞄准点，同时保留手工配置回退。
        /// </summary>
        private Transform ResolveLockPoint()
        {
            if (!_useHumanoidChest)
            {
                return _lockPoint != null ? _lockPoint : transform;
            }

            if (_resolvedLockPoint != null)
            {
                return _resolvedLockPoint;
            }

            Transform chest = ResolveChestBone();
            if (chest == null)
            {
                return _lockPoint != null ? _lockPoint : transform;
            }

            var pointObject = new GameObject("RuntimeLockOnChestPoint");
            pointObject.hideFlags = HideFlags.DontSave;
            _resolvedLockPoint = pointObject.transform;
            _resolvedLockPoint.SetParent(chest, false);
            _resolvedLockPoint.localPosition = _chestOffset;
            _resolvedLockPoint.localRotation = Quaternion.identity;
            _resolvedLockPoint.localScale = Vector3.one;

            return _resolvedLockPoint;
        }

        /// <summary>
        /// Humanoid 骨骼优先，名称递归只作为非标准 Avatar 的兼容路径，减少对具体模型层级的依赖。
        /// </summary>
        private Transform ResolveChestBone()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_animator != null && _animator.isHuman)
            {
                Transform upperChest = _animator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (upperChest != null) return upperChest;

                Transform chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
                if (chest != null) return chest;

                Transform spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
                if (spine != null) return spine;
            }

            Transform searchRoot = _animator != null ? _animator.transform : transform;
            return FindDeepChild(searchRoot, "UpperChest")
                ?? FindDeepChild(searchRoot, "Chest")
                ?? FindDeepChild(searchRoot, "Spine2")
                ?? FindDeepChild(searchRoot, "Spine1")
                ?? FindDeepChild(searchRoot, "Spine");
        }

        /// <summary>
        /// 非 Humanoid 模型只能依赖名称回退，深度优先返回首个稳定匹配以兼容不同层级的骨架资源。
        /// </summary>
        private static Transform FindDeepChild(Transform root, string namePart)
        {
            if (root == null) return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }

                Transform found = FindDeepChild(child, namePart);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
