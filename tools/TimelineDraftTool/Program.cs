using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace TimelineDraftTool;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly TextBox logPathTextBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
    private readonly TextBox outputDirectoryTextBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
    private readonly TextBox resourceDirectoryTextBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
    private readonly TextBox promoteDraftTextBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
    private readonly TextBox timelineDataDirectoryTextBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
    private readonly Button browseLogButton = new() { Text = "选择日志" };
    private readonly Button latestLogButton = new() { Text = "使用最新日志" };
    private readonly Button browseOutputButton = new() { Text = "选择目录" };
    private readonly Button openOutputButton = new() { Text = "打开目录" };
    private readonly Button refreshResourcesButton = new() { Text = "刷新额外资源" };
    private readonly Button browsePromoteDraftButton = new() { Text = "选择草稿" };
    private readonly Button promoteDraftButton = new() { Text = "转正草稿" };
    private readonly Button browseTimelineDataButton = new() { Text = "选择目录" };
    private readonly Button refreshEncountersButton = new() { Text = "刷新战斗列表" };
    private readonly Button generateButton = new() { Text = "生成时间轴草稿" };
    private readonly DataGridView encounterGrid = new()
    {
        Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        MultiSelect = false,
        ReadOnly = true,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
    };
    private readonly TextBox statusTextBox = new()
    {
        Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
    };

    private List<ParsedEncounter> encounters = [];

    public MainForm()
    {
        Text = "DPS统计 时间轴草稿生成器";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 680);
        Size = new Size(1120, 760);

        var defaultActLogDirectory = @"D:\ff14act\FFXIVLogs";
        logPathTextBox.Text = ResolveLatestLog(defaultActLogDirectory) ?? string.Empty;
        outputDirectoryTextBox.Text = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        resourceDirectoryTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DalamudACT", "Timeline", "Resource");
        timelineDataDirectoryTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncherCN", "pluginConfigs", "DalamudACT", "Timeline", "Data");

        ConfigureGrid();
        LayoutControls();
        WireEvents(defaultActLogDirectory);
    }

    private void ConfigureGrid()
    {
        encounterGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "开始时间", DataPropertyName = nameof(EncounterRow.StartTime), FillWeight = 70 });
        encounterGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "时长", DataPropertyName = nameof(EncounterRow.Duration), FillWeight = 45 });
        encounterGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "副本", DataPropertyName = nameof(EncounterRow.ZoneName), FillWeight = 130 });
        encounterGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "主要Boss", DataPropertyName = nameof(EncounterRow.PrimarySourceName), FillWeight = 120 });
        encounterGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "条目", DataPropertyName = nameof(EncounterRow.EventCount), FillWeight = 40 });
        encounterGrid.AutoGenerateColumns = false;
    }

    private void LayoutControls()
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 186));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));

        main.Controls.Add(BuildPathPanel(), 0, 0);
        main.Controls.Add(BuildActionPanel(), 0, 1);
        main.Controls.Add(encounterGrid, 0, 2);
        main.Controls.Add(BuildGeneratePanel(), 0, 3);
        main.Controls.Add(statusTextBox, 0, 4);
        Controls.Add(main);
    }

    private Control BuildPathPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        panel.Controls.Add(new Label { Text = "ACT日志", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
        panel.Controls.Add(logPathTextBox, 1, 0);
        panel.Controls.Add(browseLogButton, 2, 0);
        panel.Controls.Add(latestLogButton, 3, 0);
        panel.Controls.Add(new Label { Text = "草稿目录", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 1);
        panel.Controls.Add(outputDirectoryTextBox, 1, 1);
        panel.Controls.Add(browseOutputButton, 2, 1);
        panel.Controls.Add(openOutputButton, 3, 1);
        panel.Controls.Add(new Label { Text = "资源目录", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 2);
        panel.Controls.Add(resourceDirectoryTextBox, 1, 2);
        panel.Controls.Add(refreshResourcesButton, 2, 2);
        panel.Controls.Add(new Label { Text = "转正草稿", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 3);
        panel.Controls.Add(promoteDraftTextBox, 1, 3);
        panel.Controls.Add(browsePromoteDraftButton, 2, 3);
        panel.Controls.Add(promoteDraftButton, 3, 3);
        panel.Controls.Add(new Label { Text = "时间轴目录", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 4);
        panel.Controls.Add(timelineDataDirectoryTextBox, 1, 4);
        panel.Controls.Add(browseTimelineDataButton, 2, 4);

        return panel;
    }

    private Control BuildActionPanel()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        panel.Controls.Add(refreshEncountersButton);
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Padding = new Padding(10, 7, 0, 0),
            Text = "选择一场战斗后生成；未选择时默认最新一场。",
        });
        return panel;
    }

    private Control BuildGeneratePanel()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        panel.Controls.Add(generateButton);
        return panel;
    }

    private void WireEvents(string defaultActLogDirectory)
    {
        browseLogButton.Click += (_, _) => BrowseLog();
        latestLogButton.Click += (_, _) =>
        {
            var latest = ResolveLatestLog(defaultActLogDirectory);
            if (latest == null)
                SetStatus("没有找到最新 ACT Network*.log。");
            else
                logPathTextBox.Text = latest;
        };
        browseOutputButton.Click += (_, _) => BrowseDirectory(outputDirectoryTextBox);
        openOutputButton.Click += (_, _) => OpenDirectory(outputDirectoryTextBox.Text);
        browsePromoteDraftButton.Click += (_, _) => BrowseDraftForPromotion();
        browseTimelineDataButton.Click += (_, _) => BrowseDirectory(timelineDataDirectoryTextBox);
        promoteDraftButton.Click += (_, _) => PromoteDraft();
        refreshResourcesButton.Click += (_, _) => RunSafely(() =>
        {
            var resources = new AeAssistResources(resourceDirectoryTextBox.Text.Trim());
            resources.RefreshNow();
            SetStatus("已刷新额外资源：AoeActions / TankDeathSentence。 ");
        });
        refreshEncountersButton.Click += (_, _) => RefreshEncounters();
        generateButton.Click += (_, _) => GenerateDraft();
    }

    private void BrowseLog()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "ACT Network 日志|Network*.log|日志文件|*.log|所有文件|*.*",
            Title = "选择 ACT Network 日志",
        };

        if (File.Exists(logPathTextBox.Text))
            dialog.InitialDirectory = Path.GetDirectoryName(logPathTextBox.Text);

        if (dialog.ShowDialog(this) == DialogResult.OK)
            logPathTextBox.Text = dialog.FileName;
    }

    private void BrowseDraftForPromotion()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "时间轴草稿|*.txt|所有文件|*.*",
            Title = "选择要转正的时间轴草稿",
        };

        var draftDirectory = outputDirectoryTextBox.Text.Trim();
        if (Directory.Exists(draftDirectory))
            dialog.InitialDirectory = draftDirectory;
        else if (File.Exists(promoteDraftTextBox.Text))
            dialog.InitialDirectory = Path.GetDirectoryName(promoteDraftTextBox.Text);

        if (dialog.ShowDialog(this) == DialogResult.OK)
            promoteDraftTextBox.Text = dialog.FileName;
    }

    private void BrowseDirectory(TextBox target)
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = Directory.Exists(target.Text) ? target.Text : string.Empty,
            UseDescriptionForTitle = true,
            Description = "选择目录",
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            target.Text = dialog.SelectedPath;
    }

    private void RefreshEncounters()
    {
        RunSafely(() =>
        {
            var logPath = logPathTextBox.Text.Trim();
            if (!File.Exists(logPath))
            {
                SetStatus("ACT 日志文件不存在。");
                return;
            }

            encounters = TimelineDraftGenerator.GetEncounters(logPath);
            encounterGrid.DataSource = encounters.Select(EncounterRow.FromEncounter).ToList();
            if (encounters.Count > 0)
                encounterGrid.Rows[0].Selected = true;

            SetStatus(encounters.Count == 0
                ? "日志中没有找到可生成草稿的战斗段。"
                : $"已找到 {encounters.Count} 场可用战斗。");
        });
    }

    private void GenerateDraft()
    {
        RunSafely(() =>
        {
            var logPath = logPathTextBox.Text.Trim();
            if (!File.Exists(logPath))
            {
                SetStatus("ACT 日志文件不存在。");
                return;
            }

            if (encounters.Count == 0)
                encounters = TimelineDraftGenerator.GetEncounters(logPath);

            if (encounters.Count == 0)
            {
                SetStatus("日志中没有找到可生成草稿的战斗段。");
                return;
            }

            var selectedIndex = encounterGrid.SelectedRows.Count > 0 ? encounterGrid.SelectedRows[0].Index : 0;
            if (selectedIndex < 0 || selectedIndex >= encounters.Count)
                selectedIndex = 0;

            var outputDirectory = outputDirectoryTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                SetStatus("草稿目录不能为空。");
                return;
            }

            Directory.CreateDirectory(outputDirectory);
            var resources = new AeAssistResources(resourceDirectoryTextBox.Text.Trim());
            resources.LoadLocal();
            var outputPath = TimelineDraftGenerator.GenerateDraft(logPath, encounters[selectedIndex], outputDirectory, resources);
            SetStatus($"已生成时间轴草稿：{outputPath}");
        });
    }

    private void PromoteDraft()
    {
        RunSafely(() =>
        {
            var draftPath = promoteDraftTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(draftPath))
                draftPath = FindLatestDraft(outputDirectoryTextBox.Text.Trim()) ?? string.Empty;

            if (!File.Exists(draftPath))
            {
                SetStatus("请选择要转正的时间轴草稿。");
                return;
            }

            var timelineDataDirectory = timelineDataDirectoryTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(timelineDataDirectory))
            {
                SetStatus("时间轴目录不能为空。");
                return;
            }

            var result = TimelineDraftPromoter.Promote(draftPath, timelineDataDirectory);
            SetStatus(result);
        });
    }

    private void RunSafely(Action action)
    {
        try
        {
            UseWaitCursor = true;
            Enabled = false;
            action();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
        finally
        {
            Enabled = true;
            UseWaitCursor = false;
        }
    }

    private void SetStatus(string message)
        => statusTextBox.Text = $"{DateTime.Now:HH:mm:ss}  {message}";

    private static string? ResolveLatestLog(string directory)
        => Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "Network*.log", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName
            : null;

    private static void OpenDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true,
            Verb = "open",
        });
    }

    private static string? FindLatestDraft(string directory)
        => Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.txt", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName
            : null;
}

