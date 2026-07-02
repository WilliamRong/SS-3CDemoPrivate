# 梳理SS-3CDemoPrivate项目

> SS-3CDemoPrivate这个项目梳理一下，这是我做的状态同步仿只狼的3CDemo

<details><summary>12 previous messages</summary>

> 我先扫一遍 `Assets/Scripts` 的结构和关键类名，看看项目是按状态机、同步层、角色控制还是输入/动画模块来分的，然后再把主流程和模块关系梳理出来。
>
> 目录已经露出很清楚的分层了：`Character` 是主体，里面又把配置、意图、状态机、同步、表现、移动、锁定拆开；`AI` 看起来是 NPC 服务端权威驱动的一套平行状态。我接下来读关键入口和同步链路，不改文件。
>
> `PlayerController` 这边是本地权威输入入口：输入被组装成 `CharacterIntent`，状态机 Tick 后，再由 LateUpdate 管线驱动表现/同步后处理。接下来我把状态机规则、动作事件和网络管线串起来，这部分决定“仿只狼”的战斗手感怎么同步。
>
> 状态机核心是“白名单转移 + 打断窗口”：例如 Attack 的生效窗内轻击不打断、重击可打断，Recovery 里可翻滚，死亡永远优先。这个设计很适合 Souls/Sekiro 类 3C，因为动作承诺和可取消窗口都集中在数据/规则层。
>
> 动作层已经比较完整：攻击有四连、冲刺攻击、闪避攻击、重击起手/派生；闪避有无敌帧和闪避攻击取消；防御有 Start/Loop/Exit 阶段。下面看 `Motor` 和 `Presenter`，因为这里会决定输入意图如何变成位移、朝向和 Animator 参数。
>
> `Motor` 里本地移动、锁定朝向、闪避位移和攻击 Root Motion 都在一个地方收口；`CharacterLateUpdatePipeline` 则统一做远端插值和动画路由。我会继续读同步文件，重点看 Snapshot 和 ActionEvent 是怎么分工的。
>
> 同步层的分工很明显：`StateSnapshot` 负责连续状态、位置、朝向、动画阶段；`ActionEvent` 负责离散动作起点，避免只靠快照错过 Attack/Dodge/Hit 的瞬间。现在我看传输层和远端 Action 应用，确认 Mirror 与本地假网络两种模式怎么接起来。
>
> 我还会补一圈 `Core` 配置入口、锁定系统和 NPC AI。这样最终梳理不只是“有哪些类”，而是能说明：谁掌权、谁同步、谁只负责表现。
>
> 配置层是一个中心 Catalog：玩家/NPC 的移动、战斗、冲刺、表现、同步参数都走 ScriptableObject，不硬塞在行为脚本里。最后我会把它整理成“模块职责 + 主循环数据流 + 同步链路 + 当前完成度/可加强点”。
>
> 锁定和镜头也已经跟同步打通：本地锁定目标会通过 netId 进快照，远端表现层可以用这个信息算锁定移动/闪避的动画参数。接下来我看 NPC 驱动，主要判断它是不是复用了玩家状态机思想，还是一套单独 AI FSM。
>
> NPC 这边是服务端权威：Behavior Designer 写黑板目标，`NpcCharacterDriver` 在 Server LateUpdate 里 Tick FSM，然后 `NpcAuthoritySyncPublisher` 广播快照/动作。它复用了通用 `CharacterStateMachine` 和同一套 `CharacterStateId`，但状态实现和 Motor 是 NPC 专用的。
>
> 我已经抓到主体结构了，再补两个小入口：`CharacterIntent` 的字段形状和镜头配置。然后我会直接给你一份项目架构梳理，偏工程文档风格，方便你后面继续扩。
</details>

我梳理了一遍，整体是一个“本地/服务端权威状态机 + 快照插值 + 离散动作事件”的仿只狼 3C Demo。没有改文件。

**核心架构**
`Character` 是主干，分得比较清楚：

- `Config`：ScriptableObject 数据层，移动、战斗、冲刺、表现、同步参数都从 `GameDataCatalog` 进来。
- `Intent`：统一输入意图 `CharacterIntent`。
- `StateMachine`：通用 FSM，负责状态白名单、打断窗口、状态切换。
- `Motor`：本地角色位移、锁定朝向、闪避位移、攻击 Root Motion。
- `Presentation`：Animator 层路由，负责移动/冲刺/攻击/闪避/防御/受击/死亡表现。
- `Sync`：状态快照、动作事件、插值、Mirror/Fake 传输。
- `LockOn`：目标锁定、锁定 UI、锁定目标 netId 解析。

玩家主流程是：

`InputHandler` -> `PlayerController` -> `CharacterStateMachine` -> `CharacterMotor` -> `CharacterLateUpdatePipeline` -> `Animator / Sync`

关键入口在 [PlayerController.cs](./Character/Controller/PlayerController.cs:14)。

**状态机设计**
状态枚举是：

`Idle / Move / Sprint / Attack / Dodge / Hit / Dead / Guard`

FSM 有两层约束：

1. `CharacterTransitionMap` 控制哪些状态能跳哪些状态。
2. `CharacterInterruptRule` 控制打断窗口，比如 Attack 的 ActiveWindow 轻击不打断、重击可打断，Dodge/Dead 有更高优先级。

这块是整个“类只狼手感”的核心，入口在 [CharacterStateMachine.cs](./Character/StateMachine/CharacterStateMachine.cs:6)。

动作内容已经比较完整：

- 普通四连击
- 冲刺攻击
- 闪避攻击
- 防御派生重击
- 闪避无敌帧
- 防御 Start/Loop/Exit
- 冲刺 Start/Loop/Brake/Turn180

**同步模型**
同步层不是帧同步/回滚，而是状态同步：

- `StateSnapshot`：连续状态，包含位置、Yaw、速度、StateId、SprintPhase、DodgeMode、GuardPhase、AttackComboStep、LockOn 信息。
- `ActionEvent`：离散动作事件，负责 AttackStart、DodgeStart、Hit、Dead、Revive，避免只靠快照漏掉动作起点。
- `RemoteSnapshotBuffer`：按到达时间缓存快照。
- `RemoteInterpolator`：用 `bufferDelaySec` 做延迟插值，远端平滑跟随。
- `RemoteActionApplier`：记录远端动作事件，尤其 DodgeMode 这种动作起点参数。
- `MirrorSyncTransport` / `FakeNetworkPipe`：一个真实 Mirror，一个本地模拟延迟/抖动/丢包。

核心文件是 [StateSnapshot.cs](./Character/Sync/StateSnapshot.cs:9)、[LocalSyncPublisher.cs](./Character/Sync/LocalSyncPublisher.cs:13)、[RemoteInterpolator.cs](./Character/Sync/RemoteInterpolator.cs:8)。

**权威划分**
玩家：

- 离线时默认可输入。
- 联机时只有 `NetworkIdentity.isLocalPlayer` 能处理输入。
- 由 `PlayerAuthorityGate` 统一判断。

NPC：

- 服务端权威。
- `NpcCharacterDriver` 只在 server 上 Tick FSM。
- 客户端通过 `NpcServerAiGate` 关掉本地 AI/NavMesh，避免和网络快照打架。
- `NpcAuthoritySyncPublisher` 从服务端广播 NPC 快照和动作。

这块整体方向是对的：玩家本地响应，NPC 服务端统一，远端只表现。

**表现层**
`CharacterLateUpdatePipeline` 是表现总入口，优先级大概是：

远端插值 -> Combat/Reaction/Dodge/Attack -> Guard -> Sprint -> Locomotion

Animator 分层也比较明确：

- Layer 0：Locomotion / Sprint
- Layer 1：UpperBody
- Layer 2：Combat
- Layer 3：Reaction

这让“移动中防御用上半身，站立防御用全身”这种表现可以做出来，设计挺实用。

**我看到的几个后续重点**
1. 现在同步是“表现同步”，还没有真正的伤害判定、攻击命中校验、防御判定、架势条/体干系统。仿只狼的核心战斗闭环还可以继续往这边补。
2. `RemoteActionApplier` 目前主要记录事件，实际 Animator 还是更依赖 Snapshot 状态。后面如果要加强动作起手一致性，可以让 ActionEvent 直接触发表现层的动作起点。
3. NPC 的状态实现比玩家简化很多，部分持续时间还写死，比如 Sprint Start/Brake。后面可以改成直接读 `SprintPhaseConfig`，和玩家一致。
4. 多处中文注释显示乱码，像是编码不一致。建议统一 UTF-8，不然之后维护会有点难受。
5. `PlayerController` 里有 `F` 键直接 `ApplyHit` 的测试逻辑，后期可以挪到 Debug 脚本，避免混在正式控制器里。

整体看，这是个结构已经搭起来的 3C 状态同步原型：状态机、动作窗口、锁定、镜头、动画层、Mirror 同步、NPC 服务端权威都已经有了。下一阶段最值得补的是“战斗结算层”：命中检测、防御/弹反/架势、受击来源、服务端校验，以及这些结果如何变成同步事件。

> 我打开了unity工程，为什么不能在vscode这里启动debug

<details><summary>3 previous messages</summary>

> 我先看一下你现在打开的 workspace 和项目根目录有没有 Unity 生成的 `.sln/.csproj`、`.vscode/launch.json`，这通常决定 VS Code 能不能挂到 Unity 进程调试。
>
> 我看到项目根目录其实已经有 Unity 生成的 `.sln/.csproj` 和 `.vscode/launch.json`，但你当前这个 `Scripts.code-workspace` 是把工作区根设在 `Assets/Scripts`，不是 Unity 项目根目录。这很可能就是 VS Code 里 Debug 不出现/启动不了的原因之一；我再看下根目录的 launch 配置是不是 Unity attach。
>
> 原因基本锁定了：你现在像是从 `Assets/Scripts/Scripts.code-workspace` 打开的 VS Code，这个 workspace 的根目录是 `Assets/Scripts`，所以 VS Code 看不到项目根目录下的 `.vscode/launch.json` 和 `SS-3CDemoPrivate.sln`。Unity 调试配置在项目根目录，不在 Scripts 子目录。
</details>

主要原因：你现在打开的是 `Assets/Scripts/Scripts.code-workspace`，它的根目录是 `Assets/Scripts`，不是 Unity 项目根目录。

所以 VS Code 在“这里”看不到这些关键文件：

- [SS-3CDemoPrivate.sln](D:/UnityProject/SS-3CDemoPrivate/SS-3CDemoPrivate.sln)
- [.vscode/launch.json](D:/UnityProject/SS-3CDemoPrivate/.vscode/launch.json)

而 Unity 调试配置就在根目录的 `.vscode/launch.json` 里：

```json
{
  "name": "Attach to Unity",
  "type": "vstuc",
  "request": "attach"
}
```

正确做法：

1. VS Code 里选择 `File > Open Folder...`
2. 打开这个目录：`D:\UnityProject\SS-3CDemoPrivate`
3. 安装 VS Code 扩展：`Visual Studio Tools for Unity`
4. Unity 里打开：
   `Edit > Preferences > External Tools`
5. 确认：
   `External Script Editor = Visual Studio Code`
   勾上 `Editor Attaching`
   点 `Regenerate project files`
6. 回 VS Code，`Run and Debug` 里选 `Attach to Unity`，按 F5
7. Unity 里按 Play，断点才会命中

注意：VS Code 不是“启动 Unity 调试”，而是“附加到已经打开的 Unity Editor”。所以 Unity 必须先开着。

你这个项目根目录配置其实已经生成好了，问题大概率就是 VS Code 打开的层级错了。打开项目根目录后，Debug 面板应该能看到 `Attach to Unity`。

> 帮我update项目，我新push的AIContext文件夹怎么拉取不了

> 帮我update项目，我新push的AIContext文件夹怎么拉取不了

我先查一下这个 Unity 项目的 Git 状态、当前分支和远端配置，再判断是没 fetch 到、分支不对、文件夹没被 Git 跟踪，还是本地改动挡住了 pull。不会先动你的本地改动。

> 帮我update项目，我新push的AIContext文件夹怎么拉取不了

> 读取D:\UnityProject\SS-3CDemoPrivate\AIContext的上下文，了解项目规划和历史开发日志

> 阿

> 啊

> 检查一下我现在Katana和NPC，Player的HitBox和HurtBox的检测。我攻击NPC，没有Trigger触发log.怎么回事

<details><summary>7 previous messages</summary>

