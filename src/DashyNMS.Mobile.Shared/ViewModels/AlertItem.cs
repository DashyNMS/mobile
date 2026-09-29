using System.Globalization;
using DesktopNMS.Core;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>One row of the alert list.</summary>
public sealed class AlertItem
{
    private readonly bool _serverTimestampsAreUtc;
    private readonly string? _deviceName;

    /// <param name="deviceName">
    /// The device's name as the Device names setting picks it (see
    /// <see cref="For"/>); without it, the hostname LibreNMS sent with the alert.
    /// </param>
    public AlertItem(Alert alert, bool serverTimestampsAreUtc, string? deviceName = null)
    {
        Alert = alert;
        _serverTimestampsAreUtc = serverTimestampsAreUtc;
        _deviceName = deviceName;
    }

    /// <summary>
    /// An alert named as desktop names it: the Device names setting applied to
    /// its device, found in <paramref name="devices"/> - the alert itself only
    /// carries the hostname, so without the device that's what shows.
    /// </summary>
    public static AlertItem For(Alert alert, AppSettings settings, IReadOnlyDictionary<int, Device>? devices) =>
        new(
            alert,
            settings.ServerTimestampsAreUtc,
            settings.DeviceNameStyle.Resolve(devices?.GetValueOrDefault(alert.DeviceId), alert.Hostname));

    public Alert Alert { get; }

    public int Id => Alert.Id;

    public string Rule => Alert.DisplayRuleName;

    public string Device => _deviceName ?? Alert.DisplayHostname;

    public AlertSeverity Severity => Alert.Severity;

    public string SeverityText => Severity.ToDisplayString();

    public AlertState State => Alert.State;

    public string StateText => State.ToDisplayString();

    public bool IsAcknowledged => Alert.IsAcknowledged;

    public bool IsNotAcknowledged => !IsAcknowledged;

    public string? Note => string.IsNullOrWhiteSpace(Alert.Note) ? null : Alert.Note;

    public bool HasNote => Note is not null;

    public DateTime? LocalTimestamp => ServerTime.ToLocal(Alert.Timestamp, _serverTimestampsAreUtc);

    /// <summary>"5m ago", or empty when the server gave no time.</summary>
    public string AgeText => Alert.Timestamp is { } t ? Formatting.Age(ServerTime.Age(t, _serverTimestampsAreUtc)) : string.Empty;

    /// <summary>"5m", "now" - the list's age at the row's right, as the mock-ups (#69).</summary>
    public string ShortAge => AgeText switch
    {
        "" => string.Empty,
        "just now" => "now",
        var age => age.Replace(" ago", string.Empty, StringComparison.Ordinal),
    };

    /// <summary>"#4821 · Acknowledged": the list's quiet third line, with the age at the right instead.</summary>
    public string MetaText => $"#{Id.ToString(CultureInfo.InvariantCulture)} · {StateText}";

    /// <summary>"5m ago · Acknowledged", as the list's second line.</summary>
    public string Summary => $"{(AgeText.Length > 0 ? AgeText : "unknown time")} · {StateText}";

    /// <summary>
    /// The fields desktop's alert search looks in: device, rule, severity,
    /// state, note and alert id.
    /// </summary>
    public bool Matches(string term) =>
        Device.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Rule.Contains(term, StringComparison.OrdinalIgnoreCase)
        || SeverityText.Contains(term, StringComparison.OrdinalIgnoreCase)
        || StateText.Contains(term, StringComparison.OrdinalIgnoreCase)
        || (Note?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || Id.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.Ordinal);
}
