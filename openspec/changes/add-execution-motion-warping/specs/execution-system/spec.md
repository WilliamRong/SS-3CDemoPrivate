## ADDED Requirements

### Requirement: 处决目标资格
系统必须（SHALL）只允许 Player 对仍处于 `Parried` 或 `PostureBroken` 的存活 Player/NPC 发起处决，且处决双方必须（SHALL）未被其他处决会话占用。

#### Scenario: 被弹反目标可被处决
- **WHEN** 存活 Player/NPC 仍处于 `Parried`，且 Player 处决者满足全部空间和状态条件
- **THEN** 该目标成为合法处决候选

#### Scenario: 崩防目标可被处决
- **WHEN** 存活 Player/NPC 仍处于 `PostureBroken`，且 Player 处决者满足全部空间和状态条件
- **THEN** 该目标成为合法处决候选

#### Scenario: 资格状态已经结束
- **WHEN** 目标已经退出 `Parried` / `PostureBroken`、进入 `Dead` 或加入另一处决会话
- **THEN** 新处决请求被拒绝且双方状态不变

#### Scenario: 处决者状态不允许
- **WHEN** Player 处于 Attack、Dodge、Hit、Parry、Parried、PostureBroken、Executing、Executed 或 Dead 时按下左键
- **THEN** 该输入不能发起处决

### Requirement: 处决空间校验
处决者必须（SHALL）位于目标的配置正面扇区内，水平距离和高度差必须（SHALL）不超过配置阈值，并且双方之间必须（SHALL）具有有效视线、可用路径和不超过 Warp 预算的相对姿态。

#### Scenario: 正面半身位内
- **WHEN** 处决者位于目标正面配置半角内，水平距离不超过默认约 0.75 米，且其他资格条件有效
- **THEN** 空间校验通过并生成相对目标的处决锚点

#### Scenario: 位于目标背面或侧后方
- **WHEN** 目标前方向与目标指向处决者的水平夹角超过配置正面半角
- **THEN** 处决请求被拒绝且左键继续按普通攻击解释

#### Scenario: 距离或高度差超限
- **WHEN** 双方水平距离或绝对高度差超过配置阈值
- **THEN** 不创建处决会话且不得把处决者瞬移到目标

#### Scenario: 路径被墙体阻挡
- **WHEN** 处决者到锚点之间的视线、Capsule 或路径空间检查失败
- **THEN** 处决请求被拒绝且 Motion Warping 不得穿过阻挡物

#### Scenario: Warp 误差超出预算
- **WHEN** 处决者到锚点的位置或 Yaw 误差超过配置最大 Warp 值
- **THEN** 处决请求被拒绝而不是使用大幅瞬移修正

### Requirement: 左键处决优先级
本地 Player 的左键 Attack 脉冲必须（SHALL）在普通攻击状态转换之前解析处决候选；合法候选存在时必须（SHALL）只提交一次处决请求，没有合法候选时必须（SHALL）保持现有普通 Attack 行为。

#### Scenario: 合法目标存在
- **WHEN** Idle、Move 或 Guard Player 按下左键且存在合法处决候选
- **THEN** 本次脉冲提交一次处决请求，不同时进入普通 Attack

#### Scenario: 没有合法目标
- **WHEN** Player 按下左键但所有候选均不合法
- **THEN** 本次脉冲按现有规则进入或准备普通 Attack

#### Scenario: 多个目标同时合法
- **WHEN** 当前锁定目标合法且附近还有其他合法目标
- **THEN** 系统优先选择当前锁定目标；没有合法锁定目标时按距离、角度和稳定 ActorId 确定唯一候选

#### Scenario: Server 拒绝请求
- **WHEN** Client 本地发现候选但 Server 复核时资格已经失效
- **THEN** Server 不创建会话，旧左键脉冲不会在后续帧补触发处决或攻击

### Requirement: 权威成对处决会话
每次接受的处决必须（SHALL）创建具有唯一 `executionId` 的权威会话，绑定处决者、被处决者、固定目标姿态、锚点、开始时间和结果时间，并原子地让双方进入 `Executing` / `Executed`。

#### Scenario: Offline 接受处决
- **WHEN** `NetworkServer` 和 `NetworkClient` 均未激活且本地资格校验通过
- **THEN** 本地权威创建会话，处决者进入 `Executing`，目标进入 `Executed`

#### Scenario: Server 接受所属 Player 请求
- **WHEN** Server 收到连接为其所属 Player 发出的目标 ActorId 请求且权威复核通过
- **THEN** Server 分配 `executionId`、锁存会话并向观察者广播同一对角色与锚点

