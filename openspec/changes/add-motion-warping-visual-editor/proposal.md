## Why

当前处决 Motion Warping 的关键配置只能通过资产字段和运行时日志间接调参，开发者很难直观看到原始 Root Motion、修正轨迹、锚点误差、结果时刻和残差之间的关系。2026-09-17 Offline 验收已经暴露 `executionResultTime=0.01` 与处决者残差约 `1.5414m` 的校准问题，因此需要一个 Editor-only 可视化工具来把 MW 动作数据调参从“猜数值”变成“看轨迹、改配置、再验证”。

## What Changes

- 新增 Motion Warping 可视化编辑器，用于编辑和诊断处决等需要 MW 的动作数据。
- 编辑器支持选择正式 `CharacterCombatConfig`、`CharacterPresentationConfig`、预览角色/Avatar，以及 `rig_Execute`、`rig_Executed`、`rig_Executed_Death` 等动画资源。
- 编辑器可视化原始 Root Motion 轨迹、Warp 后预测轨迹、目标固定姿态、处决者锚点、位置/Yaw 残差、Warp Window、结果时刻和预算超限诊断。
- 编辑器允许通过 Inspector 字段、时间轴和 Scene View Handle 编辑 `executorAnchorOffset`、Warp Window、`executionWarpCurve`、位置/Yaw 预算、玩法时长、CrossFade 和 clip 校准字段。
- 编辑器使用 `PlayableGraph` 或等价 Editor 采样路径生成动画 Root Motion 采样，并与运行时共享同一套纯 MW 计算语义，避免编辑器预览与游戏内结果漂移。
- 编辑器通过 `SerializedObject` / `SerializedProperty` 写回正式配置，支持 Unity Undo/Redo、Dirty/Save 和重新打开后的资产持久化。
- 编辑器预览对象必须运行在隔离 `PreviewScene` 或等价临时环境中，不要求开发者手动打开测试场景，也不得污染当前 Scene、Prefab 或正式资产。
- 保留真实 Offline/Online 场景作为最终运行时对照验收入口，但编辑器本体不能依赖 Play Mode 或指定测试场景。

## Capabilities

### New Capabilities

- `motion-warping-editor`: 定义 Editor-only Motion Warping 可视化编辑器的选择、采样、轨迹预览、配置写回、诊断、隔离预览和运行时一致性要求。

### Modified Capabilities

无。编辑器会服务于处决系统配置校准，但不改变运行时资格、会话、权威位移或致死判定需求。

## Impact

- 影响 Editor 代码：新增 `EditorWindow`、PreviewScene/PlayableGraph 采样器、Scene View Handles、时间轴 UI、诊断面板和 EditMode 测试。
- 影响运行时代码边界：需要抽取或封装当前 `ExecutionWarpContext` 中可共享的纯 Warp 采样/轨迹计算核心，让 Editor 与 Runtime 使用同一语义；不得增加 Player 构建中的 Editor 依赖。
- 影响配置资产：通过 Unity 序列化路径编辑 `CharacterCombatConfig` 与 `CharacterPresentationConfig`，不手工改 `.asset` YAML。
- 不新增第三方包依赖；不要求 Animation Rigging 或 IK；第一版不承诺解决手部/武器接触精度，只解决根运动轨迹、锚点和残差校准。
