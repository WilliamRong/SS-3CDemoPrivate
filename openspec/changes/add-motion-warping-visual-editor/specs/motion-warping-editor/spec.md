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
编辑器预览必须（SHALL）在专用 `MotionWarpingVisualToolPreview` 场景中创建对象。窗口必须（SHALL）提供一键场景跳转，并在跳转前使用 Unity 标准流程处理当前未保存 Scene；专用场景必须以 `Single` 模式打开，不得（MUST NOT）在后台 Additive 加载。预览对象必须（SHALL）为临时对象，且不得（MUST NOT）永久改变其他 Scene、Prefab 或正式资产。

#### Scenario: 一键进入专用编辑器场景
- **WHEN** 当前打开的不是专用预览场景，开发者点击窗口中的场景跳转按钮
- **THEN** Unity 先提示保存或放弃当前未保存修改，再以 `Single` 模式打开 `MotionWarpingVisualToolPreview`，且没有后台 Additive 场景

#### Scenario: 非专用场景不创建预览对象
- **WHEN** 当前打开的不是专用预览场景
- **THEN** 编辑器显示跳转提示，不创建处决者、目标或辅助预览对象

#### Scenario: 默认预览站位满足处决空间条件
- **WHEN** 专用预览场景使用有效 `CharacterCombatConfig` 创建处决者与目标
- **THEN** 编辑器根据共享 `ExecutionSpatialValidator` 和 `executorAnchorOffset` 将两者默认设置为面对面近距离站位，且默认位置/Yaw 误差处于正式配置预算内

#### Scenario: 编辑预览初始站位
- **WHEN** 开发者修改处决者或被处决者的预览初始位置与旋转
- **THEN** 编辑器立即更新临时角色姿态、共享空间资格、锚点和 Warp 轨迹，且不得自动修改正式 Combat 配置

#### Scenario: 关闭窗口清理对象
- **WHEN** 开发者关闭编辑器、切换配置、重新加载域或销毁窗口
- **THEN** 编辑器创建的临时预览对象被确定性清理，专用场景保存后也不包含这些预览对象

### Requirement: 动画 Root Motion 采样
编辑器必须（SHALL）从处决者和目标当前分支动画生成各自的原始 Root Motion 轨迹和每帧 delta。首选采样路径必须（SHALL）使用 `PlayableGraph` 或等价 Animator 驱动方式，以接近运行时 `Animator.deltaPosition` / `deltaRotation` 语义。

#### Scenario: 采样 rig_Execute
- **WHEN** 开发者选择有效的 `rig_Execute` 和预览 Animator
- **THEN** 编辑器生成原始位置轨迹、Yaw 轨迹、每帧 delta 和 normalized time 样本

#### Scenario: 采样目标分支
- **WHEN** 开发者选择有效的 `rig_Executed` 或 `rig_Executed_Death` 和目标预览 Animator
- **THEN** 编辑器生成目标分支原始位置/Yaw 轨迹与每帧 delta，并用目标分支运行时 normalized time 计算 Warp 后轨迹

#### Scenario: 采样路径不可用
- **WHEN** Playable/Animator 采样无法生成有效 Root Motion delta
- **THEN** 编辑器显示采样失败诊断，并不得静默退化为看似有效的 MW 预测

### Requirement: 共享运行时 Warp 语义
编辑器预览必须（SHALL）与运行时使用同一套 Motion Warping 计算语义。项目不得（MUST NOT）在 Editor 工具中维护与运行时 `ExecutionWarpContext` 漂移的第二套修正公式。

#### Scenario: 同输入输出一致
- **WHEN** 编辑器和运行时使用相同配置、锚点、初始姿态、原始 Root Motion delta 序列和 normalized time
- **THEN** 两者得到一致的修正 delta、预测姿态和位置/Yaw 残差

#### Scenario: 双参与者共用核心
- **WHEN** Executor 与 Target 分别提供自己的锚点、通用 Warp Settings、原始 delta 和 normalized time
- **THEN** 双方必须通过同一个 Solver/Context 实现计算，不得为 Target 复制另一套修正公式

#### Scenario: 运行时公式变更
- **WHEN** 运行时 MW 计算核心调整 Warp Window、曲线权重或残差定义
- **THEN** 编辑器预览通过共享核心自动使用相同语义，而不需要修改一套独立公式

### Requirement: 轨迹、锚点和残差可视化
编辑器必须（SHALL）在窗口顶部固定显示时间轴，并可视化双方初始姿态、双方锚点、双方原始 Root Motion 轨迹、Warp 后预测轨迹、当前位置/Yaw 残差、Warp Window、结果时刻和预算诊断。

#### Scenario: 查看完整轨迹
- **WHEN** 配置和动画采样有效
- **THEN** Scene View 或等价预览区域显示双方原始轨迹、双方 Warp 后轨迹、双方锚点和最终残差

#### Scenario: Scrub 单帧
- **WHEN** 开发者在时间轴上 Scrub 到任意采样时间
- **THEN** 编辑器显示双方在该时刻的原始姿态、Warp 后预测姿态、已累计修正量和剩余位置/Yaw 残差

#### Scenario: 在 Edit Mode 运行动作
- **WHEN** 资源与 Root Motion 采样有效且开发者点击“运行动作”
- **THEN** 编辑器从时间轴起点推进绝对秒数，通过手动 `PlayableGraph` 以 `elapsed / executingDuration` 播放并 Warp 处决者，以 `elapsed / executedDuration` 或 `elapsed / executedDeathDuration` 播放并 Warp 目标当前分支；较短分支保持末帧，直到两者较长时长结束，且整个过程不得进入 Play Mode

