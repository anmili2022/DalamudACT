# 2026-05-23 DoT 模块拆分交接

## 本轮目标

按上一轮结构化建议继续下一步：拆 `LocalStatsService.Dots.cs`。

本轮只做结构拆分，目标是把玩家 DoT、Wildfire、DOT 诊断相关代码从 `LocalStatsService.cs` 迁到独立 partial 文件中，降低主文件体积，并为后续拆 `History` / `Encounter` 留出更清晰的边界。

执行原则：

- 不改 DoT / Wildfire 统计算法；
- 不改 ACT 对账口径；
- 不改事件入口调用方式；
- 保留原有中文注释和排查说明；
- 拆完必须构建验证。

## 已完成

### 1. 新增 DoT partial 文件

新增文件：

```text
DalamudACT/Features/Stats/LocalStatsService.Dots.cs
```

该文件现在承载：

- 玩家 DoT 挂载候选记录；
- 玩家 hostile action 样本记录；
- 玩家 DoT 伤害回补；
- 活跃 DoT 状态轮询；
- source-owned DoT 状态解析；
- tick 归因与模拟补算；
- DoT 估算伤害刷新；
- Action / ActionTransient 威力解析；
- Wildfire 状态采集、层数记录和模拟结算；
- 聚焦 DOT 诊断日志；
- DoT / Wildfire 专用嵌套类型。

### 2. 从主文件迁移的状态与缓存

从：

```text
DalamudACT/Features/Stats/LocalStatsService.cs
```

迁移到：

```text
DalamudACT/Features/Stats/LocalStatsService.Dots.cs
```

的字段包括：

- `recentHostilePlayerActions`
- `activePlayerDots`
- `activeWildfires`
- `dotStatusClassificationCache`
- `actionDescriptionDotPotencyCache`
- `actionDescriptionDotPotencyCacheMisses`
- `actionDescriptionPotencyCache`
- `actionDescriptionPotencyCacheMisses`
- `playerDotDiagnosticLogTimestamps`
- `lastPlayerDotStatusPollUtc`
- `lastPlayerDotDebugLogUtc`

相关常量也已迁移，包括：

- `PlayerDotStatusPollInterval`
- `PlayerDotTickInterval`
- `PlayerDotTickJitterAllowance`
- `PlayerDotRecentActionTtl`
- `PlayerDotSourceOwnedTargetResolutionWindow`
- `PlayerDotStatusGracePeriod`
- `PlayerDotDebugLogThrottle`
- `PlayerDotFocusedDiagnosticLogThrottle`
- `WildfireStatusGracePeriod`
- `WildfireDetonationTimingAllowance`
- `ObservedPlayerDotCriticalHitMultiplier`
- `ObservedPlayerDotDirectHitMultiplier`
- `SimulatedDotCriticalMultiplier`
- `ActionDescriptionPotencyRegex`
- `ActionDescriptionDotPotencyRegex`
- `WildfireActionId`
- `WildfireStatusId`
- `WildfirePotencyPerWeaponskill`
- `WildfireMaxWeaponskillCount`
- `WildfireDotLikeDamageScale`
- `FocusedPlayerDotDiagnosticActionIds`
- `FocusedPlayerDotDiagnosticStatusIds`
- `WildfireAnchorPotencies`

### 3. 从主文件迁移的主要方法

对外入口：

- `ObservePotentialPlayerDotApplication(...)`
- `ObservePotentialPlayerHostileActionSample(...)`
- `ObservePotentialPlayerDotDamageSeed(...)`
- `TryRecordPlayerDotDamage(...)`

轮询与状态采集：

- `PollActivePlayerDots(...)`
- `CapturePlayerDotStatusesForHostileTargetLocked(...)`
- `CaptureSourceOwnedPlayerDotStatusesForFriendlyActorLocked(...)`
- `TryResolveSourceOwnedPlayerDotTargetActorIdLocked(...)`
- `TryCreateActivePlayerDotStateLocked(...)`
- `TryResolvePlayerDotAttributionLocked(...)`

状态清理：

- `RemoveActivePlayerDotsForTargetLocked(...)`
- `RemoveActiveWildfiresForTargetLocked(...)`
- `TrimInactivePlayerDotsLocked(...)`
- `TrimInactiveWildfiresLocked(...)`
- `TrimRecentHostilePlayerActionsLocked(...)`
- `DecayActivePlayerDotStatesLocked(...)`

Wildfire：

