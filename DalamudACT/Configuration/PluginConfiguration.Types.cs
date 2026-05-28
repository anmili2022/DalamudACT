using System;
using System.Numerics;

namespace DalamudACT;

public enum StatsBarColorMode
{
    Theme = 0,
    Single = 1,
}

public enum SelfHighlightColorMode
{
    SunlightYellow = 0,
    WarmGold = 1,
    RosePink = 2,
    WhiteBlack = 3,
}

public enum CombatEndRule
{
    PartyList = 0,
    PartyListWithDelay = 1,
}

public enum FloatingStatsParticipantDisplayMode
{
    Auto = 0,
    PlayersOnly = 1,
    PlayersAndFriendlyNpc = 2,
    PlayersAndHostileNpc = 3,
}

public enum FloatingStatsDisplayStyle
{
    Classic = 0,
    Ikegami = 1,
    Minimal = 2,
}

public enum IkegamiBoxAlignment
{
    Left = 0,
    Center = 1,
    Right = 2,
}

public enum TimelineTtsContentMode
{
    MechanicAndSkill = 0,
    MechanicOnly = 1,
    SkillOnly = 2,
}

[Serializable]
public sealed class ThemeBarColorSetting
{
    public float R = 1f;
    public float G = 1f;
    public float B = 1f;
    public float A = 0.92f;

    public ThemeBarColorSetting()
    {
    }

    public ThemeBarColorSetting(Vector4 color)
        => Set(color);

    public Vector4 ToVector4()
        => new(R, G, B, A);

    public void Set(Vector4 color)
    {
        R = Math.Clamp(color.X, 0f, 1f);
        G = Math.Clamp(color.Y, 0f, 1f);
        B = Math.Clamp(color.Z, 0f, 1f);
        A = Math.Clamp(color.W, 0.2f, 1f);
    }
}
