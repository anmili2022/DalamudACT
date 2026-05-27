using System;
using System.Collections.Generic;
using System.Numerics;

namespace DalamudACT;

public sealed partial class PluginConfiguration
{
    public Vector4 GetSingleBarColor()
        => new(SingleBarColorR, SingleBarColorG, SingleBarColorB, SingleBarColorA);

    public void SetSingleBarColor(Vector4 color)
    {
        SingleBarColorR = Math.Clamp(color.X, 0f, 1f);
        SingleBarColorG = Math.Clamp(color.Y, 0f, 1f);
        SingleBarColorB = Math.Clamp(color.Z, 0f, 1f);
        SingleBarColorA = Math.Clamp(color.W, 0.2f, 1f);
    }

    public Vector4 GetThemeBarColor(string? jobName)
    {
        if (!string.IsNullOrWhiteSpace(jobName) && ThemeBarColors.TryGetValue(jobName, out var configured))
            return ApplyThemeBarOpacity(configured.ToVector4());

        return JobThemePalette.TryGetDefaultColor(jobName, out var fallback)
            ? ApplyThemeBarOpacity(fallback)
            : new Vector4(0.25f, 0.65f, 1f, ThemeBarOpacity);
    }

    public void SetThemeBarColor(string jobName, Vector4 color)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            return;

        ThemeBarColors[jobName] = new ThemeBarColorSetting(color);
    }

    public void ResetThemeBarColors()
    {
        ThemeBarColors = new Dictionary<string, ThemeBarColorSetting>();
        foreach (var entry in JobThemePalette.Entries)
            ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
    }

    public void ApplyRoleThemeBarColors()
    {
        ThemeBarColors = new Dictionary<string, ThemeBarColorSetting>();
        foreach (var entry in JobThemePalette.Entries)
            ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(GetRoleThemeBarColor(entry.Category));
    }

    public Vector4 GetSelfHighlightBarColor()
    {
        var color = SelfHighlightColor switch
        {
            SelfHighlightColorMode.WarmGold => new Vector4(0.984f, 0.749f, 0.141f, 1f),
            SelfHighlightColorMode.RosePink => new Vector4(0.984f, 0.443f, 0.522f, 1f),
            SelfHighlightColorMode.WhiteBlack => new Vector4(1f, 1f, 1f, 1f),
            _ => new Vector4(1.000f, 0.902f, 0.427f, 1f),
        };

        return ApplyThemeBarOpacity(color);
    }

    private static Vector4 GetRoleThemeBarColor(string category)
        => category switch
        {
            "坦克" => new Vector4(0.231f, 0.510f, 0.965f, DefaultThemeBarOpacity),
            "治疗" => new Vector4(0.133f, 0.773f, 0.369f, DefaultThemeBarOpacity),
            _ => new Vector4(0.937f, 0.267f, 0.267f, DefaultThemeBarOpacity),
        };

    public void NormalizeCustomFriendlyNpcNames()
    {
        var normalizedNames = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (CustomFriendlyNpcNames != null)
        {
            foreach (var name in CustomFriendlyNpcNames)
            {
                var normalizedName = NormalizeFriendlyNpcNameForCatalog(name);
                if (string.IsNullOrWhiteSpace(normalizedName) || !seen.Add(normalizedName))
                    continue;

                normalizedNames.Add(normalizedName);
            }
        }

        CustomFriendlyNpcNames = normalizedNames;
    }

    public static string NormalizeFriendlyNpcNameForCatalog(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        return name.Trim()
            .Replace("·", string.Empty, StringComparison.Ordinal)
            .Replace("・", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private void EnsureThemeBarColors()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(configured.ToVector4());
        }
    }

    private void MigrateThemeBarColorsToSkylineDefaults()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            var current = configured.ToVector4();
            if (JobThemePalette.TryGetLegacyDefaultColor(entry.JobName, out var legacy)
                && ColorsApproximatelyEqual(current, legacy))
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(current);
        }
    }

    private void MigrateThemeBarColorsToIkegamiDefaults()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            var current = configured.ToVector4();
            if (JobThemePalette.TryGetSkylineDefaultColor(entry.JobName, out var skyline)
                && ColorsApproximatelyEqual(current, skyline))
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(current);
        }
    }

    private void MigrateThemeBarColorsToIkegamiSoftDefaults()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            var current = configured.ToVector4();
            if (JobThemePalette.TryGetIkegamiOpaqueDefaultColor(entry.JobName, out var opaque)
                && ColorsApproximatelyEqual(current, opaque))
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(current);
        }
    }

    private void MigrateThemeBarColorsToIkegamiSofterDefaults()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            var current = configured.ToVector4();
            if (JobThemePalette.TryGetPreviousIkegamiSoftDefaultColor(entry.JobName, out var soft)
                && ColorsApproximatelyEqual(current, soft))
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(current);
        }
    }

    private void MigrateThemeBarColorsToFineTunedDefaults()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            var current = configured.ToVector4();
            if (JobThemePalette.TryGetPreviousTunedDefaultColor(entry.JobName, out var previous)
                && ColorsApproximatelyEqual(current, previous))
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(current);
        }
    }

    private void MigrateThemeBarColorsToAstDistinctDefaults()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            var current = configured.ToVector4();
            if (JobThemePalette.TryGetPreviousAstDefaultColor(entry.JobName, out var previousAst)
                && ColorsApproximatelyEqual(current, previousAst))
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(current);
        }
    }

    private void MigrateThemeBarColorsToSelectedHealerDefaults()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            var current = configured.ToVector4();
            if (JobThemePalette.TryGetPreviousHealerDefaultColor(entry.JobName, out var previousHealer)
                && ColorsApproximatelyEqual(current, previousHealer))
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(current);
        }
    }

    private static bool ColorsApproximatelyEqual(Vector4 left, Vector4 right, float epsilon = 0.001f)
        => Math.Abs(left.X - right.X) <= epsilon
           && Math.Abs(left.Y - right.Y) <= epsilon
           && Math.Abs(left.Z - right.Z) <= epsilon
           && Math.Abs(left.W - right.W) <= epsilon;

    private Vector4 ApplyThemeBarOpacity(Vector4 color)
        => new(color.X, color.Y, color.Z, ThemeBarOpacity);
}
