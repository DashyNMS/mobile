using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Updates;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// What's new in this release (#159), as desktop's (DashyNMS/desktop#244):
/// shown once after an update, never on a fresh install, and from Settings ›
/// About at any time. The notes are the app's own embedded WhatsNew.md, so
/// they work offline and say exactly what shipped.
/// </summary>
public sealed class WhatsNew
{
    private readonly ISettingsStore _settings;
    private bool _offeredThisSession;

    public WhatsNew(ISettingsStore settings, string? notes = null, string? currentVersion = null)
    {
        _settings = settings;
        Notes = WhatsNewNotes.Parse(notes ?? Embedded());
        CurrentVersion = currentVersion ?? typeof(WhatsNew).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    /// <summary>The release notes, or null when there are none to show.</summary>
    public WhatsNewNotes? Notes { get; }

    public string CurrentVersion { get; }

    /// <summary>"Alert groups, Event log and Graylog cards, State sensor names and 7 more changes".</summary>
    public string? Summary => Notes?.Summary();

    /// <summary>Due now: these notes are this release's, and haven't been shown for it.</summary>
    public bool IsDue => WhatsNewNotes.IsDue(Notes, CurrentVersion, _settings.Current.WhatsNewShownVersion);

    /// <summary>At start-up, before sign-in saves a server: a fresh install has nothing new to it.</summary>
    public void MarkSeenIfFreshInstall()
    {
        if (WhatsNewNotes.MarkSeenIfFreshInstall(_settings.Current, CurrentVersion))
        {
            _settings.Save();
        }
    }

    /// <summary>"Got it": not shown again for this release.</summary>
    public void MarkShown()
    {
        _settings.Current.WhatsNewShownVersion = CurrentVersion;
        _settings.Save();
    }

    /// <summary>After sign-in, once per launch at most: the notes, if they're due.</summary>
    public Task ShowIfDueAsync(INavigationService navigation)
    {
        if (_offeredThisSession || !IsDue)
        {
            return Task.CompletedTask;
        }

        _offeredThisSession = true;
        return navigation.GoToAsync(Routes.WhatsNew);
    }

    private static string? Embedded()
    {
        using var stream = typeof(WhatsNew).Assembly.GetManifestResourceStream("DashyNMS.Mobile.WhatsNew.md");
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
