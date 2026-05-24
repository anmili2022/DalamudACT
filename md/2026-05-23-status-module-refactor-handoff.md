# 2026-05-23 Status 模块拆分交接

## 本轮目标

按上一轮建议继续拆 `LocalStatsService.Status.cs`。

本轮只做结构拆分，把 StatusList 反射读取、状态 ID、来源、参数、剩余时间和状态表文本等状态 helper 从 `LocalStatsService.cs` 迁移到独立 partial 文件中。

执行原则：

- 不改状态读取口径；
- 不改状态来源 Actor 解析；
- 不改 DoT / Wildfire 状态归因；
- 不改 debug 战斗记录中的 BUFF / debuff 采集；
- 不改 Actor、Encounter、History、TestData、Formatting 模块行为；
- 保留原有中文注释和排查说明；
- 拆完必须构建验证。

## 已完成

### 1. 新增 Status partial 文件

新增文件：

```text
DalamudACT/Features/Stats/LocalStatsService.Status.cs
```

该文件现在承载：

- `StatusList` 枚举兼容读取；
- 状态 ID 读取；
- 状态来源 Actor 读取；
- 状态参数读取；
- 状态剩余时间读取；
- 状态 Excel `GameData` 文本 / 数值读取；
- 对不同 Dalamud / 运行时对象属性差异的反射兜底。

### 2. 已迁移的 helper

已从 `LocalStatsService.cs` 迁移：

- `ResolveStatusSourceActorId(...)`
- `TryGetStatusGameDataText(...)`
- `TryExtractGameDataText(...)`
- `TryGetStatusGameDataInt(...)`
- `GetStatusId(...)`
- `TryGetStatusParam(...)`
- `GetStatusRemainingTime(...)`
- `GetReflectedStatusValue(...)`
- `EnumerateStatusEntries(...)`

说明：

- `ResolveStatusSourceActorId(...)` 仍会调用 Actors partial 中的 `TryConvertActorId(...)` 和 `GetGameObjectIdentity(...)`。
- 所有方法仍在同一个 `partial class LocalStatsService` 中，Dots / DebugRecorder 调用方不需要修改。

## 当前结构状态

当前 `Features/Stats`：

```text
DalamudACT/Features/Stats/
├─ LocalStatsService.cs                  # 统计核心主文件，保留构造、共享常量和共享锁
├─ LocalStatsService.Actors.cs           # Actor / ObjectTable / Party / Buddy / owner cache 模块
├─ LocalStatsService.DebugRecorder.cs    # debug 战斗记录模块
├─ LocalStatsService.Dots.cs             # 玩家 DoT / Wildfire / DOT 诊断模块
├─ LocalStatsService.Encounter.cs        # 当前战斗 / 流水 / 结算 / ACTX 快照模块
├─ LocalStatsService.Formatting.cs       # 通用文本 / 数字格式化模块
├─ LocalStatsService.History.cs          # 历史记录 / 预览 / 导入导出模块
├─ LocalStatsService.Status.cs           # StatusList / 状态反射读取模块
├─ LocalStatsService.TestData.cs         # 内置测试数据模块
└─ PlayerDotCatalog.cs                   # 玩家 DoT 技能 / 状态目录
```

拆分后文件行数大致为：

```text
DalamudACT/Features/Stats/LocalStatsService.cs                  46
DalamudACT/Features/Stats/LocalStatsService.Actors.cs           942
DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs    661
DalamudACT/Features/Stats/LocalStatsService.Dots.cs             2642
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs        1066
DalamudACT/Features/Stats/LocalStatsService.Formatting.cs       95
DalamudACT/Features/Stats/LocalStatsService.History.cs          402
DalamudACT/Features/Stats/LocalStatsService.Status.cs           209
DalamudACT/Features/Stats/LocalStatsService.TestData.cs         579
```

## 构建验证

已执行：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

结果：

```text
已成功生成。
0 个警告
0 个错误
```

产物：

```text
E:\git\DalamudACT\output\DalamudACT.dll
```

## 注意事项

- 本轮没有改状态字段读取规则；
- 本轮没有改状态来源解析；
- 本轮没有改 DoT / Wildfire 口径；
- 本轮没有改 debug 战斗记录口径；
- 本轮没有改 ActorControl Hook；
- 本轮没有改历史记录 JSON 结构；
- 当前工作区在本轮前就不是干净状态，不要把历史移动误判为本轮删除；
- `1.txt` 不要误删；
- 不要执行：

```powershell
git reset --hard
git checkout -- .
```

## 下一步建议

统计核心主文件已经只剩构造、共享常量和共享锁。继续按结构化路线推进时，建议下一步处理当前最大的 `LocalStatsService.Dots.cs`。

建议先拆低耦合部分：

```text
LocalStatsService.Dots.Wildfire.cs
```

优先迁移：

- Wildfire / 野火状态采集；
- Wildfire 层数记录；
- Wildfire 贡献样本；
- Wildfire 模拟结算；
- `WildfireContributionSample`；
- `PlayerWildfireKey`；
- `ActiveWildfireState`。

风险：中等。Wildfire 逻辑在 `LocalStatsService.Dots.cs` 内部与 DoT 样本共用少量 helper，迁移后要立即构建验证。

继续执行：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

保持：

```text
0 warnings / 0 errors
```
