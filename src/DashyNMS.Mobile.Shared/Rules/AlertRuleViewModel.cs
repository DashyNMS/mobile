using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Rules;

/// <summary>
/// One alert rule, read-only (#22): what it matches, where it applies, how
/// it notifies, its template, and what it has alerting now - enough to
/// understand an alert without desktop's editor.
/// </summary>
/// <remarks>
/// LibreNMS's API gives a rule's targets as ids, so the devices, groups and
/// locations are looked up by name; one that's gone shows as its id. The
/// rule's delay and interval aren't in the API's rule (Core doesn't model
/// them), so they aren't shown.
/// </remarks>
public sealed partial class AlertRuleViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private int? _templateId;
    private readonly DashyNMS.Mobile.Alerts.IgnoredAlerts? _ignored;

    [ObservableProperty]
    private string _title = "Alert rule";

    [ObservableProperty]
    private AlertSeverity _severity;

    [ObservableProperty]
    private string _severityText = string.Empty;

    [ObservableProperty]
    private bool _isDisabled;

    [ObservableProperty]
    private string _condition = string.Empty;

    /// <summary>"Runs this SQL exactly as written", "Inverted: …" - or null.</summary>
    [ObservableProperty]
    private string? _conditionNote;

    [ObservableProperty]
    private string _targetSummary = string.Empty;

    /// <summary>The devices, groups and locations it names, by name.</summary>
    [ObservableProperty]
    private string? _targetNames;

    [ObservableProperty]
    private string _recoveryText = string.Empty;

    [ObservableProperty]
    private string _acknowledgementText = string.Empty;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _procedure;

    [ObservableProperty]
    private string _templateName = string.Empty;

    [ObservableProperty]
    private bool _hasLoaded;

    public AlertRuleViewModel(ILibreNmsClient client, ISettingsStore settings, INavigationService navigation, DashyNMS.Mobile.Alerts.IgnoredAlerts? ignored = null)
    {
        _client = client;
        _settings = settings;
        _navigation = navigation;
        _ignored = ignored;
    }

    public int? RuleId { get; set; }

    /// <summary>Its alerts open now, most severe first.</summary>
    public BulkObservableCollection<AlertItem> Alerts { get; } = new();

    public bool HasAlerts => Alerts.Count > 0;

    public bool HasNoAlerts => _alertsLoaded && Alerts.Count == 0;

    /// <summary>"All devices · 2 alerting" - under the rule's name in its header card (#122).</summary>
    public string HeaderText => string.Join(" · ", new[]
    {
        TargetSummary,
        IsDisabled ? "Disabled" : null,
        Alerts.Count > 0 ? RuleText.Count(Alerts.Count, "alert") + " open" : null,
    }.Where(p => !string.IsNullOrWhiteSpace(p)));

    public bool HasConditionNote => ConditionNote is not null;

    public bool HasTargetNames => !string.IsNullOrWhiteSpace(TargetNames);

    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public bool HasProcedure => !string.IsNullOrWhiteSpace(Procedure);

    /// <summary>A template of its own to open; LibreNMS's default otherwise.</summary>
    public bool HasTemplate => _templateId is not null;

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        if (RuleId is not { } id)
        {
            return;
        }

        // Only the rule is waited for (#123): it used to wait on six requests -
        // the whole device list among them - before showing anything. The
        // rest fills in behind it; alerts and templates start now, alongside.
        var alertsTask = BestEffort(() => _client.Alerts.ListAsync(AlertQuery.Open));
        var templatesTask = BestEffort(() => _client.AlertTemplates.ListAsync());
        if (await _client.Rules.GetAsync(id) is not { } rule)
        {
            ErrorMessage = "This rule is no longer in LibreNMS.";
            return;
        }

        ShowRule(rule);
        HasLoaded = true;
        OnPropertyChanged(nameof(CanChangeNotifications));
        Extras = LoadExtrasAsync(rule, alertsTask, templatesTask);
    });

    /// <summary>The template, alerts and target names, filling in behind the rule; awaited by tests.</summary>
    internal Task Extras { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// What fills in behind the rule. The device list - big on a large
    /// server - only when there's something to name with it: devices the rule
    /// targets, or alerts it has open; groups and locations only if it names any.
    /// </summary>
    private async Task LoadExtrasAsync(AlertRule rule, Task<IReadOnlyList<Alert>?> alertsTask, Task<IReadOnlyList<AlertTemplate>?> templatesTask)
    {
        var settings = _settings.Current;
        var groupsTask = rule.Groups.Count > 0 ? BestEffort(() => _client.DeviceGroups.ListAsync()) : Task.FromResult<IReadOnlyList<DeviceGroup>?>([]);
        var locationsTask = rule.Locations.Count > 0 ? BestEffort(() => _client.Locations.ListAsync()) : Task.FromResult<IReadOnlyList<Location>?>([]);

        ShowTemplate(rule, await templatesTask ?? []);

        var alerts = (await alertsTask ?? [])
            .Where(a => a.RuleId == rule.Id)
            .OrderByDescending(a => a.Severity.SortRank())
            .ThenByDescending(a => a.Timestamp)
            .ToList();
        ShowAlerts(alerts, settings, devices: null);

        var devices = rule.Devices.Count > 0 || alerts.Count > 0
            ? (await BestEffort(() => _client.Devices.ListAsync()))?.ToDictionary(d => d.DeviceId)
            : null;
        if (devices is not null)
        {
            ShowAlerts(alerts, settings, devices); // named by the Device names setting now the list is in
        }

        ShowTargets(rule, devices ?? new Dictionary<int, Device>(), await groupsTask ?? [], await locationsTask ?? [], settings);
    }

    private void ShowAlerts(IReadOnlyList<Alert> alerts, AppSettings settings, IReadOnlyDictionary<int, Device>? devices)
    {
        Alerts.ReplaceAll(alerts.Select(a => AlertItem.For(a, settings, devices)).ToList());
        _alertsLoaded = true;
        OnPropertyChanged(nameof(HasAlerts));
        OnPropertyChanged(nameof(HasNoAlerts));
        OnPropertyChanged(nameof(HeaderText));
    }

    private bool _alertsLoaded;

    [RelayCommand]
    private Task OpenAlertAsync(AlertItem? item) => item is null ? Task.CompletedTask : _navigation.GoToAlertAsync(item.Alert);

    [RelayCommand]
    private Task OpenTemplateAsync() => _templateId is { } id
        ? _navigation.GoToAsync(Routes.AlertTemplate, new Dictionary<string, object> { [Routes.TemplateIdParameter] = id })
        : Task.CompletedTask;

    internal void Show(
        AlertRule rule,
        IReadOnlyDictionary<int, Device> devices,
        IReadOnlyList<DeviceGroup> groups,
        IReadOnlyList<Location> locations,
        IReadOnlyList<AlertTemplate> templates,
        AppSettings settings)
    {
        ShowRule(rule);
        ShowTargets(rule, devices, groups, locations, settings);
        ShowTemplate(rule, templates);
    }

    /// <summary>The rule itself: everything that needs nothing else from LibreNMS.</summary>
    private void ShowRule(AlertRule rule)
    {
        Title = RuleText.Name(rule);
        Severity = rule.Severity;
        SeverityText = rule.Severity.ToDisplayString();
        IsDisabled = rule.Disabled;
        Condition = ViewModels.AlertDetailViewModel.BreakCondition(RuleText.Condition(rule));
        ConditionNote = string.Join(" ", new[]
        {
            rule.Extra?.OverrideQuery == true ? "Hand-written SQL, run exactly as written." : null,
            rule.Extra?.Invert == true ? "Inverted: alerts when the condition doesn't match." : null,
        }.Where(n => n is not null)) is { Length: > 0 } note ? note : null;

        TargetSummary = RuleText.Targets(rule);
        OnPropertyChanged(nameof(HeaderText));

        // LibreNMS treats a rule without these flags as having them on.
        RecoveryText = rule.Extra?.Recovery != false ? "On" : "Off";
        AcknowledgementText = rule.Extra?.Acknowledgement != false ? "On" : "Off";
        Notes = rule.Notes?.Trim();
        Procedure = rule.Procedure?.Trim();
        TemplateName = "…";

        OnPropertyChanged(nameof(HasConditionNote));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HasProcedure));
        ShowNotifications(rule);
    }

    /// <summary>The devices, groups and locations it names, by name - once their lists are in.</summary>
    private void ShowTargets(
        AlertRule rule,
        IReadOnlyDictionary<int, Device> devices,
        IReadOnlyList<DeviceGroup> groups,
        IReadOnlyList<Location> locations,
        AppSettings settings)
    {
        var style = settings.DeviceNameStyle;
        var groupNames = groups.ToDictionary(g => g.Id, g => g.Name);
        var locationNames = locations.ToDictionary(l => l.Id, l => l.Name);
        TargetNames = string.Join("\n", new[]
        {
            Names("Devices", rule.Devices, id => devices.TryGetValue(id, out var d) ? new DeviceItem(d, style).Name : null),
            Names("Groups", rule.Groups, id => groupNames.GetValueOrDefault(id)),
            Names("Locations", rule.Locations, id => DeviceLocation.Name(locationNames.GetValueOrDefault(id))),
        }.Where(n => n is not null));
        OnPropertyChanged(nameof(HasTargetNames));
    }

    /// <summary>Its template, once the list is in - LibreNMS's default when none is attached.</summary>
    private void ShowTemplate(AlertRule rule, IReadOnlyList<AlertTemplate> templates)
    {
        var template = AlertRulesViewModel.TemplateFor(rule.Id, templates);
        _templateId = template?.Id;
        TemplateName = template?.Name ?? "LibreNMS's default template";
        OnPropertyChanged(nameof(HasTemplate));
    }

    // ------------------------------------------------------------ notifications (#102)

    private int _ruleId;
    private string _ruleName = string.Empty;

    public bool CanChangeNotifications => _ignored is not null && HasLoaded;

    /// <summary>
    /// The phone notifies about this rule's alerts. Off ignores it on every
    /// device; on again clears that, and any single devices ignored for it.
    /// Only notifications: its alerts still list and count.
    /// </summary>
    public bool Notifies
    {
        get => _ignored?.IsRuleIgnored(_ruleId) != true;
        set
        {
            if (_ignored is null || value == Notifies)
            {
                return;
            }

            if (value)
            {
                _ignored.NotifyAgain(_ruleId);
            }
            else
            {
                _ignored.Ignore(_ruleId, _ruleName);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(NotificationsNote));
            OnPropertyChanged(nameof(HasNotificationsNote));
        }
    }

    /// <summary>The devices it's quiet on, when only some are: "Not on core-sw, edge-rtr".</summary>
    public string? NotificationsNote =>
        _ignored is null || !Notifies ? null
        : _ignored.All.Where(i => i.RuleId == _ruleId && i.DeviceId is not null).Select(i => i.DeviceName ?? $"device {i.DeviceId}").ToList() is { Count: > 0 } devices
            ? $"Not on {string.Join(", ", devices)}"
            : null;

    public bool HasNotificationsNote => NotificationsNote is not null;

    private void ShowNotifications(AlertRule rule)
    {
        _ruleId = rule.Id;
        _ruleName = RuleText.Name(rule);
        OnPropertyChanged(nameof(Notifies));
        OnPropertyChanged(nameof(NotificationsNote));
        OnPropertyChanged(nameof(HasNotificationsNote));
        OnPropertyChanged(nameof(CanChangeNotifications));
    }

    /// <summary>"Devices: core-sw, edge-rtr", with an id for one that's gone.</summary>
    private static string? Names(string heading, IReadOnlyList<int> ids, Func<int, string?> name) =>
        ids.Count == 0 ? null : $"{heading}: {string.Join(", ", ids.Select(id => name(id) ?? $"#{id}"))}";

    private static async Task<T?> BestEffort<T>(Func<Task<T>> load) where T : class
    {
        try
        {
            return await load();
        }
        catch (LibreNmsApiException)
        {
            return null;
        }
    }
}

