using System.Text.Json;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Alerts;

/// <summary>
/// Which alerts notify the phone (#167): every alert, every alert except
/// some rules, or only some rules - each on every device or on one. Desktop's
/// settings since DashyNMS/desktop#268 (<see cref="NotificationSettings.RuleMode"/>,
/// <see cref="NotificationSettings.ExceptRules"/>, <see cref="NotificationSettings.OnlyRules"/>),
/// decided by Core's <see cref="NotificationRuleChoices"/> so both apps
/// offer and word the same choices. Only notifications: a left-out alert
/// still lists on the Alerts tab and still counts everywhere.
/// </summary>
/// <remarks>
/// <para>Core's <see cref="AlertNotificationPlanner"/> applies the rules
/// itself, so nothing filters before it.</para>
/// <para>Before 1.1.0 the phone kept its own list of alerts not to notify
/// about (#102), as a preference. That list moves into
/// <see cref="NotificationSettings.ExceptRules"/> once - the fields are the
/// same - the mode becomes "Every alert except…" and the old key is cleared.
/// An old list that can't be read is dropped, as it always read as empty.</para>
/// </remarks>
public sealed class NotifyRules
{
    /// <summary>Where the phone kept its ignored alerts before #167.</summary>
    internal const string OldKey = "notifications.ignored";

    private readonly ISettingsStore _settings;

    public NotifyRules(ISettingsStore settings, Services.IAppPreferences preferences)
    {
        _settings = settings;
        MoveOldList(preferences);
    }

    /// <summary>Raised after any change, for Settings' list and the alert and rule pages.</summary>
    public event EventHandler? Changed;

    private NotificationSettings Notifications => _settings.Current.Notifications;

    public NotificationRuleMode Mode
    {
        get => Notifications.RuleMode;
        set
        {
            if (Notifications.RuleMode == value)
            {
                return;
            }

            // Each mode keeps its own list, so switching and back loses nothing.
            Notifications.RuleMode = value;
            Save();
        }
    }

    /// <summary>The list the current mode uses: left out, or the only ones that notify. Empty for every alert.</summary>
    public IReadOnlyList<NotificationRuleEntry> Listed => Notifications.RulesFor(Mode);

    public bool Allows(Alert alert) => Notifications.AllowsRule(alert);

    /// <summary>The Notifications line on an alert: "Notifies you", or why not.</summary>
    public string StatusFor(Alert alert)
    {
        var covering = Listed.FirstOrDefault(e => e.Covers(alert));
        return Mode switch
        {
            NotificationRuleMode.AllExcept when covering is not null => $"Not notified: {covering.Description}",
            NotificationRuleMode.Only when covering is not null => $"Notifies you: {covering.Description}",
            NotificationRuleMode.Only => "Not notified: only chosen rules notify you",
            _ => "Notifies you",
        };
    }

    /// <summary>The Notifications line on a rule's page, every device it's on or off for.</summary>
    public string StatusForRule(int ruleId)
    {
        var entries = Listed.Where(e => e.RuleId == ruleId).ToList();
        var everywhere = entries.Any(e => e.DeviceId is null);
        var devices = string.Join(", ", entries.Where(e => e.DeviceId is not null).Select(e => e.DeviceName ?? $"device {e.DeviceId}"));
        return Mode switch
        {
            NotificationRuleMode.AllExcept when everywhere => "Not notified on any device",
            NotificationRuleMode.AllExcept when devices.Length > 0 => $"Notifies you, except on {devices}",
            NotificationRuleMode.Only when everywhere => "Notifies you on every device",
            NotificationRuleMode.Only when devices.Length > 0 => $"Notifies you on {devices} only",
            NotificationRuleMode.Only => "Not notified: only chosen rules notify you",
            _ => "Notifies you",
        };
    }

    /// <summary>
    /// What Change offers on an alert (a device) or a rule (none): Core's
    /// choices that keep the mode. Moving between modes is Settings' job, as
    /// on desktop, where a right-click never changes mode either.
    /// </summary>
    public IReadOnlyList<NotificationRuleAction> ChoicesFor(int ruleId, int? deviceId) =>
        NotificationRuleChoices.For(Notifications, ruleId, deviceId)
            .Where(a => !NotificationRuleChoices.ChangesMode(Notifications, a))
            .ToList();

    /// <summary>Makes a choice from <see cref="ChoicesFor"/> and saves; the result undoes it.</summary>
    public Snapshot Apply(NotificationRuleAction action, int ruleId, string ruleName, int? deviceId = null, string? deviceName = null)
    {
        var before = Take();
        NotificationRuleChoices.Apply(Notifications, action, ruleId, ruleName, deviceId, deviceName);
        Save();
        return before;
    }

    /// <summary>Takes one entry off the current mode's list, as Settings' list removes it; the result undoes it.</summary>
    public Snapshot Remove(NotificationRuleEntry entry)
    {
        var before = Take();
        Notifications.RulesFor(Mode).Remove(entry);
        Save();
        return before;
    }

    /// <summary>
    /// Adds a rule on every device to the current mode's list - Settings'
    /// Add a rule. One device at a time is Change on one of its alerts.
    /// </summary>
    public bool AddEverywhere(int ruleId, string ruleName)
    {
        if (Mode == NotificationRuleMode.All || !NotificationRuleList.Add(Notifications.RulesFor(Mode), ruleId, ruleName))
        {
            return false;
        }

        Save();
        return true;
    }

    /// <summary>Puts the mode and both lists back as they were.</summary>
    public void Restore(Snapshot snapshot)
    {
        Notifications.RuleMode = snapshot.Mode;
        Notifications.ExceptRules = snapshot.Except.ToList();
        Notifications.OnlyRules = snapshot.Only.ToList();
        Save();
    }

    /// <summary>The mode and both lists, for Undo.</summary>
    public sealed record Snapshot(NotificationRuleMode Mode, IReadOnlyList<NotificationRuleEntry> Except, IReadOnlyList<NotificationRuleEntry> Only);

    private Snapshot Take() => new(Notifications.RuleMode, Notifications.ExceptRules.ToList(), Notifications.OnlyRules.ToList());

    private void Save()
    {
        _settings.Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void MoveOldList(Services.IAppPreferences preferences)
    {
        if (preferences.Get(OldKey) is not { Length: > 0 } json)
        {
            return;
        }

        List<NotificationRuleEntry>? old;
        try
        {
            old = JsonSerializer.Deserialize<List<NotificationRuleEntry>>(json);
        }
        catch (JsonException)
        {
            old = null;
        }

        if (old is { Count: > 0 })
        {
            foreach (var entry in old.Where(e => !string.IsNullOrEmpty(e.RuleName)))
            {
                NotificationRuleList.Add(Notifications.ExceptRules, entry.RuleId, entry.RuleName, entry.DeviceId, entry.DeviceName);
            }

            if (Notifications.RuleMode == NotificationRuleMode.All)
            {
                Notifications.RuleMode = NotificationRuleMode.AllExcept;
            }

            _settings.Save();
        }

        preferences.Set(OldKey, null);
    }
}
