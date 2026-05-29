using System;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
    private void DrawStatusObserverSection()
    {
        if (!DrawFirstLevelHeader("状态监控"))
            return;

        DrawSettingCard(
            "##status_observer_card",
            "状态ID监控",
            "实时查看自身和当前目标身上的状态，用于确认 Buff/Debuff 的 StatusId。右键状态行可复制或关注。",
            16f,
            () =>
            {
                ImGui.TextDisabled("窗口");
                ImGui.Separator();
                var show = config.StatusObserver.ShowWindow;
                if (ImGui.Checkbox("显示状态监控窗口", ref show))
                {
                    config.StatusObserver.ShowWindow = show;
                    config.Save();
                }

                var locked = config.StatusObserver.LockWindow;
                if (ImGui.Checkbox("锁定状态监控窗口", ref locked))
                {
                    config.StatusObserver.LockWindow = locked;
                    config.Save();
                }

                DrawCompactHelp("锁定后自动适配内容大小", "锁定状态监控窗口时，窗口不可拖动或缩放，并会根据当前显示内容自动调整大小。未锁定时可手动拖动和调整尺寸。右键状态监控悬浮窗可开关设置窗口。 ");

                ImGui.Dummy(new System.Numerics.Vector2(0f, 3f));
                ImGui.TextDisabled("显示模式");
                ImGui.Separator();

                var displayMode = config.StatusObserver.DisplayMode;
                if (ImGui.RadioButton("文字模式", displayMode == StatusObserverDisplayMode.Text))
                {
                    config.StatusObserver.DisplayMode = StatusObserverDisplayMode.Text;
                    config.Save();
                }

                ImGui.SameLine(0f, 12f);
                if (ImGui.RadioButton("图标模式", displayMode == StatusObserverDisplayMode.Icon))
                {
                    config.StatusObserver.DisplayMode = StatusObserverDisplayMode.Icon;
                    config.Save();
                }

                DrawCompactHelp(
                    config.StatusObserver.DisplayMode == StatusObserverDisplayMode.Icon ? "当前：图标模式" : "当前：文字模式",
                    "文字模式显示 ID、名称、剩余时间、层数和来源；图标模式更紧凑，鼠标悬停图标可查看完整信息。 ");

                ImGui.Dummy(new System.Numerics.Vector2(0f, 3f));
                ImGui.TextDisabled("内容");
                ImGui.Separator();

                var showSelf = config.StatusObserver.ShowSelfStatuses;
                if (ImGui.Checkbox("显示自身状态", ref showSelf))
                {
                    config.StatusObserver.ShowSelfStatuses = showSelf;
                    config.Save();
                }

                ImGui.SameLine(0f, 24f);
                var showTarget = config.StatusObserver.ShowTargetStatuses;
                if (ImGui.Checkbox("显示目标状态", ref showTarget))
                {
                    config.StatusObserver.ShowTargetStatuses = showTarget;
                    config.Save();
                }

                var hidePermanent = config.StatusObserver.HidePermanentStatuses;
                if (ImGui.Checkbox("隐藏永久状态", ref hidePermanent))
                {
                    config.StatusObserver.HidePermanentStatuses = hidePermanent;
                    config.Save();
                }

                ImGui.SameLine(0f, 24f);
                var showSource = config.StatusObserver.ShowSourceInfo;
                if (ImGui.Checkbox("显示来源信息", ref showSource))
                {
                    config.StatusObserver.ShowSourceInfo = showSource;
                    config.Save();
                }

                var showStatusIdUnderIcon = config.StatusObserver.ShowStatusIdUnderIcon;
                if (ImGui.Checkbox("图标下方显示ID", ref showStatusIdUnderIcon))
                {
                    config.StatusObserver.ShowStatusIdUnderIcon = showStatusIdUnderIcon;
                    config.Save();
                }

                ImGui.Dummy(new System.Numerics.Vector2(0f, 3f));
                ImGui.TextDisabled("数量限制");
                ImGui.Separator();

                var selfMax = Math.Clamp(config.StatusObserver.SelfMaxStatuses, 1, 200);
                if (DrawLabeledSliderInt("自身状态最大显示数量", "##status_self_max", ref selfMax, 1, 100, "%d"))
                {
                    config.StatusObserver.SelfMaxStatuses = selfMax;
                    config.Save();
                }

                var targetMax = Math.Clamp(config.StatusObserver.TargetMaxStatuses, 1, 200);
                if (DrawLabeledSliderInt("目标状态最大显示数量", "##status_target_max", ref targetMax, 1, 100, "%d"))
                {
                    config.StatusObserver.TargetMaxStatuses = targetMax;
                    config.Save();
                }

                if (config.StatusObserver.FavoriteStatusIds.Count > 0)
                {
                    ImGui.Dummy(new System.Numerics.Vector2(0f, 3f));
                    ImGui.TextUnformatted("关注状态ID");
                    ImGui.Separator();
                    foreach (var statusId in config.StatusObserver.FavoriteStatusIds.ToList())
                    {
                        ImGui.TextUnformatted(statusId.ToString());
                        ImGui.SameLine();
                        if (ImGui.SmallButton($"删除##favorite_status_{statusId}"))
                        {
                            config.StatusObserver.FavoriteStatusIds.RemoveAll(id => id == statusId);
                            config.Save();
                        }
                    }
                }
            });
    }
}
