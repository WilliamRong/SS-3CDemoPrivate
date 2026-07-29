## 背景

当前战斗链路已经具备架势系统所需的大部分集成点：

- `CombatResolver` 只在配置的攻击窗口内生成有效 `HitInfo`，在线命中只由 Server 结算。
- `CombatActor` 持有 NPC HP、把 Player HP 委托给 `PlayerController`，并判断格挡扇区、伤害倍率以及 Hit/Dead/Guard 反应。
- Player 与 NPC 使用平行 FSM 实现，但共用 `CharacterStateId`、转换规则、快照/动作协议和表现管线。
- `StateSnapshot` 携带连续表现状态，`ActionEvent` 携带离散反应和即时权威战斗结果。
- `PlayerHealthHudView` 负责本地 Player HUD，`NpcHealthBarView` 负责非本地 Player/NPC 唯一世界 UI；`LockOnTargetHudView` 只控制目标世界 UI 的可见性。

NPC HP 已经使用绝对值和 revision 进行快照纠正，但 Player 尚未接入同类周期 HP 快照。架势不能复制这种角色不对称：从第一版开始，Player 和 NPC 都应把绝对架势作为连续快照状态，离散事件只负责破势边沿。

## 目标 / 非目标

**目标：**

- 为每个 Player 和 NPC 增加初始为零、可配置的共享架势资源。
- 未格挡有效命中和成功格挡都增加防守方架势。
- 架势随时间恢复，恢复速率随 HP 比例增加。
- 存活角色达到最大架势时强制进入明确的破势逻辑状态，并且只重置一次。
- 在线架势增长、恢复、重置和状态转换全部由 Server 权威处理。
- 同步绝对架势，使远端 Player/NPC UI 可以纠正漂移和延迟绑定。
- 在本地 Player HUD 和唯一世界血条的健康条下显示独立黄色架势条。
- 适当复用现有 GuardBreak 动画，同时保持动画与逻辑状态分离。

**非目标：**

- 增加处决、忍杀、弹反时机、攻击方架势伤害、耐力消耗或非攻击来源的架势伤害。
- 除架势优先级集成所需内容外，重平衡 HP 伤害、格挡扇区、闪避无敌或现有重击破防规则。
- 在本 change 中补齐 Player 的通用周期 HP 快照纠正。
- 根据 Guard、Idle、Attack 或移动状态增加额外恢复奖励；本版本唯一恢复倍率来自 HP 比例。
- 增加动画/VFX/SFX 资产；初版破势视觉复用现有 GuardBreak 表现。

## 设计决策

### CombatActor 统一持有 Player 与 NPC 架势

增加由 `CombatActor` 持有的小型 `PostureRuntime` 值/服务，包含 current/max、最近增长时间、钳制修改、重置和恢复计算。无论 actor 使用 `PlayerController` 还是 `NpcCharacterDriver`，`CombatActor` 都公开 `CurrentPosture`、`MaxPosture`、`PostureRatio` 和 `PostureChanged`。

这可以避免把现有分裂的 HP 所有权复制成两套架势实现。Player 的 HP 比例从 `PlayerController` 读取，NPC 的 HP 比例从 `CombatActor` 健康状态读取。

备选方案：Player 架势存入 `CharacterContext`，NPC 架势存入 `CombatActor`。拒绝原因：UI、网络和战斗结算都需要按 actor 类型分支，行为容易漂移。

### 攻击定义携带两种架势伤害

扩展 `AttackDefinition`：

- `postureDamage`：未格挡有效命中增加的架势。
- `guardedPostureDamage`：同一攻击被成功格挡时增加的架势。

两者均为非负数，并独立于 HurtBox HP 倍率。为 `DefaultAttackSet` 每项攻击配置默认值，较重攻击产生更高架势压力。

备选方案：从最终 HP 伤害直接推导架势。拒绝原因：完全格挡可以造成零 HP 伤害但仍应积累架势，而且身体部位 HP 倍率变化不应连带改变架势平衡。

