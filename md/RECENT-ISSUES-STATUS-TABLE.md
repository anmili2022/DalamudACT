# 最近问题状态表（维护视角）

更新时间：`2026-05-08`

用途：给继续接手维护的人快速判断哪些问题**已解决**、哪些还需要**实测验证**、哪些仍然**未完成**。

相关维护文档：

- [维护首页（单页总览）](MAINTAINER-HOME.md)
- [维护文档总览图](MAINTAINER-DOC-MAP.md)
- [维护入口索引](MAINTAINER-INDEX.md)
- [下一位维护者第一小时清单](MAINTAINER-FIRST-HOUR-CHECKLIST.md)
- [最近问题与解决方案整理](RECENT-ISSUES-SUMMARY.md)
- [HANDOVER.md](../HANDOVER.md)

---

## 已解决

| 问题 / 事项 | 处理结果 | 关键文件 / 备注 |
|---|---|---|
| `WindowSystem.AddWindow(...)` 签名不兼容 | 已改为反射注册窗口，避免因旧签名绑定导致加载阶段崩溃 | `DalamudACT/UI/PluginUI.cs` |
| 旧版 `ISigScanner / SigScanner` 直注失败 | 已改为注入 `IGameInteropProvider`，再通过反射取 scanner，并统一走 `DalamudApi.ScanText(...)` | `DalamudACT/DalamudApi.cs` |
| `IClientState.TerritoryType` 缺失导致 `Framework.Update` 周期持续异常 | 已增加 `GetTerritoryTypeId()` / `GetLocalPlayerName()` 兼容入口；移除直接调用；补上异常防护 | `DalamudACT/DalamudApi.cs`、`DalamudACT/Plugin/ACT.cs` |
| 插件仍依赖外部 `MiniParse` 才能工作 | 已完成方向切换为“本地采集 + 本地统计 + 本地 UI 展示” | `DalamudACT/Stats/LocalStatsService.cs`、`DalamudACT/Plugin/ACT.cs` |
| DPS 统计范围过宽，可能误算队外对象 | 已收口到“自己 + 当前队伍队友”；宠物/召唤物通过 `OwnerId` 归属 | `DalamudACT/Stats/LocalStatsService.cs` |
| 禁用旧 Hook 后死亡统计链路缺失 | 已改为轮询 `PartyList` 的 HP 变化统计死亡；总计行也可显示全队死亡总数 | `DalamudACT/Stats/LocalStatsService.cs`、`DalamudACT/UI/StatsPanel.cs` |
| 历史记录只能展示条目，不能回看快照 | 已为每条历史记录保存 `CombatDataWrapper` 快照；点击后可切到历史数据回看 | `DalamudACT/UI/StatsModels.cs`、`DalamudACT/UI/StatsPanel.cs`、`DalamudACT/Stats/LocalStatsService.cs` |
| 已有快照时界面仍显示“等待战斗数据...” | 已调整 UI gating，只要存在 combat snapshot 就显示数据 | `DalamudACT/UI/StatsPanel.cs` 等相关 UI 逻辑 |
| 导入测试数据会清空历史 | 已改为保留已有历史，再追加测试快照 | `DalamudACT/Stats/LocalStatsService.cs` |
| 多次导入测试数据会重复堆积 | 已按“区域 + 时长”覆盖更新同类测试记录，避免无限重复 | `DalamudACT/Stats/LocalStatsService.cs` |
| 测试样本过于单一 | 已拆成多组不同快照，后续又扩展为 8 人样本 | `DalamudACT/Stats/LocalStatsService.cs` |
| 悬浮窗页签交互不完整 | 已补齐左键折叠/展开、右键开关设置窗口 | `DalamudACT/UI/FloatingStatsWindow.cs`、`DalamudACT/UI/PluginUI.cs`、`DalamudACT/UI/StatsPanel.cs` |
| 悬浮窗容易误移动/误缩放/误改列宽 | 已新增 `锁定悬浮窗口`；锁定后不可移动、缩放，表头拖拽禁用 | `DalamudACT/UI/SettingsWindow.cs`、`DalamudACT/UI/StatsPanel.cs`、相关配置文件 |
| 悬浮表格配置项不足 | 已新增玩家列最小宽度、固定列宽、表格行高等设置 | `DalamudACT/Configuration/PluginConfiguration.cs`、`DalamudACT/UI/SettingsWindow.cs` |
| `DPS` 页缺少 `伤害量` 列 | 已新增可配置显示/隐藏的 `伤害量` 列，总计行与 tooltip 同步补齐 | `DalamudACT/UI/StatsPanel.cs`、`DalamudACT/UI/SettingsWindow.cs` |
| `DPS / HPS / 承伤` 三页列显示分散、设置语义不统一 | 已合并为共享的 `页面列显示`；统一控制 `玩家 / 职业 / 伤害 / 秒伤 / 死亡 / 显示人数`，并明确三个页签中的列语义 | `DalamudACT/Configuration/PluginConfiguration.cs`、`DalamudACT/UI/SettingsWindow.cs`、`DalamudACT/UI/StatsPanel.cs` |
| 切换列显示会打乱列宽，重开插件后布局无法恢复 | 已将统计页与历史页列宽写入配置文件；切换列显示不再重置当前列宽，并提供重置列宽记忆入口 | `DalamudACT/Configuration/PluginConfiguration.cs`、`DalamudACT/UI/SettingsWindow.cs`、`DalamudACT/UI/StatsPanel.cs` |
| 主窗口与设置窗口信息层级不清晰 | 已重构为卡片式 UI；主窗口新增 `界面与列配置摘要`，设置窗口补充 `历史记录预览时长（秒）` 与列宽记忆说明 | `DalamudACT/UI/MainWindow.cs`、`DalamudACT/UI/SettingsWindow.cs` |
| 数值显示仍是 `K / M / B` | 已统一改为 `万 / 亿 / 兆` 中文单位 | `DalamudACT/Stats/LocalStatsService.cs` |
| 未注册主 UI 回调导致校验提示 | 已注册 `UiBuilder.OpenMainUi` | `DalamudACT/Plugin/ACT.cs`、`DalamudACT/UI/PluginUI.cs` |
| GitHub Actions 仍按旧路径 `bin/Release/*` 打包 | 已修正为从 `output/` 打包，分支构建已验证恢复 | `.github/workflows/build.yml` |
| 旧失败 release 任务直接重跑会继续使用旧 workflow 快照 | 已明确改用 `workflow_dispatch` 重建既有 tag 的 release | `HANDOVER.md`、`md/2026-05-06-RELEASE-HANDOFF.md` |
| 发版时 `git tag` 可能被 GPG 签名卡住 | 已固定使用 `-c tag.gpgSign=false tag -a ...` | `md/2026-05-06-RELEASE-HANDOFF.md` |
| 版本号分散在多个文件，容易不同步 | 已明确必须同步更新的文件清单 | `HANDOVER.md`、`md/2026-05-06-RELEASE-HANDOFF.md` |

