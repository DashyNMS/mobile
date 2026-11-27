using DesktopNMS.Core.Search;

namespace DashyNMS.Mobile.Search;

/// <summary>One source's line in "What was checked".</summary>
public sealed class CoverageItemViewModel
{
    public CoverageItemViewModel(SourceCoverage coverage)
    {
        Label = coverage.Source switch
        {
            SearchSource.Arp => "ARP tables",
            SearchSource.NeighbourDiscovery => "IPv6 neighbours",
            SearchSource.ForwardingTable => "Switch MAC tables",
            SearchSource.Networks => "Interface addresses",
            SearchSource.Devices => "Monitored devices",
            SearchSource.Links => "Neighbour links",
            SearchSource.Vendors => "MAC vendors",
            _ => coverage.Source.ToString(),
        };

        Source = coverage.Source;
        State = coverage.State;
        var stateText = State switch
        {
            SourceState.Found => "data returned",
            SourceState.NoEntry => "no entries",
            SourceState.NotQueried => "not checked",
            SourceState.PermissionDenied => "permission denied",
            SourceState.Unavailable => "unavailable",
            SourceState.Failed => "failed",
            _ => coverage.State.ToString(),
        };

        IsTruncated = coverage.IsTruncated;
        StateText = IsTruncated ? $"{stateText}, limited to {coverage.Shown} of {coverage.Total}" : stateText;
        Detail = coverage.Detail;
    }

    public string Label { get; }

    public string StateText { get; }

    public SearchSource Source { get; }

    public SourceState State { get; }

    public bool IsTruncated { get; }

    public string? Detail { get; }

    /// <summary>Whether this source leaves a gap in what the result can claim.</summary>
    public bool IsBlindSpot => State is SourceState.PermissionDenied or SourceState.Unavailable or SourceState.Failed || IsTruncated;

    /// <summary>One readable line, with the explanation when Core supplies one.</summary>
    public string Summary => $"{Label}: {StateText}." + (string.IsNullOrWhiteSpace(Detail) ? "" : $" {Detail}");
}
