# SESSION HANDOFF

## 2026-05-13 交接补充：设置页压缩 / Ikegami 设置区收尾

- 本轮详细交接已另存：`md/2026-05-13-settings-window-handoff.md`
- 本轮主要围绕 `DalamudACT/UI/SettingsWindow.cs` 持续压设置页空白，而不是继续改悬浮窗正文绘制。
- 已完成的关键点：
  - `DrawSettingCard(...)` 改成按实际内容高度自适应；
  - `样式分享码` 区域不再受固定卡片高度裁切影响；
  - `DrawHelpMarker(...)` / `DrawCompactHelp(...)` 已用于压缩说明文字；
  - Ikegami 设置区曾经出现的 `????` 文案已恢复成正常中文；
  - 滑条 / 下拉框 / checkbox 统一为“标题在上、控件在下”；
  - `透明度`、`Footer 与字号` 已压成 3 列紧凑布局。
- 这轮最关键的判断：
  - `样式分享码` 看起来像乱码，主要不是字符串坏了，而是高度不足导致的裁切 / 重叠 / 挤压。
- 本轮最近一次构建仍通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`
- 如果下一个会话继续沿设置页方向推进，建议优先：
  - 继续压 `尺寸与对齐`
  - 继续缩短各组底部帮助说明
  - 必要时再统一其它设置区的“标题在上、控件在下”样式
- 当前工作区仍然是脏的，`1.txt` 仍然不要误删。

## 2026-05-11 交接补充：DoT 状态驱动收尾

- 本轮已经把玩家 DoT 主链路改成“状态驱动 + 3 秒轮询补 tick”。
- 当前流程是：
  - 玩家施放已知 DoT 技能后先记录挂载候选；
  - 目标身上出现对应 debuff 后纳入活跃 DoT 状态；
  - `LocalStatsService.PollActivePlayerDots(...)` 负责按 3 秒节奏自动补 tick；
  - 目标不可选中时停止后续结算。
- DoT 暴击已切到普通技能那套模拟逻辑，不再依赖事件包里的直接 tick 暴击字段。
- `DalamudACT/Plugin/ACT.cs` 现在只负责：
  - 识别已知 DoT 技能；
  - 记录应用种子；
  - 让状态轮询接管后续 tick。
- `DalamudACT/Stats/PlayerDotCatalog.cs` 已作为静态白名单使用，按 `actionId / statusId` 过滤 DoT 候选，不再靠技能名字猜测。
- 本轮已经重新构建通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - 结果：`0 warnings / 0 errors`
- 目前仍建议下次接手前先做两件事：
  - 进游戏验证几个典型 DoT 职业是否稳定；
  - 再核对一遍 `PlayerDotCatalog.cs` 是否还缺白名单。
- 工作区依旧是脏的，尤其要注意：
  - `1.txt` 是未跟踪文件，不要误删。

## 2026-05-05 琛ュ厖锛氭浜＄粺璁′笌鎬?DPS 琛?

- `DPS` 闈㈡澘鏈€涓嬫柟鐨?`鎬籇PS / 鍏ㄩ槦` 姹囨€昏锛宍姝讳骸` 鍒楃幇鍦ㄦ樉绀哄叏闃熸浜℃€诲拰銆?
- 瀹炴椂姝讳骸缁熻涓嶅啀渚濊禆宸茬鐢ㄧ殑 `ActorControlSelf` Hook銆?
- 褰撳墠瀹炵幇鏀逛负鍦?`LocalStatsService.Update(...)` 涓疆璇?`PartyList`锛?
  - 璁板綍姣忎釜闃熷弸涓婁竴娆?`CurrentHP`
  - 鍙戠幇 `previousHp > 0 && currentHp == 0` 鏃讹紝璁颁竴娆℃浜?
  - 浠呭湪 `inCombat == true` 鎴栧綋鍓嶉伃閬囧凡缁忓紑濮嬫椂鐢熸晥锛岄伩鍏嶅緟鏈?鍒囧浘璇
- 鍏煎鍙?ID 瑙勫垯锛?
  - 浼樺厛浣跨敤 `member.GameObject?.EntityId`
  - 鍙栦笉鍒版椂鍥為€€鍒?`member.ObjectId`
- 杩欐剰鍛崇潃锛?
  - 鐜╁闃熷弸鍜?NPC 闃熷弸锛屽彧瑕佸嚭鐜板湪 `PartyList` 骞跺彂鐢?`HP > 0 -> 0`锛岄兘浼氳璁″叆姝讳骸
  - 濡傛灉鎻掍欢鏄湪瑙掕壊宸茬粡姝讳骸涔嬪悗鎵嶅惎鐢紝鍒欎笉浼氳ˉ璁拌繖娆″巻鍙叉浜★紱鍚庣画鏂扮殑姝讳骸浼氭甯哥粺璁?

