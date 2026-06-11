# DalamudACT 维护交接

## 2026-06-11 收工快照

最新交接详见：[`md/SESSION-HANDOFF.md`](md/SESSION-HANDOFF.md) 顶部的 `2026-06-11 0.15.2.68 紧急性能发布收工记录`。

- 最新正式发布：`0.15.2.68`
- Release：`https://github.com/anmili2022/DalamudACT/releases/tag/0.15.2.68`
- Release commit：`2c0a5358bac940f59ef95522c2b21c0299da0561`
- Release asset：`DalamudACT.zip`
- SHA256：`54960733c2317f9170ce8e57f7711ca2a87a9ac58de83053c985eac9d4b998d0`
- 订阅源 `MyDalamudRepo/pluginmaster.json` 已同步到 `DalamudACT 0.15.2.68`，同步 commit：`75ba9138455750f230921c3d193b0fd95ca737a2`
- 当前主仓库只剩未跟踪现场文件，不要误删或误提交：`1.txt`、`tools/CactbotTimelineExtractor/test_output*.txt`、`tools/时间轴预览工具.rar`、`打工计时器.html`
- 如果用户继续反馈绝妖星卡顿，先让其确认已更新 `0.15.2.68`，再按交接文档里的“下次优先事项”收集强化日志。

## 2026-05-24 NPC 队友识别与 UI 交接（整理版）

详见：[`md/2026-05-24-npc-party-handoff.md`](md/2026-05-24-npc-party-handoff.md)

> 整理说明：原顶部交接内容发生 `?` 乱码，已按当前源码和本地构建结果重建为可读摘要。旧历史交接内容保留在下方。

### 当前基线

- 工作目录：`E:\git\DalamudACT`
- 当前分支：`main`
- 当前 HEAD：`b7602c1`
- 当前可信产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 当前 DLL 版本：`0.15.2.34`
- 已验证构建：`dotnet build E:\git\DalamudACT\DalamudACT.sln`
- 构建结果：`0 warnings / 0 errors`
- 当前工作区仍是脏工作区，包含结构拆分后的大量修改 / 删除旧路径 / 新增目录；不要 reset、checkout 或清理未跟踪文件。

### 本轮重点

- NPC 队友识别与独立成行；
- 友方 NPC / 敌方 NPC / 玩家分类；
- `PronounModule <1>~<8>`、`AgentHUD.PartyMembers`、Dalamud `PartyList`、`BuddyList`、`ObjectTable` 多来源合并；
- 避免 `PronounModule.ResolvePlaceholder(string, byte, byte)` 触发 `MissingMethodException`，当前走 `MemberFunctionPointers.ResolvePlaceholder(...)`；
- `OwnerId` 只对明确宠物 / Buddy / RaceChocobo 做归属，信赖 / 剧情 NPC 队友应独立统计；
- 不要只靠 `StatusFlags.Hostile` 判断 NPC 队友；
- 设置页新增 / 整理 `NPC 队友识别名单`，用于核对当前队伍成员和添加自定义 NPC 名单；
- debug 战斗记录与战斗流水默认关闭，需要排查时手动开启；
- ActorControl Hook 仍因启动崩溃风险保持禁用。

### 当前关键文件

```text
DalamudACT/Plugin/ACT.cs
DalamudACT/Features/Stats/LocalStatsService.Actors.cs
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs
DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs
DalamudACT/Configuration/PluginConfiguration.cs
DalamudACT/UI/Windows/SettingsWindow.cs
DalamudACT/UI/Panels/StatsPanel.cs
```

### 接手优先验证

1. 使用 `output\DalamudACT.dll` 进游戏；
2. 打开 `设置 -> NPC 队友识别名单`；
3. 确认当前队伍成员列表能看到玩家与 NPC；
4. 确认玩家类型是 `玩家`，NPC 类型是 `友方NPC`；
5. 在信赖 / 单人任务 / NPC 同行场景验证 NPC 是否能在 DPS / HPS / 承伤中出行；
6. 确认 Boss 不被误收编为友方 NPC；
7. 确认战斗能正常结束。

### 禁止事项

- 不要执行 `git reset --hard` 或 `git checkout -- .`；
- 不要误删 `1.txt`；
- 不要批量删除未跟踪目录；
- 不要直接把 `ShouldInstallActorControlHook` 改回 `true`；
- 不要使用 `PronounModule.ResolvePlaceholder(string, byte, byte)`；
- 不要把所有 `OwnerId != 0` 的 `BattleNpc` 都归属给玩家；
- 不要单靠 `StatusFlags.Hostile` 判断 NPC 队友。

---
## 2026-05-23 debug 战斗记录友方合并与控制区折叠交接

### 本轮背景

- 用户最新要求：
  - debug 战斗记录窗口截图红框区域可以折叠；
  - 记录项不再分“队友”和“自己”，统一显示为“友方”；
  - Boss 的 `BUFF` 显示改成 `BUFF/debuff`；
  - 友方增加“技能”记录项。
- 之前用户仍反馈截图里的红色头顶标记没有记录；本轮没有重新打开高风险 ActorControl Hook，只做 UI / 记录项收口。
- 当前项目路径：`E:\git\DalamudACT`。

### 本轮完成内容

#### 1. debug 战斗记录顶部控制区可折叠

