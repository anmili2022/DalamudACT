# 2026-05-23 Actors 模块拆分交接

## 本轮目标

按上一轮建议继续拆 `LocalStatsService.Actors.cs`。

本轮只做结构拆分，把 Actor / ObjectTable / PartyList / BuddyList / owner cache / 本地统计对象身份归一相关逻辑从 `LocalStatsService.cs` 迁移到独立 partial 文件中。

执行原则：

- 不改统计口径；
- 不改 Actor 归属规则；
- 不改宠物 / 召唤物 / NPC 队友归属判断；
- 不改 DoT / Wildfire、debug 战斗记录、Encounter、历史记录和测试数据行为；
- 保留原有中文注释和排查说明；
- 拆完必须构建验证。

## 已完成

### 1. 新增 Actors partial 文件

新增文件：

```text
DalamudACT/Features/Stats/LocalStatsService.Actors.cs
```

该文件现在承载：

- owner cache；
- 已观测友方 Actor cache；
- 队友 HP cache；
- ObjectTable 反查；
- PartyList / BuddyList / 本地玩家身份归一；
- 宠物 / 召唤物 / 陆行鸟 / NPC 队友归属解析；
- hostile / friendly / player `TrackedActor` 构造；
- Actor ID 等价判断；
- 事件 Actor ID 规范化；
- `TrackedActor` 等嵌套类型。

### 2. 已迁移的字段

已从 `LocalStatsService.cs` 迁移：

- `OwnerCacheTtl`
- `OwnerCacheWarmupInterval`
- `ownerCache`
- `observedFriendlyActorCache`
- `partyMemberHpCache`
- `lastOwnerWarmupUtc`

说明：

- `InvalidActorId` 仍保留在 `LocalStatsService.cs`，因为 Formatting、Actors、Dots、Encounter 等多个 partial 都会用到。
- `gate` 仍保留在 `LocalStatsService.cs`，作为整个服务的共享锁。

### 3. 已迁移的公开入口

已从 `LocalStatsService.cs` 迁移：

- `WarmOwnerCacheFromObjectTable()`
- `IsTrackedActor(...)`
- `CanResolveTrackedSource(...)`
- `TryResolveTrackedSourceFromGameObject(...)`
- `ObserveFriendlyCombatantFromGameObject(...)`
- `ObserveFriendlyCombatantIdentity(...)`

调用方不需要改引用，因为这些方法仍在同一个 `partial class LocalStatsService` 中。

### 4. 已迁移的 Actor 解析和归属 helper

已从 `LocalStatsService.cs` 迁移：

- `TryResolveCombatantSource(...)`
- `TryResolveTrackedSource(...)`
- `ResolveOwner(...)`
- `TryGetResolvableOwnerId(...)`
- `ResolvePartyMemberActorId(...)`
- `TryGetTrackedActor(...)`
- `TryGetTrackedBattleCharaActor(...)`
- `TryGetPartyMemberTrackedActor(...)`
- `TryGetTrackedPartyBattleCharaActor(...)`
- `TryGetBuddyTrackedActor(...)`
- `TryGetFriendlyBattleNpcTrackedActor(...)`
- `TryGetHostileBattleNpcTrackedActor(...)`
- `MatchesPartyMemberActor(...)`
- `MatchesBuddyActor(...)`
- `AreSameGameObject(...)`
- `AreEquivalentActorIds(...)`
- `FindObjectByActorId(...)`
- `ResolveBattleCharaActorId(...)`
- `NormalizeEventActorId(...)`
- `LooksLikeCombatActorId(...)`
- `CreateTrackedActor(...)`
- `ResolvePartyMemberTrackedActorKind(...)`
- `ResolveTrackedActorKind(...)`
- `EnumerateTrackedPartyBattleCharas(...)`
- `ResolvePartyMemberBattleChara(...)`
- `ResolveBuddyBattleChara(...)`
- `TryMarkUniqueBattleChara(...)`
- `ResolveBattleCharaUniqueId(...)`
- `ResolveBuddyActorId(...)`
- `TryGetGameObjectId(...)`
- `GetGameObjectIdentity(...)`
- `GetPartyMemberIdentity(...)`
- `GetBuddyIdentity(...)`
- `TryResolveBattleCharaFromIdentity(...)`
- `TryGetPropertyActorId(...)`
- `TryConvertActorId(...)`
- `GetLocalPlayerIdentity(...)`
- `TryGetLocalPlayerTrackedActor(...)`
- `IsFriendlyTrackedBattleNpc(...)`
- `TryCreateObservedFriendlyActor(...)`
- `TryCreateNamedFriendlyActorFromGameObject(...)`
- `LooksLikeDutyCompanionName(...)`
- `HasFriendlyBattleNpcIndicators(...)`
- `ShouldResolveOwnerForObject(...)`
- `IsDutyNpcPartyMemberKind(...)`
- `IsLocalPlayerActor(...)`

