# DalamudACT {{VERSION}} 发布说明

- 目标版本：`{{VERSION}}`
- 本文件由 `.github/workflows/release.yml` 读取并写入 GitHub Release 正文。
- 发布前请确认 `{{VERSION}}` 已与 tag、项目版本和仓库 metadata 保持一致。

## 本次重点

### 时间轴

- 修复 `SystemLogMessage` 同步在不同客户端语言下的匹配问题，改为基于游戏 `LogMessage` / `PlaceName` 表展开匹配。
- 修正 `军工要地克吕提俄斯魔导工厂` 封锁同步地点参数，并校准老 1 `装甲之眼` 时间轴。
- 修复插件重载后聊天历史回放可能误触发时间轴同步的问题。
- 修复 `jump 0` 的 `SystemLogMessage` 重置条目可能被时间过滤跳过的问题。
- 开发环境优先加载源码目录时间轴，构建产物不再复制内置时间轴数据。
- 修复发布包缺少内置时间轴数据时提示“当前区域没有时间轴”的问题。

### 性能

- 优化 `DPS统计` 面板绘制，缓存战斗对象类型判断并限制历史记录页单次绘制行数，降低 UI 卡顿风险。

### 兼容性

- 兼容 Dalamud 新版聊天/日志消息结构，支持 `Text` / `Kind` 字段读取。

### 稳定性

- `SystemLogMessage` 同步不再依赖中文硬编码文本片段。
- `ActorControl` Hook 仍因启动崩溃风险保持禁用。

## 已知注意事项

- 用户目录 `Timeline/Data/timeline-index.json` 仍然优先于源码/在线缓存索引；同 `id` 条目会保留优先级最高的版本。
- `ActorControl` 时间轴行仍不会触发同步；当前只依赖 `Ability` / `SystemLogMessage` 等已启用来源。

## 验证建议

1. 确认 GitHub Release 页面标题为 `DalamudACT {{VERSION}}`。
2. 确认 Release 资产中包含 `DalamudACT.zip`。
3. 确认仓库 metadata / pluginmaster 中的下载链接指向本次 tag。
4. 在游戏中加载插件，确认主窗口显示版本与 `{{VERSION}}` 一致。
5. 进入 `军工要地克吕提俄斯魔导工厂`，确认 `距花冠广场被封锁还有15秒。` 可同步到老 1 时间轴。
6. 确认 `花冠广场的封锁解除了……` 可重置时间轴。