### 恢复确定且受 HP 比例影响

在 `CharacterCombatConfig` 增加：

- `maxPosture`
- `postureRecoveryDelay`
- `postureRecoveryPerSecondAtLowHealth`
- `postureRecoveryPerSecondAtFullHealth`
- `postureBreakDuration`

距离最近一次架势增长经过配置延迟后，权威端按以下公式降低架势：

```text
healthRatio = clamp(currentHp / maxHp, 0, 1)
recoveryRate = lerp(lowHealthRate, fullHealthRate, healthRatio)
currentPosture = max(0, currentPosture - recoveryRate * deltaTime)
```

配置校验强制 `fullHealthRate >= lowHealthRate >= 0`，保证 HP 更高时恢复不会更慢。恢复延迟可以调参，但不改变架势随时间下降的基本规则。

除 `PostureBroken` 外，任意存活状态都使用同一恢复规则，与移动、攻击或格挡无关。新的架势增长会重新开始延迟。Dead actor 把架势重置为零且不运行恢复。

备选方案：使用按 HP 比例采样的 `AnimationCurve`。第一版拒绝，因为曲线可能意外违反单调恢复，也更难做网络验证。

### 破势使用独立逻辑状态

把 `PostureBroken` 追加到 `CharacterStateId`，不重排现有数值。增加 `PostureBrokenState` 和 `NpcPostureBrokenState`，完成注册并加入转换/打断规则。

当一次战斗结算使架势达到最大值：

1. 应用该结算的 HP 伤害。
2. 如果 HP 到零，进入 `Dead`；死亡优先并重置架势。
3. 否则把架势重置为零并强制进入 `PostureBroken`。
4. 抑制本次结算原本的 Hit 或 GuardHit 反应。
5. 在 `postureBreakDuration` 内阻止移动和普通动作，结束后返回 Idle（Player 可返回有效移动状态）。

`PostureBroken` 可以被 `Dead` 打断。破势期间后续有效攻击仍可造成 HP 伤害，但直到退出破势前不再增加架势，避免刚重置就立刻再次填满。

优先级：

```text
Dead > PostureBroken > Hit / GuardBreak / GuardHit
```

初版复用 Animator GuardBreak 状态/hash 作为全身表现，但逻辑不依赖动画结束。

备选方案：把 `HitState` 配置成重击受击。拒绝原因：破势需要独立持续时间、优先级、同步身份、调试状态和未来扩展点。

### 健康、架势和反应作为单一战斗事务

重构 `CombatActor.ApplyHit` 分支，使有效命中计算单一结果，其中包含实际 HP 伤害、实际架势伤害、格挡结果、死亡、破势和表现反应。不得先发 GuardHit，再在下一帧被 PostureBroken 覆盖。

架势增长规则：

| 结算 | HP 行为 | 架势行为 |
|---|---|---|
| 被拒绝/无敌命中 | 不变 | 不变 |
| 未格挡有效命中 | 现有 HP 伤害 | `postureDamage` |
| 成功格挡 | 现有格挡倍率 | `guardedPostureDamage` |
| 现有配置重击破防 | 现有破防 HP 行为 | 使用 `guardedPostureDamage`；达到阈值可升级为 `PostureBroken` |
| 致死命中 | HP 到零 | 重置为零，Dead 优先 |

架势沿用 `CombatResolver.HitKey` 的重复命中过滤，每次只应用一次。

### 快照同步绝对架势，事件同步破势边沿

扩展 `StateSnapshot`，携带绝对 current/max posture（或显式版本化的等价字段）。Player 与 NPC 权威 publisher 都填充这两个值。远端通过 `CombatActor` 方法应用，只有数值变化时才触发 `PostureChanged`。

增加 `ActionType.PostureBreak`，保留离散破势边沿和即时表现。状态快照仍是纠正源，Client 不能只根据本地 UI 值进入逻辑状态或重置架势。