> 我来顺着 `HitBox/HurtBox/Katana` 的脚本和 prefab/scene 引用一起查，重点看三件事：触发器脚本有没有挂上、Collider/Rigidbody/Layer 条件是否满足、攻击时 HitBox 有没有被启用。先读代码和资源引用，不会先改你的东西。
>
> 我看到了三个相关 prefab 都是本地已修改状态，而且还有一个未跟踪的 [HitTest.cs](./Character/HitTest.cs)。下一步我会把这个脚本、三个 prefab 的组件引用、以及项目 Physics/Layer 配置对上，找触发链断在哪一环。
>
> 项目里目前只有一个 `HitTest` 脚本在打印 `OnTriggerEnter`，还没有独立的 `HitBox/HurtBox` 类。现在我会沿着这个脚本的 GUID 去 prefab 里找它挂在哪些物体上，再看这些物体的 Layer、Collider、Rigidbody 配置。
>
> Prefab 上的基本形状是存在的：Katana 有 `Hitbox` 子物体，Layer 6，Trigger CapsuleCollider，并挂了 `HitTest`；NPC/Player 都有 `Hurtbox` 子物体，Layer 7，Trigger CapsuleCollider。现在最可疑的不是“脚本没挂”，而是 Physics Layer 碰撞矩阵或运行时位置/父子关系；我继续把矩阵和 Rigidbody 细节拆出来。
>
> 从 prefab 看，`Katana/Hitbox` 和 `NPC/Hurtbox` 都是 Trigger，且双方父级都有 kinematic Rigidbody，所以“没有 Rigidbody”不是主因。下一步我把 Unity 的 Layer Collision Matrix 解出来，因为现在 HitBox 是 Layer 6、HurtBox 是 Layer 7，只要矩阵里 6 和 7 不碰，`OnTriggerEnter` 就完全不会触发。
>
> 还有一个更大的嫌疑浮出来了：你有一个独立的 [Katana.prefab](D:/UnityProject/SS-3CDemoPrivate/Assets/Prefabs/Characters/Katana.prefab)，里面确实有 `Hitbox + HitTest`；但 Player prefab 里没搜到这个 Katana prefab 的引用，反而是 `ARPG_Samurai_Humanoid.prefab` 里有一个叫 `Katana` 的子物体。也就是说你挥的那把刀，可能不是带 `HitTest` 的那个 prefab。
>
> 矩阵这边确认了：Layer 6 `HitBox` 和 Layer 7 `HurtBox` 是互相碰撞的，所以不是 Layer Collision Matrix 把它们屏蔽了。现在剩下两个高概率点：运行时那把 Katana 是否真的是带 `HitTest` 的 prefab，以及 Hitbox 是否真的扫进了 NPC 的 Hurtbox。
</details>

`ARPG_Samurai_Humanoid.prefab` 里确实嵌了 `Katana.prefab`，所以你手上的刀大概率就是带 `Hitbox + HitTest` 的那把。现在我再查场景/NetworkManager 实际用的是不是这套 Player/NPC prefab，以及有没有 prefab instance override 把 Hitbox 或 Hurtbox 改掉。

> 保存记忆

> 读取D:\UnityProject\SS-3CDemoPrivate\AIContext的上下文，了解项目规划和历史开发日志

<details><summary>5 previous messages</summary>

> 我来把 `AIContext` 当成项目记忆库读一遍：先看目录结构和文件体量，再按阶段文档、开发日志、最新记录抓重点。不会改任何文件，只整理出当前规划、历史脉络和对后续开发最有用的结论。
>
> `AIContext` 里有 7 个 Markdown，体量接近 1MB，主线是从 Phase A 到 F 的对话记录加一个总纲。我已经定位到最重要的入口：`PhaseA-H_大纲.md` 是路线图，`cursor_phasef2.md` 末尾记录了 Phase F 正式结项和下一步 Phase G0。
>
> 我这边刚才用默认 `Get-Content` 读总纲时出现了中文编码乱码，所以我会重新按 UTF-8 读关键段落，避免把项目记忆读歪。重点抓最后结项状态、Phase G0 起点、以及历史上定下来的架构约定。
>
> 我已经按 UTF-8 重新读了核心文档，确认总纲本身稍旧：`PhaseA-H_大纲.md` 还写着 F 进行中，但 `cursor_phasef2.md` 末尾明确记录了“Phase F 正式结项”。我再补一眼各日志的结尾和 G0 关键词，然后给你一版可作为当前项目记忆的总结。
>
> Context compaction
>
> Source: automatic
> Status: completed
</details>

我已读取 `D:\UnityProject\SS-3CDemoPrivate\AIContext` 下的项目上下文和历史日志。当前我对项目的理解是：

**项目定位**
这是一个“带状态同步的第三人称角色对战 / 生存 3C Demo”，核心不是单纯做动作，而是验证：

- FSM + Motor + Presenter 的 3C 架构拆分
- 玩家 / NPC 共用状态协议
- 网络同步不只同步 Transform，而是同步状态、动作事件、表现参数
- NPC 由服务器权威驱动，客户端只做表现

**阶段状态**
按最新日志判断，真实进度是：

- Phase A：完成，拆分 `PlayerController` 为 Context / Intent / Motor / Orchestrator
- Phase B：完成，Idle / Move / Sprint FSM
- Phase C：完成，Attack / Dodge / Hit / Dead，加中断规则和窗口
- Phase D：完成，FakeNetwork + Mirror 同步，拆成 Snapshot 和 ActionEvent
- Phase E：完成，NPC 服务器权威同步完成，E0 行为树巡逻追击攻击被延期
- Phase F：完成，武士模型、Animator 多层表现、NPC / Player 动作网络表现闭环已完成
- 下一阶段：Phase G0，正式进入战斗判定闭环

有一个小坑：[`PhaseA-H_大纲.md`](D:/UnityProject/SS-3CDemoPrivate/AIContext/PhaseA-H_%E5%A4%A7%E7%BA%B2.md) 里还写着 Phase F 进行中，但从 [`cursor_phasef2.md`](D:/UnityProject/SS-3CDemoPrivate/AIContext/cursor_phasef2.md) 看，Phase F 已经正式结项。所以之后我会以 `cursor_phasef2.md` 作为更新后的准确信息。

**当前架构关键点**
项目现在主要围绕这些模块工作：

- `Character/StateMachine`：角色状态机与中断规则
- `Character/Motor`：移动、朝向、闪避、攻击 Root Motion 等运动层
- `Character/Presentation`：Animator 表现层，含 Locomotion / Sprint / Combat
- `Character/Sync`：Snapshot + ActionEvent 同步协议
- `AI/NPC`：服务器权威 NPC 驱动，客户端消费同步结果
- `Core`：GameData、权限门、相机绑定等基础设施

**下一步方向**
按规划，现在最自然的下一步就是 **Phase G0：战斗判定闭环**：

- G0-1：HitBox / HurtBox / 攻击有效帧
- G0-2：服务器权威伤害判定
- G0-3：Guard 防御方向、减伤、破防或弹反
- G0-4：Hit / Damage / HP 的网络同步表现

这也正好对应你刚才提到的 Katana、NPC、Player 的 HitBox / HurtBox 没有触发 Trigger log 的问题。项目记忆我已经接上了，后面可以直接从 G0 的战斗检测问题继续查。

> 考虑攻击有效帧应该以什么形式配置，是否应先设计一个攻击系统或者技能系统。目前只有Collider，考虑HitBox/HurtBox是否也该收进系统中？目前不太明朗

<details><summary>2 previous messages</summary>

> 我先快速对照一下现在的 Attack 状态、配置和同步代码，这样建议不会飘在空中。重点看“动作阶段在哪里被定义”和“事件/表现/判定现在各自归谁”。
>
> 从搜索结果看，你现在的攻击“身份”主要是 `AttackState.CurrentComboStep`，而时长和连段窗口在 `CharacterCombatConfig` 里；Collider 只是场景对象，还没被纳入战斗语义。也就是说现在正处在“需要一个薄战斗系统，但还不到完整技能系统”的分界点。
</details>

我建议：**先不要做完整技能系统，先做一个很薄的 Combat Action / Attack Definition 系统**。

你现在的项目阶段刚好是 G0 战斗判定闭环。目标应该是把“攻击是谁、什么时候有效、打到谁、怎么结算”先跑通，而不是一下子上技能系统、CD、资源、施法、Buff、派生树那些大结构。

我的建议分层是：

1. `AttackState`
   继续负责状态流转：进入攻击、连段、重击、闪避攻击、结束攻击。

2. `AttackDefinition`
   新增数据层，描述某一个攻击动作：
   - attackId / step
   - duration
   - active windows，有效帧区间
   - damage
   - hitStun / isHeavy
   - guardDamage / guardBreak
   - hitbox slot，比如 KatanaBlade
   - 是否一刀只能命中同一目标一次

3. `HitBox / HurtBox`
   应该收进战斗系统，但它们不应该自己决定伤害。
   - `HitBox`：只表示“这是攻击判定区域”
   - `HurtBox`：只表示“这是可受击区域”
   - `CombatResolver` 或 `DamageService`：负责过滤阵营、服务器权威、重复命中、防御角度、无敌帧、ApplyHit

4. `Collider`
   只当 Unity 物理检测工具，不直接承载战斗规则。

攻击有效帧我建议先用 **ScriptableObject 配置 normalized time 区间**，例如：

```csharp
Attack_Combo1
duration = 0.8167
activeWindow = 0.28 - 0.42
damage = 10
hitboxSlot = KatanaBlade
```

运行时换算成秒：

```csharp
activeStartTime = duration * 0.28f;
activeEndTime = duration * 0.42f;
```

这样比直接写秒更好调，因为动作时长以后改了，有效帧还能跟着比例走。也比纯 Animation Event 更适合你这个项目，因为你后面要做服务器权威判定，不能把权威逻辑绑死在客户端 Animator 事件上。

Animation Event 可以用，但我会把它定位成辅助：比如开特效、刀光、音效。真正的命中判定窗口还是由服务器逻辑根据 `AttackDefinition + AttackState timer` 判断。

所以结论是：

**现在不要做完整技能系统。**
先做一个“小型攻击系统”：

`AttackState` 选择当前攻击 → `AttackDefinition` 提供有效帧和伤害数据 → `HitBox/HurtBox` 提供空间检测 → `CombatResolver` 做服务器权威结算。

这条路最贴合你当前架构，也方便后面自然升级成技能系统。技能系统以后可以把 `AttackDefinition` 扩展成 `CombatActionDefinition` / `SkillDefinition`，但现在先别把工程拖重。

> 那我应该具体怎么设计呢

<details><summary>1 previous message</summary>

> 我会把它设计成“攻击动作数据 + 判定运行时 + 受击接口”三件东西，不急着叫 Skill。这样既能接上你现在的 `AttackState.CurrentComboStep`，也不会把后面的格挡、伤害、网络同步堵死。
</details>

我会按这个形状设计：**先做 CombatAction，不做完整 SkillSystem**。名字可以暂时叫 `AttackDefinition`，本质是“一个攻击动作的数据”。以后它可以自然升级成技能。

**目录建议**
放在：

`Assets/Scripts/Character/Combat/`

先建这些类：

```text
Character/Combat
  AttackDefinition.cs
  AttackHitWindow.cs
  CombatHitBox.cs
  CombatHurtBox.cs
  CombatActor.cs
  CombatResolver.cs
  IDamageable.cs
```

**核心职责**
`AttackState` 还是只管状态：
现在是第几段攻击、攻击计时、连段、什么时候退出。

`AttackDefinition` 管攻击数据：
这一刀多长、什么时候有效、伤害多少、用哪个 HitBox。

`CombatHitBox / CombatHurtBox` 管空间：
HitBox 表示攻击范围，HurtBox 表示可受击范围。

`CombatResolver` 管规则：
是否敌人、是否服务器、是否已经命中过、是否格挡、是否造成伤害。

不要让 Collider 自己决定伤害。Collider 只负责“碰到了谁”。

---

**AttackDefinition 设计**

我建议先不要一个攻击一个 ScriptableObject，先作为 `CharacterCombatConfig` 里的可序列化数组，接你现在已有的配置最顺。

大概这样：

```csharp
[System.Serializable]
public sealed class AttackDefinition
{
    public byte attackStep;
    public string displayName;

    public float duration;
    public float damage = 10f;
    public bool isHeavyHit;

    public bool hitSameTargetOnce = true;
    public AttackHitWindow[] hitWindows;
}
```

```csharp
[System.Serializable]
public struct AttackHitWindow
{
    [Range(0f, 1f)]
    public float startNormalized;

    [Range(0f, 1f)]
    public float endNormalized;

    public HitBoxSlot slot;
    public float damageMultiplier;
}
```

`HitBoxSlot` 可以先简单一点：

```csharp
public enum HitBoxSlot : byte
{
    None = 0,
    KatanaBlade = 1,
    Body = 2,
    Kick = 3
}
```

比如 `Attack_Combo1`：

```text
attackStep = 1
duration = 0.8167
damage = 10
hitWindow = 0.28 - 0.42
slot = KatanaBlade
```

有效帧我建议用 **normalized time**，不是绝对秒。这样动画长度微调后，有效帧比例还能保住。

---

**AttackState 要暴露的信息**

你现在的 [AttackState.cs](./Character/StateMachine/States/AttackState.cs:7) 已经有 `CurrentComboStep`，还需要再暴露几个只读信息：