---

## 待验证

| 项目 | 当前情况 | 建议验证方式 |
|---|---|---|
| 当前 `output\\DalamudACT.dll` 的长期稳定性 | 交接记录确认“已能构建、已做兼容修复”，但还缺少更长时间用户侧实测 | 在真实游戏环境中连续启用插件、切图、进出战斗并观察日志 |
| 实时统计链路在实际战斗中的持续稳定性 | 当前核心链路基于 `ActionEffect`，方向已定，但还需更多实战确认 | 连续多场战斗验证 DPS / HPS / 承伤是否持续稳定出数 |
| 历史记录点击回看 → 新战斗自动切回实时数据 | 逻辑已实现，交接建议明确要求重点实测 | 按交接建议走完整闭环：导入/产生历史 → 点击回看 → 新战斗开始 |
| 多次导入测试数据后的历史稳定性 | 逻辑已实现，仍建议反复实测是否有重复堆积或异常刷新 | 多次执行 `导入测试数据`，观察历史记录是否稳定、是否正确覆盖 |
| 共享列显示在三页间的联动语义 | 逻辑已实现，但仍需继续确认用户理解成本与边界行为 | 分别切换 `玩家 / 职业 / 伤害 / 秒伤 / 死亡 / 显示人数`，观察 `DPS / HPS / 承伤` 是否按预期同步变化 |
| 统计页 / 历史页列宽记忆与恢复 | 功能已做完，但需要继续观察保存、恢复、重置时机是否符合预期 | 手动拖拽列宽 → 切换列显示 → 关闭并重新打开插件 → 使用重置列宽记忆按钮逐项验证 |
| 悬浮窗锁定与交互体验 | 功能已做完，但需要继续观察实用性和边界行为 | 实测拖动、缩放、切页、改列宽、锁定/解锁切换 |
| 主窗口 / 设置窗口卡片式 UI 的易用性 | 结构已重构，但仍需继续观察信息密度与发现路径是否合理 | 结合真实使用流程验证：打开主窗口 → 打开设置 → 修改列显示 / 预览时长 / 锁定状态，确认是否容易找到 |

---

## 未完成

| 未完成项 | 当前状态 | 建议后续顺序 |
|---|---|---|
| `Cast` Hook 恢复 | 当前仍处于禁用状态 | 在确认 `ActionEffect` 主链路稳定后，先单独恢复并单独测试 |
| `ActorControlSelf` Hook 恢复 | 当前仍处于禁用状态 | 在 `Cast` 验证通过后，再单独恢复并重新校验签名与调用约定 |
| DoT 归因补齐 | 已落地静态白名单表 | 继续补漏项并观察是否还需要更细的状态映射 |
| HoT 归因补齐 | 当前仍未展开 | 与 DoT 分开处理，先不要混在同一轮恢复里 |
| Death 链路进一步补齐 | 当前已有兼容替代实现，但仍不是最终完整形态 | 在稳定基础上再补更细粒度逻辑 |
| 遭遇生命周期补强逻辑 | 一些依赖旧 Hook 的补强逻辑仍关闭 | 最后再处理，避免过早引入稳定性风险 |