Host 遵循现有战斗规则：Server 只应用一次事务，Host client 侧不得从自身广播再次增加架势。

备选方案：只在 `ActionEvent` 中发送架势伤害增量。拒绝原因：事件丢失、延迟绑定和远端 UI 会产生状态漂移。

### UI 绑定 CombatActor 架势事件

扩展两类维护中的健康表面：

- `InGameHud`：在本地 Player 健康条下方增加架势条。
- `NpcHealthBar`：在非本地 Player/NPC 世界健康条下方增加架势条。

锁定目标继续复用同一个 `NpcHealthBar` 世界 UI，`LockOnTargetHudView` 只控制其持续可见，不创建独立架势条。架势条使用不同的黄色填充、稳定尺寸，并绑定目标 `CombatActor.PostureChanged(current, max)`。填充比例钳制到 `[0, 1]`；父健康 UI 可见时，即使架势为零也保留轨道，避免战斗中布局出现/消失。

UI 只读取架势状态，绝不修改。预制体引用优先序列化，并沿用当前健康 view 的路径 fallback 约定。

### 文档和诊断属于完成范围

更新：

- `docs/REQUIREMENTS.md`：架势资源、破势、同步和 UI 需求。
- `docs/ROADMAP_AND_PROGRESS.md`：active change 和真实实现状态。
- `docs/VERIFICATION_MATRIX.md`：Offline/Host/Client Player/NPC 架势用例。
- 状态调试 overlay 或 GM 诊断：显示 current/max posture，并支持确定性强制增加/重置/破势，但调试命令不成为生产权威逻辑。

## 风险 / 权衡

- [风险] 健康、架势和格挡分支产生互相矛盾的反应。-> 缓解：计算单一结果，并在发布前强制 `Dead > PostureBroken > Hit/Guard`。
- [风险] Client 架势 UI 漂移。-> 缓解：Player/NPC 都快照同步绝对 current/max，并让远端 UI 绑定快照应用后的 `CombatActor`。
- [风险] Host 重复应用架势。-> 缓解：复用 Server 权威检查，`NetworkServer.active` 时显式跳过 client 侧修改。
- [风险] 新 `CharacterStateId` 破坏序列化或网络值。-> 缓解：只追加枚举，不重排，并完整更新映射与调试 switch。
- [风险] 恢复调参让压力无意义或永久存在。-> 缓解：所有速率数据化、强制单调边界，并对低/中/满 HP 增加确定性恢复测试。
- [风险] GuardBreak 动画不适合未格挡角色。-> 缓解：把它当作初始表现映射，未来可替换专用资产而不改变逻辑。
- [风险] 现有重击破防与架势破势概念混淆。-> 缓解：保留不同结算原因，只有架势阈值溢出才进入 `PostureBroken` 并重置。

## 迁移计划

1. 增加架势配置字段和攻击架势值，默认值不改变现有 HP 行为。
2. 增加并测试 `PostureRuntime`，通过 `CombatActor` 暴露状态/事件，暂不接入增长。
3. 追加状态 ID、Player/NPC 破势状态、转换规则和表现映射。
4. 把原子架势增长/破势选择集成到命中与格挡结算。
5. 扩展快照、publisher、远端应用、动作映射和调试 overlay。
6. 在两类健康 UI 增加黄色架势条，并绑定 `CombatActor`；锁定复用世界 UI。
7. 先执行 Offline，再执行 Host/Client，验证无二次应用和 Player/NPC 绝对值一致。
8. 更新项目文档和验证证据。

回滚时移除架势字段、状态和 UI，恢复先前战斗反应选择。由于新状态只追加且架势默认值为零，现有枚举数值和 HP 数据保持兼容。

## 待决定问题

- 默认最大值、每种攻击架势值、恢复延迟/速率和破势持续时间属于调参；实现提供保守序列化默认值并保持可调整。
- 后续 change 可以加入弹反导致攻击方架势受损，但本提案不预留或模拟该规则。
