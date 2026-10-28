using System.Globalization;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Logs;

/// <summary>
/// One event log or alert log entry: a list row on the Logs page (#118) - a
/// dot, the device, the message on one line, its type, a short age - and
/// everything about it on its own page.
/// </summary>
public sealed class LogEntryItem
{
    private LogEntryItem(int id, int deviceId, string deviceName, DateTime? at, DateTime now)
    {
        Id = id;
        DeviceId = deviceId;
        DeviceName = deviceName;
        LocalTime = at;
        TimeText = at?.ToString("d MMM yyyy, HH:mm:ss", CultureInfo.CurrentCulture);
        ShortAge = at is { } time ? Short(now - time) : string.Empty;
        AgeText = at is { } when ? Formatting.Age(Positive(now - when)) : string.Empty;
    }

    /// <summary>An event log entry, newest first in the list.</summary>
    public static LogEntryItem ForEvent(EventLogEntry entry, string deviceName, bool serverUtc, DateTime now)
    {
        var status = DeviceSectionLoader.EventStatus(entry.Severity);
        return new LogEntryItem(entry.Id, entry.DeviceId, deviceName, ServerTime.ToLocal(entry.Timestamp, serverUtc), now)
        {
            Event = entry,
            Status = status,
            Message = (entry.Message ?? "(no message)").ReplaceLineEndings(" "),
            FullText = entry.Message ?? "(no message)",
            Type = entry.Type?.Trim() is { Length: > 0 } type ? type : null,
            StatusText = SeverityText(entry.Severity),
            MetaText = string.Join(" · ", new[] { entry.Type, entry.Username }.Where(s => !string.IsNullOrWhiteSpace(s))),
        };
    }

    /// <summary>An alert log entry: its rule as the message, its state as the type.</summary>
    public static LogEntryItem ForAlert(AlertLogEntry entry, string deviceName, string ruleName, bool serverUtc, DateTime now)
    {
        var state = entry.State.ToDisplayString();
        return new LogEntryItem(entry.Id, entry.DeviceId, deviceName, ServerTime.ToLocal(entry.TimeLogged, serverUtc), now)
        {
            Alert = entry,
            Status = AlertStatus(entry.State),
            Message = ruleName,
            FullText = ruleName,
            RuleId = entry.RuleId,
            StatusText = state,
            MetaText = string.Create(CultureInfo.InvariantCulture, $"{state} · alert #{entry.Id}"),
        };
    }

    /// <summary>The event log's entry, or null for an alert log entry.</summary>
    public EventLogEntry? Event { get; private init; }

    /// <summary>The alert log's entry, or null for an event log entry.</summary>
    public AlertLogEntry? Alert { get; private init; }

    public bool IsAlert => Alert is not null;

    public int Id { get; }

    /// <summary>LibreNMS's device, or 0 for an entry about LibreNMS itself.</summary>
    public int DeviceId { get; }

    public bool HasDevice => DeviceId > 0;

    public string DeviceName { get; }

    public RowStatus Status { get; private init; }

    /// <summary>"Warning", or an alert's state - the entry page's pill.</summary>
    public string StatusText { get; private init; } = string.Empty;

    /// <summary>On one line in the list; <see cref="FullText"/> keeps line breaks for its own page.</summary>
    public string Message { get; private init; } = string.Empty;

    public string FullText { get; private init; } = string.Empty;

    /// <summary>The event's type ("interface"), as LibreNMS wrote it; null for alerts.</summary>
    public string? Type { get; private init; }

    /// <summary>The alert's rule; 0 for events.</summary>
    public int RuleId { get; private init; }

    /// <summary>"interface · admin", or "Recovered · alert #4790" - the row's last line.</summary>
    public string MetaText { get; private init; } = string.Empty;

    public bool HasMeta => MetaText.Length > 0;

    public DateTime? LocalTime { get; }

    /// <summary>"4 Oct 2026, 09:31:12", for the entry's own page.</summary>
    public string? TimeText { get; }

    /// <summary>"23m ago".</summary>
    public string AgeText { get; }

    /// <summary>"23m", "now" - the row's right-hand column, as the other lists (#69).</summary>
    public string ShortAge { get; }

    /// <summary>"4 Oct 2026, 09:31:12 · 23m ago".</summary>
    public string WhenText => string.Join(" · ", new[] { TimeText, AgeText }.Where(s => !string.IsNullOrEmpty(s)));

    /// <summary>The entry page's details, by name.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Fields => Event is { } e
        ? Pairs(
            ("Type", e.Type),
            ("Severity", StatusText),
            ("Logged", TimeText),
            ("By", e.Username),
            ("Event", "#" + e.Id.ToString(CultureInfo.InvariantCulture)))
        : Pairs(
            ("State", StatusText),
            ("Rule", Message),
            ("Logged", TimeText),
            ("Alert", "#" + Id.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Whether <paramref name="term"/> is in the device, message or type - the alert log's search, the event log's being Core's.</summary>
    public bool Matches(string term) =>
        DeviceName.Contains(term, StringComparison.CurrentCultureIgnoreCase)
        || Message.Contains(term, StringComparison.CurrentCultureIgnoreCase)
        || MetaText.Contains(term, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>LibreNMS's event severities: 1 ok, 2 info, 3 notice, 4 warning, 5 critical.</summary>
    internal static string SeverityText(int? severity) => severity switch
    {
        1 => "OK",
        2 => "Info",
        3 => "Notice",
        4 => "Warning",
        5 => "Critical",
        _ => "Event",
    };

    internal static RowStatus AlertStatus(AlertState state) => state switch
    {
        AlertState.Recovered or AlertState.Better => RowStatus.Ok,
        AlertState.Acknowledged => RowStatus.Inactive,
        AlertState.Worse => RowStatus.Warning,
        _ => RowStatus.Critical,
    };

    private static string Short(TimeSpan age) => Formatting.Age(Positive(age)) switch
    {
        "just now" => "now",
        var text => text.Replace(" ago", string.Empty, StringComparison.Ordinal),
    };

    /// <summary>A server clock a little ahead of the phone's would otherwise give "-2m".</summary>
    private static TimeSpan Positive(TimeSpan age) => age < TimeSpan.Zero ? TimeSpan.Zero : age;

    private static IReadOnlyList<KeyValuePair<string, string>> Pairs(params (string Key, string? Value)[] pairs) => pairs
        .Where(p => !string.IsNullOrWhiteSpace(p.Value))
        .Select(p => new KeyValuePair<string, string>(p.Key, p.Value!))
        .ToList();
}