```csharp
public byte CurrentAttackStep => _currentAttackStep;
public float ElapsedTime => _timer;
public float CurrentDuration => _combat.GetAttackDuration(_currentAttackStep);
public int AttackInstanceId { get; private set; }
```

每次 `BeginAttack()` 的时候：

```csharp
AttackInstanceId++;
```

这个 `AttackInstanceId` 很重要，用来防止同一刀在有效帧内反复打同一个目标。

---

**CombatHitBox 设计**

挂在 Katana 的 Hitbox 子物体上。

它不要自己扣血，只上报：

```csharp
public sealed class CombatHitBox : MonoBehaviour
{
    public HitBoxSlot slot;
    public CombatActor owner;
    public Collider triggerCollider;
}
```

但是我建议权威判定不要完全依赖 `OnTriggerEnter`。

原因是你现在已经遇到了“不触发 log”的问题。Trigger 事件有几个常见坑：

- 一开始就重叠，不一定有 Enter
- 有效帧很短，物理帧错过
- Collider 一直开着，攻击开始时没有新的进入事件
- 动画移动和物理更新不同步

所以更稳的做法是：

**HitBox 在有效帧内主动做 Overlap 查询。**

也就是 `CombatHitBox` 提供一个方法：

```csharp
public int OverlapHurtBoxes(Collider[] results, LayerMask hurtBoxMask)
```

内部根据自己的 Collider 形状调用：

- `Physics.OverlapCapsuleNonAlloc`
- 或先简单用 `Physics.OverlapBoxNonAlloc`
- 初期甚至可以用 `triggerCollider.bounds` 做保守检测

Trigger 可以保留做 debug，但不要作为最终战斗判定核心。

---

**CombatHurtBox 设计**

挂在 Player / NPC 的 Hurtbox 子物体上。

```csharp
public sealed class CombatHurtBox : MonoBehaviour
{
    public CombatActor owner;
    public float damageMultiplier = 1f;
}
```

以后可以扩展：

```text
Head
Body
Back
Armor
WeakPoint
```

但现在先别做复杂。

---

**CombatActor 设计**

挂在 Player / NPC 根节点上。

它是战斗系统认识角色的入口：

```csharp
public sealed class CombatActor : MonoBehaviour, IDamageable
{
    public int ActorId { get; }
    public int TeamId;
    public bool IsDead { get; }

    public bool TryGetCurrentAttack(out AttackRuntimeInfo attack);
    public void ApplyHit(in HitInfo hit);
}
```

Player 的 `ApplyHit` 可以转调你现在的 `PlayerController.ApplyHit()`。
NPC 以后也走自己的 `NpcCharacterDriver.ApplyHit()` 或类似接口。

---

**CombatResolver 设计**

这是最关键的规则层。

```csharp
public sealed class CombatResolver : MonoBehaviour
{
    public void ResolveHit(
        CombatHitBox hitBox,
        CombatHurtBox hurtBox,
        in AttackRuntimeInfo attack)
    {
        // 1. server only
        // 2. owner != target
        // 3. team check
        // 4. attack window active
        // 5. same target once
        // 6. guard check
        // 7. ApplyHit
        // 8. send ActionEvent.Hit
    }
}
```

命中记录可以用：

```text
attackerActorId + attackInstanceId + targetActorId + windowIndex
```

这样同一刀不会每帧重复扣血。

---

**整体流程**

最终运行时应该是这样：

```text
玩家按攻击
    ↓
AttackState 进入 Attack，确定 attackStep
    ↓
AttackDefinition 找到这一刀的数据
    ↓
CombatHitBoxDriver 根据 AttackState timer 判断有效帧
    ↓
有效帧内扫描 KatanaBlade HitBox
    ↓
发现 NPC HurtBox
    ↓
CombatResolver 做服务器权威判定
    ↓
target.ApplyHit()
    ↓
目标进入 Hit / Dead 状态
    ↓
SyncPublisher 广播 Snapshot / ActionEvent.Hit
    ↓
客户端只表现，不决定伤害
```

---

**我建议你的实现顺序**

第一步，只做 G0-1：

```text
AttackDefinition
CombatHitBox
CombatHurtBox
CombatActor
CombatResolver
有效帧内输出 ValidHit log
```

先不扣血，只打印：

```text
[Combat] Player Attack_Combo1 hit NPC during window 0
```

第二步，再接伤害：

```text
IDamageable
ApplyHit
Hit / Dead FSM
server only
```

第三步，接格挡：

```text
Guard angle
Guard damage
Heavy hit guard break
```

第四步，接网络表现：

```text
ActionEvent.Hit
remote hit reaction
HP sync
```

一句话总结：
**现在要做的是“攻击判定系统”，不是“技能系统”。技能系统以后可以建立在它上面。HitBox / HurtBox 必须收进这个系统，但只能负责空间检测，不能负责战斗规则。**

> 我添加了Combat文件夹和底下的文件，现在把代码给我

<details><summary>4 previous messages</summary>

> 我来接上你已经建好的 `Combat` 文件夹，先看一下文件名、命名空间和现有类壳子，再按你当前架构把第一版 G0-1 代码填进去。重点先做到“有效帧内能稳定检测 HurtBox，并输出命中日志”。
>
> 你这些文件已经建好了，我会直接填代码而不是只贴片段。第一版会保持低耦合：先不改网络同步、不扣血也可以运行；如果挂上 `CombatActor` 和 Hit/HurtBox，就能在攻击有效帧内扫到目标并打印日志。
>
> 为了让这些类不是“孤岛”，我还需要轻量改两个已有点：`CharacterCombatConfig` 提供按 attackStep 查定义，`AttackState/NpcAttackState` 暴露当前攻击计时和实例号。这样 Combat 系统能知道“当前是哪一刀、这一刀打到第几秒”。
>
> 接下来我会改现有代码：一是把你新建的 Combat 脚本补全，二是给 `AttackState` / `NpcAttackState` 增加只读运行时信息，三是在 `CharacterCombatConfig` 里挂攻击定义数组。这样 Unity 里配置完 HitBox/HurtBox 后，代码就能直接跑第一版检测。
</details>

我要动手补代码了。第一版我会让 `CombatResolver` 默认只打日志、不实际扣血，避免你一挂上就把已有调试流程弄乱；等 G0-2 再把 `_applyDamage` 打开或接服务器伤害。

> 不对，不是让你直接写代码，撤回你的改动，我是让你把代码贴出来

<details><summary>1 previous message</summary>

> 收到，是我误会了。刚才我只做了读取和搜索，还没有执行任何写文件/补丁操作，所以没有我的改动需要撤回；下面我只把代码贴出来，不碰你的工程文件。
</details>

对，我刚才理解偏了。没有改你的文件。下面是**只贴代码版**，第一版目标是：`AttackState.CurrentComboStep` + `CombatActor` 自己计时 + 有效帧内主动扫 `HurtBox` + 打日志。默认不扣血。

**AttackHitWindow.cs**
```csharp
using System;
using UnityEngine;

namespace Character.Combat
{
    [Serializable]
    public struct AttackHitWindow
    {
        [Range(0f, 1f)] public float startNormalized;
        [Range(0f, 1f)] public float endNormalized;
        public HitBoxSlot slot;
        public float damageMultiplier;

        public bool IsValid => slot != HitBoxSlot.None && endNormalized > startNormalized;
        public float DamageMultiplierOrDefault => damageMultiplier <= 0f ? 1f : damageMultiplier;

        public bool Contains(float normalizedTime)
        {
            return IsValid && normalizedTime >= startNormalized && normalizedTime <= endNormalized;
        }

        public static AttackHitWindow Create(HitBoxSlot slot, float start, float end, float multiplier = 1f)
        {
            return new AttackHitWindow
            {
                slot = slot,
                startNormalized = start,
                endNormalized = end,
                damageMultiplier = multiplier
            };
        }
    }

    public enum HitBoxSlot : byte
    {
        None = 0,
        Katana = 1,
        Body = 2,
        Kick = 3
    }
}
```

**AttackDefinition.cs**
```csharp
using System;
using UnityEngine;

namespace Character.Combat
{
    [Serializable]
    public sealed class AttackDefinition
    {
        public byte attackStep = 1;
        public string displayName;

        [Min(0.01f)] public float duration = 0.8f;
        [Min(0f)] public float damage = 10f;
        public bool isHeavyHit;

        public bool hitSameTargetOnce = true;
        public AttackHitWindow[] hitWindows =
        {
            AttackHitWindow.Create(HitBoxSlot.Katana, 0.25f, 0.45f)
        };

        public string DebugName => string.IsNullOrWhiteSpace(displayName)
            ? $"Attack_{attackStep}"
            : displayName;

        public float NormalizedTime(float elapsedTime)
        {
            return duration > 0.0001f ? Mathf.Clamp01(elapsedTime / duration) : 0f;
        }

        public bool IsWindowActive(int windowIndex, float elapsedTime)
        {
            if (hitWindows == null || windowIndex < 0 || windowIndex >= hitWindows.Length)
                return false;

            return hitWindows[windowIndex].Contains(NormalizedTime(elapsedTime));
        }

        public static AttackDefinition CreateFallback(byte step, float duration)
        {
            return new AttackDefinition
            {
                attackStep = step,
                displayName = $"Fallback_Attack_{step}",
                duration = Mathf.Max(0.01f, duration),
                damage = 10f,
                isHeavyHit = step >= 7,
                hitSameTargetOnce = true,
                hitWindows = new[]
                {
                    AttackHitWindow.Create(HitBoxSlot.Katana, 0.25f, 0.45f)
                }
            };
        }
    }

    public struct AttackRuntimeInfo
    {
        public CombatActor owner;
        public AttackDefinition definition;
        public byte attackStep;
        public int attackInstanceId;
        public float elapsedTime;

        public float NormalizedTime => definition != null ? definition.NormalizedTime(elapsedTime) : 0f;
        public bool IsValid => owner != null && definition != null && attackStep > 0;

        public AttackRuntimeInfo(
            CombatActor owner,
            AttackDefinition definition,
            byte attackStep,
            int attackInstanceId,
            float elapsedTime)
        {
            this.owner = owner;
            this.definition = definition;
            this.attackStep = attackStep;
            this.attackInstanceId = attackInstanceId;
            this.elapsedTime = elapsedTime;
        }
    }
}
```

**IDamageable.cs**
```csharp
using UnityEngine;

namespace Character.Combat
{
    public interface IDamageable
    {
        bool CanReceiveHit { get; }
        bool ApplyHit(in HitInfo hit);
    }

    public struct HitInfo
    {
        public CombatActor attacker;
        public CombatActor target;
        public AttackDefinition attack;
        public byte attackStep;
        public int attackInstanceId;
        public int windowIndex;
        public HitBoxSlot slot;
        public float damage;
        public bool isHeavyHit;
        public Vector3 hitPoint;
        public Vector3 hitDirection;
    }
}
```

**CombatHurtBox.cs**
```csharp
using UnityEngine;

namespace Character.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class CombatHurtBox : MonoBehaviour
    {
        [SerializeField] private CombatActor _owner;
        [SerializeField] private Collider _hurtCollider;
        [SerializeField] private string _partName = "Body";
        [SerializeField] private float _damageMultiplier = 1f;

        public CombatActor Owner => _owner;
        public string PartName => _partName;
        public float DamageMultiplier => _damageMultiplier <= 0f ? 1f : _damageMultiplier;

        private void Reset()
        {
            EnsureReferences();
        }

        private void Awake()
        {
            EnsureReferences();
        }

        public bool TryGetOwner(out CombatActor owner)
        {
            EnsureReferences();
            owner = _owner;
            return owner != null;
        }

        private void EnsureReferences()
        {
            if (_owner == null)
                _owner = GetComponentInParent<CombatActor>();

            if (_hurtCollider == null)
                _hurtCollider = GetComponent<Collider>();

            if (_hurtCollider != null)
                _hurtCollider.isTrigger = true;
        }
    }
}
```