- 文件：`DalamudACT/Features/DebugCombatLog/DebugCombatLogWindow.cs`
- 新增 / 使用：`DrawCollapsibleControlPanel(...)`
- 将以下区域统一放入一个 `ImGui.CollapsingHeader`：
  - 记录项开关；
  - 复制 / 导出 / 清空 / 打开目录；
  - 保留条数；
  - 表格列显示；
  - 筛选区域。
- 折叠标题：

```text
记录项 / 操作 / 筛选 / 列显示
```

- 当前保持默认展开：

```csharp
ImGuiTreeNodeFlags.DefaultOpen
```

如果用户后续要求默认收起，去掉该 flag 即可。

#### 2. 记录项 UI 从“队友 / 自己”合并为“友方”

- 文件：
  - `DalamudACT/Features/DebugCombatLog/DebugCombatLogWindow.cs`
  - `DalamudACT/UI/Windows/SettingsWindow.cs`
- 当前记录项 UI 结构：

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

- UI 上不再显示“队友”和“自己”两组。
- 内部配置仍保留 party / self 两套字段，友方勾选框通过 helper 同时控制两套值：

```csharp
DrawFriendlyConfigCheckbox(...)
DrawDebugCombatRecordFriendlyCheckbox(...)
```

保留内部字段的原因：兼容旧配置和已有代码路径，避免一次性改动过大。

#### 3. 表格类型与筛选类型统一为“友方”

- 文件：`DalamudACT/Features/DebugCombatLog/DebugCombatLogWindow.cs`
- `DebugKindFilter` 已使用合并后的友方类型：

```csharp
FriendlyAction
FriendlyBuff
FriendlyMarker
FriendlyDebuff
```

- `MatchesKindFilter(...)` 将以下内部 kind 合并显示 / 筛选：

```text
PartyAction + SelfAction   -> 友方技能
PartyBuff   + SelfBuff     -> 友方BUFF
PartyMarker + SelfMarker   -> 友方标记
PartyDebuff + SelfDebuff   -> 友方debuff
```

- `GetKindFilterLabel(...)` / `GetKindLabel(...)` 已修正为明确中文，不再出现上一轮遗留的 `??` 文案。

#### 4. 记录内容里的“自己 / 队友”统一改为“友方”

- 文件：`DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs`
- 已修改实际写入 debug 战斗记录的消息文本：

```text
友方 XXX 发动技能 ...
友方 XXX 身上出现特效标记 ...
友方 XXX 身上出现 BUFF ...
友方 XXX 身上出现 debuff ...
友方 XXX 身上出现特效标记线索 ...
```

- 不再输出：

```text
自己 XXX ...
队友 XXX ...
```

#### 5. Boss `BUFF` 改成 `BUFF/debuff`

- 文件：
  - `DalamudACT/Features/DebugCombatLog/DebugCombatLogWindow.cs`
  - `DalamudACT/UI/Windows/SettingsWindow.cs`
  - `DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs`
- UI 显示：

```text
Boss BUFF/debuff
```

- `CaptureDebugBossBuffsLocked(...)` 目前会记录 Boss 身上的：
  - `IsBuffStatus(status)`；
  - `IsDebuffStatus(status)`。
- 记录文本会区分：

```text
BUFF
debuff
```

#### 6. 友方新增“技能”记录项

- 文件：
  - `DalamudACT/Configuration/PluginConfiguration.cs`
  - `DalamudACT/Plugin/ACT.cs`
  - `DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs`
  - `DalamudACT/Features/DebugCombatLog/DebugCombatLogWindow.cs`
  - `DalamudACT/UI/Windows/SettingsWindow.cs`
- 新增配置字段：

```csharp
public bool DebugRecordPartyAction = true;
public bool DebugRecordSelfAction = true;
```

- 配置版本已升到：

```csharp
Version = 56
```

- `PluginConfiguration.Migrate()` 中已为 `< 56` 的旧配置补默认值。
- `Reset()`、全开 / 全关 / 默认按钮都已同步处理这两个字段。
- `ACT.ProcessActionEffects(...)` 末尾调用：

```csharp
statsService.RecordDebugFriendlyAbility(...);
```

- `RecordDebugFriendlyAbility(...)` 行为：
  - 只在 `DebugCombatRecordingEnabled == true` 时记录；
  - 不记录 `actionId == 0`；
  - 不记录平A，避免刷屏；
  - 来源必须能解析为已跟踪的非 hostile actor；
  - self 受 `DebugRecordSelfAction` 控制；
  - party / 其他友方受 `DebugRecordPartyAction` 控制；
  - UI 显示统一为“友方技能”。

#### 7. ActorControl Hook 仍保持禁用

- 文件：`DalamudACT/Plugin/ACT.cs`
- 当前仍保持：

```csharp
private static bool ShouldInstallActorControlHook => false;
```

- 禁用原因见：`md/2026-05-23-actorcontrol-crash-handoff.md`
- 不要为了红色头顶标记直接改回 `true`；之前 appcrash 已确认会在 `HookFromAddress` / `HookManager.FollowJmp` / `MemoryHelper.ReadRaw` 路径触发原生崩溃。

### 本轮构建结果

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

### 下次接手优先验证

#### 1. 先做游戏内 UI 验证

建议顺序：

1. 重载插件或加载最新 `output\DalamudACT.dll`；
2. 打开主窗口；
3. 点击 `打开debug战斗记录`；
4. 确认顶部出现可折叠控制区：`记录项 / 操作 / 筛选 / 列显示`；
5. 展开记录项，确认只显示：
   - `Boss / 小怪`；
   - `友方`；
