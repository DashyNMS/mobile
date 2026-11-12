using DesktopNMS.Core.Api;
using DesktopNMS.Core.Security;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// The client handed to the shared device search (#596): the device list only, and
/// only while the connection the chat captured is still the session's.
/// </summary>
/// <remarks>
/// <para>
/// Mobile has one LibreNMS client whose connection changes on sign-in, reconnect and
/// sign-out, while the shared search expects a client bound to one connection. Each
/// request therefore checks the captured connection immediately before it reaches the
/// transport, and a retired chat makes no request at all. A request that passed the
/// check just before a switch can still be dialled on the new connection; its reply is
/// discarded by the lookup, but closing that window needs a per-connection client
/// from Core.
/// </para>
/// <para>
/// Everything else is refused before transport, so a profile change upstream (MAC
/// search through ARP, group scopes) cannot quietly widen what the chat reads. The
/// session-wide controls - connecting, certificate trust, timeouts - are never the
/// search's to touch.
/// </para>
/// </remarks>
internal sealed class ConnectionBoundDeviceList(ILibreNmsClient inner, Func<bool> isCurrent) : ILibreNmsClient
{
    /// <summary>Thrown instead of reading another connection's data; the lookup reports the switch.</summary>
    internal sealed class RetiredException() : OperationCanceledException("The connection changed.");

    /// <summary>Thrown for anything outside the reviewed read-only profile.</summary>
    internal sealed class RefusedException(string member) : NotSupportedException(member + " is not available to Intelligence device search.");

    public IDevicesApi Devices => isCurrent() ? inner.Devices : throw new RetiredException();

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
