# SS&3CDemo

基于 Unity 的 **3C（Character / Camera / Control）动作战斗 Demo**，面向类魂系体验：锁定目标、连招、冲刺、闪避、格挡、方向受击与血量反馈，并支持本地调试与 Mirror 联机同步。

> 本仓库为**私有项目**。项目包含自研内容、可再分发的开源依赖和已授权资产；Behavior Designer Pro 等第三方商业插件需自行安装，不包含在版本库中。

## 项目文档

- [项目总览](docs/PROJECT_OVERVIEW.md)：定位、技术栈、目录职责与运行时数据流
- [能力需求](docs/REQUIREMENTS.md)：按角色、战斗、网络、AI、表现等能力域维护需求
- [路线与进度](docs/ROADMAP_AND_PROGRESS.md)：Phase A-H、当前基线、证据与已知缺口
- [验证矩阵](docs/VERIFICATION_MATRIX.md)：Offline、Host、Client、Player 与 NPC 验收项

`AIContext/` 保存历史对话和旧状态记录，不作为当前进度事实源。当前状态以代码/资产、Git 历史、带环境的验证记录和上述维护文档为准。

---

## 功能概览

### 角色控制（3C）

- **状态机驱动**：Idle / Move / Sprint / Attack / Dodge / Guard / Hit / Dead
- **战斗动作**：连招（Combo）、冲刺攻击、闪避攻击、重击、格挡（全身 / 上半身叠加）
- **战斗判定**：数据化攻击窗口、HitBox/HurtBox、Server 权威结算、格挡/破防实际伤害同步
- **锁定系统**：按相机朝向筛选目标，支持切换、锁定移动与动画表现
- **相机**：Cinemachine 驱动的第三人称相机，支持锁定相机修正
- **表现与 UI**：Player/NPC 七类动作、Idle/Guard Turn90、方向受击、玩家 HUD、Player/NPC 唯一世界血条和伤害数字

### 网络同步

- **快照 + 事件** 双通道同步架构
  - `StateSnapshot`：位置、朝向、状态和锁定信息；NPC 快照同时携带权威 HP 与 `HealthRevision`
  - `ActionEvent`：状态切换和战斗结果等离散事件；命中、格挡与破防携带实际伤害及权威 HP
- **双传输模式**
  - `FakeNetworkPipe`：单机 / 离线场景本地回环调试
  - `MirrorSyncTransport`：联机场景 Mirror 消息传输
- **表现层分离**：逻辑在 `Update` / 网络 Tick 中推进，动画与视觉在 `CharacterLateUpdatePipeline` 中统一调度

> Player/NPC 即时权威 HP 结果、格挡/破防实际伤害，以及 NPC 的 revision 快照纠正链路已实现；Player 周期 HP 纠正和完整 Offline/Host/Client 验收仍待完成，详见[路线与进度](docs/ROADMAP_AND_PROGRESS.md)。

### AI

- NPC 基于 **Behavior Designer Pro** 行为树驱动（需本地安装）
- 与玩家共用 `CharacterIntent` 意图接口，支持 NavMesh 寻路与服务器侧 AI 门控

---

## 技术栈