internal sealed record EncounterRow(string StartTime, string Duration, string ZoneName, string PrimarySourceName, int EventCount)
{
    public static EncounterRow FromEncounter(ParsedEncounter encounter)
        => new(
            encounter.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture),
            FormatDuration(encounter.Duration),
            encounter.ZoneName,
            encounter.PrimarySourceName,
            encounter.Events.Count);

    private static string FormatDuration(TimeSpan duration)
        => duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
}

internal static class TimelineDraftPromoter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Promote(string draftPath, string timelineDataDirectory)
    {
        var metadata = DraftMetadata.Read(draftPath);
        Directory.CreateDirectory(timelineDataDirectory);

        var generatedDirectory = Path.Combine(timelineDataDirectory, "generated");
        Directory.CreateDirectory(generatedDirectory);

        var baseName = BuildBaseName(metadata, draftPath);
        var targetFileName = $"{baseName}.cn.txt";
        var targetPath = Path.Combine(generatedDirectory, targetFileName);
        File.Copy(draftPath, targetPath, overwrite: true);

        var relativeFile = $"generated/{targetFileName}";
        var indexPath = Path.Combine(timelineDataDirectory, "timeline-index.json");
        var entries = ReadIndex(indexPath);
        var id = $"generated-{baseName}";
        var existingIndex = entries.FindIndex(entry => entry.ZoneId == metadata.ZoneId || string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase));
        var newEntry = new TimelineIndexEntry(id, metadata.ZoneName, metadata.ZoneId, relativeFile);

        if (existingIndex >= 0)
            entries[existingIndex] = newEntry;
        else
            entries.Add(newEntry);

        entries = entries
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();

        File.WriteAllText(indexPath, JsonSerializer.Serialize(entries, JsonOptions), new UTF8Encoding(false));
        return $"已转正草稿：{targetPath}，并更新索引：{indexPath}";
    }

    private static List<TimelineIndexEntry> ReadIndex(string indexPath)
    {
        if (!File.Exists(indexPath))
            return [];

        var entries = JsonSerializer.Deserialize<List<TimelineIndexEntry>>(File.ReadAllText(indexPath), JsonOptions);
        return entries ?? [];
    }

    private static string BuildBaseName(DraftMetadata metadata, string draftPath)
    {
        var zone = Slugify(metadata.ZoneName);
        if (string.IsNullOrWhiteSpace(zone))
            zone = Path.GetFileNameWithoutExtension(draftPath);

        return $"{metadata.ZoneId:x}-{zone}";
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value.ToLowerInvariant())
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                builder.Append(ch);
            else if (IsCjk(ch))
                builder.Append(ch);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }

        return builder.ToString().Trim('-');
    }

    private static bool IsCjk(char ch)
        => ch is >= '\u4e00' and <= '\u9fff';
}

