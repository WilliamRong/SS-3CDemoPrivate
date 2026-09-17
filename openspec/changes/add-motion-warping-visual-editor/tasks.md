## 1. 共享计算核心

- [ ] 1.1 梳理 `ExecutionWarpContext` 当前输入/输出，确认可共享的纯计算边界：锚点、初始姿态、配置、原始 Root Motion delta、normalized time、修正 delta 和残差。
- [ ] 1.2 抽取不依赖 `MonoBehaviour`、Editor API 或状态机的 MW 轨迹计算核心，保持运行时 `ExecutionWarpContext` 外部行为不变。
- [ ] 1.3 为共享核心增加单元测试，覆盖 Warp Window 边界、曲线累计权重、位置/Yaw 修正、预算超限和非有限值拒绝。
- [ ] 1.4 让运行时处决路径继续通过共享核心计算修正 delta，并确认现有 Offline 处决行为不回退。

## 2. Editor 工具框架

- [ ] 2.1 新增 Editor-only 目录或程序集，创建 `MotionWarpingVisualEditorWindow`，确保 Player 构建不包含编辑器代码。
- [ ] 2.2 实现配置和资源选择 UI，支持选择 `CharacterCombatConfig`、`CharacterPresentationConfig`、预览角色/Avatar、目标预览对象和处决三段动画。
- [ ] 2.3 实现资源缺失、Animator/Avatar 无效、动画缺失和配置为空的诊断展示，禁止无效资源被标记为有效预览。
- [ ] 2.4 提供默认资源快捷填充入口，优先指向项目当前 Player prefab、默认战斗/表现配置和处决动画，但允许用户覆盖。

## 3. 隔离预览与动画采样

- [ ] 3.1 使用 `PreviewScene` 或等价临时环境创建和清理处决者、目标和辅助可视化对象，不污染当前 Scene。
- [ ] 3.2 使用 `PlayableGraph` 或等价 Animator 驱动方式采样 `rig_Execute` 的 Root Motion delta、Yaw delta 和 normalized time 序列。
- [ ] 3.3 支持采样 `rig_Executed` 与 `rig_Executed_Death` 的姿态预览，用于对比目标固定姿态与死亡分支表现。
- [ ] 3.4 增加采样失败诊断，避免静默退化为不可信轨迹。
- [ ] 3.5 在窗口关闭、配置切换、域重载和异常路径中确定性清理 PreviewScene 与 PlayableGraph。

## 4. 可视化与交互编辑

- [ ] 4.1 在窗口中实现时间轴，显示 Warp Window、结果时刻、动画时长和当前 Scrub 时间。
- [ ] 4.2 在 Scene View 或预览区域绘制目标固定姿态、处决者初始姿态、锚点、原始 Root Motion 轨迹、Warp 后预测轨迹和残差线。
- [ ] 4.3 实现 Scene View 锚点 Handle，拖动时实时更新 `executorAnchorOffset` 预览并刷新轨迹。
- [ ] 4.4 实现 Warp Window、`executionWarpCurve`、位置/Yaw 预算、玩法时长、CrossFade 和 clip 校准字段的编辑 UI。
- [ ] 4.5 显示每帧和最终诊断：原始 delta、修正 delta、累计修正量、预测残差、预算占用、结果时刻异常和预览有效状态。

## 5. 配置写回与资产安全

- [ ] 5.1 通过 `SerializedObject` / `SerializedProperty` 写回 `CharacterCombatConfig` 和 `CharacterPresentationConfig`，不得手工编辑 `.asset` YAML。
- [ ] 5.2 为字段编辑、时间轴拖拽和锚点 Handle 统一接入 Unity Undo/Redo。
- [ ] 5.3 正确标记 Dirty 并支持显式保存资产，重新打开窗口后读回最终序列化值。
- [ ] 5.4 确保非法配置会显示诊断，并且不会被窗口误报为有效收敛。
- [ ] 5.5 审计所有预览对象、临时材质、Handles 和 PlayableGraph 生命周期，确认不会持久化到 Scene、Prefab 或正式资产。

## 6. 测试与验收

- [ ] 6.1 增加 EditMode 测试覆盖共享核心、配置诊断、Undo/Redo、资产持久化和 PreviewScene 清理。
- [ ] 6.2 增加编辑器预测与运行时共享核心的一致性测试，使用相同原始 Root Motion 采样输入比较修正 delta 和残差。
- [ ] 6.3 使用编辑器重新校准当前处决配置，重点复核 `executionResultTime`、Warp Window、`executorAnchorOffset` 和处决者残差。
- [ ] 6.4 在 Offline 场景运行双 Player 处决对照验收，记录编辑器预测残差与运行时实际残差。
- [ ] 6.5 通过 Unity MCP 重新编译并检查 Console/编译错误，运行相关 EditMode/PlayMode 测试、`git diff --check` 和 `openspec validate add-motion-warping-visual-editor --strict`。
- [ ] 6.6 更新验证矩阵或相关文档，明确区分 `Preview Valid`、`Config Valid` 和 `Runtime Verified`。
