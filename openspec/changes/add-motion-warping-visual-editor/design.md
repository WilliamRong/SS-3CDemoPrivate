## Context

当前处决运行时已经有 `ExecutionWarpContext`，它根据会话锚点、初始参与者姿态、配置、原始 Root Motion delta 和 normalized time 计算修正后的位移/Yaw，并记录预测残差和真实残差。原链路只覆盖 `AnimatorRootMotionRelay -> PlayerController -> ExecutingState -> ExecutionWarpContext -> CharacterMotor`；目标 `Executed` / `NpcExecutedState` 每帧强制恢复 `FixedTargetPose`，因此 `rig_Executed` 约 `(-0.045, 0, -2.506)m` 与死亡分支约 `(-0.014, 0, -2.648)m` 的原始击飞 Root Motion 被全部丢弃。

现有 OpenSpec 验收暴露两个校准痛点：`DefaultCombat.asset` 当前 `executionResultTime=0.01`，与设计期望的约 `2.7s` 不符；非致死双 Player 复测中目标残差为 `0.0000m`，但处决者残差约 `1.5414m`。这些问题不是单靠 Inspector 数字和运行时日志容易判断的，需要把动画轨迹、锚点、Warp Window、结果时刻和预算同时可视化。

工具的使用者是设计/程序调参人员。目标是让他们不用进入 Play Mode 或手动搭测试场景，也能在 Unity Editor 中预览和编辑处决等 MW 动作的数据；最终的 Offline/Online 场景仍用于运行时对照验收。

## Goals / Non-Goals

**Goals:**

- 提供 Editor-only `EditorWindow`，用于选择正式配置、预览角色和动画资源。
- 在一键进入的专用编辑器场景中显示双方初始姿态、双方锚点、双方原始 Root Motion 轨迹、Warp 后预测轨迹、当前帧姿态和位置/Yaw 残差。
- 将时间轴固定在窗口顶部，并提供不进入 Play Mode 的动作播放/停止控制。
- 支持独立配置处决者与目标的预览初始位置/旋转，并将站位保存为 Editor-only 资产以便跨窗口和跨动作复用。
- 支持编辑处决者与目标分支锚点、Warp Window、曲线、位置/Yaw 预算、处决相关玩法时长、结果时刻、CrossFade 和 clip 校准字段。
- 使用 Unity 序列化路径写回正式配置，支持 Undo/Redo、Dirty/Save 和重新打开后的资产持久化。
- 抽取或封装共享的纯 MW 采样/轨迹计算核心，让 Editor 预览与 Runtime 使用同一语义。
- 通过诊断明确标出资源缺失、非法窗口、非单调曲线、预算超限、结果时间异常和预览不可判定状态。

**Non-Goals:**

- 不改变处决运行时资格、伤害提交或死亡判定；目标位移仍只由 Server/Host 权威消费并通过既有同步链路复制。
- 不要求开发者在 Project 面板手动查找测试场景；窗口提供保存确认和一键跳转。
- 不引入第三方包，不要求 Animation Rigging、IK 或 `Animator.MatchTarget`。
- 第一版不解决手部、武器或不同体型角色的精确接触，只校准根运动轨迹、锚点和时间。
- 不在 Player 构建中包含 Editor 代码或 PlayableGraph 预览工具代码。

## Decisions

### 使用 EditorWindow + 专用 Single 场景作为主入口

编辑器作为 `EditorWindow` 提供，并提供“一键进入预览场景”按钮。按钮在 Unity 标准未保存场景确认通过后，以 `OpenSceneMode.Single` 打开 `Assets/Scenes/MotionWarpingVisualToolPreview.unity`。只有该专用场景处于活动状态时，窗口才实例化临时预览角色和目标姿态对象；关闭窗口、切换配置、离开场景或域重载时清理。

选择该方案是因为后台 Additive 场景的可见性和当前工作上下文不明确，用户也难以确认 Scene View 正在观察哪套环境。专用场景让工作区显式可见，同时通过 `HideAndDontSave` 临时对象和确定性清理避免把预览角色写入场景资产。

窗口不得自动用 Additive 模式加载场景，也不得在任意业务场景中创建预览对象。真实 `Offline` 场景继续只用于运行时对照模式，不与编辑器预览场景混用。

### 使用固定时间轴与 Edit Mode 动作播放

