# 2026-05-21 debug战斗记录功能交接

## 本轮目标

用户要求新增一个独立的 debug 战斗记录功能，用于机制 / 统计排查，记录内容类似战斗流水，但独立成窗，重点记录：

1. BOSS 的平A；
2. BOSS 身上获得了 BUFF，并记录 buff/status id；
3. BOSS 发动了技能，并记录 action id；
4. BOSS 读条了技能，并记录 action id；
5. 队友身上出现了特效标记，并记录标记 id；
6. 队友身上出现了 debuff，并记录 status id；
7. 自己身上出现了特效标记，并记录标记 id；
8. 自己身上出现了 debuff，并记录 status id；
9. 新增“自己 BUFF / 队友 BUFF”监控项，用于区分 Boss 自身 BUFF、我方给 Boss 施加的效果，以及我方角色身上真实获得的 BUFF；
10. 新增“小怪按 BOSS 处理”开关，开启后小怪也按 Boss 进入采集链路；
11. debug 战斗记录悬浮窗需要能折叠，标题栏的默认折叠按钮要可用。

同时要求：

- 上面每一项都能单独开关；
- 还需要一个总的“开始记录”开关；
- 新建独立悬浮窗显示记录内容；
- 内容类似战斗流水；
- 可以复制当前显示内容；
- 需要技能 / 状态 / 标记筛选。

## 本轮已完成

### 1. 配置项

文件：`DalamudACT/Configuration/PluginConfiguration.cs`

已新增 / 补强：

- `DebugCombatLogMaxEntries`
- `DebugCombatRecordingEnabled`
- `DebugRecordBossAutoAttack`
- `DebugRecordBossBuff`
- `DebugRecordBossAction`
- `DebugRecordBossCast`
- `DebugRecordSmallHostileNpcAsBoss`
- `DebugRecordPartyMarker`
- `DebugRecordPartyBuff`
- `DebugRecordPartyDebuff`
- `DebugRecordSelfMarker`
- `DebugRecordSelfBuff`
- `DebugRecordSelfDebuff`

配置版本从 `52` 提升到 `53`，并补了迁移与 `Reset()` 默认值。当前默认行为是：

- `DebugCombatRecordingEnabled = false`
- 其余记录项默认开启
- `DebugRecordSmallHostileNpcAsBoss = false`

### 2. debug 记录数据模型与服务入口

文件：`DalamudACT/Stats/LocalStatsService.cs`

已新增：

- `DebugCombatLogEntry`
- `DebugCombatLogEntryKind`
- `DebugCombatLogEntries`
- `ClearDebugCombatLog()`
- `ApplyDebugCombatLogRetentionLimit()`
- `SetDebugCombatRecordingEnabled(bool enabled)`
- `RecordDebugBossAbility(...)`
- `RecordDebugMarker(...)`

记录模型字段：

- `TimestampLocal`
- `Kind`
- `Message`
- `ActorName`
- `TargetName`
- `PrimaryId`
- `PrimaryText`

其中 `PrimaryId / PrimaryText` 用于承载技能 ID、状态 ID 或标记 ID，方便窗口筛选与复制。

`DebugCombatLogEntryKind` 现在已经覆盖：

- `BossAutoAttack`
- `BossBuff`
- `BossAction`
- `BossCast`
- `PartyBuff`
- `PartyMarker`
- `PartyDebuff`
- `SelfBuff`
- `SelfMarker`
- `SelfDebuff`
- `Recorder`

### 3. BOSS 平A / 发动技能采集

文件：`DalamudACT/Plugin/ACT.cs`

基于现有 `ActionEffectHandler.Receive` Hook 接入。

实现方式：

- 每个 ActionEffect 事件结束后调用 `statsService.RecordDebugBossAbility(...)`；
- `LocalStatsService` 内部再确认 source 是否是 hostile Boss / hostile 战斗 NPC；
- 平A 识别优先按：
  - `actionId == 7`
  - `actionId == 8`
  - 或 Lumina `ActionCategory.RowId == 1`；
- 非平A且来自 Boss 时记录为 `BossAction`。

### 4. BOSS BUFF / BOSS 读条 / 自己 BUFF / 队友 BUFF / 自己 debuff / 队友 debuff 采集

文件：`DalamudACT/Stats/LocalStatsService.cs`

基于 Framework Update 轮询接入：

- `PollDebugCombatRecorderLocked(...)`
- `CaptureDebugBossCastLocked(...)`
- `CaptureDebugBossBuffsLocked(...)`
- `CaptureDebugFriendlyBuffsLocked(...)`
- `CaptureDebugFriendlyDebuffsLocked(...)`

