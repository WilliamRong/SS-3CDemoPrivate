## 背景

项目当前以 `CharacterIntent -> PlayerController/NpcCharacterDriver -> CharacterStateMachine -> CombatActor/CombatResolver -> Sync/Presenter` 推进角色与战斗。攻击有效窗由逻辑计时和数据定义，在线命中只由 Server 结算；远端 Player 的 Guard、Attack 等即时状态由经过所有权校验的最新快照提供给 Server。

现有 `rig_Parry` 是 60 FPS、约 0.567 秒的 34 帧动画，但尚未进入 `CharacterAnimator.controller`。`rig_Collide` 是 60 FPS、约 1.067 秒的动画，当前由 `PostureBroken` 使用。弹反需要跨输入、状态、命中事务、攻击取消、动画层和网络协议，因此必须保持玩法状态与动画资源分离，并避免把弹反结果误报为崩防。

## 目标与非目标

**目标：**

- 玩家按 `Q` 从允许的常规状态发动弹反，完整动作期间不能被普通输入取消。
- 以可配置帧号精确表达前摇、有效窗口和后摇，默认有效区间为 `[8, 22)`。
- 有效窗口内对 360 度来源的有效攻击生效，取消本次 HP/架势伤害并立即终止攻击实例。
- Player 或 NPC 攻击方均可进入独立 `Parried` 硬直并播放 `rig_Collide`。
- Offline 使用本地权威；Mirror 会话由 Server 决定命中结果并让所属 Client、Host 和观察端状态收敛。
- 保留方向受击、Guard、PostureBroken、Dead 和现有同步协议的既有语义。

**非目标：**

- 本 change 不实现处决、背刺、致命一击或弹反后的专用追击。
- 不为 NPC 增加主动选择弹反的 AI 行为；NPC 只需要能成为被弹反的攻击方。
- 不引入 Animator Event 作为玩法窗口或状态结束的事实源。
- 不实现历史位置回溯、延迟补偿或完整反作弊输入协议。
- 不新增弹反特效、音效、体力消耗、冷却或 UI 提示。

## 技术决策

### 使用两个独立逻辑状态

向 `CharacterStateId` 末尾追加 `Parry` 和 `Parried`，并追加对应转换原因，禁止重排既有 byte 值。

- `Parry` 属于防守动作。Player 版本保存状态计时和 `Startup/Active/Recovery` 阶段，阻断普通输入与水平移动。
- `Parried` 属于攻击方强制反应。Player/NPC 分别使用适配各自 Motor 的状态实现，进入时停止攻击与移动，超时后回到 `Idle`。
- `Parry` 可以由权威 `Hit`、`PostureBroken` 或 `Dead` 打断；`Parried` 不提供无敌，可由新的权威 `Hit`、`PostureBroken` 或 `Dead` 打断，但不能被输入或 AI 意图取消。

不直接复用 `PostureBroken`。该状态还会重置/冻结架势、发布 `PostureBreak`、触发破势 UI，并抑制崩防期间的普通 Hit 表现，这些都不是被弹反的固有语义。

### 用逻辑计时器换算动画帧

在 `CharacterCombatConfig` 保存正数采样率、有效开始帧、有效结束帧、总帧和被弹反持续时间。默认值为：

```text
sampleRate = 60
activeStartFrame = 8
activeEndFrame = 22
totalFrames = 34
parriedDuration = 64 / 60 = 1.0666667s
```

运行时使用以下半开区间，消除第 22 帧是否仍有效的歧义：

```text
Startup : elapsed < 8 / 60
Active  : 8 / 60 <= elapsed < 22 / 60
Recovery: 22 / 60 <= elapsed < 34 / 60
End     : elapsed >= 34 / 60
```

FSM 计时是唯一玩法事实源。Presenter 在进入状态时从动画时间零开始 CrossFade，但不得回读 Animator normalized time 决定窗口。配置校验保证 `0 <= activeStartFrame < activeEndFrame <= totalFrames` 且采样率、总帧为正数。