6. 确认不再出现“队友 / 自己”两组；
7. 勾选 `开始记录`；
8. 打一小段战斗，确认：
   - Boss 平A；
   - Boss 技能；
   - Boss 读条；
   - Boss BUFF/debuff；
   - 友方技能；
   - 友方 BUFF；
   - 友方 debuff；
   - 友方标记线索。

#### 2. 红色头顶标记仍需专项排查

用户仍关心截图里的红色头顶标记没有记录。

当前已做无 Hook 轮询兜底：

```text
GameObject.NamePlateIconId
Character.Icon
Character.StatusLoopVfxId
```

但用户反馈仍没有记录，说明该标记大概率不在这些字段里，或字段只在很短窗口出现而轮询没抓到。

下一步建议不要恢复 ActorControl Hook，而是走低风险方案：

1. 增加一个临时 debug dump 开关，只在 `DebugCombatRecordingEnabled` 开启时对友方 actor 输出：
   - actorId；
   - name；
   - NamePlateIconId；
   - Character.Icon；
   - StatusLoopVfxId；
   - 当前 status id 列表；
2. 只在数值变化时记录，避免刷屏；
3. 对比截图红色标记出现前后的 dump；
4. 如果仍抓不到，再查 Dalamud 是否有更安全的 TargetIcon / ActorControl 事件入口；
5. 不要直接 `HookFromAddress`。

#### 3. 文档 / 版本发布暂不做

本轮只写交接文档，没有同步正式：

- `CHANGELOG.md`
- `RELEASE-NOTES.md`
- README 使用说明
- 仓库发布 metadata

建议等用户游戏内确认 UI 和记录口径可用后再同步正式发布文档。

### 当前工作区注意事项

- 当前工作区仍是脏的，包含大量结构拆分遗留改动和新增目录。
- 未跟踪文件 `1.txt` 不要误删。
- 不要执行：

```powershell
git reset --hard
git checkout -- .
```

- 不要做任何会覆盖用户现场的批量清理。

## 2026-05-23 交接补充：ActorControl Hook 启动崩溃处理

- 详细交接见：`md/2026-05-23-actorcontrol-crash-handoff.md`
- 用户反馈刚才开游戏崩溃，本轮已暂停继续拆结构，改为排查 XIVLauncherCN / Dalamud 日志。
- 已核对 3 份崩溃日志：
  - `C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\dalamud_appcrash_20260523_021020_798_17592.log`
  - `C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\dalamud_appcrash_20260523_021058_662_18072.log`
  - `C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\dalamud_appcrash_20260523_021233_913_13152.log`
- 三份日志都显示主异常为 `System.AccessViolationException`，调用链落在 `DalamudACT.ACT.CreateActorControlHook()` -> `HookFromAddress` -> `HookManager.FollowJmp`，说明直接风险来自 ActorControl Hook 安装阶段，而不是本轮 partial 拆分。
- 已修改 `DalamudACT/Plugin/ACT.cs`：新增 `ShouldInstallActorControlHook => false`，默认不再调用 `CreateActorControlHook()`，保留原 Hook 文件和注释，避免插件加载时再次触发原生内存读崩溃。
- 追加修改 `DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs`：在 ActorControl Hook 禁用后，增加无 Hook 的 `GameObject.NamePlateIconId` 轮询兜底；如果某类头顶机制图标会同步到该字段，会记录为 `NamePlateIconId 轮询` 的队友 / 自己标记。
- 当前影响：依赖 ActorControl raw 事件的特效标记采集仍不可用；ActionEffect 主统计、BOSS 读条轮询、BUFF/debuff、DoT / Wildfire 诊断不受影响。
- 本轮本地构建已通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`
  - 产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 下一步建议：先让用户进游戏验证可启动；若要恢复特效标记采集，先做默认关闭的配置开关、目标地址范围校验和页保护校验，不要直接重开 ActorControl Hook。
- 当前工作区仍然是脏的，`1.txt` 不要误删；不要执行 `git reset --hard`、`git checkout -- .` 等会覆盖用户现场的操作。

## 2026-05-23 交接补充：Status 模块拆分

- 详细交接见：`md/2026-05-23-status-module-refactor-handoff.md`
- 本轮按上一轮建议继续下一步：新增 `DalamudACT/Features/Stats/LocalStatsService.Status.cs`。
- 已把 StatusList 反射读取、状态 ID、来源、参数、剩余时间和状态表文本等状态 helper 从 `LocalStatsService.cs` 拆到独立 partial 文件。
- 本轮只做结构拆分，不改状态读取口径、不改状态来源 Actor 解析、不改 DoT / Wildfire 状态归因、不改 debug 战斗记录中的 BUFF / debuff 采集。
- 已迁移内容包括：
  - `ResolveStatusSourceActorId(...)`；
  - `TryGetStatusGameDataText(...)`、`TryExtractGameDataText(...)`、`TryGetStatusGameDataInt(...)`；
  - `GetStatusId(...)`、`TryGetStatusParam(...)`、`GetStatusRemainingTime(...)`；
  - `GetReflectedStatusValue(...)`、`EnumerateStatusEntries(...)`。
- 本轮本地构建已通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`
  - 产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 下次结构化建议：主文件已经只剩构造、共享常量和共享锁；下一步建议拆当前最大的 `LocalStatsService.Dots.cs`，优先从 `LocalStatsService.Dots.Wildfire.cs` 开始。
