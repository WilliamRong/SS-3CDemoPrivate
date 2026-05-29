# SS&3CDemo

基于 Unity 的 **3C（Character / Camera / Control）动作战斗 Demo**，面向类魂系体验：锁定目标、连招、冲刺、闪避、格挡，并支持本地调试与 Mirror 联机同步。

> 本仓库为**私有项目**，仅包含自研代码与资源。第三方商业插件需自行安装，不包含在版本库中。

---

## 功能概览

### 角色控制（3C）

- **状态机驱动**：Idle / Move / Sprint / Attack / Dodge / Guard / Hit / Dead
- **战斗动作**：连招（Combo）、冲刺攻击、闪避攻击、重击、格挡（全身 / 上半身叠加）
- **锁定系统**：目标注册、切换、锁定下的移动与动画表现
- **相机**：Cinemachine 驱动的第三人称相机，支持锁定相机修正

### 网络同步

- **快照 + 事件** 双通道同步架构
  - `StateSnapshot`：位置、朝向、状态、冲刺阶段、格挡阶段、连招步数、锁定信息等
  - `ActionEvent`：状态切换等离散事件
- **双传输模式**
  - `FakeNetworkPipe`：单机 / 离线场景本地回环调试
  - `MirrorSyncTransport`：联机场景 Mirror 消息传输
- **表现层分离**：逻辑在 `Update` / 网络 Tick 中推进，动画与视觉在 `CharacterLateUpdatePipeline` 中统一调度

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
│   │   ├── Controller/       # 玩家 / NPC 控制器
│   │   ├── StateMachine/     # 状态机与各状态实现
│   │   ├── Sync/             # 网络同步（快照、插值、传输层）
│   │   ├── Presentation/     # 动画表现层（Locomotion / Combat / Sprint）
│   │   ├── LockOn/           # 锁定目标系统
│   │   ├── Motor/            # 移动与物理
│   │   └── Config/           # ScriptableObject 配置
│   ├── AI/                   # NPC 行为树桥接与状态
│   ├── Input/                # 输入处理
│   └── Core/                 # 全局数据、相机、游戏管理
├── Scenes/                   # 场景
└── Mirror/                   # Mirror 网络框架（内嵌）

Tools/Git/                    # Git 工具脚本（hook、历史清理、lock 清理）
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
       ↓
LocalSyncPublisher → Transport → RemoteInterpolator
       ↓
CharacterLateUpdatePipeline（LateUpdate 表现调度）
       ↓
Animator（Locomotion / Combat / Sprint Presenter）
```

表现优先级：**受击 / 闪避 / 攻击 → 格挡 → 冲刺 →  locomotion**

### 同步数据

- **StateSnapshot**：连续状态（位置、速度、当前 StateId、锁定、连招步等）
- **ActionEvent**：离散动作（如进入 Attack / Dodge 时的序号事件）
- **NetTickClock**：统一网络 Tick，支持每帧多 Tick 推进

---

## 开发说明

- 角色配置通过 `CharacterDefinition`、`CharacterCombatConfig` 等 ScriptableObject 管理
- 离线模式可在不启动 Mirror Server 的情况下验证同步与表现链路
- 联机场景需确保 `MirrorSyncTransport` 与 `SyncBootstrap` 正确接线

---

## 许可证与第三方资源

- 本仓库代码与自研资源归项目作者所有
- **Behavior Designer Pro** 等为 Opsive 商业产品，请遵守其许可协议，**禁止**将插件本体提交到 Git
- Mirror 等开源依赖遵循各自许可证

---

## 仓库

- **GitHub（私有）**：https://github.com/WilliamRong/SS-3CDemoPrivate
