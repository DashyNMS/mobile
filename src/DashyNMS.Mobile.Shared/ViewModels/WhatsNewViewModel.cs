using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Updates;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>One change, with the app's own glyph for its <c>{icon}</c>.</summary>
public sealed record WhatsNewRow(string Title, string Text, string? Image)
{
    public bool HasImage => Image is not null;
}

/// <summary>"New", "Improved" or "Fixed", and its changes.</summary>
public sealed record WhatsNewGroup(string Name, IReadOnlyList<WhatsNewRow> Rows);

/// <summary>The What's new sheet (#159): the release, its date, and its New, Improved and Fixed changes.</summary>
public sealed partial class WhatsNewViewModel
{
    private readonly WhatsNew _whatsNew;
    private readonly INavigationService _navigation;
    private readonly ILauncherService? _launcher;

    public WhatsNewViewModel(WhatsNew whatsNew, INavigationService navigation, ILauncherService? launcher = null)
    {
        _whatsNew = whatsNew;
        _navigation = navigation;
        _launcher = launcher;
        Groups = whatsNew.Notes?.Sections
            .Select(s => new WhatsNewGroup(s.Name, s.Items.Select(i => new WhatsNewRow(i.Title, i.Text, ImageFor(i.Icon))).ToList()))
            .ToList() ?? [];
    }

    /// <summary>"What's new in 2.0.0".</summary>
    public string Title => $"What's new in {_whatsNew.Notes?.Version ?? _whatsNew.CurrentVersion}";

    public string? Released => _whatsNew.Notes?.Released;

    public bool HasReleased => !string.IsNullOrWhiteSpace(Released);

    public IReadOnlyList<WhatsNewGroup> Groups { get; }

    /// <summary>Every release's full notes, on GitHub.</summary>
    public static Uri ReleaseNotes { get; } = new("https://github.com/DashyNMS/mobile/releases");

    [RelayCommand]
    private Task GotItAsync()
    {
        _whatsNew.MarkShown();
        return _navigation.GoToAsync(Routes.Back);
    }

    [RelayCommand]
    private Task OpenReleaseNotesAsync() => _launcher?.OpenAsync(ReleaseNotes) ?? Task.CompletedTask;

    /// <summary>The notes' <c>{icon}</c> names, as the tab bar's glyphs; an unknown name has none.</summary>
    internal static string? ImageFor(string? icon) => icon?.ToLowerInvariant() switch
    {
        "alerts" or "bell" => "tab_alerts.png",
        "dashboard" or "grid" => "tab_dashboard.png",
        "devices" => "tab_devices.png",
        "health" or "sensor" => "tab_health.png",
        "logs" => "tab_logs.png",
        "graylog" => "tab_graylog.png",
        "map" => "tab_map.png",
        "networkmap" => "tab_networkmap.png",
        "neighbours" => "tab_neighbours.png",
        "rules" => "tab_alertrules.png",
        "groups" => "tab_groups.png",
        "settings" or "check" => "tab_settings.png",
        _ => null,
    };
}
