using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Devices;
using DesktopNMS.Core.Graylog;
using DesktopNMS.Core.Logs;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// The Event log and Graylog cards (DashyNMS/desktop#202, #203): the newest
/// entries across the network, fetched only while such a card shows. A tap
/// opens the entry's own page, with its device; See all opens Logs or
/// Graylog on the card's filter.
/// </summary>
public sealed partial class DashboardViewModel
{
    private readonly GraylogSetup? _graylogSetup;
    private readonly IGraylogApi? _graylog;

    /// <summary>Every event type seen so far: the type chip's choices only ever grow, as desktop's.</summary>
    private readonly SortedSet<string> _eventTypes = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<GraylogStream>? _graylogStreams;
    private IReadOnlyList<Device> _logDevices = [];

    /// <summary>Awaited by tests: the log cards, after a chip changed what they show.</summary>
    internal Task LogsLoaded { get; private set; } = Task.CompletedTask;

    private Task LoadLogsAsync(IReadOnlyList<Device> devices)
    {
        _logDevices = devices;
        return Task.WhenAll(
            Shows(DashboardLayout.EventLog) ? LoadEventLogAsync() : Task.CompletedTask,
            Shows(DashboardLayout.Graylog) ? LoadGraylogAsync() : Task.CompletedTask);
    }

    /// <summary>One event log request for every Event log card, enough for the one wanting most.</summary>
    private async Task LoadEventLogAsync()
    {
        var cards = CardsOf(DashboardLayout.EventLog);
        if (cards.Count == 0)
        {
            return;
        }

        try
        {
            var limit = cards.Max(c => EventLogFeed.FetchLimit(LogCards.CountOf(c.Widget), c.Widget.LogType, c.Widget.LogSearch));
            var entries = await _client.Logs.ListEventLogAsync(null, limit);
            _eventTypes.UnionWith(EventLogFeed.Types(entries));

            var utc = _settings.Current.ServerTimestampsAreUtc;
            var now = DateTime.Now;
            foreach (var card in cards)
            {
                card.ShowEvents(EventLogFeed.Filter(entries, card.Widget.LogType, card.Widget.LogSearch, LogCards.CountOf(card.Widget))
                    .Select(e => LogEntryItem.ForEvent(e, EventDeviceName(e), utc, now))
                    .ToList());
            }
        }
        catch (LibreNmsApiException)
        {
            // The last rows stay under the error, as on desktop.
            foreach (var card in cards)
            {
                card.LogStatus = "Couldn't reach LibreNMS for the event log.";
            }
        }
    }

    private async Task LoadGraylogAsync()
    {
        var cards = CardsOf(DashboardLayout.Graylog);
        if (cards.Count == 0)
        {
            return;
        }

        // Not set up: say how, rather than showing an error or old messages.
        if (_graylogSetup is null || _graylog is null || !await _graylogSetup.EnsureConfiguredAsync())
        {
            foreach (var card in cards)
            {
                card.ShowGraylog([], configured: false);
            }

            return;
        }

        await LoadGraylogStreamsAsync();
        var zone = GraylogQuery.FindTimeZone(_settings.Current.Graylog.Timezone);
        await Task.WhenAll(cards.Select(async card =>
        {
            var widget = card.Widget;
            try
            {
                var query = string.IsNullOrWhiteSpace(widget.LogSearch) ? "*" : widget.LogSearch.Trim();
                var result = await _graylog.SearchAsync(query, LogCards.RangeOf(widget), LogCards.CountOf(widget), 0,
                    "timestamp:desc", GraylogQuery.StreamFilter(widget.GraylogStreamId));
                card.ShowGraylog(result.Messages.Select(m => new GraylogMessageItem(m.Message, zone, GraylogDevice)).ToList(), configured: true);
            }
            catch (GraylogApiException)
            {
                card.LogStatus = "Couldn't reach Graylog.";
            }
        }));
    }

