## 1. 建立处决数据与状态契约

- [x] 1.1 在 `CharacterCombatConfig` 与 `DefaultCombat.asset` 增加最大距离、正面半角、高度差、视线/路径层、锚点偏移、Warp 平移/Yaw 预算、Warp Window/曲线、双方玩法时长和结果时间，并校验非负、有限与有序约束。当前默认资产已存在这些字段；具体数值仍需 Offline 校准。
- [x] 1.2 在 `CharacterPresentationConfig` 与默认表现资产增加 `Executing` / `Executed` / `ExecutedDeath` CrossFade 和 clip 校准参数，保持玩法时钟与表现参数分离。配置字段已声明并通过 `ExecutionConfigPersistence` 写入 `DefaultPresentation.asset`；最终数值仍需 Offline 校准。
- [x] 1.3 只在现有枚举末尾追加 `CharacterStateId.Executing/Executed`、`DeathPresentationVariant.Default/Executed`、转换原因、动作类型和处决协议标识，更新字符串映射且不改变既有数值。
- [x] 1.4 定义包含 `executionId`、双方 ActorId、固定目标姿态、锚点、开始/结果时间和完成状态的 `ExecutionSession` 数据，并提供有限值与角色唯一性校验。
- [x] 1.5 更新 Player/NPC 状态注册、转换图、锁定状态查询和表现优先级，使 `Executed` 只能由权威会话从 `Parried` / `PostureBroken` 进入，并让该路径以处决死亡变体进入 `Dead`。

## 2. 实现候选解析与权威资格校验

- [x] 2.1 实现无分配或低分配的处决候选查询，读取当前锁定目标并按距离、角度和稳定 ActorId 为附近 Player/NPC 选择唯一候选。
- [x] 2.2 实现水平距离、目标正面半角、高度差、存活/状态/会话占用、视线、Capsule/路径空间和 Warp 预算校验，并返回明确拒绝码。
- [x] 2.3 仅在 Idle/Move 的左键普通攻击处理前接入处决解析：合法候选只提交一次处决请求，无候选保持既有普通 Attack；Guard 必须跳过处决解析并保持既有普通 Attack，禁止旧脉冲补执行。
- [x] 2.4 实现 Offline/Server 共用的权威资格服务，始终使用当前权威状态与 Transform 重算条件，不接受 Client 提交的距离、角度或锚点。
- [x] 2.5 增加正面扇区、最大距离、锚点和 Warp 预算 Scene Gizmo，以及候选拒绝码、距离、角度、高度差与路径诊断。`ExecutionSpatialGizmo` 已绘制正面扇区、距离、锚点和误差，`CharacterStateDebugOverlay` 已覆盖候选拒绝码与空间/Warp 数据。

## 3. 实现处决会话与成对状态

- [x] 3.1 实现权威 `ExecutionSessionCoordinator`，原子预占双方 Actor、分配单调 `executionId`、计算固定目标姿态/锚点并支持幂等查询和释放。
- [x] 3.2 实现 Player `ExecutingState`，进入时取消普通动作、阻断角色输入、启动处决 Root Motion/Warp 和计时，按配置时长结束且不依赖 Animator 状态。
- [x] 3.3 实现 Player `ExecutedState`，固定目标位置/Yaw、阻断输入和 Root Motion、保持会话锁存，并在配置时长后以 `DeathPresentationVariant.Executed` 进入 `Dead`。
- [x] 3.4 实现 NPC `NpcExecutedState`，停止攻击与 NavMesh 行为、固定权威 Transform、忽略 AI 意图，并在配置时长后以处决死亡变体进入 `Dead`。
- [x] 3.5 让 `Parried` 与 `PostureBroken` 在会话接受时退出到 `Executed`，同时保留会话接受前的既有超时、普通 HP 伤害和 `Dead` 优先级。
- [x] 3.6 在配置 `executionResultTime` 只提交一次权威致死 HP/revision，锁存结果且不让普通 `Dead` 表现在 `Executed` 动画完成前覆盖配对表现；完成后只提交一次处决死亡变体。
- [x] 3.7 处理 Actor 销毁、场景卸载、网络断开和重复退出，幂等清理双方占用、Warp、无敌和攻击抑制，不留下锁死 Actor。生命周期服务现在广播累计完成/取消边沿，远端收到 `ExecutionCompleteMsg` 后按角色释放抑制并等待快照确认。
- [x] 3.8 修正处决结果：使用配置处决伤害并锁存致死分支；非致死目标播放 `rig_Executed` 后回到 `Idle`，致死目标只播放一次 `rig_Executed_Death` 并在完成后进入 `Dead`。

