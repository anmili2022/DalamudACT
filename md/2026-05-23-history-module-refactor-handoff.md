# 2026-05-23 History 模块拆分交接

## 本轮目标

> 后续补充：`LocalStatsService.TestData.cs` 已在 2026-05-23 后续步骤完成，详见 `md/2026-05-23-testdata-module-refactor-handoff.md`。

按上一轮建议继续拆 `LocalStatsService.History.cs`。

本轮只做结构拆分，把历史记录列表、历史预览、导入 / 导出、历史记录序列化等逻辑从 `LocalStatsService.cs` 迁到独立 partial 文件中。

执行原则：

- 不改历史记录数据结构；
- 不改导入 / 导出口径；
- 不改战斗结算口径；
- 不改测试数据内容；
- 保留原有中文注释和排查说明；
- 拆完必须构建验证。

## 已完成

### 1. 新增 History partial 文件

新增文件：

```text
DalamudACT/Features/Stats/LocalStatsService.History.cs
```

该文件现在承载：

- 历史记录列表；
- 历史记录选择索引；
- 历史预览过期时间；
- 历史预览倒计时；
- 历史导入；
- 历史导出；
- 历史 JSON 序列化配置；
- 历史记录 upsert / sort / identity 判断；
- 历史记录导入校验；
- 历史导入导出状态文本；
- 历史记录导入导出文件路径；
- `HistoricalRecordsExportPayload`。

### 2. 从主文件迁移的字段 / 常量

从：

```text
DalamudACT/Features/Stats/LocalStatsService.cs
```

迁移到：

```text
DalamudACT/Features/Stats/LocalStatsService.History.cs
```

的成员包括：

- `MinimumHistoricalEncounterSeconds`
- `HistoryJsonOptions`
- `historicalRecords`
- `selectedHistoricalRecordIndex`
- `historicalPreviewExpiresAtUtc`

### 3. 从主文件迁移的属性 / 对外接口

已迁移：

- `HistoricalRecords`
- `SelectedHistoricalRecordIndex`
- `HistoryTransferStatusText`
- `HistoryTransferFilePath`
- `ClearHistory()`
- `LoadHistoricalRecord(...)`
- `PreviewHistoricalRecord(...)`
- `ExportHistoricalRecords()`
- `ImportHistoricalRecords()`

说明：

- `ClearHistory()` 虽然会同时清理 DoT / Debug / 当前战斗状态，但它的入口语义是“清空历史并重置当前统计状态”，所以本轮放入 History partial。
- `LoadTestData()` 暂时仍保留在 `LocalStatsService.cs`，后续建议单独拆 `LocalStatsService.TestData.cs`，避免这轮混做。

### 4. 从主文件迁移的历史预览逻辑

已迁移：

- `PreviewHistoricalRecordLocked(...)`
- `ClearHistoricalPreviewLocked()`
- `HasSelectedHistoricalPreviewLocked()`
- `ShouldHistoricalPreviewCountdownLocked()`
- `EnsureHistoricalPreviewCountdownStartedLocked(...)`
- `RefreshDisplayCombatDataLocked(...)`
- `GetHistoricalPreviewRemainingSeconds(...)`

这些方法仍会被 `Update(...)`、导入历史、测试数据入口和状态文本刷新逻辑调用；因为仍在同一个 partial class 内，不需要改访问级别。

### 5. 从主文件迁移的历史记录 helper

已迁移：

- `HasSameHistoryIdentity(...)`
- `UpsertHistoricalRecord(...)`
- `TrySelectLatestHistoricalRecord()`
- `SortHistoricalRecords()`
- `GetHistorySortTime(...)`
- `CreateHistoricalRecord(...)`
- `CreateSyntheticHistoricalRecord(...)`
- `ParseDurationText(...)`
- `DeserializeHistoricalRecords(...)`
- `IsValidHistoricalRecord(...)`

说明：

- `CreateSyntheticHistoricalRecord(...)` 目前仍被 `LoadTestData()` 调用，因此它虽然在 History partial 中，仍是为了保持测试数据入口可用；
- 真正的测试数据构造函数仍留在主文件，后续再拆 `LocalStatsService.TestData.cs`。

### 6. 从主文件迁移的嵌套类型

已迁移：

- `HistoricalRecordsExportPayload`

### 7. 主文件清理

`LocalStatsService.cs` 中移除了本轮不再需要的 using：

```csharp
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
```

对应依赖现在分别位于：

- `LocalStatsService.History.cs`
- `LocalStatsService.Dots.cs`

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

## 当前结构状态

当前 `Features/Stats`：

```text
DalamudACT/Features/Stats/
├─ LocalStatsService.cs                  # 统计核心主文件，仍保留 Encounter / TestData / 通用 Actor helper 等
├─ LocalStatsService.DebugRecorder.cs    # debug 战斗记录模块
├─ LocalStatsService.Dots.cs             # 玩家 DoT / Wildfire / DOT 诊断模块
├─ LocalStatsService.History.cs          # 历史记录 / 预览 / 导入导出模块
└─ PlayerDotCatalog.cs                   # 玩家 DoT 技能 / 状态目录
```

## 注意事项

- 本轮没有改 `HistoricalCombatData`；
- 本轮没有改导入导出 JSON 格式；
- 本轮没有改历史记录排序 / 去重规则；
- 本轮没有改战斗结算阈值，只移动了 `MinimumHistoricalEncounterSeconds`；
- 本轮没有拆 `LoadTestData()`；
- 当前工作区在本轮前就不是干净状态，不要把历史移动误判为本轮删除；
- `1.txt` 不要误删；
- 不要执行：

```powershell
git reset --hard
git checkout -- .
```

## 下一步建议

原建议的 `LocalStatsService.TestData.cs` 已完成。继续按结构化路线推进时，建议下一步拆：

```text
LocalStatsService.Encounter.cs
```

优先迁移当前战斗记录、结算、状态文本和快照构造相关逻辑。

拆完继续执行：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

保持：

```text
0 warnings / 0 errors
```
