using DashyNMS.Mobile.Security;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Security;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Graylog;

/// <summary>
/// Points Core's <see cref="IGraylogApi"/> at the Graylog server in settings -
/// what desktop's <c>App.ConfigureGraylogIfEnabled</c> does at start-up.
/// </summary>
/// <remarks>
/// Graylog is independent of the LibreNMS sign-in, as on desktop. Mobile does
/// this on first use rather than at start-up because the password comes from
/// <see cref="SecretCache"/>, which reads the keychain asynchronously - so
/// anything that shows Graylog awaits <see cref="EnsureConfiguredAsync"/> first.
/// </remarks>
public sealed class GraylogSetup
{
    private readonly IGraylogApi _graylog;
    private readonly IGraylogPasswordProtector _passwords;
    private readonly ISettingsStore _settings;
    private readonly SecretCache _secrets;
    private readonly ILogger<GraylogSetup> _logger;
    private bool _tried;

    public GraylogSetup(
        IGraylogApi graylog,
        IGraylogPasswordProtector passwords,
        ISettingsStore settings,
        SecretCache secrets,
        ILogger<GraylogSetup> logger)
    {
        _graylog = graylog;
        _passwords = passwords;
        _settings = settings;
        _secrets = secrets;
        _logger = logger;
    }

    /// <summary>True once Graylog is switched on and has everything it needs to connect - not necessarily reachable.</summary>
    public bool IsConfigured => _graylog.IsConfigured;

    /// <summary>
    /// Configures Graylog from settings the first time it's needed. After
    /// that, only Settings' Save changes it (see <see cref="Apply"/>), so a
    /// half-finished setup isn't retried on every page.
    /// </summary>
    public async Task<bool> EnsureConfiguredAsync()
    {
        if (_graylog.IsConfigured || _tried)
        {
            return _graylog.IsConfigured;
        }

        await _secrets.EnsureLoadedAsync(ServiceCollectionExtensions.SecretKeys).ConfigureAwait(false);

        if (!_graylog.IsConfigured && !_tried)
        {
            _tried = true;
            Configure(_settings.Current.Graylog, _passwords.Load());
        }

        return _graylog.IsConfigured;
    }

    /// <summary>
    /// Settings' Save: keeps a newly typed password, then connects with the
    /// saved settings, or disconnects when Graylog is off or incomplete.
    /// </summary>
    /// <param name="newPassword">What was typed in the password box; blank keeps the saved one.</param>
    public void Apply(string? newPassword)
    {
        if (!string.IsNullOrEmpty(newPassword))
        {
            _passwords.Save(newPassword);
        }

        _tried = true;
        Configure(_settings.Current.Graylog, string.IsNullOrEmpty(newPassword) ? _passwords.Load() : newPassword);
    }

    /// <summary>Settings' "Forget password": nothing to connect with any more.</summary>
    public void ForgetPassword()
    {
        _passwords.Clear();
        _graylog.Clear();
    }

    private void Configure(GraylogSettings settings, string? password)
    {
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Server))
        {
            _graylog.Clear();
            return;
        }

        var connection = GraylogConnection.FromSettings(settings, password, out var error);
        if (connection is null)
        {
            _logger.LogWarning("Graylog is switched on but can't be connected to: {Error}", error);
            _graylog.Clear();
            return;
        }

        _graylog.Configure(connection);
    }
}
