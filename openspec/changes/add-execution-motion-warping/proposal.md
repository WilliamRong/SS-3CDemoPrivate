## Why

现有弹反与架势崩防只能产生短暂硬直，玩家无法把近距离、正面对位优势转化为明确的处决结果。项目已经具备配对的 `rig_Execute` / `rig_Executed` 动画和 Root Motion 管线，适合增加由 Motion Warping 驱动、位置稳定且可在 Offline 与网络权威模式下验证的成对处决能力。

## What Changes

- 允许 Player 对仍处于 `Parried` 或 `PostureBroken` 的存活 Player/NPC 发起致死处决。
- 将现有左键 Attack 输入复用为处决输入：仅 `Idle` / `Move` Player 会在普通攻击前解析处决；`Guard` 时禁止处决并保持既有普通攻击行为。
- 要求处决者位于目标正面、水平距离约半个身位以内，并通过配置化距离、角度、高度差、视线和路径检查确定资格。
- 新增成对的 `Executing` / `Executed` 逻辑状态，分别播放 `rig_Execute` 与 `rig_Executed`，并以同一权威处决会话绑定双方；目标从处决进入 `Dead` 时使用 `rig_Executed_Death`，普通死亡继续使用既有死亡动画。
- 固定被处决者的世界位置与朝向，只对处决者相对目标生成的锚点执行有限窗口内的位置和旋转 Motion Warping。
- 在处决运行时与网络闭环完成后增加 Unity Motion Warping 可视化编辑器，用于选择处决配置和动画、编辑处决者锚点偏移、Warp Window/曲线/预算，并通过时间 Scrub 对比原始 Root Motion、修正后预测轨迹及位置/Yaw 残差。
- 抽离可由闪避、处决和未来技能复用的可叠加无敌语义；当前表现统一关闭 `CombatHurtBox` 并由 `CanReceiveHit` 提供权威兜底。处决双方各自持有无敌，同时独立抑制普通 HitBox/攻击，保留环境碰撞和本地镜头旋转。
- 由 Server 校验并提交在线处决资格、会话和致死结果；Offline 模式执行相同的本地权威事务，并同步远端配对表现与绝对结果。
- 增加处决锚点、Warp 误差、会话、碰撞抑制和结果诊断，以及 Offline、Host、Client、Player 与 NPC 验证用例。

## Capabilities

### New Capabilities

- `execution-system`：定义处决资格、目标选择、成对状态、可复用无敌、Motion Warping、处决死亡变体、控制锁定、战斗抑制、致死结果、网络权威和验证要求。

### Modified Capabilities

- `parry-system`：使仍处于 `Parried` 的角色成为合法处决目标，并允许权威处决状态抢占其自然超时。
- `posture-system`：使仍处于 `PostureBroken` 的角色成为合法处决目标，并允许权威处决状态抢占其自然超时和普通受击路径。

## Impact

- 输入与意图：复用 `DemoInputActions` 的左键 Attack 脉冲，仅在 `Idle` / `Move` 的普通攻击转换前增加处决目标解析和权威请求。
- 状态机与控制器：Player 增加 `Executing`，Player/NPC 增加 `Executed`、会话锁定、进入/退出和带来源的死亡表现规则。
- 战斗：`CombatActor`、`CombatResolver`、攻击实例与 HitBox/HurtBox 参与规则增加可复用无敌、处决攻击抑制和独立致死事务。
- 移动与表现：现有 Animator Root Motion relay/Motor 增加只作用于处决者的目标锚点 Warp，Animator Controller 和 Presenter 接入 `rig_Execute`、`rig_Executed` 与 `rig_Executed_Death`。
- 镜头与锁定：处决期间阻断角色动作和锁定切换，但保留本地 `Look` 输入及可旋转镜头。
- 网络：处决请求、双方 ActorId、会话序号、锚点、开始时间、状态边沿、死亡表现变体和最终绝对生命结果需要 Server 权威同步与去重。
- 数据与文档：增加处决距离、角度、高度差、Warp 窗口/误差、三段动画时长/过渡等配置，并更新需求、进度与验证矩阵。
- 编辑器工具：新增 Editor-only 的 Motion Warping 配置、预览和诊断窗口，通过正式配置资产写回数据，并与运行时共享 Warp 计算语义；编辑器不参与权威资格、会话、位移或致死判定。
- 不增加第三方包依赖，不要求 Animation Rigging；第一版 Motion Warping 复用现有 Root Motion 消费管线实现，编辑器工具不增加运行时依赖。

