# 下一位维护者第一小时清单

更新时间：`2026-05-24`

用途：适合刚接手这个仓库时使用，帮助你在 **1 小时内** 快速搞清楚当前现场、不要乱动的地方、以及下一步该从哪里下手。

---

## 相关维护文档

- [维护首页（单页总览）](MAINTAINER-HOME.md)
- [维护文档总览图](MAINTAINER-DOC-MAP.md)
- [维护入口索引](MAINTAINER-INDEX.md)
- [HANDOVER.md](../HANDOVER.md)
- [SESSION-HANDOFF.md](SESSION-HANDOFF.md)
- [2026-05-24 NPC 队友识别交接](2026-05-24-npc-party-handoff.md)

---

## 0. 先记住当前快照

当前整理时的实际状态：

- 仓库目录：`E:\git\DalamudACT`
- 当前分支：`main`
- 当前 HEAD：`b7602c1`
- 当前版本：`0.15.2.34`
- 当前可信产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 当前工作区不是干净状态，也不是只剩 `1.txt`；它包含结构拆分后的大量旧路径删除和新目录未跟踪。

接手后先执行：

```powershell
git -C E:\git\DalamudACT status --short
git -C E:\git\DalamudACT rev-parse --short HEAD
```

第一原则：**不要一上来做破坏性 Git 操作。**

尤其不要：

```powershell
git reset --hard
git checkout -- .
```

---

## 1. 第 0～10 分钟：先看这 4 份文档

按顺序读：

1. [HANDOVER.md](../HANDOVER.md)
2. [SESSION-HANDOFF.md](SESSION-HANDOFF.md)
3. [2026-05-24 NPC 队友识别交接](2026-05-24-npc-party-handoff.md)
4. [ActorControl 启动崩溃交接](2026-05-23-actorcontrol-crash-handoff.md)

你要先回答：

- 当前可信 DLL 是不是 `output\DalamudACT.dll`；
- 当前版本是不是 `0.15.2.34`；
- 当前工作区为什么有大量 `D` 和 `??`；
- 当前要优先验证 NPC 队友、debug 记录、DoT，还是发布元数据；
- ActorControl Hook 为什么不能直接打开。

---

## 2. 第 10～20 分钟：确认目录结构已经重构

旧路径已经不是当前主入口：

```text
DalamudACT/Stats/LocalStatsService.cs
DalamudACT/UI/StatsPanel.cs
DalamudACT/UI/SettingsWindow.cs
DalamudACT/UI/FloatingStatsWindow.cs
DalamudACT/DalamudApi.cs
```

当前对应新路径：

```text
DalamudACT/Features/Stats/LocalStatsService*.cs
DalamudACT/UI/Panels/StatsPanel.cs
DalamudACT/UI/Windows/SettingsWindow.cs
DalamudACT/UI/Windows/FloatingStatsWindow.cs
DalamudACT/Infrastructure/DalamudApi.cs
```

如果你看到旧路径在 `git status` 中显示 `D`，不要立刻恢复它们；先确认新路径文件是否存在。

---

## 3. 第 20～35 分钟：只抓当前关键代码入口

### 插件入口 / Hook

```text
DalamudACT/Plugin/ACT.cs
DalamudACT/Plugin/Hooks/
```

看点：

- `ActionEffect` 主链路；
- `ShouldInstallActorControlHook => false`；
- `HandleAbility(...)` 中 Damage / Heal 口径；
- debug 友方技能记录入口。

### 统计 / NPC 队友

```text
DalamudACT/Features/Stats/LocalStatsService.Actors.cs
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs
```

看点：

- `BuildLocalPartyHelperSnapshot()`；
- `PronounModule <1>~<8>`；
- `AgentHUD.PartyMembers`；
- `observedFriendlyActorCache`；
- `OwnerId` 与 `FriendlyNpc` / `HostileNpc` 分类；
- `EncounterSession.EnsureCombatant(...)`。

### UI

```text
DalamudACT/UI/Windows/SettingsWindow.cs
DalamudACT/UI/Panels/StatsPanel.cs
```

看点：

- `NPC 队友识别名单`；
- `悬浮对象显示`；
- `player / friendlyNpc / hostileNpc` 过滤；
- NPC 行高亮与 badge。

---

## 4. 第 35～45 分钟：按问题类型走最短路径

### NPC 队友没显示

先看：

```text
md/2026-05-24-npc-party-handoff.md
DalamudACT/Features/Stats/LocalStatsService.Actors.cs
DalamudACT/UI/Windows/SettingsWindow.cs
```

优先确认设置页 `当前队伍成员` 是否能看到 NPC。

### UI 能看到 NPC，但战斗统计不出行

先看：

```text
TryGetTrackedActor(...)
TryResolveTrackedSource(...)
ObserveFriendlyCombatantFromGameObject(...)
ObserveFriendlyCombatantIdentity(...)
EncounterSession.EnsureCombatant(...)
```

重点核对 combat event 的 `sourceId` 与 `EntityId / ObjectId / GameObjectId low32`。

### 战斗不结束

先看：

```text
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs
```

重点查脱战判定是否被 NPC / Buddy / observed actor 拖住。

### debug 战斗记录没内容

先确认：

```text
DebugCombatRecordingEnabled = true
```

再看：

```text
DalamudACT/Features/DebugCombatLog/DebugCombatLogWindow.cs
DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs
```

### DoT 对账

先看：

```text
DalamudACT/Features/Stats/LocalStatsService.Dots.cs
DalamudACT/Features/Stats/PlayerDotCatalog.cs
tools/DotReconcile/Program.cs
```

---

## 5. 第 45～55 分钟：跑构建

每轮改完至少跑：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

当前最近一次已验证结果：

```text
已成功生成。
0 个警告
0 个错误
```

确认 DLL 版本：

```powershell
[Reflection.AssemblyName]::GetAssemblyName('E:\git\DalamudACT\output\DalamudACT.dll').Version.ToString()
```

当前预期：

```text
0.15.2.34
```

---

## 6. 第 55～60 分钟：交付前确认

交付前至少确认：

- 没有执行破坏性 Git 操作；
- 没有误删 `1.txt`；
- 没有删除未跟踪新目录；
- 没有直接恢复 ActorControl Hook；
- 没有使用 `PronounModule.ResolvePlaceholder(string, byte, byte)`；
- 构建通过；
- 如果改了版本或发布元数据，同步检查：

```text
DalamudACT/DalamudACT.csproj
Data/DalamudACT.json
DalamudACT/DalamudACT.json
repo.json
md/CHANGELOG.md
md/RELEASE-NOTES.md
```

---

## 7. 编码检查

本仓库中文文档统一按 UTF-8 维护。继续写 README / HANDOVER / 交接文档前，先看：

```text
md/ENCODING-GUIDE.md
```

提交或交付前建议运行：

```powershell
powershell -ExecutionPolicy Bypass -File tools\Check-TextEncoding.ps1
```

禁止把中文 here-string 通过管道传给 Python；这类写法可能导致文件内容层面直接变成连续问号，无法靠重新指定编码恢复。
