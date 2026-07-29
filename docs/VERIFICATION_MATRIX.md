# SS&3CDemo 验证矩阵

> 矩阵基线：2026-07-29，Git `faa84ce`  
> 同日未提交工作树只记为 `Implemented`；除静态编译外，新增链路尚未取得运行时验收记录。  
> 本文记录“在哪里观察到了什么”。需求见[能力需求](REQUIREMENTS.md)，总体状态见[路线与进度](ROADMAP_AND_PROGRESS.md)，系统结构见[项目总览](PROJECT_OVERVIEW.md)。

## Status and Evidence

| 状态 | 在本矩阵中的含义 |
|---|---|
| `Verified` | 已记录环境和观察结果；不代表在本次文档变更中重跑 |
| `Implemented` | 代码/资产路径完整，但没有对应环境结果记录 |
| `Partial` | 部分角色/环境通过或实现，另有明确未覆盖项 |
| `Deferred` | 明确后置，不进入当前验收门槛 |
| `Not Started` | 未发现实现或验证 |

升级为 `Verified` 时必须记录：日期、Git commit、Unity 场景、Host/Client 角色、操作步骤、观察结果。只有构建通过不能替代 Play Mode 或联机验收。

## Current Evidence Matrix

| ID | 能力 | Offline / Local | Host / Server | Client / Remote | 当前结论 | 证据或缺口 |
|---|---|---|---|---|---|---|
| VM-CC-01 | Player 八状态 FSM | `Implemented` | `Implemented` | 远端表现由 F 覆盖 | `Implemented` | `Assets/Scripts/Character/StateMachine/`；缺当前完整状态回归记录 |
| VM-CC-02 | NPC 八状态 FSM | `Implemented` | `Implemented` | `Verified`（历史 F 验收） | `Verified` | `AIContext/cursor_phasef2.md`：Host/Client NPC 动画一致，位置差异为预期插值滞后；上下文提交 `0c1cfd3`（2026-06-26） |
| VM-CC-03 | Player/NPC Idle/Guard Turn90 | `Verified`（Play Mode） | 未单列 | 未单列 | `Verified` | `AIContext/turn90_current_status_2026-07-20.md`：Player/NPC 实机通过；提交 `ac5a047`、`b73e4be` |
| VM-LC-01 | 按相机朝向获取锁定目标 | `Implemented` | 本地玩家适用 | 本地玩家适用 | `Implemented` | `PlayerLockOnController` 使用相机到锁定点方向、viewport z 和屏幕中心评分；缺 Play Mode 重验 |
| VM-F-01 | Player 七种动作远端表现 | 已实现 | `Verified` | `Verified` | `Verified` | `AIContext/cursor_phasef2.md` 的 Host+Client Player 验收与正式结项；上下文提交 `0c1cfd3`（2026-06-26） |
| VM-F-02 | NPC 七种动作远端表现 | 已实现 | `Verified` | `Verified` | `Verified` | `AIContext/cursor_phasef2.md` 的 Host+Client NPC 验收与正式结项；上下文提交 `0c1cfd3`（2026-06-26） |
| VM-CB-01 | 攻击窗口与 HitBox/HurtBox | `Verified`（2026-07-02） | `Implemented` | Client 不应裁决 | `Partial` | Offline Player 攻击 NPC 成功；Online 权威路径存在但未记录重验 |
| VM-CB-02 | Player 攻击 NPC 扣血/Hit/Dead | `Verified`（2026-07-02） | `Implemented` | 绝对 HP/revision 与 NPC 快照纠正已实现 | `Partial` | 本地闭环有历史记录；当前 `CombatActor.ApplyAuthoritativeHealth`、`NpcAuthoritySyncPublisher` 缺 Host/Client 重验 |
| VM-CB-03 | Player 受到 Server 命中 | 未记录 | `Implemented` | 即时绝对 HP/revision 已实现 | `Implemented` | `RemoteActionApplier` 应用权威结果并只为本地受击者播放反应；Player 周期快照纠正仍缺 |
| VM-CB-04 | 同一攻击实例重复命中过滤 | `Implemented` | `Implemented` | 不适用 | `Implemented` | `CombatResolver.HitKey` + `_resolvedHits`；缺自动化回归 |
| VM-CB-05 | 闪避无敌窗口 | `Implemented` | `Implemented` | Snapshot 表现 | `Implemented` | Config/FSM 存在；缺边界帧验证记录 |
| VM-CB-06 | Guard 正面扇区与普通免伤 | `Implemented` | `Implemented` | Guard 反应事件已实现 | `Implemented` | `CombatActor.TryResolveGuard`；缺 Offline/Online 验收记录 |
| VM-CB-07 | 重击格挡减伤 | `Implemented` | Server 按 HP 差值结算 | GuardHit 携带实际 damage 和权威 HP | `Implemented` | `CombatResolver.BroadcastGuardReaction`；缺双端结果观察 |
| VM-CB-08 | 重击破防 | `Implemented` | Server 按 HP 差值结算 | GuardBreak 携带实际 damage 和权威 HP | `Implemented` | `guardBreakOnHeavyHit = true`、统一 Server combat result；缺双端结果观察 |
| VM-CB-09 | 方向受击 Hit1-Hit5 | `Implemented` | `Implemented` | 解包和 Presenter 路由已实现 | `Implemented` | 提交 `faa84ce`；缺最新 Offline/Host/Client 方向矩阵 |
| VM-CB-10 | Dead 后锁定切换 | `Verified`（2026-07-02） | `Implemented` | 未验证 | `Partial` | Offline 自动切换与不可重锁通过；Client 状态需重验 |
| VM-CB-11 | Revive | `Implemented`（Player 基础入口） | 部分事件路径 | 远端完整恢复未验证 | `Partial` | Phase F 留作 polish；没有当前端到端记录 |
| VM-UI-01 | 本地玩家血量 HUD | `Implemented` | 可绑定 Host 本地玩家 | 可绑定 Client 本地玩家 | `Implemented` | `PlayerHealthHudView` + `InGameHud.prefab`；提交 `c71d9f5` |
| VM-UI-02 | 非本地 Player/NPC 世界血条与伤害数字 | `Implemented` | 权威数据可更新 | 绝对 HP/revision 可更新 | `Implemented` | Player/NPC prefabs 共用 `NpcHealthBarView`；缺当前环境重验 |
| VM-UI-03 | 锁定目标复用唯一世界血条 | `Implemented` | 本地锁定控制 | 本地锁定控制 | `Implemented` | `LockOnTargetHudView` 只调用 `SetLockOnVisible`，不再创建独立 Canvas/伤害数字 |
| VM-UI-04 | 观察到任意远端命中时显示受击者血条 | `Implemented` | Server combat result 广播 | `RemoteActionApplier.ShowForHit` | `Implemented` | 覆盖远端 Player-Player、Player-NPC、NPC-NPC；统一显示时长，缺多实例观察 |
| VM-NW-01 | FakeNetwork Snapshot/Action | `Implemented` | 不适用 | 本地回环 | `Implemented` | D1 组件完整；缺当前专项记录 |
| VM-NW-02 | Mirror Snapshot/Action | 不适用 | `Verified`（Phase F 表现） | `Verified`（Phase F 表现） | `Verified` | `AIContext/cursor_phasef2.md` 的 Host+Client Player/NPC 动画一致性记录，上下文提交 `0c1cfd3`（2026-06-26）；该记录不证明 HP |
| VM-NW-03 | Server-only CombatResolver | 离线本地允许 | `Implemented` | Client 返回 false | `Implemented` | `CombatResolver.CanResolve()`；缺恶意/误配置 Client 检查 |
| VM-NW-04 | Player HP 网络一致性 | 不适用 | Server 直接应用 | 战斗结果应用绝对 HP/revision | `Partial` | 即时结果已实现；Player publisher 尚无周期 HP 快照，缺多次命中/晚加入验收 |
| VM-NW-05 | NPC HP 网络一致性 | 本地可更新 | Server 直接应用 | 事件即时应用 + snapshot 周期纠正 | `Implemented` | `NpcAuthoritySyncPublisher` 在 revision 变化或纠正间隔到期时发送绝对 HP；缺 Host/Client 验收 |
| VM-NW-06 | HP revision 乱序过滤与纠正 | 不适用 | revision 随实际 HP 变化递增 | 旧 revision 被拒绝，NPC 新快照可收敛 | `Implemented` | `CombatActor.ApplyAuthoritativeHealth`；缺可控乱序/漏事件测试 |
| VM-AI-01 | Server NPC Intent/FSM | `Implemented` | `Implemented` | Client 不运行权威逻辑 | `Implemented` | `NpcServerAiGate`、`NpcCharacterDriver.LateUpdate` |
| VM-AI-02 | E0 巡逻/追击/攻击演示树 | 部分基础 Task | 未完成样例 | 未完成样例 | `Deferred` | 历史计划明确后置 |

