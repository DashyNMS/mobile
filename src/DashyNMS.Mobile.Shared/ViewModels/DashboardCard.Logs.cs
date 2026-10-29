using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// An Event log or Graylog card (DashyNMS/desktop#202, #203): the newest
/// entries across the network, with desktop's type, stream, range and count
/// as the title's chips. If a refresh fails, the last rows stay under the error.
/// </summary>
public sealed partial class DashboardCard
{
    public bool IsEventLog => Type == DashboardLayout.EventLog;

    public BulkObservableCollection<LogEntryItem> EventRows { get; } = new();

    public BulkObservableCollection<GraylogMessageItem> GraylogRows { get; } = new();

    /// <summary>"No events yet.", "Couldn't reach Graylog." or how to set it up; null with rows to show.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogStatus))]
    private string? _logStatus;

    public bool HasLogStatus => LogStatus is not null;

    /// <summary>Graylog isn't set up: the card offers Settings rather than See all.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SeeAllLogsText))]
    private bool _needsGraylogSetUp;

    /// <summary>The stream's name, once the streams have loaded; "Stream" until then.</summary>
    private string? _streamName;

    /// <summary>"Latest 10 ▾".</summary>
    public string LogCountText => $"Latest {LogCards.CountOf(Widget)} ▾";

    /// <summary>"All types ▾", or the one type shown.</summary>
    public string LogTypeText => (string.IsNullOrWhiteSpace(Widget.LogType) ? LogCards.AllTypes : Widget.LogType) + " ▾";

    public string GraylogStreamText => (string.IsNullOrWhiteSpace(Widget.GraylogStreamId) ? LogCards.AllStreams : _streamName ?? "Stream") + " ▾";

    public string GraylogRangeText => LogCards.RangeLabel(Widget) + " ▾";

    public string SeeAllLogsText => IsEventLog ? "See all events" : NeedsGraylogSetUp ? "Set up Graylog" : "See all messages";

    [RelayCommand]
    private Task ChooseLogCountAsync() => Dashboard.ChooseLogCountAsync(this);

    [RelayCommand]
    private Task ChooseLogTypeAsync() => Dashboard.ChooseLogTypeAsync(this);

    [RelayCommand]
    private Task ChooseGraylogStreamAsync() => Dashboard.ChooseGraylogStreamAsync(this);

    [RelayCommand]
    private Task ChooseGraylogRangeAsync() => Dashboard.ChooseGraylogRangeAsync(this);

    [RelayCommand]
    private Task SeeAllLogsAsync() => Dashboard.SeeAllLogsAsync(this);

    internal void ShowEvents(IReadOnlyList<LogEntryItem> rows)
    {
        EventRows.ReplaceAll(rows);
        LogStatus = rows.Count == 0 ? LogCards.EventsEmptyText(Widget) : null;
    }

    internal void ShowGraylog(IReadOnlyList<GraylogMessageItem> rows, bool configured)
    {
        GraylogRows.ReplaceAll(rows);
        NeedsGraylogSetUp = !configured;
        LogStatus = !configured ? LogCards.NotSetUp : rows.Count == 0 ? LogCards.GraylogEmptyText(Widget) : null;
    }

    internal void NameStream(string? name)
    {
        _streamName = name;
        OnPropertyChanged(nameof(GraylogStreamText));
    }

    internal void RefreshLogOptions()
    {
        OnPropertyChanged(nameof(LogCountText));
        OnPropertyChanged(nameof(LogTypeText));
        OnPropertyChanged(nameof(GraylogStreamText));
        OnPropertyChanged(nameof(GraylogRangeText));
    }
}
