# SS&3CDemo 能力需求

> 本文定义项目“应具备什么能力”，不代表所有能力均已完成。实现状态见[路线与进度](ROADMAP_AND_PROGRESS.md)，环境验收见[验证矩阵](VERIFICATION_MATRIX.md)，架构边界见[项目总览](PROJECT_OVERVIEW.md)。

## 文档约定

- 需求按稳定能力域组织，Phase A-H 只表示交付顺序。
- `必须` 表示进入该能力验收线所需的行为。
- `后续` 表示目标范围已明确，但不属于当前技术闭环的完成门槛。
- 每条需求使用稳定 ID，进度与验证文档通过 ID 交叉引用。

## Character Control (CC)

### CC-01 统一意图输入

玩家输入和 NPC 决策必须转换为 `CharacterIntent` 语义，再交给角色逻辑消费。Intent 应描述 Move、Sprint、Attack、Dodge、Guard 等意图，不直接控制 Animator 或结算伤害。

**验收条件：**

- 玩家与 NPC 的状态逻辑不依赖具体键位或 Behavior Designer Task 类型；
- 输入边沿与按住态可以被 FSM 区分；
- 添加新的输入源时不需要重写状态机。

**相关阶段：** Phase A、B、E。

### CC-02 角色有限状态机

角色必须以 FSM 表达 `Idle / Move / Sprint / Attack / Dodge / Guard / Hit / Dead`，状态转换必须经过注册表和打断规则，而不是由表现层隐式切换。

**验收条件：**

- 状态调试信息能报告唯一 `CharacterStateId`；
- Attack、Dodge、Guard、Hit、Dead 的优先级与允许打断窗口可追踪；
- Dead 状态阻止继续移动和接受新的常规操作。

**相关阶段：** Phase B、C、F。

### CC-03 运动与朝向

角色必须支持常规移动、冲刺、锁定八向移动、闪避位移和 Idle/Guard 原地转身。位移、旋转与逻辑状态必须保持一致。

**验收条件：**

- 非锁定移动面向运动方向；
- 锁定移动保持目标导向并允许八向动画参数；
- Idle/Guard 的 Turn90 根据角度阈值、步幅和冷却执行；
- 玩家与 NPC 转身遵循相同计算规则。

**相关阶段：** Phase A、B、F、G。

### CC-04 Root Motion 所有权

Attack、Hit、Dead 等使用 Root Motion 的动作必须把动画位移应用到实际角色运动组件，避免模型与根物体分离；远端位置仍以权威快照为准。

**验收条件：**

- 本地模型不会在受击/死亡后与角色根节点持续偏离；
- Player 与 NPC 都有明确的 Root Motion 接收路径；
- 远端不以客户端 Animator 位移替代权威位置。

**相关阶段：** Phase F、G0。

### CC-05 配置驱动

移动、战斗、表现、冲刺和网络参数必须由角色定义与 ScriptableObject 配置组合，状态脚本只保留运行时状态和流程逻辑。

**验收条件：**

- 玩家/NPC 可通过不同 CharacterDefinition 组合配置；
- 攻击、格挡、转身、同步参数可以在不改流程代码的情况下调整；
- `GameDataCatalog` 提供稳定的配置入口。

**相关阶段：** Phase A、F、G0、H。

## Combat System (CB)

### CB-01 数据化攻击定义

每种攻击必须具有稳定的 `AttackMoveId`、时长、基础伤害、轻重击属性、是否单次命中，以及一个或多个 normalized hit windows。

**验收条件：**

- 连击、冲刺攻击、闪避攻击和重击可分别查到攻击定义；
- 动画时长变化后，normalized window 仍按比例生效；
- 攻击集合可以独立于 CharacterCombatConfig 编辑和复用。

**相关阶段：** Phase C、F、G0-1、H。

### CB-02 主动命中查询

攻击有效窗内必须由战斗系统主动查询 HitBox 与 HurtBox 候选，并过滤自身、同阵营、无效目标和同一攻击实例的重复命中。Collider Trigger 不作为权威攻击规则来源。

**验收条件：**

- 窗口外不产生有效命中；
- `hitSameTargetOnce` 生效；
- HurtBox 只提供受击区域和倍率，伤害规则由 Resolver/Actor 决定。

**相关阶段：** Phase G0-1。

### CB-03 权威伤害与生命

在线游戏中的命中、伤害、HP、Hit 和 Dead 结果必须由 Server 决定；离线模式允许本地权威。所有观察端最终必须收敛到相同 HP 与死亡状态。

**验收条件：**

- Client 不能独立裁决有效命中；
- Server 的一次有效命中只扣一次血；
- 玩家和 NPC 在 Host 与 Client 上报告相同 HP、Hit/Dead 结果；
- 战斗结果事件携带实际应用伤害、绝对 HP 和单调递增的 health revision；
- 周期权威快照可以纠正丢失动作事件或晚到数据，旧 revision 不得覆盖新 HP。

