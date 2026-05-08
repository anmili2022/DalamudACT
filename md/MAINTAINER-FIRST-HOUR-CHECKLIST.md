# 下一位维护者第一小时清单

更新时间：`2026-05-08`

用途：适合刚接手这个仓库时使用，帮助你在 **1 小时内** 快速搞清楚“现在是什么状态、哪些地方不能乱动、接下来该从哪里下手”。

---

## 相关维护文档

- [维护首页（单页总览）](MAINTAINER-HOME.md)
- [维护文档总览图](MAINTAINER-DOC-MAP.md)
- [维护入口索引](MAINTAINER-INDEX.md)
- [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)
- [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
- [HANDOVER.md](../HANDOVER.md)

---

## 0. 先记住当前快照

基于 `HANDOVER.md` 当前记录：

- 仓库目录：`E:\git\DalamudACT`
- 当前分支：`main`
- 当前 HEAD：`fc352b4`
- 当前工作区有一个未跟踪文件 `1.txt`
- 接手前先执行 `git status --short`，以当前输出为准
- 当前工作区里只剩一个未跟踪文件 `1.txt`，用途待确认。
- 最近一次已验证本地构建：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

- 最近一次记录结果：`0 warnings / 0 errors`
- 当前可信产物位置：`E:\git\DalamudACT\output\DalamudACT.dll`

> 第一原则：**不要一上来做破坏性 git 操作。**

尤其不要先用：

- `git reset --hard`
- `git checkout --`

---

## 1. 第 0～10 分钟：先看这 4 份文档

按这个顺序读，够用：

1. [HANDOVER.md](../HANDOVER.md)
2. [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)
3. [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
4. [2026-05-09 发布交接](2026-05-09-RELEASE-HANDOFF.md)

这一轮先不要深读所有工作记录，先回答 4 个问题：

- 当前最重要的问题已经解决了哪些？
- 还有哪些只是“暂时降级”，不是最终解？
- 当前工作区为什么是脏的？
- 现在可信的构建产物和发布路径是什么？

---

## 2. 第 10～20 分钟：确认你面对的是哪个现场

建议先做这几个最小确认：

```powershell
git -C E:\git\DalamudACT status --short
git -C E:\git\DalamudACT rev-parse --short HEAD
```

重点确认：

- 当前 HEAD 是否仍与交接记录和当前文档一致
- 当前工作区是否仍只有这个未跟踪文件 `1.txt`

如果现场和交接记录差异很大，先别急着改代码，先判断：

- 是你本地已有新改动
- 还是接手时仓库状态已经发生变化

---

## 3. 第 20～35 分钟：只抓 4 个关键代码入口

如果你这轮要继续排查或接着改，先只看下面 4 个文件：

1. `DalamudACT/Plugin/ACT.cs`
2. `DalamudACT/DalamudApi.cs`
3. `DalamudACT/Stats/LocalStatsService.cs`
4. `DalamudACT/UI/StatsPanel.cs`

为什么先看这 4 个：

- `ACT.cs`：插件入口、框架更新、Hook 主链路
- `DalamudApi.cs`：运行时兼容层，很多崩溃问题都和这里有关
- `LocalStatsService.cs`：本地统计核心，死亡、历史、测试数据都在这里汇总
- `StatsPanel.cs`：数据最终怎么展示、为什么界面没出数，通常要看这里

这一阶段的目标不是读完，而是先建立脑图：

- 数据从哪里进来
- 在哪里统计
- 在哪里切实时 / 历史
- 在哪里决定界面显示什么

如果你这轮接的是当前这批“共享列 / 列宽 / UI / 文档”工作，再补看：

- `DalamudACT/UI/MainWindow.cs`
- `DalamudACT/UI/SettingsWindow.cs`
- `DalamudACT/Configuration/PluginConfiguration.cs`
- `README.md`
- `md/README-SUMMARY.md`
- `md/USAGE.md`

如果你在读这些文件时需要对照外部接口文档，优先看：

- Dalamud 文档首页：<https://dalamud.dev/>
- Dalamud API 参考：<https://dalamud.dev/api/>
- Lumina.Excel 仓库：<https://github.com/NotAdam/Lumina.Excel>

快速对应关系：

- `DalamudApi.cs`、`PluginService`、窗口/UI、命令、状态、`IDataManager` 相关接口，优先查 Dalamud。
- `ACT.cs` 中的 `GetExcelSheet<T>()`、`ExcelSheet<T>`、`Lumina.Excel.Sheets.*`，优先查 `Lumina.Excel`。

---

## 4. 第 35～50 分钟：做最低成本验证

如果只是确认仓库目前是否还健康，优先做这一条：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

你只需要先确认 3 件事：

1. 是否还能构建通过
2. 产物是否仍落在 `output\DalamudACT.dll`
3. 是否出现了交接文档里没提到的新错误

如果你是在排查运行时问题，再补看这些日志：

- `E:\git\crash\crash.log`
- `E:\git\crash\dalamud.log`
- `E:\git\crash\output.log`
- `E:\git\crash\trouble.json`

已知关键定位时间点来自交接记录：

- `2026-05-05 18:55:50 +08:00`

---

## 5. 第 50～60 分钟：决定你这轮属于哪一种工作

到这一步，通常只会落到下面 3 类之一：

### A. 继续做“稳定性优先”

适合这种情况：

- 你怀疑还有崩溃 / 兼容问题
- 你不确定当前 Hook 恢复会不会再次把插件打崩

优先顺序：

1. 先验证 `ActionEffect` 主链路
2. 不要同时恢复多个高风险 Hook
3. 任何恢复都要带独立 `try/catch` 和独立日志

### B. 继续做“历史 / UI / 可用性”

适合这种情况：

- 你主要在接“历史预览流 + 共享列 + 列宽记忆 + 卡片式 UI”这批已落地、但仍需继续验证的改动
- 你想继续补交互、显示、配置持久化和文档同步行为

优先关注：

- `LocalStatsService.cs`
- `StatsPanel.cs`
- `MainWindow.cs`
- `SettingsWindow.cs`
- `PluginConfiguration.cs`
- `README.md`
- `md/USAGE.md`

重点关键词：

- `锁定悬浮窗口`
- `历史记录预览时长（秒）`
- `页面列显示（共享于 DPS / HPS / 承伤）`

### C. 继续做“发布 / 维护”

适合这种情况：

- 你这轮主要是出版本或修 release 流程

先看：

1. [2026-05-09 发布交接](2026-05-09-RELEASE-HANDOFF.md)
2. `repo.json`
3. `.github/workflows/build.yml`
4. `.github/workflows/release.yml`

记住两个要点：

- 不要对旧失败 release 任务直接点 `Re-run jobs`
- 发 tag 时优先使用 `-c tag.gpgSign=false`

---

## 6. 当前最重要的“不要做”

接手第一小时内，尽量不要做这些事：

- 不要一上来恢复所有高风险 Hook
- 不要没看交接就直接改发布流程
- 不要没确认工作区来源就覆盖本地未提交修改
- 不要把旧失败 GitHub Actions 任务直接重跑当成修复手段
- 不要把 `output\DalamudACT.dll` 之外的旧路径当成当前可信产物

---

## 7. 一句话结论

如果你只有 1 小时，最值得完成的不是“马上改代码”，而是先确认这 4 件事：

1. 当前仓库现场是否与交接记录一致
2. 当前稳定基线到底在哪
3. 当前哪些能力是“已完成”，哪些只是“先降级保稳定”
4. 你这一轮到底是在做稳定性、功能可用性，还是发布维护

做到这一步，后面的接手成本会低很多。
