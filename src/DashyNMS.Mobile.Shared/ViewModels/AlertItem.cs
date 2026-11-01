using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DesktopNMS.Core;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>One row of the alert list.</summary>
public sealed partial class AlertItem : ObservableObject
{
    /// <summary>Its detail is showing beside the list, on a larger screen (#88).</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Ticked for a bulk action (#85).</summary>
    [ObservableProperty]
    private bool _isTicked;

    /// <summary>
    /// The API token may acknowledge and unacknowledge alerts - false once
    /// LibreNMS has refused it (#144), when the row's swipe actions go.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAcknowledge), nameof(ShowUnacknowledge))]
    private bool _canChange = true;

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

    /// <summary>A collapsed or expanded device/rule header in the virtualised alert list.</summary>
    public AlertGroup? Group { get; init; }
    public bool IsGroupHeader => Group is not null;
    public bool IsFaultChild { get; init; }
    public bool IsExpanded { get; init; }
    public string RowRule => IsFaultChild ? AlertGrouping.EntityName(Alert) ?? "Device fault" : Rule;
    public string AcknowledgeLabel => Alert.Source == AlertSource.Fault ? "Acknowledge group" : "Acknowledge";
    public string UnacknowledgeLabel => Alert.Source == AlertSource.Fault ? "Unacknowledge group" : "Unacknowledge";
    public string? ActionRefusedText { get; init; }
    public string InteractionHint => IsGroupHeader ? "Tap to show or hide faults" : IsFaultChild ? "Tap to open this fault" : "Touch and hold to select several";
    /// <summary>What VoiceOver reads for the group's chevron: "Show 3 faults", "Hide faults".</summary>
    public string ExpansionText => IsExpanded ? "Hide faults" : $"Show {FaultsText}";

    private string FaultsText => Group is { FaultCount: var count } ? $"{count} {(count == 1 ? "fault" : "faults")}" : "faults";
    public string GroupTimesText => Group is null ? string.Empty : $"First seen {TimeText(Group.FirstSeen)} · Last seen {TimeText(Group.LastChanged)}";
    private string TimeText(DateTime? time) => ServerTime.ToLocal(time, _serverTimestampsAreUtc)?.ToString("g", CultureInfo.CurrentCulture) ?? "unknown";

    public int Id => Alert.Id;

    public string Rule => Alert.DisplayRuleName;

    public string Device => _deviceName ?? Alert.DisplayHostname;

    public AlertSeverity Severity => Group?.Severity ?? Alert.Severity;

    public string SeverityText => Severity.ToDisplayString();

    public AlertState State => Group?.State ?? Alert.State;

    public string StateText => State.ToDisplayString();

    public bool IsAcknowledged => State == AlertState.Acknowledged;

    public bool IsNotAcknowledged => !IsAcknowledged;

    /// <summary>The swipe action to acknowledge: active, and the token may.</summary>
    public bool ShowAcknowledge => IsNotAcknowledged && CanChange;

    /// <summary>The swipe action to unacknowledge: acknowledged, and the token may.</summary>
    public bool ShowUnacknowledge => (IsAcknowledged || Group is { AcknowledgedCount: > 0 }) && CanChange;

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

    /// <summary>
    /// "#4821 · Acknowledged", or a group's "3 faults · Active": the list's
    /// quiet third line, with the severity and age at the right instead.
    /// </summary>
    public string MetaText => Group is { } group
        ? $"{FaultsText} · " + (group.IsPartlyAcknowledged ? $"Partly acknowledged ({group.AcknowledgedCount}/{group.FaultCount})" : StateText)
        : $"#{Id.ToString(CultureInfo.InvariantCulture)} · {StateText}";

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
        || Id.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.Ordinal)
        || (AlertGrouping.EntityName(Alert)?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
}
