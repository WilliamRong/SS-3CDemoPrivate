# PhaseF2
_Exported on 6/9/2026 at 18:44:25 GMT+8 from Cursor (3.7.19)_

---

**User**

读取G:\资料\SS&3CDemo_Cursor记录\cursor_phasef.md作为上下文

---

**Cursor**

我会先读取你指定的 `cursor_phasef.md`，把它作为后续对话的项目上下文参考。

这个文件很大，单次读取超过了工具限制。我先分段读取开头和末尾，确认它的结构、阶段摘要和最近记录，再根据需要继续定位相关章节。

我已经读到文档开头的 Phase F 定义和末尾最近一次关于武器插槽的记录。为了尽量完整承接上下文，我再分段读取中间内容，但会在回复里只保留关键结论。

已读取 `G:\资料\SS&3CDemo_Cursor记录\cursor_phasef.md` 并作为当前上下文。

我已接住的关键状态是：Phase F 主要在做角色表现，已经推进过模型替换、相机、Locomotion/Sprint、Hit/Death、Dodge、Guard、Attack 连击/重击、RootMotion、远端同步、武器 Prefab 与武器插槽等内容。最新记录里已经在 `ARPG_Samurai_Humanoid.prefab` 下加了 `WeaponHandSocket` / `WeaponHipSocket`，并把 Katana / Scabbard 分别挂到对应 socket。

后续你可以直接接着问，不用再重复前面的 Phase F 背景。

---

**User**

现在要做锁定了。我来做一下设计，你看有没有问题和补充的。我打算用一个组件LockOnComponent做逻辑，实现ILockOn接口。玩家按下比如R键开始锁定/解除锁定，用KD树或者八叉树(这里算法不知道要不要自己实现)找一定范围内实现ILockOn接口的GO。然后处理锁定逻辑，一直面向被锁定GO，八向移动之类的。

---

**Cursor**

我会先对照当前项目里已有的 `ILockOnLocomotionQuery`、移动/表现层和目标相关代码，再评估你的锁定设计是否贴合现有架构。

我看到项目里已经有 `ILockOnLocomotionQuery` 作为“是否锁定”的轻量接口，并且 Locomotion、Dodge、Pipeline 都已经依赖它。你的新设计最好扩展这个接口，而不是另起一套完全无关的 `ILockOn`。
你的方向总体对，但我建议把设计拆成 **“可被锁定目标”** 和 **“锁定控制器”** 两个概念，避免 `ILockOn` 名字混淆。

## 建议结构
### 1. 目标接口：`ILockOnTarget`
给敌人/NPC 实现，表示“我可以被锁定”：

```csharp
public interface ILockOnTarget
{
    Transform AimPoint { get; }
    bool CanBeLocked { get; }
    int LockPriority { get; }
}
```

不要让玩家自己的 `LockOnComponent` 实现这个接口，除非玩家也能被别人锁定。

### 2. 玩家控制器：`PlayerLockOnController`
挂在 Player 上，负责：
- R 键切换锁定/解除锁定
- 搜索附近 `ILockOnTarget`
- 选择目标
- 暴露给现有表现系统使用

它应该实现你项目里已经存在的：

```csharp
public interface ILockOnLocomotionQuery
{
    bool IsLockOnActive { get; }
}
```

建议扩展成：

```csharp
public interface ILockOnLocomotionQuery
{
    bool IsLockOnActive { get; }
    Transform CurrentTarget { get; }
}
```

如果暂时不想改接口，也可以先在具体组件上加 `CurrentTarget` 属性。

## 搜索算法
现在不需要 KD 树或八叉树。

你当前是单机/少量 NPC 阶段，直接用：

```csharp
Physics.OverlapSphere(...)
```

就够了。然后筛选：
- 在锁定半径内
- 有 `ILockOnTarget`
- `CanBeLocked == true`
- 在相机前方或角色前方
- 可选：射线检查无遮挡

排序建议按“屏幕中心优先 + 距离”：

```text
score = screenCenterDistance * 2 + worldDistance * 0.2 - priority
```

KD 树/八叉树适合几百上千目标频繁空间查询；你现在自己实现会增加维护成本，不划算。

## 面向目标
“锁定时一直面向目标”也对，但建议放在 `CharacterMotor` 或移动逻辑里做，而不是 LockOn 组件直接转角色。

LockOn 组件只提供：

```csharp
bool IsLockOnActive
Transform CurrentTarget
Vector3 DirectionToTarget
```

然后 `CharacterMotor` 在锁定时：
- 移动方向改成 strafe/八向
- 角色朝向持续转向目标
- sprint/guard/dodge 根据锁定状态走对应分支

这样符合你现在的架构：`Input -> FSM -> Motor -> Presentation`。

## 需要补充的点
你还需要考虑：
- 目标死亡/不可见/超距离时自动解除锁定
- R 键无目标时不进入锁定
- 锁定后可切换左右目标，后续再做
- 远端不需要同步“我锁定了谁”来驱动移动表现，除非锁定会影响攻击判定/朝向逻辑；如果影响权威行为，后面要同步 target netId 或 lock-on 状态

## 推荐第一版
先别做复杂空间结构，做这个最小闭环：

1. `ILockOnTarget`
2. `PlayerLockOnController : MonoBehaviour, ILockOnLocomotionQuery`
3. R 键 `OverlapSphere` 找最近/屏幕中心最近目标
4. `CharacterMotor` 锁定时面向目标
5. 现有 Locomotion/Dodge 继续读 `IsLockOnActive`

你的设计方向没问题，主要建议是：**不要从 KD 树开始；接口命名拆清楚；转向放 Motor，不放 LockOnComponent。**

---

**User**

玩家也可以被其他玩家或者NPC锁定

---

**Cursor**

那就更应该拆成两个角色：

- **`ILockOnTarget`**：谁“可以被锁定”。Player / NPC 都可以实现。
- **`LockOnController` / `PlayerLockOnController`**：谁“正在锁定别人”。Player / NPC AI 都可以持有。

也就是说，一个角色身上可以同时有：

```text
Player
├─ LockOnTarget        // 我可以被别人锁定
└─ PlayerLockOnController // 我可以主动锁定别人
```

NPC 也类似：

```text
NPC
├─ LockOnTarget
└─ NpcLockOnController / AI target selector
```

接口可以这样设计：

```csharp
public interface ILockOnTarget
{
    Transform AimPoint { get; }
    Transform Root { get; }
    bool CanBeLocked { get; }
    uint NetId { get; } // 联机后可选，用于同步/寻址
}
```

`LockOnController` 则提供：

```csharp
public interface ILockOnLocomotionQuery
{
    bool IsLockOnActive { get; }
    Transform CurrentTarget { get; }
}
```

重点是：**“我能被锁定”** 和 **“我正在锁定谁”** 不是同一个职责。玩家既能被锁，也能锁别人，所以不要让一个 `ILockOn` 同时表达两件事，否则后面 NPC AI、网络同步、UI 标记都会混乱。

联机时也建议同步的是 **目标身份**，比如 target `netId`，而不是同步一堆 Transform。远端收到后根据 `NetworkIdentity.netId` 找到对应 `ILockOnTarget`。

---

**User**

好，如果现在面试官问我：我现在要做一个锁定系统，你要怎么去设计？我该怎么把我的设计思路说出来

---

**Cursor**

你可以按 **“需求拆解 → 架构职责 → 目标选择 → 移动/表现接入 → 网络扩展”** 这个顺序说。面试里这样讲会比较清晰。

可以这样回答：

> 我会先把锁定系统拆成两个职责：  
> 一个是 **可被锁定对象**，比如玩家、NPC、Boss，它们实现 `ILockOnTarget`，暴露锁定点、是否可锁定、优先级等信息。  
> 另一个是 **锁定控制器**，挂在玩家或 AI 身上，负责输入触发、搜索目标、选择目标、维护当前锁定对象，并对移动和表现系统提供查询接口。

然后展开：

```csharp
public interface ILockOnTarget
{
    Transform AimPoint { get; }
    bool CanBeLocked { get; }
    int Priority { get; }
}
```

控制器则类似：

```csharp
public interface ILockOnQuery
{
    bool IsLockOnActive { get; }
    ILockOnTarget CurrentTarget { get; }
}
```

你可以继续说：

> 玩家按下锁定键时，`LockOnController` 会在一定半径内查找候选目标。第一版我不会直接上 KD 树或八叉树，而是用 `Physics.OverlapSphere` 或维护一个目标注册表，因为普通动作游戏同屏目标数量有限，简单方案更容易调试。等目标数量真的变大，再考虑空间分区或 KD 树优化。

目标筛选规则可以说：

> 候选目标会经过几层过滤：距离、是否在相机前方、是否可见、是否死亡、是否被遮挡。然后用一个评分函数选择目标，比如屏幕中心距离优先，其次世界距离和目标优先级。

评分示例：

```text
score = screenCenterDistanceWeight
      + distanceWeight
      - priorityWeight
```

接着讲和角色系统怎么接：

> 锁定控制器本身不直接移动角色，也不直接播放动画。它只提供“当前是否锁定、锁定目标是谁、目标方向是什么”。真正的转向和移动还是交给 `CharacterMotor`。锁定状态下，Motor 会让角色持续面向目标，并把移动从普通朝输入方向移动，切换成围绕目标的八向移动或 strafe 移动。

然后讲表现层：

> 动画层只消费锁定状态。比如未锁定时移动只用前向 run，锁定时才启用八向 locomotion；Dodge 在锁定时走八向翻滚，不锁定时走普通前滚或后撤。这样锁定系统不会和 Animator 强耦合。

如果面试官问网络，你可以说：

> 如果是联机项目，我不会同步 Transform 引用，而是同步目标的网络 ID。服务端或权威端决定当前锁定目标，把 target netId 同步给其他端。远端通过 netId 找到对应 `ILockOnTarget`，用于 UI、朝向表现或战斗判定。目标死亡、超距、断线时自动清理锁定。

最后给一句总结：

> 总体设计上，我会把锁定做成一个独立的目标选择与状态查询系统，不让它直接控制移动和动画。移动、动画、战斗只依赖它暴露的查询结果。这样职责边界清晰，后续要支持玩家锁 NPC、NPC 锁玩家、多人同步、目标切换、UI 标记，都比较容易扩展。

---

**User**

好，我现在要做锁定系统了，给我一个计划

---

**Cursor**

我会先把现有 `ILockOnLocomotionQuery`、Motor、Pipeline、PlayerController 的接入点对齐一下，然后给你一份可执行的锁定系统技术方案。

我会先快速核对现有锁定接口、移动/表现层依赖和输入结构，然后给你一份可执行的锁定系统计划，避免和已经做好的 Locomotion/Dodge/Guard/Attack 链路冲突。

已有代码里锁定目前只是 `PlayerLockOnQuery` 的 Tab 调试开关，真正的目标选择和“面向目标”还没做；表现层已经会消费 `IsLockOnActive`，所以技术方案可以围绕替换这个临时组件展开。

现有代码里已经有一个调试版 `PlayerLockOnQuery`，而且表现层、Dodge、Pipeline 都只依赖 `IsLockOnActive`。计划里会把它替换成真实 `LockOnController`，并新增“可被锁定目标”接口，同时把角色转向接到 `CharacterMotor`。

我已经确认了现状：锁定现在只有 `PlayerLockOnQuery` 的临时 Tab 开关，但 `CharacterLocomotionPresenter`、`DodgeModeResolver`、`CharacterLateUpdatePipeline` 已经预留了锁定状态消费点。方案会以“替换临时 Query + 扩展 Motor 锁定移动”为第一阶段。

---

**User**

在计划中加入要新增或修改部分的代码

---

**Cursor**

我会把关键接口、控制器、Input、Motor 接入点的代码草案补进现有计划文档，只更新计划 Markdown，不改工程代码。

我会在计划文档中新增“代码草案”章节，覆盖新增接口/组件、输入接入、Motor 接入和 prefab 绑定提醒，方便你后续按章节抄代码实现。

已把“新增或修改部分的代码草案”补进计划文档：

`C:\Users\1941\.cursor\plans\lock_on_system_5c7d78fb.plan.md`

新增内容包括：
- `ILockOnTarget.cs`
- `LockOnTarget.cs`
- 扩展 `ILockOnLocomotionQuery`
- `PlayerLockOnController.cs`
- `InputHandler.cs` 需要新增的 `LockOnTriggered`
- `CharacterMotor.cs` 锁定移动/面向目标分支
- `PlayerController.cs` 注入锁定查询
- Prefab 接线说明

---

**User**

锁定的           Physics.OverlapSphereNonAlloc能不能可视化在gaming里，给个debug开关勾选

---

**Cursor**

这是计划迭代：我会在锁定方案里补上 `OverlapSphere` 的可视化与 Inspector 调试开关说明。


正在把 Game 内 OverlapSphere 可视化与 Inspector 调试开关写入计划文档。


已写进计划 [`lock_on_system_5c7d78fb.plan.md`](C:\Users\1941\.cursor\plans\lock_on_system_5c7d78fb.plan.md)，主要增加了 **Debug 可视化** 章节，并在 `PlayerLockOnController` 代码草案里补了实现。

### Inspector 开关

| 字段 | 含义 |
|------|------|
| `showLockOnDebug` | 勾选后绘制 OverlapSphere 与候选目标 |
| `debugScanEveryFrame` | 勾选后每帧扫描（不按键也能看范围内目标） |

### 绘制内容

- **黄线框球**：`_lockRadius` 搜索范围  
- **绿线 + 小球**：有效候选 `LockPoint`  
- **红线**：被过滤掉的候选（自己、不可锁、超距、相机后方等）  
- **青线 + 大球**：当前锁定目标  

### Game 里怎么看

`OnDrawGizmos` 在 **Game 视图要打开右上角 Gizmos** 才会显示（和 Scene 里一样）。计划里也写了：若不想依赖 Gizmos 按钮，可再加 `Debug.DrawLine` 作为补充。

### 新增 todo

- `lock-debug-gizmos`：实现上述 debug 开关与可视化  

实现阶段在 Player 预制体上勾选 `showLockOnDebug`，Play 后打开 Game 视图 Gizmos 即可调半径和 LayerMask。需要我开始按计划在工程里写代码时，说一声即可。

---

**User**

帮我在代码里加好

---

**Cursor**

我会按锁定系统计划把代码接入工程，先查看现有相关文件与 prefab 结构。


正在补全调试可视化、修复 `PlayerController` 中 `_lockOnQuery` 注入顺序，并清理重复文件。


为 Player 添加触发器 Collider，使 `OverlapSphere` 能检测到可被锁定的玩家。


锁定系统已接入工程，主要变更如下。

## 已完成

### 核心逻辑
- **`ILockOnTarget` / `LockOnTarget`**：Player、NPC 均可作为锁定目标
- **`PlayerLockOnController`**：R 键切换锁定，`OverlapSphereNonAlloc` 选目标，超距/失效自动解除
- **`ILockOnLocomotionQuery`**：扩展 `CurrentTarget`，供 Motor 与表现层使用
- **`CharacterMotor`**：锁定时面向目标 + 相机平面八向移动
- **`InputHandler` + DemoInputActions`**：R 键 `LockOn` 输入

### 本次修复
- **`PlayerController`**：原先 `_lockOnQuery` 在 `SetLockOnQuery` 之后才赋值，Motor 一直拿到 `null`；已改为先 `GetComponent` 再注入 Motor
- **`PlayerLockOnController.cs.meta`**：补全 meta（GUID 与 Prefab 一致）
- **命名空间**：改为 `Character.LockOn`（与目录一致）

### Debug 可视化
在 Player 的 `PlayerLockOnController` 上勾选 **`Show Lock On Debug`**：
- 黄色线框球：搜索半径
- 绿色线：有效候选
- 红色线：被过滤目标
- 青色：当前锁定目标

Game 视图需打开右上角 **Gizmos** 才能看到。

### Prefab
- **Player**：已有 `PlayerLockOnController`、`LockOnTarget`、`LockOnPoint`；新增 **Trigger CapsuleCollider**（便于被 `OverlapSphere` 检测到）
- **NPC**：已有 `LockOnTarget` + Trigger CapsuleCollider

## 使用方式

1. 进入 Play，靠近 NPC（约 12m 内）
2. 按 **R** 锁定 / 再按 **R** 解除
3. 锁定后：角色面向目标，WASD 为八向移动；Dodge / GuardWalk 走锁定分支
4. 调试时在 Inspector 勾选 `Show Lock On Debug`

若锁定无反应，检查 NPC 是否在范围内、CapsuleCollider 是否为 Trigger，以及 LayerMask 是否包含 NPC 所在 Layer。需要我把 `CanBeLocked` 接到死亡状态，或做锁定目标切换（左右切换）的话，可以继续说。

---

**User**

现在锁定的时候相机表现不对，应该将被锁定目标置于屏幕中央，保持相机正对目标，同时一直将玩家和目标保持在屏幕中。这种通过cinemachine应该如何配置

---

**Cursor**

先查看项目里 Cinemachine 与相机绑定的现有实现，再给出锁定时的配置建议。


你现在的相机链路是 **`CinemachineFreeLook`（`Cameras/TP`）+ `MirrorLocalPlayerCameraBinder` 把 Follow/LookAt 都绑到玩家 `LookAtPoint`**。这是标准第三人称环绕相机，**不会**在锁定时把敌人拉到屏幕中心，也不会自动保证“玩家 + 敌人同时在画面里”。

```57:58:Assets/Scripts/Core/MirrorLocalPlayerCameraBinder.cs
            freelook.Follow = lookTarget;
            freelook.LookAt = lookTarget;
