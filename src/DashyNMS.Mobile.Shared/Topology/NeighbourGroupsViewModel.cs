using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Topology;

namespace DashyNMS.Mobile.Topology;

/// <summary>One group in the list: its name, what its rules ask for, and how many neighbours it holds.</summary>
/// <param name="Count">Null until the neighbours have been fetched.</param>
public sealed record NeighbourGroupRow(NeighbourViewDefinition Group, int? Count = null)
{
    public string Name => Group.Name;

    /// <summary>"System name starts with "SEP" · 30", or "2 rules, any must match · 30".</summary>
    public string Summary => string.Join(" · ", new[]
    {
        Group.Rules.Count switch
        {
            0 => "No rules yet",
            1 => NeighbourRuleRow.Describe(Group.Rules[0]),
            var n => string.Create(CultureInfo.CurrentCulture, $"{n} rules, {(Group.MatchAll ? "all" : "any")} must match"),
        },
        Count?.ToString("N0", CultureInfo.CurrentCulture),
    }.Where(s => s is not null));
}

/// <summary>
/// The Neighbours page's groups (#98) - desktop's neighbour views, kept in
/// its own <see cref="AppSettings.NeighbourViews"/> so both apps share them:
/// add one, open one to change it, reorder or delete.
/// </summary>
public sealed partial class NeighbourGroupsViewModel : ViewModelBase
{
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly NeighbourDirectory _neighbours;

    public NeighbourGroupsViewModel(ISettingsStore settings, INavigationService navigation, IDialogService dialogs, NeighbourDirectory neighbours)
    {
        _settings = settings;
        _navigation = navigation;
        _dialogs = dialogs;
        _neighbours = neighbours;
        Refresh();
    }

    public ObservableCollection<NeighbourGroupRow> Groups { get; } = new();

    public bool HasNone => Groups.Count == 0;

    /// <summary>Back from the editor, or on opening - with each group's count once the neighbours are known.</summary>
    public void Refresh()
    {
        var links = _neighbours.Links;
        Groups.Clear();
        foreach (var group in _settings.Current.NeighbourViews)
        {
            Groups.Add(new NeighbourGroupRow(group, links?.Count(l => NeighboursViewModel.InGroup(group, l))));
        }

        OnPropertyChanged(nameof(HasNone));
    }

    /// <summary>Fetches the neighbours if the Neighbours page hasn't, for the counts. Not fatal - the groups still show.</summary>
    public async Task LoadCountsAsync()
    {
        if (_neighbours.Links is not null)
        {
            return;
        }

        if (await RunAsync(() => _neighbours.GetAsync()))
        {
            Refresh();
        }
    }

    [RelayCommand]
    private Task AddAsync() => _navigation.GoToAsync(Routes.NeighbourGroupEditor);

    [RelayCommand]
    private Task EditAsync(NeighbourGroupRow? row) => row is null
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.NeighbourGroupEditor, new Dictionary<string, object> { [Routes.GroupIdParameter] = row.Group.Id });

    [RelayCommand]
    private void MoveUp(NeighbourGroupRow? row) => Move(row, -1);

    [RelayCommand]
    private void MoveDown(NeighbourGroupRow? row) => Move(row, +1);

    [RelayCommand]
    private async Task DeleteAsync(NeighbourGroupRow? row)
    {
        if (row is null || !await _dialogs.ConfirmDestructiveAsync("Delete neighbourhood", $"Delete the neighbourhood \"{row.Name}\"? It goes from desktop too; nothing changes in LibreNMS. {Confirmations.CannotBeUndone}", "Delete"))
        {
            return;
        }

        _settings.Current.NeighbourViews.RemoveAll(v => v.Id == row.Group.Id);
        _settings.Save();
        Refresh();
    }

    private void Move(NeighbourGroupRow? row, int by)
    {
        var groups = _settings.Current.NeighbourViews;
        var from = row is null ? -1 : groups.FindIndex(v => v.Id == row.Group.Id);
        var to = from + by;
        if (from < 0 || to < 0 || to >= groups.Count)
        {
            return;
        }

        (groups[from], groups[to]) = (groups[to], groups[from]);
        _settings.Save();
        Refresh();
    }
}