时间轴位于窗口滚动区域之外的顶部固定区，始终显示处决者时长、当前目标分支时长、完整预览时长、Warp Window、结果时刻和当前 Scrub 秒数。“运行动作”在已有采样有效时通过 `EditorApplication.update` 推进绝对秒数，并分别计算 `executorNormalized = elapsed / executingDuration` 与 `targetNormalized = elapsed / selectedTargetDuration`；停止或到达两者较长时长的末端后取消更新。该按钮不进入 Play Mode、不创建运行时会话，也不改变 `Runtime Verified`。

处决者与目标的可见骨骼姿态分别由生命周期受控的手动 `PlayableGraph` 驱动，而不是依赖 `AnimationClip.SampleAnimation`。每次 Scrub 使用同一绝对秒数、按各自运行时配置时长换算后的 normalized time 设置 `rig_Execute` 与目标当前分支 clip；较短分支到达 1 后保持末帧，双方根节点分别使用各自的共享 Warp 轨迹覆盖位置/Yaw。目标分支时长变化或切换生存/死亡分支时立即重新采样并映射当前时间。切换资源、离开场景、域重载或关闭窗口时销毁两个 Graph。

不能按源 clip 长度或共享 normalized time 推进目标动画：运行时 `CharacterCombatPresenter` 使用 `sourceClipDuration / configuredBranchDuration` 设置目标 Animator 速度，因此 Editor 必须以 `executedDuration` / `executedDeathDuration` 作为目标时基，避免把较长目标分支压缩进 `rig_Execute` 的时长。

初次创建预览对象时，Target 固定在原点并沿自身前向放置 Executor：Executor 位置取 `targetPose + targetYaw * executorAnchorOffset`，朝向取共享 `ExecutionSpatialValidator` 返回的锚点朝向（面对 Target）。窗口同时显示共享空间判定结果，避免用一套仅供 Editor 的锚点朝向公式。

### 使用 Editor-only 资产复用预览初始站位

窗口维护处决者与目标各自的世界位置和 Euler 旋转；字段修改后立即应用到临时预览对象并刷新轨迹、预算和空间判定。未选择站位资产时，首次预览仍从正式 `executorAnchorOffset` 和共享空间判定生成默认面对面站位。

可复用站位使用位于 `Editor/` 目录下的 `ScriptableObject` 类型保存，只包含两名角色的预览 Transform，不进入 Player 构建，也不参与运行时处决解析。创建、加载和覆盖保存都通过 `AssetDatabase`、`SerializedObject` 和 Undo/Dirty 流程完成；编辑字段只改变窗口内工作副本，必须显式点击保存才覆盖所选站位资产。Combat 锚点重置只重建工作副本，不自动修改正式 Combat 配置或站位资产。

### 使用 PlayableGraph 采样双方动画 Root Motion

编辑器应优先通过 `PlayableGraph` 驱动预览 Animator，分别采样 `rig_Execute`、`rig_Executed` 与 `rig_Executed_Death`，并按固定采样率生成每帧 Root Motion delta。这样更接近运行时 `Animator.deltaPosition` / `deltaRotation` 的来源，且目标击飞轨迹不会被误当成固定姿态。

备选方案是直接使用 `AnimationClip.SampleAnimation`。实测它不能可靠驱动当前 Humanoid/Mecanim 预览姿态，因此不作为播放降级路径；Animator、Avatar 或 Playable 不可用时必须明确阻断并显示诊断。

### 将 Solver 参数化为双方共用的纯 Warp 计算核心

`ExecutionWarpSolver` 不应直接读取只属于处决者的配置字段。新增通用 `ExecutionWarpSettings` 值对象，包含 Warp Window、曲线和位置/Yaw 预算；Executor/Target 只负责从 `CharacterCombatConfig` 组装设置与锚点。`ExecutionWarpContext` 记录参与者角色并验证 ActorId，但双方都调用同一 `TryWarp`、Motor 应用回读和残差语义。

运行时继续通过 `ExecutionWarpContext` 持有会话状态；Editor 用同一 Settings、Anchor Resolver 与 Trajectory Sampler 离线计算双方整段轨迹。目标生还与死亡分支各有局部锚点偏移/Yaw，默认值取当前动画真实 Root Motion 终点；共同目标 Warp Window/曲线/预算控制收敛。这样既保留默认击飞，也允许为不同站位校准目标落点。

### 目标 Root Motion 只在权威端消费