**CombatHitBox.cs**
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Character.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class CombatHitBox : MonoBehaviour
    {
        private static readonly List<CombatHitBox> Active = new();

        [SerializeField] private CombatActor _owner;
        [SerializeField] private Collider _shapeCollider;
        [SerializeField] private HitBoxSlot _slot = HitBoxSlot.Katana;
        [SerializeField] private LayerMask _hurtBoxMask;
        [SerializeField] private QueryTriggerInteraction _queryTriggerInteraction = QueryTriggerInteraction.Collide;

        public static IReadOnlyList<CombatHitBox> ActiveHitBoxes => Active;
        public CombatActor Owner => _owner;
        public HitBoxSlot Slot => _slot;

        private void Reset()
        {
            EnsureReferences();
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            if (!Active.Contains(this))
                Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        public bool IsConfigured()
        {
            EnsureReferences();
            return _owner != null && _shapeCollider != null && _slot != HitBoxSlot.None;
        }

        public int OverlapHurtBoxesNonAlloc(Collider[] results)
        {
            if (!IsConfigured() || results == null || results.Length == 0)
                return 0;

            return _shapeCollider switch
            {
                CapsuleCollider capsule => OverlapCapsule(capsule, results),
                BoxCollider box => OverlapBox(box, results),
                SphereCollider sphere => OverlapSphere(sphere, results),
                _ => OverlapBounds(_shapeCollider, results)
            };
        }

        private void EnsureReferences()
        {
            if (_owner == null)
                _owner = GetComponentInParent<CombatActor>();

            if (_shapeCollider == null)
                _shapeCollider = GetComponent<Collider>();

            if (_shapeCollider != null)
                _shapeCollider.isTrigger = true;

            if (_hurtBoxMask.value == 0)
            {
                int mask = LayerMask.GetMask("HurtBox");
                _hurtBoxMask = mask != 0 ? mask : ~0;
            }
        }

        private int OverlapCapsule(CapsuleCollider capsule, Collider[] results)
        {
            Transform tr = capsule.transform;
            Vector3 center = tr.TransformPoint(capsule.center);
            Vector3 scale = Abs(tr.lossyScale);

            Vector3 axis;
            float radiusScale;
            float heightScale;

            switch (capsule.direction)
            {
                case 0:
                    axis = tr.right;
                    radiusScale = Mathf.Max(scale.y, scale.z);
                    heightScale = scale.x;
                    break;
                case 1:
                    axis = tr.up;
                    radiusScale = Mathf.Max(scale.x, scale.z);
                    heightScale = scale.y;
                    break;
                default:
                    axis = tr.forward;
                    radiusScale = Mathf.Max(scale.x, scale.y);
                    heightScale = scale.z;
                    break;
            }

            float radius = Mathf.Max(0.001f, capsule.radius * radiusScale);
            float height = Mathf.Max(radius * 2f, capsule.height * heightScale);
            float halfSegment = Mathf.Max(0f, height * 0.5f - radius);

            Vector3 p0 = center + axis * halfSegment;
            Vector3 p1 = center - axis * halfSegment;

            return Physics.OverlapCapsuleNonAlloc(
                p0,
                p1,
                radius,
                results,
                _hurtBoxMask,
                _queryTriggerInteraction);
        }

        private int OverlapBox(BoxCollider box, Collider[] results)
        {
            Transform tr = box.transform;
            Vector3 center = tr.TransformPoint(box.center);
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, Abs(tr.lossyScale));

            return Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                results,
                tr.rotation,
                _hurtBoxMask,
                _queryTriggerInteraction);
        }

        private int OverlapSphere(SphereCollider sphere, Collider[] results)
        {
            Transform tr = sphere.transform;
            Vector3 center = tr.TransformPoint(sphere.center);
            Vector3 scale = Abs(tr.lossyScale);
            float radius = sphere.radius * Mathf.Max(scale.x, scale.y, scale.z);

            return Physics.OverlapSphereNonAlloc(
                center,
                radius,
                results,
                _hurtBoxMask,
                _queryTriggerInteraction);
        }

        private int OverlapBounds(Collider col, Collider[] results)
        {
            Bounds bounds = col.bounds;
            return Physics.OverlapBoxNonAlloc(
                bounds.center,
                bounds.extents,
                results,
                Quaternion.identity,
                _hurtBoxMask,
                _queryTriggerInteraction);
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }
    }
}
```

**CombatActor.cs**
```csharp
using System.Collections.Generic;
using AI;
using Character.Config;
using Character.Controller;
using Character.StateMachine;
using Core;
using Mirror;
using UnityEngine;

