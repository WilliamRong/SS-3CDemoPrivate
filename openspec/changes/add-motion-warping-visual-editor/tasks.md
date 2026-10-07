## 1. 共享计算核心

- [x] 1.1 梳理 `ExecutionWarpContext` 当前输入/输出，确认可共享的纯计算边界：锚点、初始姿态、配置、原始 Root Motion delta、normalized time、修正 delta 和残差。
- [x] 1.2 抽取不依赖 `MonoBehaviour`、Editor API 或状态机的 MW 轨迹计算核心，保持运行时 `ExecutionWarpContext` 外部行为不变。
- [x] 1.3 为共享核心增加单元测试，覆盖 Warp Window 边界、曲线累计权重、位置/Yaw 修正、预算超限和非有限值拒绝。
- [x] 1.4 让运行时处决路径继续通过共享核心计算修正 delta，并确认现有 Offline 处决行为不回退。
- [x] 1.5 新增通用 `ExecutionWarpSettings`、参与者角色和局部锚点解析，使 Executor/Target 共用同一 Solver/Context，并保持现有 Executor API 兼容。
  - 2026-10-07 完成：新增参与者无关的 `ExecutionWarpSettings`、`ExecutionWarpParticipant` 和目标分支锚点解析器；原 Executor `TryCreate`/轨迹采样入口保留为兼容包装，Executor/Target 最终进入同一个 Solver。
- [x] 1.6 让 Player/NPC 的 Executed 状态在权威端消费目标 Root Motion、应用 Target Warp 并回读残差；状态恢复消息携带双方当前权威姿态。
  - 2026-10-07 完成：Player/NPC 的 Executed 状态不再逐帧写回固定姿态；Player 通过 Reaction Root Motion、NPC 通过 `NpcMotor.ApplyRootMotionDelta` 消费目标 Warp，双方都会回读实际残差。`ExecutionStateMsg` 新增 Target 当前姿态，恢复进入状态后再次应用双方当前权威姿态，避免被确定性起始姿态覆盖。

## 2. Editor 工具框架

- [x] 2.1 新增 Editor-only 目录或程序集，创建 `MotionWarpingVisualEditorWindow`，确保 Player 构建不包含编辑器代码。
- [x] 2.2 实现配置和资源选择 UI，支持选择 `CharacterCombatConfig`、`CharacterPresentationConfig`、预览角色/Avatar、目标预览对象和处决三段动画。
- [x] 2.3 实现资源缺失、Animator/Avatar 无效、动画缺失和配置为空的诊断展示，禁止无效资源被标记为有效预览。
  - 2026-09-27 完成：Timeline 与逐帧诊断统一通过 `TryEvaluatePreviewState` 判定，Combat/Presentation 配置、Prefab、动画、采样来源和配置合法性任一无效都会阻断 `Preview Valid`。
- [x] 2.4 提供默认资源快捷填充入口，优先指向项目当前 Player prefab、默认战斗/表现配置和处决动画，但允许用户覆盖。

## 3. 隔离预览与动画采样

- [x] 3.1 使用 `PreviewScene` 或等价临时环境创建和清理处决者、目标和辅助可视化对象，不污染当前 Scene。
- [x] 3.2 使用 `PlayableGraph` 或等价 Animator 驱动方式采样 `rig_Execute` 的 Root Motion delta、Yaw delta 和 normalized time 序列。
- [x] 3.3 支持采样 `rig_Executed` 与 `rig_Executed_Death` 的姿态预览，用于对比目标固定姿态与死亡分支表现。
  - 2026-10-06 修正：处决者与目标姿态统一改为生命周期受控的手动 `PlayableGraph` 采样，当前 Humanoid/Mecanim 动画可随时间轴连续播放；不再依赖无法可靠驱动该 Avatar 的 `AnimationClip.SampleAnimation`。
- [x] 3.4 增加采样失败诊断，避免静默退化为不可信轨迹。
- [x] 3.5 在窗口关闭、配置切换、域重载和异常路径中确定性清理 PreviewScene 与 PlayableGraph。
  - 2026-09-27 完成：预览源现在同时跟踪 Combat/Presentation 配置；配置资产引用变化会先执行 `DisposePreviewScene`，再按新来源重建。