internal sealed record TimelineIndexEntry(string Id, string Name, uint? ZoneId, string File);

internal sealed record DraftMetadata(uint ZoneId, string ZoneName)
{
    public static DraftMetadata Read(string draftPath)
    {
        uint? zoneId = null;
        string? zoneName = null;

        foreach (var line in File.ReadLines(draftPath, Encoding.UTF8).Take(80))
        {
            if (line.StartsWith("# 自动生成：", StringComparison.Ordinal))
            {
                zoneName = line[7..].Trim();
                continue;
            }

            if (line.StartsWith("# ZoneId:", StringComparison.Ordinal))
            {
                var raw = line[9..].Trim();
                if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decimalZoneId))
                    zoneId = decimalZoneId;
                else if (uint.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexZoneId))
                    zoneId = hexZoneId;
            }
        }

        if (zoneId == null)
            throw new InvalidOperationException("草稿缺少 # ZoneId，无法转正。");

        if (string.IsNullOrWhiteSpace(zoneName))
            zoneName = Path.GetFileNameWithoutExtension(draftPath);

        return new DraftMetadata(zoneId.Value, zoneName);
    }
}

internal static class TimelineDraftGenerator
{
    public static List<ParsedEncounter> GetEncounters(string logPath)
    {
        var zones = ParseLog(logPath);
        return zones.SelectMany(SplitEncounters)
            .OrderByDescending(encounter => encounter.StartTime)
            .ToList();
    }

