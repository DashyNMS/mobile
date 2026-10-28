using DashyNMS.Mobile.Topology;

namespace DashyNMS.Mobile.Tests;

/// <summary>Jiggle physics on the network map (#106), as desktop's.</summary>
public sealed class MapJiggleTests
{
    private readonly NetworkNode _dragged = new(1);
    private readonly NetworkNode _linked = new(2);
    private readonly MapJiggle _jiggle = new() { IsEnabled = true };

    private void Settle(double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1.0 / 60)
        {
            _jiggle.Step(1.0 / 60);
        }
    }

    [Fact]
    public void A_dragged_device_trails_behind_overshoots_and_settles()
    {
        _jiggle.Moved(_dragged, 30, 0, []);

        Assert.Equal((-30, 0), _jiggle.Offset(_dragged)); // still drawn where it was
        Assert.True(_jiggle.IsMoving);

        // Springing after it, it goes past where it really is...
        var overshot = false;
        for (var i = 0; i < 30; i++)
        {
            _jiggle.Step(1.0 / 60);
            overshot |= _jiggle.Offset(_dragged).X > 0.5;
        }

        Assert.True(overshot);

        // ...and comes to rest on it, after which nothing's left to step.
        Settle(2);
        Assert.False(_jiggle.IsMoving);
        Assert.Equal((0, 0), _jiggle.Offset(_dragged));
    }

    [Fact]
    public void Linked_devices_get_a_knock_on_wobble()
    {
        _jiggle.Moved(_dragged, 10, 0, [_linked, _dragged]);
        _jiggle.Step(1.0 / 60);

        Assert.True(_jiggle.Offset(_linked).X > 0); // nudged the way it was dragged
        Assert.Equal(0, _jiggle.Offset(new NetworkNode(3)).X); // nothing for the rest
    }

    [Fact]
    public void A_fling_never_leaves_a_device_far_from_where_it_is()
    {
        _jiggle.Moved(_dragged, 5000, 0, []);

        Assert.Equal(-MapJiggle.MaxOffset, _jiggle.Offset(_dragged).X, precision: 6);

        // And a stalled frame doesn't fling it either.
        _jiggle.Step(5);
        var (x, y) = _jiggle.Offset(_dragged);
        Assert.True(Math.Sqrt((x * x) + (y * y)) <= MapJiggle.MaxOffset);
    }

    [Fact]
    public void Off_nothing_wobbles_and_switching_off_stops_it()
    {
        var off = new MapJiggle();
        off.Moved(_dragged, 30, 0, [_linked]);
        Assert.False(off.IsMoving);

        _jiggle.Moved(_dragged, 30, 0, []);
        _jiggle.IsEnabled = false;
        Assert.False(_jiggle.IsMoving);
        Assert.Equal((0, 0), _jiggle.Offset(_dragged));
    }

    [Fact]
    public void Real_positions_are_never_touched()
    {
        _dragged.X = 100;
        _dragged.Y = 50;

        _jiggle.Moved(_dragged, 30, 0, []);
        Settle(0.3);

        Assert.Equal((100, 50), (_dragged.X, _dragged.Y));
    }
}
