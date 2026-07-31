using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Character.Combat
{
    /// <summary>
    /// 用序列化数组提供 Inspector 编辑，再以运行时字典查询，兼顾资产可编辑性和逐帧查找成本。
    /// </summary>
    [CreateAssetMenu(fileName = "AttackDefinitionSet", menuName = "SS3C/Combat/Attack Definition Set")]
    public sealed class AttackDefinitionSet : ScriptableObject
    {
        /// <summary>
        /// 保留显式键值包装，确保攻击定义可以由 Unity 序列化，同时不把运行时字典暴露给资产格式。
        /// </summary>
        [Serializable]
        public sealed class Entry
        {
            [FormerlySerializedAs("attackId")]
            public AttackMoveId step = AttackMoveId.Combo1;
            public AttackDefinition value;
        }

        [FormerlySerializedAs("attacks")]
        public Entry[] entries;

        private readonly Dictionary<AttackMoveId, AttackDefinition> _lookup = new();
        private bool _lookupDirty = true;

        // ============ Unity 生命周期 ============

        private void OnEnable()
        {
            _lookupDirty = true;
        }

        private void OnValidate()
        {
            _lookupDirty = true;
        }

        // ============ 攻击查询 ============

        public bool TryGet(AttackMoveId attackId, out AttackDefinition definition)
        {
            RebuildLookupIfNeeded();
            return _lookup.TryGetValue(attackId.ClampOrDefault(), out definition);
        }

        // ============ 查询缓存 ============

        private void RebuildLookupIfNeeded()
        {
            if (!_lookupDirty) return;

            _lookup.Clear();
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    Entry entry = entries[i];
                    if (entry == null || entry.value == null) continue;

                    AttackMoveId step = entry.step.ClampOrDefault();
                    _lookup[step] = entry.value;
                }
            }

            _lookupDirty = false;
        }
    }
}
