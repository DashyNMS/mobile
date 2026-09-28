namespace DashyNMS.Mobile.Storage;

/// <summary>Makes desktop's <c>AppPaths</c> point somewhere real on a phone.</summary>
public static class MobileStorage
{
    /// <summary>
    /// Creates the folder desktop's <c>AppPaths</c> builds on
    /// (<see cref="Environment.SpecialFolder.ApplicationData"/>), so it
    /// exists before <c>AppPaths</c> first asks for it.
    /// </summary>
    /// <remarks>
    /// Off Windows, <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/>
    /// returns an empty string for a folder that doesn't exist yet - and on a
    /// freshly installed app it never does. <c>AppPaths</c> would then put
    /// everything under a relative "DashyNMS" folder, relative to wherever the
    /// process happens to be running: nothing saved survives, and on a phone
    /// the folder usually can't even be created. Must run before anything
    /// touches <c>AppPaths</c>, which only looks once.
    /// </remarks>
    /// <returns>The folder, now existing.</returns>
    public static string EnsureDataFolder() =>
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create);
}
