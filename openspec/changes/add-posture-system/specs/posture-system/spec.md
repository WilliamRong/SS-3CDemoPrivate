## ADDED Requirements

### Requirement: 共享架势资源
每个 Player 和 NPC 战斗 actor 必须（SHALL）公开具有可配置最大值、当前值、比例和变化通知的架势资源，且该资源必须（SHALL）初始化为零。

#### Scenario: Actor 初始化
- **WHEN** Player 或 NPC 战斗 actor 使用有效战斗配置完成初始化
- **THEN** 当前架势为零，最大架势为配置的正数，架势比例为零

#### Scenario: 架势值被钳制
- **WHEN** 任意架势修改会产生小于零或大于最大值的结果
- **THEN** 存储值被钳制到包含零和最大值的闭区间

#### Scenario: Actor 复活
- **WHEN** 已死亡 actor 被复活
- **THEN** 其架势重置为零，观察者收到更新值

### Requirement: 战斗产生架势增长
有效攻击无论正常命中还是被成功格挡，都必须（SHALL）为防守方增加配置的架势伤害；被拒绝的命中不得增加架势。

#### Scenario: 未格挡命中增加架势
- **WHEN** 攻击通过命中校验且没有被格挡
- **THEN** 防守方针对该次结算只增加一次攻击定义中的非负普通架势伤害

#### Scenario: 格挡命中增加架势
- **WHEN** 攻击在有效格挡扇区和阶段内被成功格挡
- **THEN** 即使 HP 伤害为零，防守方仍增加攻击定义中的非负格挡架势伤害

#### Scenario: 无敌或被拒绝命中
- **WHEN** 防守方无敌、死亡、无效、同阵营、自身目标、攻击不在有效窗口，或该攻击实例已经结算
- **THEN** 防守方不从该命中获得架势

#### Scenario: Actor 已经破势
- **WHEN** 防守方处于 `PostureBroken` 时受到有效非致死命中
- **THEN** 现有 HP 伤害规则仍生效，但退出 `PostureBroken` 前不增加架势

### Requirement: 受 HP 影响的架势恢复
存活且架势大于零的 actor 必须（SHALL）在配置恢复延迟后随时间恢复架势，其恢复速率必须（SHALL）随 HP 比例单调增加。

#### Scenario: 恢复延迟结束
- **WHEN** 架势大于零，且距离最近一次架势增长已经超过配置延迟
- **THEN** 权威端每个模拟 tick 降低架势且不越过零

#### Scenario: 新增长重置恢复延迟
- **WHEN** actor 在等待或恢复期间获得正架势增长
- **THEN** 恢复延迟从该次增长时刻重新开始

#### Scenario: 更高 HP 恢复更快
- **WHEN** 两个其他条件相同的 actor 以不同正 HP 比例恢复相同时长
- **THEN** HP 比例更高者使用相等或更高的配置恢复速率

#### Scenario: 状态不修改恢复速率
- **WHEN** 存活 actor 在恢复延迟结束后处于 Idle、Move、Attack、Dodge 或 Guard
- **THEN** 除非进入 `PostureBroken` 或 `Dead`，否则都使用同一套 HP 比例恢复速率

#### Scenario: Actor 死亡
- **WHEN** actor 进入 `Dead`
- **THEN** 架势重置为零，死亡期间不运行恢复模拟

### Requirement: 强制破势
架势达到最大值时，系统必须（SHALL）强制存活防守方进入专用 `PostureBroken` 逻辑状态，并且必须（SHALL）在破势开始时只把架势重置一次。

#### Scenario: 普通命中达到阈值
- **WHEN** 非致死未格挡命中使架势达到或超过最大值
- **THEN** 架势重置为零，防守方进入 `PostureBroken`，本次结算的普通 Hit 反应被抑制

#### Scenario: 格挡命中达到阈值
- **WHEN** 成功格挡使命中后的架势达到或超过最大值
- **THEN** 架势重置为零，防守方进入 `PostureBroken`，本次结算的普通 GuardHit 反应被抑制

#### Scenario: 致死命中同时达到阈值
- **WHEN** 同一次命中把 HP 降到零且原本也会填满架势
- **THEN** 防守方进入 `Dead`，架势重置为零，不进入 `PostureBroken`

#### Scenario: 破势持续时间结束
- **WHEN** 存活 actor 在 `PostureBroken` 保持到配置持续时间结束
- **THEN** actor 退出到有效的非战斗移动状态，架势仍为零

#### Scenario: 破势期间死亡
- **WHEN** actor 处于 `PostureBroken` 时 HP 到零
- **THEN** `Dead` 立即打断 `PostureBroken`

### Requirement: 破势控制限制
`PostureBroken` 必须（SHALL）在配置时长内阻止普通移动和动作转换，同时仍允许死亡和权威 HP 伤害。

#### Scenario: Player 在破势期间输入
- **WHEN** Player 在 `PostureBroken` 期间输入移动、冲刺、攻击、闪避或格挡
- **THEN** 这些输入不会退出状态或移动角色

#### Scenario: NPC 在破势期间产生意图
- **WHEN** NPC AI 在 `PostureBroken` 期间提供移动或战斗意图
- **THEN** NPC 保持破势，直到超时或死亡

