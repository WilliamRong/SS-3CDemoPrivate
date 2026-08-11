## 背景

当前 `Parried` 与 `PostureBroken` 都使用 `rig_Collide` 表现，默认逻辑时长约为 1.067 秒，并在超时后返回可行动状态。左键已经映射为 Attack 单帧脉冲，普通攻击由 Idle、Move 和 Guard 等状态直接消费。战斗命中由 `CombatResolver` 遍历 `CombatHitBox`，查询 `CombatHurtBox` 后通过 `CombatActor.CanReceiveHit` 提交事务。

项目已经通过 `AnimatorRootMotionRelay` 把 Animator delta 交给 Player `CharacterMotor` 或权威 NPC Motor，但当前管线只原样消费 Root Motion，不知道目标锚点。Unity 读回显示 `rig_Execute` 长约 2.7 秒并前进约 0.75 米，`rig_Executed` 长约 3.517 秒并包含约 2.29 米的根位移，两者长度和轨迹不同；目标从处决进入死亡时还需要播放约 2.533 秒的 `rig_Executed_Death`。Animator Controller 的 Combat/Reaction Layer 尚未接入这些 clip。

处决是跨输入、状态机、战斗、移动、动画、镜头和网络的成对事务。目标位置必须稳定，只有处决者依据目标执行 Motion Warping；普通 HitBox/HurtBox 在双方各自动画结束前不得参与战斗，但环境碰撞仍要保留。

## 目标 / 非目标

**目标：**

- 让 Player 通过左键处决近距离、位于其正面对位条件内且仍处于 `Parried` 或 `PostureBroken` 的 Player/NPC。
- 让 `Idle` / `Move` Player 的处决输入在存在合法目标时优先于普通 Attack，并由 Offline/Server 权威重新验证；`Guard` 左键不得发起处决。
- 建立绑定双方、锚点、开始时间和结果的权威处决会话。
- 固定被处决者世界 Transform，只对处决者的 Root Motion 施加有限、可诊断的平移和旋转 Warp。
- 抽离可叠加、可复用的无敌语义，以统一 HurtBox 开关和权威受击门禁，并让处决额外独立抑制普通攻击。
- 在双方各自动画结束前阻断角色控制与普通战斗碰撞，同时保留本地镜头旋转；目标进入 `Dead` 时播放处决专用死亡变体。
- 使用显式玩法计时提交致死结果，不把 Animator 状态或动画事件作为事实源。
- 支持 Offline、Host/Client、Player/NPC 目标以及乱序/重复网络消息验证。

**非目标：**

- 不让 NPC AI 主动发起处决；NPC 只作为第一版的潜在被处决者。
- 不增加处决提示 UI、慢动作、镜头剪辑、特写、可选择的处决招式变体、奖励或音效系统。
- 不使用 Animation Rigging、IK 或 `Animator.MatchTarget` 修正手部/武器接触点。
- 不允许处决远距离拉拽、穿墙或绕过高度差与路径校验。
- 不改变普通 Attack、Parry、Posture、Hit、Dead 的既有数值和优先级，处决明确要求的转换除外。

## 设计决策

### 使用独立的权威处决会话和成对状态

Server 或 Offline 权威端创建不可重复的 `ExecutionSession`，至少记录 `executionId`、`executorActorId`、`targetActorId`、固定目标姿态、处决锚点、权威开始时间和结果提交时间。处决者进入 `Executing`，目标从 `Parried` 或 `PostureBroken` 进入 `Executed`。

会话接受前，`Dead` 和资格失效优先于处决。会话接受后，普通 Hit、Guard、Parry、PostureBreak 和输入不能打断双方；只有场景卸载、Actor 销毁、网络断开等生命周期事件可以触发确定性清理。目标在配置结果时刻锁存致死结果，但 `Executed` 表现保持到自身动画结束，随后以 `DeathPresentationVariant.Executed` 进入 `Dead` 并播放 `rig_Executed_Death`。

未采用复用 `Attack` / `Dead` 状态，因为普通攻击只有单方所有权，`Dead` 又会立即切换表现，无法表达双方绑定、固定目标、Warp 和动画长度差。

### 左键处决优先于普通攻击

`Idle` / `Move` 本地 Player 的 Attack 脉冲先交给处决候选解析器。解析器优先检查当前锁定目标，否则按水平距离、角度和稳定 ActorId 选择最优候选。只有存在本地合法候选时才发送处决请求并消费本次 Attack；没有候选或 Server 拒绝时，本次输入按普通 Attack 处理，Server 拒绝不得在后续帧补触发旧输入。`Guard` 左键完全跳过候选解析，继续交给既有 Guard 到普通 Attack 的状态转换。

处决者只允许从 `Idle` 或 `Move` 发起。目标必须存活、未处于其他处决会话，并仍处于 `Parried` 或 `PostureBroken`。权威端使用当前状态与 Transform 重新计算全部条件，不能信任 Client 提交的距离、角度或锚点，也必须拒绝来自 `Guard` 的处决请求。