namespace Character.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatActor : MonoBehaviour, IDamageable
    {
        [SerializeField] private int _teamId = 1;
        [SerializeField] private CharacterCombatConfig _combatConfig;
        [SerializeField] private AttackDefinition[] _attackDefinitions;

        [SerializeField] private PlayerController _playerController;
        [SerializeField] private NpcCharacterDriver _npcDriver;
        [SerializeField] private NetworkIdentity _networkIdentity;

        [SerializeField] private bool _canReceiveHit = true;

        private readonly Dictionary<byte, AttackDefinition> _fallbackDefinitions = new();

        private bool _trackingAttack;
        private byte _trackedAttackStep;
        private float _trackedAttackElapsed;
        private int _trackedAttackInstanceId;
        private int _nextAttackInstanceId = 1;
        private AttackDefinition _trackedAttackDefinition;

        public int TeamId => _teamId;

        public int ActorId
        {
            get
            {
                if (_networkIdentity != null && _networkIdentity.netId != 0)
                    return unchecked((int)_networkIdentity.netId);

                return GetInstanceID();
            }
        }

        public bool CanReceiveHit => _canReceiveHit && !IsDead;

        public bool IsDead
        {
            get
            {
                if (_playerController != null)
                    return _playerController.CurrentStateId == CharacterStateId.Dead;

                if (_npcDriver != null)
                    return _npcDriver.CurrentStateId == CharacterStateId.Dead;

                return false;
            }
        }

        private void Reset()
        {
            EnsureReferences();
            if (_npcDriver != null) _teamId = 2;
            else _teamId = 1;
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void Update()
        {
            TickAttackRuntime(Time.deltaTime);
        }

        public bool TryGetCurrentAttack(out AttackRuntimeInfo attack)
        {
            attack = default;

            if (!_trackingAttack || _trackedAttackDefinition == null)
                return false;

            attack = new AttackRuntimeInfo(
                this,
                _trackedAttackDefinition,
                _trackedAttackStep,
                _trackedAttackInstanceId,
                _trackedAttackElapsed);

            return attack.IsValid;
        }

        public bool ApplyHit(in HitInfo hit)
        {
            if (!CanReceiveHit)
                return false;

            if (_playerController != null)
            {
                _playerController.ApplyHit(hit.damage, hit.isHeavyHit);
                return true;
            }

            if (_npcDriver != null)
            {
                return _npcDriver.ServerTryEnterHit(hit.isHeavyHit);
            }

            return false;
        }

        private void TickAttackRuntime(float deltaTime)
        {
            if (!TryReadActiveAttackStep(out byte step))
            {
                StopTrackingAttack();
                return;
            }

            if (!_trackingAttack || _trackedAttackStep != step)
            {
                BeginTrackingAttack(step);
                return;
            }

            _trackedAttackElapsed += deltaTime;
        }

        private void BeginTrackingAttack(byte step)
        {
            _trackingAttack = true;
            _trackedAttackStep = step;
            _trackedAttackElapsed = 0f;
            _trackedAttackInstanceId = _nextAttackInstanceId++;
            _trackedAttackDefinition = ResolveAttackDefinition(step);

            if (_nextAttackInstanceId == int.MaxValue)
                _nextAttackInstanceId = 1;
        }

        private void StopTrackingAttack()
        {
            _trackingAttack = false;
            _trackedAttackStep = 0;
            _trackedAttackElapsed = 0f;
            _trackedAttackDefinition = null;
        }

        private bool TryReadActiveAttackStep(out byte step)
        {
            if (_playerController != null
                && _playerController.CurrentStateId == CharacterStateId.Attack
                && _playerController.TryGetActiveAttackState(out var playerAttack))
            {
                step = playerAttack.CurrentComboStep;
                return true;
            }

            if (_npcDriver != null
                && _npcDriver.CurrentStateId == CharacterStateId.Attack
                && _npcDriver.TryGetActiveAttackState(out var npcAttack))
            {
                step = npcAttack.CurrentComboStep;
                return true;
            }

            step = 0;
            return false;
        }

        private AttackDefinition ResolveAttackDefinition(byte step)
        {
            if (_attackDefinitions != null)
            {
                foreach (var definition in _attackDefinitions)
                {
                    if (definition != null && definition.attackStep == step)
                        return definition;
                }
            }

            if (!_fallbackDefinitions.TryGetValue(step, out var fallback))
            {
                fallback = AttackDefinition.CreateFallback(step, ResolveLegacyDuration(step));
                _fallbackDefinitions.Add(step, fallback);
            }

            fallback.duration = ResolveLegacyDuration(step);
            return fallback;
        }

        private float ResolveLegacyDuration(byte step)
        {
            CharacterCombatConfig config = ResolveCombatConfig();
            return config != null ? config.GetAttackDuration(step) : 0.8f;
        }

        private CharacterCombatConfig ResolveCombatConfig()
        {
            if (_combatConfig != null)
                return _combatConfig;

            if (GameDataManager.Instance == null)
                return null;

            if (_npcDriver != null)
                return GameDataManager.Instance.Npc != null ? GameDataManager.Instance.Npc.combat : null;

            return GameDataManager.Instance.Player != null ? GameDataManager.Instance.Player.combat : null;
        }

        private void EnsureReferences()
        {
            if (_playerController == null)
                _playerController = GetComponent<PlayerController>();

            if (_npcDriver == null)
                _npcDriver = GetComponent<NpcCharacterDriver>();

            if (_networkIdentity == null)
                _networkIdentity = GetComponent<NetworkIdentity>();
        }
    }
}
```

**CombatResolver.cs**
```csharp
using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Character.Combat
{
    [DefaultExecutionOrder(100)]
    public sealed class CombatResolver : MonoBehaviour
    {
        [SerializeField] private bool _serverAuthoritative = true;
        [SerializeField] private bool _applyDamage = false;
        [SerializeField] private bool _logValidHits = true;
        [SerializeField] private int _maxOverlapResults = 32;

        private Collider[] _overlapResults;
        private readonly HashSet<HitKey> _resolvedHits = new();

        private void Awake()
        {
            _overlapResults = new Collider[Mathf.Max(4, _maxOverlapResults)];
        }

        private void Update()
        {
            if (!CanResolve())
                return;

            ResolveAllActiveHitBoxes();
        }

        private void OnDisable()
        {
            _resolvedHits.Clear();
        }

        private bool CanResolve()
        {
            if (!_serverAuthoritative)
                return true;

            if (!NetworkClient.active && !NetworkServer.active)
                return true;

            return NetworkServer.active;
        }

        private void ResolveAllActiveHitBoxes()
        {
            var hitBoxes = CombatHitBox.ActiveHitBoxes;

            for (int i = 0; i < hitBoxes.Count; i++)
            {
                CombatHitBox hitBox = hitBoxes[i];
                if (hitBox == null || !hitBox.IsConfigured())
                    continue;

                CombatActor attacker = hitBox.Owner;
                if (attacker == null || !attacker.TryGetCurrentAttack(out var attack))
                    continue;

                AttackDefinition definition = attack.definition;
                if (definition.hitWindows == null)
                    continue;

                for (int windowIndex = 0; windowIndex < definition.hitWindows.Length; windowIndex++)
                {
                    AttackHitWindow window = definition.hitWindows[windowIndex];

                    if (!window.IsValid)
                        continue;

                    if (window.slot != hitBox.Slot)
                        continue;

                    if (!definition.IsWindowActive(windowIndex, attack.elapsedTime))
                        continue;

                    ResolveWindow(hitBox, attack, window, windowIndex);
                }
            }
        }

        private void ResolveWindow(
            CombatHitBox hitBox,
            AttackRuntimeInfo attack,
            AttackHitWindow window,
            int windowIndex)
        {
            int count = hitBox.OverlapHurtBoxesNonAlloc(_overlapResults);

            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapResults[i];
                if (col == null)
                    continue;

                CombatHurtBox hurtBox = col.GetComponent<CombatHurtBox>();
                if (hurtBox == null)
                    hurtBox = col.GetComponentInParent<CombatHurtBox>();

                if (hurtBox == null || !hurtBox.TryGetOwner(out CombatActor target))
                    continue;

                TryResolveHit(hitBox, hurtBox, target, attack, window, windowIndex);
            }
        }

        private void TryResolveHit(
            CombatHitBox hitBox,
            CombatHurtBox hurtBox,
            CombatActor target,
            AttackRuntimeInfo attack,
            AttackHitWindow window,
            int windowIndex)
        {
            CombatActor attacker = attack.owner;

            if (attacker == null || target == null)
                return;

            if (attacker == target)
                return;

            if (attacker.TeamId != 0 && attacker.TeamId == target.TeamId)
                return;

            if (!target.CanReceiveHit)
                return;

            int hitWindowKey = attack.definition.hitSameTargetOnce ? -1 : windowIndex;
            var key = new HitKey(attacker.ActorId, attack.attackInstanceId, target.ActorId, hitWindowKey);

            if (_resolvedHits.Contains(key))
                return;

            _resolvedHits.Add(key);

            Vector3 direction = target.transform.position - attacker.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                direction.Normalize();
            else
                direction = attacker.transform.forward;

            float damage = attack.definition.damage
                           * window.DamageMultiplierOrDefault
                           * hurtBox.DamageMultiplier;

            var hit = new HitInfo
            {
                attacker = attacker,
                target = target,
                attack = attack.definition,
                attackStep = attack.attackStep,
                attackInstanceId = attack.attackInstanceId,
                windowIndex = windowIndex,
                slot = window.slot,
                damage = damage,
                isHeavyHit = attack.definition.isHeavyHit,
                hitPoint = hurtBox.transform.position,
                hitDirection = direction
            };

            if (_logValidHits)
            {
                Debug.Log(
                    $"[Combat] {attacker.name} {attack.definition.DebugName} hit {target.name} " +
                    $"slot={window.slot}, window={windowIndex}, t={attack.NormalizedTime:F2}, damage={damage:F1}",
                    this);
            }

            if (_applyDamage)
                target.ApplyHit(hit);
        }

        private readonly struct HitKey : IEquatable<HitKey>
        {
            private readonly int _attackerId;
            private readonly int _attackInstanceId;
            private readonly int _targetId;
            private readonly int _windowKey;

            public HitKey(int attackerId, int attackInstanceId, int targetId, int windowKey)
            {
                _attackerId = attackerId;
                _attackInstanceId = attackInstanceId;
                _targetId = targetId;
                _windowKey = windowKey;
            }

            public bool Equals(HitKey other)
            {
                return _attackerId == other._attackerId
                       && _attackInstanceId == other._attackInstanceId
                       && _targetId == other._targetId
                       && _windowKey == other._windowKey;
            }

            public override bool Equals(object obj)
            {
                return obj is HitKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + _attackerId;
                    hash = hash * 31 + _attackInstanceId;
                    hash = hash * 31 + _targetId;
                    hash = hash * 31 + _windowKey;
                    return hash;
                }
            }
        }
    }
}
```

这版不需要你先改 `AttackState`。`CombatActor` 会观察当前是否处于 `Attack`，然后用 `CurrentComboStep` 自己记录这一刀的 elapsed time 和 attack instance。

挂法最小闭环是：

- 场景里放一个 `CombatResolver`
- Player / NPC 根节点挂 `CombatActor`
- Katana 的 Hitbox 子物体挂 `CombatHitBox`
- Player / NPC 的 Hurtbox 子物体挂 `CombatHurtBox`
- Player team = 1，NPC team = 2
- `CombatResolver._applyDamage` 先保持 false，只看 `[Combat] ... hit ...` 日志

> 我抄完了，检查一下有没有漏的

<details><summary>5 previous messages</summary>

> 我来按“能不能编译 + 运行时闭环是否挂齐”两条线检查：先看你抄进去的 Combat 文件内容，再扫引用和 Unity 项目里是否有明显缺口。
>
> 文件都在，命名空间也基本对上了。现在我继续看完整内容，重点找那种 Unity 编译会直接报错的小漏项，比如缺 using、类名和文件不一致、`private` 字段 Inspector 挂不上、运行顺序问题。
>
> 代码层我看下来没有明显会立刻炸掉的语法漏抄，但有两个“会导致你以为系统没工作”的细节：一个是 `AttackDefinition.duration` 你少了默认值，另一个是运行时组件/预制体是否挂上还没确认。我继续查一下 prefab/scene 里这些新脚本有没有真正被引用。
>
> 我还会做一个只读的编译层验证：如果 Unity 生成的 `.sln/.csproj` 可用，就用 `dotnet build` 抓一遍语法错误；如果 Unity 引用导致外部 build 不准，我会只把可信结论告诉你。
>
> 预制体/场景里目前没搜到新 Combat 脚本的 guid，说明你大概率还只是抄了代码，组件还没挂到 Player/NPC/Katana/场景上。这是现在最大的“漏”：代码存在，但运行时没有入口。
</details>

我检查了，**代码文件本身没有少**，7 个都在：

`AttackDefinition / AttackHitWindow / IDamageable / CombatActor / CombatHitBox / CombatHurtBox / CombatResolver`

但有两个地方需要你补一下，另外还有一个运行时挂载漏项。

**代码小漏**
你现在的 [AttackDefinition.cs](./Character/Combat/AttackDefinition.cs:13) 这里：

```csharp
[Min(0.01f)]
public float duration;
```

建议改成：

```csharp
[Min(0.01f)]
public float duration = 0.8f;
```

否则如果你在 `CombatActor` 里手动配 `_attackDefinitions`，但忘了填 duration，`NormalizedTime` 会异常偏到结尾。

另外这里：

```csharp
return duration > 0.0001f ? Mathf.Clamp01(elapsedTime / duration) : 1f;
```

建议改成：

```csharp
return duration > 0.0001f ? Mathf.Clamp01(elapsedTime / duration) : 0f;
```

不然 duration 没填时，会直接认为时间已经是 `1.0`，大多数 `0.25 - 0.45` 的有效帧都不会触发。

**最大漏项**
我搜了 prefab / scene，没找到新脚本的 guid 引用。也就是说：**代码抄进来了，但组件还没挂到对象上**。

你现在至少要挂：

- 场景里任意常驻物体：`CombatResolver`
- `Player` 根节点：`CombatActor`，TeamId = 1
- `NPC` 根节点：`CombatActor`，TeamId = 2
- `Katana/Hitbox`：`CombatHitBox`，Slot = `Katana`
- `Player/Hurtbox`：`CombatHurtBox`
- `NPC/Hurtbox`：`CombatHurtBox`

`CombatResolver` 里先保持：

```text
_serverAuthoritative = true
_applyDamage = false
_logValidHits = true
```

这样第一步只看命中 log，不会真的扣血。

**结论**
代码基本抄完整了，主要漏的是：

1. `AttackDefinition.duration` 默认值
2. `NormalizedTime` 的 fallback 返回值
3. Unity 里还没把 `CombatActor / HitBox / HurtBox / Resolver` 挂到 prefab 或 scene 上

另外我试了外部 `dotnet build`，被 Unity 的 `Temp/project.assets.json` 缺失挡住了，所以最终编译结果还是以 Unity Console 为准。

> 我改好了，先不急着挂。现在逐个文件解释给我这些是起什么作用的，解释粒度只到成员变量和函数即可，不必详细说函数实现

<details><summary>1 previous message</summary>

> 好，我按你现在工程里的版本来讲，不凭刚才那版记忆。先读一遍 7 个 Combat 文件，然后逐个解释“字段/属性/函数的职责”，不展开内部实现细节。
</details>

下面按你当前文件逐个讲，只到**成员变量 / 属性 / 函数职责**。

**AttackHitWindow.cs**
`AttackHitWindow` 表示一次攻击里的一个“有效命中窗口”。

`startNormalized`：有效帧开始时间，0 到 1 的归一化时间。
`endNormalized`：有效帧结束时间，0 到 1 的归一化时间。
`slot`：这个窗口启用哪个 HitBox，比如 Katana / Body / Kick。
`damageMultiplier`：这个窗口的伤害倍率，比如同一招不同段伤害不同。

`IsValid`：判断这个窗口配置是否有效。
`DamageMultiplierOrDefault`：如果没有填倍率，就默认 1。
`Contains()`：判断当前攻击时间是否落在这个有效窗口里。
`Create()`：快速创建一个窗口的辅助函数。

`HitBoxSlot`：HitBox 类型枚举。现在有 `Katana`、`Body`、`Kick`。

**AttackDefinition.cs**
`AttackDefinition` 表示“一种具体攻击动作”的数据，比如轻击 1、轻击 2、冲刺攻击、重击。

`attackStep`：攻击编号，对应你现在 `AttackState.CurrentComboStep`。
`displayName`：调试显示名。
`duration`：这招总时长。
`damage`：基础伤害。
`isHeavyHit`：是否算重击，用于决定受击反应。
`hitSameTargetOnce`：同一刀是否只能命中同一个目标一次。
`hitWindows`：这招包含哪些有效帧窗口。

`DebugName`：返回显示名，没有填就用 `Attack_x`。
`NormalizedTime()`：把已过去时间转换成 0 到 1 的攻击进度。
`IsWindowActive()`：判断某个命中窗口当前是否激活。
`CreateFallback()`：没有手动配置 AttackDefinition 时，用旧配置临时生成一个默认攻击数据。

`AttackRuntimeInfo` 表示“当前正在执行的一次攻击”。

`owner`：攻击发起者。
`definition`：这次攻击用的攻击数据。
`attackStep`：当前攻击编号。
`attackInstanceId`：这一次攻击实例的唯一编号，用于避免重复命中。
`elapsedTime`：这次攻击已经经过的时间。

`NormalizedTime`：当前攻击进度。
`IsValid`：这次攻击数据是否完整。
构造函数：把当前攻击运行时数据打包起来。

**IDamageable.cs**
`IDamageable` 是“可受击对象”的统一接口。

`CanReceiveHit`：当前是否可以被命中。
`ApplyHit()`：收到一次命中后如何处理。

`HitInfo` 是一次命中的结算信息。

`attacker`：攻击者。
`target`：受击者。
`attack`：使用的攻击数据。
`attackStep`：攻击编号。
`attackInstanceId`：攻击实例编号。
`windowIndex`：命中来自第几个有效帧窗口。
`slot`：哪个 HitBox 命中的。
`damage`：最终伤害值。
`isHeavyHit`：是否重击。
`hitPoint`：命中点。
`hitDirection`：命中方向。

**CombatActor.cs**
`CombatActor` 是战斗系统认识 Player / NPC 的入口。它挂在角色根节点上。

`_teamId`：阵营编号，用于避免打到自己人。
`_combatConfig`：角色战斗配置，可手动指定。
`_attackDefinitions`：这个角色的攻击数据表。
`_playerController`：如果这是玩家，引用玩家控制器。
`_npcDriver`：如果这是 NPC，引用 NPC 驱动。
`_networkIdentity`：网络身份，用来拿稳定 ActorId。
`_canReceiveHit`：是否允许受击。

`_fallbackDefinitions`：没有配置攻击数据时缓存临时生成的默认攻击。
`_trackingAttack`：当前是否正在追踪一次攻击。
`_trackedAttackStep`：正在追踪的攻击编号。
`_trackedAttackElapsed`：当前攻击已持续多久。
`_trackedAttackInstanceId`：当前攻击实例编号。
`_nextAttackInstanceId`：下一个攻击实例编号。
`_trackedAttackDefinition`：当前攻击使用的数据。

`TeamId`：公开阵营编号。
`ActorId`：战斗对象 ID，优先用 Mirror `netId`，否则用 Unity 实例 ID。
`CanReceiveHit`：当前是否可受击。
`IsDead`：通过 PlayerController 或 NpcDriver 判断是否死亡。

`Reset()`：编辑器里添加组件时自动补引用和默认队伍。
`Awake()`：运行时补引用。
`Update()`：每帧追踪当前攻击状态。
`TryGetCurrentAttack()`：给 CombatResolver 查询当前攻击运行时信息。
`ApplyHit()`：实现受击接口，把命中转发给 Player 或 NPC。
`TickAttackRuntime()`：更新当前攻击追踪。
`BeginTrackingAttack()`：开始记录一次新的攻击。
`StopTrackingAttack()`：停止当前攻击追踪。
`TryReadActiveAttackStep()`：从 Player / NPC 当前状态里读攻击编号。
`ResolveAttackDefinition()`：根据攻击编号找到攻击数据。
`ResolveLegacyDuration()`：从旧的 `CharacterCombatConfig` 取攻击时长。
`ResolveCombatConfig()`：找到应该使用的战斗配置。
`EnsureReferences()`：自动找 PlayerController / NpcDriver / NetworkIdentity。

**CombatHitBox.cs**
`CombatHitBox` 是攻击判定区域，挂在 Katana 的 Hitbox 之类的子物体上。

`Active`：当前启用中的所有 HitBox 列表。
`_owner`：这个 HitBox 属于哪个 CombatActor。
`_shapeCollider`：用于检测的 Collider。
`_slot`：这个 HitBox 的类型，比如 Katana。
`_hurtBoxMask`：只检测哪些 Layer，一般是 HurtBox。
`_queryTriggerInteraction`：Overlap 查询是否包含 Trigger。

`ActiveHitBoxes`：公开当前所有启用 HitBox。
`Owner`：公开拥有者。
`Slot`：公开 HitBox 类型。

`Reset()`：编辑器添加组件时补引用。
`Awake()`：运行时补引用。
`OnEnable()`：加入全局 Active 列表。
`OnDisable()`：从全局 Active 列表移除。
`IsConfigured()`：判断 HitBox 是否配置完整。
`OverlapHurtBoxesNonAlloc()`：主动扫描当前 HitBox 范围内的 HurtBox。
`EnsureReferences()`：自动找 owner / collider / layer mask。
`OverlapCapsule()`：胶囊 Collider 的扫描方式。
`OverlapBox()`：盒子 Collider 的扫描方式。
`OverlapSphere()`：球 Collider 的扫描方式。
`OverlapBounds()`：其他 Collider 的兜底扫描方式。
`Abs()`：处理缩放用的小工具函数。

**CombatHurtBox.cs**
`CombatHurtBox` 是受击判定区域，挂在 Player / NPC 的 Hurtbox 子物体上。

`_owner`：这个 HurtBox 属于哪个 CombatActor。
`_hurtCollider`：受击 Collider。
`_partName`：部位名，比如 Body。
`_damageMultiplier`：这个部位的受伤倍率。

`Owner`：公开拥有者。
`PartName`：公开部位名。
`DamageMultiplier`：公开伤害倍率，没填就按 1。

`Reset()`：编辑器添加组件时补引用。
`Awake()`：运行时补引用。
`TryGetOwner()`：让外部安全获取 owner。
`EnsureReferences()`：自动找 owner / collider，并保证 collider 是 Trigger。

**CombatResolver.cs**
`CombatResolver` 是战斗判定中心。它不代表某个角色，而是负责把 HitBox、HurtBox、AttackDefinition 串起来。

`_serverAuthoritative`：是否只允许服务器做判定。
`_applyDamage`：是否真的执行伤害；现在可以先 false，只打 log。
`_logValidHits`：是否输出命中日志。
`_maxOverlapResults`：一次 Overlap 最多缓存多少 Collider。
`_overlapResults`：Overlap 查询用的缓存数组。
`_resolvedHits`：记录已经结算过的命中，防止同一刀重复打同一目标。

`Awake()`：初始化查询缓存。
`Update()`：每帧执行战斗判定。
`OnDisable()`：关闭时清理命中记录。
`CanResolve()`：判断当前机器是否有资格做判定。
`ResolveAllActiveHitBoxes()`：遍历所有启用的 HitBox，找当前有效攻击。
`ResolveWindow()`：对某个有效帧窗口执行 HurtBox 扫描。
`TryResolveHit()`：过滤自己、队友、重复命中、不可受击目标，并生成 HitInfo。

`HitKey` 是内部用的重复命中 key。

`_attackerId`：攻击者 ID。
`_attackInstanceId`：攻击实例 ID。
`_targetId`：目标 ID。
`_windowKey`：窗口 key。

`Equals()` / `GetHashCode()`：让 `HashSet<HitKey>` 能判断“这次命中是否已经处理过”。

> 挂好了，检查一下

<details><summary>3 previous messages</summary>

> 我来按挂载闭环查：先确认这几个脚本的 guid，然后在 prefab / scene 里找引用位置，再看每个组件的 serialized 字段有没有 owner、layer、slot、队伍这些关键配置。
>
> 挂载引用已经能看到了：Player/NPC 根节点有 `CombatActor`，Katana 有 `CombatHitBox`，Player/NPC 的 Hurtbox 有 `CombatHurtBox`。我再补查一下 `CombatManager` 有没有进场景，以及 Katana 的运行时父级能不能找到 Player/NPC 的 `CombatActor`。
>
> 目前挂载的主链路是对的，不过有两个点我还要确认：`CombatManager` 是不是只在 Online 场景里，以及 Katana 在运行时是不是确实挂在 Player/NPC 层级下面。这个会决定 `_owner` 自动查找能不能成功。
</details>

检查完了，主链路挂得基本对。

确认到的挂载：

- [Player.prefab](D:/UnityProject/SS-3CDemoPrivate/Assets/Prefabs/Characters/Player.prefab:462)：根节点有 `CombatActor`，`TeamId = 1`
- [NPC.prefab](D:/UnityProject/SS-3CDemoPrivate/Assets/Prefabs/Characters/NPC.prefab:501)：根节点有 `CombatActor`，`TeamId = 2`
- [Katana.prefab](D:/UnityProject/SS-3CDemoPrivate/Assets/Prefabs/Characters/Katana.prefab:225)：`Hitbox` 上有 `CombatHitBox`
- [Player.prefab](D:/UnityProject/SS-3CDemoPrivate/Assets/Prefabs/Characters/Player.prefab:537)：`Hurtbox` 上有 `CombatHurtBox`
- [NPC.prefab](D:/UnityProject/SS-3CDemoPrivate/Assets/Prefabs/Characters/NPC.prefab:576)：`Hurtbox` 上有 `CombatHurtBox`
- [CombatManager.prefab](D:/UnityProject/SS-3CDemoPrivate/Assets/Prefabs/System/CombatManager.prefab:44)：有 `CombatResolver`

几个关键配置也对：

- Katana `Hitbox` 在 Layer 6
- Player / NPC `Hurtbox` 在 Layer 7
- Katana `_slot = 1`，也就是 `HitBoxSlot.Katana`
- Katana `_hurtBoxMask = 128`，对应 Layer 7 `HurtBox`
- `CombatResolver` 当前 `_applyDamage = false`，只打 log，不扣血，适合第一轮测试

有两个注意点：

1. `Katana` 上的 `_owner` 是空的，这是正常的。因为 Katana 是嵌套 prefab，不能直接引用外层 Player/NPC 的 `CombatActor`。运行时会通过 `GetComponentInParent<CombatActor>()` 自动找。

2. `CombatManager` 目前我只看到它进了 [Online.unity](D:/UnityProject/SS-3CDemoPrivate/Assets/Scenes/Online.unity:2670)。如果你在 `Offline`、`AITest` 或别的场景测试，就不会有 `CombatResolver` 跑，自然不会出 `[CombatResolver] Hit resolved` log。

下一步测试建议：在 `Online` 场景里跑，先保持 `_applyDamage = false`，攻击 NPC，看 Console 是否出现：

```text
[CombatResolver] Hit resolved: Player -> NPC
```

如果没有 log，下一步就查运行时 `CombatHitBox.Owner` 有没有自动找到，以及有效帧窗口 `0.25 - 0.45` 时刀的 collider 是否真的扫到 NPC Hurtbox。

> 有log了，接下来做什么，说起来这个攻击数据要怎么配置，有ScriptableObject吗，还是有可视化工具？

有 log 说明第一步闭环已经通了：**有效帧 + HitBox 扫 HurtBox + Resolver 过滤**都在工作。接下来先别急着开伤害，先把攻击数据配置方式定下来。

你现在这版 **没有独立 ScriptableObject**。
`AttackDefinition` 只是 `[Serializable]` 数据类，目前配置入口是：

```text
CombatActor._attackDefinitions
```

也就是挂在 Player / NPC 根节点 `CombatActor` Inspector 里的数组。如果这个数组为空，就会走 fallback 默认攻击数据。

我建议分两步走。

**短期**
先直接在 `CombatActor._attackDefinitions` 里配，最快验证：

```text
attackStep = 1
displayName = Attack_Combo1
duration = 0.8167
damage = 10
isHeavyHit = false
hitSameTargetOnce = true
hitWindows:
  slot = Katana
  startNormalized = 0.25
  endNormalized = 0.45
  damageMultiplier = 1
