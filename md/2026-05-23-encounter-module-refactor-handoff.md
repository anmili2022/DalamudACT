# 2026-05-23 Encounter 模块拆分交接

## 本轮目标

按上一轮建议继续拆 `LocalStatsService.Encounter.cs`。

本轮只做结构拆分，把当前战斗、战斗流水、战斗结算、状态文本和 ACTX 快照构造逻辑从 `LocalStatsService.cs` 迁移到独立 partial 文件中。

执行原则：

- 不改战斗统计口径；
- 不改历史记录生成口径；
- 不改战斗流水显示内容；
- 不改 debug 战斗记录、DoT / Wildfire、历史记录、测试数据模块的行为；
- 保留原有中文注释和排查说明；
- 拆完必须构建验证。

## 已完成

### 1. 新增 Encounter partial 文件

新增文件：

```text
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs
```

该文件现在承载：

- 当前战斗状态；
- 实时战斗事件记录入口；
- 当前战斗快照刷新；
- 战斗结束判断和结算；
- 战斗流水列表；
- 状态文本刷新；
- ACTX 兼容快照构造。

### 2. 已迁移的字段和属性

已从 `LocalStatsService.cs` 迁移：

- `combatTimelineEntries`
- `currentEncounter`
- `partyOutOfCombatSinceUtc`
- `enteredCombatWithoutDataSinceUtc`
- `lastNoDataCombatDiagnosticUtc`
- `encounterFinalizedVersion`
- `latestInCombatHint`
- `suppressStaleDisplayUntilNextCombatStart`
- `CurrentCombatData`
- `DisplayCombatData`
- `CombatTimelineEntries`
- `EncounterFinalizedVersion`
- `DataSourceText`
- `StatusText`

### 3. 已迁移的对外入口

已从 `LocalStatsService.cs` 迁移：

- `RecordEncounterActivity(...)`
- `RecordDamage(...)`
- `RecordHeal(...)`
- `RecordFailure(...)`
- `RecordDeath(...)`
- `ClearCombatTimeline()`
- `ApplyCombatTimelineRetentionLimit()`
- `Update(...)`

说明：这些入口仍在同一个 `partial class LocalStatsService` 中，调用方不需要改引用。

### 4. 已迁移的战斗生命周期逻辑

已从 `LocalStatsService.cs` 迁移：

- 无数据进战斗诊断：`UpdateNoDataCombatDiagnostics(...)`
- 队友死亡轮询：`PollPartyMemberDeaths(...)`
- 队友 HP 缓存刷新：`UpdateTrackedActorHp(...)`
- 脱战计时：`UpdatePartyOutOfCombatTimer(...)`
- 结算判断：`ShouldFinalizeEncounter(...)`
- 结算执行：`FinalizeEncounter(...)`
- 全队脱战判断：`AreAllPartyMembersOutOfCombat(...)`
- 战斗结束统计对象过滤：`ShouldCountBattleCharaForCombatEnd(...)`

### 5. 已迁移的战斗流水逻辑

已从 `LocalStatsService.cs` 迁移：

- `AppendEncounterStartIfNeededLocked(...)`
- `AppendCombatTimelineEntryLocked(...)`
- `TrimCombatTimelineEntriesLocked(...)`
- `ResolveCombatTimelineSourceName(...)`
- `ResolveCombatTimelineTargetName(...)`
- `UpdateStatusText(...)`

本轮特别补齐了上一轮残留在主文件里的 `UpdateStatusText(...)`，现在状态文本逻辑已经归入 Encounter 模块。

### 6. 已迁移的嵌套类型

已从 `LocalStatsService.cs` 迁移：

- `CombatTimelineEntry`
- `CombatTimelineEntryKind`
- `EncounterSession`
- `CombatantSession`
- `ActxSnapshotFormatter`

## 当前结构状态

当前 `Features/Stats`：

```text
DalamudACT/Features/Stats/
├─ LocalStatsService.cs                  # 统计核心主文件，保留构造、Actor / ObjectTable / Party / Buddy 通用 helper
├─ LocalStatsService.DebugRecorder.cs    # debug 战斗记录模块
├─ LocalStatsService.Dots.cs             # 玩家 DoT / Wildfire / DOT 诊断模块
├─ LocalStatsService.Encounter.cs        # 当前战斗 / 流水 / 结算 / ACTX 快照模块
├─ LocalStatsService.History.cs          # 历史记录 / 预览 / 导入导出模块
├─ LocalStatsService.TestData.cs         # 内置测试数据模块
└─ PlayerDotCatalog.cs                   # 玩家 DoT 技能 / 状态目录
```

拆分后文件行数大致为：

```text
DalamudACT/Features/Stats/LocalStatsService.cs                  1267
DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs     661
DalamudACT/Features/Stats/LocalStatsService.Dots.cs             2642
DalamudACT/Features/Stats/LocalStatsService.History.cs           402
DalamudACT/Features/Stats/LocalStatsService.TestData.cs          579
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs        1066
```

## 仍保留在主文件的共享内容

以下内容仍保留在 `LocalStatsService.cs`，因为多个 partial 模块都会用到，后续如要继续拆，建议单独规划：

- 构造函数和 Excel sheet 初始化；
- `gate`、配置、Owner cache、观测到的友方 Actor cache、队友 HP cache；
- ObjectTable / PartyList / BuddyList 相关通用 helper；
- Actor 身份解析、owner 解析、宠物 / 召唤物归属解析；
- status 反射兼容 helper；
- `TryResolveTrackedSource(...)`、`TryGetTrackedActor(...)`、`FindObjectByActorId(...)` 等追踪对象解析；
- `NormalizeActionName(...)`、`NormalizeZoneName(...)`；
- `ResolveJobName(...)`；
- `CreateDamageString(...)`、`FormatActionNameWithId(...)` 等通用格式化 helper；
- `ActorIdentity`、`OwnerCacheEntry`、`TrackedActorKind`、`TrackedActor` 等共享类型。

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

- 本轮没有改统计口径；
- 本轮没有改 DoT / Wildfire 口径；
- 本轮没有改 debug 战斗记录口径；
- 本轮没有改历史记录 JSON 格式；
- 本轮没有改测试数据样本；
- `UpdateStatusText(...)` 已经从主文件迁移到 Encounter partial；
- 当前工作区在本轮前就不是干净状态，不要把历史移动误判为本轮删除；
- `1.txt` 不要误删；
- 不要执行：

```powershell
git reset --hard
git checkout -- .
```

## 下一步建议

继续按结构化路线推进时，建议下一步二选一：

### 方案 A：继续拆统计核心共享 helper

新增：

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

风险：中等。多个模块都会调用这些 helper，迁移后要立即构建验证。

### 方案 B：拆通用格式化和规范化 helper

新增：

```text
LocalStatsService.Formatting.cs
```

优先迁移：

- `NormalizeActionName(...)`
- `NormalizeZoneName(...)`
- `ResolveJobName(...)`
- `CreateDamageString(...)`
- `FormatActionNameWithId(...)`
- 其它纯格式化、纯文本处理 helper。

风险：较低。适合作为下一轮更稳的结构整理。

无论选择哪条路线，拆完继续执行：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

保持：

```text
0 warnings / 0 errors
```

## 后续进展补充

`LocalStatsService.Formatting.cs` 已在后续完成拆分，详见：`md/2026-05-23-formatting-module-refactor-handoff.md`。

原建议中的低风险方案 B 已完成；后续如继续推进结构化，建议改为拆 `LocalStatsService.Actors.cs`。
