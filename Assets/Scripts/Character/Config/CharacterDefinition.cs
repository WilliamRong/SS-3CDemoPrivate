using UnityEngine;

namespace Character.Config
{
    /// <summary>
    /// 作为角色配置聚合根，让 Player/NPC 只选择一个定义资产而不会混用不同子配置。
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
