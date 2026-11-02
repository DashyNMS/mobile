using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Rules;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Alerts;

/// <summary>One of LibreNMS's alert rules, as Add a rule lists it.</summary>
/// <param name="Note">Already on the list on every device, or on some devices - which adding takes in.</param>
public sealed record NotifyRuleChoice(int RuleId, string Name, AlertSeverity Severity, string? Note, bool IsListed)
{
    public string Detail => Note ?? Severity.ToDisplayString();

    public bool CanAdd => !IsListed;

    public string SelectionDescription => $"{Name}, {Detail}. " +
        (CanAdd ? "Add on every device and go back" : "Cannot add again");
}

/// <summary>
/// Settings › Notifications › Add a rule (#167): adds one of LibreNMS's
/// alert rules, on every device, to the chosen mode's list. One device at a
/// time is Change on one of its alerts, as desktop's rule picker.
/// </summary>
public sealed partial class NotifyRulePickerViewModel : ViewModelBase
{
    private readonly ILibreNmsClient _client;
    private readonly NotifyRules _rules;
    private readonly INavigationService _navigation;
    private IReadOnlyList<NotifyRuleChoice> _all = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    public NotifyRulePickerViewModel(ILibreNmsClient client, NotifyRules rules, INavigationService navigation)
    {
        _client = client;
        _rules = rules;
        _navigation = navigation;
    }

    public BulkObservableCollection<NotifyRuleChoice> Rules { get; } = new();

    public bool IsEmpty => !IsBusy && !HasError && Rules.Count == 0;

    public string EmptyText => _all.Count == 0
        ? "No alert rules are available from this server."
        : "No alert rules match your search.";

    /// <summary>"Leaves it out" or "Notifies you about it" - what adding does in this mode.</summary>
    public string Intro => _rules.Mode == NotificationRuleMode.Only
        ? "Notifies you about the rule on every device. For one device only, use Change on one of its alerts."
        : "Leaves the rule out on every device. For one device only, use Change on one of its alerts.";

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(IsBusy) or nameof(HasError))
            OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnSearchTextChanged(string value) => Filter();

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        _all = [];
        Rules.ReplaceAll([]);
        var listed = _rules.Listed;
        _all = (await _client.Rules.ListAsync())
            .Select(rule =>
            {
                var entries = listed.Where(e => e.RuleId == rule.Id).ToList();
                var everywhere = entries.Any(e => e.DeviceId is null);
                var devices = string.Join(", ", entries.Where(e => e.DeviceId is not null).Select(e => e.DeviceName ?? $"device {e.DeviceId}"));
                var note = everywhere ? "Already on the list, on every device"
                    : devices.Length > 0 ? $"On the list for {devices} · adding covers every device"
                    : null;
                return new NotifyRuleChoice(rule.Id, RuleText.Name(rule), rule.Severity, note, everywhere);
            })
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Filter();
    });

    [RelayCommand]
    private async Task ChooseAsync(NotifyRuleChoice? choice)
    {
        if (choice is not { CanAdd: true })
        {
            return;
        }

        _rules.AddEverywhere(choice.RuleId, choice.Name);
        await _navigation.GoToAsync(Routes.Back);
    }

    private void Filter()
    {
        var term = SearchText.Trim();
        Rules.ReplaceAll(_all.Where(r => term.Length == 0 || r.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList());
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }
}