## Offline Verification Checklist

场景：`Assets/Scenes/Offline.unity`。每次执行记录 commit、Unity 版本和角色配置。

| Case | 操作 | 预期结果 | 当前记录 |
|---|---|---|---|
| OFF-01 | Player 依次进入 Idle/Move/Sprint | StateId、速度与动画一致，无异常滑步 | 待重验 |
| OFF-02 | 触发 Combo1-4、Sprint Attack、Dodge Attack、Heavy | AttackMoveId、动画、hit window 与伤害定义一致 | 待重验 |
| OFF-03 | 有效窗内攻击 NPC，同一刀保持重叠 | 只结算一次；血条和伤害数字更新 | 2026-07-02 基础通过，7/23 UI 后待重验 |
| OFF-04 | 从正面、左、右、背面攻击 Player/NPC | 分别选择配置预期的 Hit1-Hit5，状态结束后恢复 | 待验（`faa84ce`） |
| OFF-05 | 在闪避无敌窗前/中/后受击 | 仅窗口内拒绝命中 | 待验 |
| OFF-06 | Guard Loop 中从正面普通攻击 | 免伤或按普通倍率结算，播放 GuardHit | 待验 |
| OFF-07 | Guard Start/Exit 或背后普通攻击 | Guard 不生效，进入 Hit | 待验 |
| OFF-08 | Guard Loop 中用重击攻击 | 按配置触发减伤/破防、HP 与反应一致 | 待验 |
| OFF-09 | 连续攻击至 NPC 死亡 | HP 到 0、Dead、血条策略正确、不可再锁定 | 2026-07-02 基础通过，7/23 UI 后待重验 |
| OFF-10 | 当前锁定目标死亡且附近有目标 | 自动切换；无候选时解除锁定 | 2026-07-02 基础通过 |
| OFF-11 | Player/NPC 锁定目标后静止或 Guard | 超阈值触发正确 Turn90，转身后对齐目标 | 2026-07-20 通过 |
| OFF-12 | Revive 已死亡 Player/NPC | HP、FSM、Animator、锁定资格全部恢复 | 待验 |
| OFF-13 | Player 背对 NPC，旋转相机看向 NPC 后触发锁定 | 能锁定相机前方 NPC；相机后方目标不进入新候选 | 待验 |
| OFF-14 | 锁定 NPC 后命中，随后解除锁定 | 全程只存在一个世界血条；锁定时持续显示，解除后按剩余命中计时隐藏 | 待验 |