- [x] 3.6 将预览工作区改为窗口内一键跳转的专用场景：跳转前确认未保存修改，以 `Single` 模式打开，非专用场景不创建预览对象，禁止后台 Additive 加载。
  - 2026-10-06 完成：窗口提供“进入预览场景”，通过 Unity 标准保存确认以 `OpenSceneMode.Single` 打开专用场景；离开专用场景时清理临时对象。Unity MCP 验证切换前后均仅有一个加载场景，业务场景中的预览对象数量为 0。
- [x] 3.7 对 `rig_Executed` / `rig_Executed_Death` 采样 Root Motion，并按当前目标分支持续时间构建目标 Warp 轨迹；资源/分支变化时正确失效并重采样。
  - 2026-10-07 完成：Visual Editor 对当前目标分支独立缓存 60 FPS Root Motion 样本，分支、动画或目标 Prefab 变化会失效缓存；运行动作会自动补采样。正式动画实测生还分支 212 帧、总位移 `(-0.044593, 0, -2.505857)`，死亡分支 153 帧、总位移 `(-0.013544, 0, -2.648080)`。

## 4. 可视化与交互编辑

- [x] 4.1 在窗口中实现时间轴，显示 Warp Window、结果时刻、动画时长和当前 Scrub 时间。
- [x] 4.2 在 Scene View 或预览区域绘制目标固定姿态、处决者初始姿态、锚点、原始 Root Motion 轨迹、Warp 后预测轨迹和残差线。
- [x] 4.3 实现 Scene View 锚点 Handle，拖动时实时更新 `executorAnchorOffset` 预览并刷新轨迹。
- [x] 4.4 实现 Warp Window、`executionWarpCurve`、位置/Yaw 预算、玩法时长、CrossFade 和 clip 校准字段的编辑 UI。
- [x] 4.5 显示每帧和最终诊断：原始 delta、修正 delta、累计修正量、预测残差、预算占用、结果时刻异常和预览有效状态。
  - 2026-09-27 完成：窗口新增可折叠的逐帧与最终诊断，按 Scrub 帧显示原始/修正 delta、累计修正、预测/应用后残差、预算占用、结果时刻和最终 Warp 状态。
- [x] 4.6 将时间轴移到窗口顶部固定区，并增加 Edit Mode“运行动作”/停止控制，复用采样轨迹推进 Scrub，不进入 Play Mode。
  - 2026-10-06 完成：固定时间轴和播放/停止/复位控制已接入 `EditorApplication.update`；处决者与目标按同一绝对预览时间播放各自 Playable，处决者根节点同步使用共享 Warp 轨迹。默认站位由共享空间判定和 `executorAnchorOffset` 生成，面对面且默认 Eligible。
- [x] 4.7 增加处决者与被处决者初始位置/旋转编辑 UI，修改后实时应用到预览对象并刷新共享空间判定和 Warp 轨迹。
  - 2026-10-06 完成：窗口新增两名角色各自的 Position/Rotation 工作副本；编辑时停止当前播放、回到时间轴起点并实时更新临时 Transform、共享空间资格与轨迹初始姿态。处决者预览在共享 Warp Yaw 之外保留配置的 X/Z 旋转。
- [x] 4.8 将动作预览改为绝对时间轴，分别按 `executingDuration` 和当前目标分支时长换算 normalized time；较短分支保持末帧，并增加不同时长映射的 EditMode 测试。
  - 2026-10-06 完成：新增共享秒数到双分支 normalized time 的纯映射；Timeline/Scrub/运行动作使用两段运行时持续时间中的较大值，较短动画到达 1 后保持末帧。正式配置在 2.7s 时实测处决者 `1.000`、生还目标 `0.628`、死亡目标 `0.844`，与运行时按分支持续时间调速一致。
