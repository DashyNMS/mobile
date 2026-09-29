using System.Collections.ObjectModel;

namespace DashyNMS.Mobile.DeviceSections;

/// <summary>How a row's value is coloured.</summary>
public enum RowStatus
{
    None,
    Ok,
    Warning,
    Critical,

    /// <summary>Switched off or not in use (an admin-down port, a disabled sensor).</summary>
    Inactive,
}

/// <summary>
/// One line in a Device View section - a sensor, a port, an ARP entry. Every
/// section is turned into these, so one page template shows them all.
/// </summary>
public sealed record SectionRow(string Title)
{
    public string? Subtitle { get; init; }

    /// <summary>The subtitle is code or fields (a rule's condition, "ifOperStatus: down"), drawn monospaced.</summary>
    public bool IsCode { get; init; }

    /// <summary>The reading, on the right - "42.5 °C", "Established", "99.98%".</summary>
    public string? Value { get; init; }

    public RowStatus Status { get; init; }

    /// <summary>A third, smaller line.</summary>
    public string? Detail { get; init; }

    /// <summary>0-1, drawn as a usage bar (CPU, memory, disk).</summary>
    public double? Bar { get; init; }

    /// <summary>Nesting level (inventory), for indenting.</summary>
    public int Depth { get; init; }

    /// <summary>Another LibreNMS device this row leads to (a neighbour).</summary>
    public int? LinkDeviceId { get; init; }

    /// <summary>
    /// A number the row can be ranked by - a port's traffic (bytes/s in and
    /// out), an availability window's length in seconds - for the device
    /// page's quick views (busiest ports, the 30-day figure).
    /// </summary>
    public double? SortValue { get; init; }

    /// <summary>A port of this device whose graphs this row leads to - its SNMP ifName, which LibreNMS's port graph API takes.</summary>
    public string? LinkPortIfName { get; init; }

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    /// <summary>A subtitle to draw as code (see <see cref="IsCode"/>).</summary>
    public bool HasCode => IsCode && HasSubtitle;

    /// <summary>A subtitle to draw as ordinary text.</summary>
    public bool HasText => !IsCode && HasSubtitle;

    public bool HasValue => !string.IsNullOrWhiteSpace(Value);

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public bool HasBar => Bar is not null;

    public bool IsLink => LinkDeviceId is not null || LinkPortIfName is not null;

    /// <summary>Left padding for <see cref="Depth"/>.</summary>
    public double Indent => Depth * 14;

    /// <summary>Any of the row's text containing <paramref name="term"/>, for the section's search box.</summary>
    public bool Matches(string term) =>
        Contains(Title, term) || Contains(Subtitle, term) || Contains(Value, term) || Contains(Detail, term);

    private static bool Contains(string? field, string term) =>
        field?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
}

/// <summary>A titled run of rows ("Temperature", "BGP sessions").</summary>
public sealed class SectionGroup : ObservableCollection<SectionRow>
{
    public SectionGroup(string name, IEnumerable<SectionRow> rows)
        : base(rows)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>Alert detail's "Why it fired": its card carries the "Show all fields" link (#69).</summary>
    public bool HasFields { get; init; }
}