Player 的 `ExecutedState` 进入时启用 Reaction Root Motion，NPC 的 `NpcExecutedState` 通过 `NpcMotor.ApplyRootMotionDelta` 消费；两者先让通用 `ExecutionWarpContext` 修正 Animator delta，再由 CharacterController/NavMeshAgent 应用并回读实际残差。客户端不自行预测目标 Warp，继续使用权威 Transform 快照。

状态恢复消息必须同时携带 Executor 和 Target 当前权威姿态；恢复目标时不得再回退到会话创建时的 `FixedTargetPose`。`FixedTargetPose` 仍作为目标初始参考姿态和局部目标锚点基准，不代表执行期间固定不动。

### 使用 SerializedObject 写回正式配置

所有配置修改通过 `SerializedObject` / `SerializedProperty` 完成，配合 `Undo.RecordObject`、`EditorUtility.SetDirty` 和 `AssetDatabase.SaveAssets`。不得手写 `.asset` YAML，也不得直接绕过 Unity 序列化改持久化资源。

这对 `CharacterCombatConfig` 和 `CharacterPresentationConfig` 尤其重要：它们已有 `OnValidate` 钳制规则，编辑器需要让这些规则自然生效，并让撤销栈符合 Unity 编辑器语义。

### 正式配置资产与游戏运行时绑定

Visual Editor 默认加载并显式保存 `Assets/Data/Character/DefaultCombat.asset` 与 `Assets/Data/Character/DefaultPresentation.asset`。静态 GUID 引用审计确认，游戏运行时通过同一组 ScriptableObject 实例读取这些值：

| 数据入口 | 引用链 | 运行时消费者 |
|---|---|---|
| Combat | `GameDataCatalog.asset → DefaultPlayer.asset → DefaultCombat.asset` | `PlayerController`、`NpcCharacterDriver`、`ExecutionSessionCoordinator`、双方 `ExecutionWarpContext` |
| Presentation | `GameDataCatalog.asset → DefaultPlayer.asset → DefaultPresentation.asset` | `CharacterLateUpdatePipeline → CharacterCombatPresenter` |
| 预览初始站位 | `Assets/Editor/MotionWarpingPreviewLayouts/*.asset` | 仅 Visual Editor；不进入 Player 构建，不被运行时读取 |

因此，在窗口选择默认 Combat/Presentation 资产并点击显式保存后，重新进入 Play Mode 或重新生成角色即可让游戏读取最终值。若在窗口中改选其他自定义配置资产，保存只会修改该资产，不会自动替换 `GameDataCatalog` / `CharacterDefinition` 的运行时引用。运行中热改也不能保证所有字段即时更新：状态构造时会缓存双方持续时间，完整验证必须退出并重新进入 Play Mode。

### 首版以处决动作配置为模板，但数据模型预留泛化入口

首版窗口围绕处决场景展开：处决者 `rig_Execute`、目标 `rig_Executed`、目标致死 `rig_Executed_Death`、双方锚点和双方轨迹。共享计算采用通用 Settings/Context，但暂不引入新的独立运行时 profile 资产，避免额外迁移和绑定生命周期。

原因是当前真正阻塞的是处决校准；过早泛化成所有 MW 动作会引入 profile 生命周期、动作绑定和运行时解析策略。等处决工具稳定后，再考虑把配置抽成独立 `MotionWarpProfile`，服务冲刺斩、抓取、背刺等动作。

### 明确区分预览有效、配置有效和运行时验收

编辑器可以证明同一采样输入下的预测轨迹、预算和残差，但不能替代真实 `CharacterController` 碰撞、状态机、网络权威和 Play Mode 验收。窗口应明确显示预览来源和状态，例如 `Preview Valid`、`Config Invalid`、`Runtime Unverified`。

这避免把“可视化里收敛”误解成“游戏里一定收敛”。墙体阻挡、碰撞步进、角色控制器半径和网络追赶仍需要 Offline/Host/Client 验收。

当前验证矩阵（2026-10-07）：

| 状态 | 判定范围 | 当前结果 | 不能证明的内容 |
|---|---|---|---|
| `Config Valid` | 双方配置字段有限、Warp Window/曲线/预算合法，结果时刻有效 | 通过 | 动画是否能在实际场景移动 |
| `Preview Valid` | 正式 Prefab/动画采样成功，双方共享 Solver 轨迹可生成且预测收敛 | 生还/死亡目标分支通过，双方最终预测残差为 `0` | `CharacterController`、NavMesh、墙体碰撞和网络追赶后的实际残差 |
| `Runtime Verified` | Offline 双 Player 在权威端完整播放，并记录双方实际残差和观感 | 2026-10-07 用户手动验收通过：双方动画、位移和目标击飞未见明显异常或可见末端残差；本次未采集数值残差 | Host/Client 跨网络结果；墙体/NavMesh 极端布局和数值残差仍需后续专项验收 |

