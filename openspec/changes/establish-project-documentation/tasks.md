## 1. 建立文档基线

- [x] 1.1 确认基线日期、Git 提交、Unity/包版本、第一方目录树、场景、预制体、数据资产和运行时入口。
- [x] 1.2 根据当前代码、资产、Git 历史和已有人工验证，协调 Phase A-H 与七月状态声明，并将每项归类为 `Implemented`、`Verified`、`Partial`、`Deferred` 或 `Not Started`。
- [x] 1.3 在不修改运行时代码的前提下，记录 G0 战斗权威、HP revision/纠正、格挡伤害传播、健康 UI、锁定和方向受击的实现证据、验证缺口及剩余不对称。

## 2. 创建规范项目文档

- [x] 2.1 创建 `docs/PROJECT_OVERVIEW.md`，覆盖产品范围、技术栈、第一方结构、场景/资产、职责边界和控制/战斗/同步/表现/UI 数据流。
- [x] 2.2 创建 `docs/REQUIREMENTS.md`，按角色控制、战斗、锁定与相机、网络、NPC AI、表现与 UI、内容生产组织需求，并注明相关阶段。
- [x] 2.3 创建 `docs/ROADMAP_AND_PROGRESS.md`，包含状态定义、Phase A-H 映射、G0 子阶段状态、基线提交/日期、证据、延期工作、已知缺口和后续优先级。
- [x] 2.4 创建 `docs/VERIFICATION_MATRIX.md`，覆盖 Offline、Host、Client、本地/远端 Player 和 NPC，用明确开放状态保留未验证项。

## 3. 集成并维护文档

- [x] 3.1 更新 `README.md` 的文档索引和过时结构/功能摘要，不复制规范文档全文。
- [x] 3.2 增加事实源和维护规则，将 `AIContext` 视为不修改的历史材料，并要求后续改变状态的工作同步更新规范文档。
- [x] 3.3 交叉链接能力需求、阶段进度和验证用例，使读者能从预期行为导航到实现状态与证据。
- [x] 3.4 在 `openspec/config.yaml` 增加简体中文约束，并把所有现有 OpenSpec artifacts 的自然语言内容转换为中文。

## 4. 验证文档 change

- [x] 4.1 审计每项重要进度声明的代码/资产路径、提交或带日期验证记录，并移除没有证据的 `Verified` 声明。
- [x] 4.2 检查 README 与 `docs/` 的所有仓库相对链接，并确认四份规范文档均可独立阅读。
- [x] 4.3 运行 `openspec validate establish-project-documentation`，解决 proposal、design、spec 和 tasks 校验错误。
- [x] 4.4 确认文档/OpenSpec 编辑没有引入额外运行时或序列化资产变更；共享工作树中已有的实现工作保持独立可追溯。
