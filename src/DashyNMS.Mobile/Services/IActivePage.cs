namespace DashyNMS.Mobile.Services;

/// <summary>Restores page-owned controls after Shell selects a retained stack or resumes.</summary>
public interface IActivePage
{
    void Activate();
}

/// <summary>
/// Shell's modal-stack-first active-page lookup, so glass-owning modals
/// (such as <c>IntelligenceChatsPage</c>) pass ownership guards while presented.
/// </summary>
internal static class ActivePage
{
    public static Page? Current =>
        Shell.Current is { } shell
            ? shell.Navigation.ModalStack.LastOrDefault() ?? shell.CurrentPage
            : null;

    public static bool IsCurrent(Page page) => ReferenceEquals(Current, page);
}
