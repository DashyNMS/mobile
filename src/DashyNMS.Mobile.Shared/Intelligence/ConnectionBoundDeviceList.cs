using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// The client handed to the shared device search (#596): the device list only, over a
/// transport of its own that can only ever dial the connection the chat captured.
/// </summary>
/// <remarks>
/// <para>
/// Mobile's session client is one object whose connection changes on sign-in,
/// reconnect, certificate trust and sign-out. Checking "is it still current?" before
/// each request leaves a window: a switch between the check and the send dials the
/// new server with the old chat's request. The shared search warns about exactly this
/// (its <c>HostDeviceSearch.Create</c> asks for a client that stays bound even after
/// retirement). So this client never touches the session's transport. It opens its
/// own, configured once with the captured connection object and never reconfigured,
/// and builds Core's real device API on it. A switch cannot redirect it; at worst a
/// request already sent reaches the old server, and its reply is discarded.
/// </para>
/// <para>
/// The transport is opened on the first search, not when the chat starts, so a chat
/// that never searches makes no connection. <see cref="Dispose"/> retires the client:
/// later calls throw <see cref="RetiredException"/> and the transport is closed.
/// </para>
/// <para>
/// Everything except <see cref="IDevicesApi.ListAsync"/> is refused before transport,
/// including the other device endpoints (maintenance, discovery, rename, delete), so a
/// change upstream (MAC search through ARP, group scopes, per-device reads) cannot
/// quietly widen what the chat reads or let it write. The session-wide controls are
/// never the search's to touch.
/// </para>
/// </remarks>
internal sealed class ConnectionBoundDeviceList : ILibreNmsClient, IDisposable
{
    private readonly LibreNmsConnection connection;
    private readonly bool onBackup;
    private readonly Func<LibreNmsConnection, bool, ILibreNmsTransport> openTransport;
    private readonly ListOnly devices;
    private readonly Lock gate = new();
    private ILibreNmsTransport? transport;
    private bool retired;

    /// <param name="connection">The captured connection; every request uses exactly this object's address, token and trust.</param>
    /// <param name="onBackup">Whether the session was on the backup address at capture, so the first request dials the same one.</param>
    /// <param name="openTransport">Opens a transport for one connection; <see cref="OpenTransport"/> in the app, a synthetic one in tests.</param>
    public ConnectionBoundDeviceList(LibreNmsConnection connection, bool onBackup, Func<LibreNmsConnection, bool, ILibreNmsTransport> openTransport)
    {
        this.connection = connection;
        this.onBackup = onBackup;
        this.openTransport = openTransport;
        devices = new ListOnly(this);
    }

    /// <summary>Thrown instead of reading after retirement; the lookup reports the switch.</summary>
    internal sealed class RetiredException() : OperationCanceledException("The connection changed.");

    /// <summary>Thrown for anything outside the reviewed read-only profile.</summary>
    internal sealed class RefusedException(string member) : NotSupportedException(member + " is not available to Intelligence device search.");

