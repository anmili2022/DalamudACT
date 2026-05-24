# 2026-05-23 TestData 模块拆分交接

## 本轮目标

按上一轮建议继续拆 `LocalStatsService.TestData.cs`。

本轮只做结构拆分，把内置测试数据导入入口和演示战斗记录构造函数从 `LocalStatsService.cs` 迁到独立 partial 文件中。

执行原则：

- 不改测试样本内容；
- 不改测试数据导入行为；
- 不改历史记录生成口径；
- 不改 UI 显示字段；
- 保留原有中文注释和数据文本；
- 拆完必须构建验证。

## 已完成

### 1. 新增 TestData partial 文件

新增文件：

```text
DalamudACT/Features/Stats/LocalStatsService.TestData.cs
```

该文件现在承载：

- 一键导入测试数据入口；
- 三场内置测试战斗快照构造；
- 测试战斗通用 `CombatDataWrapper` 构造；
- 测试 combatant 派生治疗量补全；
- 测试用 duration 秒数解析；
- 测试 `Combatant` 构造。

### 2. 从主文件迁移的对外入口

已迁移：

- `LoadTestData()`

说明：

- `LoadTestData()` 仍然会调用 History partial 中的 `UpsertHistoricalRecord(...)`、`CreateSyntheticHistoricalRecord(...)` 和 `SortHistoricalRecords()`；
- 因为仍在同一个 `partial class LocalStatsService` 内，所以不需要改访问级别；
- 原有导入测试数据后的清理行为保持不变，包括清理 DoT、debug 战斗记录、当前战斗状态和历史预览状态。

### 3. 从主文件迁移的测试数据构造函数

已迁移：

- `BuildRaidTestCombatData()`
- `BuildRaidEightPlayerTestCombatData()`
- `BuildTrialTestCombatData()`
- `BuildTrainingTestCombatData()`
- `BuildTestCombatData(...)`
- `PopulateDerivedTestCombatantMetrics(...)`
- `ParseDurationTextToSeconds(...)`
- `CreateTestCombatant(...)`

其中：

- `BuildRaidTestCombatData()` 当前仍未作为 `LoadTestData()` 的三场默认样本之一，但它属于同一组内置测试数据构造函数，因此一并移动；
- 没有修改任何测试玩家名、职业、伤害、治疗、承伤、死亡、命中等样本字段。

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
├─ LocalStatsService.cs                  # 统计核心主文件，仍保留 Encounter / 通用 Actor helper 等
├─ LocalStatsService.DebugRecorder.cs    # debug 战斗记录模块
├─ LocalStatsService.Dots.cs             # 玩家 DoT / Wildfire / DOT 诊断模块
├─ LocalStatsService.History.cs          # 历史记录 / 预览 / 导入导出模块
├─ LocalStatsService.TestData.cs         # 内置测试数据模块
└─ PlayerDotCatalog.cs                   # 玩家 DoT 技能 / 状态目录
```

本轮拆分后，主文件 `LocalStatsService.cs` 进一步缩小，测试数据样本不再混在核心统计逻辑中。

## 注意事项

- 本轮没有改测试数据数值；
- 本轮没有改 `LoadTestData()` 调用链；
- 本轮没有改 `CreateSyntheticHistoricalRecord(...)`，它仍在 History partial 中；
- 本轮没有改战斗结算和历史记录结构；
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
LocalStatsService.Encounter.cs
```

优先迁移：

- `RecordEncounterActivity(...)`
- `RecordDamage(...)`
- `RecordHeal(...)`
- `RecordFailure(...)`
- `RecordDeath(...)`
- `FinalizeEncounter(...)`
- `ShouldFinalizeEncounter(...)`
- `UpdatePartyOutOfCombatTimer(...)`
- `AppendEncounterStartIfNeededLocked(...)`
- `UpdateStatusText(...)` 可视情况保留或一起迁移；
- `EncounterSession`
- `CombatantSession`
- `ActxSnapshotFormatter`

拆 `Encounter` 风险比前几轮更高，因为会牵涉当前战斗快照、状态文本和历史写入。建议拆完立刻构建，并优先检查历史结算和当前战斗显示是否仍能编译通过。

继续执行：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

保持：

```text
0 warnings / 0 errors
```

## 后续进展补充

`LocalStatsService.Encounter.cs` 已在后续完成拆分，详见：`md/2026-05-23-encounter-module-refactor-handoff.md`。

已迁移内容包括当前战斗状态、`RecordDamage(...)` / `RecordHeal(...)` / `RecordFailure(...)` / `RecordDeath(...)`、战斗流水、战斗结算、`UpdateStatusText(...)`、`EncounterSession`、`CombatantSession` 和 `ActxSnapshotFormatter`。

后续如继续推进结构化，建议改为拆 `LocalStatsService.Actors.cs` 或 `LocalStatsService.Formatting.cs`。

