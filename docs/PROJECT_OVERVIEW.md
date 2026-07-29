# SS&3CDemo 项目总览

> 文档基线：2026-07-29，Git `faa84ce`（`受击方向接入`）；同日工作树实现按当前路径记为 `Implemented`  
> 本文描述稳定的项目定位、结构和运行时架构。当前完成度见[路线与进度](ROADMAP_AND_PROGRESS.md)，验收状态见[验证矩阵](VERIFICATION_MATRIX.md)。

## 文档导航

- [项目总览](PROJECT_OVERVIEW.md)
- [能力需求](REQUIREMENTS.md)
- [路线与进度](ROADMAP_AND_PROGRESS.md)
- [验证矩阵](VERIFICATION_MATRIX.md)

## 项目定位

SS&3CDemo 是一个带状态同步的第三人称动作战斗 Demo。项目重点不是堆叠内容量，而是形成一条可以解释、调试和联机验证的技术闭环：

- Character / Camera / Control 三项基础体验；
- 可打断的角色有限状态机；
- 玩家与 NPC 共用的意图、状态和表现语义；
- `StateSnapshot + ActionEvent` 双通道同步；
- 服务器侧 NPC 与战斗裁决；
- 逻辑、运动、同步、动画表现和 UI 的职责分离。

当前产品形态面向类魂动作体验，包括锁定、八向移动、连段、冲刺、闪避、格挡、受击和死亡。它仍是技术 Demo，不是完整游戏；关卡内容、数值生态、技能生产工具和完整战斗反馈属于后续范围。

## 技术基线

| 类别 | 技术或版本 | 说明 |
|---|---|---|
| 引擎 | Unity `2022.3.44f1c1` | 以 `ProjectSettings/ProjectVersion.txt` 为准 |
| 渲染 | URP `14.0.11` | Universal Render Pipeline |
| 网络 | Mirror `96.0.1` | 源码位于 `Assets/Mirror/` |
| 输入 | Input System `1.7.0` | `DemoInputActions` + `InputHandler` |
| 相机 | Cinemachine `2.10.7` | 第三人称与锁定相机 |
| 导航 | AI Navigation `1.1.7` | NPC NavMesh 移动 |
| AI | Behavior Designer Pro | 商业插件，本地安装，不提交仓库 |
| UI | uGUI `2.0.0` + TextMesh Pro `3.0.6` | 当前运行时 HUD 主要使用 uGUI |

版本来源：`Packages/manifest.json`、`Assets/Mirror/version.txt` 和 Unity 项目设置。

## 仓库边界

### 第一方内容

```text
Assets/
|-- Scripts/
|   |-- Character/
|   |   |-- Combat/          攻击数据、判定、伤害、格挡、血条事件
|   |   |-- Config/          角色 ScriptableObject 配置类型
|   |   |-- Controller/      玩家编排与 Animator Root Motion 转发
|   |   |-- Core/            CharacterContext 运行时数据
|   |   |-- Diagnostics/     状态与同步调试信息
|   |   |-- Intent/          CharacterIntent 语义输入
|   |   |-- LockOn/          目标注册、相机朝向选择和世界血条可见性
|   |   |-- Motor/           玩家移动、转向与 Root Motion
|   |   |-- Presentation/    Animator Presenter、转身与玩家 HUD
|   |   |-- StateMachine/    FSM、状态注册、打断表与八种状态
|   |   `-- Sync/            快照、事件、缓冲、插值和传输适配
|   |-- AI/                  NPC Intent、FSM、Motor、服务器门控
|   |-- Core/                GameData、相机、权限、GM 控制台
|   |-- Input/               输入采样
|   |-- Editor/              场景搭建与 NPC 调试编辑器
|   `-- Util/                场景加载辅助
|-- Data/                    角色、相机、同步、表现和攻击数据
|-- Prefabs/                 角色、系统、地形和 UI 预制体
|-- Scenes/                  Offline、Online、AITest、SampleScene
|-- Animations/              Animator Controller、Avatar Mask、动作片段
`-- Input/                   Input Actions 资产及生成代码