鐩稿叧鏂囦欢锛?
- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/UI/StatsPanel.cs`

## 椤圭洰鐩爣

- 浠撳簱锛歚E:\git\DalamudACT`
- 鍙傝€冮」鐩細`E:\git\ACTX`
- 鏈€缁堢洰鏍囷細鎶?`DalamudACT` 鍋氭垚涓€涓?**鍙嫭绔嬭繍琛岀殑 Dalamud DPS 缁熻鎻掍欢**
- 绾︽潫锛?
  - 鏃х増 `DalamudACT` 鐨勬湰鍦?DPS 缁熻閫昏緫宸茬粡寮冪敤
  - 缁熻鍙ｅ緞銆佸睍绀烘ā鍨嬪拰涓昏杈撳嚭鏍煎紡浠?`ACTX / NotACT` 涓哄噯
  - 杩愯鏃朵笉鍐嶄緷璧栧閮?`ACTX / MiniParse`
  - 鎻掍欢鍚嶇О浣跨敤涓枃锛歚DPS缁熻`

## 褰撳墠鐘舵€?

### 宸插畬鎴?

- 宸叉妸鍘熷厛渚濊禆 `MiniParse` 鐨?UI 澶栧３鏀归€犳垚 **鏈湴缁熻鎻掍欢**
- 宸叉柊澧炴湰鍦扮粺璁℃湇鍔★細
  - `DalamudACT/Stats/LocalStatsService.cs`
- 宸插皢 UI 鏁版嵁婧愭敼涓烘湰鍦扮粺璁★紝鑰屼笉鏄?`MiniParseClient`
- 宸茬Щ闄わ細
  - `DalamudACT/UI/MiniParseClient.cs`
- 宸查噸鍐欐彃浠跺叆鍙ｅ拰 Hook 灞傦細
  - `DalamudACT/Plugin/ACT.cs`
- 宸茶ˉ涓?Dalamud 杩愯鏃跺吋瀹瑰眰锛?
  - `DalamudACT/DalamudApi.cs`
- 宸叉洿鏂版彃浠跺厓鏁版嵁涓庤鏄庯細
  - `DalamudACT/DalamudACT.json`
  - `Data/DalamudACT.json`
  - `DalamudACT/DalamudACT.csproj`

### 褰撳墠鍙繍琛屽舰鎬?

- 鎻掍欢鐜板湪浠?**鏈湴閲囬泦 + 鏈湴缁熻 + 鏈湴 UI 灞曠ず** 杩愯
- UI 浣跨敤涓枃
- 鎻掍欢鍙互鐙珛鍔犺浇锛屼笉鍐嶄緷璧栧閮?`MiniParse`
- 褰撳墠缁熻涓婚摼璺潵鑷?`ActionEffect` 浜嬩欢

### 褰撳墠鏋勫缓缁撴灉

宸查獙璇佸彲浠ユ垚鍔熸瀯寤猴細

```powershell
dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Debug
```

鏈€杩戜竴娆＄粨鏋滐細

- `0 warnings`
- `0 errors`

浜х墿浣嶇疆锛?

- `E:\git\DalamudACT\output\DalamudACT.dll`

## 鏈疆鍏抽敭淇

### 1. SigScanner 娉ㄥ叆鍏煎

褰撳墠 Dalamud 杩愯鏃朵腑锛岀洿鎺ユ敞鍏ユ棫鐗?`ISigScanner` / `SigScanner` 浼氬け璐ャ€?

宸查噰鐢ㄥ拰 `E:\git\StarlightBreaker` 鐩稿悓鐨勫吋瀹规€濊矾锛?

- 娉ㄥ叆 `IGameInteropProvider`
- 閫氳繃鍙嶅皠浠?`GameInteropProvider` 鍐呴儴鍙栧嚭 scanner
- 閫氳繃 `DalamudApi.ScanText(...)` 缁熶竴鎵弿绛惧悕

鐩稿叧鏂囦欢锛?

- `DalamudACT/DalamudApi.cs`

### 2. 鍚敤鎻掍欢鍚庡穿婧冪殑鐩存帴鍘熷洜

`E:\git\crash\dalamud.log` 涓凡缁忓潗瀹烇紝鏈疆宕╂簝鐨勭涓€瑙﹀彂鐐逛笉鏄?`SigScanner`锛岃€屾槸锛?

```text
System.MissingMethodException:
Method not found:
'UInt16 Dalamud.Plugin.Services.IClientState.get_TerritoryType()'
```

瑙﹀彂璺緞锛?

- `DalamudACT.ACT.GetPlaceName()`
- `DalamudACT.ACT.OnFrameworkUpdate(IFramework framework)`

涔熷氨鏄锛屾彃浠跺姞杞芥垚鍔熷悗锛屽湪 `Framework.Update` 鍛ㄦ湡閲岃皟鐢ㄤ簡褰撳墠杩愯鏃朵笉瀛樺湪鐨?`IClientState.TerritoryType`锛屽紓甯告寔缁姏鍑猴紝鏈€缁堟妸瀹夸富鎷栧穿銆?

### 3. 鏈疆绋冲畾鍖栧鐞?

宸插仛浠ヤ笅淇濆畧淇锛?

- 鍦?`DalamudApi.cs` 涓柊澧炲弽灏勫吋瀹瑰叆鍙ｏ細
  - `GetTerritoryTypeId()`
  - `GetLocalPlayerName()`
- `ACT.cs` 涓笉鍐嶇洿鎺ヨ皟鐢細
  - `ClientState.TerritoryType`
  - `ClientState.LocalPlayer`
- `ACT.cs` 涓负 `OnFrameworkUpdate(...)` 澧炲姞闃叉姢锛?
  - 棣栨寮傚父璁板綍鏃ュ織
  - 鍚庣画涓嶈寮傚父缁х画鍐插嚮娓告垙涓诲惊鐜?
- 鍙繚鐣欐渶绋崇殑 `ActionEffectHandler.Receive` Hook
- 鏆傛椂绂佺敤楂橀闄?Hook锛?
  - `ActorControlSelf`
  - `Cast`

## 褰撳墠琛屼负鍙樺寲

### 宸蹭繚鐣?

- 鏈湴 DPS / HPS / 鎵夸激缁熻涓婚潰鏉?
- ACTX 椋庢牸鐨勬湰鍦拌緭鍑烘ā鍨?
- 涓枃 UI
- 鎻掍欢鐙珛杩愯

### 褰撳墠琚檷绾х殑鑳藉姏

鐢变簬 `ActorControlSelf` 鍜?`Cast` 鏆傛椂琚鐢紝褰撳墠鐗堟湰鏄?**鍏煎浼樺厛妯″紡**锛屼細甯︽潵浠ヤ笅宸茬煡缂哄彛锛?

- DoT 褰掑洜涓嶅畬鏁?
- HoT 褰掑洜涓嶅畬鏁?
- Death 缁熻閾捐矾涓嶅畬鏁?
- 涓€浜涗緷璧?`ActorControlSelf` / `Cast` 鐨勯伃閬囨椿璺冨害琛ュ己閫昏緫琚叧闂?

鎹㈠彞璇濊锛?

- 褰撳墠鐗堟湰浼樺厛淇濊瘉 **鎻掍欢鑳界ǔ瀹氬姞杞藉苟杩愯**
- 缁熻鍔熻兘浠?`ActionEffect` 涓婚摼璺负鏍稿績
- 缁嗚妭琛ュ叏闇€瑕佸悗缁湪纭绛惧悕鍜岃皟鐢ㄧ害瀹氬畨鍏ㄥ悗鍐嶆仮澶?

## 褰撳墠鏈€閲嶈鐨勬枃浠?

- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/DalamudApi.cs`
- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/UI/PluginUI.cs`
- `DalamudACT/UI/MainWindow.cs`
- `DalamudACT/UI/SettingsWindow.cs`
- `DalamudACT/UI/StatsPanel.cs`

## 褰撳墠缁撹

### 宸茬‘璁?

- 鎻掍欢宸蹭笉鍐嶄緷璧?`MiniParse`
- 鎻掍欢鍙垚鍔熺紪璇?
- `SigScanner` 娉ㄥ叆闂宸茬粫寮€
- `IClientState.TerritoryType` 鍏煎闂宸插鐞?
- 楂橀闄?Hook 宸查檷绾э紝閬垮厤鐩存帴鎶婃父鎴忔墦宕?

### 杩樹笉鑳藉绉板畬鍏ㄥ畬鎴愮殑閮ㄥ垎

- 杩樻病鏈夎瘉鏄庡綋鍓嶇増鏈湪鐢ㄦ埛鏈哄櫒涓婂凡缁忛暱鏈熺ǔ瀹氳繍琛?
- 杩樻病鏈夋仮澶?`ActorControlSelf / Cast` 鐩稿叧缁熻琛ラ摼
- 杩樻病鏈夋妸 DoT / HoT / Death 鐨勮兘鍔涘畬鏁存仮澶嶅埌鐩爣鐘舵€?

## 涓嬩竴姝ュ缓璁?

### 浼樺厛椤哄簭

1. 鍏堣鐢ㄦ埛鐢ㄥ綋鍓?`output\DalamudACT.dll` 閲嶆柊瀹炴祴
2. 纭鏄惁杩樹細鍦ㄥ惎鐢ㄦ彃浠跺悗绔嬪嵆宕╂簝
3. 濡傛灉涓嶅穿锛屽啀鏍稿褰撳墠缁熻闈㈡澘鏄惁鑳界ǔ瀹氬嚭鏁?
4. 鍦ㄧǔ瀹氬熀纭€涓婏紝鍐嶆仮澶嶈绂佺敤鐨勮兘鍔?

### 濡傛灉缁х画寮€鍙?

鎺ㄨ崘鎸変笅闈㈤『搴忔帹杩涳細

1. 鍏堥獙璇?`ActionEffect` 涓婚摼璺槸鍚︾ǔ瀹?
2. 鍐嶅崟鐙仮澶?`Cast` Hook锛屽苟鍗曠嫭娴嬭瘯
3. 鏈€鍚庢仮澶?`ActorControlSelf`锛屽苟閲嶆柊鏍￠獙绛惧悕涓庤皟鐢ㄧ害瀹?
4. 琛ラ綈锛?
   - DoT
   - HoT
   - Death
   - 閬亣鐢熷懡鍛ㄦ湡琛ュ己

### 鎭㈠楂橀闄?Hook 鏃跺繀椤绘敞鎰?

- 涓嶈涓€娆℃€ф妸鎵€鏈?Hook 閮藉紑鍥炲幓
- 姣忎釜 Hook 瑕佺嫭绔?`try/catch` 鍜岀嫭绔嬫棩蹇?
- 浠讳綍涓€涓?Hook 澶辫触閮藉簲璇ラ檷绾х户缁繍琛岋紝鑰屼笉鏄鎻掍欢鎴栨父鎴忛€€鍑?

## 闇€瑕佸弬鑰冪殑鏃ュ織

宕╂簝鍜屽姞杞芥棩蹇椾綅缃細

- `E:\git\crash\crash.log`
- `E:\git\crash\dalamud.log`
- `E:\git\crash\output.log`
- `E:\git\crash\trouble.json`

鏈疆瀹氫綅鏃舵渶鍏抽敭鐨勬椂闂寸偣锛?

- `2026-05-05 18:55:50 +08:00`

## 鎺ユ墜鏃跺厛鐪嬩粈涔?

濡傛灉涓嬩竴杞缁х画鎺ユ墜锛屽缓璁厛鐪嬶細

1. 鏈枃浠?`md/SESSION-HANDOFF.md`
2. `DalamudACT/Plugin/ACT.cs`
3. `DalamudACT/DalamudApi.cs`
4. `DalamudACT/Stats/LocalStatsService.cs`
5. 鏈€鏂颁竴娆?`E:\git\crash\dalamud.log`

## 棰濆璇存槑

- 浠撳簱褰撳墠鏄剰宸ヤ綔鍖猴紝涓嶈浣跨敤鐮村潖鎬?git 鎿嶄綔锛?
  - `git reset --hard`
  - `git checkout --`
- 濡傛灉闇€瑕佹仮澶嶆棫鏂囦欢鍐呭锛屼紭鍏堜娇鐢細

```powershell
git -C E:\git\DalamudACT show HEAD:<path>
```

- 褰撳墠浜х墿浠?`output\DalamudACT.dll` 涓哄噯

## 2026-05-09 琛ュ厖锛氭垬鏂楁暟鎹仮澶嶄笌鐗堟湰鍙风粺涓€

### 鏈疆鐜拌薄

- 鎻掍欢涓嶅啀宕╂簝锛屼絾鐢ㄦ埛杩涘叆鎴樻枟鍚庣晫闈粛鍋滅暀鍦ㄢ€滅瓑寰呮垬鏂楁暟鎹?..鈥濄€?
- 鎻掍欢绐楀彛鏍囬鏄剧ず `v0.15.2.7`锛岃€屾彃浠剁鐞嗙晫闈㈠彸渚ф樉绀?`v0.15.2.5`锛屽嚭鐜板弻鐗堟湰鍙枫€?

### 鏈疆纭

- `ActionEffect` Hook 宸叉垚鍔熷畨瑁咃紱`dalamud.log` 涓病鏈夊嚭鐜帮細
  - `Failed to install the ActionEffect hook`
  - `live DPS data will be unavailable`
- 闂涓嶅湪 Hook 瀹夎澶辫触锛岃€屽湪浜庝簨浠舵潵婧愯瘑鍒笌璺熻釜瀵硅薄鍖归厤涓嶇ǔ瀹氥€?

### 鏈疆淇

1. **绉婚櫎楂橀闄╃殑 PronounModule 闃熶紞妲戒綅瑙ｆ瀽**
   - 涓嶅啀渚濊禆 `PronounModule.ResolvePlaceholder("<1>..<8>")`
   - 鏀逛负鍩轰簬 `PartyList / BuddyList / ObjectTable` 瑙ｆ瀽鍙窡韪璞?
   - 鐩殑锛氶伩鍏?`MissingMethodException` / 杩愯鏃剁鍚嶅彉鍖栧啀娆″鑷村穿婧?

2. **缁熶竴瀵硅薄韬唤妯″瀷**
   - 鍦?`LocalStatsService.cs` 涓紩鍏ョ粺涓€韬唤鍙ｅ緞锛?
     - `GameObjectId (ulong)`
     - `ActorId (uint, low32)`
     - `ObjectId`
     - `EntityId`
   - 鎵€鏈夊尮閰嶄笌鍥炴煡閫昏緫缁熶竴璧?`ActorIdentity`
   - 鍘绘帀娈嬬暀鐨勨€滄寜鍚嶅瓧鍏滃簳鍖归厤鈥濓紝閬垮厤璇妸闃熷鍚屽悕瀵硅薄绠楄繘鏉?

3. **琛ュ己鏈湴鐜╁璇嗗埆**
   - `DalamudApi.cs` 鏂板锛?
     - `GetLocalPlayerGameObjectId()`
     - `GetLocalPlayerEntityId()`
     - `GetLocalPlayerObjectId()`
   - `TryGetLocalPlayerTrackedActor(...)` 鏀逛负鎸夊 ID 鍙ｅ緞鍖归厤锛岃€屼笉鏄彧鎷垮崟涓€ `EntityId/ObjectId`

4. **琛ュ己浜嬩欢鏉ユ簮璇嗗埆**
   - `ACT.cs` 涓 `ActionEffect` 浜嬩欢鏉ユ簮涓嶅啀鍙俊浠?`sourceId`
   - 鐜板湪浼樺厛灏濊瘯锛?
     - `sourceId`
     - 鑻ヤ笉瓒充互瑙ｆ瀽涓?tracked source锛屽啀浣跨敤 `sourceCharacter` 鍦板潃鍥炶〃鍒涘缓瀵硅薄寮曠敤骞舵彁鍙?ID
   - 鍗曚汉 / NPC 鍚岃 / 闈炴爣鍑嗛槦浼嶅満鏅笅锛岃繖涓€姝ュ鎭㈠瀹炴椂鍑烘暟寰堝叧閿?

5. **鏀剁揣鎴樻枟娲诲姩鍒ゅ畾**
   - 鍙涓庘€滆嚜宸?/ 褰撳墠闃熶紞 / 鍙綊灞炴潵婧愨€濇湁鍏崇殑 `ActionEffect` 鎺ㄥ姩 encounter activity
   - 閬垮厤闄勮繎鏃犲叧鎴樻枟鎶婂綋鍓嶉伃閬囪寮€鎴樻垨璇画鎴?

### 鏈疆缁撴灉

- 鐢ㄦ埛渚у凡纭锛?*杩涘叆鎴樻枟鍚庡凡缁忓紑濮嬪嚭鏁版嵁**銆?
- 璇存槑褰撳墠 `ActionEffect -> tracked source/target -> LocalStatsService` 杩欐潯涓婚摼璺凡缁忔墦閫氥€?
- 宸叉妸鎮诞绐楁樉绀鸿涓烘敼涓猴細鑴辨垬鍚庝繚鐣欎笂涓€鍦烘暟鎹紝鍦ㄤ笅涓€娆¤繘鍏ユ垬鏂楁椂鍐嶅厛娓呯┖骞剁瓑寰呮柊鎴樻枟鏁版嵁銆?- 宸叉妸鐘舵€佹枃妗堣繘涓€姝ョ粏鍖栵細鑴辨垬淇濈暀鏃ф暟鎹椂鏄剧ず鈥滅瓑寰呬笅涓€鍦烘垬鏂椻€濓紝閲嶆柊杩涙垬鏂椾絾灏氭湭鍑虹涓€鏉℃暟鎹椂鏄剧ず鈥滄鍦ㄦ敹闆嗘柊鎴樻枟鏁版嵁鈥濄€?
### 鐗堟湰鍙峰垎瑁傚師鍥犱笌澶勭悊

- 绐楀彛鏍囬鐗堟湰鏉ヨ嚜绋嬪簭闆嗙増鏈細
  - `DalamudACT/DalamudACT.csproj`
  - 褰撴椂涓?`0.15.2.8`
- 鎻掍欢绠＄悊鐣岄潰鐗堟湰鏉ヨ嚜鎻掍欢鍏冩暟鎹細
  - `Data/DalamudACT.json`
  - `DalamudACT/DalamudACT.json`
  - `repo.json`
  - 褰撴椂浠嶅仠鐣欏湪 `0.15.2.5`

宸茬粺涓€涓猴細

- `0.15.2.8`

鍚屾鏂囦欢锛?

- `DalamudACT/DalamudACT.csproj`
- `Data/DalamudACT.json`
- `DalamudACT/DalamudACT.json`
- `repo.json`

骞跺悓姝ヤ簡 `repo.json` 涓殑锛?

- `AssemblyVersion`
- `TestingAssemblyVersion`
- `DownloadLinkInstall`
- `DownloadLinkTesting`
- `DownloadLinkUpdate`

### 鏈疆鏀瑰姩鐨勬牳蹇冩枃浠?

- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/DalamudApi.cs`
- `DalamudACT/Stats/LocalStatsService.cs`
- `Data/DalamudACT.json`
- `DalamudACT/DalamudACT.json`
- `repo.json`

