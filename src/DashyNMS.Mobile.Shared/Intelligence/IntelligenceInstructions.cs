using System.Collections.Concurrent;
using System.Globalization;
using DesktopNMS.Core.Intelligence;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// The model's instructions, laid out as Core's <c>Instructions/*.md</c>: the base block, the detail
/// level's block, and Spotlight when the chat has one (#638). Web search stays desktop's.
/// </summary>
/// <remarks>
/// <para>
/// On the device every token of instructions is spent on every question from a context of 4,096 or
/// 8,192, so the phone's blocks (<c>Intelligence/Instructions/*.md</c>) are short forms of Core's. Each
/// of Core's rules maps to its short form or to a reviewed exclusion with the reason, and a test fails
/// when Core gains a rule the phone hasn't mapped, so desktop's rules can't silently go missing here.
/// </para>
/// <para>
/// When the context holds Core's own text with room to spare (Private Cloud Compute, #575), the
/// model gets Core's blocks word for word, as desktop's model does, with web search marked off.
/// </para>
/// </remarks>
public static class IntelligenceInstructions
{
    /// <summary>The smallest context that gets Core's own text: Private Cloud Compute's.</summary>
    public const int CoreTextContext = 32_000;

    private const string PhonePrefix = "DashyNMS.Mobile.Intelligence.Instructions.";
    private const string CorePrefix = "DesktopNMS.Core.Intelligence.Instructions.";

    private static readonly ConcurrentDictionary<string, string> Blocks = new(StringComparer.Ordinal);

    /// <summary>
    /// The instructions for one question.
    /// </summary>
    /// <param name="detail">Brief, balanced or detailed; Full reads as detailed on the phone.</param>
    /// <param name="spotlight">What the chat is narrowed to, in the person's words, or null (#231).</param>
    /// <param name="maxLookups">The lookups this question may use (#637).</param>
    /// <param name="contextSize">The model's reported context, which decides between the phone's blocks and Core's.</param>
    /// <param name="notOffered">
    /// Tools a description names that this question doesn't offer, so the model says what it couldn't
    /// check rather than calling a tool it hasn't got (#637). Null or empty adds nothing.
    /// </param>
    public static string Build(string detail, string? spotlight, int maxLookups, int? contextSize, IReadOnlyCollection<string>? notOffered = null)
    {
        var level = Detail(detail);
        var core = contextSize >= CoreTextContext;
        var calls = maxLookups.ToString(CultureInfo.InvariantCulture);
        var blocks = new List<string>
        {
            core
                // Desktop spreads its lookups over rounds; the phone counts lookups alone, so one round per lookup.
                ? Core("base").Replace("{max_calls}", calls, StringComparison.Ordinal).Replace("{max_rounds}", calls, StringComparison.Ordinal)
                : Phone("base").Replace("{max_calls}", calls, StringComparison.Ordinal),
            core ? Core(level) : Phone(level),
        };
        if (!string.IsNullOrWhiteSpace(spotlight))
        {
            // The scope may be a LibreNMS group or location name: cleaned as lookup labels are, so it
            // can't add a line, a heading or a marker to the instructions.
            blocks.Add((core ? Core("spotlight") : Phone("spotlight")).Replace("{scope}", CompactNetworkLookups.Clean(spotlight), StringComparison.Ordinal));
        }

        if (core)
        {
            blocks.Add(Core("web-search-off"));
        }

        if (notOffered is { Count: > 0 })
        {
            blocks.Add("Not offered for this question: " + string.Join(", ", notOffered) + ". If the answer needs one, say what you couldn't check.");
        }

        return string.Join("\n\n", blocks);
    }

    /// <summary>The detail block's name: Full is desktop's alone, so it reads as Detailed here.</summary>
    internal static string Detail(string? detail)
    {
        var normalised = IntelligenceDetail.Normalize(detail);
        return normalised == IntelligenceDetail.Full ? IntelligenceDetail.Detailed : normalised;
    }

    /// <summary>One of the phone's blocks, as embedded.</summary>
    internal static string Phone(string name) => Blocks.GetOrAdd(PhonePrefix + name, static key => Read(typeof(IntelligenceInstructions).Assembly, key));

    /// <summary>One of Core's blocks, read from the pinned Core assembly, so the text is desktop's own.</summary>
    internal static string Core(string name) => Blocks.GetOrAdd(CorePrefix + name, static key => Read(typeof(IntelligenceDetail).Assembly, key));

    private static string Read(System.Reflection.Assembly assembly, string key)
    {
        using var stream = assembly.GetManifestResourceStream(key + ".md")
            ?? throw new InvalidOperationException($"The {key} instructions are missing from the build.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }
}
