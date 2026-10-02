using System.Text.Json;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Alerts;

/// <summary>
/// An alert rule the phone doesn't notify about - everywhere, or on one
/// device. The names are kept for Settings' list, as they were when chosen,
/// so it reads without asking LibreNMS.
/// </summary>
public sealed record IgnoredAlert(int RuleId, string RuleName, int? DeviceId = null, string? DeviceName = null)
{
    /// <summary>"Port down", or "Port down on core-sw".</summary>
    public string Description => DeviceId is null ? $"{RuleName}, on every device" : $"{RuleName} on {DeviceName ?? $"device {DeviceId}"}";

    public bool Covers(Alert alert) => alert.RuleId == RuleId && (DeviceId is null || alert.DeviceId == DeviceId);
}

/// <summary>
/// Alerts that don't send notifications (#102), chosen by rule - on every
/// device, or just one. Only notifications: an ignored alert still lists on
/// the Alerts tab, and still counts on the app icon, the tab's dot and the
/// dashboard, so nothing is hidden, just quiet.
/// </summary>
/// <remarks>
/// A phone preference, as <see cref="AlertCountThreshold"/> is - nothing in
/// LibreNMS changes, and desktop's notifications are its own. Kept as JSON
/// under one key; anything unreadable counts as nothing ignored rather than
/// failing the alert check.
/// </remarks>
public sealed class IgnoredAlerts(Services.IAppPreferences preferences)
{
    internal const string Key = "notifications.ignored";

    /// <summary>Raised when something is ignored or notified again, for Settings' list.</summary>
    public event EventHandler? Changed;

    /// <summary>Everything ignored, a rule's every-device entry before its single devices.</summary>
    public IReadOnlyList<IgnoredAlert> All
    {
        get
        {
            try
            {
                return preferences.Get(Key) is { Length: > 0 } json
                    ? JsonSerializer.Deserialize<List<IgnoredAlert>>(json) ?? []
                    : [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }

    public bool IsIgnored(Alert alert) => All.Any(i => i.Covers(alert));

    /// <summary>The rule is quiet on every device.</summary>
    public bool IsRuleIgnored(int ruleId) => All.Any(i => i.RuleId == ruleId && i.DeviceId is null);

    /// <summary>The changes that may notify: none from an ignored alert.</summary>
    public IReadOnlyList<AlertChange> Filter(IReadOnlyList<AlertChange> changes)
    {
        var ignored = All;
        return ignored.Count == 0 ? changes : changes.Where(c => !ignored.Any(i => i.Covers(c.Alert))).ToList();
    }

    /// <summary>
    /// Stops notifications from a rule - on one device, or (no device) on all
    /// of them, which takes in any single devices already ignored for it.
    /// </summary>
    public void Ignore(int ruleId, string ruleName, int? deviceId = null, string? deviceName = null)
    {
        var all = All.ToList();
        if (all.Any(i => i.RuleId == ruleId && (i.DeviceId is null || i.DeviceId == deviceId)))
        {
            return;
        }

        if (deviceId is null)
        {
            all.RemoveAll(i => i.RuleId == ruleId);
        }

        all.Add(new IgnoredAlert(ruleId, ruleName, deviceId, deviceName));
        Save(all.OrderBy(i => i.RuleName, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.DeviceId is not null).ThenBy(i => i.DeviceName, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Notifications again, for <paramref name="alert"/>: whatever ignored it - its rule's every-device entry, or its device's.</summary>
    public void NotifyAgain(Alert alert) => Save(All.Where(i => !i.Covers(alert)));

    /// <summary>Notifications again from one entry, as Settings' list removes it.</summary>
    public void NotifyAgain(IgnoredAlert entry) => Save(All.Where(i => i != entry));

    /// <summary>Notifications again from a rule, everywhere it was ignored.</summary>
    public void NotifyAgain(int ruleId) => Save(All.Where(i => i.RuleId != ruleId));

    private void Save(IEnumerable<IgnoredAlert> entries)
    {
        var list = entries.ToList();
        preferences.Set(Key, list.Count == 0 ? null : JsonSerializer.Serialize(list));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
