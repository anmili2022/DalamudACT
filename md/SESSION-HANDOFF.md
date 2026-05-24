# SESSION HANDOFF

## 2026-05-24 当前会话交接摘要（整理版）

完整详细交接请看：[`md/2026-05-24-npc-party-handoff.md`](2026-05-24-npc-party-handoff.md)

> 原会话交接文件发生 `?` 乱码，已重建为可读摘要。本次整理只改文档，不改代码。

### 当前基线

- 工作目录：`E:\git\DalamudACT`
- 当前分支：`main`
- 当前 HEAD：`b7602c1`
- 当前可信产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 当前 DLL 版本：`0.15.2.34`
- 当前工作区：大量结构拆分后的脏工作区；不要 reset / checkout / 清理未跟踪文件。

已验证：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

结果：

```text
已成功生成。
0 个警告
0 个错误
```

### 本轮重点

- NPC 队友识别；
- 友方 / 敌方 NPC 分类；
- `PronounModule <1>~<8>`、`AgentHUD.PartyMembers`、`PartyList`、`BuddyList`、`ObjectTable` 多路径队伍快照；
- `OwnerId` 不再一律归属到玩家；
- `StatusFlags.Hostile` 不再作为 NPC 队友唯一判断依据；
- 设置页新增 / 整理 `NPC 队友识别名单`；
- 悬浮窗支持按 `player / friendlyNpc / hostileNpc` 筛选显示；
- debug 战斗记录与战斗流水默认关闭；
- ActorControl Hook 仍保持禁用。

### 当前最重要的入口文件

```text
DalamudACT/Plugin/ACT.cs
DalamudACT/Features/Stats/LocalStatsService.Actors.cs
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs
DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs
DalamudACT/Configuration/PluginConfiguration.cs
DalamudACT/UI/Windows/SettingsWindow.cs
DalamudACT/UI/Panels/StatsPanel.cs
```

### 复测优先顺序

1. 加载 `output\DalamudACT.dll`；
2. 打开 `设置 -> NPC 队友识别名单`；
3. 确认玩家类型是 `玩家`，NPC 队友类型是 `友方NPC`；
4. 进入信赖 / 单人任务 / NPC 同行场景；
5. 在 `玩家 + 友方 NPC` 模式下确认 NPC 能在 DPS / HPS / 承伤中出行；
6. 确认 Boss 不被当成友方 NPC；
7. 确认战斗能正常结束。

### 禁止事项

```powershell
git reset --hard
git checkout -- .
```

不要：

- 删除 `1.txt`；
- 批量删除未跟踪目录；
- 直接恢复 ActorControl Hook；
- 使用 `PronounModule.ResolvePlaceholder(string, byte, byte)`；
- 把所有 `OwnerId != 0` 的 BattleNpc 都当宠物归属；
- 单靠 `StatusFlags.Hostile` 判断 NPC 队友。

### 下一步

如果用户继续反馈 NPC 队友不出行，优先按下面顺序查：

1. `GetCurrentPartyMemberDisplayInfos()` 是否能在设置页列出 NPC；
2. `BuildLocalPartyHelperSnapshot()` 中是哪个来源拿到了 / 没拿到 NPC；
3. combat event 的 `sourceId` 是否对应 `EntityId / ObjectId / GameObjectId low32`；
4. `observedFriendlyActorCache` 是否写入；
5. `EncounterSession.EnsureCombatant(...)` 是否创建了 `friendlyNpc` combatant；
6. `StatsPanel` 是否被显示模式过滤。
