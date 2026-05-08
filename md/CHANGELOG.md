# DPS统计 更新记录

说明：

- 当前包元数据版本为 `0.15.2.8`
- 未发布改动会先写在 `未发布` 部分
- 更早的开发背景可继续查看：
  - `md/SESSION-HANDOFF.md`
  - `md/2026-05-05.md`
  - `md/2026-05-06.md`

## Unreleased
### 本轮稳定性修复与版本统一

- 移除对 `PronounModule.ResolvePlaceholder("<1>..<8>")` 的直接依赖，避免运行时签名变化导致的崩溃。
- 将 `ActionEffect` 来源识别改为 `sourceId + sourceCharacter` 双路径回表，并与 `LocalStatsService` 的统一身份模型对齐。
- 统一插件程序集版本与元数据版本到 `0.15.2.8`，避免插件窗口与插件管理界面显示不同版本号。
- 本地实测确认：进入战斗后可以恢复正常出数。

- release notes template now replaces the `{{VERSION}}` placeholder with the actual tag version during formal releases
- repository release-notes template was refreshed to avoid reusing stale version text in future GitHub Releases

Current metadata version: `0.15.2.8`

## 0.15.2.8 - 2026-05-09

### Floating stats table columns

- fixed column-width persistence so widths are stored by semantic slot instead of current visual order
- hiding a stats column now hides the entire column, not just cell content
- the share column now stretches to fill remaining width after fixed columns are shown or hidden
- enforced a `20px` minimum width for the deaths column

### UI and interaction

- settings window title now shows the loaded plugin assembly version
- main window keeps version text visible for build verification during testing
- historical record preview now supports automatic return to live DPS after the configured timeout
- the floating window keeps the last encounter visible after combat ends, then clears when the next combat actually begins
- status text and empty-state hints now distinguish between "waiting for the next combat" and "collecting fresh combat data"

### Release and maintenance

- `0.15.2.7` formal tag release completed successfully
- the `latest` build/release automation path was re-verified successfully
- release-notes maintenance was improved so future formal releases do not reuse stale version bodies

## 0.15.2.5 - 2026-05-06

### Floating window lock

- added a floating window lock option under window settings
- locking prevents moving or resizing the floating window itself
- metric and history table headers are disabled while locked, so the current user-adjusted widths stay in place and can no longer be dragged

### Settings defaults

- all settings sections now default collapsed except for window settings and data/status

### Test data

- the synthetic `零式测试场` sample now contains eight characters for full-party testing

### Documentation

- refreshed release notes and release handoff for `0.15.2.5`
- updated the README handoff link to point at the current release handoff entry

## 0.15.2.4 - 2026-05-06

### 悬浮窗

- 折叠态统一为缩小窗口显示 `DPS` 页签，不再走单独的等待文案入口
- 点击折叠态 `DPS` 页签后会直接展开窗口并显示当前数据

### 显示判定

- 只要已经生成 encounter 快照，就允许统计面板显示数据
- 修复已经收到战斗快照时，界面仍停留在 `等待战斗数据...` 的问题

### 文档

- `README.md` 增加交接入口链接，便于直接跳转到 handoff 文档

## 0.15.2.3 - 2026-05-06

### 发布流程

- 补强正式发布 workflow，对 tag 触发匹配与发布说明读取流程继续做收口
- 让 GitHub Release 的正文读取与打包流程更稳定，减少因编码或触发条件导致的空发布风险

### 维护交接

- 新增仓库根目录 `HANDOVER.md` 作为维护交接入口
- `README.md` 增加交接文档入口，方便后续直接进入 runbook 和 release handoff

### 说明

- 本版本主要是发布基础设施和维护文档整理
- 插件运行时功能与 `0.15.2.2` 保持一致

## 0.15.2.2 - 2026-05-06

### 悬浮窗交互

- 悬浮窗默认展开尺寸调整为 `300x300`
- 插件加载后默认以折叠态显示，不再先展示页签栏
- 折叠态保留 `等待战斗数据...` 文本，并与展开态使用同一位置
- 点击 `等待战斗数据...` 可在展开态与无页签折叠态之间切换
- 右键折叠态提示文本仍可直接打开设置窗口

### 发布流程

- 正式发布 workflow 改为显式读取 UTF-8 发布说明文件
- GitHub Release 会自动带上发布说明正文，避免再次出现问号或空白正文

## 0.15.2.1 - 2026-05-06

### 统计修复

- 修复与 NPC 队友、信赖、小队成员战斗时仍然不出统计的问题
- 统计队友时新增按游戏队伍槽位 `<1> .. <8>` 解析成员，和 AEAssist 的队伍解析思路对齐
- 仍保留 `PartyList`、`BuddyList`、对象表匹配作为补充兜底
- NPC 队友现在会进入：
  - 伤害 / 治疗统计
  - 死亡检测
  - 脱战结束判定

### 历史记录与实时视图

- 战斗结束后不再停留在 `历史记录` 标签页
- 导入历史记录后自动打开最新一条，避免界面一直显示“等待战斗数据...”
- 历史记录页新增 `清空历史` 按钮
- 没有实时战斗数据但已有历史记录时，会明确提示可以查看历史

### 其他

- 主窗口显示插件版本号
- 小于 `30` 秒的战斗不写入历史记录

## 0.15.2.0 - 2026-05-06

### 设置与战斗结束判定

- 设置窗口拆分为：
  - `窗口设置`
  - `战斗结束设置`
- 战斗结束判定支持：
  - `全队脱战（PartyList）即为战斗结束`
  - `全队脱战，且延迟 X 秒为战斗结束`

### 历史记录

- 历史记录新增开始时间、结束时间、时长
- 历史记录支持导入 / 导出
- 导入导出文件固定为插件配置目录中的 `history-records.json`
- 历史记录表格支持：
  - 滚动
  - 拖动列宽
  - 悬停查看完整单元格内容

### 面板体验

- `概览` 页面增加滚动条
- `数据与状态` 折叠栏加入历史导入 / 导出入口
- manifest 改为中英双语 `Punchline / Description`
- 补齐 `Tags`

## 0.15.1.0 - 2026-05-06

- `DPS` 页面新增 `伤害量` 列
- 历史记录支持点击回看整场战斗快照
- 悬浮面板 `DPS` 页签支持折叠 / 展开与右键打开设置
- 主窗口入口注册修复
- 使用说明、更新记录、发布说明完成重写

## 0.15.0.0 - 2026-05-05

- 插件定位调整为独立运行的 Dalamud DPS 统计插件
- 下线旧版依赖外部 `MiniParse` 的统计链路
- 改为在游戏内直接采集战斗事件并生成本地统计结果
- UI 与输出口径整体向 `ACTX / NotACT` 风格对齐