```

然后给 `attackStep = 2/3/4/5/6/7/8/9` 分别补数据。
这样你马上能调每段攻击的有效帧。

**中期推荐**
把攻击数据收到 ScriptableObject 里。最贴合你现在项目架构的方式不是让每个攻击单独一个 SO，而是放进已有的 `CharacterCombatConfig` 或新建一个 `AttackSetConfig`。

我更推荐：

```text
CharacterCombatConfig
  attackDefinitions[]
```

原因是你现在 Player / NPC 已经通过 `GameDataManager.Instance.Player.combat` / `Npc.combat` 读战斗配置了，攻击时长也已经在 `CharacterCombatConfig` 里。把 `AttackDefinition[]` 放进去，数据就集中，不用每个 prefab 重复配。

之后 `CombatActor.ResolveAttackDefinition()` 优先顺序可以变成：

```text
1. CombatActor 自己的 _attackDefinitions override
2. CharacterCombatConfig.attackDefinitions
3. fallback 默认数据
```

这样既保留 prefab 临时覆盖，也有正式配置源。

**可视化工具**
现在不需要立刻做完整可视化工具。Inspector 数组先够用。

但后面很值得做一个小 Editor，不需要很重：

```text
AttackDefinition Editor
  Attack_Combo1
  Duration: 0.8167
  Timeline: |---- windup ---- [ active ] ---- recovery ----|
  Start / End normalized sliders
  Damage
  Slot
```

更进一步可以读 AnimationClip 长度，把 frame 转成 normalized time：

```text
startFrame = 18
endFrame = 27
clipFPS = 30
=> startNormalized / endNormalized
```

不过注意：**不要把权威命中判定绑到 Animation Event 上**。Animation Event 可以用来放刀光、音效、震屏；真正的攻击有效帧还是应该来自配置数据，这样服务器也能稳定判断。

下一步我建议：

1. 先把 `attackStep 1` 的数据手动配准。
2. 用 log 调 `startNormalized / endNormalized`，确认实际手感。
3. 再把数据入口迁移到 `CharacterCombatConfig.attackDefinitions`。
4. 最后开 `_applyDamage = true`，进入 G0-2 伤害结算。

> attackStep代表什么，这是怎么和攻击动作对应的，displayName反射找吗

`attackStep` 现在代表的是**攻击动作 ID**，不是反射，也不是按 `displayName` 找。

它和动作的对应关系来自你现有的攻击状态机约定：

```csharp
AttackState.CurrentComboStep
```

也就是这里：

```text
1 = Attack_Combo1
2 = Attack_Combo2
3 = Attack_Combo3
4 = Attack_Combo4
5 = AttackSprint
6 = AttackDodge
7 = AttackHeavy1Start
8 = AttackHeavy1
9 = AttackHeavy2
```

`CombatActor` 会在角色处于 `Attack` 状态时读取：

```csharp
playerAttack.CurrentComboStep
npcAttack.CurrentComboStep
```

然后拿这个数字去找：

```csharp
AttackDefinition.attackStep == CurrentComboStep
```

所以对应关系是：

```text
AttackState 当前攻击编号
        ↓
CurrentComboStep = 1
        ↓
CombatActor 找 attackStep == 1 的 AttackDefinition
        ↓
CombatResolver 使用这个 AttackDefinition 的 duration / damage / hitWindows
```

`displayName` 只是给人看的调试名。比如 log 里显示 `Attack_Combo1`，Inspector 里方便识别。它**不参与查找，不反射，不驱动 Animator**。

真正驱动动画的是你现有的表现层逻辑，大概是：

```text
CurrentComboStep = 1 -> AnimatorParams.AttackCombo1
CurrentComboStep = 2 -> AnimatorParams.AttackCombo2
...
```

也就是说现在有两套都用同一个数字 ID 对齐：

```text
CurrentComboStep = 1
    -> 播放 Attack_Combo1 动画
    -> 使用 attackStep = 1 的攻击数据
