using System;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
    private void DrawStatusObserverSection()
    {
        if (!DrawFirstLevelHeader("状态观察"))
            return;

        DrawSettingCard(
            "##status_observer_card",
            "状态ID观察",
            "实时查看自身和当前目标身上的状态，用于确认 Buff/Debuff 的 StatusId。右键状态行可复制或关注。",
            13f,
            () =>
            {
                var show = config.StatusObserver.ShowWindow;
                if (ImGui.Checkbox("显示状态观察窗口", ref show))
                {
                    config.StatusObserver.ShowWindow = show;
                    config.Save();
                }

                var locked = config.StatusObserver.LockWindow;
                if (ImGui.Checkbox("锁定状态观察窗口", ref locked))
                {
                    config.StatusObserver.LockWindow = locked;
                    config.Save();
                }

                var showSelf = config.StatusObserver.ShowSelfStatuses;
                if (ImGui.Checkbox("显示自身状态", ref showSelf))
                {
                    config.StatusObserver.ShowSelfStatuses = showSelf;
                    config.Save();
                }

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

                var showSource = config.StatusObserver.ShowSourceInfo;
                if (ImGui.Checkbox("显示来源信息", ref showSource))
                {
                    config.StatusObserver.ShowSourceInfo = showSource;
                    config.Save();
                }

                var selfMax = Math.Clamp(config.StatusObserver.SelfMaxStatuses, 1, 200);
                if (ImGui.SliderInt("自身状态最大显示数量", ref selfMax, 1, 100))
                {
                    config.StatusObserver.SelfMaxStatuses = selfMax;
                    config.Save();
                }

                var targetMax = Math.Clamp(config.StatusObserver.TargetMaxStatuses, 1, 200);
                if (ImGui.SliderInt("目标状态最大显示数量", ref targetMax, 1, 100))
                {
                    config.StatusObserver.TargetMaxStatuses = targetMax;
                    config.Save();
                }

                if (config.StatusObserver.FavoriteStatusIds.Count > 0)
                {
                    ImGui.Separator();
                    ImGui.TextUnformatted("关注状态ID");
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
