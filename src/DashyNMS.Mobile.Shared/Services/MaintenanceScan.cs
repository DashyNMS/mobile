using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// Which devices are in a maintenance window, shared by the Devices tab and
/// the dashboard's device counts so they agree and don't each ask.
/// </summary>
/// <remarks>
/// LibreNMS has no bulk endpoint, so - as on desktop - it's one request per
/// device, capped at <see cref="MaxConcurrentChecks"/> at once, and at most
/// every <see cref="RescanInterval"/> for the same server: a scan asked for
/// sooner gets the last result (or joins the one still running).
/// </remarks>
public sealed class MaintenanceScan
{
    internal const int MaxConcurrentChecks = 16;
    internal static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(60);

    private readonly ILibreNmsClient _client;
    private readonly TimeProvider _time;
    private DateTimeOffset? _lastScan;
    private Uri? _lastServer;
    private Task<IReadOnlySet<int>> _scan = Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());

    public MaintenanceScan(ILibreNmsClient client, TimeProvider time)
    {
        _client = client;
        _time = time;
    }

    /// <summary>
    /// The ids of <paramref name="devices"/> under maintenance. Disabled
    /// devices aren't polled, so maintenance means nothing for them and
    /// they're never asked about.
    /// </summary>
    public Task<IReadOnlySet<int>> ScanAsync(IReadOnlyList<Device> devices)
    {
        var now = _time.GetUtcNow();
        var server = _client.Connection?.WebRoot;
        if (_lastScan is { } last && now - last < RescanInterval && server == _lastServer)
        {
            return _scan;
        }

        _lastScan = now;
        _lastServer = server;
        return _scan = RunAsync(devices.Where(d => !d.Disabled).Select(d => d.DeviceId).ToList());
    }

    private async Task<IReadOnlySet<int>> RunAsync(IReadOnlyList<int> deviceIds)
    {
        var found = new HashSet<int>();
        using var gate = new SemaphoreSlim(MaxConcurrentChecks);

        await Task.WhenAll(deviceIds.Select(async id =>
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (await _client.Devices.IsUnderMaintenanceAsync(id).ConfigureAwait(false))
                {
                    lock (found)
                    {
                        found.Add(id);
                    }
                }
            }
            catch (Exception)
            {
                // One device failing to answer doesn't change anyone else's state.
            }
            finally
            {
                gate.Release();
            }
        })).ConfigureAwait(false);

        return found;
    }
}
