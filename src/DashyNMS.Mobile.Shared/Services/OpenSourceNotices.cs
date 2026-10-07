namespace DashyNMS.Mobile.Services;

/// <summary>Which builds ship a component: both, or only Android's.</summary>
public enum NoticePlatform
{
    All,
    Android,
}

/// <summary>
/// One third-party component the app ships, and the notice its licence asks
/// to go out with it (#164).
/// </summary>
/// <param name="Name">What it's called: "Leaflet".</param>
/// <param name="Use">What the app uses it for: "The map".</param>
/// <param name="Copyright">Its copyright line, as its licence gives it.</param>
/// <param name="Licence">The licence's name: "MIT".</param>
/// <param name="Link">Its home page, or for map data the copyright page.</param>
/// <param name="LicenceFile">
/// The licence's full text, shipped under Resources/Raw/licences - or null
/// when the notice itself is what's asked for (OpenStreetMap's attribution).
/// </param>
/// <param name="Platform">Android only for AndroidX and Kotlin, which iOS builds don't contain.</param>
public sealed record OpenSourceNotice(
    string Name,
    string Use,
    string Copyright,
    string Licence,
    Uri Link,
    string? LicenceFile,
    NoticePlatform Platform = NoticePlatform.All);

/// <summary>
/// Everything third-party the app ships, for Settings › About's open-source
/// licences page (#164). The MIT, BSD, OFL and Apache licences all ask for
/// their notice and licence text to go out with the app, and OpenStreetMap
/// for its attribution.
/// </summary>
/// <remarks>
/// <para>Kept by hand: add a line here with any new package, bundled script or
/// font, and its licence text under Resources/Raw/licences (a test checks each
/// file is there). Test-only packages don't ship, so they aren't listed.</para>
/// <para>Desktop's Core is to provide the notice type, its own entries and the
/// trademark wording (DashyNMS/desktop#266); these move to it then.</para>
/// </remarks>
public static class OpenSourceNotices
{
    /// <summary>Where the licence texts are in the app package.</summary>
    public const string LicencesFolder = "licences";

    /// <summary>The trademark line in About - the website and READMEs say the same of LibreNMS.</summary>
    public const string Trademarks =
        "DashyNMS isn't affiliated with LibreNMS or Graylog. LibreNMS and Graylog are trademarks of their respective owners.";

    private const string DotNetFoundation = "Copyright (c) .NET Foundation and Contributors";

    public static IReadOnlyList<OpenSourceNotice> All { get; } =
    [
        new(".NET", "The runtime and libraries the app is built on, including Microsoft.Extensions", DotNetFoundation, "MIT",
            new Uri("https://github.com/dotnet/runtime"), "mit.txt"),
        new(".NET MAUI", "The app's interface on iOS and Android", DotNetFoundation, "MIT",
            new Uri("https://github.com/dotnet/maui"), "mit.txt"),
        new("CommunityToolkit.Mvvm", "The app's view models", DotNetFoundation, "MIT",
            new Uri("https://github.com/CommunityToolkit/dotnet"), "mit.txt"),
        new("Leaflet", "The map", "Copyright (c) 2010-2023, Volodymyr Agafonkin. Copyright (c) 2010-2011, CloudMade", "BSD 2-Clause",
            new Uri("https://leafletjs.com"), "bsd-2-clause-leaflet.txt"),
        new("OpenStreetMap", "The map's tiles and data", "© OpenStreetMap contributors", "Open Database License (ODbL)",
            new Uri("https://www.openstreetmap.org/copyright"), null),
        new("IBM Plex Sans and Mono", "The app's text and figures", "Copyright © 2017 IBM Corp. with Reserved Font Name \"Plex\"", "SIL Open Font License 1.1",
            new Uri("https://github.com/IBM/plex"), "ofl-ibm-plex.txt"),
        new("Sora", "The app's headings", "Copyright 2019 The Sora Project Authors", "SIL Open Font License 1.1",
            new Uri("https://github.com/sora-xor/sora-font"), "ofl-sora.txt"),
        new("AndroidX", "Background alert checks (WorkManager) and the libraries it uses", "Copyright The Android Open Source Project", "Apache 2.0",
            new Uri("https://developer.android.com/jetpack/androidx"), "apache-2.0.txt", NoticePlatform.Android),
        new("Kotlin standard library", "Used by AndroidX", "Copyright JetBrains s.r.o. and Kotlin Programming Language contributors", "Apache 2.0",
            new Uri("https://kotlinlang.org"), "apache-2.0.txt", NoticePlatform.Android),
        new(".NET for Android bindings", "AndroidX for .NET", DotNetFoundation, "MIT",
            new Uri("https://github.com/dotnet/android-libraries"), "mit.txt", NoticePlatform.Android),
    ];

    /// <summary>What this build ships: Android's extras only on Android.</summary>
    public static IReadOnlyList<OpenSourceNotice> For(bool android) =>
        All.Where(n => n.Platform == NoticePlatform.All || android).ToList();
}

/// <summary>The licence texts in the app package, and which platform this is.</summary>
public interface ILicenceTexts
{
    bool IsAndroid { get; }

    Task<string> ReadAsync(string licenceFile);
}
