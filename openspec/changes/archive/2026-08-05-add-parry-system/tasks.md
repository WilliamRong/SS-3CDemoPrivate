## 1. 配置、输入与稳定标识

- [ ] 1.1 在 `CharacterCombatConfig` 与默认战斗资产中增加 60 FPS、开始帧 8、结束帧 22、总帧 34、`Parried` 时长 1.0666667 秒的配置、秒值查询和有序性校验。
- [ ] 1.2 在 `CharacterPresentationConfig` 与默认表现资产中增加 `Parry`、`Parried` 的 CrossFade 参数，保持现有 Idle/Guard 转身配置不变。
- [ ] 1.3 只在枚举末尾追加 `CharacterStateId.Parry/Parried`、转换原因、`CombatReactionType.Parry`、`ActionType.ParryStart/Parried` 和弹反阶段枚举，并更新调试字符串。
- [ ] 1.4 在 `DemoInputActions.inputactions` 增加绑定 `<Keyboard>/q` 的 `Parry` Button，重新生成 `DemoInputActions.cs`，在 `InputHandler` 锁存/清理单帧脉冲，并向 `CharacterIntent` 增加 `IsParryPressed`。

## 2. Player 与 NPC 状态机

- [ ] 2.1 实现 Player `ParryState` 的 `Startup/Active/Recovery` 逻辑计时、阶段查询、水平移动封锁、输入忽略和自然返回 `Idle`。
- [ ] 2.2 把 Player `ParryState` 注册到 `PlayerController`，从 `Idle/Move/Sprint/Guard` 以弹反优先级进入，并在转换图中只允许权威 `Hit/PostureBroken/Dead` 抢占。
- [ ] 2.3 实现 Player `ParriedState` 和 `TryEnterParried` 权威入口，阻断输入、取消运动、支持权威反应打断，并保证 Server 上无本地输入的远端 Player 仍持续计时。
- [x] 2.4 实现 `NpcParriedState`，在 `NpcCharacterDriver` 注册并提供 Server-only 进入入口，进入时停止攻击/NavMesh 动作且自然结束前忽略 AI 意图。
- [ ] 2.5 为 Player/NPC 控制器增加活跃 `Parry/Parried` 查询，并更新锁定战斗状态、闪避准备和 Root Motion 白名单，确认两个新状态均保持原地且不消费动画 Root Motion。

## 3. 权威命中与攻击取消

- [ ] 3.1 在 `CombatActor` 增加从本地 Player 状态或远端快照解析当前 `ParryPhase` 的查询，并确保只有 `Active` 返回可弹反。
- [ ] 3.2 在 `CombatActor.ApplyHit` 的 Guard、HP 和架势逻辑之前处理成功弹反，写入明确的零伤害/零架势 `WasParried` 结果，并保持前摇与后摇沿用普通命中事务。
- [ ] 3.3 在 `CombatActor` 增加 Player/NPC 攻击方进入 `Parried` 的统一路由和当前攻击立即停止接口，且不得重置架势或发布破势 UI 语义。
- [ ] 3.4 在 `CombatResolver` 跟踪已取消的 `ActorId + attackInstanceId`，成功弹反后中止同帧剩余 HurtBox/窗口/HitBox 候选并拒绝后续帧继续结算。
- [ ] 3.5 回归验证弹反分支不调用 Guard 角度公式，成功时正面/侧面/背面、轻击/重击均不改变防守方 HP 与架势，窗口外仍保留方向 Hit、GuardBreak、PostureBreak 和 Dead 优先级。

## 4. Animator 与表现管线

- [ ] 4.1 在 `CharacterAnimator.controller` 的 Combat Layer 增加引用 `rig_Parry` 的 `Parry` 状态，在 Reaction Layer 增加独立引用 `rig_Collide` 的 `Parried` 状态，保留现有 `PostureBroken` 状态不变。
- [ ] 4.2 在 `AnimatorParams` 和 `CharacterCombatPresenter` 增加独立 Hash、层权重清理、CrossFade 和缓存重启规则，使 `Parry` 清理 Guard/Reaction 残留、`Parried` 抑制 Attack/Dodge/Guard/locomotion。
- [ ] 4.3 在 `CharacterLateUpdatePipeline` 将 `Parry/Parried` 纳入本地、Server NPC 和远端表现状态分类、状态进入版本与离开战斗层清理流程。
- [ ] 4.4 扩展角色状态/同步诊断，显示 `ParryPhase`、`Parried`、攻击实例取消和最终 `WasParried` 结果，便于验证第 7/8/21/22 帧边界。