## Host and Client Verification Checklist

场景：`Assets/Scenes/Online.unity`。建议使用 ParrelSync 或两个独立实例，明确记录哪个实例是 Host、哪个是 Client。

| Case | 操作 | Host 观察 | Client 观察 | 当前记录 |
|---|---|---|---|---|
| NET-01 | Host Player 依次触发七种动作 | 本地正确 | 远端动作/阶段一致 | Phase F 通过，最新 commit 待回归 |
| NET-02 | Client Player 依次触发七种动作 | 远端动作/阶段一致 | 本地正确 | Phase F 通过，最新 commit 待回归 |
| NET-03 | Server NPC 依次触发七种动作 | 权威 FSM 正确 | 远端动画一致，只有预期插值滞后 | Phase F 通过，最新 commit 待回归 |
| NET-04 | Client Player 攻击 Server NPC | Server 只结算一次 | NPC Hit/Dead/方向表现与 Server 一致 | 待验 |
| NET-05 | Host Player 攻击 Client Player | Server HP 为权威 | Client 本地 HP、Hit/Dead、HUD 与 Server 一致 | 待验 |
| NET-06 | Client Player 攻击 Host Player | Server HP 为权威 | 双端 Player HP/HUD 最终值一致 | 待验 |
| NET-07 | Player 多次攻击 NPC 但不致死 | Server NPC HP/revision 正确 | Client NPC 世界血条显示同一绝对值，周期快照可纠正 | 实现已补齐，待验收 |
| NET-08 | 普通攻击被 Guard 完全防住 | Server 不扣血 | 双端 GuardHit 和 HP 一致 | 待验 |
| NET-09 | 重击被 Guard 减伤或破防 | Server 按倍率扣血 | 双端 GuardHit/Break、实际伤害与 HP 同时一致 | 实现已补齐，待验收 |
| NET-10 | 从四向攻击 Player/NPC | Server 计算 variant | 双端播放同一 Hit1-Hit5 | 待验（`faa84ce`） |
| NET-11 | NPC 死亡时 Client 正锁定该 NPC | Server Dead | Client 解除/切换，死亡目标不可重锁 | 待验 |
| NET-12 | 同时观察 Player/NPC HUD 后 Revive | Server 恢复权威状态 | Client HP、状态、动画和锁定资格恢复 | 待验 |
| NET-13 | 依次触发远端 Player-Player、Player-NPC、NPC-NPC 命中 | Server 只结算目标一次 | 每个观察端只显示受击者的一个世界血条，显示时长一致 | 待验 |
| NET-14 | 人为延迟旧 HP 结果并漏掉一次 NPC 战斗事件 | Server revision 单调递增 | 旧 revision 不回滚 HP，后续 NPC 快照恢复最新绝对值 | 待验 |

