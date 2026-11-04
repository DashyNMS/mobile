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
    /// <summary>Routing facts wrap in both the section and its overview highlights.</summary>
    public bool RoutingLayout => RoutingKind is not null;
    /// <summary>Identifies protocol context without inferring it from server-provided text.</summary>
    public string? RoutingKind { get; init; }
    /// <summary>Distinguishes the fallback title from a real VRF whose name is VRF.</summary>
    public bool RoutingNameMissing { get; init; }
    private bool IsRoutingVrf => RoutingKind == "VRF";
    /// <summary>Names absent identities without replacing the raw server title.</summary>
    public string RoutingTitleText => IsRoutingVrf && (MissingAddress(Title) || RoutingNameMissing)
        ? "VRF name not reported" : MissingAddress(Title) ? "Routing identity not reported" : Title;
    /// <summary>Distinguishes a VRF distinguisher from a protocol state, including missing values.</summary>
    public string RoutingValueText => HasValue ? (IsRoutingVrf ? "Route distinguisher: " + Value : Value!)
        : IsRoutingVrf ? "Route distinguisher not reported" : "State not reported";
    /// <summary>States the existing severity in words while leaving neutral VRFs unclassified.</summary>
    public string? RoutingStatusText => Status == RowStatus.None ? null : HealthStatusText;
    public bool HasRoutingStatus => RoutingStatusText is not null;
    /// <summary>Announces the complete facts and existing severity once, independently of colour.</summary>
    public string RoutingDescription => string.Join(", ", new[] { RoutingKind, RoutingTitleText, Subtitle, RoutingValueText, RoutingStatusText, Detail }
        .Where(text => !string.IsNullOrWhiteSpace(text)));

    /// <summary>ARP facts wrap independently to retain address and interface context.</summary>
    public bool ArpLayout { get; init; }
    /// <summary>Names missing addresses without replacing the source fields.</summary>
    public string ArpIpText => MissingAddress(Title) ? "IP address not reported" : Title;
    public string ArpMacText => MissingAddress(Subtitle) ? "MAC address not reported" : Subtitle!;
    /// <summary>An unmapped interface keeps its port identity rather than disappearing.</summary>
    public string ArpInterfaceText => HasValue ? Value! : "Interface not reported";
    public string ArpDescription => string.Join(", ", ArpIpText, ArpMacText, ArpInterfaceText);

    private static bool MissingAddress(string? text) => string.IsNullOrWhiteSpace(text) || text is "—" or "–";

    /// <summary>FDB facts wrap independently so VLAN context cannot crowd the MAC or interface.</summary>
    public bool FdbLayout { get; init; }
    /// <summary>Names unavailable MAC data without changing the raw title used by other sections.</summary>
    public string FdbMacText => string.IsNullOrWhiteSpace(Title) || Title is "—" or "–" ? "MAC address not reported" : Title;

    /// <summary>Missing VLAN data must not imply untagged membership.</summary>
    public string FdbVlanText => HasValue ? Value! : "VLAN not reported";
    /// <summary>Announces complete FDB facts, including named unavailable values, once.</summary>
    public string FdbDescription => string.Join(", ", FdbMacText, Subtitle, FdbVlanText);

    /// <summary>VLAN facts stack so counts cannot crowd names at large text sizes.</summary>
    public bool VlanLayout { get; init; }

    /// <summary>Missing server fields remain unknown, rather than implying an empty name or zero ports.</summary>
    public string VlanNameText => HasSubtitle ? Subtitle! : "Name unavailable";
    public string VlanPortsText => HasValue ? Value! : "No member ports reported";
    public string VlanDescription => string.Join(", ", Title, VlanNameText, VlanPortsText);

    /// <summary>Only device neighbours use the complete endpoint readout.</summary>
    public bool NeighbourLayout { get; init; }
    public string NeighbourSummary => string.Join("\n", new[] { Title, Subtitle, Value, Detail }
        .Where(text => !string.IsNullOrWhiteSpace(text))
        .Concat(Fields.Select(f => f.Name + ": " + f.Value)));
    public string NeighbourAction => CanOpenNeighbour ? NeighbourSummary + "\nOpen neighbouring device" : NeighbourSummary;
    public bool CanOpenNeighbour => NeighbourLayout && LinkDeviceId is > 0;
    public bool IsUnmonitoredNeighbour => NeighbourLayout && !CanOpenNeighbour;
    /// <summary>Port facts wrap independently of the legacy two-column rows.</summary>
    public bool PortLayout => Port is not null;
    /// <summary>Display-only palette; ranking and grouping retain the source classification.</summary>
    public RowStatus PortVisualStatus { get; init; }
    public bool PortHasStatusBorder => PortVisualStatus != RowStatus.None;
    public string? PortVisualLabel { get; init; }
    /// <summary>Separate from Fields so new visible facts do not change saved search semantics.</summary>
    public IReadOnlyList<SectionField> PortDisplayFacts { get; init; } = [];
    public string PortFactsDescription => string.Join(", ", new[] { Title, PortVisualLabel }
        .Concat(PortDisplayFacts.Select(f => f.Name + ": " + f.Value)));
    public string PortOverviewIdentity => string.Join("\n", new[] { Title }
        .Concat(PortDisplayFacts.Where(f => f.Name is "ifName" or "Alias").Select(f => f.Value)));
    public string PortOverviewTraffic => string.Join("\n", PortDisplayFacts
        .Where(f => f.Name is "Traffic in" or "Traffic out").Select(f => f.Name + ": " + f.Value));
    public string PortOverviewDescription => string.Join(", ", PortOverviewIdentity, PortVisualLabel, PortOverviewTraffic);
    public string PortActionDescription => PortFactsDescription + (LinkPortIfName is not null
        ? ", Open interface graphs" : ", Interface graphs unavailable: ifName not reported");
    public string? PortStatusText { get; init; }
    public string PortSummary => string.Join(", ", new[] { ContentDescription, Value == PortStatusText ? null : PortStatusText }
        .Where(text => !string.IsNullOrWhiteSpace(text)));
    public string PortDescription => string.Join(", ", new[] { PortSummary,
        LinkPortIfName is not null ? "Open interface graphs" : "Interface graphs unavailable" }
        .Where(text => !string.IsNullOrWhiteSpace(text)));
    /// <summary>Only resource readings use the wrapping usage presentation.</summary>
    public bool ResourceLayout { get; init; }
    public string? ResourceKind { get; init; }
    public string ResourceStatusText => Status == RowStatus.None ? "Reading unavailable" : HealthStatusText;
    public string ResourceDescription => string.Join(", ", new[] { Title, ResourceKind, Status == RowStatus.None && Value is "—" or "–" ? null : Value, Subtitle, ResourceStatusText }
        .Where(text => !string.IsNullOrWhiteSpace(text)));
    /// <summary>Only Device Sensors opts into wrapping, vertically ordered reading facts.</summary>
    public bool SensorLayout { get; init; }
    public string? SensorKind { get; init; }
    public string? SensorStatusText { get; init; }
    public string SensorDescription => string.Join(", ", new[] { Title, SensorKind, Status == RowStatus.None && Value is "—" or "–" ? null : Value, Subtitle, SensorStatusText }
        .Where(text => !string.IsNullOrWhiteSpace(text)));
    /// <summary>Availability facts stack so dates and durations remain readable at large text sizes.</summary>
    public bool AvailabilityLayout { get; init; }
    public string? Subtitle { get; init; }

    /// <summary>The subtitle is code or fields (a rule's condition, "ifOperStatus: down"), drawn monospaced.</summary>
    public bool IsCode { get; init; }

    /// <summary>The reading, on the right - "42.5 °C", "Established", "99.98%".</summary>
    public string? Value { get; init; }

    public RowStatus Status { get; init; }

    /// <summary>Health rows state unavailable readings explicitly, independently of colour.</summary>
    public string HealthStatusText => Status switch
    {
        RowStatus.Critical => "Critical",
        RowStatus.Warning => "Warning",
        RowStatus.Ok => "OK",
        RowStatus.Inactive => "Inactive",
        _ => "Unavailable",
    };

    /// <summary>Announces Health readings without speaking a missing-value placeholder.</summary>
    public string HealthDescription => string.Join(", ",
        new[] { Title, Subtitle, Status == RowStatus.None && Value == "–" ? null : Value, Detail, HealthStatusText }
            .Where(text => !string.IsNullOrWhiteSpace(text)));

    /// <summary>Announces complete row facts without punctuation for absent fields.</summary>
    public string ContentDescription => string.Join(", ",
        new[] { Title, Subtitle, AvailabilityLayout && Status == RowStatus.None && Value is "—" or "–" ? null : Value, Detail }.Where(text => !string.IsNullOrWhiteSpace(text)));

    /// <summary>A third, smaller line.</summary>
    public string? Detail { get; init; }

    /// <summary>0-1, drawn as a usage bar (CPU, memory, disk).</summary>
    public double? Bar { get; init; }

    /// <summary>Nesting level (inventory), for indenting.</summary>
    public int Depth { get; init; }

    /// <summary>Inventory identity and metadata retained for tree controls and serial copying.</summary>
    public long? InventoryIndex { get; init; }
    public bool IsInventoryPort { get; init; }
    public string? Serial { get; init; }
    public bool HasSerial => !string.IsNullOrWhiteSpace(Serial);
    public bool CanExpand { get; init; }
    public bool IsExpanded { get; init; }
    public string ExpansionGlyph => IsExpanded ? "⌄" : "›";
    public string ExpansionLabel => (IsExpanded ? "Collapse " : "Expand ") + Title;

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

    /// <summary>A port row's facts, for the Ports page to sort and group by (#92); null on every other row.</summary>
    public PortFacts? Port { get; init; }

    /// <summary>
    /// Part of one block rather than a row in a list - alert detail's rule,
    /// its notes and procedure - so its title is a small label. The lines
    /// between rows no longer depend on it: every pair of rows in a card has
    /// one (#120).
    /// </summary>
    public bool IsStacked { get; init; }

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
        || (PortLayout && (Contains(PortStatusText, term) || Contains(LinkPortIfName, term)))
        || (ResourceLayout && (Contains(ResourceKind, term) || Contains(ResourceStatusText, term)))
        || (SensorLayout && (Contains(SensorKind, term) || Contains(SensorStatusText, term)))
        || (RoutingLayout && (Contains(RoutingKind, term) || Contains(RoutingTitleText, term) || Contains(RoutingValueText, term) || Contains(RoutingStatusText, term)))
        || (ArpLayout && (Contains(ArpIpText, term) || Contains(ArpMacText, term) || Contains(ArpInterfaceText, term)))
        || (FdbLayout && (Contains(FdbMacText, term) || Contains(FdbVlanText, term)))
        || (VlanLayout && (Contains(VlanNameText, term) || Contains(VlanPortsText, term)))
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
