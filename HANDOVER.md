## 2026-05-11 交接补充：DoT 状态驱动收尾

- 这次已经把玩家 DoT 的主链路改成“状态驱动 + 3 秒轮询补 tick”：
  - 玩家放已知 DoT 技能后，先记录挂载候选；
  - 目标身上出现对应 debuff 后，纳入活跃 DoT 状态；
  - 后续由 `LocalStatsService.PollActivePlayerDots(...)` 按 3 秒节奏自动补 tick；
  - 目标不可选中时停止后续结算。
- DoT 暴击现在按普通技能那套思路做模拟，不再依赖原始 tick 包里的暴击字段。
- `DalamudACT/Plugin/ACT.cs` 已不再把 DoT tick 当作事件流里的独立记账路径；当前只负责：
  - 识别已知 DoT 技能；
  - 记录应用种子；
  - 让状态轮询接管后续 tick。
- `DalamudACT/Stats/PlayerDotCatalog.cs` 已作为静态白名单使用，按 `actionId / statusId` 过滤 DoT 候选，避免继续靠技能名字猜测。
- 当前代码里 `TryRecordPlayerDotDamage(...)` 仍保留，但主路径已经不再依赖它；如果后面要做清理，可以作为一个单独收尾任务。
- 这版已经重新构建通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - 结果：`0 warnings / 0 errors`
- 如果你只想先看最短版结论，请直接看：[`md/FINAL-HANDOFF.md`](md/FINAL-HANDOFF.md)
- 当前已知风险 / 待观察点：
  - DoT 伤害估算值目前还是保守推导，主要依赖 `status.ParamModifier`、最近一次应用种子、来源平均伤害；
  - 白名单外的技能不会进入 DoT 链路；
  - 仍需要进游戏实际验证：真实 DoT 是否能稳定持续结算、目标失去可选中后是否能正确停算、是否还有遗漏的 DoT 白名单。
- 工作区仍然是脏的，接手前先看：
  - `git status --short`
  - `1.txt` 是未跟踪文件，不要误删。
# DalamudACT 缁存姢浜ゆ帴

鏇存柊鏃堕棿锛歚2026-05-11`

鐢ㄩ€旓細杩欐槸褰撳墠缁存姢浜ゆ帴涓绘枃妗ｏ紝鐢ㄦ潵璇存槑褰撳墠鍙俊蹇収銆佸綊妗ｇ姸鎬併€佸彂甯冩祦绋嬪拰鎺ユ墜闃呰椤哄簭銆?

## 2026-05-11 补充：当前接手重点

- 如果你是准备开新会话的人，先看 `md/2026-05-11.md`；
- 当前最重要的 DoT 规则已经收紧为：
  - 只统计真正会在目标身上形成持续伤害状态，并在状态存续期间持续结算的效果；
  - 目标进入无法选中状态后，DoT 后续结算应停止；
  - 不再用技能名字猜测 DoT；
  - `箭毒II`、`注药III` 都不应再被当成 DoT；
- 当前代码里已经新增 `DalamudACT/Stats/PlayerDotCatalog.cs`，开始按 `actionId / statusId` 做静态白名单过滤；
- 下一步重点从“建表”转成“补漏项 + 现场复测”；
- 当前工作区仍然是脏的，`1.txt` 是未跟踪文件，不要误删；
- 上一轮本地构建仍然是通过的：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - 结果：`0 warnings / 0 errors`

鐩稿叧缁存姢鏂囨。锛?

- [缁存姢棣栭〉锛堝崟椤垫€昏锛塢(md/MAINTAINER-HOME.md)
- [缁存姢鏂囨。鎬昏鍥綸(md/MAINTAINER-DOC-MAP.md)
- [缁存姢鍏ュ彛绱㈠紩](md/MAINTAINER-INDEX.md)
- [涓嬩竴浣嶇淮鎶よ€呯涓€灏忔椂娓呭崟](md/MAINTAINER-FIRST-HOUR-CHECKLIST.md)
- [鏈€杩戦棶棰樹笌瑙ｅ喅鏂规鏁寸悊](md/RECENT-ISSUES-SUMMARY.md)
- [鏈€杩戦棶棰樼姸鎬佽〃锛堢淮鎶よ瑙掞級](md/RECENT-ISSUES-STATUS-TABLE.md)
- [README.md](README.md)

## 鐩綍 / TOC

- [褰撳墠蹇収](#handover-snapshot)
- [褰掓。璁板綍](#handover-archive)
- [褰掓。锛氬綋鏃剁姸鎬乚(#handover-archived-status)
- [褰掓。锛氳嚜鍔ㄥ寲鐘舵€乚(#handover-archived-automation)
- [褰掓。锛氭湰娆′氦鎺ョ獥鍙ｇ殑鍙樻洿](#handover-archived-changes)
- [椤圭洰瀹氫綅](#handover-direction)
- [鐗堟湰鑼冨洿](#handover-version-scope)
- [鍏抽敭鏂囦欢](#handover-key-files)
- [鍙戝竷鍏ュ彛](#handover-release)
- [鎺ユ墜鍏堢湅浠€涔圿(#handover-reading-order)
- [澶囨敞](#handover-notes)

<a id="handover-snapshot"></a>
## 褰撳墠蹇収

- 宸ヤ綔鐩綍锛歚E:\git\DalamudACT`
- 褰撳墠鍒嗘敮锛歚main`
- 褰撳墠 HEAD锛歚fc352b4`
- 褰撳墠浜ゆ帴鐜板満浠嶆槸鑴忓伐浣滃尯锛屾帴鎵嬪墠璇峰厛鎵ц `git status --short`锛屼互褰撳墠杈撳嚭涓哄噯銆?
- 褰撳墠宸ヤ綔鍖洪噷鍙墿涓€涓湭璺熻釜鏂囦欢锛歚1.txt`锛堢敤閫斿緟纭锛夈€?
- 鏇存棭涔嬪墠鐨勬垬鏂楄窡韪疄楠岋紝宸茬粡鍦ㄥ舰鎴愯繖涓揩鐓у墠鍥炴粴鍒板共鍑€鍩虹嚎銆?
- 鏈€杩戜竴娆″凡楠岃瘉鐨勬湰鍦版瀯寤猴細
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - 缁撴灉锛歚0 warnings / 0 errors`
  - 浜х墿锛歚E:\git\DalamudACT\output\DalamudACT.dll`

<a id="handover-archive"></a>
## 褰掓。璁板綍

涓嬮潰鐨勫唴瀹规槸杈冩棭鐨?`2026-05-06` 浜ゆ帴璁板綍锛屼繚鐣欎綔涓哄弬鑰冦€?

<a id="handover-archived-status"></a>
## 褰掓。锛氬綋鏃剁姸鎬?

- 宸ヤ綔鐩綍锛歚E:\git\DalamudACT`
- 杩滅锛歚origin = https://github.com/anmili2022/DalamudACT`
- 涓昏缁存姢鍒嗘敮锛歚master`
- 褰撴椂浜ゆ帴鎵€鍦ㄥ垎鏀細`master`
- 鏈€杩戜竴娆″凡楠岃瘉鐨勮嚜鍔ㄥ寲鍩虹嚎鎻愪氦锛歚b1324c9`
- 楠岃瘉涓婅堪鑷姩鍖栧熀绾挎椂锛屽伐浣滃尯鏄共鍑€鐨?
- 褰撴椂鍏冩暟鎹増鏈細`0.15.2.5`
- 褰撴椂宸查獙璇佺殑鏈湴鏋勫缓锛?
  - Debug锛歚dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Debug`
  - Release锛歚dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Release -p:Version=0.15.2.5 -p:FileVersion=0.15.2.5 -p:AssemblyVersion=0.15.2.5`
  - 缁撴灉锛歚0 warnings / 0 errors`
  - 浜х墿锛歚E:\git\DalamudACT\output\DalamudACT.dll`