## 4. 实现可复用无敌与处决攻击抑制

- [x] 4.1 实现按 `(InvulnerabilitySource, ownerId)` 叠加和幂等释放的 `InvulnerabilityRuntime`，由 `CombatActor` 统一关闭/恢复 `CombatHurtBox` 并以 `CanReceiveHit` 兜底；迁移 Player Dodge 无敌窗口并验证双令牌叠加、HurtBox 开关、Prefab 组成和 Unity 零编译错误。
- [x] 4.2 在 `CombatActor` 增加与无敌独立、带 `executionId` 所有权的攻击抑制令牌及 `CanProduceCombatHit`，无敌本身不得改变攻击能力。
- [x] 4.3 处决开始时取消双方当前攻击实例和剩余命中窗口，分别申请处决无敌和攻击抑制，并让 `CombatResolver` 同时拒绝被抑制攻击方和无敌目标方的普通命中。
- [x] 4.4 让独立处决致死事务绕过普通 HurtBox 命中入口，但继续使用绝对 HP/revision、死亡同步、处决死亡变体和重复提交保护。
- [x] 4.5 在双方各自动画完成或异常清理时幂等释放处决无敌和攻击抑制；目标完成后由 `Dead` 继续不可受击，处决者下一逻辑帧恢复普通战斗。远端通过 `ExecutionCompleteMsg` 对称释放，并在致死结果乱序时等待绝对 HP 结果。
- [ ] 4.6 验证处决无敌期间所有 `CombatHurtBox` 关闭而 CharacterController、NavMesh、地面和墙体碰撞仍生效，并覆盖多来源令牌不会提前恢复、攻击抑制不会污染普通 Dodge 无敌。2026-09-17 通过 4399 MCP Offline 临时 Runner 部分验证：Dodge/Execution 多令牌不会提前恢复，Dodge 无敌不抑制出招，Execution 攻击抑制释放后不污染 Dodge；处决中双方 HurtBox 关闭、完成后恢复，CharacterController 保持 enabled，Offline Arena 的 Ground/Wall/Obstacle 共 7 个 collider 均 enabled 且非 Trigger。NavMesh/NPC 碰撞专项仍未覆盖，因此本项不勾选。

## 5. 实现 Motion Warping 与配对动画

- [x] 5.1 为现有 Animator Root Motion relay/Motor 增加会话级 Warp 上下文，保存锚点、窗口、曲线、原始/修正 delta 和剩余位置/Yaw 误差。
- [x] 5.2 在 `Executing` Warp Window 内按单调累计权重合并有限平移与旋转修正，窗口外恢复原始 `rig_Execute` Root Motion，所有位移继续经过 CharacterController 碰撞。
- [x] 5.3 让 `Executed` Player/NPC 丢弃 `rig_Executed` 的位置与旋转 delta，并每个权威 Tick 保持会话固定目标姿态。
- [x] 5.4 通过 Unity Editor 在 `CharacterAnimator.controller` 增加全身 `Executing` / `Executed` / `ExecutedDeath` 状态并分别引用 `rig_Execute` / `rig_Executed` / `rig_Executed_Death`，不覆盖现有 Parried/PostureBroken 和普通 Death 状态。
- [x] 5.5 扩展 `AnimatorParams`、`CharacterCombatPresenter` 与 `CharacterLateUpdatePipeline`，统一处理本地、Server NPC 和远端处决层权重、CrossFade、进入版本、处决死亡变体和完成清理。远端 Start/Result/Complete 表现与完成清理路径已接入。
- [ ] 5.6 在 Offline 双角色预览中校准 `executorAnchorOffset`、Warp Window、最大 Yaw、双方时长、三段 CrossFade 和 `executionResultTime`，记录最终配置与接触残差。2026-09-17 读回 `DefaultCombat.asset` 当前值：`executionMaxDistance=1.5`、`executionFrontHalfAngle=80`、`executorAnchorOffset=(0,0,0.60)`、`executionMaxWarpTranslation=1.6`、`executionMaxWarpYaw=120`、`executingDuration=2.7`、`executedDuration=4.3`、`executedDeathDuration=3.2`、`executionResultTime=0.01`。非致死 Offline 双 Player 复测中目标残差 `0.0000m/0.000°`，处决者残差约 `1.5414m/0.000°`，且结果时刻与设计期望约 `2.7s` 不符；本项明确未通过。

