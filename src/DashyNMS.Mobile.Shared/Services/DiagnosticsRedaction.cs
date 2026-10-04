using System.Text.RegularExpressions;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// "Hide names" for shared diagnostics (#126): IP addresses, the server's
/// name and the device names the app knows become placeholders - "ip-1",
/// "host-1", "device-1" - the same placeholder each time, so the log still
/// reads ("device-2 went down, device-2 came back") without saying whose
/// network it is.
/// </summary>
public sealed partial class DiagnosticsRedaction
{
    private readonly IReadOnlyList<(string Name, string Kind)> _names;
    private readonly Dictionary<string, string> _placeholders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _counts = new();

    /// <param name="hosts">The server's names - its address and backup address.</param>
    /// <param name="devices">Device names, hostnames and sysNames the app knows.</param>
    public DiagnosticsRedaction(IEnumerable<string?> hosts, IEnumerable<string?> devices)
    {
        // Longest first, so "core-sw-01.example.net" goes before "core-sw-01".
        _names = hosts.Select(h => (Name: h, Kind: "host"))
            .Concat(devices.Select(d => (Name: d, Kind: "device")))
            .Where(n => !string.IsNullOrWhiteSpace(n.Name) && n.Name!.Trim().Length >= 3)
            .Select(n => (n.Name!.Trim(), n.Kind))
            .DistinctBy(n => n.Item1, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(n => n.Item1.Length)
            .ToList();
    }

    public string Apply(string text)
    {
        foreach (var (name, kind) in _names)
        {
            text = Regex.Replace(text, $@"(?<![\w.-]){Regex.Escape(name)}(?![\w-])", _ => Placeholder(name, kind), RegexOptions.IgnoreCase);
        }

        text = Ipv4().Replace(text, m => Placeholder(m.Value, "ip"));
        return Ipv6().Replace(text, m => Placeholder(m.Value, "ip"));
    }

    private string Placeholder(string value, string kind)
    {
        if (!_placeholders.TryGetValue(value, out var placeholder))
        {
            _counts[kind] = _counts.GetValueOrDefault(kind) + 1;
            placeholder = _placeholders[value] = $"{kind}-{_counts[kind]}";
        }

        return placeholder;
    }

    [GeneratedRegex(@"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])")]
    private static partial Regex Ipv4();

    // Groups of hex with at least two colons - not a time like 09:30:00.000.
    [GeneratedRegex(@"(?<![\w:])(?=[0-9a-fA-F:]*[a-fA-F])(?:[0-9a-fA-F]{1,4}:){2,7}[0-9a-fA-F]{0,4}(?![\w:])|(?<![\w:])(?:[0-9a-fA-F]{1,4}:){1,7}:(?:[0-9a-fA-F]{1,4}(?::[0-9a-fA-F]{1,4})*)?(?![\w:])")]
    private static partial Regex Ipv6();
}
