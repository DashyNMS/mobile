using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text;
using System.Text.RegularExpressions;
using DashyNMS.Mobile.Intelligence;

namespace DashyNMS.Mobile.Demo;

/// <summary>
/// Intelligence while the demo runs (#234). App Store reviewers have no LibreNMS
/// server and may not have Apple Intelligence, so a few example questions have
/// recorded answers: no model runs, but their lookups do, through the same path
/// as a real answer, so the tables, chips and graph come from the demo network.
/// Any other question goes to the phone's own model where it has one. Outside
/// the demo this is the phone's model, unchanged.
/// </summary>
public sealed partial class DemoIntelligenceModel(IIntelligenceModel inner, DemoState demo) : IIntelligenceModel
{
    /// <summary>The questions with recorded answers, as the starter buttons show them.</summary>
    public static readonly IReadOnlyList<string> Questions =
    [
        "How is the network doing?",
        "Which devices are currently unreachable?",
        "Show me a traffic graph for core-sw-01 over the last day",
    ];

    /// <summary>Under every recorded answer, so nobody mistakes it for a model's.</summary>
    public const string Caption = "*Example answer for the demo network. No model wrote it; the figures come from the demo's lookups.*";

    public IntelligenceAvailability Availability => demo.IsActive ? IntelligenceAvailability.Available : inner.Availability;

    // No on-device model runs when the phone has no Apple Intelligence, so the
    // recorded examples must not be clipped by the 4,096-token fallback budget.
    public int? ContextSize => demo.IsActive && inner.Availability != IntelligenceAvailability.Available ? 16_384 : inner.ContextSize;

    public bool SupportsLookupImages => (!demo.IsActive || inner.Availability == IntelligenceAvailability.Available) && inner.SupportsLookupImages;

    public IReadOnlyList<string>? ExampleQuestions => demo.IsActive ? Questions : null;

    public string? ExampleNotice => demo.IsActive
        ? inner.Availability == IntelligenceAvailability.Available
            ? "Example conversation. The suggested questions have recorded answers; Apple Intelligence answers anything else from the demo network."
            : "Example conversation. The suggested questions have recorded answers. Other questions need a device with Apple Intelligence."
        : null;

    public Task<int?> CountTokensAsync(string text, CancellationToken cancellationToken = default)
        => inner.Availability == IntelligenceAvailability.Available ? inner.CountTokensAsync(text, cancellationToken) : Task.FromResult<int?>(null);

    public IIntelligenceSession StartSession(string instructions, IReadOnlyList<IntelligenceLookup> lookups)
        => demo.IsActive ? new ExampleSession(inner, instructions, lookups) : inner.StartSession(instructions, lookups);

    /// <summary>
    /// Wraps whichever model the app registered last - Apple's on iOS, none elsewhere - so
    /// the demo answers on every device. Call after the platform's registrations.
    /// </summary>
    public static void Decorate(IServiceCollection services)
    {
        var platform = services.LastOrDefault(d => d.ServiceType == typeof(IIntelligenceModel))
            ?? throw new InvalidOperationException("Register the platform's Intelligence model first.");
        services.RemoveAll<IIntelligenceModel>();
        services.AddSingleton<IIntelligenceModel>(sp => new DemoIntelligenceModel(
            platform.ImplementationInstance as IIntelligenceModel
                ?? platform.ImplementationFactory?.Invoke(sp) as IIntelligenceModel
                ?? (IIntelligenceModel)ActivatorUtilities.CreateInstance(sp, platform.ImplementationType!),
            sp.GetRequiredService<DemoState>()));
    }

    /// <summary>The recorded question a prompt asks, ignoring case and a closing question mark; null for anything else.</summary>
    internal static string? Recorded(string prompt)
    {
        // The conversation sends "Question:\n…", after any earlier turns.
        var at = prompt.LastIndexOf("Question:\n", StringComparison.Ordinal);
        var question = Normalise(at >= 0 ? prompt[(at + "Question:\n".Length)..] : prompt);
        return Questions.FirstOrDefault(q => Normalise(q) == question);
    }

    private static string Normalise(string text) => text.Trim().TrimEnd('?', '.', '!').Trim().ToLowerInvariant().Replace('’', '\'');

    private sealed class ExampleSession(IIntelligenceModel inner, string instructions, IReadOnlyList<IntelligenceLookup> lookups) : IIntelligenceSession
    {
        private IIntelligenceSession? _live;

        public IReadOnlyList<string> LookupImageIdsOmitted => _live?.LookupImageIdsOmitted ?? [];

        public async Task<IntelligenceAnswer> AskAsync(string question, Action<string> onText, CancellationToken cancellationToken = default)
        {
            var recorded = Recorded(question);
            if (recorded is null)
            {
                if (inner.Availability == IntelligenceAvailability.Available)
                {
                    _live ??= inner.StartSession(instructions, lookups);
                    return await _live.AskAsync(question, onText, cancellationToken).ConfigureAwait(false);
                }

                var text = "This is an example conversation, and only its suggested questions have answers here: "
                    + string.Join(", ", Questions.Select(q => "\"" + q + "\"")) + ". "
                    + "Live answers about the demo network need a device with Apple Intelligence.";
                onText(text);
                return new IntelligenceAnswer(text, null);
            }

            var answer = recorded switch
            {
                _ when recorded == Questions[0] => await OverviewAsync(cancellationToken).ConfigureAwait(false),
                _ when recorded == Questions[1] => await UnreachableAsync(cancellationToken).ConfigureAwait(false),
                _ => await TrafficAsync(cancellationToken).ConfigureAwait(false),
            } + "\n\n" + Caption;
            onText(answer);
            return new IntelligenceAnswer(answer, null);
        }

