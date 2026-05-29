using System;

namespace DalamudACT;

[Flags]
public enum DebugLogModule
{
    None = 0,
    PluginHook = 1 << 0,
    Timeline = 1 << 1,
    DamageStats = 1 << 2,
    Dot = 1 << 3,
    StatusObserver = 1 << 4,
    CommandChat = 1 << 5,
    Configuration = 1 << 6,
    All = PluginHook | Timeline | DamageStats | Dot | StatusObserver | CommandChat | Configuration,
}
