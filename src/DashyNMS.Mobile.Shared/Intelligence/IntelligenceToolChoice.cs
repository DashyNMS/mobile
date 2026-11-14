using System.Text.RegularExpressions;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// What one question's context holds, in the model's own tokens (#637).
/// </summary>
/// <param name="ContextSize">The model's reported context; 4,096 on iOS 26.4, 8,192 on iOS 27.2, 32,000 on Private Cloud Compute.</param>
/// <param name="Instructions">The instructions' cost.</param>
/// <param name="Prompt">The question and the earlier turns sent with it.</param>
/// <param name="Output">Room kept for the answer, by detail level.</param>
/// <param name="Result">One lookup result at its full 900 characters.</param>
/// <param name="Tools">Each tool definition's cost: its name, description and parameter schema.</param>
public sealed record IntelligenceBudget(int ContextSize, int Instructions, int Prompt, int Output, int Result, IReadOnlyDictionary<string, int> Tools)
{
    /// <summary>Everything fits within this share of the context, leaving the model's own framing room (#570).</summary>
    public const double Usable = 0.8;

    /// <summary>Tokens left for tool definitions and results once instructions, prompt and answer are counted.</summary>
    public int Room => (int)(ContextSize * Usable) - Instructions - Prompt - Output;
}

/// <summary>One question's tools, its lookup limit, and the named tools it doesn't get.</summary>
/// <param name="Tools">The tools offered, most relevant first.</param>
/// <param name="MaxLookups">How many lookups this question may make.</param>
/// <param name="NotOffered">Tools an offered description names that this question doesn't get, for the instructions to mention.</param>
public sealed record IntelligenceChoice(IReadOnlyList<IntelligenceLookup> Tools, int MaxLookups, IReadOnlyList<string> NotOffered);

/// <summary>
/// Chooses each question's tools and lookup limit from what was asked and what the context holds (#637).
/// </summary>
/// <remarks>
/// <para>
/// Desktop offers every tool on every question and allows 48 lookups. MCP's descriptions alone come to
/// about 6,000 tokens, so on the device's 4,096 or 8,192 the full set can't be offered. Each question
/// gets a fresh session, so its tools are chosen per question: those its words point to, those the
/// chat's Spotlight and earlier questions point to, then the tools their descriptions name, while
/// they fit. Costs are the model's own token counts, measured when the question is asked.
/// </para>
/// <para>
/// The lookup limit is what's left after the tools, at one full result per lookup: two at 4,096 tokens,
/// more as the context grows, up to desktop's 48. Tools are dropped, least relevant first, until two
/// lookups fit, but at least one tool is always offered.
/// </para>
/// </remarks>
public static partial class IntelligenceToolChoice
{
    /// <summary>Desktop's limit (Core's <c>IntelligenceConversation.MaxToolCalls</c>), the most any context gets.</summary>
    public const int DesktopLookups = 48;

    /// <summary>The fewest lookups a question gets: 4,096 tokens holds two (#570).</summary>
    public const int MinimumLookups = 2;

