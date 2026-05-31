# SESSION HANDOFF

## 2026-05-31 时间轴 / 发布 / TTS 交接摘要

### 当前基线

- 工作目录：`E:\git\DalamudACT`
- 当前分支：`main`
- 当前 HEAD：`7e7ebd1 fix: update klythios timeline`
- 最新正式发布：`0.15.2.50`
- Release URL：`https://github.com/anmili2022/DalamudACT/releases/tag/0.15.2.50`
- 当前工作区仍有未提交改动，见“未提交内容”。
- 未跟踪 `1.txt` 仍存在，不要提交。

### 已发布内容

- `0.15.2.49` 已发布，但发布包缺少内置时间轴数据，正式安装后会提示“当前区域没有时间轴”。
- `0.15.2.50` 是热修版，已成功发布并替代 `0.15.2.49` 使用。
- `0.15.2.50` 修复发布包归档，打包时复制 `DalamudACT/Features/Timeline/Data` 到 `output/Timeline/Data`。
- `0.15.2.50` 恢复运行时读取 `AppContext.BaseDirectory/Timeline/Data`，正式安装后可以加载内置时间轴。
- `0.15.2.50` Release 资产：`DalamudACT.zip`，大小约 `604 KB`。
- `0.15.2.50` 资产 sha256：`ad34a7a6a5647e390d3ba32f786ef8ed54ea278d152ab73dabcfc0f8d790f3a3`。

### 已提交内容

- `a3085f9 chore: release 0.15.2.50`
- 修复发布包缺失 `Timeline/Data` 的问题。
- 更新版本号和仓库 metadata 到 `0.15.2.50`。
- 更新 release notes，注明修复“当前区域没有时间轴”。

- `7e7ebd1 fix: update klythios timeline`
- 只提交了 `DalamudACT/Features/Timeline/Data/07-dt/dungeon/klythios.cn.txt`。
- 在老 1 `装甲之眼` 时间轴末尾新增 4 条：`导弹发射`、`对地导弹`、`石化光束`、`动态扫描仪`。

### 克吕提俄斯时间轴状态

- 源码时间轴文件：`DalamudACT/Features/Timeline/Data/07-dt/dungeon/klythios.cn.txt`
- `7DC` 封锁同步已改为正确 `param1`：
- 花冠广场：`1583`
- 材料存放场：`1584`
- 兵器试验场：`1585`
- 老 1 `装甲之眼` 当前按用户提供日志校准，开战基准仍是 `1000.0`。
- 老 1 当前已包含 `凶眼注目`、`石化光束`、`动态扫描仪`、`导弹发射`、`对人导弹`、`对地导弹` 等条目。
- 时间轴要求仍是只写 `Ability` 结算/生效条目，不写 `StartsUsing`。

### SystemLogMessage 同步状态

- 已放弃硬编码中文“封锁/被封锁”文本过滤。
- 当前基于 Lumina `LogMessage` 表的 `ToMacroString()` 生成匹配模式。
- `<sheet(PlaceName,lnum1,0)>` 会用 `param1` 反查 `PlaceName` 当前客户端语言文本。
- `SystemLogMessage` 同步不再顺序回落，模板不匹配就不同步。
- 插件加载后有 3 秒冷却，避免聊天历史回放误同步。
- `jump 0` 的 `SystemLogMessage` reset 条目允许绕过 `lastSystemLogSyncTimeSeconds` 时间过滤。
- `ActorControl Hook` 仍保持禁用。

### 时间轴加载策略

- 用户配置目录优先：`pluginConfigs\DalamudACT\Timeline\Data`
- 开发环境会向上查找源码目录：`DalamudACT\Features\Timeline\Data`
- 正式安装读取发布包目录：`AppContext.BaseDirectory\Timeline\Data`
- 在线缓存作为后备：`TimelineRemoteResourceDownloader.GetCacheRootDirectory()`
- 不要再依赖 `output\Timeline\Data` 作为开发来源。
- 发布包必须包含 `Timeline/Data`，否则正式安装会再次提示“当前区域没有时间轴”。

### 当前未提交内容

- `DalamudACT/Configuration/PluginConfiguration.cs`
- `DalamudACT/Configuration/PluginConfiguration.Reset.cs`
- 已把默认 TTS 纠错 `AOE -> 范围攻击` 改成 `AOE -> 诶欧意`。
- 已同步修改默认初始化、`EnsureTimelineTtsCorrections()` 和重置配置。

- `tools/TimelineDraftTool/Program.cs`
- 已修复时间轴生成器漏掉 ACT 网络日志 `22` 多目标 Ability 事件的问题。
- 原因：`凶眼注目` 在 `D:\ff14act\FFXIVLogs\Network_30109_20260531.log` 里是 `20` 读条 + `22` 多目标结算，不是 `21`。
- 当前修改是在 `ParseLog` switch 中加入：`case "22": TryAddAbility(current, parts, timestamp); break;`

- `1.txt`
- 未跟踪文件。
- 之前内容为 `codex --dangerously-bypass-approvals-and-sandbox`。
- 不应提交。

### 已验证

- `dotnet build --no-restore` 通过，0 警告 0 错误。
- `TimelineDraftTool` 正常输出目录曾因当前正在运行的 `TimelineDraftTool.exe` 锁住 `output\TimelineDraftTool\TimelineDraftTool.exe` 而构建失败。
- 用临时输出目录验证生成器构建通过：`dotnet build --no-restore tools\TimelineDraftTool\TimelineDraftTool.csproj -p:OutputPath=C:\Users\ADMINI~1\AppData\Local\Temp\opencode\TimelineDraftToolBuild\`
- 临时输出目录构建结果：0 警告 0 错误。

### 下次优先事项

- 如果要继续当前未提交改动，优先提交 TTS 纠错和生成器 `22` 事件解析。
- 提交前确认是否也要发布新版本；如果发布，版本应从 `0.15.2.50` 递增。
- 若用户只想本地使用，可不发布，只提交 `fix: parse timeline draft multi-target abilities` 和 `fix: update AOE TTS correction`。
- 重新生成 `D:\ff14act\FFXIVLogs\Network_30109_20260531.log` 时，确认 `BF00 凶眼注目` 能出现在生成结果里。
- 如果继续校准克吕提俄斯老 1，优先用 `Ability` 结算时间，不用 `StartsUsing`。

### 禁止事项

- 不要 `git reset --hard`。
- 不要 `git checkout -- .`。
- 不要提交 `1.txt`。
- 不要恢复 `ActorControl Hook`。
- 不要更改时间轴硬编码源码路径：`E:\git\DalamudACT\DalamudACT\Features\Timeline\Data`。
- 不要把本机绝对路径 `E:\git\DalamudACT\...` 写进发布运行时代码。
- 不要再次从发布包里移除 `Timeline/Data`。

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