当前轮询间隔：`100ms`。

BOSS BUFF：

- 遍历 ObjectTable 中满足 `StatusFlags.Hostile` 且通过 `ShouldTrackHostileBattleNpc(...)` 的目标；
- 读取 `StatusList`；
- `StatusCategory == 1` 记为 BUFF；
- 如果状态来源能解析为我方 tracked source，则跳过，避免把玩家给 Boss 施加的效果误当成 Boss 自身 BUFF；
- 使用 `DebugObservedStatusKey` 去重，只记录新出现状态。

BOSS 读条：

- 检查 `IBattleChara.IsCasting` 与 `CastActionId`；
- 同一个 Boss 同一个 `CastActionId` 只记录一次；
- 目标从 `CastTargetObjectId` 解析。

自己 / 队友 BUFF：

- 遍历 `EnumerateTrackedPartyBattleCharas()`；
- `StatusCategory == 1` 记为 BUFF；
- 自己与队友分开走 `DebugRecordSelfBuff` / `DebugRecordPartyBuff` 开关；
- 使用 `DebugObservedStatusKey` 去重。

自己 / 队友 debuff：

- 遍历 `EnumerateTrackedPartyBattleCharas()`；
- `StatusCategory == 2` 记为 debuff；
- 自己与队友分开走 `DebugRecordSelfDebuff` / `DebugRecordPartyDebuff` 开关；
- 使用 `DebugObservedStatusKey` 去重。

小怪按 BOSS 处理：

- `ShouldTrackHostileBattleNpc(...)` 现在先判断 `DebugRecordSmallHostileNpcAsBoss`；
- 开关打开后，所有 hostile `BattleNpc` 都会进入 Boss 采集链路；
- 这样小怪的平A、读条、BUFF 都会按 Boss 记录。

### 5. 特效标记采集增强

文件：`DalamudACT/Plugin/ACT.cs`

已尝试接入：

- `PacketDispatcher.HandleActorControlPacket`
- 通过反射读取 `PacketDispatcher.Addresses.HandleActorControlPacket.String`，避免编译期 SDK 对该地址字段的可见性问题。

新增：

- `ActorControlDelegate`
- `HandleActorControlPacket(...)`
- `TryExtractDebugMarkerId(...)`

这一轮已经做了两处关键强化：

1. `TryExtractDebugMarkerId(...)` 不再只看单一参数，而是会从 `param1 ~ param8` 中依次找出第一个非零候选值；
2. `RecordDebugMarker(...)` 记录时会同时尝试 `targetId` 和 `entityId`，优先把真正的自己 / 队友目标收进来，避免只靠单一字段漏掉队友头顶标记。

当前实现仍然是保守的“疑似特效标记”抽取：

- 只在目标能识别为自己或队友时真正写入；
- 目标如果最终被判断成 hostile NPC，则不会写入标记记录；
- 记录内容会保留 `category / param1 / param2` 等 raw 值；
- 下次实战需要根据实际 raw 值确认：
  - 是否能命中特效标记；
  - 是否噪声过多；
  - 需要把哪些 category 精确纳入或排除。

### 6. 独立 debug 战斗记录悬浮窗

新增文件：`DalamudACT/UI/DebugCombatLogWindow.cs`

窗口名：

- `debug战斗记录###DebugCombatLogWindow`

已支持：

- 总开关：`开始记录`
- 自动滚动
- 十一个记录项独立开关
- 复制当前显示
- 清空记录
- 保留条数：`500 / 2000 / 10000 / 50000 / 全部`
- 类型筛选
- 角色筛选
- 目标筛选
- 技能 / 状态 / 标记筛选
- 搜索框
- 标题栏可正常折叠 / 展开（已去掉 `NoCollapse`）

表格列：

- 时间
- 类型
- 角色
- 目标
- ID / 技能
- 内容

复制格式为按当前筛选结果导出的文本行。

### 7. UI 入口

文件：

- `DalamudACT/UI/PluginUI.cs`
- `DalamudACT/UI/MainWindow.cs`
- `DalamudACT/UI/SettingsWindow.cs`

已新增：

- 主窗口按钮：`打开debug战斗记录`
- 设置窗口按钮：`打开debug战斗记录`
- 设置页 `数据与状态` 中新增 `debug战斗记录` 设置卡片
- 卡片内同步展示全部 11 个记录项开关与 `小怪按BOSS处理`

## 本轮构建验证

已执行：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

结果：

- `0 warnings`
- `0 errors`

产物：

- `E:\git\DalamudACT\output\DalamudACT.dll`

## 当前工作区状态提醒

