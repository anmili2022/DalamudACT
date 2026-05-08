# README 简洁总结

> 基于根目录 `README.md` 的快速摘要，适合先了解项目再决定是否继续阅读全文。

## 一句话介绍

`DPS统计` 是一个 Dalamud 插件，用来在游戏内直接查看战斗统计，不依赖外部 ACT 页面或额外网页面板。

## 核心能力

- 在游戏内显示 `DPS / HPS / 承伤 / 概览 / 历史记录`
- 提供独立悬浮面板，可显示、隐藏、折叠和调整透明度
- `DPS / HPS / 承伤` 三页共用一组列显示设置，可同时控制玩家、职业、伤害、秒伤、死亡和显示人数
- 统计页与历史页的列宽会写入配置文件，下次打开插件时自动恢复
- 支持锁定悬浮窗口，避免误拖动和误改大小
- 支持保存战斗历史，并可点击回看当时数据
- 支持导入测试数据，方便未进战时检查界面效果
- 主窗口与设置窗口已经整理为卡片式中文界面，常用状态与配置更容易查看

## 统计范围

当前版本只统计：

- 你自己
- 当前队伍里的队友

补充说明：

- 队伍内 NPC 也会进入统计
- 宠物 / 召唤物伤害会归属给主人
- 队伍外其他玩家不会被统计

## 适合谁

适合希望在 FFXIV 游戏内直接看战斗输出和历史记录的玩家，尤其适合：

- 不想依赖外部 ACT 页面的人
- 主要关心当前队伍数据的人
- 想快速回看单场战斗结果的人

## 安装与使用

普通使用时，需要先在 Dalamud 中添加自定义仓库：

`https://raw.githubusercontent.com/anmili2022/MyDalamudRepo/main/pluginmaster.json`

然后安装 `DPS统计` 并从游戏内打开主窗口，再打开悬浮面板即可。

第一次使用建议：

1. 先打开悬浮面板
2. 先只保留 `DPS` 页
3. 没有战斗时先导入测试数据
4. 再按需要调整共享的页面列显示、列宽、行高和透明度
5. 如果已经调好统计页或历史页列宽，可以直接保持当前宽度，插件会自动记忆

## 当前限制

当前版本已经可以稳定用于查看基础战斗数据，但以下部分仍在持续完善：

- 更复杂的持续伤害统计
- 更复杂的持续治疗统计
- 个别更细的战斗补算逻辑

## 本地开发信息

- 最近一次已验证构建命令：
  `dotnet build E:\git\DalamudACT\DalamudACT.sln`
- 最近一次结果：`0 warnings` / `0 errors`
- 当前产物位置：`E:\git\DalamudACT\output\DalamudACT.dll`

## 开发常用外部接口文档

如果你不只是使用插件，而是要继续开发或排查代码，常用参考如下：

- Dalamud 文档首页：<https://dalamud.dev/>
- Dalamud API 参考：<https://dalamud.dev/api/>
- Lumina.Excel 仓库：<https://github.com/NotAdam/Lumina.Excel>

快速判断：

- 查 `PluginService`、窗口/UI、命令、状态、`IDataManager` 等接口时，优先看 Dalamud 文档 / API。
- 查 `GetExcelSheet<T>()`、`ExcelSheet<T>`、`Lumina.Excel.Sheets.*` 等表数据读取时，优先看 `Lumina.Excel`。

## 推荐继续阅读

- 详细使用说明：[`md/USAGE.md`](USAGE.md)
- 更新记录：[`md/CHANGELOG.md`](CHANGELOG.md)
- 维护交接入口：[`../HANDOVER.md`](../HANDOVER.md)
- 发布交接：[`2026-05-06-RELEASE-HANDOFF.md`](2026-05-06-RELEASE-HANDOFF.md)