### 褰撳墠寤鸿

1. 缁х画鍦ㄤ互涓嬪満鏅疄娴嬶細
   - 鍗曚汉 / NPC 鍚岃鎴樻枟
   - 鏅€?4/8 浜洪槦浼?
   - Buddy / 瀹犵墿 / 鍙敜鐗╁綊灞?
2. 鐩墠鍏堜繚鎸?`ActorControlSelf / Cast` 鍏抽棴锛岄伩鍏嶄负浜嗚ˉ鍔熻兘閲嶆柊寮曞叆涓嶇ǔ瀹氬洜绱?
3. 濡傚悗缁噯澶囨寮忓彂甯冿紝鍐嶆妸锛?
   - `CHANGELOG`
   - `RELEASE-NOTES`
   - Git tag / Release 璧勬簮
   涓€骞跺悓姝ュ埌 `0.15.2.8`

## 2026-05-09 琛ュ厖锛氭棩蹇楃郴缁熸暣鐞嗐€佹渶杩戞棩蹇楁憳瑕併€佷互鍙婂綋鍓嶅緟缁х画闂

### 鏈疆鐩爣

鏈疆涓昏鍥寸粫涓や欢浜嬫帹杩涳細

1. 鍙傝€冨閮ㄩ」鐩殑 `LogHelper.cs`锛屼负褰撳墠椤圭洰琛ヤ竴濂楃粺涓€鏃ュ織鍏ュ彛锛?
2. 缁欎富绐楀彛 / 璁剧疆绐楀彛澧炲姞鈥滄渶杩戞棩蹇楁憳瑕佲€濓紝鐢ㄤ簬鍦ㄦ父鎴忓唴蹇€熺湅鍒版彃浠舵渶杩戠姸鎬侊紱
3. 璺熻繘鐢ㄦ埛鎴浘閲屸€滄渶杩戞棩蹇楁憳瑕佷竴鐩翠负绌衡€濈殑鍙嶉锛岀‘璁よ繖鍧楀綋鍓嶅埌搴曟槸涓嶆槸 bug銆?

### 鏈疆宸插畬鎴愮殑鏀瑰姩

#### 1锛夋柊澧炵粺涓€鏃ュ織甯姪绫?

鏂板鏂囦欢锛?

- `DalamudACT/LogHelper.cs`

褰撳墠宸茬粡缁熶竴鏀寔锛?

- `Info / Warning / Error / Debug / Verbose`
- 妯″潡鍓嶇紑褰㈠紡锛?
  - `LogHelper.Info("ACT", "...")`
  - `LogHelper.Warning("Stats", "...")`
  - `LogHelper.Error("Settings", ex, "...")`
- 璋冭瘯鏃ュ織寮€鍏筹細
  - `LogHelper.DefaultEnableDebugLog`
  - `LogHelper.EnableDebugLog`
- 鏈€杩戞棩蹇楃紦瀛橈細
  - `LogHelper.RecentLogs`
  - `LogHelper.RecentInfos`
  - `LogHelper.ClearRecentLogs()`
- 鑱婂ぉ妗嗚緭鍑猴細
  - `Print(...)`
  - `PrintError(...)`
  - `PrintWithModule(...)`
  - `PrintErrorWithModule(...)`

#### 2锛塂alamud 鏈嶅姟娉ㄥ叆琛ュ厖

淇敼鏂囦欢锛?

- `DalamudACT/DalamudApi.cs`

宸插鍔狅細

- `IChatGui ChatGui`

鐢ㄤ簬鎶婃彃浠剁姸鎬佹彁绀哄悓姝ヨ緭鍑哄埌娓告垙鑱婂ぉ妗嗐€?

#### 3锛夐厤缃笌璁剧疆椤垫帴鍏?

