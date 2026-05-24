# 2026-05-24 NPC 队友识别与 UI 交接（整理版）

> 整理说明：原文件中的大量中文已经在文件内容层面变成 `?`，不是终端编码问题，无法通过重新指定编码恢复。本文件依据当前源码、README、HANDOVER 残留关键信息和本地构建结果重建为可读交接。

## 1. 当前基线

- 工作目录：`E:\git\DalamudACT`
- 当前分支：`main`
- 当前 HEAD：`b7602c1`
- 当前可信产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 当前程序集版本：`0.15.2.34`
- 当前工作区：仍是脏工作区，包含结构拆分后的大量修改 / 删除旧路径 / 新增目录。

已验证命令：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

已验证结果：

```text
已成功生成。
0 个警告
0 个错误
```

已验证 DLL 版本：

```powershell
[Reflection.AssemblyName]::GetAssemblyName('E:\git\DalamudACT\output\DalamudACT.dll').Version.ToString()
```

```text
0.15.2.34
```

## 2. 本轮主题

本轮交接核心是：**NPC 队友识别、友方 / 敌方 NPC 分类、相关 UI 与 debug 记录口径整理**。

目标是确认并继续验证：

1. 玩家自己仍能正常出数；
2. 普通玩家队友仍能正常出数；
3. 信赖 / 剧情 / 单人任务中的 NPC 队友能被识别为友方；
4. NPC 队友可以按配置显示在 DPS / HPS / 承伤相关页面；
5. 友方 NPC 不应被 `StatusFlags.Hostile` 或 `OwnerId` 误判为敌方或宠物；
6. Boss / 敌方 NPC 不应因为事件口径错位被误收编为友方；
7. debug 战斗记录和战斗流水默认关闭，需要时手动开启；
8. ActorControl Hook 仍保持禁用，不要为抓头顶标记直接恢复。

## 3. 关键改动与当前口径

### 3.1 战斗流水与 debug 记录默认关闭

当前配置默认值：

```csharp
public bool CombatTimelineRecordingEnabled = false;
public bool DebugCombatRecordingEnabled = false;
```

相关文件：

- `DalamudACT/Configuration/PluginConfiguration.cs`
- `DalamudACT/Features/CombatTimeline/CombatTimelineWindow.cs`
- `DalamudACT/Features/DebugCombatLog/DebugCombatLogWindow.cs`
- `DalamudACT/UI/Windows/SettingsWindow.cs`

影响：

- 普通统计链路不依赖 debug 战斗记录窗口；
- 用户反馈“debug 窗口没有记录”时，先确认设置里是否开启 `开始记录debug战斗记录`；
- 排查结束后建议关闭 debug 记录，避免日志和内存记录增长过快。

### 3.2 修正攻击技能被记录成“治疗 Boss”的问题

在 `DalamudACT/Plugin/ACT.cs` 的 `HandleAbility(...)` 中，当前口径是：

- `Damage / BlockedDamage / ParriedDamage`：`amount <= 0` 直接忽略；
- `Heal`：`amount <= 0` 直接忽略；
- `Heal` 只在目标是已追踪友方对象时写入 HPS / 治疗流水；
- hostile 目标上附带的 `Heal` 类型效果不会再被写成“玩家 使用攻击技能 治疗 Boss”。

重点位置：

```text
DalamudACT/Plugin/ACT.cs
  HandleAbility(...)
  LocalActionEffectType.Damage
  LocalActionEffectType.Heal
```

### 3.3 NPC / 友方对象识别入口集中到 Actors 模块

当前主要入口：

```text
DalamudACT/Features/Stats/LocalStatsService.Actors.cs
```

该文件负责：

- 本地玩家身份解析；
- `ObjectTable` / `PartyList` / `BuddyList` 合并；
- owner cache；
- NPC 队友识别；
- 友方 / 敌方 NPC 分类；
- `observedFriendlyActorCache` 动态收编；
- 当前队伍成员 UI 展示数据生成。

当前队伍快照构造顺序：

```text
本地玩家
-> PronounModule <1>~<8>
-> AgentHUD.PartyMembers
-> Dalamud PartyList
-> BuddyList
-> ObjectTable 中可识别的友方 NPC
```

相关函数：

