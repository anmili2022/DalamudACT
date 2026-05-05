# RELEASE RUNBOOK

## 目的

这份文档只解决一件事：

- 下次发版时，不再重新试版本号、tag 规则、workflow 入口和打包方式

## 结论先写前面

当前仓库的正式发布流程固定为：

1. 更新代码和文档
2. 统一所有版本引用
3. 本地构建通过
4. 推送 `master`
5. 推送正式 tag
6. 由 `.github/workflows/release.yml` 自动创建 GitHub Release

正式版本不要走：

- `testing_*` tag
- `latest` / nightly

## 正式版本要改哪些文件

把下面这些文件里的版本号统一改成同一个值，例如 `0.15.2.1`：

- `DalamudACT/DalamudACT.csproj`
- `DalamudACT/DalamudACT.json`
- `Data/DalamudACT.json`
- `repo.json`
- `md/CHANGELOG.md`
- `md/RELEASE-NOTES.md`

### 具体字段

#### `DalamudACT/DalamudACT.csproj`

- `<AssemblyVersion>`

#### `DalamudACT/DalamudACT.json`

- `"AssemblyVersion"`

#### `Data/DalamudACT.json`

- `"AssemblyVersion"`

#### `repo.json`

- `"AssemblyVersion"`
- `"TestingAssemblyVersion"`
- `"DownloadLinkInstall"`
- `"DownloadLinkTesting"`
- `"DownloadLinkUpdate"`
- `"LastUpdated"`

## 正式发布前的本地验证

先跑：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

如需手动验证 release 构建参数，用：

```powershell
dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Release -p:Version=0.15.2.1 -p:FileVersion=0.15.2.1 -p:AssemblyVersion=0.15.2.1
```

## 正式发布命令顺序

### 1. 提交本次修改

```powershell
git -C E:\git\DalamudACT status --short
git -C E:\git\DalamudACT add .
git -C E:\git\DalamudACT commit -m "fix: finalize npc party tracking and release docs"
```

### 2. 推送主分支

```powershell
git -C E:\git\DalamudACT push origin master
```

### 3. 创建正式 tag

```powershell
git -C E:\git\DalamudACT -c tag.gpgSign=false tag -a 0.15.2.1 -m "DalamudACT 0.15.2.1"
git -C E:\git\DalamudACT push origin 0.15.2.1
```

说明：

- 当前这台机器启用了 `tag.gpgSign=true`
- 直接执行 `git tag 0.15.2.1` 会被 GPG 签名流程卡住
- 本次实际可用命令是上面这条：临时关闭 tag 签名，再创建带注释 tag

## GitHub Actions 对应关系

### 正式发版

- workflow：`.github/workflows/release.yml`
- 触发：普通 tag
- 示例：`0.15.2.1`

### 测试发版

- workflow：`.github/workflows/test_release.yml`
- 触发：`testing_*`
- 示例：`testing_0.15.2.1`

### nightly/latest

- workflow：`.github/workflows/build.yml`
- 用途：分支构建和 `latest`
- 不用于当前正式版本仓库发版

## release.yml 当前打包规则

正式 workflow 会：

1. 清理 `output`
2. 下载 Dalamud 依赖
3. 用 tag 版本执行 Release 构建
4. 更新输出目录中的 manifest 版本
5. 打包以下文件为 `DalamudACT.zip`
   - `output/DalamudACT.dll`
   - `output/DalamudACT.json`
   - `output/DalamudACT.deps.json`
6. 创建 GitHub Release 并上传 zip

## 发布后要检查什么

### 仓库侧

1. tag 是否存在
2. Actions 的 `Create Release` 是否成功
3. Release 页面是否生成
4. 附件里是否有 `DalamudACT.zip`

### 元数据侧

1. `repo.json` 的下载链接是否都指向同一个 tag
2. manifest 版本是否与 tag 一致

### 游戏侧

1. 插件是否能正常加载
2. 主窗口版本号是否正确
3. NPC 队友战斗是否能正常统计

## 如果自动 release 失败

不要先乱改 workflow，先按这个顺序查：

1. 看 `.github/workflows/release.yml` 是否被当前 tag 触发
2. 看 Actions 日志里失败在哪一步
3. 看 zip 是否已经构建出来
4. 看 tag 名称是否和版本号一致
5. 看 `repo.json` 是否已经提前指向该 tag

## 手动兜底方案

如果 workflow 暂时失败，但必须先发包：

1. 本地执行 release 构建
2. 手动确认 `output` 下有：
   - `DalamudACT.dll`
   - `DalamudACT.json`
   - `DalamudACT.deps.json`
3. 本地压缩成 `DalamudACT.zip`
4. 在 GitHub 上用同名 tag 手动创建 Release，并上传 zip
5. 保持 `repo.json` 中的版本号和下载链接与这个 tag 完全一致

## 不要再试验的点

- 正式版本不要用 `testing_*` tag
- 正式版本不要依赖 `build.yml` 的 `latest`
- 这台机器如果直接 `git tag`，会被 `tag.gpgSign=true` 卡住；请直接用 `git -c tag.gpgSign=false tag -a ...`
- tag 名称必须和版本号完全一致
- `repo.json` 的下载链接必须提前改到目标 tag
- 版本号不要只改 `csproj`，manifest 和 `repo.json` 也要一起改
