using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Graylog;
using DesktopNMS.Core.Security;

namespace DashyNMS.Mobile.Graylog;

/// <summary>
/// Settings, Graylog: desktop's Graylog settings (LibreNMS's own
/// <c>graylog.*</c> settings), edited as a draft and applied on Save, as on
/// desktop - so a half-typed address never reconfigures the live client.
/// </summary>
/// <remarks>
/// Desktop's per-page row count and Logs auto-update aren't offered: the
/// phone loads more as you scroll and refreshes on a pull instead. Both are
/// kept as they are in the shared settings.
/// </remarks>
public sealed partial class GraylogSettingsViewModel : ViewModelBase
{
    /// <summary>LibreNMS's <c>graylog.version</c> values, in <see cref="VersionLabels"/>' order.</summary>
    internal static readonly string[] Versions = [GraylogSettings.Version21, GraylogSettings.Version20, GraylogSettings.VersionOther];

    private readonly ISettingsStore _settings;
    private readonly GraylogSetup _setup;
    private readonly IGraylogPasswordProtector _passwords;
    private readonly IGraylogConnectionTester _tester;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly GraylogSettings _draft;

    [ObservableProperty]
    private string _passwordInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordStatusText), nameof(CanForgetPassword))]
    private bool _hasStoredPassword;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTestResult))]
    private string? _testResultText;

    [ObservableProperty]
    private bool _testSucceeded;

    public GraylogSettingsViewModel(
        ISettingsStore settings,
        GraylogSetup setup,
        IGraylogPasswordProtector passwords,
        IGraylogConnectionTester tester,
        IDialogService dialogs,
        INavigationService navigation)
    {
        _settings = settings;
        _setup = setup;
        _passwords = passwords;
        _tester = tester;
        _dialogs = dialogs;
        _navigation = navigation;
        _draft = settings.Current.Graylog.Clone();
        _hasStoredPassword = passwords.HasStoredPassword;
    }

    public bool Enabled
    {
        get => _draft.Enabled;
        set => Set(_draft.Enabled, value, v => _draft.Enabled = v);
    }

    /// <summary><c>graylog.server</c> - "https://" is added when there's no scheme, as LibreNMS does.</summary>
    public string? Server
    {
        get => _draft.Server;
        set => Set(_draft.Server, value, v => _draft.Server = v);
    }

    /// <summary>
    /// Text so a half-typed port doesn't fight the keyboard. Blank is the
    /// scheme's default; anything that isn't a whole number is caught by
    /// Test and Save rather than silently dropped.
    /// </summary>
    public string PortText
    {
        get => _portText ??= _draft.Port?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        set => SetProperty(ref _portText, value ?? string.Empty);
    }

    private string? _portText;

    public IReadOnlyList<string> VersionLabels { get; } = ["2.1 or newer", "Less than 2.1", "Other"];

    public int VersionIndex
    {
        get => Math.Max(0, Array.IndexOf(Versions, _draft.Version));
        set
        {
            if (value < 0 || value >= Versions.Length || Versions[value] == _draft.Version)
            {
                return;
            }

            _draft.Version = Versions[value];
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBaseUriVisible));
        }
    }

    /// <summary>LibreNMS only offers the base URI with version "Other"; Core only uses it then too.</summary>
    public bool IsBaseUriVisible => _draft.Version == GraylogSettings.VersionOther;

    public string? BaseUri
    {
        get => _draft.BaseUri;
        set => Set(_draft.BaseUri, value, v => _draft.BaseUri = v);
    }

    /// <summary>A username, or an access token (with "token" as the password), as LibreNMS takes them.</summary>
    public string? Username
    {
        get => _draft.Username;
        set => Set(_draft.Username, value, v => _draft.Username = v);
    }

    public string PasswordStatusText => HasStoredPassword
        ? "A password is saved on this phone. Leave blank to keep it."
        : "No password saved yet.";

    public bool CanForgetPassword => HasStoredPassword;

    public bool AllowUntrustedCertificate
    {
        get => _draft.AllowUntrustedCertificate;
        set => Set(_draft.AllowUntrustedCertificate, value, v => _draft.AllowUntrustedCertificate = v);
    }

    /// <summary><c>graylog.timezone</c> - an IANA name such as "Europe/London"; blank is the phone's own.</summary>
    public string? Timezone
    {
        get => _draft.Timezone;
        set
        {
            if (Set(_draft.Timezone, value, v => _draft.Timezone = v))
            {
                OnPropertyChanged(nameof(TimezoneStatusText));
            }
        }
    }

    public string TimezoneStatusText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_draft.Timezone))
            {
                return "Blank shows message times in the phone's own time zone.";
            }

            return GraylogQuery.FindTimeZone(_draft.Timezone) is { } zone
                ? $"Message times show in {zone.DisplayName}."
                : "Not a time zone this phone recognises - times show in the phone's own.";
        }
    }

    /// <summary>"(0) Emergency" to "(7) Debug" - each includes every more severe level.</summary>
    public IReadOnlyList<string> LevelLabels { get; } =
        Enumerable.Range(0, 8).Select(GraylogQuery.LevelText).ToList();

    /// <summary><c>graylog.device-page.loglevel</c> - where a device's Graylog page starts.</summary>
    public int DeviceLogLevel
    {
        get => Math.Clamp(_draft.DeviceLogLevel, 0, 7);
        set => Set(_draft.DeviceLogLevel, Math.Clamp(value, 0, 7), v => _draft.DeviceLogLevel = v);
    }

    /// <summary><c>graylog.query.field</c> - blank goes back to "source".</summary>
    public string? QueryField
    {
        get => _draft.QueryField;
        set => Set(_draft.QueryField, string.IsNullOrWhiteSpace(value) ? GraylogSettings.DefaultQueryField : value.Trim(), v => _draft.QueryField = v);
    }

    /// <summary><c>graylog.match-any-address</c>.</summary>
    public bool MatchAnyAddress
    {
        get => _draft.MatchAnyAddress;
        set => Set(_draft.MatchAnyAddress, value, v => _draft.MatchAnyAddress = v);
    }

    public bool HasTestResult => !string.IsNullOrEmpty(TestResultText);

    /// <summary>Tries what's in the form, with the typed password or else the saved one.</summary>
    [RelayCommand]
    private Task TestAsync() => RunAsync(async () =>
    {
        TestResultText = null;

        var connection = BuildConnection(out var error);
        if (connection is null)
        {
            TestSucceeded = false;
            TestResultText = error;
            return;
        }

        try
        {
            var streams = await _tester.TestAsync(connection);
            TestSucceeded = true;
            TestResultText = streams == 1
                ? "Connected to Graylog - 1 stream available."
                : $"Connected to Graylog - {streams} streams available.";
        }
        catch (GraylogApiException ex)
        {
            TestSucceeded = false;
            TestResultText = ex.ToUserMessage();
        }
    });

    /// <summary>
    /// Saves the settings and any typed password, then connects (or
    /// disconnects). Switched on but incomplete isn't saved - the form stays
    /// open saying what's missing, rather than a Graylog page that never loads.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (!TryReadPort(out var port, out var portError))
        {
            ErrorMessage = portError;
            return;
        }

        _draft.Port = port;

        if (_draft.Enabled && BuildConnection(out var error) is null)
        {
            ErrorMessage = error;
            return;
        }

        _settings.Current.Graylog = _draft.Clone();
        _settings.Save();
        _setup.Apply(PasswordInput);

        PasswordInput = string.Empty;
        HasStoredPassword = _passwords.HasStoredPassword;

        await _navigation.GoToAsync(Routes.Back);
    }

    [RelayCommand]
    private async Task ForgetPasswordAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "Forget password",
            "Remove the saved Graylog password from this phone? Graylog stays off until you enter it again.",
            "Forget",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        _setup.ForgetPassword();
        PasswordInput = string.Empty;
        HasStoredPassword = false;
        TestResultText = null;
    }

    private GraylogConnection? BuildConnection(out string? error)
    {
        if (!TryReadPort(out var port, out error))
        {
            return null;
        }

        var settings = _draft.Clone();
        settings.Port = port;

        var password = string.IsNullOrEmpty(PasswordInput) ? _passwords.Load() : PasswordInput;
        return GraylogConnection.FromSettings(settings, password, out error);
    }

    private bool TryReadPort(out int? port, out string? error)
    {
        port = null;
        error = null;

        var text = PortText.Trim();
        if (text.Length == 0)
        {
            return true;
        }

        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value is >= 1 and <= 65535)
        {
            port = value;
            return true;
        }

        error = "The port must be a number between 1 and 65535, or blank for the default.";
        return false;
    }

    private bool Set<T>(T current, T value, Action<T> apply, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
        {
            return false;
        }

        apply(value);
        OnPropertyChanged(property);
        return true;
    }
}