    /// <summary>For the stream chips' names; a stream this account can't see any more keeps its place.</summary>
    private async Task LoadGraylogStreamsAsync()
    {
        if (_graylogStreams is null && _graylog is not null)
        {
            try
            {
                _graylogStreams = (await _graylog.GetStreamsAsync())
                    .Where(s => !s.Disabled)
                    .OrderBy(s => s.DisplayText, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch (GraylogApiException)
            {
                // The search reports the problem; the streams are tried again next time.
            }
        }

        foreach (var card in CardsOf(DashboardLayout.Graylog).Where(c => !string.IsNullOrWhiteSpace(c.Widget.GraylogStreamId)))
        {
            card.NameStream(_graylogStreams is null ? null
                : _graylogStreams.FirstOrDefault(s => s.Id == card.Widget.GraylogStreamId)?.DisplayText ?? "Unknown stream");
        }
    }

    /// <summary>A message's source or origin as a known device - Core's match, as the Graylog page's.</summary>
    private (string Name, int? DeviceId) GraylogDevice(string? address) =>
        DeviceAddresses.Find(_logDevices, address) is { } device
            ? (new DeviceItem(device, _settings.Current.DeviceNameStyle).Name, device.DeviceId)
            : (address ?? string.Empty, null);

    private string EventDeviceName(EventLogEntry entry) =>
        _devicesById.TryGetValue(entry.DeviceId, out var device) ? new DeviceItem(device, _settings.Current.DeviceNameStyle).Name
        : !string.IsNullOrWhiteSpace(entry.Hostname) ? entry.Hostname!
        : entry.DeviceId > 0 ? $"Device {entry.DeviceId.ToString(CultureInfo.CurrentCulture)}" : "LibreNMS";

    // ------------------------------------------------------------ the chips

    internal async Task ChooseLogCountAsync(DashboardCard card)
    {
        var labels = LogCards.CountChoices.Select(c => $"Latest {c}").ToList();
        if (await ChooseAsync("Show", labels) is { } index)
        {
            card.Widget.LogCount = LogCards.CountChoices[index];
            LogOptionsChanged(card);
        }
    }

    internal async Task ChooseLogTypeAsync(DashboardCard card)
    {
        var types = _eventTypes.ToList();
        if (!string.IsNullOrWhiteSpace(card.Widget.LogType) && !types.Contains(card.Widget.LogType, StringComparer.OrdinalIgnoreCase))
        {
            types.Add(card.Widget.LogType);
        }

        if (await ChooseAsync("Event type", [LogCards.AllTypes, .. types]) is { } index)
        {
            card.Widget.LogType = index == 0 ? null : types[index - 1];
            LogOptionsChanged(card);
        }
    }

    internal async Task ChooseGraylogStreamAsync(DashboardCard card)
    {
        var streams = _graylogStreams ?? [];
        if (await ChooseAsync("Stream", [LogCards.AllStreams, .. streams.Select(s => s.DisplayText)]) is { } index)
        {
            card.Widget.GraylogStreamId = index == 0 ? null : streams[index - 1].Id;
            card.NameStream(index == 0 ? null : streams[index - 1].DisplayText);
            LogOptionsChanged(card);
        }
    }

    internal async Task ChooseGraylogRangeAsync(DashboardCard card)
    {
        if (await ChooseAsync("Time range", LogCards.RangeChoices.Select(r => $"Last {r.Label}").ToList()) is { } index)
        {
            card.Widget.GraylogRangeSeconds = LogCards.RangeChoices[index].Seconds;
            LogOptionsChanged(card);
        }
    }

    private async Task<int?> ChooseAsync(string title, IReadOnlyList<string> labels)
    {
        if (_dialogs is null || await _dialogs.ChooseAsync(title, labels) is not { } choice)
        {
            return null;
        }

        var index = labels.ToList().IndexOf(choice);
        return index >= 0 ? index : null;
    }

    /// <summary>Saved to the widget, so desktop shows the same; then that kind of card fetched again.</summary>
    private void LogOptionsChanged(DashboardCard card)
    {
        _settings.Save();
        card.RefreshLogOptions();
        LogsLoaded = card.IsEventLog ? LoadEventLogAsync() : LoadGraylogAsync();
    }

    // ------------------------------------------------------------ opening

    /// <summary>The entry's own page, with its device link - as the Logs tab opens it.</summary>
    [RelayCommand]
    private Task OpenLogEntryAsync(LogEntryItem? item) => item is null
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.LogEntry, new Dictionary<string, object> { [Routes.LogEntryParameter] = item });

    [RelayCommand]
    private Task OpenGraylogMessageAsync(GraylogMessageItem? item) => item is null
        ? Task.CompletedTask
        : _navigation.GoToAsync(Routes.GraylogMessage, new Dictionary<string, object> { [Routes.GraylogMessageParameter] = item });

    /// <summary>Logs or Graylog on the card's filter - or, with Graylog not set up, its settings.</summary>
    internal Task SeeAllLogsAsync(DashboardCard card)
    {
        if (card.IsEventLog)
        {
            return _navigation.GoToAsync(Routes.Logs, new Dictionary<string, object>
            {
                [Routes.LogTypeParameter] = card.Widget.LogType ?? string.Empty,
            });
        }

        return card.NeedsGraylogSetUp
            ? _navigation.GoToAsync(Routes.GraylogSettings)
            : _navigation.GoToAsync(Routes.Graylog, new Dictionary<string, object>
            {
                [Routes.GraylogStreamParameter] = card.Widget.GraylogStreamId ?? string.Empty,
                [Routes.GraylogRangeParameter] = LogCards.RangeOf(card.Widget),
            });
    }
}
