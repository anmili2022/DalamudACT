# 2026-05-16 DoT / ACT 对账交接

## 现场快照

- 工作目录：`E:\git\DalamudACT`
- 分支：`main`
- 当前 HEAD：`b7602c1`（本轮未提交）
- 当前工作区仍然是脏的，请接手前先执行：

```powershell
git status --short
```

截至本次收工，工作区大致包含：

```text
 M DalamudACT/DalamudACT.csproj
 M DalamudACT/DalamudACT.json
 M DalamudACT/Plugin/ACT.cs
 M DalamudACT/Stats/LocalStatsService.cs
 M DalamudACT/Stats/PlayerDotCatalog.cs
 M Data/DalamudACT.json
 M HANDOVER.md
 M tools/DotReconcile/Program.cs
?? 1.txt
?? md/2026-05-16-dot-handoff.md
```

注意：

- `1.txt` 是既有未跟踪文件，不要误删。
- 不要 reset / checkout 覆盖已有用户改动。
- 本轮最后确认构建通过：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
dotnet build tools\DotReconcile\DotReconcile.csproj
```

结果均为：

```text
0 warnings / 0 errors
```

当前插件产物：

```text
E:\git\DalamudACT\output\DalamudACT.dll
```

最近一次确认时间：

```text
2026-05-16 04:06:23
```

## 本轮目标

用户连续导出多份历史记录，要求把插件侧 `dotDamage-*` 与 ACT 的 `24|DoT` 对账。重点关注：

- 骑士 / 绝枪 DoT 偏低；
- 学者 DoT 偏高；
- 黑魔部分场次偏低或偏高；
- 贤者 `0xA38` 偏高；
- 机工 `0x35C / 0x35D` 口径；
- ACT `status=0` 时哪些是可信归属，哪些是可疑归属。

本轮主要完成：

1. 增强 `tools/DotReconcile`，增加状态窗口和窗口一致性检查；
2. 插件侧增加聚焦 `DOT诊断：`；
3. 修正 active DoT 因状态短暂不可见而提前清理的问题；
4. 移除机工 `0x35C 武装解除` 作为 DoT 的误判。

## DotReconcile 已完成增强

文件：

```text
tools/DotReconcile/Program.cs
```

### 1. 修复 `0xE0000000` 空源归类

现在：

```text
sourceId == 0
或 sourceId == 0xE0000000
且 sourceName 为空
```

会被归入：

```text
MissingSourceHostileDotLines
MissingSourceHostileDotDamage
```

不再误算成已归属玩家 DoT。

### 2. 新增 `--status-windows`

功能：

- 扫描战斗窗口内的 `26|` 状态应用；
- 输出当前显示玩家对 hostile 目标的状态摘要；
- JSON 输出 `statusWindows`；
- 用于解释 ACT `24|DoT` 里大量 `status=0` 的归属。

### 3. 新增 DoT 与状态窗口一致性检查

终端会输出：

```text
=== ACT DoT 与已知 DoT 状态窗口一致性检查 ===
```

结果状态：

- `OK`：ACT 有 DoT，且找到该职业已知 DoT 状态窗口或非零已知 status；
- `WARN`：ACT 有 DoT，但没有找到该职业已知 DoT 状态窗口，也没有非零已知 status，应视为可疑归属；
- `PLUGIN`：插件有 DoT，但 ACT 已归属为 0；
- `INFO`：无相关 DoT。

JSON 输出：

```json
"dotWindowConsistency": []
```

### 4. 新增 `--csv-windowcheck-out`

新增参数：

```powershell
--csv-windowcheck-out <path>
```

输出字段：

```text
Name
Job
ActorIdHex
PluginDotDamage
ActAttributedDotDamage
State
ZeroStatusDamage
ZeroStatusEventCount
KnownWindows
KnownActStatuses
Message
```

常用命令模板：

```powershell
dotnet run --project tools\DotReconcile\DotReconcile.csproj -- --history "C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT\history-records.json" --act-log-dir "D:\ff14act\FFXIVLogs" --latest --top-status 10 --status-windows --json-out output\dotreconcile-next.json --csv-out output\dotreconcile-next.csv --csv-status-out output\dotreconcile-next-status.csv --csv-windowcheck-out output\dotreconcile-next-windowcheck.csv
```

### 5. 机工 `0x35C` 修正

在 `KnownDotStatusIdsByJob["mch"]` 中移除了 `0x35C`。

现在 MCH 已知 DoT 状态只保留：

```text
0x74A
0x7E3
```

原因：

- 新场次里 `0x35C 武装解除` 出现在机工状态窗口；
- 但当前 100 级环境中它不应作为普通 DoT 依据；
- `0x35D 野火` 仍默认排除，因为插件 `dotDamage-*` 当前不按普通 DoT 口径计入野火。

## 插件侧已完成改动

### 1. 聚焦 `DOT诊断：` 扩展

文件：

```text
DalamudACT/Stats/LocalStatsService.cs
```

已加入聚焦诊断目标：

#### 骑士

```text
action 23
status 0xF8 厄运流转
```

#### 绝枪

```text
action 16153 / 16159
status 0x72D / 0x72E
```

#### 学者

```text
action 16540 / 29233 / 37012
status 0x767 / 0x7F7 / 0xC11 / 0xF2B
```

#### 黑魔

```text
action 36986 / 36987
status 0xF1F / 0xF20
```

诊断日志会输出类似：

```text
DOT诊断：记录挂载候选...
DOT诊断：挂载候选即时状态确认...
DOT诊断：记录伤害种子...
DOT诊断：激活状态...
DOT诊断：刷新活跃状态...
DOT诊断：补算Tick...
DOT诊断：清理活跃状态...
DOT诊断：状态存在但未能解析玩家来源...
DOT诊断：刷新估算伤害...
```

注意：这些日志只在 `LogHelper.EnableDebugLog == true` 时输出。

### 2. active DoT 生命周期修正

文件：

```text
DalamudACT/Stats/LocalStatsService.cs
```

旧逻辑：

```text
active DoT 超过 1 秒没有再次在目标状态列表中看到，就清理。
```

问题：

- 多人本 / boss debuff 很多时，客户端目标状态列表可能短暂看不到某个 DoT；
- 插件会误以为状态消失，提前停止补算 tick；
- `恩欧歼殛战` 中骑士 `0xF8 厄运流转` 插件 `144,600` vs ACT `988,005`，非常像这个问题。

新逻辑：

- 新增 `DecayActivePlayerDotStatesLocked(nowUtc)`；
- active DoT 如果暂时没有再次读到状态，就按上次 `RemainingTimeSeconds` 自然倒计时；
- 不再因为 1 秒没看到状态就直接清理。

现在 active DoT 主要在这些情况下清理：

```text
1. 剩余时间归零；
2. 目标对象消失；
3. 目标不可选中；
4. 检查异常。
```

注意：该修正还没有被现场验证。后续历史记录没有确认游戏重载到新 DLL。

### 3. 机工 `0x35C` 从插件 DoT 表移除

文件：

```text
DalamudACT/Stats/PlayerDotCatalog.cs
```

已移除：

```csharp
Skill("武装解除", [2887], [860])
```

保留：

```csharp
Skill("毒菌冲击", [16499, 29406], [1866, 2019])
```

原因：

- 当前 100 级环境下 `0x35C 武装解除` 不应作为普通 DoT；
- 该状态会误导 `DotReconcile` 把机工 ACT `status=0` 归属标记为 OK；
- 修正后机工这类情况会变成 WARN，更符合实际。

## 已分析场次

### 1. 阿卡狄亚登天斗技场 重量级3

结果：

```text
插件玩家合计：5,007,600
ACT 已归属玩家合计：5,020,041
差异：-0.25%
ACT 未归属：153,000 / 6 行
```

结论：

- 总量非常接近；
- 逐玩家差异较大；
- PLD/GNB 明显偏低；
- 已加 PLD/GNB 诊断，但仍需新场次验证。

### 2. 阿卡狄亚登天斗技场 重量级1

相关输出：

```text
output\dotreconcile-2026-05-16-heavy1-windowcheck2.json
output\dotreconcile-2026-05-16-heavy1-windowcheck2.csv
output\dotreconcile-2026-05-16-heavy1-windowcheck2-status.csv
output\dotreconcile-2026-05-16-heavy1-windowcheck2-windowcheck.csv
```

结果：

```text
插件玩家合计：4,438,500
ACT 已归属玩家合计：4,345,904
差异：+2.13%
ACT 未归属：456,776 / 13 行
source 缺失：456,776 / 13 行
```

WARN 行：

```text
陈衍烛 | 蝰蛇剑士 | 插件 0 | ACT 371,853 | status=0 / 无已知 DoT 状态窗口
止境的加护 | 武僧 | 插件 0 | ACT 313,194 | status=0 / 无已知 DoT 状态窗口
Isaiah | 战士 | 插件 0 | ACT 184,554 | status=0 / 无已知 DoT 状态窗口
```

真实待查：

- 学者偏高；
- 黑魔一人偏低；
- 暗黑中等偏低。

### 3. 恩欧歼殛战

相关输出：

```text
output\dotreconcile-latest-new.json
output\dotreconcile-latest-new.csv
output\dotreconcile-latest-new-status.csv
output\dotreconcile-latest-new-windowcheck.csv
```

结果：

```text
插件玩家合计：4,791,900
ACT 已归属：5,340,573
差异：-10.27%
ACT 未归属：69,671 / 12 行
```

关键结论：

#### 骑士 `艾絲珀瑞亞`

```text
插件：144,600
ACT：988,005
差异：-85.36%
已知状态窗口：0xF8 厄运流转 ×16
```

这是 active DoT 提前清理修正的主要依据。

#### 黑魔 `维尔海米娜`

```text
插件：1,289,800
ACT：1,242,646
差异：+3.79%
```

这场基本健康。

#### 学者 `怪偶猫`

```text
插件：2,280,900
ACT：1,455,591
差异：+56.70%
```

仍需下一轮开 `DOT诊断：` 查 tick 数与估算。

#### 机工

当时因 `0x35C` 被误判为 OK，后续已修正工具与插件表。

### 4. 阿卡狄亚登天斗技场 重量级2

以修正后的 `new2b` 输出为准：

```text
output\dotreconcile-latest-new2b.json
output\dotreconcile-latest-new2b.csv
output\dotreconcile-latest-new2b-status.csv
output\dotreconcile-latest-new2b-windowcheck.csv
```

结果：

```text
插件玩家合计：4,198,200
ACT 已归属：4,434,552
差异：-5.33%
ACT 未归属：267,623 / 13 行
```

windowcheck：

```text
[WARN] 四宮輝夜 | 机工士 | 插件 0 | ACT 429,849 | status=0 429,849 / 15 行
[WARN] 慵懒的米饭 | 战士 | 插件 0 | ACT 408,624 | status=0 408,624 / 19 行
[WARN] 狮子座的小艾 | 蝰蛇剑士 | 插件 0 | ACT 106,925 | status=0 106,925 / 3 行
[OK] 酸嘢 | 白魔法师 | 插件 1,471,300 | ACT 1,309,759 | 0x74F 天辉×23
[OK] W维什戴尔 | 黑魔法师 | 插件 937,400 | ACT 796,432 | 0xF1F×13 / 0xF20×4
[OK] 狮子兽吃肉肉 | 黑魔法师 | 插件 790,500 | ACT 621,839 | 0xF1F×11
[OK] 毒苹果 | 贤者 | 插件 935,700 | ACT 615,718 | 0xA38×16
[OK] 一叶落知秋 | 暗黑骑士 | 插件 63,300 | ACT 145,406 | ACT 非零 0x2ED
```

结论：

- 这场没有骑士 / 学者，不能验证 active DoT 修正和学者问题；
- 机工 `0x35C` 修正后变为 WARN，符合预期；
- 贤者 `0xA38` 偏高 `+51.97%`，后续可加入重点排查；
- 暗黑 `0x2ED` 偏低，属于 source-owned / 地面 DoT 特殊问题，可后续单独查。

## 重要限制：目前还没有验证到新 DLL

`dalamud.log` 中最近的 DalamudACT 加载记录仍是：

```text
2026-05-16 02:37:38 加载 DalamudACT.dll
```

没有看到 `03:45` 或 `04:06` 之后重新加载插件的记录。

因此，后续导出的场次很可能仍在运行旧内存代码，尚未验证：

- active DoT 自然倒计时修正；
- BLM/SCH/PLD/GNB 聚焦 `DOT诊断：` 扩展；
- 机工 `0x35C` 从插件 DoT 表移除。

下次验证前必须先重载插件，确认游戏使用：

```text
E:\git\DalamudACT\output\DalamudACT.dll
```

并确认 DLL 时间晚于：

```text
2026-05-16 04:06:23
```

## 调试日志状态

当前配置文件：

```text
C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT.json
```

仍为：

```json
"EnableDebugLog": false
```

所以之前没有任何 `DOT诊断：` 输出。

用户问过调试日志是否卡顿，结论：

- 有可能增加开销；
- 只打一两场验证问题不大；
- 不建议长期打开；
- 建议打完马上关闭。

推荐验证流程：

1. 重载最新 DLL；
2. 开启调试日志；
3. 打一场目标战斗；
4. 导出历史记录；
5. 立刻关闭调试日志。

查日志命令：

```powershell
rg -n "DOT诊断|DOT璇婃柇" C:\Users\Administrator\AppData\Roaming\XIVLauncherCN -g "*.log" -g "*.txt"
```

## 下次优先验证清单

1. 先确认插件已重载到最新 DLL。
2. 打开调试日志。
3. 优先打一场包含以下职业的战斗：
   - 骑士：验证 `0xF8 厄运流转` 是否因 active DoT 修正明显改善；
   - 学者：查 `0x767 / 0xF2B` 是否 tick 过多或估算偏高；
   - 贤者：查 `0xA38` 为什么插件比 ACT 已归属高约 52%；
   - 黑魔：确认 `0xF1F / 0xF20` 多目标下是否稳定；
   - 绝枪：如有，顺带验证 `0x72D / 0x72E`。
4. 导出历史记录后跑：

```powershell
dotnet run --project tools\DotReconcile\DotReconcile.csproj -- --history "C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT\history-records.json" --act-log-dir "D:\ff14act\FFXIVLogs" --latest --top-status 10 --status-windows --json-out output\dotreconcile-next.json --csv-out output\dotreconcile-next.csv --csv-status-out output\dotreconcile-next-status.csv --csv-windowcheck-out output\dotreconcile-next-windowcheck.csv
```

5. 同时查 `DOT诊断：`：

```powershell
rg -n "DOT诊断|DOT璇婃柇" C:\Users\Administrator\AppData\Roaming\XIVLauncherCN -g "*.log" -g "*.txt"
```

如果仍没有 `DOT诊断：`：

- 先查 `EnableDebugLog` 是否为 `true`；
- 再查 `dalamud.log` 里是否有最新 DLL 的重新加载记录。

## 一句话总结

离线对账工具现在已经能较好地区分“真实 DoT 状态窗口”和“ACT status=0 可疑归属”；插件侧已修 active DoT 提前清理问题，并移除 MCH `0x35C` 非 DoT 误判。下一次不要继续盲改，先重载最新 DLL、短期开启调试日志，然后用包含骑士 / 学者 / 贤者 / 黑魔的战斗重新导出历史记录验证。

## 收工备注

- 用户确认当前问题不大，本轮先收工，下次再做现场验证。
- 本文档即为下次开工入口；若继续排查，请优先按“下次优先验证清单”执行，不要在未确认新 DLL 已加载前继续根据旧历史记录判断代码修复效果。
- 如果下一次只想快速恢复上下文，先看：
  1. `HANDOVER.md` 顶部的 `2026-05-16` 补充；
  2. 本文件的“重要限制：目前还没有验证到新 DLL”；
  3. 本文件的“下次优先验证清单”。

## 2026-05-16 晚间复核补充：重量级1 已形成完整对账链路

> 本节是后续继续排查时的最新口径，优先级高于上面早期“尚未验证新 DLL / 没有 DOT诊断”的旧备注。

用户在 19:44 导出了新的历史记录：

```text
C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT\history-records.json
```

history 内第 0 条是用户关心的：

```text
阿卡狄亚登天斗技场 重量级1
2026-05-16 19:17:28 +08:00 ~ 2026-05-16 19:24:14 +08:00
时长 06:46
```

注意：`--latest` 会选到后面的“神圣禁地深空天坑”，不是重量级1。对重量级1必须使用：

```powershell
--zone "重量级1"
```

### 最新完整对账命令

```powershell
dotnet run --project tools\DotReconcile\DotReconcile.csproj -- --history "C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT\history-records.json" --act-log-dir "D:\ff14act\FFXIVLogs" --zone "重量级1" --top-status 10 --status-windows --summary-out output\dotreconcile-heavy1-summary.csv --json-out output\dotreconcile-heavy1-v5.json --csv-out output\dotreconcile-heavy1-v5.csv --csv-status-out output\dotreconcile-heavy1-v5-status.csv --csv-windowcheck-out output\dotreconcile-heavy1-v5-windowcheck.csv --csv-known-dot-out output\dotreconcile-heavy1-known-dot.csv --csv-dotdiagnostic-out output\dotreconcile-heavy1-dotdiagnostic.csv
```

### 新增 DotReconcile 参数

本轮后续又给 `tools/DotReconcile` 增加了：

- `--summary-out <path>`
  - 导出整场短汇总；
  - `.json` 扩展名写 JSON，否则写单行 CSV。
- `--csv-known-dot-out <path>`
  - 导出职业已知 DoT 状态专项表；
  - 用于区分 `ACT非零已知status` 与 `状态窗口+ACT status=0`。
- `--dalamud-log <path>`
  - 指定 Dalamud 日志；
  - 不传时默认扫描 `C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\dalamud*.log`。
- `--csv-dotdiagnostic-out <path>`
  - 读取 `DOT诊断：补算Tick`；
  - 输出插件内部补算 tick 与 ACT 证据合并后的总表。

README 的“离线 DoT 对账工具”小节已同步更新，后续查用法可直接看 README。

### 最新输出文件

```text
output\dotreconcile-heavy1-summary.csv
output\dotreconcile-heavy1-v5.json
output\dotreconcile-heavy1-v5.csv
output\dotreconcile-heavy1-v5-status.csv
output\dotreconcile-heavy1-v5-windowcheck.csv
output\dotreconcile-heavy1-known-dot.csv
output\dotreconcile-heavy1-dotdiagnostic.csv
```

### 重量级1 总量结论

`summary.csv` / 终端合计：

```text
插件 DoT 合计：4,396,800
ACT 已归属 hostile DoT：3,955,409
ACT 未归属 hostile DoT：640,795
ACT hostile 总量：4,596,204

