# 2026-05-12 ikegami 悬浮窗样式交接

## 这次会话的目标
本轮主要在做 `DalamudACT` 的 **ikegami 风格悬浮窗展示模式**，方向已经由用户反复确认：

- **必须是横向条带式 overlay**
- 不是竖向卡片列表
- 先用 HTML 预览把样式钉死，再回写到插件内的 ImGui 布局

---

## 用户到 2026-05-12 为止的最新明确要求
按最终确认顺序整理如下：

1. **职业显示中文，并取职业名的第一个字**
   - 例如：骑、赤、龙、召、学、武、白、舞

2. **卡片里要保留最高伤害技能和伤害值**
   - 但不要显示这些前缀文字：
     - `最高：`
     - `最高一击`

3. **玩家名字放到色块上方**
   - 色块顶部外面是一行：`职业首字 + 玩家名`

4. **色块内部只显示 DPS 数值**
   - 不要把玩家名继续塞在色块里

5. **总 DPS 汇总卡不要了**
   - 即横向 strip 里不再显示单独的“总DPS / 战斗汇总”卡片

6. **占比列不要了**
   - 卡片正文里不显示占比
   - 也不要把占比作为显性正文信息继续摆出来

7. **下方整条信息栏里，不要再放 DPS-总伤 / 暴击率 / 命中率**
   - 底部整条只保留战斗条本身的核心信息
   - 当前理解是：左侧时间 + 区域，右侧总 DPS

8. **总伤、暴击率、命中率放到 tooltip 里**
   - tooltip 触发区域是 **色块本身**
   - 不是卡片正文
   - 不是底部整条信息栏
   - 不是进度条 tooltip

---

## 已完成的代码改动

### 1）展示模式系统已经搭起来
已改文件：
- `DalamudACT/Configuration/PluginConfiguration.cs`
- `DalamudACT/UI/SettingsWindow.cs`

已完成内容：
- 新增 `FloatingStatsDisplayStyle`
  - `Classic = 0`
  - `Ikegami = 1`
- 配置项 `FloatingStatsDisplayStyle` 已接入配置类
- 设置页新增 **“悬浮窗展示模式”**
- 经典表格样式保留
- `ikegami` 作为独立展示模式入口已经可选

### 2）`StatsPanel.cs` 已拆出 Classic / Ikegami 两条渲染分支
已改文件：
- `DalamudACT/UI/StatsPanel.cs`

已完成内容：
- `DrawMetricTab(...)` 已根据 `config.FloatingStatsDisplayStyle` 分流：
  - `Classic` -> `DrawClassicMetricTab(...)`
  - `Ikegami` -> `DrawIkegamiMetricTab(...)`
- ikegami 相关的卡片样式、颜色、footer、badge、detail 文案等辅助方法都已经建起来了

### 3）悬浮窗尺寸和样式切换逻辑已接入
已改文件：
- `DalamudACT/UI/FloatingStatsWindow.cs`

已完成内容：
- `Ikegami` 默认窗口尺寸：
  - 展开：`920 x 126`
  - 折叠：`220 x 42`
- 已修复 **从 Classic 切到 Ikegami 时，展开尺寸仍沿用旧尺寸** 的问题
- 已补 `ApplyDisplayStyleLayoutChange(...)`，确保切换样式后窗口尺寸更接近目标样式

### 4）横向排列问题做过一次关键修复
已改文件：
- `DalamudACT/UI/StatsPanel.cs`

已完成内容：
- `DrawIkegamiMetricTab(...)` 中的 `SameLine(...)` 已改成在每张卡片绘制后调用
- strip 子区域已启用：
  - `ImGuiWindowFlags.HorizontalScrollbar`
- strip 区域高度已额外给滚动条留空间
- 已删除 `DrawIkegamiMetricCard()` 末尾那条会干扰横排的：
  - `ImGui.Spacing();`

这部分的目的是修复用户反复反馈的：
- **“还是没有横过来”**

### 5）职业首字 + 最高伤害技能已接进 ikegami 卡片
已改文件：
- `DalamudACT/UI/StatsPanel.cs`