### 明确弹反输入范围和消费方式

`DemoInputActions` 新增绑定 `<Keyboard>/q` 的 `Parry` Button，`InputHandler` 把 performed 锁存为单帧脉冲，`CharacterIntent` 增加 `IsParryPressed`。

Player 可从 `Idle/Move/Sprint/Guard` 进入 `Parry`；不可从 `Attack/Dodge/Hit/PostureBroken/Dead/Parry/Parried` 发动。允许状态在同帧收到多个普通动作时优先消费 `Parry`，避免同一个 Q 边沿又进入其他状态。`Parry` 和 `Parried` Tick 不读取动作脉冲，期间产生的单帧输入按现有 LateUpdate 清理，不做输入缓冲；状态自然结束到 `Idle` 后，下一帧才重新接受持续移动或 Guard 输入。

### 在命中事务最前面判定弹反

`CombatActor.ApplyHit` 在 GuardBreak、Guard 扇区、HP 和架势之前查询自身是否处于有效 `Parry` 阶段。查询来源遵循现有角色所有权顺序：本地 Player 活跃状态、Server NPC 状态（当前 NPC 不主动弹反）以及 Server 最近接受的远端 Player 快照。

成功弹反结果满足：

- 不调用方向扇区判断，因此正面、侧面和背面相同；
- 本次应用 HP 伤害和架势伤害均为零；
- 防守方不进入 `Hit/GuardHit/GuardBreak/PostureBroken/Dead`；
- `CombatHitResult` 明确记录 `WasParried` 和唯一 `Parry` 结果，发布层不根据零伤害反推；
- 攻击方通过 `CombatActor` 路由到 Player/NPC 的 `Parried` 状态。

前摇或后摇没有弹反能力，命中继续走完整现有事务。因为当前状态不是 Guard，所以不提供 Guard 扇区或减伤兜底。

### 成功后立即消费整个攻击实例

弹反成功后，攻击方必须立即停止 `CombatActor` 的攻击跟踪，并让 `CombatResolver` 将该 `ActorId + attackInstanceId` 标为本轮已取消。当前命中完成后，Resolver 的剩余 HurtBox、窗口、HitBox 和后续帧都不得再用该实例结算任何目标；成功弹反之前已经提交的其他目标结果不回滚。

这一显式取消标记避免仅等待下一帧状态采样，尤其可以覆盖远端 Player 的旧 Attack 快照和同一帧多 Collider 遍历。

### 分离动画状态但复用 clip

在 Animator 中增加两个无过渡条件、由 Presenter 直接 CrossFade 的状态：

- Combat Layer 的 `Parry` 引用 `rig_Parry`；
- Reaction Layer 的 `Parried` 引用 `rig_Collide`。

`PostureBroken` 继续保留自己的 Animator 状态并引用同一 `rig_Collide`。Presenter 为两个状态使用独立 Hash、缓存和 CrossFade 配置；`Parried` 进入时清理 Attack/Guard/Dodge 层，`Parry` 进入时清理 Guard 与 Reaction 残留。表现优先级保持 `Dead` 最高，权威 Hit/PostureBroken 可以按状态机转换覆盖弹反相关动画。

Player/NPC 的 Parry/Parried 都不加入 Root Motion 消费白名单，从而保持角色根节点原地稳定；远端位置继续由权威快照决定。

### 快照同步阶段，动作事件同步强制结果

向 `StateSnapshot` 和 Mirror `SnapshotMsg` 末尾追加 `ParryPhase`。仅 `StateId == Parry` 时允许非零有效阶段；阶段变化必须触发立即快照，Server 只接受所属连接为自身 Player 发布的快照并清洗非法枚举值。Server 使用最近接受的有效阶段做命中裁决，这与当前远端 Guard 阶段的信任边界一致。

