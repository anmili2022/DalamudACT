# 2026-05-11 最终交接摘要

这是一版更短的收工说明，给下一个接手的人快速看结论。

## 2026-05-11 补充：高闪雷 DoT 前几跳变成 1000 的修复

- 用户反馈的核心现象是：`高闪雷[36986]` 的后续模拟跳字前几跳经常显示成 `1000`，明显偏低。
- 这不是实战真实伤害，而是插件的 **DoT 模拟兜底值**。
- 根因是：
  - 首段真实伤害已经被 `ObservePotentialPlayerDotDamageSeed(...)` 记录；
  - 但在回写活跃 DoT 状态、查找最近动作、绑定目标时，`sourceId / targetId` 仍有口径不一致；
  - 之前只修了目标等价匹配，这次把 **来源 sourceId 也统一改成等价匹配**，避免同一对象因不同 ID 口径绑定失败。

### 本轮已完成的关键改动

- `DalamudACT/Stats/LocalStatsService.cs`
  - 新增 `AreEquivalentActorIds(...)`；
  - 将 DoT 种子记录、最近动作回查、活跃状态匹配、目标清理等路径统一改成等价 ID 匹配；
  - 重点修复高闪雷前几跳一直落到 `1000` 的问题。
- `DalamudACT/Plugin/ACT.cs`
  - 继续负责识别已知 DoT 技能、记录种子和触发状态链路。
- 战斗流水格式已保持为：
  - `xx 使用xx[id] 攻击xx`

### 当前验证结果

- 已重新构建通过：
  - `dotnet build .\DalamudACT.sln`
  - 结果：`0 warnings / 0 errors`

### 目前结论

- `1000` 是模拟兜底值，不是游戏真实伤害。
- 这次把来源 / 目标的 actorId 口径都补成“等价匹配”后，理论上高闪雷前几跳应更容易吃到首段真实种子，不再频繁掉到 `1000`。
- 仍建议下一轮进游戏实测，重点看：
  1. 首段真实高闪雷是否能正确带动后续模拟跳字；
  2. 前几跳是否还会连续出现 `1000`；
  3. 如还有异常，再继续加调试日志跟踪种子绑定链路。

## 当前结论

- 玩家 DoT 主链路已经切到**状态驱动**：
  - 玩家使用已知 DoT 技能；
  - 目标身上出现对应 debuff；
  - 才进入活跃 DoT 状态；
  - 后续由 `LocalStatsService.PollActivePlayerDots(...)` 按 **3 秒**补 tick。
- 只要目标**不可选中**，DoT 后续结算就会停止。
- DoT 暴击现在按普通技能那套暴击率逻辑**模拟**，不再直接依赖原始 tick 包里的暴击字段。
- DoT 白名单入口已经收口到：
  - `DalamudACT/Stats/PlayerDotCatalog.cs`
  - 只按 `actionId / statusId` 过滤候选。

## 已验证

- 已重新构建通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - 结果：`0 warnings / 0 errors`

## 关键文件

- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/Stats/PlayerDotCatalog.cs`

## 下次接手先看

1. `HANDOVER.md`
2. `md/SESSION-HANDOFF.md`
3. `md/2026-05-11.md`

## 仍需注意

- 当前 DoT 估算值还是偏保守的推导，后续主要靠白名单补漏和实测确认。
- `1.txt` 是未跟踪文件，**不要误删**。
- 当前工作区仍然是脏的，接手前先看 `git status --short`。
