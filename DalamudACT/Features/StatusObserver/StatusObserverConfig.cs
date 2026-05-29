using System.Collections.Generic;

namespace DalamudACT;

public sealed class StatusObserverConfig
{
    public bool ShowWindow = false;
    public bool LockWindow = false;
    public bool ShowSelfStatuses = true;
    public bool ShowTargetStatuses = true;
    public bool HidePermanentStatuses = true;
    public bool ShowSourceInfo = true;
    public int SelfMaxStatuses = 40;
    public int TargetMaxStatuses = 40;
    public List<uint> FavoriteStatusIds = new();
}