收工前执行 `git status --short`，当前工作区仍然是脏的，而且包含不少历史遗留改动。

这轮明确新增 / 修改的交接相关文件是：

- `DalamudACT/UI/DebugCombatLogWindow.cs`
- `md/2026-05-21-debug-combat-log-handoff.md`
- `md/SESSION-HANDOFF.md`
- `HANDOVER.md`

此外，`1.txt` 仍然是旧现场里保留下来的未跟踪文件，不要误删。

注意：

- 其中不少文件是本轮开始前就已经存在的历史现场改动；
- 不要做破坏性 git 操作；
- `1.txt` 仍然不要误删；
- 不要执行 `git reset --hard`、`git checkout -- .` 等会覆盖用户现场的操作。

## 下次开工建议顺序

### 1. 先用真实战斗测试稳定项

优先验证这些记录是否出现，且没有明显刷屏：

1. `Boss发动技能`
2. `Boss读条技能`
3. `Boss平A`
4. `Boss获得BUFF`
5. `自己BUFF`
6. `队友BUFF`
7. `自己debuff`
8. `队友debuff`
9. 开启 `小怪按BOSS处理` 后，小怪是否也会进入 Boss 记录
10. 队友头顶标记是否能被稳定记录

建议操作：

1. 打开主窗口；
2. 点击 `打开debug战斗记录`；
3. 勾选 `开始记录`；
4. 保持默认记录项都开启；
5. 打一场短副本或木桩 / 讨伐类战斗；
6. 点 `复制当前显示`，把结果贴回来核对。

### 2. 重点校准“特效标记”

这是当前最不确定的一块。

下次需要看窗口里是否出现类似：

```text
自己/队友 xxx 身上出现特效标记：id=0x...（category=0x...，param1=0x...，param2=0x...）
```

如果没有出现：

- 说明当前 `ActorControl` 抽取条件没有命中真实头顶 / 特效标记，需要换分类或改用其他网络事件路径。

如果出现太多无关内容：

- 根据 raw `category / param1 / param2` 收窄 `TryExtractDebugMarkerId(...)`。

建议下一步不要一次性大改 Hook，只围绕真实 raw 值小步校准。

### 3. 检查 ActorControl Hook 运行时安全性

因为本轮新增了 `ActorControl Hook`，下次进游戏后要先看 Dalamud 日志或最近日志：

- 是否出现 `已安装 ActorControl Hook`；
- 是否出现 `安装 ActorControl Hook 失败`；
- 是否出现 `处理 ActorControl 事件失败`。

如果 ActorControl Hook 安装失败：

- 其他 debug 记录功能仍应可用；
- 只有特效标记记录不可用；
- 优先确认当前运行时 `PacketDispatcher.Addresses.HandleActorControlPacket` 签名是否可用。

### 4. 校准 Boss BUFF / 自己 BUFF / 队友 BUFF 口径

当前 Boss BUFF 使用 `StatusCategory == 1`，并跳过能解析到玩家来源的状态。

如果实战发现 Boss BUFF 噪声过多，或者自己 / 队友 BUFF 记录不稳定：

- 优先看来源是否为 0 或 hostile；
- 可以增加更细过滤，例如只记录来源为 Boss 自己、来源为空、或来源 hostile 的 BUFF；
- 不要把玩家施加给 Boss 的 dot / debuff 纳入 Boss BUFF。

### 5. 小怪按 BOSS 处理

如果用户反馈某些副本里的小怪没有进入 debug 记录：

- 优先检查是否忘记勾选 `小怪按BOSS处理`；
- 该开关开启后，hostile 小怪会和 Boss 一样走读条 / BUFF / 平A 记录链路；
- 若仍有漏记，再进一步查 `ShouldTrackHostileBattleNpc(...)` 的场景过滤。

### 6. 文档 / 发布暂缓

本轮只做功能和交接记录，未同步正式 changelog / release notes。

等用户确认实战可用后，再考虑：

- `md/CHANGELOG.md`
- `md/RELEASE-NOTES.md`
- README 使用说明
- 版本号与 repo metadata

## 一句话总结

本轮已经把 debug 战斗记录功能的第二版主体做完并通过构建：配置、记录服务、BOSS 平A / 技能 / BUFF / 读条、自己 / 队友 BUFF 与 debuff、独立悬浮窗、复制与筛选、特效标记增强、小怪按 Boss 处理、以及窗口可折叠都已接入；下次重点不是继续堆功能，而是用真实战斗校准“特效标记 ActorControl 识别”和各类 BUFF 噪声，并确认新 Hook 在当前 Dalamud 运行时稳定。
