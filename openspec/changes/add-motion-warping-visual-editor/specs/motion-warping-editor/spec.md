## ADDED Requirements

### Requirement: 编辑器入口与资源选择
项目必须（SHALL）提供 Editor-only 的 Motion Warping 可视化编辑器。编辑器必须（SHALL）允许开发者选择正式 `CharacterCombatConfig`、`CharacterPresentationConfig`、预览角色/Avatar、目标预览对象以及用于 MW 预览的动画资源。

#### Scenario: 打开编辑器并选择处决资源
- **WHEN** 开发者打开 Motion Warping 可视化编辑器并选择处决战斗配置、表现配置、预览角色和 `rig_Execute` / `rig_Executed` / `rig_Executed_Death`
- **THEN** 编辑器显示可编辑配置字段、预览状态和当前资源诊断

#### Scenario: 资源缺失
- **WHEN** 必需配置、预览角色、Animator、Avatar 或动画资源缺失
- **THEN** 编辑器必须显示明确缺失项，并不得把预览标记为有效收敛结果

### Requirement: 隔离预览环境
编辑器预览必须（SHALL）在隔离 `PreviewScene` 或等价临时环境中创建对象。编辑器不得（MUST NOT）要求开发者手动打开测试场景才能进行基本预览，也不得（MUST NOT）永久改变当前 Scene、Prefab 或正式资产中的预览对象。

#### Scenario: 不依赖测试场景预览
- **WHEN** 当前打开任意 Scene 或空 Scene
- **THEN** 编辑器仍可在隔离预览环境中创建处决者和目标预览对象并显示轨迹

#### Scenario: 关闭窗口清理对象
- **WHEN** 开发者关闭编辑器、切换配置、重新加载域或销毁窗口
- **THEN** 编辑器创建的临时预览对象被确定性清理，当前 Scene 层级不留下预览对象

### Requirement: 动画 Root Motion 采样
编辑器必须（SHALL）从所选动画资源生成原始 Root Motion 轨迹和每帧 delta。首选采样路径必须（SHALL）使用 `PlayableGraph` 或等价 Animator 驱动方式，以接近运行时 `Animator.deltaPosition` / `deltaRotation` 语义。

#### Scenario: 采样 rig_Execute
- **WHEN** 开发者选择有效的 `rig_Execute` 和预览 Animator
- **THEN** 编辑器生成原始位置轨迹、Yaw 轨迹、每帧 delta 和 normalized time 样本

#### Scenario: 采样路径不可用
- **WHEN** Playable/Animator 采样无法生成有效 Root Motion delta
- **THEN** 编辑器显示采样失败诊断，并不得静默退化为看似有效的 MW 预测

### Requirement: 共享运行时 Warp 语义
编辑器预览必须（SHALL）与运行时使用同一套 Motion Warping 计算语义。项目不得（MUST NOT）在 Editor 工具中维护与运行时 `ExecutionWarpContext` 漂移的第二套修正公式。

#### Scenario: 同输入输出一致
- **WHEN** 编辑器和运行时使用相同配置、锚点、初始姿态、原始 Root Motion delta 序列和 normalized time
- **THEN** 两者得到一致的修正 delta、预测姿态和位置/Yaw 残差

#### Scenario: 运行时公式变更
- **WHEN** 运行时 MW 计算核心调整 Warp Window、曲线权重或残差定义
- **THEN** 编辑器预览通过共享核心自动使用相同语义，而不需要修改一套独立公式

### Requirement: 轨迹、锚点和残差可视化
编辑器必须（SHALL）可视化目标固定姿态、处决者初始姿态、处决者锚点、原始 Root Motion 轨迹、Warp 后预测轨迹、当前位置/Yaw 残差、Warp Window、结果时刻和预算诊断。

#### Scenario: 查看完整轨迹
- **WHEN** 配置和动画采样有效
- **THEN** Scene View 或等价预览区域显示原始轨迹、Warp 后轨迹、目标姿态、锚点和最终残差

#### Scenario: Scrub 单帧
- **WHEN** 开发者在时间轴上 Scrub 到任意采样时间
- **THEN** 编辑器显示该时刻的原始姿态、Warp 后预测姿态、已累计修正量和剩余位置/Yaw 残差

#### Scenario: 预算超限
- **WHEN** 初始位置/Yaw 误差或预测残差超过配置预算
- **THEN** 编辑器以明确诊断标出超限字段、数值和对应轨迹段

### Requirement: 配置编辑与资产持久化
编辑器必须（SHALL）通过 Unity 序列化系统修改正式配置，支持 Undo/Redo、Dirty/Save 和重新打开后的持久化。编辑器不得（MUST NOT）手工编辑 `.asset` YAML。

#### Scenario: 编辑锚点
- **WHEN** 开发者拖动 Scene View 中的处决者锚点 Handle 或修改锚点字段
- **THEN** `executorAnchorOffset` 通过 `SerializedProperty` 更新，Undo/Redo 可恢复修改前后的值

#### Scenario: 编辑 Warp Window 和曲线
- **WHEN** 开发者调整 Warp Window、`executionWarpCurve`、位置/Yaw 预算或时长字段
- **THEN** 编辑器立即刷新轨迹和残差，并将有效修改记录到所选正式配置资产

#### Scenario: 保存并重新打开
- **WHEN** 开发者保存资产、关闭并重新打开 Unity 或编辑器窗口
- **THEN** 正式配置保留最终序列化值，且不需要手工修改 `.asset` 文件

### Requirement: 非法配置诊断
编辑器必须（SHALL）校验并显示非法配置。非法配置包括但不限于 Warp Window 无序、曲线非单调、非有限数值、预算为负、结果时间超出有效时长、动画资源无效和采样失败。

#### Scenario: Warp Window 无序
- **WHEN** `executionWarpWindowEndNormalized` 小于 `executionWarpWindowStartNormalized`
- **THEN** 编辑器显示窗口无序诊断，并阻止将该预览标记为有效

#### Scenario: 结果时间异常
- **WHEN** `executionResultTime` 接近 0 或超出处决会话有效时长
- **THEN** 编辑器显示结果时刻异常诊断，并提示需要运行时复核

### Requirement: 运行时隔离
Motion Warping 可视化编辑器必须（SHALL）只存在于 Editor 环境。Player 构建或未打开编辑器时，运行时必须（SHALL）仅从正式配置读取数据，并保持现有处决资格、会话、Warp、伤害和死亡行为。

#### Scenario: 构建 Player
- **WHEN** 项目构建 Player
- **THEN** 编辑器窗口、PreviewScene 管理器和 Playable 预览工具不会进入 Player 构建

#### Scenario: 未打开编辑器运行处决
- **WHEN** 未打开 Motion Warping 编辑器时运行 Offline 或 Host/Client 处决
- **THEN** 运行时行为与实现编辑器前保持一致，只受正式配置资产数值影响

### Requirement: 运行时对照验收
编辑器必须（SHALL）支持或记录与真实 Offline/Online 场景的对照验收结果。预览通过不得（MUST NOT）自动等同于运行时通过。

#### Scenario: 预览通过但未运行时验证
- **WHEN** 编辑器预测轨迹收敛但尚未运行 Offline 或 Host/Client 验收
- **THEN** 编辑器或文档状态必须区分 `Preview Valid` 与 `Runtime Verified`

#### Scenario: 运行时残差回填
- **WHEN** 开发者使用 Offline 校准 harness 或运行时验收取得实际残差
- **THEN** 项目文档或诊断记录可以把实际残差与编辑器预测值并列展示
