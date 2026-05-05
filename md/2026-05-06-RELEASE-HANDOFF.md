# 2026-05-06 Release Handoff

## 本次发布目标

- 发布版本：`0.15.2.1`
- 仓库：`https://github.com/anmili2022/DalamudACT`
- 主分支：`master`
- 正式发布 workflow：`.github/workflows/release.yml`

## 这次版本包含什么

### 1. NPC 队友统计修复

- 统计链路不再只依赖 `PartyList`
- 新增按游戏队伍槽位 `<1> .. <8>` 解析队伍成员，和 `AEAssist` 的 `PartyManager` 思路对齐
- 信赖 / 小队 / NPC 队友只要实际占了队伍槽位，就会进入统计、死亡检测和脱战判断
- 仍然保留 `PartyList` / `BuddyList` / 对象表匹配作为补充兜底

相关文件：

- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/DalamudApi.cs`

### 2. 历史记录与实时切换体验修复

- 战斗结束后，如果悬浮窗当前停在 `历史记录`，会自动切回实时页签
- 历史记录导入后会自动打开最新一条，避免界面一直停在“等待战斗数据...”
- 历史记录页新增 `清空历史` 按钮，和设置页中的同名按钮保持一致
- 没有实时战斗数据但已经有历史记录时，界面会提示可以直接查看历史

相关文件：

- `DalamudACT/UI/FloatingStatsWindow.cs`
- `DalamudACT/UI/StatsPanel.cs`
- `DalamudACT/Stats/LocalStatsService.cs`

### 3. 主窗口补充版本号

- 主窗口标题区显示插件版本
- 概览表格中也显示版本字段，方便确认当前加载的包

相关文件：

- `DalamudACT/UI/MainWindow.cs`

### 4. 历史记录保存策略

- 战斗时长小于 `30` 秒时不写入历史记录
- 导出历史记录后会保持当前展示状态一致

相关文件：

- `DalamudACT/Stats/LocalStatsService.cs`

## 本次发布前必须知道的文件

- 版本号主入口：`DalamudACT/DalamudACT.csproj`
- 插件 manifest：
  - `DalamudACT/DalamudACT.json`
  - `Data/DalamudACT.json`
- 仓库源与下载链接：`repo.json`
- 正式发布 workflow：`.github/workflows/release.yml`
- 测试 tag workflow：`.github/workflows/test_release.yml`
- nightly/latest workflow：`.github/workflows/build.yml`

## 以后发版不要再试验的固定流程

详细 runbook 见：

- [RELEASE-RUNBOOK.md](RELEASE-RUNBOOK.md)

这次实测可用的顺序就是：

1. 改代码和文档
2. 本地执行 `dotnet build E:\git\DalamudACT\DalamudACT.sln`
3. 更新以下版本引用到同一个版本号
   - `DalamudACT/DalamudACT.csproj`
   - `DalamudACT/DalamudACT.json`
   - `Data/DalamudACT.json`
   - `repo.json`
   - `md/CHANGELOG.md`
   - `md/RELEASE-NOTES.md`
4. 提交并推送 `master`
5. 创建并推送正式 tag，例如 `0.15.2.1`
6. 由 `.github/workflows/release.yml` 自动构建并创建 GitHub Release

## 哪个 workflow 才是正式发版入口

### 正式发版

- 文件：`.github/workflows/release.yml`
- 触发方式：推送普通 tag，例如 `0.15.2.1`
- 产物：`DalamudACT.zip`
- 行为：
  - 拉代码
  - 下载 Dalamud 依赖
  - 按 tag 版本构建 Release
  - 回写输出目录中的 manifest 版本
  - 打包 `dll/json/deps`
  - 自动创建 GitHub Release

### 测试发版

- 文件：`.github/workflows/test_release.yml`
- 触发方式：推送 `testing_*` tag
- 用途：测试 tag 流程，不是正式版本入口

### nightly/latest

- 文件：`.github/workflows/build.yml`
- 用途：分支构建与 `latest` 发布
- 不是本项目当前正式插件仓库发版链路

## 本地验证

已验证通过：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

结果：

- `0 warnings`
- `0 errors`

## 发布后检查项

1. 确认 GitHub 上存在 tag `0.15.2.1`
2. 确认 Release 名称为 `DalamudACT 0.15.2.1`
3. 确认附件里有 `DalamudACT.zip`
4. 确认 `repo.json` 三个下载链接都指向同一个 tag
5. 在游戏内确认主窗口版本号显示为 `0.15.2.1`

## 接手时优先看什么

1. 本文档 `md/2026-05-06-RELEASE-HANDOFF.md`
2. [RELEASE-RUNBOOK.md](RELEASE-RUNBOOK.md)
3. [CHANGELOG.md](CHANGELOG.md)
4. [RELEASE-NOTES.md](RELEASE-NOTES.md)
5. `DalamudACT/Stats/LocalStatsService.cs`
