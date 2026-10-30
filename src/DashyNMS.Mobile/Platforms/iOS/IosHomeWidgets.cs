using DashyNMS.Mobile.Widgets;
using Foundation;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile;

/// <summary>
/// Feeds the WidgetKit extension (ios-widget/): the snapshot goes in the App
/// Group folder the app and widgets share, and they re-read it on their own
/// schedule.
/// </summary>
/// <remarks>
/// WidgetCenter - the API that lists placed widgets and asks them to reload -
/// is Swift-only, so .NET can't tell whether a widget is placed. The shared
/// folder existing (the app was built with the widget and its App Group) is
/// taken as "in use" instead.
/// </remarks>
public sealed class IosHomeWidgets : IHomeWidgets
{
    internal const string AppGroup = "group.net.pckp.DashyNMS";
    private const string SnapshotFile = "widget-snapshot.json";

    private readonly ILogger<IosHomeWidgets> _logger;
    private readonly string? _folder;
    private string? _lastSaveError;
    private bool? _lastSavedSignedIn;

    public IosHomeWidgets(ILogger<IosHomeWidgets> logger)
    {
        _logger = logger;
        _folder = NSFileManager.DefaultManager.GetContainerUrl(AppGroup)?.Path;
        if (_folder is null)
        {
            // Without it the widgets only ever say "sign in" (#286).
            _logger.LogWarning("No App Group folder for {Group}: the widgets can't be updated", AppGroup);
        }
    }

    private const string HideDetailsKey = "widgets.hideLockScreenDetails";

    public bool IsInUse => _folder is not null;

    public bool HasLockScreenWidgets => _folder is not null;

    /// <summary>On unless turned off: a lock screen is for anyone holding the phone.</summary>
    public bool HideLockScreenDetails
    {
        get => Preferences.Default.Get(HideDetailsKey, true);
        set
        {
            Preferences.Default.Set(HideDetailsKey, value);

            // Straight away, not at the next check: re-save what's there with the new choice.
            if (_folder is not null)
            {
                var path = Path.Combine(_folder, SnapshotFile);
                Update(WidgetSnapshot.FromJson(File.Exists(path) ? File.ReadAllText(path) : null));
            }
        }
    }

    public void Update(WidgetSnapshot snapshot)
    {
        if (_folder is null)
        {
            return;
        }

        try
        {
            var path = Path.Combine(_folder, SnapshotFile);
            snapshot = snapshot.RetainEnrichment(WidgetSnapshot.FromJson(File.Exists(path) ? File.ReadAllText(path) : null));
            var temp = path + ".tmp";
            File.WriteAllText(temp, (snapshot with { HideLockScreenDetails = HideLockScreenDetails }).ToJson());
            File.Move(temp, path, overwrite: true);
            _lastSaveError = null;

            // Once each way, not every check: enough to see in the log what the widgets were last given.
            if (_lastSavedSignedIn != snapshot.SignedIn)
            {
                _lastSavedSignedIn = snapshot.SignedIn;
                _logger.LogInformation("Widget snapshot saved: {State}", snapshot.SignedIn ? "signed in" : "signed out");
            }
        }
        catch (Exception ex)
        {
            // A widget that can't update mustn't fail the alert check that fed it.
            _lastSaveError = $"{ex.GetType().Name}: {ex.Message}";
            _logger.LogWarning(ex, "Could not save the widget snapshot");
        }
    }

    public IReadOnlyList<string> DiagnosticsLines
    {
        get
        {
            string? Read(string name)
            {
                try
                {
                    var path = Path.Combine(_folder!, name);
                    return File.Exists(path) ? File.ReadAllText(path) : null;
                }
                catch (IOException)
                {
                    return null;
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
            }

            if (_folder is null)
            {
                return WidgetDiagnostics.Describe(false, null, null, null, null, DateTimeOffset.Now);
            }

            var snapshotPath = Path.Combine(_folder, SnapshotFile);
            DateTimeOffset? savedAt = File.Exists(snapshotPath) ? File.GetLastWriteTimeUtc(snapshotPath) : null;
            return WidgetDiagnostics.Describe(true, Read(SnapshotFile), savedAt, _lastSaveError, Read(WidgetDiagnostics.ReadNoteFile), DateTimeOffset.Now);
        }
    }
}