- 当前工作区仍然是脏的，`1.txt` 不要误删；不要执行 `git reset --hard`、`git checkout -- .` 等会覆盖用户现场的操作。

## 2026-05-23 交接补充：Actors 模块拆分

- 详细交接见：`md/2026-05-23-actors-module-refactor-handoff.md`
- 本轮按上一轮建议继续下一步：新增 `DalamudACT/Features/Stats/LocalStatsService.Actors.cs`。
- 已把 Actor / ObjectTable / PartyList / BuddyList / owner cache / 本地统计对象身份归一相关逻辑从 `LocalStatsService.cs` 拆到独立 partial 文件。
- 本轮只做结构拆分，不改统计口径、不改 Actor 归属规则、不改宠物 / 召唤物 / NPC 队友归属判断、不改 DoT / Wildfire、不改 debug 战斗记录、不改 Encounter、不改历史记录、不改测试数据。
- 已迁移内容包括：
  - `OwnerCacheTtl`、`OwnerCacheWarmupInterval`、`ownerCache`、`observedFriendlyActorCache`、`partyMemberHpCache`、`lastOwnerWarmupUtc`；
  - `WarmOwnerCacheFromObjectTable()`、`IsTrackedActor(...)`、`CanResolveTrackedSource(...)`；
  - `TryResolveTrackedSourceFromGameObject(...)`、`ObserveFriendlyCombatantFromGameObject(...)`、`ObserveFriendlyCombatantIdentity(...)`；
  - ObjectTable / PartyList / BuddyList / 本地玩家 / owner 归属解析 helper；
  - `ActorIdentity`、`OwnerCacheEntry`、`TrackedActorKind`、`TrackedActor`。
- 本轮本地构建已通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`
  - 产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 原建议的 `LocalStatsService.Status.cs` 已在后续完成；下一步建议拆 `LocalStatsService.Dots.Wildfire.cs`。
- 当前工作区仍然是脏的，`1.txt` 不要误删；不要执行 `git reset --hard`、`git checkout -- .` 等会覆盖用户现场的操作。

## 2026-05-23 交接补充：Formatting 模块拆分

- 详细交接见：`md/2026-05-23-formatting-module-refactor-handoff.md`
- 本轮按上一轮建议继续下一步：新增 `DalamudACT/Features/Stats/LocalStatsService.Formatting.cs`。
- 已把纯文本 / 数字格式化 helper 从 `LocalStatsService.cs` 拆到独立 partial 文件。
- 本轮只做结构拆分，不改统计口径、不改战斗流水文本口径、不改 ACTX 快照字段、不改 DoT / Wildfire、不改 debug 战斗记录、不改历史记录、不改测试数据。
- 已迁移内容包括：
  - `FormatActionNameWithId(...)`；
  - `NormalizeZoneName(...)`、`NormalizeActionName(...)`；
  - `FormatCriticalSuffix(...)`、`FormatSimulatedCriticalSuffix(...)`；
  - `BuildUnknownActorName(...)`；
  - `ResolveJobName(...)`；
  - `CreateDamageString(...)`、`FormatChineseDamageUnit(...)`。
- 本轮本地构建已通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`
  - 产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 原建议的 `LocalStatsService.Actors.cs` 和后续 `LocalStatsService.Status.cs` 均已完成；下一步建议拆 `LocalStatsService.Dots.Wildfire.cs`。
- 当前工作区仍然是脏的，`1.txt` 不要误删；不要执行 `git reset --hard`、`git checkout -- .` 等会覆盖用户现场的操作。

## 2026-05-23 交接补充：Encounter 模块拆分

- 详细交接见：`md/2026-05-23-encounter-module-refactor-handoff.md`
- 本轮按上一轮建议继续下一步：新增 `DalamudACT/Features/Stats/LocalStatsService.Encounter.cs`。
- 已把当前战斗状态、实时战斗事件入口、战斗流水、战斗结束判断、结算、状态文本和 ACTX 快照构造从 `LocalStatsService.cs` 拆到独立 partial 文件。
- 本轮只做结构拆分，不改战斗统计口径、不改历史记录写入口径、不改 debug 战斗记录、不改 DoT / Wildfire、不改测试数据样本。
- 已迁移内容包括：
  - `CurrentCombatData`、`DisplayCombatData`、`CombatTimelineEntries`、`EncounterFinalizedVersion`、`DataSourceText`、`StatusText`；
  - `RecordEncounterActivity(...)`、`RecordDamage(...)`、`RecordHeal(...)`、`RecordFailure(...)`、`RecordDeath(...)`；
  - `ClearCombatTimeline()`、`ApplyCombatTimelineRetentionLimit()`、`Update(...)`；
  - 战斗无数据诊断、队友死亡轮询、脱战计时、战斗结算、战斗流水追加和裁剪；
  - `CombatTimelineEntry`、`CombatTimelineEntryKind`、`EncounterSession`、`CombatantSession`、`ActxSnapshotFormatter`。
