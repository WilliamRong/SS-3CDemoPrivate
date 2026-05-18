using UnityEngine;

namespace Character.Config
{
    /// <summary>
    /// Root character data asset. References domain-specific sub-configs.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "SS3C/Character/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        public CharacterLocomotionConfig locomotion;
        public CharacterCombatConfig combat;
        public SprintPhaseConfig sprint;
        public CharacterPresentationConfig presentation;
        public NetworkSyncConfig networkSync;
    }
}
