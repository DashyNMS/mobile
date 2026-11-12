using System;
using System.Threading;
using System.Threading.Tasks;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Security;
using Microsoft.Extensions.Logging;

namespace DesktopNMS.Services;

/// <summary>
/// Owns "am I signed in, and to what". Everything else asks this rather than
/// poking the client directly.
/// </summary>
public interface ISessionService : DesktopNMS.Core.Services.IConnectionState
{

    LibreNmsConnection? Connection { get; }

    /// <summary>
    /// Moves on every sign-in, reconnect, restore and sign-out, before the client switches;
    /// never on a trusted certificate, a timeout change or failover, which keep the
    /// connection. Something bound to one connection (an Intelligence chat, #596) compares
    /// this rather than <see cref="Connection"/>'s reference, which Core replaces on each of
    /// those (see <see cref="DashyNMS.Mobile.Services.SessionScope"/>).
    /// </summary>
    long ConnectionGeneration { get; }

    /// <summary>
    /// <see cref="Connection"/> with <see cref="ConnectionGeneration"/>, read together in one
    /// step with the switch; null when signed out. Reading the two properties one after the
    /// other can straddle a switch: the generation has moved but the client still holds the
    /// old connection, so a reconnect to the same server with a different certificate policy
    /// would pair the new generation with the old policy and pass as current (#596).
    /// </summary>
    DashyNMS.Mobile.Services.SessionScope? CurrentScope { get; }

    /// <summary>Version details of the connected instance, populated on sign-in.</summary>
    SystemInfo? ServerInfo { get; }

    event EventHandler? StateChanged;

    /// <summary>Validates the credentials, and on success makes them the active session.</summary>
    Task<ConnectionTestResult> SignInAsync(
        string serverUrl,
        string apiToken,
        bool allowUntrustedCertificate,
        bool rememberToken,
        CancellationToken cancellationToken = default,
        string? backupAddress = null);

    /// <summary>
    /// Settings' Save with changed connection details: tests the server
    /// address, then (if it doesn't answer) the backup address, and switches
    /// to whichever answered - see <see cref="SignInAsync"/>. A blank token
    /// keeps the one in use.
    /// </summary>
    Task<ConnectionTestResult> ReconnectAsync(string serverUrl, string? newApiToken, bool allowUntrustedCertificate, string? backupAddress, CancellationToken cancellationToken = default);

    /// <summary>Remembers that the user accepted this LibreNMS certificate, so the next sign-in accepts it (#189).</summary>
    void TrustCertificate(DesktopNMS.Core.Security.CertificateDetails certificate);

    /// <summary>
    /// As <see cref="TrustCertificate(DesktopNMS.Core.Security.CertificateDetails)"/>, only while
    /// the session is still on <paramref name="generation"/>, checked and trusted in one step
    /// with sign-in and sign-out. The certificate prompt passes the generation its rejection
    /// came from, so a certificate met on one server is never trusted after a switch to another.
    /// False when the session moved on and nothing was trusted.
    /// </summary>
    bool TrustCertificate(DesktopNMS.Core.Security.CertificateDetails certificate, long generation);

    /// <summary>
    /// Moves the session to its backup address because something bound to
    /// <paramref name="generation"/> couldn't reach the server - Intelligence's device search,
    /// whose transport is its own (#596). Failover stays the session's: the whole app moves
    /// together, as when the session's own requests fail over, and a chat for an earlier
    /// connection can't move a later one. True when the session is on its backup afterwards.
    /// </summary>
    bool SwitchToBackup(long generation);


    /// <summary>
    /// Attempts to sign in with the saved address and token. Returns false when
    /// there is nothing saved or the saved details no longer work.
    /// </summary>
    Task<ConnectionTestResult?> TryRestoreAsync(CancellationToken cancellationToken = default);

    /// <summary>Ends the session. Optionally deletes the stored token as well.</summary>
    void SignOut(bool forgetToken);
}

public sealed class SessionService : ISessionService
{
    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;
    private readonly ITokenProtector _tokens;
    private readonly ILogger<SessionService> _logger;

    private readonly DashyNMS.Mobile.Services.LastKnownData? _lastKnown;

    /// <summary>
    /// Held while the connection switches and while a generation-bound trust or backup switch
    /// runs, so neither can land on the connection after the one it was checked against.
    /// </summary>
    private readonly object _switching = new();
    private long _generation;

