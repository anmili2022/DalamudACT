# SESSION HANDOFF

## 2026-06-11 0.15.2.68 紧急性能发布收工记录

### 当前基线

- 工作目录：`E:\git\DalamudACT`
- 当前分支：`main`
- 最新正式发布：`0.15.2.68`
- Release URL：`https://github.com/anmili2022/DalamudACT/releases/tag/0.15.2.68`
- Release commit：`2c0a5358bac940f59ef95522c2b21c0299da0561`
- Tag：`0.15.2.68`
- Release asset：`DalamudACT.zip`
- Asset size：`640773` bytes
- Asset SHA256：`54960733c2317f9170ce8e57f7711ca2a87a9ac58de83053c985eac9d4b998d0`
- GitHub Actions：
  - `.NET Build` 成功，run id：`27337599330`
  - `Create Release` 成功，run id：`27337604419`
- 订阅源仓库 `E:\git\MyDalamudRepo` 已同步并推送：
  - commit：`75ba9138455750f230921c3d193b0fd95ca737a2`
  - 远端 raw 已确认 `DalamudACT` 为 `0.15.2.68`
  - 下载链接：`https://github.com/anmili2022/DalamudACT/releases/download/0.15.2.68/DalamudACT.zip`

### 本轮背景

用户反馈：

```text
老大 你的dps统计那个插件性能是不是有问题，昨天上次更新了之后，绝妖星打的时候会卡到个位数帧率，卸掉就没事了 应该还有其他更严重的问题
```

判断为高优先级副本内主线程性能事故，优先降低绝本 / 高压副本中的默认后台负载，而不是把问题归因到用户机器。

### 已完成优化

- 副本内自动刷新策略从低延迟降回标准间隔，避免副本里默认 100ms / 250ms 高频刷新。
- 副本内默认跳过时间轴 `ActionEffect / StartsUsing` 重型同步路径，避免高频技能包、Boss 多段 AoE 和读条轮询反复触发时间轴全表扫描。
- `TimelineService` 加运行时索引：
  - visible / in-combat / map effect / npc yell / system log 分类索引；
  - `AbilityByActionId`；
  - `StartsUsingByActionId`；
  - `StartsUsingResponsesByActionId`；
  - 时间轴 TTS、同步响应和可见条目刷新减少重复 LINQ 扫描与分配。
- 时间轴窗口可见条目增加短缓存，降低窗口打开时每帧排序、过滤和列表分配。
- 副本内战斗流水采用轻量口径，保留开战、结束、死亡、场地特效、头顶标记和连线，跳过高频伤害 / 治疗 / 状态流水。
- 队友技能监控窗口不再每帧重建成员和技能显示列表，改为复用缓存状态并动态计算倒计时。
- 状态监控缓存状态名称和图标，避免刷新时重复查询 Excel sheet。
- 强化日志关闭时不再运行 `ActionEffect`、`Framework`、统计和 DoT 的性能分段计时，减少默认路径上的 `Stopwatch` 和临时列表开销。
- 副本内战斗对象轮询间隔上调，作为高压场景保护。
- 聊天、命令和 Hook 回调在插件释放过程中提前返回，降低卸载 / 热重载时继续处理事件的风险。
- 状态监控悬浮窗默认禁用滚动条，减少窄窗口或折叠状态下的布局干扰。

### 已更新文件

发布 commit `2c0a5358` 中包含：

