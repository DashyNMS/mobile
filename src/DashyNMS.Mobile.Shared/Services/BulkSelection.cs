using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// A list's selection mode for bulk actions (#85): whether it's on, which
/// items (by id) are ticked, and the progress and outcome of acting on them.
/// Shared by Alerts and Devices so both behave alike; each list ticks its own
/// rows from <see cref="Contains"/> when <see cref="Changed"/> says to.
/// </summary>
/// <remarks>
/// Ticks are kept by id, so they survive a refresh that makes new rows; one
/// that's no longer listed simply isn't acted on.
/// </remarks>
public sealed partial class BulkSelection(string noun) : ObservableObject
{
    private readonly HashSet<int> _ids = [];

    /// <summary>Ticking rather than opening: the list shows tick circles and the action bar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOff))]
    private bool _isSelecting;

    /// <summary>"Acknowledging 3 of 14…" while an action runs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAct))]
    private string? _progressText;

    /// <summary>How the last action went: "Acknowledged 12 of 14 alerts. 2 failed: …".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    private string? _resultText;

    /// <summary>The ticks changed: tick or untick the rows.</summary>
    public event EventHandler? Changed;

    public bool IsOff => !IsSelecting;

    public int Count => _ids.Count;

    /// <summary>"3 selected", or "Select alerts" with none yet.</summary>
    public string Summary => Count == 0
        ? $"Select {noun}s"
        : string.Create(CultureInfo.CurrentCulture, $"{Count} selected");

    /// <summary>Something ticked and nothing already running.</summary>
    public bool CanAct => Count > 0 && ProgressText is null;

    public bool HasResult => !string.IsNullOrEmpty(ResultText);

    public bool Contains(int id) => _ids.Contains(id);

    public IReadOnlyCollection<int> Ids => _ids;

    public void Start()
    {
        ResultText = null;
        IsSelecting = true;
    }

    /// <summary>Done: selection mode off, nothing ticked.</summary>
    public void Stop()
    {
        IsSelecting = false;
        _ids.Clear();
        Notify();
    }

    /// <summary>
    /// A row pressed and held: selecting starts with that row ticked - or,
    /// already selecting, it's ticked or unticked as a tap would.
    /// </summary>
    public void Hold(int id)
    {
        if (!IsSelecting)
        {
            Start();
            _ids.Add(id);
            Notify();
            return;
        }

        Toggle(id);
    }

    public void Toggle(int id)
    {
        if (!_ids.Remove(id))
        {
            _ids.Add(id);
        }

        Notify();
    }

    /// <summary>
    /// "Select all" for what the list shows now - or, when that's all ticked
    /// already, none of it.
    /// </summary>
    public void ToggleAll(IReadOnlyCollection<int> shown)
    {
        if (shown.Count > 0 && shown.All(_ids.Contains))
        {
            _ids.ExceptWith(shown);
        }
        else
        {
            _ids.UnionWith(shown);
        }

        Notify();
    }

    /// <summary>"Select none" when everything shown is ticked, "Select all" otherwise.</summary>
    public string ToggleAllText(IReadOnlyCollection<int> shown) =>
        shown.Count > 0 && shown.All(_ids.Contains) ? "Select none" : "Select all";

    /// <summary>
    /// Runs <paramref name="action"/> over <paramref name="items"/> with
    /// progress ("Acknowledging 3 of 14…"), then says how it went. Only what
    /// failed stays ticked, to try again; with nothing failed, selection ends.
    /// </summary>
    /// <param name="doing">"Acknowledging".</param>
    /// <param name="done">"Acknowledged".</param>
    public async Task<BulkResult<T>> RunAsync<T>(
        IReadOnlyList<T> items,
        string doing,
        string done,
        Func<T, int> id,
        Func<T, string> name,
        Func<T, Task> action)
    {
        ResultText = null;
        ProgressText = Progress(doing, 0, items.Count);
        BulkResult<T> result;
        try
        {
            result = await BulkRun.RunAsync(items, name, action, n => ProgressText = Progress(doing, n, items.Count));
        }
        finally
        {
            ProgressText = null;
        }

        ResultText = result.Describe(done, noun);
        if (result.AllSucceeded)
        {
            IsSelecting = false;
            _ids.Clear();
        }
        else
        {
            // Just the failures, so "try again" is one tap - not the ones
            // that didn't apply (already acknowledged, say).
            var failed = items.Except(result.Succeeded).Select(id).ToList();
            _ids.Clear();
            _ids.UnionWith(failed);
        }

        Notify();
        return result;
    }

    /// <summary>Says something without running anything: "None of those are active."</summary>
    public void Report(string text) => ResultText = text;

    private void Notify()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(CanAct));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Progress(string doing, int done, int total) =>
        string.Create(CultureInfo.CurrentCulture, $"{doing} {done} of {total}…");
}
