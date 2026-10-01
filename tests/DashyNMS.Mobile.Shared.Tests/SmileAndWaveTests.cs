using DashyNMS.Mobile.Services;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class SmileAndWaveTests
{
    private const double Width = 390;

    [Fact]
    public void Everyone_starts_off_the_left_edge_and_ends_off_the_right()
    {
        var length = SmileAndWave.Length(reducedMotion: false);

        for (var i = 0; i < SmileAndWave.Scales.Count; i++)
        {
            Assert.True(SmileAndWave.Pose(i, 0, Width, false).X <= -SmileAndWave.PenguinWidth);
            Assert.True(SmileAndWave.Pose(i, length, Width, false).X >= Width);
        }
    }

    [Fact]
    public void They_stand_in_line_centred_and_wave_once_arrived()
    {
        var poses = Enumerable.Range(0, SmileAndWave.Scales.Count)
            .Select(i => SmileAndWave.Pose(i, 2.0, Width, false))
            .ToList();

        // In order, evenly spaced, the line centred.
        Assert.Equal(poses.OrderBy(p => p.X), poses);
        var groupWidth = poses[^1].X + SmileAndWave.PenguinWidth - poses[0].X;
        Assert.Equal((Width - groupWidth) / 2, poses[0].X, precision: 6);

        Assert.All(poses, p => Assert.Equal(0, p.Waddle)); // standing still
        Assert.Contains(poses, p => p.Flipper < -100);     // flippers up
    }

    [Fact]
    public void They_waddle_while_walking_and_flippers_rest_down_before_the_wave()
    {
        var walking = SmileAndWave.Pose(0, 0.2, Width, false);

        Assert.NotEqual(0, walking.Waddle);
        Assert.InRange(Math.Abs(walking.Waddle), 0, 7);
        Assert.Equal(-15, walking.Flipper);
    }

    [Fact]
    public void The_caption_shows_while_they_wave()
    {
        Assert.Equal(0, SmileAndWave.CaptionOpacity(0.5, false));
        Assert.Equal(1, SmileAndWave.CaptionOpacity(2.0, false));
        Assert.Equal(0, SmileAndWave.CaptionOpacity(SmileAndWave.Length(false), false));
    }

    [Fact]
    public void Reduced_motion_stands_them_still_waving_and_fades_instead()
    {
        foreach (var seconds in new[] { 0.0, 1.0, 3.0 })
        {
            var pose = SmileAndWave.Pose(0, seconds, Width, reducedMotion: true);
            Assert.Equal(0, pose.Waddle);
            Assert.Equal(-140, pose.Flipper);
            Assert.Equal(SmileAndWave.Pose(0, 0, Width, true).X, pose.X);
        }

        Assert.Equal(0, SmileAndWave.OverlayOpacity(0, true));
        Assert.Equal(1, SmileAndWave.OverlayOpacity(1.5, true));
        Assert.Equal(0, SmileAndWave.OverlayOpacity(SmileAndWave.Length(true), true));
        Assert.Equal(1, SmileAndWave.OverlayOpacity(0, false));
    }

    [Fact]
    public void A_narrow_screen_keeps_the_line_off_the_left_edge() =>
        Assert.True(SmileAndWave.Pose(0, 2.0, 200, false).X >= 8);

    [Fact]
    public void Three_taps_within_two_seconds_start_it()
    {
        var time = new FakeTimeProvider();
        var taps = new SecretTaps(time);

        Assert.False(taps.Tap());
        time.Advance(TimeSpan.FromSeconds(0.5));
        Assert.False(taps.Tap());
        time.Advance(TimeSpan.FromSeconds(0.5));
        Assert.True(taps.Tap());

        // And it starts counting again.
        Assert.False(taps.Tap());
    }

    [Fact]
    public void Slow_taps_dont()
    {
        var time = new FakeTimeProvider();
        var taps = new SecretTaps(time);

        taps.Tap();
        time.Advance(TimeSpan.FromSeconds(1.5));
        taps.Tap();
        time.Advance(TimeSpan.FromSeconds(1.5)); // the first is now too long ago
        Assert.False(taps.Tap());
    }
}