    public static string GenerateDraft(string logPath, ParsedEncounter encounter, string outputDirectory, AeAssistResources resources)
    {
        var fileName = $"{encounter.ZoneId:X}-{SanitizeFileName(encounter.ZoneName)}-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
        var outputPath = Path.Combine(outputDirectory, fileName);
        File.WriteAllText(outputPath, BuildTimelineText(encounter, Path.GetFileName(logPath), resources), new UTF8Encoding(false));
        return outputPath;
    }

    private static List<ParsedZone> ParseLog(string logPath)
    {
        ParsedZone current = new(0, "Unknown", [], [], [], []);
        var zones = new List<ParsedZone>();

        using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split('|');
            if (parts.Length < 3)
                continue;

            if (!DateTimeOffset.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
                continue;

            switch (parts[0])
            {
                case "01":
                    if (parts.Length >= 4 && uint.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var zoneId))
                    {
                        AddZoneIfUseful(zones, current);
                        current = new ParsedZone(zoneId, parts[3], [], [], [], []);
                    }
                    break;
                case "260":
                    TryAddCombatState(current, parts, timestamp);
                    break;
                case "03":
                    TryAddHostileNpc(current, parts);
                    break;
                case "20":
                    TryAddStartsUsingHint(current, parts, timestamp);
                    break;
                case "21":
                    TryAddAbility(current, parts, timestamp);
                    break;
            }
        }

