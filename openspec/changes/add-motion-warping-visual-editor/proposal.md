## Why

当前处决 Motion Warping 的关键配置只能通过资产字段和运行时日志间接调参，开发者很难直观看到原始 Root Motion、修正轨迹、锚点误差、结果时刻和残差之间的关系。2026-09-17 Offline 验收已经暴露 `executionResultTime=0.01` 与处决者残差约 `1.5414m` 的校准问题，因此需要一个 Editor-only 可视化工具来把 MW 动作数据调参从“猜数值”变成“看轨迹、改配置、再验证”。

## What Changes

- 新增 Motion Warping 可视化编辑器，用于编辑和诊断处决等需要 MW 的动作数据。
- 编辑器支持选择正式 `CharacterCombatConfig`、`CharacterPresentationConfig`、预览角色/Avatar，以及 `rig_Execute`、`rig_Executed`、`rig_Executed_Death` 等动画资源。
- 编辑器在窗口顶部固定显示绝对时间轴，同时采样处决者与被处决者 Root Motion，可视化双方原始轨迹、Warp 后预测轨迹、锚点、位置/Yaw 残差、Warp Window、结果时刻和预算超限诊断，并通过手动 `PlayableGraph` 按运行时分支时长同步播放双方动画。
- 编辑器允许通过 Inspector 字段、时间轴和 Scene View Handle 编辑处决者锚点、目标生还/死亡分支锚点、双方 Warp Window、曲线、位置/Yaw 预算、玩法时长、CrossFade 和 clip 校准字段。
- 编辑器使用 `PlayableGraph` 或等价 Editor 采样路径生成动画 Root Motion 采样，并与运行时共享同一套纯 MW 计算语义，避免编辑器预览与游戏内结果漂移。
- 编辑器通过 `SerializedObject` / `SerializedProperty` 写回正式配置，支持 Unity Undo/Redo、Dirty/Save 和重新打开后的资产持久化。
- 编辑器提供一键跳转专用 `MotionWarpingVisualToolPreview` 场景的入口；跳转前使用 Unity 未保存场景确认，以 `Single` 模式打开，禁止后台 Additive 加载。预览对象只在该专用场景中临时创建，不得污染其他 Scene、Prefab 或正式资产。
- 预览对象默认按正式 `executorAnchorOffset` 和共享空间判定生成面对面、近距离且可处决的初始站位，不再使用与运行时语义脱节的硬编码位置。
- 编辑器允许独立编辑处决者与被处决者的预览初始位置和旋转，并将这组站位显式保存为 Editor-only 可复用资产；站位资产不得自动改写正式 Combat 配置。
- 保留真实 Offline/Online 场景作为最终运行时对照验收入口，但编辑器本体不能依赖 Play Mode 或指定测试场景。

## Capabilities

### New Capabilities

- `motion-warping-editor`: 定义 Editor-only Motion Warping 可视化编辑器的选择、双参与者采样、轨迹预览、配置写回、诊断、隔离预览和运行时一致性要求。

### Modified Capabilities

- 处决运行时由“仅处决者 Warp、目标固定”改为“处决者与目标共享通用 Warp 核心”；目标在权威端消费自身 Root Motion 并向生还/死亡分支锚点收敛，但不改变资格、伤害提交或致死判定。

## Impact

- 影响 Editor 代码：新增 `EditorWindow`、专用预览场景导航、PlayableGraph 采样器、Scene View Handles、固定时间轴、Edit Mode 播放控制、可复用初始站位资产、诊断面板和 EditMode 测试。
- 影响运行时代码边界：将 `ExecutionWarpContext` / Solver 参数化为参与者与通用设置，处决者和目标 Player/NPC 都通过同一核心消费权威 Root Motion；状态恢复消息需要携带双方当前权威姿态，不得增加 Player 构建中的 Editor 依赖。
- 影响配置资产：通过 Unity 序列化路径编辑 `CharacterCombatConfig` 与 `CharacterPresentationConfig`，不手工改 `.asset` YAML。
- 不新增第三方包依赖；不要求 Animation Rigging 或 IK；第一版不承诺解决手部/武器接触精度，只解决根运动轨迹、锚点和残差校准。
