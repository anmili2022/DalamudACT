# 2026-05-14 新样本 DoT 对账续查交接

## 本轮目标

这轮没有继续改代码，而是先拿用户**当前机器上最新导出的 history + ACT 网络日志**做一轮新样本对账，确认在上一轮“治疗职业 DoT 口径收紧”之后，现状到底变成了什么。

重点是：

1. 复核 `白魔 / 贤者 / 学者 / 占星术士` 收紧后的实际表现；
2. 顺手确认旧问题职业是否仍然存在；
3. 把“下一刀最该改什么”写清楚，方便直接接手。

---

## 本轮实际使用的文件

### 1）插件历史记录

- `C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT\history-records.json`
- 最后写入时间：`2026-05-14 02:04:40 +08:00`

### 2）ACT 网络日志

- `D:\ff14act\FFXIVLogs\Network_30109_20260513.log`

### 3）本轮实际核对到的唯一一场战斗

当前 `history-records.json` 里只有 `1` 条记录，对应：

- 副本：`缇坦妮雅歼灭战`
- 开始：`2026-05-14T01:58:15.2039689+08:00`
- 结束：`2026-05-14T02:04:28.6689584+08:00`
- 时长：`06:13`

---

## 这轮对账口径（非常重要）

这轮为了避免再次把治疗、HoT、自身状态或奇怪的 `24` 行混进来，ACT 侧不是简单粗暴地把所有 `24|...|DoT|...` 都加总，而是额外加了一层过滤：

### ACT 统计口径

只统计同时满足以下条件的日志行：

1. 行类型是 `24|`
2. 事件种类是 `DoT`
3. 时间落在这场战斗窗口内
4. **目标 actorId 以 `4` 开头**，也就是 hostile NPC

也就是说，本轮更接近：

> **ACT hostile-only DoT**

而不是“ACT 里所有名字叫 DoT 的 24 行全算进去”。

### 为什么要这么做

因为这轮日志里我确实看到了：

- 贤者 `DoT` 打到队友：
  - 例如 `2026-05-14T02:00:22.517+08:00`
  - `24|...|10569801|満江紅|DoT|...|10978777|在爱锈蚀之前|`
- 武僧也出现了 target = 自己名字的 `DoT` 行

这些都**不适合**直接拿来和插件里的“玩家对敌 DoT 总伤”硬对。

因此：

- 如果后续有人继续对账，**优先沿用这轮 hostile-only 过滤口径**
- 不然很容易把非玩家对敌 DoT 的 24 行误算进去

---

## 插件 vs ACT hostile-only DoT 对账结果

本轮重点玩家如下：

### 1）白魔法师 `阳介`

- 插件 `dotDamage-*`：`6.99万`
- ACT hostile-only DoT：`23170`
- 结论：**明显虚高**

关键日志：

- 白魔本场实际施放：
  - `天辉` actionId = `4094`
- 对应状态：
  - `74F` = `1871`

当前代码现状：

- 后续校正：这里最初把 `4094` 记成了“缺失的旧 actionId”，后续已确认它其实是**十六进制显示**，对应十进制 `16532`
- `PlayerDotCatalog.cs` 当前已有 `天辉` 的 actionId `16532`，因此不能再把这条结论理解成“catalog 缺 `4094`”
- 白魔这场之所以仍然是 `69900 vs 23170` 这种明显虚高，更像是：
  - active DoT state 后续没有被更准确的信息充分升级
  - 直伤样本 / recent action 与状态绑定链路仍有串用
  - 状态生命周期或补 tick 逻辑仍然偏长

因此白魔下一步建议改为：

1. 先换上包含 `LocalStatsService.cs` 最小修正的新 DLL 复测
2. 如果仍明显虚高，再继续排查：
   - 直伤样本被误当 tick
   - 重复补 tick
   - 状态生命周期拖长

---

### 2）贤者 `在爱锈蚀之前`

- 插件 `dotDamage-*`：`0`
- ACT hostile-only DoT：`21494`
- 结论：**确定漏记**

关键日志：

- 本场实际施放：
  - `均衡注药II` actionId = `5EF4`
- 对应锚点直伤：
  - `注药II` actionId = `5EF2`
- DoT 状态：
  - `A37` = `2615`

当前代码现状：

- 后续校正：这里最初把 `5EF4 / 5EF2` 记成了“缺失的旧 actionId”，后续已确认它们其实是**十六进制显示**，对应十进制 `24308 / 24306`
- `PlayerDotCatalog.cs` 当前已有：
  - `均衡注药II` actionId：`24308`
  - 锚点 `注药II` actionId：`24306`
  - statusId：`2615`
  - `disableAverageFallback: true`

因此当前更可信的结论改为：

1. 状态 ID 本身是对的
2. catalog 也并不是简单缺这两个 actionId
3. 更像是 active DoT state 在后续状态轮询中没有被更准确的 `ActionId / SkillEntry` 充分升级
4. 再叠加 `disableAverageFallback: true`，一旦 skill 绑定链路失准，就更容易直接掉成 `0`

因此贤者下一步建议改为：

1. 先换上包含 `LocalStatsService.cs` 最小修正的新 DLL 复测
2. 重点确认：
   - `statusId = 2615` 是否能稳定绑定到 `均衡注药II`
   - 已有 active state 是否会被后续更准确状态更新为正确 `ActionId / SkillEntry`

---

### 3）黑魔法师 `丹凤吟`

- 插件 `dotDamage-*`：`5.02万`
- ACT hostile-only DoT：`133671`
- 结论：**明显少算**