## 6. 保留镜头输入并锁定角色控制

- [x] 6.1 在 `Executing` / `Executed` 期间清零并丢弃 Move、Sprint、Jump、Attack、Dodge、Guard、Parry 和 LockOn 切换，确保结束后不消费缓存输入。
- [x] 6.2 扩展 `PlayerCameraRigController`，处决开始时暂停锁定相机并进入仍跟随本地角色的可旋转模式，持续读取 `Look` 而不改变角色 Transform。
- [x] 6.3 处决者动画完成后恢复普通 FreeLook 配置；本地被处决者在动画结束以处决死亡变体进入 Dead；不得恢复已经死亡目标的锁定。
- [ ] 6.4 验证 Offline 中处决双方的角色输入完全锁定，同时鼠标 Look 可连续旋转镜头且不会干扰 Warp、固定目标或动画。2026-09-17 Offline Runner 已验证处决中的目标和处决者 `ForceEnterGuard` 均被拒绝且状态保持 `Executed`/`Executing`；鼠标 Look 连续旋转镜头尚未在带完整镜头 rig 的 Offline/Online 场景专项验证，因此本项不勾选。

## 7. 实现 Server 权威同步与乱序保护

- [x] 7.1 向网络协议末尾追加处决请求、开始、结果和完成数据，携带请求序号、双方 ActorId、`executionId`、固定目标姿态、锚点、权威时间和 `DeathPresentationVariant` 且保持旧字段兼容。
- [x] 7.2 在 Mirror Server 校验请求连接所有权、目标存在、权威资格与并发预占，拒绝 Client 自定义锚点、伪造状态和代表其他 Player 的请求。
- [x] 7.3 由 Server 广播唯一处决会话并驱动双方状态、处决者 Warp 后绝对 Transform、战斗抑制及目标致死 HP/revision；Offline 复用相同服务接口。Server 现在广播 Start、Result 及累计 Complete 边沿，绝对状态继续通过既有快照发布。
- [x] 7.4 在所属 Client 与远端观察者锁存 `Executing` / `Executed` 和处决死亡变体，按权威开始时间追赶动画，并使旧 Attack、Parried、PostureBroken、普通 Dead 与移动快照不能恢复或覆盖动作和无敌/攻击抑制。代码已接入权威时间 normalized time、Start/Result/Complete 去重及完成边沿释放；2026-09-13 的 Host/Client 验收已确认双端处决表现和结果正常。
- [x] 7.5 按 `executionId` 和请求序号处理 Host 回环、重复开始/结果/完成、完成先到与迟到开始，保证动画、Warp、时长和致死结果只应用一次。Complete 使用累计 Flags 去重，并在 Start 迟到时重放缓存边沿；运行时乱序验收仍待完成。
- [ ] 7.6 为晚加入或会话中重绑定观察者应用当前绝对会话、固定目标姿态、处决者位置、结果状态和死亡表现变体，不从 Animator 当前帧反推玩法。已接入 `ExecutionStateRequestMsg`/`ExecutionStateMsg`、短期终态缓存、绝对 HP/revision 与本地终态恢复；代码已具备，仍待晚加入/重绑定专项运行验收。

## 8. 制作 Motion Warping 可视化编辑器

