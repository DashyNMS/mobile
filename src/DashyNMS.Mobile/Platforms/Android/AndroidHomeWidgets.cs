using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Android.Runtime;
using DashyNMS.Mobile.Widgets;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile;

/// <summary>The home-screen widgets DashyNMS offers on Android - one provider each.</summary>
public enum AndroidWidgetKind
{
    Alerts,
    AlertPie,
    Overview,
    Pinned,
    Sensors,
}

/// <summary>
/// DashyNMS's home-screen widgets on Android, all drawn from the snapshot the
/// last alert check saved (<see cref="WidgetSnapshot"/>) by
/// <see cref="AndroidWidgetViews"/>.
/// </summary>
public sealed class AndroidHomeWidgets : IHomeWidgets
{
    private const string SnapshotFile = "widget-snapshot.json";

    private readonly ILogger<AndroidHomeWidgets> _logger;

    public AndroidHomeWidgets(ILogger<AndroidHomeWidgets> logger) => _logger = logger;

    private static Context AppContext => global::Android.App.Application.Context;

    public bool IsInUse => Enum.GetValues<AndroidWidgetKind>().Any(kind => WidgetIds(AppContext, kind).Length > 0);

    /// <summary>Android has no lock-screen widgets for phones.</summary>
    public bool HasLockScreenWidgets => false;

    public bool HideLockScreenDetails { get; set; } = true;

    public void Update(WidgetSnapshot snapshot)
    {
        try
        {
            Save(AppContext, snapshot);
            var manager = AppWidgetManager.GetInstance(AppContext)!;
            foreach (var kind in Enum.GetValues<AndroidWidgetKind>())
            {
                var ids = WidgetIds(AppContext, kind);
                if (ids.Length > 0)
                {
                    Render(AppContext, manager, kind, ids, snapshot);
                }
            }
        }
        catch (Exception ex)
        {
            // A widget that can't redraw mustn't fail the alert check that fed it.
            _logger.LogWarning(ex, "Could not update the home-screen widgets");
        }
    }

    internal static Type ProviderOf(AndroidWidgetKind kind) => kind switch
    {
        AndroidWidgetKind.AlertPie => typeof(AlertPieWidgetProvider),
        AndroidWidgetKind.Overview => typeof(OverviewWidgetProvider),
        AndroidWidgetKind.Pinned => typeof(PinnedWidgetProvider),
        AndroidWidgetKind.Sensors => typeof(SensorsWidgetProvider),
        _ => typeof(AlertsWidgetProvider),
    };

    internal static int[] WidgetIds(Context context, AndroidWidgetKind kind) =>
        AppWidgetManager.GetInstance(context)?.GetAppWidgetIds(
            new ComponentName(context, Java.Lang.Class.FromType(ProviderOf(kind)))) ?? [];

    /// <summary>In the app's private files: a provider redraws from it after a reboot or resize, with the app not running.</summary>
    internal static WidgetSnapshot Load(Context context)
    {
        var path = System.IO.Path.Combine(context.FilesDir!.AbsolutePath, SnapshotFile);
        return WidgetSnapshot.FromJson(File.Exists(path) ? File.ReadAllText(path) : null);
    }

    private static void Save(Context context, WidgetSnapshot snapshot)
    {
        var path = System.IO.Path.Combine(context.FilesDir!.AbsolutePath, SnapshotFile);
        var temp = path + ".tmp";
        File.WriteAllText(temp, snapshot.ToJson());
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Each placed widget drawn for its own size: a resized widget shows more or less.</summary>
    internal static void Render(Context context, AppWidgetManager manager, AndroidWidgetKind kind, int[] ids, WidgetSnapshot snapshot)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var id in ids)
        {
            var size = SizeOf(manager, id);
            manager.UpdateAppWidget(id, AndroidWidgetViews.Build(context, kind, id, size, snapshot, now));
        }
    }

    /// <summary>
    /// The widget's size in dp as the launcher last reported it: the narrower
    /// width and the taller height, which is portrait on a phone.
    /// </summary>
    private static WidgetSize SizeOf(AppWidgetManager manager, int id)
    {
        var options = manager.GetAppWidgetOptions(id);
        var width = options?.GetInt(AppWidgetManager.OptionAppwidgetMinWidth) ?? 0;
        var height = options?.GetInt(AppWidgetManager.OptionAppwidgetMaxHeight) ?? 0;
        return new WidgetSize(width > 0 ? width : 250, height > 0 ? height : 180);
    }
}

/// <summary>A placed widget's size, in dp.</summary>
public readonly record struct WidgetSize(int Width, int Height);

