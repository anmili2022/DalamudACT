# 2026-05-09 发版最后检查清单

适用版本：`0.15.2.8`

这份清单只保留发版前后最关键的检查项，适合最后过一遍。

## 1. 发版前

- [ ] `DalamudACT/DalamudACT.csproj`、`DalamudACT/DalamudACT.json`、`Data/DalamudACT.json`、`repo.json` 的版本号都已统一到 `0.15.2.8`
- [ ] `repo.json` 里的 3 个下载链接都已指向 `0.15.2.8`
- [ ] `md/CHANGELOG.md` 和 `md/RELEASE-NOTES.md` 已写好本次发版内容
- [ ] 本地构建通过：
  - `dotnet build E:\git\DalamudACT\DalamudACT.sln`
  - `dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Release -p:Version=0.15.2.8 -p:FileVersion=0.15.2.8 -p:AssemblyVersion=0.15.2.8`

## 2. 提交与打 tag

- [ ] `main` 已提交并推送到 `origin`
- [ ] 已创建无签名正式 tag：
  - `git -C E:\git\DalamudACT -c tag.gpgSign=false tag -a 0.15.2.8 -m "DalamudACT 0.15.2.8"`
  - `git -C E:\git\DalamudACT push origin 0.15.2.8`
- [ ] `latest` / `testing_*` 没有被误用到正式发版里

## 3. GitHub Release

- [ ] `release.yml` 已成功跑完
- [ ] Release 标题为 `DalamudACT 0.15.2.8`
- [ ] Release 附件里有 `DalamudACT.zip`
- [ ] `repo.json` 三个下载链接与 Release tag 完全一致

## 4. 游戏内验证

- [ ] 插件窗口版本显示 `0.15.2.8`
- [ ] 插件管理界面版本显示 `0.15.2.8`
- [ ] 进战斗后能正常出数
- [ ] 脱战后悬浮窗保留上一场数据
- [ ] 下一次进战斗时会先清空旧数据，再等待新数据
- [ ] 状态文案符合预期：
  - 脱战后：`上一场战斗已结束，等待下一场战斗...`
  - 新战斗已开始但尚未出第一条数据：`已进入战斗，正在收集新战斗数据...`

## 5. 出问题时优先看

1. `DalamudACT/Stats/LocalStatsService.cs`
2. `DalamudACT/Plugin/ACT.cs`
3. `DalamudACT/UI/StatsPanel.cs`
4. `md/2026-05-09-RELEASE-HANDOFF.md`
5. `md/RELEASE-RUNBOOK.md`

