using Character.Config;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Central registry of boot-time data assets. Assigned once on <see cref="GameDataManager"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "GameDataCatalog", menuName = "SS3C/Game Data Catalog")]
    public sealed class GameDataCatalog : ScriptableObject
    {
        [Header("Characters")]
        public CharacterDefinition player;
        public CharacterDefinition npc;

        [Header("Systems")]
        public NetworkSyncConfig networkSync;
    }
}
