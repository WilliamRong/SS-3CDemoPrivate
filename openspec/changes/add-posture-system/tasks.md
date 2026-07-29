## 1. 架势数据与运行时

- [ ] 1.1 在 `CharacterCombatConfig` 增加最大架势、恢复延迟、低/满 HP 恢复速率和破势持续时间，校验非负单调约束，并填写 `DefaultCombat.asset` 默认值。
- [ ] 1.2 在 `AttackDefinition` 增加独立的普通命中与格挡架势伤害字段，更新 fallback 构造，并为 `DefaultAttackSet.asset` 每项攻击配置架势值。
- [ ] 1.3 实现带钳制、最近增长时间、HP 比例恢复、重置和远端绝对状态应用的 `PostureRuntime`。
- [ ] 1.4 让 `CombatActor` 统一持有 Player/NPC 架势，公开 current/max/ratio 与 `PostureChanged`，只在 Offline/Server 权威端恢复，并在死亡/复活时重置。
- [ ] 1.5 扩展状态/调试诊断和权威安全的 GM/调试控制，使 current/max、恢复、增加/重置和破势行为可确定性检查。

## 2. 破势逻辑与表现

- [ ] 2.1 在不改变现有数值的前提下把 `PostureBroken` 追加到 `CharacterStateId`，并完整更新状态/动作映射、调试字符串和转换规则。
- [ ] 2.2 实现并注册 Player `PostureBrokenState`，包括移动/动作阻断、超时退出、死亡打断和 Controller 入口/查询接口。
- [ ] 2.3 实现并注册 NPC `NpcPostureBrokenState`，包括 Motor 停止、意图阻断、超时退出、死亡打断和 Driver 入口/查询接口。
- [ ] 2.4 通过 `CharacterLateUpdatePipeline` 和 `CharacterCombatPresenter` 路由本地/远端破势，复用现有全身 GuardBreak 动画并保持 Dead 最高优先级。

## 3. 原子战斗集成

- [ ] 3.1 将 `CombatActor.ApplyHit` 重构为单一战斗结果/事务，记录实际 HP、实际架势、格挡结果、死亡、破势和最终表现反应。
- [ ] 3.2 未格挡有效命中增加普通架势伤害，成功格挡增加格挡架势伤害，并复用现有命中窗口和重复命中过滤。
- [ ] 3.3 强制 `Dead > PostureBroken > Hit/GuardBreak/GuardHit`，进入破势时只重置一次，并抑制同一结算中的低优先级反应。
- [ ] 3.4 已处于 `PostureBroken` 时允许权威 HP 伤害但不增加架势，并验证 Player/NPC 死亡/复活生命周期。

## 4. 网络权威与同步

- [ ] 4.1 扩展 `StateSnapshot` 和 Mirror 序列化，携带绝对 current/max posture，同时保持现有字段和枚举兼容。
- [ ] 4.2 让 Player/NPC 权威 publisher 都填充架势，并把更新的远端快照应用到 `CombatActor`，为 UI 提供绝对纠正和延迟绑定状态。
- [ ] 4.3 增加并发布 `PostureBreak` 动作/状态边沿，更新远端去重和表现进入版本，并防止 Host 二次应用。
- [ ] 4.4 验证非 Server Client 不运行架势增长/恢复/破势逻辑，Offline 模式保留本地权威。

## 5. 架势 UI

- [ ] 5.1 扩展 `PlayerHealthHudView` 和 `NpcHealthBarView`，绑定/解绑 `CombatActor.PostureChanged` 并渲染钳制后的架势比例，不得修改玩法状态。
- [ ] 5.2 在 `InGameHud.prefab` 和 `NpcHealthBar.prefab` 的健康条下增加稳定黄色架势轨道/填充，保持现有布局尺寸和可见性行为。
- [ ] 5.3 接线 Player/NPC prefabs 与场景 HUD，验证零架势时保留空轨道，切换锁定目标时同一个世界 UI 立即显示新目标当前架势。

## 6. 验证与文档

- [ ] 6.1 使用项目可行的 EditMode/PlayMode 测试结构或调试 harness，为钳制、恢复延迟、低/中/满 HP 速率、阈值优先级和重复防护增加确定性覆盖。
- [ ] 6.2 构建 `Assembly-CSharp.csproj`，并执行 Offline Player/NPC 普通命中增长、格挡增长、多 HP 比例恢复、致死优先级、强制破势、超时和两类架势 UI 检查。
- [ ] 6.3 执行 Host/Client Player/NPC 绝对架势一致性、仅 Server 修改、Host 不重复增长、远端破势表现、恢复、延迟绑定和锁定目标世界 UI 切换检查。
- [ ] 6.4 更新 `docs/REQUIREMENTS.md`、`docs/ROADMAP_AND_PROGRESS.md`、`docs/VERIFICATION_MATRIX.md` 及架构/UI 说明，记录架势需求、真实状态和取得的证据。
- [ ] 6.5 运行链接/diff 检查和 `openspec validate add-posture-system`，确认只有范围内的运行时、序列化资产、测试/调试、文档和 OpenSpec 文件发生变化。