/// <summary>
/// One rule in the group editor: which LLDP field, how to test it, and
/// what with - checked as it's typed when it's a regular expression.
/// </summary>
public sealed partial class NeighbourRuleRow : ObservableObject
{
    /// <summary>The fields, in <see cref="NeighbourRuleField"/>'s order.</summary>
    public static IReadOnlyList<string> FieldLabels { get; } =
        ["System name", "System description", "Port ID", "Protocol", "Switch", "Switch port description"];

    /// <summary>The tests, in <see cref="NeighbourRuleOperator"/>'s order.</summary>
    public static IReadOnlyList<string> OperatorLabels { get; } =
        ["contains", "starts with", "equals", "does not contain", "matches (regular expression)"];

    private readonly NeighbourRule _original;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FieldText))]
    private int _fieldIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Problem), nameof(HasProblem), nameof(OperatorText))]
    private int _operatorIndex;

    /// <summary>How many neighbours this rule alone matches (#121); null until they're known, or with no value yet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MatchText), nameof(HasMatchText))]
    private int? _matchCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Problem), nameof(HasProblem))]
    private string _value;

    public NeighbourRuleRow(NeighbourRule rule)
    {
        _original = rule;
        _fieldIndex = (int)rule.Field;
        _operatorIndex = (int)rule.Operator;
        _value = rule.Value;
    }

    public IReadOnlyList<string> Fields => FieldLabels;

    public IReadOnlyList<string> Operators => OperatorLabels;

    /// <summary>The field chip: "System name".</summary>
    public string FieldText => FieldLabels[Math.Clamp(FieldIndex, 0, FieldLabels.Count - 1)];

    /// <summary>The test chip: "starts with".</summary>
    public string OperatorText => OperatorLabels[Math.Clamp(OperatorIndex, 0, OperatorLabels.Count - 1)];

    /// <summary>"Matches 28".</summary>
    public string MatchText => MatchCount is { } count ? "Matches " + count.ToString("N0", CultureInfo.CurrentCulture) : string.Empty;

    public bool HasMatchText => MatchCount is not null;

    /// <summary>"System name starts with "SEP"" - a one-rule group's summary.</summary>
    public static string Describe(NeighbourRule rule) => rule.IsSupported
        ? $"{FieldLabels[Math.Clamp((int)rule.Field, 0, FieldLabels.Count - 1)]} {OperatorLabels[Math.Clamp((int)rule.Operator, 0, OperatorLabels.Count - 1)]} \"{rule.Value}\""
        : $"{rule.FieldName} {rule.OperatorName} \"{rule.Value}\"";

    /// <summary>
    /// False for a rule made by a newer version (a field or test this one
    /// doesn't know): it's shown, kept as written, and can only be removed.
    /// </summary>
    public bool IsSupported => _original.IsSupported;

    public bool IsUnsupported => !IsSupported;

    public string UnsupportedText => $"{_original.FieldName} {_original.OperatorName} \"{_original.Value}\" - made by a newer version, kept as it is.";

    /// <summary>Why a regular expression won't work, as it's typed; null when it's fine.</summary>
    public string? Problem => IsSupported
        && (NeighbourRuleOperator)OperatorIndex == NeighbourRuleOperator.Matches
        && !string.IsNullOrWhiteSpace(Value)
        && Neighbours.RegexProblem(Value.Trim()) is { } problem
            ? "Not a valid regular expression: " + problem
            : null;

    public bool HasProblem => Problem is not null;

    /// <summary>The rule to save - anything it carried that this version doesn't model goes with it.</summary>
    public NeighbourRule ToRule()
    {
        var rule = _original.Clone();
        if (IsSupported)
        {
            rule.Field = (NeighbourRuleField)Math.Clamp(FieldIndex, 0, FieldLabels.Count - 1);
            rule.Operator = (NeighbourRuleOperator)Math.Clamp(OperatorIndex, 0, OperatorLabels.Count - 1);
            rule.Value = Value.Trim();
        }

        return rule;
    }
}

