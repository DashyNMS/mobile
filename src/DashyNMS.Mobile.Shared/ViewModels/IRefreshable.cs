using CommunityToolkit.Mvvm.Input;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// A page's view model that can load its data again - what pull to refresh
/// runs. Lets the app reload whichever page is showing when something it
/// depends on changes underneath it: a certificate trusted mid-session (#141).
/// </summary>
public interface IRefreshable
{
    IAsyncRelayCommand RefreshCommand { get; }
}