- `CaptureActiveWildfiresForHostileTargetLocked(...)`
- `TryCreateOrRefreshActiveWildfireStateLocked(...)`
- `ResolveWildfireStackCount(...)`
- `NoteWildfireWeaponskillContributionLocked(...)`
- `TryRecordPendingWildfireDetonationsLocked(...)`
- `TryRecordWildfireDetonationLocked(...)`
- `EstimateWildfireDamageLocked(...)`
- `TryEstimateWildfireDamageFromContributionSamplesLocked(...)`
- `BuildWildfireContributionSummary(...)`
- `TryResolveWildfireAnchorActionLocked(...)`

DoT 伤害估算与威力解析：

- `RefreshActivePlayerDotEstimatedDamageLocked(...)`
- `EstimatePlayerDotTickDamageFromObservedDamage(...)`
- `TryEstimatePlayerDotTickDamageFromPotencyRatio(...)`
- `TryEstimatePlayerDotTickDamageFromAveragePotencyRatio(...)`
- `ResolvePlayerDotEstimatedTickDamageLocked(...)`
- `TryEstimatePlayerDotTickDamageFromObservedPotencySamplesLocked(...)`
- `TryResolvePlayerDotPotencyRatio(...)`
- `TryGetActionDescriptionDotPotencies(...)`
- `TryGetActionDescriptionPotency(...)`
- `TryParseActionDescriptionDotPotencies(...)`

模拟 tick 与诊断：

- `SimulateActivePlayerDotTicksLocked(...)`
- `ResolvePlayerDotTicksDue(...)`
- `TryRecordSimulatedPlayerDotTickLocked(...)`
- `ResolvePlayerDotCritical(...)`
- `ResolveObservedCritRate(...)`
- `IsSimulatedCritical(...)`
- `IsFocusedPlayerDotDiagnosticAction(...)`
- `IsFocusedPlayerDotDiagnosticStatus(...)`
- `IsFocusedPlayerDotDiagnosticSkill(...)`
- `IsFocusedPlayerDotDiagnosticState(...)`
- `BuildFocusedPlayerDotDiagnosticStateText(...)`
- `LogFocusedPlayerDotDiagnosticLocked(...)`

### 4. 从主文件迁移的嵌套类型

已迁移：

- `ActionDescriptionDotPotencyEntry`
- `WildfireContributionSample`
- `RecentHostilePlayerAction`
- `PlayerDotKey`
- `PlayerWildfireKey`
- `ActivePlayerDotState`
- `ActiveWildfireState`

### 5. 保留在主文件里的共享逻辑

下列方法没有迁移，因为它们仍被 debug 战斗记录、Actor 解析或通用统计路径共用：

- `NormalizeActionName(...)`
- `FormatActionNameWithId(...)`
- `ResolveCombatTimelineSourceName(...)`
- `ResolveCombatTimelineTargetName(...)`
- `GetStatusId(...)`
- `TryGetStatusParam(...)`
- `GetStatusRemainingTime(...)`
- `GetReflectedStatusValue(...)`
- `ResolveStatusSourceActorId(...)`
- `TryGetStatusGameDataText(...)`
- `TryGetStatusGameDataInt(...)`
- `EnumerateStatusEntries(...)`
- `TryResolveTrackedSource(...)`
- `TryGetTrackedActor(...)`
- `FindObjectByActorId(...)`
- `AreEquivalentActorIds(...)`

这些后续如果要继续整理，建议单独拆一个共享状态 / Actor helper partial，不要混在 DoT 拆分里做。

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
├─ LocalStatsService.cs                  # 统计核心主文件，仍保留 Encounter / History / 通用 Actor helper 等
├─ LocalStatsService.DebugRecorder.cs    # debug 战斗记录模块
├─ LocalStatsService.Dots.cs             # 玩家 DoT / Wildfire / DOT 诊断模块
└─ PlayerDotCatalog.cs                   # 玩家 DoT 技能 / 状态目录
```

## 注意事项

- 本轮没有修改 DoT / Wildfire 统计口径；
- 本轮没有改 `PlayerDotCatalog.cs`；
- 本轮没有改 `tools/DotReconcile`；
- 本轮没有做实战 DoT 对账；
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
LocalStatsService.History.cs
```

优先迁移：

- 历史记录列表；
- history 导入 / 导出；
- 历史预览；
- `HistoricalRecordsExportPayload`；
- `HistoryTransferStatusText` / `HistoryTransferFilePath` 相关逻辑；
- `LoadTestData()` 暂时可先不动，后面单独拆 `LocalStatsService.TestData.cs`。

每次拆完继续执行：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

保持：

```text
0 warnings / 0 errors
```