- 上一轮残留在主文件中的 `UpdateStatusText(...)` 已补迁移到 Encounter partial。
- 本轮本地构建已通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`
  - 产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 原建议的低风险 `LocalStatsService.Formatting.cs`、后续 `LocalStatsService.Actors.cs` 和 `LocalStatsService.Status.cs` 均已完成；下一步可继续拆 `LocalStatsService.Dots.Wildfire.cs`。
- 当前工作区仍然是脏的，`1.txt` 不要误删；不要执行 `git reset --hard`、`git checkout -- .` 等会覆盖用户现场的操作。

## 2026-05-23 交接补充：TestData 模块拆分

- 详细交接见：`md/2026-05-23-testdata-module-refactor-handoff.md`
- 本轮按上一轮建议继续下一步：新增 `DalamudACT/Features/Stats/LocalStatsService.TestData.cs`。
- 已把内置测试数据导入入口和演示战斗记录构造函数从 `LocalStatsService.cs` 拆到独立 partial 文件。
- 本轮只做结构拆分，不改测试样本数值、不改导入测试数据后的状态清理行为、不改历史记录生成口径。
- 已迁移内容包括：
  - `LoadTestData()`；
  - `BuildRaidTestCombatData()`、`BuildRaidEightPlayerTestCombatData()`、`BuildTrialTestCombatData()`、`BuildTrainingTestCombatData()`；
  - `BuildTestCombatData(...)`、`PopulateDerivedTestCombatantMetrics(...)`、`ParseDurationTextToSeconds(...)`、`CreateTestCombatant(...)`。
- 本轮本地构建已通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`
  - 产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 下次结构化建议第一步：拆 `LocalStatsService.Encounter.cs`，注意这步风险更高，会牵涉当前战斗快照、状态文本和历史写入。
- 当前工作区仍然是脏的，`1.txt` 不要误删；不要执行 `git reset --hard`、`git checkout -- .` 等会覆盖用户现场的操作。

## 2026-05-23 交接补充：History 模块拆分

- 详细交接见：`md/2026-05-23-history-module-refactor-handoff.md`
- 本轮按上一轮建议继续下一步：新增 `DalamudACT/Features/Stats/LocalStatsService.History.cs`。
- 已把历史记录列表、历史预览、导入 / 导出、历史 JSON 序列化相关逻辑从 `LocalStatsService.cs` 拆到独立 partial 文件。
- 本轮只做结构拆分，不改历史记录数据结构、不改导入导出 JSON 格式、不改战斗结算口径。
- 已迁移内容包括：
  - `historicalRecords`、`selectedHistoricalRecordIndex`、`historicalPreviewExpiresAtUtc`；
  - `HistoricalRecords`、`SelectedHistoricalRecordIndex`、`HistoryTransferStatusText`、`HistoryTransferFilePath`；
  - `ClearHistory()`、`PreviewHistoricalRecord(...)`、`ExportHistoricalRecords()`、`ImportHistoricalRecords()`；
  - 历史预览倒计时、显示数据切换、历史记录 upsert / sort / identity 判断；
  - `HistoricalRecordsExportPayload`。
- 原本暂留的 `LoadTestData()` 已在后续拆到 `LocalStatsService.TestData.cs`，详见：`md/2026-05-23-testdata-module-refactor-handoff.md`。
- 本轮本地构建已通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`
  - 产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 原建议的下一步 `LocalStatsService.TestData.cs` 已在后续完成，详见：`md/2026-05-23-testdata-module-refactor-handoff.md`。
- 当前工作区仍然是脏的，`1.txt` 不要误删；不要执行 `git reset --hard`、`git checkout -- .` 等会覆盖用户现场的操作。

## 2026-05-23 交接补充：DoT / Wildfire 模块拆分

- 详细交接见：`md/2026-05-23-dot-module-refactor-handoff.md`
- 本轮按上一轮建议继续下一步：新增 `DalamudACT/Features/Stats/LocalStatsService.Dots.cs`。
- 已把玩家 DoT / Wildfire / DOT 诊断相关逻辑从 `LocalStatsService.cs` 拆到独立 partial 文件。
- 本轮只做结构拆分，不改 DoT 统计算法、不改 ACT 对账口径、不改 `PlayerDotCatalog.cs`、不改 `tools/DotReconcile`。
- 已迁移内容包括：
  - DoT / Wildfire 常量、缓存和活跃状态；
  - `ObservePotentialPlayerDotApplication(...)`、`ObservePotentialPlayerHostileActionSample(...)`、`TryRecordPlayerDotDamage(...)` 等入口；
  - DoT 状态采集、tick 归因、模拟补算；
  - Wildfire 层数记录、贡献样本和模拟结算；
  - DOT 聚焦诊断日志；
  - DoT / Wildfire 专用嵌套类型。
- 本轮本地构建已通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`
  - 产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 原建议的下一步 `LocalStatsService.History.cs` 已在后续完成，详见：`md/2026-05-23-history-module-refactor-handoff.md`。
- 当前工作区仍然是脏的，`1.txt` 不要误删；不要执行 `git reset --hard`、`git checkout -- .` 等会覆盖用户现场的操作。

## 2026-05-23 交接补充：项目结构化第一步 / DebugRecorder 拆分

- 详细交接见：`md/2026-05-23-structure-refactor-handoff.md`
- 本轮按“下一步建议”先做第一步：将 `LocalStatsService` 改为 `partial`，并把 debug 战斗记录逻辑拆到：
  - `DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs`
