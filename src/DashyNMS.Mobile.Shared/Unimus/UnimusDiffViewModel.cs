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
    private int _generation;

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

    public BulkObservableCollection<UnimusDiffItem> Lines { get; } = new();

    public string OriginalIdentity => Identity("Original", _request?.Original);
    public string RevisedIdentity => Identity("Revised", _request?.Revised);

    private static string Identity(string side, UnimusBackup? backup) => backup is null ? string.Empty
        : $"{side} · backup {backup.Id} · {(backup.ValidSinceUtc is { } date ? date.ToLocalTime().ToString("d MMM yyyy HH:mm:ss zzz") : "Unknown date")}";

    /// <summary>"3 Sep 2026 18:01 → 18 Sep 2026 18:01".</summary>
    public string DeviceName => _request?.DeviceName ?? string.Empty;

    /// <summary>"+12 −3", or that the two are the same.</summary>
    public string SummaryText => _result switch
    {
        null => string.Empty,
        { HasDifferences: false } => "No differences",
        { } r => $"+{r.AddedCount} −{r.RemovedCount}",
    };

    public bool HasDifferences => !IsBusy && !HasError && _result?.HasDifferences == true;

    public bool IsEmpty => !IsBusy && _result is { HasDifferences: false } && !HasError;

    public async Task LoadAsync(UnimusDiffRequest request)
    {
        var generation = ++_generation;
        _request = request;
        _diff = null;
        _result = null;
        Lines.Clear();
        _expanded.Clear();
        IsBusy = true;
        RetryCommand.NotifyCanExecuteChanged();
        ErrorMessage = null;
        NotifyResult();
        OnPropertyChanged(nameof(DeviceName));
        OnPropertyChanged(nameof(OriginalIdentity));
        OnPropertyChanged(nameof(RevisedIdentity));
        try
        {
            var diff = await _unimus.GetDiffAsync(request.Original.Id, request.Revised.Id);
            if (generation != _generation) return;
            _diff = diff ?? throw new InvalidOperationException("Comparison unavailable. Retry to load these backups.");
            Render();
        }
        catch (Exception ex)
        {
            if (generation == _generation)
                ErrorMessage = ex is UnimusApiException apiError ? apiError.ToUserMessage()
                    : "Couldn't compare these backups. Retry or return to backup history.";
        }
        finally
        {
            if (generation == _generation)
            {
                IsBusy = false;
                NotifyResult();
                RetryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task RetryAsync() => _request is { } request ? LoadAsync(request) : Task.CompletedTask;

    partial void OnOnlyChangedChanged(bool value) => Render();

    /// <summary>Folded runs are numbered for one blank-line setting, so they unfold again when it changes.</summary>
    partial void OnIgnoreEmptyLinesChanged(bool value)
    {
        _expanded.Clear();
        Render();
    }

    /// <summary>A folded run, tapped: its lines shown in place.</summary>
    [RelayCommand]
    private void Expand(UnimusDiffItem? line)
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
        Lines.ReplaceAll(_result.Rows.Select(r => new UnimusDiffItem(r)).ToList());
        NotifyResult();
    }

    private void NotifyResult()
    {
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
        if (!HasDifferences || _diff is null || _request is null)
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
