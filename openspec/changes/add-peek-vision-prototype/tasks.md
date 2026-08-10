## 1. 原型运行时代码

- [x] 1.1 建立 `Assets/PeekTest/` 目录与运行时/测试程序集定义。
- [x] 1.2 实现 `PeekVisionMath`、`PeekVisionController` 和世界空间锥形参数发布。
- [x] 1.3 实现 `PeekPlayerMovement`、`ZombiePatrol` 与基础 Animator 驱动。
- [x] 1.4 实现 `PeekDoorController` 的距离检测、20 度平滑开合和窥视状态切换。

## 2. 窥视渲染

- [x] 2.1 实现 URP `PeekVisionRendererFeature` 的颜色复制、Kawase Blur 和房间范围合成。
- [x] 2.2 实现 `PeekVisionComposite.shader` 的深度世界坐标重建、房间包围盒和柔边锥形混合。
- [x] 2.3 实现 `PeekZombie.shader` 的基础贴图显示与锥形逐像素裁剪。
- [x] 2.4 将 `PeekVisionRendererFeature` 注册到 `URP-Balanced-Renderer`，并验证普通相机无效果。

## 3. Prefab 与场景

- [x] 3.1 通过 Unity Prefab API 创建并核验精简的 `PeekPlayer.prefab` 与 `Zombie.prefab`。
- [x] 3.2 创建原型材质并为 Zombie 渲染器应用专用 Shader。
- [x] 3.3 通过 Unity Scene API 创建 `PeekTest` 的两个 Cube 房间、共享墙体、门、装饰物和灯光。
- [x] 3.4 在 `PeekTest` 中实例化角色，配置巡逻边界、门引用、头部锥形和 Cinemachine 正交软区镜头。

## 4. 验证

- [x] 4.1 添加 EditMode 测试覆盖锥形内外、未激活状态和巡逻边界方向计算。
- [x] 4.2 退出 Play Mode、重新编译并检查 Unity compilation/Console 错误，运行 EditMode 测试。
- [x] 4.3 在 Play Mode 验证玩家移动、相机软区、门 20 度开合、Zombie 转向和未窥视时完全隐藏。
- [x] 4.4 捕获未窥视、完整窥视和仅显示 Zombie 部分身体的 Game View 截图并检查画面重叠或空白。
- [x] 4.5 运行 `git diff --check` 与 `openspec validate add-peek-vision-prototype --strict`，确认原 Prefab 和现有场景未被修改。
