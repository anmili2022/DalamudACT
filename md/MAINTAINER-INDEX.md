# 维护入口索引

更新时间：`2026-05-24`

用途：给继续维护 `DalamudACT` 的人一个统一入口，帮助快速判断**先看什么**、**按什么顺序看**、以及**不同任务该查哪份文档 / 哪些代码**。

---

## 相关维护文档

- [维护首页（单页总览）](MAINTAINER-HOME.md)
- [维护文档总览图](MAINTAINER-DOC-MAP.md)
- [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
- [HANDOVER.md](../HANDOVER.md)
- [SESSION-HANDOFF.md](SESSION-HANDOFF.md)
- [2026-05-24 NPC 队友识别交接](2026-05-24-npc-party-handoff.md)

---

## 一、最快入口

如果你刚接手，先看：

1. [维护首页（单页总览）](MAINTAINER-HOME.md)
2. [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
3. [HANDOVER.md](../HANDOVER.md)
4. [SESSION-HANDOFF.md](SESSION-HANDOFF.md)
5. [2026-05-24 NPC 队友识别交接](2026-05-24-npc-party-handoff.md)

如果你只想知道最新现场：直接看 `HANDOVER.md` 顶部和 `SESSION-HANDOFF.md`。

---

## 二、当前可信基线

- 工作目录：`E:\git\DalamudACT`
- 当前分支：`main`
- 当前 HEAD：`b7602c1`
- 当前版本：`0.15.2.34`
- 当前可信产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 当前工作区：结构拆分后的脏工作区，包含旧路径删除和新目录未跟踪，不是只剩一个 `1.txt`。

最近一次已验证构建命令：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

最近一次记录结果：`0 warnings / 0 errors`

---

## 三、按任务找文档和代码

### 1. 刚接手，只想进入状态

文档：

1. [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
2. [HANDOVER.md](../HANDOVER.md)
3. [SESSION-HANDOFF.md](SESSION-HANDOFF.md)
4. [2026-05-24 NPC 队友识别交接](2026-05-24-npc-party-handoff.md)

代码先不要大范围读，先确认工作区和构建。

### 2. 排查 NPC 队友不显示 / 不出行 / 被当 Boss

文档：

1. [2026-05-24 NPC 队友识别交接](2026-05-24-npc-party-handoff.md)
2. [2026-05-23 Actors 模块拆分交接](2026-05-23-actors-module-refactor-handoff.md)

代码：

```text
DalamudACT/Features/Stats/LocalStatsService.Actors.cs
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs
DalamudACT/UI/Windows/SettingsWindow.cs
DalamudACT/UI/Panels/StatsPanel.cs
```

关键函数：

```text
BuildLocalPartyHelperSnapshot()
AddPronounPartyMembersToLocalPartyHelper(...)
AddAgentHudPartyMembersToLocalPartyHelper(...)
TryGetResolvableOwnerId(...)
LooksLikeDutySupportBattleNpc(...)
TryCreateObservedFriendlyActor(...)
EncounterSession.EnsureCombatant(...)
```

### 3. 排查运行时崩溃 / 高风险 Hook

文档：

1. [2026-05-23 ActorControl 启动崩溃交接](2026-05-23-actorcontrol-crash-handoff.md)

代码：

```text
DalamudACT/Plugin/ACT.cs
DalamudACT/Plugin/Hooks/
```

注意：`ShouldInstallActorControlHook => false` 不要直接改回 `true`。

### 4. 排查 debug 战斗记录

文档：

1. [2026-05-23 debug 战斗记录友方合并交接](2026-05-23-debug-combat-log-friendly-handoff.md)
2. [2026-05-21 debug 战斗记录交接](2026-05-21-debug-combat-log-handoff.md)

代码：

```text
DalamudACT/Features/DebugCombatLog/DebugCombatLogWindow.cs
DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs
DalamudACT/UI/Windows/SettingsWindow.cs
DalamudACT/Plugin/ACT.cs
```

### 5. 排查战斗不结束 / 历史写入 / 遭遇生命周期

文档：

1. [2026-05-23 Encounter 模块拆分交接](2026-05-23-encounter-module-refactor-handoff.md)

代码：

```text
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs
DalamudACT/Features/Stats/LocalStatsService.History.cs
```

### 6. 排查 DoT / Wildfire / 对账

文档：

1. [2026-05-16 DoT 交接](2026-05-16-dot-handoff.md)
2. [2026-05-14 DotReconcile 交接](2026-05-14-dotreconcile-handoff.md)
3. [2026-05-13 DoT 对账交接](2026-05-13-dot-reconciliation-handoff.md)

代码 / 工具：

```text
DalamudACT/Features/Stats/LocalStatsService.Dots.cs
DalamudACT/Features/Stats/PlayerDotCatalog.cs
tools/DotReconcile/Program.cs
```

常用命令：

```powershell
dotnet run --project tools\DotReconcile\DotReconcile.csproj -- --latest
```

### 7. 排查 UI / 悬浮窗 / 设置页

文档：

1. [2026-05-12 ikegami 样式交接](2026-05-12-ikegami-handoff.md)
2. [2026-05-13 设置窗口交接](2026-05-13-settings-window-handoff.md)

代码：

```text
DalamudACT/UI/Windows/MainWindow.cs
DalamudACT/UI/Windows/SettingsWindow.cs
DalamudACT/UI/Windows/FloatingStatsWindow.cs
DalamudACT/UI/Panels/StatsPanel.cs
DalamudACT/UI/Models/StatsModels.cs
DalamudACT/UI/Theme/JobThemePalette.cs
```

### 8. 补 README / 使用说明 / 交接文档

优先看：

```text
README.md
md/README-SUMMARY.md
md/USAGE.md
md/MAINTAINER-HOME.md
HANDOVER.md
md/SESSION-HANDOFF.md
```

补文档时重点统一：

- 当前路径已经重构到 `Features/`、`Infrastructure/`、`UI/Windows`、`UI/Panels`；
- NPC 队友识别入口是 `md/2026-05-24-npc-party-handoff.md`；
- 当前可信产物是 `output\DalamudACT.dll`；
- ActorControl Hook 仍禁用。

### 9. 发版或修发布流程

优先看：

1. [2026-05-09 发布交接](2026-05-09-RELEASE-HANDOFF.md)
2. [发布 Runbook](RELEASE-RUNBOOK.md)
3. `repo.json`
4. `.github/workflows/build.yml`
5. `.github/workflows/release.yml`

发布前核对：

```text
DalamudACT/DalamudACT.csproj
Data/DalamudACT.json
DalamudACT/DalamudACT.json
repo.json
md/CHANGELOG.md
md/RELEASE-NOTES.md
```

注意：当前 `Data/DalamudACT.json` 和 `DalamudACT/DalamudACT.json` 版本可能不同步，发版前必须确认发布流程实际引用哪个 manifest。

---

## 四、当前目录速查

### 主入口

```text
DalamudACT/Plugin/ACT.cs
DalamudACT/UI/PluginUI.cs
```

### 基础设施

```text
DalamudACT/Infrastructure/DalamudApi.cs
DalamudACT/Infrastructure/Logging/LogHelper.cs
```

### 统计模块

```text
DalamudACT/Features/Stats/LocalStatsService.cs
DalamudACT/Features/Stats/LocalStatsService.Actors.cs
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs
DalamudACT/Features/Stats/LocalStatsService.Dots.cs
DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs
DalamudACT/Features/Stats/LocalStatsService.History.cs
DalamudACT/Features/Stats/LocalStatsService.Status.cs
DalamudACT/Features/Stats/LocalStatsService.TestData.cs
DalamudACT/Features/Stats/LocalStatsService.Formatting.cs
DalamudACT/Features/Stats/PlayerDotCatalog.cs
```

### UI 模块

```text
DalamudACT/UI/Windows/MainWindow.cs
DalamudACT/UI/Windows/SettingsWindow.cs
DalamudACT/UI/Windows/FloatingStatsWindow.cs
DalamudACT/UI/Panels/StatsPanel.cs
DalamudACT/UI/Models/StatsModels.cs
DalamudACT/UI/Theme/JobThemePalette.cs
DalamudACT/UI/Helpers/LogUiHelper.cs
```

### 功能窗口

```text
DalamudACT/Features/CombatTimeline/CombatTimelineWindow.cs
DalamudACT/Features/DebugCombatLog/DebugCombatLogWindow.cs
```

---

## 五、常用外部接口文档

- Dalamud 文档首页：<https://dalamud.dev/>
- Dalamud API 参考：<https://dalamud.dev/api/>
- Lumina.Excel 仓库：<https://github.com/NotAdam/Lumina.Excel>

快速对应：

- `PluginService`、窗口/UI、命令、状态、`IDataManager`：优先查 Dalamud；
- `GetExcelSheet<T>()`、`ExcelSheet<T>`、`Lumina.Excel.Sheets.*`：优先查 Lumina.Excel；
- `PronounModule`、`AgentHUD`、native object：核对 FFXIVClientStructs 当前签名，避免不存在的托管重载。

---

## 六、接手最容易踩坑的点

不要：

```powershell
git reset --hard
git checkout -- .
```

也不要：

- 删除 `1.txt`；
- 批量删除未跟踪新目录；
- 把旧路径显示 `D` 当成必须恢复；
- 直接恢复 ActorControl Hook；
- 使用 `PronounModule.ResolvePlaceholder(string, byte, byte)`；
- 把所有 `OwnerId != 0` 的 BattleNpc 都归属给玩家；
- 单靠 `StatusFlags.Hostile` 判断 NPC 队友；
- 不构建就交付 DLL。
