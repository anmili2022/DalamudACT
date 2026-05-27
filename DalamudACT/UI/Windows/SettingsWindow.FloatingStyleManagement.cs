using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
    private void DrawFloatingStyleFileManagementSection()
    {
        ImGui.Dummy(new Vector2(0f, 2f));
        ImGui.Separator();
        if (!ImGui.CollapsingHeader("样式管理"))
            return;

        ImGui.TextDisabled("样式分享码");
        foreach (var style in new[]
                 {
                     FloatingStatsDisplayStyle.Classic,
                     FloatingStatsDisplayStyle.Ikegami,
                     FloatingStatsDisplayStyle.Minimal,
                 })
        {
            if (style != FloatingStatsDisplayStyle.Classic)
                ImGui.SameLine();

            if (!ImGui.Button($"生成并复制 {GetFloatingStyleShareCodeStyleLabel(style)} 分享码"))
                continue;

            if (config.TryGenerateFloatingStyleShareCode(
                    style,
                    out var shareCode,
                    out var message))
            {
                floatingStyleShareCode = shareCode;
                ImGui.SetClipboardText(shareCode);
            }

            floatingStyleTransferStatusText = message;
        }

        DrawCompactHelp("生成后会自动复制到剪贴板。", "对外分享时直接发送整段文本即可。");

        var shareCodeBoxHeight = ImGui.GetTextLineHeightWithSpacing() * 3.0f;

        DrawCompactHelp("同一个输入框可粘贴或暂存分享码。", "复制按钮适合转发现成内容；导入时会自动识别 Classic / Ikegami / Minimal。");
        if (ImGui.Button("复制当前分享码"))
        {
            ImGui.SetClipboardText(floatingStyleShareCode ?? string.Empty);
            floatingStyleTransferStatusText = "已复制当前分享码。";
        }

        ImGui.SameLine();
        if (ImGui.Button("清空分享码"))
        {
            floatingStyleShareCode = string.Empty;
            floatingStyleTransferStatusText = "已清空分享码输入框。";
        }

        floatingStyleShareCode ??= string.Empty;
        ImGui.InputTextMultiline(
            "##floating_style_share_code",
            ref floatingStyleShareCode,
            65535,
            new Vector2(-1f, shareCodeBoxHeight));

        if (config.TryPeekFloatingStyleShareCodeStyle(floatingStyleShareCode, out var detectedStyle))
        {
            ImGui.TextDisabled($"已识别分享码样式：{GetFloatingStyleShareCodeStyleLabel(detectedStyle)}");
        }
        else if (!string.IsNullOrWhiteSpace(floatingStyleShareCode))
        {
            ImGui.TextDisabled("当前输入内容还不是可识别的分享码。");
        }

        if (ImGui.Button("按分享码标识导入"))
        {
            config.ImportFloatingStyleShareCode(
                floatingStyleShareCode,
                out floatingStyleTransferStatusText);
        }

        ImGui.Dummy(new Vector2(0f, 4f));
        ImGui.Separator();
        ImGui.TextDisabled("按样式恢复默认");

        foreach (var style in new[]
                 {
                     FloatingStatsDisplayStyle.Classic,
                     FloatingStatsDisplayStyle.Ikegami,
                     FloatingStatsDisplayStyle.Minimal,
                 })
        {
            if (style != FloatingStatsDisplayStyle.Classic)
                ImGui.SameLine();

            if (!ImGui.Button($"恢复 {GetFloatingStyleShareCodeStyleLabel(style)} 默认"))
                continue;

            config.ResetFloatingStyleToDefaults(style, out floatingStyleTransferStatusText);
            if (style == config.FloatingStatsDisplayStyle)
            {
                StatsPanel.RequestMetricColumnWidthReset();
                StatsPanel.RequestHistoryColumnWidthReset();
            }
        }

        DrawCompactHelp("只恢复指定样式的默认设置。", "恢复当前正在使用的样式时，会立即刷新当前界面；其它样式会写回各自样式文件，等切过去时生效。");

        if (!string.IsNullOrWhiteSpace(floatingStyleTransferStatusText))
            ImGui.TextWrapped(floatingStyleTransferStatusText);
    }

    private static string GetFloatingDisplayStyleLabel(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Classic => "Classic（经典表格）",
            FloatingStatsDisplayStyle.Ikegami => "Ikegami",
            FloatingStatsDisplayStyle.Minimal => "Minimal（极简样式）",
            _ => style.ToString(),
        };

    private static string GetFloatingDisplayStyleDescription(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Classic => "经典表格布局，保留列宽、固定列宽和表格行高等旧参数。",
            FloatingStatsDisplayStyle.Ikegami => "横向条带卡片布局，使用专属的尺寸、透明度、滚动条与 footer 参数。",
            FloatingStatsDisplayStyle.Minimal => "极简表格布局：固定只显示 DPS，无页签；职业列与秒伤列会合并到占比条文字。",
            _ => "未识别的展示样式。",
        };

    private static string GetIkegamiBoxAlignmentLabel(IkegamiBoxAlignment alignment)
        => alignment switch
        {
            IkegamiBoxAlignment.Left => "左对齐",
            IkegamiBoxAlignment.Center => "居中",
            IkegamiBoxAlignment.Right => "右对齐",
            _ => alignment.ToString(),
        };

    private static string GetFloatingStyleShareCodeStyleLabel(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Ikegami => "Ikegami",
            FloatingStatsDisplayStyle.Minimal => "Minimal",
            _ => "Classic",
        };
}