### 使用配置化空间资格与 Warp 预算

空间资格基于水平面计算：目标前方向与“目标指向处决者”的夹角不得超过配置半角，水平距离不得超过配置最大值，绝对高度差不得超过配置值。默认最大距离以约 0.75 米为初始调参值，正面半角以 45 度为初始值。还需通过视线和 Capsule/路径空间检查。

处决锚点由目标接受瞬间的固定姿态计算：位置为目标局部空间中的可配置 `executorAnchorOffset`，旋转使处决者朝向目标。处决者当前姿态到锚点的平移和 Yaw 误差必须处于配置 Warp 预算内；超出预算直接拒绝，不能以瞬移修复。

### 在现有 Root Motion delta 管线中实现自定义 Motion Warping

`Executing` 进入时创建只属于本次会话的 Warp 上下文。Animator 仍产生原始 delta，Motor 在配置归一化 Warp Window 内叠加单调曲线分配的平移和旋转修正，使窗口结束时收敛到锚点要求的轨迹。窗口外继续消费动画原始 Root Motion。Player 使用 `CharacterController.Move`，未来若允许 NPC 发起处决则继续使用 `NavMeshAgent.Move`，环境碰撞不得绕过。

被处决者的 Transform 位置和 Yaw 在 `Executed` 期间固定为会话接受姿态，Animator 的 `rig_Executed` 根位移不交给 Motor。这样不会让约 2.29 米的 clip 根轨迹拖动权威目标，也能让所有观察者使用相同锚点重建配对表现。

未采用 `Animator.MatchTarget`，因为当前处决动画预计接入 Combat/Reaction Layer，而该 API 主要约束在 Base Layer；自定义 delta 修正也更符合现有 Motor 权威和网络读回边界。未采用开始瞬移，因为它会产生可见跳变并掩盖非法距离。

### 抽离可复用无敌并独立抑制处决攻击

`CombatActor` 持有纯 C# `InvulnerabilityRuntime`，以 `(InvulnerabilitySource, ownerId)` 作为可重入所有权键。闪避、处决和未来技能分别申请自己的令牌；只有最后一个令牌释放时才结束无敌。`CanReceiveHit` 始终以该运行时作为权威结算兜底，避免同帧缓存 Collider、直接事务或乱序消息绕过无敌。

当前无敌表现由 `CombatActor` 集中枚举并关闭所有 `CombatHurtBox` Collider，令牌全部释放后统一恢复。该开关不触碰 CharacterController、NavMesh、地面、墙体或其他环境 Collider。状态代码和处决会话不得直接逐个切换 HurtBox，因此异常退出只需要幂等释放令牌。现有 Player 闪避无敌窗口迁移到同一运行时，作为第一处复用入口。

无敌只表达“不能接收普通命中”，不默认禁止角色攻击。处决会话开始时还要取消双方当前攻击实例，并分别取得带 `executionId` 所有权的攻击抑制令牌；`CombatResolver` 在攻击方 `CanProduceCombatHit` 为假或目标 `CanReceiveHit` 为假时跳过事务。攻击抑制负责普通 HitBox/命中窗口，处决致死则通过独立权威事务提交，不经过 HurtBox。

处决者在 `rig_Execute` 完成时释放自身无敌和攻击抑制；目标在 `rig_Executed` 完成并进入 `Dead` 时释放。目标即使释放无敌，也由 `Dead` 继续拒绝普通命中。场景卸载、Actor 销毁、网络断开和重复完成均可幂等释放，不留下锁死或提前恢复的 Actor。

### 玩法时钟与动画表现分离

双方从同一权威开始时间播放 `rig_Execute` / `rig_Executed`，但使用独立配置时长。处决者在约 2.7 秒动画结束后退出控制锁定并释放自身无敌/攻击抑制；目标保持固定、不可操纵和不可受击到约 3.517 秒动画结束，然后以处决死亡变体进入 `Dead` 并 CrossFade 到约 2.533 秒的 `rig_Executed_Death`。处决会话在双方进入各自完成状态后释放，不等待死亡 clip 播完，因为 `Dead` 已接管输入锁定和不可受击。

`rig_Executed_Death` 是 `Dead` 的表现变体而不是第三个玩法状态。它沿用现有 Dead Motor/Root Motion 策略，不接受处决 Motion Warping，也不再由处决会话固定目标姿态。普通死亡继续播放既有 `rig_Death`，远端和晚加入者通过同步的 `DeathPresentationVariant` 选择正确 clip。

致死结果在配置 `executionResultTime` 提交，并锁存到会话；Animator 状态、normalizedTime 和动画事件只能驱动表现与诊断，不能决定是否死亡。远端动画延迟或 CrossFade 不得延后权威结果。

### 只锁角色控制并保留镜头域

`Executing` / `Executed` 状态忽略 Move、Sprint、Jump、Attack、Dodge、Guard、Parry 和 LockOn 切换，且不缓冲这些输入。`InputHandler` 和 Player Input Action Map 保持启用，`Look` 持续提供给本地镜头。

