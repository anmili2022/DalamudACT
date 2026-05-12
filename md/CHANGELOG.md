# DPS?? ????

???

- ????????? `0.15.2.11`
- ????????? `Unreleased` ??
- ?????????????
  - `md/SESSION-HANDOFF.md`
  - `md/2026-05-05.md`
  - `md/2026-05-06.md`

## Unreleased

- ????????????????

Current metadata version: `0.15.2.11`

## 0.15.2.11 - 2026-05-13

### ??? DoT ????

- ???????????????? `ObjectTable` ???? / `PartyMember` ????????????????
- ?? DoT ???????????????????????????????????????
- ?? 24 ???? / ????????? DoT / Debuff ??????????? DPS ??????

## 0.15.2.10 - 2026-05-13

### ???????

- ?????? `Classic` / `Ikegami` ?????????????????
- ??????????????????????`Classic` ? `Ikegami` ???????????????????
- ??????????????????? `Classic` / `Ikegami` ??????????????

### Ikegami ???

- ??????? Ikegami overlay?????????????????????tooltip ????????
- ??? DPS ???????????????????????????????????????????
- footer ???? `380px` ????????????????????????

### ?????

- Ikegami ??????????????????????????????????
- ?????????????????????????????????????????????????
- ?????????? `??????`???????????? `RGB`?????????????????????

## 0.15.2.9 - 2026-05-12

### DoT 统计

- 玩家 DoT 主链路已切换为“状态驱动 + 3 秒轮询补 tick”，不再依赖事件包里的 DoT tick 直接记账。
- `DalamudACT/Stats/PlayerDotCatalog.cs` 已作为静态白名单入口，按 `actionId / statusId` 过滤 DoT 候选，避免继续靠技能名字猜测。
- DoT 暴击改为沿用普通技能的暴击率模拟逻辑。
- DoT 活跃状态现在会优先回归到白名单中的稳定技能信息，减少同一个持续伤害在日志里频繁切换 actionId 的情况。

### 概览与流水

- 概览页角色详情新增 `DoT总伤害`，位置在“死亡”下方。
- 战斗流水窗口新增 `只显示技能` 筛选，可直接只看某个角色的某个技能。
- 技能筛选新增搜索输入框，技能较多时可先输入关键字再选择目标技能。

### 兼容与维护

- release notes 模板继续保留 `{{VERSION}}` 占位符，并在正式发布时由 workflow 自动替换。
- 已重新构建验证：`dotnet build E:\git\DalamudACT\DalamudACT.sln`，结果为 `0 warnings / 0 errors`。

## 0.15.2.8 - 2026-05-09

### Floating stats table columns

- fixed column-width persistence so widths are stored by semantic slot instead of current visual order
- hiding a stats column now hides the entire column, not just cell content
- the share column now stretches to fill remaining width after fixed columns are shown or hidden
- enforced a `20px` minimum width for the deaths column

### UI and interaction

- settings window title now shows the loaded plugin assembly version
- main window keeps version text visible for build verification during testing
- historical record preview now supports automatic return to live DPS after the configured timeout
- the floating window keeps the last encounter visible after combat ends, then clears when the next combat actually begins
- status text and empty-state hints now distinguish between "waiting for the next combat" and "collecting fresh combat data"

### Release and maintenance

- `0.15.2.7` formal tag release completed successfully
- the `latest` build/release automation path was re-verified successfully
- release-notes maintenance was improved so future formal releases do not reuse stale version bodies

## 0.15.2.5 - 2026-05-06

### Floating window lock

- added a floating window lock option under window settings
- locking prevents moving or resizing the floating window itself
- metric and history table headers are disabled while locked, so the current user-adjusted widths stay in place and can no longer be dragged

### Settings defaults

- all settings sections now default collapsed except for window settings and data/status

### Test data

