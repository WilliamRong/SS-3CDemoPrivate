# Turn90L/R 动画接入实现计划

## 📋 需求
- 在Idle/Guard状态下，锁定目标移动时触发转身动画
- 根据转身角度调整动画步幅/播放速度
- 网络同步

## 🎯 设计方案

### 方案：Phase扩展（参考Sprint Turn180）
- IdleState增加 TurnLeft/TurnRight 阶段
- GuardState增加 TurnLeft/TurnRight 阶段
- 不新增StateId，保持状态机简洁

## 📐 实现步骤

### Phase 1: 数据层 (2-3h)

#### 1.1 枚举扩展
**文件**: `IdleState.cs`, `GuardState.cs`
```csharp
// IdleState新增
public enum IdlePhase : byte {
    None = 0, Normal = 1,
    TurnLeft = 2, TurnRight = 3
}

// GuardState新增
TurnLeft = 4, TurnRight = 5  // 在现有基础上
```

#### 1.2 配置扩展
**文件**: `CharacterCombatConfig.cs` 或新建 `TurnConfig.cs`
```csharp
[Header("Turn")]
public float turnTriggerAngle = 30f;        // 触发角度阈值
public float turnAnimationAngle = 90f;      // 动画设计角度
public float turnSpeedMin = 0.6f;           // 最小播放速度
public float turnSpeedMax = 1.2f;           // 最大播放速度  
public float turnCooldown = 0.5f;           // 冷却时间
public AnimationClip idleTurnLeftClip;      // 动画引用
public AnimationClip idleTurnRightClip;
public AnimationClip guardTurnLeftClip;
public AnimationClip guardTurnRightClip;
```

#### 1.3 Snapshot扩展
**文件**: `StateSnapshot.cs`
```csharp
public byte IdlePhase;       // 新增
// GuardPhase 可能已有，确认范围能容纳新值
```

---

### Phase 2: 逻辑层 (3-4h)

#### 2.1 IdleState实现
**文件**: `IdleState.cs`

**新增字段**:
```csharp
private IdlePhase _currentPhase = IdlePhase.Normal;
private float _phaseTimer;
private float _turnCooldown;
private float _targetTurnAngle;  // 记录目标转向角度
```

**新增方法**:
```csharp
private bool ShouldTurnToTarget(out bool turnLeft, out float angleDelta)
{
    // 条件：锁定中 + Idle不移动 + 角度差超阈值 + 冷却完成
    var lockOn = GetComponent<PlayerLockOnController>();
    if (!lockOn.IsLockOnActive) return false;
    if (intent.Move.sqrMagnitude > 0.01f) return false;
    if (_turnCooldown > 0f) return false;
    
    Vector3 toTarget = lockOn.CurrentTarget.position - transform.position;
    float angle = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);
    
    angleDelta = Mathf.Abs(angle);
    turnLeft = angle < 0;
    
    return angleDelta >= _combatConfig.turnTriggerAngle;
}

private void BeginTurn(bool turnLeft, float angleDelta)
{
    _currentPhase = turnLeft ? IdlePhase.TurnLeft : IdlePhase.TurnRight;
    _targetTurnAngle = angleDelta;
    _phaseTimer = 0f;
}

private void TickTurnPhase(CharacterIntent intent, float deltaTime)
{
    // 阻止其他输入
    intent.IsAttackPressed = false;
    intent.IsDodgePressed = false;
    intent.IsGuardHeld = false;
    
    _phaseTimer += deltaTime;
    
    // 检查转身完成条件
    float duration = CalculateTurnDuration(_targetTurnAngle);
    if (_phaseTimer >= duration)
    {
        _currentPhase = IdlePhase.Normal;
        _turnCooldown = _combatConfig.turnCooldown;
    }
}

private float CalculateTurnDuration(float angleDelta)
{
    // 根据角度计算持续时间（反比于播放速度）
    float normalizedAngle = angleDelta / _combatConfig.turnAnimationAngle;
    float speed = Mathf.Lerp(
        _combatConfig.turnSpeedMax,
        _combatConfig.turnSpeedMin,
        normalizedAngle
    );
    
    AnimationClip clip = _currentPhase == IdlePhase.TurnLeft
        ? _combatConfig.idleTurnLeftClip
        : _combatConfig.idleTurnRightClip;
    
    return clip.length / speed;
}
```