```

这个设计短期可用，但我建议后面把名字改清楚一点。`attackStep` 其实不只是“连段第几段”，因为 5/6/7/8/9 已经是冲刺攻击、闪避攻击、重击了。更准确的名字应该是：

```csharp
attackId
```

或者：

```csharp
attackMoveId
```

现在可以先不改，避免牵扯同步字段和 Presenter。你只要记住：

```text
attackStep = 当前攻击动作编号
displayName = 纯显示名
查找依据 = attackStep，不是 displayName
```

> 我没看到在哪里配了，AttackState里有吗，是枚举吗？

<details><summary>1 previous message</summary>

> 我看一下当前代码里这个编号是从哪里产生、又在哪里被解释成动画的，这样能把“它是不是枚举/在哪里配”说准。
</details>

对，你没看到“配置表”是正常的，因为**现在它不是枚举，也不是可视化配置**，而是硬编码在 `AttackState` 里的 `const byte`。

在 [AttackState.cs](./Character/StateMachine/States/AttackState.cs:7) 这里：

```csharp
public const byte Combo1Step = 1;
public const byte Combo4Step = 4;
public const byte SprintAttackStep = 5;
public const byte DodgeAttackStep = 6;
public const byte Heavy1StartStep = 7;
public const byte Heavy1Step = 8;
public const byte Heavy2Step = 9;
```

所以目前的对应关系是代码约定：

```text
1 = Combo1
2 = Combo2
3 = Combo3
4 = Combo4
5 = SprintAttack
6 = DodgeAttack
7 = Heavy1Start
8 = Heavy1
9 = Heavy2
```

其中 `2/3/4` 没有单独 const，是通过这里递增出来的：

```csharp
BeginAttack((byte)(_currentAttackStep + 1));
```

动画对应关系在 [CharacterCombatPresenter.cs](./Character/Presentation/CharacterCombatPresenter.cs:227)：

```csharp
private static int ComboStepToAttackHash(byte comboStep)
{
    return comboStep switch
    {
        1 => AnimatorParams.AttackCombo1,
        2 => AnimatorParams.AttackCombo2,
        3 => AnimatorParams.AttackCombo3,
        4 => AnimatorParams.AttackCombo4,
        5 => AnimatorParams.AttackSprint,
        6 => AnimatorParams.AttackDodge,
        7 => AnimatorParams.AttackHeavy1Start,
        8 => AnimatorParams.AttackHeavy1,
        9 => AnimatorParams.AttackHeavy2,
        _ => AnimatorParams.AttackCombo1,
    };
}
```

也就是说：

```text
AttackState 决定当前 step 是几
CharacterCombatPresenter 用 step 选动画
CombatActor 用 step 选 AttackDefinition
```

现在并没有枚举，`attackStep` 只是 `byte`。
这也是为什么你会觉得“不明显”：这个 ID 体系散在两个地方：

- `AttackState`：定义 / 产生攻击编号
- `CharacterCombatPresenter`：把攻击编号映射到动画
- `CombatActor`：把攻击编号映射到攻击数据

我建议下一步把它整理成一个明确枚举，比如：

```csharp
public enum AttackMoveId : byte
{
    None = 0,
    Combo1 = 1,
    Combo2 = 2,
    Combo3 = 3,
    Combo4 = 4,
    Sprint = 5,
    Dodge = 6,
    Heavy1Start = 7,
    Heavy1 = 8,
    Heavy2 = 9
}
```

然后 `AttackDefinition.attackStep` 改成：

```csharp
public AttackMoveId attackId;
```

这样 Inspector 里你就不是填数字 `1/2/3`，而是下拉选择 `Combo1 / Combo2 / Sprint / Heavy1`，清晰很多。

现在这版能跑，但确实还比较“程序员临时编号”。你这个直觉是对的，下一步应该把它显式枚举化。

> 对，处理一下

<details><summary>32 previous messages</summary>

> 我来把这个临时数字体系收拢成显式枚举。会尽量保持网络同步和 Presenter 的 byte 字段不大动，只在边界做转换，这样不会把已有 Phase F 的同步链路牵扯太大。
>
> 我准备动手改了。核心策略是：新增 `AttackMoveId` 枚举；Inspector 里攻击数据改成下拉 `attackId`；但同步层继续传 `byte AttackComboStep`，在入口/出口做转换，避免改 Mirror 消息和快照结构。
>
> 我会新加一个 `AttackMoveId` 文件，然后把 AttackState、NpcAttackState、Combat 数据和 Presenter 都改成用这个枚举。同步层仍然继续收发 byte，所以不会改 Mirror 消息结构。
>
> <details><summary>Created 2 files</summary>
>
> - Wrote `./Character/Combat/AttackMoveId.cs` (+59 -0)
> - Wrote `./Character/Combat/AttackMoveId.cs.meta` (+11 -0)
>
> </details>
>
> 枚举文件已经加上了。接下来我会替换各处 `attackStep` 的内部含义：数据层字段改成 `attackId`，但保留一部分 byte 兼容入口，保证现有同步和调试调用还能走。
>
> <details><summary>Edited a file</summary>
>
> - Wrote `./Character/Combat/AttackDefinition.cs` (+39 -38)
>
> </details>
>
> 数据类现在用 `AttackMoveId` 了，Inspector 会显示下拉选择。下一步改状态机，保留 `CurrentComboStep` 这个 byte 属性给同步层用，同时新增更清楚的 `CurrentAttackId`。
>
> <details><summary>Edited a file</summary>
>
> - Wrote `./Character/StateMachine/States/AttackState.cs` (+100 -106)
>
> </details>
>
> 玩家攻击状态已经枚举化了。现在处理 NPC 攻击状态和配置读取，让调试入口还能传 byte，但内部统一转成 `AttackMoveId`。
>
> <details><summary>Edited 2 files</summary>
>
> - Wrote `./AI/NpcAttackState.cs` (+25 -21)
> - Wrote `./Character/Config/CharacterCombatConfig.cs` (+25 -19)
>
> </details>
>
> 配置读取也改好了，旧的 `GetAttackDuration(byte)` 还在，避免同步/历史调用断掉。接下来改 CombatActor / HitInfo / Resolver，把日志和查表都改成 attackId。
>
> <details><summary>Edited 3 files</summary>
>
> - Wrote `./Character/Combat/IDamageable.cs` (+5 -5)
> - Wrote `./Character/Combat/CombatActor.cs` (+89 -88)
> - Wrote `./Character/Combat/CombatResolver.cs` (+9 -9)
>
> </details>
>
> Combat 判定链路已经改成枚举 ID 了。还剩表现层和快照校验：Presenter 继续接收 byte，但立刻转换成枚举选择动画，StateSnapshot 的范围检查也改成用枚举最大值。
>
> <details><summary>Edited a file</summary>
>
> - Wrote `./Character/Presentation/CharacterCombatPresenter.cs` (+24 -23)
>
> </details>
>
> 我继续收尾几个引用点，尤其是旧常量 `AttackState.Heavy2Step` 已经不存在了，要把它换成 `AttackMoveId.Heavy2` 的边界判断。
>
> 剩下一个旧引用还没收：`StateSnapshot` 还在用被删掉的 `AttackState.Heavy2Step` 做上限检查。我会把它也换成 `AttackMoveId`，顺手把枚举扩展里的范围判断改成更稳的 byte 比较，避免 Unity/C# 版本对 enum 关系模式挑剔。
>
> <details><summary>Edited 2 files</summary>
>
> - Wrote `./Character/Combat/AttackMoveId.cs` (+7 -4)
> - Wrote `./Character/Sync/StateSnapshot.cs` (+12 -12)
>
> </details>
>
> 主要代码改完了。我现在跑一次搜索和编译前静态检查，找漏掉的旧字段名、常量、以及可能因为 prefab 序列化字段名变化造成的数据丢失点。
>
> 旧常量和旧 `AttackState.Heavy2Step` 都清掉了。现在我再查 NPC 调试入口，因为它可能还在用“comboStep”这个老名字；不一定影响功能，但容易让 Inspector 继续迷惑。
>
> 同步字段里还保留 `AttackComboStep` 这个旧名字，是有意的：它已经进了 Snapshot/Mirror 消息，贸然改名会牵动大。现在我会把 NPC 调试入口也改成枚举下拉，这样 Inspector 里不会再让你填数字。
>
> <details><summary>Edited 4 files</summary>
>
> - Wrote `./AI/NpcCharacterDriver.cs` (+15 -9)
> - Wrote `./AI/NpcCombatDebug.cs` (+12 -9)
> - Wrote `./Character/Combat/AttackDefinition.cs` (+4 -2)
> - Wrote `./Character/Sync/StateSnapshot.cs` (+2 -2)
>
> </details>
>
> 枚举下拉迁移现在也保护了：用了 `FormerlySerializedAs`，你以后如果已经在 Inspector 配过 `_attackComboStep` 或 `attackStep`，Unity 会尽量迁移到新字段。接下来做一轮编译级检查，找 C# 错误。
>
> 我发现一个我刚刚改出来的小编译漏点：`StateSnapshot` 还在用 `SprintState` / `GuardState` 类型，我刚才删 using 时删过头了。现在补回去，再继续验证。
>
> <details><summary>Edited a file</summary>
>
> - Wrote `./Character/Sync/StateSnapshot.cs` (+1 -0)
>
> </details>
>
> 补回来了。为了避免 Unity 里冒出“文件名和类名不匹配/using 漏掉”的低级错误，我再用搜索和简单 C# 语义检查扫一轮；外部 dotnet 仍可能被 Unity 临时资产拦住。
>
> 静态搜索现在只剩下同步字段里的旧命名和 `FormerlySerializedAs`，这是预期的。外部 `dotnet build` 被 Unity 生成的 assets 文件挡住了，我再尝试先 restore 一下，看看能不能拿到更真实的 C# 编译反馈。
>
> `dotnet restore` 已经补上了 Unity 工程的临时依赖文件，我现在再跑一次 `Assembly-CSharp` 编译，尽量抓到真正的 C# 错误。
>
> 外部 build 报了一串 `AttackMoveId` 找不到，我先确认原因。看起来不像 using 漏了，而是 Unity 生成的 `Assembly-CSharp.csproj` 还没把新文件纳入编译；我会检查文件位置和 csproj 引用。
>
> 原因确认了：新加的 `AttackMoveId.cs` 文件已经在磁盘上，但当前 `Assembly-CSharp.csproj` 还没把它列进 `<Compile>`，所以外部 `dotnet build` 看不到这个类型。Unity 刷新/Regenerate project files 后通常会自动补；我先不碰生成文件，继续把源码层检查干净。
>
> 这里有个小岔口：源码已经改成枚举了，但外部 `dotnet build` 用的是 Unity 生成的旧 `.csproj`，它还没把新文件 `AttackMoveId.cs` 纳入编译。为了不依赖 Unity 重新生成项目文件，我把枚举挪进已经在 `.csproj` 里的 `AttackDefinition.cs`，这样 VS Code / dotnet 也能立刻识别。
>
> <details><summary>Edited a file, deleted 2 files</summary>
>
> - Wrote `./Character/Combat/AttackDefinition.cs` (+59 -0)
> - Deleted `./Character/Combat/AttackMoveId.cs` (+0 -62)
> - Deleted `./Character/Combat/AttackMoveId.cs.meta` (+0 -11)
>
> </details>
>
> 枚举现在放进 `AttackDefinition.cs` 了，避免 Unity 项目文件没刷新时类型不可见。接下来再跑一次 build，这次应该能看到更真实的问题。
</details>

处理好了。

现在 `attackStep` 已经整理成显式枚举 `AttackMoveId`，放在 [AttackDefinition.cs](./Character/Combat/AttackDefinition.cs:7) 顶部：

```csharp
None
Combo1
Combo2
Combo3
Combo4
Sprint
Dodge
Heavy1Start
Heavy1
Heavy2
```

主要改动：

- `AttackDefinition.attackStep` 改成 `attackId`，Inspector 里会变成下拉选项。
- `AttackState` 内部不再用裸数字，改用 `AttackMoveId`。
- 保留 `CurrentComboStep`，让现有网络同步 `AttackComboStep` 继续可用。
- `CharacterCombatPresenter` 现在把 byte 转成 `AttackMoveId` 再映射动画。
- `CombatActor` 用 `attackId` 查攻击数据。
- `NpcCombatDebug` 的攻击调试参数也改成枚举下拉。
- `displayName` 仍然只是显示名，不参与查找。

我还加了兼容：

```csharp
[FormerlySerializedAs("attackStep")]
public AttackMoveId attackId;
```

所以如果你后面已经配过旧字段，Unity 会尽量迁移。

验证过了：

```text
dotnet build Assembly-CSharp.csproj --no-restore
已成功生成，0 error
```

现在你在 `CombatActor._attackDefinitions` 里配攻击数据时，不用填数字了，直接选：

```text
attackId = Combo1
attackId = Sprint
attackId = Heavy1
```

这样清楚很多。

> 可以了，现在把配置抽离出来，不要放在代码或者inspector里，希望能用表或者SO来配。攻击的话，用一个SO配所有的就可以了。

<details><summary>16 previous messages</summary>

> 我来把攻击数据从 `CombatActor` 上挪出去，做成一个统一的攻击配置 SO。思路是：`CombatActor` 不再持有 `_attackDefinitions` 数组，只通过角色的 `CharacterCombatConfig` 找到一个 `AttackDefinitionSet`，再按 `AttackMoveId` 查询。
>
> 我会把“攻击数据”做成一个 `AttackDefinitionSet` ScriptableObject：它里面放全部攻击条目。`CombatActor` 不再暴露 `_attackDefinitions`，只从角色的 `CharacterCombatConfig.attackSet` 读取；如果没配 SO，才用 fallback，避免测试场景立刻断。
>
> 项目已经有 `Assets/Data/SkillData` 文件夹，刚好可以放攻击配置。实现上我会新增一个 `AttackDefinitionSet` SO，并在 `CharacterCombatConfig` 里引用它；这样“所有攻击用一个 SO 配”同时又能沿用你现有的 `GameDataManager -> CharacterDefinition -> CombatConfig` 数据路径。
>
> 开始改代码和数据资源了：新增 `AttackDefinitionSet.cs`，`CharacterCombatConfig` 加 `attackSet` 字段，`CombatActor` 改为从 SO 查定义。然后我会顺手创建一个默认攻击配置 asset，并把 `DefaultCombat` 指过去。
>
> <details><summary>Created 2 files, edited 2 files</summary>
>
> - Wrote `./Character/Combat/AttackDefinitionSet.cs` (+41 -0)
> - Wrote `./Character/Combat/AttackDefinitionSet.cs.meta` (+11 -0)
> - Wrote `./Character/Combat/CombatActor.cs` (+8 -17)
> - Wrote `./Character/Config/CharacterCombatConfig.cs` (+12 -6)
>
> </details>
>
> 代码入口已经从角色 Inspector 数组切到 `CharacterCombatConfig.attackSet` 了。现在我要创建默认攻击 SO 资源，并把现有 `DefaultCombat.asset` 连接到它，这样运行时不用手工在每个 prefab 上配。
>
> <details><summary>Created 2 files, edited a file</summary>
>
> - Wrote `D:/UnityProject/SS-3CDemoPrivate/Assets/Data/Character/DefaultCombat.asset` (+5 -4)
> - Wrote `D:/UnityProject/SS-3CDemoPrivate/Assets/Data/SkillData/DefaultAttackSet.asset` (+110 -0)
> - Wrote `D:/UnityProject/SS-3CDemoPrivate/Assets/Data/SkillData/DefaultAttackSet.asset.meta` (+8 -0)
>
> </details>
>
> 默认攻击配置已经落到 `Assets/Data/SkillData/DefaultAttackSet.asset`，`DefaultCombat` 也已经引用它。接下来我会清掉 prefab 上残留的空 `_attackDefinitions` 序列化字段，避免之后 Inspector 看起来像还有旧入口。
>
> <details><summary>Edited 2 files</summary>
>
> - Wrote `D:/UnityProject/SS-3CDemoPrivate/Assets/Prefabs/Characters/NPC.prefab` (+0 -1)
> - Wrote `D:/UnityProject/SS-3CDemoPrivate/Assets/Prefabs/Characters/Player.prefab` (+0 -1)
>
> </details>
>
> 旧 Inspector 数组入口也清掉了。现在我跑一遍搜索和编译，确认没有代码还在读 `_attackDefinitions`，SO 脚本也被当前项目文件纳入编译。
>
> 我看到新建的 `AttackDefinitionSet.cs` 还用代码初始化了一套默认攻击数组，这和你“不要放代码里”的方向不够干净。我会把它改成纯容器，真实数据只放 `DefaultAttackSet.asset` 里；代码里只保留缺资源时的兜底逻辑。
>
> <details><summary>Edited a file</summary>
>
> - Wrote `./Character/Combat/AttackDefinitionSet.cs` (+1 -12)
>
> </details>
>
> 现在 SO 脚本是纯数据容器了。由于 Unity 的 `.csproj` 又没自动纳入新文件，我会先跑 restore/build看真实情况；如果缺新文件，我会用最小方式处理，保证本地编译也通过。
>
> `AttackDefinitionSet.cs` 是新文件，Unity 打开后会自动把它纳入工程；但当前外部 `dotnet build` 用的 `.csproj` 还没刷新，所以它看不到这个类型。我会把项目文件也补上这一项，保证 VS Code / dotnet 这边立刻干净。
>
> <details><summary>Edited a file</summary>
>
> - Wrote `D:/UnityProject/SS-3CDemoPrivate/Assembly-CSharp.csproj` (+3 -2)
>
> </details>
>
> 项目文件已补。再跑一遍 build，同时我会确认 prefab 里旧 `_attackDefinitions` 不再残留，配置 asset 的引用也对上。
</details>

已抽离成一个统一 SO 配置了。

现在攻击数据入口是：

[DefaultAttackSet.asset](D:/UnityProject/SS-3CDemoPrivate/Assets/Data/SkillData/DefaultAttackSet.asset)

它里面一次性配置所有攻击：

```text
Combo1
Combo2
Combo3
Combo4
Sprint
Dodge
Heavy1Start
Heavy1
Heavy2
```

每条包含：

```text
attackId
displayName
duration
damage
isHeavyHit
hitSameTargetOnce
hitWindows
```

代码链路现在是：

```text
DefaultCombat.asset
  -> attackSet = DefaultAttackSet.asset
      -> AttackDefinition[]
          -> CombatActor 按当前 AttackMoveId 查询
