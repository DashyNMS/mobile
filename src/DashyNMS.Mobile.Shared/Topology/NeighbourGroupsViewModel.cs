using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Topology;

namespace DashyNMS.Mobile.Topology;

/// <summary>One group in the list: its name and what its rules ask for.</summary>
public sealed record NeighbourGroupRow(NeighbourViewDefinition Group)
{
    public string Name => Group.Name;

    /// <summary>"2 rules, all must match".</summary>
    public string Summary => Group.Rules.Count switch
    {
        0 => "No rules yet",
        1 => "1 rule",
        var n => string.Create(CultureInfo.CurrentCulture, $"{n} rules, {(Group.MatchAll ? "all" : "any")} must match"),
    };
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

    public NeighbourGroupsViewModel(ISettingsStore settings, INavigationService navigation, IDialogService dialogs)
    {
        _settings = settings;
        _navigation = navigation;
        _dialogs = dialogs;
        Refresh();
    }

    public ObservableCollection<NeighbourGroupRow> Groups { get; } = new();

    public bool HasNone => Groups.Count == 0;

    /// <summary>Back from the editor, or on opening.</summary>
    public void Refresh()
    {
        Groups.Clear();
        foreach (var group in _settings.Current.NeighbourViews)
        {
            Groups.Add(new NeighbourGroupRow(group));
        }

        OnPropertyChanged(nameof(HasNone));
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
        if (row is null || !await _dialogs.ConfirmAsync("Delete group", $"Delete \"{row.Name}\"? It goes from desktop too.", "Delete", "Cancel"))
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
    private int _fieldIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Problem), nameof(HasProblem))]
    private int _operatorIndex;

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
    private NeighbourViewDefinition _draft = new() { Name = string.Empty };

    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>0: all rules must match; 1: any one.</summary>
    [ObservableProperty]
    private int _matchIndex;

    public NeighbourGroupEditorViewModel(ISettingsStore settings, INavigationService navigation, IDialogService dialogs)
    {
        _settings = settings;
        _navigation = navigation;
        _dialogs = dialogs;
    }

    public IReadOnlyList<string> MatchChoices { get; } = ["All rules must match", "Any rule can match"];

    public ObservableCollection<NeighbourRuleRow> Rules { get; } = new();

    /// <summary>Whether this is a group already saved (so it can be deleted), not a new one.</summary>
    public bool IsExisting { get; private set; }

    public string Heading => IsExisting ? "Edit group" : "New group";

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
            Rules.Add(new NeighbourRuleRow(rule));
        }

        if (Rules.Count == 0)
        {
            AddRule();
        }

        OnPropertyChanged(nameof(IsExisting));
        OnPropertyChanged(nameof(Heading));
    }

    [RelayCommand]
    private void AddRule() => Rules.Add(new NeighbourRuleRow(new NeighbourRule()));

    [RelayCommand]
    private void RemoveRule(NeighbourRuleRow? rule)
    {
        if (rule is not null)
        {
            Rules.Remove(rule);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Give the group a name.";
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
        if (!IsExisting || !await _dialogs.ConfirmAsync("Delete group", $"Delete \"{_draft.Name}\"? It goes from desktop too.", "Delete", "Cancel"))
        {
            return;
        }

        _settings.Current.NeighbourViews.RemoveAll(v => v.Id == _draft.Id);
        _settings.Save();
        await _navigation.GoToAsync(Routes.Back);
    }
}
