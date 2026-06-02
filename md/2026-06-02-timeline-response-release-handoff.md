# 2026-06-02 时间轴应对方案与 0.15.2.54 发布记录

## 本轮目标

- 修复时间轴注释里的 `读条ID` / `结算ID` 应对方案无法被插件正确使用的问题。
- 避免倒计时条显示 `读条IDxxx`、`结算IDxxx` 这类元数据。
- 增加简易设置里打开战斗流水的入口。
- 增加 TTS 纠偏 `对地 -> 对帝`。
- 按发布流程发布 `0.15.2.54`。

## 关键结论

### 1. 原 `读条ID` 注释问题

典型时间轴行：

```txt
3573.6 "前尾祸剑击/后尾祸剑击 1" Ability { id: ["8EED", "8EEE"], source: "蟒蛇将 詹德" } # 读条ID 8EED 去背后 # 读条ID 8EEE 去正面
```

旧逻辑问题：

- `RemoveMetadataCommentSegments` 按 `#` 分段后，只要段落包含 `读条ID` / `结算ID` 就整段过滤。
- 因此 `读条ID 8EED 去背后` 会连同 `去背后` 一起被删除。
- `ParseActionResponses` 得不到 `ActionResponses`。
- `ParseMechanicHint` 也得不到纯机械提示。

本轮修复：

- 新增 `TaggedResponseRegex`，直接从原始注释解析 tagged response。
- 支持格式：

```txt
# 读条ID 8EED 去背后 # 读条ID 8EEE 去正面
# 读条ID8EED 去背后；结算id8EEE 去正面
```

- 解析结果示例：

```txt
8EED -> StartsUsing / 去背后
8EEE -> StartsUsing / 去正面
```

- 只接受 `id` 出现在该时间轴行 `id: ...` 列表里的响应，避免注释里的无关 ID 生效。

### 2. 播报语义调整

本轮定下的语义：

- `# AOE`、`# 分摊` 这种纯注释：作为 `MechanicHint`，走时间轴 lead 窗口提前播。
- `# 读条ID XXXX 去背后`：作为读条应对方案，Boss 开始读条 `XXXX` 时即时播。
- `# 结算ID XXXX 分摊`：作为结算应对方案，`Ability XXXX` 到达时即时播。

运行时变化：

- `ObserveStartsUsing` 新增 `ProcessStartsUsingResponseTts`。
- 读条应对方案会在 Framework 读条轮询发现 Boss 正在读对应技能时播报。
- `ProcessTimelineTts` 不再聚合播报 `ActionResponses`，避免多分支机制在 lead 窗口里猜错。
- 纯 `MechanicHint` 和技能名仍按 lead 窗口播报。

### 3. 倒计时条过滤

需求：倒计时条不要显示 `读条IDxxx` / `结算idxxx`。

处理：

- 放宽 tagged response 正则，兼容大小写、无空格、`;` / `；` 分隔。
- `ParseMechanicHint` 增加兜底：残留 `读条ID` / `结算ID` 的文本不作为 `MechanicHint`。
- `RemoveTaggedResponseSegments` 过滤 tagged response 后只保留真正的纯文本提示。

### 4. SystemLogMessage 未同步排查

用户反馈：

```txt
[3:38]距兵器试验场被封锁还有15秒。
```

同时插件日志显示：

```txt
03:38:01 加载 DalamudACT v0.15.2.53
```

判断：

- 克吕提俄斯老 3 时间轴条目存在：

```txt
3000.0 "--sync--" SystemLogMessage { id: "7DC", param1: "1585" } window 3000,0
```

- `param1=1585` 对应兵器试验场。
- 最可疑原因是插件刚加载后 3 秒冷却：`startedAtUtc == null` 且 `now - timelineLoadedAtUtc < 3s` 时直接忽略 SystemLogMessage，用于防聊天历史回放误同步。
- 本轮曾开始做 `[3:38]` 前缀清理，但用户明确要求“先不改代码，先发布”，最终 `0.15.2.54` 中没有提交这个冷却放行修复。

后续建议：

- 保留冷却对 reset / 杂项系统消息的保护。
- 允许 `7DC` 封锁预警在冷却期通过，或仅过滤聊天历史回放而不丢实时封锁消息。
- 同时保留 `NormalizeSystemLogMessage` 去掉 `[H:mm]` / `[HH:mm:ss]` 前缀。

## 其他改动

### 简易设置入口

文件：`DalamudACT/UI/Windows/SettingsWindow.QuickSettings.cs`

- 简易设置标题行新增 `战斗流水` 按钮。
- 位置在 `完整设置 ->` 左侧。
- 点击调用现有 `openCombatTimelineWindow()`。

### TTS 纠偏

文件：

- `DalamudACT/Configuration/PluginConfiguration.cs`
- `DalamudACT/Configuration/PluginConfiguration.Reset.cs`

新增默认纠偏：

```txt
对地 -> 对帝
```

已加入：

- 初始默认列表。
- `EnsureTimelineTtsCorrections()` 自动补默认。
- 重置配置列表。

### TTS 最终文本 2 秒去重

本轮把记录补进：`md/2026-05-29-timeline-draft-tts-handoff.md`。

相关代码已经存在于 `TimelineService.cs`：

- `TimelineTtsDuplicateSuppressSeconds = 2d`
- `lastTimelineTtsTextUtc`
- `TrySendDailyRoutinesTts(...)`
- `TrySendPreparedDailyRoutinesTts(...)`

作用：相同最终 TTS 文本 2 秒内只发送一次，降低 DailyRoutines / EdgeTTS 重复并发播报风险。

## 发布

版本：`0.15.2.54`

发布流程按 `md/RELEASE-RUNBOOK.md` 执行：

- 更新 `DalamudACT/DalamudACT.csproj`
- 更新 `DalamudACT/DalamudACT.json`
- 更新 `Data/DalamudACT.json`
- 更新 `repo.json`
- 更新 `md/RELEASE-NOTES.md`
- 执行 `dotnet build --no-restore`
- 显式暂存目标文件，未使用 `git add .`
- 使用无 `v` 前缀 annotated tag：`0.15.2.54`

发布结果：

```txt
Commit: 8c4a887 chore: release 0.15.2.54
Tag: 0.15.2.54
Release: https://github.com/anmili2022/DalamudACT/releases/tag/0.15.2.54
Asset: DalamudACT.zip
Size: 619082 bytes
SHA256: cbb69aba9aa8802137e0e297138b99c08be8e1ab51c5c626e6a6bd1bb10f52c4
Workflow: success
```

## 未提交 / 不应提交文件

发布后仍保持未跟踪：

```txt
1.txt
tools/CactbotTimelineExtractor/test_output.txt
tools/CactbotTimelineExtractor/test_output2.txt
```

## 后续优先事项

1. 修复 SystemLogMessage 加载后 3 秒冷却误吞实时 `7DC` 封锁提示。
2. 如果继续完善时间轴事件支持，优先评估 `HeadMarker` / `Tether`，但观察层需要 Hook，存在稳定性风险。
3. 对 `读条ID` / `结算ID` 解析增加小型单元测试或解析验证工具，避免注释格式变更再次回归。