/// <summary>
/// One alert template, read-only (#22): its titles and body, monospaced as
/// written, and which rules use it.
/// </summary>
public sealed partial class AlertTemplateViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string _title = "Alert template";

    [ObservableProperty]
    private string? _alertTitle;

    [ObservableProperty]
    private string? _recoveryTitle;

    [ObservableProperty]
    private string _body = string.Empty;

    /// <summary>"Used by 3 rules", or that LibreNMS only uses it as the default.</summary>
    [ObservableProperty]
    private string _usedBy = string.Empty;

    [ObservableProperty]
    private bool _hasLoaded;

    public AlertTemplateViewModel(ILibreNmsClient client, INavigationService navigation)
    {
        _client = client;
        _navigation = navigation;
    }

    /// <summary>The rules using it, as the rules list shows them - each opens its rule (#122).</summary>
    public BulkObservableCollection<AlertRuleItem> Rules { get; } = new();

    public bool HasRules => Rules.Count > 0;

    public int? TemplateId { get; set; }

    public bool HasAlertTitle => !string.IsNullOrWhiteSpace(AlertTitle);

    public bool HasRecoveryTitle => !string.IsNullOrWhiteSpace(RecoveryTitle);

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        if (TemplateId is not { } id)
        {
            return;
        }

        var templateTask = _client.AlertTemplates.GetAsync(id);
        var rulesTask = _client.Rules.ListAsync();
        await Task.WhenAll(templateTask, rulesTask);

        if (templateTask.Result is not { } template)
        {
            ErrorMessage = "This template is no longer in LibreNMS.";
            return;
        }

        Show(template, rulesTask.Result);
    });

    [RelayCommand]
    private Task OpenRuleAsync(AlertRuleItem? item) => item is null
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.AlertRule, new Dictionary<string, object> { [Routes.RuleIdParameter] = item.Id });

    internal void Show(AlertTemplate template, IReadOnlyList<AlertRule> rules)
    {
        Title = string.IsNullOrWhiteSpace(template.Name) ? $"Template {template.Id}" : template.Name;
        AlertTitle = template.Title?.Trim();
        RecoveryTitle = template.TitleRec?.Trim();
        Body = template.Template?.Replace("\r\n", "\n").TrimEnd() ?? string.Empty;

        var name = Title;
        Rules.ReplaceAll(rules
            .Where(r => template.AlertRules.Contains(r.Id))
            .Select(r => new AlertRuleItem(r, 0, name))
            .OrderBy(r => r.IsDisabled)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase));
        UsedBy = Rules.Count == 0 ? "Used by no rules - LibreNMS uses it only if it's the default." : "Used by " + RuleText.Count(Rules.Count, "rule");
        HasLoaded = true;
        OnPropertyChanged(nameof(HasRules));

        OnPropertyChanged(nameof(HasAlertTitle));
        OnPropertyChanged(nameof(HasRecoveryTitle));
    }
}
