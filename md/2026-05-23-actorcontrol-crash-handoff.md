# 2026-05-23 ActorControl Hook 启动崩溃处理交接

## 背景

- 用户反馈：刚才开游戏崩溃，要求先暂停结构拆分，改为查看卫月 / Dalamud 日志。
- 当前工作目录：`E:\git\DalamudACT`。
- 本轮没有继续拆 `LocalStatsService`，只处理启动崩溃风险。

## 日志结论

已检查 XIVLauncherCN / Dalamud 崩溃日志，关键文件包括：

- `C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\dalamud_appcrash_20260523_021020_798_17592.log`
- `C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\dalamud_appcrash_20260523_021058_662_18072.log`
- `C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\dalamud_appcrash_20260523_021233_913_13152.log`

三份崩溃日志都指向同一条调用链：

```text
System.AccessViolationException: Attempted to read or write protected memory.
Dalamud.Memory.MemoryHelper.ReadRaw(IntPtr, Int32)
Dalamud.Hooking.Internal.HookManager.FollowJmp(IntPtr)
Dalamud.Hooking.Hook<T>.FromAddress(...)
Dalamud.Hooking.Internal.GameInteropProviderPluginScoped.HookFromAddress(...)
DalamudACT.ACT.CreateActorControlHook()
DalamudACT.ACT.InstallHooks()
DalamudACT.ACT..ctor(...)
```

判断：直接崩溃点不是这几轮 `LocalStatsService` partial 拆分，而是插件加载阶段安装 `ActorControl Hook` 时调用 `HookFromAddress`，Dalamud 在 `FollowJmp` 读取目标地址附近原生内存时触发 `AccessViolationException`。

`dalamud.log` 中还存在仓库网络错误、AEAssist 下载失败、Penumbra 资源加载失败、vnavmesh legacy mode 等信息，但这些不是这次进游戏崩溃的主异常栈。

## 本轮处理

修改文件：

- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/Features/Stats/LocalStatsService.DebugRecorder.cs`

处理方式：

- 新增 `ShouldInstallActorControlHook => false`，并保留注释说明禁用原因。
- `InstallHooks()` 中默认不再调用 `CreateActorControlHook()`，避免进入 `HookFromAddress` / `FollowJmp`。
- 保留 `DalamudACT/Plugin/Hooks/ACT.ActorControlHook.cs` 中的反射签名、本地兼容签名和事件解析代码，后续如果要重开可在加安全校验后继续使用。
- 追加无 Hook 兜底：debug 战斗记录轮询我方角色的 `GameObject.NamePlateIconId`，当该字段从 0 变成非 0 或发生变化时，按“自己标记 / 队友标记”记录一条 `NamePlateIconId 轮询` 日志。
- 日志文案改得更明确：
  - `ActorControl Hook 已因启动崩溃风险暂时禁用；debug 战斗记录中的队友/自己特效标记采集暂不可用。ActionEffect 主统计、BOSS 读条轮询、BUFF/debuff 与 DoT 诊断不受影响。`
  - `Cast Hook 当前按稳定性策略禁用；BOSS 读条使用 Framework 轮询 IBattleChara.IsCasting 采集。ActionEffect 主统计会独立安装；ActorControl 特效标记采集当前已按稳定性策略禁用。`

## 影响范围

暂时不可用：

- 所有依赖 ActorControl raw 事件采集的特效标记记录。

可尝试的新兜底：

- 如果机制头顶图标会同步到 `NamePlateIconId`，现在可以不安装 ActorControl Hook 也记录到 `队友标记 / 自己标记`。
- 如果某类红色箭头 / 头顶机制标记只存在于 ActorControl 网络事件或 VFX，不同步到 `NamePlateIconId`，这类标记仍然不会被当前安全兜底捕获。

不受影响：

- ActionEffect Hook 主统计；
- 实时 DPS / HPS / 承伤统计；
- BOSS 平 A / BOSS 发动技能等基于 ActionEffect 的 debug 记录；
- BOSS 读条技能轮询；
- BUFF / debuff / DoT / Wildfire 诊断；
- 历史记录、导入导出、测试数据。

## 构建验证

已执行：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

结果：

```text
已成功生成。
0 个警告
0 个错误
产物：E:\git\DalamudACT\output\DalamudACT.dll
```

## 后续建议

如果后续还要恢复特效标记采集，不建议直接把 `ShouldInstallActorControlHook` 改回 true。建议先补：

1. 配置项，例如 `EnableActorControlHook`，默认 `false`，只允许手动启用。
2. 安装前打印候选地址来源：FFXIVClientStructs 签名 / 本地兼容签名、call site、target address。
3. 安装前校验目标地址：
   - 非 `IntPtr.Zero`；
   - 位于 `ffxiv_dx11.exe` 主模块或明确允许的模块范围；
   - 页保护可执行且可安全读取；
   - 本地签名扫描没有命中时只记录日志，不继续安装。
4. 避免在插件构造函数中直接触发高风险原生 Hook；必要时延后到 Framework tick，并在失败后保持插件可加载。
5. 实机验证前先备份可启动版本，避免再次因为 Hook 安装导致整个游戏进程崩溃。

## 注意

- 当前工作区仍然是脏的，这是前面结构迁移遗留现场，不要执行 `git reset --hard` 或 `git checkout -- .`。
- `1.txt` 不要误删。
- 用户已明确说“先不拆了”，下一步优先让用户进游戏验证是否还能正常启动。
