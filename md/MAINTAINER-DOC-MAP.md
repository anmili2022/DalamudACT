# 维护文档总览图

更新时间：`2026-05-24`

用途：给维护者一眼看清这套文档之间的关系，知道**先看哪份**、**下一步看哪份**，以及当前重构后不同任务该从哪条链路进入。

---

## 一、先看哪份

如果你刚接手，推荐顺序：

1. [维护首页（单页总览）](MAINTAINER-HOME.md)
2. [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
3. [维护入口索引](MAINTAINER-INDEX.md)
4. [HANDOVER.md](../HANDOVER.md)
5. [SESSION-HANDOFF.md](SESSION-HANDOFF.md)
6. [2026-05-24 NPC 队友识别交接](2026-05-24-npc-party-handoff.md)

---

## 二、文档关系图

```text
README.md
├─ README 简洁总结
├─ 使用说明
├─ 维护首页（单页总览）
│  ├─ 维护入口索引
│  ├─ 下一位维护者第一小时清单
│  └─ 维护文档总览图
├─ HANDOVER.md
│  ├─ SESSION-HANDOFF.md
│  ├─ 2026-05-24 NPC 队友识别交接
│  ├─ 2026-05-23 ActorControl 启动崩溃交接
│  ├─ 2026-05-23 debug 战斗记录友方合并交接
│  └─ 2026-05-23 各模块拆分交接
├─ DoT / 对账相关交接
├─ 发布交接 / Runbook
└─ 历史工作记录
```

说明：

- `README.md` 是用户入口，也是维护入口导航；
- `HANDOVER.md` 是当前完整交接主入口；
- `SESSION-HANDOFF.md` 是最新会话摘要；
- `2026-05-24-npc-party-handoff.md` 是当前 NPC 队友识别专项入口；
- `MAINTAINER-HOME.md` 是维护者最推荐的第一站；
- `MAINTAINER-INDEX.md` 按任务分流；
- `MAINTAINER-FIRST-HOUR-CHECKLIST.md` 用于刚接手的第一小时。

---

## 三、按任务看什么

### 1. 刚接手，只想快速进入状态

先看：

- [维护首页（单页总览）](MAINTAINER-HOME.md)
- [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
- [HANDOVER.md](../HANDOVER.md)
- [SESSION-HANDOFF.md](SESSION-HANDOFF.md)

### 2. 想排查 NPC 队友 / 友方 NPC / 敌方 NPC

先看：

- [2026-05-24 NPC 队友识别交接](2026-05-24-npc-party-handoff.md)
- [2026-05-23 Actors 模块拆分交接](2026-05-23-actors-module-refactor-handoff.md)

然后看代码：

```text
DalamudACT/Features/Stats/LocalStatsService.Actors.cs
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs
DalamudACT/UI/Windows/SettingsWindow.cs
DalamudACT/UI/Panels/StatsPanel.cs
```

### 3. 想排查启动崩溃 / Hook 风险

先看：

- [2026-05-23 ActorControl 启动崩溃交接](2026-05-23-actorcontrol-crash-handoff.md)

然后看代码：

```text
DalamudACT/Plugin/ACT.cs
DalamudACT/Plugin/Hooks/
```

### 4. 想排查 debug 战斗记录

先看：

- [2026-05-23 debug 战斗记录友方合并交接](2026-05-23-debug-combat-log-friendly-handoff.md)
- [2026-05-21 debug 战斗记录交接](2026-05-21-debug-combat-log-handoff.md)

然后看代码：

```text
DalamudACT/Features/DebugCombatLog/DebugCombatLogWindow.cs
DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs
DalamudACT/UI/Windows/SettingsWindow.cs
```

### 5. 想排查 DoT / Wildfire / ACT 对账

先看：

- [2026-05-16 DoT 交接](2026-05-16-dot-handoff.md)
- [2026-05-14 DotReconcile 交接](2026-05-14-dotreconcile-handoff.md)

然后看：

```text
DalamudACT/Features/Stats/LocalStatsService.Dots.cs
DalamudACT/Features/Stats/PlayerDotCatalog.cs
tools/DotReconcile/Program.cs
```

### 6. 想排查 UI / 悬浮窗 / 设置页

先看：

- [2026-05-12 ikegami 样式交接](2026-05-12-ikegami-handoff.md)
- [2026-05-13 设置窗口交接](2026-05-13-settings-window-handoff.md)

然后看：

```text
DalamudACT/UI/Windows/MainWindow.cs
DalamudACT/UI/Windows/SettingsWindow.cs
DalamudACT/UI/Windows/FloatingStatsWindow.cs
DalamudACT/UI/Panels/StatsPanel.cs
DalamudACT/UI/Models/StatsModels.cs
DalamudACT/UI/Theme/JobThemePalette.cs
```

### 7. 想发版或修发布流程

先看：

- [2026-05-09 发布交接](2026-05-09-RELEASE-HANDOFF.md)
- [发布 Runbook](RELEASE-RUNBOOK.md)

然后核对：

```text
DalamudACT/DalamudACT.csproj
Data/DalamudACT.json
DalamudACT/DalamudACT.json
repo.json
.github/workflows/build.yml
.github/workflows/release.yml
```

---

## 四、当前代码结构图

```text
DalamudACT/
├─ Plugin/
│  ├─ ACT.cs
│  └─ Hooks/
├─ Infrastructure/
│  ├─ DalamudApi.cs
│  └─ Logging/LogHelper.cs
├─ Features/
│  ├─ Stats/
│  │  ├─ LocalStatsService.cs
│  │  ├─ LocalStatsService.Actors.cs
│  │  ├─ LocalStatsService.Encounter.cs
│  │  ├─ LocalStatsService.Dots.cs
│  │  ├─ LocalStatsService.DebugRecorder.cs
│  │  ├─ LocalStatsService.History.cs
│  │  ├─ LocalStatsService.Status.cs
│  │  ├─ LocalStatsService.TestData.cs
│  │  ├─ LocalStatsService.Formatting.cs
│  │  └─ PlayerDotCatalog.cs
│  ├─ CombatTimeline/CombatTimelineWindow.cs
│  └─ DebugCombatLog/DebugCombatLogWindow.cs
├─ UI/
│  ├─ PluginUI.cs
│  ├─ Windows/
│  │  ├─ MainWindow.cs
│  │  ├─ SettingsWindow.cs
│  │  └─ FloatingStatsWindow.cs
│  ├─ Panels/StatsPanel.cs
│  ├─ Models/StatsModels.cs
│  ├─ Theme/JobThemePalette.cs
│  └─ Helpers/LogUiHelper.cs
└─ Configuration/PluginConfiguration.cs
```

旧路径如 `DalamudACT/Stats/LocalStatsService.cs`、`DalamudACT/UI/StatsPanel.cs`、`DalamudACT/DalamudApi.cs` 已不是当前主入口。

---

## 五、常用外部接口文档

- Dalamud 文档首页：<https://dalamud.dev/>
- Dalamud API 参考：<https://dalamud.dev/api/>
- Lumina.Excel 仓库：<https://github.com/NotAdam/Lumina.Excel>

---

## 六、维护入口建议

如果只记一条：

> 先看 `维护首页`，再看 `第一小时清单`，然后看 `HANDOVER.md` 顶部和 `2026-05-24 NPC 队友识别交接`。

如果只想知道“现在该不该动代码”：

1. 先跑 `git status --short`；
2. 确认你理解结构拆分后的旧路径删除 / 新路径未跟踪；
3. 再决定修改哪个模块；
4. 改完跑 `dotnet build E:\git\DalamudACT\DalamudACT.sln`。
