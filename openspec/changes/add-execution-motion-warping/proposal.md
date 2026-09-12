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
- Mirror 已接入 `ExecutionRequestMsg`、`ExecutionStartMsg` 和 `ExecutionResultMsg`，包括连接所有权校验、Server 资格重算、Start/Result 广播以及远端表现锁存。
- `ExecutionCompleteMsg` 已注册 Handler、由生命周期服务广播并接入 `SyncBootstrap` / `RemoteActionApplier`；远端按执行者/目标完成位释放抑制，并缓存 Complete 或 Result 先于 Start 的乱序情况。动画按权威开始时间追赶已接入；`ExecutionStateRequestMsg` / `ExecutionStateMsg` 提供活跃会话、短期终态、固定姿态和绝对 HP/revision 的晚加入/重绑定恢复。上述路径仍待 Unity Host/Client 运行验收。
- `CharacterPresentationConfig` 已声明处决 CrossFade 与 clip 校准字段，但 `DefaultPresentation.asset` 尚未持久化这些字段；`DefaultCombat.asset` 的处决数值也仍需 Offline 动画校准。
- 独立执行 `dotnet build Assembly-CSharp.csproj --no-restore -m:1` 时，生成的 `.csproj` 仍引用 9 个已迁移的 `Assets/Scripts/AI/*.cs` 旧路径并产生 `CS2001`；实际文件位于 `Assets/Scripts/AI/NpcStates/` 与 `Assets/Scripts/AI/BT/`，因此该命令当前不能作为零编译错误证据。
- Scene Gizmo、Motion Warping 可视化编辑器、自动化测试以及 Offline/Host/Client 验收尚未完成。当前没有足够运行证据将这些环境标记为 `Verified`，因此本 change 暂不归档。

## 最新修正：处决伤害决定表现分支

接受处决后只应用一次配置的 `executionDamage`，不默认视为致死。目标根据该结果锁存动画分支：存活目标播放 `rig_Executed`，并在配置时长结束后返回 `Idle`；致死目标播放 `rig_Executed_Death`，仅在该分支完成后进入 `Dead`。进入 `Dead` 时不得再次播放同一处决死亡动画。