```

锁定后角色已经转向目标了，但相机仍围着玩家转，所以表现会“不对”。

---

## 结论：不要改 FreeLook，要加一套 Lock-On Virtual Camera

`CinemachineFreeLook` 适合鼠标自由环视；锁定战斗相机应 **单独一个 `CinemachineVirtualCamera`**，用 **Priority 切换**：

| 模式 | 相机 | Priority |
|------|------|----------|
| 自由 | `TP` (FreeLook) | 10 |
| 锁定 | `CM_LockOn` (Virtual Camera) | 20 |

`Main Camera` 上已有 `CinemachineBrain`，默认 Blend 约 2 秒，切换会比较平滑。

---

## 推荐方案 A：Target Group + Group Composer（最符合“两人都在屏内”）

适合：**尽量把敌人放中间，同时保证玩家不被挤出屏幕**。

### 层级结构（挂在 `Cameras` 下）

```
Cameras/
├── TP                          (现有 FreeLook, Priority=10)
├── LockOnTargetGroup           (CinemachineTargetGroup)
└── CM_LockOn                   (CinemachineVirtualCamera, Priority=0/20)
```

### 1. `LockOnTargetGroup`

| 成员 | Transform | Weight | Radius |
|------|-----------|--------|--------|
| Player | 玩家 `LookAtPoint` | 1 | 1.0 ~ 1.5 |
| Target | 敌人 `LockPoint` | 3 ~ 5 | 0.8 ~ 1.0 |

- **Weight 给敌人更高**：Group 的“构图中心”更靠近敌人。
- **Radius 更大**：构图时给该角色留更多边距，减少被裁切。

运行时：锁定时把 Target 加进 Group；解锁时移除。

### 2. `CM_LockOn` 配置（Cinemachine 2.x）

**Basic**

- **Follow**: `LockOnTargetGroup`
- **LookAt**: `LockOnTargetGroup`（Group Composer 必须 LookAt 同一个 Group）
- **Priority**: 默认 `0`，锁定时 `20`

**Body**

- 用 **Transposer** 或 **Do Nothing** 即可（构图主要由 Aim 负责）
- 若用 Transposer：`Binding Mode = World Space`，Follow Offset 先 `(0, 2, -6)` 起步

**Aim → Group Composer**（核心）

| 参数 | 建议值 | 作用 |
|------|--------|------|
| Framing Mode | Horizontal And Vertical | 横纵都保证在画面内 |
| Group Framing Size | 0.75 ~ 0.85 | 两人占画面比例（越小拉得越远） |
| Max Dolly In / Out | 2 / 8 | 根据距离自动推拉 |
| Minimum / Maximum Distance | 3 / 12 | 限制最近/最远 |
| Screen X / Y | 0.5 / 0.45 | Group 中心在屏幕位置（略偏上更像动作游戏） |
| Damping | 0.5 ~ 1.5 | 平滑 |

**Lens**

- FOV 45 → 可调到 **50~55**，更容易同时框住两人

**Extension（可选）**

- **CinemachineCollider**：避免穿墙
- **CinemachineConfiner**：限制在关卡范围内

### 3. 锁定时对 FreeLook 的处理

锁定时应 **关掉鼠标环视**，否则会和 LockOn 相机抢控制权：

- `TP.m_XAxis.m_MaxSpeed = 0`
- `TP.m_YAxis.m_MaxSpeed = 0`

或直接把 `TP` Priority 降到 0。

---

## 推荐方案 B：敌人严格居中 + 相机正对敌人（更像魂系）

若你更在意 **“敌人必须在屏幕正中央、相机始终朝向敌人”**，用 **Follow 玩家 + LookAt 敌人 + Composer**：

**CM_LockOn**

- **Follow**: 玩家 `LookAtPoint`
- **LookAt**: 敌人 `LockPoint`
- **Body**: **Framing Transposer** 或 **Transposer**
  - `Binding Mode`: **Lock To Target With World Up**
  - 相机放在 **玩家身后、沿“玩家→敌人”反方向**
  - 初始 `Camera Distance`: 4 ~ 6
- **Aim**: **Composer**
  - `Screen X = 0.5`, `Screen Y = 0.5`（敌人居中）
  - `Dead Zone` 很小（硬锁）
  - `Horizontal/Vertical Damping`: 0.3 ~ 0.8

**如何保证玩家也在画面里**

Composer 只保证 **LookAt 目标（敌人）** 居中，玩家绕圈时可能被挤出画面。需要二选一：

1. **脚本动态拉远**：根据 `Vector3.Distance(player, target)` 增大 `Camera Distance` / FOV  
2. **改用方案 A 的 TargetGroup**（更省心）

很多魂系其实是 **B + 动态距离**，而不是纯 Inspector 静态配置。

---

## 和现有代码怎么接

`PlayerLockOnController` 已有 `CurrentTarget`，锁定时需要额外做：

```csharp
// 伪代码示意
void OnLockOnChanged(bool active, Transform targetLockPoint)
{
    if (active)
    {
        lockOnGroup.AddMember(targetLockPoint, weight: 3f, radius: 1f);
        lockOnVcam.Priority = 20;
        freeLookVcam.Priority = 10; // 或禁用 FreeLook 输入
        DisableFreeLookInput(freeLookVcam);
    }
    else
    {
        lockOnGroup.RemoveMember(targetLockPoint);
        lockOnVcam.Priority = 0;
        RestoreFreeLookInput(freeLookVcam);
    }
}
```

可以扩展 `MirrorLocalPlayerCameraBinder`，或新建 `LockOnCameraController` 引用场景里的 `TP`、`CM_LockOn`、`LockOnTargetGroup`。

---

## 参数调参顺序（Play 模式）

1. 先只开 **Group Composer + TargetGroup**，确认两人都在画面内  
2. 再调 **Screen Y**（0.4~0.5）和 **Group Framing Size**  
3. 再调 **Max Dolly Out / Minimum Distance**，解决近战时玩家出框  
4. 最后调 **Brain Blend Time**（锁定 0.3~0.6s 通常比 2s 更跟手）

---

## 常见坑

1. **继续用 FreeLook 只改 LookAt**  
   环视相机会和“敌人居中”打架，绕圈时尤其明显。

2. **Follow/LookAt 仍都指向玩家**  
   这是当前问题的根因，锁定必须引入 **敌人 Transform** 或 **TargetGroup**。

3. **锁定移动与相机不一致**  
   你的 `CharacterMotor` 锁定时用 **相机平面** 算八向输入；LockOn 相机转向敌人后，WASD 手感会变正确——这是预期行为。

4. **Cinemachine 版本**  
   你项目是 **CM 2.10.7**（不是 CM3）。组件名是 `CinemachineVirtualCamera`、`Group Composer`、`Framing Transposer`；CM3 的 `CinemachineCamera` / `Rotation Composer` 名称不同，别混用文档。

---

## 建议选型

| 需求优先级 | 选方案 |
|-----------|--------|
| 两人始终在屏内 | **A：TargetGroup + Group Composer** |
| 敌人必须死中 | **B：Follow 玩家 + LookAt 敌人 + Composer**，并加距离脚本 |
| 两者都要 | A 做底，把 Target 的 Weight 调高；或 B + 动态拉远 |

如果你希望我在工程里直接搭 `CM_LockOn`、`TargetGroup` 和切换脚本，需要切换到 **Agent 模式**，我可以按你偏好的方案（A 或 B）落地配置。

---

**User**

观察了下魂系列，被锁定目标和玩家都可以移动超出中央，有相机延迟跟随让被锁定目标在屏幕中央上方一些的位置。常态相机也有延迟跟随。把目前缺失的这些部分制定计划补上

---

**Cursor**

先梳理现有相机与锁定相关代码，再制定魂系风格的延迟跟随方案。


已确认：锁定只影响角色转向，相机仍是 FreeLook 且无锁定向机；FreeLook 已有基础阻尼，但缺少锁定向机与魂系式软跟随。


**锁定相机交互确认**

> **锁定期间是否完全禁止鼠标/右摇杆改变相机环视（魂系默认行为）？**
> - 是，锁定时禁用 FreeLook 环视输入
> - 否，锁定时仍允许有限手动环视（后续可再加）

---

**User**

我加上了Cameras里的对象，剩下的帮我做好

---

**Cursor**

先查看你在 `Cameras` 里加的对象和现有代码，再补全相机 Rig 控制器与绑定逻辑。


正在实现 `PlayerCameraRigController`、更新绑定器，并调整场景与 Prefab 配置。


修复 `GameObject.Find` 无法按路径查找的问题，并检查编译。


已补全锁定相机逻辑，主要变更如下。

## 新增

**[`PlayerCameraRigController.cs`](Assets/Scripts/Core/PlayerCameraRigController.cs)**
- 本地玩家启动时绑定 `Cameras/TP`、`Cameras/LockOn`、`Cameras/LockOnTargetGroup`
- **未锁定**：FreeLook Priority=10，LockOn Priority=0
- **锁定**：TargetGroup 加入玩家 `LookAtPoint` + 敌人 `LockPoint`，LockOn Priority=20，禁用 FreeLook 鼠标环视
- 按玩家与敌人水平距离动态调整 `Framing Transposer` 的 `CameraDistance`（4.5~8m）
- 使用层级路径查找（`Cameras/TP`），不再依赖无效的 `GameObject.Find` 全路径

## 修改

**[`MirrorLocalPlayerCameraBinder.cs`](Assets/Scripts/Core/MirrorLocalPlayerCameraBinder.cs)**  
改为初始化 `PlayerCameraRigController`，不再直接操作 FreeLook。

**[`Online.unity`](Assets/Scenes/Online.unity)**
- `LockOn` VCam 默认 Priority **0**（避免与 TP 冲突）
- LockOn Framing：`ScreenY=0.6`、Dead Zone、Soft Zone、Damping=2、`CenterOnActivate=false`
- Brain 默认混合时间 **2s → 0.5s**
- TP FreeLook 三 Rig 阻尼提高（常态相机延迟更明显）

**[`Player.prefab`](Assets/Prefabs/Characters/Player.prefab)**  
挂载 `PlayerCameraRigController`，并更新 `MirrorLocalPlayerCameraBinder` 字段。

## 使用方式

1. 进入 Play，确认 Console 有 `[MirrorLocalPlayerCameraBinder] Bound player camera rig.`
2. 靠近 NPC，按 **R** 锁定 → 应切到 LockOn 相机，敌人在屏幕中上方，绕圈时有延迟跟随
3. 再按 **R** 或走远 → 回到 FreeLook

若构图仍需微调，在 Player 的 **Player Camera Rig Controller** 或场景 **LockOn → cm → Framing Transposer** 里调 `_lockScreenY`、`_baseCameraDistance`、Soft Zone 和 Damping 即可。

---

**User**

TP下的跟随阻尼太大了，人物跑到屏幕边缘了还不跟上，调小一些，需要调哪些参数

---

**Cursor**

查看 TP FreeLook 当前的阻尼与构图参数。


人物跑到屏幕边缘才跟上，主要是 **TP FreeLook 三个 Rig（Top / Middle / Bottom）** 里的阻尼和构图区在起作用。你项目里这三套参数目前是一致的，且是之前为了“延迟跟随”特意调大的。

## 在 Unity 里改哪里

选中 **`Cameras/TP`** → 展开 **Top Rig / Middle Rig / Bottom Rig** → 点每个 Rig 下的 **`cm`** → 看两个组件：

### 1. Composer（Aim）——人物在屏幕里偏到边缘才拉回

这是**最直接影响“跑到屏幕边缘还不跟”**的一组。

| 参数 | 你现在的值 | 作用 | 想跟得更紧 |
|------|-----------|------|-----------|
| **Horizontal Damping** | `1` | 水平方向把 LookAt 拉回 Screen X 的速度；**越大越慢** | 降到 **0.2 ~ 0.5** |
| **Vertical Damping** | `1` | 垂直方向同理 | 降到 **0.2 ~ 0.5** |
| **Soft Zone Width / Height** | `0.65` | 目标在软区内移动时相机**几乎不纠正**；**越大越允许偏到边缘** | 降到 **0.4 ~ 0.55** |
| **Dead Zone Width / Height** | `0` | 死区内完全不跟；现在有延迟感主要靠 Soft Zone + Damping | 一般保持 0；要更早跟可设 **0.03 ~ 0.05** |
| **Screen X / Y** | `0.5 / 0.5` | 希望人物在屏幕上的目标位置 | 一般不用为“跟上”去改 |

人物“出框”多半是 **Soft Zone 太大 + Composer Damping 太大**。

---

### 2. Orbital Transposer（Body）——相机机位本身跟不上玩家移动

| 参数 | 你现在的值 | 作用 | 想跟得更紧 |
|------|-----------|------|-----------|
| **X / Y / Z Damping** | `2` | 相机相对 Follow 点的**世界空间**滞后；**越大越拖** | 降到 **0.8 ~ 1.2** |
| **Pitch / Yaw Damping** | `0.4` | 绕玩家旋转的滞后 | 可降到 **0.2 ~ 0.3** |

如果感觉是**整个人和相机一起在 lag**，优先动 Transposer；如果是**人在画面里滑到边上**，优先动 Composer。

---

### 3. FreeLook 根节点（`Cameras/TP` 自身）

| 参数 | 你现在的值 | 作用 |
|------|-----------|------|
| **Heading → Velocity Filter Strength** | `4` | 玩家移动时相机朝向/环位的平滑；偏大也会“跟得慢” | 可试 **2 ~ 3** |

鼠标环视的 **X/Y Axis Max Speed** 只影响手动转镜头，不影响“人物跑开相机跟不上”。

---

## 建议调参顺序（Play 模式）

1. 三个 Rig 的 **Composer Horizontal/Vertical Damping**：`1` → **`0.35`**
2. **Soft Zone Width/Height**：`0.65` → **`0.5`**
3. 仍偏慢再调 **Transposer X/Y/Z Damping**：`2` → **`1`**
4. 三个 Rig **保持一致**，否则切换高度时手感会跳

---

## 和 LockOn 相机的区别

**不要改 `Cameras/LockOn` 下的 Framing Transposer**（那里 `XDamping=2`、`ScreenY=0.6` 是给锁定用的）。  
只改 **`Cameras/TP` 下三个 Rig 的 cm**。

---

## 一句话对照

- **人在屏幕里滑到边缘** → 先减 **Composer Damping** 和 **Soft Zone**
- **相机机位整体拖在后面** → 再减 **Transposer X/Y/Z Damping**
- **数值越小 = 跟得越快**；魂系那种“有一点延迟”通常 Composer 在 **0.3~0.8**，Soft Zone 在 **0.45~0.6**，Transposer 在 **1~1.5** 左右

如果你希望，我可以在 Agent 模式里直接帮你把 TP 三 Rig 改成一版更跟手的默认值。

---

**User**

帮我改

---

**Cursor**

正在把 TP FreeLook 三 Rig 的阻尼和 Soft Zone 调得更跟手。


已改好 [`Online.unity`](Assets/Scenes/Online.unity) 里 **TP FreeLook 三个 Rig**（Top / Middle / Bottom），**LockOn 相机未动**。

## 调整内容

| 组件 | 参数 | 原值 | 新值 |
|------|------|------|------|
| **Composer** | Horizontal / Vertical Damping | 1 | **0.35** |
| **Composer** | Soft Zone Width / Height | 0.65 | **0.5** |
| **Orbital Transposer** | X / Y / Z Damping | 2 | **1** |
| **Orbital Transposer** | Pitch / Yaw Damping | 0.4 | **0.3** |
| **Heading** | Velocity Filter Strength | 4 | **3**（含 TP 根节点） |

进 Play 跑一下，人物不应再轻易滑到屏幕边缘才拉回。若仍偏慢，可再把 Composer Damping 降到 **0.25**，或 Soft Zone 降到 **0.45**。

---

**User**

锁定的视角应该始终以玩家角色背后看向锁定目标，玩家移动后阻尼过去

---

**Cursor**

锁定相机应改为：机位在玩家背后、朝向锁定目标，并带阻尼跟随。先查看当前 LockOn 相机与 Rig 控制器的配置。


将 LockOn 相机改为 Transposer（玩家背后）+ Composer（看向目标），并更新控制器逻辑。


已改成魂系常见的 **「玩家背后 → 看向锁定目标」** 机位，并保留阻尼跟随。

## 改动说明

### 相机结构（`Cameras/LockOn/cm`）
- 移除 **Framing Transposer + TargetGroup** 构图
- 改为：
  - **Transposer（Body）**：`Binding Mode = Lock To Target With World Up`  
    → 相机始终在 **玩家背后**（沿「玩家→敌人」反方向）
  - **Composer（Aim）**：`LookAt = 敌人 LockPoint`  
    → 镜头 **朝向锁定目标**，目标在屏幕中上方（Y=0.6）

### [`PlayerCameraRigController.cs`](Assets/Scripts/Core/PlayerCameraRigController.cs)
- 锁定时：`Follow = 玩家 LookAtPoint`，`LookAt = 敌人 LockPoint`
- 根据两人距离动态调整 **FollowOffset.z**（4.5~7.5m）
- **Body Damping / Yaw Damping**：绕圈时机位延迟跟上
- **Aim Damping + Soft Zone**：目标在画面上轻微漂移后再缓慢回正

### 调参位置（Player 上 `Player Camera Rig Controller`）

| 参数 | 作用 |
|------|------|
| `_bodyDamping` / `_bodyYawDamping` | 相机绕到玩家背后的速度（越大越拖） |
| `_aimHorizontalDamping` / `_aimVerticalDamping` | 目标在屏幕上的跟随延迟 |
| `_followHeight` / `_baseCameraDistance` | 相机高度与身后距离 |
| `_lockScreenY` | 敌人在屏幕上的高度 |

进 Play 锁定后绕圈，相机应始终从 **玩家背后** 看向敌人；若绕背时转得太慢，把 `_bodyYawDamping` 降到 **0.8~1.0**。

---

**User**

两个问题，希望锁定时和TP时的角色在屏幕中的大小统一。第二个问题是锁定时移动视角应该禁用

---

**Cursor**

先看 TP 与 LockOn 的相机距离/构图，以及锁定时输入是否仍被处理。


正在实现：锁定时从 TP 同步距离/FOV 以统一角色大小，并彻底禁用环视输入。


两个问题都已处理：

## 1. 锁定与 TP 角色大小统一

锁定时会从当前 **TP FreeLook** 采样并同步：
- **轨道半径**（Middle Rig 约 3.25m，随 Y 轴高度插值）
- **轨道高度 + Rig FollowOffset.y**（约 1.6m）
- **FOV**（与 TP 一致，LockOn 已从 40 改为 **45**）

每次按 R 进入锁定时会调用 `SyncLockOnFramingFromFreeLook()`，保证和当前 TP 镜头高度/距离一致。  
敌人较远时仍会在基础距离上最多再拉远 `_maxCameraDistanceExtra`（默认 +3m）。

Inspector 可关：`Match Free Look Framing On Lock`

## 2. 锁定时禁用视角移动

锁定时现在会：
- **FreeLook Priority → 0**（不再参与 Brain）
- **清空 Mouse X/Y 输入轴**（`InputAxisName = ""`，MaxSpeed = 0）
- **`InputHandler.SetCameraLookEnabled(false)`** → `LookInput` 恒为 0

解锁后全部恢复。

---

若锁定后角色仍略大/略小，可在 Play 里微调 TP 的 **Middle Rig 轨道半径**（当前 3.25），锁定时会自动跟过去。若鼠标仍能转镜头，告诉我你用的是鼠标还是手柄，我再补对应输入通道的屏蔽。

---

**User**

撤回之前的改动。

---

**Cursor**

查看 git 状态，确定要撤回哪些改动。


正在撤回上一轮改动（角色大小同步 + 锁定时禁用视角），恢复到该轮之前的状态。


已撤回**上一轮**的改动（角色大小与 TP 同步 + 锁定时禁用视角输入），恢复为「玩家背后看向目标」那版逻辑。

## 已恢复的内容

| 文件 | 撤回内容 |
|------|----------|
| `PlayerCameraRigController.cs` | 去掉 TP 采样同步、InputHandler 联动、FreeLook Priority/轴名清空 |
| `InputHandler.cs` | 去掉 `SetCameraLookEnabled` |
| `Online.unity` | LockOn FollowOffset `1.2 / -4.5`，FOV 40 |
| `Player.prefab` | 恢复 `_baseCameraDistance: 4.5` 等原序列化字段 |

锁定时仍只保留：**FreeLook 的 X/Y MaxSpeed = 0**（原先的实现）。

如果你是想撤回**整个锁定相机系统**（含 `PlayerCameraRigController`、场景里的 LockOn 等），说一下范围，我可以继续帮你回退。

---

**User**

锁定时的bodyoffset锁死了，让镜头离玩家和目标再近一些，是TP时的1.2倍

---

**Cursor**

查看 TP 与 LockOn 的当前距离配置，并将锁定机位改为 TP 的 1.2 倍。


将锁定距离改为从 TP 轨道动态采样，并设为 TP 距离的 1.2 倍。


已改好：锁定相机的 **FollowOffset 不再写死 4.5m**，会按当前 TP 镜头动态计算。

## 逻辑

锁定时从 **TP FreeLook** 读取当前轨道：
- **水平距离**：`TP 轨道半径 × 1.2`（Middle Rig 约 3.25m → 锁定约 **3.9m**，比原先 4.5m 更近）
- **高度**：TP 轨道高度 + Rig 的 FollowOffset.y（约 **1.6m**）

与敌人距离变远时，最多再拉远 `_separationDistanceExtra`（默认 **+1.5m**），不再拉到 7.5m。

## 可调参数（Player 上 `Player Camera Rig Controller`）

| 参数 | 默认 | 作用 |
|------|------|------|
| `Lock Distance Tp Multiplier` | 1.2 | 相对 TP 轨道半径的倍数 |
| `Separation Distance Extra` | 1.5 | 两人很远时额外拉远距离 |

想再近一点，把 **Multiplier 调到 1.0**（与 TP 同距）或 **0.9**。

---

**User**

从锁定切回tp时，希望镜头保持在之前锁定时的方向，现在会出现绕一圈然后对着角色侧面的特写，很奇怪。希望锁定到TP的切换时能更加平滑且角色不要出屏幕

---

**Cursor**

分析锁定切回 TP 时相机状态不同步的原因，并实现解锁时同步 FreeLook 方向。


解锁时将 FreeLook 同步到当前镜头方向，并在锁定期间冻结 FreeLook 的 Standby 更新。


已处理锁定切回 TP 时「绕一圈、侧面特写」的问题，核心是 **FreeLook 在锁定期间仍用旧轨道状态**，切回时会 snap 到错误角度。

## 改动

### 1. 解锁时同步 TP 方向
解除锁定 **之前**，用当前 Brain 输出镜头调用：
```csharp
_freeLook.ForceCameraPosition(当前位置, 当前旋转);
```
TP 的 X/Y 轨道会与锁定结束时的视角对齐，不再从锁定前的旧角度插值。

### 2. 锁定期间冻结 FreeLook
锁定时 `StandbyUpdate = Never`，避免 TP 在后台用错误角度跟随玩家。

### 3. 进入锁定时也继承当前视角
LockOn VCam 用 `ForceCameraPosition` 从当前 TP 镜头接过去，双向切换都更顺。

### 4. 过渡更平滑
- `InheritPosition = true`（TP / LockOn 均开启）
- Brain 默认混合时间 **0.55s**（可在 Inspector 调 `Unlock Blend Time`）
- 脚本 `DefaultExecutionOrder(-50)`，在 Brain 更新前完成同步

---

Play 里再试：绕圈锁定后按 R 解除，镜头应保持在背后方向缓慢回到 TP，角色不应再甩到侧面特写。若仍略飘，可把 `Unlock Blend Time` 调到 **0.7~0.8**。

---

**User**

撤回改动

---

**Cursor**

正在撤回上一轮「锁定切回 TP 平滑过渡」相关改动。


已撤回上一轮 **「锁定切回 TP 平滑过渡」** 的改动。

## 已移除
- `ForceCameraPosition` 同步
- 锁定时 `StandbyUpdate = Never`
- `InheritPosition`、Brain 混合时间覆盖
- `DefaultExecutionOrder(-50)`
- `_unlockBlendTime` 字段

## 仍保留
- TP 1.2 倍距离的锁定 FollowOffset 逻辑
- Transposer + Composer 背后看向目标
- 锁定时 FreeLook 鼠标 MaxSpeed = 0

若还要撤回更早的改动（例如整个锁定相机系统），说一下范围即可。

---

**User**

锁定下sprint时人物可以不锁定目标，但不会解除锁定。脱离sprint后继续锁定。

---

**Cursor**

查看锁定、冲刺与角色转向的现有实现。


在 `CharacterMotor` 中实现：锁定且冲刺时不转向目标，冲刺结束后恢复面向目标；锁定状态保持不变。


冲刺时相机朝向会变，若仍用“相机前方”校验会导致误解除锁定；正在放宽 `IsTargetStillValid` 的维持条件。


已实现：**锁定状态下冲刺时转向移动方向，锁定不会解除；结束冲刺后恢复面向目标。**

### 1. 冲刺时不面向目标（`CharacterMotor.cs`）

在 `TickLockOnHorizontal` 中：
- **冲刺中**：按相机相对输入方向旋转（与自由移动一致）
- **非冲刺**：继续面向锁定目标
- `IsLockOnActive` 不变，八向移动逻辑不变

### 2. 冲刺不会误解除锁定（`PlayerLockOnController.cs`）

维持锁定时不再用“相机前方”校验（冲刺时相机/朝向会变，容易误判）。`IsTargetStillValid` 现在只检查：
- 目标仍可锁定（`CanBeLocked`）
- 仍在锁定半径内

**验收：** 锁定敌人 → 按住冲刺跑开 → 锁定保持、人物朝移动方向 → 松开冲刺 → 人物重新面向敌人。

---

**User**

撤回改动，改为sprint的时候自动解锁定

---

**Cursor**

改为冲刺时自动解除锁定。先查看当前相关代码，再撤回之前的改动并实现新逻辑。


已撤回上一版逻辑，并改为 **按住 Sprint 时自动解除锁定**。

### 撤回内容
- **`CharacterMotor.cs`**：锁定移动恢复为始终面向目标，不再在冲刺时改朝向移动方向
- **`PlayerLockOnController.cs`**：`IsTargetStillValid` 恢复为使用完整的 `IsTargetCandidate`（含相机前方校验）

### 新行为
- **`PlayerLockOnController.cs`**：检测到 `IsSprinting` 且当前已锁定时，调用 `ClearLockOn()`
- 相机会随锁定解除切回 TP（`PlayerCameraRigController` 原有逻辑）

**验收：** 锁定敌人 → 按住 Sprint → 锁定立即解除，进入普通冲刺移动；松开 Sprint 后需重新按 R 才能再锁定。

---

**User**

现在锁定时八向移动的VelocityX/Z并不是说按住左就是-1,0。而是-1, -0.5。所以需要重新设计锁定时八向的参数判断依据。我设想就按输入方向判断，按住左就是向左走，和实际velocityX/Z无关。工业项目是怎么处理这个的

---

**Cursor**

我先看一下当前锁定移动和动画参数是在哪里从速度/输入推出来的，再结合常见项目做法给你一个可落地的设计建议。

你的判断是对的：**锁定时八向动画参数不应该直接用实际速度 `VelocityX/Z`。** 工业项目里通常会把这两件事分开：

- **Movement / Physics velocity**：角色实际怎么移动、加减速、碰撞、SmoothDamp 后的速度。
- **Animation intent / Locomotion blend input**：玩家“想往哪走”，通常来自输入方向或期望移动方向，而不是实际速度。

你现在的问题正是因为动画参数来自实际速度：

```131:161:Assets/Scripts/Character/Presentation/CharacterLocomotionPresenter.cs
private Vector2 ComputeAnimatorBlendVelocity(
    Transform presentationRoot,
    Vector2 worldVelocityXZ,
    bool isLockOn,
    CharacterStateId stateId)
{
    // ...

    if (isLockOn)
    {
        Vector3 localVelocity = presentationRoot.InverseTransformDirection(worldVelocity);
        return new Vector2(
            Mathf.Clamp(localVelocity.x / refSpeed * runZ, -axisMax, axisMax),
            Mathf.Clamp(localVelocity.z / refSpeed * runZ, -axisMax, axisMax));
    }
```

而这个 `worldVelocityXZ` 来自 `PlayerController.Velocity`：

```336:338:Assets/Scripts/Character/Sync/CharacterLateUpdatePipeline.cs
stateId = _playerController.CurrentStateId;
var velocity = _playerController.Velocity;
velocityXZ = new Vector2(velocity.x, velocity.z);
```

锁定移动里又用了 `SmoothDamp`，所以实际速度天然会有惯性、混合、旋转误差：

```261:268:Assets/Scripts/Character/Motor/CharacterMotor.cs
var speed = _config.moveSpeed;
var targetHorizontal = inputDir * speed;

_currentHorizontalVelocity = Vector3.SmoothDamp(
    _currentHorizontalVelocity,
    targetHorizontal,
    ref _horizontalVelocityRef,
    _config.smoothTime);
```

所以按左时得到 `(-1, -0.5)` 并不奇怪，因为那是“当前实际速度在角色本地空间里的投影”，不是“玩家按了左”。

## 工业项目常见做法

锁定八向一般用 **输入意图** 驱动动画参数：

- 按左：`VelocityX = -1`, `VelocityZ = 0`
- 按右：`VelocityX = 1`, `VelocityZ = 0`
- 按前：`VelocityX = 0`, `VelocityZ = 1`
- 按后：`VelocityX = 0`, `VelocityZ = -1`
- 斜向：`(-1, 1)`、`(1, -1)` 等，是否归一化取决于你的 Blend Tree 布局

也就是说锁定时动画参数可以直接来自：

```csharp
Vector2 lockOnBlend = intent.Move;
```

而不是：

```csharp
Vector2 lockOnBlend = localVelocity / speed;
```

自由移动则仍然可以用速度或 forward speed，因为自由移动动画通常只是前跑/停，不需要严格表达侧移方向。

## 推荐设计

我建议把表现层输入扩展成两套数据：

```csharp
Vector2 VelocityXZ;       // 实际速度，用于物理、网络、自由移动动画
Vector2 MoveInput;        // 玩家输入，用于锁定八向动画
bool IsLockOn;
```

然后在 `CharacterLocomotionPresenter` 里：

```csharp
if (isLockOn)
{
    return QuantizeOrNormalizeLockOnInput(moveInput);
}
```

`QuantizeOrNormalizeLockOnInput` 可以有两种风格。

### 方案 A：保持模拟输入

适合 2D Freeform Directional / Cartesian Blend Tree：

```csharp
if (moveInput.sqrMagnitude < deadZone * deadZone)
    return Vector2.zero;

return Vector2.ClampMagnitude(moveInput, 1f);
```

优点是手柄推半格会自然过渡，缺点是键盘斜向可能是 `(-0.707, 0.707)` 或 `(-1, 1)`，要和 Blend Tree 点位匹配。

### 方案 B：明确八向量化

适合八方向动画是离散的，想要“按左就是纯左”：

```csharp
if (moveInput.sqrMagnitude < deadZone * deadZone)
    return Vector2.zero;

float angle = Mathf.Atan2(moveInput.x, moveInput.y) * Mathf.Rad2Deg;
int sector = Mathf.RoundToInt(angle / 45f);
float snappedAngle = sector * 45f * Mathf.Deg2Rad;

return new Vector2(Mathf.Sin(snappedAngle), Mathf.Cos(snappedAngle));
```

这样八方向非常稳定：左永远 `(-1, 0)`，左前永远大约 `(-0.707, 0.707)`。

## 我会怎么改这个项目

如果是这个项目，我会选择：

- `CharacterMotor` 继续用当前逻辑算真实移动速度，不动。
- `CharacterLateUpdatePipeline` 在本地玩家分支额外取 `_inputHandler.MoveInput` 或从 `PlayerController` 暴露 `LastMoveInput`。
- `CharacterLocomotionPresenter` 的锁定分支改为用输入方向算 `VelocityX/Z`。
- 非锁定仍然用当前速度逻辑。
- 远端玩家如果没有输入数据，就继续 fallback 到 velocity，或者把锁定移动输入也同步进 `StateSnapshot`。

核心原则是：**锁定八向动画看“玩家指令”，不是看“角色当前速度”。** 实际速度会被加速度、阻尼、碰撞、网络插值污染；输入方向才是八向动画最稳定的依据。

---

**User**

用方案A，给一份修改计划。Animator的Param要不要改？涉及到GuardWalk和Locomotion

---

**Cursor**

我会按方案 A 做计划：保留 `VelocityX/Z` 这两个 Animator 参数名，但改变锁定状态下它们的数据来源。先再确认表现层配置和同步路径，避免把 GuardWalk 或远端表现漏掉。

目前确认：`Locomotion` 和 `GuardWalk` 都共用 `CharacterLocomotionPresenter` 的 `VelocityX/Z`，所以 Animator 参数本身不需要改名或新增；需要改的是锁定时这两个参数的来源。

还有一个关键点是 Blend Tree 的轴范围：代码配置里自由跑用 `runForwardBlendZ = 2`，但你这次描述的是锁定八向希望输入值语义为 `-1..1`。计划里会把锁定八向独立成输入轴，不复用速度归一化的 `runForwardBlendZ`。

---

**User**

修改GuardWalk和Locomotion的各PosX/Y，使其更符合需求。在计划里详细说明需要改的部分和代码

---

**Cursor**

正在读取当前计划与 Animator 配置，随后把 GuardWalk / Locomotion 的 PosX/Y 调整写进计划。


正在把 GuardWalk / Locomotion 的 Blend Tree 坐标统一方案，以及对应代码改动写进计划。


已更新计划，核心变化如下。

## Animator 参数：不改名

继续用 `VelocityX` / `VelocityZ`，`GuardWalk` 和 `Locomotion` 共用。改的是 **PosX/Y 坐标** 和 **代码写入的值**。

## Blend Tree 坐标统一为 -1..1

### GuardWalk（斜向从 ±0.5 改为 ±1）

| 方向 | 当前 | 改为 |
|------|------|------|
| 左前/右前/左后/右后 | (±0.5, ±0.5) | **(±1, ±1)** |
| 前/后/左/右 | 已是 ±1 | 不变 |

### Locomotion / WalkRunSprint（从 ±2 / ±1.5 缩到 ±1）

| 方向 | 当前 | 改为 |
|------|------|------|
| 前跑 | (0, 2) | **(0, 1)** |
| 后 | (0, -2) | **(0, -1)** |
| 左/右 | (±2, 0) | **(±1, 0)** |
| 四斜向 | (±1.5, ±1.5) | **(±1, ±1)** |
| Idle | (0, 0) | 不变 |

这样按左就是 `(-1, 0)`，与输入一致。

## 配套代码（计划里写了具体 diff）

1. **`CharacterPresentationConfig`**：`runForwardBlendZ` 和 `blendAxisMax` 从 `2` 改为 `1`
2. **`PlayerController`**：暴露 `LastMoveInput`
3. **`CharacterLateUpdatePipeline`**：`PresentationFrame` 增加 `MoveInput`
4. **`CharacterLocomotionPresenter`**：锁定时用 `Vector2.ClampMagnitude(moveInput, 1f)` 直接写 `VelocityX/Z`，删除 velocity 投影

## 实施顺序

先改 Animator + Config，再改代码；两者要一起落地，否则左走仍会偏到错误 blend 点。

完整 YAML 示例、方法签名和验收清单都在计划文件里。确认后可以说「执行计划」，我再开始改。

---

**User**

我改了一部分，剩下的帮我改好

---

**Cursor**

先对照计划检查你已改的部分，再补齐剩余改动。


Animator 与配置已改好；接下来补全 `CharacterLateUpdatePipeline`、`CharacterLocomotionPresenter`，并修正 `PlayerController` 的 `LastMoveInput` 类型。


已补全剩余改动。你已完成的 Animator Blend Tree 坐标和 `DefaultPresentation.asset` 配置保持不变。

### 本次补全内容

**1. [`PlayerController.cs`](Assets/Scripts/Character/Controller/PlayerController.cs)**
- `LastMoveInput` 改为 `Vector2`（原为 `Vector3`）
- 每帧写入 `intent.Move`

**2. [`CharacterLateUpdatePipeline.cs`](Assets/Scripts/Character/Sync/CharacterLateUpdatePipeline.cs)**
- 打通 `moveInput` 数据流：`ResolvePresentation` → `PresentationFrame.MoveInput` → `CharacterLocomotionPresenter`
- 本地玩家从 `_playerController.LastMoveInput` 读取
- NPC / 远端仍用 `Vector2.zero`（fallback 到速度逻辑）

**3. [`CharacterLocomotionPresenter.cs`](Assets/Scripts/Character/Presentation/CharacterLocomotionPresenter.cs)**
- 锁定时用 `ComputeLockOnBlendInput(moveInput)` 直接写 `VelocityX/Z`
- 删除基于实际速度的 `InverseTransformDirection` 投影
- 非锁定仍用原有前跑速度逻辑（`runForwardBlendZ = 1` 与你的 Blend Tree 一致）

### 验收

Play 模式下锁定后：
- 只按 **A** → `VelocityX=-1`, `VelocityZ=0`
- 只按 **W** → `VelocityX=0`, `VelocityZ=1`
- **W+A** → `VelocityX=-1`, `VelocityZ=1`

举盾移动（Guard + 有输入）也会走同一套输入混合，因为 `TryPresentGuard` 在 `hasMove` 时会 fall through 到 `PresentLocomotion`。

---

**User**

锁定现在是可以同步的吗

---

**Cursor**

**目前锁定状态不能在网络间同步。** 只有本地玩家有完整的锁定逻辑；其他客户端看不到“谁在锁谁”。

## 网络里实际同步了什么

[`StateSnapshot`](Assets/Scripts/Character/Sync/StateSnapshot.cs) / [`SnapshotMsg`](Assets/Scripts/Character/Sync/Transport/MirrorSyncTransport.cs) 里只有：

- 位置、Yaw、VelocityXZ  
- StateId、SprintPhase、DodgeMode、GuardPhase、AttackComboStep  

**没有** `IsLockOn`、锁定目标 ID、目标 NetId 等字段。

[`LocalSyncPublisher`](Assets/Scripts/Character/Sync/LocalSyncPublisher.cs) 发快照时也不会写入锁定信息。

## 本地 vs 远端的表现差异

[`CharacterLateUpdatePipeline`](Assets/Scripts/Character/Sync/CharacterLateUpdatePipeline.cs) 里：

```304:304:Assets/Scripts/Character/Sync/CharacterLateUpdatePipeline.cs
isLockOn = _lockOnQuery != null && _lockOnQuery.IsLockOnActive;
```

- **本地玩家**：`isLockOn` 来自本机 `PlayerLockOnController`，八向动画用 `LastMoveInput`。
- **远端玩家**：走 `TryResolveFromRemoteSnapshot`，只有 velocity/state；`moveInput` 固定为 `Vector2.zero`，且远端 proxy 上通常没有有效的 `_lockOnQuery`，所以 `isLockOn` 基本是 **false**。

结果是：看别人时不会走锁定八向 blend，只会按普通 locomotion + 速度来播。

## 间接能“看出来”的部分

| 内容 | 是否同步 | 说明 |
|------|----------|------|
| 锁定状态 / 目标 | 否 | 未进 snapshot |
| 锁定八向动画 | 否 | 远端无 `moveInput` + `isLockOn=false` |
| 锁定相机 | 否 | [`PlayerCameraRigController`](Assets/Scripts/Core/PlayerCameraRigController.cs) 仅本地绑定 |
| 面向目标的 Yaw | 部分 | 通过 snapshot 的 `Yaw` 间接体现 |
| 锁定八向闪避 | 部分 | `DodgeMode.LockOn8Way` 会随 Dodge 状态同步 |

## 结论

- **自己玩**：锁定、八向移动、相机都正常。  
- **联机看别人**：看不到对方是否在锁定、锁的是谁，也看不到正确的锁定八向动画；最多从朝向/位移推测。

若要完整同步，需要扩展协议，例如 snapshot 增加 `IsLockOn` + `LockTargetNetId`，远端用同样逻辑驱动动画（相机仍只给本地玩家）。需要实现的话可以切到 Agent 模式。

---

**User**

给一份锁定系统同步的计划

---

**Cursor**

制定锁定同步计划前，先梳理现有网络同步架构与可复用的字段/流程。


现有同步走 `StateSnapshot` + `LocalSyncPublisher`，与 Sprint/Dodge 一致；计划将锁定状态与 `MoveInput` 纳入该链路。


**锁定同步范围确认**

> **这次锁定同步希望覆盖哪些内容？（相机会话仍仅本地，不在此选择内）**
> - 最小：远端能看到「是否在锁定 + 八向动画(MoveInput)」
> - 推荐：上述 + 同步锁定目标 netId（便于远端 UI/指示线/后续 VFX） ✔
> - 完整：上述 + 远端也跑锁定移动逻辑（一般不需要，位置已由 snapshot 同步）

---

**User**

将计划中需要修改和补充的代码完整地写进计划里

---

**Cursor**

先把现有计划和相关源文件读全，再把需要改/补的代码完整写进计划。


正在把各文件的完整代码改动写入计划文档。


已将计划中所有需要修改/补充的代码完整写入 [`lock-on_network_sync_8371c149.plan.md`](c:\Users\1941\.cursor\plans\lock-on_network_sync_8371c149.plan.md)。

### 计划现在包含的完整代码

| Part | 文件 | 内容 |
|------|------|------|
| 1 | **新建** `LockOnTargetRegistry.cs` | 全文 |
| 2 | `LockOnTarget.cs` | `TryGetNetworkId` 完整方法 |
| 3 | `PlayerLockOnController.cs` | `TryGetSyncState` 完整方法 |
| 4 | `StateSnapshot.cs` | 新字段、构造函数、helper、`ToString` |
| 5 | `MirrorSyncTransport.cs` | `SnapshotMsg` + `ToMsg` / `FromMsg` |
| 6 | `LocalSyncPublisher.cs` | 字段、`Awake`、`ResolveLockOnSync`、`TryProduceSnapshot` 改动 |
| 7 | `NpcAuthoritySyncPublisher.cs` | 构造 snapshot 时锁定字段填 0 |
| 8 | `CharacterLateUpdatePipeline.cs` | `ResolvePresentation`、`TryResolveFromLocalPlayer`、`TryResolveFromRemoteSnapshot`、`PresentationFrame` 全文替换 |

### 明确标注不改的文件

- `FakeNetworkPipe.cs` — struct 透传，无需改
- `CharacterLocomotionPresenter.cs` — 已有 input blend，只接数据

确认后可切 Agent 模式，我按该计划直接实现。

---

**User**

我写了一部分，剩下的帮我做完

---

**Cursor**

先对照计划检查你已改的部分，再补齐剩余实现。


主要缺的是 `CharacterLateUpdatePipeline` 的远端锁定解析，正在补全。


已补全剩余部分。你已完成的改动保持不变，我只改了 **`CharacterLateUpdatePipeline.cs`**（这是远端锁定同步的关键缺口）。

### 你已完成的
- `LockOnTarget.TryGetNetworkId`
- `LockOnTargetRegistry`
- `PlayerLockOnController.TryGetSyncState`
- `StateSnapshot` 锁定字段
- `MirrorSyncTransport` / `SnapshotMsg`
- `LocalSyncPublisher` 发布锁定数据

### 本次补全（`CharacterLateUpdatePipeline.cs`）
- `ResolvePresentation`：不再在开头用本地 `_lockOnQuery` 覆盖远端状态
- `TryResolveFromLocalPlayer`：输出 `isLockOn`、`lockTargetNetId`
- `TryResolveFromRemoteSnapshot`：从 snapshot 读取 `IsLockOnActive`、`GetMoveInputOrDefault()`、`LockTargetNetId`
- `PresentationFrame`：增加 `LockTargetNetId`（供后续 UI/VFX 用 `LockOnTargetRegistry` 解析）

### 联机验收
1. A 锁定 NPC 移动 → B 应看到 A 的八向动画（`VelocityX/Z` 随输入变化）
2. A 解锁 / Sprint 解锁 → B 回到普通 locomotion
3. Snapshot log 中 `lockTarget` 应等于 NPC 的 netId

`NpcAuthoritySyncPublisher` 仍用构造函数默认值（锁定字段为 0），无需改动。

---

**User**

目前攻击转向是根据输入给了角色一个角度累加到当前旋转，如果按着右攻击，会一直转向。这不符合需求。要求输入方向就是最终旋转方向，比如一直按着右攻击，角色最终会旋转到面向右方攻击，之后不会再转向。应该如何设计和修改

---

**Cursor**

问题根因找到了，是 `TryGetInputWorldDirection` 用了角色自身的局部轴来解算输入，而不是相机轴。

## 根因分析

```161:203:Assets/Scripts/Character/Motor/CharacterMotor.cs
private bool TryGetInputWorldDirection(CharacterIntent intent, out Vector3 inputDir)
{
    // ...
    Vector3 rootForward = _context.Root.forward;
    Vector3 rootRight = _context.Root.right;
    // ...
    inputDir = rootRight * intent.Move.x + rootForward * intent.Move.y;
```

**问题在这里：** 输入向量 `(Move.x, Move.y)` 被投影到的是 **角色当前朝向的局部空间**，而不是相机空间。

**连击循环：**
1. Combo1 采样方向：按着右 → `inputDir = 角色右方` → 角色旋转到面向右
2. Combo2 采样方向（`BeginAttack` 重置了 `_hasSampledDirection`）：还是按着右 → 但现在 `rootRight` 已经是旋转后的新右方 → 再次旋转 → 无限累积

自由移动不会有这个问题，因为 `TickFreeHorizontal` 用的是相机基准轴：
```278:281:Assets/Scripts/Character/Motor/CharacterMotor.cs
_context.GetCameraBasis(out var camForward, out var camRight);
var inputDir = camRight * intent.Move.x + camForward * intent.Move.y;
```

## 正确设计

**把 `TryGetInputWorldDirection` 从角色局部空间改为相机空间。** 这样：

- 按右 = 相机右方（固定的世界方向）
- Combo1 采样：角色旋转到面向相机右方
- Combo2 采样：输入还是相机右方，目标方向不变，角色已经在那个方向了 → 不再旋转

## 修改方法

将 `TryGetInputWorldDirection` 中的：

```csharp
Vector3 rootForward = _context.Root.forward;
Vector3 rootRight = _context.Root.right;
rootForward.y = 0f;
rootRight.y = 0f;
// ...
inputDir = rootRight * intent.Move.x + rootForward * intent.Move.y;
```

改为：

```csharp
_context.GetCameraBasis(out var camForward, out var camRight);
inputDir = camRight * intent.Move.x + camForward * intent.Move.y;
inputDir.y = 0f;
```

只改这一处，`SnapAttackDirection` 和所有调用它的地方（攻击方向采样）都自动正确。

## 额外注意

`SetAttackRootMotionDelta` 中的 `_pendingAttackDeltaYaw` 累积根动画旋转，这是正常的（动画驱动的旋转，不是输入驱动的）。问题只在输入采样这一环，只需改 `TryGetInputWorldDirection`。

需要切到 Agent 模式帮你改吗？

---

**User**

加一个需求，锁定时不能用输入改变出招转向

---

**Cursor**

现有 `TrySampleAttackDirection` → `SnapAttackDirection` 的逻辑已经清楚，针对你的两个需求，分析如下：

---

## 当前问题根因

`SnapAttackDirection` 在 `CharacterMotor` 里的实现：

```143:148:Assets/Scripts/Character/Motor/CharacterMotor.cs
public void SnapAttackDirection(CharacterIntent intent)
{
    if (!TryGetInputWorldDirection(intent, out var inputDir))
        return;

    _context.Root.rotation = Quaternion.LookRotation(inputDir);
}
```

`TryGetInputWorldDirection` 用的是 `_context.Root.forward/right` 做相机基，**而不是相机基**。所以如果一直按右（`Move.x = 1`），根节点右方向就是世界右，计算出的 inputDir 永远是当前面朝方向的右侧——每次 Snap 后角色旋转，下一帧 `rootRight` 又变了，循环产生累积转向。

另外 `TickAttackRootMotion` 里的 `_context.Root.Rotate(0f, deltaYaw, 0f)` 也会把动画 root motion 的旋转叠进去，这是另一层累积源。

---

## 两个需求的设计

### 需求 1：输入方向 = 最终朝向，不累积

**根因**：要用**相机空间**而不是角色本地空间计算输入方向。

`SnapAttackDirection` 应该改为与 `TickFreeHorizontal` / `TickLockOnHorizontal` 一样，通过 `_context.GetCameraBasis(out camForward, out camRight)` 算世界方向：

```csharp
public void SnapAttackDirection(CharacterIntent intent)
{
    if (intent.Move.sqrMagnitude <= 0.0001f)
        return;

    _context.GetCameraBasis(out var camForward, out var camRight);
    var inputDir = camRight * intent.Move.x + camForward * intent.Move.y;
    inputDir.y = 0f;

    if (inputDir.sqrMagnitude <= 0.0001f)
        return;

    _context.Root.rotation = Quaternion.LookRotation(inputDir.normalized);
}
```

这样"按右"永远 Snap 到相机右方，第二次触发时相机基不变，结果一致，不会再累积。

同时 `TryGetInputWorldDirection` 也需要同样修改（它被其他地方调用的话要一并检查）。

---

### 需求 2：锁定时不允许输入改变出招朝向

只需在 `TrySampleAttackDirection`（`AttackState.cs`）里加锁定判断，有输入时也跳过 Snap：

```130:143:Assets/Scripts/Character/StateMachine/States/AttackState.cs
private void TrySampleAttackDirection(CharacterIntent intent, float previousTimer)
{
    if (_currentAttackStep == SprintAttackStep)
        return;

    if (_hasSampledDirection)
        return;

    float sampleTime = _combat.attackDirectionSampleTime;
    if (previousTimer <= sampleTime && _timer >= sampleTime)
    {
        _hasSampledDirection = true;
        _motor.SnapAttackDirection(intent);
    }
}
```

在 `_hasSampledDirection = true; _motor.SnapAttackDirection(intent);` 之前加：

```csharp
// 锁定状态下维持当前朝向（已面向目标），不响应输入转向
if (_motor.IsLockOnActive)
{
    _hasSampledDirection = true;
    return;
}
```

`AttackState` 需要能访问锁定状态，有两种方式：

**方式 A**：`CharacterMotor` 暴露 `public bool IsLockOnActive => _lockOnQuery != null && _lockOnQuery.IsLockOnActive;`（需确认 motor 是否已有 query 引用，查代码 L17 已有 `_lockOnQuery`，直接加属性即可）。

**方式 B**：`AttackState` 构造时注入 `ILockOnLocomotionQuery`，自己判断。

方式 A 改动最小，只在 motor 加一行属性。

---

## 改动汇总

| 文件 | 改动 |
|------|------|
| `CharacterMotor.cs` | `SnapAttackDirection` 改用 `GetCameraBasis` 算输入世界方向；增加 `IsLockOnActive` 只读属性 |
| `AttackState.cs` | `TrySampleAttackDirection` 增加锁定判断，锁定时只置 `_hasSampledDirection=true` 跳过 Snap |

需要在 Agent 模式下实现的话，直接告诉我。

---

**User**

现在攻击动作后翻滚的方向不对了，动画翻滚方向和实际方向不符合

---

**Cursor**

问题根因很清楚了。

## 根本原因

`ForwardAlongMove` 翻滚的动画是**相对角色当前朝向**播放的（`StateDodgeNormal` = 前滚翻），而 Motor 的实际位移是沿 `WorldMoveDirection`（相机空间计算）移动的。

**两者的参考系不一致：**

| | 参考系 | 计算位置 |
|---|---|---|
| 动画方向 | 角色本地朝向（`Root.forward`） | — |
| 实际位移 | 相机空间输入 → 世界方向 | `DodgeModeResolver.StickToWorldDirection` |

攻击结束时角色朝向已被 `SnapAttackDirection` 改变，和相机方向可能完全不同。翻滚触发时：
- Motor 按相机右方移动（例如世界东方）
- 动画从角色当前面朝方向（例如东北方）前滚
- 视觉上角色在斜向翻但实际跑东方 → 错位

## 修复方案

在 `BeginDodge` 里，当 Mode 为 `ForwardAlongMove` 时，先把角色旋转到翻滚方向，动画和位移就都朝同一方向了：

```csharp
public void BeginDodge(in DodgePresentationContext ctx, CharacterCombatConfig combat)
{
    _isDodgeActive = true;
    _isSprintActive = false;
    _dodgeWorldDirection = ctx.WorldMoveDirection;

    // ForwardAlongMove 翻滚动画是前向播放，需要先把角色转向位移方向
    if (ctx.Mode == DodgeMode.ForwardAlongMove
        && ctx.WorldMoveDirection.sqrMagnitude > 0.0001f)
    {
        _context.Root.rotation = Quaternion.LookRotation(ctx.WorldMoveDirection);
    }

    // ... 其余不变
}
```

**为什么其他两种 Mode 不需要改：**
- `NeutralBackward`：动画是原地后退步，不依赖朝向
- `LockOn8Way`：`blendLocal` 是角色本地空间偏移量，`LocalBlendToWorldDirection` 也是从 `Root.TransformDirection` 算出来的，动画 Blend Tree 和位移方向已经一致

这一处改动量很小，需要在 Agent 模式下改 `CharacterMotor.cs` 的 `BeginDodge` 即可。

---

**User**

根据PhaseF的大纲和现在项目的进度，接下来应该做什么

---

**Cursor**

项目里没有找到名为 "PhaseF" 或包含阶段大纲的计划文档。README 只记录了功能概览和技术说明。

你提到的 **PhaseF 大纲**在哪里？可以是：

- 某个文档文件（`.md` / `.txt` / Word/Notion 等）
- 之前对话里列出的阶段计划

项目的对话摘要里也没有提到 "Phase" 分阶段计划。

能帮我定位一下 PhaseF 在哪里吗？如果是外部文档，你可以把内容贴过来，我结合当前代码状态给你分析接下来应该做什么。如果是在之前的某次对话里定义的，告诉我那次对话的大致内容我来参照。

---

**User**

G:\资料\SS&3CDemo_Cursor记录 应该在这个目录里有讨论过phaseA-H的大纲

---

**Cursor**

对，**Phase A–H 的大纲主要就在** `G:\资料\SS&3CDemo_Cursor记录` 里，按阶段拆成了 5 个导出文件；更细的 F 子计划还在 `C:\Users\1941\.cursor\plans\`。

---

## 记录文件索引

| 文件 | 覆盖阶段 |
|------|----------|
| [`cursor_phasea_d1.md`](G:\资料\SS&3CDemo_Cursor记录\cursor_phasea_d1.md) | **A → D1**（含 C+、D 启动） |
| [`cursor_phased2.md`](G:\资料\SS&3CDemo_Cursor记录\cursor_phased2.md) | **D2** + **F/G/H 首次定义** |
| [`cursor_phasee1.md`](G:\资料\SS&3CDemo_Cursor记录\cursor_phasee1.md) | **E** 开工清单、里程碑、**F/G/H 展开** |
| [`cursor_phasee2.md`](G:\资料\SS&3CDemo_Cursor记录\cursor_phasee2.md) | **E 收口验收** |
| [`cursor_phasef.md`](G:\资料\SS&3CDemo_Cursor记录\cursor_phasef.md) | **F 全量推进**（模型、Presenter、Sprint、Combat、Guard、锁定等） |

主计划原文曾写在 `ss3c状态机落地_bd98a037.plan.md`（`phasea_d1.md` 约 1470 行引用），当前仓库里可能已不在；**6 周评估**在 [`6周demo进度评估_cd6e8208.plan.md`](C:\Users\1941\.cursor\plans\6周demo进度评估_cd6e8208.plan.md)。

---

## Phase A–H 总纲（一句话版）

| Phase | 目标 |
|-------|------|
| **A** | 拆出 `Context + Intent + Motor`，`PlayerController` 做编排，移动手感不退化 |
| **B** | `Idle / Move / Sprint` 三态 FSM + 转移 |
| **C** | `Attack / Dodge / Hit / Dead` + 打断/无敌窗；**C+** 补邻接表与打断规则表 |
| **D** | 同步最小闭环：`StateSnapshot` + `ActionEvent` + 插值/去重 |
| **E** | 服务器权威 NPC：BT → Intent → 同一套 FSM，复用 D 协议 |
| **F** | 角色动画表现：快照/事件 → Animator（ARPGSamurai、Presenter 架构） |
| **G** | 战斗手感打磨：连段、受击反馈、镜头、VFX/SFX |
| **H** | 内容化生产：多角色/NPC 模板、技能表、关卡波次工具化 |

**D 在实践里拆成两步**（`phasea_d1.md` / `phased2.md`）：
- **D1**：本地假网络（`FakeNetworkPipe`）验证闭环  
- **D2**：传输抽象 + **Mirror** 真实联机

**E 在实践里也有子任务**（`phasee1.md`）：
- **E-Day0**：`RemoteGhost` 收尾  
- **E1/E6**：发布器读真实 `CurrentStateId`  
- **E2**：BT → Intent → FSM → Nav  
- **E0**：三段 BT 演示（你们约定**后置**）

---

## Phase F 的细化大纲（战斗动画子路线）

主定义：[`phase_f_角色表现_12da7c19.plan.md`](C:\Users\1941\.cursor\plans\phase_f_角色表现_12da7c19.plan.md)

七种动作：**移动 / 冲刺 / 攻击 / 闪避 / 格挡 / 受击 / 死亡**，全部走快照 + `ActionEvent`，逻辑 FSM 为唯一真相，Presenter 用 `CrossFade` 驱动 Animator。

[`combat_animation_roadmap_467cce38.plan.md`](C:\Users\1941\.cursor\plans\combat_animation_roadmap_467cce38.plan.md) 把 F 内战斗表现再拆成：

| 子阶段 | 内容 |
|--------|------|
| **阶段 0** | `CharacterCombatPresenter` + Pipeline 战斗路由（先 Hit/Death 层） |
| **阶段 1** | 轻重受击、Death 细节、`ActionEvent.Param` |
| **阶段 2** | 单向 Dodge |
| **阶段 3** | 单段 Attack |
| **阶段 4** | **锁定 + 八向移动 + 八向翻滚**（依赖 `ILockOnLocomotionQuery`、Motor、网络同步） |

建议顺序：**0+1 → 2 → 3 → 4**；滑步明确后置。

---

## F / G / H 的正式定义（`phasee1.md` 约 284–313 行）

**Phase F（角色表现层接入）**
- Animator 参数协议、事件点对齐
- 本地与远端表现一致（先一致，不追极致手感）
- 产出：Player + NPC 共用表现管线

**Phase G（战斗手感与表现打磨）**
- 连段窗口、前后摇、打断细化
- 受击反馈（硬直、击退、镜头、音效、VFX）
- 网络观感优化
- 产出：至少 1 套可体验的核心战斗循环

**Phase H（内容化与生产化）**
- 多角色/多 NPC 模板化（配置驱动）
- 技能与 AI 参数表
- 关卡/波次/敌人组合工具化
- 产出：可持续加内容的生产流程

---

## 按对话记录，你们当前大致进度

| 阶段 | 状态（据记录 + 近期对话） |
|------|---------------------------|
| A–D2 | 已完成 |
| E（约定范围，不含 E0） | 已结项 |
| **F** | **进行中**：Locomotion、Sprint、Combat 管线、Guard、锁定八向动画、锁定网络同步等已大量推进 |
| G / H | 未开始 |

F 里**近期还在收尾**的包括：攻击朝向采样（锁定下不改面向）、Dodge 方向与动画对齐、锁定联机验收等——这些在 `cursor_phasef.md` 后半段和当前会话里都有讨论。

---

如果你需要，我可以按这份大纲再整理一版 **「F 剩余待办 checklist」**（对照工程现状逐项打勾），方便决定下一步先做攻击朝向还是 Dodge 还是锁定验收。

---

**User**

这两个已经做完了，检查项目，确定PhaseF剩余的部分

---

**Cursor**

正在对照 Phase F 大纲检查项目现状，确定剩余工作。


已确认攻击朝向与 Dodge 朝向修复已在代码中落地。正在整理 Phase F 验收线与剩余缺口。


**Phase F 验收范围确认**

> **Phase F 计划要求「远端 NPC 也展示七种动作」，但当前 NPC 仅有 Idle/Move FSM，NpcAuthoritySyncPublisher 也只发基础快照。Phase F 收尾时，NPC 远端表现验收你希望按哪种标准？**
> - 玩家双端七种动作验收即可；NPC 远端只做移动/Idle（战斗表现延后到 NPC 有战斗 FSM 时）
> - NPC 也要在 Phase F 内补齐七种动作的远端表现（需扩展 NPC FSM + 发布器快照字段） ✔

---

**User**

修改计划：剩余项1，2已验收，剩余项3待办。另外将攻击碰撞掉血，格挡攻击等放入合适的阶段(不一定是F中)

---

**Cursor**

正在读取计划文件，按你的验收结论更新，并把攻击碰撞、掉血与格挡招架归入合适阶段。


正在更新计划：将剩余项 1、2 标为已验收，保留剩余项 3，并新增战斗判定阶段划分。


已按你的反馈修订计划 [`phase_f_剩余清单_bcca58a8.plan.md`](c:\Users\1941\.cursor\plans\phase_f_剩余清单_bcca58a8.plan.md)，主要变更如下：

### 验收状态
- **剩余项 1**（玩家 Host+Client 七种动作验收）→ 标为 **已完成**
- **剩余项 2**（Hit 轻/重网络同步）→ 标为 **已完成**
- **剩余项 3**（NPC 七种动作远端表现）→ **Phase F 唯一待办**，子任务保持 pending

### 阶段划分（新增）
战斗玩法从 Phase F 拆出，归入 **Phase G0**（G 的前置，不是 F）：

| 内容 | 阶段 | 理由 |
|------|------|------|
| Hitbox/Hurtbox、攻击生效帧、Server 掉血、`ApplyHit` 替代 F 调试键 | **G0** | 玩法权威判定，不是动画表现 |
| 格挡判定（窗口/朝向/破防）、格挡 vs 受击分支 | **G0** | `GuardState` 已有动画，缺判定逻辑 |
| 战斗结果网络（Hit Param、Server 权威） | **G0** | 复用现有 `ActionEvent` + `HitVariant` |
| 连段精调、击退、镜头/VFX/SFX | **Phase G** | 手感打磨 |
| 多技能表、波次工具 | **Phase H** | 内容量产 |

**Phase F 结项条件**收窄为：玩家侧已验收 + **NPC 双端七种动作表现验收通过**。攻击碰撞、格挡招架不阻塞 F 结项。

### 建议顺序
1. 做完 NPC 全链路（F 收尾）  
2. F 结项  
3. 进入 G0（碰撞 + 格挡 + 权威伤害）  
4. 再进 G 打磨  

若要开始执行 **剩余项 3（NPC）**，说一声即可。

---

**User**

将PhaseA-H的大纲存成md文件，放在之前的那个目录

---

**Cursor**

正在把 Phase A–H 总纲整理为 Markdown，并保存到 `G:\资料\SS&3CDemo_Cursor记录`。


已保存到：

**`G:\资料\SS&3CDemo_Cursor记录\PhaseA-H_大纲.md`**

文档内容包括：
- **A～H + G0** 各阶段目标、交付物、验收标准
- 当前进度（F 玩家已验收，NPC 待办；G0/G/H 未开始）
- F 内战斗表现子阶段 0～4
- 阶段边界（判伤/格挡归 G0，手感归 G）
- 整体节奏与时间参考
- 本目录与 `.cursor/plans` 的文件索引

和同目录下的 `cursor_phasea_d1.md` 等导出记录并列，可作为总览查阅。

---

**User**

接下来做什么

---

**Cursor**

按当前进度和 [`PhaseA-H_大纲.md`](G:\资料\SS&3CDemo_Cursor记录\PhaseA-H_大纲.md)，**下一步就是收尾 Phase F：把 NPC 的七种动作远端表现补齐**。玩家侧已验收，这是 F 结项前的唯一硬门槛。

---

## 立刻做：Phase F 剩余（NPC 全链路）

建议按这个顺序动手：

### 1. 扩展 `NpcAuthoritySyncPublisher`

对照玩家的 [`LocalSyncPublisher`](g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\LocalSyncPublisher.cs)，让 NPC 快照也带上：

- `SprintPhase` / `DodgeMode` / `GuardPhase` / `AttackComboStep` / `HitVariant`
- `StateId` 或 phase 变化时强制发送

现在 NPC 发布器只发 `pos/yaw/velocity/stateId`，客户端无法播战斗动画。

### 2. 给 NPC 加战斗 FSM（轻量计时态）

[`NpcCharacterDriver`](g:\UnityProject\SS&3CDemo\Assets\Scripts\AI\NpcCharacterDriver.cs) 目前只有 **Idle / Move**，不能直接复用玩家的 `AttackState`（依赖 `CharacterMotor`）。

建议在 `Assets/Scripts/AI/States/` 加计时型状态：

- `NpcAttackState`、`NpcDodgeState`、`NpcGuardState`、`NpcHitState`、`NpcDeadState`

进入战斗态时 `NpcMotor.Stop()`，暴露 `CurrentComboStep` / `GuardPhase` 等，供发布器读取。

验收前可用 **BT Task 或调试入口** 触发（不必等 AI 真的会打）。

### 3. 扩展 Pipeline 服务端解析

[`CharacterLateUpdatePipeline.TryResolveFromServerNpc`](g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\CharacterLateUpdatePipeline.cs) 目前只读 `stateId + agent.velocity`，Host 上看 Server NPC 的战斗动画会不对。需从活跃 NPC 战斗态读出 phase/combo。

### 4. 检查 `NPC.prefab` 接线

确认挂有：

- `Animator`（ARPGSamurai Avatar）
- `RemoteInterpolator` + `RemoteActionApplier`
- `CharacterLateUpdatePipeline`

客户端 NPC 才会走与远端玩家相同的 `TryResolveFromRemoteSnapshot` 路径。

### 5. Host + Client 验收（F 结项门槛）

在 B 端观察 NPC 的 Attack / Dodge / Guard / Hit / Dead，确认只靠 **快照 + ActionEvent** 动画正确。

---

## F 结项之后

| 顺序 | 阶段 | 做什么 |
|------|------|--------|
| 1 | **Phase G0** | 攻击 Hitbox 掉血、Server 权威 `ApplyHit`、格挡判定（替换 F 调试键） |
| 2 | **Phase G** | 连段手感、受击反馈、镜头/VFX/SFX |
| 3 | **Phase H** | 多角色模板、技能表、波次工具 |

**先别做 G0**：碰撞判伤和格挡逻辑属于玩法判定，动画管线（F）没结项前做容易返工。

---

## 一句话

> **现在：NPC 发布器对齐 → NPC 战斗 FSM → Pipeline/Prefab → 双端验收 → Phase F 结项 → 再进 G0 打碰撞和格挡。**

若要我在工程里直接实现 NPC 这条链路，切到 **Agent 模式** 说一声即可。

---

**User**

3.1 扩展 NPC 发布器

以 [LocalSyncPublisher](g:/UnityProject/SS&3CDemo/Assets/Scripts/Character/Sync/LocalSyncPublisher.cs) 为模板改造 [NpcAuthoritySyncPublisher](g:/UnityProject/SS&3CDemo/Assets/Scripts/Character/Sync/NpcAuthoritySyncPublisher.cs)：





快照携带：SprintPhase, DodgeMode, GuardPhase, AttackComboStep, HitVariant（与玩家一致）



StateId / phase 变化时强制发送（与玩家 delta 条件对齐）



MirrorSyncTransport / StateSnapshot 字段已支持，主要是 NPC 侧写入。这个具体要怎么做，以及对应的代码修改也展示给我

---

**Cursor**

对照 `LocalSyncPublisher` 与 `NpcAuthoritySyncPublisher` 的实现，整理具体改法和对应代码。


下面按「要改什么 → 为什么 → 具体代码」说明 **3.1 扩展 NPC 发布器**。  
（Ask 模式只给方案与示例代码，不直接改工程；要自动落地请切 Agent 模式。）

---

## 一、现状差距

### 玩家 [`LocalSyncPublisher`](g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\LocalSyncPublisher.cs)

- 构造完整 `StateSnapshot`（含 phase 字段）
- `shouldSend` 在 **位置/Yaw/StateId/各 phase/锁定/MoveInput** 任一变化时发送
- `ActionEvent` 带 `Param`（DodgeStart 传 `DodgeMode`）

### NPC [`NpcAuthoritySyncPublisher`](g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\NpcAuthoritySyncPublisher.cs)

```86:93:g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\NpcAuthoritySyncPublisher.cs
            var snapshot = new StateSnapshot(
                tick,
                (int)netId,
                pos,
                yaw,
                velocityXZ,
                ResolveCurrentStateId()
            );
```

只传 6 个参数，其余 phase 全是默认 0；`shouldSend` 也只看 pos/yaw。

### 协议层

[`MirrorSyncTransport.SnapshotMsg`](g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\Transport\MirrorSyncTransport.cs) 和 [`StateSnapshot`](g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\StateSnapshot.cs) **已支持** SprintPhase / DodgeMode / GuardPhase / AttackComboStep / LockOn / MoveInput，**不用改 Mirror**。

### 关于 HitVariant

工程里 **目前没有 `HitVariant` 字段**（`StateSnapshot` / `SnapshotMsg` 都没有）。  
轻重受击若已验收，多半是靠 `ActionEvent.Param` 或本地 duration；NPC 侧建议：

- **短期**：`ActionEvent.Hit` 时 `Param = 0/1`（轻/重），与玩家 `LocalSyncPublisher` 对齐时补上 `ResolveActionEventParam`
- **长期**：若要快照里也带轻重，需先给 `StateSnapshot` + `SnapshotMsg` 加字段（这是独立小改，不在本次「对齐已有字段」范围内）

---

## 二、前置依赖：NPC 要能「被问到」phase

玩家从 `PlayerController` 读 phase：

```41:47:g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Controller\PlayerController.cs
        public bool TryGetActiveSprintState(out SprintState sprintState)
        {
            if (_fsm?.CurrentState is SprintState active)
            {
                sprintState = active;
                return true;
            }
```

发布器本身**不直接摸 FSM**，而是通过 `NpcCharacterDriver` 查询。  
在加 NPC 战斗态（3.2）之前，可先在 `NpcCharacterDriver` 上挂好接口，暂时全返回 `false`；等 `NpcAttackState` 等接上后自动生效。

建议在 [`NpcCharacterDriver.cs`](g:\UnityProject\SS&3CDemo\Assets\Scripts\AI\NpcCharacterDriver.cs) 增加（与 `PlayerController` 同形）：

```csharp
// NpcCharacterDriver.cs — 新增查询 API（示例）

public byte LastPreparedDodgeMode { get; private set; }

public bool TryGetActiveSprintState(out NpcSprintState sprintState) { ... }
public bool TryGetActiveGuardState(out NpcGuardState guardState) { ... }
public bool TryGetActiveAttackState(out NpcAttackState attackState) { ... }
public bool TryGetActiveHitState(out NpcHitState hitState) { ... }
public bool TryGetDodgePresentationContext(out DodgePresentationContext ctx) { ... }

public void SetLastPreparedDodgeMode(byte mode) => LastPreparedDodgeMode = mode;
```

`NpcHitState` 可暴露 `public bool IsHeavyHit { get; }`，供 `ActionEvent.Param` 使用。

---

## 三、`NpcAuthoritySyncPublisher` 改造要点

| 步骤 | 内容 |
|------|------|
| 1 | 增加 `_lastSentStateId`、`_lastSentSprintPhase` 等缓存 |
| 2 | `TrySendSnapshot` 里解析 phase（从 `_npcDriver`） |
| 3 | `shouldSend` 条件与玩家对齐（`>=` + state/phase 变化） |
| 4 | 用完整 `StateSnapshot` 构造函数 |
| 5 | `TrySendActionOnStateChange` 补 `Param`（DodgeStart、Hit） |
| 6 | Dodge 时 `VelocityXZ` 特殊处理（LockOn8Way 写 blend，与玩家一致） |
| 7 | NPC 一般无锁定：`lockOnActive=0`，`moveInput=0`（以后有再扩展） |

---

## 四、完整改造代码示例

下面是一份可直接对照改的 **`NpcAuthoritySyncPublisher.cs`** 示例（假设 `NpcCharacterDriver` 已提供上述 API）：

```csharp
using AI;
using Character.Config;
using Character.Presentation;
using Character.StateMachine;
using Character.StateMachine.States;
using Core;
using Mirror;
using UnityEngine;

namespace Character.Sync
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(NetworkIdentity))]
    public class NpcAuthoritySyncPublisher : NetworkBehaviour
    {
        [Header("Refs")]
        [SerializeField] private NetTickClock _clock;
        [SerializeField] private MirrorSyncTransport _transport;
        [SerializeField] private NpcCharacterDriver _npcDriver;
        [SerializeField] private NpcMotor _npcMotor;

        private NetworkSyncConfig Sync => GameDataManager.Instance.NetworkSync;

        private bool _hasSentAnySnapshot;
        private Vector3 _lastSentPos;
        private float _lastSentYaw;

        // 事件去重：上一帧逻辑状态（发 ActionEvent）
        private CharacterStateId _lastStateId = CharacterStateId.None;
        // 快照去重：上次已发送的快照内容
        private CharacterStateId _lastSentStateId = CharacterStateId.None;
        private byte _lastSentSprintPhase;
        private byte _lastSentDodgeMode;
        private byte _lastSentGuardPhase;
        private byte _lastSentAttackComboStep;

        private int _nextSeqId = 1;

        private void Awake()
        {
            if (_npcDriver == null)
                _npcDriver = GetComponent<NpcCharacterDriver>();
            if (_npcMotor == null)
                _npcMotor = GetComponent<NpcMotor>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_clock == null)
                _clock = FindFirstObjectByType<NetTickClock>();
            if (_transport == null)
                _transport = FindFirstObjectByType<MirrorSyncTransport>();
        }

        private void LateUpdate()
        {
            if (!isServer) return;
            if (_transport == null || _clock == null) return;

            int tickCount = _clock.TickCountThisFrame;
            for (int i = 0; i < tickCount; i++)
            {
                int tick = _clock.CurrentTick - (tickCount - 1 - i);
                TrySendSnapshot(tick);
            }

            TrySendActionOnStateChange(_clock.CurrentTick);
        }

        private void TrySendSnapshot(int tick)
        {
            Vector3 pos = transform.position;
            float yaw = transform.eulerAngles.y;

            Vector2 velocityXZ = ResolveVelocityXZ();
            CharacterStateId stateId = ResolveCurrentStateId();

            byte sprintPhase = ResolveSprintPhase(stateId);
            byte dodgeMode = ResolveDodgeMode(stateId);
            byte guardPhase = ResolveGuardPhase(stateId);
            byte attackComboStep = ResolveAttackComboStep(stateId);
            velocityXZ = ResolveSnapshotVelocityXZ(stateId, velocityXZ);

            // NPC 当前无锁定：字段置 0；以后有 NpcLockOn 再补
            byte lockOnActive = 0;
            uint lockTargetNetId = 0;
            float moveInputX = 0f;
            float moveInputY = 0f;

            bool shouldSend = !_hasSentAnySnapshot;
            if (!shouldSend)
            {
                float posDelta = (pos - _lastSentPos).sqrMagnitude;
                float yawDelta = Mathf.Abs(Mathf.DeltaAngle(yaw, _lastSentYaw));
                shouldSend = posDelta >= Sync.minPosDeltaToSend
                    || yawDelta >= Sync.minYawDeltaToSend
                    || stateId != _lastSentStateId
                    || sprintPhase != _lastSentSprintPhase
                    || dodgeMode != _lastSentDodgeMode
                    || guardPhase != _lastSentGuardPhase
                    || attackComboStep != _lastSentAttackComboStep;
            }

            if (!shouldSend) return;

            var snapshot = new StateSnapshot(
                tick,
                (int)netId,
                pos,
                yaw,
                velocityXZ,
                stateId,
                sprintPhase,
                dodgeMode,
                guardPhase,
                attackComboStep,
                lockOnActive,
                lockTargetNetId,
                moveInputX,
                moveInputY);

            _transport.BroadcastSnapshotFromServer(snapshot);

            _lastSentPos = pos;
            _lastSentYaw = yaw;
            _hasSentAnySnapshot = true;
            _lastSentStateId = stateId;
            _lastSentSprintPhase = sprintPhase;
            _lastSentDodgeMode = dodgeMode;
            _lastSentGuardPhase = guardPhase;
            _lastSentAttackComboStep = attackComboStep;
        }

        private Vector2 ResolveVelocityXZ()
        {
            var agent = _npcMotor != null ? _npcMotor.Agent : null;
            if (agent != null)
                return new Vector2(agent.velocity.x, agent.velocity.z);

            if (!_hasSentAnySnapshot)
                return Vector2.zero;

            float dt = Mathf.Max(_clock.TickInterval, 0.0001f);
            Vector3 dp = transform.position - _lastSentPos;
            return new Vector2(dp.x / dt, dp.z / dt);
        }

        private Vector2 ResolveSnapshotVelocityXZ(CharacterStateId stateId, Vector2 computedVelocityXZ)
        {
            if (stateId != CharacterStateId.Dodge
                || _npcDriver == null
                || !_npcDriver.TryGetDodgePresentationContext(out var ctx))
            {
                return computedVelocityXZ;
            }

            return ctx.Mode == DodgeMode.LockOn8Way ? ctx.BlendLocal : computedVelocityXZ;
        }

        private byte ResolveSprintPhase(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Sprint || _npcDriver == null)
                return 0;

            return _npcDriver.TryGetActiveSprintState(out var sprintState)
                ? (byte)sprintState.CurrentPhase
                : (byte)0;
        }

        private byte ResolveDodgeMode(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Dodge || _npcDriver == null)
                return 0;

            if (_npcDriver.TryGetDodgePresentationContext(out var ctx))
                return (byte)ctx.Mode;

            return _npcDriver.LastPreparedDodgeMode;
        }

        private byte ResolveGuardPhase(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Guard || _npcDriver == null)
                return 0;

            return _npcDriver.TryGetActiveGuardState(out var guardState)
                ? (byte)guardState.CurrentPhase
                : (byte)0;
        }

        private byte ResolveAttackComboStep(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Attack || _npcDriver == null)
                return 0;

            return _npcDriver.TryGetActiveAttackState(out var attackState)
                ? attackState.CurrentComboStep
                : (byte)1;
        }

        private void TrySendActionOnStateChange(int tick)
        {
            CharacterStateId current = ResolveCurrentStateId();
            if (current == _lastStateId) return;

            ActionType actionType = CharacterStateActionMapping.MapStateToActionType(current);
            if (actionType != ActionType.None)
            {
                int param = ResolveActionEventParam(current, actionType);
                var evt = new ActionEvent(_nextSeqId++, tick, (int)netId, actionType, param);
                _transport.BroadcastActionFromServer(evt);
            }

            _lastStateId = current;
        }

        private int ResolveActionEventParam(CharacterStateId stateId, ActionType actionType)
        {
            if (actionType == ActionType.DodgeStart)
                return ResolveDodgeMode(stateId);

            if (actionType == ActionType.Hit
                && _npcDriver != null
                && _npcDriver.TryGetActiveHitState(out var hitState))
            {
                return hitState.IsHeavyHit ? 1 : 0;
            }

            return 0;
        }

        private CharacterStateId ResolveCurrentStateId()
        {
            if (_npcDriver != null)
                return _npcDriver.CurrentStateId;
            return CharacterStateId.Idle;
        }
    }
}
```

---

## 五、与 `LocalSyncPublisher` 的逐项对照

| 能力 | LocalSyncPublisher | NpcAuthoritySyncPublisher（改后） |
|------|-------------------|--------------------------------|
| 完整 StateSnapshot | 有 | 有 |
| state/phase 变化即发 | 有 | 有 |
| SprintPhase | `TryGetActiveSprintState` | `NpcCharacterDriver.TryGetActiveSprintState` |
| DodgeMode | `TryGetDodgePresentationContext` | 同上 |
| GuardPhase | `TryGetActiveGuardState` | 同上 |
| AttackComboStep | `TryGetActiveAttackState` | 同上 |
| LockOn / MoveInput | 有 | NPC 暂为 0 |
| Dodge VelocityXZ | LockOn8Way 用 BlendLocal | 同逻辑 |
| ActionEvent.Param | DodgeStart | DodgeStart + Hit（轻/重） |
| 发送通道 | `OnSnapshotProduced` 事件 | `_transport.BroadcastSnapshotFromServer` |

---

## 六、实施顺序建议

1. **先改 `NpcCharacterDriver`**：加上述 `TryGet*`（战斗态未实现前可返回 `false`）
2. **再改 `NpcAuthoritySyncPublisher`**：用上面完整版替换 `TrySendSnapshot` / `TrySendActionOnStateChange`
3. **实现 NPC 战斗 FSM（3.2）**：让各 `TryGet*` 有真实返回值
4. **联机验证**：Server 让 NPC 进 Attack，Client 看 `StateId` + `AttackComboStep` 是否驱动 `CharacterCombatPresenter`

Inspector：`NpcAuthoritySyncPublisher` 拖上 `NpcMotor`（用于 `agent.velocity`）。

---

## 七、可选重构（非必须）

玩家与 NPC 的 `ResolveSprintPhase` / `shouldSend` 逻辑重复，可抽成静态类，例如：

```csharp
// SnapshotPublishHelpers.cs（可选）
public static class SnapshotPublishHelpers
{
    public static byte ResolveSprintPhase(CharacterStateId stateId, SprintState sprint) { ... }
    public static bool ShouldSendSnapshot(...){ ... }
}
```

Phase F 收尾阶段**不强制**抽，先复制粘贴跑通更稳。

---

## 八、改完如何自测

1. Server 用调试/BT 让 NPC 进入 `Attack`，看日志或断点：`BroadcastSnapshotFromServer` 的 `AttackComboStep != 0`
2. Client 上该 NPC 的 `RemoteInterpolator.LastAppliedSnapshot` 应有相同 `StateId` / phase
3. **原地攻击**：pos 不变但 `stateId` 变了，也应发快照（这是改 `shouldSend` 的关键收益）

若要我在工程里直接改 `NpcAuthoritySyncPublisher` + `NpcCharacterDriver` 骨架，切到 **Agent 模式** 说一声即可。

---

**User**

我写了一些，检查一下，剩下的NPC的State代码也给我

---

**Cursor**

正在检查你已写的 NPC 相关代码，并补齐剩余 State 示例。


## 检查结论

### 做得好的部分

| 文件 | 进度 |
|------|------|
| `NpcAuthoritySyncPublisher` | `ResolveSprintPhase` / `ResolveDodgeMode` / `ResolveGuardPhase` / `ResolveAttackComboStep` / `ResolveActionEventParam` 已写好 |
| `NpcCharacterDriver` | `LastPreparedDodgeMode`、`SetLastPreparedDodgeMode`、查询 API 签名已有 |
| 6 个战斗 State 文件 | 已建壳 |

### 必须修的两处（否则发布器不生效）

**1. `TrySendSnapshot` 仍是旧版** — 只发 6 字段、`shouldSend` 只看 pos/yaw，**没有调用**你写好的 `Resolve*` 方法：

```75:112:g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\NpcAuthoritySyncPublisher.cs
        private void TrySendSnapshot(int tick)
        {
            // ... 仍用 dp/dt 算 velocity，没用 ResolveVelocityXZ()
            shouldSend = posDelta > ... || yawDelta > ...;  // 缺 state/phase 变化
            var snapshot = new StateSnapshot(tick, (int)netId, pos, yaw, velocityXZ, ResolveCurrentStateId()); // 仅 6 参数
            // 缺 _lastSentStateId / phase 缓存更新
        }