已完成内容：
- `ResolveIkegamiJobBadgeText(Combatant combatant)`
  - 当前直接取 `combatant.Job` 的第一个字
- DPS 卡片 detail 已能显示 `combatant.MaxHitText`
- 汇总 detail 已能显示 encounter 级别的 `MaxHitText + MaxHitValueText`

### 6）已经把“最高：/最高一击”前缀从插件代码里去掉
已改文件：
- `DalamudACT/UI/StatsPanel.cs`

已完成内容：
- `ResolveIkegamiPrimaryDetailText(...)`
  - 现在返回纯 `combatant.MaxHitText`
- `ResolveIkegamiSummaryDetailText(...)`
  - 现在返回纯 `JoinPair(encounter.MaxHitText, encounter.MaxHitValueText)`

也就是说，代码里已经去掉：
- `最高：`
- `最高一击`

---

## 当前 HTML 预览基准（最重要）
本轮 **最新、最应该跟的样式基准** 是：

- `md/images/ikegami-preview.html`

这份 HTML 预览已经改到符合用户最新口述要求的版本：

### 当前 HTML 版式
- 横向 strip
- **没有总 DPS 汇总卡**
- 每张卡片上方一行：
  - `职业首字 + 玩家名字`
- 色块内部：
  - **只显示 DPS 数值**
- 卡片正文：
  - **只保留最高伤害技能 + 伤害值**
- 底部卡片 footer：
  - `DPS · 实时`
- 下方整条 encounter bar：
  - 左侧：时间 + 区域
  - 右侧：总 DPS
- 色块 hover tooltip：
  - 总伤
  - 暴击率
  - 命中率

### 重要提醒
- `md/images/ikegami-preview.png` **大概率已经落后于最新 HTML**
- 新会话里如果要继续做 UI，请**优先看 HTML，不要以 PNG 为准**

---

## 当前插件代码和最新 HTML 目标之间，还差什么
这部分是下一位最需要注意的。

### 1）插件里大概率还保留着“总 DPS 汇总卡”逻辑
当前 `DrawDpsTab(...)` 调 `DrawMetricTab(...)` 时仍传了：
- `showSummaryRow: true`

而 `DrawIkegamiMetricTab(...)` 里也还保留了 summary card 的渲染分支。

这和用户最新要求冲突：
- **总DPS那个不需要了**

### 2）插件里的名字位置还不是最新 HTML 结构
当前 `DrawIkegamiMetricCard(...)` 的 header 内仍在画：
- badge
- title
- primaryMetricDisplayText

也就是说，**玩家名目前还是在色块区里**，没有真正移到色块上方。

而用户的最新要求是：
- **名字放到色块的上方，色块内只显示 DPS 数值**

### 3）当前 tooltip 还不是挂在“色块”上
现在 `DrawIkegamiMetricCard(...)` 的 tooltip 触发点仍是：
- `ImGui.ProgressBar(...)`
- 然后调用 `drawTooltip()`

也就是 tooltip 还是跟着 progress bar / 当前 item 走的。

用户最新要求是：
- **tips 放到色块上**

所以接下来需要把 tooltip 触发点改到 header / color block 上，而不是底部进度条。

### 4）当前 tooltip 内容也不是用户要的那套
现在 DPS 卡片 tooltip 主要还是沿用：
- 伤害量
- DPS
- 死亡

但用户最新要求是 tooltip 里要放：
- **总伤**
- **暴击率**
- **命中率**

可直接利用的字段已经存在：
- `combatant.DamageText`
- `combatant.CritHitPercentText`
- `combatant.ToHitText`

### 5）当前卡片内可能仍有 progress bar，需要重新判断是否保留
`DrawIkegamiMetricCard(...)` 目前仍然画了：
- `ImGui.ProgressBar(...)`

而用户已经明确：
- 占比不要
- 色块内只显示 DPS 数值

虽然用户没逐字点名“删进度条”，但**从最新 HTML 预览方向看，当前进度条大概率不再是目标样式的一部分**。

