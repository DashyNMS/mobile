using System.Globalization;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// What the user has agreed to share (#216): error reports, and usage and
/// performance, two choices. Error reports (#217), measurements (#218) and
/// counts (#219) ask this and nothing else, so nothing is collected or sent
/// before a choice, and turning a choice off is seen everywhere at once.
/// </summary>
public interface IDiagnosticsConsent
{
    /// <summary>Error reports may be queued and sent.</summary>
    bool ShareErrors { get; }

    /// <summary>Usage and performance measurements may be taken and sent.</summary>
    bool ShareUsage { get; }

    /// <summary>
    /// A choice changed. A collector whose choice is now off deletes what it
    /// has queued - withdrawing consent covers what wasn't sent yet.
    /// </summary>
    event EventHandler? Changed;
}

/// <summary>
/// The phone's own record of the diagnostics choices, as desktop asks them
/// (DashyNMS/desktop#62, #498): both off until the user decides, asked once
/// after sign-in, changed in Settings › Diagnostics.
/// </summary>
/// <remarks>
/// Core's <c>AppSettings.DiagnosticsSharing</c> and
/// <c>DiagnosticsConsentVersion</c> are desktop's record of what desktop's
/// notice said (DashyNMS/core#63, #65); the phone's notice is its own, so its
/// choice and notice version live in the phone's preferences instead.
/// Closing the sheet without choosing records nothing, so it asks again at the
/// next launch. A build without a reporting key can't send anything, so it
/// doesn't ask and shares nothing.
/// </remarks>
public sealed class DiagnosticsConsent : IDiagnosticsConsent
{
    /// <summary>
    /// The notice's version. Raise it when the sheet starts describing more
    /// than before: anyone sharing under an older notice is asked again, and
    /// shares nothing until they answer.
    /// </summary>
    public const int CurrentNotice = 1;

    internal const string ErrorsKey = "diagnostics.errors";
    internal const string UsageKey = "diagnostics.usage";
    internal const string NoticeKey = "diagnostics.notice";

    private readonly IAppPreferences _preferences;
    private bool _offeredThisSession;

    public DiagnosticsConsent(IAppPreferences preferences, bool reportingAvailable)
    {
        _preferences = preferences;
        IsAvailable = reportingAvailable;
    }

    public event EventHandler? Changed;

    /// <summary>This build has a reporting key; without one there's nothing to ask about.</summary>
    public bool IsAvailable { get; }

    /// <summary>The user has chosen, under the current notice.</summary>
    public bool HasChosen => Notice == CurrentNotice;

    /// <summary>Error reports, as chosen; off until chosen, and in a build that can't send them.</summary>
    public bool ShareErrors => IsAvailable && HasChosen && _preferences.Get(ErrorsKey) == bool.TrueString;

    /// <summary>Usage and performance, as chosen; off until chosen, and in a build that can't send them.</summary>
    public bool ShareUsage => IsAvailable && HasChosen && _preferences.Get(UsageKey) == bool.TrueString;

    /// <summary>The sheet is due: this build could send, and there's no answer to the current notice.</summary>
    public bool NeedsChoice => IsAvailable && !HasChosen;

    private int? Notice => int.TryParse(_preferences.Get(NoticeKey), NumberStyles.None, CultureInfo.InvariantCulture, out var notice)
        ? notice
        : null;

    /// <summary>The sheet's answer - Don't share is (false, false). Never asked again for this notice.</summary>
    public void Choose(bool errors, bool usage) => Record(errors, usage);

    /// <summary>Settings' Error reports switch; the usage choice stays as it was.</summary>
    public void SetShareErrors(bool share)
    {
        if (share != ShareErrors)
        {
            Record(share, ShareUsage);
        }
    }

    /// <summary>Settings' Usage and performance switch; the error reports choice stays as it was.</summary>
    public void SetShareUsage(bool share)
    {
        if (share != ShareUsage)
        {
            Record(ShareErrors, share);
        }
    }

    /// <summary>
    /// After sign-in, once per launch at most: the sheet, if a choice is due.
    /// True if it was shown, so What's new waits until it's closed.
    /// </summary>
    public async Task<bool> ShowIfDueAsync(INavigationService navigation)
    {
        if (_offeredThisSession || !NeedsChoice)
        {
            return false;
        }

        _offeredThisSession = true;
        await navigation.GoToAsync(Routes.DiagnosticsSharing);
        return true;
    }

    private void Record(bool errors, bool usage)
    {
        _preferences.Set(ErrorsKey, errors.ToString());
        _preferences.Set(UsageKey, usage.ToString());
        _preferences.Set(NoticeKey, CurrentNotice.ToString(CultureInfo.InvariantCulture));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
