# DalamudACT {{VERSION}} 发布说明

- 目标版本：`{{VERSION}}`
- 本文件由 `.github/workflows/release.yml` 读取并写入 GitHub Release 正文。
- 发布前请确认 `{{VERSION}}` 已与 tag、项目版本和仓库 metadata 保持一致。

## 本次重点

### 时间轴

- 修复 `读条ID` / `结算ID` 注释解析，应对方案不再被元数据过滤误删。
- `读条ID` 应对方案改为 Boss 开始读条时即时播报，避免时间轴提前窗口猜错分支。
- 时间轴提前窗口不再聚合播报分支型应对方案，只保留纯机械提示和技能名。
- 支持 `window <before>,<after>` 匹配窗口，用于 `SystemLogMessage` 等同步条目的时间范围限制。
- 系统日志同步兼容聊天文本时间戳前缀，例如 `[3:38]距...被封锁还有15秒。`。
- 简易设置顶部新增“战斗流水”入口。
- TTS 纠偏默认新增 `对地 -> 对帝`。

### 战斗流水

- 简易设置可直接打开战斗流水，便于排查时间轴和战斗事件。

### 兼容性

- 发布包继续包含内置 `Timeline/Data`，本地开发环境不依赖构建输出复制时间轴数据。

### 稳定性

- `ActorControl` Hook 仍因启动崩溃风险保持禁用。
- 网络包增强模式默认关闭，需手动开启。

## 已知注意事项

- 时间轴硬编码源码路径仅用于当前本地开发环境，不应随意修改。
- `ActorControl` 时间轴行仍不会触发同步；当前只依赖 `Ability` / `SystemLogMessage` 等已启用来源。

## 验证建议

1. 确认 GitHub Release 页面标题为 `DalamudACT {{VERSION}}`。
2. 确认 Release 资产中包含 `DalamudACT.zip`。
3. 确认仓库 metadata / pluginmaster 中的下载链接指向本次 tag。
4. 在游戏中加载插件，确认主窗口显示版本与 `{{VERSION}}` 一致。
5. 进入 `军工要地克吕提俄斯魔导工厂`，确认可自动加载时间轴并响应封锁系统日志同步。
6. 使用带 `读条ID` / `结算ID` 注释的时间轴行，确认读条应对在 Boss 开始读条时播报。
