# DalamudACT 维护交接

更新时间：`2026-05-08`

用途：这是当前维护交接主文档，用来说明当前可信快照、归档状态、发布流程和接手阅读顺序。

相关维护文档：

- [维护首页（单页总览）](md/MAINTAINER-HOME.md)
- [维护文档总览图](md/MAINTAINER-DOC-MAP.md)
- [维护入口索引](md/MAINTAINER-INDEX.md)
- [下一位维护者第一小时清单](md/MAINTAINER-FIRST-HOUR-CHECKLIST.md)
- [最近问题与解决方案整理](md/RECENT-ISSUES-SUMMARY.md)
- [最近问题状态表（维护视角）](md/RECENT-ISSUES-STATUS-TABLE.md)
- [README.md](README.md)

## 目录 / TOC

- [当前快照](#handover-snapshot)
- [归档记录](#handover-archive)
- [归档：当时状态](#handover-archived-status)
- [归档：自动化状态](#handover-archived-automation)
- [归档：本次交接窗口的变更](#handover-archived-changes)
- [项目定位](#handover-direction)
- [版本范围](#handover-version-scope)
- [关键文件](#handover-key-files)
- [发布入口](#handover-release)
- [接手先看什么](#handover-reading-order)
- [备注](#handover-notes)

<a id="handover-snapshot"></a>
## 当前快照

- 工作目录：`E:\git\DalamudACT`
- 当前分支：`main`
- 当前 HEAD：`431aad4`
- 当前交接现场是脏工作区，接手前请先执行 `git status --short`，以当前输出为准。
- 当前这批未提交修改主要集中在：
  - `DalamudACT/Configuration/PluginConfiguration.cs` / `DalamudACT/UI/StatsPanel.cs`
    - 共享列显示合并
    - 统计页与历史页列宽写入配置文件
  - `DalamudACT/UI/MainWindow.cs` / `DalamudACT/UI/SettingsWindow.cs` / `DalamudACT/UI/FloatingStatsWindow.cs`
    - 主窗口与设置窗口卡片式 UI
    - 悬浮窗锁定、列配置摘要与交互补充
  - `DalamudACT/Stats/LocalStatsService.cs` / `DalamudACT/Plugin/ACT.cs` / `DalamudACT/DalamudApi.cs` / `DalamudACT/UI/StatsModels.cs`
    - 历史预览流、统计与显示联动的配套调整
  - `README.md` / `md/USAGE.md` / `md/README-SUMMARY.md` / `md/MAINTAINER-HOME.md`
    - 文档补充、共享列说明与外部接口文档入口
- 更早之前的战斗跟踪实验，已经在形成这个快照前回滚到干净基线。
- 最近一次已验证的本地构建：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - 结果：`0 warnings / 0 errors`
  - 产物：`E:\git\DalamudACT\output\DalamudACT.dll`

<a id="handover-archive"></a>
## 归档记录

下面的内容是较早的 `2026-05-06` 交接记录，保留作为参考。

<a id="handover-archived-status"></a>
## 归档：当时状态

- 工作目录：`E:\git\DalamudACT`
- 远端：`origin = https://github.com/anmili2022/DalamudACT`
- 主要维护分支：`master`
- 当时交接所在分支：`master`
- 最近一次已验证的自动化基线提交：`b1324c9`
- 验证上述自动化基线时，工作区是干净的
- 当时元数据版本：`0.15.2.5`
- 当时已验证的本地构建：
  - Debug：`dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Debug`
  - Release：`dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Release -p:Version=0.15.2.5 -p:FileVersion=0.15.2.5 -p:AssemblyVersion=0.15.2.5`
  - 结果：`0 warnings / 0 errors`
  - 产物：`E:\git\DalamudACT\output\DalamudACT.dll`

<a id="handover-archived-automation"></a>
## 归档：自动化状态

这个仓库是 `flyrio/DalamudACT` 的 fork。

截至 `2026-05-06`，这个 fork 上的 GitHub Actions 已经验证可用。

已验证的运行记录：

- `.github/workflows/build.yml`
  - 触发检查提交：`08c4c30`
  - 首次观察到的运行：`25427532514`
  - 初始结果：workflow 能正确触发，但在 `Archive` 阶段失败
  - 原因：workflow 仍在归档 `DalamudACT/bin/Release/*`，而项目当前产物已改到 `output/`
- `.github/workflows/build.yml` 修复后状态
  - 修复提交：`b1324c9`
  - 成功运行：`25427744876`
  - 结果：`Build`、`Archive`、`Upload Artifact`、`Update Latest Release` 全部成功
- `.github/workflows/release.yml`
  - 通过已有 tag `0.15.2.3` 的 `workflow_dispatch` 完成验证
  - 成功运行：`25427944539`
  - 结果：完整发布流程成功，包括 zip 打包和 GitHub Release 更新

实际结论：

- 分支构建流程已经恢复可用
- 正式发布 workflow 已经恢复可用
- 当前正式发版路径在正常场景下已不再依赖早期“只能手工兜底”的方案

<a id="handover-archived-changes"></a>
## 归档：本次交接窗口的变更

- 发布了 `0.15.2.5`
- 已确认 `repo.json`、manifest 和程序集版本都已同步到 `0.15.2.5`
- 在设置中新增了悬浮窗锁定选项
- 锁定后悬浮窗不能移动或缩放
- 锁定后表格列宽拖动手柄会被禁用，从而保留用户当前列宽
- 除窗口设置和数据/状态外，其他设置分组默认折叠
- 合成团本测试数据已改为 8 人的 `零式测试场` 样本
- 调整了悬浮窗折叠/展开行为，使其统一使用紧凑 DPS 页签状态
- 修复了已有战斗快照时仍可能显示 `等待战斗数据...` 的 UI 判断问题
- 已确认本地 `Release` 打包路径仍然是 `output/`
- 修复 `.github/workflows/build.yml`，改为从 `output/` 归档，而不是旧的 `bin/Release`
- 已验证 `.github/workflows/release.yml` 可以成功重建并发布 tag `0.15.2.3`

<a id="handover-direction"></a>
## 项目定位

`DalamudACT` 已经不再是一个包着外部 overlay 的薄外壳。

当前项目方向是：

- 在 Dalamud 内部采集战斗事件
- 在插件内部计算本地 ACTX 风格的战斗统计
- 在游戏内直接渲染 DPS、HPS、承伤、概览和历史记录
- 在这个仓库中继续维护 UI 与发布产物

<a id="handover-version-scope"></a>
## 版本范围

当前仓库中最近一次提交的元数据版本是 `0.15.2.5`。

另外，当前工作区还有一批**尚未发版、但已本地构建验证通过**的改动，重点如下：

- 配置版本已提升到 `23`
- `DPS / HPS / 承伤` 三页已改为共享一组列显示设置
  - 玩家列
  - 职业列
  - 伤害列
  - 秒伤列
  - 死亡列
  - 显示人数
- 其中：
  - `伤害列` 分别对应 `DPS 伤害量 / HPS 治疗量 / 承伤 承伤量`
  - `秒伤列` 分别对应 `DPS 秒伤 / HPS 秒疗 / 承伤 秒承伤`
- 切换列显示时不会重置用户当前列宽
- 统计页与历史页列宽会写入配置文件，并在下次打开插件时恢复
- 设置页可一键重置统计页 / 历史页的列宽记忆
- 主窗口与设置窗口已重构为卡片式 UI
- 主窗口新增“界面与列配置摘要”卡片
- `README.md`、`md/USAGE.md`、维护文档已补充以上行为说明与外部接口文档入口

仓库里已经体现出的近期改动如下：

- `0.15.2.7`
  - fixed stats table width memory and hidden-column layout behavior
  - changed hidden stats columns to hide the whole column
  - enforced a `20px` minimum width for the deaths column
  - settings window title now shows the plugin version
  - historical preview can automatically return to live DPS after timeout

- `0.15.2.5`
  - 在窗口设置下新增悬浮窗锁定选项
  - 锁定后的悬浮窗不能移动或缩放
  - 锁定时保留当前表格列宽，并禁用表头拖拽
  - 除窗口设置和数据/状态外，设置分组默认折叠
  - `零式测试场` 合成测试数据扩展为 8 人样本

- `0.15.2.4`
  - 悬浮窗折叠态统一到紧凑 DPS 页签流程
  - 点击紧凑 DPS 页签会直接展开到实时数据
  - 只要已有战斗快照，就会展示统计，而不再卡在等待文本
  - README 新增维护交接入口链接
- `0.15.2.3`
  - 仓库根目录新增维护交接入口
  - README 增加面向维护者的直达入口
  - 分支构建 workflow 修复为从 `output/` 归档
  - 该 fork 上的 GitHub Actions 分支构建和正式发布流程均已验证可用
- `0.15.2.2`
  - 插件加载时悬浮窗默认折叠
  - 悬浮窗默认展开尺寸调整为 `300x300`
  - 点击等待文本可切换折叠 / 展开
  - 发布 workflow 加固为可正确读取 UTF-8 发布说明
- `0.15.2.1`
  - 修复 NPC 和信赖队友的跟踪问题
  - 改进历史记录与实时视图的切换
  - 主窗口新增版本显示
- `0.15.2.0`
  - 设置分组拆分与战斗结束判定配置
  - 历史记录导入 / 导出与相关 UI 改进

完整发布历史见 [md/CHANGELOG.md](md/CHANGELOG.md)。

<a id="handover-key-files"></a>
## 关键文件

构建与元数据：

- `DalamudACT/DalamudACT.csproj`
- `DalamudACT/DalamudACT.json`
- `Data/DalamudACT.json`
- `repo.json`

主要运行时与 UI 区域：

- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/DalamudApi.cs`
- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/UI/MainWindow.cs`
- `DalamudACT/UI/FloatingStatsWindow.cs`
- `DalamudACT/UI/StatsPanel.cs`
- `DalamudACT/UI/SettingsWindow.cs`

开发时常用的外部接口文档：

- Dalamud 文档首页：<https://dalamud.dev/>
- Dalamud API 参考：<https://dalamud.dev/api/>
- Lumina.Excel 仓库：<https://github.com/NotAdam/Lumina.Excel>

快速判断：

- 查 `PluginService`、窗口/UI、命令、状态、`IDataManager` 等接口时，优先看 Dalamud 文档 / API。
- 查 `GetExcelSheet<T>()`、`ExcelSheet<T>`、`Lumina.Excel.Sheets.*` 等 Excel 数据读取时，优先看 `Lumina.Excel`。

发布自动化：

- `.github/workflows/release.yml`
- `.github/workflows/test_release.yml`
- `.github/workflows/build.yml`

当前自动化说明：

- `build.yml` 当前打包的文件为 `output/DalamudACT.dll`、`output/DalamudACT.json` 和 `output/DalamudACT.deps.json`
- `release.yml` 已在这个 fork 启用 Actions 后完成成功验证

<a id="handover-release"></a>
## 发布入口

正式发布流程：

1. 更新代码与文档。
2. 将以下文件中的版本全部同步一致：
   - `DalamudACT/DalamudACT.csproj`
   - `DalamudACT/DalamudACT.json`
   - `Data/DalamudACT.json`
   - `repo.json`
   - `md/CHANGELOG.md`
   - `md/RELEASE-NOTES.md`
3. 运行 `dotnet build E:\git\DalamudACT\DalamudACT.sln`
4. 推送 `master`
5. 创建并推送正式版本 tag
6. 让 `.github/workflows/release.yml` 自动创建 GitHub Release

已有 tag 的重新发布流程：

当某个正式版本 tag 已经存在于 GitHub，但你仍需要 GitHub Actions 重新构建或重新发布这个同名 tag 时，使用这一流程。

1. 打开 GitHub Actions 中的 `Create Release` workflow。
2. 点击 `Run workflow`。
3. 在 `tag` 输入框中填入已有的正式 tag，例如 `0.15.2.6`。
4. 让 `.github/workflows/release.yml` 重新构建并上传 `DalamudACT.zip`。
5. 确认该 tag 的 Release 页面已经出现预期资产。

重要说明：

- 如果 workflow 文件自那次失败运行之后已经改过，不要对旧失败 release 直接使用 `Re-run jobs`
- GitHub 重跑旧任务时，会继续沿用原始的 `GITHUB_SHA` 和 `GITHUB_REF`
- 如果旧任务引用的是过期 workflow 快照，那么重跑仍会执行同样过期的打包逻辑
- 拿不准时，优先使用 `workflow_dispatch` 并传入准确的既有 tag

普通补丁版本的快速发布流程：

适用场景：

- 发布自动化本身已经健康
- 这次只是正常版本升级，而不是修 workflow
- 你只需要从本地改动走最短可信路径发布到 GitHub Release

1. 先确定目标版本，例如 `0.15.2.6`。
2. 将以下文件统一更新到这个精确版本：
   - `DalamudACT/DalamudACT.csproj`
   - `DalamudACT/DalamudACT.json`
   - `Data/DalamudACT.json`
   - `repo.json`
   - `md/CHANGELOG.md`
   - `md/RELEASE-NOTES.md`
3. 在本地运行 release 构建：

```powershell
$ver = "0.15.2.6"
dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Release -p:Version=$ver -p:FileVersion=$ver -p:AssemblyVersion=$ver
```

4. 提交并推送 `master`：

```powershell
git -C E:\git\DalamudACT status --short
git -C E:\git\DalamudACT add .
git -C E:\git\DalamudACT commit -m "chore: release $ver"
git -C E:\git\DalamudACT push origin master
```

5. 创建并推送发布 tag：

```powershell
git -C E:\git\DalamudACT -c tag.gpgSign=false tag -a $ver -m "DalamudACT $ver"
git -C E:\git\DalamudACT push origin $ver
```

6. 确认 GitHub 已创建 Release，并挂上 `DalamudACT.zip`。

测试发布流程：

这个流程只用于 `testing_*` tag，不用于正式发布。

1. 确认目标提交上的 `.github/workflows/test_release.yml` 已经是“从 `output/` 打包”的当前版本。
2. 从目标提交创建一个测试 tag，例如 `testing_0.15.2.6`。
3. 推送该测试 tag。
4. 让 `.github/workflows/test_release.yml` 构建插件、生成测试 release zip，并更新 `repo.json` 中的 testing 字段。
5. 验证测试 release 页面，并确认 `repo.json` 中的 testing 下载链接指向同一个 `testing_*` tag。

快速提醒：

- 正式发布不要使用 `testing_*`
- 不要使用 `latest`
- 不要漏掉 `repo.json`
- 如果签名会卡住，这台机器上不要直接用普通 `git tag`
- tag 名必须与发布版本号完全一致

当前已验证状态：

- 分支构建流程：已验证
- 通过已有 tag + `workflow_dispatch` 的正式发布流程：已验证
- runbook 中仍可保留人工兜底发布方案，但它已不再是唯一可信路径

重要 workflow 角色：

- `.github/workflows/release.yml`：按正式版本 tag 发版
- `.github/workflows/test_release.yml`：仅用于 testing tag 流程
- `.github/workflows/build.yml`：分支 / 类 nightly 构建流程

这台机器曾经出现过必须关闭 tag 签名才能继续发版的情况。如果 tag 签名阻塞发布，请看 [md/RELEASE-RUNBOOK.md](md/RELEASE-RUNBOOK.md) 和 [md/2026-05-06-RELEASE-HANDOFF.md](md/2026-05-06-RELEASE-HANDOFF.md)。

<a id="handover-reading-order"></a>
## 接手先看什么

如果你要接手维护，建议按下面顺序阅读：

1. [HANDOVER.md](HANDOVER.md)
2. [维护首页（单页总览）](md/MAINTAINER-HOME.md)
3. [维护入口索引](md/MAINTAINER-INDEX.md)
4. [下一位维护者第一小时清单](md/MAINTAINER-FIRST-HOUR-CHECKLIST.md)
5. [最近问题与解决方案整理](md/RECENT-ISSUES-SUMMARY.md)
6. [最近问题状态表（维护视角）](md/RECENT-ISSUES-STATUS-TABLE.md)
7. [2026-05-06 发布交接](md/2026-05-06-RELEASE-HANDOFF.md)
8. [发布 Runbook](md/RELEASE-RUNBOOK.md)
9. [SESSION-HANDOFF](md/SESSION-HANDOFF.md)
10. [更新记录](md/CHANGELOG.md)

如果你在排查运行时行为，优先看：

1. `DalamudACT/Stats/LocalStatsService.cs`
2. `DalamudACT/Plugin/ACT.cs`
3. `DalamudACT/DalamudApi.cs`
4. `DalamudACT/UI/FloatingStatsWindow.cs`
5. `DalamudACT/UI/StatsPanel.cs`

<a id="handover-notes"></a>
## 备注

- 创建较早这份交接记录时，仓库曾经是干净的。
- 当前可信的本地产物路径是 `output\DalamudACT.dll`。
- 当前可信的分支构建修复位于提交 `b1324c9`。
- 首个成功的分支构建运行号是 `25427744876`。
- 已验证成功的正式发布 workflow 运行号是 `25427944539`。
- 更细的逐日记录已经保存在 `md/` 目录下；本文件的定位是维护入口摘要，而不是替代那些详细记录。