## 5. Snapshot、Action 与 Mirror 权威收敛

- [ ] 5.1 向 `StateSnapshot` 末尾追加 `ParryPhase` 及清洗查询，在 `LocalSyncPublisher` 按状态/阶段解析并让 `Startup -> Active -> Recovery` 边沿立即触发快照。
- [ ] 5.2 向 Mirror `SnapshotMsg` 末尾追加 `ParryPhase` 并更新双向转换；Server 继续校验 Actor 所有权，拒绝非法阶段且仅在 `StateId == Parry` 时保留有效值。
- [ ] 5.3 更新状态动作映射和发布器以传播 `ParryStart` 表现边沿；Mirror Server 必须拒绝非所属 Actor 动作和所有 Client 上报的 `Parried`。
- [ ] 5.4 让 `CombatResolver` 在成功弹反时只由 Server 广播以攻击方为 `ActorId` 的 `Parried` 权威动作，Offline 模式直接应用状态且不依赖网络消息。
- [ ] 5.5 在 `RemoteActionApplier` 实现 `Parried` 动作/快照锁存、序列去重、所属本地 Player 强制进入和 Host 回环保护，并为远端表现提供稳定的进入版本。
- [ ] 5.6 在 Mirror Server 快照中用 Server FSM 的 `Parried` 覆盖迟到的 Client Attack/速度/移动输入，Server 状态结束后拒绝 Client 伪造或延长 `Parried`。
- [ ] 5.7 更新 `CharacterLateUpdatePipeline`、`CombatActor` 和远端快照解析的状态优先级，确认权威 `Parried` 锁存期间旧 Attack 快照不能恢复攻击判定或动画。

## 6. 静态检查与 Offline 验证

- [ ] 6.1 运行 `dotnet build Assembly-CSharp.csproj --no-restore -m:1`、`git diff --check` 和 `openspec validate add-parry-system --strict`，解决本 change 引入的错误且不改写现有无关工作树变更。
- [ ] 6.2 在 Offline 场景逐一验证第 7、8、21、22 帧附近命中，记录 `[8,22)` 的成功/失败边界、状态阶段、HP、架势和最终反应。
- [ ] 6.3 在 Offline 场景验证前、后、左、右与背后攻击，以及轻击、重击的 360 度成功结果；再验证前摇/后摇按普通方向受击结算。
- [ ] 6.4 分别让 Player 与 NPC 成为攻击方，验证 `rig_Collide`、1.067 秒控制锁定、权威反应打断、架势不重置和无破势 UI 强调。
- [ ] 6.5 构造持续重叠和同帧多候选，验证成功弹反后整个攻击实例停止结算，成功前已经提交的其他目标结果不回滚。
- [ ] 6.6 回归 Idle/Guard 原地转身、普通 Guard 扇区、方向 Hit、PostureBroken、Dead、攻击连段和闪避无敌窗口。

## 7. Host/Client 验证与项目文档

- [ ] 7.1 验证 Host Player 弹反 Client Player、Client Player 弹反 Host Player、两端 Player 弹反 Server NPC，以及 NPC 弹反结果在所属端和观察端一致。
- [ ] 7.2 使用可控延迟/乱序验证 `Parried` 动作先到、快照先到、迟到 Attack 快照和重复回环，确认硬直不丢失、不延长、不重复播放且攻击不恢复。
- [ ] 7.3 验证非 Server Client 不能自行产生成功结果、不能替其他 Actor 发布弹反状态、不能上报 `Parried`，并记录 Server 最终 HP/架势与状态证据。
- [ ] 7.4 更新 `docs/REQUIREMENTS.md` 的弹反能力、`docs/ROADMAP_AND_PROGRESS.md` 的实现状态和 `docs/VERIFICATION_MATRIX.md` 的 Offline/Host/Client 用例；没有运行证据的网络项保持待验。
- [ ] 7.5 重新运行 OpenSpec 严格校验并记录最终构建、场景、角色组合、观察结果和残余限制，确认所有 apply 任务完成后再进入归档流程。