```csharp
BuildLocalPartyHelperSnapshot()
AddPronounPartyMembersToLocalPartyHelper(...)
AddAgentHudPartyMembersToLocalPartyHelper(...)
AddNativePartyMemberToLocalPartyHelper(...)
AddUnresolvedNativePartyMemberToLocalPartyHelper(...)
TryResolveNativeBattleChara(...)
GetCurrentPartyMemberDisplayInfos()
```

## 4. PronounModule / AgentHUD 读取路径

### 4.1 PronounModule 不要使用 string 重载

曾遇到过运行时异常：

```text
System.MissingMethodException:
Method not found: PronounModule.ResolvePlaceholder(System.String, Byte, Byte)
```

因此不要写：

```csharp
pronounModule->ResolvePlaceholder($"<{index}>", 0, 0);
```

当前正确路径是构造 C 风格字符串，并走成员函数指针：

```csharp
byte* placeholder = stackalloc byte[4];
placeholder[0] = (byte)'<';
placeholder[1] = (byte)('0' + index);
placeholder[2] = (byte)'>';
placeholder[3] = 0;

return PronounModule.MemberFunctionPointers.ResolvePlaceholder(pronounModule, placeholder, 0, 0);
```

相关函数：

```csharp
ResolvePartyPlaceholder(...)
```

### 4.2 AgentHUD 作为第二读取源

当前会读取：

```csharp
AgentHUD.Instance()->PartyMembers
```

处理策略：

- 如果 `partyMember.Object` 可用，走 native object -> Dalamud `IBattleChara` 解析；
- 如果只有 `EntityId / Name`，会加入 `UnresolvedPartyMemberDisplayInfos`；
- unresolved 成员也会写入 `observedFriendlyActorCache`，用于后续事件现场按 `EntityId` 追踪。

相关函数：

```csharp
AddAgentHudPartyMembersToLocalPartyHelper(...)
AddUnresolvedNativePartyMemberToLocalPartyHelper(...)
```

## 5. NPC 队友识别规则

### 5.1 内置 NPC 名单

当前内置友方 NPC 名单在：

```csharp
BuiltInFriendlyNpcNameArray
```

位置：

```text
DalamudACT/Features/Stats/LocalStatsService.Actors.cs
```

当前包括：

```text
阿尔菲诺、阿莉塞、雅修特拉、桑克瑞德、于里昂热、古拉哈提亚、埃斯蒂尼安、乌克拉玛特、可露儿、克鲁鲁、敏菲利亚、琳、莉瑟、水晶公、零、瓦尔桑、卡尔瓦兰、爱梅特赛尔克、希斯拉德、维涅斯
```

另外，名字以：

```text
的幻体
```

结尾的对象会被规则自动识别为疑似友方 NPC / 幻体。

相关函数：

```csharp
LooksLikeDutyCompanionName(...)
IsKnownDutySupportCompanionName(...)
IsBuiltInFriendlyNpcName(...)
```

### 5.2 自定义 NPC 队友名单

当前配置字段：

```csharp
public List<string> CustomFriendlyNpcNames = new();
```

设置窗口入口：

```text
设置 -> NPC 队友识别名单
```

当前 UI 能力：

- 显示当前可识别的队伍成员；
- 显示成员名字 / 职业 / 类型 / ActorId / HP；
- 对当前队伍成员提供“填入”按钮；
- 支持手动输入 NPC 名字；
- 支持未输入时使用当前目标名字添加；
- 支持复制 / 删除 / 清空自定义名单；
- 支持查看 / 复制内置名单；
- 会自动去重和规范化名字。

相关函数：

```csharp
DrawFriendlyNpcNameListSection()
DrawCurrentPartyMemberList()
AddCustomFriendlyNpcNameFromInput()
DrawCustomFriendlyNpcNameTable()
DrawBuiltInFriendlyNpcNameTable()
NormalizeCustomFriendlyNpcNames()
NormalizeFriendlyNpcNameForCatalog(...)
```

### 5.3 OwnerId 不再一律等同“宠物归属”

部分信赖 / 剧情 / 单人任务 NPC 队友可能表现为：

```text
ObjectKind = BattleNpc
OwnerId = 本地玩家或非 0
```

如果只要看到 `OwnerId` 就归属到 owner，会导致 NPC 队友无法独立成行。

当前策略：

- 只有明确的宠物 / Buddy / 竞速陆行鸟等才走 owner 归属；
- 信赖 / 剧情 / 单人任务 NPC 队友应独立统计为 `FriendlyNpc`；
- `BattleNpcKind = NpcPartyMember` 优先视为友方 NPC；
- 具有职业 RowId、非 hostile、带 owner 的任务 NPC 也可能被视作友方 NPC。

