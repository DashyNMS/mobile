using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Adapters;

/// <summary>
/// The platform's side of the diagnostics (#126): errors nothing caught -
/// .NET's, and those surfacing through iOS or Android - with their type,
/// message and the top of the stack; the phone running low on memory; the
/// network coming and going; and the theme changing.
/// </summary>
public static class SystemWatch
{
    /// <summary>How much of an error's stack is kept - enough to see where, not a page of it.</summary>
    private const int StackLines = 4;

#if IOS
    /// <summary>Kept, so the memory warning's observer lives as long as the app.</summary>
    private static Foundation.NSObject? _memoryWarnings;
#endif

    public static void Start(Application app, DiagnosticsLog log)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            log.NoteImportant("Error [error]", Describe(e.ExceptionObject as Exception) + (e.IsTerminating ? " - the app closed" : string.Empty));

        TaskScheduler.UnobservedTaskException += (_, e) =>
            log.NoteImportant("Error [error]", "Unobserved task: " + Describe(e.Exception.InnerException ?? e.Exception));

#if IOS
        ObjCRuntime.Runtime.MarshalManagedException += (_, e) =>
            log.NoteImportant("Error [error]", "Through iOS: " + Describe(e.Exception));
        _memoryWarnings = UIKit.UIApplication.Notifications.ObserveDidReceiveMemoryWarning((_, _) =>
            log.NoteImportant("System [warning]", "Low on memory"));
#elif ANDROID
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
            log.NoteImportant("Error [error]", "Through Android: " + Describe(e.Exception));
#endif

        Connectivity.Current.ConnectivityChanged += (_, e) =>
            log.Note("System", $"Network: {Network(e.NetworkAccess, e.ConnectionProfiles)}");
        log.Note("System", $"Network: {Network(Connectivity.Current.NetworkAccess, Connectivity.Current.ConnectionProfiles)}");

        app.RequestedThemeChanged += (_, e) => log.Note("System", $"Theme: {e.RequestedTheme}");
    }

    /// <summary>Android's low-memory callbacks, from MainApplication.</summary>
    public static void LowMemory(string level) =>
        (IPlatformApplication.Current?.Services.GetService(typeof(DiagnosticsLog)) as DiagnosticsLog)
            ?.NoteImportant("System [warning]", "Low on memory" + (level.Length > 0 ? $" ({level})" : string.Empty));

    /// <summary>"InvalidOperationException: … · at A.B() | at C.D()".</summary>
    internal static string Describe(Exception? ex)
    {
        if (ex is null)
        {
            return "Unknown error";
        }

        var stack = (ex.StackTrace ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(StackLines);
        return $"{ex.GetType().Name}: {ex.Message}" + (ex.StackTrace is null ? string.Empty : " · " + string.Join(" | ", stack));
    }

    /// <summary>"online · Wi-Fi", "offline".</summary>
    private static string Network(NetworkAccess access, IEnumerable<ConnectionProfile> profiles)
    {
        var how = string.Join(", ", profiles.Select(p => p switch
        {
            ConnectionProfile.WiFi => "Wi-Fi",
            ConnectionProfile.Cellular => "cellular",
            ConnectionProfile.Ethernet => "Ethernet",
            ConnectionProfile.Bluetooth => "Bluetooth",
            _ => p.ToString(),
        }).Distinct());
        var state = access switch
        {
            NetworkAccess.Internet => "online",
            NetworkAccess.None => "offline",
            NetworkAccess.Local or NetworkAccess.ConstrainedInternet => "limited",
            _ => access.ToString().ToLowerInvariant(),
        };
        return how.Length == 0 ? state : $"{state} · {how}";
    }
}
