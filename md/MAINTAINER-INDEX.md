# 维护入口索引

更新时间：`2026-05-08`

用途：给继续维护 `DalamudACT` 的人一个统一入口，帮助快速判断**先看什么**、**按什么顺序看**、**不同任务该查哪份文档**。

---

## 相关维护文档

- [维护首页（单页总览）](MAINTAINER-HOME.md)
- [维护文档总览图](MAINTAINER-DOC-MAP.md)
- [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
- [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)
- [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
- [HANDOVER.md](../HANDOVER.md)

---

## 一、最快入口

如果你刚接手，先看这几份：

1. [维护首页（单页总览）](MAINTAINER-HOME.md)
2. [维护文档总览图](MAINTAINER-DOC-MAP.md)
3. [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
4. [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)

如果你需要完整上下文，再继续看：

5. [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
6. [HANDOVER.md](../HANDOVER.md)
7. [2026-05-09 发布交接](2026-05-09-RELEASE-HANDOFF.md)
8. [RELEASE-RUNBOOK.md](RELEASE-RUNBOOK.md)
9. [SESSION-HANDOFF.md](SESSION-HANDOFF.md)

---

## 二、按任务找文档

### 1. 刚接手，只想先进入状态

按这个顺序：

1. [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
2. [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
3. [HANDOVER.md](../HANDOVER.md)

### 2. 想搞清楚最近到底修了哪些问题

按这个顺序：

1. [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)
2. [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
3. [SESSION-HANDOFF.md](SESSION-HANDOFF.md)
4. [2026-05-09 工作记录](2026-05-09.md)
5. [2026-05-05 工作记录](2026-05-05.md)

### 3. 要排查运行时崩溃、兼容、出数问题

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

### 4. 要继续做历史预览流 / UI / 可用性

优先看：

1. [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
2. [2026-05-09 工作记录](2026-05-09.md)
3. `DalamudACT/Stats/LocalStatsService.cs`
4. `DalamudACT/UI/StatsPanel.cs`
5. `DalamudACT/UI/MainWindow.cs`
6. `DalamudACT/UI/SettingsWindow.cs`
7. `DalamudACT/Configuration/PluginConfiguration.cs`
8. `README.md`
9. `md/USAGE.md`

### 6. 要补 README / 使用说明 / 交接文档

优先看：

1. `README.md`
2. `md/README-SUMMARY.md`
3. `md/USAGE.md`
4. [维护首页（单页总览）](MAINTAINER-HOME.md)
5. [HANDOVER.md](../HANDOVER.md)

补文档时重点统一这几类表述：

- `DPS / HPS / 承伤` 三页共享列显示
- 统计页 / 历史页列宽写入配置文件并在下次打开时恢复
- 主窗口 / 设置窗口卡片式 UI
- `锁定悬浮窗口`
- `历史记录预览时长（秒）`

### 5. 要发版或修发布流程

优先看：

1. [2026-05-09 发布交接](2026-05-09-RELEASE-HANDOFF.md)
2. [RELEASE-RUNBOOK.md](RELEASE-RUNBOOK.md)
3. [HANDOVER.md](../HANDOVER.md)
4. `repo.json`
5. `.github/workflows/build.yml`
6. `.github/workflows/release.yml`

---

## 三、常用外部接口文档

- Dalamud 文档首页：<https://dalamud.dev/>
- Dalamud API 参考：<https://dalamud.dev/api/>
- Lumina.Excel 仓库：<https://github.com/NotAdam/Lumina.Excel>

快速对应关系：

- `DalamudApi.cs`、`PluginService`、窗口/UI、命令、状态、`IDataManager` 相关接口，优先查 Dalamud。
- `ACT.cs` 中的 `GetExcelSheet<T>()`、`ExcelSheet<T>`、`Lumina.Excel.Sheets.*`，优先查 `Lumina.Excel`。

---

## 四、当前可信基线

基于 `HANDOVER.md` 当前记录：

- 工作目录：`E:\git\DalamudACT`
- 当前分支：`main`
- 当前 HEAD：`fc352b4`
- 当前工作区：**有未跟踪文件 `1.txt`**
- 接手前先执行 `git status --short`，以当前输出为准
- 这批未提交内容当前只剩一个未跟踪文件 `1.txt`，用途待确认。
- 最近一次已验证构建命令：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

- 最近一次记录结果：`0 warnings / 0 errors`
- 当前可信产物路径：`E:\git\DalamudACT\output\DalamudACT.dll`

---

## 五、当前最重要的未完成项

截至当前交接记录，仍需后续继续推进的重点有：

1. `Cast` Hook 安全恢复
2. `ActorControlSelf` Hook 安全恢复
3. DoT 归因补齐
4. HoT 归因补齐
5. 更完整的 Death / 遭遇生命周期补链
6. 更多真实用户环境下的长期稳定性验证

详细状态请看：

- [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)

---

## 六、接手时最容易踩坑的点

- 不要一上来就做 `git reset --hard`
- 不要把旧失败 GitHub Actions 任务直接 `Re-run jobs`
- 不要一次性恢复所有高风险 Hook
- 不要忽略当前工作区仍有未跟踪文件 `1.txt` 这一事实
- 不要把旧的 `bin/Release/*` 当作当前可信产物路径

---

## 七、维护建议

如果你只有 15 分钟：

- 先看 [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)

如果你只有 30 分钟：

- 再补看 [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)

如果你要真正开始改代码：

- 至少先看完 [HANDOVER.md](../HANDOVER.md) 和 [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)
