namespace DashyNMS.Mobile.Services;

/// <summary>
/// Removes everything DashyNMS keeps on the phone (#139), as desktop's sign-out
/// does with Core's <c>LocalDataReset</c> (desktop #234): Core's data folder
/// (settings, alert watch state), network map layouts, exported files, and the
/// platform's preferences and secure storage. The diagnostics log stays, for
/// bug reports. The app head implements it, as it owns where those live.
/// </summary>
public interface ILocalDataWipe
{
    /// <summary>Wipes it all, carrying on past anything it can't delete. Returns those, each with the reason.</summary>
    IReadOnlyList<string> Wipe();
}
