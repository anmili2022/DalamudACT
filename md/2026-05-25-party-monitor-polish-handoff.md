# 2026-05-25 队友监控体验优化交接

本文记录 2026-05-25 第二阶段的队友监控 UI 体验优化工作，包括 CD 数字显示改进、样式界面重做、布局微调、锁定穿透和发布。

## 变更概览

基于 `0.15.2.36` 继续迭代，发布 `0.15.2.37`。

### CD 数字显示

- 图标不再整体暗化，保持原始图标亮度。
- 倒计时数字通过偏移叠绘实现**加粗**效果：黑色描边 + 白色文字多次叠绘。
- 数字大小跟随图标大小缩放：`最终大小 = CD倒计时数字大小 × 图标大小 / 30`。
- 新增 `CD倒计时数字颜色` 颜色选择器，默认白色。
- 新增 `CD数字底部居中` 开关：
  - 关：垂直居中（默认）。
  - 开：底部居中，留 2px 下边距。

### 锁定穿透

- 开启 `锁定队友监控窗口` 时，自动添加 `ImGuiWindowFlags.NoInputs`。
- 效果：不可移动、不可缩放、鼠标穿透，不挡游戏点击。
- 关闭锁定时恢复交互。

### 悬浮窗样式界面重做

设置窗口 `队友监控 -> 悬浮窗样式` 经历了多次迭代：

1. 最初为单张 `DrawSettingCard` + `SameLine` 横向堆叠，太乱。
2. 改为三张内嵌 `BeginChild` 子卡片（尺寸与 CD数字 / 布局与显示规则 / 起效高亮与背景），但嵌套滚动条体验差。
3. 去掉子卡片，改为扁平化单卡片内三段式布局，蓝色文字标题 + `ImGui.Separator` 分隔。
4. 最终改为 `CollapsingHeader` 折叠栏，默认展开，内容为扁平三段式。

最终结构：

```
悬浮窗样式 (CollapsingHeader)
├── 尺寸与 CD 数字（两列表格）
│   ├── 图标大小          │ CD数字大小
│   └── CD数字颜色         │ CD数字位置
├── 布局与显示规则（两列表格）
│   ├── CD中技能           │ 团辅/减伤分组
│   ├── 姓名/职业列         │ 图标列间距
│   └── 行间距              │ (帮助说明)
└── 起效高亮与背景（两列表格）
    ├── 起效增强样式         │ 起效增强强度
    └── 背景默认颜色         │ (帮助说明)
```

### 新增控件

| 配置项 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `CountdownTextColor` | Vector4 | (1,1,1,1) | CD数字颜色 |
| `CountdownTextBottomCenter` | bool | false | CD数字底部居中 |
| `IconGap` | float | 4 | 技能图标列间距 |
| `RowGap` | float | 3 | 成员行间距 |
| `DefaultJobSkillConfigs` | Dictionary | (空) | 用户保存的默认勾选 |

### 按钮

职业技能卡片顶部新增两个按钮：

- **设当前为默认**：把当前所有职业的团辅/减伤勾选保存为用户默认。
- **重置监控技能**：恢复为默认勾选。如果已保存用户默认，使用用户默认；否则用内置默认。
- 原 `一键关闭/开启所有团辅技能` 按钮保留。

这两个按钮不影响自定义技能定义，但自定义技能的勾选状态也会被保存/重置。

### 匿名模式列宽

`JobColumnWidth` 从固定 92px 改为计算属性：

- 匿名模式：42px（职业名 2 个汉字）
- 非匿名模式：92px（玩家名 + 职业名）

`GetNameChipWidth` 从 `static` 改为实例方法以访问动态列宽。

## 修改文件

- `Features/PartyMonitor/PartyMonitorConfig.cs`：新增 `CountdownTextColor`、`CountdownTextBottomCenter`、`IconGap`、`RowGap`、`DefaultJobSkillConfigs`；新增 `ResetEnabledSkillsToDefault()`，`SaveCurrentEnabledSkillsAsDefault()`，新增 `PartyMonitorSkillDefaultConfig` 类。
- `Features/PartyMonitor/PartyMonitorWindow.cs`：加粗 CD 数字、图标不暗化、数字随图标缩放；锁定穿透 `NoInputs`；动态 `JobColumnWidth`；`IconGap`/`RowGap` 改为实例属性读取配置；`GetNameChipWidth` 改为实例方法。
- `UI/Windows/SettingsWindow.cs`：样式卡片重做（最终为折叠栏）；新增数字颜色/位置、图标间距/行间距控件；新增设当前为默认/重置按钮。
- `DalamudACT.csproj`、`Data/DalamudACT.json`、`repo.json`：版本升到 `0.15.2.37`。

## 发布

- 版本：`0.15.2.37`
- Commit：`dce885d`
- 发布链接：`https://github.com/anmili2022/DalamudACT/releases/tag/0.15.2.37`
- 仅暂存队友监控、版本、repo 相关文件，不提交工作区其他改动。

## 已知注意事项

- 样式折叠栏默认为 `CollapsingHeader`，默认展开。
- `CountdownTextColor` 默认白色；如果用户之前配置了白色但后来想改色，会正常生效。
- `IconGap` 默认 4px，`RowGap` 默认 3px，与之前硬编码常量的值一致。
- 匿名模式列宽 42px 是按职业名最长 3 个汉字（如"占星"）估算的，2px 余量；若后续有更长职业名需调整。
- 远端弃用警告 `ObjectId -> EntityId` 仍未处理。
- 工作区仍存在大量未提交改动（`ActMcpBridge/`、`helloagents/`、`.github/`、`tools/`、`md/` 等），下次发布时注意只暂存相关文件。

## 验证命令

```powershell
dotnet build 2>&1
```

输出：

```text
E:\git\DalamudACT\output\DalamudACT.dll
0 个警告，0 个错误
```