### 5. 已迁移的嵌套类型

已从 `LocalStatsService.cs` 迁移：

- `ActorIdentity`
- `OwnerCacheEntry`
- `TrackedActorKind`
- `TrackedActor`

## 当前结构状态

当前 `Features/Stats`：

```text
DalamudACT/Features/Stats/
├─ LocalStatsService.cs                  # 统计核心主文件，保留构造、共享常量、状态反射 helper
├─ LocalStatsService.Actors.cs           # Actor / ObjectTable / Party / Buddy / owner cache 模块
├─ LocalStatsService.DebugRecorder.cs    # debug 战斗记录模块
├─ LocalStatsService.Dots.cs             # 玩家 DoT / Wildfire / DOT 诊断模块
├─ LocalStatsService.Encounter.cs        # 当前战斗 / 流水 / 结算 / ACTX 快照模块
├─ LocalStatsService.Formatting.cs       # 通用文本 / 数字格式化模块
├─ LocalStatsService.History.cs          # 历史记录 / 预览 / 导入导出模块
├─ LocalStatsService.TestData.cs         # 内置测试数据模块
└─ PlayerDotCatalog.cs                   # 玩家 DoT 技能 / 状态目录
```

拆分后文件行数大致为：

```text
DalamudACT/Features/Stats/LocalStatsService.cs                  248
DalamudACT/Features/Stats/LocalStatsService.Actors.cs           942
DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs    661
DalamudACT/Features/Stats/LocalStatsService.Dots.cs             2642
DalamudACT/Features/Stats/LocalStatsService.Encounter.cs        1066
DalamudACT/Features/Stats/LocalStatsService.Formatting.cs       95
DalamudACT/Features/Stats/LocalStatsService.History.cs          402
DalamudACT/Features/Stats/LocalStatsService.TestData.cs         579
```

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

## 注意事项

- 本轮没有改 Actor 解析逻辑；
- 本轮没有改 owner cache TTL；
- 本轮没有改 PartyList / BuddyList / ObjectTable 遍历顺序；
- 本轮没有改玩家 / 友方 NPC / hostile NPC 判定；
- 本轮没有改 DoT / Wildfire 口径；
- 本轮没有改 debug 战斗记录口径；
- 本轮没有改历史记录 JSON 结构；
- 当前工作区在本轮前就不是干净状态，不要把历史移动误判为本轮删除；
- `1.txt` 不要误删；
- 不要执行：

```powershell
git reset --hard
git checkout -- .
```

## 下一步建议

继续按结构化路线推进时，建议下一步拆：

```text
LocalStatsService.Status.cs
```

优先迁移：

- `ResolveStatusSourceActorId(...)`
- `TryGetStatusGameDataText(...)`
- `TryExtractGameDataText(...)`
- `TryGetStatusGameDataInt(...)`
- `GetStatusId(...)`
- `TryGetStatusParam(...)`
- `GetStatusRemainingTime(...)`
- `GetReflectedStatusValue(...)`
- `EnumerateStatusEntries(...)`

风险：较低到中等。状态反射 helper 被 Dots 和 DebugRecorder 共用，迁移后要立即构建验证。

继续执行：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

保持：

```text
0 warnings / 0 errors
```

## 后续进展补充

`LocalStatsService.Status.cs` 已在后续完成拆分，详见：`md/2026-05-23-status-module-refactor-handoff.md`。

原建议的状态反射 helper 拆分已完成；后续如继续推进结构化，建议改为拆 `LocalStatsService.Dots.Wildfire.cs`。