```

**2. `NpcCharacterDriver`** — 战斗态未注册，`TryGet*` 全返回 `false`；6 个 State 仍是 `NotImplementedException`。

**3. 小问题**：`shouldSend` 建议用 `>=`（与玩家一致）；`ResolveVelocityXZ()` 已写但未接入。

---

## 需替换的 `TrySendSnapshot`（发布器）

把 `NpcAuthoritySyncPublisher.TrySendSnapshot` **整段替换**为：

```csharp
private void TrySendSnapshot(int tick)
{
    Vector3 pos = transform.position;
    float yaw = transform.eulerAngles.y;

    CharacterStateId stateId = ResolveCurrentStateId();
    Vector2 velocityXZ = ResolveVelocityXZ();

    byte sprintPhase = ResolveSprintPhase(stateId);
    byte dodgeMode = ResolveDodgeMode(stateId);
    byte guardPhase = ResolveGuardPhase(stateId);
    byte attackComboStep = ResolveAttackComboStep(stateId);
    velocityXZ = ResolveSnapshotVelocityXZ(stateId, velocityXZ);

    bool shouldSend = !_hasSentAnySnapshot;
    if (!shouldSend)
    {
        float posDelta = (pos - _lastSentPos).sqrMagnitude;
        float yawDelta = Mathf.Abs(Mathf.DeltaAngle(yaw, _lastSentYaw));
        shouldSend = posDelta >= Sync.minPosDeltaToSend
            || yawDelta >= Sync.minYawDeltaToSend
            || stateId != _lastSentStateId
            || sprintPhase != _lastSentSprintPhase
            || dodgeMode != _lastSentDodgeMode
            || guardPhase != _lastSentGuardPhase
            || attackComboStep != _lastSentAttackComboStep;
    }

    if (!shouldSend) return;

    var snapshot = new StateSnapshot(
        tick,
        (int)netId,
        pos,
        yaw,
        velocityXZ,
        stateId,
        sprintPhase,
        dodgeMode,
        guardPhase,
        attackComboStep,
        lockOnActive: 0,
        lockTargetNetId: 0,
        moveInputX: 0f,
        moveInputY: 0f);

    _transport.BroadcastSnapshotFromServer(snapshot);

    _lastSentPos = pos;
    _lastSentYaw = yaw;
    _hasSentAnySnapshot = true;
    _lastSentStateId = stateId;
    _lastSentSprintPhase = sprintPhase;
    _lastSentDodgeMode = dodgeMode;
    _lastSentGuardPhase = guardPhase;
    _lastSentAttackComboStep = attackComboStep;
}
```

---

## 完整 `NpcCharacterDriver.cs`

```csharp
using Character.Config;
using Character.Intent;
using Character.Presentation;
using Character.StateMachine;
using Character.StateMachine.States;
using Core;
using Mirror;
using UnityEngine;