单元测试和 Unity MCP 编译通过只作为前两项及运行时管线回归证据；本次 `Runtime Verified` 来自独立的 Offline 双 Player 手动 Play Mode 验收，而不是由单元测试自动推导。

## Risks / Trade-offs

- [Risk] PlayableGraph 采样和运行时 Animator Controller 仍可能有细微差异。→ Mitigation: 用相同 Avatar/Animator 预制体采样，提供运行时对照按钮，并用 EditMode/PlayMode 测试比较同输入下的修正 delta。
- [Risk] 专用场景中的预览对象被误保存。→ Mitigation: 只在场景路径精确匹配时创建对象，统一使用 `HideAndDontSave`，窗口关闭、切换配置、离开场景和域重载时清理；跳转使用 Single 模式，禁止后台 Additive。
- [Risk] 编辑器复制运行时公式后产生漂移。→ Mitigation: 抽取共享纯计算核心，Editor 禁止实现第二套 Warp 修正公式。
- [Risk] 用户把预览通过误认为运行时通过。→ Mitigation: UI 区分预览状态、配置状态和运行时验收状态，并在存在碰撞/网络未验证时显示明确提示。
- [Risk] 功能一次做得太泛导致落地慢。→ Mitigation: 首版只覆盖处决三段动画和现有配置字段，后续再抽象通用动作 profile。
- [Risk] 时间轴和 Scene View Handle 修改多个资产字段，Undo/Redo 容易不一致。→ Mitigation: 每次交互通过统一修改事务记录 Undo，并增加 EditMode 测试覆盖撤销、保存、重开窗口。
- [Risk] 调试站位被误认为运行时出生点或自动污染正式配置。→ Mitigation: 站位类型和默认保存目录均为 Editor-only，UI 明确标注“预览”，并采用显式保存而非字段编辑自动写回。
- [Risk] 目标击飞可能被墙体或 NavMesh 边界阻挡。→ Mitigation: 双方沿用实际 Motor 应用回读和 shortfall 诊断；Editor 只标记预测有效，Offline/Online 仍负责碰撞验收。
- [Risk] 非权威客户端重复消费目标 Root Motion 造成漂移。→ Mitigation: Executing/Executed Warp 仅在 Server/Host 位移，客户端只播放骨骼并跟随权威快照；状态恢复携带双方当前姿态。

## Migration Plan

1. 将共享 MW 轨迹计算核心参数化为通用 Settings，并保持现有 Executor API 兼容。
2. 新增 Editor-only 程序集或 `Editor/` 目录，创建 `MotionWarpingVisualEditorWindow`。
3. 实现配置/动画/预览 prefab 选择与基础诊断，不写回资产。
4. 接入 PlayableGraph 采样和轨迹计算，显示原始轨迹、修正轨迹、锚点和残差。
5. 加入 Scene View Handle、时间轴、曲线编辑和 `SerializedObject` 写回。
6. 加入 Undo/Redo、Dirty/Save、专用场景导航、临时对象清理和非法配置诊断测试。
7. 接入 Player/NPC 目标 Root Motion、目标分支锚点与状态恢复双方姿态。
8. 使用现有 Offline 处决场景做运行时对照，更新验证矩阵。

回滚策略：删除 Editor-only 工具代码并保留共享核心兼容层；如果共享核心回滚风险较高，则让 `ExecutionWarpContext` 保持原实现，删除 Editor 对其的引用即可。配置资产字段不新增时无需数据迁移。

## Open Questions

- 首版是否只内置处决三段动画，还是同时提供可选的通用 `AnimationClip` 列表预览模式？
- 预览角色默认使用 `Assets/Prefabs/Characters/Player.prefab`，还是要求用户显式选择 Avatar/Animator prefab？
- 时间轴采样率使用固定 60 FPS，还是允许用户切换 30/60/120 FPS 以观察残差变化？
- 是否在第一版提供“一键应用建议值”，例如把 `executionResultTime` 对齐到命中帧或把 Warp Window 末端对齐到收敛点？该功能风险较高，建议先只诊断不自动改。