## 当前实现状态

- Offline/Server 共用的候选解析、资格校验、处决会话、成对状态、配置伤害分支、Motion Warping、无敌/攻击抑制、配对动画、输入锁定和处决镜头已有代码实现。
- Mirror 已接入 `ExecutionRequestMsg`、`ExecutionStartMsg`、`ExecutionResultMsg`、`ExecutionCompleteMsg` 以及 `ExecutionStateRequestMsg` / `ExecutionStateMsg`，包括连接所有权校验、Server 资格重算、绝对 HP/revision、Start/Result/Complete 广播、乱序去重和终态恢复。
- `DefaultPresentation.asset` 已持久化 `Executing` / `Executed` / `ExecutedDeath` 的 CrossFade 与 clip 校准字段；`DefaultCombat.asset` 已包含距离、角度、Warp 预算、曲线、伤害和时长配置，最终接触残差仍需 Offline 校准记录。
- `ExecutionSpatialGizmo` 已绘制正面扇区、最大距离、处决锚点、Warp 平移预算和位置/Yaw 误差；`CharacterStateDebugOverlay` 已提供候选拒绝码、空间条件、Warp 和抑制状态诊断；`ExecutionOfflineCalibrationHarness` 已提供评估、启动、取消和残差采集入口。
- 2026-09-13 的 Host/Client 验收确认双端处决、处决死亡表现、NPC 目标和重复消息保护正常。2026-09-14 通过 Host/Client MCP 复核：编辑器可连接、当前不在 Play Mode、无 Unity 编译错误或 Console Error；`openspec validate add-execution-motion-warping --strict` 与 `git diff --check` 通过。
- 2026-09-17 通过 4399 MCP 在 `Assets/Scenes/Offline.unity` 执行临时运行时验收：无敌多令牌、HurtBox 关闭/恢复、攻击抑制隔离、非致死/致死伤害单次提交、处决死亡变体、角色输入锁定和 Arena 碰撞体启用均取得证据；8765 与 4399 两个 Editor 最终均退出 Play Mode 且未编译。此次验收同时发现 Offline 校准仍未通过：`DefaultCombat.asset` 当前 `executionResultTime=0.01`，处决者位置残差约 `1.5414m`，与设计期望的约 `2.7s` 结果时刻和可接受接触残差不符。
- 当前任务清单为 `36/53`：核心运行时代码和已验收网络表现已完成；仍未完成的是 Offline 参数/碰撞/输入专项校准与证据、晚加入/重绑定专项验收、执行系统自动化测试、Motion Warping 可视化编辑器（8.1-8.6）以及完整需求/路线/验证文档更新（9.1-9.7 中的剩余项）。
- 本 change 仍不能归档，原因是 Motion Warping 编辑器和验证/文档证据尚未完成；OpenSpec 工件本身完整不等于实现任务全部完成。

## 最新修正：处决伤害决定表现分支

接受处决后只应用一次配置的 `executionDamage`，不默认视为致死。目标根据该结果锁存动画分支：存活目标播放 `rig_Executed`，并在配置时长结束后返回 `Idle`；致死目标播放 `rig_Executed_Death`，仅在该分支完成后进入 `Dead`。进入 `Dead` 时不得再次播放同一处决死亡动画。
