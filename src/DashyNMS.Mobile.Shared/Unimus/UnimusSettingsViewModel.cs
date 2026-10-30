using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Security;

namespace DashyNMS.Mobile.Unimus;

/// <summary>
/// Settings, Unimus: desktop's Unimus settings, edited as a draft and applied
/// on Save as Graylog's are, so a half-typed address never reconfigures the
/// live client.
/// </summary>
public sealed partial class UnimusSettingsViewModel : ViewModelBase
{
    private readonly ISettingsStore _settings;
    private readonly UnimusSetup _setup;
    private readonly IUnimusTokenProtector _tokens;
    private readonly IUnimusConnectionTester _tester;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly UnimusSettings _draft;

    [ObservableProperty]
    private string _tokenInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TokenStatusText), nameof(CanForgetToken))]
    private bool _hasStoredToken;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTestResult))]
    private string? _testResultText;

    [ObservableProperty]
    private bool _testSucceeded;

    public UnimusSettingsViewModel(
        ISettingsStore settings,
        UnimusSetup setup,
        IUnimusTokenProtector tokens,
        IUnimusConnectionTester tester,
        IDialogService dialogs,
        INavigationService navigation)
    {
        _settings = settings;
        _setup = setup;
        _tokens = tokens;
        _tester = tester;
        _dialogs = dialogs;
        _navigation = navigation;
        _draft = settings.Current.Unimus.Clone();
        _hasStoredToken = tokens.HasStoredToken;
    }

    public bool Enabled
    {
        get => _draft.Enabled;
        set => Set(_draft.Enabled, value, v => _draft.Enabled = v);
    }

    /// <summary>The server's address; "https://" is added when there's no scheme, and a pasted "/api/v2" is dropped.</summary>
    public string? Url
    {
        get => _draft.Url;
        set => Set(_draft.Url, value, v => _draft.Url = v);
    }

    public bool AllowUntrustedCertificate
    {
        get => _draft.AllowUntrustedCertificate;
        set => Set(_draft.AllowUntrustedCertificate, value, v => _draft.AllowUntrustedCertificate = v);
    }

    /// <summary>
    /// LibreNMS's <c>mydomain</c>, which its API doesn't give out - so a
    /// device LibreNMS knows by a short name still matches its full name in Unimus.
    /// </summary>
    public string? MyDomain
    {
        get => _draft.MyDomain;
        set => Set(_draft.MyDomain, value, v => _draft.MyDomain = v);
    }

    public string TokenStatusText => HasStoredToken
        ? "A token is saved on this phone. Leave blank to keep it."
        : "No token saved yet. Create one in Unimus under Settings › API.";

    public bool CanForgetToken => HasStoredToken;

    public bool HasTestResult => !string.IsNullOrEmpty(TestResultText);

    /// <summary>Tries what's in the form, with the typed token or else the saved one.</summary>
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
            try
            {
                await _tester.TestAsync(connection);
            }
            catch (UnimusApiException ex) when (ex.UntrustedCertificate is not null)
            {
                if (!await TrustAsync(ex.UntrustedCertificate))
                {
                    throw;
                }

                connection = BuildConnection(out _) ?? connection;
                await _tester.TestAsync(connection);
            }

            TestSucceeded = true;
            TestResultText = "Connected to Unimus.";
        }
        catch (UnimusApiException ex)
        {
            TestSucceeded = false;
            TestResultText = ex.ToUserMessage();
        }
    });

    /// <summary>
    /// Saves the settings and any typed token, then connects (or disconnects).
    /// Switched on but incomplete isn't saved: the form stays open saying what's missing.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (_draft.Enabled && BuildConnection(out var error) is null)
        {
            ErrorMessage = error;
            return;
        }

        _draft.Url = _draft.Url?.Trim();
        _draft.MyDomain = string.IsNullOrWhiteSpace(_draft.MyDomain) ? null : _draft.MyDomain.Trim().TrimStart('.');
        _settings.Current.Unimus = _draft.Clone();
        _settings.Save();
        _setup.Apply(TokenInput);

        TokenInput = string.Empty;
        HasStoredToken = _tokens.HasStoredToken;

        await _navigation.GoToAsync(Routes.Back);
    }

    [RelayCommand]
    private async Task ForgetTokenAsync()
    {
        var confirmed = await _dialogs.ConfirmDestructiveAsync(
            "Forget token",
            $"Remove the saved Unimus token from this phone? {Confirmations.CannotBeUndone} Config backups stay hidden until you enter it again.",
            "Forget");
        if (!confirmed)
        {
            return;
        }

        _setup.ForgetToken();
        TokenInput = string.Empty;
        HasStoredToken = false;
        TestResultText = null;
    }

    /// <summary>Unimus's certificate, shown as desktop shows it; accepted, it joins the form's trusted ones.</summary>
    private async Task<bool> TrustAsync(CertificateDetails certificate)
    {
        var (title, message) = CertificateTrust.DescribeForPrompt(certificate, "Unimus");
        if (!await _dialogs.ConfirmAsync(title, message, "Trust", "Cancel"))
        {
            return false;
        }

        if (!_draft.TrustedCertificates.Any(f => CertificateTrust.SameFingerprint(f, certificate.Fingerprint)))
        {
            _draft.TrustedCertificates.Add(certificate.Fingerprint);
        }

        return true;
    }

    private UnimusConnection? BuildConnection(out string? error)
    {
        if (!UnimusConnection.TryParseWebRoot(_draft.Url, out var root, out error))
        {
            return null;
        }

        var token = string.IsNullOrWhiteSpace(TokenInput) ? _tokens.Load() : TokenInput.Trim();
        if (string.IsNullOrEmpty(token))
        {
            error = "Enter an API token from Unimus.";
            return null;
        }

        return UnimusSetup.Connection(_draft, root!, token);
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