- [x] 4.9 在 Visual Editor 增加目标生还/死亡锚点、Target Warp Window/曲线/预算编辑，Scene View Target Anchor Handle、目标原始/Warp 轨迹与目标残差诊断。
  - 2026-10-07 完成：窗口可编辑两条目标锚点、统一 Target Warp Window/曲线/预算；Timeline 同时显示并编辑 Executor/Target Window。Scene View 新增当前分支 Target Anchor Handle、目标原始与修正轨迹，逐帧诊断显示目标修正量、预算占用和最终残差。生还/死亡两分支均由 Unity MCP 验证为 `Preview Valid`，最终位置残差为 `0`。

## 5. 配置写回与资产安全

- [x] 5.1 通过 `SerializedObject` / `SerializedProperty` 写回 `CharacterCombatConfig` 和 `CharacterPresentationConfig`，不得手工编辑 `.asset` YAML。
- [x] 5.2 为字段编辑、时间轴拖拽和锚点 Handle 统一接入 Unity Undo/Redo。
  - 2026-09-27 完成：配置字段通过 `SerializedObject.ApplyModifiedProperties` 进入 Unity Undo 栈；Timeline 与锚点 Handle 在写回前显式调用 `Undo.RecordObject`，并通过 `Undo.undoRedoPerformed` 统一失效轨迹缓存和刷新窗口/Scene View。编辑器程序集编译通过；交互式 Undo/Redo 自动化覆盖保留在 6.1。
- [x] 5.3 正确标记 Dirty 并支持显式保存资产，重新打开窗口后读回最终序列化值。
  - 2026-09-27 完成：配置写回统一调用 `EditorUtility.SetDirty`，窗口显示两个配置资产的 Dirty/Saved 状态，并通过 `AssetDatabase.SaveAssetIfDirty` 仅保存当前选中的脏资产；保存后再次检查 Dirty 状态并报告结果。编辑器程序集编译通过；关闭/重开后的持久化自动化覆盖保留在 6.1。
- [x] 5.4 确保非法配置会显示诊断，并且不会被窗口误报为有效收敛。
  - 2026-09-27 完成：`Preview Valid` 仅在共享 Solver 配置合法、轨迹可生成、资源/采样有效且 `executionResultTime` 合法时显示；否则明确显示 `Config Invalid` 或 `Preview Blocked`。
- [x] 5.5 审计所有预览对象、临时材质、Handles 和 PlayableGraph 生命周期，确认不会持久化到 Scene、Prefab 或正式资产。
  - 2026-09-27 完成：预览根节点和两个实例均使用 `HideAndDontSave`，并由关闭窗口、域重载、Editor 退出、资源/配置切换路径集中销毁；Root Motion 采样的 `PlayableGraph` 在 `finally` 中销毁并恢复 Animator/Transform 状态；Handles 仅即时绘制并复位颜色，代码未创建临时 Material、Texture 或 Mesh。零残留自动化验证保留在 6.1。
- [x] 5.6 增加 Editor-only 可复用初始站位资产，支持新建、加载、Undo/Dirty 和显式覆盖保存，并以 EditMode 测试验证重载后的持久化。
  - 2026-10-06 完成：新增 `MotionWarpingPreviewLayout` Editor-only 资产及新建/加载/显式保存/按 Combat 锚点重置入口；工作副本修改不会自动覆盖资产或正式 Combat 配置。Unity Test Runner 验证窗口保存、强制重载与再次加载后的双角色 Transform 一致。
- [x] 5.7 通过 Unity 序列化新增目标分支锚点和 Target Warp 设置，以当前目标动画 Root Motion 终点作为安全默认值，并验证 Undo/Dirty/保存读回。
  - 2026-10-07 完成：`CharacterCombatConfig` 新增生还/死亡目标锚点、Target Warp Window/曲线/预算并带有限值钳制；`DefaultCombat.asset` 通过 Unity 读回确认使用两段正式动画的 Root Motion 终点。新增测试覆盖目标字段 Undo/Redo、Dirty、显式保存和强制重载读回。

## 6. 测试与验收

