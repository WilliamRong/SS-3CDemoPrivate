## ADDED Requirements

### Requirement: 独立窥视测试场景
系统 SHALL 提供 `Assets/Scenes/PeekTest.unity`，场景包含由 Cube 组成的两个相邻房间、共享墙体、可开合门、`PeekPlayer`、`Zombie`、固定侧视相机和必要光源，并且能够脱离网络管理器直接进入 Play Mode。

#### Scenario: 直接运行原型
- **WHEN** 开发者打开 `PeekTest` 并进入 Play Mode
- **THEN** 场景无需启动 Mirror Server 或 Client 即可运行玩家移动、门交互、窥视效果和 Zombie 巡逻

### Requirement: 精简角色 Prefab
系统 SHALL 从现有角色 Prefab 的视觉资源创建独立的 `PeekPlayer` 与 `Zombie` Prefab，并且不得保留 Mirror、战斗、锁定、同步、行为树或 NavMesh 运行时组件。

#### Scenario: 检查原型角色组件
- **WHEN** 开发者检查两个原型 Prefab 的根节点和子节点
- **THEN** Prefab 仅包含模型渲染、Animator、原型碰撞与移动所需组件，不包含现有网络或战斗生命周期组件

### Requirement: 横板玩家移动与软区镜头
系统 SHALL 允许玩家通过键盘水平输入或手柄左摇杆控制 `PeekPlayer` 沿场景 X 轴移动，并 SHALL 使用固定朝向的正交 Cinemachine 镜头跟随角色，且镜头具有明显大于 Dead Zone 的 Soft Zone。

#### Scenario: 玩家在软区内移动
- **WHEN** `PeekPlayer` 在镜头 Soft Zone 内水平移动
- **THEN** 镜头保持固定朝向且不立即追随每个小位移

#### Scenario: 玩家越过软区边缘
- **WHEN** `PeekPlayer` 接近或越过镜头 Soft Zone 边缘
- **THEN** 镜头平滑跟随角色并继续保持横板观察角度

### Requirement: 靠近门自动窥视
系统 SHALL 在 `PeekPlayer` 进入门的触发距离时让门平滑打开至相对关闭姿态 20 度，并从玩家头部向隔壁房间启用锥形窥视；玩家离开触发距离时 SHALL 关闭门并停用清晰锥形。

#### Scenario: 玩家进入门边范围
- **WHEN** `PeekPlayer` 与门的平面距离小于配置阈值
- **THEN** 门向打开姿态平滑旋转并最终达到 20 度，窥视锥形处于激活状态

#### Scenario: 玩家离开门边范围
- **WHEN** 已在窥视的 `PeekPlayer` 离开配置阈值
- **THEN** 门平滑返回关闭姿态，隔壁房间不再存在清晰锥形区域

### Requirement: 隔壁房间模糊与锥形清晰区域
系统 SHALL 在 `PeekTest` 相机中将隔壁房间包围盒内且不在激活锥形中的不透明场景显示为模糊画面，并 SHALL 将激活锥形覆盖的房间像素显示为清晰画面。锥形边缘 SHALL 具有可调柔和过渡。

#### Scenario: 玩家未窥视
- **WHEN** 玩家未进入门的触发距离
- **THEN** 隔壁房间可见的不透明场景保持模糊且没有清晰锥形区域

#### Scenario: 玩家正在窥视
- **WHEN** 玩家靠近门且锥形覆盖隔壁房间的一部分
- **THEN** 锥形内房间像素清晰、锥形外房间像素保持模糊，并且两个区域之间存在柔和过渡

#### Scenario: 现有场景使用普通相机
- **WHEN** 相机没有启用 `PeekVisionController`
- **THEN** 新增 Renderer Feature 不改变该相机的颜色输出

### Requirement: Zombie 逐像素可见性
系统 SHALL 让 `Zombie` 仅在激活窥视锥形覆盖到的模型片元中可见，锥形外不得出现清晰或模糊的 Zombie 轮廓。

#### Scenario: 锥形只扫过 Zombie 下半身
- **WHEN** 激活锥形覆盖 Zombie 的腿部但没有覆盖躯干和头部
- **THEN** 画面只显示锥形内的腿部像素，躯干、头部及锥形外轮廓不可见

#### Scenario: 玩家未窥视
- **WHEN** 窥视锥形未激活
- **THEN** Zombie 在画面中完全不可见，但其巡逻仍继续运行

### Requirement: Zombie 边界巡逻
系统 SHALL 让 `Zombie` 在隔壁房间配置的左右边界之间持续水平移动，到达边界时转身并向反方向继续移动。

#### Scenario: Zombie 到达右边界
- **WHEN** Zombie 的 X 坐标达到或超过右侧巡逻边界
- **THEN** Zombie 的移动方向切换为负 X，模型朝向同步翻转

#### Scenario: Zombie 到达左边界
- **WHEN** Zombie 的 X 坐标达到或低于左侧巡逻边界
- **THEN** Zombie 的移动方向切换为正 X，模型朝向同步翻转