#### Scenario: 非所属或伪造请求
- **WHEN** Client 代表其他 Player 请求处决、提交自定义锚点或篡改目标状态
- **THEN** Server 拒绝请求且不改变任何角色状态

#### Scenario: 并发请求同一目标
- **WHEN** 多个请求在同一权威帧尝试处决同一目标
- **THEN** 只有按确定性顺序首先提交的有效请求创建会话，其余请求因目标已被占用而失败

### Requirement: 固定目标与处决者 Motion Warping
`Executed` 目标的世界位置和 Yaw 必须（SHALL）在自身动画期间保持为会话固定姿态；只有 `Executing` 处决者可以（SHALL）在配置 Warp Window 内依据目标锚点修正 Root Motion，窗口外必须（SHALL）继续使用动画原始 Root Motion。

#### Scenario: 被处决动画包含根位移
- **WHEN** `rig_Executed` 产生位置或旋转 Root Motion delta
- **THEN** 该 delta 不移动目标的权威 Transform，目标保持会话固定姿态

#### Scenario: 处决者进入 Warp Window
- **WHEN** `rig_Execute` 在配置 Warp Window 内产生原始 Root Motion
- **THEN** Motor 按单调 Warp 曲线合并有限平移/Yaw 修正，使轨迹收敛到相对目标的锚点

#### Scenario: Warp Window 结束
- **WHEN** 处决者越过 Warp Window 结束边界
- **THEN** 后续位移使用原始 Root Motion，不再持续拉向目标

#### Scenario: 环境阻挡 Root Motion
- **WHEN** 处决者 Warp 位移与墙体、地面或其他环境碰撞相交
- **THEN** 位移继续通过 CharacterController 或权威 Motor 处理，不绕过环境碰撞

### Requirement: 配对处决动画
处决者与被处决者必须（SHALL）从同一权威开始时间分别播放全身 `rig_Execute` 和 `rig_Executed`，并使用独立配置时长；Animator 状态不得（MUST NOT）决定处决资格、致死结果或状态结束的权威时刻。

#### Scenario: 会话表现开始
- **WHEN** 本地或远端观察者接受新的处决会话边沿
- **THEN** 双方动画从会话时间对应位置开始或校正，且同一 `executionId` 不重复重启

#### Scenario: 处决者动画先结束
- **WHEN** `rig_Execute` 的配置时长结束而 `rig_Executed` 尚未结束
- **THEN** 处决者结束自身控制锁定和战斗抑制，目标继续保持 `Executed` 到自身时长结束

#### Scenario: 动画播放延迟
- **WHEN** 远端 CrossFade、低帧率或消息延迟导致动画时间落后于权威会话
- **THEN** 权威结果时间不被延后，表现按会话开始时间追赶而不重新提交玩法结果

### Requirement: 处决战斗抑制与无敌
`Executing` 和 `Executed` Actor 必须（SHALL）在各自处决动画结束前退出所有普通 HitBox/HurtBox 战斗判定，不能产生或接收普通命中，同时必须（SHALL）保留 CharacterController、NavMesh、地面和墙体碰撞。

#### Scenario: 开始处决时存在攻击实例
- **WHEN** 任一方进入处决会话时仍有当前攻击实例或有效命中窗口
- **THEN** 该攻击实例被取消，后续 HitBox 候选不再结算

#### Scenario: 第三方攻击处决双方
- **WHEN** 第三方普通攻击 HitBox 在处决动画期间重叠任一方 HurtBox
- **THEN** `CombatResolver` 拒绝该命中，双方 HP、架势和状态不变

#### Scenario: 处决者动画完成
- **WHEN** 处决者的配置动画时长结束
- **THEN** 处决者的战斗抑制幂等释放，并可在下一逻辑帧重新产生和接收普通命中

#### Scenario: 被处决者动画完成
- **WHEN** 被处决者的配置动画时长结束
- **THEN** 其处决抑制完成清理并由 `Dead` 状态继续保持不可受击，不恢复为存活 HurtBox 目标

### Requirement: 角色控制锁定与镜头保留
`Executing` / `Executed` 期间必须（SHALL）忽略移动、冲刺、跳跃、攻击、闪避、格挡、弹反和锁定切换且不得缓冲输入，但本地 Player 必须（SHALL）仍可使用 `Look` 旋转镜头。

#### Scenario: 处决者输入普通动作
- **WHEN** 本地处决者在 `Executing` 期间输入角色移动或动作
- **THEN** 角色状态和 Warp 轨迹不受影响，输入不会在动画结束后补执行

#### Scenario: Player 被处决时输入
- **WHEN** 本地 Player 处于 `Executed` 并输入移动或动作
- **THEN** 目标 Transform 和会话状态保持不变，输入不会取消处决