淇敼鏂囦欢锛?

- `DalamudACT/Configuration/PluginConfiguration.cs`
- `DalamudACT/UI/SettingsWindow.cs`

宸插畬鎴愶細

- 閰嶇疆椤?`EnableDebugLog`
- 閰嶇疆鐗堟湰鎻愬崌鍒?`24`
- 璁剧疆椤垫柊澧炩€滄棩蹇椾笌璋冭瘯鈥濆崱鐗?
- 鍙垏鎹⑩€滃惎鐢ㄨ皟璇曟棩蹇椻€?
- 璁剧疆椤靛彲鏌ョ湅鏈€杩戞棩蹇楁憳瑕?
- 鏀寔锛?
  - 澶嶅埗鍏ㄩ儴
  - 娓呯┖鎽樿
  - 宸插鍒?/ 宸叉竻绌?杞婚噺鍙嶉

#### 4锛変富绐楀彛鏂板鏈€杩戠姸鎬佹憳瑕?

淇敼鏂囦欢锛?

- `DalamudACT/UI/MainWindow.cs`
- `DalamudACT/UI/LogUiHelper.cs`

宸插畬鎴愶細

- 涓荤獥鍙ｆ柊澧炩€滄渶杩戠姸鎬佹憳瑕佲€濆崱鐗?
- 榛樿鏄剧ず鏈€杩?4 鏉℃棩蹇?
- 棰滆壊鍖哄垎 `INFO / WARN / ERROR`
- tooltip 鍙湅瀹屾暣鏃堕棿涓庡畬鏁存棩蹇楀唴瀹?
- 鏀寔鍗曟潯澶嶅埗鍏ㄦ枃銆佸鍒跺叏閮ㄣ€佹竻绌烘憳瑕?

#### 5锛変笟鍔℃棩蹇楁帴鍏?

淇敼鏂囦欢锛?

- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/Stats/LocalStatsService.cs`

鐩墠宸叉帴鍏ヨ緝澶?`Info / Warning / Error` 鎴栬亰澶╁弽棣堢殑鍦烘櫙锛?

- ActionEffect Hook 瀹夎鎴愬姛 / 澶辫触
- 鍏煎妯″紡涓?ActorControlSelf / Cast hook 绂佺敤 warning
- 娓呯┖鍘嗗彶
- 瀵煎叆娴嬭瘯鏁版嵁
- 瀵煎嚭鍘嗗彶璁板綍
- 瀵煎叆鍘嗗彶璁板綍
- 鎭㈠榛樿
- 閲嶇疆缁熻椤靛垪瀹借蹇?
- 閲嶇疆鍘嗗彶椤靛垪瀹借蹇?
- 鍘嗗彶棰勮瓒婄晫 warning
- encounter finalize debug

### 褰撳墠宸ヤ綔鍖虹姸鎬侊紙寮€鏂颁細璇濆墠璇峰厛纭锛?

褰撳墠 `git status --short`锛?

- 宸蹭慨鏀癸細
  - `DalamudACT/Configuration/PluginConfiguration.cs`
  - `DalamudACT/DalamudApi.cs`
  - `DalamudACT/Plugin/ACT.cs`
  - `DalamudACT/Stats/LocalStatsService.cs`
  - `DalamudACT/UI/MainWindow.cs`
  - `DalamudACT/UI/SettingsWindow.cs`
- 鏈窡韪細
  - `DalamudACT/LogHelper.cs`
  - `DalamudACT/UI/LogUiHelper.cs`
  - `1.txt`

娉ㄦ剰锛?

- `1.txt` 鏄箣鍓嶇暀涓嬬殑鏈窡韪枃浠讹紝**涓嶈璇垹**锛?
- 鏈疆娌℃湁鍋氱牬鍧忔€?git 鎿嶄綔锛?
- 鏈疆鏈€鍚庢病鏈夐噸鏂拌窇鏋勫缓锛岃嫢涓嬩竴浼氳瘽缁х画鏀逛唬鐮侊紝寤鸿鍏堟墽琛屼竴娆★細

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

### 鍏充簬鈥滄渶杩戞棩蹇楁憳瑕佷负浠€涔堜竴鐩存槸绌虹殑鈥濈殑褰撳墠鍒ゆ柇

鐢ㄦ埛鏈€鏂版埅鍥句腑锛?

- 鍙充晶 DPS 闈㈡澘宸茬粡鏈夊疄鏃舵暟鎹紱
- 宸︿晶璁剧疆椤典腑鈥滄渶杩戞棩蹇楁憳瑕佲€濅粛鏄剧ず鈥滄殏鏃犳渶杩戞棩蹇椻€濓紱
- 鐢ㄦ埛宸插嬀閫夆€滃惎鐢ㄨ皟璇曟棩蹇椻€濄€?

褰撳墠婧愮爜涓嬶紝杩欎釜鐜拌薄**涓嶄竴瀹氭槸 bug**锛屾洿鍍忔槸鈥滃姛鑳藉畾涔夊拰鐢ㄦ埛棰勬湡涓嶄竴鑷粹€濓細

#### 褰撳墠瀹炵幇閫昏緫

鈥滄渶杩戞棩蹇楁憳瑕佲€濆彧缂撳瓨锛?

- `Info`
- `Warning`
- `Error`

涓嶄細缂撳瓨锛?

- `Debug`
- `Verbose`
- 姝ｅ父鎴樻枟杩囩▼涓殑姣忔浼ゅ / 娌荤枟 / DOT tick

涔熷氨鏄锛?

- 鍙宠竟 DPS 鏈夋暟鎹紝璇存槑缁熻閾捐矾鍦ㄥ伐浣滐紱
- 浣嗗彧瑕佹渶杩戞病鏈夎Е鍙?`Info / Warning / Error`锛屽乏杈规憳瑕佸氨鍙互涓€鐩翠负绌猴紱
- 鍕鹃€夆€滃惎鐢ㄨ皟璇曟棩蹇椻€濆彧鏄 `Debug / Verbose` 鑳藉啓鍏?Dalamud 鏃ュ織锛屼笉浠ｈ〃瀹冧滑浼氬嚭鐜板湪鈥滄渶杩戞棩蹇楁憳瑕佲€濋噷銆?

#### 鍥犳锛屽綋鍓嶅簲鍚戜笅涓€浼氳瘽鏄庣‘鐨勭粨璁?

1. **鈥滄渶杩戞棩蹇楁憳瑕佲€濅笉鏄垬鏂楁祦姘撮潰鏉?*
   瀹冧笉鏄敤鏉ュ睍绀烘瘡娆℃妧鑳藉懡涓€丏OT tick銆佹不鐤?tick 鐨勩€?

2. **鍚敤璋冭瘯鏃ュ織涓嶄細鑷姩濉弧鎽樿**
   鍥犱负 `Debug / Verbose` 鐩墠娌℃湁杩涘叆 `RecentLogs` 闃熷垪銆?

3. **濡傛灉鐢ㄦ埛鍒氭竻绌鸿繃鎽樿锛岀劧鍚庡彧鏄户缁甯告垬鏂楋紝鎽樿涓虹┖鏄彲鑳芥甯哥殑**

#### 浣嗕粛鏈変竴涓€煎緱缁х画纭鐨勫皬鐐?

鎸夊綋鍓嶆簮鐮侊紝鎻掍欢鍔犺浇鏃剁悊璁轰笂閫氬父浼氭湁鑷冲皯涓€鏉★細

- `[WARN] [ACT] ActorControlSelf and cast hooks are disabled ...`

寰堝鎯呭喌涓嬭繕浼氬啀鏈変竴鏉★細

- `[INFO] [ACT] Installed the ActionEffect hook for live combat statistics.`

鎵€浠ュ鏋滅敤鎴锋槸锛?

- 鍒氬姞杞?/ 閲嶈浇鎻掍欢锛?
- 娌℃湁鎵嬪姩娓呯┖鎽樿锛?
- 涔熸病鏈夊垏鎹㈠埌鏂扮殑浼氳瘽鐜锛?

缁撴灉鎽樿浠嶅畬鍏ㄤ负绌猴紝閭ｄ箞杩橀渶瑕佺户缁‘璁や袱绉嶅彲鑳斤細

1. 褰撳墠杩愯鐨?dll 骞朵笉鏄繖浠芥渶鏂版簮鐮佹瀯寤哄嚭鏉ョ殑鐗堟湰锛?
2. 鏈€杩戞棩蹇楃紦瀛橀摼璺湰韬瓨鍦ㄩ棶棰樸€?

### 宸茬粰鐢ㄦ埛鐨勫缓璁?/ 涓嬩竴浼氳瘽鍙洿鎺ュ鐢?

寤鸿鐢ㄦ埛鍋氫竴涓渶绠€鍗曡嚜娴嬶細

1. 鍦ㄨ缃〉鎶娾€滃惎鐢ㄨ皟璇曟棩蹇椻€濆彇娑堬紱
2. 鍐嶉噸鏂板嬀鍥炲幓銆?

鎸夊綋鍓嶄唬鐮侊紝鐞嗚涓婂簲璇ョ珛鍒诲啓鍏ヤ袱鏉?`Info`锛?

- `Debug logging disabled from settings.`
- `Debug logging enabled from settings.`

濡傛灉杩欐牱鍋氫箣鍚庢憳瑕佸嚭鐜板唴瀹癸細

- 璇存槑鎽樿鍔熻兘鏈韩姝ｅ父锛?
- 鍙槸鐢ㄦ埛涔嬪墠娌℃湁瑙﹀彂浼氳繘鍏ユ憳瑕佺殑 `Info/Warn/Error` 浜嬩欢銆?

濡傛灉杩欐牱鍋氫箣鍚庢憳瑕佷粛鐒跺畬鍏ㄤ负绌猴細

- 鏇村儚鏄繍琛岀増鏈笉涓€鑷达紱
- 鎴栬€呮憳瑕佺紦瀛?/ UI 灞曠ず閾捐矾鏈夐棶棰橈紱
- 杩欐椂灏卞€煎緱鐩存帴鍔犳洿寮虹殑璋冭瘯鏃ュ織鎴栫幇鍦烘瘮瀵?dll銆?

### 涓嬩竴浼氳瘽鏈€鎺ㄨ崘鐨勭户缁柟鍚?

濡傛灉鐩爣鏄€滆鐢ㄦ埛瑙夊緱杩欎釜鎽樿鐪熸鏈夌敤鈥濓紝浼樺厛绾у缓璁涓嬶細

#### 鏂瑰悜 A锛氳鏈€杩戞棩蹇楁憳瑕佷篃鏀堕泦 Debug / Verbose

杩欐槸鏈€鐩存帴鐨勬柟妗堛€?
鐢ㄦ埛鏃㈢劧鍕鹃€変簡鈥滃惎鐢ㄨ皟璇曟棩蹇椻€濓紝閫氬父涔熶細鑷劧鏈熷緟鎽樿鍖鸿兘鐪嬪埌杩欎簺璋冭瘯淇℃伅銆?

寤鸿鏀规硶锛?

- `LogHelper.Debug(...)` 鍜?`LogHelper.Verbose(...)`
  - 鍦?`EnableDebugLog == true` 鏃朵篃杩涘叆 `RecentLogs`
- 鍙互缁х画鍙湪 UI 鎽樿閲屾樉绀烘渶杩?10 鏉★紝閬垮厤鍒峰お澶?

#### 鏂瑰悜 B锛氭彃浠跺垵濮嬪寲鏃朵富鍔ㄥ啓涓€鏉℃憳瑕佹棩蹇?

渚嬪锛?

- `LogHelper.Info("ACT", "Recent log summary is ready.")`

杩欐牱鐢ㄦ埛涓€鎵撳紑璁剧疆椤靛氨涓嶄細鐪嬪埌鈥滅┖鐧藉尯鍩熲€濓紝瑙傛劅鏇寸洿瑙傘€?

#### 鏂瑰悜 C锛氬鏋滅户缁拷鈥滃巻鍙插鍏ュ悗闈㈡澘鏃犳暟鎹?/ 鐘舵€佸垏鎹笉瀵光€濈殑鑰侀棶棰?

涔嬪墠宸茬粡鍒ゆ柇杩囦竴涓洿鍋忎笟鍔℃祦鐨勯棶棰橈細
鏈夎繃鈥滃鍏ュ巻鍙插悗鏃ュ織璇村凡鑷姩鎵撳紑鏈€鏂拌褰曪紝浣嗙晫闈粛鍍忕瓑寰呮垬鏂楁暟鎹€濈殑鐜拌薄銆?

濡傛灉涓嬩竴浼氳瘽缁х画杩借繖涓棶棰橈紝閲嶇偣杩樻槸搴斿姞绮惧噯 debug 鍒帮細

- `LocalStatsService.PreviewHistoricalRecordLocked(...)`
- `LocalStatsService.RefreshDisplayCombatDataLocked(...)`
- `LocalStatsService.TrySelectLatestHistoricalRecord()`
- `LocalStatsService.UpdateStatusText(...)`
- `ACT.HandleAbility(...)`

### 鏈疆鏈€鍏抽敭鐨勬枃浠?

- `DalamudACT/LogHelper.cs`
- `DalamudACT/DalamudApi.cs`
- `DalamudACT/Configuration/PluginConfiguration.cs`
- `DalamudACT/UI/LogUiHelper.cs`
- `DalamudACT/UI/SettingsWindow.cs`
- `DalamudACT/UI/MainWindow.cs`
- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/Stats/LocalStatsService.cs`

