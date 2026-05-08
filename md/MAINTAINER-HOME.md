# 维护首页（单页总览）

更新时间：`2026-05-08`

用途：这是 `DalamudACT` 的**一页式维护首页**。  
如果你刚接手项目，或者只是想先快速知道“现在是什么状态、先看什么、不要做什么、接下来从哪里下手”，优先看这一页即可。

---

## 相关维护文档

- [维护文档总览图](MAINTAINER-DOC-MAP.md)
- [维护入口索引](MAINTAINER-INDEX.md)
- [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
- [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)
- [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
- [HANDOVER.md](../HANDOVER.md)

---

## 目录 / TOC

- [30 秒结论](#home-summary)
- [当前可信快照](#home-snapshot)
- [5 分钟先看什么](#home-5min)
- [30 分钟推荐阅读顺序](#home-30min)
- [当前状态一眼看懂](#home-status)
- [按任务快速分流](#home-routing)
- [最常看的文档入口](#home-docs)
- [常用外部接口文档](#home-refs)
- [最常看的代码入口](#home-code)
- [最容易踩坑的点](#home-pitfalls)
- [建议的下一步顺序](#home-next)
- [一句话总结](#home-final)

---

<a id="home-summary"></a>
## 一、30 秒结论

当前项目已经从早期依赖外部数据源的面板壳，转成了：

- **本地采集**
- **本地统计**
- **本地 UI 展示**

当前维护重点不是“从零搭框架”，而是：

1. 继续验证当前稳定基线
2. 继续完善历史预览流 / UI / 可用性
3. 在确保稳定的前提下，逐步恢复高风险 Hook 相关能力
4. 维持已经打通的 GitHub Actions 构建与发布流程

---

<a id="home-snapshot"></a>
## 二、当前可信快照

基于 `HANDOVER.md` 当前记录：

- 工作目录：`E:\git\DalamudACT`
- 当前分支：`main`
- 当前 HEAD：`431aad4`
- 当前工作区：**脏工作区**
- 接手前请先执行 `git status --short`，以当前输出为准
- 当前未提交修改主要集中在：
  - 共享列显示合并与列宽配置持久化
  - 主窗口 / 设置窗口卡片式 UI 与配置摘要
  - 历史预览流、悬浮窗交互与相关文档补充
- 最近一次已验证本地构建：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

- 最近一次记录结果：`0 warnings / 0 errors`
- 当前可信产物路径：`E:\git\DalamudACT\output\DalamudACT.dll`

当前这批修改涉及的重点代码 / 文档入口：

- `DalamudACT/Configuration/PluginConfiguration.cs`
- `DalamudACT/UI/StatsPanel.cs`
- `DalamudACT/UI/SettingsWindow.cs`
- `DalamudACT/UI/MainWindow.cs`
- `README.md`
- `md/USAGE.md`
- `HANDOVER.md`

---

<a id="home-5min"></a>
## 三、如果你只有 5 分钟，先看什么

按下面顺序：

1. [HANDOVER.md](../HANDOVER.md)
2. [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
3. [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)

你至少要先确认这 4 件事：

- 当前仓库现场是否和交接记录一致
- 当前哪些问题已解决，哪些只是暂时降级
- 当前可信构建产物路径是不是还在 `output\DalamudACT.dll`
- 你这一轮是在做稳定性、UI/历史，还是发布维护

---

<a id="home-30min"></a>
## 四、如果你有 30 分钟，推荐阅读顺序

1. [本页：维护首页（单页总览）](MAINTAINER-HOME.md)
2. [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
3. [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)
4. [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
5. [HANDOVER.md](../HANDOVER.md)
6. [2026-05-06 发布交接](2026-05-06-RELEASE-HANDOFF.md)

---

<a id="home-status"></a>
## 五、当前状态：一眼看懂

### 已基本完成

- 插件已不再以外部 `MiniParse` 为最终依赖方案
- 当前主链路已转为本地统计
- 运行时兼容问题已做过一轮关键修复
- 历史记录支持保存快照并点击回看
- 测试数据导入逻辑已改为更适合持续验证
- `DPS / HPS / 承伤` 已共用一组列显示设置，玩家 / 职业 / 伤害 / 秒伤 / 死亡 / 显示人数可以统一控制
- 统计页与历史页列宽已经写入配置文件，重开插件可恢复，切换列显示时不再重置列宽
- 悬浮窗交互、锁定与设置项已补齐
- 主窗口与设置窗口已整理为卡片式 UI，主窗口新增界面与列配置摘要
- GitHub Actions 构建与正式发布流程已重新打通

### 还在继续验证

- 当前 `output\DalamudACT.dll` 的长期稳定性
- `ActionEffect` 主链路在真实多场战斗中的持续稳定性
- 历史记录回看 → 新战斗切回实时数据的完整闭环
- 多次导入测试数据后的历史稳定性

### 还未真正完成

- `Cast` Hook 安全恢复
- `ActorControlSelf` Hook 安全恢复
- DoT 归因补齐
- HoT 归因补齐
- 更完整的 Death / 遭遇生命周期补链

详细表格请看：

- [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)

---

<a id="home-routing"></a>
## 六、按任务快速分流

### 1. 我是刚接手维护的人

先看：

1. [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
2. [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
3. [HANDOVER.md](../HANDOVER.md)

### 2. 我想搞清楚最近到底修了哪些问题

先看：

1. [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)
2. [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
3. [SESSION-HANDOFF.md](SESSION-HANDOFF.md)

### 3. 我要排查“启用后崩溃 / 不出数 / 兼容问题”

先看文档：

1. [HANDOVER.md](../HANDOVER.md)
2. [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)
3. [SESSION-HANDOFF.md](SESSION-HANDOFF.md)

再看代码：

1. `DalamudACT/Plugin/ACT.cs`
2. `DalamudACT/DalamudApi.cs`
3. `DalamudACT/Stats/LocalStatsService.cs`
4. `DalamudACT/UI/StatsPanel.cs`
5. `DalamudACT/UI/FloatingStatsWindow.cs`

### 4. 我要继续做历史预览流 / UI / 可用性

优先看：

1. [2026-05-06 工作记录](2026-05-06.md)
2. [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
3. `DalamudACT/Stats/LocalStatsService.cs`
4. `DalamudACT/UI/StatsPanel.cs`
5. `DalamudACT/UI/MainWindow.cs`
6. `DalamudACT/UI/SettingsWindow.cs`
7. `DalamudACT/Configuration/PluginConfiguration.cs`

### 5. 我要发版或修发布流程

优先看：

1. [2026-05-06 发布交接](2026-05-06-RELEASE-HANDOFF.md)
2. [RELEASE-RUNBOOK.md](RELEASE-RUNBOOK.md)
3. `repo.json`
4. `.github/workflows/build.yml`
5. `.github/workflows/release.yml`

---

<a id="home-docs"></a>
## 七、最常看的文档入口

- [HANDOVER.md](../HANDOVER.md)
- [维护入口索引](MAINTAINER-INDEX.md)
- [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
- [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)
- [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
- [2026-05-06 发布交接](2026-05-06-RELEASE-HANDOFF.md)
- [RELEASE-RUNBOOK.md](RELEASE-RUNBOOK.md)
- [SESSION-HANDOFF.md](SESSION-HANDOFF.md)
- [2026-05-06 工作记录](2026-05-06.md)
- [2026-05-05 工作记录](2026-05-05.md)

---

<a id="home-refs"></a>
## 八、常用外部接口文档

- Dalamud 文档首页：<https://dalamud.dev/>
- Dalamud API 参考：<https://dalamud.dev/api/>
- Lumina.Excel 仓库：<https://github.com/NotAdam/Lumina.Excel>

快速判断：

- 看 `DalamudApi.cs`、`PluginService`、窗口/UI、命令、状态、`IDataManager` 等接口时，先查 Dalamud 文档 / API。
- 看 `ACT.cs` 里的 `GetExcelSheet<T>()`、`ExcelSheet<T>`、`Lumina.Excel.Sheets.*` 时，优先查 `Lumina.Excel`。

---

<a id="home-code"></a>
## 九、最常看的代码入口

- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/DalamudApi.cs`
- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/UI/StatsPanel.cs`
- `DalamudACT/UI/FloatingStatsWindow.cs`
- `DalamudACT/UI/SettingsWindow.cs`
- `DalamudACT/Configuration/PluginConfiguration.cs`

---

<a id="home-pitfalls"></a>
## 十、接手时最容易踩坑的点

不要一上来就做这些事：

- `git reset --hard`
- `git checkout --`
- 一次性恢复所有高风险 Hook
- 对旧失败 GitHub Actions 任务直接点 `Re-run jobs`
- 把旧的 `bin/Release/*` 当成当前可信产物路径

特别要记住：

- 当前工作区是**脏工作区**
- 当前未提交修改主要在恢复**历史预览流**
- 正式发布时，如果 tag 签名阻塞，要优先使用：

```powershell
git -C E:\git\DalamudACT -c tag.gpgSign=false tag -a <version> -m "DalamudACT <version>"
```

---

<a id="home-next"></a>
## 十一、建议的下一步顺序

如果下一轮继续推进，建议按这个顺序：

1. 先确认当前仓库现场与交接记录一致
2. 再确认当前版本仍可构建通过
3. 再做真实环境下的稳定性验证
4. 稳定后，再继续推进历史 / UI / 可用性问题
5. 最后再分步恢复 `Cast` 与 `ActorControlSelf`

恢复高风险 Hook 时必须遵守：

- 每个 Hook 独立 `try/catch`
- 每个 Hook 独立日志
- 任一 Hook 失败都要能降级继续运行
- 不要一次性全部恢复

---

<a id="home-final"></a>
## 十二、一句话总结

如果你不知道从哪里开始，就把这页当成维护首页：

- 想快速进入状态，看“第一小时清单”
- 想知道最近修了什么，看“问题整理”和“状态表”
- 想查完整上下文，看 `HANDOVER.md`
- 想发版，看“发布交接”和 `RELEASE-RUNBOOK`