**修改Tick逻辑**:
```csharp
public void Tick(CharacterIntent intent, float deltaTime)
{
    _turnCooldown -= deltaTime;
    
    // Phase路由
    if (_currentPhase == IdlePhase.TurnLeft || _currentPhase == IdlePhase.TurnRight)
    {
        TickTurnPhase(intent, deltaTime);
        return;
    }
    
    // 检查转身触发
    if (ShouldTurnToTarget(out bool turnLeft, out float angleDelta))
    {
        BeginTurn(turnLeft, angleDelta);
        return;
    }
    
    // 原有Idle逻辑...
}
```

#### 2.2 GuardState实现
**文件**: `GuardState.cs`
- 类似IdleState，在Loop阶段检查转身条件
- 转身时保持Guard状态（不取消防御）
- 转身完成后返回Loop阶段

---

### Phase 3: 表现层 (2-3h)

#### 3.1 Animator参数
**文件**: `AnimatorParams.cs`
```csharp
public static readonly int StateIdleTurnLeft = Animator.StringToHash("IdleTurnLeft");
public static readonly int StateIdleTurnRight = Animator.StringToHash("IdleTurnRight");
public static readonly int StateGuardTurnLeft = Animator.StringToHash("GuardTurnLeft");
public static readonly int StateGuardTurnRight = Animator.StringToHash("GuardTurnRight");
public static readonly int TurnSpeed = Animator.StringToHash("TurnSpeed");
```

#### 3.2 Presenter实现
**新建文件**: `CharacterTurnPresenter.cs`
```csharp
public class CharacterTurnPresenter
{
    private readonly CharacterCombatConfig _config;
    
    public void TickIdleTurn(
        Animator animator, 
        IdleState.IdlePhase phase,
        float targetAngle)
    {
        if (phase == IdlePhase.TurnLeft)
        {
            float speed = CalculateTurnSpeed(targetAngle);
            animator.SetFloat(AnimatorParams.TurnSpeed, speed);
            animator.CrossFade(AnimatorParams.StateIdleTurnLeft, 0.1f, 0);
        }
        else if (phase == IdlePhase.TurnRight)
        {
            float speed = CalculateTurnSpeed(targetAngle);
            animator.SetFloat(AnimatorParams.TurnSpeed, speed);
            animator.CrossFade(AnimatorParams.StateIdleTurnRight, 0.1f, 0);
        }
    }
    
    private float CalculateTurnSpeed(float angleDelta)
    {
        float normalized = angleDelta / _config.turnAnimationAngle;
        return Mathf.Lerp(_config.turnSpeedMax, _config.turnSpeedMin, normalized);
    }
}
```

#### 3.3 接入Pipeline
**文件**: `CharacterLateUpdatePipeline.cs`
- 在Update中增加Turn Presenter调用
- 优先级：Combat > Turn > Guard > Sprint > Locomotion

---

### Phase 4: Animator配置 (1-2h)

#### 4.1 Animator Controller
1. 在Layer 0或新建TurnLayer
2. 添加4个状态：IdleTurnLeft/Right, GuardTurnLeft/Right
3. 从Idle/GuardWalk连接到Turn状态（Any State或直接连线）
4. 设置Transition：
   - Has Exit Time: false
   - Duration: 0.1s
5. 每个Turn状态设置：
   - Motion: 对应AnimationClip
   - Speed: 由Parameter "TurnSpeed" 控制
   - Speed Parameter: TurnSpeed

#### 4.2 Root Motion处理
- 确保Turn动画启用Root Motion（Rotation）
- Bake Into Pose: Rotation ✓
- Motor在Turn阶段应该让出旋转控制权

---

### Phase 5: Motor集成 (1-2h)

#### 5.1 Motor修改
**文件**: `CharacterMotor.cs`

```csharp
private bool _turnRotationOverride;  // 新增字段

public void SetTurnRotationOverride(bool enable)
{
    _turnRotationOverride = enable;
}

// 在旋转逻辑中检查
private void ApplyRotation(...)
{
    if (_turnRotationOverride)
        return;  // Turn动画接管旋转
    
    // 原有旋转逻辑...
}
```

