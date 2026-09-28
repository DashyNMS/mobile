using System.Globalization;
using DesktopNMS.Core;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>One row of the alert list.</summary>
public sealed class AlertItem
{
    private readonly bool _serverTimestampsAreUtc;

    public AlertItem(Alert alert, bool serverTimestampsAreUtc)
    {
        Alert = alert;
        _serverTimestampsAreUtc = serverTimestampsAreUtc;
    }

    public Alert Alert { get; }

    public int Id => Alert.Id;

    public string Rule => Alert.DisplayRuleName;

    public string Device => Alert.DisplayHostname;

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
