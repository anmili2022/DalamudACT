# 2026-05-29 时间轴草稿与 TTS 交接

## 范围

本次工作围绕时间轴功能补齐了以下能力：

- ACT 网络日志生成时间轴草稿。
- 选择具体日志文件和具体战斗段生成草稿。
- 使用 AEAssist 额外资源标注 `AOE` / `死刑`。
- DailyRoutines TTS 播报和依赖检测。
- 无时间轴副本的即时 AOE / 死刑 TTS。
- 战斗流水记录 Boss 读条。
- 用户配置目录注册普通 `永远之暗歼灭战` 时间轴。

## 关键文件

- `DalamudACT/Features/Timeline/TimelineLogImporter.cs`
- `DalamudACT/Features/Timeline/AeAssistResourceDownloader.cs`
- `DalamudACT/Features/Timeline/TimelineLogEncounterOption.cs`
- `DalamudACT/Features/Timeline/TimelineService.cs`
- `DalamudACT/Features/Timeline/M9STimelineParser.cs`
- `DalamudACT/Features/Timeline/TimelineModels.cs`
- `DalamudACT/UI/Windows/SettingsWindow.TimelineStyle.cs`
- `DalamudACT/UI/Windows/TimelineWindow.cs`
- `DalamudACT/Infrastructure/WindowsFileDialog.cs`
- `DalamudACT/Infrastructure/DalamudApi.cs`
- `DalamudACT/Features/Stats/LocalStatsService.Encounter.Timeline.cs`
- `DalamudACT/Features/Stats/LocalStatsService.Encounter.Types.cs`
- `DalamudACT/Features/CombatTimeline/CombatTimelineWindow*.cs`
- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/Plugin/ACT.ActionEffect.cs`

## 用户配置目录

时间轴用户覆盖目录：

```text
C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT\Timeline
```

正式用户时间轴目录：

```text
Timeline\Data
```

草稿目录：

```text
Timeline\Generated
```

额外资源目录：

```text
Timeline\Resource
```

## ACT 日志生成草稿

设置入口在 `时间轴设置` 下。

当前 UI 包含：

- ACT 日志目录输入框。
- 具体日志文件路径输入框。
- `选择日志文件`。
- `使用最新日志`。
- `刷新战斗列表`。
- `选择战斗` 下拉。
- `生成时间轴草稿`。
- `打开草稿目录`。
- `刷新额外资源`。

生成逻辑：

- 优先使用 `ActLogFilePath` 指定的具体日志文件。
- 未指定具体文件时，使用 `ActLogDirectory` 中最新的 `Network*.log`。
- 解析 ACT 网络日志 `01` 区域行、`03` 战斗对象行、`20` 读条行、`21` 技能效果行、`260` 战斗状态行。
- 使用 `03` 行过滤敌对 NPC，排除信赖/剧情友方 NPC。
- 刷新战斗列表后，按 Boss 技能时间段拆分 encounter。
- 下拉显示格式为 `开始时间  时长 mm:ss  副本名 / 主要Boss名 (条目数)`。
- 选择战斗后，生成草稿只使用该战斗段。
- 未选择战斗时，默认使用最新一场可用战斗。

## 草稿时间基准

草稿时间应以 ACT `260` 进战时间为 `0.0`。

本次已把导入器改为记录 `260` 战斗开始时间，并在拆分战斗段后把草稿时间相对该进战时间输出。

如果后续仍出现整体偏移，优先检查：

- ACT 日志里该战斗段前是否有正确 `260|...|1|1|...` 行。
- `ResolveCombatStartTime()` 是否找到距离第一条 Boss 技能 20 分钟内最近的进战时间。
- 副本是否存在中途重置或特殊剧情进战状态。

## 草稿清理规则

生成器现在会自动清理/合并：

- 只保留敌对 NPC 来源。
- 跳过 `unknown_*` 且未命中 AE 额外资源的技能。
- 同一时间附近、同名、同事件类型的事件合并成 `id: ["..."]`。
- `StartsUsing` 合并窗口为 `0.25s`。
- `Ability` 合并窗口为 `1.0s`。
- 合并时 source 不要求完全相同，优先保留主要 Boss 名。
- 如果 `Ability` 在同名同 ActionId 的 `StartsUsing` 后 `0-15s` 内出现，则删除 `Ability`，只保留读条。
- 删除 `Ability` 时不要求 source 相同，适配 `永远之暗` / `青之魂块` 这类来源拆分。

例子：

```txt
29.3 "暗之死腕" StartsUsing { id: ["ADED", "AE43"], source: "永远之暗" }
```

## AEAssist 额外资源

新增下载器：

```text
DalamudACT/Features/Timeline/AeAssistResourceDownloader.cs
```

下载地址：

```text
https://raw.githubusercontent.com/aeassist-acr/Resource/main/AoeActions.json
https://raw.githubusercontent.com/aeassist-acr/Resource/main/TankDeathSentence.json
```

缓存位置：

```text
Timeline\Resource\AoeActions.json
Timeline\Resource\TankDeathSentence.json
```

使用范围：

- 生成草稿时自动追加 `# AOE` / `# 死刑`。
- 无时间轴即时 TTS 时识别 AOE / 死刑。
- 不参与正常已加载时间轴的运行时分类。