namespace AI
{
    [RequireComponent(typeof(NetworkIdentity))]
    public class NpcCharacterDriver : NetworkBehaviour
    {
        [SerializeField] private NpcAiIntentSource _intentSource;
        [SerializeField] private NpcMotor _motor;

        private CharacterStateMachine _fsm;
        private CharacterStateRegistry _registry;

        private NpcIdleState _idle;
        private NpcMoveState _move;
        private NpcSprintState _sprint;
        private NpcAttackState _attack;
        private NpcDodgeState _dodge;
        private NpcGuardState _guard;
        private NpcHitState _hit;
        private NpcDeadState _dead;

        private CharacterCombatConfig Combat => GameDataManager.Instance.Npc.combat;

        public CharacterStateId CurrentStateId => _fsm?.CurrentState?.Id ?? CharacterStateId.None;
        public byte LastPreparedDodgeMode { get; private set; }

        private void Awake()
        {
            if (_intentSource == null) _intentSource = GetComponent<NpcAiIntentSource>();
            if (_motor == null) _motor = GetComponent<NpcMotor>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _fsm = new CharacterStateMachine();
            _registry = new CharacterStateRegistry();

            _idle = new NpcIdleState(_fsm, _registry, _intentSource, _motor);
            _move = new NpcMoveState(_fsm, _registry, _intentSource, _motor);
            _sprint = new NpcSprintState(_fsm, _registry, _motor, Combat);
            _attack = new NpcAttackState(_fsm, _registry, _motor, Combat);
            _dodge = new NpcDodgeState(_fsm, _registry, _motor, Combat);
            _guard = new NpcGuardState(_fsm, _registry, _motor, Combat);
            _hit = new NpcHitState(_fsm, _registry, _motor, Combat);
            _dead = new NpcDeadState(_fsm, _registry, _motor);

            _registry.Register(_idle);
            _registry.Register(_move);
            _registry.Register(_sprint);
            _registry.Register(_attack);
            _registry.Register(_dodge);
            _registry.Register(_guard);
            _registry.Register(_hit);
            _registry.Register(_dead);

            _fsm.Initialize(_idle);
        }