- the synthetic `闆跺紡娴嬭瘯鍦篳 sample now contains eight characters for full-party testing

### Documentation

- refreshed release notes and release handoff for `0.15.2.5`
- updated the README handoff link to point at the current release handoff entry

## 0.15.2.4 - 2026-05-06

### 鎮诞绐?

- 鎶樺彔鎬佺粺涓€涓虹缉灏忕獥鍙ｆ樉绀?`DPS` 椤电锛屼笉鍐嶈蛋鍗曠嫭鐨勭瓑寰呮枃妗堝叆鍙?
- 鐐瑰嚮鎶樺彔鎬?`DPS` 椤电鍚庝細鐩存帴灞曞紑绐楀彛骞舵樉绀哄綋鍓嶆暟鎹?

### 鏄剧ず鍒ゅ畾

- 鍙宸茬粡鐢熸垚 encounter 蹇収锛屽氨鍏佽缁熻闈㈡澘鏄剧ず鏁版嵁
- 淇宸茬粡鏀跺埌鎴樻枟蹇収鏃讹紝鐣岄潰浠嶅仠鐣欏湪 `绛夊緟鎴樻枟鏁版嵁...` 鐨勯棶棰?

### 鏂囨。

- `README.md` 澧炲姞浜ゆ帴鍏ュ彛閾炬帴锛屼究浜庣洿鎺ヨ烦杞埌 handoff 鏂囨。

## 0.15.2.3 - 2026-05-06

### 鍙戝竷娴佺▼

- 琛ュ己姝ｅ紡鍙戝竷 workflow锛屽 tag 瑙﹀彂鍖归厤涓庡彂甯冭鏄庤鍙栨祦绋嬬户缁仛鏀跺彛
- 璁?GitHub Release 鐨勬鏂囪鍙栦笌鎵撳寘娴佺▼鏇寸ǔ瀹氾紝鍑忓皯鍥犵紪鐮佹垨瑙﹀彂鏉′欢瀵艰嚧鐨勭┖鍙戝竷椋庨櫓

### 缁存姢浜ゆ帴

- 鏂板浠撳簱鏍圭洰褰?`HANDOVER.md` 浣滀负缁存姢浜ゆ帴鍏ュ彛
- `README.md` 澧炲姞浜ゆ帴鏂囨。鍏ュ彛锛屾柟渚垮悗缁洿鎺ヨ繘鍏?runbook 鍜?release handoff

### 璇存槑

- 鏈増鏈富瑕佹槸鍙戝竷鍩虹璁炬柦鍜岀淮鎶ゆ枃妗ｆ暣鐞?
- 鎻掍欢杩愯鏃跺姛鑳戒笌 `0.15.2.2` 淇濇寔涓€鑷?

## 0.15.2.2 - 2026-05-06

### 鎮诞绐椾氦浜?

- 鎮诞绐楅粯璁ゅ睍寮€灏哄璋冩暣涓?`300x300`
- 鎻掍欢鍔犺浇鍚庨粯璁や互鎶樺彔鎬佹樉绀猴紝涓嶅啀鍏堝睍绀洪〉绛炬爮
- 鎶樺彔鎬佷繚鐣?`绛夊緟鎴樻枟鏁版嵁...` 鏂囨湰锛屽苟涓庡睍寮€鎬佷娇鐢ㄥ悓涓€浣嶇疆
- 鐐瑰嚮 `绛夊緟鎴樻枟鏁版嵁...` 鍙湪灞曞紑鎬佷笌鏃犻〉绛炬姌鍙犳€佷箣闂村垏鎹?
- 鍙抽敭鎶樺彔鎬佹彁绀烘枃鏈粛鍙洿鎺ユ墦寮€璁剧疆绐楀彛

### 鍙戝竷娴佺▼

- 姝ｅ紡鍙戝竷 workflow 鏀逛负鏄惧紡璇诲彇 UTF-8 鍙戝竷璇存槑鏂囦欢
- GitHub Release 浼氳嚜鍔ㄥ甫涓婂彂甯冭鏄庢鏂囷紝閬垮厤鍐嶆鍑虹幇闂彿鎴栫┖鐧芥鏂?

## 0.15.2.1 - 2026-05-06

### 缁熻淇

- 淇涓?NPC 闃熷弸銆佷俊璧栥€佸皬闃熸垚鍛樻垬鏂楁椂浠嶇劧涓嶅嚭缁熻鐨勯棶棰?
- 缁熻闃熷弸鏃舵柊澧炴寜娓告垙闃熶紞妲戒綅 `<1> .. <8>` 瑙ｆ瀽鎴愬憳锛屽拰 AEAssist 鐨勯槦浼嶈В鏋愭€濊矾瀵归綈
- 浠嶄繚鐣?`PartyList`銆乣BuddyList`銆佸璞¤〃鍖归厤浣滀负琛ュ厖鍏滃簳
- NPC 闃熷弸鐜板湪浼氳繘鍏ワ細
  - 浼ゅ / 娌荤枟缁熻
  - 姝讳骸妫€娴?
  - 鑴辨垬缁撴潫鍒ゅ畾