### 缁欎笅涓€浼氳瘽鐨勪竴鍙ヨ瘽鎬荤粨

褰撳墠鈥滄渶杩戞棩蹇楁憳瑕佷负绌衡€濊繖浠朵簨锛?*鏇村ぇ姒傜巼涓嶆槸缁熻鍧忎簡锛岃€屾槸鎽樿鍙敹 `Info/Warn/Error`銆佷笉鏀?`Debug/Verbose` 涔熶笉鏀舵垬鏂楁祦姘?*銆?
濡傛灉瑕佺户缁紭鍖栦綋楠岋紝浼樺厛鎶?`Debug / Verbose` 涔熸帴杩?`RecentLogs`锛屽苟鍦ㄦ彃浠跺惎鍔ㄦ椂琛ヤ竴鏉″垵濮嬪寲鏃ュ織銆?

## 2026-05-10 琛ュ厖锛氬崟浜鸿В闄?/ NPC 闃熷弸 / 骞讳綋鍓湰鎺掓煡

### 鏈疆鐩爣

鏈疆涓昏鍥寸粫浠ヤ笅鍥涗欢浜嬫帹杩涳細

1. 淇鍗曚汉瑙ｉ檺杩涘叆鍓湰鏃垛€滄湁杩涙垬浣嗘病鏈変换浣曟暟鎹€濈殑闂锛?2. 淇甯?NPC 闃熷弸 / 淇¤禆 / 骞讳綋杩涘叆鍓湰鏃跺彧鏄剧ず鐜╁銆佷笉鏄剧ず NPC 鍗曠嫭琛岀殑闂锛?3. 鎸夌敤鎴疯姹傦紝鎶娾€滄垬鏂楁祦姘撮噷鐨勮鑹茬瓫閫夆€濇敼鎴愪笅鎷夋锛?4. 鎺掓煡鈥滄湁杩涘叆鎴樻枟锛屼絾娌℃湁缁撴潫鎴樻枟鈥濈殑缁撶畻闂銆?
### 鐢ㄦ埛渚у凡缁忕‘璁よ繃鐨勭幇璞?
鎸夋椂闂撮『搴忥紝鐢ㄦ埛宸茬粡鍙嶉杩囪繖浜涘叧閿簨瀹烇細

- 鏈€鏃╁湪閮ㄥ垎鍓湰閲岋紝鏈湴鐜╁ LocalPlayer 鐩稿叧 ID 鍏ㄦ槸 锛屽鑷村崟浜鸿В闄愬満鏅棤娉曞懡涓彲璺熻釜瀵硅薄锛?- 淇ˉ鏈湴鐜╁韬唤鑾峰彇鍚庯紝鐜╁鑷繁鐨勬暟鎹凡缁忔仮澶嶆甯革細
  - 鎴樻枟娴佹按閲岃兘鐪嬪埌鐜╁鑷繁鐨勬妧鑳斤紱
  - DPS 闈㈡澘閲屾湁鐜╁鑷繁涓€琛岋紱
- 浣?NPC 骞讳綋浠嶇劧娌℃湁鍗曠嫭鎴愯锛?- 鐢ㄦ埛璐村洖鐨勮皟璇曟棩蹇楀娆＄ǔ瀹氬嚭鐜帮細
  - sourceObjectName=妗戝厠鐟炲痉鐨勫够浣?  - sourceObjectName=闃垮皵鑿茶鐨勫够浣?  - sourceObjectName=闃胯帀濉炵殑骞讳綋
  - 鍚屾椂 sourceTracked=False
- 杩欒鏄庯細浜嬩欢鐜板満鑳芥嬁鍒板够浣撳悕瀛椼€佸璞″紩鐢ㄥ拰浜嬩欢 ID锛屼絾杩樻病鏈夋垚鍔熸妸杩欎簺瀵硅薄娉ㄥ唽鎴?tracked actor锛?- 鐢ㄦ埛鍦ㄦ湰杞渶鍚庝竴娆″弽棣堟椂浠嶈〃绀猴細
  - **杩樻槸瀹屽叏娌℃湁骞讳綋琛?*锛?  - **鑰屼笖鏈夎繘鍏ユ垬鏂楋紝娌℃湁缁撴潫鎴樻枟**銆?
### 鏈疆宸插畬鎴愮殑浠ｇ爜鏀瑰姩

#### 1锛夋湰鍦扮帺瀹惰韩浠藉洖閫€閫昏緫

鏂囦欢锛?
- DalamudACT/DalamudApi.cs

宸叉敼涓轰紭鍏堜粠锛?
- ObjectTable.LocalPlayer

鍐嶅洖閫€鍒帮細

- ClientState.LocalPlayer

娑夊強鏂规硶鍖呮嫭锛?
- GetLocalPlayerName()
- GetLocalPlayerGameObjectId()
- GetLocalPlayerEntityId()
- GetLocalPlayerObjectId()
- GetLocalPlayerClassJobId()