### Requirement: 数据驱动架势调参
最大架势、恢复延迟、低 HP 恢复速率、满 HP 恢复速率、破势持续时间、普通命中架势伤害和格挡架势伤害必须（SHALL）在项目战斗数据中可配置。

#### Scenario: 恢复配置无效
- **WHEN** 序列化恢复值为负数，或满 HP 恢复速率低于低 HP 恢复速率
- **THEN** 配置校验钳制或报告这些值，使运行时恢复保持非负且单调

#### Scenario: 不同攻击产生不同压力
- **WHEN** 两个攻击定义配置不同架势伤害
- **THEN** 结算时产生各自独立配置的架势增长，而不改变其 HP 伤害

### Requirement: Server 权威架势
Mirror 会话活跃时，只有 Server 可以（SHALL）修改玩法架势、运行恢复、重置架势和选择 `PostureBroken`；Offline 模式必须（SHALL）允许本地模拟权威执行这些操作。

#### Scenario: Client 观察到本地战斗重叠
- **WHEN** 非 Server Client 在攻击期间观察到 Collider 重叠
- **THEN** 它不会在本地增加架势或进入 `PostureBroken`

#### Scenario: Host 收到自身广播
- **WHEN** Host 已经应用权威架势事务后收到对应网络消息
- **THEN** 它不会再次应用架势修改

#### Scenario: Offline 战斗
- **WHEN** `NetworkServer` 和 `NetworkClient` 都未激活
- **THEN** 有效本地命中、恢复和破势由本地模拟

### Requirement: 绝对架势同步
网络状态必须（SHALL）携带足够的绝对架势数据，使每个远端 Player 和 NPC 能重建 current/max posture；离散破势事件或等价状态边沿必须（SHALL）保留即时破势表现。

#### Scenario: 远端架势纠正
- **WHEN** Client 收到更新权威快照，且 current/max posture 与本地不同
- **THEN** 远端 `CombatActor` 采用绝对值并通知已绑定 UI

#### Scenario: 远端 NPC 架势
- **WHEN** Client 观察 NPC 在 Server 上增加和恢复架势
- **THEN** Client NPC 架势条收敛到相同绝对 current/max，且不运行本地恢复

#### Scenario: 破势边沿
- **WHEN** 权威端进入 `PostureBroken`
- **THEN** 远端观察者收到状态/动作边沿并只启动一次破势表现

#### Scenario: UI 延迟绑定
- **WHEN** 架势 UI 在架势已经变化后才绑定
- **THEN** 它立即渲染最近一次快照应用后的 current/max

### Requirement: 架势表现
本地 Player HUD 以及非本地 Player/NPC 的世界健康 UI 必须（SHALL）在健康条正下方显示稳定的黄色架势条；锁定目标必须（SHALL）复用同一个世界 UI。

#### Scenario: 本地 Player 架势变化
- **WHEN** 本地 Player 增加或恢复架势
- **THEN** 黄色 Player 架势填充更新为 current/max，且不会改变周围 HUD 布局尺寸

#### Scenario: 世界目标架势变化
- **WHEN** 可见的非本地 Player 或 NPC 增加或恢复架势
- **THEN** 其世界健康条下方的黄色架势条显示更新比例

#### Scenario: 锁定目标变化
- **WHEN** Player 锁定另一个有效 Player 或 NPC 目标
- **THEN** 不创建独立锁定 HUD，同一个世界 UI 持续可见并显示该目标最新架势比例

#### Scenario: 架势为零
- **WHEN** 父健康 UI 可见且当前架势为零
- **THEN** 架势轨道仍存在，黄色填充为空

#### Scenario: UI 不能修改架势
- **WHEN** 架势条更新或重新绑定
- **THEN** 它不执行任何玩法架势修改或破势决定

### Requirement: 破势动画表现
Player 和 NPC 表现必须（SHALL）把 `PostureBroken` 映射为可区分的全身破势反应，并且不得把 Animator 状态作为破势持续时间或结束条件的事实源。

#### Scenario: 本地破势
- **WHEN** 本地权威 actor 进入 `PostureBroken`
- **THEN** 表现管线播放配置的破势动画，并抑制低优先级移动/战斗表现

#### Scenario: 远端破势
- **WHEN** 远端快照/动作表示 `PostureBroken`
- **THEN** 相同破势表现只播放一次，且优先级低于 `Dead`

### Requirement: 架势诊断与验证
项目必须（SHALL）在诊断中公开架势 current/max 和状态，并为 Player/NPC 架势行为提供确定性的 Offline 与 Host/Client 验证覆盖。

#### Scenario: 开发者检查架势
- **WHEN** 调试会话中架势变化或发生破势
- **THEN** 开发者无需从 UI 像素推断，即可观察 actor 身份、current/max posture、HP 比例、恢复状态和 `PostureBroken`

#### Scenario: 更新验证矩阵
- **WHEN** 架势实现完成
- **THEN** 项目文档包含分离的 Offline、Host、Client、Player、NPC、命中、格挡、恢复、破势和延迟绑定用例，并把没有证据的项目保持为未验证