```

我改了这些关键点：

- 新增 [AttackDefinitionSet.cs](./Character/Combat/AttackDefinitionSet.cs)，纯 SO 容器。
- [CharacterCombatConfig.cs](./Character/Config/CharacterCombatConfig.cs:19) 增加 `attackSet` 引用。
- [CombatActor.cs](./Character/Combat/CombatActor.cs:187) 不再读 prefab 上的 `_attackDefinitions`，改为从 `CharacterCombatConfig.attackSet` 查。
- [DefaultCombat.asset](D:/UnityProject/SS-3CDemoPrivate/Assets/Data/Character/DefaultCombat.asset:18) 已经指向 `DefaultAttackSet.asset`。
- Player / NPC prefab 上残留的 `_attackDefinitions: []` 已清掉。

验证：

```text
dotnet build Assembly-CSharp.csproj --no-restore
0 error, 0 warning
```

之后调攻击数据就去 `Assets/Data/SkillData/DefaultAttackSet.asset`，不用再在代码或 Player/NPC Inspector 上配了。

> SO的格式应该为字典式，按step为key，value为攻击信息。把当前的都ADD进去。现在这个是写死的不能扩展的

<details><summary>7 previous messages</summary>

> 明白，你要的不是“数组里每个 AttackDefinition 自己带 attackId”，而是更像表结构：`step/id -> attack info`。Unity 原生 Inspector 不能直接序列化 `Dictionary`，所以我会做成“可序列化 Key-Value 表 + 运行时字典缓存”：Inspector/资产里是 entries，代码读取时按 key 查。
>
> 我会把 `AttackDefinitionSet` 改成真正的 key-value entries：`step` 在外层作为 key，`AttackDefinition` 不再自带 `attackId`。这样资产里就是 `entries: - step: Combo1, value: {...}`，运行时缓存成字典查。
>
> <details><summary>Edited 2 files</summary>
>
> - Wrote `./Character/Combat/AttackDefinition.cs` (+1 -4)
> - Wrote `./Character/Combat/AttackDefinitionSet.cs` (+43 -12)
>
> </details>
>
> 代码现在是 KV 表结构了。接下来我要把现有 `DefaultAttackSet.asset` 从数组格式迁移成 `entries`，把 1 到 9 全部作为 key 加进去，value 里只保留攻击信息。
>
> <details><summary>Edited a file</summary>
>
> - Wrote `D:/UnityProject/SS-3CDemoPrivate/Assets/Data/SkillData/DefaultAttackSet.asset` (+105 -96)
>
> </details>
>
> 资产已经迁成 `entries` 结构，1-9 都在表里。现在我检查有没有旧 `attacks:` / `attackId:` 数据残留，并跑编译。
>
> 刚才一个搜索命令里的引号让 PowerShell/rg 解析歪了，编译也因为 Temp 里的 restore 文件又被清掉了。我会重跑 restore/build，并用简单搜索确认旧字段已清理。
</details>

搜索确认 SO asset 已经是 `entries/step/value` 了，代码里剩下的 `attackId` 都是运行时变量或迁移标记。现在补 restore 后再编译。

---

## 2026-07-02 G0 当前实现记忆：战斗结算、血条、RootMotion、锁定死亡逻辑

本轮目标从 G0-1/G0-2 的命中结算继续推进，同时处理表现层可见问题。当前代码仍是未提交工作区状态，但 `dotnet build Assembly-CSharp.csproj --no-restore` 已通过，0 error / 0 warning。

### 1. Attack SO 架构修正

`CharacterCombatConfig` 不再直接写死攻击数组，改为引用独立的 `AttackDefinitionSet`：

```text
DefaultCombat.asset
  -> attackSet = DefaultAttackSet.asset
      -> entries[]
          step: AttackMoveId
          value: AttackDefinition
```

关键点：

- `AttackDefinitionSet` 是 ScriptableObject 容器，Inspector 中用 `entries: step/value` 表示，运行时构建字典缓存。
- `CharacterCombatConfig.TryGetAttackDefinition(...)` 委托给 `attackSet.TryGet(...)`。
- `CombatActor` 根据当前 `AttackMoveId` 查表，不再从 prefab 或写死数组取攻击信息。
- `DefaultCombat.asset` 已指向 `Assets/Data/SkillData/DefaultAttackSet.asset`。

### 2. 当前命中判定模型

当前 G0 命中流程是主动查询式，不依赖 Collider 原生 Trigger 回调：

```text
CombatActor 读取当前 AttackMoveId
  -> AttackDefinition.hitWindows 判断当前攻击时间是否在有效窗口
  -> CombatResolver 用 HitBox / HurtBox + Overlap 查询候选
  -> HurtBox 定位 CombatActor
  -> 命中后 ApplyHit / ApplyHealthDamage
  -> 目标进入 Hit 或 Dead
```

这符合“动画时间窗 + hitbox overlap + 服务器/权威结算”的方向。后续若要工业化，建议继续保留“数据窗口 + 主动查询”，Trigger 只适合做辅助感知，不作为核心攻击判定来源。

### 3. NPC 血条与伤害数字

新增 `NpcHealthBarView`：

- 挂在 NPC prefab 上。
- 监听 `CombatActor.HealthChanged` / `DamageTaken`。
- NPC 头顶显示红色条状血条。
- 扣血时弹出红色伤害数字。
- `CombatManager.prefab` 的 `_applyDamage` 已打开，攻击可以真实扣 NPC 血。

相关文件：

- `Assets/Scripts/Character/Combat/NpcHealthBarView.cs`
- `Assets/Prefabs/Characters/NPC.prefab`
- `Assets/Prefabs/System/CombatManager.prefab`

### 4. Hit / Dead RootMotion 修正

受击和死亡以前会出现“模型动画位移”和“实际物体位置”逐渐偏离。当前处理方式：

- `AnimatorRootMotionRelay` 改为通过接口转发 `OnAnimatorMove`。
- `PlayerController` 和 `NpcCharacterDriver` 都实现 root motion 接收。
- Player 侧通过 `CharacterMotor` 消费 Hit / Dead 的 root motion。
- NPC 侧通过 `NpcMotor.ApplyRootMotionDelta(...)` 消费 Hit / Dead 的 root motion。
- Attack / Hit / Dead 都由 Animator root motion 驱动实际位置，远端仍通过已有 snapshot 同步。

相关文件：

- `Assets/Scripts/Character/Controller/AnimatorRootMotionRelay.cs`
- `Assets/Scripts/Character/Controller/PlayerController.cs`
- `Assets/Scripts/Character/Motor/CharacterMotor.cs`
- `Assets/Scripts/AI/NpcCharacterDriver.cs`
- `Assets/Scripts/AI/NpcMotor.cs`
- `Assets/Scripts/Character/StateMachine/States/HitState.cs`
- `Assets/Scripts/Character/StateMachine/States/DeadState.cs`

### 5. 锁定白点挂胸口

锁定白点不再使用根物体固定偏移，而是由 `LockOnTarget` 运行时解析模型骨骼：

```text
UpperChest -> Chest -> Spine -> 旧 LockOnPoint fallback
```

实现方式：

- `LockOnTarget` 新增 `_useHumanoidChest`、`_chestOffset`。
- 运行时在胸骨下创建 `RuntimeLockOnChestPoint`。
- Player/NPC prefab 都开启 `_useHumanoidChest`。
- 旧 `LockOnPoint` 仍保留，作为找不到骨骼时的 fallback。

相关文件：

- `Assets/Scripts/Character/LockOn/LockOnTarget.cs`
- `Assets/Prefabs/Characters/Player.prefab`
- `Assets/Prefabs/Characters/NPC.prefab`

### 6. Dead 状态与锁定逻辑

新增规则：

- 目标进入 `Dead` 后不能再被锁定。
- 当前锁定目标死亡时，玩家会自动解除旧锁定，并尝试锁定周围其他敌人。
- 本地玩家自己进入 `Dead` 后会清除锁定，并停止响应锁定输入。
- 远端 NPC 的死亡状态同时参考 `RemoteActionApplier` / `RemoteInterpolator`，避免客户端还能锁死亡 NPC。

关键实现：

- `ILockOnTarget` 增加 `bool IsDead`。
- `LockOnTarget.CanBeLocked` 现在包含 `!IsDead`。
- `CombatActor.IsDead` 汇总本地 Player FSM、NPC FSM、远端 action/snapshot、HP。
- `PlayerLockOnController` 新增自动切换目标逻辑：
  - `_autoSwitchOnTargetDeath = true`
  - `_autoSwitchRequiresCameraForward = false`

相关文件：

- `Assets/Scripts/Character/LockOn/ILockOnTarget.cs`
- `Assets/Scripts/Character/LockOn/LockOnTarget.cs`
- `Assets/Scripts/Character/LockOn/PlayerLockOnController.cs`
- `Assets/Scripts/Character/Combat/CombatActor.cs`

### 当前验证

最后一次验证：

```text
dotnet build Assembly-CSharp.csproj --no-restore
0 warning
0 error
```

### 下次继续的入口

优先建议下一步：

1. 进 Unity Play Mode 验证：攻击 NPC -> 扣血 -> 弹伤害数字 -> 血条减少 -> 死亡后自动换锁。
2. 双端验证：客户端是否无法锁定已死亡 NPC，远端 Dead 是否正确传播到 `CombatActor.IsDead`。
3. 若死亡后血条仍显示，可决定是否在 `NpcHealthBarView` 中 Dead 后隐藏或淡出。
4. G0 后续可以继续做 Guard 方向防御、减伤/破防/弹反，以及伤害结果的更完整网络同步。

> 保存记忆

<details><summary>1 previous message</summary>

> Context compaction
>
> Source: automatic
> Status: completed
</details>

> 我点了 copied as markdown，存哪去了

<details><summary>1 previous message</summary>

> Context compaction
>
> Source: automatic
> Status: completed
</details>
