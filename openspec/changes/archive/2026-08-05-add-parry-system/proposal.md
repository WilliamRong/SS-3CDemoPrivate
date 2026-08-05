## Why

当前战斗已经具备攻击窗口、格挡、方向受击和 Server 权威结算，但缺少以精确时机反制攻击的高风险防守手段。新增类似《黑暗之魂》的弹反能力，使玩家可以用短暂的全方向有效窗口取消一次来袭攻击，并强制攻击方进入可识别的硬直状态。

## What Changes

- 为 Player 增加绑定 `Q` 的单次弹反输入，并以独立 `Parry` 逻辑状态完整锁定动画期间的普通输入。
- 使用现有 `rig_Parry` 动画建立前摇、有效和后摇三个阶段；默认按 60 FPS 资源的第 8 帧开始判定、第 22 帧结束判定、总长 34 帧执行。
- 弹反有效阶段对所有来袭方向生效，不复用 Guard 扇区判断；成功时完全取消本次 HP 与架势伤害。
- 为被弹反的 Player/NPC 攻击方增加独立 `Parried` 逻辑状态，立即取消当前攻击，复用 `rig_Collide` 动画并在约 1.067 秒内拒绝普通输入或 AI 动作。
- 弹反前摇和后摇中受到攻击时继续执行现有普通 Hit、PostureBreak 或 Dead 结算，不提供格挡兜底。
- 在线模式由 Server 对最新有效弹反阶段和命中重叠作最终裁决，并把 `Parry` 阶段、`Parried` 强制状态和动作边沿同步给所属 Client 与其他观察者。
- 保持弹反与崩防的业务语义分离：两者只共享 `rig_Collide` 动画资源，不共享状态、架势重置、破势 UI 或破势事件。
- 更新项目能力需求、路线/进度和验证矩阵，增加 Offline、Host、Client、Player 与 NPC 的弹反验收用例。

## Capabilities

### New Capabilities

- `parry-system`：定义弹反输入、帧窗口、360 度命中反制、攻击取消、攻击方硬直、动画表现、Server 权威同步和验证要求。

### Modified Capabilities

无。弹反不改变现有 `posture-system` 的架势资源、增长、恢复或破势语义；项目维护文档更新作为本 change 的交付任务处理。

## Impact

- 输入与意图：`DemoInputActions`、`InputHandler`、`CharacterIntent`。
- 状态机：追加稳定的 `Parry`、`Parried` 状态编号、转换原因、Player 状态实现和 NPC 被弹反状态实现。
- 战斗结算：`CombatActor` 在格挡、生命和架势结算前判断弹反窗口，`CombatResolver` 取消攻击并向攻击方发布权威结果。
- 配置与数据：默认弹反采样率、发生帧、结束帧、动画总帧、被弹反持续时间和 CrossFade 参数。
- 表现：`CharacterAnimator.controller`、`AnimatorParams`、`CharacterCombatPresenter` 与 `CharacterLateUpdatePipeline` 增加弹反/被弹反映射；`Parried` 与 `PostureBroken` 分别引用同一个 `rig_Collide` clip。
- 网络：向协议末尾追加弹反阶段和动作类型，更新 Snapshot/Action 的发布、Mirror 转换、Server 过滤、远端锁存与表现重启逻辑。
- 验证：覆盖有效窗边界、前后摇普通受击、360 度来源、Player/NPC 攻击方、攻击实例取消及 Offline/Host/Client 收敛。
- 不增加外部包依赖，不修改既有枚举值的序号。
