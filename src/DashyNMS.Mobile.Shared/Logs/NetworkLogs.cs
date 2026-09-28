using System.Globalization;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Logs;

/// <summary>LibreNMS's event and alert logs for the whole network, newest first.</summary>
public interface INetworkLogs
{
    Task<IReadOnlyList<EventLogEntry>> EventLogAsync(int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertLogEntry>> AlertLogAsync(int limit, CancellationToken cancellationToken = default);
}

/// <summary>
/// The same endpoints as Core's per-device <see cref="ILogsApi"/>, with the
/// device left out - LibreNMS's route takes it as optional, and then lists
/// every device's entries.
/// </summary>
/// <remarks>
/// Core only offers the per-device form, so this goes through Core's own
/// transport (auth, failover, retries and all). It belongs in Core's
/// <see cref="ILogsApi"/> upstream; then this can go. As Core notes, the
/// endpoint ignores an offset, so "more" means asking again with a larger limit.
/// </remarks>
public sealed class NetworkLogs : INetworkLogs
{
    private readonly ILibreNmsTransport _transport;

    public NetworkLogs(ILibreNmsTransport transport) => _transport = transport;

    public Task<IReadOnlyList<EventLogEntry>> EventLogAsync(int limit, CancellationToken cancellationToken = default) =>
        _transport.GetCollectionAsync<EventLogEntry>(Url("eventlog", limit), "logs", cancellationToken);

    public Task<IReadOnlyList<AlertLogEntry>> AlertLogAsync(int limit, CancellationToken cancellationToken = default) =>
        _transport.GetCollectionAsync<AlertLogEntry>(Url("alertlog", limit), "logs", cancellationToken);

    internal static string Url(string log, int limit) =>
        string.Create(CultureInfo.InvariantCulture, $"logs/{log}?limit={Math.Max(1, limit)}&sortorder=DESC");
}
