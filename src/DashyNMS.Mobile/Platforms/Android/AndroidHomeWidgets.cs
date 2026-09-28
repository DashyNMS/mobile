using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using DashyNMS.Mobile.Widgets;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile;

/// <summary>
/// DashyNMS's home-screen widget on Android: open alerts by severity,
/// devices down, and the top alerts, drawn from the snapshot the last alert
/// check saved (<see cref="WidgetSnapshot"/>).
/// </summary>
public sealed class AndroidHomeWidgets : IHomeWidgets
{
    private const string SnapshotFile = "widget-snapshot.json";

    private readonly ILogger<AndroidHomeWidgets> _logger;

    public AndroidHomeWidgets(ILogger<AndroidHomeWidgets> logger) => _logger = logger;

    private static Context AppContext => global::Android.App.Application.Context;

    public bool IsInUse => WidgetIds(AppContext).Length > 0;

    public void Update(WidgetSnapshot snapshot)
    {
        try
        {
            Save(AppContext, snapshot);
            var ids = WidgetIds(AppContext);
            if (ids.Length > 0)
            {
                Render(AppContext, AppWidgetManager.GetInstance(AppContext)!, ids, snapshot);
            }
        }
        catch (Exception ex)
        {
            // A widget that can't redraw mustn't fail the alert check that fed it.
            _logger.LogWarning(ex, "Could not update the home-screen widget");
        }
    }

    internal static int[] WidgetIds(Context context) =>
        AppWidgetManager.GetInstance(context)?.GetAppWidgetIds(
            new ComponentName(context, Java.Lang.Class.FromType(typeof(AlertsWidgetProvider)))) ?? [];

    /// <summary>In the app's private files: the widget provider redraws from it after a reboot or resize, with the app not running.</summary>
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

    internal static void Render(Context context, AppWidgetManager manager, int[] ids, WidgetSnapshot snapshot)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.dashynms_widget);

        // Anywhere else on the widget: the alert list.
        views.SetOnClickPendingIntent(Resource.Id.widget_root, OpenIntent(context, alert: null, requestCode: 0));

        if (!snapshot.SignedIn)
        {
            views.SetViewVisibility(Resource.Id.widget_counts, ViewStates.Gone);
            views.SetViewVisibility(Resource.Id.widget_message, ViewStates.Visible);
            views.SetTextViewText(Resource.Id.widget_message, "Open DashyNMS to sign in.");
            views.SetTextViewText(Resource.Id.widget_checked, string.Empty);
            ShowAlerts(context, views, []);
            manager.UpdateAppWidget(ids, views);
            return;
        }

        views.SetViewVisibility(Resource.Id.widget_counts, ViewStates.Visible);
        views.SetTextViewText(Resource.Id.widget_critical_count, Count(snapshot.Critical));
        views.SetTextViewText(Resource.Id.widget_warning_count, Count(snapshot.Warning));
        views.SetTextViewText(Resource.Id.widget_down_count, snapshot.DevicesDown is { } down ? Count(down) : "–");
        views.SetTextColor(
            Resource.Id.widget_down_count,
            ColorOf(context, snapshot.DevicesDown > 0 ? Resource.Color.widget_critical : Resource.Color.widget_text));

        // The time of the last check, so a stale widget looks stale.
        var checkedAt = DateTimeOffset.FromUnixTimeSeconds(snapshot.CheckedAt).ToLocalTime();
        views.SetTextViewText(Resource.Id.widget_checked, checkedAt.ToString("t", System.Globalization.CultureInfo.CurrentCulture));

        var allClear = snapshot.Active == 0;
        views.SetViewVisibility(Resource.Id.widget_message, allClear ? ViewStates.Visible : ViewStates.Gone);
        views.SetTextViewText(Resource.Id.widget_message, "No active alerts.");

        ShowAlerts(context, views, snapshot.Alerts);
        manager.UpdateAppWidget(ids, views);
    }

    private static readonly (int Row, int Dot, int Rule, int Device)[] Rows =
    [
        (Resource.Id.widget_alert_1, Resource.Id.widget_alert_1_dot, Resource.Id.widget_alert_1_rule, Resource.Id.widget_alert_1_device),
        (Resource.Id.widget_alert_2, Resource.Id.widget_alert_2_dot, Resource.Id.widget_alert_2_rule, Resource.Id.widget_alert_2_device),
        (Resource.Id.widget_alert_3, Resource.Id.widget_alert_3_dot, Resource.Id.widget_alert_3_rule, Resource.Id.widget_alert_3_device),
    ];

    private static void ShowAlerts(Context context, RemoteViews views, IReadOnlyList<WidgetAlert> alerts)
    {
        for (var i = 0; i < Rows.Length; i++)
        {
            var (row, dot, rule, device) = Rows[i];
            if (i >= alerts.Count)
            {
                views.SetViewVisibility(row, ViewStates.Gone);
                continue;
            }

            var alert = alerts[i];
            views.SetViewVisibility(row, ViewStates.Visible);
            views.SetTextViewText(rule, alert.Rule);
            views.SetTextViewText(device, alert.Device);
            views.SetTextColor(dot, ColorOf(context, alert.Severity switch
            {
                "critical" => Resource.Color.widget_critical,
                "warning" => Resource.Color.widget_warning,
                _ => Resource.Color.widget_ok,
            }));
            views.SetOnClickPendingIntent(row, OpenIntent(context, alert, requestCode: i + 1));
        }
    }

    private static global::Android.Graphics.Color ColorOf(Context context, int colourId) => new(context.GetColor(colourId));

    private static string Count(int value) => value > 99 ? "99+" : value.ToString(System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// Opens the app as a notification tap does (MainActivity hands it to the
    /// NotificationRouter): the alert's own page, or the alert list.
    /// </summary>
    private static PendingIntent OpenIntent(Context context, WidgetAlert? alert, int requestCode)
    {
        var intent = new Intent(context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        intent.PutExtra(AndroidAlertNotifier.ExtraFromNotification, true);
        if (alert is not null)
        {
            intent.PutExtra(AndroidAlertNotifier.ExtraDeviceId, alert.DeviceId);
            intent.PutExtra(AndroidAlertNotifier.ExtraAlertId, alert.AlertId);
        }

        // Its own request code per row, or the rows would share (and
        // overwrite) one PendingIntent's extras.
        return PendingIntent.GetActivity(
            context,
            WidgetRequestBase + requestCode,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    /// <summary>Clear of the notifications' request codes (their tags' hash codes are spread across all ints, but these are few).</summary>
    private const int WidgetRequestBase = 0x5749_4400;
}

/// <summary>
/// The widget itself, as Android sees it. It only ever draws the saved
/// snapshot; alert checks (in the app, or WorkManager's) keep that current.
/// </summary>
/// <remarks>
/// A fixed Java name: a placed widget is tied to the provider's class name,
/// which must survive app updates rather than follow the generated one.
/// </remarks>
[Register("net/pckp/dashynms/AlertsWidgetProvider")]
[BroadcastReceiver(Label = "DashyNMS alerts", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/dashynms_widget_info")]
public sealed class AlertsWidgetProvider : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context is null || appWidgetManager is null || appWidgetIds is not { Length: > 0 })
        {
            return;
        }

        AndroidHomeWidgets.Render(context, appWidgetManager, appWidgetIds, AndroidHomeWidgets.Load(context));
    }

    /// <summary>The first widget placed: make sure background checks run to feed it, whatever else is switched off.</summary>
    public override void OnEnabled(Context? context)
    {
        base.OnEnabled(context);
        new AndroidAlertScheduler().Schedule();
    }
}
