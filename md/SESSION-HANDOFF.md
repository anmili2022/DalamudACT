# SESSION HANDOFF

## 2026-05-05 补充：死亡统计与总 DPS 行

- `DPS` 面板最下方的 `总DPS / 全队` 汇总行，`死亡` 列现在显示全队死亡总和。
- 实时死亡统计不再依赖已禁用的 `ActorControlSelf` Hook。
- 当前实现改为在 `LocalStatsService.Update(...)` 中轮询 `PartyList`：
  - 记录每个队友上一次 `CurrentHP`
  - 发现 `previousHp > 0 && currentHp == 0` 时，记一次死亡
  - 仅在 `inCombat == true` 或当前遭遇已经开始时生效，避免待机/切图误记
- 兼容取 ID 规则：
  - 优先使用 `member.GameObject?.EntityId`
  - 取不到时回退到 `member.ObjectId`
- 这意味着：
  - 玩家队友和 NPC 队友，只要出现在 `PartyList` 并发生 `HP > 0 -> 0`，都会被计入死亡
  - 如果插件是在角色已经死亡之后才启用，则不会补记这次历史死亡；后续新的死亡会正常统计

相关文件：
- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/UI/StatsPanel.cs`

## 项目目标

- 仓库：`E:\git\DalamudACT`
- 参考项目：`E:\git\ACTX`
- 最终目标：把 `DalamudACT` 做成一个 **可独立运行的 Dalamud DPS 统计插件**
- 约束：
  - 旧版 `DalamudACT` 的本地 DPS 统计逻辑已经弃用
  - 统计口径、展示模型和主要输出格式以 `ACTX / NotACT` 为准
  - 运行时不再依赖外部 `ACTX / MiniParse`
  - 插件名称使用中文：`DPS统计`

## 当前状态

### 已完成

- 已把原先依赖 `MiniParse` 的 UI 外壳改造成 **本地统计插件**
- 已新增本地统计服务：
  - `DalamudACT/Stats/LocalStatsService.cs`
- 已将 UI 数据源改为本地统计，而不是 `MiniParseClient`
- 已移除：
  - `DalamudACT/UI/MiniParseClient.cs`
- 已重写插件入口和 Hook 层：
  - `DalamudACT/Plugin/ACT.cs`
- 已补上 Dalamud 运行时兼容层：
  - `DalamudACT/DalamudApi.cs`
- 已更新插件元数据与说明：
  - `DalamudACT/DalamudACT.json`
  - `Data/DalamudACT.json`
  - `DalamudACT/DalamudACT.csproj`

### 当前可运行形态

- 插件现在以 **本地采集 + 本地统计 + 本地 UI 展示** 运行
- UI 使用中文
- 插件可以独立加载，不再依赖外部 `MiniParse`
- 当前统计主链路来自 `ActionEffect` 事件

### 当前构建结果

已验证可以成功构建：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Debug
```

最近一次结果：

- `0 warnings`
- `0 errors`

产物位置：

- `E:\git\DalamudACT\output\DalamudACT.dll`

## 本轮关键修复

### 1. SigScanner 注入兼容

当前 Dalamud 运行时中，直接注入旧版 `ISigScanner` / `SigScanner` 会失败。

已采用和 `E:\git\StarlightBreaker` 相同的兼容思路：

- 注入 `IGameInteropProvider`
- 通过反射从 `GameInteropProvider` 内部取出 scanner
- 通过 `DalamudApi.ScanText(...)` 统一扫描签名

相关文件：

- `DalamudACT/DalamudApi.cs`

### 2. 启用插件后崩溃的直接原因

`E:\git\crash\dalamud.log` 中已经坐实，本轮崩溃的第一触发点不是 `SigScanner`，而是：

```text
System.MissingMethodException:
Method not found:
'UInt16 Dalamud.Plugin.Services.IClientState.get_TerritoryType()'
```

触发路径：

- `DalamudACT.ACT.GetPlaceName()`
- `DalamudACT.ACT.OnFrameworkUpdate(IFramework framework)`

也就是说，插件加载成功后，在 `Framework.Update` 周期里调用了当前运行时不存在的 `IClientState.TerritoryType`，异常持续抛出，最终把宿主拖崩。

