# 2026-06-02 UI 主题优化工作记录

## 变更汇总

### 1. 完整设置窗口 UI 优化

- **SettingsWindow.cs**: `DrawAdvancedSettings()` 整体包裹樱花粉圆角风格，新增「主题配色」折叠卡片（RadioButton + 色样色块预览）
- **SettingsWindow.Helpers.cs**: 
  - `DrawFirstLevelHeader`：header 颜色从蓝色改为樱花粉 `(0.85,0.44,0.53)`
  - `DrawSettingCard`：`ChildRounding` 改为 `14f`，背景色/边框改为粉色面板色
  - 新增 `PushThemeStyle(UiThemeColors)` / `PopThemeStyle()`：统一推送 5 个 style var + 20 个颜色
- **SettingsWindow.QuickSettings.cs**: 主题色方案应用于所有颜色调用

### 2. 右下角色块移除

- **SettingsWindow.QuickSettings.cs**: 删除 `CoverQuickResizeGrip()` 方法及调用。缩放箭头已透明，无需覆盖

### 3. 快捷入口「保存当前UI为默认」 + 「还原默认UI」

- **SettingsWindow.QuickSettings.cs**: 快捷入口新增两个按钮
- **PluginConfiguration.Reset.cs**: 新增 `SaveCurrentUiAsDefault()` / `ApplyUiSettingsSnapshot()` 快照机制
- 新增 `UiSettingsSnapshot` 类，保存/恢复全部 UI 字段

### 4. 完整设置「关闭窗口」按钮

- **SettingsWindow.cs**: `DrawAdvancedSettings()` 标题行右侧新增「关闭窗口」按钮

### 5. 主题切换系统

- **新增文件**: `Configuration/UiTheme.cs`
- **UiThemeId 枚举**: 9 套配色方案
  - `Sakura` / `SakuraNight` / `Ocean` / `OceanNight`
  - `Forest` / `Purple` / `Sunset` / `Monochrome` / `Cyber`
- **UiThemeColors 结构**: 每套定义 11 个颜色向量（Text, Panel, Accent, Border, Ok 等）
- **PluginConfiguration.cs**: 新增 `SelectedUiTheme` 字段
- 完整设置「主题配色」卡片 + 快速设置「外观」页主题切换面板
- `PushSakuraStyle()` → `PushThemeStyle(UiThemeColors)`
- `DrawQuickSettingsShell` 颜色内联定义 → 读取 `config.SelectedUiTheme`
- `DrawQuickNavIcon`, `DrawQuickPanel` 等使用主题色

### 6. 发布 `preview/settings-ui-preview.html`

- 新增 9 套配色的 HTML 交互预览，点击色卡全局切换主题

## 受影响的文件

| 文件 | 变更 |
|------|------|
| `Configuration/UiTheme.cs` | **新增** — 主题枚举 + 颜色数据 |
| `Configuration/PluginConfiguration.cs` | 新增 `SelectedUiTheme` 字段 |
| `Configuration/PluginConfiguration.Reset.cs` | `SaveCurrentUiAsDefault`、`ApplyUiSettingsSnapshot`、`ResetUiSettings`、`Reset`、`UiSettingsSnapshot` 均包含主题字段 |
| `UI/Windows/SettingsWindow.cs` | `DrawAdvancedSettings` 包裹 PushThemeStyle，新增 `DrawThemeSwitcher`，添加「关闭窗口」按钮 |
| `UI/Windows/SettingsWindow.Helpers.cs` | `DrawFirstLevelHeader` 粉色化；`DrawSettingCard` 粉色圆角；`PushThemeStyle`/`PopThemeStyle` |
| `UI/Windows/SettingsWindow.QuickSettings.cs` | 所有颜色改为读取配置主题；快捷入口新增 UI 保存/还原按钮；外观页新增主题切换面板 |
| `preview/settings-ui-preview.html` | 更新为 9 套配色交互预览 |

## 构建状态

`dotnet build --no-restore` → **0 警告 0 错误**。