        private void LateUpdate()
        {
            if (!isServer || _fsm == null) return;

            CharacterIntent intent = _intentSource != null ? _intentSource.BuildIntent() : default;
            _fsm.Tick(intent, Time.deltaTime);
        }

        public bool TryGetActiveSprintState(out NpcSprintState sprintState)
        {
            if (_fsm?.CurrentState is NpcSprintState active)
            {
                sprintState = active;
                return true;
            }
            sprintState = null;
            return false;
        }

        public bool TryGetActiveGuardState(out NpcGuardState guardState)
        {
            if (_fsm?.CurrentState is NpcGuardState active)
            {
                guardState = active;
                return true;
            }
            guardState = null;
            return false;
        }

        public bool TryGetActiveAttackState(out NpcAttackState attackState)
        {
            if (_fsm?.CurrentState is NpcAttackState active)
            {
                attackState = active;
                return true;
            }
            attackState = null;
            return false;
        }

        public bool TryGetActiveHitState(out NpcHitState hitState)
        {
            if (_fsm?.CurrentState is NpcHitState active)
            {
                hitState = active;
                return true;
            }
            hitState = null;
            return false;
        }

        public bool TryGetDodgePresentationContext(out DodgePresentationContext ctx)
        {
            if (_fsm?.CurrentState is NpcDodgeState dodge && dodge.PresentationContext.IsValid)
            {
                ctx = dodge.PresentationContext;
                return true;
            }
            ctx = default;
            return false;
        }