相关函数：

```csharp
TryGetResolvableOwnerId(...)
ShouldResolveOwnerForObject(...)
LooksLikeDutySupportBattleNpc(...)
IsDutyNpcPartyMemberKind(...)
```

当前 `ShouldResolveOwnerForObject(...)` 只对以下 BattleNpcKind 做 owner 归属：

```text
Pet
Buddy
RaceChocobo
```

### 5.4 不要只用 Hostile 标志判断 NPC 队友

当前分类：

```text
Player
FriendlyNpc
HostileNpc
```

注意：

- `StatusFlags.Hostile` 不是 NPC 队友识别的唯一依据；
- 已知信赖 / 剧情 NPC 名字、`NpcPartyMember`、友方标记、AgentHUD / Pronoun 队伍来源都要优先考虑；
- 如果对象明显是 hostile 且不是已知友方 NPC，则不要收编成友方；
- 如果候选 NPC 与 hostile 目标同名，并且没有友方指标，应优先认为是 Boss / 敌方对象口径错位，避免误收编。

相关函数：

```csharp
ResolveLocalPartyActorKind(...)
ResolvePartyMemberTrackedActorKind(...)
ResolveTrackedActorKind(...)
IsFriendlyTrackedBattleNpc(...)
TryCreateObservedFriendlyActor(...)
HasFriendlyBattleNpcIndicators(...)
HasHostileBattleNpcWithSameName(...)
```

## 6. 统计输出与 UI 展示口径

### 6.1 统计 payload 中的 participantKind

统计快照中 `Combatant.ParticipantKind` 当前使用英文稳定值：

```text
player
friendlyNpc
hostileNpc
```

位置：

```text
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs
```

相关函数：

```csharp
FormatTrackedActorKind(...)
FormatCombatantJobName(...)
```

显示职业名时：

- 玩家显示实际职业；
- 友方 NPC 无职业时显示 `友方NPC`；
- 敌方 NPC 无职业时显示 `敌方NPC`。

### 6.2 悬浮统计显示模式

设置入口：

```text
设置 -> 悬浮对象显示
```

当前模式：

```text
智能：多人仅玩家，单人可含友方 NPC
玩家 + 友方 NPC
玩家 + 敌方 NPC
```

相关配置：

```csharp
FloatingStatsParticipantDisplayMode
HostileNpcMinHpMultiplier
HighlightNpcRows
```

当前行为：

- 智能模式下，多人场景通常隐藏 NPC；单人 / NPC 队友场景可显示友方 NPC；
- `玩家 + 友方 NPC` 会过滤敌方 NPC；
- `玩家 + 敌方 NPC` 会过滤友方 NPC；
- 敌方 NPC 需要达到本地玩家最大 HP 指定倍率才进入悬浮统计；
- NPC 行可高亮；友方 NPC 有职业时沿用职业主题色，否则使用友方 NPC 默认色；敌方 NPC 使用敌方色。

位置：

```text
DalamudACT/UI/Windows/SettingsWindow.cs
DalamudACT/UI/Panels/StatsPanel.cs
```

### 6.3 NPC badge / 文本

位置：

```text
DalamudACT/UI/Panels/StatsPanel.cs
```

当前 UI 口径：

- 友方 NPC badge：`友`
- 敌方 NPC badge：`敌`
- 无有效职业时显示 `友方NPC` / `敌方NPC`
- `HighlightNpcRows` 控制 NPC 行高亮

相关函数：

```csharp
ResolveFloatingCombatantKind(...)
TryParseFloatingCombatantKind(...)
ResolveIkegamiJobBadgeText(...)
ResolveBarColor(...)
TryResolveCombatantTextColor(...)
TryResolveCombatantRowBackgroundColor(...)
HasCombatantJob(...)
```

## 7. debug 战斗记录当前口径

相关文件：

```text
DalamudACT/Features/DebugCombatLog/DebugCombatLogWindow.cs
DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs
DalamudACT/UI/Windows/SettingsWindow.cs
DalamudACT/Plugin/ACT.cs
```

当前 UI 记录项：

```text
Boss / 小怪
  平A
  BUFF/debuff
  技能
  读条
  小怪按 Boss

友方
  标记
  技能
  BUFF
  debuff
```

说明：

