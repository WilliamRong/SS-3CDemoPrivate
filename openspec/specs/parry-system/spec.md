# parry-system Specification

## Purpose
TBD - created by archiving change add-parry-system. Update Purpose after archive.
## Requirements
### Requirement: 玩家弹反输入与进入资格
系统必须（SHALL）把绑定 `Q` 的单次 Player 输入转换为 `CharacterIntent` 弹反意图，并且只能从明确允许的常规角色状态进入 `Parry`。

#### Scenario: 从静止或移动发动弹反
- **WHEN** 本地 Player 在 `Idle`、`Move`、`Sprint` 或 `Guard` 按下 `Q`
- **THEN** 该次输入只触发一次 `Parry` 状态进入，角色停止普通移动并从动画起点播放弹反

#### Scenario: 锁定战斗状态拒绝弹反
- **WHEN** Player 在 `Attack`、`Dodge`、`Hit`、`PostureBroken`、`Dead`、`Parry` 或 `Parried` 按下 `Q`
- **THEN** 当前状态不因该输入进入或重入 `Parry`

#### Scenario: NPC 不主动发动弹反
- **WHEN** NPC AI 产生普通移动、攻击、闪避或格挡意图
- **THEN** NPC 不会因为本 change 自动进入 `Parry`

### Requirement: 数据驱动弹反帧窗口
`Parry` 必须（SHALL）使用逻辑计时器和可配置动画采样率、有效开始帧、有效结束帧及总帧划分前摇、有效和后摇；默认 60 FPS、开始帧 8、结束帧 22、总帧 34，且有效区间必须（MUST）为半开区间 `[8,22)`。

#### Scenario: 进入前摇
- **WHEN** `Parry` 已经过时间小于 `8 / 60` 秒
- **THEN** 当前阶段为 `Startup`，角色没有弹反判定

#### Scenario: 到达发生帧
- **WHEN** `Parry` 已经过时间达到或跨过 `8 / 60` 秒且仍小于 `22 / 60` 秒
- **THEN** 当前阶段为 `Active`，从第 8 帧开始具有弹反判定

#### Scenario: 到达结束帧
- **WHEN** `Parry` 已经过时间达到或跨过 `22 / 60` 秒且仍小于 `34 / 60` 秒
- **THEN** 当前阶段为 `Recovery`，第 22 帧及之后不再具有弹反判定

#### Scenario: 动画逻辑结束
- **WHEN** `Parry` 已经过时间达到或跨过 `34 / 60` 秒且没有被权威反应打断
- **THEN** 角色退出到 `Idle`，下一逻辑帧才重新处理普通输入

#### Scenario: 非法帧配置
- **WHEN** 序列化值不满足正采样率、正总帧或 `0 <= activeStartFrame < activeEndFrame <= totalFrames`
- **THEN** 配置校验必须钳制或报告非法值，使运行时区间保持有序且有限

### Requirement: 弹反动作期间控制锁定
Player 在 `Parry` 的前摇、有效和后摇全过程必须（SHALL）拒绝移动、冲刺、跳跃、攻击、闪避、格挡切换和新的弹反输入，但权威战斗反应仍可打断该状态。

#### Scenario: 弹反期间输入
- **WHEN** Player 在 `Parry` 任一阶段提供普通移动或动作输入
- **THEN** 输入不会移动角色、切换动作或提前结束弹反，且单帧动作不会缓冲到弹反结束后执行

#### Scenario: 弹反期间受到普通权威反应
- **WHEN** Player 在没有成功弹反该次命中的阶段受到会产生 `Hit`、`PostureBroken` 或 `Dead` 的权威结算
- **THEN** 对应权威状态可以立即打断 `Parry`

### Requirement: 360 度成功弹反
有效攻击在防守方处于 `Parry.Active` 时必须（SHALL）先于 Guard 方向、HP 和架势结算触发成功弹反，且不得检查攻击相对防守方的方向。

#### Scenario: 正面攻击进入有效窗口
- **WHEN** 有效攻击从防守方正面命中处于 `Parry.Active` 的 Player
- **THEN** 本次攻击被弹反，防守方不损失 HP 或架势且不产生普通受击、格挡或破势反应

#### Scenario: 背面攻击进入有效窗口
- **WHEN** 有效攻击从防守方背面命中处于 `Parry.Active` 的 Player
- **THEN** 结果与正面成功弹反相同，不调用 Guard 扇区或受击方向决定是否成功

#### Scenario: 侧面攻击进入有效窗口
- **WHEN** 有效攻击从防守方任一侧面命中处于 `Parry.Active` 的 Player
- **THEN** 结果与正面成功弹反相同

