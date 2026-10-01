using DashyNMS.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// The "smile and wave" easter egg (#99): four penguins along the bottom of
/// the page, moving as <see cref="SmileAndWave"/> says - desktop's show
/// (DashyNMS/desktop#205), drawn from the same plain shapes, so the two
/// apps' penguins match. Our own penguins, not any film's characters.
/// </summary>
/// <remarks>
/// Purely cosmetic, so it never throws: anything that goes wrong just ends
/// the show. It covers only a strip at the bottom, and a tap there sends
/// the penguins off early; the rest of the page carries on as normal.
/// </remarks>
public sealed class SmileAndWaveOverlay : AbsoluteLayout
{
    private const string AnimationName = "SmileAndWave";

    private static readonly Color Coat = Color.FromArgb("#1A1F29");
    private static readonly Color Outline = Color.FromArgb("#4A576E");
    private static readonly Color Belly = Color.FromArgb("#F2F4F7");
    private static readonly Color Orange = Color.FromArgb("#F59E0B");

    private readonly List<Penguin> _penguins = [];
    private Border? _caption;

    public SmileAndWaveOverlay()
    {
        HeightRequest = SmileAndWave.OverlayHeight;
        VerticalOptions = LayoutOptions.End;
        IsClippedToBounds = true;
        IsVisible = false;
        ZIndex = 1000;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => Dismiss();
        GestureRecognizers.Add(tap);
    }

    public bool IsPlaying => IsVisible;

    /// <summary>Starts the show; nothing while one is already on.</summary>
    public void Play()
    {
        if (IsPlaying)
        {
            return;
        }

        try
        {
            var reducedMotion = ReducedMotion.IsOn;
            var width = Parent is View { Width: > 0 } parent ? parent.Width : 400;
            Build();
            IsVisible = true;

            var length = SmileAndWave.Length(reducedMotion);
            Show(0, width, reducedMotion);
            this.Animate(
                AnimationName,
                progress => Show(progress * length, width, reducedMotion),
                length: (uint)(length * 1000),
                easing: Easing.Linear,
                finished: (_, _) => Stop());
        }
        catch (Exception)
        {
            Stop();
        }
    }

    /// <summary>Ends the show early - a tap on the penguins, or leaving the page.</summary>
    public void Dismiss()
    {
        try
        {
            this.AbortAnimation(AnimationName);
        }
        catch (Exception)
        {
            // Cosmetic only - nothing to recover.
        }

        Stop();
    }

    private void Stop()
    {
        IsVisible = false;
        Opacity = 1;
        Children.Clear();
        _penguins.Clear();
        _caption = null;
    }

    private void Show(double seconds, double width, bool reducedMotion)
    {
        try
        {
            for (var i = 0; i < _penguins.Count; i++)
            {
                var pose = SmileAndWave.Pose(i, seconds, width, reducedMotion);
                _penguins[i].Root.TranslationX = pose.X;
                _penguins[i].Body.Rotation = pose.Waddle;
                _penguins[i].Flipper.Rotation = pose.Flipper;
            }

            if (_caption is not null)
            {
                _caption.Opacity = SmileAndWave.CaptionOpacity(seconds, reducedMotion);
            }

            Opacity = SmileAndWave.OverlayOpacity(seconds, reducedMotion);
        }
        catch (Exception)
        {
            // A frame that fails just isn't drawn.
        }
    }

