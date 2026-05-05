# 2026-05-06 Release Handoff

## 目标版本

- 本次发布版本：`0.15.2.0`
- 仓库：`https://github.com/anmili2022/DalamudACT`
- 主分支：`master`

## 这次发布包含什么

### 1. 设置与战斗结束判定

- 设置窗口将原来的 `窗口与遭遇` 拆成：
  - `窗口设置`
  - `战斗结束设置`
- 战斗结束判定支持两种模式：
  - `全队脱战（PartyList）即为战斗结束`
  - `全队脱战，且延迟 X 秒为战斗结束`
- 主窗口会显示当前使用中的战斗结束判定方式

### 2. 历史记录增强

- 历史记录新增：
  - `开始时间`
  - `结束时间`
  - `时长`
- 历史记录支持：
  - 导入
  - 导出
  - 纵向滚动
  - 拖动列宽
  - 悬停显示单元格完整内容
- 导入导出文件固定为插件配置目录下的 `history-records.json`
- `数据与状态` 折叠栏也补上了同一套导入 / 导出入口

### 3. 其他界面体验

- `概览` 页面新增滚动条
- manifest 改成中英双语 `Punchline / Description`
- 清单补齐 `Tags`

### 4. 发布链路整理

- 版本号统一提升到 `0.15.2.0`
- `RepoUrl` 统一切回当前实际仓库 `anmili2022/DalamudACT`
- `repo.json` 下载链接改为 GitHub Release 附件地址
- `.github/workflows/release.yml` 改成更简单、稳定的 tag 构建链路：
  - 清理 `output`
  - 按 tag 版本构建 Release
  - 修正输出 manifest 版本号
  - 只打包 `dll / json / deps`
  - 自动创建 GitHub Release 并上传 `DalamudACT.zip`

## 这次重点修改的文件

- `DalamudACT/Configuration/PluginConfiguration.cs`
- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/UI/SettingsWindow.cs`
- `DalamudACT/UI/MainWindow.cs`
- `DalamudACT/UI/StatsModels.cs`
- `DalamudACT/UI/StatsPanel.cs`
- `DalamudACT/DalamudACT.csproj`
- `DalamudACT/DalamudACT.json`
- `Data/DalamudACT.json`
- `repo.json`
- `.github/workflows/release.yml`
- `README.md`
- `md/USAGE.md`
- `md/CHANGELOG.md`
- `md/RELEASE-NOTES.md`

## 本地验证

已验证：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Release -p:Version=0.15.2.0 -p:FileVersion=0.15.2.0 -p:AssemblyVersion=0.15.2.0
```

结果：

- `0 warnings`
- `0 errors`

## 发布方式

本次按以下顺序发布：

1. 提交当前工作区
2. 推送 `master`
3. 创建并推送 tag：`0.15.2.0`
4. 由 GitHub Actions 根据 tag 自动创建 Release

## 接手时优先看什么

建议按这个顺序看：

1. 本文档 `md/2026-05-06-RELEASE-HANDOFF.md`
2. [README.md](../README.md)
3. [更新记录](CHANGELOG.md)
4. [发布说明](RELEASE-NOTES.md)
5. [使用说明](USAGE.md)

## 仍需注意的点

- Release 依赖 GitHub Actions 自动构建；如果 tag 推送后没有产出 release，优先检查：
  - `.github/workflows/release.yml`
  - Actions 页的 tag 构建日志
- 当前 `repo.json` 已经直接指向 GitHub Release 附件，因此 tag 和 release 名称必须与版本号保持一致
