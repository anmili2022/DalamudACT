# 项目结构说明

更新时间：`2026-05-23`

这份文档用于说明当前 `DalamudACT` 插件工程的模块化目录。重构原则是：

- 保留原有中文注释和排查说明；
- 不改变公开命名空间，避免一次性大范围改 `using` 和类型引用；
- 先按功能边界整理目录，再逐步拆分过大的服务 / UI 文件；
- Hook、统计、UI、配置、工具分层放置，避免继续把新功能堆到同一个目录。

## 顶层目录

```text
DalamudACT/
├─ Configuration/          # 插件配置、配置迁移、默认值
├─ Features/               # 按功能域组织的业务模块
├─ Infrastructure/         # Dalamud 服务适配、日志等基础设施
├─ Plugin/                 # 插件入口、Hook 安装与生命周期
├─ UI/                     # 通用 UI 窗口、面板、主题和辅助工具
└─ images/                 # 插件图标等资源
```

## 功能模块

```text
DalamudACT/Features/
├─ Stats/                  # 本地战斗统计、DoT 目录和统计核心
├─ CombatTimeline/         # 战斗流水窗口
└─ DebugCombatLog/         # debug 战斗记录窗口
```

说明：

- `Features/Stats/LocalStatsService.cs` 仍是当前统计核心主文件，其中 Actor / ObjectTable / Party / Buddy / owner cache 已拆到 `LocalStatsService.Actors.cs`，debug 战斗记录已拆到 `LocalStatsService.DebugRecorder.cs`，玩家 DoT / Wildfire / DOT 诊断已拆到 `LocalStatsService.Dots.cs`，当前战斗 / 战斗流水 / 战斗结算 / ACTX 快照已拆到 `LocalStatsService.Encounter.cs`，通用文本 / 数字格式化已拆到 `LocalStatsService.Formatting.cs`，历史记录 / 预览 / 导入导出已拆到 `LocalStatsService.History.cs`，StatusList / 状态反射读取已拆到 `LocalStatsService.Status.cs`，内置测试数据已拆到 `LocalStatsService.TestData.cs`。
- `Features/DebugCombatLog/DebugCombatLogWindow.cs` 是独立 debug 战斗记录 UI。
- `Features/CombatTimeline/CombatTimelineWindow.cs` 是原战斗流水 UI。

## 基础设施

```text
DalamudACT/Infrastructure/
├─ DalamudApi.cs           # Dalamud 服务注入和兼容读取
└─ Logging/LogHelper.cs    # 日志封装
```

`DalamudApi.cs` 中仍保留反射兼容逻辑，用于兼容不同运行时里服务属性名或对象属性名变化。

## 插件入口与 Hook

```text
DalamudACT/Plugin/
├─ ACT.cs                  # 插件生命周期、ActionEffect 主链路、通用事件处理
└─ Hooks/
   └─ ACT.ActorControlHook.cs  # ActorControl / TargetIcon 特效标记采集 Hook
```

说明：

- `ACT` 已改为 `partial`，避免单文件继续膨胀。
- `ACT.ActorControlHook.cs` 独立承载 ActorControl Hook 的反射地址读取、签名扫描兜底、特效标记解析和委托定义。
- `Cast Hook` 仍按稳定性策略禁用；BOSS 读条使用 Framework 轮询采集。

## UI 模块

```text
DalamudACT/UI/
├─ Windows/                # 主窗口、设置窗口、悬浮统计窗口
├─ Panels/                 # 统计面板
├─ Models/                 # UI 数据模型
├─ Theme/                  # 职业主题色
├─ Helpers/                # UI 辅助函数
└─ PluginUI.cs             # 窗口总调度
```

## 后续拆分建议

优先级从高到低：

1. 将 `Features/Stats/LocalStatsService.cs` 继续拆成更小的 partial：
   - `LocalStatsService.Dots.Wildfire.cs`：从最大的 DoT 模块里拆出 Wildfire / 野火状态采集、贡献样本和模拟结算。
2. 将 `UI/Windows/SettingsWindow.cs` 按设置卡片拆分。
3. 将 `UI/Panels/StatsPanel.cs` 按 DPS/HPS/承伤/历史/概览渲染拆分。
4. 每次拆分后都运行：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

并确认 `0 warnings / 0 errors`。