Tools/Git/                   商业插件保护与仓库维护脚本
AIContext/                   历史对话与旧进度记录，不是当前事实源
openspec/                    规格、变更提案和任务状态
```

### 第三方内容

- `Assets/Mirror/`：Mirror 网络框架及示例；项目文档不枚举其内部结构。
- `Assets/ParrelSync/`、`Assets/Plugins/ParrelSync/`：多实例联机调试工具。
- Behavior Designer Pro / Opsive 包：本地商业依赖，不应进入 Git。
- `Assets/ARPGSamurai/`：角色模型与动画资产。

## 场景与运行入口

| 场景 | 主要用途 | 关键入口 |
|---|---|---|
| `Assets/Scenes/Offline.unity` | 离线与本地回环调试 | Player、NPC、Arena、InGameHud；同步配置可使用 Fake 模式 |
| `Assets/Scenes/Online.unity` | Mirror Host / Client 联机验证 | NetworkManager、NetSystem、CombatManager、InGameHud |
| `Assets/Scenes/AITest.unity` | NPC、Behavior Designer、NavMesh 和战斗调试 | NPC Spawner、AI 导航与调试入口 |
| `Assets/Scenes/SampleScene.unity` | 基础示例 | 不作为当前主要验收场景 |

主要系统预制体：

- `Assets/Prefabs/System/NetworkManager.prefab`
- `Assets/Prefabs/System/NetSystem.prefab`
- `Assets/Prefabs/System/CombatManager.prefab`
- `Assets/Prefabs/System/InputSystem.prefab`

主要角色与 UI 预制体：

- `Assets/Prefabs/Characters/Player.prefab`
- `Assets/Prefabs/Characters/NPC.prefab`
- `Assets/Prefabs/Characters/Katana.prefab`
- `Assets/Prefabs/UI/InGameHud.prefab`
- `Assets/Prefabs/UI/NpcHealthBar.prefab`
- `Assets/Prefabs/UI/NpcDamageText.prefab`

`LockOnTargetHud.prefab` 和 `RemoteGhost.prefab` 仍保留在资产中，但不属于当前运行时主路径。锁定复用目标的 `NpcHealthBarView`，远端表现则由联网角色上的快照、事件、插值器和 Presenter 组成。

## 配置与数据资产

`GameDataCatalog` 是配置入口，角色行为通过 ScriptableObject 组合，不把主要参数散落在状态脚本中。

| 数据资产 | 职责 |
|---|---|
| `Assets/Data/GameDataCatalog.asset` | 全局玩家/NPC 配置目录 |
| `Assets/Data/Character/DefaultPlayer.asset` | 默认角色定义 |
| `DefaultLocomotion.asset` | 移动、加速、转向参数 |
| `DefaultCombat.asset` | HP、受击、闪避、格挡与攻击集合引用 |
| `DefaultPresentation.asset` | Animator、融合、转身表现参数 |
| `DefaultNetworkSync.asset` | 快照、缓冲和插值参数 |
| `SceneNetworkSync.asset` | 场景同步模式 |
| `SprintPhaseConfig.asset` | 分段冲刺参数 |
| `DefaultAttackSet.asset` | 九种攻击动作、伤害和 normalized hit windows |

## 运行时架构

### 控制与状态推进

```text
InputHandler / Behavior Designer SharedVariable
                    |
                    v
          CharacterIntent 语义输入
                    |
          +---------+----------+
          |                    |
 PlayerController      NpcCharacterDriver
          |                    |
          +---------+----------+
                    v
        CharacterStateMachine + Registry
                    |
          Idle / Move / Sprint / Attack
          Dodge / Guard / Hit / Dead
                    |
          +---------+----------+
          |                    |
 CharacterMotor             NpcMotor
```

职责边界：

- Intent 只描述“想做什么”，不直接播放动画或结算伤害；
- Controller / Driver 负责采样、组装依赖和推进 FSM；
- FSM 是逻辑状态的真相来源；
- Motor 负责位移、旋转和 Root Motion 消费；
- Presenter 读取逻辑与同步结果，不反向决定玩法状态。

### 战斗判定与结算

```text
AttackState / NpcAttackState
           |
           v
CombatActor 读取 AttackMoveId 与 elapsed time
           |
           v
AttackDefinitionSet -> AttackDefinition.hitWindows
           |
           v
CombatResolver -> CombatHitBox.OverlapHurtBoxesNonAlloc
           |
     阵营 / 自身 / 重复命中过滤
           |
           v
CombatHurtBox -> target CombatActor.ApplyHit
           |
     +-----+---------+-------------+
     |               |             |
   Guard          Hit State     Dead State
减伤/破防       方向变体/硬直    HP归零/锁定清理
```

在线模式下，`CombatResolver` 在 Mirror 连接活跃时只允许 Server 结算；离线且没有 NetworkClient/NetworkServer 时允许本地结算。攻击窗口来自 `DefaultAttackSet.asset`，物理层只负责候选查询，伤害规则集中在 `CombatActor`。

### 网络同步

```text
权威逻辑
  |-- LocalSyncPublisher / NpcAuthoritySyncPublisher
  |          |
  |          +-- StateSnapshot  连续状态
  |          `-- ActionEvent    离散动作
  |                    |
  `------------ ISyncTransport ----------------+
                 |                              |
          FakeNetworkPipe              MirrorSyncTransport
                 |                              |
                 +--------------+---------------+
                                v
             RemoteSnapshotBuffer / RemoteInterpolator
                                +
                       RemoteActionApplier
                                |
                                v
                  CharacterLateUpdatePipeline