下一位建议结合 HTML 预览决定：
- 要么彻底去掉 progress bar
- 要么改成完全不抢视觉中心的弱化元素

### 6）当前 detail 逻辑里还可能夹带别的文本
`ResolveIkegamiDetailText(...)` 现在仍保留了把死亡信息拼进去的逻辑。

但最新 HTML 预览里，正文已经压到只剩一行：
- `最高伤害技能 - 数值`

所以下一步需要确认：
- ikegami 模式下是否直接只保留这一行正文
- 死亡 / 其他统计全部转 tooltip 或隐藏

---

## 建议下一位的直接施工顺序

### 第一步：先完全按 HTML 结构重排 `DrawIkegamiMetricCard(...)`
建议把当前 card 拆成更明确的结构：

1. **名字行**（色块外）
   - 职业首字 badge
   - 玩家名

2. **色块 header**
   - 只显示 DPS 数值
   - tooltip 挂这里

3. **正文 body**
   - 只显示一行：最高伤害技能 + 数值

4. **footer**
   - `DPS · 实时`

### 第二步：去掉 summary card
推荐直接在 `Ikegami` 分支里忽略 `showSummaryRow`，或者在 `DrawDpsTab(...)` 里对 `Ikegami` 不再传 summary row。

目标是让 strip 里只剩玩家卡，不再有“总DPS”卡。

### 第三步：改 tooltip 内容
DPS 卡片 tooltip 建议至少显示：
- `总伤：{combatant.DamageText}`
- `暴击率：{combatant.CritHitPercentText}`
- `命中率：{combatant.ToHitText}`

如果后面用户又要扩展样式，这里可以继续加，但当前先按这个最小集走。

### 第四步：确认底部 encounter bar 不再塞多余统计
保持用户当前要求：
- 左侧：时间 + 区域
- 右侧：总 DPS

不要再把这些塞回去：
- 总伤
- 暴击率
- 命中率
- 占比

### 第五步：按改动重新 build
当前最新一次构建通过命令：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

最近一次结果（2026-05-12）：
- `0 个警告`
- `0 个错误`

产物：
- `E:\git\DalamudACT\output\DalamudACT.dll`

---

## 本轮涉及的关键文件
### 代码
- `DalamudACT/Configuration/PluginConfiguration.cs`
- `DalamudACT/UI/SettingsWindow.cs`
- `DalamudACT/UI/FloatingStatsWindow.cs`
- `DalamudACT/UI/StatsPanel.cs`

### 预览 / 参考
- `md/images/ikegami-preview.html`
- `md/images/ikegami-preview.png`（注意：可能已过时）

---

## 当前工作区状态（交接时）
交接时 `git status --short` 看到的关键项：

- `M DalamudACT/Configuration/PluginConfiguration.cs`
- `M DalamudACT/UI/FloatingStatsWindow.cs`
- `M DalamudACT/UI/JobThemePalette.cs`
- `M DalamudACT/UI/SettingsWindow.cs`
- `M DalamudACT/UI/StatsPanel.cs`
- `M HANDOVER.md`
- `M README.md`
- `M md/CHANGELOG.md`
- `M md/RECENT-ISSUES-STATUS-TABLE.md`
- `M md/USAGE.md`
- `?? 1.txt`
- `?? md/images/ikegami-preview.html`
- `?? md/images/ikegami-preview.png`

### 特别提醒
- `1.txt` 是旧现场遗留的未跟踪文件
- **不要误删**
- 当前工作区本来就不是干净状态，接手时不要把旧改动误当成这轮新改动

---

## 给下一位的一句话总结
这轮已经把 `Classic / Ikegami` 展示模式、窗口尺寸、横向 strip 基础和职业首字 / 最高伤害技能这些底层骨架搭起来了，**最新样式方向以 `md/images/ikegami-preview.html` 为准**；下一步不要再纠结旧 PNG 或旧卡片结构，直接把 `StatsPanel.cs` 的 ikegami 卡片改成“**名字在色块上方、色块内只显示 DPS、tooltip 挂在色块上、无总DPS汇总卡**”这一版即可。
