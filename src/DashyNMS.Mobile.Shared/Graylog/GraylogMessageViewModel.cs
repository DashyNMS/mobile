using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Graylog;

/// <summary>
/// One Graylog message on its own page (#117), or beside the list on a
/// larger screen (#88): the full text, where it came from, every field
/// Graylog has, and the way to its device or to only its messages.
/// </summary>
/// <remarks>
/// The message comes from the list rather than being fetched again - the
/// list's search already returned every field.
/// </remarks>
public sealed partial class GraylogMessageViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private GraylogViewModel? _list;

    /// <summary>Beside the list there's no page to go back from - "Show only" just sets the chip.</summary>
    public bool IsBesideList { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage), nameof(MissingMessage), nameof(HasDevice), nameof(CanShowOnly), nameof(ShowOnlyText), nameof(HasActions), nameof(DeviceActionDescription))]
    [NotifyCanExecuteChangedFor(nameof(OpenDeviceCommand), nameof(ShowOnlyCommand))]
    private GraylogMessageItem? _message;

    public GraylogMessageViewModel(INavigationService navigation)
    {
        _navigation = navigation;
    }

    public bool HasMessage => Message is not null;
    public bool MissingMessage => !HasMessage;
    public string DeviceActionDescription => string.Join(" · ", new[] { "Open device", Message?.DeviceName, Message?.DeviceId?.ToString(), Message?.LevelText }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>LibreNMS knows the device - and it isn't the one whose Graylog page this came from.</summary>
    public bool HasDevice => Message?.DeviceId is > 0 and var id && id != _list?.DeviceId;

    public bool CanShowOnly => Message is { } message && _list?.CanShowOnly(message) == true;

    /// <summary>Either of the two - their card hides with neither.</summary>
    public bool HasActions => HasDevice || CanShowOnly;

    /// <summary>"Show only core-sw-01's messages".</summary>
    public string ShowOnlyText => Message?.Filter is { } filter ? $"Show only {filter.Name}'s messages" : string.Empty;

    /// <summary>The message tapped, and the list it was tapped in.</summary>
    public void Load(GraylogMessageItem? message, GraylogViewModel? list)
    {
        _list = list;
        Message = message;
        OnPropertyChanged(nameof(HasDevice));
        OnPropertyChanged(nameof(CanShowOnly));
        OnPropertyChanged(nameof(HasActions));
        OpenDeviceCommand.NotifyCanExecuteChanged();
        ShowOnlyCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasDevice))]
    private Task OpenDeviceAsync() => Message?.DeviceId is { } id && HasDevice
        ? _navigation.GoToAsync(Routes.DeviceDetail, new Dictionary<string, object> { [Routes.DeviceIdParameter] = id })
        : Task.CompletedTask;

    /// <summary>Sets the list's Device chip to this sender and goes back to the list.</summary>
    [RelayCommand(CanExecute = nameof(CanShowOnly))]
    private async Task ShowOnlyAsync()
    {
        if (Message?.Filter is not { } filter || _list is null || !CanShowOnly)
        {
            return;
        }

        _list.ShowDevice(filter);
        OnPropertyChanged(nameof(CanShowOnly));
        OnPropertyChanged(nameof(HasActions));
        ShowOnlyCommand.NotifyCanExecuteChanged();

        if (!IsBesideList)
        {
            await _navigation.GoToAsync(Routes.Back);
        }
    }
}