#### Scenario: 重击进入有效窗口
- **WHEN** 配置为重击或 GuardBreak 的有效攻击命中 `Parry.Active`
- **THEN** 弹反优先于重击减伤、GuardBreak、HP 和架势结算

### Requirement: 窗口外执行普通受击
`Parry.Startup` 和 `Parry.Recovery` 必须（SHALL）不提供弹反或 Guard 兜底；窗口外命中必须继续执行既有普通战斗事务。

#### Scenario: 前摇被攻击
- **WHEN** 有效攻击在第 8 帧之前命中处于 `Parry.Startup` 的 Player
- **THEN** 攻击按现有 HP、架势、Hit、PostureBreak 和 Dead 规则结算

#### Scenario: 后摇被攻击
- **WHEN** 有效攻击在第 22 帧或之后命中处于 `Parry.Recovery` 的 Player
- **THEN** 攻击按现有 HP、架势、Hit、PostureBreak 和 Dead 规则结算

#### Scenario: 弹反期间仍按普通 Guard 处理被禁止
- **WHEN** Player 从 `Guard` 进入 `Parry` 后在非有效阶段被攻击
- **THEN** 系统不得因为进入前曾经 Guard 而应用 Guard 扇区、免伤或格挡反应

### Requirement: 攻击方进入独立被弹反状态
成功弹反必须（SHALL）强制 Player 或 NPC 攻击方进入独立 `Parried` 状态，立即停止其攻击和水平移动，播放 `rig_Collide`，并在默认约 1.067 秒内拒绝普通输入或 AI 意图。

#### Scenario: Player 攻击方被弹反
- **WHEN** Player 的有效攻击被成功弹反
- **THEN** 该 Player 进入 `Parried`、退出 `Attack` 并在状态自然结束前不能通过输入恢复移动或攻击

#### Scenario: NPC 攻击方被弹反
- **WHEN** NPC 的有效攻击被成功弹反
- **THEN** 该 NPC 进入 `Parried`、停止当前攻击与导航动作，并在状态自然结束前忽略 AI 普通意图

#### Scenario: 被弹反状态自然结束
- **WHEN** 存活攻击方在 `Parried` 保持到配置持续时间结束且没有新的权威反应
- **THEN** 攻击方退出到 `Idle`，之后才能接受新的输入或 AI 动作

#### Scenario: 被弹反期间受到权威反应
- **WHEN** `Parried` 攻击方受到会产生 `Hit`、`PostureBroken` 或 `Dead` 的新权威结算
- **THEN** 对应状态可以打断 `Parried`，但攻击方不能通过自身输入或 AI 意图取消硬直

### Requirement: 成功弹反消费攻击实例
成功弹反必须（SHALL）立即取消产生该命中的整个攻击实例，使该实例在成功结果之后不能继续命中防守方或其他目标。

#### Scenario: HitBox 在后续帧仍重叠
- **WHEN** 被弹反攻击的 HitBox 在后续有效帧继续与同一 HurtBox 重叠
- **THEN** 该攻击实例不会再次产生伤害、架势或反应

#### Scenario: 同帧剩余候选目标
- **WHEN** Resolver 在一次遍历中成功弹反后仍有该攻击实例的其他 HurtBox、窗口或 HitBox 候选
- **THEN** 成功结果之后的剩余候选全部被拒绝

#### Scenario: 弹反前已经提交其他命中
- **WHEN** 同一攻击实例在成功弹反之前已经对另一个目标提交有效结果
- **THEN** 已提交结果不回滚，但成功弹反之后不再产生新结果

### Requirement: 弹反与崩防语义隔离
`Parried` 与 `PostureBroken` 必须（SHALL）使用不同的逻辑状态、动作类型和表现标识；共享 `rig_Collide` 动画不得引发架势重置、恢复冻结、破势 UI 或 `PostureBreak` 事件。

#### Scenario: 攻击方进入被弹反状态
- **WHEN** 攻击方因成功弹反进入 `Parried`
- **THEN** 其当前架势不会因进入该状态被重置，且观察端不会显示破势专用 UI 强调

#### Scenario: 攻击方真正破势
- **WHEN** 另一次权威结算使攻击方架势达到上限并进入 `PostureBroken`
- **THEN** 现有架势重置、破势事件和 UI 行为保持不变，不被 `Parried` 逻辑替代

### Requirement: 弹反动画表现
本地和远端表现必须（SHALL）把 `Parry` 映射到 Combat Layer 的 `rig_Parry`，把 `Parried` 映射到 Reaction Layer 的 `rig_Collide`，并且不得把 Animator 状态作为玩法阶段或结束条件。

