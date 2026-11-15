using DashyNMS.Agent;
using DashyNMS.Agent.Devices;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Intelligence;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>A picker result carries its connection so a stale selection cannot cross accounts.</summary>
public sealed record SpotlightChoice(SessionScope Scope, IntelligenceObjectReference Identity, string Name);

/// <summary>Fresh read-only identity checks over a captured transport, never the mutable session client.</summary>
public sealed class IntelligenceSpotlight(ISessionService session, IntelligenceDeviceTransports transports)
{
    public async Task<IReadOnlyList<SpotlightChoice>> ChoicesAsync(string kind, CancellationToken token = default)
    {
        if (SessionScope.Capture(session) is not { } scope) return [];
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(CompactNetworkLookups.Timeout);
        using var client = new ConnectionBoundClient(scope, transports.Open, objects: true);
        var ct = deadline.Token;
        var choices = kind switch
        {
            "device" => (await client.Devices.ListAsync(ct)).Select(d => new SpotlightChoice(scope, new("device", d.DeviceId), d.Display ?? d.Hostname ?? $"Device {d.DeviceId}")),
            "group" => (await client.DeviceGroups.ListAsync(ct)).Select(g => new SpotlightChoice(scope, new("group", g.Id), g.Name)),
            "location" => (await client.Locations.ListAsync(ct)).Select(l => new SpotlightChoice(scope, new("location", l.Id), l.Name)),
            _ => throw new ArgumentException("Unknown picker kind.", nameof(kind)),
        };
        ct.ThrowIfCancellationRequested();
        return scope.IsCurrent(session) ? choices.Where(c => c.Identity.IsValid).OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToArray() : [];
    }

    public async Task<ObjectResolutionOutcome> ResolveAsync(SessionScope scope, IntelligenceObjectReference reference, CancellationToken token)
    {
        if (!scope.IsCurrent(session)) throw new OperationCanceledException();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(CompactNetworkLookups.Timeout);
        using var client = new ConnectionBoundClient(scope, transports.Open, objects: true);
        using var resolver = HostObjectResolver.Create(client);
        var result = await resolver.ResolveAsync(reference, deadline.Token);
        if (!scope.IsCurrent(session)) throw new OperationCanceledException();
        return result;
    }
}