杩欓儴鍒嗗凡缁忚鐢ㄦ埛瀹炴祴闂存帴纭鏈夋晥锛氱帺瀹惰嚜宸辩殑缁熻閾捐矾鎭㈠浜嗐€?
#### 2锛夋垬鏂楁祦姘寸瓫閫変氦浜掑凡鏀规垚涓嬫媺妗?
鏂囦欢锛?
- DalamudACT/UI/CombatTimelineWindow.cs

鍘熸湰鐨勮鑹茬瓫閫夊垏鎹㈠凡缁忔敼鎴愪笅鎷夋锛岀敤鎴锋彁鍑虹殑杩欓」 UI 闇€姹傚彲瑙嗕负宸插畬鎴愩€?
#### 3锛夋渶杩戞棩蹇楁憳瑕佹敮鎸佹樉绀鸿皟璇曟棩蹇?
鏂囦欢锛?
- DalamudACT/LogHelper.cs
- DalamudACT/UI/LogUiHelper.cs

宸叉柊澧烇細

- LogHelper.DebugRecent(...)

鐢ㄩ€旓細

- 鎶婂叧閿?debug 绾у埆鏃ュ織涔熸帹鍏モ€滄渶杩戞棩蹇楁憳瑕佲€濓紝鏂逛究鐢ㄦ埛鐩存帴鎴浘鍥炰紶锛岃€屼笉蹇呭己渚濊禆 dalamud.log銆?
#### 4锛堿ctionEffect 鏈懡涓棩蹇楀寮?
鏂囦欢锛?
- DalamudACT/Plugin/ACT.cs

鏂板浜嗘湭鍛戒腑鍙窡韪璞℃椂鐨勮皟璇曟棩蹇楋紝鍐呭鍖呮嫭锛?
- sourceId
- irstTargetId
- sourceTracked
- 	argetTracked
- sourceCharacter
- sourceObjectName
- 	argetObjectName
- sourceObjectGameObjectId
- sourceObjectId
- sourceEntityId
- localPlayerGameObjectId
- localPlayerObjectId
- localPlayerEntityId

骞朵笖鍋氫簡鑺傛祦涓庡悎骞惰鏁帮紝閬垮厤鍒峰睆銆?
杩欑粍鏃ュ織鏄湰杞畾浣嶁€滃够浣撲负浣曟病琚撼鍏ョ粺璁♀€濈殑鍏抽敭渚濇嵁銆?
#### 5锛夎繘鍏ユ垬鏂椾絾浠嶆棤娴佹按/缁熻鐨勮瘖鏂棩蹇?
鏂囦欢锛?
- DalamudACT/Stats/LocalStatsService.cs

澧炲姞浜嗚繖绫昏皟璇曟棩蹇楋細

- 宸茶繘鍏ユ垬鏂?N 绉掍絾浠嶆湭璁板綍浠讳綍鎴樻枟娴佹按鎴栫粺璁′簨浠?..

鐢ㄤ簬鍖哄垎锛?
- 鏄牴鏈病杩?encounter锛?- 杩樻槸杩涙垬浜嗕絾娌℃湁鍛戒腑 tracked actor锛?- 杩樻槸 UI 娌℃樉绀恒€?
#### 6锛夊紩鍏モ€滆瀵熷埌鐨勫弸鏂瑰璞♀€濈紦瀛?
鏂囦欢锛?
- DalamudACT/Stats/LocalStatsService.cs

鏂板锛?
- observedFriendlyActorCache
- ObserveFriendlyCombatantFromGameObject(...)
- ObserveFriendlyCombatantIdentity(...)
- TryCreateObservedFriendlyActor(...)
- LooksLikeDutyCompanionName(...)

褰撳墠瑙勫垯閲嶇偣瑕嗙洊鍚嶅瓧绫讳技锛?
- XX鐨勫够浣?
鐨勫璞°€?
鐩爣鏄細

- 涓嶅啀寮轰緷璧栬繖浜涘璞″繀椤诲凡缁忓嚭鐜板湪 PartyList / BuddyList锛?- 鍏佽鍦ㄦ垬鏂椾簨浠剁幇鍦哄姩鎬佹妸鍙嬫柟骞讳綋鏀剁紪杩?tracked actor 闆嗗悎銆?
#### 7锛夋湰杞渶鍚庢柊澧烇細鎸夆€滀簨浠惰韩浠?+ 鍚嶅瓧鈥濈洿鎺ユ敹缂栧够浣?
鏂囦欢锛?
- DalamudACT/Plugin/ACT.cs
- DalamudACT/Stats/LocalStatsService.cs

杩欐槸鏈疆鏈€鍚庝竴鐗堛€佷篃鏄渶鍏抽敭鐨勪竴缁勮ˉ涓併€?
鏂板鏍稿績鎬濊矾锛?
- 濡傛灉浠?GameObject -> IBattleChara 杩欐潯閾捐矾鏀剁紪澶辫触锛?- 浣嗕簨浠剁幇鍦哄凡缁忔嬁鍒颁簡锛?  - ctorId
  - sourceObjectName / 	argetObjectName
- 涓斿悕瀛楃鍚?XX鐨勫够浣擄紝
- 灏辩洿鎺ヨ皟鐢細
  - ObserveFriendlyCombatantIdentity(actorId, name)

涔熷氨鏄锛屽紑濮?*鐩存帴鎸変簨浠惰韩浠芥敞鍐屽弸鏂瑰够浣?*锛屼笉鍐嶅彧渚濊禆瀵硅薄鍖呰瀹屾暣鎬с€?
鍚屾椂锛?
- source 渚у鍔犱簡 TryObserveFriendlyCombatant(...) 杈呭姪閫昏緫锛?- target 渚т篃鍋氫簡鍚屾牱澶勭悊锛?- 鏂板鏃ュ織锛?  - 宸茬撼鍏ュ彲璺熻釜鍙嬫柟瀵硅薄锛?..
  - 宸叉寜浜嬩欢韬唤绾冲叆鍙窡韪弸鏂瑰璞★細...

杩欓儴鍒嗚ˉ涓?*宸茬粡缂栬瘧閫氳繃锛屼絾鎴嚦浜ゆ帴鏃惰繕娌℃湁鏀跺埌鐢ㄦ埛鐨勬柊涓€杞疄娴嬪弽棣?*銆?
#### 8锛夋垬鏂楃粨鏉熷垽瀹氭敹绱?
鏂囦欢锛?
- DalamudACT/Stats/LocalStatsService.cs

鏂板锛?
- ShouldCountBattleCharaForCombatEnd(...)

骞惰锛?
- AreAllPartyMembersOutOfCombat(...)

鍙粺璁＄湡姝ｅ簲璇ュ弬涓庣粨绠楀垽瀹氱殑瀵硅薄锛屼紭鍏堝寘鎷細

- 鏈湴鐜╁锛?- 闃熶紞鎴愬憳锛?- Buddy锛?- 宸茬撼鍏ヨ瀵熺紦瀛樼殑鐩稿叧鍙嬫柟 NPC / 骞讳綋锛?- 甯︽槑纭槦浼嶆爣璁扮殑瀵硅薄銆?
杩欐牱鍋氱殑鐩殑锛屾槸閬垮厤瀵硅薄琛ㄩ噷涓€浜涙棤鍏崇殑鍙嬫柟 BattleNpc 闀挎椂闂村甫鐫€ InCombat 鐘舵€侊紝瀵艰嚧 encounter 涓€鐩翠笉缁撶畻銆?
### 鎴嚦鏀跺伐鍓嶇殑鏄庣‘缁撹

#### 宸茬粡纭鏈夋晥鐨勯儴鍒?
1. **鐜╁閾捐矾宸叉仮澶?*
   - 鐜╁鑷繁鐨勬妧鑳戒細杩涘叆鎴樻枟娴佹按锛?   - 鐜╁鑷繁鐨?DPS 浼氭樉绀猴紱
   - 鏈湴鐜╁ ID 涓嶅啀绋冲畾涓?0銆?
2. **鎴樻枟娴佹按绛涢€変笅鎷夋宸插畬鎴?*

3. **鏈€杩戞棩蹇楁憳瑕佺幇鍦ㄥ彲浠ユ壙杞藉叧閿?debug 淇℃伅**

4. **鏈€鍚庝竴鐗堣ˉ涓佸彲浠ュ湪浠ｇ爜灞傜洿鎺ユ寜鈥渁ctorId + 骞讳綋鍚嶅瓧鈥濇敹缂栧弸鏂瑰璞?*
   - 杩欐槸瑙ｅ喅鈥滄棩蹇椾腑鑳界湅鍒板够浣撳悕瀛楋紝浣嗘案杩滀笉鍗曠嫭鎴愯鈥濈殑鏈€鏂板皾璇曪紱
   - 浣嗙洰鍓嶈繕缂烘渶鍚庝竴娆＄敤鎴风幇鍦洪獙璇併€?
#### 浠嶆湭瀹屾垚闂幆鐨勯儴鍒?
1. **骞讳綋鏄惁宸茬粡鑳藉崟鐙垚琛岋紝浠嶅緟鐢ㄦ埛澶嶆祴**
   - 鏀跺伐鍓嶆渶鍚庝竴娆＄敤鎴峰弽棣堣繕鏄€滄病鏈夊够浣撹鈥濓紱
   - 浣嗛偅娆″弽棣堝彂鐢熷湪鏈€鏂拌ˉ涓佸墠锛?   - 鏈€鏂拌ˉ涓佷箣鍚庯紝鐢ㄦ埛灏氭湭鍐嶆璐村浘鎴栬创鏃ュ織纭銆?