---

## 维护提醒

| 提醒项 | 说明 |
|---|---|
| 当前工作区状态 | `HANDOVER.md` 提到当前工作区是脏工作区；当前未提交修改除历史预览流外，还包含共享列显示、列宽持久化、卡片式 UI 与文档补充 |
| 构建基线 | 最近验证通过的产物路径是 `E:\\git\\DalamudACT\\output\\DalamudACT.dll` |
| DoT 当前方向 | 已切换为“静态表 + 目标状态确认”，不再靠技能名字猜测 |
| 发布时不要直接重跑旧失败任务 | 如果 workflow 已变更，应使用 `workflow_dispatch` 并指定准确 tag |
| 恢复高风险 Hook 的原则 | 不要一次性全部恢复；每个 Hook 独立 `try/catch`、独立日志、失败即降级 |
| 查当前这批 UI / 配置改动优先看哪里 | 优先看 `MainWindow.cs`、`SettingsWindow.cs`、`StatsPanel.cs`、`PluginConfiguration.cs`，再对照 `README.md` / `md/USAGE.md` |
| 接手优先阅读顺序 | `HANDOVER.md` → `md/RECENT-ISSUES-SUMMARY.md` → `md/RECENT-ISSUES-STATUS-TABLE.md` → `md/2026-05-06-RELEASE-HANDOFF.md` |

---

## 2026-05-10 补充：单人解限 / NPC 队友 / 幻体副本

### 已确认进展

| 问题 / 事项 | 当前状态 | 关键文件 / 备注 |
|---|---|---|
| 单人解限场景下本地玩家身份全 0，导致完全无数据 | **已部分解决**。玩家自己的统计链路已经恢复，用户已确认玩家技能会进入战斗流水，DPS 面板也能看到玩家自己一行 | `DalamudACT/DalamudApi.cs`、`DalamudACT/Plugin/ACT.cs` |
| 战斗流水里的角色筛选交互不便 | **已解决**。角色筛选已改成下拉框 | `DalamudACT/UI/CombatTimelineWindow.cs` |
| 最近日志摘要无法直接承载关键调试信息 | **已解决**。新增 `LogHelper.DebugRecent(...)`，并补了调试级别的 UI 样式 | `DalamudACT/LogHelper.cs`、`DalamudACT/UI/LogUiHelper.cs` |
| 单人解限 / 信赖 / NPC 队友副本下缺少现场诊断信息 | **已解决**。已经补齐未命中 tracked actor、进战无流水、友方对象纳入缓存等调试日志 | `DalamudACT/Plugin/ACT.cs`、`DalamudACT/Stats/LocalStatsService.cs` |

### 待用户复测

| 项目 | 当前情况 | 建议验证方式 |
|---|---|---|
| 幻体是否能单独成行 | **待复测**。最新补丁已经允许按 `actorId + 名字` 直接把 `XX的幻体` 纳入 tracked actor，但截至交接时还没有新的现场反馈 | 让用户复测当前 `output\\DalamudACT.dll`，重点关注最近日志中是否出现 `已纳入可跟踪友方对象` / `已按事件身份纳入可跟踪友方对象` |
| 战斗结束判定是否恢复正常 | **待复测**。已经收紧 combat-end 参与对象范围，避免对象表中的无关友方 BattleNpc 长时间拖住 encounter | 让用户在明确脱战后继续观察是否正常结算；如仍不结束，继续补 `AreAllPartyMembersOutOfCombat(...)` 相关现场日志 |

### 当前仍未闭环的最高优先级问题

| 问题 | 当前判断 | 下一步建议 |
|---|---|---|
| 日志里能看到 `桑克瑞德的幻体 / 阿尔菲诺的幻体 / 阿莉塞的幻体`，但仍不显示单独行 | 问题已明显收敛到“**事件现场可见，但未成功注册为 tracked actor**”这一层；最新代码已开始直接按事件身份收编 | 如果仍不成行，优先继续检查 `TryGetTrackedActor(...)` 到 `EncounterSession.EnsureCombatant(...)` 之间的 actorId 口径是否还存在错位 |
| 有进入战斗，但没有结束战斗 | 旧实现很可能把对象表里过宽范围的友方 NPC 也纳入了脱战判定；当前代码已开始收紧，但仍需现场验证 | 如果继续复现，先打印到底是谁一直保持 `InCombat`，再决定是否继续收紧枚举范围 |