    private void Build()
    {
        Children.Clear();
        _penguins.Clear();

        var standTop = SmileAndWave.OverlayHeight - SmileAndWave.PenguinHeight - 6;
        foreach (var scale in SmileAndWave.Scales)
        {
            var penguin = new Penguin(scale);
            AbsoluteLayout.SetLayoutBounds((BindableObject)penguin.Root, new Rect(0, standTop, SmileAndWave.PenguinWidth, SmileAndWave.PenguinHeight));
            Children.Add(penguin.Root);
            _penguins.Add(penguin);
        }

        _caption = new Border
        {
            Opacity = 0,
            Padding = new Thickness(12, 5),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            StrokeThickness = 1,
            Content = new Label { Text = SmileAndWave.Caption, FontSize = 13, FontFamily = "BodySemibold" },
        };
        _caption.SetAppThemeColor(Border.BackgroundColorProperty, Colour("SurfaceAltLight"), Colour("SurfaceAltDark"));
        _caption.SetAppThemeColor(Border.StrokeProperty, Colour("StrokeLight"), Colour("StrokeDark"));
        ((Label)_caption.Content).SetAppThemeColor(Label.TextColorProperty, Colour("TextLight"), Colour("TextDark"));
        AbsoluteLayout.SetLayoutBounds((BindableObject)_caption, new Rect(0.5, 4, AutoSize, AutoSize));
        AbsoluteLayout.SetLayoutFlags((BindableObject)_caption, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.XProportional);
        Children.Add(_caption);
    }

    private static Color Colour(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color colour ? colour : Colors.Gray;

    /// <summary>
    /// One penguin, drawn as desktop's in a <see cref="SmileAndWave.PenguinWidth"/> x
    /// <see cref="SmileAndWave.PenguinHeight"/> box with its feet on the bottom edge.
    /// </summary>
    private sealed class Penguin
    {
        public Penguin(double scale)
        {
            Body = new AbsoluteLayout { AnchorX = 0.5, AnchorY = 1 };

            // The flippers first, so the body overlaps their tops.
            Body.Add(Shape(new Ellipse { Fill = Coat, Stroke = Outline, StrokeThickness = 1.2, Rotation = 15, AnchorX = 0.5, AnchorY = 0.08 }, -1, 26, 10, 26));
            Flipper = Shape(new Ellipse { Fill = Coat, Stroke = Outline, StrokeThickness = 1.2, Rotation = -15, AnchorX = 0.5, AnchorY = 0.08 }, 43, 26, 10, 26);
            Body.Add(Flipper);

            Body.Add(Shape(new Ellipse { Fill = Orange }, 9, 64, 14, 6));
            Body.Add(Shape(new Ellipse { Fill = Orange }, 29, 64, 14, 6));
            Body.Add(Shape(new Ellipse { Fill = Coat, Stroke = Outline, StrokeThickness = 1.5 }, 4, 4, 44, 62));
            Body.Add(Shape(new Ellipse { Fill = Belly }, 11, 22, 30, 42));
            Body.Add(Shape(new Ellipse { Fill = Belly }, 15, 13, 9, 9));
            Body.Add(Shape(new Ellipse { Fill = Belly }, 28, 13, 9, 9));
            Body.Add(Shape(new Ellipse { Fill = Coat }, 18.5, 16, 4, 4));
            Body.Add(Shape(new Ellipse { Fill = Coat }, 30.5, 16, 4, 4));
            Body.Add(Shape(new Polygon { Points = [new(21, 24), new(31, 24), new(26, 31)], Fill = Orange }, 0, 0, SmileAndWave.PenguinWidth, SmileAndWave.PenguinHeight));

            Root = new AbsoluteLayout { Scale = scale, AnchorX = 0.5, AnchorY = 1, InputTransparent = true };
            Root.Add(Shape(Body, 0, 0, SmileAndWave.PenguinWidth, SmileAndWave.PenguinHeight));
        }

        /// <summary>Walks across (TranslationX) at its size.</summary>
        public AbsoluteLayout Root { get; }

        /// <summary>Rocks as it waddles.</summary>
        public AbsoluteLayout Body { get; }

        /// <summary>The waving flipper, turned about its shoulder.</summary>
        public View Flipper { get; }

        private static T Shape<T>(T view, double left, double top, double width, double height)
            where T : View
        {
            AbsoluteLayout.SetLayoutBounds((BindableObject)view, new Rect(left, top, width, height));
            return view;
        }
    }
}
