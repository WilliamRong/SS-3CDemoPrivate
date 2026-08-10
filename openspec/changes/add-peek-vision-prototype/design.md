## 上下文

项目使用 Unity 2022.3、URP 14、Cinemachine 2.10.7 和 Input System。当前 `Player` 与 `NPC` Prefab 都包含网络、战斗、同步、锁定和 AI 组件，直接复用会让原型依赖现有游戏生命周期。当前质量档使用 `Assets/Settings/URP-Balanced-Renderer.asset`，其中只有 SSAO Renderer Feature，深度纹理和不透明纹理未全局开启。

原型需要在侧视镜头中明确展示三种视觉状态：隔壁房间默认模糊、锥形覆盖的房间像素清晰、Zombie 仅显示锥形覆盖的身体像素。场景和角色资产必须与现有玩法隔离。

## 目标与非目标

**目标：**

- 提供可在 Unity Editor 中直接进入 Play Mode 的 `PeekTest` 场景。
- 使用源 Prefab 的模型和 Animator 创建精简的 `PeekPlayer` 与 `Zombie` 独立 Prefab。
- 用固定朝向的正交侧视镜头、较大 Cinemachine Soft Zone 和水平移动形成横板观察方式。
- 自动驱动门的 20 度开合、窥视锥形和 Zombie 往返巡逻。
- 使用同一组世界空间锥形参数驱动房间清晰区域与 Zombie 逐像素显隐。
- 让新增渲染逻辑在非 `PeekTest` 相机上零效果。

**非目标：**

- 不接入 Mirror、现有战斗、锁定、生命、行为树或 NavMesh 流程。
- 不实现正式关卡的任意房型可见多边形、门缝物理光线追踪或多扇门管理。
- 不保证该原型 Shader 是最终美术质量或移动平台性能方案。
- 不修改源 `Player`、`NPC` Prefab 及现有场景行为。

## 技术决策

### 使用独立原型目录和复制后的 Prefab

新增运行时代码、Shader、材质、Prefab 和测试放在 `Assets/PeekTest/`，场景放在 `Assets/Scenes/PeekTest.unity`。通过 Unity Prefab API 复制源 Prefab，再递归移除网络、战斗、锁定、同步、AI、Collider 和 Rigidbody 等非视觉组件，仅保留 Transform、Renderer、MeshFilter、Animator，并补充原型移动组件与 CharacterController。

该方案保留已有模型、骨骼和动画资源，同时避免在原 Prefab 上产生 override 或生命周期副作用。替代方案是在场景实例中禁用多数组件，但被禁用组件仍会留下序列化依赖，也更容易在后续 Prefab 更新中失效。

### 使用 Cinemachine FramingTransposer 实现侧视跟随

主 Camera 保持正交投影和固定朝向，`CinemachineVirtualCamera` 跟随 `PeekPlayer/CameraTarget`。Body 使用 `CinemachineFramingTransposer`，设置较大的 `m_SoftZoneWidth` 与 `m_SoftZoneHeight`，保留较小 Dead Zone，使玩家在软区内移动时镜头稳定、接近边缘后再平滑跟随。

### 由门控制器统一驱动开门和窥视状态

`PeekDoorController` 每帧计算玩家与门的平面距离。进入阈值时，门从关闭姿态平滑旋转到相对 Y 轴 20 度并调用 `PeekVisionController.SetPeeking(true)`；离开阈值时反向关闭并禁用清晰锥形。锥形起点取 Humanoid Head 骨骼，方向指向隔壁房间中的 `PeekTarget`。

### 使用世界空间锥形和房间包围盒作为统一遮罩

`PeekVisionController` 每帧发布 `_PeekVisionOrigin`、`_PeekVisionDirection`、距离、内外角余弦、激活状态及 `_PeekRoomMin/_PeekRoomMax`。房间合成 Shader 通过深度重建世界坐标，Zombie Shader 直接使用片元世界坐标；两者使用相同的角度与距离计算。内外角之间用 `smoothstep` 形成柔和边缘。

### 不透明房间先合成，Zombie 后绘制

`PeekVisionRendererFeature` 在 `AfterRenderingOpaques` 请求 Depth、复制 Camera Color、进行低分辨率多次 Kawase Blur，再仅对隔壁房间包围盒内的像素执行 `lerp(blurred, clear, coneMask)`。Feature 在当前相机没有启用的 `PeekVisionController` 时直接跳过。

Zombie 使用透明 Render Queue 的专用 URP Shader，在房间合成之后绘制，并在片元阶段根据同一锥形遮罩裁剪或渐隐。这样 Zombie 不会进入模糊底图，锥形外不会留下模糊轮廓，锥形扫过模型时可以只看到脚或下半身。

### 共享 RendererData 上注册但按相机隔离

Renderer Feature 注册到当前 `URP-Balanced-Renderer`，但执行前必须检查相机上的 `PeekVisionController`。这避免复制整套 URP Pipeline Asset，也不会改变现有场景的渲染结果。Feature 自行请求 Depth 和分配临时 RTHandle，不全局开启 Opaque Texture。

## 风险与权衡

- [世界坐标由深度重建时，天空或没有几何覆盖的区域无法归入房间] → `PeekTest` 的隔壁房间使用完整后墙、地板和边界几何覆盖可视区域。
- [共享 RendererData 增加了一个全局 Feature] → Feature 对没有 `PeekVisionController` 的相机立即返回，并在验证中回归检查 `Offline` 场景。
- [透明队列 Zombie 的排序和光照不等同正式角色材质] → 原型仅放置一个 Zombie，专用 Shader 保留基础贴图并使用简化光照；正式方案可改为自定义 Render Pass。
- [低分辨率模糊可能在房间边界产生少量颜色渗漏] → 合成阶段再次用世界空间房间包围盒限制影响范围，并保留可调模糊半径。
- [源 Animator 的移动树依赖固定状态名] → 原型使用已核验的 `Idle`、`Locomotion`、`VelocityX` 和 `VelocityZ`，并增加编辑器测试覆盖锥形数学与状态边界。

## 迁移与回滚

实现不迁移现有数据。回滚时删除 `Assets/PeekTest/`、`Assets/Scenes/PeekTest.unity`，并从 `URP-Balanced-Renderer` 移除 `PeekVisionRendererFeature` 子资产即可；源 Prefab 和现有场景无需恢复。

## 开放问题

当前原型默认玩家使用 `A/D`、方向键或手柄左摇杆水平移动，靠近门自动窥视并在离开时关闭。正式玩法是否需要按住交互键、视野被动态障碍物截断或同时支持多扇门，留待原型评审后决定。
