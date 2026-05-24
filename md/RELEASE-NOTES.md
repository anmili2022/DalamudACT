# DalamudACT {{VERSION}} 发布说明

- 目标版本：`{{VERSION}}`
- 本文件由 `.github/workflows/release.yml` 读取并写入 GitHub Release 正文。
- 发布前请确认 `{{VERSION}}` 已与 tag、项目版本和仓库 metadata 保持一致。

## 本次重点

### NPC 队友识别

- 支持通过多条路径识别当前队伍与 NPC 队友：
  - `PronounModule <1>~<8>`；
  - `AgentHUD.PartyMembers`；
  - Dalamud `PartyList`；
  - `BuddyList`；
  - `ObjectTable` 中可识别的友方 NPC。
- 信赖 / 剧情 / 单人任务 NPC 队友应按友方 NPC 独立统计。
- `OwnerId` 只对明确宠物 / Buddy / RaceChocobo 做归属，避免把 NPC 队友错误归到玩家头上。
- 不再单靠 `StatusFlags.Hostile` 判断 NPC 队友。

### UI 与设置

- 设置页提供 `NPC 队友识别名单`，用于查看当前队伍成员、核对玩家 / 友方 NPC 类型，并维护自定义 NPC 名单。
- 悬浮统计支持按对象类型选择显示口径：
  - 智能：多人仅玩家，单人可含友方 NPC；
  - 玩家 + 友方 NPC；
  - 玩家 + 敌方 NPC。
- NPC 行支持高亮和专用 badge。

### Debug 战斗记录

- 战斗流水和 debug 战斗记录默认关闭，需要排查时手动开启。
- debug 战斗记录继续使用 `Boss / 小怪` 与 `友方` 两组记录项。

### 稳定性

- `ActorControl` Hook 仍因启动崩溃风险保持禁用。
- `PronounModule` 读取避免使用可能不存在的 string 便捷重载，改走成员函数指针。
- 修正部分攻击技能附带 `Heal` 效果时被错误写入 HPS / 治疗 Boss 的口径。

## 已知注意事项

- DoT / HoT / Wildfire 等持续效果仍建议结合 `tools/DotReconcile` 与 ACT 日志继续做真实场景对账。
- 如果 NPC 队友没有出行，优先打开 `设置 -> NPC 队友识别名单` 查看当前队伍成员是否已被识别。
- 不要手动恢复 `ActorControl` Hook；如需恢复，必须先做地址范围校验、页保护校验和显式配置开关。

## 验证建议

1. 确认 GitHub Release 页面标题为 `DalamudACT {{VERSION}}`。
2. 确认 Release 资产中包含 `DalamudACT.zip`。
3. 确认仓库 metadata / pluginmaster 中的下载链接指向本次 tag。
4. 在游戏中加载插件，确认主窗口显示版本与 `{{VERSION}}` 一致。
5. 进入普通队伍、单人任务或 NPC 同行场景，确认玩家与 NPC 队友统计正常。
6. 如需排查 debug 记录，手动开启 debug 战斗记录，验证后再关闭。
