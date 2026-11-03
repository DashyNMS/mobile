using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Licences;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// One component on the licences page: its notice, and its licence text
/// once opened - read from Core only then, so the page opens without
/// loading every licence.
/// </summary>
public sealed partial class LicenceItem : ObservableObject
{
    public LicenceItem(OpenSourceNotice notice) => Notice = notice;

    public OpenSourceNotice Notice { get; }

    public string Name => Notice.Name;

    public string Use => Notice.Use;

    public string Copyright => Notice.Copyright;

    /// <summary>"MIT · github.com/dotnet/maui".</summary>
    public string LicenceLine => Notice.Licence + " · " + Notice.Link.Host + Notice.Link.AbsolutePath.TrimEnd('/');
    public string LinkText => Notice.Link.AbsoluteUri;
    public string LicenceName => Notice.Licence;
    public string ToggleDescription => ToggleText + " for " + Name;

    public bool HasLicenceText => Notice.LicenceFile is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleText))]
    [NotifyPropertyChangedFor(nameof(ToggleDescription))]
    private bool _isExpanded;

    [ObservableProperty]
    private string? _licenceText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleText))]
    [NotifyPropertyChangedFor(nameof(ToggleDescription))]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    private string? _loadError;

    /// <summary>Missing feedback must not reserve a blank line in every notice.</summary>
    public bool HasLoadError => !string.IsNullOrWhiteSpace(LoadError);

    public string ToggleText => LoadError is not null ? "Retry licence" : IsExpanded ? "Hide licence" : "Show licence";

    [RelayCommand]
    private void Toggle()
    {
        if (!IsExpanded && LicenceText is null && Notice.LicenceFile is { } file)
        {
            try
            {
                LicenceText = OpenSourceNotices.ReadLicence(file);
            }
            catch (IOException) { LicenceText = null; }
            if (string.IsNullOrWhiteSpace(LicenceText))
            {
                LicenceText = null;
                LoadError = "The embedded licence text couldn't be read. Retry or open the website below.";
                return;
            }
            LoadError = null;
        }

        IsExpanded = !IsExpanded;
    }
}

/// <summary>
/// Settings › About › Open-source licences (#164): everything third-party
/// this build ships - Core's shared entries and the phone's own (#165) -
/// each with its copyright and licence, and the trademark line.
/// </summary>
public sealed partial class LicencesViewModel : ObservableObject
{
    private readonly ILauncherService? _launcher;

    public LicencesViewModel(INoticePlatform platform, ILauncherService? launcher = null)
    {
        _launcher = launcher;
        Items = PhoneNotices.For(platform.Platform).Select(n => new LicenceItem(n)).ToList();
    }

    public IReadOnlyList<LicenceItem> Items { get; }

    public string Trademarks => OpenSourceNotices.Trademarks;

    [RelayCommand]
    private Task OpenAsync(LicenceItem? item) =>
        item is null || _launcher is null ? Task.CompletedTask : _launcher.OpenAsync(item.Notice.Link);
}
