using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Help;

namespace DashyNMS.Mobile.Help;

/// <summary>
/// The user guide built into the app (#668): docs/guide exactly as it was for
/// this version, so help works with no signal and on first run.
/// </summary>
public static class BundledHelp
{
    private const string ResourcePrefix = "DashyNMS.Mobile.Help/";

    private static readonly Lazy<HelpFileSet> LazyFiles = new(() => HelpFileSet.FromAssembly(typeof(BundledHelp).Assembly, ResourcePrefix));

    public static HelpFileSet Files => LazyFiles.Value;

    /// <summary>The guide to show for the running version. Downloaded copies go with the rest of the app's cache.</summary>
    public static HelpGuideService CreateService(string? runningVersion)
        => new(HelpProduct.Mobile, HelpRelease.ForVersion(runningVersion), Files, AppPaths.HelpCacheDirectory);
}
