using Character.Config;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 把启动期数据引用集中成单一资产，避免场景对象各自持有一套可能不一致的配置。
    /// </summary>
    [CreateAssetMenu(fileName = "GameDataCatalog", menuName = "SS3C/Game Data Catalog")]
    public sealed class GameDataCatalog : ScriptableObject
    {
        [Header("Characters")]
        public CharacterDefinition player;
        public CharacterDefinition npc;

        [Header("Systems")]
        public NetworkSyncConfig networkSync;

        [Header("Camera")]
        public PlayerCameraRigConfig playerCameraRig;
    }
}