2. **鎴樻枟缁撴潫鍒ゅ畾鏄惁宸叉仮澶嶆甯革紝浠嶅緟鐢ㄦ埛澶嶆祴**
   - 鏈疆宸茬粡鏀剁揣浜嗗弬涓?combat-end 鍒ゅ畾鐨勫璞¤寖鍥达紱
   - 浣嗗皻鏈敹鍒扳€滆劚鎴樺悗鑳芥甯哥粨绠椻€濈殑鐜板満鍙嶉銆?
### 褰撳墠鏈€鍊煎緱鐪嬬殑鏂囦欢

濡傛灉涓嬩竴浣嶇户缁帴鎵嬶紝璇蜂紭鍏堟煡鐪嬶細

- DalamudACT/Plugin/ACT.cs
- DalamudACT/Stats/LocalStatsService.cs
- DalamudACT/DalamudApi.cs
- DalamudACT/UI/CombatTimelineWindow.cs
- DalamudACT/LogHelper.cs
- DalamudACT/UI/LogUiHelper.cs

閲嶇偣鍑芥暟锛?
- ACT.ResolveTrackedSourceActorId(...)
- ACT.HandleAbility(...)
- ACT.TryObserveFriendlyCombatant(...)
- LocalStatsService.ObserveFriendlyCombatantFromGameObject(...)
- LocalStatsService.ObserveFriendlyCombatantIdentity(...)
- LocalStatsService.TryGetTrackedActor(...)
- LocalStatsService.AreAllPartyMembersOutOfCombat(...)
- LocalStatsService.ShouldCountBattleCharaForCombatEnd(...)

### 褰撳墠宸ヤ綔鍖虹幇鍦猴紙浜ゆ帴鏃讹級

浜ゆ帴鏃?git status --short 涓猴細

- 淇敼锛?  - DalamudACT/Configuration/PluginConfiguration.cs
  - DalamudACT/DalamudApi.cs
  - DalamudACT/Plugin/ACT.cs
  - DalamudACT/Stats/LocalStatsService.cs
  - DalamudACT/UI/MainWindow.cs
  - DalamudACT/UI/PluginUI.cs
  - DalamudACT/UI/SettingsWindow.cs
  - md/SESSION-HANDOFF.md
- 鏈窡韪細
  - 1.txt
  - DalamudACT/LogHelper.cs
  - DalamudACT/UI/CombatTimelineWindow.cs
  - DalamudACT/UI/LogUiHelper.cs

娉ㄦ剰锛?
- 宸ヤ綔鍖轰粛鐒舵槸鑴忕殑锛?- 涓嶈鍋氱牬鍧忔€?git 鎿嶄綔锛?- 1.txt 浠嶇劧鏄棫鐜板満閲屼繚鐣欎笅鏉ョ殑鏈窡韪枃浠讹紝涓嶈璇垹銆?
### 鏈疆鏈€鍚庝竴娆℃湰鍦版瀯寤?
宸查獙璇侊細

`powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
`

缁撴灉锛?
-  warnings
-  errors

浜х墿锛?
- E:\git\DalamudACT\output\DalamudACT.dll

### 涓嬩竴浣嶆渶鎺ㄨ崘鐨勭户缁『搴?
1. 鍏堣鐢ㄦ埛鐢ㄥ綋鍓嶆渶鏂?output\DalamudACT.dll 澶嶆祴鈥滀俊璧?/ NPC 闃熷弸 / 骞讳綋鍓湰鈥濓紱
2. 閲嶇偣纭鏈€杩戞棩蹇楅噷鏄惁鍑虹幇锛?   - 宸茬撼鍏ュ彲璺熻釜鍙嬫柟瀵硅薄
   - 宸叉寜浜嬩欢韬唤绾冲叆鍙窡韪弸鏂瑰璞?3. 濡傛灉鍑虹幇浜嗕笂杩版棩蹇楋紝浣嗛潰鏉夸粛娌℃湁骞讳綋琛岋細
   - 缁х画妫€鏌?TryGetTrackedActor(...) 鍒?EncounterSession.EnsureCombatant(...) 涔嬮棿鏄惁杩樻湁 actorId 鍙ｅ緞涓嶄竴鑷达紱
4. 濡傛灉骞讳綋宸茬粡鎴愯锛屼絾鑴辨垬鍚庝粛涓嶇粨绠楋細
   - 缁х画缁?AreAllPartyMembersOutOfCombat(...) / ShouldFinalizeEncounter(...) / EnumerateTrackedPartyBattleCharas() 澧炲姞鐜板満鏃ュ織锛屾墦鍗板埌搴曟槸璋佷竴鐩村浜?InCombat锛?5. 鍦ㄦ湭鍏呭垎瀹炴祴鍓嶏紝涓嶅缓璁啀澶ц寖鍥存敼 Hook 灞傦紱
6. 褰撳墠鏈€璇ュ仛鐨勬槸鎶娾€滃够浣撳崟鐙垚琛?+ 鎴樻枟姝ｅ父缁撴潫鈥濊繖鏉￠棴鐜窇閫氥€?
### 缁欎笅涓€浣嶇殑涓€鍙ヨ瘽鎬荤粨

鏈疆宸茬粡鎶娾€滅帺瀹惰嚜宸辨棤鏁版嵁鈥濈殑闂淇€氾紝涔熸妸鈥滃够浣撲簨浠惰櫧鐒跺彲瑙佷絾涓嶈绾冲叆 tracked actor鈥濈殑鐥囩粨杩涗竴姝ユ敹鏁涘埌**浜嬩欢韬唤鏀剁紪**杩欎竴灞傦紱鏈€鏂拌ˉ涓佸凡缁忓厑璁哥洿鎺ユ寜 ctorId + 骞讳綋鍚嶅瓧 娉ㄥ唽鍙嬫柟瀵硅薄锛屽苟鏀剁揣浜?combat-end 鍒ゅ畾锛屼絾**鎴嚦浜ゆ帴鏃惰繕缂虹敤鎴锋渶鍚庝竴杞幇鍦洪獙璇?*銆

## 2026-05-10 补充：单人解限 / NPC 队友 / 幻体副本排查

### 本轮目标

本轮主要围绕以下四件事推进：

1. 修复单人解限进入副本时“有进战但没有任何数据”的问题；
2. 修复带 NPC 队友 / 信赖 / 幻体进入副本时只显示玩家、不显示 NPC 单独行的问题；
3. 按用户要求，把“战斗流水里的角色筛选”改成下拉框；
4. 排查“有进入战斗，但没有结束战斗”的结算问题。

### 用户侧已经确认过的现象

按时间顺序，用户已经反馈过这些关键事实：

- 最早在部分副本里，本地玩家 `LocalPlayer` 相关 ID 全是 `0`，导致单人解限场景无法命中可跟踪对象；
- 修补本地玩家身份获取后，玩家自己的数据已经恢复正常：
  - 战斗流水里能看到玩家自己的技能；
  - DPS 面板里有玩家自己一行；
- 但 NPC 幻体仍然没有单独成行；
- 用户贴回的调试日志多次稳定出现：
  - `sourceObjectName=桑克瑞德的幻体`
  - `sourceObjectName=阿尔菲诺的幻体`
  - `sourceObjectName=阿莉塞的幻体`
  - 同时 `sourceTracked=False`
- 这说明：事件现场能拿到幻体名字、对象引用和事件 ID，但还没有成功把这些对象注册成 tracked actor；
- 用户在本轮最后一次反馈时仍表示：
  - **还是完全没有幻体行**；
  - **而且有进入战斗，没有结束战斗**。

### 本轮已完成的代码改动

#### 1）本地玩家身份回退逻辑

文件：

- `DalamudACT/DalamudApi.cs`

已改为优先从：

- `ObjectTable.LocalPlayer`

再回退到：

- `ClientState.LocalPlayer`

涉及方法包括：

- `GetLocalPlayerName()`
- `GetLocalPlayerGameObjectId()`
- `GetLocalPlayerEntityId()`
- `GetLocalPlayerObjectId()`
- `GetLocalPlayerClassJobId()`

这部分已经被用户实测间接确认有效：玩家自己的统计链路恢复了。

#### 2）战斗流水筛选交互已改成下拉框

文件：

- `DalamudACT/UI/CombatTimelineWindow.cs`

原本的角色筛选切换已经改成下拉框，用户提出的这项 UI 需求可视为已完成。

#### 3）最近日志摘要支持显示调试日志

文件：

- `DalamudACT/LogHelper.cs`
- `DalamudACT/UI/LogUiHelper.cs`

已新增：

- `LogHelper.DebugRecent(...)`

用途：

- 把关键 debug 级别日志也推入“最近日志摘要”，方便用户直接截图回传，而不必强依赖 `dalamud.log`。

#### 4）ActionEffect 未命中日志增强

文件：

- `DalamudACT/Plugin/ACT.cs`

新增了未命中可跟踪对象时的调试日志，内容包括：

