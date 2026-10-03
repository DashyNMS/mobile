using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Topology;

/// <summary>
/// Every CDP/LLDP link as the Neighbours page last fetched it, kept so the
/// Neighbourhoods list and editor can say how many each group and rule
/// matches (#121) without fetching every link and port again.
/// </summary>
public sealed class NeighbourDirectory
{
    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;

    public NeighbourDirectory(ILibreNmsClient client, ISettingsStore settings)
    {
        _client = client;
        _settings = settings;
    }

    /// <summary>The links from the last fetch, or null before there's been one.</summary>
    public IReadOnlyList<NeighbourLink>? Links { get; private set; }

    /// <summary>Fetches the links, devices and ports afresh.</summary>
    public async Task<IReadOnlyList<NeighbourLink>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var linksTask = _client.Links.ListAllAsync(cancellationToken);
        var devicesTask = _client.Devices.ListAsync(cancellationToken);
        var portsTask = _client.Ports.ListAllStatusAsync(cancellationToken);
        await Task.WhenAll(linksTask, devicesTask, portsTask);

        return Links = NeighboursViewModel.Build(linksTask.Result, devicesTask.Result, portsTask.Result, _settings.Current.DeviceNameStyle);
    }

    /// <summary>The last fetch's links, fetching them if there hasn't been one.</summary>
    public Task<IReadOnlyList<NeighbourLink>> GetAsync(CancellationToken cancellationToken = default) =>
        Links is { } links ? Task.FromResult(links) : LoadAsync(cancellationToken);
}