- UI 上已合并“自己 / 队友”为“友方”；
- 内部配置仍保留 self / party 字段，友方勾选框会同时控制两套字段；
- `RecordDebugFriendlyAbility(...)` 会记录友方技能；
- 平A不会作为友方技能刷屏；
- ActorControl Hook 禁用后，友方特效标记 Hook 采集不可用，只保留低风险轮询 / 线索路径。

## 8. 高风险项：ActorControl Hook 仍禁止直接恢复

当前代码保持：

```csharp
private static bool ShouldInstallActorControlHook => false;
```

位置：

```text
DalamudACT/Plugin/ACT.cs
```

背景：

- 2026-05-23 已确认 ActorControl Hook 在 `HookFromAddress` / `HookManager.FollowJmp` / `MemoryHelper.ReadRaw` 路径触发 `AccessViolationException`，会导致游戏进程崩溃；
- 详细见：`md/2026-05-23-actorcontrol-crash-handoff.md`。

下一步如果确实要恢复：

1. 先做显式配置开关，默认关闭；
2. 做目标地址范围校验；
3. 做页保护校验；
4. 每个 Hook 独立 `try/catch` 和日志；
5. 任一 Hook 失败必须降级继续运行；
6. 不要一次性恢复所有高风险 Hook；
7. 不要直接把 `ShouldInstallActorControlHook` 改回 `true` 给用户测试。

## 9. 建议复测流程

### 9.1 启动 / UI 基础验证

1. 使用当前产物：`E:\git\DalamudACT\output\DalamudACT.dll`；
2. 进游戏加载插件；
3. 打开设置；
4. 进入：

```text
设置 -> NPC 队友识别名单
```

5. 查看 `当前队伍成员（N）`；
6. 确认玩家类型显示为 `玩家`；
7. 确认 NPC 队友类型显示为 `友方NPC`；
8. 若漏识别，选中 NPC 后尝试用“添加”加入自定义名单。

### 9.2 NPC 队友统计验证

建议场景：

- 单人任务；
- 信赖 / 幻体 / NPC 同行副本；
- 普通 4 人 / 8 人队伍；
- 有 Buddy / 宠物 / 召唤物的场景。

验证点：

1. 玩家自己能出行；
2. 普通玩家队友能出行；
3. NPC 队友在 `玩家 + 友方 NPC` 模式下能出行；
4. NPC 队友不被当成 Boss；
5. Boss 不被当成友方 NPC；
6. NPC 队友的 DPS / HPS / 承伤不异常归到玩家 owner；
7. 战斗结束判定不会被 NPC 队友或敌方对象拖住。

### 9.3 debug 战斗记录验证

1. 打开设置；
2. 开启 `开始记录debug战斗记录`；
3. 打开 debug 战斗记录窗口；
4. 确认记录项只显示 `Boss / 小怪` 与 `友方` 两组；
5. 打一小段战斗；
6. 确认 Boss 平A / 技能 / 读条 / BUFF/debuff 记录；
7. 确认友方技能 / BUFF / debuff / 标记线索记录；
8. 验证完关闭 debug 记录。

## 10. 如果仍然出问题，优先查哪里

### 10.1 设置页看不到 NPC 队友

优先查：

```csharp
BuildLocalPartyHelperSnapshot()
AddPronounPartyMembersToLocalPartyHelper(...)
AddAgentHudPartyMembersToLocalPartyHelper(...)
AddNativePartyMemberToLocalPartyHelper(...)
AddUnresolvedNativePartyMemberToLocalPartyHelper(...)
GetCurrentPartyMemberDisplayInfos()
```

重点看：

- `PronounModule <1>~<8>` 是否解析到对象；
- `AgentHUD.PartyMembers` 是否有 `EntityId / Name / Object`；
- `PartyList` / `BuddyList` 是否为空；
- `ObjectTable` 中 NPC 是否有 `BattleNpcKind = NpcPartyMember`；
- NPC 名字是否在内置 / 自定义名单中。

### 10.2 设置页能看到 NPC，但战斗统计不出 NPC 行

优先查：

```csharp
TryGetTrackedActor(...)
TryResolveTrackedSource(...)
TryResolveTrackedSourceFromGameObject(...)
ObserveFriendlyCombatantFromGameObject(...)
ObserveFriendlyCombatantIdentity(...)
EncounterSession.EnsureCombatant(...)
```

重点看：