        AddZoneIfUseful(zones, current);
        return zones;
    }

    private static void AddZoneIfUseful(List<ParsedZone> zones, ParsedZone candidate)
    {
        var filteredEvents = FilterEvents(candidate.Events);
        if (filteredEvents.Count > 0)
            zones.Add(candidate with { Events = filteredEvents });
    }

    private static List<ParsedEncounter> SplitEncounters(ParsedZone zone)
    {
        List<ParsedEncounter> encounters = [];
        List<DraftEvent> currentEvents = [];
        DateTimeOffset? lastTimestamp = null;

        foreach (var ev in zone.Events.OrderBy(item => item.Timestamp))
        {
            if (lastTimestamp.HasValue && (ev.Timestamp - lastTimestamp.Value).TotalSeconds > 45 && currentEvents.Count > 0)
            {
                encounters.Add(new ParsedEncounter(zone.ZoneId, zone.ZoneName, currentEvents, ResolveCombatStartTime(zone, currentEvents), zone.StartsUsingHints));
                currentEvents = [];
            }

            currentEvents.Add(ev);
            lastTimestamp = ev.Timestamp;
        }

        if (currentEvents.Count > 0)
            encounters.Add(new ParsedEncounter(zone.ZoneId, zone.ZoneName, currentEvents, ResolveCombatStartTime(zone, currentEvents), zone.StartsUsingHints));

        return encounters;
    }

    private static DateTimeOffset? ResolveCombatStartTime(ParsedZone zone, List<DraftEvent> events)
    {
        if (events.Count == 0)
            return null;

        var firstEvent = events.Min(static ev => ev.Timestamp);
        return zone.CombatStartTimes
            .Where(time => time <= firstEvent && (firstEvent - time).TotalMinutes <= 20)
            .OrderByDescending(static time => time)
            .Select(time => (DateTimeOffset?)time)
            .FirstOrDefault();
    }

    private static void TryAddStartsUsingHint(ParsedZone parsed, string[] parts, DateTimeOffset timestamp)
    {
        if (parts.Length < 7 || !IsLikelyNpcId(parts[2]) || !parsed.HostileNpcIds.Contains(parts[2]) || !IsUsefulActionId(parts[4]))
            return;

        parsed.StartsUsingHints.Add(new StartsUsingHint(timestamp, parts[4], parts[5], parts[3]));
    }

    private static void TryAddAbility(ParsedZone parsed, string[] parts, DateTimeOffset timestamp)
    {
        if (parts.Length < 7 || !IsLikelyNpcId(parts[2]) || !parsed.HostileNpcIds.Contains(parts[2]) || !IsUsefulActionId(parts[4]))
            return;

        parsed.Events.Add(new DraftEvent(timestamp, "Ability", parts[4], parts[5], parts[3]));
    }

    private static void TryAddHostileNpc(ParsedZone parsed, string[] parts)
    {
        if (parts.Length < 8 || !IsLikelyNpcId(parts[2]))
            return;

        var classJob = parts[4];
        var ownerId = parts[6];
        if (classJob == "00" && (ownerId == "0000" || ownerId == "00000000"))
            parsed.HostileNpcIds.Add(parts[2]);
    }

    private static void TryAddCombatState(ParsedZone parsed, string[] parts, DateTimeOffset timestamp)
    {
        if (parts.Length >= 6 && parts[2] == "1" && parts[3] == "1")
            parsed.CombatStartTimes.Add(timestamp);
    }

    private static List<DraftEvent> FilterEvents(List<DraftEvent> events)
    {
        List<DraftEvent> result = [];
        Dictionary<string, DateTimeOffset> lastSeen = [];

        foreach (var ev in events.OrderBy(item => item.Timestamp))
        {
            var key = $"{ev.Kind}|{ev.ActionId}|{ev.SourceName}";
            if (lastSeen.TryGetValue(key, out var previous) && (ev.Timestamp - previous).TotalSeconds < 2.5)
                continue;

            lastSeen[key] = ev.Timestamp;
            result.Add(ev);
            if (result.Count >= 500)
                break;
        }

        return result;
    }

    private static string BuildTimelineText(ParsedEncounter encounter, string sourceLogName, AeAssistResources resources)
    {
        var firstTimestamp = encounter.CombatStartTime ?? encounter.Events[0].Timestamp;
        var builder = new StringBuilder();
        builder.AppendLine($"# 自动生成：{encounter.ZoneName}");
        builder.AppendLine($"# ZoneId: {encounter.ZoneId}");
        builder.AppendLine($"# Source: {sourceLogName}");
        builder.AppendLine($"# GeneratedAt: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine("# 这是 ACT 网络日志生成的草稿，请人工删除小怪/玩家/NPC杂项并校正分段同步点。");
        builder.AppendLine();
        builder.AppendLine("hideall \"--Reset--\"");
        builder.AppendLine("hideall \"--sync--\"");
        builder.AppendLine();
        builder.AppendLine("0.0 \"--sync--\" InCombat { inGameCombat: \"1\" } window 0,1");

        var startsUsingHints = BuildStartsUsingHintMap(encounter.StartsUsingHints, resources);
        foreach (var group in MergeDuplicateEvents(encounter.Events, resources, startsUsingHints, encounter.PrimarySourceName))
        {
            var seconds = Math.Max(0, (group.Timestamp - firstTimestamp).TotalSeconds);
            var hintSuffix = string.IsNullOrWhiteSpace(group.Hint) ? string.Empty : $" # {group.Hint}";
            builder.AppendLine(FormattableString.Invariant($"{seconds:0.0} \"{Escape(group.ActionName)}\" {group.Kind} {{ id: {FormatActionIds(group.ActionIds)}, source: \"{Escape(group.SourceName)}\" }}{hintSuffix}"));
        }

        return builder.ToString();
    }

    private static List<MergedDraftEvent> MergeDuplicateEvents(List<DraftEvent> events, AeAssistResources resources, List<ResolvedStartsUsingHint> startsUsingHints, string primarySourceName)
    {
        List<MergedDraftEvent> result = [];

        foreach (var ev in events.OrderBy(item => item.Timestamp))
        {
            var hint = GetAeAssistHint(ev.ActionId, resources) ?? GetRecentStartsUsingHint(ev, startsUsingHints);
            if (IsUnknownActionName(ev.ActionName) && string.IsNullOrWhiteSpace(hint))
                continue;

            var existingIndex = result.FindIndex(item => IsMergeCandidate(item, ev));
            if (existingIndex >= 0)
            {
                var existing = result[existingIndex];
                if (!existing.ActionIds.Contains(ev.ActionId, StringComparer.OrdinalIgnoreCase))
                    existing.ActionIds.Add(ev.ActionId);

                var sourceName = ChooseMergedSource(existing.SourceName, ev.SourceName, primarySourceName);
                var mergedHint = string.IsNullOrWhiteSpace(existing.Hint) ? hint : existing.Hint;
                if (!string.Equals(sourceName, existing.SourceName, StringComparison.Ordinal) || !string.Equals(mergedHint, existing.Hint, StringComparison.Ordinal))
                    result[existingIndex] = existing with { SourceName = sourceName, Hint = mergedHint };
                continue;
            }

            result.Add(new MergedDraftEvent(ev.Timestamp, ev.Kind, ev.ActionName, ev.SourceName, [ev.ActionId], hint));
        }

        return result;
    }

    private static bool IsMergeCandidate(MergedDraftEvent existing, DraftEvent candidate)
        => existing.Kind == candidate.Kind
           && string.Equals(existing.ActionName, candidate.ActionName, StringComparison.Ordinal)
           && Math.Abs((existing.Timestamp - candidate.Timestamp).TotalSeconds) <= GetMergeWindowSeconds(candidate.Kind);

    private static double GetMergeWindowSeconds(string kind)
        => kind == "Ability" ? 1.0 : 0.25;

    private static string ChooseMergedSource(string existingSource, string candidateSource, string primarySourceName)
    {
        if (!string.IsNullOrWhiteSpace(primarySourceName))
        {
            if (string.Equals(candidateSource, primarySourceName, StringComparison.Ordinal))
                return candidateSource;

            if (string.Equals(existingSource, primarySourceName, StringComparison.Ordinal))
                return existingSource;
        }

        return existingSource;
    }

    private static string FormatActionIds(IReadOnlyList<string> actionIds)
    {
        if (actionIds.Count == 1)
            return $"\"{actionIds[0].ToUpperInvariant()}\"";

        return "[" + string.Join(", ", actionIds.Select(id => $"\"{id.ToUpperInvariant()}\"")) + "]";
    }

    private static bool IsUnknownActionName(string actionName)
        => actionName.StartsWith("unknown_", StringComparison.OrdinalIgnoreCase)
           || string.IsNullOrWhiteSpace(actionName);

    private static string? GetAeAssistHint(string actionId, AeAssistResources resources)
        => uint.TryParse(actionId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsedActionId)
            ? resources.GetHint(parsedActionId)
            : null;

    private static List<ResolvedStartsUsingHint> BuildStartsUsingHintMap(List<StartsUsingHint> startsUsingHints, AeAssistResources resources)
    {
        List<ResolvedStartsUsingHint> result = [];
        foreach (var hint in startsUsingHints.OrderBy(static item => item.Timestamp))
        {
            var mechanicHint = GetAeAssistHint(hint.ActionId, resources);
            if (!string.IsNullOrWhiteSpace(mechanicHint))
                result.Add(new ResolvedStartsUsingHint(hint.Timestamp, hint.ActionName, hint.SourceName, mechanicHint));
        }

        return result;
    }

    private static string? GetRecentStartsUsingHint(DraftEvent ability, List<ResolvedStartsUsingHint> startsUsingHints)
        => startsUsingHints.LastOrDefault(hint => string.Equals(hint.ActionName, ability.ActionName, StringComparison.Ordinal)
                                                 && string.Equals(hint.SourceName, ability.SourceName, StringComparison.Ordinal)
                                                 && (ability.Timestamp - hint.Timestamp).TotalSeconds is >= 0 and <= 20)?.Hint;

    private static bool IsLikelyNpcId(string id)
        => id.StartsWith("4", StringComparison.OrdinalIgnoreCase)
           || id.StartsWith("8", StringComparison.OrdinalIgnoreCase);

    private static bool IsUsefulActionId(string actionId)
        => !string.IsNullOrWhiteSpace(actionId)
           && actionId != "0"
           && actionId != "0000"
           && !string.Equals(actionId, "07", StringComparison.OrdinalIgnoreCase);

    private static string Escape(string value)
        => (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string((value ?? "Unknown").Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "Unknown" : safe;
    }
}

internal sealed class AeAssistResources(string directory)
{
    private const string ResourceBaseUrl = "https://raw.githubusercontent.com/aeassist-acr/Resource/main";
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };
    private readonly HashSet<uint> aoeActions = [];
    private readonly HashSet<uint> tankDeathSentence = [];

    public string? GetHint(uint actionId)
    {
        if (tankDeathSentence.Contains(actionId))
            return "死刑";

        if (aoeActions.Contains(actionId))
            return "AOE";

        return null;
    }

    public void LoadLocal()
    {
        LoadLocal("AoeActions", aoeActions);
        LoadLocal("TankDeathSentence", tankDeathSentence);
    }

    public void RefreshNow()
    {
        Directory.CreateDirectory(directory);
        DownloadResource("AoeActions", aoeActions);
        DownloadResource("TankDeathSentence", tankDeathSentence);
    }

    private void DownloadResource(string name, HashSet<uint> target)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        var json = client.GetStringAsync($"{ResourceBaseUrl}/{name}.json").GetAwaiter().GetResult();
        var values = JsonSerializer.Deserialize<HashSet<uint>>(json, JsonOptions);
        if (values == null)
            return;

        File.WriteAllText(GetCachePath(name), json, new UTF8Encoding(false));
        target.Clear();
        foreach (var value in values)
            target.Add(value);
    }

    private void LoadLocal(string name, HashSet<uint> target)
    {
        var path = GetCachePath(name);
        if (!File.Exists(path))
            return;

        var values = JsonSerializer.Deserialize<HashSet<uint>>(File.ReadAllText(path), JsonOptions);
        if (values == null)
            return;

        target.Clear();
        foreach (var value in values)
            target.Add(value);
    }

    private string GetCachePath(string name)
        => Path.Combine(directory, $"{name}.json");
}