- [ ] 8.1 抽取运行时与编辑器共享、不依赖 `MonoBehaviour` 的纯 Warp 采样/轨迹计算核心，统一窗口累计权重、位置/Yaw 修正和残差语义。
- [ ] 8.2 创建 Editor-only `EditorWindow`，支持选择正式战斗/表现配置、预览 Animator/Avatar 和 `rig_Execute` / `rig_Executed` / `rig_Executed_Death` 动画资源。
- [ ] 8.3 实现带 Warp Window 色块的时间轴、`executionWarpCurve` 编辑和动画时间 Scrub，并即时刷新原始及修正姿态。
- [ ] 8.4 实现 Scene View 处决者锚点 Handle、原始/修正 Root Motion 轨迹、位置/Yaw 残差及缺失资源、非法窗口、非单调曲线和预算超限诊断。
- [ ] 8.5 使用 `SerializedObject` / `SerializedProperty`、Undo/Redo、Dirty/Save 写回正式配置，并在隔离 `PreviewScene` 中创建和确定性清理临时预览对象。
- [ ] 8.6 增加 EditMode 测试，验证编辑器 Undo/Redo、资产持久化、非法配置诊断、预览对象清理，以及相同采样输入下编辑器预测与运行时 Warp 输出一致。

## 9. 增加测试、验收与文档证据

- [ ] 9.1 增加 EditMode 测试覆盖正面角、距离/高度、确定性候选排序、Warp 累计权重/边界、配置钳制、会话预占、无敌多令牌/重复释放、攻击抑制隔离和死亡变体选择。
- [ ] 9.2 增加可行的 PlayMode 或调试 harness，覆盖 Parried/PostureBroken 进入、Idle/Move 左键优先级、Guard 禁止处决、固定目标、Warp 收敛、第三方命中抑制、结果时刻、双方独立动画结束和 `rig_Executed_Death`。2026-09-17 使用 MCP 注入的临时运行时 Runner 覆盖了 Parried/PostureBroken 入口、Guard 强制输入拒绝、HurtBox/无敌/攻击抑制、结果单次提交、非致死/致死分支和 `rig_Executed_Death` 死亡变体，但未落地为项目 PlayMode/EditMode 自动化测试，且 Warp 收敛未通过。
- [ ] 9.3 运行 Offline Player 对 NPC 与 Player 目标的正面/背面、距离/高度、墙体、锁定优先、Guard/无候选普通攻击、无敌/HurtBox、处决死亡变体、镜头和异常清理验收，并在 Motion Warping 编辑器中验证锚点、轨迹、残差与运行时结果一致。2026-09-17 仅完成 Offline Player 对 Player 的正面运行时子集：资格通过、非致死 HP `100 -> 50`、致死 HP 到 0、死亡变体为 `Executed`、HurtBox/无敌/攻击抑制通过、Arena collider 均启用；NPC、背面/距离/高度/墙体拒绝、锁定优先、无候选普通攻击、镜头、异常清理和 Motion Warping 编辑器一致性仍未覆盖。
- [ ] 9.4 运行 Host/Client 双向 Player 处决、Player 处决 Server NPC、并发目标、迟到/重复/乱序消息、死亡变体、绝对 Transform 和 HP/revision 收敛验收。
- [ ] 9.5 通过 Unity MCP 退出 Play Mode、重新编译并检查 Console/编译错误，运行项目测试、`git diff --check` 和 `openspec validate add-execution-motion-warping --strict`。2026-09-14 已确认 Host/Client MCP 均可用、Offline 场景不在 Play Mode、无 Unity 编译错误或 Console Error，且严格校验与 diff 检查通过。2026-09-17 再次确认 4399 与 8765 Editor 均退出 Play Mode、未编译；`openspec validate add-execution-motion-warping --strict` 通过，`git diff --check` 通过但有既有 CRLF warning；仍缺项目测试、重新编译后的完整 Console 清洁记录、执行系统自动化测试和完整回归记录。
- [ ] 9.6 更新 `docs/REQUIREMENTS.md`、`docs/ROADMAP_AND_PROGRESS.md`、`docs/VERIFICATION_MATRIX.md` 及相关架构说明，纳入 Guard 禁止处决、可复用无敌、处决死亡变体和 Motion Warping 编辑器，只把取得运行证据的环境标为 `Verified`。
- [ ] 9.7 审计变更范围与 Unity 资产保存路径，确认未手工编辑 `.unity`、`.prefab`、`.asset`，且既有无关工作树变更保持独立可追溯。
