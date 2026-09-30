using DashyNMS.Mobile.Topology;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Draws the network map (#86) as desktop's NetworkMapCanvas does: lines
/// first, then nodes, labels last so nothing covers one. A link to a device
/// that's down is dotted, several cables between a pair draw thicker, and
/// with a device selected everything not connected to it is dimmed so its
/// connections stand out. Positions are in map units, put on screen by one
/// scale and offset that the page's pan and pinch change.
/// </summary>
public sealed class NetworkMapDrawable : IDrawable
{
    public const double MinScale = 0.05;

    public const double MaxScale = 4;

    /// <summary>Below this zoom only the selection and its neighbours are labelled - a label on every node would just be noise.</summary>
    private const double LabelAllScale = 0.75;

    private readonly NetworkMapViewModel _map;

    public NetworkMapDrawable(NetworkMapViewModel map)
    {
        _map = map;
    }

    public double Scale { get; set; } = 1;

    public PointF Offset { get; set; }

    /// <summary>Node size tracks zoom, within limits - still findable zoomed out, not huge zoomed in. A little bigger than desktop's, for fingers.</summary>
    public float NodeRadius => (float)Math.Clamp(9 * Scale, 4.5, 14);

    public PointF ToScreen(NetworkNode node) => new((float)(node.X * Scale + Offset.X), (float)(node.Y * Scale + Offset.Y));

    public PointF ToMap(PointF screen) => new((float)((screen.X - Offset.X) / Scale), (float)((screen.Y - Offset.Y) / Scale));

    /// <summary>The node under a tap, within a finger's reach of its centre, nearest first.</summary>
    public NetworkNode? HitTest(PointF screen)
    {
        var reach = Math.Max(NodeRadius + 8, 22);
        NetworkNode? best = null;
        var bestDistance = double.MaxValue;
        foreach (var node in _map.Nodes)
        {
            var distance = ToScreen(node).Distance(screen);
            if (distance <= reach && distance < bestDistance)
            {
                best = node;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Zooms and pans so every node fits, with a margin; small maps stay at no more than 1:1.</summary>
    public void FitTo(Size size)
    {
        var nodes = _map.Nodes;
        if (nodes.Count == 0 || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var left = nodes.Min(n => n.X);
        var right = nodes.Max(n => n.X);
        var top = nodes.Min(n => n.Y);
        var bottom = nodes.Max(n => n.Y);

        const double margin = 40;
        Scale = Math.Clamp(
            Math.Min((size.Width - margin * 2) / Math.Max(right - left, 1), (size.Height - margin * 2) / Math.Max(bottom - top, 1)),
            MinScale,
            1);
        Offset = new PointF(
            (float)(size.Width / 2 - (left + right) / 2 * Scale),
            (float)(size.Height / 2 - (top + bottom) / 2 * Scale));
    }

    /// <summary>Brings a node to the middle, zooming in first if it's zoomed right out.</summary>
    public void CenterOn(NetworkNode node, Size size)
    {
        if (Scale < LabelAllScale)
        {
            Scale = 1;
        }

        Offset = new PointF((float)(size.Width / 2 - node.X * Scale), (float)(size.Height / 2 - node.Y * Scale));
    }

    /// <summary>Zooms by <paramref name="factor"/>, keeping the point under the fingers where it is.</summary>
    public void ZoomAround(PointF screen, double factor)
    {
        var before = ToMap(screen);
        Scale = Math.Clamp(Scale * factor, MinScale, MaxScale);
        Offset = new PointF((float)(screen.X - before.X * Scale), (float)(screen.Y - before.Y * Scale));
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var nodes = _map.Nodes;
        if (nodes.Count == 0)
        {
            return;
        }

        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var line = Colour(dark ? "TextSecondaryDark" : "TextSecondaryLight", Colors.Gray);
        var accent = Colour(dark ? "PrimaryTextDark" : "PrimaryTextLight", Colors.DodgerBlue);
        var surface = Colour(dark ? "SurfaceDark" : "SurfaceLight", Colors.Black);
        var text = Colour(dark ? "TextDark" : "TextLight", Colors.White);

        var selected = _map.SelectedNode;
        var edges = _map.Edges;
        var neighbours = selected is null
            ? new HashSet<NetworkNode>()
            : edges.Where(e => e.Touches(selected)).Select(e => e.Other(selected)).ToHashSet();
        var dimOthers = selected is not null;

        canvas.StrokeLineCap = LineCap.Round;
        foreach (var edge in edges)
        {
            var highlighted = selected is not null && edge.Touches(selected);
            canvas.StrokeColor = highlighted ? accent : line;
            canvas.StrokeSize = (float)((edge.LinkCount > 1 ? 2.5 : 1.2) * (highlighted ? 1.6 : 1));
            canvas.StrokeDashPattern = edge.IsToOfflineDevice ? [1f, 2.5f] : null;
            canvas.Alpha = highlighted ? 1 : dimOthers ? 0.15f : 0.5f;
            var a = ToScreen(edge.A);
            var b = ToScreen(edge.B);
            canvas.DrawLine(a, b);
        }

        canvas.StrokeDashPattern = null;
        var radius = NodeRadius;
        foreach (var node in nodes)
        {
            var centre = ToScreen(node);
            var faded = dimOthers && !ReferenceEquals(node, selected) && !neighbours.Contains(node);
            canvas.Alpha = faded ? 0.3f : 1;
            canvas.FillColor = StateColour(node.State);
            canvas.FillCircle(centre, radius);
            canvas.StrokeColor = surface;
            canvas.StrokeSize = 1.5f;
            canvas.DrawCircle(centre, radius);

            if (ReferenceEquals(node, selected))
            {
                canvas.StrokeColor = accent;
                canvas.StrokeSize = 3;
                canvas.DrawCircle(centre, radius + 4);
            }
        }

        // Labels last, so no node is drawn over one.
        var labelAll = Scale >= LabelAllScale;
        canvas.Alpha = 1;
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontSize = 11;
        foreach (var node in nodes)
        {
            var isFocus = ReferenceEquals(node, selected);
            var isNeighbour = neighbours.Contains(node);
            if ((!labelAll && !isFocus && !isNeighbour) || (dimOthers && !isFocus && !isNeighbour))
            {
                continue;
            }

            var centre = ToScreen(node);
            var top = centre.Y + radius + 3;

            if (isFocus)
            {
                // A backing plate, so the selected name reads over lines -
                // generous, as the measure can come out narrower than the drawn text.
                var width = canvas.GetStringSize(node.Name, Microsoft.Maui.Graphics.Font.Default, 11).Width * 1.15f;
                canvas.FillColor = surface.WithAlpha(0.85f);
                canvas.FillRoundedRectangle(new RectF(centre.X - width / 2 - 6, top, width + 12, 16), 3);
            }

            // Drawn from a point, not into a box: a box sized from the measure
            // clipped the end of longer names with "…".
            canvas.FontColor = text;
            canvas.DrawString(node.Name, centre.X, top + 12, HorizontalAlignment.Center);
        }
    }

    private static Color StateColour(DeviceState state) => state switch
    {
        DeviceState.Up => Colour("OkColor", Colors.Green),
        DeviceState.Down => Colour("CriticalColor", Colors.Red),
        DeviceState.Maintenance => Colour("MaintenanceColor", Colors.CornflowerBlue),
        _ => Color.FromArgb("#6E6E6E"),
    };

    private static Color Colour(string key, Color fallback) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color colour ? colour : fallback;
}
