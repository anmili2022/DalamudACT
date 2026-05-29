# TimelineDraftTool

带 UI 的 ACT 网络日志时间轴草稿生成器，不需要启动游戏，也不依赖 Dalamud。

## 用法

1. 运行 `TimelineDraftTool.exe`。
2. 选择 ACT `Network*.log`。
3. 点击 `刷新战斗列表`。
4. 在列表里选择一场战斗。
5. 点击 `生成时间轴草稿`。
6. 如需让插件加载该草稿，点击 `选择草稿` 后点击 `转正草稿`。

可选点击 `刷新额外资源` 下载 AEAssist 的 `AoeActions.json` 和 `TankDeathSentence.json`，草稿会用它们标注 `# AOE` / `# 死刑`。

默认草稿输出到 `TimelineDraftTool.exe` 所在目录。

`转正草稿` 会把草稿复制到用户配置时间轴目录的 `generated` 子目录，并更新 `timeline-index.json`。默认目录为：

`%APPDATA%\XIVLauncherCN\pluginConfigs\DalamudACT\Timeline\Data`
