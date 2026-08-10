## ADDED Requirements

### Requirement: 被弹反状态提供处决资格
处于 `Parried` 的存活 Player/NPC 必须（SHALL）在该状态存续期间提供处决资格，并允许已被权威接受的 `Executed` 抢占其自然超时；处决资格本身不得（MUST NOT）改变既有架势、破势 UI 或普通被弹反语义。

#### Scenario: 被弹反期间接受处决
- **WHEN** 合法 Player 在 `Parried` 目标正面、配置距离内按下左键且权威校验通过
- **THEN** 目标退出 `Parried` 并进入与该 Player 绑定的 `Executed`，原 `Parried` 超时不再把目标送回 Idle

#### Scenario: 没有发起处决
- **WHEN** `Parried` 持续到既有配置时长结束且没有权威处决会话
- **THEN** 目标继续按现有规则退出到 Idle，架势和被弹反表现语义不变

#### Scenario: 处决请求在超时后到达
- **WHEN** 权威端处理请求时目标已经退出 `Parried`
- **THEN** 请求被拒绝，目标不会重新进入 Parried 或 Executed

#### Scenario: 被弹反与崩防保持隔离
- **WHEN** `Parried` 目标进入 `Executed`
- **THEN** 系统不发布 `PostureBreak`、不重置架势且不显示破势专用 UI
