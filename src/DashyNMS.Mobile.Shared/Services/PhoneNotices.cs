using DesktopNMS.Core.Licences;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// What the phone ships on top of Core's shared entries (#164, #165): MAUI,
/// the MVVM toolkit, Lucide drawings, the bundled Leaflet, and on Android the AndroidX and
/// Kotlin libraries. Core holds the notice type, its own entries (.NET, the
/// fonts, OpenStreetMap), shared licence texts and upstream trademark notices
/// (DashyNMS/desktop#266), so both apps word them the same.
/// </summary>
/// <remarks>
/// Kept by hand: add a line here with any new package or bundled script,
/// and embed its licence text here or use an existing Core text. Test-only packages don't ship, so they aren't listed.
/// </remarks>
public static class PhoneNotices
{
    private const NoticePlatforms Phone = NoticePlatforms.iOS | NoticePlatforms.Android;

    /// <summary>Uses the phone-owned resource because Lucide includes Feather-derived notices.</summary>
    public const string LucideLicence = "lucide.txt";

    /// <summary>Reads phone-owned notices offline, falling back to Core's shared texts.</summary>
    public static string? ReadLicence(string file)
    {
        if (file != LucideLicence) return OpenSourceNotices.ReadLicence(file);
        using var stream = typeof(PhoneNotices).Assembly.GetManifestResourceStream("DashyNMS.Mobile.Licences.lucide.txt");
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Keeps the project trademark reservation separate from third-party licence grants.</summary>
    public const string TrademarkReservation =
        "DashyNMS and its logo/app icon are trademarks of Thomas Pickup. The MIT licence permits use of the artwork under copyright, but grants no trademark rights. Truthful reference to DashyNMS is permitted; use of the marks must not imply endorsement or identify another product or service.";

    public static IReadOnlyList<OpenSourceNotice> Own { get; } =
    [
        new("Lucide (including Feather-derived icons)", "Interface drawings; includes upstream Feather provenance where applicable",
            "Copyright (c) 2026 Lucide Icons and Contributors; Copyright (c) 2013-present Cole Bemis", "ISC; MIT for listed Feather-derived icons",
            new Uri("https://github.com/lucide-icons/lucide"), LucideLicence, Phone),
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
    ];

    /// <summary>The maintained component catalogue: Core's shared entries, then the phone's, for <paramref name="platform"/>.</summary>
    public static IReadOnlyList<OpenSourceNotice> For(NoticePlatforms platform) =>
        OpenSourceNotices.For(platform, OpenSourceNotices.Shared.Concat(Own));
}

/// <summary>Which phone this is, for the licences page's Android-only entries.</summary>
public interface INoticePlatform
{
    NoticePlatforms Platform { get; }
}
