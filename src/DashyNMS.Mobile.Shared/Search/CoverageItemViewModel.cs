using DesktopNMS.Core;
using DesktopNMS.Core.Models;
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
            SearchSource.Devices => "Devices",
            SearchSource.Links => "Neighbour links",
            SearchSource.Vendors => "MAC vendors",
            _ => coverage.Source.ToString(),
        };

        StateText = coverage.State switch
        {
            SourceState.Found when coverage.IsTruncated => $"found, {coverage.Shown} of {coverage.Total} checked",
            SourceState.Found => "found",
            SourceState.NoEntry => "nothing",
            SourceState.NotQueried => "not checked",
            SourceState.PermissionDenied => "no permission",
            SourceState.Unavailable => "unavailable",
            SourceState.Failed => "failed",
            _ => coverage.State.ToString(),
        };

        Tone = coverage.State switch
        {
            SourceState.Found when coverage.IsTruncated => AlertSeverity.Warning,
            SourceState.Found => AlertSeverity.Ok,
            SourceState.PermissionDenied or SourceState.Failed => AlertSeverity.Critical,
            SourceState.Unavailable => AlertSeverity.Warning,
            _ => AlertSeverity.Unknown,
        };

        Detail = coverage.Detail ?? Label + ": " + StateText;
        IsBlindSpot = coverage.State is SourceState.PermissionDenied or SourceState.Unavailable or SourceState.Failed || coverage.IsTruncated;
    }

    public string Label { get; }

    public string StateText { get; }

    public AlertSeverity Tone { get; }

    public string Detail { get; }

    /// <summary>Whether this source leaves a gap in what the result can claim.</summary>
    public bool IsBlindSpot { get; }

    /// <summary>Label, state and the reason, so screen readers get what the tooltip shows.</summary>
    public string AccessibleText => Detail == Label + ": " + StateText ? Detail : $"{Label}: {StateText}. {Detail}";
}
