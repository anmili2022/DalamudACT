# DPS统计 结项汇总

这份文档适合：

- 准备提交这轮改动的人
- 准备发版本公告的人
- 需要快速了解“这轮到底做了什么”的接手者

## 本轮工作范围

本轮主要完成了三类工作：

1. 继续完善插件本体功能
2. 补齐悬浮面板和历史记录的交互细节
3. 将仓库文档整理成更接近正式项目的结构

## 本轮主要代码改动

### 1. DPS 统计范围收口

- 当前只统计：
  - 自己
  - 当前队伍里的队友
- 队伍外玩家不会进入统计
- 队伍里的 NPC 队友仍然会进入统计
- 宠物 / 召唤物伤害会归到主人头上

相关文件：

- `DalamudACT/Stats/LocalStatsService.cs`

### 2. DPS 页面增强

- 新增 `伤害量` 列
- `伤害量` 列可单独显示 / 隐藏
- `职业 / 伤害量 / 秒伤 / 死亡` 列均可在设置中控制
- `总DPS` 汇总行继续保留在表格底部

相关文件：

- `DalamudACT/Configuration/PluginConfiguration.cs`
- `DalamudACT/UI/SettingsWindow.cs`
- `DalamudACT/UI/StatsPanel.cs`

### 3. 伤害数值显示中文化

- 伤害相关数字改为中文单位显示：
  - `万`
  - `亿`
  - `兆`
- 影响范围包括：
  - `DPS` 页伤害量
  - 概览中的总伤害 / 总承伤
  - 最大伤害文本
  - 测试数据

相关文件：

- `DalamudACT/Stats/LocalStatsService.cs`

### 4. 悬浮面板交互补齐

- `DPS` 页签左键可折叠 / 恢复
- 折叠态窗口尺寸固定为：
  - 宽度 `270`
  - 高度 `42`
- `DPS` 页签右键可打开 / 关闭设置窗口

相关文件：

- `DalamudACT/UI/FloatingStatsWindow.cs`
- `DalamudACT/UI/PluginUI.cs`
- `DalamudACT/UI/StatsPanel.cs`

### 5. 悬浮面板设置补齐

- 增加表格布局设置：
  - `玩家列最小宽度`
  - `固定列宽`
  - `表格行高`

相关文件：

- `DalamudACT/Configuration/PluginConfiguration.cs`
- `DalamudACT/UI/SettingsWindow.cs`
- `DalamudACT/UI/StatsPanel.cs`

### 6. 历史记录支持点击回看

- 历史记录现在保存完整战斗快照
- 点击某条历史记录后：
  - 悬浮面板切换到那场战斗的数据
  - `DPS / HPS / 承伤 / 概览` 一起切换
- 当前选中的历史记录支持高亮
- 新战斗开始后自动切回实时数据

相关文件：

- `DalamudACT/UI/StatsModels.cs`
- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/UI/StatsPanel.cs`

### 7. 测试数据逻辑调整

- `导入测试数据` 不再清空已有历史记录
- 重复导入相同测试记录时改为覆盖更新
- 3 条测试数据已经拆成不同场景，不再只是名称不同

相关文件：

- `DalamudACT/Stats/LocalStatsService.cs`

### 8. 主窗口入口补齐

- 已注册主窗口入口
- 修复“未注册 main UI callback”的插件校验问题

相关文件：

- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/UI/PluginUI.cs`

## 本轮主要文档改动

### 1. 使用说明重写

- 已将使用说明改成普通玩家也能读懂的版本
- 增加：
  - `3分钟快速上手`
  - `第一次建议这样设置`
  - `最常用的几个操作`
  - `常见问题`
- 已接入真实截图

文件：

- `md/USAGE.md`

### 2. 更新记录整理

- 已将更新记录整理成：
  - `未发布`
  - `已发布版本`
  - `更早历史`

文件：

- `md/CHANGELOG.md`

### 3. 发布说明补齐

- 已新增可直接复制使用的发布说明文档
- 包括：
  - 一句话介绍
  - 短版更新公告
  - 标准版更新说明
  - 面向玩家的说明

文件：

- `md/RELEASE-NOTES.md`

### 4. README 首页重构

- README 已从“交接说明”改成“项目首页”
- 当前首页已包含：
  - 插件简介
  - 界面预览
  - 安装与启用
  - 当前功能
  - 统计范围
  - 快速开始
  - 常见问题
  - 文档入口

文件：

- `README.md`

## 当前对外可见状态

从普通用户视角看，这一轮最明显的变化是：

- `DPS` 页可以看 `伤害量`
- 伤害数字改成中文单位显示
- 历史记录可以点进去回看
- 悬浮窗的 `DPS` 页签交互更完整
- 设置项更细，面板更容易调到自己喜欢的样子
- 文档比之前更像正式项目文档

## 当前已知限制

当前版本仍然以“稳定能用”为优先，仍有以下限制：

- 某些更复杂的持续伤害统计仍在继续完善
- 某些更复杂的持续治疗统计仍在继续完善
- 个别更细的战斗补算逻辑还没有完全补齐

## 构建验证

本轮文档整理期间最近一次已验证构建命令：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Debug
```

结果：

- `0 warnings`
- `0 errors`

## 推荐提交说明

### 推荐 commit title

可选 1：

```text
feat: 完善DPS面板/历史记录回看并重构项目文档
```

可选 2：

```text
feat: add damage column, history replay, docs refresh
```

### 推荐 commit body

```text
- limit tracked actors to self and current party members
- add DPS damage column and per-column visibility settings
- format damage values with Chinese units
- improve floating window DPS tab interactions
- support loading historical snapshots from history list
- keep existing history when importing test data
- add main UI callback registration
- rewrite README/USAGE/CHANGELOG and add release notes
```

## 推荐发布标题

```text
DPS统计：伤害量列、历史记录回看与文档整理
```

## 推荐发布摘要

```text
本次更新补齐了 DPS 页面和历史记录体验。现在可以查看伤害量列，伤害数字改为中文单位显示；历史记录支持点击回看整场战斗；导入测试数据不会再清空已有历史；悬浮面板和设置项也更完整。文档方面同步补齐了使用说明、更新记录和发布说明。
```

## 相关文档入口

- [README](../README.md)
- [使用说明](USAGE.md)
- [更新记录](CHANGELOG.md)
- [发布说明](RELEASE-NOTES.md)
- [2026-05-06 工作记录](2026-05-06.md)
- [SESSION-HANDOFF](SESSION-HANDOFF.md)