```

- `StateSnapshot`：位置、朝向、速度、StateId、冲刺/格挡/Idle 阶段、连段、闪避模式和锁定；其 schema 还包含权威 `CurrentHp / MaxHp / HealthRevision`，当前由 NPC 权威发布器填充；
- `ActionEvent`：AttackStart、DodgeStart、Hit、Dead、Revive、GuardHit、GuardBreak 等边沿事件；Server 战斗结果附带实际伤害、绝对 HP 和同一 health revision；
- `NetTickClock`：统一网络 Tick；
- 远端位姿由缓冲和插值处理，离散动作由序号去重后交给表现层。

`ActionEvent` 为 Player/NPC 命中提供即时权威结果；NPC 的 `StateSnapshot` 在 health revision 变化或纠正间隔到期时重发绝对 HP。`CombatActor.ApplyAuthoritativeHealth` 只接受更新的 revision，因此乱序旧包不能覆盖新状态；Host 已直接结算时跳过客户端二次扣血。NPC 纠正链路和所有战斗结果目前仍需按[验证矩阵](VERIFICATION_MATRIX.md)完成 Host/Client 验收；Player 尚未接入同类周期 HP 快照纠正。

### 锁定目标选择

候选搜索半径仍以玩家位置为中心，但视角过滤和屏幕中心评分使用当前主相机。候选方向为“相机位置到锁定点”，所以角色可以背对目标、只要相机看向目标就能锁定；相机后方目标不会被新选中。已经锁定的目标不会仅因离开相机前方而立即失效。

### 动画表现与 UI

```text
FSM / Remote Snapshot / Remote Action
                    |
          CharacterLateUpdatePipeline
                    |
      +-------------+-------------+
      |             |             |
Locomotion      Sprint/Turn      Combat
Presenter        Presenter      Presenter
      |             |             |
      +-------------+-------------+
                    v
             Animator 多 Layer

HealthChanged / DamageTaken / Server Combat Result
                    |
      +-------------+-------------------+
      |                                 |
PlayerHealthHud                 NpcHealthBarView
                                      ^
                                      |
                         LockOnTargetHudView 仅控制可见性
```

表现层处理 Locomotion、Sprint、Turn90、Attack、Dodge、Guard、Hit、Dead 等动画。受击由 `HitVariant` 选择 Hit1-Hit5。本地玩家使用固定 HUD；每个非本地 Player/NPC 只拥有一个世界血条。观察到任意 Player/NPC 组合发生命中时，受击者世界血条显示相同配置时长；锁定只让同一个世界血条持续可见，不创建第二套 Canvas 或伤害数字。

## 模块所有权摘要

| 问题 | 主要所有者 | 不应承担该职责的模块 |
|---|---|---|
| 输入/AI 想做什么 | `CharacterIntent`、Input、AI source | Animator、Collider |
| 当前逻辑状态 | `CharacterStateMachine` | Presenter、远端 UI |
| 位移和旋转 | Motor、Root Motion relay | CombatResolver |
| 攻击何时有效 | `AttackDefinition`、`CombatActor` | Animation Event、HurtBox |
| 命中与伤害规则 | `CombatResolver`、`CombatActor` | HitBox、UI |
| 连续网络状态 | Snapshot publishers/interpolator | Animator |
| 离散网络动作 | Action publishers/applier | Transform 同步 |
| 动画播放 | LateUpdate pipeline + Presenter | FSM 状态实现 |
| 血条和伤害数字 | UI views + health events | 战斗规则层 |

## 文档事实源

1. 当前代码、场景、Prefab、数据资产和 Git 历史用于证明“已实现”。
2. 带环境与观察结果的人工/自动记录用于证明“已验证”。
3. 本目录文档汇总前两类证据。
4. `AIContext/` 仅用于追溯历史意图；旧结论不能覆盖更新的代码或验收记录。

需求不应仅从现有代码反推。新增或改变产品要求时更新[能力需求](REQUIREMENTS.md)，实现状态变化时更新[路线与进度](ROADMAP_AND_PROGRESS.md)，验收结果变化时更新[验证矩阵](VERIFICATION_MATRIX.md)。
