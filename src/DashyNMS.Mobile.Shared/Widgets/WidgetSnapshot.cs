using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Widgets;

/// <summary>One alert as a widget row: enough to draw it and to open it.</summary>
public sealed record WidgetAlert(
    [property: JsonPropertyName("alertId")] int AlertId,
    [property: JsonPropertyName("deviceId")] int DeviceId,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("device")] string Device);

/// <summary>
/// What a home-screen widget shows, written after every alert check. The
/// widget draws from this alone - it never talks to LibreNMS, and holds no
/// token - so it keeps working, showing its last state, while the app sleeps.
/// </summary>
/// <remarks>
/// On iOS a Swift WidgetKit extension reads it from the shared App Group
/// folder, so the JSON names are a contract with
/// <c>ios-widget/DashyNMSWidget/Snapshot.swift</c>: change both together.
/// </remarks>
public sealed record WidgetSnapshot
{
    /// <summary>How many alerts a widget can list.</summary>
    public const int MaxAlerts = 3;

    [JsonPropertyName("signedIn")]
    public bool SignedIn { get; init; }

    /// <summary>Active (unacknowledged) critical alerts.</summary>
    [JsonPropertyName("critical")]
    public int Critical { get; init; }

    [JsonPropertyName("warning")]
    public int Warning { get; init; }

    [JsonPropertyName("acknowledged")]
    public int Acknowledged { get; init; }

    /// <summary>Null until the device list has been read.</summary>
    [JsonPropertyName("devicesDown")]
    public int? DevicesDown { get; init; }

    /// <summary>The alerts most needing attention - desktop's order: critical first, newest first.</summary>
    [JsonPropertyName("alerts")]
    public IReadOnlyList<WidgetAlert> Alerts { get; init; } = [];

    /// <summary>When the check ran, as Unix seconds (simple for Swift's decoder).</summary>
    [JsonPropertyName("checkedAt")]
    public long CheckedAt { get; init; }

    /// <summary>What a widget shows before the first check, and after signing out.</summary>
    public static WidgetSnapshot SignedOut { get; } = new();

    [JsonIgnore]
    public int Active => Critical + Warning;

    public static WidgetSnapshot Build(IEnumerable<Alert> alerts, int? devicesDown, DateTimeOffset checkedAt)
    {
        var open = alerts.Where(a => a.State is AlertState.Active or AlertState.Acknowledged).ToList();
        var active = open.Where(a => a.State == AlertState.Active).ToList();

        return new WidgetSnapshot
        {
            SignedIn = true,
            Critical = active.Count(a => a.Severity == AlertSeverity.Critical),
            Warning = active.Count(a => a.Severity == AlertSeverity.Warning),
            Acknowledged = open.Count - active.Count,
            DevicesDown = devicesDown,
            Alerts = active
                .OrderByDescending(a => a.Severity.SortRank())
                .ThenByDescending(a => a.Timestamp)
                .Take(MaxAlerts)
                .Select(a => new WidgetAlert(a.Id, a.DeviceId, SeverityKey(a.Severity), a.DisplayRuleName, a.DisplayHostname))
                .ToList(),
            CheckedAt = checkedAt.ToUnixTimeSeconds(),
        };
    }

    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>The saved snapshot, or <see cref="SignedOut"/> when there's none or it can't be read.</summary>
    public static WidgetSnapshot FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return SignedOut;
        }

        try
        {
            return JsonSerializer.Deserialize<WidgetSnapshot>(json) ?? SignedOut;
        }
        catch (JsonException)
        {
            return SignedOut;
        }
    }

    /// <summary>"critical", "warning" or "ok" - what the widgets colour by.</summary>
    internal static string SeverityKey(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "critical",
        AlertSeverity.Warning => "warning",
        _ => "ok",
    };
}

/// <summary>
/// The platform's home-screen widgets - an Android app widget, an iOS
/// WidgetKit extension.
/// </summary>
public interface IHomeWidgets
{
    /// <summary>
    /// True when there's a widget to keep up to date, so checks are worth
    /// running (and the device list worth reading) for it alone.
    /// </summary>
    bool IsInUse { get; }

    /// <summary>Saves <paramref name="snapshot"/> where the widget reads it, and asks it to redraw.</summary>
    void Update(WidgetSnapshot snapshot);
}

/// <summary>No widgets: the default, and in tests.</summary>
public sealed class NoHomeWidgets : IHomeWidgets
{
    public bool IsInUse => false;

    public void Update(WidgetSnapshot snapshot)
    {
    }
}
