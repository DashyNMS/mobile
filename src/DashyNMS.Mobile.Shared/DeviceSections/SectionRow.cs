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

    /// <summary>
    /// Named values drawn one to a line, name then value - an alert fault's
    /// fields in "Why it fired", as the mock-up, where one block of
    /// "name: value" text ran the reasons together (Batch 11).
    /// </summary>
    public IReadOnlyList<SectionField> Fields { get; init; } = [];

    public bool HasFields => Fields.Count > 0;

    /// <summary>
    /// Part of one block rather than a row in a list - alert detail's rule,
    /// its notes and procedure - so no line above it.
    /// </summary>
    public bool IsStacked { get; init; }

    /// <summary>The line above a row in a list (see <see cref="IsStacked"/>).</summary>
    public bool HasDivider => !IsStacked;

    /// <summary>A stacked row's title is a label for its text ("Notes"), drawn small, not a heading.</summary>
    public bool HasCaptionTitle => IsStacked && !IsCode;

    /// <summary>The title as a heading - every row but a stacked one's label.</summary>
    public bool HasHeadingTitle => !HasCaptionTitle;

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
        Contains(Title, term) || Contains(Subtitle, term) || Contains(Value, term) || Contains(Detail, term)
        || Fields.Any(f => Contains(f.Name, term) || Contains(f.Value, term));

    private static bool Contains(string? field, string term) =>
        field?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
}

/// <summary>
/// One of a row's <see cref="SectionRow.Fields"/>. Secondary fields are the
/// ones the rule doesn't test, shown fainter after "Show all fields".
/// </summary>
public sealed record SectionField(string Name, string Value, bool IsSecondary = false);

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

    /// <summary>A link at the foot of the group's card - "Show all fields".</summary>
    public string? FooterText { get; set; }

    public System.Windows.Input.ICommand? FooterCommand { get; set; }

    public bool HasFooter => FooterCommand is not null && !string.IsNullOrEmpty(FooterText);
}