- 已迁移内容包括：
  - debug 战斗记录开关、日志列表、保留条数；
  - BOSS 技能 / 平A / 读条 / BUFF 采集；
  - 自己 / 队友 BUFF、debuff、特效标记记录；
  - debug 专用 `DebugCombatLogEntry`、`DebugCombatLogEntryKind`、`DebugObservedStatusKey`。
- 本轮只做结构拆分，不改统计口径，不改 ActorControl Hook 逻辑，不删除原有中文注释。
- 本轮本地构建已通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`
  - 产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 原建议的下一步 `LocalStatsService.Dots.cs` 已在后续完成，详见：`md/2026-05-23-dot-module-refactor-handoff.md`。
- 当前工作区仍然是脏的，`1.txt` 不要误删；不要执行 `git reset --hard`、`git checkout -- .` 等会覆盖用户现场的操作。

## 2026-05-21 交接补充：debug战斗记录第一版收工
## 2026-05-21 补充：debug战斗记录第二版收工

- 详细交接见：`md/2026-05-21-debug-combat-log-handoff.md`
- 本轮已经把 debug 战斗记录从“第一版骨架”补到可继续实测的第二版：
  - 自己 BUFF / 队友 BUFF
  - 小怪按 BOSS 处理
  - 特效标记 ActorControl 扫描增强
  - 悬浮窗标题栏可折叠
- 本轮本地构建已通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`
  - `E:\git\DalamudACT\output\DalamudACT.dll`
- 下次接手第一步：先实战确认 `自己BUFF / 队友BUFF / 队友头顶标记 / 小怪按BOSS处理` 是否稳定。
- 当前工作区仍然是脏的，`1.txt` 不要误删。

- 本轮按用户需求新增独立 `debug战斗记录` 功能，详细交接已单独整理：`md/2026-05-21-debug-combat-log-handoff.md`。
- 已完成第一版主体：
  - 新增独立悬浮窗：`DalamudACT/UI/DebugCombatLogWindow.cs`；
  - 新增总开关 `开始记录`，以及 8 个独立记录项开关；
  - 支持复制当前显示、清空、自动滚动、保留条数设置、类型/角色/目标/技能状态标记筛选、搜索框；
  - 接入 BOSS 平A、BOSS 发动技能、BOSS 读条、BOSS BUFF、自己 debuff、队友 debuff；
  - 特效标记已通过 `ActorControl` 预接入，但仍需实战 raw 值校准。
- 本轮主要修改/新增文件：
  - `DalamudACT/Configuration/PluginConfiguration.cs`
  - `DalamudACT/Plugin/ACT.cs`
  - `DalamudACT/Stats/LocalStatsService.cs`
  - `DalamudACT/UI/PluginUI.cs`
  - `DalamudACT/UI/MainWindow.cs`
  - `DalamudACT/UI/SettingsWindow.cs`
  - `DalamudACT/UI/DebugCombatLogWindow.cs`
  - `md/2026-05-21-debug-combat-log-handoff.md`