    /// <summary>
    /// The app's transport for one connection: Core's own HTTP transport, so URLs,
    /// certificate pinning, redirects, retries and failover are Core's, but private to
    /// this chat. Logging is off, as the lookups log nothing about the network.
    /// </summary>
    /// <param name="shared">
    /// The session's transport, read only for its request timing hook when each transport
    /// opens, so these requests are measured like any other. Never used to send.
    /// </param>
    internal static Func<LibreNmsConnection, bool, ILibreNmsTransport> OpenTransport(LibreNmsTransport? shared = null) =>
        (connection, onBackup) =>
        {
            var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance) { RequestObserver = shared?.RequestObserver };
            transport.Configure(connection, onBackup);
            return transport;
        };

    /// <summary>The connection the transport was opened for; for tests and diagnostics only.</summary>
    internal LibreNmsConnection BoundConnection => connection;

    /// <summary>Whether a transport has been opened; for tests.</summary>
    internal bool IsOpen
    {
        get { lock (gate) return transport is not null; }
    }

    public IDevicesApi Devices
    {
        get
        {
            lock (gate)
                return retired ? throw new RetiredException() : devices;
        }
    }

    /// <summary>
    /// The devices from the latest list this client fetched, by id: the raw records
    /// behind the shared summaries, so a row can say "Unknown" when LibreNMS didn't
    /// report a state rather than the summary's "down".
    /// </summary>
    internal IReadOnlyDictionary<int, Device> Latest => Volatile.Read(ref latest);
    private IReadOnlyDictionary<int, Device> latest = new Dictionary<int, Device>();

    private async Task<IReadOnlyList<Device>> ListAsync(CancellationToken cancellationToken)
    {
        ILibreNmsTransport open;
        lock (gate)
        {
            if (retired) throw new RetiredException();
            open = transport ??= openTransport(connection, onBackup);
        }

        // Defence in depth: the transport must be the captured connection's, never a shared one.
        if (!ReferenceEquals(open.Connection, connection) && open.Connection is not null)
            throw new RetiredException();
        var list = await new LibreNmsResourceApis(open).Devices.ListAsync(cancellationToken).ConfigureAwait(false);
        var byId = new Dictionary<int, Device>(list.Count);
        foreach (var device in list) byId.TryAdd(device.DeviceId, device);
        Volatile.Write(ref latest, byId);
        return list;
    }

    /// <summary>Retires the client and closes its transport. Safe to call more than once.</summary>
    public void Dispose()
    {
        ILibreNmsTransport? closing;
        lock (gate)
        {
            retired = true;
            closing = transport;
            transport = null;
        }
        (closing as IDisposable)?.Dispose();
    }

    /// <summary>The device list and nothing else of <see cref="IDevicesApi"/>.</summary>
    private sealed class ListOnly(ConnectionBoundDeviceList owner) : IDevicesApi
    {
        public Task<IReadOnlyList<Device>> ListAsync(CancellationToken cancellationToken = default) => owner.ListAsync(cancellationToken);

        public Task<Device?> GetAsync(string idOrHostname, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetAsync));
        public Task<bool> IsUnderMaintenanceAsync(int deviceId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(IsUnderMaintenanceAsync));
        public Task<string> ScheduleMaintenanceAsync(int deviceId, DeviceMaintenanceRequest request, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(ScheduleMaintenanceAsync));
        public Task<IReadOnlyList<AvailabilityWindow>> GetAvailabilityAsync(int deviceId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetAvailabilityAsync));
        public Task<IReadOnlyList<DeviceOutage>> GetOutagesAsync(int deviceId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetOutagesAsync));
        public Task<IReadOnlyList<InventoryEntry>> GetInventoryAsync(int deviceId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetInventoryAsync));
        public Task<IReadOnlyList<WirelessSensor>> GetWirelessSensorsAsync(int deviceId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetWirelessSensorsAsync));
        public Task<string> DiscoverAsync(int deviceId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(DiscoverAsync));
        public Task<AddDeviceResult> AddAsync(AddDeviceRequest request, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(AddAsync));
        public Task UpdateFieldsAsync(int deviceId, IReadOnlyDictionary<string, string?> fields, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(UpdateFieldsAsync));
        public Task RenameAsync(int deviceId, string newHostname, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(RenameAsync));
        public Task<string> DeleteAsync(int deviceId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(DeleteAsync));
    }

    public IAlertsApi Alerts => throw new RefusedException(nameof(Alerts));
    public IAlertRulesApi Rules => throw new RefusedException(nameof(Rules));
    public IAlertTemplatesApi AlertTemplates => throw new RefusedException(nameof(AlertTemplates));
    public ISensorsApi Sensors => throw new RefusedException(nameof(Sensors));
    public IPortsApi Ports => throw new RefusedException(nameof(Ports));
    public ILinksApi Links => throw new RefusedException(nameof(Links));
    public IDeviceHealthApi Health => throw new RefusedException(nameof(Health));
    public IFdbApi Fdb => throw new RefusedException(nameof(Fdb));
    public IArpApi Arp => throw new RefusedException(nameof(Arp));
    public IVlansApi Vlans => throw new RefusedException(nameof(Vlans));
    public IDeviceGroupsApi DeviceGroups => throw new RefusedException(nameof(DeviceGroups));
    public ILocationsApi Locations => throw new RefusedException(nameof(Locations));
    public IRoutingApi Routing => throw new RefusedException(nameof(Routing));
    public IPollerGroupsApi PollerGroups => throw new RefusedException(nameof(PollerGroups));
    public ILogsApi Logs => throw new RefusedException(nameof(Logs));
    public ISystemApi System => throw new RefusedException(nameof(System));
    public IGraphsApi Graphs => throw new RefusedException(nameof(Graphs));

    public LibreNmsConnection? Connection => throw new RefusedException(nameof(Connection));
    public bool IsConnected => throw new RefusedException(nameof(IsConnected));
    public ServerFailover Failover => throw new RefusedException(nameof(Failover));
    public ApiPermissions Permissions => throw new RefusedException(nameof(Permissions));

    public event EventHandler<CertificateDetails>? CertificateRejected
    {
        add => throw new RefusedException(nameof(CertificateRejected));
        remove { }
    }

    public void Connect(LibreNmsConnection connection, bool startOnBackup = false) => throw new RefusedException(nameof(Connect));
    public void Disconnect() => throw new RefusedException(nameof(Disconnect));
    public void TrustCertificate(string fingerprint) => throw new RefusedException(nameof(TrustCertificate));
    public void SetTimeout(int timeoutSeconds) => throw new RefusedException(nameof(SetTimeout));

    public Task<ConnectionTestResult> TestAsync(LibreNmsConnection connection, CancellationToken cancellationToken = default)
        => throw new RefusedException(nameof(TestAsync));
}
