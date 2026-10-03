# 料理菜谱、工序与状态机设计

日期：2026-10-03

## 目标

实现一个接近《幻想生活 i》料理演示的限时工序玩法：

- 每道菜由提前配置好的固定轮次组成。
- 每轮按固定顺序显示并执行 2～3 个工序。
- 玩家通过左右键在汤锅、案板、炒锅之间离散换位。
- 玩家使用 J 完成短按、连续按、长按三类输入。
- 每轮根据完成时间评为 Miss、Good、Great 或 Excellent。
- 评价转换为整道菜的制作进度；达到 100% 即成功并可提前结束。
- 固定轮次耗尽仍未达到 100% 时制作失败。

## 非目标

第一阶段不实现：

- 随机生成轮次。
- 自由选择工序顺序。
- 单个菜谱对工序目标次数或持续时间的特殊覆盖。
- 最终美术品质的 UI、VFX、音频与镜头表现。
- 由 Animator、Timeline 或 Animation Event 决定玩法状态。

## 空间布局

站位与设备固定：

| 站位 | 设备 | 支持工序 |
|---|---|---|
| 左 | 汤锅 | 搅一下汤锅、持续搅动汤锅 |
| 中 | 案板 | 切一下菜、连续切菜 |
| 右 | 炒锅 | 翻一下炒锅 |

换位通过启用目标站位角色、禁用其他站位角色实现，不进行 Transform 平滑移动。

## 工序类型

| 工序 | 输入模式 | 默认完成条件 | 中断行为 |
|---|---|---|---|
| 切一下菜 | 短按 | J 按下 1 次 | 按下即完成 |
| 连续切菜 | 连续按 | J 按下 6 次 | 停止或换位时保留进度 |
| 搅一下汤锅 | 短按 | J 按下 1 次 | 按下即完成 |
| 持续搅动汤锅 | 长按 | 累计按住 J 2 秒 | 松开或换位时保留进度 |
| 翻一下炒锅 | 短按 | J 按下 1 次 | 按下即完成 |

每种工序由一个 OperationDefinition ScriptableObject 描述：

- OperationType
- StationType
- InputMode
- RequiredAmount
- StandardDuration
- Icon
- AnimatorTrigger

第一阶段只使用工序全局默认值。以后出现真实内容需求时，再增加单个工序实例的目标覆盖。

## 菜谱与轮次

每道菜由一个 RecipeDefinition ScriptableObject 描述：

- RecipeId
- DisplayName
- Icon
- 固定且有序的 RoundDefinition 列表

每个 RoundDefinition 包含 2～3 个按顺序排列的 OperationDefinition。

玩家只能完成当前激活工序。对后续工序提前输入不会产生进度。

## 输入规则

CookingSession 对外接收：

- StartRecipe
- MoveStation(direction)
- PressCook
- ReleaseCook
- Tick(deltaTime)
- ContinueAfterRoundResult

规则：

- 正确站位短按：立即完成短按工序。
- 正确站位连续按：每次 PressCook 增加一次进度；只有第一次启动动作表现。
- 正确站位长按：J 保持按下且站位正确时，Tick 持续增加进度。
- 松开 J 或离开正确站位：长按进度暂停但保留。
- 连续按中断或离开正确站位：已有点击进度保留。
- 错误站位按 J：输入无效，工序进度不变，本轮计时继续，并产生 InvalidInput 事件。

## 轮次计时与评价

本轮计时从第一个工序进入可操作状态开始，到最后一个工序完成为止。

默认标准时间：

StandardRoundTime =

- 所有工序 StandardDuration 之和；
- 加上相邻工序需要换位时的默认换位/反应时间。

默认评价阈值：

- Excellent：用时不超过 StandardRoundTime × 0.85。
- Great：用时不超过 StandardRoundTime × 1.00。
- Good：用时不超过 StandardRoundTime × 1.40。
- Miss：超过 Good 阈值仍未完成全部工序。

RoundDefinition 支持可选的整轮阈值覆盖：

- UseCustomTimeThresholds
- ExcellentTime
- GreatTime
- GoodTime

阈值必须满足 ExcellentTime ≤ GreatTime ≤ GoodTime。

## 整道菜进度

菜谱成功目标固定为 100%。

每轮基础进度：

BaseRoundProgress = 100 ÷ RecipeDefinition.Rounds.Count

评价乘数：