### 3. 本轮稳定化处理

已做以下保守修复：

- 在 `DalamudApi.cs` 中新增反射兼容入口：
  - `GetTerritoryTypeId()`
  - `GetLocalPlayerName()`
- `ACT.cs` 中不再直接调用：
  - `ClientState.TerritoryType`
  - `ClientState.LocalPlayer`
- `ACT.cs` 中为 `OnFrameworkUpdate(...)` 增加防护：
  - 首次异常记录日志
  - 后续不让异常继续冲击游戏主循环
- 只保留最稳的 `ActionEffectHandler.Receive` Hook
- 暂时禁用高风险 Hook：
  - `ActorControlSelf`
  - `Cast`

## 当前行为变化

### 已保留

- 本地 DPS / HPS / 承伤统计主面板
- ACTX 风格的本地输出模型
- 中文 UI
- 插件独立运行

### 当前被降级的能力

由于 `ActorControlSelf` 和 `Cast` 暂时被禁用，当前版本是 **兼容优先模式**，会带来以下已知缺口：

- DoT 归因不完整
- HoT 归因不完整
- Death 统计链路不完整
- 一些依赖 `ActorControlSelf` / `Cast` 的遭遇活跃度补强逻辑被关闭

换句话说：

- 当前版本优先保证 **插件能稳定加载并运行**
- 统计功能以 `ActionEffect` 主链路为核心
- 细节补全需要后续在确认签名和调用约定安全后再恢复

## 当前最重要的文件

- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/DalamudApi.cs`
- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/UI/PluginUI.cs`
- `DalamudACT/UI/MainWindow.cs`
- `DalamudACT/UI/SettingsWindow.cs`
- `DalamudACT/UI/StatsPanel.cs`

## 当前结论

### 已确认

- 插件已不再依赖 `MiniParse`
- 插件可成功编译
- `SigScanner` 注入问题已绕开
- `IClientState.TerritoryType` 兼容问题已处理
- 高风险 Hook 已降级，避免直接把游戏打崩

### 还不能宣称完全完成的部分

- 还没有证明当前版本在用户机器上已经长期稳定运行
- 还没有恢复 `ActorControlSelf / Cast` 相关统计补链
- 还没有把 DoT / HoT / Death 的能力完整恢复到目标状态

## 下一步建议

### 优先顺序

1. 先让用户用当前 `output\DalamudACT.dll` 重新实测
2. 确认是否还会在启用插件后立即崩溃
3. 如果不崩，再核对当前统计面板是否能稳定出数
4. 在稳定基础上，再恢复被禁用的能力

### 如果继续开发

推荐按下面顺序推进：

1. 先验证 `ActionEffect` 主链路是否稳定
2. 再单独恢复 `Cast` Hook，并单独测试
3. 最后恢复 `ActorControlSelf`，并重新校验签名与调用约定
4. 补齐：
   - DoT
   - HoT
   - Death
   - 遭遇生命周期补强

### 恢复高风险 Hook 时必须注意

- 不要一次性把所有 Hook 都开回去
- 每个 Hook 要独立 `try/catch` 和独立日志
- 任何一个 Hook 失败都应该降级继续运行，而不是让插件或游戏退出

## 需要参考的日志

崩溃和加载日志位置：

- `E:\git\crash\crash.log`
- `E:\git\crash\dalamud.log`
- `E:\git\crash\output.log`
- `E:\git\crash\trouble.json`

本轮定位时最关键的时间点：

- `2026-05-05 18:55:50 +08:00`

## 接手时先看什么

如果下一轮要继续接手，建议先看：

1. 本文件 `md/SESSION-HANDOFF.md`
2. `DalamudACT/Plugin/ACT.cs`
3. `DalamudACT/DalamudApi.cs`
4. `DalamudACT/Stats/LocalStatsService.cs`
5. 最新一次 `E:\git\crash\dalamud.log`

## 额外说明

- 仓库当前是脏工作区，不要使用破坏性 git 操作：
  - `git reset --hard`
  - `git checkout --`
- 如果需要恢复旧文件内容，优先使用：

```powershell
git -C E:\git\DalamudACT show HEAD:<path>
```

- 当前产物以 `output\DalamudACT.dll` 为准
