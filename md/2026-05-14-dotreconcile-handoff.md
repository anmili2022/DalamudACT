# 2026-05-14 交接补充：离线 DoT 对账工具收口

## 本轮做了什么

本轮主要是在准备收工前，把“离线对账工具 + 文档”补齐，并把当前排查状态整理成一份可以直接接手的交接说明。

### 1）补了离线对账工具说明

README 里已经新增 `tools/DotReconcile` 的使用说明，重点包括：

- 读取插件导出的 `history-records.json`
- 自动选择最新一场战斗，或按副本名筛选
- 扫描 ACT `Network_*.log`
- 统计 `24|DoT|...` 事件
- 导出 `json / csv / status csv`

### 2）补了更稳妥的 ACT 对账口径

`tools/DotReconcile` 现在不再把所有 hostile DoT 一把抓，而是拆成两部分：

- **已归属 hostile DoT**
  - source 能明确归到玩家
- **未归属 hostile DoT**
  - 例如 source 缺失
  - 或者 source 本身也是 hostile / 自身行

这很重要，因为：

- 玩家页里显示的 ACT 值，现在应当理解为 **ACT 已归属**
- 如果终端里出现“未归属 hostile DoT”，那玩家 ACT 结果就只能当**下限**看
- 这能避免把“ACT 源丢失”误判成“插件虚高”

### 3）顺手保留了前几轮的核心修正方向

虽然这轮没有继续深改 `LocalStatsService.cs`，但当前仓库里已经保留了上一轮的重要补强：

- `PlayerDotCatalog.cs`
  - 骑士 `厄运流转` 已补 `seedPotency / dotTickPotency`
  - 占星 `炽灼` 已补 `凶星 / 灾星` 双锚点
  - 贤者 `均衡注药` 已补 `dotTickPotency` 和 `注药` 锚点
- `README.md`
  - 已写清楚离线对账工具的使用方法
  - 已写清楚 hostile-only 与“未归属 hostile DoT”的注意事项

---

## 当前验证结果

### 1）构建结果

已验证通过：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

结果：

- `0 warnings`
- `0 errors`
- 产物：`E:\git\DalamudACT\output\DalamudACT.dll`

### 2）DotReconcile 结果

已验证通过：

```powershell
dotnet run --project tools\DotReconcile\DotReconcile.csproj -- --latest --top-status 5
```

当前这次最新历史记录对应的是：

- 副本：`万魔殿 边境之狱2`
- 开始：`2026-05-14 20:03:57 +08:00`
- 结束：`2026-05-14 20:07:40 +08:00`

本次工具输出里：

- `ACT 已归属` 有正常结果
- `未归属 hostile DoT = 0`

也就是说，**这次最新样本里没有触发“ACT 只剩下下限”的情况**。

---

## 需要特别注意的事情

### 1）旧的利维亚桑样本已经被新的 history 覆盖

如果后面还要继续追前一轮的：

- `利维亚桑歼灭战`
- `骑士 / 占星 / 贤者` 那组对账

那就需要重新导出对应的 `history-records.json`。

因为现在本机的 `history-records.json` 已经变成了新的战斗记录，**不能直接拿当前文件复现上一场**。

### 2）当前工作区仍然是脏的

仓库里还有未提交改动和新文件，收工前请注意：

- `1.txt` 是未跟踪文件，**不要删除**
- `tools/DotReconcile/` 是新工具目录，当前还没正式整理成提交态

---

## 如果下一位继续接手，建议直接从这几步开始

1. 先看 `README.md` 里的 `离线 DoT 对账工具` 一节，确认当前对账口径
2. 如果要继续追旧样本，先重新导出那一场的 `history-records.json`
3. 继续用：

```powershell
dotnet run --project tools\DotReconcile\DotReconcile.csproj -- --latest --top-status 5
```

   或者按副本筛：

```powershell
dotnet run --project tools\DotReconcile\DotReconcile.csproj -- --zone <副本名> --top-status 5
```

4. 如果还要继续追 PLD 偏高 / AST、SGE 漏记，再回到：
   - `DalamudACT/Stats/LocalStatsService.cs`
   - `DalamudACT/Stats/PlayerDotCatalog.cs`

---

## 一句话收尾

这轮已经把“离线对账工具怎么用、ACT 怎么算、未归属 DoT 怎么解释”写清楚了；后面如果要继续追具体职业偏差，就直接拿新导出的 history 继续跑对账，不要再沿用已经被覆盖的旧样本。
