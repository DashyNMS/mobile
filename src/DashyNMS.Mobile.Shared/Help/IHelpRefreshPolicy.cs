namespace DashyNMS.Mobile.Help;

/// <summary>Whether newer help pages may be looked for on GitHub: not while nothing should leave the phone.</summary>
public interface IHelpRefreshPolicy
{
    bool MayRefresh { get; }
}

/// <summary>Refreshing is allowed whenever Help is open.</summary>
public sealed class HelpRefreshAllowed : IHelpRefreshPolicy
{
    public bool MayRefresh => true;
}
