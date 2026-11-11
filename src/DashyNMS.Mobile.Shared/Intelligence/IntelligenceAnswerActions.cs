using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Intelligence;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// Opens what an answer links to (#228). Model text never chooses a route: a chip
/// opens only a reference the current chat's lookups supplied, through the app's
/// existing pages, and a link opens only if Core's web-link policy accepts it and
/// the answer still contains it. A turn from a previous connection opens nothing.
/// </summary>
public sealed class IntelligenceAnswerActions(
    IntelligenceConversationViewModel conversation, INavigationService navigation, ILauncherService launcher)
{
    /// <summary>
    /// The page each verified object kind opens; destination pages handle a target
    /// that has since gone. Routes use the evidence's ids and exact filter values,
    /// never the chip's display text, which lookups may have cleaned or shortened.
    /// </summary>
    public static bool TryRoute(IntelligenceInlineReference reference, IReadOnlyList<IntelligenceEvidence> evidence,
        out string route, out Dictionary<string, object> parameters)
    {
        route = "";
        parameters = new();
        if (reference.Object is not { IsValid: true } target
            || evidence.FirstOrDefault(e => e.Reference == reference) is not { } observed
            || reference.Key != KeyKind(target.Kind) + ":" + target.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            return false;
        switch (target.Kind)
        {
            case "device":
                route = Routes.DeviceDetail;
                parameters[Routes.DeviceIdParameter] = target.Id;
                return true;
            case "alert" or "fault":
                route = Routes.AlertDetail;
                parameters[Routes.AlertKeyParameter] = new AlertKey(target.Kind == "fault" ? AlertSource.Fault : AlertSource.Alert, target.Id).ToString();
                parameters[Routes.DeviceIdParameter] = target.DeviceId!.Value;
                return true;
            case "sensor":
                route = Routes.DeviceSection;
                parameters[Routes.DeviceIdParameter] = target.DeviceId!.Value;
                parameters[Routes.SectionParameter] = DeviceSection.Sensors;
                // A page title only, so the cleaned display name is the right one.
                parameters[Routes.DeviceNameParameter] = evidence.FirstOrDefault(e => e.Reference.Object is { Kind: "device" } d && d.Id == target.DeviceId)?.Reference.Name ?? "";
                return true;
            // Devices filters groups and locations by exact name, as Groups & locations does.
            // Without LibreNMS's own name the chip stays plain text rather than opening an empty list.
            case "group" when !string.IsNullOrWhiteSpace(observed.Filter):
                route = Routes.Devices;
                parameters[Routes.GroupParameter] = observed.Filter!;
                return true;
            case "location" when !string.IsNullOrWhiteSpace(observed.Filter):
                route = Routes.Devices;
                parameters[Routes.LocationParameter] = observed.Filter!;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Faults use the alert marker: on one server, alert ids come from one source.</summary>
    private static string KeyKind(string kind) => kind == "fault" ? "alert" : kind;

    public bool CanOpen(IntelligenceTurn turn, IntelligenceInlineReference reference) =>
        conversation.OwnsTurn(turn) && TryRoute(reference, turn.Evidence, out _, out _);

    public async Task<bool> OpenAsync(IntelligenceTurn turn, IntelligenceInlineReference reference)
    {
        if (!conversation.OwnsTurn(turn) || !TryRoute(reference, turn.Evidence, out var route, out var parameters)) return false;
        await navigation.GoToAsync(route, parameters);
        return true;
    }

    /// <summary>Opens a source link in the system browser, never inside the app.</summary>
    public async Task<bool> OpenLinkAsync(IntelligenceTurn turn, Uri link)
    {
        if (!conversation.OwnsTurn(turn) || !WebSearchCitations.TryGetLink(link.OriginalString, out var uri)
            || !turn.Answer.Contains(link.OriginalString, StringComparison.Ordinal)) return false;
        await launcher.OpenAsync(uri);
        return true;
    }
}

/// <summary>
/// Colour for Core's reference tones, following Settings › Intelligence › Colour
/// status indicators. Colour only ever accompanies the status words, never replaces them.
/// </summary>
public static class IntelligenceStatusTone
{
    /// <summary>The theme colour family ("Critical", "Warning", "Ok"), or null for the ordinary text colour.</summary>
    public static string? ColourKey(string? tone, bool colourStatusIndicators) => !colourStatusIndicators ? null : tone switch
    {
        "down" or "critical" => "Critical",
        "warning" => "Warning",
        "up" => "Ok",
        _ => null,
    };

    /// <summary>What a chip opens, for its accessible name.</summary>
    public static string Describe(IntelligenceInlineReference reference)
    {
        var status = reference.Status.Length > 0 ? ", " + reference.Status : "";
        return reference.Object?.Kind switch
        {
            "device" => $"Open device {reference.Name}{status}",
            "alert" or "fault" => $"Open alert {reference.Name}{status}",
            "sensor" => $"Open sensors including {reference.Name}",
            "group" => $"Show devices in group {reference.Name}",
            "location" => $"Show devices at location {reference.Name}",
            _ => reference.Name + status,
        };
    }
}
