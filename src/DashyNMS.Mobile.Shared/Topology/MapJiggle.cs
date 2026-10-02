namespace DashyNMS.Mobile.Topology;

/// <summary>
/// "Jiggle physics" for the network map (#106), as desktop's (desktop #207):
/// a damped spring per node still settling, giving an offset from where the
/// node really is. When a dragged device's real position jumps to the finger,
/// its offset takes up the jump, so what's drawn trails behind, overshoots and
/// settles like jelly; devices linked to it get a smaller knock-on wobble.
/// Purely visual - real positions, and so the saved layout, are never touched.
/// </summary>
/// <remarks>
/// Desktop's numbers, so the two apps wobble alike. Only the dragged node and
/// its neighbours ever get a spring, never the whole graph, and the page only
/// steps it (<see cref="Step"/>) while <see cref="IsMoving"/>, so an idle or
/// large map costs nothing. No timer of its own, so it can be tested.
/// </remarks>
public sealed class MapJiggle
{
    /// <summary>Spring stiffness and damping (per second): under-damped, for an overshoot that settles in about half a second.</summary>
    internal const double Stiffness = 170;
    internal const double Damping = 11;

    /// <summary>How much of a dragged node's movement linked nodes feel, as velocity.</summary>
    internal const double NeighbourKick = 7;

    /// <summary>The furthest (map units) a node is ever drawn from where it really is - a fast fling shouldn't leave it across the map.</summary>
    internal const double MaxOffset = 90;

    /// <summary>The longest step taken at once: a stalled frame shouldn't fling anything.</summary>
    internal const double MaxStepSeconds = 1.0 / 30;

    private readonly Dictionary<NetworkNode, Body> _bodies = new(ReferenceEqualityComparer.Instance);
    private bool _isEnabled;

    /// <summary>The setting (desktop's jigglePhysicsOnMaps), and motion allowed by the phone. Off stops anything still wobbling.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (!value)
            {
                Clear();
            }
        }
    }

    /// <summary>True while anything is still settling - the page keeps stepping until it isn't.</summary>
    public bool IsMoving => _bodies.Count > 0;

    /// <summary>
    /// <paramref name="node"/>'s real position just moved by (<paramref name="dx"/>,
    /// <paramref name="dy"/>): it keeps being drawn where it was and springs
    /// after it, and each of <paramref name="linked"/> gets a smaller nudge.
    /// </summary>
    public void Moved(NetworkNode node, double dx, double dy, IEnumerable<NetworkNode> linked)
    {
        if (!_isEnabled || (dx == 0 && dy == 0))
        {
            return;
        }

        var body = BodyFor(node);
        (body.X, body.Y) = Clamp(body.X - dx, body.Y - dy);

        foreach (var other in linked)
        {
            if (!ReferenceEquals(other, node))
            {
                var kicked = BodyFor(other);
                kicked.Vx += dx * NeighbourKick;
                kicked.Vy += dy * NeighbourKick;
            }
        }
    }

    /// <summary>How far from its real position to draw <paramref name="node"/> now, in map units.</summary>
    public (double X, double Y) Offset(NetworkNode node) => _bodies.TryGetValue(node, out var body) ? (body.X, body.Y) : (0, 0);

    /// <summary>Moves every spring on by <paramref name="seconds"/>; those that have settled are dropped.</summary>
    public void Step(double seconds)
    {
        seconds = Math.Clamp(seconds, 0, MaxStepSeconds);

        // Small fixed sub-steps keep a stiff spring stable at any frame rate.
        const double subStep = 1.0 / 240;
        var settled = new List<NetworkNode>();
        foreach (var (node, body) in _bodies)
        {
            for (var t = 0.0; t < seconds; t += subStep)
            {
                var dt = Math.Min(subStep, seconds - t);
                body.Vx += ((body.X * -Stiffness) - (body.Vx * Damping)) * dt;
                body.Vy += ((body.Y * -Stiffness) - (body.Vy * Damping)) * dt;
                body.X += body.Vx * dt;
                body.Y += body.Vy * dt;
            }

            (body.X, body.Y) = Clamp(body.X, body.Y);
            if (Math.Sqrt((body.X * body.X) + (body.Y * body.Y)) < 0.05 && Math.Sqrt((body.Vx * body.Vx) + (body.Vy * body.Vy)) < 0.5)
            {
                settled.Add(node);
            }
        }

        foreach (var node in settled)
        {
            _bodies.Remove(node);
        }
    }

    /// <summary>Everything still at once - the setting off, or the map replaced.</summary>
    public void Clear() => _bodies.Clear();

    private Body BodyFor(NetworkNode node)
    {
        if (!_bodies.TryGetValue(node, out var body))
        {
            body = new Body();
            _bodies[node] = body;
        }

        return body;
    }

    private static (double X, double Y) Clamp(double x, double y)
    {
        var length = Math.Sqrt((x * x) + (y * y));
        return length > MaxOffset ? (x * MaxOffset / length, y * MaxOffset / length) : (x, y);
    }

    private sealed class Body
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
    }
}
