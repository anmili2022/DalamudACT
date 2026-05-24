# 维护首页（单页总览）

更新时间：`2026-05-24`

用途：这是 `DalamudACT` 的**一页式维护首页**。如果你刚接手项目，优先看这一页，先确认当前现场、最新交接、关键代码入口和禁止事项。

---

## 相关维护文档

- [维护文档总览图](MAINTAINER-DOC-MAP.md)
- [维护入口索引](MAINTAINER-INDEX.md)
- [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
- [HANDOVER.md](../HANDOVER.md)
- [SESSION-HANDOFF.md](SESSION-HANDOFF.md)
- [2026-05-24 NPC 队友识别交接](2026-05-24-npc-party-handoff.md)
- [ActorControl 启动崩溃交接](2026-05-23-actorcontrol-crash-handoff.md)

---

## 目录 / TOC

- [30 秒结论](#home-summary)
- [当前可信快照](#home-snapshot)
- [5 分钟先看什么](#home-5min)
- [当前状态一眼看懂](#home-status)
- [按任务快速分流](#home-routing)
- [最常看的代码入口](#home-code)
- [外部接口文档](#home-refs)
- [最容易踩坑的点](#home-pitfalls)
- [建议下一步](#home-next)

---

<a id="home-summary"></a>
## 一、30 秒结论

当前项目已经从早期外部页面壳，转成：

- **Dalamud 内采集**；
- **插件内统计**；
- **游戏内 UI 展示**；
- 支持 `DPS / HPS / 承伤 / 概览 / 历史记录`；
- 当前重点是 **NPC 队友识别、友方/敌方 NPC 分类、debug 记录可用性、DoT/持续效果对账**。

当前不是干净仓库。已经完成一轮结构拆分：旧路径显示为删除，新路径大量未跟踪。**不要用 reset / checkout / 清理未跟踪文件来“整理现场”。**

---

<a id="home-snapshot"></a>
## 二、当前可信快照

以本次整理时的实际输出为准：

- 工作目录：`E:\git\DalamudACT`
- 当前分支：`main`
- 当前 HEAD：`b7602c1`
- 当前版本：`0.15.2.34`
- 当前可信产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 最近一次已验证构建：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

- 最近一次结果：`0 warnings / 0 errors`
- 当前工作区：脏工作区，包含结构拆分后的修改、旧路径删除、新路径未跟踪，以及文档整理改动。

当前 `git status --short` 的特征不是“只剩 `1.txt`”，而是包括：

```text
D DalamudACT/Stats/LocalStatsService.cs
D DalamudACT/UI/StatsPanel.cs
D DalamudACT/UI/SettingsWindow.cs
?? DalamudACT/Features/
?? DalamudACT/Infrastructure/
?? DalamudACT/UI/Panels/
?? DalamudACT/UI/Windows/
?? md/2026-05-24-npc-party-handoff.md
?? 1.txt
```

说明：这些 `D` 多数是结构拆分造成的旧路径删除，不代表应该恢复旧文件。

---

<a id="home-5min"></a>
## 三、如果你只有 5 分钟，先看什么

按这个顺序：

1. [HANDOVER.md](../HANDOVER.md)
2. [SESSION-HANDOFF.md](SESSION-HANDOFF.md)
3. [2026-05-24 NPC 队友识别交接](2026-05-24-npc-party-handoff.md)
4. [ActorControl 启动崩溃交接](2026-05-23-actorcontrol-crash-handoff.md)

至少确认：

- 当前仓库现场是否仍是结构拆分后的脏工作区；
- 当前可信产物是不是 `output\DalamudACT.dll`；
- 当前版本是不是 `0.15.2.34`；
- 你要改的是 NPC 队友识别、debug 记录、DoT，对应入口分别在哪里；
- ActorControl Hook 仍不能直接打开。

---

<a id="home-status"></a>
## 四、当前状态：一眼看懂

### 已基本完成

- 插件主链路已转为本地统计；
- `ActionEffect` 主统计链路可构建；
- 历史记录与回看链路已存在；
- `DPS / HPS / 承伤` 共享列显示设置；
- 统计页 / 历史页列宽配置持久化；
- UI 已拆到 `UI/Windows`、`UI/Panels`、`UI/Models`、`UI/Theme`；
- 统计核心已拆到 `Features/Stats/LocalStatsService.*.cs`；
- debug 战斗记录 UI 已合并“自己 / 队友”为“友方”；
- NPC 队友识别已接入 `PronounModule <1>~<8>`、`AgentHUD.PartyMembers`、`PartyList`、`BuddyList`、`ObjectTable` 多来源；
- 当前构建通过：`0 warnings / 0 errors`。

### 正在验证

- NPC 队友是否在真实信赖 / 单人任务 / 幻体场景中稳定独立成行；
- `OwnerId` 与宠物 / NPC 队友的边界；
- `StatusFlags.Hostile` 与友方 NPC 分类边界；
- debug 战斗记录在真实机制中的完整性；
- 战斗结束判定是否会被 NPC / observed actor 拖住；
- DoT / HoT / Wildfire 等持续效果长期对账。

### 仍不应贸然恢复

- `ActorControl` Hook；
- `Cast` Hook；
- 任何会触发原生内存读崩溃的 HookFromAddress 路径。

---

<a id="home-routing"></a>
## 五、按任务快速分流

### 1. 刚接手维护

先看：

1. [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
2. [HANDOVER.md](../HANDOVER.md)
3. [SESSION-HANDOFF.md](SESSION-HANDOFF.md)
4. [2026-05-24 NPC 队友识别交接](2026-05-24-npc-party-handoff.md)

### 2. 排查 NPC 队友不出行 / 分类错误

先看：

1. [2026-05-24 NPC 队友识别交接](2026-05-24-npc-party-handoff.md)
2. `DalamudACT/Features/Stats/LocalStatsService.Actors.cs`
3. `DalamudACT/Features/Stats/LocalStatsService.Encounter.cs`
4. `DalamudACT/UI/Windows/SettingsWindow.cs`
5. `DalamudACT/UI/Panels/StatsPanel.cs`

### 3. 排查崩溃 / 高风险 Hook

先看：

1. [ActorControl 启动崩溃交接](2026-05-23-actorcontrol-crash-handoff.md)
2. `DalamudACT/Plugin/ACT.cs`
3. `DalamudACT/Plugin/Hooks/`

### 4. 排查 debug 战斗记录

先看：

1. [2026-05-23 debug 战斗记录友方合并交接](2026-05-23-debug-combat-log-friendly-handoff.md)
2. `DalamudACT/Features/DebugCombatLog/DebugCombatLogWindow.cs`
3. `DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs`
4. `DalamudACT/UI/Windows/SettingsWindow.cs`

### 5. 排查 DoT / 对账

先看：

1. [2026-05-16 DoT 交接](2026-05-16-dot-handoff.md)
2. `DalamudACT/Features/Stats/LocalStatsService.Dots.cs`
3. `DalamudACT/Features/Stats/PlayerDotCatalog.cs`
4. `tools/DotReconcile/Program.cs`

---

<a id="home-code"></a>
## 六、最常看的代码入口

### 插件主链路

```text
DalamudACT/Plugin/ACT.cs
DalamudACT/Plugin/Hooks/
DalamudACT/Infrastructure/DalamudApi.cs
DalamudACT/Infrastructure/Logging/LogHelper.cs
DalamudACT/UI/PluginUI.cs
```

### 统计核心

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

### UI

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

<a id="home-refs"></a>
## 七、常用外部接口文档

- Dalamud 文档首页：<https://dalamud.dev/>
- Dalamud API 参考：<https://dalamud.dev/api/>
- Lumina.Excel 仓库：<https://github.com/NotAdam/Lumina.Excel>

快速判断：

- 查 `PluginService`、窗口/UI、命令、状态、`IDataManager` 等接口，优先看 Dalamud 文档 / API；
- 查 `GetExcelSheet<T>()`、`ExcelSheet<T>`、`Lumina.Excel.Sheets.*`，优先看 Lumina.Excel；
- 查 `PronounModule`、`AgentHUD`、native object 时，优先核对 FFXIVClientStructs 当前运行时签名，避免使用不存在的托管便捷重载。

---

<a id="home-pitfalls"></a>
## 八、最容易踩坑的点

不要做：

```powershell
git reset --hard
git checkout -- .
```

也不要：

- 删除 `1.txt`；
- 批量删除 `DalamudACT/Features/`、`Infrastructure/`、`UI/Windows/` 等未跟踪新目录；
- 把旧路径 `DalamudACT/Stats/LocalStatsService.cs` 当成当前主文件；
- 直接恢复 `ActorControl` Hook；
- 使用 `PronounModule.ResolvePlaceholder(string, byte, byte)`；
- 把所有 `OwnerId != 0` 的 `BattleNpc` 都归属给玩家；
- 单靠 `StatusFlags.Hostile` 判定 NPC 队友；
- 忽略 `DalamudACT/DalamudACT.json` 与 `Data/DalamudACT.json` 可能不同步的问题。

---

<a id="home-next"></a>
## 九、建议下一步

1. 先让用户用当前 `output\DalamudACT.dll` 复测；
2. 优先验证 `设置 -> NPC 队友识别名单 -> 当前队伍成员`；
3. 如果 UI 能看到 NPC，再验证 NPC 是否出现在 DPS / HPS / 承伤；
4. 如果 UI 看不到 NPC，查 `PronounModule` / `AgentHUD`；
5. 如果 UI 能看到但战斗不出行，查事件 `sourceId` 与 `EntityId / ObjectId / GameObjectId low32`；
6. 如果战斗不结束，查 `LocalStatsService.Encounter.cs` 的脱战判定；
7. 修改后必须跑：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

---

## 十、编码防乱码规范

后续改中文文档前请先看：

```text
md/ENCODING-GUIDE.md
```

提交前建议运行：

```powershell
powershell -ExecutionPolicy Bypass -File tools\Check-TextEncoding.ps1
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

特别注意：不要把包含中文的 here-string 通过管道传给 Python，例如 `<中文 here-string> | python -`，这在 Windows PowerShell 下可能把中文提前降级成 `?`。
