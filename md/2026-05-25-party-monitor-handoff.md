# 2026-05-25 队友监控交接

本文记录 `队友监控` 悬浮窗当前实现、设计取舍、调试入口和后续维护注意事项。

## 功能范围

- 显示当前队伍/本地玩家的技能监控与食物监控。
- 支持 `团辅`、`减伤`、`食物` 三个分组。
- 支持匿名模式：开启时用职业名代替玩家名。
- 支持无队伍/跨服队伍读取不完整时显示本地玩家监控。
- 支持每职业勾选监控技能。
- 支持添加自定义技能。
- 支持悬浮窗锁定、透明度、背景颜色、图标大小、倒计时字体大小、起效增强强度等样式配置。

## 关键文件

- `Features/PartyMonitor/PartyMonitorWindow.cs`
  队友监控悬浮窗绘制逻辑，包括分组、名牌、技能图标、CD/起效显示、食物名单和样式配置应用。
- `Features/PartyMonitor/PartyMonitorService.cs`
  队伍成员状态、食物状态、技能冷却状态构建逻辑。
- `Features/PartyMonitor/PartyMonitorConfig.cs`
  队友监控配置，包括模块开关、匿名模式、样式字段和每职业技能配置。
- `Features/PartyMonitor/PartySkillCatalog.cs`
  内置技能目录、技能 ID、共享 CD/升级技能触发关系、默认勾选逻辑。
- `Features/PartyMonitor/KamiIconLoader.cs`
  技能图标加载。
- `UI/Windows/SettingsWindow.cs`
  队友监控设置 UI、样式卡片、职业技能勾选、自定义技能、BUFF 调试按钮。
- `Infrastructure/DalamudApi.cs`
  Dalamud 服务访问和本地玩家兼容读取。

## 当前显示规则

- 悬浮窗按当前快照收集顺序显示成员，不再额外排序。
- 本地玩家会先尝试加入，避免无队伍或跨服队伍信息不完整时窗口为空。
- 技能进入 CD 后，如果剩余 CD 大于 `10s`，隐藏技能图标。
- 技能剩余 CD 小于等于 `10s` 时重新显示图标和倒计时。
- 起效中技能始终显示。
- 某成员所有技能都隐藏时，仍保留该成员行，并放一个不可见占位，避免下一行图标上挤。
- 分组标题里的数量统计当前实际显示的图标数。
- 食物分组只显示需要处理的异常成员：未进食，或食物剩余时间小于等于提醒阈值。
- 食物异常名单横向排列，并按当前窗口可用宽度自动换行；没有异常成员时隐藏整个分组。
- 食物分组标题为 `需补食 (N)`，数量表示当前未进食或即将到期的成员数。

## 样式规则

- 名字/职业标签使用 DPS 占比条同一套职业配色作为背景。
- 名字/职业标签文字为白色，带黑色阴影和深色边框。
- 未进食成员使用红色粗边框；食物即将到期成员使用橙黄色粗边框，标签内部仍保留职业配色。
- 起效中技能使用金色双层边框。
- 起效中的减伤技能有额外金色底光和左上角 `效` 标记。
- 队友监控样式设置位于设置窗口 `队友监控 -> 样式`。
- 当前可配置项：
  - `图标大小`
  - `CD倒计时数字大小`
  - `启用起效增强样式`
  - `起效增强强度`
  - `背景默认颜色`

## 食物监控

当前环境确认食物状态为：

```text
id=48 name=进食 category=1 remaining=1116.6s
```

维护重点：

- 不要只依赖 `StatusCategory` 判断食物。
- 当前以 `StatusId = 48` 为主判断食物。
- 由于运行时 Dalamud 类型和编译期类型曾出现不一致，食物状态读取改为反射读取。
- 反射只读状态数据，不写内存、不调用 native、不安装 hook，主要风险是 API 属性改名后读不到。

### 到期提醒规则

- 配置字段为 `PartyMonitorConfig.FoodExpiryWarningMinutes`，默认 `10` 分钟。
- 设置入口位于 `队友监控 -> 监控模块 -> 食物到期提醒阈值`，仅在开启食物监控时显示。
- 可调范围为 `1-60` 分钟；显示判断使用相同范围限制，防止异常配置值影响界面。
- 当 `FoodRemainingSeconds <= FoodExpiryWarningMinutes * 60` 时，成员进入 `需补食` 分组。
- 剩余时间恰好等于阈值时也提醒。
- 完全未检测到食物时使用红框；检测到食物但不足阈值时使用橙黄框。
- 配置字段需要同步维护 `PluginConfiguration.Reset.cs` 中的 UI 设置快照复制和恢复逻辑，避免取消设置或恢复快照后丢失。

### 悬停提示

- 鼠标悬停在异常成员标签上时显示 Tooltip。
- 已进食成员显示食物剩余时间和当前提醒阈值。
- 未进食成员显示 `未检测到食物效果`。
- 剩余时间不足一小时显示 `N分 N秒`，达到一小时显示 `N小时 N分`，不足一分钟显示秒数。
- 匿名模式下 Tooltip 不显示玩家名，避免通过提示泄露姓名。