| 类别 | 技术 |
|------|------|
| 引擎 | Unity **2022.3.44f1c1**（LTS） |
| 渲染 | Universal Render Pipeline (URP) |
| 网络 | [Mirror](https://github.com/MirrorNetworking/Mirror) |
| 输入 | Unity Input System |
| 相机 | Cinemachine |
| 导航 | AI Navigation |
| AI | Behavior Designer Pro（本地安装，不入库） |

---

## 场景说明

| 场景 | 路径 | 说明 |
|------|------|------|
| Offline | `Assets/Scenes/Offline.unity` | 离线单人调试，使用 FakeNetworkPipe |
| Online | `Assets/Scenes/Online.unity` | Mirror 联机测试 |
| AITest | `Assets/Scenes/AITest.unity` | AI / 行为树测试 |
| SampleScene | `Assets/Scenes/SampleScene.unity` | 基础示例场景 |

---

## 快速开始

### 环境要求

- Unity **2022.3.44f1c1** 或兼容的 2022.3 LTS 版本
- Git

### 克隆与打开

```bash
git clone git@github.com:WilliamRong/SS-3CDemoPrivate.git
cd SS-3CDemoPrivate
```

使用 Unity Hub 打开项目根目录，等待 Package Manager 解析依赖。

### 安装 Behavior Designer Pro（必需）

本项目的 NPC AI 依赖 [Behavior Designer Pro](https://assetstore.unity.com/packages/tools/visual-scripting/behavior-designer-pro-3-dots-powered-behavior-trees-368344)。该插件**不包含在本仓库**，请自行购买并从 Asset Store 导入到本地 `Packages/` 目录。

> Unity 重新打开后若 `Packages/packages-lock.json` 被写入了 `com.opsive.*` 引用，运行以下脚本清理后再提交：
>
> ```powershell
> powershell -ExecutionPolicy Bypass -File Tools/Git/strip-opsive-lock.ps1
> ```

### 安装 Git Hooks（推荐）

防止误提交商业插件资源：

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Git/install-hooks.ps1
```

---

## 项目结构

```
Assets/
├── Scripts/
│   ├── Character/
│   │   ├── Combat/           # 攻击定义、HitBox/HurtBox、伤害与格挡
│   │   ├── Controller/       # 玩家控制器与 Root Motion 转发
│   │   ├── StateMachine/     # 状态机与各状态实现
│   │   ├── Sync/             # 网络同步（快照、插值、传输层）
│   │   ├── Presentation/     # 动画表现层（Locomotion / Combat / Sprint）
│   │   ├── LockOn/           # 锁定目标系统
│   │   ├── Motor/            # 移动与物理
│   │   └── Config/           # ScriptableObject 配置
│   ├── AI/                   # NPC 行为树桥接、Driver、FSM 与 Motor
│   ├── Input/                # 输入处理
│   └── Core/                 # 全局数据、相机、游戏管理
├── Data/                     # 角色、相机、同步、表现与攻击数据
├── Prefabs/                  # Character / System / Terrain / UI
├── Scenes/                   # 场景
└── Mirror/                   # Mirror 网络框架（内嵌）

Tools/Git/                    # Git 工具脚本（hook、历史清理、lock 清理）
docs/                         # 当前项目总览、需求、进度与验证文档
AIContext/                    # 历史上下文（非当前事实源）
openspec/                     # 规格与变更记录
```

---

## 架构要点

### 逻辑与表现分离

```
Input / AI Intent
       ↓
PlayerController / NpcCharacterDriver
       ↓
CharacterStateMachine（逻辑状态）
       ├──→ CharacterMotor / NpcMotor
       ├──→ CombatActor → CombatResolver
       └──→ Publisher → Transport → RemoteInterpolator / RemoteActionApplier
                                      ↓
CharacterLateUpdatePipeline（LateUpdate 表现调度）
       ↓
Animator（Locomotion / Combat / Sprint Presenter）
```

表现优先级：**受击 / 闪避 / 攻击 → 格挡 → 冲刺 →  locomotion**

### 同步数据

- **StateSnapshot**：连续状态（位置、速度、StateId、锁定、连招；NPC 还包含权威 HP 与 revision）
- **ActionEvent**：离散动作与即时战斗结果（实际伤害、绝对 HP 与 revision）
- **NetTickClock**：统一网络 Tick，支持每帧多 Tick 推进

---

## 开发说明

- 角色配置通过 `CharacterDefinition`、`CharacterCombatConfig` 等 ScriptableObject 管理
- 离线模式可在不启动 Mirror Server 的情况下验证同步与表现链路
- 联机场景需确保 `MirrorSyncTransport` 与 `SyncBootstrap` 正确接线
- 改变需求、进度、架构或验收结果时，应同步更新对应 `docs/` 文档；不要用新增 `AIContext` 记录替代当前文档

---

## 许可证与第三方资源

- 本仓库代码与自研资源归项目作者所有
- **Behavior Designer Pro** 等为 Opsive 商业产品，请遵守其许可协议，**禁止**将插件本体提交到 Git
- Mirror 等开源依赖遵循各自许可证

---

## 仓库

- **GitHub（私有）**：https://github.com/WilliamRong/SS-3CDemoPrivate