- `sourceId`
- `firstTargetId`
- `sourceTracked`
- `targetTracked`
- `sourceCharacter`
- `sourceObjectName`
- `targetObjectName`
- `sourceObjectGameObjectId`
- `sourceObjectId`
- `sourceEntityId`
- `localPlayerGameObjectId`
- `localPlayerObjectId`
- `localPlayerEntityId`

并且做了节流与合并计数，避免刷屏。

这组日志是本轮定位“幻体为何没被纳入统计”的关键依据。

#### 5）进入战斗但仍无流水/统计的诊断日志

文件：

- `DalamudACT/Stats/LocalStatsService.cs`

增加了这类调试日志：

- `已进入战斗 N 秒但仍未记录任何战斗流水或统计事件...`

用于区分：

- 是根本没进 encounter；
- 还是进战了但没有命中 tracked actor；
- 还是 UI 没显示。

#### 6）引入“观察到的友方对象”缓存

文件：

- `DalamudACT/Stats/LocalStatsService.cs`

新增：

- `observedFriendlyActorCache`
- `ObserveFriendlyCombatantFromGameObject(...)`
- `ObserveFriendlyCombatantIdentity(...)`
- `TryCreateObservedFriendlyActor(...)`
- `LooksLikeDutyCompanionName(...)`

当前规则重点覆盖名字类似：

- `XX的幻体`

的对象。

目标是：

- 不再强依赖这些对象必须已经出现在 `PartyList` / `BuddyList`；
- 允许在战斗事件现场动态把友方幻体收编进 tracked actor 集合。

#### 7）本轮最后新增：按“事件身份 + 名字”直接收编幻体

文件：

- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/Stats/LocalStatsService.cs`

这是本轮最后一版、也是最关键的一组补丁。

新增核心思路：

- 如果从 `GameObject -> IBattleChara` 这条链路收编失败；
- 但事件现场已经拿到了：
  - `actorId`
  - `sourceObjectName` / `targetObjectName`
- 且名字符合 `XX的幻体`，
- 就直接调用：
  - `ObserveFriendlyCombatantIdentity(actorId, name)`

也就是说，开始**直接按事件身份注册友方幻体**，不再只依赖对象包装完整性。

同时：

- source 侧增加了 `TryObserveFriendlyCombatant(...)` 辅助逻辑；
- target 侧也做了同样处理；
- 新增日志：
  - `已纳入可跟踪友方对象：...`
  - `已按事件身份纳入可跟踪友方对象：...`

这部分补丁**已经编译通过，但截至交接时还没有收到用户的新一轮实测反馈**。

#### 8）战斗结束判定收紧

文件：

- `DalamudACT/Stats/LocalStatsService.cs`

新增：

- `ShouldCountBattleCharaForCombatEnd(...)`

并让：

- `AreAllPartyMembersOutOfCombat(...)`

只统计真正应该参与结算判定的对象，优先包括：

- 本地玩家；
- 队伍成员；
- Buddy；
- 已纳入观察缓存的相关友方 NPC / 幻体；
- 带明确队伍标记的对象。

这样做的目的，是避免对象表里一些无关的友方 BattleNpc 长时间带着 `InCombat` 状态，导致 encounter 一直不结算。

### 截至收工前的明确结论

#### 已经确认有效的部分

1. **玩家链路已恢复**
   - 玩家自己的技能会进入战斗流水；
   - 玩家自己的 DPS 会显示；
   - 本地玩家 ID 不再稳定为 0。

2. **战斗流水筛选下拉框已完成**

3. **最近日志摘要现在可以承载关键 debug 信息**

4. **最后一版补丁可以在代码层直接按“actorId + 幻体名字”收编友方对象**
   - 这是解决“日志中能看到幻体名字，但永远不单独成行”的最新尝试；
   - 但目前还缺最后一次用户现场验证。

#### 仍未完成闭环的部分

1. **幻体是否已经能单独成行，仍待用户复测**
   - 收工前最后一次用户反馈还是“没有幻体行”；
   - 但那次反馈发生在最新补丁前；
   - 最新补丁之后，用户尚未再次贴图或贴日志确认。

2. **战斗结束判定是否已恢复正常，仍待用户复测**
   - 本轮已经收紧了参与 combat-end 判定的对象范围；
   - 但尚未收到“脱战后能正常结算”的现场反馈。

### 当前最值得看的文件

如果下一位继续接手，请优先查看：

- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/DalamudApi.cs`
- `DalamudACT/UI/CombatTimelineWindow.cs`
- `DalamudACT/LogHelper.cs`
- `DalamudACT/UI/LogUiHelper.cs`

重点函数：

- `ACT.ResolveTrackedSourceActorId(...)`
- `ACT.HandleAbility(...)`
- `ACT.TryObserveFriendlyCombatant(...)`
- `LocalStatsService.ObserveFriendlyCombatantFromGameObject(...)`
- `LocalStatsService.ObserveFriendlyCombatantIdentity(...)`
- `LocalStatsService.TryGetTrackedActor(...)`
- `LocalStatsService.AreAllPartyMembersOutOfCombat(...)`
- `LocalStatsService.ShouldCountBattleCharaForCombatEnd(...)`

### 当前工作区现场（交接时）

交接时 `git status --short` 为：

- 修改：
  - `DalamudACT/Configuration/PluginConfiguration.cs`
  - `DalamudACT/DalamudApi.cs`
  - `DalamudACT/Plugin/ACT.cs`
  - `DalamudACT/Stats/LocalStatsService.cs`
  - `DalamudACT/UI/MainWindow.cs`
  - `DalamudACT/UI/PluginUI.cs`
  - `DalamudACT/UI/SettingsWindow.cs`
  - `md/SESSION-HANDOFF.md`
- 未跟踪：
  - `1.txt`
  - `DalamudACT/LogHelper.cs`
  - `DalamudACT/UI/CombatTimelineWindow.cs`
  - `DalamudACT/UI/LogUiHelper.cs`

注意：

- 工作区仍然是脏的；
- 不要做破坏性 git 操作；
- `1.txt` 仍然是旧现场里保留下来的未跟踪文件，不要误删。

### 本轮最后一次本地构建

已验证：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

结果：

- `0 warnings`
- `0 errors`

产物：

- `E:\git\DalamudACT\output\DalamudACT.dll`

### 下一位最推荐的继续顺序

1. 先让用户用当前最新 `output\DalamudACT.dll` 复测“信赖 / NPC 队友 / 幻体副本”；
2. 重点确认最近日志里是否出现：
   - `已纳入可跟踪友方对象`
   - `已按事件身份纳入可跟踪友方对象`
3. 如果出现了上述日志，但面板仍没有幻体行：
   - 继续检查 `TryGetTrackedActor(...)` 到 `EncounterSession.EnsureCombatant(...)` 之间是否还有 actorId 口径不一致；
4. 如果幻体已经成行，但脱战后仍不结算：
   - 继续给 `AreAllPartyMembersOutOfCombat(...)` / `ShouldFinalizeEncounter(...)` / `EnumerateTrackedPartyBattleCharas()` 增加现场日志，打印到底是谁一直处于 `InCombat`；
5. 在未充分实测前，不建议再大范围改 Hook 层；
6. 当前最该做的是把“幻体单独成行 + 战斗正常结束”这条闭环跑通。

### 给下一位的一句话总结

本轮已经把“玩家自己无数据”的问题修通，也把“幻体事件虽然可见但不被纳入 tracked actor”的症结进一步收敛到**事件身份收编**这一层；最新补丁已经允许直接按 `actorId + 幻体名字` 注册友方对象，并收紧了 combat-end 判定，但**截至交接时还缺用户最后一轮现场验证**。
### 文档补记（收工前最后一轮）

在本节写完后，又继续补充了以下文档：

- `md/RECENT-ISSUES-STATUS-TABLE.md`
- `md/RECENT-ISSUES-SUMMARY.md`
- `md/CHANGELOG.md`
- `md/2026-05-10.md`

因此如果以下一位真正接手时的 `git status --short` 为准，除前文已列内容外，还应额外看到：

- `M md/CHANGELOG.md`
- `M md/RECENT-ISSUES-STATUS-TABLE.md`
- `M md/RECENT-ISSUES-SUMMARY.md`
- `?? md/2026-05-10.md`
### 文档补记（收工前继续补充）

在上一轮文档补记之后，又继续同步了：

- `md/DELIVERY-SUMMARY.md`
- `md/README-SUMMARY.md`

因此如果以下一位真正接手时的 `git status --short` 为准，文档相关改动还应额外包括：

- `M md/DELIVERY-SUMMARY.md`
- `M md/README-SUMMARY.md`
### 文档补记（收工前继续同步对外说明）

在上一轮补完摘要文档后，又继续同步了：

- `md/RELEASE-NOTES.md`
- `md/USAGE.md`

因此如果以下一位真正接手时的 `git status --short` 为准，文档相关改动还应额外包括：

- `M md/RELEASE-NOTES.md`
- `M md/USAGE.md`
## 2026-05-12 补充：ikegami 悬浮窗样式交接
- 本轮 ikegami 样式专项交接已单独整理：`md/2026-05-12-ikegami-handoff.md`
- 新会话如果要继续做悬浮窗样式，请优先阅读这份文档。
- 当前最新视觉基准以 `md/images/ikegami-preview.html` 为准，不要再以旧 PNG 或旧卡片布局为准。
