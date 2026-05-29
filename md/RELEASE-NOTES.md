# DalamudACT {{VERSION}} 发布说明

- 目标版本：`{{VERSION}}`
- 本文件由 `.github/workflows/release.yml` 读取并写入 GitHub Release 正文。
- 发布前请确认 `{{VERSION}}` 已与 tag、项目版本和仓库 metadata 保持一致。

## 本次重点

### 时间轴

- 新增 `军工要地克吕提俄斯魔导工厂` 时间轴，`ZoneId = 1345`。
- 修复用户目录自定义索引会遮蔽内置/在线索引的问题，多个索引来源现在会合并读取。
- 支持时间轴 `jump 0` / `jump 123.4` 数字跳转目标。
- `SystemLogMessage` 同步支持应用 jump 目标。
- 运行中的技能同步增加大幅回跳保护，避免时间轴末尾后被重复技能拉回旧时间点。

### 稳定性

- 时间轴 parser 从历史名称 `M9STimelineParser` 改为通用 `TimelineParser`。
- `ActorControl` Hook 仍因启动崩溃风险保持禁用。

## 已知注意事项

- 用户目录 `Timeline/Data/timeline-index.json` 仍然优先于在线缓存和内置索引；同 `id` 条目会保留优先级最高的版本。
- `ActorControl` 时间轴行仍不会触发同步；当前只依赖 `Ability` / `SystemLogMessage` 等已启用来源。

## 验证建议

1. 确认 GitHub Release 页面标题为 `DalamudACT {{VERSION}}`。
2. 确认 Release 资产中包含 `DalamudACT.zip`。
3. 确认仓库 metadata / pluginmaster 中的下载链接指向本次 tag。
4. 在游戏中加载插件，确认主窗口显示版本与 `{{VERSION}}` 一致。
5. 进入 `遗忘行路雾之迹`，确认存在用户自定义索引时仍能加载 `ZoneId = 1314` 时间轴。
6. 进入 `军工要地克吕提俄斯魔导工厂`，确认自动匹配 `ZoneId = 1345` 时间轴。
