# 2026-05-23 debug 战斗记录友方合并与控制区折叠交接

## 本轮背景

- 用户最新要求：
  - debug 战斗记录窗口截图红框区域可以折叠；
  - 记录项不再分“队友”和“自己”，统一显示为“友方”；
  - Boss 的 `BUFF` 显示改成 `BUFF/debuff`；
  - 友方增加“技能”记录项。
- 之前用户仍反馈截图里的红色头顶标记没有记录；本轮没有重新打开高风险 ActorControl Hook，只做 UI / 记录项收口。
- 当前项目路径：`E:\git\DalamudACT`。

## 本轮完成内容

### 1. debug 战斗记录顶部控制区可折叠

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

### 2. 记录项 UI 从“队友 / 自己”合并为“友方”

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

### 3. 表格类型与筛选类型统一为“友方”

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

### 4. 记录内容里的“自己 / 队友”统一改为“友方”

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

### 5. Boss `BUFF` 改成 `BUFF/debuff`

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

### 6. 友方新增“技能”记录项

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

### 7. ActorControl Hook 仍保持禁用

- 文件：`DalamudACT/Plugin/ACT.cs`
- 当前仍保持：

```csharp
private static bool ShouldInstallActorControlHook => false;
```

- 禁用原因见：`md/2026-05-23-actorcontrol-crash-handoff.md`
- 不要为了红色头顶标记直接改回 `true`；之前 appcrash 已确认会在 `HookFromAddress` / `HookManager.FollowJmp` / `MemoryHelper.ReadRaw` 路径触发原生崩溃。

## 本轮构建结果

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

## 下次接手优先验证

### 1. 先做游戏内 UI 验证

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

### 2. 红色头顶标记仍需专项排查

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

### 3. 文档 / 版本发布暂不做

本轮只写交接文档，没有同步正式：

- `CHANGELOG.md`
- `RELEASE-NOTES.md`
- README 使用说明
- 仓库发布 metadata

建议等用户游戏内确认 UI 和记录口径可用后再同步正式发布文档。

## 当前工作区注意事项

- 当前工作区仍是脏的，包含大量结构拆分遗留改动和新增目录。
- 未跟踪文件 `1.txt` 不要误删。
- 不要执行：

```powershell
git reset --hard
git checkout -- .
```

- 不要做任何会覆盖用户现场的批量清理。