- 本轮本地构建已通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`
  - 产物：`E:\git\DalamudACT\output\DalamudACT.dll`
- 下次开工第一步建议做实战验证：
  1. 使用最新 `output\DalamudACT.dll`；
  2. 打开主窗口，点击 `打开debug战斗记录`；
  3. 勾选 `开始记录`，默认保持 8 项都开启；
  4. 打一场短战斗；
  5. 点击 `复制当前显示`，把记录贴回来核对。
- 下次优先校准点：
  - `ACT.TryExtractDebugMarkerId(...)`：确认队友/自己身上特效标记是否能命中；若无记录，需要调整 ActorControl 分类或改用其他事件路径；若噪声太多，按 raw `category / param1 / param2` 收窄规则；
  - `ACT` 中 ActorControl Hook 运行时安全性：进游戏后看是否出现 `已安装 ActorControl Hook`、`安装 ActorControl Hook 失败`、`处理 ActorControl 事件失败`；
  - `LocalStatsService.CaptureDebugBossBuffsLocked(...)`：根据实战结果校准 BOSS BUFF 噪声，优先避免把玩家给 Boss 的 DoT/debuff 误记成 Boss BUFF。
- 本轮尚未同步正式 `CHANGELOG`、`RELEASE-NOTES`、README 使用说明、版本号或仓库 metadata；建议等用户实战确认可用后再做。
- 当前工作区仍然是脏的，`1.txt` 仍然不要误删；不要执行 `git reset --hard`、`git checkout -- .` 等会覆盖用户现场的操作。

## 2026-05-17 交接补充：新 history 复核与 DoT 对账收工

- 本轮继续沿 `md/2026-05-16-dot-handoff.md` 的 DoT / ACT 对账口径收尾，没有改代码，只复核用户新导出的历史记录。
- 用户新导出的 history：
  - `C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT\history-records.json`
  - 文件时间约 `2026-05-17 15:51`
- 已用 `DotReconcile` 跑最新一场：
  - 战斗：`恩欧歼灭战`
  - 时间：`2026-05-17 15:43:27 +08:00 ~ 2026-05-17 15:51:06 +08:00`
  - 时长：`07:38`
  - 输出文件：
    - `output\reconcile-new-summary.csv`
    - `output\reconcile-new.csv`
    - `output\reconcile-new-status.csv`
    - `output\reconcile-new-windowcheck.csv`
    - `output\reconcile-new-known-dot.csv`
    - `output\reconcile-new-dotdiagnostic.csv`
    - `output\reconcile-new.json`
- 本场整场结论：
  - 插件 DoT 合计：`2,947,000`
  - ACT 已归属 hostile DoT：`3,121,278`
  - ACT 未归属 hostile DoT：`0`
  - ACT hostile 总量：`3,121,278`
  - 插件相对 ACT hostile 总量：`-5.58%`
  - 结论：插件整场略低于 ACT hostile 总量，属于当前“保守统计、优先避免虚高”口径下可接受结果。
- 学者本轮已再次闭环：
  - `四宮輝夜 | 学者`
  - 插件历史 DoT：`1,407,800`
  - DOT诊断合计：`1,407,775`
  - 插件历史 vs DOT诊断：`0.00%`
  - ACT 已归属：`419,390`
  - ACT 非零已知 status：`0`
  - ACT `status=0`：`419,390 / 21 行`
  - ACT 状态窗口存在：`0x767 蛊毒法`、`0xF2B 埋伏之毒`
  - 结论：学者插件内部补算与历史完全对齐；ACT 个人已归属全部落在 `status=0`，不适合作为学者 DoT 真值，不再按 ACT 已归属硬调。
- 贤者本场未出现，无法新增样本；沿用上一轮结论：
  - 贤者 `0xA38 均衡注药III` 已有记录显示 DOT诊断与插件历史对齐；
  - ACT 有状态窗口但归属主要/全部为 `status=0`；
  - 不建议为了贴 ACT 已归属个人数调整贤者算法。
- 本轮收工原则：
  - 当前不要为了贴近 ACT `status=0` 个人已归属数去调大学者、贤者、白魔、骑士等 DoT；
  - 后续只在 ACT 给出明确“非零已知 status”，且多场稳定大幅偏离时，再考虑针对具体技能小范围排查；
  - 目前更有价值的可对账样本是暗黑 `0x2ED 腐秽大地`，本场为插件 `72,100` / DOT诊断 `72,140` / ACT 非零 `0x2ED` 为 `84,028`，约 `-14%`，建议仅记录观察。
- 当前工作区仍然是脏的，`1.txt` 仍然不要误删；不要 reset / checkout 覆盖用户现场。

## 2026-05-16 交接补充：DoT / ACT 对账阶段收工

- 本轮 DoT / ACT 对账详细交接已单独整理：`md/2026-05-16-dot-handoff.md`
- 当前已完成但尚未现场闭环的重点：
  - `tools/DotReconcile` 增强了 `--status-windows`、窗口一致性检查、`--summary-out`、`--csv-known-dot-out`、`--csv-dotdiagnostic-out`，可以同时输出整场总量、职业已知 DoT 专项表和 DOT诊断总表；
  - 继续清理：`tools/DotReconcile/Program.cs` 里的 `WriteExports(...)` 已拆成 `BuildExportContext(...)`、`WriteJsonExport(...)`、`WriteSummaryExport(...)`、`WriteMainCsvExport(...)`、`WriteStatusCsvExport(...)`、`WriteWindowCheckCsvExport(...)`、`WriteKnownDotCsvExport(...)`、`WriteDotDiagnosticCsvExport(...)`，只做结构拆分，不改对账口径；
  - 插件侧扩展了 `DOT诊断：` 聚焦日志；
  - `LocalStatsService` 已改为 active DoT 自然倒计时，不再因目标状态列表短暂看不到 DoT 就在 1 秒后清理；
  - 已从机工 DoT 口径中移除 `0x35C 武装解除`，保留 `0x74A / 0x7E3`，`0x35D 野火` 仍默认不纳入普通 DoT。
- 本轮最后本地构建已通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `dotnet build tools\DotReconcile\DotReconcile.csproj`
  - 均为 `0 warnings / 0 errors`
- 已用 `2026-05-16 19:17:28 ~ 19:24:14` 的“阿卡狄亚登天斗技场 重量级1”记录复核：插件 DoT 合计 `4,396,800`，ACT 已归属 `3,955,409`，ACT 未归属 hostile `640,795`，ACT hostile 总量 `4,596,204`，插件相对 ACT hostile 总量约 `-4.34%`。
- 下次开工第一步：如果继续 DoT 现场验证，优先看 `output\dotreconcile-heavy1-summary.csv`、`output\dotreconcile-heavy1-known-dot.csv`、`output\dotreconcile-heavy1-dotdiagnostic.csv` 的口径；如要验证暗黑 `0x2ED`，打一场包含暗黑的记录并导出 history 后复跑完整命令。
- 当前工作区仍然是脏的，`1.txt` 仍然不要误删；不要 reset / checkout 覆盖用户现场。

## 2026-05-13 交接补充：设置页压缩 / Ikegami 设置区收尾

- 本轮详细交接已单独整理：`md/2026-05-13-settings-window-handoff.md`
- 这一轮重点不在 `StatsPanel.cs` 主渲染，而在：
  - `DalamudACT/UI/SettingsWindow.cs`
  - 持续压缩设置页空白
  - 修复 Ikegami 设置区标签被挤没
  - 修复 `样式分享码` 区域因卡片高度不足造成的裁切/挤压
- 当前 `DrawSettingCard(...)` 已改成按实际内容高度自适应，不再用固定最大行数卡死；
- Ikegami 设置区目前已经完成：
  - 正常中文文案恢复
  - 标签上置
  - checkbox 状态提示
  - `透明度` / `Footer 与字号` 3 列紧凑布局
- 本轮多次重新构建均通过，最近一次仍是：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`
- 如果下一个会话继续沿这条线做，建议优先继续压：
  - `尺寸与对齐`
  - 各组底部帮助说明文字