### 刷新策略

- 战斗中食物状态沿用队友监控的有效刷新间隔。
- 战斗外技能监控仍保持暂停，但食物状态独立按 `1000ms` 低频刷新并重建成员快照。
- 该策略用于覆盖进本准备、倒计时和战斗外补食场景，避免显示停留在旧缓存。
- `IsPausedOutOfCombat` 仍表示技能监控暂停；不表示食物刷新也已暂停。

## BUFF 调试按钮

设置窗口 `数据与状态` 中，`恢复默认` 旁有 `打印当前BUFF` 按钮。

输出位置：

- 游戏聊天框，标签为 `DPS统计 · [调试] BUFF`。
- Dalamud/卫月日志。
- 设置页最近日志摘要。

输出字段：

- 状态栏序号
- `StatusId`
- 状态名称
- `StatusCategory`
- 剩余时间
- 参数
- 层数
- 来源 ID
- ActorId

如果食物监控再次误判，优先让用户点击此按钮，确认当前食物状态的 `id/category/remaining`。

## Dalamud 兼容性

已确认当前官方 API 15 文档中：

- `IClientState` 不再暴露 `LocalPlayer`。
- 官方本地角色对象入口是 `IObjectTable.LocalPlayer`。
- `IPlayerState` 在 API 15 中存在，但当前本项目本地 SDK 暂无该接口，不能强类型注入。

当前实现：

- 优先从 `ObjectTable.LocalPlayer` 读本地玩家对象。
- 反射兜底 `ClientState.Pc` / `ClientState.LocalPlayer`，并吞掉版本差异异常。
- 不直接强类型调用 `ClientState.LocalPlayer`，避免 `MissingMethodException`。

## Hook 状态

- ActionEffect Hook：启用，用于技能使用记录和战斗统计。
- Cast Hook：未启用。
- ActorControl Hook：默认不启用。

不要随意重新启用 ActorControl Hook。之前在部分 Dalamud/客户端组合下，ActorControl Hook 的 `HookFromAddress/FollowJmp` 曾触发原生 `AccessViolation`。

## 技能目录维护

技能目录位于 `PartySkillCatalog.cs`。

维护原则：

- 优先参考 JobBars 的技能 ID、共享 CD 和升级技能触发关系。
- 对共享 CD/升级技能，显示高等级技能，`TriggerActionIds` 包含低等级和高等级触发 ID。
- 不要每帧自动重建默认勾选，否则用户取消勾选会失效。
- 默认不勾选的噪音技能应保留可手动勾选能力。

## 设置 UI

队友监控设置结构：

- `监控模块` 卡片：食物、技能、匿名模式。
- 开启食物监控时，`监控模块` 卡片额外显示 `食物到期提醒阈值`，范围 `1-60` 分钟，默认 `10` 分钟。
- `样式` 卡片：图标、字体、起效增强、背景颜色。
- `按职业选择监控技能`：外层可折叠，内部按 21 个常规战斗职业分组。
- `添加自定义技能`：单个全局添加区，通过职业下拉选择目标职业。

## 已知注意事项

- 反射读取是为了兼容当前运行时/编译期 API 差异，不是长期最理想方案。
- 如果 SDK 和运行环境统一到新的 Dalamud API，可考虑把状态读取集中封装到 `StatusReader`，再逐步替换为强类型读取。
- 图标加载当前使用 Lumina `.tex` 路径和 `CreateFromTexFile`。用户确认当前构建可显示图标且没有复现 NVIDIA 驱动崩溃。
- 悬浮窗背景透明度为 `1` 时应完全不透明；背景颜色 alpha 由 `PartyMonitorOpacity` 统一控制。
- 锁定悬浮窗时只添加 `NoMove | NoResize`，不再添加 `NoInputs`。这样锁定后仍可触发食物和技能 Tooltip；右键打开设置由 `HandleContextClick()` 单独禁止。

## 验证命令

构建命令：

```powershell
$dalamudPath = Join-Path $env:APPDATA 'XIVLauncherCN\addon\Hooks\dev\'
dotnet build "DalamudACT/DalamudACT.csproj" `
  --no-restore `
  -p:DalamudLibPath="$dalamudPath"
```

显式传入 `DalamudLibPath` 是本地 MSBuild 未正确传递 Dalamud 引用路径时的构建环境兜底，不是源码修复。依赖目录应至少包含 `Dalamud.dll`、`Dalamud.Bindings.ImGui.dll`、`FFXIVClientStructs.dll` 和 `Lumina.dll`。

当前输出 DLL：

```text
E:\git\DalamudACT\output\DalamudACT.dll
```

2026-08-25 食物到期提醒改进构建结果：`0 个警告，0 个错误`。

## 后续建议

- 把反射状态读取抽成独立工具类，减少 `SettingsWindow` 和 `PartyMonitorService` 的重复代码。
- 增加一个“重置为推荐默认勾选”按钮，而不是通过迁移或自动初始化反复影响用户选择。
- 继续按用户反馈修正技能 ID、持续时间、共享 CD 关系。