**相关阶段：** Phase G0-2、G0-4。

### CB-04 格挡与破防

Guard 的有效阶段必须按受击方向扇区判断是否防住攻击，并根据攻击类型应用免伤、减伤、格挡反应或破防受击。

**验收条件：**

- Guard Start/Exit 与无效朝向不能提供有效防御；
- 普通攻击、重击和破防可以配置不同伤害倍率；
- GuardHit 与 GuardBreak 有不同逻辑和动画结果；
- GuardHit/GuardBreak 同步实际结算伤害及其权威 HP 结果；
- 非零格挡/破防伤害在所有观察端最终保持一致。

**相关阶段：** Phase F、G0-3、G0-4。

### CB-05 闪避与受击

闪避必须在配置的无敌窗口内拒绝受击；有效命中必须区分轻重击、方向变体和硬直时间。

**验收条件：**

- 无敌窗前后仍可受击；
- 轻重击选择相应持续时间；
- 攻击者相对受击者的位置映射到稳定的 Hit1-Hit5 变体；
- 方向变体能传递到远端表现层。

**相关阶段：** Phase C、F、G0、G。

### CB-06 死亡与恢复

HP 归零必须进入 Dead，死亡目标不可继续受击或被重新锁定；Revive 必须恢复 HP、逻辑状态、锁定资格和远端表现。

**验收条件：**

- 当前锁定目标死亡后自动解除或切换；
- Dead NPC 不可再次成为锁定候选；
- Revive 在本地与远端清理旧的 Dead/Hit 表现状态。

**相关阶段：** Phase C、F、G0、G。

## Lock-on and Camera (LC)

### LC-01 锁定目标管理

锁定系统必须集中注册可锁定目标，按距离、视角和存活状态筛选，并支持锁定、解除与目标切换。

**验收条件：**

- Player 与 NPC 都可以作为配置允许的目标；
- 锁定点优先解析 humanoid 胸部骨骼并保留 fallback；
- 候选目标按主相机朝向而非角色朝向过滤，角色背对目标但相机看向目标时仍可锁定；
- 相机后方目标不会成为新的锁定或自动切换候选；
- 目标死亡或失效时不会留下悬空锁定状态。

**相关阶段：** Phase F、G0。

### LC-02 锁定运动和相机

锁定状态必须同时影响角色朝向、八向运动参数、闪避模式和第三人称相机构图；相机逻辑不得决定角色玩法状态。

**验收条件：**

- 锁定目标移动时角色转身和相机目标同步更新；
- 本地玩家拥有相机控制权，远端角色不创建或抢占主相机；
- 切换/解除锁定后恢复自由运动与相机行为。

**相关阶段：** Phase F、G。

## Networking (NW)

### NW-01 快照与事件双通道

连续状态必须通过 `StateSnapshot` 传输，离散动作必须通过带序号的 `ActionEvent` 传输；两条通道共同驱动远端表现。

**验收条件：**

- 快照覆盖位置、朝向、速度、StateId、表现阶段字段和可纠正的权威 HP revision；
- Attack、Dodge、Hit、Dead、Guard 反应等动作事件可去重，战斗结果携带实际伤害与绝对 HP；
- 同一状态重复进入（如连续受击）不会因 StateId 未变化而丢失表现边沿。

**相关阶段：** Phase D、E、F、G0-4。

### NW-02 传输抽象

同步发布和消费逻辑必须通过 `ISyncTransport` 与具体传输解耦，支持 Fake 本地回环与 Mirror 联机模式。

**验收条件：**

- 离线调试不要求启动 Mirror Server；
- Online 场景通过 Mirror 广播同一类 Snapshot/Action 数据；
- 切换传输模式不重写 FSM、Presenter 或 CombatActor。

**相关阶段：** Phase D1、D2。

### NW-03 权限和服务器所有权

本地玩家只处理自己有权限的输入；NPC AI 与在线战斗裁决只在 Server 推进；Client 只消费权威结果并负责表现。

**验收条件：**

- 远端 PlayerController 不读取本地输入；
- Client 上 NPC 不运行权威 FSM/导航；
- Host 不因同时具备 Server/Client 身份而重复扣血或重复应用动作。

**相关阶段：** Phase D2、E、G0。

### NW-04 插值与纠正

远端连续运动必须经过缓冲插值，并在误差过大时收敛到权威状态；离散动作不得被位置插值吞掉或乱序重放。

**验收条件：**

- 常规网络延迟下远端位置无高频抖动；
- 状态和动作的 Tick/Seq 可以用于诊断延迟与乱序；
- buffer delay 引入的预期滞后与同步错误可以区分；
- HP 快照按 revision 拒绝旧值，并通过 revision 变化或周期纠正最终收敛。

