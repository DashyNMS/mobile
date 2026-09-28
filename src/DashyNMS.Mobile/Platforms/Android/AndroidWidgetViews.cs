using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using Color = Android.Graphics.Color;
using Paint = Android.Graphics.Paint;
using RectF = Android.Graphics.RectF;
using DashyNMS.Mobile.Widgets;

namespace DashyNMS.Mobile;

/// <summary>
/// Draws each Android widget from the snapshot, for its size: RemoteViews
/// layouts with rows added per item (widget_row), and the pie and bars drawn
/// as bitmaps, since RemoteViews can't host a custom view.
/// </summary>
/// <remarks>
/// A RemoteViews call naming a view its layout doesn't have breaks the whole
/// widget ("Can't load widget"), so each layout's views are only touched by
/// the code that inflates it.
/// </remarks>
internal static class AndroidWidgetViews
{
    /// <summary>Taller than this, the alert list shows acknowledged alerts too and wraps rules in full (the large size).</summary>
    private const int LargeHeight = 280;

    /// <summary>Narrower or shorter than this, the alerts and pie widgets use their small layouts.</summary>
    private const int SmallSize = 200;

    private const int HeaderHeight = 52;
    private const int RowHeight = 38;

    public static RemoteViews Build(Context context, AndroidWidgetKind kind, int widgetId, WidgetSize size, WidgetSnapshot snapshot, DateTimeOffset now)
    {
        var slots = new Slots(widgetId);
        if (!snapshot.SignedIn)
        {
            return SignedOut(context, slots);
        }

        return kind switch
        {
            AndroidWidgetKind.AlertPie => size.Width < SmallSize ? PieSmall(context, slots, snapshot) : Pie(context, slots, size, snapshot, now),
            AndroidWidgetKind.Overview => Overview(context, slots, size, snapshot, now),
            AndroidWidgetKind.Pinned => Pinned(context, slots, size, snapshot, now),
            AndroidWidgetKind.Sensors => Sensors(context, slots, size, snapshot, now),
            _ => size.Width < SmallSize && size.Height < SmallSize
                ? AlertsSmall(context, slots, snapshot, now)
                : Alerts(context, slots, size, snapshot, now),
        };
    }

    private static RemoteViews SignedOut(Context context, Slots slots)
    {
        var views = List(context, slots, "DashyNMS", string.Empty);
        Message(views, "Open DashyNMS to sign in.");
        return views;
    }

    // ---- Alerts ----

