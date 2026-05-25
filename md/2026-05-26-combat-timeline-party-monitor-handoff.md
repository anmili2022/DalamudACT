# 2026-05-26 战斗流水与技能监控交接

本文记录 2026-05-26 对 `战斗流水`、`技能监控悬浮窗`、插件元数据和技能目录的修改，供后续维护接手。

## 变更范围

- 战斗流水事件区域由表格改为多行文本列表。
- 战斗流水支持点击多选、Shift 连续多选、复制选中行。
- 战斗流水筛选时保留战斗开始/结束上下文。
- 战斗流水新增玩家/队友获得 BUFF/debuff 状态事件。
- 修复治疗事件在战斗结束后独立开启假战斗。
- 技能监控悬浮窗支持右键打开/关闭设置窗口。
- 技能监控悬浮窗 `技能监控` 标题支持左键折叠/展开。
- 修正贤者 `坚角清汁[24298]` 的冷却和持续时间。
- 插件作者元数据改为 `Shiyuvi, Anmi`。

## 战斗流水 UI

文件：`DalamudACT/Features/CombatTimeline/CombatTimelineWindow.cs`

当前显示结构：

- 顶部固定行：`开始记录`、记录状态、`自动滚动到最新事件`。
- 第二行固定按钮：`复制当前显示`、`复制选中(N)`、`取消选中`、`清空流水`。
- 折叠栏 `保留与筛选`：保留条数、角色/目标/技能筛选、快捷筛选。
- 事件区：非表格，逐行显示 `[HH:mm:ss] 事件内容`。

多选行为：

- 点击行：切换选中/取消选中。
- Shift+点击：从上次选中行到当前行连续选中。
- `复制选中(N)`：按显示顺序复制选中行，带毫秒时间戳。
- 筛选结果数量变化时会清空选中状态，避免复制错行。

筛选上下文规则：

- 只筛角色、目标或技能时，仍显示 `进入战斗` / `战斗结束`。
- 如果明确选择事件类型，则按事件类型过滤，不强行插入其它类型。

## 战斗流水状态事件

文件：`DalamudACT/Features/Stats/LocalStatsService.Encounter.cs`

新增 `CombatTimelineEntryKind.Status`，用于记录友方获得状态：

```text
丹凤吟 获得BUFF 坚角清汁[24298]，来源 Lokis，剩余 15.0s。
墨乄染 获得debuff 易伤[1234]，来源 Boss，剩余 30.0s。
```

采集规则：

- 只采集本地玩家、队友、可识别 NPC 队友。
- 通过 `EnumerateTrackedPartyBattleCharas()` 轮询状态。
- 100ms 轮询一次。
- 首次进入战斗只建立基线，不把已有状态刷屏写入。
- 状态消失后再次获得，会重新记录。
- 使用现有反射状态读取工具，继续兼容 Dalamud 状态类型差异。

注意事项：

- 当前状态分类仍依赖 `StatusCategory == 1/2` 区分 BUFF/debuff。
- 食物监控仍单独使用 `StatusId = 48` 判断，不应被这里的通用状态分类替代。
- 如果状态事件太多，后续可加独立开关或白名单/黑名单。

## 假战斗修复

文件：`DalamudACT/Features/Stats/LocalStatsService.Encounter.cs`

本轮样例：

```text
战斗结束：遗忘行路雾之迹，持续 01:03。
Lokis 使用均衡预后II[37034] 治疗 ...
战斗结束：遗忘行路雾之迹，持续 00:01。
```

修复点：

- `RecordHeal()` 在当前战斗未开始时直接返回。
- 治疗事件不再独立调用 `MarkActivity()` 拉起新战斗。
- 无有效数据的假战斗结算不再向战斗流水追加 `战斗结束（未记录到有效战斗数据）`。
- 若已经追加过对应 `CombatStart`，无有效数据结算会移除最后一个 `CombatStart`。

## 技能监控悬浮窗

文件：`DalamudACT/Features/PartyMonitor/PartyMonitorWindow.cs`、`DalamudACT/UI/PluginUI.cs`

变化：

- 合并模式标题从 `技能` 改为 `技能监控`。
- 右键悬浮窗：打开/关闭设置窗口。
- 左键点击 `技能监控` 标题：折叠/展开悬浮窗。
- 折叠时只显示标题，并通过调整窗口大小实现。
- 展开时恢复折叠前窗口大小。

限制：

- 开启 `锁定队友监控窗口` 后窗口使用 `NoInputs` 鼠标穿透，无法接收右键/左键点击；需要先解锁再操作。
- 当前折叠入口只在 `团辅减伤合并` 模式下的 `技能监控` 标题显示；非合并模式仍显示 `团辅` / `减伤` 标题。

## 技能目录

文件：`DalamudACT/Features/PartyMonitor/PartySkillCatalog.cs`

修正：

```csharp
Register(40, new(24298, "坚角清汁", SkillCategory.Mitigation, 30f, 15f, 24298));
```

- 冷却：`30s`
- 持续：`15s`

## 作者元数据

插件作者字段已改为：

```text
Shiyuvi, Anmi
```

修改位置：

- `DalamudACT/DalamudACT.csproj`
- `DalamudACT/DalamudACT.json`
- `Data/DalamudACT.json`
- `repo.json`

仓库 URL 中的 `anmili2022` 是 GitHub 地址，不是作者字段。

## 发布注意

本轮发布只应暂存以下相关文件：

- `README.md`
- `md/2026-05-26-combat-timeline-party-monitor-handoff.md`
- `DalamudACT/DalamudACT.csproj`
- `DalamudACT/DalamudACT.json`
- `Data/DalamudACT.json`
- `repo.json`
- `DalamudACT/Features/CombatTimeline/CombatTimelineWindow.cs`
- `DalamudACT/Features/PartyMonitor/PartyMonitorWindow.cs`
- `DalamudACT/Features/PartyMonitor/PartySkillCatalog.cs`
- `DalamudACT/Features/Stats/LocalStatsService.Encounter.cs`
- `DalamudACT/UI/PluginUI.cs`

工作区仍有大量无关未提交改动，不要一键 `git add -A`。

## 验证

本地验证命令：

```powershell
dotnet build 2>&1
```

当前本地构建结果：`0 个警告，0 个错误`。
