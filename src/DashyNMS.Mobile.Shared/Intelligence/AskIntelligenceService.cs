using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Intelligence;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>Validate before starting a distinct chat; never install an unverified row or overwrite its predecessor.</summary>
public sealed class AskIntelligenceService(IntelligenceConversationViewModel conversation, IntelligenceChatListViewModel chats,
    IntelligencePreferences preferences, IntelligenceSpotlight spotlight, ISessionService session, INavigationService navigation)
{
    private bool _busy;
    public bool IsAvailable => !_busy && conversation.CanAsk;
    public bool IsCurrent(SessionScope scope) => scope.IsCurrent(session);
    public SessionScope? CaptureScope() => SessionScope.Capture(session);

    public async Task<bool> StartAsync(SessionScope scope, IntelligenceObjectReference identity, CancellationToken token = default)
    {
        if (!IsAvailable || !scope.IsCurrent(session) || !identity.IsValid) return false;
        _busy = true;
        var revision = conversation.ChatRevision;
        try
        {
            var resolved = await spotlight.ResolveAsync(scope, identity, token);
            if (resolved.Result is not { } found || !scope.IsCurrent(session) || token.IsCancellationRequested
                || revision != conversation.ChatRevision || !conversation.CanAsk) return false;
            var created = preferences.SingleChatMode || conversation.IsMemoryOnlyChat
                ? conversation.NewObjectChatRetainingDraft()
                : await chats.NewChatAsync();
            if (!created || !scope.IsCurrent(session) || !conversation.CanAsk) return false;
            // The object was checked before leaving. Installation still uses the normal ownership
            // gate, without a second network wait in which the new empty chat could be edited.
            var installed = await conversation.SetSpotlightAsync(new(scope, identity, found.Name), conversation.ChatRevision,
                (_, _, _) => Task.FromResult(resolved), only: true, token: token);
            if (!installed) return false;
            await navigation.GoToAsync("//main/intelligence");
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch { return false; }
        finally { _busy = false; }
    }
}
