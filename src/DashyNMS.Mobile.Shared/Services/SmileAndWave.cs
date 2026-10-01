namespace DashyNMS.Mobile.Services;

/// <summary>Where one penguin is at a moment of the show, and how its body and flipper are turned.</summary>
/// <param name="X">Its left edge across the overlay, in points.</param>
/// <param name="Waddle">The body's rock, in degrees clockwise about its feet.</param>
/// <param name="Flipper">The waving flipper, in degrees clockwise about its shoulder: -15 down, -140 up.</param>
public readonly record struct PenguinPose(double X, double Waddle, double Flipper);

/// <summary>
/// The "smile and wave" easter egg's choreography (#99), as desktop's
/// (DashyNMS/desktop#205, SmileAndWaveOverlay): four penguins waddle in
/// along the bottom, stop, wave a flipper three times, and waddle off, under
/// the caption. Kept apart from the drawing so the timings - the same as
/// desktop's - can be tested, and so the app just asks where everyone is.
/// </summary>
/// <remarks>
/// With reduced motion on, nobody walks: they stand in line with a flipper
/// up while the whole thing fades in and out, as desktop does.
/// </remarks>
public static class SmileAndWave
{
    public const string Caption = "Smile and wave, boys. Smile and wave.";

    public const double PenguinWidth = 52;
    public const double PenguinHeight = 70;
    public const double OverlayHeight = 130;

    /// <summary>Each penguin's size, front of the line first.</summary>
    public static IReadOnlyList<double> Scales { get; } = [1.0, 0.92, 0.85, 0.78];

    private const double Spacing = 64;
    private const double WalkInEnd = 1.3;
    private const double WaveEnd = 2.7;
    private const double WalkOff = 1.2;
    private const double Stagger = 0.08;
    private const double WaddleStep = 0.15;
    private const double WaddleAngle = 7;
    private const double FlipperDown = -15;
    private const double FlipperUp = -140;
    private const double ReducedLength = 3.7;
    private const double Fade = 0.3;

    /// <summary>How long the show lasts, in seconds.</summary>
    public static double Length(bool reducedMotion) =>
        reducedMotion ? ReducedLength : WaveEnd + WalkOff + (Stagger * Scales.Count);

    /// <summary>Penguin <paramref name="index"/> (0 at the front) at <paramref name="seconds"/> into a show <paramref name="width"/> points wide.</summary>
    public static PenguinPose Pose(int index, double seconds, double width, bool reducedMotion)
    {
        var count = Scales.Count;
        var groupWidth = (Spacing * (count - 1)) + PenguinWidth;
        var standX = Math.Max(8, (width - groupWidth) / 2) + (index * Spacing);
        if (reducedMotion)
        {
            return new PenguinPose(standX, 0, FlipperUp);
        }

        // In from off the left edge, keeping the line in order; the front
        // sets off first on the way out, so nobody catches up.
        var delay = Stagger * index;
        var startX = -PenguinWidth - 20 - ((count - 1 - index) * Spacing);
        var endX = width + 20 + (index * Spacing);
        var walkOffStart = WaveEnd + (Stagger * (count - 1 - index));

        var x = Interpolate(
            [(0, startX), (delay, startX), (WalkInEnd, standX), (walkOffStart, standX), (walkOffStart + WalkOff, endX)],
            seconds);

        var waddle = Waddling(seconds, delay, WalkInEnd) ?? Waddling(seconds, walkOffStart, walkOffStart + WalkOff) ?? 0;

        var waveFrom = WalkInEnd + delay;
        var flipper = Interpolate(
            [
                (0, FlipperDown),
                (waveFrom, FlipperDown),
                (waveFrom + 0.2, FlipperUp),
                (waveFrom + 0.4, -105),
                (waveFrom + 0.6, FlipperUp),
                (waveFrom + 0.8, -105),
                (waveFrom + 1.0, FlipperUp),
                (waveFrom + 1.25, FlipperDown),
            ],
            seconds,
            ease: true);

        return new PenguinPose(x, waddle, flipper);
    }

    /// <summary>The caption: in once they've arrived, out as they leave.</summary>
    public static double CaptionOpacity(double seconds, bool reducedMotion) =>
        reducedMotion ? 1 : FadeInOut(seconds, WalkInEnd, WaveEnd + 0.2);

    /// <summary>The whole overlay: with reduced motion it fades in and out instead of anyone walking.</summary>
    public static double OverlayOpacity(double seconds, bool reducedMotion) =>
        reducedMotion ? FadeInOut(seconds, 0, ReducedLength - Fade) : 1;

    private static double FadeInOut(double seconds, double inAt, double outAt) =>
        Interpolate([(inAt, 0), (inAt + Fade, 1), (outAt, 1), (outAt + Fade, 0)], seconds);

    /// <summary>A side-to-side rock between <paramref name="from"/> and <paramref name="to"/>, upright at both ends; null outside it.</summary>
    private static double? Waddling(double seconds, double from, double to)
    {
        if (seconds < from || seconds > to)
        {
            return null;
        }

        var frames = new List<(double, double)> { (from, 0) };
        var sign = 1;
        for (var at = from + WaddleStep; at < to; at += WaddleStep)
        {
            frames.Add((at, WaddleAngle * sign));
            sign = -sign;
        }

        frames.Add((to, 0));
        return Interpolate(frames, seconds);
    }

    /// <summary>
    /// The value at <paramref name="seconds"/> between key frames: straight
    /// lines, or eased in and out (as desktop's wave). Before the first and
    /// after the last it holds.
    /// </summary>
    private static double Interpolate(IReadOnlyList<(double At, double Value)> frames, double seconds, bool ease = false)
    {
        if (seconds <= frames[0].At)
        {
            return frames[0].Value;
        }

        for (var i = 1; i < frames.Count; i++)
        {
            var (at, value) = frames[i];
            if (seconds > at)
            {
                continue;
            }

            var (fromAt, fromValue) = frames[i - 1];
            var span = at - fromAt;
            var t = span <= 0 ? 1 : (seconds - fromAt) / span;
            if (ease)
            {
                t = (1 - Math.Cos(Math.PI * t)) / 2;
            }

            return fromValue + ((value - fromValue) * t);
        }

        return frames[^1].Value;
    }
}

/// <summary>
/// The easter egg's secret knock (#99): three taps within two seconds.
/// Desktop's is three Shift+clicks; a phone has no Shift key.
/// </summary>
public sealed class SecretTaps(TimeProvider time)
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(2);

    private readonly List<DateTimeOffset> _taps = [];

    /// <summary>Counts a tap; true on the third in time, which starts again from none.</summary>
    public bool Tap()
    {
        var now = time.GetUtcNow();
        _taps.RemoveAll(t => now - t > Window);
        _taps.Add(now);
        if (_taps.Count < 3)
        {
            return false;
        }

        _taps.Clear();
        return true;
    }
}
