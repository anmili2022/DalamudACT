# 2026-05-23 项目结构化重构交接

## 本轮目标

> 后续补充：`LocalStatsService.Dots.cs` 已在 2026-05-23 后续步骤完成，详见 `md/2026-05-23-dot-module-refactor-handoff.md`。

本轮按“下一步建议”先做第一步：继续拆分 `LocalStatsService.cs`，优先把已经相对独立的 `debug 战斗记录` 逻辑拆到单独 partial 文件中。

执行原则：

- 只做第一步，不一次性继续拆 DoT、History、UI 等其他大块；
- 保留原有中文注释、排查说明和业务逻辑；
- 以移动代码为主，不借重构机会改统计口径；
- 每次拆分后都跑构建确认。

## 已完成

### 1. `LocalStatsService` 改为 partial

主文件：

```text
DalamudACT/Features/Stats/LocalStatsService.cs
```

已从：

```csharp
internal sealed class LocalStatsService
```

改为：

```csharp
internal sealed partial class LocalStatsService
```

这样后续可以继续按功能域拆成多个 partial 文件，避免单文件继续膨胀。

### 2. 拆出 debug 战斗记录模块

新增文件：

```text
DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs
```

本次迁移到该文件的内容包括：

- debug 战斗记录状态字段：
  - `DebugCombatRecordPollInterval`
  - `debugCombatLogEntries`
  - `debugObservedStatusKeys`
  - `debugBossCastActionIds`
  - `lastDebugCombatRecordPollUtc`
  - `debugCombatRecorderPrimed`
- 对外接口：
  - `DebugCombatLogEntries`
  - `ClearDebugCombatLog()`
  - `ApplyDebugCombatLogRetentionLimit()`
  - `SetDebugCombatRecordingEnabled(...)`
  - `RecordDebugBossAbility(...)`
  - `RecordDebugMarker(...)`
- debug 战斗记录轮询与采集：
  - `PollDebugCombatRecorderLocked(...)`
  - `EnumerateDebugBossBattleNpcs()`
  - `CaptureDebugBossCastLocked(...)`
  - `CaptureDebugBossBuffsLocked(...)`
  - `CaptureDebugFriendlyBuffsLocked(...)`
  - `CaptureDebugFriendlyDebuffsLocked(...)`
- debug 日志维护与格式化：
  - `AppendDebugCombatLogEntryLocked(...)`
  - `TrimDebugCombatLogEntriesLocked()`
  - `GetDebugActionName(...)`
  - `GetDebugStatusName(...)`
  - `FormatStatusNameWithId(...)`
  - `FormatDebugStatusRemaining(...)`
  - `IsBuffStatus(...)`
  - `IsDebuffStatus(...)`
  - `ResolveCastTargetActorId(...)`
  - `BuildDebugTargetSummary(...)`
- debug 专用嵌套类型：
  - `DebugCombatLogEntry`
  - `DebugCombatLogEntryKind`
  - `DebugObservedStatusKey`

未迁移的共享辅助逻辑仍保留在主文件中，例如：

- `NormalizeEventActorId(...)`
- `LooksLikeCombatActorId(...)`
- `IsLocalPlayerActor(...)`
- `ResolveCombatTimelineSourceName(...)`
- `ResolveCombatTimelineTargetName(...)`
- `NormalizeActionName(...)`

这些方法仍被 DoT、战斗流水、演员解析等多个路径共用，暂时不强行归到 debug 文件里，避免拆分边界过度扩大。

### 3. 注释保留情况

- 本次拆分以原代码块移动为主；
- 原有中文注释和排查说明没有主动删减；
- 新文件顶部只新增了一行模块说明注释；
- 没有改 debug 战斗记录的业务判断、开关条件或日志内容。

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

## 当前代码结构状态

当前统计核心相关文件：

```text
DalamudACT/Features/Stats/
├─ LocalStatsService.cs                  # 统计核心主文件，仍较大
├─ LocalStatsService.DebugRecorder.cs    # 本轮拆出的 debug 战斗记录模块
└─ PlayerDotCatalog.cs                   # 玩家 DoT 目录
```

当前 Hook 相关文件：

```text
DalamudACT/Plugin/
├─ ACT.cs
└─ Hooks/
   └─ ACT.ActorControlHook.cs
```

当前 debug 战斗记录 UI：

```text
DalamudACT/Features/DebugCombatLog/DebugCombatLogWindow.cs
```

## 当前风险与注意事项

- 工作区在本轮开始前就不是干净状态，包含较多历史改动、移动文件和未跟踪文件；
- `1.txt` 是历史交接/现场文件，不要误删；
- 根目录里的历史临时文件暂未清理，不要在未确认前删除；
- 本轮只是结构拆分，没有实战验证 debug 战斗记录是否能采到“特效标记”；
- `LocalStatsService.cs` 仍然很大，后续还需要继续按功能拆分；
- 不要执行会覆盖现场的命令，例如：

```powershell
git reset --hard
git checkout -- .
```

## 下一步建议

如果继续按当前结构化路线推进，建议顺序如下：

1. 拆 `LocalStatsService.Dots.cs`
   - 只移动玩家 DoT / Wildfire / potency 推断 / DOT 诊断相关逻辑；
   - 不改 DoT 统计口径；
   - 拆完后跑 `dotnet build`。
2. 拆 `LocalStatsService.History.cs`
   - 历史记录导入导出、预览、序列化 payload 可以独立出来；
   - 注意保留历史预览相关状态和 UI 调用接口。
3. 拆 `LocalStatsService.Encounter.cs`
   - 战斗开始/结束、当前战斗快照、战斗流水主逻辑可逐步迁移；
   - 不要和 DoT 拆分同一轮混做。
4. 再拆 UI：
   - `UI/Windows/SettingsWindow.cs`
   - `UI/Panels/StatsPanel.cs`

每完成一个小拆分，都先执行：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

目标保持：

```text
0 warnings / 0 errors
```
