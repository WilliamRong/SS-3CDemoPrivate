# Turn90 当前进度记录 - 2026-07-20

## Turn90 接入状态

- Player idle / guard 转身已接入并由用户实机验证通过。
- NPC idle / guard 转身已接入并由用户实机验证通过。
- 转身由代码旋转驱动，不交给 root motion；Turn90 动画只负责表现。
- 转身参数已放在 `CharacterPresentationConfig`，不放在 `CombatConfig`。
- 当前设计允许一定落后，用 `turnCooldown` 控制连续转身节奏，用 `turnStepAngle` 控制单步转身幅度。

## Animator / 动画资产

- Player idle 使用新生成的放下刀版本：
  - `rig_Idle2_Turn_90L.anim`
  - `rig_Idle2_Turn_90R.anim`
- Guard 使用已有：
  - `rig_guard_turn_90L`
  - `rig_guard_turn_90R`
- Animator 已接入：
  - Locomotion layer: `IdleTurnLeft` / `IdleTurnRight`
  - Combat layer: `GuardTurnLeft` / `GuardTurnRight`
  - `TurnSpeed` 控制播放倍率

## NPC / GM 调试

- NPC 可通过 AI intent 面向 BehaviorDesigner `target` 或 `destination`。
- GM 面板新增：
  - NPC 锁最近玩家 / NPC 解锁玩家
  - 所有角色防御 / 所有角色解除防御
- “所有角色防御”按用户要求恢复为：除发起者以外的所有角色。
- GM 防御消息使用 `ExcludedPlayerNetId` 排除发起者。

## 重构结果

- 已新增 `CharacterTurnPlanner`，统一以下转身计算：
  - facing delta
  - trigger angle
  - step angle
  - target rotation
  - duration
  - yaw speed
  - align tolerance
  - cooldown
  - `TurnSpeed` multiplier
- `CharacterTurnPresenter` 已改为复用 `CharacterTurnPlanner.CalculateSpeed`。
- Player/NPC 的 idle/guard state 只保留各自 phase、输入打断、状态切换逻辑。
- 重复的转身参数 getter 和计算函数已删除。

## 验证

- `dotnet build Assembly-CSharp.csproj --no-restore` 通过。
- `git diff --check` 通过。
- 只有项目已有 warning，没有新增编译错误。

## Git / UGit 状态

- 最新提交：`b73e4be 重构代码`
- 当前 `main` 与 `origin/main` 已对齐。
- 之前 UGit UI 显示“一直推送中”，但命令行检查显示最新提交已推到远端：
  - `git status --short --branch` 显示 `## main...origin/main`
  - `git log --oneline --decorate --max-count=3` 显示 `b73e4be (HEAD -> main, origin/main, origin/HEAD) 重构代码`
- 如果 UGit UI 仍显示卡住，可以重启 UGit；仓库状态本身是干净且已推送的。