/// <summary>
/// What the providers share: they only ever draw the saved snapshot; alert
/// checks (in the app, or WorkManager's) keep that current.
/// </summary>
internal static class WidgetProviders
{
    public static void Update(AndroidWidgetKind kind, Context? context, AppWidgetManager? manager, int[]? ids)
    {
        if (context is null || manager is null || ids is not { Length: > 0 })
        {
            return;
        }

        AndroidHomeWidgets.Render(context, manager, kind, ids, AndroidHomeWidgets.Load(context));
    }

    /// <summary>The first widget placed: make sure background checks run to feed it, whatever else is switched off.</summary>
    public static void Enabled() => new AndroidAlertScheduler().Schedule();
}

// Fixed Java names: a placed widget is tied to its provider's class name,
// which must survive app updates rather than follow the generated one.

/// <summary>Alerts in full: the worst alert when small, a list when bigger.</summary>
[Register("net/pckp/dashynms/AlertsWidgetProvider")]
[BroadcastReceiver(Label = "DashyNMS alerts", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/dashynms_widget_info")]
public sealed class AlertsWidgetProvider : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds) =>
        WidgetProviders.Update(AndroidWidgetKind.Alerts, context, appWidgetManager, appWidgetIds);

    public override void OnAppWidgetOptionsChanged(Context? context, AppWidgetManager? appWidgetManager, int appWidgetId, Bundle? newOptions) =>
        WidgetProviders.Update(AndroidWidgetKind.Alerts, context, appWidgetManager, [appWidgetId]);

    public override void OnEnabled(Context? context)
    {
        base.OnEnabled(context);
        WidgetProviders.Enabled();
    }
}

/// <summary>Devices by their worst alert, as a ring.</summary>
[Register("net/pckp/dashynms/AlertPieWidgetProvider")]
[BroadcastReceiver(Label = "DashyNMS alert pie chart", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/widget_pie_info")]
public sealed class AlertPieWidgetProvider : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds) =>
        WidgetProviders.Update(AndroidWidgetKind.AlertPie, context, appWidgetManager, appWidgetIds);

    public override void OnAppWidgetOptionsChanged(Context? context, AppWidgetManager? appWidgetManager, int appWidgetId, Bundle? newOptions) =>
        WidgetProviders.Update(AndroidWidgetKind.AlertPie, context, appWidgetManager, [appWidgetId]);

    public override void OnEnabled(Context? context)
    {
        base.OnEnabled(context);
        WidgetProviders.Enabled();
    }
}

/// <summary>The dashboard in one glance.</summary>
[Register("net/pckp/dashynms/OverviewWidgetProvider")]
[BroadcastReceiver(Label = "DashyNMS overview", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/widget_overview_info")]
public sealed class OverviewWidgetProvider : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds) =>
        WidgetProviders.Update(AndroidWidgetKind.Overview, context, appWidgetManager, appWidgetIds);

    public override void OnAppWidgetOptionsChanged(Context? context, AppWidgetManager? appWidgetManager, int appWidgetId, Bundle? newOptions) =>
        WidgetProviders.Update(AndroidWidgetKind.Overview, context, appWidgetManager, [appWidgetId]);

    public override void OnEnabled(Context? context)
    {
        base.OnEnabled(context);
        WidgetProviders.Enabled();
    }
}

/// <summary>The devices pinned in the app.</summary>
[Register("net/pckp/dashynms/PinnedWidgetProvider")]
[BroadcastReceiver(Label = "DashyNMS pinned devices", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/widget_pinned_info")]
public sealed class PinnedWidgetProvider : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds) =>
        WidgetProviders.Update(AndroidWidgetKind.Pinned, context, appWidgetManager, appWidgetIds);

    public override void OnAppWidgetOptionsChanged(Context? context, AppWidgetManager? appWidgetManager, int appWidgetId, Bundle? newOptions) =>
        WidgetProviders.Update(AndroidWidgetKind.Pinned, context, appWidgetManager, [appWidgetId]);

    public override void OnEnabled(Context? context)
    {
        base.OnEnabled(context);
        WidgetProviders.Enabled();
    }
}

/// <summary>The dashboard Sensors card's sensors.</summary>
[Register("net/pckp/dashynms/SensorsWidgetProvider")]
[BroadcastReceiver(Label = "DashyNMS sensors", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/widget_sensors_info")]
public sealed class SensorsWidgetProvider : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds) =>
        WidgetProviders.Update(AndroidWidgetKind.Sensors, context, appWidgetManager, appWidgetIds);

    public override void OnAppWidgetOptionsChanged(Context? context, AppWidgetManager? appWidgetManager, int appWidgetId, Bundle? newOptions) =>
        WidgetProviders.Update(AndroidWidgetKind.Sensors, context, appWidgetManager, [appWidgetId]);

    public override void OnEnabled(Context? context)
    {
        base.OnEnabled(context);
        WidgetProviders.Enabled();
    }
}
