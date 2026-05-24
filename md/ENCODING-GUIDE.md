# 文档与源码编码规范

更新时间：`2026-05-24`

## 目标

避免中文文档再次出现：

- `连续问号`；
- 常见 mojibake 字符；
- `U+FFFD` 替换字符；
- GitHub Release / README / HANDOVER 里中文变问号。

## 统一规则

本仓库文本文件统一使用：

```text
UTF-8
LF 换行
文件末尾保留一个换行
```

已新增：

```text
.editorconfig
tools/Check-TextEncoding.ps1
```

## Windows PowerShell 下的安全写法

### 推荐：直接用 PowerShell 写文件

```powershell
Set-Content -Encoding UTF8 path\file.md -Value <UTF8中文文本>
```

或使用 .NET 明确写 UTF-8：

```powershell
$text = <UTF8中文文本>
[System.IO.File]::WriteAllText(
    'path\file.md',
    $text,
    [System.Text.UTF8Encoding]::new($false))
```

### 推荐：读 Markdown 时显式指定 UTF-8

```powershell
Get-Content -Encoding UTF8 README.md
Get-Content -Encoding UTF8 md/SESSION-HANDOFF.md
```

## 禁止 / 高风险写法

### 不要把中文 here-string 通过管道传给 Python

不要这样写：

```powershell
<中文 here-string> | python -
```

原因：在 Windows PowerShell / 控制台编码组合下，管道可能先把中文降级成 `?`，Python 收到时已经不可恢复。

### 不要依赖默认编码读写中文文档

避免：

```powershell
Get-Content README.md
Set-Content README.md "中文"
```

应显式使用：

```powershell
Get-Content -Encoding UTF8 README.md
Set-Content -Encoding UTF8 README.md "中文"
```

### 不要把已乱码文件当成“编码显示问题”反复转码

如果文件内容层面已经变成 `连续问号`，通常不能靠重新指定 `-Encoding UTF8` 恢复。需要从 Git 历史、备份或上下文重建。

## 编码检查脚本

检查当前工作区变更文件：

```powershell
powershell -ExecutionPolicy Bypass -File tools\Check-TextEncoding.ps1
```

检查全仓库文本文件：

```powershell
powershell -ExecutionPolicy Bypass -File tools\Check-TextEncoding.ps1 -All
```

说明：

- 默认只检查 Git 变更 / 未跟踪文本文件，适合作为提交前检查；
- `-All` 会检查全仓库，可能会报告历史遗留的已损坏文档；
- `md/HANDOVER-LEGACY-MOJIBAKE-ARCHIVE.md` 是故意保留的旧乱码归档，脚本默认跳过。

## 提交前建议流程

```powershell
powershell -ExecutionPolicy Bypass -File tools\Check-TextEncoding.ps1
dotnet build E:\git\DalamudACT\DalamudACT.sln
```

如果检查报出 `疑似乱码`，先打开文件确认，不要直接提交。