    /// <summary>The words that point a question at each tool. Lower case; matched as whole words or word starts.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> Cues = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["network_overview"] = ["network", "overview", "how is", "how's", "status", "summary", "summarise", "summarize", "everything", "overall", "how many", "dashboard", "doing", "brief"],
        ["active_alerts"] = ["alert", "alarm", "critical", "warning", "fault", "firing", "acknowledg", "unacknowledg", "attention", "severe", "severity", "problem", "issue", "wrong"],
        ["find_device"] = ["find", "search", "which device", "which devices", "where", "list", "mac", "address", "location", "site", "group", "down", "disabled", "offline"],
        ["device_health"] = ["health", "uptime", "os", "running", "hardware", "model", "reboot", "version", "firmware"],
        ["device_sensors"] = ["sensor", "temperature", "temp", "hot", "fan", "optic", "dbm", "power", "voltage", "psu", "humidity", "signal", "current"],
    };

    /// <summary>The tools a question about one device wants, besides any its words point to.</summary>
    private static readonly string[] DeviceTools = ["device_health", "active_alerts", "device_sensors"];

    /// <summary>A general question, or one that names nothing a tool covers, starts here.</summary>
    private static readonly string[] General = ["network_overview", "active_alerts"];

    /// <summary>
    /// Chooses the tools and lookup limit.
    /// </summary>
    /// <param name="question">What was asked.</param>
    /// <param name="earlier">Earlier questions in this chat, newest last; a follow-up often leaves its subject out.</param>
    /// <param name="spotlight">What the chat is narrowed to (#231), or null.</param>
    /// <param name="available">The tools the phone has for this question.</param>
    /// <param name="budget">The measured costs.</param>
    public static IntelligenceChoice Choose(string question, IReadOnlyList<string> earlier, string? spotlight, IReadOnlyList<IntelligenceLookup> available, IntelligenceBudget budget)
    {
        var order = available.Select((tool, index) => (tool.Name, index)).ToDictionary(t => t.Name, t => t.index, StringComparer.Ordinal);
        var scores = available.ToDictionary(t => t.Name, _ => 0.0, StringComparer.Ordinal);
        void Score(string text, double weight)
        {
            var words = Normalise(text);
            foreach (var (tool, cues) in Cues)
                if (scores.ContainsKey(tool))
                    scores[tool] += weight * cues.Count(cue => Matches(words, cue));
            if (NamesADevice(text))
                foreach (var tool in DeviceTools.Where(scores.ContainsKey))
                    scores[tool] += weight;
        }

        Score(question, 1);
        if (!string.IsNullOrWhiteSpace(spotlight))
        {
            // A Spotlight on a device asks about that device; on a group or location, about what's in it.
            foreach (var tool in DeviceTools.Append("find_device").Where(scores.ContainsKey))
                scores[tool] += 0.75;
        }

        for (var i = earlier.Count - 1; i >= 0 && i >= earlier.Count - 2; i--)
            Score(earlier[i], 0.5);

        if (scores.Values.All(s => s == 0))
            foreach (var tool in General.Where(scores.ContainsKey))
                scores[tool] = 1;

        var ranked = available.Where(t => scores[t.Name] > 0)
            .OrderByDescending(t => scores[t.Name]).ThenBy(t => order[t.Name])
            .ToList();
        // The tools their descriptions name come along after them, when they fit.
        foreach (var named in ranked.ToList().SelectMany(t => t.NamedTools))
            if (available.FirstOrDefault(t => t.Name == named) is { } tool && !ranked.Contains(tool))
                ranked.Add(tool);

        var room = budget.Room;
        var result = Math.Max(1, budget.Result);
        var chosen = new List<IntelligenceLookup>();
        var spent = 0;
        foreach (var tool in ranked)
        {
            var cost = budget.Tools.GetValueOrDefault(tool.Name);
            // The first tool always comes; each further one only while two full results still fit.
            if (chosen.Count == 0 || spent + cost + MinimumLookups * result <= room)
            {
                chosen.Add(tool);
                spent += cost;
            }
        }

        var lookups = budget.ContextSize <= 4096
            ? MinimumLookups
            : Math.Clamp((room - spent) / result, MinimumLookups, DesktopLookups);
        var offered = chosen.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var notOffered = chosen.SelectMany(t => t.NamedTools).Where(n => !offered.Contains(n)).Distinct(StringComparer.Ordinal).ToList();
        return new(chosen, lookups, notOffered);
    }

    /// <summary>Lower case with punctuation as spaces, padded, so cues match at word starts.</summary>
    private static string Normalise(string text) => " " + Punctuation().Replace(text.ToLowerInvariant(), " ") + " ";

    /// <summary>
    /// A cue matches at a word start; a short one (four letters or fewer) only as a whole word or its
    /// plural, so "os" isn't found in "ospf" nor "temp" in "template".
    /// </summary>
    private static bool Matches(string words, string cue)
    {
        var plain = Punctuation().Replace(cue, " ").Trim();
        return plain.Length > 4
            ? words.Contains(" " + plain, StringComparison.Ordinal)
            : words.Contains(" " + plain + " ", StringComparison.Ordinal) || words.Contains(" " + plain + "s ", StringComparison.Ordinal);
    }

    /// <summary>An address, or a name with a digit joined by a dash or dot (core-sw1, edge.rtr2): a device the question names.</summary>
    private static bool NamesADevice(string text) => DeviceName().IsMatch(text);

    [GeneratedRegex(@"[^\p{L}\p{N}']+")]
    private static partial Regex Punctuation();

    [GeneratedRegex(@"\b(?:\d{1,3}(?:\.\d{1,3}){3}|[A-Za-z][\w]*[-.][\w.-]*\d[\w.-]*|[A-Za-z][\w]*\d[\w]*[-.][\w.-]*)\b")]
    private static partial Regex DeviceName();
}