插件 vs ACT 已归属：+11.16%
插件 vs ACT hostile 总量：-4.34%
ACT 未归属 hostile 占比：13.94%
```

结论：

```text
整场插件不是比 ACT 高很多。
只看 ACT 已归属会显示插件高 11.16%，但把 ACT 未归属 hostile DoT 加上后，插件反而比 ACT hostile 总量低约 4.34%。
```

### DOT诊断与插件历史已对齐

`output\dotreconcile-heavy1-dotdiagnostic.csv` 显示：

```text
Tamomo 天辉：DOT诊断 1,325,024 vs 插件历史 1,325,000，0.00%
初戀日記 天辉：DOT诊断 1,249,769 vs 插件历史 1,249,800，0.00%
四宮輝夜 高闪雷：DOT诊断 1,071,792 vs 插件历史 1,071,800，0.00%
弥叶薰 厄运流转：DOT诊断 161,258 vs 插件历史 161,300，-0.03%
彌央 0xA92：DOT诊断 142,920 vs 插件历史 142,900，+0.01%
无名策 音速破 + 弓形冲波：DOT诊断 446,026 vs 插件历史 446,000，+0.01%
```

结论：

```text
插件历史里的 DoT 基本就是 DOT诊断补算 tick 的合计；导出/UI 汇总没有乱加伤害。
```

### 职业专项结论

`known-dot.csv` / `dotdiagnostic.csv` 共同结论：

- 召唤 `0xA92 螺旋气流`
  - DOT诊断：`142,920`
  - ACT 非零 `0xA92`：`138,026`
  - 差异：`+3.55%`
  - 结论：`0xA92 / 星极超流 / Slipstream` 修复成功，局部对账基本对上。
- 白魔 `0x74F 天辉`
  - 两个白魔各有 `0x74F` 状态窗口 `17` 次；
  - 但 ACT 只给 `status=0`：
    - Tamomo：`66,166 / 3 行`
    - 初戀日記：`79,254 / 6 行`
  - 结论：白魔“插件比 ACT 高很多”主要是 ACT 个人归属缺失，不是插件虚高。
- 黑魔 `0xF1F 高闪雷`
  - ACT 有 `0xF1F` 状态窗口；
  - 但 ACT DoT tick 全部是 `status=0`；
  - 不能把 ACT 已归属总量直接当成 `0xF1F` 实值。
- 骑士 `0xF8 厄运流转`
  - DOT诊断 `161,258` 与插件历史 `161,300` 对上；
  - ACT 有状态窗口，但 DoT tick 全是 `status=0`；
  - 不能把 ACT `897,183` 直接当作骑士厄运流转。
- 绝枪 `0x72D / 0x72E`
  - DOT诊断合计 `446,026` 与插件历史 `446,000` 对上；
  - ACT 有状态窗口，但 tick 全是 `status=0`。

### 后续建议

1. 当前不建议为了贴 ACT 已归属个人数而削弱插件 DoT。
2. 后续继续验证时优先看：
   - `summary.csv`：判断整场总量；
   - `known-dot.csv`：判断 ACT 是否有非零已知状态；
   - `dotdiagnostic.csv`：判断插件内部补算 tick 是否和历史一致。
3. 如要继续找缺口，优先打一场包含暗黑的记录，验证 `0x2ED 腐秽大地`。
4. `DOT诊断` 日志量很大，排查结束后建议关闭调试日志。

## 2026-05-16 继续补充：DotReconcile 导出逻辑已拆分

本次继续没有改 DoT 算法，只做 `tools/DotReconcile/Program.cs` 的维护性清理：

- 原本很长的 `WriteExports(...)` 已拆成一组单一职责方法：
  - `BuildExportContext(...)`
  - `WriteJsonExport(...)`
  - `WriteSummaryExport(...)`
  - `WriteMainCsvExport(...)`
  - `WriteStatusCsvExport(...)`
  - `WriteWindowCheckCsvExport(...)`
  - `WriteKnownDotCsvExport(...)`
  - `WriteDotDiagnosticCsvExport(...)`
- 新增内部 `ExportContext`，集中保存导出共用的合计、差异百分比、生成时间和 summary 结论。
- 这次拆分目标是降低后续维护成本，不改变任何输出字段或对账口径。

已做验证：

```powershell
dotnet build tools\DotReconcile\DotReconcile.csproj
dotnet build DalamudACT.sln
```

结果均为 `0 warnings / 0 errors`。

同时用重量级1记录复跑验证导出：

```powershell
dotnet run --project tools\DotReconcile\DotReconcile.csproj -- --history "C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT\history-records.json" --act-log-dir "D:\ff14act\FFXIVLogs" --zone "重量级1" --top-status 10 --status-windows --summary-out output\dotreconcile-refactor-verify\summary.csv --json-out output\dotreconcile-refactor-verify\full.json --csv-out output\dotreconcile-refactor-verify\main.csv --csv-status-out output\dotreconcile-refactor-verify\status.csv --csv-windowcheck-out output\dotreconcile-refactor-verify\windowcheck.csv --csv-known-dot-out output\dotreconcile-refactor-verify\known-dot.csv --csv-dotdiagnostic-out output\dotreconcile-refactor-verify\dotdiagnostic.csv
```

核对结果：

- `main.csv`、`status.csv`、`windowcheck.csv`、`known-dot.csv`、`dotdiagnostic.csv` 与拆分前对应输出完全一致；
- `summary.csv` 除 `GeneratedAtUtc` 外一致；
- `json` 在使用拆分前相同参数时，除 `generatedAtUtc` 外一致。

后续如果继续开发，优先做真实战斗验证或更细的工具单元化；不需要再优先拆 `WriteExports(...)`。

## 2026-05-17 收工补充：新 history 复核，学者闭环，不按 ACT status=0 调整

> 本节是 2026-05-17 下午用户新导出历史记录后的最新收工结论。  
> 重点：本轮没有继续改代码，只用现有 `DotReconcile` 口径复核新 history，并确认“当前略偏低是可接受的保守口径，不要为了贴 ACT `status=0` 个人已归属数调算法”。

### 新导出的 history

用户新导出：

```text
C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT\history-records.json
```

本地看到文件时间约：

```text
2026-05-17 15:51
```

### 本轮运行命令

```powershell
dotnet run --project tools\DotReconcile\DotReconcile.csproj -- --history "C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT\history-records.json" --act-log-dir "D:\ff14act\FFXIVLogs" --latest --top-status 10 --status-windows --summary-out output\reconcile-new-summary.csv --json-out output\reconcile-new.json --csv-out output\reconcile-new.csv --csv-status-out output\reconcile-new-status.csv --csv-windowcheck-out output\reconcile-new-windowcheck.csv --csv-known-dot-out output\reconcile-new-known-dot.csv --csv-dotdiagnostic-out output\reconcile-new-dotdiagnostic.csv
```

### 输出文件

```text
output\reconcile-new-summary.csv
output\reconcile-new.csv
output\reconcile-new-status.csv
output\reconcile-new-windowcheck.csv
output\reconcile-new-known-dot.csv
output\reconcile-new-dotdiagnostic.csv
output\reconcile-new.json
```

### 选中的战斗

`--latest` 选中：

```text
恩欧歼灭战
2026-05-17 15:43:27 +08:00 ~ 2026-05-17 15:51:06 +08:00
时长：07:38
```

ACT 实际命中日志：

```text
D:\ff14act\FFXIVLogs\Network_30109_20260517.log
```

### 整场总量结论

`output\reconcile-new-summary.csv`：

```text
插件 DoT 合计：2,947,000
ACT 已归属 hostile DoT：3,121,278
ACT 未归属 hostile DoT：0
ACT hostile 总量：3,121,278
插件 vs ACT 已归属：-5.58%
插件 vs ACT hostile 总量：-5.58%
```

结论：

```text
插件低于 ACT hostile 总量（-5.58%）；未发现未归属 hostile DoT。
```

这次整场插件不是虚高，而是略低。这个结果符合当前 DoT 口径的目标：  

```text
保守统计，优先避免虚高。
```

因此本轮不建议为了贴近 ACT 的个人已归属数而调高插件 DoT。

### 学者复核结论

本场有学者：

```text
四宮輝夜 | 学者 | actorId 0x103608CB
```

主表：

```text
插件 DoT：1,407,800
ACT 已归属：419,390
差异：+235.68%
TopStatuses：0x0:419390(21)
```

如果只看这一行会显示红字，但 `known-dot` 和 `dotdiagnostic` 已经把原因拆清楚。

#### known-dot 结果

学者 `0x767 蛊毒法`：

```text
插件总 DoT：1,407,800
ACT 已归属：419,390
ACT 非零已知 status 伤害：0
ACT status=0 伤害：419,390
ACT status=0 事件：21
状态窗口应用次数：14
状态窗口目标数：5
证据：状态窗口 + ACT status=0
```

状态窗口目标：

```text
恩欧 0x40004FB3 x10
虚无巨影 0x400050B5 x1
虚无之影 0x400050AD x1
虚无之影 0x400050AF x1
光之征兆 0x400050FF x1
```

学者 `0xF2B 埋伏之毒`：

```text
插件总 DoT：1,407,800
ACT 已归属：419,390
ACT 非零已知 status 伤害：0
ACT status=0 伤害：419,390
ACT status=0 事件：21
状态窗口应用次数：4
状态窗口目标数：1
目标：恩欧 0x40004FB3 x4
证据：状态窗口 + ACT status=0
```

解释：

```text
ACT 能看到学者 DoT 状态窗口，但 24|DoT tick 没给出 0x767 / 0xF2B，而是全部落在 status=0。
因此 ACT 已归属个人数 419,390 不能直接当作学者 DoT 真值。
```

#### DOT诊断结果

`0xF2B 埋伏之毒`：

```text
DOT诊断 tick 数：20
DOT诊断伤害：355,303
暴击 tick：7
目标：恩欧
```

`0x767 蛊毒法`：

```text
DOT诊断 tick 数：105
DOT诊断伤害：1,052,472
暴击 tick：25
目标：恩欧 / 虚无之影 / 虚无之影 / 虚无巨影
```

学者合计：

```text
DOT诊断总伤害：1,407,775
插件历史 DoT：1,407,800
差异：0.00%
```

结论：

```text
学者插件历史里的 DoT 基本就是 DOT诊断补算 tick 的合计，导出/UI 没有乱加。
学者红字来自 ACT status=0 个人归属缺口，不应该按 ACT 已归属 419,390 去削插件 DoT。
```

### 贤者状态

本场没有贤者，无法新增贤者样本。

当前仍沿用上一轮 `0xA38 均衡注药III` 结论：

```text
贤者 DOT诊断 ≈ 插件历史；
ACT 有 0xA38 状态窗口；
ACT 个人已归属主要/全部为 status=0；
ACT 已归属不适合作为贤者 DoT 真值。
```

如果后续需要补贤者证据，需要打一场包含贤者的记录再导出 history。  
但目前不建议为了贴 ACT 已归属个人数调整贤者算法。

### 暗黑可作为后续真实对账样本

本场暗黑：

```text
丨浮屠c丨 | 暗黑骑士 | 0x2ED 腐秽大地
```

结果：

```text
插件历史：72,100
DOT诊断：72,140
ACT 非零 0x2ED：84,028
差异：约 -14.15%
```

这是本场少数 ACT 给出非零已知 status 的可对账样本。  
当前只建议记录观察，不急着改；如果后续多场 `0x2ED` 稳定偏低，再针对暗黑单独查。

### 骑士 / 白魔 / 武士等红字不要直接按 ACT 调

本场还看到：

```text
骑士：插件 97,800 vs ACT 已归属 1,315,484
白魔：插件 843,700 vs ACT 已归属 255,594
武士：插件 525,600 vs ACT 已归属 786,686
```

但这些都属于：

```text
ACT 有状态窗口；
ACT 非零已知 status = 0；
ACT 24|DoT tick 主要/全部为 status=0。
```

尤其骑士：

```text
DOT诊断 97,758 vs 插件历史 97,800，差异 -0.04%；
ACT 0xF8 非零 status 为 0；
ACT status=0 数值高达 1,315,484。
```

因此不能把 ACT `1,315,484` 当作骑士 `0xF8 厄运流转` 真值，更不能据此把插件 DoT 放大。

### 本轮最终收工判断

当前判断规则继续保持：

1. 如果 `DOT诊断 ≈ 插件历史`，且 ACT 只有 `status=0`，则不按 ACT 已归属硬调。
2. 如果 ACT 有明确非零已知 status，例如暗黑 `0x2ED`、召唤 `0xA92`，可以作为局部对账参考。
3. 只有当同一明确 status 多场稳定大幅偏离时，才考虑针对具体技能查 potency、tick 数、目标可选中、首尾 tick、暴击模拟等逻辑。
4. 当前整场插件 DoT 比 ACT hostile 总量低约 `5.58%`，属于可接受的保守偏低，不需要往 ACT `status=0` 个人数值方向调。

一句话总结：

```text
本轮新 history 再次证明：学者内部闭环，整场插件略低但正常；贤者暂未新增样本但沿用已闭环结论。当前不要为了贴 ACT status=0 已归属个人数去调整学者、贤者或其他 DoT。
```

### 当前现场提醒

- 本轮只跑对账和补文档，没有继续改代码。
- 当前工作区仍然是脏的，请下次接手先看：

```powershell
git status --short
```

- `1.txt` 仍然是既有未跟踪文件，不要误删。
- 不要执行 `git reset --hard` 或覆盖用户现场改动。