- `DalamudACT/Configuration/PluginConfiguration.cs`
- `DalamudACT/DalamudACT.csproj`
- `DalamudACT/DalamudACT.json`
- `Data/DalamudACT.json`
- `repo.json`
- `md/CHANGELOG.md`
- `md/RELEASE-NOTES.md`
- `DalamudACT/Features/PartyMonitor/PartyMonitorService.cs`
- `DalamudACT/Features/PartyMonitor/PartyMonitorWindow.cs`
- `DalamudACT/Features/Stats/LocalStatsService.Dots.cs`
- `DalamudACT/Features/Stats/LocalStatsService.Encounter.Timeline.cs`
- `DalamudACT/Features/Stats/LocalStatsService.Encounter.cs`
- `DalamudACT/Features/StatusObserver/StatusObserverService.cs`
- `DalamudACT/Features/StatusObserver/StatusObserverWindow.cs`
- `DalamudACT/Features/Timeline/TimelineService.cs`
- `DalamudACT/Plugin/ACT.ActionEffect.cs`
- `DalamudACT/Plugin/ACT.Chat.cs`
- `DalamudACT/Plugin/ACT.Commands.cs`
- `DalamudACT/Plugin/ACT.Hooks.cs`
- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/UI/Windows/SettingsWindow.Maintenance.cs`

订阅源同步 commit `75ba9138` 中只改了：

- `MyDalamudRepo/pluginmaster.json`

### 已验证

本地验证：

```powershell
dotnet build --no-restore
dotnet build .\DalamudACT\DalamudACT.csproj --configuration Release --no-restore -p:Version=0.15.2.68 -p:FileVersion=0.15.2.68 -p:AssemblyVersion=0.15.2.68
powershell -ExecutionPolicy Bypass -File tools\Check-TextEncoding.ps1 -All
git diff --check
```

结果：

- 构建通过，`0` 警告，`0` 错误。
- 编码检查通过：`Text encoding check passed: all repository text files, checked 263 files.`
- 空白检查通过。
- Release 正文已确认 `{{VERSION}}` 正确渲染为 `0.15.2.68`。
- Release asset digest 与本地下载校验一致。
- 发布包已确认包含 `Timeline/Data/timeline-index.json` 和内置时间轴目录。
- 远端 `MyDalamudRepo/main/pluginmaster.json` 已确认 `DalamudACT` 版本和下载链接均指向 `0.15.2.68`。

### 订阅源同步注意

- `MyDalamudRepo` 的自动同步脚本本轮在处理无关插件 `RouletteRecorder.Dalamud` 时失败：

```text
Unable to fetch 'RouletteRecorder.Dalamud/RouletteRecorder.Dalamud.json' from anmili2022/RouletteRecorder.Dalamud using refs: v1.0.7.1, master.
```

- 该失败没有写入 `pluginmaster.json`。
- 为避免阻塞本次 `DalamudACT` 紧急性能发布，本轮只手动同步 `DalamudACT` 这一条订阅源记录，其它插件没有纳入提交。
- 后续若要恢复 `MyDalamudRepo` 全量自动同步，需要单独修 `RouletteRecorder.Dalamud` 的 manifest 路径或同步脚本容错。

### 当前工作区状态

`DalamudACT` 主仓库在发布后只剩未跟踪现场文件，未提交也未删除：

```text
1.txt
tools/CactbotTimelineExtractor/test_output.txt
tools/CactbotTimelineExtractor/test_output2.txt
tools/时间轴预览工具.rar
打工计时器.html
```

这些文件不属于本次发布内容，下一位维护者不要误删、不要误提交。

`MyDalamudRepo` 在订阅源提交推送后为干净工作区。

### 下次优先事项

1. 让反馈用户更新到 `0.15.2.68`，优先在绝妖星 / 同等高压副本复测帧率。
2. 如果仍卡顿：
   - 先开高性能模式复测；
   - 再分别关闭时间轴、状态监控、完整战斗流水做对比；
   - 然后开启强化日志，收集卡顿前后的 `ActionEffect`、`Framework`、时间轴和 DoT 慢包日志。
3. 观察副本内停用部分时间轴重型自动同步后的副作用：
   - 个别依赖 Ability / StartsUsing 自动重同步的时间轴提示可能不如之前激进；
   - 若要恢复，必须先确认不会再次造成绝本主线程卡顿。
4. 单独处理 `MyDalamudRepo` 自动同步脚本的 `RouletteRecorder.Dalamud` manifest 路径问题。

### 禁止事项

- 不要重打或移动 tag `0.15.2.68`。
- 如果必须重建同一 tag 的发布包，优先使用 release workflow 的 `workflow_dispatch`，不要删除重建 tag。
- 不要恢复副本内时间轴重型 `ActionEffect / StartsUsing` 自动同步，除非已有实测证明不会再导致绝本掉帧。
- 不要把副本内自动刷新默认值改回低延迟。
- 不要提交或删除 `1.txt` 和其它未跟踪现场文件。
- 不要执行 `git reset --hard` 或 `git checkout -- .` 清理现场。

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
