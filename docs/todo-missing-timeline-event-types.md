# 缺失的时间轴事件类型

## 现状

当前 Parser（`EventTypeRegex`）未注册以下事件类型，对应 `.txt`/`.cn.txt` 时间轴文件中的条目不会被匹配。

| EventType | 文件数（去重） | 条目数（去重） | 观察方式 | 崩游戏风险 |
|---|---|---|---|---|
| **Tether** | 8 | ~48 | 网络包 Hook | 高 |
| **HeadMarker** | 3 | ~16 | 网络包 Hook（ActorControl category 0x1F1） | 高 |
| **NameToggle** | 3 | ~3 | 内存 Hook | 高 |
| **NpcYell** | 1 | ~2 | 网络包 Hook 或 Chat 解析（不可靠） | 高 |

## 为何搁置

1. 四种都需要 raw network packet hook 或 game memory hook，风险和目前禁用的 `ActorControl Hook` 同级
2. 条目总数不多（去重合计 ~70 条），分布在少量 raid 和旧副本文件
3. 现有 `Ability`/`StartsUsing`/`SystemLogMessage`/`MapEffect` 同步已覆盖绝大多数场景
4. 网络包增强模式（`CombatTimelineEnhancedNetworking`）默认关闭，不适合作为依赖

## 如果要加

### Parser（零风险，可随时加）

1. `EventTypeRegex` 追加 `HeadMarker|Tether|NpcYell|NameToggle`
2. 如需解析新参数（`npcNameId`、`npcYellId`、`target`）加对应 regex
3. `TimelineEntry` 加可选字段（可选）

### Observation（需要测试）

- 参考 `MapEffect` 的 `InstallMapEffectHook` 模式（`ACT.Hooks.cs:48`）
- 先开 `CombatTimelineEnhancedNetworking` 才安装 hook，避免默认启动崩游戏
- 各类型对应的 ActorControl category 或网络包 opcode 需要对照 FFXIV 内存文档验证
