## ADDED Requirements

### Requirement: 规范文档集
仓库必须（SHALL）在 `docs/` 下提供规范的项目总览、能力需求、路线与进度以及验证矩阵文档。

#### Scenario: 读者从仓库入口进入
- **WHEN** 读者打开仓库 README
- **THEN** 读者可以通过直接相对链接进入每一份规范文档

#### Scenario: 读者需要当前项目上下文
- **WHEN** 读者只使用 README 和规范文档
- **THEN** 读者无需阅读 `AIContext` 即可理解项目范围、架构、需求、进度、验证缺口和后续工作

### Requirement: 项目总览覆盖范围
项目总览必须（SHALL）描述产品定位、Unity 与主要依赖版本、第一方目录职责、重要场景和资产、运行时架构，以及控制、战斗、同步、表现和 UI 的主要数据流。

#### Scenario: 仓库包含第三方代码
- **WHEN** 总览描述仓库结构
- **THEN** 它区分第一方项目模块与内嵌或本地安装的第三方依赖

#### Scenario: 评审架构
- **WHEN** 读者沿运行时数据流阅读
- **THEN** 总览明确 Intent、Controller/Driver、FSM、Motor、Combat、Sync、Presenter 和 UI 之间的职责边界

### Requirement: 按能力域组织需求
需求文档必须（SHALL）以稳定能力域组织预期行为，而不是把 Phase A-H 作为主要层级。

#### Scenario: 分类需求
- **WHEN** 记录当前项目需求
- **THEN** 角色控制、战斗、锁定与相机、网络、NPC AI、表现与 UI、内容生产分别成为可导航的能力域

#### Scenario: 需要交付历史
- **WHEN** 某项需求关联历史 Phase A-H 里程碑
- **THEN** 该需求链接或注明对应进度阶段，但不改变其能力归属

### Requirement: 明确进度语义
进度文档必须（SHALL）只使用 `Implemented`、`Verified`、`Partial`、`Deferred` 和 `Not Started`，并定义每种状态所代表的证据。

#### Scenario: 代码存在但没有运行证据
- **WHEN** 代码或资产已经实现功能，但没有相关验收记录
- **THEN** 该功能标为 `Implemented` 而不是 `Verified`

#### Scenario: 验收面不完整
- **WHEN** 功能只在部分环境或角色类型工作
- **THEN** 该功能标为 `Partial`，并明确缺失环境或行为

#### Scenario: 工作有意延期
- **WHEN** E0 行为树演示等功能被明确排除在当前完成门槛之外
- **THEN** 该功能标为 `Deferred`，而不是已完成或未开始

### Requirement: 当前状态声明可追溯
每项重要进度声明必须（SHALL）可以追溯到当前代码/资产路径、Git 提交或带日期的验证记录，且进度文档必须注明基线日期和提交。

#### Scenario: 历史文档与代码冲突
- **WHEN** `AIContext` 状态声明与更新的实现证据冲突
- **THEN** 规范进度文档报告较新的状态，并在必要时注明历史记录已过时

#### Scenario: 报告验证结果
- **WHEN** 某一行标为 `Verified`
- **THEN** 文档注明测试环境和观察到的验收结果

### Requirement: 验证矩阵
验证矩阵必须（SHALL）在行为受角色或环境影响时区分 Offline、Host、Client、本地 Player、远端 Player 和 NPC 验收面。

#### Scenario: 评估多人健康状态
- **WHEN** 文档记录健康和伤害行为
- **THEN** 它分别评估 Player/NPC HP 表现、即时权威结果、revision 过滤和周期纠正

#### Scenario: 评估战斗反应
- **WHEN** 文档记录 Hit、Dead、GuardHit、GuardBreak 或方向受击
- **THEN** 本地结算与远端表现作为不同验证用例表示

### Requirement: 当前状态与已知缺口维护
规范文档必须（MUST）保留真实限制，更新已经由当前实现解决的旧限制，并且不得把代码存在或本地 UI 完成冒充为完整多人验收。

#### Scenario: 已记录缺口获得实现
- **WHEN** 当前代码增加远端 NPC HP 纠正、实际格挡伤害、相机朝向锁定或共享世界血条行为
- **THEN** 规范文档描述已实现路径，并在取得环境证据前保持验证项开放

#### Scenario: 相似角色路径仍不对称
- **WHEN** NPC 快照携带周期权威 HP 纠正而 Player 快照没有
- **THEN** 文档保留 Player 限制，而不是把 NPC 实现泛化到所有角色

### Requirement: 历史上下文边界
文档必须（SHALL）把 `AIContext` 标识为历史研究材料，并在本 change 中保持其原始记录不变。

#### Scenario: 查阅历史意图
- **WHEN** 使用 `AIContext` 恢复决策或阶段边界
- **THEN** 形成的规范陈述与更新的代码、Git 历史和验证证据交叉核对

### Requirement: OpenSpec 中文文档
仓库中的 OpenSpec proposal、design、spec、tasks 和归档文档，其标题与自然语言内容必须（MUST）使用简体中文。

#### Scenario: 创建或更新 artifact
- **WHEN** 代理创建或更新任意 OpenSpec 文档
- **THEN** 自然语言标题、说明、需求名称、场景内容和任务描述均使用简体中文

#### Scenario: 固定语法与技术标识
- **WHEN** OpenSpec 校验器要求固定英文标记，或内容引用代码标识符、路径、命令和协议字段
- **THEN** 仅这些不可翻译部分保留原文，其余内容仍使用简体中文

#### Scenario: 发现历史英文文档
- **WHEN** 维护工作触及包含英文自然语言的现有 OpenSpec artifact
- **THEN** 在不改变技术含义和任务状态的前提下将其转换为简体中文

### Requirement: 文档维护
规范文档必须（SHALL）包含足够的所有权或维护指导，使架构、进度和验证声明可以随未来 change 保持同步。

#### Scenario: 后续功能改变状态
- **WHEN** 后续实现改变已记录能力或验收结果
- **THEN** 同一个 change 按需更新需求、进度行和验证证据

#### Scenario: 审计文档链接
- **WHEN** 文档基线完成
- **THEN** README 与规范文档之间的所有仓库相对链接都指向现有文件
