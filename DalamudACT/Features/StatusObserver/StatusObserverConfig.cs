using System.Collections.Generic;

namespace DalamudACT;

public enum StatusObserverDisplayMode
{
    Text = 0,
    Icon = 1,
}

public sealed class StatusObserverConfig
{
    public bool ShowWindow = false;
    public bool LockWindow = false;
    public float WindowOpacity = 0.9f;
    public StatusObserverDisplayMode DisplayMode = StatusObserverDisplayMode.Text;
    public bool ShowSelfStatuses = true;
    public bool ShowTargetStatuses = true;
    public bool HidePermanentStatuses = true;
    public bool ShowSourceInfo = true;
    public bool ShowStatusIdUnderIcon = false;
    public int SelfMaxStatuses = 40;
    public int TargetMaxStatuses = 40;
    public List<uint> FavoriteStatusIds = new();
}
