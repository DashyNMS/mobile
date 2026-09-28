namespace DashyNMS.Mobile.Services;

/// <summary>Opens a URL in the system browser.</summary>
public interface ILauncherService
{
    Task OpenAsync(Uri uri);
}
