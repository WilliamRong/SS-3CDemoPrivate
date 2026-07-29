# SS&3CDemo 路线与进度

> 状态基线：2026-07-29  
> Git 基线：`faa84ceb49062ea7e9c4854f793e2ab71a5a9387`（`faa84ce 受击方向接入`，2026-07-24）  
> 同日未提交工作树按当前代码路径记为 `Implemented`，不作为运行时 `Verified` 证据。  
> 本文是当前进度的维护入口。需求定义见[能力需求](REQUIREMENTS.md)，具体验收见[验证矩阵](VERIFICATION_MATRIX.md)。

## Status Vocabulary

本文只使用以下状态，不使用模糊百分比：

| 状态 | 含义 | 所需证据 |
|---|---|---|
| `Implemented` | 代码/场景/Prefab/数据已存在，但不隐含运行时验收 | 当前路径、资产或提交 |
| `Verified` | 已在明确环境观察到符合验收条件的结果 | 环境、日期/记录和观察结果 |
| `Partial` | 只覆盖部分角色、环境或验收面 | 已有部分和明确缺口 |
| `Deferred` | 有意移出当前完成门槛 | 延期决定和后续归属 |
| `Not Started` | 未发现实质实现 | 需求存在但没有当前证据 |

阶段“结项”是项目管理决定；`Verified` 是证据等级。历史上结项的阶段，如果缺少可定位的当前环境记录，仍可在本基线中标为 `Implemented`。

## Evidence Rules

1. 当前第一方代码、场景、Prefab、数据资产和 Git 历史证明 `Implemented`。
2. 带环境和观察结果的记录证明 `Verified`。
3. 本文汇总前两者；不从旧计划直接推导完成状态。
4. `AIContext/` 只提供历史意图。较新的代码和验证记录优先。
5. 需求状态与实现状态分离：目标行为以[能力需求](REQUIREMENTS.md)为准。

## Current Summary

- Phase A-F 的开发主线已经落地，Phase F 有 Host/Client Player/NPC 动画一致性的历史验收记录。
- E0 可演示行为树被明确后置，不阻塞 Phase E 主线结项。
- G0-1 本地命中判定闭环已验证；G0-2/G0-3 已有实现，但最新完整联机一致性尚未验证。
- 本地玩家 HUD，以及非本地 Player/NPC 共用的唯一世界血条和伤害数字已实现；锁定只控制世界血条持续可见。
- 受击方向 Hit1-Hit5 的计算、打包、远端解包与 Animator 路由已实现。
- Server 战斗结果现携带实际伤害、绝对 HP 和 health revision；远端 NPC 还通过权威快照按 revision 变化和周期进行纠正。
- G0-4 仍是 `Partial`：Player 有即时绝对 HP 结果但尚无 NPC 同类周期纠正，且新增链路缺完整 Host/Client 验收。
- 锁定候选改为按相机朝向和屏幕中心筛选，角色背对目标但相机看向目标时仍可锁定。
- Phase G 已有 Turn90、健康 UI 和方向受击等增量，但镜头/VFX/SFX/击退/连段手感并未系统收口。
- Phase H 尚未开始。

## Phase Roadmap

