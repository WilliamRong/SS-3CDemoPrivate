## Context

当前处决运行时已经有 `ExecutionWarpContext`，它根据会话锚点、初始处决者姿态、配置、原始 Root Motion delta 和 normalized time 计算修正后的位移/Yaw，并记录预测残差和真实残差。运行链路是 `AnimatorRootMotionRelay -> PlayerController -> ExecutingState -> ExecutionWarpContext -> CharacterMotor`，目标 `Executed` 侧固定世界位置和 Yaw，不消费自身 Root Motion。

现有 OpenSpec 验收暴露两个校准痛点：`DefaultCombat.asset` 当前 `executionResultTime=0.01`，与设计期望的约 `2.7s` 不符；非致死双 Player 复测中目标残差为 `0.0000m`，但处决者残差约 `1.5414m`。这些问题不是单靠 Inspector 数字和运行时日志容易判断的，需要把动画轨迹、锚点、Warp Window、结果时刻和预算同时可视化。

工具的使用者是设计/程序调参人员。目标是让他们不用进入 Play Mode 或手动搭测试场景，也能在 Unity Editor 中预览和编辑处决等 MW 动作的数据；最终的 Offline/Online 场景仍用于运行时对照验收。

## Goals / Non-Goals

**Goals:**

- 提供 Editor-only `EditorWindow`，用于选择正式配置、预览角色和动画资源。
- 在隔离预览环境中显示目标固定姿态、处决者初始姿态、锚点、原始 Root Motion 轨迹、Warp 后预测轨迹、当前帧姿态和位置/Yaw 残差。
- 支持编辑 `executorAnchorOffset`、Warp Window、`executionWarpCurve`、位置/Yaw 预算、处决相关玩法时长、结果时刻、CrossFade 和 clip 校准字段。
- 使用 Unity 序列化路径写回正式配置，支持 Undo/Redo、Dirty/Save 和重新打开后的资产持久化。
- 抽取或封装共享的纯 MW 采样/轨迹计算核心，让 Editor 预览与 Runtime 使用同一语义。
- 通过诊断明确标出资源缺失、非法窗口、非单调曲线、预算超限、结果时间异常和预览不可判定状态。

**Non-Goals:**

- 不改变处决运行时资格、会话、权威位移、伤害提交或死亡判定。
- 不要求开发者手动打开测试场景才能使用编辑器本体。
- 不引入第三方包，不要求 Animation Rigging、IK 或 `Animator.MatchTarget`。
- 第一版不解决手部、武器或不同体型角色的精确接触，只校准根运动轨迹、锚点和时间。
- 不在 Player 构建中包含 Editor 代码或 PreviewScene/PlayableGraph 工具代码。

## Decisions

### 使用 EditorWindow + PreviewScene 作为主入口

编辑器作为 `EditorWindow` 提供，不依赖当前打开的 Scene。窗口创建隔离 `PreviewScene` 或等价临时环境，实例化预览角色和目标姿态对象，关闭窗口、切换配置或域重载时清理。

选择该方案是因为手动测试场景适合最终验收，但不适合日常调参：场景对象容易被污染，状态机/网络/输入也会让轨迹诊断噪声变多。PreviewScene 让工具可以稳定复现同一组动画采样和配置输入。

备选方案是要求用户打开专用测试场景并选择两个角色。它实现较快，但会把工具和场景层级绑死，也难以保证 Undo/Redo 与临时对象清理，因此只作为后续“运行时对照模式”保留。

### 使用 PlayableGraph 采样动画 Root Motion

编辑器应优先通过 `PlayableGraph` 驱动预览 Animator 采样 `rig_Execute` 等 clip，并按固定采样率生成每帧 Root Motion delta。这样更接近运行时 `Animator.deltaPosition` / `deltaRotation` 的来源。

备选方案是直接使用 `AnimationClip.SampleAnimation`。它适合显示姿势，但 Root Motion delta 和 Animator Controller/Avatar 处理可能与运行时不完全一致。第一版可以把它作为降级路径，用于资源缺失或无 Animator 的诊断预览，但不能作为通过验收的唯一采样语义。

### 抽取共享的纯 Warp 计算核心

当前 `ExecutionWarpContext` 已经接近纯计算，但它同时承担运行时上下文状态。设计上应把“根据配置、锚点、原始 delta、normalized time 计算修正 delta 和残差序列”的逻辑抽成运行时和 Editor 共用的纯核心，例如 `ExecutionWarpSolver` / `MotionWarpTrajectorySampler`。

运行时继续可以通过 `ExecutionWarpContext` 持有会话状态；Editor 则用同一核心离线计算整段轨迹。这样避免编辑器复制一份公式，后续运行时调整曲线、窗口或残差定义时不会出现工具漂移。