#### Scenario: 不同时长动画同步 Scrub
- **WHEN** 处决者与目标当前分支的运行时配置时长不同，开发者拖动固定时间轴
- **THEN** 编辑器显示同一绝对时刻下分别换算的 Executor/Target normalized time，双方 Warp 轨迹各自使用对应 normalized time，不得把目标动画压缩到处决者时长

#### Scenario: 预算超限
- **WHEN** 初始位置/Yaw 误差或预测残差超过配置预算
- **THEN** 编辑器以明确诊断标出超限字段、数值和对应轨迹段

### Requirement: 配置编辑与资产持久化
编辑器必须（SHALL）通过 Unity 序列化系统修改正式配置，支持 Undo/Redo、Dirty/Save 和重新打开后的持久化。编辑器不得（MUST NOT）手工编辑 `.asset` YAML。

#### Scenario: 编辑锚点
- **WHEN** 开发者拖动 Scene View 中的处决者锚点 Handle 或修改锚点字段
- **THEN** `executorAnchorOffset` 通过 `SerializedProperty` 更新，Undo/Redo 可恢复修改前后的值

#### Scenario: 编辑 Warp Window 和曲线
- **WHEN** 开发者调整任一参与者的 Warp Window、曲线、位置/Yaw 预算、分支锚点或时长字段
- **THEN** 编辑器立即刷新对应轨迹和残差，并将有效修改记录到所选正式配置资产

#### Scenario: 编辑目标锚点
- **WHEN** 开发者切换目标生还/死亡分支并拖动 Scene View 中的 Target Anchor Handle
- **THEN** 编辑器通过 `SerializedProperty` 更新该分支局部锚点，Undo/Redo 可恢复修改前后的值，另一目标分支锚点不被覆盖

#### Scenario: 保存并重新打开
- **WHEN** 开发者保存资产、关闭并重新打开 Unity 或编辑器窗口
- **THEN** 正式配置保留最终序列化值，且不需要手工修改 `.asset` 文件

#### Scenario: 保存并复用预览初始站位
- **WHEN** 开发者新建或显式覆盖保存包含处决者与被处决者位置/旋转的预览站位资产，随后重新加载该资产
- **THEN** 编辑器通过 Unity 序列化系统恢复两名角色的初始 Transform，支持 Undo/Dirty，并且该 Editor-only 资产不进入 Player 构建或运行时处决配置

### Requirement: 非法配置诊断
编辑器必须（SHALL）校验并显示非法配置。非法配置包括但不限于 Warp Window 无序、曲线非单调、非有限数值、预算为负、结果时间超出有效时长、动画资源无效和采样失败。

#### Scenario: Warp Window 无序
- **WHEN** `executionWarpWindowEndNormalized` 小于 `executionWarpWindowStartNormalized`
- **THEN** 编辑器显示窗口无序诊断，并阻止将该预览标记为有效

#### Scenario: 结果时间异常
- **WHEN** `executionResultTime` 接近 0 或超出处决会话有效时长
- **THEN** 编辑器显示结果时刻异常诊断，并提示需要运行时复核

### Requirement: 运行时隔离
Motion Warping 可视化编辑器必须（SHALL）只存在于 Editor 环境。Player 构建或未打开编辑器时，运行时必须（SHALL）仅从正式配置读取双方 Warp 数据；编辑器预览代码不得进入 Player 构建。

#### Scenario: 构建 Player
- **WHEN** 项目构建 Player
- **THEN** 编辑器窗口、PreviewScene 管理器和 Playable 预览工具不会进入 Player 构建

#### Scenario: 未打开编辑器运行处决
- **WHEN** 未打开 Motion Warping 编辑器时运行 Offline 或 Host/Client 处决
- **THEN** 处决者与目标仍通过通用运行时 Warp 核心消费正式配置，且不依赖任何 Editor 状态

### Requirement: 被处决者权威 Warp
被处决者必须（SHALL）保留当前目标动画的 Root Motion，并在 Server/Host 通过与处决者相同的通用 Warp Solver 向当前分支目标锚点收敛。运行时不得（MUST NOT）在每帧把目标重置到初始 `FixedTargetPose`。

#### Scenario: 生还目标被击飞
- **WHEN** 权威端目标进入 `Executed` 生还分支并产生 Animator Root Motion
- **THEN** 目标通过 CharacterController 或 NavMeshAgent 消费 Warp 后 delta，向生还分支锚点移动并记录实际残差

#### Scenario: 死亡目标被击飞
- **WHEN** 权威端目标进入 `Executed` 死亡分支并产生 Animator Root Motion
- **THEN** 目标使用死亡分支锚点与相同 Solver 消费 Warp 后 delta，完成后保持死亡动画最终位置

#### Scenario: 非权威客户端
- **WHEN** Client 观察远端处决者与目标
- **THEN** Client 不重复消费双方位移 Root Motion，而是播放骨骼动画并跟随权威 Transform 快照；状态恢复使用双方当前权威姿态而非目标初始姿态

### Requirement: 运行时对照验收
编辑器必须（SHALL）支持或记录与真实 Offline/Online 场景的对照验收结果。预览通过不得（MUST NOT）自动等同于运行时通过。

#### Scenario: 预览通过但未运行时验证
- **WHEN** 编辑器预测轨迹收敛但尚未运行 Offline 或 Host/Client 验收
- **THEN** 编辑器或文档状态必须区分 `Preview Valid` 与 `Runtime Verified`

#### Scenario: 运行时残差回填
- **WHEN** 开发者使用 Offline 校准 harness 或运行时验收取得实际残差
- **THEN** 项目文档或诊断记录可以把实际残差与编辑器预测值并列展示