#### Scenario: 本地发动弹反
- **WHEN** 本地 Player 进入 `Parry`
- **THEN** Presenter 从动画起点播放 `rig_Parry`，清理冲突的 Guard/Reaction 表现且不消费 Root Motion

#### Scenario: 攻击方被弹反
- **WHEN** 本地、服务器 NPC 或远端角色进入 `Parried`
- **THEN** Presenter 播放独立 `Parried` Animator 状态引用的 `rig_Collide`，并抑制低优先级 Attack、Dodge、Guard 和 locomotion 表现

#### Scenario: 崩防与被弹反引用同一 clip
- **WHEN** Animator Controller 同时包含 `PostureBroken` 和 `Parried`
- **THEN** 两个状态可以引用同一 `rig_Collide` clip，但使用不同状态名、逻辑来源和动作边沿

### Requirement: Server 权威弹反结果
Mirror 会话活跃时，只有 Server 可以（SHALL）根据有效攻击重叠和最近接受的合法 `Parry` 阶段决定成功弹反并产生 `Parried` 强制结果；Offline 模式必须（SHALL）允许本地权威执行同一事务。

#### Scenario: Client 仅观察本地碰撞
- **WHEN** 非 Server Client 在 `Parry.Active` 期间观察到攻击 Collider 与自身重叠
- **THEN** Client 不自行取消伤害或强制攻击方进入 `Parried`，最终结果等待 Server

#### Scenario: Server 接受所属 Player 的阶段快照
- **WHEN** Server 收到连接为其自身 Player 发布且字段合法的 `Parry` 状态快照
- **THEN** Server 保存最近阶段用于后续命中裁决，并把阶段边沿转发给观察者

#### Scenario: 非所属连接伪造弹反状态
- **WHEN** Client 为其他 Actor 发布 `Parry` 快照或 `Parried` 动作事件
- **THEN** Server 拒绝该消息且不改变任何角色的权威结果

#### Scenario: Offline 成功弹反
- **WHEN** `NetworkServer` 和 `NetworkClient` 均未激活且本地攻击命中 `Parry.Active`
- **THEN** 本地权威执行与 Server 相同的零伤害、攻击取消和 `Parried` 转换

### Requirement: 弹反阶段与强制状态同步
网络协议必须（SHALL）同步 `Parry` 阶段和 Server-only `Parried` 动作边沿，并在动作/快照乱序时保持攻击方硬直，直到权威状态明确退出。

#### Scenario: 阶段边界触发快照
- **WHEN** Player 的 `Parry` 从 `Startup` 进入 `Active` 或从 `Active` 进入 `Recovery`
- **THEN** 发布端立即发送包含合法 `ParryPhase` 的状态快照，不等待位置或周期阈值变化

#### Scenario: 被弹反动作先于快照
- **WHEN** 观察端先收到 Server `Parried` 动作，之后才收到攻击方状态快照
- **THEN** 观察端立即锁存并表现 `Parried`，在观察到对应快照前不允许旧 Attack 快照恢复攻击

#### Scenario: 所属 Client 收到自身被弹反结果
- **WHEN** Client 收到 `ActorId` 指向本地 Player 的 Server `Parried` 动作
- **THEN** 本地 Player 立即进入 `Parried` 并停止输入驱动的攻击

#### Scenario: Host 回环权威结果
- **WHEN** Host 已在 Server 侧让攻击方进入 `Parried` 后收到同一结果的本地回环或状态边沿
- **THEN** 该角色不会重复进入、延长硬直或重启动画

#### Scenario: 迟到攻击快照
- **WHEN** Server 已强制远端 Player 进入 `Parried` 后收到该 Client 在弹反前发送的 Attack 快照
- **THEN** Server 保持 `Parried` 并把速度与移动输入清零，不让旧快照恢复攻击

### Requirement: 弹反诊断与验证
项目必须（SHALL）公开足够的状态、阶段、攻击实例和权威结果诊断，并提供分离的 Offline、Host、Client、Player 与 NPC 验收用例。

#### Scenario: 检查帧边界
- **WHEN** 开发者在第 7、8、21、22 帧附近执行测试命中
- **THEN** 可以从日志或状态诊断确认阶段、攻击实例、是否弹反、实际 HP/架势结果和最终反应

#### Scenario: 更新验证矩阵
- **WHEN** 弹反实现完成
- **THEN** 项目文档包含前摇、有效、后摇、四向来源、Player/NPC 攻击方、攻击取消和 Host/Client 乱序用例，且没有运行证据的项目保持未验证