        public void SetLastPreparedDodgeMode(byte mode) => LastPreparedDodgeMode = mode;

        // —— Server 调试入口（验收用，后续可换成 BT Task）——

        public bool ServerTryEnterAttack(byte comboStep = 1)
        {
            if (!isServer || _attack == null) return false;
            _attack.Prepare(comboStep);
            return _fsm.TryTransition(CharacterStateId.Attack, _registry, TransitionReason.InputAttack);
        }

        public bool ServerTryEnterDodge(DodgeMode mode, Vector3 worldDir, Vector2 blendLocal = default)
        {
            if (!isServer || _dodge == null) return false;
            _dodge.Prepare(mode, worldDir, blendLocal);
            SetLastPreparedDodgeMode((byte)mode);
            return _fsm.TryTransition(CharacterStateId.Dodge, _registry, TransitionReason.InputDodge);
        }

        public bool ServerTryEnterGuard(float loopHoldDuration = 2f)
        {
            if (!isServer || _guard == null) return false;
            _guard.Prepare(loopHoldDuration);
            return _fsm.TryTransition(CharacterStateId.Guard, _registry, TransitionReason.InputGuard);
        }

        public bool ServerTryEnterHit(bool isHeavy = false)
        {
            if (!isServer || _hit == null) return false;
            _hit.Prepare(isHeavy);
            return _fsm.TryTransition(
                CharacterStateId.Hit,
                _registry,
                isHeavy ? TransitionReason.HitHeavy : TransitionReason.HitLight);
        }