<a id="handover-archived-automation"></a>
## 褰掓。锛氳嚜鍔ㄥ寲鐘舵€?

杩欎釜浠撳簱鏄?`flyrio/DalamudACT` 鐨?fork銆?

鎴嚦 `2026-05-06`锛岃繖涓?fork 涓婄殑 GitHub Actions 宸茬粡楠岃瘉鍙敤銆?

宸查獙璇佺殑杩愯璁板綍锛?

- `.github/workflows/build.yml`
  - 瑙﹀彂妫€鏌ユ彁浜わ細`08c4c30`
  - 棣栨瑙傚療鍒扮殑杩愯锛歚25427532514`
  - 鍒濆缁撴灉锛歸orkflow 鑳芥纭Е鍙戯紝浣嗗湪 `Archive` 闃舵澶辫触
  - 鍘熷洜锛歸orkflow 浠嶅湪褰掓。 `DalamudACT/bin/Release/*`锛岃€岄」鐩綋鍓嶄骇鐗╁凡鏀瑰埌 `output/`
- `.github/workflows/build.yml` 淇鍚庣姸鎬?
  - 淇鎻愪氦锛歚b1324c9`
  - 鎴愬姛杩愯锛歚25427744876`
  - 缁撴灉锛歚Build`銆乣Archive`銆乣Upload Artifact`銆乣Update Latest Release` 鍏ㄩ儴鎴愬姛
- `.github/workflows/release.yml`
  - 閫氳繃宸叉湁 tag `0.15.2.3` 鐨?`workflow_dispatch` 瀹屾垚楠岃瘉
  - 鎴愬姛杩愯锛歚25427944539`
  - 缁撴灉锛氬畬鏁村彂甯冩祦绋嬫垚鍔燂紝鍖呮嫭 zip 鎵撳寘鍜?GitHub Release 鏇存柊

瀹為檯缁撹锛?

- 鍒嗘敮鏋勫缓娴佺▼宸茬粡鎭㈠鍙敤
- 姝ｅ紡鍙戝竷 workflow 宸茬粡鎭㈠鍙敤
- 褰撳墠姝ｅ紡鍙戠増璺緞鍦ㄦ甯稿満鏅笅宸蹭笉鍐嶄緷璧栨棭鏈熲€滃彧鑳芥墜宸ュ厹搴曗€濈殑鏂规

<a id="handover-archived-changes"></a>
## 褰掓。锛氭湰娆′氦鎺ョ獥鍙ｇ殑鍙樻洿

- 鍙戝竷浜?`0.15.2.5`
- 宸茬‘璁?`repo.json`銆乵anifest 鍜岀▼搴忛泦鐗堟湰閮藉凡鍚屾鍒?`0.15.2.5`
- 鍦ㄨ缃腑鏂板浜嗘偓娴獥閿佸畾閫夐」
- 閿佸畾鍚庢偓娴獥涓嶈兘绉诲姩鎴栫缉鏀?
- 閿佸畾鍚庤〃鏍煎垪瀹芥嫋鍔ㄦ墜鏌勪細琚鐢紝浠庤€屼繚鐣欑敤鎴峰綋鍓嶅垪瀹?
- 闄ょ獥鍙ｈ缃拰鏁版嵁/鐘舵€佸锛屽叾浠栬缃垎缁勯粯璁ゆ姌鍙?
- 鍚堟垚鍥㈡湰娴嬭瘯鏁版嵁宸叉敼涓?8 浜虹殑 `闆跺紡娴嬭瘯鍦篳 鏍锋湰
- 璋冩暣浜嗘偓娴獥鎶樺彔/灞曞紑琛屼负锛屼娇鍏剁粺涓€浣跨敤绱у噾 DPS 椤电鐘舵€?
- 淇浜嗗凡鏈夋垬鏂楀揩鐓ф椂浠嶅彲鑳芥樉绀?`绛夊緟鎴樻枟鏁版嵁...` 鐨?UI 鍒ゆ柇闂
- 宸茬‘璁ゆ湰鍦?`Release` 鎵撳寘璺緞浠嶇劧鏄?`output/`
- 淇 `.github/workflows/build.yml`锛屾敼涓轰粠 `output/` 褰掓。锛岃€屼笉鏄棫鐨?`bin/Release`
- 宸查獙璇?`.github/workflows/release.yml` 鍙互鎴愬姛閲嶅缓骞跺彂甯?tag `0.15.2.3`

<a id="handover-direction"></a>
## 椤圭洰瀹氫綅

`DalamudACT` 宸茬粡涓嶅啀鏄竴涓寘鐫€澶栭儴 overlay 鐨勮杽澶栧３銆?

褰撳墠椤圭洰鏂瑰悜鏄細

- 鍦?Dalamud 鍐呴儴閲囬泦鎴樻枟浜嬩欢
- 鍦ㄦ彃浠跺唴閮ㄨ绠楁湰鍦?ACTX 椋庢牸鐨勬垬鏂楃粺璁?
- 鍦ㄦ父鎴忓唴鐩存帴娓叉煋 DPS銆丠PS銆佹壙浼ゃ€佹瑙堝拰鍘嗗彶璁板綍
- 鍦ㄨ繖涓粨搴撲腑缁х画缁存姢 UI 涓庡彂甯冧骇鐗?

<a id="handover-version-scope"></a>
## 鐗堟湰鑼冨洿

褰撳墠浠撳簱涓渶杩戜竴娆℃彁浜ょ殑鍏冩暟鎹増鏈槸 `0.15.2.5`銆?

鍙﹀锛屽綋鍓嶅伐浣滃尯杩樻湁涓€鎵?*灏氭湭鍙戠増銆佷絾宸叉湰鍦版瀯寤洪獙璇侀€氳繃**鐨勬敼鍔紝閲嶇偣濡備笅锛?

- 閰嶇疆鐗堟湰宸叉彁鍗囧埌 `23`
- `DPS / HPS / 鎵夸激` 涓夐〉宸叉敼涓哄叡浜竴缁勫垪鏄剧ず璁剧疆
  - 鐜╁鍒?
  - 鑱屼笟鍒?
  - 浼ゅ鍒?
  - 绉掍激鍒?
  - 姝讳骸鍒?
  - 鏄剧ず浜烘暟
- 鍏朵腑锛?
  - `浼ゅ鍒梎 鍒嗗埆瀵瑰簲 `DPS 浼ゅ閲?/ HPS 娌荤枟閲?/ 鎵夸激 鎵夸激閲廯
  - `绉掍激鍒梎 鍒嗗埆瀵瑰簲 `DPS 绉掍激 / HPS 绉掔枟 / 鎵夸激 绉掓壙浼
- 鍒囨崲鍒楁樉绀烘椂涓嶄細閲嶇疆鐢ㄦ埛褰撳墠鍒楀
- 缁熻椤典笌鍘嗗彶椤靛垪瀹戒細鍐欏叆閰嶇疆鏂囦欢锛屽苟鍦ㄤ笅娆℃墦寮€鎻掍欢鏃舵仮澶?
- 璁剧疆椤靛彲涓€閿噸缃粺璁￠〉 / 鍘嗗彶椤电殑鍒楀璁板繂
- 涓荤獥鍙ｄ笌璁剧疆绐楀彛宸查噸鏋勪负鍗＄墖寮?UI
- 涓荤獥鍙ｆ柊澧炩€滅晫闈笌鍒楅厤缃憳瑕佲€濆崱鐗?
- `README.md`銆乣md/USAGE.md`銆佺淮鎶ゆ枃妗ｅ凡琛ュ厖浠ヤ笂琛屼负璇存槑涓庡閮ㄦ帴鍙ｆ枃妗ｅ叆鍙?

浠撳簱閲屽凡缁忎綋鐜板嚭鐨勮繎鏈熸敼鍔ㄥ涓嬶細

- `0.15.2.8`
  - fixed stats table width memory and hidden-column layout behavior
  - changed hidden stats columns to hide the whole column
  - enforced a `20px` minimum width for the deaths column
  - settings window title now shows the plugin version
  - historical preview can automatically return to live DPS after timeout

- `0.15.2.5`
  - 鍦ㄧ獥鍙ｈ缃笅鏂板鎮诞绐楅攣瀹氶€夐」
  - 閿佸畾鍚庣殑鎮诞绐椾笉鑳界Щ鍔ㄦ垨缂╂斁
  - 閿佸畾鏃朵繚鐣欏綋鍓嶈〃鏍煎垪瀹斤紝骞剁鐢ㄨ〃澶存嫋鎷?
  - 闄ょ獥鍙ｈ缃拰鏁版嵁/鐘舵€佸锛岃缃垎缁勯粯璁ゆ姌鍙?
  - `闆跺紡娴嬭瘯鍦篳 鍚堟垚娴嬭瘯鏁版嵁鎵╁睍涓?8 浜烘牱鏈?

- `0.15.2.4`
  - 鎮诞绐楁姌鍙犳€佺粺涓€鍒扮揣鍑?DPS 椤电娴佺▼
  - 鐐瑰嚮绱у噾 DPS 椤电浼氱洿鎺ュ睍寮€鍒板疄鏃舵暟鎹?
  - 鍙宸叉湁鎴樻枟蹇収锛屽氨浼氬睍绀虹粺璁★紝鑰屼笉鍐嶅崱鍦ㄧ瓑寰呮枃鏈?
  - README 鏂板缁存姢浜ゆ帴鍏ュ彛閾炬帴
- `0.15.2.3`
  - 浠撳簱鏍圭洰褰曟柊澧炵淮鎶や氦鎺ュ叆鍙?
  - README 澧炲姞闈㈠悜缁存姢鑰呯殑鐩磋揪鍏ュ彛
  - 鍒嗘敮鏋勫缓 workflow 淇涓轰粠 `output/` 褰掓。
  - 璇?fork 涓婄殑 GitHub Actions 鍒嗘敮鏋勫缓鍜屾寮忓彂甯冩祦绋嬪潎宸查獙璇佸彲鐢?
- `0.15.2.2`
  - 鎻掍欢鍔犺浇鏃舵偓娴獥榛樿鎶樺彔
  - 鎮诞绐楅粯璁ゅ睍寮€灏哄璋冩暣涓?`300x300`
  - 鐐瑰嚮绛夊緟鏂囨湰鍙垏鎹㈡姌鍙?/ 灞曞紑
  - 鍙戝竷 workflow 鍔犲浐涓哄彲姝ｇ‘璇诲彇 UTF-8 鍙戝竷璇存槑
- `0.15.2.1`
  - 淇 NPC 鍜屼俊璧栭槦鍙嬬殑璺熻釜闂
  - 鏀硅繘鍘嗗彶璁板綍涓庡疄鏃惰鍥剧殑鍒囨崲
  - 涓荤獥鍙ｆ柊澧炵増鏈樉绀?
- `0.15.2.0`
  - 璁剧疆鍒嗙粍鎷嗗垎涓庢垬鏂楃粨鏉熷垽瀹氶厤缃?
  - 鍘嗗彶璁板綍瀵煎叆 / 瀵煎嚭涓庣浉鍏?UI 鏀硅繘

瀹屾暣鍙戝竷鍘嗗彶瑙?[md/CHANGELOG.md](md/CHANGELOG.md)銆?

<a id="handover-key-files"></a>
## 鍏抽敭鏂囦欢

鏋勫缓涓庡厓鏁版嵁锛?

- `DalamudACT/DalamudACT.csproj`
- `DalamudACT/DalamudACT.json`
- `Data/DalamudACT.json`
- `repo.json`

涓昏杩愯鏃朵笌 UI 鍖哄煙锛?

- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/DalamudApi.cs`
- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/UI/MainWindow.cs`
- `DalamudACT/UI/FloatingStatsWindow.cs`
- `DalamudACT/UI/StatsPanel.cs`
- `DalamudACT/UI/SettingsWindow.cs`

寮€鍙戞椂甯哥敤鐨勫閮ㄦ帴鍙ｆ枃妗ｏ細

- Dalamud 鏂囨。棣栭〉锛?https://dalamud.dev/>
- Dalamud API 鍙傝€冿細<https://dalamud.dev/api/>
- Lumina.Excel 浠撳簱锛?https://github.com/NotAdam/Lumina.Excel>

蹇€熷垽鏂細

- 鏌?`PluginService`銆佺獥鍙?UI銆佸懡浠ゃ€佺姸鎬併€乣IDataManager` 绛夋帴鍙ｆ椂锛屼紭鍏堢湅 Dalamud 鏂囨。 / API銆?
- 鏌?`GetExcelSheet<T>()`銆乣ExcelSheet<T>`銆乣Lumina.Excel.Sheets.*` 绛?Excel 鏁版嵁璇诲彇鏃讹紝浼樺厛鐪?`Lumina.Excel`銆?

鍙戝竷鑷姩鍖栵細

- `.github/workflows/release.yml`
- `.github/workflows/test_release.yml`
- `.github/workflows/build.yml`

褰撳墠鑷姩鍖栬鏄庯細

- `build.yml` 褰撳墠鎵撳寘鐨勬枃浠朵负 `output/DalamudACT.dll`銆乣output/DalamudACT.json` 鍜?`output/DalamudACT.deps.json`
- `release.yml` 宸插湪杩欎釜 fork 鍚敤 Actions 鍚庡畬鎴愭垚鍔熼獙璇?

<a id="handover-release"></a>
## 鍙戝竷鍏ュ彛

姝ｅ紡鍙戝竷娴佺▼锛?

1. 鏇存柊浠ｇ爜涓庢枃妗ｃ€?
2. 灏嗕互涓嬫枃浠朵腑鐨勭増鏈叏閮ㄥ悓姝ヤ竴鑷达細
   - `DalamudACT/DalamudACT.csproj`
   - `DalamudACT/DalamudACT.json`
   - `Data/DalamudACT.json`
   - `repo.json`
   - `md/CHANGELOG.md`
   - `md/RELEASE-NOTES.md`
3. 杩愯 `dotnet build E:\git\DalamudACT\DalamudACT.sln`
4. 鎺ㄩ€?`master`
5. 鍒涘缓骞舵帹閫佹寮忕増鏈?tag
6. 璁?`.github/workflows/release.yml` 鑷姩鍒涘缓 GitHub Release

宸叉湁 tag 鐨勯噸鏂板彂甯冩祦绋嬶細

褰撴煇涓寮忕増鏈?tag 宸茬粡瀛樺湪浜?GitHub锛屼絾浣犱粛闇€瑕?GitHub Actions 閲嶆柊鏋勫缓鎴栭噸鏂板彂甯冭繖涓悓鍚?tag 鏃讹紝浣跨敤杩欎竴娴佺▼銆?

1. 鎵撳紑 GitHub Actions 涓殑 `Create Release` workflow銆?
2. 鐐瑰嚮 `Run workflow`銆?
3. 鍦?`tag` 杈撳叆妗嗕腑濉叆宸叉湁鐨勬寮?tag锛屼緥濡?`0.15.2.6`銆?
4. 璁?`.github/workflows/release.yml` 閲嶆柊鏋勫缓骞朵笂浼?`DalamudACT.zip`銆?
5. 纭璇?tag 鐨?Release 椤甸潰宸茬粡鍑虹幇棰勬湡璧勪骇銆?

閲嶈璇存槑锛?

- 濡傛灉 workflow 鏂囦欢鑷偅娆″け璐ヨ繍琛屼箣鍚庡凡缁忔敼杩囷紝涓嶈瀵规棫澶辫触 release 鐩存帴浣跨敤 `Re-run jobs`
- GitHub 閲嶈窇鏃т换鍔℃椂锛屼細缁х画娌跨敤鍘熷鐨?`GITHUB_SHA` 鍜?`GITHUB_REF`
- 濡傛灉鏃т换鍔″紩鐢ㄧ殑鏄繃鏈?workflow 蹇収锛岄偅涔堥噸璺戜粛浼氭墽琛屽悓鏍疯繃鏈熺殑鎵撳寘閫昏緫
- 鎷夸笉鍑嗘椂锛屼紭鍏堜娇鐢?`workflow_dispatch` 骞朵紶鍏ュ噯纭殑鏃㈡湁 tag

鏅€氳ˉ涓佺増鏈殑蹇€熷彂甯冩祦绋嬶細

閫傜敤鍦烘櫙锛?

- 鍙戝竷鑷姩鍖栨湰韬凡缁忓仴搴?
- 杩欐鍙槸姝ｅ父鐗堟湰鍗囩骇锛岃€屼笉鏄慨 workflow
- 浣犲彧闇€瑕佷粠鏈湴鏀瑰姩璧版渶鐭彲淇¤矾寰勫彂甯冨埌 GitHub Release

1. 鍏堢‘瀹氱洰鏍囩増鏈紝渚嬪 `0.15.2.6`銆?
2. 灏嗕互涓嬫枃浠剁粺涓€鏇存柊鍒拌繖涓簿纭増鏈細
   - `DalamudACT/DalamudACT.csproj`
   - `DalamudACT/DalamudACT.json`
   - `Data/DalamudACT.json`
   - `repo.json`
   - `md/CHANGELOG.md`
   - `md/RELEASE-NOTES.md`
3. 鍦ㄦ湰鍦拌繍琛?release 鏋勫缓锛?

```powershell
$ver = "0.15.2.6"
dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Release -p:Version=$ver -p:FileVersion=$ver -p:AssemblyVersion=$ver
```

4. 鎻愪氦骞舵帹閫?`master`锛?

```powershell
git -C E:\git\DalamudACT status --short
git -C E:\git\DalamudACT add .
git -C E:\git\DalamudACT commit -m "chore: release $ver"
git -C E:\git\DalamudACT push origin master
```

5. 鍒涘缓骞舵帹閫佸彂甯?tag锛?

```powershell
git -C E:\git\DalamudACT -c tag.gpgSign=false tag -a $ver -m "DalamudACT $ver"
git -C E:\git\DalamudACT push origin $ver
```

6. 纭 GitHub 宸插垱寤?Release锛屽苟鎸備笂 `DalamudACT.zip`銆?

娴嬭瘯鍙戝竷娴佺▼锛?

杩欎釜娴佺▼鍙敤浜?`testing_*` tag锛屼笉鐢ㄤ簬姝ｅ紡鍙戝竷銆?

1. 纭鐩爣鎻愪氦涓婄殑 `.github/workflows/test_release.yml` 宸茬粡鏄€滀粠 `output/` 鎵撳寘鈥濈殑褰撳墠鐗堟湰銆?
2. 浠庣洰鏍囨彁浜ゅ垱寤轰竴涓祴璇?tag锛屼緥濡?`testing_0.15.2.6`銆?
3. 鎺ㄩ€佽娴嬭瘯 tag銆?
4. 璁?`.github/workflows/test_release.yml` 鏋勫缓鎻掍欢銆佺敓鎴愭祴璇?release zip锛屽苟鏇存柊 `repo.json` 涓殑 testing 瀛楁銆?
5. 楠岃瘉娴嬭瘯 release 椤甸潰锛屽苟纭 `repo.json` 涓殑 testing 涓嬭浇閾炬帴鎸囧悜鍚屼竴涓?`testing_*` tag銆?

蹇€熸彁閱掞細

- 姝ｅ紡鍙戝竷涓嶈浣跨敤 `testing_*`
- 涓嶈浣跨敤 `latest`
- 涓嶈婕忔帀 `repo.json`
- 濡傛灉绛惧悕浼氬崱浣忥紝杩欏彴鏈哄櫒涓婁笉瑕佺洿鎺ョ敤鏅€?`git tag`
- tag 鍚嶅繀椤讳笌鍙戝竷鐗堟湰鍙峰畬鍏ㄤ竴鑷?

褰撳墠宸查獙璇佺姸鎬侊細

- 鍒嗘敮鏋勫缓娴佺▼锛氬凡楠岃瘉
- 閫氳繃宸叉湁 tag + `workflow_dispatch` 鐨勬寮忓彂甯冩祦绋嬶細宸查獙璇?
- runbook 涓粛鍙繚鐣欎汉宸ュ厹搴曞彂甯冩柟妗堬紝浣嗗畠宸蹭笉鍐嶆槸鍞竴鍙俊璺緞

閲嶈 workflow 瑙掕壊锛?

- `.github/workflows/release.yml`锛氭寜姝ｅ紡鐗堟湰 tag 鍙戠増
- `.github/workflows/test_release.yml`锛氫粎鐢ㄤ簬 testing tag 娴佺▼
- `.github/workflows/build.yml`锛氬垎鏀?/ 绫?nightly 鏋勫缓娴佺▼

杩欏彴鏈哄櫒鏇剧粡鍑虹幇杩囧繀椤诲叧闂?tag 绛惧悕鎵嶈兘缁х画鍙戠増鐨勬儏鍐点€傚鏋?tag 绛惧悕闃诲鍙戝竷锛岃鐪?[md/RELEASE-RUNBOOK.md](md/RELEASE-RUNBOOK.md) 鍜?[md/2026-05-09-RELEASE-HANDOFF.md](md/2026-05-09-RELEASE-HANDOFF.md)銆?
<a id="handover-reading-order"></a>
## 鎺ユ墜鍏堢湅浠€涔?

濡傛灉浣犺鎺ユ墜缁存姢锛屽缓璁寜涓嬮潰椤哄簭闃呰锛?

1. [HANDOVER.md](HANDOVER.md)
2. [缁存姢棣栭〉锛堝崟椤垫€昏锛塢(md/MAINTAINER-HOME.md)
3. [缁存姢鍏ュ彛绱㈠紩](md/MAINTAINER-INDEX.md)
4. [涓嬩竴浣嶇淮鎶よ€呯涓€灏忔椂娓呭崟](md/MAINTAINER-FIRST-HOUR-CHECKLIST.md)
5. [鏈€杩戦棶棰樹笌瑙ｅ喅鏂规鏁寸悊](md/RECENT-ISSUES-SUMMARY.md)
6. [鏈€杩戦棶棰樼姸鎬佽〃锛堢淮鎶よ瑙掞級](md/RECENT-ISSUES-STATUS-TABLE.md)
7. [2026-05-09 鍙戝竷浜ゆ帴](md/2026-05-09-RELEASE-HANDOFF.md)
8. [鍙戝竷 Runbook](md/RELEASE-RUNBOOK.md)
9. [SESSION-HANDOFF](md/SESSION-HANDOFF.md)
10. [鏇存柊璁板綍](md/CHANGELOG.md)

濡傛灉浣犲湪鎺掓煡杩愯鏃惰涓猴紝浼樺厛鐪嬶細

1. `DalamudACT/Stats/LocalStatsService.cs`
2. `DalamudACT/Plugin/ACT.cs`
3. `DalamudACT/DalamudApi.cs`
4. `DalamudACT/UI/FloatingStatsWindow.cs`
5. `DalamudACT/UI/StatsPanel.cs`

<a id="handover-notes"></a>
## 澶囨敞

- 鍒涘缓杈冩棭杩欎唤浜ゆ帴璁板綍鏃讹紝浠撳簱鏇剧粡鏄共鍑€鐨勩€?
- 褰撳墠鍙俊鐨勬湰鍦颁骇鐗╄矾寰勬槸 `output\DalamudACT.dll`銆?
- 褰撳墠鍙俊鐨勫垎鏀瀯寤轰慨澶嶄綅浜庢彁浜?`b1324c9`銆?
- 棣栦釜鎴愬姛鐨勫垎鏀瀯寤鸿繍琛屽彿鏄?`25427744876`銆?
- 宸查獙璇佹垚鍔熺殑姝ｅ紡鍙戝竷 workflow 杩愯鍙锋槸 `25427944539`銆?
- 鏇寸粏鐨勯€愭棩璁板綍宸茬粡淇濆瓨鍦?`md/` 鐩綍涓嬶紱鏈枃浠剁殑瀹氫綅鏄淮鎶ゅ叆鍙ｆ憳瑕侊紝鑰屼笉鏄浛浠ｉ偅浜涜缁嗚褰曘€?

## 2026-05-09 琛ュ厖锛氭湰杞ǔ瀹氭€т慨澶嶄笌鐗堟湰缁熶竴

- 宸蹭慨澶嶁€滆繘鎴樻枟浣嗕笉鍑烘暟鎹€濈殑闂锛屽綋鍓嶅疄鎴橀摼璺凡鎭㈠鍑烘暟銆?
- 宸茬Щ闄ゅ `PronounModule.ResolvePlaceholder("<1>..<8>")` 鐨勭洿鎺ヤ緷璧栵紝閬垮厤杩愯鏃剁鍚嶅彉鍖栧啀娆¤Е鍙戝穿婧冦€?
- 宸叉妸 `ActionEffect` 鏉ユ簮璇嗗埆鏀跺彛鍒?`sourceId + sourceCharacter` 鐨勭粍鍚堝垽鏂紝骞朵笌 `LocalStatsService` 鐨勭粺涓€韬唤妯″瀷瀵归綈銆?
- 宸叉妸鎮诞绐楁樉绀鸿涓烘敼涓猴細鑴辨垬鍚庝繚鐣欎笂涓€鍦烘暟鎹紝鍦ㄤ笅涓€娆¤繘鍏ユ垬鏂楁椂鍐嶅厛娓呯┖骞剁瓑寰呮柊鎴樻枟鏁版嵁銆?- 宸叉妸鐘舵€佹枃妗堣繘涓€姝ョ粏鍖栵細鑴辨垬淇濈暀鏃ф暟鎹椂鏄剧ず鈥滅瓑寰呬笅涓€鍦烘垬鏂椻€濓紝閲嶆柊杩涙垬鏂椾絾灏氭湭鍑虹涓€鏉℃暟鎹椂鏄剧ず鈥滄鍦ㄦ敹闆嗘柊鎴樻枟鏁版嵁鈥濄€?- 宸插皢鐗堟湰鍙风粺涓€鍒?`0.15.2.8`锛?  - `DalamudACT/DalamudACT.csproj`
  - `Data/DalamudACT.json`
  - `DalamudACT/DalamudACT.json`
  - `repo.json`
- 褰撳墠鍙洿鎺ョ敤鐨勬湰鍦颁骇鐗╀粛鏄細
  - `output\DalamudACT.dll`
- 鍚庣画缁х画瀹炴祴寤鸿锛?
  1. 鍗曚汉 / NPC 鍚岃鎴樻枟
  2. 鏅€?4/8 浜洪槦浼?
  3. Buddy / 瀹犵墿 / 鍙敜鐗╁綊灞?

## 2026-05-10 琛ュ厖锛氬崟浜鸿В闄?/ NPC 闃熷弸 / 骞讳綋鍓湰鎺掓煡鐜板満

### 褰撳墠鐜板満蹇収锛堜互鏈妭涓哄噯锛?
- 宸ヤ綔鐩綍锛欵:\git\DalamudACT
- 褰撳墠鍒嗘敮锛歮ain
- 褰撳墠 HEAD锛?f1d330
- 褰撳墠宸ヤ綔鍖轰粛鐒舵槸鑴忕殑锛岃鎺ユ墜鍓嶅厛鎵ц锛?  - git status --short
- 鎴嚦浜ゆ帴鏃剁殑宸ヤ綔鍖虹姸鎬侊細
  - 淇敼锛?    - DalamudACT/Configuration/PluginConfiguration.cs
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
- 浠嶇劧涓嶈璇垹锛?.txt
- 鏈€杩戜竴娆℃湰鍦版瀯寤哄凡楠岃瘉閫氳繃锛?  - dotnet build E:\git\DalamudACT\DalamudACT.sln
  -  warnings / 0 errors
  - 浜х墿锛欵:\git\DalamudACT\output\DalamudACT.dll

### 鏈疆澶勭悊鐨勬牳蹇冮棶棰?
鐢ㄦ埛鏈疆涓昏鍥寸粫浠ヤ笅闂杩炵画鍙嶉锛?
1. 鍗曚汉瑙ｉ檺杩涘叆鍓湰鏃舵病鏈夋暟鎹樉绀猴紱
2. 鍗曚汉瑙ｉ檺鎴栬窡 NPC 闃熷弸杩涘叆鍓湰鏃舵病鏈夋暟鎹樉绀猴紱
3. 鐜╁淇鍚庢仮澶嶆甯革紝浣?NPC 骞讳綋浠嶇劧娌℃湁鍗曠嫭鎴愯锛?4. 鐢ㄦ埛瑕佹眰鎶婃垬鏂楁祦姘翠腑鐨勮鑹茬瓫閫夋敼鎴愪笅鎷夋锛?5. 鍚庣画鍙堝弽棣堬細杩涘叆鎴樻枟鍚庝笉浼氱粨鏉熸垬鏂椼€?
### 鏈疆宸插畬鎴愮殑鍏抽敭鏀瑰姩

- 鍦?DalamudACT/DalamudApi.cs 涓妸鏈湴鐜╁韬唤鑾峰彇浼樺厛鍒囧埌 ObjectTable.LocalPlayer锛屽啀鍥為€€ ClientState.LocalPlayer锛?- 鍦?DalamudACT/UI/CombatTimelineWindow.cs 涓妸鎴樻枟娴佹按瑙掕壊绛涢€夋敼鎴愪笅鎷夋锛?- 鍦?DalamudACT/LogHelper.cs 涓?DalamudACT/UI/LogUiHelper.cs 涓鍔犲彲杩涘叆鏈€杩戞棩蹇楁憳瑕佺殑璋冭瘯鏃ュ織鑳藉姏锛?- 鍦?DalamudACT/Plugin/ACT.cs 涓ˉ寮烘湭鍛戒腑 tracked actor 鐨勮皟璇曟棩蹇楋紱
- 鍦?DalamudACT/Stats/LocalStatsService.cs 涓鍔犫€滆繘鎴樹絾鏃犳祦姘?缁熻鈥濈殑璇婃柇鏃ュ織锛?- 寮曞叆 observedFriendlyActorCache锛屽厑璁稿湪浜嬩欢鐜板満鍔ㄦ€佹敹缂栧弸鏂瑰璞★紱
- 鏂板 ObserveFriendlyCombatantIdentity(actorId, name)锛屽紑濮嬪厑璁哥洿鎺ユ寜浜嬩欢韬唤鎶?XX鐨勫够浣?绾冲叆 tracked actor锛?- 鍦?ACT.HandleAbility(...) 鐨?source / target 涓や晶閮芥帴鍏ヤ簡涓婅堪瑙傚療閫昏緫锛?- 鏀剁揣浜?combat-end 鍒ゅ畾锛屽彧璁╃湡姝ｇ浉鍏崇殑鐜╁ / 闃熶紞鎴愬憳 / Buddy / 宸茶瀵熷埌鐨勭浉鍏冲弸鏂瑰璞″弬涓庘€滄槸鍚﹁劚鎴樷€濈殑鍒ゆ柇銆?
### 鎴浜ゆ帴鏃跺凡纭涓庢湭纭鐨勭粨璁?
#### 宸茬‘璁?
- 鐜╁鑷繁鐨勭粺璁￠摼璺凡缁忔仮澶嶏細
  - 鎴樻枟娴佹按鑳界湅鍒扮帺瀹惰嚜宸辩殑鎶€鑳斤紱
  - DPS 闈㈡澘鑳界湅鍒扮帺瀹惰嚜宸变竴琛岋紱
- 鏈湴鐜╁ ID 鍦ㄨ繖绫诲壇鏈腑涓嶅啀绋冲畾涓?0锛?- 鎴樻枟娴佹按瑙掕壊绛涢€夋敼涓嬫媺妗嗗凡瀹屾垚锛?- 鏈€鏂颁唬鐮佸凡缁忚兘澶熷皾璇曠洿鎺ユ寜 ctorId + 骞讳綋鍚嶅瓧 鏀剁紪鍙嬫柟骞讳綋銆?
#### 灏氭湭瀹屾垚鐜板満闂幆

- 鐢ㄦ埛鍦ㄦ渶鏂拌ˉ涓佸墠鐨勬渶鍚庡弽棣堜粛鏄細
  - 杩樻槸娌℃湁骞讳綋鍗曠嫭鎴愯锛?  - 杩涘叆鎴樻枟鍚庝笉浼氱粨鏉熸垬鏂楋紱
- 浣?*鏈€鏂颁竴鐗堚€滄寜浜嬩欢韬唤鏀剁紪骞讳綋 + 鏀剁揣缁撴潫鎴樻枟鍒ゅ畾鈥濈殑琛ヤ竵涔嬪悗锛岃繕娌℃湁鎷垮埌鐢ㄦ埛鐨勬柊涓€杞娴嬬粨鏋?*銆?
### 涓嬩竴浣嶅缓璁厛鍋氫粈涔?
1. 浼樺厛璁╃敤鎴峰娴嬪綋鍓?output\DalamudACT.dll锛?2. 閲嶇偣瑙傚療鏈€杩戞棩蹇椾腑鏄惁鍑虹幇锛?   - 宸茬撼鍏ュ彲璺熻釜鍙嬫柟瀵硅薄
   - 宸叉寜浜嬩欢韬唤绾冲叆鍙窡韪弸鏂瑰璞?3. 濡傛灉杩欎簺鏃ュ織宸茬粡鍑虹幇锛屼絾闈㈡澘浠嶆病鏈夊够浣撹锛?   - 浼樺厛缁х画鏌?LocalStatsService.TryGetTrackedActor(...)銆乀ryResolveTrackedSource(...)銆丒ncounterSession.EnsureCombatant(...) 涔嬮棿鐨?actorId 鏄惁浠嶆湁鍙ｅ緞閿欎綅锛?4. 濡傛灉骞讳綋寮€濮嬫垚琛岋紝浣?encounter 浠嶄笉缁撴潫锛?   - 缁х画缁?AreAllPartyMembersOutOfCombat(...) 鐩稿叧鍒嗘敮琛ユ棩蹇楋紝纭鍒板簳鏄皝涓€鐩翠繚鎸?InCombat锛?5. 鍦ㄨ繖鏉￠棴鐜窇閫氬墠锛屾殏鏃朵笉瑕佸ぇ鑼冨洿鍐嶅姩 Hook 灞傘€?
### 鏈妭娑夊強鐨勬牳蹇冩枃浠?
- DalamudACT/DalamudApi.cs
- DalamudACT/Plugin/ACT.cs
- DalamudACT/Stats/LocalStatsService.cs
- DalamudACT/UI/CombatTimelineWindow.cs
- DalamudACT/LogHelper.cs
- DalamudACT/UI/LogUiHelper.cs
- md/SESSION-HANDOFF.md

涓€鍙ヨ瘽鎬荤粨锛?
> 鐜╁鑷繁鐨勭粺璁″凡缁忔仮澶嶏紱褰撳墠鏈棴鐜殑闂宸茬粡鏀舵暃鍒扳€滃够浣撴槸鍚﹁兘琚簨浠剁幇鍦烘垚鍔熸敹缂栵紝浠ュ強杩欎簺瀵硅薄鏄惁杩樺湪鎷栦綇鎴樻枟缁撴潫鍒ゅ畾鈥濊繖涓ゅ眰锛岃€屾渶鏂拌ˉ涓佸凡鍐欏叆浠ｇ爜浣嗗皻寰呯敤鎴风幇鍦洪獙璇併€

## 2026-05-10 补充：单人解限 / NPC 队友 / 幻体副本排查现场

### 当前现场快照（以本节为准）

- 工作目录：`E:\git\DalamudACT`
- 当前分支：`main`
- 当前 HEAD：`0f1d330`
- 当前工作区仍然是脏的，请接手前先执行：
  - `git status --short`
- 截至交接时的工作区状态：
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
- 仍然不要误删：`1.txt`
- 最近一次本地构建已验证通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `0 warnings / 0 errors`
  - 产物：`E:\git\DalamudACT\output\DalamudACT.dll`

### 本轮处理的核心问题

用户本轮主要围绕以下问题连续反馈：

1. 单人解限进入副本时没有数据显示；
2. 单人解限或跟 NPC 队友进入副本时没有数据显示；
3. 玩家修复后恢复正常，但 NPC 幻体仍然没有单独成行；
4. 用户要求把战斗流水中的角色筛选改成下拉框；
5. 后续又反馈：进入战斗后不会结束战斗。

### 本轮已完成的关键改动

- 在 `DalamudACT/DalamudApi.cs` 中把本地玩家身份获取优先切到 `ObjectTable.LocalPlayer`，再回退 `ClientState.LocalPlayer`；
- 在 `DalamudACT/UI/CombatTimelineWindow.cs` 中把战斗流水角色筛选改成下拉框；
- 在 `DalamudACT/LogHelper.cs` 与 `DalamudACT/UI/LogUiHelper.cs` 中增加可进入最近日志摘要的调试日志能力；
- 在 `DalamudACT/Plugin/ACT.cs` 中补强未命中 tracked actor 的调试日志；
- 在 `DalamudACT/Stats/LocalStatsService.cs` 中增加“进战但无流水/统计”的诊断日志；
- 引入 `observedFriendlyActorCache`，允许在事件现场动态收编友方对象；
- 新增 `ObserveFriendlyCombatantIdentity(actorId, name)`，开始允许直接按事件身份把 `XX的幻体` 纳入 tracked actor；
- 在 `ACT.HandleAbility(...)` 的 source / target 两侧都接入了上述观察逻辑；
- 收紧了 combat-end 判定，只让真正相关的玩家 / 队伍成员 / Buddy / 已观察到的相关友方对象参与“是否脱战”的判断。

### 截止交接时已确认与未确认的结论

#### 已确认

- 玩家自己的统计链路已经恢复：
  - 战斗流水能看到玩家自己的技能；
  - DPS 面板能看到玩家自己一行；
- 本地玩家 ID 在这类副本中不再稳定为 0；
- 战斗流水角色筛选改下拉框已完成；
- 最新代码已经能够尝试直接按 `actorId + 幻体名字` 收编友方幻体。

#### 尚未完成现场闭环

- 用户在最新补丁前的最后反馈仍是：
  - 还是没有幻体单独成行；
  - 进入战斗后不会结束战斗；
- 但**最新一版“按事件身份收编幻体 + 收紧结束战斗判定”的补丁之后，还没有拿到用户的新一轮复测结果**。

### 下一位建议先做什么

1. 优先让用户复测当前 `output\DalamudACT.dll`；
2. 重点观察最近日志中是否出现：
   - `已纳入可跟踪友方对象`
   - `已按事件身份纳入可跟踪友方对象`
3. 如果这些日志已经出现，但面板仍没有幻体行：
   - 优先继续查 `LocalStatsService.TryGetTrackedActor(...)`、`TryResolveTrackedSource(...)`、`EncounterSession.EnsureCombatant(...)` 之间的 actorId 是否仍有口径错位；
4. 如果幻体开始成行，但 encounter 仍不结束：
   - 继续给 `AreAllPartyMembersOutOfCombat(...)` 相关分支补日志，确认到底是谁一直保持 `InCombat`；
5. 在这条闭环跑通前，暂时不要大范围再动 Hook 层。

### 本节涉及的核心文件

- `DalamudACT/DalamudApi.cs`
- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/UI/CombatTimelineWindow.cs`
- `DalamudACT/LogHelper.cs`
- `DalamudACT/UI/LogUiHelper.cs`
- `md/SESSION-HANDOFF.md`

一句话总结：

> 玩家自己的统计已经恢复；当前未闭环的问题已经收敛到“幻体是否能被事件现场成功收编，以及这些对象是否还在拖住战斗结束判定”这两层，而最新补丁已写入代码但尚待用户现场验证。
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