向 `ActionType` 末尾追加 `ParryStart` 和 `Parried`：

- `ParryStart` 是所属 Player 的动作边沿，只用于远端及时重启表现，不能单独让 Server 认定有效窗口。
- `Parried` 只能由 Server 战斗结算产生，`ActorId` 必须指向攻击方；Client 上报该类型必须被拒绝。
- 所属 Client 收到自身 `Parried` 后立即强制进入本地 `Parried`，Server 上的远端 Player FSM 也进入并持续计时。
- `RemoteActionApplier` 对 `Parried` 使用与权威强制状态类似的动作/快照锁存：动作先到时保持 `Parried`，观察到对应快照后再以快照退出，防止在途旧 Attack 快照恢复攻击。
- Server 转发 Client 快照时，如果 Server FSM 处于 `Parried`，必须覆盖 Client 上报状态、速度和移动输入；Server 退出后也必须拒绝迟到的 Client `Parried` 伪造或延长硬直。

Server 序列号和状态进入版本共同避免 CombatResolver、快照与发布器描述同一次边沿时重复重启动画。Host 已直接应用的强制状态不得因回环消息重复进入。

### 保持协议和文档可迁移

枚举只追加，Snapshot/Message 字段只追加，所有发送与接收转换在同一 change 中升级。该项目不承诺新旧客户端混联，因此部署要求 Host/Client 使用相同构建；不存在存档迁移。

实现完成时同步更新 `docs/REQUIREMENTS.md`、`docs/ROADMAP_AND_PROGRESS.md` 和 `docs/VERIFICATION_MATRIX.md`，把代码存在标为 Implemented，把没有运行证据的网络矩阵保持待验。

## 风险与权衡

- [远端弹反窗口受网络延迟影响] → 阶段边沿立即发快照，并以 Server 最近接受值裁决；不在本 change 内伪造无延迟或实现回溯补偿。
- [Client 可以伪造较长的 Parry Active 阶段] → 保留与现有 Guard 相同的信任边界，校验 Actor 所有权、状态枚举和字段合法性；完整 Server 输入模拟与反作弊另行设计。
- [动作事件和快照乱序导致攻击短暂恢复] → `Parried` 使用 Server-only 事件、状态锁存和 Server 快照覆盖，直到观察到权威退出。
- [同一帧多个 HurtBox 在弹反后继续结算] → 使用攻击实例取消集合并在每层遍历前检查，而不是只依赖下一帧状态。
- [CrossFade 使视觉姿势与逻辑帧看起来略有偏差] → 两者从同一状态进入边沿开始，但玩法坚持逻辑计时；CrossFade 只作为表现参数调节。
- [复用 `rig_Collide` 引发语义串线] → 使用独立 `Parried` Animator/FSM/Action 标识，不复用 `PostureBroken` 事件和 UI。
- [协议字段增加破坏旧构建互通] → 所有端同步升级且枚举只追加；联机验收前清理旧进程并使用同一构建。

## 迁移与回退

1. 先追加配置、协议枚举和状态编号，保持既有默认资产可反序列化。
2. 接入 Offline 输入、状态、命中取消和 Animator，完成帧边界与 360 度验证。
3. 接入 Player/NPC `Parried` 路由和攻击实例取消，再扩展 Snapshot/Action/Mirror 链路。
4. 完成 Host/Client 矩阵与项目文档更新后才标记网络行为 Verified。

回退时可以整体撤销本 change 的新增状态、协议字段、Animator 状态和输入映射；既有枚举序号与姿态/生命数据未被重排或迁移。已包含新增协议的构建不得与回退后的旧构建混联。

## 开放问题

无。本 change 采用以下确定范围：仅 Player 主动发动；Player/NPC 攻击方均可被弹反；有效区间为 `[8,22)`；弹反不提供方向或 Guard 兜底；专用追击与延迟补偿不在本次范围。
