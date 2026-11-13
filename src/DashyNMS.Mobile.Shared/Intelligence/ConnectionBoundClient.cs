using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Security;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// The client handed to shared operations: reviewed search and open-alert reads,
/// or the overview profile's device, open-alert, port-status and system reads.
/// Each instance owns a transport bound to the connection the chat captured.
/// </summary>
/// <remarks>
/// <para>
/// Mobile's session client is one object whose connection changes on sign-in,
/// reconnect, certificate trust and sign-out. Checking "is it still current?" before
/// each request leaves a window: a switch between the check and the send dials the
/// new server with the old chat's request. The shared operations warn about exactly this
/// (<c>HostDeviceSearch.Create</c> and <c>HostOpenAlerts.Create</c> ask for a client that
/// stays bound even after retirement). So this client never touches the session's transport.
/// It sends through one opened for the captured connection alone and builds Core's real
/// device and alert APIs on it. A switch cannot redirect it; at worst a request already sent reaches the old
/// server, and its reply is discarded.
/// </para>
/// <para>
/// In the app <see cref="IntelligenceDeviceTransports"/> opens that transport and keeps
/// it in step with the session (timeout, trusted certificates, backup address and the
/// certificate prompt) without ever changing its server or token.
/// </para>
/// <para>
/// The transport is opened on the first lookup that needs it, not when the chat starts,
/// so a chat that never searches or asks about alerts makes no connection. <see cref="Dispose"/> retires the client:
/// later calls throw <see cref="RetiredException"/> and the transport is closed.
/// </para>
/// <para>
/// Search allows the device list, MAC-specific ARP search, group catalogue and one
/// group's membership, plus the open alerts query. Location filtering uses the device
/// list. Overview uses a separate instance permitting only the device and open-alert
/// lists, fleet port status and system information; it refuses ARP and groups.
/// All other reads, writes and session controls throw before transport.
/// </para>
/// </remarks>
internal sealed class ConnectionBoundClient : ILibreNmsClient, IDisposable
{
    private readonly SessionScope scope;
    private readonly Func<SessionScope, ILibreNmsTransport> openTransport;
    private readonly ListOnly devices;
    private readonly OpenOnly alerts;
    private readonly bool overview;
    private readonly Lock gate = new();
    private ILibreNmsTransport? transport;
    private LibreNmsResourceApis? apis;
    private bool retired;

    /// <param name="scope">
    /// The captured connection and its session generation; every request goes to exactly this
    /// connection's address and backup, with its token.
    /// </param>
    /// <param name="openTransport">
    /// Opens a transport for one connection, owned by this client from then on;
    /// <see cref="IntelligenceDeviceTransports.Open"/> in the app, a synthetic one in tests.
    /// </param>
    public ConnectionBoundClient(SessionScope scope, Func<SessionScope, ILibreNmsTransport> openTransport, bool overview = false)
    {
        this.scope = scope;
        this.overview = overview;
        this.openTransport = openTransport;
        devices = new ListOnly(this);
        alerts = new OpenOnly(this);
    }

    /// <summary>Thrown instead of reading after retirement; the lookup reports the switch.</summary>
    internal sealed class RetiredException() : OperationCanceledException("The connection changed.");

    /// <summary>Thrown for anything outside the reviewed read-only profile.</summary>
    internal sealed class RefusedException(string member) : NotSupportedException(member + " is not available to Intelligence lookups.");

    /// <summary>The connection the transport was opened for; for tests and diagnostics only.</summary>
    internal LibreNmsConnection BoundConnection => scope.Connection;

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
    /// The resource APIs over the captured connection's transport, opened on first use and
    /// kept with it, so Core's alert source probe runs once per transport rather than per call.
    /// </summary>
    private LibreNmsResourceApis Apis()
    {
        LibreNmsResourceApis opened;
        ILibreNmsTransport open;
        lock (gate)
        {
            if (retired) throw new RetiredException();
            open = transport ??= openTransport(scope);
            opened = apis ??= new LibreNmsResourceApis(open);
        }

        // Defence in depth: the transport must be the captured connection's, never a shared
        // one. Its connection object may be a newer copy (a timeout or a trusted certificate
        // applied since), but never another server, backup or token.
        if (open.Connection is { } dialling && !SameServer(dialling, scope.Connection))
            throw new RetiredException();
        return opened;
    }

    private Task<IReadOnlyList<Device>> ListAsync(CancellationToken cancellationToken) => Apis().Devices.ListAsync(cancellationToken);

    private Task<IReadOnlyList<Alert>> ListOpenAsync(AlertQuery? query, CancellationToken cancellationToken)
    {
        // Only the reviewed query: firing and acknowledged, never recovered history or one by id.
        if (!ReferenceEquals(query, AlertQuery.Open)) throw new RefusedException(nameof(IAlertsApi.ListAsync) + " other than the open alerts");
        return Apis().Alerts.ListAsync(AlertQuery.Open, cancellationToken);
    }

    /// <summary>Whether two connections reach the same server, by the same backup, with the same token.</summary>
    internal static bool SameServer(LibreNmsConnection a, LibreNmsConnection b) => SessionScope.SameServer(a, b);

    /// <summary>Retires the client and closes its transport. Safe to call more than once.</summary>
    public void Dispose()
    {
        ILibreNmsTransport? closing;
        lock (gate)
        {
            retired = true;
            closing = transport;
            transport = null;
            apis = null;
        }
        (closing as IDisposable)?.Dispose();
    }