#### 5.2 State调用Motor
```csharp
// IdleState.TickTurnPhase
_motor.SetTurnRotationOverride(true);
_motor.SetMovementBlocked(true);  // 阻止移动

// 转身完成后
_motor.SetTurnRotationOverride(false);
_motor.SetMovementBlocked(false);
```

---

### Phase 6: 网络同步 (2h)

#### 6.1 Snapshot序列化
**文件**: `LocalSyncPublisher.cs`
```csharp
snapshot.IdlePhase = (byte)idleState.CurrentPhase;
snapshot.GuardPhase = (byte)guardState.CurrentPhase;
```

#### 6.2 远端应用
**文件**: `RemoteInterpolator.cs` 或 Presenter
- 远端根据IdlePhase/GuardPhase播放对应动画
- 不需要插值旋转，直接播放动画
- 目标角度可以从快照计算或传递Param

---

### Phase 7: NPC支持 (1-2h)

#### 7.1 NPC状态机
- NPC如果复用IdleState/GuardState，自动获得Turn逻辑
- NPC的LockOn系统确认是否存在

#### 7.2 AI驱动
- NPC的Behavior Tree不需要额外处理
- Turn由状态机自动触发（当AI让NPC站立时）

---

## 🧪 测试计划

### 测试场景
1. **单机Idle转身**
   - 锁定NPC，绕圈移动NPC位置（Console命令）
   - 验证：触发角度、动画选择、播放速度
   
2. **Guard转身**
   - 按住防御键，重复场景1
   
3. **边界情况**
   - 转身中途开始移动 → 应取消转身
   - 转身中途攻击/闪避 → 应取消转身（可选：允许打断）
   - 快速连续转身 → 冷却生效
   
4. **网络同步**
   - Host锁定Client，Host转身，Client观察
   - Client锁定NPC，NPC移动，Client转身，Host观察
   
5. **性能**
   - 多个NPC同时转身无卡顿

---

## 📊 工作量评估

| 阶段 | 时间 | 风险 |
|------|------|------|
| Phase 1: 数据层 | 2-3h | 低 |
| Phase 2: 逻辑层 | 3-4h | 中（角度计算） |
| Phase 3: 表现层 | 2-3h | 低 |
| Phase 4: Animator | 1-2h | 低 |
| Phase 5: Motor集成 | 1-2h | 中（Root Motion冲突） |
| Phase 6: 网络同步 | 2h | 低 |
| Phase 7: NPC | 1-2h | 低 |
| 测试调优 | 2-3h | - |
| **总计** | **14-21h** | **2-3工作日** |

---

## ⚠️ 潜在问题

### 1. Root Motion冲突
**问题**：Turn动画的Root Motion可能与Motor的旋转冲突
**解决**：
- Motor在Turn阶段让出旋转控制
- 或Turn动画不用Root Motion，改用速度曲线驱动

### 2. 网络抖动
**问题**：远端转身可能与插值冲突
**解决**：
- 转身期间暂停旋转插值
- 或发送ActionEvent通知转身开始

### 3. 动画融合
**问题**：Idle→Turn→Idle的融合可能不平滑
**解决**：
- 调整CrossFade Duration
- 使用Blend Tree混合Turn和Idle

### 4. 目标快速移动
**问题**：目标快速绕圈会导致频繁转身
**解决**：
- 增加冷却时间
- 增加角度容差（如±10°内不重新转身）
- 设置最大转身频率（1秒最多1次）

---

## 🔄 后续优化

1. **IK微调**：脚步落点IK适配地形
2. **Blend Tree**：不同角度的Turn动画融合
3. **取消窗口**：Turn前期可被攻击/闪避打断
4. **音效**：脚步声、衣物摩擦
5. **VFX**：地面灰尘（可选）

---

## 📝 提交划分建议

### Commit 1: 数据层
- 枚举、配置、Snapshot

### Commit 2: Idle转身逻辑
- IdleState Turn实现

### Commit 3: Guard转身逻辑
- GuardState Turn实现

### Commit 4: 表现层
- Presenter + Pipeline集成

### Commit 5: Animator配置
- Animator Controller设置

### Commit 6: 网络同步
- Snapshot序列化 + 远端应用

### Commit 7: 测试修正
- Bug修复 + 参数调优
