# 2026-05-23 Formatting 模块拆分交接

## 本轮目标

按上一轮建议继续拆低风险的 `LocalStatsService.Formatting.cs`。

本轮只做结构拆分，把区域名、技能名、职业名、伤害数字、暴击后缀和未知对象文本等纯格式化 helper 从 `LocalStatsService.cs` 迁移到独立 partial 文件中。

执行原则：

- 不改统计口径；
- 不改战斗流水文本口径；
- 不改 ACTX 快照字段；
- 不改 DoT / Wildfire、debug 战斗记录、历史记录和测试数据行为；
- 保留原有中文文本；
- 拆完必须构建验证。

## 已完成

### 1. 新增 Formatting partial 文件

新增文件：

```text
DalamudACT/Features/Stats/LocalStatsService.Formatting.cs
```

该文件现在承载通用文本和数字格式化逻辑：

- 区域名规范化；
- 技能名规范化；
- 技能名 + ID 展示文本；
- 暴击 / 模拟暴击后缀；
- 未知对象 fallback 名称；
- 职业 ID 到中文职业名；
- 伤害数字中文单位格式化。

### 2. 已迁移的 helper

已从 `LocalStatsService.cs` 迁移：

- `FormatActionNameWithId(...)`
- `NormalizeZoneName(...)`
- `NormalizeActionName(...)`
- `FormatCriticalSuffix(...)`
- `FormatSimulatedCriticalSuffix(...)`
- `BuildUnknownActorName(...)`
- `ResolveJobName(...)`
- `CreateDamageString(...)`
- `FormatChineseDamageUnit(...)`

说明：

- `BuildUnknownActorName(...)` 虽然会引用 `InvalidActorId`，但职责仍是文本 fallback 格式化，因此归入 Formatting partial。
- 所有方法仍在同一个 `partial class LocalStatsService` 中，调用方和访问级别不需要修改。

## 当前结构状态

当前 `Features/Stats`：

```text
DalamudACT/Features/Stats/
├─ LocalStatsService.cs                  # 统计核心主文件，保留构造、Actor / ObjectTable / Party / Buddy 通用 helper
├─ LocalStatsService.DebugRecorder.cs    # debug 战斗记录模块
├─ LocalStatsService.Dots.cs             # 玩家 DoT / Wildfire / DOT 诊断模块
├─ LocalStatsService.Encounter.cs        # 当前战斗 / 流水 / 结算 / ACTX 快照模块
├─ LocalStatsService.Formatting.cs       # 通用文本 / 数字格式化模块
├─ LocalStatsService.History.cs          # 历史记录 / 预览 / 导入导出模块
├─ LocalStatsService.TestData.cs         # 内置测试数据模块
└─ PlayerDotCatalog.cs                   # 玩家 DoT 技能 / 状态目录
```

拆分后文件行数大致为：

```text
DalamudACT/Features/Stats/LocalStatsService.cs                  1181
DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs     661
DalamudACT/Features/Stats/LocalStatsService.Dots.cs             2642
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs        1066
DalamudACT/Features/Stats/LocalStatsService.Formatting.cs         95
DalamudACT/Features/Stats/LocalStatsService.History.cs           402
DalamudACT/Features/Stats/LocalStatsService.TestData.cs          579
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

- 本轮没有改任何数值计算；
- 本轮没有改中文显示结果；
- 本轮没有改历史记录 JSON 结构；
- 本轮没有改 debug 战斗记录；
- 本轮没有改 ActorControl Hook；
- 当前工作区在本轮前就不是干净状态，不要把历史移动误判为本轮删除；
- `1.txt` 不要误删；
- 不要执行：

```powershell
git reset --hard
git checkout -- .
```

## 下一步建议

继续按结构化路线推进时，建议下一步拆：

```text
LocalStatsService.Actors.cs
```

优先迁移：

- ObjectTable / PartyList / BuddyList 遍历 helper；
- actor identity / owner cache / pet owner 解析；
- `TryResolveTrackedSource(...)`；
- `TryGetTrackedActor(...)`；
- `FindObjectByActorId(...)`；
- `AreEquivalentActorIds(...)`；
- `TrackedActor`、`TrackedActorKind`、`ActorIdentity`、`OwnerCacheEntry`。

风险：中等。Actor helper 被 Encounter、Dots、DebugRecorder 多个 partial 共同调用，迁移后要立即构建验证。

继续执行：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

保持：

```text
0 warnings / 0 errors
```

## 后续进展补充

`LocalStatsService.Actors.cs` 已在后续完成拆分，详见：`md/2026-05-23-actors-module-refactor-handoff.md`。

原建议的 Actor helper 拆分已完成；后续如继续推进结构化，建议改为拆 `LocalStatsService.Status.cs`。
