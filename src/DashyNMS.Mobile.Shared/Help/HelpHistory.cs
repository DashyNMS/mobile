namespace DashyNMS.Mobile.Help;

/// <summary>
/// The help page read last (#668), so the page beside the contents shows it
/// when the window widens again after reading pages on their own.
/// </summary>
public sealed class HelpHistory
{
    private HelpTopic? _lastRead;

    /// <summary>Counts pages opened, so a contents page can tell what was read since it opened.</summary>
    public long Reads { get; private set; }

    public HelpTopic? LastRead
    {
        get => _lastRead;
        set
        {
            _lastRead = value;
            Reads++;
        }
    }
}