处决开始时暂停现有锁定相机并切换到仍跟随本地 Player 的可旋转 FreeLook/处决镜头模式；不得把目标锁定自动朝向当作镜头旋转。处决结束后恢复普通自由镜头配置，不恢复已经死亡目标的锁定。

### Server 同步会话而不是让 Client 推导结果

Client 请求只携带目标 ActorId 和本地请求序号。Server 重新解析双方状态、空间、路径和会话占用，接受后分配 `executionId`，广播双方 ActorId、固定目标姿态、锚点、开始时间和结果时间。Server 负责双方权威状态、Warp 后位置、战斗抑制和致死结果。

远端通过会话边沿锁存 `Executing` / `Executed`，旧 Attack、Parried 或 PostureBroken 快照不能恢复动作。重复开始、Host 回环、乱序完成和迟到普通动作必须按 `executionId` 去重。完成边沿与后续 `Dead` 快照携带 `DeathPresentationVariant.Executed`，让晚加入者不会错误播放普通死亡。加入较晚或表现延迟的观察者采用当前权威会话与绝对 Transform，不从动画当前帧反推玩法状态。Offline 模式走同一服务接口，但直接在本地创建会话。

### 配置与诊断保持数据驱动

战斗/表现数据至少公开最大处决距离、正面半角、最大高度差、视线/路径层、处决者锚点偏移、最大 Warp 平移/Yaw、Warp Window、Warp 曲线、双方处决动画时长、三段动画 CrossFade/校准参数和结果提交时间。校验必须保持距离和时长非负、角度有限、Warp Window 有序且结果时间位于会话有效范围。

诊断显示候选拒绝原因、双方 ActorId、`executionId`、目标来源状态、距离/角度/高度差、锚点、剩余 Warp 误差、抑制令牌、结果是否提交和网络序号。Scene Gizmo 显示目标正面扇区、距离范围和锚点。

## 风险 / 权衡

- [风险] `rig_Execute` 与 `rig_Executed` 并非严格按当前角色比例配对，根节点对齐后手部或武器仍穿模。-> 缓解：先在统一 Avatar 上校准锚点、Warp Window 和结果时间；第一版不承诺 IK 级接触精度。
- [风险] 现有 `Parried` / `PostureBroken` 只有约 1.067 秒，玩家处决窗口较严。-> 缓解：第一版明确只在状态存续期内有效并提供拒绝诊断；体验评审后再单独调整硬直时长，不隐式延长资格。
- [风险] Warp 受墙体或 CharacterController 阻挡后无法收敛。-> 缓解：接受前执行空间/路径检查并限制初始误差；运行时记录残差，不允许穿过环境碰撞。
- [风险] 多个系统直接切换 HurtBox 会造成提前恢复。-> 缓解：只允许 `InvulnerabilityRuntime` 的首个申请/最后释放边沿集中切换 `CombatHurtBox`，并以 `CanReceiveHit` 作为权威双重门禁；环境 Collider 始终不变。
- [风险] Client 延迟让左键处决反馈晚于普通攻击。-> 缓解：本地只在发现候选时消费输入并立即播放非承诺提示；第一版不预测权威位移或致死结果。
- [风险] 目标已逻辑死亡但 `Executed` 动画尚未结束，旧快照可能切回 `Dead` clip，晚加入者也可能选择普通死亡。-> 缓解：会话锁存的表现优先级覆盖普通 Dead 表现，结束边沿后才携带 `DeathPresentationVariant.Executed` 交给 `Dead`。
- [权衡] 处决者动画结束早于目标。-> 处决者按自身 2.7 秒解锁，目标继续完成约 3.517 秒表现；会话保留到双方结束，避免额外冻结处决者约 0.817 秒。

## 迁移计划

1. 抽离可复用无敌运行时并迁移既有 Player 闪避窗口，独立验证令牌叠加与 HurtBox 开关。
2. 增加配置、枚举、会话数据、死亡表现变体与诊断，但保持默认场景未触发处决时行为不变。
3. 先完成 Offline Player 对 NPC 的候选、状态、固定目标、Warp、攻击抑制和致死闭环。
4. 接入 Player 目标与 Server 权威会话协议，验证 Host/Client 去重、乱序、死亡变体和绝对位置。
5. 接入 Animator Controller、Presenter、镜头模式和数据资产，使用真实 clip 校准锚点与时间。
6. 更新项目需求、路线与验证矩阵，并在取得运行证据前保持网络项未验证。

回滚时移除处决输入分流和会话入口即可恢复普通 Attack；新增枚举只追加、不重排，旧网络字段保持兼容。场景、预制体和数据资产回滚必须通过 Unity Editor 保存路径完成。

## 待决定问题

- `executorAnchorOffset`、Warp Window、最大 Yaw 和 `executionResultTime` 的最终默认值需要在 Unity 动画预览与 Offline 双人实测后确定。
- 是否为合法目标显示处决提示 UI、是否延长约 1.067 秒的资格窗口、是否允许 NPC AI 主动处决，均保留为后续独立 change。
