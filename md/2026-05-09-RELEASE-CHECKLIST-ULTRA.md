# 2026-05-09 发版极简清单

适用版本：`0.15.2.8`

只保留最关键的 10 项，适合打印、贴墙、最后过目。

- [ ] 版本号已统一到 `0.15.2.8`
  - `DalamudACT/DalamudACT.csproj`
  - `DalamudACT/DalamudACT.json`
  - `Data/DalamudACT.json`
  - `repo.json`
- [ ] `repo.json` 三个下载链接都指向 `0.15.2.8`
- [ ] `md/CHANGELOG.md` 和 `md/RELEASE-NOTES.md` 已更新
- [ ] 本地 Release 构建通过
- [ ] `main` 已提交并推送
- [ ] 正式 tag 已创建并推送：`0.15.2.8`
- [ ] `release.yml` 已成功跑完
- [ ] Release 里有 `DalamudACT.zip`
- [ ] 游戏内版本显示 `0.15.2.8`
- [ ] 游戏内行为正确：
  - 进战斗能出数
  - 脱战后保留上一场数据
  - 下一场战斗开始时先清空旧数据
  - 状态文案符合预期