### 鍘嗗彶璁板綍涓庡疄鏃惰鍥?

- 鎴樻枟缁撴潫鍚庝笉鍐嶅仠鐣欏湪 `鍘嗗彶璁板綍` 鏍囩椤?
- 瀵煎叆鍘嗗彶璁板綍鍚庤嚜鍔ㄦ墦寮€鏈€鏂颁竴鏉★紝閬垮厤鐣岄潰涓€鐩存樉绀衡€滅瓑寰呮垬鏂楁暟鎹?..鈥?
- 鍘嗗彶璁板綍椤垫柊澧?`娓呯┖鍘嗗彶` 鎸夐挳
- 娌℃湁瀹炴椂鎴樻枟鏁版嵁浣嗗凡鏈夊巻鍙茶褰曟椂锛屼細鏄庣‘鎻愮ず鍙互鏌ョ湅鍘嗗彶

### 鍏朵粬

- 涓荤獥鍙ｆ樉绀烘彃浠剁増鏈彿
- 灏忎簬 `30` 绉掔殑鎴樻枟涓嶅啓鍏ュ巻鍙茶褰?

## 0.15.2.0 - 2026-05-06

### 璁剧疆涓庢垬鏂楃粨鏉熷垽瀹?

- 璁剧疆绐楀彛鎷嗗垎涓猴細
  - `绐楀彛璁剧疆`
  - `鎴樻枟缁撴潫璁剧疆`
- 鎴樻枟缁撴潫鍒ゅ畾鏀寔锛?
  - `鍏ㄩ槦鑴辨垬锛圥artyList锛夊嵆涓烘垬鏂楃粨鏉焋
  - `鍏ㄩ槦鑴辨垬锛屼笖寤惰繜 X 绉掍负鎴樻枟缁撴潫`

### 鍘嗗彶璁板綍

- 鍘嗗彶璁板綍鏂板寮€濮嬫椂闂淬€佺粨鏉熸椂闂淬€佹椂闀?
- 鍘嗗彶璁板綍鏀寔瀵煎叆 / 瀵煎嚭
- 瀵煎叆瀵煎嚭鏂囦欢鍥哄畾涓烘彃浠堕厤缃洰褰曚腑鐨?`history-records.json`
- 鍘嗗彶璁板綍琛ㄦ牸鏀寔锛?
  - 婊氬姩
  - 鎷栧姩鍒楀
  - 鎮仠鏌ョ湅瀹屾暣鍗曞厓鏍煎唴瀹?

### 闈㈡澘浣撻獙

- `姒傝` 椤甸潰澧炲姞婊氬姩鏉?
- `鏁版嵁涓庣姸鎬乣 鎶樺彔鏍忓姞鍏ュ巻鍙插鍏?/ 瀵煎嚭鍏ュ彛
- manifest 鏀逛负涓嫳鍙岃 `Punchline / Description`
- 琛ラ綈 `Tags`

## 0.15.1.0 - 2026-05-06

- `DPS` 椤甸潰鏂板 `浼ゅ閲廯 鍒?
- 鍘嗗彶璁板綍鏀寔鐐瑰嚮鍥炵湅鏁村満鎴樻枟蹇収
- 鎮诞闈㈡澘 `DPS` 椤电鏀寔鎶樺彔 / 灞曞紑涓庡彸閿墦寮€璁剧疆
- 涓荤獥鍙ｅ叆鍙ｆ敞鍐屼慨澶?
- 浣跨敤璇存槑銆佹洿鏂拌褰曘€佸彂甯冭鏄庡畬鎴愰噸鍐?

## 0.15.0.0 - 2026-05-05

- 鎻掍欢瀹氫綅璋冩暣涓虹嫭绔嬭繍琛岀殑 Dalamud DPS 缁熻鎻掍欢
- 涓嬬嚎鏃х増渚濊禆澶栭儴 `MiniParse` 鐨勭粺璁￠摼璺?
- 鏀逛负鍦ㄦ父鎴忓唴鐩存帴閲囬泦鎴樻枟浜嬩欢骞剁敓鎴愭湰鍦扮粺璁＄粨鏋?
- UI 涓庤緭鍑哄彛寰勬暣浣撳悜 `ACTX / NotACT` 椋庢牸瀵归綈