/// <summary>
/// Makes or changes one neighbour group, as desktop's view editor: a name,
/// whether all its rules or any one must match, and the rules. Nothing is
/// saved until Save, which refuses a group with no name or a broken
/// regular expression.
/// </summary>
public sealed partial class NeighbourGroupEditorViewModel : ViewModelBase
{
    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly NeighbourDirectory _neighbours;
    private NeighbourViewDefinition _draft = new() { Name = string.Empty };
    private IReadOnlyList<NeighbourLink>? _links;

    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>0: all rules must match; 1: any one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMatchAll), nameof(IsMatchAny))]
    private int _matchIndex;

    /// <summary>How many neighbours the whole group matches, as it's edited; null until they're known.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GroupMatchText), nameof(HasGroupMatchText))]
    private int? _groupMatchCount;

    public NeighbourGroupEditorViewModel(ISettingsStore settings, INavigationService navigation, IDialogService dialogs, NeighbourDirectory neighbours)
    {
        _settings = settings;
        _navigation = navigation;
        _dialogs = dialogs;
        _neighbours = neighbours;
    }

    public IReadOnlyList<string> MatchChoices { get; } = ["All rules must match", "Any rule can match"];

    /// <summary>The match switch's two sides.</summary>
    public bool IsMatchAll => MatchIndex == 0;

    public bool IsMatchAny => MatchIndex != 0;

    /// <summary>"Matches 30 neighbours".</summary>
    public string GroupMatchText => GroupMatchCount is { } count
        ? string.Create(CultureInfo.CurrentCulture, $"Matches {count:N0} {(count == 1 ? "neighbour" : "neighbours")}")
        : string.Empty;

    public bool HasGroupMatchText => GroupMatchCount is not null;

    public ObservableCollection<NeighbourRuleRow> Rules { get; } = new();

    /// <summary>Whether this is a group already saved (so it can be deleted), not a new one.</summary>
    public bool IsExisting { get; private set; }

    public string Heading => IsExisting ? "Edit neighbourhood" : "New neighbourhood";

    /// <summary>The group <paramref name="id"/>, or a new one.</summary>
    public void Load(string? id)
    {
        var existing = _settings.Current.NeighbourViews.FirstOrDefault(v => v.Id == id);
        IsExisting = existing is not null;
        _draft = existing?.Clone() ?? new NeighbourViewDefinition { Name = string.Empty };

        Name = _draft.Name;
        MatchIndex = _draft.MatchAll ? 0 : 1;
        Rules.Clear();
        foreach (var rule in _draft.Rules)
        {
            Rules.Add(Track(new NeighbourRuleRow(rule)));
        }

        if (Rules.Count == 0)
        {
            AddRule();
        }

        OnPropertyChanged(nameof(IsExisting));
        OnPropertyChanged(nameof(Heading));

        _links = _neighbours.Links;
        Recount();
    }

    /// <summary>Fetches the neighbours if the Neighbours page hasn't, for the counts. Not fatal - the editor works without them.</summary>
    public async Task LoadNeighboursAsync()
    {
        if (_links is not null)
        {
            return;
        }

        try
        {
            _links = await _neighbours.GetAsync();
        }
        catch (Exception ex) when (ex is DesktopNMS.Core.Api.LibreNmsApiException or HttpRequestException or TaskCanceledException)
        {
            return;
        }

        Recount();
    }

    partial void OnMatchIndexChanged(int value) => Recount();

    [RelayCommand]
    private void SetMatchAll() => MatchIndex = 0;

    [RelayCommand]
    private void SetMatchAny() => MatchIndex = 1;

    /// <summary>A rule's field chip.</summary>
    [RelayCommand]
    private async Task ChooseFieldAsync(NeighbourRuleRow? rule)
    {
        if (rule is not null && await _dialogs.ChooseAsync("Field", NeighbourRuleRow.FieldLabels) is { } choice
            && NeighbourRuleRow.FieldLabels.ToList().IndexOf(choice) is var index and >= 0)
        {
            rule.FieldIndex = index;
        }
    }

    /// <summary>A rule's test chip.</summary>
    [RelayCommand]
    private async Task ChooseOperatorAsync(NeighbourRuleRow? rule)
    {
        if (rule is not null && await _dialogs.ChooseAsync("Test", NeighbourRuleRow.OperatorLabels) is { } choice
            && NeighbourRuleRow.OperatorLabels.ToList().IndexOf(choice) is var index and >= 0)
        {
            rule.OperatorIndex = index;
        }
    }

    [RelayCommand]
    private void AddRule()
    {
        Rules.Add(Track(new NeighbourRuleRow(new NeighbourRule())));
        Recount();
    }

    [RelayCommand]
    private void RemoveRule(NeighbourRuleRow? rule)
    {
        if (rule is not null && Rules.Remove(rule))
        {
            Recount();
        }
    }

    /// <summary>Counts again whenever a rule's field, test or value changes.</summary>
    private NeighbourRuleRow Track(NeighbourRuleRow rule)
    {
        rule.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(NeighbourRuleRow.FieldIndex) or nameof(NeighbourRuleRow.OperatorIndex) or nameof(NeighbourRuleRow.Value))
            {
                Recount();
            }
        };
        return rule;
    }

    /// <summary>
    /// Each rule's count on its own, and the group's - by Core's own test, so
    /// they're what the Neighbours page and desktop will list. A rule with no
    /// value yet, or a broken pattern, has no count.
    /// </summary>
    private void Recount()
    {
        if (_links is not { } links)
        {
            return;
        }

        var ready = new List<NeighbourRule>();
        foreach (var row in Rules)
        {
            if (row.IsUnsupported || row.HasProblem || string.IsNullOrWhiteSpace(row.Value))
            {
                row.MatchCount = null;
                continue;
            }

            var rule = row.ToRule();
            ready.Add(rule);
            var alone = new NeighbourViewDefinition { MatchAll = true, Rules = [rule] };
            row.MatchCount = links.Count(l => NeighboursViewModel.InGroup(alone, l));
        }

        var group = new NeighbourViewDefinition { MatchAll = MatchIndex == 0, Rules = ready };
        GroupMatchCount = ready.Count == 0 ? null : links.Count(l => NeighboursViewModel.InGroup(group, l));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Give the neighbourhood a name.";
            return;
        }

        if (Rules.FirstOrDefault(r => r.HasProblem) is { } broken)
        {
            ErrorMessage = broken.Problem;
            return;
        }

        _draft.Name = Name.Trim();
        _draft.MatchAll = MatchIndex == 0;
        _draft.Rules = Rules.Where(r => r.IsUnsupported || !string.IsNullOrWhiteSpace(r.Value)).Select(r => r.ToRule()).ToList();

        var groups = _settings.Current.NeighbourViews;
        var index = groups.FindIndex(v => v.Id == _draft.Id);
        if (index >= 0)
        {
            groups[index] = _draft;
        }
        else
        {
            groups.Add(_draft);
        }

        _settings.Save();
        await _navigation.GoToAsync(Routes.Back);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!IsExisting || !await _dialogs.ConfirmDestructiveAsync("Delete neighbourhood", $"Delete the neighbourhood \"{_draft.Name}\"? It goes from desktop too; nothing changes in LibreNMS. {Confirmations.CannotBeUndone}", "Delete"))
        {
            return;
        }

        _settings.Current.NeighbourViews.RemoveAll(v => v.Id == _draft.Id);
        _settings.Save();
        await _navigation.GoToAsync(Routes.Back);
    }
}
