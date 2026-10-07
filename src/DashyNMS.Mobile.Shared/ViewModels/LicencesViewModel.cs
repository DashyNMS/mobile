using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// One component on the licences page: its notice, and its licence text
/// once opened - read only then, so the page opens without loading every
/// licence.
/// </summary>
public sealed partial class LicenceItem : ObservableObject
{
    private readonly ILicenceTexts _texts;

    public LicenceItem(OpenSourceNotice notice, ILicenceTexts texts)
    {
        Notice = notice;
        _texts = texts;
    }

    public OpenSourceNotice Notice { get; }

    public string Name => Notice.Name;

    public string Use => Notice.Use;

    public string Copyright => Notice.Copyright;

    /// <summary>"MIT · github.com/dotnet/maui".</summary>
    public string LicenceLine => Notice.Licence + " · " + Notice.Link.Host + Notice.Link.AbsolutePath.TrimEnd('/');

    public bool HasLicenceText => Notice.LicenceFile is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleText))]
    private bool _isExpanded;

    [ObservableProperty]
    private string? _licenceText;

    public string ToggleText => IsExpanded ? "Hide licence" : "Show licence";

    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (!IsExpanded && LicenceText is null && Notice.LicenceFile is { } file)
        {
            try
            {
                LicenceText = await _texts.ReadAsync(file);
            }
            catch (Exception)
            {
                LicenceText = "The licence text couldn't be read. It's at " + Notice.Link + ".";
            }
        }

        IsExpanded = !IsExpanded;
    }
}

/// <summary>
/// Settings › About › Open-source licences (#164): everything third-party
/// this build ships, each with its copyright and licence, and the trademark
/// line.
/// </summary>
public sealed partial class LicencesViewModel : ObservableObject
{
    private readonly ILauncherService? _launcher;

    public LicencesViewModel(ILicenceTexts texts, ILauncherService? launcher = null)
    {
        _launcher = launcher;
        Items = OpenSourceNotices.For(texts.IsAndroid).Select(n => new LicenceItem(n, texts)).ToList();
    }

    public IReadOnlyList<LicenceItem> Items { get; }

    public string Trademarks => OpenSourceNotices.Trademarks;

    [RelayCommand]
    private Task OpenAsync(LicenceItem? item) =>
        item is null || _launcher is null ? Task.CompletedTask : _launcher.OpenAsync(item.Notice.Link);
}
