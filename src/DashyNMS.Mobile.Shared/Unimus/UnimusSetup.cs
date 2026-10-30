using DashyNMS.Mobile.Security;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Security;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Unimus;

/// <summary>
/// Points Core's <see cref="IUnimusApi"/> at the Unimus server in settings,
/// and matches LibreNMS devices to Unimus's - desktop's start-up
/// configuration and <c>UnimusDeviceResolver</c> in one.
/// </summary>
/// <remarks>
/// As <see cref="Graylog.GraylogSetup"/>: configured on first use, because the
/// token comes from <see cref="SecretCache"/>, which reads the keychain
/// asynchronously. Unimus's whole device list is fetched once and matched on
/// the phone (see <see cref="UnimusDeviceMatcher"/> for why), and kept for a
/// while so opening each device doesn't fetch it again.
/// </remarks>
public sealed class UnimusSetup
{
    /// <summary>Desktop's: short enough that a device just added to Unimus is found soon after.</summary>
    internal static readonly TimeSpan DeviceListLifetime = TimeSpan.FromMinutes(15);

    private readonly IUnimusApi _unimus;
    private readonly IUnimusTokenProtector _tokens;
    private readonly ISettingsStore _settings;
    private readonly SecretCache _secrets;
    private readonly TimeProvider _time;
    private readonly ILogger<UnimusSetup> _logger;
    private readonly SemaphoreSlim _listGate = new(1, 1);
    private IReadOnlyList<UnimusDevice>? _devices;
    private DateTimeOffset _devicesFetched;
    private bool _tried;

    public UnimusSetup(
        IUnimusApi unimus,
        IUnimusTokenProtector tokens,
        ISettingsStore settings,
        SecretCache secrets,
        TimeProvider time,
        ILogger<UnimusSetup> logger)
    {
        _unimus = unimus;
        _tokens = tokens;
        _settings = settings;
        _secrets = secrets;
        _time = time;
        _logger = logger;
    }

    /// <summary>True once Unimus is switched on and has an address and token - not necessarily reachable.</summary>
    public bool IsConfigured => _unimus.IsConfigured;

    /// <summary>Configures Unimus from settings the first time it's needed; after that only Save changes it.</summary>
    public async Task<bool> EnsureConfiguredAsync()
    {
        if (_unimus.IsConfigured || _tried)
        {
            return _unimus.IsConfigured;
        }

        await _secrets.EnsureLoadedAsync(ServiceCollectionExtensions.SecretKeys).ConfigureAwait(false);

        if (!_unimus.IsConfigured && !_tried)
        {
            _tried = true;
            Configure(_settings.Current.Unimus, _tokens.Load());
        }

        return _unimus.IsConfigured;
    }

    /// <summary>Settings' Save: keeps a newly typed token, then connects with the saved settings, or disconnects.</summary>
    /// <param name="newToken">What was typed in the token box; blank keeps the saved one.</param>
    public void Apply(string? newToken)
    {
        if (!string.IsNullOrWhiteSpace(newToken))
        {
            _tokens.Save(newToken.Trim());
        }

        _tried = true;
        Configure(_settings.Current.Unimus, string.IsNullOrWhiteSpace(newToken) ? _tokens.Load() : newToken.Trim());
    }

    /// <summary>Settings' "Forget token", and signing out with "forget everything".</summary>
    public void ForgetToken()
    {
        _tokens.Clear();
        _unimus.Clear();
        ForgetDevices();
    }

    /// <summary>
    /// The Unimus device for a LibreNMS one, by its names then its address,
    /// as LibreNMS itself matches them. Null when Unimus isn't set up or
    /// doesn't have the device.
    /// </summary>
    public async Task<UnimusDevice?> FindAsync(Device device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!await EnsureConfiguredAsync().ConfigureAwait(false))
        {
            return null;
        }

        var devices = await DevicesAsync(cancellationToken).ConfigureAwait(false);
        return UnimusDeviceMatcher.Match(devices, UnimusAddressCandidates.ForDevice(device, _settings.Current.Unimus.MyDomain));
    }

    /// <summary>Unimus's device list is fetched again next time - a refresh, or new settings.</summary>
    public void ForgetDevices() => _devices = null;

    private async Task<IReadOnlyList<UnimusDevice>> DevicesAsync(CancellationToken cancellationToken)
    {
        if (_devices is { } cached && _time.GetUtcNow() - _devicesFetched < DeviceListLifetime)
        {
            return cached;
        }

        await _listGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_devices is { } fresh && _time.GetUtcNow() - _devicesFetched < DeviceListLifetime)
            {
                return fresh;
            }

            _devices = await _unimus.ListAllDevicesAsync(cancellationToken).ConfigureAwait(false);
            _devicesFetched = _time.GetUtcNow();
            return _devices;
        }
        finally
        {
            _listGate.Release();
        }
    }

    private void Configure(UnimusSettings settings, string? token)
    {
        ForgetDevices();

        if (!settings.Enabled || string.IsNullOrWhiteSpace(token)
            || !UnimusConnection.TryParseWebRoot(settings.Url, out var root, out var error))
        {
            if (settings.Enabled)
            {
                _logger.LogWarning("Unimus is switched on but can't be connected to: {Error}", string.IsNullOrWhiteSpace(token) ? "no token" : "bad address");
            }

            _unimus.Clear();
            return;
        }

        _unimus.Configure(Connection(settings, root!, token));
    }

    /// <summary>A connection from settings, as Save and Test both build it.</summary>
    internal static UnimusConnection Connection(UnimusSettings settings, Uri root, string token) =>
        new(root, token, settings.AllowUntrustedCertificate, trustedCertificates: settings.TrustedCertificates);
}

/// <summary>Settings' "Test connection" - so tests can answer without a Unimus server.</summary>
public interface IUnimusConnectionTester
{
    /// <summary>Completes when Unimus accepted the address and token; otherwise a <see cref="UnimusApiException"/> saying why not.</summary>
    Task TestAsync(UnimusConnection connection, CancellationToken cancellationToken = default);
}

/// <summary>Tries the form's settings on a throwaway client, so the live <see cref="IUnimusApi"/> isn't touched until Save.</summary>
public sealed class UnimusConnectionTester : IUnimusConnectionTester
{
    public async Task TestAsync(UnimusConnection connection, CancellationToken cancellationToken = default)
    {
        using var probe = new UnimusApi(Microsoft.Extensions.Logging.Abstractions.NullLogger<UnimusApi>.Instance);
        probe.Configure(connection);
        await probe.TestConnectionAsync(cancellationToken).ConfigureAwait(false);
    }
}