- 当前工作区仍然是脏的，`1.txt` 仍然不要误删。

## 2026-05-12 交接补充：悬浮窗展示模式下一步方向

- 今天先记需求，不在这一轮继续实现，明天再开工。
- 下一步准备做一个 **悬浮窗展示模式切换**：
  - 配置入口放到 `悬浮面板显示项目` 里；
  - 用它替换当前的 `表格布局参数` 配置入口；
  - 当前现有样式作为其中一种展示模式保留；
  - 新增另一种展示模式：`ikegami` 样式。
- 这一轮的目标更偏向“展示模式切换”，不是继续堆叠表格布局微调项。
- 明天实现时优先关注的文件：
  - `DalamudACT/UI/SettingsWindow.cs`
  - `DalamudACT/UI/FloatingStatsWindow.cs`
  - `DalamudACT/UI/StatsPanel.cs`
  - `DalamudACT/Configuration/PluginConfiguration.cs`
- 建议实现顺序：
  1. 先补配置项与枚举，明确“当前样式 / ikegami 样式”两种模式；
  2. 再把设置入口放进 `悬浮面板显示项目`；
  3. 最后替换现有 `表格布局参数` 相关 UI，并补一次预览验证。

## 2026-05-12 交接补充：治疗职业主题色定稿

- 这轮已经把治疗职业主题色按用户确认结果同步进插件默认值：
  - `白魔法师`：`珍珠白银`（`#D8DDE6`）
  - `占星术士`：`玫瑰灰红`（`#B76E79`）
  - `学者`：`妖精薄荷`（`#66AA96`）
  - `贤者`：`冰晶蓝`（`#7FA8E8`）
- 主题色模式现在支持：
  - 在设置窗口的 `主题色调色板` 中统一调 `主题色透明度`
  - 按职业单独调 RGB
  - 一键 `恢复默认主题色`
- 当前主题色透明度默认值为：`0.75`
- 相关代码位置：
  - `DalamudACT/UI/JobThemePalette.cs`
  - `DalamudACT/Configuration/PluginConfiguration.cs`
  - `DalamudACT/UI/SettingsWindow.cs`
- 这轮已补配置迁移：
  - 配置版本已提升到 `35`
  - 如果用户当前还是上一轮默认治疗配色，会自动迁移到这次确认后的 4 个治疗职业默认色
- 相关用户文档已同步：
  - `README.md`
  - `md/USAGE.md`
- 本地构建已再次通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - 结果：`0 warnings / 0 errors`

## 2026-05-11 交接补充：DoT 状态驱动收尾

- 这次已经把玩家 DoT 的主链路改成“状态驱动 + 3 秒轮询补 tick”：
  - 玩家放已知 DoT 技能后，先记录挂载候选；
  - 目标身上出现对应 debuff 后，纳入活跃 DoT 状态；
  - 后续由 `LocalStatsService.PollActivePlayerDots(...)` 按 3 秒节奏自动补 tick；
  - 目标不可选中时停止后续结算。
- DoT 暴击现在按普通技能那套思路做模拟，不再依赖原始 tick 包里的暴击字段。
- `DalamudACT/Plugin/ACT.cs` 已不再把 DoT tick 当作事件流里的独立记账路径；当前只负责：
  - 识别已知 DoT 技能；
  - 记录应用种子；
  - 让状态轮询接管后续 tick。
- `DalamudACT/Stats/PlayerDotCatalog.cs` 已作为静态白名单使用，按 `actionId / statusId` 过滤 DoT 候选，避免继续靠技能名字猜测。
- 当前代码里 `TryRecordPlayerDotDamage(...)` 仍保留，但主路径已经不再依赖它；如果后面要做清理，可以作为一个单独收尾任务。
- 这版已经重新构建通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - 结果：`0 warnings / 0 errors`
- 如果你只想先看最短版结论，请直接看：[`md/FINAL-HANDOFF.md`](md/FINAL-HANDOFF.md)
- 当前已知风险 / 待观察点：
  - DoT 伤害估算值目前还是保守推导，主要依赖 `status.ParamModifier`、最近一次应用种子、来源平均伤害；
  - 白名单外的技能不会进入 DoT 链路；
  - 仍需要进游戏实际验证：真实 DoT 是否能稳定持续结算、目标失去可选中后是否能正确停算、是否还有遗漏的 DoT 白名单。
- 工作区仍然是脏的，接手前先看：
  - `git status --short`
  - `1.txt` 是未跟踪文件，不要误删。
---

## 旧历史归档说明

`HANDOVER.md` 中早期一大段已损坏编码的历史交接内容，已迁出到：

```text
md/HANDOVER-LEGACY-MOJIBAKE-ARCHIVE.md
```

迁出原因：

- 其中大量中文已变成 mojibake / 乱码，容易误导接手者；
- 部分内容与当前顶部交接重复，且包含过期 HEAD / 旧路径；
- 有价值的专项内容已整理在 `md/` 下对应日期的交接文档中。

如需追溯 2026-05-06～2026-05-12 一带的原始历史，可打开该归档文件，但当前维护请以本文件顶部和最新专项交接为准。
