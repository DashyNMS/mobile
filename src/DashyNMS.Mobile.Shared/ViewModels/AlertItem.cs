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

    public DateTime? LocalTimestamp => ServerTime.ToLocal(Alert.Timestamp, _serverTimestampsAreUtc);

    /// <summary>"5m ago · Acknowledged", as the list's second line.</summary>
    public string Summary
    {
        get
        {
            var age = Alert.Timestamp is { } t ? Formatting.Age(ServerTime.Age(t, _serverTimestampsAreUtc)) : "unknown time";
            return $"{age} · {StateText}";
        }
    }
}