    public SessionService(
        ILibreNmsClient client,
        ISettingsStore settings,
        ITokenProtector tokens,
        ILogger<SessionService> logger,
        DashyNMS.Mobile.Services.LastKnownData? lastKnown = null)
    {
        _lastKnown = lastKnown;
        _client = client;
        _settings = settings;
        _tokens = tokens;
        _logger = logger;

        // Settings' Request timeout applies straight away, not just after the
        // next sign-in - the connection used to keep the one it started with.
        _settings.Changed += (_, current) =>
        {
            if (_client.IsConnected)
            {
                _client.SetTimeout(current.TimeoutSeconds);
            }
        };
    }

    public bool IsConnected => _client.IsConnected;

    public LibreNmsConnection? Connection => _client.Connection;

    public long ConnectionGeneration => Interlocked.Read(ref _generation);

    public DashyNMS.Mobile.Services.SessionScope? CurrentScope
    {
        get
        {
            // Under the switch lock: the generation moves and the client switches inside it,
            // so the pair read here is always one connection's.
            lock (_switching)
            {
                return _client.Connection is { } connection
                    ? new DashyNMS.Mobile.Services.SessionScope(_generation, connection)
                    : null;
            }
        }
    }

    public SystemInfo? ServerInfo { get; private set; }

    public event EventHandler? StateChanged;

    public async Task<ConnectionTestResult> SignInAsync(
        string serverUrl,
        string apiToken,
        bool allowUntrustedCertificate,
        bool rememberToken,
        CancellationToken cancellationToken = default,
        string? backupAddress = null)
    {
        if (!LibreNmsConnection.TryParseWebRoot(serverUrl, out var webRoot, out var urlError))
        {
            return ConnectionTestResult.Failure(urlError ?? "The server address is not valid.");
        }

        // Left blank: the token in use, or the saved one - no retyping it
        // just to change the address or add a backup.
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            apiToken = _client.Connection?.ApiToken ?? _tokens.Load() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(apiToken))
        {
            return ConnectionTestResult.Failure(DesktopNMS.Core.SignIn.SignInHelp.MissingToken);
        }

        if (!TryParseBackup(backupAddress, out var backupWebRoot, out var backupError))
        {
            return ConnectionTestResult.Failure("The backup address isn't usable: " + backupError);
        }

        var settings = _settings.Current;
        var connection = new LibreNmsConnection(
            webRoot!,
            apiToken.Trim(),
            allowUntrustedCertificate,
            settings.TimeoutSeconds,
            backupWebRoot,
            settings.TrustedCertificates);

        // Back on the caller's (UI) thread: saving settings and StateChanged
        // below set every listener updating what's on screen.
        var result = await _client.TestAsync(connection, cancellationToken).ConfigureAwait(true);

        if (!result.Succeeded)
        {
            return result;
        }

        Switch(() => _client.Connect(connection, result.UsedBackupAddress));
        ServerInfo = result.SystemInfo;

        settings.ServerUrl = webRoot!.ToString();
        settings.BackupServerAddress = connection.BackupWebRoot?.ToString();
        settings.AllowUntrustedCertificate = allowUntrustedCertificate;
        settings.RememberToken = rememberToken;
        _settings.Save();

        if (rememberToken)
        {
            _tokens.Save(connection.ApiToken);
        }
        else
        {
            _tokens.Clear();
        }

        _logger.LogInformation("Signed in to {Host}", webRoot);
        StateChanged?.Invoke(this, EventArgs.Empty);