| 评价 | 乘数 |
|---|---:|
| Miss | 0 |
| Good | 1.00 |
| Great | 1.25 |
| Excellent | 1.50 |

每轮结算：

AwardedProgress = BaseRoundProgress × GradeMultiplier

累计进度限制在 0～100%。

- 达到 100%：立即制作成功，跳过剩余轮次。
- 未达到 100% 且还有轮次：进入下一轮。
- 所有固定轮次耗尽仍未达到 100%：制作失败。

因此全部 Good 会在最后一轮刚好完成；Great 和 Excellent 可以弥补 Miss，也可以让玩家提前完成。

## 状态机

核心状态：

1. Idle
2. RoundIntro
3. OperationActive
4. RoundResult
5. RecipeSuccess
6. RecipeFailure

主要转换：

Idle → RoundIntro：

- StartRecipe 创建运行时会话。

RoundIntro → OperationActive：

- UI 展示本轮工序后开始第一项工序和本轮计时。

OperationActive → OperationActive：

- 当前工序完成但本轮仍有后续工序。

OperationActive → RoundResult：

- 本轮全部工序完成；或者超过 Good 阈值，判定 Miss。

RoundResult → RecipeSuccess：

- 累计进度达到 100%。

RoundResult → RoundIntro：

- 未达到 100%，且仍有固定轮次。

RoundResult → RecipeFailure：

- 未达到 100%，且固定轮次已经耗尽。

## 架构与职责

### 纯核心层

CookingSession：

- 不依赖 MonoBehaviour、Input System、Animator、UI、场景对象或 VFX。
- 保存当前状态、轮次、工序、站位、输入状态、计时与进度。
- 执行所有玩法判定。
- 通过纯 C# 事件输出状态变化。

RecipeRuntimeData、RoundRuntimeData、OperationRuntimeData：

- 从 ScriptableObject 转换得到的只读运行时数据。
- 允许核心层独立测试和复用。

### Unity 配置层

OperationDefinition、RecipeDefinition：

- 提供 Inspector 配置和 OnValidate 校验。
- 将 Unity 资源转换为核心运行时数据。

### Unity 适配与表现层

CookingInputController：

- 使用自动生成的 CookInputActions。
- 将左右键、J 按下/松开和 deltaTime 转交给 CookingSession。
- 根据核心事件切换三个站位角色并控制 Animator。
- 不包含连按、长按、评价或菜谱进度判定。

CookingHud：

- 显示当前轮的有序工序图标与完成勾选。
- 仅在连续按和长按工序中显示工序进度条。
- 显示整道菜的 0～100% 进度。
- 显示 Miss、Good、Great、Excellent 和最终成功/失败。
- 第一阶段使用占位文字、色块和现有占位资源。

## 核心事件

CookingSession 输出：

- StateChanged
- StationChanged
- OperationStarted
- OperationProgressChanged
- OperationCompleted
- InvalidInput
- RoundEvaluated
- RecipeProgressChanged
- RecipeSucceeded
- RecipeFailed

Unity 适配层消费这些事件，不允许 UI 或 Animator 反向决定核心状态。

## 配置校验

开始料理前必须验证：

- 菜谱至少有一轮。
- 每轮包含 2～3 个工序。
- 工序引用不为空。
- 工序默认目标大于 0。
- 时间阈值为正数并保持 Excellent ≤ Great ≤ Good。
- 工序类型、站位与输入模式配置有效。

无效菜谱不得启动，并输出明确错误。

## 测试策略

EditMode 核心测试：

- 工序只能按固定顺序完成。
- 错误站位输入无效。
- 短按立即完成。
- 连续按逐次增加且不会重复 Start。
- 长按随 Tick 增长。
- 中断、松开和换位保留工序进度。
- 超过 Good 阈值产生 Miss。
- 四档评价边界正确。
- 四档评价正确增加菜谱进度。
- 达到 100% 提前成功。
- 轮次耗尽且不足 100% 时失败。

PlayMode 集成验证：

- 强类型 Input Action 能驱动 CookingSession。
- 左中右角色只显示一个。
- Animator 收到正确 Trigger。
- HUD 数据与核心状态一致。

## 第一阶段完成标准

- 可以配置五种默认工序和至少一道固定轮次的演示菜谱。
- 可以从第一轮完整游玩到提前成功或最终失败。
- UI 能显示工序顺序、当前工序进度、菜谱总进度和评价。
- 核心测试全部通过。
- PlayMode 中无 Console Error。