## Build and Static Checks

| 检查 | 最近记录 | 当前结论 |
|---|---|---|
| `dotnet build Assembly-CSharp.csproj --no-restore -m:1` | 2026-07-29 当前工作树：0 errors | 静态编译通过；现有依赖/序列化字段警告不替代 Play Mode 验收 |
| 第一方 EditMode/PlayMode 测试 | 未找到 `Assets/Scripts` 下的 `[Test]` / `[UnityTest]` | `Not Started` |
| `git diff --check` | 2026-07-20 记录通过 | 文档变更完成时重新执行 |
| OpenSpec validate | 文档 change 提案阶段已通过 | apply 完成后重新执行 |

## Recording Template

执行矩阵后，在对应行更新状态，并追加如下记录；不要只写“测试通过”。

```text
Date:
Commit:
Unity:
Scene:
Roles: Host / Client / local Player / remote Player / NPC
Cases:
Observed:
Result: Pass / Fail
Evidence: log, screenshot, video, or issue link
Notes:
```

## Maintenance

- 代码存在但未运行：保持 `Implemented`。
- 只验证 Offline：涉及网络的总体行保持 `Partial`。
- 发现缺口：在本矩阵记录失败环境，并同步更新[路线与进度](ROADMAP_AND_PROGRESS.md)。
- 改变预期结果：先更新[能力需求](REQUIREMENTS.md)，不能只改测试预期。
- `AIContext` 中的旧验收只作为历史证据；新提交影响相关链路后，应执行回归而不是自动沿用 `Verified`。