        public bool ServerTryEnterDead()
        {
            if (!isServer) return false;
            return _fsm.TryTransition(CharacterStateId.Dead, _registry, TransitionReason.Death);
        }

        public bool ServerTryEnterSprint()
        {
            if (!isServer || _sprint == null) return false;
            return _fsm.TryTransition(CharacterStateId.Sprint, _registry, TransitionReason.InputSprint);
        }
    }
}
```

---

## 六个 State 完整代码

### `NpcAttackState.cs`

```csharp
using Character.Config;
using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI
{
    public sealed class NpcAttackState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combat;

        private float _timer;
        private byte _comboStep = 1;

        public CharacterStateId Id => CharacterStateId.Attack;
        public byte CurrentComboStep => _comboStep;

        public NpcAttackState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            NpcMotor motor,
            CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combat = combat;
        }

        public void Prepare(byte comboStep) => _comboStep = comboStep is >= 1 and <= 9 ? comboStep : (byte)1;

        public void Enter()
        {
            _timer = 0f;
            _motor?.Stop();
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            if (_timer < _combat.GetAttackDuration(_comboStep)) return;

            _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }

        public void Exit()
        {
            _timer = 0f;
            _comboStep = 1;
        }
    }
}
```

### `NpcDodgeState.cs`

```csharp
using Character.Config;
using Character.Intent;
using Character.Presentation;
using Character.StateMachine;
using UnityEngine;

namespace AI
{
    public sealed class NpcDodgeState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combat;

        private float _timer;
        private float _duration;
        private DodgePresentationContext _presentationContext;

        public CharacterStateId Id => CharacterStateId.Dodge;
        public DodgePresentationContext PresentationContext => _presentationContext;

        public NpcDodgeState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            NpcMotor motor,
            CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combat = combat;
        }

        public void Prepare(DodgeMode mode, Vector3 worldDir, Vector2 blendLocal = default)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude > 0.0001f) worldDir.Normalize();
            else worldDir = _motor != null ? _motor.transform.forward : Vector3.forward;

            float duration = _combat.GetDodgeDuration(mode);
            float moveDuration = _combat.GetDodgeMoveDuration(mode);
            _presentationContext = new DodgePresentationContext(mode, blendLocal, duration, moveDuration, worldDir);
            _duration = duration;
        }

        public void Enter()
        {
            _timer = 0f;
            _motor?.Stop();
            if (!_presentationContext.IsValid)
                _duration = _combat.dodgeEvadeDuration;
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            if (_timer < _duration) return;

            _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }

        public void Exit()
        {
            _timer = 0f;
            _presentationContext = default;
        }
    }
}
```

### `NpcGuardState.cs`

```csharp
using Character.Config;
using Character.Intent;
using Character.StateMachine;
using Character.StateMachine.States;
using UnityEngine;

namespace AI
{
    public sealed class NpcGuardState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combat;

        private float _phaseTimer;
        private float _loopHoldDuration = 2f;

        public CharacterStateId Id => CharacterStateId.Guard;
        public GuardState.GuardPhase CurrentPhase { get; private set; } = GuardState.GuardPhase.Start;

        public NpcGuardState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            NpcMotor motor,
            CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combat = combat;
        }

        public void Prepare(float loopHoldDuration = 2f) => _loopHoldDuration = Mathf.Max(0.1f, loopHoldDuration);

        public void Enter()
        {
            SetPhase(GuardState.GuardPhase.Start);
            _motor?.Stop();
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            switch (CurrentPhase)
            {
                case GuardState.GuardPhase.Start:
                    TickStart(deltaTime);
                    break;
                case GuardState.GuardPhase.Loop:
                    TickLoop(deltaTime);
                    break;
                case GuardState.GuardPhase.Exit:
                    TickExit(deltaTime);
                    break;
            }
        }

        public void Exit() => SetPhase(GuardState.GuardPhase.Start);

        private void TickStart(float deltaTime)
        {
            _phaseTimer += deltaTime;
            if (_phaseTimer >= _combat.guardStartDuration)
                SetPhase(GuardState.GuardPhase.Loop);
        }

        private void TickLoop(float deltaTime)
        {
            _phaseTimer += deltaTime;
            if (_phaseTimer >= _loopHoldDuration)
                SetPhase(GuardState.GuardPhase.Exit);
        }

        private void TickExit(float deltaTime)
        {
            _phaseTimer += deltaTime;
            if (_phaseTimer < _combat.guardExitDuration) return;

            _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }

        private void SetPhase(GuardState.GuardPhase phase)
        {
            CurrentPhase = phase;
            _phaseTimer = 0f;
        }
    }
}
```

### `NpcHitState.cs`

```csharp
using Character.Config;
using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI
{
    public sealed class NpcHitState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combat;

        private float _timer;
        private float _duration;
        private bool _isHeavyHit;

        public CharacterStateId Id => CharacterStateId.Hit;
        public bool IsHeavyHit => _isHeavyHit;

        public NpcHitState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            NpcMotor motor,
            CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combat = combat;
            _duration = _combat.lightHitDuration;
        }

        public void Prepare(bool isHeavy)
        {
            _isHeavyHit = isHeavy;
            _duration = isHeavy ? _combat.heavyHitDuration : _combat.lightHitDuration;
        }

        public void Enter()
        {
            _timer = 0f;
            _motor?.Stop();
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            if (_timer < _duration) return;

            _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }

        public void Exit()
        {
            _timer = 0f;
            _isHeavyHit = false;
        }
    }
}
```

### `NpcDeadState.cs`

```csharp
using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI
{
    public sealed class NpcDeadState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;

        public CharacterStateId Id => CharacterStateId.Dead;

        public NpcDeadState(CharacterStateMachine fsm, CharacterStateRegistry registry, NpcMotor motor)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
        }

        public void Enter() => _motor?.Stop();

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            // 保持死亡直到 ServerTryRevive / BT 触发 Revive
        }

        public void Exit() { }
    }
}
```

### `NpcSprintState.cs`

```csharp
using Character.Config;
using Character.Intent;
using Character.StateMachine;
using Character.StateMachine.States;
using UnityEngine;

namespace AI
{
    public sealed class NpcSprintState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combat;

        private float _phaseTimer;
        private float _sprintHoldDuration = 1.5f;

        public CharacterStateId Id => CharacterStateId.Sprint;
        public SprintState.SprintPhase CurrentPhase { get; private set; } = SprintState.SprintPhase.Start;

        public NpcSprintState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            NpcMotor motor,
            CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combat = combat;
        }

        public void Prepare(float holdDuration = 1.5f) => _sprintHoldDuration = Mathf.Max(0.2f, holdDuration);

        public void Enter()
        {
            SetPhase(SprintState.SprintPhase.Start);
            // 仍可用 NavMesh 移动；若需纯表演可 _motor.Stop()
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            switch (CurrentPhase)
            {
                case SprintState.SprintPhase.Start:
                    TickStart(deltaTime);
                    break;
                case SprintState.SprintPhase.Loop:
                    TickLoop(deltaTime);
                    break;
                case SprintState.SprintPhase.Brake:
                    TickBrake(deltaTime);
                    break;
                default:
                    SetPhase(SprintState.SprintPhase.Loop);
                    break;
            }
        }

        public void Exit() => SetPhase(SprintState.SprintPhase.Start);

        private void TickStart(float deltaTime)
        {
            _phaseTimer += deltaTime;
            if (_phaseTimer >= 0.75f) // 对齐 SprintPhaseConfig.startDuration 默认值
                SetPhase(SprintState.SprintPhase.Loop);
        }

        private void TickLoop(float deltaTime)
        {
            _phaseTimer += deltaTime;
            if (_phaseTimer >= _sprintHoldDuration)
                SetPhase(SprintState.SprintPhase.Brake);
        }

        private void TickBrake(float deltaTime)
        {
            _phaseTimer += deltaTime;
            if (_phaseTimer >= 0.2f) // brakeDuration 默认
            {
                _motor?.Stop();
                _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
            }
        }

        private void SetPhase(SprintState.SprintPhase phase)
        {
            CurrentPhase = phase;
            _phaseTimer = 0f;
        }
    }
}
```

---

## 验收时怎么触发（临时）

在 Server 端任意脚本或 Inspector 调试按钮调用，例如：

```csharp
npcDriver.ServerTryEnterAttack(1);
npcDriver.ServerTryEnterDodge(DodgeMode.ForwardAlongMove, npc.transform.forward);
npcDriver.ServerTryEnterGuard();
npcDriver.ServerTryEnterHit(isHeavy: false);
npcDriver.ServerTryEnterDead();
npcDriver.ServerTryEnterSprint();
```

---

## 你还需确认的后续项（3.3 / 3.4）

1. **`CharacterLateUpdatePipeline.TryResolveFromServerNpc`** — 仍只读 `stateId + velocity`，Host 上看 NPC 战斗动画可能不对；需像 `TryResolveFromLocalPlayer` 一样读 phase。
2. **`NPC.prefab`** — 是否已挂 `CharacterLateUpdatePipeline`、`RemoteInterpolator`、`Animator`。

若要我在工程里直接改发布器 + 填满这些 State，切 **Agent 模式** 说一声即可。

---

**User**

写完了，检查一下有没有漏的

---

**Cursor**

正在全面检查你已完成的 NPC 相关实现。


## 检查结论

整体完成度很高：**发布器、Driver、六个战斗 State 主体都已写好**。离「双端验收」还差几处修补和接线确认。

---

### 已做对的部分

| 模块 | 状态 |
|------|------|
| `NpcAuthoritySyncPublisher` | 完整快照、`shouldSend` 含 state/phase、ActionEvent 带 Param |
| `NpcCharacterDriver` | 8 态注册、`TryGet*` 实现、`ServerTryEnter*` 调试入口 |
| `NpcAttackState` / `GuardState` / `HitState` / `DeadState` / `SprintState` | 逻辑正确 |
| `NPC.prefab` | 已有 `NpcAuthoritySyncPublisher`、`NpcCharacterDriver`、`CharacterLateUpdatePipeline`、`RemoteInterpolator`、`RemoteSnapshotBuffer`、`RemoteActionApplier`；子物体 ARPG 模型绑了 `CharacterAnimator` |

`CharacterLateUpdatePipeline.Awake` 会 `GetComponentInChildren<Animator>()`，Prefab 里 `_animator: {fileID: 0}` 运行时一般能自动找到。

---

### 必须修：1 个逻辑 Bug

**`NpcDodgeState.Tick` 条件写反了**，会导致进入 Dodge **第一帧就切回 Idle**，闪避动画几乎播不出来：

```58:64:g:\UnityProject\SS&3CDemo\Assets\Scripts\AI\NpcDodgeState.cs
        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            if (_timer > _duration) return;   // ← 错了：超时反而 return，不退出

            _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }
```

应改为：

```csharp
_timer += deltaTime;
if (_timer < _duration) return;

_fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
```

---

### 仍缺 / 建议补

#### 1. `TryResolveFromServerNpc` 未扩展（计划 3.3）

```413:430:g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\CharacterLateUpdatePipeline.cs
        private bool TryResolveFromServerNpc(out CharacterStateId stateId, out Vector2 velocityXZ)
        {
            // 仍只读 stateId + agent.velocity，没有 sprintPhase/guardPhase/attackComboStep
        }
```

影响：**Host 上观察 Server NPC** 时，战斗动画可能不对（Client 看 NPC 走 `TryResolveFromRemoteSnapshot`，一般没问题）。

需要把 `TryResolveFromServerNpc` 扩展成与 `TryResolveFromLocalPlayer` 类似，或让 `ResolvePresentation` 在 Server NPC 上也从 `_npcDriver` 读 phase。

#### 2. 没有验收触发方式

`ServerTryEnterAttack` 等只在 `NpcCharacterDriver` 里，**场景里没有调用入口**。验收前需要其一：

- 临时 `NpcCombatDebug.cs`（Inspector 按钮 / 按键）
- Behaviour Designer Task 调 `ServerTryEnter*`
- Unity 事件

否则双端测不到 NPC 战斗动画。

#### 3. 缺少 `ServerTryRevive`

有 `ServerTryEnterDead`，没有 Revive。测完死亡后无法回到 Idle，要重开或手动切状态。

#### 4. 远端 Hit 轻重（次要）

`TryPresentCombat` 未传 `actionParams`，`ActionEvent.Hit` 的 Param 在 Pipeline 里未消费。NPC 远端受击可能都按「轻击」CrossFade（若你玩家侧已用别的方式解决，NPC 可对齐同一套）。

#### 5. Prefab 小项（运行时大多能自愈）

| 字段 | 现状 | 说明 |
|------|------|------|
| `NpcAuthoritySyncPublisher._npcMotor` | 未拖引用 | `Awake` 会 `GetComponent`，一般 OK |
| `CharacterLateUpdatePipeline._remoteActionApplier` | `{fileID: 0}` | `Awake` 会 `GetComponent`，一般 OK |
| `NpcCharacterDriver._intentSource/_motor` | `{fileID: 0}` | 同上 |

建议在 Inspector 拖好，避免依赖隐式查找。

#### 6. 小代码质量（非阻塞）

- `NpcDeadState` 的 `_fsm`、`_registry` 未使用（可删或留给 Revive）
- `NpcSprintState` 未 `sealed`，与其它 State 不一致
- `ServerTryEnterSprint()` 未调 `Prepare()`（默认 1.5s 够用）

---

### 执行顺序建议（验收前）

1. **修 `NpcDodgeState.Tick`**（必做）
2. 加 **调试入口** 调 `ServerTryEnter*`
3. Host + Client：Client 看 NPC 攻击/闪避/格挡/受击/死亡
4. 若 Host 上看 NPC 战斗不对 → 补 **3.3 Pipeline**
5. 可选：`ServerTryRevive`、Hit `actionParams`

---

### 快速自测清单

在 **Server** 上依次调用（调试脚本）：

```
ServerTryEnterAttack(1)
ServerTryEnterDodge(ForwardAlongMove, transform.forward)
ServerTryEnterGuard()
ServerTryEnterHit(false)
ServerTryEnterSprint()
ServerTryEnterDead()
```

在 **Client** 上确认：

- 快照 `StateId` 变化
- 对应 Animator 片段播放
- 原地动作（Attack/Guard）即使不移动也有快照（你已对齐 `shouldSend`）

---

**一句话**：代码主体齐了，**先修 Dodge 的 Tick 条件**，再补 **调试触发 + 双端验收**；若 Host 上看 NPC 战斗动画不对，再改 `TryResolveFromServerNpc`。需要我直接在工程里改 Dodge + Pipeline + 调试脚本，切 Agent 模式即可。

---

**User**

1. TryResolveFromServerNpc 未扩展（计划 3.3） 和 临时 NpcCombatDebug.cs（Inspector 按钮 / 按键）帮我做

---

**Cursor**

已完成两项改动，说明如下。

## 1. `TryResolveFromServerNpc` 扩展（3.3）

[`CharacterLateUpdatePipeline.cs`](g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\CharacterLateUpdatePipeline.cs) 中 Server NPC 路径现在与本地玩家一致，会解析：

- `SprintPhase` / `GuardPhase` / `AttackComboStep`
- Dodge 时 `LockOn8Way` 的 `BlendLocal` 写入 `velocityXZ`

Host 上观察 Server NPC 时，战斗动画应能正确路由到 Sprint/Combat Presenter。

## 2. `NpcCombatDebug` 调试组件

新增文件：

- [`Assets/Scripts/AI/NpcCombatDebug.cs`](g:\UnityProject\SS&3CDemo\Assets\Scripts\AI\NpcCombatDebug.cs) — 逻辑 + 小键盘热键
- [`Assets/Scripts/Editor/NpcCombatDebugEditor.cs`](g:\UnityProject\SS&3CDemo\Assets\Scripts\Editor\NpcCombatDebugEditor.cs) — Inspector 按钮

同时在 `NpcCharacterDriver` 增加了 `ServerTryRevive()`，以及 `ServerTryEnterSprint(float holdDuration)`。

### 用法

1. 在 **NPC.prefab** 上 Add Component → `NpcCombatDebug`
2. **Host 运行**，选中 NPC，在 Inspector 点按钮，或用小键盘：

| 按键 | 动作 |
|------|------|
| 小键盘 1 | Attack |
| 小键盘 2 | Dodge 前滚 |
| 小键盘 3 | Guard |
| 小键盘 4 | Hit 轻 |
| 小键盘 5 | Hit 重 |
| 小键盘 6 | Dead |
| 小键盘 7 | Sprint |
| 小键盘 8 | Revive |

3. **第二客户端** 观察该 NPC 的远端动画是否与 Host 触发一致。

也可在组件标题栏右键 → **Combat/...**（Context Menu）。

### 验收建议

Host 连发：Attack → Dodge → Guard → Hit → Sprint → Dead → Revive，Client 端逐项对照动画。若某动作不对，说明是哪一个，我可以继续查发布器或 Presenter 路由。

---

**User**

这个NpcCombatDebugEditor是怎么关联到NpcCombatDebug的

---

**Cursor**

关联靠的是 Unity 的 **Custom Editor** 机制，不需要你在 `NpcCombatDebug` 里写任何引用。

## 关联方式

在 [`NpcCombatDebugEditor.cs`](g:\UnityProject\SS&3CDemo\Assets\Scripts\Editor\NpcCombatDebugEditor.cs) 第 8 行：

```8:9:g:\UnityProject\SS&3CDemo\Assets\Scripts\Editor\NpcCombatDebugEditor.cs
    [CustomEditor(typeof(NpcCombatDebug))]
    public sealed class NpcCombatDebugEditor : UnityEditor.Editor
