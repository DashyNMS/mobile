using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Unimus;

/// <summary>
/// What changed between two config backups, from Unimus's own diff and
/// rendered by Core's <see cref="UnimusDiffBuilder"/>, as desktop's: removed
/// lines red with "-", added green with "+", and long unchanged stretches
/// folded away until tapped.
/// </summary>
public sealed partial class UnimusDiffViewModel : ViewModelBase
{
    private readonly IUnimusApi _unimus;
    private readonly IShareService _share;
    private readonly HashSet<int> _expanded = [];
    private UnimusDiffRequest? _request;
    private UnimusBackupDiff? _diff;
    private UnimusDiffResult? _result;

    /// <summary>On by default, as Unimus's and desktop's diffs start.</summary>
    [ObservableProperty]
    private bool _onlyChanged = true;

    [ObservableProperty]
    private bool _ignoreEmptyLines;

    public UnimusDiffViewModel(IUnimusApi unimus, IShareService share)
    {
        _unimus = unimus;
        _share = share;
    }

    public BulkObservableCollection<UnimusLine> Lines { get; } = new();

    /// <summary>"3 Sep 2026 18:01 → 18 Sep 2026 18:01".</summary>
    public string Subtitle => _request is null
        ? string.Empty
        : $"{UnimusText.Date(_request.Original.ValidSinceUtc)} → {UnimusText.Date(_request.Revised.ValidSinceUtc)}";

    public string DeviceName => _request?.DeviceName ?? string.Empty;

    /// <summary>"+12 −3", or that the two are the same.</summary>
    public string SummaryText => _result switch
    {
        null => string.Empty,
        { HasDifferences: false } => "No differences",
        { } r => $"+{r.AddedCount} −{r.RemovedCount}",
    };

    public bool HasDifferences => _result?.HasDifferences == true;

    public bool IsEmpty => !IsBusy && _result is { HasDifferences: false } && !HasError;

    public Task LoadAsync(UnimusDiffRequest request)
    {
        _request = request;
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(DeviceName));

        return RunAsync(async () =>
        {
            _diff = await _unimus.GetDiffAsync(request.Original.Id, request.Revised.Id)
                ?? new UnimusBackupDiff();
            _expanded.Clear();
            Render();
        });
    }

    partial void OnOnlyChangedChanged(bool value) => Render();

    /// <summary>Folded runs are numbered for one blank-line setting, so they unfold again when it changes.</summary>
    partial void OnIgnoreEmptyLinesChanged(bool value)
    {
        _expanded.Clear();
        Render();
    }

    /// <summary>A folded run, tapped: its lines shown in place.</summary>
    [RelayCommand]
    private void Expand(UnimusLine? line)
    {
        if (line is { IsHidden: true } && _expanded.Add(line.Row.HiddenStart))
        {
            Render();
        }
    }

    private void Render()
    {
        if (_diff is null)
        {
            return;
        }

        _result = UnimusDiffBuilder.Build(_diff, new UnimusDiffOptions
        {
            OnlyChanged = OnlyChanged,
            IgnoreEmptyLines = IgnoreEmptyLines,
            ExpandedHiddenStarts = _expanded,
        });
        Lines.ReplaceAll(_result.Rows.Select(r => new UnimusLine(r)).ToList());
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasDifferences));
        OnPropertyChanged(nameof(IsEmpty));
        ShareCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// A standard unified diff of the whole change - never the folded view -
    /// that git, patch and diff viewers all read, through the share sheet.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasDifferences))]
    private Task ShareAsync()
    {
        if (_diff is null || _request is null)
        {
            return Task.CompletedTask;
        }

        var text = UnimusExport.ToUnifiedDiff(
            _diff,
            UnimusExport.FileHeader(_request.DeviceName, _request.Original.ValidSinceUtc),
            UnimusExport.FileHeader(_request.DeviceName, _request.Revised.ValidSinceUtc));
        var name = UnimusExport.FileNameFor(_request.DeviceName, _request.Revised.ValidSinceUtc?.ToLocalTime(), ".diff");
        return _share.ShareTextFileAsync(name, text, "text/x-diff", "Share changes");
    }
}
