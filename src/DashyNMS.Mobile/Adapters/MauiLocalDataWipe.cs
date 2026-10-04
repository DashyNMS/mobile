using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Adapters;

/// <summary>
/// "Sign out and forget everything" (#139): everything DashyNMS keeps on the
/// phone, gone - see <see cref="ILocalDataWipe"/>.
/// </summary>
/// <remarks>
/// Only DashyNMS's own files, not the whole of <see cref="FileSystem.AppDataDirectory"/>:
/// on iOS that's the app's Library folder, which also holds the system's
/// preferences file and WebKit's data, and deleting those under a running app
/// isn't safe. So it's Core's data folder (through <see cref="LocalDataReset"/>,
/// which keeps its logs folder), the map layouts, the exports, then the
/// platform stores cleared through their own APIs. The diagnostics log sits
/// beside them and isn't touched.
/// </remarks>
public sealed class MauiLocalDataWipe(ResettableMapLayoutStore layouts) : ILocalDataWipe
{
    public IReadOnlyList<string> Wipe()
    {
        var failed = new List<string>(LocalDataReset.Wipe(AppPaths.DataDirectory, Path.Combine(FileSystem.CacheDirectory, "exports")));

        if (!layouts.ForgetAll())
        {
            failed.Add("map layouts");
        }

        // Appearance, tab pins, ignored alerts, the badge threshold, remembered
        // map views - and the keychain or keystore: tokens and passwords.
        Preferences.Default.Clear();
        SecureStorage.Default.RemoveAll();
        return failed;
    }
}