    private static RemoteViews AlertsSmall(Context context, Slots slots, WidgetSnapshot snapshot, DateTimeOffset now)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_alerts_small);
        views.SetOnClickPendingIntent(Resource.Id.widget_root, slots.Open(context, deviceId: null, alertId: null));
        views.SetTextViewText(Resource.Id.widget_counts, Spans(context, "  ",
            (WidgetFormat.Count(snapshot.Critical), Resource.Color.widget_critical_text),
            (WidgetFormat.Count(snapshot.Warning), Resource.Color.widget_warning_text),
            (WidgetFormat.Count(snapshot.Acknowledged), Resource.Color.widget_acknowledged_text)));

        var worst = snapshot.Alerts.FirstOrDefault(a => !a.Acknowledged);
        if (worst is null)
        {
            views.SetViewVisibility(Resource.Id.widget_badge, ViewStates.Gone);
            views.SetTextViewText(Resource.Id.widget_rule, "No active alerts.");
            views.SetTextColor(Resource.Id.widget_rule, ColourOf(context, Resource.Color.widget_ok_text));
            views.SetTextViewText(Resource.Id.widget_device, string.Empty);
            views.SetTextViewText(Resource.Id.widget_footer, WidgetFormat.Checked(snapshot.CheckedAt, now));
            return views;
        }

        var critical = worst.Severity == "critical";
        views.SetViewVisibility(Resource.Id.widget_badge, ViewStates.Visible);
        views.SetTextViewText(Resource.Id.widget_badge, critical ? "CRITICAL" : "WARNING");
        views.SetTextColor(Resource.Id.widget_badge, ColourOf(context, critical ? Resource.Color.widget_critical_text : Resource.Color.widget_warning_text));
        views.SetInt(Resource.Id.widget_badge, "setBackgroundResource", critical ? Resource.Drawable.widget_badge_critical : Resource.Drawable.widget_badge_warning);
        views.SetTextViewText(Resource.Id.widget_rule, worst.Rule);
        views.SetTextColor(Resource.Id.widget_rule, ColourOf(context, Resource.Color.widget_text));
        views.SetTextViewText(Resource.Id.widget_device, worst.Device);

        var age = WidgetFormat.Age(worst.RaisedAt, now);
        var more = snapshot.Active - 1;
        views.SetTextViewText(Resource.Id.widget_footer, string.Join(" · ", new[]
        {
            age.Length == 0 ? null : age == "just now" ? age : $"{age} ago",
            more > 0 ? $"{more} more" : null,
        }.Where(p => p is not null)));
        views.SetOnClickPendingIntent(Resource.Id.widget_root, slots.Open(context, worst.DeviceId, worst.AlertId));
        return views;
    }

    private static RemoteViews Alerts(Context context, Slots slots, WidgetSize size, WidgetSnapshot snapshot, DateTimeOffset now)
    {
        var large = size.Height >= LargeHeight;
        var views = List(context, slots, "Alerts", WidgetFormat.Checked(snapshot.CheckedAt, now));
        Summary(views, context, snapshot);

        var alerts = (large ? snapshot.Alerts : snapshot.Alerts.Where(a => !a.Acknowledged).ToList())
            .Take(Capacity(size, large ? 44 : RowHeight, WidgetSnapshot.MaxAlerts))
            .ToList();
        if (alerts.Count == 0)
        {
            Message(views, snapshot.Alerts.Count == 0 ? "No open alerts." : "No active alerts.");
            return views;
        }

        foreach (var alert in alerts)
        {
            var age = WidgetFormat.Age(alert.RaisedAt, now);
            views.AddView(Resource.Id.widget_rows, large
                ? Row(context, alert.Acknowledged ? "acknowledged" : alert.Severity, alert.Rule,
                    string.Join(" · ", new[] { alert.Device, age.Length == 0 ? null : age, alert.Acknowledged ? "Acknowledged" : null }.Where(p => p is not null)),
                    trailing: null, slots.Open(context, alert.DeviceId, alert.AlertId), titleLines: 2, dim: alert.Acknowledged)
                : Row(context, alert.Severity, alert.Rule, alert.Device, age, slots.Open(context, alert.DeviceId, alert.AlertId)));
        }

        return views;
    }

    /// <summary>"2 critical  3 warning  1 ack'd", each in its colour.</summary>
    private static void Summary(RemoteViews views, Context context, WidgetSnapshot snapshot, bool withDown = false)
    {
        var parts = new List<(string, int)>
        {
            ($"{snapshot.Critical} critical", Resource.Color.widget_critical_text),
            ($"{snapshot.Warning} warning", Resource.Color.widget_warning_text),
        };
        if (snapshot.Acknowledged > 0)
        {
            parts.Add(($"{snapshot.Acknowledged} ack'd", Resource.Color.widget_acknowledged_text));
        }

        if (withDown && snapshot.DevicesDown is { } down)
        {
            parts.Add(($"{down} down", down > 0 ? Resource.Color.widget_critical_text : Resource.Color.widget_text_secondary));
        }

        views.SetViewVisibility(Resource.Id.widget_summary, ViewStates.Visible);
        views.SetTextViewText(Resource.Id.widget_summary, Spans(context, "   ", parts.ToArray()));
    }

    // ---- Pie ----

    private static RemoteViews PieSmall(Context context, Slots slots, WidgetSnapshot snapshot)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_pie_small);
        views.SetOnClickPendingIntent(Resource.Id.widget_root, slots.Open(context, deviceId: null, alertId: null));
        if (snapshot.Devices is not { } counts)
        {
            views.SetViewVisibility(Resource.Id.widget_counts, ViewStates.Gone);
            views.SetViewVisibility(Resource.Id.widget_message, ViewStates.Visible);
            views.SetTextViewText(Resource.Id.widget_message, "Waiting for the device list - open DashyNMS.");
            return views;
        }

        var problems = counts.Critical + counts.Warning + counts.Acknowledged;
        views.SetImageViewBitmap(Resource.Id.widget_pie, WidgetDrawing.Pie(
            context, counts, sizeDp: 110, strokeDp: 13,
            centre: problems == 0 ? "All" : problems.ToString(System.Globalization.CultureInfo.CurrentCulture),
            label: problems == 0 ? "OK" : "need a look"));
        views.SetTextViewText(Resource.Id.widget_counts, Spans(context, "  ",
            (counts.Critical.ToString(System.Globalization.CultureInfo.CurrentCulture), Resource.Color.widget_critical_text),
            (counts.Warning.ToString(System.Globalization.CultureInfo.CurrentCulture), Resource.Color.widget_warning_text),
            (counts.Acknowledged.ToString(System.Globalization.CultureInfo.CurrentCulture), Resource.Color.widget_acknowledged_text),
            (counts.Ok.ToString(System.Globalization.CultureInfo.CurrentCulture), Resource.Color.widget_ok_text)));
        return views;
    }

    private static RemoteViews Pie(Context context, Slots slots, WidgetSize size, WidgetSnapshot snapshot, DateTimeOffset now)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_pie);
        views.SetOnClickPendingIntent(Resource.Id.widget_root, slots.Open(context, deviceId: null, alertId: null));
        views.RemoveAllViews(Resource.Id.widget_legend);
        views.RemoveAllViews(Resource.Id.widget_rows);

        if (snapshot.Devices is not { } counts)
        {
            views.SetViewVisibility(Resource.Id.widget_message, ViewStates.Visible);
            views.SetTextViewText(Resource.Id.widget_message, "Waiting for the device list - open DashyNMS.");
            views.SetTextViewText(Resource.Id.widget_checked, string.Empty);
            return views;
        }

        var large = size.Height >= LargeHeight;
        views.SetImageViewBitmap(Resource.Id.widget_pie, WidgetDrawing.Pie(
            context, counts, sizeDp: 120, strokeDp: 14,
            centre: large ? WidgetFormat.OkShare(counts) : PercentOk(counts),
            label: large ? "devices OK" : "OK"));

        foreach (var (label, value, state) in new[]
        {
            ("Critical", counts.Critical, "critical"),
            ("Warning", counts.Warning, "warning"),
            ("Acknowledged", counts.Acknowledged, "acknowledged"),
            ("OK", counts.Ok, "ok"),
        })
        {
            var row = Row(context, state, label, subtitle: null, value.ToString(System.Globalization.CultureInfo.CurrentCulture), click: null);
            row.SetTextColor(Resource.Id.widget_row_trailing, ColourOf(context, Resource.Color.widget_text));
            views.AddView(Resource.Id.widget_legend, row);
        }

        var watched = counts.Critical + counts.Warning + counts.Acknowledged + counts.Ok;
        views.SetTextViewText(Resource.Id.widget_checked, $"{watched} devices · {WidgetFormat.Age(snapshot.CheckedAt, now)}");

        if (large)
        {
            // The devices behind the red and amber: each device's worst active alert, once.
            var devices = snapshot.Alerts.Where(a => !a.Acknowledged).DistinctBy(a => a.DeviceId).ToList();
            views.SetViewVisibility(Resource.Id.widget_section, ViewStates.Visible);
            if (devices.Count == 0)
            {
                views.SetViewVisibility(Resource.Id.widget_message, ViewStates.Visible);
                views.SetTextViewText(Resource.Id.widget_message, "Every device is OK.");
            }

            foreach (var alert in devices.Take(Capacity(size with { Height = size.Height - 150 }, RowHeight, 5)))
            {
                views.AddView(Resource.Id.widget_rows, Row(context, alert.Severity, alert.Device, alert.Rule, trailing: null, slots.Open(context, alert.DeviceId, alert.AlertId)));
            }
        }

        return views;
    }

    private static string PercentOk(WidgetDeviceCounts counts)
    {
        var watched = counts.Critical + counts.Warning + counts.Acknowledged + counts.Ok;
        return watched == 0 ? "–" : $"{counts.Ok * 100 / watched}%";
    }

    // ---- Overview ----

    private static RemoteViews Overview(Context context, Slots slots, WidgetSize size, WidgetSnapshot snapshot, DateTimeOffset now)
    {
        var views = List(context, slots, "Network", WidgetFormat.Checked(snapshot.CheckedAt, now));

        if (snapshot.Devices is { } counts)
        {
            views.SetViewVisibility(Resource.Id.widget_image, ViewStates.Visible);
            views.SetImageViewBitmap(Resource.Id.widget_image, WidgetDrawing.Bar(context, WidgetFormat.PieSlices(counts), heightDp: 10));
            views.SetViewVisibility(Resource.Id.widget_detail, ViewStates.Visible);
            var parts = new List<string> { $"{counts.Up} up", $"{counts.Down} down" };
            if (counts.Disabled > 0)
            {
                parts.Add($"{counts.Disabled} disabled");
            }

            parts.Add($"{counts.Total} devices");
            views.SetTextViewText(Resource.Id.widget_detail, string.Join(" · ", parts));
        }

        Summary(views, context, snapshot, withDown: true);
        views.SetViewVisibility(Resource.Id.widget_section, ViewStates.Visible);
        views.SetTextViewText(Resource.Id.widget_section, "Top alerts");

        var alerts = snapshot.Alerts.Where(a => !a.Acknowledged).Take(Capacity(size with { Height = size.Height - 70 }, RowHeight, 5)).ToList();
        if (alerts.Count == 0)
        {
            Message(views, "No active alerts.");
        }

        foreach (var alert in alerts)
        {
            var age = WidgetFormat.Age(alert.RaisedAt, now);
            views.AddView(Resource.Id.widget_rows, Row(
                context, alert.Severity, alert.Rule, alert.Device, age, slots.Open(context, alert.DeviceId, alert.AlertId)));
        }

        return views;
    }

    // ---- Pinned devices ----

    private static RemoteViews Pinned(Context context, Slots slots, WidgetSize size, WidgetSnapshot snapshot, DateTimeOffset now)
    {
        var pinned = snapshot.Pinned;
        var down = pinned.Count(p => p.State == "down");
        var up = pinned.Count(p => p.State is not ("down" or "disabled"));
        var views = List(context, slots, "Pinned", pinned.Count == 0 ? string.Empty : down > 0 ? $"{up} up · {down} down" : $"{up} up");

        if (pinned.Count == 0)
        {
            Message(views, "Pin devices in DashyNMS to see them here.");
            return views;
        }

        foreach (var device in pinned.Take(Capacity(size, RowHeight, WidgetSnapshot.MaxRows)))
        {
            var row = Row(context, device.State, device.Name, WidgetFormat.DeviceStatus(device, now), device.Location, slots.Open(context, device.DeviceId, alertId: null));
            row.SetTextColor(Resource.Id.widget_row_subtitle, ColourOf(context, StateTextColour(device.State)));
            views.AddView(Resource.Id.widget_rows, row);
        }

        return views;
    }

    // ---- Sensors ----

    private static RemoteViews Sensors(Context context, Slots slots, WidgetSize size, WidgetSnapshot snapshot, DateTimeOffset now)
    {
        var views = List(context, slots, "Sensors", WidgetFormat.Checked(snapshot.SensorsReadAt, now, "Read"));
        if (snapshot.Sensors.Count == 0)
        {
            Message(views, "Pick sensors for the dashboard's Sensors card in DashyNMS to see them here.");
            return views;
        }

        foreach (var sensor in snapshot.Sensors.Take(Capacity(size, 46, WidgetSnapshot.MaxRows)))
        {
            var row = Row(context, sensor.Status, sensor.Name, sensor.Device, sensor.Value, slots.Open(context, sensor.DeviceId, alertId: null));
            row.SetTextColor(Resource.Id.widget_row_trailing, ColourOf(context, sensor.Status switch
            {
                "critical" => Resource.Color.widget_critical_text,
                "warning" => Resource.Color.widget_warning_text,
                "unknown" => Resource.Color.widget_text_secondary,
                _ => Resource.Color.widget_text,
            }));
            if (sensor.Position is { } position)
            {
                row.SetViewVisibility(Resource.Id.widget_row_bar, ViewStates.Visible);
                row.SetImageViewBitmap(Resource.Id.widget_row_bar, WidgetDrawing.Level(context, position, StateColour(sensor.Status)));
            }

            views.AddView(Resource.Id.widget_rows, row);
        }

        return views;
    }

    // ---- Shared pieces ----

    /// <summary>widget_list with its title and checked time set, rows emptied, and a tap opening the alert list.</summary>
    private static RemoteViews List(Context context, Slots slots, string title, string checkedText)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_list);
        views.SetOnClickPendingIntent(Resource.Id.widget_root, slots.Open(context, deviceId: null, alertId: null));
        views.SetTextViewText(Resource.Id.widget_title, title);
        views.SetTextViewText(Resource.Id.widget_checked, checkedText);
        views.RemoveAllViews(Resource.Id.widget_rows);
        return views;
    }

    private static void Message(RemoteViews views, string text)
    {
        views.SetViewVisibility(Resource.Id.widget_message, ViewStates.Visible);
        views.SetTextViewText(Resource.Id.widget_message, text);
    }

    private static RemoteViews Row(
        Context context, string state, string title, string? subtitle, string? trailing, PendingIntent? click, int titleLines = 1, bool dim = false)
    {
        var row = new RemoteViews(context.PackageName, Resource.Layout.widget_row);
        row.SetTextColor(Resource.Id.widget_row_dot, new Color(StateColour(state)));
        row.SetTextViewText(Resource.Id.widget_row_title, title);
        row.SetInt(Resource.Id.widget_row_title, "setMaxLines", titleLines);
        if (dim)
        {
            row.SetTextColor(Resource.Id.widget_row_title, ColourOf(context, Resource.Color.widget_text_secondary));
        }

        row.SetViewVisibility(Resource.Id.widget_row_subtitle, string.IsNullOrEmpty(subtitle) ? ViewStates.Gone : ViewStates.Visible);
        row.SetTextViewText(Resource.Id.widget_row_subtitle, subtitle ?? string.Empty);
        row.SetViewVisibility(Resource.Id.widget_row_trailing, string.IsNullOrEmpty(trailing) ? ViewStates.Gone : ViewStates.Visible);
        row.SetTextViewText(Resource.Id.widget_row_trailing, trailing ?? string.Empty);
        if (click is not null)
        {
            row.SetOnClickPendingIntent(Resource.Id.widget_row, click);
        }

        return row;
    }

    /// <summary>How many rows fit under the header at this height.</summary>
    private static int Capacity(WidgetSize size, int rowHeight, int max) =>
        Math.Clamp((size.Height - HeaderHeight) / rowHeight, 1, max);

    private static Java.Lang.ICharSequence Spans(Context context, string separator, params (string Text, int ColourId)[] parts)
    {
        var builder = new SpannableStringBuilder();
        foreach (var (text, colourId) in parts)
        {
            if (builder.Length() > 0)
            {
                builder.Append(separator);
            }

            var start = builder.Length();
            builder.Append(text);
            builder.SetSpan(new ForegroundColorSpan(ColourOf(context, colourId)), start, builder.Length(), SpanTypes.ExclusiveExclusive);
        }

        return builder;
    }

    /// <summary>A dot's colour (ARGB) for a severity or state key from the snapshot.</summary>
    internal static int StateColour(string state) => state switch
    {
        "critical" or "down" => unchecked((int)0xFFDA3633),
        "warning" => unchecked((int)0xFFDB9A04),
        "acknowledged" => unchecked((int)0xFF6E7A91),
        "ok" => unchecked((int)0xFF2EA043),
        "disabled" => unchecked((int)0xFF6E6E6E),
        _ => unchecked((int)0xFF8B93A1),
    };

    private static int StateTextColour(string state) => state switch
    {
        "critical" or "down" => Resource.Color.widget_critical_text,
        "warning" => Resource.Color.widget_warning_text,
        "acknowledged" => Resource.Color.widget_acknowledged_text,
        _ => Resource.Color.widget_text_secondary,
    };

    private static Color ColourOf(Context context, int colourId) => new(context.GetColor(colourId));

    /// <summary>
    /// Tap targets for one placed widget. Each needs its own request code, or
    /// PendingIntents that differ only in extras would share (and overwrite)
    /// one another - across rows, and across widgets.
    /// </summary>
    private sealed class Slots(int widgetId)
    {
        /// <summary>Clear of the notifications' request codes (their tags' hash codes are spread across all ints, but these are few).</summary>
        private const int RequestBase = 0x5749_4400;

        private int _next;

        /// <summary>Opens the app as a notification tap does (MainActivity hands it to the NotificationRouter).</summary>
        public PendingIntent Open(Context context, int? deviceId, int? alertId)
        {
            var intent = new Intent(context, typeof(MainActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            intent.PutExtra(AndroidAlertNotifier.ExtraFromNotification, true);
            if (deviceId is { } device)
            {
                intent.PutExtra(AndroidAlertNotifier.ExtraDeviceId, device);
            }

            if (alertId is { } alert)
            {
                intent.PutExtra(AndroidAlertNotifier.ExtraAlertId, alert);
            }

            var requestCode = RequestBase + ((widgetId & 0xFFFF) << 5) + (_next++ & 0x1F);
            return PendingIntent.GetActivity(
                context, requestCode, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
        }
    }
}

/// <summary>The bits RemoteViews can't draw themselves: the pie, the overview's bar and sensor levels.</summary>
internal static class WidgetDrawing
{
    private static readonly Color Track = new(unchecked((int)0xFF262C38));
    private static readonly Color TextColour = new(unchecked((int)0xFFE6EAF0));
    private static readonly Color Secondary = new(unchecked((int)0xFF8B93A1));

    /// <summary>A ring of slices with a number and a label in the middle.</summary>
    public static Bitmap Pie(Context context, WidgetDeviceCounts counts, float sizeDp, float strokeDp, string centre, string label)
    {
        var density = context.Resources!.DisplayMetrics!.Density;
        var px = (int)(sizeDp * density);
        var bitmap = Bitmap.CreateBitmap(px, px, Bitmap.Config.Argb8888!)!;
        using var canvas = new Canvas(bitmap);

        var stroke = strokeDp * density;
        var inset = stroke / 2;
        using var rect = new RectF(inset, inset, px - inset, px - inset);
        using var ring = new Paint(PaintFlags.AntiAlias) { StrokeWidth = stroke };
        ring.SetStyle(Paint.Style.Stroke);
        ring.Color = Track;
        canvas.DrawArc(rect, 0, 360, false, ring);

        var slices = WidgetFormat.PieSlices(counts);
        var gap = slices.Count > 1 ? 2.5f : 0f;
        var start = -90f;
        foreach (var (state, fraction) in slices)
        {
            var sweep = (float)(fraction * 360);
            ring.Color = new Color(AndroidWidgetViews.StateColour(state));
            canvas.DrawArc(rect, start + gap / 2, Math.Max(sweep - gap, 1f), false, ring);
            start += sweep;
        }

        using var value = new Paint(PaintFlags.AntiAlias) { TextAlign = Paint.Align.Center, Color = TextColour, TextSize = sizeDp * 0.19f * density };
        value.SetTypeface(Typeface.Create(Typeface.Monospace, TypefaceStyle.Bold));
        var maxWidth = px - stroke * 2.6f;
        if (value.MeasureText(centre) > maxWidth)
        {
            value.TextSize *= maxWidth / value.MeasureText(centre);
        }

        using var caption = new Paint(PaintFlags.AntiAlias) { TextAlign = Paint.Align.Center, Color = Secondary, TextSize = sizeDp * 0.09f * density };
        canvas.DrawText(centre, px / 2f, px / 2f + value.TextSize * 0.2f, value);
        canvas.DrawText(label, px / 2f, px / 2f + value.TextSize * 0.2f + caption.TextSize * 1.3f, caption);
        return bitmap;
    }

    /// <summary>A rounded bar split into slices, stretched to the widget's width by its ImageView.</summary>
    public static Bitmap Bar(Context context, IReadOnlyList<(string State, double Fraction)> slices, float heightDp)
    {
        var density = context.Resources!.DisplayMetrics!.Density;
        var width = (int)(320 * density);
        var height = Math.Max(1, (int)(heightDp * density));
        var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!)!;
        using var canvas = new Canvas(bitmap);
        using var paint = new Paint(PaintFlags.AntiAlias);

        // The whole bar's rounded outline, then the slices clipped to it.
        using var outline = new Android.Graphics.Path();
        using var bounds = new RectF(0, 0, width, height);
        outline.AddRoundRect(bounds, height / 2f, height / 2f, Android.Graphics.Path.Direction.Cw!);
        canvas.ClipPath(outline);
        paint.Color = Track;
        canvas.DrawRect(bounds, paint);

        var gap = slices.Count > 1 ? 2 * density : 0;
        var x = 0f;
        foreach (var (state, fraction) in slices)
        {
            var w = (float)(fraction * width);
            paint.Color = new Color(AndroidWidgetViews.StateColour(state));
            canvas.DrawRect(x, 0, Math.Max(x + 1, x + w - gap), height, paint);
            x += w;
        }

        return bitmap;
    }

    /// <summary>Where a reading sits between its limits.</summary>
    public static Bitmap Level(Context context, double position, int colour)
    {
        var density = context.Resources!.DisplayMetrics!.Density;
        var width = (int)(300 * density);
        var height = Math.Max(1, (int)(4 * density));
        var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!)!;
        using var canvas = new Canvas(bitmap);
        using var paint = new Paint(PaintFlags.AntiAlias) { Color = Track };
        var radius = height / 2f;
        canvas.DrawRoundRect(0, 0, width, height, radius, radius, paint);
        paint.Color = new Color(colour);
        canvas.DrawRoundRect(0, 0, Math.Max(height, (float)(Math.Clamp(position, 0, 1) * width)), height, radius, radius, paint);
        return bitmap;
    }
}