- [x] 6.1 增加 EditMode 测试覆盖共享核心、配置诊断、Undo/Redo、资产持久化和 PreviewScene 清理。
  - 2026-10-06 完成：`ExecutionWarpSolverTests` 覆盖共享核心；`MotionWarpingEditorPersistenceTests` 覆盖缺失配置诊断、配置字段 Undo/Redo 与保存后重载、预览站位资产保存/重载、双角色自定义 Transform 应用、窗口持有的 PreviewScene 和临时对象清理、默认面对面 Eligible 站位、Humanoid Playable 姿态采样及不同时长动画的绝对时间映射。Unity MCP 实际运行相关 EditMode 测试 18/18 通过，Console 无编译错误。
- [x] 6.2 增加编辑器预测与运行时共享核心的一致性测试，使用相同原始 Root Motion 采样输入比较修正 delta 和残差。
  - 2026-09-27 核对：`ExecutionWarpSolverTests.RuntimeContextMatchesSharedSolverForSameInputs` 使用同一配置、锚点、初始姿态和输入样本，逐帧比较 `ExecutionWarpContext` 与 `ExecutionWarpSolver` 的位置/Yaw 修正及实际残差。
- [x] 6.3 使用编辑器重新校准当前处决配置，重点复核 `executionResultTime`、Warp Window、`executorAnchorOffset` 和处决者残差。
  - 2026-10-05 完成：通过 Unity MCP 使用正式 `DefaultCombat.asset`、Player prefab 和 `rig_Execute` 进行 60 FPS/162 帧采样；`executionResultTime` 从 `0.01s` 校准为 `2.7s`，Warp Window 从 `0→0.45` 校准为 `0→1.0`，`executorAnchorOffset` 保持 `(0, 0, 0.6)`。编辑器预测最终位置残差 `0.00000m`、Yaw 残差 `0.0000°`，状态为 `Config Valid / Preview Valid / Runtime Unverified`。
  - 2026-10-07 最终调参资产读回：`executorAnchorOffset=(0,0,1.1777699)`、Executor Warp Window=`0→0.43759876`、平移/Yaw 预算=`1.2m/50°`、`executionResultTime=2.7s`；Target 生还/死亡锚点分别为 `(0,0,0)` / `(-0.013544,0,0)`，Target Warp Window=`0→1`、预算=`4m/90°`。
- [x] 6.4 在 Offline 场景运行双 Player 处决对照验收，记录编辑器预测残差与运行时实际残差。
  - 2026-10-07 完成：用户在 Offline 双 Player 中手动运行处决，双方动画、位移和目标击飞观感未见明显异常或可见末端残差；编辑器双目标分支预测最终位置/Yaw 残差为 `0`。本次运行时残差为肉眼对照记录，未通过 Calibration Harness 采集数值；Host/Client 网络对照不属于本项。
- [ ] 6.5 通过 Unity MCP 重新编译并检查 Console/编译错误，运行相关 EditMode/PlayMode 测试、`git diff --check` 和 `openspec validate add-motion-warping-visual-editor --strict`。
  - 2026-10-07 当前证据：此前 Unity MCP 重编译与 Console 检查无错误，相关 EditMode 回归 `28/28` 通过；本次 Offline 手动 Play Mode 验收通过，`openspec validate ... --strict` 与未暂存 `git diff --check` 通过。最终复核时 4399 MCP 无响应，且暂存区 `git diff --cached --check` 仍报告 Unity 自动生成 `.asset/.meta` 空值行的尾随空格，因此保留本项未完成。
- [x] 6.6 更新验证矩阵或相关文档，明确区分 `Preview Valid`、`Config Valid` 和 `Runtime Verified`。
  - 2026-10-07 完成：设计文档新增验证矩阵，记录当前 `Config Valid`、双目标分支 `Preview Valid` 和尚未完成 6.4 的 `Runtime Verified`，并明确单元测试/编译通过不会自动替代实际 Offline 验收。
- [x] 6.7 增加通用 Settings/Context、Player/NPC Target Warp、目标轨迹预览及状态恢复双方姿态的 EditMode/PlayMode 回归测试。
  - 2026-10-07 完成：新增通用 Settings/Context、分支锚点、Player/NPC Executed Target Warp、Mirror 双方当前姿态序列化、目标双分支编辑器轨迹和目标配置持久化回归；Unity MCP 运行相关 EditMode 测试 28/28 通过。完整 Offline 双 Player 动作观感仍由 6.4 单独验收。
