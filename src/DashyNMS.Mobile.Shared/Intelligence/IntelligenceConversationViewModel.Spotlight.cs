using DashyNMS.Agent.Devices;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Intelligence;

namespace DashyNMS.Mobile.Intelligence;

public sealed partial class IntelligenceConversationViewModel
{
    public const int MaxSpotlight = 32;
    public SessionScope? CurrentScope => CanAsk ? SessionScope.Capture(_session) : null;
    private IReadOnlyList<SavedChatSpotlight> _spotlight = [];
    private long _spotlightVersion;
    public IReadOnlyList<SavedChatSpotlight> Spotlight => _spotlight;
    public string SpotlightInstructions => string.Join("; ", _spotlight.Select(s =>
        $"{s.Object!.Kind} {s.Object.Id}" + (s.Object.DeviceId is { } device ? $" on device {device}" : "")
        + (s.Object.RuleId is { } rule ? $", rule {rule}" : "") + ": " + CompactNetworkLookups.Clean(s.Name)));

    private void ClearSpotlight()
    {
        _spotlight = [];
        _spotlightVersion++;
        OnPropertyChanged(nameof(Spotlight));
    }

    public bool RemoveSpotlight(SavedChatSpotlight item)
    {
        if (!CanAsk) { Notice = "Spotlight is locked while an answer runs or Intelligence is unavailable."; return false; }
        _spotlight = _spotlight.Where(s => s != item).ToArray();
        _spotlightVersion++;
        OnPropertyChanged(nameof(Spotlight));
        return true;
    }

    /// <summary>Resolution is outside the model; an old picker or answer cannot change a newer chat.</summary>
    public async Task<bool> SetSpotlightAsync(SpotlightChoice choice, long revision,
        Func<SessionScope, IntelligenceObjectReference, CancellationToken, Task<ObjectResolutionOutcome>> resolve,
        bool only = false, IntelligenceTurn? source = null, CancellationToken token = default)
    {
        if (!CanAsk || revision != ChatRevision || !choice.Scope.IsCurrent(_session)
            || source is not null && (!OwnsTurn(source) || !source.Evidence.Any(e => e.Reference.Object == choice.Identity)))
        { Notice = "Spotlight could not be changed. The chat or connection changed, or an answer is running."; return false; }
        var version = _spotlightVersion;
        try
        {
            var outcome = await resolve(choice.Scope, choice.Identity, token);
            if (!CanAsk || token.IsCancellationRequested || revision != ChatRevision || version != _spotlightVersion
                || !choice.Scope.IsCurrent(_session) || source is not null && !OwnsTurn(source)) return false;
            if (outcome.Result is not { } found || found.Identity != choice.Identity)
            { Notice = "The object could not be checked on this connection. Spotlight was kept."; return false; }
            var already = _spotlight.Any(s => s.Object is { } o && o.Kind == found.Identity.Kind && o.Id == found.Identity.Id && o.DeviceId == found.Identity.DeviceId);
            if (!only && already) { Notice = "Already in Spotlight."; return false; }
            if (!only && _spotlight.Count >= MaxSpotlight) { Notice = "Spotlight is full. Remove an item first."; return false; }
            var kind = found.Identity.Kind switch { "device" => 0, "group" => 1, "location" => 2, "sensor" => 3, "alert" => 4, "fault" => 5, _ => 6 };
            var entry = new SavedChatSpotlight(kind, found.Identity.Id, found.Name) { Object = found.Identity };
            _spotlight = only ? [entry] : [.. _spotlight, entry];
            _spotlightVersion++;
            OnPropertyChanged(nameof(Spotlight));
            Notice = "";
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch { if (revision == ChatRevision && choice.Scope.IsCurrent(_session)) Notice = "Spotlight could not be checked. Try again."; return false; }
    }
}
