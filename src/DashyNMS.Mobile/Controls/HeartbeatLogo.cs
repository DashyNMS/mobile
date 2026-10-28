using DesktopNMS.Core.Branding;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// The DashyNMS mark, drawn rather than an image so its heartbeat can move:
/// while <see cref="IsBeating"/> the line sweeps in and away as a monitor's
/// does (option A of the refresh mock-up), and at rest it's the logo as
/// dashynms_logo.svg draws it. The dashboard's refresh indicator - every
/// refresh, pulled, automatic or the first - in place of a spinner.
/// </summary>
/// <remarks>
/// With Reduce Motion (or Android's "Remove animations") on, the mark
/// gently fades in and out instead of sweeping.
/// </remarks>
public sealed class HeartbeatLogo : GraphicsView
{
    public static readonly BindableProperty IsBeatingProperty = BindableProperty.Create(
        nameof(IsBeating), typeof(bool), typeof(HeartbeatLogo), false,
        propertyChanged: (view, _, value) => ((HeartbeatLogo)view).Beat((bool)value));

    private const string AnimationName = "Heartbeat";

    private readonly Mark _mark = new();

    public HeartbeatLogo()
    {
        Drawable = _mark;
        BackgroundColor = Colors.Transparent;
        SemanticProperties.SetDescription(this, "DashyNMS");
    }

    public bool IsBeating
    {
        get => (bool)GetValue(IsBeatingProperty);
        set => SetValue(IsBeatingProperty, value);
    }

    private void Beat(bool beating)
    {
        this.AbortAnimation(AnimationName);
        _mark.Phase = null;
        Opacity = 1;
        SemanticProperties.SetDescription(this, beating ? "DashyNMS, refreshing" : "DashyNMS");

        if (beating)
        {
            if (ReducedMotion.IsOn)
            {
                this.Animate(AnimationName, v => Opacity = 1 - (0.45 * Math.Sin(v * Math.PI)), length: 1600, repeat: () => IsBeating);
            }
            else
            {
                this.Animate(
                    AnimationName,
                    v =>
                    {
                        _mark.Phase = v;
                        Invalidate();
                    },
                    length: HeartbeatTrace.SweepMilliseconds,
                    easing: Easing.Linear,
                    repeat: () => IsBeating,
                    finished: (_, _) => Settle());
                return;
            }
        }

        Invalidate();
    }

    /// <summary>Done: the whole line again.</summary>
    private void Settle()
    {
        if (!IsBeating)
        {
            _mark.Phase = null;
            Invalidate();
        }
    }

    private sealed class Mark : IDrawable
    {
        private static readonly Color Background = Color.FromArgb("#171B23");
        private static readonly Color Ring = Color.FromArgb("#3B82F6");
        private static readonly Color Line = Color.FromArgb("#E8EEF6");

        /// <summary>How far through a sweep, or null at rest (the whole line).</summary>
        public double? Phase { get; set; }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            // The logo's 100 x 100 units, scaled to fit and centred.
            var scale = Math.Min(dirtyRect.Width, dirtyRect.Height) / 100f;
            canvas.SaveState();
            canvas.Translate(dirtyRect.Center.X - (50 * scale), dirtyRect.Center.Y - (50 * scale));
            canvas.Scale(scale, scale);

            canvas.FillColor = Background;
            canvas.FillCircle(50, 50, 46);
            canvas.StrokeColor = Ring;
            canvas.StrokeSize = 7.5f;
            canvas.DrawCircle(50, 50, 42.25f);

            var (from, to) = Phase is { } phase ? HeartbeatTrace.Visible(phase) : (0, HeartbeatTrace.Length);
            var points = HeartbeatTrace.Between(from, to);
            if (points.Count > 1)
            {
                var path = new PathF();
                path.MoveTo((float)points[0].X, (float)points[0].Y);
                foreach (var (x, y) in points.Skip(1))
                {
                    path.LineTo((float)x, (float)y);
                }

                canvas.StrokeColor = Line;
                canvas.StrokeSize = 8.5f;
                canvas.StrokeLineCap = LineCap.Round;
                canvas.StrokeLineJoin = LineJoin.Round;
                canvas.DrawPath(path);
            }

            canvas.RestoreState();
        }
    }
}
