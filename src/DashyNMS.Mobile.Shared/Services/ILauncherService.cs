namespace DashyNMS.Mobile.Services;

/// <summary>Opens links outside the app.</summary>
public interface ILauncherService
{
    /// <summary>In the system browser.</summary>
    Task OpenAsync(Uri uri);

    /// <summary>In whichever app handles the link's scheme (ssh://, telnet://); false when none does.</summary>
    Task<bool> TryOpenAppAsync(Uri uri);
}