- combat event 的 `sourceId` 是否等于 UI 中看到的 `EntityId`；
- 是否需要同时核对 `GameObjectId low32 / ObjectId / EntityId`；
- `observedFriendlyActorCache` 是否写入；
- `ParticipantKind` 是否最终写成 `friendlyNpc`。

### 10.3 NPC 仍被当成 Boss / 敌方

优先查：

```csharp
ResolveTrackedActorKind(...)
IsFriendlyTrackedBattleNpc(...)
TryCreateObservedFriendlyActor(...)
HasFriendlyBattleNpcIndicators(...)
LooksLikeDutySupportBattleNpc(...)
HasHostileBattleNpcWithSameName(...)
```

重点看：

- 是否只有 `StatusFlags.Hostile` 一条线索；
- 是否有同名 hostile NPC 导致保护逻辑拒绝收编；
- 是否应加入自定义 NPC 名单；
- 是否 `BattleNpcKind` / `ClassJob.RowId` / `OwnerId` 与预期不一致。

### 10.4 战斗不结束

优先查：

```text
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs
```

重点看：

- `AreAllPartyMembersOutOfCombat(...)` 相关分支；
- 是否有 NPC / Buddy / observed friendly actor 一直被认为在战斗；
- 是否有 hostile NPC 被错误纳入需要脱战判断的集合。

### 10.5 攻击技能又显示治疗 Boss

优先查：

```text
DalamudACT/Plugin/ACT.cs
```

重点看：

- `HandleAbility(...)` 中 `LocalActionEffectType.Heal` 分支；
- `targetIsTrackedActor` 是否被错误判为 true；
- `resolvedTargetActorId` 是否被错误解析为友方。

## 11. 当前关键文件清单

### 必看代码

```text
DalamudACT/Plugin/ACT.cs
DalamudACT/Features/Stats/LocalStatsService.Actors.cs
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs
DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs
DalamudACT/Configuration/PluginConfiguration.cs
DalamudACT/UI/Windows/SettingsWindow.cs
DalamudACT/UI/Panels/StatsPanel.cs
```

### 相关文档

```text
HANDOVER.md
md/SESSION-HANDOFF.md
md/2026-05-23-actorcontrol-crash-handoff.md
md/2026-05-23-debug-combat-log-friendly-handoff.md
md/2026-05-23-actors-module-refactor-handoff.md
md/2026-05-24-npc-party-handoff.md
```

## 12. 发布 / 版本注意

当前已验证 DLL 版本是：

```text
0.15.2.34
```

当前构建由 `DalamudACT/DalamudACT.csproj` 复制：

```text
Data/DalamudACT.json -> output/DalamudACT.json
```

因此发布前至少核对：

```text
DalamudACT/DalamudACT.csproj
Data/DalamudACT.json
repo.json
md/CHANGELOG.md
md/RELEASE-NOTES.md
```

额外提醒：当前仓库中 `DalamudACT/DalamudACT.json` 不是 csproj 中复制到输出的那个 manifest；如果发布流程仍引用它，需要单独同步版本号，避免元数据不一致。

## 13. 禁止事项

在确认用户现场和当前文档前，不要执行：

```powershell
git reset --hard
git checkout -- .
```

也不要：

- 删除 `1.txt`；
- 批量删除未跟踪目录；
- 直接恢复 ActorControl Hook；
- 使用 `PronounModule.ResolvePlaceholder(string, byte, byte)`；
- 把所有 `OwnerId != 0` 的 BattleNpc 都归属到玩家；
- 单靠 `StatusFlags.Hostile` 判断 NPC 队友；
- 在没有构建验证的情况下交付 `output\DalamudACT.dll`。

## 14. 下一步建议

1. 先让用户用当前 `output\DalamudACT.dll` 进游戏复测；
2. 优先看 `设置 -> NPC 队友识别名单 -> 当前队伍成员` 是否列出 NPC；
3. 如果 UI 能看到 NPC，再进战斗验证 NPC 是否出行；
4. 如果 UI 看不到 NPC，先补 `PronounModule / AgentHUD` 相关 debug；
5. 如果 UI 能看到但战斗不出行，查事件 `sourceId` 与 `EntityId / ObjectId / GameObjectId low32` 是否错位；
6. 如果战斗不结束，查 Encounter 脱战判断；
7. 一切稳定后，再考虑同步 `CHANGELOG.md` / `RELEASE-NOTES.md` / 发布 metadata。