| Phase | 目标 | 当前状态 | 结论与证据 | 能力域 |
|---|---|---|---|---|
| A | Context / Intent / Motor / Controller 骨架拆分 | `Implemented` | 结构存在于 `Character/Core`、`Intent`、`Motor`、`Controller`；历史记录判定完成 | [CC](REQUIREMENTS.md#character-control-cc) |
| B | Idle / Move / Sprint 移动 FSM | `Implemented` | 三状态、FSM、注册表和转换逻辑存在 | [CC](REQUIREMENTS.md#character-control-cc) |
| C | Attack / Dodge / Hit / Dead 与打断规则 | `Implemented` | 八状态、TransitionMap、Runtime window 存在 | [CC](REQUIREMENTS.md#character-control-cc)、[CB](REQUIREMENTS.md#combat-system-cb) |
| D1 | FakeNetwork 快照/事件闭环 | `Implemented` | `FakeNetworkPipe`、publishers、buffer/interpolator/applier 存在 | [NW](REQUIREMENTS.md#networking-nw) |
| D2 | Mirror 真实传输与玩家权限 | `Implemented` | `MirrorSyncTransport`、SyncBootstrap、PlayerAuthorityGate 存在 | [NW](REQUIREMENTS.md#networking-nw) |
| E | Server NPC、BT -> Intent -> FSM、协议复用 | `Implemented` | NpcServerAiGate、IntentSource、Driver、Publisher 和 NPC 状态存在 | [AI](REQUIREMENTS.md#npc-ai-ai)、[NW](REQUIREMENTS.md#networking-nw) |
| E0 | 巡逻/追击/攻击三段行为树样例 | `Deferred` | `AIContext/PhaseA-H_大纲.md` 与 `cursor_phasee2.md` 明确后置 | [AI-03](REQUIREMENTS.md#ai-03-可演示行为树) |
| F | Player/NPC 七种动作及双端表现 | `Verified` | Host+Client Player 已验收；Host+Client NPC 动画一致、仅有预期插值滞后，正式结项记录见 `AIContext/cursor_phasef2.md`，上下文提交 `0c1cfd3`（2026-06-26） | [UI](REQUIREMENTS.md#presentation-and-ui-ui)、[NW](REQUIREMENTS.md#networking-nw) |
| G0 | 攻击判定、权威伤害、格挡、战斗网络 | `Partial` | 本地命中/死亡已验证；NPC HP 纠正和格挡实际伤害已实现，Player 周期纠正及最新联机验收仍缺失 | [CB](REQUIREMENTS.md#combat-system-cb)、[NW](REQUIREMENTS.md#networking-nw) |
| G | 连段、受击、镜头、VFX/SFX 与网络观感 | `Partial` | Turn90、血量 UI、方向受击已增量实现；完整手感目标未收口 | [CC](REQUIREMENTS.md#character-control-cc)、[UI](REQUIREMENTS.md#presentation-and-ui-ui) |
| H | 多角色、技能、AI 与关卡内容生产 | `Not Started` | 当前只有配置基础，未形成内容生产流程 | [CP](REQUIREMENTS.md#content-production-cp) |

## G0 Detail

| 子阶段 | 当前状态 | 已有证据 | 尚缺内容 |
|---|---|---|---|
| G0-1 攻击碰撞 | `Verified` | `AttackDefinitionSet`、normalized hit windows、HitBox/HurtBox、Overlap、重复命中过滤；2026-07-02 Offline Play Mode 记录显示 Player 攻击 NPC 能扣血并进入 Hit/Dead | 需要纳入持续回归矩阵 |
| G0-2 权威伤害 | `Partial` | `CombatResolver.CanResolve()` 在线只允许 Server；战斗结果携带绝对 HP/revision；NPC 快照提供周期纠正；Offline 本地闭环已验证 | Player 周期 HP 纠正尚未实现；Host/Client Player/NPC 最终值未重验 |
| G0-3 格挡交互 | `Implemented` | Guard 方向扇区、轻重倍率、GuardHit/GuardBreak 路由存在；事件使用 HP 差值广播实际 applied damage 和权威结果 | 缺少最新 Offline + Host/Client 格挡/破防验收记录 |
| G0-4 战斗网络 | `Partial` | Hit/Dead/Guard 统一广播绝对 HP、MaxHp、实际伤害和 revision；`CombatActor` 拒绝旧 revision；NPC snapshot 按变化/周期纠正 | Player snapshot 尚未携带周期 HP 纠正；缺少最新双端 HP、方向受击和乱序/纠正验证 |

### G0 本地验证记录

`AIContext/PhaseG0-1.md` 的 2026-07-02 记录包含以下 Offline Play Mode 观察结果：

```text
Player 攻击 NPC
  -> NPC 扣血
  -> NPC 进入 Hit
  -> HP 归零进入 Dead
  -> 锁定自动切换到周围其他敌人
  -> Dead NPC 不可再次锁定
```

这只能证明当时的本地场景，不自动证明后来提交或 Host/Client 场景。

## Recent Increments

| 日期 | 提交 | 增量 | 当前证据等级 |
|---|---|---|---|
| 2026-06-29 | `f177999` | HitBox / HurtBox | `Implemented`，后续被本地 G0 记录覆盖验证 |
| 2026-07-02 | `92bcb78` | 攻击系统架构、AttackDefinitionSet | `Implemented` |
| 2026-07-02 | `a08061b` | 攻击数据调整、NPC 血条、死亡锁定优化 | 部分 `Verified`（当日 Offline 记录） |
| 2026-07-08 | `6d2b98a` | 防御链路基本结构 | `Implemented` |
| 2026-07-20 | `ac5a047`、`b73e4be` | Player/NPC Idle/Guard Turn90 与重构 | `Verified`（2026-07-20 Player/NPC 实机记录） |
| 2026-07-23 | `c71d9f5` | Player/NPC/锁定血量 UI、伤害数字 | `Implemented`；无当前环境矩阵记录 |
| 2026-07-24 | `faa84ce` | 五种方向受击与网络参数链 | `Implemented`；无当前 Host/Client 验收记录 |
| 2026-07-29 | 当前工作树 | Server 战斗结果绝对 HP/revision、NPC 快照纠正、格挡/破防实际伤害 | `Implemented`；`dotnet build` 通过，缺 Play Mode/Host/Client 验收 |
| 2026-07-29 | 当前工作树 | 远端命中显示唯一世界血条；锁定复用该血条；按相机朝向获取目标 | `Implemented`；缺 Offline/Host/Client 视觉验收 |

## Capability Progress

| 能力 | 状态 | 说明 |
|---|---|---|
| [Character Control](REQUIREMENTS.md#character-control-cc) | `Partial` | 核心控制/FSM/Turn 已完成；仍有 Phase G 手感优化 |
| [Combat System](REQUIREMENTS.md#combat-system-cb) | `Partial` | 攻击、格挡实际伤害、方向受击和即时权威 HP 已实现；Player 周期纠正与联机验收未收口 |
| [Lock-on and Camera](REQUIREMENTS.md#lock-on-and-camera-lc) | `Implemented` | 相机朝向选择、切换、死亡目标处理和锁定相机已实现；缺最新完整矩阵 |
| [Networking](REQUIREMENTS.md#networking-nw) | `Partial` | 状态/动作与 NPC HP 纠正闭环已实现；Player 周期 HP 快照仍缺失 |
| [NPC AI](REQUIREMENTS.md#npc-ai-ai) | `Partial` | Server 主线完成；E0 演示树 Deferred |
| [Presentation and UI](REQUIREMENTS.md#presentation-and-ui-ui) | `Partial` | 七种动作已验收，Turn/UI/方向受击已实现；完整反馈未完成 |
| [Content Production](REQUIREMENTS.md#content-production-cp) | `Not Started` | Phase H 未进入 |

<a id="known-gaps"></a>

## Known Gaps

### 1. Player 尚无周期 HP 快照纠正

Server 战斗结果已为 Player/NPC 携带绝对 HP 和 revision，NPC 还通过 `NpcAuthoritySyncPublisher` 的快照在 revision 变化或纠正间隔到期时重发权威 HP。`LocalSyncPublisher` 当前没有填充同类 HP 快照字段，因此 Player 仍主要依赖即时战斗结果；晚加入或完全缺失结果事件后的周期收敛尚未形成。

### 2. 最新网络、锁定和 UI 实现缺少环境验收记录

当前工作树证明 NPC HP 纠正、格挡/破防实际伤害、远端命中血条、唯一世界血条和相机朝向锁定已经实现，但尚无覆盖 Offline、Host、Client、Player、NPC 组合的正式结果表。它们不能仅凭代码存在标记为 `Verified`。

### 3. 缺少第一方自动化测试

`Assets/Scripts` 下未找到第一方 `[Test]` / `[UnityTest]`。FSM、Hit Param 编解码、方向映射、Guard 扇区和攻击窗口适合补 EditMode/PlayMode 回归，但不属于本次文档变更。

### 4. E0 与 Phase H 仍未展开

AI 主链可运行，但巡逻/追击/攻击演示树被后置；多角色、技能、波次和 AI 内容生产工具也未开始。

## Next Priorities

1. 按[验证矩阵](VERIFICATION_MATRIX.md)重新执行 Offline 与 Host/Client 战斗验收，覆盖绝对 HP/revision、NPC 周期纠正、GuardHit/Break 实际伤害和 Dead。
2. 验证远端 Player/NPC 互相命中时的血条显示，以及锁定目标只存在一个世界血条；验证角色背对目标但相机看向目标时可锁定。
3. 为 Player publisher 补充与 NPC 对称的周期 HP 快照纠正，并验证晚加入/缺失事件后的收敛。
4. 补充关键纯逻辑 EditMode 测试，再继续 Phase G 的镜头、VFX/SFX、击退与连段手感。
5. 根据演示目标决定先补 E0 行为树样例，还是进入 Phase H 内容生产。

## Evidence Index

| 证据 | 用途 |
|---|---|
| `AIContext/PhaseA-H_大纲.md` | 原始阶段目标；其 F/G0 状态已过期 |
| `AIContext/cursor_phasef2.md` | Phase F Host/Client Player/NPC 验收与正式结项 |
| `AIContext/PhaseG0-1.md` | 2026-07-02 G0 本地闭环与构建记录 |
| `AIContext/turn90_current_status_2026-07-20.md` | Player/NPC Turn90 实机验收与构建记录 |
| Git `f177999..faa84ce` | G0、格挡、Turn90、UI 和方向受击实现时间线 |
| 2026-07-29 当前工作树的 `CombatActor`、`CombatResolver`、`ActionEvent`、`StateSnapshot`、publishers/appliers | 权威 HP、revision、格挡实际伤害和 NPC 纠正实现证据 |
| 2026-07-29 当前工作树的 `NpcHealthBarView`、`LockOnTargetHudView`、`PlayerLockOnController` 与 Player/NPC prefabs | 唯一世界血条、远端命中显示和相机朝向锁定实现证据 |

## Maintenance

- 改变需求：更新 [REQUIREMENTS.md](REQUIREMENTS.md) 与对应 OpenSpec。
- 改变实现状态：在同一变更中更新本文的 Phase/能力行与证据。
- 完成验收：先把环境、步骤和结果写入 [VERIFICATION_MATRIX.md](VERIFICATION_MATRIX.md)，再升级本文状态。
- 改变架构：更新 [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md)。
- 不直接修改 `AIContext` 来表达当前状态；它保持历史记录身份。
