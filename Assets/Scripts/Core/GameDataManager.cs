using Character.Config;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 在角色生成前发布唯一数据入口，避免运行时通过 Resources 或场景搜索得到不同配置实例。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameDataManager : MonoBehaviour
    {
        public static GameDataManager Instance { get; private set; }

        [SerializeField] private GameDataCatalog _catalog;

        public CharacterDefinition Player => _catalog.player;
        public CharacterDefinition Npc => _catalog.npc;
        public NetworkSyncConfig NetworkSync => _catalog.networkSync;
        public PlayerCameraRigConfig PlayerCameraRig => _catalog.playerCameraRig;

        // ============ Unity 生命周期 ============

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            // 只由当前实例清空，避免场景切换时旧对象销毁覆盖新对象的注册。
            if (Instance == this)
                Instance = null;
        }
    }
}