    /// <summary>The device list and nothing else of <see cref="IDevicesApi"/>.</summary>
    private sealed class ListOnly(ConnectionBoundClient owner) : IDevicesApi
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

    /// <summary>The open alerts list and nothing else of <see cref="IAlertsApi"/>.</summary>
    private sealed class OpenOnly(ConnectionBoundClient owner) : IAlertsApi
    {
        public Task<IReadOnlyList<Alert>> ListAsync(AlertQuery? query = null, CancellationToken cancellationToken = default) => owner.ListOpenAsync(query, cancellationToken);

        public Task<AlertSource> GetSourceAsync(CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetSourceAsync));
        public Task<Alert?> GetAsync(int alertId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetAsync));
        public Task AcknowledgeAsync(int alertId, string? note = null, bool untilClear = true, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(AcknowledgeAsync));
        public Task UnmuteAsync(int alertId, string? note = null, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(UnmuteAsync));
    }

    public IAlertsApi Alerts
    {
        get
        {
            lock (gate)
                return retired ? throw new RetiredException() : alerts;
        }
    }

    public IAlertRulesApi Rules => throw new RefusedException(nameof(Rules));
    public IAlertTemplatesApi AlertTemplates => throw new RefusedException(nameof(AlertTemplates));
    public ISensorsApi Sensors => throw new RefusedException(nameof(Sensors));
    public IPortsApi Ports => overview ? new StatusOnly(this) : throw new RefusedException(nameof(Ports));

    private sealed class StatusOnly(ConnectionBoundClient owner) : IPortsApi
    {
        public Task<IReadOnlyList<Port>> ListAllStatusAsync(CancellationToken cancellationToken = default) => owner.Apis().Ports.ListAllStatusAsync(cancellationToken);
        public Task<IReadOnlyList<Port>> ListForDeviceAsync(int deviceId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(ListForDeviceAsync));
        public Task<IReadOnlyList<Port>> ListAllNamesAsync(CancellationToken cancellationToken = default) => throw new RefusedException(nameof(ListAllNamesAsync));
        public Task<IReadOnlyList<DeviceIpAddress>> ListIpAddressesAsync(int deviceId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(ListIpAddressesAsync));
    }

    private sealed class SystemOnly(ConnectionBoundClient owner) : ISystemApi
    {
        public Task<SystemInfo> GetAsync(CancellationToken cancellationToken = default) => owner.Apis().System.GetAsync(cancellationToken);
    }
    public ILinksApi Links => throw new RefusedException(nameof(Links));
    public IDeviceHealthApi Health => throw new RefusedException(nameof(Health));
    public IFdbApi Fdb => throw new RefusedException(nameof(Fdb));
    public IArpApi Arp => overview ? throw new RefusedException(nameof(Arp)) : new SearchArp(this);

    private sealed class SearchArp(ConnectionBoundClient owner) : IArpApi
    {
        public Task<IReadOnlyList<ArpEntry>> FindByMacAsync(string mac, CancellationToken cancellationToken = default)
            => owner.Apis().Arp.FindByMacAsync(mac, cancellationToken);
        public Task<IReadOnlyList<ArpEntry>> ListAllAsync(CancellationToken cancellationToken = default) => throw new RefusedException(nameof(ListAllAsync));
        public Task<IReadOnlyList<ArpEntry>> ListForDeviceAsync(int deviceId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(ListForDeviceAsync));
    }
    public IVlansApi Vlans => throw new RefusedException(nameof(Vlans));
    public IDeviceGroupsApi DeviceGroups => overview ? throw new RefusedException(nameof(DeviceGroups)) : new SearchGroups(this);

    private sealed class SearchGroups(ConnectionBoundClient owner) : IDeviceGroupsApi
    {
        public Task<IReadOnlyList<DeviceGroup>> ListAsync(CancellationToken cancellationToken = default)
            => owner.Apis().DeviceGroups.ListAsync(cancellationToken);
        public Task<IReadOnlyList<int>> GetMemberDeviceIdsAsync(int groupId, CancellationToken cancellationToken = default)
            => owner.Apis().DeviceGroups.GetMemberDeviceIdsAsync(groupId, cancellationToken);
        public Task<IReadOnlyList<DeviceGroup>> ListForDeviceAsync(int deviceId, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(ListForDeviceAsync));
        public Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetMembershipByDeviceAsync(CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetMembershipByDeviceAsync));
        public Task<IReadOnlyDictionary<int, int>> GetMemberCountsAsync(IReadOnlyList<DeviceGroup> groups, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(GetMemberCountsAsync));
        public Task CreateAsync(string name, string? description, IReadOnlyList<int> deviceIds, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(CreateAsync));
        public Task UpdateAsync(string currentName, string newName, string? description, IReadOnlyList<int> deviceIds, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(UpdateAsync));
        public Task DeleteAsync(string name, CancellationToken cancellationToken = default) => throw new RefusedException(nameof(DeleteAsync));
    }
    public ILocationsApi Locations => throw new RefusedException(nameof(Locations));
    public IRoutingApi Routing => throw new RefusedException(nameof(Routing));
    public IPollerGroupsApi PollerGroups => throw new RefusedException(nameof(PollerGroups));
    public ILogsApi Logs => throw new RefusedException(nameof(Logs));
    public ISystemApi System => overview ? new SystemOnly(this) : throw new RefusedException(nameof(System));
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