#### Scenario: 处决期间移动镜头
- **WHEN** 本地 Player 在 `Executing` 或 `Executed` 期间提供 Look 输入
- **THEN** 镜头可以围绕当前本地角色旋转，角色位置、朝向和动画不随镜头输入改变

#### Scenario: 从锁定状态开始处决
- **WHEN** 处决开始前本地 Player 正在锁定目标
- **THEN** 处决期间锁定相机被暂停并允许镜头旋转，已经死亡的目标不会在结束后恢复为锁定目标

### Requirement: 确定性致死与结束
权威处决会话必须（SHALL）在配置结果时刻只提交一次致死结果，并在目标动画完成后进入 `Dead`；普通战斗反应、重复消息和动画事件不得（MUST NOT）重复伤害、复活或改变该结果。

#### Scenario: 到达结果时刻
- **WHEN** 活跃会话首次达到配置 `executionResultTime`
- **THEN** 权威端把目标生命结果锁存为致死并广播绝对 HP/revision，目标继续播放 `Executed`

#### Scenario: 重复结果消息
- **WHEN** Host 或 Client 收到同一 `executionId` 的重复致死/完成边沿
- **THEN** HP、revision、状态时长和动画不会再次应用或延长

#### Scenario: 目标动画结束
- **WHEN** `Executed` 配置时长结束且致死结果已经锁存
- **THEN** 目标进入 `Dead`，处决会话在双方均完成后释放

#### Scenario: 生命周期异常清理
- **WHEN** 场景卸载、Actor 销毁或网络断开终止活跃会话
- **THEN** 权威端幂等清理会话、Warp 和战斗抑制，不留下可攻击锁死或重复占用的 Actor

### Requirement: 处决网络同步
网络协议必须（SHALL）同步足以重建处决会话的双方 ActorId、`executionId`、固定目标姿态、锚点、开始时间、结果状态和完成边沿，并在消息乱序、迟到或 Host 回环时保持唯一权威结果。

#### Scenario: 旧动作快照迟到
- **WHEN** Server 或观察端已锁存处决会话后收到更早的 Attack、Parried 或 PostureBroken 快照
- **THEN** 旧快照不能让任一方退出 `Executing` / `Executed` 或恢复普通 HitBox/HurtBox

#### Scenario: 远端晚收到会话
- **WHEN** 观察端在权威开始时间之后才收到处决开始消息
- **THEN** 它使用会话绝对姿态和经过时间追赶配对表现，而不是从动画零帧延后整个结果

#### Scenario: Host 回环
- **WHEN** Host 已在 Server 侧应用会话后收到本地回环边沿
- **THEN** 会话、Warp、抑制令牌和动画不会重复创建或重启

#### Scenario: 完成边沿先于旧开始消息
- **WHEN** 观察端先应用某 `executionId` 的完成/死亡结果，之后收到该会话的迟到开始消息
- **THEN** 观察端保持完成状态并拒绝重新进入处决

### Requirement: 数据驱动处决配置
处决距离、正面半角、高度差、视线/路径层、锚点偏移、Warp 平移/Yaw 预算、Warp Window/曲线、双方动画时长、CrossFade 和结果时间必须（SHALL）在项目战斗或表现数据中可配置并经过有效性校验。

#### Scenario: 配置值非法
- **WHEN** 距离/时长为负、角度非有限值、Warp Window 无序或结果时间超出会话范围
- **THEN** 配置校验钳制或报告错误，使运行时条件保持有限、有序且可确定

#### Scenario: 动画资产重新校准
- **WHEN** `rig_Execute` / `rig_Executed` 或角色比例改变
- **THEN** 开发者可以只调整锚点、Warp 和时间配置，而不改变处决资格与网络协议语义

### Requirement: 处决诊断与验证
项目必须（SHALL）公开候选资格、会话、锚点、Warp、战斗抑制和结果诊断，并为 Offline、Host、Client、Player 与 NPC 目标提供分离的确定性验证覆盖。

#### Scenario: 检查候选拒绝原因
- **WHEN** 左键未能发起处决
- **THEN** 开发者可以观察目标状态、距离、正面角、高度差、视线、路径、Warp 预算和占用中的具体失败项

#### Scenario: 检查活跃会话
- **WHEN** 处决正在播放
- **THEN** 开发者可以观察双方 ActorId、`executionId`、固定目标姿态、锚点、剩余 Warp 误差、抑制状态、结果时刻和网络序号

#### Scenario: 更新验证矩阵
- **WHEN** 处决实现完成
- **THEN** 项目文档包含 Parried/PostureBroken、正反面、距离/高度/墙体、输入优先级、Warp、双方无敌、镜头、Player/NPC 目标及 Offline/Host/Client 乱序用例，且没有证据的项目保持未验证