### 使用 SerializedObject 写回正式配置

所有配置修改通过 `SerializedObject` / `SerializedProperty` 完成，配合 `Undo.RecordObject`、`EditorUtility.SetDirty` 和 `AssetDatabase.SaveAssets`。不得手写 `.asset` YAML，也不得直接绕过 Unity 序列化改持久化资源。

这对 `CharacterCombatConfig` 和 `CharacterPresentationConfig` 尤其重要：它们已有 `OnValidate` 钳制规则，编辑器需要让这些规则自然生效，并让撤销栈符合 Unity 编辑器语义。

### 首版以处决动作配置为模板，但数据模型预留泛化入口

首版窗口围绕处决场景展开：处决者 `rig_Execute`、目标 `rig_Executed`、目标致死 `rig_Executed_Death`、目标固定姿态和处决者锚点。内部数据结构可以命名为更通用的 `MotionWarpPreviewProfile` 或 `MotionWarpActionPreview`，但不急于引入新的运行时资产类型。

原因是当前真正阻塞的是处决校准；过早泛化成所有 MW 动作会引入 profile 生命周期、动作绑定和运行时解析策略。等处决工具稳定后，再考虑把配置抽成独立 `MotionWarpProfile`，服务冲刺斩、抓取、背刺等动作。

### 明确区分预览有效、配置有效和运行时验收

编辑器可以证明同一采样输入下的预测轨迹、预算和残差，但不能替代真实 `CharacterController` 碰撞、状态机、网络权威和 Play Mode 验收。窗口应明确显示预览来源和状态，例如 `Preview Valid`、`Config Invalid`、`Runtime Unverified`。

这避免把“可视化里收敛”误解成“游戏里一定收敛”。墙体阻挡、碰撞步进、角色控制器半径和网络追赶仍需要 Offline/Host/Client 验收。

## Risks / Trade-offs

- [Risk] PlayableGraph 采样和运行时 Animator Controller 仍可能有细微差异。→ Mitigation: 用相同 Avatar/Animator 预制体采样，提供运行时对照按钮，并用 EditMode/PlayMode 测试比较同输入下的修正 delta。
- [Risk] 预览对象污染当前 Scene 或正式 Prefab。→ Mitigation: 使用 PreviewScene，所有临时对象命名和生命周期集中管理，窗口关闭、配置切换和域重载时清理。
- [Risk] 编辑器复制运行时公式后产生漂移。→ Mitigation: 抽取共享纯计算核心，Editor 禁止实现第二套 Warp 修正公式。
- [Risk] 用户把预览通过误认为运行时通过。→ Mitigation: UI 区分预览状态、配置状态和运行时验收状态，并在存在碰撞/网络未验证时显示明确提示。
- [Risk] 功能一次做得太泛导致落地慢。→ Mitigation: 首版只覆盖处决三段动画和现有配置字段，后续再抽象通用动作 profile。
- [Risk] 时间轴和 Scene View Handle 修改多个资产字段，Undo/Redo 容易不一致。→ Mitigation: 每次交互通过统一修改事务记录 Undo，并增加 EditMode 测试覆盖撤销、保存、重开窗口。

## Migration Plan

1. 抽取共享 MW 轨迹计算核心，保持现有 `ExecutionWarpContext` 外部行为不变。
2. 新增 Editor-only 程序集或 `Editor/` 目录，创建 `MotionWarpingVisualEditorWindow`。
3. 实现配置/动画/预览 prefab 选择与基础诊断，不写回资产。
4. 接入 PlayableGraph 采样和轨迹计算，显示原始轨迹、修正轨迹、锚点和残差。
5. 加入 Scene View Handle、时间轴、曲线编辑和 `SerializedObject` 写回。
6. 加入 Undo/Redo、Dirty/Save、PreviewScene 清理和非法配置诊断测试。
7. 使用现有 Offline 处决场景做运行时对照，更新验证矩阵。

回滚策略：删除 Editor-only 工具代码并保留共享核心兼容层；如果共享核心回滚风险较高，则让 `ExecutionWarpContext` 保持原实现，删除 Editor 对其的引用即可。配置资产字段不新增时无需数据迁移。

## Open Questions

- 首版是否只内置处决三段动画，还是同时提供可选的通用 `AnimationClip` 列表预览模式？
- 预览角色默认使用 `Assets/Prefabs/Characters/Player.prefab`，还是要求用户显式选择 Avatar/Animator prefab？
- 时间轴采样率使用固定 60 FPS，还是允许用户切换 30/60/120 FPS 以观察残差变化？
- 是否在第一版提供“一键应用建议值”，例如把 `executionResultTime` 对齐到命中帧或把 Warp Window 末端对齐到收敛点？该功能风险较高，建议先只诊断不自动改。