正常时间轴运行时分类优先级：

1. 时间轴行尾人工标记，例如 `# AOE`、`# 死刑`。
2. cactbot 静态 `Responses.xxx` 分类。
3. 无分类时只显示技能名。

## 人工机制标记

时间轴行尾支持：

```txt
# AOE
# 范围
# 死刑
# 分散
# 分摊
# 远离
# 靠近
# 背对
# 击退
# 踩塔
# 停止
# 移动
```

`# 范围` 会归一成 `AOE`。

显示/TTS 会优先使用人工标记。

## DailyRoutines TTS

TTS 使用命令：

```text
/pdr tts 文本
```

开启 `DailyRoutines TTS` 时会检测 `/pdr` 命令是否注册。

如果用户未安装或未启用 DailyRoutines：

- 不允许开启 TTS。
- 设置页提示未检测到 DailyRoutines。
- 聊天框发送文本通知。

TTS 内容模式：

- `机制类型+技能名`
- `仅机制类型`
- `仅技能名`

`仅机制类型` 已改为严格模式：没有机制类型时不再退回播技能名。

### 2026-06-02：最终 TTS 文本 2 秒去重

背景：

- 排查游戏崩溃时发现，时间轴/机制播报会在毫秒级连续发送多条相同的 `/pdr tts` 命令。
- 典型日志表现为同一时刻连续出现多条同样文本，例如 `诶欧意`。
- DailyRoutines / EdgeTTS 每收到一条 `/pdr tts` 都可能创建一条播放任务，短时间并发进入 NAudio / Windows 音频输出链路，增加崩溃风险。

根因：

- 旧逻辑主要按时间轴条目、技能应对 key、`sourceId + actionId` 去重。
- 多个不同事件经过 TTS 修正后可能得到同一句最终文本，例如多个 AOE 最终都修正成 `诶欧意`。
- 旧逻辑没有按“最终发给 DailyRoutines 的文本”去重，因此仍会连续发送多条相同 `/pdr tts`。

本次处理方案：

- 文件：`DalamudACT/Features/Timeline/TimelineService.cs`
- 新增固定窗口：

```csharp
private const double TimelineTtsDuplicateSuppressSeconds = 2d;
```

- 新增最终文本发送记录：

```csharp
private readonly Dictionary<string, DateTime> lastTimelineTtsTextUtc = new(StringComparer.Ordinal);
```

- 新增统一发送入口：

```csharp
PrepareDailyRoutinesTtsText(...)
TrySendDailyRoutinesTts(...)
TrySendPreparedDailyRoutinesTts(...)
PruneTimelineTtsTextDedupe(...)
```

- 去重发生在 `ApplyTtsCorrections(...)` 和 `SanitizeTtsText(...)` 之后，也就是按最终实际发送文本判断。
- 同一句最终 TTS 文本在 2 秒内只发送一次。
- 如果重复被抑制，会写 Debug 日志：

```text
抑制重复 TTS（...）：文本
```

已收口的发送来源：