internal sealed record ParsedZone(
    uint ZoneId,
    string ZoneName,
    List<DraftEvent> Events,
    HashSet<string> HostileNpcIds,
    List<DateTimeOffset> CombatStartTimes,
    List<StartsUsingHint> StartsUsingHints);

internal sealed record ParsedEncounter(uint ZoneId, string ZoneName, List<DraftEvent> Events, DateTimeOffset? CombatStartTime, List<StartsUsingHint> StartsUsingHints)
{
    public DateTimeOffset StartTime => Events.Count == 0 ? DateTimeOffset.MinValue : Events.Min(static ev => ev.Timestamp);

    public DateTimeOffset EndTime => Events.Count == 0 ? DateTimeOffset.MinValue : Events.Max(static ev => ev.Timestamp);

    public TimeSpan Duration => EndTime >= StartTime ? EndTime - StartTime : TimeSpan.Zero;

    public string PrimarySourceName => Events
        .GroupBy(static ev => ev.SourceName)
        .OrderByDescending(static group => group.Count())
        .ThenBy(static group => group.Key, StringComparer.Ordinal)
        .FirstOrDefault()?.Key ?? "Unknown";
}

internal sealed record DraftEvent(DateTimeOffset Timestamp, string Kind, string ActionId, string ActionName, string SourceName);

internal sealed record StartsUsingHint(DateTimeOffset Timestamp, string ActionId, string ActionName, string SourceName);

internal sealed record ResolvedStartsUsingHint(DateTimeOffset Timestamp, string ActionName, string SourceName, string Hint);

internal sealed record MergedDraftEvent(DateTimeOffset Timestamp, string Kind, string ActionName, string SourceName, List<string> ActionIds, string? Hint);
