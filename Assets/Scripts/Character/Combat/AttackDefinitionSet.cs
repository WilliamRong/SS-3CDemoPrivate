using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Character.Combat
{
    [CreateAssetMenu(fileName = "AttackDefinitionSet", menuName = "SS3C/Combat/Attack Definition Set")]
    public sealed class AttackDefinitionSet : ScriptableObject
    {
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

        public bool TryGet(AttackMoveId attackId, out AttackDefinition definition)
        {
            RebuildLookupIfNeeded();
            return _lookup.TryGetValue(attackId.ClampOrDefault(), out definition);
        }

        private void OnValidate()
        {
            _lookupDirty = true;
        }

        private void OnEnable()
        {
            _lookupDirty = true;
        }

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