关键日志：

- 本场实际施放：
  - `暴雷` actionId = `99` = `153`
- 对应状态：
  - `A3` = `163`

这两个其实都已经在当前 catalog 里。

所以这次黑魔问题**不是白名单漏条目**，而更像：

- 旧雷系状态刷新链仍有问题
- tick 生命周期或补算连续性仍有问题

这和上一轮的结论一致：

> `黑魔旧雷系 DoT 仍是待修项`

---

### 4）骑士 `理溏丁真`

- 插件 `dotDamage-*`：`1.04万`
- ACT hostile-only DoT：`59251`
- 结论：**明显少算**

关键日志：

- 本场实际施放：
  - `厄运流转` actionId = `17` = `23`
- 对应状态：
  - `F8` = `248`

这两个也都已经在 catalog 里。

因此骑士问题也不是“ID 缺失”，更像：

- 现有 potency / 估算支撑不足
- 或 active state 生命周期 / 补 tick 逻辑仍漏一段

---

### 5）绝枪战士 `左若童`

- 插件 `dotDamage-*`：`3.46万`
- ACT hostile-only DoT：`64322`
- 结论：**少算**

关键日志：

- `音速破` actionId = `3F19` = `16153`
- `弓形冲波` actionId = `3F1F` = `16159`
- 对应状态：
  - `72D` = `1837`
  - `72E` = `1838`

这些也都已经在 catalog 里。

所以绝枪这次和黑魔 / 骑士一样，更像：

- 不是白名单缺失
- 而是 DoT 生命周期 / 多目标 / 补算完整性问题

---

### 6）赤魔 / 舞者 / 武僧

- `爆炒奶油派`（赤魔）
  - 插件：`0`
  - ACT hostile-only：`0`
  - 本场正常

- `雷牢`（舞者）
  - 插件：`0`
  - ACT hostile-only：`0`
  - 本场正常

- `満江紅`（武僧）
  - 插件：`0`
  - ACT hostile-only：`0`
  - ACT 原始 `24|DoT|...` 里虽然能看到异常自目标行，但 hostile-only 过滤后应视为 `0`

---

## 这轮最重要的新发现

### A. 白魔 `天辉` 这里最初把十六进制显示误记成了缺失 actionId

本场实际 actionId：

- `4094`（十六进制） = `16532`（十进制）

当前 catalog 只有：

- `16532`

后续已确认：这里不是“catalog 漏了旧 actionId”，而是同一个 actionId 被分别用十六进制 / 十进制写了两次。

---

### B. 贤者 `均衡注药II / 注药II` 这里最初也把十六进制显示误记成了缺失 actionId

本场实际 actionId：

- `均衡注药II`：`5EF4`（十六进制） = `24308`（十进制）
- `注药II`：`5EF2`（十六进制） = `24306`（十进制）

当前 catalog 只有：

- `24308`
- `24306`

后续已确认：这里同样不是“catalog 缺 ID”，而更可能是状态绑定 / active state 更新链路问题；在贤者已禁用平均兜底的前提下，这类问题会更容易直接表现成 `0`。

---

### C. hostile-only 过滤非常有必要

这轮确认到：

- 有些 ACT `24|DoT|...` 并不是玩家对敌输出型 DoT
- 直接全量相加会把：
  - 队友目标
  - 自身目标
  - 其他异常 24 行
  
  一起算进去

因此后续再做对账时：

> **优先用 hostile-only DoT，而不是所有 24 DoT 生加总。**

---

## 这轮没有改代码

这次只做了：

- 新样本读取
- history / ACT 对账
- 结论归档

### 没有做的事

- 没有继续改 `PlayerDotCatalog.cs`
- 没有继续改 `LocalStatsService.cs`
- 没有重新 build

所以：

- 最近一次已验证构建，仍然沿用上一轮：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`

### 后续校正

后续复查已确认：

- `4094 / 5EF4 / 5EF2` 都应按**十六进制显示**理解，对应十进制分别是：
  - `16532`
  - `24308`
  - `24306`
- 当前 `PlayerDotCatalog.cs` 已经包含这些十进制 ID
- 因此接手优先级应从“先补 catalog”调整为“先实测包含 `LocalStatsService.cs` 最小修正的新 DLL”

---

## 下一位接手最推荐先做什么

按收益 / 确定性排序，建议：

### 第一优先级

先**不要**继续按 `4094 / 5EF4 / 5EF2` 去补 `DalamudACT/Stats/PlayerDotCatalog.cs`。

这些值后续已确认只是十六进制显示，当前 catalog 已有对应十进制 ID。

真正该优先做的是：

1. 换上当前工作区里包含 `LocalStatsService.cs` 最小修正的新 DLL
2. 重新 build：
   - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
3. 换新 `output\DalamudACT.dll`
4. 再打一场新样本
5. 继续优先对：
   - `白魔`
   - `贤者`

### 第二优先级

如果白魔 / 贤者复测后仍异常，优先沿着下面两条继续排：

1. `activePlayerDots` 已有 state 是否会在后续状态轮询中被更准确的 `ActionId / SkillEntry` 充分升级
2. 创建 DoT state 时是否稳定优先按 `statusId` 解析 skill，而不是过早依赖 recent action

### 第三优先级

如果白魔 / 贤者修正后仍有余力，再继续排：

1. `骑士`
2. `黑魔旧雷系`
3. `绝枪`

---

## 最后提醒

- 当前工作区仍然是脏的
- `1.txt` 依然**不要误删**
- 这轮新增的主要是文档结论，不是代码修复