        return result;
    }

    /// <summary>
    /// The backup address as a URL, like the server address - "192.0.2.20"
    /// reads as https://192.0.2.20/. Blank is fine: no backup.
    /// </summary>
    public static bool TryParseBackup(string? text, out Uri? backup, out string? error)
    {
        backup = null;
        error = null;
        return string.IsNullOrWhiteSpace(text) || LibreNmsConnection.TryParseWebRoot(text, out backup, out error);
    }

    public void TrustCertificate(DesktopNMS.Core.Security.CertificateDetails certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        var trusted = _settings.Current.TrustedCertificates;
        if (!trusted.Any(f => DesktopNMS.Core.Security.CertificateTrust.SameFingerprint(f, certificate.Fingerprint)))
        {
            trusted.Add(certificate.Fingerprint);
            _settings.Save();
        }

        // And straight away for the session that's running - e.g. the backup
        // address's certificate, met when the app fails over to it.
        _client.TrustCertificate(certificate.Fingerprint);

        _logger.LogInformation("Trusted the certificate {Fingerprint} for {Host}", certificate.Fingerprint, certificate.Host);
    }

    public bool TrustCertificate(DesktopNMS.Core.Security.CertificateDetails certificate, long generation)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        lock (_switching)
        {
            if (generation != _generation || !_client.IsConnected)
            {
                return false;
            }

            TrustCertificate(certificate);
            return true;
        }
    }

    public bool SwitchToBackup(long generation)
    {
        lock (_switching)
        {
            if (generation != _generation || !_client.IsConnected)
            {
                return false;
            }

            var failover = _client.Failover;
            if (failover.IsOnBackup)
            {
                return true;
            }

            if (failover.BackupAddress is not { } backup)
            {
                return false;
            }

            // The session's own backup address, never the caller's: what moves is the
            // session's failover, and its transport rebuilds the client for it (Changed).
            failover.Configure(backup, startOnBackup: true);
            _logger.LogWarning("Moved to the backup address: an Intelligence lookup couldn't reach the server");
            return true;
        }
    }

    /// <summary>
    /// Moves the generation, then switches the client, in one step with generation-bound
    /// trust and backup switches. The generation moves first, so anything that sees the new
    /// connection also sees that its own generation is out of date.
    /// </summary>
    private void Switch(Action change)
    {
        lock (_switching)
        {
            Interlocked.Increment(ref _generation);
            change();
        }
    }

    public Task<ConnectionTestResult> ReconnectAsync(string serverUrl, string? newApiToken, bool allowUntrustedCertificate, string? backupAddress, CancellationToken cancellationToken = default)
    {
        return SignInAsync(serverUrl, newApiToken ?? string.Empty, allowUntrustedCertificate, _settings.Current.RememberToken, cancellationToken, backupAddress);
    }

    public async Task<ConnectionTestResult?> TryRestoreAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Current;

        if (string.IsNullOrWhiteSpace(settings.ServerUrl))
        {
            return null;
        }

        var token = _tokens.Load();
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        if (!LibreNmsConnection.TryParseWebRoot(settings.ServerUrl, out var webRoot, out _))
        {
            return null;
        }

        var connection = new LibreNmsConnection(
            webRoot!,
            token!,
            settings.AllowUntrustedCertificate,
            settings.TimeoutSeconds,
            TryParseBackup(settings.BackupServerAddress, out var savedBackup, out _) ? savedBackup : null,
            settings.TrustedCertificates);

        // Back on the caller's (UI) thread: saving settings and StateChanged
        // below set every listener updating what's on screen.
        var result = await _client.TestAsync(connection, cancellationToken).ConfigureAwait(true);

        if (result.Succeeded)
        {
            Switch(() => _client.Connect(connection, result.UsedBackupAddress));
            ServerInfo = result.SystemInfo;
            _logger.LogInformation("Restored the saved session for {Host}{Backup}", webRoot, result.UsedBackupAddress ? " through its backup address" : string.Empty);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _logger.LogWarning("Could not restore the saved session: {Error}", result.ErrorMessage);

            // A rejected token is never going to start working; drop it so the
            // user is asked for a new one rather than seeing the same failure
            // on every launch.
            if (result.IsAuthenticationFailure)
            {
                _tokens.Clear();
            }
            else if (result.UntrustedCertificate is null && _lastKnown?.HasSaved() == true)
            {
                // Out of reach - no signal, the VPN down - with a copy saved (#130):
                // open on it rather than the sign-in form. Everything is fetched
                // again, and used, as soon as the server answers.
                Switch(() => _client.Connect(connection, false));
                _logger.LogInformation("Opened offline on the saved lists for {Host}", webRoot);
                StateChanged?.Invoke(this, EventArgs.Empty);
                return ConnectionTestResult.Success(new SystemInfo());
            }
        }

        return result;
    }

    public void SignOut(bool forgetToken)
    {
        Switch(_client.Disconnect);
        ServerInfo = null;

        // Nothing of this server's left on the phone (#130).
        _lastKnown?.Clear();

        if (forgetToken)
        {
            _tokens.Clear();
        }

        _logger.LogInformation("Signed out (token {Action})", forgetToken ? "deleted" : "kept");
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
