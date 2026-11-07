namespace DashyNMS.Mobile.Services;

/// <summary>What the iOS tab controller does next while its tab pill is missing.</summary>
public enum TabSettleAction
{
    /// <summary>Nothing to do: the pill is there, or no wide tab bar is wanted.</summary>
    None,

    /// <summary>Shell has not supplied the tabs yet; try again on a later layout.</summary>
    Wait,

    /// <summary>Apply <see cref="TabSettleStep.Stage"/>, then check again after <see cref="TabSettleStep.NextCheck"/>.</summary>
    Nudge,

    /// <summary>Every stage has been tried without a pill; stop until the size changes.</summary>
    GiveUp,
}

/// <summary>How hard a nudge pushes UIKit to build the tab pill, gentlest first.</summary>
public enum TabSettleStage
{
    /// <summary>No nudge.</summary>
    None,

    /// <summary>Hand the same view controllers back, keeping the selection.</summary>
    Refresh,

    /// <summary>Hide the tab bar and show it again.</summary>
    ToggleBar,

    /// <summary>Flip the horizontal size class override for one layout pass, then restore it.</summary>
    FlipTraits,
}

/// <summary>One decision: what to do, which nudge, and how long until the next check.</summary>
public readonly record struct TabSettleStep(TabSettleAction Action, TabSettleStage Stage = TabSettleStage.None, TimeSpan NextCheck = default);

/// <summary>
/// Decides when the iOS tab controller nudges UIKit into drawing its tab pill.
/// </summary>
/// <remarks>
/// <para>On the iPhone Duo's inner display, a landscape cold launch can leave
/// the tab bar with all its items but no glass pill until the phone is rotated
/// (#372). A single refresh straight after the first appearance was not enough:
/// UIKit may not be ready to build the pill on that run-loop turn. So the
/// controller checks a few times over about a second, escalating from the
/// gentlest nudge, and stops for good once the pill is found or every stage has
/// been tried.</para>
/// <para>Portrait and narrow views are left alone: they already show the bar,
/// and a trait change there changed the portrait tabs in #381. The policy is
/// plain .NET so the escalation can be tested without UIKit.</para>
/// </remarks>
public sealed class TabSettlePolicy
{
    /// <summary>When each check happens, from the first. A nudge follows every check but the last.</summary>
    private static readonly TimeSpan[] Checks =
    [
        TimeSpan.Zero,
        TimeSpan.FromMilliseconds(150),
        TimeSpan.FromMilliseconds(400),
        TimeSpan.FromMilliseconds(1000),
    ];

    /// <summary>
    /// Nudges allowed over the controller's life, so a nudge that itself
    /// resizes the view can never restart the escalation without end.
    /// </summary>
    public const int MostNudges = 9;

    private int _attempt;
    private int _nudges;
    private double _width = double.NaN;
    private double _height = double.NaN;

    /// <summary>Checks made since the last size change.</summary>
    public int Attempt => _attempt;

    /// <summary>True once the pill was found, or every stage tried, at this size.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>
    /// The decision for check <paramref name="attempt"/> (0 for the first).
    /// Only a wide view that should show tabs, but has no pill, is nudged.
    /// </summary>
    public static TabSettleStep Decide(int attempt, bool showTabs, bool isWide, bool controllersPresent, bool pillFound)
    {
        if (!showTabs || !isWide || pillFound)
            return new(TabSettleAction.None);
        if (!controllersPresent)
            return new(TabSettleAction.Wait);

        attempt = Math.Max(0, attempt);
        if (attempt >= Checks.Length - 1)
            return new(TabSettleAction.GiveUp);

        return new(TabSettleAction.Nudge, (TabSettleStage)(attempt + 1), Checks[attempt + 1] - Checks[attempt]);
    }

    /// <summary>
    /// Notes the view's size. A new size starts the escalation again, as the
    /// display may have folded, unfolded or turned. True if the size changed.
    /// </summary>
    public bool Resize(double width, double height)
    {
        if (width.Equals(_width) && height.Equals(_height))
            return false;

        _width = width;
        _height = height;
        _attempt = 0;
        IsFinished = false;
        return true;
    }

    /// <summary>
    /// Makes the next check and records it: a nudge counts as an attempt, and
    /// a found pill or a give-up finishes until the size changes.
    /// </summary>
    public TabSettleStep Check(bool showTabs, bool isWide, bool controllersPresent, bool pillFound)
    {
        if (IsFinished)
            return new(TabSettleAction.None);

        var step = Decide(_attempt, showTabs, isWide, controllersPresent, pillFound);
        if (step.Action == TabSettleAction.Nudge && _nudges >= MostNudges)
            step = new(TabSettleAction.GiveUp);

        switch (step.Action)
        {
            case TabSettleAction.Nudge:
                _attempt++;
                _nudges++;
                break;
            case TabSettleAction.GiveUp:
                IsFinished = true;
                break;
            case TabSettleAction.None when pillFound:
                IsFinished = true;
                break;
        }

        return step;
    }
}