**相关阶段：** Phase D、F、G。

## NPC AI (AI)

### AI-01 Behavior Designer 到 Intent

Behavior Designer Task 和 SharedVariable 必须表达目标、目的地和动作请求，再由 `NpcAiIntentSource` 汇总成 CharacterIntent；Task 不应绕过 FSM 直接触发战斗表现。

**验收条件：**

- target/destination 可驱动移动与面对目标；
- 攻击、闪避、格挡等请求通过 Intent/FSM；
- Behavior Tree 与 Driver 的更新顺序不会稳定读取上一帧意图。

**相关阶段：** Phase E。

### AI-02 服务器权威 NPC

NPC 的 AI、NavMesh、FSM 和状态发布必须只在 Server 权威端运行，远端客户端使用与玩家相同的 Snapshot/Action 表现协议。

**验收条件：**

- 多客户端观察同一 NPC 时状态和关键动作一致；
- 添加第二只 NPC 不需要新增传输协议；
- NPC 七种动作通过相同 Presenter 规则展示。

**相关阶段：** Phase E、F。

### AI-03 可演示行为树

后续必须提供可复现的巡逻、追击、攻击行为树或等价演示流程，作为 AI 主链的内容化验收样例。

**验收条件：**

- 无 GM 调试输入时 NPC 能完成巡逻到发现目标再到交战的循环；
- 行为树关键变量和 Task 可复用到多个 NPC；
- 演示可在 Host/Client 环境观察一致结果。

**相关阶段：** E0（当前 Deferred）、Phase H。

## Presentation and UI (UI)

### UI-01 动画表现分层

Animator 必须由 LateUpdate 表现管线和 Presenter 驱动，逻辑 FSM 保持唯一真相；Locomotion、Sprint、Turn、Combat、Reaction 和 Death 的优先级必须明确。

**验收条件：**

- Idle/Move、Sprint、Attack、Dodge、Guard、Hit、Dead 能在 Player/NPC 本地及远端正确路由；
- Reaction/Death 不被低优先级 locomotion 覆盖；
- Presenter 不反向改变伤害或状态转换。

**相关阶段：** Phase F、G。

### UI-02 战斗可视反馈

玩家必须能看到自己的 HP，以及非本地 Player/NPC 的世界血条和伤害数字；显示值必须来自相应角色的健康事件或权威同步状态。

**验收条件：**

- InGameHud 只绑定本地玩家；
- 每个非本地 Player/NPC 只创建一个世界血条，锁定只让该血条持续可见，不创建独立锁定 HUD；
- 未锁定目标被任意远端 Player/NPC 命中时，所有观察端都让受击者世界血条显示统一时长；
- 世界血条在 HP 变化或权威纠正时更新绝对值；
- 伤害数字只为实际应用的正伤害生成；
- 联机 UI 不因本地缺失 HP 同步而显示过期值。

**相关阶段：** Phase G0、G。

### UI-03 战斗手感反馈

后续必须为命中、格挡、破防和死亡提供可区分的镜头、VFX、SFX、停顿或击退反馈，并保证网络观察端的时间关系可接受。

**验收条件：**

- 普通命中、重击、格挡成功、破防不会只有同一种反馈；
- 反馈不改变 Server 的权威判定结果；
- 低帧率和常规延迟下不会重复播放关键反馈。

**相关阶段：** Phase G。

## Content Production (CP)

### CP-01 多角色和技能数据

后续必须支持通过配置添加角色、武器和技能，而不是复制整套状态与同步代码。

**验收条件：**

- 新角色可以组合 CharacterDefinition、Animator 表现和 AttackDefinitionSet；
- 新武器/技能可以声明命中窗口、伤害和表现引用；
- 网络协议不为每个角色新增专用消息。

**相关阶段：** Phase H。

### CP-02 关卡与 AI 内容

后续必须提供波次、敌人组合、出生点、巡逻/仇恨参数和行为树任务库的生产流程。

**验收条件：**

- 设计者能通过数据或工具配置一轮敌人波次；
- 多种 NPC 复用通用 AI Task 与战斗系统；
- AITest/Online 可以作为内容验收入口。

**相关阶段：** Phase H。

## 需求变更规则

- 改变预期行为：更新本文件对应需求，并创建/更新 OpenSpec change。
- 只完成既有需求：更新[路线与进度](ROADMAP_AND_PROGRESS.md)。
- 新增验收证据：更新[验证矩阵](VERIFICATION_MATRIX.md)。
- 改变模块边界或数据流：同步更新[项目总览](PROJECT_OVERVIEW.md)。
- `AIContext/` 中的旧讨论不直接构成新需求，必须重新进入上述维护链路。
