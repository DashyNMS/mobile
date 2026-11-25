namespace DashyNMS.Mobile.Help;

/// <summary>
/// The help page read last (#668), so the page beside the contents shows it
/// when the window widens again after reading pages on their own.
/// </summary>
public sealed class HelpHistory
{
    public HelpTopic? LastRead { get; set; }
}