- 时间轴提前播报。
- 技能应对方案 TTS。
- 无时间轴即时 AOE / 死刑 TTS。

保留不变的逻辑：

- `spokenTtsKeys`
- `spokenActionResponseKeys`
- `lastInstantTtsByActionKey`
- 无时间轴即时 TTS 原本的 `sourceId + actionId` 去重。

本次没有做：

- 没有修改 DailyRoutines。
- 没有新增 TTS 队列。
- 没有新增 UI 配置项。
- 没有对不同文本做全局限流。

预期效果：

```text
02:40:10.157 /pdr tts 诶欧意
02:40:10.160 抑制重复 TTS：诶欧意
02:40:10.161 抑制重复 TTS：诶欧意
02:40:10.162 抑制重复 TTS：诶欧意
```

也就是 DailyRoutines 实际只收到第一条相同文本，后续 2 秒内的相同最终文本不会再触发新的 TTS 播放任务。

验证：

```powershell
dotnet build DalamudACT.sln
```

结果：

```text
已成功生成。
0 个警告
0 个错误
```

## 无时间轴即时 TTS

当满足以下条件时，会即时播报：

- `DailyRoutines TTS` 开启。
- 当前区域没有加载时间轴。
- Boss 当前读条或技能命中 ActionId 命中 AE 额外资源。

播报内容：

```text
AOE
死刑
```

即时 TTS 通过 Framework 轮询 `IBattleChara.IsCasting`，不启用 Cast Hook。

去重逻辑：

- key 为 `sourceId + actionId`。
- 去重时间按 Action 表读条时间计算。
- `max(8s, Cast100ms / 10 + 3s)`，上限 `60s`。

## 战斗流水 Boss 读条

新增 `CombatTimelineEntryKind.Cast`。

当战斗流水记录开启、当前进战、当前战斗已开始时：

- 每 `100ms` 最多扫描一次 `ObjectTable`。
- 轮询敌对 `IBattleChara` 的读条状态。
- 记录格式类似：

```text
永远之暗 开始读条 暗之死腕 (ADED)。
```

没有启用 Cast Hook。

性能注意：

- 只有战斗流水记录开启时才扫描。
- 扫描有 `100ms` 节流。
- 同一 `sourceId + actionId` 有短时间去重。

## 永远之暗用户时间轴

已把新草稿注册为普通 `永远之暗歼灭战` 时间轴。

正式文件：

```text
C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT\Timeline\Data\generated\necron-normal.cn.txt
```

索引：

```text
C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT\Timeline\Data\timeline-index.json
```

索引条目：

```json
{
  "id": "necron-normal-generated",
  "name": "永远之暗歼灭战",
  "zoneId": 1295,
  "zoneNameContains": [
    "永远之暗歼灭战"
  ],
  "file": "generated/necron-normal.cn.txt"
}
```

注意：用户配置目录的 `timeline-index.json` 必须是标准 JSON 数组，不能被 PowerShell 包成 `{ "value": [...] }`。

## 已知问题和后续建议

- 下次优先做：合并 `ACT日志目录` 和 `具体日志文件路径` 两个文本框，减少设置区混乱。
- 下次优先做：增加一个 `草稿文件路径` 文本框和一个 `转正草稿` 按钮，用于把指定草稿复制到 `Timeline/Data/generated/...` 并更新 `timeline-index.json`。
- Windows 原生文件选择框已经加了错误码诊断，但仍保留手动输入完整日志路径的兜底。
- 草稿时间准确性依赖 ACT `260` 进战状态行；如果日志缺失该行，会回退到第一条保留事件时间。
- 普通 `永远之暗歼灭战` 时间轴来自实战草稿，仍建议继续人工校对机制名、删减重复或不需要提示的条目。
- 战斗流水读条轮询目前使用反射属性名兼容，不同 Dalamud 运行时如果属性名变化，需要看 Debug 日志并补充字段名。
- 若后续要降低读条轮询开销，可把 `100ms` 改成 `250ms`，或增加独立设置 `战斗流水记录Boss读条`。

## 验证

最近一次验证命令：

```powershell
dotnet build 2>&1
```

结果：

```text
已成功生成。
0 个警告
0 个错误
```
