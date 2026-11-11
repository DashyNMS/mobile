namespace DashyNMS.Mobile.Services;

/// <summary>
/// The app's pages on the DashyNMS website, linked from Settings' About card.
/// Apple wants the privacy policy reachable from inside the app as well as
/// on the store listing (guideline 5.1.1), so these must stay live.
/// </summary>
public static class AppLinks
{
    public static Uri PrivacyPolicy { get; } = new("https://dashynms.pckp.net/mobile/privacy/");

    public static Uri Support { get; } = new("https://dashynms.pckp.net/mobile/support/");

    public static Uri Website { get; } = new("https://dashynms.pckp.net/mobile/");

    /// <summary>
    /// The contact address published on the <see cref="Support"/> page and in the
    /// privacy policy. Intelligence answer reports (#574) go here rather than to
    /// a DashyNMS service, so the app keeps no backend of its own.
    /// </summary>
    public const string SupportEmail = "dashynms@pckp.net";
}
