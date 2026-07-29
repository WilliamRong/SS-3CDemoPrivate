## Why

仓库中的项目知识分散在高层 README、大量历史 AI 对话导出和多份已经落后于代码的状态记录中。现在需要建立一套持续维护的文档基线，让读者无需从 `AIContext` 或提交历史重建上下文，就能理解项目结构、需求、实际进度、验证缺口和后续工作。

## What Changes

- 建立简洁的项目总览，覆盖产品定位、技术栈、第一方目录职责、运行时架构、场景、数据资产和关键数据流。
- 建立按能力域组织的需求文档，覆盖角色控制、战斗、锁定与相机、网络、NPC AI、表现与 UI、内容生产。
- 建立路线与进度文档，将能力视图映射到 Phase A-H，并区分 `Implemented`、`Verified`、`Partial`、`Deferred` 和 `Not Started`。
- 建立 Offline/Host/Client/NPC 验证矩阵，区分实现证据与运行时验收，覆盖权威 HP/revision、格挡伤害、相机朝向锁定和共享世界血条可见性。
- 定义文档事实源与维护规则：当前代码和已验证行为具有权威性，维护文档负责汇总，`AIContext` 只保留历史参考身份。
- 刷新 README 的文档入口以及过时的目录和功能描述。
- 约束所有 OpenSpec 文档的自然语言内容使用简体中文，并将已有英文 artifacts 转换为中文。

## Capabilities

### New Capabilities

- `project-documentation`：定义规范项目文档、内容范围、事实源规则、进度词汇、交叉链接、中文语言约束和维护要求。

### Modified Capabilities

无。仓库当前没有已归档的主能力规格，本 change 不修改游戏运行时行为。

## Impact

- 在 `docs/` 下增加维护文档，并从 `README.md` 提供入口。
- 增加项目文档的 OpenSpec 需求基线和仓库级中文文档规则。
- 使用 `AIContext`、Git 历史、当前 Unity 资产和第一方 C# 代码作为研究输入，但不改写历史记录。
- 不修改应用代码、序列化玩法行为、公开 API、Unity 包、场景、预制体或运行时依赖。