        public void Dispose() => _live?.Dispose();

        private async Task<string?> LookAsync(string name, string arguments, CancellationToken cancellationToken)
        {
            if (lookups.FirstOrDefault(l => l.Name == name) is not { } lookup) return null;
            var result = await lookup.ReadAsync(arguments, cancellationToken).ConfigureAwait(false);
            return CompactNetworkLookups.IsFailure(result.Text) ? null : result.Text;
        }

        private async Task<string> OverviewAsync(CancellationToken cancellationToken)
        {
            var overview = await LookAsync("network_overview", "{}", cancellationToken).ConfigureAwait(false);
            var alerts = await LookAsync("active_alerts", "{}", cancellationToken).ConfigureAwait(false) ?? overview;
            if (overview is null) return Missing;

            var text = new StringBuilder();
            if (DeviceCounts().Match(overview) is { Success: true } devices && AlertCounts().Match(overview) is { Success: true } open)
            {
                var down = int.Parse(devices.Groups["down"].Value, CultureInfo.InvariantCulture);
                text.Append(down == 0 ? "**The network is healthy.** " : $"**Most of the network is up, but {Count(down, "device is", "devices are")} down.** ");
                text.Append(CultureInfo.InvariantCulture, $"Of {devices.Groups["total"].Value} devices, {devices.Groups["up"].Value} are up and {devices.Groups["down"].Value} down. ");
                text.Append(CultureInfo.InvariantCulture, $"There are {open.Groups["open"].Value} open alerts: {open.Groups["critical"].Value} critical and {open.Groups["warning"].Value} warning.");
            }
            else
            {
                text.Append(overview.Split('\n')[0]);
            }

            var rows = AlertRow().Matches(alerts ?? "").Take(6).ToList();
            if (rows.Count > 0)
            {
                text.Append("\n\nThe most urgent alerts:\n\n| Severity | Alert | Device | Open for |\n|---|---|---|---|\n");
                foreach (Match row in rows)
                {
                    var state = row.Groups["state"].Value == "acknowledged" ? " (acknowledged)" : "";
                    text.Append(CultureInfo.InvariantCulture, $"| {row.Groups["severity"].Value}{state} | {row.Groups["alert"].Value} | {row.Groups["device"].Value} | {row.Groups["age"].Value} |\n");
                }

                text.Append("\nStart with the devices that are down: their alerts are critical and still open.");
            }

            return text.ToString().TrimEnd();
        }

        private async Task<string> UnreachableAsync(CancellationToken cancellationToken)
        {
            var found = await LookAsync("find_device", "{\"status\":\"down\"}", cancellationToken).ConfigureAwait(false);
            if (found is null) return Missing;
            var rows = DeviceRow().Matches(found).ToList();
            if (rows.Count == 0) return "**Every device is reachable.** LibreNMS reports none down.";

            var text = new StringBuilder($"**{Count(rows.Count, "device is", "devices are")} unreachable.**\n\n| Device | Address | Location |\n|---|---|---|\n");
            foreach (Match row in rows)
            {
                text.Append(CultureInfo.InvariantCulture, $"| {row.Groups["device"].Value} | {row.Groups["ip"].Value} | {row.Groups["location"].Value} |\n");
            }

            text.Append("\nOpen a device for its last poll and what it connects to.");
            return text.ToString().TrimEnd();
        }

        private async Task<string> TrafficAsync(CancellationToken cancellationToken)
        {
            var graph = await LookAsync("graph", "{\"device\":\"core-sw-01\",\"type\":\"device_bits\"}", cancellationToken).ConfigureAwait(false);
            if (graph is null) return Missing;
            var device = DeviceMarker().Match(graph) is { Success: true } marker ? " " + marker.Value : "";
            return $"**Here's core-sw-01{device}'s traffic over the last day**, inbound and outbound, from the demo network's graph.";
        }

        private const string Missing = "The demo network didn't answer that lookup. Try again, or ask another question.";

        private static string Count(int n, string one, string many) => n.ToString(CultureInfo.InvariantCulture) + " " + (n == 1 ? one : many);
    }

    [GeneratedRegex(@"Visible devices: (?<total>\d+) total, (?<up>\d+) up, (?<down>\d+) down")]
    private static partial Regex DeviceCounts();

    [GeneratedRegex(@"Open alerts: (?<open>\d+) .*?; (?<critical>\d+) critical, (?<warning>\d+) warning")]
    private static partial Regex AlertCounts();

    [GeneratedRegex(@"^(?<severity>Critical|Warning|Ok|Unknown); (?<state>\w+); (?<alert>.+? \[\[alert:\d+\]\]) on (?<device>.+? \[\[device:\d+\]\]) \([^)]*\); open (?<age>.+)$", RegexOptions.Multiline)]
    private static partial Regex AlertRow();

    [GeneratedRegex(@"^(?<device>[^\n]+? \[\[device:\d+\]\]): Down; IP (?<ip>[^;]+);.*?location (?<location>[^\n]+)$", RegexOptions.Multiline)]
    private static partial Regex DeviceRow();

    [GeneratedRegex(@"\[\[device:\d+\]\]")]
    private static partial Regex DeviceMarker();
}
