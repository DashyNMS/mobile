using DesktopNMS.Core.Licences;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// What the phone ships on top of Core's shared entries (#164, #165): MAUI,
/// the MVVM toolkit, the bundled Leaflet, and on Android the AndroidX and
/// Kotlin libraries. Core holds the notice type, its own entries (.NET, the
/// fonts, OpenStreetMap), every licence text and the trademark line
/// (DashyNMS/desktop#266), so both apps word them the same.
/// </summary>
/// <remarks>
/// Kept by hand: add a line here with any new package or bundled script,
/// and its licence text to Core if it isn't there (a test checks each file
/// is). Test-only packages don't ship, so they aren't listed. A text only
/// the phone ships - a package's own LICENSE with its copyright line - is
/// embedded in this assembly instead and read by <see cref="ReadLicence"/>.
/// </remarks>
public static class PhoneNotices
{
    private const NoticePlatforms Phone = NoticePlatforms.iOS | NoticePlatforms.Android;
    private const string ResourcePrefix = "DashyNMS.Mobile.Licences.";

    /// <summary>The LICENSE inside the DashyNMS.Agent.Search package, embedded at build time.</summary>
    public const string AgentSearchMit = "dashynms-agent-search-mit.txt";

    public static IReadOnlyList<OpenSourceNotice> Own { get; } =
    [
        new(".NET MAUI", "The app's interface on iOS and Android", OpenSourceNotices.DotNetFoundation, "MIT",
            new Uri("https://github.com/dotnet/maui"), OpenSourceNotices.Mit, Phone),
        new("CommunityToolkit.Mvvm", "The app's view models", OpenSourceNotices.DotNetFoundation, "MIT",
            new Uri("https://github.com/CommunityToolkit/dotnet"), OpenSourceNotices.Mit, Phone),
        new("Leaflet", "The map", "Copyright (c) 2010-2023, Volodymyr Agafonkin. Copyright (c) 2010-2011, CloudMade", "BSD 2-Clause",
            new Uri("https://leafletjs.com"), OpenSourceNotices.Bsd2Leaflet, Phone),
        new("AndroidX", "Background alert checks (WorkManager) and the libraries it uses", "Copyright The Android Open Source Project", "Apache 2.0",
            new Uri("https://developer.android.com/jetpack/androidx"), OpenSourceNotices.Apache2, NoticePlatforms.Android),
        new("Kotlin standard library", "Used by AndroidX", "Copyright JetBrains s.r.o. and Kotlin Programming Language contributors", "Apache 2.0",
            new Uri("https://kotlinlang.org"), OpenSourceNotices.Apache2, NoticePlatforms.Android),
        new(".NET for Android bindings", "AndroidX for .NET", OpenSourceNotices.DotNetFoundation, "MIT",
            new Uri("https://github.com/dotnet/android-libraries"), OpenSourceNotices.Mit, NoticePlatforms.Android),
        new("DashyNMS.Agent.Search", "Device search when asking about the network", "Copyright (c) 2026 Thomas Pickup", "MIT",
            new Uri("https://github.com/DashyNMS/mcp"), AgentSearchMit, Phone),
    ];

    /// <summary>Every licence text embedded in the phone itself, by file name.</summary>
    public static IReadOnlyList<string> LicenceFiles { get; } =
        typeof(PhoneNotices).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(n => n[ResourcePrefix.Length..])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    /// <summary>A licence's full text from the phone's own texts, then Core's; null when neither has it.</summary>
    public static string? ReadLicence(string licenceFile)
    {
        using var stream = typeof(PhoneNotices).Assembly.GetManifestResourceStream(ResourcePrefix + licenceFile);
        if (stream is null)
            return OpenSourceNotices.ReadLicence(licenceFile);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Everything this build ships: Core's shared entries, then the phone's, for <paramref name="platform"/>.</summary>
    public static IReadOnlyList<OpenSourceNotice> For(NoticePlatforms platform) =>
        OpenSourceNotices.For(platform, OpenSourceNotices.Shared.Concat(Own));
}

/// <summary>Which phone this is, for the licences page's Android-only entries.</summary>
public interface INoticePlatform
{
    NoticePlatforms Platform { get; }
}
