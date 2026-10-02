using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Rules;

/// <summary>A rule's condition and targets as text, as desktop's rule list shows them.</summary>
public static class RuleText
{
    /// <summary>
    /// The condition as LibreNMS's own rule list shows it - the builder in its
    /// SQL-ish form, or the hand-written SQL when the rule overrides its
    /// query, that being what actually runs.
    /// </summary>
    public static string Condition(AlertRule rule) =>
        (rule.Extra?.OverrideQuery == true && !string.IsNullOrWhiteSpace(rule.Query)
            ? rule.Query
            : AlertRuleSqlFormatter.Format(rule.Builder) ?? rule.Rule ?? rule.Query ?? string.Empty).Trim();

    /// <summary>
    /// "3 devices, 2 groups", "1 location", or "All devices" when nothing is
    /// listed - LibreNMS applies an untargeted rule everywhere. With
    /// invert_map the lists are what it leaves out.
    /// </summary>
    public static string Targets(AlertRule rule)
    {
        var parts = new List<string>();
        if (rule.Devices.Count > 0)
        {
            parts.Add(Count(rule.Devices.Count, "device"));
        }

        if (rule.Groups.Count > 0)
        {
            parts.Add(Count(rule.Groups.Count, "group"));
        }

        if (rule.Locations.Count > 0)
        {
            parts.Add(Count(rule.Locations.Count, "location"));
        }

        return parts.Count == 0 ? "All devices" : (rule.InvertMap ? "All except " : string.Empty) + string.Join(", ", parts);
    }

    public static string Name(AlertRule rule) => string.IsNullOrWhiteSpace(rule.Name) ? $"Rule {rule.Id}" : rule.Name;

    internal static string Count(int count, string thing) => count == 1 ? $"1 {thing}" : $"{count} {thing}s";
}

/// <summary>One rule in the list.</summary>
public sealed record AlertRuleItem(AlertRule Rule, int Alerting, string? TemplateName)
{
    public int Id => Rule.Id;

    public string Name => RuleText.Name(Rule);

    public AlertSeverity Severity => Rule.Severity;

    public bool IsDisabled => Rule.Disabled;

    /// <summary>"Critical · All devices", "Warning · 3 devices · Disabled".</summary>
    public string Summary => string.Join(" · ", new[]
    {
        Severity.ToDisplayString(),
        RuleText.Targets(Rule),
        IsDisabled ? "Disabled" : null,
    }.Where(p => p is not null));

    public string Condition { get; } = RuleText.Condition(Rule);

    public bool IsAlerting => Alerting > 0;

    public string AlertingText => Alerting == 1 ? "1 alerting" : $"{Alerting} alerting";

    public bool Matches(string term) =>
        Name.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Condition.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Summary.Contains(term, StringComparison.OrdinalIgnoreCase)
        || (TemplateName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || (Rule.Notes?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
}

/// <summary>
/// LibreNMS's alert rules, read-only (#22): desktop has the editor, but on a
/// phone it's for seeing which rules exist and what they match while
/// looking into an alert. Rules alerting now come first, then by severity.
/// </summary>
public sealed partial class AlertRulesViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly INavigationService _navigation;
    private IReadOnlyList<AlertRuleItem> _all = [];
    private bool _loaded;

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>Only the rules alerting now - the filter chip.</summary>
    [ObservableProperty]
    private bool _onlyAlerting;

    public AlertRulesViewModel(ILibreNmsClient client, INavigationService navigation)
    {
        _client = client;
        _navigation = navigation;
    }

    public BulkObservableCollection<AlertRuleItem> Rules { get; } = new();

    /// <summary>"42 rules · 3 alerting".</summary>
    [ObservableProperty]
    private string _summary = string.Empty;

    public bool IsEmpty => _loaded && Rules.Count == 0 && !IsBusy;

    public string EmptyText => !string.IsNullOrWhiteSpace(SearchText) || OnlyAlerting ? "No rules match." : "LibreNMS has no alert rules.";

    partial void OnSearchTextChanged(string value) => WhenTypingPauses(ApplyFilter);

    partial void OnOnlyAlertingChanged(bool value) => ApplyFilter();

    [RelayCommand]
    private void ToggleAlerting() => OnlyAlerting = !OnlyAlerting;

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        var rulesTask = _client.Rules.ListAsync();
        var alertsTask = _client.Alerts.ListAsync(AlertQuery.Open);
        var templatesTask = BestEffort(() => _client.AlertTemplates.ListAsync());
        await Task.WhenAll(rulesTask, alertsTask, templatesTask);

        _all = Build(rulesTask.Result, alertsTask.Result, templatesTask.Result ?? []);
        _loaded = true;
        var alerting = _all.Count(r => r.IsAlerting);
        Summary = $"{RuleText.Count(_all.Count, "rule")}{(alerting > 0 ? $" · {alerting} alerting" : string.Empty)}";
        ApplyFilter();
    });

    [RelayCommand]
    private Task OpenAsync(AlertRuleItem? item) => item is null
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.AlertRule, new Dictionary<string, object> { [Routes.RuleIdParameter] = item.Id });

    /// <summary>Alerting first, then by severity and name; each with how many alerts it has open and its template.</summary>
    internal static IReadOnlyList<AlertRuleItem> Build(IReadOnlyList<AlertRule> rules, IReadOnlyList<Alert> openAlerts, IReadOnlyList<AlertTemplate> templates)
    {
        var alerting = openAlerts.GroupBy(a => a.RuleId).ToDictionary(g => g.Key, g => g.Count());
        return rules
            .Select(r => new AlertRuleItem(r, alerting.GetValueOrDefault(r.Id), TemplateFor(r.Id, templates)?.Name))
            .OrderByDescending(r => r.IsAlerting)
            .ThenBy(r => r.IsDisabled)
            .ThenByDescending(r => r.Severity.SortRank())
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The template attached to a rule, if any - otherwise LibreNMS uses its default.</summary>
    internal static AlertTemplate? TemplateFor(int ruleId, IReadOnlyList<AlertTemplate> templates) =>
        templates.FirstOrDefault(t => t.AlertRules.Contains(ruleId));

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        Rules.ReplaceAll(_all.Where(r => (!OnlyAlerting || r.IsAlerting) && (term.Length == 0 || r.Matches(term))).ToList());
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    /// <summary>Templates are an extra: without them the rules still list.</summary>
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
