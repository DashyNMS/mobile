using System.Globalization;
using DashyNMS.Mobile.Converters;
using DashyNMS.Mobile.DeviceSections;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// A scrollable star around this device. Native buttons keep every neighbour
/// reachable by touch and assistive technology even on a busy switch.
/// </summary>
public sealed class DeviceNeighbourGraph : ContentView
{
    private readonly AbsoluteLayout _layout = new();
    private readonly LinkDrawing _drawing = new();
    private readonly GraphicsView _lines;
    private IReadOnlyList<SectionRow> _rows = [];
    private string _device = string.Empty;
    private double _lastWidth;

    public event Action<SectionRow>? Picked;

    public DeviceNeighbourGraph()
    {
        _lines = new GraphicsView { Drawable = _drawing, InputTransparent = true };
        Content = new ScrollView { Content = _layout };
        SizeChanged += (_, _) =>
        {
            if (Width > 0 && Math.Abs(Width - _lastWidth) > 1) Rebuild();
        };
    }

    public void Update(IReadOnlyList<SectionRow> rows, string device)
    {
        _rows = rows;
        _device = device;
        Rebuild();
    }

    private void Rebuild()
    {
        if (Width <= 0) return;
        _lastWidth = Width;
        _layout.Children.Clear();
        _drawing.Links.Clear();
        var height = Math.Max(240, Math.Ceiling(_rows.Count / 2.0) * 76 + 24);
        _layout.HeightRequest = height;
        _layout.Children.Add(_lines);
        AbsoluteLayout.SetLayoutBounds(_lines, new Rect(0, 0, Width, height));
        var centre = new PointF((float)(Width / 2), (float)(height / 2));
        var nodeWidth = Math.Max(70, Math.Min(140, Width * 0.29));
        var converter = new StatusColorConverter();
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var text = dark ? Colors.White : Colors.Black;
        AddNode(_device, Width / 2 - nodeWidth / 2, height / 2 - 30, Colors.SlateGray, null);
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var x = i % 2 == 0 ? 0 : Width - nodeWidth;
            var y = 12 + (i / 2) * 76;
            var color = converter.Convert(row.Status, typeof(Color), null, CultureInfo.InvariantCulture) as Color ?? Colors.Gray;
            _drawing.Links.Add((centre, new PointF((float)(x + nodeWidth / 2), y + 30), color));
            AddNode(row.Title, x, y, color, row);
        }
        _lines.Invalidate();

        void AddNode(string name, double x, double y, Color color, SectionRow? row)
        {
            var button = new Button
            {
                Text = name, FontSize = 12, Padding = new Thickness(5),
                TextColor = text, BackgroundColor = dark ? Color.FromArgb("#20252C") : Colors.White,
                BorderColor = color, BorderWidth = 2, CornerRadius = 12,
            };
            SemanticProperties.SetDescription(button, row is null ? "Current device: " + name
                : "Show neighbour card: " + name + ", " + row.Subtitle + ", " + row.Fields.FirstOrDefault()?.Value);
            if (row is not null) button.Clicked += (_, _) => Picked?.Invoke(row);
            _layout.Children.Add(button);
            AbsoluteLayout.SetLayoutBounds(button, new Rect(x, y, nodeWidth, 60));
        }
    }

    private sealed class LinkDrawing : IDrawable
    {
        public List<(PointF From, PointF To, Color Color)> Links { get; } = [];
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.StrokeSize = 2;
            foreach (var link in Links)
            {
                canvas.StrokeColor = link.Color;
                canvas.DrawLine(link.From, link.To);
            }
        }
    }
}
