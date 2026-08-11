using System.Collections.Generic;

namespace Character.Combat
{
    /// <summary>
    /// Tracks execution-owned suppression of ordinary combat hits.
    /// </summary>
    public sealed class AttackSuppressionRuntime
    {
        private readonly HashSet<ulong> _owners = new();

        public bool IsActive => _owners.Count > 0;
        public int ActiveOwnerCount => _owners.Count;

        public bool Acquire(ulong executionId)
        {
            if (executionId == 0)
                return false;

            return _owners.Add(executionId);
        }

        public bool Release(ulong executionId)
        {
            if (executionId == 0)
                return false;

            return _owners.Remove(executionId);
        }

        public void Clear()
        {
            _owners.Clear();
        }
    }
}