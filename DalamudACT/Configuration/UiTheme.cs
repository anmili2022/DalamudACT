using System.Numerics;

namespace DalamudACT;

public enum UiThemeId
{
    Sakura,
    SakuraNight,
    Ocean,
    OceanNight,
    Forest,
    Purple,
    Sunset,
    Monochrome,
    Cyber,
}

public readonly struct UiThemeColors
{
    public readonly string Label;
    public readonly Vector4 Text;
    public readonly Vector4 TextDisabled;
    public readonly Vector4 WindowBg;
    public readonly Vector4 Panel;
    public readonly Vector4 PanelDark;
    public readonly Vector4 Accent;
    public readonly Vector4 AccentSoft;
    public readonly Vector4 Border;
    public readonly Vector4 Ok;
    public readonly Vector4 FrameBgHoveredExtra;
    public readonly Vector4 CheckMark;

    public UiThemeColors(
        string label,
        Vector4 text, Vector4 textDisabled,
        Vector4 windowBg, Vector4 panel, Vector4 panelDark,
        Vector4 accent, Vector4 accentSoft, Vector4 border,
        Vector4 ok, Vector4 frameBgHoveredExtra, Vector4 checkMark)
    {
        Label = label;
        Text = text;
        TextDisabled = textDisabled;
        WindowBg = windowBg;
        Panel = panel;
        PanelDark = panelDark;
        Accent = accent;
        AccentSoft = accentSoft;
        Border = border;
        Ok = ok;
        FrameBgHoveredExtra = frameBgHoveredExtra;
        CheckMark = checkMark;
    }

    public static UiThemeColors Get(UiThemeId id) => id switch
    {
        UiThemeId.Sakura => new UiThemeColors(
            "桜 Sakura",
            new Vector4(0.17f, 0.11f, 0.13f, 1f),
            new Vector4(0.52f, 0.42f, 0.46f, 1f),
            new Vector4(1f, 0.97f, 0.98f, 1f),
            new Vector4(0.95f, 0.87f, 0.89f, 1f),
            new Vector4(0.88f, 0.76f, 0.79f, 1f),
            new Vector4(0.85f, 0.44f, 0.53f, 1f),
            new Vector4(0.93f, 0.82f, 0.84f, 1f),
            new Vector4(0.9f, 0.78f, 0.81f, 1f),
            new Vector4(0.18f, 0.65f, 0.43f, 1f),
            new Vector4(0.96f, 0.86f, 0.88f, 1f),
            new Vector4(0.26f, 0.47f, 0.77f, 1f)),

        UiThemeId.SakuraNight => new UiThemeColors(
            "夜樱 Sakura Night",
            new Vector4(0.96f, 0.92f, 0.93f, 1f),
            new Vector4(0.71f, 0.61f, 0.64f, 1f),
            new Vector4(0.23f, 0.16f, 0.19f, 1f),
            new Vector4(0.27f, 0.19f, 0.22f, 1f),
            new Vector4(0.20f, 0.14f, 0.16f, 1f),
            new Vector4(0.91f, 0.58f, 0.54f, 1f),
            new Vector4(0.35f, 0.22f, 0.25f, 1f),
            new Vector4(0.33f, 0.24f, 0.27f, 1f),
            new Vector4(0.49f, 0.65f, 0.77f, 1f),
            new Vector4(0.38f, 0.27f, 0.30f, 1f),
            new Vector4(0.66f, 0.77f, 1f, 1f)),

        UiThemeId.Ocean => new UiThemeColors(
            "海 Ocean Breeze",
            new Vector4(0.10f, 0.16f, 0.21f, 1f),
            new Vector4(0.37f, 0.48f, 0.56f, 1f),
            new Vector4(0.96f, 0.98f, 0.99f, 1f),
            new Vector4(0.89f, 0.94f, 0.97f, 1f),
            new Vector4(0.86f, 0.91f, 0.95f, 1f),
            new Vector4(0.24f, 0.56f, 0.79f, 1f),
            new Vector4(0.78f, 0.88f, 0.94f, 1f),
            new Vector4(0.76f, 0.83f, 0.88f, 1f),
            new Vector4(0.18f, 0.60f, 0.43f, 1f),
            new Vector4(0.82f, 0.90f, 0.95f, 1f),
            new Vector4(0.24f, 0.56f, 0.79f, 1f)),

        UiThemeId.OceanNight => new UiThemeColors(
            "深海 Ocean Night",
            new Vector4(0.91f, 0.94f, 0.96f, 1f),
            new Vector4(0.56f, 0.66f, 0.72f, 1f),
            new Vector4(0.12f, 0.18f, 0.23f, 1f),
            new Vector4(0.15f, 0.22f, 0.28f, 1f),
            new Vector4(0.11f, 0.16f, 0.20f, 1f),
            new Vector4(0.36f, 0.69f, 0.88f, 1f),
            new Vector4(0.16f, 0.25f, 0.31f, 1f),
            new Vector4(0.21f, 0.30f, 0.36f, 1f),
            new Vector4(0.43f, 0.80f, 0.60f, 1f),
            new Vector4(0.18f, 0.27f, 0.34f, 1f),
            new Vector4(0.36f, 0.69f, 0.88f, 1f)),

        UiThemeId.Forest => new UiThemeColors(
            "森 Forest Canopy",
            new Vector4(0.12f, 0.18f, 0.10f, 1f),
            new Vector4(0.36f, 0.48f, 0.33f, 1f),
            new Vector4(0.98f, 1f, 0.98f, 1f),
            new Vector4(0.92f, 0.96f, 0.91f, 1f),
            new Vector4(0.86f, 0.92f, 0.85f, 1f),
            new Vector4(0.29f, 0.62f, 0.35f, 1f),
            new Vector4(0.78f, 0.88f, 0.77f, 1f),
            new Vector4(0.77f, 0.85f, 0.75f, 1f),
            new Vector4(0.23f, 0.54f, 0.29f, 1f),
            new Vector4(0.80f, 0.90f, 0.79f, 1f),
            new Vector4(0.29f, 0.62f, 0.35f, 1f)),

        UiThemeId.Purple => new UiThemeColors(
            "紫 Royal Purple",
            new Vector4(0.14f, 0.11f, 0.19f, 1f),
            new Vector4(0.44f, 0.36f, 0.51f, 1f),
            new Vector4(0.99f, 0.98f, 1f, 1f),
            new Vector4(0.93f, 0.89f, 0.96f, 1f),
            new Vector4(0.90f, 0.85f, 0.94f, 1f),
            new Vector4(0.48f, 0.31f, 0.66f, 1f),
            new Vector4(0.83f, 0.77f, 0.90f, 1f),
            new Vector4(0.82f, 0.77f, 0.88f, 1f),
            new Vector4(0.23f, 0.60f, 0.42f, 1f),
            new Vector4(0.87f, 0.83f, 0.93f, 1f),
            new Vector4(0.48f, 0.31f, 0.66f, 1f)),

        UiThemeId.Sunset => new UiThemeColors(
            "暮 Sunset Glow",
            new Vector4(0.20f, 0.13f, 0.09f, 1f),
            new Vector4(0.54f, 0.42f, 0.35f, 1f),
            new Vector4(1f, 0.97f, 0.95f, 1f),
            new Vector4(0.96f, 0.91f, 0.86f, 1f),
            new Vector4(0.94f, 0.88f, 0.83f, 1f),
            new Vector4(0.83f, 0.46f, 0.23f, 1f),
            new Vector4(0.91f, 0.82f, 0.75f, 1f),
            new Vector4(0.88f, 0.79f, 0.74f, 1f),
            new Vector4(0.23f, 0.60f, 0.42f, 1f),
            new Vector4(0.94f, 0.88f, 0.84f, 1f),
            new Vector4(0.83f, 0.46f, 0.23f, 1f)),

        UiThemeId.Monochrome => new UiThemeColors(
            "极简 Monochrome",
            new Vector4(0.10f, 0.10f, 0.10f, 1f),
            new Vector4(0.42f, 0.42f, 0.42f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(0.94f, 0.94f, 0.94f, 1f),
            new Vector4(0.91f, 0.91f, 0.91f, 1f),
            new Vector4(0.33f, 0.33f, 0.33f, 1f),
            new Vector4(0.87f, 0.87f, 0.87f, 1f),
            new Vector4(0.82f, 0.82f, 0.82f, 1f),
            new Vector4(0.23f, 0.54f, 0.29f, 1f),
            new Vector4(0.90f, 0.90f, 0.90f, 1f),
            new Vector4(0.33f, 0.33f, 0.33f, 1f)),

        UiThemeId.Cyber => new UiThemeColors(
            "赛博 Cyberpunk",
            new Vector4(0.90f, 0.93f, 0.95f, 1f),
            new Vector4(0.55f, 0.58f, 0.62f, 1f),
            new Vector4(0.09f, 0.10f, 0.13f, 1f),
            new Vector4(0.11f, 0.14f, 0.20f, 1f),
            new Vector4(0.07f, 0.09f, 0.12f, 1f),
            new Vector4(0.35f, 0.65f, 1f, 1f),
            new Vector4(0.11f, 0.16f, 0.26f, 1f),
            new Vector4(0.19f, 0.21f, 0.24f, 1f),
            new Vector4(0.25f, 0.73f, 0.31f, 1f),
            new Vector4(0.13f, 0.19f, 0.30f, 1f),
            new Vector4(0.35f, 0.65f, 1f, 1f)),

        _ => Get(UiThemeId.Sakura),
    };
}
