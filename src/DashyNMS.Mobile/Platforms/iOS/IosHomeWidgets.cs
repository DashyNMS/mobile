using DashyNMS.Mobile.Widgets;
using Foundation;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile;

/// <summary>
/// Feeds the WidgetKit extension (ios-widget/): the snapshot goes in the App
/// Group folder the app and widget share, and the widget re-reads it on its
/// own schedule.
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

    public IosHomeWidgets(ILogger<IosHomeWidgets> logger)
    {
        _logger = logger;
        _folder = NSFileManager.DefaultManager.GetContainerUrl(AppGroup)?.Path;
    }

    public bool IsInUse => _folder is not null;

    public void Update(WidgetSnapshot snapshot)
    {
        if (_folder is null)
        {
            return;
        }

        try
        {
            var path = Path.Combine(_folder, SnapshotFile);
            var temp = path + ".tmp";
            File.WriteAllText(temp, snapshot.ToJson());
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            // A widget that can't update mustn't fail the alert check that fed it.
            _logger.LogWarning(ex, "Could not save the widget snapshot");
        }
    }
}
