using System.Globalization;
using DashyNMS.Mobile.DeviceSections;
using DesktopNMS.Core.Graylog;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Graylog;

/// <summary>
/// One Graylog message: LibreNMS's columns (level, time, source, message,
/// facility) as a list row, and every field on its own page (#117) -
/// desktop's details pane, for a screen with no room beside the list.
/// </summary>
public sealed class GraylogMessageItem
{
    public GraylogMessageItem(GraylogMessage message, TimeZoneInfo? zone, Func<string?, (string Name, int? DeviceId)> device, DateTimeOffset? now = null)
    {
        Id = message.Id;
        Level = message.Level;
        LevelText = GraylogQuery.LevelText(message.Level);

        if (message.Timestamp is { } timestamp)
        {
            var shown = InZone(timestamp, zone);
            TimeText = shown.ToString("d MMM yyyy HH:mm:ss", CultureInfo.CurrentCulture);

            // Today's by the second, as a log reads; older ones by the day.
            var today = InZone(now ?? DateTimeOffset.UtcNow, zone).Date;
            ShortTimeText = shown.Date == today
                ? shown.ToString("HH:mm:ss", CultureInfo.CurrentCulture)
                : shown.ToString("d MMM", CultureInfo.CurrentCulture);
        }

        var source = device(message.Source);
        var origin = device(message.RemoteIp);
        SourceText = source.Name;
        OriginText = origin.Name;

        // The device the message is about: its source, or failing that the
        // address it arrived from - as desktop decides.
        DeviceId = source.DeviceId ?? origin.DeviceId;
        DeviceName = source.DeviceId is not null ? source.Name : origin.DeviceId is not null ? origin.Name : null;
        SourceAddress = message.Source?.Trim() is { Length: > 0 } address ? address : null;

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

    public bool HasLevel => LevelText.Length > 0;

    /// <summary>"3 Oct 2026 14:02:11", for the message's own page.</summary>
    public string? TimeText { get; }

    /// <summary>"14:02:11" today, "2 Oct" before - the row's right-hand column.</summary>
    public string? ShortTimeText { get; }

    /// <summary>The source's device name when LibreNMS knows the address, otherwise the address.</summary>
    public string SourceText { get; }

    public string OriginText { get; }

    /// <summary>"from 10.20.0.1 · local7" - where it arrived from, when that isn't the source, and the facility.</summary>
    public string OriginLine => string.Join(" · ", new[]
    {
        OriginText.Length > 0 && !string.Equals(OriginText, SourceText, StringComparison.OrdinalIgnoreCase) ? "from " + OriginText : null,
        FacilityText,
    }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public bool HasOriginLine => OriginLine.Length > 0;

    /// <summary>The LibreNMS device this came from, when its source or origin matches one.</summary>
    public int? DeviceId { get; }

    public bool HasDevice => DeviceId is not null;

    /// <summary>That device's name, as the app shows it.</summary>
    public string? DeviceName { get; }

    /// <summary>The message's source field as sent, for a sender LibreNMS doesn't know.</summary>
    public string? SourceAddress { get; }

    /// <summary>The Device chip's setting for "only this one's messages"; null with no source to go on.</summary>
    public GraylogDeviceFilter? Filter => DeviceId is { } id
        ? GraylogDeviceFilter.ForDevice(id, DeviceName ?? SourceText)
        : SourceAddress is { } address ? GraylogDeviceFilter.ForAddress(address) : null;

    /// <summary>On one line in the list; <see cref="FullText"/> keeps line breaks for its own page.</summary>
    public string MessageText { get; }

    public string FullText { get; }

    public string FacilityText { get; }

    /// <summary>"(4) Warning · local7" - the row's last line.</summary>
    public string MetaText => string.Join(" · ", new[] { LevelText, FacilityText }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>Every field Graylog has for the message, by name.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Fields { get; }

    private static DateTimeOffset InZone(DateTimeOffset time, TimeZoneInfo? zone) =>
        zone is null ? time.ToLocalTime() : TimeZoneInfo.ConvertTime(time, zone);
}