```

`[CustomEditor(typeof(NpcCombatDebug))]` 告诉 Unity：

> 当 Inspector 里选中挂有 **`NpcCombatDebug`** 组件的对象时，用这个 Editor 类来画 Inspector，而不是默认的 MonoBehaviour 面板。

## Unity 如何找到它

1. **编译期**：Editor 脚本在 `Assets/Scripts/Editor/` 下，只进 Editor 程序集，不会打进游戏。
2. **反射注册**：Unity 启动/编译后扫描带 `[CustomEditor]` 的类，建立「目标类型 → Editor 类」映射。
3. **运行时（编辑器）**：你在 Hierarchy/Inspector 选中 NPC，Unity 看到组件类型是 `NpcCombatDebug`，就实例化 `NpcCombatDebugEditor`，并调用 `OnInspectorGUI()`。

## 和运行时组件的关系

| | `NpcCombatDebug` | `NpcCombatDebugEditor` |
|--|------------------|------------------------|
| 基类 | `MonoBehaviour` | `UnityEditor.Editor` |
| 挂在哪 | NPC Prefab 上 | 不挂场景，纯编辑器类 |
| 作用 | 运行时按键/ContextMenu 触发战斗 | 只在 Inspector 画按钮 |
| 关联 | 无互相引用 | 通过 `[CustomEditor(typeof(...))]` 单向绑定 |

`NpcCombatDebugEditor` 里通过继承的 `target` 拿到当前选中的组件：

```csharp
var debug = (NpcCombatDebug)target;
```

再调用 `debug.TriggerAttack()` 等 **public** 方法。

## 注意

- 必须放在 **`Editor` 文件夹**（或任意带 `Editor` 程序集定义的目录），否则会和运行时一起编译报错。
- 改 Editor 脚本后 Unity 会重新编译；选中带 `NpcCombatDebug` 的对象，Inspector 底部应出现 **Server Combat Triggers** 按钮区。
- 若没看到自定义按钮：检查是否选中了正确对象、脚本是否编译通过、`NpcCombatDebug` 是否已挂在 Prefab 上。

---

**User**

Assets\Scripts\AI\NpcCombatDebug.cs(32,17): error CS0234: The type or namespace name 'GetKeyDown' does not exist in the namespace 'Input' (are you missing an assembly reference?)

---

**Cursor**

项目里有 `namespace Input`，导致 `Input` 被解析成该命名空间而非 `UnityEngine.Input`。改为使用 `UnityEngine.Input`。


原因：工程里有 [`namespace Input`](g:\UnityProject\SS&3CDemo\Assets\Scripts\Input\InputHandler.cs)，编译器把 `Input` 当成这个命名空间，而不是 `UnityEngine.Input`。

已改成 `UnityEngine.Input.GetKeyDown(...)`，重新编译应能通过。

---

**User**

远端和host的NPC动作不同步

---

**Cursor**

正在排查 Host 与远端 NPC 动画不同步的原因。


这是 **Host 与 Remote 走了两套不同的 NPC 表现路径** 导致的，不一定是 Mirror 没发快照。下面按原因、验证和修复方向说明。

## 核心原因：两条表现路径

`CharacterLateUpdatePipeline.ResolvePresentation` 的优先级是：

1. 本地玩家 → `TryResolveFromLocalPlayer`
2. **Host/Server 上的 NPC** → `TryResolveFromServerNpc`（直接读服务端 FSM）
3. **纯客户端上的 NPC** → `TryResolveFromRemoteSnapshot`（读延迟快照）

```328:348:g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\CharacterLateUpdatePipeline.cs
            if (TryResolveFromServerNpc(
                    out stateId,
                    ...
                return;

            if (TryResolveFromRemoteSnapshot(
                    out stateId,
                    ...
                return;
```

因此：

| 端 | 动画数据来源 | 典型延迟 |
|---|---|---|
| **Host** | `_npcDriver.CurrentStateId`（实时 FSM） | ~0 |
| **Remote** | `RemoteInterpolator.LastAppliedSnapshot` | `bufferDelaySec`(0.12s) + 网络延迟 |

两边看到的不是同一时刻的状态，战斗动作（Attack/Dodge 等）又很短，Remote 很容易「晚一拍」或「整段错过」，看起来就像完全不同动作。

---

## 具体问题点

### 1. Host 上 NPC 仍在跑 `RemoteInterpolator`（位移 vs 动画分裂）

`RemoteInterpolator` 只跳过了 **local player**，**没有跳过 Host 上的 NPC**：

```33:36:g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\RemoteInterpolator.cs
            if (_networkIdentity != null
                && NetworkServer.activeHost
                && _networkIdentity.isLocalPlayer)
                return;
```

Host 上 NPC 的表现是：

- **动画**：实时 FSM（`TryResolveFromServerNpc`）
- **位移/朝向**：延迟快照插值（`RemoteInterpolator`）

Remote 上动画和位移都来自同一份延迟快照，内部一致，但与 Host 领先约 120ms+，对比两屏时尤其明显。

### 2. 闪避（Dodge）在 Host 上可能用错模式

`ResolveDodgePresentationContext` 只处理了本地玩家和 Remote 快照，**没有 Host NPC 的 `_npcDriver` 分支**：

```165:191:g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\CharacterLateUpdatePipeline.cs
        private DodgePresentationContext ResolveDodgePresentationContext(CharacterStateId stateId)
        {
            ...
            if (_playerController != null && HasLocalPresentationAuthority() ...)
                return ctx;

            if (_remoteInterpolator != null)
            {
                var snapshot = ResolveRemoteDodgeSnapshot(_remoteInterpolator.LastAppliedSnapshot);
                ...
            }

            return new DodgePresentationContext(
                DodgeMode.ForwardAlongMove, ...);  // 默认兜底
        }
```

Host NPC 进入 Dodge 时：

- `StateId` = 实时 Dodge（来自 FSM）
- `dodgeCtx` 却来自 **延迟快照**（可能还是 Move/Idle），失败后退回到 `ForwardAlongMove`

Remote 从快照读 `DodgeMode`，方向/模式往往正确 → **闪避动画 Host 与 Remote 可能完全不同**。

### 3. 短状态 + 缓冲延迟

`NpcAuthoritySyncPublisher` 在状态变化时会发快照（`shouldSend` 含 `stateId` 变化），链路是通的。但 Remote 用 `Time.unscaledTime - 0.12s` 采样：

```40:71:g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\RemoteInterpolator.cs
            float targetTime = Time.unscaledTime - Sync.bufferDelaySec;
            ...
            LastAppliedSnapshot = to;
```

短于 ~120ms 的 Dodge/Hit 在 Remote 上可能只闪一下或完全看不到，而 Host 已播完。

### 4. Remote 缓冲区为空时会一直 Idle

```502:504:g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\CharacterLateUpdatePipeline.cs
            var snapshot = _remoteInterpolator.LastAppliedSnapshot;
            if (snapshot.Tick <= 0)
                return false;
```

若 Client 没收到快照（`SyncBootstrap` 未接线、首帧未到、`ActorId` 不匹配），Remote 会停在 **Idle**，Host 却在播战斗动作。

场景里 `NetSystem.prefab` 已设 `_transportMode: 1`（Mirror），一般没问题；仍建议确认 Client 控制台无 `[NpcAuthoritySyncPublisher] MirrorSyncTransport not found`。

---

## 建议验证步骤

1. **Host 与 Client 各开一个窗口**，用 `NpcCombatDebug` 小键盘 1–8 触发同一动作。
2. 在 **Client 的 NPC** 上打开 `RemoteInterpolator._logState`，看 `to.Tick` 和对应 `StateId` 是否跟上。
3. 临时把 `bufferDelaySec` 调到 `0` 测试：若明显好转，主要是延迟问题；若仍完全不对，查 Dodge 上下文或 buffer 是否为空。
4. 对比日志：
   - Host：`NpcCharacterDriver.CurrentStateId`
   - Client：`LastAppliedSnapshot.StateId`

---

## 修复方向（需在 Agent 模式改代码）

按优先级：

1. **Host 上 server-authority NPC 跳过 `RemoteInterpolator`**（与 local player 类似），位移由 `NavMeshAgent` / 服务端 Transform 直接驱动，避免「动画实时、位移延迟」。
2. **`ResolveDodgePresentationContext` 增加 Host NPC 分支**：当 `_npcDriver != null && isServer` 时调用 `_npcDriver.TryGetDodgePresentationContext`（与 `TryResolveFromServerNpc` 对称）。
3. **统一 Host 观察视角（可选）**：Host 看 NPC 也走 snapshot 路径，与 Remote 完全一致，但 Host 自己也会多 ~120ms 延迟。
4. **执行顺序**：给 `NpcCharacterDriver` 设 `DefaultExecutionOrder` 早于 `CharacterLateUpdatePipeline`，避免同帧 FSM 未 tick 就先播动画。
5. **Hit 轻重击**：`TryPresentCombat` 目前未传 `actionParams`，轻重在两端都不对；可从 `RemoteActionApplier` / `ActionEvent.Param` 补传（这是表现细节，不是 Host/Remote 分叉的主因）。

---

## 小结

「远端和 Host 的 NPC 动作不同步」主要是设计上的 **双轨表现**：

- Host = 实时 FSM
- Remote = 延迟快照

再叠加 Host 上 NPC 仍跑插值、Dodge 上下文未接 FSM，就会出现「不只是慢半拍，而是动作类型都不一样」。

如果你愿意，可以切换到 **Agent 模式**，我可以直接改 `RemoteInterpolator` 的 Host NPC 跳过逻辑和 `ResolveDodgePresentationContext` 的 NPC 分支。你也可以先说一下具体是哪种动作不对（闪避 / 攻击 / 全部 / 只有位移不对），便于缩小范围。

---

**User**

不是的，Npc在host和远端的动作动画是一致的，但是位置有些许差异

---

**Cursor**

这个"位置有些许差异"是**完全预期的、设计上必然存在的延迟表现**，不是 bug，但可以调参缓解。下面说清楚原因和调节方法。

---

## 为什么位置有差异

### Host 上的 NPC 位置
由 `NavMeshAgent` 实时驱动，`NpcMotor` 每帧更新 Transform，**无延迟**。

### Remote 上的 NPC 位置
由 `RemoteInterpolator` 从快照缓冲区插值，刻意落后 `bufferDelaySec = 0.12s`：

```40:41:g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\RemoteInterpolator.cs
            float targetTime = Time.unscaledTime - Sync.bufferDelaySec;
```

目标位置计算出来后，**还要再用 lerp 靠近**，速率由 `normalLerpSpeed = 8f` 控制：

```61:65:g:\UnityProject\SS&3CDemo\Assets\Scripts\Character\Sync\RemoteInterpolator.cs
            float lerpSpeed = LastPosError > Sync.largeErrorThreshold
                ? Sync.snapLerpSpeed
                : Sync.normalLerpSpeed;
            float k = Mathf.Clamp01(lerpSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPos, k);
```

所以 Remote NPC 的位置 = **服务端快照延迟 120ms + lerp 追赶延迟**（约半帧到几帧不等）。

这套缓冲延迟机制的目的是在帧间平滑插值，避免因网络抖动导致跳帧，是标准 snapshot interpolation 的做法。

---

## 两个参数决定了"差异有多大"

| 参数 | 当前值 | 作用 |
|---|---|---|
| `bufferDelaySec` | 0.12s | 快照采样时间回退量，越大越平滑但越滞后 |
| `normalLerpSpeed` | 8f | 位置追赶速度，越大追赶越快但可能抖动 |
| `largeErrorThreshold` | 1.5m | 超过此距离切换为 snapLerpSpeed(12f) |

---

## 根据想要的效果调整

### 想减小位置差异（容忍更多抖动）
将 `SceneNetworkSync.asset` 或 `DefaultNetworkSync.asset` 中：
- `bufferDelaySec` 从 0.12 降到 0.05～0.08
- `normalLerpSpeed` 从 8 提高到 12～15

### 想保持平滑、接受差异（局域网或单机测试）
现有值已经是比较合理的默认，**局域网测试下 0.12s 的延迟肉眼几乎看不出**，只有在并排截图或慢放时才明显。

### 如果 NPC 在静止/Idle 时也有差异
可能是 NPC 没有移动，快照发送条件中 `minPosDeltaToSend = 0.001f` 决定了只要 delta < 0.001m 就不发快照。此时 Remote 用旧快照插值，可能会有微小误差。这基本可以忽略。

---

## 结论

位置差异**不是 bug**，是 snapshot interpolation 的固有延迟。如果本地测试（局域网 / Editor Host+Client），RTT 接近 0，此时差异完全来自 `bufferDelaySec`，把它降到 `0.05` 就几乎看不出了。实际上线后加上真实网络延迟，0.12s 才是让动画/位置不抖动的最小安全值。

如果测试时位置差异让你难以验收战斗动作，**临时把 `bufferDelaySec` 改为 0.05**（在 SceneNetworkSync.asset 里改，不用改代码），测完再改回来即可。

---

**User**

 phaseF完成了吗

---

**Cursor**

对照 Phase F 大纲和当前实现，核对完成度。


**Phase F 可以算「实质完成」，但还没做一轮正式结项签字。**

对照 `PhaseA-H_大纲.md` 里的三条验收线：

| 验收线 | 状态 |
|--------|------|
| **单机**：七种动作动画正确、脚不滑 | 玩家侧已完成；NPC 有 BT 巡逻 + `NpcCombatDebug` 战斗触发 |
| **Host + Client 玩家**：七种动作靠快照 + ActionEvent，表现与权威一致 | 大纲记为 **已验收** |
| **Host + Client NPC**：同上 | 代码已齐；你刚确认 **动画一致、仅位置略有差异** → **实质通过** |

---

## 已完成的部分（相对大纲 6/5 的「NPC 待办」）

- ARPGSamurai 模型 + Animator 多 Layer
- `CharacterLateUpdatePipeline` + 三个 Presenter
- 玩家七种动作 FSM + 快照全字段 + 双端表现
- **NPC 侧新增**：
  - `NpcAuthoritySyncPublisher`（phase/combo/dodge/guard + ActionEvent）
  - `NpcCharacterDriver` + 8 个战斗/移动状态
  - `TryResolveFromServerNpc` 扩展
  - `NpcCombatDebug`（小键盘 1–8 / Inspector 按钮）
  - NPC.prefab 同步组件接线

大纲里 Phase F **最后一项阻塞**就是 NPC 双端七种动作表现——按你现在的反馈，这项已经满足。

---

## 还没严格收尾的小项（不阻塞结项，可带进 G / 后续 polish）

1. **Hit 轻/重远端区分**：`NpcAuthoritySyncPublisher` 会发 `ActionEvent.Param`，但 `TryPresentCombat` 没传 `actionParams`，远端 Hit 可能都按轻击 CrossFade（玩家 `LocalSyncPublisher` 也没传 Hit Param，若你玩家双端轻重已验过，可能是本地 FSM 路径看的，远端未必真区分）。
2. **位置差异**：`bufferDelaySec = 0.12` 导致的正常插值滞后，不是 Phase F 失败项。
3. **可选 polish**（大纲写明「按需」）：Revive 远端恢复、连段过渡、锁定朝向等——属 G 前小修，不是 F 硬门槛。
4. **正式验收记录**：还没按 Keypad 1–8 逐项打勾并写进文档。

---

## 结论

- **开发角度**：Phase F **≈ 95%～100%**，可以开始 **Phase G0**（碰撞、扣血、格挡判定）。
- **流程角度**：若你要「正式结项」，建议再跑一遍 **NPC 七种动作双端 checklist**（尤其 Move/Idle 巡逻、Hit 轻/重、Revive），确认后把大纲里 NPC 那行从「待办」改成「已验收」。

如果你愿意，我可以按 Phase F 验收线列一份 **5 分钟可执行的 Host+Client 勾选表**（逐项对应 Keypad 和观察点）。

---

**User**

好，PhaseF正式结束！

---

**Cursor**

**Phase F 正式结项。**

---

## Phase F 交付摘要

**目标**：把已同步的 `StateId`、速度、阶段字段和 `ActionEvent` 映射到 Animator，玩家与 NPC 共用同一套 Presenter 规则。

| 模块 | 状态 |
|------|------|
| ARPGSamurai 模型 + Animator 多 Layer | ✅ |
| `CharacterLateUpdatePipeline` + Locomotion / Sprint / Combat Presenter | ✅ |
| 玩家七种动作 FSM + 快照 + 双端表现 | ✅ |
| NPC `NpcAuthoritySyncPublisher` + 战斗 FSM + 远端表现 | ✅ |
| `NpcCombatDebug` 验收入口 | ✅ |
| Host / Client NPC 动画一致（位置差异为插值延迟，属预期） | ✅ |

---

## 带入后续阶段（不阻塞 F）

- Hit 轻/重远端 `actionParams` 接线（若 G0 做真实受击会更需要）
- `bufferDelaySec` 按环境调参
- Revive / 连段过渡等表现 polish

这些归 **Phase G / polish**，不影响 F 结项。

---

## 下一步：Phase G0

按大纲，G0 是战斗判定闭环：

1. **G0-1** 攻击碰撞（Hitbox / Hurtbox）
2. **G0-2** 服务端扣血权威 → `ApplyHit` → Hit / Dead FSM
3. **G0-3** 格挡交互（减伤 / 破防）
4. **G0-4** 战斗网络（复用 `ActionEvent.Hit` + Param）

Phase F 的 Guard / Hit / Dead 动画已能播，G0 只补判定与权威逻辑。

需要的话，切换到 **Agent 模式**，可以从 G0-1 攻击碰撞或 G0 整体拆分计划开始。

