using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DashyNMS.Mobile.DeviceSections;
using DesktopNMS.Core.Graylog;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Graylog;

/// <summary>
/// One Graylog message: LibreNMS's columns (level, time, source, message,
/// facility) folded into a card, which opens out to every field on a tap -
/// desktop's details pane, for a screen with no room beside the list.
/// </summary>
public sealed partial class GraylogMessageItem : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    public GraylogMessageItem(GraylogMessage message, TimeZoneInfo? zone, Func<string?, (string Name, int? DeviceId)> device)
    {
        Id = message.Id;
        Level = message.Level;
        LevelText = GraylogQuery.LevelText(message.Level);

        if (message.Timestamp is { } timestamp)
        {
            var shown = zone is null ? timestamp.ToLocalTime() : TimeZoneInfo.ConvertTime(timestamp, zone);
            TimeText = shown.ToString("d MMM HH:mm:ss", CultureInfo.CurrentCulture);
        }

        var source = device(message.Source);
        var origin = device(message.RemoteIp);
        SourceText = source.Name;
        OriginText = origin.Name;

        // The device the message is about: its source, or failing that the
        // address it arrived from - as desktop decides.
        DeviceId = source.DeviceId ?? origin.DeviceId;

        MessageText = (message.Text ?? string.Empty).ReplaceLineEndings(" ");
        FullText = message.FullMessage ?? message.Text ?? string.Empty;
        FacilityText = GraylogQuery.FacilityText(message.Facility);

        Fields = message.Fields
            .OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
            .Select(f => new KeyValuePair<string, string>(f.Key, message.GetText(f.Key) ?? string.Empty))
            .ToList();
    }

    public string? Id { get; }

    public int? Level { get; }

    /// <summary>LibreNMS's colour bands: 0-3 danger, 4 warning, 7 muted, the rest (and no level) informational.</summary>
    public RowStatus Status => Level switch
    {
        >= 0 and <= 3 => RowStatus.Critical,
        4 => RowStatus.Warning,
        7 => RowStatus.Inactive,
        _ => RowStatus.None,
    };

    public string LevelText { get; }

    public string? TimeText { get; }

    /// <summary>The source's device name when LibreNMS knows the address, otherwise the address.</summary>
    public string SourceText { get; }

    public string OriginText { get; }

    /// <summary>The LibreNMS device this came from, when its source or origin matches one.</summary>
    public int? DeviceId { get; }

    public bool HasDevice => DeviceId is not null;

    /// <summary>On one line in the list; <see cref="FullText"/> keeps line breaks for when it's opened.</summary>
    public string MessageText { get; }

    public string FullText { get; }

    public string FacilityText { get; }

    /// <summary>"core-sw-01 · (4) Warning" - the card's second line.</summary>
    public string Subtitle => string.Join(" · ", new[] { SourceText, LevelText }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>Every field Graylog has for the message, by name.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Fields { get; }
}
