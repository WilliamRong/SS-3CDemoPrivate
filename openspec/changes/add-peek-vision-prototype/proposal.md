## Why

项目需要一个可直接运行和观察的门缝窥视原型，用于验证隔壁房间模糊、锥形区域清晰以及角色按像素显隐的表现是否适合后续玩法开发。现有离线和联机场景耦合了网络、战斗与 AI 系统，不适合承担这个独立视觉实验。

## What Changes

- 新建 `PeekTest` 场景，用 Cube 搭建两个相邻房间、共享墙体和一扇可开合的门。
- 从现有 `Player` 与 `NPC` Prefab 复制并精简出 `PeekPlayer` 与 `Zombie`，仅保留模型、动画、碰撞和原型所需的移动能力。
- 为 `PeekPlayer` 增加横板移动和固定朝向的 Cinemachine 跟随镜头，并配置较大的软区。
- 玩家靠近门时让门平滑打开至 20 度，并从玩家头部朝隔壁房间启用锥形窥视区域。
- 隔壁房间在窥视区域外显示模糊画面，在锥形区域内显示清晰画面；`Zombie` 仅在锥形区域覆盖到的像素中可见。
- `Zombie` 在隔壁房间内往返移动，到达边界后转身继续巡逻。
- 增加原型范围的运行时验证和编辑器测试，不接入现有网络、战斗、锁定或行为树流程。

## Capabilities

### New Capabilities

- `peek-vision-prototype`: 定义独立窥视测试场景、精简角色移动、门触发、侧视镜头、房间模糊与锥形逐像素显隐行为。

### Modified Capabilities

无。

## Impact

- 新增 `Assets/PeekTest/` 下的脚本、Shader、材质、Prefab 和测试资源，以及 `Assets/Scenes/PeekTest.unity`。
- 为当前 URP RendererData 注册一个默认不产生效果、仅在 `PeekVisionController` 存在时执行的 Renderer Feature。
- 依赖项目已安装的 URP 14、Cinemachine 2.10.7 和 Input System，不增加第三方包。
- 不修改原始 `Assets/Prefabs/Characters/Player.prefab`、`Assets/Prefabs/Characters/NPC.prefab` 及现有玩法场景。
