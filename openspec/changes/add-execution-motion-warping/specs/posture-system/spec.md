## ADDED Requirements

### Requirement: 崩防状态提供处决资格
处于 `PostureBroken` 的存活 Player/NPC 必须（SHALL）在该状态存续期间提供处决资格，并允许已被权威接受的 `Executed` 抢占其自然超时和后续普通受击反应；在处决被接受之前，既有权威 HP 伤害和死亡优先级必须（SHALL）保持不变。

#### Scenario: 崩防期间接受处决
- **WHEN** 合法 Player 在 `PostureBroken` 目标正面、配置距离内按下左键且权威校验通过
- **THEN** 目标退出 `PostureBroken` 并进入与该 Player 绑定的 `Executed`，原崩防超时不再把目标送回 Idle/Move

#### Scenario: 崩防目标先被普通攻击杀死
- **WHEN** 处决会话接受前的权威普通攻击把 `PostureBroken` 目标 HP 降到零
- **THEN** `Dead` 优先，后续处决请求被拒绝

#### Scenario: 处决接受后出现普通命中
- **WHEN** 目标已经从 `PostureBroken` 进入 `Executed` 后普通 HitBox 与其重叠
- **THEN** 普通命中被处决战斗抑制拒绝，不产生额外 HP、架势、Hit 或 PostureBreak 结果

#### Scenario: 没有发起处决
- **WHEN** `PostureBroken` 持续到既有配置时长结束且没有权威处决会话
- **THEN** 目标继续按现有规则退出，架势保持零且既有恢复流程不变

#### Scenario: 处决请求在崩防超时后到达
- **WHEN** 权威端处理请求时目标已经退出 `PostureBroken`
- **THEN** 请求被拒绝，目标不会重新进入 PostureBroken 或 Executed
