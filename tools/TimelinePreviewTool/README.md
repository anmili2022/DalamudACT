# Timeline Preview Tool

静态时间轴预览工具，用于加载 DalamudACT / cactbot 风格 `.txt` / `.cn.txt` 时间轴，并通过拖动时间滑块模拟插件屏幕上的时间轴窗口内容。

## 使用方法

1. 用浏览器打开 `tools/TimelinePreviewTool/index.html`。
2. 选择一个时间轴文件，例如：

   ```txt
   DalamudACT/Features/Timeline/Data/07-dt/dungeon/klythios.cn.txt
   ```

3. 拖动顶部时间滑块，查看当前时间点未来若干秒内的屏幕显示内容。

## 支持内容

- 普通 timeline 行：`time "text" EventType { ... }`
- cactbot `timeline: \`...\`` 模板块
- `duration` 条目解析
- `Timer` 条目筛选
- `#` 注释机制提示预览
- `读条ID` / `结算ID` 注释元数据过滤
- `--middle--` / `--north--` 等常见内部文本替换
- 模拟插件 `TimelineWindow` 的：
  - 未来秒数
  - 最大显示条数
  - 行间距
  - 面板宽度
  - 5 秒内粉色紧急条

## 注意事项

- 这是离线预览工具，不会执行游戏事件同步，也不会模拟 `jump` / `forcejump` 后的真实运行时间线。
- 显示逻辑用于视觉检查和时间轴文本排版检查，不能替代游戏内同步验证。
