# 维护文档总览图

更新时间：`2026-05-08`

用途：给维护者一眼看清这套文档之间的关系，知道**先看哪份**、**下一步看哪份**、以及**不同任务该从哪条链路进入**。

相关维护文档：

- [维护首页（单页总览）](MAINTAINER-HOME.md)
- [维护入口索引](MAINTAINER-INDEX.md)
- [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
- [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)
- [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
- [HANDOVER.md](../HANDOVER.md)

---

## 一、先看哪份

如果你刚接手，推荐顺序是：

1. [维护首页（单页总览）](MAINTAINER-HOME.md)
2. [维护入口索引](MAINTAINER-INDEX.md)
3. [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
4. [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)
5. [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
6. [HANDOVER.md](../HANDOVER.md)

---

## 二、文档关系图

```text
README.md
├─ README 简洁总结
├─ 使用说明
├─ 维护首页（单页总览）
│  ├─ 维护入口索引
│  ├─ 下一位维护者第一小时清单
│  ├─ 最近问题与解决方案整理
│  └─ 最近问题状态表（维护视角）
├─ HANDOVER.md
│  ├─ 维护首页（单页总览）
│  ├─ 维护入口索引
│  ├─ 下一位维护者第一小时清单
│  ├─ 最近问题与解决方案整理
│  └─ 最近问题状态表（维护视角）
├─ 2026-05-06 发布交接
├─ 发布 Runbook
└─ SESSION-HANDOFF
```

说明：

- `README.md` 是用户入口，也是维护入口的总导航
- `README-SUMMARY.md` 适合快速确认项目定位与最近可用能力
- `USAGE.md` 适合补用户侧说明、截图说明与设置项解释
- `MAINTAINER-HOME.md` 是维护者最推荐的第一站
- `MAINTAINER-INDEX.md` 用来按任务分流
- `MAINTAINER-FIRST-HOUR-CHECKLIST.md` 用来快速上手
- `RECENT-ISSUES-SUMMARY.md` 用来了解最近问题和解决方案
- `RECENT-ISSUES-STATUS-TABLE.md` 用来快速判断问题状态
- `HANDOVER.md` 是完整交接主文档

---

## 三、按任务看什么

### 1. 刚接手，只想快速进入状态

先看：

- [维护首页（单页总览）](MAINTAINER-HOME.md)
- [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)

### 2. 想知道最近到底修了什么

先看：

- [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)
- [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)

### 3. 想排查运行时崩溃 / 不出数 / 兼容问题

先看：

- [HANDOVER.md](../HANDOVER.md)
- [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)

然后看代码：

- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/DalamudApi.cs`
- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/UI/StatsPanel.cs`
- `DalamudACT/UI/FloatingStatsWindow.cs`

### 4. 想继续做历史预览流 / UI / 可用性

先看：

- [维护入口索引](MAINTAINER-INDEX.md)
- [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
- [最近问题状态表（维护视角）](RECENT-ISSUES-STATUS-TABLE.md)
- [HANDOVER.md](../HANDOVER.md)

再看代码 / 文档：

- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/UI/StatsPanel.cs`
- `DalamudACT/UI/MainWindow.cs`
- `DalamudACT/UI/SettingsWindow.cs`
- `DalamudACT/Configuration/PluginConfiguration.cs`
- `README.md`
- `md/USAGE.md`

### 5. 想发版或修发布流程

先看：

- [2026-05-06 发布交接](2026-05-06-RELEASE-HANDOFF.md)
- [发布 Runbook](RELEASE-RUNBOOK.md)
- [HANDOVER.md](../HANDOVER.md)

### 6. 想补 README / 使用说明 / 维护文档

先看：

- `README.md`
- `md/README-SUMMARY.md`
- `md/USAGE.md`
- [维护首页（单页总览）](MAINTAINER-HOME.md)
- [HANDOVER.md](../HANDOVER.md)

当前这条链路里最容易漏掉的点：

- `DPS / HPS / 承伤` 三页共享列显示
- 切换列显示时不重置当前列宽
- 统计页 / 历史页列宽写入配置文件并恢复
- `锁定悬浮窗口`
- `历史记录预览时长（秒）`
- 主窗口 / 设置窗口卡片式 UI

---

## 四、常用外部接口文档

- Dalamud 文档首页：<https://dalamud.dev/>
- Dalamud API 参考：<https://dalamud.dev/api/>
- Lumina.Excel 仓库：<https://github.com/NotAdam/Lumina.Excel>

快速对应关系：

- `DalamudApi.cs`、`PluginService`、窗口/UI、命令、状态、`IDataManager` 等接口，优先查 Dalamud。
- `ACT.cs` 中的 `GetExcelSheet<T>()`、`ExcelSheet<T>`、`Lumina.Excel.Sheets.*`，优先查 `Lumina.Excel`。

---

## 五、维护入口建议

如果你只记一条：

> 先看 `维护首页`，再看 `维护入口索引`，最后再翻 `HANDOVER.md` 和问题整理。

如果你只想知道“现在该不该动代码”：

- 先看 `最近问题状态表`
- 再看 `第一小时清单`
- 再决定继续修哪个模块

---

## 六、和当前仓库文档的关系

这份总览图不是替代其他文档，而是帮助你把它们串起来：

- `README.md`：对外总入口，同时包含维护入口
- `README-SUMMARY.md`：README 的快速摘要
- `USAGE.md`：用户侧详细使用说明
- `MAINTAINER-HOME.md`：维护者的第一站
- `MAINTAINER-INDEX.md`：任务分流
- `MAINTAINER-FIRST-HOUR-CHECKLIST.md`：快速上手
- `RECENT-ISSUES-SUMMARY.md`：最近问题与解决方案
- `RECENT-ISSUES-STATUS-TABLE.md`：问题状态快表
- `HANDOVER.md`：完整交接主文档
- `SESSION-HANDOFF.md`：阶段性会话交接
- `2026-05-06 发布交接`：发版相关上下文

---

## 七、一句话总结

如果你要接手这个仓库，最推荐的路径是：

**README → 维护首页 → 维护入口索引 → 第一小时清单 → 问题整理 / 状态表 → HANDOVER**
